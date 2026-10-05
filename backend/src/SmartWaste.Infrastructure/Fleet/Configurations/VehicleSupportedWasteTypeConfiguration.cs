using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartWaste.Domain.Collection.Entities;
using SmartWaste.Domain.Reporting.Enums;

namespace SmartWaste.Infrastructure.Collection.Configurations;

/// <summary>
/// EF Core configuration for structured vehicle waste-type compatibility data.
/// </summary>
public class VehicleSupportedWasteTypeConfiguration : IEntityTypeConfiguration<VehicleSupportedWasteType>
{
    public void Configure(EntityTypeBuilder<VehicleSupportedWasteType> builder)
    {
        builder.ToTable("VehicleSupportedWasteTypes");

        builder.HasKey(v => new { v.VehicleId, v.WasteType });

        builder.Property(v => v.VehicleId)
            .IsRequired();

        builder.Property(v => v.WasteType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.HasOne(v => v.Vehicle)
            .WithMany(vehicle => vehicle.SupportedWasteTypes)
            .HasForeignKey(v => v.VehicleId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
