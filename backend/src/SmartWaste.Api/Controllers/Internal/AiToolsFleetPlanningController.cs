using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartWaste.Api.Authentication.InternalService;
using SmartWaste.Application.Fleet.DTOs.Requests;
using SmartWaste.Application.Fleet.DTOs.Responses;
using SmartWaste.Application.Fleet.Interfaces;
using SmartWaste.Application.Fleet.Queries;
using SmartWaste.Application.Fleet.Validation;

namespace SmartWaste.Api.Controllers.Internal;

/// <summary>
/// Allow-listed, read-only fleet planning and compatibility tools for the private internal AI service.
/// Accessible exclusively by the internal AI service via the "InternalServicePolicy" authorization policy.
/// </summary>
[ApiController]
[Route("api/v1/internal/ai-tools")]
[Authorize(Policy = InternalServiceDefaults.PolicyName)]
public class AiToolsFleetPlanningController : ControllerBase
{
    private readonly IFleetPlanningAiService _service;

    public AiToolsFleetPlanningController(IFleetPlanningAiService service)
    {
        _service = service;
    }

    /// <summary>
    /// Retrieves a minimal, structured fleet planning snapshot:
    /// available scheduled tasks, available and unoccupied drivers, and available and unoccupied vehicles.
    /// </summary>
    [HttpGet("fleet-planning-context")]
    [ProducesResponseType(typeof(FleetPlanningContextDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetFleetPlanningContext(
        [FromQuery] GetFleetPlanningContextForAiQuery query,
        CancellationToken cancellationToken)
    {
        var validator = new GetFleetPlanningContextForAiQueryValidator();
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

        var result = await _service.GetPlanningContextAsync(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Executes deterministic task/vehicle waste compatibility checks.
    /// Returns Compatible, Unknown (requires officer acknowledgement), or Incompatible.
    /// </summary>
    [HttpPost("fleet-compatibility")]
    [ProducesResponseType(typeof(FleetCompatibilityResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CheckFleetCompatibility(
        [FromBody] CheckFleetCompatibilityRequest request,
        CancellationToken cancellationToken)
    {
        var validator = new CheckFleetCompatibilityRequestValidator();
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return BadRequest(new ValidationProblemDetails(validationResult.ToDictionary())
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid Request Parameters",
                Detail = "One or more request parameters failed validation.",
                Instance = HttpContext.Request.Path
            });
        }

        var result = await _service.CheckCompatibilityAsync(request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves fresh, minimal operational context for specific tasks, drivers, and vehicles referenced in an AI dispatch plan.
    /// Used exclusively by the C4 validation and operations agent.
    /// </summary>
    [HttpPost("operational-validation-context")]
    [ProducesResponseType(typeof(OperationalValidationContextDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetOperationalValidationContext(
        [FromBody] GetOperationalValidationContextRequest request,
        CancellationToken cancellationToken)
    {
        var validator = new GetOperationalValidationContextRequestValidator();
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return BadRequest(new ValidationProblemDetails(validationResult.ToDictionary())
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid Request Parameters",
                Detail = "One or more request parameters failed validation.",
                Instance = HttpContext.Request.Path
            });
        }

        var result = await _service.GetOperationalValidationContextAsync(request, cancellationToken);
        return Ok(result);
    }
}
