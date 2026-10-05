import json
from datetime import datetime, timezone
from unittest.mock import MagicMock, patch
from uuid import uuid4

from langchain_core.messages import AIMessage

from app.agents.collection_planning_agent import run_collection_planning
from app.models.shared_planner import SharedPlannerRequest
from app.orchestration.enums import OrchestrationPhase
from app.orchestration.graph import resume_after_report_verification, run_collection_approval_phase
from tests.test_collection_planning_agent import _group_payload, _need, _snapshot
from tests.test_orchestration_collection_phase import _c1_result, _planner_result
from tests.test_report_trigger_orchestration import _context, _paused_report


NOW = datetime(2026, 10, 5, 16, 30, tzinfo=timezone.utc)  # 22:00 Colombo
VALID = "2026-10-06T02:30:00Z"  # 08:00 Colombo


def _real_c2(request):
    return run_collection_planning(request, model=MagicMock(), planning_reference_at=NOW)


@patch("app.agents.collection_planning_agent.invoke_chat_model")
@patch("app.agents.collection_planning_agent.fetch_all_collection_needs")
def test_manual_graph_uses_validated_c2_schedule(fetch, invoke):
    needs = [_need(), _need()]
    fetch.return_value = _snapshot(needs)
    invoke.return_value = AIMessage(content=json.dumps(_group_payload(needs, VALID)))
    calls = []

    def planner(request, *, policy):
        calls.append("planner")
        return _planner_result(request.objective)

    def c1(request):
        calls.append("c1")
        return _c1_result(request.objective)

    result = run_collection_approval_phase(
        SharedPlannerRequest(workflowId=uuid4(), objective="Coordinate municipal waste collections."),
        planner_runner=planner, waste_analysis_runner=c1, collection_planning_runner=_real_c2,
    )
    assert calls == ["planner", "c1"]
    assert invoke.call_count == 1
    assert result.current_phase == OrchestrationPhase.PausedForCollectionApproval
    assert result.collection_planning_result.candidate_groups[0].proposed_schedule.scheduled_at.isoformat() == "2026-10-06T02:30:00+00:00"
    assert result.fleet_route_result is None and result.validation_operations_result is None


@patch("app.agents.collection_planning_agent.invoke_chat_model")
@patch("app.agents.collection_planning_agent.fetch_all_collection_needs")
def test_report_resume_uses_same_validated_c2_schedule(fetch, invoke):
    paused, report_id, calls = _paused_report()
    needs = [_need(), _need()]
    fetch.return_value = _snapshot(needs)
    invoke.return_value = AIMessage(content=json.dumps(_group_payload(needs, VALID)))

    result = resume_after_report_verification(
        paused, _context(paused.workflow_id, report_id), collection_planning_runner=_real_c2,
    )
    assert calls == ["planner", "c1"]
    assert invoke.call_count == 1
    assert result.current_phase == OrchestrationPhase.PausedForCollectionApproval
    assert result.collection_planning_result.candidate_groups[0].proposed_schedule.scheduled_at.isoformat() == "2026-10-06T02:30:00+00:00"
    assert result.fleet_route_result is None and result.validation_operations_result is None
