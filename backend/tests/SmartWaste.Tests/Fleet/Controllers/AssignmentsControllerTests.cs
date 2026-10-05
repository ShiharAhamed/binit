using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SmartWaste.Api.Controllers.Collection;
using SmartWaste.Application.Collection.DTOs.Requests;
using SmartWaste.Application.Collection.DTOs.Responses;
using SmartWaste.Application.Collection.Interfaces;
using SmartWaste.Domain.Common;

namespace SmartWaste.Tests.Collection.Controllers;

public class AssignmentsControllerTests
{
    [Fact]
    public async Task Reorder_UsesAuthenticatedWasteOfficerAndReturnsRoute()
    {
        var actor = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        var request = new ReorderRouteStopsRequest { Stops = new[] { new RouteStopSequenceRequest { RouteStopId = Guid.NewGuid(), Sequence = 1 } } };
        var writes = new Mock<ICollectionAssignmentService>();
        writes.Setup(x => x.ReorderStopsAsync(assignmentId, request, actor, AppRoles.WasteOfficer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RouteReadDto { Id = Guid.NewGuid(), CollectionAssignmentId = assignmentId });
        var controller = WithActor(new AssignmentsController(new Mock<IAssignmentReadService>().Object, writes.Object), actor, AppRoles.WasteOfficer);

        var result = await controller.Reorder(assignmentId, request, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        writes.VerifyAll();
    }

    [Fact]
    public async Task Reorder_WithoutAuthenticatedActorReturnsUnauthorized()
    {
        var controller = new AssignmentsController(new Mock<IAssignmentReadService>().Object, new Mock<ICollectionAssignmentService>().Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal() } }
        };

        var result = await controller.Reorder(Guid.NewGuid(), new ReorderRouteStopsRequest(), CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public void Reorder_DeclaresWasteOfficerOnlyAuthorization()
    {
        var authorization = typeof(AssignmentsController).GetMethod(nameof(AssignmentsController.Reorder))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        authorization.Roles.Should().Be(AppRoles.WasteOfficer);
    }

    [Fact]
    public async Task Cancel_UsesAuthenticatedManagerAndReturnsAssignment()
    {
        var actor = Guid.NewGuid(); var assignmentId = Guid.NewGuid();
        var request = new CancelCollectionAssignmentRequest { Reason = "Unstarted assignment is no longer required." };
        var writes = new Mock<ICollectionAssignmentService>();
        writes.Setup(x => x.CancelAsync(assignmentId, request, actor, AppRoles.MunicipalManager, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AssignmentDetailDto { Id = assignmentId });
        var controller = WithActor(new AssignmentsController(new Mock<IAssignmentReadService>().Object, writes.Object), actor, AppRoles.MunicipalManager);

        var result = await controller.Cancel(assignmentId, request, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        writes.VerifyAll();
    }

    [Fact]
    public void Cancel_DeclaresAuthorizedStaffRoles()
    {
        var authorization = typeof(AssignmentsController).GetMethod(nameof(AssignmentsController.Cancel))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        authorization.Roles.Should().Be($"{AppRoles.WasteOfficer},{AppRoles.MunicipalManager}");
    }

    [Fact]
    public async Task Cancel_WithoutAuthenticatedActorReturnsUnauthorized()
    {
        var controller = new AssignmentsController(new Mock<IAssignmentReadService>().Object, new Mock<ICollectionAssignmentService>().Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal() } }
        };

        var result = await controller.Cancel(Guid.NewGuid(), new CancelCollectionAssignmentRequest { Reason = "No authenticated staff actor." }, CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task Start_UsesAuthenticatedDriverAndReturnsAssignment()
    {
        var actor = Guid.NewGuid(); var assignmentId = Guid.NewGuid(); var writes = new Mock<ICollectionAssignmentService>();
        writes.Setup(x => x.StartAsync(assignmentId, actor, AppRoles.Driver, It.IsAny<CancellationToken>())).ReturnsAsync(new AssignmentDetailDto { Id = assignmentId });
        var controller = WithActor(new AssignmentsController(new Mock<IAssignmentReadService>().Object, writes.Object), actor, AppRoles.Driver);

        var result = await controller.Start(assignmentId, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result); writes.VerifyAll();
    }

    [Fact]
    public void Start_DeclaresDriverOnlyAuthorization()
    {
        var authorization = typeof(AssignmentsController).GetMethod(nameof(AssignmentsController.Start))!.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single();
        authorization.Roles.Should().Be(AppRoles.Driver);
    }

    [Fact]
    public async Task Start_WithoutAuthenticatedActorReturnsUnauthorized()
    {
        var controller = new AssignmentsController(new Mock<IAssignmentReadService>().Object, new Mock<ICollectionAssignmentService>().Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal() } }
        };

        var result = await controller.Start(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task StopOutcomes_UseAuthenticatedDriverAndReturnAssignment()
    {
        var actor = Guid.NewGuid(); var assignmentId = Guid.NewGuid(); var stopId = Guid.NewGuid(); var writes = new Mock<ICollectionAssignmentService>();
        writes.Setup(x => x.CompleteStopAsync(assignmentId, stopId, actor, AppRoles.Driver, It.IsAny<CancellationToken>())).ReturnsAsync(new AssignmentDetailDto { Id = assignmentId });
        writes.Setup(x => x.FailStopAsync(assignmentId, stopId, It.Is<FailRouteStopRequest>(x => x.Reason == "Access was blocked."), actor, AppRoles.Driver, It.IsAny<CancellationToken>())).ReturnsAsync(new AssignmentDetailDto { Id = assignmentId });
        var controller = WithActor(new AssignmentsController(new Mock<IAssignmentReadService>().Object, writes.Object), actor, AppRoles.Driver);

        Assert.IsType<OkObjectResult>(await controller.CompleteStop(assignmentId, stopId, CancellationToken.None));
        Assert.IsType<OkObjectResult>(await controller.FailStop(assignmentId, stopId, new FailRouteStopRequest { Reason = "Access was blocked." }, CancellationToken.None));
        writes.VerifyAll();
    }

    [Theory]
    [InlineData(nameof(AssignmentsController.CompleteStop))]
    [InlineData(nameof(AssignmentsController.FailStop))]
    public void StopOutcomes_DeclareDriverOnlyAuthorization(string methodName)
    {
        var authorization = typeof(AssignmentsController).GetMethod(methodName)!.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single();
        authorization.Roles.Should().Be(AppRoles.Driver);
    }

    [Fact]
    public async Task Finalize_UsesAuthenticatedDriverAndReturnsAssignment()
    {
        var actor = Guid.NewGuid(); var assignmentId = Guid.NewGuid(); var writes = new Mock<ICollectionAssignmentService>();
        writes.Setup(x => x.FinalizeAsync(assignmentId, actor, AppRoles.Driver, It.IsAny<CancellationToken>())).ReturnsAsync(new AssignmentDetailDto { Id = assignmentId });
        var controller = WithActor(new AssignmentsController(new Mock<IAssignmentReadService>().Object, writes.Object), actor, AppRoles.Driver);

        Assert.IsType<OkObjectResult>(await controller.Finalize(assignmentId, CancellationToken.None));
        writes.VerifyAll();
    }

    [Fact]
    public void Finalize_DeclaresDriverOnlyAuthorization()
    {
        var authorization = typeof(AssignmentsController).GetMethod(nameof(AssignmentsController.Finalize))!.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single();
        authorization.Roles.Should().Be(AppRoles.Driver);
    }

    [Fact]
    public async Task RecordBinObservation_UsesAuthenticatedDriverAndReturns201()
    {
        var actor = Guid.NewGuid(); var assignmentId = Guid.NewGuid(); var stopId = Guid.NewGuid(); var request = new RecordBinObservationRequest { FillLevelPercent = 25, Condition = SmartWaste.Domain.Collection.Enums.BinCondition.Good }; var writes = new Mock<ICollectionAssignmentService>();
        writes.Setup(x => x.RecordDriverBinObservationAsync(assignmentId, stopId, request, actor, AppRoles.Driver, It.IsAny<CancellationToken>())).ReturnsAsync(new BinObservationDto { Id = Guid.NewGuid() });
        var result = await WithActor(new AssignmentsController(new Mock<IAssignmentReadService>().Object, writes.Object), actor, AppRoles.Driver).RecordBinObservation(assignmentId, stopId, request, CancellationToken.None);
        Assert.IsType<ObjectResult>(result).StatusCode.Should().Be(StatusCodes.Status201Created); writes.VerifyAll();
    }

    private static T WithActor<T>(T controller, Guid id, string role) where T : ControllerBase
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, id.ToString()),
                    new Claim(ClaimTypes.Role, role)
                }, "test"))
            }
        };
        return controller;
    }
}
