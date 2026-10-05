using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using SmartWaste.Application.Common.Exceptions;
using SmartWaste.Infrastructure.Reporting.Storage;
using Xunit;

namespace SmartWaste.Tests.Reporting.Storage;

/// <summary>
/// Unit tests for SupabaseFileStorageService verifying HTTP protocol compliance,
/// headers (Authorization, apikey, x-upsert), request endpoints, JSON payloads,
/// relative/absolute signed URL parsing, and error wrapping without leaking secrets.
/// </summary>
public class SupabaseFileStorageServiceTests
{
    private const string TestBaseUrl = "https://project123.supabase.co";
    private const string TestSecretKey = "sb_secret_mock_key_12345";
    private const string TestBucket = "waste-report-attachments";

    private static (SupabaseFileStorageService Service, Mock<HttpMessageHandler> Handler) CreateService(
        string baseUrl = TestBaseUrl,
        string secretKey = TestSecretKey,
        string bucket = TestBucket,
        int expirySeconds = 900)
    {
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/")
        };

        var options = Microsoft.Extensions.Options.Options.Create(new SupabaseStorageOptions
        {
            BaseUrl = baseUrl,
            SecretKey = secretKey,
            Bucket = bucket,
            SignedUrlExpirySeconds = expirySeconds
        });

        var logger = new Mock<ILogger<SupabaseFileStorageService>>().Object;
        var service = new SupabaseFileStorageService(httpClient, options, logger);

