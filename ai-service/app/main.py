from contextlib import asynccontextmanager
from fastapi import FastAPI

from app.api.health import router as health_router
from app.api.orchestration import router as orchestration_router
from app.core.config import get_settings
from app.core.logging import setup_logging


@asynccontextmanager
async def lifespan(app: FastAPI):
    """Lifecycle manager for startup and shutdown logging."""
    settings = get_settings()
    logger = setup_logging(settings.APP_ENV)
    logger.info(
        "Starting %s [env=%s, host=%s, port=%d, authoritative_backend=%s]",
        settings.APP_NAME,
        settings.APP_ENV,
        settings.HOST,
        settings.PORT,
        settings.ASPNET_API_BASE_URL,
    )
    yield
    logger.info("Shutting down %s", settings.APP_NAME)


def create_app() -> FastAPI:
    """Factory creating and configuring the FastAPI application instance."""
    settings = get_settings()
    application = FastAPI(
        title=settings.APP_NAME,
        description="Internal AI microservice supporting the Smart Waste Management System.",
        version="0.1.0",
        lifespan=lifespan,
    )

    application.include_router(health_router)
    application.include_router(orchestration_router)
    return application


app = create_app()


if __name__ == "__main__":
    import uvicorn

    settings = get_settings()
    uvicorn.run(
        "app.main:app",
        host=settings.HOST,
        port=settings.PORT,
        reload=(settings.APP_ENV.lower() == "development"),
    )
