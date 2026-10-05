using SmartWaste.Domain.Collection.Enums;

namespace SmartWaste.Domain.Collection.Entities;

/// <summary>
/// Represents a registered municipal roadside public waste bin.
/// </summary>
public class WasteBin
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string BinCode { get; set; } = string.Empty;

    public int CapacityLiters { get; set; }

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public string? AddressText { get; set; }

    public BinAdministrativeStatus AdministrativeStatus { get; set; } = BinAdministrativeStatus.Active;

    public int[] CollectionWeekdays { get; set; } = Array.Empty<int>();

    public DateTime? LastCollectedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public ICollection<WasteBinAcceptedWasteType> AcceptedWasteTypes { get; set; } = new List<WasteBinAcceptedWasteType>();
    public ICollection<BinObservation> Observations { get; set; } = new List<BinObservation>();
    public ICollection<CollectionTask> CollectionTasks { get; set; } = new List<CollectionTask>();
}
