import time
from unittest.mock import MagicMock, patch
from uuid import uuid4

import httpx
import pytest

from app.core.config import get_settings
from app.models.fleet_resources import (
    FleetCompatibilityResult,
    FleetCompatibilityStatus,
    FleetPlanningContextResponse,
)
from app.tools.fleet_planning import (
    FLEET_COMPATIBILITY_PATH,
    FLEET_PLANNING_CONTEXT_PATH,
    INTERNAL_AUTH_HEADER,
    check_fleet_compatibility,
    fetch_all_fleet_planning_context,
    fetch_fleet_compatibility,
    fetch_fleet_planning_context,
    get_fleet_planning_context,
)

TEST_INTERNAL_KEY = "test-internal-fleet-key-12345"


@pytest.fixture(autouse=True)
def configure_test_internal_key(monkeypatch):
    monkeypatch.setenv("INTERNAL_SERVICE_KEY", TEST_INTERNAL_KEY)
    get_settings.cache_clear()
    yield
    get_settings.cache_clear()


def _sample_task(task_id=None, task_code="TASK-001", target_type="Report", waste_types=None):
    return {
        "taskId": str(task_id or uuid4()),
        "taskCode": task_code,
        "targetType": target_type,
        "collectionReason": "VerifiedReport",
        "scheduledAt": "2026-09-27T08:00:00Z",
        "addressText": "123 Galle Rd, Colombo",
        "latitude": 6.9271,
        "longitude": 79.8612,
        "wasteTypes": ["Organic"] if waste_types is None else waste_types,
    }


def _sample_driver(driver_id=None, display_name="John Driver", is_occupied=False):
    return {
        "driverId": str(driver_id or uuid4()),
        "displayName": display_name,
        "availabilityStatus": "Available",
        "isOccupied": is_occupied,
    }


def _sample_vehicle(vehicle_id=None, reg="WP-CAB-1234", vehicle_type="Compactor", is_occupied=False, capacity_liters=3000):
    return {
        "vehicleId": str(vehicle_id or uuid4()),
        "registrationNumber": reg,
        "vehicleType": vehicle_type,
        "capacityLiters": capacity_liters,
        "operationalStatus": "Available",
        "isOccupied": is_occupied,
        "supportedWasteTypes": ["Organic", "General"],
    }


def _sample_context_json(tasks=None, drivers=None, vehicles=None, page=1, page_size=20, total=None):
    t_list = [_sample_task()] if tasks is None else tasks
    d_list = [_sample_driver()] if drivers is None else drivers
    v_list = [_sample_vehicle()] if vehicles is None else vehicles
    total_count = len(t_list) if total is None else total
    total_pages = 1 if total_count > 0 else 0
    return {
        "tasks": t_list,
        "drivers": d_list,
        "vehicles": v_list,
        "taskPage": page,
        "taskPageSize": page_size,
        "taskTotalCount": total_count,
        "taskTotalPages": total_pages,
    }


# ──────────────────────────────────────────────────────────────────────────────
# 1. CONTEXT FETCH TESTS
# ──────────────────────────────────────────────────────────────────────────────

def test_fetch_fleet_planning_context_success():
    task_id = uuid4()
    driver_id = uuid4()
    vehicle_id = uuid4()

    mock_client = MagicMock(spec=httpx.Client)
    mock_response = MagicMock(spec=httpx.Response)
    mock_response.status_code = 200
    mock_response.json.return_value = _sample_context_json(
        tasks=[_sample_task(task_id=task_id, task_code="T-100", waste_types=["Organic"])],
        drivers=[_sample_driver(driver_id=driver_id, display_name="Jane Doe")],
        vehicles=[_sample_vehicle(vehicle_id=vehicle_id, reg="WP-XYZ-9999", capacity_liters=4500)],
        page=1,
        page_size=20,
        total=1,
    )
    mock_client.get.return_value = mock_response

    result = fetch_fleet_planning_context(page=1, page_size=20, client=mock_client)

    assert isinstance(result, FleetPlanningContextResponse)
    assert len(result.tasks) == 1
    assert result.tasks[0].task_id == task_id
    assert result.tasks[0].task_code == "T-100"
    assert result.tasks[0].waste_types == ["Organic"]
    assert result.tasks[0].latitude == 6.9271
    assert result.tasks[0].longitude == 79.8612

    assert len(result.drivers) == 1
    assert result.drivers[0].driver_id == driver_id
    assert result.drivers[0].display_name == "Jane Doe"
    assert result.drivers[0].availability_status == "Available"
    assert result.drivers[0].is_occupied is False

    assert len(result.vehicles) == 1
    assert result.vehicles[0].vehicle_id == vehicle_id
    assert result.vehicles[0].registration_number == "WP-XYZ-9999"
    assert result.vehicles[0].capacity_liters == 4500
    assert result.vehicles[0].is_occupied is False
    assert result.vehicles[0].supported_waste_types == ["Organic", "General"]

    assert result.task_page == 1
    assert result.task_page_size == 20
    assert result.task_total_count == 1
    assert result.task_total_pages == 1


