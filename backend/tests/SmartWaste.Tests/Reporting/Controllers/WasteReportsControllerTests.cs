using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using SmartWaste.Application.Common.Models;
using SmartWaste.Application.DTOs.Auth;
using SmartWaste.Application.Reporting.DTOs.Requests;
using SmartWaste.Application.Reporting.DTOs.Responses;
using SmartWaste.Domain.Reporting.Enums;
using Xunit;

namespace SmartWaste.Tests.Reporting.Controllers;

[Collection(IntegrationTestCollection.Name)]
public class WasteReportsControllerTests
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions SharedTestJsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false) }
    };

    public WasteReportsControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // AUTH HELPERS
    // ──────────────────────────────────────────────────────────────────────────

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
        var auth = await res.Content.ReadFromJsonAsync<AuthResponse>(SharedTestJsonOptions);
        return auth!.AccessToken;
    }

    private async Task<string> RegisterUniqueCitizenTokenAsync()
    {
        var email = $"citizen_{Guid.NewGuid():N}@smartwaste.test";
        var regReq = new RegisterRequest
        {
            FullName = "Integration Test Citizen",
            Email = email,
            PhoneNumber = "+94771234567",
            Password = "Password123!"
        };
        var res = await _client.PostAsJsonAsync("/api/v1/auth/register", regReq);
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = await res.Content.ReadFromJsonAsync<AuthResponse>(SharedTestJsonOptions);
        return auth!.AccessToken;
    }

    private async Task<string> GetOfficerTokenAsync()
        => await GetTokenAsync("officer@smartwaste.local", "DevPassword123!", "web");

    private async Task<string> GetManagerTokenAsync()
        => await GetTokenAsync("manager@smartwaste.local", "DevPassword123!", "web");

    private async Task<string> GetDriverTokenAsync()
        => await GetTokenAsync("driver@smartwaste.local", "DevPassword123!", "mobile");

    private static HttpRequestMessage CreateAuthorizedRequest(HttpMethod method, string url, string token, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body != null)
        {
            if (body is string rawJson)
            {
                request.Content = new StringContent(rawJson, Encoding.UTF8, "application/json");
            }
            else
            {
                request.Content = JsonContent.Create(body, options: SharedTestJsonOptions);
            }
        }
        return request;
    }

    private static CreateWasteReportRequest ValidCreateRequest(string description = "Overflowing garbage heap near the Pettah junction") =>
        new()
        {
            Description = description,
            WasteType = WasteType.General,
            Latitude = 6.9271,
            Longitude = 79.8612,
            AddressText = "Main Street, Pettah"
        };

    // ──────────────────────────────────────────────────────────────────────────
    // 1. UNAUTHENTICATED ACCESS (401)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Post_WasteReports_WithoutAuth_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/waste-reports", ValidCreateRequest(), SharedTestJsonOptions);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_WasteReports_WithoutAuth_Returns401()
    {
        var response = await _client.GetAsync("/api/v1/waste-reports");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetById_WithoutAuth_Returns401()
    {
        var response = await _client.GetAsync($"/api/v1/waste-reports/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Patch_WithoutAuth_Returns401()
    {
        var response = await _client.PatchAsJsonAsync($"/api/v1/waste-reports/{Guid.NewGuid()}", new UpdateWasteReportRequest { Description = "Updated description" }, SharedTestJsonOptions);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Delete_WithoutAuth_Returns401()
    {
        var response = await _client.DeleteAsync($"/api/v1/waste-reports/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task StartReview_WithoutAuth_Returns401()
    {
        var response = await _client.PostAsync($"/api/v1/waste-reports/{Guid.NewGuid()}/start-review", null);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Verify_WithoutAuth_Returns401()
    {
        var response = await _client.PostAsync($"/api/v1/waste-reports/{Guid.NewGuid()}/verify", null);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Reject_WithoutAuth_Returns401()
    {
        var response = await _client.PostAsJsonAsync($"/api/v1/waste-reports/{Guid.NewGuid()}/reject", new RejectWasteReportRequest { Reason = "Duplicate report" }, SharedTestJsonOptions);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task History_WithoutAuth_Returns401()
    {
        var response = await _client.GetAsync($"/api/v1/waste-reports/{Guid.NewGuid()}/history");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 2. ROLE-BASED ACCESS CONTROL (403)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task WasteOfficer_CannotCreateReport_Returns403()
    {
        var officerToken = await GetOfficerTokenAsync();
        var req = CreateAuthorizedRequest(HttpMethod.Post, "/api/v1/waste-reports", officerToken, ValidCreateRequest());

        var res = await _client.SendAsync(req);

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Driver_CannotListReports_Returns403()
    {
        var driverToken = await GetDriverTokenAsync();
        var req = CreateAuthorizedRequest(HttpMethod.Get, "/api/v1/waste-reports", driverToken);

        var res = await _client.SendAsync(req);

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task MunicipalManager_VerifyMissingReport_Returns404()
    {
        var managerToken = await GetManagerTokenAsync();
        var req = CreateAuthorizedRequest(HttpMethod.Post, $"/api/v1/waste-reports/{Guid.NewGuid()}/verify", managerToken,
            new VerifyWasteReportRequest { Priority = WasteReportPriority.High });

        var res = await _client.SendAsync(req);

        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Citizen_CannotVerifyReport_Returns403()
    {
        var citizenToken = await RegisterUniqueCitizenTokenAsync();
        var req = CreateAuthorizedRequest(HttpMethod.Post, $"/api/v1/waste-reports/{Guid.NewGuid()}/verify", citizenToken);

        var res = await _client.SendAsync(req);

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Citizen_CannotStartReview_Returns403()
    {
        var citizenToken = await RegisterUniqueCitizenTokenAsync();
        var req = CreateAuthorizedRequest(HttpMethod.Post, $"/api/v1/waste-reports/{Guid.NewGuid()}/start-review", citizenToken);

        var res = await _client.SendAsync(req);

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Driver_CannotGetReportById_Returns403()
    {
        var driverToken = await GetDriverTokenAsync();
        var req = CreateAuthorizedRequest(HttpMethod.Get, $"/api/v1/waste-reports/{Guid.NewGuid()}", driverToken);

        var res = await _client.SendAsync(req);

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 3. STRING ENUM JSON CONTRACT & VALIDATION
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateReport_WithValidStringEnum_SerializesAndDeserializesCorrectly()
    {
        var citizenToken = await RegisterUniqueCitizenTokenAsync();
        var jsonPayload = @"{
            ""description"": ""Valid garbage report with string enum General"",
            ""wasteType"": ""General"",
            ""latitude"": 6.9271,
            ""longitude"": 79.8612,
            ""addressText"": ""Pettah market area""
        }";

        var req = CreateAuthorizedRequest(HttpMethod.Post, "/api/v1/waste-reports", citizenToken, jsonPayload);
        var res = await _client.SendAsync(req);

        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var rawString = await res.Content.ReadAsStringAsync();
        rawString.Should().Contain("\"wasteType\"");
        rawString.Should().Contain("\"General\"");
        rawString.Should().Contain("\"status\"");
        rawString.Should().Contain("\"Submitted\"");
    }

    [Fact]
    public async Task CreateReport_WithInvalidStringEnum_Returns400()
    {
        var citizenToken = await RegisterUniqueCitizenTokenAsync();
        var jsonPayload = @"{
            ""description"": ""Report with invalid enum type"",
            ""wasteType"": ""NotARealType"",
            ""latitude"": 6.9271,
            ""longitude"": 79.8612
        }";

        var req = CreateAuthorizedRequest(HttpMethod.Post, "/api/v1/waste-reports", citizenToken, jsonPayload);
        var res = await _client.SendAsync(req);

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateReport_WithIntegerEnum_WhenIntegerDisabled_Returns400()
    {
        var citizenToken = await RegisterUniqueCitizenTokenAsync();
        var jsonPayload = @"{
            ""description"": ""Report with integer enum value 0"",
            ""wasteType"": 0,
            ""latitude"": 6.9271,
            ""longitude"": 79.8612
        }";

        var req = CreateAuthorizedRequest(HttpMethod.Post, "/api/v1/waste-reports", citizenToken, jsonPayload);
        var res = await _client.SendAsync(req);

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public void SharedTestJsonOptions_SerializesEnumsAsStrings_AndRejectsIntegers()
    {
        var model = new CreateWasteReportRequest
        {
            Description = "A valid test description for serialization testing",
            WasteType = WasteType.Hazardous,
            Latitude = 6.9,
            Longitude = 79.8
        };

        var json = JsonSerializer.Serialize(model, SharedTestJsonOptions);
        json.Should().Contain("\"wasteType\":\"Hazardous\"");
        json.Should().NotContain("\"wasteType\":3");

        // Deserializing string enum succeeds
        var deserialized = JsonSerializer.Deserialize<CreateWasteReportRequest>(json, SharedTestJsonOptions);
        deserialized.Should().NotBeNull();
        deserialized!.WasteType.Should().Be(WasteType.Hazardous);

        // Deserializing numeric enum fails because allowIntegerValues is false
        var numericJson = "{\"description\":\"A valid test description for serialization testing\",\"wasteType\":3,\"latitude\":6.9,\"longitude\":79.8}";
        var act = () => JsonSerializer.Deserialize<CreateWasteReportRequest>(numericJson, SharedTestJsonOptions);
        act.Should().Throw<JsonException>();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 4. FULL LIFECYCLE & HTTP STATUS CODES
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task EndToEnd_WasteReport_Lifecycle_SucceedsWithCorrectStatusCodes()
    {
        var citizenToken = await RegisterUniqueCitizenTokenAsync();
        var officerToken = await GetOfficerTokenAsync();

        // 1. Citizen submits report -> 201 Created
        var createReq = CreateAuthorizedRequest(HttpMethod.Post, "/api/v1/waste-reports", citizenToken, ValidCreateRequest());
        var createRes = await _client.SendAsync(createReq);
        createRes.StatusCode.Should().Be(HttpStatusCode.Created);
        createRes.Headers.Location.Should().NotBeNull();

        var createdReport = await createRes.Content.ReadFromJsonAsync<WasteReportDetailDto>(SharedTestJsonOptions);
        createdReport.Should().NotBeNull();
        createdReport!.Status.Should().Be(WasteReportStatus.Submitted);
        createdReport.Priority.Should().BeNull();
        createdReport.Attachments.Should().BeEmpty();
        var reportId = createdReport.Id;

        // 2. Citizen gets detail -> 200 OK
        var getReq = CreateAuthorizedRequest(HttpMethod.Get, $"/api/v1/waste-reports/{reportId}", citizenToken);
        var getRes = await _client.SendAsync(getReq);
        getRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await getRes.Content.ReadFromJsonAsync<WasteReportDetailDto>(SharedTestJsonOptions);
        detail!.Id.Should().Be(reportId);

        // 3. Citizen lists reports -> 200 OK
        var listReq = CreateAuthorizedRequest(HttpMethod.Get, "/api/v1/waste-reports?page=1&pageSize=10", citizenToken);
        var listRes = await _client.SendAsync(listReq);
        listRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var paged = await listRes.Content.ReadFromJsonAsync<PagedResult<WasteReportSummaryDto>>(SharedTestJsonOptions);
        paged!.Items.Should().Contain(r => r.Id == reportId);

        // 4. Citizen updates evidence (PATCH) -> 200 OK
        var updateDto = new UpdateWasteReportRequest
        {
            Description = "Updated description for the overflowing garbage pile"
        };
        var patchReq = CreateAuthorizedRequest(HttpMethod.Patch, $"/api/v1/waste-reports/{reportId}", citizenToken, updateDto);
        var patchRes = await _client.SendAsync(patchReq);
        patchRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var patched = await patchRes.Content.ReadFromJsonAsync<WasteReportDetailDto>(SharedTestJsonOptions);
        patched!.Description.Should().Be(updateDto.Description);

        // 5. Citizen views history -> 200 OK
        var historyReq = CreateAuthorizedRequest(HttpMethod.Get, $"/api/v1/waste-reports/{reportId}/history", citizenToken);
        var historyRes = await _client.SendAsync(historyReq);
        historyRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var history = await historyRes.Content.ReadFromJsonAsync<List<WasteReportStatusHistoryDto>>(SharedTestJsonOptions);
        history.Should().NotBeEmpty();
        history![0].ToStatus.Should().Be(WasteReportStatus.Submitted);

        // 6. Waste Officer starts review -> 200 OK
        var reviewReq = CreateAuthorizedRequest(HttpMethod.Post, $"/api/v1/waste-reports/{reportId}/start-review", officerToken);
        var reviewRes = await _client.SendAsync(reviewReq);
        reviewRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var reviewed = await reviewRes.Content.ReadFromJsonAsync<WasteReportDetailDto>(SharedTestJsonOptions);
        reviewed!.Status.Should().Be(WasteReportStatus.UnderReview);

        await ReportWorkflowTestPreparation.MarkAwaitingVerificationAsync(_factory.Services, reportId);

        // 7. Waste Officer verifies report -> 200 OK
        var verifyReq = CreateAuthorizedRequest(HttpMethod.Post, $"/api/v1/waste-reports/{reportId}/verify", officerToken,
            new VerifyWasteReportRequest { Priority = WasteReportPriority.High });
        var verifyRes = await _client.SendAsync(verifyReq);
        verifyRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var verified = await verifyRes.Content.ReadFromJsonAsync<WasteReportDetailDto>(SharedTestJsonOptions);
        verified!.Status.Should().Be(WasteReportStatus.Verified);
        verified.Priority.Should().Be(WasteReportPriority.High);
        verified.VerifiedByUserId.Should().NotBeNull();
        verified.VerifiedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Officer_CanReject_ReportUnderReview()
    {
        var citizenToken = await RegisterUniqueCitizenTokenAsync();
        var officerToken = await GetOfficerTokenAsync();

        // 1. Submit
        var createReq = CreateAuthorizedRequest(HttpMethod.Post, "/api/v1/waste-reports", citizenToken, ValidCreateRequest("Report to be rejected by officer"));
        var createRes = await _client.SendAsync(createReq);
        var report = await createRes.Content.ReadFromJsonAsync<WasteReportDetailDto>(SharedTestJsonOptions);

        // 2. Start review
        var reviewReq = CreateAuthorizedRequest(HttpMethod.Post, $"/api/v1/waste-reports/{report!.Id}/start-review", officerToken);
        await _client.SendAsync(reviewReq);
        await ReportWorkflowTestPreparation.MarkAwaitingVerificationAsync(_factory.Services, report.Id);

        // 3. Reject with reason
        var rejectBody = new RejectWasteReportRequest { Reason = "Duplicate report already handled by zone team" };
        var rejectReq = CreateAuthorizedRequest(HttpMethod.Post, $"/api/v1/waste-reports/{report.Id}/reject", officerToken, rejectBody);
        var rejectRes = await _client.SendAsync(rejectReq);

        rejectRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var rejected = await rejectRes.Content.ReadFromJsonAsync<WasteReportDetailDto>(SharedTestJsonOptions);
        rejected!.Status.Should().Be(WasteReportStatus.Rejected);
        rejected.VerifiedByUserId.Should().BeNull();
    }

    [Fact]
    public async Task Citizen_CanCancel_SubmittedReport()
    {
        var citizenToken = await RegisterUniqueCitizenTokenAsync();

        // 1. Submit
        var createReq = CreateAuthorizedRequest(HttpMethod.Post, "/api/v1/waste-reports", citizenToken, ValidCreateRequest("Report to be cancelled by citizen"));
        var createRes = await _client.SendAsync(createReq);
        var report = await createRes.Content.ReadFromJsonAsync<WasteReportDetailDto>(SharedTestJsonOptions);

        // 2. Cancel via DELETE
        var cancelReq = CreateAuthorizedRequest(HttpMethod.Delete, $"/api/v1/waste-reports/{report!.Id}", citizenToken);
        var cancelRes = await _client.SendAsync(cancelReq);

        cancelRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var cancelPayload = await cancelRes.Content.ReadFromJsonAsync<CancelWasteReportResponse>(SharedTestJsonOptions);
        cancelPayload.Should().NotBeNull();
        cancelPayload!.Status.Should().Be("Cancelled");
        cancelPayload.Message.Should().Contain("cancelled");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 5. BUSINESS RULE CONFLICTS (409)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Citizen_CannotPatch_ReportUnderReview_Returns409()
    {
        var citizenToken = await RegisterUniqueCitizenTokenAsync();
        var officerToken = await GetOfficerTokenAsync();

        var createReq = CreateAuthorizedRequest(HttpMethod.Post, "/api/v1/waste-reports", citizenToken, ValidCreateRequest());
        var createRes = await _client.SendAsync(createReq);
        var report = await createRes.Content.ReadFromJsonAsync<WasteReportDetailDto>(SharedTestJsonOptions);

        // Move to UnderReview
        var reviewReq = CreateAuthorizedRequest(HttpMethod.Post, $"/api/v1/waste-reports/{report!.Id}/start-review", officerToken);
        await _client.SendAsync(reviewReq);

        // Attempt PATCH
        var patchReq = CreateAuthorizedRequest(HttpMethod.Patch, $"/api/v1/waste-reports/{report.Id}", citizenToken,
            new UpdateWasteReportRequest { Description = "Attempting to update while under review" });
        var patchRes = await _client.SendAsync(patchReq);

        patchRes.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await patchRes.Content.ReadFromJsonAsync<ProblemDetails>(SharedTestJsonOptions);
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(409);
        problem.Title.Should().Be("Business Rule Conflict");
    }

    [Fact]
    public async Task Citizen_CannotCancel_ReportUnderReview_Returns409()
    {
        var citizenToken = await RegisterUniqueCitizenTokenAsync();
        var officerToken = await GetOfficerTokenAsync();

        var createReq = CreateAuthorizedRequest(HttpMethod.Post, "/api/v1/waste-reports", citizenToken, ValidCreateRequest());
        var createRes = await _client.SendAsync(createReq);
        var report = await createRes.Content.ReadFromJsonAsync<WasteReportDetailDto>(SharedTestJsonOptions);

        // Move to UnderReview
        var reviewReq = CreateAuthorizedRequest(HttpMethod.Post, $"/api/v1/waste-reports/{report!.Id}/start-review", officerToken);
        await _client.SendAsync(reviewReq);

        // Attempt Cancel
        var cancelReq = CreateAuthorizedRequest(HttpMethod.Delete, $"/api/v1/waste-reports/{report.Id}", citizenToken);
        var cancelRes = await _client.SendAsync(cancelReq);

        cancelRes.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Officer_CannotVerify_SubmittedReport_Returns409()
    {
        var citizenToken = await RegisterUniqueCitizenTokenAsync();
        var officerToken = await GetOfficerTokenAsync();

        var createReq = CreateAuthorizedRequest(HttpMethod.Post, "/api/v1/waste-reports", citizenToken, ValidCreateRequest());
        var createRes = await _client.SendAsync(createReq);
        var report = await createRes.Content.ReadFromJsonAsync<WasteReportDetailDto>(SharedTestJsonOptions);

        // Attempt verify without start-review
        var verifyReq = CreateAuthorizedRequest(HttpMethod.Post, $"/api/v1/waste-reports/{report!.Id}/verify", officerToken,
            new VerifyWasteReportRequest { Priority = WasteReportPriority.High });
        var verifyRes = await _client.SendAsync(verifyReq);

        verifyRes.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Officer_CannotVerify_LinkedReportBeforeC1Pause_Returns409()
    {
        var citizenToken = await RegisterUniqueCitizenTokenAsync();
        var officerToken = await GetOfficerTokenAsync();
        var created = await _client.SendAsync(CreateAuthorizedRequest(HttpMethod.Post,
            "/api/v1/waste-reports", citizenToken, ValidCreateRequest()));
        var report = await created.Content.ReadFromJsonAsync<WasteReportDetailDto>(SharedTestJsonOptions);
        var reportId = report!.Id;
        (await _client.SendAsync(CreateAuthorizedRequest(HttpMethod.Post,
            $"/api/v1/waste-reports/{reportId}/start-review", officerToken)))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var verify = await _client.SendAsync(CreateAuthorizedRequest(HttpMethod.Post,
            $"/api/v1/waste-reports/{reportId}/verify", officerToken,
            new VerifyWasteReportRequest { Priority = WasteReportPriority.High }));

        verify.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var detail = await _client.SendAsync(CreateAuthorizedRequest(HttpMethod.Get,
            $"/api/v1/waste-reports/{reportId}", citizenToken));
        (await detail.Content.ReadFromJsonAsync<WasteReportDetailDto>(SharedTestJsonOptions))!
            .Status.Should().Be(WasteReportStatus.UnderReview);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 6. NOT FOUND (404)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetById_NonExistent_Returns404()
    {
        var officerToken = await GetOfficerTokenAsync();
        var req = CreateAuthorizedRequest(HttpMethod.Get, $"/api/v1/waste-reports/{Guid.NewGuid()}", officerToken);

        var res = await _client.SendAsync(req);

        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var problem = await res.Content.ReadFromJsonAsync<ProblemDetails>(SharedTestJsonOptions);
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(404);
        problem.Title.Should().Be("Resource Not Found");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 7. VALIDATION FAILURES (400)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Post_WithShortDescription_Returns400()
    {
        var citizenToken = await RegisterUniqueCitizenTokenAsync();
        var req = CreateAuthorizedRequest(HttpMethod.Post, "/api/v1/waste-reports", citizenToken, new CreateWasteReportRequest
        {
            Description = "Too short",
            WasteType = WasteType.General,
            Latitude = 6.9,
            Longitude = 79.8
        });

        var res = await _client.SendAsync(req);

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Patch_WithEmptyBody_Returns400()
    {
        var citizenToken = await RegisterUniqueCitizenTokenAsync();
        var req = CreateAuthorizedRequest(HttpMethod.Patch, $"/api/v1/waste-reports/{Guid.NewGuid()}", citizenToken, "{}");

        var res = await _client.SendAsync(req);

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Reject_WithEmptyReason_Returns400()
    {
        var officerToken = await GetOfficerTokenAsync();
        var req = CreateAuthorizedRequest(HttpMethod.Post, $"/api/v1/waste-reports/{Guid.NewGuid()}/reject", officerToken, new RejectWasteReportRequest { Reason = "" });

        var res = await _client.SendAsync(req);

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 8. QUERY STRING BINDING
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetList_WithCompleteQueryParameters_BindsSuccessfully()
    {
        var officerToken = await GetOfficerTokenAsync();
        var url = "/api/v1/waste-reports?page=1&pageSize=15&status=Submitted&wasteType=General&search=pettah&sortBy=createdAt&sortDirection=desc";
        var req = CreateAuthorizedRequest(HttpMethod.Get, url, officerToken);

        var res = await _client.SendAsync(req);

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var paged = await res.Content.ReadFromJsonAsync<PagedResult<WasteReportSummaryDto>>(SharedTestJsonOptions);
        paged.Should().NotBeNull();
        paged!.Page.Should().Be(1);
        paged.PageSize.Should().Be(15);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 9. PROBLEMDETAILS NO SENSITIVE DATA LEAK
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ProblemDetails_DoesNotLeakStackTraceOrInternalTypes()
    {
        var officerToken = await GetOfficerTokenAsync();
        var req = CreateAuthorizedRequest(HttpMethod.Get, $"/api/v1/waste-reports/{Guid.NewGuid()}", officerToken);

        var res = await _client.SendAsync(req);

        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var rawContent = await res.Content.ReadAsStringAsync();
        rawContent.Should().NotContain("Exception");
        rawContent.Should().NotContain("stackTrace");
        rawContent.Should().NotContain("StackTrace");
        rawContent.Should().NotContain("Npgsql");
        rawContent.Should().NotContain("SELECT");
        rawContent.Should().NotContain("Password");
    }
}
