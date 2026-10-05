namespace SmartWaste.Application.Workflow.DTOs.Requests;

/// <summary>
/// Request payload for rejecting a fleet dispatch proposal at Gate 2 (terminal).
/// </summary>
public class RejectDispatchPlanRequest
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
