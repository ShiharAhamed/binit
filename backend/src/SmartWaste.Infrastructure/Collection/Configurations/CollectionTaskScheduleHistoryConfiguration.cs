using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartWaste.Domain.Collection.Entities;

namespace SmartWaste.Infrastructure.Collection.Configurations;

/// <summary>
/// EF Core configuration for CollectionTaskScheduleHistory entity.
/// </summary>
public class CollectionTaskScheduleHistoryConfiguration : IEntityTypeConfiguration<CollectionTaskScheduleHistory>
{
    public void Configure(EntityTypeBuilder<CollectionTaskScheduleHistory> builder)
    {
        builder.ToTable("CollectionTaskScheduleHistories");

        builder.HasKey(h => h.Id);

        builder.Property(h => h.CollectionTaskId)
            .IsRequired();

        builder.Property(h => h.PreviousScheduledAt)
            .IsRequired();

        builder.Property(h => h.NewScheduledAt)
            .IsRequired();

        builder.Property(h => h.Reason)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(h => h.RescheduledByUserId)
            .IsRequired();

        builder.Property(h => h.RescheduledAt)
            .IsRequired();

        // Relationships
        builder.HasOne(h => h.CollectionTask)
            .WithMany(t => t.ScheduleHistory)
            .HasForeignKey(h => h.CollectionTaskId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(h => h.RescheduledByUser)
            .WithMany()
            .HasForeignKey(h => h.RescheduledByUserId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(h => new { h.CollectionTaskId, h.RescheduledAt });
        builder.HasIndex(h => h.RescheduledByUserId);
    }
}
