namespace SmartWaste.Application.Collection.Queries;

/// <summary>
/// Pagination query parameters for retrieving chronological observation history for a bin (GET /api/v1/bins/{id}/observations).
/// </summary>
public class ObservationListQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
