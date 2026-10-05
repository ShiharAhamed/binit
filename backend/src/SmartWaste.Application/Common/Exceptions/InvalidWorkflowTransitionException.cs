using SmartWaste.Domain.Workflow.Enums;

namespace SmartWaste.Application.Common.Exceptions;

/// <summary>
/// Thrown when an illegal lifecycle state transition is attempted on an AgentWorkflow.
/// Inherits from BusinessRuleConflictException, which maps to HTTP 409 Conflict.
/// </summary>
public class InvalidWorkflowTransitionException : BusinessRuleConflictException
{
    public AgentWorkflowStatus FromStatus { get; }
    public AgentWorkflowStatus ToStatus { get; }

    public InvalidWorkflowTransitionException(AgentWorkflowStatus fromStatus, AgentWorkflowStatus toStatus)
        : base($"Cannot transition AgentWorkflow from '{fromStatus}' to '{toStatus}'. This state transition is not permitted by the workflow state machine.")
    {
        FromStatus = fromStatus;
        ToStatus = toStatus;
    }

    public InvalidWorkflowTransitionException(string message)
        : base(message)
    {
    }
}
