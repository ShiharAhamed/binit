using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SmartWaste.Api.Controllers.Fleet;
using SmartWaste.Application.Fleet.DTOs.Requests;
using SmartWaste.Application.Fleet.DTOs.Responses;
using SmartWaste.Application.Fleet.Interfaces;
using SmartWaste.Domain.Collection.Enums;
using SmartWaste.Domain.Common;

namespace SmartWaste.Tests.Collection.Controllers;

public class FleetControllersTests
{
    private static T WithActor<T>(T controller, Guid id, string role) where T : ControllerBase
    {
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, id.ToString()), new Claim(ClaimTypes.Role, role) }, "test")) } };
        return controller;
    }

    [Fact]
    public async Task VehicleCreate_UsesManagerActorAndReturnsCreated()
    {
        var actor = Guid.NewGuid(); var service = new Mock<IVehicleService>();
        service.Setup(x => x.CreateAsync(It.IsAny<CreateVehicleRequest>(), actor, AppRoles.MunicipalManager, It.IsAny<CancellationToken>())).ReturnsAsync(new VehicleDetailDto { Id = Guid.NewGuid() });
        var controller = WithActor(new VehiclesController(service.Object), actor, AppRoles.MunicipalManager);
        var result = await controller.Create(new CreateVehicleRequest { RegistrationNumber = "V-1" }, CancellationToken.None);
        Assert.IsType<CreatedAtActionResult>(result); service.VerifyAll();
    }

    [Fact]
    public async Task DriverSelfDetail_UsesActorIdAndCannotUseManagerDto()
    {
        var actor = Guid.NewGuid(); var service = new Mock<IDriverProfileService>();
        service.Setup(x => x.GetSelfAsync(actor, AppRoles.Driver, It.IsAny<CancellationToken>())).ReturnsAsync(new DriverSelfDto { Id = actor, AvailabilityStatus = DriverAvailabilityStatus.Available });
        var controller = WithActor(new DriversController(service.Object), actor, AppRoles.Driver);
        var result = await controller.GetById(actor, CancellationToken.None);
        Assert.IsType<OkObjectResult>(result); service.VerifyAll();
    }

    [Fact]
    public async Task DriverSelfDetail_RejectsAnotherDriverIdBeforeServiceCall()
    {
        var controller = WithActor(new DriversController(new Mock<IDriverProfileService>().Object), Guid.NewGuid(), AppRoles.Driver);
        await Assert.ThrowsAsync<SmartWaste.Application.Common.Exceptions.ForbiddenException>(() => controller.GetById(Guid.NewGuid(), CancellationToken.None));
    }
}
