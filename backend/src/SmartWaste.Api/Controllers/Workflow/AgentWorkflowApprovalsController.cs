using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartWaste.Application.Workflow.DTOs.Requests;
using SmartWaste.Application.Workflow.DTOs.Responses;
using SmartWaste.Application.Workflow.Interfaces;
using SmartWaste.Domain.Common;

namespace SmartWaste.Api.Controllers.Workflow;

/// <summary>
/// Authenticated HTTP API for human-in-the-loop review and approval decisions.
/// Enforces Gate 1 (Collection Planning) and Gate 2 (Fleet Dispatch) state transitions.
/// Authorized for MunicipalManager and WasteOfficer roles.
/// </summary>
[ApiController]
[Route("api/v1/agent-workflows/{id:guid}")]
[Authorize(Roles = AppRoles.AgentWorkflowAuthorityRoles)]
public class AgentWorkflowApprovalsController : ControllerBase
{
    private readonly IAgentWorkflowService _workflowService;
    private readonly ILogger<AgentWorkflowApprovalsController> _logger;

    public AgentWorkflowApprovalsController(
        IAgentWorkflowService workflowService,
        ILogger<AgentWorkflowApprovalsController> logger)
    {
        _workflowService = workflowService;
        _logger = logger;
    }

    /// <summary>
    /// Approves the collection planning proposal at Gate 1 (AwaitingCollectionApproval -> CollectionApproved).
    /// Requires that C2 output represents a complete snapshot with status 'completed'.
    /// </summary>
    [HttpPost("collection-approval/approve")]
    [ProducesResponseType(typeof(AgentWorkflowDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ApproveCollection(
        [FromRoute] Guid id,
        [FromBody] ApproveCollectionPlanningRequest request,
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

        var result = await _workflowService.ApproveCollectionPlanningAsync(id, request, actorUserId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Requests revision of the collection planning proposal at Gate 1 (AwaitingCollectionApproval -> CollectionNeedsRevision).
    /// Requires reviewer feedback notes.
    /// </summary>
    [HttpPost("collection-approval/request-revision")]
    [ProducesResponseType(typeof(AgentWorkflowDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RequestCollectionRevision(
        [FromRoute] Guid id,
        [FromBody] RequestCollectionRevisionRequest request,
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

        var result = await _workflowService.RequestCollectionRevisionAsync(id, request, actorUserId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Authoritatively rejects the collection planning proposal at Gate 1 (AwaitingCollectionApproval -> Rejected).
    /// Sets terminal CompletedAt timestamp.
    /// </summary>
    [HttpPost("collection-approval/reject")]
    [ProducesResponseType(typeof(AgentWorkflowDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RejectCollection(
        [FromRoute] Guid id,
        [FromBody] RejectCollectionPlanningRequest request,
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

        var result = await _workflowService.RejectCollectionPlanningAsync(id, request, actorUserId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Approves the fleet dispatch proposal at Gate 2 (AwaitingDispatchApproval -> DispatchApproved).
    /// Requires C4 outcome 'ReadyForHumanReview'. If requiresAcknowledgement=true, acknowledgeWarnings must be true.
    /// </summary>
    [HttpPost("dispatch-approval/approve")]
    [ProducesResponseType(typeof(AgentWorkflowDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ApproveDispatch(
        [FromRoute] Guid id,
        [FromBody] ApproveDispatchPlanRequest request,
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

        var result = await _workflowService.ApproveDispatchPlanAsync(id, request, actorUserId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Requests revision of the fleet dispatch proposal at Gate 2 (AwaitingDispatchApproval -> DispatchNeedsRevision).
    /// Requires reviewer feedback notes.
    /// </summary>
    [HttpPost("dispatch-approval/request-revision")]
    [ProducesResponseType(typeof(AgentWorkflowDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RequestDispatchRevision(
        [FromRoute] Guid id,
        [FromBody] RequestDispatchRevisionRequest request,
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

        var result = await _workflowService.RequestDispatchRevisionAsync(id, request, actorUserId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Authoritatively rejects the fleet dispatch proposal at Gate 2 (AwaitingDispatchApproval -> Rejected).
    /// Sets terminal CompletedAt timestamp.
    /// </summary>
    [HttpPost("dispatch-approval/reject")]
    [ProducesResponseType(typeof(AgentWorkflowDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RejectDispatch(
        [FromRoute] Guid id,
        [FromBody] RejectDispatchPlanRequest request,
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

        var result = await _workflowService.RejectDispatchPlanAsync(id, request, actorUserId, cancellationToken);
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
