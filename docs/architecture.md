# SmartWaste — System Architecture Specification

> **Status:** FROZEN SOURCE OF TRUTH  
> **System Name:** SmartWaste — AI-Assisted Waste Collection & Management System  
> **Architecture Pattern:** ASP.NET Core Modular Monolith with an Internal Python AI Service  
> **Authoritative Backend:** ASP.NET Core 8 Web API  
> **Primary Database:** PostgreSQL 16+ (Accessed exclusively via EF Core in ASP.NET Core)  
> **AI Orchestration:** Python 3.12, FastAPI, LangGraph

---

## 1. Executive Summary & Architectural Pattern

SmartWaste is an enterprise-grade university software engineering project designed to modernize municipal solid waste collection, citizen reporting, route planning, and fleet logistics through collaborative multi-agent artificial intelligence.

The architectural pattern is strictly an **ASP.NET Core Modular Monolith paired with an Internal Python AI Service**. It is explicitly **NOT** a distributed microservices architecture:
- All core business logic, domain boundaries, data persistence, and role-based authorizations reside centrally in the **ASP.NET Core 8 Web API**.
- The **Python FastAPI service** operates purely as an internal, stateless computational and agentic reasoning coprocessor invoked over private HTTP by ASP.NET Core.
- The **PostgreSQL database** is private to ASP.NET Core. The Python AI service has **no direct database connection** and cannot perform unauthorized database writes.
- Web (React) and Mobile (Flutter) client applications communicate **exclusively** with ASP.NET Core. Under no circumstances do client applications communicate directly with the Python AI service.

---

## 2. End-to-End System Topography

```mermaid
flowchart TD
    subgraph Clients ["Client Layer"]
        ReactApp["React 18 + Vite (Web)\n[Waste Officers & Municipal Managers]"]
        FlutterApp["Flutter 3.x (Android)\n[Citizens & Collection Drivers]"]
    end

    subgraph Boundary ["Public API Boundary"]
        ReverseProxy["Reverse Proxy / TLS Gateway\n(CORS, Rate Limiting, SSL Termination)"]
    end

    subgraph Backend ["Authoritative Backend (ASP.NET Core 8)"]
        ApiLayer["SmartWaste.Api\n(Controllers, Middleware, JWT Auth)"]
        AppLayer["SmartWaste.Application\n(Use Cases, DTOs, Validation, Interfaces)"]
        DomainLayer["SmartWaste.Domain\n(Entities, Lifecycles, Domain Rules)"]
        InfraLayer["SmartWaste.Infrastructure\n(EF Core, Identity, Npgsql, AI HTTP Client)"]
    end

    subgraph Storage ["Authoritative Storage"]
        Postgres[(PostgreSQL 16+\nSingle AppDbContext)]
    end

    subgraph AIService ["Internal AI Microservice (Private Loopback)"]
        FastAPIApp["FastAPI 0.115+ App\n(Internal HTTP: 127.0.0.1:8000)"]
        LangGraph["LangGraph Multi-Agent Runtime\n(Planner + 4 Domain Agents)"]
    end

    ReactApp -->|HTTPS /api/v1| ReverseProxy
    FlutterApp -->|HTTPS /api/v1| ReverseProxy
    ReverseProxy --> ApiLayer

    ApiLayer --> AppLayer
    AppLayer --> DomainLayer
    AppLayer --> InfraLayer
    InfraLayer --> Postgres

    InfraLayer -->|Internal HTTP / Private JSON| FastAPIApp
    FastAPIApp --> LangGraph
```

---

## 3. Core Business Components & Primary Users

The application is decomposed into four cohesive business components, each matched to specific primary actors and client platforms:

| Component | Scope & Mission | Primary Actor | Primary Client |
| :--- | :--- | :--- | :--- |
| **1. Waste Reporting & Citizen Management** | Citizen waste issue reporting, photo uploads, officer field verification, and citizen engagement. | **Citizen** (Public)<br>**WasteOfficer** (Field) | **Flutter** (Citizen)<br>**React** (Officer) |
| **2. Waste Collection & Bin Management** | Public roadside bin registry, accepted waste streams, manual observation tracking (discrete 0–100% fill levels & physical condition), derived collection needs queue (Sources A, B, C), and atomic collection task scheduling. | **WasteOfficer** (Management)<br>**Citizen** (Discovery) | **React** (Officer)<br>**Flutter** (Citizen) |
| **3. Fleet, Driver & Route Management** | Manager-owned vehicle records, automatically maintained Driver availability profiles, WasteOfficer manual multi-task assignments, task-linked route stops, and Driver execution. Manual ordering is the operational baseline; road-aware routing is a separately approved future integration. | **MunicipalManager**<br>**WasteOfficer**<br>**Driver** | **React** (Manager/Officer)<br>**Flutter** (Driver) |
| **4. Operations, Complaints & Analytics** | Citizen grievances, operational field incidents, notifications, multi-agent AI approval workflows, and executive analytics. | **MunicipalManager**<br>**Citizen** | **React** (Manager)<br>**Flutter** (Citizen) |

