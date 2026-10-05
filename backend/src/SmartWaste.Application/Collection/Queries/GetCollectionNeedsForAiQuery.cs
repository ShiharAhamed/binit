namespace SmartWaste.Application.Collection.Queries;

/// <summary>
/// Bounded, allow-listed query parameters for the internal AI collection-needs tool.
/// This intentionally excludes staff-only search and waste-type filters.
/// </summary>
public class GetCollectionNeedsForAiQuery
{
    public string? TargetType { get; set; }
    public string? CollectionReason { get; set; }

    /// <summary>
    /// Optional municipality-local calendar date used only for routine-collection evaluation.
    /// Current report, bin, observation, and task data are always read authoritatively.
    /// </summary>
    public DateOnly? TargetDate { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
