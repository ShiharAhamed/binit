using System.Text.Json;
using SmartWaste.Domain.Workflow.Enums;

namespace SmartWaste.Application.Workflow.DTOs.Responses;

/// <summary>
/// Structured execution record for an agent specialist or system bridge step.
/// Excludes hidden reasoning or internal navigation internals.
/// </summary>
public class AgentWorkflowStepDto
{
    public Guid Id { get; set; }
    public int Sequence { get; set; }
    public WorkflowStepType StepType { get; set; }
    public string? AgentName { get; set; }
    public WorkflowStepStatus Status { get; set; }
    public JsonElement? Input { get; set; }
    public JsonElement? Output { get; set; }
    public JsonElement? Validation { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
