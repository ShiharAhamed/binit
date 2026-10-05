namespace SmartWaste.Application.Workflow.DTOs.Requests;

/// <summary>
/// Command payload for authoritative execution of an approved fleet dispatch proposal (Step 8).
/// The approved dispatch proposal is already persisted in the workflow; the client only provides concurrency control.
/// </summary>
public class ExecuteDispatchPlanRequest
{
    /// <summary>
    /// Optimistic concurrency token. Must match the current AgentWorkflow.Version.
    /// </summary>
    public int ExpectedVersion { get; set; }
}
