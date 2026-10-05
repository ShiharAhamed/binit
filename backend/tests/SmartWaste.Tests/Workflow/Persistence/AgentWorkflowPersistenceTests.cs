using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SmartWaste.Application.Common.Exceptions;
using SmartWaste.Application.Workflow.Services;
using SmartWaste.Domain.Entities;
using SmartWaste.Domain.Workflow.Entities;
using SmartWaste.Domain.Workflow.Enums;
using SmartWaste.Infrastructure.Persistence;
using SmartWaste.Infrastructure.Workflow.Services;
using Xunit;

namespace SmartWaste.Tests.Workflow.Persistence;

public class AgentWorkflowPersistenceTests
{
    private static AppDbContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new AppDbContext(options);
    }

    private static AgentWorkflowService CreateService(AppDbContext db)
    {
        var stateMachine = new AgentWorkflowStateMachine();
        var logger = NullLogger<AgentWorkflowService>.Instance;
        return new AgentWorkflowService(db, stateMachine, logger);
    }

    private static void SeedUser(AppDbContext db, Guid userId, string email = "officer@smartwaste.lk")
    {
        db.Users.Add(new AppUser
        {
            Id = userId,
            UserName = email,
            Email = email,
            FullName = "Test Officer",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        db.SaveChanges();
    }

    // =========================================================================
    // 1. Initial Workflow Creation & Audit (Section 24, 33)
    // =========================================================================

    [Fact]
    public async Task CreateWorkflowAsync_PersistsWorkflow_AndInitialTransitionAudit()
    {
        var dbName = Guid.NewGuid().ToString("N");
        using var db = CreateContext(dbName);
        var sut = CreateService(db);
        var userId = Guid.NewGuid();
        SeedUser(db, userId);

        var objective = "  Plan morning urgent collection needs and formulate fleet routes.  ";
        var workflow = await sut.CreateWorkflowAsync(objective, userId);

        workflow.Should().NotBeNull();
        workflow.Objective.Should().Be("Plan morning urgent collection needs and formulate fleet routes.");
        workflow.Status.Should().Be(AgentWorkflowStatus.Created);
        workflow.CurrentStep.Should().Be(WorkflowStepType.None);
        workflow.InitiatedByUserId.Should().Be(userId);
        workflow.CompletedAt.Should().BeNull();
        workflow.Version.Should().Be(1);

        // Check database persistence
        var persisted = await sut.GetWorkflowByIdAsync(workflow.Id, includeDetails: true);
        persisted.Should().NotBeNull();
        persisted!.Transitions.Should().HaveCount(1);

        var initialTransition = persisted.Transitions.First();
        initialTransition.FromStatus.Should().BeNull();
        initialTransition.ToStatus.Should().Be(AgentWorkflowStatus.Created);
        initialTransition.ChangedByUserId.Should().Be(userId);
        initialTransition.Reason.Should().Be("Workflow created.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1234")]
    public async Task CreateWorkflowAsync_InvalidObjective_ThrowsArgumentException(string invalidObjective)
    {
        using var db = CreateContext(Guid.NewGuid().ToString("N"));
        var sut = CreateService(db);

        var act = () => sut.CreateWorkflowAsync(invalidObjective, Guid.NewGuid());
        await act.Should().ThrowAsync<ArgumentException>();
    }

    // =========================================================================
    // 2. Transition Audit & Concurrency (Section 33, 22, 25)
    // =========================================================================

    [Fact]
    public async Task TransitionAsync_ValidTransition_UpdatesStatus_AndAppendsAuditRecord()
    {
        var dbName = Guid.NewGuid().ToString("N");
        using var db = CreateContext(dbName);
        var sut = CreateService(db);
        var userId = Guid.NewGuid();
        SeedUser(db, userId);

        var workflow = await sut.CreateWorkflowAsync("Valid workflow objective", userId);

        var updated = await sut.TransitionAsync(
            workflow.Id,
            AgentWorkflowStatus.Planning,
            reason: "Starting Shared Planning phase.",
            changedByUserId: userId);

        updated.Status.Should().Be(AgentWorkflowStatus.Planning);
        updated.UpdatedAt.Should().NotBeNull();
        updated.Version.Should().Be(2);

        var reloaded = await sut.GetWorkflowByIdAsync(workflow.Id, includeDetails: true);
        reloaded!.Transitions.Should().HaveCount(2);

        var secondTransition = reloaded.Transitions.Last();
        secondTransition.FromStatus.Should().Be(AgentWorkflowStatus.Created);
        secondTransition.ToStatus.Should().Be(AgentWorkflowStatus.Planning);
        secondTransition.Reason.Should().Be("Starting Shared Planning phase.");
        secondTransition.ChangedByUserId.Should().Be(userId);
    }

    [Fact]
    public async Task TransitionAsync_IllegalTransition_Throws_LeavesStatusUnchanged_NoAuditRecord()
    {
        var dbName = Guid.NewGuid().ToString("N");
        using var db = CreateContext(dbName);
        var sut = CreateService(db);
        var userId = Guid.NewGuid();
        SeedUser(db, userId);

        var workflow = await sut.CreateWorkflowAsync("Valid workflow objective", userId);

        var act = () => sut.TransitionAsync(workflow.Id, AgentWorkflowStatus.DispatchApproved, "Illegal jump", userId);
        await act.Should().ThrowAsync<InvalidWorkflowTransitionException>();

        // Status must remain unchanged, and no transition history record added
        var reloaded = await sut.GetWorkflowByIdAsync(workflow.Id, includeDetails: true);
        reloaded!.Status.Should().Be(AgentWorkflowStatus.Created);
        reloaded.Transitions.Should().HaveCount(1);
    }

    // =========================================================================
    // 3. Terminal Timestamps (Section 34, 23)
    // =========================================================================

    [Fact]
    public async Task TransitionAsync_ToTerminalStates_SetsCompletedAt()
    {
        var dbName = Guid.NewGuid().ToString("N");
        using var db = CreateContext(dbName);
        var sut = CreateService(db);
        var userId = Guid.NewGuid();

        // 1. Completed
        var wf1 = await sut.CreateWorkflowAsync("Workflow 1", userId);
        await sut.TransitionAsync(wf1.Id, AgentWorkflowStatus.Planning);
        await sut.TransitionAsync(wf1.Id, AgentWorkflowStatus.AwaitingCollectionApproval);
        await sut.TransitionAsync(wf1.Id, AgentWorkflowStatus.CollectionApproved);
        await sut.TransitionAsync(wf1.Id, AgentWorkflowStatus.CreatingScheduledTasks);
        await sut.TransitionAsync(wf1.Id, AgentWorkflowStatus.FleetPlanning);
        await sut.TransitionAsync(wf1.Id, AgentWorkflowStatus.OperationalValidation);
        await sut.TransitionAsync(wf1.Id, AgentWorkflowStatus.AwaitingDispatchApproval);
        await sut.TransitionAsync(wf1.Id, AgentWorkflowStatus.DispatchApproved);
        await sut.TransitionAsync(wf1.Id, AgentWorkflowStatus.ExecutingAssignments);
        var completed = await sut.TransitionAsync(wf1.Id, AgentWorkflowStatus.Completed);

        completed.CompletedAt.Should().NotBeNull();
        completed.CompletedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        // 2. Rejected
        var wf2 = await sut.CreateWorkflowAsync("Workflow 2", userId);
        await sut.TransitionAsync(wf2.Id, AgentWorkflowStatus.Planning);
        await sut.TransitionAsync(wf2.Id, AgentWorkflowStatus.AwaitingCollectionApproval);
        var rejected = await sut.TransitionAsync(wf2.Id, AgentWorkflowStatus.Rejected);

        rejected.CompletedAt.Should().NotBeNull();

        // 3. Failed
        var wf3 = await sut.CreateWorkflowAsync("Workflow 3", userId);
        await sut.TransitionAsync(wf3.Id, AgentWorkflowStatus.Planning);
        var failed = await sut.TransitionAsync(wf3.Id, AgentWorkflowStatus.Failed);

        failed.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task TransitionAsync_IntermediateTransitions_DoNotSetCompletedAt()
    {
        var dbName = Guid.NewGuid().ToString("N");
        using var db = CreateContext(dbName);
        var sut = CreateService(db);
        var userId = Guid.NewGuid();

        var wf = await sut.CreateWorkflowAsync("Workflow intermediate", userId);
        wf.CompletedAt.Should().BeNull();

        var planning = await sut.TransitionAsync(wf.Id, AgentWorkflowStatus.Planning);
        planning.CompletedAt.Should().BeNull();

        var awaiting = await sut.TransitionAsync(wf.Id, AgentWorkflowStatus.AwaitingCollectionApproval);
        awaiting.CompletedAt.Should().BeNull();
    }

    // =========================================================================
    // 4. Step Persistence Lifecycle (Section 35, 12, 13)
    // =========================================================================

    [Fact]
    public async Task StepPersistence_FullLifecycle_TracksSequenceAndOutputs()
    {
        var dbName = Guid.NewGuid().ToString("N");
        using var db = CreateContext(dbName);
        var sut = CreateService(db);
        var userId = Guid.NewGuid();
        SeedUser(db, userId);

        var wf = await sut.CreateWorkflowAsync("Step lifecycle workflow", userId);

        // Add Step 1
        var step1 = await sut.AddStepAsync(
            wf.Id,
            WorkflowStepType.CollectionPlanning,
            agentName: "collection_planning_agent",
            inputJson: "{\"objective\":\"Plan urgent needs\"}");

        step1.Sequence.Should().Be(1);
        step1.Status.Should().Be(WorkflowStepStatus.Pending);
        step1.StartedAt.Should().BeNull();

        // Add Step 2
        var step2 = await sut.AddStepAsync(
            wf.Id,
            WorkflowStepType.FleetPlanning,
            agentName: "fleet_route_agent");

        step2.Sequence.Should().Be(2);

        // Start Step 1
        var startedStep1 = await sut.StartStepAsync(step1.Id);
        startedStep1.Status.Should().Be(WorkflowStepStatus.Running);
        startedStep1.StartedAt.Should().NotBeNull();

        // Complete Step 1 with structured JSON
        var outputJson = "{\"candidateGroups\":[],\"isCompleteSnapshot\":true,\"status\":\"completed\"}";
        var validationJson = "{\"isValid\":true,\"issues\":[]}";
        var completedStep1 = await sut.CompleteStepAsync(step1.Id, outputJson, validationJson);

        completedStep1.Status.Should().Be(WorkflowStepStatus.Completed);
        completedStep1.CompletedAt.Should().NotBeNull();
        completedStep1.OutputJson.Should().Be(outputJson);
        completedStep1.ValidationJson.Should().Be(validationJson);

        // Fail Step 2
        var failedStep2 = await sut.FailStepAsync(step2.Id, "Resource timeout contacting fleet service");
        failedStep2.Status.Should().Be(WorkflowStepStatus.Failed);
        failedStep2.ErrorMessage.Should().Be("Resource timeout contacting fleet service");
        failedStep2.CompletedAt.Should().NotBeNull();

        // Verify retrieval on aggregate
        var reloaded = await sut.GetWorkflowByIdAsync(wf.Id, includeDetails: true);
        reloaded!.Steps.Should().HaveCount(2);
        reloaded.Steps.First().Sequence.Should().Be(1);
        reloaded.Steps.Last().Sequence.Should().Be(2);
    }

    // =========================================================================
    // 5. Human Approval Infrastructure (Section 36, 15)
    // =========================================================================

    [Fact]
    public async Task RecordApprovalAsync_PersistsCollectionPlanningAndFleetDispatchApprovals()
    {
        var dbName = Guid.NewGuid().ToString("N");
        using var db = CreateContext(dbName);
        var sut = CreateService(db);
        var officerId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        SeedUser(db, officerId, "officer@smartwaste.lk");
        SeedUser(db, managerId, "manager@smartwaste.lk");

        var wf = await sut.CreateWorkflowAsync("Approval test workflow", officerId);

        var approval1 = await sut.RecordApprovalAsync(
            wf.Id,
            WorkflowApprovalStage.CollectionPlanning,
            WorkflowApprovalDecision.Approved,
            decidedByUserId: managerId,
            reason: "Approved 3 collection tasks for immediate scheduling.",
            payloadJson: "{\"approvedTaskCount\":3,\"adjustedScheduleTime\":\"2026-09-29T08:00:00Z\"}");

        approval1.ApprovalStage.Should().Be(WorkflowApprovalStage.CollectionPlanning);
        approval1.Decision.Should().Be(WorkflowApprovalDecision.Approved);
        approval1.DecidedByUserId.Should().Be(managerId);

        var approval2 = await sut.RecordApprovalAsync(
            wf.Id,
            WorkflowApprovalStage.FleetDispatch,
            WorkflowApprovalDecision.RevisionRequested,
            decidedByUserId: managerId,
            reason: "Vehicle WP-CAB-1234 reassigned to emergency maintenance.");

        approval2.ApprovalStage.Should().Be(WorkflowApprovalStage.FleetDispatch);
        approval2.Decision.Should().Be(WorkflowApprovalDecision.RevisionRequested);

        var reloaded = await sut.GetWorkflowByIdAsync(wf.Id, includeDetails: true);
        reloaded!.Approvals.Should().HaveCount(2);
    }

    // =========================================================================
    // 6. Execution Result Infrastructure (Section 37, 16)
    // =========================================================================

    [Fact]
    public async Task RecordExecutionResultAsync_PersistsTaskCreationAndAssignmentOutcomes()
    {
        var dbName = Guid.NewGuid().ToString("N");
        using var db = CreateContext(dbName);
        var sut = CreateService(db);
        var userId = Guid.NewGuid();
        SeedUser(db, userId);

        var wf = await sut.CreateWorkflowAsync("Execution result workflow", userId);

        var res1 = await sut.RecordExecutionResultAsync(
            wf.Id,
            WorkflowExecutionType.CollectionTaskCreation,
            WorkflowExecutionStatus.Succeeded,
            resultJson: "{\"createdTaskCodes\":[\"TSK-00101\",\"TSK-00102\"],\"totalCreated\":2}");

        res1.ExecutionType.Should().Be(WorkflowExecutionType.CollectionTaskCreation);
        res1.Status.Should().Be(WorkflowExecutionStatus.Succeeded);

        var res2 = await sut.RecordExecutionResultAsync(
            wf.Id,
            WorkflowExecutionType.CollectionAssignment,
            WorkflowExecutionStatus.Failed,
            errorMessage: "Driver profile inactive at time of assignment");

        res2.ExecutionType.Should().Be(WorkflowExecutionType.CollectionAssignment);
        res2.Status.Should().Be(WorkflowExecutionStatus.Failed);
        res2.ErrorMessage.Should().Be("Driver profile inactive at time of assignment");

        var reloaded = await sut.GetWorkflowByIdAsync(wf.Id, includeDetails: true);
        reloaded!.ExecutionResults.Should().HaveCount(2);
    }

    // =========================================================================
    // 7. Structured JSON & No Hidden Reasoning Guard (Section 38, 13)
    // =========================================================================

    [Fact]
    public async Task StructuredJsonStorage_PersistsValidJson_WithoutHiddenReasoning()
    {
        var dbName = Guid.NewGuid().ToString("N");
        using var db = CreateContext(dbName);
        var sut = CreateService(db);
        var userId = Guid.NewGuid();

        var wf = await sut.CreateWorkflowAsync("JSON contract verification", userId);

        var validOutputJson = "{\"status\":\"completed\",\"advisoryOnly\":true,\"dispatchPlans\":[{\"planId\":\"plan-1\"}]}";
        var step = await sut.AddStepAsync(wf.Id, WorkflowStepType.FleetPlanning, "fleet_route_agent");
        var completed = await sut.CompleteStepAsync(step.Id, outputJson: validOutputJson);

        completed.OutputJson.Should().Be(validOutputJson);

        // Prove via reflection that entity models strictly forbid hidden reasoning fields
        var stepProps = typeof(AgentWorkflowStep).GetProperties().Select(p => p.Name).ToList();
        stepProps.Should().NotContain(new[] { "ChainOfThought", "ReasoningTrace", "HiddenReasoning", "HiddenScratchpad" });

        var wfProps = typeof(AgentWorkflow).GetProperties().Select(p => p.Name).ToList();
        wfProps.Should().NotContain(new[] { "ChainOfThought", "ReasoningTrace", "HiddenReasoning" });
    }

    // =========================================================================
    // 8. Current Step Model (Sections 6, 7, 20)
    // =========================================================================

    [Fact]
    public async Task SetCurrentStepAsync_UpdatesCurrentStep_PreservingAuthoritativeStatus()
    {
        var dbName = Guid.NewGuid().ToString("N");
        using var db = CreateContext(dbName);
        var sut = CreateService(db);
        var userId = Guid.NewGuid();

        var wf = await sut.CreateWorkflowAsync("Current step tracking", userId);
        await sut.TransitionAsync(wf.Id, AgentWorkflowStatus.Planning);

        // Transition CurrentStep to SharedPlanning while Status remains Planning
        var step1 = await sut.SetCurrentStepAsync(wf.Id, WorkflowStepType.SharedPlanning);
        step1.CurrentStep.Should().Be(WorkflowStepType.SharedPlanning);
        step1.Status.Should().Be(AgentWorkflowStatus.Planning); // Status remains authoritative
        step1.UpdatedAt.Should().NotBeNull();

        // Operational progression: SharedPlanning -> WasteAnalysis without state-machine transition
        var step2 = await sut.SetCurrentStepAsync(wf.Id, WorkflowStepType.WasteAnalysis);
        step2.CurrentStep.Should().Be(WorkflowStepType.WasteAnalysis);
        step2.Status.Should().Be(AgentWorkflowStatus.Planning);

        // Verification after aggregate reload
        var reloaded = await sut.GetWorkflowByIdAsync(wf.Id, includeDetails: false);
        reloaded!.CurrentStep.Should().Be(WorkflowStepType.WasteAnalysis);
        reloaded.Status.Should().Be(AgentWorkflowStatus.Planning);
    }

    // =========================================================================
    // 9. Revision State Transitions (Section 4, 18)
    // =========================================================================

    [Fact]
    public async Task TransitionAsync_FromRevisionStates_SupportsRePlanningAndDefinitiveRejection()
    {
        var dbName = Guid.NewGuid().ToString("N");
        using var db = CreateContext(dbName);
        var sut = CreateService(db);
        var userId = Guid.NewGuid();

        // Path 1: CollectionNeedsRevision -> Rejected directly
        var wf1 = await sut.CreateWorkflowAsync("Collection revision reject test", userId);
        await sut.TransitionAsync(wf1.Id, AgentWorkflowStatus.Planning);
        await sut.TransitionAsync(wf1.Id, AgentWorkflowStatus.AwaitingCollectionApproval);
        await sut.TransitionAsync(wf1.Id, AgentWorkflowStatus.CollectionNeedsRevision, "Needs fewer tasks");
        var rejected1 = await sut.TransitionAsync(wf1.Id, AgentWorkflowStatus.Rejected, "Manager definitively rejects plan");
        rejected1.Status.Should().Be(AgentWorkflowStatus.Rejected);
        rejected1.CompletedAt.Should().NotBeNull();

        // Path 2: DispatchNeedsRevision -> Rejected directly
        var wf2 = await sut.CreateWorkflowAsync("Dispatch revision reject test", userId);
        await sut.TransitionAsync(wf2.Id, AgentWorkflowStatus.Planning);
        await sut.TransitionAsync(wf2.Id, AgentWorkflowStatus.AwaitingCollectionApproval);
        await sut.TransitionAsync(wf2.Id, AgentWorkflowStatus.CollectionApproved);
        await sut.TransitionAsync(wf2.Id, AgentWorkflowStatus.CreatingScheduledTasks);
        await sut.TransitionAsync(wf2.Id, AgentWorkflowStatus.FleetPlanning);
        await sut.TransitionAsync(wf2.Id, AgentWorkflowStatus.OperationalValidation);
        await sut.TransitionAsync(wf2.Id, AgentWorkflowStatus.AwaitingDispatchApproval);
        await sut.TransitionAsync(wf2.Id, AgentWorkflowStatus.DispatchNeedsRevision, "Vehicle out of service");
        var rejected2 = await sut.TransitionAsync(wf2.Id, AgentWorkflowStatus.Rejected, "Manager cancels fleet operation");
        rejected2.Status.Should().Be(AgentWorkflowStatus.Rejected);
        rejected2.CompletedAt.Should().NotBeNull();
    }

    // =========================================================================
    // 10. Completed State Semantics with Unplanned Tasks (Sections 8, 9, 21)
    // =========================================================================

    [Fact]
    public async Task CompletedWorkflow_CanRecordFinalOutcome_WithUnplannedTasks_AndStructuredResults()
    {
        var dbName = Guid.NewGuid().ToString("N");
        using var db = CreateContext(dbName);
        var sut = CreateService(db);
        var userId = Guid.NewGuid();
        SeedUser(db, userId);

        var wf = await sut.CreateWorkflowAsync("Workflow with unplanned tasks", userId);
        await sut.TransitionAsync(wf.Id, AgentWorkflowStatus.Planning);
        await sut.TransitionAsync(wf.Id, AgentWorkflowStatus.AwaitingCollectionApproval);
        await sut.TransitionAsync(wf.Id, AgentWorkflowStatus.CollectionApproved);
        await sut.TransitionAsync(wf.Id, AgentWorkflowStatus.CreatingScheduledTasks);
        await sut.TransitionAsync(wf.Id, AgentWorkflowStatus.FleetPlanning);
        await sut.TransitionAsync(wf.Id, AgentWorkflowStatus.OperationalValidation);
        await sut.TransitionAsync(wf.Id, AgentWorkflowStatus.AwaitingDispatchApproval);
        await sut.TransitionAsync(wf.Id, AgentWorkflowStatus.DispatchApproved);
        await sut.TransitionAsync(wf.Id, AgentWorkflowStatus.ExecutingAssignments);
        var completed = await sut.TransitionAsync(wf.Id, AgentWorkflowStatus.Completed);

        // Record execution results showing partial execution / unplanned tasks
        await sut.RecordExecutionResultAsync(
            wf.Id,
            WorkflowExecutionType.CollectionAssignment,
            WorkflowExecutionStatus.PartiallySucceeded,
            resultJson: "{\"assignedTaskCodes\":[\"TSK-101\"],\"unplannedTaskCodes\":[\"TSK-102\"]}");

        completed.FinalOutcome = "1 dispatch plan executed; 1 Scheduled task remained unplanned.";
        await db.SaveChangesAsync();

        var reloaded = await sut.GetWorkflowByIdAsync(wf.Id, includeDetails: true);
        reloaded!.Status.Should().Be(AgentWorkflowStatus.Completed);
        reloaded.CompletedAt.Should().NotBeNull();
        reloaded.FinalOutcome.Should().Be("1 dispatch plan executed; 1 Scheduled task remained unplanned.");
        reloaded.ExecutionResults.Should().ContainSingle(e => e.Status == WorkflowExecutionStatus.PartiallySucceeded);
    }

    // =========================================================================
    // 11. Approval Concurrency & Version Token (Sections 10, 11, 22)
    // =========================================================================

    [Fact]
    public async Task RecordApprovalAsync_IncrementsParentWorkflowVersion_AndParticipatesInOptimisticConcurrency()
    {
        var dbName = Guid.NewGuid().ToString("N");
        using var db = CreateContext(dbName);
        var sut = CreateService(db);
        var managerId = Guid.NewGuid();
        SeedUser(db, managerId);

        var wf = await sut.CreateWorkflowAsync("Approval concurrency test", managerId);
        wf.Version.Should().Be(1);

        // Client A loads the workflow while Version = 1
        using var clientADb = CreateContext(dbName);
        var clientAWf = await clientADb.AgentWorkflows.FirstAsync(w => w.Id == wf.Id);
        clientAWf.Version.Should().Be(1);

        // Client B records approval (bumps parent Version in database to 2)
        var approval = await sut.RecordApprovalAsync(
            wf.Id,
            WorkflowApprovalStage.CollectionPlanning,
            WorkflowApprovalDecision.Approved,
            decidedByUserId: managerId,
            reason: "Approved candidate groups");

        approval.Should().NotBeNull();

        var reloaded = await sut.GetWorkflowByIdAsync(wf.Id, includeDetails: false);
        reloaded!.Version.Should().Be(2);
        reloaded.UpdatedAt.Should().NotBeNull();

        // Client A now attempts to save a modification using its stale Version=1 read
        clientAWf.Objective = "Concurrent colliding modification";
        var act = () => clientADb.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }
}
