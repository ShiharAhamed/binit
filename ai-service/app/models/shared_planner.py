from enum import Enum
from typing import List, Literal, Optional
from uuid import UUID

from pydantic import BaseModel, ConfigDict, Field, model_validator


class SpecialistType(str, Enum):
    """Allowed AI specialist agent identifiers for Shared Planner delegation.

    Authoritative ASP.NET business actions (such as CollectionTaskCreation,
    CollectionAssignmentCreation, or HumanApproval) MUST NOT be included here.
    """

    WasteAnalysis = "WasteAnalysis"
    CollectionPlanning = "CollectionPlanning"
    FleetRoute = "FleetRoute"
    ValidationOperations = "ValidationOperations"


class SharedPlannerStep(BaseModel):
    """A single specialist delegation step produced by the Shared Planner."""

    model_config = ConfigDict(populate_by_name=True, extra="forbid", str_strip_whitespace=True)

    step_id: str = Field(
        ...,
        alias="stepId",
        min_length=1,
        max_length=50,
        description="Stable deterministic planner-local ID (e.g. 'step-1').",
    )
    specialist: SpecialistType = Field(
        ...,
        description="Target AI specialist agent for this execution stage.",
    )
    objective: str = Field(
        ...,
        min_length=5,
        max_length=1000,
        description="Concise specialist-level operational objective.",
    )
    depends_on: List[str] = Field(
        default_factory=list,
        alias="dependsOn",
        description="List of stepId values that must complete before this step.",
    )
    sequence: Optional[int] = Field(
        default=None,
        ge=1,
        description="Optional contiguous 1-indexed execution sequence.",
    )


class SharedPlannerRequest(BaseModel):
    """Input contract for invoking the Shared Planner."""

    model_config = ConfigDict(populate_by_name=True, extra="forbid", str_strip_whitespace=True)

    objective: str = Field(
        ...,
        min_length=5,
        max_length=1000,
        description="High-level domain objective for end-to-end collection and dispatch planning.",
    )
    workflow_id: Optional[UUID] = Field(
        default=None,
        alias="workflowId",
        description="Authoritative ASP.NET AgentWorkflow ID for correlation.",
    )


class PlannerValidationError(ValueError):
    """Raised when a SharedPlannerResult violates deterministic plan invariants."""

    pass


def has_dependency_path_to_specialist(
    step_id: str,
    target_specialist: SpecialistType,
    step_map: dict[str, SharedPlannerStep],
    visited: Optional[set[str]] = None,
) -> bool:
    """Traverses depends_on relationships to check for a direct or transitive dependency on a target specialist."""
    if visited is None:
        visited = set()
    if step_id in visited:
        return False
    visited.add(step_id)

    step = step_map.get(step_id)
    if not step:
        return False

    for dep_id in step.depends_on:
        dep_step = step_map.get(dep_id)
        if not dep_step:
            continue
        if dep_step.specialist == target_specialist:
            return True
        if has_dependency_path_to_specialist(dep_id, target_specialist, step_map, visited):
            return True

    return False


