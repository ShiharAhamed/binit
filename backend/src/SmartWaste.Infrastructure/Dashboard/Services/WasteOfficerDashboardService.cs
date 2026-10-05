using Microsoft.EntityFrameworkCore;
using SmartWaste.Application.Dashboard.DTOs;
using SmartWaste.Application.Dashboard.Interfaces;
using SmartWaste.Domain.Collection.Enums;
using SmartWaste.Domain.Complaints.Enums;
using SmartWaste.Domain.Reporting.Enums;
using SmartWaste.Infrastructure.Persistence;

namespace SmartWaste.Infrastructure.Dashboard.Services;

public sealed class WasteOfficerDashboardService(AppDbContext dbContext) : IWasteOfficerDashboardService
{
    public async Task<WasteOfficerDashboardOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        // DbContext does not support concurrent operations. Each count runs in the database.
        var reportsAwaitingReview = await dbContext.WasteReports.AsNoTracking()
            .CountAsync(report => report.Status == WasteReportStatus.Submitted, cancellationToken);
        var activeBins = await dbContext.WasteBins.AsNoTracking()
            .CountAsync(bin => bin.AdministrativeStatus == BinAdministrativeStatus.Active, cancellationToken);
        var scheduledCollections = await dbContext.CollectionTasks.AsNoTracking()
            .CountAsync(task => task.Status == CollectionTaskStatus.Scheduled, cancellationToken);
        var openCollectionTasks = await dbContext.CollectionTasks.AsNoTracking()
            .CountAsync(task => task.Status == CollectionTaskStatus.Assigned ||
                task.Status == CollectionTaskStatus.InProgress, cancellationToken);
        var openComplaints = await dbContext.Complaints.AsNoTracking()
            .CountAsync(complaint => complaint.Status == ComplaintStatus.Submitted, cancellationToken);

        return new WasteOfficerDashboardOverviewDto(
            reportsAwaitingReview, activeBins, scheduledCollections, openCollectionTasks, openComplaints);
    }

    public Task<IReadOnlyList<WasteOfficerNeedsAttentionItemDto>> GetNeedsAttentionAsync(
        CancellationToken cancellationToken = default)
        => DashboardReviewQueueQuery.GetAsync(dbContext, cancellationToken);
}
