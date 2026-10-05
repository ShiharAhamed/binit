using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartWaste.Application.Common.Models;
using SmartWaste.Application.Complaints.DTOs.Requests;
using SmartWaste.Application.Complaints.DTOs.Responses;
using SmartWaste.Application.Complaints.Interfaces;
using SmartWaste.Application.Complaints.Queries;
using SmartWaste.Domain.Common;

namespace SmartWaste.Api.Controllers;

/// <summary>
/// Component 4 — Citizen Complaints Management API.
/// Provides authenticated endpoints for submitting, listing, querying,
/// reviewing, and resolving citizen service complaints.
/// </summary>
[ApiController]
[Route("api/v1/complaints")]
[Authorize]
public class ComplaintsController : ControllerBase
{
    private readonly IComplaintService _complaintService;

    public ComplaintsController(IComplaintService complaintService)
    {
        _complaintService = complaintService;
    }

    /// <summary>
    /// Submits a new citizen service complaint. CitizenId is determined authoritatively from the caller token.
    /// </summary>
    /// <param name="request">Complaint details including category, subject, description, and optional location.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Created complaint details.</returns>
    [HttpPost]
    [Authorize(Roles = AppRoles.Citizen)]
    [ProducesResponseType(typeof(ComplaintDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(
        [FromBody] CreateComplaintRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole, out var errorResult))
        {
            return errorResult!;
        }

        var complaint = await _complaintService.CreateAsync(request, actorUserId, actorRole, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = complaint.Id }, complaint);
    }

    /// <summary>
    /// Lists complaints with pagination, filtering, search, and sorting. Scope is role-enforced.
    /// Citizens see only their own complaints; Waste Officers and Municipal Managers see all complaints.
    /// </summary>
    /// <param name="query">Pagination, filtering, and search query parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paged summary list of complaints.</returns>
    [HttpGet]
    [Authorize(Roles = $"{AppRoles.Citizen},{AppRoles.WasteOfficer},{AppRoles.MunicipalManager}")]
    [ProducesResponseType(typeof(PagedResult<ComplaintSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetList(
        [FromQuery] ComplaintListQuery query,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole, out var errorResult))
        {
            return errorResult!;
        }

        var result = await _complaintService.GetListAsync(query, actorUserId, actorRole, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves full details of a specific complaint by ID.
    /// Citizens may only access their own complaints; staff may access any complaint.
    /// </summary>
    /// <param name="id">The complaint unique identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Full complaint detail with resolution and optional location snapshot.</returns>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = $"{AppRoles.Citizen},{AppRoles.WasteOfficer},{AppRoles.MunicipalManager}")]
    [ProducesResponseType(typeof(ComplaintDetailDto), StatusCodes.Status200OK)]
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

        var complaint = await _complaintService.GetByIdAsync(id, actorUserId, actorRole, cancellationToken);
        return Ok(complaint);
    }

    /// <summary>
    /// Waste Officer or Municipal Manager initiates review of a Submitted complaint (Submitted -> InReview).
    /// </summary>
    /// <param name="id">The complaint unique identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Updated complaint details in InReview status.</returns>
    [HttpPost("{id:guid}/review")]
    [Authorize(Roles = $"{AppRoles.WasteOfficer},{AppRoles.MunicipalManager}")]
    [ProducesResponseType(typeof(ComplaintDetailDto), StatusCodes.Status200OK)]
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

        var complaint = await _complaintService.StartReviewAsync(id, actorUserId, actorRole, cancellationToken);
        return Ok(complaint);
    }

    /// <summary>
    /// Waste Officer or Municipal Manager resolves an InReview complaint (InReview -> Resolved).
    /// </summary>
    /// <param name="id">The complaint unique identifier.</param>
    /// <param name="request">Resolution note payload explaining the outcome.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Updated complaint details in terminal Resolved status.</returns>
    [HttpPost("{id:guid}/resolve")]
    [Authorize(Roles = $"{AppRoles.WasteOfficer},{AppRoles.MunicipalManager}")]
    [ProducesResponseType(typeof(ComplaintDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Resolve(
        [FromRoute] Guid id,
        [FromBody] ResolveComplaintRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole, out var errorResult))
        {
            return errorResult!;
        }

        var complaint = await _complaintService.ResolveAsync(id, request, actorUserId, actorRole, cancellationToken);
        return Ok(complaint);
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
