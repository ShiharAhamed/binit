using SmartWaste.Domain.Reporting.Enums;

namespace SmartWaste.Application.Reporting.DTOs.Requests;

/// <summary>
/// PATCH request payload for editing a submitted waste report (PATCH /api/v1/waste-reports/{id}).
/// All fields are optional to support partial updates.
/// </summary>
public class UpdateWasteReportRequest
{
    public string? Description { get; set; }
    public WasteType? WasteType { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? AddressText { get; set; }
}
