using SmartWaste.Domain.Collection.Enums;

namespace SmartWaste.Application.Collection.DTOs.Responses;

/// <summary>
/// Response representation of an immutable manual field observation (POST /api/v1/bins/{id}/observations).
/// </summary>
public class BinObservationDto
{
    public Guid Id { get; set; }
    public Guid WasteBinId { get; set; }
    public int FillLevelPercent { get; set; }
    public BinCondition Condition { get; set; }
    public string? Notes { get; set; }
    public Guid RecordedByUserId { get; set; }
    public string? RecordedByUserName { get; set; }
    public DateTime RecordedAt { get; set; }
}
