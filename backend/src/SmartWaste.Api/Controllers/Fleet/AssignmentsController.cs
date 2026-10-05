using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartWaste.Application.Collection.Interfaces;
using SmartWaste.Application.Collection.DTOs.Requests;
using SmartWaste.Application.Collection.Queries;
using SmartWaste.Domain.Common;

namespace SmartWaste.Api.Controllers.Collection;
[ApiController, Route("api/v1/assignments"), Authorize]
public sealed class AssignmentsController(IAssignmentReadService service, ICollectionAssignmentService assignmentService) : ControllerBase
{
 [HttpPost, Authorize(Roles=AppRoles.WasteOfficer)] public async Task<IActionResult> Create([FromBody] CreateCollectionAssignmentRequest request,CancellationToken ct){var a=Actor();if(a is null)return Unauthorized();var result=await assignmentService.CreateAsync(request,a.Value.Id,a.Value.Role,ct);return CreatedAtAction(nameof(Detail),new{id=result.Id},result);}
 [HttpPatch("{id:guid}/route/stops"), Authorize(Roles=AppRoles.WasteOfficer)] public async Task<IActionResult> Reorder(Guid id,[FromBody] ReorderRouteStopsRequest request,CancellationToken ct){var a=Actor();if(a is null)return Unauthorized();return Ok(await assignmentService.ReorderStopsAsync(id,request,a.Value.Id,a.Value.Role,ct));}
 [HttpPost("{id:guid}/cancel"), Authorize(Roles=AppRoles.WasteOfficer+","+AppRoles.MunicipalManager)] public async Task<IActionResult> Cancel(Guid id,[FromBody] CancelCollectionAssignmentRequest request,CancellationToken ct){var a=Actor();if(a is null)return Unauthorized();return Ok(await assignmentService.CancelAsync(id,request,a.Value.Id,a.Value.Role,ct));}
 [HttpPost("{id:guid}/start"), Authorize(Roles=AppRoles.Driver)] public async Task<IActionResult> Start(Guid id,CancellationToken ct){var a=Actor();if(a is null)return Unauthorized();return Ok(await assignmentService.StartAsync(id,a.Value.Id,a.Value.Role,ct));}
 [HttpPost("{id:guid}/stops/{stopId:guid}/complete"), Authorize(Roles=AppRoles.Driver)] public async Task<IActionResult> CompleteStop(Guid id,Guid stopId,CancellationToken ct){var a=Actor();if(a is null)return Unauthorized();return Ok(await assignmentService.CompleteStopAsync(id,stopId,a.Value.Id,a.Value.Role,ct));}
 [HttpPost("{id:guid}/stops/{stopId:guid}/fail"), Authorize(Roles=AppRoles.Driver)] public async Task<IActionResult> FailStop(Guid id,Guid stopId,[FromBody] FailRouteStopRequest request,CancellationToken ct){var a=Actor();if(a is null)return Unauthorized();return Ok(await assignmentService.FailStopAsync(id,stopId,request,a.Value.Id,a.Value.Role,ct));}
 [HttpPost("{id:guid}/finalize"), Authorize(Roles=AppRoles.Driver)] public async Task<IActionResult> Finalize(Guid id,CancellationToken ct){var a=Actor();if(a is null)return Unauthorized();return Ok(await assignmentService.FinalizeAsync(id,a.Value.Id,a.Value.Role,ct));}
 [HttpPost("{id:guid}/stops/{stopId:guid}/bin-observation"), Authorize(Roles=AppRoles.Driver)] public async Task<IActionResult> RecordBinObservation(Guid id,Guid stopId,[FromBody] RecordBinObservationRequest request,CancellationToken ct){var a=Actor();if(a is null)return Unauthorized();return StatusCode(StatusCodes.Status201Created,await assignmentService.RecordDriverBinObservationAsync(id,stopId,request,a.Value.Id,a.Value.Role,ct));}
 [HttpGet, Authorize(Roles=AppRoles.WasteOfficer+","+AppRoles.MunicipalManager)] public async Task<IActionResult> List([FromQuery] AssignmentListQuery q,CancellationToken ct){var a=Actor();if(a is null)return Unauthorized();return Ok(await service.GetStaffListAsync(q,a.Value.Id,a.Value.Role,ct));}
 [HttpGet("mine"), Authorize(Roles=AppRoles.Driver)] public async Task<IActionResult> Mine([FromQuery] AssignmentListQuery q,CancellationToken ct){var a=Actor();if(a is null)return Unauthorized();return Ok(await service.GetMineAsync(q,a.Value.Id,a.Value.Role,ct));}
 [HttpGet("{id:guid}"), Authorize(Roles=AppRoles.WasteOfficer+","+AppRoles.MunicipalManager+","+AppRoles.Driver)] public async Task<IActionResult> Detail(Guid id,CancellationToken ct){var a=Actor();if(a is null)return Unauthorized();return Ok(await service.GetDetailAsync(id,a.Value.Id,a.Value.Role,ct));}
 [HttpGet("{id:guid}/history"), Authorize(Roles=AppRoles.WasteOfficer+","+AppRoles.MunicipalManager+","+AppRoles.Driver)] public async Task<IActionResult> History(Guid id,CancellationToken ct){var a=Actor();if(a is null)return Unauthorized();return Ok((await service.GetDetailAsync(id,a.Value.Id,a.Value.Role,ct)).History);}
 private (Guid Id,string Role)? Actor(){var id=User.FindFirstValue(ClaimTypes.NameIdentifier)??User.FindFirstValue("sub");var role=User.FindFirstValue(ClaimTypes.Role)??User.FindFirstValue("role");return Guid.TryParse(id,out var value)&&role is not null?(value,role):null;}
}
