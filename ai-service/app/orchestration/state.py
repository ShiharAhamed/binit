from typing import Any, Dict, List, Literal, Optional, TypedDict
from uuid import UUID

from pydantic import BaseModel, ConfigDict, Field, model_validator

from app.models.analysis import WasteAnalysisResult
from app.models.collection_planning import CollectionPlanningResult
from app.models.fleet_route import FleetRouteResult
from app.models.shared_planner import SharedPlannerResult, SpecialistType
from app.models.workflow_trigger import WorkflowTriggerType, validate_trigger_pair
from app.models.validation_operations import ValidationOperationsResult
from app.orchestration.enums import (
    ApprovalPauseStage,
    OrchestrationPhase,
    OrchestrationStatus,
    ResumeDecision,
)
from app.orchestration.errors import OrchestrationError


class OrchestrationState(TypedDict, total=False):
    """Strongly typed LangGraph shared state for the SmartWaste orchestrator.

    Stores structured inputs, planner proposals, specialist outputs, and execution
    metadata. Strictly excludes hidden reasoning traces, model scratchpads, raw prompts,
    and user/provider credentials.
    """

    workflow_id: Optional[UUID]
    objective: str
    trigger_type: WorkflowTriggerType
    triggering_waste_report_id: Optional[UUID]
    planner_result: Optional[SharedPlannerResult]
    current_phase: OrchestrationPhase
    current_specialist: Optional[SpecialistType]
    completed_specialists: List[SpecialistType]
    waste_analysis_result: Optional[WasteAnalysisResult]
    collection_planning_result: Optional[CollectionPlanningResult]
    fleet_route_result: Optional[FleetRouteResult]
    validation_operations_result: Optional[ValidationOperationsResult]
    pause_stage: ApprovalPauseStage
    pause_reason: Optional[str]
    errors: List[OrchestrationError]
    warnings: List[str]
    final_outcome: Optional[str]


class OrchestrationPauseResult(BaseModel):
    """Structured information returned to ASP.NET Core when orchestration pauses for human approval."""

    model_config = ConfigDict(populate_by_name=True, extra="forbid", str_strip_whitespace=True)

    pause_type: Literal["HumanApproval"] = Field(default="HumanApproval", alias="pauseType")
    approval_stage: ApprovalPauseStage = Field(..., alias="approvalStage")
    message: str = Field(..., min_length=5, max_length=1000)
    next_expected_action: str = Field(
        default="ASP.NET human approval",
        alias="nextExpectedAction",
        description="The authoritative action required from ASP.NET Core before orchestration can resume.",
    )


class WorkflowResumeContext(BaseModel):
    """Contract supplied by ASP.NET Core when resuming orchestration after an authoritative decision."""

    model_config = ConfigDict(populate_by_name=True, extra="forbid", str_strip_whitespace=True)

    workflow_id: UUID = Field(..., alias="workflowId")
    approval_stage: ApprovalPauseStage = Field(..., alias="approvalStage")
    decision: ResumeDecision = Field(..., alias="decision")
    authoritative_execution_summary: Optional[Dict[str, Any]] = Field(
        default=None,
        alias="authoritativeExecutionSummary",
        description="Structured summary of backend mutations executed before resumption (e.g. created task IDs).",
    )


class OrchestrationResultEnvelope(BaseModel):
    """Top-level response envelope returned from Python orchestration to ASP.NET Core."""

    model_config = ConfigDict(populate_by_name=True, extra="forbid", str_strip_whitespace=True)

    workflow_id: Optional[UUID] = Field(default=None, alias="workflowId")
    objective: Optional[str] = Field(default=None, alias="objective")
    trigger_type: WorkflowTriggerType = Field(
        default=WorkflowTriggerType.ManualOperationalPlanning,
        alias="triggerType",
    )
    triggering_waste_report_id: Optional[UUID] = Field(default=None, alias="triggeringWasteReportId")
    status: OrchestrationStatus = Field(..., alias="status")
    current_phase: OrchestrationPhase = Field(..., alias="currentPhase")
    current_specialist: Optional[SpecialistType] = Field(default=None, alias="currentSpecialist")
    approval_stage: ApprovalPauseStage = Field(
        default=ApprovalPauseStage.NONE,
        alias="approvalStage",
    )
    pause_reason: Optional[str] = Field(default=None, alias="pauseReason")
    planner_result: Optional[SharedPlannerResult] = Field(default=None, alias="plannerResult")
    waste_analysis_result: Optional[WasteAnalysisResult] = Field(
        default=None,
        alias="wasteAnalysisResult",
    )
    collection_planning_result: Optional[CollectionPlanningResult] = Field(
        default=None,
        alias="collectionPlanningResult",
    )
    fleet_route_result: Optional[FleetRouteResult] = Field(
        default=None,
        alias="fleetRouteResult",
    )
    validation_operations_result: Optional[ValidationOperationsResult] = Field(
        default=None,
        alias="validationOperationsResult",
    )
    completed_specialists: List[SpecialistType] = Field(
        default_factory=list,
        alias="completedSpecialists",
    )
    errors: List[OrchestrationError] = Field(default_factory=list, alias="errors")
    warnings: List[str] = Field(default_factory=list, alias="warnings")
    final_outcome: Optional[str] = Field(default=None, alias="finalOutcome")

    @model_validator(mode="after")
    def validate_trigger(self) -> "OrchestrationResultEnvelope":
        validate_trigger_pair(self.trigger_type, self.triggering_waste_report_id)
        return self


