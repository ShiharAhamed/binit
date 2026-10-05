using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartWaste.Application.Common.Models;
using SmartWaste.Application.Operations.DTOs.Requests;
using SmartWaste.Application.Operations.DTOs.Responses;
using SmartWaste.Application.Operations.Interfaces;
using SmartWaste.Application.Operations.Queries;
using SmartWaste.Domain.Common;

namespace SmartWaste.Api.Controllers;

/// <summary>
/// Component 4 — Driver Operational Issues Management API.
/// Provides authenticated endpoints for reporting, listing, querying,
/// reviewing, and resolving field operational issues.
/// </summary>
[ApiController]
[Route("api/v1/operational-issues")]
[Authorize]
public class OperationalIssuesController : ControllerBase
{
    private readonly IOperationalIssueService _operationalIssueService;

    public OperationalIssuesController(IOperationalIssueService operationalIssueService)
    {
        _operationalIssueService = operationalIssueService;
    }

    /// <summary>
    /// Reports a new operational issue from the field. DriverId is determined authoritatively from the caller token.
    /// </summary>
    /// <param name="request">Operational issue details including type, title, description, and optional location.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Created operational issue details.</returns>
    [HttpPost]
    [Authorize(Roles = AppRoles.Driver)]
    [ProducesResponseType(typeof(OperationalIssueDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(
        [FromBody] CreateOperationalIssueRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole, out var errorResult))
        {
            return errorResult!;
        }

        var issue = await _operationalIssueService.CreateAsync(request, actorUserId, actorRole, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = issue.Id }, issue);
    }

    /// <summary>
    /// Lists operational issues reported specifically by the authenticated Driver.
    /// </summary>
    /// <param name="query">Pagination, filtering, and search query parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paginated list of the authenticated Driver's operational issues.</returns>
    [HttpGet("mine")]
    [Authorize(Roles = AppRoles.Driver)]
    [ProducesResponseType(typeof(PagedResult<OperationalIssueSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMine(
        [FromQuery] OperationalIssueListQuery query,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole, out var errorResult))
        {
            return errorResult!;
        }

        var result = await _operationalIssueService.GetMineAsync(query, actorUserId, actorRole, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Lists all operational issues for staff (Waste Officers and Municipal Managers) with filtering, search, and pagination.
    /// </summary>
    /// <param name="query">Pagination, filtering, and search query parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paginated list of operational issues across all drivers.</returns>
    [HttpGet]
    [Authorize(Roles = $"{AppRoles.WasteOfficer},{AppRoles.MunicipalManager}")]
    [ProducesResponseType(typeof(PagedResult<OperationalIssueSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetList(
        [FromQuery] OperationalIssueListQuery query,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole, out var errorResult))
        {
            return errorResult!;
        }

        var result = await _operationalIssueService.GetListAsync(query, actorUserId, actorRole, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves the full details of a specific operational issue.
    /// Drivers can only access their own issues; Waste Officers and Municipal Managers can access any issue.
    /// </summary>
    /// <param name="id">Operational issue unique identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Detailed operational issue information.</returns>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = $"{AppRoles.Driver},{AppRoles.WasteOfficer},{AppRoles.MunicipalManager}")]
    [ProducesResponseType(typeof(OperationalIssueDetailDto), StatusCodes.Status200OK)]
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

        var issue = await _operationalIssueService.GetByIdAsync(id, actorUserId, actorRole, cancellationToken);
        return Ok(issue);
    }

    /// <summary>
    /// Initiates review on an operational issue (Reported -> InReview).
    /// </summary>
    /// <param name="id">Operational issue unique identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Updated operational issue details with InReview status.</returns>
    [HttpPost("{id:guid}/review")]
    [Authorize(Roles = $"{AppRoles.WasteOfficer},{AppRoles.MunicipalManager}")]
    [ProducesResponseType(typeof(OperationalIssueDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> StartReview(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole, out var errorResult))
        {
            return errorResult!;
        }

        var issue = await _operationalIssueService.StartReviewAsync(id, actorUserId, actorRole, cancellationToken);
        return Ok(issue);
    }

    /// <summary>
    /// Resolves an operational issue (InReview -> Resolved).
    /// </summary>
    /// <param name="id">Operational issue unique identifier.</param>
    /// <param name="request">Resolution payload including required resolution note.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Updated operational issue details with Resolved status and resolution metadata.</returns>
    [HttpPost("{id:guid}/resolve")]
    [Authorize(Roles = $"{AppRoles.WasteOfficer},{AppRoles.MunicipalManager}")]
    [ProducesResponseType(typeof(OperationalIssueDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Resolve(
        [FromRoute] Guid id,
        [FromBody] ResolveOperationalIssueRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole, out var errorResult))
        {
            return errorResult!;
        }

        var issue = await _operationalIssueService.ResolveAsync(id, request, actorUserId, actorRole, cancellationToken);
        return Ok(issue);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // ACTOR EXTRACTION HELPER
    // ──────────────────────────────────────────────────────────────────────────

    private bool TryGetActor(out Guid actorUserId, out string actorRole, out IActionResult? errorResult)
    {
        actorUserId = Guid.Empty;
        actorRole = string.Empty;
        errorResult = null;

        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub");

        if (string.IsNullOrWhiteSpace(sub) || !Guid.TryParse(sub, out actorUserId))
        {
            errorResult = Unauthorized(new ProblemDetails
            {
                Title = "Unauthorized",
                Detail = "Authenticated user id claim is missing or malformed.",
                Status = StatusCodes.Status401Unauthorized
            });
            return false;
        }

        actorRole = User.FindFirstValue(ClaimTypes.Role)
                    ?? User.FindFirstValue("role")
                    ?? string.Empty;

        return true;
    }
}
