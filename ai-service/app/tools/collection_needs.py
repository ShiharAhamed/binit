import logging
import time
from dataclasses import dataclass, field
from datetime import date
from typing import List, Optional

import httpx
from langchain_core.tools import tool

from app.core.config import get_settings
from app.models.collection_needs import CollectionNeedToolItem, CollectionNeedsToolResponse


@dataclass
class CollectionNeedsSnapshot:
    """Merged result of all fetched pages for the complete authoritative CollectionNeeds snapshot.

    Invariant: is_complete is True only when every page up to total_pages was successfully
    fetched and the merged item count matches the authoritative total_count.
    is_complete == False means the snapshot is PARTIAL and must NOT be treated as
    approval-ready in the future Shared Planner workflow.
    """
    items: List[CollectionNeedToolItem] = field(default_factory=list)
    retrieved_pages: List[int] = field(default_factory=list)
    page_size: int = 20
    total_count: int = 0
    total_pages: int = 0
    is_complete: bool = False

logger = logging.getLogger(__name__)

INTERNAL_AUTH_HEADER = "X-Internal-Service-Key"
COLLECTION_NEEDS_PATH = "/api/v1/internal/ai-tools/collection-needs"
MAX_TRANSIENT_RETRIES = 1
INITIAL_RETRY_BACKOFF_SECONDS = 0.5
_TARGET_TYPES = {"Report", "Bin"}
_COLLECTION_REASONS = {"VerifiedReport", "FullOrBlockedBin", "RoutineCollection"}


def _validate_query(
    target_type: Optional[str],
    collection_reason: Optional[str],
    target_date: Optional[str],
    page: int,
    page_size: int,
) -> Optional[str]:
    if not isinstance(page, int) or isinstance(page, bool) or page < 1:
        raise ValueError("page must be an integer greater than or equal to 1.")
    if not isinstance(page_size, int) or isinstance(page_size, bool) or not 1 <= page_size <= 50:
        raise ValueError("page_size must be an integer between 1 and 50.")
    if target_type is not None and target_type not in _TARGET_TYPES:
        raise ValueError("target_type must be Report or Bin.")
    if collection_reason is not None and collection_reason not in _COLLECTION_REASONS:
        raise ValueError("collection_reason must be VerifiedReport, FullOrBlockedBin, or RoutineCollection.")
    if target_date is None:
        return None
    if not isinstance(target_date, str):
        raise ValueError("target_date must be an ISO calendar date (YYYY-MM-DD).")
    try:
        return date.fromisoformat(target_date).isoformat()
    except ValueError as ex:
        raise ValueError("target_date must be an ISO calendar date (YYYY-MM-DD).") from ex


def fetch_collection_needs(
    target_type: Optional[str] = None,
    collection_reason: Optional[str] = None,
    target_date: Optional[str] = None,
    page: int = 1,
    page_size: int = 20,
    client: Optional[httpx.Client] = None,
) -> CollectionNeedsToolResponse:
    """Retrieve a bounded, sanitized C2 collection-needs page from ASP.NET Core.

    The endpoint is read-only. It applies the backend's current authoritative
    eligibility, urgency, routine-date, and active-task-suppression rules.
    Empty results are returned as an empty paginated response; failures raise.
    """
    normalized_target_date = _validate_query(
        target_type, collection_reason, target_date, page, page_size
    )
    settings = get_settings()
    if not settings.INTERNAL_SERVICE_KEY or not settings.INTERNAL_SERVICE_KEY.strip():
        logger.error("Internal service key is not configured in settings/environment.")
        raise RuntimeError("Internal service authentication key is not configured.")

    params = {"page": page, "pageSize": page_size}
    if target_type is not None:
        params["targetType"] = target_type
    if collection_reason is not None:
        params["collectionReason"] = collection_reason
    if normalized_target_date is not None:
        params["targetDate"] = normalized_target_date

    url = f"{settings.ASPNET_API_BASE_URL.rstrip('/')}{COLLECTION_NEEDS_PATH}"
    headers = {INTERNAL_AUTH_HEADER: settings.INTERNAL_SERVICE_KEY, "Accept": "application/json"}
    should_close_client = client is None
    http_client = client or httpx.Client(timeout=settings.TOOL_HTTP_TIMEOUT)

    try:
        for attempt in range(1, MAX_TRANSIENT_RETRIES + 2):
            try:
                response = http_client.get(url, params=params, headers=headers)
                status = response.status_code
                if status in (401, 403) or 400 <= status < 500:
                    logger.warning("Internal collection-needs request was rejected (HTTP %d).", status)
                    raise RuntimeError(f"Backend rejected collection-needs request (HTTP {status}).")
                if status >= 500:
                    if attempt <= MAX_TRANSIENT_RETRIES:
                        logger.warning("Transient collection-needs backend failure (HTTP %d); retrying once.", status)
                        time.sleep(INITIAL_RETRY_BACKOFF_SECONDS)
                        continue
                    raise RuntimeError(f"Collection-needs backend error (HTTP {status}) after retry.")

                return CollectionNeedsToolResponse.model_validate(response.json())
            except (httpx.ConnectError, httpx.ConnectTimeout, httpx.ReadTimeout, httpx.NetworkError) as ex:
                if attempt <= MAX_TRANSIENT_RETRIES:
                    logger.warning("Transient collection-needs network error (%s); retrying once.", type(ex).__name__)
                    time.sleep(INITIAL_RETRY_BACKOFF_SECONDS)
                    continue
                raise RuntimeError(
                    f"Failed to retrieve collection needs after retry: {type(ex).__name__}"
                ) from None
    finally:
        if should_close_client:
            http_client.close()


