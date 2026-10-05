using SmartWaste.Domain.Workflow.Enums;

namespace SmartWaste.Domain.Workflow.Entities;

/// <summary>
/// Chronological record of a discrete specialist agent or system execution step.
/// Stores structured contract inputs, outputs, and validation findings (strictly excluding hidden reasoning).
/// </summary>
public class AgentWorkflowStep
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid WorkflowId { get; set; }
    public AgentWorkflow? Workflow { get; set; }

    /// <summary>
    /// Sequential step order within the workflow (1, 2, 3...).
    /// </summary>
    public int Sequence { get; set; }

    public WorkflowStepType StepType { get; set; }

    /// <summary>
    /// Name of the specialist agent (e.g. "waste_analysis_agent", "collection_planning_agent",
    /// "fleet_route_agent", "validation_operations_agent"), or null for system tasks.
    /// </summary>
    public string? AgentName { get; set; }

    public WorkflowStepStatus Status { get; set; } = WorkflowStepStatus.Pending;

    /// <summary>Structured JSON input payload adhering to frozen specialist request schema.</summary>
    public string? InputJson { get; set; }

    /// <summary>Structured JSON output payload adhering to frozen specialist result schema.</summary>
    public string? OutputJson { get; set; }

    /// <summary>Structured validation findings, compatibility checks, or diagnostic summaries.</summary>
    public string? ValidationJson { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }
}
