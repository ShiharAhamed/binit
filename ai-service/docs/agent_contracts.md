# SmartWaste Agent Contracts

## Contract Stability Rule

These contracts are the stable integration boundary for the future Shared Planner and ASP.NET Agentic workflow.
Future schema-breaking changes must be intentional and update:
- contract documentation
- contract tests
- downstream workflow mappings

No runtime `contractVersion` fields are introduced; documentation and automated contract tests serve as the authoritative integration contract.

---

## Cross-Agent Contract Matrix

| Agent | Input Source | Main Output | Mutation Allowed | Human Boundary |
| :--- | :--- | :--- | :--- | :--- |
| **C1 — Waste Analysis** | Authoritative Verified Waste Reports | Advisory operational waste assessments | **No** (read-only advisory) | Officer reviews analysis context |
| **C2 — Collection Planning** | Authoritative Collection Needs Snapshot | Candidate collection groups, separate handling, and deferred needs with proposed schedules | **No** (read-only advisory; creates no tasks) | Municipal Manager approves scheduling proposal before task creation |
| **C3 — Fleet & Route** | Authoritative Scheduled Collection Tasks + Fleet Resources | Multi-plan dispatch recommendations (driver + vehicle + stop sequence) and unplanned tasks | **No** (read-only advisory; creates no assignments) | Feeds C4 validation; later Municipal Manager approves dispatch |
| **C4 — Validation & Operations** | C3 Dispatch Proposal (`dispatchPlans` + `unplannedTasks`) + Fresh Authoritative State | Plan validation reviews, diagnostic findings (`ReadyForHumanReview` vs `NeedsRevision`), and acknowledgement flags | **No** (read-only advisory; cannot approve or reject) | Municipal Manager conducts final operational review and authorization |

---

## C1 — Waste Analysis Agent

### Responsibility
The Waste Analysis Agent analyzes authoritative verified waste reports to provide advisory categorization, recommended operational priority, handling guidance, and operational concern flags for downstream planning.

### Request Schema
```json
{
  "objective": "Analyse verified waste reports for downstream collection planning.",
  "page": 1,
  "pageSize": 20
}
```

- **Pydantic Model**: `WasteAnalysisRequest` (`app.models.analysis`)
- **Fields**:
  - `objective` (`str`, required, 5–500 chars): Domain objective for analysis.
  - `page` (`int`, optional, default `1`, ge `1`): Source page of verified reports.
  - `pageSize` (`int`, optional, default `20`, 1–50, alias `pageSize`): Reports per page.

### Result Schema
```json
{
  "objective": "Analyse verified waste reports for downstream collection planning.",
  "analyses": [
    {
      "reportId": "b1a2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d",
      "categoryAssessment": "Bulky roadside waste accumulation",
      "recommendedPriority": "High",
      "operationalConcerns": [
        "Pedestrian sidewalk obstruction",
        "Potential hazardous runoff"
      ],
      "recommendedHandling": "Bulky waste compactor truck with two-person crew",
      "confidence": "High",
      "rationale": "High-volume waste reported in dense commercial zone requiring rapid clearing."
    }
  ],
  "sourcePage": 1,
  "sourcePageSize": 20,
  "sourceTotalCount": 1,
  "agentName": "waste_analysis_agent",
  "modelName": "gemini-2.5-flash",
  "status": "completed"
}
```

- **Pydantic Model**: `WasteAnalysisResult` (`app.models.analysis`)
- **Key Enums**:
  - `recommendedPriority`: `"Low" | "Medium" | "High" | "Urgent"` (PascalCase)
  - `confidence`: `"Low" | "Medium" | "High"` (PascalCase)

### Status Semantics
- `"completed"`: 1 or more verified reports retrieved and successfully analysed.
- `"empty"`: 0 verified reports found in backend for the requested page.

### Deterministic Guarantees
- Exact 1-to-1 coverage of all report IDs returned by the authoritative backend tool.
- Rejection of fabricated or hallucinated report IDs.
- Advisory operational priority is purely informational; it does **not** alter authoritative report priority or status.

