from datetime import datetime, timezone
from uuid import uuid4
import pytest
from pydantic import ValidationError

from app.models.analysis import (
    AnalysisConfidence,
    RecommendedPriority,
    WasteAnalysisRequest,
    WasteAnalysisResult,
    WasteReportAnalysis,
)
from app.models.collection_planning import (
    CandidateCollectionGroup,
    CollectionNeedReference,
    CollectionPlanningRequest,
    CollectionPlanningResult,
    NeedHandlingRecommendation,
    ProposedSchedule,
)
from app.models.fleet_resources import FleetCompatibilityStatus
from app.models.fleet_route import (
    DispatchPlanRecommendation,
    DriverRecommendation,
    FleetRouteRequest,
    FleetRouteResult,
    RecommendationCompatibility,
    RecommendedFleetTask,
    UnplannedTask,
    VehicleRecommendation,
)
from app.models.validation_operations import (
    PlanValidationReview,
    ValidationFinding,
    ValidationOperationsRequest,
    ValidationOperationsResult,
)


# ==============================================================================
# Test A: C1 Canonical Serialization & Advisory Nature
# ==============================================================================

def test_c1_canonical_serialization():
    report_id = uuid4()
    analysis = WasteReportAnalysis(
        reportId=report_id,
        categoryAssessment="Bulky roadside waste",
        recommendedPriority=RecommendedPriority.HIGH,
        operationalConcerns=["Sidewalk obstruction"],
        recommendedHandling="Compactor truck",
        confidence=AnalysisConfidence.HIGH,
        rationale="Urgent roadside buildup near school zone.",
    )

    result = WasteAnalysisResult(
        objective="Analyse verified waste reports",
        analyses=[analysis],
        sourcePage=1,
        sourcePageSize=20,
        sourceTotalCount=1,
        agentName="waste_analysis_agent",
        modelName="test-model",
        status="completed",
    )

    dumped = result.model_dump(by_alias=True, mode="json")

    # Assert top-level canonical keys
    expected_top_keys = {
        "objective",
        "analyses",
        "sourcePage",
        "sourcePageSize",
        "sourceTotalCount",
        "agentName",
        "modelName",
        "status",
    }
    assert expected_top_keys.issubset(dumped.keys())
    assert dumped["status"] == "completed"
    assert dumped["agentName"] == "waste_analysis_agent"

    # Assert analysis item canonical keys
    item = dumped["analyses"][0]
    expected_item_keys = {
        "reportId",
        "categoryAssessment",
        "recommendedPriority",
        "operationalConcerns",
        "recommendedHandling",
        "confidence",
        "rationale",
    }
    assert expected_item_keys.issubset(item.keys())
    assert item["reportId"] == str(report_id)
    assert item["recommendedPriority"] == "High"
    assert item["confidence"] == "High"

    # C1 status variants
    empty_result = WasteAnalysisResult(
        objective="Analyse verified waste reports",
        analyses=[],
        sourcePage=1,
        sourcePageSize=20,
        sourceTotalCount=0,
        agentName="waste_analysis_agent",
        modelName=None,
        status="empty",
    )
    assert empty_result.status == "empty"


# ==============================================================================
# Test B: C2 Canonical Serialization & ProposedSchedule Contract
# ==============================================================================

