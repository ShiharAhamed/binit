using SmartWaste.Domain.Reporting.Enums;

namespace SmartWaste.Application.Collection.DTOs.Requests;

/// <summary>
/// Request payload for updating operational metadata of a registered bin (PUT /api/v1/bins/{id}).
/// BinCode, administrative status, and audit timestamps cannot be modified via this endpoint.
/// </summary>
public class UpdateWasteBinRequest
{
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? AddressText { get; set; }
    public int? CapacityLiters { get; set; }
    public IReadOnlyList<WasteType> AcceptedWasteTypes { get; set; } = Array.Empty<WasteType>();
    public IReadOnlyList<int> CollectionWeekdays { get; set; } = Array.Empty<int>();
}
