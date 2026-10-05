using SmartWaste.Application.Common.Models;
using SmartWaste.Application.DTOs.Users;

namespace SmartWaste.Application.Interfaces;

/// <summary>
/// Service interface for administrative management of internal staff users.
/// </summary>
public interface IUserService
{
    Task<PagedResult<UserManagementDto>> GetUsersAsync(UserListQuery query);
    Task<CreateUserResponse> CreateUserAsync(CreateUserRequest request);
    Task<UserManagementDto> UpdateUserStatusAsync(Guid userId, UpdateUserStatusRequest request, Guid currentUserId);
}
