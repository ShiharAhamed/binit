namespace SmartWaste.Application.DTOs.Auth;

/// <summary>
/// Payload for public citizen registration.
/// Note: Role selection is strictly disallowed; registrations are always assigned the Citizen role.
/// </summary>
public class RegisterRequest
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
