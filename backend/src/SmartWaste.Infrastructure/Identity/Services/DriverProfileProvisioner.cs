using Microsoft.EntityFrameworkCore;
using SmartWaste.Domain.Entities;
using SmartWaste.Infrastructure.Persistence;

namespace SmartWaste.Infrastructure.Identity.Services;

/// <summary>
/// Creates the internal availability extension for a Driver-role account.
/// The profile is not an administratively managed Driver record.
/// </summary>
public static class DriverProfileProvisioner
{
    public static async Task EnsureAsync(AppDbContext db, Guid driverUserId, CancellationToken cancellationToken = default)
    {
        if (await db.DriverProfiles.AnyAsync(profile => profile.UserId == driverUserId, cancellationToken))
        {
            return;
        }

        var profile = new DriverProfile
        {
            UserId = driverUserId,
            LicenseNumber = null,
            CreatedAt = DateTime.UtcNow
        };

        db.DriverProfiles.Add(profile);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            db.Entry(profile).State = EntityState.Detached;
            if (!await db.DriverProfiles.AnyAsync(existing => existing.UserId == driverUserId, cancellationToken))
            {
                throw;
            }
        }
    }
}
