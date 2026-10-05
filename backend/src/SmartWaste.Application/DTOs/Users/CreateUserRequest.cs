namespace SmartWaste.Application.DTOs.Users;

/// <summary>
/// Request payload for creating an internal staff user (Driver, WasteOfficer, MunicipalManager).
/// Citizen accounts are strictly rejected from internal creation.
/// </summary>
public class CreateUserRequest
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}
