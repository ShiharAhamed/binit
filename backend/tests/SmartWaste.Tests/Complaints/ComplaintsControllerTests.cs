using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SmartWaste.Api.Controllers;
using SmartWaste.Application.Common.Models;
using SmartWaste.Application.Complaints.DTOs.Requests;
using SmartWaste.Application.Complaints.DTOs.Responses;
using SmartWaste.Application.Complaints.Interfaces;
using SmartWaste.Application.Complaints.Queries;
using SmartWaste.Domain.Common;
using SmartWaste.Domain.Complaints.Enums;
using Xunit;

namespace SmartWaste.Tests.Complaints;

public class ComplaintsControllerTests
{
    private readonly Mock<IComplaintService> _complaintServiceMock = new();
    private readonly ComplaintsController _controller;

    public ComplaintsControllerTests()
    {
        _controller = new ComplaintsController(_complaintServiceMock.Object);
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
        var classAuthorize = typeof(ComplaintsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .ToList();
        classAuthorize.Should().NotBeEmpty();
    }

    [Fact]
    public void Create_Endpoint_AllowsCitizenRoleOnly()
    {
        var methodAuthorize = typeof(ComplaintsController)
            .GetMethod(nameof(ComplaintsController.Create))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        methodAuthorize.Roles.Should().Be(AppRoles.Citizen);
    }

    [Fact]
    public void GetList_Endpoint_AllowsCitizenOfficerManagerRoles()
    {
        var methodAuthorize = typeof(ComplaintsController)
            .GetMethod(nameof(ComplaintsController.GetList))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        methodAuthorize.Roles.Should().Contain(AppRoles.Citizen);
        methodAuthorize.Roles.Should().Contain(AppRoles.WasteOfficer);
        methodAuthorize.Roles.Should().Contain(AppRoles.MunicipalManager);
        methodAuthorize.Roles.Should().NotContain(AppRoles.Driver);
    }

    [Fact]
    public void StartReview_Endpoint_AllowsOfficerAndManagerRolesOnly()
    {
        var methodAuthorize = typeof(ComplaintsController)
            .GetMethod(nameof(ComplaintsController.StartReview))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        methodAuthorize.Roles.Should().Contain(AppRoles.WasteOfficer);
        methodAuthorize.Roles.Should().Contain(AppRoles.MunicipalManager);
        methodAuthorize.Roles.Should().NotContain(AppRoles.Citizen);
        methodAuthorize.Roles.Should().NotContain(AppRoles.Driver);
    }

    [Fact]
    public void Resolve_Endpoint_AllowsOfficerAndManagerRolesOnly()
    {
        var methodAuthorize = typeof(ComplaintsController)
            .GetMethod(nameof(ComplaintsController.Resolve))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        methodAuthorize.Roles.Should().Contain(AppRoles.WasteOfficer);
        methodAuthorize.Roles.Should().Contain(AppRoles.MunicipalManager);
        methodAuthorize.Roles.Should().NotContain(AppRoles.Citizen);
        methodAuthorize.Roles.Should().NotContain(AppRoles.Driver);
    }

    // =========================================================================
    // 2. Action Executions & Status Code Returns
    // =========================================================================

    [Fact]
    public async Task Create_AuthenticatedCitizen_ReturnsCreatedAtAction()
    {
        var citizenId = Guid.NewGuid();
        WithActor(_controller, citizenId, AppRoles.Citizen);

        var request = new CreateComplaintRequest
        {
            Category = ComplaintCategory.MissedCollection,
            Subject = "Missed collection",
            Description = "Bin was missed on Friday morning."
        };

        var expectedDetail = new ComplaintDetailDto
        {
            Id = Guid.NewGuid(),
            CitizenId = citizenId,
            CitizenName = "Citizen User",
            Category = ComplaintCategory.MissedCollection,
            Subject = request.Subject,
            Description = request.Description,
            Status = ComplaintStatus.Submitted,
            CreatedAt = DateTime.UtcNow
        };

        _complaintServiceMock
            .Setup(s => s.CreateAsync(request, citizenId, AppRoles.Citizen, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDetail);

        var actionResult = await _controller.Create(request, CancellationToken.None);

        var createdResult = actionResult.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.StatusCode.Should().Be(StatusCodes.Status201Created);
        createdResult.ActionName.Should().Be(nameof(ComplaintsController.GetById));
        createdResult.RouteValues.Should().ContainKey("id");
        createdResult.RouteValues!["id"].Should().Be(expectedDetail.Id);
        createdResult.Value.Should().BeEquivalentTo(expectedDetail);
    }

    [Fact]
    public async Task Create_MissingActor_ReturnsUnauthorized()
    {
        WithAnonymous(_controller);

        var request = new CreateComplaintRequest
        {
            Category = ComplaintCategory.MissedCollection,
            Subject = "Missed collection",
            Description = "Bin missed"
        };

        var actionResult = await _controller.Create(request, CancellationToken.None);

        var unauthorizedResult = actionResult.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthorizedResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task GetList_AuthenticatedUser_ReturnsOkWithPagedResult()
    {
        var officerId = Guid.NewGuid();
        WithActor(_controller, officerId, AppRoles.WasteOfficer);

        var query = new ComplaintListQuery { Page = 1, PageSize = 20 };
        var expectedPaged = new PagedResult<ComplaintSummaryDto>
        {
            Items = new List<ComplaintSummaryDto>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    CitizenId = Guid.NewGuid(),
                    CitizenName = "Test Citizen",
                    Category = ComplaintCategory.PoorService,
                    Subject = "Poor collection service",
                    Status = ComplaintStatus.Submitted,
                    CreatedAt = DateTime.UtcNow
                }
            },
            Page = 1,
            PageSize = 20,
            TotalCount = 1
        };

        _complaintServiceMock
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
        var citizenId = Guid.NewGuid();
        var complaintId = Guid.NewGuid();
        WithActor(_controller, citizenId, AppRoles.Citizen);

        var expectedDetail = new ComplaintDetailDto
        {
            Id = complaintId,
            CitizenId = citizenId,
            CitizenName = "Test Citizen",
            Category = ComplaintCategory.PoorService,
            Subject = "Litter on sidewalk",
            Description = "Crew dropped waste on sidewalk during collection",
            Status = ComplaintStatus.Submitted,
            CreatedAt = DateTime.UtcNow
        };

        _complaintServiceMock
            .Setup(s => s.GetByIdAsync(complaintId, citizenId, AppRoles.Citizen, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDetail);

        var actionResult = await _controller.GetById(complaintId, CancellationToken.None);

        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().BeEquivalentTo(expectedDetail);
    }

    [Fact]
    public async Task StartReview_StaffActor_ReturnsOkWithUpdatedDetail()
    {
        var officerId = Guid.NewGuid();
        var complaintId = Guid.NewGuid();
        WithActor(_controller, officerId, AppRoles.WasteOfficer);

        var expectedDetail = new ComplaintDetailDto
        {
            Id = complaintId,
            CitizenId = Guid.NewGuid(),
            CitizenName = "Test Citizen",
            Category = ComplaintCategory.PoorService,
            Subject = "Overflowing bin",
            Description = "Bin is full",
            Status = ComplaintStatus.InReview,
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            UpdatedAt = DateTime.UtcNow
        };

        _complaintServiceMock
            .Setup(s => s.StartReviewAsync(complaintId, officerId, AppRoles.WasteOfficer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDetail);

        var actionResult = await _controller.StartReview(complaintId, CancellationToken.None);

        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().BeEquivalentTo(expectedDetail);
    }

    [Fact]
    public async Task Resolve_StaffActor_ReturnsOkWithResolvedDetail()
    {
        var officerId = Guid.NewGuid();
        var complaintId = Guid.NewGuid();
        WithActor(_controller, officerId, AppRoles.WasteOfficer);

        var request = new ResolveComplaintRequest
        {
            ResolutionNote = "Investigation conducted. Waste bin replaced."
        };

        var expectedDetail = new ComplaintDetailDto
        {
            Id = complaintId,
            CitizenId = Guid.NewGuid(),
            CitizenName = "Test Citizen",
            Category = ComplaintCategory.UnresolvedIssue,
            Subject = "Damaged bin",
            Description = "Bin broken",
            Status = ComplaintStatus.Resolved,
            ResolvedAt = DateTime.UtcNow,
            ResolvedByUserId = officerId,
            ResolvedByUserName = "Officer User",
            ResolutionNote = request.ResolutionNote,
            CreatedAt = DateTime.UtcNow.AddHours(-2),
            UpdatedAt = DateTime.UtcNow
        };

        _complaintServiceMock
            .Setup(s => s.ResolveAsync(complaintId, request, officerId, AppRoles.WasteOfficer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDetail);

        var actionResult = await _controller.Resolve(complaintId, request, CancellationToken.None);

        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().BeEquivalentTo(expectedDetail);
    }
}
