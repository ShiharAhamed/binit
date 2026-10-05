using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using SmartWaste.Domain.Collection.Entities;
using SmartWaste.Domain.Collection.Enums;
using SmartWaste.Domain.Entities;
using SmartWaste.Domain.Reporting.Enums;
using SmartWaste.Infrastructure.Persistence;

namespace SmartWaste.Tests.Collection.Persistence;

/// <summary>
/// Model-level coverage for the C3.3a DriverProfile and Vehicle persistence foundation.
/// PostgreSQL enforcement is intentionally not executed until the generated migration is approved.
/// </summary>
public class DriverProfileVehiclePersistenceModelTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"SmartWaste_C3_Model_{Guid.NewGuid():N}")
            .Options;

        return new AppDbContext(options);
    }

    private static IModel GetDesignTimeModel(AppDbContext db) =>
        db.GetService<IDesignTimeModel>().Model;

    [Fact]
    public void DriverProfile_Defaults_UseAvailableDutyIndicationWithoutLicence()
    {
        var profile = new DriverProfile();

        profile.AvailabilityStatus.Should().Be(DriverAvailabilityStatus.Available);
        profile.LicenseNumber.Should().BeNull();
    }

    [Fact]
    public void Vehicle_Defaults_UseAvailableOperationalStatus()
    {
        var vehicle = new Vehicle();

        vehicle.OperationalStatus.Should().Be(VehicleOperationalStatus.Available);
        Enum.GetValues<VehicleOperationalStatus>().Should().BeEquivalentTo(new[]
        {
            VehicleOperationalStatus.Available,
            VehicleOperationalStatus.Maintenance,
            VehicleOperationalStatus.Inactive
        });
    }

    [Fact]
    public void DriverProfile_Model_UsesOneToOneIdentityExtensionAndNullableUniqueLicense()
    {
        using var db = CreateContext();
        var entity = GetDesignTimeModel(db).FindEntityType(typeof(DriverProfile))!;

        entity.FindPrimaryKey()!.Properties.Select(p => p.Name).Should().Equal(nameof(DriverProfile.UserId));
        entity.FindProperty(nameof(DriverProfile.LicenseNumber))!.IsNullable.Should().BeTrue();
        entity.FindProperty(nameof(DriverProfile.LicenseNumber))!.GetMaxLength().Should().Be(50);
        entity.FindProperty(nameof(DriverProfile.EligibilityNotes))!.GetMaxLength().Should().Be(500);
        entity.GetIndexes().Should().ContainSingle(index =>
            index.IsUnique && index.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(DriverProfile.LicenseNumber) }));

        var foreignKey = entity.GetForeignKeys().Should().ContainSingle().Subject;
        foreignKey.PrincipalEntityType.ClrType.Should().Be(typeof(AppUser));
        foreignKey.Properties.Select(p => p.Name).Should().Equal(nameof(DriverProfile.UserId));
        foreignKey.DeleteBehavior.Should().Be(DeleteBehavior.Cascade);
        entity.GetCheckConstraints().Should().ContainSingle(constraint =>
            constraint.Name == "CK_DriverProfiles_AvailabilityStatus" &&
            constraint.Sql == "\"AvailabilityStatus\" IN ('Available', 'OffDuty')");
    }

    [Fact]
    public void Vehicle_Model_UsesLitresStatusesAndSupportedWasteTypeJoin()
    {
        using var db = CreateContext();
        var model = GetDesignTimeModel(db);
        var vehicle = model.FindEntityType(typeof(Vehicle))!;
        var supportedWasteType = model.FindEntityType(typeof(VehicleSupportedWasteType))!;

        vehicle.FindProperty(nameof(Vehicle.RegistrationNumber))!.GetMaxLength().Should().Be(50);
        vehicle.FindProperty(nameof(Vehicle.CapacityLiters))!.IsNullable.Should().BeFalse();
        vehicle.FindProperty(nameof(Vehicle.Notes))!.GetMaxLength().Should().Be(1000);
        vehicle.GetIndexes().Should().ContainSingle(index =>
            index.IsUnique && index.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(Vehicle.RegistrationNumber) }));
        vehicle.GetCheckConstraints().Should().ContainSingle(constraint =>
            constraint.Name == "CK_Vehicles_CapacityLiters" && constraint.Sql == "\"CapacityLiters\" > 0");
        vehicle.GetCheckConstraints().Should().ContainSingle(constraint =>
            constraint.Name == "CK_Vehicles_VehicleType" &&
            constraint.Sql == "\"VehicleType\" IN ('Compactor', 'Flatbed', 'Tipper', 'SmallVan')");
        vehicle.GetCheckConstraints().Should().ContainSingle(constraint =>
            constraint.Name == "CK_Vehicles_OperationalStatus" &&
            constraint.Sql == "\"OperationalStatus\" IN ('Available', 'Maintenance', 'Inactive')");

        supportedWasteType.FindPrimaryKey()!.Properties.Select(p => p.Name)
            .Should().Equal(nameof(VehicleSupportedWasteType.VehicleId), nameof(VehicleSupportedWasteType.WasteType));
        supportedWasteType.FindProperty(nameof(VehicleSupportedWasteType.WasteType))!.GetMaxLength().Should().Be(50);
        supportedWasteType.GetForeignKeys().Should().ContainSingle(foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(Vehicle) &&
            foreignKey.DeleteBehavior == DeleteBehavior.Cascade);
    }

    [Fact]
    public async Task DriverProfile_AndVehicleCompatibilityData_CanUseExistingIdentityAndWasteTypeModels()
    {
        await using var db = CreateContext();
        var driver = new AppUser
        {
            Id = Guid.NewGuid(),
            FullName = "C3 Test Driver",
            UserName = "c3.driver@example.test",
            NormalizedUserName = "C3.DRIVER@EXAMPLE.TEST",
            Email = "c3.driver@example.test",
            NormalizedEmail = "C3.DRIVER@EXAMPLE.TEST"
        };
        var vehicle = new Vehicle
        {
            RegistrationNumber = "C3-TEST-001",
            VehicleType = VehicleType.Compactor,
            CapacityLiters = 12000,
            SupportedWasteTypes =
            {
                new VehicleSupportedWasteType { WasteType = WasteType.General },
                new VehicleSupportedWasteType { WasteType = WasteType.Recyclable }
            }
        };

        db.Add(new DriverProfile
        {
            UserId = driver.Id,
            User = driver,
            LicenseNumber = "C3-LICENSE-001"
        });
        db.Add(vehicle);
        await db.SaveChangesAsync();

        (await db.DriverProfiles.Include(profile => profile.User).SingleAsync()).User!.Id.Should().Be(driver.Id);
        (await db.Vehicles.Include(item => item.SupportedWasteTypes).SingleAsync()).SupportedWasteTypes
            .Select(item => item.WasteType)
            .Should().BeEquivalentTo(new[] { WasteType.General, WasteType.Recyclable });
    }
}
