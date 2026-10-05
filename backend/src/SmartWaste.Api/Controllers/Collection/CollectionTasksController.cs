using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartWaste.Application.Collection.DTOs.Requests;
using SmartWaste.Application.Collection.DTOs.Responses;
using SmartWaste.Application.Collection.Interfaces;
using SmartWaste.Application.Collection.Queries;
using SmartWaste.Application.Common.Models;
using SmartWaste.Domain.Common;

namespace SmartWaste.Api.Controllers.Collection;

/// <summary>
/// Component 2 — Waste Collection & Bin Management API.
/// Exposes collection tasks management for staff: creation, listing, detail, rescheduling, and audit trail (Section 4.4).
/// </summary>
[ApiController]
[Route("api/v1/collection-tasks")]
[Authorize]
public class CollectionTasksController : ControllerBase
{
    private readonly ICollectionTaskService _collectionTaskService;
    private readonly IAssignmentReadService _assignmentReadService;

    public CollectionTasksController(ICollectionTaskService collectionTaskService, IAssignmentReadService assignmentReadService)
    {
        _collectionTaskService = collectionTaskService;
        _assignmentReadService = assignmentReadService;
    }

    /// <summary>
    /// Lists collection tasks with operational filters and pagination (Section 4.4.1).
    /// Restricted to authenticated WasteOfficer and MunicipalManager.
    /// </summary>
    /// <param name="query">Operational filter, date range, and pagination parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paged summary list of collection tasks.</returns>
    [HttpGet]
    [Authorize(Roles = $"{AppRoles.WasteOfficer},{AppRoles.MunicipalManager}")]
    [ProducesResponseType(typeof(PagedResult<CollectionTaskSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetList(
        [FromQuery] CollectionTaskListQuery query,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole, out var errorResult))
        {
            return errorResult!;
        }

        var result = await _collectionTaskService.GetListAsync(query, actorUserId, actorRole, cancellationToken);
        return Ok(result);
    }

    [HttpGet("available-for-assignment")]
    [Authorize(Roles = $"{AppRoles.WasteOfficer},{AppRoles.MunicipalManager}")]
    public async Task<IActionResult> GetAvailableForAssignment([FromQuery] AvailableAssignmentTaskQuery query, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole, out var errorResult)) return errorResult!;
        return Ok(await _assignmentReadService.GetAvailableTasksAsync(query, actorUserId, actorRole, cancellationToken));
    }

    /// <summary>
    /// Retrieves detailed collection task information including target details and histories (Section 4.4.2).
    /// Restricted to authenticated WasteOfficer and MunicipalManager.
    /// </summary>
    /// <param name="id">The unique identifier of the collection task.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Full operational details of the collection task.</returns>
    [HttpGet("{id:guid}")]
    [ActionName(nameof(GetById))]
    [Authorize(Roles = $"{AppRoles.WasteOfficer},{AppRoles.MunicipalManager}")]
    [ProducesResponseType(typeof(CollectionTaskDetailDto), StatusCodes.Status200OK)]
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

        var result = await _collectionTaskService.GetByIdAsync(id, actorUserId, actorRole, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Authoritative manual task scheduling command executed by a WasteOfficer (Section 4.4.3).
    /// Restricted to authenticated WasteOfficer only.
    /// </summary>
    /// <param name="request">Manual collection task creation payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Full operational details of the newly scheduled collection task with Location header.</returns>
    [HttpPost("manual")]
    [Authorize(Roles = AppRoles.WasteOfficer)]
    [ProducesResponseType(typeof(CollectionTaskDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateManualTask(
        [FromBody] CreateManualCollectionTaskRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole, out var errorResult))
        {
            return errorResult!;
        }

        var result = await _collectionTaskService.CreateManualTaskAsync(request, actorUserId, actorRole, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPost("{failedTaskId:guid}/replacement")]
    [Authorize(Roles = AppRoles.WasteOfficer)]
    [ProducesResponseType(typeof(CollectionTaskDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateReplacementTask(
        [FromRoute] Guid failedTaskId,
        [FromBody] CreateReplacementCollectionTaskRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole, out var errorResult)) return errorResult!;
        var result = await _collectionTaskService.CreateReplacementTaskAsync(failedTaskId, request, actorUserId, actorRole, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>
    /// Reschedules an unstarted collection task to a new planned execution time (Section 4.4.4).
    /// Restricted to authenticated WasteOfficer only.
    /// </summary>
    /// <param name="id">The unique identifier of the collection task to reschedule.</param>
    /// <param name="request">Rescheduling payload containing new scheduled time and justification.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Full updated operational details of the collection task.</returns>
    [HttpPost("{id:guid}/reschedule")]
    [Authorize(Roles = AppRoles.WasteOfficer)]
    [ProducesResponseType(typeof(CollectionTaskDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RescheduleTask(
        [FromRoute] Guid id,
        [FromBody] RescheduleCollectionTaskRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole, out var errorResult))
        {
            return errorResult!;
        }

        var result = await _collectionTaskService.RescheduleTaskAsync(id, request, actorUserId, actorRole, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves the complete audit trail of status transitions and reschedule events for a collection task (Section 4.4.5).
    /// Restricted to authenticated WasteOfficer and MunicipalManager.
    /// </summary>
    /// <param name="id">The unique identifier of the collection task.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Complete audit trail of the collection task.</returns>
    [HttpGet("{id:guid}/history")]
    [Authorize(Roles = $"{AppRoles.WasteOfficer},{AppRoles.MunicipalManager}")]
    [ProducesResponseType(typeof(CollectionTaskAuditTrailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTaskHistory(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole, out var errorResult))
        {
            return errorResult!;
        }

        var result = await _collectionTaskService.GetTaskAuditTrailAsync(id, actorUserId, actorRole, cancellationToken);
        return Ok(result);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // CLAIMS & IDENTITY EXTRACTION HELPERS
    // ──────────────────────────────────────────────────────────────────────────

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

        if (string.IsNullOrEmpty(actorRole))
        {
            errorResult = Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Detail = "User role claim could not be determined."
            });
            return false;
        }

        errorResult = null;
        return true;
    }
}
