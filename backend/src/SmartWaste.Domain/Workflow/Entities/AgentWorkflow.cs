using SmartWaste.Domain.Entities;
using SmartWaste.Domain.Reporting.Entities;
using SmartWaste.Domain.Workflow.Enums;

namespace SmartWaste.Domain.Workflow.Entities;

/// <summary>
/// Authoritative aggregate root representing an end-to-end Agentic AI collection workflow lifecycle.
/// Persisted in PostgreSQL and managed authoritatively by ASP.NET Core.
/// </summary>
public class AgentWorkflow
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Original domain objective passed to the Shared Planner / specialist agents.
    /// Excludes hidden reasoning, prompt templates, and raw tokens.
    /// </summary>
    public string Objective { get; set; } = string.Empty;

    public AgentWorkflowTriggerType TriggerType { get; set; } = AgentWorkflowTriggerType.ManualOperationalPlanning;
    public Guid? TriggeringWasteReportId { get; set; }
    public WasteReport? TriggeringWasteReport { get; set; }

    // Durable worker claim metadata. Attempt count belongs to the current AI phase;
    // authoritative report verification resets it before the C2 continuation.
    public Guid? ProcessingLeaseId { get; set; }
    public DateTime? ProcessingLeaseExpiresAt { get; set; }
    public int ProcessingAttemptCount { get; set; }

    /// <summary>
    /// Current authoritative workflow lifecycle status.
    /// </summary>
    public AgentWorkflowStatus Status { get; set; } = AgentWorkflowStatus.Created;

    /// <summary>
    /// Current logical execution step within the workflow, tracking execution position for pause/resume.
    /// Status remains the authoritative lifecycle state.
    /// </summary>
    public WorkflowStepType CurrentStep { get; set; } = WorkflowStepType.None;

    public Guid InitiatedByUserId { get; set; }
    public AppUser? InitiatedByUser { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Timestamp recorded when the workflow enters a terminal state (Completed, Rejected, Failed).
    /// Null for non-terminal states.
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Concise final authoritative operational outcome summary (e.g. "2 dispatch plans executed; 2 Scheduled tasks remained unplanned").
    /// Excludes hidden model reasoning or chain-of-thought tokens.
    /// </summary>
    public string? FinalOutcome { get; set; }

    /// <summary>
    /// Optimistic concurrency token ensuring atomic and safe state transitions.
    /// </summary>
    public int Version { get; set; } = 1;

    public void EnsureValidTrigger()
    {
        if ((TriggerType == AgentWorkflowTriggerType.ManualOperationalPlanning && TriggeringWasteReportId is null) ||
            (TriggerType == AgentWorkflowTriggerType.CitizenReportSubmission && TriggeringWasteReportId is not null))
            return;

        throw new InvalidOperationException("AgentWorkflow trigger type and report ID must agree.");
    }

    // Navigation collections
    public ICollection<AgentWorkflowStep> Steps { get; set; } = new List<AgentWorkflowStep>();
    public ICollection<AgentWorkflowTransition> Transitions { get; set; } = new List<AgentWorkflowTransition>();
    public ICollection<AgentWorkflowApproval> Approvals { get; set; } = new List<AgentWorkflowApproval>();
    public ICollection<AgentWorkflowExecutionResult> ExecutionResults { get; set; } = new List<AgentWorkflowExecutionResult>();
}
