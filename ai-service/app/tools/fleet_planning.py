import logging
import time
from typing import Optional, Sequence, Union
from uuid import UUID

import httpx
from langchain_core.tools import tool

from app.core.config import get_settings
from app.models.fleet_resources import (
    FleetCompatibilityResult,
    FleetPlanningContextResponse,
)

logger = logging.getLogger(__name__)

# Constants for internal communication
INTERNAL_AUTH_HEADER = "X-Internal-Service-Key"
FLEET_PLANNING_CONTEXT_PATH = "/api/v1/internal/ai-tools/fleet-planning-context"
FLEET_COMPATIBILITY_PATH = "/api/v1/internal/ai-tools/fleet-compatibility"
MAX_TRANSIENT_RETRIES = 1
INITIAL_RETRY_BACKOFF_SECONDS = 0.5


def fetch_fleet_planning_context(
    page: int = 1,
    page_size: int = 20,
    client: Optional[httpx.Client] = None,
) -> FleetPlanningContextResponse:
    """Fetch a bounded fleet planning context snapshot from the ASP.NET Core backend.

    Retrieves Scheduled unassigned collection tasks, available and unoccupied Drivers,
    and operationally available and unoccupied Vehicles. Applies data minimization
    and bounded single-retry policy for transient server/network errors.

    Args:
        page: Page number for tasks (must be integer >= 1).
        page_size: Number of tasks per page (must be integer between 1 and 50).
        client: Optional custom httpx.Client for dependency injection or testing.

    Returns:
        FleetPlanningContextResponse containing tasks, drivers, and vehicles.

    Raises:
        ValueError: If pagination bounds are violated.
        RuntimeError: If authentication fails, backend rejects, or network fails after retry.
    """
    if not isinstance(page, int) or isinstance(page, bool) or page < 1:
        raise ValueError("page must be an integer greater than or equal to 1.")
    if not isinstance(page_size, int) or isinstance(page_size, bool) or not 1 <= page_size <= 50:
        raise ValueError("page_size must be an integer between 1 and 50.")

    settings = get_settings()
    if not settings.INTERNAL_SERVICE_KEY or not settings.INTERNAL_SERVICE_KEY.strip():
        logger.error("Internal service key is not configured in settings/environment.")
        raise RuntimeError(
            "Internal service authentication key is not configured. "
            "Please set the INTERNAL_SERVICE_KEY environment variable."
        )

    base_url = settings.ASPNET_API_BASE_URL.rstrip("/")
    url = f"{base_url}{FLEET_PLANNING_CONTEXT_PATH}"
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
                        "Backend rejected fleet planning context request (status: %d)",
                        response.status_code,
                    )
                    raise RuntimeError(
                        f"Backend rejected fleet planning context request (HTTP {response.status_code}): {response.text}"
                    )

                # Server errors (5xx) - transient, eligible for bounded retry
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
                    raise RuntimeError(
                        f"Backend error (HTTP {response.status_code}) after retry: {response.text}"
                    )

                # Successful 2xx response
                data = response.json()
                return FleetPlanningContextResponse.model_validate(data)

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
                logger.error("Failed to connect to backend after retry: %s", type(ex).__name__)
                raise RuntimeError(
                    f"Failed to connect to SmartWaste backend after retry: {type(ex).__name__}"
                ) from None

    finally:
        if should_close_client:
            http_client.close()


def fetch_all_fleet_planning_context(
    page_size: int = 20,
    client: Optional[httpx.Client] = None,
) -> FleetPlanningContextResponse:
    """Fetch a complete fleet planning context snapshot across all task pages.

    Deterministically fetches page 1, inspects taskTotalPages, and retrieves all
    remaining pages (2..taskTotalPages). Merges Scheduled collection tasks and
    deduplicates driver and vehicle resources by authoritative ID.
    Raises RuntimeError on any page failure or metadata inconsistency so that
    incomplete snapshots are never treated as complete.

    Args:
        page_size: Number of collection tasks per page (between 1 and 50, default 20).
        client: Optional custom httpx.Client for dependency injection or testing.

    Returns:
        FleetPlanningContextResponse containing all merged tasks, drivers, and vehicles.
    """
    first_page = fetch_fleet_planning_context(
        page=1,
        page_size=page_size,
        client=client,
    )

    all_tasks = list(first_page.tasks)
    driver_map = {d.driver_id: d for d in first_page.drivers}
    vehicle_map = {v.vehicle_id: v for v in first_page.vehicles}

    for page_num in range(2, first_page.task_total_pages + 1):
        page_context = fetch_fleet_planning_context(
            page=page_num,
            page_size=page_size,
            client=client,
        )

        if page_context.task_total_count != first_page.task_total_count:
            raise RuntimeError(
                f"Inconsistent taskTotalCount across pages: page 1 reported {first_page.task_total_count}, "
                f"page {page_num} reported {page_context.task_total_count}. "
                "Aborting — authoritative source data is inconsistent."
            )
        if page_context.task_total_pages != first_page.task_total_pages:
            raise RuntimeError(
                f"Inconsistent taskTotalPages across pages: page 1 reported {first_page.task_total_pages}, "
                f"page {page_num} reported {page_context.task_total_pages}. "
                "Aborting — authoritative source data is inconsistent."
            )

        all_tasks.extend(page_context.tasks)

        for d in page_context.drivers:
            if d.driver_id not in driver_map:
                driver_map[d.driver_id] = d

        for v in page_context.vehicles:
            if v.vehicle_id not in vehicle_map:
                vehicle_map[v.vehicle_id] = v

    task_ids = [t.task_id for t in all_tasks]
    if len(task_ids) != len(set(task_ids)):
        raise RuntimeError("Duplicate collection task detected across context pages.")

    return FleetPlanningContextResponse(
        tasks=all_tasks,
        drivers=list(driver_map.values()),
        vehicles=list(vehicle_map.values()),
        taskPage=1,
        taskPageSize=page_size,
        taskTotalCount=first_page.task_total_count,
        taskTotalPages=first_page.task_total_pages,
    )


