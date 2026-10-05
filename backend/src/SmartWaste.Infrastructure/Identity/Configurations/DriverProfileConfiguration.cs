using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartWaste.Domain.Collection.Enums;
using SmartWaste.Domain.Entities;

namespace SmartWaste.Infrastructure.Identity.Configurations;

/// <summary>
/// EF Core configuration for the one-to-one DriverProfile extension of AppUser.
/// </summary>
public class DriverProfileConfiguration : IEntityTypeConfiguration<DriverProfile>
{
    public void Configure(EntityTypeBuilder<DriverProfile> builder)
    {
        builder.ToTable("DriverProfiles", t =>
        {
            t.HasCheckConstraint("CK_DriverProfiles_AvailabilityStatus",
                "\"AvailabilityStatus\" IN ('Available', 'OffDuty')");
        });

        builder.HasKey(p => p.UserId);

        builder.Property(p => p.LicenseNumber)
            .IsRequired(false)
            .HasMaxLength(50);

        builder.Property(p => p.AvailabilityStatus)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(30)
            .HasDefaultValue(DriverAvailabilityStatus.Available);

        builder.Property(p => p.IsEligible)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(p => p.EligibilityNotes)
            .IsRequired(false)
            .HasMaxLength(500);

        builder.Property(p => p.CreatedAt)
            .IsRequired();

        builder.Property(p => p.UpdatedAt)
            .IsRequired(false);

        builder.HasOne(p => p.User)
            .WithOne(u => u.DriverProfile)
            .HasForeignKey<DriverProfile>(p => p.UserId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => p.LicenseNumber)
            .IsUnique();
    }
}
