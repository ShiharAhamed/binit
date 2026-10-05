from collections.abc import Callable
from typing import Any, Dict, Optional
from uuid import UUID

from langgraph.graph import END, START, StateGraph

from app.models.analysis import WasteAnalysisRequest, WasteAnalysisResult
from app.models.collection_planning import CollectionPlanningRequest, CollectionPlanningResult
from app.models.fleet_route import FleetRouteRequest, FleetRouteResult
from app.models.shared_planner import (
    SharedPlannerRequest,
    SharedPlannerResult,
    SpecialistType,
    create_flagship_planner_result,
    validate_planner_result,
)
from app.models.workflow_trigger import WorkflowTriggerType
from app.models.validation_operations import (
    ValidationOperationsRequest,
    ValidationOperationsResult,
)
from app.orchestration.enums import (
    ApprovalPauseStage,
    GraphNodeId,
    OrchestrationPhase,
    OrchestrationStatus,
    ResumeDecision,
    SharedPlannerPolicy,
)
from app.orchestration.errors import (
    COLLECTION_PLANNING_FAILED,
    FLEET_PLANNING_FAILED,
    INVALID_RESUME_CONTEXT,
    OPERATIONAL_VALIDATION_FAILED,
    PLANNER_VALIDATION_FAILED,
    WASTE_ANALYSIS_FAILED,
    create_orchestration_error,
)
from app.orchestration.state import (
    OrchestrationResultEnvelope,
    OrchestrationState,
    WorkflowResumeContext,
    validate_state_invariants,
)
from app.orchestration.transitions import (
    create_initial_state,
    mark_completed,
    mark_failed,
    mark_dispatch_needs_revision,
    mark_paused_for_approval,
    mark_planning_completed,
    mark_planning_started,
    mark_specialist_completed,
    mark_specialist_started,
)


PlannerRunner = Callable[..., SharedPlannerResult]
WasteAnalysisRunner = Callable[[WasteAnalysisRequest], WasteAnalysisResult]
CollectionPlanningRunner = Callable[[CollectionPlanningRequest], CollectionPlanningResult]
FleetRouteRunner = Callable[[FleetRouteRequest], FleetRouteResult]
ValidationOperationsRunner = Callable[[ValidationOperationsRequest], ValidationOperationsResult]

COLLECTION_APPROVAL_PAUSE_REASON = (
    "Collection planning is ready for authorized human review. Scheduled collection tasks "
    "must be created authoritatively after approval before fleet planning can continue."
)
REPORT_VERIFICATION_PAUSE_REASON = (
    "Waste report analysis is ready for authorized human verification. "
    "Collection planning may continue only after ASP.NET confirms the report was verified."
)
DISPATCH_APPROVAL_PAUSE_REASON = (
    "Fleet dispatch planning and operational validation are complete. The proposed dispatch "
    "plan is ready for authorized human review before CollectionAssignments can be created."
)


def deterministic_shared_planner_stub(state: OrchestrationState) -> Dict[str, Any]:
    """Deterministic Shared Planner node stub for Step 9A foundation.

    In Step 9A, this stub executes deterministically without making any LLM / model calls.
    If a `planner_result` already exists in state, it is validated.
    If not, the canonical flagship 4-step plan is generated and validated.
    Validation failures transition state to Failed with PLANNER_VALIDATION_FAILED error.
    """
    try:
        planner_result = state.get("planner_result")
        if planner_result is None:
            objective = state.get("objective") or "SmartWaste End-to-End Dynamic Workflow"
            planner_result = create_flagship_planner_result(objective=objective)

        # Validate deterministic plan rules
        validate_planner_result(planner_result)
        return mark_planning_completed(planner_result, state.get("warnings"))
    except Exception as ex:
        error = create_orchestration_error(
            code=PLANNER_VALIDATION_FAILED,
            stage="SharedPlanner",
            message=str(ex),
            retryable=False,
        )
        return mark_failed(error, state.get("errors"))


def finish_node(state: OrchestrationState) -> Dict[str, Any]:
    """Terminal node representing successful completion of the Step 9A skeleton execution.

    IMPORTANT SKELETON SEMANTICS:
    In Step 9A, reaching this node and marking OrchestrationPhase.Completed signifies solely that
    the foundation graph skeleton executed deterministically to completion. It does NOT mean the real
    end-to-end SmartWaste workflow completed (C1, C2, human collection approval, CollectionTask creation,
    C3, C4, human dispatch approval, and CollectionAssignment creation occur in future stages).
    """
    outcome = state.get("final_outcome") or "Step 9A orchestration skeleton completed successfully."
    return mark_completed(outcome)


def failure_node(state: OrchestrationState) -> Dict[str, Any]:
    """Terminal node representing failed workflow execution."""
    return {
        "final_outcome": state.get("final_outcome")
        or "Execution terminated due to orchestration error.",
    }


