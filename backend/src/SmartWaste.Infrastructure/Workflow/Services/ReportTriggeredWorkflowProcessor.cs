using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartWaste.Application.Common.Exceptions;
using SmartWaste.Application.Workflow.Interfaces;
using SmartWaste.Domain.Reporting.Enums;
using SmartWaste.Domain.Workflow.Entities;
using SmartWaste.Domain.Workflow.Enums;
using SmartWaste.Infrastructure.Persistence;

namespace SmartWaste.Infrastructure.Workflow.Services;

/// <summary>Durable initial and post-verification report continuation; no request or process-local queue state.</summary>
public sealed class ReportTriggeredWorkflowProcessor
{
    private sealed record Claim(Guid WorkflowId, int Version, Guid LeaseId, WorkflowStepType Phase, bool Finalized);

    private readonly AppDbContext _db;
    private readonly IAgentWorkflowService _workflowService;
    private readonly IAgentWorkflowStateMachine _stateMachine;
    private readonly IPythonOrchestrationClient _python;
    private readonly ReportTriggeredWorkflowWorkerOptions _options;
    private readonly ILogger<ReportTriggeredWorkflowProcessor> _logger;

    public ReportTriggeredWorkflowProcessor(
        AppDbContext db,
        IAgentWorkflowService workflowService,
        IAgentWorkflowStateMachine stateMachine,
        IPythonOrchestrationClient python,
        IOptions<ReportTriggeredWorkflowWorkerOptions> options,
        ILogger<ReportTriggeredWorkflowProcessor> logger)
    {
        _db = db;
        _workflowService = workflowService;
        _stateMachine = stateMachine;
        _python = python;
        _options = options.Value;
        _logger = logger;
    }

