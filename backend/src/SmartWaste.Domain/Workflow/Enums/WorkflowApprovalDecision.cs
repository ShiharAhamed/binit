namespace SmartWaste.Domain.Workflow.Enums;

/// <summary>
/// Decision made by a human reviewer for an AgentWorkflow proposal stage.
/// </summary>
public enum WorkflowApprovalDecision
{
    Approved = 0,
    Rejected = 1,
    RevisionRequested = 2
}
