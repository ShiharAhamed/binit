using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Identity;
using SmartWaste.Application.Collection.DTOs.Requests;
using SmartWaste.Application.Collection.Interfaces;
using SmartWaste.Application.Common.Exceptions;
using SmartWaste.Application.Common.Models;
using SmartWaste.Application.Fleet.DTOs.Responses;
using SmartWaste.Application.Fleet.Services;
using SmartWaste.Application.Workflow.DTOs.Helpers;
using SmartWaste.Application.Workflow.DTOs.Requests;
using SmartWaste.Application.Workflow.DTOs.Responses;
using SmartWaste.Application.Workflow.DTOs.Transport;
using SmartWaste.Application.Workflow.Interfaces;
using SmartWaste.Domain.Collection.Entities;
using SmartWaste.Domain.Collection.Enums;
using SmartWaste.Domain.Common;
using SmartWaste.Domain.Entities;
using SmartWaste.Domain.Reporting.Enums;
using SmartWaste.Domain.Workflow.Entities;
using SmartWaste.Domain.Workflow.Enums;
using SmartWaste.Infrastructure.Persistence;

namespace SmartWaste.Infrastructure.Workflow.Services;

/// <summary>
/// Authoritative infrastructure service managing AgentWorkflow persistence and atomic lifecycle transitions.
/// </summary>
public class AgentWorkflowService : IAgentWorkflowService
{
    private readonly AppDbContext _db;
    private readonly IAgentWorkflowStateMachine _stateMachine;
    private readonly ILogger<AgentWorkflowService> _logger;
    private readonly ICollectionTaskService? _collectionTaskService;
    private readonly ICollectionAssignmentService? _collectionAssignmentService;
    private readonly UserManager<AppUser>? _userManager;
    private readonly IPythonOrchestrationClient? _pythonOrchestrationClient;
    private readonly CollectionSchedulePolicy _collectionSchedulePolicy;
    private readonly TimeProvider _timeProvider;

    public AgentWorkflowService(
        AppDbContext db,
        IAgentWorkflowStateMachine stateMachine,
        ILogger<AgentWorkflowService> logger,
        ICollectionTaskService? collectionTaskService = null,
        ICollectionAssignmentService? collectionAssignmentService = null,
        UserManager<AppUser>? userManager = null,
        IPythonOrchestrationClient? pythonOrchestrationClient = null,
        IOptions<CollectionScheduleOptions>? collectionScheduleOptions = null,
        TimeProvider? timeProvider = null)
    {
        _db = db;
        _stateMachine = stateMachine;
        _logger = logger;
        _collectionTaskService = collectionTaskService;
        _collectionAssignmentService = collectionAssignmentService;
        _userManager = userManager;
        _pythonOrchestrationClient = pythonOrchestrationClient;
        _collectionSchedulePolicy = new CollectionSchedulePolicy(
            collectionScheduleOptions?.Value ?? new CollectionScheduleOptions());
        _timeProvider = timeProvider ?? TimeProvider.System;
    }


    public async Task<AgentWorkflow> CreateWorkflowAsync(
        string objective,
        Guid initiatedByUserId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(objective) || objective.Trim().Length < 5)
        {
            throw new ArgumentException("Workflow objective must be at least 5 characters long.", nameof(objective));
        }

        if (objective.Length > 1000)
        {
            throw new ArgumentException("Workflow objective cannot exceed 1000 characters.", nameof(objective));
        }

        var workflow = new AgentWorkflow
        {
            Id = Guid.NewGuid(),
            Objective = objective.Trim(),
            Status = AgentWorkflowStatus.Created,
            CurrentStep = WorkflowStepType.None,
            InitiatedByUserId = initiatedByUserId,
            CreatedAt = DateTime.UtcNow,
            Version = 1
        };

        workflow.EnsureValidTrigger();

        _db.AgentWorkflows.Add(workflow);

        // Initial transition audit record: null -> Created
        var initialTransition = new AgentWorkflowTransition
        {
            Id = Guid.NewGuid(),
            WorkflowId = workflow.Id,
            FromStatus = null,
            ToStatus = AgentWorkflowStatus.Created,
            Reason = "Workflow created.",
            ChangedByUserId = initiatedByUserId,
            ChangedAt = DateTime.UtcNow
        };

