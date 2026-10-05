namespace SmartWaste.Application.Collection.DTOs.Requests;

public sealed class CancelCollectionAssignmentRequest
{
    public string Reason { get; set; } = string.Empty;
}
