namespace SmartWaste.Application.Reporting.DTOs.Requests;

/// <summary>
/// Request payload for rejecting a waste report during review (POST /api/v1/waste-reports/{id}/reject).
/// </summary>
public class RejectWasteReportRequest
{
    public string Reason { get; set; } = string.Empty;
}
