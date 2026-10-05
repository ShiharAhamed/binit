namespace SmartWaste.Application.Workflow.DTOs.Requests;

/// <summary>
/// Request payload for requesting revision of a collection planning proposal at Gate 1.
/// </summary>
public class RequestCollectionRevisionRequest
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
