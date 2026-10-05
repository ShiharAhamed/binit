"""Inbound machine-to-machine authentication for internal FastAPI routes."""

import hmac

from fastapi import Header, HTTPException, status

from app.core.config import get_settings

INTERNAL_AUTH_HEADER = "X-Internal-Service-Key"


def require_internal_service_key(
    provided_key: str | None = Header(default=None, alias=INTERNAL_AUTH_HEADER),
) -> None:
    """Fail closed unless the configured internal service key matches the request header."""
    configured_key = get_settings().INTERNAL_SERVICE_KEY
    if not configured_key or not configured_key.strip() or not provided_key:
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Internal service authentication failed.",
        )
    if not hmac.compare_digest(provided_key, configured_key):
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Internal service authentication failed.",
        )
