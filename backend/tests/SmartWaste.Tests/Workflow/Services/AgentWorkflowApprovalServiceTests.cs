using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SmartWaste.Application.Common.Exceptions;
using SmartWaste.Application.Workflow.DTOs.Requests;
using SmartWaste.Application.Workflow.Services;
using SmartWaste.Domain.Entities;
using SmartWaste.Domain.Workflow.Entities;
using SmartWaste.Domain.Workflow.Enums;
using SmartWaste.Infrastructure.Persistence;
using SmartWaste.Infrastructure.Workflow.Services;
using Xunit;

namespace SmartWaste.Tests.Workflow.Services;

public class AgentWorkflowApprovalServiceTests
{
    private static AppDbContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new AppDbContext(options);
    }

    private static AgentWorkflowService CreateService(AppDbContext db, TimeProvider? clock = null)
    {
        var stateMachine = new AgentWorkflowStateMachine();
        var logger = NullLogger<AgentWorkflowService>.Instance;
        return new AgentWorkflowService(db, stateMachine, logger, timeProvider: clock);
    }

    private static (Guid workflowId, Guid stepId, Guid managerId) SeedWorkflowWithStep(
        AppDbContext db,
        AgentWorkflowStatus status,
        WorkflowStepType stepType,
        string outputJson,
        int version = 1,
        AgentWorkflowTriggerType trigger = AgentWorkflowTriggerType.ManualOperationalPlanning)
    {
        var managerId = Guid.NewGuid();
        db.Users.Add(new AppUser
        {
            Id = managerId,
            UserName = "manager",
            Email = "manager@smartwaste.lk",
            FullName = "Municipal Manager",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });

        var workflow = new AgentWorkflow
        {
            Id = Guid.NewGuid(),
            Objective = "Test workflow for approval gates",
            Status = status,
            TriggerType = trigger,
            TriggeringWasteReportId = trigger == AgentWorkflowTriggerType.CitizenReportSubmission ? Guid.NewGuid() : null,
            CurrentStep = stepType,
            InitiatedByUserId = managerId,
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

        return (workflow.Id, step.Id, managerId);
    }

    // =========================================================================
    // Gate 1: Collection Planning Approval
    // =========================================================================

    private sealed class FixedTimeProvider(DateTimeOffset instant) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => instant;
    }

    private static string CanonicalC2Schedule(string scheduledAt) => JsonSerializer.Serialize(new
    {
        status = "completed",
        isCompleteSnapshot = true,
        candidateGroups = new[]
        {
            new
            {
                groupId = "group-1",
                proposedSchedule = new { scheduledAt, schedulingReason = "Municipal collection review." },
                needReferences = new[] { new { needId = Guid.NewGuid(), targetType = "Report", collectionReason = "VerifiedReport" } }
            }
        },
        separateHandling = Array.Empty<object>(),
        deferredNeeds = Array.Empty<object>()
    });

    [Theory]
    [InlineData(AgentWorkflowTriggerType.ManualOperationalPlanning)]
    [InlineData(AgentWorkflowTriggerType.CitizenReportSubmission)]
    public async Task CollectionApproval_ValidMunicipalTime_PreservesExistingTransition(AgentWorkflowTriggerType trigger)
    {
        using var db = CreateContext(Guid.NewGuid().ToString("N"));
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 10, 5, 16, 30, 0, TimeSpan.Zero));
        var sut = CreateService(db, clock);
        var (workflowId, _, managerId) = SeedWorkflowWithStep(db,
            AgentWorkflowStatus.AwaitingCollectionApproval, WorkflowStepType.CollectionPlanning,
            CanonicalC2Schedule("2026-10-06T02:30:00Z"), trigger: trigger);

        var result = await sut.ApproveCollectionPlanningAsync(workflowId,
            new ApproveCollectionPlanningRequest { ExpectedVersion = 1 }, managerId);

        result.Status.Should().Be(AgentWorkflowStatus.CollectionApproved);
        result.Approvals.Should().ContainSingle();
        (await db.CollectionTasks.CountAsync()).Should().Be(0, "approval precedes the existing materialization step");
    }

    [Theory]
    [InlineData(AgentWorkflowTriggerType.ManualOperationalPlanning)]
    [InlineData(AgentWorkflowTriggerType.CitizenReportSubmission)]
    public async Task CollectionApproval_OutOfHoursPlan_LeavesWorkflowAwaitingApproval(AgentWorkflowTriggerType trigger)
    {
        using var db = CreateContext(Guid.NewGuid().ToString("N"));
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 10, 5, 16, 30, 0, TimeSpan.Zero));
        var sut = CreateService(db, clock);
        var (workflowId, _, managerId) = SeedWorkflowWithStep(db,
            AgentWorkflowStatus.AwaitingCollectionApproval, WorkflowStepType.CollectionPlanning,
            CanonicalC2Schedule("2026-10-05T21:00:00Z"), trigger: trigger); // 02:30 local

        var act = () => sut.ApproveCollectionPlanningAsync(workflowId,
            new ApproveCollectionPlanningRequest { ExpectedVersion = 1 }, managerId);
        await act.Should().ThrowAsync<BusinessRuleConflictException>().WithMessage("*municipality timezone*");

        var workflow = await db.AgentWorkflows.Include(w => w.Approvals).Include(w => w.Transitions)
            .FirstAsync(w => w.Id == workflowId);
        workflow.Status.Should().Be(AgentWorkflowStatus.AwaitingCollectionApproval);
        workflow.Version.Should().Be(1);
        workflow.Approvals.Should().BeEmpty();
        workflow.Transitions.Should().BeEmpty();
        (await db.CollectionTasks.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CollectionApproval_InvalidTimeStillAllowsRevisionAndRejection()
    {
        using var db = CreateContext(Guid.NewGuid().ToString("N"));
        var sut = CreateService(db);
        var invalid = CanonicalC2Schedule("2026-10-06T02:30:00+05:30"); // 02:30 local
        var (reviseId, _, managerId) = SeedWorkflowWithStep(db,
            AgentWorkflowStatus.AwaitingCollectionApproval, WorkflowStepType.CollectionPlanning, invalid);
        var (rejectId, _, _) = SeedWorkflowWithStep(db,
            AgentWorkflowStatus.AwaitingCollectionApproval, WorkflowStepType.CollectionPlanning, invalid);

        (await sut.RequestCollectionRevisionAsync(reviseId,
            new RequestCollectionRevisionRequest { ExpectedVersion = 1, Reason = "Collection time is outside operating hours." },
            managerId)).Status.Should().Be(AgentWorkflowStatus.CollectionNeedsRevision);
        (await sut.RejectCollectionPlanningAsync(rejectId,
            new RejectCollectionPlanningRequest { ExpectedVersion = 1, Reason = "Collection time is invalid." },
            managerId)).Status.Should().Be(AgentWorkflowStatus.Rejected);
    }

    [Fact]
    public async Task ApproveCollectionPlanningAsync_ValidProposal_TransitionsAndPersistsApprovalAtomically()
    {
        var dbName = Guid.NewGuid().ToString("N");
        using var db = CreateContext(dbName);
        var sut = CreateService(db);

        var validC2Json = JsonSerializer.Serialize(new
        {
            status = "completed",
            isCompleteSnapshot = true,
            collectionNeeds = new[] { new { taskId = Guid.NewGuid() } }
        });

        var (workflowId, stepId, managerId) = SeedWorkflowWithStep(
            db, AgentWorkflowStatus.AwaitingCollectionApproval, WorkflowStepType.CollectionPlanning, validC2Json, version: 5);

        var request = new ApproveCollectionPlanningRequest
        {
            ExpectedVersion = 5,
            Reason = "Collection schedule verified."
        };

        var result = await sut.ApproveCollectionPlanningAsync(workflowId, request, managerId);

        // Verify DTO response
        result.Status.Should().Be(AgentWorkflowStatus.CollectionApproved);
        result.Version.Should().Be(6, "Version must increment exactly once");
        result.Approvals.Should().HaveCount(1);
        result.Approvals[0].ApprovalStage.Should().Be(WorkflowApprovalStage.CollectionPlanning);
        result.Approvals[0].Decision.Should().Be(WorkflowApprovalDecision.Approved);
        result.Approvals[0].WorkflowStepId.Should().Be(stepId);
        result.Approvals[0].DecisionReason.Should().Be("Collection schedule verified.");
        result.Transitions.Should().HaveCount(1);
        result.Transitions[0].FromStatus.Should().Be(AgentWorkflowStatus.AwaitingCollectionApproval);
        result.Transitions[0].ToStatus.Should().Be(AgentWorkflowStatus.CollectionApproved);

        // Verify DB persistence
        var dbWorkflow = await db.AgentWorkflows
            .Include(w => w.Approvals)
            .Include(w => w.Transitions)
            .FirstAsync(w => w.Id == workflowId);

        dbWorkflow.Status.Should().Be(AgentWorkflowStatus.CollectionApproved);
        dbWorkflow.Version.Should().Be(6);
        dbWorkflow.Approvals.Should().HaveCount(1);
        dbWorkflow.Transitions.Should().HaveCount(1);
    }

    [Fact]
    public async Task ApproveCollectionPlanningAsync_StaleVersion_ThrowsConflict_PersistsNoMutations()
    {
        var dbName = Guid.NewGuid().ToString("N");
        using var db = CreateContext(dbName);
        var sut = CreateService(db);

        var validC2Json = JsonSerializer.Serialize(new { status = "completed", isCompleteSnapshot = true });
        var (workflowId, _, managerId) = SeedWorkflowWithStep(
            db, AgentWorkflowStatus.AwaitingCollectionApproval, WorkflowStepType.CollectionPlanning, validC2Json, version: 5);

        var request = new ApproveCollectionPlanningRequest { ExpectedVersion = 4 };

        var act = () => sut.ApproveCollectionPlanningAsync(workflowId, request, managerId);
        await act.Should().ThrowAsync<BusinessRuleConflictException>()
            .WithMessage("*changed since it was loaded*");

        // Verify DB unchanged
        var dbWorkflow = await db.AgentWorkflows
            .Include(w => w.Approvals)
            .Include(w => w.Transitions)
            .FirstAsync(w => w.Id == workflowId);

        dbWorkflow.Status.Should().Be(AgentWorkflowStatus.AwaitingCollectionApproval);
        dbWorkflow.Version.Should().Be(5);
        dbWorkflow.Approvals.Should().BeEmpty();
        dbWorkflow.Transitions.Should().BeEmpty();
    }

    [Fact]
    public async Task ApproveCollectionPlanningAsync_WrongWorkflowState_ThrowsConflict()
    {
        var dbName = Guid.NewGuid().ToString("N");
        using var db = CreateContext(dbName);
        var sut = CreateService(db);

        var validC2Json = JsonSerializer.Serialize(new { status = "completed", isCompleteSnapshot = true });
        var (workflowId, _, managerId) = SeedWorkflowWithStep(
            db, AgentWorkflowStatus.Planning, WorkflowStepType.CollectionPlanning, validC2Json, version: 1);

        var request = new ApproveCollectionPlanningRequest { ExpectedVersion = 1 };

        var act = () => sut.ApproveCollectionPlanningAsync(workflowId, request, managerId);
        await act.Should().ThrowAsync<InvalidWorkflowTransitionException>();
    }

    [Theory]
    [InlineData(false, "completed")]
    [InlineData(true, "partial")]
    [InlineData(true, "empty")]
    [InlineData(false, "failed")]
    public async Task ApproveCollectionPlanningAsync_IncompleteOrUnreadySnapshot_ThrowsConflict(bool isComplete, string status)
    {
        var dbName = Guid.NewGuid().ToString("N");
        using var db = CreateContext(dbName);
        var sut = CreateService(db);

        var c2Json = JsonSerializer.Serialize(new { status = status, isCompleteSnapshot = isComplete });
        var (workflowId, _, managerId) = SeedWorkflowWithStep(
            db, AgentWorkflowStatus.AwaitingCollectionApproval, WorkflowStepType.CollectionPlanning, c2Json, version: 1);

        var request = new ApproveCollectionPlanningRequest { ExpectedVersion = 1 };

        var act = () => sut.ApproveCollectionPlanningAsync(workflowId, request, managerId);
        await act.Should().ThrowAsync<BusinessRuleConflictException>();

        // Assert atomicity
        var dbWorkflow = await db.AgentWorkflows.Include(w => w.Approvals).FirstAsync(w => w.Id == workflowId);
        dbWorkflow.Approvals.Should().BeEmpty();
        dbWorkflow.Version.Should().Be(1);
    }

    [Fact]
    public async Task ApproveCollectionPlanningAsync_MissingStep_ThrowsConflict()
    {
        var dbName = Guid.NewGuid().ToString("N");
        using var db = CreateContext(dbName);
        var sut = CreateService(db);

        var managerId = Guid.NewGuid();
        var workflow = new AgentWorkflow
        {
            Id = Guid.NewGuid(),
            Status = AgentWorkflowStatus.AwaitingCollectionApproval,
            Version = 1
        };
        db.AgentWorkflows.Add(workflow);
        db.SaveChanges();

        var request = new ApproveCollectionPlanningRequest { ExpectedVersion = 1 };

        var act = () => sut.ApproveCollectionPlanningAsync(workflow.Id, request, managerId);
        await act.Should().ThrowAsync<BusinessRuleConflictException>()
            .WithMessage("*No completed collection planning step*");
    }

    [Fact]
    public async Task RequestCollectionRevisionAsync_Valid_TransitionsToNeedsRevision()
    {
        var dbName = Guid.NewGuid().ToString("N");
        using var db = CreateContext(dbName);
        var sut = CreateService(db);

        // Note: Even with an incomplete snapshot, revision request MUST be permitted
        var c2Json = JsonSerializer.Serialize(new { status = "partial", isCompleteSnapshot = false });
        var (workflowId, stepId, managerId) = SeedWorkflowWithStep(
            db, AgentWorkflowStatus.AwaitingCollectionApproval, WorkflowStepType.CollectionPlanning, c2Json, version: 2);

        var request = new RequestCollectionRevisionRequest
        {
            ExpectedVersion = 2,
            Reason = "Include the northern ward in the morning shift."
        };

        var result = await sut.RequestCollectionRevisionAsync(workflowId, request, managerId);

        result.Status.Should().Be(AgentWorkflowStatus.CollectionNeedsRevision);
        result.Version.Should().Be(3);
        result.Approvals.Should().HaveCount(1);
        result.Approvals[0].Decision.Should().Be(WorkflowApprovalDecision.RevisionRequested);
        result.Approvals[0].WorkflowStepId.Should().Be(stepId);
        result.Approvals[0].DecisionReason.Should().Be("Include the northern ward in the morning shift.");
    }

    [Fact]
    public async Task RejectCollectionPlanningAsync_Valid_TransitionsToRejectedTerminalState()
    {
        var dbName = Guid.NewGuid().ToString("N");
        using var db = CreateContext(dbName);
        var sut = CreateService(db);

        var c2Json = JsonSerializer.Serialize(new { status = "completed", isCompleteSnapshot = true });
        var (workflowId, _, managerId) = SeedWorkflowWithStep(
            db, AgentWorkflowStatus.AwaitingCollectionApproval, WorkflowStepType.CollectionPlanning, c2Json, version: 1);

        var request = new RejectCollectionPlanningRequest
        {
            ExpectedVersion = 1,
            Reason = "Operationally unfeasible due to strikes."
        };

        var result = await sut.RejectCollectionPlanningAsync(workflowId, request, managerId);

        result.Status.Should().Be(AgentWorkflowStatus.Rejected);
        result.CompletedAt.Should().NotBeNull("Rejected is a terminal state");
        result.Version.Should().Be(2);
        result.Approvals[0].Decision.Should().Be(WorkflowApprovalDecision.Rejected);
    }

    // =========================================================================
    // Gate 2: Fleet Dispatch Approval
    // =========================================================================

    [Fact]
    public async Task ApproveDispatchPlanAsync_ReadyForHumanReview_WithoutWarnings_Succeeds()
    {
        var dbName = Guid.NewGuid().ToString("N");
        using var db = CreateContext(dbName);
        var sut = CreateService(db);

        var c4Json = JsonSerializer.Serialize(new
        {
            validationOutcome = "ReadyForHumanReview",
            requiresAcknowledgement = false
        });

        var (workflowId, stepId, managerId) = SeedWorkflowWithStep(
            db, AgentWorkflowStatus.AwaitingDispatchApproval, WorkflowStepType.OperationalValidation, c4Json, version: 10);

        var request = new ApproveDispatchPlanRequest
        {
            ExpectedVersion = 10,
            Reason = "Fleet plan approved."
        };

        var result = await sut.ApproveDispatchPlanAsync(workflowId, request, managerId);

        result.Status.Should().Be(AgentWorkflowStatus.DispatchApproved);
        result.Version.Should().Be(11);
        result.Approvals.Should().HaveCount(1);
        result.Approvals[0].ApprovalStage.Should().Be(WorkflowApprovalStage.FleetDispatch);
        result.Approvals[0].Decision.Should().Be(WorkflowApprovalDecision.Approved);
        result.Approvals[0].WorkflowStepId.Should().Be(stepId);

        // Verify DecisionPayload stores acknowledgement false
        var payloadJson = result.Approvals[0].DecisionPayload?.GetRawText();
        payloadJson.Should().Contain("\"acknowledgeWarnings\":false");
    }

    [Fact]
    public async Task ApproveDispatchPlanAsync_RequiresAcknowledgement_Unacknowledged_ThrowsConflict()
    {
        var dbName = Guid.NewGuid().ToString("N");
        using var db = CreateContext(dbName);
        var sut = CreateService(db);

        var c4Json = JsonSerializer.Serialize(new
        {
            validationOutcome = "ReadyForHumanReview",
            requiresAcknowledgement = true
        });

        var (workflowId, _, managerId) = SeedWorkflowWithStep(
            db, AgentWorkflowStatus.AwaitingDispatchApproval, WorkflowStepType.OperationalValidation, c4Json, version: 10);

        var request = new ApproveDispatchPlanRequest
        {
            ExpectedVersion = 10,
            Reason = "Trying to approve without acknowledging warnings.",
            AcknowledgeWarnings = false
        };

        var act = () => sut.ApproveDispatchPlanAsync(workflowId, request, managerId);
        await act.Should().ThrowAsync<BusinessRuleConflictException>()
            .WithMessage("*must be explicitly acknowledged*");

        // Verify atomicity
        var dbWorkflow = await db.AgentWorkflows.Include(w => w.Approvals).FirstAsync(w => w.Id == workflowId);
        dbWorkflow.Approvals.Should().BeEmpty();
        dbWorkflow.Version.Should().Be(10);
    }

    [Fact]
    public async Task ApproveDispatchPlanAsync_RequiresAcknowledgement_Acknowledged_Succeeds()
    {
        var dbName = Guid.NewGuid().ToString("N");
        using var db = CreateContext(dbName);
        var sut = CreateService(db);

        var c4Json = JsonSerializer.Serialize(new
        {
            validationOutcome = "ReadyForHumanReview",
            requiresAcknowledgement = true
        });

        var (workflowId, _, managerId) = SeedWorkflowWithStep(
            db, AgentWorkflowStatus.AwaitingDispatchApproval, WorkflowStepType.OperationalValidation, c4Json, version: 10);

        var request = new ApproveDispatchPlanRequest
        {
            ExpectedVersion = 10,
            Reason = "Acknowledged compatibility warning with operations supervisor.",
            AcknowledgeWarnings = true
        };

        var result = await sut.ApproveDispatchPlanAsync(workflowId, request, managerId);

        result.Status.Should().Be(AgentWorkflowStatus.DispatchApproved);
        result.Version.Should().Be(11);
        var payloadJson = result.Approvals[0].DecisionPayload?.GetRawText();
        payloadJson.Should().Contain("\"acknowledgeWarnings\":true");
    }

    [Fact]
    public async Task ApproveDispatchPlanAsync_OutcomeIsNeedsRevision_ThrowsConflict()
    {
        var dbName = Guid.NewGuid().ToString("N");
        using var db = CreateContext(dbName);
        var sut = CreateService(db);

        var c4Json = JsonSerializer.Serialize(new
        {
            validationOutcome = "NeedsRevision",
            requiresAcknowledgement = false
        });

        var (workflowId, _, managerId) = SeedWorkflowWithStep(
            db, AgentWorkflowStatus.AwaitingDispatchApproval, WorkflowStepType.OperationalValidation, c4Json, version: 1);

        var request = new ApproveDispatchPlanRequest { ExpectedVersion = 1 };

        var act = () => sut.ApproveDispatchPlanAsync(workflowId, request, managerId);
        await act.Should().ThrowAsync<BusinessRuleConflictException>()
            .WithMessage("*cannot be approved without revision*");
    }

    [Fact]
    public async Task RequestDispatchRevisionAsync_Valid_TransitionsToDispatchNeedsRevision()
    {
        var dbName = Guid.NewGuid().ToString("N");
        using var db = CreateContext(dbName);
        var sut = CreateService(db);

        // Allowed even if C4 is NeedsRevision
        var c4Json = JsonSerializer.Serialize(new { validationOutcome = "NeedsRevision" });
        var (workflowId, stepId, managerId) = SeedWorkflowWithStep(
            db, AgentWorkflowStatus.AwaitingDispatchApproval, WorkflowStepType.OperationalValidation, c4Json, version: 3);

        var request = new RequestDispatchRevisionRequest
        {
            ExpectedVersion = 3,
            Reason = "Re-route vehicle 4 to avoid congested central corridor."
        };

        var result = await sut.RequestDispatchRevisionAsync(workflowId, request, managerId);

        result.Status.Should().Be(AgentWorkflowStatus.DispatchNeedsRevision);
        result.Version.Should().Be(4);
        result.Approvals[0].Decision.Should().Be(WorkflowApprovalDecision.RevisionRequested);
        result.Approvals[0].ApprovalStage.Should().Be(WorkflowApprovalStage.FleetDispatch);
        result.Approvals[0].WorkflowStepId.Should().Be(stepId);
    }

    [Fact]
    public async Task RejectDispatchPlanAsync_Valid_TransitionsToTerminalRejectedState()
    {
        var dbName = Guid.NewGuid().ToString("N");
        using var db = CreateContext(dbName);
        var sut = CreateService(db);

        var c4Json = JsonSerializer.Serialize(new { validationOutcome = "ReadyForHumanReview" });
        var (workflowId, _, managerId) = SeedWorkflowWithStep(
            db, AgentWorkflowStatus.AwaitingDispatchApproval, WorkflowStepType.OperationalValidation, c4Json, version: 5);

        var request = new RejectDispatchPlanRequest
        {
            ExpectedVersion = 5,
            Reason = "Fleet depot shut down due to maintenance."
        };

        var result = await sut.RejectDispatchPlanAsync(workflowId, request, managerId);

        result.Status.Should().Be(AgentWorkflowStatus.Rejected);
        result.CompletedAt.Should().NotBeNull();
        result.Version.Should().Be(6);
        result.Approvals[0].Decision.Should().Be(WorkflowApprovalDecision.Rejected);
    }
}
