using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SmartWaste.Api.Authentication.InternalService;
using SmartWaste.Api.Controllers.Internal;
using SmartWaste.Application.Fleet.DTOs.Requests;
using SmartWaste.Application.Fleet.DTOs.Responses;
using SmartWaste.Application.Fleet.Interfaces;
using SmartWaste.Application.Fleet.Queries;
using Xunit;

namespace SmartWaste.Tests.Fleet.Controllers;

public class AiToolsFleetPlanningControllerUnitTests
{
    private readonly Mock<IFleetPlanningAiService> _service = new();

    private AiToolsFleetPlanningController CreateController()
    {
        return new AiToolsFleetPlanningController(_service.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    [Fact]
    public void Controller_RequiresInternalServicePolicy()
    {
        var authorization = typeof(AiToolsFleetPlanningController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Should()
            .ContainSingle()
            .Subject;

        authorization.Policy.Should().Be(InternalServiceDefaults.PolicyName);
    }

    [Fact]
    public async Task GetFleetPlanningContext_ValidQuery_ReturnsOkResultWithContext()
    {
        var query = new GetFleetPlanningContextForAiQuery { Page = 1, PageSize = 20 };
        var expected = new FleetPlanningContextDto
        {
            Tasks = new List<FleetPlanningTaskDto>(),
            Drivers = new List<FleetPlanningDriverDto>(),
            Vehicles = new List<FleetPlanningVehicleDto>(),
            TaskPage = 1,
            TaskPageSize = 20,
            TaskTotalCount = 0
        };

        _service.Setup(s => s.GetPlanningContextAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var controller = CreateController();
        var result = await controller.GetFleetPlanningContext(query, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(expected);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(-1, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 51)]
    public async Task GetFleetPlanningContext_InvalidPagination_ReturnsBadRequest(int page, int pageSize)
    {
        var controller = CreateController();
        var result = await controller.GetFleetPlanningContext(
            new GetFleetPlanningContextForAiQuery { Page = page, PageSize = pageSize },
            CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var details = badRequest.Value.Should().BeOfType<ValidationProblemDetails>().Subject;
        details.Status.Should().Be(StatusCodes.Status400BadRequest);
        _service.Verify(s => s.GetPlanningContextAsync(It.IsAny<GetFleetPlanningContextForAiQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CheckFleetCompatibility_ValidRequest_ReturnsOkWithResult()
    {
        var request = new CheckFleetCompatibilityRequest
        {
            TaskIds = new[] { Guid.NewGuid() },
            VehicleId = Guid.NewGuid()
        };
        var expected = new FleetCompatibilityResultDto
        {
            Status = FleetCompatibilityStatus.Compatible,
            RequiresAcknowledgement = false,
            Issues = Array.Empty<string>()
        };

        _service.Setup(s => s.CheckCompatibilityAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var controller = CreateController();
        var result = await controller.CheckFleetCompatibility(request, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task CheckFleetCompatibility_EmptyTaskIds_ReturnsBadRequest()
    {
        var controller = CreateController();
        var result = await controller.CheckFleetCompatibility(
            new CheckFleetCompatibilityRequest { TaskIds = Array.Empty<Guid>(), VehicleId = Guid.NewGuid() },
            CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var details = badRequest.Value.Should().BeOfType<ValidationProblemDetails>().Subject;
        details.Errors.Should().ContainKey("TaskIds");
        _service.Verify(s => s.CheckCompatibilityAsync(It.IsAny<CheckFleetCompatibilityRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CheckFleetCompatibility_DuplicateTaskIds_ReturnsBadRequest()
    {
        var taskId = Guid.NewGuid();
        var controller = CreateController();
        var result = await controller.CheckFleetCompatibility(
            new CheckFleetCompatibilityRequest { TaskIds = new[] { taskId, taskId }, VehicleId = Guid.NewGuid() },
            CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var details = badRequest.Value.Should().BeOfType<ValidationProblemDetails>().Subject;
        details.Errors.Should().ContainKey("TaskIds");
    }

    [Fact]
    public async Task CheckFleetCompatibility_EmptyVehicleId_ReturnsBadRequest()
    {
        var controller = CreateController();
        var result = await controller.CheckFleetCompatibility(
            new CheckFleetCompatibilityRequest { TaskIds = new[] { Guid.NewGuid() }, VehicleId = Guid.Empty },
            CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var details = badRequest.Value.Should().BeOfType<ValidationProblemDetails>().Subject;
        details.Errors.Should().ContainKey("VehicleId");
    }

    [Fact]
    public async Task GetOperationalValidationContext_ValidRequest_ReturnsOkResultWithContext()
    {
        var request = new GetOperationalValidationContextRequest
        {
            TaskIds = new[] { Guid.NewGuid() },
            DriverIds = new[] { Guid.NewGuid() },
            VehicleIds = new[] { Guid.NewGuid() }
        };
        var expected = new OperationalValidationContextDto
        {
            Tasks = new List<OperationalValidationTaskDto>(),
            Drivers = new List<OperationalValidationDriverDto>(),
            Vehicles = new List<OperationalValidationVehicleDto>()
        };

        _service.Setup(s => s.GetOperationalValidationContextAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var controller = CreateController();
        var result = await controller.GetOperationalValidationContext(request, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GetOperationalValidationContext_DuplicateTaskIds_ReturnsBadRequest()
    {
        var taskId = Guid.NewGuid();
        var controller = CreateController();
        var result = await controller.GetOperationalValidationContext(
            new GetOperationalValidationContextRequest { TaskIds = new[] { taskId, taskId } },
            CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var details = badRequest.Value.Should().BeOfType<ValidationProblemDetails>().Subject;
        details.Errors.Should().ContainKey("TaskIds");
    }

    [Fact]
    public async Task GetOperationalValidationContext_DuplicateDriverIds_ReturnsBadRequest()
    {
        var driverId = Guid.NewGuid();
        var controller = CreateController();
        var result = await controller.GetOperationalValidationContext(
            new GetOperationalValidationContextRequest { DriverIds = new[] { driverId, driverId } },
            CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var details = badRequest.Value.Should().BeOfType<ValidationProblemDetails>().Subject;
        details.Errors.Should().ContainKey("DriverIds");
    }

    [Fact]
    public async Task GetOperationalValidationContext_DuplicateVehicleIds_ReturnsBadRequest()
    {
        var vehicleId = Guid.NewGuid();
        var controller = CreateController();
        var result = await controller.GetOperationalValidationContext(
            new GetOperationalValidationContextRequest { VehicleIds = new[] { vehicleId, vehicleId } },
            CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var details = badRequest.Value.Should().BeOfType<ValidationProblemDetails>().Subject;
        details.Errors.Should().ContainKey("VehicleIds");
    }

    [Fact]
    public async Task GetOperationalValidationContext_EmptyGuidInTaskIds_ReturnsBadRequest()
    {
        var controller = CreateController();
        var result = await controller.GetOperationalValidationContext(
            new GetOperationalValidationContextRequest { TaskIds = new[] { Guid.Empty } },
            CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var details = badRequest.Value.Should().BeOfType<ValidationProblemDetails>().Subject;
        details.Errors.Should().ContainKey("TaskIds[0]");
    }
}
