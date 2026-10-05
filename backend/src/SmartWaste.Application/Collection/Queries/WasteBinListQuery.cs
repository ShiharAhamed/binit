using SmartWaste.Domain.Collection.Enums;
using SmartWaste.Domain.Reporting.Enums;

namespace SmartWaste.Application.Collection.Queries;

/// <summary>
/// Operational filter, search, and pagination parameters for internal bin management (GET /api/v1/bins).
/// Used by WasteOfficers and MunicipalManagers.
/// </summary>
public class WasteBinListQuery
{
    public BinAdministrativeStatus? Status { get; set; }
    public WasteType? WasteType { get; set; }
    public BinCondition? Condition { get; set; }
    public int? MinFillLevel { get; set; }
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
