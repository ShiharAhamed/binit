using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartWaste.Api.Authentication.InternalService;
using SmartWaste.Application.Common.Models;
using SmartWaste.Application.Reporting.DTOs.Responses;
using SmartWaste.Application.Reporting.Interfaces;
using SmartWaste.Application.Reporting.Queries;
using SmartWaste.Application.Reporting.Validation;

namespace SmartWaste.Api.Controllers.Internal;

/// <summary>
/// Internal service-to-service API controller providing allow-listed, read-only tools
/// for the private Python AI microservice.
/// Accessible exclusively by the internal AI service via the "InternalServicePolicy" authorization policy.
/// </summary>
[ApiController]
[Route("api/v1/internal/ai-tools/waste-reports")]
[Authorize(Policy = InternalServiceDefaults.PolicyName)]
public class AiToolsWasteReportsController : ControllerBase
{
    private readonly IWasteReportService _wasteReportService;

    public AiToolsWasteReportsController(IWasteReportService wasteReportService)
    {
        _wasteReportService = wasteReportService;
    }

    /// <summary>
    /// Retrieves a paginated, read-only, minimal set of waste reports that have already been explicitly verified.
    /// Exposes only allow-listed fields necessary for AI waste analysis and planning.
    /// Excludes citizen PII, officer IDs, storage keys, signed URLs, and status history.
    /// </summary>
    [HttpGet("verified")]
    [ProducesResponseType(typeof(PagedResult<VerifiedWasteReportToolItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetVerifiedReports(
        [FromQuery] GetVerifiedWasteReportsForAiQuery query,
        CancellationToken cancellationToken)
    {
        var validator = new GetVerifiedWasteReportsForAiQueryValidator();
        var validationResult = await validator.ValidateAsync(query, cancellationToken);
        if (!validationResult.IsValid)
        {
            return BadRequest(new ValidationProblemDetails(validationResult.ToDictionary())
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid Query Parameters",
                Detail = "One or more query parameters failed validation.",
                Instance = HttpContext.Request.Path
            });
        }

        var result = await _wasteReportService.GetVerifiedReportsForAiAsync(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Reads exactly one Submitted or UnderReview report by authoritative UUID for advisory analysis.
    /// The existing verified-report list remains the manual planning tool.
    /// </summary>
    [HttpGet("for-verification/{reportId:guid}")]
    [ProducesResponseType(typeof(WasteReportForVerificationToolItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GetReportForVerification(
        [FromRoute] Guid reportId,
        CancellationToken cancellationToken)
    {
        var report = await _wasteReportService.GetReportForVerificationAiAsync(reportId, cancellationToken);
        return Ok(report);
    }
}