---

## 4. User Roles & Access Control

Access control is enforced through ASP.NET Core Identity with role-based authorization attributes (`[Authorize(Roles = AppRoles.X)]`):

```csharp
public static class AppRoles
{
    public const string Citizen = "Citizen";
    public const string WasteOfficer = "WasteOfficer";
    public const string Driver = "Driver";
    public const string MunicipalManager = "MunicipalManager";
}
```

### Role Capabilities
1. **`Citizen`**:
   - Registers publicly via `/api/v1/auth/register` (strictly restricted to `Citizen` role).
   - Submits and tracks own `WasteReport` records.
   - Discovers nearby public roadside bins, inspects accepted waste streams and public availability, and requests external directions.
   - Files and monitors own `Complaint` records.
   - Cannot verify reports, register or modify bins, record bin observations, dispatch/reschedule tasks, view other citizens' records, or approve AI workflows.
2. **`WasteOfficer`**:
   - Verifies or rejects citizen waste reports.
   - Manages municipal roadside bin registry (registration, operational metadata updates, deactivation).
   - Records manual field bin observations (fill levels and physical condition).
   - Reviews the unified municipal collection needs queue (Sources A, B, and C with active task suppression).
   - Creates ordinary manual assignments from existing Scheduled C2 tasks, manually orders their task-linked route stops, cancels only unstarted assignments, and reviews/replaces failed collection work.
   - Initiates AI planning workflows.
3. **`Driver`**:
   - Manages personal duty availability (`Available`, `OffDuty`).
   - Sees only own assignments, starts own assigned run, and completes or fails individual task-linked stops.
   - Can record an authorised genuine post-collection bin observation for an own completed bin stop; cannot reassign, transfer, or cancel work.
4. **`MunicipalManager`**:
   - Senior administrative supervisor who manages vehicle records and can view Driver operational availability and derived occupancy.
   - Reviews and adjudicates escalated complaints and operational incidents.
   - Reviews AI workflow recommendations and holds sole authority to execute `AiApproval` (`Approved`, `Rejected`, `RevisionRequested`).
   - Accesses high-level aggregated operational analytics.

---

## 5. Backend Solution Architecture (`/backend`)

The backend follows the principles of Clean Architecture / Onion Architecture organized across four distinct projects:

```
backend/
├── SmartWaste.sln
├── src/
│   ├── SmartWaste.Api/               # Presentation & Boundary Layer
│   ├── SmartWaste.Application/       # Orchestration & Use Case Layer
│   ├── SmartWaste.Domain/            # Core Domain & Entity Layer
│   └── SmartWaste.Infrastructure/    # Persistence & External Service Layer
└── tests/
    └── SmartWaste.Tests/             # Unit & Integration Test Suite
```

### 5.1 Project Responsibilities

#### `SmartWaste.Domain`
- **Dependencies:** None (Pure .NET standard/net8.0).
- **Contents:**
  - Entities (`AppUser`, `WasteReport`, `WasteBin`, `CollectionTask`, `Vehicle`, `Complaint`, `AiWorkflow`, etc.).
  - Enums and static constants (`AppRoles`, status lifecycles).
  - Domain exceptions and core domain invariants.

#### `SmartWaste.Application`
- **Dependencies:** `SmartWaste.Domain`, `FluentValidation`.
- **Contents:**
  - Use-case services and business workflows (e.g., `ReportVerificationService`, `AssignmentService`).
  - DTOs (`RegisterRequest`, `AuthResponse`, `WasteReportDto`, `AiHealthDto`).
  - Service interfaces (`IAuthService`, `IAiServiceClient`, `ITokenService`).
  - Request validators (`RegisterRequestValidator`, `LoginRequestValidator`).
  - Configuration option models (`JwtOptions`, `AiServiceOptions`).
