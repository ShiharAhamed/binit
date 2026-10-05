using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using SmartWaste.Domain.Complaints.Entities;
using SmartWaste.Domain.Complaints.Enums;
using SmartWaste.Domain.Entities;
using SmartWaste.Infrastructure.Persistence;
using Xunit;

namespace SmartWaste.Tests.Complaints.Persistence;

public class ComplaintPersistenceModelTests
{
    private static AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase($"SmartWaste_C4_Complaint_Model_{Guid.NewGuid():N}")
        .Options);

    private static IModel GetDesignTimeModel(AppDbContext db) =>
        db.GetService<IDesignTimeModel>().Model;

    [Fact]
    public void Complaint_Model_ConfiguresTableColumnsLengthsAndEnumsCorrectly()
    {
        using var db = CreateContext();
        var complaint = GetDesignTimeModel(db).FindEntityType(typeof(Complaint))!;

        complaint.GetTableName().Should().Be("Complaints");
        complaint.FindPrimaryKey()!.Properties.Select(p => p.Name).Should().Equal(nameof(Complaint.Id));

        // String lengths
        complaint.FindProperty(nameof(Complaint.Subject))!.GetMaxLength().Should().Be(200);
        complaint.FindProperty(nameof(Complaint.Subject))!.IsNullable.Should().BeFalse();

        complaint.FindProperty(nameof(Complaint.Description))!.GetMaxLength().Should().Be(2000);
        complaint.FindProperty(nameof(Complaint.Description))!.IsNullable.Should().BeFalse();

        complaint.FindProperty(nameof(Complaint.LocationDescription))!.GetMaxLength().Should().Be(500);
        complaint.FindProperty(nameof(Complaint.LocationDescription))!.IsNullable.Should().BeTrue();

        complaint.FindProperty(nameof(Complaint.ResolutionNote))!.GetMaxLength().Should().Be(1000);
        complaint.FindProperty(nameof(Complaint.ResolutionNote))!.IsNullable.Should().BeTrue();

        // Nullable coordinates
        complaint.FindProperty(nameof(Complaint.Latitude))!.IsNullable.Should().BeTrue();
        complaint.FindProperty(nameof(Complaint.Longitude))!.IsNullable.Should().BeTrue();

        // Enums and defaults
        complaint.FindProperty(nameof(Complaint.Category))!.ClrType.Should().Be(typeof(ComplaintCategory));
        complaint.FindProperty(nameof(Complaint.Status))!.ClrType.Should().Be(typeof(ComplaintStatus));
        complaint.FindProperty(nameof(Complaint.Status))!.GetDefaultValue().Should().Be(ComplaintStatus.Submitted);
    }

    [Fact]
    public void Complaint_Model_ConfiguresUserForeignKeysAndIndexesCorrectly()
    {
        using var db = CreateContext();
        var complaint = GetDesignTimeModel(db).FindEntityType(typeof(Complaint))!;

        // Foreign keys to AppUser
        var foreignKeys = complaint.GetForeignKeys().ToList();
        foreignKeys.Should().HaveCount(2);

        var citizenFk = foreignKeys.Single(fk => fk.Properties.Any(p => p.Name == nameof(Complaint.CitizenId)));
        citizenFk.PrincipalEntityType.ClrType.Should().Be(typeof(AppUser));
        citizenFk.IsRequired.Should().BeTrue();
        citizenFk.DeleteBehavior.Should().Be(DeleteBehavior.Restrict);

        var resolvedByFk = foreignKeys.Single(fk => fk.Properties.Any(p => p.Name == nameof(Complaint.ResolvedByUserId)));
        resolvedByFk.PrincipalEntityType.ClrType.Should().Be(typeof(AppUser));
        resolvedByFk.IsRequired.Should().BeFalse();
        resolvedByFk.DeleteBehavior.Should().Be(DeleteBehavior.Restrict);

        // Indexes
        var indexes = complaint.GetIndexes().ToList();
        indexes.Should().Contain(idx => idx.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(Complaint.CitizenId) }));
        indexes.Should().Contain(idx => idx.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(Complaint.Status) }));
        indexes.Should().Contain(idx => idx.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(Complaint.CreatedAt) }));
        indexes.Should().Contain(idx => idx.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(Complaint.Status), nameof(Complaint.Category) }));

        // Check constraints
        var constraints = complaint.GetCheckConstraints().ToList();
        constraints.Should().Contain(c => c.Name == "CK_Complaints_Status");
        constraints.Should().Contain(c => c.Name == "CK_Complaints_Category");
        constraints.Should().Contain(c => c.Name == "CK_Complaints_Coordinates");
    }
}
