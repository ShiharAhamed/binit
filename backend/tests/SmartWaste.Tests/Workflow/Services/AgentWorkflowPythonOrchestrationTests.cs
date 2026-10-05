using System.Text.Json;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SmartWaste.Application.Collection.Interfaces;
using SmartWaste.Application.Common.Exceptions;
using SmartWaste.Application.Workflow.DTOs.Requests;
using SmartWaste.Application.Workflow.DTOs.Transport;
using SmartWaste.Application.Workflow.Interfaces;
using SmartWaste.Application.Workflow.Services;
using SmartWaste.Domain.Collection.Entities;
using SmartWaste.Domain.Collection.Enums;
using SmartWaste.Domain.Common;
using SmartWaste.Domain.Entities;
using SmartWaste.Domain.Reporting.Entities;
using SmartWaste.Domain.Reporting.Enums;
using SmartWaste.Domain.Workflow.Enums;
using SmartWaste.Infrastructure.Collection.Services;
using SmartWaste.Infrastructure.Persistence;
using SmartWaste.Infrastructure.Workflow.Services;

namespace SmartWaste.Tests.Workflow.Services;

public sealed class AgentWorkflowPythonOrchestrationTests
{
    private static DateTime NextValidUtc()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Colombo");
        var localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone)).AddDays(2);
        return TimeZoneInfo.ConvertTimeToUtc(localDate.ToDateTime(new TimeOnly(9, 0)), zone);
    }

    private sealed class FakePythonOrchestrationClient : IPythonOrchestrationClient
    {
        private readonly Func<PythonWorkflowStartRequest, PythonOrchestrationEnvelope> _start;
        private readonly Func<PythonWorkflowResumeRequest, PythonOrchestrationEnvelope> _resume;

        public FakePythonOrchestrationClient(
            Func<PythonWorkflowStartRequest, PythonOrchestrationEnvelope> start,
            Func<PythonWorkflowResumeRequest, PythonOrchestrationEnvelope> resume)
        {
            _start = start;
            _resume = resume;
        }

        public int StartCalls { get; private set; }
        public int ResumeCalls { get; private set; }
        public PythonWorkflowResumeRequest? LastResumeRequest { get; private set; }

        public Task<PythonOrchestrationEnvelope> StartAsync(PythonWorkflowStartRequest request, CancellationToken cancellationToken = default)
        {
            StartCalls++;
            return Task.FromResult(_start(request));
        }

        public Task<PythonOrchestrationEnvelope> ResumeAfterCollectionApprovalAsync(PythonWorkflowResumeRequest request, CancellationToken cancellationToken = default)
        {
            ResumeCalls++;
            LastResumeRequest = request;
            return Task.FromResult(_resume(request));
        }

        public Task<PythonOrchestrationEnvelope> ResumeAfterReportVerificationAsync(PythonWorkflowResumeRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Report verification continuation is exercised by separate foundation tests.");
    }

    private static AppDbContext CreateContext(string name) => new(
        new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(name).Options);

    private static JsonElement Json(string value)
    {
        using var document = JsonDocument.Parse(value);
        return document.RootElement.Clone();
    }

    private static AgentWorkflowService CreateService(AppDbContext db, IPythonOrchestrationClient python)
    {
        ICollectionTaskService tasks = new CollectionTaskService(db);
        return new AgentWorkflowService(
            db,
            new AgentWorkflowStateMachine(),
            NullLogger<AgentWorkflowService>.Instance,
            tasks,
            pythonOrchestrationClient: python);
    }

    [Theory]
    [InlineData("PausedForDispatchApproval", "Paused", "FleetDispatch", "ReadyForHumanReview", AgentWorkflowStatus.AwaitingDispatchApproval)]
    [InlineData("DispatchNeedsRevision", "Running", "None", "NeedsRevision", AgentWorkflowStatus.DispatchNeedsRevision)]
    public async Task ApprovedCollectionExecution_ReconstructsSnapshot_AndPersistsC3C4WithMappedStatus(
        string finalPhase,
        string finalPythonStatus,
        string approvalStage,
        string validationOutcome,
        AgentWorkflowStatus expectedStatus)
    {
        using var db = CreateContext(Guid.NewGuid().ToString("N"));
        var managerId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        SeedAuthoritativeCollectionData(db, managerId, reportId);

        var python = new FakePythonOrchestrationClient(
            start => FirstHalf(start.WorkflowId, start.Objective, reportId),
            resume => SecondHalf(resume.Workflow.WorkflowId!.Value, resume.Workflow.Objective!, finalPhase, finalPythonStatus, approvalStage, validationOutcome));
        var service = CreateService(db, python);
        var created = await service.CreateWorkflowAsync("Coordinate the verified waste report", managerId);

        var started = await service.StartWorkflowAsync(created.Id, managerId, AppRoles.WasteOfficer);
        started.Status.Should().Be(AgentWorkflowStatus.AwaitingCollectionApproval);
        started.Version.Should().Be(3);
        started.CurrentStep.Should().Be(WorkflowStepType.CollectionPlanning);

        var approved = await service.ApproveCollectionPlanningAsync(created.Id, new ApproveCollectionPlanningRequest
        {
            ExpectedVersion = started.Version,
            Reason = "Collection plan is approved."
        }, managerId);

        var completed = await service.ExecuteCollectionPlanAsync(created.Id, new ExecuteCollectionPlanRequest
        {
            ExpectedVersion = approved.Version
        }, managerId);

        completed.Status.Should().Be(expectedStatus);
        python.StartCalls.Should().Be(1);
        python.ResumeCalls.Should().Be(1);
        python.LastResumeRequest!.Workflow.CurrentPhase.Should().Be("PausedForCollectionApproval");
        python.LastResumeRequest.ResumeContext.AuthoritativeExecutionSummary.GetProperty("createdTaskCount").GetInt32().Should().Be(1);
        (await db.CollectionTasks.CountAsync()).Should().Be(1, "scheduled tasks are committed before the resume request");

        // JsonContent.Create uses the same HTTP serializer as PythonOrchestrationClient.
        using var wireContent = JsonContent.Create(python.LastResumeRequest);
        using var wireJson = JsonDocument.Parse(await wireContent.ReadAsStringAsync());
        var wireWorkflow = wireJson.RootElement.GetProperty("workflow");
        wireWorkflow.GetProperty("workflowId").GetGuid().Should().Be(created.Id);
        wireWorkflow.GetProperty("status").GetString().Should().Be("Paused");
        wireWorkflow.GetProperty("currentPhase").GetString().Should().Be("PausedForCollectionApproval");
        wireWorkflow.GetProperty("approvalStage").GetString().Should().Be("CollectionPlanning");
        wireWorkflow.GetProperty("completedSpecialists").EnumerateArray().Select(x => x.GetString())
            .Should().Equal("WasteAnalysis", "CollectionPlanning");
        wireWorkflow.GetProperty("plannerResult").ValueKind.Should().Be(JsonValueKind.Object);
        wireWorkflow.GetProperty("wasteAnalysisResult").ValueKind.Should().Be(JsonValueKind.Object);
        wireWorkflow.GetProperty("collectionPlanningResult").ValueKind.Should().Be(JsonValueKind.Object);
        wireWorkflow.GetProperty("errors").GetArrayLength().Should().Be(0);
        wireWorkflow.GetProperty("warnings").GetArrayLength().Should().Be(0);
        var wireContext = wireJson.RootElement.GetProperty("resumeContext");
        wireContext.GetProperty("workflowId").GetGuid().Should().Be(created.Id);
        wireContext.GetProperty("approvalStage").GetString().Should().Be("CollectionPlanning");
        wireContext.GetProperty("decision").GetString().Should().Be("Approved");
        var wireSummary = wireContext.GetProperty("authoritativeExecutionSummary");
        wireSummary.GetProperty("createdTaskCount").GetInt32().Should().Be(1);
        wireSummary.GetProperty("sourceCollectionPlanningStepId").GetGuid()
            .Should().Be(completed.Steps.Single(s => s.StepType == WorkflowStepType.CollectionPlanning).Id);
        var wireTask = wireSummary.GetProperty("createdTasks")[0];
        wireTask.GetProperty("collectionTaskId").GetGuid().Should().Be((await db.CollectionTasks.SingleAsync()).Id);
        wireTask.GetProperty("taskCode").GetString().Should().NotBeNullOrWhiteSpace();
        wireTask.GetProperty("scheduledAt").GetDateTime().Should().BeAfter(DateTime.UtcNow);

        var c3 = completed.Steps.Single(s => s.StepType == WorkflowStepType.FleetPlanning);
        var c4 = completed.Steps.Single(s => s.StepType == WorkflowStepType.OperationalValidation);
        c3.Status.Should().Be(WorkflowStepStatus.Completed);
        c4.Status.Should().Be(WorkflowStepStatus.Completed);
        c4.Input!.Value.GetRawText().Should().Be(c3.Output!.Value.GetRawText(), "C4 is bound to canonical C3 output");
        completed.Transitions.Should().Contain(t => t.FromStatus == AgentWorkflowStatus.FleetPlanning && t.ToStatus == AgentWorkflowStatus.OperationalValidation);
        completed.Transitions.Should().Contain(t => t.FromStatus == AgentWorkflowStatus.OperationalValidation && t.ToStatus == expectedStatus);

        if (expectedStatus == AgentWorkflowStatus.AwaitingDispatchApproval)
        {
            var dispatchApproval = await service.ApproveDispatchPlanAsync(created.Id, new ApproveDispatchPlanRequest
            {
                ExpectedVersion = completed.Version,
                Reason = "Dispatch proposal is approved."
            }, managerId);
            dispatchApproval.Approvals.Single(a => a.ApprovalStage == WorkflowApprovalStage.FleetDispatch).WorkflowStepId.Should().Be(c4.Id);
        }

        var retry = async () => await service.ExecuteCollectionPlanAsync(created.Id, new ExecuteCollectionPlanRequest
        {
            ExpectedVersion = approved.Version
        }, managerId);
        await retry.Should().ThrowAsync<BusinessRuleConflictException>();
        python.ResumeCalls.Should().Be(1, "a retry after the workflow has advanced must not call Python again");
    }

    [Fact]
    public async Task StartWorkflowAsync_FailedPythonEnvelope_MarksWorkflowFailedWithoutRetry()
    {
        using var db = CreateContext(Guid.NewGuid().ToString("N"));
        var userId = Guid.NewGuid();
        db.Users.Add(new AppUser { Id = userId, UserName = "officer", Email = "officer@test.local", FullName = "Waste Officer", IsActive = true, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var python = new FakePythonOrchestrationClient(
            start => new PythonOrchestrationEnvelope { WorkflowId = start.WorkflowId, Objective = start.Objective, Status = "Failed", CurrentPhase = "Failed", Errors = Json("[{\"code\":\"PLANNER_FAILURE\"}]") },
            _ => throw new InvalidOperationException("Resume is not expected."));
        var service = CreateService(db, python);
        var workflow = await service.CreateWorkflowAsync("Coordinate failed planning request", userId);

        var result = await service.StartWorkflowAsync(workflow.Id, userId, AppRoles.WasteOfficer);

        result.Status.Should().Be(AgentWorkflowStatus.Failed);
        python.StartCalls.Should().Be(1);
        (await db.AgentWorkflowTransitions.CountAsync(t => t.WorkflowId == workflow.Id && t.ToStatus == AgentWorkflowStatus.Failed)).Should().Be(1);
        (await db.AgentWorkflowSteps.SingleAsync(s => s.WorkflowId == workflow.Id && s.Status == WorkflowStepStatus.Failed)).ValidationJson.Should().Contain("PLANNER_FAILURE");
    }

    [Fact]
    public async Task StartWorkflowAsync_TransportFailure_MarksWorkflowFailedAndRethrows()
    {
        using var db = CreateContext(Guid.NewGuid().ToString("N"));
        var userId = Guid.NewGuid();
        db.Users.Add(new AppUser { Id = userId, UserName = "officer2", Email = "officer2@test.local", FullName = "Waste Officer", IsActive = true, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var python = new FakePythonOrchestrationClient(
            _ => throw new AiServiceUnavailableException("AI service is unavailable."),
            _ => throw new InvalidOperationException("Resume is not expected."));
        var service = CreateService(db, python);
        var workflow = await service.CreateWorkflowAsync("Coordinate transport failure request", userId);

        var act = async () => await service.StartWorkflowAsync(workflow.Id, userId, AppRoles.WasteOfficer);

        await act.Should().ThrowAsync<AiServiceUnavailableException>();
        python.StartCalls.Should().Be(1);
        (await db.AgentWorkflows.FindAsync(workflow.Id))!.Status.Should().Be(AgentWorkflowStatus.Failed);
    }

    [Theory]
    [InlineData(true, false, "PausedForCollectionApproval")]
    [InlineData(false, true, "PausedForCollectionApproval")]
    [InlineData(false, false, "PausedForDispatchApproval")]
    public async Task StartWorkflowAsync_InvalidFirstHalfEnvelope_MarksWorkflowFailed(bool wrongWorkflowId, bool wrongObjective, string phase)
    {
        using var db = CreateContext(Guid.NewGuid().ToString("N"));
        var userId = Guid.NewGuid();
        db.Users.Add(new AppUser { Id = userId, UserName = "officer3", Email = "officer3@test.local", FullName = "Waste Officer", IsActive = true, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var python = new FakePythonOrchestrationClient(
            start => new PythonOrchestrationEnvelope
            {
                WorkflowId = wrongWorkflowId ? Guid.NewGuid() : start.WorkflowId,
                Objective = wrongObjective ? "A different objective" : start.Objective,
                Status = "Paused",
                CurrentPhase = phase,
                PlannerResult = Json("{}"),
                WasteAnalysisResult = Json("{}"),
                CollectionPlanningResult = Json("{}"),
                CompletedSpecialists = ["WasteAnalysis", "CollectionPlanning"]
            },
            _ => throw new InvalidOperationException("Resume is not expected."));
        var service = CreateService(db, python);
        var workflow = await service.CreateWorkflowAsync("Coordinate invalid Python response", userId);

        var act = async () => await service.StartWorkflowAsync(workflow.Id, userId, AppRoles.WasteOfficer);

        await act.Should().ThrowAsync<AiServiceUnavailableException>();
        (await db.AgentWorkflows.FindAsync(workflow.Id))!.Status.Should().Be(AgentWorkflowStatus.Failed);
    }

    [Fact]
    public async Task ExecuteCollectionPlanAsync_FailedResumeEnvelope_PreservesCommittedTasksAndMarksWorkflowFailed()
    {
        using var db = CreateContext(Guid.NewGuid().ToString("N"));
        var managerId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        SeedAuthoritativeCollectionData(db, managerId, reportId);
        var python = new FakePythonOrchestrationClient(
            start => FirstHalf(start.WorkflowId, start.Objective, reportId),
            resume => new PythonOrchestrationEnvelope
            {
                WorkflowId = resume.Workflow.WorkflowId,
                Objective = resume.Workflow.Objective,
                Status = "Failed",
                CurrentPhase = "Failed",
                Errors = Json("[{\"code\":\"C3_FAILURE\"}]")
            });
        var service = CreateService(db, python);
        var workflow = await service.CreateWorkflowAsync("Coordinate failed resume workflow", managerId);
        var started = await service.StartWorkflowAsync(workflow.Id, managerId, AppRoles.WasteOfficer);
        var approved = await service.ApproveCollectionPlanningAsync(workflow.Id, new ApproveCollectionPlanningRequest { ExpectedVersion = started.Version, Reason = "Approved." }, managerId);

        var result = await service.ExecuteCollectionPlanAsync(workflow.Id, new ExecuteCollectionPlanRequest { ExpectedVersion = approved.Version }, managerId);

        result.Status.Should().Be(AgentWorkflowStatus.Failed);
        python.ResumeCalls.Should().Be(1);
        (await db.CollectionTasks.CountAsync()).Should().Be(1, "C2 task creation was committed before the failed Python resume");
        (await db.AgentWorkflowExecutionResults.SingleAsync(e => e.WorkflowId == workflow.Id)).Status.Should().Be(WorkflowExecutionStatus.Succeeded);
        (await db.AgentWorkflowSteps.SingleAsync(s => s.WorkflowId == workflow.Id && s.Status == WorkflowStepStatus.Failed)).ValidationJson.Should().Contain("C3_FAILURE");
    }

    [Fact]
    public async Task ExecuteCollectionPlanAsync_ResumeTransportFailure_PreservesCommittedTasksAndMarksWorkflowFailed()
    {
        using var db = CreateContext(Guid.NewGuid().ToString("N"));
        var managerId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        SeedAuthoritativeCollectionData(db, managerId, reportId);
        var python = new FakePythonOrchestrationClient(
            start => FirstHalf(start.WorkflowId, start.Objective, reportId),
            _ => throw new AiServiceUnavailableException("Python resume endpoint is unavailable."));
        var service = CreateService(db, python);
        var workflow = await service.CreateWorkflowAsync("Coordinate resume transport failure", managerId);
        var started = await service.StartWorkflowAsync(workflow.Id, managerId, AppRoles.WasteOfficer);
        var approved = await service.ApproveCollectionPlanningAsync(workflow.Id, new ApproveCollectionPlanningRequest { ExpectedVersion = started.Version, Reason = "Approved." }, managerId);

        var act = async () => await service.ExecuteCollectionPlanAsync(workflow.Id, new ExecuteCollectionPlanRequest { ExpectedVersion = approved.Version }, managerId);

        await act.Should().ThrowAsync<AiServiceUnavailableException>();
        python.ResumeCalls.Should().Be(1);
        (await db.CollectionTasks.CountAsync()).Should().Be(1, "a post-commit transport failure must not roll back scheduled tasks");
        (await db.AgentWorkflows.FindAsync(workflow.Id))!.Status.Should().Be(AgentWorkflowStatus.Failed);
    }

    [Fact]
    public async Task ExecuteCollectionPlanAsync_RecoversOnlyPostCommitResumeWithoutCreatingTasksAgain()
    {
        using var db = CreateContext(Guid.NewGuid().ToString("N"));
        var officerId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        SeedAuthoritativeCollectionData(db, officerId, reportId);
        var failingPython = new FakePythonOrchestrationClient(
            start => FirstHalf(start.WorkflowId, start.Objective, reportId),
            _ => throw new AiServiceUnavailableException("HTTP 422"));
        var service = CreateService(db, failingPython);
        var workflow = await service.CreateWorkflowAsync("Coordinate recoverable collection workflow", officerId);
        var started = await service.StartWorkflowAsync(workflow.Id, officerId, AppRoles.WasteOfficer);
        var approved = await service.ApproveCollectionPlanningAsync(workflow.Id,
            new ApproveCollectionPlanningRequest { ExpectedVersion = started.Version, Reason = "Approved." }, officerId);

        var initial = async () => await service.ExecuteCollectionPlanAsync(workflow.Id,
            new ExecuteCollectionPlanRequest { ExpectedVersion = approved.Version }, officerId);
        await initial.Should().ThrowAsync<AiServiceUnavailableException>();
        var taskIds = await db.CollectionTasks.Select(t => t.Id).ToListAsync();
        taskIds.Should().ContainSingle();
        var successfulExecution = await db.AgentWorkflowExecutionResults.SingleAsync(e => e.WorkflowId == workflow.Id);
        successfulExecution.Status.Should().Be(WorkflowExecutionStatus.Succeeded);
        var failedVersion = (await db.AgentWorkflows.SingleAsync(w => w.Id == workflow.Id)).Version;

        var recoveringPython = new FakePythonOrchestrationClient(
            _ => throw new InvalidOperationException("Shared Planner/C1/C2 must not run again."),
            resume => SecondHalf(resume.Workflow.WorkflowId!.Value, resume.Workflow.Objective!,
                "PausedForDispatchApproval", "Paused", "FleetDispatch", "ReadyForHumanReview"));
        var recoveryService = CreateService(db, recoveringPython);
        var stale = async () => await recoveryService.ExecuteCollectionPlanAsync(workflow.Id,
            new ExecuteCollectionPlanRequest { ExpectedVersion = approved.Version }, officerId);
        await stale.Should().ThrowAsync<BusinessRuleConflictException>();
        recoveringPython.ResumeCalls.Should().Be(0);
        var recovered = await recoveryService.ExecuteCollectionPlanAsync(workflow.Id,
            new ExecuteCollectionPlanRequest { ExpectedVersion = failedVersion }, officerId);

        recovered.Status.Should().Be(AgentWorkflowStatus.AwaitingDispatchApproval);
        recoveringPython.StartCalls.Should().Be(0);
        recoveringPython.ResumeCalls.Should().Be(1);
        (await db.CollectionTasks.Select(t => t.Id).ToListAsync()).Should().BeEquivalentTo(taskIds);
        (await db.AgentWorkflowExecutionResults.SingleAsync(e => e.WorkflowId == workflow.Id)).Id
            .Should().Be(successfulExecution.Id);
        recovered.Transitions.Should().Contain(t => t.FromStatus == AgentWorkflowStatus.Failed &&
            t.ToStatus == AgentWorkflowStatus.FleetPlanning &&
            t.Reason!.Contains("Retrying post-collection-execution AI continuation"));
        recovered.Steps.Count(s => s.StepType == WorkflowStepType.FleetPlanning).Should().Be(1);
        recovered.Steps.Count(s => s.StepType == WorkflowStepType.OperationalValidation).Should().Be(1);
        recovered.Approvals.Should().NotContain(a => a.ApprovalStage == WorkflowApprovalStage.FleetDispatch);

        var staleAfterRecovery = async () => await recoveryService.ExecuteCollectionPlanAsync(workflow.Id,
            new ExecuteCollectionPlanRequest { ExpectedVersion = failedVersion }, officerId);
        await staleAfterRecovery.Should().ThrowAsync<BusinessRuleConflictException>();
        recoveringPython.ResumeCalls.Should().Be(1);
        (await db.CollectionTasks.Select(t => t.Id).ToListAsync()).Should().BeEquivalentTo(taskIds);
    }

    [Fact]
    public async Task ExecuteCollectionPlanAsync_DoesNotRecoverUnrelatedFailedWorkflowWithSucceededTasks()
    {
        using var db = CreateContext(Guid.NewGuid().ToString("N"));
        var officerId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        SeedAuthoritativeCollectionData(db, officerId, reportId);
        var failingPython = new FakePythonOrchestrationClient(
            start => FirstHalf(start.WorkflowId, start.Objective, reportId),
            _ => throw new AiServiceUnavailableException("HTTP 422"));
        var service = CreateService(db, failingPython);
        var workflow = await service.CreateWorkflowAsync("Coordinate guarded recovery workflow", officerId);
        var started = await service.StartWorkflowAsync(workflow.Id, officerId, AppRoles.WasteOfficer);
        var approved = await service.ApproveCollectionPlanningAsync(workflow.Id,
            new ApproveCollectionPlanningRequest { ExpectedVersion = started.Version, Reason = "Approved." }, officerId);
        var initial = async () => await service.ExecuteCollectionPlanAsync(workflow.Id,
            new ExecuteCollectionPlanRequest { ExpectedVersion = approved.Version }, officerId);
        await initial.Should().ThrowAsync<AiServiceUnavailableException>();

        var failureTransition = await db.AgentWorkflowTransitions.SingleAsync(t => t.WorkflowId == workflow.Id && t.ToStatus == AgentWorkflowStatus.Failed);
        failureTransition.Reason = "A different failure after collection execution.";
        await db.SaveChangesAsync();
        var taskIds = await db.CollectionTasks.Select(t => t.Id).ToListAsync();
        var failedVersion = (await db.AgentWorkflows.SingleAsync(w => w.Id == workflow.Id)).Version;
        var recoveryPython = new FakePythonOrchestrationClient(
            _ => throw new InvalidOperationException("Start must not run."),
            _ => throw new InvalidOperationException("Unrelated failure must not resume."));

        var result = await CreateService(db, recoveryPython).ExecuteCollectionPlanAsync(workflow.Id,
            new ExecuteCollectionPlanRequest { ExpectedVersion = failedVersion }, officerId);

        result.Status.Should().Be(AgentWorkflowStatus.Failed);
        recoveryPython.ResumeCalls.Should().Be(0);
        (await db.CollectionTasks.Select(t => t.Id).ToListAsync()).Should().BeEquivalentTo(taskIds);
    }

    [Fact]
    public async Task ExecuteCollectionPlanAsync_FailedC2Execution_DoesNotCallPythonResume()
    {
        using var db = CreateContext(Guid.NewGuid().ToString("N"));
        var managerId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        SeedAuthoritativeCollectionData(db, managerId, reportId);
        var python = new FakePythonOrchestrationClient(
            start => FirstHalf(start.WorkflowId, start.Objective, reportId),
            _ => throw new InvalidOperationException("Resume must not be called after failed C2 execution."));
        var service = CreateService(db, python);
        var workflow = await service.CreateWorkflowAsync("Coordinate invalid approved collection plan", managerId);
        var started = await service.StartWorkflowAsync(workflow.Id, managerId, AppRoles.WasteOfficer);
        var approved = await service.ApproveCollectionPlanningAsync(workflow.Id, new ApproveCollectionPlanningRequest { ExpectedVersion = started.Version, Reason = "Approved." }, managerId);
        var c2 = await db.AgentWorkflowSteps.SingleAsync(s => s.WorkflowId == workflow.Id && s.StepType == WorkflowStepType.CollectionPlanning);
        c2.OutputJson = CollectionPlanJson(reportId, DateTime.UtcNow.AddHours(-1));
        await db.SaveChangesAsync();

        var act = async () => await service.ExecuteCollectionPlanAsync(workflow.Id, new ExecuteCollectionPlanRequest { ExpectedVersion = approved.Version }, managerId);

        await act.Should().ThrowAsync<BusinessRuleConflictException>();
        python.ResumeCalls.Should().Be(0);
        (await db.CollectionTasks.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ExecuteCollectionPlanAsync_InvalidSecondHalfEnvelope_MarksWorkflowFailedAfterTaskCommit()
    {
        using var db = CreateContext(Guid.NewGuid().ToString("N"));
        var managerId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        SeedAuthoritativeCollectionData(db, managerId, reportId);
        var python = new FakePythonOrchestrationClient(
            start => FirstHalf(start.WorkflowId, start.Objective, reportId),
            _ => new PythonOrchestrationEnvelope
            {
                WorkflowId = Guid.NewGuid(),
                Objective = "Mismatched objective",
                Status = "Completed",
                CurrentPhase = "Completed"
            });
        var service = CreateService(db, python);
        var workflow = await service.CreateWorkflowAsync("Coordinate invalid second-half response", managerId);
        var started = await service.StartWorkflowAsync(workflow.Id, managerId, AppRoles.WasteOfficer);
        var approved = await service.ApproveCollectionPlanningAsync(workflow.Id, new ApproveCollectionPlanningRequest { ExpectedVersion = started.Version, Reason = "Approved." }, managerId);

        var act = async () => await service.ExecuteCollectionPlanAsync(workflow.Id, new ExecuteCollectionPlanRequest { ExpectedVersion = approved.Version }, managerId);

        await act.Should().ThrowAsync<AiServiceUnavailableException>();
        python.ResumeCalls.Should().Be(1);
        (await db.CollectionTasks.CountAsync()).Should().Be(1);
        (await db.AgentWorkflows.FindAsync(workflow.Id))!.Status.Should().Be(AgentWorkflowStatus.Failed);
    }

    private static PythonOrchestrationEnvelope FirstHalf(Guid workflowId, string objective, Guid reportId)
    {
        return new PythonOrchestrationEnvelope
        {
            WorkflowId = workflowId,
            Objective = objective,
            Status = "Paused",
            CurrentPhase = "PausedForCollectionApproval",
            ApprovalStage = "CollectionPlanning",
            PlannerResult = Json("{\"objective\":\"shared planning\"}"),
            WasteAnalysisResult = Json("{\"objective\":\"waste analysis\"}"),
            CollectionPlanningResult = Json(CollectionPlanJson(reportId, NextValidUtc())),
            CompletedSpecialists = ["WasteAnalysis", "CollectionPlanning"]
        };
    }

    private static PythonOrchestrationEnvelope SecondHalf(Guid workflowId, string objective, string phase, string status, string approvalStage, string validationOutcome) => new()
    {
        WorkflowId = workflowId,
        Objective = objective,
        Status = status,
        CurrentPhase = phase,
        ApprovalStage = approvalStage,
        FleetRouteResult = Json("{\"fleetPlans\":[]}"),
        ValidationOperationsResult = Json($"{{\"validationOutcome\":\"{validationOutcome}\",\"planReviews\":[]}}"),
        CompletedSpecialists = ["WasteAnalysis", "CollectionPlanning", "FleetRoute", "ValidationOperations"]
    };

    private static string CollectionPlanJson(Guid reportId, DateTime scheduledAt) => JsonSerializer.Serialize(new
    {
        status = "completed",
        isCompleteSnapshot = true,
        candidateGroups = new[]
        {
            new
            {
                groupId = "collection-group",
                attentionOrder = 1,
                proposedSchedule = new { scheduledAt = scheduledAt.ToString("yyyy-MM-ddTHH:mm:ssZ"), schedulingReason = "Verified report needs collection." },
                wasteHandlingConsiderations = Array.Empty<string>(),
                needReferences = new[] { new { needId = reportId, targetType = "Report", collectionReason = "VerifiedReport" } }
            }
        },
        separateHandling = Array.Empty<object>(),
        deferredNeeds = Array.Empty<object>()
    });

    private static void SeedAuthoritativeCollectionData(AppDbContext db, Guid managerId, Guid reportId)
    {
        db.Users.Add(new AppUser { Id = managerId, UserName = "manager", Email = "manager@test.local", FullName = "Waste Officer", IsActive = true, CreatedAt = DateTime.UtcNow });
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
            CreatedAt = DateTime.UtcNow.AddHours(-3),
            VerifiedAt = DateTime.UtcNow.AddHours(-1)
        });
        db.SaveChanges();
    }
}
