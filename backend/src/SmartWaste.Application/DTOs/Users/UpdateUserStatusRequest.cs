namespace SmartWaste.Application.DTOs.Users;

/// <summary>
/// Request payload for activating or deactivating an internal user account.
/// </summary>
public class UpdateUserStatusRequest
{
    public bool IsActive { get; set; }
}
