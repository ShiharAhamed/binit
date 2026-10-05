namespace SmartWaste.Application.DTOs.Auth;

/// <summary>
/// Response payload for GET /api/v1/auth/me containing the authenticated user's profile.
/// </summary>
public class CurrentUserResponse
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}
