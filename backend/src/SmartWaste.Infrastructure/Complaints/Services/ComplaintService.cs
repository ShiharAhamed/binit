using Microsoft.EntityFrameworkCore;
using SmartWaste.Application.Common.Exceptions;
using SmartWaste.Application.Common.Models;
using SmartWaste.Application.Complaints.DTOs.Requests;
using SmartWaste.Application.Complaints.DTOs.Responses;
using SmartWaste.Application.Complaints.Interfaces;
using SmartWaste.Application.Complaints.Queries;
using SmartWaste.Domain.Common;
using SmartWaste.Domain.Complaints.Entities;
using SmartWaste.Domain.Complaints.Enums;
using SmartWaste.Infrastructure.Persistence;

namespace SmartWaste.Infrastructure.Complaints.Services;

/// <summary>
/// Application service implementing Component 4 — Citizen Complaints Management.
/// Enforces business rules, state machine transitions (Submitted -> InReview -> Resolved),
/// role-scoped access control, server-authoritative actor assignment, and atomic persistence.
/// </summary>
public sealed class ComplaintService : IComplaintService
{
    private readonly AppDbContext _db;

    public ComplaintService(AppDbContext db)
    {
        _db = db;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // CREATE COMPLAINT
    // ──────────────────────────────────────────────────────────────────────────

    public async Task<ComplaintDetailDto> CreateAsync(
        CreateComplaintRequest request,
        Guid actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default)
    {
        if (actorRole != AppRoles.Citizen)
        {
            throw new ForbiddenException("Only Citizens can create complaints.");
        }

        var now = DateTime.UtcNow;

        var complaint = new Complaint
        {
            Id = Guid.NewGuid(),
            CitizenId = actorUserId, // Sourced authoritatively from JWT actor claims
            Category = request.Category!.Value,
            Subject = request.Subject.Trim(),
            Description = request.Description.Trim(),
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            LocationDescription = string.IsNullOrWhiteSpace(request.LocationDescription) ? null : request.LocationDescription.Trim(),
            Status = ComplaintStatus.Submitted,
            ResolvedByUserId = null,
            ResolvedAt = null,
            ResolutionNote = null,
            CreatedAt = now,
            UpdatedAt = null
        };

        _db.Complaints.Add(complaint);
        await _db.SaveChangesAsync(cancellationToken);

        return await BuildDetailDtoAsync(complaint, cancellationToken);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // GET COMPLAINT BY ID
    // ──────────────────────────────────────────────────────────────────────────

    public async Task<ComplaintDetailDto> GetByIdAsync(
        Guid complaintId,
        Guid actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default)
    {
        EnforceNotDriver(actorRole);

        var complaint = await _db.Complaints
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == complaintId, cancellationToken);

        if (complaint is null)
        {
            throw new NotFoundException($"Complaint '{complaintId}' was not found.");
        }

        EnforceCitizenOwnership(actorRole, actorUserId, complaint.CitizenId);

        return await BuildDetailDtoAsync(complaint, cancellationToken);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // LIST COMPLAINTS
    // ──────────────────────────────────────────────────────────────────────────

    public async Task<PagedResult<ComplaintSummaryDto>> GetListAsync(
        ComplaintListQuery query,
        Guid actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default)
    {
        EnforceNotDriver(actorRole);

        var page = query.Page > 0 ? query.Page : 1;
        var pageSize = query.PageSize is > 0 and <= 100 ? query.PageSize : 20;

        IQueryable<Complaint> baseQuery = _db.Complaints.AsNoTracking();

        // 1. Role scope (Citizens see only their own complaints; staff see all)
        if (actorRole == AppRoles.Citizen)
        {
            baseQuery = baseQuery.Where(c => c.CitizenId == actorUserId);
        }

        // 2. Status filter
        if (query.Status.HasValue)
        {
            baseQuery = baseQuery.Where(c => c.Status == query.Status.Value);
        }

        // 3. Category filter
        if (query.Category.HasValue)
        {
            baseQuery = baseQuery.Where(c => c.Category == query.Category.Value);
        }

        // 4. Case-insensitive search across Subject and Description
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            baseQuery = baseQuery.Where(c =>
                c.Subject.ToLower().Contains(search) ||
                c.Description.ToLower().Contains(search));
        }

        // 5. Total count before pagination
        var totalCount = await baseQuery.CountAsync(cancellationToken);

        // 6. Ordering (safe explicit switch, deterministic secondary order on Id)
        var sortDir = query.SortDirection?.ToLowerInvariant() ?? "desc";
        baseQuery = (query.SortBy?.ToLowerInvariant() ?? "createdat") switch
        {
            "updatedat" => sortDir == "asc"
                ? baseQuery.OrderBy(c => c.UpdatedAt).ThenBy(c => c.Id)
                : baseQuery.OrderByDescending(c => c.UpdatedAt).ThenByDescending(c => c.Id),
            _ => sortDir == "asc"
                ? baseQuery.OrderBy(c => c.CreatedAt).ThenBy(c => c.Id)
                : baseQuery.OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.Id)
        };

        // 7. Pagination
        var pagedComplaints = await baseQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        // 8. Resolve Citizen names in batch to avoid N+1 queries
        var citizenIds = pagedComplaints.Select(c => c.CitizenId).Distinct().ToList();
        var citizenNames = await ResolveUserNamesAsync(citizenIds, cancellationToken);

        // 9. Project to summary DTOs
        var items = pagedComplaints.Select(c => new ComplaintSummaryDto
        {
            Id = c.Id,
            CitizenId = c.CitizenId,
            CitizenName = citizenNames.GetValueOrDefault(c.CitizenId),
            Category = c.Category,
            Subject = c.Subject,
            Status = c.Status,
            Latitude = c.Latitude,
            Longitude = c.Longitude,
            CreatedAt = c.CreatedAt,
            UpdatedAt = c.UpdatedAt
        }).ToList();

        return new PagedResult<ComplaintSummaryDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    // ──────────────────────────────────────────────────────────────────────────
    // START REVIEW (Submitted -> InReview)
    // ──────────────────────────────────────────────────────────────────────────

    public async Task<ComplaintDetailDto> StartReviewAsync(
        Guid complaintId,
        Guid actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default)
    {
        if (actorRole != AppRoles.WasteOfficer && actorRole != AppRoles.MunicipalManager)
        {
            throw new ForbiddenException("Only Waste Officers or Municipal Managers can initiate complaint review.");
        }

        var complaint = await _db.Complaints
            .FirstOrDefaultAsync(c => c.Id == complaintId, cancellationToken);

        if (complaint is null)
        {
            throw new NotFoundException($"Complaint '{complaintId}' was not found.");
        }

        if (complaint.Status != ComplaintStatus.Submitted)
        {
            throw new BusinessRuleConflictException(
                $"Cannot start review. Complaint is currently '{complaint.Status}'. Only Submitted complaints can be moved to InReview.");
        }

        var now = DateTime.UtcNow;
        complaint.Status = ComplaintStatus.InReview;
        complaint.UpdatedAt = now;

        await _db.SaveChangesAsync(cancellationToken);

        return await BuildDetailDtoAsync(complaint, cancellationToken);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // RESOLVE COMPLAINT (InReview -> Resolved)
    // ──────────────────────────────────────────────────────────────────────────

    public async Task<ComplaintDetailDto> ResolveAsync(
        Guid complaintId,
        ResolveComplaintRequest request,
        Guid actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default)
    {
        if (actorRole != AppRoles.WasteOfficer && actorRole != AppRoles.MunicipalManager)
        {
            throw new ForbiddenException("Only Waste Officers or Municipal Managers can resolve complaints.");
        }

        var complaint = await _db.Complaints
            .FirstOrDefaultAsync(c => c.Id == complaintId, cancellationToken);

        if (complaint is null)
        {
            throw new NotFoundException($"Complaint '{complaintId}' was not found.");
        }

        if (complaint.Status != ComplaintStatus.InReview)
        {
            throw new BusinessRuleConflictException(
                $"Cannot resolve complaint. Current status is '{complaint.Status}'. Only InReview complaints can be resolved.");
        }

        if (string.IsNullOrWhiteSpace(request.ResolutionNote))
        {
            throw new ArgumentException("Resolution note is required and cannot be empty.", nameof(request));
        }

        var now = DateTime.UtcNow;
        complaint.Status = ComplaintStatus.Resolved;
        complaint.ResolvedByUserId = actorUserId;
        complaint.ResolvedAt = now;
        complaint.ResolutionNote = request.ResolutionNote.Trim();
        complaint.UpdatedAt = now;

        await _db.SaveChangesAsync(cancellationToken);

        return await BuildDetailDtoAsync(complaint, cancellationToken);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // PRIVATE HELPERS
    // ──────────────────────────────────────────────────────────────────────────

    private async Task<ComplaintDetailDto> BuildDetailDtoAsync(
        Complaint complaint,
        CancellationToken cancellationToken)
    {
        var citizenName = await ResolveUserNameAsync(complaint.CitizenId, cancellationToken);

        string? resolvedByUserName = null;
        if (complaint.ResolvedByUserId.HasValue)
        {
            resolvedByUserName = await ResolveUserNameAsync(complaint.ResolvedByUserId.Value, cancellationToken);
        }

        return new ComplaintDetailDto
        {
            Id = complaint.Id,
            CitizenId = complaint.CitizenId,
            CitizenName = citizenName,
            Category = complaint.Category,
            Subject = complaint.Subject,
            Description = complaint.Description,
            Latitude = complaint.Latitude,
            Longitude = complaint.Longitude,
            LocationDescription = complaint.LocationDescription,
            Status = complaint.Status,
            ResolutionNote = complaint.ResolutionNote,
            ResolvedAt = complaint.ResolvedAt,
            ResolvedByUserId = complaint.ResolvedByUserId,
            ResolvedByUserName = resolvedByUserName,
            CreatedAt = complaint.CreatedAt,
            UpdatedAt = complaint.UpdatedAt
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

    private static void EnforceNotDriver(string actorRole)
    {
        if (actorRole == AppRoles.Driver)
        {
            throw new ForbiddenException("Drivers do not have access to complaint operations.");
        }
    }

    private static void EnforceCitizenOwnership(string actorRole, Guid actorUserId, Guid complaintCitizenId)
    {
        if (actorRole == AppRoles.Citizen && complaintCitizenId != actorUserId)
        {
            throw new ForbiddenException("You can only access your own complaints.");
        }
    }
}