def validate_planner_result(result: "SharedPlannerResult") -> None:
    """Deterministically validates a SharedPlannerResult without LLM assistance.

    Enforces:
    1. At least one planner step.
    2. Unique step IDs.
    3. Unique specialist types (at most one step per SpecialistType).
    4. Non-blank step objectives.
    5. All dependencies reference known step IDs.
    6. No self-dependencies.
    7. No duplicate dependency IDs in depends_on.
    8. No dependency cycles (via topological sort).
    9. Contiguous sequences 1..N if sequence numbers are provided.
    10. Required specialist presence:
       - FleetRoute requires CollectionPlanning to be present.
       - ValidationOperations requires FleetRoute to be present.
    11. Required direct or transitive dependency paths:
       - FleetRoute must depend directly or transitively on CollectionPlanning.
       - ValidationOperations must depend directly or transitively on FleetRoute.
    12. Domain dependency ordering:
       - FleetRoute cannot run before or be depended on by CollectionPlanning.
       - ValidationOperations cannot run before or be depended on by FleetRoute.
       - WasteAnalysis cannot depend on downstream planning stages.
    """
    if not result.steps:
        raise PlannerValidationError("Shared planner plan must contain at least one step.")

    step_ids: set[str] = set()
    specialists_seen: set[SpecialistType] = set()
    step_map: dict[str, SharedPlannerStep] = {}

    for step in result.steps:
        if step.step_id in step_ids:
            raise PlannerValidationError(f"Duplicate step ID detected: '{step.step_id}'.")
        step_ids.add(step.step_id)

        if step.specialist in specialists_seen:
            raise PlannerValidationError(
                f"Specialist '{step.specialist.value}' may appear only once in a Shared Planner result."
            )
        specialists_seen.add(step.specialist)

        step_map[step.step_id] = step

        if not step.objective or not step.objective.strip():
            raise PlannerValidationError(f"Step '{step.step_id}' has a blank objective.")

    # Dependency existence, self-dependency, duplicate dependency checks
    for step in result.steps:
        seen_deps: set[str] = set()
        for dep in step.depends_on:
            if dep == step.step_id:
                raise PlannerValidationError(f"Step '{step.step_id}' has a self-dependency.")
            if dep not in step_ids:
                raise PlannerValidationError(
                    f"Step '{step.step_id}' depends on unknown step '{dep}'."
                )
            if dep in seen_deps:
                raise PlannerValidationError(
                    f"Step '{step.step_id}' contains duplicate dependency '{dep}'."
                )
            seen_deps.add(dep)

    # Sequence validation if provided
    has_sequences = any(s.sequence is not None for s in result.steps)
    if has_sequences:
        seqs = [s.sequence for s in result.steps]
        if any(s is None for s in seqs):
            raise PlannerValidationError(
                "If sequence is provided for any step, it must be provided for all steps."
            )
        expected_seqs = list(range(1, len(result.steps) + 1))
        if [s for s in seqs] != expected_seqs:
            raise PlannerValidationError(
                f"Step sequences must be contiguous 1..{len(result.steps)} matching array order."
            )

    # Cycle detection via Kahn's algorithm (topological sort)
    in_degree: dict[str, int] = {sid: 0 for sid in step_ids}
    adj: dict[str, list[str]] = {sid: [] for sid in step_ids}

    for step in result.steps:
        for dep in step.depends_on:
            adj[dep].append(step.step_id)
            in_degree[step.step_id] += 1

    queue = [sid for sid, deg in in_degree.items() if deg == 0]
    visited_count = 0
    topological_order: list[str] = []

    while queue:
        curr = queue.pop(0)
        visited_count += 1
        topological_order.append(curr)
        for neighbor in adj[curr]:
            in_degree[neighbor] -= 1
            if in_degree[neighbor] == 0:
                queue.append(neighbor)

    if visited_count != len(step_ids):
        raise PlannerValidationError("Dependency cycle detected in Shared Planner steps.")

    # Required specialist presence rules
    present_specialists = {s.specialist for s in result.steps}

    if SpecialistType.FleetRoute in present_specialists:
        if SpecialistType.CollectionPlanning not in present_specialists:
            raise PlannerValidationError(
                "Specialist 'FleetRoute' requires 'CollectionPlanning' to be present in the execution plan."
            )

    if SpecialistType.ValidationOperations in present_specialists:
        if SpecialistType.FleetRoute not in present_specialists:
            raise PlannerValidationError(
                "Specialist 'ValidationOperations' requires 'FleetRoute' to be present in the execution plan."
            )

    # Required direct or transitive dependency paths
    for step in result.steps:
        if step.specialist == SpecialistType.FleetRoute:
            if not has_dependency_path_to_specialist(step.step_id, SpecialistType.CollectionPlanning, step_map):
                raise PlannerValidationError(
                    f"FleetRoute step '{step.step_id}' must have a direct or transitive dependency on a CollectionPlanning step."
                )

        if step.specialist == SpecialistType.ValidationOperations:
            if not has_dependency_path_to_specialist(step.step_id, SpecialistType.FleetRoute, step_map):
                raise PlannerValidationError(
                    f"ValidationOperations step '{step.step_id}' must have a direct or transitive dependency on a FleetRoute step."
                )

    # Domain dependency order rules
    # Map each step to its specialist
    topological_specialists = [step_map[sid].specialist for sid in topological_order]

    for step in result.steps:
        spec = step.specialist
        # WasteAnalysis cannot depend on anything downstream
        if spec == SpecialistType.WasteAnalysis:
            for dep in step.depends_on:
                dep_spec = step_map[dep].specialist
                if dep_spec in (
                    SpecialistType.CollectionPlanning,
                    SpecialistType.FleetRoute,
                    SpecialistType.ValidationOperations,
                ):
                    raise PlannerValidationError(
                        f"WasteAnalysis step '{step.step_id}' cannot depend on downstream specialist '{dep_spec}'."
                    )

        # CollectionPlanning cannot depend on FleetRoute or ValidationOperations
        if spec == SpecialistType.CollectionPlanning:
            for dep in step.depends_on:
                dep_spec = step_map[dep].specialist
                if dep_spec in (SpecialistType.FleetRoute, SpecialistType.ValidationOperations):
                    raise PlannerValidationError(
                        f"CollectionPlanning step '{step.step_id}' cannot depend on downstream specialist '{dep_spec}'."
                    )

        # FleetRoute cannot depend on ValidationOperations
        if spec == SpecialistType.FleetRoute:
            for dep in step.depends_on:
                dep_spec = step_map[dep].specialist
                if dep_spec == SpecialistType.ValidationOperations:
                    raise PlannerValidationError(
                        f"FleetRoute step '{step.step_id}' cannot depend on downstream specialist 'ValidationOperations'."
                    )

    # Domain sequence checks across all present specialists
    if SpecialistType.FleetRoute in topological_specialists and SpecialistType.CollectionPlanning in topological_specialists:
        fr_idx = topological_specialists.index(SpecialistType.FleetRoute)
        cp_idx = topological_specialists.index(SpecialistType.CollectionPlanning)
        if fr_idx < cp_idx:
            raise PlannerValidationError(
                "Domain ordering violation: FleetRoute cannot execute before CollectionPlanning."
            )

    if SpecialistType.ValidationOperations in topological_specialists and SpecialistType.FleetRoute in topological_specialists:
        vo_idx = topological_specialists.index(SpecialistType.ValidationOperations)
        fr_idx = topological_specialists.index(SpecialistType.FleetRoute)
        if vo_idx < fr_idx:
            raise PlannerValidationError(
                "Domain ordering violation: ValidationOperations cannot execute before FleetRoute."
            )


