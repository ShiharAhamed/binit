namespace SmartWaste.Application.Reporting;

/// <summary>Display-only reference; the full Guid remains the authoritative identity.</summary>
public static class WasteReportReference
{
    public static string FromId(Guid id) => id.ToString("N")[..8].ToUpperInvariant();
}
