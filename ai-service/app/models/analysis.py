from enum import Enum
from typing import List, Optional
from uuid import UUID

from pydantic import BaseModel, ConfigDict, Field, model_validator

from app.models.workflow_trigger import WorkflowTriggerType, validate_trigger_pair


class RecommendedPriority(str, Enum):
    """Advisory operational priority recommended by the Waste Analysis Agent.

    IMPORTANT:
    This recommendation is purely advisory for downstream collection planning.
    It does NOT mutate authoritative WasteReport.Priority or WasteReport.Status
    in ASP.NET Core or PostgreSQL.
    """

    LOW = "Low"
    MEDIUM = "Medium"
    HIGH = "High"
    URGENT = "Urgent"


class AnalysisConfidence(str, Enum):
    """Subjective confidence level based on available structured evidence."""

    LOW = "Low"
    MEDIUM = "Medium"
    HIGH = "High"


class WasteAnalysisRequest(BaseModel):
    """Input contract for invoking the Waste Analysis Agent.

    Enforces bounded validation and strictly forbids arbitrary/injected parameters
    such as SQL queries, citizen IDs, role overrides, or arbitrary endpoints.
    """

    model_config = ConfigDict(populate_by_name=True, str_strip_whitespace=True, extra="forbid")

    objective: str = Field(
        ...,
        min_length=5,
        max_length=500,
        description="Domain-specific analysis objective, e.g. 'Analyse verified waste reports for downstream collection planning.'",
    )
    page: int = Field(
        default=1,
        ge=1,
        description="Page number of verified reports to retrieve from backend.",
    )
    page_size: int = Field(
        default=20,
        ge=1,
        le=50,
        description="Number of verified reports per page, bounded between 1 and 50.",
    )
    trigger_type: WorkflowTriggerType = Field(
        default=WorkflowTriggerType.ManualOperationalPlanning,
        alias="triggerType",
    )
    triggering_waste_report_id: Optional[UUID] = Field(
        default=None,
        alias="triggeringWasteReportId",
    )
    workflow_objective: Optional[str] = Field(
        default=None,
        alias="workflowObjective",
        min_length=5,
        max_length=1000,
        description="ASP.NET-generated trusted workflow objective for report-triggered analysis.",
    )

    @model_validator(mode="after")
    def validate_trigger(self) -> "WasteAnalysisRequest":
        validate_trigger_pair(self.trigger_type, self.triggering_waste_report_id)
        if self.trigger_type == WorkflowTriggerType.CitizenReportSubmission and not self.workflow_objective:
            raise ValueError("Report-triggered C1 requires the ASP.NET workflow objective.")
        return self


class WasteReportAnalysis(BaseModel):
    """Structured advisory operational assessment for a single verified waste report.

    Rules:
    - Advisory only; no authoritative action fields (status, taskId, routeId, driverId).
    - Excludes hidden chain-of-thought tokens.
    - Excludes visual/photo claims.
    """

    model_config = ConfigDict(populate_by_name=True, extra="forbid")

    report_id: UUID = Field(
        ...,
        alias="reportId",
        description="UUID of the authoritative verified waste report being analysed.",
    )
    category_assessment: str = Field(
        ...,
        alias="categoryAssessment",
        min_length=3,
        max_length=150,
        description="Concise normalized operational description (e.g. 'bulky roadside waste accumulation').",
    )
    recommended_priority: RecommendedPriority = Field(
        ...,
        alias="recommendedPriority",
        description="Advisory operational priority recommendation (Low, Medium, High, Urgent).",
    )
    operational_concerns: List[str] = Field(
        default_factory=list,
        alias="operationalConcerns",
        max_length=5,
        description="Bounded list of operational or public health concerns (0 to 5 items, max 200 chars each).",
    )
    recommended_handling: str = Field(
        ...,
        alias="recommendedHandling",
        min_length=3,
        max_length=300,
        description="Advisory handling and equipment guidance (e.g. 'standard recyclable collection', 'bulky waste vehicle recommended').",
    )
    confidence: AnalysisConfidence = Field(
        ...,
        description="Confidence level based on available report evidence (Low, Medium, High).",
    )
    rationale: str = Field(
        ...,
        min_length=5,
        max_length=500,
        description="Concise audit-friendly summary of evidence factors. No hidden chain-of-thought.",
    )


class WasteAnalysisResult(BaseModel):
    """Structured batch result of the Waste Analysis Agent run."""

    model_config = ConfigDict(populate_by_name=True, extra="forbid")

    objective: str = Field(
        ...,
        description="The domain objective provided to the agent.",
    )
    analyses: List[WasteReportAnalysis] = Field(
        default_factory=list,
        description="List of structured analyses, adhering to the exact coverage rule for verified reports returned by the tool.",
    )
    source_page: int = Field(
        ...,
        alias="sourcePage",
        description="Source page queried from authoritative backend.",
    )
    source_page_size: int = Field(
        ...,
        alias="sourcePageSize",
        description="Source page size queried from authoritative backend.",
    )
    source_total_count: int = Field(
        ...,
        alias="sourceTotalCount",
        description="Total count of verified reports reported by backend.",
    )
    agent_name: str = Field(
        default="waste_analysis_agent",
        alias="agentName",
        description="Name of the specialized agent that executed the analysis.",
    )
    model_name: Optional[str] = Field(
        default=None,
        alias="modelName",
        description="Model or provider identifier used for the analysis.",
    )
    status: str = Field(
        default="completed",
        description="Execution status ('completed' or 'empty').",
    )