        return (service, handlerMock);
    }

    [Fact]
    public async Task UploadAsync_SendsCorrectHeaders_Endpoint_AndPayloadToSupabase()
    {
        var (service, handlerMock) = CreateService();
        var storageKey = "waste-reports/rep-1/att-1.jpg";
        var payloadBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };

        HttpRequestMessage? capturedRequest = null;
        byte[]? sentBytes = null;
        string? capturedContentType = null;

        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>(async (req, _) =>
            {
                capturedRequest = req;
                capturedContentType = req.Content?.Headers.ContentType?.MediaType;
                if (req.Content != null)
                {
                    sentBytes = await req.Content.ReadAsByteArrayAsync();
                }

                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("{\"Key\":\"waste-report-attachments/waste-reports/rep-1/att-1.jpg\"}")
                };
            });

        using var stream = new MemoryStream(payloadBytes);
        await service.UploadAsync(storageKey, stream, "image/jpeg");

        capturedRequest.Should().NotBeNull();
        capturedRequest!.Method.Should().Be(HttpMethod.Post);
        capturedRequest.RequestUri!.ToString().Should().Be($"{TestBaseUrl}/storage/v1/object/{TestBucket}/{storageKey}");

        // Headers: Verify apikey and x-upsert are sent, and Authorization (Bearer) is NOT sent
        capturedRequest.Headers.Authorization.Should().BeNull();
        capturedRequest.Headers.GetValues("apikey").Should().ContainSingle(TestSecretKey);
        capturedRequest.Headers.GetValues("x-upsert").Should().ContainSingle("false");

        // Content
        capturedContentType.Should().Be("image/jpeg");
        sentBytes.Should().Equal(payloadBytes);
    }

    [Fact]
    public async Task DeleteAsync_SendsDeleteMethod_WithPrefixesJsonPayload()
    {
        var (service, handlerMock) = CreateService();
        var storageKey = "waste-reports/rep-1/att-1.jpg";

        HttpRequestMessage? capturedRequest = null;
        string? bodyJson = null;

        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>(async (req, _) =>
            {
                capturedRequest = req;
                if (req.Content != null)
                {
                    bodyJson = await req.Content.ReadAsStringAsync();
                }

                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("[{\"name\":\"waste-reports/rep-1/att-1.jpg\"}]")
                };
            });

        await service.DeleteAsync(storageKey);

        capturedRequest.Should().NotBeNull();
        capturedRequest!.Method.Should().Be(HttpMethod.Delete);
        capturedRequest.RequestUri!.ToString().Should().Be($"{TestBaseUrl}/storage/v1/object/{TestBucket}");

        capturedRequest.Headers.Authorization.Should().BeNull();
        capturedRequest.Headers.GetValues("apikey").Should().ContainSingle(TestSecretKey);

        bodyJson.Should().NotBeNull();
        using var doc = JsonDocument.Parse(bodyJson!);
        var prefixes = doc.RootElement.GetProperty("prefixes").EnumerateArray().Select(e => e.GetString()).ToList();
        prefixes.Should().ContainSingle(storageKey);
    }

    [Fact]
    public async Task GetReadUrlAsync_HandlesRelativeSignedUrl_AndReturnsAbsoluteUrl()
    {
        var (service, handlerMock) = CreateService();
        var storageKey = "waste-reports/rep-1/att-1.jpg";

        HttpRequestMessage? capturedRequest = null;

        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"signedURL\":\"/storage/v1/object/sign/waste-report-attachments/waste-reports/rep-1/att-1.jpg?token=abc123token\"}")
            });

        var result = await service.GetReadUrlAsync(storageKey, TimeSpan.FromSeconds(900));

        capturedRequest.Should().NotBeNull();
        capturedRequest!.Method.Should().Be(HttpMethod.Post);
        capturedRequest.RequestUri!.ToString().Should().Be($"{TestBaseUrl}/storage/v1/object/sign/{TestBucket}/{storageKey}");

        capturedRequest.Headers.Authorization.Should().BeNull();
        capturedRequest.Headers.GetValues("apikey").Should().ContainSingle(TestSecretKey);

        result.Should().Be($"{TestBaseUrl}/storage/v1/object/sign/waste-report-attachments/waste-reports/rep-1/att-1.jpg?token=abc123token");
    }

    [Fact]
    public async Task GetReadUrlAsync_HandlesObjectSignRelativePath_AndPrependsStorageV1Prefix()
    {
        var (service, handlerMock) = CreateService();
        var storageKey = "waste-reports/rep-1/att-1.jpg";

        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"signedURL\":\"/object/sign/waste-report-attachments/waste-reports/rep-1/att-1.jpg?token=abc123token\"}")
            });

        var result = await service.GetReadUrlAsync(storageKey, TimeSpan.FromSeconds(900));

        result.Should().Be($"{TestBaseUrl}/storage/v1/object/sign/waste-report-attachments/waste-reports/rep-1/att-1.jpg?token=abc123token");
    }

    [Fact]
    public async Task GetReadUrlAsync_HandlesAbsoluteSignedUrl_Directly()
    {
        var (service, handlerMock) = CreateService();
        var storageKey = "waste-reports/rep-1/att-1.jpg";
        var absoluteUrl = "https://custom-cdn.supabase.co/storage/v1/object/sign/waste-report-attachments/rep-1/att-1.jpg?token=xyz";

        HttpRequestMessage? capturedRequest = null;

        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent($"{{\"signedURL\":\"{absoluteUrl}\"}}")
            });

        var result = await service.GetReadUrlAsync(storageKey, TimeSpan.FromSeconds(900));
        result.Should().Be(absoluteUrl);

        capturedRequest.Should().NotBeNull();
        capturedRequest!.Headers.Authorization.Should().BeNull();
        capturedRequest.Headers.GetValues("apikey").Should().ContainSingle(TestSecretKey);
    }

    [Fact]
    public async Task GetReadUrlAsync_HandlesAbsoluteHttpSignedUrl_Directly()
    {
        var (service, handlerMock) = CreateService();
        const string signedUrl = "http://custom-cdn.supabase.co/storage/v1/object/sign/waste-report-attachments/rep-1/att-1.jpg?token=abc123token";
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(new { signedURL = signedUrl }))
            });

        var result = await service.GetReadUrlAsync("waste-reports/rep-1/att-1.jpg", TimeSpan.FromSeconds(900));

        result.Should().Be(signedUrl);
    }

    [Theory]
    [InlineData("storage/v1/object/sign/waste-report-attachments/waste-reports/rep-1/att-1.jpg?token=abc123token")]
    [InlineData("object/sign/waste-report-attachments/waste-reports/rep-1/att-1.jpg?token=abc123token")]
    public async Task GetReadUrlAsync_HandlesRelativePathWithoutLeadingSlash(string signedPath)
    {
        var (service, handlerMock) = CreateService();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(new { signedURL = signedPath }))
            });

        var result = await service.GetReadUrlAsync("waste-reports/rep-1/att-1.jpg", TimeSpan.FromSeconds(900));

        result.Should().Be($"{TestBaseUrl}/storage/v1/object/sign/waste-report-attachments/waste-reports/rep-1/att-1.jpg?token=abc123token");
    }

    [Theory]
    [InlineData("file:///storage/v1/object/sign/waste-report-attachments/att-1.jpg?token=abc123token")]
    [InlineData("ftp://example.com/object/sign/waste-report-attachments/att-1.jpg?token=abc123token")]
    [InlineData("data:text/plain,not-a-signed-url")]
    public async Task GetReadUrlAsync_RejectsNonHttpAbsoluteSignedUrl(string signedUrl)
    {
        var (service, handlerMock) = CreateService();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(new { signedURL = signedUrl }))
            });

        var act = () => service.GetReadUrlAsync("waste-reports/rep-1/att-1.jpg", TimeSpan.FromSeconds(900));

        var exception = await act.Should().ThrowAsync<StorageServiceException>();
        exception.Which.Message.Should().Contain("unsupported signed read URL");
        exception.Which.Message.Should().NotContain("token=").And.NotContain(TestSecretKey);
    }

    [Fact]
    public async Task WhenConfigurationMissing_ThrowsInvalidOperationException_WithoutLeakingSecrets()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        var httpClient = new HttpClient(handlerMock.Object);
        var options = Microsoft.Extensions.Options.Options.Create(new SupabaseStorageOptions
        {
            BaseUrl = "",
            SecretKey = ""
        });
        var logger = new Mock<ILogger<SupabaseFileStorageService>>().Object;
        var service = new SupabaseFileStorageService(httpClient, options, logger);

        var act = () => service.UploadAsync("test.jpg", new MemoryStream(new byte[] { 1 }), "image/jpeg");

        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.Which.Message.Should().NotContain(TestSecretKey);
        ex.Which.Message.Should().Contain("Missing required BaseUrl or SecretKey");
    }

    [Fact]
    public async Task ProviderHttpError_ThrowsStorageServiceException_WithoutLeakingSecrets()
    {
        var (service, handlerMock) = CreateService();

        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError,
                Content = new StringContent("{\"error\":\"Internal Server Error\"}")
            });

        var act = () => service.UploadAsync("key.jpg", new MemoryStream(new byte[] { 1 }), "image/jpeg");

        var ex = await act.Should().ThrowAsync<StorageServiceException>();
        ex.Which.Message.Should().NotContain(TestSecretKey);
    }
}
