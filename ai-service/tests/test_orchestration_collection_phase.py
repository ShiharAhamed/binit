import json
from uuid import UUID, uuid4

from app.agents.collection_planning_agent import CollectionPlanningModelError
from app.agents.shared_planner_agent import SharedPlannerModelError
from app.agents.waste_analysis_agent import WasteAnalysisModelError
from app.models.analysis import WasteAnalysisResult
from app.models.collection_planning import CollectionPlanningResult
from app.models.shared_planner import (
    SharedPlannerRequest,
    SpecialistType,
    create_flagship_planner_result,
)
from app.orchestration.enums import (
    ApprovalPauseStage,
    GraphNodeId,
    OrchestrationPhase,
    OrchestrationStatus,
    SharedPlannerPolicy,
)
from app.orchestration.errors import (
    COLLECTION_PLANNING_FAILED,
    PLANNER_VALIDATION_FAILED,
    WASTE_ANALYSIS_FAILED,
)
from app.orchestration.graph import (
    build_collection_approval_phase_graph,
    run_collection_approval_phase,
)
from app.orchestration.state import FORBIDDEN_REASONING_KEYS, validate_state_invariants
from app.orchestration.transitions import create_initial_state


def _planner_result(objective: str):
    plan = create_flagship_planner_result(objective=objective)
    objectives = {
        SpecialistType.WasteAnalysis: "Analyze current verified waste conditions.",
        SpecialistType.CollectionPlanning: "Prepare collection grouping and schedules.",
    }
    return plan.model_copy(
        update={
            "steps": [
                step.model_copy(update={"objective": objectives.get(step.specialist, step.objective)})
                for step in plan.steps
            ]
        }
    )


def _c1_result(objective: str) -> WasteAnalysisResult:
    return WasteAnalysisResult(
        objective=objective,
        analyses=[],
        sourcePage=1,
        sourcePageSize=20,
        sourceTotalCount=0,
        agentName="waste_analysis_agent",
        modelName="fake-c1",
        status="empty",
    )


def _c2_result(objective: str) -> CollectionPlanningResult:
    return CollectionPlanningResult(
        objective=objective,
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
        agentName="collection_planning_agent",
        modelName="fake-c2",
        advisoryOnly=True,
        status="empty",
    )


def _successful_runners(order: list[str], requests: dict[str, object]):
    def planner(request, *, policy):
        order.append("SharedPlanner")
        requests["planner"] = request
        requests["policy"] = policy
        return _planner_result(request.objective)

    def waste_analysis(request):
        order.append("WasteAnalysis")
        requests["c1"] = request
        return _c1_result(request.objective)

    def collection_planning(request):
        order.append("CollectionPlanning")
        requests["c2"] = request
        return _c2_result(request.objective)

    return planner, waste_analysis, collection_planning


def _initial_state(workflow_id: UUID):
    return create_initial_state(
        workflow_id=workflow_id,
        objective="Coordinate the current city waste collection operation.",
    )


