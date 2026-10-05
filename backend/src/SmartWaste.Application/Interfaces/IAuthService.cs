using SmartWaste.Application.DTOs.Auth;
using SmartWaste.Domain.Entities;

namespace SmartWaste.Application.Interfaces;

/// <summary>
/// Service interface for authentication and token generation.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Registers a new citizen account with the Citizen role strictly enforced.
    /// </summary>
    Task<AuthResponse> RegisterCitizenAsync(RegisterRequest request);

    /// <summary>
    /// Authenticates a user with email and password and returns a JWT access token.
    /// Inactive accounts are rejected.
    /// </summary>
    Task<AuthResponse> LoginAsync(LoginRequest request);

    /// <summary>
    /// Retrieves the profile and primary role of the currently authenticated user.
    /// </summary>
    Task<CurrentUserResponse> GetCurrentUserAsync(Guid userId);

    /// <summary>
    /// Changes the password of an authenticated user. Validates current password and updates MustChangePassword flag to false.
    /// </summary>
    Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request);

    /// <summary>
    /// Generates a signed JWT bearer token containing user claims and roles.
    /// </summary>
    string GenerateJwtToken(AppUser user, IList<string> roles, out DateTime expiresAt);
}