- **Rule:** Uses standard service interfaces and dependency injection; avoids unnecessary CQRS/MediatR complexity.

#### `SmartWaste.Infrastructure`
- **Dependencies:** `SmartWaste.Application`, `SmartWaste.Domain`, EF Core, Npgsql, ASP.NET Core Identity.
- **Contents:**
  - `AppDbContext`: Main EF Core DbContext extending `IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>`.
  - Fluent API configurations (`IEntityTypeConfiguration<T>`).
  - ASP.NET Core Identity store bindings.
  - Implementations of application interfaces (`TokenService`, `AuthService`).
  - Typed `HttpClient` implementations (`AiServiceClient`) with resilience, timeouts, and error mapping.

#### `SmartWaste.Api`
- **Dependencies:** `SmartWaste.Application`, `SmartWaste.Infrastructure`, ASP.NET Core Web API.
- **Contents:**
  - RESTful API Controllers (`AuthController`, `WasteReportsController`, `DevAiController`).
  - Pipeline Middleware: `ExceptionHandlingMiddleware` (maps exceptions to RFC 7807 `ProblemDetails`).
  - Authentication/Authorization configuration (JWT Bearer tokens).
  - Swagger/OpenAPI documentation setup.

### 5.2 Request Execution Flow
```
HTTP Request ──> Controller ──> Application Service ──> Infrastructure/EF Core ──> PostgreSQL
                      │
                      └──> Exception? ──> ExceptionHandlingMiddleware ──> ProblemDetails (503/400/404)
```

---

## 6. Client Architecture & Integration

Both client applications are built to serve specific user roles while sharing identical authentication, error conventions, and authoritative business rules.

### 6.1 React Web Application (`/web`)
- **Technology:** React 18, Vite, TypeScript, Tailwind CSS, Axios, React Router, Zustand / React Context.
- **Target Audience:** `WasteOfficer` and `MunicipalManager`.
- **Key Modules:**
  - Authentication & Role Guarding (`ProtectedRoute`, `RoleRoute`).
  - Verification Dashboard: Review citizen waste reports with photo attachments.
  - Bin & Collection Task Manager: C2 bin/task workflows and collection-needs review.
  - Fleet & Dispatch Console: MunicipalManager vehicle/driver administration; WasteOfficer assignment creation, manual route ordering, run monitoring, and failed-task replacement review.
  - AI Review Portal: Human-in-the-loop review interface displaying structured AI recommendations, route metrics, and approval/rejection actions.
- **Security:** JWT stored securely in memory / local session storage with automatic Bearer token injection via Axios interceptors.

### 6.2 Flutter Mobile Application (`/mobile`)
- **Technology:** Flutter 3.x, Dart, Dio HTTP client, Flutter Secure Storage, Riverpod.
- **Target Audience:** `Citizen` and `Driver`.
- **Key Modules:**
  - Citizen Mode:
    - GPS-tagged photo report submission.
    - Personal report status tracking with timeline history.
    - Service complaint filing.
  - Driver Mode:
    - Shift availability toggle (`Available` / `OffDuty`).
    - Own assignment list, ordered task-linked stops, and external navigation for one saved stop destination.
    - Start-run, complete-stop, fail-stop-with-reason, and assignment-finalization actions scoped from the authenticated Driver identity.
    - Optional append-only post-collection bin observation for an authorised own completed bin stop.
- **Security:** Access tokens stored strictly in hardware-backed `FlutterSecureStorage`. Base URL dynamically configurable (`10.0.2.2:5276` for Android Emulator, LAN IP for physical device via `--dart-define=API_BASE_URL=...`).

### 6.3 Component 3 client integration contract

- React and Flutter communicate only with ASP.NET Core; neither client calls a routing provider, FastAPI, or PostgreSQL directly.
- React exposes manager-only resource administration separately from WasteOfficer assignment operations. Driver availability, vehicle operational state, task eligibility, claim occupancy, waste compatibility, and every status transition are revalidated by ASP.NET Core.
- Flutter Driver screens receive only the authenticated Driver’s assignment projection. Execution commands derive the Driver identity from the JWT and never trust a client-supplied driver ID.
- The route view always supports the stored manual order. It may display provider geometry, duration, distance, or a suggested order only after an approved server-side provider has actually returned verified data; provider failure preserves manual ordering.

---

## 7. Python AI Service Architecture (`/ai-service`)

