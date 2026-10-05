using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartWaste.Application.Common.Exceptions;
using SmartWaste.Application.Reporting.Interfaces;

namespace SmartWaste.Infrastructure.Reporting.Storage;

/// <summary>
/// Infrastructure implementation of IFileStorageService communicating with Supabase Storage REST API.
/// Uses typed HttpClient and server-side SecretKey to upload, delete, and generate
/// time-limited signed read URLs for private bucket objects.
/// </summary>
public class SupabaseFileStorageService : IFileStorageService
{
    private readonly HttpClient _httpClient;
    private readonly SupabaseStorageOptions _options;
    private readonly ILogger<SupabaseFileStorageService> _logger;

    public SupabaseFileStorageService(
        HttpClient httpClient,
        IOptions<SupabaseStorageOptions> options,
        ILogger<SupabaseFileStorageService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task UploadAsync(
        string storageKey,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        var bucket = _options.Bucket;
        var requestUri = $"storage/v1/object/{bucket}/{storageKey.TrimStart('/')}";

        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
        ApplyAuthHeaders(request);
        request.Headers.TryAddWithoutValidation("x-upsert", "false");

        using var streamContent = new StreamContent(content);
        streamContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        request.Content = streamContent;

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to connect to Supabase Storage when uploading object {StorageKey}", storageKey);
            throw new StorageServiceException("Failed to upload attachment due to a storage network connectivity failure.", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Supabase Storage upload returned HTTP {StatusCode} for object {StorageKey}",
                response.StatusCode, storageKey);
            throw new StorageServiceException(
                $"Cloud storage provider returned error HTTP {(int)response.StatusCode} during file upload.");
        }
    }

    public async Task DeleteAsync(
        string storageKey,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        var bucket = _options.Bucket;
        var requestUri = $"storage/v1/object/{bucket}";

        using var request = new HttpRequestMessage(HttpMethod.Delete, requestUri);
        ApplyAuthHeaders(request);

        var payload = JsonSerializer.Serialize(new { prefixes = new[] { storageKey.TrimStart('/') } });
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to connect to Supabase Storage when deleting object {StorageKey}", storageKey);
            throw new StorageServiceException("Failed to delete attachment due to a storage network connectivity failure.", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Supabase Storage delete returned HTTP {StatusCode} for object {StorageKey}",
                response.StatusCode, storageKey);
            throw new StorageServiceException(
                $"Cloud storage provider returned error HTTP {(int)response.StatusCode} during file deletion.");
        }
    }

    public async Task<string> GetReadUrlAsync(
        string storageKey,
        TimeSpan expiresIn,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        var bucket = _options.Bucket;
        var requestUri = $"storage/v1/object/sign/{bucket}/{storageKey.TrimStart('/')}";

        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
        ApplyAuthHeaders(request);

        var seconds = (int)Math.Max(expiresIn.TotalSeconds, 60);
        var payload = JsonSerializer.Serialize(new { expiresIn = seconds });
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to connect to Supabase Storage when signing URL for {StorageKey}", storageKey);
            throw new StorageServiceException("Failed to generate signed read URL due to a storage network connectivity failure.", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Supabase Storage sign returned HTTP {StatusCode} for object {StorageKey}",
                response.StatusCode, storageKey);
            throw new StorageServiceException(
                $"Cloud storage provider returned error HTTP {(int)response.StatusCode} during signed URL generation.");
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);

        // Supabase returns { "signedURL": "/storage/v1/object/sign/..." } or { "signedUrl": "..." }
        string? signedPath = null;
        if (doc.RootElement.TryGetProperty("signedURL", out var signedUrlProp))
        {
            signedPath = signedUrlProp.GetString();
        }
        else if (doc.RootElement.TryGetProperty("signedUrl", out var signedUrlProp2))
        {
            signedPath = signedUrlProp2.GetString();
        }

        if (string.IsNullOrWhiteSpace(signedPath))
        {
            _logger.LogError("Supabase Storage sign response did not contain a valid signedURL property for {StorageKey}", storageKey);
            throw new StorageServiceException("Cloud storage provider response was missing signed read URL.");
        }

        // A leading-slash Supabase path can be parsed as an absolute file URI on Linux.
        // Only HTTP(S) URLs are complete signed read URLs.
        if (Uri.TryCreate(signedPath, UriKind.Absolute, out var absoluteUri))
        {
            if (absoluteUri.Scheme == Uri.UriSchemeHttp || absoluteUri.Scheme == Uri.UriSchemeHttps)
            {
                return signedPath;
            }

            if (!signedPath.StartsWith('/'))
            {
                throw new StorageServiceException("Cloud storage provider returned an unsupported signed read URL.");
            }
        }

        var normalizedPath = signedPath.TrimStart('/');
        if (!normalizedPath.StartsWith("storage/v1/", StringComparison.OrdinalIgnoreCase))
        {
            normalizedPath = $"storage/v1/{normalizedPath}";
        }

        if (!Uri.TryCreate(_options.BaseUrl.TrimEnd('/') + "/", UriKind.Absolute, out var baseUri) ||
            (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException("Supabase Storage BaseUrl must use HTTP or HTTPS.");
        }

        // The relative URI contains the provider's query string, so '?' stays a query delimiter.
        return new Uri(baseUri, normalizedPath).AbsoluteUri;
    }

    private void ApplyAuthHeaders(HttpRequestMessage request)
    {
        request.Headers.TryAddWithoutValidation("apikey", _options.SecretKey);
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl) || string.IsNullOrWhiteSpace(_options.SecretKey))
        {
            throw new InvalidOperationException(
                "Supabase Storage is not configured. Missing required BaseUrl or SecretKey in Storage:Supabase configuration.");
        }
    }
}