def test_fetch_fleet_planning_context_empty_collections():
    mock_client = MagicMock(spec=httpx.Client)
    mock_response = MagicMock(spec=httpx.Response)
    mock_response.status_code = 200
    mock_response.json.return_value = _sample_context_json(tasks=[], drivers=[], vehicles=[], total=0)
    mock_client.get.return_value = mock_response

    result = fetch_fleet_planning_context(client=mock_client)

    assert result.tasks == []
    assert result.drivers == []
    assert result.vehicles == []
    assert result.task_total_count == 0


def test_fetch_fleet_planning_context_optional_fields():
    task = _sample_task()
    task["addressText"] = None
    task["latitude"] = None
    task["longitude"] = None
    task["wasteTypes"] = []

    mock_client = MagicMock(spec=httpx.Client)
    mock_response = MagicMock(spec=httpx.Response)
    mock_response.status_code = 200
    mock_response.json.return_value = _sample_context_json(tasks=[task])
    mock_client.get.return_value = mock_response

    result = fetch_fleet_planning_context(client=mock_client)
    assert result.tasks[0].address_text is None
    assert result.tasks[0].latitude is None
    assert result.tasks[0].longitude is None
    assert result.tasks[0].waste_types == []


@pytest.mark.parametrize("page,page_size", [
    (0, 20),
    (-1, 20),
    (True, 20),
    (1, 0),
    (1, 51),
    (1, -5),
    (1, True),
    ("1", 20),
    (1, "20"),
])
def test_fetch_fleet_planning_context_pagination_bounds_validation(page, page_size):
    with pytest.raises(ValueError):
        fetch_fleet_planning_context(page=page, page_size=page_size)


# ──────────────────────────────────────────────────────────────────────────────
# 2. AUTHENTICATION & HEADER TESTS
# ──────────────────────────────────────────────────────────────────────────────

def test_missing_internal_service_key_fails_before_http(monkeypatch):
    monkeypatch.setenv("INTERNAL_SERVICE_KEY", "")
    get_settings.cache_clear()

    mock_client = MagicMock(spec=httpx.Client)

    with pytest.raises(RuntimeError, match="Internal service authentication key is not configured"):
        fetch_fleet_planning_context(client=mock_client)

    mock_client.get.assert_not_called()

    with pytest.raises(RuntimeError, match="Internal service authentication key is not configured"):
        fetch_fleet_compatibility(task_ids=[uuid4()], vehicle_id=uuid4(), client=mock_client)

    mock_client.post.assert_not_called()


def test_auth_header_sent_properly():
    mock_client = MagicMock(spec=httpx.Client)
    mock_response = MagicMock(spec=httpx.Response)
    mock_response.status_code = 200
    mock_response.json.return_value = _sample_context_json()
    mock_client.get.return_value = mock_response

    fetch_fleet_planning_context(page=2, page_size=15, client=mock_client)

    call_args = mock_client.get.call_args
    assert call_args is not None
    url, kwargs = call_args[0][0], call_args[1]

    assert FLEET_PLANNING_CONTEXT_PATH in url
    assert kwargs["params"] == {"page": 2, "pageSize": 15}
    assert kwargs["headers"][INTERNAL_AUTH_HEADER] == TEST_INTERNAL_KEY
    assert "Authorization" not in kwargs["headers"]
    assert "Bearer" not in str(kwargs["headers"])


