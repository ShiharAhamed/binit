using SmartWaste.Domain.Collection.Enums;

namespace SmartWaste.Application.Collection.DTOs.Responses;

/// <summary>
/// Nested bin-specific operational details within a derived collection need item.
/// Null when the collection need targets a citizen waste report.
/// </summary>
public class CollectionNeedBinDetailsDto
{
    public string BinCode { get; set; } = string.Empty;
    public int CapacityLiters { get; set; }
    public int? LatestFillLevelPercent { get; set; }
    public BinCondition? LatestCondition { get; set; }
    public double? ObservationAgeHours { get; set; }
}
