# SmartWaste — Developer & AI Agent Guidelines

> **Status:** MANDATORY RULES & OPERATIONAL CONTRACT  
> **Audience:** All human developers, student contributors, and autonomous coding agents working on the SmartWaste repository.

---

## 1. Repository Topography

```
/
├── backend/          # Authoritative ASP.NET Core 8 Web API & Clean Architecture layers
├── web/              # React 18 + Vite + TypeScript web application (Officers / Managers)
├── mobile/           # Flutter 3.x Android application (Citizens / Drivers)
├── ai-service/       # Python 3.12 FastAPI + LangGraph internal AI service
├── docs/             # Frozen architectural, database, and API contract specifications
├── .github/          # GitHub Actions workflows and CI configurations
├── AGENTS.md         # Mandatory development rules (this file)
└── README.md         # Project overview and run guides
```

---

## 2. Source-of-Truth Hierarchy

Before making code modifications in any component, developers and agents **MUST** consult documentation in this strict order:

1. **`AGENTS.md`**: Mandatory constraints, scope discipline, and testing protocols.
2. **`docs/architecture.md`**: System topology, component boundaries, and AI governance.
3. **`docs/database-schema.md`**: Authoritative entity definitions, relations, enums, and indexes.
4. **`docs/api-contract.md`**: Frozen endpoints, HTTP methods, payloads, and status codes.
5. **Existing Codebase Implementation**: Current source code patterns and conventions.

> [!CAUTION]
> If implementation and source-of-truth documentation disagree, do not silently choose one. Report the conflict unless the current task explicitly authorizes a contract change. For an authorized change, update the relevant documentation and implementation together.

---

## 3. Scope & Change Discipline

- **Task Isolation**: Modify **only** files strictly necessary to satisfy the assigned task.
- **No Speculative Abstractions**: Implement only what is required. Keep solutions straightforward, maintainable, and defensible in an academic viva setting.
- **No Unsolicited Refactoring**: Do not reformat or refactor unrelated files, variable names, or existing passing tests.
- **Frozen Contracts**: Do not rename entities, table columns, routes, or enum values without explicit contractual agreement.
- **Dependency Guard**: Do not introduce new NuGet, npm, pub, or pip packages without explicit project approval.

---

## 4. Git & Version Control Rules for Agents

Unless explicitly instructed otherwise:
- **NO `git commit`**: Never create git commits autonomously.
- **NO `git push`**: Never push changes to remote repositories.
- **NO branch manipulation**: Do not create, switch, delete, or merge branches.
- **Local Working Tree Only**: Apply all modifications strictly to the local working directory.
- **Audit Trail**: Clearly report all created, modified, or deleted files in the final task output.

---

## 5. Backend Development Rules (`/backend`)

- **Authoritative Gateway**: ASP.NET Core is the **sole** authoritative backend. It controls database access, business rules, and security.
- **Layer Separation**:
  - `SmartWaste.Api`: Controllers, HTTP mapping, middleware, Swagger.
  - `SmartWaste.Application`: Use cases, DTOs, validations, service interfaces.
  - `SmartWaste.Domain`: Entities, enums, domain invariants (no external dependencies).
  - `SmartWaste.Infrastructure`: EF Core, PostgreSQL mappings, Identity, external/AI HTTP clients.
- **API Standards**:
  - Prefix: Always preserve `/api/v1`.
  - JSON Casing: Always use `camelCase`.
- **Validation**: Business rules and data validation must reside in application services or FluentValidation validators, **never** solely in controller action methods.
- **Dependency Injection**: Use typed clients (`AddHttpClient<TInterface, TImpl>()`) and strongly typed options (`IOptions<TOptions>`).
- **Platform Role Invariants**:
  - React Web connects users with roles `WasteOfficer` and `MunicipalManager`.
  - Flutter Mobile connects users with roles `Citizen` and `Driver`.
  - Incompatible platform login attempts are authoritatively rejected by ASP.NET Core with `403 Forbidden` (`ProblemDetails`) and issue no JWT.
  - Internal accounts (`Driver`, `WasteOfficer`, `MunicipalManager`) are provisioned exclusively by `MunicipalManager` with secure temporary passwords and mandatory first-login password change (`MustChangePassword = true`). Tokens with `must_change_password=True` are restricted from accessing operational business endpoints.
  - Citizen accounts can only be created via public registration (`POST /api/v1/auth/register`).

