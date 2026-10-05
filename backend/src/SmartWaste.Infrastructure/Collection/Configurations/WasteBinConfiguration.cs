using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartWaste.Domain.Collection.Entities;
using SmartWaste.Domain.Collection.Enums;

namespace SmartWaste.Infrastructure.Collection.Configurations;

/// <summary>
/// EF Core configuration for WasteBin entity.
/// </summary>
public class WasteBinConfiguration : IEntityTypeConfiguration<WasteBin>
{
    public void Configure(EntityTypeBuilder<WasteBin> builder)
    {
        builder.ToTable("WasteBins", t =>
        {
            t.HasCheckConstraint("CK_WasteBins_CapacityLiters", "\"CapacityLiters\" > 0");
            t.HasCheckConstraint("CK_WasteBins_Coordinates",
                "\"Latitude\" >= -90.0 AND \"Latitude\" <= 90.0 AND \"Longitude\" >= -180.0 AND \"Longitude\" <= 180.0");
            t.HasCheckConstraint("CK_WasteBins_AdministrativeStatus",
                "\"AdministrativeStatus\" IN ('Active', 'OutOfService', 'Retired')");
            t.HasCheckConstraint("CK_WasteBins_CollectionWeekdays_Range",
                "\"CollectionWeekdays\" <@ ARRAY[1, 2, 3, 4, 5, 6, 7]");
        });

        builder.HasKey(b => b.Id);

        builder.Property(b => b.BinCode)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(b => b.CapacityLiters)
            .IsRequired();

        builder.Property(b => b.Latitude)
            .IsRequired();

        builder.Property(b => b.Longitude)
            .IsRequired();

        builder.Property(b => b.AddressText)
            .IsRequired(false)
            .HasMaxLength(500);

        builder.Property(b => b.AdministrativeStatus)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50)
            .HasDefaultValue(BinAdministrativeStatus.Active);

        builder.Property(b => b.CollectionWeekdays)
            .IsRequired()
            .HasColumnType("integer[]");

        builder.Property(b => b.LastCollectedAt)
            .IsRequired(false);

        builder.Property(b => b.CreatedAt)
            .IsRequired();

        builder.Property(b => b.UpdatedAt)
            .IsRequired(false);

        // Dependent child relationships
        builder.HasMany(b => b.AcceptedWasteTypes)
            .WithOne(a => a.WasteBin)
            .HasForeignKey(a => a.WasteBinId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(b => b.Observations)
            .WithOne(o => o.WasteBin)
            .HasForeignKey(o => o.WasteBinId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(b => b.CollectionTasks)
            .WithOne(t => t.WasteBin)
            .HasForeignKey(t => t.WasteBinId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(b => b.BinCode)
            .IsUnique();

        builder.HasIndex(b => b.AdministrativeStatus);

        builder.HasIndex(b => new { b.Latitude, b.Longitude });
    }
}
