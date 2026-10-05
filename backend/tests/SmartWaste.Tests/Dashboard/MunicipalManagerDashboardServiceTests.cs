using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SmartWaste.Domain.Collection.Entities;
using SmartWaste.Domain.Collection.Enums;
using SmartWaste.Domain.Complaints.Entities;
using SmartWaste.Domain.Complaints.Enums;
using SmartWaste.Domain.Operations.Entities;
using SmartWaste.Domain.Operations.Enums;
using SmartWaste.Domain.Workflow.Entities;
using SmartWaste.Domain.Workflow.Enums;
using SmartWaste.Infrastructure.Dashboard.Services;
using SmartWaste.Infrastructure.Persistence;
using Xunit;

namespace SmartWaste.Tests.Dashboard;

public class MunicipalManagerDashboardServiceTests
{
    [Fact]
    public async Task GetOverviewAsync_CountsOnlyHumanPausesActiveAssignmentsEligibleVehiclesAndUnresolvedRecords()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"manager_dashboard_{Guid.NewGuid():N}").Options);

        db.AgentWorkflows.AddRange(
            Workflow(AgentWorkflowStatus.AwaitingReportVerification),
            Workflow(AgentWorkflowStatus.AwaitingCollectionApproval),
            Workflow(AgentWorkflowStatus.AwaitingDispatchApproval),
            Workflow(AgentWorkflowStatus.CollectionNeedsRevision),
            Workflow(AgentWorkflowStatus.DispatchNeedsRevision),
            Workflow(AgentWorkflowStatus.Planning),
            Workflow(AgentWorkflowStatus.Completed),
            Workflow(AgentWorkflowStatus.Rejected),
            Workflow(AgentWorkflowStatus.Failed));

        var freeVehicle = Vehicle(VehicleOperationalStatus.Available);
        var assignedVehicle = Vehicle(VehicleOperationalStatus.Available);
        var inProgressVehicle = Vehicle(VehicleOperationalStatus.Available);
        var previouslyUsedVehicle = Vehicle(VehicleOperationalStatus.Available);
        db.Vehicles.AddRange(freeVehicle, assignedVehicle, inProgressVehicle, previouslyUsedVehicle,
            Vehicle(VehicleOperationalStatus.Maintenance), Vehicle(VehicleOperationalStatus.Inactive));
        db.CollectionAssignments.AddRange(
            Assignment(CollectionAssignmentStatus.Assigned, assignedVehicle.Id),
            Assignment(CollectionAssignmentStatus.InProgress, inProgressVehicle.Id),
            Assignment(CollectionAssignmentStatus.Completed, previouslyUsedVehicle.Id),
            Assignment(CollectionAssignmentStatus.PartiallyCompleted, Guid.NewGuid()),
            Assignment(CollectionAssignmentStatus.Failed, Guid.NewGuid()),
            Assignment(CollectionAssignmentStatus.Cancelled, Guid.NewGuid()));
        db.OperationalIssues.AddRange(
            Issue(OperationalIssueStatus.Reported), Issue(OperationalIssueStatus.InReview),
            Issue(OperationalIssueStatus.Resolved));
        db.Complaints.AddRange(
            Complaint(ComplaintStatus.Submitted), Complaint(ComplaintStatus.InReview),
            Complaint(ComplaintStatus.Resolved));
        await db.SaveChangesAsync();

        var result = await new MunicipalManagerDashboardService(db).GetOverviewAsync();

        result.AiWorkflowsAwaitingApproval.Should().Be(3);
        result.ActiveCollectionAssignments.Should().Be(2);
        result.AvailableVehicles.Should().Be(2); // Free and previously completed only.
        result.OpenOperationalIncidents.Should().Be(2);
        result.UnresolvedComplaints.Should().Be(2);
    }

    private static AgentWorkflow Workflow(AgentWorkflowStatus status) => new() { Status = status };
    private static Vehicle Vehicle(VehicleOperationalStatus status) => new() { OperationalStatus = status };
    private static CollectionAssignment Assignment(CollectionAssignmentStatus status, Guid vehicleId) =>
        new() { Status = status, VehicleId = vehicleId };
    private static OperationalIssue Issue(OperationalIssueStatus status) => new() { Status = status };
    private static Complaint Complaint(ComplaintStatus status) => new() { Status = status };
}
