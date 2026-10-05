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
using SmartWaste.Application.Collection.DTOs.Requests;
using SmartWaste.Application.Collection.DTOs.Responses;
using SmartWaste.Application.Collection.Interfaces;
using SmartWaste.Application.Collection.Queries;
using SmartWaste.Application.Common.Exceptions;
using SmartWaste.Application.Common.Models;
using SmartWaste.Application.DTOs.Auth;
using SmartWaste.Domain.Collection.Enums;
using SmartWaste.Domain.Common;
using SmartWaste.Domain.Reporting.Enums;
using Xunit;

namespace SmartWaste.Tests.Collection.Controllers;

/// <summary>
/// End-to-end API integration tests for WasteBinsController read endpoints.
/// Exercises real ASP.NET Core routing, JWT Bearer authentication, role-based authorization,
/// and ExceptionHandlingMiddleware using CustomWebApplicationFactory with a mocked IWasteBinService
/// to guarantee zero mutations against the live development PostgreSQL database.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class WasteBinsApiIntegrationTests
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _baseClient;

    private static readonly JsonSerializerOptions SharedTestJsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false) }
    };

    public WasteBinsApiIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _baseClient = factory.CreateClient();
    }

    private (HttpClient client, Mock<IWasteBinService> mockService) CreateClientWithMockService()
    {
        var mock = new Mock<IWasteBinService>();
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IWasteBinService>();
                services.AddSingleton<IWasteBinService>(mock.Object);
            });
        }).CreateClient();

        return (client, mock);
    }

    private (HttpClient client, Mock<IBinObservationService> mockObsService) CreateClientWithMockObsService()
    {
        var mockObs = new Mock<IBinObservationService>();
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IBinObservationService>();
                services.AddSingleton<IBinObservationService>(mockObs.Object);
            });
        }).CreateClient();

        return (client, mockObs);
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
        var email = $"citizen_bins_{Guid.NewGuid():N}@smartwaste.test";
        var regReq = new RegisterRequest
        {
            FullName = "Citizen Bin Explorer",
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

    private static HttpRequestMessage CreateAuthorizedJsonRequest<T>(HttpMethod method, string url, string? token, T body)
    {
        var request = CreateAuthorizedRequest(method, url, token);
        request.Content = JsonContent.Create(body, options: SharedTestJsonOptions);
        return request;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 1. PUBLIC CITIZEN ENDPOINTS (GET /api/v1/bins/public, GET /api/v1/bins/public/{id})
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetPublicList_CitizenToken_Returns200OKWithPagedResult()
    {
        var (client, mock) = CreateClientWithMockService();
        var token = await GetCitizenTokenAsync();

        mock.Setup(s => s.GetPublicListAsync(It.IsAny<PublicWasteBinQuery>(), It.IsAny<Guid>(), AppRoles.Citizen, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<PublicWasteBinDto>
            {
                Items = new List<PublicWasteBinDto>
                {
                    new()
                    {
                        Id = Guid.NewGuid(),
                        BinCode = "BIN-COL-0001",
                        Latitude = 6.9271,
                        Longitude = 79.8612,
                        AddressText = "Main Street, Pettah",
                        CapacityLiters = 660,
                        AcceptedWasteTypes = new[] { "General" },
                        PublicAvailability = "Usable"
                    }
                },
                Page = 1,
                PageSize = 20,
                TotalCount = 1
            });

        var request = CreateAuthorizedRequest(HttpMethod.Get, "/api/v1/bins/public?page=1&pageSize=20", token);
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<PublicWasteBinDto>>(SharedTestJsonOptions);
        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(1);
        result.Items[0].BinCode.Should().Be("BIN-COL-0001");
    }

    [Fact]
    public async Task GetPublicById_CitizenToken_Returns200OKWithPublicDetail()
    {
        var (client, mock) = CreateClientWithMockService();
        var token = await GetCitizenTokenAsync();
        var binId = Guid.NewGuid();

        mock.Setup(s => s.GetPublicByIdAsync(binId, It.IsAny<Guid>(), AppRoles.Citizen, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PublicWasteBinDetailDto
            {
                Id = binId,
                BinCode = "BIN-COL-0001",
                Latitude = 6.9271,
                Longitude = 79.8612,
                AddressText = "Main Street, Pettah",
                CapacityLiters = 660,
                AcceptedWasteTypes = new[] { "General", "Recyclable" },
                PublicAvailability = "Usable",
                LastObservedAt = DateTime.UtcNow.AddMinutes(-30),
                IsCollectionScheduled = false
            });

        var request = CreateAuthorizedRequest(HttpMethod.Get, $"/api/v1/bins/public/{binId}", token);
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await response.Content.ReadFromJsonAsync<PublicWasteBinDetailDto>(SharedTestJsonOptions);
        detail.Should().NotBeNull();
        detail!.Id.Should().Be(binId);
        detail.BinCode.Should().Be("BIN-COL-0001");
    }

    [Fact]
    public async Task GetPublicById_MissingBin_Returns404NotFound()
    {
        var (client, mock) = CreateClientWithMockService();
        var token = await GetCitizenTokenAsync();
        var missingId = Guid.NewGuid();

        mock.Setup(s => s.GetPublicByIdAsync(missingId, It.IsAny<Guid>(), AppRoles.Citizen, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException($"Waste bin '{missingId}' was not found."));

        var request = CreateAuthorizedRequest(HttpMethod.Get, $"/api/v1/bins/public/{missingId}", token);
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(SharedTestJsonOptions);
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status404NotFound);
        problem.Title.Should().Be("Resource Not Found");
    }

    [Fact]
    public async Task GetPublicList_InvalidQuery_Returns400BadRequest()
    {
        var (client, _) = CreateClientWithMockService();
        var token = await GetCitizenTokenAsync();

        var request = CreateAuthorizedRequest(HttpMethod.Get, "/api/v1/bins/public?radiusKm=5", token);
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(SharedTestJsonOptions);
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status400BadRequest);
        problem.Title.Should().NotBeNullOrWhiteSpace();
        problem.Errors.Should().NotBeEmpty();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 2. INTERNAL STAFF ENDPOINTS (GET /api/v1/bins, GET /api/v1/bins/{id})
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetInternalList_WasteOfficerToken_Returns200OK()
    {
        var (client, mock) = CreateClientWithMockService();
        var token = await GetOfficerTokenAsync();

        mock.Setup(s => s.GetInternalListAsync(It.IsAny<WasteBinListQuery>(), It.IsAny<Guid>(), AppRoles.WasteOfficer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<WasteBinSummaryDto>
            {
                Items = new List<WasteBinSummaryDto>
                {
                    new()
                    {
                        Id = Guid.NewGuid(),
                        BinCode = "BIN-COL-0042",
                        Latitude = 6.9271,
                        Longitude = 79.8612,
                        AddressText = "Pettah Depot",
                        CapacityLiters = 1100,
                        AdministrativeStatus = BinAdministrativeStatus.Active,
                        AcceptedWasteTypes = new[] { "General" },
                        CollectionWeekdays = new[] { 1, 3, 5 },
                        LatestFillLevelPercent = 50,
                        LatestCondition = BinCondition.Good
                    }
                },
                Page = 1,
                PageSize = 20,
                TotalCount = 1
            });

        var request = CreateAuthorizedRequest(HttpMethod.Get, "/api/v1/bins", token);
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<WasteBinSummaryDto>>(SharedTestJsonOptions);
        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetInternalById_WasteOfficerToken_Returns200OK()
    {
        var (client, mock) = CreateClientWithMockService();
        var token = await GetOfficerTokenAsync();
        var binId = Guid.NewGuid();

        mock.Setup(s => s.GetInternalByIdAsync(binId, It.IsAny<Guid>(), AppRoles.WasteOfficer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WasteBinDetailDto
            {
                Id = binId,
                BinCode = "BIN-COL-0042",
                Latitude = 6.9271,
                Longitude = 79.8612,
                AddressText = "Pettah Depot",
                CapacityLiters = 1100,
                AdministrativeStatus = BinAdministrativeStatus.Active,
                AcceptedWasteTypes = new[] { "General" },
                CollectionWeekdays = new[] { 1, 3, 5 }
            });

        var request = CreateAuthorizedRequest(HttpMethod.Get, $"/api/v1/bins/{binId}", token);
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await response.Content.ReadFromJsonAsync<WasteBinDetailDto>(SharedTestJsonOptions);
        detail.Should().NotBeNull();
        detail!.Id.Should().Be(binId);
    }

    [Fact]
    public async Task GetInternalList_MunicipalManagerToken_Returns200OK()
    {
        var (client, mock) = CreateClientWithMockService();
        var token = await GetManagerTokenAsync();

        mock.Setup(s => s.GetInternalListAsync(It.IsAny<WasteBinListQuery>(), It.IsAny<Guid>(), AppRoles.MunicipalManager, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<WasteBinSummaryDto>
            {
                Items = new List<WasteBinSummaryDto>(),
                Page = 1,
                PageSize = 20,
                TotalCount = 0
            });

        var request = CreateAuthorizedRequest(HttpMethod.Get, "/api/v1/bins", token);
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetInternalById_MunicipalManagerToken_Returns200OK()
    {
        var (client, mock) = CreateClientWithMockService();
        var token = await GetManagerTokenAsync();
        var binId = Guid.NewGuid();

        mock.Setup(s => s.GetInternalByIdAsync(binId, It.IsAny<Guid>(), AppRoles.MunicipalManager, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WasteBinDetailDto
            {
                Id = binId,
                BinCode = "BIN-COL-0042",
                Latitude = 6.9271,
                Longitude = 79.8612,
                AddressText = "Pettah Depot",
                CapacityLiters = 1100,
                AdministrativeStatus = BinAdministrativeStatus.Active,
                AcceptedWasteTypes = new[] { "General" },
                CollectionWeekdays = new[] { 1, 3, 5 }
            });

        var request = CreateAuthorizedRequest(HttpMethod.Get, $"/api/v1/bins/{binId}", token);
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetInternalById_MissingBin_Returns404NotFound()
    {
        var (client, mock) = CreateClientWithMockService();
        var token = await GetOfficerTokenAsync();
        var missingId = Guid.NewGuid();

        mock.Setup(s => s.GetInternalByIdAsync(missingId, It.IsAny<Guid>(), AppRoles.WasteOfficer, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException($"Waste bin '{missingId}' was not found."));

        var request = CreateAuthorizedRequest(HttpMethod.Get, $"/api/v1/bins/{missingId}", token);
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 3. ROLE-BASED ACCESS CONTROL ENFORCEMENT
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetInternalList_CitizenToken_Returns403Forbidden()
    {
        var (client, _) = CreateClientWithMockService();
        var token = await GetCitizenTokenAsync();

        var request = CreateAuthorizedRequest(HttpMethod.Get, "/api/v1/bins", token);
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetInternalById_CitizenToken_Returns403Forbidden()
    {
        var (client, _) = CreateClientWithMockService();
        var token = await GetCitizenTokenAsync();

        var request = CreateAuthorizedRequest(HttpMethod.Get, $"/api/v1/bins/{Guid.NewGuid()}", token);
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetPublicList_StaffTokens_Return403Forbidden()
    {
        var (client, _) = CreateClientWithMockService();
        var officerToken = await GetOfficerTokenAsync();
        var managerToken = await GetManagerTokenAsync();

        var req1 = CreateAuthorizedRequest(HttpMethod.Get, "/api/v1/bins/public", officerToken);
        var res1 = await client.SendAsync(req1);
        res1.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var req2 = CreateAuthorizedRequest(HttpMethod.Get, "/api/v1/bins/public", managerToken);
        var res2 = await client.SendAsync(req2);
        res2.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetPublicById_StaffTokens_Return403Forbidden()
    {
        var (client, _) = CreateClientWithMockService();
        var officerToken = await GetOfficerTokenAsync();
        var binId = Guid.NewGuid();

        var request = CreateAuthorizedRequest(HttpMethod.Get, $"/api/v1/bins/public/{binId}", officerToken);
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Endpoints_DriverToken_Returns403Forbidden()
    {
        var (client, _) = CreateClientWithMockService();
        var driverToken = await GetDriverTokenAsync();
        var binId = Guid.NewGuid();

        // Driver cannot access public citizen endpoint
        var req1 = CreateAuthorizedRequest(HttpMethod.Get, "/api/v1/bins/public", driverToken);
        var res1 = await client.SendAsync(req1);
        res1.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Driver cannot access internal staff endpoint
        var req2 = CreateAuthorizedRequest(HttpMethod.Get, "/api/v1/bins", driverToken);
        var res2 = await client.SendAsync(req2);
        res2.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var req3 = CreateAuthorizedRequest(HttpMethod.Get, $"/api/v1/bins/{binId}", driverToken);
        var res3 = await client.SendAsync(req3);
        res3.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 4. UNAUTHENTICATED ACCESS
    // ──────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("/api/v1/bins/public")]
    [InlineData("/api/v1/bins")]
    public async Task ReadEndpoints_Unauthenticated_Return401Unauthorized(string url)
    {
        var (client, _) = CreateClientWithMockService();

        var request = CreateAuthorizedRequest(HttpMethod.Get, url, null);
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 5. INTERNAL STAFF WRITE ENDPOINTS (POST /api/v1/bins, PUT /api/v1/bins/{id})
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_WasteOfficerToken_ValidPayload_Returns201CreatedWithLocationHeaderAndBody()
    {
        var (client, mock) = CreateClientWithMockService();
        var officerToken = await GetOfficerTokenAsync();
        var createdBinId = Guid.NewGuid();

        var requestDto = new CreateWasteBinRequest
        {
            BinCode = "BIN-COL-0043",
            Latitude = 6.9312,
            Longitude = 79.8504,
            AddressText = "Galle Face Green Promenade",
            CapacityLiters = 1100,
            AcceptedWasteTypes = new[] { WasteType.General, WasteType.Organic },
            CollectionWeekdays = new[] { 1, 3, 5 }
        };

        var expectedDetail = new WasteBinDetailDto
        {
            Id = createdBinId,
            BinCode = "BIN-COL-0043",
            Latitude = 6.9312,
            Longitude = 79.8504,
            AddressText = "Galle Face Green Promenade",
            CapacityLiters = 1100,
            AdministrativeStatus = BinAdministrativeStatus.Active,
            AcceptedWasteTypes = new[] { "General", "Organic" },
            CollectionWeekdays = new[] { 1, 3, 5 },
            CreatedAt = DateTime.UtcNow
        };

        mock.Setup(s => s.CreateAsync(
                It.Is<CreateWasteBinRequest>(r => r.BinCode == "BIN-COL-0043" && r.CapacityLiters == 1100),
                It.IsAny<Guid>(),
                AppRoles.WasteOfficer,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDetail);

        var request = CreateAuthorizedJsonRequest(HttpMethod.Post, "/api/v1/bins", officerToken, requestDto);
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Contain($"/api/v1/bins/{createdBinId}");

        var detail = await response.Content.ReadFromJsonAsync<WasteBinDetailDto>(SharedTestJsonOptions);
        detail.Should().NotBeNull();
        detail!.Id.Should().Be(createdBinId);
        detail.BinCode.Should().Be("BIN-COL-0043");
        detail.CapacityLiters.Should().Be(1100);
    }

    [Fact]
    public async Task Create_ValidationFailure_Returns400BadRequest()
    {
        var (client, mock) = CreateClientWithMockService();
        var officerToken = await GetOfficerTokenAsync();

        var requestDto = new CreateWasteBinRequest
        {
            BinCode = "",
            CapacityLiters = -10
        };

        mock.Setup(s => s.CreateAsync(
                It.IsAny<CreateWasteBinRequest>(),
                It.IsAny<Guid>(),
                AppRoles.WasteOfficer,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException(new[]
            {
                new FluentValidation.Results.ValidationFailure("BinCode", "Bin code is required."),
                new FluentValidation.Results.ValidationFailure("CapacityLiters", "Capacity must be greater than 0 liters.")
            }));

        var request = CreateAuthorizedJsonRequest(HttpMethod.Post, "/api/v1/bins", officerToken, requestDto);
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(SharedTestJsonOptions);
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status400BadRequest);
        problem.Errors.Should().ContainKey("BinCode");
    }

    [Fact]
    public async Task Create_DuplicateBinCode_Returns409Conflict()
    {
        var (client, mock) = CreateClientWithMockService();
        var officerToken = await GetOfficerTokenAsync();

        var requestDto = new CreateWasteBinRequest
        {
            BinCode = "BIN-COL-0043",
            Latitude = 6.9312,
            Longitude = 79.8504,
            CapacityLiters = 1100,
            AcceptedWasteTypes = new[] { WasteType.General },
            CollectionWeekdays = new[] { 1 }
        };

        mock.Setup(s => s.CreateAsync(
                It.IsAny<CreateWasteBinRequest>(),
                It.IsAny<Guid>(),
                AppRoles.WasteOfficer,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BusinessRuleConflictException("A waste bin with code 'BIN-COL-0043' already exists."));

        var request = CreateAuthorizedJsonRequest(HttpMethod.Post, "/api/v1/bins", officerToken, requestDto);
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(SharedTestJsonOptions);
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status409Conflict);
        problem.Title.Should().Be("Business Rule Conflict");
        problem.Detail.Should().Contain("already exists");
    }

    [Fact]
    public async Task Update_WasteOfficerToken_ValidPayload_Returns200OKWithUpdatedDetail()
    {
        var (client, mock) = CreateClientWithMockService();
        var officerToken = await GetOfficerTokenAsync();
        var binId = Guid.NewGuid();

        var requestDto = new UpdateWasteBinRequest
        {
            Latitude = 6.9315,
            Longitude = 79.8506,
            AddressText = "Galle Face Green Promenade (North Pavilion)",
            CapacityLiters = 1100,
            AcceptedWasteTypes = new[] { WasteType.General, WasteType.Organic, WasteType.Recyclable },
            CollectionWeekdays = new[] { 1, 3, 5 }
        };

        var expectedDetail = new WasteBinDetailDto
        {
            Id = binId,
            BinCode = "BIN-COL-0043",
            Latitude = 6.9315,
            Longitude = 79.8506,
            AddressText = "Galle Face Green Promenade (North Pavilion)",
            CapacityLiters = 1100,
            AdministrativeStatus = BinAdministrativeStatus.Active,
            AcceptedWasteTypes = new[] { "General", "Organic", "Recyclable" },
            CollectionWeekdays = new[] { 1, 3, 5 },
            UpdatedAt = DateTime.UtcNow
        };

        mock.Setup(s => s.UpdateAsync(
                binId,
                It.Is<UpdateWasteBinRequest>(r => r.CapacityLiters == 1100 && r.AddressText!.Contains("North Pavilion")),
                It.IsAny<Guid>(),
                AppRoles.WasteOfficer,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDetail);

        var request = CreateAuthorizedJsonRequest(HttpMethod.Put, $"/api/v1/bins/{binId}", officerToken, requestDto);
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await response.Content.ReadFromJsonAsync<WasteBinDetailDto>(SharedTestJsonOptions);
        detail.Should().NotBeNull();
        detail!.Id.Should().Be(binId);
        detail.AddressText.Should().Be("Galle Face Green Promenade (North Pavilion)");
    }

    [Fact]
    public async Task Update_ValidationFailure_Returns400BadRequest()
    {
        var (client, mock) = CreateClientWithMockService();
        var officerToken = await GetOfficerTokenAsync();
        var binId = Guid.NewGuid();

        var requestDto = new UpdateWasteBinRequest
        {
            CapacityLiters = 0
        };

        mock.Setup(s => s.UpdateAsync(
                binId,
                It.IsAny<UpdateWasteBinRequest>(),
                It.IsAny<Guid>(),
                AppRoles.WasteOfficer,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException(new[]
            {
                new FluentValidation.Results.ValidationFailure("CapacityLiters", "Capacity must be greater than 0 liters.")
            }));

        var request = CreateAuthorizedJsonRequest(HttpMethod.Put, $"/api/v1/bins/{binId}", officerToken, requestDto);
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(SharedTestJsonOptions);
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status400BadRequest);
        problem.Errors.Should().ContainKey("CapacityLiters");
    }

    [Fact]
    public async Task Update_MissingBin_Returns404NotFound()
    {
        var (client, mock) = CreateClientWithMockService();
        var officerToken = await GetOfficerTokenAsync();
        var missingId = Guid.NewGuid();

        var requestDto = new UpdateWasteBinRequest
        {
            Latitude = 6.9315,
            Longitude = 79.8506,
            CapacityLiters = 1100,
            AcceptedWasteTypes = new[] { WasteType.General },
            CollectionWeekdays = new[] { 1 }
        };

        mock.Setup(s => s.UpdateAsync(
                missingId,
                It.IsAny<UpdateWasteBinRequest>(),
                It.IsAny<Guid>(),
                AppRoles.WasteOfficer,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException($"Waste bin '{missingId}' was not found."));

        var request = CreateAuthorizedJsonRequest(HttpMethod.Put, $"/api/v1/bins/{missingId}", officerToken, requestDto);
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(SharedTestJsonOptions);
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status404NotFound);
        problem.Title.Should().Be("Resource Not Found");
    }

    [Fact]
    public async Task Update_RetiredBin_Returns409Conflict()
    {
        var (client, mock) = CreateClientWithMockService();
        var officerToken = await GetOfficerTokenAsync();
        var retiredId = Guid.NewGuid();

        var requestDto = new UpdateWasteBinRequest
        {
            Latitude = 6.9315,
            Longitude = 79.8506,
            CapacityLiters = 1100,
            AcceptedWasteTypes = new[] { WasteType.General },
            CollectionWeekdays = new[] { 1 }
        };

        mock.Setup(s => s.UpdateAsync(
                retiredId,
                It.IsAny<UpdateWasteBinRequest>(),
                It.IsAny<Guid>(),
                AppRoles.WasteOfficer,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BusinessRuleConflictException("Cannot update a retired waste bin."));

        var request = CreateAuthorizedJsonRequest(HttpMethod.Put, $"/api/v1/bins/{retiredId}", officerToken, requestDto);
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(SharedTestJsonOptions);
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status409Conflict);
        problem.Title.Should().Be("Business Rule Conflict");
        problem.Detail.Should().Contain("Cannot update a retired waste bin");
    }

    [Fact]
    public async Task Create_CitizenAndManagerAndDriverTokens_Return403Forbidden()
    {
        var (client, _) = CreateClientWithMockService();
        var citizenToken = await GetCitizenTokenAsync();
        var managerToken = await GetManagerTokenAsync();
        var driverToken = await GetDriverTokenAsync();

        var payload = new CreateWasteBinRequest { BinCode = "BIN-001" };

        var resCitizen = await client.SendAsync(CreateAuthorizedJsonRequest(HttpMethod.Post, "/api/v1/bins", citizenToken, payload));
        resCitizen.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var resManager = await client.SendAsync(CreateAuthorizedJsonRequest(HttpMethod.Post, "/api/v1/bins", managerToken, payload));
        resManager.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var resDriver = await client.SendAsync(CreateAuthorizedJsonRequest(HttpMethod.Post, "/api/v1/bins", driverToken, payload));
        resDriver.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Update_CitizenAndManagerAndDriverTokens_Return403Forbidden()
    {
        var (client, _) = CreateClientWithMockService();
        var citizenToken = await GetCitizenTokenAsync();
        var managerToken = await GetManagerTokenAsync();
        var driverToken = await GetDriverTokenAsync();
        var binId = Guid.NewGuid();

        var payload = new UpdateWasteBinRequest { CapacityLiters = 1100 };

        var resCitizen = await client.SendAsync(CreateAuthorizedJsonRequest(HttpMethod.Put, $"/api/v1/bins/{binId}", citizenToken, payload));
        resCitizen.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var resManager = await client.SendAsync(CreateAuthorizedJsonRequest(HttpMethod.Put, $"/api/v1/bins/{binId}", managerToken, payload));
        resManager.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var resDriver = await client.SendAsync(CreateAuthorizedJsonRequest(HttpMethod.Put, $"/api/v1/bins/{binId}", driverToken, payload));
        resDriver.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("POST", "/api/v1/bins")]
    [InlineData("PUT", "/api/v1/bins/00000000-0000-0000-0000-000000000001")]
    public async Task WriteEndpoints_Unauthenticated_Return401Unauthorized(string method, string url)
    {
        var (client, _) = CreateClientWithMockService();

        var request = CreateAuthorizedJsonRequest(new HttpMethod(method), url, null, new { });
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 6. BIN OBSERVATION ENDPOINTS (POST & GET /api/v1/bins/{id}/observations)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RecordObservation_WasteOfficerToken_ValidPayload_Returns201Created()
    {
        var (client, mockObs) = CreateClientWithMockObsService();
        var officerToken = await GetOfficerTokenAsync();
        var binId = Guid.NewGuid();

        var requestDto = new RecordBinObservationRequest
        {
            FillLevelPercent = 100,
            Condition = BinCondition.Good,
            Notes = "Severe overflow observed due to market crowd."
        };

        var expectedDto = new BinObservationDto
        {
            Id = Guid.NewGuid(),
            WasteBinId = binId,
            FillLevelPercent = 100,
            Condition = BinCondition.Good,
            Notes = "Severe overflow observed due to market crowd.",
            RecordedByUserId = Guid.NewGuid(),
            RecordedByUserName = "Officer Silva",
            RecordedAt = DateTime.UtcNow
        };

        mockObs.Setup(s => s.RecordObservationAsync(
                binId,
                It.Is<RecordBinObservationRequest>(r => r.FillLevelPercent == 100 && r.Condition == BinCondition.Good),
                It.IsAny<Guid>(),
                AppRoles.WasteOfficer,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDto);

        var request = CreateAuthorizedJsonRequest(HttpMethod.Post, $"/api/v1/bins/{binId}/observations", officerToken, requestDto);
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var detail = await response.Content.ReadFromJsonAsync<BinObservationDto>(SharedTestJsonOptions);
        detail.Should().NotBeNull();
        detail!.Id.Should().Be(expectedDto.Id);
        detail.WasteBinId.Should().Be(binId);
        detail.FillLevelPercent.Should().Be(100);
        detail.Condition.Should().Be(BinCondition.Good);
    }

    [Fact]
    public async Task RecordObservation_CitizenAndManagerAndDriverTokens_Return403Forbidden()
    {
        var (client, mockObs) = CreateClientWithMockObsService();
        var citizenToken = await GetCitizenTokenAsync();
        var managerToken = await GetManagerTokenAsync();
        var driverToken = await GetDriverTokenAsync();
        var binId = Guid.NewGuid();

        var payload = new RecordBinObservationRequest
        {
            FillLevelPercent = 50,
            Condition = BinCondition.Good
        };

        var resCitizen = await client.SendAsync(CreateAuthorizedJsonRequest(HttpMethod.Post, $"/api/v1/bins/{binId}/observations", citizenToken, payload));
        resCitizen.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var resManager = await client.SendAsync(CreateAuthorizedJsonRequest(HttpMethod.Post, $"/api/v1/bins/{binId}/observations", managerToken, payload));
        resManager.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var resDriver = await client.SendAsync(CreateAuthorizedJsonRequest(HttpMethod.Post, $"/api/v1/bins/{binId}/observations", driverToken, payload));
        resDriver.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        mockObs.Verify(s => s.RecordObservationAsync(It.IsAny<Guid>(), It.IsAny<RecordBinObservationRequest>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RecordObservation_ValidationFailure_Returns400BadRequest()
    {
        var (client, mockObs) = CreateClientWithMockObsService();
        var officerToken = await GetOfficerTokenAsync();
        var binId = Guid.NewGuid();

        var requestDto = new RecordBinObservationRequest
        {
            FillLevelPercent = 33, // Invalid: not 0, 25, 50, 75, 100
            Condition = BinCondition.Good
        };

        mockObs.Setup(s => s.RecordObservationAsync(
                binId,
                It.IsAny<RecordBinObservationRequest>(),
                It.IsAny<Guid>(),
                AppRoles.WasteOfficer,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException(new[]
            {
                new FluentValidation.Results.ValidationFailure("FillLevelPercent", "Fill level percent must be exactly 0, 25, 50, 75, or 100.")
            }));

        var request = CreateAuthorizedJsonRequest(HttpMethod.Post, $"/api/v1/bins/{binId}/observations", officerToken, requestDto);
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(SharedTestJsonOptions);
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status400BadRequest);
        problem.Errors.Should().ContainKey("FillLevelPercent");
    }

    [Fact]
    public async Task RecordObservation_MissingBin_Returns404NotFound()
    {
        var (client, mockObs) = CreateClientWithMockObsService();
        var officerToken = await GetOfficerTokenAsync();
        var missingId = Guid.NewGuid();

        var requestDto = new RecordBinObservationRequest
        {
            FillLevelPercent = 50,
            Condition = BinCondition.Good
        };

        mockObs.Setup(s => s.RecordObservationAsync(
                missingId,
                It.IsAny<RecordBinObservationRequest>(),
                It.IsAny<Guid>(),
                AppRoles.WasteOfficer,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException($"Waste bin '{missingId}' was not found."));

        var request = CreateAuthorizedJsonRequest(HttpMethod.Post, $"/api/v1/bins/{missingId}/observations", officerToken, requestDto);
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(SharedTestJsonOptions);
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task RecordObservation_RetiredBinConflict_Returns409Conflict()
    {
        var (client, mockObs) = CreateClientWithMockObsService();
        var officerToken = await GetOfficerTokenAsync();
        var retiredId = Guid.NewGuid();

        var requestDto = new RecordBinObservationRequest
        {
            FillLevelPercent = 50,
            Condition = BinCondition.Good
        };

        mockObs.Setup(s => s.RecordObservationAsync(
                retiredId,
                It.IsAny<RecordBinObservationRequest>(),
                It.IsAny<Guid>(),
                AppRoles.WasteOfficer,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BusinessRuleConflictException("Cannot record an observation for a retired waste bin."));

        var request = CreateAuthorizedJsonRequest(HttpMethod.Post, $"/api/v1/bins/{retiredId}/observations", officerToken, requestDto);
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(SharedTestJsonOptions);
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status409Conflict);
        problem.Title.Should().Be("Business Rule Conflict");
        problem.Detail.Should().Contain("retired");
    }

    [Fact]
    public async Task GetObservationHistory_WasteOfficerToken_Returns200OKWithPagedResult()
    {
        var (client, mockObs) = CreateClientWithMockObsService();
        var officerToken = await GetOfficerTokenAsync();
        var binId = Guid.NewGuid();

        var expectedResult = new PagedResult<BinObservationDto>
        {
            Items = new List<BinObservationDto>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    WasteBinId = binId,
                    FillLevelPercent = 100,
                    Condition = BinCondition.Good,
                    Notes = "Full",
                    RecordedByUserId = Guid.NewGuid(),
                    RecordedByUserName = "Officer Silva",
                    RecordedAt = DateTime.UtcNow
                }
            },
            Page = 1,
            PageSize = 20,
            TotalCount = 1
        };

        mockObs.Setup(s => s.GetObservationHistoryAsync(
                binId,
                It.Is<ObservationListQuery>(q => q.Page == 1 && q.PageSize == 20),
                It.IsAny<Guid>(),
                AppRoles.WasteOfficer,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var request = CreateAuthorizedRequest(HttpMethod.Get, $"/api/v1/bins/{binId}/observations?page=1&pageSize=20", officerToken);
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<BinObservationDto>>(SharedTestJsonOptions);
        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(1);
        result.Items[0].WasteBinId.Should().Be(binId);
    }

    [Fact]
    public async Task GetObservationHistory_MunicipalManagerToken_Returns200OKWithPagedResult()
    {
        var (client, mockObs) = CreateClientWithMockObsService();
        var managerToken = await GetManagerTokenAsync();
        var binId = Guid.NewGuid();

        var expectedResult = new PagedResult<BinObservationDto>
        {
            Items = new List<BinObservationDto>(),
            Page = 1,
            PageSize = 20,
            TotalCount = 0
        };

        mockObs.Setup(s => s.GetObservationHistoryAsync(
                binId,
                It.IsAny<ObservationListQuery>(),
                It.IsAny<Guid>(),
                AppRoles.MunicipalManager,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var request = CreateAuthorizedRequest(HttpMethod.Get, $"/api/v1/bins/{binId}/observations", managerToken);
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetObservationHistory_CitizenAndDriverTokens_Return403Forbidden()
    {
        var (client, mockObs) = CreateClientWithMockObsService();
        var citizenToken = await GetCitizenTokenAsync();
        var driverToken = await GetDriverTokenAsync();
        var binId = Guid.NewGuid();

        var resCitizen = await client.SendAsync(CreateAuthorizedRequest(HttpMethod.Get, $"/api/v1/bins/{binId}/observations", citizenToken));
        resCitizen.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var resDriver = await client.SendAsync(CreateAuthorizedRequest(HttpMethod.Get, $"/api/v1/bins/{binId}/observations", driverToken));
        resDriver.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        mockObs.Verify(s => s.GetObservationHistoryAsync(It.IsAny<Guid>(), It.IsAny<ObservationListQuery>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetObservationHistory_ValidationFailure_Returns400BadRequest()
    {
        var (client, mockObs) = CreateClientWithMockObsService();
        var officerToken = await GetOfficerTokenAsync();
        var binId = Guid.NewGuid();

        mockObs.Setup(s => s.GetObservationHistoryAsync(
                binId,
                It.IsAny<ObservationListQuery>(),
                It.IsAny<Guid>(),
                AppRoles.WasteOfficer,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException(new[]
            {
                new FluentValidation.Results.ValidationFailure("PageSize", "Page size must be between 1 and 100.")
            }));

        var request = CreateAuthorizedRequest(HttpMethod.Get, $"/api/v1/bins/{binId}/observations?pageSize=500", officerToken);
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(SharedTestJsonOptions);
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status400BadRequest);
        problem.Errors.Should().ContainKey("PageSize");
    }

    [Fact]
    public async Task GetObservationHistory_MissingBin_Returns404NotFound()
    {
        var (client, mockObs) = CreateClientWithMockObsService();
        var officerToken = await GetOfficerTokenAsync();
        var missingId = Guid.NewGuid();

        mockObs.Setup(s => s.GetObservationHistoryAsync(
                missingId,
                It.IsAny<ObservationListQuery>(),
                It.IsAny<Guid>(),
                AppRoles.WasteOfficer,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException($"Waste bin '{missingId}' was not found."));

        var request = CreateAuthorizedRequest(HttpMethod.Get, $"/api/v1/bins/{missingId}/observations", officerToken);
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(SharedTestJsonOptions);
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status404NotFound);
    }

    [Theory]
    [InlineData("POST", "/api/v1/bins/00000000-0000-0000-0000-000000000001/observations")]
    [InlineData("GET", "/api/v1/bins/00000000-0000-0000-0000-000000000001/observations")]
    public async Task ObservationEndpoints_Unauthenticated_Return401Unauthorized(string method, string url)
    {
        var (client, _) = CreateClientWithMockObsService();

        var request = CreateAuthorizedJsonRequest(new HttpMethod(method), url, null, new { });
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 7. BIN DEACTIVATION ENDPOINT (POST /api/v1/bins/{id}/deactivate)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Deactivate_WasteOfficerToken_ValidRequest_Returns200OK()
    {
        var (client, mockBin) = CreateClientWithMockService();
        var officerToken = await GetOfficerTokenAsync();
        var binId = Guid.NewGuid();

        var requestDto = new DeactivateWasteBinRequest
        {
            TargetStatus = BinAdministrativeStatus.OutOfService,
            Reason = "Damaged hinge undergoing depot repair."
        };

        var expectedDetail = new WasteBinDetailDto
        {
            Id = binId,
            BinCode = "BIN-COL-0042",
            AdministrativeStatus = BinAdministrativeStatus.OutOfService,
            UpdatedAt = DateTime.UtcNow
        };

        mockBin.Setup(s => s.DeactivateAsync(
                binId,
                It.Is<DeactivateWasteBinRequest>(r => r.TargetStatus == BinAdministrativeStatus.OutOfService && r.Reason!.Contains("Damaged hinge")),
                It.IsAny<Guid>(),
                AppRoles.WasteOfficer,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDetail);

        var request = CreateAuthorizedJsonRequest(HttpMethod.Post, $"/api/v1/bins/{binId}/deactivate", officerToken, requestDto);
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await response.Content.ReadFromJsonAsync<WasteBinDetailDto>(SharedTestJsonOptions);
        detail.Should().NotBeNull();
        detail!.Id.Should().Be(binId);
        detail.AdministrativeStatus.Should().Be(BinAdministrativeStatus.OutOfService);
    }

    [Fact]
    public async Task Deactivate_CitizenAndManagerAndDriverTokens_Return403Forbidden()
    {
        var (client, mockBin) = CreateClientWithMockService();
        var citizenToken = await GetCitizenTokenAsync();
        var managerToken = await GetManagerTokenAsync();
        var driverToken = await GetDriverTokenAsync();
        var binId = Guid.NewGuid();

        var payload = new DeactivateWasteBinRequest
        {
            TargetStatus = BinAdministrativeStatus.OutOfService
        };

        var resCitizen = await client.SendAsync(CreateAuthorizedJsonRequest(HttpMethod.Post, $"/api/v1/bins/{binId}/deactivate", citizenToken, payload));
        resCitizen.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var resManager = await client.SendAsync(CreateAuthorizedJsonRequest(HttpMethod.Post, $"/api/v1/bins/{binId}/deactivate", managerToken, payload));
        resManager.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var resDriver = await client.SendAsync(CreateAuthorizedJsonRequest(HttpMethod.Post, $"/api/v1/bins/{binId}/deactivate", driverToken, payload));
        resDriver.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        mockBin.Verify(s => s.DeactivateAsync(It.IsAny<Guid>(), It.IsAny<DeactivateWasteBinRequest>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Deactivate_Unauthenticated_Returns401Unauthorized()
    {
        var (client, _) = CreateClientWithMockService();
        var binId = Guid.NewGuid();

        var request = CreateAuthorizedJsonRequest(HttpMethod.Post, $"/api/v1/bins/{binId}/deactivate", null, new { });
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Deactivate_ActiveTaskConflict_Returns409Conflict()
    {
        var (client, mockBin) = CreateClientWithMockService();
        var officerToken = await GetOfficerTokenAsync();
        var binId = Guid.NewGuid();

        var requestDto = new DeactivateWasteBinRequest
        {
            TargetStatus = BinAdministrativeStatus.OutOfService
        };

        mockBin.Setup(s => s.DeactivateAsync(
                binId,
                It.IsAny<DeactivateWasteBinRequest>(),
                It.IsAny<Guid>(),
                AppRoles.WasteOfficer,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BusinessRuleConflictException("Cannot deactivate a waste bin with an active collection task (Scheduled, Assigned, or InProgress)."));

        var request = CreateAuthorizedJsonRequest(HttpMethod.Post, $"/api/v1/bins/{binId}/deactivate", officerToken, requestDto);
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(SharedTestJsonOptions);
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status409Conflict);
        problem.Title.Should().Be("Business Rule Conflict");
        problem.Detail.Should().Contain("active collection task");
    }

    [Fact]
    public async Task Deactivate_RetiredBinConflict_Returns409Conflict()
    {
        var (client, mockBin) = CreateClientWithMockService();
        var officerToken = await GetOfficerTokenAsync();
        var binId = Guid.NewGuid();

        var requestDto = new DeactivateWasteBinRequest
        {
            TargetStatus = BinAdministrativeStatus.OutOfService
        };

        mockBin.Setup(s => s.DeactivateAsync(
                binId,
                It.IsAny<DeactivateWasteBinRequest>(),
                It.IsAny<Guid>(),
                AppRoles.WasteOfficer,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BusinessRuleConflictException("Waste bin is already retired and cannot be modified."));

        var request = CreateAuthorizedJsonRequest(HttpMethod.Post, $"/api/v1/bins/{binId}/deactivate", officerToken, requestDto);
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(SharedTestJsonOptions);
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status409Conflict);
        problem.Title.Should().Be("Business Rule Conflict");
        problem.Detail.Should().Contain("already retired");
    }

    [Fact]
    public async Task Deactivate_MissingBin_Returns404NotFound()
    {
        var (client, mockBin) = CreateClientWithMockService();
        var officerToken = await GetOfficerTokenAsync();
        var missingId = Guid.NewGuid();

        var requestDto = new DeactivateWasteBinRequest
        {
            TargetStatus = BinAdministrativeStatus.OutOfService
        };

        mockBin.Setup(s => s.DeactivateAsync(
                missingId,
                It.IsAny<DeactivateWasteBinRequest>(),
                It.IsAny<Guid>(),
                AppRoles.WasteOfficer,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException($"Waste bin '{missingId}' was not found."));

        var request = CreateAuthorizedJsonRequest(HttpMethod.Post, $"/api/v1/bins/{missingId}/deactivate", officerToken, requestDto);
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(SharedTestJsonOptions);
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Deactivate_ValidationFailure_Returns400BadRequest()
    {
        var (client, mockBin) = CreateClientWithMockService();
        var officerToken = await GetOfficerTokenAsync();
        var binId = Guid.NewGuid();

        var requestDto = new DeactivateWasteBinRequest
        {
            TargetStatus = null
        };

        mockBin.Setup(s => s.DeactivateAsync(
                binId,
                It.IsAny<DeactivateWasteBinRequest>(),
                It.IsAny<Guid>(),
                AppRoles.WasteOfficer,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException(new[]
            {
                new FluentValidation.Results.ValidationFailure("TargetStatus", "Target status is required.")
            }));

        var request = CreateAuthorizedJsonRequest(HttpMethod.Post, $"/api/v1/bins/{binId}/deactivate", officerToken, requestDto);
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(SharedTestJsonOptions);
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status400BadRequest);
        problem.Errors.Should().ContainKey("TargetStatus");
    }
}
