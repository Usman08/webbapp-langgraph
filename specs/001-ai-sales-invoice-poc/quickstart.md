# Quickstart & Validation Guide: AI-Native Sales Invoice PoC

This guide proves the feature works end-to-end. It is a run/validation script, not an
implementation reference — see [data-model.md](./data-model.md) and [contracts/](./contracts/)
for details.

## Prerequisites

- Docker + Docker Compose
- An Anthropic API key
- Free ports: 5173 (frontend), 8080 (.NET API), 5432 (Postgres)

## Setup

```bash
# from repo root
cp .env.example .env            # set ANTHROPIC_API_KEY, ENGINE_TOKEN, POSTGRES_* , ANTHROPIC_MODEL=claude-sonnet-4-6
docker compose up --build       # starts postgres, backend, ai-engine, frontend
```

On startup the .NET API applies EF Core migrations and runs the **idempotent seeder**
(FR-015/021): 3 customers (Retail/Wholesale/VIP), ~12 products (≥2 out-of-stock, one with
no in-stock alternative), discount rules, and historical invoices. Re-running is safe.

Open `http://localhost:5173`.

## Validation scenarios

### V1 — Core NL request → draft (US1, P1) ✅ MVP
1. In the request box, enter:
   `Create an invoice for ABC Traders. Same products as last month. Increase quantities by 20% and apply the usual discount.`
2. Submit.
   - **Expect**: workflow steps stream in (customer lookup → history → quantity adjust →
     inventory → discount → draft). Draft appears **within 30 s** (SC-001).
   - **Expect**: quantities are last month's × 1.2, rounded to whole units (FR-017);
     wholesale discount applied; totals correct to ≤ 0.01 (SC-006).

### V2 — Workflow transparency (US2, P2)
1. During/after V1, inspect the progress panel and execution log.
   - **Expect**: **≥ 6 labelled steps** (SC-002), each showing tool name, input, output.
   - **Expect**: any out-of-stock handling appears as an `exception` entry with reasoning
     and resolution (US2 #3).

### V3 — Out-of-stock & alternatives / back-order (FR-007, FR-020)
1. Submit a request for a customer whose last order includes a seeded out-of-stock product.
   - **Expect**: line flagged `AlternativeSuggested` with a proposed alternative (SC-003), OR
     `BackOrder` when no alternative is in stock — retained, not dropped (FR-020).

### V4 — Product recommendation (US3, P3)
1. Use a customer with a co-purchase pattern (e.g. buys SKU-1 with SKU-9).
   - **Expect**: a recommendation with justification; accepting it adds a correctly priced
     line and recalculates totals (FR-009); declining leaves the draft unchanged.

### V5 — Human-in-the-loop approval (US4, P4)
1. On the draft, click **Reject / Edit** → line items become editable (status stays `Draft`).
2. Edit a quantity, then **Approve**.
   - **Expect**: status → `Finalised`; invoice appears in history; finalised totals reflect the
     edit, not the original AI output (US4 #3).
   - **Expect**: with no approval click, the invoice is never finalised (SC-007).

### V6 — Persistence across sessions (FR-014, SC-008)
1. After finalising, refresh the browser / open a new tab and reload from history.
   - **Expect**: the invoice **and its full workflow reasoning trail** are intact
     (`GET /api/invoices/{id}/workflow`), proving Postgres-backed persistence (FR-013a).

### V7 — Parse failure (FR-019)
1. Submit gibberish (e.g. `asdfghjkl`).
   - **Expect**: inline actionable error + suggested phrasing; request text stays editable.

### V8 — Mobile-first (Constitution I)
1. Set viewport to **375 px**.
   - **Expect**: all panels usable, no horizontal scroll, touch targets ≥ 44 px.

## Automated test mapping

| Scenario | Test layer |
|----------|-----------|
| V1, V5 | Playwright E2E (frontend) |
| V3, V4, totals/rounding | .NET integration (Testcontainers-Postgres) + domain unit tests |
| Tool contracts (agent-tools.md) | .NET integration + pytest (engine, LLM mocked) |
| SSE event ordering (workflow-events.md) | .NET integration (stream) + pytest |
| V8 | Playwright mobile viewport project |

## Teardown
```bash
docker compose down -v          # -v also drops the Postgres volume
```
