using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartWaste.Application.Common.Models;
using SmartWaste.Application.DTOs.Users;
using SmartWaste.Application.Interfaces;
using SmartWaste.Domain.Common;

namespace SmartWaste.Api.Controllers;

/// <summary>
/// Administrative endpoints for managing internal staff accounts.
/// Access is strictly restricted to MunicipalManager.
/// </summary>
[ApiController]
[Route("api/v1/users")]
[Authorize(Roles = AppRoles.MunicipalManager)]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    /// <summary>
    /// Retrieves a paginated list of internal staff accounts with optional search and role filtering.
    /// </summary>
    /// <param name="query">Pagination and filter parameters</param>
    /// <returns>Paged user management list</returns>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<UserManagementDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetUsers([FromQuery] UserListQuery query)
    {
        var result = await _userService.GetUsersAsync(query);
        return Ok(result);
    }

    /// <summary>
    /// Creates a new internal staff user (Driver, WasteOfficer, MunicipalManager) with a secure temporary password.
    /// Citizen accounts cannot be created through this endpoint.
    /// </summary>
    /// <param name="request">User creation details</param>
    /// <returns>Created user details and one-time temporary password</returns>
    [HttpPost]
    [ProducesResponseType(typeof(CreateUserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
    {
        var result = await _userService.CreateUserAsync(request);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>
    /// Activates or deactivates an internal staff account.
    /// A manager cannot deactivate their own account.
    /// </summary>
    /// <param name="id">Target user ID</param>
    /// <param name="request">New active status</param>
    /// <returns>Updated user metadata</returns>
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(typeof(UserManagementDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateUserStatus([FromRoute] Guid id, [FromBody] UpdateUserStatusRequest request)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier)
                          ?? User.FindFirstValue("sub");

        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var currentUserId))
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Detail = "Current user identity could not be determined."
            });
        }

        var result = await _userService.UpdateUserStatusAsync(id, request, currentUserId);
        return Ok(result);
    }
}
