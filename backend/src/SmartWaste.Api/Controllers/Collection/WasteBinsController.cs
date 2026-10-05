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
/// Exposes public roadside bin discovery for Citizens and operational bin management for Staff.
/// </summary>
[ApiController]
[Route("api/v1/bins")]
[Authorize]
public class WasteBinsController : ControllerBase
{
    private readonly IWasteBinService _wasteBinService;
    private readonly IBinObservationService _binObservationService;

    public WasteBinsController(
        IWasteBinService wasteBinService,
        IBinObservationService binObservationService)
    {
        _wasteBinService = wasteBinService;
        _binObservationService = binObservationService;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 1. PUBLIC CITIZEN READ ENDPOINTS (Section 4.1)
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Retrieves a paginated list of public roadside bins with computed availability and optional proximity filtering.
    /// Restricted to authenticated Citizen.
    /// </summary>
    /// <param name="query">Public filter, proximity, and pagination parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paged list of public bin discovery items.</returns>
    [HttpGet("public")]
    [Authorize(Roles = AppRoles.Citizen)]
    [ProducesResponseType(typeof(PagedResult<PublicWasteBinDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPublicList(
        [FromQuery] PublicWasteBinQuery query,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole, out var errorResult))
        {
            return errorResult!;
        }

        var result = await _wasteBinService.GetPublicListAsync(query, actorUserId, actorRole, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves detailed public operational information for a specific roadside bin.
    /// Restricted to authenticated Citizen.
    /// </summary>
    /// <param name="id">The unique identifier of the waste bin.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Public bin detail item.</returns>
    [HttpGet("public/{id:guid}")]
    [Authorize(Roles = AppRoles.Citizen)]
    [ProducesResponseType(typeof(PublicWasteBinDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPublicById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole, out var errorResult))
        {
            return errorResult!;
        }

        var result = await _wasteBinService.GetPublicByIdAsync(id, actorUserId, actorRole, cancellationToken);
        return Ok(result);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 2. INTERNAL STAFF READ ENDPOINTS (Section 4.2)
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Lists registered bins with full administrative filters for WasteOfficers and MunicipalManagers.
    /// Restricted to WasteOfficer and MunicipalManager.
    /// </summary>
    /// <param name="query">Administrative filter, search, and pagination parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paged summary list of registered waste bins.</returns>
    [HttpGet]
    [Authorize(Roles = $"{AppRoles.WasteOfficer},{AppRoles.MunicipalManager}")]
    [ProducesResponseType(typeof(PagedResult<WasteBinSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetInternalList(
        [FromQuery] WasteBinListQuery query,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole, out var errorResult))
        {
            return errorResult!;
        }

        var result = await _wasteBinService.GetInternalListAsync(query, actorUserId, actorRole, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves full internal operational details of a specific roadside bin.
    /// Restricted to WasteOfficer and MunicipalManager.
    /// </summary>
    /// <param name="id">The unique identifier of the waste bin.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Full internal operational details of the bin.</returns>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = $"{AppRoles.WasteOfficer},{AppRoles.MunicipalManager}")]
    [ProducesResponseType(typeof(WasteBinDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetInternalById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole, out var errorResult))
        {
            return errorResult!;
        }

        var result = await _wasteBinService.GetInternalByIdAsync(id, actorUserId, actorRole, cancellationToken);
        return Ok(result);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 3. INTERNAL STAFF WRITE ENDPOINTS (Section 4.2)
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Registers a new municipal public roadside bin.
    /// Restricted to authenticated WasteOfficer.
    /// </summary>
    /// <param name="request">Bin registration payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Full internal operational details of the newly created bin.</returns>
    [HttpPost]
    [Authorize(Roles = AppRoles.WasteOfficer)]
    [ProducesResponseType(typeof(WasteBinDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateWasteBinRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole, out var errorResult))
        {
            return errorResult!;
        }

        var result = await _wasteBinService.CreateAsync(request, actorUserId, actorRole, cancellationToken);
        return CreatedAtAction(nameof(GetInternalById), new { id = result.Id }, result);
    }

    /// <summary>
    /// Updates operational metadata of a registered bin.
    /// Restricted to authenticated WasteOfficer.
    /// </summary>
    /// <param name="id">The unique identifier of the waste bin to update.</param>
    /// <param name="request">Bin operational metadata update payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Full updated internal operational details of the bin.</returns>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = AppRoles.WasteOfficer)]
    [ProducesResponseType(typeof(WasteBinDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        [FromRoute] Guid id,
        [FromBody] UpdateWasteBinRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole, out var errorResult))
        {
            return errorResult!;
        }

        var result = await _wasteBinService.UpdateAsync(id, request, actorUserId, actorRole, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Changes the administrative status of a bin to OutOfService or Retired.
    /// Restricted to authenticated WasteOfficer.
    /// </summary>
    /// <param name="id">The unique identifier of the waste bin to deactivate.</param>
    /// <param name="request">Deactivation payload containing target status and optional reason.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Full updated internal operational details of the bin.</returns>
    [HttpPost("{id:guid}/deactivate")]
    [Authorize(Roles = AppRoles.WasteOfficer)]
    [ProducesResponseType(typeof(WasteBinDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Deactivate(
        [FromRoute] Guid id,
        [FromBody] DeactivateWasteBinRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole, out var errorResult))
        {
            return errorResult!;
        }

        var result = await _wasteBinService.DeactivateAsync(id, request, actorUserId, actorRole, cancellationToken);
        return Ok(result);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 4. BIN OBSERVATION ENDPOINTS (Sections 4.2.6 & 4.2.7)
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Records an append-only manual field observation for a roadside bin.
    /// Restricted to authenticated WasteOfficer.
    /// </summary>
    /// <param name="id">The unique identifier of the waste bin.</param>
    /// <param name="request">Observation details including fill level, condition, and optional notes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Created bin observation record.</returns>
    [HttpPost("{id:guid}/observations")]
    [Authorize(Roles = AppRoles.WasteOfficer)]
    [ProducesResponseType(typeof(BinObservationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RecordObservation(
        [FromRoute] Guid id,
        [FromBody] RecordBinObservationRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole, out var errorResult))
        {
            return errorResult!;
        }

        var result = await _binObservationService.RecordObservationAsync(id, request, actorUserId, actorRole, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>
    /// Retrieves chronological observation history for a specific roadside bin.
    /// Restricted to authenticated WasteOfficer and MunicipalManager.
    /// </summary>
    /// <param name="id">The unique identifier of the waste bin.</param>
    /// <param name="query">Pagination query parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paged list of chronological bin observations.</returns>
    [HttpGet("{id:guid}/observations")]
    [Authorize(Roles = $"{AppRoles.WasteOfficer},{AppRoles.MunicipalManager}")]
    [ProducesResponseType(typeof(PagedResult<BinObservationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetObservationHistory(
        [FromRoute] Guid id,
        [FromQuery] ObservationListQuery query,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole, out var errorResult))
        {
            return errorResult!;
        }

        var result = await _binObservationService.GetObservationHistoryAsync(id, query, actorUserId, actorRole, cancellationToken);
        return Ok(result);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 5. CLAIMS & IDENTITY EXTRACTION HELPERS
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