def route_after_planner(state: OrchestrationState) -> str:
    """Conditional routing function determining next step after Shared Planner execution.

    In Step 9A, if errors were encountered during planning, routes to failure.
    Otherwise, routes to finish. (In future steps, routes to waste_analysis).
    """
    if state.get("errors"):
        return GraphNodeId.FAILURE.value
    return GraphNodeId.FINISH.value


def build_step9a_skeleton_graph() -> StateGraph:
    """Constructs the uncompiled Step 9A LangGraph StateGraph.

    Architecture:
    START -> shared_planner -> (conditional) -> finish  -> END
                                             -> failure -> END
    """
    workflow = StateGraph(OrchestrationState)

    # Register nodes
    workflow.add_node(GraphNodeId.SHARED_PLANNER.value, deterministic_shared_planner_stub)
    workflow.add_node(GraphNodeId.FINISH.value, finish_node)
    workflow.add_node(GraphNodeId.FAILURE.value, failure_node)

    # Register edges
    workflow.add_edge(START, GraphNodeId.SHARED_PLANNER.value)
    workflow.add_conditional_edges(
        GraphNodeId.SHARED_PLANNER.value,
        route_after_planner,
        {
            GraphNodeId.FINISH.value: GraphNodeId.FINISH.value,
            GraphNodeId.FAILURE.value: GraphNodeId.FAILURE.value,
        },
    )
    workflow.add_edge(GraphNodeId.FINISH.value, END)
    workflow.add_edge(GraphNodeId.FAILURE.value, END)

    return workflow


def build_workflow_graph() -> Any:
    """Builds and compiles the Step 9A LangGraph orchestration workflow."""
    return build_step9a_skeleton_graph().compile()


def _planner_step_objective(
    planner_result: SharedPlannerResult,
    specialist: SpecialistType,
) -> str:
    """Return the already-validated planner objective for one required specialist."""
    for step in planner_result.steps:
        if step.specialist == specialist:
            return step.objective
    raise ValueError(f"Shared Planner result does not include required {specialist.value} step.")


def _safe_failure_update(
    state: OrchestrationState,
    *,
    code: str,
    stage: str,
    exception: Exception,
) -> Dict[str, Any]:
    """Record a bounded operational failure without exposing exception internals."""
    error = create_orchestration_error(
        code=code,
        stage=stage,
        message=f"{stage} execution failed ({type(exception).__name__}).",
        retryable=False,
    )
    return mark_failed(error, state.get("errors"))


def _has_errors(state: OrchestrationState) -> str:
    """Route executable nodes directly to the terminal failure node when needed."""
    return GraphNodeId.FAILURE.value if state.get("errors") else "continue"


def _make_real_shared_planner_node(planner_runner: PlannerRunner) -> Callable[[OrchestrationState], Dict[str, Any]]:
    """Create the first-half graph node that delegates to the real Shared Planner agent."""

    def real_shared_planner_node(state: OrchestrationState) -> Dict[str, Any]:
        try:
            request = SharedPlannerRequest(
                objective=state["objective"],
                workflowId=state.get("workflow_id"),
            )
            planner_result = planner_runner(
                request,
                policy=SharedPlannerPolicy.EndToEndCollectionOperation,
            )
            return {
                **mark_planning_started(),
                **mark_planning_completed(planner_result, state.get("warnings")),
            }
        except Exception as exception:
            return {
                **mark_planning_started(),
                **_safe_failure_update(
                    state,
                    code=PLANNER_VALIDATION_FAILED,
                    stage="SharedPlanner",
                    exception=exception,
                ),
            }

    return real_shared_planner_node


def _make_waste_analysis_node(
    waste_analysis_runner: WasteAnalysisRunner,
) -> Callable[[OrchestrationState], Dict[str, Any]]:
    """Create the C1 node that uses its planner objective and canonical request model."""

    def waste_analysis_node(state: OrchestrationState) -> Dict[str, Any]:
        try:
            planner_result = state.get("planner_result")
            if planner_result is None:
                raise ValueError("Shared Planner result is required before WasteAnalysis.")
            request = WasteAnalysisRequest(
                objective=_planner_step_objective(planner_result, SpecialistType.WasteAnalysis),
                triggerType=state.get("trigger_type", WorkflowTriggerType.ManualOperationalPlanning),
                triggeringWasteReportId=state.get("triggering_waste_report_id"),
                workflowObjective=(
                    state["objective"]
                    if state.get("trigger_type") == WorkflowTriggerType.CitizenReportSubmission
                    else None
                ),
            )
            result = waste_analysis_runner(request)
            if state.get("trigger_type") == WorkflowTriggerType.CitizenReportSubmission:
                analyses = result.analyses
                if len(analyses) != 1 or analyses[0].report_id != state.get("triggering_waste_report_id"):
                    raise ValueError("Report-triggered C1 must cover exactly its authoritative triggering report.")
            return {
                **mark_specialist_started(SpecialistType.WasteAnalysis),
                **mark_specialist_completed(
                    SpecialistType.WasteAnalysis,
                    result,
                    state.get("completed_specialists"),
                ),
            }
        except Exception as exception:
            return {
                **mark_specialist_started(SpecialistType.WasteAnalysis),
                **_safe_failure_update(
                    state,
                    code=WASTE_ANALYSIS_FAILED,
                    stage="WasteAnalysis",
                    exception=exception,
                ),
            }

    return waste_analysis_node


