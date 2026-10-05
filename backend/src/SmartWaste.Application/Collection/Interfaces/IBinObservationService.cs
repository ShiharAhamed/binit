using SmartWaste.Application.Collection.DTOs.Requests;
using SmartWaste.Application.Collection.DTOs.Responses;
using SmartWaste.Application.Collection.Queries;
using SmartWaste.Application.Common.Models;

namespace SmartWaste.Application.Collection.Interfaces;

/// <summary>
/// Application service interface for manual roadside bin observations (Component 2).
/// Methods receive explicit actorUserId and actorRole sourced from authenticated JWT claims.
/// </summary>
public interface IBinObservationService
{
    /// <summary>
    /// Records an append-only manual field observation for a registered bin.
    /// Accessible by WasteOfficer (now) and Driver (in future C3).
    /// </summary>
    Task<BinObservationDto> RecordObservationAsync(
        Guid binId,
        RecordBinObservationRequest request,
        Guid actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves chronological observation history for a specific bin.
    /// Accessible by WasteOfficer and MunicipalManager.
    /// </summary>
    Task<PagedResult<BinObservationDto>> GetObservationHistoryAsync(
        Guid binId,
        ObservationListQuery query,
        Guid actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default);
}