### Tool / Authoritative Data Dependencies
- `GET /api/v1/internal/ai-tools/verified-waste-reports` via `fetch_verified_waste_reports`.

### Advisory Boundary
Advisory only. Produces no mutations in ASP.NET Core or PostgreSQL. Does not define an `advisoryOnly` property on the model, but enforces advisory-only behavior at the prompt, validation, and domain level.

### Explicit Non-Responsibilities
- Does not change `WasteReport.Status` or `WasteReport.Priority`.
- Does not create `CollectionNeed` or `CollectionTask` records.
- Does not make visual/photographic claims or inspect raw images directly.
- Does not query PostgreSQL directly.
- Advisory priority must **not** overwrite authoritative `CollectionNeed.Urgency`.

---

## C2 — Collection Planning Agent

### Responsibility
The Collection Planning Agent partitions authoritative outstanding `CollectionNeeds` into candidate collection groups, separate-handling recommendations, and deferred needs, equipping actionable needs with structured proposed schedules.

### Request Schema
```json
{
  "objective": "Plan urgent and high-priority collection needs for tomorrow morning.",
  "targetType": "Bin",
  "collectionReason": "FullOrBlockedBin",
  "targetDate": "2026-09-29",
  "page": 1,
  "pageSize": 20
}
```

- **Pydantic Model**: `CollectionPlanningRequest` (`app.models.collection_planning`)
- **Fields**:
  - `objective` (`str`, required, 5–500 chars): Planning objective.
  - `targetType` (`Optional[Literal["Report", "Bin"]]`, alias `targetType`): Optional target filter.
  - `collectionReason` (`Optional[Literal["VerifiedReport", "FullOrBlockedBin", "RoutineCollection"]]`, alias `collectionReason`): Optional reason filter.
  - `targetDate` (`Optional[date]`, alias `targetDate`): Advisory target collection date.
  - `page` (`int`, optional, default `1`, ge `1`): Starting page.
  - `pageSize` (`int`, optional, default `20`, 1–50, alias `pageSize`): Page size.

### Result Schema
```json
{
  "objective": "Plan urgent and high-priority collection needs for tomorrow morning.",
  "candidateGroups": [
    {
      "groupId": "group-1",
      "attentionOrder": 1,
      "needReferences": [
        {
          "needId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "targetType": "Bin",
          "collectionReason": "FullOrBlockedBin",
          "urgency": "Urgent"
        },
        {
          "needId": "3fa85f64-5717-4562-b3fc-2c963f66afa9",
          "targetType": "Bin",
          "collectionReason": "FullOrBlockedBin",
          "urgency": "High"
        }
      ],
      "proposedSchedule": {
        "scheduledAt": "2026-09-29T08:00:00Z",
        "schedulingReason": "Critical morning clearance for overflow bins in commercial district."
      },
      "rationale": "High urgency commercial bins grouped by proximity.",
      "wasteHandlingConsiderations": [
        "Standard compactor vehicle suitable"
      ],
      "warnings": []
    }
  ],
  "separateHandling": [
    {
      "needReference": {
        "needId": "4ba85f64-5717-4562-b3fc-2c963f66afa7",
        "targetType": "Report",
        "collectionReason": "VerifiedReport",
        "urgency": "High"
      },
      "attentionOrder": 1,
      "proposedSchedule": {
        "scheduledAt": "2026-09-29T09:30:00Z",
        "schedulingReason": "Hazardous industrial waste requires dedicated containment vehicle."
      },
      "rationale": "Hazardous materials require dedicated handling separate from standard compactors."
    }
  ],
  "deferredNeeds": [
    {
      "needReference": {
        "needId": "5ca85f64-5717-4562-b3fc-2c963f66afa8",
        "targetType": "Bin",
        "collectionReason": "RoutineCollection",
        "urgency": "Low"
      },
      "attentionOrder": null,
      "proposedSchedule": null,
      "rationale": "Low urgency bin deferred to regular weekly route cycle."
    }
  ],
  "warnings": [
    "Need 3fa85f64-5717-4562-b3fc-2c963f66afa6 lacks current bin telemetry (fill level unknown)."
  ],
  "sourcePage": 1,
  "sourcePageSize": 20,
  "sourceTotalCount": 3,
  "sourceTotalPages": 1,
  "retrievedPages": [1],
  "isCompleteSnapshot": true,
  "agentName": "collection_planning_agent",
  "modelName": "gemini-2.5-flash",
  "advisoryOnly": true,
  "status": "completed"
}
```

