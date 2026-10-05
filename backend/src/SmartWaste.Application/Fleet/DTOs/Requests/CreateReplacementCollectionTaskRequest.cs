namespace SmartWaste.Application.Collection.DTOs.Requests;

public sealed class CreateReplacementCollectionTaskRequest
{
    public DateTime? ScheduledAt { get; set; }
    public string? ReplacementReason { get; set; }
    public string? HandlingNotes { get; set; }
    public string? SchedulingReason { get; set; }
}
