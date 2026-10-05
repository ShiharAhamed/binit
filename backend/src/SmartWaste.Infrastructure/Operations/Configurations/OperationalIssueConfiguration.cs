using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartWaste.Domain.Operations.Entities;
using SmartWaste.Domain.Operations.Enums;

namespace SmartWaste.Infrastructure.Operations.Configurations;

/// <summary>
/// EF Core configuration for OperationalIssue entity.
/// Configures scalar fields, string lengths, enum-to-string mappings,
/// AppUser foreign key relationships, optional coordinate storage, and operational indexes.
/// </summary>
public class OperationalIssueConfiguration : IEntityTypeConfiguration<OperationalIssue>
{
    public void Configure(EntityTypeBuilder<OperationalIssue> builder)
    {
        builder.ToTable("OperationalIssues", t =>
        {
            t.HasCheckConstraint("CK_OperationalIssues_Status",
                "\"Status\" IN ('Reported', 'InReview', 'Resolved')");
            t.HasCheckConstraint("CK_OperationalIssues_Type",
                "\"IssueType\" IN ('VehicleProblem', 'RoadOrAccessIssue', 'EquipmentProblem', 'SafetyConcern', 'OperationalDelay', 'Other')");
            t.HasCheckConstraint("CK_OperationalIssues_Coordinates",
                "(\"Latitude\" IS NULL AND \"Longitude\" IS NULL) OR (\"Latitude\" IS NOT NULL AND \"Longitude\" IS NOT NULL AND \"Latitude\" >= -90.0 AND \"Latitude\" <= 90.0 AND \"Longitude\" >= -180.0 AND \"Longitude\" <= 180.0)");
        });

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(i => i.Description)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(i => i.IssueType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(i => i.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(30)
            .HasDefaultValue(OperationalIssueStatus.Reported);

        builder.Property(i => i.Latitude)
            .IsRequired(false);

        builder.Property(i => i.Longitude)
            .IsRequired(false);

        builder.Property(i => i.LocationDescription)
            .IsRequired(false)
            .HasMaxLength(500);

        builder.Property(i => i.ResolvedAt)
            .IsRequired(false);

        builder.Property(i => i.ResolutionNote)
            .IsRequired(false)
            .HasMaxLength(1000);

        builder.Property(i => i.CreatedAt)
            .IsRequired();

        builder.Property(i => i.UpdatedAt)
            .IsRequired(false);

        // AppUser relationships
        builder.HasOne(i => i.Driver)
            .WithMany()
            .HasForeignKey(i => i.DriverId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.ResolvedByUser)
            .WithMany()
            .HasForeignKey(i => i.ResolvedByUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(i => i.DriverId);
        builder.HasIndex(i => i.Status);
        builder.HasIndex(i => i.CreatedAt);
        builder.HasIndex(i => new { i.Status, i.IssueType });
    }
}
