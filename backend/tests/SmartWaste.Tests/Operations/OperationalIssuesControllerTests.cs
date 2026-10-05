using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SmartWaste.Api.Controllers;
using SmartWaste.Application.Common.Models;
using SmartWaste.Application.Operations.DTOs.Requests;
using SmartWaste.Application.Operations.DTOs.Responses;
using SmartWaste.Application.Operations.Interfaces;
using SmartWaste.Application.Operations.Queries;
using SmartWaste.Domain.Common;
using SmartWaste.Domain.Operations.Enums;
using Xunit;

namespace SmartWaste.Tests.Operations;

public class OperationalIssuesControllerTests
{
    private readonly Mock<IOperationalIssueService> _issueServiceMock = new();
    private readonly OperationalIssuesController _controller;

    public OperationalIssuesControllerTests()
    {
        _controller = new OperationalIssuesController(_issueServiceMock.Object);
    }

    private static T WithActor<T>(T controller, Guid actorId, string role) where T : ControllerBase
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, actorId.ToString()),
            new Claim(ClaimTypes.Role, role)
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
        return controller;
    }

    private static T WithAnonymous<T>(T controller) where T : ControllerBase
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal() }
        };
        return controller;
    }

    // =========================================================================
    // 1. Authorization Attributes & Roles Declaration
    // =========================================================================

    [Fact]
    public void Controller_HasAuthorizeAttribute()
    {
        var classAuthorize = typeof(OperationalIssuesController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .ToList();
        classAuthorize.Should().NotBeEmpty();
    }

    [Fact]
    public void Create_Endpoint_AllowsDriverRoleOnly()
    {
        var methodAuthorize = typeof(OperationalIssuesController)
            .GetMethod(nameof(OperationalIssuesController.Create))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        methodAuthorize.Roles.Should().Be(AppRoles.Driver);
    }

    [Fact]
    public void GetMine_Endpoint_AllowsDriverRoleOnly()
    {
        var methodAuthorize = typeof(OperationalIssuesController)
            .GetMethod(nameof(OperationalIssuesController.GetMine))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        methodAuthorize.Roles.Should().Be(AppRoles.Driver);
    }

    [Fact]
    public void GetList_Endpoint_AllowsOfficerAndManagerRolesOnly()
    {
        var methodAuthorize = typeof(OperationalIssuesController)
            .GetMethod(nameof(OperationalIssuesController.GetList))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        methodAuthorize.Roles.Should().Contain(AppRoles.WasteOfficer);
        methodAuthorize.Roles.Should().Contain(AppRoles.MunicipalManager);
        methodAuthorize.Roles.Should().NotContain(AppRoles.Driver);
        methodAuthorize.Roles.Should().NotContain(AppRoles.Citizen);
    }

    [Fact]
    public void GetById_Endpoint_AllowsDriverOfficerManagerRoles()
    {
        var methodAuthorize = typeof(OperationalIssuesController)
            .GetMethod(nameof(OperationalIssuesController.GetById))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        methodAuthorize.Roles.Should().Contain(AppRoles.Driver);
        methodAuthorize.Roles.Should().Contain(AppRoles.WasteOfficer);
        methodAuthorize.Roles.Should().Contain(AppRoles.MunicipalManager);
        methodAuthorize.Roles.Should().NotContain(AppRoles.Citizen);
    }

    [Fact]
    public void StartReview_Endpoint_AllowsOfficerAndManagerRolesOnly()
    {
        var methodAuthorize = typeof(OperationalIssuesController)
            .GetMethod(nameof(OperationalIssuesController.StartReview))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        methodAuthorize.Roles.Should().Contain(AppRoles.WasteOfficer);
        methodAuthorize.Roles.Should().Contain(AppRoles.MunicipalManager);
        methodAuthorize.Roles.Should().NotContain(AppRoles.Driver);
        methodAuthorize.Roles.Should().NotContain(AppRoles.Citizen);
    }

    [Fact]
    public void Resolve_Endpoint_AllowsOfficerAndManagerRolesOnly()
    {
        var methodAuthorize = typeof(OperationalIssuesController)
            .GetMethod(nameof(OperationalIssuesController.Resolve))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        methodAuthorize.Roles.Should().Contain(AppRoles.WasteOfficer);
        methodAuthorize.Roles.Should().Contain(AppRoles.MunicipalManager);
        methodAuthorize.Roles.Should().NotContain(AppRoles.Driver);
        methodAuthorize.Roles.Should().NotContain(AppRoles.Citizen);
    }

    // =========================================================================
    // 2. Action Executions & Status Code Returns
    // =========================================================================

    [Fact]
    public async Task Create_AuthenticatedDriver_ReturnsCreatedAtAction()
    {
        var driverId = Guid.NewGuid();
        WithActor(_controller, driverId, AppRoles.Driver);

        var request = new CreateOperationalIssueRequest
        {
            IssueType = OperationalIssueType.VehicleProblem,
            Title = "Tire puncture",
            Description = "Rear right tire punctured on Sector 2 gravel road."
        };

        var expectedDetail = new OperationalIssueDetailDto
        {
            Id = Guid.NewGuid(),
            DriverId = driverId,
            DriverName = "Driver User",
            IssueType = OperationalIssueType.VehicleProblem,
            Title = request.Title,
            Description = request.Description,
            Status = OperationalIssueStatus.Reported,
            CreatedAt = DateTime.UtcNow
        };

        _issueServiceMock
            .Setup(s => s.CreateAsync(request, driverId, AppRoles.Driver, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDetail);

        var actionResult = await _controller.Create(request, CancellationToken.None);

        var createdResult = actionResult.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.StatusCode.Should().Be(StatusCodes.Status201Created);
        createdResult.ActionName.Should().Be(nameof(OperationalIssuesController.GetById));
        createdResult.RouteValues.Should().ContainKey("id");
        createdResult.RouteValues!["id"].Should().Be(expectedDetail.Id);
        createdResult.Value.Should().BeEquivalentTo(expectedDetail);
    }

    [Fact]
    public async Task Create_MissingActor_ReturnsUnauthorized()
    {
        WithAnonymous(_controller);

        var request = new CreateOperationalIssueRequest
        {
            IssueType = OperationalIssueType.VehicleProblem,
            Title = "Tire puncture",
            Description = "Rear right tire punctured"
        };

        var actionResult = await _controller.Create(request, CancellationToken.None);

        var unauthorizedResult = actionResult.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthorizedResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task GetMine_AuthenticatedDriver_ReturnsOkWithPagedResult()
    {
        var driverId = Guid.NewGuid();
        WithActor(_controller, driverId, AppRoles.Driver);

        var query = new OperationalIssueListQuery { Page = 1, PageSize = 20 };
        var expectedPaged = new PagedResult<OperationalIssueSummaryDto>
        {
            Items = new List<OperationalIssueSummaryDto>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    DriverId = driverId,
                    DriverName = "Driver User",
                    IssueType = OperationalIssueType.OperationalDelay,
                    Title = "Traffic gridlock",
                    Status = OperationalIssueStatus.Reported,
                    CreatedAt = DateTime.UtcNow
                }
            },
            Page = 1,
            PageSize = 20,
            TotalCount = 1
        };

        _issueServiceMock
            .Setup(s => s.GetMineAsync(query, driverId, AppRoles.Driver, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedPaged);

        var actionResult = await _controller.GetMine(query, CancellationToken.None);

        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().BeEquivalentTo(expectedPaged);
    }

    [Fact]
    public async Task GetList_AuthenticatedStaff_ReturnsOkWithPagedResult()
    {
        var officerId = Guid.NewGuid();
        WithActor(_controller, officerId, AppRoles.WasteOfficer);

        var query = new OperationalIssueListQuery { Page = 1, PageSize = 20 };
        var expectedPaged = new PagedResult<OperationalIssueSummaryDto>
        {
            Items = new List<OperationalIssueSummaryDto>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    DriverId = Guid.NewGuid(),
                    DriverName = "Driver User",
                    IssueType = OperationalIssueType.SafetyConcern,
                    Title = "Chemical spill on road",
                    Status = OperationalIssueStatus.Reported,
                    CreatedAt = DateTime.UtcNow
                }
            },
            Page = 1,
            PageSize = 20,
            TotalCount = 1
        };

        _issueServiceMock
            .Setup(s => s.GetListAsync(query, officerId, AppRoles.WasteOfficer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedPaged);

        var actionResult = await _controller.GetList(query, CancellationToken.None);

        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().BeEquivalentTo(expectedPaged);
    }

    [Fact]
    public async Task GetById_AuthenticatedUser_ReturnsOkWithDetail()
    {
        var driverId = Guid.NewGuid();
        var issueId = Guid.NewGuid();
        WithActor(_controller, driverId, AppRoles.Driver);

        var expectedDetail = new OperationalIssueDetailDto
        {
            Id = issueId,
            DriverId = driverId,
            DriverName = "Driver User",
            IssueType = OperationalIssueType.RoadOrAccessIssue,
            Title = "Bridge road closed",
            Description = "Bridge undergoes maintenance, truck cannot cross.",
            Status = OperationalIssueStatus.Reported,
            CreatedAt = DateTime.UtcNow
        };

        _issueServiceMock
            .Setup(s => s.GetByIdAsync(issueId, driverId, AppRoles.Driver, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDetail);

        var actionResult = await _controller.GetById(issueId, CancellationToken.None);

        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().BeEquivalentTo(expectedDetail);
    }

    [Fact]
    public async Task StartReview_StaffActor_ReturnsOkWithUpdatedDetail()
    {
        var officerId = Guid.NewGuid();
        var issueId = Guid.NewGuid();
        WithActor(_controller, officerId, AppRoles.WasteOfficer);

        var expectedDetail = new OperationalIssueDetailDto
        {
            Id = issueId,
            DriverId = Guid.NewGuid(),
            DriverName = "Driver User",
            IssueType = OperationalIssueType.EquipmentProblem,
            Title = "Broken compactor",
            Description = "Compactor broken",
            Status = OperationalIssueStatus.InReview,
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            UpdatedAt = DateTime.UtcNow
        };

        _issueServiceMock
            .Setup(s => s.StartReviewAsync(issueId, officerId, AppRoles.WasteOfficer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDetail);

        var actionResult = await _controller.StartReview(issueId, CancellationToken.None);

        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().BeEquivalentTo(expectedDetail);
    }

    [Fact]
    public async Task Resolve_StaffActor_ReturnsOkWithResolvedDetail()
    {
        var officerId = Guid.NewGuid();
        var issueId = Guid.NewGuid();
        WithActor(_controller, officerId, AppRoles.WasteOfficer);

        var request = new ResolveOperationalIssueRequest
        {
            ResolutionNote = "Alternative route assigned and backup vehicle sent."
        };

        var expectedDetail = new OperationalIssueDetailDto
        {
            Id = issueId,
            DriverId = Guid.NewGuid(),
            DriverName = "Driver User",
            IssueType = OperationalIssueType.RoadOrAccessIssue,
            Title = "Bridge closed",
            Description = "Bridge maintenance",
            Status = OperationalIssueStatus.Resolved,
            ResolvedAt = DateTime.UtcNow,
            ResolvedByUserId = officerId,
            ResolvedByUserName = "Officer User",
            ResolutionNote = request.ResolutionNote,
            CreatedAt = DateTime.UtcNow.AddHours(-2),
            UpdatedAt = DateTime.UtcNow
        };

        _issueServiceMock
            .Setup(s => s.ResolveAsync(issueId, request, officerId, AppRoles.WasteOfficer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDetail);

        var actionResult = await _controller.Resolve(issueId, request, CancellationToken.None);

        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().BeEquivalentTo(expectedDetail);
    }
}