def _make_collection_planning_node(
    collection_planning_runner: CollectionPlanningRunner,
) -> Callable[[OrchestrationState], Dict[str, Any]]:
    """Create the C2 node that independently delegates to the Collection Planning agent."""

    def collection_planning_node(state: OrchestrationState) -> Dict[str, Any]:
        try:
            planner_result = state.get("planner_result")
            if planner_result is None:
                raise ValueError("Shared Planner result is required before CollectionPlanning.")
            request = CollectionPlanningRequest(
                objective=_planner_step_objective(
                    planner_result,
                    SpecialistType.CollectionPlanning,
                ),
            )
            result = collection_planning_runner(request)
            return {
                **mark_specialist_started(SpecialistType.CollectionPlanning),
                **mark_specialist_completed(
                    SpecialistType.CollectionPlanning,
                    result,
                    state.get("completed_specialists"),
                ),
            }
        except Exception as exception:
            return {
                **mark_specialist_started(SpecialistType.CollectionPlanning),
                **_safe_failure_update(
                    state,
                    code=COLLECTION_PLANNING_FAILED,
                    stage="CollectionPlanning",
                    exception=exception,
                ),
            }

    return collection_planning_node


def pause_for_collection_approval_node(state: OrchestrationState) -> Dict[str, Any]:
    """Stop after advisory C2 output; approval and mutations remain ASP.NET responsibilities."""
    return mark_paused_for_approval(
        ApprovalPauseStage.COLLECTION_PLANNING,
        COLLECTION_APPROVAL_PAUSE_REASON,
    )


def pause_for_report_verification_node(state: OrchestrationState) -> Dict[str, Any]:
    """Stop after exact-report C1; the report decision remains authoritative in ASP.NET."""
    return mark_paused_for_approval(
        ApprovalPauseStage.REPORT_VERIFICATION,
        REPORT_VERIFICATION_PAUSE_REASON,
    )


def _route_after_waste_analysis(state: OrchestrationState) -> str:
    """Route only by trusted trigger metadata after C1 succeeds."""
    if state.get("errors"):
        return GraphNodeId.FAILURE.value
    if state.get("trigger_type") == WorkflowTriggerType.CitizenReportSubmission:
        return GraphNodeId.PAUSE_REPORT_VERIFICATION.value
    return GraphNodeId.COLLECTION_PLANNING.value