# ──────────────────────────────────────────────────────────────────────────────
# 3. HTTP FAILURE & RETRY TESTS
# ──────────────────────────────────────────────────────────────────────────────

@pytest.mark.parametrize("status_code", [400, 401, 403, 404])
def test_client_errors_no_retry(status_code):
    mock_client = MagicMock(spec=httpx.Client)
    mock_response = MagicMock(spec=httpx.Response)
    mock_response.status_code = status_code
    mock_response.text = "Client error details"
    mock_client.get.return_value = mock_response

    with pytest.raises(RuntimeError):
        fetch_fleet_planning_context(client=mock_client)

    assert mock_client.get.call_count == 1


@patch("time.sleep", return_value=None)
def test_server_error_500_retries_once_then_succeeds(mock_sleep):
    mock_client = MagicMock(spec=httpx.Client)
    err_res = MagicMock(spec=httpx.Response)
    err_res.status_code = 500
    err_res.text = "Internal server error"

    ok_res = MagicMock(spec=httpx.Response)
    ok_res.status_code = 200
    ok_res.json.return_value = _sample_context_json()

    mock_client.get.side_effect = [err_res, ok_res]

    result = fetch_fleet_planning_context(client=mock_client)
    assert isinstance(result, FleetPlanningContextResponse)
    assert mock_client.get.call_count == 2
    mock_sleep.assert_called_once_with(0.5)


@patch("time.sleep", return_value=None)
def test_server_error_500_persists_after_retry(mock_sleep):
    mock_client = MagicMock(spec=httpx.Client)
    err_res = MagicMock(spec=httpx.Response)
    err_res.status_code = 500
    err_res.text = "Server down"
    mock_client.get.return_value = err_res

    with pytest.raises(RuntimeError, match="Backend error \\(HTTP 500\\) after retry"):
        fetch_fleet_planning_context(client=mock_client)

    assert mock_client.get.call_count == 2


@patch("time.sleep", return_value=None)
def test_network_connect_error_retries_once(mock_sleep):
    mock_client = MagicMock(spec=httpx.Client)
    mock_client.get.side_effect = [
        httpx.ConnectError("Failed to connect"),
        MagicMock(status_code=200, json=lambda: _sample_context_json()),
    ]

    result = fetch_fleet_planning_context(client=mock_client)
    assert isinstance(result, FleetPlanningContextResponse)
    assert mock_client.get.call_count == 2


@patch("time.sleep", return_value=None)
def test_network_timeout_persists_after_retry(mock_sleep):
    mock_client = MagicMock(spec=httpx.Client)
    mock_client.get.side_effect = httpx.ReadTimeout("Timed out")

    with pytest.raises(RuntimeError, match="Failed to connect to SmartWaste backend after retry"):
        fetch_fleet_planning_context(client=mock_client)

    assert mock_client.get.call_count == 2


# ──────────────────────────────────────────────────────────────────────────────
# 4. FLEET COMPATIBILITY TESTS
# ──────────────────────────────────────────────────────────────────────────────

def test_fetch_fleet_compatibility_compatible():
    task_id = uuid4()
    vehicle_id = uuid4()

    mock_client = MagicMock(spec=httpx.Client)
    mock_response = MagicMock(spec=httpx.Response)
    mock_response.status_code = 200
    mock_response.json.return_value = {
        "status": "Compatible",
        "requiresAcknowledgement": False,
        "issues": [],
    }
    mock_client.post.return_value = mock_response

    result = fetch_fleet_compatibility(task_ids=[task_id], vehicle_id=vehicle_id, client=mock_client)

    assert isinstance(result, FleetCompatibilityResult)
    assert result.status == FleetCompatibilityStatus.COMPATIBLE
    assert result.requires_acknowledgement is False
    assert result.issues == []

    # Check POST payload
    call_args = mock_client.post.call_args
    assert call_args is not None
    url, kwargs = call_args[0][0], call_args[1]
    assert FLEET_COMPATIBILITY_PATH in url
    assert kwargs["json"] == {"taskIds": [str(task_id)], "vehicleId": str(vehicle_id)}
    assert kwargs["headers"][INTERNAL_AUTH_HEADER] == TEST_INTERNAL_KEY


