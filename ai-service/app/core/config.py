from functools import lru_cache
from datetime import time, timedelta, timezone
from zoneinfo import ZoneInfo, ZoneInfoNotFoundError

from pydantic import field_validator, model_validator
from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    """Foundation configuration for the SmartWaste AI Service."""

    model_config = SettingsConfigDict(
        env_file=".env",
        env_file_encoding="utf-8",
        extra="ignore",
    )

    APP_NAME: str = "SmartWaste AI Service"
    APP_ENV: str = "development"
    HOST: str = "127.0.0.1"
    PORT: int = 8000
    ASPNET_API_BASE_URL: str = "http://localhost:5276"
    INTERNAL_SERVICE_KEY: str = ""
    TOOL_HTTP_TIMEOUT: float = 10.0

    # Match the backend Municipality:TimeZoneId setting. Collection starts use
    # municipality wall-clock hours while scheduledAt remains UTC on the wire.
    MUNICIPAL_TIMEZONE: str = "Asia/Colombo"
    COLLECTION_WINDOW_START: time = time(8, 0)
    COLLECTION_WINDOW_END: time = time(16, 0)

    # LLM Provider Configuration
    LLM_PROVIDER: str = "mock"
    LLM_MODEL: str = "mock-model"
    LLM_API_KEY: str = ""
    LLM_TEMPERATURE: float = 0.0
    LLM_TIMEOUT: float = 30.0

    @field_validator("MUNICIPAL_TIMEZONE")
    @classmethod
    def validate_municipal_timezone(cls, value: str) -> str:
        municipal_timezone(value)
        return value

    @field_validator("COLLECTION_WINDOW_START", "COLLECTION_WINDOW_END")
    @classmethod
    def validate_local_window_time(cls, value: time) -> time:
        if value.tzinfo is not None:
            raise ValueError("Collection window times must be municipality-local wall-clock times")
        return value

    @model_validator(mode="after")
    def validate_collection_window(self) -> "Settings":
        if self.COLLECTION_WINDOW_START >= self.COLLECTION_WINDOW_END:
            raise ValueError("COLLECTION_WINDOW_START must be before COLLECTION_WINDOW_END")
        return self


def municipal_timezone(name: str):
    """Resolve an IANA zone; support Colombo on Windows without an OS tz database.

    Sri Lanka uses UTC+05:30 for all future collection dates. Other zones
    require the platform's IANA database and fail closed if it is unavailable.
    """
    try:
        return ZoneInfo(name)
    except ZoneInfoNotFoundError:
        if name == "Asia/Colombo":
            return timezone(timedelta(hours=5, minutes=30), name)
        raise ValueError(f"Unknown or unavailable MUNICIPAL_TIMEZONE: {name}") from None


@lru_cache
def get_settings() -> Settings:
    """Cached singleton provider for application settings."""
    return Settings()