def build_collection_approval_phase_graph(
    *,
    planner_runner: Optional[PlannerRunner] = None,
    waste_analysis_runner: Optional[WasteAnalysisRunner] = None,
    collection_planning_runner: Optional[CollectionPlanningRunner] = None,
) -> StateGraph:
    """Build the real Step 9C first-half graph through collection human approval.

    The graph makes no direct model or tool calls: it delegates each executable
    stage to the established specialist entry point and has no graph-level retries.
    """
    # Agent modules import orchestration policy enums, so production defaults are
    # deliberately resolved only when this real graph is requested. This keeps the
    # package import graph acyclic while preserving straightforward test injection.
    if planner_runner is None:
        from app.agents.shared_planner_agent import run_shared_planner

        planner_runner = run_shared_planner
    if waste_analysis_runner is None:
        from app.agents.waste_analysis_agent import run_waste_analysis

        waste_analysis_runner = run_waste_analysis
    if collection_planning_runner is None:
        from app.agents.collection_planning_agent import run_collection_planning

        collection_planning_runner = run_collection_planning

    workflow = StateGraph(OrchestrationState)

    workflow.add_node(
        GraphNodeId.SHARED_PLANNER.value,
        _make_real_shared_planner_node(planner_runner),
    )
    workflow.add_node(
        GraphNodeId.WASTE_ANALYSIS.value,
        _make_waste_analysis_node(waste_analysis_runner),
    )
    workflow.add_node(
        GraphNodeId.COLLECTION_PLANNING.value,
        _make_collection_planning_node(collection_planning_runner),
    )
    workflow.add_node(
        GraphNodeId.PAUSE_REPORT_VERIFICATION.value,
        pause_for_report_verification_node,
    )
    workflow.add_node(
        GraphNodeId.PAUSE_COLLECTION_APPROVAL.value,
        pause_for_collection_approval_node,
    )
    workflow.add_node(GraphNodeId.FAILURE.value, failure_node)

    workflow.add_edge(START, GraphNodeId.SHARED_PLANNER.value)
    workflow.add_conditional_edges(
        GraphNodeId.SHARED_PLANNER.value,
        _has_errors,
        {
            "continue": GraphNodeId.WASTE_ANALYSIS.value,
            GraphNodeId.FAILURE.value: GraphNodeId.FAILURE.value,
        },
    )
    workflow.add_conditional_edges(
        GraphNodeId.WASTE_ANALYSIS.value,
        _route_after_waste_analysis,
        {
            GraphNodeId.COLLECTION_PLANNING.value: GraphNodeId.COLLECTION_PLANNING.value,
            GraphNodeId.PAUSE_REPORT_VERIFICATION.value: GraphNodeId.PAUSE_REPORT_VERIFICATION.value,
            GraphNodeId.FAILURE.value: GraphNodeId.FAILURE.value,
        },
    )
    workflow.add_conditional_edges(
        GraphNodeId.COLLECTION_PLANNING.value,
        _has_errors,
        {
            "continue": GraphNodeId.PAUSE_COLLECTION_APPROVAL.value,
            GraphNodeId.FAILURE.value: GraphNodeId.FAILURE.value,
        },
    )
    workflow.add_edge(GraphNodeId.PAUSE_COLLECTION_APPROVAL.value, END)
    workflow.add_edge(GraphNodeId.PAUSE_REPORT_VERIFICATION.value, END)
    workflow.add_edge(GraphNodeId.FAILURE.value, END)

    return workflow


def _envelope_status(phase: OrchestrationPhase) -> OrchestrationStatus:
    """Map internal execution phase to the public envelope status."""
    if phase in {
        OrchestrationPhase.PausedForReportVerification,
        OrchestrationPhase.PausedForCollectionApproval,
        OrchestrationPhase.PausedForDispatchApproval,
    }:
        return OrchestrationStatus.PAUSED
    if phase == OrchestrationPhase.Failed:
        return OrchestrationStatus.FAILED
    if phase == OrchestrationPhase.Completed:
        return OrchestrationStatus.COMPLETED
    return OrchestrationStatus.RUNNING


def run_collection_approval_phase(
    request: SharedPlannerRequest,
    *,
    trigger_type: WorkflowTriggerType = WorkflowTriggerType.ManualOperationalPlanning,
    triggering_waste_report_id: Optional[UUID] = None,
    planner_runner: Optional[PlannerRunner] = None,
    waste_analysis_runner: Optional[WasteAnalysisRunner] = None,
    collection_planning_runner: Optional[CollectionPlanningRunner] = None,
) -> OrchestrationResultEnvelope:
    """Run the real flagship first half and return its structured pause/failure snapshot.

    This deliberately stops before human approval, authoritative task creation,
    C3 FleetRoute, and C4 ValidationOperations.
    """
    initial_state = create_initial_state(
        request.workflow_id,
        request.objective,
        trigger_type=trigger_type,
        triggering_waste_report_id=triggering_waste_report_id,
    )
    graph = build_collection_approval_phase_graph(
        planner_runner=planner_runner,
        waste_analysis_runner=waste_analysis_runner,
        collection_planning_runner=collection_planning_runner,
    ).compile()
    final_state = graph.invoke(initial_state)
    validate_state_invariants(final_state)

    phase = final_state["current_phase"]
    return OrchestrationResultEnvelope(
        workflowId=final_state.get("workflow_id"),
        objective=final_state["objective"],
        triggerType=final_state.get("trigger_type", WorkflowTriggerType.ManualOperationalPlanning),
        triggeringWasteReportId=final_state.get("triggering_waste_report_id"),
        status=_envelope_status(phase),
        currentPhase=phase,
        currentSpecialist=final_state.get("current_specialist"),
        approvalStage=final_state.get("pause_stage", ApprovalPauseStage.NONE),
        pauseReason=final_state.get("pause_reason"),
        plannerResult=final_state.get("planner_result"),
        wasteAnalysisResult=final_state.get("waste_analysis_result"),
        collectionPlanningResult=final_state.get("collection_planning_result"),
        completedSpecialists=final_state.get("completed_specialists", []),
        errors=final_state.get("errors", []),
        warnings=final_state.get("warnings", []),
        finalOutcome=final_state.get("final_outcome"),
    )


