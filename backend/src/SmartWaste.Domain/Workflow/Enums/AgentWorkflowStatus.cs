namespace SmartWaste.Domain.Workflow.Enums;

/// <summary>
/// Authoritative lifecycle status for an end-to-end AgentWorkflow in ASP.NET Core and PostgreSQL.
/// </summary>
public enum AgentWorkflowStatus
{
    /// <summary>Workflow record created; processing has not started.</summary>
    Created = 0,

    /// <summary>Initial planning phase running (Shared Planner / C1 Waste Analysis / C2 Collection Planning).</summary>
    Planning = 1,

    /// <summary>C2 proposal complete with full snapshot; workflow paused for human scheduling approval.</summary>
    AwaitingCollectionApproval = 2,

    /// <summary>Human reviewer requested modifications to the collection proposal.</summary>
    CollectionNeedsRevision = 3,

    /// <summary>Human approved collection proposal; scheduled task creation pending.</summary>
    CollectionApproved = 4,

    /// <summary>ASP.NET is converting approved C2 collection needs into authoritative Scheduled CollectionTasks.</summary>
    CreatingScheduledTasks = 5,

    /// <summary>C3 Fleet & Route Agent is formulating multi-plan dispatch proposals for Scheduled tasks.</summary>
    FleetPlanning = 6,

    /// <summary>C4 Validation & Operations Agent is checking dispatch plans against fresh operational state.</summary>
    OperationalValidation = 7,

    /// <summary>C4 validation returned a reviewable dispatch proposal; paused for human fleet authorization.</summary>
    AwaitingDispatchApproval = 8,

    /// <summary>Fleet dispatch proposal requires revision or replanning.</summary>
    DispatchNeedsRevision = 9,

    /// <summary>Human approved fleet dispatch proposal; assignment execution pending.</summary>
    DispatchApproved = 10,

    /// <summary>ASP.NET is executing approved assignments via CollectionAssignmentService.</summary>
    ExecutingAssignments = 11,

    /// <summary>
    /// The approved scope of this Agentic workflow has finished successfully and its authoritative
    /// execution results/final outcome have been recorded. Any tasks intentionally remaining unplanned
    /// or not executed are explicitly represented in the persisted workflow outcome/execution results (terminal).
    /// </summary>
    Completed = 12,

    /// <summary>Workflow proposal was rejected or its source report was withdrawn (terminal).</summary>
    Rejected = 13,

    /// <summary>Workflow encountered an unrecoverable failure (terminal).</summary>
    Failed = 14,

    /// <summary>Exact-report C1 analysis completed; awaiting authoritative WasteReport review.</summary>
    AwaitingReportVerification = 15
}
