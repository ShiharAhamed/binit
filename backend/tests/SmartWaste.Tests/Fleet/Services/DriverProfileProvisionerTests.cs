using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SmartWaste.Domain.Collection.Enums;
using SmartWaste.Domain.Entities;
using SmartWaste.Infrastructure.Identity.Services;
using SmartWaste.Infrastructure.Persistence;

namespace SmartWaste.Tests.Fleet.Services;

public class DriverProfileProvisionerTests
{
    private static AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase($"SmartWaste_DriverProfileProvisioner_{Guid.NewGuid():N}")
        .Options);

    [Fact]
    public async Task EnsureAsync_CreatesOneAvailableProfileWithoutALicence()
    {
        await using var db = CreateContext();
        var driverId = Guid.NewGuid();

        await DriverProfileProvisioner.EnsureAsync(db, driverId);
        await DriverProfileProvisioner.EnsureAsync(db, driverId);

        var profile = await db.DriverProfiles.SingleAsync();
        profile.UserId.Should().Be(driverId);
        profile.LicenseNumber.Should().BeNull();
        profile.AvailabilityStatus.Should().Be(DriverAvailabilityStatus.Available);
    }

    [Fact]
    public async Task EnsureAsync_PreservesExistingDriverAvailabilityAndHistoricalFields()
    {
        await using var db = CreateContext();
        var driverId = Guid.NewGuid();
        db.DriverProfiles.Add(new DriverProfile
        {
            UserId = driverId,
            LicenseNumber = "HISTORICAL-DRIVER-LICENCE",
            AvailabilityStatus = DriverAvailabilityStatus.OffDuty,
            IsEligible = false,
            EligibilityNotes = "Historical record"
        });
        await db.SaveChangesAsync();

        await DriverProfileProvisioner.EnsureAsync(db, driverId);

        var profile = await db.DriverProfiles.SingleAsync();
        profile.AvailabilityStatus.Should().Be(DriverAvailabilityStatus.OffDuty);
        profile.LicenseNumber.Should().Be("HISTORICAL-DRIVER-LICENCE");
        profile.IsEligible.Should().BeFalse();
        profile.EligibilityNotes.Should().Be("Historical record");
    }
}