def _reconstruct_collection_pause_state(
    paused_result: OrchestrationResultEnvelope,
) -> OrchestrationState:
    """Rebuild the Step 9C snapshot locally; no checkpoint or process memory is used."""
    state = create_initial_state(
        paused_result.workflow_id,
        paused_result.objective or "",
        trigger_type=paused_result.trigger_type,
        triggering_waste_report_id=paused_result.triggering_waste_report_id,
    )
    state.update(
        {
            "planner_result": paused_result.planner_result,
            "current_phase": paused_result.current_phase,
            "current_specialist": paused_result.current_specialist,
            "completed_specialists": list(paused_result.completed_specialists),
            "waste_analysis_result": paused_result.waste_analysis_result,
            "collection_planning_result": paused_result.collection_planning_result,
            "fleet_route_result": paused_result.fleet_route_result,
            "validation_operations_result": paused_result.validation_operations_result,
            "pause_stage": paused_result.approval_stage,
            "pause_reason": paused_result.pause_reason,
            "errors": list(paused_result.errors),
            "warnings": list(paused_result.warnings),
            "final_outcome": paused_result.final_outcome,
        }
    )
    return state


def _validate_report_resume_preconditions(
    state: OrchestrationState,
    resume_context: WorkflowResumeContext,
) -> None:
    """Validate a persisted report pause and ASP.NET's authoritative verification evidence."""
    validate_state_invariants(state)
    report_id = state.get("triggering_waste_report_id")
    if state.get("workflow_id") is None or state["workflow_id"] != resume_context.workflow_id:
        raise ValueError("Resume workflow ID does not match the paused workflow snapshot.")
    if not state.get("objective"):
        raise ValueError("Paused workflow snapshot must include its objective.")
    if state.get("trigger_type") != WorkflowTriggerType.CitizenReportSubmission or report_id is None:
        raise ValueError("Report verification resume requires a linked citizen-report workflow.")
    if state.get("current_phase") != OrchestrationPhase.PausedForReportVerification:
        raise ValueError("Resume requires a PausedForReportVerification workflow snapshot.")
    if state.get("pause_stage") != ApprovalPauseStage.REPORT_VERIFICATION:
        raise ValueError("Resume requires the ReportVerification approval stage.")
    planner_result = state.get("planner_result")
    if planner_result is None:
        raise ValueError("Paused workflow snapshot must include the Shared Planner result.")
    validate_planner_result(planner_result)
    _planner_step_objective(planner_result, SpecialistType.WasteAnalysis)
    _planner_step_objective(planner_result, SpecialistType.CollectionPlanning)
    analysis_result = state.get("waste_analysis_result")
    if analysis_result is None or len(analysis_result.analyses) != 1 or analysis_result.analyses[0].report_id != report_id:
        raise ValueError("Paused C1 result must contain exactly one analysis of the triggering report.")
    if state.get("collection_planning_result") is not None or state.get("fleet_route_result") is not None or state.get("validation_operations_result") is not None:
        raise ValueError("Report verification resume cannot contain downstream specialist results.")
    if state.get("completed_specialists") != [SpecialistType.WasteAnalysis]:
        raise ValueError("Paused workflow snapshot has an invalid completed specialist sequence.")
    if state.get("errors"):
        raise ValueError("Paused workflow snapshot cannot contain orchestration errors.")
    if resume_context.approval_stage != ApprovalPauseStage.REPORT_VERIFICATION:
        raise ValueError("Resume context must confirm the ReportVerification stage.")
    if resume_context.decision != ResumeDecision.APPROVED:
        raise ValueError("Report verification resume requires ASP.NET's Approved decision.")
    evidence = resume_context.authoritative_execution_summary or {}
    if evidence.get("verifiedReportId") != str(report_id) or evidence.get("reportStatus") != "Verified":
        raise ValueError("Resume context must identify the authoritatively verified triggering report.")


def _make_validate_report_resume_node(
    resume_context: WorkflowResumeContext,
) -> Callable[[OrchestrationState], Dict[str, Any]]:
    def validate_report_resume_node(state: OrchestrationState) -> Dict[str, Any]:
        try:
            _validate_report_resume_preconditions(state, resume_context)
            return {"pause_stage": ApprovalPauseStage.NONE, "pause_reason": None}
        except Exception as exception:
            return _safe_failure_update(
                state,
                code=INVALID_RESUME_CONTEXT,
                stage="ReportVerificationResume",
                exception=exception,
            )

    return validate_report_resume_node


