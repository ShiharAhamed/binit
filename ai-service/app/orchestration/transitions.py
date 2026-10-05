from typing import Any, Dict, List, Optional, Union
from uuid import UUID

from app.models.shared_planner import SharedPlannerResult, SpecialistType
from app.models.workflow_trigger import WorkflowTriggerType, validate_trigger_pair
from app.orchestration.enums import ApprovalPauseStage, OrchestrationPhase
from app.orchestration.errors import OrchestrationError
from app.orchestration.state import OrchestrationState


def create_initial_state(
    workflow_id: Optional[Union[str, UUID]],
    objective: str,
    trigger_type: WorkflowTriggerType = WorkflowTriggerType.ManualOperationalPlanning,
    triggering_waste_report_id: Optional[UUID] = None,
) -> OrchestrationState:
    """Creates a pristine initial OrchestrationState."""
    parsed_id: Optional[UUID] = None
    if workflow_id:
        parsed_id = workflow_id if isinstance(workflow_id, UUID) else UUID(str(workflow_id))
    validate_trigger_pair(trigger_type, triggering_waste_report_id)
    return OrchestrationState(
        workflow_id=parsed_id,
        objective=objective.strip(),
        trigger_type=trigger_type,
        triggering_waste_report_id=triggering_waste_report_id,
        planner_result=None,
        current_phase=OrchestrationPhase.NotStarted,
        current_specialist=None,
        completed_specialists=[],
        waste_analysis_result=None,
        collection_planning_result=None,
        fleet_route_result=None,
        validation_operations_result=None,
        pause_stage=ApprovalPauseStage.NONE,
        pause_reason=None,
        errors=[],
        warnings=[],
        final_outcome=None,
    )


def mark_planning_started() -> Dict[str, Any]:
    """Returns a state update dictionary recording that planning has started."""
    return {
        "current_phase": OrchestrationPhase.Planning,
        "current_specialist": None,
    }


def mark_planning_completed(
    planner_result: SharedPlannerResult,
    existing_warnings: Optional[List[str]] = None,
) -> Dict[str, Any]:
    """Returns a state update recording successful completion of the Shared Planner."""
    warnings = list(existing_warnings or [])
    for w in planner_result.warnings:
        if w not in warnings:
            warnings.append(w)
    return {
        "planner_result": planner_result,
        "warnings": warnings,
    }


def mark_specialist_started(specialist: SpecialistType) -> Dict[str, Any]:
    """Returns a state update setting the phase and active specialist."""
    phase_map = {
        SpecialistType.WasteAnalysis: OrchestrationPhase.RunningWasteAnalysis,
        SpecialistType.CollectionPlanning: OrchestrationPhase.RunningCollectionPlanning,
        SpecialistType.FleetRoute: OrchestrationPhase.RunningFleetPlanning,
        SpecialistType.ValidationOperations: OrchestrationPhase.RunningOperationalValidation,
    }
    return {
        "current_phase": phase_map[specialist],
        "current_specialist": specialist,
    }


def mark_specialist_completed(
    specialist: SpecialistType,
    result: Any,
    completed_specialists: Optional[List[SpecialistType]] = None,
) -> Dict[str, Any]:
    """Returns a state update placing the specialist's result in its slot."""
    slot_map = {
        SpecialistType.WasteAnalysis: "waste_analysis_result",
        SpecialistType.CollectionPlanning: "collection_planning_result",
        SpecialistType.FleetRoute: "fleet_route_result",
        SpecialistType.ValidationOperations: "validation_operations_result",
    }
    updated_completed = list(completed_specialists or [])
    if specialist not in updated_completed:
        updated_completed.append(specialist)

    return {
        slot_map[specialist]: result,
        "completed_specialists": updated_completed,
        "current_specialist": None,
    }


def mark_paused_for_approval(
    approval_stage: ApprovalPauseStage,
    message: str,
) -> Dict[str, Any]:
    """Returns a state update recording that orchestration has paused for human approval."""
    if approval_stage == ApprovalPauseStage.NONE:
        raise ValueError("Cannot pause for ApprovalPauseStage.NONE.")

    phase_map = {
        ApprovalPauseStage.REPORT_VERIFICATION: OrchestrationPhase.PausedForReportVerification,
        ApprovalPauseStage.COLLECTION_PLANNING: OrchestrationPhase.PausedForCollectionApproval,
        ApprovalPauseStage.FLEET_DISPATCH: OrchestrationPhase.PausedForDispatchApproval,
    }
    return {
        "current_phase": phase_map[approval_stage],
        "pause_stage": approval_stage,
        "pause_reason": message,
        "current_specialist": None,
    }


def mark_failed(
    error: OrchestrationError,
    existing_errors: Optional[List[OrchestrationError]] = None,
) -> Dict[str, Any]:
    """Returns a state update recording orchestration failure."""
    updated_errors = list(existing_errors or [])
    updated_errors.append(error)
    return {
        "current_phase": OrchestrationPhase.Failed,
        "current_specialist": None,
        "pause_stage": ApprovalPauseStage.NONE,
        "pause_reason": None,
        "errors": updated_errors,
    }


def mark_dispatch_needs_revision() -> Dict[str, Any]:
    """Returns a valid non-failure dispatch revision outcome after C4 validation."""
    return {
        "current_phase": OrchestrationPhase.DispatchNeedsRevision,
        "current_specialist": None,
        "pause_stage": ApprovalPauseStage.NONE,
        "pause_reason": None,
        "final_outcome": "DispatchNeedsRevision",
    }


def mark_completed(final_outcome: str) -> Dict[str, Any]:
    """Returns a state update recording orchestration completion."""
    return {
        "current_phase": OrchestrationPhase.Completed,
        "current_specialist": None,
        "pause_stage": ApprovalPauseStage.NONE,
        "pause_reason": None,
        "final_outcome": final_outcome,
    }
