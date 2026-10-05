using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using SmartWaste.Application.DTOs.Auth;
using SmartWaste.Application.DTOs.Users;
using SmartWaste.Domain.Common;

namespace SmartWaste.Tests;
using Xunit;

[Collection(IntegrationTestCollection.Name)]
public class PlatformLoginAndChangePasswordTests
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public PlatformLoginAndChangePasswordTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Login_WasteOfficer_Web_ShouldSucceed()
    {
        var request = new LoginRequest
        {
            Email = "officer@smartwaste.local",
            Password = "DevPassword123!",
            ClientType = "web"
        };

        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        auth.Should().NotBeNull();
        auth!.AccessToken.Should().NotBeNullOrWhiteSpace();
        auth.User.Role.Should().Be(AppRoles.WasteOfficer);
    }

    [Fact]
    public async Task Login_MunicipalManager_Web_ShouldSucceed()
    {
        var request = new LoginRequest
        {
            Email = "manager@smartwaste.local",
            Password = "DevPassword123!",
            ClientType = "web"
        };

        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        auth.Should().NotBeNull();
        auth!.AccessToken.Should().NotBeNullOrWhiteSpace();
        auth.User.Role.Should().Be(AppRoles.MunicipalManager);
    }

    [Fact]
    public async Task Login_Citizen_Mobile_ShouldSucceed()
    {
        var request = new LoginRequest
        {
            Email = "citizen@smartwaste.local",
            Password = "DevPassword123!",
            ClientType = "mobile"
        };

        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        auth.Should().NotBeNull();
        auth!.AccessToken.Should().NotBeNullOrWhiteSpace();
        auth.User.Role.Should().Be(AppRoles.Citizen);
    }

    [Fact]
    public async Task Login_Driver_Mobile_ShouldSucceed()
    {
        var request = new LoginRequest
        {
            Email = "driver@smartwaste.local",
            Password = "DevPassword123!",
            ClientType = "mobile"
        };

        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        auth.Should().NotBeNull();
        auth!.AccessToken.Should().NotBeNullOrWhiteSpace();
        auth.User.Role.Should().Be(AppRoles.Driver);
    }

    [Fact]
    public async Task Login_Citizen_Web_ShouldReturn403_NoToken()
    {
        var request = new LoginRequest
        {
            Email = "citizen@smartwaste.local",
            Password = "DevPassword123!",
            ClientType = "web"
        };

        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotContain("accessToken");
        content.Should().Contain("This account is for the SmartWaste mobile application.");

        var problem = JsonSerializer.Deserialize<ProblemDetails>(content, JsonOptions);
        problem.Should().NotBeNull();
        problem!.Detail.Should().Be("This account is for the SmartWaste mobile application.");
    }

    [Fact]
    public async Task Login_Driver_Web_ShouldReturn403_NoToken()
    {
        var request = new LoginRequest
        {
            Email = "driver@smartwaste.local",
            Password = "DevPassword123!",
            ClientType = "web"
        };

        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotContain("accessToken");
        content.Should().Contain("This account is for the SmartWaste mobile application.");
    }

    [Fact]
    public async Task Login_WasteOfficer_Mobile_ShouldReturn403_NoToken()
    {
        var request = new LoginRequest
        {
            Email = "officer@smartwaste.local",
            Password = "DevPassword123!",
            ClientType = "mobile"
        };

        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotContain("accessToken");
        content.Should().Contain("This account is for the SmartWaste web application.");
    }

    [Fact]
    public async Task Login_MunicipalManager_Mobile_ShouldReturn403_NoToken()
    {
        var request = new LoginRequest
        {
            Email = "manager@smartwaste.local",
            Password = "DevPassword123!",
            ClientType = "mobile"
        };

        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotContain("accessToken");
        content.Should().Contain("This account is for the SmartWaste web application.");
    }

    [Fact]
    public async Task Login_InvalidPassword_PreservesGenericFailure()
    {
        var request = new LoginRequest
        {
            Email = "officer@smartwaste.local",
            Password = "CompletelyWrongPassword!",
            ClientType = "web"
        };

        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ForcedPasswordUser_MustChangePasswordFlow()
    {
        // 1. Manager logs in and creates a new Driver
        var managerLogin = new LoginRequest
        {
            Email = "manager@smartwaste.local",
            Password = "DevPassword123!",
            ClientType = "web"
        };
        var managerRes = await _client.PostAsJsonAsync("/api/v1/auth/login", managerLogin);
        var managerAuth = await managerRes.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);

        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var createRequest = new CreateUserRequest
        {
            FullName = $"Driver {uniqueId}",
            Email = $"driver_{uniqueId}@smartwaste.test",
            Username = $"driver_{uniqueId}",
            Role = AppRoles.Driver
        };

        var createMsg = new HttpRequestMessage(HttpMethod.Post, "/api/v1/users")
        {
            Content = JsonContent.Create(createRequest)
        };
        createMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", managerAuth!.AccessToken);
        var createRes = await _client.SendAsync(createMsg);
        createRes.StatusCode.Should().Be(HttpStatusCode.Created);

        var createResult = await createRes.Content.ReadFromJsonAsync<CreateUserResponse>(JsonOptions);
        createResult.Should().NotBeNull();
        createResult!.User.MustChangePassword.Should().BeTrue();
        createResult.TemporaryPassword.Should().NotBeNullOrWhiteSpace();

        // 2. Newly created user logs in with temporary password on mobile
        var tempLogin = new LoginRequest
        {
            Username = createRequest.Username,
            Password = createResult.TemporaryPassword,
            ClientType = "mobile"
        };
        var tempLoginRes = await _client.PostAsJsonAsync("/api/v1/auth/login", tempLogin);
        tempLoginRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var tempAuth = await tempLoginRes.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        tempAuth.Should().NotBeNull();
        tempAuth!.MustChangePassword.Should().BeTrue();

        // 3. User attempts to access a normal protected endpoint -> blocked with 403
        var normalReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/test-auth/authenticated");
        normalReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tempAuth.AccessToken);
        var normalRes = await _client.SendAsync(normalReq);
        normalRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var normalContent = await normalRes.Content.ReadAsStringAsync();
        normalContent.Should().Contain("Password change required before accessing this resource.");

        // 4. User attempts change-password with wrong current password -> 401
        var wrongPwdReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/change-password")
        {
            Content = JsonContent.Create(new ChangePasswordRequest
            {
                CurrentPassword = "WrongCurrentPassword123!",
                NewPassword = "NewPermanentPassword123!"
            })
        };
        wrongPwdReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tempAuth.AccessToken);
        var wrongPwdRes = await _client.SendAsync(wrongPwdReq);
        wrongPwdRes.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // 5. User attempts change-password with weak new password -> 400
        var weakPwdReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/change-password")
        {
            Content = JsonContent.Create(new ChangePasswordRequest
            {
                CurrentPassword = createResult.TemporaryPassword,
                NewPassword = "weak"
            })
        };
        weakPwdReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tempAuth.AccessToken);
        var weakPwdRes = await _client.SendAsync(weakPwdReq);
        weakPwdRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // 6. User calls change-password with correct credentials -> 204 No Content
        var validPwdReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/change-password")
        {
            Content = JsonContent.Create(new ChangePasswordRequest
            {
                CurrentPassword = createResult.TemporaryPassword,
                NewPassword = "NewPermanentPassword123!"
            })
        };
        validPwdReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tempAuth.AccessToken);
        var validPwdRes = await _client.SendAsync(validPwdReq);
        validPwdRes.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 7. Old temporary password no longer works
        var oldLoginRes = await _client.PostAsJsonAsync("/api/v1/auth/login", tempLogin);
        oldLoginRes.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // 8. User logs in with new permanent password -> MustChangePassword is false
        var newLogin = new LoginRequest
        {
            Username = createRequest.Username,
            Password = "NewPermanentPassword123!",
            ClientType = "mobile"
        };
        var newLoginRes = await _client.PostAsJsonAsync("/api/v1/auth/login", newLogin);
        newLoginRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var newAuth = await newLoginRes.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        newAuth.Should().NotBeNull();
        newAuth!.MustChangePassword.Should().BeFalse();
        newAuth.User.MustChangePassword.Should().BeFalse();

        // 9. Now user CAN access normal protected business endpoint
        var allowReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/test-auth/authenticated");
        allowReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", newAuth.AccessToken);
        var allowRes = await _client.SendAsync(allowReq);
        allowRes.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
