import json
from unittest.mock import MagicMock, call, patch
from uuid import UUID, uuid4

import httpx
import pytest

from app.core.config import Settings
from app.models.validation_operations import OperationalValidationContextResponse
from app.tools.operational_validation import (
    INTERNAL_AUTH_HEADER,
    OPERATIONAL_VALIDATION_CONTEXT_PATH,
    fetch_operational_validation_context,
    get_operational_validation_context,
)


def _mock_settings(internal_key: str = "TestKey123!", base_url: str = "http://backend.local") -> Settings:
    return Settings(
        INTERNAL_SERVICE_KEY=internal_key,
        ASPNET_API_BASE_URL=base_url,
        TOOL_HTTP_TIMEOUT=5.0,
    )


def test_fetch_operational_validation_context_success():
    tid = uuid4()
    did = uuid4()
    vid = uuid4()

    mock_resp_data = {
        "tasks": [
            {
                "taskId": str(tid),
                "taskCode": "TSK-001",
                "status": "Scheduled",
                "hasActiveAssignment": False,
                "wasteTypes": ["General"],
            }
        ],
        "drivers": [
            {
                "driverId": str(did),
                "displayName": "Sunil Perera",
                "availabilityStatus": "Available",
                "isOccupied": False,
            }
        ],
        "vehicles": [
            {
                "vehicleId": str(vid),
                "registrationNumber": "WP-CAB-1234",
                "vehicleType": "CompactorTruck",
                "operationalStatus": "Available",
                "isOccupied": False,
                "supportedWasteTypes": ["General"],
            }
        ],
    }

    mock_client = MagicMock(spec=httpx.Client)
    mock_resp = MagicMock(spec=httpx.Response)
    mock_resp.status_code = 200
    mock_resp.json.return_value = mock_resp_data
    mock_client.post.return_value = mock_resp

    with patch("app.tools.operational_validation.get_settings", return_value=_mock_settings()):
        res = fetch_operational_validation_context(
            task_ids=[tid],
            driver_ids=[did],
            vehicle_ids=[vid],
            client=mock_client,
        )

    assert isinstance(res, OperationalValidationContextResponse)
    assert len(res.tasks) == 1
    assert res.tasks[0].task_id == tid
    assert res.tasks[0].status == "Scheduled"
    assert res.tasks[0].has_active_assignment is False
    assert len(res.drivers) == 1
    assert res.drivers[0].driver_id == did
    assert res.drivers[0].availability_status == "Available"
    assert len(res.vehicles) == 1
    assert res.vehicles[0].vehicle_id == vid
    assert res.vehicles[0].operational_status == "Available"


def test_fetch_operational_validation_context_empty_collections():
    mock_client = MagicMock(spec=httpx.Client)
    mock_resp = MagicMock(spec=httpx.Response)
    mock_resp.status_code = 200
    mock_resp.json.return_value = {"tasks": [], "drivers": [], "vehicles": []}
    mock_client.post.return_value = mock_resp

    with patch("app.tools.operational_validation.get_settings", return_value=_mock_settings()):
        res = fetch_operational_validation_context(
            task_ids=[],
            driver_ids=[],
            vehicle_ids=[],
            client=mock_client,
        )

    assert len(res.tasks) == 0
    assert len(res.drivers) == 0
    assert len(res.vehicles) == 0


def test_missing_internal_service_key_fails_before_http():
    mock_client = MagicMock(spec=httpx.Client)
    with patch("app.tools.operational_validation.get_settings", return_value=_mock_settings(internal_key="")):
        with pytest.raises(RuntimeError, match="authentication key is not configured"):
            fetch_operational_validation_context(client=mock_client)
    mock_client.post.assert_not_called()


def test_auth_header_sent_properly():
    mock_client = MagicMock(spec=httpx.Client)
    mock_resp = MagicMock(spec=httpx.Response)
    mock_resp.status_code = 200
    mock_resp.json.return_value = {"tasks": [], "drivers": [], "vehicles": []}
    mock_client.post.return_value = mock_resp

    key = "SpecialSecretKey999"
    with patch("app.tools.operational_validation.get_settings", return_value=_mock_settings(internal_key=key)):
        fetch_operational_validation_context(client=mock_client)

    args, kwargs = mock_client.post.call_args
    assert kwargs["headers"][INTERNAL_AUTH_HEADER] == key


@pytest.mark.parametrize("status_code", [400, 401, 403, 404])
def test_client_errors_no_retry(status_code):
    mock_client = MagicMock(spec=httpx.Client)
    mock_resp = MagicMock(spec=httpx.Response)
    mock_resp.status_code = status_code
    mock_resp.text = f"Error {status_code}"
    mock_client.post.return_value = mock_resp

    with patch("app.tools.operational_validation.get_settings", return_value=_mock_settings()):
        with pytest.raises(RuntimeError):
            fetch_operational_validation_context(client=mock_client)

    assert mock_client.post.call_count == 1


