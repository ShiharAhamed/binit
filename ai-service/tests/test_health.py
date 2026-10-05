import importlib.metadata
from fastapi.testclient import TestClient

from app.core.config import get_settings
from app.main import app

client = TestClient(app)


def test_get_health():
    """Verify GET /health returns HTTP 200, status healthy, and matching service name."""
    response = client.get("/health")
    assert response.status_code == 200

    data = response.json()
    settings = get_settings()

    assert data["status"] == "healthy"
    assert data["service"] == settings.APP_NAME


def test_langgraph_importable():
    """Verify LangGraph package and core graph primitives are installed and importable."""
    import langgraph
    from langgraph.graph import END, START, StateGraph

    assert langgraph is not None
    assert StateGraph is not None
    assert START is not None
    assert END is not None

    version = importlib.metadata.version("langgraph")
    assert version.startswith("1.") or version.startswith("0.")
