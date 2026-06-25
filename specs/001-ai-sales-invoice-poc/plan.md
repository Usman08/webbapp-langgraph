# Implementation Plan: AI-Native Sales Invoice PoC

**Branch**: `001-ai-sales-invoice-poc` | **Date**: 2026-06-25 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/001-ai-sales-invoice-poc/spec.md`

## Summary

An AI-native sales invoice tool where a sales operator types a natural-language request
(e.g. *"Create an invoice for ABC Traders. Same products as last month. Increase quantities
by 20% and apply the usual discount."*) and a multi-step AI workflow interprets the intent,
identifies the customer, analyses purchase history, adjusts quantities, recommends products,
validates inventory, calculates pricing/discounts, and produces an invoice draft for
human approval — surfacing every reasoning step in real time.

**Technical approach**: A .NET Core Web API hosts the business domain (pricing, discounts,
inventory, customer, invoice rules) and is the single source of truth for all data in
PostgreSQL. A Python **LangGraph** service runs the agentic workflow as a stateful graph;
its nodes call back into the .NET API's internal "agent-tool" endpoints so all business
logic stays in the .NET monolith. The LangGraph service streams workflow steps over
Server-Sent Events (SSE) to the .NET API, which relays them to a React SPA. The React UI
(mobile-first, design system from the `ui-ux-pro-max` skill) renders the request area,
real-time workflow progress, execution history, and invoice preview with an approval gate.

## Technical Context

**Language/Version**:
- Backend API: C# / .NET 8 (LTS)
- AI Engine: Python 3.12
- Frontend: TypeScript 5.x / React 18

**Primary Dependencies**:
- Backend: ASP.NET Core 8 Web API, Entity Framework Core 8 (Npgsql provider), FluentValidation
- AI Engine: LangGraph, LangChain, langchain-groq, FastAPI, Uvicorn, httpx (calls back to .NET), SSE-Starlette
- Frontend: React 18, Vite, TanStack Query, React Router, Tailwind CSS, Lucide icons
- LLM: Groq API (`openai/gpt-oss-120b` default) via `langchain-groq` ChatGroq in the AI engine

**Storage**: PostgreSQL 16 (all entities; EF Core migrations + idempotent seeder)

**Testing**:
- Backend: xUnit + FluentAssertions (unit), WebApplicationFactory + Testcontainers-Postgres (integration)
- AI Engine: pytest (graph node + tool-contract tests, LLM calls mocked)
- Frontend: Vitest + React Testing Library (unit), Playwright (E2E for P1 acceptance)

**Target Platform**: Containerised web app (Docker Compose) — Linux containers; browser SPA
(mobile-first baseline 375 px, tablet 768 px, desktop 1280 px)

**Project Type**: Web application (React frontend + .NET API backend + Python AI sidecar + PostgreSQL)

**Performance Goals**:
- Draft invoice produced within 30 s of submission (SC-001)
- First workflow step visible in the UI < 2 s after submission (perceived responsiveness)
- API P95 < 500 ms for non-AI endpoints (constitution Quality standard)

**Constraints**:
- PostgreSQL mandatory for all entities (FR-021)
- Mobile-first, 375 px baseline; 44×44 px touch targets (Constitution I)
- Security-first: input sanitisation, no secrets in client bundle, parameterised queries (Constitution II)
- Human-in-the-loop approval gate is non-bypassable (FR-011, SC-007)
- Single-user PoC; no auth/multi-tenancy in scope (Assumptions)

**Scale/Scope**: Single-operator PoC. ~7 core entities, ~6 agent workflow nodes,
4 UI screens/panels, seeded dataset (3 customer types, ~12 products incl. 2 out-of-stock,
~6 historical invoices).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Mobile First | ✅ PASS | Design system from `ui-ux-pro-max`; breakpoints 375/768/1280; mobile-viewport Playwright test required before UI tasks close. |
| II. Security First | ✅ PASS (PoC exemption invoked) | EF Core parameterised queries; FluentValidation on all inputs; LLM/Groq keys server-side only (AI engine env vars), never in React bundle; SSE proxied through .NET (AI engine not publicly exposed). **End-user authentication is omitted under the Principle II PoC exemption (Constitution v1.1.0)**: (a) the feature is single-user and non-public; (b) the non-public service boundary `/internal/tools/*` is protected by the `X-Engine-Token` shared secret (task T023); (c) this exemption is recorded here. All other Security First requirements (input validation, secret handling, parameterised queries) remain in force. |
| III. Intuitive UX | ✅ PASS | Single-screen SPA; actionable inline errors (FR-019); loading/streaming states for the >300 ms AI workflow; approval confirmation gate (FR-011). |
| IV. Monolithic N-Layer | ⚠️ JUSTIFIED | Two runtime processes (.NET monolith + Python AI engine) instead of one. LangGraph has no .NET implementation, so the AI engine is a polyglot necessity. Mitigation: **all business/domain logic stays in the .NET N-layer monolith**; the Python service is a stateless reasoning orchestrator whose nodes call .NET agent-tool endpoints. See Complexity Tracking. |

**Gate result**: PASS with one justified deviation (Principle IV). No unjustified violations.

## Project Structure

### Documentation (this feature)

```text
specs/001-ai-sales-invoice-poc/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   ├── rest-api.md          # .NET public API (UI-facing)
│   ├── agent-tools.md       # .NET internal tool endpoints (AI-engine-facing)
│   └── workflow-events.md   # SSE event stream contract
├── checklists/
│   └── requirements.md  # Spec quality checklist (from /speckit-clarify)
└── tasks.md             # Phase 2 output (/speckit-tasks - NOT created here)
```

### Source Code (repository root)

```text
backend/                              # .NET 8 monolith (N-layer)
├── src/
│   ├── SalesInvoice.Api/             # Presentation: controllers, SSE relay, DI composition
│   ├── SalesInvoice.Application/     # Use cases / orchestration (CQRS handlers, DTOs)
│   ├── SalesInvoice.Domain/          # Entities, value objects, business rules
│   │                                 #   (pricing, discount, inventory, rounding)
│   └── SalesInvoice.Infrastructure/  # EF Core DbContext, migrations, seeder,
│                                     #   AI-engine HTTP client
└── tests/
    ├── SalesInvoice.UnitTests/       # Domain + Application logic
    └── SalesInvoice.IntegrationTests/# API + Postgres (Testcontainers)

ai-engine/                            # Python 3.12 LangGraph workflow service
├── app/
│   ├── graph/                        # State schema, nodes, edges, compiled graph
│   ├── tools/                        # Tool defs that call .NET agent-tool endpoints
│   ├── streaming/                    # SSE event emitter
│   └── main.py                       # FastAPI app (run-workflow endpoint + SSE)
└── tests/                            # pytest (nodes, tools, graph; LLM mocked)

frontend/                             # React 18 + Vite SPA
├── src/
│   ├── components/                   # WorkflowProgress, ExecutionLog, InvoicePreview, RequestBox
│   ├── pages/                        # Single workspace page
│   ├── services/                     # REST client, SSE (EventSource) client
│   ├── design-system/                # Tokens/components per ui-ux-pro-max MASTER.md
│   └── App.tsx
└── tests/                            # Vitest + Playwright

design-system/ai-sales-invoice-poc/   # ui-ux-pro-max output (MASTER + page overrides) — generated
docker-compose.yml                     # postgres + backend + ai-engine + frontend
```

**Structure Decision**: **Web application, multi-process**. Three deployable components plus
PostgreSQL, orchestrated by Docker Compose. The `.NET` backend is the N-layer monolith and the
authoritative owner of domain logic and data; `ai-engine` is a thin polyglot sidecar for the
LangGraph reasoning loop; `frontend` is the React presentation tier. This honours the
constitution's monolith intent for business logic while accommodating LangGraph's
Python-only reality (justified in Complexity Tracking).

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| Separate Python AI-engine process (polyglot, 2nd runtime) | LangGraph (the user-mandated AI workflow engine) exists only for Python/JS — there is no .NET LangGraph. The reasoning loop must run in Python. | (a) Reimplementing LangGraph orchestration in .NET would abandon the mandated engine and its streaming/state primitives. (b) Embedding via Python.NET/IronPython is fragile for async LLM + graph streaming. (c) Using LangGraph-JS in the React tier would push secrets/business calls to the client, violating Principle II. |
| Domain logic split risk between .NET and Python | The AI engine must *use* business rules (pricing, discounts, inventory). | Mitigation keeps logic single-sourced: Python nodes hold NO business rules — they call .NET internal agent-tool endpoints. The Python service stays a stateless orchestrator, preserving the monolith as the single source of business truth. |
