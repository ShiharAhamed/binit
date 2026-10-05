using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartWaste.Application.Common.Models;
using SmartWaste.Application.Fleet.DTOs.Requests;
using SmartWaste.Application.Fleet.DTOs.Responses;
using SmartWaste.Application.Fleet.Interfaces;
using SmartWaste.Application.Fleet.Queries;
using SmartWaste.Domain.Common;

namespace SmartWaste.Api.Controllers.Fleet;

[ApiController]
[Route("api/v1/vehicles")]
[Authorize]
public class VehiclesController : ControllerBase
{
    private readonly IVehicleService _service;
    public VehiclesController(IVehicleService service) => _service = service;

    [HttpGet]
    [Authorize(Roles = AppRoles.MunicipalManager + "," + AppRoles.WasteOfficer)]
    [ProducesResponseType(typeof(PagedResult<VehicleSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetList([FromQuery] VehicleListQuery query, CancellationToken cancellationToken)
    {
        var actor = GetActor(); if (actor is null) return Unauthorized();
        return Ok(await _service.GetListAsync(query, actor.Value.Id, actor.Value.Role, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = AppRoles.MunicipalManager + "," + AppRoles.WasteOfficer)]
    [ProducesResponseType(typeof(VehicleDetailDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var actor = GetActor(); if (actor is null) return Unauthorized();
        return Ok(await _service.GetByIdAsync(id, actor.Value.Id, actor.Value.Role, cancellationToken));
    }

    [HttpPost]
    [Authorize(Roles = AppRoles.MunicipalManager)]
    [ProducesResponseType(typeof(VehicleDetailDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateVehicleRequest request, CancellationToken cancellationToken)
    {
        var actor = GetActor(); if (actor is null) return Unauthorized();
        var result = await _service.CreateAsync(request, actor.Value.Id, actor.Value.Role, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPatch("{id:guid}")]
    [Authorize(Roles = AppRoles.MunicipalManager)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateVehicleRequest request, CancellationToken cancellationToken)
    {
        var actor = GetActor(); if (actor is null) return Unauthorized();
        return Ok(await _service.UpdateAsync(id, request, actor.Value.Id, actor.Value.Role, cancellationToken));
    }

    [HttpPatch("{id:guid}/operational-status")]
    [Authorize(Roles = AppRoles.MunicipalManager)]
    public async Task<IActionResult> UpdateOperationalStatus(Guid id, [FromBody] UpdateVehicleOperationalStatusRequest request, CancellationToken cancellationToken)
    {
        var actor = GetActor(); if (actor is null) return Unauthorized();
        return Ok(await _service.UpdateOperationalStatusAsync(id, request, actor.Value.Id, actor.Value.Role, cancellationToken));
    }

    private (Guid Id, string Role)? GetActor()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        var role = User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role");
        return Guid.TryParse(id, out var userId) && !string.IsNullOrWhiteSpace(role) ? (userId, role) : null;
    }
}
