namespace SmartWaste.Domain.Collection.Enums;

/// <summary>
/// Lifecycle status of an authoritative collection task.
/// </summary>
public enum CollectionTaskStatus
{
    Scheduled,
    Assigned,
    InProgress,
    Completed,
    Failed,
    Cancelled
}