def fetch_fleet_compatibility(
    task_ids: Sequence[Union[UUID, str]],
    vehicle_id: Union[UUID, str],
    client: Optional[httpx.Client] = None,
) -> FleetCompatibilityResult:
    """Evaluate deterministic task/vehicle waste compatibility via ASP.NET Core.

    Sends selected task IDs and candidate vehicle ID to the authoritative backend
    compatibility tool. Returns Compatible, Unknown (requires officer acknowledgement),
    or Incompatible. Does not mutate database state.

    Args:
        task_ids: Sequence of task UUIDs (must not be empty, max 50, no duplicates).
        vehicle_id: Vehicle UUID.
        client: Optional custom httpx.Client for dependency injection or testing.

    Returns:
        FleetCompatibilityResult indicating status, acknowledgement requirement, and issues.

    Raises:
        ValueError: If input arguments are invalid or malformed.
        RuntimeError: If authentication fails, backend rejects, or network fails after retry.
    """
    if not task_ids:
        raise ValueError("task_ids must contain at least one task ID.")
    if len(task_ids) > 50:
        raise ValueError("task_ids cannot exceed 50 tasks per check.")

    validated_task_ids = []
    for tid in task_ids:
        try:
            validated_task_ids.append(str(UUID(str(tid))))
        except (ValueError, AttributeError) as ex:
            raise ValueError(f"Invalid task ID '{tid}': must be a valid UUID.") from ex

    if len(set(validated_task_ids)) != len(validated_task_ids):
        raise ValueError("task_ids must not contain duplicate task IDs.")

    try:
        validated_vehicle_id = str(UUID(str(vehicle_id)))
    except (ValueError, AttributeError) as ex:
        raise ValueError(f"Invalid vehicle ID '{vehicle_id}': must be a valid UUID.") from ex

    settings = get_settings()
    if not settings.INTERNAL_SERVICE_KEY or not settings.INTERNAL_SERVICE_KEY.strip():
        logger.error("Internal service key is not configured in settings/environment.")
        raise RuntimeError(
            "Internal service authentication key is not configured. "
            "Please set the INTERNAL_SERVICE_KEY environment variable."
        )

    base_url = settings.ASPNET_API_BASE_URL.rstrip("/")
    url = f"{base_url}{FLEET_COMPATIBILITY_PATH}"
    headers = {
        INTERNAL_AUTH_HEADER: settings.INTERNAL_SERVICE_KEY,
        "Content-Type": "application/json",
        "Accept": "application/json",
    }
    payload = {
        "taskIds": validated_task_ids,
        "vehicleId": validated_vehicle_id,
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
                        "Backend rejected fleet compatibility request (status: %d)",
                        response.status_code,
                    )
                    raise RuntimeError(
                        f"Backend rejected fleet compatibility request (HTTP {response.status_code}): {response.text}"
                    )

                # Server errors (5xx) - transient, eligible for bounded retry
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
                    raise RuntimeError(
                        f"Backend error (HTTP {response.status_code}) after retry: {response.text}"
                    )

                # Successful 2xx response
                data = response.json()
                return FleetCompatibilityResult.model_validate(data)

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
                logger.error("Failed to connect to backend after retry: %s", type(ex).__name__)
                raise RuntimeError(
                    f"Failed to connect to SmartWaste backend after retry: {type(ex).__name__}"
                ) from None

    finally:
        if should_close_client:
            http_client.close()


@tool("get_fleet_planning_context")
def get_fleet_planning_context(page: int = 1, page_size: int = 20) -> FleetPlanningContextResponse:
    """Retrieve authoritative Scheduled collection tasks and usable Driver and Vehicle resources for fleet planning.

    Returns unassigned Scheduled collection tasks, currently available and unoccupied
    Drivers, and operationally available and unoccupied Vehicles from the SmartWaste backend.
    This tool does not mutate state or create assignments.

    Args:
        page: Page number for collection tasks (minimum 1, default 1).
        page_size: Number of collection tasks per page (between 1 and 50, default 20).

    Returns:
        FleetPlanningContextResponse containing tasks, drivers, and vehicles.
    """
    return fetch_fleet_planning_context(page=page, page_size=page_size)


@tool("check_fleet_compatibility")
def check_fleet_compatibility(task_ids: list[str], vehicle_id: str) -> FleetCompatibilityResult:
    """Check deterministic waste-type compatibility between selected collection tasks and a candidate Vehicle.

    Evaluates collection task waste handling requirements against vehicle supported waste types
    using authoritative backend rules. Returns Compatible, Unknown (requires officer acknowledgement),
    or Incompatible with any identified operational issues.

    Args:
        task_ids: List of collection task UUID strings to evaluate.
        vehicle_id: Candidate vehicle UUID string.

    Returns:
        FleetCompatibilityResult indicating status, acknowledgement requirement, and issues.
    """
    return fetch_fleet_compatibility(task_ids=task_ids, vehicle_id=vehicle_id)
