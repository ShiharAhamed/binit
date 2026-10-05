using SmartWaste.Domain.Collection.Enums;

namespace SmartWaste.Application.Collection.DTOs.Requests;

/// <summary>
/// Request payload for recording an append-only manual field observation for a bin (POST /api/v1/bins/{id}/observations).
/// Server-controlled fields (RecordedAt, RecordedByUserId) are strictly forbidden on client input.
/// </summary>
public class RecordBinObservationRequest
{
    public int? FillLevelPercent { get; set; }
    public BinCondition? Condition { get; set; }
    public string? Notes { get; set; }
}
