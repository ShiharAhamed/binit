namespace SmartWaste.Application.DTOs.Auth;

/// <summary>
/// Payload for changing account password.
/// </summary>
public class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}
