using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SmartWaste.Application.Collection.DTOs.Requests;
using SmartWaste.Application.Collection.DTOs.Responses;
using SmartWaste.Application.Collection.Interfaces;
using SmartWaste.Application.Common.Exceptions;
using SmartWaste.Application.Workflow.DTOs.Requests;
using SmartWaste.Application.Workflow.Services;
using SmartWaste.Domain.Collection.Entities;
using SmartWaste.Domain.Collection.Enums;
using SmartWaste.Domain.Common;
using SmartWaste.Domain.Entities;
using SmartWaste.Domain.Reporting.Entities;
using SmartWaste.Domain.Reporting.Enums;
using SmartWaste.Domain.Workflow.Entities;
using SmartWaste.Domain.Workflow.Enums;
using SmartWaste.Infrastructure.Collection.Services;
using SmartWaste.Infrastructure.Persistence;
using SmartWaste.Infrastructure.Workflow.Services;
using Xunit;

namespace SmartWaste.Tests.Workflow.Services;

public class AgentWorkflowDispatchExecutionBridgeTests
{
    private static AppDbContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    private static UserManager<AppUser> MockUserManager(bool isDriver = true)
    {
        var store = new Mock<IUserStore<AppUser>>();
        var manager = new Mock<UserManager<AppUser>>(store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        manager.Setup(x => x.IsInRoleAsync(It.IsAny<AppUser>(), AppRoles.Driver)).ReturnsAsync(isDriver);
        return manager.Object;
    }

    private static AgentWorkflowService CreateService(
        AppDbContext db,
        ICollectionAssignmentService? assignmentService = null,
        UserManager<AppUser>? userManager = null)
    {
        var stateMachine = new AgentWorkflowStateMachine();
        var logger = NullLogger<AgentWorkflowService>.Instance;
        var users = userManager ?? MockUserManager();
        var svc = assignmentService ?? new CollectionAssignmentService(db, users);
        return new AgentWorkflowService(db, stateMachine, logger, collectionAssignmentService: svc, userManager: users);
    }

    private sealed class SeedTestData
    {
        public Guid WorkflowId { get; set; }
        public Guid C4StepId { get; set; }
        public Guid ManagerUserId { get; set; }
        public Guid DriverUserId { get; set; }
        public Guid Driver2UserId { get; set; }
        public Guid VehicleId { get; set; }
        public Guid Vehicle2Id { get; set; }
        public Guid Task1Id { get; set; }
        public Guid Task2Id { get; set; }
        public Guid Task3Id { get; set; }
        public Guid UnplannedTaskId { get; set; }
        public AgentWorkflow Workflow { get; set; } = null!;
    }

    private static SeedTestData SeedStandardDispatchScenario(
        AppDbContext db,
        string? customC3Json = null,
        string? customC4OutputJson = null,
        bool acknowledgeWarningsInApproval = false,
        AgentWorkflowStatus status = AgentWorkflowStatus.DispatchApproved,
        int version = 3,
        Guid? explicitWorkflowStepId = null,
        bool omitApprovalStepId = false)
    {
        var managerId = Guid.NewGuid();
        db.Users.Add(new AppUser
        {
            Id = managerId,
            UserName = "manager@smartwaste.lk",
            Email = "manager@smartwaste.lk",
            FullName = "Municipal Manager",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });

        // Driver 1
        var driver1Id = Guid.NewGuid();
        var driver1User = new AppUser
        {
            Id = driver1Id,
            UserName = "driver1@smartwaste.lk",
            Email = "driver1@smartwaste.lk",
            FullName = "Driver One",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };
        db.Users.Add(driver1User);
        db.DriverProfiles.Add(new DriverProfile
        {
            UserId = driver1Id,
            User = driver1User,
            AvailabilityStatus = DriverAvailabilityStatus.Available,
            LicenseNumber = "DL12345",
            IsEligible = true
        });

        // Driver 2
        var driver2Id = Guid.NewGuid();
        var driver2User = new AppUser
        {
            Id = driver2Id,
            UserName = "driver2@smartwaste.lk",
            Email = "driver2@smartwaste.lk",
            FullName = "Driver Two",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };
        db.Users.Add(driver2User);
        db.DriverProfiles.Add(new DriverProfile
        {
            UserId = driver2Id,
            User = driver2User,
            AvailabilityStatus = DriverAvailabilityStatus.Available,
            LicenseNumber = "DL67890",
            IsEligible = true
        });

        // Vehicle 1
        var vehicle1Id = Guid.NewGuid();
        var vehicle1 = new Vehicle
        {
            Id = vehicle1Id,
            RegistrationNumber = "WP-CAB-1001",
            VehicleType = VehicleType.Compactor,
            CapacityLiters = 5000,
            OperationalStatus = VehicleOperationalStatus.Available
        };
        vehicle1.SupportedWasteTypes.Add(new VehicleSupportedWasteType { VehicleId = vehicle1Id, WasteType = WasteType.General });
        vehicle1.SupportedWasteTypes.Add(new VehicleSupportedWasteType { VehicleId = vehicle1Id, WasteType = WasteType.Organic });
        db.Vehicles.Add(vehicle1);

        // Vehicle 2
        var vehicle2Id = Guid.NewGuid();
        var vehicle2 = new Vehicle
        {
            Id = vehicle2Id,
            RegistrationNumber = "WP-CAB-2002",
            VehicleType = VehicleType.Flatbed,
            CapacityLiters = 3000,
            OperationalStatus = VehicleOperationalStatus.Available
        };
        vehicle2.SupportedWasteTypes.Add(new VehicleSupportedWasteType { VehicleId = vehicle2Id, WasteType = WasteType.General });
        db.Vehicles.Add(vehicle2);

        // Authoritative Tasks
        var task1Id = Guid.NewGuid();
        var report1Id = Guid.NewGuid();
        db.WasteReports.Add(new WasteReport
        {
            Id = report1Id,
            CitizenId = Guid.NewGuid(),
            Description = "Market waste",
            AddressText = "Market St, Colombo",
            Latitude = 6.9271,
            Longitude = 79.8612,
            Status = WasteReportStatus.UnderReview,
            WasteType = WasteType.General
        });
        db.CollectionTasks.Add(new CollectionTask
        {
            Id = task1Id,
            TaskCode = "TASK-001",
            Status = CollectionTaskStatus.Scheduled,
            WasteReportId = report1Id,
            ScheduledAt = DateTime.UtcNow.AddHours(2),
            CreatedAt = DateTime.UtcNow
        });

        var task2Id = Guid.NewGuid();
        var report2Id = Guid.NewGuid();
        db.WasteReports.Add(new WasteReport
        {
            Id = report2Id,
            CitizenId = Guid.NewGuid(),
            Description = "Residential waste",
            AddressText = "2nd Lane, Colombo",
            Latitude = 6.9280,
            Longitude = 79.8620,
            Status = WasteReportStatus.UnderReview,
            WasteType = WasteType.General
        });
        db.CollectionTasks.Add(new CollectionTask
        {
            Id = task2Id,
            TaskCode = "TASK-002",
            Status = CollectionTaskStatus.Scheduled,
            WasteReportId = report2Id,
            ScheduledAt = DateTime.UtcNow.AddHours(2),
            CreatedAt = DateTime.UtcNow
        });

        var task3Id = Guid.NewGuid();
        var report3Id = Guid.NewGuid();
        db.WasteReports.Add(new WasteReport
        {
            Id = report3Id,
            CitizenId = Guid.NewGuid(),
            Description = "Commercial waste",
            AddressText = "Main St, Colombo",
            Latitude = 6.9290,
            Longitude = 79.8630,
            Status = WasteReportStatus.UnderReview,
            WasteType = WasteType.General
        });
        db.CollectionTasks.Add(new CollectionTask
        {
            Id = task3Id,
            TaskCode = "TASK-003",
            Status = CollectionTaskStatus.Scheduled,
            WasteReportId = report3Id,
            ScheduledAt = DateTime.UtcNow.AddHours(2),
            CreatedAt = DateTime.UtcNow
        });

        var unplannedTaskId = Guid.NewGuid();
        var reportUnplannedId = Guid.NewGuid();
        db.WasteReports.Add(new WasteReport
        {
            Id = reportUnplannedId,
            CitizenId = Guid.NewGuid(),
            Description = "Remote waste",
            AddressText = "Outstation Rd",
            Latitude = 6.9400,
            Longitude = 79.8700,
            Status = WasteReportStatus.UnderReview,
            WasteType = WasteType.General
        });
        db.CollectionTasks.Add(new CollectionTask
        {
            Id = unplannedTaskId,
            TaskCode = "TASK-UNP",
            Status = CollectionTaskStatus.Scheduled,
            WasteReportId = reportUnplannedId,
            ScheduledAt = DateTime.UtcNow.AddHours(2),
            CreatedAt = DateTime.UtcNow
        });

        // Default C3 Snapshot
        var c3Json = customC3Json ?? JsonSerializer.Serialize(new
        {
            dispatchPlans = new[]
            {
                new
                {
                    planId = "plan-1",
                    recommendedDriver = new { driverId = driver1Id, displayName = "Driver One" },
                    recommendedVehicle = new { vehicleId = vehicle1Id, registrationNumber = "WP-CAB-1001", vehicleType = "Compactor" },
                    recommendedTasks = new[]
                    {
                        new { taskId = task1Id, taskCode = "TASK-001", sequence = 1, addressText = "Market St, Colombo", reason = "Priority 1" },
                        new { taskId = task2Id, taskCode = "TASK-002", sequence = 2, addressText = "2nd Lane, Colombo", reason = "Priority 2" }
                    }
                }
            },
            unplannedTasks = new[]
            {
                new { taskId = unplannedTaskId, taskCode = "TASK-UNP", reason = "Vehicle capacity limit reached" }
            }
        });

        // Default C4 Output
        var c4OutputJson = customC4OutputJson ?? JsonSerializer.Serialize(new
        {
            validationOutcome = "ReadyForHumanReview",
            requiresAcknowledgement = false
        });

        var workflowId = Guid.NewGuid();
        var c4StepId = Guid.NewGuid();

        var workflow = new AgentWorkflow
        {
            Id = workflowId,
            Objective = "Fleet dispatch workflow",
            Status = status,
            CurrentStep = WorkflowStepType.OperationalValidation,
            InitiatedByUserId = managerId,
            Version = version,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var c4Step = new AgentWorkflowStep
        {
            Id = c4StepId,
            WorkflowId = workflowId,
            Sequence = 4,
            StepType = WorkflowStepType.OperationalValidation,
            AgentName = "c4_validation_operations_agent",
            Status = WorkflowStepStatus.Completed,
            InputJson = c3Json,
            OutputJson = c4OutputJson,
            StartedAt = DateTime.UtcNow.AddMinutes(-10),
            CompletedAt = DateTime.UtcNow.AddMinutes(-5)
        };

        var approval = new AgentWorkflowApproval
        {
            Id = Guid.NewGuid(),
            WorkflowId = workflowId,
            WorkflowStepId = omitApprovalStepId ? null : (explicitWorkflowStepId ?? c4StepId),
            ApprovalStage = WorkflowApprovalStage.FleetDispatch,
            Decision = WorkflowApprovalDecision.Approved,
            DecisionReason = "Approved by manager.",
            DecisionPayloadJson = JsonSerializer.Serialize(new { acknowledgeWarnings = acknowledgeWarningsInApproval }),
            DecidedByUserId = managerId,
            DecidedAt = DateTime.UtcNow.AddMinutes(-3)
        };

        workflow.Steps.Add(c4Step);
        workflow.Approvals.Add(approval);
        db.AgentWorkflows.Add(workflow);
        db.SaveChanges();

        return new SeedTestData
        {
            WorkflowId = workflowId,
            C4StepId = c4StepId,
            ManagerUserId = managerId,
            DriverUserId = driver1Id,
            Driver2UserId = driver2Id,
            VehicleId = vehicle1Id,
            Vehicle2Id = vehicle2Id,
            Task1Id = task1Id,
            Task2Id = task2Id,
            Task3Id = task3Id,
            UnplannedTaskId = unplannedTaskId,
            Workflow = workflow
        };
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Section 72: TEST — CANONICAL SINGLE PLAN
    // ──────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ExecuteDispatchPlan_PersistedC3DoesNotMatchApprovedC4Input_ThrowsConflictBeforeMutation()
    {
        var dbName = $"DispatchBridge_{Guid.NewGuid():N}";
        using var db = CreateContext(dbName);
        var seed = SeedStandardDispatchScenario(db);
        db.AgentWorkflowSteps.Add(new AgentWorkflowStep
        {
            Id = Guid.NewGuid(),
            WorkflowId = seed.WorkflowId,
            Sequence = 3,
            StepType = WorkflowStepType.FleetPlanning,
            AgentName = "fleet_route_agent",
            Status = WorkflowStepStatus.Completed,
            OutputJson = "{\"dispatchPlans\":[]}",
            StartedAt = DateTime.UtcNow.AddMinutes(-11),
            CompletedAt = DateTime.UtcNow.AddMinutes(-10)
        });
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var act = async () => await service.ExecuteDispatchPlanAsync(
            seed.WorkflowId,
            new ExecuteDispatchPlanRequest { ExpectedVersion = 3 },
            seed.ManagerUserId);

        await act.Should().ThrowAsync<BusinessRuleConflictException>()
            .WithMessage("*does not validate the persisted fleet planning snapshot*");
        (await db.CollectionAssignments.CountAsync()).Should().Be(0);
        (await db.AgentWorkflows.FindAsync(seed.WorkflowId))!.Status.Should().Be(AgentWorkflowStatus.DispatchApproved);
    }

    [Fact]
    public async Task ExecuteDispatchPlan_CanonicalSinglePlan_CreatesAssignment_SetsRouteStops_AndCompletes()
    {
        var dbName = $"DispatchBridge_{Guid.NewGuid():N}";
        using var db = CreateContext(dbName);
        var seed = SeedStandardDispatchScenario(db);
        var service = CreateService(db);

        var request = new ExecuteDispatchPlanRequest { ExpectedVersion = 3 };
        var result = await service.ExecuteDispatchPlanAsync(seed.WorkflowId, request, seed.ManagerUserId);

        result.Status.Should().Be(AgentWorkflowStatus.Completed);
        result.CurrentStep.Should().Be(WorkflowStepType.AssignmentExecution);
        result.Version.Should().Be(5); // 3 -> 4 (ExecutingAssignments) -> 5 (Completed)

        // Verify assignment in DB
        var assignments = await db.CollectionAssignments
            .Where(a => a.DriverId == seed.DriverUserId)
            .ToListAsync();
        assignments.Should().HaveCount(1);
        var assignment = assignments[0];
        assignment.VehicleId.Should().Be(seed.VehicleId);
        assignment.Status.Should().Be(CollectionAssignmentStatus.Assigned);

        // Verify route and stops
        var route = await db.Routes.Include(r => r.Stops).FirstAsync(r => r.CollectionAssignmentId == assignment.Id);
        route.Stops.Should().HaveCount(2);

        var sortedStops = route.Stops.OrderBy(s => s.Sequence).ToList();
        sortedStops[0].CollectionTaskId.Should().Be(seed.Task1Id);
        sortedStops[0].Sequence.Should().Be(1);
        sortedStops[1].CollectionTaskId.Should().Be(seed.Task2Id);
        sortedStops[1].Sequence.Should().Be(2);

        // Verify task claims
        var claims = await db.CollectionAssignmentTaskClaims
            .Where(c => c.CollectionAssignmentId == assignment.Id && c.IsActive)
            .ToListAsync();
        claims.Should().HaveCount(2);

        // Verify Step audit
        var execStep = await db.AgentWorkflowSteps
            .FirstOrDefaultAsync(s => s.WorkflowId == seed.WorkflowId && s.StepType == WorkflowStepType.AssignmentExecution);
        execStep.Should().NotBeNull();
        execStep!.Status.Should().Be(WorkflowStepStatus.Completed);

        // Verify ExecutionResult audit
        var execResult = await db.AgentWorkflowExecutionResults
            .FirstOrDefaultAsync(e => e.WorkflowId == seed.WorkflowId && e.ExecutionType == WorkflowExecutionType.CollectionAssignment);
        execResult.Should().NotBeNull();
        execResult!.Status.Should().Be(WorkflowExecutionStatus.Succeeded);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Section 73: TEST — MULTIPLE PLANS
    // ──────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ExecuteDispatchPlan_MultiplePlans_CreatesMultipleAssignmentsAtomically()
    {
        var dbName = $"DispatchBridge_{Guid.NewGuid():N}";
        using var db = CreateContext(dbName);

        var seed = SeedStandardDispatchScenario(db);
        var multiPlanJson = JsonSerializer.Serialize(new
        {
            dispatchPlans = new[]
            {
                new
                {
                    planId = "plan-1",
                    recommendedDriver = new { driverId = seed.DriverUserId, displayName = "Driver One" },
                    recommendedVehicle = new { vehicleId = seed.VehicleId, registrationNumber = "WP-CAB-1001", vehicleType = "Compactor" },
                    recommendedTasks = new[]
                    {
                        new { taskId = seed.Task1Id, taskCode = "TASK-001", sequence = 1, addressText = "Market St", reason = "P1" }
                    }
                },
                new
                {
                    planId = "plan-2",
                    recommendedDriver = new { driverId = seed.Driver2UserId, displayName = "Driver Two" },
                    recommendedVehicle = new { vehicleId = seed.Vehicle2Id, registrationNumber = "WP-CAB-2002", vehicleType = "Flatbed" },
                    recommendedTasks = new[]
                    {
                        new { taskId = seed.Task2Id, taskCode = "TASK-002", sequence = 1, addressText = "2nd Lane", reason = "P2" }
                    }
                }
            },
            unplannedTasks = Array.Empty<object>()
        });

        var c4Step = await db.AgentWorkflowSteps.FirstAsync(s => s.Id == seed.C4StepId);
        c4Step.InputJson = multiPlanJson;
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.ExecuteDispatchPlanAsync(seed.WorkflowId, new ExecuteDispatchPlanRequest { ExpectedVersion = 3 }, seed.ManagerUserId);

        result.Status.Should().Be(AgentWorkflowStatus.Completed);

        var assignments = await db.CollectionAssignments.ToListAsync();
        assignments.Should().HaveCount(2);
        assignments.Should().Contain(a => a.DriverId == seed.DriverUserId && a.VehicleId == seed.VehicleId);
        assignments.Should().Contain(a => a.DriverId == seed.Driver2UserId && a.VehicleId == seed.Vehicle2Id);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Section 74: TEST — UNPLANNED TASKS
    // ──────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ExecuteDispatchPlan_WithUnplannedTasks_ExcludesUnplannedFromAssignments_AndRecordsInAudit()
    {
        var dbName = $"DispatchBridge_{Guid.NewGuid():N}";
        using var db = CreateContext(dbName);
        var seed = SeedStandardDispatchScenario(db);
        var service = CreateService(db);

        var result = await service.ExecuteDispatchPlanAsync(seed.WorkflowId, new ExecuteDispatchPlanRequest { ExpectedVersion = 3 }, seed.ManagerUserId);

        result.Status.Should().Be(AgentWorkflowStatus.Completed);
        result.FinalOutcome.Should().Contain("1 tasks remained unplanned");

        // Verify unplanned task was NOT assigned
        var unplannedClaims = await db.CollectionAssignmentTaskClaims
            .Where(c => c.CollectionTaskId == seed.UnplannedTaskId)
            .ToListAsync();
        unplannedClaims.Should().BeEmpty();

        var unplannedTaskInDb = await db.CollectionTasks.FirstAsync(t => t.Id == seed.UnplannedTaskId);
        unplannedTaskInDb.Status.Should().Be(CollectionTaskStatus.Scheduled); // unchanged!
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Section 75: TEST — ORDERED ROUTE STOPS
    // ──────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ExecuteDispatchPlan_OrderedRouteStops_RespectsSequenceFieldOverJsonOrder()
    {
        var dbName = $"DispatchBridge_{Guid.NewGuid():N}";
        using var db = CreateContext(dbName);
        var seed = SeedStandardDispatchScenario(db);

        // JSON has Task2 first (seq 2), then Task1 (seq 1)
        var jumbledJson = JsonSerializer.Serialize(new
        {
            dispatchPlans = new[]
            {
                new
                {
                    planId = "plan-1",
                    recommendedDriver = new { driverId = seed.DriverUserId, displayName = "Driver One" },
                    recommendedVehicle = new { vehicleId = seed.VehicleId, registrationNumber = "WP-CAB-1001", vehicleType = "Compactor" },
                    recommendedTasks = new[]
                    {
                        new { taskId = seed.Task2Id, taskCode = "TASK-002", sequence = 2, addressText = "Stop 2", reason = "R2" },
                        new { taskId = seed.Task1Id, taskCode = "TASK-001", sequence = 1, addressText = "Stop 1", reason = "R1" }
                    }
                }
            },
            unplannedTasks = Array.Empty<object>()
        });

        var c4Step = await db.AgentWorkflowSteps.FirstAsync(s => s.Id == seed.C4StepId);
        c4Step.InputJson = jumbledJson;
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await service.ExecuteDispatchPlanAsync(seed.WorkflowId, new ExecuteDispatchPlanRequest { ExpectedVersion = 3 }, seed.ManagerUserId);

        var assignment = await db.CollectionAssignments.FirstAsync();
        var route = await db.Routes.Include(r => r.Stops).FirstAsync(r => r.CollectionAssignmentId == assignment.Id);
        var stops = route.Stops.OrderBy(s => s.Sequence).ToList();
        stops[0].CollectionTaskId.Should().Be(seed.Task1Id);
        stops[0].Sequence.Should().Be(1);
        stops[1].CollectionTaskId.Should().Be(seed.Task2Id);
        stops[1].Sequence.Should().Be(2);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Section 76: TEST — DUPLICATE TASK
    // ──────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ExecuteDispatchPlan_DuplicateTaskAcrossPlans_RejectsWith409BeforePhaseA()
    {
        var dbName = $"DispatchBridge_{Guid.NewGuid():N}";
        using var db = CreateContext(dbName);
        var seed = SeedStandardDispatchScenario(db);

        var duplicateTaskJson = JsonSerializer.Serialize(new
        {
            dispatchPlans = new[]
            {
                new
                {
                    planId = "plan-1",
                    recommendedDriver = new { driverId = seed.DriverUserId, displayName = "Driver 1" },
                    recommendedVehicle = new { vehicleId = seed.VehicleId, registrationNumber = "WP-CAB-1001", vehicleType = "Compactor" },
                    recommendedTasks = new[] { new { taskId = seed.Task1Id, taskCode = "TASK-001", sequence = 1, reason = "R1" } }
                },
                new
                {
                    planId = "plan-2",
                    recommendedDriver = new { driverId = seed.Driver2UserId, displayName = "Driver 2" },
                    recommendedVehicle = new { vehicleId = seed.Vehicle2Id, registrationNumber = "WP-CAB-2002", vehicleType = "Flatbed" },
                    recommendedTasks = new[] { new { taskId = seed.Task1Id, taskCode = "TASK-001", sequence = 1, reason = "R2" } }
                }
            }
        });

        var c4Step = await db.AgentWorkflowSteps.FirstAsync(s => s.Id == seed.C4StepId);
        c4Step.InputJson = duplicateTaskJson;
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var act = async () => await service.ExecuteDispatchPlanAsync(seed.WorkflowId, new ExecuteDispatchPlanRequest { ExpectedVersion = 3 }, seed.ManagerUserId);

        await act.Should().ThrowAsync<BusinessRuleConflictException>()
            .WithMessage("*appears in multiple dispatch plans*");

        var wf = await db.AgentWorkflows.FirstAsync(w => w.Id == seed.WorkflowId);
        wf.Status.Should().Be(AgentWorkflowStatus.DispatchApproved); // Unchanged!
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Section 77: TEST — PLANNED / UNPLANNED OVERLAP
    // ──────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ExecuteDispatchPlan_PlannedAndUnplannedTaskOverlap_RejectsWith409BeforePhaseA()
    {
        var dbName = $"DispatchBridge_{Guid.NewGuid():N}";
        using var db = CreateContext(dbName);
        var seed = SeedStandardDispatchScenario(db);

        var overlapJson = JsonSerializer.Serialize(new
        {
            dispatchPlans = new[]
            {
                new
                {
                    planId = "plan-1",
                    recommendedDriver = new { driverId = seed.DriverUserId, displayName = "Driver 1" },
                    recommendedVehicle = new { vehicleId = seed.VehicleId, registrationNumber = "WP-CAB-1001", vehicleType = "Compactor" },
                    recommendedTasks = new[] { new { taskId = seed.Task1Id, taskCode = "TASK-001", sequence = 1, reason = "R1" } }
                }
            },
            unplannedTasks = new[]
            {
                new { taskId = seed.Task1Id, taskCode = "TASK-001", reason = "Also unplanned" }
            }
        });

        var c4Step = await db.AgentWorkflowSteps.FirstAsync(s => s.Id == seed.C4StepId);
        c4Step.InputJson = overlapJson;
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var act = async () => await service.ExecuteDispatchPlanAsync(seed.WorkflowId, new ExecuteDispatchPlanRequest { ExpectedVersion = 3 }, seed.ManagerUserId);

        await act.Should().ThrowAsync<BusinessRuleConflictException>()
            .WithMessage("*cannot appear as both planned and unplanned*");

        var wf = await db.AgentWorkflows.FirstAsync(w => w.Id == seed.WorkflowId);
        wf.Status.Should().Be(AgentWorkflowStatus.DispatchApproved);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Section 78: TEST — DUPLICATE DRIVER
    // ──────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ExecuteDispatchPlan_DuplicateDriverAcrossPlans_RejectsWith409BeforePhaseA()
    {
        var dbName = $"DispatchBridge_{Guid.NewGuid():N}";
        using var db = CreateContext(dbName);
        var seed = SeedStandardDispatchScenario(db);

        var duplicateDriverJson = JsonSerializer.Serialize(new
        {
            dispatchPlans = new[]
            {
                new
                {
                    planId = "plan-1",
                    recommendedDriver = new { driverId = seed.DriverUserId, displayName = "Driver 1" },
                    recommendedVehicle = new { vehicleId = seed.VehicleId, registrationNumber = "WP-CAB-1001", vehicleType = "Compactor" },
                    recommendedTasks = new[] { new { taskId = seed.Task1Id, taskCode = "TASK-001", sequence = 1, reason = "R1" } }
                },
                new
                {
                    planId = "plan-2",
                    recommendedDriver = new { driverId = seed.DriverUserId, displayName = "Driver 1" },
                    recommendedVehicle = new { vehicleId = seed.Vehicle2Id, registrationNumber = "WP-CAB-2002", vehicleType = "Flatbed" },
                    recommendedTasks = new[] { new { taskId = seed.Task2Id, taskCode = "TASK-002", sequence = 1, reason = "R2" } }
                }
            }
        });

        var c4Step = await db.AgentWorkflowSteps.FirstAsync(s => s.Id == seed.C4StepId);
        c4Step.InputJson = duplicateDriverJson;
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var act = async () => await service.ExecuteDispatchPlanAsync(seed.WorkflowId, new ExecuteDispatchPlanRequest { ExpectedVersion = 3 }, seed.ManagerUserId);

        await act.Should().ThrowAsync<BusinessRuleConflictException>()
            .WithMessage("*assigned to multiple dispatch plans*");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Section 79: TEST — DUPLICATE VEHICLE
    // ──────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ExecuteDispatchPlan_DuplicateVehicleAcrossPlans_RejectsWith409BeforePhaseA()
    {
        var dbName = $"DispatchBridge_{Guid.NewGuid():N}";
        using var db = CreateContext(dbName);
        var seed = SeedStandardDispatchScenario(db);

        var duplicateVehicleJson = JsonSerializer.Serialize(new
        {
            dispatchPlans = new[]
            {
                new
                {
                    planId = "plan-1",
                    recommendedDriver = new { driverId = seed.DriverUserId, displayName = "Driver 1" },
                    recommendedVehicle = new { vehicleId = seed.VehicleId, registrationNumber = "WP-CAB-1001", vehicleType = "Compactor" },
                    recommendedTasks = new[] { new { taskId = seed.Task1Id, taskCode = "TASK-001", sequence = 1, reason = "R1" } }
                },
                new
                {
                    planId = "plan-2",
                    recommendedDriver = new { driverId = seed.Driver2UserId, displayName = "Driver 2" },
                    recommendedVehicle = new { vehicleId = seed.VehicleId, registrationNumber = "WP-CAB-1001", vehicleType = "Compactor" },
                    recommendedTasks = new[] { new { taskId = seed.Task2Id, taskCode = "TASK-002", sequence = 1, reason = "R2" } }
                }
            }
        });

        var c4Step = await db.AgentWorkflowSteps.FirstAsync(s => s.Id == seed.C4StepId);
        c4Step.InputJson = duplicateVehicleJson;
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var act = async () => await service.ExecuteDispatchPlanAsync(seed.WorkflowId, new ExecuteDispatchPlanRequest { ExpectedVersion = 3 }, seed.ManagerUserId);

        await act.Should().ThrowAsync<BusinessRuleConflictException>()
            .WithMessage("*assigned to multiple dispatch plans*");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Section 80: TEST — INVALID SEQUENCE
    // ──────────────────────────────────────────────────────────────────────────
    [Theory]
    [InlineData(0, 1)] // starts at 0
    [InlineData(1, 3)] // gap: 1, 3
    [InlineData(1, 1)] // duplicate: 1, 1
    public async Task ExecuteDispatchPlan_InvalidSequence_RejectsWith409BeforePhaseA(int seq1, int seq2)
    {
        var dbName = $"DispatchBridge_{Guid.NewGuid():N}";
        using var db = CreateContext(dbName);
        var seed = SeedStandardDispatchScenario(db);

        var invalidSeqJson = JsonSerializer.Serialize(new
        {
            dispatchPlans = new[]
            {
                new
                {
                    planId = "plan-1",
                    recommendedDriver = new { driverId = seed.DriverUserId, displayName = "Driver 1" },
                    recommendedVehicle = new { vehicleId = seed.VehicleId, registrationNumber = "WP-CAB-1001", vehicleType = "Compactor" },
                    recommendedTasks = new[]
                    {
                        new { taskId = seed.Task1Id, taskCode = "TASK-001", sequence = seq1, reason = "R1" },
                        new { taskId = seed.Task2Id, taskCode = "TASK-002", sequence = seq2, reason = "R2" }
                    }
                }
            }
        });

        var c4Step = await db.AgentWorkflowSteps.FirstAsync(s => s.Id == seed.C4StepId);
        c4Step.InputJson = invalidSeqJson;
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var act = async () => await service.ExecuteDispatchPlanAsync(seed.WorkflowId, new ExecuteDispatchPlanRequest { ExpectedVersion = 3 }, seed.ManagerUserId);

        await act.Should().ThrowAsync<BusinessRuleConflictException>();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Section 81: TEST — C4 NOT READY
    // ──────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ExecuteDispatchPlan_C4OutcomeNeedsRevision_RejectsWith409BeforePhaseA()
    {
        var dbName = $"DispatchBridge_{Guid.NewGuid():N}";
        using var db = CreateContext(dbName);
        var seed = SeedStandardDispatchScenario(db, customC4OutputJson: JsonSerializer.Serialize(new
        {
            validationOutcome = "NeedsRevision",
            requiresAcknowledgement = false
        }));

        var service = CreateService(db);
        var act = async () => await service.ExecuteDispatchPlanAsync(seed.WorkflowId, new ExecuteDispatchPlanRequest { ExpectedVersion = 3 }, seed.ManagerUserId);

        await act.Should().ThrowAsync<BusinessRuleConflictException>()
            .WithMessage("*cannot be executed without revision*");
    }

    [Fact]
    public async Task ExecuteDispatchPlan_C4OutcomeRequiresRevision_RejectsWith409BeforePhaseA()
    {
        var dbName = $"DispatchBridge_{Guid.NewGuid():N}";
        using var db = CreateContext(dbName);
        var seed = SeedStandardDispatchScenario(db, customC4OutputJson: JsonSerializer.Serialize(new
        {
            validationOutcome = "RequiresRevision",
            requiresAcknowledgement = false
        }));

        var service = CreateService(db);
        var act = async () => await service.ExecuteDispatchPlanAsync(seed.WorkflowId, new ExecuteDispatchPlanRequest { ExpectedVersion = 3 }, seed.ManagerUserId);

        await act.Should().ThrowAsync<BusinessRuleConflictException>()
            .WithMessage("*validation outcome is 'RequiresRevision' and cannot be executed without revision*");
    }

    [Fact]
    public async Task ExecuteDispatchPlan_C4OutcomeRejected_RejectsWith409BeforePhaseA()
    {
        var dbName = $"DispatchBridge_{Guid.NewGuid():N}";
        using var db = CreateContext(dbName);
        var seed = SeedStandardDispatchScenario(db, customC4OutputJson: JsonSerializer.Serialize(new
        {
            validationOutcome = "Rejected",
            requiresAcknowledgement = false
        }));

        var service = CreateService(db);
        var act = async () => await service.ExecuteDispatchPlanAsync(seed.WorkflowId, new ExecuteDispatchPlanRequest { ExpectedVersion = 3 }, seed.ManagerUserId);

        await act.Should().ThrowAsync<BusinessRuleConflictException>()
            .WithMessage("*validation outcome is 'Rejected' and cannot be executed without revision*");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Section 82: TEST — REQUIRED ACKNOWLEDGEMENT
    // ──────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ExecuteDispatchPlan_RequiredWarningNotAcknowledged_RejectsWith409BeforePhaseA()
    {
        var dbName = $"DispatchBridge_{Guid.NewGuid():N}";
        using var db = CreateContext(dbName);
        var seed = SeedStandardDispatchScenario(db,
            customC4OutputJson: JsonSerializer.Serialize(new
            {
                validationOutcome = "ReadyForHumanReview",
                requiresAcknowledgement = true
            }),
            acknowledgeWarningsInApproval: false);

        var service = CreateService(db);
        var act = async () => await service.ExecuteDispatchPlanAsync(seed.WorkflowId, new ExecuteDispatchPlanRequest { ExpectedVersion = 3 }, seed.ManagerUserId);

        await act.Should().ThrowAsync<BusinessRuleConflictException>()
            .WithMessage("*requires warning acknowledgement*");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Section 83: TEST — FRESH COMPATIBLE
    // ──────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ExecuteDispatchPlan_FreshCompatible_ProceedsSuccessfully()
    {
        var dbName = $"DispatchBridge_{Guid.NewGuid():N}";
        using var db = CreateContext(dbName);
        var seed = SeedStandardDispatchScenario(db);
        var service = CreateService(db);

        var result = await service.ExecuteDispatchPlanAsync(seed.WorkflowId, new ExecuteDispatchPlanRequest { ExpectedVersion = 3 }, seed.ManagerUserId);

        result.Status.Should().Be(AgentWorkflowStatus.Completed);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Section 84: TEST — FRESH INCOMPATIBLE
    // ──────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ExecuteDispatchPlan_FreshIncompatible_RejectsWith409BeforePhaseA()
    {
        var dbName = $"DispatchBridge_{Guid.NewGuid():N}";
        using var db = CreateContext(dbName);
        var seed = SeedStandardDispatchScenario(db);

        // Make vehicle 1 support ONLY Recyclable waste
        var vehicle = await db.Vehicles.Include(v => v.SupportedWasteTypes).FirstAsync(v => v.Id == seed.VehicleId);
        vehicle.SupportedWasteTypes.Clear();
        vehicle.SupportedWasteTypes.Add(new VehicleSupportedWasteType { VehicleId = vehicle.Id, WasteType = WasteType.Recyclable });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var act = async () => await service.ExecuteDispatchPlanAsync(seed.WorkflowId, new ExecuteDispatchPlanRequest { ExpectedVersion = 3 }, seed.ManagerUserId);

        await act.Should().ThrowAsync<BusinessRuleConflictException>()
            .WithMessage("*is incompatible with the assigned collection tasks*");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Section 85 & 86: TEST — FRESH UNKNOWN WITHOUT ACK vs WITH ACK
    // ──────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ExecuteDispatchPlan_FreshUnknownWithoutAck_RejectsWith409BeforePhaseA()
    {
        var dbName = $"DispatchBridge_{Guid.NewGuid():N}";
        using var db = CreateContext(dbName);
        var seed = SeedStandardDispatchScenario(db, acknowledgeWarningsInApproval: false);

        // Add a target bin to task 1 that accepts mixed waste types (General + Hazardous)
        // Vehicle supports General, but not Hazardous -> triggers partial compatibility / Unknown status
        var bin = new WasteBin
        {
            Id = Guid.NewGuid(),
            BinCode = "BIN-MIXED-001",
            Latitude = 6.9,
            Longitude = 79.9,
            AdministrativeStatus = BinAdministrativeStatus.Active
        };
        bin.AcceptedWasteTypes.Add(new WasteBinAcceptedWasteType { WasteType = WasteType.General });
        bin.AcceptedWasteTypes.Add(new WasteBinAcceptedWasteType { WasteType = WasteType.Hazardous });
        db.WasteBins.Add(bin);

        var task = await db.CollectionTasks.FirstAsync(t => t.Id == seed.Task1Id);
        task.WasteReportId = null;
        task.WasteReport = null;
        task.WasteBinId = bin.Id;
        task.WasteBin = bin;
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var act = async () => await service.ExecuteDispatchPlanAsync(seed.WorkflowId, new ExecuteDispatchPlanRequest { ExpectedVersion = 3 }, seed.ManagerUserId);

        await act.Should().ThrowAsync<BusinessRuleConflictException>()
            .WithMessage("*Current fleet compatibility now requires acknowledgement that was not part of the approved dispatch decision*");
    }

    [Fact]
    public async Task ExecuteDispatchPlan_FreshUnknownWithAck_ProceedsSuccessfully()
    {
        var dbName = $"DispatchBridge_{Guid.NewGuid():N}";
        using var db = CreateContext(dbName);
        // Manager acknowledged warnings in approval
        var seed = SeedStandardDispatchScenario(db, acknowledgeWarningsInApproval: true);

        // Add a target bin to task 1 that accepts mixed waste types (General + Hazardous)
        var bin = new WasteBin
        {
            Id = Guid.NewGuid(),
            BinCode = "BIN-MIXED-001",
            Latitude = 6.9,
            Longitude = 79.9,
            AdministrativeStatus = BinAdministrativeStatus.Active
        };
        bin.AcceptedWasteTypes.Add(new WasteBinAcceptedWasteType { WasteType = WasteType.General });
        bin.AcceptedWasteTypes.Add(new WasteBinAcceptedWasteType { WasteType = WasteType.Hazardous });
        db.WasteBins.Add(bin);

        var task = await db.CollectionTasks.FirstAsync(t => t.Id == seed.Task1Id);
        task.WasteReportId = null;
        task.WasteReport = null;
        task.WasteBinId = bin.Id;
        task.WasteBin = bin;
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.ExecuteDispatchPlanAsync(seed.WorkflowId, new ExecuteDispatchPlanRequest { ExpectedVersion = 3 }, seed.ManagerUserId);

        result.Status.Should().Be(AgentWorkflowStatus.Completed);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Section 87: TEST — TASK NO LONGER SCHEDULED
    // ──────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ExecuteDispatchPlan_TaskNoLongerScheduled_RejectsWith409BeforePhaseA()
    {
        var dbName = $"DispatchBridge_{Guid.NewGuid():N}";
        using var db = CreateContext(dbName);
        var seed = SeedStandardDispatchScenario(db);

        // Change task 1 to InProgress
        var task = await db.CollectionTasks.FirstAsync(t => t.Id == seed.Task1Id);
        task.Status = CollectionTaskStatus.InProgress;
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var act = async () => await service.ExecuteDispatchPlanAsync(seed.WorkflowId, new ExecuteDispatchPlanRequest { ExpectedVersion = 3 }, seed.ManagerUserId);

        await act.Should().ThrowAsync<BusinessRuleConflictException>()
            .WithMessage("*current status is 'InProgress', but 'Scheduled' is required*");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Section 88: TEST — TASK ALREADY CLAIMED
    // ──────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ExecuteDispatchPlan_TaskAlreadyClaimed_RejectsWith409BeforePhaseA()
    {
        var dbName = $"DispatchBridge_{Guid.NewGuid():N}";
        using var db = CreateContext(dbName);
        var seed = SeedStandardDispatchScenario(db);

        // Add active claim for task 1
        db.CollectionAssignmentTaskClaims.Add(new CollectionAssignmentTaskClaim
        {
            Id = Guid.NewGuid(),
            CollectionTaskId = seed.Task1Id,
            CollectionAssignmentId = Guid.NewGuid(),
            ClaimedAt = DateTime.UtcNow,
            IsActive = true
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var act = async () => await service.ExecuteDispatchPlanAsync(seed.WorkflowId, new ExecuteDispatchPlanRequest { ExpectedVersion = 3 }, seed.ManagerUserId);

        await act.Should().ThrowAsync<BusinessRuleConflictException>()
            .WithMessage("*already assigned to an active assignment*");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Section 89: TEST — DRIVER OFF DUTY / UNAVAILABLE
    // ──────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ExecuteDispatchPlan_DriverOffDuty_RejectsWith409BeforePhaseA()
    {
        var dbName = $"DispatchBridge_{Guid.NewGuid():N}";
        using var db = CreateContext(dbName);
        var seed = SeedStandardDispatchScenario(db);

        var profile = await db.DriverProfiles.FirstAsync(d => d.UserId == seed.DriverUserId);
        profile.AvailabilityStatus = DriverAvailabilityStatus.OffDuty;
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var act = async () => await service.ExecuteDispatchPlanAsync(seed.WorkflowId, new ExecuteDispatchPlanRequest { ExpectedVersion = 3 }, seed.ManagerUserId);

        await act.Should().ThrowAsync<BusinessRuleConflictException>()
            .WithMessage("*is not available (status: 'OffDuty')*");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Section 90: TEST — DRIVER OCCUPIED
    // ──────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ExecuteDispatchPlan_DriverOccupied_RejectsWith409BeforePhaseA()
    {
        var dbName = $"DispatchBridge_{Guid.NewGuid():N}";
        using var db = CreateContext(dbName);
        var seed = SeedStandardDispatchScenario(db);

        db.CollectionAssignments.Add(new CollectionAssignment
        {
            Id = Guid.NewGuid(),
            DriverId = seed.DriverUserId,
            VehicleId = seed.Vehicle2Id,
            Status = CollectionAssignmentStatus.Assigned,
            AssignedAt = DateTime.UtcNow,
            AssignedByUserId = seed.ManagerUserId
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var act = async () => await service.ExecuteDispatchPlanAsync(seed.WorkflowId, new ExecuteDispatchPlanRequest { ExpectedVersion = 3 }, seed.ManagerUserId);

        await act.Should().ThrowAsync<BusinessRuleConflictException>()
            .WithMessage("*already has an active collection assignment*");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Section 91: TEST — VEHICLE UNAVAILABLE
    // ──────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ExecuteDispatchPlan_VehicleUnavailable_RejectsWith409BeforePhaseA()
    {
        var dbName = $"DispatchBridge_{Guid.NewGuid():N}";
        using var db = CreateContext(dbName);
        var seed = SeedStandardDispatchScenario(db);

        var vehicle = await db.Vehicles.FirstAsync(v => v.Id == seed.VehicleId);
        vehicle.OperationalStatus = VehicleOperationalStatus.Maintenance;
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var act = async () => await service.ExecuteDispatchPlanAsync(seed.WorkflowId, new ExecuteDispatchPlanRequest { ExpectedVersion = 3 }, seed.ManagerUserId);

        await act.Should().ThrowAsync<BusinessRuleConflictException>()
            .WithMessage("*is not available (status: 'Maintenance')*");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Section 92: TEST — VEHICLE OCCUPIED
    // ──────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ExecuteDispatchPlan_VehicleOccupied_RejectsWith409BeforePhaseA()
    {
        var dbName = $"DispatchBridge_{Guid.NewGuid():N}";
        using var db = CreateContext(dbName);
        var seed = SeedStandardDispatchScenario(db);

        db.CollectionAssignments.Add(new CollectionAssignment
        {
            Id = Guid.NewGuid(),
            DriverId = seed.Driver2UserId,
            VehicleId = seed.VehicleId,
            Status = CollectionAssignmentStatus.InProgress,
            AssignedAt = DateTime.UtcNow,
            AssignedByUserId = seed.ManagerUserId
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var act = async () => await service.ExecuteDispatchPlanAsync(seed.WorkflowId, new ExecuteDispatchPlanRequest { ExpectedVersion = 3 }, seed.ManagerUserId);

        await act.Should().ThrowAsync<BusinessRuleConflictException>()
            .WithMessage("*already has an active collection assignment*");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Section 93: TEST — APPROVAL BINDING
    // ──────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ExecuteDispatchPlan_ApprovalBindingToSpecificStep_ResolvesCorrectStep()
    {
        var dbName = $"DispatchBridge_{Guid.NewGuid():N}";
        using var db = CreateContext(dbName);
        var seed = SeedStandardDispatchScenario(db);

        // Add a newer C4 step that is NOT linked to the approval
        var newerStepId = Guid.NewGuid();
        db.AgentWorkflowSteps.Add(new AgentWorkflowStep
        {
            Id = newerStepId,
            WorkflowId = seed.WorkflowId,
            Sequence = 5,
            StepType = WorkflowStepType.OperationalValidation,
            AgentName = "c4_validation_operations_agent",
            Status = WorkflowStepStatus.Failed, // This newer step failed!
            StartedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        // Approval is bound to seed.C4StepId (which was Completed and Approved)
        var service = CreateService(db);
        var result = await service.ExecuteDispatchPlanAsync(seed.WorkflowId, new ExecuteDispatchPlanRequest { ExpectedVersion = 3 }, seed.ManagerUserId);

        result.Status.Should().Be(AgentWorkflowStatus.Completed);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Section 94: TEST — C4 -> C3 SOURCE BINDING
    // ──────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ExecuteDispatchPlan_SourceBinding_ReadsDirectlyFromC4InputJson()
    {
        var dbName = $"DispatchBridge_{Guid.NewGuid():N}";
        using var db = CreateContext(dbName);
        var seed = SeedStandardDispatchScenario(db);

        // Ensure the execution output records sourceOperationalValidationStepId
        var service = CreateService(db);
        await service.ExecuteDispatchPlanAsync(seed.WorkflowId, new ExecuteDispatchPlanRequest { ExpectedVersion = 3 }, seed.ManagerUserId);

        var execStep = await db.AgentWorkflowSteps
            .FirstAsync(s => s.WorkflowId == seed.WorkflowId && s.StepType == WorkflowStepType.AssignmentExecution);

        execStep.OutputJson.Should().Contain(seed.C4StepId.ToString());
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Section 95: TEST — MISSING SOURCE BINDING
    // ──────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ExecuteDispatchPlan_MissingSourceBinding_RejectsWith409BeforePhaseA()
    {
        var dbName = $"DispatchBridge_{Guid.NewGuid():N}";
        using var db = CreateContext(dbName);
        var seed = SeedStandardDispatchScenario(db, omitApprovalStepId: true);

        var service = CreateService(db);
        var act = async () => await service.ExecuteDispatchPlanAsync(seed.WorkflowId, new ExecuteDispatchPlanRequest { ExpectedVersion = 3 }, seed.ManagerUserId);

        await act.Should().ThrowAsync<BusinessRuleConflictException>()
            .WithMessage("*not bound to a specific operational validation workflow step*");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Section 96: TEST — MALFORMED C3 JSON
    // ──────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ExecuteDispatchPlan_MalformedC3Json_RejectsWith409BeforePhaseA()
    {
        var dbName = $"DispatchBridge_{Guid.NewGuid():N}";
        using var db = CreateContext(dbName);
        var seed = SeedStandardDispatchScenario(db, customC3Json: "{ invalid json");

        var service = CreateService(db);
        var act = async () => await service.ExecuteDispatchPlanAsync(seed.WorkflowId, new ExecuteDispatchPlanRequest { ExpectedVersion = 3 }, seed.ManagerUserId);

        await act.Should().ThrowAsync<BusinessRuleConflictException>()
            .WithMessage("*input snapshot is malformed JSON*");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Section 97: TEST — MALFORMED C4 JSON
    // ──────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ExecuteDispatchPlan_MalformedC4Json_RejectsWith409BeforePhaseA()
    {
        var dbName = $"DispatchBridge_{Guid.NewGuid():N}";
        using var db = CreateContext(dbName);
        var seed = SeedStandardDispatchScenario(db, customC4OutputJson: "{ invalid json");

        var service = CreateService(db);
        var act = async () => await service.ExecuteDispatchPlanAsync(seed.WorkflowId, new ExecuteDispatchPlanRequest { ExpectedVersion = 3 }, seed.ManagerUserId);

        await act.Should().ThrowAsync<BusinessRuleConflictException>()
            .WithMessage("*output payload is malformed*");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Section 98: TEST — WRONG WORKFLOW STATE
    // ──────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ExecuteDispatchPlan_WrongWorkflowState_RejectsWith409Conflict()
    {
        var dbName = $"DispatchBridge_{Guid.NewGuid():N}";
        using var db = CreateContext(dbName);
        var seed = SeedStandardDispatchScenario(db, status: AgentWorkflowStatus.AwaitingDispatchApproval);

        var service = CreateService(db);
        var act = async () => await service.ExecuteDispatchPlanAsync(seed.WorkflowId, new ExecuteDispatchPlanRequest { ExpectedVersion = 3 }, seed.ManagerUserId);

        await act.Should().ThrowAsync<BusinessRuleConflictException>()
            .WithMessage("*Execution requires 'DispatchApproved'*");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Section 99: TEST — STALE EXPECTED VERSION
    // ──────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ExecuteDispatchPlan_StaleExpectedVersion_RejectsWith409Conflict()
    {
        var dbName = $"DispatchBridge_{Guid.NewGuid():N}";
        using var db = CreateContext(dbName);
        var seed = SeedStandardDispatchScenario(db, version: 5);

        var service = CreateService(db);
        var act = async () => await service.ExecuteDispatchPlanAsync(seed.WorkflowId, new ExecuteDispatchPlanRequest { ExpectedVersion = 4 }, seed.ManagerUserId);

        await act.Should().ThrowAsync<BusinessRuleConflictException>()
            .WithMessage("*Reload the workflow*");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Section 100: TEST — SUCCESSFUL IDEMPOTENT RETRY
    // ──────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ExecuteDispatchPlan_SuccessfulIdempotentRetry_ReturnsExistingWithoutDuplication()
    {
        var dbName = $"DispatchBridge_{Guid.NewGuid():N}";
        using var db = CreateContext(dbName);
        var seed = SeedStandardDispatchScenario(db);
        var service = CreateService(db);

        // First execution succeeds -> version becomes 5
        var firstResult = await service.ExecuteDispatchPlanAsync(seed.WorkflowId, new ExecuteDispatchPlanRequest { ExpectedVersion = 3 }, seed.ManagerUserId);
        firstResult.Status.Should().Be(AgentWorkflowStatus.Completed);
        firstResult.Version.Should().Be(5);

        // Second execution with version 5 should be idempotent
        var secondResult = await service.ExecuteDispatchPlanAsync(seed.WorkflowId, new ExecuteDispatchPlanRequest { ExpectedVersion = 5 }, seed.ManagerUserId);
        secondResult.Status.Should().Be(AgentWorkflowStatus.Completed);
        secondResult.Version.Should().Be(5); // unchanged!

        // Confirm assignments not duplicated
        var assignments = await db.CollectionAssignments.ToListAsync();
        assignments.Should().HaveCount(1);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Section 101: TEST — PENDING RESUME
    // ──────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ExecuteDispatchPlan_PendingResume_ResumesInFlightExecutionToCompleted()
    {
        var dbName = $"DispatchBridge_{Guid.NewGuid():N}";
        using var db = CreateContext(dbName);
        var seed = SeedStandardDispatchScenario(db, status: AgentWorkflowStatus.ExecutingAssignments, version: 4);

        // Add in-flight step and execution result
        var stepId = Guid.NewGuid();
        db.AgentWorkflowSteps.Add(new AgentWorkflowStep
        {
            Id = stepId,
            WorkflowId = seed.WorkflowId,
            Sequence = 5,
            StepType = WorkflowStepType.AssignmentExecution,
            Status = WorkflowStepStatus.Running,
            StartedAt = DateTime.UtcNow
        });
        db.AgentWorkflowExecutionResults.Add(new AgentWorkflowExecutionResult
        {
            Id = Guid.NewGuid(),
            WorkflowId = seed.WorkflowId,
            WorkflowStepId = stepId,
            ExecutionType = WorkflowExecutionType.CollectionAssignment,
            Status = WorkflowExecutionStatus.Pending,
            ExecutedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.ExecuteDispatchPlanAsync(seed.WorkflowId, new ExecuteDispatchPlanRequest { ExpectedVersion = 4 }, seed.ManagerUserId);

        result.Status.Should().Be(AgentWorkflowStatus.Completed);
        result.Version.Should().Be(5);

        var assignment = await db.CollectionAssignments.ToListAsync();
        assignment.Should().HaveCount(1);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Section 102: TEST — TRANSACTION ROLLBACK
    // ──────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ExecuteDispatchPlan_PhaseBFailure_RollsBackAndRecordsFailedState()
    {
        var dbName = $"DispatchBridge_{Guid.NewGuid():N}";
        using var db = CreateContext(dbName);
        var seed = SeedStandardDispatchScenario(db);

        // Mock assignment service throwing an unhandled exception during creation
        var mockAssignmentSvc = new Mock<ICollectionAssignmentService>();
        mockAssignmentSvc.Setup(s => s.CreateAssignmentFromApprovedPlanAsync(
                It.IsAny<CreateCollectionAssignmentRequest>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Simulated database failure during assignment creation."));

        var service = CreateService(db, mockAssignmentSvc.Object);
        var act = async () => await service.ExecuteDispatchPlanAsync(seed.WorkflowId, new ExecuteDispatchPlanRequest { ExpectedVersion = 3 }, seed.ManagerUserId);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Simulated database failure*");

        // Verify workflow transitioned to Failed
        var wf = await db.AgentWorkflows.FirstAsync(w => w.Id == seed.WorkflowId);
        wf.Status.Should().Be(AgentWorkflowStatus.Failed);
        wf.CurrentStep.Should().Be(WorkflowStepType.AssignmentExecution);

        // Verify Step status is Failed
        var step = await db.AgentWorkflowSteps
            .FirstAsync(s => s.WorkflowId == seed.WorkflowId && s.StepType == WorkflowStepType.AssignmentExecution);
        step.Status.Should().Be(WorkflowStepStatus.Failed);
        step.ErrorMessage.Should().Contain("Simulated database failure");

        // Verify ExecutionResult is Failed
        var execResult = await db.AgentWorkflowExecutionResults
            .FirstAsync(e => e.WorkflowId == seed.WorkflowId && e.ExecutionType == WorkflowExecutionType.CollectionAssignment);
        execResult.Status.Should().Be(WorkflowExecutionStatus.Failed);
        execResult.ErrorMessage.Should().Contain("Simulated database failure");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Section 34: ZERO DISPATCH PLANS SEMANTICS
    // ──────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ExecuteDispatchPlan_ZeroDispatchPlans_CompletesDeterministically()
    {
        var dbName = $"DispatchBridge_{Guid.NewGuid():N}";
        using var db = CreateContext(dbName);

        var zeroPlanJson = JsonSerializer.Serialize(new
        {
            dispatchPlans = Array.Empty<object>(),
            unplannedTasks = new[]
            {
                new { taskId = Guid.NewGuid(), taskCode = "UNP-1", reason = "No available vehicles." }
            }
        });

        var seed = SeedStandardDispatchScenario(db, customC3Json: zeroPlanJson);
        var service = CreateService(db);

        var result = await service.ExecuteDispatchPlanAsync(seed.WorkflowId, new ExecuteDispatchPlanRequest { ExpectedVersion = 3 }, seed.ManagerUserId);

        result.Status.Should().Be(AgentWorkflowStatus.Completed);
        result.FinalOutcome.Should().Contain("Created 0 collection assignments covering 0 planned tasks. 1 tasks remained unplanned");

        var assignments = await db.CollectionAssignments.ToListAsync();
        assignments.Should().BeEmpty();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // STEP 8B AUDIT & FIX TESTS
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteDispatchPlan_LostResponseIdempotentRetry_OriginalPreExecutionVersion_ReturnsExistingWithoutDuplication()
    {
        var dbName = $"DispatchBridge_{Guid.NewGuid():N}";
        using var db = CreateContext(dbName);
        var seed = SeedStandardDispatchScenario(db);
        var service = CreateService(db);

        // First execution succeeds: moves from version 3 -> 5
        var firstResult = await service.ExecuteDispatchPlanAsync(
            seed.WorkflowId,
            new ExecuteDispatchPlanRequest { ExpectedVersion = 3 },
            seed.ManagerUserId);
        firstResult.Status.Should().Be(AgentWorkflowStatus.Completed);
        firstResult.Version.Should().Be(5);

        // Simulated lost response: client retries with the ORIGINAL pre-execution expectedVersion = 3
        var retryResult = await service.ExecuteDispatchPlanAsync(
            seed.WorkflowId,
            new ExecuteDispatchPlanRequest { ExpectedVersion = 3 },
            seed.ManagerUserId);

        retryResult.Status.Should().Be(AgentWorkflowStatus.Completed);
        retryResult.Version.Should().Be(5);
        retryResult.Id.Should().Be(seed.WorkflowId);

        // Verify no duplicate assignments
        var assignments = await db.CollectionAssignments.ToListAsync();
        assignments.Should().HaveCount(1);
    }

    [Fact]
    public async Task ExecuteDispatchPlan_CompletedWithoutSucceededExecution_ThrowsConflict()
    {
        var dbName = $"DispatchBridge_{Guid.NewGuid():N}";
        using var db = CreateContext(dbName);
        var seed = SeedStandardDispatchScenario(db, status: AgentWorkflowStatus.Completed, version: 5);
        var service = CreateService(db);

        var act = async () => await service.ExecuteDispatchPlanAsync(
            seed.WorkflowId,
            new ExecuteDispatchPlanRequest { ExpectedVersion = 5 },
            seed.ManagerUserId);

        await act.Should().ThrowAsync<BusinessRuleConflictException>()
            .WithMessage("*Cannot execute dispatch plan for workflow*in status 'Completed'*");
    }

    [Fact]
    public async Task ExecuteDispatchPlan_StaleVersion_UncompletedWorkflow_ThrowsConflict()
    {
        var dbName = $"DispatchBridge_{Guid.NewGuid():N}";
        using var db = CreateContext(dbName);
        var seed = SeedStandardDispatchScenario(db, version: 3);
        var service = CreateService(db);

        var act = async () => await service.ExecuteDispatchPlanAsync(
            seed.WorkflowId,
            new ExecuteDispatchPlanRequest { ExpectedVersion = 2 },
            seed.ManagerUserId);

        await act.Should().ThrowAsync<BusinessRuleConflictException>()
            .WithMessage("*workflow has changed since it was loaded*");
    }

    [Fact]
    public async Task ExecuteDispatchPlan_CanonicalContractFixture_MatchesPythonTestAgentContractsShape_Succeeds()
    {
        var dbName = $"DispatchBridge_{Guid.NewGuid():N}";
        using var db = CreateContext(dbName);

        var managerId = Guid.NewGuid();
        var driverId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var unplannedId = Guid.NewGuid();

        db.Users.Add(new AppUser { Id = managerId, UserName = "mgr@sw.lk", Email = "mgr@sw.lk", FullName = "Mgr", CreatedAt = DateTime.UtcNow, IsActive = true });
        var dUser = new AppUser { Id = driverId, UserName = "driver@sw.lk", Email = "driver@sw.lk", FullName = "John Driver", CreatedAt = DateTime.UtcNow, IsActive = true };
        db.Users.Add(dUser);
        db.DriverProfiles.Add(new DriverProfile { UserId = driverId, User = dUser, AvailabilityStatus = DriverAvailabilityStatus.Available, LicenseNumber = "DL-01", IsEligible = true });

        var veh = new Vehicle { Id = vehicleId, RegistrationNumber = "WP-CAB-1234", VehicleType = VehicleType.Compactor, CapacityLiters = 5000, OperationalStatus = VehicleOperationalStatus.Available };
        veh.SupportedWasteTypes.Add(new VehicleSupportedWasteType { VehicleId = vehicleId, WasteType = WasteType.General });
        db.Vehicles.Add(veh);

        var rpt = new WasteReport { Id = Guid.NewGuid(), CitizenId = Guid.NewGuid(), Description = "Test", AddressText = "Main St", Latitude = 6.9, Longitude = 79.8, Status = WasteReportStatus.UnderReview, WasteType = WasteType.General };
        db.WasteReports.Add(rpt);
        db.CollectionTasks.Add(new CollectionTask { Id = taskId, TaskCode = "TSK-001", Status = CollectionTaskStatus.Scheduled, WasteReportId = rpt.Id, ScheduledAt = DateTime.UtcNow.AddHours(2), CreatedAt = DateTime.UtcNow });

        var rptUnp = new WasteReport { Id = Guid.NewGuid(), CitizenId = Guid.NewGuid(), Description = "Unp", AddressText = "2nd St", Latitude = 6.9, Longitude = 79.8, Status = WasteReportStatus.UnderReview, WasteType = WasteType.General };
        db.WasteReports.Add(rptUnp);
        db.CollectionTasks.Add(new CollectionTask { Id = unplannedId, TaskCode = "TSK-002", Status = CollectionTaskStatus.Scheduled, WasteReportId = rptUnp.Id, ScheduledAt = DateTime.UtcNow.AddHours(2), CreatedAt = DateTime.UtcNow });

        // Canonical C3 fixture strictly emitting recommendedDriver and recommendedVehicle (modeled after test_agent_contracts.py)
        var c3FixtureJson = JsonSerializer.Serialize(new
        {
            objective = "Dispatch fleet",
            dispatchPlans = new[]
            {
                new
                {
                    planId = "plan-1",
                    recommendedDriver = new { driverId = driverId, displayName = "John Driver" },
                    recommendedVehicle = new { vehicleId = vehicleId, registrationNumber = "WP-CAB-1234", vehicleType = "Compactor" },
                    recommendedTasks = new[]
                    {
                        new { taskId = taskId, taskCode = "TSK-001", sequence = 1, addressText = "Main St", reason = "First stop" }
                    },
                    compatibility = new { status = "Compatible", requiresAcknowledgement = false, issues = Array.Empty<string>() },
                    rationale = "Valid candidate plan",
                    warnings = Array.Empty<string>()
                }
            },
            unplannedTasks = new[]
            {
                new { taskId = unplannedId, taskCode = "TSK-002", reason = "Deferred due to capacity limits." }
            },
            warnings = Array.Empty<string>(),
            rationale = "Plan formulated for morning operations.",
            status = "completed"
        });

        // Canonical C4 fixture strictly emitting validationOutcome in camelCase
        var c4FixtureJson = JsonSerializer.Serialize(new
        {
            objective = "Validate dispatch proposal",
            validationOutcome = "ReadyForHumanReview",
            requiresAcknowledgement = false,
            warnings = Array.Empty<string>(),
            status = "completed"
        });

        var wfId = Guid.NewGuid();
        var c4StepId = Guid.NewGuid();
        var wf = new AgentWorkflow { Id = wfId, Objective = "Fixture test", Status = AgentWorkflowStatus.DispatchApproved, CurrentStep = WorkflowStepType.OperationalValidation, InitiatedByUserId = managerId, Version = 3, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var c4Step = new AgentWorkflowStep { Id = c4StepId, WorkflowId = wfId, Sequence = 4, StepType = WorkflowStepType.OperationalValidation, Status = WorkflowStepStatus.Completed, InputJson = c3FixtureJson, OutputJson = c4FixtureJson, StartedAt = DateTime.UtcNow.AddMinutes(-5), CompletedAt = DateTime.UtcNow.AddMinutes(-1) };
        var apprv = new AgentWorkflowApproval { Id = Guid.NewGuid(), WorkflowId = wfId, WorkflowStepId = c4StepId, ApprovalStage = WorkflowApprovalStage.FleetDispatch, Decision = WorkflowApprovalDecision.Approved, DecisionReason = "Approved.", DecidedByUserId = managerId, DecidedAt = DateTime.UtcNow.AddMinutes(-1) };

        wf.Steps.Add(c4Step);
        wf.Approvals.Add(apprv);
        db.AgentWorkflows.Add(wf);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.ExecuteDispatchPlanAsync(wfId, new ExecuteDispatchPlanRequest { ExpectedVersion = 3 }, managerId);

        result.Status.Should().Be(AgentWorkflowStatus.Completed);
        result.Version.Should().Be(5);

        var assignment = await db.CollectionAssignments.Include(a => a.Route).ThenInclude(r => r!.Stops).FirstAsync();
        assignment.DriverId.Should().Be(driverId);
        assignment.VehicleId.Should().Be(vehicleId);
        assignment.Route.Should().NotBeNull();
        assignment.Route!.Stops.Should().ContainSingle();
        assignment.Route!.Stops.First().CollectionTaskId.Should().Be(taskId);
    }

    [Fact]
    public async Task ExecuteDispatchPlan_NoncanonicalC3Alias_DriverInsteadOfRecommendedDriver_ThrowsConflict()
    {
        var dbName = $"DispatchBridge_{Guid.NewGuid():N}";
        using var db = CreateContext(dbName);

        var noncanonicalC3Json = JsonSerializer.Serialize(new
        {
            dispatchPlans = new[]
            {
                new
                {
                    planId = "plan-1",
                    driver = new { driverId = Guid.NewGuid(), displayName = "John Driver" },
                    recommendedVehicle = new { vehicleId = Guid.NewGuid(), registrationNumber = "WP-CAB-1001", vehicleType = "Compactor" },
                    recommendedTasks = new[]
                    {
                        new { taskId = Guid.NewGuid(), taskCode = "TASK-001", sequence = 1 }
                    }
                }
            }
        });

        var seed = SeedStandardDispatchScenario(db, customC3Json: noncanonicalC3Json);
        var service = CreateService(db);

        var act = async () => await service.ExecuteDispatchPlanAsync(seed.WorkflowId, new ExecuteDispatchPlanRequest { ExpectedVersion = 3 }, seed.ManagerUserId);

        await act.Should().ThrowAsync<BusinessRuleConflictException>()
            .WithMessage("*missing a valid recommended driver*");
    }

    [Fact]
    public async Task ExecuteDispatchPlan_NoncanonicalC3Alias_VehicleInsteadOfRecommendedVehicle_ThrowsConflict()
    {
        var dbName = $"DispatchBridge_{Guid.NewGuid():N}";
        using var db = CreateContext(dbName);

        var noncanonicalC3Json = JsonSerializer.Serialize(new
        {
            dispatchPlans = new[]
            {
                new
                {
                    planId = "plan-1",
                    recommendedDriver = new { driverId = Guid.NewGuid(), displayName = "John Driver" },
                    vehicle = new { vehicleId = Guid.NewGuid(), registrationNumber = "WP-CAB-1001", vehicleType = "Compactor" },
                    recommendedTasks = new[]
                    {
                        new { taskId = Guid.NewGuid(), taskCode = "TASK-001", sequence = 1 }
                    }
                }
            }
        });

        var seed = SeedStandardDispatchScenario(db, customC3Json: noncanonicalC3Json);
        var service = CreateService(db);

        var act = async () => await service.ExecuteDispatchPlanAsync(seed.WorkflowId, new ExecuteDispatchPlanRequest { ExpectedVersion = 3 }, seed.ManagerUserId);

        await act.Should().ThrowAsync<BusinessRuleConflictException>()
            .WithMessage("*missing a valid recommended vehicle*");
    }

    [Fact]
    public async Task ExecuteDispatchPlan_NoncanonicalC4Alias_ValidationOutcomeSnakeCase_ThrowsConflict()
    {
        var dbName = $"DispatchBridge_{Guid.NewGuid():N}";
        using var db = CreateContext(dbName);

        var noncanonicalC4Json = JsonSerializer.Serialize(new
        {
            validation_outcome = "ReadyForHumanReview",
            requires_acknowledgement = false
        });

        var seed = SeedStandardDispatchScenario(db, customC4OutputJson: noncanonicalC4Json);
        var service = CreateService(db);

        var act = async () => await service.ExecuteDispatchPlanAsync(seed.WorkflowId, new ExecuteDispatchPlanRequest { ExpectedVersion = 3 }, seed.ManagerUserId);

        await act.Should().ThrowAsync<BusinessRuleConflictException>()
            .WithMessage("*validation outcome is 'missing' and cannot be executed*");
    }

    [Fact]
    public async Task ExecuteDispatchPlan_MultiPlanAtomicRollback_Plan2Fails_AllRollBackAndWorkflowFails()
    {
        var dbName = $"DispatchBridge_{Guid.NewGuid():N}";
        using var db = CreateContext(dbName);

        var seed = SeedStandardDispatchScenario(db);

        var concreteC3 = JsonSerializer.Serialize(new
        {
            dispatchPlans = new[]
            {
                new
                {
                    planId = "plan-1",
                    recommendedDriver = new { driverId = seed.DriverUserId, displayName = "Driver 1" },
                    recommendedVehicle = new { vehicleId = seed.VehicleId, registrationNumber = "WP-CAB-1001", vehicleType = "Compactor" },
                    recommendedTasks = new[]
                    {
                        new { taskId = seed.Task1Id, taskCode = "TASK-001", sequence = 1, addressText = "Loc 1", reason = "P1" }
                    }
                },
                new
                {
                    planId = "plan-2",
                    recommendedDriver = new { driverId = seed.Driver2UserId, displayName = "Driver 2" },
                    recommendedVehicle = new { vehicleId = seed.Vehicle2Id, registrationNumber = "WP-CAB-2002", vehicleType = "Flatbed" },
                    recommendedTasks = new[]
                    {
                        new { taskId = seed.Task2Id, taskCode = "TASK-002", sequence = 1, addressText = "Loc 2", reason = "P2" }
                    }
                }
            },
            unplannedTasks = Array.Empty<object>()
        });

        var step = await db.AgentWorkflowSteps.FirstAsync(s => s.Id == seed.C4StepId);
        step.InputJson = concreteC3;
        await db.SaveChangesAsync();

        var callCount = 0;
        var mockAssignmentSvc = new Mock<ICollectionAssignmentService>();
        mockAssignmentSvc.Setup(s => s.CreateAssignmentFromApprovedPlanAsync(
                It.IsAny<CreateCollectionAssignmentRequest>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .Returns<CreateCollectionAssignmentRequest, Guid, CancellationToken>((req, mgr, ct) =>
            {
                callCount++;
                if (callCount == 1)
                {
                    return Task.FromResult(new AssignmentDetailDto
                    {
                        Id = Guid.NewGuid(),
                        DriverId = req.DriverId,
                        VehicleId = req.VehicleId,
                        Status = CollectionAssignmentStatus.Assigned,
                        AssignedAt = DateTime.UtcNow
                    });
                }
                throw new InvalidOperationException("Simulated transient failure on second plan.");
            });

        var service = CreateService(db, mockAssignmentSvc.Object);
        var act = async () => await service.ExecuteDispatchPlanAsync(
            seed.WorkflowId,
            new ExecuteDispatchPlanRequest { ExpectedVersion = 3 },
            seed.ManagerUserId);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Simulated transient failure on second plan*");

        var wf = await db.AgentWorkflows.FirstAsync(w => w.Id == seed.WorkflowId);
        wf.Status.Should().Be(AgentWorkflowStatus.Failed);
        wf.CurrentStep.Should().Be(WorkflowStepType.AssignmentExecution);

        var execStep = await db.AgentWorkflowSteps
            .FirstAsync(s => s.WorkflowId == seed.WorkflowId && s.StepType == WorkflowStepType.AssignmentExecution);
        execStep.Status.Should().Be(WorkflowStepStatus.Failed);
        execStep.ErrorMessage.Should().Contain("Simulated transient failure on second plan");

        var execResult = await db.AgentWorkflowExecutionResults
            .FirstAsync(e => e.WorkflowId == seed.WorkflowId && e.ExecutionType == WorkflowExecutionType.CollectionAssignment);
        execResult.Status.Should().Be(WorkflowExecutionStatus.Failed);
        execResult.ErrorMessage.Should().Contain("Simulated transient failure on second plan");
    }
}