def build_report_verification_resume_graph(
    resume_context: WorkflowResumeContext,
    *,
    collection_planning_runner: Optional[CollectionPlanningRunner] = None,
) -> StateGraph:
    """Build stateless C2-only continuation after authoritative report verification."""
    if collection_planning_runner is None:
        from app.agents.collection_planning_agent import run_collection_planning

        collection_planning_runner = run_collection_planning

    workflow = StateGraph(OrchestrationState)
    workflow.add_node("validate_report_resume", _make_validate_report_resume_node(resume_context))
    workflow.add_node(GraphNodeId.COLLECTION_PLANNING.value, _make_collection_planning_node(collection_planning_runner))
    workflow.add_node(GraphNodeId.PAUSE_COLLECTION_APPROVAL.value, pause_for_collection_approval_node)
    workflow.add_node(GraphNodeId.FAILURE.value, failure_node)
    workflow.add_edge(START, "validate_report_resume")
    workflow.add_conditional_edges(
        "validate_report_resume",
        _has_errors,
        {"continue": GraphNodeId.COLLECTION_PLANNING.value, GraphNodeId.FAILURE.value: GraphNodeId.FAILURE.value},
    )
    workflow.add_conditional_edges(
        GraphNodeId.COLLECTION_PLANNING.value,
        _has_errors,
        {"continue": GraphNodeId.PAUSE_COLLECTION_APPROVAL.value, GraphNodeId.FAILURE.value: GraphNodeId.FAILURE.value},
    )
    workflow.add_edge(GraphNodeId.PAUSE_COLLECTION_APPROVAL.value, END)
    workflow.add_edge(GraphNodeId.FAILURE.value, END)
    return workflow


def resume_after_report_verification(
    paused_result: OrchestrationResultEnvelope,
    resume_context: WorkflowResumeContext,
    *,
    collection_planning_runner: Optional[CollectionPlanningRunner] = None,
) -> OrchestrationResultEnvelope:
    """Rebuild the persisted pause, run fresh C2, then stop at collection approval."""
    initial_state = _reconstruct_collection_pause_state(paused_result)
    graph = build_report_verification_resume_graph(
        resume_context,
        collection_planning_runner=collection_planning_runner,
    ).compile()
    final_state = graph.invoke(initial_state)
    validate_state_invariants(final_state)
    phase = final_state["current_phase"]
    return OrchestrationResultEnvelope(
        workflowId=final_state.get("workflow_id"),
        objective=final_state["objective"],
        triggerType=final_state.get("trigger_type", WorkflowTriggerType.ManualOperationalPlanning),
        triggeringWasteReportId=final_state.get("triggering_waste_report_id"),
        status=_envelope_status(phase),
        currentPhase=phase,
        currentSpecialist=final_state.get("current_specialist"),
        approvalStage=final_state.get("pause_stage", ApprovalPauseStage.NONE),
        pauseReason=final_state.get("pause_reason"),
        plannerResult=final_state.get("planner_result"),
        wasteAnalysisResult=final_state.get("waste_analysis_result"),
        collectionPlanningResult=final_state.get("collection_planning_result"),
        completedSpecialists=final_state.get("completed_specialists", []),
        errors=final_state.get("errors", []),
        warnings=final_state.get("warnings", []),
        finalOutcome=final_state.get("final_outcome"),
    )


def _validate_collection_resume_preconditions(
    state: OrchestrationState,
    resume_context: WorkflowResumeContext,
) -> None:
    """Validate the authoritative collection approval boundary before C3 may run."""
    validate_state_invariants(state)
    if state.get("workflow_id") is None or state["workflow_id"] != resume_context.workflow_id:
        raise ValueError("Resume workflow ID does not match the paused workflow snapshot.")
    if not state.get("objective"):
        raise ValueError("Paused workflow snapshot must include its objective.")
    if state.get("current_phase") != OrchestrationPhase.PausedForCollectionApproval:
        raise ValueError("Resume requires a PausedForCollectionApproval workflow snapshot.")
    if state.get("pause_stage") != ApprovalPauseStage.COLLECTION_PLANNING:
        raise ValueError("Resume requires the CollectionPlanning approval stage.")
    if state.get("current_specialist") is not None:
        raise ValueError("Paused workflow snapshot cannot have an active specialist.")
    if state.get("planner_result") is None:
        raise ValueError("Paused workflow snapshot must include the Shared Planner result.")
    if state.get("waste_analysis_result") is None or state.get("collection_planning_result") is None:
        raise ValueError("Paused workflow snapshot must include completed C1 and C2 results.")
    if state.get("fleet_route_result") is not None or state.get("validation_operations_result") is not None:
        raise ValueError("Collection approval resume cannot include existing C3 or C4 results.")
    if state.get("completed_specialists") != [
        SpecialistType.WasteAnalysis,
        SpecialistType.CollectionPlanning,
    ]:
        raise ValueError("Paused workflow snapshot has an invalid completed specialist sequence.")
    if state.get("errors"):
        raise ValueError("Paused workflow snapshot cannot contain orchestration errors.")
    if resume_context.approval_stage != ApprovalPauseStage.COLLECTION_PLANNING:
        raise ValueError("Resume context must approve the CollectionPlanning stage.")
    if resume_context.decision.value != "Approved":
        raise ValueError("Resume context decision must be Approved for dispatch planning.")
    if not resume_context.authoritative_execution_summary:
        raise ValueError("Approved collection resume requires a non-empty authoritative execution summary.")


