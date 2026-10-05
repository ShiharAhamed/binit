using SmartWaste.Application.Common.Models;
using SmartWaste.Application.Reporting.DTOs.Requests;
using SmartWaste.Application.Reporting.DTOs.Responses;
using SmartWaste.Application.Reporting.Queries;

namespace SmartWaste.Application.Reporting.Interfaces;

/// <summary>
/// Application service interface for Component 1 — Waste Reporting &amp; Citizen Management.
/// All methods receive explicit actorUserId and actorRole sourced from authenticated JWT claims
/// at the controller layer. No HttpContext dependency exists here.
/// </summary>
public interface IWasteReportService
{
    /// <summary>
    /// Creates a new waste report for the authenticated Citizen.
    /// CitizenId is always sourced from actorUserId; never from the request payload.
    /// Initial status history (null → Submitted) is persisted atomically in one SaveChanges.
    /// </summary>
    /// <exception cref="SmartWaste.Application.Common.Exceptions.ForbiddenException">Actor is not Citizen.</exception>
    Task<WasteReportDetailDto> CreateAsync(
        CreateWasteReportRequest request,
        Guid actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the full detail of a specific waste report with role-scoped access control.
    /// Citizen: own reports only. WasteOfficer/MunicipalManager: any report. Driver: forbidden.
    /// </summary>
    /// <exception cref="SmartWaste.Application.Common.Exceptions.NotFoundException">Report not found.</exception>
    /// <exception cref="SmartWaste.Application.Common.Exceptions.ForbiddenException">Actor lacks access.</exception>
    Task<WasteReportDetailDto> GetByIdAsync(
        Guid reportId,
        Guid actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a paginated, filtered, and sorted list of waste reports with role-scoped visibility.
    /// Citizen: own reports only. WasteOfficer/MunicipalManager: all reports. Driver: forbidden.
    /// </summary>
    /// <exception cref="SmartWaste.Application.Common.Exceptions.ForbiddenException">Driver access attempt.</exception>
    Task<PagedResult<WasteReportSummaryDto>> GetListAsync(
        WasteReportListQuery query,
        Guid actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies a partial PATCH update to a Citizen's own Submitted report.
    /// All non-null fields in the request are applied. AddressText="" clears to null.
    /// Ordinary evidence edits do NOT create status history entries.
    /// </summary>
    /// <exception cref="SmartWaste.Application.Common.Exceptions.NotFoundException">Report not found.</exception>
    /// <exception cref="SmartWaste.Application.Common.Exceptions.ForbiddenException">Not the owning Citizen.</exception>
    /// <exception cref="SmartWaste.Application.Common.Exceptions.BusinessRuleConflictException">Report not in Submitted status.</exception>
    Task<WasteReportDetailDto> UpdateAsync(
        Guid reportId,
        UpdateWasteReportRequest request,
        Guid actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Business-cancels a Citizen's own Submitted report (no physical delete).
    /// Submitted → Cancelled with history entry, persisted atomically.
    /// </summary>
    /// <exception cref="SmartWaste.Application.Common.Exceptions.NotFoundException">Report not found.</exception>
    /// <exception cref="SmartWaste.Application.Common.Exceptions.ForbiddenException">Not the owning Citizen.</exception>
    /// <exception cref="SmartWaste.Application.Common.Exceptions.BusinessRuleConflictException">Report not in Submitted status.</exception>
    Task<WasteReportDetailDto> CancelAsync(
        Guid reportId,
        Guid actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// MunicipalManager or WasteOfficer initiates review: Submitted → UnderReview. Persisted atomically with history.
    /// Viewing a report does NOT automatically start review.
    /// </summary>
    /// <exception cref="SmartWaste.Application.Common.Exceptions.NotFoundException">Report not found.</exception>
    /// <exception cref="SmartWaste.Application.Common.Exceptions.ForbiddenException">Actor is not authorized staff.</exception>
    /// <exception cref="SmartWaste.Application.Common.Exceptions.BusinessRuleConflictException">Report not in Submitted status.</exception>
    Task<WasteReportDetailDto> StartReviewAsync(
        Guid reportId,
        Guid actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// MunicipalManager or WasteOfficer verifies a report: UnderReview → Verified, with an explicit priority.
    /// VerifiedByUserId and VerifiedAt are set. Persisted atomically with history.
    /// </summary>
    /// <exception cref="SmartWaste.Application.Common.Exceptions.NotFoundException">Report not found.</exception>
    /// <exception cref="SmartWaste.Application.Common.Exceptions.ForbiddenException">Actor is not authorized staff.</exception>
    /// <exception cref="SmartWaste.Application.Common.Exceptions.BusinessRuleConflictException">Report not in UnderReview status.</exception>
    Task<WasteReportDetailDto> VerifyAsync(
        Guid reportId,
        VerifyWasteReportRequest request,
        Guid actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// MunicipalManager or WasteOfficer rejects a report: UnderReview → Rejected. Reason stored in history notes.
    /// VerifiedByUserId/VerifiedAt are NOT set on rejection. Persisted atomically with history.
    /// </summary>
    /// <exception cref="SmartWaste.Application.Common.Exceptions.NotFoundException">Report not found.</exception>
    /// <exception cref="SmartWaste.Application.Common.Exceptions.ForbiddenException">Actor is not authorized staff.</exception>
    /// <exception cref="SmartWaste.Application.Common.Exceptions.BusinessRuleConflictException">Report not in UnderReview status.</exception>
    Task<WasteReportDetailDto> RejectAsync(
        Guid reportId,
        RejectWasteReportRequest request,
        Guid actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the chronological status transition audit trail for a report, sorted by ChangedAt ascending.
    /// Citizen: own report only. WasteOfficer/MunicipalManager: any report. Driver: forbidden.
    /// </summary>
    /// <exception cref="SmartWaste.Application.Common.Exceptions.NotFoundException">Report not found.</exception>
    /// <exception cref="SmartWaste.Application.Common.Exceptions.ForbiddenException">Actor lacks access.</exception>
    Task<IReadOnlyList<WasteReportStatusHistoryDto>> GetHistoryAsync(
        Guid reportId,
        Guid actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a paginated, read-only, minimal set of waste reports that have already been explicitly verified.
    /// Used strictly by the internal AI service. Authoritatively forces Status == Verified.
    /// Zero business-state side effects.
    /// </summary>
    Task<PagedResult<VerifiedWasteReportToolItemDto>> GetVerifiedReportsForAiAsync(
        GetVerifiedWasteReportsForAiQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns exactly one report by authoritative UUID for advisory pre-verification analysis.
    /// Only Submitted and UnderReview reports are eligible; no report state is changed.
    /// </summary>
    Task<WasteReportForVerificationToolItemDto> GetReportForVerificationAiAsync(
        Guid reportId,
        CancellationToken cancellationToken = default);
}
