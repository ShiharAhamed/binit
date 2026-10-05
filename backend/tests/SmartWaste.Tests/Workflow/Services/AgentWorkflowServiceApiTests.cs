using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SmartWaste.Application.Common.Exceptions;
using SmartWaste.Application.Workflow.DTOs.Requests;
using SmartWaste.Application.Workflow.Services;
using SmartWaste.Domain.Common;
using SmartWaste.Domain.Collection.Entities;
using SmartWaste.Domain.Entities;
using SmartWaste.Domain.Workflow.Entities;
using SmartWaste.Domain.Workflow.Enums;
using SmartWaste.Infrastructure.Persistence;
using SmartWaste.Infrastructure.Workflow.Services;
using Xunit;

namespace SmartWaste.Tests.Workflow.Services;

public class AgentWorkflowServiceApiTests
{
    private static AppDbContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new AppDbContext(options);
    }

    private static AgentWorkflowService CreateService(AppDbContext db)
    {
        var stateMachine = new AgentWorkflowStateMachine();
        var logger = NullLogger<AgentWorkflowService>.Instance;
        return new AgentWorkflowService(db, stateMachine, logger);
    }

    private static void SeedUser(AppDbContext db, Guid userId, string email = "staff@smartwaste.lk")
    {
        if (db.Users.Any(u => u.Id == userId)) return;
        var user = new AppUser
        {
            Id = userId,
            Email = email,
            UserName = email,
            FullName = "Staff User",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };
        db.Users.Add(user);
        db.SaveChanges();
    }

    // =========================================================================
    // 1. GetWorkflowsAsync: Row-Level Visibility and Pagination
    // =========================================================================

    [Fact]
    public async Task GetWorkflowsAsync_WasteOfficer_SeesAllWorkflowsAcrossInitiators()
    {
        var dbName = Guid.NewGuid().ToString("N");
        using var db = CreateContext(dbName);
        var sut = CreateService(db);

        var officerA = Guid.NewGuid();
        var officerB = Guid.NewGuid();
        SeedUser(db, officerA, "officerA@smartwaste.lk");
        SeedUser(db, officerB, "officerB@smartwaste.lk");

        await sut.CreateWorkflowAsync("Officer A Objective 1", officerA);
        await sut.CreateWorkflowAsync("Officer A Objective 2", officerA);
        await sut.CreateWorkflowAsync("Officer B Objective 1", officerB);

        var query = new AgentWorkflowListQuery { Page = 1, PageSize = 10 };
        var result = await sut.GetWorkflowsAsync(query, officerA, AppRoles.WasteOfficer);

        result.TotalCount.Should().Be(3);
        result.Items.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetWorkflowsAsync_MunicipalManager_SeesAllWorkflowsAcrossInitiators()
    {
        var dbName = Guid.NewGuid().ToString("N");
        using var db = CreateContext(dbName);
        var sut = CreateService(db);

        var officerA = Guid.NewGuid();
        var officerB = Guid.NewGuid();
        var manager = Guid.NewGuid();
        SeedUser(db, officerA);
        SeedUser(db, officerB);
        SeedUser(db, manager);

        await sut.CreateWorkflowAsync("Workflow 1", officerA);
        await sut.CreateWorkflowAsync("Workflow 2", officerB);

        var query = new AgentWorkflowListQuery { Page = 1, PageSize = 10 };
        var result = await sut.GetWorkflowsAsync(query, manager, AppRoles.MunicipalManager);

        result.TotalCount.Should().Be(2);
        result.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task WorkflowReadDtos_ExposeTriggerMetadataAndDynamicReportReference()
    {
        using var db = CreateContext(Guid.NewGuid().ToString("N"));
        var sut = CreateService(db);
        var staffId = Guid.NewGuid();
        SeedUser(db, staffId);
        var manual = await sut.CreateWorkflowAsync("Manual collection planning objective", staffId);
        var reportId = Guid.Parse("e4b9a172-2211-4ccd-8855-112233445566");
        var citizen = new AgentWorkflow
        {
            Objective = "Analyze one submitted waste report",
            InitiatedByUserId = staffId,
            TriggerType = AgentWorkflowTriggerType.CitizenReportSubmission,
            TriggeringWasteReportId = reportId
        };
        citizen.EnsureValidTrigger();
        db.AgentWorkflows.Add(citizen);
        await db.SaveChangesAsync();

        var list = await sut.GetWorkflowsAsync(new AgentWorkflowListQuery { Page = 1, PageSize = 10 },
            staffId, AppRoles.WasteOfficer);
        var manualSummary = list.Items.Single(item => item.Id == manual.Id);
        manualSummary.TriggerType.Should().Be(AgentWorkflowTriggerType.ManualOperationalPlanning);
        manualSummary.TriggeringWasteReportId.Should().BeNull();
        manualSummary.ReportReference.Should().BeNull();
        var citizenSummary = list.Items.Single(item => item.Id == citizen.Id);
        citizenSummary.TriggeringWasteReportId.Should().Be(reportId);
        citizenSummary.ReportReference.Should().Be("E4B9A172");

        var manualDetail = await sut.GetWorkflowDetailsAsync(manual.Id, staffId, AppRoles.WasteOfficer);
        manualDetail.TriggerType.Should().Be(AgentWorkflowTriggerType.ManualOperationalPlanning);
        manualDetail.ReportReference.Should().BeNull();
        var citizenDetail = await sut.GetWorkflowDetailsAsync(citizen.Id, staffId, AppRoles.WasteOfficer);
        citizenDetail.TriggerType.Should().Be(AgentWorkflowTriggerType.CitizenReportSubmission);
        citizenDetail.TriggeringWasteReportId.Should().Be(reportId);
        citizenDetail.ReportReference.Should().Be("E4B9A172");
    }

    [Fact]
    public async Task GetWorkflowsAsync_UnauthorizedRole_ThrowsForbiddenException()
    {
        var dbName = Guid.NewGuid().ToString("N");
        using var db = CreateContext(dbName);
        var sut = CreateService(db);
        var citizenId = Guid.NewGuid();

        var query = new AgentWorkflowListQuery { Page = 1, PageSize = 10 };
        var act = () => sut.GetWorkflowsAsync(query, citizenId, AppRoles.Citizen);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task GetWorkflowsAsync_StatusFilter_ReturnsMatchingOnly()
    {
        var dbName = Guid.NewGuid().ToString("N");
        using var db = CreateContext(dbName);
        var sut = CreateService(db);
        var managerId = Guid.NewGuid();

        var wf1 = await sut.CreateWorkflowAsync("Workflow 1", managerId);
        var wf2 = await sut.CreateWorkflowAsync("Workflow 2", managerId);
        await sut.TransitionAsync(wf2.Id, AgentWorkflowStatus.Planning);

        var query = new AgentWorkflowListQuery { Page = 1, PageSize = 10, Status = "Planning" };
        var result = await sut.GetWorkflowsAsync(query, managerId, AppRoles.MunicipalManager);

        result.TotalCount.Should().Be(1);
        result.Items.Single().Id.Should().Be(wf2.Id);
        result.Items.Single().Status.Should().Be(AgentWorkflowStatus.Planning);
    }

    // =========================================================================
    // 2. GetWorkflowDetailsAsync: Row-Level Access & Structured JSON
    // =========================================================================

    [Fact]
    public async Task GetWorkflowDetailsAsync_WasteOfficer_CanAccessOthersWorkflow()
    {
        var dbName = Guid.NewGuid().ToString("N");
        using var db = CreateContext(dbName);
        var sut = CreateService(db);

        var ownerOfficer = Guid.NewGuid();
        var strangerOfficer = Guid.NewGuid();
        SeedUser(db, ownerOfficer);
        SeedUser(db, strangerOfficer);

        var wf = await sut.CreateWorkflowAsync("Owner workflow", ownerOfficer);

        var result = await sut.GetWorkflowDetailsAsync(wf.Id, strangerOfficer, AppRoles.WasteOfficer);
        result.Should().NotBeNull();
        result.Id.Should().Be(wf.Id);
    }

    [Fact]
    public async Task GetWorkflowDetailsAsync_AddsStoredBinCodesWithoutChangingC2Identifiers()
    {
        using var db = CreateContext(Guid.NewGuid().ToString("N"));
        var sut = CreateService(db);
        var officerId = Guid.NewGuid();
        SeedUser(db, officerId);
        var bin = new WasteBin { Id = Guid.NewGuid(), BinCode = "BIN-COL-0042" };
        db.WasteBins.Add(bin);
        await db.SaveChangesAsync();

        var workflow = await sut.CreateWorkflowAsync("Review bin needs", officerId);
        var step = await sut.AddStepAsync(workflow.Id, WorkflowStepType.CollectionPlanning, "c2_agent");
        await sut.CompleteStepAsync(step.Id, JsonSerializer.Serialize(new
        {
            candidateGroups = new[] { new { needReferences = new[] { new { targetType = "Bin", needId = bin.Id } } } },
            separateHandling = Array.Empty<object>(),
            deferredNeeds = Array.Empty<object>()
        }));

        var detail = await sut.GetWorkflowDetailsAsync(workflow.Id, officerId, AppRoles.WasteOfficer);
        detail.BinCodes.Should().ContainKey(bin.Id).WhoseValue.Should().Be(bin.BinCode);
        detail.Steps.Single(s => s.Id == step.Id).Output!.Value
            .GetProperty("candidateGroups")[0].GetProperty("needReferences")[0]
            .GetProperty("needId").GetGuid().Should().Be(bin.Id);
    }

    [Fact]
    public async Task GetWorkflowDetailsAsync_UnauthorizedRole_ThrowsForbiddenException()
    {
        var dbName = Guid.NewGuid().ToString("N");
        using var db = CreateContext(dbName);
        var sut = CreateService(db);

        var ownerOfficer = Guid.NewGuid();
        var citizenId = Guid.NewGuid();
        SeedUser(db, ownerOfficer);
        SeedUser(db, citizenId);

        var wf = await sut.CreateWorkflowAsync("Owner workflow", ownerOfficer);

        var act = () => sut.GetWorkflowDetailsAsync(wf.Id, citizenId, AppRoles.Citizen);
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task GetWorkflowDetailsAsync_StepsOrderedBySequence_RegardlessOfStartedAt()
    {
        var dbName = Guid.NewGuid().ToString("N");
        using var db = CreateContext(dbName);
        var sut = CreateService(db);
        var userId = Guid.NewGuid();
        SeedUser(db, userId);

        var wf = await sut.CreateWorkflowAsync("Step sequence order test", userId);

        // Intentionally create Step 2 with earlier StartedAt timestamp
        var step2 = await sut.AddStepAsync(wf.Id, WorkflowStepType.FleetPlanning, "c3_agent");
        step2.StartedAt = DateTime.UtcNow.AddHours(-10);
        step2.Sequence = 2;

        // Intentionally create Step 1 with later StartedAt timestamp
        var step1 = await sut.AddStepAsync(wf.Id, WorkflowStepType.CollectionPlanning, "c2_agent");
        step1.StartedAt = DateTime.UtcNow;
        step1.Sequence = 1;

        await db.SaveChangesAsync();

        var detail = await sut.GetWorkflowDetailsAsync(wf.Id, userId, AppRoles.WasteOfficer);

        detail.Steps.Should().HaveCount(2);
        detail.Steps[0].Sequence.Should().Be(1);
        detail.Steps[0].StepType.Should().Be(WorkflowStepType.CollectionPlanning);
        detail.Steps[1].Sequence.Should().Be(2);
        detail.Steps[1].StepType.Should().Be(WorkflowStepType.FleetPlanning);
    }

    [Fact]
    public async Task GetWorkflowDetailsAsync_ReturnsOrderedCollectionsAndParsedNativeJson()
    {
        var dbName = Guid.NewGuid().ToString("N");
        using var db = CreateContext(dbName);
        var sut = CreateService(db);
        var userId = Guid.NewGuid();
        SeedUser(db, userId);

        var wf = await sut.CreateWorkflowAsync("Structured test workflow", userId);

        // Add steps with structured JSON
        var step1 = await sut.AddStepAsync(wf.Id, WorkflowStepType.CollectionPlanning, "c2_agent",
            inputJson: "{\"zone\":\"North\"}");
        await sut.CompleteStepAsync(step1.Id,
            outputJson: "{\"status\":\"completed\",\"advisoryOnly\":true,\"candidateGroups\":[{\"id\":1}]}",
            validationJson: "{\"valid\":true}");

        var step2 = await sut.AddStepAsync(wf.Id, WorkflowStepType.FleetPlanning, "c3_agent");

        // Record approval with decision payload JSON
        await sut.RecordApprovalAsync(wf.Id, WorkflowApprovalStage.CollectionPlanning,
            WorkflowApprovalDecision.Approved, userId,
            reason: "Approved", payloadJson: "{\"adjustedUnits\":2}");

        // Record execution result with result JSON
        await sut.RecordExecutionResultAsync(wf.Id, WorkflowExecutionType.CollectionTaskCreation,
            WorkflowExecutionStatus.Succeeded, resultJson: "{\"createdCount\":5}");

        var detail = await sut.GetWorkflowDetailsAsync(wf.Id, userId, AppRoles.WasteOfficer);

        detail.Should().NotBeNull();
        detail.Id.Should().Be(wf.Id);
        detail.Steps.Should().HaveCount(2);
        detail.Steps[0].Sequence.Should().Be(1);
        detail.Steps[1].Sequence.Should().Be(2);

        // Verify native JSON parsing (JsonElement)
        detail.Steps[0].Output.Should().NotBeNull();
        detail.Steps[0].Output!.Value.GetProperty("status").GetString().Should().Be("completed");
        detail.Steps[0].Output!.Value.GetProperty("advisoryOnly").GetBoolean().Should().BeTrue();
        detail.Steps[0].Validation!.Value.GetProperty("valid").GetBoolean().Should().BeTrue();

        detail.Approvals.Should().HaveCount(1);
        detail.Approvals[0].DecisionPayload.Should().NotBeNull();
        detail.Approvals[0].DecisionPayload!.Value.GetProperty("adjustedUnits").GetInt32().Should().Be(2);

        detail.ExecutionResults.Should().HaveCount(1);
        detail.ExecutionResults[0].Result!.Value.GetProperty("createdCount").GetInt32().Should().Be(5);
    }

    [Fact]
    public async Task GetWorkflowDetailsAsync_UnknownId_ThrowsNotFoundException()
    {
        var dbName = Guid.NewGuid().ToString("N");
        using var db = CreateContext(dbName);
        var sut = CreateService(db);
        var managerId = Guid.NewGuid();

        var act = () => sut.GetWorkflowDetailsAsync(Guid.NewGuid(), managerId, AppRoles.MunicipalManager);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    // =========================================================================
    // 3. GetWorkflowHistoryAsync: Chronological Order
    // =========================================================================

    [Fact]
    public async Task GetWorkflowHistoryAsync_ReturnsChronologicalTransitions()
    {
        var dbName = Guid.NewGuid().ToString("N");
        using var db = CreateContext(dbName);
        var sut = CreateService(db);
        var userId = Guid.NewGuid();
        SeedUser(db, userId);

        var wf = await sut.CreateWorkflowAsync("History tracking", userId);
        await sut.TransitionAsync(wf.Id, AgentWorkflowStatus.Planning, "Starting planning", userId);
        await sut.TransitionAsync(wf.Id, AgentWorkflowStatus.AwaitingCollectionApproval, "Proposed groups", userId);

        var history = await sut.GetWorkflowHistoryAsync(wf.Id, userId, AppRoles.WasteOfficer);

        history.Should().HaveCount(3);
        history[0].FromStatus.Should().BeNull();
        history[0].ToStatus.Should().Be(AgentWorkflowStatus.Created);
        history[1].FromStatus.Should().Be(AgentWorkflowStatus.Created);
        history[1].ToStatus.Should().Be(AgentWorkflowStatus.Planning);
        history[2].FromStatus.Should().Be(AgentWorkflowStatus.Planning);
        history[2].ToStatus.Should().Be(AgentWorkflowStatus.AwaitingCollectionApproval);
    }

    // =========================================================================
    // 4. StartWorkflowAsync: Atomic State, Concurrency & Guard Rails
    // =========================================================================

    [Fact]
    public async Task StartWorkflowAsync_CreatedWorkflow_TransitionsToPlanningAndSharedPlanning()
    {
        var dbName = Guid.NewGuid().ToString("N");
        using var db = CreateContext(dbName);
        var sut = CreateService(db);
        var userId = Guid.NewGuid();
        SeedUser(db, userId);

        var wf = await sut.CreateWorkflowAsync("Initial workflow objective", userId);
        wf.Status.Should().Be(AgentWorkflowStatus.Created);
        wf.CurrentStep.Should().Be(WorkflowStepType.None);
        wf.Version.Should().Be(1);

        var started = await sut.StartWorkflowAsync(wf.Id, userId, AppRoles.WasteOfficer);

        started.Status.Should().Be(AgentWorkflowStatus.Planning);
        started.CurrentStep.Should().Be(WorkflowStepType.SharedPlanning);
        started.Version.Should().Be(2);

        // Verify history row
        var reloaded = await sut.GetWorkflowByIdAsync(wf.Id, includeDetails: true);
        reloaded!.Transitions.Should().HaveCount(2);
        var startTransition = reloaded.Transitions.Last();
        startTransition.FromStatus.Should().Be(AgentWorkflowStatus.Created);
        startTransition.ToStatus.Should().Be(AgentWorkflowStatus.Planning);
        startTransition.ChangedByUserId.Should().Be(userId);
    }

    [Fact]
    public async Task StartWorkflowAsync_AlreadyPlanning_ThrowsConflict_LeavesStateUnchanged()
    {
        var dbName = Guid.NewGuid().ToString("N");
        using var db = CreateContext(dbName);
        var sut = CreateService(db);
        var userId = Guid.NewGuid();
        SeedUser(db, userId);

        var wf = await sut.CreateWorkflowAsync("Double start workflow", userId);
        await sut.StartWorkflowAsync(wf.Id, userId, AppRoles.WasteOfficer);

        // Attempting to start again when already in Planning
        var act = () => sut.StartWorkflowAsync(wf.Id, userId, AppRoles.WasteOfficer);
        await act.Should().ThrowAsync<InvalidWorkflowTransitionException>();

        var reloaded = await sut.GetWorkflowByIdAsync(wf.Id, includeDetails: true);
        reloaded!.Status.Should().Be(AgentWorkflowStatus.Planning);
        reloaded.Version.Should().Be(2);
        reloaded.Transitions.Should().HaveCount(2); // Initial (1) + Start (1) = 2, no extra row
    }

    [Fact]
    public async Task StartWorkflowAsync_TerminalWorkflow_ThrowsConflict()
    {
        var dbName = Guid.NewGuid().ToString("N");
        using var db = CreateContext(dbName);
        var sut = CreateService(db);
        var userId = Guid.NewGuid();
        SeedUser(db, userId);

        var wf = await sut.CreateWorkflowAsync("Terminal workflow test", userId);
        await sut.TransitionAsync(wf.Id, AgentWorkflowStatus.Planning);
        await sut.TransitionAsync(wf.Id, AgentWorkflowStatus.Failed);

        var act = () => sut.StartWorkflowAsync(wf.Id, userId, AppRoles.WasteOfficer);
        await act.Should().ThrowAsync<InvalidWorkflowTransitionException>();
    }

    [Fact]
    public async Task StartWorkflowAsync_OtherOfficer_TransitionsToPlanning()
    {
        var dbName = Guid.NewGuid().ToString("N");
        using var db = CreateContext(dbName);
        var sut = CreateService(db);

        var ownerOfficer = Guid.NewGuid();
        var strangerOfficer = Guid.NewGuid();
        SeedUser(db, ownerOfficer);
        SeedUser(db, strangerOfficer);

        var wf = await sut.CreateWorkflowAsync("Owner workflow", ownerOfficer);

        var result = await sut.StartWorkflowAsync(wf.Id, strangerOfficer, AppRoles.WasteOfficer);
        result.Status.Should().Be(AgentWorkflowStatus.Planning);
    }

    [Fact]
    public async Task StartWorkflowAsync_UnauthorizedRole_ThrowsForbiddenException()
    {
        var dbName = Guid.NewGuid().ToString("N");
        using var db = CreateContext(dbName);
        var sut = CreateService(db);

        var ownerOfficer = Guid.NewGuid();
        var citizenId = Guid.NewGuid();
        SeedUser(db, ownerOfficer);
        SeedUser(db, citizenId);

        var wf = await sut.CreateWorkflowAsync("Owner workflow", ownerOfficer);

        var act = () => sut.StartWorkflowAsync(wf.Id, citizenId, AppRoles.Citizen);
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task StartWorkflowAsync_ConcurrentStarts_OneSucceeds_OneThrowsConflict()
    {
        var dbName = Guid.NewGuid().ToString("N");
        using var db = CreateContext(dbName);
        var sut = CreateService(db);
        var managerId = Guid.NewGuid();
        SeedUser(db, managerId);

        var wf = await sut.CreateWorkflowAsync("Concurrent start test", managerId);
        wf.Version.Should().Be(1);

        // Client A loads workflow when Version = 1
        using var clientADb = CreateContext(dbName);
        var clientAWf = await clientADb.AgentWorkflows.FirstAsync(w => w.Id == wf.Id);
        clientAWf.Version.Should().Be(1);

        // Client B starts workflow via service -> transitions to Planning, increments Version to 2
        var startedB = await sut.StartWorkflowAsync(wf.Id, managerId, AppRoles.MunicipalManager);
        startedB.Version.Should().Be(2);

        // Client A now tries to mutate using its stale Version=1 read
        clientAWf.Status = AgentWorkflowStatus.Planning;
        clientAWf.Version = 2; // attempts to bump from 1 to 2
        var act = () => clientADb.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }
}
