using SmartWaste.Application.Collection.DTOs.Responses;
using SmartWaste.Application.Collection.Queries;
using SmartWaste.Application.Common.Models;

namespace SmartWaste.Application.Collection.Interfaces;

/// <summary>
/// Application service interface for derived collection needs read queue (Component 2).
/// Methods receive explicit actorUserId and actorRole sourced from authenticated JWT claims.
/// </summary>
public interface ICollectionNeedService
{
    /// <summary>
    /// Retrieves the unified derived read queue of outstanding collection needs.
    /// Accessible by WasteOfficer and MunicipalManager.
    /// </summary>
    Task<PagedResult<CollectionNeedItemDto>> GetCollectionNeedsAsync(
        CollectionNeedListQuery query,
        Guid actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a bounded, sanitized, read-only collection-needs projection for the internal AI service.
    /// Internal-service authentication is enforced by the API controller policy.
    /// </summary>
    Task<PagedResult<CollectionNeedToolItemDto>> GetCollectionNeedsForAiAsync(
        GetCollectionNeedsForAiQuery query,
        CancellationToken cancellationToken = default);
}
