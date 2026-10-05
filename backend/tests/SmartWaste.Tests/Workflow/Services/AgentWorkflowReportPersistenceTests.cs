using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SmartWaste.Application.Common.Exceptions;
using SmartWaste.Application.Workflow.DTOs.Transport;
using SmartWaste.Application.Workflow.Services;
using SmartWaste.Domain.Entities;
using SmartWaste.Domain.Common;
using SmartWaste.Domain.Reporting.Entities;
using SmartWaste.Domain.Reporting.Enums;
using SmartWaste.Domain.Workflow.Entities;
using SmartWaste.Domain.Workflow.Enums;
using SmartWaste.Infrastructure.Persistence;
using SmartWaste.Infrastructure.Workflow.Services;

namespace SmartWaste.Tests.Workflow.Services;

public sealed class AgentWorkflowReportPersistenceTests
{
    private const string Objective = "Analyze this submitted report before authorized collection planning.";

    private static AppDbContext NewDb() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);

    private static AgentWorkflowService NewService(AppDbContext db) => new(
        db, new AgentWorkflowStateMachine(), NullLogger<AgentWorkflowService>.Instance);

    private static async Task<(AgentWorkflow Workflow, WasteReport Report)> SeedAsync(AppDbContext db, AgentWorkflowTriggerType trigger = AgentWorkflowTriggerType.CitizenReportSubmission)
    {
        var user = new AppUser { Id = Guid.NewGuid(), UserName = "reporter", Email = "reporter@test.local", FullName = "Reporter", IsActive = true, CreatedAt = DateTime.UtcNow };
        var report = new WasteReport { Id = Guid.NewGuid(), CitizenId = user.Id, Description = "Overflowing waste", Status = WasteReportStatus.Submitted, CreatedAt = DateTime.UtcNow };
        var workflow = new AgentWorkflow
        {
            Id = Guid.NewGuid(), Objective = Objective, InitiatedByUserId = user.Id,
            TriggerType = trigger,
            TriggeringWasteReportId = trigger == AgentWorkflowTriggerType.CitizenReportSubmission ? report.Id : null,
            Status = AgentWorkflowStatus.Planning, CurrentStep = WorkflowStepType.SharedPlanning,
            Version = 2, CreatedAt = DateTime.UtcNow
        };
        db.Users.Add(user);
        db.WasteReports.Add(report);
        db.AgentWorkflows.Add(workflow);
        await db.SaveChangesAsync();
        return (workflow, report);
    }

    private static JsonElement Planner(string[]? warnings = null) => JsonSerializer.SerializeToElement(new
    {
        objective = Objective,
        steps = new[]
        {
            new { stepId = "step-1", specialist = "WasteAnalysis", objective = "Analyze the exact report.", dependsOn = Array.Empty<string>(), sequence = 1 },
            new { stepId = "step-2", specialist = "CollectionPlanning", objective = "Plan current collection needs.", dependsOn = new[] { "step-1" }, sequence = 2 },
            new { stepId = "step-3", specialist = "FleetRoute", objective = "Prepare routes after approval.", dependsOn = new[] { "step-2" }, sequence = 3 },
            new { stepId = "step-4", specialist = "ValidationOperations", objective = "Validate route proposals.", dependsOn = new[] { "step-3" }, sequence = 4 }
        },
        summary = "Deterministic four-specialist plan.", warnings = warnings ?? Array.Empty<string>(),
        agentName = "shared_planner_agent", modelName = "test", advisoryOnly = true
    });

    private static JsonElement C1(Guid reportId) => JsonSerializer.SerializeToElement(new
    {
        objective = "Analyze the exact report.",
        analyses = new[]
        {
            new { reportId, categoryAssessment = "Mixed roadside waste", recommendedPriority = "Medium",
                operationalConcerns = Array.Empty<string>(), recommendedHandling = "Standard collection",
                confidence = "Medium", rationale = "Citizen report warrants authorized review." }
        },
        sourcePage = 1, sourcePageSize = 1, sourceTotalCount = 1,
        agentName = "waste_analysis_agent", modelName = "test", status = "completed"
    });

    private static JsonElement C2() => JsonSerializer.SerializeToElement(new
    {
        objective = "Plan current collection needs.", candidateGroups = Array.Empty<object>(),
        separateHandling = Array.Empty<object>(), deferredNeeds = Array.Empty<object>(),
        warnings = Array.Empty<string>(), sourcePage = 1, sourcePageSize = 20,
        sourceTotalCount = 0, sourceTotalPages = 0, retrievedPages = new[] { 1 },
        isCompleteSnapshot = true, agentName = "collection_planning_agent", modelName = "test",
        advisoryOnly = true, status = "empty"
    });

    private static PythonOrchestrationEnvelope Pause(AgentWorkflow workflow, Guid reportId,
        string phase = "PausedForReportVerification", Guid? analysisId = null,
        bool includeC2 = false, bool includePlanner = true, bool includeC1 = true,
        AgentWorkflowTriggerType trigger = AgentWorkflowTriggerType.CitizenReportSubmission,
        string[]? warnings = null) => new()
    {
        WorkflowId = workflow.Id, Objective = workflow.Objective,
        TriggerType = trigger, TriggeringWasteReportId = reportId,
        Status = "Paused", CurrentPhase = phase, ApprovalStage = "ReportVerification",
        PauseReason = "Awaiting authorized report verification.",
        PlannerResult = includePlanner ? Planner(warnings) : null,
        WasteAnalysisResult = includeC1 ? C1(analysisId ?? reportId) : null,
        CollectionPlanningResult = includeC2 ? C2() : null,
        CompletedSpecialists = ["WasteAnalysis"],
        Errors = JsonSerializer.SerializeToElement(Array.Empty<object>()),
        Warnings = JsonSerializer.SerializeToElement(warnings ?? Array.Empty<string>())
    };

    private static PythonOrchestrationEnvelope Continuation(AgentWorkflow workflow, Guid reportId) => new()
    {
        WorkflowId = workflow.Id, Objective = workflow.Objective,
        TriggerType = AgentWorkflowTriggerType.CitizenReportSubmission, TriggeringWasteReportId = reportId,
        Status = "Paused", CurrentPhase = "PausedForCollectionApproval", ApprovalStage = "CollectionPlanning",
        PauseReason = "Awaiting authorized collection approval.", PlannerResult = Planner(),
        WasteAnalysisResult = C1(reportId), CollectionPlanningResult = C2(),
        CompletedSpecialists = ["WasteAnalysis", "CollectionPlanning"],
        Errors = JsonSerializer.SerializeToElement(Array.Empty<object>()),
        Warnings = JsonSerializer.SerializeToElement(Array.Empty<string>())
    };

    private static async Task CommitVerifiedDecisionAsync(AppDbContext db, AgentWorkflow workflow, WasteReport report)
    {
        report.Status = WasteReportStatus.Verified;
        workflow.Status = AgentWorkflowStatus.Planning;
        workflow.CurrentStep = WorkflowStepType.CollectionPlanning;
        workflow.ProcessingAttemptCount = 0;
        workflow.Version++;
        db.AgentWorkflowTransitions.Add(new AgentWorkflowTransition
        {
            Id = Guid.NewGuid(), WorkflowId = workflow.Id,
            FromStatus = AgentWorkflowStatus.AwaitingReportVerification,
            ToStatus = AgentWorkflowStatus.Planning,
            Reason = "Test authoritative report verification.", ChangedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task ReportPausePersistsOnlyPlannerAndExactC1Atomically()
    {
        using var db = NewDb();
        var (workflow, report) = await SeedAsync(db);
        var service = NewService(db);
        var start = await service.BuildPythonStartRequestAsync(workflow.Id, 2);
        start.TriggerType.Should().Be(AgentWorkflowTriggerType.CitizenReportSubmission);
        start.TriggeringWasteReportId.Should().Be(report.Id);

        var persisted = await service.PersistReportVerificationPauseAsync(workflow.Id, Pause(workflow, report.Id), 2);
        persisted.Status.Should().Be(AgentWorkflowStatus.AwaitingReportVerification);
        persisted.CurrentStep.Should().Be(WorkflowStepType.WasteAnalysis);
        persisted.Version.Should().Be(3);
        var steps = await db.AgentWorkflowSteps.Where(s => s.WorkflowId == workflow.Id).OrderBy(s => s.Sequence).ToListAsync();
        steps.Select(s => s.StepType).Should().Equal(WorkflowStepType.SharedPlanning, WorkflowStepType.WasteAnalysis);
        steps.All(s => s.Status == WorkflowStepStatus.Completed && s.OutputJson != null).Should().BeTrue();
        (await db.AgentWorkflowTransitions.SingleAsync(t => t.WorkflowId == workflow.Id)).ToStatus
            .Should().Be(AgentWorkflowStatus.AwaitingReportVerification);
        var detail = await service.GetWorkflowDetailsAsync(workflow.Id, workflow.InitiatedByUserId, AppRoles.WasteOfficer);
        detail.TriggerType.Should().Be(AgentWorkflowTriggerType.CitizenReportSubmission);
        detail.TriggeringWasteReportId.Should().Be(report.Id);
        detail.ReportReference.Should().Be(report.Id.ToString("N")[..8].ToUpperInvariant());
        var replay = async () => await service.PersistReportVerificationPauseAsync(workflow.Id, Pause(workflow, report.Id), 3);
        await replay.Should().ThrowAsync<BusinessRuleConflictException>();
        (await db.AgentWorkflowSteps.CountAsync(s => s.WorkflowId == workflow.Id)).Should().Be(2);
    }

    [Theory]
    [InlineData("wrong-phase")]
    [InlineData("wrong-report")]
    [InlineData("extra-c2")]
    [InlineData("missing-planner")]
    [InlineData("missing-c1")]
    [InlineData("wrong-trigger")]
    public async Task InvalidReportPauseDoesNotPersistAnyStepOrTransition(string invalid)
    {
        using var db = NewDb();
        var (workflow, report) = await SeedAsync(db);
        var envelope = Pause(workflow, report.Id,
            phase: invalid == "wrong-phase" ? "PausedForCollectionApproval" : "PausedForReportVerification",
            analysisId: invalid == "wrong-report" ? Guid.NewGuid() : null,
            includeC2: invalid == "extra-c2", includePlanner: invalid != "missing-planner",
            includeC1: invalid != "missing-c1",
            trigger: invalid == "wrong-trigger" ? AgentWorkflowTriggerType.ManualOperationalPlanning : AgentWorkflowTriggerType.CitizenReportSubmission);
        var service = NewService(db);
        var act = async () => await service.PersistReportVerificationPauseAsync(workflow.Id, envelope, 2);
        await act.Should().ThrowAsync<AiServiceUnavailableException>();
        (await db.AgentWorkflowSteps.CountAsync(s => s.WorkflowId == workflow.Id)).Should().Be(0);
        (await db.AgentWorkflowTransitions.CountAsync(t => t.WorkflowId == workflow.Id)).Should().Be(0);
        (await db.AgentWorkflows.SingleAsync(w => w.Id == workflow.Id)).Status.Should().Be(AgentWorkflowStatus.Planning);
    }

    [Fact]
    public async Task VerifiedReportResumesFromPersistedSnapshotAndAddsC2Once()
    {
        using var db = NewDb();
        var (workflow, report) = await SeedAsync(db);
        var service = NewService(db);
        await service.PersistReportVerificationPauseAsync(workflow.Id, Pause(workflow, report.Id), 2);
        var premature = async () => await service.BuildReportVerificationResumeRequestAsync(workflow.Id, 3);
        await premature.Should().ThrowAsync<BusinessRuleConflictException>();
        await CommitVerifiedDecisionAsync(db, workflow, report);

        var request = await service.BuildReportVerificationResumeRequestAsync(workflow.Id, 4);
        using var wire = JsonDocument.Parse(JsonSerializer.Serialize(request));
        var snapshot = wire.RootElement.GetProperty("workflow");
        snapshot.GetProperty("currentPhase").GetString().Should().Be("PausedForReportVerification");
        snapshot.GetProperty("errors").GetArrayLength().Should().Be(0);
        snapshot.GetProperty("warnings").GetArrayLength().Should().Be(0);
        snapshot.GetProperty("plannerResult").ValueKind.Should().Be(JsonValueKind.Object);
        snapshot.GetProperty("wasteAnalysisResult").GetProperty("analyses")[0].GetProperty("reportId").GetGuid().Should().Be(report.Id);
        snapshot.GetProperty("collectionPlanningResult").ValueKind.Should().Be(JsonValueKind.Null);
        request.ResumeContext.AuthoritativeExecutionSummary.GetProperty("reportStatus").GetString().Should().Be("Verified");

        var persisted = await service.PersistReportVerificationContinuationAsync(workflow.Id, Continuation(workflow, report.Id), 4);
        persisted.Status.Should().Be(AgentWorkflowStatus.AwaitingCollectionApproval);
        persisted.CurrentStep.Should().Be(WorkflowStepType.CollectionPlanning);
        var steps = await db.AgentWorkflowSteps.Where(s => s.WorkflowId == workflow.Id).OrderBy(s => s.Sequence).ToListAsync();
        steps.Select(s => s.StepType).Should().Equal(WorkflowStepType.SharedPlanning, WorkflowStepType.WasteAnalysis, WorkflowStepType.CollectionPlanning);
        (await db.AgentWorkflowTransitions.Where(t => t.WorkflowId == workflow.Id).OrderBy(t => t.ChangedAt).Select(t => t.ToStatus).ToListAsync())
            .Should().Equal(AgentWorkflowStatus.AwaitingReportVerification, AgentWorkflowStatus.Planning, AgentWorkflowStatus.AwaitingCollectionApproval);
        var detail = await service.GetWorkflowDetailsAsync(workflow.Id, workflow.InitiatedByUserId, AppRoles.WasteOfficer);
        detail.ReportReference.Should().Be(report.Id.ToString("N")[..8].ToUpperInvariant());
        var replay = async () => await service.PersistReportVerificationContinuationAsync(workflow.Id, Continuation(workflow, report.Id), 4);
        await replay.Should().ThrowAsync<BusinessRuleConflictException>();
        (await db.AgentWorkflowSteps.CountAsync(s => s.WorkflowId == workflow.Id && s.StepType == WorkflowStepType.CollectionPlanning)).Should().Be(1);
    }

    [Fact]
    public async Task ExpectedVersionAndLeaseRejectStaleResponsesWithoutWrites()
    {
        using var db = NewDb();
        var (workflow, report) = await SeedAsync(db);
        workflow.ProcessingLeaseId = Guid.NewGuid();
        await db.SaveChangesAsync();
        var service = NewService(db);
        var badLease = async () => await service.PersistReportVerificationPauseAsync(workflow.Id, Pause(workflow, report.Id), 2);
        await badLease.Should().ThrowAsync<BusinessRuleConflictException>();
        var badVersion = async () => await service.PersistReportVerificationPauseAsync(workflow.Id, Pause(workflow, report.Id), 1, expectedProcessingLeaseId: workflow.ProcessingLeaseId);
        await badVersion.Should().ThrowAsync<BusinessRuleConflictException>();
        (await db.AgentWorkflowSteps.CountAsync(s => s.WorkflowId == workflow.Id)).Should().Be(0);
    }

    [Fact]
    public async Task ManualWorkflowCannotBuildOrPersistReportTriggerPath()
    {
        using var db = NewDb();
        var (workflow, report) = await SeedAsync(db, AgentWorkflowTriggerType.ManualOperationalPlanning);
        var service = NewService(db);
        var start = await service.BuildPythonStartRequestAsync(workflow.Id, 2);
        start.TriggerType.Should().Be(AgentWorkflowTriggerType.ManualOperationalPlanning);
        start.TriggeringWasteReportId.Should().BeNull();
        var persist = async () => await service.PersistReportVerificationPauseAsync(workflow.Id, Pause(workflow, report.Id), 2);
        await persist.Should().ThrowAsync<BusinessRuleConflictException>();
        var resume = async () => await service.BuildReportVerificationResumeRequestAsync(workflow.Id, 2);
        await resume.Should().ThrowAsync<BusinessRuleConflictException>();
    }

    [Fact]
    public async Task MalformedC2ResponseCannotPartiallyAdvanceReportWorkflow()
    {
        using var db = NewDb();
        var (workflow, report) = await SeedAsync(db);
        var service = NewService(db);
        await service.PersistReportVerificationPauseAsync(workflow.Id, Pause(workflow, report.Id), 2);
        await CommitVerifiedDecisionAsync(db, workflow, report);
        var invalid = new PythonOrchestrationEnvelope
        {
            WorkflowId = workflow.Id, Objective = workflow.Objective,
            TriggerType = AgentWorkflowTriggerType.CitizenReportSubmission,
            TriggeringWasteReportId = report.Id, Status = "Paused",
            CurrentPhase = "PausedForCollectionApproval", ApprovalStage = "CollectionPlanning",
            PauseReason = "Awaiting collection approval.", PlannerResult = Planner(),
            WasteAnalysisResult = C1(report.Id), CollectionPlanningResult = JsonSerializer.SerializeToElement(new { }),
            CompletedSpecialists = ["WasteAnalysis", "CollectionPlanning"],
            Errors = JsonSerializer.SerializeToElement(Array.Empty<object>()),
            Warnings = JsonSerializer.SerializeToElement(Array.Empty<string>())
        };
        var act = async () => await service.PersistReportVerificationContinuationAsync(workflow.Id, invalid, 4);
        await act.Should().ThrowAsync<AiServiceUnavailableException>();
        (await db.AgentWorkflowSteps.CountAsync(s => s.WorkflowId == workflow.Id)).Should().Be(2);
        (await db.AgentWorkflows.SingleAsync(w => w.Id == workflow.Id)).Status.Should().Be(AgentWorkflowStatus.Planning);
    }

    [Fact]
    public async Task ResumeBuilderReconstructsPersistedWarningsAndRejectsDamagedC1()
    {
        using var db = NewDb();
        var (workflow, report) = await SeedAsync(db);
        var service = NewService(db);
        await service.PersistReportVerificationPauseAsync(workflow.Id, Pause(workflow, report.Id, warnings: ["Review access conditions."]), 2);
        await CommitVerifiedDecisionAsync(db, workflow, report);
        var request = await service.BuildReportVerificationResumeRequestAsync(workflow.Id, 4);
        request.Workflow.Warnings!.Value[0].GetString().Should().Be("Review access conditions.");
        request.Workflow.Errors!.Value.GetArrayLength().Should().Be(0);

        var c1 = await db.AgentWorkflowSteps.SingleAsync(s => s.WorkflowId == workflow.Id && s.StepType == WorkflowStepType.WasteAnalysis);
        c1.OutputJson = C1(Guid.NewGuid()).GetRawText();
        await db.SaveChangesAsync();
        var invalid = async () => await service.BuildReportVerificationResumeRequestAsync(workflow.Id, 4);
        await invalid.Should().ThrowAsync<BusinessRuleConflictException>();
    }
}
