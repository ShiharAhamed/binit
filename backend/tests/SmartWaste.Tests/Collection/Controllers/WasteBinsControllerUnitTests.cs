using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SmartWaste.Api.Controllers.Collection;
using SmartWaste.Application.Collection.DTOs.Requests;
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
/// Focused controller unit tests for WasteBinsController.
/// Uses a mocked IWasteBinService and simulated ClaimsPrincipal.
/// Validates claim extraction, query delegation, response envelope preservation, and data minimization.
/// </summary>
public class WasteBinsControllerUnitTests
{
    private readonly Mock<IWasteBinService> _mockService = new();
    private readonly Mock<IBinObservationService> _mockObservationService = new();

    private WasteBinsController CreateControllerWithUser(Guid? userId, string? role, string? rawUserId = null)
    {
        var controller = new WasteBinsController(_mockService.Object, _mockObservationService.Object);
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

    // ──────────────────────────────────────────────────────────────────────────
    // 1. PUBLIC CITIZEN ENDPOINTS
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetPublicList_CitizenClaims_DelegatesToServiceAndReturns200WithPagedResult()
    {
        var citizenId = Guid.NewGuid();
        var controller = CreateControllerWithUser(citizenId, AppRoles.Citizen);
        var query = new PublicWasteBinQuery
        {
            Latitude = 6.9271,
            Longitude = 79.8612,
            RadiusKm = 2.5,
            WasteType = WasteType.General,
            Page = 1,
            PageSize = 10
        };

        var expectedResult = new PagedResult<PublicWasteBinDto>
        {
            Items = new List<PublicWasteBinDto>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    BinCode = "BIN-COL-0042",
                    Latitude = 6.9271,
                    Longitude = 79.8612,
                    AddressText = "Pettah",
                    CapacityLiters = 660,
                    AcceptedWasteTypes = new[] { "General" },
                    PublicAvailability = "Usable",
                    DistanceMeters = 150.0
                }
            },
            Page = 1,
            PageSize = 10,
            TotalCount = 1
        };