class TestCollectionApprovalPhase:
    """Step 9C tests for the real injected LangGraph first-half workflow."""

    def test_successful_flagship_first_half_pauses_with_shared_canonical_results(self):
        workflow_id = uuid4()
        order: list[str] = []
        requests: dict[str, object] = {}
        planner, waste_analysis, collection_planning = _successful_runners(order, requests)

        graph = build_collection_approval_phase_graph(
            planner_runner=planner,
            waste_analysis_runner=waste_analysis,
            collection_planning_runner=collection_planning,
        ).compile()
        final_state = graph.invoke(_initial_state(workflow_id))

        assert final_state["workflow_id"] == workflow_id
        assert final_state["current_phase"] == OrchestrationPhase.PausedForCollectionApproval
        assert final_state["pause_stage"] == ApprovalPauseStage.COLLECTION_PLANNING
        assert final_state["current_specialist"] is None
        assert final_state["completed_specialists"] == [
            SpecialistType.WasteAnalysis,
            SpecialistType.CollectionPlanning,
        ]
        assert final_state["planner_result"] is not None
        assert final_state["waste_analysis_result"] is not None
        assert final_state["collection_planning_result"] is not None
        assert final_state["fleet_route_result"] is None
        assert final_state["validation_operations_result"] is None
        assert final_state["errors"] == []
        assert order == ["SharedPlanner", "WasteAnalysis", "CollectionPlanning"]
        assert requests["policy"] == SharedPlannerPolicy.EndToEndCollectionOperation
        assert requests["planner"].workflow_id == workflow_id
        assert requests["c1"].objective == "Analyze current verified waste conditions."
        assert requests["c2"].objective == "Prepare collection grouping and schedules."
        validate_state_invariants(final_state)

    def test_public_runner_returns_serializable_paused_envelope_without_hidden_reasoning(self):
        workflow_id = uuid4()
        order: list[str] = []
        requests: dict[str, object] = {}
        planner, waste_analysis, collection_planning = _successful_runners(order, requests)

        result = run_collection_approval_phase(
            SharedPlannerRequest(
                workflowId=workflow_id,
                objective="Coordinate the current city waste collection operation.",
            ),
            planner_runner=planner,
            waste_analysis_runner=waste_analysis,
            collection_planning_runner=collection_planning,
        )

        assert result.workflow_id == workflow_id
        assert result.status == OrchestrationStatus.PAUSED
        assert result.current_phase == OrchestrationPhase.PausedForCollectionApproval
        assert result.approval_stage == ApprovalPauseStage.COLLECTION_PLANNING
        assert result.pause_reason
        assert result.planner_result is not None
        assert result.waste_analysis_result is not None
        assert result.collection_planning_result is not None
        assert result.completed_specialists == [
            SpecialistType.WasteAnalysis,
            SpecialistType.CollectionPlanning,
        ]
        assert result.final_outcome is None

        serialized = json.loads(result.model_dump_json(by_alias=True))
        assert serialized["workflowId"] == str(workflow_id)
        assert serialized["plannerResult"] is not None
        assert serialized["wasteAnalysisResult"] is not None
        assert serialized["collectionPlanningResult"] is not None

        def collect_keys(value):
            if isinstance(value, dict):
                return set(value) | set().union(*(collect_keys(child) for child in value.values()))
            if isinstance(value, list):
                return set().union(*(collect_keys(child) for child in value)) if value else set()
            return set()

        keys = collect_keys(serialized)
        assert not keys.intersection(FORBIDDEN_REASONING_KEYS)
        assert not keys.intersection({"reasoning", "scratchpad", "thoughts"})

    def test_planner_failure_stops_before_c1_and_c2(self):
        calls = {"planner": 0, "c1": 0, "c2": 0}

        def planner(_request, *, policy):
            calls["planner"] += 1
            assert policy == SharedPlannerPolicy.EndToEndCollectionOperation
            raise SharedPlannerModelError("fake planner failure")

        def waste_analysis(_request):
            calls["c1"] += 1
            raise AssertionError("C1 must not run after planner failure")

        def collection_planning(_request):
            calls["c2"] += 1
            raise AssertionError("C2 must not run after planner failure")

        result = run_collection_approval_phase(
            SharedPlannerRequest(objective="Coordinate the current city waste collection operation."),
            planner_runner=planner,
            waste_analysis_runner=waste_analysis,
            collection_planning_runner=collection_planning,
        )

        assert calls == {"planner": 1, "c1": 0, "c2": 0}
        assert result.status == OrchestrationStatus.FAILED
        assert result.current_phase == OrchestrationPhase.Failed
        assert result.approval_stage == ApprovalPauseStage.NONE
        assert result.planner_result is None
        assert result.errors[0].code == PLANNER_VALIDATION_FAILED

    def test_c1_failure_preserves_plan_stops_c2_and_does_not_retry(self):
        calls = {"c1": 0, "c2": 0}

        def planner(request, *, policy):
            assert policy == SharedPlannerPolicy.EndToEndCollectionOperation
            return _planner_result(request.objective)

        def waste_analysis(_request):
            calls["c1"] += 1
            raise WasteAnalysisModelError("fake C1 failure")

        def collection_planning(_request):
            calls["c2"] += 1
            raise AssertionError("C2 must not run after C1 failure")

        result = run_collection_approval_phase(
            SharedPlannerRequest(objective="Coordinate the current city waste collection operation."),
            planner_runner=planner,
            waste_analysis_runner=waste_analysis,
            collection_planning_runner=collection_planning,
        )

        assert calls == {"c1": 1, "c2": 0}
        assert result.current_phase == OrchestrationPhase.Failed
        assert result.approval_stage == ApprovalPauseStage.NONE
        assert result.planner_result is not None
        assert result.waste_analysis_result is None
        assert result.collection_planning_result is None
        assert result.errors[0].code == WASTE_ANALYSIS_FAILED

    def test_c2_failure_preserves_c1_stops_pause_and_does_not_retry(self):
        calls = {"c2": 0}

        def planner(request, *, policy):
            assert policy == SharedPlannerPolicy.EndToEndCollectionOperation
            return _planner_result(request.objective)

        def waste_analysis(request):
            return _c1_result(request.objective)

        def collection_planning(_request):
            calls["c2"] += 1
            raise CollectionPlanningModelError("fake C2 failure")

        result = run_collection_approval_phase(
            SharedPlannerRequest(objective="Coordinate the current city waste collection operation."),
            planner_runner=planner,
            waste_analysis_runner=waste_analysis,
            collection_planning_runner=collection_planning,
        )

        assert calls == {"c2": 1}
        assert result.status == OrchestrationStatus.FAILED
        assert result.current_phase == OrchestrationPhase.Failed
        assert result.approval_stage == ApprovalPauseStage.NONE
        assert result.planner_result is not None
        assert result.waste_analysis_result is not None
        assert result.collection_planning_result is None
        assert result.errors[0].code == COLLECTION_PLANNING_FAILED

    def test_graph_compiles_only_the_step_9c_nodes(self):
        graph = build_collection_approval_phase_graph().compile()
        node_names = set(graph.get_graph().nodes)

        assert GraphNodeId.SHARED_PLANNER.value in node_names
        assert GraphNodeId.WASTE_ANALYSIS.value in node_names
        assert GraphNodeId.COLLECTION_PLANNING.value in node_names
        assert GraphNodeId.PAUSE_COLLECTION_APPROVAL.value in node_names
        assert GraphNodeId.FLEET_ROUTE.value not in node_names
        assert GraphNodeId.VALIDATION_OPERATIONS.value not in node_names
