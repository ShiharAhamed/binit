using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SmartWaste.Application.Collection.Queries;
using SmartWaste.Domain.Collection.Entities;
using SmartWaste.Domain.Common;
using SmartWaste.Domain.Entities;
using SmartWaste.Infrastructure.Collection.Services;
using SmartWaste.Infrastructure.Persistence;

namespace SmartWaste.Tests.Collection.Persistence;

[Collection(IntegrationTestCollection.Name)]
public class CollectionAssignmentNumberPostgreSqlTests(CustomWebApplicationFactory factory)
{
    [Fact]
    public async Task PostgreSql_GeneratesDistinctReferences_AndEnforcesNumberConstraints()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();

        var actor = User();
        var driver1 = User();
        var driver2 = User();
        var vehicle1 = Vehicle();
        var vehicle2 = Vehicle();
        db.Users.AddRange(actor, driver1, driver2);
        db.DriverProfiles.AddRange(new DriverProfile { UserId = driver1.Id }, new DriverProfile { UserId = driver2.Id });
        db.Vehicles.AddRange(vehicle1, vehicle2);
        await db.SaveChangesAsync();

        var first = new CollectionAssignment { DriverId = driver1.Id, VehicleId = vehicle1.Id, AssignedByUserId = actor.Id };
        var second = new CollectionAssignment { DriverId = driver2.Id, VehicleId = vehicle2.Id, AssignedByUserId = actor.Id };
        db.CollectionAssignments.AddRange(first, second);
        await db.SaveChangesAsync();

        first.Id.Should().NotBe(Guid.Empty);
        second.Id.Should().NotBe(first.Id);
        first.AssignmentNumber.Should().BePositive();
        second.AssignmentNumber.Should().BePositive();
        second.AssignmentNumber.Should().NotBe(first.AssignmentNumber);

        var read = new AssignmentReadService(db);
        var list = await read.GetStaffListAsync(new AssignmentListQuery(), actor.Id, AppRoles.WasteOfficer);
        list.Items.Single(x => x.Id == first.Id).AssignmentReference.Should().Be(
            $"Assignment {first.AssignmentNumber:D3}");
        var detail = await read.GetDetailAsync(first.Id, actor.Id, AppRoles.WasteOfficer);
        detail.AssignmentNumber.Should().Be(first.AssignmentNumber);
        detail.AssignmentReference.Should().Be(list.Items.Single(x => x.Id == first.Id).AssignmentReference);

        await AssertRejectedAsync(db, "sp_duplicate", $"UPDATE \"CollectionAssignments\" SET \"AssignmentNumber\" = {first.AssignmentNumber} WHERE \"Id\" = '{second.Id}'", PostgresErrorCodes.UniqueViolation);
        await AssertRejectedAsync(db, "sp_null", $"UPDATE \"CollectionAssignments\" SET \"AssignmentNumber\" = NULL WHERE \"Id\" = '{first.Id}'", PostgresErrorCodes.NotNullViolation);
        await AssertRejectedAsync(db, "sp_zero", $"UPDATE \"CollectionAssignments\" SET \"AssignmentNumber\" = 0 WHERE \"Id\" = '{first.Id}'", PostgresErrorCodes.CheckViolation);
        await AssertRejectedAsync(db, "sp_negative", $"UPDATE \"CollectionAssignments\" SET \"AssignmentNumber\" = -1 WHERE \"Id\" = '{first.Id}'", PostgresErrorCodes.CheckViolation);

        first.AssignmentNumber += 100_000;
        var renumber = () => db.SaveChangesAsync();
        await renumber.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task PostgreSqlSequence_ConcurrentAllocationsRemainUnique()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var connectionString = db.Database.GetConnectionString()!;

        var numbers = await Task.WhenAll(Enumerable.Range(0, 12).Select(async _ =>
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand("SELECT nextval('\"CollectionAssignmentNumberSequence\"'::regclass)", connection);
            return (long)(await command.ExecuteScalarAsync())!;
        }));

        numbers.Should().OnlyHaveUniqueItems();
        numbers.Should().OnlyContain(number => number > 0);
    }

    private static async Task AssertRejectedAsync(AppDbContext db, string savepoint, string sql, string expectedState)
    {
        var transaction = db.Database.CurrentTransaction!;
        await transaction.CreateSavepointAsync(savepoint);
        try
        {
            var action = () => db.Database.ExecuteSqlRawAsync(sql);
            var error = await action.Should().ThrowAsync<PostgresException>();
            error.Which.SqlState.Should().Be(expectedState);
        }
        finally
        {
            await transaction.RollbackToSavepointAsync(savepoint);
        }
    }

    private static AppUser User()
    {
        var id = Guid.NewGuid();
        var email = $"assignment-number-{id:N}@smartwaste.test";
        return new AppUser { Id = id, UserName = email, NormalizedUserName = email.ToUpperInvariant(), Email = email, NormalizedEmail = email.ToUpperInvariant(), FullName = "Assignment Number Test" };
    }

    private static Vehicle Vehicle() => new()
    {
        RegistrationNumber = $"AN-{Guid.NewGuid():N}"[..20],
        CapacityLiters = 1000
    };
}