The Python AI service is an internal, stateless microservice dedicated to Agentic AI workflow orchestration.

### 7.1 Directory Layout
```
ai-service/
├── app/
│   ├── agents/          # Autonomous agent implementations
│   ├── api/             # Internal routers (/health, internal workflow endpoints)
│   ├── core/            # Configuration (pydantic-settings), structured logging
│   ├── graphs/          # LangGraph state graph definitions and workflows
│   ├── models/          # Pydantic schemas for state, inputs, outputs
│   ├── services/        # Service-layer business helpers
│   ├── tools/           # Allow-listed tools callable by agents
│   └── main.py          # FastAPI entrypoint and lifespan manager
├── tests/               # Pytest suite
├── requirements.txt     # Locked production dependencies
└── requirements-dev.txt # Development and testing dependencies
```

### 7.2 Current AI Foundation Status
- **Python Version:** 3.12.
- **Framework:** FastAPI with Uvicorn server running on `127.0.0.1:8000`.
- **Graph Orchestration:** `langgraph` installed and importable.
- **Operational Endpoints:** `GET /health` responding with `{"status":"healthy","service":"SmartWaste AI Service"}`.
- **LLM Provider Decision (Step 9A.14a):**
  - **Authoritative Provider:** Google Gemini API via `langchain-google-genai` (`ChatGoogleGenerativeAI`).
  - **Configuration (Environment Variables):**
    - `LLM_PROVIDER`: `"mock"` (default for offline automated tests) or `"google"` (for live development/demo).
    - `LLM_MODEL`: Model identifier (e.g. `gemini-1.5-flash`).
    - `LLM_API_KEY`: Runtime secret key (never committed; loaded from gitignored `.env` or runtime environment).
    - `LLM_TEMPERATURE`: Default `0.0` for deterministic structured output.
    - `LLM_TIMEOUT`: Default `30.0` seconds.
  - **Offline Testing:** Automated pytest suites use `LLM_PROVIDER="mock"` with zero live network calls and no live API keys.
  - **ADR Notice:** This selection represents the project's authoritative model provider decision and will be formalized in the final Agentic AI ADR.

---

## 8. Multi-Agent Agentic AI Architecture

The agentic architecture divides responsibilities among five distinct agents using **LangGraph**:

```mermaid
flowchart TD
    Initiate["ASP.NET Core:\nInitiates AI Workflow (POST /api/v1/ai/workflows)"]
    Planner["1. Planner Agent (Shared Orchestrator)\nDeconstructs objective into sub-tasks"]
    
    WasteAgent["2. Waste Analysis Agent\n(Student 1: Component 1)\nEvaluates waste type & recommends priority"]
    CollectionAgent["3. Collection Planning Agent\n(Student 2: Component 2)\nEvaluates bin fullness & zones"]
    FleetAgent["4. Fleet & Route Agent\n(Student 3: Component 3)\nProposes advisory allocation & stop order"]
    ValidationAgent["5. Validation & Operations Agent\n(Student 4: Component 4)\nChecks capacity, driver status & SLA"]

    Deterministic["ASP.NET Core:\nDeterministic Business Rule Verification"]
    ManagerReview["React Portal:\nMunicipal Manager Review (Human-in-the-Loop)"]
    Commit["ASP.NET Core:\nAuthoritative Transaction Commit to PostgreSQL"]

    Initiate --> Planner
    Planner --> WasteAgent
    Planner --> CollectionAgent
    Planner --> FleetAgent
    Planner --> ValidationAgent

    WasteAgent --> Planner
    CollectionAgent --> Planner
    FleetAgent --> Planner
    ValidationAgent --> Planner

    Planner -->|Structured Proposal| Deterministic
    Deterministic -->|Passed Rules| ManagerReview
    Deterministic -->|Failed Rules| Initiate
    ManagerReview -->|Approved| Commit
    ManagerReview -->|Rejected| Initiate
```

### 8.1 Student Agent Ownership & Specialization
To ensure distinct individual academic contributions while maintaining architectural coherence, ownership is distributed as follows:

1. **Planner Agent (Shared Orchestration)**:
   - Maintains global workflow state in LangGraph.
   - Deconstructs municipal objectives into sequenced agent tasks.
   - Aggregates agent findings into a cohesive proposal.
