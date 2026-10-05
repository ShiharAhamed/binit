using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartWaste.Application.Dashboard.DTOs;
using SmartWaste.Application.Dashboard.Interfaces;
using SmartWaste.Domain.Common;

namespace SmartWaste.Api.Controllers.Dashboard;

[ApiController]
[Route("api/v1/dashboard/waste-officer")]
[Authorize(Roles = AppRoles.WasteOfficer)]
public sealed class WasteOfficerDashboardController(IWasteOfficerDashboardService dashboardService) : ControllerBase
{
    [HttpGet("overview")]
    [ProducesResponseType(typeof(WasteOfficerDashboardOverviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<WasteOfficerDashboardOverviewDto>> GetOverview(CancellationToken cancellationToken)
        => Ok(await dashboardService.GetOverviewAsync(cancellationToken));

    [HttpGet("needs-attention")]
    [ProducesResponseType(typeof(IReadOnlyList<WasteOfficerNeedsAttentionItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<WasteOfficerNeedsAttentionItemDto>>> GetNeedsAttention(CancellationToken cancellationToken)
        => Ok(await dashboardService.GetNeedsAttentionAsync(cancellationToken));
}
