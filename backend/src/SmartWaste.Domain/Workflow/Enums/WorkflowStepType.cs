namespace SmartWaste.Domain.Workflow.Enums;

/// <summary>
/// Logical specialist or system execution step within an AgentWorkflow.
/// Provides durable operational progress tracking for the future orchestrator without altering authoritative status.
/// </summary>
public enum WorkflowStepType
{
    None = 0,
    SharedPlanning = 1,
    WasteAnalysis = 2,
    CollectionPlanning = 3,
    ScheduledTaskCreation = 4,
    FleetPlanning = 5,
    OperationalValidation = 6,
    DispatchApproval = 7,
    AssignmentExecution = 8
}
