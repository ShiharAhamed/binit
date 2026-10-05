"""Trusted ASP.NET workflow initiation metadata shared by transport and C1."""

from enum import Enum
from uuid import UUID


class WorkflowTriggerType(str, Enum):
    ManualOperationalPlanning = "ManualOperationalPlanning"
    CitizenReportSubmission = "CitizenReportSubmission"


def validate_trigger_pair(trigger_type: WorkflowTriggerType, report_id: UUID | None) -> None:
    """Reject missing or unexpected authoritative report links without inferring intent."""
    if trigger_type == WorkflowTriggerType.ManualOperationalPlanning and report_id is None:
        return
    if trigger_type == WorkflowTriggerType.CitizenReportSubmission and report_id is not None:
        return
    raise ValueError("triggerType and triggeringWasteReportId must form a valid workflow trigger pair.")
