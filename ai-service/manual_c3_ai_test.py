from app.agents.fleet_route_agent import run_fleet_route
from app.models.fleet_route import FleetRouteRequest


request = FleetRouteRequest(
    objective=(
        "Suggest a practical fleet dispatch plan for the currently "
        "available scheduled collection tasks."
    ),
    page=1,
    page_size=20,
)

try:
    result = run_fleet_route(request)

    print("\n=== C3 FLEET & ROUTE AGENT RESULT ===\n")
    print(result.model_dump_json(indent=2))

except Exception as exc:
    print("\n=== C3 AGENT FAILED SAFELY ===")
    print(type(exc).__name__)
    print(str(exc))