using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using SmartWaste.Application.DTOs.Auth;
using SmartWaste.Application.Fleet.DTOs.Requests;
using SmartWaste.Application.Fleet.DTOs.Responses;
using Xunit;

namespace SmartWaste.Tests.Fleet.Controllers;

[Collection(IntegrationTestCollection.Name)]
public class AiToolsFleetPlanningAuthIntegrationTests
{
    private readonly HttpClient _client;
    private const string InternalApiKey = "TestInternalServiceKey_12345!";
    private const string ContextEndpoint = "/api/v1/internal/ai-tools/fleet-planning-context";
    private const string CompatibilityEndpoint = "/api/v1/internal/ai-tools/fleet-compatibility";

    private static readonly JsonSerializerOptions SharedTestJsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false) }
    };

    public AiToolsFleetPlanningAuthIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private static HttpRequestMessage CreateInternalRequest(HttpMethod method, string url, string? apiKey = InternalApiKey, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (apiKey != null)
        {
            request.Headers.Add("X-Internal-Service-Key", apiKey);
        }
        if (content != null)
        {
            request.Content = content;
        }
        return request;
    }

    [Fact]
    public async Task GetPlanningContext_WithMissingInternalServiceKey_Returns401Unauthorized()
    {
        var request = CreateInternalRequest(HttpMethod.Get, ContextEndpoint, apiKey: null);
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetPlanningContext_WithInvalidInternalServiceKey_Returns401Unauthorized()
    {
        var request = CreateInternalRequest(HttpMethod.Get, ContextEndpoint, apiKey: "WrongSecretKey123!");
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetPlanningContext_WithUserBearerJwtOnly_Returns401Unauthorized()
    {
        var loginReq = new LoginRequest
        {
            Email = "officer@smartwaste.local",
            Password = "DevPassword123!",
            ClientType = "web"
        };
        var loginRes = await _client.PostAsJsonAsync("/api/v1/auth/login", loginReq);
        loginRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = await loginRes.Content.ReadFromJsonAsync<AuthResponse>(SharedTestJsonOptions);

        var request = new HttpRequestMessage(HttpMethod.Get, ContextEndpoint);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetPlanningContext_WithValidInternalServiceKey_Returns200OK()
    {
        var request = CreateInternalRequest(HttpMethod.Get, ContextEndpoint);
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<FleetPlanningContextDto>(SharedTestJsonOptions);
        result.Should().NotBeNull();
        result!.Tasks.Should().NotBeNull();
        result.Drivers.Should().NotBeNull();
        result.Vehicles.Should().NotBeNull();
    }

    [Fact]
    public async Task CheckCompatibility_WithMissingInternalServiceKey_Returns401Unauthorized()
    {
        var request = CreateInternalRequest(HttpMethod.Post, CompatibilityEndpoint, apiKey: null,
            JsonContent.Create(new CheckFleetCompatibilityRequest { TaskIds = new[] { Guid.NewGuid() }, VehicleId = Guid.NewGuid() }));
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CheckCompatibility_WithInvalidInternalServiceKey_Returns401Unauthorized()
    {
        var request = CreateInternalRequest(HttpMethod.Post, CompatibilityEndpoint, apiKey: "WrongKey",
            JsonContent.Create(new CheckFleetCompatibilityRequest { TaskIds = new[] { Guid.NewGuid() }, VehicleId = Guid.NewGuid() }));
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CheckCompatibility_WithUserBearerJwtOnly_Returns401Unauthorized()
    {
        var loginReq = new LoginRequest
        {
            Email = "officer@smartwaste.local",
            Password = "DevPassword123!",
            ClientType = "web"
        };
        var loginRes = await _client.PostAsJsonAsync("/api/v1/auth/login", loginReq);
        loginRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = await loginRes.Content.ReadFromJsonAsync<AuthResponse>(SharedTestJsonOptions);

        var request = new HttpRequestMessage(HttpMethod.Post, CompatibilityEndpoint);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        request.Content = JsonContent.Create(new CheckFleetCompatibilityRequest { TaskIds = new[] { Guid.NewGuid() }, VehicleId = Guid.NewGuid() });

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetOperationalValidationContext_WithMissingInternalServiceKey_Returns401Unauthorized()
    {
        var request = CreateInternalRequest(HttpMethod.Post, "/api/v1/internal/ai-tools/operational-validation-context", apiKey: null,
            JsonContent.Create(new GetOperationalValidationContextRequest()));
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetOperationalValidationContext_WithInvalidInternalServiceKey_Returns401Unauthorized()
    {
        var request = CreateInternalRequest(HttpMethod.Post, "/api/v1/internal/ai-tools/operational-validation-context", apiKey: "WrongKey",
            JsonContent.Create(new GetOperationalValidationContextRequest()));
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetOperationalValidationContext_WithValidKey_Returns200OK()
    {
        var request = CreateInternalRequest(HttpMethod.Post, "/api/v1/internal/ai-tools/operational-validation-context", apiKey: InternalApiKey,
            JsonContent.Create(new GetOperationalValidationContextRequest
            {
                TaskIds = Array.Empty<Guid>(),
                DriverIds = Array.Empty<Guid>(),
                VehicleIds = Array.Empty<Guid>()
            }));
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<OperationalValidationContextDto>(SharedTestJsonOptions);
        result.Should().NotBeNull();
        result!.Tasks.Should().BeEmpty();
        result.Drivers.Should().BeEmpty();
        result.Vehicles.Should().BeEmpty();
    }
}
