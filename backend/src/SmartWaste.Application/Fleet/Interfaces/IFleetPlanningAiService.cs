using SmartWaste.Application.Fleet.DTOs.Requests;
using SmartWaste.Application.Fleet.DTOs.Responses;
using SmartWaste.Application.Fleet.Queries;

namespace SmartWaste.Application.Fleet.Interfaces;

/// <summary>
/// Application service providing AI-safe fleet planning context and compatibility checks.
/// </summary>
public interface IFleetPlanningAiService
{
    Task<FleetPlanningContextDto> GetPlanningContextAsync(
        GetFleetPlanningContextForAiQuery query,
        CancellationToken cancellationToken = default);

    Task<FleetCompatibilityResultDto> CheckCompatibilityAsync(
        CheckFleetCompatibilityRequest request,
        CancellationToken cancellationToken = default);

    Task<OperationalValidationContextDto> GetOperationalValidationContextAsync(
        GetOperationalValidationContextRequest request,
        CancellationToken cancellationToken = default);
}
