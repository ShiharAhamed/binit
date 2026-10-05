using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SmartWaste.Application.Interfaces;
using SmartWaste.Application.Workflow.DTOs.Requests;
using SmartWaste.Application.Workflow.DTOs.Responses;
using SmartWaste.Domain.Collection.Entities;
using SmartWaste.Domain.Collection.Enums;
using SmartWaste.Domain.Common;
using SmartWaste.Domain.Entities;
using SmartWaste.Domain.Reporting.Entities;
using SmartWaste.Domain.Reporting.Enums;
using SmartWaste.Domain.Workflow.Entities;
using SmartWaste.Domain.Workflow.Enums;
using SmartWaste.Infrastructure.Persistence;
using Xunit;

namespace SmartWaste.Tests.Workflow.Controllers;

/// <summary>
/// End-to-end HTTP integration tests for Step 7 Authoritative C2 CollectionTask Execution Bridge:
/// Routing, JWT authentication, role authorization (MunicipalManager only),
/// concurrency token (expectedVersion), and state transitions to FleetPlanning.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class AgentWorkflowExecutionsApiIntegrationTests
{
    private readonly CustomWebApplicationFactory _factory;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false) }
    };

    public AgentWorkflowExecutionsApiIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private (HttpClient client, AppDbContext db, IAuthService authService, IServiceScope scope) CreateTestClient()
    {
        var dbName = $"SmartWaste_ExecutionsApiAuth_{Guid.NewGuid():N}";
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.RemoveAll<AppDbContext>();
                services.AddDbContext<AppDbContext>(options =>
                {
                    options.UseInMemoryDatabase(dbName);
                    options.ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));
                });
            });
        });

        var client = factory.CreateClient();
        var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        return (client, db, authService, scope);
    }

    private static (AppUser user, string token) CreateUserAndToken(AppDbContext db, IAuthService authService, string role)
    {
        var userId = Guid.NewGuid();
        var user = new AppUser
        {
            Id = userId,
            UserName = $"user_{userId:N}",
            NormalizedUserName = $"USER_{userId:N}",
            Email = $"{role.ToLowerInvariant()}_{userId:N}@smartwaste.test",
            NormalizedEmail = $"{role.ToUpperInvariant()}_{userId:N}@SMARTWASTE.TEST",
            FullName = $"{role} User",
            PhoneNumber = "+94770000000",
            EmailConfirmed = true,
            MustChangePassword = false,
            IsActive = true,
            SecurityStamp = Guid.NewGuid().ToString("N")
        };
        db.Users.Add(user);
        db.SaveChanges();

        var token = authService.GenerateJwtToken(user, new[] { role }, out _);
        return (user, token);
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string url, string? token, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (body != null)
        {
            request.Content = JsonContent.Create(body, options: JsonOptions);
        }

        return request;
    }

    private static (AgentWorkflow workflow, AgentWorkflowStep step, Guid reportId) SeedApprovedWorkflowWithReport(
        AppDbContext db,
        Guid initiatorId,
        int version = 3)
    {
        var reportId = Guid.NewGuid();
        db.WasteReports.Add(new WasteReport
        {
            Id = reportId,
            CitizenId = Guid.NewGuid(),
            Description = "Accumulated organic waste near school",
            AddressText = "Temple Road, Nugegoda",
            Latitude = 6.8722,
            Longitude = 79.8883,
            Status = WasteReportStatus.Verified,
            WasteType = WasteType.General,
            CreatedAt = DateTime.UtcNow.AddHours(-8),
            VerifiedAt = DateTime.UtcNow.AddHours(-1)
        });

        var zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Colombo");
        var localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone)).AddDays(2);
        var futureTime = TimeZoneInfo.ConvertTimeToUtc(localDate.ToDateTime(new TimeOnly(9, 0)), zone)
            .ToString("yyyy-MM-ddTHH:mm:ssZ");
        var c2Output = JsonSerializer.Serialize(new
        {
            status = "completed",
            isCompleteSnapshot = true,
            candidateGroups = new[]
            {
                new
                {
                    groupId = "group-1",
                    attentionOrder = 1,
                    proposedSchedule = new
                    {
                        scheduledAt = futureTime,
                        schedulingReason = "Urgent collection approved."
                    },
                    needReferences = new[]
                    {
                        new { needId = reportId, targetType = "Report", collectionReason = "VerifiedReport" }
                    }
                }
            },
            separateHandling = Array.Empty<object>(),
            deferredNeeds = Array.Empty<object>()
        });

        var workflow = new AgentWorkflow
        {
            Id = Guid.NewGuid(),
            Objective = "Execution bridge test workflow",
            Status = AgentWorkflowStatus.CollectionApproved,
            CurrentStep = WorkflowStepType.CollectionPlanning,
            InitiatedByUserId = initiatorId,
            Version = version,
            CreatedAt = DateTime.UtcNow.AddHours(-4)
        };

        var step = new AgentWorkflowStep
        {
            Id = Guid.NewGuid(),
            WorkflowId = workflow.Id,
            Sequence = 1,
            StepType = WorkflowStepType.CollectionPlanning,
            AgentName = "collection_planning_agent",
            Status = WorkflowStepStatus.Completed,
            OutputJson = c2Output,
            CompletedAt = DateTime.UtcNow.AddHours(-2)
        };

        var approval = new AgentWorkflowApproval
        {
            Id = Guid.NewGuid(),
            WorkflowId = workflow.Id,
            WorkflowStepId = step.Id,
            ApprovalStage = WorkflowApprovalStage.CollectionPlanning,
            Decision = WorkflowApprovalDecision.Approved,
            DecisionReason = "Plan verified and approved",
            DecidedByUserId = initiatorId,
            DecidedAt = DateTime.UtcNow.AddHours(-2)
        };

        db.AgentWorkflows.Add(workflow);
        db.AgentWorkflowSteps.Add(step);
        db.AgentWorkflowApprovals.Add(approval);
        db.SaveChanges();

        return (workflow, step, reportId);
    }

    [Fact]
    public async Task ExecuteCollectionPlan_MunicipalManager_ValidApprovedPlan_Returns200WithFleetPlanning()
    {
        var (client, db, authService, scope) = CreateTestClient();
        using var _ = scope;

        var (managerUser, managerToken) = CreateUserAndToken(db, authService, AppRoles.MunicipalManager);
        var (workflow, step, reportId) = SeedApprovedWorkflowWithReport(db, managerUser.Id, version: 3);

        var reqBody = new ExecuteCollectionPlanRequest { ExpectedVersion = 3 };
        var req = CreateRequest(HttpMethod.Post, $"/api/v1/agent-workflows/{workflow.Id}/execute-collection-plan", managerToken, reqBody);
        var res = await client.SendAsync(req);

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await res.Content.ReadFromJsonAsync<AgentWorkflowDetailDto>(JsonOptions);
        detail.Should().NotBeNull();
        detail!.Status.Should().Be(AgentWorkflowStatus.FleetPlanning);
        detail.CurrentStep.Should().Be(WorkflowStepType.FleetPlanning);
        detail.Version.Should().Be(5); // 3 -> 4 (CreatingScheduledTasks) -> 5 (FleetPlanning)

        // Step and execution result
        detail.Steps.Should().Contain(s => s.StepType == WorkflowStepType.ScheduledTaskCreation && s.Status == WorkflowStepStatus.Completed);
        detail.ExecutionResults.Should().Contain(e => e.ExecutionType == WorkflowExecutionType.CollectionTaskCreation && e.Status == WorkflowExecutionStatus.Succeeded);

        // Verify task in DB
        var tasks = await db.CollectionTasks.ToListAsync();
        tasks.Should().ContainSingle();
        tasks[0].WasteReportId.Should().Be(reportId);
        tasks[0].Status.Should().Be(CollectionTaskStatus.Scheduled);
        tasks[0].CreationMethod.Should().Be(TaskCreationMethod.ApprovedAiPlan);
    }

    [Fact]
    public async Task ExecuteCollectionPlan_WasteOfficer_ValidApprovedPlan_Returns200WithFleetPlanning()
    {
        var (client, db, authService, scope) = CreateTestClient();
        using var _ = scope;

        var (officerUser, officerToken) = CreateUserAndToken(db, authService, AppRoles.WasteOfficer);
        var (workflow, step, reportId) = SeedApprovedWorkflowWithReport(db, officerUser.Id, version: 3);

        var reqBody = new ExecuteCollectionPlanRequest { ExpectedVersion = 3 };
        var req = CreateRequest(HttpMethod.Post, $"/api/v1/agent-workflows/{workflow.Id}/execute-collection-plan", officerToken, reqBody);
        var res = await client.SendAsync(req);

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await res.Content.ReadFromJsonAsync<AgentWorkflowDetailDto>(JsonOptions);
        detail.Should().NotBeNull();
        detail!.Status.Should().Be(AgentWorkflowStatus.FleetPlanning);
        detail.CurrentStep.Should().Be(WorkflowStepType.FleetPlanning);
        detail.Version.Should().Be(5);

        detail.Steps.Should().Contain(s => s.StepType == WorkflowStepType.ScheduledTaskCreation && s.Status == WorkflowStepStatus.Completed);
        detail.ExecutionResults.Should().Contain(e => e.ExecutionType == WorkflowExecutionType.CollectionTaskCreation && e.Status == WorkflowExecutionStatus.Succeeded);

        var tasks = await db.CollectionTasks.ToListAsync();
        tasks.Should().ContainSingle();
        tasks[0].WasteReportId.Should().Be(reportId);
        tasks[0].Status.Should().Be(CollectionTaskStatus.Scheduled);
        tasks[0].CreationMethod.Should().Be(TaskCreationMethod.ApprovedAiPlan);
    }

    [Fact]
    public async Task ExecuteCollectionPlan_ManagerApproved_WasteOfficerExecutes_Returns200()
    {
        var (client, db, authService, scope) = CreateTestClient();
        using var _ = scope;

        var (managerUser, _) = CreateUserAndToken(db, authService, AppRoles.MunicipalManager);
        var (officerUser, officerToken) = CreateUserAndToken(db, authService, AppRoles.WasteOfficer);
        var (workflow, step, reportId) = SeedApprovedWorkflowWithReport(db, managerUser.Id, version: 3);

        var reqBody = new ExecuteCollectionPlanRequest { ExpectedVersion = 3 };
        var req = CreateRequest(HttpMethod.Post, $"/api/v1/agent-workflows/{workflow.Id}/execute-collection-plan", officerToken, reqBody);
        var res = await client.SendAsync(req);

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await res.Content.ReadFromJsonAsync<AgentWorkflowDetailDto>(JsonOptions);
        detail.Should().NotBeNull();
        detail!.Status.Should().Be(AgentWorkflowStatus.FleetPlanning);
    }

    [Theory]
    [InlineData(AppRoles.Citizen)]
    [InlineData(AppRoles.Driver)]
    public async Task ExecuteCollectionPlan_UnauthorizedRoles_Returns403Forbidden(string role)
    {
        var (client, db, authService, scope) = CreateTestClient();
        using var _ = scope;

        var (_, token) = CreateUserAndToken(db, authService, role);
        var workflowId = Guid.NewGuid();

        var reqBody = new ExecuteCollectionPlanRequest { ExpectedVersion = 1 };
        var req = CreateRequest(HttpMethod.Post, $"/api/v1/agent-workflows/{workflowId}/execute-collection-plan", token, reqBody);
        var res = await client.SendAsync(req);

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ExecuteCollectionPlan_Unauthenticated_Returns401Unauthorized()
    {
        var (client, _, _, scope) = CreateTestClient();
        using var _ = scope;

        var req = CreateRequest(HttpMethod.Post, $"/api/v1/agent-workflows/{Guid.NewGuid()}/execute-collection-plan", token: null, new { expectedVersion = 1 });
        var res = await client.SendAsync(req);

        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ExecuteCollectionPlan_WrongWorkflowState_Returns409Conflict()
    {
        var (client, db, authService, scope) = CreateTestClient();
        using var _ = scope;

        var (managerUser, managerToken) = CreateUserAndToken(db, authService, AppRoles.MunicipalManager);
        var (workflow, _, _) = SeedApprovedWorkflowWithReport(db, managerUser.Id, version: 1);

        // Change workflow to AwaitingCollectionApproval
        workflow.Status = AgentWorkflowStatus.AwaitingCollectionApproval;
        await db.SaveChangesAsync();

        var reqBody = new ExecuteCollectionPlanRequest { ExpectedVersion = 1 };
        var req = CreateRequest(HttpMethod.Post, $"/api/v1/agent-workflows/{workflow.Id}/execute-collection-plan", managerToken, reqBody);
        var res = await client.SendAsync(req);

        res.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ExecuteCollectionPlan_StaleExpectedVersion_Returns409Conflict()
    {
        var (client, db, authService, scope) = CreateTestClient();
        using var _ = scope;

        var (managerUser, managerToken) = CreateUserAndToken(db, authService, AppRoles.MunicipalManager);
        var (workflow, _, _) = SeedApprovedWorkflowWithReport(db, managerUser.Id, version: 5);

        var reqBody = new ExecuteCollectionPlanRequest { ExpectedVersion = 4 }; // Stale!
        var req = CreateRequest(HttpMethod.Post, $"/api/v1/agent-workflows/{workflow.Id}/execute-collection-plan", managerToken, reqBody);
        var res = await client.SendAsync(req);

        res.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ExecuteCollectionPlan_WorkflowNotFound_Returns404NotFound()
    {
        var (client, db, authService, scope) = CreateTestClient();
        using var _ = scope;

        var (_, managerToken) = CreateUserAndToken(db, authService, AppRoles.MunicipalManager);

        var reqBody = new ExecuteCollectionPlanRequest { ExpectedVersion = 1 };
        var req = CreateRequest(HttpMethod.Post, $"/api/v1/agent-workflows/{Guid.NewGuid()}/execute-collection-plan", managerToken, reqBody);
        var res = await client.SendAsync(req);

        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ExecuteCollectionPlan_Result_VisibleInGetWorkflowDetails()
    {
        var (client, db, authService, scope) = CreateTestClient();
        using var _ = scope;

        var (managerUser, managerToken) = CreateUserAndToken(db, authService, AppRoles.MunicipalManager);
        var (workflow, _, _) = SeedApprovedWorkflowWithReport(db, managerUser.Id, version: 3);

        // Execute plan
        var execReq = CreateRequest(HttpMethod.Post, $"/api/v1/agent-workflows/{workflow.Id}/execute-collection-plan", managerToken, new { expectedVersion = 3 });
        var execRes = await client.SendAsync(execReq);
        execRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // Query GET workflow details
        var getReq = CreateRequest(HttpMethod.Get, $"/api/v1/agent-workflows/{workflow.Id}", managerToken);
        var getRes = await client.SendAsync(getReq);
        getRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await getRes.Content.ReadFromJsonAsync<AgentWorkflowDetailDto>(JsonOptions);
        detail.Should().NotBeNull();
        detail!.Status.Should().Be(AgentWorkflowStatus.FleetPlanning);
        detail.CurrentStep.Should().Be(WorkflowStepType.FleetPlanning);
        detail.Steps.Should().Contain(s => s.StepType == WorkflowStepType.ScheduledTaskCreation);
        detail.ExecutionResults.Should().Contain(e => e.ExecutionType == WorkflowExecutionType.CollectionTaskCreation && e.Status == WorkflowExecutionStatus.Succeeded);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Step 8: POST /api/v1/agent-workflows/{id}/execute-dispatch-plan HTTP Tests
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteDispatchPlan_Unauthenticated_Returns401Unauthorized()
    {
        var (client, _, _, scope) = CreateTestClient();
        using var _ = scope;

        var req = CreateRequest(HttpMethod.Post, $"/api/v1/agent-workflows/{Guid.NewGuid()}/execute-dispatch-plan", token: null, new { expectedVersion = 1 });
        var res = await client.SendAsync(req);

        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(AppRoles.Citizen)]
    [InlineData(AppRoles.Driver)]
    public async Task ExecuteDispatchPlan_UnauthorizedRoles_Returns403Forbidden(string role)
    {
        var (client, db, authService, scope) = CreateTestClient();
        using var _ = scope;

        var (_, token) = CreateUserAndToken(db, authService, role);
        var workflowId = Guid.NewGuid();

        var reqBody = new ExecuteDispatchPlanRequest { ExpectedVersion = 1 };
        var req = CreateRequest(HttpMethod.Post, $"/api/v1/agent-workflows/{workflowId}/execute-dispatch-plan", token, reqBody);
        var res = await client.SendAsync(req);

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ExecuteDispatchPlan_WorkflowNotFound_Returns404NotFound()
    {
        var (client, db, authService, scope) = CreateTestClient();
        using var _ = scope;

        var (_, managerToken) = CreateUserAndToken(db, authService, AppRoles.MunicipalManager);

        var reqBody = new ExecuteDispatchPlanRequest { ExpectedVersion = 1 };
        var req = CreateRequest(HttpMethod.Post, $"/api/v1/agent-workflows/{Guid.NewGuid()}/execute-dispatch-plan", managerToken, reqBody);
        var res = await client.SendAsync(req);

        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ExecuteDispatchPlan_MunicipalManager_ValidApprovedPlan_Returns200WithCompletedAndLostResponseIdempotency()
    {
        var (client, db, authService, scope) = CreateTestClient();
        using var _ = scope;

        var (managerUser, managerToken) = CreateUserAndToken(db, authService, AppRoles.MunicipalManager);
        var (workflow, driverId, vehicleId, taskId) = SeedApprovedDispatchWorkflow(db, managerUser.Id, version: 3);

        // 1. Initial execution call: expectedVersion = 3
        var reqBody = new ExecuteDispatchPlanRequest { ExpectedVersion = 3 };
        var req = CreateRequest(HttpMethod.Post, $"/api/v1/agent-workflows/{workflow.Id}/execute-dispatch-plan", managerToken, reqBody);
        var res = await client.SendAsync(req);

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await res.Content.ReadFromJsonAsync<AgentWorkflowDetailDto>(JsonOptions);
        detail.Should().NotBeNull();
        detail!.Status.Should().Be(AgentWorkflowStatus.Completed);
        detail.CurrentStep.Should().Be(WorkflowStepType.AssignmentExecution);
        detail.Version.Should().Be(5); // 3 -> 4 (ExecutingAssignments) -> 5 (Completed)

        // Verify assignment in DB
        var assignments = await db.CollectionAssignments.Include(a => a.Route).ThenInclude(r => r!.Stops).ToListAsync();
        assignments.Should().ContainSingle();
        assignments[0].DriverId.Should().Be(driverId);
        assignments[0].VehicleId.Should().Be(vehicleId);
        assignments[0].Route.Should().NotBeNull();
        assignments[0].Route!.Stops.Should().ContainSingle();
        assignments[0].Route!.Stops.First().CollectionTaskId.Should().Be(taskId);

        // 2. Lost-response retry: client retries with the ORIGINAL pre-execution expectedVersion = 3
        var retryReq = CreateRequest(HttpMethod.Post, $"/api/v1/agent-workflows/{workflow.Id}/execute-dispatch-plan", managerToken, reqBody);
        var retryRes = await client.SendAsync(retryReq);

        retryRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var retryDetail = await retryRes.Content.ReadFromJsonAsync<AgentWorkflowDetailDto>(JsonOptions);
        retryDetail.Should().NotBeNull();
        retryDetail!.Status.Should().Be(AgentWorkflowStatus.Completed);
        retryDetail.Version.Should().Be(5);

        // DB still has exactly 1 assignment (no duplication)
        var assignmentsAfterRetry = await db.CollectionAssignments.ToListAsync();
        assignmentsAfterRetry.Should().HaveCount(1);
    }

    [Fact]
    public async Task ExecuteDispatchPlan_WasteOfficer_ValidApprovedPlan_Returns200WithCompletedAndLostResponseIdempotency()
    {
        var (client, db, authService, scope) = CreateTestClient();
        using var _ = scope;

        var (officerUser, officerToken) = CreateUserAndToken(db, authService, AppRoles.WasteOfficer);
        var (workflow, driverId, vehicleId, taskId) = SeedApprovedDispatchWorkflow(db, officerUser.Id, version: 3);

        // 1. Initial execution call by WasteOfficer: expectedVersion = 3
        var reqBody = new ExecuteDispatchPlanRequest { ExpectedVersion = 3 };
        var req = CreateRequest(HttpMethod.Post, $"/api/v1/agent-workflows/{workflow.Id}/execute-dispatch-plan", officerToken, reqBody);
        var res = await client.SendAsync(req);

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await res.Content.ReadFromJsonAsync<AgentWorkflowDetailDto>(JsonOptions);
        detail.Should().NotBeNull();
        detail!.Status.Should().Be(AgentWorkflowStatus.Completed);
        detail.CurrentStep.Should().Be(WorkflowStepType.AssignmentExecution);
        detail.Version.Should().Be(5);

        // Verify assignment in DB
        var assignments = await db.CollectionAssignments.Include(a => a.Route).ThenInclude(r => r!.Stops).ToListAsync();
        assignments.Should().ContainSingle();
        assignments[0].DriverId.Should().Be(driverId);
        assignments[0].VehicleId.Should().Be(vehicleId);

        // 2. Lost-response retry: WasteOfficer retries with original pre-execution expectedVersion = 3
        var retryReq = CreateRequest(HttpMethod.Post, $"/api/v1/agent-workflows/{workflow.Id}/execute-dispatch-plan", officerToken, reqBody);
        var retryRes = await client.SendAsync(retryReq);

        retryRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var retryDetail = await retryRes.Content.ReadFromJsonAsync<AgentWorkflowDetailDto>(JsonOptions);
        retryDetail.Should().NotBeNull();
        retryDetail!.Status.Should().Be(AgentWorkflowStatus.Completed);
        retryDetail.Version.Should().Be(5);

        var assignmentsAfterRetry = await db.CollectionAssignments.ToListAsync();
        assignmentsAfterRetry.Should().HaveCount(1);
    }

    [Fact]
    public async Task ExecuteDispatchPlan_WasteOfficerApproved_ManagerExecutes_Returns200()
    {
        var (client, db, authService, scope) = CreateTestClient();
        using var _ = scope;

        var (officerUser, _) = CreateUserAndToken(db, authService, AppRoles.WasteOfficer);
        var (managerUser, managerToken) = CreateUserAndToken(db, authService, AppRoles.MunicipalManager);
        var (workflow, driverId, vehicleId, taskId) = SeedApprovedDispatchWorkflow(db, officerUser.Id, version: 3);

        // Manager executes workflow approved by WasteOfficer
        var reqBody = new ExecuteDispatchPlanRequest { ExpectedVersion = 3 };
        var req = CreateRequest(HttpMethod.Post, $"/api/v1/agent-workflows/{workflow.Id}/execute-dispatch-plan", managerToken, reqBody);
        var res = await client.SendAsync(req);

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await res.Content.ReadFromJsonAsync<AgentWorkflowDetailDto>(JsonOptions);
        detail.Should().NotBeNull();
        detail!.Status.Should().Be(AgentWorkflowStatus.Completed);
    }

    [Fact]
    public async Task ExecuteDispatchPlan_Result_VisibleInGetWorkflowDetails()
    {
        var (client, db, authService, scope) = CreateTestClient();
        using var _ = scope;

        var (managerUser, managerToken) = CreateUserAndToken(db, authService, AppRoles.MunicipalManager);
        var (workflow, _, _, _) = SeedApprovedDispatchWorkflow(db, managerUser.Id, version: 3);

        // Execute dispatch plan
        var execReq = CreateRequest(HttpMethod.Post, $"/api/v1/agent-workflows/{workflow.Id}/execute-dispatch-plan", managerToken, new { expectedVersion = 3 });
        var execRes = await client.SendAsync(execReq);
        execRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // Query GET workflow details
        var getReq = CreateRequest(HttpMethod.Get, $"/api/v1/agent-workflows/{workflow.Id}", managerToken);
        var getRes = await client.SendAsync(getReq);
        getRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await getRes.Content.ReadFromJsonAsync<AgentWorkflowDetailDto>(JsonOptions);
        detail.Should().NotBeNull();
        detail!.Status.Should().Be(AgentWorkflowStatus.Completed);
        detail.CurrentStep.Should().Be(WorkflowStepType.AssignmentExecution);
        detail.Steps.Should().Contain(s => s.StepType == WorkflowStepType.AssignmentExecution && s.Status == WorkflowStepStatus.Completed);
        detail.ExecutionResults.Should().Contain(e => e.ExecutionType == WorkflowExecutionType.CollectionAssignment && e.Status == WorkflowExecutionStatus.Succeeded);
    }

    private static (AgentWorkflow workflow, Guid driverId, Guid vehicleId, Guid taskId) SeedApprovedDispatchWorkflow(
        AppDbContext db,
        Guid managerId,
        int version = 3)
    {
        var driverUser = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = $"driver_{Guid.NewGuid():N}@smartwaste.lk",
            Email = $"driver_{Guid.NewGuid():N}@smartwaste.lk",
            FullName = "Assigned Driver",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };
        db.Users.Add(driverUser);

        var driverRole = db.Roles.FirstOrDefault(r => r.Name == AppRoles.Driver);
        if (driverRole == null)
        {
            driverRole = new IdentityRole<Guid>
            {
                Id = Guid.NewGuid(),
                Name = AppRoles.Driver,
                NormalizedName = AppRoles.Driver.ToUpperInvariant(),
                ConcurrencyStamp = Guid.NewGuid().ToString("N")
            };
            db.Roles.Add(driverRole);
        }
        db.UserRoles.Add(new IdentityUserRole<Guid>
        {
            UserId = driverUser.Id,
            RoleId = driverRole.Id
        });

        db.DriverProfiles.Add(new DriverProfile
        {
            UserId = driverUser.Id,
            User = driverUser,
            AvailabilityStatus = DriverAvailabilityStatus.Available,
            LicenseNumber = "DL99999",
            IsEligible = true
        });

        var vehicleId = Guid.NewGuid();
        var vehicle = new Vehicle
        {
            Id = vehicleId,
            RegistrationNumber = $"WP-CAB-{Random.Shared.Next(1000, 9999)}",
            VehicleType = VehicleType.Compactor,
            CapacityLiters = 5000,
            OperationalStatus = VehicleOperationalStatus.Available
        };
        vehicle.SupportedWasteTypes.Add(new VehicleSupportedWasteType { VehicleId = vehicleId, WasteType = WasteType.General });
        db.Vehicles.Add(vehicle);

        var reportId = Guid.NewGuid();
        db.WasteReports.Add(new WasteReport
        {
            Id = reportId,
            CitizenId = Guid.NewGuid(),
            Description = "Commercial waste collection",
            AddressText = "Galle Road, Colombo",
            Latitude = 6.9147,
            Longitude = 79.8778,
            Status = WasteReportStatus.Scheduled,
            WasteType = WasteType.General,
            CreatedAt = DateTime.UtcNow.AddHours(-10),
            VerifiedAt = DateTime.UtcNow.AddHours(-2)
        });

        var taskId = Guid.NewGuid();
        db.CollectionTasks.Add(new CollectionTask
        {
            Id = taskId,
            TaskCode = $"TSK-{Guid.NewGuid():N}"[..8].ToUpperInvariant(),
            Status = CollectionTaskStatus.Scheduled,
            WasteReportId = reportId,
            ScheduledAt = DateTime.UtcNow.AddHours(3),
            CreatedAt = DateTime.UtcNow.AddHours(-1)
        });

        var c3Json = JsonSerializer.Serialize(new
        {
            objective = "Dispatch fleet",
            dispatchPlans = new[]
            {
                new
                {
                    planId = "plan-1",
                    recommendedDriver = new { driverId = driverUser.Id, displayName = driverUser.FullName },
                    recommendedVehicle = new { vehicleId = vehicleId, registrationNumber = vehicle.RegistrationNumber, vehicleType = "Compactor" },
                    recommendedTasks = new[]
                    {
                        new { taskId = taskId, taskCode = "TSK-001", sequence = 1, addressText = "Galle Road, Colombo", reason = "First stop" }
                    },
                    compatibility = new { status = "Compatible", requiresAcknowledgement = false, issues = Array.Empty<string>() },
                    rationale = "Candidate dispatch plan",
                    warnings = Array.Empty<string>()
                }
            },
            unplannedTasks = Array.Empty<object>(),
            warnings = Array.Empty<string>(),
            rationale = "Formulated plan",
            status = "completed"
        });

        var c4OutputJson = JsonSerializer.Serialize(new
        {
            objective = "Validate dispatch proposal",
            validationOutcome = "ReadyForHumanReview",
            requiresAcknowledgement = false,
            warnings = Array.Empty<string>(),
            status = "completed"
        });

        var workflowId = Guid.NewGuid();
        var c4StepId = Guid.NewGuid();

        var workflow = new AgentWorkflow
        {
            Id = workflowId,
            Objective = "Fleet dispatch execution test workflow",
            Status = AgentWorkflowStatus.DispatchApproved,
            CurrentStep = WorkflowStepType.OperationalValidation,
            InitiatedByUserId = managerId,
            Version = version,
            CreatedAt = DateTime.UtcNow.AddHours(-4),
            UpdatedAt = DateTime.UtcNow.AddHours(-1)
        };

        var c4Step = new AgentWorkflowStep
        {
            Id = c4StepId,
            WorkflowId = workflowId,
            Sequence = 4,
            StepType = WorkflowStepType.OperationalValidation,
            AgentName = "c4_validation_operations_agent",
            Status = WorkflowStepStatus.Completed,
            InputJson = c3Json,
            OutputJson = c4OutputJson,
            StartedAt = DateTime.UtcNow.AddMinutes(-30),
            CompletedAt = DateTime.UtcNow.AddMinutes(-10)
        };

        var approval = new AgentWorkflowApproval
        {
            Id = Guid.NewGuid(),
            WorkflowId = workflowId,
            WorkflowStepId = c4StepId,
            ApprovalStage = WorkflowApprovalStage.FleetDispatch,
            Decision = WorkflowApprovalDecision.Approved,
            DecisionReason = "Dispatch verified and approved.",
            DecisionPayloadJson = JsonSerializer.Serialize(new { acknowledgeWarnings = false }),
            DecidedByUserId = managerId,
            DecidedAt = DateTime.UtcNow.AddMinutes(-5)
        };

        workflow.Steps.Add(c4Step);
        workflow.Approvals.Add(approval);
        db.AgentWorkflows.Add(workflow);
        db.SaveChanges();

        return (workflow, driverUser.Id, vehicleId, taskId);
    }
}
