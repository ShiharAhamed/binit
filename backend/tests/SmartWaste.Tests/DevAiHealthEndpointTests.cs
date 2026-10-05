using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SmartWaste.Application.Common.Exceptions;
using SmartWaste.Application.DTOs.Ai;
using SmartWaste.Application.Interfaces;

namespace SmartWaste.Tests;
using Xunit;

[Collection(IntegrationTestCollection.Name)]
public class DevAiHealthEndpointTests
{
    private readonly CustomWebApplicationFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public DevAiHealthEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetDevAiHealth_WhenAiServiceIsHealthy_Returns200WithHealthDto()
    {
        // Arrange
        var fakeClient = new FakeAiServiceClient(new AiHealthDto
        {
            Status = "healthy",
            Service = "SmartWaste AI Service"
        });

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddScoped<IAiServiceClient>(_ => fakeClient);
            });
        }).CreateClient();

        // Act
        var response = await client.GetAsync("/api/v1/dev/ai-health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<AiHealthDto>(JsonOptions);
        result.Should().NotBeNull();
        result!.Status.Should().Be("healthy");
        result.Service.Should().Be("SmartWaste AI Service");
    }

    [Fact]
    public async Task GetDevAiHealth_WhenAiServiceThrowsUnavailable_Returns503ProblemDetails()
    {
        // Arrange
        var fakeClient = new FakeAiServiceClient(
            throwException: new AiServiceUnavailableException("Unable to connect to the AI service."));

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.AddScoped<IAiServiceClient>(_ => fakeClient);
            });
        }).CreateClient();

        // Act
        var response = await client.GetAsync("/api/v1/dev/ai-health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(503);
        problem.Title.Should().Be("AI Service Unavailable");
        problem.Detail.Should().Contain("Unable to connect");
    }

    [Fact]
    public async Task GetDevAiHealth_WhenEnvironmentIsProduction_Returns404NotFound()
    {
        // Arrange
        var fakeClient = new FakeAiServiceClient(new AiHealthDto
        {
            Status = "healthy",
            Service = "SmartWaste AI Service"
        });

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.ConfigureTestServices(services =>
            {
                services.AddScoped<IAiServiceClient>(_ => fakeClient);
            });
        }).CreateClient();

        // Act
        var response = await client.GetAsync("/api/v1/dev/ai-health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private class FakeAiServiceClient : IAiServiceClient
    {
        private readonly AiHealthDto? _dto;
        private readonly Exception? _exception;

        public FakeAiServiceClient(AiHealthDto dto) => _dto = dto;
        public FakeAiServiceClient(Exception throwException) => _exception = throwException;

        public Task<AiHealthDto> CheckHealthAsync(CancellationToken cancellationToken = default)
        {
            if (_exception != null)
            {
                throw _exception;
            }
            return Task.FromResult(_dto!);
        }
    }
}
