using SmartWaste.Application.Common.Exceptions;
using SmartWaste.Application.Workflow.Interfaces;
using SmartWaste.Domain.Workflow.Enums;

namespace SmartWaste.Application.Workflow.Services;

/// <summary>
/// Thread-safe, deterministic state machine enforcing legal AgentWorkflow status transitions.
/// </summary>
public class AgentWorkflowStateMachine : IAgentWorkflowStateMachine
{
    private static readonly Dictionary<AgentWorkflowStatus, HashSet<AgentWorkflowStatus>> LegalTransitions = new()
    {
        [AgentWorkflowStatus.Created] = new()
        {
            AgentWorkflowStatus.Planning,
            AgentWorkflowStatus.Rejected
        },
        [AgentWorkflowStatus.Planning] = new()
        {
            AgentWorkflowStatus.AwaitingReportVerification,
            AgentWorkflowStatus.AwaitingCollectionApproval,
            AgentWorkflowStatus.Rejected,
            AgentWorkflowStatus.Failed
        },
        [AgentWorkflowStatus.AwaitingReportVerification] = new()
        {
            AgentWorkflowStatus.Planning,
            AgentWorkflowStatus.Rejected
        },
        [AgentWorkflowStatus.AwaitingCollectionApproval] = new()
        {
            AgentWorkflowStatus.CollectionApproved,
            AgentWorkflowStatus.CollectionNeedsRevision,
            AgentWorkflowStatus.Rejected
        },
        [AgentWorkflowStatus.CollectionNeedsRevision] = new()
        {
            AgentWorkflowStatus.Planning,
            AgentWorkflowStatus.Rejected
        },
        [AgentWorkflowStatus.CollectionApproved] = new()
        {
            AgentWorkflowStatus.CreatingScheduledTasks,
            AgentWorkflowStatus.Failed
        },
        [AgentWorkflowStatus.CreatingScheduledTasks] = new()
        {
            AgentWorkflowStatus.FleetPlanning,
            AgentWorkflowStatus.Failed
        },
        [AgentWorkflowStatus.FleetPlanning] = new()
        {
            AgentWorkflowStatus.OperationalValidation,
            AgentWorkflowStatus.Failed
        },
        [AgentWorkflowStatus.OperationalValidation] = new()
        {
            AgentWorkflowStatus.AwaitingDispatchApproval,
            AgentWorkflowStatus.DispatchNeedsRevision,
            AgentWorkflowStatus.Failed
        },
        [AgentWorkflowStatus.AwaitingDispatchApproval] = new()
        {
            AgentWorkflowStatus.DispatchApproved,
            AgentWorkflowStatus.DispatchNeedsRevision,
            AgentWorkflowStatus.Rejected
        },
        [AgentWorkflowStatus.DispatchNeedsRevision] = new()
        {
            AgentWorkflowStatus.FleetPlanning,
            AgentWorkflowStatus.Rejected
        },
        [AgentWorkflowStatus.DispatchApproved] = new()
        {
            AgentWorkflowStatus.ExecutingAssignments,
            AgentWorkflowStatus.Failed
        },
        [AgentWorkflowStatus.ExecutingAssignments] = new()
        {
            AgentWorkflowStatus.Completed,
            AgentWorkflowStatus.Failed
        },
        [AgentWorkflowStatus.Completed] = new(),
        [AgentWorkflowStatus.Rejected] = new(),
        [AgentWorkflowStatus.Failed] = new()
    };

    private static readonly HashSet<AgentWorkflowStatus> TerminalStatuses = new()
    {
        AgentWorkflowStatus.Completed,
        AgentWorkflowStatus.Rejected,
        AgentWorkflowStatus.Failed
    };

    public bool CanTransition(AgentWorkflowStatus from, AgentWorkflowStatus to)
    {
        if (from == to)
        {
            return false;
        }

        if (LegalTransitions.TryGetValue(from, out var targets))
        {
            return targets.Contains(to);
        }

        return false;
    }

    public void EnsureCanTransition(AgentWorkflowStatus from, AgentWorkflowStatus to)
    {
        if (from == to)
        {
            throw new InvalidWorkflowTransitionException(
                $"Cannot transition AgentWorkflow from '{from}' to '{to}'. Same-state transitions are not permitted.");
        }

        if (IsTerminal(from))
        {
            throw new InvalidWorkflowTransitionException(
                $"Cannot transition AgentWorkflow from terminal state '{from}' to '{to}'. Terminal states cannot be transitioned out of.");
        }

        if (!CanTransition(from, to))
        {
            throw new InvalidWorkflowTransitionException(from, to);
        }
    }

    public IReadOnlySet<AgentWorkflowStatus> GetPermittedTransitions(AgentWorkflowStatus from)
    {
        return LegalTransitions.TryGetValue(from, out var targets)
            ? targets
            : (IReadOnlySet<AgentWorkflowStatus>)new HashSet<AgentWorkflowStatus>();
    }

    public bool IsTerminal(AgentWorkflowStatus status)
    {
        return TerminalStatuses.Contains(status);
    }
}
