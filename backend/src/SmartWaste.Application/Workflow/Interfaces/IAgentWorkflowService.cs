using SmartWaste.Application.Common.Models;
using SmartWaste.Application.Workflow.DTOs.Requests;
using SmartWaste.Application.Workflow.DTOs.Responses;
using SmartWaste.Application.Workflow.DTOs.Transport;
using SmartWaste.Domain.Workflow.Entities;
using SmartWaste.Domain.Workflow.Enums;

namespace SmartWaste.Application.Workflow.Interfaces;

/// <summary>
/// Application service interface for authoritatively creating, updating, querying, and auditing AgentWorkflow aggregates.
/// </summary>
public interface IAgentWorkflowService
{
    Task<PythonWorkflowStartRequest> BuildPythonStartRequestAsync(Guid workflowId, int expectedVersion, Guid? expectedProcessingLeaseId = null, CancellationToken cancellationToken = default);
    Task<AgentWorkflow> PersistReportVerificationPauseAsync(Guid workflowId, PythonOrchestrationEnvelope result, int expectedVersion, Guid? changedByUserId = null, Guid? expectedProcessingLeaseId = null, CancellationToken cancellationToken = default);
    Task<PythonWorkflowResumeRequest> BuildReportVerificationResumeRequestAsync(Guid workflowId, int expectedVersion, Guid? expectedProcessingLeaseId = null, CancellationToken cancellationToken = default);
    Task<AgentWorkflow> PersistReportVerificationContinuationAsync(Guid workflowId, PythonOrchestrationEnvelope result, int expectedVersion, Guid? changedByUserId = null, Guid? expectedProcessingLeaseId = null, CancellationToken cancellationToken = default);
    /// <summary>
    /// Creates a new AgentWorkflow in Created state with an initial audit transition record.
    /// </summary>
    Task<AgentWorkflow> CreateWorkflowAsync(
        string objective,
        Guid initiatedByUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a workflow by ID with optional eager loading of child collections.
    /// </summary>
    Task<AgentWorkflow?> GetWorkflowByIdAsync(
        Guid id,
        bool includeDetails = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a paged list of workflow summaries filtered by row-level role access and optional status.
    /// </summary>
    Task<PagedResult<AgentWorkflowSummaryDto>> GetWorkflowsAsync(
        AgentWorkflowListQuery query,
        Guid actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the complete structured view of a specific workflow, validating row-level access.
    /// </summary>
    Task<AgentWorkflowDetailDto> GetWorkflowDetailsAsync(
        Guid id,
        Guid actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the chronological audit transition history for a workflow, validating row-level access.
    /// </summary>
    Task<IReadOnlyList<AgentWorkflowTransitionDto>> GetWorkflowHistoryAsync(
        Guid id,
        Guid actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically starts a Created workflow, transitioning it to Planning and setting CurrentStep to SharedPlanning.
    /// Does not trigger external AI microservice execution.
    /// </summary>
    Task<AgentWorkflowSummaryDto> StartWorkflowAsync(
        Guid id,
        Guid actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically transitions the workflow from its current status to a target status,
    /// validating against the state machine, setting terminal timestamps if applicable,
    /// recording an immutable audit transition row, and enforcing optimistic concurrency.
    /// </summary>
    Task<AgentWorkflow> TransitionAsync(
        Guid workflowId,
        AgentWorkflowStatus toStatus,
        string? reason = null,
        Guid? changedByUserId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the logical execution position of the workflow.
    /// </summary>
    Task<AgentWorkflow> SetCurrentStepAsync(
        Guid workflowId,
        WorkflowStepType stepType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a discrete specialist or system execution step to the workflow.
    /// </summary>
    Task<AgentWorkflowStep> AddStepAsync(
        Guid workflowId,
        WorkflowStepType stepType,
        string? agentName = null,
        string? inputJson = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a step as Running and sets its StartedAt timestamp.
    /// </summary>
    Task<AgentWorkflowStep> StartStepAsync(
        Guid stepId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a step as Completed with structured output and optional validation summary.
    /// </summary>
    Task<AgentWorkflowStep> CompleteStepAsync(
        Guid stepId,
        string? outputJson = null,
        string? validationJson = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a step as Failed with an error message and optional validation diagnostic.
    /// </summary>
    Task<AgentWorkflowStep> FailStepAsync(
        Guid stepId,
        string errorMessage,
        string? validationJson = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a human-in-the-loop review decision (infrastructure for future approval workflows).
    /// </summary>
    Task<AgentWorkflowApproval> RecordApprovalAsync(
        Guid workflowId,
        WorkflowApprovalStage stage,
        WorkflowApprovalDecision decision,
        Guid decidedByUserId,
        string? reason = null,
        string? payloadJson = null,
        Guid? stepId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists an authoritative system execution result (infrastructure for future execution bridges).
    /// </summary>
    Task<AgentWorkflowExecutionResult> RecordExecutionResultAsync(
        Guid workflowId,
        WorkflowExecutionType executionType,
        WorkflowExecutionStatus status,
        string? resultJson = null,
        string? errorMessage = null,
        Guid? stepId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically approves the collection planning proposal at Gate 1 (AwaitingCollectionApproval -> CollectionApproved).
    /// Enforces optimistic concurrency, verifies complete C2 snapshot, records approval and audit transition in one transaction.
    /// Authorized for MunicipalManager and WasteOfficer.
    /// </summary>
    Task<AgentWorkflowDetailDto> ApproveCollectionPlanningAsync(
        Guid workflowId,
        ApproveCollectionPlanningRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically requests revision of the collection planning proposal at Gate 1 (AwaitingCollectionApproval -> CollectionNeedsRevision).
    /// Enforces optimistic concurrency, records reviewer feedback reason, approval and audit transition in one transaction.
    /// Authorized for MunicipalManager and WasteOfficer.
    /// </summary>
    Task<AgentWorkflowDetailDto> RequestCollectionRevisionAsync(
        Guid workflowId,
        RequestCollectionRevisionRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically rejects the collection planning proposal at Gate 1 (AwaitingCollectionApproval -> Rejected).
    /// Enforces optimistic concurrency, records rejection reason, sets terminal CompletedAt, approval and transition in one transaction.
    /// Authorized for MunicipalManager and WasteOfficer.
    /// </summary>
    Task<AgentWorkflowDetailDto> RejectCollectionPlanningAsync(
        Guid workflowId,
        RejectCollectionPlanningRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically approves the fleet dispatch proposal at Gate 2 (AwaitingDispatchApproval -> DispatchApproved).
    /// Enforces optimistic concurrency, verifies C4 ReadyForHumanReview and required warning acknowledgement, records approval and audit transition in one transaction.
    /// Authorized for MunicipalManager and WasteOfficer.
    /// </summary>
    Task<AgentWorkflowDetailDto> ApproveDispatchPlanAsync(
        Guid workflowId,
        ApproveDispatchPlanRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically requests revision of the fleet dispatch proposal at Gate 2 (AwaitingDispatchApproval -> DispatchNeedsRevision).
    /// Enforces optimistic concurrency, records reviewer feedback reason, approval and audit transition in one transaction.
    /// Authorized for MunicipalManager and WasteOfficer.
    /// </summary>
    Task<AgentWorkflowDetailDto> RequestDispatchRevisionAsync(
        Guid workflowId,
        RequestDispatchRevisionRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically rejects the fleet dispatch proposal at Gate 2 (AwaitingDispatchApproval -> Rejected).
    /// Enforces optimistic concurrency, records rejection reason, sets terminal CompletedAt, approval and transition in one transaction.
    /// Authorized for MunicipalManager and WasteOfficer.
    /// </summary>
    Task<AgentWorkflowDetailDto> RejectDispatchPlanAsync(
        Guid workflowId,
        RejectDispatchPlanRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Authoritative C2 execution bridge: converts an approved C2 collection planning proposal
    /// into real Scheduled CollectionTasks via the existing CollectionTaskService.
    /// Enforces:
    /// - Role: MunicipalManager or WasteOfficer
    /// - Status: CollectionApproved (or resumable CreatingScheduledTasks)
    /// - Human approval prerequisite
    /// - Fresh authoritative need re-validation
    /// - ScheduledTaskCreation workflow step and CollectionTaskCreation execution result persistence
    /// - Transitions: CollectionApproved -> CreatingScheduledTasks -> FleetPlanning
    /// </summary>
    Task<AgentWorkflowDetailDto> ExecuteCollectionPlanAsync(
        Guid workflowId,
        ExecuteCollectionPlanRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Authoritative C3 execution bridge: converts an approved C3 fleet dispatch proposal
    /// into real CollectionAssignments via the existing CollectionAssignmentService.
    /// Enforces:
    /// - Role: MunicipalManager or WasteOfficer
    /// - Status: DispatchApproved (or resumable ExecutingAssignments)
    /// - Gate 2 human approval bound to completed OperationalValidation step
    /// - C4 ReadyForHumanReview output and warning acknowledgement
    /// - Fresh authoritative task, driver, vehicle, and compatibility re-validation
    /// - Ordered RouteStops (1..N) matching approved C3 Sequence
    /// - AssignmentExecution workflow step and CollectionAssignment execution result persistence
    /// - Transitions: DispatchApproved -> ExecutingAssignments -> Completed (or Failed)
    /// </summary>
    Task<AgentWorkflowDetailDto> ExecuteDispatchPlanAsync(
        Guid workflowId,
        ExecuteDispatchPlanRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default);
}

