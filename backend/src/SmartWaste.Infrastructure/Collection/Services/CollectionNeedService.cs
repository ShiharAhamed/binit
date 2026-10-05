using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
/// Infrastructure query service implementing ICollectionNeedService (Component 2).
/// Computes the unified, derived, read-only collection needs queue synthesized dynamically from:
/// - Source A: Verified citizen WasteReports (Status == Verified, no active task)
/// - Source B: Active WasteBins with latest observation 100% full or Blocked (no active task)
/// - Source C: Active WasteBins due for routine collection on configured weekdays (no active task)
/// Strictly read-only: performs zero writes, task creations, or status transitions.
/// </summary>
public class CollectionNeedService : ICollectionNeedService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly CollectionNeedListQueryValidator _queryValidator = new();

    public CollectionNeedService(AppDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    public async Task<PagedResult<CollectionNeedItemDto>> GetCollectionNeedsAsync(
        CollectionNeedListQuery query,
        Guid actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default)
    {
        // 1. Authorization: WasteOfficer or MunicipalManager only
        if (actorRole != AppRoles.WasteOfficer && actorRole != AppRoles.MunicipalManager)
        {
            throw new ForbiddenException("Only Waste Officers and Municipal Managers can access the collection needs queue.");
        }

        // 2. Request validation
        var validationResult = await _queryValidator.ValidateAsync(query, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        return await GetCollectionNeedsCoreAsync(query, targetDate: null, cancellationToken);
    }

    /// <summary>
    /// Retrieves the safe, bounded collection-needs projection used exclusively by the internal AI tool.
    /// The controller's InternalServicePolicy authorizes this path; this service only reuses the
    /// authoritative derivation rules and maps their result to an allow-listed DTO.
    /// </summary>
    public async Task<PagedResult<CollectionNeedToolItemDto>> GetCollectionNeedsForAiAsync(
        GetCollectionNeedsForAiQuery query,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await new GetCollectionNeedsForAiQueryValidator()
            .ValidateAsync(query, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var derivedNeeds = await GetCollectionNeedsCoreAsync(
            new CollectionNeedListQuery
            {
                TargetType = NormalizeTargetType(query.TargetType),
                CollectionReason = NormalizeCollectionReason(query.CollectionReason),
                Page = query.Page,
                PageSize = query.PageSize
            },
            query.TargetDate,
            cancellationToken);

        return new PagedResult<CollectionNeedToolItemDto>
        {
            Items = derivedNeeds.Items.Select(MapAiToolItem).ToList(),
            Page = derivedNeeds.Page,
            PageSize = derivedNeeds.PageSize,
            TotalCount = derivedNeeds.TotalCount
        };
    }

    private async Task<PagedResult<CollectionNeedItemDto>> GetCollectionNeedsCoreAsync(
        CollectionNeedListQuery query,
        DateOnly? targetDate,
        CancellationToken cancellationToken)
    {

        var nowUtc = DateTime.UtcNow;
        var municipalityTimeZone = ResolveMunicipalityTimeZone();
        var routineEvaluationDate = targetDate ?? DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTimeFromUtc(nowUtc, municipalityTimeZone));

        // 3. Active Task Suppression: Identify reports and bins with an active task (Scheduled, Assigned, InProgress)
        var activeTasks = await _db.CollectionTasks
            .AsNoTracking()
            .Where(t => t.Status == CollectionTaskStatus.Scheduled ||
                        t.Status == CollectionTaskStatus.Assigned ||
                        t.Status == CollectionTaskStatus.InProgress)
            .Select(t => new { t.WasteReportId, t.WasteBinId })
            .ToListAsync(cancellationToken);

        var activeReportIds = activeTasks
            .Where(t => t.WasteReportId.HasValue)
            .Select(t => t.WasteReportId!.Value)
            .ToHashSet();

        var activeBinIds = activeTasks
            .Where(t => t.WasteBinId.HasValue)
            .Select(t => t.WasteBinId!.Value)
            .ToHashSet();

        var allItems = new List<CollectionNeedItemDto>();

        // 4. Source A: Verified Citizen Waste Reports
        var shouldIncludeReports = string.IsNullOrEmpty(query.TargetType) ||
                                  query.TargetType.Equals("Report", StringComparison.OrdinalIgnoreCase);
        var shouldIncludeVerifiedReason = string.IsNullOrEmpty(query.CollectionReason) ||
                                         query.CollectionReason.Equals("VerifiedReport", StringComparison.OrdinalIgnoreCase);

        if (shouldIncludeReports && shouldIncludeVerifiedReason)
        {
            var reportsQuery = _db.WasteReports
                .AsNoTracking()
                .Include(r => r.Attachments)
                .Where(r => r.Status == WasteReportStatus.Verified && !activeReportIds.Contains(r.Id));

            if (query.WasteType.HasValue)
            {
                reportsQuery = reportsQuery.Where(r => r.WasteType == query.WasteType.Value);
            }

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var search = query.Search.Trim().ToLower();
                reportsQuery = reportsQuery.Where(r =>
                    (r.AddressText != null && r.AddressText.ToLower().Contains(search)) ||
                    r.Description.ToLower().Contains(search));
            }

            var verifiedReports = await reportsQuery.ToListAsync(cancellationToken);

            foreach (var report in verifiedReports)
            {
                var titleText = !string.IsNullOrWhiteSpace(report.AddressText)
                    ? report.AddressText
                    : (report.Description.Length > 40 ? report.Description[..40] + "..." : report.Description);

                allItems.Add(new CollectionNeedItemDto
                {
                    Id = report.Id,
                    TargetType = "Report",
                    CollectionReason = "VerifiedReport",
                    Title = $"Report {SmartWaste.Application.Reporting.WasteReportReference.FromId(report.Id)}: {titleText}",
                    Latitude = report.Latitude,
                    Longitude = report.Longitude,
                    AddressText = report.AddressText,
                    WasteTypes = new[] { report.WasteType.ToString() },
                    Urgency = report.Priority.HasValue ? report.Priority.Value.ToString() : "High",
                    TriggerDate = report.VerifiedAt ?? report.CreatedAt,
                    AttachmentCount = report.Attachments?.Count ?? 0,
                    BinDetails = null
                });
            }
        }

        // 5. Sources B & C: Bins Requiring Collection (Full/Blocked or Routine Due)
        var shouldIncludeBins = string.IsNullOrEmpty(query.TargetType) ||
                               query.TargetType.Equals("Bin", StringComparison.OrdinalIgnoreCase);
        var canIncludeFullOrBlocked = string.IsNullOrEmpty(query.CollectionReason) ||
                                      query.CollectionReason.Equals("FullOrBlockedBin", StringComparison.OrdinalIgnoreCase);
        var canIncludeRoutine = string.IsNullOrEmpty(query.CollectionReason) ||
                                query.CollectionReason.Equals("RoutineCollection", StringComparison.OrdinalIgnoreCase);

        if (shouldIncludeBins && (canIncludeFullOrBlocked || canIncludeRoutine))
        {
            var binsQuery = _db.WasteBins
                .AsNoTracking()
                .Include(b => b.AcceptedWasteTypes)
                .Where(b => b.AdministrativeStatus == BinAdministrativeStatus.Active && !activeBinIds.Contains(b.Id));

            if (query.WasteType.HasValue)
            {
                binsQuery = binsQuery.Where(b => b.AcceptedWasteTypes.Any(awt => awt.WasteType == query.WasteType.Value));
            }

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var search = query.Search.Trim().ToLower();
                binsQuery = binsQuery.Where(b =>
                    b.BinCode.ToLower().Contains(search) ||
                    (b.AddressText != null && b.AddressText.ToLower().Contains(search)));
            }

            var candidateBins = await binsQuery.ToListAsync(cancellationToken);

            if (candidateBins.Count > 0)
            {
                var binIds = candidateBins.Select(b => b.Id).ToList();

                // Fetch observations for candidate bins, newest first
                var observations = await _db.BinObservations
                    .AsNoTracking()
                    .Where(o => binIds.Contains(o.WasteBinId))
                    .OrderByDescending(o => o.RecordedAt)
                    .ThenByDescending(o => o.Id)
                    .ToListAsync(cancellationToken);

                var latestObsByBinId = observations
                    .GroupBy(o => o.WasteBinId)
                    .ToDictionary(g => g.Key, g => g.First());

                foreach (var bin in candidateBins)
                {
                    latestObsByBinId.TryGetValue(bin.Id, out var latestObs);

                    // Evaluate Source B: 100% Full or Blocked
                    var isSourceBQualified = false;
                    if (latestObs != null)
                    {
                        var isPreCollection = bin.LastCollectedAt.HasValue && bin.LastCollectedAt.Value >= latestObs.RecordedAt;
                        var isStale = (nowUtc - latestObs.RecordedAt).TotalHours > 48.0;
                        var isDamagedOrMissing = latestObs.Condition == BinCondition.Damaged || latestObs.Condition == BinCondition.Missing;

                        if (!isPreCollection && !isStale && !isDamagedOrMissing)
                        {
                            if (latestObs.FillLevelPercent == 100 || latestObs.Condition == BinCondition.Blocked)
                            {
                                isSourceBQualified = true;
                            }
                        }
                    }

                    // Evaluate Source C: Routine Collection Due
                    var isSourceCQualified = IsRoutineDue(bin, routineEvaluationDate, municipalityTimeZone, out var routineDueDate);

                    // Overlapping Bin Needs Resolution:
                    // When querying unified queue (query.CollectionReason == null), acute need (Source B) takes precedence.
                    // When filtered by query.CollectionReason, match the requested reason.
                    var includeAsSourceB = isSourceBQualified && canIncludeFullOrBlocked;
                    var includeAsSourceC = isSourceCQualified && canIncludeRoutine;

                    if (includeAsSourceB && includeAsSourceC)
                    {
                        if (string.Equals(query.CollectionReason, "RoutineCollection", StringComparison.OrdinalIgnoreCase))
                        {
                            allItems.Add(CreateRoutineCollectionItem(bin, routineDueDate!.Value, latestObs, nowUtc, municipalityTimeZone));
                        }
                        else
                        {
                            // In unified view or FullOrBlockedBin filter, prioritize FullOrBlockedBin
                            allItems.Add(CreateFullOrBlockedItem(bin, latestObs!, nowUtc));
                        }
                    }
                    else if (includeAsSourceB)
                    {
                        allItems.Add(CreateFullOrBlockedItem(bin, latestObs!, nowUtc));
                    }
                    else if (includeAsSourceC)
                    {
                        allItems.Add(CreateRoutineCollectionItem(bin, routineDueDate!.Value, latestObs, nowUtc, municipalityTimeZone));
                    }
                }
            }
        }

        // 6. Sorting: Urgency (Urgent/High before Medium/Low), then TriggerDate ASC, then Id ASC (tie-breaker)
        var sortedItems = allItems
            .OrderBy(item => GetUrgencyRank(item.Urgency))
            .ThenBy(item => item.TriggerDate)
            .ThenBy(item => item.Id)
            .ToList();

        var totalCount = sortedItems.Count;
        var page = query.Page > 0 ? query.Page : 1;
        var pageSize = query.PageSize is > 0 and <= 100 ? query.PageSize : 20;

        var pagedItems = sortedItems
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new PagedResult<CollectionNeedItemDto>
        {
            Items = pagedItems,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    // ──────────────────────────────────────────────────────────────────────────
    // PROJECTION HELPERS
    // ──────────────────────────────────────────────────────────────────────────

    private static CollectionNeedToolItemDto MapAiToolItem(CollectionNeedItemDto need)
    {
        var isReport = string.Equals(need.TargetType, "Report", StringComparison.Ordinal);
        return new CollectionNeedToolItemDto
        {
            Id = need.Id,
            TargetType = need.TargetType,
            WasteReportId = isReport ? need.Id : null,
            WasteBinId = isReport ? null : need.Id,
            CollectionReason = need.CollectionReason,
            Latitude = need.Latitude,
            Longitude = need.Longitude,
            AddressText = need.AddressText,
            WasteTypes = need.WasteTypes,
            Urgency = need.Urgency,
            TriggerDate = need.TriggerDate,
            BinTelemetry = need.BinDetails == null
                ? null
                : new CollectionNeedBinTelemetryDto
                {
                    BinCode = need.BinDetails.BinCode,
                    CapacityLiters = need.BinDetails.CapacityLiters,
                    LatestFillLevelPercent = need.BinDetails.LatestFillLevelPercent,
                    LatestCondition = need.BinDetails.LatestCondition,
                    ObservationAgeHours = need.BinDetails.ObservationAgeHours
                }
        };
    }

    private static string? NormalizeTargetType(string? targetType) => targetType?.ToLowerInvariant() switch
    {
        "report" => "Report",
        "bin" => "Bin",
        _ => null
    };

    private static string? NormalizeCollectionReason(string? collectionReason) => collectionReason?.ToLowerInvariant() switch
    {
        "verifiedreport" => "VerifiedReport",
        "fullorblockedbin" => "FullOrBlockedBin",
        "routinecollection" => "RoutineCollection",
        _ => null
    };

    private static CollectionNeedItemDto CreateFullOrBlockedItem(
        WasteBin bin,
        BinObservation latestObs,
        DateTime nowUtc)
    {
        var title = latestObs.FillLevelPercent == 100 && latestObs.Condition == BinCondition.Blocked
            ? $"{bin.BinCode} (100% Full, Blocked)"
            : latestObs.FillLevelPercent == 100
                ? $"{bin.BinCode} (100% Full)"
                : $"{bin.BinCode} (Blocked)";

        return new CollectionNeedItemDto
        {
            Id = bin.Id,
            TargetType = "Bin",
            CollectionReason = "FullOrBlockedBin",
            Title = title,
            Latitude = bin.Latitude,
            Longitude = bin.Longitude,
            AddressText = bin.AddressText,
            WasteTypes = bin.AcceptedWasteTypes.Select(awt => awt.WasteType.ToString()).ToList(),
            Urgency = "High",
            TriggerDate = latestObs.RecordedAt,
            AttachmentCount = 0,
            BinDetails = new CollectionNeedBinDetailsDto
            {
                BinCode = bin.BinCode,
                CapacityLiters = bin.CapacityLiters,
                LatestFillLevelPercent = latestObs.FillLevelPercent,
                LatestCondition = latestObs.Condition,
                ObservationAgeHours = Math.Round((nowUtc - latestObs.RecordedAt).TotalHours, 1)
            }
        };
    }

    private static CollectionNeedItemDto CreateRoutineCollectionItem(
        WasteBin bin,
        DateOnly routineDueDate,
        BinObservation? latestObs,
        DateTime nowUtc,
        TimeZoneInfo municipalityTimeZone)
    {
        var triggerDateUtc = TimeZoneInfo.ConvertTimeToUtc(
            routineDueDate.ToDateTime(TimeOnly.MinValue),
            municipalityTimeZone);

        return new CollectionNeedItemDto
        {
            Id = bin.Id,
            TargetType = "Bin",
            CollectionReason = "RoutineCollection",
            Title = $"{bin.BinCode} (Routine Collection Due)",
            Latitude = bin.Latitude,
            Longitude = bin.Longitude,
            AddressText = bin.AddressText,
            WasteTypes = bin.AcceptedWasteTypes.Select(awt => awt.WasteType.ToString()).ToList(),
            Urgency = "Medium",
            TriggerDate = triggerDateUtc,
            AttachmentCount = 0,
            BinDetails = new CollectionNeedBinDetailsDto
            {
                BinCode = bin.BinCode,
                CapacityLiters = bin.CapacityLiters,
                LatestFillLevelPercent = latestObs?.FillLevelPercent,
                LatestCondition = latestObs?.Condition,
                ObservationAgeHours = latestObs != null
                    ? Math.Round((nowUtc - latestObs.RecordedAt).TotalHours, 1)
                    : null
            }
        };
    }

    // ──────────────────────────────────────────────────────────────────────────
    // ROUTINE DUE CALCULATION ALGORITHM
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Computes whether an active bin is due for routine collection per Section 4.8 of database-schema.md.
    /// Operates in the municipality's configured local calendar.
    /// </summary>
    private static bool IsRoutineDue(
        WasteBin bin,
        DateOnly localToday,
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

        DateOnly startDate;
        if (bin.LastCollectedAt.HasValue)
        {
            var localLastCollected = TimeZoneInfo.ConvertTimeFromUtc(bin.LastCollectedAt.Value, tz);
            var lastCollectedDate = DateOnly.FromDateTime(localLastCollected);

            // If collected today or on a future date in local calendar, not due today
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

    private static int GetUrgencyRank(string urgency) => urgency switch
    {
        "Urgent" => 0,
        "High" => 1,
        "Medium" => 2,
        "Low" => 3,
        _ => 4
    };

    private TimeZoneInfo ResolveMunicipalityTimeZone()
    {
        var timeZoneId = _configuration["Municipality:TimeZoneId"];
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
}