    /// <returns>True when an eligible workflow was claimed or finalized; false when none was available.</returns>
    public async Task<bool> ProcessOneAsync(CancellationToken stoppingToken = default, Guid? workflowId = null)
    {
        var claim = await TryClaimAsync(workflowId, stoppingToken);
        if (claim is null)
            return false;
        if (claim.Finalized)
            return true;

        try
        {
            // This token belongs to the hosted worker, never the citizen's RequestAborted token.
            if (claim.Phase == WorkflowStepType.CollectionPlanning)
            {
                var request = await _workflowService.BuildReportVerificationResumeRequestAsync(
                    claim.WorkflowId, claim.Version, claim.LeaseId, stoppingToken);
                var result = await _python.ResumeAfterReportVerificationAsync(request, stoppingToken);
                await _workflowService.PersistReportVerificationContinuationAsync(
                    claim.WorkflowId, result, claim.Version,
                    expectedProcessingLeaseId: claim.LeaseId, cancellationToken: stoppingToken);
            }
            else
            {
                var request = await _workflowService.BuildPythonStartRequestAsync(
                    claim.WorkflowId, claim.Version, claim.LeaseId, stoppingToken);
                var result = await _python.StartAsync(request, stoppingToken);
                await _workflowService.PersistReportVerificationPauseAsync(
                    claim.WorkflowId, result, claim.Version,
                    expectedProcessingLeaseId: claim.LeaseId, cancellationToken: stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // On shutdown the durable lease expires; a new process reclaims the same workflow.
            return true;
        }
        catch (Exception exception)
        {
            _logger.LogWarning("Report workflow attempt failed [workflow={WorkflowId}, phase={Phase}, kind={FailureKind}].",
                claim.WorkflowId, claim.Phase, exception.GetType().Name);
            await RecordAttemptFailureAsync(claim, stoppingToken);
        }
        return true;
    }

    private IQueryable<AgentWorkflow> Eligible(DateTime now) => _db.AgentWorkflows.Where(w =>
        w.TriggerType == AgentWorkflowTriggerType.CitizenReportSubmission &&
        (((w.Status == AgentWorkflowStatus.Created && w.CurrentStep == WorkflowStepType.None) ||
          (w.Status == AgentWorkflowStatus.Planning && w.CurrentStep == WorkflowStepType.SharedPlanning)) &&
         !w.Steps.Any() ||
         (w.Status == AgentWorkflowStatus.Planning && w.CurrentStep == WorkflowStepType.CollectionPlanning &&
          w.TriggeringWasteReport != null && w.TriggeringWasteReport.Status == WasteReportStatus.Verified &&
          w.Steps.Count == 2 &&
          w.Steps.Any(s => s.Sequence == 1 && s.StepType == WorkflowStepType.SharedPlanning && s.Status == WorkflowStepStatus.Completed) &&
          w.Steps.Any(s => s.Sequence == 2 && s.StepType == WorkflowStepType.WasteAnalysis && s.Status == WorkflowStepStatus.Completed) &&
          !w.Steps.Any(s => s.StepType == WorkflowStepType.CollectionPlanning))) &&
        ((w.ProcessingLeaseId == null && w.ProcessingLeaseExpiresAt == null) ||
         (w.ProcessingLeaseExpiresAt != null && w.ProcessingLeaseExpiresAt <= now)));

    private async Task<Claim?> TryClaimAsync(Guid? workflowId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var candidate = await Eligible(now).Where(w => workflowId == null || w.Id == workflowId)
            .AsNoTracking().OrderBy(w => w.CurrentStep == WorkflowStepType.CollectionPlanning ? 0 : 1)
            .ThenBy(w => w.CreatedAt)
            .Select(w => new { w.Id, w.Status, w.CurrentStep, w.Version, w.ProcessingAttemptCount })
            .FirstOrDefaultAsync(cancellationToken);
        if (candidate is null)
            return null;

        // The conditional UPDATE and audit transition commit together. Two instances can
        // read the same candidate, but only one can update its expected version/lease state.
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var target = Eligible(now).Where(w => w.Id == candidate.Id && w.Version == candidate.Version &&
            w.ProcessingAttemptCount == candidate.ProcessingAttemptCount);

        if (candidate.ProcessingAttemptCount >= _options.MaxAttempts)
        {
            if (candidate.Status != AgentWorkflowStatus.Planning)
                return null;
            _stateMachine.EnsureCanTransition(candidate.Status, AgentWorkflowStatus.Failed);
            var finalized = await target.ExecuteUpdateAsync(update => update
                .SetProperty(w => w.Status, AgentWorkflowStatus.Failed)
                .SetProperty(w => w.ProcessingLeaseId, (Guid?)null)
                .SetProperty(w => w.ProcessingLeaseExpiresAt, (DateTime?)null)
                .SetProperty(w => w.CompletedAt, now)
                .SetProperty(w => w.UpdatedAt, now)
                .SetProperty(w => w.Version, w => w.Version + 1), cancellationToken);
            if (finalized != 1)
                return null;
            _db.AgentWorkflowTransitions.Add(new AgentWorkflowTransition
            {
                Id = Guid.NewGuid(), WorkflowId = candidate.Id,
                FromStatus = AgentWorkflowStatus.Planning, ToStatus = AgentWorkflowStatus.Failed,
                Reason = candidate.CurrentStep == WorkflowStepType.CollectionPlanning
                    ? "Post-verification collection planning exhausted its allowed attempts."
                    : "Initial internal AI orchestration exhausted its allowed attempts.",
                ChangedByUserId = null, ChangedAt = now
            });
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new Claim(candidate.Id, candidate.Version + 1, Guid.Empty, candidate.CurrentStep, true);
        }

        var leaseId = Guid.NewGuid();
        var expiresAt = now.AddSeconds(_options.LeaseSeconds);
        var claimed = await target.ExecuteUpdateAsync(update => update
            .SetProperty(w => w.Status, AgentWorkflowStatus.Planning)
            .SetProperty(w => w.CurrentStep, candidate.CurrentStep == WorkflowStepType.CollectionPlanning
                ? WorkflowStepType.CollectionPlanning : WorkflowStepType.SharedPlanning)
            .SetProperty(w => w.ProcessingLeaseId, leaseId)
            .SetProperty(w => w.ProcessingLeaseExpiresAt, expiresAt)
            .SetProperty(w => w.ProcessingAttemptCount, w => w.ProcessingAttemptCount + 1)
            .SetProperty(w => w.UpdatedAt, now)
            .SetProperty(w => w.Version, w => w.Version + 1), cancellationToken);
        if (claimed != 1)
            return null;

        if (candidate.Status == AgentWorkflowStatus.Created)
        {
            _stateMachine.EnsureCanTransition(candidate.Status, AgentWorkflowStatus.Planning);
            _db.AgentWorkflowTransitions.Add(new AgentWorkflowTransition
            {
                Id = Guid.NewGuid(), WorkflowId = candidate.Id,
                FromStatus = AgentWorkflowStatus.Created, ToStatus = AgentWorkflowStatus.Planning,
                Reason = "Initial citizen-report AI orchestration claimed by the system.",
                ChangedByUserId = null, ChangedAt = now
            });
            await _db.SaveChangesAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return new Claim(candidate.Id, candidate.Version + 1, leaseId,
            candidate.CurrentStep == WorkflowStepType.CollectionPlanning ? WorkflowStepType.CollectionPlanning : WorkflowStepType.SharedPlanning, false);
    }

    private async Task RecordAttemptFailureAsync(Claim claim, CancellationToken cancellationToken)
    {
        // A failed EF save can leave rejected envelope artifacts tracked locally.
        // They must never be included in the separate, safe retry-state update.
        _db.ChangeTracker.Clear();
        var now = DateTime.UtcNow;
        var current = await _db.AgentWorkflows.AsNoTracking()
            .Where(w => w.Id == claim.WorkflowId && w.Version == claim.Version &&
                w.ProcessingLeaseId == claim.LeaseId && w.Status == AgentWorkflowStatus.Planning &&
                w.CurrentStep == claim.Phase &&
                (claim.Phase == WorkflowStepType.CollectionPlanning
                    ? !w.Steps.Any(s => s.StepType == WorkflowStepType.CollectionPlanning)
                    : !w.Steps.Any()))
            .Select(w => new { w.ProcessingAttemptCount })
            .FirstOrDefaultAsync(cancellationToken);
        if (current is null)
            return; // A newer lease or accepted response owns the workflow.

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var target = _db.AgentWorkflows.Where(w => w.Id == claim.WorkflowId && w.Version == claim.Version &&
            w.ProcessingLeaseId == claim.LeaseId && w.Status == AgentWorkflowStatus.Planning &&
            w.CurrentStep == claim.Phase &&
            (claim.Phase == WorkflowStepType.CollectionPlanning
                ? !w.Steps.Any(s => s.StepType == WorkflowStepType.CollectionPlanning)
                : !w.Steps.Any()));
        if (current.ProcessingAttemptCount >= _options.MaxAttempts)
        {
            _stateMachine.EnsureCanTransition(AgentWorkflowStatus.Planning, AgentWorkflowStatus.Failed);
            var failed = await target.ExecuteUpdateAsync(update => update
                .SetProperty(w => w.Status, AgentWorkflowStatus.Failed)
                .SetProperty(w => w.ProcessingLeaseId, (Guid?)null)
                .SetProperty(w => w.ProcessingLeaseExpiresAt, (DateTime?)null)
                .SetProperty(w => w.CompletedAt, now)
                .SetProperty(w => w.UpdatedAt, now)
                .SetProperty(w => w.Version, w => w.Version + 1), cancellationToken);
            if (failed != 1)
                return;
            _db.AgentWorkflowTransitions.Add(new AgentWorkflowTransition
            {
                Id = Guid.NewGuid(), WorkflowId = claim.WorkflowId,
                FromStatus = AgentWorkflowStatus.Planning, ToStatus = AgentWorkflowStatus.Failed,
                Reason = "Internal AI orchestration failed after the configured maximum attempts.",
                ChangedByUserId = null, ChangedAt = now
            });
            await _db.SaveChangesAsync(cancellationToken);
        }
        else
        {
            var retryAt = now.AddSeconds(_options.RetryDelaySeconds);
            var released = await target.ExecuteUpdateAsync(update => update
                .SetProperty(w => w.ProcessingLeaseId, (Guid?)null)
                .SetProperty(w => w.ProcessingLeaseExpiresAt, retryAt)
                .SetProperty(w => w.UpdatedAt, now)
                .SetProperty(w => w.Version, w => w.Version + 1), cancellationToken);
            if (released != 1)
                return;
        }
        await transaction.CommitAsync(cancellationToken);
    }
}
