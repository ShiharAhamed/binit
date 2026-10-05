namespace SmartWaste.Domain.Workflow.Enums;

/// <summary>
/// Execution status for an individual AgentWorkflowStep.
/// </summary>
public enum WorkflowStepStatus
{
    Pending = 0,
    Running = 1,
    Completed = 2,
    Failed = 3,
    Skipped = 4
}
