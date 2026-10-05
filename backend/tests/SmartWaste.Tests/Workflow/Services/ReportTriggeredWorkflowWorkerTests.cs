using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SmartWaste.Application.Common.Exceptions;
using SmartWaste.Application.DTOs.Auth;
using SmartWaste.Application.Reporting.DTOs.Requests;
using SmartWaste.Application.Reporting.DTOs.Responses;
using SmartWaste.Application.Reporting.Interfaces;
using SmartWaste.Application.Workflow.DTOs.Transport;
using SmartWaste.Application.Workflow.DTOs.Requests;
using SmartWaste.Application.Workflow.Interfaces;
using SmartWaste.Domain.Common;
using SmartWaste.Domain.Entities;
using SmartWaste.Domain.Reporting.Entities;
using SmartWaste.Domain.Reporting.Enums;
using SmartWaste.Domain.Workflow.Entities;
using SmartWaste.Domain.Workflow.Enums;
using SmartWaste.Infrastructure.Persistence;
using SmartWaste.Infrastructure.Workflow.Services;

namespace SmartWaste.Tests.Workflow.Services;

[Collection(IntegrationTestCollection.Name)]
public sealed class ReportTriggeredWorkflowWorkerTests
{
    private const string Objective = "Analyze submitted waste report for authorized staff verification and collection planning.";
    private readonly CustomWebApplicationFactory _factory;

    public ReportTriggeredWorkflowWorkerTests(CustomWebApplicationFactory factory) => _factory = factory;

    private sealed class FakePythonClient : IPythonOrchestrationClient
    {
        public Func<PythonWorkflowStartRequest, CancellationToken, Task<PythonOrchestrationEnvelope>> Start { get; set; } =
            (_, _) => throw new InvalidOperationException("Test start response was not configured.");
        public int StartCalls;
        public Func<PythonWorkflowResumeRequest, CancellationToken, Task<PythonOrchestrationEnvelope>> Resume { get; set; } =
            (_, _) => throw new InvalidOperationException("C2 must not be invoked before Verify commits.");
        public int ResumeCalls;

        public Task<PythonOrchestrationEnvelope> StartAsync(PythonWorkflowStartRequest request, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref StartCalls);
            return Start(request, cancellationToken);
        }

