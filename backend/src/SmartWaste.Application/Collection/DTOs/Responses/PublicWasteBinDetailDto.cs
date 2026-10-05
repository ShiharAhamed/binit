namespace SmartWaste.Application.Collection.DTOs.Responses;

/// <summary>
/// Public Citizen-facing detailed representation of a municipal roadside waste bin (GET /api/v1/bins/public/{id}).
/// Excludes internal notes, staff user IDs, maintenance logs, and private operational data.
/// </summary>
public class PublicWasteBinDetailDto
{
    public Guid Id { get; set; }
    public string BinCode { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string? AddressText { get; set; }
    public int CapacityLiters { get; set; }
    public IReadOnlyList<string> AcceptedWasteTypes { get; set; } = Array.Empty<string>();
    public string PublicAvailability { get; set; } = string.Empty;
    public DateTime? LastObservedAt { get; set; }
    public bool IsCollectionScheduled { get; set; }
}
