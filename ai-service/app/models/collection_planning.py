from datetime import date, datetime
from typing import List, Literal, Optional
from uuid import UUID

from pydantic import BaseModel, ConfigDict, Field

TargetType = Literal["Report", "Bin"]
CollectionReason = Literal["VerifiedReport", "FullOrBlockedBin", "RoutineCollection"]
Urgency = Literal["Low", "Medium", "High", "Urgent"]


class CollectionPlanningRequest(BaseModel):
    """Strict, bounded request for one advisory collection-needs planning page."""
    model_config = ConfigDict(str_strip_whitespace=True, extra="forbid", populate_by_name=True)
    objective: str = Field(..., min_length=5, max_length=500)
    target_type: Optional[TargetType] = Field(default=None, alias="targetType")
    collection_reason: Optional[CollectionReason] = Field(default=None, alias="collectionReason")
    target_date: Optional[date] = Field(default=None, alias="targetDate")
    page: int = Field(default=1, ge=1)
    page_size: int = Field(default=20, ge=1, le=50, alias="pageSize")


class CollectionNeedReference(BaseModel):
    """Minimal source reference used only to account for an authoritative need."""
    model_config = ConfigDict(populate_by_name=True, extra="forbid")
    need_id: UUID = Field(alias="needId")
    target_type: TargetType = Field(alias="targetType")
    collection_reason: CollectionReason = Field(alias="collectionReason")
    urgency: Urgency


class ProposedSchedule(BaseModel):
    """Advisory scheduling proposal for a candidate group or separate-handling recommendation.

    Fields mirror the ASP.NET Core CreateManualCollectionTaskRequest scheduling fields:
    - scheduled_at  → ScheduledAt  (UTC datetime; must be a future time at execution)
    - scheduling_reason → SchedulingReason  (5–500 chars; advisory justification for the WasteOfficer)

    This is NOT an executed schedule. It is a recommendation for WasteOfficer review.
    Actual scheduling occurs in ASP.NET after human approval.
    """
    model_config = ConfigDict(populate_by_name=True, extra="forbid")
    scheduled_at: datetime = Field(
        alias="scheduledAt",
        description="Proposed UTC datetime for collection; must be in the future relative to planning reference time.",
    )
    scheduling_reason: str = Field(
        alias="schedulingReason",
        min_length=5,
        max_length=500,
        description=(
            "Concise advisory justification for the proposed schedule (5–500 chars), "
            "matching the ASP.NET SchedulingReason field. Free text for WasteOfficer review."
        ),
    )


class CandidateCollectionGroup(BaseModel):
    """Non-authoritative candidate group, not a task, route, or dispatch instruction."""
    model_config = ConfigDict(populate_by_name=True, extra="forbid")
    group_id: str = Field(alias="groupId", min_length=3, max_length=80, pattern=r"^group-[1-9][0-9]*$")
    attention_order: int = Field(alias="attentionOrder", ge=1, le=50)
    need_references: List[CollectionNeedReference] = Field(alias="needReferences", min_length=2, max_length=10)
    proposed_schedule: ProposedSchedule = Field(alias="proposedSchedule")
    rationale: str = Field(min_length=5, max_length=500)
    waste_handling_considerations: List[str] = Field(default_factory=list, alias="wasteHandlingConsiderations", max_length=5)
    warnings: List[str] = Field(default_factory=list, max_length=5)


class NeedHandlingRecommendation(BaseModel):
    """A need to consider individually (separate handling) or defer for human review.

    proposed_schedule is REQUIRED for separate_handling items and MUST be None for deferred_needs.
    This distinction is enforced by the agent's deterministic validator, not by schema alone,
    so that the same model class can represent both list types.
    """
    model_config = ConfigDict(populate_by_name=True, extra="forbid")
    need_reference: CollectionNeedReference = Field(alias="needReference")
    attention_order: Optional[int] = Field(default=None, alias="attentionOrder", ge=1, le=50)
    proposed_schedule: Optional[ProposedSchedule] = Field(default=None, alias="proposedSchedule")
    rationale: str = Field(min_length=5, max_length=500)


class CollectionPlanningResult(BaseModel):
    """Validated advisory output for a future planner or human reviewer; never an executed plan."""
    model_config = ConfigDict(populate_by_name=True, extra="forbid")
    objective: str
    candidate_groups: List[CandidateCollectionGroup] = Field(default_factory=list, alias="candidateGroups", max_length=20)
    separate_handling: List[NeedHandlingRecommendation] = Field(default_factory=list, alias="separateHandling", max_length=50)
    deferred_needs: List[NeedHandlingRecommendation] = Field(default_factory=list, alias="deferredNeeds", max_length=50)
    warnings: List[str] = Field(default_factory=list, max_length=100)
    source_page: int = Field(alias="sourcePage", ge=1)
    source_page_size: int = Field(alias="sourcePageSize", ge=1, le=50)
    source_total_count: int = Field(alias="sourceTotalCount", ge=0)
    source_total_pages: int = Field(alias="sourceTotalPages", ge=0)
    retrieved_pages: List[int] = Field(alias="retrievedPages", min_length=1, max_length=200)
    is_complete_snapshot: bool = Field(alias="isCompleteSnapshot")
    agent_name: str = Field(default="collection_planning_agent", alias="agentName")
    model_name: Optional[str] = Field(default=None, alias="modelName")
    advisory_only: Literal[True] = Field(default=True, alias="advisoryOnly")
    status: Literal["completed", "partial", "empty"]
