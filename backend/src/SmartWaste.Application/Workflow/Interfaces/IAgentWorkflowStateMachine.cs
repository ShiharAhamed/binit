using SmartWaste.Domain.Workflow.Enums;

namespace SmartWaste.Application.Workflow.Interfaces;

/// <summary>
/// Deterministic state machine governing legal lifecycle state transitions for AgentWorkflow.
/// Strictly programmatic; operates without LLM inference or free-text parsing.
/// </summary>
public interface IAgentWorkflowStateMachine
{
    /// <summary>
    /// Evaluates whether transitioning from one status to another is legal.
    /// </summary>
    bool CanTransition(AgentWorkflowStatus from, AgentWorkflowStatus to);

    /// <summary>
    /// Throws InvalidWorkflowTransitionException if the transition is illegal.
    /// </summary>
    void EnsureCanTransition(AgentWorkflowStatus from, AgentWorkflowStatus to);

    /// <summary>
    /// Returns the set of valid next statuses reachable from the current status.
    /// </summary>
    IReadOnlySet<AgentWorkflowStatus> GetPermittedTransitions(AgentWorkflowStatus from);

    /// <summary>
    /// Indicates whether the given status is a terminal state (Completed, Rejected, Failed).
    /// </summary>
    bool IsTerminal(AgentWorkflowStatus status);
}
