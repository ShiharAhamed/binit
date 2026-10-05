using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SmartWaste.Application.Common.Exceptions;
using SmartWaste.Application.Fleet.DTOs.Requests;
using SmartWaste.Domain.Collection.Enums;
using SmartWaste.Domain.Common;
using SmartWaste.Domain.Reporting.Enums;
using SmartWaste.Infrastructure.Fleet.Services;
using SmartWaste.Infrastructure.Persistence;

namespace SmartWaste.Tests.Collection.Services;

public class VehicleServiceTests
{
    private static AppDbContext Context() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Fact]
    public async Task Manager_CanCreateAndUpdateVehicleCompatibility()
    {
        await using var db = Context(); var service = new VehicleService(db);
        var created = await service.CreateAsync(new CreateVehicleRequest { RegistrationNumber = "V-001", VehicleType = VehicleType.Compactor, CapacityLiters = 1000, SupportedWasteTypes = new[] { WasteType.General } }, Guid.NewGuid(), AppRoles.MunicipalManager);
        var updated = await service.UpdateAsync(created.Id, new UpdateVehicleRequest { RegistrationNumber = "V-001", VehicleType = VehicleType.Tipper, CapacityLiters = 2000, SupportedWasteTypes = new[] { WasteType.Recyclable }, Notes = "Updated" }, Guid.NewGuid(), AppRoles.MunicipalManager);
        updated.CapacityLiters.Should().Be(2000); updated.SupportedWasteTypes.Should().Equal("Recyclable");
        (await db.VehicleSupportedWasteTypes.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task VehicleMutations_RejectNonManagersAndDuplicateRegistration()
    {
        await using var db = Context(); var service = new VehicleService(db);
        var request = new CreateVehicleRequest { RegistrationNumber = "V-001", VehicleType = VehicleType.Compactor, CapacityLiters = 1000, SupportedWasteTypes = new[] { WasteType.General } };
        await Assert.ThrowsAsync<ForbiddenException>(() => service.CreateAsync(request, Guid.NewGuid(), AppRoles.WasteOfficer));
        await service.CreateAsync(request, Guid.NewGuid(), AppRoles.MunicipalManager);
        await Assert.ThrowsAsync<BusinessRuleConflictException>(() => service.CreateAsync(request, Guid.NewGuid(), AppRoles.MunicipalManager));
    }
}