def test_fetch_fleet_compatibility_unknown_is_data_not_exception():
    mock_client = MagicMock(spec=httpx.Client)
    mock_response = MagicMock(spec=httpx.Response)
    mock_response.status_code = 200
    mock_response.json.return_value = {
        "status": "Unknown",
        "requiresAcknowledgement": True,
        "issues": ["Waste-handling uncertainty requires an officer acknowledgement."],
    }
    mock_client.post.return_value = mock_response

    result = fetch_fleet_compatibility(task_ids=[uuid4()], vehicle_id=uuid4(), client=mock_client)

    assert result.status == FleetCompatibilityStatus.UNKNOWN
    assert result.requires_acknowledgement is True
    assert len(result.issues) == 1
    assert "uncertainty" in result.issues[0]


def test_fetch_fleet_compatibility_incompatible_is_data_not_exception():
    mock_client = MagicMock(spec=httpx.Client)
    mock_response = MagicMock(spec=httpx.Response)
    mock_response.status_code = 200
    mock_response.json.return_value = {
        "status": "Incompatible",
        "requiresAcknowledgement": False,
        "issues": ["The selected vehicle is incompatible with the selected waste handling requirements."],
    }
    mock_client.post.return_value = mock_response

    result = fetch_fleet_compatibility(task_ids=[uuid4()], vehicle_id=uuid4(), client=mock_client)

    assert result.status == FleetCompatibilityStatus.INCOMPATIBLE
    assert result.requires_acknowledgement is False
    assert len(result.issues) == 1
    assert "incompatible" in result.issues[0]


def test_fetch_fleet_compatibility_404_raises_runtime_error():
    mock_client = MagicMock(spec=httpx.Client)
    mock_response = MagicMock(spec=httpx.Response)
    mock_response.status_code = 404
    mock_response.text = "Vehicle was not found."
    mock_client.post.return_value = mock_response

    with pytest.raises(RuntimeError, match="Backend rejected fleet compatibility request \\(HTTP 404\\)"):
        fetch_fleet_compatibility(task_ids=[uuid4()], vehicle_id=uuid4(), client=mock_client)

    assert mock_client.post.call_count == 1


@pytest.mark.parametrize("task_ids,vehicle_id,error_msg", [
    ([], str(uuid4()), "must contain at least one task ID"),
    ([str(uuid4())] * 51, str(uuid4()), "cannot exceed 50 tasks"),
    (["not-a-uuid"], str(uuid4()), "must be a valid UUID"),
    ([str(uuid4())], "not-a-uuid", "must be a valid UUID"),
])
def test_fetch_fleet_compatibility_input_validation(task_ids, vehicle_id, error_msg):
    with pytest.raises(ValueError, match=error_msg):
        fetch_fleet_compatibility(task_ids=task_ids, vehicle_id=vehicle_id)


def test_fetch_fleet_compatibility_rejects_duplicate_tasks():
    tid = uuid4()
    with pytest.raises(ValueError, match="duplicate task IDs"):
        fetch_fleet_compatibility(task_ids=[tid, tid], vehicle_id=uuid4())


# ──────────────────────────────────────────────────────────────────────────────
# 5. LANGCHAIN @tool WRAPPER TESTS
# ──────────────────────────────────────────────────────────────────────────────

def test_langchain_tool_wrappers_metadata():
    assert get_fleet_planning_context.name == "get_fleet_planning_context"
    assert "Scheduled collection tasks" in get_fleet_planning_context.description
    assert check_fleet_compatibility.name == "check_fleet_compatibility"
    assert "waste-type compatibility" in check_fleet_compatibility.description


# ──────────────────────────────────────────────────────────────────────────────
# 6. MULTI-PAGE SNAPSHOT RETRIEVAL TESTS (fetch_all_fleet_planning_context)
# ──────────────────────────────────────────────────────────────────────────────

