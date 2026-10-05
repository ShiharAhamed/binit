using System.Text.Json;
using System.Text.Json.Serialization;
using SmartWaste.Domain.Workflow.Enums;

namespace SmartWaste.Application.Workflow.DTOs.Transport;

public sealed class PythonWorkflowStartRequest
{
    [JsonPropertyName("workflowId")] public Guid WorkflowId { get; init; }
    [JsonPropertyName("objective")] public string Objective { get; init; } = string.Empty;
    [JsonPropertyName("triggerType"), JsonConverter(typeof(JsonStringEnumConverter))]
    public AgentWorkflowTriggerType TriggerType { get; init; } = AgentWorkflowTriggerType.ManualOperationalPlanning;
    [JsonPropertyName("triggeringWasteReportId")] public Guid? TriggeringWasteReportId { get; init; }
}

public sealed class PythonWorkflowResumeRequest
{
    [JsonPropertyName("workflow")] public PythonOrchestrationEnvelope Workflow { get; init; } = new();
    [JsonPropertyName("resumeContext")] public PythonResumeContext ResumeContext { get; init; } = new();
}

public sealed class PythonResumeContext
{
    [JsonPropertyName("workflowId")] public Guid WorkflowId { get; init; }
    [JsonPropertyName("approvalStage")] public string ApprovalStage { get; init; } = "CollectionPlanning";
    [JsonPropertyName("decision")] public string Decision { get; init; } = "Approved";
    [JsonPropertyName("authoritativeExecutionSummary")] public JsonElement AuthoritativeExecutionSummary { get; init; }
}

public sealed class PythonOrchestrationEnvelope
{
    [JsonPropertyName("workflowId")] public Guid? WorkflowId { get; init; }
    [JsonPropertyName("objective")] public string? Objective { get; init; }
    [JsonPropertyName("triggerType"), JsonConverter(typeof(JsonStringEnumConverter))]
    public AgentWorkflowTriggerType TriggerType { get; init; } = AgentWorkflowTriggerType.ManualOperationalPlanning;
    [JsonPropertyName("triggeringWasteReportId")] public Guid? TriggeringWasteReportId { get; init; }
    [JsonPropertyName("status")] public string? Status { get; init; }
    [JsonPropertyName("currentPhase")] public string? CurrentPhase { get; init; }
    [JsonPropertyName("currentSpecialist")] public string? CurrentSpecialist { get; init; }
    [JsonPropertyName("approvalStage")] public string? ApprovalStage { get; init; }
    [JsonPropertyName("pauseReason")] public string? PauseReason { get; init; }
    [JsonPropertyName("plannerResult")] public JsonElement? PlannerResult { get; init; }
    [JsonPropertyName("wasteAnalysisResult")] public JsonElement? WasteAnalysisResult { get; init; }
    [JsonPropertyName("collectionPlanningResult")] public JsonElement? CollectionPlanningResult { get; init; }
    [JsonPropertyName("fleetRouteResult")] public JsonElement? FleetRouteResult { get; init; }
    [JsonPropertyName("validationOperationsResult")] public JsonElement? ValidationOperationsResult { get; init; }
    [JsonPropertyName("completedSpecialists")] public List<string> CompletedSpecialists { get; init; } = [];
    [JsonPropertyName("errors")] public JsonElement? Errors { get; init; }
    [JsonPropertyName("warnings")] public JsonElement? Warnings { get; init; }
    [JsonPropertyName("finalOutcome")] public string? FinalOutcome { get; init; }
}