- **Pydantic Model**: `CollectionPlanningResult` (`app.models.collection_planning`)
- **Key Invariants**:
  - `advisoryOnly`: Strictly `True`.
  - `proposedSchedule` contract:
    - **Mandatory** on every item in `candidateGroups` and `separateHandling`.
    - **Strictly forbidden (`None`)** on every item in `deferredNeeds`.
  - Telemetry warnings are deterministically derived from `hasCurrentTelemetry == False` in backend data. Natural language prose inspection is not used for telemetry validation.

### Status Semantics
- `"completed"`: Full snapshot retrieved (`isCompleteSnapshot == true`) and all needs partitioned.
- `"partial"`: Incomplete snapshot (`isCompleteSnapshot == false`) due to pagination ceiling. **Must not proceed to human approval/execution.**
- `"empty"`: 0 collection needs returned from authoritative endpoint.

### Deterministic Guarantees
- Exact partition coverage: every need ID in the authoritative snapshot appears exactly once across `candidateGroups`, `separateHandling`, or `deferredNeeds`.
- `isCompleteSnapshot == true` required for approval readiness.

### Tool / Authoritative Data Dependencies
- `GET /api/v1/internal/ai-tools/collection-needs` via `fetch_collection_needs` (supports multi-page traversal).

### Advisory Boundary
Advisory only (`advisoryOnly: true`). Does not create `CollectionTask` records or mutate database state.

### Explicit Non-Responsibilities
- Does not create `CollectionTask` or `CollectionAssignment` records.
- Does not assign drivers or vehicles.
- Does not dispatch fleet.
- Does not mutate `CollectionNeed` status or priority.

---

## C3 — Fleet & Route Agent

### Responsibility
The Fleet & Route Agent evaluates authoritative `Scheduled` collection tasks alongside available drivers and vehicles, generating candidate multi-plan dispatch proposals with suggested stop sequences, vehicle-task compatibility validation, and explicit accounting of unplanned tasks.

### Request Schema
```json
{
  "objective": "Formulate morning dispatch plans for all scheduled collection tasks.",
  "page": 1,
  "pageSize": 20
}
```

- **Pydantic Model**: `FleetRouteRequest` (`app.models.fleet_route`)
- **Fields**:
  - `objective` (`str`, required, 5–500 chars): Planning objective.
  - `page` (`int`, optional, default `1`, ge `1`): Task pagination page.
  - `pageSize` (`int`, optional, default `20`, 1–50, alias `pageSize`): Task pagination page size.

### Result Schema
```json
{
  "objective": "Formulate morning dispatch plans for all scheduled collection tasks.",
  "dispatchPlans": [
    {
      "planId": "plan-1",
      "recommendedDriver": {
        "driverId": "d1a2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d",
        "displayName": "John Doe",
        "reason": "Experienced driver with full shift availability."
      },
      "recommendedVehicle": {
        "vehicleId": "v1a2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d",
        "registrationNumber": "WP-CAB-1234",
        "vehicleType": "Compactor",
        "reason": "Operational compactor compatible with scheduled organic waste."
      },
      "recommendedTasks": [
        {
          "taskId": "t1a2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d",
          "taskCode": "TSK-00101",
          "sequence": 1,
          "addressText": "123 Galle Road, Colombo 03",
          "reason": "First stop based on geographic starting point."
        }
      ],
      "compatibility": {
        "status": "Compatible",
        "requiresAcknowledgement": false,
        "issues": []
      },
      "rationale": "High-efficiency route covering Colombo 03 tasks.",
      "warnings": []
    }
  ],
  "unplannedTasks": [
    {
      "taskId": "t2a2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d",
      "taskCode": "TSK-00102",
      "reason": "Deferred: no additional compactor vehicles available in morning shift."
    }
  ],
  "warnings": [],
  "rationale": "Formulated 1 plan covering 1 task; 1 task deferred due to fleet constraints.",
  "sourceTaskPage": 1,
  "sourceTaskPageSize": 20,
  "sourceTaskTotalCount": 2,
  "sourceTaskTotalPages": 1,
  "agentName": "fleet_route_agent",
  "modelName": "gemini-2.5-flash",
  "advisoryOnly": true,
  "status": "completed"
}
```

