# AI-Native Sales Invoice PoC

An AI-native sales invoice tool where a sales operator submits a natural-language request and a multi-step LangGraph workflow interprets intent, resolves customer and purchase history, adjusts quantities, validates inventory, applies discounts, and produces an invoice draft for human approval — streaming every reasoning step in real time.

## Architecture

- **Backend** (`backend/`): .NET 8 N-layer monolith (Api / Application / Domain / Infrastructure). Owns all business logic and data in PostgreSQL.
- **AI Engine** (`ai-engine/`): Python 3.12 LangGraph workflow service. Calls .NET agent-tool endpoints; streams SSE events back through the .NET relay.
- **Frontend** (`frontend/`): React 18 + Vite SPA. Mobile-first (375 px baseline). Renders workflow progress, invoice preview, and approval gate.
- **Database**: PostgreSQL 16 via EF Core 8 (Npgsql).

```
frontend (React SPA)
       ↕  REST + SSE relay
backend (.NET API)  ←→  PostgreSQL
       ↕  HTTP (internal, X-Engine-Token)
ai-engine (LangGraph)
       ↕  Groq API (openai/gpt-oss-120b)
```

## Quick Start

```bash
cp .env.example .env
# Fill in GROQ_API_KEY in .env
docker compose up
```

Frontend: http://localhost:5173  
Backend API: http://localhost:8080  
AI Engine (internal only): http://localhost:8000

## Validation Scenarios (V1–V8)

Full end-to-end validation steps are in [`specs/001-ai-sales-invoice-poc/quickstart.md`](specs/001-ai-sales-invoice-poc/quickstart.md), covering:

| Scenario | What it tests |
|---|---|
| V1 | NL request → draft invoice within 30 s (SC-001) |
| V2 | ≥6 labelled workflow steps streaming live (SC-002) |
| V3 | Out-of-stock alternative / back-order handling (SC-003) |
| V4 | AI product recommendation accept/decline |
| V5 | Reject → edit → approve full journey under 2 min (SC-004) |
| V6 | Invoice + workflow trail persists across reload (SC-008) |
| V7 | Parse error shows actionable message (FR-019) |
| V8 | 375 px mobile viewport — no horizontal scroll, 44 px targets |

## Running Tests

```bash
# .NET unit tests (domain logic, pricing)
cd backend && dotnet test tests/SalesInvoice.UnitTests

# .NET integration tests (requires Docker — Testcontainers spins up Postgres 16)
cd backend && dotnet test tests/SalesInvoice.IntegrationTests

# Python AI engine tests (mocked LLM + tools)
cd ai-engine && pytest

# Playwright E2E (requires full stack running at localhost:5173)
cd frontend && npx playwright test

# k6 smoke performance test (requires backend running at localhost:5261)
k6 run perf/smoke.js
```

## Environment Variables

| Variable | Service | Description |
|---|---|---|
| `GROQ_API_KEY` | ai-engine | Groq API key |
| `ENGINE_TOKEN` | backend + ai-engine | Shared secret for `/internal/tools/*` |
| `POSTGRES_PASSWORD` | backend + postgres | Database password |
| `AI_ENGINE_URL` | backend | Internal URL of the LangGraph service |

## Key Design Decisions

- **SSE streaming**: the AI engine emits named SSE events per workflow node; the .NET API relays them to the browser so no polling is needed.
- **asyncio.Queue per run_id**: isolates concurrent workflow runs in the Python service; a SENTINEL signals end-of-stream.
- **X-Engine-Token**: `/internal/tools/*` endpoints are only reachable from Docker's internal network — not from the browser.
- **Testcontainers-Postgres**: integration tests use a real Postgres 16 container to prevent mock/prod divergence.
- **MidpointRounding.AwayFromZero**: used throughout `InvoiceCalculator` so rounding error stays ≤ 0.01 (SC-006).

## Development

See [`specs/001-ai-sales-invoice-poc/plan.md`](specs/001-ai-sales-invoice-poc/plan.md) for full stack and architecture details.

### Backend

```bash
cd backend
dotnet restore
dotnet build
dotnet test
```

### AI Engine

```bash
cd ai-engine
python -m venv .venv && source .venv/bin/activate  # or .venv\Scripts\activate on Windows
pip install -e ".[dev]"
uvicorn app.main:app --reload
```

### Frontend

```bash
cd frontend
npm install
npm run dev
```
