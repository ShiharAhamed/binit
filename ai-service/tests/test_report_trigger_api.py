from uuid import uuid4

import pytest
from fastapi.testclient import TestClient

from app.api import orchestration as api
from app.core.config import get_settings
from app.main import app
from app.models.workflow_trigger import WorkflowTriggerType
from app.orchestration.enums import ApprovalPauseStage, OrchestrationPhase, OrchestrationStatus
from app.orchestration.graph import resume_after_report_verification
from app.orchestration.state import OrchestrationResultEnvelope
from tests.test_report_trigger_orchestration import _collection, _context, _paused_report


KEY = "test-only-report-resume-key"
START = "/api/v1/internal/agent-workflows/start"
RESUME = "/api/v1/internal/agent-workflows/resume-after-report-verification"


@pytest.fixture(autouse=True)
def internal_key(monkeypatch):
    monkeypatch.setenv("INTERNAL_SERVICE_KEY", KEY)
    get_settings.cache_clear()
    yield
    get_settings.cache_clear()


@pytest.fixture
def client():
    with TestClient(app) as test_client:
        yield test_client


def _headers(key=KEY):
    return {"X-Internal-Service-Key": key}


def test_old_manual_start_request_remains_valid(client, monkeypatch):
    paused = OrchestrationResultEnvelope(
        workflowId=uuid4(), objective="Coordinate authorized waste planning.",
        status=OrchestrationStatus.PAUSED,
        currentPhase=OrchestrationPhase.PausedForCollectionApproval,
        approvalStage=ApprovalPauseStage.COLLECTION_PLANNING,
    )
    received = []

    def runner(request):
        received.append(request)
        return paused

    monkeypatch.setattr(api, "run_collection_approval_phase", runner)
    response = client.post(START, headers=_headers(), json={"workflowId": str(uuid4()), "objective": paused.objective})
    assert response.status_code == 200
    assert len(received) == 1
    assert response.json()["errors"] == []
    assert response.json()["triggerType"] == "ManualOperationalPlanning"
    assert response.json()["triggeringWasteReportId"] is None


def test_citizen_start_passes_explicit_metadata_to_graph(client, monkeypatch):
    paused, report_id, _ = _paused_report()
    received = []

    def runner(request, **kwargs):
        received.append(kwargs)
        return paused

    monkeypatch.setattr(api, "run_collection_approval_phase", runner)
    response = client.post(START, headers=_headers(), json={
        "workflowId": str(paused.workflow_id), "objective": paused.objective,
        "triggerType": "CitizenReportSubmission", "triggeringWasteReportId": str(report_id),
    })
    assert response.status_code == 200
    assert received == [{"trigger_type": WorkflowTriggerType.CitizenReportSubmission,
                         "triggering_waste_report_id": report_id}]
    assert response.json()["currentPhase"] == "PausedForReportVerification"
    assert response.json()["triggeringWasteReportId"] == str(report_id)


@pytest.mark.parametrize("bad", [
    {"triggerType": "CitizenReportSubmission"},
    {"triggerType": "ManualOperationalPlanning", "triggeringWasteReportId": str(uuid4())},
    {"triggerType": "unknown"},
    {"triggerType": "CitizenReportSubmission", "triggeringWasteReportId": "not-a-uuid"},
    {"unrelated": "extra"},
])
def test_start_rejects_invalid_trigger_pairs_and_extra_fields(client, monkeypatch, bad):
    monkeypatch.setattr(api, "run_collection_approval_phase", lambda *_args, **_kwargs: pytest.fail("invalid request ran"))
    response = client.post(START, headers=_headers(), json={
        "workflowId": str(uuid4()), "objective": "Coordinate authorized waste planning.", **bad,
    })
    assert response.status_code == 422


def test_report_resume_auth_snapshot_and_c2_only_flow(client, monkeypatch):
    paused, report_id, _ = _paused_report()
    context = _context(paused.workflow_id, report_id)
    payload = {
        "workflow": paused.model_dump(by_alias=True, mode="json"),
        "resumeContext": context.model_dump(by_alias=True, mode="json"),
    }
    calls = []

    def offline_resume(workflow, resume_context):
        return resume_after_report_verification(
            workflow, resume_context,
            collection_planning_runner=lambda request: calls.append(request) or _collection(request.objective),
        )

    monkeypatch.setattr(api, "resume_after_report_verification", offline_resume)
    assert client.post(RESUME, json=payload).status_code == 401
    assert client.post(RESUME, headers=_headers("bad-key"), json=payload).status_code == 401
    response = client.post(RESUME, headers=_headers(), json=payload)
    assert response.status_code == 200
    assert len(calls) == 1
    assert response.json()["currentPhase"] == "PausedForCollectionApproval"
    assert response.json()["errors"] == []
    assert response.json()["warnings"] == []

    bad = {**payload, "extra": "forbidden"}
    assert client.post(RESUME, headers=_headers(), json=bad).status_code == 422
    malformed = {**payload, "workflow": {**payload["workflow"], "plannerResult": {"bad": "planner"}}}
    assert client.post(RESUME, headers=_headers(), json=malformed).status_code == 422
    wrong_phase = {**payload, "workflow": {**payload["workflow"], "currentPhase": "PausedForCollectionApproval"}}
    assert client.post(RESUME, headers=_headers(), json=wrong_phase).status_code == 409
    assert len(calls) == 1
