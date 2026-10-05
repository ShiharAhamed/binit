namespace SmartWaste.Application.Collection.DTOs.Requests;

/// <summary>
/// Request payload for rescheduling an unstarted collection task (POST /api/v1/collection-tasks/{id}/reschedule).
/// Server-controlled audit and actor fields are strictly forbidden on client input.
/// </summary>
public class RescheduleCollectionTaskRequest
{
    public DateTime? NewScheduledAt { get; set; }
    public string Reason { get; set; } = string.Empty;
}
