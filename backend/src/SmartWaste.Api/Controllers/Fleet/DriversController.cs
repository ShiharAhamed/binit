using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartWaste.Application.Common.Exceptions;
using SmartWaste.Application.Common.Models;
using SmartWaste.Application.Fleet.DTOs.Requests;
using SmartWaste.Application.Fleet.DTOs.Responses;
using SmartWaste.Application.Fleet.Interfaces;
using SmartWaste.Application.Fleet.Queries;
using SmartWaste.Domain.Common;

namespace SmartWaste.Api.Controllers.Fleet;

[ApiController]
[Route("api/v1/drivers")]
[Authorize]
public class DriversController : ControllerBase
{
    private readonly IDriverProfileService _service;
    public DriversController(IDriverProfileService service) => _service = service;

    [HttpGet]
    [Authorize(Roles = AppRoles.MunicipalManager + "," + AppRoles.WasteOfficer)]
    [ProducesResponseType(typeof(PagedResult<DriverSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetList([FromQuery] DriverListQuery query, CancellationToken cancellationToken)
    {
        var actor = GetActor(); if (actor is null) return Unauthorized();
        return Ok(await _service.GetListAsync(query, actor.Value.Id, actor.Value.Role, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = AppRoles.MunicipalManager + "," + AppRoles.Driver)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var actor = GetActor(); if (actor is null) return Unauthorized();
        if (actor.Value.Role == AppRoles.Driver)
        {
            if (actor.Value.Id != id) throw new ForbiddenException("Drivers can view only their own profile.");
            return Ok(await _service.GetSelfAsync(actor.Value.Id, actor.Value.Role, cancellationToken));
        }
        return Ok(await _service.GetAdministrativeByIdAsync(id, actor.Value.Id, actor.Value.Role, cancellationToken));
    }

    [HttpPatch("me/availability")]
    [Authorize(Roles = AppRoles.Driver)]
    [ProducesResponseType(typeof(DriverSelfDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateMyAvailability([FromBody] UpdateDriverAvailabilityRequest request, CancellationToken cancellationToken)
    {
        var actor = GetActor(); if (actor is null) return Unauthorized();
        return Ok(await _service.UpdateMyAvailabilityAsync(request, actor.Value.Id, actor.Value.Role, cancellationToken));
    }

    private (Guid Id, string Role)? GetActor()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        var role = User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role");
        return Guid.TryParse(id, out var userId) && !string.IsNullOrWhiteSpace(role) ? (userId, role) : null;
    }
}
