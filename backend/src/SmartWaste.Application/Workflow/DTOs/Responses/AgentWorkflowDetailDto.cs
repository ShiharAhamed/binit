using SmartWaste.Domain.Workflow.Enums;
using SmartWaste.Application.Reporting;

namespace SmartWaste.Application.Workflow.DTOs.Responses;

/// <summary>
/// Full structured view of an AgentWorkflow aggregate including child step executions,
/// audit transition logs, human approvals, and execution results.
/// </summary>
public class AgentWorkflowDetailDto
{
    public Guid Id { get; set; }
    public AgentWorkflowTriggerType TriggerType { get; set; } = AgentWorkflowTriggerType.ManualOperationalPlanning;
    public Guid? TriggeringWasteReportId { get; set; }
    public string? ReportReference => TriggeringWasteReportId is Guid id ? WasteReportReference.FromId(id) : null;
    public string Objective { get; set; } = string.Empty;
    public AgentWorkflowStatus Status { get; set; }
    public WorkflowStepType CurrentStep { get; set; }
    public Guid InitiatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? FinalOutcome { get; set; }
    public int Version { get; set; }

    public IReadOnlyList<AgentWorkflowStepDto> Steps { get; set; } = Array.Empty<AgentWorkflowStepDto>();
    public IReadOnlyList<AgentWorkflowTransitionDto> Transitions { get; set; } = Array.Empty<AgentWorkflowTransitionDto>();
    public IReadOnlyList<AgentWorkflowApprovalDto> Approvals { get; set; } = Array.Empty<AgentWorkflowApprovalDto>();
    public IReadOnlyList<AgentWorkflowExecutionResultDto> ExecutionResults { get; set; } = Array.Empty<AgentWorkflowExecutionResultDto>();
    public IReadOnlyDictionary<Guid, string> BinCodes { get; set; } = new Dictionary<Guid, string>();
}
