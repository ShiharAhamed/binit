namespace SmartWaste.Infrastructure.Reporting.Storage;

/// <summary>
/// Strongly-typed configuration options for Supabase Storage.
/// Bound from configuration section "Storage:Supabase".
/// </summary>
public class SupabaseStorageOptions
{
    public const string SectionName = "Storage:Supabase";

    /// <summary>
    /// Base URL of the Supabase project (e.g., "https://xyzcompany.supabase.co").
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Server-side secret key (e.g. sb_secret_...). Never exposed to clients or committed to VCS.
    /// </summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// Name of the private storage bucket. Defaults to "waste-report-attachments".
    /// </summary>
    public string Bucket { get; set; } = "waste-report-attachments";

    /// <summary>
    /// Expiry duration in seconds for generated signed read URLs. Defaults to 900 seconds (15 minutes).
    /// </summary>
    public int SignedUrlExpirySeconds { get; set; } = 900;
}
