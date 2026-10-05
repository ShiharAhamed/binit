using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartWaste.Domain.Collection.Entities;
using SmartWaste.Domain.Collection.Enums;

namespace SmartWaste.Infrastructure.Collection.Configurations;

/// <summary>
/// EF Core configuration for BinObservation entity.
/// </summary>
public class BinObservationConfiguration : IEntityTypeConfiguration<BinObservation>
{
    public void Configure(EntityTypeBuilder<BinObservation> builder)
    {
        builder.ToTable("BinObservations", t =>
        {
            t.HasCheckConstraint("CK_BinObservations_FillLevelPercent",
                "\"FillLevelPercent\" IN (0, 25, 50, 75, 100)");
            t.HasCheckConstraint("CK_BinObservations_Condition",
                "\"Condition\" IN ('Good', 'Damaged', 'Blocked', 'Missing')");
        });

        builder.HasKey(o => o.Id);

        builder.Property(o => o.WasteBinId)
            .IsRequired();

        builder.Property(o => o.FillLevelPercent)
            .IsRequired();

        builder.Property(o => o.Condition)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(o => o.Notes)
            .IsRequired(false)
            .HasMaxLength(500);

        builder.Property(o => o.RecordedByUserId)
            .IsRequired();

        builder.Property(o => o.RecordedAt)
            .IsRequired();

        // Foreign keys
        builder.HasOne(o => o.WasteBin)
            .WithMany(b => b.Observations)
            .HasForeignKey(o => o.WasteBinId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(o => o.RecordedByUser)
            .WithMany()
            .HasForeignKey(o => o.RecordedByUserId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(o => new { o.WasteBinId, o.RecordedAt })
            .IsDescending(false, true);

        builder.HasIndex(o => o.RecordedByUserId);
    }
}
