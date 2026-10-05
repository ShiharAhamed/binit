using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartWaste.Application.Common.Exceptions;
using SmartWaste.Application.Common.Models;
using SmartWaste.Application.Fleet.DTOs.Requests;
using SmartWaste.Application.Fleet.DTOs.Responses;
using SmartWaste.Application.Fleet.Interfaces;
using SmartWaste.Application.Fleet.Queries;
using SmartWaste.Application.Fleet.Validation;
using SmartWaste.Domain.Collection.Entities;
using SmartWaste.Domain.Collection.Enums;
using SmartWaste.Domain.Common;
using SmartWaste.Domain.Entities;
using SmartWaste.Infrastructure.Persistence;

namespace SmartWaste.Infrastructure.Fleet.Services;

public class DriverProfileService : IDriverProfileService
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _users;
    private readonly UpdateDriverAvailabilityRequestValidator _availabilityValidator = new();
    private readonly DriverListQueryValidator _listValidator = new();

    public DriverProfileService(AppDbContext db, UserManager<AppUser> users) { _db = db; _users = users; }

    public async Task<PagedResult<DriverSummaryDto>> GetListAsync(DriverListQuery query, Guid actorUserId, string actorRole, CancellationToken cancellationToken = default)
    {
        RequireStaff(actorRole); await Validate(_listValidator, query, cancellationToken);
        var users = _db.Users.AsNoTracking().Include(x => x.DriverProfile).AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Search)) { var s = query.Search.Trim().ToLower(); users = users.Where(x => x.FullName.ToLower().Contains(s)); }
        var all = await users.OrderBy(x => x.FullName).ToListAsync(cancellationToken);
        var drivers = new List<DriverSummaryDto>();
        var occupied = await _db.CollectionAssignments.AsNoTracking().Where(x => x.Status == CollectionAssignmentStatus.Assigned || x.Status == CollectionAssignmentStatus.InProgress).Select(x => x.DriverId).ToListAsync(cancellationToken);
        foreach (var user in all)
        {
            if (!await _users.IsInRoleAsync(user, AppRoles.Driver)) continue;
            var profile = user.DriverProfile ?? throw new BusinessRuleConflictException("A Driver account is missing its internal availability profile.");
            if (query.AvailabilityStatus.HasValue && profile.AvailabilityStatus != query.AvailabilityStatus.Value) continue;
            drivers.Add(ToSummary(profile, user, occupied.Contains(user.Id)));
        }
        return new PagedResult<DriverSummaryDto> { Items = drivers.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList(), Page = query.Page, PageSize = query.PageSize, TotalCount = drivers.Count };
    }

    public async Task<DriverDetailDto> GetAdministrativeByIdAsync(Guid driverUserId, Guid actorUserId, string actorRole, CancellationToken cancellationToken = default)
    {
        RequireManager(actorRole);
        var profile = await _db.DriverProfiles.Include(x => x.User).SingleOrDefaultAsync(x => x.UserId == driverUserId, cancellationToken) ?? throw new NotFoundException("Driver account was not found.");
        if (profile.User is null || !await _users.IsInRoleAsync(profile.User, AppRoles.Driver)) throw new NotFoundException("Driver account was not found.");
        return ToDetail(profile, profile.User, await IsDriverOccupiedAsync(profile.UserId, cancellationToken));
    }

    public async Task<DriverSelfDto> GetSelfAsync(Guid actorUserId, string actorRole, CancellationToken cancellationToken = default)
    {
        RequireDriver(actorRole);
        var profile = await _db.DriverProfiles.Include(x => x.User).SingleOrDefaultAsync(x => x.UserId == actorUserId, cancellationToken) ?? throw new NotFoundException("Driver profile was not found.");
        return ToSelf(profile, profile.User!, await IsDriverOccupiedAsync(profile.UserId, cancellationToken));
    }

    public async Task<DriverSelfDto> UpdateMyAvailabilityAsync(UpdateDriverAvailabilityRequest request, Guid actorUserId, string actorRole, CancellationToken cancellationToken = default)
    {
        RequireDriver(actorRole); await Validate(_availabilityValidator, request, cancellationToken);
        var profile = await _db.DriverProfiles.Include(x => x.User).SingleOrDefaultAsync(x => x.UserId == actorUserId, cancellationToken) ?? throw new NotFoundException("Driver profile was not found.");
        if (!profile.User!.IsActive) throw new BusinessRuleConflictException("Inactive Driver accounts cannot change availability.");
        profile.AvailabilityStatus = request.AvailabilityStatus!.Value; profile.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken); return ToSelf(profile, profile.User, await IsDriverOccupiedAsync(profile.UserId, cancellationToken));
    }

    private static DriverSummaryDto ToSummary(DriverProfile p, AppUser u, bool occupied = false) => new() { Id = p.UserId, DisplayName = u.FullName, AvailabilityStatus = p.AvailabilityStatus, IsOccupied = occupied };
    private static DriverDetailDto ToDetail(DriverProfile p, AppUser u, bool occupied = false) => new() { Id = p.UserId, DisplayName = u.FullName, AvailabilityStatus = p.AvailabilityStatus, IsOccupied = occupied };
    private static DriverSelfDto ToSelf(DriverProfile p, AppUser u, bool occupied = false) => new() { Id = p.UserId, DisplayName = u.FullName, AvailabilityStatus = p.AvailabilityStatus, IsOccupied = occupied };
    private Task<bool> IsDriverOccupiedAsync(Guid driverId, CancellationToken cancellationToken) => _db.CollectionAssignments.AsNoTracking().AnyAsync(x => x.DriverId == driverId && (x.Status == CollectionAssignmentStatus.Assigned || x.Status == CollectionAssignmentStatus.InProgress), cancellationToken);
    private static void RequireManager(string role) { if (role != AppRoles.MunicipalManager) throw new ForbiddenException("Only Municipal Managers can view Driver details."); }
    private static void RequireStaff(string role) { if (role != AppRoles.MunicipalManager && role != AppRoles.WasteOfficer) throw new ForbiddenException("Only municipal staff can view Drivers."); }
    private static void RequireDriver(string role) { if (role != AppRoles.Driver) throw new ForbiddenException("Only Drivers can change their own availability."); }
    private static async Task Validate<T>(IValidator<T> validator, T value, CancellationToken ct) { var r = await validator.ValidateAsync(value, ct); if (!r.IsValid) throw new ValidationException(r.Errors); }
    private static bool IsUnique(DbUpdateException ex) => ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}

