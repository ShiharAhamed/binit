using SmartWaste.Application.Collection.DTOs.Requests;
using SmartWaste.Application.Collection.DTOs.Responses;
using SmartWaste.Application.Collection.Queries;
using SmartWaste.Application.Common.Models;

namespace SmartWaste.Application.Collection.Interfaces;

/// <summary>
/// Application service interface for collection task management (Component 2).
/// Methods receive explicit actorUserId and actorRole sourced from authenticated JWT claims.
/// </summary>
public interface ICollectionTaskService
{
    /// <summary>
    /// Creates an authoritative collection task via the manual WasteOfficer path.
    /// Restricted to WasteOfficer.
    /// </summary>
    Task<CollectionTaskDetailDto> CreateManualTaskAsync(
        CreateManualCollectionTaskRequest request,
        Guid actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates an authoritative collection task from an approved AI collection planning proposal.
    /// Restricted to MunicipalManager.
    /// Reuses all authoritative domain rules (report status/claim, bin observations, routine schedules, task codes).
    /// Does not initiate an internal transaction if an ambient/outer transaction exists.
    /// </summary>
    Task<CollectionTaskDetailDto> CreateTaskFromApprovedPlanAsync(
        CreateManualCollectionTaskRequest request,
        Guid managerUserId,
        CancellationToken cancellationToken = default);

    Task<CollectionTaskDetailDto> CreateReplacementTaskAsync(
        Guid failedTaskId,
        CreateReplacementCollectionTaskRequest request,
        Guid actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves detailed collection task information including target details and histories.
    /// Accessible by WasteOfficer and MunicipalManager.
    /// </summary>
    Task<CollectionTaskDetailDto> GetByIdAsync(
        Guid id,
        Guid actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a paginated list of collection tasks with operational filters.
    /// Accessible by WasteOfficer and MunicipalManager.
    /// </summary>
    Task<PagedResult<CollectionTaskSummaryDto>> GetListAsync(
        CollectionTaskListQuery query,
        Guid actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reschedules an unstarted (Scheduled) collection task to a new planned execution time.
    /// Restricted to WasteOfficer.
    /// </summary>
    Task<CollectionTaskDetailDto> RescheduleTaskAsync(
        Guid id,
        RescheduleCollectionTaskRequest request,
        Guid actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the complete audit trail of status transitions and reschedule events for a task.
    /// Accessible by WasteOfficer and MunicipalManager.
    /// </summary>
    Task<CollectionTaskAuditTrailDto> GetTaskAuditTrailAsync(
        Guid id,
        Guid actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default);
}
