using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SmartWaste.Domain.Entities;
using SmartWaste.Domain.Reporting.Entities;
using SmartWaste.Domain.Reporting.Enums;
using SmartWaste.Domain.Workflow.Entities;
using SmartWaste.Domain.Workflow.Enums;
using SmartWaste.Infrastructure.Persistence;
using Xunit;

namespace SmartWaste.Tests.Workflow.Persistence;

[Collection(IntegrationTestCollection.Name)]
public class AgentWorkflowTriggerPostgreSqlTests
{
    private readonly CustomWebApplicationFactory _factory;

    public AgentWorkflowTriggerPostgreSqlTests(CustomWebApplicationFactory factory) => _factory = factory;

    private static async Task<(Guid UserId, Guid ReportId)> SeedAsync(AppDbContext db)
    {
        var userId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        db.Users.Add(new AppUser
        {
            Id = userId,
            UserName = $"trigger_{userId:N}@smartwaste.test",
            Email = $"trigger_{userId:N}@smartwaste.test",
            FullName = "Workflow Trigger Test User",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        db.WasteReports.Add(new WasteReport
        {
            Id = reportId,
            CitizenId = userId,
            Description = "Waste at a test location for workflow persistence.",
            WasteType = WasteType.General,
            Latitude = 6.9271,
            Longitude = 79.8612,
            Status = WasteReportStatus.Submitted,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        return (userId, reportId);
    }

    private static AgentWorkflow Workflow(Guid userId, AgentWorkflowTriggerType trigger, Guid? reportId) => new()
    {
        Objective = "Test report workflow foundation",
        InitiatedByUserId = userId,
        TriggerType = trigger,
        TriggeringWasteReportId = reportId,
        CreatedAt = DateTime.UtcNow
    };

    [Fact]
    public async Task ValidManualAndCitizenWorkflows_PersistWithClaimDefaults()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var (userId, reportId) = await SeedAsync(db);
        db.AgentWorkflows.AddRange(
            Workflow(userId, AgentWorkflowTriggerType.ManualOperationalPlanning, null),
            Workflow(userId, AgentWorkflowTriggerType.CitizenReportSubmission, reportId));
        await db.SaveChangesAsync();

        var rows = await db.AgentWorkflows.AsNoTracking().Where(w => w.InitiatedByUserId == userId).ToListAsync();
        rows.Should().HaveCount(2);
        rows.Single(w => w.TriggerType == AgentWorkflowTriggerType.ManualOperationalPlanning).TriggeringWasteReportId.Should().BeNull();
        var citizen = rows.Single(w => w.TriggerType == AgentWorkflowTriggerType.CitizenReportSubmission);
        citizen.TriggeringWasteReportId.Should().Be(reportId);
        citizen.ProcessingAttemptCount.Should().Be(0);
        citizen.ProcessingLeaseId.Should().BeNull();
        citizen.ProcessingLeaseExpiresAt.Should().BeNull();
    }

    [Theory]
    [InlineData(AgentWorkflowTriggerType.ManualOperationalPlanning, true)]
    [InlineData(AgentWorkflowTriggerType.CitizenReportSubmission, false)]
    public async Task InvalidTriggerReportPair_IsRejectedByDatabase(AgentWorkflowTriggerType trigger, bool useReportId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var (userId, reportId) = await SeedAsync(db);
        db.AgentWorkflows.Add(Workflow(userId, trigger, useReportId ? reportId : null));

        var act = () => db.SaveChangesAsync();
        var error = await act.Should().ThrowAsync<DbUpdateException>();
        error.Which.InnerException.Should().BeOfType<PostgresException>()
            .Which.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);
    }

    [Fact]
    public async Task SecondWorkflowForSameReport_IsRejectedByUniqueIndex()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var (userId, reportId) = await SeedAsync(db);
        db.AgentWorkflows.Add(Workflow(userId, AgentWorkflowTriggerType.CitizenReportSubmission, reportId));
        await db.SaveChangesAsync();
        db.AgentWorkflows.Add(Workflow(userId, AgentWorkflowTriggerType.CitizenReportSubmission, reportId));

        var act = () => db.SaveChangesAsync();
        var error = await act.Should().ThrowAsync<DbUpdateException>();
        error.Which.InnerException.Should().BeOfType<PostgresException>()
            .Which.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
    }

    [Fact]
    public async Task LinkedReportCannotBeDeletedOrSilentlyUnlinked()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var (userId, reportId) = await SeedAsync(db);
        db.AgentWorkflows.Add(Workflow(userId, AgentWorkflowTriggerType.CitizenReportSubmission, reportId));
        await db.SaveChangesAsync();

        var act = () => db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"WasteReports\" WHERE \"Id\" = {reportId}");
        var error = await act.Should().ThrowAsync<PostgresException>();
        error.Which.SqlState.Should().Be(PostgresErrorCodes.ForeignKeyViolation);
    }

    [Fact]
    public async Task NegativeProcessingAttemptCount_IsRejectedByDatabase()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var (userId, _) = await SeedAsync(db);
        var workflow = Workflow(userId, AgentWorkflowTriggerType.ManualOperationalPlanning, null);
        workflow.ProcessingAttemptCount = -1;
        db.AgentWorkflows.Add(workflow);

        var act = () => db.SaveChangesAsync();
        var error = await act.Should().ThrowAsync<DbUpdateException>();
        error.Which.InnerException.Should().BeOfType<PostgresException>()
            .Which.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);
    }
}