def test_fetch_all_context_single_page():
    mock_client = MagicMock(spec=httpx.Client)
    page1_data = {
        "tasks": [_sample_task(task_code="TSK-001")],
        "drivers": [_sample_driver(display_name="Driver 1")],
        "vehicles": [_sample_vehicle(reg="WP-001")],
        "taskPage": 1,
        "taskPageSize": 20,
        "taskTotalCount": 1,
        "taskTotalPages": 1,
    }
    mock_resp = MagicMock()
    mock_resp.status_code = 200
    mock_resp.json.return_value = page1_data
    mock_client.get.return_value = mock_resp

    result = fetch_all_fleet_planning_context(page_size=20, client=mock_client)

    assert len(result.tasks) == 1
    assert result.task_total_count == 1
    assert result.task_total_pages == 1
    assert mock_client.get.call_count == 1


def test_fetch_all_context_multi_page_merges_tasks_and_dedupes_resources():
    mock_client = MagicMock(spec=httpx.Client)
    d1 = _sample_driver(display_name="Driver 1")
    v1 = _sample_vehicle(reg="WP-001")
    page1_data = {
        "tasks": [_sample_task(task_code="TSK-001")],
        "drivers": [d1],
        "vehicles": [v1],
        "taskPage": 1,
        "taskPageSize": 1,
        "taskTotalCount": 2,
        "taskTotalPages": 2,
    }
    page2_data = {
        "tasks": [_sample_task(task_code="TSK-002")],
        "drivers": [d1],  # Duplicate driver across pages
        "vehicles": [v1],  # Duplicate vehicle across pages
        "taskPage": 2,
        "taskPageSize": 1,
        "taskTotalCount": 2,
        "taskTotalPages": 2,
    }
    resp1, resp2 = MagicMock(), MagicMock()
    resp1.status_code = 200
    resp1.json.return_value = page1_data
    resp2.status_code = 200
    resp2.json.return_value = page2_data
    mock_client.get.side_effect = [resp1, resp2]

    result = fetch_all_fleet_planning_context(page_size=1, client=mock_client)

    assert len(result.tasks) == 2
    assert result.tasks[0].task_code == "TSK-001"
    assert result.tasks[1].task_code == "TSK-002"
    assert len(result.drivers) == 1  # Deduplicated
    assert len(result.vehicles) == 1  # Deduplicated
    assert mock_client.get.call_count == 2


def test_fetch_all_context_inconsistent_total_count_raises_error():
    mock_client = MagicMock(spec=httpx.Client)
    page1_data = {
        "tasks": [_sample_task()],
        "drivers": [],
        "vehicles": [],
        "taskPage": 1,
        "taskPageSize": 1,
        "taskTotalCount": 2,
        "taskTotalPages": 2,
    }
    page2_data = {
        "tasks": [_sample_task()],
        "drivers": [],
        "vehicles": [],
        "taskPage": 2,
        "taskPageSize": 1,
        "taskTotalCount": 999,  # Inconsistent!
        "taskTotalPages": 2,
    }
    resp1, resp2 = MagicMock(), MagicMock()
    resp1.status_code = 200
    resp1.json.return_value = page1_data
    resp2.status_code = 200
    resp2.json.return_value = page2_data
    mock_client.get.side_effect = [resp1, resp2]

    with pytest.raises(RuntimeError, match="Inconsistent taskTotalCount"):
        fetch_all_fleet_planning_context(page_size=1, client=mock_client)


def test_fetch_all_context_duplicate_task_id_across_pages_raises_error():
    mock_client = MagicMock(spec=httpx.Client)
    shared_task_id = uuid4()
    page1_data = {
        "tasks": [_sample_task(task_id=shared_task_id)],
        "drivers": [],
        "vehicles": [],
        "taskPage": 1,
        "taskPageSize": 1,
        "taskTotalCount": 2,
        "taskTotalPages": 2,
    }
    page2_data = {
        "tasks": [_sample_task(task_id=shared_task_id)],  # Same task ID!
        "drivers": [],
        "vehicles": [],
        "taskPage": 2,
        "taskPageSize": 1,
        "taskTotalCount": 2,
        "taskTotalPages": 2,
    }
    resp1, resp2 = MagicMock(), MagicMock()
    resp1.status_code = 200
    resp1.json.return_value = page1_data
    resp2.status_code = 200
    resp2.json.return_value = page2_data
    mock_client.get.side_effect = [resp1, resp2]

    with pytest.raises(RuntimeError, match="Duplicate collection task detected"):
        fetch_all_fleet_planning_context(page_size=1, client=mock_client)

