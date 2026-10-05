namespace SmartWaste.Application.Workflow.DTOs.Requests;

/// <summary>
/// Command payload for authoritative execution of an approved collection planning proposal.
/// The approved plan is already persisted in the workflow; the client only provides concurrency control.
/// </summary>
public class ExecuteCollectionPlanRequest
{
    /// <summary>
    /// Optimistic concurrency token. Must match the current AgentWorkflow.Version.
    /// </summary>
    public int ExpectedVersion { get; set; }
}
