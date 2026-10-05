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
using Xunit;

namespace SmartWaste.Tests.Collection.Controllers;

/// <summary>
/// End-to-end API integration tests for CollectionTasksController endpoints (Section 4.4).
/// Exercises real ASP.NET Core routing, JWT Bearer authentication, role-based authorization attributes,
/// model binding, Location headers, and ExceptionHandlingMiddleware using CustomWebApplicationFactory with a mocked
/// ICollectionTaskService to guarantee zero database mutations.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class CollectionTasksApiIntegrationTests
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _baseClient;

    private static readonly JsonSerializerOptions SharedTestJsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false) }
    };

    public CollectionTasksApiIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _baseClient = factory.CreateClient();
    }

    private (HttpClient client, Mock<ICollectionTaskService> mockService) CreateClientWithMockService()
    {
        var mock = new Mock<ICollectionTaskService>();
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ICollectionTaskService>();
                services.AddSingleton<ICollectionTaskService>(mock.Object);
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
        var email = $"citizen_tasks_{Guid.NewGuid():N}@smartwaste.test";
        var regReq = new RegisterRequest
        {
            FullName = "Citizen Task Viewer",
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

    private static HttpRequestMessage CreateAuthorizedRequest(HttpMethod method, string url, string? token, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        if (body != null)
        {
            request.Content = JsonContent.Create(body, options: SharedTestJsonOptions);
        }
        return request;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 1. POST /api/v1/collection-tasks/manual (CreateManualTask)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateManualTask_WasteOfficerToken_Returns201CreatedWithLocationHeaderAndBody()
    {
        var (client, mock) = CreateClientWithMockService();
        var officerToken = await GetOfficerTokenAsync();
        var taskId = Guid.NewGuid();
        var binId = Guid.NewGuid();

        var requestBody = new CreateManualCollectionTaskRequest
        {
            WasteBinId = binId,
            CollectionReason = CollectionReason.FullOrBlockedBin,
            ScheduledAt = DateTime.UtcNow.AddHours(3),
            HandlingNotes = "Compactor vehicle required"
        };

        var expectedDetail = new CollectionTaskDetailDto
        {
            Id = taskId,
            TaskCode = "TSK-20260921-0012",
            TargetType = "Bin",
            WasteBinId = binId,
            CollectionReason = CollectionReason.FullOrBlockedBin,
            Status = CollectionTaskStatus.Scheduled,
            ScheduledAt = requestBody.ScheduledAt.Value,
            HandlingNotes = requestBody.HandlingNotes,
            CreatedByUserId = Guid.NewGuid(),
            CreatedByUserName = "Officer Silva",
            CreationMethod = TaskCreationMethod.Manual,
            CreatedAt = DateTime.UtcNow
        };

        mock.Setup(s => s.CreateManualTaskAsync(
                It.Is<CreateManualCollectionTaskRequest>(r => r.WasteBinId == binId && r.CollectionReason == CollectionReason.FullOrBlockedBin),
                It.IsAny<Guid>(),
                AppRoles.WasteOfficer,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDetail);

        var request = CreateAuthorizedRequest(HttpMethod.Post, "/api/v1/collection-tasks/manual", officerToken, requestBody);
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Contain($"/api/v1/collection-tasks/{taskId}");

        var result = await response.Content.ReadFromJsonAsync<CollectionTaskDetailDto>(SharedTestJsonOptions);
        result.Should().NotBeNull();
        result!.Id.Should().Be(taskId);
        result.TaskCode.Should().Be("TSK-20260921-0012");
        result.TargetType.Should().Be("Bin");
        result.WasteBinId.Should().Be(binId);
        result.Status.Should().Be(CollectionTaskStatus.Scheduled);
    }

    [Fact]
    public async Task CreateManualTask_MunicipalManagerToken_Returns403Forbidden()
    {
        var (client, mock) = CreateClientWithMockService();
        var managerToken = await GetManagerTokenAsync();

        var request = CreateAuthorizedRequest(HttpMethod.Post, "/api/v1/collection-tasks/manual", managerToken, new CreateManualCollectionTaskRequest
        {
            WasteBinId = Guid.NewGuid(),
            CollectionReason = CollectionReason.FullOrBlockedBin,
            ScheduledAt = DateTime.UtcNow.AddHours(2)
        });
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        mock.Verify(s => s.CreateManualTaskAsync(It.IsAny<CreateManualCollectionTaskRequest>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateManualTask_CitizenToken_Returns403Forbidden()
    {
        var (client, mock) = CreateClientWithMockService();
        var citizenToken = await GetCitizenTokenAsync();

        var request = CreateAuthorizedRequest(HttpMethod.Post, "/api/v1/collection-tasks/manual", citizenToken, new CreateManualCollectionTaskRequest());
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        mock.Verify(s => s.CreateManualTaskAsync(It.IsAny<CreateManualCollectionTaskRequest>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateManualTask_DriverToken_Returns403Forbidden()
    {
        var (client, mock) = CreateClientWithMockService();
        var driverToken = await GetDriverTokenAsync();

        var request = CreateAuthorizedRequest(HttpMethod.Post, "/api/v1/collection-tasks/manual", driverToken, new CreateManualCollectionTaskRequest());
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        mock.Verify(s => s.CreateManualTaskAsync(It.IsAny<CreateManualCollectionTaskRequest>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateManualTask_Unauthenticated_Returns401Unauthorized()
    {
        var (client, mock) = CreateClientWithMockService();

        var request = CreateAuthorizedRequest(HttpMethod.Post, "/api/v1/collection-tasks/manual", null, new CreateManualCollectionTaskRequest());
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        mock.Verify(s => s.CreateManualTaskAsync(It.IsAny<CreateManualCollectionTaskRequest>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateManualTask_ValidationError_Returns400BadRequest()
    {
        var (client, mock) = CreateClientWithMockService();
        var officerToken = await GetOfficerTokenAsync();

        var requestBody = new CreateManualCollectionTaskRequest
        {
            WasteBinId = Guid.NewGuid(),
            CollectionReason = CollectionReason.FullOrBlockedBin,
            ScheduledAt = DateTime.UtcNow.AddHours(2)
        };

        mock.Setup(s => s.CreateManualTaskAsync(
                It.IsAny<CreateManualCollectionTaskRequest>(),
                It.IsAny<Guid>(),
                AppRoles.WasteOfficer,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException(new[]
            {
                new FluentValidation.Results.ValidationFailure("WasteReportId", "Exactly one of WasteReportId or WasteBinId must be provided.")
            }));

        var request = CreateAuthorizedRequest(HttpMethod.Post, "/api/v1/collection-tasks/manual", officerToken, requestBody);
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(SharedTestJsonOptions);
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status400BadRequest);
        problem.Errors.Keys.Should().Contain(k => string.Equals(k, "WasteReportId", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CreateManualTask_ModelValidationError_Returns400BadRequest()
    {
        var (client, mock) = CreateClientWithMockService();
        var officerToken = await GetOfficerTokenAsync();

        var request = CreateAuthorizedRequest(HttpMethod.Post, "/api/v1/collection-tasks/manual", officerToken, new CreateManualCollectionTaskRequest());
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(SharedTestJsonOptions);
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status400BadRequest);
        problem.Errors.Keys.Should().Contain(k => string.Equals(k, "ScheduledAt", StringComparison.OrdinalIgnoreCase));
        mock.Verify(s => s.CreateManualTaskAsync(It.IsAny<CreateManualCollectionTaskRequest>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateManualTask_BusinessConflict_Returns409Conflict()
    {
        var (client, mock) = CreateClientWithMockService();
        var officerToken = await GetOfficerTokenAsync();

        mock.Setup(s => s.CreateManualTaskAsync(
                It.IsAny<CreateManualCollectionTaskRequest>(),
                It.IsAny<Guid>(),
                AppRoles.WasteOfficer,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BusinessRuleConflictException("An active collection task already exists for this target."));

        var request = CreateAuthorizedRequest(HttpMethod.Post, "/api/v1/collection-tasks/manual", officerToken, new CreateManualCollectionTaskRequest
        {
            WasteBinId = Guid.NewGuid(),
            CollectionReason = CollectionReason.FullOrBlockedBin,
            ScheduledAt = DateTime.UtcNow.AddHours(2)
        });
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(SharedTestJsonOptions);
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status409Conflict);
        problem.Detail.Should().Contain("active collection task already exists");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 2. GET /api/v1/collection-tasks (GetList)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetList_WasteOfficerToken_Returns200OKWithPagedResult()
    {
        var (client, mock) = CreateClientWithMockService();
        var officerToken = await GetOfficerTokenAsync();
        var taskId = Guid.NewGuid();

        var expectedResult = new PagedResult<CollectionTaskSummaryDto>
        {
            Items = new List<CollectionTaskSummaryDto>
            {
                new()
                {
                    Id = taskId,
                    TaskCode = "TSK-20260921-0001",
                    TargetType = "Bin",
                    WasteBinId = Guid.NewGuid(),
                    TargetReference = "BIN-COL-0042",
                    CollectionReason = CollectionReason.FullOrBlockedBin,
                    Status = CollectionTaskStatus.Scheduled,
                    ScheduledAt = DateTime.UtcNow.AddHours(2),
                    CreationMethod = TaskCreationMethod.Manual,
                    CreatedByUserId = Guid.NewGuid(),
                    CreatedByUserName = "Officer Silva",
                    CreatedAt = DateTime.UtcNow
                }
            },
            Page = 1,
            PageSize = 20,
            TotalCount = 1
        };

        mock.Setup(s => s.GetListAsync(
                It.Is<CollectionTaskListQuery>(q => q.Status == CollectionTaskStatus.Scheduled && q.Page == 1 && q.PageSize == 20),
                It.IsAny<Guid>(),
                AppRoles.WasteOfficer,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var request = CreateAuthorizedRequest(HttpMethod.Get, "/api/v1/collection-tasks?status=Scheduled&page=1&pageSize=20", officerToken);
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<CollectionTaskSummaryDto>>(SharedTestJsonOptions);
        result.Should().NotBeNull();
        result!.TotalCount.Should().Be(1);
        result.Items.Should().HaveCount(1);
        result.Items[0].Id.Should().Be(taskId);
        result.Items[0].TaskCode.Should().Be("TSK-20260921-0001");
    }

    [Fact]
    public async Task GetList_DateOnlyFilters_BindToCalendarDatesWithoutInvokingTheDatabaseService()
    {
        var (client, mock) = CreateClientWithMockService();
        var officerToken = await GetOfficerTokenAsync();
        var fromDate = new DateOnly(2026, 9, 23);
        var toDate = new DateOnly(2026, 9, 24);

        mock.Setup(s => s.GetListAsync(
                It.Is<CollectionTaskListQuery>(q =>
                    q.DateFrom == fromDate && q.DateTo == toDate && q.Page == 1 && q.PageSize == 20),
                It.IsAny<Guid>(),
                AppRoles.WasteOfficer,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<CollectionTaskSummaryDto>
            {
                Items = new List<CollectionTaskSummaryDto>(),
                Page = 1,
                PageSize = 20,
                TotalCount = 0
            });

        var request = CreateAuthorizedRequest(
            HttpMethod.Get,
            "/api/v1/collection-tasks?dateFrom=2026-09-23&dateTo=2026-09-24&page=1&pageSize=20",
            officerToken);
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        mock.VerifyAll();
    }

    [Fact]
    public async Task GetList_MunicipalManagerToken_Returns200OKWithPagedResult()
    {
        var (client, mock) = CreateClientWithMockService();
        var managerToken = await GetManagerTokenAsync();

        var expectedResult = new PagedResult<CollectionTaskSummaryDto>
        {
            Items = new List<CollectionTaskSummaryDto>(),
            Page = 1,
            PageSize = 20,
            TotalCount = 0
        };

        mock.Setup(s => s.GetListAsync(
                It.IsAny<CollectionTaskListQuery>(),
                It.IsAny<Guid>(),
                AppRoles.MunicipalManager,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var request = CreateAuthorizedRequest(HttpMethod.Get, "/api/v1/collection-tasks", managerToken);
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<CollectionTaskSummaryDto>>(SharedTestJsonOptions);
        result.Should().NotBeNull();
        result!.TotalCount.Should().Be(0);
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetList_CitizenToken_Returns403Forbidden()
    {
        var (client, mock) = CreateClientWithMockService();
        var citizenToken = await GetCitizenTokenAsync();

        var request = CreateAuthorizedRequest(HttpMethod.Get, "/api/v1/collection-tasks", citizenToken);
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        mock.Verify(s => s.GetListAsync(It.IsAny<CollectionTaskListQuery>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetList_DriverToken_Returns403Forbidden()
    {
        var (client, mock) = CreateClientWithMockService();
        var driverToken = await GetDriverTokenAsync();

        var request = CreateAuthorizedRequest(HttpMethod.Get, "/api/v1/collection-tasks", driverToken);
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        mock.Verify(s => s.GetListAsync(It.IsAny<CollectionTaskListQuery>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetList_Unauthenticated_Returns401Unauthorized()
    {
        var (client, mock) = CreateClientWithMockService();

        var request = CreateAuthorizedRequest(HttpMethod.Get, "/api/v1/collection-tasks", null);
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        mock.Verify(s => s.GetListAsync(It.IsAny<CollectionTaskListQuery>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 3. GET /api/v1/collection-tasks/{id:guid} (GetById)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetById_WasteOfficerToken_Returns200OKWithDetail()
    {
        var (client, mock) = CreateClientWithMockService();
        var officerToken = await GetOfficerTokenAsync();
        var taskId = Guid.NewGuid();

        var expectedDetail = new CollectionTaskDetailDto
        {
            Id = taskId,
            TaskCode = "TSK-20260921-0012",
            TargetType = "Bin",
            WasteBinId = Guid.NewGuid(),
            CollectionReason = CollectionReason.FullOrBlockedBin,
            Status = CollectionTaskStatus.Scheduled,
            ScheduledAt = DateTime.UtcNow.AddHours(2),
            CreatedByUserId = Guid.NewGuid(),
            CreatedByUserName = "Officer Silva",
            CreationMethod = TaskCreationMethod.Manual,
            CreatedAt = DateTime.UtcNow
        };

        mock.Setup(s => s.GetByIdAsync(taskId, It.IsAny<Guid>(), AppRoles.WasteOfficer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDetail);

        var request = CreateAuthorizedRequest(HttpMethod.Get, $"/api/v1/collection-tasks/{taskId}", officerToken);
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<CollectionTaskDetailDto>(SharedTestJsonOptions);
        result.Should().NotBeNull();
        result!.Id.Should().Be(taskId);
        result.TaskCode.Should().Be("TSK-20260921-0012");
    }

    [Fact]
    public async Task GetById_MunicipalManagerToken_Returns200OKWithDetail()
    {
        var (client, mock) = CreateClientWithMockService();
        var managerToken = await GetManagerTokenAsync();
        var taskId = Guid.NewGuid();

        var expectedDetail = new CollectionTaskDetailDto
        {
            Id = taskId,
            TaskCode = "TSK-20260921-0015",
            TargetType = "Report",
            WasteReportId = Guid.NewGuid(),
            CollectionReason = CollectionReason.VerifiedReport,
            Status = CollectionTaskStatus.Scheduled,
            ScheduledAt = DateTime.UtcNow.AddHours(3),
            CreatedByUserId = Guid.NewGuid(),
            CreatedByUserName = "Officer Silva",
            CreationMethod = TaskCreationMethod.Manual,
            CreatedAt = DateTime.UtcNow
        };

        mock.Setup(s => s.GetByIdAsync(taskId, It.IsAny<Guid>(), AppRoles.MunicipalManager, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDetail);

        var request = CreateAuthorizedRequest(HttpMethod.Get, $"/api/v1/collection-tasks/{taskId}", managerToken);
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<CollectionTaskDetailDto>(SharedTestJsonOptions);
        result.Should().NotBeNull();
        result!.Id.Should().Be(taskId);
    }

    [Fact]
    public async Task GetById_NotFound_Returns404NotFound()
    {
        var (client, mock) = CreateClientWithMockService();
        var officerToken = await GetOfficerTokenAsync();
        var nonExistentId = Guid.NewGuid();

        mock.Setup(s => s.GetByIdAsync(nonExistentId, It.IsAny<Guid>(), AppRoles.WasteOfficer, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException($"Collection task '{nonExistentId}' was not found."));

        var request = CreateAuthorizedRequest(HttpMethod.Get, $"/api/v1/collection-tasks/{nonExistentId}", officerToken);
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(SharedTestJsonOptions);
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task GetById_CitizenToken_Returns403Forbidden()
    {
        var (client, mock) = CreateClientWithMockService();
        var citizenToken = await GetCitizenTokenAsync();
        var taskId = Guid.NewGuid();

        var request = CreateAuthorizedRequest(HttpMethod.Get, $"/api/v1/collection-tasks/{taskId}", citizenToken);
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        mock.Verify(s => s.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 4. POST /api/v1/collection-tasks/{id:guid}/reschedule (RescheduleTask)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RescheduleTask_WasteOfficerToken_Returns200OKWithUpdatedDetail()
    {
        var (client, mock) = CreateClientWithMockService();
        var officerToken = await GetOfficerTokenAsync();
        var taskId = Guid.NewGuid();

        var requestBody = new RescheduleCollectionTaskRequest
        {
            NewScheduledAt = DateTime.UtcNow.AddHours(6),
            Reason = "Depot vehicle maintenance delayed morning departure."
        };

        var expectedDetail = new CollectionTaskDetailDto
        {
            Id = taskId,
            TaskCode = "TSK-20260921-0012",
            TargetType = "Bin",
            WasteBinId = Guid.NewGuid(),
            CollectionReason = CollectionReason.FullOrBlockedBin,
            Status = CollectionTaskStatus.Scheduled,
            ScheduledAt = requestBody.NewScheduledAt.Value,
            CreatedByUserId = Guid.NewGuid(),
            CreatedByUserName = "Officer Silva",
            CreationMethod = TaskCreationMethod.Manual,
            CreatedAt = DateTime.UtcNow.AddHours(-2),
            UpdatedAt = DateTime.UtcNow
        };

        mock.Setup(s => s.RescheduleTaskAsync(
                taskId,
                It.Is<RescheduleCollectionTaskRequest>(r => r.Reason == requestBody.Reason),
                It.IsAny<Guid>(),
                AppRoles.WasteOfficer,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDetail);

        var request = CreateAuthorizedRequest(HttpMethod.Post, $"/api/v1/collection-tasks/{taskId}/reschedule", officerToken, requestBody);
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<CollectionTaskDetailDto>(SharedTestJsonOptions);
        result.Should().NotBeNull();
        result!.Id.Should().Be(taskId);
        result.ScheduledAt.Should().BeCloseTo(requestBody.NewScheduledAt.Value, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task RescheduleTask_MunicipalManagerToken_Returns403Forbidden()
    {
        var (client, mock) = CreateClientWithMockService();
        var managerToken = await GetManagerTokenAsync();
        var taskId = Guid.NewGuid();

        var request = CreateAuthorizedRequest(HttpMethod.Post, $"/api/v1/collection-tasks/{taskId}/reschedule", managerToken, new RescheduleCollectionTaskRequest
        {
            NewScheduledAt = DateTime.UtcNow.AddHours(4),
            Reason = "Manager reschedule attempt"
        });
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        mock.Verify(s => s.RescheduleTaskAsync(It.IsAny<Guid>(), It.IsAny<RescheduleCollectionTaskRequest>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RescheduleTask_NotFound_Returns404NotFound()
    {
        var (client, mock) = CreateClientWithMockService();
        var officerToken = await GetOfficerTokenAsync();
        var nonExistentId = Guid.NewGuid();

        mock.Setup(s => s.RescheduleTaskAsync(
                nonExistentId,
                It.IsAny<RescheduleCollectionTaskRequest>(),
                It.IsAny<Guid>(),
                AppRoles.WasteOfficer,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException($"Collection task '{nonExistentId}' was not found."));

        var request = CreateAuthorizedRequest(HttpMethod.Post, $"/api/v1/collection-tasks/{nonExistentId}/reschedule", officerToken, new RescheduleCollectionTaskRequest
        {
            NewScheduledAt = DateTime.UtcNow.AddHours(4),
            Reason = "Rescheduling non-existent task"
        });
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RescheduleTask_Conflict_Returns409Conflict()
    {
        var (client, mock) = CreateClientWithMockService();
        var officerToken = await GetOfficerTokenAsync();
        var taskId = Guid.NewGuid();

        mock.Setup(s => s.RescheduleTaskAsync(
                taskId,
                It.IsAny<RescheduleCollectionTaskRequest>(),
                It.IsAny<Guid>(),
                AppRoles.WasteOfficer,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BusinessRuleConflictException("Cannot reschedule collection task. Current status is 'InProgress', but only 'Scheduled' tasks can be rescheduled."));

        var request = CreateAuthorizedRequest(HttpMethod.Post, $"/api/v1/collection-tasks/{taskId}/reschedule", officerToken, new RescheduleCollectionTaskRequest
        {
            NewScheduledAt = DateTime.UtcNow.AddHours(4),
            Reason = "Rescheduling already in-progress task"
        });
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(SharedTestJsonOptions);
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(StatusCodes.Status409Conflict);
        problem.Detail.Should().Contain("only 'Scheduled' tasks can be rescheduled");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 5. GET /api/v1/collection-tasks/{id:guid}/history (GetTaskHistory)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTaskHistory_WasteOfficerToken_Returns200OKWithAuditTrail()
    {
        var (client, mock) = CreateClientWithMockService();
        var officerToken = await GetOfficerTokenAsync();
        var taskId = Guid.NewGuid();

        var expectedAudit = new CollectionTaskAuditTrailDto
        {
            CollectionTaskId = taskId,
            TaskCode = "TSK-20260921-0012",
            StatusHistory = new List<CollectionTaskStatusHistoryDto>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    FromStatus = null,
                    ToStatus = "Scheduled",
                    ChangedByUserId = Guid.NewGuid(),
                    ChangedByUserName = "Officer Silva",
                    Notes = "Task manually created",
                    ChangedAt = DateTime.UtcNow.AddHours(-4)
                }
            },
            ScheduleHistory = new List<CollectionTaskScheduleHistoryDto>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    PreviousScheduledAt = DateTime.UtcNow.AddHours(2),
                    NewScheduledAt = DateTime.UtcNow.AddHours(6),
                    Reason = "Depot vehicle maintenance",
                    RescheduledByUserId = Guid.NewGuid(),
                    RescheduledByUserName = "Officer Silva",
                    RescheduledAt = DateTime.UtcNow.AddHours(-2)
                }
            }
        };

        mock.Setup(s => s.GetTaskAuditTrailAsync(taskId, It.IsAny<Guid>(), AppRoles.WasteOfficer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedAudit);

        var request = CreateAuthorizedRequest(HttpMethod.Get, $"/api/v1/collection-tasks/{taskId}/history", officerToken);
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<CollectionTaskAuditTrailDto>(SharedTestJsonOptions);
        result.Should().NotBeNull();
        result!.CollectionTaskId.Should().Be(taskId);
        result.StatusHistory.Should().HaveCount(1);
        result.ScheduleHistory.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetTaskHistory_MunicipalManagerToken_Returns200OK()
    {
        var (client, mock) = CreateClientWithMockService();
        var managerToken = await GetManagerTokenAsync();
        var taskId = Guid.NewGuid();

        var expectedAudit = new CollectionTaskAuditTrailDto
        {
            CollectionTaskId = taskId,
            TaskCode = "TSK-20260921-0012",
            StatusHistory = new List<CollectionTaskStatusHistoryDto>(),
            ScheduleHistory = new List<CollectionTaskScheduleHistoryDto>()
        };

        mock.Setup(s => s.GetTaskAuditTrailAsync(taskId, It.IsAny<Guid>(), AppRoles.MunicipalManager, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedAudit);

        var request = CreateAuthorizedRequest(HttpMethod.Get, $"/api/v1/collection-tasks/{taskId}/history", managerToken);
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<CollectionTaskAuditTrailDto>(SharedTestJsonOptions);
        result.Should().NotBeNull();
        result!.CollectionTaskId.Should().Be(taskId);
    }

    [Fact]
    public async Task GetTaskHistory_NotFound_Returns404NotFound()
    {
        var (client, mock) = CreateClientWithMockService();
        var officerToken = await GetOfficerTokenAsync();
        var nonExistentId = Guid.NewGuid();

        mock.Setup(s => s.GetTaskAuditTrailAsync(nonExistentId, It.IsAny<Guid>(), AppRoles.WasteOfficer, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException($"Collection task '{nonExistentId}' was not found."));

        var request = CreateAuthorizedRequest(HttpMethod.Get, $"/api/v1/collection-tasks/{nonExistentId}/history", officerToken);
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetTaskHistory_CitizenToken_Returns403Forbidden()
    {
        var (client, mock) = CreateClientWithMockService();
        var citizenToken = await GetCitizenTokenAsync();
        var taskId = Guid.NewGuid();

        var request = CreateAuthorizedRequest(HttpMethod.Get, $"/api/v1/collection-tasks/{taskId}/history", citizenToken);
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        mock.Verify(s => s.GetTaskAuditTrailAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 6. ROUTE INTEGRITY VERIFICATION
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExistingRoutes_WasteBinsAndCollectionNeeds_RemainIntact()
    {
        var (client, _) = CreateClientWithMockService();
        var citizenToken = await GetCitizenTokenAsync();

        // 1. Citizen public bins discovery route still responds (not 404)
        var binRequest = CreateAuthorizedRequest(HttpMethod.Get, "/api/v1/bins/public", citizenToken);
        var binResponse = await client.SendAsync(binRequest);
        binResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 2. Collection needs route for citizen returns 403 Forbidden (not 404)
        var needRequest = CreateAuthorizedRequest(HttpMethod.Get, "/api/v1/collection-needs", citizenToken);
        var needResponse = await client.SendAsync(needRequest);
        needResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
