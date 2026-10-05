using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartWaste.Application.Common.Models;
using SmartWaste.Application.Reporting.DTOs.Requests;
using SmartWaste.Application.Reporting.DTOs.Responses;
using SmartWaste.Application.Reporting.Interfaces;
using SmartWaste.Application.Reporting.Queries;
using SmartWaste.Domain.Common;

namespace SmartWaste.Api.Controllers.Reporting;

/// <summary>
/// Component 1 — Waste Reporting & Citizen Management API.
/// Provides authenticated endpoints for submitting, querying, updating, cancelling,
/// and reviewing municipal solid waste incident reports.
/// </summary>
[ApiController]
[Route("api/v1/waste-reports")]
[Authorize]
public class WasteReportsController : ControllerBase
{
    private readonly IWasteReportService _wasteReportService;
    private readonly IWasteReportAttachmentService _attachmentService;

    public WasteReportsController(
        IWasteReportService wasteReportService,
        IWasteReportAttachmentService attachmentService)
    {
        _wasteReportService = wasteReportService;
        _attachmentService = attachmentService;
    }

    /// <summary>
    /// Submits a new citizen waste report. CitizenId is determined authoritatively from the caller token.
    /// </summary>
    /// <param name="request">Report details including description, coordinates, waste type, and optional address.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Created waste report details.</returns>
    [HttpPost]
    [Authorize(Roles = AppRoles.Citizen)]
    [ProducesResponseType(typeof(WasteReportDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(
        [FromBody] CreateWasteReportRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole, out var errorResult))
        {
            return errorResult!;
        }

        var report = await _wasteReportService.CreateAsync(request, actorUserId, actorRole, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = report.Id }, report);
    }

