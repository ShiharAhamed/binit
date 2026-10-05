using Microsoft.AspNetCore.Mvc;

namespace SmartWaste.Api.Controllers;

/// <summary>
/// Simple health-check endpoint — returns HTTP 200 with { "status": "healthy" }.
/// Used to verify the API is running correctly.
/// </summary>
[ApiController]
[Route("[controller]")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new { status = "healthy" });
    }
}
