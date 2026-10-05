using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using SmartWaste.Application.Collection.DTOs.Responses;
using SmartWaste.Application.Collection.Interfaces;
using SmartWaste.Application.Collection.Queries;
using SmartWaste.Application.Common.Models;
using SmartWaste.Application.DTOs.Auth;
using SmartWaste.Domain.Common;
using SmartWaste.Domain.Collection.Enums;
using SmartWaste.Domain.Reporting.Enums;
using Xunit;

namespace SmartWaste.Tests.Collection.Controllers;

/// <summary>
/// End-to-end API integration tests for CollectionNeedsController read endpoint (GET /api/v1/collection-needs).
/// Exercises real ASP.NET Core routing, JWT Bearer authentication, role-based authorization,
/// query string binding, and ExceptionHandlingMiddleware using CustomWebApplicationFactory with a mocked
/// ICollectionNeedService to guarantee zero database mutations.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class CollectionNeedsApiIntegrationTests
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _baseClient;

    private static readonly JsonSerializerOptions SharedTestJsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false) }
    };

    public CollectionNeedsApiIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _baseClient = factory.CreateClient();
    }

    private (HttpClient client, Mock<ICollectionNeedService> mockService) CreateClientWithMockService()
    {
        var mock = new Mock<ICollectionNeedService>();
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ICollectionNeedService>();
                services.AddSingleton<ICollectionNeedService>(mock.Object);
            });
        }).CreateClient();

        return (client, mock);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // AUTHENTICATION TOKEN HELPERS
    // ──────────────────────────────────────────────────────────────────────────

    private async Task<string> GetTokenAsync(string email, string password, string clientType)
    {
        var loginReq = new LoginRequest
        {
            Email = email,
            Password = password,
            ClientType = clientType
        };
        var res = await _baseClient.PostAsJsonAsync("/api/v1/auth/login", loginReq);
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = await res.Content.ReadFromJsonAsync<AuthResponse>(SharedTestJsonOptions);
        return auth!.AccessToken;
    }

    private async Task<string> GetCitizenTokenAsync()
    {
        var email = $"citizen_needs_{Guid.NewGuid():N}@smartwaste.test";
        var regReq = new RegisterRequest
        {
            FullName = "Citizen Needs Explorer",
            Email = email,
            PhoneNumber = "+94771234567",
            Password = "Password123!"
        };
        var res = await _baseClient.PostAsJsonAsync("/api/v1/auth/register", regReq);
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = await res.Content.ReadFromJsonAsync<AuthResponse>(SharedTestJsonOptions);
        return auth!.AccessToken;
    }

    private async Task<string> GetOfficerTokenAsync()
        => await GetTokenAsync("officer@smartwaste.local", "DevPassword123!", "web");

    private async Task<string> GetManagerTokenAsync()
        => await GetTokenAsync("manager@smartwaste.local", "DevPassword123!", "web");

    private async Task<string> GetDriverTokenAsync()
        => await GetTokenAsync("driver@smartwaste.local", "DevPassword123!", "mobile");

    private static HttpRequestMessage CreateAuthorizedRequest(HttpMethod method, string url, string? token)
    {
        var request = new HttpRequestMessage(method, url);
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        return request;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 1. SUCCESSFUL READ QUEUE RETRIEVAL
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetCollectionNeeds_WasteOfficerToken_ValidQuery_Returns200OKWithPagedResult()
    {
        var (client, mock) = CreateClientWithMockService();
        var officerToken = await GetOfficerTokenAsync();
        var item1Id = Guid.NewGuid();
        var item2Id = Guid.NewGuid();

        var expectedResult = new PagedResult<CollectionNeedItemDto>
        {
            Items = new List<CollectionNeedItemDto>
            {
                new()
                {
                    Id = item1Id,
                    TargetType = "Report",
                    CollectionReason = "VerifiedReport",
                    Title = "Verified Report: Main Street, Pettah",
                    Latitude = 6.9351,
                    Longitude = 79.8512,
                    AddressText = "Main Street, Pettah",
                    WasteTypes = new List<string> { "General" },
                    Urgency = "High",
                    TriggerDate = DateTime.UtcNow.AddHours(-3),
                    AttachmentCount = 2,
                    BinDetails = null
                },
                new()
                {
                    Id = item2Id,
                    TargetType = "Bin",
                    CollectionReason = "FullOrBlockedBin",
                    Title = "BIN-COL-0042 (100% Full)",
                    Latitude = 6.9271,
                    Longitude = 79.8612,
                    AddressText = "Main Street, Pettah",
                    WasteTypes = new List<string> { "General", "Recyclable" },
                    Urgency = "High",
                    TriggerDate = DateTime.UtcNow.AddHours(-1),
                    AttachmentCount = 0,
                    BinDetails = new CollectionNeedBinDetailsDto
                    {
                        BinCode = "BIN-COL-0042",
                        CapacityLiters = 660,
                        LatestFillLevelPercent = 100,
                        LatestCondition = BinCondition.Good,
                        ObservationAgeHours = 1.0
                    }
                }
            },
            Page = 1,
            PageSize = 20,
            TotalCount = 2
        };

        mock.Setup(s => s.GetCollectionNeedsAsync(
                It.Is<CollectionNeedListQuery>(q => q.TargetType == "Report" && q.Search == "Pettah" && q.Page == 1 && q.PageSize == 20),
                It.IsAny<Guid>(),
                AppRoles.WasteOfficer,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var request = CreateAuthorizedRequest(HttpMethod.Get, "/api/v1/collection-needs?targetType=Report&search=Pettah&page=1&pageSize=20", officerToken);
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<CollectionNeedItemDto>>(SharedTestJsonOptions);
        result.Should().NotBeNull();
        result!.TotalCount.Should().Be(2);
        result.Items.Should().HaveCount(2);
        result.Items[0].Id.Should().Be(item1Id);
        result.Items[0].TargetType.Should().Be("Report");
        result.Items[1].Id.Should().Be(item2Id);
        result.Items[1].BinDetails.Should().NotBeNull();
        result.Items[1].BinDetails!.BinCode.Should().Be("BIN-COL-0042");
    }

    [Fact]
    public async Task GetCollectionNeeds_MunicipalManagerToken_Returns200OKWithPagedResult()
    {
        var (client, mock) = CreateClientWithMockService();
        var managerToken = await GetManagerTokenAsync();

        var expectedResult = new PagedResult<CollectionNeedItemDto>
        {
            Items = new List<CollectionNeedItemDto>(),
            Page = 1,
            PageSize = 20,
            TotalCount = 0
        };

        mock.Setup(s => s.GetCollectionNeedsAsync(
                It.IsAny<CollectionNeedListQuery>(),
                It.IsAny<Guid>(),
                AppRoles.MunicipalManager,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var request = CreateAuthorizedRequest(HttpMethod.Get, "/api/v1/collection-needs", managerToken);
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<CollectionNeedItemDto>>(SharedTestJsonOptions);
        result.Should().NotBeNull();
        result!.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 2. ROLE-BASED ACCESS CONTROL (RBAC)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetCollectionNeeds_CitizenToken_Returns403Forbidden()
    {
        var (client, mock) = CreateClientWithMockService();
        var citizenToken = await GetCitizenTokenAsync();

        var request = CreateAuthorizedRequest(HttpMethod.Get, "/api/v1/collection-needs", citizenToken);
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        mock.Verify(s => s.GetCollectionNeedsAsync(It.IsAny<CollectionNeedListQuery>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetCollectionNeeds_DriverToken_Returns403Forbidden()
    {
        var (client, mock) = CreateClientWithMockService();
        var driverToken = await GetDriverTokenAsync();

        var request = CreateAuthorizedRequest(HttpMethod.Get, "/api/v1/collection-needs", driverToken);
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        mock.Verify(s => s.GetCollectionNeedsAsync(It.IsAny<CollectionNeedListQuery>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetCollectionNeeds_Unauthenticated_Returns401Unauthorized()
    {
        var (client, mock) = CreateClientWithMockService();

        var request = CreateAuthorizedRequest(HttpMethod.Get, "/api/v1/collection-needs", null);
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        mock.Verify(s => s.GetCollectionNeedsAsync(It.IsAny<CollectionNeedListQuery>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 3. QUERY VALIDATION ERROR HANDLING
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetCollectionNeeds_InvalidQuery_Returns400BadRequest()
    {
        var (client, mock) = CreateClientWithMockService();
        var officerToken = await GetOfficerTokenAsync();

        mock.Setup(s => s.GetCollectionNeedsAsync(
                It.IsAny<CollectionNeedListQuery>(),
                It.IsAny<Guid>(),
                AppRoles.WasteOfficer,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException(new[]
            {
                new FluentValidation.Results.ValidationFailure("TargetType", "TargetType must be 'Report' or 'Bin'.")
            }));

        var request = CreateAuthorizedRequest(HttpMethod.Get, "/api/v1/collection-needs?targetType=InvalidType", officerToken);
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(SharedTestJsonOptions);
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status400BadRequest);
        problem.Errors.Should().ContainKey("TargetType");
    }
}
