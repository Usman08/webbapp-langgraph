# Contract: Workflow Event Stream (SSE)

Transport: **Server-Sent Events**. Emitted by the AI engine, relayed verbatim by the .NET API
on `GET /api/invoices/requests/{runId}/stream`. Each SSE message has an `event:` type and a
JSON `data:` payload. Powers US2 (real-time transparency) and SC-002 (≥6 labelled steps).

```
event: <type>
data: { ...json... }

```

## Event types

| `event` | When | `data` shape |
|---------|------|--------------|
| `run_started` | Workflow begins | `{ "runId": "uuid", "requestText": "..." }` |
| `node_started` | A graph node begins | `{ "sequence": 1, "name": "customer_lookup" }` |
| `tool_invoked` | Node calls an agent tool | `{ "sequence": 1, "tool": "resolve-customer", "input": { } }` |
| `tool_result` | Tool returns | `{ "sequence": 1, "tool": "resolve-customer", "output": { } }` |
| `decision` | AI makes a reasoning decision | `{ "sequence": 2, "summary": "Increasing all quantities by 20%" }` |
| `needs_input` | Disambiguation required (FR-018) | `{ "kind": "customer", "candidates": [ ... ] }` |
| `recommendation` | Product recommended (FR-009) | `{ "recommendationId": "uuid", "sku": "SKU-9", "basis": "..." }` |
| `exception` | Handled error/out-of-stock (US2 #3) | `{ "sequence": 4, "name": "inventory_validation", "detail": "SKU-2 out of stock", "resolution": "alternative SKU-ALT suggested" }` |
| `draft_ready` | Draft assembled, awaiting approval | `{ "invoiceId": "uuid", "total": 248.40 }` |
| `parse_error` | NL request not understood (FR-019) | `{ "message": "No customer found in request", "suggestion": "Try: 'Create an invoice for <customer> ...'" }` |
| `workflow_complete` | Run finished (draft ready) | `{ "runId": "uuid", "status": "AwaitingApproval" }` |
| `workflow_failed` | Unrecoverable failure | `{ "runId": "uuid", "reason": "..." }` |

## Ordering & guarantees

- Events for a run are strictly ordered by `sequence` for node-scoped events.
- Every `tool_invoked` is followed by exactly one `tool_result` or one `exception`.
- The stream always terminates with `workflow_complete` **or** `workflow_failed`.
- Each emitted node/tool event corresponds 1:1 to a persisted `WorkflowStep` (FR-014), so a
  page reload can reconstruct identical history via `GET /api/invoices/requests/{runId}`.

## Client behaviour (React)

- Subscribe via `EventSource(streamUrl)`; render steps into the WorkflowProgress panel as they
  arrive; append to ExecutionLog.
- On `needs_input` → show disambiguation UI; POST to `/disambiguate`; stream resumes.
- On `draft_ready` / `workflow_complete` → load `GET /api/invoices/{invoiceId}` into the preview
  and reveal the Approve / Reject-Edit gate.
- On `parse_error` → show inline actionable error; keep request text editable (FR-019).
- Respect `prefers-reduced-motion` for step-appearance animations.
