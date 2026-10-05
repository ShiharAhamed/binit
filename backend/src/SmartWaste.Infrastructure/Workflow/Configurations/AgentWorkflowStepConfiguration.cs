using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartWaste.Domain.Workflow.Entities;
using SmartWaste.Domain.Workflow.Enums;

namespace SmartWaste.Infrastructure.Workflow.Configurations;

public class AgentWorkflowStepConfiguration : IEntityTypeConfiguration<AgentWorkflowStep>
{
    public void Configure(EntityTypeBuilder<AgentWorkflowStep> builder)
    {
        builder.ToTable("AgentWorkflowSteps", t =>
        {
            t.HasCheckConstraint(
                "CK_AgentWorkflowSteps_Sequence",
                "\"Sequence\" > 0");

            t.HasCheckConstraint(
                "CK_AgentWorkflowSteps_Status",
                "\"Status\" IN ('Pending', 'Running', 'Completed', 'Failed', 'Skipped')");
        });

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Sequence)
            .IsRequired();

        builder.Property(s => s.StepType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(s => s.AgentName)
            .HasMaxLength(100);

        builder.Property(s => s.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(30)
            .HasDefaultValue(WorkflowStepStatus.Pending);

        builder.Property(s => s.InputJson)
            .HasColumnType("jsonb");

        builder.Property(s => s.OutputJson)
            .HasColumnType("jsonb");

        builder.Property(s => s.ValidationJson)
            .HasColumnType("jsonb");

        builder.Property(s => s.ErrorMessage)
            .HasMaxLength(2000);

        builder.HasIndex(s => new { s.WorkflowId, s.Sequence })
            .IsUnique()
            .HasDatabaseName("IX_AgentWorkflowSteps_WorkflowId_Sequence");
    }
}
