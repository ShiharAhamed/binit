using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartWaste.Application.Workflow.DTOs.Requests;
using SmartWaste.Application.Workflow.DTOs.Responses;
using SmartWaste.Application.Workflow.Interfaces;
using SmartWaste.Domain.Common;

namespace SmartWaste.Api.Controllers.Workflow;

/// <summary>
/// Authenticated HTTP API for authoritative execution bridges in the Agentic AI workflow.
/// Converts approved advisory proposals into real operational entities (e.g. Scheduled CollectionTasks).
/// Authorized for MunicipalManager and WasteOfficer roles.
/// </summary>
[ApiController]
[Route("api/v1/agent-workflows/{id:guid}")]
[Authorize(Roles = AppRoles.AgentWorkflowAuthorityRoles)]
public class AgentWorkflowExecutionsController : ControllerBase
{
    private readonly IAgentWorkflowService _workflowService;
    private readonly ILogger<AgentWorkflowExecutionsController> _logger;

    public AgentWorkflowExecutionsController(
        IAgentWorkflowService workflowService,
        ILogger<AgentWorkflowExecutionsController> logger)
    {
        _workflowService = workflowService;
        _logger = logger;
    }

    /// <summary>
    /// Converts an approved C2 collection planning proposal into real authoritative Scheduled CollectionTasks (Step 7).
    /// Enforces:
    /// - Role: MunicipalManager or WasteOfficer
    /// - State: CollectionApproved -> CreatingScheduledTasks -> FleetPlanning
    /// - Human approval prerequisite
    /// - Fresh authoritative need re-validation
    /// - Atomic creation through existing CollectionTaskService
    /// </summary>
    /// <param name="id">The workflow aggregate identifier.</param>
    /// <param name="request">Execution command containing expectedVersion.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Updated AgentWorkflowDetailDto reflecting FleetPlanning status and execution results.</returns>
    [HttpPost("execute-collection-plan")]
    [ProducesResponseType(typeof(AgentWorkflowDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ExecuteCollectionPlan(
        [FromRoute] Guid id,
        [FromBody] ExecuteCollectionPlanRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole, out var errorResult))
        {
            return errorResult!;
        }

        if (!AppRoles.IsAgentWorkflowAuthority(actorRole))
        {
            return Forbid();
        }

        var result = await _workflowService.ExecuteCollectionPlanAsync(id, request, actorUserId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Converts an approved C3 fleet dispatch proposal into real authoritative CollectionAssignments (Step 8).
    /// Enforces:
    /// - Role: MunicipalManager or WasteOfficer
    /// - State: DispatchApproved -> ExecutingAssignments -> Completed (or Failed)
    /// - Gate 2 human approval bound to completed OperationalValidation step
    /// - C4 ReadyForHumanReview output and warning acknowledgement
    /// - Fresh authoritative task, driver, vehicle, and compatibility re-validation
    /// - Atomic creation through existing CollectionAssignmentService
    /// </summary>
    /// <param name="id">The workflow aggregate identifier.</param>
    /// <param name="request">Execution command containing expectedVersion.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Updated AgentWorkflowDetailDto reflecting Completed status and assignment execution results.</returns>
    [HttpPost("execute-dispatch-plan")]
    [ProducesResponseType(typeof(AgentWorkflowDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ExecuteDispatchPlan(
        [FromRoute] Guid id,
        [FromBody] ExecuteDispatchPlanRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole, out var errorResult))
        {
            return errorResult!;
        }

        if (!AppRoles.IsAgentWorkflowAuthority(actorRole))
        {
            return Forbid();
        }

        var result = await _workflowService.ExecuteDispatchPlanAsync(id, request, actorUserId, cancellationToken);
        return Ok(result);
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
