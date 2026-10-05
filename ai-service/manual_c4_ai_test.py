import json

from app.agents.fleet_route_agent import run_fleet_route
from app.agents.validation_operations_agent import run_validation_operations
from app.models.fleet_route import FleetRouteRequest
from app.models.validation_operations import ValidationOperationsRequest

print("\n--- Step 1: Running C3 Fleet & Route Planning ---")
c3_req = FleetRouteRequest(
    objective="Plan collection dispatch for currently available Scheduled tasks.",
    page=1,
    page_size=20,
)

try:
    c3_res = run_fleet_route(c3_req)
    print(f"C3 Status: {c3_res.status} | Dispatch Plans: {len(c3_res.dispatch_plans)} | Unplanned Tasks: {len(c3_res.unplanned_tasks)}")

    print("\n--- Step 2: Running C4 Validation & Operations Review ---")
    c4_req = ValidationOperationsRequest(
        objective="Validate C3 fleet dispatch proposal against fresh authoritative system state.",
        dispatchPlans=c3_res.dispatch_plans,
        unplannedTasks=c3_res.unplanned_tasks,
    )

    c4_res = run_validation_operations(c4_req)

    print("\n=== C4 VALIDATION & OPERATIONS AGENT RESULT ===")
    print(f"Status:                   {c4_res.status}")
    print(f"Validation Outcome:       {c4_res.validation_outcome}")
    print(f"Requires Acknowledgement: {c4_res.requires_acknowledgement}")
    print(f"Advisory Only:            {c4_res.advisory_only}")
    print(f"Agent Name:               {c4_res.agent_name}")
    print(f"Model Name:               {c4_res.model_name}")

    print("\n--- Plan Reviews ---")
    for pr in c4_res.plan_reviews:
        print(f"  [{pr.plan_id}] Outcome: {pr.outcome} | RequiresAck: {pr.requires_acknowledgement}")
        print(f"  Summary: {pr.summary}")
        for f in pr.findings:
            print(f"    - [{f.severity}] {f.code}: {f.message}")

    if c4_res.unplanned_task_findings:
        print("\n--- Unplanned Task Findings ---")
        for uf in c4_res.unplanned_task_findings:
            print(f"  [{uf.severity}] {uf.code}: {uf.message}")

    if c4_res.warnings:
        print("\n--- Warnings ---")
        for w in c4_res.warnings:
            print(f"  - {w}")

    print("\n--- Overall Summary ---")
    print(c4_res.summary)

except Exception as exc:
    print(f"\n=== EXECUTION FAILED SAFELY ===")
    print(f"{type(exc).__name__}: {str(exc)}")
