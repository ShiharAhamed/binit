using System.Data;
using System.Security.Cryptography;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using SmartWaste.Application.Collection.DTOs.Requests;
using SmartWaste.Application.Collection.DTOs.Responses;
using SmartWaste.Application.Collection.Interfaces;
using SmartWaste.Application.Collection.Queries;
using SmartWaste.Application.Collection.Validation;
using SmartWaste.Application.Common.Exceptions;
using SmartWaste.Application.Common.Models;
using SmartWaste.Domain.Collection.Entities;
using SmartWaste.Domain.Collection.Enums;
using SmartWaste.Domain.Common;
using SmartWaste.Domain.Reporting.Entities;
using SmartWaste.Domain.Reporting.Enums;
using SmartWaste.Infrastructure.Persistence;

namespace SmartWaste.Infrastructure.Collection.Services;

/// <summary>
/// Infrastructure service implementing ICollectionTaskService (Component 2).
/// Supports:
/// - GetListAsync (paginated task list with operational filters)
/// - GetByIdAsync (detailed task view with target summary and histories)
/// - GetTaskAuditTrailAsync (chronological status transitions and reschedule events)
/// - CreateManualTaskAsync (authoritative manual collection task creation for WasteOfficers)
/// - RescheduleTaskAsync (authoritative collection task rescheduling for WasteOfficers)
/// </summary>
public class CollectionTaskService : ICollectionTaskService
{
    private static readonly SemaphoreSlim _reportSchedulingLock = new(1, 1);
    private static readonly SemaphoreSlim _taskReschedulingLock = new(1, 1);
    private readonly AppDbContext _db;
    private readonly IConfiguration? _configuration;
    private readonly CollectionTaskListQueryValidator _listQueryValidator = new();
    private readonly CreateManualCollectionTaskRequestValidator _createValidator = new();
    private readonly CreateReplacementCollectionTaskRequestValidator _replacementValidator = new();
    private readonly RescheduleCollectionTaskRequestValidator _rescheduleValidator = new();

