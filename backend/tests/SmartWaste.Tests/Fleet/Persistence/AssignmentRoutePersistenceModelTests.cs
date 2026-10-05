using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using SmartWaste.Domain.Collection.Entities;
using SmartWaste.Domain.Collection.Enums;
using SmartWaste.Infrastructure.Persistence;

namespace SmartWaste.Tests.Collection.Persistence;

/// <summary>
/// Model-level coverage for C3.4a. PostgreSQL enforcement is intentionally
/// deferred until the unapplied migration receives separate approval.
/// </summary>
public class AssignmentRoutePersistenceModelTests
{
    private static AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase($"SmartWaste_C3_Assignment_Model_{Guid.NewGuid():N}")
        .Options);

    private static IModel GetDesignTimeModel(AppDbContext db) =>
        db.GetService<IDesignTimeModel>().Model;

    [Fact]
    public void Assignment_Model_UsesUnfinishedDriverAndVehiclePartialUniqueIndexes()
    {
        using var db = CreateContext();
        var assignment = GetDesignTimeModel(db).FindEntityType(typeof(CollectionAssignment))!;

        Enum.GetValues<CollectionAssignmentStatus>().Should().BeEquivalentTo(new[]
        {
            CollectionAssignmentStatus.Assigned, CollectionAssignmentStatus.InProgress,
            CollectionAssignmentStatus.Completed, CollectionAssignmentStatus.PartiallyCompleted,
            CollectionAssignmentStatus.Failed, CollectionAssignmentStatus.Cancelled
        });
        assignment.GetIndexes().Should().Contain(index => index.IsUnique &&
            index.GetDatabaseName() == "IX_CollectionAssignments_DriverId_Unfinished" &&
            index.GetFilter() == "\"Status\" IN ('Assigned', 'InProgress')");
        assignment.GetIndexes().Should().Contain(index => index.IsUnique &&
            index.GetDatabaseName() == "IX_CollectionAssignments_VehicleId_Unfinished" &&
            index.GetFilter() == "\"Status\" IN ('Assigned', 'InProgress')");
        assignment.GetIndexes().Should().Contain(index => index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual(new[]
            { nameof(CollectionAssignment.AssignmentNumber) }));
        assignment.FindProperty(nameof(CollectionAssignment.AssignmentNumber))!
            .GetDefaultValueSql().Should().Be("nextval('\"CollectionAssignmentNumberSequence\"'::regclass)");
        assignment.FindProperty(nameof(CollectionAssignment.AssignmentNumber))!
            .GetAfterSaveBehavior().Should().Be(PropertySaveBehavior.Throw);
        assignment.GetCheckConstraints().Should().Contain(constraint =>
            constraint.Name == "CK_CollectionAssignments_AssignmentNumber" &&
            constraint.Sql == "\"AssignmentNumber\" > 0");
        assignment.GetForeignKeys().Should().OnlyContain(foreignKey =>
            foreignKey.DeleteBehavior == DeleteBehavior.Restrict);
        assignment.GetCheckConstraints().Should().Contain(constraint =>
            constraint.Name == "CK_CollectionAssignments_CancellationReason");
    }

    [Fact]
    public void Claim_AndStop_Model_EnforceActiveClaimAndSameTaskRelationship()
    {
        using var db = CreateContext();
        var model = GetDesignTimeModel(db);
        var claim = model.FindEntityType(typeof(CollectionAssignmentTaskClaim))!;
        var stop = model.FindEntityType(typeof(RouteStop))!;

        claim.GetIndexes().Should().Contain(index => index.IsUnique &&
            index.GetDatabaseName() == "IX_CollectionAssignmentTaskClaims_TaskId_Active" &&
            index.GetFilter() == "\"IsActive\" = TRUE");
        claim.GetIndexes().Should().Contain(index => index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual(new[]
            {
                nameof(CollectionAssignmentTaskClaim.CollectionAssignmentId),
                nameof(CollectionAssignmentTaskClaim.CollectionTaskId)
            }));

        var claimForeignKey = stop.GetForeignKeys().Single(foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(CollectionAssignmentTaskClaim));
        claimForeignKey.Properties.Select(property => property.Name).Should().Equal(
            nameof(RouteStop.CollectionAssignmentTaskClaimId), nameof(RouteStop.CollectionTaskId));
        claimForeignKey.PrincipalKey.Properties.Select(property => property.Name).Should().Equal(
            nameof(CollectionAssignmentTaskClaim.Id), nameof(CollectionAssignmentTaskClaim.CollectionTaskId));
        claimForeignKey.DeleteBehavior.Should().Be(DeleteBehavior.Restrict);
        stop.GetIndexes().Should().Contain(index => index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual(new[]
            { nameof(RouteStop.RouteId), nameof(RouteStop.Sequence) }));
        stop.GetIndexes().Should().Contain(index => index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual(new[]
            { nameof(RouteStop.CollectionAssignmentTaskClaimId) }));
        stop.GetCheckConstraints().Should().Contain(constraint =>
            constraint.Name == "CK_RouteStops_Sequence" && constraint.Sql == "\"Sequence\" > 0");
        stop.GetCheckConstraints().Should().Contain(constraint =>
            constraint.Name == "CK_RouteStops_Failure");
    }

    [Fact]
    public void Route_AndHistory_Model_UseRequiredOneToOneAndHistoricalRelationships()
    {
        using var db = CreateContext();
        var model = GetDesignTimeModel(db);
        var route = model.FindEntityType(typeof(Route))!;
        var assignmentHistory = model.FindEntityType(typeof(CollectionAssignmentStatusHistory))!;
        var stopHistory = model.FindEntityType(typeof(RouteStopStatusHistory))!;

        route.GetIndexes().Should().Contain(index => index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual(new[] { nameof(Route.CollectionAssignmentId) }));
        route.GetForeignKeys().Single().DeleteBehavior.Should().Be(DeleteBehavior.Cascade);
        assignmentHistory.GetForeignKeys().Should().Contain(foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(CollectionAssignment) &&
            foreignKey.DeleteBehavior == DeleteBehavior.Cascade);
        stopHistory.GetForeignKeys().Should().Contain(foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(RouteStop) &&
            foreignKey.DeleteBehavior == DeleteBehavior.Cascade);
        Enum.GetValues<RouteStopStatus>().Should().BeEquivalentTo(new[]
        { RouteStopStatus.Pending, RouteStopStatus.Completed, RouteStopStatus.Failed });
    }
}
