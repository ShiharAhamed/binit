using SmartWaste.Domain.Workflow.Enums;

namespace SmartWaste.Domain.Workflow.Entities;

/// <summary>
/// Persistence record for execution bridges that convert approved proposals into real entities
/// (Scheduled CollectionTasks or CollectionAssignments).
/// Infrastructure entity for future execution phases.
/// </summary>
public class AgentWorkflowExecutionResult
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid WorkflowId { get; set; }
    public AgentWorkflow? Workflow { get; set; }

    public Guid? WorkflowStepId { get; set; }
    public AgentWorkflowStep? WorkflowStep { get; set; }

    public WorkflowExecutionType ExecutionType { get; set; }

    public WorkflowExecutionStatus Status { get; set; } = WorkflowExecutionStatus.Pending;

    /// <summary>Structured summary of created entities (e.g. task IDs, assignment IDs, route IDs).</summary>
    public string? ResultJson { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
}
