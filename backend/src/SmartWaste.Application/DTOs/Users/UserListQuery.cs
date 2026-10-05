namespace SmartWaste.Application.DTOs.Users;

/// <summary>
/// Query filter and pagination parameters for listing internal users.
/// </summary>
public class UserListQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
    public string? Role { get; set; }
    public bool? IsActive { get; set; }
}
