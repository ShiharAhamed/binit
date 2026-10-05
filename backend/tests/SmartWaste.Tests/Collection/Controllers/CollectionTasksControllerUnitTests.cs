using System.Security.Claims;
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
using Xunit;

namespace SmartWaste.Tests.Collection.Controllers;

/// <summary>
/// Focused controller unit tests for CollectionTasksController.
/// Uses a mocked ICollectionTaskService and simulated ClaimsPrincipal.
/// Validates claim extraction, query and payload delegation, response envelope preservation, Location header, and service call isolation.
/// </summary>
public class CollectionTasksControllerUnitTests
{
    private readonly Mock<ICollectionTaskService> _mockService = new();
    private readonly Mock<IAssignmentReadService> _assignmentReadService = new();

    private CollectionTasksController CreateControllerWithUser(Guid? userId, string? role, string? rawUserId = null)
    {
        var controller = new CollectionTasksController(_mockService.Object, _assignmentReadService.Object);
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
    public async Task CreateReplacementTask_WasteOfficerClaims_DelegatesAndReturns201()
    {
        var officerId = Guid.NewGuid(); var failedTaskId = Guid.NewGuid(); var request = new CreateReplacementCollectionTaskRequest { ScheduledAt = DateTime.UtcNow.AddHours(1), ReplacementReason = "Officer reviewed and approved a replacement collection." };
        _mockService.Setup(x => x.CreateReplacementTaskAsync(failedTaskId, request, officerId, AppRoles.WasteOfficer, It.IsAny<CancellationToken>())).ReturnsAsync(new CollectionTaskDetailDto { Id = Guid.NewGuid() });
        var result = await CreateControllerWithUser(officerId, AppRoles.WasteOfficer).CreateReplacementTask(failedTaskId, request, CancellationToken.None);
        Assert.IsType<CreatedAtActionResult>(result);
        _mockService.VerifyAll();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 1. GET /api/v1/collection-tasks (GetList)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetList_WasteOfficerClaims_DelegatesToServiceAndReturns200WithPagedResult()
    {
        var officerId = Guid.NewGuid();
        var controller = CreateControllerWithUser(officerId, AppRoles.WasteOfficer);
        var query = new CollectionTaskListQuery
        {
            Status = CollectionTaskStatus.Scheduled,
            TargetType = "Bin",
            CollectionReason = CollectionReason.FullOrBlockedBin,
            Page = 1,
            PageSize = 10
        };

        var expectedResult = new PagedResult<CollectionTaskSummaryDto>
        {
            Items = new List<CollectionTaskSummaryDto>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    TaskCode = "TSK-20260921-0001",
                    TargetType = "Bin",
                    WasteBinId = Guid.NewGuid(),
                    TargetReference = "BIN-COL-0042",
                    CollectionReason = CollectionReason.FullOrBlockedBin,
                    Status = CollectionTaskStatus.Scheduled,
                    ScheduledAt = DateTime.UtcNow.AddHours(2),
                    CreationMethod = TaskCreationMethod.Manual,
                    CreatedByUserId = officerId,
                    CreatedByUserName = "Officer Silva",
                    CreatedAt = DateTime.UtcNow
                }
            },
            Page = 1,
            PageSize = 10,
            TotalCount = 1
        };

        _mockService.Setup(s => s.GetListAsync(query, officerId, AppRoles.WasteOfficer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var actionResult = await controller.GetList(query, CancellationToken.None);

        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().BeSameAs(expectedResult);

        _mockService.Verify(s => s.GetListAsync(query, officerId, AppRoles.WasteOfficer, It.IsAny<CancellationToken>()), Times.Once);
        _mockService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetList_MunicipalManagerClaims_DelegatesToServiceAndReturns200WithPagedResult()
    {
        var managerId = Guid.NewGuid();
        var controller = CreateControllerWithUser(managerId, AppRoles.MunicipalManager);
        var query = new CollectionTaskListQuery();

        var expectedResult = new PagedResult<CollectionTaskSummaryDto>
        {
            Items = new List<CollectionTaskSummaryDto>(),
            Page = 1,
            PageSize = 20,
            TotalCount = 0
        };

        _mockService.Setup(s => s.GetListAsync(query, managerId, AppRoles.MunicipalManager, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var actionResult = await controller.GetList(query, CancellationToken.None);

        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().BeSameAs(expectedResult);

        _mockService.Verify(s => s.GetListAsync(query, managerId, AppRoles.MunicipalManager, It.IsAny<CancellationToken>()), Times.Once);
        _mockService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetList_MissingUserClaim_Returns401Unauthorized()
    {
        var controller = CreateControllerWithUser(null, AppRoles.WasteOfficer);

        var actionResult = await controller.GetList(new CollectionTaskListQuery(), CancellationToken.None);

        var unauthResult = actionResult.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        var problem = unauthResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Status.Should().Be(StatusCodes.Status401Unauthorized);
        problem.Detail.Should().Contain("User identity claim could not be determined");

        _mockService.Verify(s => s.GetListAsync(It.IsAny<CollectionTaskListQuery>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetList_MalformedGuidUserClaim_Returns401Unauthorized()
    {
        var controller = CreateControllerWithUser(null, AppRoles.WasteOfficer, rawUserId: "invalid-not-a-guid");

        var actionResult = await controller.GetList(new CollectionTaskListQuery(), CancellationToken.None);

        var unauthResult = actionResult.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        var problem = unauthResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Status.Should().Be(StatusCodes.Status401Unauthorized);

        _mockService.Verify(s => s.GetListAsync(It.IsAny<CollectionTaskListQuery>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetList_MissingRoleClaim_Returns401Unauthorized()
    {
        var controller = CreateControllerWithUser(Guid.NewGuid(), null);

        var actionResult = await controller.GetList(new CollectionTaskListQuery(), CancellationToken.None);

        var unauthResult = actionResult.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        var problem = unauthResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Status.Should().Be(StatusCodes.Status401Unauthorized);
        problem.Detail.Should().Contain("User role claim could not be determined");

        _mockService.Verify(s => s.GetListAsync(It.IsAny<CollectionTaskListQuery>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 2. GET /api/v1/collection-tasks/{id:guid} (GetById)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetById_WasteOfficerClaims_DelegatesToServiceAndReturns200WithDetailDto()
    {
        var officerId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var controller = CreateControllerWithUser(officerId, AppRoles.WasteOfficer);

        var expectedDetail = new CollectionTaskDetailDto
        {
            Id = taskId,
            TaskCode = "TSK-20260921-0001",
            TargetType = "Bin",
            WasteBinId = Guid.NewGuid(),
            CollectionReason = CollectionReason.FullOrBlockedBin,
            Status = CollectionTaskStatus.Scheduled,
            ScheduledAt = DateTime.UtcNow.AddHours(3),
            CreatedByUserId = officerId,
            CreatedByUserName = "Officer Silva",
            CreationMethod = TaskCreationMethod.Manual,
            CreatedAt = DateTime.UtcNow
        };

        _mockService.Setup(s => s.GetByIdAsync(taskId, officerId, AppRoles.WasteOfficer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDetail);

        var actionResult = await controller.GetById(taskId, CancellationToken.None);

        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().BeSameAs(expectedDetail);

        _mockService.Verify(s => s.GetByIdAsync(taskId, officerId, AppRoles.WasteOfficer, It.IsAny<CancellationToken>()), Times.Once);
        _mockService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetById_MunicipalManagerClaims_DelegatesToServiceAndReturns200WithDetailDto()
    {
        var managerId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var controller = CreateControllerWithUser(managerId, AppRoles.MunicipalManager);

        var expectedDetail = new CollectionTaskDetailDto
        {
            Id = taskId,
            TaskCode = "TSK-20260921-0002",
            TargetType = "Report",
            WasteReportId = Guid.NewGuid(),
            CollectionReason = CollectionReason.VerifiedReport,
            Status = CollectionTaskStatus.Scheduled,
            ScheduledAt = DateTime.UtcNow.AddHours(4),
            CreatedByUserId = Guid.NewGuid(),
            CreatedByUserName = "Officer Silva",
            CreationMethod = TaskCreationMethod.Manual,
            CreatedAt = DateTime.UtcNow
        };

        _mockService.Setup(s => s.GetByIdAsync(taskId, managerId, AppRoles.MunicipalManager, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDetail);

        var actionResult = await controller.GetById(taskId, CancellationToken.None);

        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().BeSameAs(expectedDetail);

        _mockService.Verify(s => s.GetByIdAsync(taskId, managerId, AppRoles.MunicipalManager, It.IsAny<CancellationToken>()), Times.Once);
        _mockService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetById_MissingUserClaim_Returns401Unauthorized()
    {
        var controller = CreateControllerWithUser(null, AppRoles.WasteOfficer);

        var actionResult = await controller.GetById(Guid.NewGuid(), CancellationToken.None);

        var unauthResult = actionResult.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);

        _mockService.Verify(s => s.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 3. POST /api/v1/collection-tasks/manual (CreateManualTask)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateManualTask_WasteOfficerClaims_DelegatesToServiceAndReturns201WithCreatedAtAction()
    {
        var officerId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        var controller = CreateControllerWithUser(officerId, AppRoles.WasteOfficer);

        var request = new CreateManualCollectionTaskRequest
        {
            WasteReportId = reportId,
            CollectionReason = CollectionReason.VerifiedReport,
            ScheduledAt = DateTime.UtcNow.AddHours(2),
            HandlingNotes = "Bulky compaction required"
        };

        var expectedDetail = new CollectionTaskDetailDto
        {
            Id = taskId,
            TaskCode = "TSK-20260921-0003",
            TargetType = "Report",
            WasteReportId = reportId,
            CollectionReason = CollectionReason.VerifiedReport,
            Status = CollectionTaskStatus.Scheduled,
            ScheduledAt = request.ScheduledAt.Value,
            HandlingNotes = request.HandlingNotes,
            CreatedByUserId = officerId,
            CreatedByUserName = "Officer Silva",
            CreationMethod = TaskCreationMethod.Manual,
            CreatedAt = DateTime.UtcNow
        };

        _mockService.Setup(s => s.CreateManualTaskAsync(request, officerId, AppRoles.WasteOfficer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDetail);

        var actionResult = await controller.CreateManualTask(request, CancellationToken.None);

        var createdResult = actionResult.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.StatusCode.Should().Be(StatusCodes.Status201Created);
        createdResult.ActionName.Should().Be(nameof(CollectionTasksController.GetById));
        createdResult.RouteValues.Should().ContainKey("id").WhoseValue.Should().Be(taskId);
        createdResult.Value.Should().BeSameAs(expectedDetail);

        _mockService.Verify(s => s.CreateManualTaskAsync(request, officerId, AppRoles.WasteOfficer, It.IsAny<CancellationToken>()), Times.Once);
        _mockService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CreateManualTask_MissingUserClaim_Returns401Unauthorized()
    {
        var controller = CreateControllerWithUser(null, AppRoles.WasteOfficer);

        var actionResult = await controller.CreateManualTask(new CreateManualCollectionTaskRequest(), CancellationToken.None);

        var unauthResult = actionResult.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);

        _mockService.Verify(s => s.CreateManualTaskAsync(It.IsAny<CreateManualCollectionTaskRequest>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateManualTask_MissingRoleClaim_Returns401Unauthorized()
    {
        var controller = CreateControllerWithUser(Guid.NewGuid(), null);

        var actionResult = await controller.CreateManualTask(new CreateManualCollectionTaskRequest(), CancellationToken.None);

        var unauthResult = actionResult.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);

        _mockService.Verify(s => s.CreateManualTaskAsync(It.IsAny<CreateManualCollectionTaskRequest>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 4. POST /api/v1/collection-tasks/{id:guid}/reschedule (RescheduleTask)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RescheduleTask_WasteOfficerClaims_DelegatesToServiceAndReturns200WithDetailDto()
    {
        var officerId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var controller = CreateControllerWithUser(officerId, AppRoles.WasteOfficer);

        var request = new RescheduleCollectionTaskRequest
        {
            NewScheduledAt = DateTime.UtcNow.AddHours(5),
            Reason = "Depot vehicle maintenance delayed departure."
        };

        var expectedDetail = new CollectionTaskDetailDto
        {
            Id = taskId,
            TaskCode = "TSK-20260921-0001",
            TargetType = "Bin",
            WasteBinId = Guid.NewGuid(),
            CollectionReason = CollectionReason.FullOrBlockedBin,
            Status = CollectionTaskStatus.Scheduled,
            ScheduledAt = request.NewScheduledAt.Value,
            CreatedByUserId = officerId,
            CreatedByUserName = "Officer Silva",
            CreationMethod = TaskCreationMethod.Manual,
            CreatedAt = DateTime.UtcNow.AddHours(-2),
            UpdatedAt = DateTime.UtcNow
        };

        _mockService.Setup(s => s.RescheduleTaskAsync(taskId, request, officerId, AppRoles.WasteOfficer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDetail);

        var actionResult = await controller.RescheduleTask(taskId, request, CancellationToken.None);

        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().BeSameAs(expectedDetail);

        _mockService.Verify(s => s.RescheduleTaskAsync(taskId, request, officerId, AppRoles.WasteOfficer, It.IsAny<CancellationToken>()), Times.Once);
        _mockService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RescheduleTask_MissingUserClaim_Returns401Unauthorized()
    {
        var controller = CreateControllerWithUser(null, AppRoles.WasteOfficer);

        var actionResult = await controller.RescheduleTask(Guid.NewGuid(), new RescheduleCollectionTaskRequest(), CancellationToken.None);

        var unauthResult = actionResult.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);

        _mockService.Verify(s => s.RescheduleTaskAsync(It.IsAny<Guid>(), It.IsAny<RescheduleCollectionTaskRequest>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 5. GET /api/v1/collection-tasks/{id:guid}/history (GetTaskHistory)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTaskHistory_WasteOfficerClaims_DelegatesToServiceAndReturns200WithAuditTrailDto()
    {
        var officerId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var controller = CreateControllerWithUser(officerId, AppRoles.WasteOfficer);

        var expectedAudit = new CollectionTaskAuditTrailDto
        {
            CollectionTaskId = taskId,
            TaskCode = "TSK-20260921-0001",
            StatusHistory = new List<CollectionTaskStatusHistoryDto>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    FromStatus = null,
                    ToStatus = "Scheduled",
                    ChangedByUserId = officerId,
                    ChangedByUserName = "Officer Silva",
                    Notes = "Task manually created",
                    ChangedAt = DateTime.UtcNow.AddHours(-3)
                }
            },
            ScheduleHistory = new List<CollectionTaskScheduleHistoryDto>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    PreviousScheduledAt = DateTime.UtcNow.AddHours(2),
                    NewScheduledAt = DateTime.UtcNow.AddHours(5),
                    Reason = "Vehicle maintenance",
                    RescheduledByUserId = officerId,
                    RescheduledByUserName = "Officer Silva",
                    RescheduledAt = DateTime.UtcNow.AddHours(-1)
                }
            }
        };

        _mockService.Setup(s => s.GetTaskAuditTrailAsync(taskId, officerId, AppRoles.WasteOfficer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedAudit);

        var actionResult = await controller.GetTaskHistory(taskId, CancellationToken.None);

        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().BeSameAs(expectedAudit);

        _mockService.Verify(s => s.GetTaskAuditTrailAsync(taskId, officerId, AppRoles.WasteOfficer, It.IsAny<CancellationToken>()), Times.Once);
        _mockService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetTaskHistory_MunicipalManagerClaims_DelegatesToServiceAndReturns200WithAuditTrailDto()
    {
        var managerId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var controller = CreateControllerWithUser(managerId, AppRoles.MunicipalManager);

        var expectedAudit = new CollectionTaskAuditTrailDto
        {
            CollectionTaskId = taskId,
            TaskCode = "TSK-20260921-0002",
            StatusHistory = new List<CollectionTaskStatusHistoryDto>(),
            ScheduleHistory = new List<CollectionTaskScheduleHistoryDto>()
        };

        _mockService.Setup(s => s.GetTaskAuditTrailAsync(taskId, managerId, AppRoles.MunicipalManager, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedAudit);

        var actionResult = await controller.GetTaskHistory(taskId, CancellationToken.None);

        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().BeSameAs(expectedAudit);

        _mockService.Verify(s => s.GetTaskAuditTrailAsync(taskId, managerId, AppRoles.MunicipalManager, It.IsAny<CancellationToken>()), Times.Once);
        _mockService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetTaskHistory_MissingUserClaim_Returns401Unauthorized()
    {
        var controller = CreateControllerWithUser(null, AppRoles.WasteOfficer);

        var actionResult = await controller.GetTaskHistory(Guid.NewGuid(), CancellationToken.None);

        var unauthResult = actionResult.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);

        _mockService.Verify(s => s.GetTaskAuditTrailAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
