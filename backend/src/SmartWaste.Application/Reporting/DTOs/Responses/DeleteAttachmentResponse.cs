namespace SmartWaste.Application.Reporting.DTOs.Responses;

/// <summary>
/// Confirmation response returned when a photographic attachment is successfully deleted.
/// Matches the frozen API contract: { "message": "Attachment removed successfully." }.
/// </summary>
public class DeleteAttachmentResponse
{
    public string Message { get; set; } = "Attachment removed successfully.";
}
