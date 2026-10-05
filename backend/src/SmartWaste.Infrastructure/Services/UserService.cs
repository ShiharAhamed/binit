using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SmartWaste.Application.Common.Exceptions;
using SmartWaste.Application.Common.Models;
using SmartWaste.Application.DTOs.Users;
using SmartWaste.Application.Interfaces;
using SmartWaste.Domain.Common;
using SmartWaste.Domain.Entities;
using SmartWaste.Infrastructure.Identity.Services;
using SmartWaste.Infrastructure.Persistence;

namespace SmartWaste.Infrastructure.Services;

/// <summary>
/// Infrastructure service for administrative management of internal staff accounts.
/// Restricts creation to Driver, WasteOfficer, and MunicipalManager, generating cryptographically secure temporary passwords.
/// </summary>
public class UserService : IUserService
{
    private static readonly string[] AllowedRoles =
    {
        AppRoles.Driver,
        AppRoles.WasteOfficer,
        AppRoles.MunicipalManager
    };

    private readonly UserManager<AppUser> _userManager;
    private readonly RoleManager<IdentityRole<Guid>> _roleManager;
    private readonly AppDbContext _db;

    public UserService(
        UserManager<AppUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        AppDbContext db)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _db = db;
    }

    public async Task<PagedResult<UserManagementDto>> GetUsersAsync(UserListQuery query)
    {
        var page = query.Page > 0 ? query.Page : 1;
        var pageSize = query.PageSize is > 0 and <= 100 ? query.PageSize : 20;

        var usersQuery = _userManager.Users.AsNoTracking();

        if (query.IsActive.HasValue)
        {
            usersQuery = usersQuery.Where(u => u.IsActive == query.IsActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            usersQuery = usersQuery.Where(u =>
                u.FullName.ToLower().Contains(search) ||
                (u.Email != null && u.Email.ToLower().Contains(search)) ||
                (u.UserName != null && u.UserName.ToLower().Contains(search)));
        }

        // Order by registration timestamp descending
        usersQuery = usersQuery.OrderByDescending(u => u.CreatedAt);

        var allMatchingUsers = await usersQuery.ToListAsync();

        // Project with roles and filter by allowed internal roles / role filter
        var dtos = new List<UserManagementDto>();

        foreach (var user in allMatchingUsers)
        {
            var roles = await _userManager.GetRolesAsync(user);
            var primaryRole = roles.FirstOrDefault() ?? string.Empty;

            // Only list internal staff roles (Driver, WasteOfficer, MunicipalManager)
            if (!AllowedRoles.Contains(primaryRole))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(query.Role) &&
                !string.Equals(primaryRole, query.Role.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            dtos.Add(new UserManagementDto
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email ?? string.Empty,
                Username = user.UserName ?? string.Empty,
                Role = primaryRole,
                IsActive = user.IsActive,
                MustChangePassword = user.MustChangePassword,
                CreatedAt = user.CreatedAt
            });
        }

        var totalCount = dtos.Count;
        var pagedItems = dtos
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new PagedResult<UserManagementDto>
        {
            Items = pagedItems,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<CreateUserResponse> CreateUserAsync(CreateUserRequest request)
    {
        // 1. Validate role is allowed internal staff role
        if (!AllowedRoles.Contains(request.Role))
        {
            throw new InvalidRoleException($"Role '{request.Role}' is invalid. Allowed roles: {string.Join(", ", AllowedRoles)}. Citizen accounts cannot be created internally.");
        }

        // 2. Validate email uniqueness
        var existingEmail = await _userManager.FindByEmailAsync(request.Email.Trim());
        if (existingEmail != null)
        {
            throw new DuplicateEmailException(request.Email.Trim());
        }

        // 3. Validate username uniqueness
        var existingUsername = await _userManager.FindByNameAsync(request.Username.Trim());
        if (existingUsername != null)
        {
            throw new UserManagementException($"A user with username '{request.Username.Trim()}' already exists.");
        }

        // 4. Generate cryptographically secure random temporary password
        var temporaryPassword = GenerateSecureTemporaryPassword();

        // 5. Create AppUser with MustChangePassword = true
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            FullName = request.FullName.Trim(),
            Email = request.Email.Trim(),
            UserName = request.Username.Trim(),
            IsActive = true,
            MustChangePassword = true,
            CreatedAt = DateTime.UtcNow
        };

        var createResult = await _userManager.CreateAsync(user, temporaryPassword);
        if (!createResult.Succeeded)
        {
            throw new IdentityOperationException(createResult.Errors.Select(e => e.Description));
        }

        // 6. Ensure role exists and assign it
        if (!await _roleManager.RoleExistsAsync(request.Role))
        {
            await _roleManager.CreateAsync(new IdentityRole<Guid>(request.Role));
        }

        var roleResult = await _userManager.AddToRoleAsync(user, request.Role);
        if (!roleResult.Succeeded)
        {
            // Rollback user creation to maintain clean state
            await _userManager.DeleteAsync(user);
            throw new IdentityOperationException(roleResult.Errors.Select(e => e.Description));
        }

        if (request.Role == AppRoles.Driver)
        {
            try
            {
                await DriverProfileProvisioner.EnsureAsync(_db, user.Id);
            }
            catch
            {
                await _userManager.RemoveFromRoleAsync(user, request.Role);
                await _userManager.DeleteAsync(user);
                throw;
            }
        }

        return new CreateUserResponse
        {
            User = new UserManagementDto
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email ?? string.Empty,
                Username = user.UserName ?? string.Empty,
                Role = request.Role,
                IsActive = user.IsActive,
                MustChangePassword = user.MustChangePassword,
                CreatedAt = user.CreatedAt
            },
            TemporaryPassword = temporaryPassword
        };
    }

    public async Task<UserManagementDto> UpdateUserStatusAsync(Guid userId, UpdateUserStatusRequest request, Guid currentUserId)
    {
        if (userId == currentUserId && !request.IsActive)
        {
            throw new UserManagementException("A manager cannot deactivate their own account.");
        }

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            throw new NotFoundException("User not found.");
        }

        user.IsActive = request.IsActive;
        user.UpdatedAt = DateTime.UtcNow;

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            throw new IdentityOperationException(updateResult.Errors.Select(e => e.Description));
        }

        var roles = await _userManager.GetRolesAsync(user);
        var primaryRole = roles.FirstOrDefault() ?? string.Empty;

        return new UserManagementDto
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email ?? string.Empty,
            Username = user.UserName ?? string.Empty,
            Role = primaryRole,
            IsActive = user.IsActive,
            MustChangePassword = user.MustChangePassword,
            CreatedAt = user.CreatedAt
        };
    }

    /// <summary>
    /// Generates a cryptographically secure 16-character random temporary password
    /// satisfying ASP.NET Core Identity complexity requirements.
    /// </summary>
    private static string GenerateSecureTemporaryPassword()
    {
        const string uppers = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lowers = "abcdefghijkmnopqrstuvwxyz";
        const string digits = "23456789";
        const string specials = "!@#$%^&*()-_=+";
        const string allChars = uppers + lowers + digits + specials;

        var bytes = new byte[16];
        RandomNumberGenerator.Fill(bytes);

        var chars = new char[16];
        // Ensure at least one from each required character class
        chars[0] = uppers[bytes[0] % uppers.Length];
        chars[1] = lowers[bytes[1] % lowers.Length];
        chars[2] = digits[bytes[2] % digits.Length];
        chars[3] = specials[bytes[3] % specials.Length];

        for (var i = 4; i < 16; i++)
        {
            chars[i] = allChars[bytes[i] % allChars.Length];
        }

        // Shuffle characters
        var random = new byte[16];
        RandomNumberGenerator.Fill(random);
        for (var i = chars.Length - 1; i > 0; i--)
        {
            var j = random[i] % (i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }

        return new string(chars);
    }
}
