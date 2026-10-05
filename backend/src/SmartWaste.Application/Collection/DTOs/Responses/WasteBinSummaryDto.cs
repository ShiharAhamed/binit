using SmartWaste.Domain.Collection.Enums;

namespace SmartWaste.Application.Collection.DTOs.Responses;

/// <summary>
/// Internal staff summary DTO for paginated bin lists (GET /api/v1/bins).
/// Used by WasteOfficers and MunicipalManagers.
/// </summary>
public class WasteBinSummaryDto
{
    public Guid Id { get; set; }
    public string BinCode { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string? AddressText { get; set; }
    public int CapacityLiters { get; set; }
    public BinAdministrativeStatus AdministrativeStatus { get; set; }
    public IReadOnlyList<string> AcceptedWasteTypes { get; set; } = Array.Empty<string>();
    public IReadOnlyList<int> CollectionWeekdays { get; set; } = Array.Empty<int>();
    public int? LatestFillLevelPercent { get; set; }
    public BinCondition? LatestCondition { get; set; }
    public DateTime? LatestObservationAt { get; set; }
    public bool HasActiveTask { get; set; }
    public DateTime? LastCollectedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
