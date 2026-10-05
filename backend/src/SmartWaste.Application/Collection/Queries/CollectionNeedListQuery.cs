using SmartWaste.Domain.Reporting.Enums;

namespace SmartWaste.Application.Collection.Queries;

/// <summary>
/// Filter, search, and pagination parameters for the unified collection needs queue (GET /api/v1/collection-needs).
/// </summary>
public class CollectionNeedListQuery
{
    public string? TargetType { get; set; }
    public string? CollectionReason { get; set; }
    public WasteType? WasteType { get; set; }
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
