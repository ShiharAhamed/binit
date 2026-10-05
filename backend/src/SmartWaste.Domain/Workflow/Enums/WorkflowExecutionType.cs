namespace SmartWaste.Domain.Workflow.Enums;

/// <summary>
/// Type of authoritative system execution triggered after human approval.
/// </summary>
public enum WorkflowExecutionType
{
    CollectionTaskCreation = 0,
    CollectionAssignment = 1
}
