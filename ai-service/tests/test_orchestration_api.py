import inspect
from uuid import uuid4

import pytest
from fastapi.testclient import TestClient

from app.api.internal_auth import INTERNAL_AUTH_HEADER, require_internal_service_key
from app.api import orchestration as orchestration_api
from app.core.config import get_settings
from app.orchestration.enums import ApprovalPauseStage, OrchestrationPhase, OrchestrationStatus, ResumeDecision
from app.orchestration.errors import INVALID_RESUME_CONTEXT, OrchestrationError
from app.orchestration.state import OrchestrationResultEnvelope, WorkflowResumeContext
from app.orchestration.graph import resume_after_collection_approval as run_dispatch_graph
from app.main import app
from tests.test_orchestration_dispatch_phase import _paused_result as complete_paused_result, _successful_runners


TEST_KEY = "test-only-internal-service-key"


@pytest.fixture(autouse=True)
def internal_service_key(monkeypatch):
    monkeypatch.setenv("INTERNAL_SERVICE_KEY", TEST_KEY)
    get_settings.cache_clear()
    yield
    get_settings.cache_clear()


@pytest.fixture
def client():
    with TestClient(app) as test_client:
        yield test_client


def _headers(key=TEST_KEY):
    return {INTERNAL_AUTH_HEADER: key}


def _paused_result(workflow_id=None):
    return OrchestrationResultEnvelope(
        workflowId=workflow_id or uuid4(),
        objective="Coordinate an end-to-end collection operation.",
        status=OrchestrationStatus.PAUSED,
        currentPhase=OrchestrationPhase.PausedForCollectionApproval,
        approvalStage=ApprovalPauseStage.COLLECTION_PLANNING,
        pauseReason="Collection planning awaits authorized review.",
        completedSpecialists=["WasteAnalysis", "CollectionPlanning"],
    )


def _resume_context(workflow_id):
    return WorkflowResumeContext(
        workflowId=workflow_id,
        approvalStage=ApprovalPauseStage.COLLECTION_PLANNING,
        decision=ResumeDecision.APPROVED,
        authoritativeExecutionSummary={"createdTaskCount": 1},
    )


def test_internal_auth_rejects_missing_wrong_and_unconfigured_keys(client, monkeypatch):
    response = client.post("/api/v1/internal/agent-workflows/start", json={})
    assert response.status_code == 401

    response = client.post(
        "/api/v1/internal/agent-workflows/start",
        headers=_headers("wrong-key"),
        json={},
    )
    assert response.status_code == 401

    monkeypatch.setenv("INTERNAL_SERVICE_KEY", "")
    get_settings.cache_clear()
    response = client.post(
        "/api/v1/internal/agent-workflows/start",
        headers=_headers(),
        json={},
    )
    assert response.status_code == 401


def test_start_delegates_once_and_preserves_aspnet_workflow_id(client, monkeypatch):
    workflow_id = uuid4()
    expected = _paused_result(workflow_id)
    received = []

    def start_runner(request):
        received.append(request)
        return expected

    monkeypatch.setattr(orchestration_api, "run_collection_approval_phase", start_runner)
    response = client.post(
        "/api/v1/internal/agent-workflows/start",
        headers=_headers(),
        json={"workflowId": str(workflow_id), "objective": expected.objective},
    )

    assert response.status_code == 200
    assert len(received) == 1
    assert received[0].workflow_id == workflow_id
    assert received[0].objective == expected.objective
    assert response.json()["currentPhase"] == "PausedForCollectionApproval"


def test_start_invalid_objective_never_invokes_runner(client, monkeypatch):
    monkeypatch.setattr(
        orchestration_api,
        "run_collection_approval_phase",
        lambda _request: pytest.fail("runner must not run for invalid transport input"),
    )
    response = client.post(
        "/api/v1/internal/agent-workflows/start",
        headers=_headers(),
        json={"workflowId": str(uuid4()), "objective": "bad"},
    )
    assert response.status_code == 422


