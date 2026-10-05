using SmartWaste.Application.Collection.DTOs.Requests;
using SmartWaste.Application.Collection.DTOs.Responses;
using SmartWaste.Application.Collection.Queries;
using SmartWaste.Application.Common.Models;

namespace SmartWaste.Application.Collection.Interfaces;

/// <summary>
/// Application service interface for municipal roadside waste bin management (Component 2).
/// Methods receive explicit actorUserId and actorRole sourced from authenticated JWT claims.
/// </summary>
public interface IWasteBinService
{
    /// <summary>
    /// Registers a new municipal public roadside bin.
    /// Restricted to WasteOfficer.
    /// </summary>
    Task<WasteBinDetailDto> CreateAsync(
        CreateWasteBinRequest request,
        Guid actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates operational metadata of an existing bin.
    /// Restricted to WasteOfficer.
    /// </summary>
    Task<WasteBinDetailDto> UpdateAsync(
        Guid id,
        UpdateWasteBinRequest request,
        Guid actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deactivates or retires an existing bin.
    /// Restricted to WasteOfficer.
    /// </summary>
    Task<WasteBinDetailDto> DeactivateAsync(
        Guid id,
        DeactivateWasteBinRequest request,
        Guid actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves full internal operational details of a specific bin.
    /// Accessible by WasteOfficer and MunicipalManager.
    /// </summary>
    Task<WasteBinDetailDto> GetInternalByIdAsync(
        Guid id,
        Guid actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a paginated list of bins with operational filters for staff.
    /// Accessible by WasteOfficer and MunicipalManager.
    /// </summary>
    Task<PagedResult<WasteBinSummaryDto>> GetInternalListAsync(
        WasteBinListQuery query,
        Guid actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves public citizen operational information for a specific roadside bin.
    /// Accessible by authenticated Citizen.
    /// </summary>
    Task<PublicWasteBinDetailDto> GetPublicByIdAsync(
        Guid id,
        Guid actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a paginated list of public roadside bins with computed public availability.
    /// Accessible by authenticated Citizen.
    /// </summary>
    Task<PagedResult<PublicWasteBinDto>> GetPublicListAsync(
        PublicWasteBinQuery query,
        Guid actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default);
}
