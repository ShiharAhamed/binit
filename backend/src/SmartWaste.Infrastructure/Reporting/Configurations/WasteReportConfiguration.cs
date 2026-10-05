using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartWaste.Domain.Reporting.Entities;
using SmartWaste.Domain.Reporting.Enums;

namespace SmartWaste.Infrastructure.Reporting.Configurations;

/// <summary>
/// EF Core configuration for WasteReport entity.
/// </summary>
public class WasteReportConfiguration : IEntityTypeConfiguration<WasteReport>
{
    public void Configure(EntityTypeBuilder<WasteReport> builder)
    {
        builder.ToTable("WasteReports");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Description)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(r => r.WasteType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(r => r.Latitude)
            .IsRequired();

        builder.Property(r => r.Longitude)
            .IsRequired();

        builder.Property(r => r.AddressText)
            .IsRequired(false)
            .HasMaxLength(500);

        builder.Property(r => r.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(r => r.Priority)
            .IsRequired(false)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(r => r.CreatedAt)
            .IsRequired();

        builder.Property(r => r.UpdatedAt)
            .IsRequired(false);

        // AppUser relationships
        builder.HasOne(r => r.Citizen)
            .WithMany()
            .HasForeignKey(r => r.CitizenId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.VerifiedByUser)
            .WithMany()
            .HasForeignKey(r => r.VerifiedByUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        // Dependent child relationships
        builder.HasMany(r => r.Attachments)
            .WithOne(a => a.WasteReport)
            .HasForeignKey(a => a.WasteReportId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(r => r.StatusHistory)
            .WithOne(h => h.WasteReport)
            .HasForeignKey(h => h.WasteReportId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        // Operational query indexes
        builder.HasIndex(r => r.Status);
        builder.HasIndex(r => r.CitizenId);
        builder.HasIndex(r => r.CreatedAt);
        builder.HasIndex(r => new { r.Status, r.WasteType });
    }
}