    public CollectionTaskService(AppDbContext db, IConfiguration? configuration = null)
    {
        _db = db;
        _configuration = configuration;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // MANUAL TASK CREATION (Step 10A.4e.2)
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates an authoritative collection task via the manual WasteOfficer path.
    /// Restricted to WasteOfficer.
    /// </summary>
    public async Task<CollectionTaskDetailDto> CreateManualTaskAsync(
        CreateManualCollectionTaskRequest request,
        Guid actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default)
    {
        // 1. Authorization: Only WasteOfficer can manually create a collection task
        if (actorRole != AppRoles.WasteOfficer)
        {
            throw new ForbiddenException("Only Waste Officers can manually create collection tasks.");
        }

        // 2. Request validation
        var validationResult = await _createValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var nowUtc = DateTime.UtcNow;

        if (request.ScheduledAt.HasValue && request.ScheduledAt.Value < nowUtc)
        {
            throw new ValidationException("Scheduled time cannot be in the past.");
        }

        var isInMemory = _db.Database.ProviderName?.Contains("InMemory") == true;
        await using var tx = (isInMemory || _db.Database.CurrentTransaction != null) ? null : await _db.Database.BeginTransactionAsync(cancellationToken);

        var lockAcquired = false;
        if (request.WasteReportId.HasValue)
        {
            await _reportSchedulingLock.WaitAsync(cancellationToken);
            lockAcquired = true;
        }

        try
        {
            var taskCode = await GenerateUniqueTaskCodeAsync(nowUtc, cancellationToken);
            CollectionTask task;

            if (request.WasteReportId.HasValue)
            {
                task = await HandleReportTaskCreationAsync(request, taskCode, actorUserId, nowUtc, isInMemory, cancellationToken);
            }
            else if (request.WasteBinId.HasValue)
            {
                task = await HandleBinTaskCreationAsync(request, taskCode, actorUserId, nowUtc, cancellationToken);
            }
            else
            {
                throw new ValidationException("Exactly one target (WasteReportId or WasteBinId) must be supplied.");
            }

            await _db.SaveChangesAsync(cancellationToken);

            if (tx != null)
            {
                await tx.CommitAsync(cancellationToken);
            }

            return await BuildDetailDtoAsync(task.Id, cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            if (tx != null)
            {
                await tx.RollbackAsync(cancellationToken);
            }

            // Structured PostgreSQL unique violation identification using error codes and constraint names
            if (ex.InnerException is PostgresException postgresEx && postgresEx.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                if (postgresEx.ConstraintName == "IX_CollectionTasks_WasteReportId_Active" ||
                    postgresEx.ConstraintName == "IX_CollectionTasks_WasteBinId_Active")
                {
                    throw new BusinessRuleConflictException("An active collection task already exists for this target.");
                }

                if (postgresEx.ConstraintName == "IX_CollectionTasks_TaskCode")
                {
                    throw new BusinessRuleConflictException("A task code collision occurred. Please retry the operation.");
                }
            }
            else if (ex.InnerException?.Message.Contains("IX_CollectionTasks_WasteReportId_Active") == true ||
                     ex.InnerException?.Message.Contains("IX_CollectionTasks_WasteBinId_Active") == true ||
                     ex.Message.Contains("IX_CollectionTasks_WasteReportId_Active") ||
                     ex.Message.Contains("IX_CollectionTasks_WasteBinId_Active"))
            {
                throw new BusinessRuleConflictException("An active collection task already exists for this target.");
            }
            else if (ex.InnerException?.Message.Contains("IX_CollectionTasks_TaskCode") == true ||
                     ex.Message.Contains("IX_CollectionTasks_TaskCode"))
            {
                throw new BusinessRuleConflictException("A task code collision occurred. Please retry the operation.");
            }

            throw;
        }
        catch (Exception)
        {
            if (tx != null)
            {
                await tx.RollbackAsync(cancellationToken);
            }
            throw;
        }
        finally
        {
            if (lockAcquired)
            {
                _reportSchedulingLock.Release();
            }
        }
    }

    /// <summary>
    /// Creates an authoritative collection task from an approved AI collection planning proposal.
    /// Restricted to MunicipalManager.
    /// Reuses all authoritative domain rules (report status/claim, bin observations, routine schedules, task codes).
    /// Does not initiate an internal transaction if an ambient/outer transaction exists.
    /// </summary>
    public async Task<CollectionTaskDetailDto> CreateTaskFromApprovedPlanAsync(
        CreateManualCollectionTaskRequest request,
        Guid managerUserId,
        CancellationToken cancellationToken = default)
    {
        // 1. Request validation
        var validationResult = await _createValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var nowUtc = DateTime.UtcNow;

        if (request.ScheduledAt.HasValue && request.ScheduledAt.Value < nowUtc)
        {
            throw new ValidationException("Scheduled time cannot be in the past.");
        }

        var isInMemory = _db.Database.ProviderName?.Contains("InMemory") == true;
        await using var tx = (isInMemory || _db.Database.CurrentTransaction != null)
            ? null
            : await _db.Database.BeginTransactionAsync(cancellationToken);

        var lockAcquired = false;
        if (request.WasteReportId.HasValue)
        {
            await _reportSchedulingLock.WaitAsync(cancellationToken);
            lockAcquired = true;
        }

        try
        {
            var taskCode = await GenerateUniqueTaskCodeAsync(nowUtc, cancellationToken);
            CollectionTask task;

            if (request.WasteReportId.HasValue)
            {
                task = await HandleReportTaskCreationAsync(
                    request,
                    taskCode,
                    managerUserId,
                    nowUtc,
                    isInMemory,
                    cancellationToken,
                    creationMethod: TaskCreationMethod.ApprovedAiPlan,
                    initialHistoryNotes: "Task created from approved AI collection plan");
            }
            else if (request.WasteBinId.HasValue)
            {
                task = await HandleBinTaskCreationAsync(
                    request,
                    taskCode,
                    managerUserId,
                    nowUtc,
                    cancellationToken,
                    creationMethod: TaskCreationMethod.ApprovedAiPlan,
                    initialHistoryNotes: "Task created from approved AI collection plan");
            }
            else
            {
                throw new ValidationException("Exactly one target (WasteReportId or WasteBinId) must be supplied.");
            }

            await _db.SaveChangesAsync(cancellationToken);

            if (tx != null)
            {
                await tx.CommitAsync(cancellationToken);
            }

            return await BuildDetailDtoAsync(task.Id, cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            if (tx != null)
            {
                await tx.RollbackAsync(cancellationToken);
            }

            if (ex.InnerException is PostgresException postgresEx && postgresEx.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                if (postgresEx.ConstraintName == "IX_CollectionTasks_WasteReportId_Active" ||
                    postgresEx.ConstraintName == "IX_CollectionTasks_WasteBinId_Active")
                {
                    throw new BusinessRuleConflictException("An active collection task already exists for this target.");
                }

                if (postgresEx.ConstraintName == "IX_CollectionTasks_TaskCode")
                {
                    throw new BusinessRuleConflictException("A task code collision occurred. Please retry the operation.");
                }
            }
            else if (ex.InnerException?.Message.Contains("IX_CollectionTasks_WasteReportId_Active") == true ||
                     ex.InnerException?.Message.Contains("IX_CollectionTasks_WasteBinId_Active") == true ||
                     ex.Message.Contains("IX_CollectionTasks_WasteReportId_Active") ||
                     ex.Message.Contains("IX_CollectionTasks_WasteBinId_Active"))
            {
                throw new BusinessRuleConflictException("An active collection task already exists for this target.");
            }
            else if (ex.InnerException?.Message.Contains("IX_CollectionTasks_TaskCode") == true ||
                     ex.Message.Contains("IX_CollectionTasks_TaskCode"))
            {
                throw new BusinessRuleConflictException("A task code collision occurred. Please retry the operation.");
            }

            throw;
        }
        catch (Exception)
        {
            if (tx != null)
            {
                await tx.RollbackAsync(cancellationToken);
            }
            throw;
        }
        finally
        {
            if (lockAcquired)
            {
                _reportSchedulingLock.Release();
            }
        }
    }

    public async Task<CollectionTaskDetailDto> CreateReplacementTaskAsync(
        Guid failedTaskId,
        CreateReplacementCollectionTaskRequest request,
        Guid actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default)
    {
        if (actorRole != AppRoles.WasteOfficer) throw new ForbiddenException("Only Waste Officers can create failed-task replacements.");
        var validation = await _replacementValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) throw new ValidationException(validation.Errors);
        var nowUtc = DateTime.UtcNow;
        var isInMemory = _db.Database.ProviderName?.Contains("InMemory") == true;
        await using var tx = isInMemory ? null : await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var original = await _db.CollectionTasks.Include(x => x.WasteReport).Include(x => x.WasteBin).SingleOrDefaultAsync(x => x.Id == failedTaskId, cancellationToken)
                ?? throw new NotFoundException($"Collection task '{failedTaskId}' was not found.");
            if (original.Status != CollectionTaskStatus.Failed) throw new BusinessRuleConflictException("Only terminal Failed collection tasks can be replaced.");
            if (original.WasteReportId.HasValue == original.WasteBinId.HasValue) throw new BusinessRuleConflictException("The failed collection task does not have a valid single target.");
            if (await _db.CollectionAssignmentTaskClaims.AnyAsync(x => x.CollectionTaskId == original.Id && x.IsActive, cancellationToken)) throw new BusinessRuleConflictException("The failed task still has an active assignment claim.");
            var hasConflictingTask = await _db.CollectionTasks.AnyAsync(x => x.Id != original.Id &&
                ((original.WasteReportId.HasValue && x.WasteReportId == original.WasteReportId) || (original.WasteBinId.HasValue && x.WasteBinId == original.WasteBinId)) &&
                (x.Status == CollectionTaskStatus.Scheduled || x.Status == CollectionTaskStatus.Assigned || x.Status == CollectionTaskStatus.InProgress), cancellationToken);
            if (hasConflictingTask) throw new BusinessRuleConflictException("An active collection task already exists for this target.");
            var taskCode = await GenerateUniqueTaskCodeAsync(nowUtc, cancellationToken);
            var replacementReason = request.ReplacementReason!.Trim();
            var handlingNotes = string.IsNullOrWhiteSpace(request.HandlingNotes) ? original.HandlingNotes : request.HandlingNotes.Trim();
            CollectionTask replacement;
            if (original.WasteReportId.HasValue)
            {
                var report = original.WasteReport ?? throw new BusinessRuleConflictException("The failed task report target no longer exists.");
                if (original.CollectionReason != CollectionReason.VerifiedReport || report.Status != WasteReportStatus.InProgress) throw new BusinessRuleConflictException("The failed report task source is not eligible for replacement.");
                replacement = new CollectionTask { TaskCode = taskCode, WasteReportId = report.Id, CollectionReason = CollectionReason.VerifiedReport, Status = CollectionTaskStatus.Scheduled, ScheduledAt = request.ScheduledAt!.Value, HandlingNotes = handlingNotes, SchedulingReason = string.IsNullOrWhiteSpace(request.SchedulingReason) ? original.SchedulingReason : request.SchedulingReason.Trim(), CreatedByUserId = actorUserId, CreationMethod = TaskCreationMethod.Manual, CreatedAt = nowUtc };
                report.Status = WasteReportStatus.Scheduled; report.UpdatedAt = nowUtc;
                _db.WasteReportStatusHistories.Add(new WasteReportStatusHistory { WasteReportId = report.Id, FromStatus = WasteReportStatus.InProgress, ToStatus = WasteReportStatus.Scheduled, ChangedByUserId = actorUserId, ChangedAt = nowUtc, Notes = $"Failed task {original.TaskCode} replaced by {taskCode}: {replacementReason}" });
                _db.CollectionTasks.Add(replacement);
                _db.CollectionTaskStatusHistories.Add(new CollectionTaskStatusHistory { CollectionTaskId = replacement.Id, ToStatus = CollectionTaskStatus.Scheduled, ChangedByUserId = actorUserId, ChangedAt = nowUtc, Notes = $"Replacement for failed task {original.TaskCode}: {replacementReason}" });
            }
            else
            {
                var bin = original.WasteBin ?? throw new BusinessRuleConflictException("The failed task bin target no longer exists.");
                var schedulingReason = string.IsNullOrWhiteSpace(request.SchedulingReason) ? original.SchedulingReason : request.SchedulingReason.Trim();
                var creationRequest = new CreateManualCollectionTaskRequest { WasteBinId = bin.Id, CollectionReason = original.CollectionReason, ScheduledAt = request.ScheduledAt, HandlingNotes = handlingNotes, SchedulingReason = schedulingReason };
                replacement = await HandleBinTaskCreationAsync(creationRequest, taskCode, actorUserId, nowUtc, cancellationToken, allowFailedReplacement: true, initialHistoryNotes: $"Replacement for failed task {original.TaskCode}: {replacementReason}");
            }
            await _db.SaveChangesAsync(cancellationToken);
            if (tx is not null) await tx.CommitAsync(cancellationToken);
            return await BuildDetailDtoAsync(replacement.Id, cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            if (tx is not null) await tx.RollbackAsync(cancellationToken);
            throw new BusinessRuleConflictException("A replacement task for this target was created concurrently.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure })
        {
            if (tx is not null) await tx.RollbackAsync(cancellationToken);
            throw new BusinessRuleConflictException("The failed task changed while replacement was in progress. Retry the operation.");
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.SerializationFailure)
        {
            if (tx is not null) await tx.RollbackAsync(cancellationToken);
            throw new BusinessRuleConflictException("The failed task changed while replacement was in progress. Retry the operation.");
        }
        catch
        {
            if (tx is not null) await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task<CollectionTask> HandleReportTaskCreationAsync(
        CreateManualCollectionTaskRequest request,
        string taskCode,
        Guid actorUserId,
        DateTime nowUtc,
        bool isInMemory,
        CancellationToken cancellationToken,
        TaskCreationMethod creationMethod = TaskCreationMethod.Manual,
        string? initialHistoryNotes = null)
    {
        var reportId = request.WasteReportId!.Value;

        // 1. Check if report exists
        var report = await _db.WasteReports
            .FirstOrDefaultAsync(r => r.Id == reportId, cancellationToken);

        if (report is null)
        {
            throw new NotFoundException($"Waste report '{reportId}' was not found.");
        }

        // 2. Precondition guard: Report must be in Verified status
        if (report.Status != WasteReportStatus.Verified)
        {
            throw new BusinessRuleConflictException(
                $"Cannot schedule collection task for waste report '{reportId}'. Current status is '{report.Status}', but 'Verified' is required.");
        }

        // 3. Active task guard: Target must not already have an active task
        var hasActiveTask = await _db.CollectionTasks
            .AnyAsync(t => t.WasteReportId == reportId &&
                           (t.Status == CollectionTaskStatus.Scheduled ||
                            t.Status == CollectionTaskStatus.Assigned ||
                            t.Status == CollectionTaskStatus.InProgress), cancellationToken);

        if (hasActiveTask)
        {
            throw new BusinessRuleConflictException($"Waste report '{reportId}' already has an active collection task.");
        }

        // 4. Authoritative database-level conditional report claim (relational PostgreSQL)
        if (!isInMemory)
        {
            var rowsUpdated = await _db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"WasteReports\" SET \"Status\" = {WasteReportStatus.Scheduled.ToString()}, \"UpdatedAt\" = {nowUtc} WHERE \"Id\" = {reportId} AND \"Status\" = {WasteReportStatus.Verified.ToString()}",
                cancellationToken);

            if (rowsUpdated == 0)
            {
                var currentReport = await _db.WasteReports
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r => r.Id == reportId, cancellationToken);

                if (currentReport is null)
                {
                    throw new NotFoundException($"Waste report '{reportId}' was not found.");
                }

                throw new BusinessRuleConflictException(
                    $"Cannot schedule collection task for waste report '{reportId}'. Current status is '{currentReport.Status}', but 'Verified' is required.");
            }

            report.Status = WasteReportStatus.Scheduled;
            report.UpdatedAt = nowUtc;
            _db.Entry(report).Property(r => r.Status).IsModified = false;
            _db.Entry(report).Property(r => r.UpdatedAt).IsModified = false;
        }
        else
        {
            report.Status = WasteReportStatus.Scheduled;
            report.UpdatedAt = nowUtc;
        }

        // 5. Create Scheduled CollectionTask
        var task = new CollectionTask
        {
            Id = Guid.NewGuid(),
            TaskCode = taskCode,
            WasteReportId = report.Id,
            WasteBinId = null,
            CollectionReason = CollectionReason.VerifiedReport,
            Status = CollectionTaskStatus.Scheduled,
            ScheduledAt = request.ScheduledAt!.Value,
            HandlingNotes = string.IsNullOrWhiteSpace(request.HandlingNotes) ? null : request.HandlingNotes.Trim(),
            SchedulingReason = string.IsNullOrWhiteSpace(request.SchedulingReason) ? null : request.SchedulingReason.Trim(),
            CreatedByUserId = actorUserId,
            CreationMethod = creationMethod,
            TriggerObservationId = null,
            RoutineDueDate = null,
            CreatedAt = nowUtc
        };
        _db.CollectionTasks.Add(task);

        // 6. Append initial CollectionTaskStatusHistory
        var taskStatusHistory = new CollectionTaskStatusHistory
        {
            Id = Guid.NewGuid(),
            CollectionTaskId = task.Id,
            FromStatus = null,
            ToStatus = CollectionTaskStatus.Scheduled,
            ChangedByUserId = actorUserId,
            Notes = initialHistoryNotes ?? "Task manually created",
            ChangedAt = nowUtc
        };
        _db.CollectionTaskStatusHistories.Add(taskStatusHistory);

        // 7. Append C1 WasteReportStatusHistory
        var reportStatusHistory = new WasteReportStatusHistory
        {
            Id = Guid.NewGuid(),
            WasteReportId = report.Id,
            FromStatus = WasteReportStatus.Verified,
            ToStatus = WasteReportStatus.Scheduled,
            ChangedByUserId = actorUserId,
            Notes = $"Collection task {taskCode} scheduled",
            ChangedAt = nowUtc
        };
        _db.WasteReportStatusHistories.Add(reportStatusHistory);

        return task;
    }

    private async Task<CollectionTask> HandleBinTaskCreationAsync(
        CreateManualCollectionTaskRequest request,
        string taskCode,
        Guid actorUserId,
        DateTime nowUtc,
        CancellationToken cancellationToken,
        bool allowFailedReplacement = false,
        string? initialHistoryNotes = null,
        TaskCreationMethod creationMethod = TaskCreationMethod.Manual)
    {
        var binId = request.WasteBinId!.Value;

        // 1. Check if bin exists
        var bin = await _db.WasteBins
            .Include(b => b.AcceptedWasteTypes)
            .FirstOrDefaultAsync(b => b.Id == binId, cancellationToken);

        if (bin is null)
        {
            throw new NotFoundException($"Waste bin '{binId}' was not found.");
        }

        // 2. Administrative status guard
        if (bin.AdministrativeStatus != BinAdministrativeStatus.Active)
        {
            throw new BusinessRuleConflictException(
                $"Cannot schedule a collection task for waste bin '{binId}' with administrative status '{bin.AdministrativeStatus}'. Only 'Active' bins can be scheduled.");
        }

        // 3. Active task guard
        var hasActiveTask = await _db.CollectionTasks
            .AnyAsync(t => t.WasteBinId == binId &&
                           (t.Status == CollectionTaskStatus.Scheduled ||
                            t.Status == CollectionTaskStatus.Assigned ||
                            t.Status == CollectionTaskStatus.InProgress), cancellationToken);

        if (hasActiveTask)
        {
            throw new BusinessRuleConflictException($"Waste bin '{binId}' already has an active collection task.");
        }

        // 4. Failed task replacement review guard: Unreviewed replacement is prohibited
        var latestTask = await _db.CollectionTasks
            .Where(t => t.WasteBinId == binId)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (!allowFailedReplacement && latestTask != null && latestTask.Status == CollectionTaskStatus.Failed)
        {
            throw new BusinessRuleConflictException(
                $"Cannot schedule a collection task for waste bin '{binId}' because its previous collection task '{latestTask.TaskCode}' failed. Explicit WasteOfficer review is required before replacement.");
        }

        Guid? triggerObservationId = null;
        DateOnly? routineDueDateResult = null;

        // 5. Evaluate collection reason prerequisites
        switch (request.CollectionReason!.Value)
        {
            case CollectionReason.FullOrBlockedBin:
            {
                var latestObs = await _db.BinObservations
                    .Where(o => o.WasteBinId == binId)
                    .OrderByDescending(o => o.RecordedAt)
                    .ThenByDescending(o => o.Id)
                    .FirstOrDefaultAsync(cancellationToken);

                if (latestObs is null)
                {
                    throw new BusinessRuleConflictException(
                        $"Cannot schedule collection for bin '{binId}' with reason 'FullOrBlockedBin': no observations recorded.");
                }

                if (bin.LastCollectedAt.HasValue && bin.LastCollectedAt.Value >= latestObs.RecordedAt)
                {
                    throw new BusinessRuleConflictException(
                        $"Cannot schedule collection for bin '{binId}' with reason 'FullOrBlockedBin': the latest observation is superseded by a subsequent collection.");
                }

                if ((nowUtc - latestObs.RecordedAt).TotalHours > 48.0)
                {
                    throw new BusinessRuleConflictException(
                        $"Cannot schedule collection for bin '{binId}' with reason 'FullOrBlockedBin': the latest observation is stale (older than 48 hours).");
                }

                if (latestObs.Condition == BinCondition.Damaged || latestObs.Condition == BinCondition.Missing)
                {
                    throw new BusinessRuleConflictException(
                        $"Cannot schedule collection for bin '{binId}' with reason 'FullOrBlockedBin': condition is '{latestObs.Condition}', which represents a maintenance concern rather than an ordinary collection need.");
                }

                if (latestObs.FillLevelPercent != 100 && latestObs.Condition != BinCondition.Blocked)
                {
                    throw new BusinessRuleConflictException(
                        $"Cannot schedule collection for bin '{binId}' with reason 'FullOrBlockedBin': latest observation has fill level {latestObs.FillLevelPercent}% and condition '{latestObs.Condition}'. A fill level of 100% or 'Blocked' condition is required.");
                }

                triggerObservationId = latestObs.Id;
                break;
            }

            case CollectionReason.RoutineCollection:
            {
                var municipalityTimeZone = ResolveMunicipalityTimeZone();
                if (!IsRoutineDue(bin, nowUtc, municipalityTimeZone, out var routineDueDate))
                {
                    throw new BusinessRuleConflictException(
                        $"Cannot schedule collection for bin '{binId}' with reason 'RoutineCollection': the bin is not currently due for routine collection in the configured schedule.");
                }

                routineDueDateResult = routineDueDate;
                break;
            }

            case CollectionReason.OfficerDiscretion:
            {
                if (string.IsNullOrWhiteSpace(request.SchedulingReason))
                {
                    throw new BusinessRuleConflictException(
                        "SchedulingReason is strictly mandatory when collection reason is 'OfficerDiscretion'.");
                }
                break;
            }

            default:
                throw new BusinessRuleConflictException($"Invalid collection reason '{request.CollectionReason}' for a bin target.");
        }

        // 6. Create CollectionTask
        var task = new CollectionTask
        {
            Id = Guid.NewGuid(),
            TaskCode = taskCode,
            WasteReportId = null,
            WasteBinId = bin.Id,
            CollectionReason = request.CollectionReason!.Value,
            Status = CollectionTaskStatus.Scheduled,
            ScheduledAt = request.ScheduledAt!.Value,
            HandlingNotes = string.IsNullOrWhiteSpace(request.HandlingNotes) ? null : request.HandlingNotes.Trim(),
            SchedulingReason = string.IsNullOrWhiteSpace(request.SchedulingReason) ? null : request.SchedulingReason.Trim(),
            CreatedByUserId = actorUserId,
            CreationMethod = creationMethod,
            TriggerObservationId = triggerObservationId,
            RoutineDueDate = routineDueDateResult,
            CreatedAt = nowUtc
        };
        _db.CollectionTasks.Add(task);

        // 7. Append initial CollectionTaskStatusHistory
        var taskStatusHistory = new CollectionTaskStatusHistory
        {
            Id = Guid.NewGuid(),
            CollectionTaskId = task.Id,
            FromStatus = null,
            ToStatus = CollectionTaskStatus.Scheduled,
            ChangedByUserId = actorUserId,
            Notes = initialHistoryNotes ?? "Task manually created",
            ChangedAt = nowUtc
        };
        _db.CollectionTaskStatusHistories.Add(taskStatusHistory);

        return task;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // READ OPERATIONS (Step 10A.4e.1)
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a paginated list of collection tasks with operational filters.
    /// Accessible by WasteOfficer and MunicipalManager.
    /// </summary>
    public async Task<PagedResult<CollectionTaskSummaryDto>> GetListAsync(
        CollectionTaskListQuery query,
        Guid actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default)
    {
        // 1. Authorization: WasteOfficer or MunicipalManager only
        if (actorRole != AppRoles.WasteOfficer && actorRole != AppRoles.MunicipalManager)
        {
            throw new ForbiddenException("Only Waste Officers and Municipal Managers can access collection tasks.");
        }

        // 2. Query validation
        var validationResult = await _listQueryValidator.ValidateAsync(query, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        // 3. Build query with eager loading of target and creator navigation properties
        var baseQuery = _db.CollectionTasks
            .AsNoTracking()
            .Include(t => t.WasteReport)
            .Include(t => t.WasteBin)
            .Include(t => t.CreatedByUser)
            .AsQueryable();

        // 4. Apply filters
        if (query.Status.HasValue)
        {
            baseQuery = baseQuery.Where(t => t.Status == query.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.TargetType))
        {
            if (query.TargetType.Equals("Report", StringComparison.OrdinalIgnoreCase))
            {
                baseQuery = baseQuery.Where(t => t.WasteReportId != null);
            }
            else if (query.TargetType.Equals("Bin", StringComparison.OrdinalIgnoreCase))
            {
                baseQuery = baseQuery.Where(t => t.WasteBinId != null);
            }
        }

        if (query.CollectionReason.HasValue)
        {
            baseQuery = baseQuery.Where(t => t.CollectionReason == query.CollectionReason.Value);
        }

        if (query.DateFrom.HasValue || query.DateTo.HasValue)
        {
            var municipalityTimeZone = ResolveMunicipalityTimeZone();
            if (query.DateFrom.HasValue)
            {
                var fromUtc = ConvertMunicipalDateStartToUtc(query.DateFrom.Value, municipalityTimeZone);
                baseQuery = baseQuery.Where(t => t.ScheduledAt >= fromUtc);
            }

            if (query.DateTo.HasValue)
            {
                var toExclusiveUtc = ConvertMunicipalDateStartToUtc(query.DateTo.Value.AddDays(1), municipalityTimeZone);
                baseQuery = baseQuery.Where(t => t.ScheduledAt < toExclusiveUtc);
            }
        }

        // 5. Total count before pagination
        var totalCount = await baseQuery.CountAsync(cancellationToken);

        var page = query.Page > 0 ? query.Page : 1;
        var pageSize = query.PageSize is > 0 and <= 100 ? query.PageSize : 20;

        // 6. Deterministic ordering: ScheduledAt ASC, Id ASC tie-breaker
        var pagedTasks = await baseQuery
            .OrderBy(t => t.ScheduledAt)
            .ThenBy(t => t.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        // 7. Project to summary DTOs
        var items = pagedTasks.Select(t => new CollectionTaskSummaryDto
        {
            Id = t.Id,
            TaskCode = t.TaskCode,
            TargetType = t.WasteBinId.HasValue ? "Bin" : "Report",
            WasteReportId = t.WasteReportId,
            WasteBinId = t.WasteBinId,
            TargetReference = t.WasteBin != null
                ? t.WasteBin.BinCode
                : (t.WasteReportId.HasValue ? $"Report {SmartWaste.Application.Reporting.WasteReportReference.FromId(t.WasteReportId.Value)}" : string.Empty),
            CollectionReason = t.CollectionReason,
            Status = t.Status,
            ScheduledAt = t.ScheduledAt,
            CreationMethod = t.CreationMethod,
            CreatedByUserId = t.CreatedByUserId,
            CreatedByUserName = t.CreatedByUser?.FullName ?? t.CreatedByUser?.UserName ?? "Officer",
            CreatedAt = t.CreatedAt
        }).ToList();

        return new PagedResult<CollectionTaskSummaryDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    /// <summary>
    /// Retrieves detailed collection task information including target details and histories.
    /// Accessible by WasteOfficer and MunicipalManager.
    /// </summary>
    public async Task<CollectionTaskDetailDto> GetByIdAsync(
        Guid id,
        Guid actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default)
    {
        // 1. Authorization: WasteOfficer or MunicipalManager only
        if (actorRole != AppRoles.WasteOfficer && actorRole != AppRoles.MunicipalManager)
        {
            throw new ForbiddenException("Only Waste Officers and Municipal Managers can access collection task details.");
        }

        return await BuildDetailDtoAsync(id, cancellationToken);
    }

    /// <summary>
    /// Retrieves the complete audit trail of status transitions and reschedule events for a task.
    /// Accessible by WasteOfficer and MunicipalManager.
    /// </summary>
    public async Task<CollectionTaskAuditTrailDto> GetTaskAuditTrailAsync(
        Guid id,
        Guid actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default)
    {
        // 1. Authorization: WasteOfficer or MunicipalManager only
        if (actorRole != AppRoles.WasteOfficer && actorRole != AppRoles.MunicipalManager)
        {
            throw new ForbiddenException("Only Waste Officers and Municipal Managers can access collection task history.");
        }

        // 2. Fetch task with histories
        var task = await _db.CollectionTasks
            .AsNoTracking()
            .Include(t => t.StatusHistory)
                .ThenInclude(h => h.ChangedByUser)
            .Include(t => t.ScheduleHistory)
                .ThenInclude(h => h.RescheduledByUser)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (task is null)
        {
            throw new NotFoundException($"Collection task '{id}' was not found.");
        }

        // 3. Project status history chronologically (ChangedAt ASC, Id ASC)
        var statusHistory = task.StatusHistory
            .OrderBy(h => h.ChangedAt)
            .ThenBy(h => h.Id)
            .Select(h => new CollectionTaskStatusHistoryDto
            {
                Id = h.Id,
                FromStatus = h.FromStatus?.ToString(),
                ToStatus = h.ToStatus.ToString(),
                ChangedByUserId = h.ChangedByUserId,
                ChangedByUserName = h.ChangedByUser?.FullName ?? h.ChangedByUser?.UserName ?? "Staff",
                Notes = h.Notes,
                ChangedAt = h.ChangedAt
            })
            .ToList();

        // 4. Project schedule history chronologically (RescheduledAt ASC, Id ASC)
        var scheduleHistory = task.ScheduleHistory
            .OrderBy(h => h.RescheduledAt)
            .ThenBy(h => h.Id)
            .Select(h => new CollectionTaskScheduleHistoryDto
            {
                Id = h.Id,
                PreviousScheduledAt = h.PreviousScheduledAt,
                NewScheduledAt = h.NewScheduledAt,
                Reason = h.Reason,
                RescheduledByUserId = h.RescheduledByUserId,
                RescheduledByUserName = h.RescheduledByUser?.FullName ?? h.RescheduledByUser?.UserName ?? "Staff",
                RescheduledAt = h.RescheduledAt
            })
            .ToList();

        return new CollectionTaskAuditTrailDto
        {
            CollectionTaskId = task.Id,
            TaskCode = task.TaskCode,
            StatusHistory = statusHistory,
            ScheduleHistory = scheduleHistory
        };
    }

    // ──────────────────────────────────────────────────────────────────────────
    // TASK RESCHEDULING (Step 10A.4e.3)
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Reschedules an unstarted (Scheduled) collection task to a new planned execution time.
    /// Restricted to WasteOfficer.
    /// </summary>
    public async Task<CollectionTaskDetailDto> RescheduleTaskAsync(
        Guid id,
        RescheduleCollectionTaskRequest request,
        Guid actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default)
    {
        // 1. Authorization: Only WasteOfficer can reschedule collection tasks
        if (actorRole != AppRoles.WasteOfficer)
        {
            throw new ForbiddenException("Only Waste Officers can reschedule collection tasks.");
        }

        // 2. Request validation
        var validationResult = await _rescheduleValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var nowUtc = DateTime.UtcNow;

        // Strict UTC future time guard
        if (request.NewScheduledAt.HasValue && request.NewScheduledAt.Value < nowUtc)
        {
            throw new ValidationException("New scheduled time cannot be in the past.");
        }

        var newScheduledAt = request.NewScheduledAt!.Value;

        // 3. Precondition guard: Retrieve task to verify existence and status
        var task = await _db.CollectionTasks
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (task is null)
        {
            throw new NotFoundException($"Collection task '{id}' was not found.");
        }

        if (task.Status != CollectionTaskStatus.Scheduled)
        {
            throw new BusinessRuleConflictException(
                $"Cannot reschedule collection task '{id}'. Current status is '{task.Status}', but only 'Scheduled' tasks can be rescheduled.");
        }

        var previousScheduledAt = task.ScheduledAt;

        // 4. Idempotency / No-op check: if unchanged, return existing details without updating UpdatedAt or adding history
        if (previousScheduledAt == newScheduledAt)
        {
            return await BuildDetailDtoAsync(task.Id, cancellationToken);
        }

        // 5. Atomic database execution & concurrency protection
        var isInMemory = _db.Database.ProviderName?.Contains("InMemory") == true;
        await using var tx = isInMemory ? null : await _db.Database.BeginTransactionAsync(cancellationToken);

        var lockAcquired = false;
        if (isInMemory)
        {
            await _taskReschedulingLock.WaitAsync(cancellationToken);
            lockAcquired = true;
        }

        try
        {
            if (!isInMemory)
            {
                var rowsUpdated = await _db.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE \"CollectionTasks\" SET \"ScheduledAt\" = {newScheduledAt}, \"UpdatedAt\" = {nowUtc} WHERE \"Id\" = {id} AND \"Status\" = {CollectionTaskStatus.Scheduled.ToString()} AND \"ScheduledAt\" = {previousScheduledAt}",
                    cancellationToken);

                if (rowsUpdated == 0)
                {
                    var currentTask = await _db.CollectionTasks
                        .AsNoTracking()
                        .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

                    if (currentTask is null)
                    {
                        throw new NotFoundException($"Collection task '{id}' was not found.");
                    }

                    if (currentTask.Status != CollectionTaskStatus.Scheduled)
                    {
                        throw new BusinessRuleConflictException(
                            $"Cannot reschedule collection task '{id}'. Current status is '{currentTask.Status}', but only 'Scheduled' tasks can be rescheduled.");
                    }

                    throw new BusinessRuleConflictException(
                        $"Cannot reschedule collection task '{id}'. The task has been concurrently modified or rescheduled by another operation.");
                }

                task.ScheduledAt = newScheduledAt;
                task.UpdatedAt = nowUtc;
                _db.Entry(task).Property(t => t.ScheduledAt).IsModified = false;
                _db.Entry(task).Property(t => t.UpdatedAt).IsModified = false;
            }
            else
            {
                // In-memory concurrency verification
                var currentTask = await _db.CollectionTasks
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

                if (currentTask is null)
                {
                    throw new NotFoundException($"Collection task '{id}' was not found.");
                }

                if (currentTask.Status != CollectionTaskStatus.Scheduled)
                {
                    throw new BusinessRuleConflictException(
                        $"Cannot reschedule collection task '{id}'. Current status is '{currentTask.Status}', but only 'Scheduled' tasks can be rescheduled.");
                }

                if (currentTask.ScheduledAt != previousScheduledAt)
                {
                    throw new BusinessRuleConflictException(
                        $"Cannot reschedule collection task '{id}'. The task has been concurrently modified or rescheduled by another operation.");
                }

                task.ScheduledAt = newScheduledAt;
                task.UpdatedAt = nowUtc;
            }

            var scheduleHistory = new CollectionTaskScheduleHistory
            {
                Id = Guid.NewGuid(),
                CollectionTaskId = id,
                PreviousScheduledAt = previousScheduledAt,
                NewScheduledAt = newScheduledAt,
                Reason = request.Reason.Trim(),
                RescheduledByUserId = actorUserId,
                RescheduledAt = nowUtc
            };

            _db.CollectionTaskScheduleHistories.Add(scheduleHistory);

            await _db.SaveChangesAsync(cancellationToken);

            if (tx != null)
            {
                await tx.CommitAsync(cancellationToken);
            }

            return await BuildDetailDtoAsync(task.Id, cancellationToken);
        }
        catch (Exception)
        {
            if (tx != null)
            {
                await tx.RollbackAsync(cancellationToken);
            }
            throw;
        }
        finally
        {
            if (lockAcquired)
            {
                _taskReschedulingLock.Release();
            }
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // PRIVATE HELPERS
    // ──────────────────────────────────────────────────────────────────────────

    private async Task<CollectionTaskDetailDto> BuildDetailDtoAsync(Guid id, CancellationToken cancellationToken)
    {
        var task = await _db.CollectionTasks
            .AsNoTracking()
            .Include(t => t.WasteReport)
            .Include(t => t.WasteBin)
                .ThenInclude(b => b!.AcceptedWasteTypes)
            .Include(t => t.CreatedByUser)
            .Include(t => t.TriggerObservation)
            .Include(t => t.StatusHistory)
                .ThenInclude(h => h.ChangedByUser)
            .Include(t => t.ScheduleHistory)
                .ThenInclude(h => h.RescheduledByUser)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (task is null)
        {
            throw new NotFoundException($"Collection task '{id}' was not found.");
        }

        int? latestFillLevel = task.TriggerObservation?.FillLevelPercent;
        if (!latestFillLevel.HasValue && task.WasteBinId.HasValue)
        {
            var latestObs = await _db.BinObservations
                .AsNoTracking()
                .Where(o => o.WasteBinId == task.WasteBinId.Value)
                .OrderByDescending(o => o.RecordedAt)
                .ThenByDescending(o => o.Id)
                .FirstOrDefaultAsync(cancellationToken);
            latestFillLevel = latestObs?.FillLevelPercent;
        }

        CollectionTaskTargetSummaryDto targetSummary;
        if (task.WasteBin != null)
        {
            targetSummary = new CollectionTaskTargetSummaryDto
            {
                Identifier = task.WasteBin.BinCode,
                Latitude = task.WasteBin.Latitude,
                Longitude = task.WasteBin.Longitude,
                AddressText = task.WasteBin.AddressText,
                CapacityLiters = task.WasteBin.CapacityLiters,
                WasteTypes = task.WasteBin.AcceptedWasteTypes.Select(a => a.WasteType.ToString()).ToList(),
                LatestFillLevelPercent = latestFillLevel
            };
        }
        else if (task.WasteReport != null)
        {
            targetSummary = new CollectionTaskTargetSummaryDto
            {
                Identifier = $"Report {SmartWaste.Application.Reporting.WasteReportReference.FromId(task.WasteReport.Id)}",
                Latitude = task.WasteReport.Latitude,
                Longitude = task.WasteReport.Longitude,
                AddressText = task.WasteReport.AddressText,
                CapacityLiters = null,
                WasteTypes = new[] { task.WasteReport.WasteType.ToString() },
                LatestFillLevelPercent = null
            };
        }
        else
        {
            targetSummary = new CollectionTaskTargetSummaryDto();
        }

        var statusHistory = task.StatusHistory
            .OrderBy(h => h.ChangedAt)
            .ThenBy(h => h.Id)
            .Select(h => new CollectionTaskStatusHistoryDto
            {
                Id = h.Id,
                FromStatus = h.FromStatus?.ToString(),
                ToStatus = h.ToStatus.ToString(),
                ChangedByUserId = h.ChangedByUserId,
                ChangedByUserName = h.ChangedByUser?.FullName ?? h.ChangedByUser?.UserName ?? "Staff",
                Notes = h.Notes,
                ChangedAt = h.ChangedAt
            })
            .ToList();

        var scheduleHistory = task.ScheduleHistory
            .OrderBy(h => h.RescheduledAt)
            .ThenBy(h => h.Id)
            .Select(h => new CollectionTaskScheduleHistoryDto
            {
                Id = h.Id,
                PreviousScheduledAt = h.PreviousScheduledAt,
                NewScheduledAt = h.NewScheduledAt,
                Reason = h.Reason,
                RescheduledByUserId = h.RescheduledByUserId,
                RescheduledByUserName = h.RescheduledByUser?.FullName ?? h.RescheduledByUser?.UserName ?? "Staff",
                RescheduledAt = h.RescheduledAt
            })
            .ToList();

        return new CollectionTaskDetailDto
        {
            Id = task.Id,
            TaskCode = task.TaskCode,
            TargetType = task.WasteBinId.HasValue ? "Bin" : "Report",
            WasteReportId = task.WasteReportId,
            WasteBinId = task.WasteBinId,
            TargetSummary = targetSummary,
            CollectionReason = task.CollectionReason,
            Status = task.Status,
            ScheduledAt = task.ScheduledAt,
            HandlingNotes = task.HandlingNotes,
            SchedulingReason = task.SchedulingReason,
            CreatedByUserId = task.CreatedByUserId,
            CreatedByUserName = task.CreatedByUser?.FullName ?? task.CreatedByUser?.UserName ?? "Officer",
            CreationMethod = task.CreationMethod,
            CreatedAt = task.CreatedAt,
            UpdatedAt = task.UpdatedAt,
            StatusHistory = statusHistory,
            ScheduleHistory = scheduleHistory
        };
    }

    private static bool IsRoutineDue(
        WasteBin bin,
        DateTime nowUtc,
        TimeZoneInfo tz,
        out DateOnly? routineDueDate)
    {
        routineDueDate = null;

        if (bin.AdministrativeStatus != BinAdministrativeStatus.Active)
        {
            return false;
        }

        if (bin.CollectionWeekdays == null || bin.CollectionWeekdays.Length == 0)
        {
            return false;
        }

        var localNow = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, tz);
        var localToday = DateOnly.FromDateTime(localNow);

        DateOnly startDate;
        if (bin.LastCollectedAt.HasValue)
        {
            var localLastCollected = TimeZoneInfo.ConvertTimeFromUtc(bin.LastCollectedAt.Value, tz);
            var lastCollectedDate = DateOnly.FromDateTime(localLastCollected);

            if (lastCollectedDate >= localToday)
            {
                return false;
            }

            startDate = lastCollectedDate.AddDays(1);
        }
        else
        {
            var localCreated = TimeZoneInfo.ConvertTimeFromUtc(bin.CreatedAt, tz);
            startDate = DateOnly.FromDateTime(localCreated);
        }

        if (startDate > localToday)
        {
            return false;
        }

        var weekdaysSet = bin.CollectionWeekdays.ToHashSet();
        for (var date = startDate; date <= localToday; date = date.AddDays(1))
        {
            var isoWeekday = (int)date.DayOfWeek == 0 ? 7 : (int)date.DayOfWeek;
            if (weekdaysSet.Contains(isoWeekday))
            {
                routineDueDate = date;
                return true;
            }
        }

        return false;
    }

    private TimeZoneInfo ResolveMunicipalityTimeZone()
    {
        var timeZoneId = _configuration?["Municipality:TimeZoneId"];
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            timeZoneId = "Asia/Colombo";
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Sri Lanka Standard Time");
            }
            catch
            {
                return TimeZoneInfo.Utc;
            }
        }
    }

    private static DateTime ConvertMunicipalDateStartToUtc(DateOnly date, TimeZoneInfo municipalityTimeZone)
    {
        var localMidnight = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(localMidnight, municipalityTimeZone);
    }

    private async Task<string> GenerateUniqueTaskCodeAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        var datePart = nowUtc.ToString("yyyyMMdd");
        for (var i = 0; i < 5; i++)
        {
            var suffix = RandomNumberGenerator.GetInt32(1000, 10000).ToString("D4");
            var code = $"TSK-{datePart}-{suffix}";
            var exists = await _db.CollectionTasks.AnyAsync(t => t.TaskCode == code, cancellationToken);
            if (!exists)
            {
                return code;
            }
        }
        return $"TSK-{datePart}-{Guid.NewGuid():N}"[..17].ToUpper();
    }
}