2. **Waste Analysis Agent (Student 1 — Component 1)**:
   - **Responsibility:** Analyses already-verified WasteReports and produces a structured, non-authoritative operational assessment for downstream collection planning.
   - **Allowed Tools:** Strictly allow-listed `get_verified_waste_reports` tool (retrieving authoritative `Verified` reports via ASP.NET Core internal authenticated endpoint). Zero direct PostgreSQL or Supabase access.
   - An additive internal exact-report read endpoint is available for a future citizen-report advisory verification path. Current manual C1 execution continues to use verified reports; trigger-aware Python behavior is deferred.
   - **Input Contract (`WasteAnalysisRequest`):** `objective` (string, 5-500 chars), `page` (int >= 1), `page_size` (int 1-50).
   - **Structured Output Contract (`WasteAnalysisResult` / `WasteReportAnalysis`):** `reportId`, `categoryAssessment`, `recommendedPriority` (`Low`, `Medium`, `High`, `Urgent`), `operationalConcerns` (0-5 items), `recommendedHandling`, `confidence` (`Low`, `Medium`, `High`), `rationale`.
   - **Advisory Recommendation Semantics:** `recommendedPriority` is purely advisory for downstream planning; it NEVER mutates authoritative `WasteReport.Priority` or `WasteReport.Status` in the database.
   - **Safety Boundaries:**
     - *No Image Analysis:* `attachmentCount` indicates file presence only. Agent never inspects photos or claims visual evidence.
     - *Location Boundary:* GPS coordinates and address are raw reported data; agent never invents road names, traffic, distance, or route facts.
     - *Prompt-Injection Resistance:* Report fields are untrusted citizen data; instructions embedded in descriptions are strictly ignored.
     - *Exact Coverage Rule:* Produces exactly one analysis per verified report returned by the tool.
   - **Planner Delegation:** Shared Planner Agent will delegate objectives to this specialized agent in multi-agent workflows.
3. **Collection Planning Agent (Student 2 — Component 2)**:
   - **Responsibility:** Evaluates municipal collection needs (derived from Verified waste reports, full/overflowing roadside bins, and routine collection schedules) and proposes non-authoritative collection route/batch candidates.
   - **Allowed Tools:** Strictly allow-listed `get_collection_needs` tool (retrieving candidate needs via ASP.NET Core internal authenticated endpoint `GET /api/v1/internal/ai-tools/collection-needs`). Zero direct PostgreSQL access.
   - **Advisory Recommendation Semantics:** Proposes candidate task groups and collection priority sequences for downstream fleet allocation and routing; never authoritatively creates `CollectionTask` records or updates database statuses directly.
   - **Safety Boundaries:** Respects active task suppression, administrative bin availability, and deterministic weekday schedules.
   - **Planner Delegation:** Shared Planner Agent will delegate collection needs evaluation to this specialized agent in multi-agent workflows.
4. **Fleet & Route Agent (Student 3 — Component 3)**:
   - **Responsibility:** Evaluates already Scheduled tasks against current eligible Driver/Vehicle resources and proposes advisory allocation and stop-order candidates.
   - **Future read-only tool boundary:** Receives only sanitized task IDs/locations, target type, authoritative reason, scheduled time, known handling requirements, opaque eligible-driver references with occupancy, and vehicle status/capacity-in-litres/supported waste types. Verified routing metrics are included only after an approved routing service supplies them.
   - **Safety boundaries:** Has no database connection or write tools; cannot assign, dispatch, approve, change task state, override backend availability, claim vehicle capacity feasibility, or describe a manual/coordinate order as road-optimised. It exposes no citizen identity, Driver licence/contact data, credentials, or unrelated records.
   - **Advisory semantics:** Manual WasteOfficer assignments never require Gemini. Any future AI-driven operational change requires the shared workflow’s MunicipalManager approval and fresh ASP.NET Core validation before execution.
5. **Validation & Operations Agent (Student 4 — Component 4)**:
   - Pre-evaluates operational feasibility against historical SLA metrics.
   - Validates that proposal adheres to municipal collection guidelines.

---

## 9. AI Governance, Safety & Deterministic Rules

The integration of generative and agentic AI is bounded by strict software engineering controls:

### 9.1 Core AI Directives
1. **AI Proposes; ASP.NET Validates and Commits**:
   - The AI service has zero write access to the PostgreSQL database.
   - All AI outputs are structured recommendations (`jsonb`) returned to ASP.NET Core.
