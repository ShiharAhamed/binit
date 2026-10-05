using SmartWaste.Application.Common.Models;
using SmartWaste.Application.Fleet.DTOs.Requests;
using SmartWaste.Application.Fleet.DTOs.Responses;
using SmartWaste.Application.Fleet.Queries;

namespace SmartWaste.Application.Fleet.Interfaces;

public interface IDriverProfileService
{
    Task<PagedResult<DriverSummaryDto>> GetListAsync(DriverListQuery query, Guid actorUserId, string actorRole, CancellationToken cancellationToken = default);
    Task<DriverDetailDto> GetAdministrativeByIdAsync(Guid driverUserId, Guid actorUserId, string actorRole, CancellationToken cancellationToken = default);
    Task<DriverSelfDto> GetSelfAsync(Guid actorUserId, string actorRole, CancellationToken cancellationToken = default);
    Task<DriverSelfDto> UpdateMyAvailabilityAsync(UpdateDriverAvailabilityRequest request, Guid actorUserId, string actorRole, CancellationToken cancellationToken = default);
}

public interface IVehicleService
{
    Task<VehicleDetailDto> CreateAsync(CreateVehicleRequest request, Guid actorUserId, string actorRole, CancellationToken cancellationToken = default);
    Task<VehicleDetailDto> UpdateAsync(Guid id, UpdateVehicleRequest request, Guid actorUserId, string actorRole, CancellationToken cancellationToken = default);
    Task<VehicleDetailDto> UpdateOperationalStatusAsync(Guid id, UpdateVehicleOperationalStatusRequest request, Guid actorUserId, string actorRole, CancellationToken cancellationToken = default);
    Task<PagedResult<VehicleSummaryDto>> GetListAsync(VehicleListQuery query, Guid actorUserId, string actorRole, CancellationToken cancellationToken = default);
    Task<VehicleDetailDto> GetByIdAsync(Guid id, Guid actorUserId, string actorRole, CancellationToken cancellationToken = default);
}
