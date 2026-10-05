# AI Approvals React Foundation

MunicipalManager and WasteOfficer intentionally share the same AI Approvals
feature because ASP.NET grants both roles equal AgentWorkflow authority. Their
role-specific routes (`/manager/ai-approvals` and `/officer/ai-approvals`) both
render `AiApprovalsPage`.

The browser calls only the normal authenticated ASP.NET API client. Python
orchestration endpoints and internal service credentials are never browser
contracts.

## Audited AgentWorkflow API

- `POST /api/v1/agent-workflows`
- `GET /api/v1/agent-workflows`
- `GET /api/v1/agent-workflows/{id}`
- `GET /api/v1/agent-workflows/{id}/history`
- `POST /api/v1/agent-workflows/{id}/start`
- `POST /api/v1/agent-workflows/{id}/collection-approval/approve`
- `POST /api/v1/agent-workflows/{id}/collection-approval/request-revision`
- `POST /api/v1/agent-workflows/{id}/collection-approval/reject`
- `POST /api/v1/agent-workflows/{id}/execute-collection-plan`
- `POST /api/v1/agent-workflows/{id}/dispatch-approval/approve`
- `POST /api/v1/agent-workflows/{id}/dispatch-approval/request-revision`
- `POST /api/v1/agent-workflows/{id}/dispatch-approval/reject`
- `POST /api/v1/agent-workflows/{id}/execute-dispatch-plan`

Workflow detail step payloads are public JSON values. `parseJsonValue` accepts
those objects and safely handles legacy string payloads for future rendering.

## Step 11B dashboard and initiation

The shared dashboard lists the newest persisted workflows through the paginated
ASP.NET list endpoint. Total workflows is the authoritative server total;
attention, in-progress, and completed counts are explicitly scoped to the
currently retrieved page. Search and category filters are client-side on that
page because the backend list contract has no objective search or aggregate
category filters.

Both `/manager/ai-approvals/:workflowId` and
`/officer/ai-approvals/:workflowId` render the same detail shell. Starting a
new operation is deliberately two browser calls: create the persisted workflow,
then start that workflow through ASP.NET. The browser never calls Python. If
start fails after creation, the existing workflow remains available to open.

## Step 11C read-only workflow detail

The shared workflow detail page reads one persisted `AgentWorkflow` through the
authenticated ASP.NET detail endpoint and renders a structured operational
review. It shows the workflow overview and a timeline that distinguishes AI
stages, human review gates, and backend execution stages.

The first-half persisted outputs are rendered without raw JSON:

- Shared Planner summary, ordered specialist delegation, and planner notices.
- C1 Waste Analysis records, advisory priority and handling, concerns,
  structured rationale, and source paging metadata.
- C2 Collection Planning candidate groups, proposed schedules, separate
  handling, deferred needs, warnings, and complete versus partial source
  snapshot coverage.

An empty specialist result is distinct from a missing or incompatible legacy
result. A partial C2 snapshot is shown as a caution, not as approval-ready.
Step 11C is deliberately read-only: collection approval, revision, rejection,
and execution controls are deferred to Step 11D. C3/C4 detailed cards remain
deferred to Step 11E. The browser still never calls Python.

## Step 11D collection-plan human review and execution

When an authoritative C2 result is completed and has a complete source
snapshot, either authorized role can approve it, request revision, or reject
it. Revision and rejection require a 5–500 character reason; approval may
include an optional note of up to 500 characters. These decisions always use
the workflow's persisted version and never execute the plan implicitly.

After `CollectionApproved`, a separate explicit action sends only the updated
`expectedVersion` to ASP.NET. The backend remains responsible for creating
Scheduled Collection Tasks and continuing C3/C4. The React UI shows only the
authoritative resulting status and a compact scheduled-task creation summary;
detailed C3/C4 cards and dispatch actions remain deferred.

Every mutation refreshes both workflow detail and list queries. A 409 refreshes
the authoritative workflow without retrying the stale POST. Other transport
outcomes are treated as unconfirmed and likewise are never retried
automatically. The browser continues to call only ASP.NET; it has no Python
transport, credentials, polling, or orchestration coupling.

