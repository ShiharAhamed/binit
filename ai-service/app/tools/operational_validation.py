import logging
import time
from typing import Optional, Sequence, Union
from uuid import UUID

import httpx
from langchain_core.tools import tool

from app.core.config import get_settings
from app.models.validation_operations import OperationalValidationContextResponse

logger = logging.getLogger(__name__)

# Constants for internal communication
INTERNAL_AUTH_HEADER = "X-Internal-Service-Key"
OPERATIONAL_VALIDATION_CONTEXT_PATH = "/api/v1/internal/ai-tools/operational-validation-context"
MAX_TRANSIENT_RETRIES = 1
INITIAL_RETRY_BACKOFF_SECONDS = 0.5


def fetch_operational_validation_context(
    task_ids: Optional[Sequence[Union[UUID, str]]] = None,
    driver_ids: Optional[Sequence[Union[UUID, str]]] = None,
    vehicle_ids: Optional[Sequence[Union[UUID, str]]] = None,
    client: Optional[httpx.Client] = None,
) -> OperationalValidationContextResponse:
    """Fetch fresh authoritative operational state for specific tasks, drivers, and vehicles from ASP.NET Core.

    Used by the C4 Validation & Operations Agent to verify referenced resources against fresh backend state.

    Args:
        task_ids: Specific collection task IDs to look up (max 100, no duplicates).
        driver_ids: Specific driver IDs to look up (max 50, no duplicates).
        vehicle_ids: Specific vehicle IDs to look up (max 50, no duplicates).
        client: Optional custom httpx.Client for dependency injection or testing.

    Returns:
        OperationalValidationContextResponse containing fresh tasks, drivers, and vehicles.

    Raises:
        ValueError: If input bounds or duplicate rules are violated.
        RuntimeError: If authentication fails, backend rejects, or network fails after retry.
    """
    cleaned_task_ids = []
    if task_ids:
        for tid in task_ids:
            try:
                cleaned_task_ids.append(str(UUID(str(tid))))
            except (ValueError, AttributeError, TypeError):
                raise ValueError(f"Task ID '{tid}' must be a valid UUID.")
        if len(cleaned_task_ids) != len(set(cleaned_task_ids)):
            raise ValueError("Task IDs list must not contain duplicates.")
        if len(cleaned_task_ids) > 100:
            raise ValueError("Cannot request more than 100 task IDs for validation.")

    cleaned_driver_ids = []
    if driver_ids:
        for did in driver_ids:
            try:
                cleaned_driver_ids.append(str(UUID(str(did))))
            except (ValueError, AttributeError, TypeError):
                raise ValueError(f"Driver ID '{did}' must be a valid UUID.")
        if len(cleaned_driver_ids) != len(set(cleaned_driver_ids)):
            raise ValueError("Driver IDs list must not contain duplicates.")
        if len(cleaned_driver_ids) > 50:
            raise ValueError("Cannot request more than 50 driver IDs for validation.")

    cleaned_vehicle_ids = []
    if vehicle_ids:
        for vid in vehicle_ids:
            try:
                cleaned_vehicle_ids.append(str(UUID(str(vid))))
            except (ValueError, AttributeError, TypeError):
                raise ValueError(f"Vehicle ID '{vid}' must be a valid UUID.")
        if len(cleaned_vehicle_ids) != len(set(cleaned_vehicle_ids)):
            raise ValueError("Vehicle IDs list must not contain duplicates.")
        if len(cleaned_vehicle_ids) > 50:
            raise ValueError("Cannot request more than 50 vehicle IDs for validation.")

    settings = get_settings()
    if not settings.INTERNAL_SERVICE_KEY or not settings.INTERNAL_SERVICE_KEY.strip():
        logger.error("Internal service key is not configured in settings/environment.")
        raise RuntimeError(
            "Internal service authentication key is not configured. "
            "Please set the INTERNAL_SERVICE_KEY environment variable."
        )

    base_url = settings.ASPNET_API_BASE_URL.rstrip("/")
    url = f"{base_url}{OPERATIONAL_VALIDATION_CONTEXT_PATH}"
    headers = {
        INTERNAL_AUTH_HEADER: settings.INTERNAL_SERVICE_KEY,
        "Accept": "application/json",
        "Content-Type": "application/json",
    }
    payload = {
        "taskIds": cleaned_task_ids,
        "driverIds": cleaned_driver_ids,
        "vehicleIds": cleaned_vehicle_ids,
    }

    should_close_client = client is None
    http_client = client or httpx.Client(timeout=settings.TOOL_HTTP_TIMEOUT)

    try:
        attempts = 0
        max_attempts = 1 + MAX_TRANSIENT_RETRIES

        while attempts < max_attempts:
            attempts += 1
            try:
                response = http_client.post(url, json=payload, headers=headers)

                # Never retry 401/403 auth rejections
                if response.status_code in (401, 403):
                    logger.error(
                        "Internal service authentication rejected by backend (status: %d)",
                        response.status_code,
                    )
                    raise RuntimeError(
                        f"Authentication failed: Backend rejected internal service authorization (HTTP {response.status_code})."
                    )

                # Never retry other 4xx client errors (400, 404, etc.)
                if 400 <= response.status_code < 500:
                    logger.warning(
                        "Backend rejected operational validation context request (status: %d)",
                        response.status_code,
                    )
                    raise RuntimeError(
                        f"Backend rejected operational validation context request: HTTP {response.status_code} - {response.text}"
                    )

                # Handle 5xx server errors with transient retry
                if response.status_code >= 500:
                    logger.warning(
                        "Backend server error on operational validation context (status: %d, attempt %d/%d)",
                        response.status_code,
                        attempts,
                        max_attempts,
                    )
                    if attempts < max_attempts:
                        time.sleep(INITIAL_RETRY_BACKOFF_SECONDS)
                        continue
                    raise RuntimeError(
                        f"Backend server error on operational validation context: HTTP {response.status_code}."
                    )

                # 200 OK -> parse payload
                try:
                    data = response.json()
                    return OperationalValidationContextResponse.model_validate(data)
                except Exception as ex:
                    logger.error("Failed to parse operational validation context JSON response: %s", ex)
                    raise RuntimeError(
                        f"Failed to parse operational validation context payload from backend: {ex}"
                    ) from ex

            except httpx.RequestError as ex:
                logger.warning(
                    "Network error during operational validation context retrieval (attempt %d/%d): %s",
                    attempts,
                    max_attempts,
                    ex,
                )
                if attempts < max_attempts:
                    time.sleep(INITIAL_RETRY_BACKOFF_SECONDS)
                    continue
                raise RuntimeError(
                    f"Network error while connecting to backend operational validation context endpoint: {ex}"
                ) from ex

        raise RuntimeError("Operational validation context retrieval failed: exhausted retries.")

    finally:
        if should_close_client:
            http_client.close()


@tool
def get_operational_validation_context(
    task_ids: Optional[Sequence[str]] = None,
    driver_ids: Optional[Sequence[str]] = None,
    vehicle_ids: Optional[Sequence[str]] = None,
) -> str:
    """Retrieve fresh authoritative operational state for specific collection tasks, drivers, and vehicles.

    Returns the current assignment status, driver availability, vehicle operational status,
    and supported waste types directly from the authoritative backend.
    """
    result = fetch_operational_validation_context(task_ids=task_ids, driver_ids=driver_ids, vehicle_ids=vehicle_ids)
    return result.model_dump_json(by_alias=True)