def _make_validate_collection_resume_node(
    resume_context: WorkflowResumeContext,
) -> Callable[[OrchestrationState], Dict[str, Any]]:
    """Create the graph entry node that gates C3 on authoritative ASP.NET evidence."""

    def validate_collection_resume_node(state: OrchestrationState) -> Dict[str, Any]:
        try:
            _validate_collection_resume_preconditions(state, resume_context)
            return {
                "pause_stage": ApprovalPauseStage.NONE,
                "pause_reason": None,
            }
        except Exception as exception:
            return _safe_failure_update(
                state,
                code=INVALID_RESUME_CONTEXT,
                stage="CollectionResume",
                exception=exception,
            )

    return validate_collection_resume_node


def _make_fleet_route_node(
    fleet_route_runner: FleetRouteRunner,
) -> Callable[[OrchestrationState], Dict[str, Any]]:
    """Create C3 delegation node using the existing FleetRoute planner step objective."""

    def fleet_route_node(state: OrchestrationState) -> Dict[str, Any]:
        try:
            planner_result = state.get("planner_result")
            if planner_result is None:
                raise ValueError("Shared Planner result is required before FleetRoute.")
            result = fleet_route_runner(
                FleetRouteRequest(
                    objective=_planner_step_objective(planner_result, SpecialistType.FleetRoute),
                )
            )
            return {
                **mark_specialist_started(SpecialistType.FleetRoute),
                **mark_specialist_completed(
                    SpecialistType.FleetRoute,
                    result,
                    state.get("completed_specialists"),
                ),
            }
        except Exception as exception:
            return {
                **mark_specialist_started(SpecialistType.FleetRoute),
                **_safe_failure_update(
                    state,
                    code=FLEET_PLANNING_FAILED,
                    stage="FleetRoute",
                    exception=exception,
                ),
            }

    return fleet_route_node


def _make_validation_operations_node(
    validation_operations_runner: ValidationOperationsRunner,
) -> Callable[[OrchestrationState], Dict[str, Any]]:
    """Create C4 delegation node with the canonical direct typed C3 handoff."""

    def validation_operations_node(state: OrchestrationState) -> Dict[str, Any]:
        try:
            planner_result = state.get("planner_result")
            fleet_result = state.get("fleet_route_result")
            if planner_result is None or fleet_result is None:
                raise ValueError("Shared Planner and FleetRoute results are required before ValidationOperations.")
            result = validation_operations_runner(
                ValidationOperationsRequest(
                    objective=_planner_step_objective(
                        planner_result,
                        SpecialistType.ValidationOperations,
                    ),
                    dispatchPlans=fleet_result.dispatch_plans,
                    unplannedTasks=fleet_result.unplanned_tasks,
                )
            )
            if result.validation_outcome not in {"ReadyForHumanReview", "NeedsRevision"}:
                raise ValueError("ValidationOperations result must provide a dispatch validation outcome.")
            return {
                **mark_specialist_started(SpecialistType.ValidationOperations),
                **mark_specialist_completed(
                    SpecialistType.ValidationOperations,
                    result,
                    state.get("completed_specialists"),
                ),
            }
        except Exception as exception:
            return {
                **mark_specialist_started(SpecialistType.ValidationOperations),
                **_safe_failure_update(
                    state,
                    code=OPERATIONAL_VALIDATION_FAILED,
                    stage="ValidationOperations",
                    exception=exception,
                ),
            }

    return validation_operations_node


def pause_for_dispatch_approval_node(state: OrchestrationState) -> Dict[str, Any]:
    """Pause the advisory dispatch proposal for future authoritative human approval."""
    return mark_paused_for_approval(
        ApprovalPauseStage.FLEET_DISPATCH,
        DISPATCH_APPROVAL_PAUSE_REASON,
    )


def dispatch_needs_revision_node(state: OrchestrationState) -> Dict[str, Any]:
    """Return the valid non-failure outcome when C4 identifies operational revisions."""
    return mark_dispatch_needs_revision()


def _route_after_validation_operations(state: OrchestrationState) -> str:
    """Route C4's validated outcome without treating NeedsRevision as a system failure."""
    if state.get("errors"):
        return GraphNodeId.FAILURE.value
    result = state.get("validation_operations_result")
    if result is not None and result.validation_outcome == "ReadyForHumanReview":
        return GraphNodeId.PAUSE_DISPATCH_APPROVAL.value
    if result is not None and result.validation_outcome == "NeedsRevision":
        return GraphNodeId.DISPATCH_NEEDS_REVISION.value
    return GraphNodeId.FAILURE.value


