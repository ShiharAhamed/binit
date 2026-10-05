using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using SmartWaste.Application.Common.Models;
using SmartWaste.Application.DTOs.Auth;
using SmartWaste.Application.DTOs.Users;
using SmartWaste.Domain.Common;

namespace SmartWaste.Tests;
using Xunit;

[Collection(IntegrationTestCollection.Name)]
public class UserManagementIntegrationTests
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public UserManagementIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<string> GetTokenAsync(string email, string password, string clientType)
    {
        var loginReq = new LoginRequest
        {
            Email = email,
            Password = password,
            ClientType = clientType
        };
        var res = await _client.PostAsJsonAsync("/api/v1/auth/login", loginReq);
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = await res.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        return auth!.AccessToken;
    }

    [Fact]
    public async Task MunicipalManager_CanListInternalUsers_ExcludingPasswords()
    {
        var managerToken = await GetTokenAsync("manager@smartwaste.local", "DevPassword123!", "web");

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/users?page=1&pageSize=20");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", managerToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var pagedResult = await response.Content.ReadFromJsonAsync<PagedResult<UserManagementDto>>(JsonOptions);
        pagedResult.Should().NotBeNull();
        pagedResult!.Items.Should().NotBeEmpty();

        var json = await response.Content.ReadAsStringAsync();
        json.Should().NotContain("passwordHash");
        json.Should().NotContain("securityStamp");
        json.Should().NotContain("temporaryPassword");
    }

    [Fact]
    public async Task MunicipalManager_CanCreateDriver_WasteOfficer_MunicipalManager()
    {
        var managerToken = await GetTokenAsync("manager@smartwaste.local", "DevPassword123!", "web");

        var rolesToTest = new[] { AppRoles.Driver, AppRoles.WasteOfficer, AppRoles.MunicipalManager };

        foreach (var role in rolesToTest)
        {
            var uid = Guid.NewGuid().ToString("N")[..8];
            var createReq = new CreateUserRequest
            {
                FullName = $"Staff {role} {uid}",
                Email = $"staff_{role.ToLower()}_{uid}@smartwaste.test",
                Username = $"staff_{role.ToLower()}_{uid}",
                Role = role
            };

            var msg = new HttpRequestMessage(HttpMethod.Post, "/api/v1/users")
            {
                Content = JsonContent.Create(createReq)
            };
            msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", managerToken);

            var res = await _client.SendAsync(msg);
            res.StatusCode.Should().Be(HttpStatusCode.Created);

            var result = await res.Content.ReadFromJsonAsync<CreateUserResponse>(JsonOptions);
            result.Should().NotBeNull();
            result!.User.Role.Should().Be(role);
            result.User.MustChangePassword.Should().BeTrue();
            result.TemporaryPassword.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public async Task MunicipalManager_CannotCreateCitizen_ShouldReturnBadRequest()
    {
        var managerToken = await GetTokenAsync("manager@smartwaste.local", "DevPassword123!", "web");

        var uid = Guid.NewGuid().ToString("N")[..8];
        var createReq = new CreateUserRequest
        {
            FullName = $"Citizen {uid}",
            Email = $"citizen_{uid}@smartwaste.test",
            Username = $"citizen_{uid}",
            Role = AppRoles.Citizen
        };

        var msg = new HttpRequestMessage(HttpMethod.Post, "/api/v1/users")
        {
            Content = JsonContent.Create(createReq)
        };
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", managerToken);

        var res = await _client.SendAsync(msg);
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await res.Content.ReadAsStringAsync();
        content.Should().Contain("Citizen accounts cannot be created internally");
    }

    [Fact]
    public async Task NonManagers_CannotAccessUserManagementEndpoints()
    {
        var officerToken = await GetTokenAsync("officer@smartwaste.local", "DevPassword123!", "web");
        var citizenToken = await GetTokenAsync("citizen@smartwaste.local", "DevPassword123!", "mobile");

        // Officer accessing GET /api/v1/users -> 403
        var officerMsg = new HttpRequestMessage(HttpMethod.Get, "/api/v1/users");
        officerMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", officerToken);
        var officerRes = await _client.SendAsync(officerMsg);
        officerRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Citizen accessing GET /api/v1/users -> 403
        var citizenMsg = new HttpRequestMessage(HttpMethod.Get, "/api/v1/users");
        citizenMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", citizenToken);
        var citizenRes = await _client.SendAsync(citizenMsg);
        citizenRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateUser_DuplicateEmailOrUsername_ShouldFail()
    {
        var managerToken = await GetTokenAsync("manager@smartwaste.local", "DevPassword123!", "web");

        var uid = Guid.NewGuid().ToString("N")[..8];
        var createReq = new CreateUserRequest
        {
            FullName = $"Duplicate Test {uid}",
            Email = $"dup_{uid}@smartwaste.test",
            Username = $"dup_{uid}",
            Role = AppRoles.Driver
        };

        var msg1 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/users")
        {
            Content = JsonContent.Create(createReq)
        };
        msg1.Headers.Authorization = new AuthenticationHeaderValue("Bearer", managerToken);
        var res1 = await _client.SendAsync(msg1);
        res1.StatusCode.Should().Be(HttpStatusCode.Created);

        // Duplicate Email
        var dupEmailReq = new CreateUserRequest
        {
            FullName = $"Duplicate Test 2 {uid}",
            Email = $"dup_{uid}@smartwaste.test",
            Username = $"other_{uid}",
            Role = AppRoles.Driver
        };
        var msg2 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/users")
        {
            Content = JsonContent.Create(dupEmailReq)
        };
        msg2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", managerToken);
        var res2 = await _client.SendAsync(msg2);
        res2.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Duplicate Username
        var dupUserReq = new CreateUserRequest
        {
            FullName = $"Duplicate Test 3 {uid}",
            Email = $"other_{uid}@smartwaste.test",
            Username = $"dup_{uid}",
            Role = AppRoles.Driver
        };
        var msg3 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/users")
        {
            Content = JsonContent.Create(dupUserReq)
        };
        msg3.Headers.Authorization = new AuthenticationHeaderValue("Bearer", managerToken);
        var res3 = await _client.SendAsync(msg3);
        res3.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Manager_CanActivateAndDeactivateAccount_CannotDeactivateSelf()
    {
        var managerToken = await GetTokenAsync("manager@smartwaste.local", "DevPassword123!", "web");

        // 1. Create a user
        var uid = Guid.NewGuid().ToString("N")[..8];
        var createReq = new CreateUserRequest
        {
            FullName = $"Status Test {uid}",
            Email = $"status_{uid}@smartwaste.test",
            Username = $"status_{uid}",
            Role = AppRoles.Driver
        };
        var createMsg = new HttpRequestMessage(HttpMethod.Post, "/api/v1/users")
        {
            Content = JsonContent.Create(createReq)
        };
        createMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", managerToken);
        var createRes = await _client.SendAsync(createMsg);
        var created = await createRes.Content.ReadFromJsonAsync<CreateUserResponse>(JsonOptions);

        // 2. Deactivate the created user
        var deactivateMsg = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/users/{created!.User.Id}/status")
        {
            Content = JsonContent.Create(new UpdateUserStatusRequest { IsActive = false })
        };
        deactivateMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", managerToken);
        var deactRes = await _client.SendAsync(deactivateMsg);
        deactRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var deactUser = await deactRes.Content.ReadFromJsonAsync<UserManagementDto>(JsonOptions);
        deactUser!.IsActive.Should().BeFalse();

        // 3. Deactivated user cannot log in
        var deactLogin = new LoginRequest
        {
            Username = createReq.Username,
            Password = created.TemporaryPassword,
            ClientType = "mobile"
        };
        var deactLoginRes = await _client.PostAsJsonAsync("/api/v1/auth/login", deactLogin);
        deactLoginRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var loginContent = await deactLoginRes.Content.ReadAsStringAsync();
        loginContent.Should().Contain("Account is inactive");

        // 4. Reactivate user
        var reactivateMsg = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/users/{created.User.Id}/status")
        {
            Content = JsonContent.Create(new UpdateUserStatusRequest { IsActive = true })
        };
        reactivateMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", managerToken);
        var reactRes = await _client.SendAsync(reactivateMsg);
        reactRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var reactUser = await reactRes.Content.ReadFromJsonAsync<UserManagementDto>(JsonOptions);
        reactUser!.IsActive.Should().BeTrue();

        // 5. Manager cannot deactivate own account
        var meMsg = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        meMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", managerToken);
        var meRes = await _client.SendAsync(meMsg);
        var me = await meRes.Content.ReadFromJsonAsync<CurrentUserResponse>(JsonOptions);

        var selfDeactMsg = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/users/{me!.Id}/status")
        {
            Content = JsonContent.Create(new UpdateUserStatusRequest { IsActive = false })
        };
        selfDeactMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", managerToken);
        var selfDeactRes = await _client.SendAsync(selfDeactMsg);
        selfDeactRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var selfDeactContent = await selfDeactRes.Content.ReadAsStringAsync();
        selfDeactContent.Should().Contain("A manager cannot deactivate their own account");
    }
}
