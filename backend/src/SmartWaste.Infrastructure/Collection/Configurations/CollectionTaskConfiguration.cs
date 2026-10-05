using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartWaste.Domain.Collection.Entities;
using SmartWaste.Domain.Collection.Enums;

namespace SmartWaste.Infrastructure.Collection.Configurations;

/// <summary>
/// EF Core configuration for CollectionTask entity.
/// </summary>
public class CollectionTaskConfiguration : IEntityTypeConfiguration<CollectionTask>
{
    public void Configure(EntityTypeBuilder<CollectionTask> builder)
    {
        builder.ToTable("CollectionTasks", t =>
        {
            t.HasCheckConstraint("CK_CollectionTasks_Target_XOR",
                "(\"WasteReportId\" IS NOT NULL AND \"WasteBinId\" IS NULL) OR (\"WasteReportId\" IS NULL AND \"WasteBinId\" IS NOT NULL)");

            t.HasCheckConstraint("CK_CollectionTasks_Reason_Consistency",
                "(\"WasteReportId\" IS NOT NULL AND \"CollectionReason\" = 'VerifiedReport') OR (\"WasteBinId\" IS NOT NULL AND \"CollectionReason\" IN ('FullOrBlockedBin', 'RoutineCollection', 'OfficerDiscretion'))");

            t.HasCheckConstraint("CK_CollectionTasks_OfficerDiscretion_Reason",
                "(\"CollectionReason\" != 'OfficerDiscretion') OR (\"SchedulingReason\" IS NOT NULL AND LENGTH(TRIM(\"SchedulingReason\")) > 0)");

            t.HasCheckConstraint("CK_CollectionTasks_Status",
                "\"Status\" IN ('Scheduled', 'Assigned', 'InProgress', 'Completed', 'Failed', 'Cancelled')");
        });

        builder.HasKey(t => t.Id);

        builder.Property(t => t.TaskCode)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(t => t.WasteReportId)
            .IsRequired(false);

        builder.Property(t => t.WasteBinId)
            .IsRequired(false);

        builder.Property(t => t.CollectionReason)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(t => t.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50)
            .HasDefaultValue(CollectionTaskStatus.Scheduled);

        builder.Property(t => t.ScheduledAt)
            .IsRequired();

        builder.Property(t => t.HandlingNotes)
            .IsRequired(false)
            .HasMaxLength(1000);

        builder.Property(t => t.SchedulingReason)
            .IsRequired(false)
            .HasMaxLength(500);

        builder.Property(t => t.CreatedByUserId)
            .IsRequired();

        builder.Property(t => t.CreationMethod)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(30)
            .HasDefaultValue(TaskCreationMethod.Manual);

        builder.Property(t => t.TriggerObservationId)
            .IsRequired(false);

        builder.Property(t => t.RoutineDueDate)
            .IsRequired(false)
            .HasColumnType("date");

        builder.Property(t => t.CreatedAt)
            .IsRequired();

        builder.Property(t => t.UpdatedAt)
            .IsRequired(false);

        // Relationships
        builder.HasOne(t => t.WasteReport)
            .WithMany()
            .HasForeignKey(t => t.WasteReportId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.WasteBin)
            .WithMany(b => b.CollectionTasks)
            .HasForeignKey(t => t.WasteBinId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.CreatedByUser)
            .WithMany()
            .HasForeignKey(t => t.CreatedByUserId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.TriggerObservation)
            .WithMany()
            .HasForeignKey(t => t.TriggerObservationId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(t => t.StatusHistory)
            .WithOne(h => h.CollectionTask)
            .HasForeignKey(h => h.CollectionTaskId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.ScheduleHistory)
            .WithOne(h => h.CollectionTask)
            .HasForeignKey(h => h.CollectionTaskId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        // Unique index on TaskCode
        builder.HasIndex(t => t.TaskCode)
            .IsUnique();

        // Partial unique indexes for active tasks (preventing concurrent active tasks for same target)
        builder.HasIndex(t => t.WasteReportId)
            .IsUnique()
            .HasDatabaseName("IX_CollectionTasks_WasteReportId_Active")
            .HasFilter("\"WasteReportId\" IS NOT NULL AND \"Status\" IN ('Scheduled', 'Assigned', 'InProgress')");

        builder.HasIndex(t => t.WasteBinId)
            .IsUnique()
            .HasDatabaseName("IX_CollectionTasks_WasteBinId_Active")
            .HasFilter("\"WasteBinId\" IS NOT NULL AND \"Status\" IN ('Scheduled', 'Assigned', 'InProgress')");

        // Operational query indexes
        builder.HasIndex(t => new { t.Status, t.ScheduledAt });
        builder.HasIndex(t => t.CreatedByUserId);
    }
}
