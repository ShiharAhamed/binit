using SmartWaste.Domain.Entities;
using SmartWaste.Domain.Workflow.Enums;

namespace SmartWaste.Domain.Workflow.Entities;

/// <summary>
/// Persistence record for human-in-the-loop review decisions (CollectionPlanning and FleetDispatch).
/// Infrastructure entity for future approval workflows.
/// </summary>
public class AgentWorkflowApproval
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid WorkflowId { get; set; }
    public AgentWorkflow? Workflow { get; set; }

    public Guid? WorkflowStepId { get; set; }
    public AgentWorkflowStep? WorkflowStep { get; set; }

    public WorkflowApprovalStage ApprovalStage { get; set; }

    public WorkflowApprovalDecision Decision { get; set; }

    public string? DecisionReason { get; set; }

    /// <summary>
    /// Optional structured review payload (e.g. adjusted schedule times, per-plan approvals, acknowledgement flags).
    /// </summary>
    public string? DecisionPayloadJson { get; set; }

    public Guid DecidedByUserId { get; set; }
    public AppUser? DecidedByUser { get; set; }

    public DateTime DecidedAt { get; set; } = DateTime.UtcNow;
}
