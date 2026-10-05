import json
from uuid import uuid4

import pytest

from app.agents.fleet_route_agent import FleetRouteModelError
from app.agents.validation_operations_agent import ValidationOperationsModelError
from app.models.analysis import WasteAnalysisResult
from app.models.collection_planning import CollectionPlanningResult
from app.models.fleet_resources import FleetCompatibilityStatus
from app.models.fleet_route import (
    DispatchPlanRecommendation,
    DriverRecommendation,
    FleetRouteResult,
    RecommendedFleetTask,
    RecommendationCompatibility,
    UnplannedTask,
    VehicleRecommendation,
)
from app.models.shared_planner import SpecialistType, create_flagship_planner_result
from app.models.validation_operations import ValidationOperationsResult
from app.orchestration.enums import (
    ApprovalPauseStage,
    GraphNodeId,
    OrchestrationPhase,
    OrchestrationStatus,
    ResumeDecision,
)
from app.orchestration.errors import (
    FLEET_PLANNING_FAILED,
    INVALID_RESUME_CONTEXT,
    OPERATIONAL_VALIDATION_FAILED,
)
from app.orchestration.graph import (
    build_dispatch_planning_phase_graph,
    resume_after_collection_approval,
)
from app.orchestration.state import (
    FORBIDDEN_REASONING_KEYS,
    OrchestrationResultEnvelope,
    WorkflowResumeContext,
    validate_state_invariants,
)


def _c1_result() -> WasteAnalysisResult:
    return WasteAnalysisResult(
        objective="Analyze current verified waste conditions.",
        analyses=[],
        sourcePage=1,
        sourcePageSize=20,
        sourceTotalCount=0,
        status="empty",
    )


def _c2_result() -> CollectionPlanningResult:
    return CollectionPlanningResult(
        objective="Prepare collection grouping and schedules.",
        candidateGroups=[],
        separateHandling=[],
        deferredNeeds=[],
        warnings=[],
        sourcePage=1,
        sourcePageSize=20,
        sourceTotalCount=0,
        sourceTotalPages=0,
        retrievedPages=[1],
        isCompleteSnapshot=True,
        status="empty",
    )


def _planner_result():
    plan = create_flagship_planner_result("Coordinate the approved collection workflow.")
    objectives = {
        SpecialistType.FleetRoute: "Recommend fleet dispatch plans for the newly created Scheduled tasks.",
        SpecialistType.ValidationOperations: "Validate fleet dispatch proposals against fresh operational state.",
    }
    return plan.model_copy(
        update={
            "steps": [
                step.model_copy(update={"objective": objectives.get(step.specialist, step.objective)})
                for step in plan.steps
            ]
        }
    )


def _fleet_result() -> FleetRouteResult:
    task_id, driver_id, vehicle_id = uuid4(), uuid4(), uuid4()
    plan = DispatchPlanRecommendation(
        planId="plan-1",
        recommendedDriver=DriverRecommendation(
            driverId=driver_id,
            displayName="Asha Driver",
            reason="Available for the scheduled collection.",
        ),
        recommendedVehicle=VehicleRecommendation(
            vehicleId=vehicle_id,
            registrationNumber="WP-CAB-1234",
            vehicleType="CompactorTruck",
            reason="Compatible operational vehicle.",
        ),
        recommendedTasks=[
            RecommendedFleetTask(
                taskId=task_id,
                taskCode="TSK-001",
                sequence=1,
                addressText="Pettah Market, Colombo",
                reason="First scheduled stop.",
            )
        ],
        compatibility=RecommendationCompatibility(
            status=FleetCompatibilityStatus.COMPATIBLE,
            requiresAcknowledgement=False,
            issues=[],
        ),
        rationale="Advisory dispatch plan for the approved collection task.",
        warnings=[],
    )
    return FleetRouteResult(
        objective="Recommend fleet dispatch plans for the newly created Scheduled tasks.",
        dispatchPlans=[plan],
        unplannedTasks=[
            UnplannedTask(
                taskId=uuid4(),
                taskCode="TSK-002",
                reason="Deferred because capacity is reserved for the first route.",
            )
        ],
        warnings=[],
        rationale="A dispatch plan and one advisory unplanned task were identified.",
        sourceTaskPage=1,
        sourceTaskPageSize=20,
        sourceTaskTotalCount=2,
        sourceTaskTotalPages=1,
        status="completed",
    )


