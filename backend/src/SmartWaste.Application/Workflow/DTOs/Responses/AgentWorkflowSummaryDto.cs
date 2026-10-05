using SmartWaste.Domain.Workflow.Enums;
using SmartWaste.Application.Reporting;

namespace SmartWaste.Application.Workflow.DTOs.Responses;

/// <summary>
/// Lightweight workflow summary for list queries and initial mutation responses.
/// </summary>
public class AgentWorkflowSummaryDto
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
}
