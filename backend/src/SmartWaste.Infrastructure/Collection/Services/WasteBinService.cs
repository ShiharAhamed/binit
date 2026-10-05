using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
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
using SmartWaste.Domain.Entities;
using SmartWaste.Domain.Reporting.Enums;
using SmartWaste.Infrastructure.Persistence;

namespace SmartWaste.Infrastructure.Collection.Services;

/// <summary>
/// Infrastructure service implementing IWasteBinService for municipal roadside waste bin management (Component 2).
/// Enforces role-based authorization, invariant constraints, 48-hour observation freshness,
/// public availability derivation, and geospatial proximity calculations.
/// </summary>
public class WasteBinService : IWasteBinService
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _userManager;
    private readonly CreateWasteBinRequestValidator _createValidator = new();
    private readonly UpdateWasteBinRequestValidator _updateValidator = new();
    private readonly DeactivateWasteBinRequestValidator _deactivateValidator = new();
    private readonly PublicWasteBinQueryValidator _publicQueryValidator = new();
    private readonly WasteBinListQueryValidator _internalQueryValidator = new();

    public WasteBinService(AppDbContext db, UserManager<AppUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // BIN REGISTRATION (WasteOfficer only)
    // ──────────────────────────────────────────────────────────────────────────

    public async Task<WasteBinDetailDto> CreateAsync(
        CreateWasteBinRequest request,
        Guid actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default)
    {
        // 1. Authorization
        if (actorRole != AppRoles.WasteOfficer)
        {
            throw new ForbiddenException("Only Waste Officers can register waste bins.");
        }

        // 2. Request validation
        var validationResult = await _createValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var canonicalCode = request.BinCode.Trim().ToUpperInvariant();

        // 3. Unique BinCode guard (early application-level check)
        var exists = await _db.WasteBins
            .AnyAsync(b => b.BinCode.ToUpper() == canonicalCode, cancellationToken);
        if (exists)
        {
            throw new BusinessRuleConflictException($"A waste bin with code '{canonicalCode}' already exists.");
        }

        var now = DateTime.UtcNow;

        // 4. Normalize weekdays (duplicate-free, sorted ISO weekdays 1..7)
        var normalizedWeekdays = request.CollectionWeekdays
            .Distinct()
            .OrderBy(w => w)
            .ToArray();

        // 5. Build entity
        var bin = new WasteBin
        {
            Id = Guid.NewGuid(),
            BinCode = canonicalCode,
            CapacityLiters = request.CapacityLiters!.Value,
            Latitude = request.Latitude!.Value,
            Longitude = request.Longitude!.Value,
            AddressText = string.IsNullOrWhiteSpace(request.AddressText) ? null : request.AddressText.Trim(),
            AdministrativeStatus = BinAdministrativeStatus.Active,
            CollectionWeekdays = normalizedWeekdays,
            LastCollectedAt = null,
            CreatedAt = now,
            UpdatedAt = null
        };

        // 6. Associate accepted waste types (duplicate-free)
        var distinctTypes = request.AcceptedWasteTypes.Distinct().ToList();
        foreach (var wasteType in distinctTypes)
        {
            bin.AcceptedWasteTypes.Add(new WasteBinAcceptedWasteType
            {
                WasteBinId = bin.Id,
                WasteType = wasteType
            });
        }

        _db.WasteBins.Add(bin);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            if (IsBinCodeUniqueViolation(ex))
            {
                throw new BusinessRuleConflictException($"A waste bin with code '{canonicalCode}' already exists.");
            }

            throw;
        }

        return ProjectToDetailDto(bin, latestObservation: null, activeTaskId: null);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // BIN UPDATES (WasteOfficer only)
    // ──────────────────────────────────────────────────────────────────────────

    public async Task<WasteBinDetailDto> UpdateAsync(
        Guid id,
        UpdateWasteBinRequest request,
        Guid actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default)
    {
        // 1. Authorization
        if (actorRole != AppRoles.WasteOfficer)
        {
            throw new ForbiddenException("Only Waste Officers can update waste bins.");
        }

        // 2. Request validation
        var validationResult = await _updateValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        // 3. Load target bin
        var bin = await _db.WasteBins
            .Include(b => b.AcceptedWasteTypes)
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

        if (bin is null)
        {
            throw new NotFoundException($"Waste bin '{id}' was not found.");
        }

        // 4. Precondition check: cannot update a Retired bin
        if (bin.AdministrativeStatus == BinAdministrativeStatus.Retired)
        {
            throw new BusinessRuleConflictException("Cannot update a retired waste bin.");
        }

        // 5. Update permitted metadata
        bin.CapacityLiters = request.CapacityLiters!.Value;
        bin.Latitude = request.Latitude!.Value;
        bin.Longitude = request.Longitude!.Value;
        bin.AddressText = string.IsNullOrWhiteSpace(request.AddressText) ? null : request.AddressText.Trim();
        bin.CollectionWeekdays = request.CollectionWeekdays.Distinct().OrderBy(w => w).ToArray();
        bin.UpdatedAt = DateTime.UtcNow;

        // 6. Synchronize accepted waste types
        var targetTypes = request.AcceptedWasteTypes.Distinct().ToHashSet();
        var existingTypes = bin.AcceptedWasteTypes.Select(a => a.WasteType).ToHashSet();

        // Remove types not in request
        var toRemove = bin.AcceptedWasteTypes.Where(a => !targetTypes.Contains(a.WasteType)).ToList();
        foreach (var item in toRemove)
        {
            bin.AcceptedWasteTypes.Remove(item);
        }

        // Add new types from request
        foreach (var wasteType in targetTypes.Where(t => !existingTypes.Contains(t)))
        {
            bin.AcceptedWasteTypes.Add(new WasteBinAcceptedWasteType
            {
                WasteBinId = bin.Id,
                WasteType = wasteType
            });
        }

        await _db.SaveChangesAsync(cancellationToken);

        // 7. Read latest observation and active task for projection
        var latestObs = await GetLatestObservationAsync(bin.Id, cancellationToken);
        var activeTaskId = await GetActiveTaskIdAsync(bin.Id, cancellationToken);

        return ProjectToDetailDto(bin, latestObs, activeTaskId);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // BIN DEACTIVATION (WasteOfficer only)
    // ──────────────────────────────────────────────────────────────────────────

    public async Task<WasteBinDetailDto> DeactivateAsync(
        Guid id,
        DeactivateWasteBinRequest request,
        Guid actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default)
    {
        // 1. Authorization
        if (actorRole != AppRoles.WasteOfficer)
        {
            throw new ForbiddenException("Only Waste Officers can deactivate waste bins.");
        }

        // 2. Request validation
        var validationResult = await _deactivateValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        // 3. Load target bin
        var bin = await _db.WasteBins
            .Include(b => b.AcceptedWasteTypes)
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

        if (bin is null)
        {
            throw new NotFoundException($"Waste bin '{id}' was not found.");
        }

        // 4. Precondition check: cannot deactivate a bin that is already Retired
        if (bin.AdministrativeStatus == BinAdministrativeStatus.Retired)
        {
            throw new BusinessRuleConflictException("Waste bin is already retired and cannot be modified.");
        }

        // 5. Active task protection: cannot deactivate a bin with an active collection task
        var hasActiveTask = await _db.CollectionTasks
            .AnyAsync(t => t.WasteBinId == bin.Id &&
                          (t.Status == CollectionTaskStatus.Scheduled ||
                           t.Status == CollectionTaskStatus.Assigned ||
                           t.Status == CollectionTaskStatus.InProgress),
                      cancellationToken);

        if (hasActiveTask)
        {
            throw new BusinessRuleConflictException("Cannot deactivate a waste bin with an active collection task (Scheduled, Assigned, or InProgress). The active task must be completed, cancelled, or resolved before deactivating the bin.");
        }

        // 6. Transition status and set UpdatedAt (preserves observations and tasks)
        bin.AdministrativeStatus = request.TargetStatus!.Value;
        bin.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        var latestObs = await GetLatestObservationAsync(bin.Id, cancellationToken);
        var activeTaskId = await GetActiveTaskIdAsync(bin.Id, cancellationToken);

        return ProjectToDetailDto(bin, latestObs, activeTaskId);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // INTERNAL READS (WasteOfficer or MunicipalManager)
    // ──────────────────────────────────────────────────────────────────────────

    public async Task<WasteBinDetailDto> GetInternalByIdAsync(
        Guid id,
        Guid actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default)
    {
        EnforceStaffRole(actorRole);

        var bin = await _db.WasteBins
            .AsNoTracking()
            .Include(b => b.AcceptedWasteTypes)
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

        if (bin is null)
        {
            throw new NotFoundException($"Waste bin '{id}' was not found.");
        }

        var latestObs = await GetLatestObservationAsync(bin.Id, cancellationToken);
        var activeTaskId = await GetActiveTaskIdAsync(bin.Id, cancellationToken);

        return ProjectToDetailDto(bin, latestObs, activeTaskId);
    }

    public async Task<PagedResult<WasteBinSummaryDto>> GetInternalListAsync(
        WasteBinListQuery query,
        Guid actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default)
    {
        EnforceStaffRole(actorRole);

        var validationResult = await _internalQueryValidator.ValidateAsync(query, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var page = query.Page > 0 ? query.Page : 1;
        var pageSize = query.PageSize is > 0 and <= 100 ? query.PageSize : 20;

        IQueryable<WasteBin> baseQuery = _db.WasteBins.AsNoTracking();

        // 1. Status filter
        if (query.Status.HasValue)
        {
            baseQuery = baseQuery.Where(b => b.AdministrativeStatus == query.Status.Value);
        }

        // 2. WasteType filter (via join relationship)
        if (query.WasteType.HasValue)
        {
            baseQuery = baseQuery.Where(b => b.AcceptedWasteTypes.Any(a => a.WasteType == query.WasteType.Value));
        }

        // 3. Search matching BinCode or AddressText
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            baseQuery = baseQuery.Where(b =>
                b.BinCode.ToLower().Contains(search) ||
                (b.AddressText != null && b.AddressText.ToLower().Contains(search)));
        }

        // 4. Condition and MinFillLevel filter based on latest observation
        if (query.Condition.HasValue)
        {
            baseQuery = baseQuery.Where(b =>
                b.Observations
                    .OrderByDescending(o => o.RecordedAt)
                    .ThenByDescending(o => o.Id)
                    .Select(o => (BinCondition?)o.Condition)
                    .FirstOrDefault() == query.Condition.Value);
        }

        if (query.MinFillLevel.HasValue)
        {
            baseQuery = baseQuery.Where(b =>
                b.Observations
                    .OrderByDescending(o => o.RecordedAt)
                    .ThenByDescending(o => o.Id)
                    .Select(o => (int?)o.FillLevelPercent)
                    .FirstOrDefault() >= query.MinFillLevel.Value);
        }

        var totalCount = await baseQuery.CountAsync(cancellationToken);

        // 5. Deterministic sorting: newest CreatedAt first, then Id
        var pagedBins = await baseQuery
            .OrderByDescending(b => b.CreatedAt)
            .ThenBy(b => b.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(b => b.AcceptedWasteTypes)
            .ToListAsync(cancellationToken);

        var binIds = pagedBins.Select(b => b.Id).ToList();

        // 6. Batch load latest observations and active task statuses to avoid N+1
        var latestObservations = await GetLatestObservationsForBinsAsync(binIds, cancellationToken);
        var activeTaskBinIds = await GetActiveTaskBinIdsAsync(binIds, cancellationToken);

        var items = pagedBins.Select(b =>
        {
            latestObservations.TryGetValue(b.Id, out var obs);
            var hasActiveTask = activeTaskBinIds.Contains(b.Id);

            return new WasteBinSummaryDto
            {
                Id = b.Id,
                BinCode = b.BinCode,
                Latitude = b.Latitude,
                Longitude = b.Longitude,
                AddressText = b.AddressText,
                CapacityLiters = b.CapacityLiters,
                AdministrativeStatus = b.AdministrativeStatus,
                AcceptedWasteTypes = b.AcceptedWasteTypes.Select(a => a.WasteType.ToString()).ToList(),
                CollectionWeekdays = b.CollectionWeekdays.ToList(),
                LatestFillLevelPercent = obs?.FillLevelPercent,
                LatestCondition = obs?.Condition,
                LatestObservationAt = obs?.RecordedAt,
                HasActiveTask = hasActiveTask,
                LastCollectedAt = b.LastCollectedAt,
                CreatedAt = b.CreatedAt
            };
        }).ToList();

        return new PagedResult<WasteBinSummaryDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    // ──────────────────────────────────────────────────────────────────────────
    // CITIZEN PUBLIC DISCOVERY (Citizen only)
    // ──────────────────────────────────────────────────────────────────────────

    public async Task<PublicWasteBinDetailDto> GetPublicByIdAsync(
        Guid id,
        Guid actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default)
    {
        EnforceCitizenRole(actorRole);

        var bin = await _db.WasteBins
            .AsNoTracking()
            .Include(b => b.AcceptedWasteTypes)
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

        if (bin is null)
        {
            throw new NotFoundException($"Waste bin '{id}' was not found.");
        }

        var latestObs = await GetLatestObservationAsync(bin.Id, cancellationToken);
        var hasActiveTask = (await GetActiveTaskIdAsync(bin.Id, cancellationToken)).HasValue;

        var availability = ComputePublicAvailability(
            bin.AdministrativeStatus,
            bin.LastCollectedAt,
            latestObs,
            DateTime.UtcNow);

        return new PublicWasteBinDetailDto
        {
            Id = bin.Id,
            BinCode = bin.BinCode,
            Latitude = bin.Latitude,
            Longitude = bin.Longitude,
            AddressText = bin.AddressText,
            CapacityLiters = bin.CapacityLiters,
            AcceptedWasteTypes = bin.AcceptedWasteTypes.Select(a => a.WasteType.ToString()).ToList(),
            PublicAvailability = availability,
            LastObservedAt = latestObs?.RecordedAt,
            IsCollectionScheduled = hasActiveTask
        };
    }

    public async Task<PagedResult<PublicWasteBinDto>> GetPublicListAsync(
        PublicWasteBinQuery query,
        Guid actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default)
    {
        EnforceCitizenRole(actorRole);

        var validationResult = await _publicQueryValidator.ValidateAsync(query, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var page = query.Page > 0 ? query.Page : 1;
        var pageSize = query.PageSize is > 0 and <= 50 ? query.PageSize : 20;

        IQueryable<WasteBin> baseQuery = _db.WasteBins.AsNoTracking();

        // 1. WasteType filter (multi-stream join)
        if (query.WasteType.HasValue)
        {
            baseQuery = baseQuery.Where(b => b.AcceptedWasteTypes.Any(a => a.WasteType == query.WasteType.Value));
        }

        // 2. Geospatial proximity filtering
        var hasCoords = query.Latitude.HasValue && query.Longitude.HasValue;
        var hasRadius = query.RadiusKm.HasValue && hasCoords;

        if (hasRadius)
        {
            // Bounding box pre-filter for database query efficiency
            var radiusKm = query.RadiusKm!.Value;
            var latDelta = radiusKm / 111.0;
            var lonDelta = radiusKm / (111.0 * Math.Cos(query.Latitude!.Value * Math.PI / 180.0));

            var minLat = query.Latitude.Value - latDelta;
            var maxLat = query.Latitude.Value + latDelta;
            var minLon = query.Longitude!.Value - Math.Abs(lonDelta);
            var maxLon = query.Longitude.Value + Math.Abs(lonDelta);

            baseQuery = baseQuery.Where(b =>
                b.Latitude >= minLat && b.Latitude <= maxLat &&
                b.Longitude >= minLon && b.Longitude <= maxLon);
        }

        var matchingBins = await baseQuery
            .Include(b => b.AcceptedWasteTypes)
            .ToListAsync(cancellationToken);

        // 3. Batch load latest observations for availability calculation
        var binIds = matchingBins.Select(b => b.Id).ToList();
        var latestObservations = await GetLatestObservationsForBinsAsync(binIds, cancellationToken);
        var now = DateTime.UtcNow;

        // 4. Calculate exact distances and filter strictly within RadiusKm
        var candidateItems = new List<(WasteBin Bin, double? DistanceMeters, string Availability, DateTime? LastObservedAt)>();

        foreach (var bin in matchingBins)
        {
            latestObservations.TryGetValue(bin.Id, out var obs);

            double? distance = null;
            if (hasCoords)
            {
                distance = CalculateDistanceMeters(
                    query.Latitude!.Value,
                    query.Longitude!.Value,
                    bin.Latitude,
                    bin.Longitude);

                if (hasRadius && distance > query.RadiusKm!.Value * 1000.0)
                {
                    continue; // Exclude outside circle
                }
            }

            var availability = ComputePublicAvailability(
                bin.AdministrativeStatus,
                bin.LastCollectedAt,
                obs,
                now);

            candidateItems.Add((bin, distance, availability, obs?.RecordedAt));
        }

        // 5. Total count after exact geospatial filter
        var totalCount = candidateItems.Count;

        // 6. Ordering: nearest first if coordinates provided; otherwise newest CreatedAt
        IEnumerable<(WasteBin Bin, double? DistanceMeters, string Availability, DateTime? LastObservedAt)> orderedItems = hasCoords
            ? candidateItems.OrderBy(c => c.DistanceMeters ?? double.MaxValue).ThenBy(c => c.Bin.Id)
            : candidateItems.OrderByDescending(c => c.Bin.CreatedAt).ThenBy(c => c.Bin.Id);

        var pagedItems = orderedItems
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new PublicWasteBinDto
            {
                Id = c.Bin.Id,
                BinCode = c.Bin.BinCode,
                Latitude = c.Bin.Latitude,
                Longitude = c.Bin.Longitude,
                AddressText = c.Bin.AddressText,
                CapacityLiters = c.Bin.CapacityLiters,
                AcceptedWasteTypes = c.Bin.AcceptedWasteTypes.Select(a => a.WasteType.ToString()).ToList(),
                PublicAvailability = c.Availability,
                LastObservedAt = c.LastObservedAt,
                DistanceMeters = c.DistanceMeters.HasValue ? Math.Round(c.DistanceMeters.Value, 1) : null
            })
            .ToList();

        return new PagedResult<PublicWasteBinDto>
        {
            Items = pagedItems,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    // ──────────────────────────────────────────────────────────────────────────
    // PUBLIC AVAILABILITY & OBSERVATION FRESHNESS LOGIC
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Computes Citizen public availability per frozen state machine in Section 4.9:
    /// Precedence:
    /// 1. OutOfService / Retired -> Unavailable
    /// 2. Missing observation -> Unknown
    /// 3. LastCollectedAt > RecordedAt (pre-collection observation obsolete) -> Unknown
    /// 4. RecordedAt older than 48 hours (stale observation) -> Unknown
    /// 5. Condition in {Damaged, Blocked, Missing} -> Unavailable
    /// 6. FillLevelPercent >= 100% -> Full (unavailable)
    /// 7. FillLevelPercent >= 75% -> Warning (usable)
    /// 8. FillLevelPercent &lt; 75% -> Usable
    /// </summary>
    public static string ComputePublicAvailability(
        BinAdministrativeStatus administrativeStatus,
        DateTime? lastCollectedAt,
        BinObservation? latestObservation,
        DateTime utcNow)
    {
        if (administrativeStatus != BinAdministrativeStatus.Active)
        {
            return "Unavailable";
        }

        if (latestObservation is null)
        {
            return "Unknown";
        }

        // If bin was collected after the observation was recorded, pre-collection reading is obsolete
        if (lastCollectedAt.HasValue && lastCollectedAt.Value > latestObservation.RecordedAt)
        {
            return "Unknown";
        }

        // Stale observation beyond 48 hours
        if (utcNow - latestObservation.RecordedAt > TimeSpan.FromHours(48))
        {
            return "Unknown";
        }

        // Physical condition inspection
        if (latestObservation.Condition is BinCondition.Damaged or BinCondition.Blocked or BinCondition.Missing)
        {
            return "Unavailable";
        }

        // Discrete fill level evaluation
        if (latestObservation.FillLevelPercent >= 100)
        {
            return "Full";
        }

        if (latestObservation.FillLevelPercent >= 75)
        {
            return "Warning";
        }

        return "Usable";
    }

    // ──────────────────────────────────────────────────────────────────────────
    // GEOSPATIAL PROXIMITY HELPER (Haversine Formula)
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Calculates great-circle distance between two geographic coordinates in meters.
    /// </summary>
    public static double CalculateDistanceMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadiusMeters = 6371000.0;
        var dLat = (lat2 - lat1) * Math.PI / 180.0;
        var dLon = (lon2 - lon1) * Math.PI / 180.0;

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return earthRadiusMeters * c;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // PRIVATE QUERY & PROJECTION HELPERS
    // ──────────────────────────────────────────────────────────────────────────

    private static void EnforceStaffRole(string actorRole)
    {
        if (actorRole != AppRoles.WasteOfficer && actorRole != AppRoles.MunicipalManager)
        {
            throw new ForbiddenException("Only Waste Officers and Municipal Managers can access internal bin records.");
        }
    }

    private static void EnforceCitizenRole(string actorRole)
    {
        if (actorRole != AppRoles.Citizen)
        {
            throw new ForbiddenException("Only authenticated Citizens can access public bin discovery endpoints.");
        }
    }

    private async Task<BinObservation?> GetLatestObservationAsync(Guid binId, CancellationToken cancellationToken)
    {
        return await _db.BinObservations
            .AsNoTracking()
            .Include(o => o.RecordedByUser)
            .Where(o => o.WasteBinId == binId)
            .OrderByDescending(o => o.RecordedAt)
            .ThenByDescending(o => o.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<Guid?> GetActiveTaskIdAsync(Guid binId, CancellationToken cancellationToken)
    {
        return await _db.CollectionTasks
            .AsNoTracking()
            .Where(t => t.WasteBinId == binId &&
                       (t.Status == CollectionTaskStatus.Scheduled ||
                        t.Status == CollectionTaskStatus.Assigned ||
                        t.Status == CollectionTaskStatus.InProgress))
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => (Guid?)t.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<Dictionary<Guid, BinObservation>> GetLatestObservationsForBinsAsync(
        List<Guid> binIds,
        CancellationToken cancellationToken)
    {
        if (binIds.Count == 0)
        {
            return new Dictionary<Guid, BinObservation>();
        }

        // Fetch all observations for the bin batch ordered chronologically
        var observations = await _db.BinObservations
            .AsNoTracking()
            .Where(o => binIds.Contains(o.WasteBinId))
            .OrderByDescending(o => o.RecordedAt)
            .ThenByDescending(o => o.Id)
            .ToListAsync(cancellationToken);

        // Group by WasteBinId and take the first (latest)
        return observations
            .GroupBy(o => o.WasteBinId)
            .ToDictionary(g => g.Key, g => g.First());
    }

    private async Task<HashSet<Guid>> GetActiveTaskBinIdsAsync(
        List<Guid> binIds,
        CancellationToken cancellationToken)
    {
        if (binIds.Count == 0)
        {
            return new HashSet<Guid>();
        }

        var activeBinIds = await _db.CollectionTasks
            .AsNoTracking()
            .Where(t => t.WasteBinId.HasValue &&
                        binIds.Contains(t.WasteBinId.Value) &&
                       (t.Status == CollectionTaskStatus.Scheduled ||
                        t.Status == CollectionTaskStatus.Assigned ||
                        t.Status == CollectionTaskStatus.InProgress))
            .Select(t => t.WasteBinId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        return activeBinIds.ToHashSet();
    }

    private static WasteBinDetailDto ProjectToDetailDto(
        WasteBin bin,
        BinObservation? latestObservation,
        Guid? activeTaskId)
    {
        BinObservationSummaryDto? observationDto = null;
        if (latestObservation is not null)
        {
            observationDto = new BinObservationSummaryDto
            {
                Id = latestObservation.Id,
                FillLevelPercent = latestObservation.FillLevelPercent,
                Condition = latestObservation.Condition,
                Notes = latestObservation.Notes,
                RecordedByUserId = latestObservation.RecordedByUserId,
                RecordedByUserName = latestObservation.RecordedByUser?.FullName
                    ?? latestObservation.RecordedByUser?.UserName,
                RecordedAt = latestObservation.RecordedAt
            };
        }

        return new WasteBinDetailDto
        {
            Id = bin.Id,
            BinCode = bin.BinCode,
            Latitude = bin.Latitude,
            Longitude = bin.Longitude,
            AddressText = bin.AddressText,
            CapacityLiters = bin.CapacityLiters,
            AdministrativeStatus = bin.AdministrativeStatus,
            AcceptedWasteTypes = bin.AcceptedWasteTypes.Select(a => a.WasteType.ToString()).ToList(),
            CollectionWeekdays = bin.CollectionWeekdays.ToList(),
            LastCollectedAt = bin.LastCollectedAt,
            LatestObservation = observationDto,
            HasActiveTask = activeTaskId.HasValue,
            ActiveTaskId = activeTaskId,
            CreatedAt = bin.CreatedAt,
            UpdatedAt = bin.UpdatedAt
        };
    }

    private static bool IsBinCodeUniqueViolation(DbUpdateException ex)
    {
        // 1. Structured PostgreSQL exception inspection
        if (ex.InnerException is PostgresException postgresEx)
        {
            if (postgresEx.SqlState == PostgresErrorCodes.UniqueViolation || postgresEx.SqlState == "23505")
            {
                if (!string.IsNullOrEmpty(postgresEx.ConstraintName))
                {
                    return postgresEx.ConstraintName.Equals("IX_WasteBins_BinCode", StringComparison.OrdinalIgnoreCase) ||
                           postgresEx.ConstraintName.Equals("IX_WasteBins_BinCode_CaseInsensitive", StringComparison.OrdinalIgnoreCase);
                }

                return postgresEx.Message.Contains("IX_WasteBins_BinCode", StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        // 2. Fallback message inspection for wrapped or simulated provider exceptions in tests
        var innerMsg = ex.InnerException?.Message;
        if (!string.IsNullOrEmpty(innerMsg) &&
            (innerMsg.Contains("IX_WasteBins_BinCode", StringComparison.OrdinalIgnoreCase) ||
             innerMsg.Contains("IX_WasteBins_BinCode_CaseInsensitive", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (ex.Message.Contains("IX_WasteBins_BinCode", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("IX_WasteBins_BinCode_CaseInsensitive", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}
