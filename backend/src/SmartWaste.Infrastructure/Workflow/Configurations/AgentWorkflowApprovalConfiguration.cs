using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartWaste.Domain.Workflow.Entities;

namespace SmartWaste.Infrastructure.Workflow.Configurations;

public class AgentWorkflowApprovalConfiguration : IEntityTypeConfiguration<AgentWorkflowApproval>
{
    public void Configure(EntityTypeBuilder<AgentWorkflowApproval> builder)
    {
        builder.ToTable("AgentWorkflowApprovals");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.ApprovalStage)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(a => a.Decision)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(a => a.DecisionReason)
            .HasMaxLength(1000);

        builder.Property(a => a.DecisionPayloadJson)
            .HasColumnType("jsonb");

        builder.Property(a => a.DecidedAt)
            .IsRequired();

        builder.HasOne(a => a.DecidedByUser)
            .WithMany()
            .HasForeignKey(a => a.DecidedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.WorkflowStep)
            .WithMany()
            .HasForeignKey(a => a.WorkflowStepId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(a => a.WorkflowId)
            .HasDatabaseName("IX_AgentWorkflowApprovals_WorkflowId");
    }
}