2. **Deterministic Rules Override AI Suggestions**:
   - Every proposal undergoes strict programmatic validation in ASP.NET Core before reaching a human manager.
   - If any deterministic rule fails, the proposal is rejected or returned for revision:
     - `WasteReport.Status` must be `Verified` before scheduling (`WasteReport` transitions atomically from `Verified` to `Scheduled`; C1 audit trail appended using existing `ChangedByUserId` and `Notes` properties).
     - `WasteBin.AdministrativeStatus` must be `Active`.
     - Single-target XOR constraint: each `CollectionTask` targets strictly EITHER a `WasteReport` OR a `WasteBin`.
     - Reason consistency: Report tasks require `VerifiedReport` reason; `OfficerDiscretion` is permitted strictly for `WasteBin` targets and mandates a non-empty `SchedulingReason` (distinct from optional operational `HandlingNotes`).
     - No duplicate active `CollectionTask` for the same target (enforced via partial unique indexes on active statuses `Scheduled`, `Assigned`, `InProgress`).
     - Selected Driver must hold the `Driver` role, have an automatically maintained internal `DriverProfile`, be `Available`, and have no unfinished assignment; selected Vehicle must be operationally `Available` and unoccupied. Driver availability is Driver-controlled, and an already assigned Driver retains execution authority after changing to `OffDuty`.
     - One active assignment task claim per CollectionTask and one unfinished assignment per Driver/Vehicle are enforced through direct-row PostgreSQL partial unique indexes plus transaction-time revalidation.
     - Known waste incompatibility blocks assignment. Missing or ambiguous compatibility requires a recorded WasteOfficer acknowledgement and never asserts load, compartments, or vehicle-capacity feasibility.
     - Terminal task immutability: `Completed`, `Cancelled`, and `Failed` are terminal historical records. `Failed` tasks never revert or resolve linked reports and cannot be mutated to `Cancelled`.
     - Failed-task replacement creates a new task only through the dedicated C3 reviewed replacement command. It preserves Failed history; report replacement moves the still-in-progress report back to Scheduled rather than to Verified. Unstarted assignment cancellation explicitly requeues Assigned tasks to Scheduled and never cancels them.
3. **Human-in-the-Loop (HITL) for High-Impact Actions**:
   - High-impact AI recommendations (automated batch collection dispatch, vehicle route generation) require explicit `MunicipalManager` approval in the React web app (`POST /api/v1/ai/workflows/{id}/decision`) before database commit.
   - Deterministic manual operational scheduling: `WasteOfficer` can create single-target collection tasks directly (`POST /api/v1/collection-tasks/manual`) and reschedule unstarted tasks (`POST /api/v1/collection-tasks/{id}/reschedule`) without AI workflow involvement.
4. **Transparent, Auditable Workflow History**:
   - All workflow steps (`AiWorkflowStep`) and tool calls (`AiToolCall`) are persistently logged with inputs and outputs in PostgreSQL.
   - **No Hidden Chain-of-Thought Storage**: Internal reasoning traces or raw thoughts are never stored in the database. Only auditable, structured actions, parameters, summaries, and outcomes are persisted.
5. **Strict Allow-Listed Tools**:
   - Agents invoke only pre-registered, strongly typed tools with validated JSON schemas.

---

## 10. Agentic Workflow Dispatch Review

The shared React AI Approvals detail is available to both `WasteOfficer` and `MunicipalManager` roles. It renders the persisted C3 Fleet & Route proposal and C4 Operational Validation result, including unplanned tasks and the recorded validation outcome, without treating the client as an execution authority.

- `AwaitingDispatchApproval` exposes explicit approve, revision, and reject decisions only when persisted C4 reports `ReadyForHumanReview`. A required C4 warning acknowledgement must be deliberately checked and is sent as `acknowledgeWarnings`; it is never inferred from client state.
- Dispatch approval and execution are separate POST operations. Approval records the `FleetDispatch` decision. Only `DispatchApproved` exposes execution, which sends just the workflow ID and optimistic-concurrency version; React never posts a fleet plan, route, task list, driver, or vehicle selection.
- ASP.NET revalidates the approved C3/C4 snapshot and current authoritative task, driver, vehicle, and compatibility state before atomically creating `CollectionAssignment` records. `Completed` therefore means planning and approved assignment creation completed, not physical route completion.
- The UI refreshes workflow detail and list queries after every outcome. It reports `409 Conflict` as a stale workflow, treats unknown transport outcomes as unconfirmed, and never automatically retries a decision or execution request.

---

