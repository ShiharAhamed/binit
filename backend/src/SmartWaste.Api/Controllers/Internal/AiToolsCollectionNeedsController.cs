using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartWaste.Api.Authentication.InternalService;
using SmartWaste.Application.Collection.DTOs.Responses;
using SmartWaste.Application.Collection.Interfaces;
using SmartWaste.Application.Collection.Queries;
using SmartWaste.Application.Collection.Validation;
using SmartWaste.Application.Common.Models;

namespace SmartWaste.Api.Controllers.Internal;

/// <summary>
/// Allow-listed, read-only collection-needs tool for the private AI service.
/// Exposes only the minimal operational projection required for advisory collection planning.
/// </summary>
[ApiController]
[Route("api/v1/internal/ai-tools/collection-needs")]
[Authorize(Policy = InternalServiceDefaults.PolicyName)]
public class AiToolsCollectionNeedsController : ControllerBase
{
    private readonly ICollectionNeedService _collectionNeedService;

    public AiToolsCollectionNeedsController(ICollectionNeedService collectionNeedService)
    {
        _collectionNeedService = collectionNeedService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<CollectionNeedToolItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetCollectionNeeds(
        [FromQuery] GetCollectionNeedsForAiQuery query,
        CancellationToken cancellationToken)
    {
        var validationResult = await new GetCollectionNeedsForAiQueryValidator()
            .ValidateAsync(query, cancellationToken);
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

        var result = await _collectionNeedService.GetCollectionNeedsForAiAsync(query, cancellationToken);
        return Ok(result);
    }
}
