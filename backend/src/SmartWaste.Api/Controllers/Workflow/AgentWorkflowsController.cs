using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartWaste.Application.Common.Models;
using SmartWaste.Application.Workflow.DTOs.Requests;
using SmartWaste.Application.Workflow.DTOs.Responses;
using SmartWaste.Application.Workflow.Interfaces;
using SmartWaste.Domain.Common;

namespace SmartWaste.Api.Controllers.Workflow;

/// <summary>
/// Authenticated HTTP API for creating, listing, inspecting, auditing, and starting Agentic workflows.
/// Reuses the Step 4 persistence and deterministic state-machine foundation.
/// </summary>
[ApiController]
[Route("api/v1/agent-workflows")]
[Authorize]
public class AgentWorkflowsController : ControllerBase
{
    private readonly IAgentWorkflowService _workflowService;
    private readonly ILogger<AgentWorkflowsController> _logger;

    public AgentWorkflowsController(
        IAgentWorkflowService workflowService,
        ILogger<AgentWorkflowsController> logger)
    {
        _workflowService = workflowService;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new AgentWorkflow in Created state with an initial transition record.
    /// Initiator identity is determined authoritatively from caller authentication claims.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = AppRoles.AgentWorkflowAuthorityRoles)]
    [ProducesResponseType(typeof(AgentWorkflowSummaryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(
        [FromBody] CreateAgentWorkflowRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole, out var errorResult))
        {
            return errorResult!;
        }

        var workflow = await _workflowService.CreateWorkflowAsync(request.Objective, actorUserId, cancellationToken);

        var response = new AgentWorkflowSummaryDto
        {
            Id = workflow.Id,
            TriggerType = workflow.TriggerType,
            TriggeringWasteReportId = workflow.TriggeringWasteReportId,
            Objective = workflow.Objective,
            Status = workflow.Status,
            CurrentStep = workflow.CurrentStep,
            InitiatedByUserId = workflow.InitiatedByUserId,
            CreatedAt = workflow.CreatedAt,
            UpdatedAt = workflow.UpdatedAt,
            CompletedAt = workflow.CompletedAt,
            FinalOutcome = workflow.FinalOutcome,
            Version = workflow.Version
        };

        return CreatedAtAction(nameof(GetById), new { id = response.Id }, response);
    }

    /// <summary>
    /// Lists accessible workflows with bounded pagination and optional status filter.
    /// Equal visibility: MunicipalManager and WasteOfficer have equal operational visibility across all workflows.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = AppRoles.AgentWorkflowAuthorityRoles)]
    [ProducesResponseType(typeof(PagedResult<AgentWorkflowSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetList(
        [FromQuery] AgentWorkflowListQuery query,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole, out var errorResult))
        {
            return errorResult!;
        }

        var result = await _workflowService.GetWorkflowsAsync(query, actorUserId, actorRole, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves full structured view of a workflow including child steps, transitions, approvals, and execution results.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ActionName(nameof(GetById))]
    [Authorize(Roles = AppRoles.AgentWorkflowAuthorityRoles)]
    [ProducesResponseType(typeof(AgentWorkflowDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole, out var errorResult))
        {
            return errorResult!;
        }

        var detail = await _workflowService.GetWorkflowDetailsAsync(id, actorUserId, actorRole, cancellationToken);
        return Ok(detail);
    }

    /// <summary>
    /// Retrieves the chronological audit transition history for a workflow.
    /// </summary>
    [HttpGet("{id:guid}/history")]
    [Authorize(Roles = AppRoles.AgentWorkflowAuthorityRoles)]
    [ProducesResponseType(typeof(IReadOnlyList<AgentWorkflowTransitionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetHistory(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole, out var errorResult))
        {
            return errorResult!;
        }

        var history = await _workflowService.GetWorkflowHistoryAsync(id, actorUserId, actorRole, cancellationToken);
        return Ok(history);
    }

    /// <summary>
    /// Atomically starts a Created workflow, transitioning it to Planning with CurrentStep=SharedPlanning.
    /// Does not trigger external Python AI execution.
    /// </summary>
    [HttpPost("{id:guid}/start")]
    [Authorize(Roles = AppRoles.AgentWorkflowAuthorityRoles)]
    [ProducesResponseType(typeof(AgentWorkflowSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Start(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole, out var errorResult))
        {
            return errorResult!;
        }

        var started = await _workflowService.StartWorkflowAsync(id, actorUserId, actorRole, cancellationToken);
        return Ok(started);
    }

    /// <summary>
    /// Extracts authenticated user identity (Guid) and primary role from ClaimsPrincipal.
    /// Returns 401 Unauthorized if claims are missing or malformed.
    /// </summary>
    private bool TryGetActor(out Guid actorUserId, out string actorRole, out IActionResult? errorResult)
    {
        actorUserId = Guid.Empty;
        actorRole = string.Empty;

        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier)
                          ?? User.FindFirstValue("sub");

        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out actorUserId))
        {
            errorResult = Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Detail = "User identity claim could not be determined."
            });
            return false;
        }

        actorRole = User.FindFirstValue(ClaimTypes.Role)
                    ?? User.FindFirstValue("role")
                    ?? string.Empty;

        errorResult = null;
        return true;
    }
}
