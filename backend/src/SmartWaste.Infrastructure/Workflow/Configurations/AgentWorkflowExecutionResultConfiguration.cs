using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartWaste.Domain.Workflow.Entities;

namespace SmartWaste.Infrastructure.Workflow.Configurations;

public class AgentWorkflowExecutionResultConfiguration : IEntityTypeConfiguration<AgentWorkflowExecutionResult>
{
    public void Configure(EntityTypeBuilder<AgentWorkflowExecutionResult> builder)
    {
        builder.ToTable("AgentWorkflowExecutionResults");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.ExecutionType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(e => e.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(e => e.ResultJson)
            .HasColumnType("jsonb");

        builder.Property(e => e.ErrorMessage)
            .HasMaxLength(2000);

        builder.Property(e => e.ExecutedAt)
            .IsRequired();

        builder.HasOne(e => e.WorkflowStep)
            .WithMany()
            .HasForeignKey(e => e.WorkflowStepId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(e => e.WorkflowId)
            .HasDatabaseName("IX_AgentWorkflowExecutionResults_WorkflowId");
    }
}
