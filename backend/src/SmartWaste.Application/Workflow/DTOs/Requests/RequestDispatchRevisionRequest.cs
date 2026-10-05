namespace SmartWaste.Application.Workflow.DTOs.Requests;

/// <summary>
/// Request payload for requesting revision of a fleet dispatch proposal at Gate 2.
/// </summary>
public class RequestDispatchRevisionRequest
{
    /// <summary>
    /// Expected workflow Version concurrency token from the loaded client state.
    /// </summary>
    public int ExpectedVersion { get; set; }

    /// <summary>
    /// Mandatory human feedback describing the changes required before resubmission.
    /// </summary>
    public string Reason { get; set; } = string.Empty;
}
