using System.Net;
using Microsoft.AspNetCore.Mvc;
using SmartWaste.Application.Common.Exceptions;
using SmartWaste.Application.DTOs.Ai;
using SmartWaste.Application.Interfaces;

namespace SmartWaste.Api.Controllers;

/// <summary>
/// Development-only diagnostic controller for verifying internal communication with the FastAPI AI service.
/// Active only when running in the Development environment.
/// </summary>
[ApiController]
[Route("api/v1/dev/ai-health")]
public class DevAiController : ControllerBase
{
    private readonly IAiServiceClient _aiServiceClient;
    private readonly IWebHostEnvironment _environment;

    public DevAiController(IAiServiceClient aiServiceClient, IWebHostEnvironment environment)
    {
        _aiServiceClient = aiServiceClient;
        _environment = environment;
    }

    /// <summary>
    /// Checks the health of the internal FastAPI AI service.
    /// Only accessible in Development environment.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(AiHealthDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAiHealth(CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        try
        {
            var health = await _aiServiceClient.CheckHealthAsync(cancellationToken);
            return Ok(health);
        }
        catch (AiServiceUnavailableException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Status = (int)HttpStatusCode.ServiceUnavailable,
                Title = "AI Service Unavailable",
                Detail = ex.Message,
                Instance = HttpContext.Request.Path
            });
        }
    }
}