## Step 11E read-only C3 and C4 visualization

After approved collection execution, the shared detail page renders the
persisted `FleetPlanning.OutputJson` and `OperationalValidation.OutputJson`
returned by the same authenticated ASP.NET workflow-detail request. C3 remains
advisory: its driver and vehicle entries are recommendations, and its ordered
task list is always labelled **Suggested Stop Sequence**. It does not claim an
optimal, fastest, shortest, or otherwise optimized route.

C3 displays separate dispatch-plan cards, recommended driver and vehicle,
structured compatibility notes, unplanned Scheduled CollectionTasks, warnings,
and useful source-task counts. It never calls a route service, map provider,
fleet-resource endpoint, Python agent, or live-refresh mechanism.

C4 displays the persisted validation outcome, summary, per-plan reviews,
structured findings, unplanned-task findings, and warnings. `ReadyForHumanReview`
means ready for an authorized human review; it is not an approval, assignment,
or execution result. `NeedsRevision` is a valid advisory validation outcome.
When C4 records `requiresAcknowledgement`, the page informs the reviewer that
warning acknowledgement will be required before dispatch approval, but provides
no acknowledgement input or dispatch action.

## Step 11F dispatch human review and approved execution

Dispatch review is now part of the same shared detail page, after the persisted
C3 Fleet & Route and C4 Operational Validation sections. It exposes human
actions only when the authoritative workflow is `AwaitingDispatchApproval` and
C4 is valid with `ReadyForHumanReview`.

When C4 requires acknowledgement, the approval dialog presents an unchecked,
explicit warning acknowledgement checkbox. Approval, revision, and rejection
all use the workflow's current `expectedVersion`; revision and rejection require
a 5–500 character reason. A dispatch approval records a human decision only.
It does not create assignments.

After `DispatchApproved`, a separate confirmation sends only
`{ expectedVersion }` to ASP.NET. React never sends C3/C4 plans, task IDs,
driver IDs, vehicle IDs, routes, or a raw `CollectionAssignment` request.
ASP.NET revalidates current authoritative operational state and creates real
`CollectionAssignment` records. A completed workflow therefore means approved
planning and assignment creation completed; driver route execution continues in
the Driver application. C3 unplanned tasks can remain visible at completion.

Both collection and dispatch review refresh the workflow detail and list after
success, conflict, or an unconfirmed transport outcome. A `409 Conflict` is
shown as a stale workflow and is never retried automatically. Unknown mutating
outcomes are treated as unconfirmed and refreshed from ASP.NET rather than
reported as definite failure.

## Final UI flow and responsibility boundary

`Create → Start → Shared Planner / C1 / C2 → Collection Human Review → Execute
Approved Collection Plan → C3 / C4 → Dispatch Human Review → Execute Approved
Dispatch Plan → Workflow Completed`

- **AI** provides persisted advisory planning and validation outputs.
- **Human reviewers** approve, request revision, or reject at the C2 and C4
  boundaries.
- **ASP.NET** owns authentication, authorization, workflow state, optimistic
  concurrency, validation, persistence, and authoritative task/assignment
  creation.
- **Driver application** owns physical route execution; it is not implied by
  `Workflow Completed`.

Manager and WasteOfficer share the interface because they have equal backend
AgentWorkflow authority. React uses only the authenticated ASP.NET API client;
it has no Python URL, internal service key, Gemini credential, database
credential, polling, or browser-to-Python orchestration path.

For a concise viva explanation: the user starts a persisted workflow through
ASP.NET, reviews the structured advisory C1/C2 outputs, makes a human C2
decision, and separately authorizes ASP.NET to create Scheduled CollectionTasks.
C3 recommends the fleet, driver, vehicle, and Suggested Stop Sequence; C4
validates that advisory proposal. A human separately approves the dispatch,
acknowledging warnings when required, then separately authorizes ASP.NET to
freshly validate current operational state and create CollectionAssignments.
