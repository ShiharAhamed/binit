namespace SmartWaste.Application.Operations.DTOs.Requests;

/// <summary>
/// Request payload for resolving an operational issue.
/// Resolution note provides mandatory staff context explaining how the issue was addressed.
/// </summary>
public class ResolveOperationalIssueRequest
{
    public string ResolutionNote { get; set; } = string.Empty;
}
