using SmartWaste.Domain.Collection.Enums;

namespace SmartWaste.Application.Collection.DTOs.Responses;

/// <summary>
/// Full internal operational details of a municipal roadside waste bin (GET /api/v1/bins/{id}).
/// Used by WasteOfficers and MunicipalManagers.
/// </summary>
public class WasteBinDetailDto
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
    public DateTime? LastCollectedAt { get; set; }
    public BinObservationSummaryDto? LatestObservation { get; set; }
    public bool HasActiveTask { get; set; }
    public Guid? ActiveTaskId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
