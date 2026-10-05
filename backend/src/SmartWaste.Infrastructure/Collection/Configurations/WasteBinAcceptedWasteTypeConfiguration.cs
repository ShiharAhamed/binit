using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartWaste.Domain.Collection.Entities;
using SmartWaste.Domain.Reporting.Enums;

namespace SmartWaste.Infrastructure.Collection.Configurations;

/// <summary>
/// EF Core configuration for WasteBinAcceptedWasteType join entity.
/// </summary>
public class WasteBinAcceptedWasteTypeConfiguration : IEntityTypeConfiguration<WasteBinAcceptedWasteType>
{
    public void Configure(EntityTypeBuilder<WasteBinAcceptedWasteType> builder)
    {
        builder.ToTable("WasteBinAcceptedWasteTypes");

        builder.HasKey(a => new { a.WasteBinId, a.WasteType });

        builder.Property(a => a.WasteBinId)
            .IsRequired();

        builder.Property(a => a.WasteType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.HasOne(a => a.WasteBin)
            .WithMany(b => b.AcceptedWasteTypes)
            .HasForeignKey(a => a.WasteBinId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
