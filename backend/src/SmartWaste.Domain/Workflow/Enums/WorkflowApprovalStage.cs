namespace SmartWaste.Domain.Workflow.Enums;

/// <summary>
/// Operational stage requiring human-in-the-loop review and authorization.
/// </summary>
public enum WorkflowApprovalStage
{
    CollectionPlanning = 0,
    FleetDispatch = 1
}