- **Pydantic Model**: `FleetRouteResult` (`app.models.fleet_route`)
- **Key Serialization Contract**:
  - `model_dump(by_alias=True)` strictly emits canonical aliases: `recommendedDriver` and `recommendedVehicle`.
  - Input parsing accepts both `recommendedDriver` / `driver` and `recommendedVehicle` / `vehicle` via `AliasChoices`.
  - `advisoryOnly`: Strictly `True`.

### Status Semantics
- `"completed"`: Scheduled collection tasks exist and were evaluated. All tasks accounted for across `dispatchPlans` and `unplannedTasks`.
- `"empty"`: 0 `Scheduled` collection tasks exist in the authoritative backend.

### Deterministic Guarantees
- **Exact Task Coverage**: Every authoritative `Scheduled` task must appear exactly once: either in one plan's `recommendedTasks` OR in `unplannedTasks`.
- **Resource Uniqueness**:
  - Each driver is assigned to at most one dispatch plan.
  - Each vehicle is assigned to at most one dispatch plan.
  - Each task is assigned to at most one dispatch plan.
- **Stop Sequence**: Contiguous 1..N sequence per dispatch plan.
- **Plan ID Format**: Strictly matches regex `^plan-[1-9][0-9]*$`.
- **Authoritative Compatibility**: Re-evaluated deterministically against backend rules:
  - `"Compatible"`: Allowed.
  - `"Unknown"`: Allowed, with `requiresAcknowledgement: true`.
  - `"Incompatible"`: Invalid candidate plan, triggers validation failure or exclusion.
- **Contextual Capacity**: `capacityLiters` is purely contextual metadata; C3 does **not** perform volumetric packing calculations.

### Tool / Authoritative Data Dependencies
- `GET /api/v1/internal/ai-tools/fleet-planning-context` via `fetch_fleet_planning_context`.
- `POST /api/v1/internal/ai-tools/fleet-compatibility` via `fetch_fleet_compatibility`.

### Advisory Boundary
Advisory only (`advisoryOnly: true`). Does not create `CollectionAssignment` records, does not update task status, and does not dispatch drivers.

### Explicit Non-Responsibilities
- Does not create `CollectionAssignment` records in PostgreSQL.
- Does not mutate `CollectionTask.Status` (tasks remain `Scheduled`).
- Does not dispatch drivers or send push notifications.
- Does not calculate GPS turn-by-turn road routes or optimize traffic navigation.
- Does not enforce volumetric bin-packing math.

---

## C4 — Validation & Operations Agent

### Responsibility
The Validation & Operations Agent performs pre-execution operational validation on C3 dispatch proposals (`dispatchPlans` and `unplannedTasks`), cross-referencing fresh authoritative state (task status, driver availability, vehicle operational status, and fleet compatibility) to classify proposals as `ReadyForHumanReview` or `NeedsRevision`.

### Request Schema
```json
{
  "objective": "Validate morning fleet dispatch proposal against current operational state.",
  "dispatchPlans": [
    {
      "planId": "plan-1",
      "recommendedDriver": {
        "driverId": "d1a2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d",
        "displayName": "John Doe",
        "reason": "Selected for route"
      },
      "recommendedVehicle": {
        "vehicleId": "v1a2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d",
        "registrationNumber": "WP-CAB-1234",
        "vehicleType": "Compactor",
        "reason": "Assigned vehicle"
      },
      "recommendedTasks": [
        {
          "taskId": "t1a2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d",
          "taskCode": "TSK-00101",
          "sequence": 1,
          "addressText": "123 Galle Road",
          "reason": "Stop 1"
        }
      ],
      "compatibility": {
        "status": "Compatible",
        "requiresAcknowledgement": false,
        "issues": []
      },
      "rationale": "Advisory route proposal",
      "warnings": []
    }
  ],
  "unplannedTasks": [
    {
      "taskId": "t2a2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d",
      "taskCode": "TSK-00102",
      "reason": "Deferred"
    }
  ]
}
```

