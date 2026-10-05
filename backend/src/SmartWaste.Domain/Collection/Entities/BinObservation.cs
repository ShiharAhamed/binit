using SmartWaste.Domain.Collection.Enums;
using SmartWaste.Domain.Entities;

namespace SmartWaste.Domain.Collection.Entities;

/// <summary>
/// Append-only manual inspection record recording bin fill level and physical condition.
/// </summary>
public class BinObservation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid WasteBinId { get; set; }
    public WasteBin? WasteBin { get; set; }

    public int FillLevelPercent { get; set; }

    public BinCondition Condition { get; set; }

    public string? Notes { get; set; }

    public Guid RecordedByUserId { get; set; }
    public AppUser? RecordedByUser { get; set; }

    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}