public class VehicleService : IVehicleService
{
    private readonly AppDbContext _db;
    private readonly CreateVehicleRequestValidator _createValidator = new();
    private readonly UpdateVehicleRequestValidator _updateValidator = new();
    private readonly UpdateVehicleOperationalStatusRequestValidator _statusValidator = new();
    private readonly VehicleListQueryValidator _listValidator = new();
    public VehicleService(AppDbContext db) { _db = db; }

    public async Task<VehicleDetailDto> CreateAsync(CreateVehicleRequest request, Guid actorUserId, string actorRole, CancellationToken cancellationToken = default)
    {
        RequireManager(actorRole); await Validate(_createValidator, request, cancellationToken);
        var registration = request.RegistrationNumber.Trim();
        if (await _db.Vehicles.AnyAsync(x => x.RegistrationNumber == registration, cancellationToken)) throw new BusinessRuleConflictException("A vehicle with this registration number already exists.");
        var vehicle = new Vehicle { Id = Guid.NewGuid(), RegistrationNumber = registration, VehicleType = request.VehicleType!.Value, CapacityLiters = request.CapacityLiters!.Value, Notes = Normalize(request.Notes), CreatedAt = DateTime.UtcNow };
        SyncWasteTypes(vehicle, request.SupportedWasteTypes); _db.Vehicles.Add(vehicle);
        try { await _db.SaveChangesAsync(cancellationToken); } catch (DbUpdateException ex) when (IsUnique(ex)) { throw new BusinessRuleConflictException("A vehicle with this registration number already exists."); }
        return ToDetail(vehicle);
    }

    public async Task<VehicleDetailDto> UpdateAsync(Guid id, UpdateVehicleRequest request, Guid actorUserId, string actorRole, CancellationToken cancellationToken = default)
    {
        RequireManager(actorRole); await Validate(_updateValidator, request, cancellationToken);
        var vehicle = await _db.Vehicles.Include(x => x.SupportedWasteTypes).SingleOrDefaultAsync(x => x.Id == id, cancellationToken) ?? throw new NotFoundException("Vehicle was not found.");
        var registration = request.RegistrationNumber.Trim();
        if (await _db.Vehicles.AnyAsync(x => x.RegistrationNumber == registration && x.Id != id, cancellationToken)) throw new BusinessRuleConflictException("A vehicle with this registration number already exists.");
        vehicle.RegistrationNumber = registration; vehicle.VehicleType = request.VehicleType!.Value; vehicle.CapacityLiters = request.CapacityLiters!.Value; vehicle.Notes = Normalize(request.Notes); vehicle.UpdatedAt = DateTime.UtcNow;
        _db.VehicleSupportedWasteTypes.RemoveRange(vehicle.SupportedWasteTypes); vehicle.SupportedWasteTypes.Clear(); SyncWasteTypes(vehicle, request.SupportedWasteTypes);
        try { await _db.SaveChangesAsync(cancellationToken); } catch (DbUpdateException ex) when (IsUnique(ex)) { throw new BusinessRuleConflictException("A vehicle with this registration number already exists."); }
        return ToDetail(vehicle);
    }