def _validation_result(outcome: str) -> ValidationOperationsResult:
    return ValidationOperationsResult(
        objective="Validate fleet dispatch proposals against fresh operational state.",
        validationOutcome=outcome,
        planReviews=[],
        unplannedTaskFindings=[],
        requiresAcknowledgement=False,
        warnings=[],
        summary="Fresh operational validation has completed for the advisory dispatch proposal.",
        status="completed",
    )


def _paused_result(workflow_id=None) -> OrchestrationResultEnvelope:
    return OrchestrationResultEnvelope(
        workflowId=workflow_id or uuid4(),
        objective="Coordinate the approved collection workflow.",
        status=OrchestrationStatus.PAUSED,
        currentPhase=OrchestrationPhase.PausedForCollectionApproval,
        currentSpecialist=None,
        approvalStage=ApprovalPauseStage.COLLECTION_PLANNING,
        pauseReason="Collection plan is awaiting authorized review.",
        plannerResult=_planner_result(),
        wasteAnalysisResult=_c1_result(),
        collectionPlanningResult=_c2_result(),
        completedSpecialists=[
            SpecialistType.WasteAnalysis,
            SpecialistType.CollectionPlanning,
        ],
        errors=[],
        warnings=[],
        finalOutcome=None,
    )


def _resume_context(workflow_id, **updates) -> WorkflowResumeContext:
    values = {
        "workflowId": workflow_id,
        "approvalStage": ApprovalPauseStage.COLLECTION_PLANNING,
        "decision": ResumeDecision.APPROVED,
        "authoritativeExecutionSummary": {"createdTaskCount": 2},
    }
    values.update(updates)
    return WorkflowResumeContext(**values)


def _successful_runners(calls, requests, outcome="ReadyForHumanReview"):
    fleet_result = _fleet_result()

    def fleet_route(request):
        calls.append("FleetRoute")
        requests["c3"] = request
        return fleet_result

    def validation_operations(request):
        calls.append("ValidationOperations")
        requests["c4"] = request
        return _validation_result(outcome)

    return fleet_route, validation_operations, fleet_result


