using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartWaste.Domain.Reporting.Entities;

namespace SmartWaste.Infrastructure.Reporting.Configurations;

/// <summary>
/// EF Core configuration for WasteReportStatusHistory entity.
/// </summary>
public class WasteReportStatusHistoryConfiguration : IEntityTypeConfiguration<WasteReportStatusHistory>
{
    public void Configure(EntityTypeBuilder<WasteReportStatusHistory> builder)
    {
        builder.ToTable("WasteReportStatusHistories");

        builder.HasKey(h => h.Id);

        builder.Property(h => h.FromStatus)
            .IsRequired(false)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(h => h.ToStatus)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(h => h.Notes)
            .IsRequired(false)
            .HasMaxLength(500);

        builder.Property(h => h.ChangedAt)
            .IsRequired();

        builder.HasOne(h => h.WasteReport)
            .WithMany(r => r.StatusHistory)
            .HasForeignKey(h => h.WasteReportId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(h => h.ChangedByUser)
            .WithMany()
            .HasForeignKey(h => h.ChangedByUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(h => h.WasteReportId);
        builder.HasIndex(h => h.ChangedAt);
    }
}
