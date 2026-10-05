using SmartWaste.Application.Common.Models;
using SmartWaste.Application.Collection.DTOs.Responses;
using SmartWaste.Application.Collection.Queries;
namespace SmartWaste.Application.Collection.Interfaces;
public interface IAssignmentReadService { Task<PagedResult<AssignmentTaskDto>> GetAvailableTasksAsync(AvailableAssignmentTaskQuery query, Guid actorId, string role, CancellationToken ct=default); Task<PagedResult<AssignmentSummaryDto>> GetStaffListAsync(AssignmentListQuery query, Guid actorId, string role, CancellationToken ct=default); Task<PagedResult<AssignmentSummaryDto>> GetMineAsync(AssignmentListQuery query, Guid actorId, string role, CancellationToken ct=default); Task<AssignmentDetailDto> GetDetailAsync(Guid id, Guid actorId, string role, CancellationToken ct=default); Task<RouteReadDto> GetRouteAsync(Guid id, Guid actorId, string role, CancellationToken ct=default); }
