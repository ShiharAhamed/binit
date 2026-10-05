import sys
from datetime import datetime, timezone
from unittest.mock import MagicMock, patch
from uuid import UUID, uuid4

import httpx
import pytest

from app.core.config import get_settings
from app.models.reporting import VerifiedWasteReportItem, VerifiedWasteReportsResponse
from app.agents.waste_analysis_agent import WASTE_ANALYSIS_SYSTEM_PROMPT, _format_prompt_data
from app.tools.verified_waste_reports import (
    INTERNAL_AUTH_HEADER,
    MAX_TRANSIENT_RETRIES,
    VERIFIED_REPORTS_PATH,
    fetch_verified_waste_reports,
    get_verified_waste_reports,
)

# Test-only fake key injected strictly at test runtime (never used in dev/prod)
TEST_INTERNAL_SERVICE_KEY = "test-only-fake-internal-service-key-98765!"


@pytest.fixture(autouse=True)
def configure_test_internal_key(monkeypatch):
    """Ensure tests run with a test-only fake secret by default."""
    monkeypatch.setenv("INTERNAL_SERVICE_KEY", TEST_INTERNAL_SERVICE_KEY)
    get_settings.cache_clear()
    yield
    get_settings.cache_clear()


def _create_mock_backend_response(items=None, total_count=1, page=1, page_size=20):
    """Helper to generate mock ASP.NET Core camelCase PagedResult JSON."""
    if items is None:
        items = [
            {
                "id": str(uuid4()),
                "description": "Verified organic waste on sidewalk",
                "wasteType": "Organic",
                "latitude": 6.9271,
                "longitude": 79.8612,
                "addressText": "Main Street, Colombo",
                "status": "Verified",
                "createdAt": "2026-09-17T10:00:00Z",
                "verifiedAt": "2026-09-17T10:30:00Z",
                "attachmentCount": 2,
            }
        ]
    return {
        "items": items,
        "totalCount": total_count,
        "page": page,
        "pageSize": page_size,
        "totalPages": (total_count + page_size - 1) // page_size if page_size > 0 else 1,
    }


