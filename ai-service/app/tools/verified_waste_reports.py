import logging
import time
from typing import Optional

import httpx
from langchain_core.tools import tool

from app.core.config import get_settings
from app.models.reporting import VerifiedWasteReportsResponse

logger = logging.getLogger(__name__)

# Constants for internal communication
INTERNAL_AUTH_HEADER = "X-Internal-Service-Key"
VERIFIED_REPORTS_PATH = "/api/v1/internal/ai-tools/waste-reports/verified"
MAX_TRANSIENT_RETRIES = 1
INITIAL_RETRY_BACKOFF_SECONDS = 0.5


def fetch_verified_waste_reports(
    page: int = 1,
    page_size: int = 20,
    client: Optional[httpx.Client] = None,
) -> VerifiedWasteReportsResponse:
    """Fetch verified waste reports from the authoritative ASP.NET Core backend.

    This function enforces strict argument validation, sends internal service
    authentication, and handles transient errors with a bounded single-retry policy.
    Zero side effects are produced on the backend.

    Args:
        page: Page number (must be >= 1).
        page_size: Number of records per page (must be 1 <= page_size <= 50).
        client: Optional custom httpx.Client for dependency injection or testing.

    Returns:
        VerifiedWasteReportsResponse containing safe, minimal verified report items.

    Raises:
        ValueError: If input bounds are violated.
        RuntimeError: If authentication fails, backend rejects, or network fails after retry.
    """
    if not isinstance(page, int) or page < 1:
        raise ValueError("page must be an integer greater than or equal to 1.")
    if not isinstance(page_size, int) or page_size < 1 or page_size > 50:
        raise ValueError("page_size must be an integer between 1 and 50.")

    settings = get_settings()
    if not settings.INTERNAL_SERVICE_KEY or not settings.INTERNAL_SERVICE_KEY.strip():
        logger.error("Internal service key is not configured in settings/environment.")
        raise RuntimeError(
            "Internal service authentication key is not configured. "
            "Please set the INTERNAL_SERVICE_KEY environment variable."
        )

    base_url = settings.ASPNET_API_BASE_URL.rstrip("/")
    url = f"{base_url}{VERIFIED_REPORTS_PATH}"
    headers = {
        INTERNAL_AUTH_HEADER: settings.INTERNAL_SERVICE_KEY,
        "Accept": "application/json",
    }
    params = {
        "page": page,
        "pageSize": page_size,
    }

    should_close_client = client is None
    http_client = client or httpx.Client(timeout=settings.TOOL_HTTP_TIMEOUT)

    try:
        attempts = 0
        max_attempts = 1 + MAX_TRANSIENT_RETRIES

        while attempts < max_attempts:
            attempts += 1
            try:
                response = http_client.get(url, params=params, headers=headers)

                # Explicitly handle auth rejections - NEVER retry 401/403
                if response.status_code in (401, 403):
                    logger.error(
                        "Internal service authentication rejected by backend (status: %d)",
                        response.status_code,
                    )
                    raise RuntimeError(
                        f"Authentication failed: Backend rejected internal service authorization (HTTP {response.status_code})."
                    )

                # Explicitly handle other client errors (400, 404, etc.) - NEVER retry 4xx
                if 400 <= response.status_code < 500:
                    logger.warning(
                        "Backend rejected request with client error (status: %d)",
                        response.status_code,
                    )
                    raise RuntimeError(
                        f"Backend rejected request (HTTP {response.status_code}): {response.text}"
                    )

                # Server errors (5xx) - transient, eligible for bounded single retry
                if response.status_code >= 500:
                    if attempts < max_attempts:
                        logger.warning(
                            "Transient 5xx error from backend (status: %d). Retrying attempt %d/%d...",
                            response.status_code,
                            attempts,
                            max_attempts,
                        )
                        time.sleep(INITIAL_RETRY_BACKOFF_SECONDS)
                        continue
                    else:
                        raise RuntimeError(
                            f"Backend error (HTTP {response.status_code}) after retry: {response.text}"
                        )

                # Successful 2xx response
                data = response.json()
                return VerifiedWasteReportsResponse.model_validate(data)

            except (httpx.ConnectError, httpx.ConnectTimeout, httpx.ReadTimeout, httpx.NetworkError) as ex:
                if attempts < max_attempts:
                    logger.warning(
                        "Transient network error connecting to backend: %s. Retrying attempt %d/%d...",
                        type(ex).__name__,
                        attempts,
                        max_attempts,
                    )
                    time.sleep(INITIAL_RETRY_BACKOFF_SECONDS)
                    continue
                else:
                    logger.error("Failed to connect to backend after retry: %s", type(ex).__name__)
                    raise RuntimeError(
                        f"Failed to connect to SmartWaste backend after retry: {type(ex).__name__}"
                    ) from None

    finally:
        if should_close_client:
            http_client.close()


@tool("get_verified_waste_reports")
def get_verified_waste_reports(page: int = 1, page_size: int = 20) -> VerifiedWasteReportsResponse:
    """Retrieve a safe, paginated list of authoritative Verified waste reports from SmartWaste backend.

    Only reports whose authoritative status in ASP.NET Core is 'Verified' are returned.
    Citizen PII, officer identifiers, private storage keys, and internal history notes
    are excluded by design.

    Args:
        page: Page number (minimum 1, default 1).
        page_size: Number of records per page (between 1 and 50, default 20).

    Returns:
        VerifiedWasteReportsResponse with paginated verified waste reports.
    """
    return fetch_verified_waste_reports(page=page, page_size=page_size)
