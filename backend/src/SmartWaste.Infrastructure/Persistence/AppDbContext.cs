using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SmartWaste.Domain.Collection.Entities;
using SmartWaste.Domain.Complaints.Entities;
using SmartWaste.Domain.Entities;
using SmartWaste.Domain.Operations.Entities;
using SmartWaste.Domain.Reporting.Entities;
using SmartWaste.Domain.Workflow.Entities;

namespace SmartWaste.Infrastructure.Persistence;

/// <summary>
/// Main EF Core database context configured for ASP.NET Core Identity with AppUser.
/// Business DbSets will be added in subsequent component implementations.
/// </summary>
public class AppDbContext : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    // Component 1 — Waste Reporting & Citizen Management
    public DbSet<WasteReport> WasteReports => Set<WasteReport>();
    public DbSet<ReportAttachment> ReportAttachments => Set<ReportAttachment>();
    public DbSet<WasteReportStatusHistory> WasteReportStatusHistories => Set<WasteReportStatusHistory>();

    // Component 2 — Waste Collection & Bin Management
    public DbSet<WasteBin> WasteBins => Set<WasteBin>();
    public DbSet<WasteBinAcceptedWasteType> WasteBinAcceptedWasteTypes => Set<WasteBinAcceptedWasteType>();
    public DbSet<BinObservation> BinObservations => Set<BinObservation>();
    public DbSet<CollectionTask> CollectionTasks => Set<CollectionTask>();
    public DbSet<CollectionTaskStatusHistory> CollectionTaskStatusHistories => Set<CollectionTaskStatusHistory>();
    public DbSet<CollectionTaskScheduleHistory> CollectionTaskScheduleHistories => Set<CollectionTaskScheduleHistory>();

    // Component 3 — Fleet and driver persistence foundation
    public DbSet<DriverProfile> DriverProfiles => Set<DriverProfile>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<VehicleSupportedWasteType> VehicleSupportedWasteTypes => Set<VehicleSupportedWasteType>();
    public DbSet<CollectionAssignment> CollectionAssignments => Set<CollectionAssignment>();
    public DbSet<CollectionAssignmentTaskClaim> CollectionAssignmentTaskClaims => Set<CollectionAssignmentTaskClaim>();
    public DbSet<CollectionAssignmentStatusHistory> CollectionAssignmentStatusHistories => Set<CollectionAssignmentStatusHistory>();
    public DbSet<Route> Routes => Set<Route>();
    public DbSet<RouteStop> RouteStops => Set<RouteStop>();
    public DbSet<RouteStopStatusHistory> RouteStopStatusHistories => Set<RouteStopStatusHistory>();

    // Component 4 — Complaints & Driver Operational Issues
    public DbSet<Complaint> Complaints => Set<Complaint>();
    public DbSet<OperationalIssue> OperationalIssues => Set<OperationalIssue>();

    // Agentic AI Workflow Persistence Foundation
    public DbSet<AgentWorkflow> AgentWorkflows => Set<AgentWorkflow>();
    public DbSet<AgentWorkflowStep> AgentWorkflowSteps => Set<AgentWorkflowStep>();
    public DbSet<AgentWorkflowTransition> AgentWorkflowTransitions => Set<AgentWorkflowTransition>();
    public DbSet<AgentWorkflowApproval> AgentWorkflowApprovals => Set<AgentWorkflowApproval>();
    public DbSet<AgentWorkflowExecutionResult> AgentWorkflowExecutionResults => Set<AgentWorkflowExecutionResult>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasSequence<long>("CollectionAssignmentNumberSequence");
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // AppUser configuration
        builder.Entity<AppUser>(entity =>
        {
            entity.Property(u => u.FullName)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(u => u.IsActive)
                .HasDefaultValue(true);

            entity.Property(u => u.MustChangePassword)
                .HasDefaultValue(false)
                .IsRequired();

            entity.Property(u => u.CreatedAt)
                .IsRequired();
        });
    }
}