- **Pydantic Model**: `ValidationOperationsRequest` (`app.models.validation_operations`)
- **Direct Re-use**: Direct reuse of `DispatchPlanRecommendation` and `UnplannedTask` from C3.

### Result Schema
```json
{
  "objective": "Validate morning fleet dispatch proposal against current operational state.",
  "validationOutcome": "ReadyForHumanReview",
  "planReviews": [
    {
      "planId": "plan-1",
      "outcome": "ReadyForHumanReview",
      "requiresAcknowledgement": false,
      "findings": [],
      "summary": "Plan plan-1 verified: driver available, vehicle operational, task in Scheduled state, waste types compatible."
    }
  ],
  "unplannedTaskFindings": [
    {
      "code": "TASK_UNPLANNED_DEFERRED",
      "severity": "Info",
      "message": "Task TSK-00102 remains unscheduled: Deferred.",
      "relatedTaskIds": [
        "t2a2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d"
      ],
      "relatedDriverId": null,
      "relatedVehicleId": null
    }
  ],
  "requiresAcknowledgement": false,
  "warnings": [],
  "summary": "All 1 candidate dispatch plans are operationally sound and ready for human review.",
  "agentName": "validation_operations_agent",
  "modelName": "gemini-2.5-flash",
  "advisoryOnly": true,
  "status": "completed"
}
```

- **Pydantic Model**: `ValidationOperationsResult` (`app.models.validation_operations`)
- **Outcome Literals**: Strictly `"ReadyForHumanReview"` or `"NeedsRevision"` (or `None` when status is `"empty"`).
- **Severity Literals**: Strictly `"Info"`, `"Warning"`, `"Error"`.

### Status Semantics
- `"completed"`: 1 or more plans or unplanned tasks were evaluated.
- `"empty"`: Both `dispatchPlans` and `unplannedTasks` are empty.

### Deterministic Guarantees
- Re-verifies fresh authoritative state via `operational-validation-context`:
  - Detects if tasks are no longer `Scheduled` (e.g., cancelled or already assigned).
  - Detects if driver is occupied, inactive, or unavailable.
  - Detects if vehicle is occupied, inactive, or in maintenance.
- Re-verifies compatibility via `fleet-compatibility`:
  - `"Incompatible"` strictly forces `outcome = "NeedsRevision"`.
  - `"Unknown"` allows `outcome = "ReadyForHumanReview"` with `requiresAcknowledgement = true`.
- **Unplanned Task Invariant**: Unplanned tasks produce diagnostic findings (`severity = "Info"`) but do **not** invalidate valid dispatch plans.

### Tool / Authoritative Data Dependencies
- `POST /api/v1/internal/ai-tools/operational-validation-context` via `fetch_operational_validation_context`.
- `POST /api/v1/internal/ai-tools/fleet-compatibility` via `fetch_fleet_compatibility`.

### Advisory Boundary
Advisory only (`advisoryOnly: true`). C4 cannot commit, approve, or reject operational plans. It prepares structured recommendations for human manager authorization.

### Explicit Non-Responsibilities
- Does **not** approve or reject plans authoritatively (outcomes are strictly `ReadyForHumanReview` or `NeedsRevision`, never `Approved` or `Rejected`).
- Does not create `CollectionAssignment` records in PostgreSQL.
- Does not update database records or dispatch drivers.
- Does not modify Project Component 4 (Complaints, Operations & Analytics).

---

## Cross-Agent Handoff Contracts

