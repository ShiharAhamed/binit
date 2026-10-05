import sys
from unittest.mock import MagicMock, patch
from uuid import UUID, uuid4

import httpx
import pytest

from app.core.config import get_settings
from app.models.collection_needs import CollectionNeedToolItem, CollectionNeedsToolResponse
from app.agents.collection_planning_agent import COLLECTION_PLANNING_SYSTEM_PROMPT, _format_prompt_data
from app.tools.collection_needs import (
    COLLECTION_NEEDS_PATH,
    INTERNAL_AUTH_HEADER,
    MAX_TRANSIENT_RETRIES,
    fetch_collection_needs,
    get_collection_needs,
)

TEST_INTERNAL_SERVICE_KEY = "test-only-c2-internal-key"


@pytest.fixture(autouse=True)
def configure_test_internal_key(monkeypatch):
    monkeypatch.setenv("INTERNAL_SERVICE_KEY", TEST_INTERNAL_SERVICE_KEY)
    get_settings.cache_clear()
    yield
    get_settings.cache_clear()


def _item(target_type="Bin", item_id=None):
    item_id = item_id or uuid4()
    item = {
        "id": str(item_id),
        "targetType": target_type,
        "wasteReportId": None if target_type == "Bin" else str(uuid4()),
        "wasteBinId": str(uuid4()) if target_type == "Bin" else None,
        "collectionReason": "FullOrBlockedBin" if target_type == "Bin" else "VerifiedReport",
        "latitude": 6.9271,
        "longitude": 79.8612,
        "addressText": "Pettah, Colombo",
        "wasteTypes": ["General", "Organic"],
        "urgency": "High",
        "triggerDate": "2026-09-23T10:00:00Z",
        "binTelemetry": None,
    }
    if target_type == "Bin":
        item["binTelemetry"] = {
            "binCode": "BIN-AI-001",
            "capacityLiters": 660,
            "latestFillLevelPercent": 100,
            "latestCondition": "Good",
            "observationAgeHours": 1.5,
        }
    return item


def _response(items=None, page=1, page_size=20, total_count=None):
    items = [_item()] if items is None else items
    total_count = len(items) if total_count is None else total_count
    return {
        "items": items,
        "page": page,
        "pageSize": page_size,
        "totalCount": total_count,
        "totalPages": (total_count + page_size - 1) // page_size,
    }


def test_display_metadata_is_parsed_without_replacing_canonical_need_ids():
    report_payload = _item("Report")
    report_payload["wasteReportId"] = report_payload["id"]
    report_payload["reportReference"] = report_payload["id"].replace("-", "")[:8].upper()
    bin_payload = _item("Bin")
    bin_payload["wasteBinId"] = bin_payload["id"]
    bin_payload["binCode"] = "BIN-COL-0042"
    report = CollectionNeedToolItem.model_validate(report_payload)
    bin_need = CollectionNeedToolItem.model_validate(bin_payload)
    context = _format_prompt_data([report, bin_need])
    assert report.id == UUID(report_payload["id"])
    assert bin_need.id == UUID(bin_payload["id"])
    assert f'"reportReference": "{report_payload["reportReference"]}"' in context
    assert '"binCode": "BIN-COL-0042"' in context
    assert str(report.id) in context
    assert str(bin_need.id) in context
    assert "full needId values unchanged" in COLLECTION_PLANNING_SYSTEM_PROMPT


def _http_response(status, payload=None):
    response = MagicMock(spec=httpx.Response)
    response.status_code = status
    response.json.return_value = payload
    return response