def test_server_error_500_retries_once_then_succeeds():
    mock_client = MagicMock(spec=httpx.Client)
    resp_500 = MagicMock(spec=httpx.Response)
    resp_500.status_code = 500

    resp_200 = MagicMock(spec=httpx.Response)
    resp_200.status_code = 200
    resp_200.json.return_value = {"tasks": [], "drivers": [], "vehicles": []}

    mock_client.post.side_effect = [resp_500, resp_200]

    with patch("app.tools.operational_validation.get_settings", return_value=_mock_settings()):
        with patch("time.sleep") as mock_sleep:
            res = fetch_operational_validation_context(client=mock_client)

    assert mock_client.post.call_count == 2
    mock_sleep.assert_called_once()
    assert len(res.tasks) == 0


def test_server_error_500_persists_after_retry():
    mock_client = MagicMock(spec=httpx.Client)
    resp_500 = MagicMock(spec=httpx.Response)
    resp_500.status_code = 500
    mock_client.post.return_value = resp_500

    with patch("app.tools.operational_validation.get_settings", return_value=_mock_settings()):
        with patch("time.sleep"):
            with pytest.raises(RuntimeError, match="Backend server error"):
                fetch_operational_validation_context(client=mock_client)

    assert mock_client.post.call_count == 2


def test_network_connect_error_retries_once():
    mock_client = MagicMock(spec=httpx.Client)
    resp_200 = MagicMock(spec=httpx.Response)
    resp_200.status_code = 200
    resp_200.json.return_value = {"tasks": [], "drivers": [], "vehicles": []}

    mock_client.post.side_effect = [httpx.ConnectError("Connection refused"), resp_200]

    with patch("app.tools.operational_validation.get_settings", return_value=_mock_settings()):
        with patch("time.sleep") as mock_sleep:
            res = fetch_operational_validation_context(client=mock_client)

    assert mock_client.post.call_count == 2
    mock_sleep.assert_called_once()
    assert len(res.tasks) == 0


def test_network_timeout_persists_after_retry():
    mock_client = MagicMock(spec=httpx.Client)
    mock_client.post.side_effect = httpx.TimeoutException("Read timed out")

    with patch("app.tools.operational_validation.get_settings", return_value=_mock_settings()):
        with patch("time.sleep"):
            with pytest.raises(RuntimeError, match="Network error"):
                fetch_operational_validation_context(client=mock_client)

    assert mock_client.post.call_count == 2


def test_input_validation_invalid_task_uuid():
    with pytest.raises(ValueError, match="must be a valid UUID"):
        fetch_operational_validation_context(task_ids=["not-a-uuid"])


def test_input_validation_duplicate_task_ids():
    tid = uuid4()
    with pytest.raises(ValueError, match="must not contain duplicates"):
        fetch_operational_validation_context(task_ids=[tid, tid])


def test_input_validation_task_ids_exceed_limit():
    tids = [uuid4() for _ in range(101)]
    with pytest.raises(ValueError, match="Cannot request more than 100"):
        fetch_operational_validation_context(task_ids=tids)


def test_input_validation_invalid_driver_uuid():
    with pytest.raises(ValueError, match="must be a valid UUID"):
        fetch_operational_validation_context(driver_ids=["invalid-driver"])


def test_input_validation_duplicate_driver_ids():
    did = uuid4()
    with pytest.raises(ValueError, match="must not contain duplicates"):
        fetch_operational_validation_context(driver_ids=[did, did])


def test_input_validation_driver_ids_exceed_limit():
    dids = [uuid4() for _ in range(51)]
    with pytest.raises(ValueError, match="Cannot request more than 50"):
        fetch_operational_validation_context(driver_ids=dids)


def test_input_validation_invalid_vehicle_uuid():
    with pytest.raises(ValueError, match="must be a valid UUID"):
        fetch_operational_validation_context(vehicle_ids=["invalid-vehicle"])


def test_input_validation_duplicate_vehicle_ids():
    vid = uuid4()
    with pytest.raises(ValueError, match="must not contain duplicates"):
        fetch_operational_validation_context(vehicle_ids=[vid, vid])


def test_input_validation_vehicle_ids_exceed_limit():
    vids = [uuid4() for _ in range(51)]
    with pytest.raises(ValueError, match="Cannot request more than 50"):
        fetch_operational_validation_context(vehicle_ids=vids)


def test_schema_mismatch_rejected():
    mock_client = MagicMock(spec=httpx.Client)
    mock_resp = MagicMock(spec=httpx.Response)
    mock_resp.status_code = 200
    mock_resp.json.return_value = {"tasks": "not-a-list", "drivers": []}
    mock_client.post.return_value = mock_resp

    with patch("app.tools.operational_validation.get_settings", return_value=_mock_settings()):
        with pytest.raises(RuntimeError, match="Failed to parse operational validation context payload"):
            fetch_operational_validation_context(client=mock_client)


def test_langchain_tool_wrapper():
    mock_client = MagicMock(spec=httpx.Client)
    mock_resp = MagicMock(spec=httpx.Response)
    mock_resp.status_code = 200
    mock_resp.json.return_value = {"tasks": [], "drivers": [], "vehicles": []}
    mock_client.post.return_value = mock_resp

    with patch("app.tools.operational_validation.get_settings", return_value=_mock_settings()):
        with patch("httpx.Client", return_value=mock_client):
            res_str = get_operational_validation_context.invoke({})
            parsed = json.loads(res_str)
            assert "tasks" in parsed
            assert "drivers" in parsed
            assert "vehicles" in parsed
