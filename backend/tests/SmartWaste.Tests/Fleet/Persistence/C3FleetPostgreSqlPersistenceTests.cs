using System.Data.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartWaste.Domain.Entities;
using SmartWaste.Infrastructure.Persistence;
using Moq;
using Microsoft.AspNetCore.Identity;
using SmartWaste.Application.Collection.DTOs.Requests;
using SmartWaste.Infrastructure.Collection.Services;
using SmartWaste.Domain.Reporting.Entities;
using SmartWaste.Domain.Reporting.Enums;
using SmartWaste.Domain.Collection.Entities;
using SmartWaste.Domain.Collection.Enums;
using SmartWaste.Domain.Common;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using SmartWaste.Application.Collection.DTOs.Responses;
using SmartWaste.Application.Collection.Interfaces;
using SmartWaste.Application.Fleet.DTOs.Responses;
using SmartWaste.Application.Workflow.DTOs.Requests;
using SmartWaste.Application.Workflow.Services;
using SmartWaste.Domain.Workflow.Entities;
using SmartWaste.Domain.Workflow.Enums;
using SmartWaste.Infrastructure.Workflow.Services;

namespace SmartWaste.Tests.Collection.Persistence;

/// <summary>
/// PostgreSQL verification against the isolated CI test database.
/// </summary>
[Trait("Category", "PostgreSql")]
public class C3FleetPostgreSqlPersistenceTests
{
    private static string GetTestConnectionString()
    {
        return Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings__DefaultConnection must point to an isolated test database.");
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(GetTestConnectionString())
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task AppliedMigration_HasExpectedC3SchemaAndPreservesCoreTables()
    {
        await using var db = CreateContext();
        await db.Database.OpenConnectionAsync();

        try
        {
            var expectedTables = new[]
            {
                "AspNetUsers", "WasteReports", "WasteBins", "CollectionTasks",
                "DriverProfiles", "Vehicles", "VehicleSupportedWasteTypes"
            };

            foreach (var table in expectedTables)
            {
                (await ScalarAsync<long>(db, $"SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_name = '{table}';"))
                    .Should().Be(1, $"the {table} table must exist");
            }

            (await ScalarAsync<long>(db,
                "SELECT COUNT(*) FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" = '20260924063914_AddC3DriverProfileAndVehicleFoundation';"))
                .Should().Be(1);

            (await ScalarAsync<long>(db,
                "SELECT COUNT(*) FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" = '20260925135923_SimplifyDriverProfilesAutomaticProvisioning';"))
                .Should().Be(1);

            (await ScalarAsync<long>(db,
                "SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'DriverProfiles' AND column_name = 'LicenseNumber' AND is_nullable = 'YES';"))
                .Should().Be(1);

            (await ScalarAsync<long>(db, """
                SELECT COUNT(*)
                FROM "AspNetUsers" AS users
                INNER JOIN "AspNetUserRoles" AS user_roles ON user_roles."UserId" = users."Id"
                INNER JOIN "AspNetRoles" AS roles ON roles."Id" = user_roles."RoleId"
                LEFT JOIN "DriverProfiles" AS profiles ON profiles."UserId" = users."Id"
                WHERE roles."Name" = 'Driver' AND profiles."UserId" IS NULL;
                """))
                .Should().Be(0);

            var driverConstraints = await StringsAsync(db,
                "SELECT conname FROM pg_constraint WHERE conrelid = 'public.\"DriverProfiles\"'::regclass;");
            driverConstraints.Should().Contain(new[]
            {
                "PK_DriverProfiles",
                "FK_DriverProfiles_AspNetUsers_UserId",
                "CK_DriverProfiles_AvailabilityStatus"
            });

            var vehicleConstraints = await StringsAsync(db,
                "SELECT conname FROM pg_constraint WHERE conrelid = 'public.\"Vehicles\"'::regclass;");
            vehicleConstraints.Should().Contain(new[]
            {
                "PK_Vehicles",
                "CK_Vehicles_CapacityLiters",
                "CK_Vehicles_OperationalStatus",
                "CK_Vehicles_VehicleType"
            });

            var vehicleTypeConstraints = await StringsAsync(db,
                "SELECT conname FROM pg_constraint WHERE conrelid = 'public.\"VehicleSupportedWasteTypes\"'::regclass;");
            vehicleTypeConstraints.Should().Contain(new[]
            {
                "PK_VehicleSupportedWasteTypes",
                "FK_VehicleSupportedWasteTypes_Vehicles_VehicleId"
            });

            var indexes = await StringsAsync(db,
                "SELECT indexname FROM pg_indexes WHERE schemaname = 'public' AND tablename IN ('DriverProfiles', 'Vehicles');");
            indexes.Should().Contain(new[] { "IX_DriverProfiles_LicenseNumber", "IX_Vehicles_RegistrationNumber" });
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    [Fact]
    public async Task PostgreSql_EnforcesC3FleetConstraintsWithinRollbackTransaction()
    {
        await using var db = CreateContext();
        await db.Database.OpenConnectionAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();

        try
        {
            var firstDriver = CreateTestUser();
            var secondDriver = CreateTestUser();
            var thirdDriver = CreateTestUser();
            db.Users.AddRange(firstDriver, secondDriver, thirdDriver);
            await db.SaveChangesAsync();

            await ExecuteAsync(db, $"""
                INSERT INTO "DriverProfiles" ("UserId", "LicenseNumber", "AvailabilityStatus", "IsEligible", "CreatedAt")
                VALUES ({firstDriver.Id}, {"C3-TEST-LICENSE"}, {"Available"}, {true}, {DateTime.UtcNow});
                """);

            await AssertSqlStateAsync(
                db,
                () => ExecuteAsync(db, $"""
                    INSERT INTO "DriverProfiles" ("UserId", "LicenseNumber", "AvailabilityStatus", "IsEligible", "CreatedAt")
                    VALUES ({secondDriver.Id}, {"C3-TEST-LICENSE"}, {"Available"}, {true}, {DateTime.UtcNow});
                    """),
                PostgresErrorCodes.UniqueViolation);

            await AssertSqlStateAsync(
                db,
                () => ExecuteAsync(db, $"""
                    INSERT INTO "DriverProfiles" ("UserId", "LicenseNumber", "AvailabilityStatus", "IsEligible", "CreatedAt")
                    VALUES ({thirdDriver.Id}, {"C3-TEST-LICENSE-INVALID-STATUS"}, {"Unavailable"}, {true}, {DateTime.UtcNow});
                    """),
                PostgresErrorCodes.CheckViolation);

            await AssertSqlStateAsync(
                db,
                () => ExecuteAsync(db, $"""
                    INSERT INTO "DriverProfiles" ("UserId", "LicenseNumber", "AvailabilityStatus", "IsEligible", "CreatedAt")
                    VALUES ({Guid.NewGuid()}, {"C3-TEST-LICENSE-INVALID-FK"}, {"Available"}, {true}, {DateTime.UtcNow});
                    """),
                PostgresErrorCodes.ForeignKeyViolation);

            var vehicleId = Guid.NewGuid();
            await ExecuteAsync(db, $"""
                INSERT INTO "Vehicles" ("Id", "RegistrationNumber", "VehicleType", "CapacityLiters", "OperationalStatus", "CreatedAt")
                VALUES ({vehicleId}, {"C3-TEST-REG"}, {"Compactor"}, {12000}, {"Available"}, {DateTime.UtcNow});
                """);

            await ExecuteAsync(db, $"""
                INSERT INTO "VehicleSupportedWasteTypes" ("VehicleId", "WasteType")
                VALUES ({vehicleId}, {"General"});
                """);

            await AssertSqlStateAsync(
                db,
                () => ExecuteAsync(db, $"""
                    INSERT INTO "Vehicles" ("Id", "RegistrationNumber", "VehicleType", "CapacityLiters", "OperationalStatus", "CreatedAt")
                    VALUES ({Guid.NewGuid()}, {"C3-TEST-REG"}, {"Compactor"}, {12000}, {"Available"}, {DateTime.UtcNow});
                    """),
                PostgresErrorCodes.UniqueViolation);

            await AssertSqlStateAsync(
                db,
                () => ExecuteAsync(db, $"""
                    INSERT INTO "Vehicles" ("Id", "RegistrationNumber", "VehicleType", "CapacityLiters", "OperationalStatus", "CreatedAt")
                    VALUES ({Guid.NewGuid()}, {"C3-TEST-CAPACITY"}, {"Compactor"}, {0}, {"Available"}, {DateTime.UtcNow});
                    """),
                PostgresErrorCodes.CheckViolation);

            await AssertSqlStateAsync(
                db,
                () => ExecuteAsync(db, $"""
                    INSERT INTO "Vehicles" ("Id", "RegistrationNumber", "VehicleType", "CapacityLiters", "OperationalStatus", "CreatedAt")
                    VALUES ({Guid.NewGuid()}, {"C3-TEST-STATUS"}, {"Compactor"}, {12000}, {"Assigned"}, {DateTime.UtcNow});
                    """),
                PostgresErrorCodes.CheckViolation);

            await AssertSqlStateAsync(
                db,
                () => ExecuteAsync(db, $"""
                    INSERT INTO "Vehicles" ("Id", "RegistrationNumber", "VehicleType", "CapacityLiters", "OperationalStatus", "CreatedAt")
                    VALUES ({Guid.NewGuid()}, {"C3-TEST-TYPE"}, {"UnknownType"}, {12000}, {"Available"}, {DateTime.UtcNow});
                    """),
                PostgresErrorCodes.CheckViolation);

            await AssertSqlStateAsync(
                db,
                () => ExecuteAsync(db, $"""
                    INSERT INTO "VehicleSupportedWasteTypes" ("VehicleId", "WasteType")
                    VALUES ({vehicleId}, {"General"});
                    """),
                PostgresErrorCodes.UniqueViolation);
        }
        finally
        {
            await transaction.RollbackAsync();
            await db.Database.CloseConnectionAsync();
        }
    }

    [Fact]
    public async Task ExistingDriverAccount_RemainsValidWithoutDriverProfile()
    {
        await using var db = CreateContext();
        await db.Database.OpenConnectionAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();

        try
        {
            var driver = CreateTestUser();
            var role = await db.Roles.SingleOrDefaultAsync(x => x.Name == AppRoles.Driver);
            if (role is null)
            {
                role = new IdentityRole<Guid> { Id = Guid.NewGuid(), Name = AppRoles.Driver, NormalizedName = AppRoles.Driver.ToUpperInvariant() };
                db.Roles.Add(role);
            }
            db.Users.Add(driver);
            db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = driver.Id, RoleId = role.Id });
            await db.SaveChangesAsync();

            (await ScalarAsync<long>(db, $"""
                SELECT COUNT(*)
                FROM "AspNetUsers" AS u
                INNER JOIN "AspNetUserRoles" AS ur ON ur."UserId" = u."Id"
                INNER JOIN "AspNetRoles" AS r ON r."Id" = ur."RoleId"
                LEFT JOIN "DriverProfiles" AS dp ON dp."UserId" = u."Id"
                WHERE u."Id" = '{driver.Id}' AND r."Name" = 'Driver' AND dp."UserId" IS NULL;
                """))
                .Should().Be(1, "a Driver account can exist before its profile is provisioned");
        }
        finally
        {
            await transaction.RollbackAsync();
            await db.Database.CloseConnectionAsync();
        }
    }

    [Fact]
    public async Task PostgreSql_CollectionAssignmentService_CreatesReordersCancelsReclaimsStartsAndRecordsStopOutcomes()
    {
        await using var db = CreateContext();
        var suffix = Guid.NewGuid();
        var officer = CreateTestUser(); var citizen = CreateTestUser(); var driverUser = CreateTestUser();
        var report = new WasteReport { CitizenId = citizen.Id, Description = "C3 PostgreSQL assignment test", WasteType = WasteType.General, Latitude = 6.9, Longitude = 79.9, Status = WasteReportStatus.Scheduled, Priority = WasteReportPriority.High };
        var bin = new WasteBin { BinCode = $"C3-PG-{suffix:N}"[..20], CapacityLiters = 100, Latitude = 6.91, Longitude = 79.91, AdministrativeStatus = BinAdministrativeStatus.Active };
        var vehicle = new Vehicle { RegistrationNumber = $"C3-PG-{suffix:N}"[..20], VehicleType = VehicleType.Compactor, CapacityLiters = 1000, OperationalStatus = VehicleOperationalStatus.Available };
        var reportTask = new CollectionTask { TaskCode = $"C3-R-{suffix:N}"[..20], WasteReportId = report.Id, CollectionReason = CollectionReason.VerifiedReport, Status = CollectionTaskStatus.Scheduled, ScheduledAt = DateTime.UtcNow.AddHours(1), CreatedByUserId = officer.Id };
        var binTask = new CollectionTask { TaskCode = $"C3-B-{suffix:N}"[..20], WasteBinId = bin.Id, CollectionReason = CollectionReason.OfficerDiscretion, SchedulingReason = "C3 PostgreSQL test", Status = CollectionTaskStatus.Scheduled, ScheduledAt = DateTime.UtcNow.AddHours(2), CreatedByUserId = officer.Id };
        var assignmentId = Guid.Empty;
        var reassignmentId = Guid.Empty;
        var binReplacementTaskId = Guid.Empty;
        try
        {
            db.Users.AddRange(officer, citizen, driverUser);
            db.DriverProfiles.Add(new DriverProfile { UserId = driverUser.Id, LicenseNumber = $"C3-L-{suffix:N}"[..20], IsEligible = true, AvailabilityStatus = DriverAvailabilityStatus.Available });
            vehicle.SupportedWasteTypes.Add(new VehicleSupportedWasteType { WasteType = WasteType.General });
            bin.AcceptedWasteTypes.Add(new WasteBinAcceptedWasteType { WasteType = WasteType.General });
            db.AddRange(report, bin, vehicle, reportTask, binTask);
            await db.SaveChangesAsync();
            var manager = MockUserManager();
            var result = await new CollectionAssignmentService(db, manager.Object).CreateAsync(new CreateCollectionAssignmentRequest
            {
                DriverId = driverUser.Id, VehicleId = vehicle.Id, CollectionTaskIds = new[] { reportTask.Id, binTask.Id },
                Stops = new[] { new CreateRouteStopRequest { CollectionTaskId = binTask.Id, Sequence = 1 }, new CreateRouteStopRequest { CollectionTaskId = reportTask.Id, Sequence = 2 } }
            }, officer.Id, AppRoles.WasteOfficer);
            assignmentId = result.Id;
            result.AssignmentNumber.Should().BePositive();
            result.AssignmentReference.Should().Be($"Assignment {result.AssignmentNumber:D3}");
            (await db.CollectionAssignments.CountAsync(x => x.Id == assignmentId)).Should().Be(1);
            (await db.CollectionAssignmentTaskClaims.CountAsync(x => x.CollectionAssignmentId == assignmentId && x.IsActive)).Should().Be(2);
            var route = await db.Routes.Include(x => x.Stops).SingleAsync(x => x.CollectionAssignmentId == assignmentId);
            route.Stops.OrderBy(x => x.Sequence).Select(x => x.CollectionTaskId).Should().Equal(binTask.Id, reportTask.Id);
            var originalStops = route.Stops.ToDictionary(x => x.CollectionTaskId);
            db.ChangeTracker.Clear();

            var reordered = await new CollectionAssignmentService(db, manager.Object).ReorderStopsAsync(
                assignmentId,
                new ReorderRouteStopsRequest
                {
                    Stops = new[]
                    {
                        new RouteStopSequenceRequest { RouteStopId = originalStops[reportTask.Id].Id, Sequence = 1 },
                        new RouteStopSequenceRequest { RouteStopId = originalStops[binTask.Id].Id, Sequence = 2 }
                    }
                },
                officer.Id,
                AppRoles.WasteOfficer);

            reordered.Stops.Select(x => x.Task.Id).Should().Equal(reportTask.Id, binTask.Id);
            var storedRoute = await db.Routes.Include(x => x.Stops).SingleAsync(x => x.CollectionAssignmentId == assignmentId);
            storedRoute.Stops.OrderBy(x => x.Sequence).Select(x => x.Id).Should().Equal(originalStops[reportTask.Id].Id, originalStops[binTask.Id].Id);
            storedRoute.Stops.OrderBy(x => x.Sequence).Select(x => x.CollectionAssignmentTaskClaimId).Should().OnlyHaveUniqueItems();

            Func<Task> rejected = () => new CollectionAssignmentService(db, manager.Object).ReorderStopsAsync(
                assignmentId,
                new ReorderRouteStopsRequest
                {
                    Stops = new[] { new RouteStopSequenceRequest { RouteStopId = originalStops[reportTask.Id].Id, Sequence = 1 } }
                },
                officer.Id,
                AppRoles.WasteOfficer);
            await rejected.Should().ThrowAsync<FluentValidation.ValidationException>();
            db.ChangeTracker.Clear();
            (await db.Routes.Include(x => x.Stops).SingleAsync(x => x.CollectionAssignmentId == assignmentId))
                .Stops.OrderBy(x => x.Sequence).Select(x => x.CollectionTaskId).Should().Equal(reportTask.Id, binTask.Id);

            (await db.CollectionTasks.Where(x => x.Id == reportTask.Id || x.Id == binTask.Id).Select(x => x.Status).ToListAsync()).Should().AllBeEquivalentTo(CollectionTaskStatus.Assigned);
            (await db.WasteReports.SingleAsync(x => x.Id == report.Id)).Status.Should().Be(WasteReportStatus.Scheduled);
            (await db.WasteBins.SingleAsync(x => x.Id == bin.Id)).LastCollectedAt.Should().BeNull();
            (await db.BinObservations.CountAsync(x => x.WasteBinId == bin.Id)).Should().Be(0);

            var cancelled = await new CollectionAssignmentService(db, manager.Object).CancelAsync(
                assignmentId,
                new CancelCollectionAssignmentRequest { Reason = "Test cancellation before driver start." },
                officer.Id,
                AppRoles.WasteOfficer);
            cancelled.Status.Should().Be(CollectionAssignmentStatus.Cancelled);
            cancelled.Route!.Stops.Should().HaveCount(2);
            (await db.CollectionAssignmentTaskClaims.Where(x => x.CollectionAssignmentId == assignmentId).ToListAsync()).Should().OnlyContain(x => !x.IsActive && x.ReleasedByUserId == officer.Id);
            (await db.CollectionTasks.Where(x => x.Id == reportTask.Id || x.Id == binTask.Id).Select(x => x.Status).ToListAsync()).Should().AllBeEquivalentTo(CollectionTaskStatus.Scheduled);
            (await db.CollectionAssignmentStatusHistories.CountAsync(x => x.CollectionAssignmentId == assignmentId)).Should().Be(2);
            (await db.CollectionTaskStatusHistories.CountAsync(x => x.CollectionTaskId == reportTask.Id || x.CollectionTaskId == binTask.Id)).Should().Be(4);
            (await db.Routes.Include(x => x.Stops).SingleAsync(x => x.CollectionAssignmentId == assignmentId)).Stops.Should().OnlyContain(x => x.Status == RouteStopStatus.Pending);
            (await db.WasteReports.SingleAsync(x => x.Id == report.Id)).Status.Should().Be(WasteReportStatus.Scheduled);
            (await db.WasteBins.SingleAsync(x => x.Id == bin.Id)).LastCollectedAt.Should().BeNull();
            (await db.BinObservations.CountAsync(x => x.WasteBinId == bin.Id)).Should().Be(0);

            var reassignment = await new CollectionAssignmentService(db, manager.Object).CreateAssignmentFromApprovedPlanAsync(new CreateCollectionAssignmentRequest
            {
                DriverId = driverUser.Id, VehicleId = vehicle.Id, CollectionTaskIds = new[] { reportTask.Id, binTask.Id },
                Stops = new[] { new CreateRouteStopRequest { CollectionTaskId = reportTask.Id, Sequence = 1 }, new CreateRouteStopRequest { CollectionTaskId = binTask.Id, Sequence = 2 } }
            }, officer.Id);
            reassignmentId = reassignment.Id;
            reassignment.AssignmentNumber.Should().BePositive();
            reassignment.AssignmentNumber.Should().NotBe(result.AssignmentNumber);
            reassignment.AssignmentReference.Should().Be($"Assignment {reassignment.AssignmentNumber:D3}");
            (await db.CollectionAssignmentTaskClaims.Where(x => x.CollectionTaskId == reportTask.Id || x.CollectionTaskId == binTask.Id).ToListAsync()).Count(x => x.IsActive).Should().Be(2);
            var started = await new CollectionAssignmentService(db, manager.Object).StartAsync(reassignmentId, driverUser.Id, AppRoles.Driver);
            started.Status.Should().Be(CollectionAssignmentStatus.InProgress);
            started.Route!.Stops.Select(x => x.Sequence).Should().Equal(1, 2);
            started.Route.Stops.Should().OnlyContain(x => x.Status == RouteStopStatus.Pending);
            (await db.CollectionAssignments.SingleAsync(x => x.Id == reassignmentId)).StartedAt.Should().NotBeNull();
            (await db.CollectionTasks.Where(x => x.Id == reportTask.Id || x.Id == binTask.Id).Select(x => x.Status).ToListAsync()).Should().AllBeEquivalentTo(CollectionTaskStatus.InProgress);
            (await db.WasteReports.SingleAsync(x => x.Id == report.Id)).Status.Should().Be(WasteReportStatus.InProgress);
            (await db.WasteReportStatusHistories.CountAsync(x => x.WasteReportId == report.Id)).Should().Be(1);
            (await db.CollectionAssignmentTaskClaims.Where(x => x.CollectionAssignmentId == reassignmentId).ToListAsync()).Should().OnlyContain(x => x.IsActive);
            (await db.WasteBins.SingleAsync(x => x.Id == bin.Id)).LastCollectedAt.Should().BeNull();
            (await db.BinObservations.CountAsync(x => x.WasteBinId == bin.Id)).Should().Be(0);

            var outcomeStops = started.Route.Stops.ToDictionary(x => x.Task.Id);
            var completed = await new CollectionAssignmentService(db, manager.Object).CompleteStopAsync(reassignmentId, outcomeStops[reportTask.Id].Id, driverUser.Id, AppRoles.Driver);
            completed.Status.Should().Be(CollectionAssignmentStatus.InProgress);
            var failed = await new CollectionAssignmentService(db, manager.Object).FailStopAsync(reassignmentId, outcomeStops[binTask.Id].Id,
                new FailRouteStopRequest { Reason = "Access was safely blocked at the test bin." }, driverUser.Id, AppRoles.Driver);
            failed.Status.Should().Be(CollectionAssignmentStatus.InProgress);
            db.ChangeTracker.Clear();
            (await db.RouteStops.SingleAsync(x => x.Id == outcomeStops[reportTask.Id].Id)).Status.Should().Be(RouteStopStatus.Completed);
            var persistedFailedStop = await db.RouteStops.SingleAsync(x => x.Id == outcomeStops[binTask.Id].Id);
            persistedFailedStop.Status.Should().Be(RouteStopStatus.Failed);
            persistedFailedStop.FailureReason.Should().Be("Access was safely blocked at the test bin.");
            (await db.CollectionTasks.SingleAsync(x => x.Id == reportTask.Id)).Status.Should().Be(CollectionTaskStatus.Completed);
            (await db.CollectionTasks.SingleAsync(x => x.Id == binTask.Id)).Status.Should().Be(CollectionTaskStatus.Failed);
            (await db.WasteReports.SingleAsync(x => x.Id == report.Id)).Status.Should().Be(WasteReportStatus.Resolved);
            (await db.WasteBins.SingleAsync(x => x.Id == bin.Id)).LastCollectedAt.Should().BeNull();
            (await db.BinObservations.CountAsync(x => x.WasteBinId == bin.Id)).Should().Be(0);
            (await db.CollectionAssignmentTaskClaims.Where(x => x.CollectionAssignmentId == reassignmentId).ToListAsync()).Should().OnlyContain(x => x.IsActive);
            (await db.CollectionAssignments.SingleAsync(x => x.Id == reassignmentId)).Status.Should().Be(CollectionAssignmentStatus.InProgress);
            (await db.RouteStopStatusHistories.CountAsync(x => x.RouteStopId == outcomeStops[reportTask.Id].Id || x.RouteStopId == outcomeStops[binTask.Id].Id)).Should().Be(4);
            (await db.CollectionTaskStatusHistories.CountAsync(x => x.CollectionTaskId == reportTask.Id || x.CollectionTaskId == binTask.Id)).Should().Be(10);
            (await db.WasteReportStatusHistories.CountAsync(x => x.WasteReportId == report.Id)).Should().Be(2);
            await Assert.ThrowsAsync<SmartWaste.Application.Common.Exceptions.BusinessRuleConflictException>(() => new CollectionAssignmentService(db, manager.Object).CompleteStopAsync(reassignmentId, outcomeStops[reportTask.Id].Id, driverUser.Id, AppRoles.Driver));
            var finalized = await new CollectionAssignmentService(db, manager.Object).FinalizeAsync(reassignmentId, driverUser.Id, AppRoles.Driver);
            finalized.Status.Should().Be(CollectionAssignmentStatus.PartiallyCompleted);
            (await db.CollectionAssignments.SingleAsync(x => x.Id == reassignmentId)).FinalizedAt.Should().NotBeNull();
            (await db.CollectionAssignmentTaskClaims.Where(x => x.CollectionAssignmentId == reassignmentId).ToListAsync()).Should().OnlyContain(x => !x.IsActive && x.ReleasedByUserId == driverUser.Id);
            (await db.CollectionAssignmentStatusHistories.CountAsync(x => x.CollectionAssignmentId == reassignmentId)).Should().Be(3);
            var binReplacement = await new CollectionTaskService(db).CreateReplacementTaskAsync(binTask.Id, new CreateReplacementCollectionTaskRequest
            {
                ScheduledAt = DateTime.UtcNow.AddHours(3),
                ReplacementReason = "Officer reviewed the failed bin collection and approved a replacement."
            }, officer.Id, AppRoles.WasteOfficer);
            binReplacementTaskId = binReplacement.Id;
            binReplacement.Status.Should().Be(CollectionTaskStatus.Scheduled);
            binReplacement.WasteBinId.Should().Be(bin.Id);
            (await db.CollectionTasks.SingleAsync(x => x.Id == binTask.Id)).Status.Should().Be(CollectionTaskStatus.Failed);
            (await db.CollectionAssignmentTaskClaims.AnyAsync(x => x.CollectionTaskId == binReplacementTaskId && x.IsActive)).Should().BeFalse();
            await Assert.ThrowsAsync<SmartWaste.Application.Common.Exceptions.BusinessRuleConflictException>(() => new CollectionTaskService(db).CreateReplacementTaskAsync(binTask.Id, new CreateReplacementCollectionTaskRequest
            {
                ScheduledAt = DateTime.UtcNow.AddHours(4),
                ReplacementReason = "A duplicate replacement must not create another active task."
            }, officer.Id, AppRoles.WasteOfficer));
            (await db.CollectionTasks.CountAsync(x => x.WasteBinId == bin.Id && x.Status == CollectionTaskStatus.Scheduled)).Should().Be(1);
            await Assert.ThrowsAsync<SmartWaste.Application.Common.Exceptions.BusinessRuleConflictException>(() => new CollectionAssignmentService(db, manager.Object).StartAsync(reassignmentId, driverUser.Id, AppRoles.Driver));
            Func<Task> rejectedCancellation = () => new CollectionAssignmentService(db, manager.Object).CancelAsync(
                assignmentId,
                new CancelCollectionAssignmentRequest { Reason = "Already cancelled assignment." },
                officer.Id,
                AppRoles.WasteOfficer);
            await rejectedCancellation.Should().ThrowAsync<SmartWaste.Application.Common.Exceptions.BusinessRuleConflictException>();
            (await db.CollectionAssignments.SingleAsync(x => x.Id == assignmentId)).Status.Should().Be(CollectionAssignmentStatus.Cancelled);
        }
        finally
        {
            db.ChangeTracker.Clear();
            if (reassignmentId != Guid.Empty) await ExecuteAsync(db, $"DELETE FROM \"Routes\" WHERE \"CollectionAssignmentId\" = {reassignmentId};");
            if (assignmentId != Guid.Empty) await ExecuteAsync(db, $"DELETE FROM \"Routes\" WHERE \"CollectionAssignmentId\" = {assignmentId};");
            if (reassignmentId != Guid.Empty) await ExecuteAsync(db, $"DELETE FROM \"CollectionAssignmentTaskClaims\" WHERE \"CollectionAssignmentId\" = {reassignmentId};");
            if (assignmentId != Guid.Empty) await ExecuteAsync(db, $"DELETE FROM \"CollectionAssignmentTaskClaims\" WHERE \"CollectionAssignmentId\" = {assignmentId};");
            if (reassignmentId != Guid.Empty) await ExecuteAsync(db, $"DELETE FROM \"CollectionAssignments\" WHERE \"Id\" = {reassignmentId};");
            if (assignmentId != Guid.Empty) await ExecuteAsync(db, $"DELETE FROM \"CollectionAssignments\" WHERE \"Id\" = {assignmentId};");
            if (binReplacementTaskId != Guid.Empty) await ExecuteAsync(db, $"DELETE FROM \"CollectionTasks\" WHERE \"Id\" = {binReplacementTaskId};");
            await ExecuteAsync(db, $"DELETE FROM \"CollectionTasks\" WHERE \"Id\" IN ({reportTask.Id}, {binTask.Id});");
            await ExecuteAsync(db, $"DELETE FROM \"WasteReports\" WHERE \"Id\" = {report.Id};");
            await ExecuteAsync(db, $"DELETE FROM \"WasteBins\" WHERE \"Id\" = {bin.Id};");
            await ExecuteAsync(db, $"DELETE FROM \"Vehicles\" WHERE \"Id\" = {vehicle.Id};");
            await ExecuteAsync(db, $"DELETE FROM \"DriverProfiles\" WHERE \"UserId\" = {driverUser.Id};");
            await ExecuteAsync(db, $"DELETE FROM \"AspNetUsers\" WHERE \"Id\" IN ({officer.Id}, {citizen.Id}, {driverUser.Id});");
        }
    }

    [Fact]
    public async Task PostgreSql_CollectionAssignmentService_CompletesBinStopWithoutCreatingObservation()
    {
        await using var db = CreateContext();
        var suffix = Guid.NewGuid();
        var officer = CreateTestUser();
        var driver = CreateTestUser();
        var bin = new WasteBin { BinCode = $"C3-OUTCOME-{suffix:N}"[..20], CapacityLiters = 100, Latitude = 6.91, Longitude = 79.91, AdministrativeStatus = BinAdministrativeStatus.Active };
        var vehicle = new Vehicle { RegistrationNumber = $"C3-OUTCOME-{suffix:N}"[..20], CapacityLiters = 1000, OperationalStatus = VehicleOperationalStatus.Available };
        var task = new CollectionTask { TaskCode = $"C3-O-{suffix:N}"[..20], WasteBinId = bin.Id, CollectionReason = CollectionReason.OfficerDiscretion, SchedulingReason = "PostgreSQL bin completion test", Status = CollectionTaskStatus.Scheduled, ScheduledAt = DateTime.UtcNow.AddHours(1), CreatedByUserId = officer.Id };
        var assignmentId = Guid.Empty;
        try
        {
            db.Users.AddRange(officer, driver);
            db.DriverProfiles.Add(new DriverProfile { UserId = driver.Id, LicenseNumber = $"C3-OL-{suffix:N}"[..20], IsEligible = true, AvailabilityStatus = DriverAvailabilityStatus.Available });
            bin.AcceptedWasteTypes.Add(new WasteBinAcceptedWasteType { WasteType = WasteType.General });
            vehicle.SupportedWasteTypes.Add(new VehicleSupportedWasteType { WasteType = WasteType.General });
            db.AddRange(bin, vehicle, task);
            await db.SaveChangesAsync();
            var service = new CollectionAssignmentService(db, MockUserManager().Object);
            var created = await service.CreateAsync(new CreateCollectionAssignmentRequest
            {
                DriverId = driver.Id, VehicleId = vehicle.Id, CollectionTaskIds = new[] { task.Id },
                Stops = new[] { new CreateRouteStopRequest { CollectionTaskId = task.Id, Sequence = 1 } }
            }, officer.Id, AppRoles.WasteOfficer);
            assignmentId = created.Id;
            var started = await service.StartAsync(assignmentId, driver.Id, AppRoles.Driver);
            var stopId = started.Route!.Stops.Single().Id;
            var completed = await service.CompleteStopAsync(assignmentId, stopId, driver.Id, AppRoles.Driver);

            completed.Status.Should().Be(CollectionAssignmentStatus.InProgress);
            (await db.RouteStops.SingleAsync(x => x.Id == stopId)).Status.Should().Be(RouteStopStatus.Completed);
            (await db.CollectionTasks.SingleAsync(x => x.Id == task.Id)).Status.Should().Be(CollectionTaskStatus.Completed);
            (await db.WasteBins.SingleAsync(x => x.Id == bin.Id)).LastCollectedAt.Should().NotBeNull();
            (await db.BinObservations.CountAsync(x => x.WasteBinId == bin.Id)).Should().Be(0);
            (await db.CollectionAssignmentTaskClaims.Where(x => x.CollectionAssignmentId == assignmentId).ToListAsync()).Should().OnlyContain(x => x.IsActive);
            (await db.CollectionAssignmentStatusHistories.CountAsync(x => x.CollectionAssignmentId == assignmentId)).Should().Be(2);
            (await db.RouteStopStatusHistories.CountAsync(x => x.RouteStopId == stopId)).Should().Be(2);
            (await db.CollectionTaskStatusHistories.CountAsync(x => x.CollectionTaskId == task.Id)).Should().Be(3);
            var finalized = await service.FinalizeAsync(assignmentId, driver.Id, AppRoles.Driver);
            finalized.Status.Should().Be(CollectionAssignmentStatus.Completed);
            (await db.CollectionAssignments.SingleAsync(x => x.Id == assignmentId)).FinalizedAt.Should().NotBeNull();
            (await db.CollectionAssignmentTaskClaims.Where(x => x.CollectionAssignmentId == assignmentId).ToListAsync()).Should().OnlyContain(x => !x.IsActive && x.ReleasedByUserId == driver.Id);
            (await db.CollectionAssignmentStatusHistories.CountAsync(x => x.CollectionAssignmentId == assignmentId)).Should().Be(3);
            var collectedAt = (await db.WasteBins.SingleAsync(x => x.Id == bin.Id)).LastCollectedAt;
            var observation = await service.RecordDriverBinObservationAsync(assignmentId, stopId, new RecordBinObservationRequest { FillLevelPercent = 25, Condition = BinCondition.Good, Notes = "Genuine post-collection test observation." }, driver.Id, AppRoles.Driver);
            observation.WasteBinId.Should().Be(bin.Id);
            observation.RecordedByUserId.Should().Be(driver.Id);
            (await db.BinObservations.CountAsync(x => x.WasteBinId == bin.Id)).Should().Be(1);
            (await db.WasteBins.SingleAsync(x => x.Id == bin.Id)).LastCollectedAt.Should().Be(collectedAt);
            (await db.CollectionAssignments.SingleAsync(x => x.Id == assignmentId)).Status.Should().Be(CollectionAssignmentStatus.Completed);
        }
        finally
        {
            db.ChangeTracker.Clear();
            if (assignmentId != Guid.Empty) await ExecuteAsync(db, $"DELETE FROM \"Routes\" WHERE \"CollectionAssignmentId\" = {assignmentId};");
            if (assignmentId != Guid.Empty) await ExecuteAsync(db, $"DELETE FROM \"CollectionAssignmentTaskClaims\" WHERE \"CollectionAssignmentId\" = {assignmentId};");
            if (assignmentId != Guid.Empty) await ExecuteAsync(db, $"DELETE FROM \"CollectionAssignments\" WHERE \"Id\" = {assignmentId};");
            await ExecuteAsync(db, $"DELETE FROM \"CollectionTasks\" WHERE \"Id\" = {task.Id};");
            await ExecuteAsync(db, $"DELETE FROM \"WasteBins\" WHERE \"Id\" = {bin.Id};");
            await ExecuteAsync(db, $"DELETE FROM \"Vehicles\" WHERE \"Id\" = {vehicle.Id};");
            await ExecuteAsync(db, $"DELETE FROM \"DriverProfiles\" WHERE \"UserId\" = {driver.Id};");
            await ExecuteAsync(db, $"DELETE FROM \"AspNetUsers\" WHERE \"Id\" IN ({officer.Id}, {driver.Id});");
        }
    }

    [Fact]
    public async Task PostgreSql_CollectionAssignmentService_FinalizesAllFailedStops()
    {
        await using var db = CreateContext();
        var suffix = Guid.NewGuid();
        var officer = CreateTestUser();
        var driver = CreateTestUser();
        var citizen = CreateTestUser();
        var report = new WasteReport { CitizenId = citizen.Id, Description = "PostgreSQL failed report replacement", WasteType = WasteType.General, Latitude = 6.9, Longitude = 79.9, Status = WasteReportStatus.Scheduled };
        var vehicle = new Vehicle { RegistrationNumber = $"C3-FAIL-{suffix:N}"[..20], CapacityLiters = 1000, OperationalStatus = VehicleOperationalStatus.Available };
        var task = new CollectionTask { TaskCode = $"C3-F-{suffix:N}"[..20], WasteReportId = report.Id, CollectionReason = CollectionReason.VerifiedReport, Status = CollectionTaskStatus.Scheduled, ScheduledAt = DateTime.UtcNow.AddHours(1), CreatedByUserId = officer.Id };
        var assignmentId = Guid.Empty;
        var replacementTaskId = Guid.Empty;
        try
        {
            db.Users.AddRange(officer, driver, citizen);
            db.DriverProfiles.Add(new DriverProfile { UserId = driver.Id, LicenseNumber = $"C3-FL-{suffix:N}"[..20], IsEligible = true, AvailabilityStatus = DriverAvailabilityStatus.Available });
            vehicle.SupportedWasteTypes.Add(new VehicleSupportedWasteType { WasteType = WasteType.General });
            db.AddRange(report, vehicle, task);
            await db.SaveChangesAsync();
            var service = new CollectionAssignmentService(db, MockUserManager().Object);
            var created = await service.CreateAsync(new CreateCollectionAssignmentRequest
            {
                DriverId = driver.Id, VehicleId = vehicle.Id, CollectionTaskIds = new[] { task.Id },
                Stops = new[] { new CreateRouteStopRequest { CollectionTaskId = task.Id, Sequence = 1 } }
            }, officer.Id, AppRoles.WasteOfficer);
            assignmentId = created.Id;
            var started = await service.StartAsync(assignmentId, driver.Id, AppRoles.Driver);
            var stopId = started.Route!.Stops.Single().Id;
            await service.FailStopAsync(assignmentId, stopId, new FailRouteStopRequest { Reason = "The collection site could not be safely accessed." }, driver.Id, AppRoles.Driver);
            var finalized = await service.FinalizeAsync(assignmentId, driver.Id, AppRoles.Driver);

            finalized.Status.Should().Be(CollectionAssignmentStatus.Failed);
            (await db.CollectionAssignments.SingleAsync(x => x.Id == assignmentId)).FinalizedAt.Should().NotBeNull();
            (await db.RouteStops.SingleAsync(x => x.Id == stopId)).Status.Should().Be(RouteStopStatus.Failed);
            (await db.CollectionTasks.SingleAsync(x => x.Id == task.Id)).Status.Should().Be(CollectionTaskStatus.Failed);
            (await db.WasteReports.SingleAsync(x => x.Id == report.Id)).Status.Should().Be(WasteReportStatus.InProgress);
            (await db.CollectionAssignmentTaskClaims.Where(x => x.CollectionAssignmentId == assignmentId).ToListAsync()).Should().OnlyContain(x => !x.IsActive && x.ReleasedByUserId == driver.Id);
            (await db.CollectionAssignmentStatusHistories.CountAsync(x => x.CollectionAssignmentId == assignmentId)).Should().Be(3);
            var replacement = await new CollectionTaskService(db).CreateReplacementTaskAsync(task.Id, new CreateReplacementCollectionTaskRequest { ScheduledAt = DateTime.UtcNow.AddHours(3), ReplacementReason = "Officer reviewed the failed report collection and approved a replacement." }, officer.Id, AppRoles.WasteOfficer);
            replacementTaskId = replacement.Id;
            replacement.Status.Should().Be(CollectionTaskStatus.Scheduled);
            replacement.WasteReportId.Should().Be(report.Id);
            (await db.WasteReports.SingleAsync(x => x.Id == report.Id)).Status.Should().Be(WasteReportStatus.Scheduled);
            (await db.WasteReportStatusHistories.CountAsync(x => x.WasteReportId == report.Id && x.FromStatus == WasteReportStatus.InProgress && x.ToStatus == WasteReportStatus.Scheduled)).Should().Be(1);
        }
        finally
        {
            db.ChangeTracker.Clear();
            if (assignmentId != Guid.Empty) await ExecuteAsync(db, $"DELETE FROM \"Routes\" WHERE \"CollectionAssignmentId\" = {assignmentId};");
            if (assignmentId != Guid.Empty) await ExecuteAsync(db, $"DELETE FROM \"CollectionAssignmentTaskClaims\" WHERE \"CollectionAssignmentId\" = {assignmentId};");
            if (assignmentId != Guid.Empty) await ExecuteAsync(db, $"DELETE FROM \"CollectionAssignments\" WHERE \"Id\" = {assignmentId};");
            if (replacementTaskId != Guid.Empty) await ExecuteAsync(db, $"DELETE FROM \"CollectionTasks\" WHERE \"Id\" = {replacementTaskId};");
            await ExecuteAsync(db, $"DELETE FROM \"CollectionTasks\" WHERE \"Id\" = {task.Id};");
            await ExecuteAsync(db, $"DELETE FROM \"WasteReports\" WHERE \"Id\" = {report.Id};");
            await ExecuteAsync(db, $"DELETE FROM \"Vehicles\" WHERE \"Id\" = {vehicle.Id};");
            await ExecuteAsync(db, $"DELETE FROM \"DriverProfiles\" WHERE \"UserId\" = {driver.Id};");
            await ExecuteAsync(db, $"DELETE FROM \"AspNetUsers\" WHERE \"Id\" IN ({officer.Id}, {driver.Id}, {citizen.Id});");
        }
    }

    [Fact]
    public async Task PostgreSql_EnforcesC3AssignmentAndRouteConstraintsWithinRollbackTransaction()
    {
        await using var db = CreateContext();
        await db.Database.OpenConnectionAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();

        try
        {
            var suffix = Guid.NewGuid();
            var taskOwner = CreateTestUser();
            var firstBin = new WasteBin { BinCode = $"C3-A-{suffix:N}"[..20], CapacityLiters = 100, Latitude = 6.9, Longitude = 79.9 };
            var secondBin = new WasteBin { BinCode = $"C3-B-{suffix:N}"[..20], CapacityLiters = 100, Latitude = 6.91, Longitude = 79.91 };
            var firstTask = new CollectionTask { TaskCode = $"C3-A-{suffix:N}"[..20], WasteBinId = firstBin.Id, CollectionReason = CollectionReason.OfficerDiscretion, SchedulingReason = "C3 constraint test", Status = CollectionTaskStatus.Scheduled, ScheduledAt = DateTime.UtcNow.AddHours(1), CreatedByUserId = taskOwner.Id };
            var secondTask = new CollectionTask { TaskCode = $"C3-B-{suffix:N}"[..20], WasteBinId = secondBin.Id, CollectionReason = CollectionReason.OfficerDiscretion, SchedulingReason = "C3 constraint test", Status = CollectionTaskStatus.Scheduled, ScheduledAt = DateTime.UtcNow.AddHours(2), CreatedByUserId = taskOwner.Id };
            db.AddRange(taskOwner, firstBin, secondBin, firstTask, secondTask);
            await db.SaveChangesAsync();
            var taskIds = new[] { firstTask.Id, secondTask.Id };

            var driver = CreateTestUser();
            var otherDriver = CreateTestUser();
            var officer = CreateTestUser();
            db.Users.AddRange(driver, otherDriver, officer);
            await db.SaveChangesAsync();
            await ExecuteAsync(db, $"""
                INSERT INTO "DriverProfiles" ("UserId", "LicenseNumber", "AvailabilityStatus", "IsEligible", "CreatedAt")
                VALUES ({driver.Id}, {"C3-ASSIGN-DRIVER"}, {"Available"}, {true}, {DateTime.UtcNow}),
                       ({otherDriver.Id}, {"C3-ASSIGN-DRIVER-2"}, {"Available"}, {true}, {DateTime.UtcNow});
                """);

            var vehicleId = Guid.NewGuid();
            var otherVehicleId = Guid.NewGuid();
            await ExecuteAsync(db, $"""
                INSERT INTO "Vehicles" ("Id", "RegistrationNumber", "VehicleType", "CapacityLiters", "OperationalStatus", "CreatedAt")
                VALUES ({vehicleId}, {"C3-ASSIGN-VEHICLE"}, {"Compactor"}, {1000}, {"Available"}, {DateTime.UtcNow}),
                       ({otherVehicleId}, {"C3-ASSIGN-VEHICLE-2"}, {"Compactor"}, {1000}, {"Available"}, {DateTime.UtcNow});
                """);

            var assignmentId = Guid.NewGuid();
            await ExecuteAsync(db, $"""
                INSERT INTO "CollectionAssignments" ("Id", "DriverId", "VehicleId", "AssignedByUserId", "Status", "AssignedAt", "CreatedAt")
                VALUES ({assignmentId}, {driver.Id}, {vehicleId}, {officer.Id}, {"Assigned"}, {DateTime.UtcNow}, {DateTime.UtcNow});
                """);
            await AssertSqlStateAsync(db, () => ExecuteAsync(db, $"""
                INSERT INTO "CollectionAssignments" ("Id", "DriverId", "VehicleId", "AssignedByUserId", "Status", "AssignedAt", "CreatedAt")
                VALUES ({Guid.NewGuid()}, {driver.Id}, {otherVehicleId}, {officer.Id}, {"Assigned"}, {DateTime.UtcNow}, {DateTime.UtcNow});
                """), PostgresErrorCodes.UniqueViolation);
            await AssertSqlStateAsync(db, () => ExecuteAsync(db, $"""
                INSERT INTO "CollectionAssignments" ("Id", "DriverId", "VehicleId", "AssignedByUserId", "Status", "AssignedAt", "CreatedAt")
                VALUES ({Guid.NewGuid()}, {otherDriver.Id}, {vehicleId}, {officer.Id}, {"InProgress"}, {DateTime.UtcNow}, {DateTime.UtcNow});
                """), PostgresErrorCodes.UniqueViolation);

            var claimId = Guid.NewGuid();
            await ExecuteAsync(db, $"""
                INSERT INTO "CollectionAssignmentTaskClaims" ("Id", "CollectionAssignmentId", "CollectionTaskId", "IsActive", "ClaimedAt")
                VALUES ({claimId}, {assignmentId}, {taskIds[0]}, {true}, {DateTime.UtcNow});
                """);
            await AssertSqlStateAsync(db, () => ExecuteAsync(db, $"""
                INSERT INTO "CollectionAssignmentTaskClaims" ("Id", "CollectionAssignmentId", "CollectionTaskId", "IsActive", "ClaimedAt")
                VALUES ({Guid.NewGuid()}, {assignmentId}, {taskIds[0]}, {true}, {DateTime.UtcNow});
                """), PostgresErrorCodes.UniqueViolation);

            var routeId = Guid.NewGuid();
            await ExecuteAsync(db, $"""
                INSERT INTO "Routes" ("Id", "CollectionAssignmentId", "RoutingMethod", "CreatedAt")
                VALUES ({routeId}, {assignmentId}, {"ManualOrder"}, {DateTime.UtcNow});
                """);
            await AssertSqlStateAsync(db, () => ExecuteAsync(db, $"""
                INSERT INTO "RouteStops" ("Id", "RouteId", "CollectionTaskId", "CollectionAssignmentTaskClaimId", "Sequence", "Status", "CreatedAt")
                VALUES ({Guid.NewGuid()}, {routeId}, {taskIds[1]}, {claimId}, {1}, {"Pending"}, {DateTime.UtcNow});
                """), PostgresErrorCodes.ForeignKeyViolation);
            await ExecuteAsync(db, $"""
                INSERT INTO "RouteStops" ("Id", "RouteId", "CollectionTaskId", "CollectionAssignmentTaskClaimId", "Sequence", "Status", "CreatedAt")
                VALUES ({Guid.NewGuid()}, {routeId}, {taskIds[0]}, {claimId}, {1}, {"Pending"}, {DateTime.UtcNow});
                """);
            await AssertSqlStateAsync(db, () => ExecuteAsync(db, $"""
                INSERT INTO "RouteStops" ("Id", "RouteId", "CollectionTaskId", "CollectionAssignmentTaskClaimId", "Sequence", "Status", "CreatedAt")
                VALUES ({Guid.NewGuid()}, {routeId}, {taskIds[0]}, {claimId}, {1}, {"Pending"}, {DateTime.UtcNow});
                """), PostgresErrorCodes.UniqueViolation);
        }
        finally
        {
            await transaction.RollbackAsync();
            await db.Database.CloseConnectionAsync();
        }
    }

    private static AppUser CreateTestUser()
    {
        var id = Guid.NewGuid();
        var email = $"c3-fleet-{id:N}@example.test";
        return new AppUser
        {
            Id = id,
            FullName = "C3 Fleet Transaction Test",
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            IsActive = true,
            MustChangePassword = false,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static Mock<UserManager<AppUser>> MockUserManager()
    {
        var store = new Mock<IUserStore<AppUser>>();
        var manager = new Mock<UserManager<AppUser>>(store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        manager.Setup(x => x.IsInRoleAsync(It.IsAny<AppUser>(), AppRoles.Driver)).ReturnsAsync(true);
        return manager;
    }

    private static async Task ExecuteAsync(AppDbContext db, FormattableString sql) =>
        await db.Database.ExecuteSqlInterpolatedAsync(sql);

    private static async Task AssertSqlStateAsync(AppDbContext db, Func<Task> action, string expectedSqlState)
    {
        var transaction = db.Database.CurrentTransaction
            ?? throw new InvalidOperationException("A transaction is required for PostgreSQL constraint verification.");
        var savepoint = $"c3_constraint_{Guid.NewGuid():N}";
        await transaction.CreateSavepointAsync(savepoint);

        try
        {
            var exception = await Assert.ThrowsAnyAsync<Exception>(action);
            FindPostgresException(exception).Should().NotBeNull();
            FindPostgresException(exception)!.SqlState.Should().Be(expectedSqlState);
        }
        finally
        {
            await transaction.RollbackToSavepointAsync(savepoint);
        }
    }

    private static PostgresException? FindPostgresException(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgresException)
            {
                return postgresException;
            }
        }

        return null;
    }

    private static async Task<long> ScalarAsync<T>(AppDbContext db, string sql)
    {
        await using var command = CreateCommand(db, sql);
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private static async Task<IReadOnlyList<Guid>> GuidListAsync(AppDbContext db, string sql)
    {
        await using var command = CreateCommand(db, sql);
        await using var reader = await command.ExecuteReaderAsync();
        var values = new List<Guid>();
        while (await reader.ReadAsync()) values.Add(reader.GetGuid(0));
        return values;
    }

    private static async Task<IReadOnlyList<string>> StringsAsync(AppDbContext db, string sql)
    {
        await using var command = CreateCommand(db, sql);
        await using var reader = await command.ExecuteReaderAsync();
        var values = new List<string>();
        while (await reader.ReadAsync())
        {
            values.Add(reader.GetString(0));
        }

        return values;
    }

    private static DbCommand CreateCommand(AppDbContext db, string sql)
    {
        var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        return command;
    }

    private sealed class FaultInjectionAssignmentServiceDecorator : ICollectionAssignmentService
    {
        private readonly CollectionAssignmentService _inner;
        private int _callCount;

        public FaultInjectionAssignmentServiceDecorator(CollectionAssignmentService inner)
        {
            _inner = inner;
        }

        public Guid? FirstCreatedAssignmentId { get; private set; }

        public async Task<AssignmentDetailDto> CreateAssignmentFromApprovedPlanAsync(
            CreateCollectionAssignmentRequest request,
            Guid managerUserId,
            CancellationToken ct = default)
        {
            _callCount++;
            if (_callCount == 1)
            {
                var result = await _inner.CreateAssignmentFromApprovedPlanAsync(request, managerUserId, ct);
                FirstCreatedAssignmentId = result.Id;
                return result;
            }

            throw new InvalidOperationException("Simulated transient failure on second plan.");
        }

        public Task<AssignmentDetailDto> CreateAsync(CreateCollectionAssignmentRequest request, Guid actorId, string role, CancellationToken ct = default)
            => _inner.CreateAsync(request, actorId, role, ct);

        public Task<RouteReadDto> ReorderStopsAsync(Guid assignmentId, ReorderRouteStopsRequest request, Guid actorId, string role, CancellationToken ct = default)
            => _inner.ReorderStopsAsync(assignmentId, request, actorId, role, ct);

        public Task<AssignmentDetailDto> CancelAsync(Guid assignmentId, CancelCollectionAssignmentRequest request, Guid actorId, string role, CancellationToken ct = default)
            => _inner.CancelAsync(assignmentId, request, actorId, role, ct);

        public Task<AssignmentDetailDto> StartAsync(Guid assignmentId, Guid actorId, string role, CancellationToken ct = default)
            => _inner.StartAsync(assignmentId, actorId, role, ct);

        public Task<AssignmentDetailDto> CompleteStopAsync(Guid assignmentId, Guid stopId, Guid actorId, string role, CancellationToken ct = default)
            => _inner.CompleteStopAsync(assignmentId, stopId, actorId, role, ct);

        public Task<AssignmentDetailDto> FailStopAsync(Guid assignmentId, Guid stopId, FailRouteStopRequest request, Guid actorId, string role, CancellationToken ct = default)
            => _inner.FailStopAsync(assignmentId, stopId, request, actorId, role, ct);

        public Task<AssignmentDetailDto> FinalizeAsync(Guid assignmentId, Guid actorId, string role, CancellationToken ct = default)
            => _inner.FinalizeAsync(assignmentId, actorId, role, ct);

        public Task<BinObservationDto> RecordDriverBinObservationAsync(Guid assignmentId, Guid stopId, RecordBinObservationRequest request, Guid actorId, string role, CancellationToken ct = default)
            => _inner.RecordDriverBinObservationAsync(assignmentId, stopId, request, actorId, role, ct);
    }

    [Fact]
    public async Task PostgreSql_AgentWorkflowDispatchExecution_MultiPlanFailure_RollsBackAllAssignmentsAndPersistsWorkflowFailure()
    {
        await using var db = CreateContext();
        await db.Database.OpenConnectionAsync();

        var suffix = Guid.NewGuid();
        var managerUser = CreateTestUser();
        managerUser.FullName = "Manager PostgreSQL Rollback Test";
        managerUser.UserName = $"mgr-{suffix:N}@smartwaste.test";
        managerUser.NormalizedUserName = managerUser.UserName.ToUpperInvariant();
        managerUser.Email = managerUser.UserName;
        managerUser.NormalizedEmail = managerUser.NormalizedUserName;

        var driver1User = CreateTestUser();
        driver1User.FullName = "Driver 1 PostgreSQL Rollback Test";
        driver1User.UserName = $"d1-{suffix:N}@smartwaste.test";
        driver1User.NormalizedUserName = driver1User.UserName.ToUpperInvariant();
        driver1User.Email = driver1User.UserName;
        driver1User.NormalizedEmail = driver1User.NormalizedUserName;

        var driver2User = CreateTestUser();
        driver2User.FullName = "Driver 2 PostgreSQL Rollback Test";
        driver2User.UserName = $"d2-{suffix:N}@smartwaste.test";
        driver2User.NormalizedUserName = driver2User.UserName.ToUpperInvariant();
        driver2User.Email = driver2User.UserName;
        driver2User.NormalizedEmail = driver2User.NormalizedUserName;

        var driver1Profile = new DriverProfile
        {
            UserId = driver1User.Id,
            LicenseNumber = $"L1-{suffix:N}"[..12],
            AvailabilityStatus = DriverAvailabilityStatus.Available,
            IsEligible = true,
            CreatedAt = DateTime.UtcNow
        };

        var driver2Profile = new DriverProfile
        {
            UserId = driver2User.Id,
            LicenseNumber = $"L2-{suffix:N}"[..12],
            AvailabilityStatus = DriverAvailabilityStatus.Available,
            IsEligible = true,
            CreatedAt = DateTime.UtcNow
        };

        var vehicle1Id = Guid.NewGuid();
        var vehicle1 = new Vehicle
        {
            Id = vehicle1Id,
            RegistrationNumber = $"V1-{suffix:N}"[..12],
            VehicleType = VehicleType.Compactor,
            CapacityLiters = 5000,
            OperationalStatus = VehicleOperationalStatus.Available,
            CreatedAt = DateTime.UtcNow
        };
        vehicle1.SupportedWasteTypes.Add(new VehicleSupportedWasteType { VehicleId = vehicle1Id, WasteType = WasteType.General });

        var vehicle2Id = Guid.NewGuid();
        var vehicle2 = new Vehicle
        {
            Id = vehicle2Id,
            RegistrationNumber = $"V2-{suffix:N}"[..12],
            VehicleType = VehicleType.Flatbed,
            CapacityLiters = 4000,
            OperationalStatus = VehicleOperationalStatus.Available,
            CreatedAt = DateTime.UtcNow
        };
        vehicle2.SupportedWasteTypes.Add(new VehicleSupportedWasteType { VehicleId = vehicle2Id, WasteType = WasteType.General });

        var report1Id = Guid.NewGuid();
        var report1 = new WasteReport
        {
            Id = report1Id,
            CitizenId = managerUser.Id,
            Description = "Rollback test report 1",
            AddressText = "Test Street 1, Colombo",
            Latitude = 6.9271,
            Longitude = 79.8612,
            Status = WasteReportStatus.UnderReview,
            WasteType = WasteType.General,
            CreatedAt = DateTime.UtcNow
        };

        var report2Id = Guid.NewGuid();
        var report2 = new WasteReport
        {
            Id = report2Id,
            CitizenId = managerUser.Id,
            Description = "Rollback test report 2",
            AddressText = "Test Street 2, Colombo",
            Latitude = 6.9280,
            Longitude = 79.8620,
            Status = WasteReportStatus.UnderReview,
            WasteType = WasteType.General,
            CreatedAt = DateTime.UtcNow
        };

        var task1Id = Guid.NewGuid();
        var task1 = new CollectionTask
        {
            Id = task1Id,
            TaskCode = $"T1-{suffix:N}"[..12],
            Status = CollectionTaskStatus.Scheduled,
            WasteReportId = report1Id,
            ScheduledAt = DateTime.UtcNow.AddHours(2),
            CreatedByUserId = managerUser.Id,
            CreatedAt = DateTime.UtcNow
        };

        var task2Id = Guid.NewGuid();
        var task2 = new CollectionTask
        {
            Id = task2Id,
            TaskCode = $"T2-{suffix:N}"[..12],
            Status = CollectionTaskStatus.Scheduled,
            WasteReportId = report2Id,
            ScheduledAt = DateTime.UtcNow.AddHours(2),
            CreatedByUserId = managerUser.Id,
            CreatedAt = DateTime.UtcNow
        };

        var workflowId = Guid.NewGuid();
        var c4StepId = Guid.NewGuid();
        var approvalId = Guid.NewGuid();

        var concreteC3 = JsonSerializer.Serialize(new
        {
            dispatchPlans = new[]
            {
                new
                {
                    planId = "plan-pg-1",
                    recommendedDriver = new { driverId = driver1User.Id, displayName = "Driver 1" },
                    recommendedVehicle = new { vehicleId = vehicle1Id, registrationNumber = vehicle1.RegistrationNumber, vehicleType = "Compactor" },
                    recommendedTasks = new[]
                    {
                        new { taskId = task1Id, taskCode = task1.TaskCode, sequence = 1, addressText = "Test Street 1, Colombo", reason = "P1" }
                    }
                },
                new
                {
                    planId = "plan-pg-2",
                    recommendedDriver = new { driverId = driver2User.Id, displayName = "Driver 2" },
                    recommendedVehicle = new { vehicleId = vehicle2Id, registrationNumber = vehicle2.RegistrationNumber, vehicleType = "Flatbed" },
                    recommendedTasks = new[]
                    {
                        new { taskId = task2Id, taskCode = task2.TaskCode, sequence = 1, addressText = "Test Street 2, Colombo", reason = "P2" }
                    }
                }
            },
            unplannedTasks = Array.Empty<object>()
        });

        var c4OutputJson = JsonSerializer.Serialize(new
        {
            validationOutcome = "ReadyForHumanReview",
            requiresAcknowledgement = false
        });

        var workflow = new AgentWorkflow
        {
            Id = workflowId,
            Objective = "Fleet dispatch PostgreSQL rollback verification",
            Status = AgentWorkflowStatus.DispatchApproved,
            CurrentStep = WorkflowStepType.OperationalValidation,
            InitiatedByUserId = managerUser.Id,
            Version = 3,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var c4Step = new AgentWorkflowStep
        {
            Id = c4StepId,
            WorkflowId = workflowId,
            Sequence = 1,
            StepType = WorkflowStepType.OperationalValidation,
            AgentName = "c4_validation_operations_agent",
            Status = WorkflowStepStatus.Completed,
            InputJson = concreteC3,
            OutputJson = c4OutputJson,
            StartedAt = DateTime.UtcNow.AddMinutes(-5),
            CompletedAt = DateTime.UtcNow.AddMinutes(-4)
        };

        var approval = new AgentWorkflowApproval
        {
            Id = approvalId,
            WorkflowId = workflowId,
            WorkflowStepId = c4StepId,
            ApprovalStage = WorkflowApprovalStage.FleetDispatch,
            Decision = WorkflowApprovalDecision.Approved,
            DecisionReason = "Approved by manager for rollback test.",
            DecisionPayloadJson = JsonSerializer.Serialize(new { acknowledgeWarnings = false }),
            DecidedByUserId = managerUser.Id,
            DecidedAt = DateTime.UtcNow.AddMinutes(-3)
        };

        workflow.Steps.Add(c4Step);
        workflow.Approvals.Add(approval);

        FaultInjectionAssignmentServiceDecorator? decorator = null;

        try
        {
            // Seed initial state in PostgreSQL
            db.Users.AddRange(managerUser, driver1User, driver2User);
            db.DriverProfiles.AddRange(driver1Profile, driver2Profile);
            db.Vehicles.AddRange(vehicle1, vehicle2);
            db.WasteReports.AddRange(report1, report2);
            db.CollectionTasks.AddRange(task1, task2);
            db.AgentWorkflows.Add(workflow);

            await db.SaveChangesAsync();

            // Set up workflow service with fault injection decorator wrapping real CollectionAssignmentService
            var userManager = MockUserManager();
            var realAssignmentSvc = new CollectionAssignmentService(db, userManager.Object);
            decorator = new FaultInjectionAssignmentServiceDecorator(realAssignmentSvc);

            var stateMachine = new AgentWorkflowStateMachine();
            var logger = NullLogger<AgentWorkflowService>.Instance;
            var agentWorkflowService = new AgentWorkflowService(
                db,
                stateMachine,
                logger,
                collectionAssignmentService: decorator,
                userManager: userManager.Object);

            // Execute dispatch plan: Plan 1 succeeds, Plan 2 throws
            var act = async () => await agentWorkflowService.ExecuteDispatchPlanAsync(
                workflowId,
                new ExecuteDispatchPlanRequest { ExpectedVersion = 3 },
                managerUser.Id);

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*Simulated transient failure on second plan*");

            decorator.FirstCreatedAssignmentId.Should().NotBeNull();
            var plan1AssignmentId = decorator.FirstCreatedAssignmentId!.Value;

            // Open a FRESH DbContext directly against PostgreSQL to verify committed state
            await using var verifyDb = CreateContext();

            // 1. Plan 1 assignment must have been ROLLED BACK by PostgreSQL
            var assignmentInDb = await verifyDb.CollectionAssignments.FindAsync(plan1AssignmentId);
            assignmentInDb.Should().BeNull("Plan 1 assignment must have rolled back when Plan 2 failed");

            var anyAssignmentsForDrivers = await verifyDb.CollectionAssignments
                .AnyAsync(a => a.DriverId == driver1User.Id || a.DriverId == driver2User.Id);
            anyAssignmentsForDrivers.Should().BeFalse("No assignments should be committed for either driver");

            // 2. Plan 1 routes, stops, and claims must have been ROLLED BACK
            var anyRoutes = await verifyDb.Routes
                .AnyAsync(r => r.CollectionAssignmentId == plan1AssignmentId);
            anyRoutes.Should().BeFalse("Plan 1 route must have rolled back");

            var anyStops = await verifyDb.RouteStops
                .AnyAsync(s => s.CollectionTaskId == task1Id || s.CollectionTaskId == task2Id);
            anyStops.Should().BeFalse("Route stops must have rolled back");

            var anyClaims = await verifyDb.CollectionAssignmentTaskClaims
                .AnyAsync(c => c.CollectionTaskId == task1Id || c.CollectionTaskId == task2Id);
            anyClaims.Should().BeFalse("Task claims must have rolled back");

            // 3. Tasks must NOT remain assigned — they must remain Scheduled
            var persistedTask1 = await verifyDb.CollectionTasks.FindAsync(task1Id);
            persistedTask1!.Status.Should().Be(CollectionTaskStatus.Scheduled, "Task 1 status change to Assigned must have rolled back");

            var persistedTask2 = await verifyDb.CollectionTasks.FindAsync(task2Id);
            persistedTask2!.Status.Should().Be(CollectionTaskStatus.Scheduled, "Task 2 must remain Scheduled");

            // 4. Drivers and vehicles must remain Available
            var persistedD1 = await verifyDb.DriverProfiles.FirstAsync(d => d.UserId == driver1User.Id);
            persistedD1.AvailabilityStatus.Should().Be(DriverAvailabilityStatus.Available);

            var persistedD2 = await verifyDb.DriverProfiles.FirstAsync(d => d.UserId == driver2User.Id);
            persistedD2.AvailabilityStatus.Should().Be(DriverAvailabilityStatus.Available);

            var persistedV1 = await verifyDb.Vehicles.FindAsync(vehicle1Id);
            persistedV1!.OperationalStatus.Should().Be(VehicleOperationalStatus.Available);

            var persistedV2 = await verifyDb.Vehicles.FindAsync(vehicle2Id);
            persistedV2!.OperationalStatus.Should().Be(VehicleOperationalStatus.Available);

            // 5. Durable workflow failure state must be persisted in PostgreSQL
            var persistedWf = await verifyDb.AgentWorkflows.FindAsync(workflowId);
            persistedWf.Should().NotBeNull();
            persistedWf!.Status.Should().Be(AgentWorkflowStatus.Failed);
            persistedWf.CurrentStep.Should().Be(WorkflowStepType.AssignmentExecution);

            var persistedExecStep = await verifyDb.AgentWorkflowSteps
                .FirstAsync(s => s.WorkflowId == workflowId && s.StepType == WorkflowStepType.AssignmentExecution);
            persistedExecStep.Status.Should().Be(WorkflowStepStatus.Failed);
            persistedExecStep.ErrorMessage.Should().Contain("Simulated transient failure on second plan");

            var persistedExecResult = await verifyDb.AgentWorkflowExecutionResults
                .FirstAsync(e => e.WorkflowId == workflowId && e.ExecutionType == WorkflowExecutionType.CollectionAssignment);
            persistedExecResult.Status.Should().Be(WorkflowExecutionStatus.Failed);
            persistedExecResult.ErrorMessage.Should().Contain("Simulated transient failure on second plan");

            // 6. Both transitions must exist: DispatchApproved -> ExecutingAssignments and ExecutingAssignments -> Failed
            var transitions = await verifyDb.AgentWorkflowTransitions
                .Where(t => t.WorkflowId == workflowId)
                .OrderBy(t => t.ChangedAt)
                .ToListAsync();

            transitions.Should().HaveCount(2);
            transitions[0].FromStatus.Should().Be(AgentWorkflowStatus.DispatchApproved);
            transitions[0].ToStatus.Should().Be(AgentWorkflowStatus.ExecutingAssignments);
            transitions[1].FromStatus.Should().Be(AgentWorkflowStatus.ExecutingAssignments);
            transitions[1].ToStatus.Should().Be(AgentWorkflowStatus.Failed);
        }
        finally
        {
            // Clean up all seeded test data in reverse foreign key order
            await using var cleanupDb = CreateContext();
            await cleanupDb.Database.OpenConnectionAsync();
            try
            {
                if (decorator?.FirstCreatedAssignmentId != null)
                {
                    var aId = decorator.FirstCreatedAssignmentId.Value;
                    await ExecuteAsync(cleanupDb, $"DELETE FROM \"RouteStopStatusHistories\" WHERE \"RouteStopId\" IN (SELECT \"Id\" FROM \"RouteStops\" WHERE \"RouteId\" IN (SELECT \"Id\" FROM \"Routes\" WHERE \"CollectionAssignmentId\" = {aId}));");
                    await ExecuteAsync(cleanupDb, $"DELETE FROM \"RouteStops\" WHERE \"RouteId\" IN (SELECT \"Id\" FROM \"Routes\" WHERE \"CollectionAssignmentId\" = {aId});");
                    await ExecuteAsync(cleanupDb, $"DELETE FROM \"Routes\" WHERE \"CollectionAssignmentId\" = {aId};");
                    await ExecuteAsync(cleanupDb, $"DELETE FROM \"CollectionAssignmentTaskClaims\" WHERE \"CollectionAssignmentId\" = {aId};");
                    await ExecuteAsync(cleanupDb, $"DELETE FROM \"CollectionAssignmentStatusHistories\" WHERE \"CollectionAssignmentId\" = {aId};");
                    await ExecuteAsync(cleanupDb, $"DELETE FROM \"CollectionAssignments\" WHERE \"Id\" = {aId};");
                }

                await ExecuteAsync(cleanupDb, $"DELETE FROM \"AgentWorkflowTransitions\" WHERE \"WorkflowId\" = {workflowId};");
                await ExecuteAsync(cleanupDb, $"DELETE FROM \"AgentWorkflowExecutionResults\" WHERE \"WorkflowId\" = {workflowId};");
                await ExecuteAsync(cleanupDb, $"DELETE FROM \"AgentWorkflowApprovals\" WHERE \"WorkflowId\" = {workflowId};");
                await ExecuteAsync(cleanupDb, $"DELETE FROM \"AgentWorkflowSteps\" WHERE \"WorkflowId\" = {workflowId};");
                await ExecuteAsync(cleanupDb, $"DELETE FROM \"AgentWorkflows\" WHERE \"Id\" = {workflowId};");

                await ExecuteAsync(cleanupDb, $"DELETE FROM \"CollectionTaskStatusHistories\" WHERE \"CollectionTaskId\" = {task1Id} OR \"CollectionTaskId\" = {task2Id};");
                await ExecuteAsync(cleanupDb, $"DELETE FROM \"CollectionTasks\" WHERE \"Id\" = {task1Id} OR \"Id\" = {task2Id};");
                await ExecuteAsync(cleanupDb, $"DELETE FROM \"WasteReports\" WHERE \"Id\" = {report1Id} OR \"Id\" = {report2Id};");

                await ExecuteAsync(cleanupDb, $"DELETE FROM \"VehicleSupportedWasteTypes\" WHERE \"VehicleId\" = {vehicle1Id} OR \"VehicleId\" = {vehicle2Id};");
                await ExecuteAsync(cleanupDb, $"DELETE FROM \"Vehicles\" WHERE \"Id\" = {vehicle1Id} OR \"Id\" = {vehicle2Id};");
                await ExecuteAsync(cleanupDb, $"DELETE FROM \"DriverProfiles\" WHERE \"UserId\" = {driver1User.Id} OR \"UserId\" = {driver2User.Id};");

                await ExecuteAsync(cleanupDb, $"DELETE FROM \"AspNetUserRoles\" WHERE \"UserId\" = {managerUser.Id} OR \"UserId\" = {driver1User.Id} OR \"UserId\" = {driver2User.Id};");
                await ExecuteAsync(cleanupDb, $"DELETE FROM \"AspNetUsers\" WHERE \"Id\" = {managerUser.Id} OR \"Id\" = {driver1User.Id} OR \"Id\" = {driver2User.Id};");
            }
            finally
            {
                await cleanupDb.Database.CloseConnectionAsync();
                await db.Database.CloseConnectionAsync();
            }
        }
    }
}
