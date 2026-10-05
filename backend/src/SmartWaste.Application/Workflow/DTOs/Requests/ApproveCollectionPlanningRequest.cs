namespace SmartWaste.Application.Workflow.DTOs.Requests;

/// <summary>
/// Request payload for approving a collection planning proposal at Gate 1.
/// </summary>
public class ApproveCollectionPlanningRequest
{
    /// <summary>
    /// Expected workflow Version concurrency token from the loaded client state.
    /// Prevents approving an outdated proposal if another operation mutated the workflow.
    /// </summary>
    public int ExpectedVersion { get; set; }

    /// <summary>
    /// Optional reviewer confirmation reason or notes.
    /// </summary>
    public string? Reason { get; set; }
}
