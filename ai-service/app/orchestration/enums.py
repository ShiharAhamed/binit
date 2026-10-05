from enum import Enum

from app.models.workflow_trigger import WorkflowTriggerType


class SharedPlannerPolicy(str, Enum):
    """Deterministic planning policy applied in addition to generic DAG validation."""

    Generic = "Generic"
    EndToEndCollectionOperation = "EndToEndCollectionOperation"


class OrchestrationPhase(str, Enum):
    """Internal orchestration control phases for LangGraph specialist progression.

    IMPORTANT ARCHITECTURAL DISTINCTION:
    These are internal Python orchestration phases, NOT authoritative workflow statuses.
    ASP.NET Core AgentWorkflowStatus (e.g. Created, WasteAnalysis, CollectionApproved, etc.)
    remains the sole authoritative lifecycle state in PostgreSQL.
    """

    NotStarted = "NotStarted"
    Planning = "Planning"
    RunningWasteAnalysis = "RunningWasteAnalysis"
    RunningCollectionPlanning = "RunningCollectionPlanning"
    PausedForReportVerification = "PausedForReportVerification"
    PausedForCollectionApproval = "PausedForCollectionApproval"
    RunningFleetPlanning = "RunningFleetPlanning"
    RunningOperationalValidation = "RunningOperationalValidation"
    PausedForDispatchApproval = "PausedForDispatchApproval"
    DispatchNeedsRevision = "DispatchNeedsRevision"
    Completed = "Completed"
    Failed = "Failed"


class ApprovalPauseStage(str, Enum):
    """Indicates which human approval stage orchestration has paused for.

    This represents the pause boundary reason, NOT the human approval decision itself.
    Authoritative decisions (Approved, RevisionRequested, Rejected) are made in ASP.NET Core
    and passed back to Python via the resume context.
    """

    NONE = "None"
    REPORT_VERIFICATION = "ReportVerification"
    COLLECTION_PLANNING = "CollectionPlanning"
    FLEET_DISPATCH = "FleetDispatch"


class ResumeDecision(str, Enum):
    """The authoritative human decision provided by ASP.NET Core when resuming orchestration."""

    APPROVED = "Approved"
    REVISION_REQUESTED = "RevisionRequested"
    REJECTED = "Rejected"


class GraphNodeId(str, Enum):
    """Centralized identifiers for all nodes in the LangGraph orchestration graph."""

    SHARED_PLANNER = "shared_planner"
    WASTE_ANALYSIS = "waste_analysis"
    PAUSE_REPORT_VERIFICATION = "pause_report_verification"
    COLLECTION_PLANNING = "collection_planning"
    PAUSE_COLLECTION_APPROVAL = "pause_collection_approval"
    FLEET_ROUTE = "fleet_route"
    VALIDATION_OPERATIONS = "validation_operations"
    PAUSE_DISPATCH_APPROVAL = "pause_dispatch_approval"
    DISPATCH_NEEDS_REVISION = "dispatch_needs_revision"
    FINISH = "finish"
    FAILURE = "failure"


class OrchestrationStatus(str, Enum):
    """High-level envelope status returned to ASP.NET Core."""

    RUNNING = "Running"
    PAUSED = "Paused"
    COMPLETED = "Completed"
    FAILED = "Failed"
