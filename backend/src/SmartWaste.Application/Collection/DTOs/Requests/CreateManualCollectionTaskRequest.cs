using SmartWaste.Domain.Collection.Enums;

namespace SmartWaste.Application.Collection.DTOs.Requests;

/// <summary>
/// Request payload for manual collection task creation by a WasteOfficer (POST /api/v1/collection-tasks/manual).
/// Exactly one of WasteReportId or WasteBinId must be supplied.
/// Server-controlled fields (TaskCode, CreatedByUserId, CreationMethod, Status, audit fields) are strictly forbidden.
/// </summary>
public class CreateManualCollectionTaskRequest
{
    public Guid? WasteReportId { get; set; }
    public Guid? WasteBinId { get; set; }
    public CollectionReason? CollectionReason { get; set; }
    public DateTime? ScheduledAt { get; set; }
    public string? HandlingNotes { get; set; }
    public string? SchedulingReason { get; set; }
}
