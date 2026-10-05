using SmartWaste.Domain.Collection.Enums;

namespace SmartWaste.Application.Collection.DTOs.Responses;

/// <summary>
/// Full operational details of a collection task (GET /api/v1/collection-tasks/{id}).
/// Used by WasteOfficers and MunicipalManagers.
/// </summary>
public class CollectionTaskDetailDto
{
    public Guid Id { get; set; }
    public string TaskCode { get; set; } = string.Empty;
    public string TargetType { get; set; } = string.Empty;
    public Guid? WasteReportId { get; set; }
    public Guid? WasteBinId { get; set; }
    public CollectionTaskTargetSummaryDto TargetSummary { get; set; } = new();
    public CollectionReason CollectionReason { get; set; }
    public CollectionTaskStatus Status { get; set; }
    public DateTime ScheduledAt { get; set; }
    public string? HandlingNotes { get; set; }
    public string? SchedulingReason { get; set; }
    public Guid CreatedByUserId { get; set; }
    public string? CreatedByUserName { get; set; }
    public TaskCreationMethod CreationMethod { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public IReadOnlyList<CollectionTaskStatusHistoryDto> StatusHistory { get; set; } = Array.Empty<CollectionTaskStatusHistoryDto>();
    public IReadOnlyList<CollectionTaskScheduleHistoryDto> ScheduleHistory { get; set; } = Array.Empty<CollectionTaskScheduleHistoryDto>();
}
