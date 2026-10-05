"""Internal transport endpoints for stateless SmartWaste orchestration phases."""

import logging
from uuid import UUID

from fastapi import APIRouter, Depends, HTTPException, status
from pydantic import BaseModel, ConfigDict, Field, model_validator

from app.api.internal_auth import require_internal_service_key
from app.models.shared_planner import SharedPlannerRequest
from app.models.workflow_trigger import WorkflowTriggerType, validate_trigger_pair
from app.orchestration import (
    INVALID_RESUME_CONTEXT,
    OrchestrationResultEnvelope,
    WorkflowResumeContext,
    resume_after_collection_approval,
    resume_after_report_verification,
    run_collection_approval_phase,
)

logger = logging.getLogger("smartwaste.ai")

router = APIRouter(
    prefix="/api/v1/internal/agent-workflows",
    tags=["Internal Agent Workflows"],
    dependencies=[Depends(require_internal_service_key)],
)


class StartAgentWorkflowRequest(BaseModel):
    """Transport input supplied by ASP.NET for a new flagship workflow run."""

    model_config = ConfigDict(populate_by_name=True, extra="forbid", str_strip_whitespace=True)

    workflow_id: UUID = Field(..., alias="workflowId")
    objective: str = Field(..., min_length=5, max_length=1000)
    trigger_type: WorkflowTriggerType = Field(
        default=WorkflowTriggerType.ManualOperationalPlanning,
        alias="triggerType",
    )
    triggering_waste_report_id: UUID | None = Field(default=None, alias="triggeringWasteReportId")

    @model_validator(mode="after")
    def validate_trigger(self) -> "StartAgentWorkflowRequest":
        validate_trigger_pair(self.trigger_type, self.triggering_waste_report_id)
        return self


class ResumeAgentWorkflowRequest(BaseModel):
    """Transport input containing an ASP.NET-persisted Step 9C snapshot and resume evidence."""

    model_config = ConfigDict(populate_by_name=True, extra="forbid")

    workflow: OrchestrationResultEnvelope
    resume_context: WorkflowResumeContext = Field(..., alias="resumeContext")


def _raise_if_invalid_resume(result: OrchestrationResultEnvelope) -> None:
    """Map expected resume-state conflicts to HTTP 409 without leaking implementation detail."""
    if any(error.code == INVALID_RESUME_CONTEXT for error in result.errors):
        raise HTTPException(
            status_code=status.HTTP_409_CONFLICT,
            detail="The supplied workflow snapshot cannot be resumed in its current state.",
        )


@router.post(
    "/start",
    response_model=OrchestrationResultEnvelope,
    status_code=status.HTTP_200_OK,
    summary="INTERNAL: start collection orchestration",
    description="Runs the stateless Step 9C collection-planning phase for ASP.NET only.",
)
def start_agent_workflow(request: StartAgentWorkflowRequest) -> OrchestrationResultEnvelope:
    """Transport only: delegate exactly once to the established Step 9C runner."""
    planner_request = SharedPlannerRequest(workflowId=request.workflow_id, objective=request.objective)
    if request.trigger_type == WorkflowTriggerType.ManualOperationalPlanning:
        result = run_collection_approval_phase(planner_request)
    else:
        result = run_collection_approval_phase(
            planner_request,
            trigger_type=request.trigger_type,
            triggering_waste_report_id=request.triggering_waste_report_id,
        )
    logger.info("Internal workflow start completed [workflow_id=%s, phase=%s]", request.workflow_id, result.current_phase.value)
    return result


@router.post(
    "/resume-after-report-verification",
    response_model=OrchestrationResultEnvelope,
    status_code=status.HTTP_200_OK,
    summary="INTERNAL: resume collection planning after report verification",
    description="Runs only C2 after ASP.NET authoritatively verifies the triggering report.",
)
def resume_after_report_verification_endpoint(
    request: ResumeAgentWorkflowRequest,
) -> OrchestrationResultEnvelope:
    """Reconstruct a persisted report pause; no in-memory checkpoint is required."""
    result = resume_after_report_verification(request.workflow, request.resume_context)
    _raise_if_invalid_resume(result)
    logger.info(
        "Internal report-verification resume completed [workflow_id=%s, phase=%s]",
        request.resume_context.workflow_id,
        result.current_phase.value,
    )
    return result


@router.post(
    "/resume-after-collection-approval",
    response_model=OrchestrationResultEnvelope,
    status_code=status.HTTP_200_OK,
    summary="INTERNAL: resume dispatch orchestration",
    description="Runs the stateless Step 9D dispatch phase after ASP.NET-authoritative collection execution.",
)
def resume_agent_workflow(
    request: ResumeAgentWorkflowRequest,
) -> OrchestrationResultEnvelope:
    """Transport only: delegate exactly once to the established Step 9D runner."""
    result = resume_after_collection_approval(request.workflow, request.resume_context)
    _raise_if_invalid_resume(result)
    logger.info(
        "Internal workflow resume completed [workflow_id=%s, phase=%s]",
        request.resume_context.workflow_id,
        result.current_phase.value,
    )
    return result
