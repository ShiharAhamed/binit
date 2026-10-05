namespace SmartWaste.Application.DTOs.Users;

/// <summary>
/// Representation of an internal user account for administrative management.
/// Excludes sensitive credentials such as password hashes and security stamps.
/// </summary>
public class UserManagementDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool MustChangePassword { get; set; }
    public DateTime CreatedAt { get; set; }
}
