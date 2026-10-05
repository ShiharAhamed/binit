namespace SmartWaste.Domain.Operations.Enums;

/// <summary>
/// Lifecycle status of an operational issue reported by a driver.
/// Reported -> InReview -> Resolved
/// </summary>
public enum OperationalIssueStatus
{
    Reported = 0,
    InReview = 1,
    Resolved = 2
}