## 11. Future Integrations (Third-Party Services)

Future development phases will incorporate external services under strict encapsulation:
- **Routing & Maps**: Manual C3 stop ordering is fully usable without a provider. A future separately approved server-side routing provider may calculate road geometry, distance, duration, or a suggested multi-stop order according to its actual capabilities. Calculating a route for a supplied order is not optimisation. Any provider key remains private to the backend; React and Flutter never call it directly and retain manual ordering when it is unavailable.
- **Object Storage**: Photographic attachments (`ReportAttachment`) will be persisted using secure cloud storage (e.g., S3/Blob) or managed municipal storage with pre-signed upload URLs.
- **Push Notifications**: Firebase Cloud Messaging (FCM) or Apple APNs will be integrated into ASP.NET Core for mobile alert dispatch.

---

## 12. Security & Compliance Architecture

- **Authentication**: Stateless HMAC-SHA256 JWT access tokens issued by ASP.NET Core with strict expiration and cryptographic validation.
- **Client Platform Role Policy**:
  - **React Web Application**: Strictly restricted to municipal staff (`WasteOfficer` and `MunicipalManager`).
  - **Flutter Mobile Application**: Strictly restricted to public citizens and field collectors (`Citizen` and `Driver`).
  - **Platform Enforcement**: Validated on `POST /api/v1/auth/login` via `clientType`. Incompatible role attempts return `403 Forbidden` (`ProblemDetails`) without issuing a JWT. Both web and mobile frontends also perform defense-in-depth role checks.
- **Internal User Provisioning & Password Lifecycle**:
  - **Citizen Registration**: Only `Citizen` accounts can be created via public registration (`POST /api/v1/auth/register`).
  - **Staff User Provisioning**: Internal accounts (`Driver`, `WasteOfficer`, `MunicipalManager`) are created exclusively by authorized `MunicipalManager` users (`POST /api/v1/users`).
  - **One-Time Temporary Passwords**: Provisioned users receive a cryptographically secure 16-character temporary password returned strictly once upon creation (never stored in plaintext, never logged, never returned in list/detail queries).
  - **Mandatory First-Login Password Change**: Accounts created with temporary passwords have `MustChangePassword = true`. The issued JWT includes `must_change_password = "True"`. Backend middleware strictly restricts access to `POST /api/v1/auth/change-password` and `GET /api/v1/auth/me`; attempts to access business endpoints return `403 Forbidden` (`ProblemDetails`). Both client applications immediately force navigation to a dedicated Change Password screen.
  - **Account Deactivation Guard**: Municipal Managers can toggle staff active status (`isActive`), but cannot deactivate their own active account.
- **Authorization**: Role-based access control (`Citizen`, `WasteOfficer`, `Driver`, `MunicipalManager`) enforced on every endpoint.
- **Secrets Management**: Configuration via ASP.NET Core User Secrets (Development) and Environment Variables (Production). No plain-text credentials, database passwords, or JWT signing keys are committed to Git.
- **Cross-Origin Resource Sharing (CORS)**: Controlled strictly by ASP.NET Core in `Program.cs`, permitting only authorized client origins.
- **Audit Logging**: Immutable ledger (`AuditLog`) capturing user actions, administrative decisions, and AI approvals.

---

## 13. Deployment Architecture Concept

The entire SmartWaste platform is designed for containerized or modular deployment:

```mermaid
flowchart LR
    subgraph Host ["Municipal Server / Cloud Host"]
        Postgres[(PostgreSQL 16\nDatabase)]
        WebApi["ASP.NET Core 8 Web API\n(:5276 / :443)"]
        AiSvc["FastAPI AI Service\n(127.0.0.1:8000)"]
        StaticWeb["Nginx / SPA Static Host\n(React Web App)"]
    end

    subgraph MobileDevice ["Mobile Clients"]
        FlutterApp["Flutter APK\n(Android Client)"]
    end

    StaticWeb --> WebApi
    FlutterApp --> WebApi
    WebApi --> Postgres
    WebApi --> AiSvc
```

- **Database**: Single PostgreSQL container/instance.
- **Authoritative Backend**: ASP.NET Core 8 Linux/Windows container.
- **AI Service**: Python 3.12 container bound to internal private networking.
- **Web Client**: Production build of React SPA served via Nginx or ASP.NET static file middleware.
- **Mobile Client**: Standalone Android APK installed on citizen and driver devices, communicating securely over HTTPS.
