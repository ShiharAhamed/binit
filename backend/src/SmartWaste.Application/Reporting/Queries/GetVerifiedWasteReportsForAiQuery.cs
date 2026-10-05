namespace SmartWaste.Application.Reporting.Queries;

/// <summary>
/// Bounded query parameters for internal AI retrieval of verified waste reports.
/// Exposes strictly page and pageSize; does not allow caller-selected status, arbitrary filters, or sorting.
/// </summary>
public class GetVerifiedWasteReportsForAiQuery
{
    /// <summary>
    /// 1-based page number. Defaults to 1.
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// Page size between 1 and 50. Defaults to 20.
    /// </summary>
    public int PageSize { get; set; } = 20;
}
