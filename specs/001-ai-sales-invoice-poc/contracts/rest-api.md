# Contract: Public REST API (.NET → React)

Base path `/api`. All responses JSON unless noted. Errors use RFC 7807
`application/problem+json`. This is the **only** surface the browser talks to.

---

## POST /api/invoices/requests

Submit a natural-language sales request; starts an AI workflow run.

**Request**
```json
{ "requestText": "Create an invoice for ABC Traders. Same products as last month. Increase quantities by 20% and apply the usual discount." }
```
**Validation**: `requestText` required, 1–2000 chars, sanitised server-side (Principle II).

**201 Created**
```json
{ "runId": "uuid", "status": "Running", "streamUrl": "/api/invoices/requests/{runId}/stream" }
```
**400** problem+json on empty/oversized input.

---

## GET /api/invoices/requests/{runId}/stream

Server-Sent Events stream of workflow progress. See [workflow-events.md](./workflow-events.md).
`Content-Type: text/event-stream`. Closes on `workflow_complete` / `workflow_failed`.

---

## GET /api/invoices/requests/{runId}

Poll run state + current draft (fallback for SSE / page reload).

**200**
```json
{
  "runId": "uuid",
  "status": "AwaitingApproval",
  "customer": { "id": "uuid", "name": "ABC Traders", "type": "Wholesale" },
  "draftInvoiceId": "uuid|null",
  "steps": [ /* WorkflowStep[] */ ],
  "recommendations": [ /* ProductRecommendation[] */ ]
}
```

---

## POST /api/invoices/requests/{runId}/disambiguate

Resolve an ambiguous customer (FR-018).
```json
{ "customerId": "uuid" }   // → 200, workflow resumes
```

---

## POST /api/invoices/requests/{runId}/recommendations/{recommendationId}

Accept/decline a product recommendation (FR-009).
```json
{ "accepted": true }   // → 200 with recalculated draft
```

---

## GET /api/invoices/{invoiceId}

Full draft/finalised invoice for the preview screen.

**200**
```json
{
  "id": "uuid", "status": "Draft",
  "customer": { "id": "uuid", "name": "ABC Traders", "type": "Wholesale" },
  "lineItems": [
    { "productId": "uuid", "sku": "SKU-1", "name": "Widget",
      "quantity": 24, "unitPrice": 10.00, "lineTotal": 240.00,
      "stockStatus": "InStock" }
  ],
  "subtotal": 240.00, "discountPercentage": 10.0, "discountAmount": 24.00,
  "taxPercentage": 15.0, "taxAmount": 32.40, "total": 248.40,
  "workflowRunId": "uuid"
}
```

---

## PUT /api/invoices/{invoiceId}/lines

Operator edits a draft (reject/edit flow, FR-012). Body = full line-item array; server
recalculates totals. Allowed only while `status = Draft`. **409** if `Finalised`.

---

## POST /api/invoices/{invoiceId}/approve

Human-in-the-loop finalisation (FR-011, SC-007).

**200** → `{ "id": "uuid", "status": "Finalised", "finalisedAt": "..." }`
**409** problem+json if already Finalised.

---

## POST /api/invoices/{invoiceId}/reject

Revert AI draft to editable Draft (FR-012). Status stays `Draft`; UI unlocks line editing.
**200** → current draft.

---

## GET /api/invoices

Invoice history list (id, customer, date, total, status). Supports `?status=` filter.

---

## GET /api/invoices/{invoiceId}/workflow

The full reasoning trail for a finalised invoice (FR-013a) — ordered `WorkflowStep[]`.

---

## Reference data (UI helpers)

- `GET /api/customers` — list (for disambiguation UI).
- `GET /api/products` — catalogue incl. inventory + alternatives.
