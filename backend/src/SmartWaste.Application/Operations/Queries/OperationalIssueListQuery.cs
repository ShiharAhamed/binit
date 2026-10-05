using SmartWaste.Domain.Operations.Enums;

namespace SmartWaste.Application.Operations.Queries;

/// <summary>
/// Query filter, search, sort, and pagination parameters for listing operational issues (GET /api/v1/operations/issues).
/// </summary>
public class OperationalIssueListQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public OperationalIssueStatus? Status { get; set; }
    public OperationalIssueType? IssueType { get; set; }
    public string? Search { get; set; }
    public string? SortBy { get; set; } = "createdAt";
    public string? SortDirection { get; set; } = "desc";
}