class TestDispatchPlanningPhase:
    """Step 9D deterministic tests for stateless C3/C4 resume orchestration."""

    def test_approved_resume_runs_c3_then_c4_and_pauses_for_dispatch_approval(self):
        paused = _paused_result()
        calls, requests = [], {}
        fleet_route, validation_operations, fleet_result = _successful_runners(calls, requests)

        result = resume_after_collection_approval(
            paused,
            _resume_context(paused.workflow_id),
            fleet_route_runner=fleet_route,
            validation_operations_runner=validation_operations,
        )

        assert calls == ["FleetRoute", "ValidationOperations"]
        assert requests["c3"].objective == "Recommend fleet dispatch plans for the newly created Scheduled tasks."
        assert requests["c4"].objective == "Validate fleet dispatch proposals against fresh operational state."
        assert requests["c4"].dispatch_plans == fleet_result.dispatch_plans
        assert requests["c4"].unplanned_tasks == fleet_result.unplanned_tasks
        assert result.workflow_id == paused.workflow_id
        assert result.current_phase == OrchestrationPhase.PausedForDispatchApproval
        assert result.status == OrchestrationStatus.PAUSED
        assert result.approval_stage == ApprovalPauseStage.FLEET_DISPATCH
        assert result.current_specialist is None
        assert result.fleet_route_result is not None
        assert result.validation_operations_result is not None
        assert result.errors == []
        assert result.completed_specialists == [
            SpecialistType.WasteAnalysis,
            SpecialistType.CollectionPlanning,
            SpecialistType.FleetRoute,
            SpecialistType.ValidationOperations,
        ]
        assert result.current_phase != OrchestrationPhase.Completed

    def test_needs_revision_is_a_serializable_non_failure_outcome(self):
        paused = _paused_result()
        calls, requests = [], {}
        fleet_route, validation_operations, _ = _successful_runners(calls, requests, "NeedsRevision")

        result = resume_after_collection_approval(
            paused,
            _resume_context(paused.workflow_id),
            fleet_route_runner=fleet_route,
            validation_operations_runner=validation_operations,
        )

        assert calls == ["FleetRoute", "ValidationOperations"]
        assert result.current_phase == OrchestrationPhase.DispatchNeedsRevision
        assert result.status == OrchestrationStatus.RUNNING
        assert result.approval_stage == ApprovalPauseStage.NONE
        assert result.errors == []
        assert result.final_outcome == "DispatchNeedsRevision"
        assert result.current_phase != OrchestrationPhase.Failed
        assert result.current_phase != OrchestrationPhase.PausedForDispatchApproval

        serialized = json.loads(result.model_dump_json(by_alias=True))
        assert serialized["fleetRouteResult"] is not None
        assert serialized["validationOperationsResult"]["validationOutcome"] == "NeedsRevision"

        def collect_keys(value):
            if isinstance(value, dict):
                return set(value) | set().union(*(collect_keys(child) for child in value.values()))
            if isinstance(value, list):
                return set().union(*(collect_keys(child) for child in value)) if value else set()
            return set()

        keys = collect_keys(serialized)
        assert not keys.intersection(FORBIDDEN_REASONING_KEYS)
        assert not keys.intersection({"reasoning", "scratchpad", "privateReasoning"})

    @pytest.mark.parametrize(
        ("context_updates", "snapshot_updates"),
        [
            ({"approvalStage": ApprovalPauseStage.FLEET_DISPATCH}, {}),
            ({"decision": ResumeDecision.REVISION_REQUESTED}, {}),
            ({"decision": ResumeDecision.REJECTED}, {}),
            ({"authoritativeExecutionSummary": None}, {}),
            ({"authoritativeExecutionSummary": {}}, {}),
            ({}, {"current_phase": OrchestrationPhase.Planning}),
            ({}, {"waste_analysis_result": None}),
            ({}, {"collection_planning_result": None}),
        ],
    )
    def test_invalid_resume_preconditions_stop_before_c3_and_c4(self, context_updates, snapshot_updates):
        paused = _paused_result()
        if snapshot_updates:
            paused = paused.model_copy(update=snapshot_updates)
        context = _resume_context(paused.workflow_id, **context_updates)
        calls = []

        def fleet_route(_request):
            calls.append("FleetRoute")
            raise AssertionError("C3 must not run for an invalid resume")

        def validation_operations(_request):
            calls.append("ValidationOperations")
            raise AssertionError("C4 must not run for an invalid resume")

        result = resume_after_collection_approval(
            paused,
            context,
            fleet_route_runner=fleet_route,
            validation_operations_runner=validation_operations,
        )

        assert calls == []
        assert result.current_phase == OrchestrationPhase.Failed
        assert result.approval_stage == ApprovalPauseStage.NONE
        assert result.errors[0].code == INVALID_RESUME_CONTEXT

    def test_workflow_id_mismatch_stops_before_c3_and_c4(self):
        paused = _paused_result()
        result = resume_after_collection_approval(
            paused,
            _resume_context(uuid4()),
            fleet_route_runner=lambda _request: pytest.fail("C3 must not run"),
            validation_operations_runner=lambda _request: pytest.fail("C4 must not run"),
        )

        assert result.current_phase == OrchestrationPhase.Failed
        assert result.errors[0].code == INVALID_RESUME_CONTEXT

    def test_c3_failure_does_not_run_c4_or_retry(self):
        paused = _paused_result()
        calls = {"c3": 0, "c4": 0}

        def fleet_route(_request):
            calls["c3"] += 1
            raise FleetRouteModelError("fake C3 failure")

        def validation_operations(_request):
            calls["c4"] += 1
            raise AssertionError("C4 must not run after C3 failure")

        result = resume_after_collection_approval(
            paused,
            _resume_context(paused.workflow_id),
            fleet_route_runner=fleet_route,
            validation_operations_runner=validation_operations,
        )

        assert calls == {"c3": 1, "c4": 0}
        assert result.current_phase == OrchestrationPhase.Failed
        assert result.approval_stage == ApprovalPauseStage.NONE
        assert result.fleet_route_result is None
        assert result.validation_operations_result is None
        assert result.errors[0].code == FLEET_PLANNING_FAILED

    def test_c4_failure_preserves_c3_without_retrying_it(self):
        paused = _paused_result()
        fleet_result = _fleet_result()
        calls = {"c3": 0, "c4": 0}

        def fleet_route(_request):
            calls["c3"] += 1
            return fleet_result

        def validation_operations(_request):
            calls["c4"] += 1
            raise ValidationOperationsModelError("fake C4 failure")

        result = resume_after_collection_approval(
            paused,
            _resume_context(paused.workflow_id),
            fleet_route_runner=fleet_route,
            validation_operations_runner=validation_operations,
        )

        assert calls == {"c3": 1, "c4": 1}
        assert result.current_phase == OrchestrationPhase.Failed
        assert result.approval_stage == ApprovalPauseStage.NONE
        assert result.fleet_route_result == fleet_result
        assert result.validation_operations_result is None
        assert result.errors[0].code == OPERATIONAL_VALIDATION_FAILED

    def test_dispatch_graph_has_only_resume_c3_c4_and_terminal_nodes(self):
        paused = _paused_result()
        graph = build_dispatch_planning_phase_graph(_resume_context(paused.workflow_id)).compile()
        node_names = set(graph.get_graph().nodes)

        assert "validate_collection_resume" in node_names
        assert GraphNodeId.FLEET_ROUTE.value in node_names
        assert GraphNodeId.VALIDATION_OPERATIONS.value in node_names
        assert GraphNodeId.PAUSE_DISPATCH_APPROVAL.value in node_names
        assert GraphNodeId.DISPATCH_NEEDS_REVISION.value in node_names
        assert GraphNodeId.SHARED_PLANNER.value not in node_names
        assert GraphNodeId.WASTE_ANALYSIS.value not in node_names
        assert GraphNodeId.COLLECTION_PLANNING.value not in node_names

    def test_new_dispatch_outcome_state_invariants(self):
        paused = _paused_result()
        calls, requests = [], {}
        fleet_route, validation_operations, _ = _successful_runners(calls, requests)
        result = resume_after_collection_approval(
            paused,
            _resume_context(paused.workflow_id),
            fleet_route_runner=fleet_route,
            validation_operations_runner=validation_operations,
        )
        state = {
            "workflow_id": result.workflow_id,
            "objective": result.objective,
            "planner_result": result.planner_result,
            "current_phase": result.current_phase,
            "current_specialist": result.current_specialist,
            "completed_specialists": result.completed_specialists,
            "waste_analysis_result": result.waste_analysis_result,
            "collection_planning_result": result.collection_planning_result,
            "fleet_route_result": result.fleet_route_result,
            "validation_operations_result": result.validation_operations_result,
            "pause_stage": result.approval_stage,
            "pause_reason": result.pause_reason,
            "errors": result.errors,
            "warnings": result.warnings,
            "final_outcome": result.final_outcome,
        }
        validate_state_invariants(state)
