namespace SmartWaste.Application.Reporting.DTOs.Responses;

/// <summary>
/// Photographic attachment response DTO. Exposes FileUrl for client rendering.
/// Internal StorageKey is not exposed.
/// </summary>
public class ReportAttachmentDto
{
    public Guid Id { get; set; }
    public Guid WasteReportId { get; set; }
    public string FileUrl { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
