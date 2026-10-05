namespace SmartWaste.Domain.Reporting.Enums;

/// <summary>
/// Lifecycle status of a citizen waste report.
/// </summary>
public enum WasteReportStatus
{
    Submitted,
    UnderReview,
    Verified,
    Rejected,
    Scheduled,
    InProgress,
    Resolved,
    Cancelled
}
