using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SmartWaste.Application.Interfaces;
using SmartWaste.Application.Workflow.DTOs.Requests;
using SmartWaste.Application.Workflow.DTOs.Responses;
using SmartWaste.Domain.Common;
using SmartWaste.Domain.Entities;
using SmartWaste.Domain.Workflow.Entities;
using SmartWaste.Domain.Workflow.Enums;
using SmartWaste.Infrastructure.Persistence;
using Xunit;

namespace SmartWaste.Tests.Workflow.Controllers;

/// <summary>
/// End-to-end HTTP integration tests for Step 6 Human Approval API:
/// Routing, JWT authentication, role authorization (MunicipalManager only),
/// concurrency tokens (expectedVersion), deterministic prerequisite validation,
/// and atomic state transitions for Gate 1 (Collection) and Gate 2 (Dispatch).
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class AgentWorkflowApprovalsApiIntegrationTests
{
    private readonly CustomWebApplicationFactory _factory;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false) }
    };

    public AgentWorkflowApprovalsApiIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private (HttpClient client, AppDbContext db, IAuthService authService, IServiceScope scope) CreateTestClient()
    {
        var dbName = $"SmartWaste_ApprovalsApiAuth_{Guid.NewGuid():N}";
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

    private static (AgentWorkflow workflow, AgentWorkflowStep step) SeedWorkflowWithStep(
        AppDbContext db,
        Guid initiatorId,
        AgentWorkflowStatus status,
        WorkflowStepType stepType,
        string outputJson,
        int version = 1)
    {
        var workflow = new AgentWorkflow
        {
            Id = Guid.NewGuid(),
            Objective = "Human approval test workflow",
            Status = status,
            CurrentStep = stepType,
            InitiatedByUserId = initiatorId,
            Version = version,
            CreatedAt = DateTime.UtcNow
        };

        var step = new AgentWorkflowStep
        {
            Id = Guid.NewGuid(),
            WorkflowId = workflow.Id,
            Sequence = 1,
            StepType = stepType,
            Status = WorkflowStepStatus.Completed,
            OutputJson = outputJson,
            CompletedAt = DateTime.UtcNow
        };

        db.AgentWorkflows.Add(workflow);
        db.AgentWorkflowSteps.Add(step);
        db.SaveChanges();

        return (workflow, step);
    }

    // =========================================================================
    // Gate 1: Collection Approval HTTP Tests
    // =========================================================================

    [Fact]
    public async Task ApproveCollection_OutOfHoursC2Plan_Returns409WithoutMutatingWorkflow()
    {
        var (client, db, authService, scope) = CreateTestClient();
        using var _ = scope;
        var (manager, token) = CreateUserAndToken(db, authService, AppRoles.MunicipalManager);
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Colombo");
        var localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone)).AddDays(2);
        var overnightUtc = TimeZoneInfo.ConvertTimeToUtc(
            localDate.ToDateTime(new TimeOnly(2, 30)), zone);
        var c2Output = JsonSerializer.Serialize(new
        {
            status = "completed", isCompleteSnapshot = true,
            candidateGroups = new[] { new
            {
                groupId = "group-1",
                proposedSchedule = new { scheduledAt = overnightUtc.ToString("O"), schedulingReason = "Early collection." },
                needReferences = new[] { new { needId = Guid.NewGuid(), targetType = "Report", collectionReason = "VerifiedReport" } }
            } },
            separateHandling = Array.Empty<object>(), deferredNeeds = Array.Empty<object>()
        });
        var (workflow, _) = SeedWorkflowWithStep(db, manager.Id,
            AgentWorkflowStatus.AwaitingCollectionApproval, WorkflowStepType.CollectionPlanning, c2Output);

        var request = CreateRequest(HttpMethod.Post,
            $"/api/v1/agent-workflows/{workflow.Id}/collection-approval/approve", token,
            new ApproveCollectionPlanningRequest { ExpectedVersion = 1 });
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Detail.Should().Contain("municipality timezone");
        (await db.AgentWorkflows.Include(w => w.Approvals).SingleAsync(w => w.Id == workflow.Id))
            .Status.Should().Be(AgentWorkflowStatus.AwaitingCollectionApproval);
        (await db.AgentWorkflowApprovals.CountAsync()).Should().Be(0);
        (await db.CollectionTasks.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ApproveCollection_MunicipalManager_ValidProposal_Returns200WithUpdatedWorkflow()
    {
        var (client, db, authService, scope) = CreateTestClient();
        using var _ = scope;

        var (_, managerToken) = CreateUserAndToken(db, authService, AppRoles.MunicipalManager);
        var (officerUser, _) = CreateUserAndToken(db, authService, AppRoles.WasteOfficer);

        var c2Output = JsonSerializer.Serialize(new
        {
            status = "completed",
            isCompleteSnapshot = true,
            collectionNeeds = new[] { new { taskId = Guid.NewGuid() } }
        });

        var (workflow, step) = SeedWorkflowWithStep(
            db, officerUser.Id, AgentWorkflowStatus.AwaitingCollectionApproval, WorkflowStepType.CollectionPlanning, c2Output, version: 3);

        var reqBody = new ApproveCollectionPlanningRequest
        {
            ExpectedVersion = 3,
            Reason = "Collection schedule verified and approved."
        };

        var req = CreateRequest(HttpMethod.Post, $"/api/v1/agent-workflows/{workflow.Id}/collection-approval/approve", managerToken, reqBody);
        var res = await client.SendAsync(req);

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await res.Content.ReadFromJsonAsync<AgentWorkflowDetailDto>(JsonOptions);
        detail.Should().NotBeNull();
        detail!.Status.Should().Be(AgentWorkflowStatus.CollectionApproved);
        detail.Version.Should().Be(4);
        detail.Approvals.Should().HaveCount(1);
        detail.Approvals[0].ApprovalStage.Should().Be(WorkflowApprovalStage.CollectionPlanning);
        detail.Approvals[0].Decision.Should().Be(WorkflowApprovalDecision.Approved);
        detail.Approvals[0].WorkflowStepId.Should().Be(step.Id);
        detail.Transitions.Should().HaveCount(1);
        detail.Transitions[0].ToStatus.Should().Be(AgentWorkflowStatus.CollectionApproved);
    }

    [Fact]
    public async Task ApproveCollection_WasteOfficer_ValidProposal_Returns200WithUpdatedWorkflow()
    {
        var (client, db, authService, scope) = CreateTestClient();
        using var _ = scope;

        var (officerUser, officerToken) = CreateUserAndToken(db, authService, AppRoles.WasteOfficer);
        var (creatorUser, _) = CreateUserAndToken(db, authService, AppRoles.WasteOfficer);

        var c2Output = JsonSerializer.Serialize(new
        {
            status = "completed",
            isCompleteSnapshot = true,
            collectionNeeds = new[] { new { taskId = Guid.NewGuid() } }
        });

        var (workflow, step) = SeedWorkflowWithStep(
            db, creatorUser.Id, AgentWorkflowStatus.AwaitingCollectionApproval, WorkflowStepType.CollectionPlanning, c2Output, version: 3);

        var reqBody = new ApproveCollectionPlanningRequest
        {
            ExpectedVersion = 3,
            Reason = "Collection schedule verified and approved by WasteOfficer."
        };

        var req = CreateRequest(HttpMethod.Post, $"/api/v1/agent-workflows/{workflow.Id}/collection-approval/approve", officerToken, reqBody);
        var res = await client.SendAsync(req);

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await res.Content.ReadFromJsonAsync<AgentWorkflowDetailDto>(JsonOptions);
        detail.Should().NotBeNull();
        detail!.Status.Should().Be(AgentWorkflowStatus.CollectionApproved);
        detail.Version.Should().Be(4);
        detail.Approvals.Should().HaveCount(1);
        detail.Approvals[0].ApprovalStage.Should().Be(WorkflowApprovalStage.CollectionPlanning);
        detail.Approvals[0].Decision.Should().Be(WorkflowApprovalDecision.Approved);
        detail.Approvals[0].DecidedByUserId.Should().Be(officerUser.Id);
        detail.Approvals[0].WorkflowStepId.Should().Be(step.Id);
        detail.Transitions.Should().HaveCount(1);
        detail.Transitions[0].ToStatus.Should().Be(AgentWorkflowStatus.CollectionApproved);
        detail.Transitions[0].ChangedByUserId.Should().Be(officerUser.Id);
    }

    [Theory]
    [InlineData(AppRoles.Citizen)]
    [InlineData(AppRoles.Driver)]
    public async Task ApproveCollection_UnauthorizedRoles_Returns403Forbidden(string role)
    {
        var (client, db, authService, scope) = CreateTestClient();
        using var _ = scope;

        var (_, token) = CreateUserAndToken(db, authService, role);
        var workflowId = Guid.NewGuid();

        var reqBody = new ApproveCollectionPlanningRequest { ExpectedVersion = 1 };
        var req = CreateRequest(HttpMethod.Post, $"/api/v1/agent-workflows/{workflowId}/collection-approval/approve", token, reqBody);
        var res = await client.SendAsync(req);

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ApproveCollection_Unauthenticated_Returns401Unauthorized()
    {
        var (client, _, _, scope) = CreateTestClient();
        using var _ = scope;

        var req = CreateRequest(HttpMethod.Post, $"/api/v1/agent-workflows/{Guid.NewGuid()}/collection-approval/approve", token: null, new { expectedVersion = 1 });
        var res = await client.SendAsync(req);

        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ApproveCollection_WrongWorkflowState_Returns409Conflict()
    {
        var (client, db, authService, scope) = CreateTestClient();
        using var _ = scope;

        var (manager, managerToken) = CreateUserAndToken(db, authService, AppRoles.MunicipalManager);
        var c2Output = JsonSerializer.Serialize(new { status = "completed", isCompleteSnapshot = true });

        // Seed in Planning state instead of AwaitingCollectionApproval
        var (workflow, _) = SeedWorkflowWithStep(
            db, manager.Id, AgentWorkflowStatus.Planning, WorkflowStepType.CollectionPlanning, c2Output, version: 1);

        var reqBody = new ApproveCollectionPlanningRequest { ExpectedVersion = 1 };
        var req = CreateRequest(HttpMethod.Post, $"/api/v1/agent-workflows/{workflow.Id}/collection-approval/approve", managerToken, reqBody);
        var res = await client.SendAsync(req);

        res.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await res.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problem!.Status.Should().Be(409);
    }

    [Fact]
    public async Task ApproveCollection_IncompleteC2Snapshot_Returns409Conflict()
    {
        var (client, db, authService, scope) = CreateTestClient();
        using var _ = scope;

        var (manager, managerToken) = CreateUserAndToken(db, authService, AppRoles.MunicipalManager);
        var c2Output = JsonSerializer.Serialize(new { status = "completed", isCompleteSnapshot = false });

        var (workflow, _) = SeedWorkflowWithStep(
            db, manager.Id, AgentWorkflowStatus.AwaitingCollectionApproval, WorkflowStepType.CollectionPlanning, c2Output, version: 1);

        var reqBody = new ApproveCollectionPlanningRequest { ExpectedVersion = 1 };
        var req = CreateRequest(HttpMethod.Post, $"/api/v1/agent-workflows/{workflow.Id}/collection-approval/approve", managerToken, reqBody);
        var res = await client.SendAsync(req);

        res.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await res.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problem!.Detail.Should().Contain("not based on a complete authoritative snapshot");
    }

    [Fact]
    public async Task ApproveCollection_C2StatusPartial_Returns409Conflict()
    {
        var (client, db, authService, scope) = CreateTestClient();
        using var _ = scope;

        var (manager, managerToken) = CreateUserAndToken(db, authService, AppRoles.MunicipalManager);
        var c2Output = JsonSerializer.Serialize(new { status = "partial", isCompleteSnapshot = true });

        var (workflow, _) = SeedWorkflowWithStep(
            db, manager.Id, AgentWorkflowStatus.AwaitingCollectionApproval, WorkflowStepType.CollectionPlanning, c2Output, version: 1);

        var reqBody = new ApproveCollectionPlanningRequest { ExpectedVersion = 1 };
        var req = CreateRequest(HttpMethod.Post, $"/api/v1/agent-workflows/{workflow.Id}/collection-approval/approve", managerToken, reqBody);
        var res = await client.SendAsync(req);

        res.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ApproveCollection_StaleExpectedVersion_Returns409Conflict()
    {
        var (client, db, authService, scope) = CreateTestClient();
        using var _ = scope;

        var (manager, managerToken) = CreateUserAndToken(db, authService, AppRoles.MunicipalManager);
        var c2Output = JsonSerializer.Serialize(new { status = "completed", isCompleteSnapshot = true });

        var (workflow, _) = SeedWorkflowWithStep(
            db, manager.Id, AgentWorkflowStatus.AwaitingCollectionApproval, WorkflowStepType.CollectionPlanning, c2Output, version: 5);

        // ExpectedVersion is 4, but current DB version is 5
        var reqBody = new ApproveCollectionPlanningRequest { ExpectedVersion = 4 };
        var req = CreateRequest(HttpMethod.Post, $"/api/v1/agent-workflows/{workflow.Id}/collection-approval/approve", managerToken, reqBody);
        var res = await client.SendAsync(req);

        res.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await res.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problem!.Detail.Should().Contain("changed since it was loaded");
    }

    [Fact]
    public async Task ApproveCollection_SequentialDuplicateApproval_SecondAttemptReturns409Conflict()
    {
        var (client, db, authService, scope) = CreateTestClient();
        using var _ = scope;

        var (manager, managerToken) = CreateUserAndToken(db, authService, AppRoles.MunicipalManager);
        var c2Output = JsonSerializer.Serialize(new { status = "completed", isCompleteSnapshot = true });

        var (workflow, _) = SeedWorkflowWithStep(
            db, manager.Id, AgentWorkflowStatus.AwaitingCollectionApproval, WorkflowStepType.CollectionPlanning, c2Output, version: 1);

        var reqBody = new ApproveCollectionPlanningRequest { ExpectedVersion = 1 };

        // First attempt succeeds
        var req1 = CreateRequest(HttpMethod.Post, $"/api/v1/agent-workflows/{workflow.Id}/collection-approval/approve", managerToken, reqBody);
        var res1 = await client.SendAsync(req1);
        res1.StatusCode.Should().Be(HttpStatusCode.OK);

        // Second attempt with stale ExpectedVersion 1 (and now workflow is in CollectionApproved) -> 409 Conflict
        var req2 = CreateRequest(HttpMethod.Post, $"/api/v1/agent-workflows/{workflow.Id}/collection-approval/approve", managerToken, reqBody);
        var res2 = await client.SendAsync(req2);
        res2.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task RequestCollectionRevision_MunicipalManager_Returns200WithNeedsRevisionStatus()
    {
        var (client, db, authService, scope) = CreateTestClient();
        using var _ = scope;

        var (manager, managerToken) = CreateUserAndToken(db, authService, AppRoles.MunicipalManager);
        var c2Output = JsonSerializer.Serialize(new { status = "partial", isCompleteSnapshot = false });

        var (workflow, step) = SeedWorkflowWithStep(
            db, manager.Id, AgentWorkflowStatus.AwaitingCollectionApproval, WorkflowStepType.CollectionPlanning, c2Output, version: 2);

        var reqBody = new RequestCollectionRevisionRequest
        {
            ExpectedVersion = 2,
            Reason = "Replan collection window to start at 08:00 AM."
        };

        var req = CreateRequest(HttpMethod.Post, $"/api/v1/agent-workflows/{workflow.Id}/collection-approval/request-revision", managerToken, reqBody);
        var res = await client.SendAsync(req);

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await res.Content.ReadFromJsonAsync<AgentWorkflowDetailDto>(JsonOptions);
        detail!.Status.Should().Be(AgentWorkflowStatus.CollectionNeedsRevision);
        detail.Version.Should().Be(3);
        detail.Approvals.Should().HaveCount(1);
        detail.Approvals[0].Decision.Should().Be(WorkflowApprovalDecision.RevisionRequested);
        detail.Approvals[0].WorkflowStepId.Should().Be(step.Id);
    }

    [Fact]
    public async Task RequestCollectionRevision_MissingReason_Returns400BadRequest()
    {
        var (client, db, authService, scope) = CreateTestClient();
        using var _ = scope;

        var (manager, managerToken) = CreateUserAndToken(db, authService, AppRoles.MunicipalManager);
        var c2Output = JsonSerializer.Serialize(new { status = "completed", isCompleteSnapshot = true });

        var (workflow, _) = SeedWorkflowWithStep(
            db, manager.Id, AgentWorkflowStatus.AwaitingCollectionApproval, WorkflowStepType.CollectionPlanning, c2Output, version: 1);

        var reqBody = new RequestCollectionRevisionRequest { ExpectedVersion = 1, Reason = "" };
        var req = CreateRequest(HttpMethod.Post, $"/api/v1/agent-workflows/{workflow.Id}/collection-approval/request-revision", managerToken, reqBody);
        var res = await client.SendAsync(req);

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RejectCollection_MunicipalManager_Returns200WithTerminalRejectedStatus()
    {
        var (client, db, authService, scope) = CreateTestClient();
        using var _ = scope;

        var (manager, managerToken) = CreateUserAndToken(db, authService, AppRoles.MunicipalManager);
        var c2Output = JsonSerializer.Serialize(new { status = "completed", isCompleteSnapshot = true });

        var (workflow, _) = SeedWorkflowWithStep(
            db, manager.Id, AgentWorkflowStatus.AwaitingCollectionApproval, WorkflowStepType.CollectionPlanning, c2Output, version: 1);

        var reqBody = new RejectCollectionPlanningRequest
        {
            ExpectedVersion = 1,
            Reason = "Collection cancelled due to adverse weather emergency."
        };

        var req = CreateRequest(HttpMethod.Post, $"/api/v1/agent-workflows/{workflow.Id}/collection-approval/reject", managerToken, reqBody);
        var res = await client.SendAsync(req);

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await res.Content.ReadFromJsonAsync<AgentWorkflowDetailDto>(JsonOptions);
        detail!.Status.Should().Be(AgentWorkflowStatus.Rejected);
        detail.CompletedAt.Should().NotBeNull();
        detail.Version.Should().Be(2);
        detail.Approvals[0].Decision.Should().Be(WorkflowApprovalDecision.Rejected);
    }

    // =========================================================================
    // Gate 2: Fleet Dispatch Approval HTTP Tests
    // =========================================================================

    [Fact]
    public async Task ApproveDispatch_MunicipalManager_ReadyForReview_Returns200()
    {
        var (client, db, authService, scope) = CreateTestClient();
        using var _ = scope;

        var (manager, managerToken) = CreateUserAndToken(db, authService, AppRoles.MunicipalManager);
        var c4Output = JsonSerializer.Serialize(new
        {
            validationOutcome = "ReadyForHumanReview",
            requiresAcknowledgement = false
        });

        var (workflow, step) = SeedWorkflowWithStep(
            db, manager.Id, AgentWorkflowStatus.AwaitingDispatchApproval, WorkflowStepType.OperationalValidation, c4Output, version: 10);

        var reqBody = new ApproveDispatchPlanRequest
        {
            ExpectedVersion = 10,
            Reason = "Fleet routes reviewed and verified."
        };

        var req = CreateRequest(HttpMethod.Post, $"/api/v1/agent-workflows/{workflow.Id}/dispatch-approval/approve", managerToken, reqBody);
        var res = await client.SendAsync(req);

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await res.Content.ReadFromJsonAsync<AgentWorkflowDetailDto>(JsonOptions);
        detail!.Status.Should().Be(AgentWorkflowStatus.DispatchApproved);
        detail.Version.Should().Be(11);
        detail.Approvals[0].ApprovalStage.Should().Be(WorkflowApprovalStage.FleetDispatch);
        detail.Approvals[0].Decision.Should().Be(WorkflowApprovalDecision.Approved);
        detail.Approvals[0].WorkflowStepId.Should().Be(step.Id);
    }

    [Fact]
    public async Task ApproveDispatch_WasteOfficer_ReadyForReview_Returns200()
    {
        var (client, db, authService, scope) = CreateTestClient();
        using var _ = scope;

        var (officer, officerToken) = CreateUserAndToken(db, authService, AppRoles.WasteOfficer);
        var c4Output = JsonSerializer.Serialize(new
        {
            validationOutcome = "ReadyForHumanReview",
            requiresAcknowledgement = false
        });

        var (workflow, step) = SeedWorkflowWithStep(
            db, officer.Id, AgentWorkflowStatus.AwaitingDispatchApproval, WorkflowStepType.OperationalValidation, c4Output, version: 10);

        var reqBody = new ApproveDispatchPlanRequest
        {
            ExpectedVersion = 10,
            Reason = "Fleet routes reviewed and verified by WasteOfficer."
        };

        var req = CreateRequest(HttpMethod.Post, $"/api/v1/agent-workflows/{workflow.Id}/dispatch-approval/approve", officerToken, reqBody);
        var res = await client.SendAsync(req);

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await res.Content.ReadFromJsonAsync<AgentWorkflowDetailDto>(JsonOptions);
        detail!.Status.Should().Be(AgentWorkflowStatus.DispatchApproved);
        detail.Version.Should().Be(11);
        detail.Approvals[0].ApprovalStage.Should().Be(WorkflowApprovalStage.FleetDispatch);
        detail.Approvals[0].Decision.Should().Be(WorkflowApprovalDecision.Approved);
        detail.Approvals[0].DecidedByUserId.Should().Be(officer.Id);
        detail.Approvals[0].WorkflowStepId.Should().Be(step.Id);
    }

    [Fact]
    public async Task ApproveDispatch_RequiresAcknowledgement_Unacknowledged_Returns409Conflict()
    {
        var (client, db, authService, scope) = CreateTestClient();
        using var _ = scope;

        var (manager, managerToken) = CreateUserAndToken(db, authService, AppRoles.MunicipalManager);
        var c4Output = JsonSerializer.Serialize(new
        {
            validationOutcome = "ReadyForHumanReview",
            requiresAcknowledgement = true
        });

        var (workflow, _) = SeedWorkflowWithStep(
            db, manager.Id, AgentWorkflowStatus.AwaitingDispatchApproval, WorkflowStepType.OperationalValidation, c4Output, version: 1);

        var reqBody = new ApproveDispatchPlanRequest
        {
            ExpectedVersion = 1,
            AcknowledgeWarnings = false
        };

        var req = CreateRequest(HttpMethod.Post, $"/api/v1/agent-workflows/{workflow.Id}/dispatch-approval/approve", managerToken, reqBody);
        var res = await client.SendAsync(req);

        res.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await res.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problem!.Detail.Should().Contain("must be explicitly acknowledged");
    }

    [Fact]
    public async Task ApproveDispatch_RequiresAcknowledgement_Acknowledged_Returns200WithPayload()
    {
        var (client, db, authService, scope) = CreateTestClient();
        using var _ = scope;

        var (manager, managerToken) = CreateUserAndToken(db, authService, AppRoles.MunicipalManager);
        var c4Output = JsonSerializer.Serialize(new
        {
            validationOutcome = "ReadyForHumanReview",
            requiresAcknowledgement = true
        });

        var (workflow, _) = SeedWorkflowWithStep(
            db, manager.Id, AgentWorkflowStatus.AwaitingDispatchApproval, WorkflowStepType.OperationalValidation, c4Output, version: 1);

        var reqBody = new ApproveDispatchPlanRequest
        {
            ExpectedVersion = 1,
            Reason = "Unknown vehicle compatibility noted and accepted.",
            AcknowledgeWarnings = true
        };

        var req = CreateRequest(HttpMethod.Post, $"/api/v1/agent-workflows/{workflow.Id}/dispatch-approval/approve", managerToken, reqBody);
        var res = await client.SendAsync(req);

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await res.Content.ReadFromJsonAsync<AgentWorkflowDetailDto>(JsonOptions);
        detail!.Status.Should().Be(AgentWorkflowStatus.DispatchApproved);
        var payloadRaw = detail.Approvals[0].DecisionPayload?.GetRawText();
        payloadRaw.Should().Contain("\"acknowledgeWarnings\":true");
    }

    [Fact]
    public async Task ApproveDispatch_NeedsRevisionOutcome_Returns409Conflict()
    {
        var (client, db, authService, scope) = CreateTestClient();
        using var _ = scope;

        var (manager, managerToken) = CreateUserAndToken(db, authService, AppRoles.MunicipalManager);
        var c4Output = JsonSerializer.Serialize(new
        {
            validationOutcome = "NeedsRevision",
            requiresAcknowledgement = false
        });

        var (workflow, _) = SeedWorkflowWithStep(
            db, manager.Id, AgentWorkflowStatus.AwaitingDispatchApproval, WorkflowStepType.OperationalValidation, c4Output, version: 1);

        var reqBody = new ApproveDispatchPlanRequest { ExpectedVersion = 1 };
        var req = CreateRequest(HttpMethod.Post, $"/api/v1/agent-workflows/{workflow.Id}/dispatch-approval/approve", managerToken, reqBody);
        var res = await client.SendAsync(req);

        res.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ApproveDispatch_RequiresRevisionOutcome_Returns409Conflict()
    {
        var (client, db, authService, scope) = CreateTestClient();
        using var _ = scope;

        var (manager, managerToken) = CreateUserAndToken(db, authService, AppRoles.MunicipalManager);
        var c4Output = JsonSerializer.Serialize(new
        {
            validationOutcome = "RequiresRevision",
            requiresAcknowledgement = false
        });

        var (workflow, _) = SeedWorkflowWithStep(
            db, manager.Id, AgentWorkflowStatus.AwaitingDispatchApproval, WorkflowStepType.OperationalValidation, c4Output, version: 1);

        var reqBody = new ApproveDispatchPlanRequest { ExpectedVersion = 1 };
        var req = CreateRequest(HttpMethod.Post, $"/api/v1/agent-workflows/{workflow.Id}/dispatch-approval/approve", managerToken, reqBody);
        var res = await client.SendAsync(req);

        res.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ApproveDispatch_RejectedOutcome_Returns409Conflict()
    {
        var (client, db, authService, scope) = CreateTestClient();
        using var _ = scope;

        var (manager, managerToken) = CreateUserAndToken(db, authService, AppRoles.MunicipalManager);
        var c4Output = JsonSerializer.Serialize(new
        {
            validationOutcome = "Rejected",
            requiresAcknowledgement = false
        });

        var (workflow, _) = SeedWorkflowWithStep(
            db, manager.Id, AgentWorkflowStatus.AwaitingDispatchApproval, WorkflowStepType.OperationalValidation, c4Output, version: 1);

        var reqBody = new ApproveDispatchPlanRequest { ExpectedVersion = 1 };
        var req = CreateRequest(HttpMethod.Post, $"/api/v1/agent-workflows/{workflow.Id}/dispatch-approval/approve", managerToken, reqBody);
        var res = await client.SendAsync(req);

        res.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task RequestDispatchRevision_MunicipalManager_Returns200WithNeedsRevisionStatus()
    {
        var (client, db, authService, scope) = CreateTestClient();
        using var _ = scope;

        var (manager, managerToken) = CreateUserAndToken(db, authService, AppRoles.MunicipalManager);
        var c4Output = JsonSerializer.Serialize(new { validationOutcome = "NeedsRevision" });

        var (workflow, step) = SeedWorkflowWithStep(
            db, manager.Id, AgentWorkflowStatus.AwaitingDispatchApproval, WorkflowStepType.OperationalValidation, c4Output, version: 2);

        var reqBody = new RequestDispatchRevisionRequest
        {
            ExpectedVersion = 2,
            Reason = "Re-route vehicle away from flooded highway."
        };

        var req = CreateRequest(HttpMethod.Post, $"/api/v1/agent-workflows/{workflow.Id}/dispatch-approval/request-revision", managerToken, reqBody);
        var res = await client.SendAsync(req);

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await res.Content.ReadFromJsonAsync<AgentWorkflowDetailDto>(JsonOptions);
        detail!.Status.Should().Be(AgentWorkflowStatus.DispatchNeedsRevision);
        detail.Approvals[0].ApprovalStage.Should().Be(WorkflowApprovalStage.FleetDispatch);
        detail.Approvals[0].Decision.Should().Be(WorkflowApprovalDecision.RevisionRequested);
        detail.Approvals[0].WorkflowStepId.Should().Be(step.Id);
    }

    [Fact]
    public async Task RejectDispatch_MunicipalManager_Returns200WithTerminalRejectedStatus()
    {
        var (client, db, authService, scope) = CreateTestClient();
        using var _ = scope;

        var (manager, managerToken) = CreateUserAndToken(db, authService, AppRoles.MunicipalManager);
        var c4Output = JsonSerializer.Serialize(new { validationOutcome = "ReadyForHumanReview" });

        var (workflow, _) = SeedWorkflowWithStep(
            db, manager.Id, AgentWorkflowStatus.AwaitingDispatchApproval, WorkflowStepType.OperationalValidation, c4Output, version: 4);

        var reqBody = new RejectDispatchPlanRequest
        {
            ExpectedVersion = 4,
            Reason = "Fleet operations suspended due to curfew."
        };

        var req = CreateRequest(HttpMethod.Post, $"/api/v1/agent-workflows/{workflow.Id}/dispatch-approval/reject", managerToken, reqBody);
        var res = await client.SendAsync(req);

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await res.Content.ReadFromJsonAsync<AgentWorkflowDetailDto>(JsonOptions);
        detail!.Status.Should().Be(AgentWorkflowStatus.Rejected);
        detail.CompletedAt.Should().NotBeNull();
        detail.Version.Should().Be(5);
    }

    [Fact]
    public async Task ApprovalAndTransitions_ReflectedInWorkflowDetailGetEndpoint()
    {
        var (client, db, authService, scope) = CreateTestClient();
        using var _ = scope;

        var (manager, managerToken) = CreateUserAndToken(db, authService, AppRoles.MunicipalManager);
        var c2Output = JsonSerializer.Serialize(new { status = "completed", isCompleteSnapshot = true });

        var (workflow, step) = SeedWorkflowWithStep(
            db, manager.Id, AgentWorkflowStatus.AwaitingCollectionApproval, WorkflowStepType.CollectionPlanning, c2Output, version: 1);

        // Approve collection
        var approveReq = CreateRequest(
            HttpMethod.Post,
            $"/api/v1/agent-workflows/{workflow.Id}/collection-approval/approve",
            managerToken,
            new ApproveCollectionPlanningRequest { ExpectedVersion = 1, Reason = "Verified." });

        var approveRes = await client.SendAsync(approveReq);
        approveRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // Call GET /api/v1/agent-workflows/{id}
        var getReq = CreateRequest(HttpMethod.Get, $"/api/v1/agent-workflows/{workflow.Id}", managerToken);
        var getRes = await client.SendAsync(getReq);

        getRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await getRes.Content.ReadFromJsonAsync<AgentWorkflowDetailDto>(JsonOptions);

        detail.Should().NotBeNull();
        detail!.Status.Should().Be(AgentWorkflowStatus.CollectionApproved);
        detail.Version.Should().Be(2);
        detail.Approvals.Should().HaveCount(1);
        detail.Approvals[0].WorkflowStepId.Should().Be(step.Id);
        detail.Transitions.Should().HaveCount(1);
        detail.Transitions[0].FromStatus.Should().Be(AgentWorkflowStatus.AwaitingCollectionApproval);
        detail.Transitions[0].ToStatus.Should().Be(AgentWorkflowStatus.CollectionApproved);
    }
}