class StateInvariantError(ValueError):
    """Raised when OrchestrationState violates system invariants."""

    pass


FORBIDDEN_REASONING_KEYS = {
    "chain_of_thought",
    "chainOfThought",
    "reasoning_trace",
    "reasoningTrace",
    "model_scratchpad",
    "modelScratchpad",
    "raw_prompt",
    "rawPrompt",
    "internal_thoughts",
    "internalThoughts",
}


def validate_state_invariants(state: OrchestrationState) -> None:
    """Verifies that an OrchestrationState satisfies all architectural constraints."""
    # 1. No hidden reasoning keys
    for key in state.keys():
        if key in FORBIDDEN_REASONING_KEYS:
            raise StateInvariantError(
                f"Forbidden hidden reasoning key '{key}' detected in OrchestrationState."
            )

    try:
        validate_trigger_pair(
            state.get("trigger_type", WorkflowTriggerType.ManualOperationalPlanning),
            state.get("triggering_waste_report_id"),
        )
    except ValueError as exception:
        raise StateInvariantError(str(exception)) from exception

    # 2. Pause invariants
    pause_stage = state.get("pause_stage", ApprovalPauseStage.NONE)
    if pause_stage != ApprovalPauseStage.NONE:
        if not state.get("pause_reason"):
            raise StateInvariantError(
                f"Active pause stage '{pause_stage}' requires a non-empty pause_reason."
            )

    # 3. Completed state invariants
    phase = state.get("current_phase", OrchestrationPhase.NotStarted)
    if phase == OrchestrationPhase.PausedForReportVerification:
        if state.get("trigger_type") != WorkflowTriggerType.CitizenReportSubmission:
            raise StateInvariantError("Report verification pause requires CitizenReportSubmission trigger.")
        if pause_stage != ApprovalPauseStage.REPORT_VERIFICATION:
            raise StateInvariantError("Report verification pause requires ReportVerification pause stage.")
        if state.get("planner_result") is None or state.get("waste_analysis_result") is None:
            raise StateInvariantError("Report verification pause requires Shared Planner and C1 results.")
        if state.get("collection_planning_result") is not None:
            raise StateInvariantError("Report verification pause cannot include C2 output.")
        analyses = state["waste_analysis_result"].analyses
        if len(analyses) != 1 or analyses[0].report_id != state.get("triggering_waste_report_id"):
            raise StateInvariantError("Report verification pause requires exactly one analysis of the triggering report.")
        if state.get("current_specialist") is not None:
            raise StateInvariantError("Report verification pause cannot have an active specialist.")
    if phase == OrchestrationPhase.Completed:
        if pause_stage != ApprovalPauseStage.NONE:
            raise StateInvariantError("Completed orchestration state cannot have an active pause.")
        if state.get("current_specialist") is not None:
            raise StateInvariantError("Completed orchestration state cannot have an active specialist.")

    # 4. Failed state invariants
    if phase == OrchestrationPhase.Failed:
        if not state.get("errors"):
            raise StateInvariantError("Failed orchestration state must contain at least one error.")

    # 5. Dispatch outcome invariants
    fleet_result = state.get("fleet_route_result")
    validation_result = state.get("validation_operations_result")
    current_specialist = state.get("current_specialist")
    if phase == OrchestrationPhase.PausedForDispatchApproval:
        if fleet_result is None or validation_result is None:
            raise StateInvariantError(
                "PausedForDispatchApproval requires FleetRouteResult and ValidationOperationsResult."
            )
        if validation_result.validation_outcome != "ReadyForHumanReview":
            raise StateInvariantError(
                "PausedForDispatchApproval requires ValidationOperationsResult outcome ReadyForHumanReview."
            )
        if pause_stage != ApprovalPauseStage.FLEET_DISPATCH:
            raise StateInvariantError("PausedForDispatchApproval requires FleetDispatch pause stage.")
        if current_specialist is not None:
            raise StateInvariantError("PausedForDispatchApproval cannot have an active specialist.")

    if phase == OrchestrationPhase.DispatchNeedsRevision:
        if fleet_result is None or validation_result is None:
            raise StateInvariantError(
                "DispatchNeedsRevision requires FleetRouteResult and ValidationOperationsResult."
            )
        if validation_result.validation_outcome != "NeedsRevision":
            raise StateInvariantError(
                "DispatchNeedsRevision requires ValidationOperationsResult outcome NeedsRevision."
            )
        if pause_stage != ApprovalPauseStage.NONE:
            raise StateInvariantError("DispatchNeedsRevision cannot have an active approval pause.")
        if current_specialist is not None:
            raise StateInvariantError("DispatchNeedsRevision cannot have an active specialist.")
        if state.get("errors"):
            raise StateInvariantError("DispatchNeedsRevision is a valid outcome and cannot contain errors.")

    # 6. Specialist dependency sequence invariants
    if fleet_result is not None:
        if state.get("collection_planning_result") is None:
            raise StateInvariantError(
                "FleetRouteResult cannot exist in state without an existing CollectionPlanningResult."
            )

    if validation_result is not None:
        if fleet_result is None:
            raise StateInvariantError(
                "ValidationOperationsResult cannot exist in state without an existing FleetRouteResult."
            )
