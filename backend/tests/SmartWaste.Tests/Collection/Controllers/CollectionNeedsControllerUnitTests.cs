using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SmartWaste.Api.Controllers.Collection;
using SmartWaste.Application.Collection.DTOs.Responses;
using SmartWaste.Application.Collection.Interfaces;
using SmartWaste.Application.Collection.Queries;
using SmartWaste.Application.Common.Models;
using SmartWaste.Domain.Collection.Enums;
using SmartWaste.Domain.Common;
using SmartWaste.Domain.Reporting.Enums;
using Xunit;

namespace SmartWaste.Tests.Collection.Controllers;

/// <summary>
/// Focused controller unit tests for CollectionNeedsController.
/// Uses a mocked ICollectionNeedService and simulated ClaimsPrincipal.
/// Validates claim extraction, query delegation, response envelope preservation, and service call isolation.
/// </summary>
public class CollectionNeedsControllerUnitTests
{
    private readonly Mock<ICollectionNeedService> _mockService = new();

    private CollectionNeedsController CreateControllerWithUser(Guid? userId, string? role, string? rawUserId = null)
    {
        var controller = new CollectionNeedsController(_mockService.Object);
        var claims = new List<Claim>();

        if (rawUserId != null)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, rawUserId));
        }
        else if (userId.HasValue)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));
        }

        if (!string.IsNullOrEmpty(role))
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        return controller;
    }

    [Fact]
    public async Task GetCollectionNeeds_WasteOfficerClaims_DelegatesToServiceAndReturns200WithPagedResult()
    {
        var officerId = Guid.NewGuid();
        var controller = CreateControllerWithUser(officerId, AppRoles.WasteOfficer);
        var query = new CollectionNeedListQuery
        {
            TargetType = "Report",
            CollectionReason = "VerifiedReport",
            WasteType = WasteType.General,
            Search = "Pettah",
            Page = 1,
            PageSize = 10
        };

        var expectedResult = new PagedResult<CollectionNeedItemDto>
        {
            Items = new List<CollectionNeedItemDto>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    TargetType = "Report",
                    CollectionReason = "VerifiedReport",
                    Title = "Verified Report: Main Street, Pettah",
                    Latitude = 6.9351,
                    Longitude = 79.8512,
                    AddressText = "Main Street, Pettah",
                    WasteTypes = new List<string> { "General" },
                    Urgency = "High",
                    TriggerDate = DateTime.UtcNow.AddHours(-2),
                    AttachmentCount = 2,
                    BinDetails = null
                }
            },
            Page = 1,
            PageSize = 10,
            TotalCount = 1
        };

        _mockService.Setup(s => s.GetCollectionNeedsAsync(query, officerId, AppRoles.WasteOfficer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var actionResult = await controller.GetCollectionNeeds(query, CancellationToken.None);

        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        var pagedResult = okResult.Value.Should().BeOfType<PagedResult<CollectionNeedItemDto>>().Subject;
        pagedResult.Should().BeSameAs(expectedResult);
        pagedResult.TotalCount.Should().Be(1);

        _mockService.Verify(s => s.GetCollectionNeedsAsync(query, officerId, AppRoles.WasteOfficer, It.IsAny<CancellationToken>()), Times.Once);
        _mockService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetCollectionNeeds_MunicipalManagerClaims_DelegatesToServiceAndReturns200WithPagedResult()
    {
        var managerId = Guid.NewGuid();
        var controller = CreateControllerWithUser(managerId, AppRoles.MunicipalManager);
        var query = new CollectionNeedListQuery
        {
            TargetType = "Bin",
            CollectionReason = "FullOrBlockedBin",
            Page = 2,
            PageSize = 25
        };

        var expectedResult = new PagedResult<CollectionNeedItemDto>
        {
            Items = new List<CollectionNeedItemDto>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    TargetType = "Bin",
                    CollectionReason = "FullOrBlockedBin",
                    Title = "BIN-COL-0042 (100% Full)",
                    Latitude = 6.9271,
                    Longitude = 79.8612,
                    AddressText = "Pettah Depot",
                    WasteTypes = new List<string> { "General", "Organic" },
                    Urgency = "High",
                    TriggerDate = DateTime.UtcNow.AddMinutes(-45),
                    AttachmentCount = 0,
                    BinDetails = new CollectionNeedBinDetailsDto
                    {
                        BinCode = "BIN-COL-0042",
                        CapacityLiters = 1100,
                        LatestFillLevelPercent = 100,
                        LatestCondition = BinCondition.Good,
                        ObservationAgeHours = 0.75
                    }
                }
            },
            Page = 2,
            PageSize = 25,
            TotalCount = 26
        };

        _mockService.Setup(s => s.GetCollectionNeedsAsync(query, managerId, AppRoles.MunicipalManager, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var actionResult = await controller.GetCollectionNeeds(query, CancellationToken.None);

        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().BeSameAs(expectedResult);

        _mockService.Verify(s => s.GetCollectionNeedsAsync(query, managerId, AppRoles.MunicipalManager, It.IsAny<CancellationToken>()), Times.Once);
        _mockService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetCollectionNeeds_EmptyResult_Returns200WithEmptyPagedResult()
    {
        var officerId = Guid.NewGuid();
        var controller = CreateControllerWithUser(officerId, AppRoles.WasteOfficer);
        var query = new CollectionNeedListQuery();

        var emptyResult = new PagedResult<CollectionNeedItemDto>
        {
            Items = new List<CollectionNeedItemDto>(),
            Page = 1,
            PageSize = 20,
            TotalCount = 0
        };

        _mockService.Setup(s => s.GetCollectionNeedsAsync(query, officerId, AppRoles.WasteOfficer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(emptyResult);

        var actionResult = await controller.GetCollectionNeeds(query, CancellationToken.None);

        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        var pagedResult = okResult.Value.Should().BeOfType<PagedResult<CollectionNeedItemDto>>().Subject;
        pagedResult.Items.Should().BeEmpty();
        pagedResult.TotalCount.Should().Be(0);

        _mockService.Verify(s => s.GetCollectionNeedsAsync(query, officerId, AppRoles.WasteOfficer, It.IsAny<CancellationToken>()), Times.Once);
        _mockService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetCollectionNeeds_MissingUserClaim_Returns401Unauthorized()
    {
        var controller = CreateControllerWithUser(null, AppRoles.WasteOfficer);

        var actionResult = await controller.GetCollectionNeeds(new CollectionNeedListQuery(), CancellationToken.None);

        var unauthResult = actionResult.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        var problem = unauthResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Status.Should().Be(StatusCodes.Status401Unauthorized);
        problem.Detail.Should().Contain("User identity claim could not be determined");

        _mockService.Verify(s => s.GetCollectionNeedsAsync(It.IsAny<CollectionNeedListQuery>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetCollectionNeeds_MalformedGuidUserClaim_Returns401Unauthorized()
    {
        var controller = CreateControllerWithUser(null, AppRoles.WasteOfficer, rawUserId: "invalid-not-a-guid");

        var actionResult = await controller.GetCollectionNeeds(new CollectionNeedListQuery(), CancellationToken.None);

        var unauthResult = actionResult.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        var problem = unauthResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Status.Should().Be(StatusCodes.Status401Unauthorized);
        problem.Detail.Should().Contain("User identity claim could not be determined");

        _mockService.Verify(s => s.GetCollectionNeedsAsync(It.IsAny<CollectionNeedListQuery>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetCollectionNeeds_MissingRoleClaim_Returns401Unauthorized()
    {
        var controller = CreateControllerWithUser(Guid.NewGuid(), null);

        var actionResult = await controller.GetCollectionNeeds(new CollectionNeedListQuery(), CancellationToken.None);

        var unauthResult = actionResult.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        var problem = unauthResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Status.Should().Be(StatusCodes.Status401Unauthorized);
        problem.Detail.Should().Contain("User role claim could not be determined");

        _mockService.Verify(s => s.GetCollectionNeedsAsync(It.IsAny<CollectionNeedListQuery>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