---

## 6. Database & Migration Rules

- **Single Database**: Exactly one PostgreSQL database, managed via a single `AppDbContext`.
- **Authoritative Migrations**: All schema modifications **MUST** be executed via EF Core migrations (`dotnet ef migrations add`). Never edit the production schema directly.
- **Migration Ownership**: Only one migration should be generated per conceptual schema milestone. Multiple agents must not independently create overlapping migrations.
- **Data Integrity**: Enforce foreign keys, unique constraints, and UTC timestamps (`timestamptz`).

---

## 7. Web Application Rules (`/web`)

- **Sole Communication Target**: React connects **only** to ASP.NET Core (`http://localhost:5276/api/v1` or configured `VITE_API_BASE_URL`).
- **Never Call FastAPI**: React must **NEVER** call the Python AI service directly.
- **Client Discipline**: Do not duplicate authoritative server-side business rules on the client. UI checks are for UX only; backend validates authoritatively.
- **Centralized API Client**: Use the established `axiosClient` with Bearer token interceptor and standard token storage.

### React Web UI & Styling
- **Authoritative Framework**: Tailwind CSS v4 is the primary styling approach for the React web application via the official Vite plugin (`@tailwindcss/vite`).
- **Visual Foundation**: The established SmartWaste visual language features:
  - Deep forest/emerald sidebar and navigation (`#064e3b` / dark emerald with bright emerald active indicators)
  - White content surfaces on a light cool-gray page background (`#f8fafc`)
  - Dark navy/slate primary text (`#0f172a`) and muted blue-gray secondary text (`#64748b`)
  - Restrained SmartWaste green accents (pale green circular icon containers, emerald primary actions)
  - Subtle borders and soft shadows
  - Compact operational dashboard density with clear section hierarchy
  - Reusable UI/layout components (`Button`, `Input`, `Card`, `Alert`, `LoadingSpinner`, `DashboardLayout`)
  - Responsive and accessible design across mobile, tablet, and desktop viewports
  - No unrelated UI frameworks and no random per-page themes
  - No excessive gradients, glassmorphism, or neon styling
  - Future React pages must visually align with the established dashboard theme.
- **Reference Consistency**: Reference-style consistency should be achieved through shared theme tokens and reusable components rather than copying large Tailwind class strings independently across pages.
- **No Competing Frameworks**: Do not introduce a second CSS/UI framework (Bootstrap, Material UI, Chakra, Radix, etc.) without explicit approval.
- **No Static Inline Styles**: Avoid static inline `style={{ ... }}` objects for normal UI styling; use Tailwind utility classes.
- **Reusable Primitives**: Prefer reusable components from `web/src/components/ui` (`Button`, `Input`, `Card`, `Alert`, `LoadingSpinner`) and established layouts (`web/src/layouts`).
- **Accessibility & Responsiveness**: Maintain semantic HTML, accessible form labels, keyboard navigation, and visible focus rings.
- **Header Account Menu**: Authenticated dashboard layouts should expose account/session actions through the shared header account menu rather than requiring users to scroll the sidebar for Logout.
- **Role Dashboard Shell Reuse**: WasteOfficer and MunicipalManager dashboards must reuse the shared SmartWaste dashboard shell and visual theme; role-specific content and navigation should be supplied through reusable configuration and components rather than duplicated layouts.

---

## 8. Mobile Application Rules (`/mobile`)

- **Sole Communication Target**: Flutter connects **only** to ASP.NET Core.
- **Never Call FastAPI**: Flutter must **NEVER** call the Python AI service directly.
- **Secure Storage**: JWT access tokens must be stored strictly in hardware-backed `FlutterSecureStorage`.
- **Configurable Networking**: Preserve configurable base URLs via `ApiConstants` (`10.0.2.2:5276` for Android Emulator, `--dart-define=API_BASE_URL=...` for physical devices). Never hardcode personal LAN IP addresses into committed files.

