import logging
import threading
import time
from typing import Any, Optional

from langchain_core.language_models import BaseChatModel
from langchain_core.language_models.fake_chat_models import FakeChatModel

from app.core.config import Settings, get_settings

logger = logging.getLogger(__name__)



_GEMINI_MIN_INTERVAL_SECONDS = 20.0
_gemini_rate_lock = threading.Lock()
_last_gemini_request_at: float | None = None


def _wait_for_gemini_rate_limit() -> None:
    """Ensure top-level Gemini requests are spaced safely for the free tier."""

    global _last_gemini_request_at

    with _gemini_rate_lock:
        now = time.monotonic()

        if _last_gemini_request_at is not None:
            elapsed = now - _last_gemini_request_at
            wait_seconds = _GEMINI_MIN_INTERVAL_SECONDS - elapsed

            if wait_seconds > 0:
                logger.info(
                    "Gemini rate limiter waiting %.2f second(s) before next model request.",
                    wait_seconds,
                )
                time.sleep(wait_seconds)

        # Record immediately before releasing the lock so another thread
        # cannot begin a Gemini request at the same time.
        _last_gemini_request_at = time.monotonic()


def invoke_chat_model(
    model: BaseChatModel,
    model_input: Any,
    settings: Optional[Settings] = None,
    **kwargs: Any,
) -> Any:
    """Invoke a chat model while respecting Gemini free-tier pacing.

    Mock/test providers are not delayed.
    """

    active_settings = settings or get_settings()
    provider = active_settings.LLM_PROVIDER.lower().strip()

    # A supplied model override is commonly an offline fake in agent tests. Only
    # the concrete LangChain Google integration can issue a Gemini request, so
    # do not throttle other model implementations merely because Google is the
    # configured production provider.
    if provider == "google" and model.__class__.__module__.startswith("langchain_google_genai"):
        _wait_for_gemini_rate_limit()

    return model.invoke(model_input, **kwargs)


def get_chat_model(
    settings: Optional[Settings] = None,
    model_override: Optional[BaseChatModel] = None,
) -> BaseChatModel:
    """Factory provider for the configured LangChain chat model.

    Design rules:
    - If model_override is supplied (e.g. for testing/fixtures), it is returned directly.
    - Default provider is 'mock' which produces an offline FakeChatModel.
    - Supported real provider is 'google' via langchain-google-genai (ChatGoogleGenerativeAI).
    - Never leaks API keys in logs or exceptions.
    - Fails safely with clear RuntimeError if an unsupported or unconfigured provider is requested.
    """
    if model_override is not None:
        return model_override

    active_settings = settings or get_settings()
    provider = active_settings.LLM_PROVIDER.lower().strip()

    if provider == "mock":
        logger.debug("Using mock LangChain chat model provider.")
        return FakeChatModel()

    if provider == "google":
        try:
            from langchain_google_genai import ChatGoogleGenerativeAI
        except ImportError:
            raise RuntimeError(
                "LLM provider 'google' requires 'langchain-google-genai'. "
                "Ensure dependencies are installed or use LLM_PROVIDER='mock'."
            ) from None

        if not active_settings.LLM_API_KEY or not active_settings.LLM_API_KEY.strip():
            raise RuntimeError(
                "LLM provider 'google' requested but LLM_API_KEY is not configured. "
                "Set the LLM_API_KEY environment variable."
            )

        model_name = active_settings.LLM_MODEL.strip()
        if not model_name or model_name == "mock-model":
            model_name = "gemini-1.5-flash"

        logger.info("Configuring Google Gemini chat model [model=%s]", model_name)

        return ChatGoogleGenerativeAI(
            model=model_name,
            google_api_key=active_settings.LLM_API_KEY,
            temperature=active_settings.LLM_TEMPERATURE,
            timeout=active_settings.LLM_TIMEOUT,

            # IMPORTANT:
            # Agent code already controls model correction attempts.
            # Disable provider-level retries so one logical model invocation
            # does not unexpectedly consume several Gemini RPM requests.
            max_retries=0,
        )

    raise RuntimeError(
        f"Unsupported LLM provider: '{provider}'. "
        "Supported providers: 'mock', 'google'."
    )