def test_c2_canonical_serialization_and_proposed_schedule():
    need_id_1 = uuid4()
    need_id_2 = uuid4()
    need_id_3 = uuid4()
    need_id_4 = uuid4()

    sched_time = datetime(2026, 9, 29, 8, 30, tzinfo=timezone.utc)
    schedule = ProposedSchedule(
        scheduledAt=sched_time,
        schedulingReason="Morning clearance for overflow bins.",
    )

    group = CandidateCollectionGroup(
        groupId="group-1",
        attentionOrder=1,
        needReferences=[
            CollectionNeedReference(
                needId=need_id_1,
                targetType="Bin",
                collectionReason="FullOrBlockedBin",
                urgency="Urgent",
            ),
            CollectionNeedReference(
                needId=need_id_2,
                targetType="Bin",
                collectionReason="FullOrBlockedBin",
                urgency="High",
            ),
        ],
        proposedSchedule=schedule,
        rationale="High urgency commercial bins grouped by proximity.",
        wasteHandlingConsiderations=["Standard compactor vehicle"],
        warnings=[],
    )

    sep = NeedHandlingRecommendation(
        needReference=CollectionNeedReference(
            needId=need_id_3,
            targetType="Report",
            collectionReason="VerifiedReport",
            urgency="High",
        ),
        attentionOrder=2,
        proposedSchedule=schedule,
        rationale="Hazardous chemicals require dedicated containment.",
    )

    deferred = NeedHandlingRecommendation(
        needReference=CollectionNeedReference(
            needId=need_id_4,
            targetType="Bin",
            collectionReason="RoutineCollection",
            urgency="Low",
        ),
        attentionOrder=None,
        proposedSchedule=None,
        rationale="Routine collection scheduled for standard cycle.",
    )

    result = CollectionPlanningResult(
        objective="Plan collection needs",
        candidateGroups=[group],
        separateHandling=[sep],
        deferredNeeds=[deferred],
        warnings=["Bin lacks telemetry"],
        sourcePage=1,
        sourcePageSize=20,
        sourceTotalCount=3,
        sourceTotalPages=1,
        retrievedPages=[1],
        isCompleteSnapshot=True,
        agentName="collection_planning_agent",
        modelName="test-model",
        advisoryOnly=True,
        status="completed",
    )

    dumped = result.model_dump(by_alias=True, mode="json")

    # Assert canonical keys
    expected_keys = {
        "objective",
        "candidateGroups",
        "separateHandling",
        "deferredNeeds",
        "warnings",
        "sourcePage",
        "sourcePageSize",
        "sourceTotalCount",
        "sourceTotalPages",
        "retrievedPages",
        "isCompleteSnapshot",
        "agentName",
        "modelName",
        "advisoryOnly",
        "status",
    }
    assert expected_keys.issubset(dumped.keys())
    assert dumped["advisoryOnly"] is True
    assert dumped["isCompleteSnapshot"] is True
    assert dumped["status"] == "completed"

    # Assert proposed schedule canonical serialization
    group_dump = dumped["candidateGroups"][0]
    assert "proposedSchedule" in group_dump
    assert "scheduledAt" in group_dump["proposedSchedule"]
    assert "schedulingReason" in group_dump["proposedSchedule"]
    assert group_dump["proposedSchedule"]["scheduledAt"].startswith("2026-09-29T08:30:00")

    # Assert deferred needs have no proposedSchedule
    deferred_dump = dumped["deferredNeeds"][0]
    assert deferred_dump["proposedSchedule"] is None


# ==============================================================================
# Test C: C3 Canonical Serialization & Output Alias Invariance
# ==============================================================================

def test_c3_canonical_serialization_and_alias_guarantee():
    task_id = uuid4()
    driver_id = uuid4()
    vehicle_id = uuid4()
    unplanned_id = uuid4()

    # Test that input parsing accepts alternate aliases ('driver', 'vehicle')
    plan_from_alternate = DispatchPlanRecommendation.model_validate({
        "planId": "plan-1",
        "driver": {
            "driverId": driver_id,
            "displayName": "John Driver",
            "reason": "Available driver",
        },
        "vehicle": {
            "vehicleId": vehicle_id,
            "registrationNumber": "WP-CAB-1234",
            "vehicleType": "Compactor",
            "reason": "Operational vehicle",
        },
        "recommendedTasks": [
            {
                "taskId": task_id,
                "taskCode": "TSK-001",
                "sequence": 1,
                "reason": "First stop",
            }
        ],
        "compatibility": {
            "status": "Compatible",
            "requiresAcknowledgement": False,
            "issues": [],
        },
        "rationale": "Valid candidate plan",
        "warnings": [],
    })

    result = FleetRouteResult(
        objective="Dispatch fleet",
        dispatchPlans=[plan_from_alternate],
        unplannedTasks=[
            UnplannedTask(
                taskId=unplanned_id,
                taskCode="TSK-002",
                reason="Deferred due to capacity limits.",
            )
        ],
        warnings=[],
        rationale="Plan formulated for morning operations.",
        sourceTaskPage=1,
        sourceTaskPageSize=20,
        sourceTaskTotalCount=2,
        sourceTaskTotalPages=1,
        agentName="fleet_route_agent",
        modelName="test-model",
        advisoryOnly=True,
        status="completed",
    )

    dumped = result.model_dump(by_alias=True, mode="json")

    # Assert top-level keys
    expected_top_keys = {
        "objective",
        "dispatchPlans",
        "unplannedTasks",
        "warnings",
        "rationale",
        "sourceTaskPage",
        "sourceTaskPageSize",
        "sourceTaskTotalCount",
        "sourceTaskTotalPages",
        "agentName",
        "modelName",
        "advisoryOnly",
        "status",
    }
    assert expected_top_keys.issubset(dumped.keys())
    assert dumped["advisoryOnly"] is True

    # Assert plan canonical serialization strictly emits 'recommendedDriver' and 'recommendedVehicle'
    plan_dump = dumped["dispatchPlans"][0]
    assert "recommendedDriver" in plan_dump
    assert "recommendedVehicle" in plan_dump
    assert "driver" not in plan_dump
    assert "vehicle" not in plan_dump

    assert plan_dump["recommendedDriver"]["driverId"] == str(driver_id)
    assert plan_dump["recommendedVehicle"]["vehicleId"] == str(vehicle_id)
    assert plan_dump["recommendedTasks"][0]["taskId"] == str(task_id)
    assert plan_dump["compatibility"]["status"] == "Compatible"


