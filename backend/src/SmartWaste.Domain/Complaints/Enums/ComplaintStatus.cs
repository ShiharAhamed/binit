namespace SmartWaste.Domain.Complaints.Enums;

/// <summary>
/// Lifecycle status of a citizen service complaint.
/// Submitted -> InReview -> Resolved
/// </summary>
public enum ComplaintStatus
{
    Submitted = 0,
    InReview = 1,
    Resolved = 2
}
