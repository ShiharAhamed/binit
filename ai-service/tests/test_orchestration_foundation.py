from datetime import datetime, timezone
from uuid import UUID, uuid4

import pytest
from pydantic import ValidationError

from app.models.analysis import (
    AnalysisConfidence,
    RecommendedPriority,
    WasteAnalysisResult,
    WasteReportAnalysis,
)
from app.models.collection_planning import (
    CandidateCollectionGroup,
    CollectionNeedReference,
    CollectionPlanningResult,
    ProposedSchedule,
)
from app.models.fleet_route import (
    DispatchPlanRecommendation,
    DriverRecommendation,
    FleetRouteResult,
    RecommendedFleetTask,
    UnplannedTask,
    VehicleRecommendation,
)
from app.models.shared_planner import (
    SharedPlannerResult,
    SharedPlannerStep,
    SpecialistType,
    create_flagship_planner_result,
)
from app.orchestration.enums import (
    ApprovalPauseStage,
    GraphNodeId,
    OrchestrationPhase,
    OrchestrationStatus,
    ResumeDecision,
)
from app.orchestration.errors import (
    INTERNAL_ORCHESTRATION_ERROR,
    INVALID_RESUME_CONTEXT,
    OPERATIONAL_VALIDATION_FAILED,
    PLANNER_VALIDATION_FAILED,
    PROVIDER_RATE_LIMIT,
    STATE_INVARIANT_VIOLATION,
    WASTE_ANALYSIS_FAILED,
    OrchestrationError,
    create_orchestration_error,
)
from app.orchestration.graph import (
    build_step9a_skeleton_graph,
    build_workflow_graph,
    deterministic_shared_planner_stub,
    failure_node,
    finish_node,
    route_after_planner,
)
from app.orchestration.state import (
    FORBIDDEN_REASONING_KEYS,
    OrchestrationPauseResult,
    OrchestrationResultEnvelope,
    OrchestrationState,
    StateInvariantError,
    WorkflowResumeContext,
    validate_state_invariants,
)
from app.orchestration.transitions import (
    create_initial_state,
    mark_completed,
    mark_failed,
    mark_paused_for_approval,
    mark_planning_completed,
    mark_planning_started,
    mark_specialist_completed,
    mark_specialist_started,
)


