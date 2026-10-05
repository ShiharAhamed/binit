from uuid import uuid4

import httpx
import pytest

from app.core.config import get_settings
from app.tools.report_for_verification import (
    EXACT_REPORTS_PATH,
    ReportForVerificationAuthError,
    ReportForVerificationIneligible,
    ReportForVerificationNotFound,
    ReportForVerificationToolError,
    fetch_report_for_verification,
)


KEY = "test-only-exact-report-key"


@pytest.fixture(autouse=True)
def internal_key(monkeypatch):
    monkeypatch.setenv("INTERNAL_SERVICE_KEY", KEY)
    get_settings.cache_clear()
    yield
    get_settings.cache_clear()


def _body(report_id, status="Submitted"):
    return {
        "id": str(report_id),
        "reportReference": report_id.hex[:8].upper(),
        "description": "Untrusted citizen evidence",
        "wasteType": "Mixed",
        "latitude": 6.9,
        "longitude": 79.8,
        "addressText": "A street",
        "status": status,
        "createdAt": "2026-10-01T10:00:00Z",
        "attachmentCount": 1,
    }


@pytest.mark.parametrize("status", ["Submitted", "UnderReview"])
def test_fetches_exact_uuid_and_internal_key(status):
    report_id = uuid4()
    requests = []

    def respond(request):
        requests.append(request)
        return httpx.Response(200, json=_body(report_id, status))

    with httpx.Client(transport=httpx.MockTransport(respond)) as client:
        result = fetch_report_for_verification(report_id, client)

    assert result.id == report_id
    assert result.status == status
    assert len(requests) == 1
    assert requests[0].url.path == f"{EXACT_REPORTS_PATH}/{report_id}"
    assert requests[0].headers["X-Internal-Service-Key"] == KEY


@pytest.mark.parametrize(
    ("status", "error"),
    [(404, ReportForVerificationNotFound), (409, ReportForVerificationIneligible),
     (401, ReportForVerificationAuthError), (500, ReportForVerificationToolError)],
)
def test_http_failures_are_deterministic_and_never_fall_back(status, error):
    calls = []

    def respond(request):
        calls.append(request)
        return httpx.Response(status, text="secret response must not leak")

    with httpx.Client(transport=httpx.MockTransport(respond)) as client:
        with pytest.raises(error) as raised:
            fetch_report_for_verification(uuid4(), client)
    assert len(calls) == 1
    assert "secret response" not in str(raised.value)


@pytest.mark.parametrize("body", [{"bad": "body"}, "not-json"])
def test_malformed_backend_body_is_rejected(body):
    response = httpx.Response(200, json=body) if isinstance(body, dict) else httpx.Response(200, text=body)
    with httpx.Client(transport=httpx.MockTransport(lambda _: response)) as client:
        with pytest.raises(ReportForVerificationToolError):
            fetch_report_for_verification(uuid4(), client)


def test_wrong_report_id_and_sensitive_extra_fields_are_rejected():
    target = uuid4()
    wrong = _body(uuid4())
    with httpx.Client(transport=httpx.MockTransport(lambda _: httpx.Response(200, json=wrong))) as client:
        with pytest.raises(ReportForVerificationToolError):
            fetch_report_for_verification(target, client)

    unsafe = _body(target)
    unsafe["citizenId"] = str(uuid4())
    with httpx.Client(transport=httpx.MockTransport(lambda _: httpx.Response(200, json=unsafe))) as client:
        with pytest.raises(ReportForVerificationToolError):
            fetch_report_for_verification(target, client)


def test_unconfigured_key_stops_before_http(monkeypatch):
    monkeypatch.setenv("INTERNAL_SERVICE_KEY", "")
    get_settings.cache_clear()
    calls = []
    with httpx.Client(transport=httpx.MockTransport(lambda request: calls.append(request))) as client:
        with pytest.raises(ReportForVerificationAuthError):
            fetch_report_for_verification(uuid4(), client)
    assert calls == []
