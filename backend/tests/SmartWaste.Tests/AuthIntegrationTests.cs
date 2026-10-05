using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using SmartWaste.Application.DTOs.Auth;
using SmartWaste.Domain.Common;

namespace SmartWaste.Tests;
using Xunit;

[Collection(IntegrationTestCollection.Name)]
public class AuthIntegrationTests
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public AuthIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_ValidCitizen_ShouldSucceed_AndAssignCitizenRole()
    {
        // Arrange
        var uniqueEmail = $"citizen_{Guid.NewGuid():N}@smartwaste.test";
        var request = new RegisterRequest
        {
            FullName = "Test Citizen",
            Email = uniqueEmail,
            PhoneNumber = "+94771234567",
            Password = "Password123!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        authResponse.Should().NotBeNull();
        authResponse!.AccessToken.Should().NotBeNullOrWhiteSpace();
        authResponse.User.Role.Should().Be(AppRoles.Citizen);
        authResponse.User.Email.Should().Be(uniqueEmail);
    }

    [Fact]
    public async Task Register_DuplicateEmail_ShouldBeRejected()
    {
        // Arrange
        var email = $"duplicate_{Guid.NewGuid():N}@smartwaste.test";
        var request = new RegisterRequest
        {
            FullName = "First User",
            Email = email,
            PhoneNumber = "+94771234567",
            Password = "Password123!"
        };

        var firstResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", request);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act - register with same email
        var duplicateResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", request);

        // Assert
        duplicateResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Login_ValidCredentials_ShouldReturnAccessToken()
    {
        // Arrange - use development seeded citizen account or register new
        var email = $"loginuser_{Guid.NewGuid():N}@smartwaste.test";
        var registerRequest = new RegisterRequest
        {
            FullName = "Login User",
            Email = email,
            PhoneNumber = "+94771234567",
            Password = "Password123!"
        };
        await _client.PostAsJsonAsync("/api/v1/auth/register", registerRequest);

        var loginRequest = new LoginRequest
        {
            Email = email,
            Password = "Password123!"
        };

        // Act
        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);

        // Assert
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var authResponse = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        authResponse.Should().NotBeNull();
        authResponse!.AccessToken.Should().NotBeNullOrWhiteSpace();
        authResponse.User.Email.Should().Be(email);
    }

    [Fact]
    public async Task Login_InvalidPassword_ShouldReturnUnauthorized()
    {
        // Arrange
        var loginRequest = new LoginRequest
        {
            Email = "manager@smartwaste.local",
            Password = "WrongPassword999!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_ShouldReturnUnauthorized()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/auth/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetCurrentUser_WithValidToken_ShouldReturnProfile()
    {
        // Arrange
        var email = $"me_test_{Guid.NewGuid():N}@smartwaste.test";
        var registerRequest = new RegisterRequest
        {
            FullName = "Profile Test User",
            Email = email,
            PhoneNumber = "+94779876543",
            Password = "Password123!"
        };
        var regResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", registerRequest);
        var auth = await regResponse.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);

        var requestMessage = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        // Act
        var meResponse = await _client.SendAsync(requestMessage);

        // Assert
        meResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var profile = await meResponse.Content.ReadFromJsonAsync<CurrentUserResponse>(JsonOptions);
        profile.Should().NotBeNull();
        profile!.Email.Should().Be(email);
        profile.FullName.Should().Be("Profile Test User");
        profile.Role.Should().Be(AppRoles.Citizen);
    }

    [Fact]
    public async Task RoleRestrictedEndpoint_CitizenToken_AccessingManager_ShouldReturnForbidden()
    {
        // Arrange: Register a citizen
        var email = $"citizen_forbid_{Guid.NewGuid():N}@smartwaste.test";
        var registerRequest = new RegisterRequest
        {
            FullName = "Regular Citizen",
            Email = email,
            PhoneNumber = "+94771122334",
            Password = "Password123!"
        };
        var regResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", registerRequest);
        var auth = await regResponse.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);

        var requestMessage = new HttpRequestMessage(HttpMethod.Get, "/api/v1/test-auth/manager");
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        // Act - Citizen tries to access manager-only endpoint
        var response = await _client.SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RoleRestrictedEndpoint_ManagerToken_AccessingManager_ShouldSucceed()
    {
        // Arrange: Login as seeded MunicipalManager
        var loginRequest = new LoginRequest
        {
            Email = "manager@smartwaste.local",
            Password = "DevPassword123!"
        };
        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);

        var requestMessage = new HttpRequestMessage(HttpMethod.Get, "/api/v1/test-auth/manager");
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        // Act
        var response = await _client.SendAsync(requestMessage);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task HealthEndpoint_ShouldReturnHealthy()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("healthy");
    }
}
