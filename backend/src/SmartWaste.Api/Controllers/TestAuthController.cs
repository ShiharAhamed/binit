using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartWaste.Domain.Common;

namespace SmartWaste.Api.Controllers;

/// <summary>
/// Temporary test endpoints to verify role-based and token-based authorization.
/// </summary>
[ApiController]
[Route("api/v1/test-auth")]
public class TestAuthController : ControllerBase
{
    /// <summary>
    /// Accessible by any authenticated user regardless of role.
    /// </summary>
    [HttpGet("authenticated")]
    [Authorize]
    public IActionResult AuthenticatedOnly()
    {
        var userName = User.FindFirstValue(ClaimTypes.Name) ?? User.Identity?.Name;
        var role = User.FindFirstValue(ClaimTypes.Role);

        return Ok(new
        {
            message = "Access granted to authenticated user.",
            user = userName,
            role
        });
    }

    /// <summary>
    /// Accessible ONLY by users with the MunicipalManager role.
    /// </summary>
    [HttpGet("manager")]
    [Authorize(Roles = AppRoles.MunicipalManager)]
    public IActionResult ManagerOnly()
    {
        var userName = User.FindFirstValue(ClaimTypes.Name) ?? User.Identity?.Name;

        return Ok(new
        {
            message = "Access granted to MunicipalManager.",
            user = userName,
            role = AppRoles.MunicipalManager
        });
    }
}
