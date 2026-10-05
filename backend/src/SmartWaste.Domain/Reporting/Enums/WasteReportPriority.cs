namespace SmartWaste.Domain.Reporting.Enums;

/// <summary>
/// Operational priority assigned to a waste report.
/// Priority is nullable until an authorized human selects it during verification.
/// </summary>
public enum WasteReportPriority
{
    Low,
    Medium,
    High,
    Urgent
}
