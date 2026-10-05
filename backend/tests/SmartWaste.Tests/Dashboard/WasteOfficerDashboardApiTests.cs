using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SmartWaste.Application.Dashboard.DTOs;
using SmartWaste.Application.Dashboard.Interfaces;
using SmartWaste.Domain.Common;
using SmartWaste.Infrastructure.Persistence;
using Xunit;

namespace SmartWaste.Tests.Dashboard;

[Collection(IntegrationTestCollection.Name)]
public class WasteOfficerDashboardApiTests(CustomWebApplicationFactory factory)
{
    private const string Route = "/api/v1/dashboard/waste-officer/overview";

    [Theory]
    [InlineData(AppRoles.WasteOfficer, HttpStatusCode.OK)]
    [InlineData(AppRoles.MunicipalManager, HttpStatusCode.Forbidden)]
    [InlineData(AppRoles.Citizen, HttpStatusCode.Forbidden)]
    [InlineData(AppRoles.Driver, HttpStatusCode.Forbidden)]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    public async Task Overview_EnforcesWasteOfficerRole(string? role, HttpStatusCode expectedStatus)
    {
        var overview = new WasteOfficerDashboardOverviewDto(4, 12, 7, 5, 3);
        var service = new Mock<IWasteOfficerDashboardService>();
        service.Setup(x => x.GetOverviewAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(overview);
        using var host = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.RemoveAll<AppDbContext>();
                services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase($"dashboard_api_{Guid.NewGuid():N}"));
                services.RemoveAll<IWasteOfficerDashboardService>();
                services.AddSingleton(service.Object);
                services.AddAuthentication("DashboardTest")
                    .AddScheme<AuthenticationSchemeOptions, DashboardTestAuthHandler>("DashboardTest", _ => { });
                services.Configure<AuthenticationOptions>(options =>
                {
                    options.DefaultAuthenticateScheme = "DashboardTest";
                    options.DefaultChallengeScheme = "DashboardTest";
                });
            });
        });
        using var client = host.CreateClient();
        if (role is not null)
            client.DefaultRequestHeaders.Add("X-Test-Role", role);

        var response = await client.GetAsync(Route);

        response.StatusCode.Should().Be(expectedStatus);
        if (expectedStatus == HttpStatusCode.OK)
        {
            var body = await response.Content.ReadFromJsonAsync<Dictionary<string, int>>()
                ?? throw new InvalidOperationException("Dashboard response was empty.");
            body.Should().HaveCount(5);
            body["reportsAwaitingReview"].Should().Be(4);
            body["activeBins"].Should().Be(12);
            body["scheduledCollections"].Should().Be(7);
            body["openCollectionTasks"].Should().Be(5);
            body["openComplaints"].Should().Be(3);
            service.Verify(x => x.GetOverviewAsync(It.IsAny<CancellationToken>()), Times.Once());
        }
        else
        {
            service.Verify(x => x.GetOverviewAsync(It.IsAny<CancellationToken>()), Times.Never());
        }
    }

    [Theory]
    [InlineData(AppRoles.WasteOfficer, HttpStatusCode.OK)]
    [InlineData(AppRoles.MunicipalManager, HttpStatusCode.Forbidden)]
    [InlineData(AppRoles.Citizen, HttpStatusCode.Forbidden)]
    [InlineData(AppRoles.Driver, HttpStatusCode.Forbidden)]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    public async Task NeedsAttention_EnforcesWasteOfficerRole(string? role, HttpStatusCode expectedStatus)
    {
        var service = new Mock<IWasteOfficerDashboardService>();
        service.Setup(x => x.GetNeedsAttentionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new WasteOfficerNeedsAttentionItemDto(
                Guid.Parse("12345678-0000-0000-0000-000000000001"),
                WasteOfficerNeedsAttentionItemType.WasteReport, "Report 12345678",
                DateTime.UtcNow, "General", "Kasun Silva", "Rajagiriya")]);
        using var host = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.RemoveAll<AppDbContext>();
                services.AddDbContext<AppDbContext>(options =>
                    options.UseInMemoryDatabase($"dashboard_queue_api_{Guid.NewGuid():N}"));
                services.RemoveAll<IWasteOfficerDashboardService>();
                services.AddSingleton(service.Object);
                services.AddAuthentication("DashboardTest")
                    .AddScheme<AuthenticationSchemeOptions, DashboardTestAuthHandler>("DashboardTest", _ => { });
                services.Configure<AuthenticationOptions>(options =>
                {
                    options.DefaultAuthenticateScheme = "DashboardTest";
                    options.DefaultChallengeScheme = "DashboardTest";
                });
            });
        });
        using var client = host.CreateClient();
        if (role is not null)
            client.DefaultRequestHeaders.Add("X-Test-Role", role);

        var response = await client.GetAsync("/api/v1/dashboard/waste-officer/needs-attention");

        response.StatusCode.Should().Be(expectedStatus);
        if (expectedStatus == HttpStatusCode.OK)
        {
            var json = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            json.ValueKind.Should().Be(System.Text.Json.JsonValueKind.Array);
            json.GetArrayLength().Should().Be(1);
            var item = json[0];
            item.GetProperty("itemType").GetString().Should().Be("WasteReport");
            item.GetProperty("reference").GetString().Should().Be("Report 12345678");
            item.GetProperty("id").GetGuid().Should().Be(Guid.Parse("12345678-0000-0000-0000-000000000001"));
            item.GetProperty("submittedByName").GetString().Should().Be("Kasun Silva");
            item.GetProperty("addressText").GetString().Should().Be("Rajagiriya");
            item.EnumerateObject().Select(property => property.Name)
                .Should().BeEquivalentTo(["id", "itemType", "reference", "createdAt", "secondaryLabel", "submittedByName", "addressText"]);
            service.Verify(x => x.GetNeedsAttentionAsync(It.IsAny<CancellationToken>()), Times.Once());
        }
        else
            service.Verify(x => x.GetNeedsAttentionAsync(It.IsAny<CancellationToken>()), Times.Never());
    }

    private sealed class DashboardTestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Test-Role", out var role))
                return Task.FromResult(AuthenticateResult.NoResult());

            var identity = new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                 new Claim(ClaimTypes.Role, role.ToString())],
                "DashboardTest");
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), "DashboardTest")));
        }
    }
}
