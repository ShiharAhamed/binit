using SmartWaste.Application.Common.Models;
using SmartWaste.Application.Operations.DTOs.Requests;
using SmartWaste.Application.Operations.DTOs.Responses;
using SmartWaste.Application.Operations.Queries;

namespace SmartWaste.Application.Operations.Interfaces;

/// <summary>
/// Application service interface for Component 4 — Driver Operational Issues Management.
/// All methods receive explicit actorUserId and actorRole sourced from authenticated JWT claims at the controller layer.
/// </summary>
public interface IOperationalIssueService
{
    /// <summary>
    /// Creates a new operational issue reported by the authenticated Driver.
    /// DriverId is always sourced from actorUserId; never from the request payload.
    /// </summary>
    Task<OperationalIssueDetailDto> CreateAsync(
        CreateOperationalIssueRequest request,
        Guid actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a paginated, filtered, and sorted list of operational issues with role-scoped visibility for staff.
    /// </summary>
    Task<PagedResult<OperationalIssueSummaryDto>> GetListAsync(
        OperationalIssueListQuery query,
        Guid actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a paginated list of operational issues reported specifically by the authenticated Driver.
    /// </summary>
    Task<PagedResult<OperationalIssueSummaryDto>> GetMineAsync(
        OperationalIssueListQuery query,
        Guid actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the full detail of a specific operational issue with role-scoped access control.
    /// Driver: own issues only. Staff (WasteOfficer/MunicipalManager): any issue.
    /// </summary>
    Task<OperationalIssueDetailDto> GetByIdAsync(
        Guid issueId,
        Guid actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Staff initiates review: Reported -> InReview.
    /// </summary>
    Task<OperationalIssueDetailDto> StartReviewAsync(
        Guid issueId,
        Guid actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Staff resolves an operational issue: InReview -> Resolved.
    /// Sets ResolvedByUserId, ResolvedAt, and ResolutionNote.
    /// </summary>
    Task<OperationalIssueDetailDto> ResolveAsync(
        Guid issueId,
        ResolveOperationalIssueRequest request,
        Guid actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default);
}