class TestCollectionNeedsTool:
    def test_tool_metadata_and_no_database_dependencies(self):
        assert get_collection_needs.name == "get_collection_needs"
        assert "paginated" in get_collection_needs.description.lower()
        for module in ["psycopg2", "asyncpg", "sqlalchemy", "tortoise", "ormar", "peewee"]:
            assert module not in sys.modules

    def test_schema_does_not_expose_sensitive_or_internal_fields(self):
        fields = set(CollectionNeedToolItem.model_fields)
        forbidden = {
            "citizen_id", "officer_id", "description", "notes", "attachment_urls",
            "task_id", "vehicle_id", "driver_id", "route", "audit_history",
        }
        assert not fields.intersection(forbidden)

    def test_fetch_serializes_only_approved_query_parameters_and_parses_page(self):
        client = MagicMock(spec=httpx.Client)
        client.get.return_value = _http_response(200, _response(page=2, page_size=10, total_count=25))

        result = fetch_collection_needs(
            target_type="Bin",
            collection_reason="FullOrBlockedBin",
            target_date="2026-09-23",
            page=2,
            page_size=10,
            client=client,
        )

        assert isinstance(result, CollectionNeedsToolResponse)
        assert result.total_count == 25
        assert result.total_pages == 3
        assert result.items[0].bin_telemetry.bin_code == "BIN-AI-001"
        settings = get_settings()
        client.get.assert_called_once_with(
            f"{settings.ASPNET_API_BASE_URL.rstrip('/')}{COLLECTION_NEEDS_PATH}",
            params={
                "page": 2,
                "pageSize": 10,
                "targetType": "Bin",
                "collectionReason": "FullOrBlockedBin",
                "targetDate": "2026-09-23",
            },
            headers={INTERNAL_AUTH_HEADER: TEST_INTERNAL_SERVICE_KEY, "Accept": "application/json"},
        )

    def test_empty_queue_is_a_valid_empty_result_not_a_failure(self):
        client = MagicMock(spec=httpx.Client)
        client.get.return_value = _http_response(200, _response(items=[], total_count=0))

        result = fetch_collection_needs(client=client)

        assert result.items == []
        assert result.total_count == 0
        assert result.total_pages == 0

    @pytest.mark.parametrize(
        "kwargs",
        [
            {"page": 0}, {"page_size": 51}, {"target_type": "Citizen"},
            {"collection_reason": "Manual"}, {"target_date": "23-09-2026"},
            {"target_date": "2026-09-23T00:00:00Z"},
        ],
    )
    def test_invalid_query_is_rejected_before_any_http_request(self, kwargs):
        client = MagicMock(spec=httpx.Client)
        with pytest.raises(ValueError):
            fetch_collection_needs(client=client, **kwargs)
        client.get.assert_not_called()

    def test_report_and_bin_references_are_validated(self):
        malformed = _item("Report")
        malformed["binTelemetry"] = {"binCode": "leak", "capacityLiters": 1}
        client = MagicMock(spec=httpx.Client)
        client.get.return_value = _http_response(200, _response(items=[malformed]))

        with pytest.raises(Exception):
            fetch_collection_needs(client=client)

    def test_unknown_or_sensitive_response_fields_are_rejected(self):
        item = _item()
        item["officerId"] = str(uuid4())
        client = MagicMock(spec=httpx.Client)
        client.get.return_value = _http_response(200, _response(items=[item]))

        with pytest.raises(Exception):
            fetch_collection_needs(client=client)

    def test_missing_required_response_fields_are_rejected(self):
        item = _item()
        del item["wasteTypes"]
        client = MagicMock(spec=httpx.Client)
        client.get.return_value = _http_response(200, _response(items=[item]))

        with pytest.raises(Exception):
            fetch_collection_needs(client=client)

    @pytest.mark.parametrize("status", [400, 401, 403])
    def test_non_transient_client_errors_do_not_retry_or_leak_key(self, status):
        client = MagicMock(spec=httpx.Client)
        client.get.return_value = _http_response(status)

        with pytest.raises(RuntimeError) as exc_info:
            fetch_collection_needs(client=client)

        assert str(status) in str(exc_info.value)
        assert TEST_INTERNAL_SERVICE_KEY not in str(exc_info.value)
        assert client.get.call_count == 1

    @patch("app.tools.collection_needs.time.sleep", return_value=None)
    def test_transient_5xx_retries_once_then_succeeds(self, mock_sleep):
        client = MagicMock(spec=httpx.Client)
        client.get.side_effect = [_http_response(503), _http_response(200, _response())]

        result = fetch_collection_needs(client=client)

        assert result.total_count == 1
        assert client.get.call_count == 2
        mock_sleep.assert_called_once()

    @patch("app.tools.collection_needs.time.sleep", return_value=None)
    def test_persistent_5xx_retries_only_once_then_fails(self, mock_sleep):
        client = MagicMock(spec=httpx.Client)
        client.get.return_value = _http_response(500)

        with pytest.raises(RuntimeError) as exc_info:
            fetch_collection_needs(client=client)

        assert "500" in str(exc_info.value)
        assert TEST_INTERNAL_SERVICE_KEY not in str(exc_info.value)
        assert client.get.call_count == 1 + MAX_TRANSIENT_RETRIES
        mock_sleep.assert_called_once()

    @patch("app.tools.collection_needs.time.sleep", return_value=None)
    def test_timeout_retries_once_and_is_sanitized(self, mock_sleep):
        client = MagicMock(spec=httpx.Client)
        client.get.side_effect = httpx.ReadTimeout("timeout")

        with pytest.raises(RuntimeError) as exc_info:
            fetch_collection_needs(client=client)

        assert "ReadTimeout" in str(exc_info.value)
        assert TEST_INTERNAL_SERVICE_KEY not in str(exc_info.value)
        assert client.get.call_count == 1 + MAX_TRANSIENT_RETRIES
        mock_sleep.assert_called_once()

    def test_tool_direct_invocation_uses_the_same_validated_function(self):
        client = MagicMock(spec=httpx.Client)
        response = _http_response(200, _response(items=[_item("Report")]))
        client.get.return_value = response

        with patch("app.tools.collection_needs.httpx.Client", return_value=client):
            result = get_collection_needs.invoke({"target_type": "Report", "page": 1, "page_size": 20})

        assert result.items[0].target_type == "Report"