@tool("get_collection_needs")
def get_collection_needs(
    target_type: Optional[str] = None,
    collection_reason: Optional[str] = None,
    target_date: Optional[str] = None,
    page: int = 1,
    page_size: int = 20,
) -> CollectionNeedsToolResponse:
    """Retrieve a safe, paginated snapshot of current authoritative collection needs.

    Arguments: target_type may be Report or Bin; collection_reason may be
    VerifiedReport, FullOrBlockedBin, or RoutineCollection; target_date is an
    optional municipality-local ISO calendar date used only for routine-needs
    evaluation. Page metadata must be used to distinguish a partial page from a
    complete queue. This tool cannot create, reschedule, assign, or approve tasks.
    """
    return fetch_collection_needs(
        target_type=target_type,
        collection_reason=collection_reason,
        target_date=target_date,
        page=page,
        page_size=page_size,
    )


def fetch_all_collection_needs(
    target_type: Optional[str] = None,
    collection_reason: Optional[str] = None,
    target_date: Optional[str] = None,
    page_size: int = 20,
    client: Optional[httpx.Client] = None,
) -> CollectionNeedsSnapshot:
    """Deterministically fetch every page of the authoritative CollectionNeeds snapshot.

    Flow:
    1. Fetch page 1.
    2. Read total_pages from the authoritative response.
    3. If total_pages > 1, fetch pages 2..total_pages using the same client.
    4. Merge all items into one list.
    5. Return a CollectionNeedsSnapshot with is_complete=True only when every page
       was retrieved and the merged item count equals authoritative total_count.

    Failure contract:
    - Any page failure (network, HTTP error, validation) raises RuntimeError immediately.
    - The caller receives is_complete=False or an exception; never a silently incomplete
      snapshot presented as complete.
    - is_complete=False is the sentinel for the future Shared Planner to refuse
      advancing to human approval/execution.
    """
    snapshot = CollectionNeedsSnapshot(page_size=page_size)

    # Fetch page 1 and determine total_pages
    first_page = fetch_collection_needs(
        target_type=target_type,
        collection_reason=collection_reason,
        target_date=target_date,
        page=1,
        page_size=page_size,
        client=client,
    )

    snapshot.total_count = first_page.total_count
    snapshot.total_pages = first_page.total_pages
    snapshot.items.extend(first_page.items)
    snapshot.retrieved_pages.append(1)

    logger.debug(
        "CollectionNeeds page 1/%d fetched: %d items, totalCount=%d",
        first_page.total_pages, len(first_page.items), first_page.total_count,
    )

    # Fetch remaining pages if totalPages > 1
    for page_num in range(2, first_page.total_pages + 1):
        logger.debug("Fetching CollectionNeeds page %d/%d", page_num, first_page.total_pages)
        page_response = fetch_collection_needs(
            target_type=target_type,
            collection_reason=collection_reason,
            target_date=target_date,
            page=page_num,
            page_size=page_size,
            client=client,
        )

        # Guard: backend pagination metadata must remain consistent across pages
        if page_response.total_count != first_page.total_count:
            raise RuntimeError(
                f"Inconsistent totalCount across pages: page 1 reported {first_page.total_count}, "
                f"page {page_num} reported {page_response.total_count}. "
                "Aborting — authoritative source data is inconsistent."
            )
        if page_response.total_pages != first_page.total_pages:
            raise RuntimeError(
                f"Inconsistent totalPages across pages: page 1 reported {first_page.total_pages}, "
                f"page {page_num} reported {page_response.total_pages}. "
                "Aborting — authoritative source data is inconsistent."
            )

        snapshot.items.extend(page_response.items)
        snapshot.retrieved_pages.append(page_num)

    # Derive is_complete deterministically — never set blindly
    snapshot.is_complete = (
        len(snapshot.retrieved_pages) == snapshot.total_pages
        and len(snapshot.items) == snapshot.total_count
    ) if snapshot.total_pages > 0 else (snapshot.total_count == 0)

    logger.info(
        "CollectionNeeds full snapshot: %d/%d items, pages=%s, isComplete=%s",
        len(snapshot.items), snapshot.total_count,
        snapshot.retrieved_pages, snapshot.is_complete,
    )

    return snapshot

