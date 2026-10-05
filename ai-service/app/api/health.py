from fastapi import APIRouter
from pydantic import BaseModel

from app.core.config import get_settings

router = APIRouter(tags=["Health"])


class HealthResponse(BaseModel):
    status: str
    service: str


@router.get("/health", response_model=HealthResponse)
async def get_health() -> HealthResponse:
    """Returns foundation health status and service name."""
    settings = get_settings()
    return HealthResponse(
        status="healthy",
        service=settings.APP_NAME,
    )