class SharedPlannerResult(BaseModel):
    """Advisory specialist execution plan produced by the Shared Planner.

    Contains at most one step for each specialist type, matching the
    one-result-slot-per-specialist orchestration state.
    """

    model_config = ConfigDict(populate_by_name=True, extra="forbid", str_strip_whitespace=True)

    objective: str = Field(
        ...,
        min_length=5,
        max_length=1000,
        description="The domain objective being planned.",
    )
    steps: List[SharedPlannerStep] = Field(
        ...,
        min_length=1,
        max_length=10,
        description="Structured list of planned specialist delegation steps.",
    )
    summary: str = Field(
        default="",
        max_length=1500,
        description="High-level narrative summary of the execution plan.",
    )
    warnings: List[str] = Field(
        default_factory=list,
        max_length=50,
        description="Advisory warnings identified during planning.",
    )
    agent_name: str = Field(
        default="shared_planner_agent",
        alias="agentName",
        description="Name of the planning agent.",
    )
    model_name: Optional[str] = Field(
        default=None,
        alias="modelName",
        description="Model or provider identifier used for planning.",
    )
    advisory_only: Literal[True] = Field(
        default=True,
        alias="advisoryOnly",
        description="Confirms this plan is purely advisory and performs no mutations.",
    )
    status: Literal["completed"] = "completed"

    @model_validator(mode="after")
    def validate_invariants(self) -> "SharedPlannerResult":
        validate_planner_result(self)
        return self


def create_flagship_planner_result(
    objective: str,
    summary: str = "Canonical end-to-end collection planning and fleet dispatch workflow.",
) -> SharedPlannerResult:
    """Helper to create a canonical 4-step flagship SharedPlannerResult fixture."""
    return SharedPlannerResult(
        objective=objective,
        steps=[
            SharedPlannerStep(
                step_id="step-1",
                specialist=SpecialistType.WasteAnalysis,
                objective="Analyse verified citizen reports and calculate advisory priorities.",
                depends_on=[],
                sequence=1,
            ),
            SharedPlannerStep(
                step_id="step-2",
                specialist=SpecialistType.CollectionPlanning,
                objective="Synthesize verified needs and propose candidate collection schedule.",
                depends_on=["step-1"],
                sequence=2,
            ),
            SharedPlannerStep(
                step_id="step-3",
                specialist=SpecialistType.FleetRoute,
                objective="Allocate available vehicles and drivers to approved collection tasks.",
                depends_on=["step-2"],
                sequence=3,
            ),
            SharedPlannerStep(
                step_id="step-4",
                specialist=SpecialistType.ValidationOperations,
                objective="Validate dispatch proposal against capacity and compatibility constraints.",
                depends_on=["step-3"],
                sequence=4,
            ),
        ],
        summary=summary,
        warnings=[],
    )
