using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartWaste.Application.Common.Models;
using SmartWaste.Application.DTOs.Auth;
using SmartWaste.Application.Reporting.DTOs.Requests;
using SmartWaste.Application.Reporting.DTOs.Responses;
using SmartWaste.Domain.Reporting.Entities;
using SmartWaste.Domain.Reporting.Enums;
using SmartWaste.Infrastructure.Persistence;
using Xunit;

namespace SmartWaste.Tests.Reporting.Integration;

/// <summary>
/// Step 9A.6: Real PostgreSQL integration and authorization verification for Component 1.
/// Exercises the full pipeline:
/// HttpClient -> ASP.NET Core Middleware -> JWT Authentication/Authorization ->
/// WasteReportsController -> WasteReportService -> EF Core/Npgsql -> PostgreSQL Database.
/// Inspects PostgreSQL state via fresh AppDbContext scopes to verify persistence, FKs, and invariants.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class WasteReportPostgreSqlIntegrationTests : IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly ConcurrentBag<Guid> _createdReportIds = new();
    private readonly ConcurrentBag<Guid> _createdCitizenIds = new();

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false) }
    };

    public WasteReportPostgreSqlIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        // Ensure all EF Core migrations are applied to the PostgreSQL database
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        // Clean up test reports and histories created by this test class
        if (!_createdReportIds.IsEmpty || !_createdCitizenIds.IsEmpty)
        {
            await using var scope = _factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            if (!_createdReportIds.IsEmpty)
            {
                var reportIds = _createdReportIds.ToList();
                await db.AgentWorkflows
                    .Where(w => w.TriggeringWasteReportId.HasValue && reportIds.Contains(w.TriggeringWasteReportId.Value))
                    .ExecuteDeleteAsync();
                await db.WasteReportStatusHistories
                    .Where(h => reportIds.Contains(h.WasteReportId))
                    .ExecuteDeleteAsync();
                await db.WasteReports
                    .Where(r => reportIds.Contains(r.Id))
                    .ExecuteDeleteAsync();
            }

            if (!_createdCitizenIds.IsEmpty)
            {
                var userIds = _createdCitizenIds.ToList();
                await db.Users
                    .Where(u => userIds.Contains(u.Id))
                    .ExecuteDeleteAsync();
            }
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // AUTHENTICATION & REQUEST HELPERS
    // ──────────────────────────────────────────────────────────────────────────

    private async Task<string> GetTokenAsync(string email, string password, string clientType)
    {
        var loginReq = new LoginRequest
        {
            Email = email,
            Password = password,
            ClientType = clientType
        };
        var res = await _client.PostAsJsonAsync("/api/v1/auth/login", loginReq, JsonOptions);
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = await res.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        return auth!.AccessToken;
    }

    private async Task<(string Token, Guid UserId)> RegisterCitizenAsync(string fullName = "Integration Citizen")
    {
        var email = $"citizen_{Guid.NewGuid():N}@smartwaste.test";
        var regReq = new RegisterRequest
        {
            FullName = fullName,
            Email = email,
            PhoneNumber = "+94771234567",
            Password = "Password123!"
        };
        var res = await _client.PostAsJsonAsync("/api/v1/auth/register", regReq, JsonOptions);
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = await res.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        _createdCitizenIds.Add(auth!.User.Id);
        return (auth.AccessToken, auth.User.Id);
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
                request.Content = JsonContent.Create(body, options: JsonOptions);
            }
        }
        return request;
    }

    private async Task<WasteReportDetailDto> CreateReportViaHttpAsync(string token, CreateWasteReportRequest? request = null)
    {
        request ??= new CreateWasteReportRequest
        {
            Description = "Default test waste report at the Pettah roundabout",
            WasteType = WasteType.General,
            Latitude = 6.9271,
            Longitude = 79.8612,
            AddressText = "Pettah Roundabout, Colombo"
        };

        var httpReq = CreateAuthorizedRequest(HttpMethod.Post, "/api/v1/waste-reports", token, request);
        var res = await _client.SendAsync(httpReq);
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var report = await res.Content.ReadFromJsonAsync<WasteReportDetailDto>(JsonOptions);
        report.Should().NotBeNull();
        _createdReportIds.Add(report!.Id);
        return report;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 1. CREATE + INITIAL HISTORY + FK VERIFICATION IN POSTGRESQL
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_PersistsInPostgreSql_WithCorrectFields_InitialHistory_AndFkToAppUser()
    {
        var (citizenToken, citizenId) = await RegisterCitizenAsync("Nimal Perera");
        var createRequest = new CreateWasteReportRequest
        {
            Description = "Overflowing garbage dump on Galle Road near bus halt",
            WasteType = WasteType.General,
            Latitude = 6.9012,
            Longitude = 79.8523,
            AddressText = "123 Galle Road, Bambalapitiya"
        };

        // 1. HTTP Call
        var httpReq = CreateAuthorizedRequest(HttpMethod.Post, "/api/v1/waste-reports", citizenToken, createRequest);
        var res = await _client.SendAsync(httpReq);

        // 2. HTTP Response Verification
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        res.Headers.Location.Should().NotBeNull();
        var reportDto = await res.Content.ReadFromJsonAsync<WasteReportDetailDto>(JsonOptions);
        reportDto.Should().NotBeNull();
        reportDto!.CitizenId.Should().Be(citizenId);
        reportDto.Status.Should().Be(WasteReportStatus.Submitted);
        reportDto.Priority.Should().BeNull("Priority must remain null in Component 1");
        reportDto.VerifiedByUserId.Should().BeNull();
        reportDto.VerifiedAt.Should().BeNull();
        reportDto.Attachments.Should().BeEmpty();
        _createdReportIds.Add(reportDto.Id);

        // 3. PostgreSQL Database Inspection via fresh DbContext scope
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var dbReport = await db.WasteReports
            .Include(r => r.Citizen)
            .FirstOrDefaultAsync(r => r.Id == reportDto.Id);

        dbReport.Should().NotBeNull();
        dbReport!.CitizenId.Should().Be(citizenId);
        dbReport.Description.Should().Be(createRequest.Description);
        dbReport.WasteType.Should().Be(WasteType.General);
        dbReport.Latitude.Should().Be(createRequest.Latitude.Value);
        dbReport.Longitude.Should().Be(createRequest.Longitude.Value);
        dbReport.AddressText.Should().Be(createRequest.AddressText);
        dbReport.Status.Should().Be(WasteReportStatus.Submitted);
        dbReport.Priority.Should().BeNull();
        dbReport.VerifiedByUserId.Should().BeNull();
        dbReport.VerifiedAt.Should().BeNull();
        dbReport.UpdatedAt.Should().BeNull();
        dbReport.CreatedAt.Kind.Should().Be(DateTimeKind.Utc);

        // 4. FK Relationship Check: references a real AppUser row
        dbReport.Citizen.Should().NotBeNull();
        dbReport.Citizen!.Id.Should().Be(citizenId);
        dbReport.Citizen.FullName.Should().Be("Nimal Perera");

        // 5. Initial History Record in PostgreSQL
        var historyRows = await db.WasteReportStatusHistories
            .Where(h => h.WasteReportId == reportDto.Id)
            .ToListAsync();

        historyRows.Should().HaveCount(1);
        var initialHistory = historyRows[0];
        initialHistory.FromStatus.Should().BeNull();
        initialHistory.ToStatus.Should().Be(WasteReportStatus.Submitted);
        initialHistory.ChangedByUserId.Should().Be(citizenId);
        initialHistory.Notes.Should().BeNull();
        initialHistory.ChangedAt.Kind.Should().Be(DateTimeKind.Utc);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 2. CITIZEN OWNERSHIP & ISOLATION IN HTTP + POSTGRESQL
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CitizenOwnership_EnforcesStrictIsolationInHttpAndPostgreSql()
    {
        var (tokenA, _) = await RegisterCitizenAsync("Citizen A");
        var (tokenB, _) = await RegisterCitizenAsync("Citizen B");

        // Citizen A creates report
        var reportA = await CreateReportViaHttpAsync(tokenA);

        // Citizen A can view report
        var getReqA = CreateAuthorizedRequest(HttpMethod.Get, $"/api/v1/waste-reports/{reportA.Id}", tokenA);
        var getResA = await _client.SendAsync(getReqA);
        getResA.StatusCode.Should().Be(HttpStatusCode.OK);

        // Citizen B CANNOT view Citizen A's report (403 Forbidden)
        var getReqB = CreateAuthorizedRequest(HttpMethod.Get, $"/api/v1/waste-reports/{reportA.Id}", tokenB);
        var getResB = await _client.SendAsync(getReqB);
        getResB.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Citizen B CANNOT update Citizen A's report (403 Forbidden)
        var patchReqB = CreateAuthorizedRequest(HttpMethod.Patch, $"/api/v1/waste-reports/{reportA.Id}", tokenB,
            new UpdateWasteReportRequest { Description = "Unauthorized modification attempt" });
        var patchResB = await _client.SendAsync(patchReqB);
        patchResB.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Citizen B CANNOT cancel Citizen A's report (403 Forbidden)
        var deleteReqB = CreateAuthorizedRequest(HttpMethod.Delete, $"/api/v1/waste-reports/{reportA.Id}", tokenB);
        var deleteResB = await _client.SendAsync(deleteReqB);
        deleteResB.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Citizen B CANNOT view history of Citizen A's report (403 Forbidden)
        var historyReqB = CreateAuthorizedRequest(HttpMethod.Get, $"/api/v1/waste-reports/{reportA.Id}/history", tokenB);
        var historyResB = await _client.SendAsync(historyReqB);
        historyResB.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Verify in PostgreSQL: row remained completely untouched
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var reportInDb = await db.WasteReports.FindAsync(reportA.Id);
        reportInDb!.Description.Should().Be(reportA.Description);
        reportInDb.Status.Should().Be(WasteReportStatus.Submitted);
        reportInDb.UpdatedAt.Should().BeNull();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 3. CITIZEN LIST ISOLATION & STAFF VISIBILITY (POSTGRESQL QUERIES)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task List_EnforcesCitizenScopeAndStaffGlobalVisibilityInPostgreSql()
    {
        var (tokenA, _) = await RegisterCitizenAsync("Citizen Alpha");
        var (tokenB, _) = await RegisterCitizenAsync("Citizen Beta");
        var officerToken = await GetOfficerTokenAsync();
        var managerToken = await GetManagerTokenAsync();
        var driverToken = await GetDriverTokenAsync();

        var reportA = await CreateReportViaHttpAsync(tokenA);
        var reportB = await CreateReportViaHttpAsync(tokenB);

        // 1. Citizen A only sees report A
        var listReqA = CreateAuthorizedRequest(HttpMethod.Get, "/api/v1/waste-reports", tokenA);
        var resA = await _client.SendAsync(listReqA);
        resA.StatusCode.Should().Be(HttpStatusCode.OK);
        var pagedA = await resA.Content.ReadFromJsonAsync<PagedResult<WasteReportSummaryDto>>(JsonOptions);
        pagedA!.Items.Should().Contain(r => r.Id == reportA.Id);
        pagedA.Items.Should().NotContain(r => r.Id == reportB.Id);

        // 2. Citizen B only sees report B
        var listReqB = CreateAuthorizedRequest(HttpMethod.Get, "/api/v1/waste-reports", tokenB);
        var resB = await _client.SendAsync(listReqB);
        resB.StatusCode.Should().Be(HttpStatusCode.OK);
        var pagedB = await resB.Content.ReadFromJsonAsync<PagedResult<WasteReportSummaryDto>>(JsonOptions);
        pagedB!.Items.Should().Contain(r => r.Id == reportB.Id);
        pagedB.Items.Should().NotContain(r => r.Id == reportA.Id);

        // 3. Waste Officer sees BOTH reports
        var listReqOfficer = CreateAuthorizedRequest(HttpMethod.Get, "/api/v1/waste-reports", officerToken);
        var resOfficer = await _client.SendAsync(listReqOfficer);
        resOfficer.StatusCode.Should().Be(HttpStatusCode.OK);
        var pagedOfficer = await resOfficer.Content.ReadFromJsonAsync<PagedResult<WasteReportSummaryDto>>(JsonOptions);
        pagedOfficer!.Items.Should().Contain(r => r.Id == reportA.Id);
        pagedOfficer.Items.Should().Contain(r => r.Id == reportB.Id);

        // 4. Municipal Manager sees BOTH reports
        var listReqManager = CreateAuthorizedRequest(HttpMethod.Get, "/api/v1/waste-reports", managerToken);
        var resManager = await _client.SendAsync(listReqManager);
        resManager.StatusCode.Should().Be(HttpStatusCode.OK);
        var pagedManager = await resManager.Content.ReadFromJsonAsync<PagedResult<WasteReportSummaryDto>>(JsonOptions);
        pagedManager!.Items.Should().Contain(r => r.Id == reportA.Id);
        pagedManager.Items.Should().Contain(r => r.Id == reportB.Id);

        // 5. Driver is forbidden
        var listReqDriver = CreateAuthorizedRequest(HttpMethod.Get, "/api/v1/waste-reports", driverToken);
        var resDriver = await _client.SendAsync(listReqDriver);
        resDriver.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 4. POSTGRESQL CASE-INSENSITIVE SEARCH TRANSLATION
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Search_ExecutesCaseInsensitiveQueryAcrossDescriptionAndAddressInPostgreSql()
    {
        var (citizenToken, _) = await RegisterCitizenAsync("Search Citizen");
        var officerToken = await GetOfficerTokenAsync();

        var r1 = await CreateReportViaHttpAsync(citizenToken, new CreateWasteReportRequest
        {
            Description = "Overflowing Plastic Waste Dump near the Pettah market",
            WasteType = WasteType.Recyclable,
            Latitude = 6.9271,
            Longitude = 79.8612,
            AddressText = "Galle Road Market Sector"
        });

        var r2 = await CreateReportViaHttpAsync(citizenToken, new CreateWasteReportRequest
        {
            Description = "Hazardous chemical drums leaked near stream",
            WasteType = WasteType.Hazardous,
            Latitude = 6.9300,
            Longitude = 79.8650,
            AddressText = "Kandy Highway Industrial Zone"
        });

        // Search lower case "plastic"
        var req1 = CreateAuthorizedRequest(HttpMethod.Get, "/api/v1/waste-reports?search=plastic", officerToken);
        var res1 = await _client.SendAsync(req1);
        res1.StatusCode.Should().Be(HttpStatusCode.OK);
        var paged1 = await res1.Content.ReadFromJsonAsync<PagedResult<WasteReportSummaryDto>>(JsonOptions);
        paged1!.Items.Should().Contain(r => r.Id == r1.Id);
        paged1.Items.Should().NotContain(r => r.Id == r2.Id);

        // Search UPPER CASE "PLASTIC"
        var req2 = CreateAuthorizedRequest(HttpMethod.Get, "/api/v1/waste-reports?search=PLASTIC", officerToken);
        var res2 = await _client.SendAsync(req2);
        res2.StatusCode.Should().Be(HttpStatusCode.OK);
        var paged2 = await res2.Content.ReadFromJsonAsync<PagedResult<WasteReportSummaryDto>>(JsonOptions);
        paged2!.Items.Should().Contain(r => r.Id == r1.Id);
        paged2.Items.Should().NotContain(r => r.Id == r2.Id);

        // Search Address lower case "galle"
        var req3 = CreateAuthorizedRequest(HttpMethod.Get, "/api/v1/waste-reports?search=galle", officerToken);
        var res3 = await _client.SendAsync(req3);
        res3.StatusCode.Should().Be(HttpStatusCode.OK);
        var paged3 = await res3.Content.ReadFromJsonAsync<PagedResult<WasteReportSummaryDto>>(JsonOptions);
        paged3!.Items.Should().Contain(r => r.Id == r1.Id);
        paged3.Items.Should().NotContain(r => r.Id == r2.Id);

        // Search Address UPPER CASE "GALLE"
        var req4 = CreateAuthorizedRequest(HttpMethod.Get, "/api/v1/waste-reports?search=GALLE", officerToken);
        var res4 = await _client.SendAsync(req4);
        res4.StatusCode.Should().Be(HttpStatusCode.OK);
        var paged4 = await res4.Content.ReadFromJsonAsync<PagedResult<WasteReportSummaryDto>>(JsonOptions);
        paged4!.Items.Should().Contain(r => r.Id == r1.Id);
        paged4.Items.Should().NotContain(r => r.Id == r2.Id);

        // Search Address "kandy"
        var req5 = CreateAuthorizedRequest(HttpMethod.Get, "/api/v1/waste-reports?search=kandy", officerToken);
        var res5 = await _client.SendAsync(req5);
        res5.StatusCode.Should().Be(HttpStatusCode.OK);
        var paged5 = await res5.Content.ReadFromJsonAsync<PagedResult<WasteReportSummaryDto>>(JsonOptions);
        paged5!.Items.Should().Contain(r => r.Id == r2.Id);
        paged5.Items.Should().NotContain(r => r.Id == r1.Id);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 5. STATUS FILTER & LEGAL WORKFLOW TRANSITIONS IN POSTGRESQL
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task StatusFilter_FiltersAccuratelyAcrossLegalTransitionsInPostgreSql()
    {
        var (citizenToken, _) = await RegisterCitizenAsync("Workflow Citizen");
        var officerToken = await GetOfficerTokenAsync();

        // 1. Report in Submitted status
        var rSubmitted = await CreateReportViaHttpAsync(citizenToken);

        // 2. Report in UnderReview status
        var rUnderReview = await CreateReportViaHttpAsync(citizenToken);
        var reviewReq = CreateAuthorizedRequest(HttpMethod.Post, $"/api/v1/waste-reports/{rUnderReview.Id}/start-review", officerToken);
        (await _client.SendAsync(reviewReq)).StatusCode.Should().Be(HttpStatusCode.OK);

        // 3. Report in Verified status
        var rVerified = await CreateReportViaHttpAsync(citizenToken);
        var revReq3 = CreateAuthorizedRequest(HttpMethod.Post, $"/api/v1/waste-reports/{rVerified.Id}/start-review", officerToken);
        await _client.SendAsync(revReq3);
        await ReportWorkflowTestPreparation.MarkAwaitingVerificationAsync(_factory.Services, rVerified.Id);
        var verReq3 = CreateAuthorizedRequest(HttpMethod.Post, $"/api/v1/waste-reports/{rVerified.Id}/verify", officerToken,
            new VerifyWasteReportRequest { Priority = WasteReportPriority.High });
        (await _client.SendAsync(verReq3)).StatusCode.Should().Be(HttpStatusCode.OK);

        // 4. Report in Rejected status
        var rRejected = await CreateReportViaHttpAsync(citizenToken);
        var revReq4 = CreateAuthorizedRequest(HttpMethod.Post, $"/api/v1/waste-reports/{rRejected.Id}/start-review", officerToken);
        await _client.SendAsync(revReq4);
        await ReportWorkflowTestPreparation.MarkAwaitingVerificationAsync(_factory.Services, rRejected.Id);
        var rejReq4 = CreateAuthorizedRequest(HttpMethod.Post, $"/api/v1/waste-reports/{rRejected.Id}/reject", officerToken,
            new RejectWasteReportRequest { Reason = "Invalid location outside municipality" });
        (await _client.SendAsync(rejReq4)).StatusCode.Should().Be(HttpStatusCode.OK);

        // 5. Report in Cancelled status
        var rCancelled = await CreateReportViaHttpAsync(citizenToken);
        var canReq5 = CreateAuthorizedRequest(HttpMethod.Delete, $"/api/v1/waste-reports/{rCancelled.Id}", citizenToken);
        (await _client.SendAsync(canReq5)).StatusCode.Should().Be(HttpStatusCode.OK);

        // Test filtering by each status
        var testCases = new[]
        {
            (WasteReportStatus.Submitted, rSubmitted.Id),
            (WasteReportStatus.UnderReview, rUnderReview.Id),
            (WasteReportStatus.Verified, rVerified.Id),
            (WasteReportStatus.Rejected, rRejected.Id),
            (WasteReportStatus.Cancelled, rCancelled.Id)
        };

        foreach (var (status, expectedId) in testCases)
        {
            var req = CreateAuthorizedRequest(HttpMethod.Get, $"/api/v1/waste-reports?status={status}", officerToken);
            var res = await _client.SendAsync(req);
            res.StatusCode.Should().Be(HttpStatusCode.OK);
            var paged = await res.Content.ReadFromJsonAsync<PagedResult<WasteReportSummaryDto>>(JsonOptions);
            paged!.Items.Should().Contain(r => r.Id == expectedId);
            paged.Items.Should().AllSatisfy(r => r.Status.Should().Be(status));
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 6. WASTE TYPE FILTER & COMBINED PREDICATES IN POSTGRESQL
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task WasteTypeFilter_AndCombinedPredicates_ExecuteAccuratelyInPostgreSql()
    {
        var (citizenToken, _) = await RegisterCitizenAsync("Filter Citizen");
        var officerToken = await GetOfficerTokenAsync();

        var rGen = await CreateReportViaHttpAsync(citizenToken, new CreateWasteReportRequest
        {
            Description = "General household waste near residential area",
            WasteType = WasteType.General,
            Latitude = 6.9100,
            Longitude = 79.8500,
            AddressText = "Colombo 03"
        });

        var rHaz = await CreateReportViaHttpAsync(citizenToken, new CreateWasteReportRequest
        {
            Description = "Hazardous chemical waste bottles dumped",
            WasteType = WasteType.Hazardous,
            Latitude = 6.9200,
            Longitude = 79.8600,
            AddressText = "Industrial Canal"
        });

        // Query by wasteType=Hazardous
        var hazReq = CreateAuthorizedRequest(HttpMethod.Get, "/api/v1/waste-reports?wasteType=Hazardous", officerToken);
        var hazRes = await _client.SendAsync(hazReq);
        hazRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var hazPaged = await hazRes.Content.ReadFromJsonAsync<PagedResult<WasteReportSummaryDto>>(JsonOptions);
        hazPaged!.Items.Should().Contain(r => r.Id == rHaz.Id);
        hazPaged.Items.Should().NotContain(r => r.Id == rGen.Id);

        // Combined query: status=Submitted & wasteType=Hazardous & search=chemical
        var combReq = CreateAuthorizedRequest(HttpMethod.Get,
            "/api/v1/waste-reports?status=Submitted&wasteType=Hazardous&search=chemical", officerToken);
        var combRes = await _client.SendAsync(combReq);
        combRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var combPaged = await combRes.Content.ReadFromJsonAsync<PagedResult<WasteReportSummaryDto>>(JsonOptions);
        combPaged!.Items.Should().Contain(r => r.Id == rHaz.Id);
        combPaged.Items.Should().NotContain(r => r.Id == rGen.Id);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 7. DATE RANGE FILTER & TIMESTAMPTZ IN POSTGRESQL
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DateRangeFilter_AndTimestamptz_ExecutesCorrectlyInPostgreSql()
    {
        var (citizenToken, _) = await RegisterCitizenAsync("Date Citizen");
        var officerToken = await GetOfficerTokenAsync();

        var report = await CreateReportViaHttpAsync(citizenToken);

        // FromDate in the past, ToDate in the future (UTC)
        var fromDate = DateTime.UtcNow.AddMinutes(-10).ToString("o");
        var toDate = DateTime.UtcNow.AddMinutes(10).ToString("o");

        var req = CreateAuthorizedRequest(HttpMethod.Get,
            $"/api/v1/waste-reports?fromDate={Uri.EscapeDataString(fromDate)}&toDate={Uri.EscapeDataString(toDate)}", officerToken);
        var res = await _client.SendAsync(req);

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var paged = await res.Content.ReadFromJsonAsync<PagedResult<WasteReportSummaryDto>>(JsonOptions);
        paged!.Items.Should().Contain(r => r.Id == report.Id);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 8. SORTING — CREATEDAT AND NULLABLE UPDATEDAT IN POSTGRESQL
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Sorting_CreatedAtAndUpdatedAt_ExecutesCorrectlyInPostgreSql()
    {
        var (citizenToken, _) = await RegisterCitizenAsync("Sort Citizen");
        var officerToken = await GetOfficerTokenAsync();

        var r1 = await CreateReportViaHttpAsync(citizenToken, new CreateWasteReportRequest
        {
            Description = "First report for chronological sort verification",
            WasteType = WasteType.General,
            Latitude = 6.91,
            Longitude = 79.85
        });

        // Patch r1 so UpdatedAt is set
        var patchReq = CreateAuthorizedRequest(HttpMethod.Patch, $"/api/v1/waste-reports/{r1.Id}", citizenToken,
            new UpdateWasteReportRequest { Description = "First report updated description" });
        var patchRes = await _client.SendAsync(patchReq);
        patchRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var r2 = await CreateReportViaHttpAsync(citizenToken, new CreateWasteReportRequest
        {
            Description = "Second report untouched without UpdatedAt",
            WasteType = WasteType.General,
            Latitude = 6.92,
            Longitude = 79.86
        });

        // Set deterministic CreatedAt timestamps in PostgreSQL directly
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var dbR1 = await db.WasteReports.FindAsync(r1.Id);
            var dbR2 = await db.WasteReports.FindAsync(r2.Id);
            dbR1!.CreatedAt = DateTime.UtcNow.AddMinutes(-30);
            dbR2!.CreatedAt = DateTime.UtcNow.AddMinutes(-10);
            await db.SaveChangesAsync();
        }

        // 1. Sort by createdAt asc (oldest -> newest, citizen scope contains exactly r1 and r2)
        var sortCreatedAsc = CreateAuthorizedRequest(HttpMethod.Get, "/api/v1/waste-reports?sortBy=createdAt&sortDirection=asc", citizenToken);
        var resCreatedAsc = await _client.SendAsync(sortCreatedAsc);
        resCreatedAsc.StatusCode.Should().Be(HttpStatusCode.OK);
        var pagedCreatedAsc = await resCreatedAsc.Content.ReadFromJsonAsync<PagedResult<WasteReportSummaryDto>>(JsonOptions);
        var itemsAsc = pagedCreatedAsc!.Items.ToList();
        var idxR1Asc = itemsAsc.FindIndex(r => r.Id == r1.Id);
        var idxR2Asc = itemsAsc.FindIndex(r => r.Id == r2.Id);
        idxR1Asc.Should().BeGreaterThanOrEqualTo(0);
        idxR2Asc.Should().BeGreaterThanOrEqualTo(0);
        idxR1Asc.Should().BeLessThan(idxR2Asc, "createdAt asc must return older report (r1) before newer report (r2)");

        // 2. Sort by createdAt desc (newest -> oldest, citizen scope)
        var sortCreatedDesc = CreateAuthorizedRequest(HttpMethod.Get, "/api/v1/waste-reports?sortBy=createdAt&sortDirection=desc", citizenToken);
        var resCreatedDesc = await _client.SendAsync(sortCreatedDesc);
        resCreatedDesc.StatusCode.Should().Be(HttpStatusCode.OK);
        var pagedCreatedDesc = await resCreatedDesc.Content.ReadFromJsonAsync<PagedResult<WasteReportSummaryDto>>(JsonOptions);
        var itemsDesc = pagedCreatedDesc!.Items.ToList();
        var idxR1Desc = itemsDesc.FindIndex(r => r.Id == r1.Id);
        var idxR2Desc = itemsDesc.FindIndex(r => r.Id == r2.Id);
        idxR1Desc.Should().BeGreaterThanOrEqualTo(0);
        idxR2Desc.Should().BeGreaterThanOrEqualTo(0);
        idxR2Desc.Should().BeLessThan(idxR1Desc, "createdAt desc must return newer report (r2) before older report (r1)");

        // 3. Sort by updatedAt asc (r1 has UpdatedAt set, r2 has UpdatedAt null)
        // PostgreSQL default order for ASC puts non-null before null (nulls last)
        var sortUpdatedAsc = CreateAuthorizedRequest(HttpMethod.Get, "/api/v1/waste-reports?sortBy=updatedAt&sortDirection=asc", citizenToken);
        var resUpdatedAsc = await _client.SendAsync(sortUpdatedAsc);
        resUpdatedAsc.StatusCode.Should().Be(HttpStatusCode.OK);
        var pagedUpdatedAsc = await resUpdatedAsc.Content.ReadFromJsonAsync<PagedResult<WasteReportSummaryDto>>(JsonOptions);
        var itemsUpAsc = pagedUpdatedAsc!.Items.ToList();
        var idxR1UpAsc = itemsUpAsc.FindIndex(r => r.Id == r1.Id);
        var idxR2UpAsc = itemsUpAsc.FindIndex(r => r.Id == r2.Id);
        idxR1UpAsc.Should().BeGreaterThanOrEqualTo(0);
        idxR2UpAsc.Should().BeGreaterThanOrEqualTo(0);
        idxR1UpAsc.Should().BeLessThan(idxR2UpAsc, "updatedAt asc must return non-null UpdatedAt (r1) before null UpdatedAt (r2)");

        // 4. Sort by updatedAt desc (r1 has UpdatedAt set, r2 has UpdatedAt null)
        // PostgreSQL default order for DESC puts null before non-null (nulls first)
        var sortUpdatedDesc = CreateAuthorizedRequest(HttpMethod.Get, "/api/v1/waste-reports?sortBy=updatedAt&sortDirection=desc", citizenToken);
        var resUpdatedDesc = await _client.SendAsync(sortUpdatedDesc);
        resUpdatedDesc.StatusCode.Should().Be(HttpStatusCode.OK);
        var pagedUpdatedDesc = await resUpdatedDesc.Content.ReadFromJsonAsync<PagedResult<WasteReportSummaryDto>>(JsonOptions);
        var itemsUpDesc = pagedUpdatedDesc!.Items.ToList();
        var idxR1UpDesc = itemsUpDesc.FindIndex(r => r.Id == r1.Id);
        var idxR2UpDesc = itemsUpDesc.FindIndex(r => r.Id == r2.Id);
        idxR1UpDesc.Should().BeGreaterThanOrEqualTo(0);
        idxR2UpDesc.Should().BeGreaterThanOrEqualTo(0);
        idxR2UpDesc.Should().BeLessThan(idxR1UpDesc, "updatedAt desc must return null UpdatedAt (r2) before non-null UpdatedAt (r1)");

        // 5. Also verify officer global query succeeds with sorting
        var officerSortReq = CreateAuthorizedRequest(HttpMethod.Get, "/api/v1/waste-reports?sortBy=createdAt&sortDirection=desc", officerToken);
        var officerSortRes = await _client.SendAsync(officerSortReq);
        officerSortRes.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 9. PAGINATION IN POSTGRESQL
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Pagination_CalculatesTotalsAndPageSlicesWithoutDuplicatesInPostgreSql()
    {
        var (citizenToken, _) = await RegisterCitizenAsync("Pagination Citizen");

        // Create 3 reports for this citizen
        var r1 = await CreateReportViaHttpAsync(citizenToken);
        var r2 = await CreateReportViaHttpAsync(citizenToken);
        var r3 = await CreateReportViaHttpAsync(citizenToken);

        // Page 1 with pageSize 2
        var page1Req = CreateAuthorizedRequest(HttpMethod.Get, "/api/v1/waste-reports?page=1&pageSize=2", citizenToken);
        var res1 = await _client.SendAsync(page1Req);
        res1.StatusCode.Should().Be(HttpStatusCode.OK);
        var paged1 = await res1.Content.ReadFromJsonAsync<PagedResult<WasteReportSummaryDto>>(JsonOptions);
        paged1!.Page.Should().Be(1);
        paged1.PageSize.Should().Be(2);
        paged1.TotalCount.Should().Be(3);
        paged1.TotalPages.Should().Be(2);
        paged1.Items.Should().HaveCount(2);

        // Page 2 with pageSize 2
        var page2Req = CreateAuthorizedRequest(HttpMethod.Get, "/api/v1/waste-reports?page=2&pageSize=2", citizenToken);
        var res2 = await _client.SendAsync(page2Req);
        res2.StatusCode.Should().Be(HttpStatusCode.OK);
        var paged2 = await res2.Content.ReadFromJsonAsync<PagedResult<WasteReportSummaryDto>>(JsonOptions);
        paged2!.Page.Should().Be(2);
        paged2.PageSize.Should().Be(2);
        paged2.TotalCount.Should().Be(3);
        paged2.TotalPages.Should().Be(2);
        paged2.Items.Should().HaveCount(1);

        // Ensure no item duplication between page 1 and page 2
        var page1Ids = paged1.Items.Select(x => x.Id).ToList();
        var page2Ids = paged2.Items.Select(x => x.Id).ToList();
        page1Ids.Intersect(page2Ids).Should().BeEmpty();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 10. PATCH PERSISTENCE & ADDRESSTEXT CONTRACT IN POSTGRESQL
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Patch_PersistsInPostgreSql_AndFollowsAddressTextFrozenContract()
    {
        var (citizenToken, _) = await RegisterCitizenAsync("Patch Citizen");

        var report = await CreateReportViaHttpAsync(citizenToken, new CreateWasteReportRequest
        {
            Description = "Original report description before update",
            WasteType = WasteType.General,
            Latitude = 6.9000,
            Longitude = 79.8500,
            AddressText = "Original Address Text"
        });

        // 1. Partial update: description only (omitted addressText -> no change)
        var patch1 = new UpdateWasteReportRequest
        {
            Description = "Updated description with omitted address"
        };
        var req1 = CreateAuthorizedRequest(HttpMethod.Patch, $"/api/v1/waste-reports/{report.Id}", citizenToken, patch1);
        var res1 = await _client.SendAsync(req1);
        res1.StatusCode.Should().Be(HttpStatusCode.OK);

        // Inspect PostgreSQL
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var dbReport = await db.WasteReports.FindAsync(report.Id);
            dbReport!.Description.Should().Be(patch1.Description);
            dbReport.AddressText.Should().Be("Original Address Text", "Omitted addressText must keep existing value");
            dbReport.UpdatedAt.Should().NotBeNull();
            dbReport.UpdatedAt!.Value.Kind.Should().Be(DateTimeKind.Utc);

            // Verify no history record was added for ordinary PATCH
            var historyCount = await db.WasteReportStatusHistories.CountAsync(h => h.WasteReportId == report.Id);
            historyCount.Should().Be(1, "Ordinary PATCH must not insert status history rows");
        }

        // 2. Partial update: explicit null addressText -> no change
        var patch2Raw = "{\"description\":\"Updated description keeping address null\",\"addressText\":null}";
        var req2 = CreateAuthorizedRequest(HttpMethod.Patch, $"/api/v1/waste-reports/{report.Id}", citizenToken, patch2Raw);
        var res2 = await _client.SendAsync(req2);
        res2.StatusCode.Should().Be(HttpStatusCode.OK);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var dbReport = await db.WasteReports.FindAsync(report.Id);
            dbReport!.Description.Should().Be("Updated description keeping address null");
            dbReport.AddressText.Should().Be("Original Address Text", "null addressText must keep existing value");

            var historyCount = await db.WasteReportStatusHistories.CountAsync(h => h.WasteReportId == report.Id);
            historyCount.Should().Be(1, "Ordinary PATCH must not insert status history rows");
        }

        // 3. Partial update: addressText = "" -> clears to NULL in PostgreSQL
        var patch3Raw = "{\"addressText\":\"\"}";
        var req3 = CreateAuthorizedRequest(HttpMethod.Patch, $"/api/v1/waste-reports/{report.Id}", citizenToken, patch3Raw);
        var res3 = await _client.SendAsync(req3);
        res3.StatusCode.Should().Be(HttpStatusCode.OK);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var dbReport = await db.WasteReports.FindAsync(report.Id);
            dbReport!.AddressText.Should().BeNull("Empty string addressText must be cleared to NULL in PostgreSQL");

            var historyCount = await db.WasteReportStatusHistories.CountAsync(h => h.WasteReportId == report.Id);
            historyCount.Should().Be(1, "Ordinary PATCH must not insert status history rows");
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 11. CANCEL PERSISTENCE (NO HARD DELETE) IN POSTGRESQL
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Cancel_PersistsInPostgreSql_NoHardDelete_CreatesStatusHistory()
    {
        var (citizenToken, citizenId) = await RegisterCitizenAsync("Cancel Citizen");

        var report = await CreateReportViaHttpAsync(citizenToken);

        // Citizen cancels report via HTTP DELETE
        var deleteReq = CreateAuthorizedRequest(HttpMethod.Delete, $"/api/v1/waste-reports/{report.Id}", citizenToken);
        var deleteRes = await _client.SendAsync(deleteReq);

        deleteRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var cancelPayload = await deleteRes.Content.ReadFromJsonAsync<CancelWasteReportResponse>(JsonOptions);
        cancelPayload!.Status.Should().Be("Cancelled");

        // Inspect PostgreSQL
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var dbReport = await db.WasteReports.FindAsync(report.Id);
        dbReport.Should().NotBeNull("Cancellation is a business cancellation, NOT a physical database delete");
        dbReport!.Status.Should().Be(WasteReportStatus.Cancelled);
        dbReport.UpdatedAt.Should().NotBeNull();

        var historyRows = await db.WasteReportStatusHistories
            .Where(h => h.WasteReportId == report.Id)
            .OrderBy(h => h.ChangedAt)
            .ToListAsync();

        historyRows.Should().HaveCount(2);
        var cancelHistory = historyRows[1];
        cancelHistory.FromStatus.Should().Be(WasteReportStatus.Submitted);
        cancelHistory.ToStatus.Should().Be(WasteReportStatus.Cancelled);
        cancelHistory.ChangedByUserId.Should().Be(citizenId);
        cancelHistory.Notes.Should().Be("Cancelled by citizen");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 12. START REVIEW PERSISTENCE IN POSTGRESQL
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task StartReview_PersistsInPostgreSql_UpdatesStatusAndAddsHistory()
    {
        var (citizenToken, _) = await RegisterCitizenAsync("Review Citizen");
        var officerToken = await GetOfficerTokenAsync();

        var report = await CreateReportViaHttpAsync(citizenToken);

        var reviewReq = CreateAuthorizedRequest(HttpMethod.Post, $"/api/v1/waste-reports/{report.Id}/start-review", officerToken);
        var reviewRes = await _client.SendAsync(reviewReq);
        reviewRes.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var dbReport = await db.WasteReports.FindAsync(report.Id);
        dbReport!.Status.Should().Be(WasteReportStatus.UnderReview);
        dbReport.UpdatedAt.Should().NotBeNull();

        var historyRows = await db.WasteReportStatusHistories
            .Where(h => h.WasteReportId == report.Id)
            .OrderBy(h => h.ChangedAt)
            .ToListAsync();

        historyRows.Should().HaveCount(2);
        var reviewHistory = historyRows[1];
        reviewHistory.FromStatus.Should().Be(WasteReportStatus.Submitted);
        reviewHistory.ToStatus.Should().Be(WasteReportStatus.UnderReview);
        reviewHistory.Notes.Should().BeNull();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 13. VERIFY PERSISTENCE & EXPLICIT PRIORITY IN POSTGRESQL
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Verify_PersistsInPostgreSql_SetsVerifierMetadata_AndSelectedPriority()
    {
        var (citizenToken, _) = await RegisterCitizenAsync("Verify Citizen");
        var officerToken = await GetOfficerTokenAsync();

        var report = await CreateReportViaHttpAsync(citizenToken);

        // 1. Move to UnderReview
        var reviewReq = CreateAuthorizedRequest(HttpMethod.Post, $"/api/v1/waste-reports/{report.Id}/start-review", officerToken);
        await _client.SendAsync(reviewReq);

        // 2. Verify
        await ReportWorkflowTestPreparation.MarkAwaitingVerificationAsync(_factory.Services, report.Id);
        var verifyReq = CreateAuthorizedRequest(HttpMethod.Post, $"/api/v1/waste-reports/{report.Id}/verify", officerToken,
            new VerifyWasteReportRequest { Priority = WasteReportPriority.Urgent });
        var verifyRes = await _client.SendAsync(verifyReq);
        verifyRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // 3. Inspect PostgreSQL
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var dbReport = await db.WasteReports.FindAsync(report.Id);
        dbReport!.Status.Should().Be(WasteReportStatus.Verified);
        dbReport.VerifiedByUserId.Should().NotBeNull();
        dbReport.VerifiedAt.Should().NotBeNull();
        dbReport.VerifiedAt!.Value.Kind.Should().Be(DateTimeKind.Utc);
        dbReport.UpdatedAt.Should().NotBeNull();

        dbReport.Priority.Should().Be(WasteReportPriority.Urgent);

        var historyRows = await db.WasteReportStatusHistories
            .Where(h => h.WasteReportId == report.Id)
            .OrderBy(h => h.ChangedAt)
            .ToListAsync();

        historyRows.Should().HaveCount(3);
        var verifyHistory = historyRows[2];
        verifyHistory.FromStatus.Should().Be(WasteReportStatus.UnderReview);
        verifyHistory.ToStatus.Should().Be(WasteReportStatus.Verified);
        verifyHistory.ChangedByUserId.Should().Be(dbReport.VerifiedByUserId!.Value);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 14. REJECT PERSISTENCE & NOTES IN POSTGRESQL
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Reject_PersistsInPostgreSql_StoresReasonInHistory_DoesNotSetVerifiedFields()
    {
        var (citizenToken, _) = await RegisterCitizenAsync("Reject Citizen");
        var officerToken = await GetOfficerTokenAsync();

        var report = await CreateReportViaHttpAsync(citizenToken);

        // Move to UnderReview
        var reviewReq = CreateAuthorizedRequest(HttpMethod.Post, $"/api/v1/waste-reports/{report.Id}/start-review", officerToken);
        await _client.SendAsync(reviewReq);

        // Reject
        await ReportWorkflowTestPreparation.MarkAwaitingVerificationAsync(_factory.Services, report.Id);
        var rejectionReason = "Duplicate report already covered by regular route";
        var rejectReq = CreateAuthorizedRequest(HttpMethod.Post, $"/api/v1/waste-reports/{report.Id}/reject", officerToken,
            new RejectWasteReportRequest { Reason = rejectionReason });
        var rejectRes = await _client.SendAsync(rejectReq);
        rejectRes.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var dbReport = await db.WasteReports.FindAsync(report.Id);
        dbReport!.Status.Should().Be(WasteReportStatus.Rejected);
        dbReport.VerifiedByUserId.Should().BeNull("Rejection must not populate verifiedByUserId");
        dbReport.VerifiedAt.Should().BeNull("Rejection must not populate verifiedAt");

        var historyRows = await db.WasteReportStatusHistories
            .Where(h => h.WasteReportId == report.Id)
            .OrderBy(h => h.ChangedAt)
            .ToListAsync();

        historyRows.Should().HaveCount(3);
        var rejectHistory = historyRows[2];
        rejectHistory.FromStatus.Should().Be(WasteReportStatus.UnderReview);
        rejectHistory.ToStatus.Should().Be(WasteReportStatus.Rejected);
        rejectHistory.Notes.Should().Be(rejectionReason);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 15. INVALID STATE TRANSITIONS LEAVE POSTGRESQL UNCORRUPTED
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task InvalidTransitions_DoNotCorruptPostgreSqlStateOrAddHistory()
    {
        var (citizenToken, _) = await RegisterCitizenAsync("State Citizen");
        var officerToken = await GetOfficerTokenAsync();

        var report = await CreateReportViaHttpAsync(citizenToken);

        // 1. Submitted -> verify directly (illegal, must start review first) -> 409 Conflict
        var illegalVerify = CreateAuthorizedRequest(HttpMethod.Post, $"/api/v1/waste-reports/{report.Id}/verify", officerToken,
            new VerifyWasteReportRequest { Priority = WasteReportPriority.High });
        var res1 = await _client.SendAsync(illegalVerify);
        res1.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // 2. Move to UnderReview
        var reviewReq = CreateAuthorizedRequest(HttpMethod.Post, $"/api/v1/waste-reports/{report.Id}/start-review", officerToken);
        await _client.SendAsync(reviewReq);

        // 3. UnderReview -> citizen PATCH -> 409 Conflict
        var illegalPatch = CreateAuthorizedRequest(HttpMethod.Patch, $"/api/v1/waste-reports/{report.Id}", citizenToken,
            new UpdateWasteReportRequest { Description = "Illegal edit attempt" });
        var res2 = await _client.SendAsync(illegalPatch);
        res2.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // 4. UnderReview -> citizen Cancel -> 409 Conflict
        var illegalCancel = CreateAuthorizedRequest(HttpMethod.Delete, $"/api/v1/waste-reports/{report.Id}", citizenToken);
        var res3 = await _client.SendAsync(illegalCancel);
        res3.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // 5. Verify report
        await ReportWorkflowTestPreparation.MarkAwaitingVerificationAsync(_factory.Services, report.Id);
        var verifyReq = CreateAuthorizedRequest(HttpMethod.Post, $"/api/v1/waste-reports/{report.Id}/verify", officerToken,
            new VerifyWasteReportRequest { Priority = WasteReportPriority.High });
        await _client.SendAsync(verifyReq);

        // 6. Verified -> reject -> 409 Conflict
        var illegalReject = CreateAuthorizedRequest(HttpMethod.Post, $"/api/v1/waste-reports/{report.Id}/reject", officerToken,
            new RejectWasteReportRequest { Reason = "Illegal reject on verified report" });
        var res4 = await _client.SendAsync(illegalReject);
        res4.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Verify in PostgreSQL: exactly 3 valid history records exist (Submitted -> UnderReview -> Verified)
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var historyRows = await db.WasteReportStatusHistories
            .Where(h => h.WasteReportId == report.Id)
            .OrderBy(h => h.ChangedAt)
            .ToListAsync();

        historyRows.Should().HaveCount(3);
        historyRows[0].ToStatus.Should().Be(WasteReportStatus.Submitted);
        historyRows[1].ToStatus.Should().Be(WasteReportStatus.UnderReview);
        historyRows[2].ToStatus.Should().Be(WasteReportStatus.Verified);
    }

    [Fact]
    public async Task RejectedReport_CannotTransitionToVerify_Returns409_AndDoesNotCorruptPostgreSql()
    {
        var (citizenToken, _) = await RegisterCitizenAsync("RejectVerify Citizen");
        var officerToken = await GetOfficerTokenAsync();

        var report = await CreateReportViaHttpAsync(citizenToken);

        // 1. Move to UnderReview
        var reviewReq = CreateAuthorizedRequest(HttpMethod.Post, $"/api/v1/waste-reports/{report.Id}/start-review", officerToken);
        var reviewRes = await _client.SendAsync(reviewReq);
        reviewRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // 2. Reject
        await ReportWorkflowTestPreparation.MarkAwaitingVerificationAsync(_factory.Services, report.Id);
        var rejectReq = CreateAuthorizedRequest(HttpMethod.Post, $"/api/v1/waste-reports/{report.Id}/reject", officerToken,
            new RejectWasteReportRequest { Reason = "Invalid report location outside municipal area" });
        var rejectRes = await _client.SendAsync(rejectReq);
        rejectRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // 3. Attempt illegal verify on Rejected report -> 409 Conflict
        var verifyReq = CreateAuthorizedRequest(HttpMethod.Post, $"/api/v1/waste-reports/{report.Id}/verify", officerToken,
            new VerifyWasteReportRequest { Priority = WasteReportPriority.High });
        var verifyRes = await _client.SendAsync(verifyReq);
        verifyRes.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // 4. Verify PostgreSQL integrity
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var dbReport = await db.WasteReports.FindAsync(report.Id);
        dbReport.Should().NotBeNull();
        dbReport!.Status.Should().Be(WasteReportStatus.Rejected);
        dbReport.VerifiedByUserId.Should().BeNull();
        dbReport.VerifiedAt.Should().BeNull();

        // Exactly 3 history records: Submitted -> UnderReview -> Rejected
        var historyRows = await db.WasteReportStatusHistories
            .Where(h => h.WasteReportId == report.Id)
            .OrderBy(h => h.ChangedAt)
            .ToListAsync();

        historyRows.Should().HaveCount(3);
        historyRows[0].ToStatus.Should().Be(WasteReportStatus.Submitted);
        historyRows[1].ToStatus.Should().Be(WasteReportStatus.UnderReview);
        historyRows[2].ToStatus.Should().Be(WasteReportStatus.Rejected);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 16. CHRONOLOGICAL HISTORY ORDERING
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task History_ReturnsChronologicalAuditTrailFromPostgreSql()
    {
        var (citizenToken, _) = await RegisterCitizenAsync("History Citizen");
        var officerToken = await GetOfficerTokenAsync();

        var report = await CreateReportViaHttpAsync(citizenToken);

        var revReq = CreateAuthorizedRequest(HttpMethod.Post, $"/api/v1/waste-reports/{report.Id}/start-review", officerToken);
        await _client.SendAsync(revReq);
        await ReportWorkflowTestPreparation.MarkAwaitingVerificationAsync(_factory.Services, report.Id);

        var verReq = CreateAuthorizedRequest(HttpMethod.Post, $"/api/v1/waste-reports/{report.Id}/verify", officerToken,
            new VerifyWasteReportRequest { Priority = WasteReportPriority.High });
        await _client.SendAsync(verReq);

        var historyReq = CreateAuthorizedRequest(HttpMethod.Get, $"/api/v1/waste-reports/{report.Id}/history", officerToken);
        var historyRes = await _client.SendAsync(historyReq);
        historyRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var historyList = await historyRes.Content.ReadFromJsonAsync<List<WasteReportStatusHistoryDto>>(JsonOptions);
        historyList.Should().HaveCount(3);
        historyList![0].ToStatus.Should().Be(WasteReportStatus.Submitted);
        historyList[1].ToStatus.Should().Be(WasteReportStatus.UnderReview);
        historyList[2].ToStatus.Should().Be(WasteReportStatus.Verified);

        // Strict chronological ordering
        historyList[0].ChangedAt.Should().BeOnOrBefore(historyList[1].ChangedAt);
        historyList[1].ChangedAt.Should().BeOnOrBefore(historyList[2].ChangedAt);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 17. MUNICIPAL MANAGER REPORT REVIEW & DRIVER FORBIDDEN
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task MunicipalManager_CanReviewReports_AndDriverIsForbiddenInPostgreSql()
    {
        var (citizenToken, _) = await RegisterCitizenAsync("Role Citizen");
        var managerToken = await GetManagerTokenAsync();
        var driverToken = await GetDriverTokenAsync();

        var report = await CreateReportViaHttpAsync(citizenToken);

        // Manager Read operations -> 200 OK
        var listRes = await _client.SendAsync(CreateAuthorizedRequest(HttpMethod.Get, "/api/v1/waste-reports", managerToken));
        listRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var detailRes = await _client.SendAsync(CreateAuthorizedRequest(HttpMethod.Get, $"/api/v1/waste-reports/{report.Id}", managerToken));
        detailRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var historyRes = await _client.SendAsync(CreateAuthorizedRequest(HttpMethod.Get, $"/api/v1/waste-reports/{report.Id}/history", managerToken));
        historyRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // Manager citizen-only write operations remain forbidden.
        var createRes = await _client.SendAsync(CreateAuthorizedRequest(HttpMethod.Post, "/api/v1/waste-reports", managerToken,
            new CreateWasteReportRequest { Description = "Manager report attempt", WasteType = WasteType.General, Latitude = 6.9, Longitude = 79.8 }));
        createRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var patchRes = await _client.SendAsync(CreateAuthorizedRequest(HttpMethod.Patch, $"/api/v1/waste-reports/{report.Id}", managerToken,
            new UpdateWasteReportRequest { Description = "Manager patch attempt" }));
        patchRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var deleteRes = await _client.SendAsync(CreateAuthorizedRequest(HttpMethod.Delete, $"/api/v1/waste-reports/{report.Id}", managerToken));
        deleteRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Manager can perform authoritative report review decisions.
        var reviewRes = await _client.SendAsync(CreateAuthorizedRequest(HttpMethod.Post, $"/api/v1/waste-reports/{report.Id}/start-review", managerToken));
        reviewRes.StatusCode.Should().Be(HttpStatusCode.OK);
        await ReportWorkflowTestPreparation.MarkAwaitingVerificationAsync(_factory.Services, report.Id);

        var verifyRes = await _client.SendAsync(CreateAuthorizedRequest(HttpMethod.Post, $"/api/v1/waste-reports/{report.Id}/verify", managerToken,
            new VerifyWasteReportRequest { Priority = WasteReportPriority.Medium }));
        verifyRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var verified = await verifyRes.Content.ReadFromJsonAsync<WasteReportDetailDto>(JsonOptions);
        verified!.Priority.Should().Be(WasteReportPriority.Medium);

        var rejectedReport = await CreateReportViaHttpAsync(citizenToken);
        (await _client.SendAsync(CreateAuthorizedRequest(HttpMethod.Post,
            $"/api/v1/waste-reports/{rejectedReport.Id}/start-review", managerToken))).StatusCode.Should().Be(HttpStatusCode.OK);
        await ReportWorkflowTestPreparation.MarkAwaitingVerificationAsync(_factory.Services, rejectedReport.Id);
        var rejectRes = await _client.SendAsync(CreateAuthorizedRequest(HttpMethod.Post,
            $"/api/v1/waste-reports/{rejectedReport.Id}/reject", managerToken,
            new RejectWasteReportRequest { Reason = "Duplicate report submitted." }));
        rejectRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // Driver operations -> 403 Forbidden
        var driverList = await _client.SendAsync(CreateAuthorizedRequest(HttpMethod.Get, "/api/v1/waste-reports", driverToken));
        driverList.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var driverDetail = await _client.SendAsync(CreateAuthorizedRequest(HttpMethod.Get, $"/api/v1/waste-reports/{report.Id}", driverToken));
        driverDetail.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var driverHistory = await _client.SendAsync(CreateAuthorizedRequest(HttpMethod.Get, $"/api/v1/waste-reports/{report.Id}/history", driverToken));
        driverHistory.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 18. ENUM DATABASE ROUNDTRIP & POSTGRESQL STRING STORAGE
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task StringEnum_DatabaseRoundtrip_StoresAsStringInPostgreSql()
    {
        var (citizenToken, _) = await RegisterCitizenAsync("Enum Citizen");

        var report = await CreateReportViaHttpAsync(citizenToken, new CreateWasteReportRequest
        {
            Description = "Report for string enum database storage verification",
            WasteType = WasteType.Hazardous,
            Latitude = 6.9123,
            Longitude = 79.8456
        });

        // 1. Verify HTTP response
        report.WasteType.Should().Be(WasteType.Hazardous);
        report.Status.Should().Be(WasteReportStatus.Submitted);

        // 2. Direct PostgreSQL string verification via raw ADO.NET SQL reader
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
        {
            await conn.OpenAsync();
        }

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT \"WasteType\", \"Status\" FROM \"WasteReports\" WHERE \"Id\" = @id";
        var param = cmd.CreateParameter();
        param.ParameterName = "id";
        param.Value = report.Id;
        cmd.Parameters.Add(param);

        await using var reader = await cmd.ExecuteReaderAsync();
        (await reader.ReadAsync()).Should().BeTrue();

        var rawWasteType = reader.GetString(0);
        var rawStatus = reader.GetString(1);

        rawWasteType.Should().Be("Hazardous", "PostgreSQL column WasteType must store varchar text, not integer");
        rawStatus.Should().Be("Submitted", "PostgreSQL column Status must store varchar text, not integer");
    }
}
