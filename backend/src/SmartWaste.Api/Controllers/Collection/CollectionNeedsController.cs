using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartWaste.Application.Collection.DTOs.Responses;
using SmartWaste.Application.Collection.Interfaces;
using SmartWaste.Application.Collection.Queries;
using SmartWaste.Application.Common.Models;
using SmartWaste.Domain.Common;

namespace SmartWaste.Api.Controllers.Collection;

/// <summary>
/// Component 2 — Waste Collection & Bin Management API.
/// Exposes the unified, derived, read-only collection needs queue for staff (Section 4.3).
/// </summary>
[ApiController]
[Route("api/v1/collection-needs")]
[Authorize]
public class CollectionNeedsController : ControllerBase
{
    private readonly ICollectionNeedService _collectionNeedService;

    public CollectionNeedsController(ICollectionNeedService collectionNeedService)
    {
        _collectionNeedService = collectionNeedService;
    }

    /// <summary>
    /// Retrieves the unified derived read queue of outstanding collection needs across the municipality.
    /// Restricted to authenticated WasteOfficer and MunicipalManager.
    /// </summary>
    /// <param name="query">Filter, search, and pagination parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paged summary list of derived collection needs.</returns>
    [HttpGet]
    [Authorize(Roles = $"{AppRoles.WasteOfficer},{AppRoles.MunicipalManager}")]
    [ProducesResponseType(typeof(PagedResult<CollectionNeedItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetCollectionNeeds(
        [FromQuery] CollectionNeedListQuery query,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole, out var errorResult))
        {
            return errorResult!;
        }

        var result = await _collectionNeedService.GetCollectionNeedsAsync(query, actorUserId, actorRole, cancellationToken);
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
