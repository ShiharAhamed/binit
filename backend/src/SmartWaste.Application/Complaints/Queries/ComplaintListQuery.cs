using SmartWaste.Domain.Complaints.Enums;

namespace SmartWaste.Application.Complaints.Queries;

/// <summary>
/// Query filter, search, sort, and pagination parameters for listing complaints (GET /api/v1/complaints).
/// </summary>
public class ComplaintListQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public ComplaintStatus? Status { get; set; }
    public ComplaintCategory? Category { get; set; }
    public string? Search { get; set; }
    public string? SortBy { get; set; } = "createdAt";
    public string? SortDirection { get; set; } = "desc";
}
