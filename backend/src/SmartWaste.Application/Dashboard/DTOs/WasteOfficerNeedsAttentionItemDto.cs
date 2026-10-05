namespace SmartWaste.Application.Dashboard.DTOs;

public enum WasteOfficerNeedsAttentionItemType
{
    WasteReport,
    Complaint
}

public sealed record WasteOfficerNeedsAttentionItemDto(
    Guid Id,
    WasteOfficerNeedsAttentionItemType ItemType,
    string Reference,
    DateTime CreatedAt,
    string SecondaryLabel,
    string SubmittedByName,
    string? AddressText);
