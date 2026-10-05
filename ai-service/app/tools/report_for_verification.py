"""Exact-ID, read-only ASP.NET report tool for pre-verification C1 analysis."""

import logging
from uuid import UUID

import httpx
from langchain_core.tools import tool

from app.core.config import get_settings
from app.models.reporting import WasteReportForVerificationItem

logger = logging.getLogger(__name__)

EXACT_REPORTS_PATH = "/api/v1/internal/ai-tools/waste-reports/for-verification"
INTERNAL_AUTH_HEADER = "X-Internal-Service-Key"


class ReportForVerificationToolError(RuntimeError):
    """Safe deterministic failure while reading the authoritative report."""


class ReportForVerificationNotFound(ReportForVerificationToolError):
    pass


class ReportForVerificationIneligible(ReportForVerificationToolError):
    pass


class ReportForVerificationAuthError(ReportForVerificationToolError):
    pass


def fetch_report_for_verification(
    report_id: UUID,
    client: httpx.Client | None = None,
) -> WasteReportForVerificationItem:
    """Fetch exactly one Submitted/UnderReview report; never search or fall back."""
    if not isinstance(report_id, UUID):
        raise ValueError("report_id must be an authoritative UUID.")

    settings = get_settings()
    if not settings.INTERNAL_SERVICE_KEY or not settings.INTERNAL_SERVICE_KEY.strip():
        raise ReportForVerificationAuthError("Internal service authentication key is not configured.")

    url = f"{settings.ASPNET_API_BASE_URL.rstrip('/')}{EXACT_REPORTS_PATH}/{report_id}"
    headers = {INTERNAL_AUTH_HEADER: settings.INTERNAL_SERVICE_KEY, "Accept": "application/json"}
    owns_client = client is None
    http_client = client or httpx.Client(timeout=settings.TOOL_HTTP_TIMEOUT)
    try:
        try:
            response = http_client.get(url, headers=headers)
        except httpx.RequestError as exception:
            logger.warning("Exact-report backend request failed: %s", type(exception).__name__)
            raise ReportForVerificationToolError("Exact-report backend request failed.") from None

        if response.status_code == 404:
            raise ReportForVerificationNotFound("The triggering waste report was not found.")
        if response.status_code == 409:
            raise ReportForVerificationIneligible("The triggering waste report is not eligible for pre-verification analysis.")
        if response.status_code in (401, 403):
            raise ReportForVerificationAuthError("Internal service authentication was rejected by ASP.NET.")
        if response.status_code != 200:
            logger.warning("Exact-report backend returned HTTP %d", response.status_code)
            raise ReportForVerificationToolError(f"Exact-report backend returned HTTP {response.status_code}.")

        try:
            report = WasteReportForVerificationItem.model_validate(response.json())
        except (ValueError, TypeError):
            raise ReportForVerificationToolError("Exact-report backend returned an invalid response.") from None
        if report.id != report_id:
            raise ReportForVerificationToolError("Exact-report backend returned a different report ID.")
        return report
    finally:
        if owns_client:
            http_client.close()


@tool("get_report_for_verification")
def get_report_for_verification(report_id: UUID) -> WasteReportForVerificationItem:
    """Read one authoritative report by UUID for advisory analysis before human verification."""
    return fetch_report_for_verification(report_id)
