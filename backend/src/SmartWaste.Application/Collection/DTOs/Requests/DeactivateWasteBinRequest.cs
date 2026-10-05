using SmartWaste.Domain.Collection.Enums;

namespace SmartWaste.Application.Collection.DTOs.Requests;

/// <summary>
/// Request payload for deactivating or retiring a registered bin (POST /api/v1/bins/{id}/deactivate).
/// TargetStatus must be OutOfService or Retired.
/// </summary>
public class DeactivateWasteBinRequest
{
    public BinAdministrativeStatus? TargetStatus { get; set; }
    public string? Reason { get; set; }
}
