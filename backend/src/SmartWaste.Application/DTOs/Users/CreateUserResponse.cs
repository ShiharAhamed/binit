namespace SmartWaste.Application.DTOs.Users;

/// <summary>
/// Response payload for successful internal user creation,
/// returning user metadata and the one-time temporary password.
/// </summary>
public class CreateUserResponse
{
    public UserManagementDto User { get; set; } = null!;
    public string TemporaryPassword { get; set; } = string.Empty;
}
