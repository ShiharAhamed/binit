using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SmartWaste.Application.Common.Exceptions;
using SmartWaste.Application.DTOs.Ai;
using SmartWaste.Application.Interfaces;

namespace SmartWaste.Infrastructure.Services;

/// <summary>
/// HTTP client implementation for communicating with the internal FastAPI AI service.
/// Utilizes ASP.NET Core IHttpClientFactory via typed client registration.
/// </summary>
public class AiServiceClient : IAiServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AiServiceClient> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public AiServiceClient(HttpClient httpClient, ILogger<AiServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<AiHealthDto> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Querying internal AI service health at {BaseUrl}health", _httpClient.BaseAddress);

            using var response = await _httpClient.GetAsync("health", cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "AI service health check returned non-success status code {StatusCode} ({Reason})",
                    (int)response.StatusCode,
                    response.ReasonPhrase);

                throw new AiServiceUnavailableException(
                    $"AI service returned non-success status code {(int)response.StatusCode}.");
            }

            var result = await response.Content.ReadFromJsonAsync<AiHealthDto>(JsonOptions, cancellationToken);

            if (result == null || string.IsNullOrWhiteSpace(result.Status))
            {
                _logger.LogWarning("AI service returned an empty or malformed health response payload");
                throw new AiServiceUnavailableException("AI service returned an invalid or empty response payload.");
            }

            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to connect to AI service at {BaseUrl}: {Message}", _httpClient.BaseAddress, ex.Message);
            throw new AiServiceUnavailableException("Unable to connect to the AI service. Please verify the service is running.", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "AI service health check timed out after configured duration");
            throw new AiServiceUnavailableException("The request to the AI service timed out.", ex);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize JSON response from AI service health check");
            throw new AiServiceUnavailableException("Failed to deserialize response from the AI service.", ex);
        }
        catch (AiServiceUnavailableException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error occurred during AI service health check");
            throw new AiServiceUnavailableException("An unexpected error occurred while communicating with the AI service.", ex);
        }
    }
}
