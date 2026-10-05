using System.Text.Json;
using SmartWaste.Domain.Workflow.Enums;

namespace SmartWaste.Application.Workflow.DTOs.Responses;

/// <summary>
/// Authoritative system execution outcome.
/// </summary>
public class AgentWorkflowExecutionResultDto
{
    public Guid Id { get; set; }
    public Guid? WorkflowStepId { get; set; }
    public WorkflowExecutionType ExecutionType { get; set; }
    public WorkflowExecutionStatus Status { get; set; }
    public JsonElement? Result { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime ExecutedAt { get; set; }
}
