from typing import List, Literal, Optional
from uuid import UUID

from pydantic import AliasChoices, BaseModel, ConfigDict, Field

from app.models.fleet_resources import FleetCompatibilityStatus


class FleetRouteRequest(BaseModel):
    """Input request for the advisory Fleet & Route Agent."""

    model_config = ConfigDict(populate_by_name=True, extra="forbid")

    objective: str = Field(min_length=5, max_length=500)
    page: int = Field(default=1, ge=1)
    page_size: int = Field(default=20, ge=1, le=50, alias="pageSize")


class RecommendedFleetTask(BaseModel):
    """Structured advisory recommendation for a collection task in a suggested stop sequence."""

    model_config = ConfigDict(populate_by_name=True, extra="forbid")

    task_id: UUID = Field(alias="taskId")
    task_code: str = Field(alias="taskCode")
    sequence: int = Field(ge=1)
    address_text: Optional[str] = Field(default=None, alias="addressText")
    reason: str


class DriverRecommendation(BaseModel):
    """Structured recommendation of an available, unoccupied Driver from authoritative resources."""

    model_config = ConfigDict(populate_by_name=True, extra="forbid")

    driver_id: UUID = Field(alias="driverId")
    display_name: str = Field(alias="displayName")
    reason: str


class VehicleRecommendation(BaseModel):
    """Structured recommendation of an operationally available, unoccupied Vehicle from authoritative resources."""

    model_config = ConfigDict(populate_by_name=True, extra="forbid")

    vehicle_id: UUID = Field(alias="vehicleId")
    registration_number: str = Field(alias="registrationNumber")
    vehicle_type: str = Field(alias="vehicleType")
    reason: str


class RecommendationCompatibility(BaseModel):
    """Authoritative deterministic compatibility outcome evaluated against the ASP.NET Core backend."""

    model_config = ConfigDict(populate_by_name=True, extra="forbid")

    status: FleetCompatibilityStatus
    requires_acknowledgement: bool = Field(alias="requiresAcknowledgement")
    issues: List[str] = Field(default_factory=list)


class UnplannedTask(BaseModel):
    """Authoritative Scheduled task not included in a dispatch plan, with advisory reason."""

    model_config = ConfigDict(populate_by_name=True, extra="forbid")

    task_id: UUID = Field(alias="taskId")
    task_code: Optional[str] = Field(default=None, alias="taskCode")
    reason: str = Field(min_length=5, max_length=500)


class DispatchPlanRecommendation(BaseModel):
    """Independent advisory dispatch plan pairing one driver, one vehicle, and a sequence of tasks."""

    model_config = ConfigDict(populate_by_name=True, extra="forbid")

    plan_id: str = Field(alias="planId", min_length=3, max_length=80, pattern=r"^plan-[1-9][0-9]*$")
    recommended_driver: DriverRecommendation = Field(
        validation_alias=AliasChoices("recommendedDriver", "driver"),
        serialization_alias="recommendedDriver",
    )
    recommended_vehicle: VehicleRecommendation = Field(
        validation_alias=AliasChoices("recommendedVehicle", "vehicle"),
        serialization_alias="recommendedVehicle",
    )
    recommended_tasks: List[RecommendedFleetTask] = Field(alias="recommendedTasks", min_length=1)
    compatibility: Optional[RecommendationCompatibility] = None
    rationale: str = Field(min_length=5, max_length=500)
    warnings: List[str] = Field(default_factory=list, max_length=10)


class _StructuredFleetRoutePayload(BaseModel):
    """Internal schema for model output parsing."""

    model_config = ConfigDict(populate_by_name=True, extra="forbid")

    dispatch_plans: List[DispatchPlanRecommendation] = Field(default_factory=list, alias="dispatchPlans")
    unplanned_tasks: List[UnplannedTask] = Field(default_factory=list, alias="unplannedTasks")
    warnings: List[str] = Field(default_factory=list, max_length=15)
    rationale: str = Field(min_length=5, max_length=500)


class FleetRouteResult(BaseModel):
    """Advisory-only fleet dispatch proposal returned by the Fleet & Route Agent."""

    model_config = ConfigDict(populate_by_name=True, extra="forbid")

    objective: str
    dispatch_plans: List[DispatchPlanRecommendation] = Field(default_factory=list, alias="dispatchPlans")
    unplanned_tasks: List[UnplannedTask] = Field(default_factory=list, alias="unplannedTasks")
    warnings: List[str] = Field(default_factory=list, max_length=100)
    rationale: str

    source_task_page: int = Field(alias="sourceTaskPage")
    source_task_page_size: int = Field(alias="sourceTaskPageSize")
    source_task_total_count: int = Field(alias="sourceTaskTotalCount")
    source_task_total_pages: int = Field(alias="sourceTaskTotalPages")

    agent_name: str = Field(default="fleet_route_agent", alias="agentName")
    model_name: Optional[str] = Field(default=None, alias="modelName")
    advisory_only: Literal[True] = Field(default=True, alias="advisoryOnly")
    status: Literal["completed", "empty"]