### 1. C1 → C2 Advisory Handoff
- **Nature**: Asynchronous, advisory context only.
- **Rule**: There is currently no direct runtime dependency. In future shared workflows, C1 analysis may be supplied as advisory context to C2.
- **Contract Constraint**: C1 advisory recommended priority (`Low`, `Medium`, `High`, `Urgent`) must **never** overwrite or mutate authoritative `CollectionNeed.Urgency`.

### 2. C2 → ASP.NET Execution Boundary
- **Nature**: Authoritative execution and human approval barrier.
- **Rule**: C2 output does **not** feed C3 directly.
- **Workflow Sequence**:
  1. C2 produces `CollectionPlanningResult` (`candidateGroups`, `separateHandling`, `deferredNeeds`, `proposedSchedule`).
  2. Municipal Manager reviews and approves the scheduling proposal.
  3. ASP.NET Core validates and creates real `CollectionTask` records in state `Scheduled`.
  4. C3 subsequently retrieves those authoritative `Scheduled` tasks.

### 3. C3 → C4 Direct Specialist Handoff
- **Nature**: Direct, lossless structured handoff.
- **Rule**: C3 output directly matches C4 input:
  - `C3 FleetRouteResult.dispatch_plans` $\rightarrow$ `C4 ValidationOperationsRequest.dispatch_plans`
  - `C3 FleetRouteResult.unplanned_tasks` $\rightarrow$ `C4 ValidationOperationsRequest.unplanned_tasks`
- **Verification**: Reuses shared Pydantic models `DispatchPlanRecommendation` and `UnplannedTask`. No manual field translation, renaming, or object recreation is required.

### 4. C4 → Municipal Manager Review Boundary
- **Nature**: Human-in-the-Loop (HITL) operational authorization.
- **Rule**: C4 outputs `ReadyForHumanReview` or `NeedsRevision`.
- **Contract Constraint**: C4 never approves, rejects, or executes plans. The Municipal Manager conducts the authoritative review and triggers backend assignment creation via `CollectionAssignmentService`.

---

## Enum and Literal Value Reference

All enums and string literals must preserve their exact casing:

| Enum / Literal Concept | Allowed Values | Target Agent(s) |
| :--- | :--- | :--- |
| **Result Status** | `"completed"`, `"empty"` | C1, C3, C4 |
| **C2 Result Status** | `"completed"`, `"partial"`, `"empty"` | C2 |
| **Validation Outcome** | `"ReadyForHumanReview"`, `"NeedsRevision"` | C4 |
| **Finding Severity** | `"Info"`, `"Warning"`, `"Error"` | C4 |
| **Fleet Compatibility** | `"Compatible"`, `"Unknown"`, `"Incompatible"` | C3, C4 |
| **Need Urgency** | `"Low"`, `"Medium"`, `"High"`, `"Urgent"` | C2 |
| **Target Type** | `"Report"`, `"Bin"` | C2 |
| **Collection Reason** | `"VerifiedReport"`, `"FullOrBlockedBin"`, `"RoutineCollection"` | C2 |
| **Recommended Priority** | `"Low"`, `"Medium"`, `"High"`, `"Urgent"` | C1 |
| **Analysis Confidence** | `"Low"`, `"Medium"`, `"High"` | C1 |

---

## Error Contract Semantics

1. **Tool HTTP Failures**: When an authoritative backend endpoint returns `4xx` or `5xx`, tool client functions raise `httpx.HTTPStatusError` or `RuntimeError`. These are captured by agent orchestration or surfaced as unhandled exceptions.
2. **Deterministic Validation Failures**: When LLM generation violates schema or deterministic domain invariants (e.g. incomplete task coverage, invalid stop sequence, duplicate driver assignment, missing proposed schedule), agents perform bounded retries (up to 2 retry attempts). If validation still fails, a `RuntimeError` or `ValueError` is raised.
3. **Empty Data States**: When 0 records exist for the requested query, agents do not raise errors; they return a valid result with `status = "empty"` and empty list payloads.
4. **Outcome vs Exception**:
   - In C4, a plan failing operational checks is **not** an exception: it returns `status = "completed"` with `validationOutcome = "NeedsRevision"` and structured `ValidationFinding` records.