class TestOrchestrationFoundation:
    """Test suite verifying LangGraph orchestration state, transitions, invariants, and execution."""

    def test_create_initial_state_and_invariants(self):
        """Initial state is pristine, unpaused, and satisfies invariants with UUID."""
        wf_id = uuid4()
        state = create_initial_state(workflow_id=wf_id, objective="Manage city waste surge")

        assert state["workflow_id"] == wf_id
        assert state["objective"] == "Manage city waste surge"
        assert state["current_phase"] == OrchestrationPhase.NotStarted
        assert state["current_specialist"] is None
        assert state["completed_specialists"] == []
        assert state["planner_result"] is None
        assert state["waste_analysis_result"] is None
        assert state["collection_planning_result"] is None
        assert state["fleet_route_result"] is None
        assert state["validation_operations_result"] is None
        assert state["pause_stage"] == ApprovalPauseStage.NONE
        assert state["pause_reason"] is None
        assert state["errors"] == []
        assert state["warnings"] == []
        assert state["final_outcome"] is None

        validate_state_invariants(state)

        # String UUID parsing into UUID
        state_str = create_initial_state(workflow_id=str(wf_id), objective="Manage city waste surge")
        assert state_str["workflow_id"] == wf_id

    def test_specialist_slots_and_transitions(self):
        """Specialist lifecycle transitions correctly populate state slots and tracking."""
        state = create_initial_state(workflow_id=uuid4(), objective="Test transitions")

        # Start WasteAnalysis
        update = mark_specialist_started(SpecialistType.WasteAnalysis)
        state.update(update)
        assert state["current_phase"] == OrchestrationPhase.RunningWasteAnalysis
        assert state["current_specialist"] == SpecialistType.WasteAnalysis

        # Mock C1 result
        c1_result = WasteAnalysisResult(
            objective="Analyse verified waste reports",
            analyses=[
                WasteReportAnalysis(
                    reportId=uuid4(),
                    categoryAssessment="Bulky roadside waste",
                    recommendedPriority=RecommendedPriority.HIGH,
                    operationalConcerns=["Sidewalk obstruction"],
                    recommendedHandling="Compactor truck",
                    confidence=AnalysisConfidence.HIGH,
                    rationale="Urgent roadside buildup.",
                )
            ],
            sourcePage=1,
            sourcePageSize=20,
            sourceTotalCount=1,
            agentName="waste_analysis_agent",
            modelName="test-model",
            status="completed",
        )
        update = mark_specialist_completed(
            SpecialistType.WasteAnalysis,
            c1_result,
            state["completed_specialists"],
        )
        state.update(update)
        assert state["waste_analysis_result"] == c1_result
        assert state["completed_specialists"] == [SpecialistType.WasteAnalysis]
        assert state["current_specialist"] is None

        # Start and complete C2 CollectionPlanning
        update = mark_specialist_started(SpecialistType.CollectionPlanning)
        state.update(update)
        c2_result = CollectionPlanningResult(
            objective="Plan collection needs",
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
            agentName="collection_planning_agent",
            modelName="test-model",
            advisoryOnly=True,
            status="empty",
        )
        update = mark_specialist_completed(
            SpecialistType.CollectionPlanning,
            c2_result,
            state["completed_specialists"],
        )
        state.update(update)
        assert state["collection_planning_result"] == c2_result
        assert state["completed_specialists"] == [
            SpecialistType.WasteAnalysis,
            SpecialistType.CollectionPlanning,
        ]

        # Invariants should pass
        validate_state_invariants(state)

    def test_pause_for_collection_approval(self):
        """Pause transition sets pause stage and reason for human MunicipalManager or WasteOfficer collection approval."""
        state = create_initial_state(workflow_id=uuid4(), objective="Test pause")
        update = mark_paused_for_approval(
            approval_stage=ApprovalPauseStage.COLLECTION_PLANNING,
            message="Collection plan requires MunicipalManager or WasteOfficer approval.",
        )
        state.update(update)

        assert state["current_phase"] == OrchestrationPhase.PausedForCollectionApproval
        assert state["pause_stage"] == ApprovalPauseStage.COLLECTION_PLANNING
        assert state["pause_reason"] == "Collection plan requires MunicipalManager or WasteOfficer approval."
        assert state["current_specialist"] is None

        validate_state_invariants(state)

    def test_pause_for_fleet_dispatch_approval(self):
        """Pause transition sets dispatch stage and reason; Step 9D validates required C3/C4 outputs separately."""
        state = create_initial_state(workflow_id=uuid4(), objective="Test pause")
        update = mark_paused_for_approval(
            approval_stage=ApprovalPauseStage.FLEET_DISPATCH,
            message="Fleet dispatch plan requires MunicipalManager or WasteOfficer approval.",
        )
        state.update(update)

        assert state["current_phase"] == OrchestrationPhase.PausedForDispatchApproval
        assert state["pause_stage"] == ApprovalPauseStage.FLEET_DISPATCH
        assert state["pause_reason"] == "Fleet dispatch plan requires MunicipalManager or WasteOfficer approval."


    def test_pause_for_none_rejected(self):
        """Cannot pause for ApprovalPauseStage.NONE."""
        with pytest.raises(ValueError):
            mark_paused_for_approval(
                approval_stage=ApprovalPauseStage.NONE,
                message="Invalid pause",
            )

    def test_invariants_reject_forbidden_reasoning_keys(self):
        """State containing hidden CoT / scratchpad keys is rejected."""
        for forbidden_key in FORBIDDEN_REASONING_KEYS:
            state = create_initial_state(workflow_id=uuid4(), objective="Test CoT rejection")
            state[forbidden_key] = "Hidden thoughts..."  # type: ignore
            with pytest.raises(StateInvariantError) as exc:
                validate_state_invariants(state)
            assert f"Forbidden hidden reasoning key '{forbidden_key}'" in str(exc.value)

    def test_invariants_completed_state_checks(self):
        """Completed state cannot have active pause or active specialist."""
        state = create_initial_state(workflow_id=uuid4(), objective="Test completed invariants")
        state["current_phase"] = OrchestrationPhase.Completed
        state["pause_stage"] = ApprovalPauseStage.COLLECTION_PLANNING
        state["pause_reason"] = "Still paused"

        with pytest.raises(StateInvariantError) as exc:
            validate_state_invariants(state)
        assert "cannot have an active pause" in str(exc.value)

        state["pause_stage"] = ApprovalPauseStage.NONE
        state["current_specialist"] = SpecialistType.WasteAnalysis
        with pytest.raises(StateInvariantError) as exc:
            validate_state_invariants(state)
        assert "cannot have an active specialist" in str(exc.value)

    def test_invariants_failed_state_requires_error(self):
        """Failed state must have at least one error recorded."""
        state = create_initial_state(workflow_id=uuid4(), objective="Test failed invariant")
        state["current_phase"] = OrchestrationPhase.Failed
        state["errors"] = []

        with pytest.raises(StateInvariantError) as exc:
            validate_state_invariants(state)
        assert "must contain at least one error" in str(exc.value)

    def test_invariants_specialist_dependency_ordering(self):
        """FleetRoute cannot exist without CollectionPlanningResult."""
        state = create_initial_state(workflow_id=uuid4(), objective="Test order")
        state["fleet_route_result"] = FleetRouteResult(
            objective="Dispatch fleet",
            dispatchPlans=[],
            unplannedTasks=[],
            warnings=[],
            rationale="No tasks to dispatch.",
            sourceTaskPage=1,
            sourceTaskPageSize=20,
            sourceTaskTotalCount=0,
            sourceTaskTotalPages=0,
            agentName="fleet_route_agent",
            modelName="test-model",
            advisoryOnly=True,
            status="empty",
        )
        with pytest.raises(StateInvariantError) as exc:
            validate_state_invariants(state)
        assert "FleetRouteResult cannot exist in state without an existing CollectionPlanningResult" in str(exc.value)

    def test_orchestration_pause_result_model(self):
        """OrchestrationPauseResult validates and serializes with camelCase aliases."""
        pause = OrchestrationPauseResult(
            approvalStage=ApprovalPauseStage.COLLECTION_PLANNING,
            message="Human review required for candidate tasks.",
        )
        assert pause.pause_type == "HumanApproval"
        assert pause.approval_stage == ApprovalPauseStage.COLLECTION_PLANNING

        dumped = pause.model_dump(by_alias=True)
        assert dumped["pauseType"] == "HumanApproval"
        assert dumped["approvalStage"] == "CollectionPlanning"
        assert dumped["nextExpectedAction"] == "ASP.NET human approval"

    def test_workflow_resume_context_model(self):
        """WorkflowResumeContext validates and serializes with camelCase aliases and UUID typing."""
        wf_id = uuid4()
        ctx = WorkflowResumeContext(
            workflowId=wf_id,
            approvalStage=ApprovalPauseStage.COLLECTION_PLANNING,
            decision=ResumeDecision.APPROVED,
            authoritativeExecutionSummary={"createdTaskCount": 3},
        )
        assert ctx.workflow_id == wf_id
        assert ctx.decision == ResumeDecision.APPROVED

        # JSON mode serialization preserves canonical UUID string
        dumped = ctx.model_dump(by_alias=True, mode="json")
        assert dumped["workflowId"] == str(wf_id)
        assert dumped["decision"] == "Approved"
        assert dumped["authoritativeExecutionSummary"] == {"createdTaskCount": 3}

        # Parsing string UUID works
        ctx_str = WorkflowResumeContext(
            workflowId=str(wf_id),
            approvalStage=ApprovalPauseStage.COLLECTION_PLANNING,
            decision=ResumeDecision.APPROVED,
        )
        assert ctx_str.workflow_id == wf_id

        # Arbitrary non-UUID string fails validation
        with pytest.raises(ValidationError):
            WorkflowResumeContext(
                workflowId="hello",
                approvalStage=ApprovalPauseStage.COLLECTION_PLANNING,
                decision=ResumeDecision.APPROVED,
            )

    def test_orchestration_result_envelope_model(self):
        """OrchestrationResultEnvelope validates and serializes top-level execution results with UUID."""
        wf_id = uuid4()
        planner = create_flagship_planner_result(objective="Envelope test")
        env = OrchestrationResultEnvelope(
            workflowId=wf_id,
            status=OrchestrationStatus.COMPLETED,
            currentPhase=OrchestrationPhase.Completed,
            approvalStage=ApprovalPauseStage.NONE,
            plannerResult=planner,
            warnings=["High temperature forecast"],
            finalOutcome="Step 9A orchestration skeleton completed successfully.",
        )
        dumped = env.model_dump(by_alias=True, mode="json")
        assert dumped["workflowId"] == str(wf_id)
        assert dumped["status"] == "Completed"
        assert dumped["currentPhase"] == "Completed"
        assert dumped["plannerResult"] is not None
        assert dumped["warnings"] == ["High temperature forecast"]
        assert dumped["finalOutcome"] == "Step 9A orchestration skeleton completed successfully."

    def test_langgraph_step9a_skeleton_execution_success(self):
        """Compiled Step 9A LangGraph executes deterministically to completion with real UUID."""
        graph = build_workflow_graph()
        wf_id = uuid4()
        initial_state = create_initial_state(
            workflow_id=wf_id,
            objective="SmartWaste End-to-End Orchestration Baseline",
        )

        final_state = graph.invoke(initial_state)

        assert final_state["workflow_id"] == wf_id
        assert final_state["current_phase"] == OrchestrationPhase.Completed
        assert final_state["planner_result"] is not None
        assert len(final_state["planner_result"].steps) == 4
        assert final_state["errors"] == []
        assert final_state["final_outcome"] is not None
        assert "Step 9A orchestration skeleton completed successfully." in final_state["final_outcome"]

        # Validate invariants on output state
        validate_state_invariants(final_state)

    def test_langgraph_step9a_skeleton_execution_failure_routing(self):
        """When planner validation fails, graph routes to failure node and marks Failed."""
        graph = build_workflow_graph()
        wf_id = uuid4()

        # Seed initial state with an invalid plan (self-dependency)
        invalid_plan = SharedPlannerResult.model_construct(
            objective="Valid objective string",
            steps=[
                SharedPlannerStep(
                    stepId="step-1",
                    specialist=SpecialistType.WasteAnalysis,
                    objective="First step",
                    dependsOn=["step-1"],  # invalid self-dependency
                    sequence=1,
                )
            ],
            summary="Invalid self-dependent plan",
            advisoryOnly=True,
            agentName="shared_planner_agent",
            modelName="gemini-2.5-flash",
            status="completed",
            warnings=[],
        )

        initial_state = create_initial_state(
            workflow_id=wf_id,
            objective="Execute invalid plan",
        )
        initial_state["planner_result"] = invalid_plan

        final_state = graph.invoke(initial_state)

        assert final_state["workflow_id"] == wf_id
        assert final_state["current_phase"] == OrchestrationPhase.Failed
        assert len(final_state["errors"]) >= 1
        assert final_state["errors"][0].code == PLANNER_VALIDATION_FAILED
        assert "self-dependency" in final_state["errors"][0].message
        assert final_state["final_outcome"] is not None
        assert "terminated due to orchestration error" in final_state["final_outcome"]

        # Validate invariants on failed state
        validate_state_invariants(final_state)

    def test_langgraph_step9a_skeleton_execution_duplicate_specialist_failure_routing(self):
        """When an invalid duplicate-specialist plan enters the graph, it is caught at validation and routes to failure."""
        graph = build_workflow_graph()
        wf_id = uuid4()

        # Seed initial state with a plan containing duplicate CollectionPlanning steps constructed via model_construct
        duplicate_specialist_plan = SharedPlannerResult.model_construct(
            objective="Valid objective string",
            steps=[
                SharedPlannerStep(
                    stepId="step-1",
                    specialist=SpecialistType.CollectionPlanning,
                    objective="Primary collection planning",
                    dependsOn=[],
                    sequence=1,
                ),
                SharedPlannerStep(
                    stepId="step-2",
                    specialist=SpecialistType.CollectionPlanning,
                    objective="Duplicate collection planning stage",
                    dependsOn=["step-1"],
                    sequence=2,
                ),
            ],
            summary="Invalid duplicate specialist plan",
            advisoryOnly=True,
            agentName="shared_planner_agent",
            modelName="gemini-2.5-flash",
            status="completed",
            warnings=[],
        )

        initial_state = create_initial_state(
            workflow_id=wf_id,
            objective="Execute duplicate specialist plan",
        )
        initial_state["planner_result"] = duplicate_specialist_plan

        final_state = graph.invoke(initial_state)

        assert final_state["workflow_id"] == wf_id
        assert final_state["current_phase"] == OrchestrationPhase.Failed
        assert len(final_state["errors"]) >= 1
        assert final_state["errors"][0].code == PLANNER_VALIDATION_FAILED
        assert "Specialist 'CollectionPlanning' may appear only once in a Shared Planner result." in final_state["errors"][0].message
        assert final_state["final_outcome"] is not None
        assert "terminated due to orchestration error" in final_state["final_outcome"]

        # Validate invariants on failed state
        validate_state_invariants(final_state)

