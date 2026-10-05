using SmartWaste.Domain.Collection.Enums;
using SmartWaste.Domain.Entities;

namespace SmartWaste.Domain.Collection.Entities;

/// <summary>
/// Chronological audit record tracking every lifecycle status transition of a CollectionTask.
/// </summary>
public class CollectionTaskStatusHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CollectionTaskId { get; set; }
    public CollectionTask? CollectionTask { get; set; }

    public CollectionTaskStatus? FromStatus { get; set; }

    public CollectionTaskStatus ToStatus { get; set; }

    public Guid? ChangedByUserId { get; set; }
    public AppUser? ChangedByUser { get; set; }

    public string? Notes { get; set; }

    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}
