namespace SmartWaste.Application.Collection.DTOs.Responses;

/// <summary>
/// Dedicated audit record capturing reschedule events for unstarted collection tasks.
/// </summary>
public class CollectionTaskScheduleHistoryDto
{
    public Guid Id { get; set; }
    public DateTime PreviousScheduledAt { get; set; }
    public DateTime NewScheduledAt { get; set; }
    public string Reason { get; set; } = string.Empty;
    public Guid RescheduledByUserId { get; set; }
    public string? RescheduledByUserName { get; set; }
    public DateTime RescheduledAt { get; set; }
}
