using System.Text.Json;
using SmartWaste.Domain.Workflow.Enums;

namespace SmartWaste.Application.Workflow.DTOs.Responses;

/// <summary>
/// Human-in-the-loop decision record.
/// </summary>
public class AgentWorkflowApprovalDto
{
    public Guid Id { get; set; }
    public Guid? WorkflowStepId { get; set; }
    public WorkflowApprovalStage ApprovalStage { get; set; }
    public WorkflowApprovalDecision Decision { get; set; }
    public string? DecisionReason { get; set; }
    public JsonElement? DecisionPayload { get; set; }
    public Guid DecidedByUserId { get; set; }
    public DateTime DecidedAt { get; set; }
}
