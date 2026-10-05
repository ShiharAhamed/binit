using Microsoft.EntityFrameworkCore;
using SmartWaste.Application.Common.Exceptions;
using SmartWaste.Application.Common.Models;
using SmartWaste.Application.Operations.DTOs.Requests;
using SmartWaste.Application.Operations.DTOs.Responses;
using SmartWaste.Application.Operations.Interfaces;
using SmartWaste.Application.Operations.Queries;
using SmartWaste.Domain.Common;
using SmartWaste.Domain.Operations.Entities;
using SmartWaste.Domain.Operations.Enums;
using SmartWaste.Infrastructure.Persistence;

namespace SmartWaste.Infrastructure.Operations.Services;

/// <summary>
/// Application service implementing Component 4 — Driver Operational Issues Management.
/// Enforces domain rules, state machine transitions (Reported -> InReview -> Resolved),
/// role-scoped access control, server-authoritative driver assignment, and atomic persistence.
/// </summary>
public sealed class OperationalIssueService : IOperationalIssueService
{
    private readonly AppDbContext _db;

    public OperationalIssueService(AppDbContext db)
    {
        _db = db;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 1. CREATE OPERATIONAL ISSUE
    // ──────────────────────────────────────────────────────────────────────────

    public async Task<OperationalIssueDetailDto> CreateAsync(
        CreateOperationalIssueRequest request,
        Guid actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default)
    {
        if (actorRole != AppRoles.Driver)
        {
            throw new ForbiddenException("Only Drivers can report operational issues.");
        }

        var now = DateTime.UtcNow;

        var issue = new OperationalIssue
        {
            Id = Guid.NewGuid(),
            DriverId = actorUserId, // Sourced authoritatively from JWT actor claims
            IssueType = request.IssueType!.Value,
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            LocationDescription = string.IsNullOrWhiteSpace(request.LocationDescription) ? null : request.LocationDescription.Trim(),
            Status = OperationalIssueStatus.Reported,
            ResolvedByUserId = null,
            ResolvedAt = null,
            ResolutionNote = null,
            CreatedAt = now,
            UpdatedAt = null
        };

        _db.OperationalIssues.Add(issue);
        await _db.SaveChangesAsync(cancellationToken);

        return await BuildDetailDtoAsync(issue, cancellationToken);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 2. GET OPERATIONAL ISSUE BY ID
    // ──────────────────────────────────────────────────────────────────────────

    public async Task<OperationalIssueDetailDto> GetByIdAsync(
        Guid issueId,
        Guid actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default)
    {
        EnforceNotCitizen(actorRole);

        var issue = await _db.OperationalIssues
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == issueId, cancellationToken);

        if (issue is null)
        {
            throw new NotFoundException($"Operational issue '{issueId}' was not found.");
        }

        EnforceDriverOwnership(actorRole, actorUserId, issue.DriverId);

        return await BuildDetailDtoAsync(issue, cancellationToken);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 3. GET MINE (DRIVER'S OWN ISSUES)
    // ──────────────────────────────────────────────────────────────────────────

    public async Task<PagedResult<OperationalIssueSummaryDto>> GetMineAsync(
        OperationalIssueListQuery query,
        Guid actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default)
    {
        if (actorRole != AppRoles.Driver)
        {
            throw new ForbiddenException("Only Drivers can access their own operational issues list.");
        }

        return await ExecuteListQueryAsync(query, driverIdScope: actorUserId, cancellationToken);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 4. GET STAFF LIST (ALL ISSUES FOR OFFICERS / MANAGERS)
    // ──────────────────────────────────────────────────────────────────────────

    public async Task<PagedResult<OperationalIssueSummaryDto>> GetListAsync(
        OperationalIssueListQuery query,
        Guid actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default)
    {
        if (actorRole != AppRoles.WasteOfficer && actorRole != AppRoles.MunicipalManager)
        {
            throw new ForbiddenException("Only Waste Officers and Municipal Managers can access the staff operational issues list.");
        }

        return await ExecuteListQueryAsync(query, driverIdScope: null, cancellationToken);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 5. START REVIEW (Reported -> InReview)
    // ──────────────────────────────────────────────────────────────────────────

    public async Task<OperationalIssueDetailDto> StartReviewAsync(
        Guid issueId,
        Guid actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default)
    {
        if (actorRole != AppRoles.WasteOfficer && actorRole != AppRoles.MunicipalManager)
        {
            throw new ForbiddenException("Only Waste Officers or Municipal Managers can initiate review of operational issues.");
        }

        var issue = await _db.OperationalIssues
            .FirstOrDefaultAsync(i => i.Id == issueId, cancellationToken);

        if (issue is null)
        {
            throw new NotFoundException($"Operational issue '{issueId}' was not found.");
        }

        if (issue.Status != OperationalIssueStatus.Reported)
        {
            throw new BusinessRuleConflictException(
                $"Cannot start review. Operational issue is currently '{issue.Status}'. Only Reported issues can be moved to InReview.");
        }

        var now = DateTime.UtcNow;
        issue.Status = OperationalIssueStatus.InReview;
        issue.UpdatedAt = now;

        await _db.SaveChangesAsync(cancellationToken);

        return await BuildDetailDtoAsync(issue, cancellationToken);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 6. RESOLVE OPERATIONAL ISSUE (InReview -> Resolved)
    // ──────────────────────────────────────────────────────────────────────────

    public async Task<OperationalIssueDetailDto> ResolveAsync(
        Guid issueId,
        ResolveOperationalIssueRequest request,
        Guid actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default)
    {
        if (actorRole != AppRoles.WasteOfficer && actorRole != AppRoles.MunicipalManager)
        {
            throw new ForbiddenException("Only Waste Officers or Municipal Managers can resolve operational issues.");
        }

        var issue = await _db.OperationalIssues
            .FirstOrDefaultAsync(i => i.Id == issueId, cancellationToken);

        if (issue is null)
        {
            throw new NotFoundException($"Operational issue '{issueId}' was not found.");
        }

        if (issue.Status != OperationalIssueStatus.InReview)
        {
            throw new BusinessRuleConflictException(
                $"Cannot resolve operational issue. Current status is '{issue.Status}'. Only InReview issues can be resolved.");
        }

        if (string.IsNullOrWhiteSpace(request.ResolutionNote))
        {
            throw new ArgumentException("Resolution note is required and cannot be empty.", nameof(request));
        }

        var now = DateTime.UtcNow;
        issue.Status = OperationalIssueStatus.Resolved;
        issue.ResolvedByUserId = actorUserId;
        issue.ResolvedAt = now;
        issue.ResolutionNote = request.ResolutionNote.Trim();
        issue.UpdatedAt = now;

        await _db.SaveChangesAsync(cancellationToken);

        return await BuildDetailDtoAsync(issue, cancellationToken);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // PRIVATE QUERY & MAPPING HELPERS
    // ──────────────────────────────────────────────────────────────────────────

    private async Task<PagedResult<OperationalIssueSummaryDto>> ExecuteListQueryAsync(
        OperationalIssueListQuery query,
        Guid? driverIdScope,
        CancellationToken cancellationToken)
    {
        var page = query.Page > 0 ? query.Page : 1;
        var pageSize = query.PageSize is > 0 and <= 100 ? query.PageSize : 20;

        IQueryable<OperationalIssue> baseQuery = _db.OperationalIssues.AsNoTracking();

        // 1. Ownership scope if specified
        if (driverIdScope.HasValue)
        {
            baseQuery = baseQuery.Where(i => i.DriverId == driverIdScope.Value);
        }

        // 2. Status filter
        if (query.Status.HasValue)
        {
            baseQuery = baseQuery.Where(i => i.Status == query.Status.Value);
        }

        // 3. IssueType filter
        if (query.IssueType.HasValue)
        {
            baseQuery = baseQuery.Where(i => i.IssueType == query.IssueType.Value);
        }

        // 4. Case-insensitive search across Title and Description
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            baseQuery = baseQuery.Where(i =>
                i.Title.ToLower().Contains(search) ||
                i.Description.ToLower().Contains(search));
        }

        // 5. Total count before pagination
        var totalCount = await baseQuery.CountAsync(cancellationToken);

        // 6. Ordering (safe explicit switch, deterministic secondary order on Id)
        var sortDir = query.SortDirection?.ToLowerInvariant() ?? "desc";
        baseQuery = (query.SortBy?.ToLowerInvariant() ?? "createdat") switch
        {
            "updatedat" => sortDir == "asc"
                ? baseQuery.OrderBy(i => i.UpdatedAt).ThenBy(i => i.Id)
                : baseQuery.OrderByDescending(i => i.UpdatedAt).ThenByDescending(i => i.Id),
            _ => sortDir == "asc"
                ? baseQuery.OrderBy(i => i.CreatedAt).ThenBy(i => i.Id)
                : baseQuery.OrderByDescending(i => i.CreatedAt).ThenByDescending(i => i.Id)
        };

        // 7. Pagination
        var pagedIssues = await baseQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        // 8. Resolve Driver names in batch to avoid N+1 queries
        var driverIds = pagedIssues.Select(i => i.DriverId).Distinct().ToList();
        var driverNames = await ResolveUserNamesAsync(driverIds, cancellationToken);

        // 9. Project to summary DTOs
        var items = pagedIssues.Select(i => new OperationalIssueSummaryDto
        {
            Id = i.Id,
            DriverId = i.DriverId,
            DriverName = driverNames.GetValueOrDefault(i.DriverId),
            IssueType = i.IssueType,
            Title = i.Title,
            Status = i.Status,
            Latitude = i.Latitude,
            Longitude = i.Longitude,
            CreatedAt = i.CreatedAt,
            UpdatedAt = i.UpdatedAt
        }).ToList();

        return new PagedResult<OperationalIssueSummaryDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    private async Task<OperationalIssueDetailDto> BuildDetailDtoAsync(
        OperationalIssue issue,
        CancellationToken cancellationToken)
    {
        var driverName = await ResolveUserNameAsync(issue.DriverId, cancellationToken);

        string? resolvedByUserName = null;
        if (issue.ResolvedByUserId.HasValue)
        {
            resolvedByUserName = await ResolveUserNameAsync(issue.ResolvedByUserId.Value, cancellationToken);
        }

        return new OperationalIssueDetailDto
        {
            Id = issue.Id,
            DriverId = issue.DriverId,
            DriverName = driverName,
            IssueType = issue.IssueType,
            Title = issue.Title,
            Description = issue.Description,
            Latitude = issue.Latitude,
            Longitude = issue.Longitude,
            LocationDescription = issue.LocationDescription,
            Status = issue.Status,
            ResolutionNote = issue.ResolutionNote,
            ResolvedAt = issue.ResolvedAt,
            ResolvedByUserId = issue.ResolvedByUserId,
            ResolvedByUserName = resolvedByUserName,
            CreatedAt = issue.CreatedAt,
            UpdatedAt = issue.UpdatedAt
        };
    }

    private async Task<string?> ResolveUserNameAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.FullName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<Dictionary<Guid, string?>> ResolveUserNamesAsync(
        IReadOnlyList<Guid> userIds,
        CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<Guid, string?>();
        }

        return await _db.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName })
            .ToDictionaryAsync(u => u.Id, u => (string?)u.FullName, cancellationToken);
    }

    private static void EnforceNotCitizen(string actorRole)
    {
        if (actorRole == AppRoles.Citizen)
        {
            throw new ForbiddenException("Citizens do not have access to operational issues.");
        }
    }

    private static void EnforceDriverOwnership(string actorRole, Guid actorUserId, Guid issueDriverId)
    {
        if (actorRole == AppRoles.Driver && issueDriverId != actorUserId)
        {
            throw new ForbiddenException("You can only access your own operational issues.");
        }
    }
}
