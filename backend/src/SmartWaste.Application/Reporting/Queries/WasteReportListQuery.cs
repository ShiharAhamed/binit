using SmartWaste.Domain.Reporting.Enums;

namespace SmartWaste.Application.Reporting.Queries;

/// <summary>
/// Query filter, search, sort, and pagination parameters for listing waste reports (GET /api/v1/waste-reports).
/// </summary>
public class WasteReportListQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public WasteReportStatus? Status { get; set; }
    public WasteType? WasteType { get; set; }
    public string? Search { get; set; }
    public string? SortBy { get; set; } = "createdAt";
    public string? SortDirection { get; set; } = "desc";
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}
