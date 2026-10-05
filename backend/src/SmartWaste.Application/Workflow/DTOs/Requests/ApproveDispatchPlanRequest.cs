namespace SmartWaste.Application.Workflow.DTOs.Requests;

/// <summary>
/// Request payload for approving a fleet dispatch proposal at Gate 2.
/// </summary>
public class ApproveDispatchPlanRequest
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

    /// <summary>
    /// Required explicit boolean acknowledgement when the C4 operational review returned requiresAcknowledgement=true.
    /// </summary>
    public bool AcknowledgeWarnings { get; set; }
}
