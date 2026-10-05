using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartWaste.Domain.Collection.Entities;
using SmartWaste.Domain.Collection.Enums;

namespace SmartWaste.Infrastructure.Collection.Configurations;

/// <summary>
/// EF Core configuration for a municipal fleet vehicle.
/// </summary>
public class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.ToTable("Vehicles", t =>
        {
            t.HasCheckConstraint("CK_Vehicles_CapacityLiters", "\"CapacityLiters\" > 0");
            t.HasCheckConstraint("CK_Vehicles_VehicleType",
                "\"VehicleType\" IN ('Compactor', 'Flatbed', 'Tipper', 'SmallVan')");
            t.HasCheckConstraint("CK_Vehicles_OperationalStatus",
                "\"OperationalStatus\" IN ('Available', 'Maintenance', 'Inactive')");
        });

        builder.HasKey(v => v.Id);

        builder.Property(v => v.RegistrationNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(v => v.VehicleType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(v => v.CapacityLiters)
            .IsRequired();

        builder.Property(v => v.OperationalStatus)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(30)
            .HasDefaultValue(VehicleOperationalStatus.Available);

        builder.Property(v => v.Notes)
            .IsRequired(false)
            .HasMaxLength(1000);

        builder.Property(v => v.CreatedAt)
            .IsRequired();

        builder.Property(v => v.UpdatedAt)
            .IsRequired(false);

        builder.HasMany(v => v.SupportedWasteTypes)
            .WithOne(w => w.Vehicle)
            .HasForeignKey(w => w.VehicleId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(v => v.RegistrationNumber)
            .IsUnique();
    }
}
