namespace SmartWaste.Application.Collection.DTOs.Responses;

/// <summary>
/// Chronological lifecycle status transition record for a collection task.
/// </summary>
public class CollectionTaskStatusHistoryDto
{
    public Guid Id { get; set; }
    public string? FromStatus { get; set; }
    public string ToStatus { get; set; } = string.Empty;
    public Guid? ChangedByUserId { get; set; }
    public string? ChangedByUserName { get; set; }
    public string? Notes { get; set; }
    public DateTime ChangedAt { get; set; }
}
