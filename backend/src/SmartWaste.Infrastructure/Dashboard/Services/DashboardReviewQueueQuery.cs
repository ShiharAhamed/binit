using Microsoft.EntityFrameworkCore;
using SmartWaste.Application.Dashboard.DTOs;
using SmartWaste.Application.Reporting;
using SmartWaste.Domain.Complaints.Enums;
using SmartWaste.Domain.Reporting.Enums;
using SmartWaste.Infrastructure.Persistence;

namespace SmartWaste.Infrastructure.Dashboard.Services;

internal static class DashboardReviewQueueQuery
{
    public static async Task<IReadOnlyList<WasteOfficerNeedsAttentionItemDto>> GetAsync(
        AppDbContext dbContext, CancellationToken cancellationToken)
    {
        const int limit = 5;
        // The projections join only the submitter's display name; no user profile is materialized.
        var reports = await (
            from report in dbContext.WasteReports.AsNoTracking()
            join user in dbContext.Users.AsNoTracking() on report.CitizenId equals user.Id into submitters
            from submitter in submitters.DefaultIfEmpty()
            where report.Status == WasteReportStatus.Submitted
            orderby report.CreatedAt descending, report.Id descending
            select new
            {
                report.Id, report.CreatedAt, report.WasteType, report.AddressText,
                SubmitterName = submitter == null ? null : submitter.FullName
            })
            .Take(limit)
            .ToListAsync(cancellationToken);

        var complaints = await (
            from complaint in dbContext.Complaints.AsNoTracking()
            join user in dbContext.Users.AsNoTracking() on complaint.CitizenId equals user.Id into submitters
            from submitter in submitters.DefaultIfEmpty()
            where complaint.Status == ComplaintStatus.Submitted
            orderby complaint.CreatedAt descending, complaint.Id descending
            select new
            {
                complaint.Id, complaint.CreatedAt, complaint.Category, complaint.LocationDescription,
                SubmitterName = submitter == null ? null : submitter.FullName
            })
            .Take(limit)
            .ToListAsync(cancellationToken);

        return reports.Select(report => new WasteOfficerNeedsAttentionItemDto(
                report.Id, WasteOfficerNeedsAttentionItemType.WasteReport,
                $"Report {WasteReportReference.FromId(report.Id)}", report.CreatedAt,
                report.WasteType.ToString(), DisplayName(report.SubmitterName),
                ItemAddress(report.AddressText)))
            .Concat(complaints.Select(complaint => new WasteOfficerNeedsAttentionItemDto(
                complaint.Id, WasteOfficerNeedsAttentionItemType.Complaint,
                $"Complaint {complaint.Id.ToString("N")[..8].ToUpperInvariant()}",
                complaint.CreatedAt, complaint.Category switch
                {
                    ComplaintCategory.MissedCollection => "Missed collection",
                    ComplaintCategory.DelayedService => "Delayed service",
                    ComplaintCategory.PoorService => "Poor service",
                    ComplaintCategory.UnresolvedIssue => "Unresolved issue",
                    _ => "Other"
                }, DisplayName(complaint.SubmitterName), ItemAddress(complaint.LocationDescription))))
            .OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.Id)
            .ThenBy(item => item.ItemType)
            .Take(limit)
            .ToArray();
    }

    private static string DisplayName(string? name) => string.IsNullOrWhiteSpace(name) ? "Citizen" : name.Trim();
    private static string? ItemAddress(string? address) => string.IsNullOrWhiteSpace(address) ? null : address.Trim();
}
