from datetime import datetime
from enum import Enum
from typing import List, Optional
from uuid import UUID

from pydantic import BaseModel, ConfigDict, Field


class FleetCompatibilityStatus(str, Enum):
    """Authoritative compatibility status between collection tasks and a candidate vehicle."""

    COMPATIBLE = "Compatible"
    UNKNOWN = "Unknown"
    INCOMPATIBLE = "Incompatible"


class FleetPlanningTaskItem(BaseModel):
    """Sanitized, safe projection of a Scheduled collection task available for fleet planning.

    Data minimization rules:
    - Excludes citizen PII (name, email, phone)
    - Excludes officer IDs and administrative notes
    - Excludes attachment URLs and audit histories
    """

    model_config = ConfigDict(populate_by_name=True, extra="forbid")

    task_id: UUID = Field(alias="taskId")
    task_code: str = Field(alias="taskCode")
    target_type: str = Field(alias="targetType")
    collection_reason: str = Field(alias="collectionReason")
    scheduled_at: datetime = Field(alias="scheduledAt")
    address_text: Optional[str] = Field(default=None, alias="addressText")
    latitude: Optional[float] = None
    longitude: Optional[float] = None
    waste_types: List[str] = Field(default_factory=list, alias="wasteTypes")


class FleetPlanningDriverItem(BaseModel):
    """Safe projection of an available, unoccupied Driver suitable for assignment consideration.

    Data minimization rules:
    - Excludes driver phone, email, license numbers, and profile notes
    """

    model_config = ConfigDict(populate_by_name=True, extra="forbid")

    driver_id: UUID = Field(alias="driverId")
    display_name: str = Field(alias="displayName")
    availability_status: str = Field(alias="availabilityStatus")
    is_occupied: bool = Field(alias="isOccupied")


class FleetPlanningVehicleItem(BaseModel):
    """Safe projection of an operationally available, unoccupied Vehicle for assignment consideration.

    Data minimization rules:
    - Excludes internal maintenance histories and administrative notes
    """

    model_config = ConfigDict(populate_by_name=True, extra="forbid")

    vehicle_id: UUID = Field(alias="vehicleId")
    registration_number: str = Field(alias="registrationNumber")
    vehicle_type: str = Field(alias="vehicleType")
    capacity_liters: int = Field(alias="capacityLiters")
    operational_status: str = Field(alias="operationalStatus")
    is_occupied: bool = Field(alias="isOccupied")
    supported_waste_types: List[str] = Field(default_factory=list, alias="supportedWasteTypes")


class FleetPlanningContextResponse(BaseModel):
    """Paginated, structured fleet planning snapshot containing tasks, drivers, and vehicles."""

    model_config = ConfigDict(populate_by_name=True, extra="forbid")

    tasks: List[FleetPlanningTaskItem]
    drivers: List[FleetPlanningDriverItem]
    vehicles: List[FleetPlanningVehicleItem]
    task_page: int = Field(alias="taskPage")
    task_page_size: int = Field(alias="taskPageSize")
    task_total_count: int = Field(alias="taskTotalCount")
    task_total_pages: int = Field(alias="taskTotalPages")


class CheckFleetCompatibilityRequest(BaseModel):
    """Request payload sent to the backend fleet compatibility check endpoint."""

    model_config = ConfigDict(populate_by_name=True, extra="forbid")

    task_ids: List[UUID] = Field(alias="taskIds")
    vehicle_id: UUID = Field(alias="vehicleId")


class FleetCompatibilityResult(BaseModel):
    """Authoritative result of deterministic task/vehicle waste compatibility evaluation."""

    model_config = ConfigDict(populate_by_name=True, extra="forbid")

    status: FleetCompatibilityStatus
    requires_acknowledgement: bool = Field(alias="requiresAcknowledgement")
    issues: List[str] = Field(default_factory=list)
