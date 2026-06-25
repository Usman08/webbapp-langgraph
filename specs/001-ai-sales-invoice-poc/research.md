# Phase 0 Research: AI-Native Sales Invoice PoC

All Technical Context unknowns are resolved below. Each entry records the **Decision**,
**Rationale**, and **Alternatives considered**.

---

## R1. AI workflow engine integration (LangGraph ↔ .NET)

**Decision**: Run LangGraph in a dedicated **Python 3.12 FastAPI service** (`ai-engine`).
Its graph nodes invoke **internal "agent-tool" HTTP endpoints** on the .NET API for every
business action (customer lookup, history retrieval, inventory check, pricing, discount,
draft assembly). The Python service holds **no business rules** — it is a stateless reasoning
orchestrator.

**Rationale**:
- LangGraph is the user-mandated engine and is Python/JS-only; the reasoning loop must be Python.
- Keeping business logic in .NET preserves the constitution's monolithic N-layer intent
  (single source of business truth) despite the polyglot runtime.
- Tool-calls-over-HTTP make each AI decision an auditable, testable boundary — ideal for the
  PoC's transparency goal.

**Alternatives considered**:
- *LangGraph-JS in the React client* — rejected: would expose the LLM key and business calls
  to the browser (violates Principle II).
- *Reimplement orchestration in .NET* — rejected: abandons the mandated engine and its
  checkpointing/streaming primitives.
- *Python.NET / IronPython embedding* — rejected: fragile for async LLM + graph streaming;
  poor isolation.

---

## R2. Real-time workflow step streaming to the UI

**Decision**: **Server-Sent Events (SSE)**. The AI engine emits one event per workflow step
(`node_started`, `tool_invoked`, `tool_result`, `decision`, `exception`, `draft_ready`,
`workflow_complete`). The .NET API exposes `GET /api/invoices/requests/{id}/stream`
(text/event-stream) that **relays** the AI engine's stream to the React `EventSource`.

**Rationale**:
- Workflow streaming is one-directional server→client; SSE is simpler than WebSockets,
  auto-reconnects, and works over plain HTTP/1.1.
- Relaying through .NET keeps the AI engine private (not browser-exposed) — Principle II.
- `LangGraph .astream(stream_mode="updates")` maps cleanly to per-node SSE events.

**Alternatives considered**:
- *WebSockets* — rejected: bidirectional complexity unnecessary for a one-way log.
- *Polling* — rejected: laggy, fails the "watch each step in real time" UX goal (US2).

---

## R3. Workflow state & persistence

**Decision**: LangGraph in-memory state during a run + **PostgreSQL as the durable store** for
`WorkflowStep` records (written by the .NET API as steps stream in). Steps are linked to a
`WorkflowRun`, and the run is linked to the resulting `Invoice` on finalisation (FR-013a, FR-014).
Use LangGraph's `MemorySaver` checkpointer for the single run (no cross-run resume needed in PoC).

**Rationale**:
- Spec mandates backend persistence surviving refresh/device change (FR-014) — Postgres satisfies it.
- The .NET API already owns Postgres; having it persist steps keeps one writer and one schema owner.

**Alternatives considered**:
- *LangGraph Postgres checkpointer as system of record* — rejected: would split schema ownership
  across two services; PoC needs Postgres owned by the .NET monolith.

---

## R4. .NET data access & PostgreSQL

**Decision**: **Entity Framework Core 8** with the **Npgsql** provider. Code-first migrations.
A hosted `DbSeeder` runs at startup, **idempotent** (upsert by natural keys), satisfying FR-021/FR-015.

**Rationale**:
- EF Core gives parameterised queries by default (Principle II) and clean N-layer separation
  (Infrastructure owns persistence; Domain stays framework-agnostic).
- Idempotent seeding makes `docker compose up` repeatable for demos.

**Alternatives considered**:
- *Dapper* — rejected: more manual mapping; EF migrations better for a seeded schema.
- *SQL scripts only* — rejected: harder to keep in sync with the domain model.

---

## R5. LLM provider & model

**Decision**: **Claude `claude-sonnet-4-6`** via the Anthropic SDK inside the AI engine, with
tool-use (function calling) bound to the agent-tool endpoints. Model id configurable via env.

**Rationale**:
- Latest, most capable general model for multi-step tool reasoning; strong tool-use reliability.
- Per project guidance, default to the latest capable Claude model for AI applications.
- Key stays server-side in the AI engine (Principle II).

**Alternatives considered**:
- *Smaller/older model* — rejected: tool-calling reliability matters for a demo of reasoning.
- *Local model* — rejected: out of scope for a PoC; adds infra cost without demo value.

---

## R6. Frontend design system

**Decision**: Adopt the **`ui-ux-pro-max`** output persisted at
`design-system/ai-sales-invoice-poc/MASTER.md` (+ `pages/workflow-dashboard.md`):
- **Style**: "Sales Intelligence Dashboard" (professional B2B)
- **Palette**: Primary `#0F172A`, Secondary `#334155`, CTA `#0369A1`, Background `#F8FAFC`,
  Text `#020617` (navy + blue, WCAG AA)
- **Typography**: Plus Jakarta Sans (headings + body)
- **Icons**: Lucide (SVG; no emoji icons)
- **Build**: React 18 + Vite + Tailwind; tokens mapped to Tailwind theme.

**Rationale**:
- Matches a professional sales tool; light-mode default (anti-pattern: dark-by-default) suits
  readability of dense workflow logs and invoices.
- Pre-defined accessible palette accelerates Principle I/III compliance.

**Alternatives considered**:
- *Ad-hoc styling* — rejected: violates Constitution III (shared design-token system required).

---

## R7. Natural-language request parsing strategy

**Decision**: The LLM extracts a **structured intent** (customer name, "same as last month"
reference, quantity delta %, discount directive, explicit products) via tool-use. Ambiguous
customer → call disambiguation tool → emit a `needs_input` event (FR-018). Unparseable →
emit `parse_error` event with a suggested phrasing (FR-019).

**Rationale**:
- Tool-structured extraction is more reliable and auditable than free-text-to-JSON prompting.
- Maps each ambiguity/error directly to a spec requirement and a UI affordance.

**Alternatives considered**:
- *Regex/keyword parsing* — rejected: brittle for free-form language; defeats the AI-native goal.

---

## R8. Local orchestration / runtime

**Decision**: **Docker Compose** with four services: `postgres`, `backend` (.NET), `ai-engine`
(Python), `frontend` (Vite dev / static build). The AI engine is on the internal network only;
the browser talks solely to the .NET API.

**Rationale**:
- One-command demo bring-up; network isolation enforces Principle II (AI engine not public).

**Alternatives considered**:
- *Run each service by hand* — rejected: poor demo ergonomics, error-prone env wiring.

---

## Resolved unknowns summary

| Unknown (from Technical Context) | Resolved by |
|----------------------------------|-------------|
| How .NET integrates LangGraph | R1 |
| Real-time step delivery mechanism | R2 |
| Workflow state persistence | R3 |
| .NET ↔ Postgres approach & seeding | R4 |
| LLM provider/model | R5 |
| Design system specifics | R6 |
| NL parsing approach | R7 |
| Local runtime/orchestration | R8 |

No `NEEDS CLARIFICATION` markers remain.
