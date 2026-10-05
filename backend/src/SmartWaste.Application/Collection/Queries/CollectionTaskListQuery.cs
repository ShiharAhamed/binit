using SmartWaste.Domain.Collection.Enums;

namespace SmartWaste.Application.Collection.Queries;

/// <summary>
/// Operational filter, date range, and pagination parameters for listing collection tasks (GET /api/v1/collection-tasks).
/// </summary>
public class CollectionTaskListQuery
{
    public CollectionTaskStatus? Status { get; set; }
    public string? TargetType { get; set; }
    public CollectionReason? CollectionReason { get; set; }
    // Date-only filters represent municipal calendar days. The service converts
    // them to UTC timestamptz boundaries before querying ScheduledAt.
    public DateOnly? DateFrom { get; set; }
    public DateOnly? DateTo { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