        _db.AgentWorkflowTransitions.Add(initialTransition);

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "AgentWorkflow {WorkflowId} created by User {UserId} with status {Status}.",
            workflow.Id, initiatedByUserId, workflow.Status);

        return workflow;
    }

    public async Task<AgentWorkflow?> GetWorkflowByIdAsync(
        Guid id,
        bool includeDetails = false,
        CancellationToken cancellationToken = default)
    {
        IQueryable<AgentWorkflow> query = _db.AgentWorkflows;

        if (includeDetails)
        {
            query = query
                .Include(w => w.InitiatedByUser)
                .Include(w => w.Steps)
                .Include(w => w.Transitions)
                .Include(w => w.Approvals)
                .Include(w => w.ExecutionResults);
        }

        return await query.FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
    }

    public async Task<PagedResult<AgentWorkflowSummaryDto>> GetWorkflowsAsync(
        AgentWorkflowListQuery query,
        Guid actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default)
    {
        IQueryable<AgentWorkflow> queryable = _db.AgentWorkflows.AsNoTracking();

        if (!AppRoles.IsAgentWorkflowAuthority(actorRole))
        {
            throw new ForbiddenException("You do not have permission to view agent workflows.");
        }

        // Status filter validation and application
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (Enum.TryParse<AgentWorkflowStatus>(query.Status.Trim(), true, out var status))
            {
                queryable = queryable.Where(w => w.Status == status);
            }
            else
            {
                throw new FluentValidation.ValidationException(new[]
                {
                    new FluentValidation.Results.ValidationFailure(
                        nameof(query.Status),
                        $"Invalid status filter '{query.Status}'. Must be a valid AgentWorkflowStatus.")
                });
            }
        }

        var totalCount = await queryable.CountAsync(cancellationToken);

        var items = await queryable
            .OrderByDescending(w => w.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(w => new AgentWorkflowSummaryDto
            {
                Id = w.Id,
                TriggerType = w.TriggerType,
                TriggeringWasteReportId = w.TriggeringWasteReportId,
                Objective = w.Objective,
                Status = w.Status,
                CurrentStep = w.CurrentStep,
                InitiatedByUserId = w.InitiatedByUserId,
                CreatedAt = w.CreatedAt,
                UpdatedAt = w.UpdatedAt,
                CompletedAt = w.CompletedAt,
                FinalOutcome = w.FinalOutcome,
                Version = w.Version
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<AgentWorkflowSummaryDto>
        {
            Items = items,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = totalCount
        };
    }

    public async Task<AgentWorkflowDetailDto> GetWorkflowDetailsAsync(
        Guid id,
        Guid actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default)
    {
        var workflow = await _db.AgentWorkflows
            .AsNoTracking()
            .Include(w => w.Steps)
            .Include(w => w.Transitions)
            .Include(w => w.Approvals)
            .Include(w => w.ExecutionResults)
            .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);

        if (workflow == null)
        {
            throw new NotFoundException($"AgentWorkflow with ID '{id}' was not found.");
        }

        if (!AppRoles.IsAgentWorkflowAuthority(actorRole))
        {
            throw new ForbiddenException("You do not have permission to view agent workflows.");
        }

        var detail = MapToDetailDto(workflow);
        var binNeedIds = GetBinNeedIds(workflow);
        if (binNeedIds.Count > 0)
        {
            detail.BinCodes = await _db.WasteBins.AsNoTracking()
                .Where(bin => binNeedIds.Contains(bin.Id))
                .ToDictionaryAsync(bin => bin.Id, bin => bin.BinCode, cancellationToken);
        }
        return detail;
    }

    private static HashSet<Guid> GetBinNeedIds(AgentWorkflow workflow)
    {
        var ids = new HashSet<Guid>();
        var output = workflow.Steps
            .Where(step => step.StepType == WorkflowStepType.CollectionPlanning && step.OutputJson != null)
            .OrderByDescending(step => step.Sequence)
            .Select(step => step.OutputJson)
            .FirstOrDefault();
        if (output is null)
            return ids;

        try
        {
            using var document = JsonDocument.Parse(output);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return ids;

            static void AddReference(JsonElement reference, HashSet<Guid> target)
            {
                if (reference.ValueKind == JsonValueKind.Object &&
                    reference.TryGetProperty("targetType", out var type) && type.ValueKind == JsonValueKind.String && type.GetString() == "Bin" &&
                    reference.TryGetProperty("needId", out var id) && id.ValueKind == JsonValueKind.String && id.TryGetGuid(out var binId))
                    target.Add(binId);
            }

            if (root.TryGetProperty("candidateGroups", out var groups) && groups.ValueKind == JsonValueKind.Array)
                foreach (var group in groups.EnumerateArray())
                    if (group.ValueKind == JsonValueKind.Object && group.TryGetProperty("needReferences", out var references) && references.ValueKind == JsonValueKind.Array)
                        foreach (var reference in references.EnumerateArray())
                            AddReference(reference, ids);

            foreach (var name in new[] { "separateHandling", "deferredNeeds" })
                if (root.TryGetProperty(name, out var items) && items.ValueKind == JsonValueKind.Array)
                    foreach (var item in items.EnumerateArray())
                        if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("needReference", out var reference))
                            AddReference(reference, ids);
        }
        catch (JsonException)
        {
            // Historical or failed step payloads may be incomplete; display metadata is optional.
        }
        return ids;
    }

    public async Task<IReadOnlyList<AgentWorkflowTransitionDto>> GetWorkflowHistoryAsync(
        Guid id,
        Guid actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default)
    {
        var workflow = await _db.AgentWorkflows
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);

        if (workflow == null)
        {
            throw new NotFoundException($"AgentWorkflow with ID '{id}' was not found.");
        }

        if (!AppRoles.IsAgentWorkflowAuthority(actorRole))
        {
            throw new ForbiddenException("You do not have permission to view agent workflows.");
        }

        var transitions = await _db.AgentWorkflowTransitions
            .AsNoTracking()
            .Where(t => t.WorkflowId == id)
            .OrderBy(t => t.ChangedAt)
            .Select(t => new AgentWorkflowTransitionDto
            {
                Id = t.Id,
                FromStatus = t.FromStatus,
                ToStatus = t.ToStatus,
                Reason = t.Reason,
                ChangedByUserId = t.ChangedByUserId,
                ChangedAt = t.ChangedAt
            })
            .ToListAsync(cancellationToken);

        return transitions;
    }

    public async Task<AgentWorkflowSummaryDto> StartWorkflowAsync(
        Guid id,
        Guid actorUserId,
        string actorRole,
        CancellationToken cancellationToken = default)
    {
        var workflow = await _db.AgentWorkflows
            .FirstOrDefaultAsync(w => w.Id == id, cancellationToken)
            ?? throw new NotFoundException($"AgentWorkflow with ID '{id}' was not found.");

        if (!AppRoles.IsAgentWorkflowAuthority(actorRole))
        {
            throw new ForbiddenException("You do not have permission to start agent workflows.");
        }

        if (workflow.TriggerType != AgentWorkflowTriggerType.ManualOperationalPlanning)
            throw new BusinessRuleConflictException("Report-triggered workflows are started by the durable worker.");

        var fromStatus = workflow.Status;

        // State machine validation: only Created -> Planning is legal (throws InvalidWorkflowTransitionException -> 409 Conflict if already Planning, Completed, etc.)
        _stateMachine.EnsureCanTransition(fromStatus, AgentWorkflowStatus.Planning);

        // Atomic state and operational progress update
        workflow.Status = AgentWorkflowStatus.Planning;
        workflow.CurrentStep = WorkflowStepType.SharedPlanning;
        workflow.UpdatedAt = DateTime.UtcNow;
        workflow.Version++;

        var transition = new AgentWorkflowTransition
        {
            Id = Guid.NewGuid(),
            WorkflowId = workflow.Id,
            FromStatus = fromStatus,
            ToStatus = AgentWorkflowStatus.Planning,
            Reason = "Workflow started by authorized actor.",
            ChangedByUserId = actorUserId,
            ChangedAt = DateTime.UtcNow
        };

        _db.AgentWorkflowTransitions.Add(transition);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Concurrency conflict starting AgentWorkflow {WorkflowId}.", id);
            throw new BusinessRuleConflictException(
                "The workflow was modified concurrently by another operation. Please reload and try again.");
        }

        _logger.LogInformation(
            "AgentWorkflow {WorkflowId} started by User {UserId} ({Role}): Status=Planning, CurrentStep=SharedPlanning.",
            id, actorUserId, actorRole);

        if (_pythonOrchestrationClient is not null)
        {
            try
            {
                var python = await _pythonOrchestrationClient.StartAsync(new PythonWorkflowStartRequest
                {
                    WorkflowId = workflow.Id,
                    Objective = workflow.Objective,
                    TriggerType = workflow.TriggerType,
                    TriggeringWasteReportId = workflow.TriggeringWasteReportId
                }, cancellationToken);
                workflow = workflow.TriggerType == AgentWorkflowTriggerType.CitizenReportSubmission
                    ? await PersistReportVerificationPauseAsync(workflow.Id, python, workflow.Version, actorUserId, workflow.ProcessingLeaseId, cancellationToken)
                    : await PersistFirstHalfAsync(workflow.Id, python, actorUserId, cancellationToken);
            }
            catch (Exception ex) when (ex is AiServiceUnavailableException or BusinessRuleConflictException)
            {
                await FailWorkflowSafelyAsync(workflow.Id, "Internal AI orchestration start failed.", actorUserId, cancellationToken);
                throw;
            }
        }

        return MapToSummaryDto(workflow);
    }

    /// <summary>Builds a stateless Python start request from authoritative workflow metadata.</summary>
    public async Task<PythonWorkflowStartRequest> BuildPythonStartRequestAsync(
        Guid workflowId, int expectedVersion, Guid? expectedProcessingLeaseId = null,
        CancellationToken cancellationToken = default)
    {
        var workflow = await _db.AgentWorkflows.AsNoTracking()
            .SingleOrDefaultAsync(w => w.Id == workflowId, cancellationToken)
            ?? throw new NotFoundException($"AgentWorkflow with ID '{workflowId}' was not found.");
        EnsureExpectedAttempt(workflow, expectedVersion, expectedProcessingLeaseId);
        workflow.EnsureValidTrigger();
        if (workflow.Status != AgentWorkflowStatus.Planning || workflow.CurrentStep != WorkflowStepType.SharedPlanning ||
            await _db.AgentWorkflowSteps.AnyAsync(s => s.WorkflowId == workflowId, cancellationToken))
            throw new BusinessRuleConflictException("The workflow is not ready for an initial AI start.");
        return new PythonWorkflowStartRequest
        {
            WorkflowId = workflow.Id, Objective = workflow.Objective,
            TriggerType = workflow.TriggerType, TriggeringWasteReportId = workflow.TriggeringWasteReportId
        };
    }

    /// <summary>Stores the exact-report Planner/C1 pause in one EF save, with no report mutation.</summary>
    public async Task<AgentWorkflow> PersistReportVerificationPauseAsync(
        Guid workflowId, PythonOrchestrationEnvelope result, int expectedVersion,
        Guid? changedByUserId = null, Guid? expectedProcessingLeaseId = null,
        CancellationToken cancellationToken = default)
    {
        var workflow = await _db.AgentWorkflows.Include(w => w.Steps)
            .SingleOrDefaultAsync(w => w.Id == workflowId, cancellationToken)
            ?? throw new NotFoundException($"AgentWorkflow with ID '{workflowId}' was not found.");
        EnsureExpectedAttempt(workflow, expectedVersion, expectedProcessingLeaseId);
        if (workflow.TriggerType != AgentWorkflowTriggerType.CitizenReportSubmission ||
            workflow.TriggeringWasteReportId is null || workflow.Status != AgentWorkflowStatus.Planning ||
            workflow.CurrentStep != WorkflowStepType.SharedPlanning || workflow.Steps.Any())
            throw new BusinessRuleConflictException("The workflow cannot accept a report verification pause.");
        ValidateReportPauseEnvelope(workflow, result);

        var now = DateTime.UtcNow;
        _db.AgentWorkflowSteps.Add(new AgentWorkflowStep
        {
            Id = Guid.NewGuid(), WorkflowId = workflowId, Sequence = 1,
            StepType = WorkflowStepType.SharedPlanning, AgentName = "shared_planner_agent",
            Status = WorkflowStepStatus.Completed, OutputJson = result.PlannerResult!.Value.GetRawText(),
            ValidationJson = result.Warnings!.Value.GetRawText(), StartedAt = now, CompletedAt = now
        });
        _db.AgentWorkflowSteps.Add(new AgentWorkflowStep
        {
            Id = Guid.NewGuid(), WorkflowId = workflowId, Sequence = 2,
            StepType = WorkflowStepType.WasteAnalysis, AgentName = "waste_analysis_agent",
            Status = WorkflowStepStatus.Completed, OutputJson = result.WasteAnalysisResult!.Value.GetRawText(),
            StartedAt = now, CompletedAt = now
        });
        _stateMachine.EnsureCanTransition(workflow.Status, AgentWorkflowStatus.AwaitingReportVerification);
        workflow.Status = AgentWorkflowStatus.AwaitingReportVerification;
        workflow.CurrentStep = WorkflowStepType.WasteAnalysis;
        workflow.Version++;
        workflow.UpdatedAt = now;
        workflow.ProcessingLeaseId = null;
        workflow.ProcessingLeaseExpiresAt = null;
        _db.AgentWorkflowTransitions.Add(new AgentWorkflowTransition
        {
            Id = Guid.NewGuid(), WorkflowId = workflowId, FromStatus = AgentWorkflowStatus.Planning,
            ToStatus = AgentWorkflowStatus.AwaitingReportVerification,
            Reason = "Exact-report AI analysis paused for authoritative report verification.",
            ChangedByUserId = changedByUserId, ChangedAt = now
        });
        await SaveOrThrowConcurrencyAsync("persisting report verification pause", workflowId, cancellationToken);
        return workflow;
    }

    /// <summary>Reconstructs Python's report-pause snapshot solely from persisted workflow data.</summary>
    public async Task<PythonWorkflowResumeRequest> BuildReportVerificationResumeRequestAsync(
        Guid workflowId, int expectedVersion, Guid? expectedProcessingLeaseId = null,
        CancellationToken cancellationToken = default)
    {
        var workflow = await _db.AgentWorkflows.AsNoTracking().Include(w => w.Steps)
            .SingleOrDefaultAsync(w => w.Id == workflowId, cancellationToken)
            ?? throw new NotFoundException($"AgentWorkflow with ID '{workflowId}' was not found.");
        EnsureExpectedAttempt(workflow, expectedVersion, expectedProcessingLeaseId);
        if (workflow.TriggerType != AgentWorkflowTriggerType.CitizenReportSubmission ||
            workflow.TriggeringWasteReportId is not Guid reportId ||
            workflow.Status != AgentWorkflowStatus.Planning ||
            workflow.CurrentStep != WorkflowStepType.CollectionPlanning ||
            workflow.Steps.Count != 2)
            throw new BusinessRuleConflictException("The workflow is not paused for report verification.");

        var report = await _db.WasteReports.AsNoTracking().SingleOrDefaultAsync(r => r.Id == reportId, cancellationToken);
        if (report?.Status != WasteReportStatus.Verified)
            throw new BusinessRuleConflictException("The triggering report has not been authoritatively verified.");

        var (planner, c1) = GetValidReportPauseSteps(workflow, reportId);
        try
        {
            JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement.Clone();
            var plannerOutput = Parse(planner.OutputJson!);
            var c1Output = Parse(c1.OutputJson!);
            var warnings = planner.ValidationJson is null ? JsonSerializer.SerializeToElement(Array.Empty<string>()) : Parse(planner.ValidationJson);
            if (!IsValidPlannerSnapshot(plannerOutput) || !HasExactReportAnalysis(c1Output, reportId) || warnings.ValueKind != JsonValueKind.Array)
                throw new BusinessRuleConflictException("The persisted Planner/C1 snapshot is invalid.");
            return new PythonWorkflowResumeRequest
            {
                Workflow = new PythonOrchestrationEnvelope
                {
                    WorkflowId = workflow.Id, Objective = workflow.Objective,
                    TriggerType = workflow.TriggerType, TriggeringWasteReportId = reportId,
                    Status = "Paused", CurrentPhase = "PausedForReportVerification",
                    ApprovalStage = "ReportVerification",
                    PauseReason = "Waste report analysis awaits authoritative human verification.",
                    PlannerResult = plannerOutput, WasteAnalysisResult = c1Output,
                    CompletedSpecialists = ["WasteAnalysis"],
                    Errors = JsonSerializer.SerializeToElement(Array.Empty<object>()), Warnings = warnings
                },
                ResumeContext = new PythonResumeContext
                {
                    WorkflowId = workflow.Id, ApprovalStage = "ReportVerification", Decision = "Approved",
                    AuthoritativeExecutionSummary = JsonSerializer.SerializeToElement(new
                    {
                        verifiedReportId = reportId,
                        reportStatus = "Verified"
                    })
                }
            };
        }
        catch (JsonException)
        {
            throw new BusinessRuleConflictException("The persisted Planner/C1 snapshot is invalid.");
        }
    }

    /// <summary>Accepts C2 only; both legal status transitions and the step share one EF save.</summary>
    public async Task<AgentWorkflow> PersistReportVerificationContinuationAsync(
        Guid workflowId, PythonOrchestrationEnvelope result, int expectedVersion,
        Guid? changedByUserId = null, Guid? expectedProcessingLeaseId = null,
        CancellationToken cancellationToken = default)
    {
        var workflow = await _db.AgentWorkflows.Include(w => w.Steps)
            .SingleOrDefaultAsync(w => w.Id == workflowId, cancellationToken)
            ?? throw new NotFoundException($"AgentWorkflow with ID '{workflowId}' was not found.");
        EnsureExpectedAttempt(workflow, expectedVersion, expectedProcessingLeaseId);
        if (workflow.TriggerType != AgentWorkflowTriggerType.CitizenReportSubmission ||
            workflow.TriggeringWasteReportId is not Guid reportId ||
            workflow.Status != AgentWorkflowStatus.Planning ||
            workflow.CurrentStep != WorkflowStepType.CollectionPlanning || workflow.Steps.Count != 2 ||
            workflow.Steps.Any(s => s.StepType == WorkflowStepType.CollectionPlanning))
            throw new BusinessRuleConflictException("The workflow cannot accept a report-verification continuation.");
        var report = await _db.WasteReports.AsNoTracking().SingleOrDefaultAsync(r => r.Id == reportId, cancellationToken);
        if (report?.Status != WasteReportStatus.Verified)
            throw new BusinessRuleConflictException("The triggering report has not been authoritatively verified.");
        GetValidReportPauseSteps(workflow, reportId);
        ValidateReportContinuationEnvelope(workflow, result);

        _stateMachine.EnsureCanTransition(workflow.Status, AgentWorkflowStatus.AwaitingCollectionApproval);
        var now = DateTime.UtcNow;
        _db.AgentWorkflowSteps.Add(new AgentWorkflowStep
        {
            Id = Guid.NewGuid(), WorkflowId = workflowId, Sequence = 3,
            StepType = WorkflowStepType.CollectionPlanning, AgentName = "collection_planning_agent",
            Status = WorkflowStepStatus.Completed, OutputJson = result.CollectionPlanningResult!.Value.GetRawText(),
            StartedAt = now, CompletedAt = now
        });
        _db.AgentWorkflowTransitions.Add(new AgentWorkflowTransition
        {
            Id = Guid.NewGuid(), WorkflowId = workflowId, FromStatus = AgentWorkflowStatus.Planning,
            ToStatus = AgentWorkflowStatus.AwaitingCollectionApproval,
            Reason = "C2 planning paused for collection approval.",
            ChangedByUserId = changedByUserId, ChangedAt = now.AddTicks(1)
        });
        workflow.Status = AgentWorkflowStatus.AwaitingCollectionApproval;
        workflow.CurrentStep = WorkflowStepType.CollectionPlanning;
        workflow.Version++;
        workflow.UpdatedAt = now;
        workflow.ProcessingLeaseId = null;
        workflow.ProcessingLeaseExpiresAt = null;
        await SaveOrThrowConcurrencyAsync("persisting report-verification C2 continuation", workflowId, cancellationToken);
        return workflow;
    }

    private static void EnsureExpectedAttempt(AgentWorkflow workflow, int expectedVersion, Guid? expectedProcessingLeaseId)
    {
        if (workflow.Version != expectedVersion || workflow.ProcessingLeaseId != expectedProcessingLeaseId)
            throw new BusinessRuleConflictException("The workflow was modified or its processing lease changed.");
    }

    internal static (AgentWorkflowStep Planner, AgentWorkflowStep C1) GetValidReportPauseSteps(AgentWorkflow workflow, Guid reportId)
    {
        var planner = workflow.Steps.SingleOrDefault(s => s.Sequence == 1 && s.StepType == WorkflowStepType.SharedPlanning && s.Status == WorkflowStepStatus.Completed);
        var c1 = workflow.Steps.SingleOrDefault(s => s.Sequence == 2 && s.StepType == WorkflowStepType.WasteAnalysis && s.Status == WorkflowStepStatus.Completed);
        if (workflow.Steps.Count != 2 || planner?.OutputJson is null || c1?.OutputJson is null)
            throw new BusinessRuleConflictException("The persisted Planner/C1 snapshot is incomplete.");
        try
        {
            using var plannerDocument = JsonDocument.Parse(planner.OutputJson);
            using var c1Document = JsonDocument.Parse(c1.OutputJson);
            if (!IsValidPlannerSnapshot(plannerDocument.RootElement) || !HasExactReportAnalysis(c1Document.RootElement, reportId))
                throw new BusinessRuleConflictException("The persisted Planner/C1 snapshot is invalid.");
        }
        catch (JsonException)
        {
            throw new BusinessRuleConflictException("The persisted Planner/C1 snapshot is invalid.");
        }
        return (planner, c1);
    }

    private static bool IsArray(JsonElement? value) => value is { ValueKind: JsonValueKind.Array };

    private static bool HasExactReportAnalysis(JsonElement? output, Guid reportId)
    {
        if (output is not { ValueKind: JsonValueKind.Object } root ||
            !HasNonBlankString(root, "objective") ||
            !HasNonBlankString(root, "status") ||
            !root.TryGetProperty("sourceTotalCount", out var sourceCount) || sourceCount.ValueKind != JsonValueKind.Number ||
            !sourceCount.TryGetInt32(out var count) || count != 1 ||
            !root.TryGetProperty("analyses", out var analyses) || analyses.ValueKind != JsonValueKind.Array ||
            analyses.GetArrayLength() != 1)
            return false;
        var analysis = analyses[0];
        return analysis.ValueKind == JsonValueKind.Object &&
            analysis.TryGetProperty("reportId", out var id) && id.ValueKind == JsonValueKind.String &&
            id.TryGetGuid(out var actualId) && actualId == reportId &&
            HasNonBlankString(analysis, "categoryAssessment") &&
            HasNonBlankString(analysis, "recommendedPriority") &&
            HasNonBlankString(analysis, "recommendedHandling") &&
            HasNonBlankString(analysis, "confidence") &&
            HasNonBlankString(analysis, "rationale") &&
            analysis.TryGetProperty("operationalConcerns", out var concerns) && concerns.ValueKind == JsonValueKind.Array;
    }

    private static bool HasNonBlankString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String &&
        !string.IsNullOrWhiteSpace(value.GetString());

    private static bool IsValidCollectionPlanningSnapshot(JsonElement? output) =>
        output is { ValueKind: JsonValueKind.Object } root &&
        HasNonBlankString(root, "objective") && HasNonBlankString(root, "status") &&
        root.TryGetProperty("candidateGroups", out var groups) && groups.ValueKind == JsonValueKind.Array &&
        root.TryGetProperty("separateHandling", out var separate) && separate.ValueKind == JsonValueKind.Array &&
        root.TryGetProperty("deferredNeeds", out var deferred) && deferred.ValueKind == JsonValueKind.Array;

    private static bool IsValidPlannerSnapshot(JsonElement? output)
    {
        if (output is not { ValueKind: JsonValueKind.Object } root ||
            !root.TryGetProperty("objective", out var objective) || objective.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(objective.GetString()) ||
            !root.TryGetProperty("steps", out var steps) || steps.ValueKind != JsonValueKind.Array)
            return false;
        var specialists = new List<string>();
        foreach (var step in steps.EnumerateArray())
        {
            if (step.ValueKind != JsonValueKind.Object ||
                !step.TryGetProperty("stepId", out var stepId) || stepId.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(stepId.GetString()) ||
                !step.TryGetProperty("specialist", out var specialist) || specialist.ValueKind != JsonValueKind.String ||
                !step.TryGetProperty("objective", out var stepObjective) || stepObjective.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(stepObjective.GetString()) ||
                !step.TryGetProperty("dependsOn", out var dependencies) || dependencies.ValueKind != JsonValueKind.Array)
                return false;
            specialists.Add(specialist.GetString()!);
        }
        return specialists.SequenceEqual(["WasteAnalysis", "CollectionPlanning", "FleetRoute", "ValidationOperations"]);
    }

    private static void ValidateReportPauseEnvelope(AgentWorkflow workflow, PythonOrchestrationEnvelope result)
    {
        if (result.WorkflowId != workflow.Id || result.Objective != workflow.Objective ||
            result.TriggerType != workflow.TriggerType || result.TriggeringWasteReportId != workflow.TriggeringWasteReportId ||
            result.Status != "Paused" || result.CurrentPhase != "PausedForReportVerification" ||
            result.ApprovalStage != "ReportVerification" || string.IsNullOrWhiteSpace(result.PauseReason) ||
            result.CurrentSpecialist is not null ||
            !result.CompletedSpecialists.SequenceEqual(["WasteAnalysis"]) ||
            !IsValidPlannerSnapshot(result.PlannerResult) ||
            !HasExactReportAnalysis(result.WasteAnalysisResult, workflow.TriggeringWasteReportId!.Value) ||
            result.CollectionPlanningResult is not null || result.FleetRouteResult is not null ||
            result.ValidationOperationsResult is not null ||
            !IsArray(result.Errors) || result.Errors!.Value.GetArrayLength() != 0 || !IsArray(result.Warnings))
            throw new AiServiceUnavailableException("Internal AI orchestration returned an invalid report verification pause.");
    }

    private static void ValidateReportContinuationEnvelope(AgentWorkflow workflow, PythonOrchestrationEnvelope result)
    {
        if (result.WorkflowId != workflow.Id || result.Objective != workflow.Objective ||
            result.TriggerType != workflow.TriggerType || result.TriggeringWasteReportId != workflow.TriggeringWasteReportId ||
            result.Status != "Paused" || result.CurrentPhase != "PausedForCollectionApproval" ||
            result.ApprovalStage != "CollectionPlanning" || string.IsNullOrWhiteSpace(result.PauseReason) ||
            result.CurrentSpecialist is not null ||
            !result.CompletedSpecialists.SequenceEqual(["WasteAnalysis", "CollectionPlanning"]) ||
            !IsValidPlannerSnapshot(result.PlannerResult) ||
            !HasExactReportAnalysis(result.WasteAnalysisResult, workflow.TriggeringWasteReportId!.Value) ||
            !IsValidCollectionPlanningSnapshot(result.CollectionPlanningResult) ||
            result.FleetRouteResult is not null || result.ValidationOperationsResult is not null ||
            !IsArray(result.Errors) || result.Errors!.Value.GetArrayLength() != 0 || !IsArray(result.Warnings))
            throw new AiServiceUnavailableException("Internal AI orchestration returned an invalid report-verification continuation.");
    }

    private async Task<AgentWorkflow> PersistFirstHalfAsync(Guid workflowId, PythonOrchestrationEnvelope result, Guid actorUserId, CancellationToken cancellationToken)
    {
        if (result.WorkflowId != workflowId || result.Objective is null || result.CurrentPhase is null)
            throw new AiServiceUnavailableException("Internal AI orchestration response does not match the workflow.");
        var workflow = await _db.AgentWorkflows.Include(w => w.Steps).FirstAsync(w => w.Id == workflowId, cancellationToken);
        if (workflow.Objective != result.Objective || result.TriggerType != workflow.TriggerType ||
            result.TriggeringWasteReportId != workflow.TriggeringWasteReportId)
            throw new AiServiceUnavailableException("Internal AI orchestration response objective does not match the workflow.");

        if (result.CurrentPhase == "Failed")
        {
            PersistReturnedFirstHalfArtifacts(workflow, result);
            PersistSafeFailureDiagnostic(workflow, result, WorkflowStepType.SharedPlanning, "python_orchestration");
            MarkWorkflowFailed(workflow, "Internal AI orchestration returned a failed start result.", actorUserId);
            await SaveOrThrowConcurrencyAsync("persisting a failed internal AI start result", workflowId, cancellationToken);
            return workflow;
        }

        if (workflow.Status != AgentWorkflowStatus.Planning ||
            workflow.Steps.Any(s => s.StepType is WorkflowStepType.SharedPlanning or WorkflowStepType.WasteAnalysis or WorkflowStepType.CollectionPlanning) ||
            result.CurrentPhase != "PausedForCollectionApproval" ||
            result.Status != "Paused" ||
            result.ApprovalStage != "CollectionPlanning" ||
            result.PlannerResult is null ||
            result.WasteAnalysisResult is null ||
            result.CollectionPlanningResult is null ||
            result.FleetRouteResult is not null ||
            result.ValidationOperationsResult is not null ||
            !result.CompletedSpecialists.SequenceEqual(["WasteAnalysis", "CollectionPlanning"]))
            throw new AiServiceUnavailableException("Internal AI orchestration returned an unexpected first-half result.");

        var sequence = workflow.Steps.Count == 0 ? 0 : workflow.Steps.Max(s => s.Sequence);
        foreach (var item in new[] { (WorkflowStepType.SharedPlanning, "shared_planner_agent", result.PlannerResult), (WorkflowStepType.WasteAnalysis, "waste_analysis_agent", result.WasteAnalysisResult), (WorkflowStepType.CollectionPlanning, "collection_planning_agent", result.CollectionPlanningResult) })
        {
            _db.AgentWorkflowSteps.Add(new AgentWorkflowStep { Id = Guid.NewGuid(), WorkflowId = workflowId, Sequence = ++sequence, StepType = item.Item1, AgentName = item.Item2, Status = WorkflowStepStatus.Completed, OutputJson = item.Item3!.Value.GetRawText(), StartedAt = DateTime.UtcNow, CompletedAt = DateTime.UtcNow });
        }
        _stateMachine.EnsureCanTransition(workflow.Status, AgentWorkflowStatus.AwaitingCollectionApproval);
        workflow.Status = AgentWorkflowStatus.AwaitingCollectionApproval;
        workflow.CurrentStep = WorkflowStepType.CollectionPlanning;
        workflow.Version++;
        workflow.UpdatedAt = DateTime.UtcNow;
        _db.AgentWorkflowTransitions.Add(new AgentWorkflowTransition { Id = Guid.NewGuid(), WorkflowId = workflowId, FromStatus = AgentWorkflowStatus.Planning, ToStatus = AgentWorkflowStatus.AwaitingCollectionApproval, Reason = "Python first-half orchestration paused for collection approval.", ChangedByUserId = actorUserId, ChangedAt = DateTime.UtcNow });
        await SaveOrThrowConcurrencyAsync("persisting the internal AI first-half result", workflowId, cancellationToken);
        return workflow;
    }

    private void PersistReturnedFirstHalfArtifacts(AgentWorkflow workflow, PythonOrchestrationEnvelope result)
    {
        var sequence = workflow.Steps.Count == 0 ? 0 : workflow.Steps.Max(s => s.Sequence);
        AddReturnedArtifact(workflow.Id, ref sequence, WorkflowStepType.SharedPlanning, "shared_planner_agent", result.PlannerResult);
        AddReturnedArtifact(workflow.Id, ref sequence, WorkflowStepType.WasteAnalysis, "waste_analysis_agent", result.WasteAnalysisResult);
        AddReturnedArtifact(workflow.Id, ref sequence, WorkflowStepType.CollectionPlanning, "collection_planning_agent", result.CollectionPlanningResult);
    }

    private void AddReturnedArtifact(Guid workflowId, ref int sequence, WorkflowStepType stepType, string agentName, JsonElement? output)
    {
        if (output is null)
            return;

        _db.AgentWorkflowSteps.Add(new AgentWorkflowStep
        {
            Id = Guid.NewGuid(),
            WorkflowId = workflowId,
            Sequence = ++sequence,
            StepType = stepType,
            AgentName = agentName,
            Status = WorkflowStepStatus.Completed,
            OutputJson = output.Value.GetRawText(),
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        });
    }

    private void MarkWorkflowFailed(AgentWorkflow workflow, string reason, Guid actorUserId)
    {
        if (_stateMachine.IsTerminal(workflow.Status))
            return;

        var fromStatus = workflow.Status;
        _stateMachine.EnsureCanTransition(fromStatus, AgentWorkflowStatus.Failed);
        workflow.Status = AgentWorkflowStatus.Failed;
        workflow.UpdatedAt = DateTime.UtcNow;
        workflow.CompletedAt = DateTime.UtcNow;
        workflow.Version++;
        _db.AgentWorkflowTransitions.Add(new AgentWorkflowTransition
        {
            Id = Guid.NewGuid(),
            WorkflowId = workflow.Id,
            FromStatus = fromStatus,
            ToStatus = AgentWorkflowStatus.Failed,
            Reason = reason,
            ChangedByUserId = actorUserId,
            ChangedAt = DateTime.UtcNow
        });
    }

    private async Task FailWorkflowSafelyAsync(Guid workflowId, string reason, Guid actorUserId, CancellationToken cancellationToken)
    {
        _db.ChangeTracker.Clear();
        var workflow = await _db.AgentWorkflows.FirstOrDefaultAsync(w => w.Id == workflowId, cancellationToken)
            ?? throw new NotFoundException($"AgentWorkflow with ID '{workflowId}' was not found.");
        MarkWorkflowFailed(workflow, reason, actorUserId);
        await SaveOrThrowConcurrencyAsync("recording an internal AI orchestration failure", workflowId, cancellationToken);
    }

    private async Task SaveOrThrowConcurrencyAsync(string operation, Guid workflowId, CancellationToken cancellationToken)
    {
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Concurrency conflict {Operation} on AgentWorkflow {WorkflowId}.", operation, workflowId);
            throw new BusinessRuleConflictException("The workflow was modified concurrently by another operation. Please reload and try again.");
        }
    }

    private static AgentWorkflowSummaryDto MapToSummaryDto(AgentWorkflow workflow) => new()
    {
        Id = workflow.Id,
        TriggerType = workflow.TriggerType,
        TriggeringWasteReportId = workflow.TriggeringWasteReportId,
        Objective = workflow.Objective,
        Status = workflow.Status,
        CurrentStep = workflow.CurrentStep,
        InitiatedByUserId = workflow.InitiatedByUserId,
        CreatedAt = workflow.CreatedAt,
        UpdatedAt = workflow.UpdatedAt,
        CompletedAt = workflow.CompletedAt,
        FinalOutcome = workflow.FinalOutcome,
        Version = workflow.Version
    };

    public async Task<AgentWorkflow> TransitionAsync(
        Guid workflowId,
        AgentWorkflowStatus toStatus,
        string? reason = null,
        Guid? changedByUserId = null,
        CancellationToken cancellationToken = default)
    {
        var workflow = await _db.AgentWorkflows.FirstOrDefaultAsync(w => w.Id == workflowId, cancellationToken)
            ?? throw new NotFoundException($"AgentWorkflow with ID '{workflowId}' was not found.");

        var fromStatus = workflow.Status;

        // Validate legal state transition through deterministic state machine
        _stateMachine.EnsureCanTransition(fromStatus, toStatus);

        // Update workflow status & timestamps
        workflow.Status = toStatus;
        workflow.UpdatedAt = DateTime.UtcNow;

        if (_stateMachine.IsTerminal(toStatus))
        {
            workflow.CompletedAt = DateTime.UtcNow;
        }

        // Record immutable audit transition
        var transition = new AgentWorkflowTransition
        {
            Id = Guid.NewGuid(),
            WorkflowId = workflow.Id,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            Reason = reason,
            ChangedByUserId = changedByUserId,
            ChangedAt = DateTime.UtcNow
        };

        _db.AgentWorkflowTransitions.Add(transition);

        // Increment optimistic concurrency token
        workflow.Version++;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "AgentWorkflow {WorkflowId} transitioned from {FromStatus} to {ToStatus}. Reason: {Reason}",
            workflow.Id, fromStatus, toStatus, reason ?? "N/A");

        return workflow;
    }

    public async Task<AgentWorkflow> SetCurrentStepAsync(
        Guid workflowId,
        WorkflowStepType stepType,
        CancellationToken cancellationToken = default)
    {
        var workflow = await _db.AgentWorkflows.FirstOrDefaultAsync(w => w.Id == workflowId, cancellationToken)
            ?? throw new NotFoundException($"AgentWorkflow with ID '{workflowId}' was not found.");

        workflow.CurrentStep = stepType;
        workflow.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "AgentWorkflow {WorkflowId} current step updated to {CurrentStep}.",
            workflow.Id, stepType);

        return workflow;
    }

    public async Task<AgentWorkflowStep> AddStepAsync(
        Guid workflowId,
        WorkflowStepType stepType,
        string? agentName = null,
        string? inputJson = null,
        CancellationToken cancellationToken = default)
    {
        var workflow = await _db.AgentWorkflows.FirstOrDefaultAsync(w => w.Id == workflowId, cancellationToken)
            ?? throw new NotFoundException($"AgentWorkflow with ID '{workflowId}' was not found.");

        var maxSequence = await _db.AgentWorkflowSteps
            .Where(s => s.WorkflowId == workflowId)
            .Select(s => (int?)s.Sequence)
            .MaxAsync(cancellationToken) ?? 0;

        var step = new AgentWorkflowStep
        {
            Id = Guid.NewGuid(),
            WorkflowId = workflowId,
            Sequence = maxSequence + 1,
            StepType = stepType,
            AgentName = agentName,
            Status = WorkflowStepStatus.Pending,
            InputJson = inputJson,
            StartedAt = null,
            CompletedAt = null
        };

        _db.AgentWorkflowSteps.Add(step);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Added Step {Sequence} ({StepType}) to AgentWorkflow {WorkflowId}.",
            step.Sequence, step.StepType, workflowId);

        return step;
    }

    public async Task<AgentWorkflowStep> StartStepAsync(
        Guid stepId,
        CancellationToken cancellationToken = default)
    {
        var step = await _db.AgentWorkflowSteps.FirstOrDefaultAsync(s => s.Id == stepId, cancellationToken)
            ?? throw new NotFoundException($"AgentWorkflowStep with ID '{stepId}' was not found.");

        step.Status = WorkflowStepStatus.Running;
        step.StartedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Started AgentWorkflowStep {StepId} ({StepType}) in Workflow {WorkflowId}.",
            step.Id, step.StepType, step.WorkflowId);

        return step;
    }

    public async Task<AgentWorkflowStep> CompleteStepAsync(
        Guid stepId,
        string? outputJson = null,
        string? validationJson = null,
        CancellationToken cancellationToken = default)
    {
        var step = await _db.AgentWorkflowSteps.FirstOrDefaultAsync(s => s.Id == stepId, cancellationToken)
            ?? throw new NotFoundException($"AgentWorkflowStep with ID '{stepId}' was not found.");

        step.Status = WorkflowStepStatus.Completed;
        step.OutputJson = outputJson;
        step.ValidationJson = validationJson;
        step.CompletedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Completed AgentWorkflowStep {StepId} ({StepType}) in Workflow {WorkflowId}.",
            step.Id, step.StepType, step.WorkflowId);

        return step;
    }

    public async Task<AgentWorkflowStep> FailStepAsync(
        Guid stepId,
        string errorMessage,
        string? validationJson = null,
        CancellationToken cancellationToken = default)
    {
        var step = await _db.AgentWorkflowSteps.FirstOrDefaultAsync(s => s.Id == stepId, cancellationToken)
            ?? throw new NotFoundException($"AgentWorkflowStep with ID '{stepId}' was not found.");

        step.Status = WorkflowStepStatus.Failed;
        step.ErrorMessage = errorMessage;
        step.ValidationJson = validationJson;
        step.CompletedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogWarning(
            "Failed AgentWorkflowStep {StepId} ({StepType}) in Workflow {WorkflowId}: {Error}",
            step.Id, step.StepType, step.WorkflowId, errorMessage);

        return step;
    }

    public async Task<AgentWorkflowApproval> RecordApprovalAsync(
        Guid workflowId,
        WorkflowApprovalStage stage,
        WorkflowApprovalDecision decision,
        Guid decidedByUserId,
        string? reason = null,
        string? payloadJson = null,
        Guid? stepId = null,
        CancellationToken cancellationToken = default)
    {
        var workflow = await _db.AgentWorkflows.FirstOrDefaultAsync(w => w.Id == workflowId, cancellationToken)
            ?? throw new NotFoundException($"AgentWorkflow with ID '{workflowId}' was not found.");

        var approval = new AgentWorkflowApproval
        {
            Id = Guid.NewGuid(),
            WorkflowId = workflowId,
            WorkflowStepId = stepId,
            ApprovalStage = stage,
            Decision = decision,
            DecisionReason = reason,
            DecisionPayloadJson = payloadJson,
            DecidedByUserId = decidedByUserId,
            DecidedAt = DateTime.UtcNow
        };

        _db.AgentWorkflowApprovals.Add(approval);

        // Participate in aggregate root optimistic concurrency
        workflow.Version++;
        workflow.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Recorded human approval {ApprovalId} for Stage {Stage}, Decision {Decision} in Workflow {WorkflowId}.",
            approval.Id, stage, decision, workflowId);

        return approval;
    }

    public async Task<AgentWorkflowExecutionResult> RecordExecutionResultAsync(
        Guid workflowId,
        WorkflowExecutionType executionType,
        WorkflowExecutionStatus status,
        string? resultJson = null,
        string? errorMessage = null,
        Guid? stepId = null,
        CancellationToken cancellationToken = default)
    {
        var workflow = await _db.AgentWorkflows.FirstOrDefaultAsync(w => w.Id == workflowId, cancellationToken)
            ?? throw new NotFoundException($"AgentWorkflow with ID '{workflowId}' was not found.");

        var result = new AgentWorkflowExecutionResult
        {
            Id = Guid.NewGuid(),
            WorkflowId = workflowId,
            WorkflowStepId = stepId,
            ExecutionType = executionType,
            Status = status,
            ResultJson = resultJson,
            ErrorMessage = errorMessage,
            ExecutedAt = DateTime.UtcNow
        };

        _db.AgentWorkflowExecutionResults.Add(result);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Recorded execution result {ResultId} for Type {Type}, Status {Status} in Workflow {WorkflowId}.",
            result.Id, executionType, status, workflowId);

        return result;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // HUMAN-IN-THE-LOOP APPROVAL GATES (Step 6)
    // ──────────────────────────────────────────────────────────────────────────

    public async Task<AgentWorkflowDetailDto> ApproveCollectionPlanningAsync(
        Guid workflowId,
        ApproveCollectionPlanningRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var workflow = await LoadWorkflowForDecisionAsync(workflowId, cancellationToken);

        EnsureExpectedVersion(workflow, request.ExpectedVersion);

        _stateMachine.EnsureCanTransition(workflow.Status, AgentWorkflowStatus.CollectionApproved);

        if (workflow.Status != AgentWorkflowStatus.AwaitingCollectionApproval)
        {
            throw new InvalidWorkflowTransitionException(workflow.Status, AgentWorkflowStatus.CollectionApproved);
        }

        var latestStep = workflow.Steps
            .Where(s => s.StepType == WorkflowStepType.CollectionPlanning && s.Status == WorkflowStepStatus.Completed)
            .OrderByDescending(s => s.Sequence)
            .FirstOrDefault();

        if (latestStep is null)
        {
            throw new BusinessRuleConflictException("No completed collection planning step was found for this workflow.");
        }

        ValidateCollectionPlanningOutputForApproval(latestStep.OutputJson);

        return await ExecuteHumanDecisionAsync(
            workflow,
            AgentWorkflowStatus.CollectionApproved,
            WorkflowApprovalStage.CollectionPlanning,
            WorkflowApprovalDecision.Approved,
            request.Reason ?? "Collection proposal approved.",
            payloadJson: null,
            stepId: latestStep.Id,
            actorUserId: actorUserId,
            cancellationToken: cancellationToken);
    }

    public async Task<AgentWorkflowDetailDto> RequestCollectionRevisionAsync(
        Guid workflowId,
        RequestCollectionRevisionRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var workflow = await LoadWorkflowForDecisionAsync(workflowId, cancellationToken);

        EnsureExpectedVersion(workflow, request.ExpectedVersion);

        _stateMachine.EnsureCanTransition(workflow.Status, AgentWorkflowStatus.CollectionNeedsRevision);

        if (workflow.Status != AgentWorkflowStatus.AwaitingCollectionApproval)
        {
            throw new InvalidWorkflowTransitionException(workflow.Status, AgentWorkflowStatus.CollectionNeedsRevision);
        }

        var relevantStep = workflow.Steps
            .Where(s => s.StepType == WorkflowStepType.CollectionPlanning)
            .OrderByDescending(s => s.Sequence)
            .FirstOrDefault();

        return await ExecuteHumanDecisionAsync(
            workflow,
            AgentWorkflowStatus.CollectionNeedsRevision,
            WorkflowApprovalStage.CollectionPlanning,
            WorkflowApprovalDecision.RevisionRequested,
            request.Reason,
            payloadJson: null,
            stepId: relevantStep?.Id,
            actorUserId: actorUserId,
            cancellationToken: cancellationToken);
    }

    public async Task<AgentWorkflowDetailDto> RejectCollectionPlanningAsync(
        Guid workflowId,
        RejectCollectionPlanningRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var workflow = await LoadWorkflowForDecisionAsync(workflowId, cancellationToken);

        EnsureExpectedVersion(workflow, request.ExpectedVersion);

        _stateMachine.EnsureCanTransition(workflow.Status, AgentWorkflowStatus.Rejected);

        if (workflow.Status != AgentWorkflowStatus.AwaitingCollectionApproval)
        {
            throw new InvalidWorkflowTransitionException(workflow.Status, AgentWorkflowStatus.Rejected);
        }

        var relevantStep = workflow.Steps
            .Where(s => s.StepType == WorkflowStepType.CollectionPlanning)
            .OrderByDescending(s => s.Sequence)
            .FirstOrDefault();

        return await ExecuteHumanDecisionAsync(
            workflow,
            AgentWorkflowStatus.Rejected,
            WorkflowApprovalStage.CollectionPlanning,
            WorkflowApprovalDecision.Rejected,
            request.Reason,
            payloadJson: null,
            stepId: relevantStep?.Id,
            actorUserId: actorUserId,
            cancellationToken: cancellationToken);
    }

    public async Task<AgentWorkflowDetailDto> ApproveDispatchPlanAsync(
        Guid workflowId,
        ApproveDispatchPlanRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var workflow = await LoadWorkflowForDecisionAsync(workflowId, cancellationToken);

        EnsureExpectedVersion(workflow, request.ExpectedVersion);

        _stateMachine.EnsureCanTransition(workflow.Status, AgentWorkflowStatus.DispatchApproved);

        if (workflow.Status != AgentWorkflowStatus.AwaitingDispatchApproval)
        {
            throw new InvalidWorkflowTransitionException(workflow.Status, AgentWorkflowStatus.DispatchApproved);
        }

        var latestStep = workflow.Steps
            .Where(s => s.StepType == WorkflowStepType.OperationalValidation && s.Status == WorkflowStepStatus.Completed)
            .OrderByDescending(s => s.Sequence)
            .FirstOrDefault();

        if (latestStep is null)
        {
            throw new BusinessRuleConflictException("No completed operational validation step was found for this workflow.");
        }

        ValidateOperationalValidationOutputForApproval(latestStep.OutputJson, request.AcknowledgeWarnings);

        var payloadJson = JsonSerializer.Serialize(
            new { acknowledgeWarnings = request.AcknowledgeWarnings },
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        return await ExecuteHumanDecisionAsync(
            workflow,
            AgentWorkflowStatus.DispatchApproved,
            WorkflowApprovalStage.FleetDispatch,
            WorkflowApprovalDecision.Approved,
            request.Reason ?? "Fleet dispatch proposal approved.",
            payloadJson: payloadJson,
            stepId: latestStep.Id,
            actorUserId: actorUserId,
            cancellationToken: cancellationToken);
    }

    public async Task<AgentWorkflowDetailDto> RequestDispatchRevisionAsync(
        Guid workflowId,
        RequestDispatchRevisionRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var workflow = await LoadWorkflowForDecisionAsync(workflowId, cancellationToken);

        EnsureExpectedVersion(workflow, request.ExpectedVersion);

        _stateMachine.EnsureCanTransition(workflow.Status, AgentWorkflowStatus.DispatchNeedsRevision);

        if (workflow.Status != AgentWorkflowStatus.AwaitingDispatchApproval)
        {
            throw new InvalidWorkflowTransitionException(workflow.Status, AgentWorkflowStatus.DispatchNeedsRevision);
        }

        var relevantStep = workflow.Steps
            .Where(s => s.StepType == WorkflowStepType.OperationalValidation)
            .OrderByDescending(s => s.Sequence)
            .FirstOrDefault();

        return await ExecuteHumanDecisionAsync(
            workflow,
            AgentWorkflowStatus.DispatchNeedsRevision,
            WorkflowApprovalStage.FleetDispatch,
            WorkflowApprovalDecision.RevisionRequested,
            request.Reason,
            payloadJson: null,
            stepId: relevantStep?.Id,
            actorUserId: actorUserId,
            cancellationToken: cancellationToken);
    }

    public async Task<AgentWorkflowDetailDto> RejectDispatchPlanAsync(
        Guid workflowId,
        RejectDispatchPlanRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var workflow = await LoadWorkflowForDecisionAsync(workflowId, cancellationToken);

        EnsureExpectedVersion(workflow, request.ExpectedVersion);

        _stateMachine.EnsureCanTransition(workflow.Status, AgentWorkflowStatus.Rejected);

        if (workflow.Status != AgentWorkflowStatus.AwaitingDispatchApproval)
        {
            throw new InvalidWorkflowTransitionException(workflow.Status, AgentWorkflowStatus.Rejected);
        }

        var relevantStep = workflow.Steps
            .Where(s => s.StepType == WorkflowStepType.OperationalValidation)
            .OrderByDescending(s => s.Sequence)
            .FirstOrDefault();

        return await ExecuteHumanDecisionAsync(
            workflow,
            AgentWorkflowStatus.Rejected,
            WorkflowApprovalStage.FleetDispatch,
            WorkflowApprovalDecision.Rejected,
            request.Reason,
            payloadJson: null,
            stepId: relevantStep?.Id,
            actorUserId: actorUserId,
            cancellationToken: cancellationToken);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // APPROVAL HELPERS & DETERMINISTIC SPECIALIST JSON GUARDS
    // ──────────────────────────────────────────────────────────────────────────

    private async Task<AgentWorkflow> LoadWorkflowForDecisionAsync(Guid workflowId, CancellationToken cancellationToken)
    {
        return await _db.AgentWorkflows
            .Include(w => w.Steps)
            .Include(w => w.Transitions)
            .Include(w => w.Approvals)
            .Include(w => w.ExecutionResults)
            .FirstOrDefaultAsync(w => w.Id == workflowId, cancellationToken)
            ?? throw new NotFoundException($"AgentWorkflow with ID '{workflowId}' was not found.");
    }

    private static void EnsureExpectedVersion(AgentWorkflow workflow, int expectedVersion)
    {
        if (workflow.Version != expectedVersion)
        {
            throw new BusinessRuleConflictException(
                "The workflow has changed since it was loaded. Reload the workflow before submitting this decision.");
        }
    }

    private async Task<AgentWorkflowDetailDto> ExecuteHumanDecisionAsync(
        AgentWorkflow workflow,
        AgentWorkflowStatus toStatus,
        WorkflowApprovalStage stage,
        WorkflowApprovalDecision decision,
        string? reason,
        string? payloadJson,
        Guid? stepId,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var fromStatus = workflow.Status;

        var approval = new AgentWorkflowApproval
        {
            Id = Guid.NewGuid(),
            WorkflowId = workflow.Id,
            WorkflowStepId = stepId,
            ApprovalStage = stage,
            Decision = decision,
            DecisionReason = reason,
            DecisionPayloadJson = payloadJson,
            DecidedByUserId = actorUserId,
            DecidedAt = DateTime.UtcNow
        };

        var transition = new AgentWorkflowTransition
        {
            Id = Guid.NewGuid(),
            WorkflowId = workflow.Id,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            Reason = reason,
            ChangedByUserId = actorUserId,
            ChangedAt = DateTime.UtcNow
        };

        _db.AgentWorkflowApprovals.Add(approval);
        _db.AgentWorkflowTransitions.Add(transition);

        workflow.Status = toStatus;
        workflow.Version++;
        workflow.UpdatedAt = DateTime.UtcNow;

        if (_stateMachine.IsTerminal(toStatus))
        {
            workflow.CompletedAt = DateTime.UtcNow;
        }

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Concurrency conflict saving human decision on AgentWorkflow {WorkflowId}.", workflow.Id);
            throw new BusinessRuleConflictException(
                "The workflow was modified concurrently by another operation. Please reload and try again.");
        }

        _logger.LogInformation(
            "Human decision recorded on AgentWorkflow {WorkflowId} for Stage {Stage}: Decision={Decision}, NewStatus={NewStatus}, Version={Version}.",
            workflow.Id, stage, decision, toStatus, workflow.Version);

        return MapToDetailDto(workflow);
    }

    private void ValidateCollectionPlanningOutputForApproval(string? outputJson)
    {
        if (string.IsNullOrWhiteSpace(outputJson))
        {
            throw new BusinessRuleConflictException("The collection planning step has no output payload.");
        }

        try
        {
            using var doc = JsonDocument.Parse(outputJson);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new BusinessRuleConflictException("The collection planning step output is not a valid JSON object.");
            }

            // Check status == "completed"
            string? status = null;
            if (root.TryGetProperty("status", out var statusProp) && statusProp.ValueKind == JsonValueKind.String)
            {
                status = statusProp.GetString();
            }

            if (!string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase))
            {
                throw new BusinessRuleConflictException(
                    $"The collection proposal status is '{status ?? "missing"}' and is not approval-ready.");
            }

            // Check isCompleteSnapshot == true
            bool isCompleteSnapshot = false;
            if (root.TryGetProperty("isCompleteSnapshot", out var snapProp) ||
                root.TryGetProperty("is_complete_snapshot", out snapProp))
            {
                isCompleteSnapshot = snapProp.ValueKind == JsonValueKind.True;
            }

            if (!isCompleteSnapshot)
            {
                throw new BusinessRuleConflictException(
                    "The collection proposal is not based on a complete authoritative snapshot and cannot be approved.");
            }

            // Validate every available executable schedule before recording the human decision.
            // Legacy payloads without canonical arrays retain their existing approval behavior;
            // task execution still requires and validates the complete canonical plan.
            var nowUtc = _timeProvider.GetUtcNow();
            foreach (var collectionName in new[] { "candidateGroups", "separateHandling" })
            {
                if (!root.TryGetProperty(collectionName, out var collection))
                {
                    continue;
                }
                if (collection.ValueKind != JsonValueKind.Array)
                {
                    throw new BusinessRuleConflictException($"The collection proposal has a malformed {collectionName} collection.");
                }

                foreach (var item in collection.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object ||
                        !item.TryGetProperty("proposedSchedule", out var schedule) ||
                        schedule.ValueKind != JsonValueKind.Object ||
                        !schedule.TryGetProperty("scheduledAt", out var timestamp))
                    {
                        throw new BusinessRuleConflictException("An executable collection proposal is missing its scheduledAt time.");
                    }

                    _collectionSchedulePolicy.ValidateProposedStart(
                        timestamp.ValueKind == JsonValueKind.String ? timestamp.GetString() : null, nowUtc);
                }
            }
        }
        catch (JsonException ex)
        {
            throw new BusinessRuleConflictException("The collection planning step output payload is malformed.", ex);
        }
    }

    private static void ValidateOperationalValidationOutputForApproval(string? outputJson, bool acknowledgeWarnings)
    {
        if (string.IsNullOrWhiteSpace(outputJson))
        {
            throw new BusinessRuleConflictException("The operational validation step has no output payload.");
        }

        try
        {
            using var doc = JsonDocument.Parse(outputJson);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new BusinessRuleConflictException("The operational validation step output is not a valid JSON object.");
            }

            // Check validationOutcome == "ReadyForHumanReview"
            string? outcome = null;
            if (root.TryGetProperty("validationOutcome", out var outcomeProp) &&
                outcomeProp.ValueKind == JsonValueKind.String)
            {
                outcome = outcomeProp.GetString();
            }

            if (!string.Equals(outcome, "ReadyForHumanReview", StringComparison.OrdinalIgnoreCase))
            {
                throw new BusinessRuleConflictException(
                    $"The dispatch proposal validation outcome is '{outcome ?? "missing"}' and cannot be approved without revision.");
            }

            // Check requiresAcknowledgement
            bool requiresAck = false;
            if (root.TryGetProperty("requiresAcknowledgement", out var ackProp))
            {
                requiresAck = ackProp.ValueKind == JsonValueKind.True;
            }

            if (requiresAck && !acknowledgeWarnings)
            {
                throw new BusinessRuleConflictException(
                    "The dispatch proposal contains operational warnings that must be explicitly acknowledged before approval.");
            }
        }
        catch (JsonException ex)
        {
            throw new BusinessRuleConflictException("The operational validation step output payload is malformed.", ex);
        }
    }

    private static AgentWorkflowDetailDto MapToDetailDto(AgentWorkflow workflow)
    {
        return new AgentWorkflowDetailDto
        {
            Id = workflow.Id,
            TriggerType = workflow.TriggerType,
            TriggeringWasteReportId = workflow.TriggeringWasteReportId,
            Objective = workflow.Objective,
            Status = workflow.Status,
            CurrentStep = workflow.CurrentStep,
            InitiatedByUserId = workflow.InitiatedByUserId,
            CreatedAt = workflow.CreatedAt,
            UpdatedAt = workflow.UpdatedAt,
            CompletedAt = workflow.CompletedAt,
            FinalOutcome = workflow.FinalOutcome,
            Version = workflow.Version,
            Steps = workflow.Steps
                .OrderBy(s => s.Sequence)
                .Select(s => new AgentWorkflowStepDto
                {
                    Id = s.Id,
                    Sequence = s.Sequence,
                    StepType = s.StepType,
                    AgentName = s.AgentName,
                    Status = s.Status,
                    Input = JsonElementHelper.ParseJsonElement(s.InputJson),
                    Output = JsonElementHelper.ParseJsonElement(s.OutputJson),
                    Validation = JsonElementHelper.ParseJsonElement(s.ValidationJson),
                    ErrorMessage = s.ErrorMessage,
                    StartedAt = s.StartedAt,
                    CompletedAt = s.CompletedAt
                })
                .ToList(),
            Transitions = workflow.Transitions
                .OrderBy(t => t.ChangedAt)
                .Select(t => new AgentWorkflowTransitionDto
                {
                    Id = t.Id,
                    FromStatus = t.FromStatus,
                    ToStatus = t.ToStatus,
                    Reason = t.Reason,
                    ChangedByUserId = t.ChangedByUserId,
                    ChangedAt = t.ChangedAt
                })
                .ToList(),
            Approvals = workflow.Approvals
                .OrderBy(a => a.DecidedAt)
                .Select(a => new AgentWorkflowApprovalDto
                {
                    Id = a.Id,
                    WorkflowStepId = a.WorkflowStepId,
                    ApprovalStage = a.ApprovalStage,
                    Decision = a.Decision,
                    DecisionReason = a.DecisionReason,
                    DecisionPayload = JsonElementHelper.ParseJsonElement(a.DecisionPayloadJson),
                    DecidedByUserId = a.DecidedByUserId,
                    DecidedAt = a.DecidedAt
                })
                .ToList(),
            ExecutionResults = workflow.ExecutionResults
                .OrderBy(e => e.ExecutedAt)
                .Select(e => new AgentWorkflowExecutionResultDto
                {
                    Id = e.Id,
                    WorkflowStepId = e.WorkflowStepId,
                    ExecutionType = e.ExecutionType,
                    Status = e.Status,
                    Result = JsonElementHelper.ParseJsonElement(e.ResultJson),
                    ErrorMessage = e.ErrorMessage,
                    ExecutedAt = e.ExecutedAt
                })
                .ToList()
        };
    }

    public async Task<AgentWorkflowDetailDto> ExecuteCollectionPlanAsync(
        Guid workflowId,
        ExecuteCollectionPlanRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var workflow = await LoadWorkflowForDecisionAsync(workflowId, cancellationToken);

        // 1. Expected version check
        EnsureExpectedVersion(workflow, request.ExpectedVersion);

        // 2. Idempotency check: if Succeeded CollectionTaskCreation execution result already exists, return current details
        var existingSuccess = workflow.ExecutionResults
            .FirstOrDefault(e => e.ExecutionType == WorkflowExecutionType.CollectionTaskCreation &&
                                 e.Status == WorkflowExecutionStatus.Succeeded);
        if (existingSuccess != null)
        {
            if (workflow.Status == AgentWorkflowStatus.Failed &&
                await HasRecoverablePostCollectionResumeFailureAsync(workflow, existingSuccess, cancellationToken))
            {
                return await ResumeCommittedCollectionPlanAsync(workflow, existingSuccess, actorUserId, cancellationToken);
            }

            _logger.LogInformation(
                "Idempotent execution retry: AgentWorkflow {WorkflowId} already completed CollectionTaskCreation execution {ExecutionId}.",
                workflow.Id, existingSuccess.Id);
            return MapToDetailDto(workflow);
        }

        // 3. Workflow status check: must be CollectionApproved (or resumable CreatingScheduledTasks with Pending execution)
        var isResuming = workflow.Status == AgentWorkflowStatus.CreatingScheduledTasks &&
                         workflow.ExecutionResults.Any(e => e.ExecutionType == WorkflowExecutionType.CollectionTaskCreation &&
                                                            e.Status == WorkflowExecutionStatus.Pending);

        if (workflow.Status != AgentWorkflowStatus.CollectionApproved && !isResuming)
        {
            throw new BusinessRuleConflictException(
                $"Cannot execute collection plan for workflow '{workflowId}' in status '{workflow.Status}'. Execution requires 'CollectionApproved' (or resumable 'CreatingScheduledTasks').");
        }

        // 4. Verify human approval exists for CollectionPlanning
        var approval = workflow.Approvals
            .Where(a => a.ApprovalStage == WorkflowApprovalStage.CollectionPlanning &&
                        a.Decision == WorkflowApprovalDecision.Approved)
            .OrderByDescending(a => a.DecidedAt)
            .FirstOrDefault();

        if (approval is null)
        {
            throw new BusinessRuleConflictException("Workflow has no recorded human approval for collection planning.");
        }

        // 5. Resolve C2 CollectionPlanning step bound by approval
        if (!approval.WorkflowStepId.HasValue)
        {
            throw new BusinessRuleConflictException("The recorded approval is not bound to a specific collection planning workflow step.");
        }

        var c2Step = workflow.Steps.FirstOrDefault(s => s.Id == approval.WorkflowStepId.Value &&
                                                        s.StepType == WorkflowStepType.CollectionPlanning);
        if (c2Step is null)
        {
            throw new BusinessRuleConflictException("The collection planning step linked by the approval was not found.");
        }

        if (string.IsNullOrWhiteSpace(c2Step.OutputJson))
        {
            throw new BusinessRuleConflictException("The approved collection planning step contains no structured output.");
        }

        // 6. Flatten and validate all approved schedules before any mutation.
        var nowUtc = _timeProvider.GetUtcNow();
        var (instructions, deferredNeedCount) = ParseAndFlattenCollectionPlan(
            c2Step.OutputJson, _collectionSchedulePolicy, nowUtc);

        if (instructions.Count == 0)
        {
            throw new BusinessRuleConflictException(
                "Approved collection plan contains zero executable collection needs to schedule (all needs are deferred or empty). Cannot create collection tasks.");
        }

        // 7. Revalidate required scheduling reasons before any mutation.
        foreach (var inst in instructions)
        {
            if (string.IsNullOrWhiteSpace(inst.SchedulingReason))
            {
                throw new BusinessRuleConflictException(
                    $"Proposed schedule for collection need '{inst.NeedId}' is missing a required scheduling reason.");
            }
        }

        // 8. Fresh authoritative need validation before beginning mutations
        foreach (var inst in instructions)
        {
            if (string.Equals(inst.TargetType, "Report", StringComparison.OrdinalIgnoreCase))
            {
                var report = await _db.WasteReports
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r => r.Id == inst.NeedId, cancellationToken);

                if (report is null)
                {
                    throw new BusinessRuleConflictException($"Referenced waste report '{inst.NeedId}' was not found in authoritative records.");
                }

                if (report.Status != WasteReportStatus.Verified)
                {
                    throw new BusinessRuleConflictException(
                        $"Cannot schedule collection task for waste report '{inst.NeedId}'. Current status is '{report.Status}', but 'Verified' is required.");
                }

                var hasActiveTask = await _db.CollectionTasks
                    .AsNoTracking()
                    .AnyAsync(t => t.WasteReportId == inst.NeedId &&
                                   (t.Status == CollectionTaskStatus.Scheduled ||
                                    t.Status == CollectionTaskStatus.Assigned ||
                                    t.Status == CollectionTaskStatus.InProgress), cancellationToken);

                if (hasActiveTask)
                {
                    throw new BusinessRuleConflictException($"Waste report '{inst.NeedId}' already has an active collection task.");
                }
            }
            else if (string.Equals(inst.TargetType, "Bin", StringComparison.OrdinalIgnoreCase))
            {
                var bin = await _db.WasteBins
                    .AsNoTracking()
                    .FirstOrDefaultAsync(b => b.Id == inst.NeedId, cancellationToken);

                if (bin is null)
                {
                    throw new BusinessRuleConflictException($"Referenced waste bin '{inst.NeedId}' was not found in authoritative records.");
                }

                if (bin.AdministrativeStatus != BinAdministrativeStatus.Active)
                {
                    throw new BusinessRuleConflictException(
                        $"Cannot schedule collection task for waste bin '{inst.NeedId}'. Bin administrative status is '{bin.AdministrativeStatus}', but 'Active' is required.");
                }

                var hasActiveTask = await _db.CollectionTasks
                    .AsNoTracking()
                    .AnyAsync(t => t.WasteBinId == inst.NeedId &&
                                   (t.Status == CollectionTaskStatus.Scheduled ||
                                    t.Status == CollectionTaskStatus.Assigned ||
                                    t.Status == CollectionTaskStatus.InProgress), cancellationToken);

                if (hasActiveTask)
                {
                    throw new BusinessRuleConflictException($"Waste bin '{inst.NeedId}' already has an active collection task.");
                }
            }
            else
            {
                throw new BusinessRuleConflictException($"Unsupported target type '{inst.TargetType}' in collection need reference.");
            }
        }

        // 9. Phase A: Persist execution start (CreatingScheduledTasks)
        AgentWorkflowStep step;
        AgentWorkflowExecutionResult executionResult;

        if (!isResuming)
        {
            _stateMachine.EnsureCanTransition(workflow.Status, AgentWorkflowStatus.CreatingScheduledTasks);

            var fromStatus = workflow.Status;
            workflow.Status = AgentWorkflowStatus.CreatingScheduledTasks;
            workflow.CurrentStep = WorkflowStepType.ScheduledTaskCreation;
            workflow.Version++;
            workflow.UpdatedAt = DateTime.UtcNow;

            var startTransition = new AgentWorkflowTransition
            {
                Id = Guid.NewGuid(),
                WorkflowId = workflow.Id,
                FromStatus = fromStatus,
                ToStatus = AgentWorkflowStatus.CreatingScheduledTasks,
                Reason = "Starting authoritative creation of scheduled collection tasks.",
                ChangedByUserId = actorUserId,
                ChangedAt = DateTime.UtcNow
            };
            _db.AgentWorkflowTransitions.Add(startTransition);

            var nextSeq = (workflow.Steps.Count > 0 ? workflow.Steps.Max(s => s.Sequence) : 0) + 1;
            step = new AgentWorkflowStep
            {
                Id = Guid.NewGuid(),
                WorkflowId = workflow.Id,
                Sequence = nextSeq,
                StepType = WorkflowStepType.ScheduledTaskCreation,
                AgentName = null,
                Status = WorkflowStepStatus.Running,
                InputJson = JsonSerializer.Serialize(new
                {
                    sourceCollectionPlanningStepId = c2Step.Id,
                    approvedExecutableNeedCount = instructions.Count,
                    deferredNeedCount = deferredNeedCount
                }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
                StartedAt = DateTime.UtcNow
            };
            _db.AgentWorkflowSteps.Add(step);

            executionResult = new AgentWorkflowExecutionResult
            {
                Id = Guid.NewGuid(),
                WorkflowId = workflow.Id,
                WorkflowStepId = step.Id,
                ExecutionType = WorkflowExecutionType.CollectionTaskCreation,
                Status = WorkflowExecutionStatus.Pending,
                ExecutedAt = DateTime.UtcNow
            };
            _db.AgentWorkflowExecutionResults.Add(executionResult);

            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "Concurrency conflict starting collection plan execution on AgentWorkflow {WorkflowId}.", workflow.Id);
                throw new BusinessRuleConflictException("The workflow was modified concurrently by another operation. Please reload and try again.");
            }
        }
        else
        {
            step = workflow.Steps
                .Where(s => s.StepType == WorkflowStepType.ScheduledTaskCreation && s.Status == WorkflowStepStatus.Running)
                .OrderByDescending(s => s.Sequence)
                .FirstOrDefault()
                ?? throw new BusinessRuleConflictException("Resumable ScheduledTaskCreation step was not found.");

            executionResult = workflow.ExecutionResults
                .Where(e => e.ExecutionType == WorkflowExecutionType.CollectionTaskCreation && e.Status == WorkflowExecutionStatus.Pending)
                .OrderByDescending(e => e.ExecutedAt)
                .FirstOrDefault()
                ?? throw new BusinessRuleConflictException("Resumable Pending CollectionTaskCreation execution result was not found.");
        }

        // 10. Phase B: Authoritative Task Creation Transaction
        if (_collectionTaskService is null)
        {
            throw new InvalidOperationException("ICollectionTaskService is not configured for AgentWorkflowService.");
        }

        var isInMemory = _db.Database.ProviderName?.Contains("InMemory") == true;
        await using var tx = isInMemory ? null : await _db.Database.BeginTransactionAsync(cancellationToken);

        var createdTasks = new List<CreatedTaskAuditItem>();

        try
        {
            foreach (var inst in instructions)
            {
                var isReport = string.Equals(inst.TargetType, "Report", StringComparison.OrdinalIgnoreCase);

                var collectionReason = isReport
                    ? CollectionReason.VerifiedReport
                    : (Enum.TryParse<CollectionReason>(inst.CollectionReason, true, out var cr) && cr != CollectionReason.VerifiedReport
                        ? cr
                        : CollectionReason.FullOrBlockedBin);

                var taskRequest = new CreateManualCollectionTaskRequest
                {
                    WasteReportId = isReport ? inst.NeedId : null,
                    WasteBinId = isReport ? null : inst.NeedId,
                    CollectionReason = collectionReason,
                    ScheduledAt = inst.ScheduledAt,
                    SchedulingReason = inst.SchedulingReason,
                    HandlingNotes = inst.HandlingNotes
                };

                var taskDetail = await _collectionTaskService.CreateTaskFromApprovedPlanAsync(
                    taskRequest,
                    actorUserId,
                    cancellationToken);

                createdTasks.Add(new CreatedTaskAuditItem
                {
                    CollectionNeedId = inst.NeedId,
                    CollectionTaskId = taskDetail.Id,
                    TaskCode = taskDetail.TaskCode,
                    TargetType = inst.TargetType,
                    TargetId = inst.NeedId,
                    ScheduledAt = taskDetail.ScheduledAt
                });
            }

            if (tx != null)
            {
                await tx.CommitAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authoritative collection task creation failed for AgentWorkflow {WorkflowId}.", workflow.Id);

            if (tx != null)
            {
                await tx.RollbackAsync(cancellationToken);
            }

            await RecordExecutionFailureAsync(
                workflow,
                step,
                executionResult,
                actorUserId,
                ex.Message,
                cancellationToken);

            throw;
        }

        // 11. Complete Phase B: Transition to FleetPlanning
        _stateMachine.EnsureCanTransition(workflow.Status, AgentWorkflowStatus.FleetPlanning);

        var preSuccessStatus = workflow.Status;
        workflow.Status = AgentWorkflowStatus.FleetPlanning;
        workflow.CurrentStep = WorkflowStepType.FleetPlanning;
        workflow.Version++;
        workflow.UpdatedAt = DateTime.UtcNow;

        var successTransition = new AgentWorkflowTransition
        {
            Id = Guid.NewGuid(),
            WorkflowId = workflow.Id,
            FromStatus = preSuccessStatus,
            ToStatus = AgentWorkflowStatus.FleetPlanning,
            Reason = $"Successfully created {createdTasks.Count} scheduled collection tasks.",
            ChangedByUserId = actorUserId,
            ChangedAt = DateTime.UtcNow
        };
        _db.AgentWorkflowTransitions.Add(successTransition);

        step.Status = WorkflowStepStatus.Completed;
        step.CompletedAt = DateTime.UtcNow;
        step.OutputJson = JsonSerializer.Serialize(new
        {
            sourceCollectionPlanningStepId = c2Step.Id,
            createdTaskCount = createdTasks.Count,
            deferredNeedCount = deferredNeedCount,
            createdTaskIds = createdTasks.Select(t => t.CollectionTaskId).ToList()
        }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        executionResult.Status = WorkflowExecutionStatus.Succeeded;
        executionResult.ResultJson = JsonSerializer.Serialize(new
        {
            createdTaskCount = createdTasks.Count,
            deferredNeedCount = deferredNeedCount,
            sourceCollectionPlanningStepId = c2Step.Id,
            createdTasks = createdTasks
        }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Concurrency conflict completing collection plan execution on AgentWorkflow {WorkflowId}.", workflow.Id);
            throw new BusinessRuleConflictException("The workflow was modified concurrently by another operation. Please reload and try again.");
        }

        _logger.LogInformation(
            "AgentWorkflow {WorkflowId} successfully executed collection plan: created {Count} tasks, transitioned to FleetPlanning (Version: {Version}).",
            workflow.Id, createdTasks.Count, workflow.Version);

        if (_pythonOrchestrationClient is not null)
        {
            if (!TryBuildPythonResumeRequest(workflow, executionResult, out var resumeRequest))
            {
                _logger.LogWarning(
                    "AgentWorkflow {WorkflowId} has no complete persisted first-half Python snapshot; preserving the existing C2 execution result without a resume call.",
                    workflow.Id);
            }
            else
            {
                try
                {
                    var resumed = await _pythonOrchestrationClient.ResumeAfterCollectionApprovalAsync(resumeRequest!, cancellationToken);
                    workflow = await PersistSecondHalfAsync(workflow.Id, resumed, actorUserId, cancellationToken);
                }
                catch (Exception ex) when (ex is AiServiceUnavailableException or BusinessRuleConflictException)
                {
                    await FailWorkflowSafelyAsync(workflow.Id, "Internal AI orchestration resume failed after scheduled task creation.", actorUserId, cancellationToken);
                    throw;
                }
            }
        }

        return MapToDetailDto(workflow);
    }

    private async Task<bool> HasRecoverablePostCollectionResumeFailureAsync(
        AgentWorkflow workflow,
        AgentWorkflowExecutionResult executionResult,
        CancellationToken cancellationToken)
    {
        if (workflow.CurrentStep != WorkflowStepType.FleetPlanning ||
            workflow.Steps.Any(s => s.StepType is WorkflowStepType.FleetPlanning or WorkflowStepType.OperationalValidation) ||
            workflow.Approvals.Any(a => a.ApprovalStage == WorkflowApprovalStage.FleetDispatch) ||
            workflow.ExecutionResults.Any(e => e.ExecutionType == WorkflowExecutionType.CollectionAssignment) ||
            workflow.Transitions.OrderByDescending(t => t.ChangedAt).FirstOrDefault(t => t.ToStatus == AgentWorkflowStatus.Failed)
                is not { FromStatus: AgentWorkflowStatus.FleetPlanning,
                    Reason: "Internal AI orchestration resume failed after scheduled task creation." } ||
            string.IsNullOrWhiteSpace(executionResult.ResultJson))
            return false;

        var approval = workflow.Approvals
            .Where(a => a.ApprovalStage == WorkflowApprovalStage.CollectionPlanning &&
                        a.Decision == WorkflowApprovalDecision.Approved)
            .OrderByDescending(a => a.DecidedAt)
            .FirstOrDefault();
        if (approval?.WorkflowStepId is not Guid c2StepId ||
            !workflow.Steps.Any(s => s.Id == c2StepId && s.StepType == WorkflowStepType.CollectionPlanning &&
                                     s.Status == WorkflowStepStatus.Completed) ||
            !workflow.Steps.Any(s => s.Id == executionResult.WorkflowStepId &&
                                     s.StepType == WorkflowStepType.ScheduledTaskCreation &&
                                     s.Status == WorkflowStepStatus.Completed))
            return false;

        try
        {
            using var document = JsonDocument.Parse(executionResult.ResultJson);
            var root = document.RootElement;
            if (!root.TryGetProperty("sourceCollectionPlanningStepId", out var sourceStep) ||
                sourceStep.GetGuid() != c2StepId ||
                !root.TryGetProperty("createdTaskCount", out var countElement) ||
                !countElement.TryGetInt32(out var count) || count < 1 ||
                !root.TryGetProperty("createdTasks", out var createdTasks) ||
                createdTasks.ValueKind != JsonValueKind.Array || createdTasks.GetArrayLength() != count)
                return false;

            var taskIds = new HashSet<Guid>();
            foreach (var item in createdTasks.EnumerateArray())
            {
                if (!item.TryGetProperty("collectionTaskId", out var idElement) ||
                    !idElement.TryGetGuid(out var taskId) || taskId == Guid.Empty ||
                    !taskIds.Add(taskId))
                    return false;
            }

            var scheduledTaskIds = await _db.CollectionTasks.AsNoTracking()
                .Where(t => taskIds.Contains(t.Id) && t.Status == CollectionTaskStatus.Scheduled)
                .Select(t => t.Id)
                .ToListAsync(cancellationToken);
            return scheduledTaskIds.Count == count && taskIds.SetEquals(scheduledTaskIds);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            return false;
        }
    }

    private async Task<AgentWorkflowDetailDto> ResumeCommittedCollectionPlanAsync(
        AgentWorkflow workflow,
        AgentWorkflowExecutionResult executionResult,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        if (_pythonOrchestrationClient is null ||
            !TryBuildPythonResumeRequest(workflow, executionResult, out var resumeRequest))
            throw new BusinessRuleConflictException("The committed collection workflow cannot be safely resumed from its persisted snapshot.");

        // This is the sole exception to terminal Failed semantics: a proven post-commit
        // transport failure with the exact approved task IDs already persisted.
        workflow.Status = AgentWorkflowStatus.FleetPlanning;
        workflow.CurrentStep = WorkflowStepType.FleetPlanning;
        workflow.CompletedAt = null;
        workflow.Version++;
        workflow.UpdatedAt = DateTime.UtcNow;
        _db.AgentWorkflowTransitions.Add(new AgentWorkflowTransition
        {
            Id = Guid.NewGuid(),
            WorkflowId = workflow.Id,
            FromStatus = AgentWorkflowStatus.Failed,
            ToStatus = AgentWorkflowStatus.FleetPlanning,
            Reason = "Retrying post-collection-execution AI continuation without creating collection tasks.",
            ChangedByUserId = actorUserId,
            ChangedAt = DateTime.UtcNow
        });
        await SaveOrThrowConcurrencyAsync("recovering post-collection AI continuation", workflow.Id, cancellationToken);

        try
        {
            var resumed = await _pythonOrchestrationClient.ResumeAfterCollectionApprovalAsync(resumeRequest!, cancellationToken);
            workflow = await PersistSecondHalfAsync(workflow.Id, resumed, actorUserId, cancellationToken);
        }
        catch (Exception ex) when (ex is AiServiceUnavailableException or BusinessRuleConflictException)
        {
            await FailWorkflowSafelyAsync(workflow.Id, "Internal AI orchestration resume failed after scheduled task creation.", actorUserId, cancellationToken);
            throw;
        }

        return MapToDetailDto(workflow);
    }

    private static bool TryBuildPythonResumeRequest(AgentWorkflow workflow, AgentWorkflowExecutionResult executionResult, out PythonWorkflowResumeRequest? request)
    {
        request = null;
        var planner = workflow.Steps.SingleOrDefault(s => s.StepType == WorkflowStepType.SharedPlanning && s.Status == WorkflowStepStatus.Completed);
        var c1 = workflow.Steps.SingleOrDefault(s => s.StepType == WorkflowStepType.WasteAnalysis && s.Status == WorkflowStepStatus.Completed);
        var c2 = workflow.Steps.SingleOrDefault(s => s.StepType == WorkflowStepType.CollectionPlanning && s.Status == WorkflowStepStatus.Completed);
        if (planner?.OutputJson is null || c1?.OutputJson is null || c2?.OutputJson is null || string.IsNullOrWhiteSpace(executionResult.ResultJson))
            return false;

        try
        {
            JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement.Clone();
            var warnings = planner.ValidationJson is null
                ? JsonSerializer.SerializeToElement(Array.Empty<string>())
                : Parse(planner.ValidationJson);
            if (warnings.ValueKind != JsonValueKind.Array)
                return false;
            request = new PythonWorkflowResumeRequest
            {
                Workflow = new PythonOrchestrationEnvelope { WorkflowId = workflow.Id, Objective = workflow.Objective, TriggerType = workflow.TriggerType, TriggeringWasteReportId = workflow.TriggeringWasteReportId, Status = "Paused", CurrentPhase = "PausedForCollectionApproval", ApprovalStage = "CollectionPlanning", PauseReason = "Collection planning is ready for authorized human review.", PlannerResult = Parse(planner.OutputJson), WasteAnalysisResult = Parse(c1.OutputJson), CollectionPlanningResult = Parse(c2.OutputJson), CompletedSpecialists = ["WasteAnalysis", "CollectionPlanning"], Errors = JsonSerializer.SerializeToElement(Array.Empty<object>()), Warnings = warnings },
                ResumeContext = new PythonResumeContext { WorkflowId = workflow.Id, ApprovalStage = "CollectionPlanning", Decision = "Approved", AuthoritativeExecutionSummary = Parse(executionResult.ResultJson) }
            };
            return true;
        }
        catch (JsonException)
        {
            request = null;
            return false;
        }
    }

    private async Task<AgentWorkflow> PersistSecondHalfAsync(Guid workflowId, PythonOrchestrationEnvelope result, Guid actorUserId, CancellationToken cancellationToken)
    {
        var workflow = await _db.AgentWorkflows.Include(w => w.Steps).FirstAsync(w => w.Id == workflowId, cancellationToken);
        if (result.WorkflowId != workflowId || result.Objective != workflow.Objective ||
            (workflow.TriggerType == AgentWorkflowTriggerType.CitizenReportSubmission &&
             (result.TriggerType != workflow.TriggerType || result.TriggeringWasteReportId != workflow.TriggeringWasteReportId)))
            throw new AiServiceUnavailableException("Internal AI resume response does not match the workflow.");

        if (result.CurrentPhase == "Failed")
        {
            PersistReturnedSecondHalfArtifacts(workflow, result);
            PersistSafeFailureDiagnostic(workflow, result, WorkflowStepType.FleetPlanning, "python_orchestration");
            MarkWorkflowFailed(workflow, "Internal AI orchestration returned a failed resume result.", actorUserId);
            await SaveOrThrowConcurrencyAsync("persisting a failed internal AI resume result", workflowId, cancellationToken);
            return workflow;
        }

        var ready = result.CurrentPhase == "PausedForDispatchApproval";
        var revision = result.CurrentPhase == "DispatchNeedsRevision";
        var expectedStatus = ready ? "Paused" : "Running";
        var expectedApprovalStage = ready ? "FleetDispatch" : "None";
        var expectedOutcome = ready ? "ReadyForHumanReview" : "NeedsRevision";
        if (workflow.Status != AgentWorkflowStatus.FleetPlanning ||
            workflow.Steps.Any(s => s.StepType is WorkflowStepType.FleetPlanning or WorkflowStepType.OperationalValidation) ||
            (!ready && !revision) ||
            result.Status != expectedStatus ||
            result.ApprovalStage != expectedApprovalStage ||
            result.FleetRouteResult is null ||
            result.ValidationOperationsResult is null ||
            !result.CompletedSpecialists.SequenceEqual(["WasteAnalysis", "CollectionPlanning", "FleetRoute", "ValidationOperations"]) ||
            !HasValidationOutcome(result.ValidationOperationsResult.Value, expectedOutcome))
            throw new AiServiceUnavailableException("Internal AI orchestration returned an unexpected resume result.");

        _stateMachine.EnsureCanTransition(workflow.Status, AgentWorkflowStatus.OperationalValidation);
        var fleetPlanningStatus = workflow.Status;
        workflow.Status = AgentWorkflowStatus.OperationalValidation;
        workflow.CurrentStep = WorkflowStepType.OperationalValidation;
        var seq = workflow.Steps.Count == 0 ? 0 : workflow.Steps.Max(s => s.Sequence);
        var c3 = new AgentWorkflowStep { Id = Guid.NewGuid(), WorkflowId = workflowId, Sequence = ++seq, StepType = WorkflowStepType.FleetPlanning, AgentName = "fleet_route_agent", Status = WorkflowStepStatus.Completed, OutputJson = result.FleetRouteResult.Value.GetRawText(), StartedAt = DateTime.UtcNow, CompletedAt = DateTime.UtcNow };
        var c4 = new AgentWorkflowStep { Id = Guid.NewGuid(), WorkflowId = workflowId, Sequence = ++seq, StepType = WorkflowStepType.OperationalValidation, AgentName = "validation_operations_agent", Status = WorkflowStepStatus.Completed, InputJson = result.FleetRouteResult.Value.GetRawText(), OutputJson = result.ValidationOperationsResult.Value.GetRawText(), StartedAt = DateTime.UtcNow, CompletedAt = DateTime.UtcNow };
        _db.AgentWorkflowSteps.AddRange(c3, c4);
        _db.AgentWorkflowTransitions.Add(new AgentWorkflowTransition { Id = Guid.NewGuid(), WorkflowId = workflowId, FromStatus = fleetPlanningStatus, ToStatus = AgentWorkflowStatus.OperationalValidation, Reason = "Python resumed fleet and operational validation.", ChangedByUserId = actorUserId, ChangedAt = DateTime.UtcNow });
        var finalStatus = ready ? AgentWorkflowStatus.AwaitingDispatchApproval : AgentWorkflowStatus.DispatchNeedsRevision;
        _stateMachine.EnsureCanTransition(AgentWorkflowStatus.OperationalValidation, finalStatus);
        workflow.Status = finalStatus;
        workflow.CurrentStep = ready ? WorkflowStepType.DispatchApproval : WorkflowStepType.OperationalValidation;
        workflow.Version++;
        workflow.UpdatedAt = DateTime.UtcNow;
        _db.AgentWorkflowTransitions.Add(new AgentWorkflowTransition { Id = Guid.NewGuid(), WorkflowId = workflowId, FromStatus = AgentWorkflowStatus.OperationalValidation, ToStatus = finalStatus, Reason = ready ? "Python validation produced a dispatch proposal for human approval." : "Python validation requires dispatch revision.", ChangedByUserId = actorUserId, ChangedAt = DateTime.UtcNow });
        await SaveOrThrowConcurrencyAsync("persisting the internal AI resume result", workflowId, cancellationToken);
        return workflow;
    }

    private void PersistReturnedSecondHalfArtifacts(AgentWorkflow workflow, PythonOrchestrationEnvelope result)
    {
        var sequence = workflow.Steps.Count == 0 ? 0 : workflow.Steps.Max(s => s.Sequence);
        AddReturnedArtifact(workflow.Id, ref sequence, WorkflowStepType.FleetPlanning, "fleet_route_agent", result.FleetRouteResult);
        if (result.ValidationOperationsResult is not null)
        {
            var c3Output = result.FleetRouteResult?.GetRawText();
            _db.AgentWorkflowSteps.Add(new AgentWorkflowStep
            {
                Id = Guid.NewGuid(),
                WorkflowId = workflow.Id,
                Sequence = ++sequence,
                StepType = WorkflowStepType.OperationalValidation,
                AgentName = "validation_operations_agent",
                Status = WorkflowStepStatus.Completed,
                InputJson = c3Output,
                OutputJson = result.ValidationOperationsResult.Value.GetRawText(),
                StartedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow
            });
        }
    }

    private static bool HasValidationOutcome(JsonElement validationResult, string expectedOutcome)
    {
        return validationResult.ValueKind == JsonValueKind.Object &&
               validationResult.TryGetProperty("validationOutcome", out var outcome) &&
               outcome.ValueKind == JsonValueKind.String &&
               string.Equals(outcome.GetString(), expectedOutcome, StringComparison.Ordinal);
    }

    private void PersistSafeFailureDiagnostic(AgentWorkflow workflow, PythonOrchestrationEnvelope result, WorkflowStepType stepType, string agentName)
    {
        if (result.Errors is null && result.Warnings is null)
            return;

        var sequence = workflow.Steps.Count == 0 ? 0 : workflow.Steps.Max(s => s.Sequence);
        _db.AgentWorkflowSteps.Add(new AgentWorkflowStep
        {
            Id = Guid.NewGuid(),
            WorkflowId = workflow.Id,
            Sequence = sequence + 1,
            StepType = stepType,
            AgentName = agentName,
            Status = WorkflowStepStatus.Failed,
            ValidationJson = JsonSerializer.Serialize(new
            {
                errors = result.Errors,
                warnings = result.Warnings
            }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
            ErrorMessage = "Internal AI orchestration returned a failed envelope.",
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Authoritative C3 execution bridge: converts an approved C3 fleet dispatch proposal
    /// into real CollectionAssignments via the existing CollectionAssignmentService (Step 8).
    /// </summary>
    public async Task<AgentWorkflowDetailDto> ExecuteDispatchPlanAsync(
        Guid workflowId,
        ExecuteDispatchPlanRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var workflow = await LoadWorkflowForDecisionAsync(workflowId, cancellationToken);

        // 1. Idempotency check: if workflow is Completed with Succeeded CollectionAssignment execution result
        // and completed AssignmentExecution step, return current details immediately (true lost-response idempotency).
        // Pre-execution version mismatch must not reject a genuine lost-response retry.
        var existingSuccess = workflow.ExecutionResults
            .FirstOrDefault(e => e.ExecutionType == WorkflowExecutionType.CollectionAssignment &&
                                 e.Status == WorkflowExecutionStatus.Succeeded);
        var hasCompletedStep = workflow.Steps
            .Any(s => s.StepType == WorkflowStepType.AssignmentExecution &&
                      s.Status == WorkflowStepStatus.Completed);

        if (workflow.Status == AgentWorkflowStatus.Completed && existingSuccess != null && hasCompletedStep)
        {
            _logger.LogInformation(
                "Idempotent execution retry: AgentWorkflow {WorkflowId} already completed CollectionAssignment execution {ExecutionId}.",
                workflow.Id, existingSuccess.Id);
            return MapToDetailDto(workflow);
        }

        // 2. Expected version check (for uncompleted workflows or completed workflows without succeeded execution)
        EnsureExpectedVersion(workflow, request.ExpectedVersion);

        // 3. Workflow status check: must be DispatchApproved (or resumable ExecutingAssignments with Pending execution)
        var isResuming = workflow.Status == AgentWorkflowStatus.ExecutingAssignments &&
                         workflow.ExecutionResults.Any(e => e.ExecutionType == WorkflowExecutionType.CollectionAssignment &&
                                                            e.Status == WorkflowExecutionStatus.Pending);

        if (workflow.Status != AgentWorkflowStatus.DispatchApproved && !isResuming)
        {
            throw new BusinessRuleConflictException(
                $"Cannot execute dispatch plan for workflow '{workflowId}' in status '{workflow.Status}'. Execution requires 'DispatchApproved' (or resumable 'ExecutingAssignments').");
        }

        // 4. Verify human approval exists for FleetDispatch
        var approval = workflow.Approvals
            .Where(a => a.ApprovalStage == WorkflowApprovalStage.FleetDispatch &&
                        a.Decision == WorkflowApprovalDecision.Approved)
            .OrderByDescending(a => a.DecidedAt)
            .FirstOrDefault();

        if (approval is null)
        {
            throw new BusinessRuleConflictException("Workflow has no recorded human approval for fleet dispatch.");
        }

        // 5. Resolve C4 OperationalValidation step bound by approval
        if (!approval.WorkflowStepId.HasValue)
        {
            throw new BusinessRuleConflictException("The recorded dispatch approval is not bound to a specific operational validation workflow step.");
        }

        var c4Step = workflow.Steps.FirstOrDefault(s => s.Id == approval.WorkflowStepId.Value &&
                                                        s.StepType == WorkflowStepType.OperationalValidation);
        if (c4Step is null)
        {
            throw new BusinessRuleConflictException("The operational validation step linked by the approval was not found.");
        }

        if (c4Step.Status != WorkflowStepStatus.Completed)
        {
            throw new BusinessRuleConflictException("The operational validation step linked by the approval is not completed.");
        }

        // 6. Validate C4 structured output and warning acknowledgement
        ValidateC4OutputForDispatchExecution(c4Step.OutputJson, approval.DecisionPayloadJson);

        // 7. Resolve the exact C3 snapshot C4 validated. Step 10B persists both the
        // FleetPlanning output and C4 input as the same canonical JSON. Preserve
        // compatibility with legacy C4-only records while rejecting any mismatch
        // where a persisted C3 step is available.
        if (string.IsNullOrWhiteSpace(c4Step.InputJson))
        {
            throw new BusinessRuleConflictException("The operational validation step contains no input payload.");
        }

        var c3Step = workflow.Steps
            .Where(s => s.StepType == WorkflowStepType.FleetPlanning &&
                        s.Status == WorkflowStepStatus.Completed &&
                        s.Sequence < c4Step.Sequence)
            .OrderByDescending(s => s.Sequence)
            .FirstOrDefault();

        if (c3Step is not null)
        {
            if (string.IsNullOrWhiteSpace(c3Step.OutputJson))
            {
                throw new BusinessRuleConflictException("The fleet planning step linked to the approved operational validation step has no output payload.");
            }

            if (!string.Equals(c3Step.OutputJson, c4Step.InputJson, StringComparison.Ordinal))
            {
                throw new BusinessRuleConflictException("The approved operational validation step does not validate the persisted fleet planning snapshot.");
            }
        }

        var snapshot = ParseAndValidateFleetDispatchSnapshot(c4Step.InputJson);
        var dispatchPlans = snapshot.DispatchPlans!;

        // 8. Defensive preflight checks before Phase A
        var allPlannedTaskIds = dispatchPlans
            .SelectMany(p => p.RecommendedTasks!.Select(t => t.TaskId!.Value))
            .ToList();

        // Check tasks
        var tasks = await _db.CollectionTasks
            .Include(t => t.WasteReport)
            .Include(t => t.WasteBin)
            .ThenInclude(b => b!.AcceptedWasteTypes)
            .Where(t => allPlannedTaskIds.Contains(t.Id))
            .ToListAsync(cancellationToken);

        if (tasks.Count != allPlannedTaskIds.Count)
        {
            throw new BusinessRuleConflictException("One or more planned collection tasks were not found in authoritative records.");
        }

        var taskMap = tasks.ToDictionary(t => t.Id);

        foreach (var taskId in allPlannedTaskIds)
        {
            var task = taskMap[taskId];
            if (task.Status != CollectionTaskStatus.Scheduled)
            {
                throw new BusinessRuleConflictException(
                    $"Cannot assign collection task '{task.TaskCode}' ({task.Id}): current status is '{task.Status}', but 'Scheduled' is required.");
            }

            if (!task.WasteReportId.HasValue && !task.WasteBinId.HasValue)
            {
                throw new BusinessRuleConflictException($"Planned collection task '{task.TaskCode}' has no valid target.");
            }

            var isClaimed = await _db.CollectionAssignmentTaskClaims
                .AnyAsync(c => c.IsActive && c.CollectionTaskId == taskId, cancellationToken);
            if (isClaimed)
            {
                throw new BusinessRuleConflictException($"Planned collection task '{task.TaskCode}' is already assigned to an active assignment.");
            }
        }

        // Check drivers
        var allDriverIds = dispatchPlans.Select(p => p.RecommendedDriver!.DriverId!.Value).Distinct().ToList();
        var drivers = await _db.DriverProfiles
            .Include(d => d.User)
            .Where(d => allDriverIds.Contains(d.UserId))
            .ToListAsync(cancellationToken);

        if (drivers.Count != allDriverIds.Count)
        {
            throw new BusinessRuleConflictException("One or more recommended drivers were not found in authoritative records.");
        }

        var driverMap = drivers.ToDictionary(d => d.UserId);

        foreach (var driverId in allDriverIds)
        {
            var driver = driverMap[driverId];
            if (driver.User is null || !driver.User.IsActive)
            {
                throw new BusinessRuleConflictException($"Driver profile '{driverId}' is inactive or has no associated user account.");
            }

            if (driver.AvailabilityStatus != DriverAvailabilityStatus.Available)
            {
                throw new BusinessRuleConflictException($"Driver '{driver.User.FullName ?? driver.User.UserName}' is not available (status: '{driver.AvailabilityStatus}').");
            }

            if (_userManager != null && !await _userManager.IsInRoleAsync(driver.User, AppRoles.Driver))
            {
                throw new BusinessRuleConflictException($"User '{driver.User.UserName}' does not hold the Driver role.");
            }

            var driverOccupied = await _db.CollectionAssignments
                .AnyAsync(a => (a.Status == CollectionAssignmentStatus.Assigned || a.Status == CollectionAssignmentStatus.InProgress) &&
                               a.DriverId == driverId, cancellationToken);
            if (driverOccupied)
            {
                throw new BusinessRuleConflictException($"Driver '{driver.User.FullName ?? driver.User.UserName}' already has an active collection assignment.");
            }
        }

        // Check vehicles
        var allVehicleIds = dispatchPlans.Select(p => p.RecommendedVehicle!.VehicleId!.Value).Distinct().ToList();
        var vehicles = await _db.Vehicles
            .Include(v => v.SupportedWasteTypes)
            .Where(v => allVehicleIds.Contains(v.Id))
            .ToListAsync(cancellationToken);

        if (vehicles.Count != allVehicleIds.Count)
        {
            throw new BusinessRuleConflictException("One or more recommended vehicles were not found in authoritative records.");
        }

        var vehicleMap = vehicles.ToDictionary(v => v.Id);

        foreach (var vehicleId in allVehicleIds)
        {
            var vehicle = vehicleMap[vehicleId];
            if (vehicle.OperationalStatus != VehicleOperationalStatus.Available)
            {
                throw new BusinessRuleConflictException($"Vehicle '{vehicle.RegistrationNumber}' is not available (status: '{vehicle.OperationalStatus}').");
            }

            var vehicleOccupied = await _db.CollectionAssignments
                .AnyAsync(a => (a.Status == CollectionAssignmentStatus.Assigned || a.Status == CollectionAssignmentStatus.InProgress) &&
                               a.VehicleId == vehicleId, cancellationToken);
            if (vehicleOccupied)
            {
                throw new BusinessRuleConflictException($"Vehicle '{vehicle.RegistrationNumber}' already has an active collection assignment.");
            }
        }

        // Fresh compatibility checks
        var managerAcknowledged = HasManagerAcknowledgedWarnings(approval.DecisionPayloadJson);

        foreach (var plan in dispatchPlans)
        {
            var planVehicle = vehicleMap[plan.RecommendedVehicle!.VehicleId!.Value];
            var planTasks = plan.RecommendedTasks!.Select(t => taskMap[t.TaskId!.Value]).ToList();

            var compat = FleetWasteCompatibilityEvaluator.Evaluate(planTasks, planVehicle);

            if (compat.Status == FleetCompatibilityStatus.Incompatible)
            {
                throw new BusinessRuleConflictException(
                    $"Vehicle '{planVehicle.RegistrationNumber}' is incompatible with the assigned collection tasks in dispatch plan '{plan.PlanId}'.");
            }

            if (compat.Status == FleetCompatibilityStatus.Unknown || compat.RequiresAcknowledgement)
            {
                if (!managerAcknowledged)
                {
                    throw new BusinessRuleConflictException(
                        "Current fleet compatibility now requires acknowledgement that was not part of the approved dispatch decision. The approved dispatch plan cannot be executed without a new validation/approval cycle.");
                }
            }
        }

        // 9. Phase A: Persist execution start (ExecutingAssignments)
        AgentWorkflowStep step;
        AgentWorkflowExecutionResult executionResult;

        if (!isResuming)
        {
            _stateMachine.EnsureCanTransition(workflow.Status, AgentWorkflowStatus.ExecutingAssignments);

            var fromStatus = workflow.Status;
            workflow.Status = AgentWorkflowStatus.ExecutingAssignments;
            workflow.CurrentStep = WorkflowStepType.AssignmentExecution;
            workflow.Version++;
            workflow.UpdatedAt = DateTime.UtcNow;

            var startTransition = new AgentWorkflowTransition
            {
                Id = Guid.NewGuid(),
                WorkflowId = workflow.Id,
                FromStatus = fromStatus,
                ToStatus = AgentWorkflowStatus.ExecutingAssignments,
                Reason = "Starting authoritative creation of collection assignments.",
                ChangedByUserId = actorUserId,
                ChangedAt = DateTime.UtcNow
            };
            _db.AgentWorkflowTransitions.Add(startTransition);

            var nextSeq = (workflow.Steps.Count > 0 ? workflow.Steps.Max(s => s.Sequence) : 0) + 1;

            step = new AgentWorkflowStep
            {
                Id = Guid.NewGuid(),
                WorkflowId = workflow.Id,
                Sequence = nextSeq,
                StepType = WorkflowStepType.AssignmentExecution,
                AgentName = null,
                Status = WorkflowStepStatus.Running,
                InputJson = JsonSerializer.Serialize(new
                {
                    sourceOperationalValidationStepId = c4Step.Id,
                    approvedDispatchPlanCount = dispatchPlans.Count,
                    plannedTaskCount = allPlannedTaskIds.Count,
                    unplannedTaskCount = snapshot.UnplannedTasks?.Count ?? 0
                }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
                StartedAt = DateTime.UtcNow
            };
            _db.AgentWorkflowSteps.Add(step);

            executionResult = new AgentWorkflowExecutionResult
            {
                Id = Guid.NewGuid(),
                WorkflowId = workflow.Id,
                WorkflowStepId = step.Id,
                ExecutionType = WorkflowExecutionType.CollectionAssignment,
                Status = WorkflowExecutionStatus.Pending,
                ExecutedAt = DateTime.UtcNow
            };
            _db.AgentWorkflowExecutionResults.Add(executionResult);

            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "Concurrency conflict starting dispatch plan execution on AgentWorkflow {WorkflowId}.", workflow.Id);
                throw new BusinessRuleConflictException("The workflow was modified concurrently by another operation. Please reload and try again.");
            }
        }
        else
        {
            step = workflow.Steps
                .Where(s => s.StepType == WorkflowStepType.AssignmentExecution && s.Status == WorkflowStepStatus.Running)
                .OrderByDescending(s => s.Sequence)
                .FirstOrDefault()
                ?? throw new BusinessRuleConflictException("Resumable AssignmentExecution step was not found.");

            executionResult = workflow.ExecutionResults
                .Where(e => e.ExecutionType == WorkflowExecutionType.CollectionAssignment && e.Status == WorkflowExecutionStatus.Pending)
                .OrderByDescending(e => e.ExecutedAt)
                .FirstOrDefault()
                ?? throw new BusinessRuleConflictException("Resumable Pending CollectionAssignment execution result was not found.");
        }

        // 10. Phase B: Authoritative Assignment Creation Transaction
        var unplannedCount = snapshot.UnplannedTasks?.Count ?? 0;
        var unplannedIds = snapshot.UnplannedTasks?.Select(u => u.TaskId!.Value).ToList() ?? new List<Guid>();
        var createdAssignments = new List<CreatedAssignmentAuditItem>();

        if (dispatchPlans.Count > 0)
        {
            if (_collectionAssignmentService is null)
            {
                throw new InvalidOperationException("ICollectionAssignmentService is not configured for AgentWorkflowService.");
            }

            var isInMemory = _db.Database.ProviderName?.Contains("InMemory") == true;
            await using var tx = isInMemory ? null : await _db.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                foreach (var plan in dispatchPlans)
                {
                    var orderedTasks = plan.RecommendedTasks!
                        .OrderBy(t => t.Sequence!.Value)
                        .ToList();

                    var assignmentRequest = new CreateCollectionAssignmentRequest
                    {
                        DriverId = plan.RecommendedDriver!.DriverId!.Value,
                        VehicleId = plan.RecommendedVehicle!.VehicleId!.Value,
                        CollectionTaskIds = orderedTasks.Select(t => t.TaskId!.Value).ToList(),
                        Stops = orderedTasks.Select(t => new CreateRouteStopRequest
                        {
                            CollectionTaskId = t.TaskId!.Value,
                            Sequence = t.Sequence!.Value
                        }).ToList(),
                        CompatibilityAcknowledgement = managerAcknowledged
                            ? (!string.IsNullOrWhiteSpace(approval.DecisionReason) && approval.DecisionReason.Trim().Length >= 5
                                ? approval.DecisionReason.Trim()
                                : "Acknowledged operational warnings for fleet dispatch proposal.")
                            : null
                    };

                    var assignmentDetail = await _collectionAssignmentService.CreateAssignmentFromApprovedPlanAsync(
                        assignmentRequest,
                        actorUserId,
                        cancellationToken);

                    createdAssignments.Add(new CreatedAssignmentAuditItem
                    {
                        PlanId = plan.PlanId ?? string.Empty,
                        CollectionAssignmentId = assignmentDetail.Id,
                        DriverId = assignmentDetail.DriverId,
                        VehicleId = assignmentDetail.VehicleId,
                        OrderedTaskIds = orderedTasks.Select(t => t.TaskId!.Value).ToList()
                    });
                }

                if (tx != null)
                {
                    await tx.CommitAsync(cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Authoritative collection assignment creation failed for AgentWorkflow {WorkflowId}.", workflow.Id);

                if (tx != null)
                {
                    await tx.RollbackAsync(cancellationToken);
                }

                await RecordAssignmentExecutionFailureAsync(
                    workflow,
                    step,
                    executionResult,
                    actorUserId,
                    ex.Message,
                    cancellationToken);

                throw;
            }
        }

        // 11. Complete Phase B: Transition to Completed
        _stateMachine.EnsureCanTransition(workflow.Status, AgentWorkflowStatus.Completed);

        var preSuccessStatus = workflow.Status;
        workflow.Status = AgentWorkflowStatus.Completed;
        workflow.CurrentStep = WorkflowStepType.AssignmentExecution;
        workflow.Version++;
        workflow.UpdatedAt = DateTime.UtcNow;
        workflow.CompletedAt = DateTime.UtcNow;

        var finalSummary = $"Created {createdAssignments.Count} collection assignments covering {allPlannedTaskIds.Count} planned tasks. {unplannedCount} tasks remained unplanned as recorded in the approved C3 result.";
        workflow.FinalOutcome = finalSummary;

        var successTransition = new AgentWorkflowTransition
        {
            Id = Guid.NewGuid(),
            WorkflowId = workflow.Id,
            FromStatus = preSuccessStatus,
            ToStatus = AgentWorkflowStatus.Completed,
            Reason = finalSummary,
            ChangedByUserId = actorUserId,
            ChangedAt = DateTime.UtcNow
        };
        _db.AgentWorkflowTransitions.Add(successTransition);

        step.Status = WorkflowStepStatus.Completed;
        step.CompletedAt = DateTime.UtcNow;
        step.OutputJson = JsonSerializer.Serialize(new
        {
            sourceOperationalValidationStepId = c4Step.Id,
            createdAssignmentCount = createdAssignments.Count,
            plannedTaskCount = allPlannedTaskIds.Count,
            unplannedTaskCount = unplannedCount,
            createdAssignmentIds = createdAssignments.Select(a => a.CollectionAssignmentId).ToList()
        }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        executionResult.Status = WorkflowExecutionStatus.Succeeded;
        executionResult.ResultJson = JsonSerializer.Serialize(new
        {
            createdAssignmentCount = createdAssignments.Count,
            plannedTaskCount = allPlannedTaskIds.Count,
            unplannedTaskCount = unplannedCount,
            sourceOperationalValidationStepId = c4Step.Id,
            assignments = createdAssignments,
            unplannedTaskIds = unplannedIds
        }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Concurrency conflict completing dispatch plan execution on AgentWorkflow {WorkflowId}.", workflow.Id);
            throw new BusinessRuleConflictException("The workflow was modified concurrently by another operation. Please reload and try again.");
        }

        _logger.LogInformation(
            "AgentWorkflow {WorkflowId} successfully executed dispatch plan: created {Count} assignments, transitioned to Completed (Version: {Version}).",
            workflow.Id, createdAssignments.Count, workflow.Version);

        return MapToDetailDto(workflow);
    }


    private async Task RecordExecutionFailureAsync(
        AgentWorkflow workflow,
        AgentWorkflowStep step,
        AgentWorkflowExecutionResult executionResult,
        Guid actorUserId,
        string rawErrorMessage,
        CancellationToken cancellationToken)
    {
        var safeMessage = TruncateOrSanitizeErrorMessage(rawErrorMessage);

        var fromStatus = workflow.Status;
        workflow.Status = AgentWorkflowStatus.Failed;
        workflow.CurrentStep = WorkflowStepType.ScheduledTaskCreation;
        workflow.CompletedAt = DateTime.UtcNow;
        workflow.UpdatedAt = DateTime.UtcNow;
        workflow.Version++;

        var failureTransition = new AgentWorkflowTransition
        {
            Id = Guid.NewGuid(),
            WorkflowId = workflow.Id,
            FromStatus = fromStatus,
            ToStatus = AgentWorkflowStatus.Failed,
            Reason = $"Task creation failed: {safeMessage}",
            ChangedByUserId = actorUserId,
            ChangedAt = DateTime.UtcNow
        };
        _db.AgentWorkflowTransitions.Add(failureTransition);

        step.Status = WorkflowStepStatus.Failed;
        step.ErrorMessage = safeMessage;
        step.CompletedAt = DateTime.UtcNow;

        executionResult.Status = WorkflowExecutionStatus.Failed;
        executionResult.ErrorMessage = safeMessage;

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist failure audit state for AgentWorkflow {WorkflowId}.", workflow.Id);
        }
    }

    private static string TruncateOrSanitizeErrorMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "An error occurred during authoritative collection task creation.";
        }

        var trimmed = message.Trim();
        return trimmed.Length > 500 ? trimmed[..500] : trimmed;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // INTERNAL C2 EXECUTION SNAPSHOT CONTRACT (FROZEN PYDANTIC BOUNDARY)
    // ──────────────────────────────────────────────────────────────────────────

    private sealed class CollectionPlanningExecutionSnapshot
    {
        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("isCompleteSnapshot")]
        public bool? IsCompleteSnapshot { get; set; }

        [JsonPropertyName("candidateGroups")]
        public List<CandidateGroupExecutionItem>? CandidateGroups { get; set; }

        [JsonPropertyName("separateHandling")]
        public List<NeedHandlingExecutionItem>? SeparateHandling { get; set; }

        [JsonPropertyName("deferredNeeds")]
        public List<NeedHandlingExecutionItem>? DeferredNeeds { get; set; }
    }

    private sealed class CandidateGroupExecutionItem
    {
        [JsonPropertyName("groupId")]
        public string? GroupId { get; set; }

        [JsonPropertyName("attentionOrder")]
        public int? AttentionOrder { get; set; }

        [JsonPropertyName("needReferences")]
        public List<NeedReferenceExecutionItem>? NeedReferences { get; set; }

        [JsonPropertyName("proposedSchedule")]
        public ProposedScheduleExecutionItem? ProposedSchedule { get; set; }

        [JsonPropertyName("wasteHandlingConsiderations")]
        public List<string>? WasteHandlingConsiderations { get; set; }
    }

    private sealed class NeedHandlingExecutionItem
    {
        [JsonPropertyName("needReference")]
        public NeedReferenceExecutionItem? NeedReference { get; set; }

        [JsonPropertyName("attentionOrder")]
        public int? AttentionOrder { get; set; }

        [JsonPropertyName("proposedSchedule")]
        public ProposedScheduleExecutionItem? ProposedSchedule { get; set; }

        [JsonPropertyName("rationale")]
        public string? Rationale { get; set; }
    }

    private sealed class NeedReferenceExecutionItem
    {
        [JsonPropertyName("needId")]
        public Guid? NeedId { get; set; }

        [JsonPropertyName("targetType")]
        public string? TargetType { get; set; }

        [JsonPropertyName("collectionReason")]
        public string? CollectionReason { get; set; }

        [JsonPropertyName("urgency")]
        public string? Urgency { get; set; }
    }

    private sealed class ProposedScheduleExecutionItem
    {
        [JsonPropertyName("scheduledAt")]
        public string? ScheduledAt { get; set; }

        [JsonPropertyName("schedulingReason")]
        public string? SchedulingReason { get; set; }
    }

    private sealed class ExecutableNeedInstruction
    {
        public Guid NeedId { get; init; }
        public string TargetType { get; init; } = string.Empty;
        public string CollectionReason { get; init; } = string.Empty;
        public DateTime ScheduledAt { get; init; }
        public string SchedulingReason { get; init; } = string.Empty;
        public string? HandlingNotes { get; init; }
    }

    private sealed class CreatedTaskAuditItem
    {
        [JsonPropertyName("collectionNeedId")]
        public Guid CollectionNeedId { get; set; }

        [JsonPropertyName("collectionTaskId")]
        public Guid CollectionTaskId { get; set; }

        [JsonPropertyName("taskCode")]
        public string TaskCode { get; set; } = string.Empty;

        [JsonPropertyName("targetType")]
        public string TargetType { get; set; } = string.Empty;

        [JsonPropertyName("targetId")]
        public Guid TargetId { get; set; }

        [JsonPropertyName("scheduledAt")]
        public DateTime ScheduledAt { get; set; }
    }

    private static (List<ExecutableNeedInstruction> Instructions, int DeferredCount) ParseAndFlattenCollectionPlan(
        string outputJson, CollectionSchedulePolicy schedulePolicy, DateTimeOffset nowUtc)
    {
        if (string.IsNullOrWhiteSpace(outputJson))
        {
            throw new BusinessRuleConflictException("The collection planning output is missing or empty.");
        }

        CollectionPlanningExecutionSnapshot snapshot;
        try
        {
            snapshot = JsonSerializer.Deserialize<CollectionPlanningExecutionSnapshot>(
                outputJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = false })
                ?? throw new BusinessRuleConflictException("The collection planning output deserialized to null.");
        }
        catch (JsonException ex)
        {
            throw new BusinessRuleConflictException("The collection planning output is malformed JSON.", ex);
        }

        // Top-level approval invariants
        if (!string.Equals(snapshot.Status, "completed", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessRuleConflictException(
                $"The collection planning output is not approval-ready: status must be 'completed' (actual: '{snapshot.Status ?? "missing"}').");
        }

        if (snapshot.IsCompleteSnapshot != true)
        {
            throw new BusinessRuleConflictException(
                "The collection planning output is not approval-ready: isCompleteSnapshot must be true.");
        }

        // Required canonical arrays (rejects legacy schema where candidateGroups, separateHandling, or deferredNeeds are missing)
        if (snapshot.CandidateGroups == null || snapshot.SeparateHandling == null || snapshot.DeferredNeeds == null)
        {
            throw new BusinessRuleConflictException(
                "The collection planning output is missing canonical collections (candidateGroups, separateHandling, or deferredNeeds).");
        }

        var instructions = new List<ExecutableNeedInstruction>();

        // 1. Process candidateGroups
        foreach (var group in snapshot.CandidateGroups)
        {
            if (group.ProposedSchedule == null)
            {
                throw new BusinessRuleConflictException(
                    $"Candidate group '{group.GroupId ?? "unknown"}' in the approved plan is missing a proposed schedule.");
            }

            var scheduledAtUtc = schedulePolicy.ValidateProposedStart(group.ProposedSchedule.ScheduledAt, nowUtc);

            var schedulingReason = group.ProposedSchedule.SchedulingReason?.Trim();
            if (string.IsNullOrWhiteSpace(schedulingReason) || schedulingReason.Length < 5)
            {
                throw new BusinessRuleConflictException(
                    $"Candidate group '{group.GroupId ?? "unknown"}' proposed schedule is missing a valid scheduling reason (minimum 5 characters).");
            }

            string? handlingNotes = null;
            if (group.WasteHandlingConsiderations != null && group.WasteHandlingConsiderations.Count > 0)
            {
                var validConsiderations = group.WasteHandlingConsiderations
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .Select(c => c.Trim())
                    .ToList();

                if (validConsiderations.Count > 0)
                {
                    var joined = string.Join("; ", validConsiderations);
                    handlingNotes = joined.Length > 1000 ? joined[..1000] : joined;
                }
            }

            if (group.NeedReferences == null || group.NeedReferences.Count == 0)
            {
                throw new BusinessRuleConflictException(
                    $"Candidate group '{group.GroupId ?? "unknown"}' contains no collection need references.");
            }

            foreach (var needRef in group.NeedReferences)
            {
                if (!needRef.NeedId.HasValue || needRef.NeedId.Value == Guid.Empty)
                {
                    throw new BusinessRuleConflictException("A collection need reference in candidate groups has a missing or empty needId.");
                }

                if (string.IsNullOrWhiteSpace(needRef.TargetType))
                {
                    throw new BusinessRuleConflictException("A collection need reference in candidate groups is missing targetType.");
                }

                if (string.IsNullOrWhiteSpace(needRef.CollectionReason))
                {
                    throw new BusinessRuleConflictException("A collection need reference in candidate groups is missing collectionReason.");
                }

                instructions.Add(new ExecutableNeedInstruction
                {
                    NeedId = needRef.NeedId.Value,
                    TargetType = needRef.TargetType.Trim(),
                    CollectionReason = needRef.CollectionReason.Trim(),
                    ScheduledAt = scheduledAtUtc,
                    SchedulingReason = schedulingReason,
                    HandlingNotes = handlingNotes
                });
            }
        }

        // 2. Process separateHandling
        foreach (var sepItem in snapshot.SeparateHandling)
        {
            if (sepItem.NeedReference == null)
            {
                throw new BusinessRuleConflictException("A separate handling item is missing needReference.");
            }

            if (!sepItem.NeedReference.NeedId.HasValue || sepItem.NeedReference.NeedId.Value == Guid.Empty)
            {
                throw new BusinessRuleConflictException("A separate handling item has a missing or empty needId.");
            }

            if (string.IsNullOrWhiteSpace(sepItem.NeedReference.TargetType))
            {
                throw new BusinessRuleConflictException("A separate handling item is missing targetType.");
            }

            if (string.IsNullOrWhiteSpace(sepItem.NeedReference.CollectionReason))
            {
                throw new BusinessRuleConflictException("A separate handling item is missing collectionReason.");
            }

            if (sepItem.ProposedSchedule == null)
            {
                throw new BusinessRuleConflictException(
                    $"Separate handling item for need '{sepItem.NeedReference.NeedId}' is missing a proposed schedule.");
            }

            var scheduledAtUtc = schedulePolicy.ValidateProposedStart(sepItem.ProposedSchedule.ScheduledAt, nowUtc);

            var schedulingReason = sepItem.ProposedSchedule.SchedulingReason?.Trim();
            if (string.IsNullOrWhiteSpace(schedulingReason) || schedulingReason.Length < 5)
            {
                throw new BusinessRuleConflictException(
                    $"Separate handling item for need '{sepItem.NeedReference.NeedId}' proposed schedule is missing a valid scheduling reason (minimum 5 characters).");
            }

            instructions.Add(new ExecutableNeedInstruction
            {
                NeedId = sepItem.NeedReference.NeedId.Value,
                TargetType = sepItem.NeedReference.TargetType.Trim(),
                CollectionReason = sepItem.NeedReference.CollectionReason.Trim(),
                ScheduledAt = scheduledAtUtc,
                SchedulingReason = schedulingReason,
                HandlingNotes = null
            });
        }

        // 3. Deferred needs processing (collect IDs for overlap check and count)
        var deferredNeedIds = new HashSet<Guid>();
        foreach (var defItem in snapshot.DeferredNeeds)
        {
            if (defItem.NeedReference?.NeedId.HasValue == true && defItem.NeedReference.NeedId.Value != Guid.Empty)
            {
                deferredNeedIds.Add(defItem.NeedReference.NeedId.Value);
            }
        }

        // 4. Pre-flight duplicate detection across executable entries
        var duplicateExecutable = instructions
            .GroupBy(i => i.NeedId)
            .FirstOrDefault(g => g.Count() > 1);

        if (duplicateExecutable != null)
        {
            throw new BusinessRuleConflictException(
                $"Duplicate collection need reference detected: {duplicateExecutable.Key} appears multiple times in executable plan.");
        }

        // 5. Pre-flight executable vs deferred overlap detection
        var overlappingNeed = instructions.FirstOrDefault(i => deferredNeedIds.Contains(i.NeedId));
        if (overlappingNeed != null)
        {
            throw new BusinessRuleConflictException(
                $"Collection need '{overlappingNeed.NeedId}' cannot appear as both executable and deferred in the same proposal.");
        }

        return (instructions, snapshot.DeferredNeeds.Count);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // STEP 8 DISPATCH PLAN EXECUTION HELPERS & SNAPSHOT MODELS
    // ──────────────────────────────────────────────────────────────────────────

    private async Task RecordAssignmentExecutionFailureAsync(
        AgentWorkflow workflow,
        AgentWorkflowStep step,
        AgentWorkflowExecutionResult executionResult,
        Guid actorUserId,
        string rawErrorMessage,
        CancellationToken cancellationToken)
    {
        var safeMessage = TruncateOrSanitizeErrorMessage(rawErrorMessage);

        // Detach any tracked entities from the failed execution attempt so rolled-back mutations are not flushed
        foreach (var entry in _db.ChangeTracker.Entries().ToList())
        {
            if (entry.Entity != workflow &&
                entry.Entity != step &&
                entry.Entity != executionResult)
            {
                entry.State = EntityState.Detached;
            }
        }

        var fromStatus = workflow.Status;
        workflow.Status = AgentWorkflowStatus.Failed;
        workflow.CurrentStep = WorkflowStepType.AssignmentExecution;
        workflow.CompletedAt = DateTime.UtcNow;
        workflow.UpdatedAt = DateTime.UtcNow;
        workflow.Version++;

        var failureTransition = new AgentWorkflowTransition
        {
            Id = Guid.NewGuid(),
            WorkflowId = workflow.Id,
            FromStatus = fromStatus,
            ToStatus = AgentWorkflowStatus.Failed,
            Reason = $"Assignment execution failed: {safeMessage}",
            ChangedByUserId = actorUserId,
            ChangedAt = DateTime.UtcNow
        };
        _db.AgentWorkflowTransitions.Add(failureTransition);

        step.Status = WorkflowStepStatus.Failed;
        step.ErrorMessage = safeMessage;
        step.CompletedAt = DateTime.UtcNow;

        executionResult.Status = WorkflowExecutionStatus.Failed;
        executionResult.ErrorMessage = safeMessage;

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist failure audit state for AgentWorkflow {WorkflowId}.", workflow.Id);
        }
    }

    private static bool HasManagerAcknowledgedWarnings(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("acknowledgeWarnings", out var ackProp) ||
                    root.TryGetProperty("acknowledge_warnings", out ackProp))
                {
                    return ackProp.ValueKind == JsonValueKind.True;
                }
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return false;
    }

    private static void ValidateC4OutputForDispatchExecution(string? outputJson, string? approvalPayloadJson)
    {
        if (string.IsNullOrWhiteSpace(outputJson))
        {
            throw new BusinessRuleConflictException("The operational validation step has no output payload.");
        }

        OperationalValidationExecutionSnapshot? c4;
        try
        {
            c4 = JsonSerializer.Deserialize<OperationalValidationExecutionSnapshot>(outputJson);
        }
        catch (JsonException ex)
        {
            throw new BusinessRuleConflictException("The operational validation step output payload is malformed.", ex);
        }

        if (c4 is null)
        {
            throw new BusinessRuleConflictException("The operational validation step output payload could not be parsed.");
        }

        var outcome = c4.ValidationOutcome?.Trim();
        if (!string.Equals(outcome, "ReadyForHumanReview", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessRuleConflictException(
                $"Dispatch proposal validation outcome is '{outcome ?? "missing"}' and cannot be executed without revision.");
        }

        if (c4.RequiresAcknowledgement == true)
        {
            if (!HasManagerAcknowledgedWarnings(approvalPayloadJson))
            {
                throw new BusinessRuleConflictException(
                    "The operational validation step requires warning acknowledgement, but the manager approval did not acknowledge warnings.");
            }
        }
    }

    private static FleetDispatchExecutionSnapshot ParseAndValidateFleetDispatchSnapshot(string inputJson)
    {
        FleetDispatchExecutionSnapshot? snapshot;
        try
        {
            snapshot = JsonSerializer.Deserialize<FleetDispatchExecutionSnapshot>(inputJson);
        }
        catch (JsonException ex)
        {
            throw new BusinessRuleConflictException("The operational validation input snapshot is malformed JSON.", ex);
        }

        if (snapshot is null)
        {
            throw new BusinessRuleConflictException("The fleet dispatch proposal snapshot could not be parsed.");
        }

        if (snapshot.DispatchPlans is null)
        {
            throw new BusinessRuleConflictException("The dispatch proposal is missing the required 'dispatchPlans' collection.");
        }

        var seenPlanIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenDriverIds = new HashSet<Guid>();
        var seenVehicleIds = new HashSet<Guid>();
        var seenTaskIds = new HashSet<Guid>();

        for (var pIdx = 0; pIdx < snapshot.DispatchPlans.Count; pIdx++)
        {
            var plan = snapshot.DispatchPlans[pIdx];
            if (plan is null)
            {
                throw new BusinessRuleConflictException($"Dispatch plan at index {pIdx} is null.");
            }

            var planId = plan.PlanId?.Trim();
            if (string.IsNullOrWhiteSpace(planId))
            {
                throw new BusinessRuleConflictException($"Dispatch plan at index {pIdx} is missing a valid planId.");
            }

            if (!seenPlanIds.Add(planId))
            {
                throw new BusinessRuleConflictException($"Duplicate dispatch plan ID detected: '{planId}'.");
            }

            if (plan.RecommendedDriver is null || !plan.RecommendedDriver.DriverId.HasValue || plan.RecommendedDriver.DriverId.Value == Guid.Empty)
            {
                throw new BusinessRuleConflictException($"Dispatch plan '{planId}' is missing a valid recommended driver.");
            }

            var driverId = plan.RecommendedDriver.DriverId.Value;
            if (!seenDriverIds.Add(driverId))
            {
                throw new BusinessRuleConflictException($"Driver '{driverId}' is assigned to multiple dispatch plans.");
            }

            if (plan.RecommendedVehicle is null || !plan.RecommendedVehicle.VehicleId.HasValue || plan.RecommendedVehicle.VehicleId.Value == Guid.Empty)
            {
                throw new BusinessRuleConflictException($"Dispatch plan '{planId}' is missing a valid recommended vehicle.");
            }

            var vehicleId = plan.RecommendedVehicle.VehicleId.Value;
            if (!seenVehicleIds.Add(vehicleId))
            {
                throw new BusinessRuleConflictException($"Vehicle '{vehicleId}' is assigned to multiple dispatch plans.");
            }

            if (plan.RecommendedTasks is null || plan.RecommendedTasks.Count == 0)
            {
                throw new BusinessRuleConflictException($"Dispatch plan '{planId}' contains no recommended collection tasks.");
            }

            var planTaskSequences = new List<int>();
            for (var tIdx = 0; tIdx < plan.RecommendedTasks.Count; tIdx++)
            {
                var taskItem = plan.RecommendedTasks[tIdx];
                if (taskItem is null || !taskItem.TaskId.HasValue || taskItem.TaskId.Value == Guid.Empty)
                {
                    throw new BusinessRuleConflictException($"Dispatch plan '{planId}' has an invalid task at index {tIdx}.");
                }

                var taskId = taskItem.TaskId.Value;
                if (!seenTaskIds.Add(taskId))
                {
                    throw new BusinessRuleConflictException($"Collection task '{taskId}' appears in multiple dispatch plans.");
                }

                if (!taskItem.Sequence.HasValue || taskItem.Sequence.Value < 1)
                {
                    throw new BusinessRuleConflictException($"Collection task '{taskId}' in plan '{planId}' has an invalid sequence value: {taskItem.Sequence}.");
                }

                planTaskSequences.Add(taskItem.Sequence.Value);
            }

            // Sequence validation: 1..N strictly contiguous, no duplicates, no gaps
            planTaskSequences.Sort();
            for (var i = 0; i < planTaskSequences.Count; i++)
            {
                var expectedSeq = i + 1;
                if (planTaskSequences[i] != expectedSeq)
                {
                    throw new BusinessRuleConflictException(
                        $"Dispatch plan '{planId}' has invalid stop sequence progression. Expected 1..{planTaskSequences.Count} contiguous integers, but found non-consecutive or duplicate sequence '{planTaskSequences[i]}'.");
                }
            }
        }

        // Unplanned tasks validation
        var unplannedIds = new HashSet<Guid>();
        if (snapshot.UnplannedTasks != null)
        {
            foreach (var unp in snapshot.UnplannedTasks)
            {
                if (unp.TaskId.HasValue && unp.TaskId.Value != Guid.Empty)
                {
                    var uId = unp.TaskId.Value;
                    if (!unplannedIds.Add(uId))
                    {
                        throw new BusinessRuleConflictException($"Duplicate unplanned task ID detected: '{uId}'.");
                    }

                    if (seenTaskIds.Contains(uId))
                    {
                        throw new BusinessRuleConflictException(
                            $"Collection task '{uId}' cannot appear as both planned and unplanned in the same dispatch proposal.");
                    }
                }
            }
        }

        return snapshot;
    }

    private sealed class FleetDispatchExecutionSnapshot
    {
        [JsonPropertyName("dispatchPlans")]
        public List<DispatchPlanExecutionItem>? DispatchPlans { get; set; }

        [JsonPropertyName("unplannedTasks")]
        public List<UnplannedTaskExecutionItem>? UnplannedTasks { get; set; }
    }

    private sealed class DispatchPlanExecutionItem
    {
        [JsonPropertyName("planId")]
        public string? PlanId { get; set; }

        [JsonPropertyName("recommendedDriver")]
        public RecommendedDriverExecutionItem? RecommendedDriver { get; set; }

        [JsonPropertyName("recommendedVehicle")]
        public RecommendedVehicleExecutionItem? RecommendedVehicle { get; set; }

        [JsonPropertyName("recommendedTasks")]
        public List<RecommendedTaskExecutionItem>? RecommendedTasks { get; set; }
    }

    private sealed class RecommendedDriverExecutionItem
    {
        [JsonPropertyName("driverId")]
        public Guid? DriverId { get; set; }

        [JsonPropertyName("displayName")]
        public string? DisplayName { get; set; }
    }

    private sealed class RecommendedVehicleExecutionItem
    {
        [JsonPropertyName("vehicleId")]
        public Guid? VehicleId { get; set; }

        [JsonPropertyName("registrationNumber")]
        public string? RegistrationNumber { get; set; }

        [JsonPropertyName("vehicleType")]
        public string? VehicleType { get; set; }
    }

    private sealed class RecommendedTaskExecutionItem
    {
        [JsonPropertyName("taskId")]
        public Guid? TaskId { get; set; }

        [JsonPropertyName("taskCode")]
        public string? TaskCode { get; set; }

        [JsonPropertyName("sequence")]
        public int? Sequence { get; set; }

        [JsonPropertyName("addressText")]
        public string? AddressText { get; set; }
    }

    private sealed class UnplannedTaskExecutionItem
    {
        [JsonPropertyName("taskId")]
        public Guid? TaskId { get; set; }

        [JsonPropertyName("taskCode")]
        public string? TaskCode { get; set; }

        [JsonPropertyName("reason")]
        public string? Reason { get; set; }
    }

    private sealed class OperationalValidationExecutionSnapshot
    {
        [JsonPropertyName("validationOutcome")]
        public string? ValidationOutcome { get; set; }

        [JsonPropertyName("requiresAcknowledgement")]
        public bool? RequiresAcknowledgement { get; set; }
    }

    private sealed class CreatedAssignmentAuditItem
    {
        [JsonPropertyName("planId")]
        public string PlanId { get; set; } = string.Empty;

        [JsonPropertyName("collectionAssignmentId")]
        public Guid CollectionAssignmentId { get; set; }

        [JsonPropertyName("driverId")]
        public Guid DriverId { get; set; }

        [JsonPropertyName("vehicleId")]
        public Guid VehicleId { get; set; }

        [JsonPropertyName("orderedTaskIds")]
        public List<Guid> OrderedTaskIds { get; set; } = new();
    }
}
