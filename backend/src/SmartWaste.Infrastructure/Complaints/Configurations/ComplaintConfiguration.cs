using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartWaste.Domain.Complaints.Entities;
using SmartWaste.Domain.Complaints.Enums;

namespace SmartWaste.Infrastructure.Complaints.Configurations;

/// <summary>
/// EF Core configuration for Complaint entity.
/// Configures scalar fields, string lengths, enum-to-string mappings,
/// AppUser foreign key relationships, optional coordinate storage, and operational indexes.
/// </summary>
public class ComplaintConfiguration : IEntityTypeConfiguration<Complaint>
{
    public void Configure(EntityTypeBuilder<Complaint> builder)
    {
        builder.ToTable("Complaints", t =>
        {
            t.HasCheckConstraint("CK_Complaints_Status",
                "\"Status\" IN ('Submitted', 'InReview', 'Resolved')");
            t.HasCheckConstraint("CK_Complaints_Category",
                "\"Category\" IN ('MissedCollection', 'DelayedService', 'PoorService', 'UnresolvedIssue', 'Other')");
            t.HasCheckConstraint("CK_Complaints_Coordinates",
                "(\"Latitude\" IS NULL AND \"Longitude\" IS NULL) OR (\"Latitude\" IS NOT NULL AND \"Longitude\" IS NOT NULL AND \"Latitude\" >= -90.0 AND \"Latitude\" <= 90.0 AND \"Longitude\" >= -180.0 AND \"Longitude\" <= 180.0)");
        });

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Subject)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(c => c.Description)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(c => c.Category)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(c => c.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(30)
            .HasDefaultValue(ComplaintStatus.Submitted);

        builder.Property(c => c.Latitude)
            .IsRequired(false);

        builder.Property(c => c.Longitude)
            .IsRequired(false);

        builder.Property(c => c.LocationDescription)
            .IsRequired(false)
            .HasMaxLength(500);

        builder.Property(c => c.ResolvedAt)
            .IsRequired(false);

        builder.Property(c => c.ResolutionNote)
            .IsRequired(false)
            .HasMaxLength(1000);

        builder.Property(c => c.CreatedAt)
            .IsRequired();

        builder.Property(c => c.UpdatedAt)
            .IsRequired(false);

        // AppUser relationships
        builder.HasOne(c => c.Citizen)
            .WithMany()
            .HasForeignKey(c => c.CitizenId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.ResolvedByUser)
            .WithMany()
            .HasForeignKey(c => c.ResolvedByUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(c => c.CitizenId);
        builder.HasIndex(c => c.Status);
        builder.HasIndex(c => c.CreatedAt);
        builder.HasIndex(c => new { c.Status, c.Category });
    }
}
