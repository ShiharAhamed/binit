using SmartWaste.Domain.Entities;
using SmartWaste.Domain.Workflow.Enums;

namespace SmartWaste.Domain.Workflow.Entities;

/// <summary>
/// Immutable audit record tracking every lifecycle status transition of an AgentWorkflow.
/// Guarantees auditability across all AI, human, and execution state changes.
/// </summary>
public class AgentWorkflowTransition
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid WorkflowId { get; set; }
    public AgentWorkflow? Workflow { get; set; }

    /// <summary>Previous status, or null for the initial workflow creation transition.</summary>
    public AgentWorkflowStatus? FromStatus { get; set; }

    /// <summary>Target status reached by this transition.</summary>
    public AgentWorkflowStatus ToStatus { get; set; }

    /// <summary>Audit justification or descriptive reason for the transition.</summary>
    public string? Reason { get; set; }

    /// <summary>User ID who triggered the transition, or null for automated system transitions.</summary>
    public Guid? ChangedByUserId { get; set; }
    public AppUser? ChangedByUser { get; set; }

    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}
