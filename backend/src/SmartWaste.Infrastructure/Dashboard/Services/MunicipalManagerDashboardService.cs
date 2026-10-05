using Microsoft.EntityFrameworkCore;
using SmartWaste.Application.Dashboard.DTOs;
using SmartWaste.Application.Dashboard.Interfaces;
using SmartWaste.Domain.Collection.Enums;
using SmartWaste.Domain.Complaints.Enums;
using SmartWaste.Domain.Operations.Enums;
using SmartWaste.Domain.Workflow.Enums;
using SmartWaste.Infrastructure.Persistence;

namespace SmartWaste.Infrastructure.Dashboard.Services;

public sealed class MunicipalManagerDashboardService(AppDbContext dbContext) : IMunicipalManagerDashboardService
{
    public async Task<MunicipalManagerDashboardOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        // Run the aggregates sequentially because one EF DbContext cannot execute concurrent queries.
        var aiWorkflowsAwaitingApproval = await dbContext.AgentWorkflows.AsNoTracking()
            .CountAsync(workflow => workflow.Status == AgentWorkflowStatus.AwaitingReportVerification ||
                workflow.Status == AgentWorkflowStatus.AwaitingCollectionApproval ||
                workflow.Status == AgentWorkflowStatus.AwaitingDispatchApproval, cancellationToken);
        var activeCollectionAssignments = await dbContext.CollectionAssignments.AsNoTracking()
            .CountAsync(assignment => assignment.Status == CollectionAssignmentStatus.Assigned ||
                assignment.Status == CollectionAssignmentStatus.InProgress, cancellationToken);
        var availableVehicles = await dbContext.Vehicles.AsNoTracking()
            .CountAsync(vehicle => vehicle.OperationalStatus == VehicleOperationalStatus.Available &&
                !dbContext.CollectionAssignments.Any(assignment => assignment.VehicleId == vehicle.Id &&
                    (assignment.Status == CollectionAssignmentStatus.Assigned ||
                     assignment.Status == CollectionAssignmentStatus.InProgress)), cancellationToken);
        var openOperationalIncidents = await dbContext.OperationalIssues.AsNoTracking()
            .CountAsync(issue => issue.Status == OperationalIssueStatus.Reported ||
                issue.Status == OperationalIssueStatus.InReview, cancellationToken);
        var unresolvedComplaints = await dbContext.Complaints.AsNoTracking()
            .CountAsync(complaint => complaint.Status == ComplaintStatus.Submitted ||
                complaint.Status == ComplaintStatus.InReview, cancellationToken);

        return new MunicipalManagerDashboardOverviewDto(
            aiWorkflowsAwaitingApproval, activeCollectionAssignments, availableVehicles,
            openOperationalIncidents, unresolvedComplaints);
    }

    public Task<IReadOnlyList<WasteOfficerNeedsAttentionItemDto>> GetNeedsAttentionAsync(
        CancellationToken cancellationToken = default)
        => DashboardReviewQueueQuery.GetAsync(dbContext, cancellationToken);
}