    /// <summary>
    /// Lists waste reports with pagination, filtering, search, and sorting. Scope is role-enforced.
    /// </summary>
    /// <param name="query">Pagination, filtering, sorting, and search query parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paged summary list of waste reports.</returns>
    [HttpGet]
    [Authorize(Roles = $"{AppRoles.Citizen},{AppRoles.WasteOfficer},{AppRoles.MunicipalManager}")]
    [ProducesResponseType(typeof(PagedResult<WasteReportSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetList(
        [FromQuery] WasteReportListQuery query,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole, out var errorResult))
        {
            return errorResult!;
        }

        var result = await _wasteReportService.GetListAsync(query, actorUserId, actorRole, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves full details of a specific waste report by ID.
    /// </summary>
    /// <param name="id">The report unique identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Full report detail with verification and attachment metadata.</returns>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = $"{AppRoles.Citizen},{AppRoles.WasteOfficer},{AppRoles.MunicipalManager}")]
    [ProducesResponseType(typeof(WasteReportDetailDto), StatusCodes.Status200OK)]
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

        var report = await _wasteReportService.GetByIdAsync(id, actorUserId, actorRole, cancellationToken);
        return Ok(report);
    }

    /// <summary>
    /// Updates citizen-submitted evidence while the report remains in Submitted status.
    /// </summary>
    /// <param name="id">The report unique identifier.</param>
    /// <param name="request">Partial update payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Updated waste report details.</returns>
    [HttpPatch("{id:guid}")]
    [Authorize(Roles = AppRoles.Citizen)]
    [ProducesResponseType(typeof(WasteReportDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        [FromRoute] Guid id,
        [FromBody] UpdateWasteReportRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole, out var errorResult))
        {
            return errorResult!;
        }

        var report = await _wasteReportService.UpdateAsync(id, request, actorUserId, actorRole, cancellationToken);
        return Ok(report);
    }

    /// <summary>
    /// Business-cancels a submitted report by the owning citizen. Not a physical database delete.
    /// </summary>
    /// <param name="id">The report unique identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Confirmation message and new status.</returns>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = AppRoles.Citizen)]
    [ProducesResponseType(typeof(CancelWasteReportResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole, out var errorResult))
        {
            return errorResult!;
        }

        var report = await _wasteReportService.CancelAsync(id, actorUserId, actorRole, cancellationToken);
        return Ok(new CancelWasteReportResponse
        {
            Message = "Waste report cancelled successfully.",
            Status = report.Status.ToString()
        });
    }

    /// <summary>
    /// Authorized staff initiates formal review of a Submitted report (Submitted -> UnderReview).
    /// </summary>
    /// <param name="id">The report unique identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Updated waste report details.</returns>
    [HttpPost("{id:guid}/start-review")]
    [Authorize(Roles = AppRoles.AgentWorkflowAuthorityRoles)]
    [ProducesResponseType(typeof(WasteReportDetailDto), StatusCodes.Status200OK)]
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

        var report = await _wasteReportService.StartReviewAsync(id, actorUserId, actorRole, cancellationToken);
        return Ok(report);
    }

    /// <summary>
    /// Authorized staff verifies an inspected report with an explicit priority (UnderReview -> Verified).
    /// </summary>
    /// <param name="id">The report unique identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Updated waste report details.</returns>
    [HttpPost("{id:guid}/verify")]
    [Authorize(Roles = AppRoles.AgentWorkflowAuthorityRoles)]
    [ProducesResponseType(typeof(WasteReportDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Verify(
        [FromRoute] Guid id,
        [FromBody] VerifyWasteReportRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole, out var errorResult))
        {
            return errorResult!;
        }

        var report = await _wasteReportService.VerifyAsync(id, request, actorUserId, actorRole, cancellationToken);
        return Ok(report);
    }

    /// <summary>
    /// Authorized staff rejects an invalid or duplicate report (UnderReview -> Rejected).
    /// </summary>
    /// <param name="id">The report unique identifier.</param>
    /// <param name="request">Rejection reason payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Updated waste report details.</returns>
    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = AppRoles.AgentWorkflowAuthorityRoles)]
    [ProducesResponseType(typeof(WasteReportDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reject(
        [FromRoute] Guid id,
        [FromBody] RejectWasteReportRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole, out var errorResult))
        {
            return errorResult!;
        }

        var report = await _wasteReportService.RejectAsync(id, request, actorUserId, actorRole, cancellationToken);
        return Ok(report);
    }

    /// <summary>
    /// Retrieves the chronological status transition audit trail for a waste report.
    /// </summary>
    /// <param name="id">The report unique identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Chronological list of status transition history records.</returns>
    [HttpGet("{id:guid}/history")]
    [Authorize(Roles = $"{AppRoles.Citizen},{AppRoles.WasteOfficer},{AppRoles.MunicipalManager}")]
    [ProducesResponseType(typeof(IReadOnlyList<WasteReportStatusHistoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetHistory(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole, out var errorResult))
        {
            return errorResult!;
        }

        var history = await _wasteReportService.GetHistoryAsync(id, actorUserId, actorRole, cancellationToken);
        return Ok(history);
    }

    /// <summary>
    /// Uploads a photographic evidence attachment to a citizen's own Submitted waste report.
    /// </summary>
    /// <param name="id">The report unique identifier.</param>
    /// <param name="file">The uploaded image file (multipart/form-data field 'file').</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Created ReportAttachmentDto with signed read URL.</returns>
    [HttpPost("{id:guid}/attachments")]
    [Authorize(Roles = AppRoles.Citizen)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 10 * 1024 * 1024)]
    [ProducesResponseType(typeof(ReportAttachmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UploadAttachment(
        [FromRoute] Guid id,
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole, out var errorResult))
        {
            return errorResult!;
        }

        if (file is null || file.Length == 0)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid File",
                Detail = "A non-empty image file must be provided in form field 'file'.",
                Instance = HttpContext.Request.Path
            });
        }

        await using var stream = file.OpenReadStream();
        var attachment = await _attachmentService.UploadAsync(
            id,
            stream,
            file.ContentType,
            file.FileName,
            file.Length,
            actorUserId,
            actorRole,
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, attachment);
    }

    /// <summary>
    /// Removes a photographic evidence attachment from a citizen's own Submitted waste report.
    /// </summary>
    /// <param name="id">The report unique identifier.</param>
    /// <param name="attachmentId">The attachment unique identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Confirmation message indicating attachment was removed.</returns>
    [HttpDelete("{id:guid}/attachments/{attachmentId:guid}")]
    [Authorize(Roles = AppRoles.Citizen)]
    [ProducesResponseType(typeof(DeleteAttachmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteAttachment(
        [FromRoute] Guid id,
        [FromRoute] Guid attachmentId,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actorUserId, out var actorRole, out var errorResult))
        {
            return errorResult!;
        }

        await _attachmentService.DeleteAsync(id, attachmentId, actorUserId, actorRole, cancellationToken);
        return Ok(new DeleteAttachmentResponse { Message = "Attachment removed successfully." });
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