### Flutter Mobile UI & Styling
- **Centralized Design System**: Flutter uses centralized Material 3 `ThemeData` (`mobile/lib/core/theme/`) and shared SmartWaste UI widgets (`mobile/lib/shared/widgets/`).
- **Reuse Foundation**: New screens must reuse established colors (`AppColors`), typography (`AppTypography`), spacing (`AppSpacing`), and reusable components (`AppButton`, `AppTextField`, `AppCard`, `AppAlert`, `AppLoadingIndicator`).
- **No Hard-Coded Styling**: Avoid repeated hard-coded colors, padding, and corner radius across screens.
- **No Competing Frameworks**: Do not introduce a second styling or UI framework without explicit project approval.
- **Product Identity**: Flutter mobile UI should reflect the SmartWaste product identity (forest/emerald greens, slate text, light cool-gray background `#F8FAFC`, white surfaces, restrained accents) while remaining mobile-native.
- **Responsiveness & Accessibility**: Screens must be responsive across phone form factors (`SafeArea`, `SingleChildScrollView`, keyboard avoidance) and accessible (semantic widgets, clear contrast, 48dp min tap targets, clear text error indicators).
- **Shell & Theme Reuse**: Citizen and Driver dashboards reuse the shared authenticated SmartWaste mobile shell and theme, while role-specific navigation and content remain strictly separate.

---

## 9. AI Service & Agentic Governance (`/ai-service`)

- **Internal Microservice Only**: FastAPI is strictly private on `127.0.0.1:8000` (or private Docker network).
- **No Direct Database Access**: Python has **zero** direct connection to PostgreSQL.
- **No Authoritative Writes**: The AI service cannot commit operational changes. It returns structured proposals to ASP.NET Core.
- **Allow-Listed Tools**: Agents invoke only pre-registered tools with strongly validated schemas.
- **Deterministic Override**: Programmatic business rules in ASP.NET Core always override LLM recommendations.
- **Human-in-the-Loop (HITL)**: High-impact actions (dispatch, re-routing) require `MunicipalManager` approval before database commit.
- **Audit & Privacy**:
  - Never store hidden chain-of-thought tokens in the database.
  - Store structured inputs, tool calls, and summary results in `AiWorkflowStep` and `AiToolCall`.
- **Provider Isolation**: Do not configure external LLM API keys (OpenAI, Anthropic, Gemini) until specifically instructed.

---

## 10. Testing & Verification Protocol

After implementing changes in any component, execute the corresponding test suite and report real outcomes:

| Subsystem | Required Verification Commands | Success Criteria |
| :--- | :--- | :--- |
| **Backend** | `dotnet build backend/SmartWaste.sln`<br>`dotnet test backend/SmartWaste.sln` | 0 compilation errors, 0 warnings.<br>All unit and integration tests pass. |
| **Web** | `npm test` (or `npm run build` in `/web`) | Clean TypeScript compilation and test pass. |
| **Mobile** | `flutter analyze`<br>`flutter test` in `/mobile` | 0 analyzer issues, all widget/unit tests pass. |
| **AI Service** | `pytest tests` in `/ai-service` | All pytest test cases pass. |

> **Honesty Mandate:** Never claim tests passed unless they were physically executed with exit code 0.

---

## 11. Team Collaboration & Student Ownership

The system represents four distinct student modules coordinated under a shared foundation:

1. **Component 1**: Waste Reporting & Citizen Management
2. **Component 2**: Waste Collection & Bin Management
3. **Component 3**: Fleet, Driver & Route Management
4. **Component 4**: Operations, Complaints & Analytics

### Collaboration Protocol
- **Shared Files**: Exercise extreme care when modifying `AppDbContext.cs`, `Program.cs`, shared enums (`AppRoles`), migrations, or root configurations.
- **Feature Branches**: When collaborating on Git, use short-lived feature branches (`feat/comp1-reporting`, `feat/comp2-bins`).
- **Main Branch**: `main` must remain deployable, compiling, and tested at all times.

---

## 12. Coding Agent Final Reporting Requirement

At the conclusion of every automated agent task, provide a structured report containing:

1. **Files Created / Modified / Deleted**: Full relative paths.
2. **Functionality Implemented**: Concise summary of new capabilities.
3. **Verification Commands & Results**: Exact commands run with pass/fail counts and output summaries.
4. **Assumptions Made**: Technical choices made within the boundaries of the frozen specification.
5. **Unresolved Issues / Next Steps**: Open items or prerequisites for the next phase.
6. **Contract Integrity Check**: Explicit confirmation that all frozen contracts (`docs/`) were respected without unauthorized alterations.
