using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartWaste.Domain.Reporting.Entities;

namespace SmartWaste.Infrastructure.Reporting.Configurations;

/// <summary>
/// EF Core configuration for ReportAttachment entity.
/// </summary>
public class ReportAttachmentConfiguration : IEntityTypeConfiguration<ReportAttachment>
{
    public void Configure(EntityTypeBuilder<ReportAttachment> builder)
    {
        builder.ToTable("ReportAttachments");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.StorageKey)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(a => a.FileType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.CreatedAt)
            .IsRequired();

        builder.HasOne(a => a.WasteReport)
            .WithMany(r => r.Attachments)
            .HasForeignKey(a => a.WasteReportId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => a.WasteReportId);
    }
}
