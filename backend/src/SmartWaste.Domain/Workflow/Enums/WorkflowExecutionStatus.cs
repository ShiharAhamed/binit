namespace SmartWaste.Domain.Workflow.Enums;

/// <summary>
/// Execution status for an authoritative workflow execution result.
/// </summary>
public enum WorkflowExecutionStatus
{
    Pending = 0,
    Succeeded = 1,
    Failed = 2,
    PartiallySucceeded = 3
}