# ==============================================================================
# Test D: C4 Canonical Serialization & Validation Outcome Values
# ==============================================================================

def test_c4_canonical_serialization_and_outcomes():
    finding = ValidationFinding(
        code="TEST_WARNING",
        severity="Warning",
        message="Minor timing consideration.",
        relatedTaskIds=[uuid4()],
    )

    review = PlanValidationReview(
        planId="plan-1",
        outcome="ReadyForHumanReview",
        requiresAcknowledgement=True,
        findings=[finding],
        summary="Plan is viable pending manager review.",
    )

    result = ValidationOperationsResult(
        objective="Validate dispatch proposal",
        validationOutcome="ReadyForHumanReview",
        planReviews=[review],
        unplannedTaskFindings=[],
        requiresAcknowledgement=True,
        warnings=[],
        summary="Validation completed successfully.",
        agentName="validation_operations_agent",
        modelName="test-model",
        advisoryOnly=True,
        status="completed",
    )

    dumped = result.model_dump(by_alias=True, mode="json")

    expected_keys = {
        "objective",
        "validationOutcome",
        "planReviews",
        "unplannedTaskFindings",
        "requiresAcknowledgement",
        "warnings",
        "summary",
        "agentName",
        "modelName",
        "advisoryOnly",
        "status",
    }
    assert expected_keys.issubset(dumped.keys())
    assert dumped["validationOutcome"] == "ReadyForHumanReview"
    assert dumped["requiresAcknowledgement"] is True
    assert dumped["advisoryOnly"] is True

    review_dump = dumped["planReviews"][0]
    assert review_dump["planId"] == "plan-1"
    assert review_dump["outcome"] == "ReadyForHumanReview"

    finding_dump = review_dump["findings"][0]
    assert finding_dump["code"] == "TEST_WARNING"
    assert finding_dump["severity"] == "Warning"


# ==============================================================================
# Test E: Direct C3 -> C4 Structured Handoff
# ==============================================================================

def test_c3_to_c4_direct_handoff():
    task_id = uuid4()
    driver_id = uuid4()
    vehicle_id = uuid4()
    unplanned_id = uuid4()

    c3_plan = DispatchPlanRecommendation(
        planId="plan-1",
        recommendedDriver=DriverRecommendation(
            driverId=driver_id,
            displayName="Sarah Driver",
            reason="Primary assigned driver",
        ),
        recommendedVehicle=VehicleRecommendation(
            vehicleId=vehicle_id,
            registrationNumber="WP-DEF-5678",
            vehicleType="Compactor",
            reason="Designated organic waste vehicle",
        ),
        recommendedTasks=[
            RecommendedFleetTask(
                taskId=task_id,
                taskCode="TSK-00105",
                sequence=1,
                reason="High priority clearance",
            )
        ],
        compatibility=RecommendationCompatibility(
            status=FleetCompatibilityStatus.COMPATIBLE,
            requiresAcknowledgement=False,
            issues=[],
        ),
        rationale="Morning shift dispatch plan",
        warnings=[],
    )

    c3_unplanned = UnplannedTask(
        taskId=unplanned_id,
        taskCode="TSK-00106",
        reason="Deferred due to driver shift limit",
    )

    c3_result = FleetRouteResult(
        objective="Dispatch fleet for morning shift",
        dispatchPlans=[c3_plan],
        unplannedTasks=[c3_unplanned],
        warnings=[],
        rationale="Advisory route generation completed",
        sourceTaskPage=1,
        sourceTaskPageSize=20,
        sourceTaskTotalCount=2,
        sourceTaskTotalPages=1,
        advisoryOnly=True,
        status="completed",
    )

    # 1. Direct Python object handoff (Zero manual transformation)
    c4_request_direct = ValidationOperationsRequest(
        objective="Validate morning shift dispatch proposal",
        dispatchPlans=c3_result.dispatch_plans,
        unplannedTasks=c3_result.unplanned_tasks,
    )

    assert len(c4_request_direct.dispatch_plans) == 1
    assert c4_request_direct.dispatch_plans[0].plan_id == "plan-1"
    assert c4_request_direct.dispatch_plans[0].recommended_driver.driver_id == driver_id
    assert c4_request_direct.dispatch_plans[0].recommended_vehicle.vehicle_id == vehicle_id
    assert len(c4_request_direct.unplanned_tasks) == 1
    assert c4_request_direct.unplanned_tasks[0].task_id == unplanned_id

    # 2. JSON-serialized dictionary handoff
    c3_json_dump = c3_result.model_dump(by_alias=True, mode="json")
    c4_request_from_json = ValidationOperationsRequest.model_validate({
        "objective": "Validate morning shift dispatch proposal",
        "dispatchPlans": c3_json_dump["dispatchPlans"],
        "unplannedTasks": c3_json_dump["unplannedTasks"],
    })

    assert c4_request_from_json.dispatch_plans[0].plan_id == "plan-1"
    assert c4_request_from_json.dispatch_plans[0].recommended_driver.display_name == "Sarah Driver"
    assert c4_request_from_json.unplanned_tasks[0].task_code == "TSK-00106"