    public async Task<VehicleDetailDto> UpdateOperationalStatusAsync(Guid id, UpdateVehicleOperationalStatusRequest request, Guid actorUserId, string actorRole, CancellationToken cancellationToken = default)
    {
        RequireManager(actorRole); await Validate(_statusValidator, request, cancellationToken);
        var vehicle = await _db.Vehicles.Include(x => x.SupportedWasteTypes).SingleOrDefaultAsync(x => x.Id == id, cancellationToken) ?? throw new NotFoundException("Vehicle was not found.");
        vehicle.OperationalStatus = request.OperationalStatus!.Value; vehicle.UpdatedAt = DateTime.UtcNow; await _db.SaveChangesAsync(cancellationToken); return ToDetail(vehicle);
    }

    public async Task<PagedResult<VehicleSummaryDto>> GetListAsync(VehicleListQuery query, Guid actorUserId, string actorRole, CancellationToken cancellationToken = default)
    {
        RequireStaff(actorRole); await Validate(_listValidator, query, cancellationToken);
        var source = _db.Vehicles.AsNoTracking().Include(x => x.SupportedWasteTypes).AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Search)) { var s = query.Search.Trim().ToLower(); source = source.Where(x => x.RegistrationNumber.ToLower().Contains(s)); }
        if (query.OperationalStatus.HasValue) source = source.Where(x => x.OperationalStatus == query.OperationalStatus);
        if (query.VehicleType.HasValue) source = source.Where(x => x.VehicleType == query.VehicleType);
        var total = await source.CountAsync(cancellationToken); var items = await source.OrderBy(x => x.RegistrationNumber).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(cancellationToken); var occupied = await _db.CollectionAssignments.AsNoTracking().Where(x => (x.Status == CollectionAssignmentStatus.Assigned || x.Status == CollectionAssignmentStatus.InProgress) && items.Select(v => v.Id).Contains(x.VehicleId)).Select(x => new { x.VehicleId, x.Id }).ToDictionaryAsync(x => x.VehicleId, x => x.Id, cancellationToken);
        return new PagedResult<VehicleSummaryDto> { Items = items.Select(v => ToSummary(v, occupied.ContainsKey(v.Id))).ToList(), Page = query.Page, PageSize = query.PageSize, TotalCount = total };
    }

    public async Task<VehicleDetailDto> GetByIdAsync(Guid id, Guid actorUserId, string actorRole, CancellationToken cancellationToken = default)
    {
        RequireStaff(actorRole); var vehicle = await _db.Vehicles.AsNoTracking().Include(x => x.SupportedWasteTypes).SingleOrDefaultAsync(x => x.Id == id, cancellationToken) ?? throw new NotFoundException("Vehicle was not found."); var assignmentId = await _db.CollectionAssignments.AsNoTracking().Where(x => x.VehicleId == id && (x.Status == CollectionAssignmentStatus.Assigned || x.Status == CollectionAssignmentStatus.InProgress)).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(cancellationToken); return ToDetail(vehicle, assignmentId);
    }
    private static void SyncWasteTypes(Vehicle v, IReadOnlyList<SmartWaste.Domain.Reporting.Enums.WasteType> types) { foreach (var t in types.Distinct()) v.SupportedWasteTypes.Add(new VehicleSupportedWasteType { VehicleId = v.Id, WasteType = t }); }
    private static VehicleSummaryDto ToSummary(Vehicle v, bool occupied = false) => new() { Id = v.Id, RegistrationNumber = v.RegistrationNumber, VehicleType = v.VehicleType, OperationalStatus = v.OperationalStatus, SupportedWasteTypes = v.SupportedWasteTypes.Select(x => x.WasteType.ToString()).OrderBy(x => x).ToList(), IsOccupied = occupied };
    private static VehicleDetailDto ToDetail(Vehicle v, Guid? assignmentId = null) => new() { Id = v.Id, RegistrationNumber = v.RegistrationNumber, VehicleType = v.VehicleType, OperationalStatus = v.OperationalStatus, CapacityLiters = v.CapacityLiters, Notes = v.Notes, SupportedWasteTypes = v.SupportedWasteTypes.Select(x => x.WasteType.ToString()).OrderBy(x => x).ToList(), IsOccupied = assignmentId.HasValue, CurrentAssignmentId = assignmentId, CreatedAt = v.CreatedAt, UpdatedAt = v.UpdatedAt };
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static void RequireManager(string role) { if (role != AppRoles.MunicipalManager) throw new ForbiddenException("Only Municipal Managers can manage vehicles."); }
    private static void RequireStaff(string role) { if (role != AppRoles.MunicipalManager && role != AppRoles.WasteOfficer) throw new ForbiddenException("Only municipal staff can view vehicles."); }
    private static async Task Validate<T>(IValidator<T> validator, T value, CancellationToken ct) { var r = await validator.ValidateAsync(value, ct); if (!r.IsValid) throw new ValidationException(r.Errors); }
    private static bool IsUnique(DbUpdateException ex) => ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
