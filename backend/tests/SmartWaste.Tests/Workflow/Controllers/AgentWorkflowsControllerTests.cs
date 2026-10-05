using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SmartWaste.Api.Controllers.Workflow;
using SmartWaste.Application.Common.Exceptions;
using SmartWaste.Application.Common.Models;
using SmartWaste.Application.Workflow.DTOs.Requests;
using SmartWaste.Application.Workflow.DTOs.Responses;
using SmartWaste.Application.Workflow.Interfaces;
using SmartWaste.Domain.Common;
using SmartWaste.Domain.Workflow.Entities;
using SmartWaste.Domain.Workflow.Enums;
using Xunit;

namespace SmartWaste.Tests.Workflow.Controllers;

public class AgentWorkflowsControllerTests
{
    private readonly Mock<IAgentWorkflowService> _workflowServiceMock = new();
    private readonly AgentWorkflowsController _controller;

    public AgentWorkflowsControllerTests()
    {
        _controller = new AgentWorkflowsController(
            _workflowServiceMock.Object,
            NullLogger<AgentWorkflowsController>.Instance);
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
    public void Controller_DeclaresAuthorizedRoles_WasteOfficerAndMunicipalManager()
    {
        var classAuthorize = typeof(AgentWorkflowsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .ToList();
        classAuthorize.Should().NotBeEmpty();

        var methodAuthorize = typeof(AgentWorkflowsController)
            .GetMethod(nameof(AgentWorkflowsController.Create))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        methodAuthorize.Roles.Should().Contain(AppRoles.WasteOfficer);
        methodAuthorize.Roles.Should().Contain(AppRoles.MunicipalManager);
    }

    // =========================================================================
    // 2. POST /api/v1/agent-workflows (Create)
    // =========================================================================

    [Fact]
    public async Task Create_AuthenticatedOfficer_CreatesWorkflowAndReturns201Created()
    {
        var actorId = Guid.NewGuid();
        WithActor(_controller, actorId, AppRoles.WasteOfficer);

        var request = new CreateAgentWorkflowRequest
        {
            Objective = "Prepare an end-to-end municipal collection run for Zone A."
        };

        var createdEntity = new AgentWorkflow
        {
            Id = Guid.NewGuid(),
            Objective = request.Objective,
            Status = AgentWorkflowStatus.Created,
            CurrentStep = WorkflowStepType.None,
            InitiatedByUserId = actorId,
            CreatedAt = DateTime.UtcNow,
            Version = 1
        };

        _workflowServiceMock.Setup(s => s.CreateWorkflowAsync(request.Objective, actorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdEntity);

        var result = await _controller.Create(request, CancellationToken.None);

        var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.StatusCode.Should().Be(StatusCodes.Status201Created);
        createdResult.ActionName.Should().Be(nameof(AgentWorkflowsController.GetById));

        var responseDto = createdResult.Value.Should().BeOfType<AgentWorkflowSummaryDto>().Subject;
        responseDto.Id.Should().Be(createdEntity.Id);
        responseDto.Objective.Should().Be(request.Objective);
        responseDto.Status.Should().Be(AgentWorkflowStatus.Created);
        responseDto.CurrentStep.Should().Be(WorkflowStepType.None);
        responseDto.InitiatedByUserId.Should().Be(actorId);
        responseDto.Version.Should().Be(1);
    }

    [Fact]
    public async Task Create_Unauthenticated_Returns401Unauthorized()
    {
        WithAnonymous(_controller);

        var request = new CreateAgentWorkflowRequest { Objective = "Objective with valid length" };
        var result = await _controller.Create(request, CancellationToken.None);

        var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthorizedResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    // =========================================================================
    // 3. GET /api/v1/agent-workflows (List)
    // =========================================================================

    [Fact]
    public async Task GetList_AuthenticatedStaff_ReturnsPagedSummaryList()
    {
        var actorId = Guid.NewGuid();
        WithActor(_controller, actorId, AppRoles.WasteOfficer);

        var query = new AgentWorkflowListQuery { Page = 1, PageSize = 20 };
        var pagedResult = new PagedResult<AgentWorkflowSummaryDto>
        {
            Items = new[]
            {
                new AgentWorkflowSummaryDto
                {
                    Id = Guid.NewGuid(),
                    Objective = "Plan 1",
                    Status = AgentWorkflowStatus.Created,
                    CurrentStep = WorkflowStepType.None,
                    InitiatedByUserId = actorId,
                    CreatedAt = DateTime.UtcNow,
                    Version = 1
                }
            },
            Page = 1,
            PageSize = 20,
            TotalCount = 1
        };

        _workflowServiceMock.Setup(s => s.GetWorkflowsAsync(query, actorId, AppRoles.WasteOfficer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        var result = await _controller.GetList(query, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(StatusCodes.Status200OK);
        var returned = okResult.Value.Should().BeOfType<PagedResult<AgentWorkflowSummaryDto>>().Subject;
        returned.TotalCount.Should().Be(1);
        returned.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetList_Unauthenticated_Returns401Unauthorized()
    {
        WithAnonymous(_controller);
        var result = await _controller.GetList(new AgentWorkflowListQuery(), CancellationToken.None);
        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    // =========================================================================
    // 4. GET /api/v1/agent-workflows/{id} (Details)
    // =========================================================================

    [Fact]
    public async Task GetById_ExistingWorkflow_Returns200WithFullDetailDto()
    {
        var actorId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        WithActor(_controller, actorId, AppRoles.MunicipalManager);

        var detailDto = new AgentWorkflowDetailDto
        {
            Id = workflowId,
            Objective = "Objective for full inspection",
            Status = AgentWorkflowStatus.Planning,
            CurrentStep = WorkflowStepType.SharedPlanning,
            InitiatedByUserId = actorId,
            CreatedAt = DateTime.UtcNow,
            Version = 2,
            Steps = new[]
            {
                new AgentWorkflowStepDto { Id = Guid.NewGuid(), Sequence = 1, StepType = WorkflowStepType.CollectionPlanning, Status = WorkflowStepStatus.Completed }
            },
            Transitions = new[]
            {
                new AgentWorkflowTransitionDto { Id = Guid.NewGuid(), FromStatus = null, ToStatus = AgentWorkflowStatus.Created, ChangedAt = DateTime.UtcNow.AddMinutes(-5) },
                new AgentWorkflowTransitionDto { Id = Guid.NewGuid(), FromStatus = AgentWorkflowStatus.Created, ToStatus = AgentWorkflowStatus.Planning, ChangedAt = DateTime.UtcNow }
            }
        };

        _workflowServiceMock.Setup(s => s.GetWorkflowDetailsAsync(workflowId, actorId, AppRoles.MunicipalManager, It.IsAny<CancellationToken>()))
            .ReturnsAsync(detailDto);

        var result = await _controller.GetById(workflowId, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var returned = okResult.Value.Should().BeOfType<AgentWorkflowDetailDto>().Subject;
        returned.Id.Should().Be(workflowId);
        returned.Steps.Should().HaveCount(1);
        returned.Transitions.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetById_UnknownId_ThrowsNotFoundException_HandledByMiddleware()
    {
        var actorId = Guid.NewGuid();
        var unknownId = Guid.NewGuid();
        WithActor(_controller, actorId, AppRoles.MunicipalManager);

        _workflowServiceMock.Setup(s => s.GetWorkflowDetailsAsync(unknownId, actorId, AppRoles.MunicipalManager, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException($"AgentWorkflow with ID '{unknownId}' was not found."));

        var act = () => _controller.GetById(unknownId, CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetById_UnauthorizedUser_ThrowsForbiddenException_HandledByMiddleware()
    {
        var actorId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        WithActor(_controller, actorId, AppRoles.WasteOfficer);

        _workflowServiceMock.Setup(s => s.GetWorkflowDetailsAsync(workflowId, actorId, AppRoles.WasteOfficer, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ForbiddenException("You do not have permission to view this workflow."));

        var act = () => _controller.GetById(workflowId, CancellationToken.None);
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    // =========================================================================
    // 5. GET /api/v1/agent-workflows/{id}/history (History)
    // =========================================================================

    [Fact]
    public async Task GetHistory_ExistingWorkflow_ReturnsChronologicalTransitions()
    {
        var actorId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        WithActor(_controller, actorId, AppRoles.WasteOfficer);

        var historyList = new List<AgentWorkflowTransitionDto>
        {
            new() { Id = Guid.NewGuid(), FromStatus = null, ToStatus = AgentWorkflowStatus.Created, ChangedAt = DateTime.UtcNow.AddMinutes(-10) },
            new() { Id = Guid.NewGuid(), FromStatus = AgentWorkflowStatus.Created, ToStatus = AgentWorkflowStatus.Planning, ChangedAt = DateTime.UtcNow.AddMinutes(-5) }
        };

        _workflowServiceMock.Setup(s => s.GetWorkflowHistoryAsync(workflowId, actorId, AppRoles.WasteOfficer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(historyList);

        var result = await _controller.GetHistory(workflowId, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var returned = okResult.Value.Should().BeAssignableTo<IReadOnlyList<AgentWorkflowTransitionDto>>().Subject;
        returned.Should().HaveCount(2);
    }

    // =========================================================================
    // 6. POST /api/v1/agent-workflows/{id}/start (Start)
    // =========================================================================

    [Fact]
    public async Task Start_CreatedWorkflow_AtomicallyTransitionsToPlanningAndReturns200()
    {
        var actorId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        WithActor(_controller, actorId, AppRoles.WasteOfficer);

        var startedSummary = new AgentWorkflowSummaryDto
        {
            Id = workflowId,
            Objective = "Started objective",
            Status = AgentWorkflowStatus.Planning,
            CurrentStep = WorkflowStepType.SharedPlanning,
            InitiatedByUserId = actorId,
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            UpdatedAt = DateTime.UtcNow,
            Version = 2
        };

        _workflowServiceMock.Setup(s => s.StartWorkflowAsync(workflowId, actorId, AppRoles.WasteOfficer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(startedSummary);

        var result = await _controller.Start(workflowId, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var returned = okResult.Value.Should().BeOfType<AgentWorkflowSummaryDto>().Subject;
        returned.Status.Should().Be(AgentWorkflowStatus.Planning);
        returned.CurrentStep.Should().Be(WorkflowStepType.SharedPlanning);
        returned.Version.Should().Be(2);
    }

    [Fact]
    public async Task Start_AlreadyPlanning_ThrowsInvalidWorkflowTransitionException()
    {
        var actorId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        WithActor(_controller, actorId, AppRoles.WasteOfficer);

        _workflowServiceMock.Setup(s => s.StartWorkflowAsync(workflowId, actorId, AppRoles.WasteOfficer, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidWorkflowTransitionException(AgentWorkflowStatus.Planning, AgentWorkflowStatus.Planning));

        var act = () => _controller.Start(workflowId, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidWorkflowTransitionException>();
    }

    [Fact]
    public async Task Start_TerminalWorkflow_ThrowsInvalidWorkflowTransitionException()
    {
        var actorId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        WithActor(_controller, actorId, AppRoles.WasteOfficer);

        _workflowServiceMock.Setup(s => s.StartWorkflowAsync(workflowId, actorId, AppRoles.WasteOfficer, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidWorkflowTransitionException("Cannot transition from terminal state 'Completed'."));

        var act = () => _controller.Start(workflowId, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidWorkflowTransitionException>();
    }

    [Fact]
    public async Task Start_RejectedWorkflow_ThrowsInvalidWorkflowTransitionException()
    {
        var actorId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        WithActor(_controller, actorId, AppRoles.WasteOfficer);

        _workflowServiceMock.Setup(s => s.StartWorkflowAsync(workflowId, actorId, AppRoles.WasteOfficer, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidWorkflowTransitionException(AgentWorkflowStatus.Rejected, AgentWorkflowStatus.Planning));

        var act = () => _controller.Start(workflowId, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidWorkflowTransitionException>();
    }

    [Fact]
    public async Task Start_ConcurrentConflict_ThrowsBusinessRuleConflictException()
    {
        var actorId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        WithActor(_controller, actorId, AppRoles.WasteOfficer);

        _workflowServiceMock.Setup(s => s.StartWorkflowAsync(workflowId, actorId, AppRoles.WasteOfficer, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BusinessRuleConflictException("The workflow was modified concurrently by another operation. Please reload and try again."));

        var act = () => _controller.Start(workflowId, CancellationToken.None);
        await act.Should().ThrowAsync<BusinessRuleConflictException>();
    }

    // =========================================================================
    // 7. Security and DTO Boundary Invariant Check
    // =========================================================================

    [Fact]
    public void DTOs_DoNotContainHiddenReasoningProperties()
    {
        var dtoTypes = new[]
        {
            typeof(CreateAgentWorkflowRequest),
            typeof(AgentWorkflowListQuery),
            typeof(AgentWorkflowSummaryDto),
            typeof(AgentWorkflowDetailDto),
            typeof(AgentWorkflowStepDto),
            typeof(AgentWorkflowTransitionDto),
            typeof(AgentWorkflowApprovalDto),
            typeof(AgentWorkflowExecutionResultDto)
        };

        var forbiddenNames = new[] { "ChainOfThought", "ReasoningTrace", "HiddenReasoning", "HiddenScratchpad" };

        foreach (var type in dtoTypes)
        {
            var propNames = type.GetProperties().Select(p => p.Name).ToList();
            propNames.Should().NotContain(forbiddenNames, because: $"{type.Name} must strictly guard against hidden reasoning leakage.");
        }
    }
}