# ==============================================================================
# Test F: Advisory-Only Guarantee
# ==============================================================================

def test_advisory_only_guarantees():
    # C2 explicitly declares advisoryOnly: Literal[True] = True
    c2_res = CollectionPlanningResult(
        objective="Test C2",
        candidateGroups=[],
        separateHandling=[],
        deferredNeeds=[],
        warnings=[],
        sourcePage=1,
        sourcePageSize=20,
        sourceTotalCount=0,
        sourceTotalPages=0,
        retrievedPages=[1],
        isCompleteSnapshot=True,
        status="empty",
    )
    assert c2_res.advisory_only is True

    # C3 explicitly declares advisoryOnly: Literal[True] = True
    c3_res = FleetRouteResult(
        objective="Test C3",
        dispatchPlans=[],
        unplannedTasks=[],
        warnings=[],
        rationale="None",
        sourceTaskPage=1,
        sourceTaskPageSize=20,
        sourceTaskTotalCount=0,
        sourceTaskTotalPages=0,
        status="empty",
    )
    assert c3_res.advisory_only is True

    # C4 explicitly declares advisoryOnly: Literal[True] = True
    c4_res = ValidationOperationsResult(
        objective="Test C4",
        validationOutcome=None,
        planReviews=[],
        unplannedTaskFindings=[],
        requiresAcknowledgement=False,
        warnings=[],
        summary="Empty",
        status="empty",
    )
    assert c4_res.advisory_only is True

    # Attempting to construct C2, C3, C4 with advisoryOnly=False must fail validation
    with pytest.raises(ValidationError):
        CollectionPlanningResult(
            objective="Test",
            sourcePage=1,
            sourcePageSize=20,
            sourceTotalCount=0,
            sourceTotalPages=0,
            retrievedPages=[1],
            isCompleteSnapshot=True,
            advisoryOnly=False,  # Invalid! Must be True
            status="empty",
        )

    with pytest.raises(ValidationError):
        FleetRouteResult(
            objective="Test",
            rationale="Test",
            sourceTaskPage=1,
            sourceTaskPageSize=20,
            sourceTaskTotalCount=0,
            sourceTaskTotalPages=0,
            advisoryOnly=False,  # Invalid! Must be True
            status="empty",
        )

    with pytest.raises(ValidationError):
        ValidationOperationsResult(
            objective="Test",
            summary="Test",
            advisoryOnly=False,  # Invalid! Must be True
            status="empty",
        )


# ==============================================================================
# Test G: Invalid Outcome / Status Value Protection
# ==============================================================================

def test_invalid_status_and_outcome_protection():
    # C4: validationOutcome rejects unauthorized AI actions like "Approved" or "Rejected"
    with pytest.raises(ValidationError):
        ValidationOperationsResult(
            objective="Test",
            validationOutcome="Approved",  # Disallowed! Only ReadyForHumanReview or NeedsRevision
            summary="Test summary",
            status="completed",
        )

    with pytest.raises(ValidationError):
        ValidationOperationsResult(
            objective="Test",
            validationOutcome="Rejected",  # Disallowed!
            summary="Test summary",
            status="completed",
        )

    # C3: FleetCompatibilityStatus rejects invalid enum values
    with pytest.raises(ValidationError):
        RecommendationCompatibility(
            status="MaybeCompatible",  # Disallowed! Only Compatible, Unknown, Incompatible
            requiresAcknowledgement=False,
            issues=[],
        )

    # C2: status rejects invalid status literals
    with pytest.raises(ValidationError):
        CollectionPlanningResult(
            objective="Test",
            sourcePage=1,
            sourcePageSize=20,
            sourceTotalCount=0,
            sourceTotalPages=0,
            retrievedPages=[1],
            isCompleteSnapshot=True,
            status="failed",  # Disallowed! Only completed, partial, empty
        )

    # C4: Finding severity rejects unknown values
    with pytest.raises(ValidationError):
        ValidationFinding(
            code="ERR_01",
            severity="Critical",  # Disallowed! Only Info, Warning, Error
            message="Critical test failure",
        )
