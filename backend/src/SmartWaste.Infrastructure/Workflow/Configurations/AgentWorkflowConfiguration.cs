using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartWaste.Domain.Workflow.Entities;
using SmartWaste.Domain.Workflow.Enums;

namespace SmartWaste.Infrastructure.Workflow.Configurations;

public class AgentWorkflowConfiguration : IEntityTypeConfiguration<AgentWorkflow>
{
    public void Configure(EntityTypeBuilder<AgentWorkflow> builder)
    {
        builder.ToTable("AgentWorkflows", t =>
        {
            t.HasCheckConstraint(
                "CK_AgentWorkflows_Status",
                "\"Status\" IN ('Created', 'Planning', 'AwaitingCollectionApproval', 'CollectionNeedsRevision', " +
                "'CollectionApproved', 'CreatingScheduledTasks', 'FleetPlanning', 'OperationalValidation', " +
                "'AwaitingDispatchApproval', 'DispatchNeedsRevision', 'DispatchApproved', 'ExecutingAssignments', " +
                "'Completed', 'Rejected', 'Failed', 'AwaitingReportVerification')");

            t.HasCheckConstraint(
                "CK_AgentWorkflows_Objective",
                "LENGTH(TRIM(\"Objective\")) >= 5");

            t.HasCheckConstraint(
                "CK_AgentWorkflows_TriggerReport",
                "(\"TriggerType\" = 'ManualOperationalPlanning' AND \"TriggeringWasteReportId\" IS NULL) OR " +
                "(\"TriggerType\" = 'CitizenReportSubmission' AND \"TriggeringWasteReportId\" IS NOT NULL)");

            t.HasCheckConstraint(
                "CK_AgentWorkflows_ProcessingAttemptCount",
                "\"ProcessingAttemptCount\" >= 0");
        });

        builder.HasKey(w => w.Id);

        builder.Property(w => w.Objective)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(w => w.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50)
            .HasDefaultValue(AgentWorkflowStatus.Created);

        builder.Property(w => w.TriggerType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50)
            .HasDefaultValue(AgentWorkflowTriggerType.ManualOperationalPlanning);

        builder.Property(w => w.ProcessingLeaseExpiresAt)
            .HasColumnType("timestamp with time zone");

        builder.Property(w => w.ProcessingAttemptCount)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(w => w.CurrentStep)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50)
            .HasDefaultValue(WorkflowStepType.None);

        builder.Property(w => w.FinalOutcome)
            .HasMaxLength(1000);

        builder.Property(w => w.Version)
            .IsRequired()
            .IsConcurrencyToken()
            .HasDefaultValue(1);

        builder.Property(w => w.CreatedAt)
            .IsRequired();

        // Foreign keys and relationships
        builder.HasOne(w => w.InitiatedByUser)
            .WithMany()
            .HasForeignKey(w => w.InitiatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(w => w.TriggeringWasteReport)
            .WithMany()
            .HasForeignKey(w => w.TriggeringWasteReportId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(w => w.Steps)
            .WithOne(s => s.Workflow)
            .HasForeignKey(s => s.WorkflowId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(w => w.Transitions)
            .WithOne(t => t.Workflow)
            .HasForeignKey(t => t.WorkflowId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(w => w.Approvals)
            .WithOne(a => a.Workflow)
            .HasForeignKey(a => a.WorkflowId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(w => w.ExecutionResults)
            .WithOne(e => e.Workflow)
            .HasForeignKey(e => e.WorkflowId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(w => w.Status)
            .HasDatabaseName("IX_AgentWorkflows_Status");

        builder.HasIndex(w => w.CreatedAt)
            .HasDatabaseName("IX_AgentWorkflows_CreatedAt");

        builder.HasIndex(w => w.InitiatedByUserId)
            .HasDatabaseName("IX_AgentWorkflows_InitiatedByUserId");

        builder.HasIndex(w => w.TriggerType)
            .HasDatabaseName("IX_AgentWorkflows_TriggerType");

        builder.HasIndex(w => w.TriggeringWasteReportId)
            .IsUnique()
            .HasFilter("\"TriggeringWasteReportId\" IS NOT NULL")
            .HasDatabaseName("IX_AgentWorkflows_TriggeringWasteReportId");
    }
}
