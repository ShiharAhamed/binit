# SmartWaste AI Service (Foundation)

Internal AI microservice for the Smart Waste Management System.

## Architecture Boundaries & Rules

1. **Authoritative Backend**: The ASP.NET Core 8 Web API (`/backend`) remains the single authoritative application backend and source of truth.
2. **No Direct Client Access**: The Python AI service is strictly an internal service. It is **never** called directly by the React web client or Flutter mobile client.
3. **No Direct Database Access**: The AI service does **not** connect directly to the PostgreSQL database. All state persistence and authoritative writes are mediated through the ASP.NET Core Web API.
4. **Stateless Agent Processing**: The service will host LangGraph agent workflows for waste analysis, collection planning, and route optimization.
5. **No Auth Logic**: User management, registration, and role authentication reside solely in the ASP.NET Core backend.

## Requirements

- Python 3.12+
- Virtual environment (`.venv`)

## Getting Started

### 1. Create Virtual Environment
```powershell
python -m venv .venv
.\.venv\Scripts\Activate.ps1
```

### 2. Install Dependencies
```powershell
pip install -r requirements.txt
```

### 3. Environment Variables
Copy `.env.example` to `.env` if local overrides are needed:
```powershell
Copy-Item .env.example .env
```

C2 uses `MUNICIPAL_TIMEZONE=Asia/Colombo` and permits collection start times
from `COLLECTION_WINDOW_START=08:00` inclusive to `COLLECTION_WINDOW_END=16:00`
exclusive in municipality local time. At or after closing, the earliest start
is 08:00 the next local calendar day. The advisory `scheduledAt` field stays
an offset-aware UTC timestamp. Invalid timezone or window settings fail fast.

### 4. Run Development Server
```powershell
.\.venv\Scripts\python.exe -m uvicorn app.main:app --host 127.0.0.1 --port 8000 --reload
```

- Health check: `http://127.0.0.1:8000/health`
- Interactive API Docs: `http://127.0.0.1:8000/docs`

### 5. Run Tests
```powershell
.\.venv\Scripts\python.exe -m pytest -v
```
