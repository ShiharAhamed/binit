namespace SmartWaste.Application.DTOs.Auth;

/// <summary>
/// Response payload for successful authentication (register/login).
/// </summary>
public class AuthResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool MustChangePassword { get; set; }
    public UserDto User { get; set; } = null!;
}

/// <summary>
/// Basic user information included in the authentication response.
/// </summary>
public class UserDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string Role { get; set; } = string.Empty;
    public bool MustChangePassword { get; set; }
}