def test_start_preserves_safe_failed_envelope_as_http_success(client, monkeypatch):
    workflow_id = uuid4()
    failed = OrchestrationResultEnvelope(
        workflowId=workflow_id,
        objective="Coordinate collection operation.",
        status=OrchestrationStatus.FAILED,
        currentPhase=OrchestrationPhase.Failed,
        errors=[OrchestrationError(code="PLANNER_VALIDATION_FAILED", stage="SharedPlanner", message="Safe failure")],
    )
    monkeypatch.setattr(orchestration_api, "run_collection_approval_phase", lambda _request: failed)
    response = client.post(
        "/api/v1/internal/agent-workflows/start",
        headers=_headers(),
        json={"workflowId": str(workflow_id), "objective": failed.objective},
    )
    assert response.status_code == 200
    assert response.json()["currentPhase"] == "Failed"
    assert response.json()["errors"][0]["code"] == "PLANNER_VALIDATION_FAILED"


def test_resume_ready_and_revision_results_are_successful_transport(client, monkeypatch):
    paused = _paused_result()
    context = _resume_context(paused.workflow_id)

    ready = paused.model_copy(
        update={
            "current_phase": OrchestrationPhase.PausedForDispatchApproval,
            "approval_stage": ApprovalPauseStage.FLEET_DISPATCH,
            "pause_reason": "Dispatch review required.",
        }
    )
    monkeypatch.setattr(orchestration_api, "resume_after_collection_approval", lambda *_args: ready)
    payload = {"workflow": paused.model_dump(by_alias=True, mode="json"), "resumeContext": context.model_dump(by_alias=True, mode="json")}
    response = client.post("/api/v1/internal/agent-workflows/resume-after-collection-approval", headers=_headers(), json=payload)
    assert response.status_code == 200
    assert response.json()["currentPhase"] == "PausedForDispatchApproval"

    revision = ready.model_copy(
        update={
            "current_phase": OrchestrationPhase.DispatchNeedsRevision,
            "approval_stage": ApprovalPauseStage.NONE,
            "pause_reason": None,
            "status": OrchestrationStatus.RUNNING,
            "final_outcome": "DispatchNeedsRevision",
        }
    )
    monkeypatch.setattr(orchestration_api, "resume_after_collection_approval", lambda *_args: revision)
    response = client.post("/api/v1/internal/agent-workflows/resume-after-collection-approval", headers=_headers(), json=payload)
    assert response.status_code == 200
    assert response.json()["currentPhase"] == "DispatchNeedsRevision"


def test_resume_invalid_context_maps_to_conflict_and_malformed_snapshot_is_422(client, monkeypatch):
    paused = _paused_result()
    context = _resume_context(paused.workflow_id)
    invalid = OrchestrationResultEnvelope(
        workflowId=paused.workflow_id,
        objective=paused.objective,
        status=OrchestrationStatus.FAILED,
        currentPhase=OrchestrationPhase.Failed,
        errors=[OrchestrationError(code=INVALID_RESUME_CONTEXT, stage="CollectionResume", message="Safe conflict")],
    )
    monkeypatch.setattr(orchestration_api, "resume_after_collection_approval", lambda *_args: invalid)
    payload = {"workflow": paused.model_dump(by_alias=True, mode="json"), "resumeContext": context.model_dump(by_alias=True, mode="json")}
    response = client.post("/api/v1/internal/agent-workflows/resume-after-collection-approval", headers=_headers(), json=payload)
    assert response.status_code == 409
    assert response.json()["detail"] == "The supplied workflow snapshot cannot be resumed in its current state."

    response = client.post(
        "/api/v1/internal/agent-workflows/resume-after-collection-approval",
        headers=_headers(),
        json={"workflow": {}, "resumeContext": context.model_dump(by_alias=True, mode="json")},
    )
    assert response.status_code == 422


