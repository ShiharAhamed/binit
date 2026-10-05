namespace SmartWaste.Application.Workflow.DTOs.Requests;

/// <summary>
/// Request payload to initiate a new AgentWorkflow aggregate.
/// </summary>
public class CreateAgentWorkflowRequest
{
    /// <summary>
    /// High-level municipal waste collection objective for the workflow.
    /// Min 5 characters, max 1000 characters.
    /// </summary>
    public string Objective { get; set; } = string.Empty;
}
