from uuid import UUID, uuid4
import pytest
from pydantic import ValidationError

from app.models.shared_planner import (
    PlannerValidationError,
    SharedPlannerRequest,
    SharedPlannerResult,
    SharedPlannerStep,
    SpecialistType,
    create_flagship_planner_result,
    has_dependency_path_to_specialist,
    validate_planner_result,
)


class TestSharedPlannerContract:
    """Test suite verifying the Shared Planner data models, presence rules, and deterministic DAG validation."""

    def test_flagship_planner_result_creation_and_validation(self):
        """Flagship 4-step canonical plan is constructed and passes validation."""
        result = create_flagship_planner_result(objective="Optimize City Center Waste Collection")

        assert result.advisory_only is True
        assert result.agent_name == "shared_planner_agent"
        assert result.status == "completed"
        assert len(result.steps) == 4

        # Verify step order
        assert result.steps[0].specialist == SpecialistType.WasteAnalysis
        assert result.steps[1].specialist == SpecialistType.CollectionPlanning
        assert result.steps[2].specialist == SpecialistType.FleetRoute
        assert result.steps[3].specialist == SpecialistType.ValidationOperations

        # Verify dependencies
        assert result.steps[0].depends_on == []
        assert result.steps[1].depends_on == ["step-1"]
        assert result.steps[2].depends_on == ["step-2"]
        assert result.steps[3].depends_on == ["step-3"]

        # Validate pure rules
        validate_planner_result(result)

    def test_serialization_camel_case_aliases(self):
        """SharedPlannerResult serializes to JSON with canonical camelCase aliases."""
        result = create_flagship_planner_result(objective="Test camelCase aliases")
        dumped = result.model_dump(by_alias=True)

        assert "advisoryOnly" in dumped
        assert dumped["advisoryOnly"] is True
        assert "agentName" in dumped
        assert "modelName" in dumped
        assert "steps" in dumped

        step_dump = dumped["steps"][0]
        assert "stepId" in step_dump
        assert "specialist" in step_dump
        assert "objective" in step_dump
        assert "dependsOn" in step_dump
        assert "sequence" in step_dump

        # JSON string roundtrip
        json_str = result.model_dump_json(by_alias=True)
        assert '"advisoryOnly":true' in json_str or '"advisoryOnly": true' in json_str
        assert '"stepId":' in json_str

        # Validate deserialization back to model
        rehydrated = SharedPlannerResult.model_validate_json(json_str)
        assert len(rehydrated.steps) == 4
        assert rehydrated.steps[0].step_id == "step-1"

    def test_shared_planner_request_validation(self):
        """SharedPlannerRequest requires a non-blank objective and parses UUID."""
        req_id = uuid4()
        req = SharedPlannerRequest(
            objective="Plan waste collection for district 4",
            workflowId=req_id,
        )
        assert req.objective == "Plan waste collection for district 4"
        assert req.workflow_id == req_id

        # String UUID parsing
        req_str = SharedPlannerRequest(
            objective="Plan waste collection for district 4",
            workflowId=str(req_id),
        )
        assert req_str.workflow_id == req_id

    def test_objective_min_valid_and_too_short(self):
        """Objective minimum valid length is 5 characters; < 5 or whitespace is rejected."""
        valid_min = "12345"  # exactly 5 characters
        req = SharedPlannerRequest(objective=valid_min)
        assert req.objective == valid_min

        plan = SharedPlannerResult(
            objective=valid_min,
            steps=[
                SharedPlannerStep(
                    stepId="step-1",
                    specialist=SpecialistType.WasteAnalysis,
                    objective="Waste analysis",
                    dependsOn=[],
                    sequence=1,
                )
            ],
            summary="Valid minimum objective plan",
        )
        assert plan.objective == valid_min

        with pytest.raises(ValidationError):
            SharedPlannerRequest(objective="1234")  # 4 chars

        with pytest.raises(ValidationError):
            SharedPlannerRequest(objective="   ")

        with pytest.raises(ValidationError):
            SharedPlannerResult(
                objective="1234",
                steps=[
                    SharedPlannerStep(
                        stepId="step-1",
                        specialist=SpecialistType.WasteAnalysis,
                        objective="Waste analysis",
                        dependsOn=[],
                        sequence=1,
                    )
                ],
                summary="Too short",
            )

    def test_objective_max_authoritative_and_over_limit(self):
        """Objective maximum aligns with authoritative ASP.NET Core constraint (1000 characters).

        Inspected from ASP.NET Core:
        - SmartWaste.Application.Workflow.Validation.CreateAgentWorkflowRequestValidator (MaximumLength(1000))
        - SmartWaste.Infrastructure.Workflow.Configurations.AgentWorkflowConfiguration (HasMaxLength(1000))
        """
        valid_max_1000 = "A" * 1000  # exactly 1000 characters
        req = SharedPlannerRequest(objective=valid_max_1000)
        assert len(req.objective) == 1000

        plan = SharedPlannerResult(
            objective=valid_max_1000,
            steps=[
                SharedPlannerStep(
                    stepId="step-1",
                    specialist=SpecialistType.WasteAnalysis,
                    objective="Waste analysis",
                    dependsOn=[],
                    sequence=1,
                )
            ],
            summary="Plan with 1000-char objective",
        )
        assert len(plan.objective) == 1000

        # 1001 characters should fail validation
        over_limit_1001 = "A" * 1001
        with pytest.raises(ValidationError):
            SharedPlannerRequest(objective=over_limit_1001)

        with pytest.raises(ValidationError):
            SharedPlannerResult(
                objective=over_limit_1001,
                steps=[
                    SharedPlannerStep(
                        stepId="step-1",
                        specialist=SpecialistType.WasteAnalysis,
                        objective="Waste analysis",
                        dependsOn=[],
                        sequence=1,
                    )
                ],
                summary="Plan with 1001-char objective",
            )

    def test_empty_steps_rejected(self):
        """Planner result with empty steps list is rejected."""
        with pytest.raises(ValidationError):
            SharedPlannerResult(
                objective="Valid objective string",
                steps=[],
                summary="Empty plan",
            )

    def test_duplicate_step_ids_rejected(self):
        """Planner result with duplicate step IDs is rejected by validation."""
        with pytest.raises((ValidationError, PlannerValidationError)) as exc:
            SharedPlannerResult(
                objective="Valid objective string",
                steps=[
                    SharedPlannerStep(
                        stepId="step-1",
                        specialist=SpecialistType.WasteAnalysis,
                        objective="First analysis step",
                        dependsOn=[],
                        sequence=1,
                    ),
                    SharedPlannerStep(
                        stepId="step-1",  # duplicate ID
                        specialist=SpecialistType.CollectionPlanning,
                        objective="Second planning step",
                        dependsOn=[],
                        sequence=2,
                    ),
                ],
                summary="Duplicate step IDs",
            )
        assert "Duplicate step ID detected" in str(exc.value) or "Duplicate step" in str(exc.value)

    def test_unknown_dependency_rejected(self):
        """Step depending on a non-existent step ID is rejected."""
        with pytest.raises((ValidationError, PlannerValidationError)) as exc:
            SharedPlannerResult(
                objective="Valid objective string",
                steps=[
                    SharedPlannerStep(
                        stepId="step-1",
                        specialist=SpecialistType.WasteAnalysis,
                        objective="First step",
                        dependsOn=["step-99"],  # unknown
                        sequence=1,
                    ),
                ],
                summary="Unknown dependency",
            )
        assert "depends on unknown step 'step-99'" in str(exc.value)

    def test_self_dependency_rejected(self):
        """Step depending on itself is rejected."""
        with pytest.raises((ValidationError, PlannerValidationError)) as exc:
            SharedPlannerResult(
                objective="Valid objective string",
                steps=[
                    SharedPlannerStep(
                        stepId="step-1",
                        specialist=SpecialistType.WasteAnalysis,
                        objective="First step",
                        dependsOn=["step-1"],  # self-dependency
                        sequence=1,
                    ),
                ],
                summary="Self dependency",
            )
        assert "self-dependency" in str(exc.value)

    def test_cyclic_dependency_rejected_direct(self):
        """Direct 2-step cycle (A -> B -> A) is detected and rejected."""
        with pytest.raises((ValidationError, PlannerValidationError)) as exc:
            SharedPlannerResult(
                objective="Valid objective string",
                steps=[
                    SharedPlannerStep(
                        stepId="step-1",
                        specialist=SpecialistType.WasteAnalysis,
                        objective="First step",
                        dependsOn=["step-2"],
                        sequence=1,
                    ),
                    SharedPlannerStep(
                        stepId="step-2",
                        specialist=SpecialistType.CollectionPlanning,
                        objective="Second step",
                        dependsOn=["step-1"],
                        sequence=2,
                    ),
                ],
                summary="Direct cycle",
            )
        assert "Dependency cycle detected" in str(exc.value) or "Cyclic dependency" in str(exc.value)

    def test_cyclic_dependency_rejected_transitive(self):
        """Transitive 3-step cycle (A -> B -> C -> A) is detected and rejected."""
        with pytest.raises((ValidationError, PlannerValidationError)) as exc:
            SharedPlannerResult(
                objective="Valid objective string",
                steps=[
                    SharedPlannerStep(
                        stepId="step-1",
                        specialist=SpecialistType.WasteAnalysis,
                        objective="First step",
                        dependsOn=["step-3"],
                        sequence=1,
                    ),
                    SharedPlannerStep(
                        stepId="step-2",
                        specialist=SpecialistType.CollectionPlanning,
                        objective="Second step",
                        dependsOn=["step-1"],
                        sequence=2,
                    ),
                    SharedPlannerStep(
                        stepId="step-3",
                        specialist=SpecialistType.FleetRoute,
                        objective="Third step",
                        dependsOn=["step-2"],
                        sequence=3,
                    ),
                ],
                summary="Transitive cycle",
            )
        assert "Dependency cycle detected" in str(exc.value) or "Cyclic dependency" in str(exc.value)

    def test_non_contiguous_sequence_numbers_rejected(self):
        """Sequence numbers must be contiguous starting from 1 if provided."""
        with pytest.raises((ValidationError, PlannerValidationError)) as exc:
            SharedPlannerResult(
                objective="Valid objective string",
                steps=[
                    SharedPlannerStep(
                        stepId="step-1",
                        specialist=SpecialistType.WasteAnalysis,
                        objective="First step",
                        dependsOn=[],
                        sequence=1,
                    ),
                    SharedPlannerStep(
                        stepId="step-2",
                        specialist=SpecialistType.CollectionPlanning,
                        objective="Second step",
                        dependsOn=["step-1"],
                        sequence=3,  # skipped 2
                    ),
                ],
                summary="Skipped sequence",
            )
        assert "contiguous from 1 to 2" in str(exc.value) or "contiguous 1..2" in str(exc.value)

    def test_domain_ordering_fleet_before_collection_rejected(self):
        """FleetRoute cannot be scheduled before CollectionPlanning without dependency."""
        with pytest.raises((ValidationError, PlannerValidationError)) as exc:
            SharedPlannerResult(
                objective="Valid objective string",
                steps=[
                    SharedPlannerStep(
                        stepId="step-1",
                        specialist=SpecialistType.FleetRoute,
                        objective="Fleet routing",
                        dependsOn=[],
                        sequence=1,
                    ),
                    SharedPlannerStep(
                        stepId="step-2",
                        specialist=SpecialistType.CollectionPlanning,
                        objective="Collection planning",
                        dependsOn=[],
                        sequence=2,
                    ),
                ],
                summary="Invalid domain order",
            )
        assert (
            "must have a direct or transitive dependency on a CollectionPlanning step" in str(exc.value)
            or "FleetRoute cannot execute before CollectionPlanning" in str(exc.value)
        )

    def test_domain_ordering_validation_before_fleet_rejected(self):
        """ValidationOperations cannot be scheduled before FleetRoute without dependency."""
        with pytest.raises((ValidationError, PlannerValidationError)) as exc:
            SharedPlannerResult(
                objective="Valid objective string",
                steps=[
                    SharedPlannerStep(
                        stepId="step-1",
                        specialist=SpecialistType.CollectionPlanning,
                        objective="Collection planning",
                        dependsOn=[],
                        sequence=1,
                    ),
                    SharedPlannerStep(
                        stepId="step-2",
                        specialist=SpecialistType.ValidationOperations,
                        objective="Validation",
                        dependsOn=[],
                        sequence=2,
                    ),
                    SharedPlannerStep(
                        stepId="step-3",
                        specialist=SpecialistType.FleetRoute,
                        objective="Fleet routing",
                        dependsOn=["step-1"],
                        sequence=3,
                    ),
                ],
                summary="Invalid domain order",
            )
        assert (
            "must have a direct or transitive dependency on a FleetRoute step" in str(exc.value)
            or "ValidationOperations cannot execute before FleetRoute" in str(exc.value)
        )

    def test_domain_ordering_waste_analysis_downstream_dependency_rejected(self):
        """WasteAnalysis cannot depend on downstream specialists."""
        with pytest.raises((ValidationError, PlannerValidationError)) as exc:
            SharedPlannerResult(
                objective="Valid objective string",
                steps=[
                    SharedPlannerStep(
                        stepId="step-1",
                        specialist=SpecialistType.CollectionPlanning,
                        objective="Collection planning",
                        dependsOn=[],
                        sequence=1,
                    ),
                    SharedPlannerStep(
                        stepId="step-2",
                        specialist=SpecialistType.WasteAnalysis,
                        objective="Waste analysis",
                        dependsOn=["step-1"],  # depends on downstream
                        sequence=2,
                    ),
                ],
                summary="Downstream dependency",
            )
        assert "cannot depend on downstream specialist" in str(exc.value)

    # =========================================================================
    # Step 9A-B Hardened Tests: Specialist Presence & Dependency Path Rules
    # =========================================================================

    def test_fleet_without_collection_planning_rejected(self):
        """FleetRoute requires CollectionPlanning to be present in the execution plan."""
        with pytest.raises((ValidationError, PlannerValidationError)) as exc:
            SharedPlannerResult(
                objective="Dispatch fleet directly without collection planning",
                steps=[
                    SharedPlannerStep(
                        stepId="step-1",
                        specialist=SpecialistType.FleetRoute,
                        objective="Fleet routing alone",
                        dependsOn=[],
                        sequence=1,
                    ),
                ],
                summary="Fleet route alone",
            )
        assert "Specialist 'FleetRoute' requires 'CollectionPlanning' to be present" in str(exc.value)

    def test_validation_without_fleet_route_rejected(self):
        """ValidationOperations requires FleetRoute to be present in the execution plan."""
        with pytest.raises((ValidationError, PlannerValidationError)) as exc:
            SharedPlannerResult(
                objective="Validate operations without fleet route",
                steps=[
                    SharedPlannerStep(
                        stepId="step-1",
                        specialist=SpecialistType.ValidationOperations,
                        objective="Validation operations alone",
                        dependsOn=[],
                        sequence=1,
                    ),
                ],
                summary="Validation alone",
            )
        assert "Specialist 'ValidationOperations' requires 'FleetRoute' to be present" in str(exc.value)

    def test_collection_and_validation_without_fleet_route_rejected(self):
        """CollectionPlanning + ValidationOperations without FleetRoute fails presence check."""
        with pytest.raises((ValidationError, PlannerValidationError)) as exc:
            SharedPlannerResult(
                objective="Plan collection and validate without fleet",
                steps=[
                    SharedPlannerStep(
                        stepId="step-1",
                        specialist=SpecialistType.CollectionPlanning,
                        objective="Collection planning",
                        dependsOn=[],
                        sequence=1,
                    ),
                    SharedPlannerStep(
                        stepId="step-2",
                        specialist=SpecialistType.ValidationOperations,
                        objective="Validation operations",
                        dependsOn=["step-1"],
                        sequence=2,
                    ),
                ],
                summary="Missing FleetRoute",
            )
        assert "Specialist 'ValidationOperations' requires 'FleetRoute' to be present" in str(exc.value)

    def test_fleet_present_but_not_dependent_on_collection_rejected(self):
        """FleetRoute present with CollectionPlanning but lacking dependency path must fail."""
        with pytest.raises((ValidationError, PlannerValidationError)) as exc:
            SharedPlannerResult(
                objective="Collection planning and fleet route independent",
                steps=[
                    SharedPlannerStep(
                        stepId="step-1",
                        specialist=SpecialistType.CollectionPlanning,
                        objective="Collection planning",
                        dependsOn=[],
                        sequence=1,
                    ),
                    SharedPlannerStep(
                        stepId="step-2",
                        specialist=SpecialistType.FleetRoute,
                        objective="Fleet routing with no dependency on collection",
                        dependsOn=[],  # no dependency on step-1!
                        sequence=2,
                    ),
                ],
                summary="FleetRoute not dependent on CollectionPlanning",
            )
        assert "must have a direct or transitive dependency on a CollectionPlanning step" in str(exc.value)

    def test_validation_present_but_not_dependent_on_fleet_rejected(self):
        """ValidationOperations present with FleetRoute but lacking dependency path must fail."""
        with pytest.raises((ValidationError, PlannerValidationError)) as exc:
            SharedPlannerResult(
                objective="Validation present but not dependent on FleetRoute",
                steps=[
                    SharedPlannerStep(
                        stepId="step-1",
                        specialist=SpecialistType.CollectionPlanning,
                        objective="Collection planning",
                        dependsOn=[],
                        sequence=1,
                    ),
                    SharedPlannerStep(
                        stepId="step-2",
                        specialist=SpecialistType.FleetRoute,
                        objective="Fleet routing",
                        dependsOn=["step-1"],
                        sequence=2,
                    ),
                    SharedPlannerStep(
                        stepId="step-3",
                        specialist=SpecialistType.ValidationOperations,
                        objective="Validation depending on collection planning instead of fleet",
                        dependsOn=["step-1"],  # depends on C2, not C3!
                        sequence=3,
                    ),
                ],
                summary="ValidationOperations not dependent on FleetRoute",
            )
        assert "must have a direct or transitive dependency on a FleetRoute step" in str(exc.value)

    def test_valid_transitive_dependency_path(self):
        """Transitive dependency helper correctly discovers multi-hop dependencies."""
        step_1 = SharedPlannerStep(
            stepId="step-1",
            specialist=SpecialistType.CollectionPlanning,
            objective="Collection planning",
            dependsOn=[],
            sequence=1,
        )
        step_2 = SharedPlannerStep(
            stepId="step-2",
            specialist=SpecialistType.FleetRoute,
            objective="Fleet routing",
            dependsOn=["step-1"],
            sequence=2,
        )
        step_3 = SharedPlannerStep(
            stepId="step-3",
            specialist=SpecialistType.ValidationOperations,
            objective="Validation operations",
            dependsOn=["step-2"],
            sequence=3,
        )

        step_map = {"step-1": step_1, "step-2": step_2, "step-3": step_3}

        # Step 3 directly depends on FleetRoute (step-2)
        assert has_dependency_path_to_specialist("step-3", SpecialistType.FleetRoute, step_map) is True
        # Step 3 transitively depends on CollectionPlanning (step-1 via step-2)
        assert has_dependency_path_to_specialist("step-3", SpecialistType.CollectionPlanning, step_map) is True
        # Step 2 directly depends on CollectionPlanning (step-1)
        assert has_dependency_path_to_specialist("step-2", SpecialistType.CollectionPlanning, step_map) is True
        # Step 1 has no dependencies
        assert has_dependency_path_to_specialist("step-1", SpecialistType.WasteAnalysis, step_map) is False

    def test_status_strictness(self):
        """SharedPlannerResult.status accepts only 'completed'; others fail validation."""
        # Default status omitted -> "completed"
        default_plan = create_flagship_planner_result(objective="Status default check")
        assert default_plan.status == "completed"

        # Explicit status="completed" -> accepted
        explicit_plan = SharedPlannerResult(
            objective="Status explicit check",
            steps=[
                SharedPlannerStep(
                    stepId="step-1",
                    specialist=SpecialistType.WasteAnalysis,
                    objective="Waste analysis",
                    dependsOn=[],
                    sequence=1,
                )
            ],
            summary="Completed plan",
            status="completed",
        )
        assert explicit_plan.status == "completed"

        # Invalid statuses -> ValidationError
        with pytest.raises(ValidationError):
            SharedPlannerResult(
                objective="Status invalid check",
                steps=[
                    SharedPlannerStep(
                        stepId="step-1",
                        specialist=SpecialistType.WasteAnalysis,
                        objective="Waste analysis",
                        dependsOn=[],
                        sequence=1,
                    )
                ],
                summary="Invalid status plan",
                status="success",  # not allowed
            )

        with pytest.raises(ValidationError):
            SharedPlannerResult(
                objective="Status invalid check",
                steps=[
                    SharedPlannerStep(
                        stepId="step-1",
                        specialist=SpecialistType.WasteAnalysis,
                        objective="Waste analysis",
                        dependsOn=[],
                        sequence=1,
                    )
                ],
                summary="Invalid status plan",
                status="failed",  # not allowed
            )

        with pytest.raises(ValidationError):
            SharedPlannerResult(
                objective="Status invalid check",
                steps=[
                    SharedPlannerStep(
                        stepId="step-1",
                        specialist=SpecialistType.WasteAnalysis,
                        objective="Waste analysis",
                        dependsOn=[],
                        sequence=1,
                    )
                ],
                summary="Invalid status plan",
                status="empty",  # not allowed
            )

    def test_valid_subsets_accepted(self):
        """Valid sub-plans (WasteAnalysis alone, CollectionPlanning alone, C1+C2, C2+C3+C4) are accepted."""
        # 1. Single step WasteAnalysis
        c1_alone = SharedPlannerResult(
            objective="Analyze reported waste images only",
            steps=[
                SharedPlannerStep(
                    stepId="step-1",
                    specialist=SpecialistType.WasteAnalysis,
                    objective="Waste report analysis",
                    dependsOn=[],
                    sequence=1,
                ),
            ],
            summary="Analysis only",
        )
        validate_planner_result(c1_alone)

        # 2. Single step CollectionPlanning (WasteAnalysis is explicitly optional for CollectionPlanning)
        c2_alone = SharedPlannerResult(
            objective="Plan collections from existing verified needs",
            steps=[
                SharedPlannerStep(
                    stepId="step-1",
                    specialist=SpecialistType.CollectionPlanning,
                    objective="Synthesize needs and schedule collection tasks",
                    dependsOn=[],
                    sequence=1,
                ),
            ],
            summary="Collection planning alone",
        )
        validate_planner_result(c2_alone)

        # 3. WasteAnalysis -> CollectionPlanning
        c1_c2_plan = SharedPlannerResult(
            objective="Analyze reports and plan collection needs",
            steps=[
                SharedPlannerStep(
                    stepId="step-1",
                    specialist=SpecialistType.WasteAnalysis,
                    objective="Waste analysis",
                    dependsOn=[],
                    sequence=1,
                ),
                SharedPlannerStep(
                    stepId="step-2",
                    specialist=SpecialistType.CollectionPlanning,
                    objective="Collection planning",
                    dependsOn=["step-1"],
                    sequence=2,
                ),
            ],
            summary="C1 to C2 plan",
        )
        validate_planner_result(c1_c2_plan)

        # 4. CollectionPlanning -> FleetRoute -> ValidationOperations (C2 + C3 + C4 without C1)
        c2_c3_c4_plan = SharedPlannerResult(
            objective="Plan collection needs, route fleet, and validate operations",
            steps=[
                SharedPlannerStep(
                    stepId="step-1",
                    specialist=SpecialistType.CollectionPlanning,
                    objective="Collection planning",
                    dependsOn=[],
                    sequence=1,
                ),
                SharedPlannerStep(
                    stepId="step-2",
                    specialist=SpecialistType.FleetRoute,
                    objective="Fleet routing",
                    dependsOn=["step-1"],
                    sequence=2,
                ),
                SharedPlannerStep(
                    stepId="step-3",
                    specialist=SpecialistType.ValidationOperations,
                    objective="Validation operations",
                    dependsOn=["step-2"],
                    sequence=3,
                ),
            ],
            summary="C2 to C4 plan",
        )
        validate_planner_result(c2_c3_c4_plan)

    @pytest.mark.parametrize(
        "duplicate_specialist",
        [
            SpecialistType.WasteAnalysis,
            SpecialistType.CollectionPlanning,
            SpecialistType.FleetRoute,
            SpecialistType.ValidationOperations,
        ],
    )
    def test_duplicate_specialist_type_rejected(self, duplicate_specialist: SpecialistType):
        """Each SpecialistType may appear at most once in a SharedPlannerResult, even with distinct step IDs."""
        steps = [
            SharedPlannerStep(
                stepId="step-1",
                specialist=duplicate_specialist,
                objective=f"First {duplicate_specialist.value} stage",
                dependsOn=[],
                sequence=1,
            ),
            SharedPlannerStep(
                stepId="step-2",
                specialist=duplicate_specialist,
                objective=f"Second {duplicate_specialist.value} stage",
                dependsOn=["step-1"],
                sequence=2,
            ),
        ]

        with pytest.raises((ValidationError, PlannerValidationError)) as exc_info:
            SharedPlannerResult(
                objective="Test duplicate specialist rejection",
                steps=steps,
            )

        assert f"Specialist '{duplicate_specialist.value}' may appear only once in a Shared Planner result." in str(
            exc_info.value
        )

    def test_duplicate_specialist_bypassing_constructor_rejected_at_validation(self):
        """Constructing via model_construct still fails when validate_planner_result is called."""
        steps = [
            SharedPlannerStep(
                stepId="step-1",
                specialist=SpecialistType.CollectionPlanning,
                objective="Primary collection planning",
                dependsOn=[],
            ),
            SharedPlannerStep(
                stepId="step-2",
                specialist=SpecialistType.CollectionPlanning,
                objective="Secondary collection planning",
                dependsOn=["step-1"],
            ),
        ]
        unvalidated = SharedPlannerResult.model_construct(
            objective="Bypass constructor test",
            steps=steps,
            summary="",
            warnings=[],
            agent_name="shared_planner_agent",
            model_name=None,
            advisory_only=True,
            status="completed",
        )
        with pytest.raises(PlannerValidationError) as exc_info:
            validate_planner_result(unvalidated)

        assert "Specialist 'CollectionPlanning' may appear only once in a Shared Planner result." in str(
            exc_info.value
        )

