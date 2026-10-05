using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using SmartWaste.Domain.Entities;
using SmartWaste.Domain.Operations.Entities;
using SmartWaste.Domain.Operations.Enums;
using SmartWaste.Infrastructure.Persistence;
using Xunit;

namespace SmartWaste.Tests.Operations.Persistence;

public class OperationalIssuePersistenceModelTests
{
    private static AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase($"SmartWaste_C4_OperationalIssue_Model_{Guid.NewGuid():N}")
        .Options);

    private static IModel GetDesignTimeModel(AppDbContext db) =>
        db.GetService<IDesignTimeModel>().Model;

    [Fact]
    public void OperationalIssue_Model_ConfiguresTableColumnsLengthsAndEnumsCorrectly()
    {
        using var db = CreateContext();
        var issue = GetDesignTimeModel(db).FindEntityType(typeof(OperationalIssue))!;

        issue.GetTableName().Should().Be("OperationalIssues");
        issue.FindPrimaryKey()!.Properties.Select(p => p.Name).Should().Equal(nameof(OperationalIssue.Id));

        // String lengths
        issue.FindProperty(nameof(OperationalIssue.Title))!.GetMaxLength().Should().Be(200);
        issue.FindProperty(nameof(OperationalIssue.Title))!.IsNullable.Should().BeFalse();

        issue.FindProperty(nameof(OperationalIssue.Description))!.GetMaxLength().Should().Be(2000);
        issue.FindProperty(nameof(OperationalIssue.Description))!.IsNullable.Should().BeFalse();

        issue.FindProperty(nameof(OperationalIssue.LocationDescription))!.GetMaxLength().Should().Be(500);
        issue.FindProperty(nameof(OperationalIssue.LocationDescription))!.IsNullable.Should().BeTrue();

        issue.FindProperty(nameof(OperationalIssue.ResolutionNote))!.GetMaxLength().Should().Be(1000);
        issue.FindProperty(nameof(OperationalIssue.ResolutionNote))!.IsNullable.Should().BeTrue();

        // Nullable coordinates
        issue.FindProperty(nameof(OperationalIssue.Latitude))!.IsNullable.Should().BeTrue();
        issue.FindProperty(nameof(OperationalIssue.Longitude))!.IsNullable.Should().BeTrue();

        // Enums and defaults
        issue.FindProperty(nameof(OperationalIssue.IssueType))!.ClrType.Should().Be(typeof(OperationalIssueType));
        issue.FindProperty(nameof(OperationalIssue.Status))!.ClrType.Should().Be(typeof(OperationalIssueStatus));
        issue.FindProperty(nameof(OperationalIssue.Status))!.GetDefaultValue().Should().Be(OperationalIssueStatus.Reported);
    }

    [Fact]
    public void OperationalIssue_Model_ConfiguresUserForeignKeysAndIndexesCorrectly()
    {
        using var db = CreateContext();
        var issue = GetDesignTimeModel(db).FindEntityType(typeof(OperationalIssue))!;

        // Foreign keys to AppUser
        var foreignKeys = issue.GetForeignKeys().ToList();
        foreignKeys.Should().HaveCount(2);

        var driverFk = foreignKeys.Single(fk => fk.Properties.Any(p => p.Name == nameof(OperationalIssue.DriverId)));
        driverFk.PrincipalEntityType.ClrType.Should().Be(typeof(AppUser));
        driverFk.IsRequired.Should().BeTrue();
        driverFk.DeleteBehavior.Should().Be(DeleteBehavior.Restrict);

        var resolvedByFk = foreignKeys.Single(fk => fk.Properties.Any(p => p.Name == nameof(OperationalIssue.ResolvedByUserId)));
        resolvedByFk.PrincipalEntityType.ClrType.Should().Be(typeof(AppUser));
        resolvedByFk.IsRequired.Should().BeFalse();
        resolvedByFk.DeleteBehavior.Should().Be(DeleteBehavior.Restrict);

        // Indexes
        var indexes = issue.GetIndexes().ToList();
        indexes.Should().Contain(idx => idx.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(OperationalIssue.DriverId) }));
        indexes.Should().Contain(idx => idx.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(OperationalIssue.Status) }));
        indexes.Should().Contain(idx => idx.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(OperationalIssue.CreatedAt) }));
        indexes.Should().Contain(idx => idx.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(OperationalIssue.Status), nameof(OperationalIssue.IssueType) }));

        // Check constraints
        var constraints = issue.GetCheckConstraints().ToList();
        constraints.Should().Contain(c => c.Name == "CK_OperationalIssues_Status");
        constraints.Should().Contain(c => c.Name == "CK_OperationalIssues_Type");
        constraints.Should().Contain(c => c.Name == "CK_OperationalIssues_Coordinates");
    }
}
