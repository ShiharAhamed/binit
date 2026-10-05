using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
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
using SmartWaste.Infrastructure.Persistence;

namespace SmartWaste.Infrastructure.Collection.Services;

/// <summary>
/// Infrastructure service implementing IBinObservationService for manual roadside bin observations (Component 2).
/// Enforces WasteOfficer observation recording, staff-only history access, append-only immutability,
/// server-generated audit timestamps, and deterministic chronological ordering.
/// </summary>
public class BinObservationService : IBinObservationService
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _userManager;
    private readonly RecordBinObservationRequestValidator _recordValidator = new();
    private readonly ObservationListQueryValidator _queryValidator = new();

    public BinObservationService(AppDbContext db, UserManager<AppUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // RECORD OBSERVATION (WasteOfficer only)
    // ──────────────────────────────────────────────────────────────────────────

    public async Task<BinObservationDto> RecordObservationAsync(
        Guid binId,
        RecordBinObservationRequest request,
        Guid actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default)
    {
        // 1. Authorization: Only WasteOfficer can record observations in this phase
        if (actorRole != AppRoles.WasteOfficer)
        {
            throw new ForbiddenException("Only Waste Officers can record bin observations.");
        }

        // 2. Request validation
        var validationResult = await _recordValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        // 3. Verify target bin exists
        var bin = await _db.WasteBins
            .FirstOrDefaultAsync(b => b.Id == binId, cancellationToken);

        if (bin is null)
        {
            throw new NotFoundException($"Waste bin '{binId}' was not found.");
        }

        // 4. Precondition check: Cannot record observation for a Retired bin
        if (bin.AdministrativeStatus == BinAdministrativeStatus.Retired)
        {
            throw new BusinessRuleConflictException("Cannot record an observation for a retired waste bin.");
        }

        // 5. Build append-only observation with authoritative server time and actor ID
        var now = DateTime.UtcNow;

        var observation = new BinObservation
        {
            Id = Guid.NewGuid(),
            WasteBinId = bin.Id,
            FillLevelPercent = request.FillLevelPercent!.Value,
            Condition = request.Condition!.Value,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            RecordedByUserId = actorUserId,
            RecordedAt = now
        };

        // Note: Does NOT modify WasteBin.AdministrativeStatus, LastCollectedAt,
        // does NOT create a CollectionTask, and does NOT modify WasteReports.
        _db.BinObservations.Add(observation);
        await _db.SaveChangesAsync(cancellationToken);

        // 6. Resolve observer user name
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == actorUserId, cancellationToken);

        var observerName = user?.FullName ?? user?.UserName ?? "Officer";

        return new BinObservationDto
        {
            Id = observation.Id,
            WasteBinId = observation.WasteBinId,
            FillLevelPercent = observation.FillLevelPercent,
            Condition = observation.Condition,
            Notes = observation.Notes,
            RecordedByUserId = observation.RecordedByUserId,
            RecordedByUserName = observerName,
            RecordedAt = observation.RecordedAt
        };
    }

    // ──────────────────────────────────────────────────────────────────────────
    // OBSERVATION HISTORY (WasteOfficer or MunicipalManager)
    // ──────────────────────────────────────────────────────────────────────────

    public async Task<PagedResult<BinObservationDto>> GetObservationHistoryAsync(
        Guid binId,
        ObservationListQuery query,
        Guid actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default)
    {
        // 1. Authorization: WasteOfficer or MunicipalManager only
        if (actorRole != AppRoles.WasteOfficer && actorRole != AppRoles.MunicipalManager)
        {
            throw new ForbiddenException("Only Waste Officers and Municipal Managers can access observation history.");
        }

        // 2. Request validation
        var validationResult = await _queryValidator.ValidateAsync(query, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        // 3. Verify target bin exists
        var binExists = await _db.WasteBins
            .AnyAsync(b => b.Id == binId, cancellationToken);

        if (!binExists)
        {
            throw new NotFoundException($"Waste bin '{binId}' was not found.");
        }

        var page = query.Page > 0 ? query.Page : 1;
        var pageSize = query.PageSize is > 0 and <= 100 ? query.PageSize : 20;

        // 4. Query observations scoped strictly to target bin
        var baseQuery = _db.BinObservations
            .AsNoTracking()
            .Where(o => o.WasteBinId == binId);

        var totalCount = await baseQuery.CountAsync(cancellationToken);

        // 5. Deterministic ordering: RecordedAt DESC, then Id DESC
        var pagedObservations = await baseQuery
            .OrderByDescending(o => o.RecordedAt)
            .ThenByDescending(o => o.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        // 6. Batch resolve observer names to avoid N+1
        var userIds = pagedObservations
            .Select(o => o.RecordedByUserId)
            .Distinct()
            .ToList();

        var userNames = await ResolveUserNamesAsync(userIds, cancellationToken);

        var items = pagedObservations.Select(o => new BinObservationDto
        {
            Id = o.Id,
            WasteBinId = o.WasteBinId,
            FillLevelPercent = o.FillLevelPercent,
            Condition = o.Condition,
            Notes = o.Notes,
            RecordedByUserId = o.RecordedByUserId,
            RecordedByUserName = userNames.GetValueOrDefault(o.RecordedByUserId),
            RecordedAt = o.RecordedAt
        }).ToList();

        return new PagedResult<BinObservationDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    // ──────────────────────────────────────────────────────────────────────────
    // PRIVATE HELPERS
    // ──────────────────────────────────────────────────────────────────────────

    private async Task<Dictionary<Guid, string>> ResolveUserNamesAsync(
        List<Guid> userIds,
        CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        return await _db.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName ?? u.UserName ?? "Officer", cancellationToken);
    }
}
