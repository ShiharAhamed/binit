using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartWaste.Domain.Collection.Entities;
using SmartWaste.Domain.Collection.Enums;

namespace SmartWaste.Infrastructure.Collection.Configurations;

/// <summary>
/// EF Core configuration for CollectionTaskStatusHistory entity.
/// </summary>
public class CollectionTaskStatusHistoryConfiguration : IEntityTypeConfiguration<CollectionTaskStatusHistory>
{
    public void Configure(EntityTypeBuilder<CollectionTaskStatusHistory> builder)
    {
        builder.ToTable("CollectionTaskStatusHistories");

        builder.HasKey(h => h.Id);

        builder.Property(h => h.CollectionTaskId)
            .IsRequired();

        builder.Property(h => h.FromStatus)
            .IsRequired(false)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(h => h.ToStatus)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(h => h.ChangedByUserId)
            .IsRequired(false);

        builder.Property(h => h.Notes)
            .IsRequired(false)
            .HasMaxLength(1000);

        builder.Property(h => h.ChangedAt)
            .IsRequired();

        // Relationships
        builder.HasOne(h => h.CollectionTask)
            .WithMany(t => t.StatusHistory)
            .HasForeignKey(h => h.CollectionTaskId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(h => h.ChangedByUser)
            .WithMany()
            .HasForeignKey(h => h.ChangedByUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(h => new { h.CollectionTaskId, h.ChangedAt });
        builder.HasIndex(h => h.ChangedByUserId);
    }
}
