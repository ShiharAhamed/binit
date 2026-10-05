using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartWaste.Application.DTOs.Auth;
using SmartWaste.Application.Reporting.DTOs.Requests;
using SmartWaste.Application.Reporting.DTOs.Responses;
using SmartWaste.Domain.Reporting.Enums;
using SmartWaste.Infrastructure.Persistence;
using Xunit;

namespace SmartWaste.Tests.Reporting.Controllers;

/// <summary>
/// HTTP integration tests for photographic attachment endpoints:
/// POST /api/v1/waste-reports/{id}/attachments
/// DELETE /api/v1/waste-reports/{id}/attachments/{attachmentId}
/// Exercises the full pipeline:
/// MultipartFormDataContent -> Middleware -> Controller -> Service -> FakeFileStorageService + PostgreSQL.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class WasteReportAttachmentControllerTests : IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly ConcurrentBag<Guid> _createdReportIds = new();
    private readonly ConcurrentBag<Guid> _createdCitizenIds = new();

    private static readonly byte[] ValidJpegBytes = { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01 };
    private static readonly byte[] ValidPngBytes = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D };
    private static readonly byte[] InvalidPdfBytes = { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x35, 0x0A };

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false) }
    };

    public WasteReportAttachmentControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync()
    {
        _factory.FakeStorage.Reset();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
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
                await db.ReportAttachments
                    .Where(a => reportIds.Contains(a.WasteReportId))
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
    // HELPERS
    // ──────────────────────────────────────────────────────────────────────────

    private async Task<string> GetTokenAsync(string email, string password, string clientType)
    {
        var loginReq = new LoginRequest { Email = email, Password = password, ClientType = clientType };
        var res = await _client.PostAsJsonAsync("/api/v1/auth/login", loginReq, JsonOptions);
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = await res.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        return auth!.AccessToken;
    }

    private async Task<(string Token, Guid UserId)> RegisterCitizenAsync(string fullName = "Attachment Citizen")
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

    private static HttpRequestMessage CreateAuthorizedRequest(HttpMethod method, string url, string token, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (content != null)
        {
            request.Content = content;
        }
        return request;
    }

    private async Task<WasteReportDetailDto> CreateReportViaHttpAsync(string token)
    {
        var req = new CreateWasteReportRequest
        {
            Description = "Test waste report for attachment tests",
            WasteType = WasteType.General,
            Latitude = 6.9271,
            Longitude = 79.8612,
            AddressText = "Colombo, Sri Lanka"
        };

        var httpReq = CreateAuthorizedRequest(HttpMethod.Post, "/api/v1/waste-reports", token, JsonContent.Create(req, options: JsonOptions));
        var res = await _client.SendAsync(httpReq);
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var report = await res.Content.ReadFromJsonAsync<WasteReportDetailDto>(JsonOptions);
        _createdReportIds.Add(report!.Id);
        return report;
    }

    private static MultipartFormDataContent BuildMultipartContent(byte[] fileBytes, string fieldName = "file", string fileName = "evidence.jpg", string mimeType = "image/jpeg")
    {
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(mimeType);
        content.Add(fileContent, fieldName, fileName);
        return content;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 1. SUCCESSFUL UPLOAD & PRESENTATION (201 CREATED)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Upload_ValidJpeg_Returns201Created_AndReturnsReportAttachmentDto()
    {
        var (token, _) = await RegisterCitizenAsync("Citizen Uploader");
        var report = await CreateReportViaHttpAsync(token);

        using var multipart = BuildMultipartContent(ValidJpegBytes, "file", "photo.jpg", "image/jpeg");
        var req = CreateAuthorizedRequest(HttpMethod.Post, $"/api/v1/waste-reports/{report.Id}/attachments", token, multipart);
        var res = await _client.SendAsync(req);

        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var attachment = await res.Content.ReadFromJsonAsync<ReportAttachmentDto>(JsonOptions);

        attachment.Should().NotBeNull();
        attachment!.Id.Should().NotBeEmpty();
        attachment.WasteReportId.Should().Be(report.Id);
        attachment.FileType.Should().Be("image/jpeg");
        attachment.FileUrl.Should().StartWith("https://storage.fake.local/waste-reports/");
        attachment.FileUrl.Should().Contain(report.Id.ToString());

        // Verify attachment is present in GetById detail response
        var getReq = CreateAuthorizedRequest(HttpMethod.Get, $"/api/v1/waste-reports/{report.Id}", token);
        var getRes = await _client.SendAsync(getReq);
        getRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await getRes.Content.ReadFromJsonAsync<WasteReportDetailDto>(JsonOptions);

        detail!.Attachments.Should().HaveCount(1);
        detail.Attachments[0].Id.Should().Be(attachment.Id);
        detail.Attachments[0].FileUrl.Should().Be(attachment.FileUrl);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 2. VALIDATION FAILURES (400 BAD REQUEST)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Upload_MissingFileFormField_Returns400BadRequest()
    {
        var (token, _) = await RegisterCitizenAsync("Citizen Form Test");
        var report = await CreateReportViaHttpAsync(token);

        // Send multipart with wrong form field name "wrong_field" instead of "file"
        using var multipart = BuildMultipartContent(ValidJpegBytes, "wrong_field", "photo.jpg", "image/jpeg");
        var req = CreateAuthorizedRequest(HttpMethod.Post, $"/api/v1/waste-reports/{report.Id}/attachments", token, multipart);
        var res = await _client.SendAsync(req);

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Upload_FileExceeds5Mb_Returns400BadRequest()
    {
        var (token, _) = await RegisterCitizenAsync("Citizen Large Test");
        var report = await CreateReportViaHttpAsync(token);

        // 5 MB + 1 byte payload
        var largeBytes = new byte[5 * 1024 * 1024 + 1];
        largeBytes[0] = 0xFF; largeBytes[1] = 0xD8; largeBytes[2] = 0xFF; // JPEG magic header

        using var multipart = BuildMultipartContent(largeBytes, "file", "huge.jpg", "image/jpeg");
        var req = CreateAuthorizedRequest(HttpMethod.Post, $"/api/v1/waste-reports/{report.Id}/attachments", token, multipart);
        var res = await _client.SendAsync(req);

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Upload_InvalidMagicBytes_PdfBytesInJpg_Returns400BadRequest()
    {
        var (token, _) = await RegisterCitizenAsync("Citizen Spoof Test");
        var report = await CreateReportViaHttpAsync(token);

        using var multipart = BuildMultipartContent(InvalidPdfBytes, "file", "spoofed.jpg", "image/jpeg");
        var req = CreateAuthorizedRequest(HttpMethod.Post, $"/api/v1/waste-reports/{report.Id}/attachments", token, multipart);
        var res = await _client.SendAsync(req);

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 3. AUTHORIZATION & STATUS CONFLICTS (403 & 409)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Upload_CitizenBToCitizenAReport_Returns403Forbidden()
    {
        var (tokenA, _) = await RegisterCitizenAsync("Citizen A");
        var (tokenB, _) = await RegisterCitizenAsync("Citizen B");
        var reportA = await CreateReportViaHttpAsync(tokenA);

        using var multipart = BuildMultipartContent(ValidJpegBytes, "file", "photo.jpg", "image/jpeg");
        var req = CreateAuthorizedRequest(HttpMethod.Post, $"/api/v1/waste-reports/{reportA.Id}/attachments", tokenB, multipart);
        var res = await _client.SendAsync(req);

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Upload_WhenReportUnderReview_Returns409Conflict()
    {
        var (token, _) = await RegisterCitizenAsync("Citizen Review Test");
        var officerToken = await GetOfficerTokenAsync();
        var report = await CreateReportViaHttpAsync(token);

        // Waste Officer moves report to UnderReview
        var reviewReq = CreateAuthorizedRequest(HttpMethod.Post, $"/api/v1/waste-reports/{report.Id}/start-review", officerToken);
        var reviewRes = await _client.SendAsync(reviewReq);
        reviewRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // Citizen attempts upload -> 409 Conflict
        using var multipart = BuildMultipartContent(ValidJpegBytes, "file", "photo.jpg", "image/jpeg");
        var uploadReq = CreateAuthorizedRequest(HttpMethod.Post, $"/api/v1/waste-reports/{report.Id}/attachments", token, multipart);
        var uploadRes = await _client.SendAsync(uploadReq);

        uploadRes.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Upload_FourthAttachment_Returns409Conflict()
    {
        var (token, _) = await RegisterCitizenAsync("Citizen Max Test");
        var report = await CreateReportViaHttpAsync(token);

        // Upload 3 valid attachments
        for (int i = 0; i < 3; i++)
        {
            using var mp = BuildMultipartContent(ValidJpegBytes, "file", $"photo{i}.jpg", "image/jpeg");
            var req = CreateAuthorizedRequest(HttpMethod.Post, $"/api/v1/waste-reports/{report.Id}/attachments", token, mp);
            var res = await _client.SendAsync(req);
            res.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        // 4th attachment attempt -> 409 Conflict
        using var mp4 = BuildMultipartContent(ValidJpegBytes, "file", "photo4.jpg", "image/jpeg");
        var req4 = CreateAuthorizedRequest(HttpMethod.Post, $"/api/v1/waste-reports/{report.Id}/attachments", token, mp4);
        var res4 = await _client.SendAsync(req4);

        res4.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 4. DELETE ATTACHMENT (200 OK)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_OwningCitizenOnSubmittedReport_Returns200OK_AndRemovesAttachment()
    {
        var (token, _) = await RegisterCitizenAsync("Citizen Delete Test");
        var report = await CreateReportViaHttpAsync(token);

        // Upload attachment
        using var multipart = BuildMultipartContent(ValidPngBytes, "file", "photo.png", "image/png");
        var uploadReq = CreateAuthorizedRequest(HttpMethod.Post, $"/api/v1/waste-reports/{report.Id}/attachments", token, multipart);
        var uploadRes = await _client.SendAsync(uploadReq);
        uploadRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var attachment = await uploadRes.Content.ReadFromJsonAsync<ReportAttachmentDto>(JsonOptions);

        // Delete attachment
        var deleteReq = CreateAuthorizedRequest(HttpMethod.Delete, $"/api/v1/waste-reports/{report.Id}/attachments/{attachment!.Id}", token);
        var deleteRes = await _client.SendAsync(deleteReq);

        deleteRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var deleteBody = await deleteRes.Content.ReadFromJsonAsync<DeleteAttachmentResponse>(JsonOptions);
        deleteBody!.Message.Should().Be("Attachment removed successfully.");

        // Verify detail response now has 0 attachments
        var getReq = CreateAuthorizedRequest(HttpMethod.Get, $"/api/v1/waste-reports/{report.Id}", token);
        var getRes = await _client.SendAsync(getReq);
        var detail = await getRes.Content.ReadFromJsonAsync<WasteReportDetailDto>(JsonOptions);
        detail!.Attachments.Should().BeEmpty();
    }

    [Fact]
    public async Task Delete_CitizenBOnCitizenAReport_Returns403Forbidden()
    {
        var (tokenA, _) = await RegisterCitizenAsync("Citizen A Del");
        var (tokenB, _) = await RegisterCitizenAsync("Citizen B Del");
        var reportA = await CreateReportViaHttpAsync(tokenA);

        // Upload attachment as Citizen A
        using var multipart = BuildMultipartContent(ValidJpegBytes, "file", "photo.jpg", "image/jpeg");
        var uploadReq = CreateAuthorizedRequest(HttpMethod.Post, $"/api/v1/waste-reports/{reportA.Id}/attachments", tokenA, multipart);
        var uploadRes = await _client.SendAsync(uploadReq);
        uploadRes.StatusCode.Should().Be(HttpStatusCode.Created);
        var attachment = await uploadRes.Content.ReadFromJsonAsync<ReportAttachmentDto>(JsonOptions);

        // Citizen B attempts delete -> 403 Forbidden
        var deleteReq = CreateAuthorizedRequest(HttpMethod.Delete, $"/api/v1/waste-reports/{reportA.Id}/attachments/{attachment!.Id}", tokenB);
        var deleteRes = await _client.SendAsync(deleteReq);

        deleteRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
