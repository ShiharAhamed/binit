using SmartWaste.Domain.Collection.Enums;

namespace SmartWaste.Application.Collection.DTOs.Responses;

/// <summary>
/// Summary representation of the latest bin observation embedded in WasteBinDetailDto.
/// </summary>
public class BinObservationSummaryDto
{
    public Guid Id { get; set; }
    public int FillLevelPercent { get; set; }
    public BinCondition Condition { get; set; }
    public string? Notes { get; set; }
    public Guid RecordedByUserId { get; set; }
    public string? RecordedByUserName { get; set; }
    public DateTime RecordedAt { get; set; }
}
