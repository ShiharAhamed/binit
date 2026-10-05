using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
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
/// End-to-end API integration tests exercising real ASP.NET Core HTTP pipeline:
/// routing, JWT Bearer authentication, role authorization, row-level visibility,
/// ExceptionHandlingMiddleware (409 Conflict ProblemDetails), and persistence.
/// Uses CustomWebApplicationFactory with in-memory AppDbContext to run fast, deterministically,
/// and without requiring an external PostgreSQL instance.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class AgentWorkflowsApiAuthIntegrationTests
{
    private readonly CustomWebApplicationFactory _factory;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false) }
    };

    public AgentWorkflowsApiAuthIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private (HttpClient client, AppDbContext db, IAuthService authService, IServiceScope scope) CreateTestClient()
    {
        var dbName = $"SmartWaste_WorkflowApiAuth_{Guid.NewGuid():N}";
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
            request.Content = JsonContent.Create(body);
        }
        return request;
    }

    // =========================================================================
    // 1. AUTH TEST 1 — UNAUTHENTICATED
    // =========================================================================

    [Fact]
    public async Task Request_WithoutToken_Returns401Unauthorized()
    {
        var (client, _, _, scope) = CreateTestClient();
        using (scope)
        {
            var postReq = CreateRequest(HttpMethod.Post, "/api/v1/agent-workflows", null,
                new CreateAgentWorkflowRequest { Objective = "Valid length objective text" });
            var postRes = await client.SendAsync(postReq);
            postRes.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

            var getReq = CreateRequest(HttpMethod.Get, "/api/v1/agent-workflows", null);
            var getRes = await client.SendAsync(getReq);
            getRes.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
    }

    // =========================================================================
    // 2. AUTH TEST 2 — CITIZEN ROLE
    // =========================================================================

    [Fact]
    public async Task Request_CitizenToken_Returns403Forbidden()
    {
        var (client, db, authService, scope) = CreateTestClient();
        using (scope)
        {
            var (_, citizenToken) = CreateUserAndToken(db, authService, AppRoles.Citizen);

            var req = CreateRequest(HttpMethod.Get, "/api/v1/agent-workflows", citizenToken);
            var res = await client.SendAsync(req);

            res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
    }

    // =========================================================================
    // 3. AUTH TEST 3 — DRIVER ROLE
    // =========================================================================

    [Fact]
    public async Task Request_DriverToken_Returns403Forbidden()
    {
        var (client, db, authService, scope) = CreateTestClient();
        using (scope)
        {
            var (_, driverToken) = CreateUserAndToken(db, authService, AppRoles.Driver);

            var req = CreateRequest(HttpMethod.Get, "/api/v1/agent-workflows", driverToken);
            var res = await client.SendAsync(req);

            res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
    }

    // =========================================================================
    // 4. AUTH TEST 4 — WASTE OFFICER OWN WORKFLOW
    // =========================================================================

    [Fact]
    public async Task GetById_WasteOfficerOwnWorkflow_Returns200OK()
    {
        var (client, db, authService, scope) = CreateTestClient();
        using (scope)
        {
            var (officer, token) = CreateUserAndToken(db, authService, AppRoles.WasteOfficer);

            var wf = new AgentWorkflow
            {
                Id = Guid.NewGuid(),
                Objective = "Officer's own workflow",
                Status = AgentWorkflowStatus.Created,
                CurrentStep = WorkflowStepType.None,
                InitiatedByUserId = officer.Id,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Version = 1
            };
            db.AgentWorkflows.Add(wf);
            await db.SaveChangesAsync();

            var req = CreateRequest(HttpMethod.Get, $"/api/v1/agent-workflows/{wf.Id}", token);
            var res = await client.SendAsync(req);

            res.StatusCode.Should().Be(HttpStatusCode.OK);
            var detail = await res.Content.ReadFromJsonAsync<AgentWorkflowDetailDto>(JsonOptions);
            detail.Should().NotBeNull();
            detail!.Id.Should().Be(wf.Id);
            detail.InitiatedByUserId.Should().Be(officer.Id);
        }
    }

    // =========================================================================
    // 5. AUTH TEST 5 — WASTE OFFICER OTHER USER WORKFLOW
    // =========================================================================

    [Fact]
    public async Task GetById_WasteOfficerAccessingOtherOfficersWorkflow_Returns200OK()
    {
        var (client, db, authService, scope) = CreateTestClient();
        using (scope)
        {
            var (_, tokenA) = CreateUserAndToken(db, authService, AppRoles.WasteOfficer);
            var (officerB, _) = CreateUserAndToken(db, authService, AppRoles.WasteOfficer);

            var wfB = new AgentWorkflow
            {
                Id = Guid.NewGuid(),
                Objective = "Officer B's workflow",
                Status = AgentWorkflowStatus.Created,
                CurrentStep = WorkflowStepType.None,
                InitiatedByUserId = officerB.Id,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Version = 1
            };
            db.AgentWorkflows.Add(wfB);
            await db.SaveChangesAsync();

            var req = CreateRequest(HttpMethod.Get, $"/api/v1/agent-workflows/{wfB.Id}", tokenA);
            var res = await client.SendAsync(req);

            // Equal authority: WasteOfficer A can view WasteOfficer B's workflow
            res.StatusCode.Should().Be(HttpStatusCode.OK);
            var detail = await res.Content.ReadFromJsonAsync<AgentWorkflowDetailDto>(JsonOptions);
            detail.Should().NotBeNull();
            detail!.Id.Should().Be(wfB.Id);
        }
    }

    // =========================================================================
    // 6. AUTH TEST 6 — MUNICIPAL MANAGER CROSS-USER ACCESS
    // =========================================================================

    [Fact]
    public async Task GetById_MunicipalManagerViewingOfficersWorkflow_Returns200OK()
    {
        var (client, db, authService, scope) = CreateTestClient();
        using (scope)
        {
            var (_, managerToken) = CreateUserAndToken(db, authService, AppRoles.MunicipalManager);
            var (officer, _) = CreateUserAndToken(db, authService, AppRoles.WasteOfficer);

            var wf = new AgentWorkflow
            {
                Id = Guid.NewGuid(),
                Objective = "Officer's workflow inspected by Manager",
                Status = AgentWorkflowStatus.Created,
                CurrentStep = WorkflowStepType.None,
                InitiatedByUserId = officer.Id,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Version = 1
            };
            db.AgentWorkflows.Add(wf);
            await db.SaveChangesAsync();

            var req = CreateRequest(HttpMethod.Get, $"/api/v1/agent-workflows/{wf.Id}", managerToken);
            var res = await client.SendAsync(req);

            res.StatusCode.Should().Be(HttpStatusCode.OK);
            var detail = await res.Content.ReadFromJsonAsync<AgentWorkflowDetailDto>(JsonOptions);
            detail.Should().NotBeNull();
            detail!.Id.Should().Be(wf.Id);
            detail.InitiatedByUserId.Should().Be(officer.Id);
        }
    }

    // =========================================================================
    // 7. AUTH TEST 7 — START OWN WORKFLOW
    // =========================================================================

    [Fact]
    public async Task Start_WasteOfficerOwnWorkflow_Returns200OKAndAwaitsCollectionApproval()
    {
        var (client, db, authService, scope) = CreateTestClient();
        using (scope)
        {
            var (officer, token) = CreateUserAndToken(db, authService, AppRoles.WasteOfficer);

            var wf = new AgentWorkflow
            {
                Id = Guid.NewGuid(),
                Objective = "Officer's workflow to start",
                Status = AgentWorkflowStatus.Created,
                CurrentStep = WorkflowStepType.None,
                InitiatedByUserId = officer.Id,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Version = 1
            };
            db.AgentWorkflows.Add(wf);
            await db.SaveChangesAsync();

            var req = CreateRequest(HttpMethod.Post, $"/api/v1/agent-workflows/{wf.Id}/start", token);
            var res = await client.SendAsync(req);

            res.StatusCode.Should().Be(HttpStatusCode.OK);
            var summary = await res.Content.ReadFromJsonAsync<AgentWorkflowSummaryDto>(JsonOptions);
            summary.Should().NotBeNull();
            summary!.Status.Should().Be(AgentWorkflowStatus.AwaitingCollectionApproval);
            summary.CurrentStep.Should().Be(WorkflowStepType.CollectionPlanning);
            summary.Version.Should().Be(3);

            // Verify persisted database state
            var persistedWf = await db.AgentWorkflows.AsNoTracking().Include(w => w.Transitions).FirstOrDefaultAsync(w => w.Id == wf.Id);
            persistedWf.Should().NotBeNull();
            persistedWf!.Status.Should().Be(AgentWorkflowStatus.AwaitingCollectionApproval);
            persistedWf.CurrentStep.Should().Be(WorkflowStepType.CollectionPlanning);
            persistedWf.Transitions.Should().Contain(t =>
                t.FromStatus == AgentWorkflowStatus.Created &&
                t.ToStatus == AgentWorkflowStatus.Planning &&
                t.ChangedByUserId == officer.Id);
            persistedWf.Transitions.Should().Contain(t =>
                t.FromStatus == AgentWorkflowStatus.Planning &&
                t.ToStatus == AgentWorkflowStatus.AwaitingCollectionApproval);
        }
    }

    // =========================================================================
    // 8. AUTH TEST 8 — START ANOTHER OFFICER'S WORKFLOW
    // =========================================================================

    [Fact]
    public async Task Start_WasteOfficerOtherOfficersWorkflow_Returns200OKAndAwaitsCollectionApproval()
    {
        var (client, db, authService, scope) = CreateTestClient();
        using (scope)
        {
            var (_, tokenA) = CreateUserAndToken(db, authService, AppRoles.WasteOfficer);
            var (officerB, _) = CreateUserAndToken(db, authService, AppRoles.WasteOfficer);

            var wfB = new AgentWorkflow
            {
                Id = Guid.NewGuid(),
                Objective = "Officer B's workflow started by officer A",
                Status = AgentWorkflowStatus.Created,
                CurrentStep = WorkflowStepType.None,
                InitiatedByUserId = officerB.Id,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Version = 1
            };
            db.AgentWorkflows.Add(wfB);
            await db.SaveChangesAsync();

            var req = CreateRequest(HttpMethod.Post, $"/api/v1/agent-workflows/{wfB.Id}/start", tokenA);
            var res = await client.SendAsync(req);

            // Equal authority: WasteOfficer A can start WasteOfficer B's workflow
            res.StatusCode.Should().Be(HttpStatusCode.OK);
            var summary = await res.Content.ReadFromJsonAsync<AgentWorkflowSummaryDto>(JsonOptions);
            summary.Should().NotBeNull();
            summary!.Status.Should().Be(AgentWorkflowStatus.AwaitingCollectionApproval);
            summary.CurrentStep.Should().Be(WorkflowStepType.CollectionPlanning);

            var persistedWf = await db.AgentWorkflows.AsNoTracking().Include(w => w.Transitions).FirstOrDefaultAsync(w => w.Id == wfB.Id);
            persistedWf.Should().NotBeNull();
            persistedWf!.Status.Should().Be(AgentWorkflowStatus.AwaitingCollectionApproval);
            persistedWf.CurrentStep.Should().Be(WorkflowStepType.CollectionPlanning);
            persistedWf.Transitions.Should().HaveCount(2);
        }
    }

    // =========================================================================
    // 9. AUTH TEST 9 — MANAGER START
    // =========================================================================

    [Fact]
    public async Task Start_MunicipalManagerPermittedWorkflow_Returns200OK()
    {
        var (client, db, authService, scope) = CreateTestClient();
        using (scope)
        {
            var (_, managerToken) = CreateUserAndToken(db, authService, AppRoles.MunicipalManager);
            var (officer, _) = CreateUserAndToken(db, authService, AppRoles.WasteOfficer);

            var wf = new AgentWorkflow
            {
                Id = Guid.NewGuid(),
                Objective = "Officer's workflow started by manager",
                Status = AgentWorkflowStatus.Created,
                CurrentStep = WorkflowStepType.None,
                InitiatedByUserId = officer.Id,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Version = 1
            };
            db.AgentWorkflows.Add(wf);
            await db.SaveChangesAsync();

            var req = CreateRequest(HttpMethod.Post, $"/api/v1/agent-workflows/{wf.Id}/start", managerToken);
            var res = await client.SendAsync(req);

            res.StatusCode.Should().Be(HttpStatusCode.OK);
            var summary = await res.Content.ReadFromJsonAsync<AgentWorkflowSummaryDto>(JsonOptions);
            summary.Should().NotBeNull();
            summary!.Status.Should().Be(AgentWorkflowStatus.AwaitingCollectionApproval);
        }
    }

    // =========================================================================
    // 10. AUTH TEST 10 — INVALID START RETURNS 409 CONFLICT (PROBLEMDETAILS)
    // =========================================================================

    [Theory]
    [InlineData(AgentWorkflowStatus.Planning)]
    [InlineData(AgentWorkflowStatus.Completed)]
    [InlineData(AgentWorkflowStatus.Rejected)]
    public async Task Start_InvalidState_Returns409ConflictWithProblemDetails(AgentWorkflowStatus currentStatus)
    {
        var (client, db, authService, scope) = CreateTestClient();
        using (scope)
        {
            var (officer, token) = CreateUserAndToken(db, authService, AppRoles.WasteOfficer);

            var wf = new AgentWorkflow
            {
                Id = Guid.NewGuid(),
                Objective = $"Workflow in status {currentStatus}",
                Status = currentStatus,
                CurrentStep = currentStatus == AgentWorkflowStatus.Planning ? WorkflowStepType.SharedPlanning : WorkflowStepType.None,
                InitiatedByUserId = officer.Id,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Version = 1
            };
            db.AgentWorkflows.Add(wf);
            await db.SaveChangesAsync();

            var req = CreateRequest(HttpMethod.Post, $"/api/v1/agent-workflows/{wf.Id}/start", token);
            var res = await client.SendAsync(req);

            res.StatusCode.Should().Be(HttpStatusCode.Conflict);
            res.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

            var problem = await res.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
            problem.Should().NotBeNull();
            problem!.Status.Should().Be((int)HttpStatusCode.Conflict);
            problem.Title.Should().Contain("Workflow State Transition");
            problem.Detail.Should().Contain("Cannot transition");
        }
    }

    // =========================================================================
    // 11. CREATE AUTH TEST — INITIATOR EXTRACTED AUTHORITATIVELY FROM CLAIMS
    // =========================================================================

    [Fact]
    public async Task Create_AuthenticatedWasteOfficer_SetsInitiatedByUserIdStrictlyFromClaims()
    {
        var (client, db, authService, scope) = CreateTestClient();
        using (scope)
        {
            var (officer, token) = CreateUserAndToken(db, authService, AppRoles.WasteOfficer);

            var createRequest = new CreateAgentWorkflowRequest
            {
                Objective = "Prepare an end-to-end waste collection workflow."
            };

            var req = CreateRequest(HttpMethod.Post, "/api/v1/agent-workflows", token, createRequest);
            var res = await client.SendAsync(req);

            res.StatusCode.Should().Be(HttpStatusCode.Created);
            res.Headers.Location.Should().NotBeNull();

            var summary = await res.Content.ReadFromJsonAsync<AgentWorkflowSummaryDto>(JsonOptions);
            summary.Should().NotBeNull();
            summary!.InitiatedByUserId.Should().Be(officer.Id);
            summary.Objective.Should().Be(createRequest.Objective);
            summary.Status.Should().Be(AgentWorkflowStatus.Created);
            summary.CurrentStep.Should().Be(WorkflowStepType.None);

            // Verify in persisted database directly
            var persistedWf = await db.AgentWorkflows.FirstOrDefaultAsync(w => w.Id == summary.Id);
            persistedWf.Should().NotBeNull();
            persistedWf!.InitiatedByUserId.Should().Be(officer.Id);
        }
    }

    // =========================================================================
    // 12. 400 BAD REQUEST FOR VALIDATION ERRORS VS 409 CONFLICT FOR STATE ERRORS
    // =========================================================================

    [Fact]
    public async Task Create_InvalidObjective_Returns400BadRequest_Not409Conflict()
    {
        var (client, db, authService, scope) = CreateTestClient();
        using (scope)
        {
            var (_, token) = CreateUserAndToken(db, authService, AppRoles.WasteOfficer);

            var invalidRequest = new CreateAgentWorkflowRequest
            {
                Objective = "shrt" // Under 5 characters -> fails FluentValidation
            };

            var req = CreateRequest(HttpMethod.Post, "/api/v1/agent-workflows", token, invalidRequest);
            var res = await client.SendAsync(req);

            res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var problem = await res.Content.ReadFromJsonAsync<ValidationProblemDetails>(JsonOptions);
            problem.Should().NotBeNull();
            problem!.Status.Should().Be(StatusCodes.Status400BadRequest);
            problem.Errors.Should().ContainKey("Objective");
        }
    }
}
