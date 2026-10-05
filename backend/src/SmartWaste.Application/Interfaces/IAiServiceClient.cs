using SmartWaste.Application.DTOs.Ai;

namespace SmartWaste.Application.Interfaces;

/// <summary>
/// Client abstraction for communicating with the internal FastAPI AI microservice.
/// </summary>
public interface IAiServiceClient
{
    /// <summary>
    /// Checks the health status of the internal FastAPI AI service.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A strongly-typed health status DTO.</returns>
    Task<AiHealthDto> CheckHealthAsync(CancellationToken cancellationToken = default);
}