        _mockService.Setup(s => s.GetPublicListAsync(query, citizenId, AppRoles.Citizen, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var actionResult = await controller.GetPublicList(query, CancellationToken.None);

        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        var pagedResult = okResult.Value.Should().BeOfType<PagedResult<PublicWasteBinDto>>().Subject;
        pagedResult.Should().BeSameAs(expectedResult);
        pagedResult.TotalCount.Should().Be(1);

        _mockService.Verify(s => s.GetPublicListAsync(query, citizenId, AppRoles.Citizen, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPublicById_CitizenClaims_DelegatesToServiceAndReturns200WithDetail()
    {
        var citizenId = Guid.NewGuid();
        var binId = Guid.NewGuid();
        var controller = CreateControllerWithUser(citizenId, AppRoles.Citizen);

        var expectedDetail = new PublicWasteBinDetailDto
        {
            Id = binId,
            BinCode = "BIN-COL-0042",
            Latitude = 6.9271,
            Longitude = 79.8612,
            AddressText = "Pettah Central",
            CapacityLiters = 660,
            AcceptedWasteTypes = new[] { "General", "Recyclable" },
            PublicAvailability = "Usable",
            LastObservedAt = DateTime.UtcNow.AddHours(-1),
            IsCollectionScheduled = false
        };

        _mockService.Setup(s => s.GetPublicByIdAsync(binId, citizenId, AppRoles.Citizen, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDetail);

        var actionResult = await controller.GetPublicById(binId, CancellationToken.None);

        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        var detail = okResult.Value.Should().BeOfType<PublicWasteBinDetailDto>().Subject;
        detail.Should().BeSameAs(expectedDetail);

        _mockService.Verify(s => s.GetPublicByIdAsync(binId, citizenId, AppRoles.Citizen, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 2. INTERNAL STAFF ENDPOINTS
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetInternalList_WasteOfficerClaims_DelegatesToServiceAndReturns200WithPagedResult()
    {
        var officerId = Guid.NewGuid();
        var controller = CreateControllerWithUser(officerId, AppRoles.WasteOfficer);
        var query = new WasteBinListQuery
        {
            Search = "Pettah",
            Page = 2,
            PageSize = 25
        };

        var expectedResult = new PagedResult<WasteBinSummaryDto>
        {
            Items = new List<WasteBinSummaryDto>(),
            Page = 2,
            PageSize = 25,
            TotalCount = 0
        };

        _mockService.Setup(s => s.GetInternalListAsync(query, officerId, AppRoles.WasteOfficer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var actionResult = await controller.GetInternalList(query, CancellationToken.None);

        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().BeSameAs(expectedResult);

        _mockService.Verify(s => s.GetInternalListAsync(query, officerId, AppRoles.WasteOfficer, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetInternalById_MunicipalManagerClaims_DelegatesToServiceAndReturns200WithDetail()
    {
        var managerId = Guid.NewGuid();
        var binId = Guid.NewGuid();
        var controller = CreateControllerWithUser(managerId, AppRoles.MunicipalManager);

        var expectedDetail = new WasteBinDetailDto
        {
            Id = binId,
            BinCode = "BIN-COL-0099",
            Latitude = 6.9312,
            Longitude = 79.8504,
            AddressText = "Galle Face Promenade",
            CapacityLiters = 1100,
            AdministrativeStatus = BinAdministrativeStatus.Active,
            AcceptedWasteTypes = new[] { "General", "Organic" },
            CollectionWeekdays = new[] { 1, 3, 5 },
            HasActiveTask = true,
            ActiveTaskId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow.AddDays(-30),
            UpdatedAt = DateTime.UtcNow.AddDays(-2)
        };

        _mockService.Setup(s => s.GetInternalByIdAsync(binId, managerId, AppRoles.MunicipalManager, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDetail);

        var actionResult = await controller.GetInternalById(binId, CancellationToken.None);

        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().BeSameAs(expectedDetail);

        _mockService.Verify(s => s.GetInternalByIdAsync(binId, managerId, AppRoles.MunicipalManager, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 3. ACTOR CLAIM EXTRACTION & ERROR HANDLING
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task TryGetActor_MissingNameIdentifierClaim_Returns401Unauthorized()
    {
        var controller = CreateControllerWithUser(null, AppRoles.Citizen);

        var actionResult = await controller.GetPublicList(new PublicWasteBinQuery(), CancellationToken.None);

        var unauthResult = actionResult.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        var problem = unauthResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Status.Should().Be(StatusCodes.Status401Unauthorized);
        problem.Detail.Should().Contain("User identity claim could not be determined");

        _mockService.Verify(s => s.GetPublicListAsync(It.IsAny<PublicWasteBinQuery>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TryGetActor_MalformedGuidNameIdentifierClaim_Returns401Unauthorized()
    {
        var controller = CreateControllerWithUser(null, AppRoles.Citizen, rawUserId: "invalid-not-a-guid");

        var actionResult = await controller.GetPublicById(Guid.NewGuid(), CancellationToken.None);

        var unauthResult = actionResult.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        var problem = unauthResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Status.Should().Be(StatusCodes.Status401Unauthorized);
        problem.Detail.Should().Contain("User identity claim could not be determined");
    }

    [Fact]
    public async Task TryGetActor_MissingRoleClaim_Returns401Unauthorized()
    {
        var controller = CreateControllerWithUser(Guid.NewGuid(), null);

        var actionResult = await controller.GetInternalList(new WasteBinListQuery(), CancellationToken.None);

        var unauthResult = actionResult.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        var problem = unauthResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Status.Should().Be(StatusCodes.Status401Unauthorized);
        problem.Detail.Should().Contain("User role claim could not be determined");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 4. DATA MINIMIZATION VERIFICATION
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void PublicWasteBinDto_DoesNotContainStaffMetadata()
    {
        var publicDto = new PublicWasteBinDto
        {
            Id = Guid.NewGuid(),
            BinCode = "BIN-COL-0001",
            Latitude = 6.9,
            Longitude = 79.8,
            AddressText = "Public Road",
            CapacityLiters = 660,
            AcceptedWasteTypes = new[] { "General" },
            PublicAvailability = "Usable",
            LastObservedAt = DateTime.UtcNow,
            DistanceMeters = 100
        };

        var json = JsonSerializer.Serialize(publicDto);

        // Verify staff-only properties do not exist on the public DTO
        json.Should().NotContain("collectionWeekdays");
        json.Should().NotContain("latestObservation");
        json.Should().NotContain("latestCondition");
        json.Should().NotContain("lastCollectedAt");
        json.Should().NotContain("administrativeStatus");
        json.Should().NotContain("hasActiveTask");
        json.Should().NotContain("recordedByUserId");
        json.Should().NotContain("recordedByUserName");
    }

    [Fact]
    public void PublicWasteBinDetailDto_DoesNotContainStaffMetadata()
    {
        var publicDetailDto = new PublicWasteBinDetailDto
        {
            Id = Guid.NewGuid(),
            BinCode = "BIN-COL-0001",
            Latitude = 6.9,
            Longitude = 79.8,
            AddressText = "Public Road",
            CapacityLiters = 660,
            AcceptedWasteTypes = new[] { "General" },
            PublicAvailability = "Usable",
            LastObservedAt = DateTime.UtcNow,
            IsCollectionScheduled = false
        };

        var json = JsonSerializer.Serialize(publicDetailDto);

        // Verify staff-only properties do not exist on the public detail DTO
        json.Should().NotContain("collectionWeekdays");
        json.Should().NotContain("latestObservation");
        json.Should().NotContain("latestCondition");
        json.Should().NotContain("lastCollectedAt");
        json.Should().NotContain("administrativeStatus");
        json.Should().NotContain("hasActiveTask");
        json.Should().NotContain("activeTaskId");
        json.Should().NotContain("recordedByUserId");
        json.Should().NotContain("recordedByUserName");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 5. INTERNAL STAFF WRITE ENDPOINTS
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_WasteOfficerClaims_DelegatesToServiceAndReturns201WithCreatedAtAction()
    {
        var officerId = Guid.NewGuid();
        var controller = CreateControllerWithUser(officerId, AppRoles.WasteOfficer);
        var request = new CreateWasteBinRequest
        {
            BinCode = "BIN-COL-0043",
            Latitude = 6.9312,
            Longitude = 79.8504,
            AddressText = "Galle Face Green Promenade",
            CapacityLiters = 1100,
            AcceptedWasteTypes = new[] { WasteType.General, WasteType.Organic },
            CollectionWeekdays = new[] { 1, 3, 5 }
        };

        var createdId = Guid.NewGuid();
        var expectedDetail = new WasteBinDetailDto
        {
            Id = createdId,
            BinCode = "BIN-COL-0043",
            Latitude = 6.9312,
            Longitude = 79.8504,
            AddressText = "Galle Face Green Promenade",
            CapacityLiters = 1100,
            AdministrativeStatus = BinAdministrativeStatus.Active,
            AcceptedWasteTypes = new[] { "General", "Organic" },
            CollectionWeekdays = new[] { 1, 3, 5 },
            CreatedAt = DateTime.UtcNow
        };

        _mockService.Setup(s => s.CreateAsync(request, officerId, AppRoles.WasteOfficer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDetail);

        var actionResult = await controller.Create(request, CancellationToken.None);

        var createdAtResult = actionResult.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdAtResult.StatusCode.Should().Be(StatusCodes.Status201Created);
        createdAtResult.ActionName.Should().Be(nameof(WasteBinsController.GetInternalById));
        createdAtResult.RouteValues.Should().ContainKey("id");
        createdAtResult.RouteValues!["id"].Should().Be(createdId);
        createdAtResult.Value.Should().BeSameAs(expectedDetail);

        _mockService.Verify(s => s.CreateAsync(request, officerId, AppRoles.WasteOfficer, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_MissingUserClaims_Returns401Unauthorized()
    {
        var controller = CreateControllerWithUser(null, AppRoles.WasteOfficer);
        var request = new CreateWasteBinRequest { BinCode = "BIN-COL-0043" };

        var actionResult = await controller.Create(request, CancellationToken.None);

        var unauthResult = actionResult.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        _mockService.Verify(s => s.CreateAsync(It.IsAny<CreateWasteBinRequest>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Update_WasteOfficerClaims_DelegatesToServiceAndReturns200WithOk()
    {
        var officerId = Guid.NewGuid();
        var binId = Guid.NewGuid();
        var controller = CreateControllerWithUser(officerId, AppRoles.WasteOfficer);
        var request = new UpdateWasteBinRequest
        {
            Latitude = 6.9315,
            Longitude = 79.8506,
            AddressText = "Galle Face Green Promenade (North Pavilion)",
            CapacityLiters = 1100,
            AcceptedWasteTypes = new[] { WasteType.General, WasteType.Organic, WasteType.Recyclable },
            CollectionWeekdays = new[] { 1, 3, 5 }
        };

        var expectedDetail = new WasteBinDetailDto
        {
            Id = binId,
            BinCode = "BIN-COL-0043",
            Latitude = 6.9315,
            Longitude = 79.8506,
            AddressText = "Galle Face Green Promenade (North Pavilion)",
            CapacityLiters = 1100,
            AdministrativeStatus = BinAdministrativeStatus.Active,
            AcceptedWasteTypes = new[] { "General", "Organic", "Recyclable" },
            CollectionWeekdays = new[] { 1, 3, 5 },
            UpdatedAt = DateTime.UtcNow
        };

        _mockService.Setup(s => s.UpdateAsync(binId, request, officerId, AppRoles.WasteOfficer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDetail);

        var actionResult = await controller.Update(binId, request, CancellationToken.None);

        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().BeSameAs(expectedDetail);

        _mockService.Verify(s => s.UpdateAsync(binId, request, officerId, AppRoles.WasteOfficer, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_MissingUserClaims_Returns401Unauthorized()
    {
        var controller = CreateControllerWithUser(null, AppRoles.WasteOfficer);
        var request = new UpdateWasteBinRequest();

        var actionResult = await controller.Update(Guid.NewGuid(), request, CancellationToken.None);

        var unauthResult = actionResult.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        _mockService.Verify(s => s.UpdateAsync(It.IsAny<Guid>(), It.IsAny<UpdateWasteBinRequest>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 6. BIN OBSERVATION ENDPOINTS
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RecordObservation_WasteOfficerClaims_DelegatesToServiceAndReturns201Created()
    {
        var officerId = Guid.NewGuid();
        var binId = Guid.NewGuid();
        var controller = CreateControllerWithUser(officerId, AppRoles.WasteOfficer);
        var request = new RecordBinObservationRequest
        {
            FillLevelPercent = 75,
            Condition = BinCondition.Good,
            Notes = "Near capacity, holiday weekend."
        };

        var expectedDto = new BinObservationDto
        {
            Id = Guid.NewGuid(),
            WasteBinId = binId,
            FillLevelPercent = 75,
            Condition = BinCondition.Good,
            Notes = "Near capacity, holiday weekend.",
            RecordedByUserId = officerId,
            RecordedByUserName = "Officer Silva",
            RecordedAt = DateTime.UtcNow
        };

        _mockObservationService.Setup(s => s.RecordObservationAsync(binId, request, officerId, AppRoles.WasteOfficer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDto);

        var actionResult = await controller.RecordObservation(binId, request, CancellationToken.None);

        var objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status201Created);
        objectResult.Value.Should().BeSameAs(expectedDto);

        _mockObservationService.Verify(s => s.RecordObservationAsync(binId, request, officerId, AppRoles.WasteOfficer, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RecordObservation_MissingUserClaims_Returns401Unauthorized()
    {
        var controller = CreateControllerWithUser(null, AppRoles.WasteOfficer);
        var request = new RecordBinObservationRequest { FillLevelPercent = 50, Condition = BinCondition.Good };

        var actionResult = await controller.RecordObservation(Guid.NewGuid(), request, CancellationToken.None);

        var unauthResult = actionResult.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        _mockObservationService.Verify(s => s.RecordObservationAsync(It.IsAny<Guid>(), It.IsAny<RecordBinObservationRequest>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetObservationHistory_WasteOfficerClaims_DelegatesToServiceAndReturns200WithPagedResult()
    {
        var officerId = Guid.NewGuid();
        var binId = Guid.NewGuid();
        var controller = CreateControllerWithUser(officerId, AppRoles.WasteOfficer);
        var query = new ObservationListQuery { Page = 1, PageSize = 10 };

        var expectedResult = new PagedResult<BinObservationDto>
        {
            Items = new List<BinObservationDto>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    WasteBinId = binId,
                    FillLevelPercent = 100,
                    Condition = BinCondition.Blocked,
                    RecordedByUserId = officerId,
                    RecordedByUserName = "Officer Silva",
                    RecordedAt = DateTime.UtcNow
                }
            },
            Page = 1,
            PageSize = 10,
            TotalCount = 1
        };

        _mockObservationService.Setup(s => s.GetObservationHistoryAsync(binId, query, officerId, AppRoles.WasteOfficer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var actionResult = await controller.GetObservationHistory(binId, query, CancellationToken.None);

        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().BeSameAs(expectedResult);

        _mockObservationService.Verify(s => s.GetObservationHistoryAsync(binId, query, officerId, AppRoles.WasteOfficer, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetObservationHistory_MunicipalManagerClaims_DelegatesToServiceAndReturns200WithPagedResult()
    {
        var managerId = Guid.NewGuid();
        var binId = Guid.NewGuid();
        var controller = CreateControllerWithUser(managerId, AppRoles.MunicipalManager);
        var query = new ObservationListQuery { Page = 1, PageSize = 20 };

        var expectedResult = new PagedResult<BinObservationDto>
        {
            Items = new List<BinObservationDto>(),
            Page = 1,
            PageSize = 20,
            TotalCount = 0
        };

        _mockObservationService.Setup(s => s.GetObservationHistoryAsync(binId, query, managerId, AppRoles.MunicipalManager, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var actionResult = await controller.GetObservationHistory(binId, query, CancellationToken.None);

        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().BeSameAs(expectedResult);

        _mockObservationService.Verify(s => s.GetObservationHistoryAsync(binId, query, managerId, AppRoles.MunicipalManager, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetObservationHistory_MissingUserClaims_Returns401Unauthorized()
    {
        var controller = CreateControllerWithUser(null, AppRoles.WasteOfficer);
        var query = new ObservationListQuery();

        var actionResult = await controller.GetObservationHistory(Guid.NewGuid(), query, CancellationToken.None);

        var unauthResult = actionResult.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        _mockObservationService.Verify(s => s.GetObservationHistoryAsync(It.IsAny<Guid>(), It.IsAny<ObservationListQuery>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Deactivate_WasteOfficerClaims_DelegatesToServiceAndReturns200WithOk()
    {
        var officerId = Guid.NewGuid();
        var binId = Guid.NewGuid();
        var controller = CreateControllerWithUser(officerId, AppRoles.WasteOfficer);
        var request = new DeactivateWasteBinRequest
        {
            TargetStatus = BinAdministrativeStatus.OutOfService,
            Reason = "Repair hinge"
        };

        var expectedDetail = new WasteBinDetailDto
        {
            Id = binId,
            BinCode = "BIN-DEACT",
            AdministrativeStatus = BinAdministrativeStatus.OutOfService,
            UpdatedAt = DateTime.UtcNow
        };

        _mockService.Setup(s => s.DeactivateAsync(binId, request, officerId, AppRoles.WasteOfficer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDetail);

        var actionResult = await controller.Deactivate(binId, request, CancellationToken.None);

        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().BeSameAs(expectedDetail);

        _mockService.Verify(s => s.DeactivateAsync(binId, request, officerId, AppRoles.WasteOfficer, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Deactivate_MissingUserClaims_Returns401Unauthorized()
    {
        var controller = CreateControllerWithUser(null, AppRoles.WasteOfficer);
        var request = new DeactivateWasteBinRequest { TargetStatus = BinAdministrativeStatus.OutOfService };

        var actionResult = await controller.Deactivate(Guid.NewGuid(), request, CancellationToken.None);

        var unauthResult = actionResult.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        _mockService.Verify(s => s.DeactivateAsync(It.IsAny<Guid>(), It.IsAny<DeactivateWasteBinRequest>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
