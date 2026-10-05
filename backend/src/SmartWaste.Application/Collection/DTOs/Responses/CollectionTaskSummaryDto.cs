using SmartWaste.Domain.Collection.Enums;

namespace SmartWaste.Application.Collection.DTOs.Responses;

/// <summary>
/// Operational summary DTO for paginated collection task list views (GET /api/v1/collection-tasks).
/// </summary>
public class CollectionTaskSummaryDto
{
    public Guid Id { get; set; }
    public string TaskCode { get; set; } = string.Empty;
    public string TargetType { get; set; } = string.Empty;
    public Guid? WasteReportId { get; set; }
    public Guid? WasteBinId { get; set; }
    public string TargetReference { get; set; } = string.Empty;
    public CollectionReason CollectionReason { get; set; }
    public CollectionTaskStatus Status { get; set; }
    public DateTime ScheduledAt { get; set; }
    public TaskCreationMethod CreationMethod { get; set; }
    public Guid CreatedByUserId { get; set; }
    public string? CreatedByUserName { get; set; }
    public DateTime CreatedAt { get; set; }
}
