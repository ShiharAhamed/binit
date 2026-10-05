using SmartWaste.Domain.Reporting.Enums;

namespace SmartWaste.Application.Collection.DTOs.Requests;

/// <summary>
/// Request payload for registering a new municipal public roadside bin (POST /api/v1/bins).
/// Server-controlled audit and lifecycle fields are strictly forbidden on client input.
/// </summary>
public class CreateWasteBinRequest
{
    public string BinCode { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? AddressText { get; set; }
    public int? CapacityLiters { get; set; }
    public IReadOnlyList<WasteType> AcceptedWasteTypes { get; set; } = Array.Empty<WasteType>();
    public IReadOnlyList<int> CollectionWeekdays { get; set; } = Array.Empty<int>();
}
