using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SmartWaste.Application.Common.Exceptions;
using SmartWaste.Infrastructure.Services;

namespace SmartWaste.Tests;

public class AiServiceClientTests
{
    private class TestHttpMessageHandler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Handler { get; set; } = null!;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Handler(request, cancellationToken);
        }
    }

    private static (AiServiceClient Client, TestHttpMessageHandler Handler) CreateTestClient()
    {
        var handler = new TestHttpMessageHandler();
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://127.0.0.1:8000/"),
            Timeout = TimeSpan.FromSeconds(300)
        };
        var client = new AiServiceClient(httpClient, NullLogger<AiServiceClient>.Instance);
        return (client, handler);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenFastApiReturns200_DeserializesResponseCorrectly()
    {
        // Arrange
        var (client, handler) = CreateTestClient();
        handler.Handler = (req, ct) =>
        {
            req.RequestUri!.PathAndQuery.Should().Be("/health");
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"status\":\"healthy\",\"service\":\"SmartWaste AI Service\"}",
                    Encoding.UTF8,
                    "application/json")
            };
            return Task.FromResult(response);
        };

        // Act
        var result = await client.CheckHealthAsync();

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be("healthy");
        result.Service.Should().Be("SmartWaste AI Service");
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task CheckHealthAsync_WhenFastApiReturnsNonSuccess_ThrowsAiServiceUnavailableException(HttpStatusCode statusCode)
    {
        // Arrange
        var (client, handler) = CreateTestClient();
        handler.Handler = (req, ct) => Task.FromResult(new HttpResponseMessage(statusCode));

        // Act & Assert
        var act = async () => await client.CheckHealthAsync();
        var ex = await act.Should().ThrowAsync<AiServiceUnavailableException>();
        ex.WithMessage($"*{(int)statusCode}*");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenConnectionRefused_ThrowsAiServiceUnavailableException_WithoutCrashing()
    {
        // Arrange
        var (client, handler) = CreateTestClient();
        handler.Handler = (req, ct) => throw new HttpRequestException("Connection refused by target host");

        // Act & Assert
        var act = async () => await client.CheckHealthAsync();
        var ex = await act.Should().ThrowAsync<AiServiceUnavailableException>();
        ex.WithMessage("*Unable to connect*");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenRequestTimesOut_ThrowsAiServiceUnavailableException()
    {
        // Arrange
        var (client, handler) = CreateTestClient();
        handler.Handler = (req, ct) => throw new TaskCanceledException("The HttpClient request timed out.");

        // Act & Assert
        var act = async () => await client.CheckHealthAsync();
        var ex = await act.Should().ThrowAsync<AiServiceUnavailableException>();
        ex.WithMessage("*timed out*");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenResponseIsInvalidJson_ThrowsAiServiceUnavailableException()
    {
        // Arrange
        var (client, handler) = CreateTestClient();
        handler.Handler = (req, ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{ not valid json }", Encoding.UTF8, "application/json")
        });

        // Act & Assert
        var act = async () => await client.CheckHealthAsync();
        var ex = await act.Should().ThrowAsync<AiServiceUnavailableException>();
        ex.WithMessage("*Failed to deserialize*");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenResponseHasEmptyStatus_ThrowsAiServiceUnavailableException()
    {
        // Arrange
        var (client, handler) = CreateTestClient();
        handler.Handler = (req, ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"status\":\"\",\"service\":\"\"}", Encoding.UTF8, "application/json")
        });

        // Act & Assert
        var act = async () => await client.CheckHealthAsync();
        var ex = await act.Should().ThrowAsync<AiServiceUnavailableException>();
        ex.WithMessage("*invalid or empty*");
    }
}
