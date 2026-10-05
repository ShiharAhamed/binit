from app.agents.shared_planner_agent import run_shared_planner
from app.models.shared_planner import (
    SharedPlannerRequest,
    validate_planner_result,
)


def main():
    objective = (
        "Prepare an end-to-end waste collection operation for the current "
        "verified waste needs, including collection planning, fleet dispatch "
        "planning, validation, and human-approved execution."
    )

    request = SharedPlannerRequest(
        objective=objective
    )

    print("\n=== RUNNING LIVE SHARED PLANNER GEMINI TEST ===\n")

    result = run_shared_planner(request)

    # Run deterministic validation again explicitly for the smoke test.
    validate_planner_result(result)

    print("\n=== SHARED PLANNER RESULT ===\n")
    print(result.model_dump_json(by_alias=True, indent=2))

    print("\n=== SPECIALIST PLAN ===\n")

    for step in result.steps:
        print(
            f"{step.sequence}. "
            f"{step.specialist.value} | "
            f"stepId={step.step_id} | "
            f"dependsOn={step.depends_on}"
        )

    print("\n=== VALIDATION ===")
    print("Contract validation: PASSED")
    print("Deterministic planner validation: PASSED")
    print(f"Status: {result.status}")
    print(f"Advisory only: {result.advisory_only}")
    print(f"Agent: {result.agent_name}")
    print(f"Model: {result.model_name}")


if __name__ == "__main__":
    main()