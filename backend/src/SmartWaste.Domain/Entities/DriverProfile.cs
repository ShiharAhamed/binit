using SmartWaste.Domain.Collection.Enums;

namespace SmartWaste.Domain.Entities;

/// <summary>
/// Operational profile for an existing AppUser in the Driver role.
/// This is not a separate identity or authentication record.
/// </summary>
public class DriverProfile
{
    public Guid UserId { get; set; }

    /// <summary>
    /// Historical municipal licence information. It is no longer required for
    /// Driver provisioning, dispatch, or collection execution.
    /// </summary>
    public string? LicenseNumber { get; set; }

    /// <summary>
    /// Driver-declared duty indication. Assignment occupancy remains derived
    /// from future CollectionAssignment records, not this value.
    /// </summary>
    public DriverAvailabilityStatus AvailabilityStatus { get; set; } = DriverAvailabilityStatus.Available;

    /// <summary>
    /// Historical administrative eligibility data retained for existing rows.
    /// It is not an active dispatch gate.
    /// </summary>
    public bool IsEligible { get; set; } = true;

    public string? EligibilityNotes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public AppUser? User { get; set; }
}
