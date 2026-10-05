namespace SmartWaste.Application.Collection.DTOs.Responses;

/// <summary>
/// Complete audit trail of status transitions and reschedule events for a collection task (GET /api/v1/collection-tasks/{id}/history).
/// </summary>
public class CollectionTaskAuditTrailDto
{
    public Guid CollectionTaskId { get; set; }
    public string TaskCode { get; set; } = string.Empty;
    public IReadOnlyList<CollectionTaskStatusHistoryDto> StatusHistory { get; set; } = Array.Empty<CollectionTaskStatusHistoryDto>();
    public IReadOnlyList<CollectionTaskScheduleHistoryDto> ScheduleHistory { get; set; } = Array.Empty<CollectionTaskScheduleHistoryDto>();
}
