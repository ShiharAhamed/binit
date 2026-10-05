namespace SmartWaste.Application.Workflow.DTOs.Requests;

/// <summary>
/// Query parameters for listing and filtering AgentWorkflows with bounded pagination.
/// </summary>
public class AgentWorkflowListQuery
{
    private const int MaxPageSize = 50;
    private int _pageSize = 20;

    /// <summary>
    /// Page number (1-based, minimum 1).
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// Number of items per page (1 to 50, default 20).
    /// </summary>
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value > MaxPageSize ? MaxPageSize : (value < 1 ? 1 : value);
    }

    /// <summary>
    /// Optional status filter to restrict results to a specific AgentWorkflowStatus.
    /// </summary>
    public string? Status { get; set; }
}
