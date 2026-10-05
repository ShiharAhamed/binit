using SmartWaste.Domain.Workflow.Enums;

namespace SmartWaste.Application.Workflow.DTOs.Responses;

/// <summary>
/// Immutable audit transition log entry.
/// </summary>
public class AgentWorkflowTransitionDto
{
    public Guid Id { get; set; }
    public AgentWorkflowStatus? FromStatus { get; set; }
    public AgentWorkflowStatus ToStatus { get; set; }
    public string? Reason { get; set; }
    public Guid? ChangedByUserId { get; set; }
    public DateTime ChangedAt { get; set; }
}
