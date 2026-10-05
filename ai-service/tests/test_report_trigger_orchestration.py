from uuid import uuid4

import pytest
from pydantic import ValidationError

from app.models.analysis import WasteAnalysisResult, WasteReportAnalysis
from app.models.collection_planning import CollectionPlanningResult
from app.models.shared_planner import SharedPlannerRequest, create_flagship_planner_result
from app.models.workflow_trigger import WorkflowTriggerType
from app.orchestration.enums import ApprovalPauseStage, OrchestrationPhase, OrchestrationStatus, ResumeDecision
from app.orchestration.errors import INVALID_RESUME_CONTEXT
from app.orchestration.graph import resume_after_report_verification, run_collection_approval_phase
from app.orchestration.state import WorkflowResumeContext


OBJECTIVE = "Analyze the report and coordinate authorized collection planning."


def _analysis(report_id, objective):
    return WasteAnalysisResult(
        objective=objective,
        analyses=[WasteReportAnalysis(
            reportId=report_id,
            categoryAssessment="Mixed roadside waste",
            recommendedPriority="Medium",
            operationalConcerns=[],
            recommendedHandling="Standard collection",
            confidence="Medium",
            rationale="Reported waste requires human verification.",
        )],
        sourcePage=1, sourcePageSize=1, sourceTotalCount=1,
        agentName="waste_analysis_agent", modelName="fake", status="completed",
    )


def _collection(objective):
    return CollectionPlanningResult(
        objective=objective, candidateGroups=[], separateHandling=[], deferredNeeds=[], warnings=[],
        sourcePage=1, sourcePageSize=20, sourceTotalCount=0, sourceTotalPages=0,
        retrievedPages=[1], isCompleteSnapshot=True, agentName="collection_planning_agent",
        modelName="fake", advisoryOnly=True, status="empty",
    )


def _paused_report():
    report_id, workflow_id = uuid4(), uuid4()
    calls = []

    def planner(request, *, policy):
        calls.append("planner")
        return create_flagship_planner_result(objective=request.objective)

    def c1(request):
        calls.append("c1")
        assert request.triggering_waste_report_id == report_id
        assert request.workflow_objective == OBJECTIVE
        return _analysis(report_id, request.objective)

    def c2(request):
        calls.append("c2")
        return _collection(request.objective)

    result = run_collection_approval_phase(
        SharedPlannerRequest(workflowId=workflow_id, objective=OBJECTIVE),
        trigger_type=WorkflowTriggerType.CitizenReportSubmission,
        triggering_waste_report_id=report_id,
        planner_runner=planner, waste_analysis_runner=c1, collection_planning_runner=c2,
    )
    return result, report_id, calls


def _context(workflow_id, report_id):
    return WorkflowResumeContext(
        workflowId=workflow_id, approvalStage=ApprovalPauseStage.REPORT_VERIFICATION,
        decision=ResumeDecision.APPROVED,
        authoritativeExecutionSummary={"verifiedReportId": str(report_id), "reportStatus": "Verified"},
    )


def test_citizen_start_stops_after_exact_c1_and_persists_trigger_metadata():
    result, report_id, calls = _paused_report()
    assert calls == ["planner", "c1"]
    assert result.status == OrchestrationStatus.PAUSED
    assert result.current_phase == OrchestrationPhase.PausedForReportVerification
    assert result.approval_stage == ApprovalPauseStage.REPORT_VERIFICATION
    assert result.trigger_type == WorkflowTriggerType.CitizenReportSubmission
    assert result.triggering_waste_report_id == report_id
    assert result.collection_planning_result is None
    assert result.errors == [] and result.warnings == []
    wire = result.model_dump(by_alias=True, mode="json")
    assert wire["errors"] == [] and wire["warnings"] == []


