using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
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

public class AgentWorkflowExecutionBridgeTests
{
    private static DateTime NextValidUtc(int localHour = 9)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Colombo");
        var localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone)).AddDays(2);
        return TimeZoneInfo.ConvertTimeToUtc(localDate.ToDateTime(new TimeOnly(localHour, 0)), zone);
    }

    private static AppDbContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new AppDbContext(options);
    }

    private static (AgentWorkflowService Service, Mock<ICollectionTaskService> TaskServiceMock) CreateServiceWithMockTaskService(AppDbContext db)
    {
        var stateMachine = new AgentWorkflowStateMachine();
        var logger = NullLogger<AgentWorkflowService>.Instance;
        var taskServiceMock = new Mock<ICollectionTaskService>();
        var service = new AgentWorkflowService(db, stateMachine, logger, taskServiceMock.Object);
        return (service, taskServiceMock);
    }

    private static AgentWorkflowService CreateServiceWithRealTaskService(AppDbContext db, TimeProvider? clock = null)
    {
        var stateMachine = new AgentWorkflowStateMachine();
        var logger = NullLogger<AgentWorkflowService>.Instance;
        var taskService = new CollectionTaskService(db);
        return new AgentWorkflowService(db, stateMachine, logger, taskService, timeProvider: clock);
    }

    private static (Guid workflowId, Guid stepId, Guid managerId, Guid reportId, Guid binId) SeedWorkflowWithApprovedC2Plan(
        AppDbContext db,
        string c2OutputJson,
        AgentWorkflowStatus status = AgentWorkflowStatus.CollectionApproved,
        int version = 3,
        Guid? explicitReportId = null,
        Guid? explicitBinId = null,
        AgentWorkflowTriggerType trigger = AgentWorkflowTriggerType.ManualOperationalPlanning)
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

        // Seed authoritative WasteReport
        var reportId = explicitReportId ?? Guid.NewGuid();
        db.WasteReports.Add(new WasteReport
        {
            Id = reportId,
            CitizenId = Guid.NewGuid(),
            Description = "Overflowing waste near market",
            AddressText = "Market Road, Colombo",
            Latitude = 6.9271,
            Longitude = 79.8612,
            Status = WasteReportStatus.Verified,
            WasteType = WasteType.General,
            CreatedAt = DateTime.UtcNow.AddHours(-10),
            VerifiedAt = DateTime.UtcNow.AddHours(-2)
        });

        // Seed authoritative WasteBin
        var binId = explicitBinId ?? Guid.NewGuid();
        var bin = new WasteBin
        {
            Id = binId,
            BinCode = "BIN-EXEC-001",
            Latitude = 6.9319,
            Longitude = 79.8478,
            CapacityLiters = 660,
            AdministrativeStatus = BinAdministrativeStatus.Active,
            CollectionWeekdays = new[] { 1, 2, 3, 4, 5 },
            CreatedAt = DateTime.UtcNow.AddDays(-20)
        };
        bin.AcceptedWasteTypes.Add(new WasteBinAcceptedWasteType
        {
            WasteBinId = bin.Id,
            WasteType = WasteType.General
        });
        db.WasteBins.Add(bin);

        // Seed recent fresh full observation for bin
        db.BinObservations.Add(new BinObservation
        {
            Id = Guid.NewGuid(),
            WasteBinId = binId,
            RecordedAt = DateTime.UtcNow.AddHours(-1),
            FillLevelPercent = 100,
            Condition = BinCondition.Good,
            RecordedByUserId = managerId
        });

        var workflow = new AgentWorkflow
        {
            Id = Guid.NewGuid(),
            Objective = "Authoritative collection task execution test",
            Status = status,
            TriggerType = trigger,
            TriggeringWasteReportId = trigger == AgentWorkflowTriggerType.CitizenReportSubmission ? reportId : null,
            CurrentStep = WorkflowStepType.CollectionPlanning,
            InitiatedByUserId = managerId,
            Version = version,
            CreatedAt = DateTime.UtcNow.AddHours(-5)
        };
        db.AgentWorkflows.Add(workflow);

        var c2Step = new AgentWorkflowStep
        {
            Id = Guid.NewGuid(),
            WorkflowId = workflow.Id,
            Sequence = 1,
            StepType = WorkflowStepType.CollectionPlanning,
            AgentName = "collection_planning_agent",
            Status = WorkflowStepStatus.Completed,
            OutputJson = c2OutputJson,
            StartedAt = DateTime.UtcNow.AddHours(-4),
            CompletedAt = DateTime.UtcNow.AddHours(-3)
        };
        db.AgentWorkflowSteps.Add(c2Step);

        // Persist human approval linking c2Step
        db.AgentWorkflowApprovals.Add(new AgentWorkflowApproval
        {
            Id = Guid.NewGuid(),
            WorkflowId = workflow.Id,
            WorkflowStepId = c2Step.Id,
            ApprovalStage = WorkflowApprovalStage.CollectionPlanning,
            Decision = WorkflowApprovalDecision.Approved,
            DecisionReason = "Approved for execution",
            DecidedByUserId = managerId,
            DecidedAt = DateTime.UtcNow.AddHours(-2)
        });

        db.SaveChanges();
        return (workflow.Id, c2Step.Id, managerId, reportId, binId);
    }

    private sealed class FixedTimeProvider(DateTimeOffset instant) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => instant;
    }

    [Theory]
    [InlineData(AgentWorkflowTriggerType.ManualOperationalPlanning, false)]
    [InlineData(AgentWorkflowTriggerType.CitizenReportSubmission, false)]
    [InlineData(AgentWorkflowTriggerType.ManualOperationalPlanning, true)]
    [InlineData(AgentWorkflowTriggerType.CitizenReportSubmission, true)]
    public async Task ExecuteCollectionPlanAsync_MunicipalWindowValidatesWholePlanBeforeWrites(
        AgentWorkflowTriggerType trigger, bool invalidMiddle)
    {
        using var db = CreateContext(Guid.NewGuid().ToString());
        var reportId = Guid.NewGuid();
        var binId = Guid.NewGuid();
        var secondReportId = Guid.NewGuid();
        var nineUtc = NextValidUtc(9);
        var nineLocalIso = TimeZoneInfo.ConvertTime(new DateTimeOffset(nineUtc),
            TimeZoneInfo.FindSystemTimeZoneById("Asia/Colombo")).ToString("O");
        var elevenUtc = NextValidUtc(11);
        var fourteenThirtyUtc = NextValidUtc(14).AddMinutes(30);
        var overnightUtc = NextValidUtc(9).Date.AddDays(1).AddHours(-3); // 02:30 local next day
        var middleUtc = invalidMiddle ? overnightUtc : elevenUtc;

        var c2Output = JsonSerializer.Serialize(new
        {
            status = "completed",
            isCompleteSnapshot = true,
            candidateGroups = new[]
            {
                new { groupId = "group-1", proposedSchedule = new { scheduledAt = nineLocalIso, schedulingReason = "Morning report collection." }, needReferences = new[] { new { needId = reportId, targetType = "Report", collectionReason = "VerifiedReport" } } },
                new { groupId = "group-2", proposedSchedule = new { scheduledAt = middleUtc.ToString("O"), schedulingReason = "Municipal bin collection." }, needReferences = new[] { new { needId = binId, targetType = "Bin", collectionReason = "FullOrBlockedBin" } } },
                new { groupId = "group-3", proposedSchedule = new { scheduledAt = fourteenThirtyUtc.ToString("O"), schedulingReason = "Afternoon report collection." }, needReferences = new[] { new { needId = secondReportId, targetType = "Report", collectionReason = "VerifiedReport" } } }
            },
            separateHandling = Array.Empty<object>(),
            deferredNeeds = Array.Empty<object>()
        });
        var (workflowId, _, managerId, _, _) = SeedWorkflowWithApprovedC2Plan(
            db, c2Output, explicitReportId: reportId, explicitBinId: binId, trigger: trigger);
        db.WasteReports.Add(new WasteReport
        {
            Id = secondReportId, CitizenId = Guid.NewGuid(), Description = "Second verified collection report",
            Latitude = 6.9, Longitude = 79.8, Status = WasteReportStatus.Verified,
            WasteType = WasteType.General, CreatedAt = DateTime.UtcNow.AddDays(-1), VerifiedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var sut = CreateServiceWithRealTaskService(db);

        if (invalidMiddle)
        {
            var act = () => sut.ExecuteCollectionPlanAsync(workflowId,
                new ExecuteCollectionPlanRequest { ExpectedVersion = 3 }, managerId);
            await act.Should().ThrowAsync<BusinessRuleConflictException>().WithMessage("*municipality timezone*");
            (await db.CollectionTasks.CountAsync()).Should().Be(0);
            (await db.AgentWorkflowExecutionResults.CountAsync()).Should().Be(0);
            (await db.AgentWorkflows.FindAsync(workflowId))!.Status.Should().Be(AgentWorkflowStatus.CollectionApproved);
        }
        else
        {
            var result = await sut.ExecuteCollectionPlanAsync(workflowId,
                new ExecuteCollectionPlanRequest { ExpectedVersion = 3 }, managerId);
            result.Status.Should().Be(AgentWorkflowStatus.FleetPlanning);
            var tasks = await db.CollectionTasks.ToListAsync();
            tasks.Should().HaveCount(3);
            tasks.Single(t => t.WasteReportId == reportId).ScheduledAt.Should().Be(nineUtc);
            tasks.Single(t => t.WasteBinId == binId).ScheduledAt.Should().Be(elevenUtc);
            tasks.Single(t => t.WasteReportId == secondReportId).ScheduledAt.Should().Be(fourteenThirtyUtc);
        }
    }

    [Fact]
    public async Task ExecuteCollectionPlanAsync_StaleAfterHumanApproval_CreatesNoTasks()
    {
        using var db = CreateContext(Guid.NewGuid().ToString());
        var reportId = Guid.NewGuid();
        var proposed = NextValidUtc(9);
        var c2Output = JsonSerializer.Serialize(new
        {
            status = "completed", isCompleteSnapshot = true,
            candidateGroups = Array.Empty<object>(),
            separateHandling = new[] { new { needReference = new { needId = reportId, targetType = "Report", collectionReason = "VerifiedReport" }, proposedSchedule = new { scheduledAt = proposed.ToString("O"), schedulingReason = "Approved morning pickup." } } },
            deferredNeeds = Array.Empty<object>()
        });
        var (workflowId, _, managerId, _, _) = SeedWorkflowWithApprovedC2Plan(
            db, c2Output, explicitReportId: reportId);
        var clock = new FixedTimeProvider(new DateTimeOffset(proposed.AddHours(1), TimeSpan.Zero));
        var sut = CreateServiceWithRealTaskService(db, clock);

        var act = () => sut.ExecuteCollectionPlanAsync(workflowId,
            new ExecuteCollectionPlanRequest { ExpectedVersion = 3 }, managerId);
        await act.Should().ThrowAsync<BusinessRuleConflictException>().WithMessage("*past*");
        (await db.CollectionTasks.CountAsync()).Should().Be(0);
        (await db.AgentWorkflows.FindAsync(workflowId))!.Status.Should().Be(AgentWorkflowStatus.CollectionApproved);
    }

    [Fact]
    public async Task ExecuteCollectionPlanAsync_CandidateGroup_CreatesScheduledTasksAndTransitionsToFleetPlanning()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateContext(dbName);

        var reportId = Guid.NewGuid();
        var binId = Guid.NewGuid();
        var futureTime = NextValidUtc().ToString("yyyy-MM-ddTHH:mm:ssZ");

        var c2Output = JsonSerializer.Serialize(new
        {
            status = "completed",
            isCompleteSnapshot = true,
            candidateGroups = new[]
            {
                new
                {
                    groupId = "group-1",
                    attentionOrder = 1,
                    proposedSchedule = new
                    {
                        scheduledAt = futureTime,
                        schedulingReason = "Urgent morning collection required."
                    },
                    wasteHandlingConsiderations = new[] { "Use compactor vehicle", "Wear PPE" },
                    needReferences = new[]
                    {
                        new { needId = reportId, targetType = "Report", collectionReason = "VerifiedReport" },
                        new { needId = binId, targetType = "Bin", collectionReason = "FullOrBlockedBin" }
                    }
                }
            },
            separateHandling = Array.Empty<object>(),
            deferredNeeds = Array.Empty<object>()
        });

        var (workflowId, _, managerId, _, _) = SeedWorkflowWithApprovedC2Plan(
            db, c2Output, version: 3, explicitReportId: reportId, explicitBinId: binId);

        var service = CreateServiceWithRealTaskService(db);

        var request = new ExecuteCollectionPlanRequest { ExpectedVersion = 3 };
        var result = await service.ExecuteCollectionPlanAsync(workflowId, request, managerId);

        // Verify result DTO
        result.Should().NotBeNull();
        result.Status.Should().Be(AgentWorkflowStatus.FleetPlanning);
        result.CurrentStep.Should().Be(WorkflowStepType.FleetPlanning);
        result.Version.Should().Be(5); // 3 -> 4 (CreatingScheduledTasks) -> 5 (FleetPlanning)

        // Verify workflow steps
        var execStep = result.Steps.FirstOrDefault(s => s.StepType == WorkflowStepType.ScheduledTaskCreation);
        execStep.Should().NotBeNull();
        execStep!.Status.Should().Be(WorkflowStepStatus.Completed);
        execStep.Sequence.Should().Be(2);

        // Verify execution results
        result.ExecutionResults.Should().ContainSingle();
        var execResult = result.ExecutionResults[0];
        execResult.ExecutionType.Should().Be(WorkflowExecutionType.CollectionTaskCreation);
        execResult.Status.Should().Be(WorkflowExecutionStatus.Succeeded);

        // Verify ResultJson contains 2 tasks
        execResult.Result.Should().NotBeNull();
        execResult.Result!.Value.GetProperty("createdTaskCount").GetInt32().Should().Be(2);
        execResult.Result!.Value.GetProperty("deferredNeedCount").GetInt32().Should().Be(0);

        // Verify real CollectionTasks were created in database
        var tasks = await db.CollectionTasks.ToListAsync();
        tasks.Should().HaveCount(2);
        tasks.Should().OnlyContain(t => t.Status == CollectionTaskStatus.Scheduled);
        tasks.Should().OnlyContain(t => t.CreationMethod == TaskCreationMethod.ApprovedAiPlan);
        tasks.Should().OnlyContain(t => t.SchedulingReason == "Urgent morning collection required.");
        tasks.Should().OnlyContain(t => t.HandlingNotes == "Use compactor vehicle; Wear PPE");

        // Verify report task has report target and no bin target
        var reportTask = tasks.FirstOrDefault(t => t.WasteReportId == reportId);
        reportTask.Should().NotBeNull();
        reportTask!.WasteBinId.Should().BeNull();
        reportTask.CollectionReason.Should().Be(CollectionReason.VerifiedReport);

        // Verify bin task has bin target and no report target
        var binTask = tasks.FirstOrDefault(t => t.WasteBinId == binId);
        binTask.Should().NotBeNull();
        binTask!.WasteReportId.Should().BeNull();
        binTask.CollectionReason.Should().Be(CollectionReason.FullOrBlockedBin);

        // Verify WasteReport was transitioned to Scheduled
        var updatedReport = await db.WasteReports.FindAsync(reportId);
        updatedReport!.Status.Should().Be(WasteReportStatus.Scheduled);
    }

    [Fact]
    public async Task ExecuteCollectionPlanAsync_SeparateHandling_CreatesTaskWithItsSchedule()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateContext(dbName);

        var reportId = Guid.NewGuid();
        var futureTime = NextValidUtc().ToString("yyyy-MM-ddTHH:mm:ssZ");

        var c2Output = JsonSerializer.Serialize(new
        {
            status = "completed",
            isCompleteSnapshot = true,
            candidateGroups = Array.Empty<object>(),
            separateHandling = new[]
            {
                new
                {
                    needReference = new { needId = reportId, targetType = "Report", collectionReason = "VerifiedReport" },
                    proposedSchedule = new
                    {
                        scheduledAt = futureTime,
                        schedulingReason = "Special individual handling required for clinical waste."
                    }
                }
            },
            deferredNeeds = Array.Empty<object>()
        });

        var (workflowId, _, managerId, _, _) = SeedWorkflowWithApprovedC2Plan(
            db, c2Output, version: 1, explicitReportId: reportId);

        var service = CreateServiceWithRealTaskService(db);

        var result = await service.ExecuteCollectionPlanAsync(workflowId, new ExecuteCollectionPlanRequest { ExpectedVersion = 1 }, managerId);

        result.Status.Should().Be(AgentWorkflowStatus.FleetPlanning);
        var tasks = await db.CollectionTasks.ToListAsync();
        tasks.Should().ContainSingle();
        tasks[0].WasteReportId.Should().Be(reportId);
        tasks[0].SchedulingReason.Should().Be("Special individual handling required for clinical waste.");
    }

    [Fact]
    public async Task ExecuteCollectionPlanAsync_MixedGroupSeparateDeferred_CreatesOnlyExecutableTasks()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateContext(dbName);

        var reportId = Guid.NewGuid();
        var binId = Guid.NewGuid();
        var deferredId1 = Guid.NewGuid();
        var deferredId2 = Guid.NewGuid();
        var futureTime = NextValidUtc().ToString("yyyy-MM-ddTHH:mm:ssZ");

        var c2Output = JsonSerializer.Serialize(new
        {
            status = "completed",
            isCompleteSnapshot = true,
            candidateGroups = new[]
            {
                new
                {
                    groupId = "group-1",
                    attentionOrder = 1,
                    proposedSchedule = new { scheduledAt = futureTime, schedulingReason = "Group collection" },
                    needReferences = new[]
                    {
                        new { needId = reportId, targetType = "Report", collectionReason = "VerifiedReport" }
                    }
                }
            },
            separateHandling = new[]
            {
                new
                {
                    needReference = new { needId = binId, targetType = "Bin", collectionReason = "FullOrBlockedBin" },
                    proposedSchedule = new { scheduledAt = futureTime, schedulingReason = "Separate bin collection" }
                }
            },
            deferredNeeds = new[]
            {
                new { needReference = new { needId = deferredId1, targetType = "Bin", collectionReason = "RoutineCollection" } },
                new { needReference = new { needId = deferredId2, targetType = "Report", collectionReason = "VerifiedReport" } }
            }
        });

        var (workflowId, _, managerId, _, _) = SeedWorkflowWithApprovedC2Plan(
            db, c2Output, version: 2, explicitReportId: reportId, explicitBinId: binId);

        var service = CreateServiceWithRealTaskService(db);

        var result = await service.ExecuteCollectionPlanAsync(workflowId, new ExecuteCollectionPlanRequest { ExpectedVersion = 2 }, managerId);

        result.Status.Should().Be(AgentWorkflowStatus.FleetPlanning);

        // Only 2 executable tasks created
        var tasks = await db.CollectionTasks.ToListAsync();
        tasks.Should().HaveCount(2);

        // ResultJson should reflect 2 created, 2 deferred
        var execResult = result.ExecutionResults.First();
        execResult.Result!.Value.GetProperty("createdTaskCount").GetInt32().Should().Be(2);
        execResult.Result!.Value.GetProperty("deferredNeedCount").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task ExecuteCollectionPlanAsync_AllDeferred_ThrowsConflictPreflight()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateContext(dbName);

        var c2Output = JsonSerializer.Serialize(new
        {
            status = "completed",
            isCompleteSnapshot = true,
            candidateGroups = Array.Empty<object>(),
            separateHandling = Array.Empty<object>(),
            deferredNeeds = new[]
            {
                new { needReference = new { needId = Guid.NewGuid(), targetType = "Report", collectionReason = "VerifiedReport" } }
            }
        });

        var (workflowId, _, managerId, _, _) = SeedWorkflowWithApprovedC2Plan(db, c2Output, version: 1);
        var service = CreateServiceWithRealTaskService(db);

        var act = () => service.ExecuteCollectionPlanAsync(workflowId, new ExecuteCollectionPlanRequest { ExpectedVersion = 1 }, managerId);

        await act.Should().ThrowAsync<BusinessRuleConflictException>()
            .WithMessage("*zero executable collection needs*");

        var workflow = await db.AgentWorkflows.FindAsync(workflowId);
        workflow!.Status.Should().Be(AgentWorkflowStatus.CollectionApproved);
        (await db.CollectionTasks.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ExecuteCollectionPlanAsync_DuplicateNeedReference_ThrowsConflictPreflight()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateContext(dbName);

        var duplicateId = Guid.NewGuid();
        var futureTime = NextValidUtc().ToString("yyyy-MM-ddTHH:mm:ssZ");

        var c2Output = JsonSerializer.Serialize(new
        {
            status = "completed",
            isCompleteSnapshot = true,
            candidateGroups = new[]
            {
                new
                {
                    groupId = "group-1",
                    attentionOrder = 1,
                    proposedSchedule = new { scheduledAt = futureTime, schedulingReason = "Group 1" },
                    needReferences = new[]
                    {
                        new { needId = duplicateId, targetType = "Report", collectionReason = "VerifiedReport" }
                    }
                }
            },
            separateHandling = new[]
            {
                new
                {
                    needReference = new { needId = duplicateId, targetType = "Report", collectionReason = "VerifiedReport" },
                    proposedSchedule = new { scheduledAt = futureTime, schedulingReason = "Separate 1" }
                }
            },
            deferredNeeds = Array.Empty<object>()
        });

        var (workflowId, _, managerId, _, _) = SeedWorkflowWithApprovedC2Plan(
            db, c2Output, version: 1, explicitReportId: duplicateId);
        var service = CreateServiceWithRealTaskService(db);

        var act = () => service.ExecuteCollectionPlanAsync(workflowId, new ExecuteCollectionPlanRequest { ExpectedVersion = 1 }, managerId);

        await act.Should().ThrowAsync<BusinessRuleConflictException>()
            .WithMessage("*Duplicate collection need reference detected*");

        var workflow = await db.AgentWorkflows.FindAsync(workflowId);
        workflow!.Status.Should().Be(AgentWorkflowStatus.CollectionApproved);
    }

    [Fact]
    public async Task ExecuteCollectionPlanAsync_TargetReportNotVerified_ThrowsConflictPreflight()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateContext(dbName);

        var reportId = Guid.NewGuid();
        var futureTime = NextValidUtc().ToString("yyyy-MM-ddTHH:mm:ssZ");

        var c2Output = JsonSerializer.Serialize(new
        {
            status = "completed",
            isCompleteSnapshot = true,
            candidateGroups = new[]
            {
                new
                {
                    groupId = "group-1",
                    attentionOrder = 1,
                    proposedSchedule = new { scheduledAt = futureTime, schedulingReason = "Routine check" },
                    needReferences = new[]
                    {
                        new { needId = reportId, targetType = "Report", collectionReason = "VerifiedReport" }
                    }
                }
            },
            separateHandling = Array.Empty<object>(),
            deferredNeeds = Array.Empty<object>()
        });

        var (workflowId, _, managerId, _, _) = SeedWorkflowWithApprovedC2Plan(
            db, c2Output, version: 1, explicitReportId: reportId);

        var report = await db.WasteReports.FirstAsync();
        report.Status = WasteReportStatus.InProgress; // Not Verified!
        await db.SaveChangesAsync();

        var service = CreateServiceWithRealTaskService(db);

        var act = () => service.ExecuteCollectionPlanAsync(workflowId, new ExecuteCollectionPlanRequest { ExpectedVersion = 1 }, managerId);

        await act.Should().ThrowAsync<BusinessRuleConflictException>()
            .WithMessage("*Current status is 'InProgress', but 'Verified' is required*");

        var workflow = await db.AgentWorkflows.FindAsync(workflowId);
        workflow!.Status.Should().Be(AgentWorkflowStatus.CollectionApproved);
    }

    [Fact]
    public async Task ExecuteCollectionPlanAsync_TargetAlreadyHasActiveTask_ThrowsConflictPreflight()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateContext(dbName);

        var reportId = Guid.NewGuid();
        var futureTime = NextValidUtc().ToString("yyyy-MM-ddTHH:mm:ssZ");

        var c2Output = JsonSerializer.Serialize(new
        {
            status = "completed",
            isCompleteSnapshot = true,
            candidateGroups = new[]
            {
                new
                {
                    groupId = "group-1",
                    attentionOrder = 1,
                    proposedSchedule = new { scheduledAt = futureTime, schedulingReason = "Routine check" },
                    needReferences = new[]
                    {
                        new { needId = reportId, targetType = "Report", collectionReason = "VerifiedReport" }
                    }
                }
            },
            separateHandling = Array.Empty<object>(),
            deferredNeeds = Array.Empty<object>()
        });

        var (workflowId, _, managerId, _, _) = SeedWorkflowWithApprovedC2Plan(
            db, c2Output, version: 1, explicitReportId: reportId);

        // Existing active task for this report
        db.CollectionTasks.Add(new CollectionTask
        {
            Id = Guid.NewGuid(),
            TaskCode = "TSK-ACTIVE-001",
            WasteReportId = reportId,
            Status = CollectionTaskStatus.Scheduled,
            CollectionReason = CollectionReason.VerifiedReport,
            CreatedByUserId = managerId,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var service = CreateServiceWithRealTaskService(db);

        var act = () => service.ExecuteCollectionPlanAsync(workflowId, new ExecuteCollectionPlanRequest { ExpectedVersion = 1 }, managerId);

        await act.Should().ThrowAsync<BusinessRuleConflictException>()
            .WithMessage("*already has an active collection task*");

        var workflow = await db.AgentWorkflows.FindAsync(workflowId);
        workflow!.Status.Should().Be(AgentWorkflowStatus.CollectionApproved);
    }

    [Fact]
    public async Task ExecuteCollectionPlanAsync_ScheduleInThePast_ThrowsConflictPreflight()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateContext(dbName);

        var reportId = Guid.NewGuid();
        var pastTime = DateTime.UtcNow.AddHours(-2).ToString("yyyy-MM-ddTHH:mm:ssZ"); // in past!

        var c2Output = JsonSerializer.Serialize(new
        {
            status = "completed",
            isCompleteSnapshot = true,
            candidateGroups = new[]
            {
                new
                {
                    groupId = "group-1",
                    attentionOrder = 1,
                    proposedSchedule = new { scheduledAt = pastTime, schedulingReason = "Past schedule" },
                    needReferences = new[]
                    {
                        new { needId = reportId, targetType = "Report", collectionReason = "VerifiedReport" }
                    }
                }
            },
            separateHandling = Array.Empty<object>(),
            deferredNeeds = Array.Empty<object>()
        });

        var (workflowId, _, managerId, _, _) = SeedWorkflowWithApprovedC2Plan(
            db, c2Output, version: 1, explicitReportId: reportId);

        var service = CreateServiceWithRealTaskService(db);

        var act = () => service.ExecuteCollectionPlanAsync(workflowId, new ExecuteCollectionPlanRequest { ExpectedVersion = 1 }, managerId);

        await act.Should().ThrowAsync<BusinessRuleConflictException>()
            .WithMessage("*is in the past and cannot be executed*");

        var workflow = await db.AgentWorkflows.FindAsync(workflowId);
        workflow!.Status.Should().Be(AgentWorkflowStatus.CollectionApproved);
    }

    [Fact]
    public async Task ExecuteCollectionPlanAsync_NoApprovalRecord_ThrowsConflict()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateContext(dbName);

        var c2Output = JsonSerializer.Serialize(new
        {
            status = "completed",
            isCompleteSnapshot = true,
            candidateGroups = Array.Empty<object>(),
            separateHandling = Array.Empty<object>(),
            deferredNeeds = Array.Empty<object>()
        });

        var (workflowId, _, managerId, _, _) = SeedWorkflowWithApprovedC2Plan(db, c2Output, version: 1);

        // Remove the approval
        var approvals = await db.AgentWorkflowApprovals.ToListAsync();
        db.AgentWorkflowApprovals.RemoveRange(approvals);
        await db.SaveChangesAsync();

        var service = CreateServiceWithRealTaskService(db);

        var act = () => service.ExecuteCollectionPlanAsync(workflowId, new ExecuteCollectionPlanRequest { ExpectedVersion = 1 }, managerId);

        await act.Should().ThrowAsync<BusinessRuleConflictException>()
            .WithMessage("*no recorded human approval*");
    }

    [Fact]
    public async Task ExecuteCollectionPlanAsync_InvalidC2Output_ThrowsConflict()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateContext(dbName);

        var c2Output = JsonSerializer.Serialize(new
        {
            status = "partial", // NOT completed!
            isCompleteSnapshot = false
        });

        var (workflowId, _, managerId, _, _) = SeedWorkflowWithApprovedC2Plan(db, c2Output, version: 1);
        var service = CreateServiceWithRealTaskService(db);

        var act = () => service.ExecuteCollectionPlanAsync(workflowId, new ExecuteCollectionPlanRequest { ExpectedVersion = 1 }, managerId);

        await act.Should().ThrowAsync<BusinessRuleConflictException>()
            .WithMessage("*status must be 'completed'*");
    }

    [Fact]
    public async Task ExecuteCollectionPlanAsync_WrongStatus_ThrowsConflict()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateContext(dbName);

        var c2Output = JsonSerializer.Serialize(new
        {
            status = "completed",
            isCompleteSnapshot = true
        });

        var (workflowId, _, managerId, _, _) = SeedWorkflowWithApprovedC2Plan(
            db, c2Output, status: AgentWorkflowStatus.AwaitingCollectionApproval, version: 1);
        var service = CreateServiceWithRealTaskService(db);

        var act = () => service.ExecuteCollectionPlanAsync(workflowId, new ExecuteCollectionPlanRequest { ExpectedVersion = 1 }, managerId);

        await act.Should().ThrowAsync<BusinessRuleConflictException>()
            .WithMessage("*Execution requires 'CollectionApproved'*");
    }

    [Fact]
    public async Task ExecuteCollectionPlanAsync_StaleExpectedVersion_ThrowsConflict()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateContext(dbName);

        var c2Output = JsonSerializer.Serialize(new
        {
            status = "completed",
            isCompleteSnapshot = true
        });

        var (workflowId, _, managerId, _, _) = SeedWorkflowWithApprovedC2Plan(db, c2Output, version: 10);
        var service = CreateServiceWithRealTaskService(db);

        var act = () => service.ExecuteCollectionPlanAsync(workflowId, new ExecuteCollectionPlanRequest { ExpectedVersion = 9 }, managerId);

        await act.Should().ThrowAsync<BusinessRuleConflictException>()
            .WithMessage("*The workflow has changed since it was loaded*");
    }

    [Fact]
    public async Task ExecuteCollectionPlanAsync_IdempotentRetry_ReturnsExistingWithoutDuplicateTasks()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateContext(dbName);

        var reportId = Guid.NewGuid();
        var futureTime = NextValidUtc().ToString("yyyy-MM-ddTHH:mm:ssZ");

        var c2Output = JsonSerializer.Serialize(new
        {
            status = "completed",
            isCompleteSnapshot = true,
            candidateGroups = new[]
            {
                new
                {
                    groupId = "group-1",
                    attentionOrder = 1,
                    proposedSchedule = new { scheduledAt = futureTime, schedulingReason = "Plan 1" },
                    needReferences = new[]
                    {
                        new { needId = reportId, targetType = "Report", collectionReason = "VerifiedReport" }
                    }
                }
            },
            separateHandling = Array.Empty<object>(),
            deferredNeeds = Array.Empty<object>()
        });

        var (workflowId, _, managerId, _, _) = SeedWorkflowWithApprovedC2Plan(
            db, c2Output, version: 1, explicitReportId: reportId);

        var service = CreateServiceWithRealTaskService(db);

        // First execution succeeds
        var firstResult = await service.ExecuteCollectionPlanAsync(workflowId, new ExecuteCollectionPlanRequest { ExpectedVersion = 1 }, managerId);
        firstResult.Status.Should().Be(AgentWorkflowStatus.FleetPlanning);
        (await db.CollectionTasks.CountAsync()).Should().Be(1);

        // Second execution with current version (3) should be idempotent
        var secondResult = await service.ExecuteCollectionPlanAsync(workflowId, new ExecuteCollectionPlanRequest { ExpectedVersion = 3 }, managerId);
        secondResult.Status.Should().Be(AgentWorkflowStatus.FleetPlanning);

        // No new tasks created
        (await db.CollectionTasks.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task ExecuteCollectionPlanAsync_ResumePendingExecution_Succeeds()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateContext(dbName);

        var reportId = Guid.NewGuid();
        var futureTime = NextValidUtc().ToString("yyyy-MM-ddTHH:mm:ssZ");

        var c2Output = JsonSerializer.Serialize(new
        {
            status = "completed",
            isCompleteSnapshot = true,
            candidateGroups = new[]
            {
                new
                {
                    groupId = "group-1",
                    attentionOrder = 1,
                    proposedSchedule = new { scheduledAt = futureTime, schedulingReason = "Resume plan" },
                    needReferences = new[]
                    {
                        new { needId = reportId, targetType = "Report", collectionReason = "VerifiedReport" }
                    }
                }
            },
            separateHandling = Array.Empty<object>(),
            deferredNeeds = Array.Empty<object>()
        });

        // Seed workflow directly in CreatingScheduledTasks with Running step and Pending result
        var (workflowId, c2StepId, managerId, _, _) = SeedWorkflowWithApprovedC2Plan(
            db, c2Output, status: AgentWorkflowStatus.CreatingScheduledTasks, version: 4, explicitReportId: reportId);

        var execStep = new AgentWorkflowStep
        {
            Id = Guid.NewGuid(),
            WorkflowId = workflowId,
            Sequence = 2,
            StepType = WorkflowStepType.ScheduledTaskCreation,
            Status = WorkflowStepStatus.Running,
            StartedAt = DateTime.UtcNow.AddMinutes(-5)
        };
        db.AgentWorkflowSteps.Add(execStep);

        var execResult = new AgentWorkflowExecutionResult
        {
            Id = Guid.NewGuid(),
            WorkflowId = workflowId,
            WorkflowStepId = execStep.Id,
            ExecutionType = WorkflowExecutionType.CollectionTaskCreation,
            Status = WorkflowExecutionStatus.Pending,
            ExecutedAt = DateTime.UtcNow.AddMinutes(-5)
        };
        db.AgentWorkflowExecutionResults.Add(execResult);
        await db.SaveChangesAsync();

        var service = CreateServiceWithRealTaskService(db);

        // Calling with version 4 resumes
        var result = await service.ExecuteCollectionPlanAsync(workflowId, new ExecuteCollectionPlanRequest { ExpectedVersion = 4 }, managerId);

        result.Status.Should().Be(AgentWorkflowStatus.FleetPlanning);
        result.CurrentStep.Should().Be(WorkflowStepType.FleetPlanning);
        (await db.CollectionTasks.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task ExecuteCollectionPlanAsync_TaskCreationFails_RollsBackAndRecordsDurableFailure()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateContext(dbName);

        var reportId = Guid.NewGuid();
        var futureTime = NextValidUtc().ToString("yyyy-MM-ddTHH:mm:ssZ");

        var c2Output = JsonSerializer.Serialize(new
        {
            status = "completed",
            isCompleteSnapshot = true,
            candidateGroups = new[]
            {
                new
                {
                    groupId = "group-1",
                    attentionOrder = 1,
                    proposedSchedule = new { scheduledAt = futureTime, schedulingReason = "Failing task" },
                    needReferences = new[]
                    {
                        new { needId = reportId, targetType = "Report", collectionReason = "VerifiedReport" }
                    }
                }
            },
            separateHandling = Array.Empty<object>(),
            deferredNeeds = Array.Empty<object>()
        });

        var (workflowId, _, managerId, _, _) = SeedWorkflowWithApprovedC2Plan(
            db, c2Output, version: 1, explicitReportId: reportId);

        var (service, mockTaskService) = CreateServiceWithMockTaskService(db);
        mockTaskService.Setup(m => m.CreateTaskFromApprovedPlanAsync(
                It.IsAny<CreateManualCollectionTaskRequest>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BusinessRuleConflictException("Downstream vehicle shortage simulated failure."));

        var act = () => service.ExecuteCollectionPlanAsync(workflowId, new ExecuteCollectionPlanRequest { ExpectedVersion = 1 }, managerId);

        await act.Should().ThrowAsync<BusinessRuleConflictException>()
            .WithMessage("*Downstream vehicle shortage simulated failure*");

        // Verify durable failure state
        var workflow = await db.AgentWorkflows
            .Include(w => w.Steps)
            .Include(w => w.ExecutionResults)
            .Include(w => w.Transitions)
            .FirstAsync(w => w.Id == workflowId);

        workflow.Status.Should().Be(AgentWorkflowStatus.Failed);
        workflow.CompletedAt.Should().NotBeNull();

        var failedStep = workflow.Steps.FirstOrDefault(s => s.StepType == WorkflowStepType.ScheduledTaskCreation);
        failedStep.Should().NotBeNull();
        failedStep!.Status.Should().Be(WorkflowStepStatus.Failed);
        failedStep.ErrorMessage.Should().Contain("vehicle shortage");

        var failedResult = workflow.ExecutionResults.FirstOrDefault(e => e.ExecutionType == WorkflowExecutionType.CollectionTaskCreation);
        failedResult.Should().NotBeNull();
        failedResult!.Status.Should().Be(WorkflowExecutionStatus.Failed);
        failedResult.ErrorMessage.Should().Contain("vehicle shortage");

        workflow.Transitions.Should().Contain(t => t.ToStatus == AgentWorkflowStatus.Failed);
    }

    [Fact]
    public async Task ExecuteCollectionPlanAsync_CanonicalDocumentationFixture_ExecutesCleanly()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateContext(dbName);

        var binId1 = Guid.NewGuid();
        var binId2 = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        var deferredBinId = Guid.NewGuid();

        var bin1 = new WasteBin
        {
            Id = binId1,
            BinCode = "BIN-CANON-001",
            Latitude = 6.9271,
            Longitude = 79.8612,
            CapacityLiters = 660,
            AdministrativeStatus = BinAdministrativeStatus.Active,
            CollectionWeekdays = new[] { 1, 2, 3, 4, 5 },
            CreatedAt = DateTime.UtcNow.AddDays(-30)
        };
        bin1.AcceptedWasteTypes.Add(new WasteBinAcceptedWasteType { WasteBinId = bin1.Id, WasteType = WasteType.General });
        db.WasteBins.Add(bin1);
        db.BinObservations.Add(new BinObservation { Id = Guid.NewGuid(), WasteBinId = binId1, RecordedAt = DateTime.UtcNow.AddHours(-1), FillLevelPercent = 100, Condition = BinCondition.Good });

        var bin2 = new WasteBin
        {
            Id = binId2,
            BinCode = "BIN-CANON-002",
            Latitude = 6.9280,
            Longitude = 79.8620,
            CapacityLiters = 1100,
            AdministrativeStatus = BinAdministrativeStatus.Active,
            CollectionWeekdays = new[] { 1, 2, 3, 4, 5 },
            CreatedAt = DateTime.UtcNow.AddDays(-30)
        };
        bin2.AcceptedWasteTypes.Add(new WasteBinAcceptedWasteType { WasteBinId = bin2.Id, WasteType = WasteType.General });
        db.WasteBins.Add(bin2);
        db.BinObservations.Add(new BinObservation { Id = Guid.NewGuid(), WasteBinId = binId2, RecordedAt = DateTime.UtcNow.AddHours(-1), FillLevelPercent = 100, Condition = BinCondition.Good });

        var report = new WasteReport
        {
            Id = reportId,
            CitizenId = Guid.NewGuid(),
            Description = "Hazardous chemical spill near warehouse",
            AddressText = "Industrial Zone, Kelaniya",
            Latitude = 6.9600,
            Longitude = 79.9100,
            Status = WasteReportStatus.Verified,
            WasteType = WasteType.Hazardous,
            CreatedAt = DateTime.UtcNow.AddHours(-12),
            VerifiedAt = DateTime.UtcNow.AddHours(-3)
        };
        db.WasteReports.Add(report);

        var futureTime1 = NextValidUtc(9).ToString("yyyy-MM-ddTHH:mm:ssZ");
        var futureTime2 = NextValidUtc(10).ToString("yyyy-MM-ddTHH:mm:ssZ");

        // Exact canonical JSON from ai-service/docs/agent_contracts.md
        var canonicalJson = JsonSerializer.Serialize(new
        {
            objective = "Plan urgent and high-priority collection needs for tomorrow morning.",
            candidateGroups = new[]
            {
                new
                {
                    groupId = "group-1",
                    attentionOrder = 1,
                    needReferences = new[]
                    {
                        new { needId = binId1, targetType = "Bin", collectionReason = "FullOrBlockedBin", urgency = "Urgent" },
                        new { needId = binId2, targetType = "Bin", collectionReason = "FullOrBlockedBin", urgency = "High" }
                    },
                    proposedSchedule = new
                    {
                        scheduledAt = futureTime1,
                        schedulingReason = "Critical morning clearance for overflow bins in commercial district."
                    },
                    rationale = "High urgency commercial bins grouped by proximity.",
                    wasteHandlingConsiderations = new[] { "Standard compactor vehicle suitable" },
                    warnings = Array.Empty<string>()
                }
            },
            separateHandling = new[]
            {
                new
                {
                    needReference = new { needId = reportId, targetType = "Report", collectionReason = "VerifiedReport", urgency = "High" },
                    attentionOrder = 1,
                    proposedSchedule = new
                    {
                        scheduledAt = futureTime2,
                        schedulingReason = "Hazardous industrial waste requires dedicated containment vehicle."
                    },
                    rationale = "Hazardous materials require dedicated handling separate from standard compactors."
                }
            },
            deferredNeeds = new[]
            {
                new
                {
                    needReference = new { needId = deferredBinId, targetType = "Bin", collectionReason = "RoutineCollection", urgency = "Low" },
                    attentionOrder = (int?)null,
                    proposedSchedule = (object?)null,
                    rationale = "Low urgency bin deferred to regular weekly route cycle."
                }
            },
            warnings = new[] { "Need lacks current bin telemetry" },
            sourcePage = 1,
            sourcePageSize = 20,
            sourceTotalCount = 3,
            sourceTotalPages = 1,
            retrievedPages = new[] { 1 },
            isCompleteSnapshot = true,
            agentName = "collection_planning_agent",
            modelName = "gemini-2.5-flash",
            advisoryOnly = true,
            status = "completed"
        });

        var (workflowId, c2StepId, managerId, _, _) = SeedWorkflowWithApprovedC2Plan(db, canonicalJson, version: 1);
        var service = CreateServiceWithRealTaskService(db);

        var result = await service.ExecuteCollectionPlanAsync(workflowId, new ExecuteCollectionPlanRequest { ExpectedVersion = 1 }, managerId);

        result.Status.Should().Be(AgentWorkflowStatus.FleetPlanning);
        result.CurrentStep.Should().Be(WorkflowStepType.FleetPlanning);

        var tasks = await db.CollectionTasks.ToListAsync();
        tasks.Should().HaveCount(3);

        var execResult = result.ExecutionResults.First();
        execResult.Result!.Value.GetProperty("createdTaskCount").GetInt32().Should().Be(3);
        execResult.Result!.Value.GetProperty("deferredNeedCount").GetInt32().Should().Be(1);
        execResult.Result!.Value.GetProperty("sourceCollectionPlanningStepId").GetString().Should().Be(c2StepId.ToString());

        var stepOutput = result.Steps.First(s => s.StepType == WorkflowStepType.ScheduledTaskCreation);
        stepOutput.Output.Should().NotBeNull();
        stepOutput.Output!.Value.GetProperty("sourceCollectionPlanningStepId").GetString().Should().Be(c2StepId.ToString());
    }

    [Fact]
    public async Task ExecuteCollectionPlanAsync_ApprovedSchedulingReasonPreserved_MatchesExactProse()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateContext(dbName);

        var reportId = Guid.NewGuid();
        var distinctiveReason = "Prioritize verified overflow report before evening traffic.";
        var futureTime = NextValidUtc().ToString("yyyy-MM-ddTHH:mm:ssZ");

        var c2Output = JsonSerializer.Serialize(new
        {
            status = "completed",
            isCompleteSnapshot = true,
            candidateGroups = Array.Empty<object>(),
            separateHandling = new[]
            {
                new
                {
                    needReference = new { needId = reportId, targetType = "Report", collectionReason = "VerifiedReport" },
                    proposedSchedule = new
                    {
                        scheduledAt = futureTime,
                        schedulingReason = distinctiveReason
                    }
                }
            },
            deferredNeeds = Array.Empty<object>()
        });

        var (workflowId, _, managerId, _, _) = SeedWorkflowWithApprovedC2Plan(
            db, c2Output, version: 1, explicitReportId: reportId);

        var service = CreateServiceWithRealTaskService(db);
        await service.ExecuteCollectionPlanAsync(workflowId, new ExecuteCollectionPlanRequest { ExpectedVersion = 1 }, managerId);

        var task = await db.CollectionTasks.FirstAsync(t => t.WasteReportId == reportId);
        task.SchedulingReason.Should().Be(distinctiveReason);
    }

    [Fact]
    public async Task ExecuteCollectionPlanAsync_ApprovedScheduledAtPreserved_MatchesExactUtcInstant()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateContext(dbName);

        var reportId = Guid.NewGuid();
        var exactUtcInstant = NextValidUtc(); // 09:00 municipality local time
        var timeString = exactUtcInstant.ToString("yyyy-MM-ddTHH:mm:ssZ");

        var c2Output = JsonSerializer.Serialize(new
        {
            status = "completed",
            isCompleteSnapshot = true,
            candidateGroups = Array.Empty<object>(),
            separateHandling = new[]
            {
                new
                {
                    needReference = new { needId = reportId, targetType = "Report", collectionReason = "VerifiedReport" },
                    proposedSchedule = new
                    {
                        scheduledAt = timeString,
                        schedulingReason = "Future scheduled time verification."
                    }
                }
            },
            deferredNeeds = Array.Empty<object>()
        });

        var (workflowId, _, managerId, _, _) = SeedWorkflowWithApprovedC2Plan(
            db, c2Output, version: 1, explicitReportId: reportId);

        var service = CreateServiceWithRealTaskService(db);
        await service.ExecuteCollectionPlanAsync(workflowId, new ExecuteCollectionPlanRequest { ExpectedVersion = 1 }, managerId);

        var task = await db.CollectionTasks.FirstAsync(t => t.WasteReportId == reportId);
        task.ScheduledAt.Should().BeCloseTo(exactUtcInstant, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task ExecuteCollectionPlanAsync_AuthoritativeTargetId_ResolvesFromAuthoritativeBackend()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateContext(dbName);

        var reportId = Guid.NewGuid();
        var futureTime = NextValidUtc().ToString("yyyy-MM-ddTHH:mm:ssZ");

        // C2 JSON contains ONLY needId, no targetId, latitude, longitude, or volume
        var c2Output = JsonSerializer.Serialize(new
        {
            status = "completed",
            isCompleteSnapshot = true,
            candidateGroups = new[]
            {
                new
                {
                    groupId = "group-1",
                    attentionOrder = 1,
                    proposedSchedule = new { scheduledAt = futureTime, schedulingReason = "Plan resolution" },
                    needReferences = new[]
                    {
                        new { needId = reportId, targetType = "Report", collectionReason = "VerifiedReport" }
                    }
                }
            },
            separateHandling = Array.Empty<object>(),
            deferredNeeds = Array.Empty<object>()
        });

        var (workflowId, _, managerId, _, _) = SeedWorkflowWithApprovedC2Plan(
            db, c2Output, version: 1, explicitReportId: reportId);

        var service = CreateServiceWithRealTaskService(db);
        await service.ExecuteCollectionPlanAsync(workflowId, new ExecuteCollectionPlanRequest { ExpectedVersion = 1 }, managerId);

        var task = await db.CollectionTasks.FirstAsync();
        task.WasteReportId.Should().Be(reportId);
        task.WasteBinId.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteCollectionPlanAsync_DuplicateNeedInCandidateGroups_ThrowsConflict()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateContext(dbName);

        var duplicateNeedId = Guid.NewGuid();
        var futureTime = NextValidUtc().ToString("yyyy-MM-ddTHH:mm:ssZ");

        var c2Output = JsonSerializer.Serialize(new
        {
            status = "completed",
            isCompleteSnapshot = true,
            candidateGroups = new[]
            {
                new
                {
                    groupId = "group-1",
                    attentionOrder = 1,
                    proposedSchedule = new { scheduledAt = futureTime, schedulingReason = "Group 1 reason" },
                    needReferences = new[]
                    {
                        new { needId = duplicateNeedId, targetType = "Report", collectionReason = "VerifiedReport" }
                    }
                },
                new
                {
                    groupId = "group-2",
                    attentionOrder = 2,
                    proposedSchedule = new { scheduledAt = futureTime, schedulingReason = "Group 2 reason" },
                    needReferences = new[]
                    {
                        new { needId = duplicateNeedId, targetType = "Report", collectionReason = "VerifiedReport" }
                    }
                }
            },
            separateHandling = Array.Empty<object>(),
            deferredNeeds = Array.Empty<object>()
        });

        var (workflowId, _, managerId, _, _) = SeedWorkflowWithApprovedC2Plan(
            db, c2Output, version: 1, explicitReportId: duplicateNeedId);
        var service = CreateServiceWithRealTaskService(db);

        var act = () => service.ExecuteCollectionPlanAsync(workflowId, new ExecuteCollectionPlanRequest { ExpectedVersion = 1 }, managerId);

        await act.Should().ThrowAsync<BusinessRuleConflictException>()
            .WithMessage("*Duplicate collection need reference detected*");
    }

    [Fact]
    public async Task ExecuteCollectionPlanAsync_ExecutableAndDeferredOverlap_ThrowsConflict()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateContext(dbName);

        var overlappingId = Guid.NewGuid();
        var futureTime = NextValidUtc().ToString("yyyy-MM-ddTHH:mm:ssZ");

        var c2Output = JsonSerializer.Serialize(new
        {
            status = "completed",
            isCompleteSnapshot = true,
            candidateGroups = new[]
            {
                new
                {
                    groupId = "group-1",
                    attentionOrder = 1,
                    proposedSchedule = new { scheduledAt = futureTime, schedulingReason = "Group 1 reason" },
                    needReferences = new[]
                    {
                        new { needId = overlappingId, targetType = "Report", collectionReason = "VerifiedReport" }
                    }
                }
            },
            separateHandling = Array.Empty<object>(),
            deferredNeeds = new[]
            {
                new
                {
                    needReference = new { needId = overlappingId, targetType = "Report", collectionReason = "VerifiedReport" }
                }
            }
        });

        var (workflowId, _, managerId, _, _) = SeedWorkflowWithApprovedC2Plan(
            db, c2Output, version: 1, explicitReportId: overlappingId);
        var service = CreateServiceWithRealTaskService(db);

        var act = () => service.ExecuteCollectionPlanAsync(workflowId, new ExecuteCollectionPlanRequest { ExpectedVersion = 1 }, managerId);

        await act.Should().ThrowAsync<BusinessRuleConflictException>()
            .WithMessage("*cannot appear as both executable and deferred*");
    }

    [Fact]
    public async Task ExecuteCollectionPlanAsync_MissingProposedSchedule_ThrowsConflict()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateContext(dbName);

        var reportId = Guid.NewGuid();

        var c2Output = JsonSerializer.Serialize(new
        {
            status = "completed",
            isCompleteSnapshot = true,
            candidateGroups = new[]
            {
                new
                {
                    groupId = "group-1",
                    attentionOrder = 1,
                    proposedSchedule = (object?)null, // Missing schedule!
                    needReferences = new[]
                    {
                        new { needId = reportId, targetType = "Report", collectionReason = "VerifiedReport" }
                    }
                }
            },
            separateHandling = Array.Empty<object>(),
            deferredNeeds = Array.Empty<object>()
        });

        var (workflowId, _, managerId, _, _) = SeedWorkflowWithApprovedC2Plan(
            db, c2Output, version: 1, explicitReportId: reportId);
        var service = CreateServiceWithRealTaskService(db);

        var act = () => service.ExecuteCollectionPlanAsync(workflowId, new ExecuteCollectionPlanRequest { ExpectedVersion = 1 }, managerId);

        await act.Should().ThrowAsync<BusinessRuleConflictException>()
            .WithMessage("*missing a proposed schedule*");
    }

    [Fact]
    public async Task ExecuteCollectionPlanAsync_MalformedScheduledAt_ThrowsConflict()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateContext(dbName);

        var reportId = Guid.NewGuid();

        var c2Output = JsonSerializer.Serialize(new
        {
            status = "completed",
            isCompleteSnapshot = true,
            candidateGroups = new[]
            {
                new
                {
                    groupId = "group-1",
                    attentionOrder = 1,
                    proposedSchedule = new
                    {
                        scheduledAt = "not-a-valid-date-time",
                        schedulingReason = "Valid reason here."
                    },
                    needReferences = new[]
                    {
                        new { needId = reportId, targetType = "Report", collectionReason = "VerifiedReport" }
                    }
                }
            },
            separateHandling = Array.Empty<object>(),
            deferredNeeds = Array.Empty<object>()
        });

        var (workflowId, _, managerId, _, _) = SeedWorkflowWithApprovedC2Plan(
            db, c2Output, version: 1, explicitReportId: reportId);
        var service = CreateServiceWithRealTaskService(db);

        var act = () => service.ExecuteCollectionPlanAsync(workflowId, new ExecuteCollectionPlanRequest { ExpectedVersion = 1 }, managerId);

        await act.Should().ThrowAsync<BusinessRuleConflictException>();
    }

    [Fact]
    public async Task ExecuteCollectionPlanAsync_MissingSchedulingReason_ThrowsConflict()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateContext(dbName);

        var reportId = Guid.NewGuid();
        var futureTime = NextValidUtc().ToString("yyyy-MM-ddTHH:mm:ssZ");

        var c2Output = JsonSerializer.Serialize(new
        {
            status = "completed",
            isCompleteSnapshot = true,
            candidateGroups = new[]
            {
                new
                {
                    groupId = "group-1",
                    attentionOrder = 1,
                    proposedSchedule = new
                    {
                        scheduledAt = futureTime,
                        schedulingReason = "   " // Blank reason!
                    },
                    needReferences = new[]
                    {
                        new { needId = reportId, targetType = "Report", collectionReason = "VerifiedReport" }
                    }
                }
            },
            separateHandling = Array.Empty<object>(),
            deferredNeeds = Array.Empty<object>()
        });

        var (workflowId, _, managerId, _, _) = SeedWorkflowWithApprovedC2Plan(
            db, c2Output, version: 1, explicitReportId: reportId);
        var service = CreateServiceWithRealTaskService(db);

        var act = () => service.ExecuteCollectionPlanAsync(workflowId, new ExecuteCollectionPlanRequest { ExpectedVersion = 1 }, managerId);

        await act.Should().ThrowAsync<BusinessRuleConflictException>()
            .WithMessage("*missing a valid scheduling reason*");
    }

    [Fact]
    public async Task ExecuteCollectionPlanAsync_WrongLegacySchema_ThrowsConflict()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateContext(dbName);

        var reportId = Guid.NewGuid();

        // Obsolete/invented schema
        var legacyJson = JsonSerializer.Serialize(new
        {
            high_priority_reports = new[]
            {
                new { targetId = reportId, scheduledStartTime = "2026-09-30T08:00:00Z" }
            },
            overflowing_bins = Array.Empty<object>()
        });

        var (workflowId, _, managerId, _, _) = SeedWorkflowWithApprovedC2Plan(
            db, legacyJson, version: 1, explicitReportId: reportId);
        var service = CreateServiceWithRealTaskService(db);

        var act = () => service.ExecuteCollectionPlanAsync(workflowId, new ExecuteCollectionPlanRequest { ExpectedVersion = 1 }, managerId);

        await act.Should().ThrowAsync<BusinessRuleConflictException>();
    }

    [Fact]
    public async Task ExecuteCollectionPlanAsync_ApprovalStepId_ExecutesApprovedStepNotNewerStep()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateContext(dbName);

        var managerId = Guid.NewGuid();
        db.Users.Add(new AppUser { Id = managerId, UserName = "manager@smartwaste.lk", Email = "manager@smartwaste.lk", FullName = "Manager", CreatedAt = DateTime.UtcNow });

        var reportA = Guid.NewGuid();
        var reportB = Guid.NewGuid();

        db.WasteReports.Add(new WasteReport { Id = reportA, CitizenId = Guid.NewGuid(), Description = "Report A", Latitude = 6.9, Longitude = 79.8, Status = WasteReportStatus.Verified, CreatedAt = DateTime.UtcNow.AddHours(-5), VerifiedAt = DateTime.UtcNow.AddHours(-1) });
        db.WasteReports.Add(new WasteReport { Id = reportB, CitizenId = Guid.NewGuid(), Description = "Report B", Latitude = 6.9, Longitude = 79.8, Status = WasteReportStatus.Verified, CreatedAt = DateTime.UtcNow.AddHours(-5), VerifiedAt = DateTime.UtcNow.AddHours(-1) });

        var futureTime = NextValidUtc().ToString("yyyy-MM-ddTHH:mm:ssZ");

        var c2OutputStepA = JsonSerializer.Serialize(new
        {
            status = "completed",
            isCompleteSnapshot = true,
            candidateGroups = Array.Empty<object>(),
            separateHandling = new[]
            {
                new { needReference = new { needId = reportA, targetType = "Report", collectionReason = "VerifiedReport" }, proposedSchedule = new { scheduledAt = futureTime, schedulingReason = "Step A schedule" } }
            },
            deferredNeeds = Array.Empty<object>()
        });

        var c2OutputStepB = JsonSerializer.Serialize(new
        {
            status = "completed",
            isCompleteSnapshot = true,
            candidateGroups = Array.Empty<object>(),
            separateHandling = new[]
            {
                new { needReference = new { needId = reportB, targetType = "Report", collectionReason = "VerifiedReport" }, proposedSchedule = new { scheduledAt = futureTime, schedulingReason = "Step B schedule" } }
            },
            deferredNeeds = Array.Empty<object>()
        });

        var workflow = new AgentWorkflow
        {
            Id = Guid.NewGuid(),
            Objective = "Multi-step test",
            Status = AgentWorkflowStatus.CollectionApproved,
            CurrentStep = WorkflowStepType.CollectionPlanning,
            InitiatedByUserId = managerId,
            Version = 3,
            CreatedAt = DateTime.UtcNow.AddHours(-3)
        };
        db.AgentWorkflows.Add(workflow);

        var stepA = new AgentWorkflowStep
        {
            Id = Guid.NewGuid(),
            WorkflowId = workflow.Id,
            Sequence = 1,
            StepType = WorkflowStepType.CollectionPlanning,
            Status = WorkflowStepStatus.Completed,
            OutputJson = c2OutputStepA,
            CompletedAt = DateTime.UtcNow.AddHours(-2)
        };
        db.AgentWorkflowSteps.Add(stepA);

        var stepB = new AgentWorkflowStep
        {
            Id = Guid.NewGuid(),
            WorkflowId = workflow.Id,
            Sequence = 2,
            StepType = WorkflowStepType.CollectionPlanning,
            Status = WorkflowStepStatus.Completed,
            OutputJson = c2OutputStepB,
            CompletedAt = DateTime.UtcNow.AddHours(-1)
        };
        db.AgentWorkflowSteps.Add(stepB);

        // Approval points explicitly to Step A
        db.AgentWorkflowApprovals.Add(new AgentWorkflowApproval
        {
            Id = Guid.NewGuid(),
            WorkflowId = workflow.Id,
            WorkflowStepId = stepA.Id,
            ApprovalStage = WorkflowApprovalStage.CollectionPlanning,
            Decision = WorkflowApprovalDecision.Approved,
            DecidedByUserId = managerId,
            DecidedAt = DateTime.UtcNow.AddHours(-1)
        });

        db.SaveChanges();

        var service = CreateServiceWithRealTaskService(db);
        await service.ExecuteCollectionPlanAsync(workflow.Id, new ExecuteCollectionPlanRequest { ExpectedVersion = 3 }, managerId);

        // Task must be created for Report A (from Step A), NOT Report B!
        var tasks = await db.CollectionTasks.ToListAsync();
        tasks.Should().ContainSingle();
        tasks[0].WasteReportId.Should().Be(reportA);
        tasks[0].SchedulingReason.Should().Be("Step A schedule");
    }

    [Fact]
    public async Task ExecuteCollectionPlanAsync_ApprovalMissingStepId_ThrowsConflict()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateContext(dbName);

        var reportId = Guid.NewGuid();
        var futureTime = NextValidUtc().ToString("yyyy-MM-ddTHH:mm:ssZ");

        var c2Output = JsonSerializer.Serialize(new
        {
            status = "completed",
            isCompleteSnapshot = true,
            candidateGroups = Array.Empty<object>(),
            separateHandling = new[]
            {
                new { needReference = new { needId = reportId, targetType = "Report", collectionReason = "VerifiedReport" }, proposedSchedule = new { scheduledAt = futureTime, schedulingReason = "Reason" } }
            },
            deferredNeeds = Array.Empty<object>()
        });

        var (workflowId, _, managerId, _, _) = SeedWorkflowWithApprovedC2Plan(
            db, c2Output, version: 1, explicitReportId: reportId);

        // Set WorkflowStepId to null on the approval
        var approval = await db.AgentWorkflowApprovals.FirstAsync();
        approval.WorkflowStepId = null;
        await db.SaveChangesAsync();

        var service = CreateServiceWithRealTaskService(db);

        var act = () => service.ExecuteCollectionPlanAsync(workflowId, new ExecuteCollectionPlanRequest { ExpectedVersion = 1 }, managerId);

        await act.Should().ThrowAsync<BusinessRuleConflictException>()
            .WithMessage("*not bound to a specific collection planning workflow step*");
    }

    [Fact]
    public async Task ExecuteCollectionPlanAsync_ServiceRuleReuse_UpdatesReportAndAppendsHistories()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateContext(dbName);

        var reportId = Guid.NewGuid();
        var futureTime = NextValidUtc().ToString("yyyy-MM-ddTHH:mm:ssZ");

        var c2Output = JsonSerializer.Serialize(new
        {
            status = "completed",
            isCompleteSnapshot = true,
            candidateGroups = Array.Empty<object>(),
            separateHandling = new[]
            {
                new { needReference = new { needId = reportId, targetType = "Report", collectionReason = "VerifiedReport" }, proposedSchedule = new { scheduledAt = futureTime, schedulingReason = "Audit reason" } }
            },
            deferredNeeds = Array.Empty<object>()
        });

        var (workflowId, _, managerId, _, _) = SeedWorkflowWithApprovedC2Plan(
            db, c2Output, version: 1, explicitReportId: reportId);

        var service = CreateServiceWithRealTaskService(db);
        await service.ExecuteCollectionPlanAsync(workflowId, new ExecuteCollectionPlanRequest { ExpectedVersion = 1 }, managerId);

        var report = await db.WasteReports.FindAsync(reportId);
        report!.Status.Should().Be(WasteReportStatus.Scheduled);

        var reportHistories = await db.WasteReportStatusHistories.Where(h => h.WasteReportId == reportId).ToListAsync();
        reportHistories.Should().Contain(h => h.ToStatus == WasteReportStatus.Scheduled);

        var task = await db.CollectionTasks.FirstAsync(t => t.WasteReportId == reportId);
        var taskHistories = await db.CollectionTaskStatusHistories.Where(h => h.CollectionTaskId == task.Id).ToListAsync();
        taskHistories.Should().Contain(h => h.Notes == "Task created from approved AI collection plan");
    }
}
