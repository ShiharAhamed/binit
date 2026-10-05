from typing import Any, Dict, Optional

from pydantic import BaseModel, ConfigDict, Field

# Standard error codes
PLANNER_VALIDATION_FAILED = "PLANNER_VALIDATION_FAILED"
WASTE_ANALYSIS_FAILED = "WASTE_ANALYSIS_FAILED"
COLLECTION_PLANNING_FAILED = "COLLECTION_PLANNING_FAILED"
FLEET_PLANNING_FAILED = "FLEET_PLANNING_FAILED"
OPERATIONAL_VALIDATION_FAILED = "OPERATIONAL_VALIDATION_FAILED"
INVALID_RESUME_CONTEXT = "INVALID_RESUME_CONTEXT"
STATE_INVARIANT_VIOLATION = "STATE_INVARIANT_VIOLATION"
PROVIDER_RATE_LIMIT = "PROVIDER_RATE_LIMIT"
PROVIDER_UNAVAILABLE = "PROVIDER_UNAVAILABLE"
PROVIDER_CONFIG_ERROR = "PROVIDER_CONFIG_ERROR"
INTERNAL_ORCHESTRATION_ERROR = "INTERNAL_ORCHESTRATION_ERROR"


class OrchestrationError(BaseModel):
    """Structured, bounded operational error for LangGraph orchestration.

    Excludes raw stack traces, connection strings, API keys, or provider secrets.
    """

    model_config = ConfigDict(populate_by_name=True, extra="forbid", str_strip_whitespace=True)

    code: str = Field(
        ...,
        min_length=2,
        max_length=100,
        description="Machine-readable error code classifying the failure.",
    )
    stage: str = Field(
        ...,
        min_length=2,
        max_length=100,
        description="The orchestration stage or specialist where the failure occurred.",
    )
    message: str = Field(
        ...,
        min_length=3,
        max_length=1000,
        description="Human-readable safe operational error message.",
    )
    retryable: bool = Field(
        default=False,
        description="Whether this failure is considered transient and safe for bounded retry.",
    )
    details: Dict[str, Any] = Field(
        default_factory=dict,
        description="Optional structured non-sensitive diagnostic metadata.",
    )


def create_orchestration_error(
    code: str,
    stage: str,
    message: str,
    retryable: bool = False,
    details: Optional[Dict[str, Any]] = None,
) -> OrchestrationError:
    """Helper to construct a validated OrchestrationError."""
    return OrchestrationError(
        code=code,
        stage=stage,
        message=message,
        retryable=retryable,
        details=details or {},
    )
