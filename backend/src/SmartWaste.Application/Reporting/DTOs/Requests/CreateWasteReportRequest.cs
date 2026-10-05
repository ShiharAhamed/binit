using SmartWaste.Domain.Reporting.Enums;

namespace SmartWaste.Application.Reporting.DTOs.Requests;

/// <summary>
/// Request payload for citizen waste report creation (POST /api/v1/waste-reports).
/// Server-controlled fields (CitizenId, Status, Priority, etc.) are forbidden on client input.
/// </summary>
public class CreateWasteReportRequest
{
    public string Description { get; set; } = string.Empty;
    public WasteType? WasteType { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? AddressText { get; set; }
}