class TestVerifiedWasteReportsTool:
    """Tests for the get_verified_waste_reports allow-listed AI tool."""

    def test_display_reference_is_parsed_for_prose_while_full_id_is_retained(self):
        payload = _create_mock_backend_response()["items"][0]
        payload["reportReference"] = payload["id"].replace("-", "")[:8].upper()
        item = VerifiedWasteReportItem.model_validate(payload)
        context = _format_prompt_data([item])
        assert item.id == UUID(payload["id"])
        assert f'"reportReference": "{payload["reportReference"]}"' in context
        assert f'"reportId": "{payload["id"]}"' in context
        assert "Report {reportReference}" in WASTE_ANALYSIS_SYSTEM_PROMPT

    def test_tool_metadata(self):
        """Verify tool naming, LangChain tool registration, and parameter descriptions."""
        assert get_verified_waste_reports.name == "get_verified_waste_reports"
        assert "Verified" in get_verified_waste_reports.description
        assert "citizen" in get_verified_waste_reports.description.lower()

    def test_no_database_or_orm_dependencies(self):
        """Verify the tool and model modules do not import or depend on any database engines."""
        forbidden_modules = ["psycopg2", "asyncpg", "sqlalchemy", "tortoise", "ormar", "peewee"]
        for mod in forbidden_modules:
            assert mod not in sys.modules, f"Forbidden database module '{mod}' must not be imported!"

    def test_data_minimization_contract(self):
        """Verify VerifiedWasteReportItem schema contains NO citizen PII, officer IDs, or storage keys."""
        field_names = set(VerifiedWasteReportItem.model_fields.keys())
        forbidden_fields = {
            "citizen_id", "citizenId",
            "citizen_name", "citizenName",
            "email", "phone", "phone_number",
            "officer_id", "officerId",
            "storage_key", "storageKey",
            "blob_url", "photo_url", "photoUrls",
            "history_notes", "notes",
        }
        intersection = field_names.intersection(forbidden_fields)
        assert len(intersection) == 0, f"Leaked sensitive fields found: {intersection}"

    def test_missing_internal_service_key_fails_safely_before_request(self, monkeypatch):
        """Verify when INTERNAL_SERVICE_KEY is missing/empty, the tool fails safely without making an HTTP request."""
        monkeypatch.setenv("INTERNAL_SERVICE_KEY", "")
        get_settings.cache_clear()

        mock_client = MagicMock(spec=httpx.Client)

        with pytest.raises(RuntimeError) as exc_info:
            fetch_verified_waste_reports(page=1, page_size=20, client=mock_client)

        error_message = str(exc_info.value)
        assert "INTERNAL_SERVICE_KEY" in error_message
        assert "not configured" in error_message.lower()
        # Verify no network request was attempted
        assert mock_client.get.call_count == 0

    def test_whitespace_internal_service_key_fails_safely_before_request(self, monkeypatch):
        """Verify when INTERNAL_SERVICE_KEY is whitespace only, the tool fails safely without making an HTTP request."""
        monkeypatch.setenv("INTERNAL_SERVICE_KEY", "   ")
        get_settings.cache_clear()

        mock_client = MagicMock(spec=httpx.Client)

        with pytest.raises(RuntimeError) as exc_info:
            fetch_verified_waste_reports(page=1, page_size=20, client=mock_client)

        error_message = str(exc_info.value)
        assert "INTERNAL_SERVICE_KEY" in error_message
        assert "not configured" in error_message.lower()
        assert mock_client.get.call_count == 0

    @pytest.mark.parametrize("invalid_page", [0, -1, -100, "one"])
    def test_validation_page_bounds(self, invalid_page):
        """Verify page must be an integer >= 1."""
        with pytest.raises(ValueError, match="page must be an integer greater than or equal to 1"):
            fetch_verified_waste_reports(page=invalid_page, page_size=20)

    @pytest.mark.parametrize("invalid_page_size", [0, -1, 51, 100, "ten"])
    def test_validation_page_size_bounds(self, invalid_page_size):
        """Verify page_size must be an integer between 1 and 50."""
        with pytest.raises(ValueError, match="page_size must be an integer between 1 and 50"):
            fetch_verified_waste_reports(page=1, page_size=invalid_page_size)

    def test_successful_fetch_and_model_mapping(self):
        """Verify successful response parsing, query parameter encoding, and auth header injection."""
        settings = get_settings()
        report_id = uuid4()
        payload = _create_mock_backend_response(
            items=[
                {
                    "id": str(report_id),
                    "description": "Plastic bin overflow",
                    "wasteType": "Plastic",
                    "latitude": 6.9147,
                    "longitude": 79.8778,
                    "addressText": "Galle Road, Colombo 03",
                    "status": "Verified",
                    "createdAt": "2026-09-17T11:00:00Z",
                    "verifiedAt": "2026-09-17T11:15:00Z",
                    "attachmentCount": 3,
                }
            ],
            total_count=1,
            page=1,
            page_size=10,
        )

        mock_response = MagicMock(spec=httpx.Response)
        mock_response.status_code = 200
        mock_response.json.return_value = payload

        mock_client = MagicMock(spec=httpx.Client)
        mock_client.get.return_value = mock_response

        result = fetch_verified_waste_reports(page=1, page_size=10, client=mock_client)

        # Verify client was called with correct URL, params, and auth header
        expected_url = f"{settings.ASPNET_API_BASE_URL.rstrip('/')}{VERIFIED_REPORTS_PATH}"
        mock_client.get.assert_called_once_with(
            expected_url,
            params={"page": 1, "pageSize": 10},
            headers={
                INTERNAL_AUTH_HEADER: TEST_INTERNAL_SERVICE_KEY,
                "Accept": "application/json",
            },
        )

        # Verify parsed Pydantic object
        assert isinstance(result, VerifiedWasteReportsResponse)
        assert result.total_count == 1
        assert result.page == 1
        assert result.page_size == 10
        assert len(result.items) == 1

        item = result.items[0]
        assert item.id == report_id
        assert item.description == "Plastic bin overflow"
        assert item.waste_type == "Plastic"
        assert item.latitude == 6.9147
        assert item.longitude == 79.8778
        assert item.address_text == "Galle Road, Colombo 03"
        assert item.status == "Verified"
        assert item.attachment_count == 3
        assert item.verified_at is not None

    def test_auth_rejection_401_no_retry_and_no_leak(self):
        """Verify 401 Unauthorized causes immediate failure without retrying and without leaking secrets."""
        mock_response = MagicMock(spec=httpx.Response)
        mock_response.status_code = 401
        mock_response.text = '{"title":"Unauthorized","status":401}'

        mock_client = MagicMock(spec=httpx.Client)
        mock_client.get.return_value = mock_response

        with pytest.raises(RuntimeError) as exc_info:
            fetch_verified_waste_reports(page=1, page_size=20, client=mock_client)

        error_message = str(exc_info.value)
        assert "401" in error_message
        assert TEST_INTERNAL_SERVICE_KEY not in error_message
        # Verify exactly 1 attempt was made (0 retries)
        assert mock_client.get.call_count == 1

    def test_auth_rejection_403_no_retry_and_no_leak(self):
        """Verify 403 Forbidden causes immediate failure without retrying."""
        mock_response = MagicMock(spec=httpx.Response)
        mock_response.status_code = 403
        mock_response.text = '{"title":"Forbidden","status":403}'

        mock_client = MagicMock(spec=httpx.Client)
        mock_client.get.return_value = mock_response

        with pytest.raises(RuntimeError) as exc_info:
            fetch_verified_waste_reports(page=1, page_size=20, client=mock_client)

        error_message = str(exc_info.value)
        assert "403" in error_message
        assert TEST_INTERNAL_SERVICE_KEY not in error_message
        assert mock_client.get.call_count == 1

    def test_client_error_400_no_retry(self):
        """Verify 400 Bad Request causes immediate failure without retrying."""
        mock_response = MagicMock(spec=httpx.Response)
        mock_response.status_code = 400
        mock_response.text = '{"title":"Bad Request","status":400}'

        mock_client = MagicMock(spec=httpx.Client)
        mock_client.get.return_value = mock_response

        with pytest.raises(RuntimeError) as exc_info:
            fetch_verified_waste_reports(page=1, page_size=20, client=mock_client)

        assert "400" in str(exc_info.value)
        assert mock_client.get.call_count == 1

    @patch("app.tools.verified_waste_reports.time.sleep", return_value=None)
    def test_transient_5xx_retry_success(self, mock_sleep):
        """Verify transient 503 error succeeds after a single retry."""
        payload = _create_mock_backend_response()

        err_response = MagicMock(spec=httpx.Response)
        err_response.status_code = 503
        err_response.text = "Service Temporarily Unavailable"

        ok_response = MagicMock(spec=httpx.Response)
        ok_response.status_code = 200
        ok_response.json.return_value = payload

        mock_client = MagicMock(spec=httpx.Client)
        mock_client.get.side_effect = [err_response, ok_response]

        result = fetch_verified_waste_reports(page=1, page_size=20, client=mock_client)

        assert result.total_count == 1
        assert mock_client.get.call_count == 2
        mock_sleep.assert_called_once()

    @patch("app.tools.verified_waste_reports.time.sleep", return_value=None)
    def test_transient_5xx_retry_exhausted(self, mock_sleep):
        """Verify persistent 500 error raises RuntimeError after bounded single retry."""
        err_response = MagicMock(spec=httpx.Response)
        err_response.status_code = 500
        err_response.text = "Internal Server Error"

        mock_client = MagicMock(spec=httpx.Client)
        mock_client.get.return_value = err_response

        with pytest.raises(RuntimeError) as exc_info:
            fetch_verified_waste_reports(page=1, page_size=20, client=mock_client)

        assert "500" in str(exc_info.value)
        assert mock_client.get.call_count == 1 + MAX_TRANSIENT_RETRIES
        mock_sleep.assert_called_once()

    @patch("app.tools.verified_waste_reports.time.sleep", return_value=None)
    def test_transient_network_error_retry_success(self, mock_sleep):
        """Verify transient ConnectError recovers on retry."""
        payload = _create_mock_backend_response()

        ok_response = MagicMock(spec=httpx.Response)
        ok_response.status_code = 200
        ok_response.json.return_value = payload

        mock_client = MagicMock(spec=httpx.Client)
        mock_client.get.side_effect = [
            httpx.ConnectError("Connection refused"),
            ok_response,
        ]

        result = fetch_verified_waste_reports(page=1, page_size=20, client=mock_client)

        assert result.total_count == 1
        assert mock_client.get.call_count == 2
        mock_sleep.assert_called_once()

    @patch("app.tools.verified_waste_reports.time.sleep", return_value=None)
    def test_transient_network_error_retry_exhausted(self, mock_sleep):
        """Verify persistent ConnectTimeout raises RuntimeError after retry."""
        mock_client = MagicMock(spec=httpx.Client)
        mock_client.get.side_effect = httpx.ConnectTimeout("Connection timed out")

        with pytest.raises(RuntimeError) as exc_info:
            fetch_verified_waste_reports(page=1, page_size=20, client=mock_client)

        assert "ConnectTimeout" in str(exc_info.value)
        assert mock_client.get.call_count == 1 + MAX_TRANSIENT_RETRIES
        mock_sleep.assert_called_once()

    def test_langchain_tool_direct_invocation(self):
        """Verify tool direct execution via invoke and .func without requiring an LLM."""
        payload = _create_mock_backend_response(total_count=5, page=2, page_size=15)

        mock_response = MagicMock(spec=httpx.Response)
        mock_response.status_code = 200
        mock_response.json.return_value = payload

        with patch("app.tools.verified_waste_reports.httpx.Client") as mock_client_cls:
            mock_client_instance = MagicMock()
            mock_client_instance.get.return_value = mock_response
            mock_client_cls.return_value = mock_client_instance

            # Invocation via LangChain .invoke()
            res_invoke = get_verified_waste_reports.invoke({"page": 2, "page_size": 15})
            assert isinstance(res_invoke, VerifiedWasteReportsResponse)
            assert res_invoke.page == 2
            assert res_invoke.page_size == 15
            assert res_invoke.total_count == 5

            # Invocation via direct .func()
            res_func = get_verified_waste_reports.func(page=2, page_size=15)
            assert isinstance(res_func, VerifiedWasteReportsResponse)
            assert res_func.page == 2
