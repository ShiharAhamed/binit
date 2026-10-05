using SmartWaste.Domain.Entities;

namespace SmartWaste.Domain.Collection.Entities;

/// <summary>
/// Dedicated audit entity capturing changes to the planned execution time of an unstarted (Scheduled) collection task.
/// </summary>
public class CollectionTaskScheduleHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CollectionTaskId { get; set; }
    public CollectionTask? CollectionTask { get; set; }

    public DateTime PreviousScheduledAt { get; set; }

    public DateTime NewScheduledAt { get; set; }

    public string Reason { get; set; } = string.Empty;

    public Guid RescheduledByUserId { get; set; }
    public AppUser? RescheduledByUser { get; set; }

    public DateTime RescheduledAt { get; set; } = DateTime.UtcNow;
}
