namespace SmartWaste.Application.Fleet.Queries;

/// <summary>
/// Bounded query parameters for the internal AI fleet planning context tool.
/// </summary>
public class GetFleetPlanningContextForAiQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
