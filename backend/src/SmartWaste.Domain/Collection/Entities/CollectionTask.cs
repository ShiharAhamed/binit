using SmartWaste.Domain.Collection.Enums;
using SmartWaste.Domain.Entities;
using SmartWaste.Domain.Reporting.Entities;

namespace SmartWaste.Domain.Collection.Entities;

/// <summary>
/// Authoritative unit of collection work dispatched to clear a verified citizen waste report OR empty a roadside bin.
/// </summary>
public class CollectionTask
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string TaskCode { get; set; } = string.Empty;

    public Guid? WasteReportId { get; set; }
    public WasteReport? WasteReport { get; set; }

    public Guid? WasteBinId { get; set; }
    public WasteBin? WasteBin { get; set; }

    public CollectionReason CollectionReason { get; set; }

    public CollectionTaskStatus Status { get; set; } = CollectionTaskStatus.Scheduled;

    public DateTime ScheduledAt { get; set; }

    public string? HandlingNotes { get; set; }

    public string? SchedulingReason { get; set; }

    public Guid CreatedByUserId { get; set; }
    public AppUser? CreatedByUser { get; set; }

    public TaskCreationMethod CreationMethod { get; set; } = TaskCreationMethod.Manual;

    public Guid? TriggerObservationId { get; set; }
    public BinObservation? TriggerObservation { get; set; }

    public DateOnly? RoutineDueDate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public ICollection<CollectionTaskStatusHistory> StatusHistory { get; set; } = new List<CollectionTaskStatusHistory>();
    public ICollection<CollectionTaskScheduleHistory> ScheduleHistory { get; set; } = new List<CollectionTaskScheduleHistory>();
}
