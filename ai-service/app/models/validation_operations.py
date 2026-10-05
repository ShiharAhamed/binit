from typing import List, Literal, Optional
from uuid import UUID

from pydantic import BaseModel, ConfigDict, Field

from app.models.fleet_route import DispatchPlanRecommendation, UnplannedTask


class OperationalTaskItem(BaseModel):
    """Authoritative task state projection for operational validation."""

    model_config = ConfigDict(populate_by_name=True, extra="forbid")

    task_id: UUID = Field(alias="taskId")
    task_code: str = Field(alias="taskCode")
    status: str
    has_active_assignment: bool = Field(alias="hasActiveAssignment")
    waste_types: List[str] = Field(default_factory=list, alias="wasteTypes")


class OperationalDriverItem(BaseModel):
    """Authoritative driver state projection for operational validation."""

    model_config = ConfigDict(populate_by_name=True, extra="forbid")

    driver_id: UUID = Field(alias="driverId")
    display_name: str = Field(alias="displayName")
    availability_status: str = Field(alias="availabilityStatus")
    is_occupied: bool = Field(alias="isOccupied")


class OperationalVehicleItem(BaseModel):
    """Authoritative vehicle state projection for operational validation."""

    model_config = ConfigDict(populate_by_name=True, extra="forbid")

    vehicle_id: UUID = Field(alias="vehicleId")
    registration_number: str = Field(alias="registrationNumber")
    vehicle_type: str = Field(alias="vehicleType")
    operational_status: str = Field(alias="operationalStatus")
    is_occupied: bool = Field(alias="isOccupied")
    supported_waste_types: List[str] = Field(default_factory=list, alias="supportedWasteTypes")


class OperationalValidationContextResponse(BaseModel):
    """Fresh authoritative operational state of requested resources from ASP.NET Core."""

    model_config = ConfigDict(populate_by_name=True, extra="forbid")

    tasks: List[OperationalTaskItem] = Field(default_factory=list)
    drivers: List[OperationalDriverItem] = Field(default_factory=list)
    vehicles: List[OperationalVehicleItem] = Field(default_factory=list)


class ValidationOperationsRequest(BaseModel):
    """Input request for the Validation & Operations Agent containing a C3 dispatch proposal."""

    model_config = ConfigDict(populate_by_name=True, extra="forbid")

    objective: str = Field(min_length=5, max_length=500)
    dispatch_plans: List[DispatchPlanRecommendation] = Field(default_factory=list, alias="dispatchPlans")
    unplanned_tasks: List[UnplannedTask] = Field(default_factory=list, alias="unplannedTasks")


ValidationFindingSeverity = Literal["Info", "Warning", "Error"]


class ValidationFinding(BaseModel):
    """Structured diagnostic finding produced during operational validation."""

    model_config = ConfigDict(populate_by_name=True, extra="forbid")

    code: str = Field(min_length=2, max_length=100)
    severity: ValidationFindingSeverity
    message: str = Field(min_length=3, max_length=1000)
    related_task_ids: List[UUID] = Field(default_factory=list, alias="relatedTaskIds")
    related_driver_id: Optional[UUID] = Field(default=None, alias="relatedDriverId")
    related_vehicle_id: Optional[UUID] = Field(default=None, alias="relatedVehicleId")


class PlanValidationReview(BaseModel):
    """Operational validation evaluation for a single dispatch plan."""

    model_config = ConfigDict(populate_by_name=True, extra="forbid")

    plan_id: str = Field(alias="planId")
    outcome: Literal["ReadyForHumanReview", "NeedsRevision"]
    requires_acknowledgement: bool = Field(default=False, alias="requiresAcknowledgement")
    findings: List[ValidationFinding] = Field(default_factory=list)
    summary: str = Field(min_length=5, max_length=1000)


class ValidationOperationsResult(BaseModel):
    """Advisory operational review produced by the Validation & Operations Agent."""

    model_config = ConfigDict(populate_by_name=True, extra="forbid")

    objective: str
    validation_outcome: Optional[Literal["ReadyForHumanReview", "NeedsRevision"]] = Field(
        default=None, alias="validationOutcome"
    )
    plan_reviews: List[PlanValidationReview] = Field(default_factory=list, alias="planReviews")
    unplanned_task_findings: List[ValidationFinding] = Field(default_factory=list, alias="unplannedTaskFindings")
    requires_acknowledgement: bool = Field(default=False, alias="requiresAcknowledgement")
    warnings: List[str] = Field(default_factory=list, max_length=100)
    summary: str = Field(min_length=5, max_length=1500)

    agent_name: str = Field(default="validation_operations_agent", alias="agentName")
    model_name: Optional[str] = Field(default=None, alias="modelName")
    advisory_only: Literal[True] = Field(default=True, alias="advisoryOnly")
    status: Literal["completed", "empty"] = "completed"


class _StructuredValidationOperationsPayload(BaseModel):
    """Internal model output parsing schema for the LLM review call."""

    model_config = ConfigDict(populate_by_name=True, extra="forbid")

    validation_outcome: Literal["ReadyForHumanReview", "NeedsRevision"] = Field(alias="validationOutcome")
    plan_reviews: List[PlanValidationReview] = Field(default_factory=list, alias="planReviews")
    unplanned_task_findings: List[ValidationFinding] = Field(default_factory=list, alias="unplannedTaskFindings")
    requires_acknowledgement: bool = Field(default=False, alias="requiresAcknowledgement")
    warnings: List[str] = Field(default_factory=list)
    summary: str = Field(min_length=5, max_length=1500)
