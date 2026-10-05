namespace SmartWaste.Application.Workflow.DTOs.Requests;

/// <summary>
/// Request payload for rejecting a collection planning proposal at Gate 1 (terminal).
/// </summary>
public class RejectCollectionPlanningRequest
{
    /// <summary>
    /// Expected workflow Version concurrency token from the loaded client state.
    /// </summary>
    public int ExpectedVersion { get; set; }

    /// <summary>
    /// Mandatory human reason explaining why the workflow proposal is authoritatively rejected.
    /// </summary>
    public string Reason { get; set; } = string.Empty;
}