        public Task<PythonOrchestrationEnvelope> ResumeAfterReportVerificationAsync(PythonWorkflowResumeRequest request, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref ResumeCalls);
            return Resume(request, cancellationToken);
        }
        public Task<PythonOrchestrationEnvelope> ResumeAfterCollectionApprovalAsync(PythonWorkflowResumeRequest request, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Worker must not run C3/C4.");
    }

    private sealed record Seed(Guid UserId, Guid ReportId, Guid WorkflowId);

    private Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> CreateHost(FakePythonClient python, int maxAttempts = 3) =>
        _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IPythonOrchestrationClient>();
            services.AddSingleton<IPythonOrchestrationClient>(python);
            services.Configure<ReportTriggeredWorkflowWorkerOptions>(options =>
            {
                options.Enabled = false;
                options.MaxAttempts = maxAttempts;
            });
        }));

    private static async Task<Seed> SeedWorkflowAsync(IServiceProvider services, bool manual = false, bool expiredPlanning = false)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTime.UtcNow;
        var user = new AppUser
        {
            Id = Guid.NewGuid(), UserName = $"worker_{Guid.NewGuid():N}", Email = $"worker_{Guid.NewGuid():N}@test.local",
            FullName = "Worker Test Citizen", IsActive = true, CreatedAt = now
        };
        var report = new WasteReport
        {
            Id = Guid.NewGuid(), CitizenId = user.Id, Description = "Roadside waste",
            Status = WasteReportStatus.Submitted, WasteType = WasteType.General,
            Latitude = 6.9, Longitude = 79.8, CreatedAt = now
        };
        var workflow = new AgentWorkflow
        {
            Id = Guid.NewGuid(), Objective = Objective, InitiatedByUserId = user.Id,
            TriggerType = manual ? AgentWorkflowTriggerType.ManualOperationalPlanning : AgentWorkflowTriggerType.CitizenReportSubmission,
            TriggeringWasteReportId = manual ? null : report.Id,
            Status = expiredPlanning ? AgentWorkflowStatus.Planning : AgentWorkflowStatus.Created,
            CurrentStep = expiredPlanning ? WorkflowStepType.SharedPlanning : WorkflowStepType.None,
            ProcessingLeaseId = expiredPlanning ? Guid.NewGuid() : null,
            ProcessingLeaseExpiresAt = expiredPlanning ? now.AddMinutes(-1) : null,
            ProcessingAttemptCount = expiredPlanning ? 1 : 0,
            Version = expiredPlanning ? 3 : 1, CreatedAt = now
        };
        db.Users.Add(user);
        db.WasteReports.Add(report);
        db.AgentWorkflows.Add(workflow);
        db.AgentWorkflowTransitions.Add(new AgentWorkflowTransition
        {
            Id = Guid.NewGuid(), WorkflowId = workflow.Id, FromStatus = null,
            ToStatus = AgentWorkflowStatus.Created, Reason = "Test workflow created.", ChangedAt = now
        });
        if (expiredPlanning)
            db.AgentWorkflowTransitions.Add(new AgentWorkflowTransition
            {
                Id = Guid.NewGuid(), WorkflowId = workflow.Id,
                FromStatus = AgentWorkflowStatus.Created, ToStatus = AgentWorkflowStatus.Planning,
                Reason = "Prior worker claim.", ChangedAt = now.AddTicks(1)
            });
        await db.SaveChangesAsync();
        return new Seed(user.Id, report.Id, workflow.Id);
    }

    private static PythonOrchestrationEnvelope ValidPause(PythonWorkflowStartRequest request)
    {
        var reportId = request.TriggeringWasteReportId!.Value;
        var planner = JsonSerializer.SerializeToElement(new
        {
            objective = request.Objective,
            steps = new[]
            {
                new { stepId = "step-1", specialist = "WasteAnalysis", objective = "Analyze exactly this report.", dependsOn = Array.Empty<string>(), sequence = 1 },
                new { stepId = "step-2", specialist = "CollectionPlanning", objective = "Plan current collection needs.", dependsOn = new[] { "step-1" }, sequence = 2 },
                new { stepId = "step-3", specialist = "FleetRoute", objective = "Plan fleet routes after approval.", dependsOn = new[] { "step-2" }, sequence = 3 },
                new { stepId = "step-4", specialist = "ValidationOperations", objective = "Validate dispatch operations.", dependsOn = new[] { "step-3" }, sequence = 4 }
            },
            summary = "Four-step planning sequence", warnings = Array.Empty<string>(),
            agentName = "shared_planner_agent", modelName = "fake", advisoryOnly = true
        });
        var c1 = JsonSerializer.SerializeToElement(new
        {
            objective = "Analyze exactly this report.",
            analyses = new[] { new { reportId, categoryAssessment = "Roadside waste",
                recommendedPriority = "Medium", operationalConcerns = Array.Empty<string>(),
                recommendedHandling = "Standard collection", confidence = "Medium",
                rationale = "Report data indicates waste requiring human review." } },
            sourcePage = 1, sourcePageSize = 1, sourceTotalCount = 1,
            agentName = "waste_analysis_agent", modelName = "fake", status = "completed"
        });
        return new PythonOrchestrationEnvelope
        {
            WorkflowId = request.WorkflowId, Objective = request.Objective,
            TriggerType = AgentWorkflowTriggerType.CitizenReportSubmission,
            TriggeringWasteReportId = reportId,
            Status = "Paused", CurrentPhase = "PausedForReportVerification",
            ApprovalStage = "ReportVerification", PauseReason = "Awaiting human verification.",
            PlannerResult = planner, WasteAnalysisResult = c1,
            CompletedSpecialists = ["WasteAnalysis"],
            Errors = JsonSerializer.SerializeToElement(Array.Empty<object>()),
            Warnings = JsonSerializer.SerializeToElement(Array.Empty<string>())
        };
    }

    private static PythonOrchestrationEnvelope ValidContinuation(PythonWorkflowResumeRequest request)
    {
        var snapshot = request.Workflow;
        return new PythonOrchestrationEnvelope
        {
            WorkflowId = snapshot.WorkflowId, Objective = snapshot.Objective,
            TriggerType = AgentWorkflowTriggerType.CitizenReportSubmission,
            TriggeringWasteReportId = snapshot.TriggeringWasteReportId,
            Status = "Paused", CurrentPhase = "PausedForCollectionApproval",
            ApprovalStage = "CollectionPlanning", PauseReason = "Awaiting human collection approval.",
            PlannerResult = snapshot.PlannerResult, WasteAnalysisResult = snapshot.WasteAnalysisResult,
            CollectionPlanningResult = JsonSerializer.SerializeToElement(new
            {
                objective = "Plan current authoritative collection needs.", status = "completed",
                candidateGroups = Array.Empty<object>(), separateHandling = Array.Empty<object>(),
                deferredNeeds = Array.Empty<object>(), isCompleteSnapshot = true
            }),
            CompletedSpecialists = ["WasteAnalysis", "CollectionPlanning"],
            Errors = JsonSerializer.SerializeToElement(Array.Empty<object>()),
            Warnings = JsonSerializer.SerializeToElement(Array.Empty<string>())
        };
    }

    private static async Task<(AgentWorkflow Workflow, WasteReport Report)> ReadAsync(IServiceProvider services, Seed seed)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var workflow = await db.AgentWorkflows.AsNoTracking().Include(w => w.Steps).Include(w => w.Transitions)
            .SingleAsync(w => w.Id == seed.WorkflowId);
        var report = await db.WasteReports.AsNoTracking().SingleAsync(r => r.Id == seed.ReportId);
        return (workflow, report);
    }

    private static async Task CleanupAsync(IServiceProvider services, Seed seed)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.AgentWorkflowSteps.RemoveRange(db.AgentWorkflowSteps.Where(s => s.WorkflowId == seed.WorkflowId));
        db.AgentWorkflowTransitions.RemoveRange(db.AgentWorkflowTransitions.Where(t => t.WorkflowId == seed.WorkflowId));
        db.AgentWorkflows.RemoveRange(db.AgentWorkflows.Where(w => w.Id == seed.WorkflowId));
        db.WasteReportStatusHistories.RemoveRange(db.WasteReportStatusHistories.Where(h => h.WasteReportId == seed.ReportId));
        db.WasteReports.RemoveRange(db.WasteReports.Where(r => r.Id == seed.ReportId));
        db.Users.RemoveRange(db.Users.Where(u => u.Id == seed.UserId));
        await db.SaveChangesAsync();
    }

    private static async Task PrepareVerifiedC2Async(IServiceProvider services, Seed seed)
    {
        using (var initialScope = services.CreateScope())
            await initialScope.ServiceProvider.GetRequiredService<ReportTriggeredWorkflowProcessor>()
                .ProcessOneAsync(workflowId: seed.WorkflowId);
        using var decisionScope = services.CreateScope();
        var reporting = decisionScope.ServiceProvider.GetRequiredService<IWasteReportService>();
        await reporting.StartReviewAsync(seed.ReportId, seed.UserId, AppRoles.MunicipalManager);
        await reporting.VerifyAsync(seed.ReportId,
            new VerifyWasteReportRequest { Priority = WasteReportPriority.Medium },
            seed.UserId, AppRoles.MunicipalManager);
    }

    [Fact]
    public async Task CitizenHttpSubmissionCommitsReportAndWorkflowWithoutCallingPython()
    {
        var python = new FakePythonClient
        {
            Start = (_, _) => throw new InvalidOperationException("Citizen HTTP request must not call Python.")
        };
        using var host = CreateHost(python);
        using var client = host.CreateClient();
        var email = $"submission_{Guid.NewGuid():N}@test.local";
        var registration = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest
        {
            FullName = "Submission Test Citizen", Email = email,
            PhoneNumber = "+94771234567", Password = "Password123!"
        });
        registration.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = await registration.Content.ReadFromJsonAsync<AuthResponse>();
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        jsonOptions.Converters.Add(new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false));
        using var submission = new HttpRequestMessage(HttpMethod.Post, "/api/v1/waste-reports")
        {
            Content = JsonContent.Create(new CreateWasteReportRequest
            {
                Description = "Waste near the roadside, awaiting collection",
                WasteType = WasteType.General, Latitude = 6.9271,
                Longitude = 79.8612, AddressText = "Main Street"
            }, options: jsonOptions)
        };
        submission.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        var response = await client.SendAsync(submission).WaitAsync(TimeSpan.FromSeconds(10));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = await response.Content.ReadFromJsonAsync<WasteReportDetailDto>(jsonOptions);
        dto!.Status.Should().Be(WasteReportStatus.Submitted);
        python.StartCalls.Should().Be(0);

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var workflow = await db.AgentWorkflows.AsNoTracking().Include(w => w.Steps)
            .SingleAsync(w => w.TriggeringWasteReportId == dto.Id);
        workflow.TriggerType.Should().Be(AgentWorkflowTriggerType.CitizenReportSubmission);
        workflow.Status.Should().Be(AgentWorkflowStatus.Created);
        workflow.Steps.Should().BeEmpty();
        (await db.WasteReportStatusHistories.CountAsync(h => h.WasteReportId == dto.Id)).Should().Be(1);

        db.AgentWorkflowTransitions.RemoveRange(db.AgentWorkflowTransitions.Where(t => t.WorkflowId == workflow.Id));
        db.AgentWorkflows.RemoveRange(db.AgentWorkflows.Where(w => w.Id == workflow.Id));
        db.WasteReportStatusHistories.RemoveRange(db.WasteReportStatusHistories.Where(h => h.WasteReportId == dto.Id));
        db.WasteReports.RemoveRange(db.WasteReports.Where(r => r.Id == dto.Id));
        await db.SaveChangesAsync();
        await db.Users.Where(u => u.Id == auth.User.Id).ExecuteDeleteAsync();
    }

    [Fact]
    public async Task InitialClaimRunsPlannerAndC1OnceThenClearsLease()
    {
        var python = new FakePythonClient();
        python.Start = (request, _) => Task.FromResult(ValidPause(request));
        using var host = CreateHost(python);
        var seed = await SeedWorkflowAsync(host.Services);
        try
        {
            using var scope = host.Services.CreateScope();
            (await scope.ServiceProvider.GetRequiredService<ReportTriggeredWorkflowProcessor>().ProcessOneAsync(workflowId: seed.WorkflowId)).Should().BeTrue();
            var (workflow, report) = await ReadAsync(host.Services, seed);
            python.StartCalls.Should().Be(1);
            workflow.Status.Should().Be(AgentWorkflowStatus.AwaitingReportVerification);
            workflow.CurrentStep.Should().Be(WorkflowStepType.WasteAnalysis);
            workflow.ProcessingAttemptCount.Should().Be(1);
            workflow.ProcessingLeaseId.Should().BeNull();
            workflow.ProcessingLeaseExpiresAt.Should().BeNull();
            workflow.Steps.OrderBy(s => s.Sequence).Select(s => s.StepType)
                .Should().Equal(WorkflowStepType.SharedPlanning, WorkflowStepType.WasteAnalysis);
            workflow.Transitions.Count(t => t.FromStatus == AgentWorkflowStatus.Created && t.ToStatus == AgentWorkflowStatus.Planning).Should().Be(1);
            workflow.Steps.Should().NotContain(s => s.StepType == WorkflowStepType.CollectionPlanning);
            report.Status.Should().Be(WasteReportStatus.Submitted);
        }
        finally { await CleanupAsync(host.Services, seed); }
    }

    [Fact]
    public async Task VerifiedReportContinuesOnSameWorkflowWithFreshPersistedSnapshotAndC2Only()
    {
        var python = new FakePythonClient { Start = (request, _) => Task.FromResult(ValidPause(request)) };
        using var host = CreateHost(python);
        var seed = await SeedWorkflowAsync(host.Services);
        try
        {
            using (var initialScope = host.Services.CreateScope())
                await initialScope.ServiceProvider.GetRequiredService<ReportTriggeredWorkflowProcessor>()
                    .ProcessOneAsync(workflowId: seed.WorkflowId);

            using (var decisionScope = host.Services.CreateScope())
            {
                var reporting = decisionScope.ServiceProvider.GetRequiredService<IWasteReportService>();
                await reporting.StartReviewAsync(seed.ReportId, seed.UserId, AppRoles.MunicipalManager);
                var verified = await reporting.VerifyAsync(seed.ReportId,
                    new VerifyWasteReportRequest { Priority = WasteReportPriority.High },
                    seed.UserId, AppRoles.MunicipalManager);
                verified.Status.Should().Be(WasteReportStatus.Verified);
                verified.Priority.Should().Be(WasteReportPriority.High);
            }
            python.ResumeCalls.Should().Be(0); // The HTTP/service decision did not invoke Python.
            var (pending, _) = await ReadAsync(host.Services, seed);
            pending.Status.Should().Be(AgentWorkflowStatus.Planning);
            pending.CurrentStep.Should().Be(WorkflowStepType.CollectionPlanning);
            pending.ProcessingAttemptCount.Should().Be(0); // New phase receives a full retry budget.

            python.Resume = (request, _) =>
            {
                request.Workflow.WorkflowId.Should().Be(seed.WorkflowId);
                request.Workflow.TriggeringWasteReportId.Should().Be(seed.ReportId);
                request.Workflow.PlannerResult.Should().NotBeNull();
                request.Workflow.WasteAnalysisResult!.Value.GetProperty("analyses")[0]
                    .GetProperty("reportId").GetGuid().Should().Be(seed.ReportId);
                request.Workflow.Errors!.Value.GetArrayLength().Should().Be(0);
                request.Workflow.Warnings!.Value.GetArrayLength().Should().Be(0);
                request.ResumeContext.Decision.Should().Be("Approved");
                request.ResumeContext.AuthoritativeExecutionSummary.GetProperty("verifiedReportId")
                    .GetGuid().Should().Be(seed.ReportId);
                request.ResumeContext.AuthoritativeExecutionSummary.GetProperty("reportStatus")
                    .GetString().Should().Be("Verified");
                return Task.FromResult(ValidContinuation(request));
            };
            using (var continuationScope = host.Services.CreateScope())
                (await continuationScope.ServiceProvider.GetRequiredService<ReportTriggeredWorkflowProcessor>()
                    .ProcessOneAsync(workflowId: seed.WorkflowId)).Should().BeTrue();

            var (finished, report) = await ReadAsync(host.Services, seed);
            python.StartCalls.Should().Be(1);
            python.ResumeCalls.Should().Be(1);
            report.Status.Should().Be(WasteReportStatus.Verified);
            finished.Status.Should().Be(AgentWorkflowStatus.AwaitingCollectionApproval);
            finished.CurrentStep.Should().Be(WorkflowStepType.CollectionPlanning);
            finished.ProcessingLeaseId.Should().BeNull();
            finished.ProcessingLeaseExpiresAt.Should().BeNull();
            finished.ProcessingAttemptCount.Should().Be(1);
            finished.Steps.OrderBy(s => s.Sequence).Select(s => s.StepType)
                .Should().Equal(WorkflowStepType.SharedPlanning, WorkflowStepType.WasteAnalysis,
                    WorkflowStepType.CollectionPlanning);
            finished.Transitions.Count(t => t.ToStatus == AgentWorkflowStatus.Planning).Should().Be(2);
            finished.Transitions.Count(t => t.ToStatus == AgentWorkflowStatus.AwaitingCollectionApproval).Should().Be(1);
            using var replayScope = host.Services.CreateScope();
            (await replayScope.ServiceProvider.GetRequiredService<ReportTriggeredWorkflowProcessor>()
                .ProcessOneAsync(workflowId: seed.WorkflowId)).Should().BeFalse();
            using var approvalScope = host.Services.CreateScope();
            var approved = await approvalScope.ServiceProvider.GetRequiredService<IAgentWorkflowService>()
                .ApproveCollectionPlanningAsync(seed.WorkflowId,
                    new ApproveCollectionPlanningRequest { ExpectedVersion = finished.Version, Reason = "Approved." },
                    seed.UserId);
            approved.Status.Should().Be(AgentWorkflowStatus.CollectionApproved);
        }
        finally { await CleanupAsync(host.Services, seed); }
    }

    [Fact]
    public async Task RejectedReportAndWorkflowAreNeverClaimedForC2()
    {
        var python = new FakePythonClient { Start = (request, _) => Task.FromResult(ValidPause(request)) };
        using var host = CreateHost(python);
        var seed = await SeedWorkflowAsync(host.Services);
        try
        {
            using (var initialScope = host.Services.CreateScope())
                await initialScope.ServiceProvider.GetRequiredService<ReportTriggeredWorkflowProcessor>()
                    .ProcessOneAsync(workflowId: seed.WorkflowId);
            using (var decisionScope = host.Services.CreateScope())
            {
                var reporting = decisionScope.ServiceProvider.GetRequiredService<IWasteReportService>();
                await reporting.StartReviewAsync(seed.ReportId, seed.UserId, AppRoles.WasteOfficer);
                await reporting.RejectAsync(seed.ReportId,
                    new RejectWasteReportRequest { Reason = "Duplicate report." }, seed.UserId, AppRoles.WasteOfficer);
            }
            using var workerScope = host.Services.CreateScope();
            (await workerScope.ServiceProvider.GetRequiredService<ReportTriggeredWorkflowProcessor>()
                .ProcessOneAsync(workflowId: seed.WorkflowId)).Should().BeFalse();
            var (workflow, report) = await ReadAsync(host.Services, seed);
            report.Status.Should().Be(WasteReportStatus.Rejected);
            workflow.Status.Should().Be(AgentWorkflowStatus.Rejected);
            workflow.Steps.Should().NotContain(s => s.StepType == WorkflowStepType.CollectionPlanning);
            workflow.Steps.Should().NotContain(s => s.StepType == WorkflowStepType.FleetPlanning ||
                s.StepType == WorkflowStepType.OperationalValidation);
            python.ResumeCalls.Should().Be(0);
            using var checkScope = host.Services.CreateScope();
            var db = checkScope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.AgentWorkflowExecutionResults.CountAsync(r => r.WorkflowId == seed.WorkflowId)).Should().Be(0);
        }
        finally { await CleanupAsync(host.Services, seed); }
    }

    [Fact]
    public async Task VerifyAndRejectRaceCommitsExactlyOneAuthoritativeDecision()
    {
        var python = new FakePythonClient { Start = (request, _) => Task.FromResult(ValidPause(request)) };
        using var host = CreateHost(python);
        var seed = await SeedWorkflowAsync(host.Services);
        try
        {
            using (var initialScope = host.Services.CreateScope())
                await initialScope.ServiceProvider.GetRequiredService<ReportTriggeredWorkflowProcessor>()
                    .ProcessOneAsync(workflowId: seed.WorkflowId);
            using (var reviewScope = host.Services.CreateScope())
                await reviewScope.ServiceProvider.GetRequiredService<IWasteReportService>()
                    .StartReviewAsync(seed.ReportId, seed.UserId, AppRoles.WasteOfficer);

            async Task<bool> DecideAsync(bool verify)
            {
                using var scope = host.Services.CreateScope();
                try
                {
                    var reporting = scope.ServiceProvider.GetRequiredService<IWasteReportService>();
                    if (verify)
                        await reporting.VerifyAsync(seed.ReportId,
                            new VerifyWasteReportRequest { Priority = WasteReportPriority.Urgent },
                            seed.UserId, AppRoles.MunicipalManager);
                    else
                        await reporting.RejectAsync(seed.ReportId,
                            new RejectWasteReportRequest { Reason = "Invalid report." },
                            seed.UserId, AppRoles.WasteOfficer);
                    return true;
                }
                catch (BusinessRuleConflictException) { return false; }
            }

            var results = await Task.WhenAll(DecideAsync(true), DecideAsync(false));
            results.Count(value => value).Should().Be(1);
            var (workflow, report) = await ReadAsync(host.Services, seed);
            ((report.Status == WasteReportStatus.Verified && workflow.Status == AgentWorkflowStatus.Planning &&
              workflow.CurrentStep == WorkflowStepType.CollectionPlanning) ||
             (report.Status == WasteReportStatus.Rejected && workflow.Status == AgentWorkflowStatus.Rejected))
                .Should().BeTrue();
            using var checkScope = host.Services.CreateScope();
            var db = checkScope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.WasteReportStatusHistories.CountAsync(h => h.WasteReportId == seed.ReportId &&
                h.FromStatus == WasteReportStatus.UnderReview)).Should().Be(1);
            workflow.Transitions.Count(t => t.FromStatus == AgentWorkflowStatus.AwaitingReportVerification)
                .Should().Be(1);
            python.ResumeCalls.Should().Be(0);
        }
        finally { await CleanupAsync(host.Services, seed); }
    }

    [Fact]
    public async Task CancelDuringC1MakesLateResponseStaleAndTerminal()
    {
        var python = new FakePythonClient();
        using var host = CreateHost(python);
        var seed = await SeedWorkflowAsync(host.Services);
        python.Start = async (request, _) =>
        {
            using var scope = host.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IWasteReportService>()
                .CancelAsync(seed.ReportId, seed.UserId, AppRoles.Citizen);
            return ValidPause(request);
        };
        try
        {
            using var scope = host.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<ReportTriggeredWorkflowProcessor>()
                .ProcessOneAsync(workflowId: seed.WorkflowId);
            var (workflow, report) = await ReadAsync(host.Services, seed);
            report.Status.Should().Be(WasteReportStatus.Cancelled);
            workflow.Status.Should().Be(AgentWorkflowStatus.Rejected);
            workflow.ProcessingLeaseId.Should().BeNull();
            workflow.Steps.Should().BeEmpty();
            python.ResumeCalls.Should().Be(0);
        }
        finally { await CleanupAsync(host.Services, seed); }
    }

    [Fact]
    public async Task C2TransientFailureRetriesSameWorkflowAfterDurableDelay()
    {
        var python = new FakePythonClient { Start = (request, _) => Task.FromResult(ValidPause(request)) };
        using var host = CreateHost(python, maxAttempts: 2);
        var seed = await SeedWorkflowAsync(host.Services);
        try
        {
            using (var scope = host.Services.CreateScope())
                await scope.ServiceProvider.GetRequiredService<ReportTriggeredWorkflowProcessor>()
                    .ProcessOneAsync(workflowId: seed.WorkflowId);
            using (var scope = host.Services.CreateScope())
            {
                var reporting = scope.ServiceProvider.GetRequiredService<IWasteReportService>();
                await reporting.StartReviewAsync(seed.ReportId, seed.UserId, AppRoles.WasteOfficer);
                await reporting.VerifyAsync(seed.ReportId,
                    new VerifyWasteReportRequest { Priority = WasteReportPriority.High },
                    seed.UserId, AppRoles.WasteOfficer);
            }
            python.Resume = (_, _) => throw new AiServiceUnavailableException("Fake outage.");
            using (var scope = host.Services.CreateScope())
                await scope.ServiceProvider.GetRequiredService<ReportTriggeredWorkflowProcessor>()
                    .ProcessOneAsync(workflowId: seed.WorkflowId);
            var (retrying, report) = await ReadAsync(host.Services, seed);
            report.Status.Should().Be(WasteReportStatus.Verified);
            retrying.Status.Should().Be(AgentWorkflowStatus.Planning);
            retrying.ProcessingAttemptCount.Should().Be(1);
            retrying.ProcessingLeaseId.Should().BeNull();
            retrying.ProcessingLeaseExpiresAt.Should().BeAfter(DateTime.UtcNow);
            using (var scope = host.Services.CreateScope())
                (await scope.ServiceProvider.GetRequiredService<ReportTriggeredWorkflowProcessor>()
                    .ProcessOneAsync(workflowId: seed.WorkflowId)).Should().BeFalse();

            using (var scope = host.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await db.AgentWorkflows.Where(w => w.Id == seed.WorkflowId).ExecuteUpdateAsync(update => update
                    .SetProperty(w => w.ProcessingLeaseExpiresAt, DateTime.UtcNow.AddSeconds(-1)));
            }
            python.Resume = (request, _) => Task.FromResult(ValidContinuation(request));
            using (var scope = host.Services.CreateScope())
                await scope.ServiceProvider.GetRequiredService<ReportTriggeredWorkflowProcessor>()
                    .ProcessOneAsync(workflowId: seed.WorkflowId);
            var (completed, verifiedReport) = await ReadAsync(host.Services, seed);
            completed.Status.Should().Be(AgentWorkflowStatus.AwaitingCollectionApproval);
            completed.ProcessingAttemptCount.Should().Be(2);
            completed.Steps.Count(s => s.StepType == WorkflowStepType.CollectionPlanning).Should().Be(1);
            python.ResumeCalls.Should().Be(2);
            verifiedReport.Status.Should().Be(WasteReportStatus.Verified);
        }
        finally { await CleanupAsync(host.Services, seed); }
    }

    [Fact]
    public async Task ConcurrentC2ClaimCallsPythonAndPersistsC2Once()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var python = new FakePythonClient { Start = (request, _) => Task.FromResult(ValidPause(request)) };
        using var host = CreateHost(python);
        var seed = await SeedWorkflowAsync(host.Services);
        try
        {
            await PrepareVerifiedC2Async(host.Services, seed);
            python.Resume = async (request, _) =>
            {
                entered.TrySetResult();
                await release.Task;
                return ValidContinuation(request);
            };
            using var scopeA = host.Services.CreateScope();
            using var scopeB = host.Services.CreateScope();
            var first = scopeA.ServiceProvider.GetRequiredService<ReportTriggeredWorkflowProcessor>()
                .ProcessOneAsync(workflowId: seed.WorkflowId);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            (await scopeB.ServiceProvider.GetRequiredService<ReportTriggeredWorkflowProcessor>()
                .ProcessOneAsync(workflowId: seed.WorkflowId)).Should().BeFalse();
            release.TrySetResult();
            (await first).Should().BeTrue();
            var (workflow, _) = await ReadAsync(host.Services, seed);
            python.ResumeCalls.Should().Be(1);
            workflow.Steps.Count(s => s.StepType == WorkflowStepType.CollectionPlanning).Should().Be(1);
            workflow.Transitions.Count(t => t.ToStatus == AgentWorkflowStatus.AwaitingCollectionApproval).Should().Be(1);
        }
        finally { release.TrySetResult(); await CleanupAsync(host.Services, seed); }
    }

    [Fact]
    public async Task StaleC2ResponseCannotAppendCollectionPlanning()
    {
        var python = new FakePythonClient { Start = (request, _) => Task.FromResult(ValidPause(request)) };
        using var host = CreateHost(python);
        var seed = await SeedWorkflowAsync(host.Services);
        try
        {
            await PrepareVerifiedC2Async(host.Services, seed);
            python.Resume = async (request, _) =>
            {
                using var scope = host.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await db.AgentWorkflows.Where(w => w.Id == seed.WorkflowId).ExecuteUpdateAsync(update => update
                    .SetProperty(w => w.ProcessingLeaseId, Guid.NewGuid())
                    .SetProperty(w => w.Version, w => w.Version + 1));
                return ValidContinuation(request);
            };
            using var workerScope = host.Services.CreateScope();
            await workerScope.ServiceProvider.GetRequiredService<ReportTriggeredWorkflowProcessor>()
                .ProcessOneAsync(workflowId: seed.WorkflowId);
            var (workflow, report) = await ReadAsync(host.Services, seed);
            workflow.Status.Should().Be(AgentWorkflowStatus.Planning);
            workflow.Steps.Count(s => s.StepType == WorkflowStepType.CollectionPlanning).Should().Be(0);
            workflow.ProcessingLeaseId.Should().NotBeNull();
            report.Status.Should().Be(WasteReportStatus.Verified);
        }
        finally { await CleanupAsync(host.Services, seed); }
    }

    [Fact]
    public async Task ExpiredC2LeaseIsReclaimedAfterRestartWithoutRepeatingPlannerOrC1()
    {
        var python = new FakePythonClient { Start = (request, _) => Task.FromResult(ValidPause(request)) };
        using var host = CreateHost(python);
        var seed = await SeedWorkflowAsync(host.Services);
        try
        {
            await PrepareVerifiedC2Async(host.Services, seed);
            using (var scope = host.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await db.AgentWorkflows.Where(w => w.Id == seed.WorkflowId).ExecuteUpdateAsync(update => update
                    .SetProperty(w => w.ProcessingLeaseId, Guid.NewGuid())
                    .SetProperty(w => w.ProcessingLeaseExpiresAt, DateTime.UtcNow.AddMinutes(-1))
                    .SetProperty(w => w.ProcessingAttemptCount, 1)
                    .SetProperty(w => w.Version, w => w.Version + 1));
            }
            python.Resume = (request, _) => Task.FromResult(ValidContinuation(request));
            using (var scope = host.Services.CreateScope())
                await scope.ServiceProvider.GetRequiredService<ReportTriggeredWorkflowProcessor>()
                    .ProcessOneAsync(workflowId: seed.WorkflowId);
            var (workflow, _) = await ReadAsync(host.Services, seed);
            workflow.Status.Should().Be(AgentWorkflowStatus.AwaitingCollectionApproval);
            workflow.ProcessingAttemptCount.Should().Be(2);
            workflow.Steps.Count(s => s.StepType == WorkflowStepType.SharedPlanning).Should().Be(1);
            workflow.Steps.Count(s => s.StepType == WorkflowStepType.WasteAnalysis).Should().Be(1);
            workflow.Steps.Count(s => s.StepType == WorkflowStepType.CollectionPlanning).Should().Be(1);
            python.StartCalls.Should().Be(1);
            python.ResumeCalls.Should().Be(1);
        }
        finally { await CleanupAsync(host.Services, seed); }
    }

    [Fact]
    public async Task ExhaustedC2AttemptsFailWorkflowButKeepReportVerified()
    {
        var python = new FakePythonClient { Start = (request, _) => Task.FromResult(ValidPause(request)) };
        using var host = CreateHost(python, maxAttempts: 2);
        var seed = await SeedWorkflowAsync(host.Services);
        try
        {
            await PrepareVerifiedC2Async(host.Services, seed);
            python.Resume = (_, _) => throw new AiServiceUnavailableException("Fake unavailable.");
            using (var scope = host.Services.CreateScope())
                await scope.ServiceProvider.GetRequiredService<ReportTriggeredWorkflowProcessor>()
                    .ProcessOneAsync(workflowId: seed.WorkflowId);
            using (var scope = host.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await db.AgentWorkflows.Where(w => w.Id == seed.WorkflowId).ExecuteUpdateAsync(update => update
                    .SetProperty(w => w.ProcessingLeaseExpiresAt, DateTime.UtcNow.AddSeconds(-1)));
                await scope.ServiceProvider.GetRequiredService<ReportTriggeredWorkflowProcessor>()
                    .ProcessOneAsync(workflowId: seed.WorkflowId);
            }
            var (workflow, report) = await ReadAsync(host.Services, seed);
            workflow.Status.Should().Be(AgentWorkflowStatus.Failed);
            workflow.ProcessingLeaseId.Should().BeNull();
            workflow.ProcessingAttemptCount.Should().Be(2);
            workflow.Steps.Count(s => s.StepType == WorkflowStepType.CollectionPlanning).Should().Be(0);
            report.Status.Should().Be(WasteReportStatus.Verified);
            python.ResumeCalls.Should().Be(2);
        }
        finally { await CleanupAsync(host.Services, seed); }
    }

    [Theory]
    [InlineData("timeout")]
    [InlineData("conflict")]
    [InlineData("validation")]
    [InlineData("invalid-envelope")]
    public async Task C2FailureKindsLeaveVerifiedReportAndRecoverableWorkflow(string failure)
    {
        var python = new FakePythonClient { Start = (request, _) => Task.FromResult(ValidPause(request)) };
        using var host = CreateHost(python, maxAttempts: 2);
        var seed = await SeedWorkflowAsync(host.Services);
        try
        {
            await PrepareVerifiedC2Async(host.Services, seed);
            python.Resume = (request, _) => failure switch
            {
                "timeout" => throw new TaskCanceledException("Fake HTTP timeout."),
                "conflict" => throw new AiServiceUnavailableException("Fake 409 response."),
                "validation" => throw new AiServiceUnavailableException("Fake safe 422 response."),
                _ => Task.FromResult(new PythonOrchestrationEnvelope
                {
                    WorkflowId = request.Workflow.WorkflowId,
                    Objective = request.Workflow.Objective,
                    TriggerType = AgentWorkflowTriggerType.CitizenReportSubmission,
                    TriggeringWasteReportId = seed.ReportId,
                    Status = "Paused", CurrentPhase = "PausedForReportVerification"
                })
            };
            using var scope = host.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<ReportTriggeredWorkflowProcessor>()
                .ProcessOneAsync(workflowId: seed.WorkflowId);
            var (workflow, report) = await ReadAsync(host.Services, seed);
            report.Status.Should().Be(WasteReportStatus.Verified);
            workflow.Status.Should().Be(AgentWorkflowStatus.Planning);
            workflow.ProcessingLeaseExpiresAt.Should().BeAfter(DateTime.UtcNow);
            workflow.Steps.Count(s => s.StepType == WorkflowStepType.CollectionPlanning).Should().Be(0);
            python.ResumeCalls.Should().Be(1);
        }
        finally { await CleanupAsync(host.Services, seed); }
    }

    [Fact]
    public async Task UnderReviewWhileC1RunsStillAcceptsExactReportPause()
    {
        var python = new FakePythonClient();
        using var host = CreateHost(python);
        var seed = await SeedWorkflowAsync(host.Services);
        python.Start = async (request, _) =>
        {
            using var scope = host.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var report = await db.WasteReports.SingleAsync(r => r.Id == seed.ReportId);
            report.Status = WasteReportStatus.UnderReview;
            await db.SaveChangesAsync();
            return ValidPause(request);
        };
        try
        {
            using var scope = host.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<ReportTriggeredWorkflowProcessor>().ProcessOneAsync(workflowId: seed.WorkflowId);
            var (workflow, report) = await ReadAsync(host.Services, seed);
            workflow.Status.Should().Be(AgentWorkflowStatus.AwaitingReportVerification);
            report.Status.Should().Be(WasteReportStatus.UnderReview);
        }
        finally { await CleanupAsync(host.Services, seed); }
    }

    [Fact]
    public async Task ExpiredPlanningLeaseIsReclaimedWithoutSecondPlanningTransition()
    {
        var python = new FakePythonClient();
        python.Start = (request, _) => Task.FromResult(ValidPause(request));
        using var host = CreateHost(python);
        var seed = await SeedWorkflowAsync(host.Services, expiredPlanning: true);
        try
        {
            using var scope = host.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<ReportTriggeredWorkflowProcessor>().ProcessOneAsync(workflowId: seed.WorkflowId);
            var (workflow, _) = await ReadAsync(host.Services, seed);
            workflow.Status.Should().Be(AgentWorkflowStatus.AwaitingReportVerification);
            workflow.ProcessingAttemptCount.Should().Be(2);
            workflow.Transitions.Count(t => t.FromStatus == AgentWorkflowStatus.Created && t.ToStatus == AgentWorkflowStatus.Planning).Should().Be(1);
            workflow.Steps.Count.Should().Be(2);
        }
        finally { await CleanupAsync(host.Services, seed); }
    }

    [Fact]
    public async Task ConcurrentProcessorsCannotCallPythonTwice()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var python = new FakePythonClient();
        python.Start = async (request, _) =>
        {
            entered.TrySetResult();
            await release.Task;
            return ValidPause(request);
        };
        using var host = CreateHost(python);
        var seed = await SeedWorkflowAsync(host.Services);
        try
        {
            using var scopeA = host.Services.CreateScope();
            using var scopeB = host.Services.CreateScope();
            var first = scopeA.ServiceProvider.GetRequiredService<ReportTriggeredWorkflowProcessor>().ProcessOneAsync(workflowId: seed.WorkflowId);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            (await scopeB.ServiceProvider.GetRequiredService<ReportTriggeredWorkflowProcessor>().ProcessOneAsync(workflowId: seed.WorkflowId)).Should().BeFalse();
            release.TrySetResult();
            (await first).Should().BeTrue();
            var (workflow, _) = await ReadAsync(host.Services, seed);
            python.StartCalls.Should().Be(1);
            workflow.Steps.Count.Should().Be(2);
            workflow.Transitions.Count(t => t.ToStatus == AgentWorkflowStatus.Planning).Should().Be(1);
        }
        finally { release.TrySetResult(); await CleanupAsync(host.Services, seed); }
    }

    [Fact]
    public async Task StaleLeaseResponseCannotPersistPlannerOrC1()
    {
        var python = new FakePythonClient();
        using var host = CreateHost(python);
        var seed = await SeedWorkflowAsync(host.Services);
        python.Start = async (request, _) =>
        {
            using var scope = host.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.AgentWorkflows.Where(w => w.Id == seed.WorkflowId).ExecuteUpdateAsync(update => update
                .SetProperty(w => w.ProcessingLeaseId, Guid.NewGuid())
                .SetProperty(w => w.Version, w => w.Version + 1));
            return ValidPause(request);
        };
        try
        {
            using var scope = host.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<ReportTriggeredWorkflowProcessor>().ProcessOneAsync(workflowId: seed.WorkflowId);
            var (workflow, report) = await ReadAsync(host.Services, seed);
            workflow.Status.Should().Be(AgentWorkflowStatus.Planning);
            workflow.Steps.Should().BeEmpty();
            workflow.ProcessingLeaseId.Should().NotBeNull();
            report.Status.Should().Be(WasteReportStatus.Submitted);
        }
        finally { await CleanupAsync(host.Services, seed); }
    }

    [Theory]
    [InlineData("unavailable")]
    [InlineData("timeout")]
    [InlineData("invalid-envelope")]
    public async Task TransientFailureRetainsReportAndDurablyDelaysSameWorkflow(string failure)
    {
        var python = new FakePythonClient();
        python.Start = (request, _) => failure switch
        {
            "unavailable" => throw new AiServiceUnavailableException("Test Python unavailable."),
            "timeout" => throw new TaskCanceledException("Test HTTP timeout."),
            _ => Task.FromResult(new PythonOrchestrationEnvelope
            {
                WorkflowId = request.WorkflowId, Objective = request.Objective,
                TriggerType = AgentWorkflowTriggerType.CitizenReportSubmission,
                TriggeringWasteReportId = request.TriggeringWasteReportId,
                Status = "Paused", CurrentPhase = "PausedForCollectionApproval"
            })
        };
        using var host = CreateHost(python, maxAttempts: 2);
        var seed = await SeedWorkflowAsync(host.Services);
        try
        {
            using var scope = host.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<ReportTriggeredWorkflowProcessor>().ProcessOneAsync(workflowId: seed.WorkflowId);
            var (workflow, report) = await ReadAsync(host.Services, seed);
            workflow.Status.Should().Be(AgentWorkflowStatus.Planning);
            workflow.ProcessingAttemptCount.Should().Be(1);
            workflow.ProcessingLeaseId.Should().BeNull();
            workflow.ProcessingLeaseExpiresAt.Should().BeAfter(DateTime.UtcNow);
            workflow.Steps.Should().BeEmpty();
            report.Status.Should().Be(WasteReportStatus.Submitted);
            (await scope.ServiceProvider.GetRequiredService<ReportTriggeredWorkflowProcessor>().ProcessOneAsync(workflowId: seed.WorkflowId)).Should().BeFalse();
        }
        finally { await CleanupAsync(host.Services, seed); }
    }

    [Fact]
    public async Task RetryAfterDurableDelayAndMaxAttemptsFailOnlyWorkflow()
    {
        var python = new FakePythonClient { Start = (_, _) => throw new AiServiceUnavailableException("Test outage.") };
        using var host = CreateHost(python, maxAttempts: 2);
        var seed = await SeedWorkflowAsync(host.Services);
        try
        {
            using (var firstScope = host.Services.CreateScope())
                await firstScope.ServiceProvider.GetRequiredService<ReportTriggeredWorkflowProcessor>().ProcessOneAsync(workflowId: seed.WorkflowId);
            using (var scope = host.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await db.AgentWorkflows.Where(w => w.Id == seed.WorkflowId).ExecuteUpdateAsync(update => update
                    .SetProperty(w => w.ProcessingLeaseExpiresAt, DateTime.UtcNow.AddSeconds(-1)));
                await scope.ServiceProvider.GetRequiredService<ReportTriggeredWorkflowProcessor>().ProcessOneAsync(workflowId: seed.WorkflowId);
            }
            var (workflow, report) = await ReadAsync(host.Services, seed);
            python.StartCalls.Should().Be(2);
            workflow.ProcessingAttemptCount.Should().Be(2);
            workflow.Status.Should().Be(AgentWorkflowStatus.Failed);
            workflow.ProcessingLeaseId.Should().BeNull();
            workflow.ProcessingLeaseExpiresAt.Should().BeNull();
            workflow.Steps.Should().BeEmpty();
            report.Status.Should().Be(WasteReportStatus.Submitted);
            workflow.Transitions.Count(t => t.ToStatus == AgentWorkflowStatus.Failed).Should().Be(1);
        }
        finally { await CleanupAsync(host.Services, seed); }
    }

    [Fact]
    public async Task ManualWorkflowIsNeverClaimed()
    {
        var python = new FakePythonClient { Start = (_, _) => throw new InvalidOperationException("Manual path must not run.") };
        using var host = CreateHost(python);
        var seed = await SeedWorkflowAsync(host.Services, manual: true);
        try
        {
            using var scope = host.Services.CreateScope();
            (await scope.ServiceProvider.GetRequiredService<ReportTriggeredWorkflowProcessor>().ProcessOneAsync(workflowId: seed.WorkflowId)).Should().BeFalse();
            python.StartCalls.Should().Be(0);
        }
        finally { await CleanupAsync(host.Services, seed); }
    }
}