def test_report_resume_runs_only_c2_and_retains_c1_snapshot():
    paused, report_id, calls = _paused_report()
    fresh_reads = []

    def c2(request):
        calls.append("c2")
        fresh_reads.append(request.objective)
        return _collection(request.objective)

    resumed = resume_after_report_verification(
        paused, _context(paused.workflow_id, report_id), collection_planning_runner=c2,
    )
    assert calls == ["planner", "c1", "c2"]
    assert len(fresh_reads) == 1
    assert resumed.current_phase == OrchestrationPhase.PausedForCollectionApproval
    assert resumed.approval_stage == ApprovalPauseStage.COLLECTION_PLANNING
    assert resumed.planner_result == paused.planner_result
    assert resumed.waste_analysis_result == paused.waste_analysis_result
    assert resumed.triggering_waste_report_id == report_id
    assert resumed.errors == [] and resumed.warnings == []


def test_report_resume_reads_fresh_authoritative_collection_needs(monkeypatch):
    from app.agents import collection_planning_agent as c2_agent
    from app.tools.collection_needs import CollectionNeedsSnapshot

    paused, report_id, calls = _paused_report()
    reads = []

    def fetch_needs(**kwargs):
        reads.append(kwargs)
        return CollectionNeedsSnapshot(
            items=[], retrieved_pages=[1], page_size=20,
            total_count=0, total_pages=0, is_complete=True,
        )

    monkeypatch.setattr(c2_agent, "fetch_all_collection_needs", fetch_needs)
    monkeypatch.setattr(c2_agent, "get_chat_model", lambda **_: pytest.fail("empty C2 must not call Gemini"))
    resumed = resume_after_report_verification(paused, _context(paused.workflow_id, report_id))
    assert calls == ["planner", "c1"]
    assert len(reads) == 1
    assert resumed.current_phase == OrchestrationPhase.PausedForCollectionApproval
    assert resumed.collection_planning_result.status == "empty"


@pytest.mark.parametrize("mutation", [
    "manual", "wrong_phase", "missing_c1", "wrong_analysis", "extra_analysis",
    "missing_planner", "wrong_evidence", "wrong_stage", "wrong_decision",
])
def test_report_resume_rejects_invalid_snapshot_without_running_c2(mutation):
    paused, report_id, _ = _paused_report()
    context = _context(paused.workflow_id, report_id)
    if mutation == "manual":
        paused = paused.model_copy(update={"trigger_type": WorkflowTriggerType.ManualOperationalPlanning,
                                          "triggering_waste_report_id": None})
    elif mutation == "wrong_phase":
        paused = paused.model_copy(update={"current_phase": OrchestrationPhase.PausedForCollectionApproval})
    elif mutation == "missing_c1":
        paused = paused.model_copy(update={"waste_analysis_result": None})
    elif mutation == "wrong_analysis":
        paused = paused.model_copy(update={"waste_analysis_result": _analysis(uuid4(), "Analyze reports")})
    elif mutation == "extra_analysis":
        bad = paused.waste_analysis_result.model_copy(update={"analyses": paused.waste_analysis_result.analyses + [_analysis(uuid4(), "Analyze reports").analyses[0]]})
        paused = paused.model_copy(update={"waste_analysis_result": bad})
    elif mutation == "missing_planner":
        paused = paused.model_copy(update={"planner_result": None})
    elif mutation == "wrong_evidence":
        context = context.model_copy(update={"authoritative_execution_summary": {"verifiedReportId": str(uuid4()), "reportStatus": "Verified"}})
    elif mutation == "wrong_stage":
        context = context.model_copy(update={"approval_stage": ApprovalPauseStage.COLLECTION_PLANNING})
    elif mutation == "wrong_decision":
        context = context.model_copy(update={"decision": ResumeDecision.REJECTED})

    def forbidden_c2(_):
        pytest.fail("Invalid report snapshot must not run C2")

    result = resume_after_report_verification(paused, context, collection_planning_runner=forbidden_c2)
    assert result.current_phase == OrchestrationPhase.Failed
    assert result.errors[0].code == INVALID_RESUME_CONTEXT


def test_malformed_planner_snapshot_fails_strict_transport_validation():
    paused, _, _ = _paused_report()
    wire = paused.model_dump(by_alias=True, mode="json")
    wire["plannerResult"] = {"bad": "plan"}
    with pytest.raises(ValidationError):
        type(paused).model_validate(wire)
