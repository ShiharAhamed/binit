from datetime import datetime
from typing import List, Optional
from uuid import UUID

from pydantic import BaseModel, ConfigDict, Field, model_validator


class CollectionNeedBinTelemetry(BaseModel):
    """Essential, non-personal bin state supplied only for bin-targeted collection needs."""

    model_config = ConfigDict(populate_by_name=True, extra="forbid")

    bin_code: str = Field(alias="binCode")
    capacity_liters: int = Field(alias="capacityLiters")
    latest_fill_level_percent: Optional[int] = Field(default=None, alias="latestFillLevelPercent")
    latest_condition: Optional[str] = Field(default=None, alias="latestCondition")
    observation_age_hours: Optional[float] = Field(default=None, alias="observationAgeHours")


class CollectionNeedToolItem(BaseModel):
    """Sanitized authoritative collection need returned by the C2 internal tool.

    This model intentionally contains no report description, citizen/officer identity,
    notes, attachment, task, vehicle, or route data.
    """

    model_config = ConfigDict(populate_by_name=True, extra="forbid")

    id: UUID
    target_type: str = Field(alias="targetType")
    waste_report_id: Optional[UUID] = Field(default=None, alias="wasteReportId")
    waste_bin_id: Optional[UUID] = Field(default=None, alias="wasteBinId")
    report_reference: Optional[str] = Field(default=None, alias="reportReference")
    bin_code: Optional[str] = Field(default=None, alias="binCode")
    collection_reason: str = Field(alias="collectionReason")
    latitude: float
    longitude: float
    address_text: Optional[str] = Field(default=None, alias="addressText")
    waste_types: List[str] = Field(alias="wasteTypes")
    urgency: str
    trigger_date: datetime = Field(alias="triggerDate")
    bin_telemetry: Optional[CollectionNeedBinTelemetry] = Field(default=None, alias="binTelemetry")

    @model_validator(mode="after")
    def validate_target_reference_and_telemetry(self):
        if self.target_type not in {"Report", "Bin"}:
            raise ValueError("targetType must be Report or Bin.")
        if self.collection_reason not in {"VerifiedReport", "FullOrBlockedBin", "RoutineCollection"}:
            raise ValueError("collectionReason is not supported by the internal collection-needs contract.")
        if self.urgency not in {"Low", "Medium", "High", "Urgent"}:
            raise ValueError("urgency is not supported by the internal collection-needs contract.")

        is_report = self.target_type == "Report"
        if is_report and self.collection_reason != "VerifiedReport":
            raise ValueError("Report needs must use the VerifiedReport collection reason.")
        if not is_report and self.collection_reason not in {"FullOrBlockedBin", "RoutineCollection"}:
            raise ValueError("Bin needs must use a bin collection reason.")
        if is_report and (self.waste_report_id is None or self.waste_bin_id is not None or self.bin_telemetry is not None):
            raise ValueError("Report needs must include only wasteReportId and no binTelemetry.")
        if not is_report and (self.waste_bin_id is None or self.waste_report_id is not None or self.bin_telemetry is None):
            raise ValueError("Bin needs must include only wasteBinId and binTelemetry.")
        return self


class CollectionNeedsToolResponse(BaseModel):
    """Paginated internal-tool result; page metadata distinguishes partial from complete snapshots."""

    model_config = ConfigDict(populate_by_name=True, extra="forbid")

    items: List[CollectionNeedToolItem]
    page: int
    page_size: int = Field(alias="pageSize")
    total_count: int = Field(alias="totalCount")
    total_pages: int = Field(alias="totalPages")
