namespace SmartWaste.Application.DTOs.Auth;

/// <summary>
/// Payload for user login.
/// </summary>
public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string Password { get; set; } = string.Empty;
    public string ClientType { get; set; } = string.Empty;
}
