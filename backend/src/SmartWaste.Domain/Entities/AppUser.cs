using Microsoft.AspNetCore.Identity;

namespace SmartWaste.Domain.Entities;

/// <summary>
/// Core application user entity representing all system accounts.
/// Extends ASP.NET Core IdentityUser with common domain fields.
/// </summary>
public class AppUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool MustChangePassword { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Component 3 — optional one-to-one extension for accounts in the Driver role.
    // Existing accounts can remain valid before a municipal profile is provisioned.
    public DriverProfile? DriverProfile { get; set; }
}
