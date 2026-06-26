---
description: "Task list for AI-Native Sales Invoice PoC"
---

# Tasks: AI-Native Sales Invoice PoC

**Input**: Design documents from `/specs/001-ai-sales-invoice-poc/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: INCLUDED — the project constitution (Quality & Testing Standards) mandates unit,
integration, and E2E tests (≥80% domain/application coverage; E2E for every P1 acceptance scenario).

**Organization**: Tasks grouped by user story (US1–US4) for independent implementation and testing.

## Stack & Path Conventions

- **Backend (.NET 8 monolith)**: `backend/src/{SalesInvoice.Api,SalesInvoice.Application,SalesInvoice.Domain,SalesInvoice.Infrastructure}`, tests in `backend/tests/`
- **AI engine (Python 3.12 LangGraph)**: `ai-engine/app/`, tests in `ai-engine/tests/`
- **Frontend (React 18 + Vite)**: `frontend/src/`, tests in `frontend/tests/`
- **Design system**: `design-system/ai-sales-invoice-poc/` (already generated)
- **Orchestration**: `docker-compose.yml`, `.env.example` at repo root

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and scaffolding for all three components.

- [X] T001 Create repo solution layout (`backend/`, `ai-engine/`, `frontend/` dirs) and root `README.md` per plan.md Project Structure
- [X] T002 [P] Initialize .NET 8 solution with 4 projects (Api/Application/Domain/Infrastructure) + 2 test projects in `backend/` (`SalesInvoice.sln`)
- [X] T003 [P] Initialize Python 3.12 `ai-engine/` project with `pyproject.toml` (langgraph, langchain, langchain-groq, fastapi, uvicorn, httpx, sse-starlette, pytest)
- [X] T004 [P] Initialize React 18 + Vite + TypeScript app in `frontend/` with Tailwind, TanStack Query, React Router, Lucide (`frontend/package.json`, `vite.config.ts`)
- [X] T005 [P] Map design tokens from `design-system/ai-sales-invoice-poc/MASTER.md` into `frontend/tailwind.config.ts` (palette #0F172A/#334155/#0369A1/#F8FAFC/#020617, Plus Jakarta Sans)
- [X] T006 [P] Configure backend linting/formatting (`.editorconfig`, analyzers) and `Directory.Build.props` in `backend/`
- [X] T007 [P] Configure Python lint/format (ruff + black) and frontend ESLint/Prettier configs
- [X] T008 Create root `docker-compose.yml` (services: postgres, backend, ai-engine, frontend; ai-engine on internal network only) and `.env.example` (GROQ_API_KEY, GROQ_MODEL=openai/gpt-oss-120b, ENGINE_TOKEN, POSTGRES_*)

**Checkpoint**: All three apps build/run empty; `docker compose up` starts Postgres.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared domain model, persistence, seeding, and host wiring required by ALL stories.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

### Domain entities & enums (backend/src/SalesInvoice.Domain)

- [X] T009 [P] Create enums `CustomerType`, `InvoiceStatus`, `LineStockStatus`, `RunStatus` in `backend/src/SalesInvoice.Domain/Enums/`
- [X] T010 [P] Create entity `Customer` in `backend/src/SalesInvoice.Domain/Entities/Customer.cs` (per data-model.md)
- [X] T011 [P] Create entities `Product` + `ProductAlternative` in `backend/src/SalesInvoice.Domain/Entities/`
- [X] T012 [P] Create entity `DiscountRule` in `backend/src/SalesInvoice.Domain/Entities/DiscountRule.cs`
- [X] T013 [P] Create entities `Invoice` + `InvoiceLineItem` in `backend/src/SalesInvoice.Domain/Entities/`
- [X] T014 [P] Create entities `WorkflowRun`, `WorkflowStep`, `ProductRecommendation` in `backend/src/SalesInvoice.Domain/Entities/`

### Domain business rules (single source of truth)

- [X] T015 [P] Implement `QuantityCalculator` (half-up percentage adjustment to whole units, FR-017) in `backend/src/SalesInvoice.Domain/Pricing/QuantityCalculator.cs`
- [X] T016 [P] Implement `InvoiceCalculator` (subtotal, discount, tax, total; ≤0.01 rounding, SC-006) in `backend/src/SalesInvoice.Domain/Pricing/InvoiceCalculator.cs`
- [X] T017 [P] Unit tests for `QuantityCalculator` + `InvoiceCalculator` in `backend/tests/SalesInvoice.UnitTests/Pricing/`

### Persistence (backend/src/SalesInvoice.Infrastructure)

- [X] T018 Create `AppDbContext` with EF Core entity configurations (uuid PKs, numeric(12,2), jsonb, enums) in `backend/src/SalesInvoice.Infrastructure/Persistence/AppDbContext.cs`
- [X] T019 Add Npgsql + initial EF Core migration generating all tables in `backend/src/SalesInvoice.Infrastructure/Persistence/Migrations/`
- [X] T020 Implement idempotent `DbSeeder` (3 customers Retail/Wholesale/VIP, ~12 products incl. ≥2 out-of-stock + one with no in-stock alternative, discount rules, fixed tax, historical invoices with co-purchase patterns — FR-015/016/021) in `backend/src/SalesInvoice.Infrastructure/Persistence/DbSeeder.cs`
- [X] T021 Integration test (Testcontainers-Postgres) verifying seeder is idempotent and dataset matches FR-015/016 in `backend/tests/SalesInvoice.IntegrationTests/SeederTests.cs`

### Host wiring & cross-cutting

- [X] T022 Configure `Program.cs` (DI, DbContext, run migrations + seeder at startup, FluentValidation, problem+json errors, CORS for frontend) in `backend/src/SalesInvoice.Api/Program.cs`
- [X] T023 [P] Add `X-Engine-Token` auth handler for `/internal/tools/*` (env-configured, server-side only — Principle II) in `backend/src/SalesInvoice.Api/Security/EngineTokenMiddleware.cs`
- [X] T024 [P] Create reference endpoints `GET /api/customers` and `GET /api/products` in `backend/src/SalesInvoice.Api/Controllers/ReferenceController.cs`
- [X] T025 [P] Scaffold AI engine skeleton: FastAPI app, settings (env), `.NET` agent-tool httpx client with `X-Engine-Token` in `ai-engine/app/main.py` + `ai-engine/app/client.py`
- [X] T026 [P] Define LangGraph state schema (`WorkflowState`) and empty compiled graph stub in `ai-engine/app/graph/state.py` + `ai-engine/app/graph/build.py`
- [X] T027 [P] Create React app shell: router, TanStack Query provider, single workspace page layout, design-system base components (Button, Card, Panel) using tokens — mobile-first 375px in `frontend/src/App.tsx` + `frontend/src/design-system/`
- [X] T028 [P] Create typed REST API client + `EventSource` SSE client wrapper in `frontend/src/services/apiClient.ts` + `frontend/src/services/sseClient.ts`

**Checkpoint**: Schema migrated + seeded; backend, ai-engine, and frontend boot; reference endpoints return seeded data.

---

## Phase 3: User Story 1 — Natural Language Invoice Request (Priority: P1) 🎯 MVP

**Goal**: Operator submits an NL request and receives a correct AI-generated invoice draft end-to-end.

**Independent Test**: Submit the ABC Traders example; verify correct customer, ×1.2 rounded quantities, applied discount, and a draft within 30s (SC-001) with no manual entry.

### Agent-tool endpoints (.NET — business logic; contracts/agent-tools.md)

- [X] T029 [P] [US1] Implement `POST /internal/tools/resolve-customer` (resolved/ambiguous/not_found) in `backend/src/SalesInvoice.Api/Controllers/Internal/ToolsController.cs` + `backend/src/SalesInvoice.Infrastructure/Tools/ResolveCustomerHandler.cs`
- [X] T030 [P] [US1] Implement `POST /internal/tools/get-purchase-history` (most recent invoice + co-purchase stats) in `backend/src/SalesInvoice.Infrastructure/Tools/GetPurchaseHistoryHandler.cs`
- [X] T031 [P] [US1] Implement `POST /internal/tools/adjust-quantities` (uses `QuantityCalculator`) in `backend/src/SalesInvoice.Application/Tools/AdjustQuantitiesHandler.cs`
- [X] T032 [P] [US1] Implement `POST /internal/tools/validate-inventory` (InStock/AlternativeSuggested/BackOrder) in `backend/src/SalesInvoice.Infrastructure/Tools/ValidateInventoryHandler.cs`
- [X] T033 [P] [US1] Implement `POST /internal/tools/resolve-discount` (resolved/no_rule) in `backend/src/SalesInvoice.Infrastructure/Tools/ResolveDiscountHandler.cs`
- [X] T034 [US1] Implement `POST /internal/tools/build-draft` (persist Draft invoice + lines, compute totals via `InvoiceCalculator`) in `backend/src/SalesInvoice.Infrastructure/Tools/BuildDraftHandler.cs`
- [X] T035 [P] [US1] Integration tests for all six agent-tool endpoints against seeded Postgres in `backend/tests/SalesInvoice.IntegrationTests/ToolsTests.cs`

### LangGraph workflow (ai-engine)

- [X] T036 [US1] Implement tool wrappers calling .NET agent-tools (resolve-customer, history, adjust, inventory, discount, build-draft) in `ai-engine/app/tools/`
- [X] T037 [US1] Implement graph nodes (intent parse → customer lookup → history → quantity adjust → inventory → discount → build draft) and wire edges in `ai-engine/app/graph/nodes.py` + `build.py`
- [X] T038 [US1] Bind Groq `openai/gpt-oss-120b` (via `langchain-groq` ChatGroq) with tool-use for structured intent extraction (R7) in `ai-engine/app/graph/llm.py`
- [X] T039 [US1] Implement `POST /run` engine endpoint that executes the graph for a request in `ai-engine/app/main.py`
- [X] T040 [P] [US1] pytest for graph happy-path with mocked LLM + mocked .NET tools in `ai-engine/tests/test_workflow_us1.py`

### .NET request orchestration

- [X] T041 [US1] Implement `POST /api/invoices/requests` (create WorkflowRun, trigger ai-engine `/run`, return runId) in `backend/src/SalesInvoice.Api/Controllers/InvoiceRequestsController.cs`
- [X] T042 [US1] Implement `GET /api/invoices/requests/{runId}` (run state + draft + steps) and `GET /api/invoices/{invoiceId}` (full draft) in `backend/src/SalesInvoice.Api/Controllers/`
- [X] T043 [P] [US1] Implement `POST /api/invoices/requests/{runId}/disambiguate` (FR-018) in `InvoiceRequestsController.cs`

### Frontend (happy path)

- [X] T044 [P] [US1] Build `RequestBox` component (NL input, submit, char limit/sanitise) in `frontend/src/components/RequestBox.tsx`
- [X] T045 [P] [US1] Build `InvoicePreview` component (line items, totals, stock flags) in `frontend/src/components/InvoicePreview.tsx`
- [X] T046 [US1] Wire submit → poll/load draft → render preview on the workspace page in `frontend/src/pages/Workspace.tsx`
- [X] T047 [US1] Add customer disambiguation UI (FR-018) in `frontend/src/components/DisambiguationDialog.tsx`

### E2E

- [X] T048 [US1] Playwright E2E for V1 (ABC Traders → correct draft within 30s) AND US1 acceptance scenario 2 (an out-of-stock line is flagged with a suggested alternative in the draft) in `frontend/tests/e2e/us1-nl-request.spec.ts`

**Checkpoint**: US1 fully functional and independently testable — MVP ready.

---

## Phase 4: User Story 2 — Workflow Transparency & Execution Log (Priority: P2)

**Goal**: Operator watches each AI step stream in real time with tool name, input, output; full log persists.

**Independent Test**: Submit any valid request; confirm ≥6 labelled steps (SC-002) stream live with inputs/outputs, and the log survives reload.

- [X] T049 [P] [US2] Implement `POST /internal/tools/record-step` (persist WorkflowStep, FR-014) in `backend/src/SalesInvoice.Application/Tools/RecordStepHandler.cs`
- [X] T050 [US2] Emit SSE events per node/tool (run_started, node_started, tool_invoked, tool_result, decision, exception, draft_ready, workflow_complete — contracts/workflow-events.md) in `ai-engine/app/streaming/events.py` + integrate into graph nodes
- [X] T051 [US2] Engine: call `record-step` for each step as it streams (1:1 with SSE events) in `ai-engine/app/graph/nodes.py`
- [X] T052 [US2] Implement `GET /api/invoices/requests/{runId}/stream` SSE relay (proxy ai-engine stream, keep engine private) in `backend/src/SalesInvoice.Api/Controllers/InvoiceRequestsController.cs`
- [X] T053 [P] [US2] Build `WorkflowProgress` component (ordered step nodes, live status) in `frontend/src/components/WorkflowProgress.tsx`
- [X] T054 [P] [US2] Build `ExecutionLog` component (tool name, input, output, exception highlighting) in `frontend/src/components/ExecutionLog.tsx`
- [X] T055 [US2] Subscribe to SSE on submit; render steps live; respect `prefers-reduced-motion` in `frontend/src/pages/Workspace.tsx`
- [X] T056 [P] [US2] Integration test: SSE event ordering + every tool_invoked has one tool_result/exception (contract) in `backend/tests/SalesInvoice.IntegrationTests/StreamTests.cs`
- [X] T057 [P] [US2] pytest: step persistence + event/step 1:1 mapping in `ai-engine/tests/test_streaming.py`

**Checkpoint**: US1 + US2 work together; reasoning is fully transparent and reload-safe.

---

## Phase 5: User Story 3 — Product Recommendation (Priority: P3)

**Goal**: AI recommends co-purchased products with justification; operator accepts/declines with live recalculation.

**Independent Test**: Use a customer with a known co-purchase pattern; confirm ≥1 justified recommendation; accepting adds a correctly priced line (FR-009).

- [ ] T058 [P] [US3] Implement `POST /internal/tools/recommend-products` (co-purchase ranking from history) in `backend/src/SalesInvoice.Application/Tools/RecommendProductsHandler.cs`
- [ ] T059 [US3] Add recommendation node + `recommendation` SSE event to the graph in `ai-engine/app/graph/nodes.py`
- [ ] T060 [US3] Implement `POST /api/invoices/requests/{runId}/recommendations/{id}` (accept/decline → recalc draft) in `backend/src/SalesInvoice.Api/Controllers/InvoiceRequestsController.cs`
- [ ] T061 [P] [US3] Build `RecommendationPanel` component (product, basis text, accept/decline) in `frontend/src/components/RecommendationPanel.tsx`
- [ ] T062 [US3] Wire accept/decline → recalc → update preview in `frontend/src/pages/Workspace.tsx`
- [ ] T063 [P] [US3] Integration test: recommendation generation + accept recalculation in `backend/tests/SalesInvoice.IntegrationTests/RecommendationTests.cs`

**Checkpoint**: US1–US3 independently functional.

---

## Phase 6: User Story 4 — Human-in-the-Loop Approval Gate (Priority: P4)

**Goal**: Operator must approve before finalisation; can reject/edit (status stays Draft); finalised invoice reflects edits.

**Independent Test**: Submit → Reject/Edit (lines editable, status Draft) → edit qty → Approve → Finalised in history with edits applied; finalisation impossible without explicit approval (SC-007).

- [ ] T064 [US4] Implement `PUT /api/invoices/{invoiceId}/lines` (edit draft, recalc; 409 if Finalised, FR-012) in `backend/src/SalesInvoice.Api/Controllers/InvoicesController.cs`
- [ ] T065 [US4] Implement `POST /api/invoices/{invoiceId}/approve` (Draft→Finalised, link WorkflowRun→Invoice, FR-011/013a; 409 if already Finalised) in `InvoicesController.cs`
- [ ] T066 [US4] Implement `POST /api/invoices/{invoiceId}/reject` (unlock editing, status stays Draft, FR-012) in `InvoicesController.cs`
- [ ] T067 [P] [US4] Implement `GET /api/invoices` (history, `?status=` filter) and `GET /api/invoices/{invoiceId}/workflow` (reasoning trail, FR-013a) in `InvoicesController.cs`
- [ ] T068 [P] [US4] Build approval gate UI (Approve / Reject-Edit, editable lines incl. BackOrder retain/remove FR-020, confirmation) in `frontend/src/components/ApprovalGate.tsx`
- [ ] T069 [P] [US4] Build `InvoiceHistory` view with drill-in to finalised invoice + its workflow trail in `frontend/src/components/InvoiceHistory.tsx`
- [ ] T070 [US4] Wire approve/reject/edit flows and history navigation in `frontend/src/pages/Workspace.tsx`
- [ ] T071 [P] [US4] Integration tests: approval finalises + reflects edits; no-approval never finalises (SC-007); reject keeps Draft in `backend/tests/SalesInvoice.IntegrationTests/ApprovalTests.cs`
- [ ] T072 [US4] Playwright E2E for V5 (reject→edit→approve→history) and V6 (persistence across reload), asserting the full submit→approve journey completes in under 2 minutes (SC-004), in `frontend/tests/e2e/us4-approval.spec.ts`

**Checkpoint**: All user stories independently functional and integrated.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Edge cases, quality gates, and constitution compliance across all stories.

- [ ] T073 [P] Implement `parse_error` handling end-to-end (FR-019): engine emits event, UI shows inline actionable error keeping input editable; test in `frontend/tests/e2e/parse-error.spec.ts`
- [ ] T074 [P] Edge-case tests: first-time customer (no prior invoice), fractional rounding, missing discount rule, cascade out-of-stock BackOrder (FR-020) in `backend/tests/SalesInvoice.IntegrationTests/EdgeCaseTests.cs`
- [ ] T075 [P] Mobile-first verification: Playwright 375px viewport project (no horizontal scroll, ≥44px touch targets — Constitution I) in `frontend/tests/e2e/mobile.spec.ts`
- [ ] T076 [P] Accessibility pass: focus states, aria-labels on icon buttons, color-contrast ≥4.5:1, form labels (per ui-ux-pro-max checklist) across `frontend/src/components/`
- [ ] T077 [P] Security review: confirm input sanitisation/validation on all endpoints, no secrets in client bundle, ai-engine not browser-reachable, parameterised queries (Constitution II)
- [ ] T078 Verify domain/application unit coverage ≥80% (Constitution Quality gate); add tests where short in `backend/tests/SalesInvoice.UnitTests/`
- [ ] T079 [P] Run `quickstart.md` V1–V8 validation scenarios end-to-end via `docker compose up` and record results
- [ ] T080 [P] Add `README.md` run instructions + architecture diagram reference (plan.md) at repo root
- [ ] T081 [P] Smoke perf check: assert P95 < 500 ms for non-AI endpoints (`GET /api/products`, `GET /api/invoices`) under light concurrent load (Constitution Quality gate); record results via a k6/bombardier script at repo root (`perf/smoke.js`)

---

## Dependencies & Execution Order

### Phase dependencies

- **Setup (P1)**: no dependencies.
- **Foundational (P2)**: depends on Setup. **BLOCKS all user stories.**
- **User Stories (P3–P6)**: all depend on Foundational. US1 is the MVP; US2–US4 build on US1's run but are independently testable.
- **Polish (P7)**: depends on the targeted user stories being complete.

### Story dependencies

- **US1 (P1)**: only Foundational. Delivers MVP.
- **US2 (P2)**: needs US1's workflow to stream/persist steps (T041/T039). Independently testable on top of US1.
- **US3 (P3)**: needs US1's draft/recalc path. Independent of US2.
- **US4 (P4)**: needs US1's draft. Independent of US2/US3; integrates BackOrder (FR-020) from US1's inventory step.

### Within each story

- Agent-tool endpoints (.NET) → engine tool wrappers/nodes → .NET orchestration → frontend → E2E.
- Models/calculators (Foundational) precede all handlers.

---

## Parallel Execution Examples

```text
# Foundational entities (after T009 enums):
T010, T011, T012, T013, T014  → all [P] (different entity files)
T015, T016                    → [P] then T017 tests

# US1 agent tools (after Foundational):
T029, T030, T031, T032, T033  → all [P] (different handler files); T034 after (build-draft)

# US2 UI:
T053, T054                    → [P] (different components)
```

---

## Implementation Strategy

### MVP First (US1 only)

1. Phase 1 Setup → 2. Phase 2 Foundational → 3. Phase 3 US1 → **STOP & validate V1** → demo.

### Incremental Delivery

US1 (MVP) → add US2 (transparency) → add US3 (recommendations) → add US4 (approval gate) →
Polish. Each story is demoable without breaking prior stories.

### Notes

- [P] = different files, no incomplete dependencies.
- Tests are mandated by the constitution — keep them in each story's slice, not deferred.
- Commit after each task or logical group; verify the story's Independent Test at each checkpoint.
