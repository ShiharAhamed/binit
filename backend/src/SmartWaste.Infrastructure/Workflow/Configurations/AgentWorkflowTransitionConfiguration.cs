using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartWaste.Domain.Workflow.Entities;

namespace SmartWaste.Infrastructure.Workflow.Configurations;

public class AgentWorkflowTransitionConfiguration : IEntityTypeConfiguration<AgentWorkflowTransition>
{
    public void Configure(EntityTypeBuilder<AgentWorkflowTransition> builder)
    {
        builder.ToTable("AgentWorkflowTransitions");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.FromStatus)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(t => t.ToStatus)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(t => t.Reason)
            .HasMaxLength(500);

        builder.Property(t => t.ChangedAt)
            .IsRequired();

        builder.HasOne(t => t.ChangedByUser)
            .WithMany()
            .HasForeignKey(t => t.ChangedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(t => new { t.WorkflowId, t.ChangedAt })
            .HasDatabaseName("IX_AgentWorkflowTransitions_WorkflowId_ChangedAt");
    }
}
