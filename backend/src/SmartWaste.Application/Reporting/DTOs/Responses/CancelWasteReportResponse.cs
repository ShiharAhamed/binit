namespace SmartWaste.Application.Reporting.DTOs.Responses;

/// <summary>
/// Response payload for successful business cancellation of a waste report (DELETE /api/v1/waste-reports/{id}).
/// </summary>
public class CancelWasteReportResponse
{
    public string Message { get; set; } = "Waste report cancelled successfully.";
    public string Status { get; set; } = "Cancelled";
}