def build_dispatch_planning_phase_graph(
    resume_context: WorkflowResumeContext,
    *,
    fleet_route_runner: Optional[FleetRouteRunner] = None,
    validation_operations_runner: Optional[ValidationOperationsRunner] = None,
) -> StateGraph:
    """Build the real stateless Step 9D resume graph from collection approval to C4."""
    if fleet_route_runner is None:
        from app.agents.fleet_route_agent import run_fleet_route

        fleet_route_runner = run_fleet_route
    if validation_operations_runner is None:
        from app.agents.validation_operations_agent import run_validation_operations

        validation_operations_runner = run_validation_operations

    workflow = StateGraph(OrchestrationState)
    workflow.add_node(
        "validate_collection_resume",
        _make_validate_collection_resume_node(resume_context),
    )
    workflow.add_node(GraphNodeId.FLEET_ROUTE.value, _make_fleet_route_node(fleet_route_runner))
    workflow.add_node(
        GraphNodeId.VALIDATION_OPERATIONS.value,
        _make_validation_operations_node(validation_operations_runner),
    )
    workflow.add_node(GraphNodeId.PAUSE_DISPATCH_APPROVAL.value, pause_for_dispatch_approval_node)
    workflow.add_node(GraphNodeId.DISPATCH_NEEDS_REVISION.value, dispatch_needs_revision_node)
    workflow.add_node(GraphNodeId.FAILURE.value, failure_node)

    workflow.add_edge(START, "validate_collection_resume")
    workflow.add_conditional_edges(
        "validate_collection_resume",
        _has_errors,
        {"continue": GraphNodeId.FLEET_ROUTE.value, GraphNodeId.FAILURE.value: GraphNodeId.FAILURE.value},
    )
    workflow.add_conditional_edges(
        GraphNodeId.FLEET_ROUTE.value,
        _has_errors,
        {"continue": GraphNodeId.VALIDATION_OPERATIONS.value, GraphNodeId.FAILURE.value: GraphNodeId.FAILURE.value},
    )
    workflow.add_conditional_edges(
        GraphNodeId.VALIDATION_OPERATIONS.value,
        _route_after_validation_operations,
        {
            GraphNodeId.PAUSE_DISPATCH_APPROVAL.value: GraphNodeId.PAUSE_DISPATCH_APPROVAL.value,
            GraphNodeId.DISPATCH_NEEDS_REVISION.value: GraphNodeId.DISPATCH_NEEDS_REVISION.value,
            GraphNodeId.FAILURE.value: GraphNodeId.FAILURE.value,
        },
    )
    workflow.add_edge(GraphNodeId.PAUSE_DISPATCH_APPROVAL.value, END)
    workflow.add_edge(GraphNodeId.DISPATCH_NEEDS_REVISION.value, END)
    workflow.add_edge(GraphNodeId.FAILURE.value, END)
    return workflow


def resume_after_collection_approval(
    paused_result: OrchestrationResultEnvelope,
    resume_context: WorkflowResumeContext,
    *,
    fleet_route_runner: Optional[FleetRouteRunner] = None,
    validation_operations_runner: Optional[ValidationOperationsRunner] = None,
) -> OrchestrationResultEnvelope:
    """Statelessly continue one approved collection workflow through C3 and C4 only."""
    initial_state = _reconstruct_collection_pause_state(paused_result)
    graph = build_dispatch_planning_phase_graph(
        resume_context,
        fleet_route_runner=fleet_route_runner,
        validation_operations_runner=validation_operations_runner,
    ).compile()
    final_state = graph.invoke(initial_state)
    validate_state_invariants(final_state)

    phase = final_state["current_phase"]
    return OrchestrationResultEnvelope(
        workflowId=final_state.get("workflow_id"),
        objective=final_state["objective"],
        triggerType=final_state.get("trigger_type", WorkflowTriggerType.ManualOperationalPlanning),
        triggeringWasteReportId=final_state.get("triggering_waste_report_id"),
        status=_envelope_status(phase),
        currentPhase=phase,
        currentSpecialist=final_state.get("current_specialist"),
        approvalStage=final_state.get("pause_stage", ApprovalPauseStage.NONE),
        pauseReason=final_state.get("pause_reason"),
        plannerResult=final_state.get("planner_result"),
        wasteAnalysisResult=final_state.get("waste_analysis_result"),
        collectionPlanningResult=final_state.get("collection_planning_result"),
        fleetRouteResult=final_state.get("fleet_route_result"),
        validationOperationsResult=final_state.get("validation_operations_result"),
        completedSpecialists=final_state.get("completed_specialists", []),
        errors=final_state.get("errors", []),
        warnings=final_state.get("warnings", []),
        finalOutcome=final_state.get("final_outcome"),
    )