def test_aspnet_resume_wire_shape_validates_and_runs_c3_c4_without_gemini(client, monkeypatch):
    paused = complete_paused_result()
    calls, requests = [], {}
    fleet_route, validation_operations, _ = _successful_runners(calls, requests)

    def offline_resume(workflow, context):
        assert context.authoritative_execution_summary["createdTaskCount"] == 1
        assert context.authoritative_execution_summary["createdTasks"][0]["taskCode"] == "TSK-001"
        return run_dispatch_graph(
            workflow,
            context,
            fleet_route_runner=fleet_route,
            validation_operations_runner=validation_operations,
        )

    monkeypatch.setattr(orchestration_api, "resume_after_collection_approval", offline_resume)
    payload = {
        "workflow": paused.model_dump(by_alias=True, mode="json"),
        "resumeContext": {
            "workflowId": str(paused.workflow_id),
            "approvalStage": "CollectionPlanning",
            "decision": "Approved",
            "authoritativeExecutionSummary": {
                "createdTaskCount": 1,
                "deferredNeedCount": 0,
                "sourceCollectionPlanningStepId": str(uuid4()),
                "createdTasks": [
                    {
                        "collectionNeedId": str(uuid4()),
                        "collectionTaskId": str(uuid4()),
                        "taskCode": "TSK-001",
                        "targetType": "Report",
                        "targetId": str(uuid4()),
                        "scheduledAt": "2026-10-02T09:00:00Z",
                    }
                ],
            },
        },
    }
    response = client.post(
        "/api/v1/internal/agent-workflows/resume-after-collection-approval",
        headers=_headers(),
        json=payload,
    )

    assert response.status_code == 200
    assert calls == ["FleetRoute", "ValidationOperations"]
    assert response.json()["currentPhase"] == "PausedForDispatchApproval"
    assert response.json()["approvalStage"] == "FleetDispatch"
    assert response.json()["validationOperationsResult"]["validationOutcome"] == "ReadyForHumanReview"


def test_resume_wire_shape_rejects_null_lists_and_invalid_enum_before_agents(client, monkeypatch):
    paused = complete_paused_result()
    payload = {
        "workflow": paused.model_dump(by_alias=True, mode="json"),
        "resumeContext": _resume_context(paused.workflow_id).model_dump(by_alias=True, mode="json"),
    }
    monkeypatch.setattr(
        orchestration_api,
        "resume_after_collection_approval",
        lambda *_args: pytest.fail("invalid transport input must not run C3 or C4"),
    )

    payload["workflow"]["errors"] = None
    payload["workflow"]["warnings"] = None
    response = client.post(
        "/api/v1/internal/agent-workflows/resume-after-collection-approval",
        headers=_headers(),
        json=payload,
    )
    assert response.status_code == 422
    assert {tuple(item["loc"]) for item in response.json()["detail"]} == {
        ("body", "workflow", "errors"),
        ("body", "workflow", "warnings"),
    }

    payload["workflow"]["errors"] = []
    payload["workflow"]["warnings"] = []
    payload["resumeContext"]["approvalStage"] = "collection_planning"
    response = client.post(
        "/api/v1/internal/agent-workflows/resume-after-collection-approval",
        headers=_headers(),
        json=payload,
    )
    assert response.status_code == 422
    assert response.json()["detail"][0]["loc"] == ["body", "resumeContext", "approvalStage"]


def test_router_depends_only_on_orchestration_entry_points_and_health_remains_public(client):
    source = inspect.getsource(orchestration_api)
    for forbidden in (
        "run_shared_planner",
        "run_waste_analysis",
        "run_collection_planning",
        "run_fleet_route",
        "run_validation_operations",
    ):
        assert forbidden not in source
    assert "run_collection_approval_phase" in source
    assert "resume_after_collection_approval" in source
    assert client.get("/health").status_code == 200
