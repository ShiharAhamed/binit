using SmartWaste.Application.Common.Models;
using SmartWaste.Application.Complaints.DTOs.Requests;
using SmartWaste.Application.Complaints.DTOs.Responses;
using SmartWaste.Application.Complaints.Queries;

namespace SmartWaste.Application.Complaints.Interfaces;

/// <summary>
/// Application service interface for Component 4 — Citizen Complaints Management.
/// All methods receive explicit actorUserId and actorRole sourced from authenticated JWT claims at the controller layer.
/// </summary>
public interface IComplaintService
{
    /// <summary>
    /// Creates a new service complaint for the authenticated Citizen.
    /// CitizenId is always sourced from actorUserId; never from the request payload.
    /// </summary>
    Task<ComplaintDetailDto> CreateAsync(
        CreateComplaintRequest request,
        Guid actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a paginated, filtered, and sorted list of complaints with role-scoped visibility.
    /// Citizen: own complaints only. Staff (WasteOfficer/MunicipalManager): all complaints.
    /// </summary>
    Task<PagedResult<ComplaintSummaryDto>> GetListAsync(
        ComplaintListQuery query,
        Guid actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the full detail of a specific complaint with role-scoped access control.
    /// Citizen: own complaint only. Staff (WasteOfficer/MunicipalManager): any complaint.
    /// </summary>
    Task<ComplaintDetailDto> GetByIdAsync(
        Guid complaintId,
        Guid actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Staff initiates review: Submitted -> InReview.
    /// </summary>
    Task<ComplaintDetailDto> StartReviewAsync(
        Guid complaintId,
        Guid actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Staff resolves a complaint: InReview -> Resolved.
    /// Sets ResolvedByUserId, ResolvedAt, and ResolutionNote.
    /// </summary>
    Task<ComplaintDetailDto> ResolveAsync(
        Guid complaintId,
        ResolveComplaintRequest request,
        Guid actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default);
}
