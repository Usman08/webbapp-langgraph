# Contract: Internal Agent-Tool API (.NET ← Python AI engine)

Base path `/internal/tools`. **Not exposed to the browser** — reachable only on the Docker
internal network, authenticated with a shared `X-Engine-Token` header (env var, server-side
only; Principle II). These endpoints encapsulate **all business logic**; the LangGraph nodes
are thin callers (keeps the .NET monolith the single source of business truth).

Each tool is also the LangGraph tool schema the LLM binds to.

---

## POST /internal/tools/resolve-customer

Identify a customer from free text (FR-002, FR-018).
```jsonc
// in
{ "nameHint": "ABC Traders" }
// out — unique
{ "status": "resolved", "customer": { "id": "uuid", "name": "ABC Traders", "type": "Wholesale", "discountTier": "wholesale-bulk" } }
// out — ambiguous
{ "status": "ambiguous", "candidates": [ { "id": "uuid", "name": "ABC Traders Ltd" }, { "id": "uuid", "name": "ABC Trading Co" } ] }
// out — none
{ "status": "not_found" }
```

## POST /internal/tools/get-purchase-history

Retrieve prior invoices + co-purchase stats (FR-003, FR-009).
```jsonc
// in
{ "customerId": "uuid", "lookback": "last_month" }
// out
{ "mostRecentInvoice": { "invoiceId": "uuid", "date": "...", "lines": [ { "productId": "uuid", "sku": "SKU-1", "quantity": 20 } ] },
  "coPurchasePatterns": [ { "withSku": "SKU-1", "recommendSku": "SKU-9", "support": "4/6 invoices" } ] }
```

## POST /internal/tools/adjust-quantities

Apply a percentage delta with half-up rounding (FR-004, FR-017).
```jsonc
// in
{ "lines": [ { "productId": "uuid", "quantity": 20 } ], "deltaPercent": 20 }
// out
{ "lines": [ { "productId": "uuid", "quantity": 24 } ] }
```

## POST /internal/tools/validate-inventory

Check stock; suggest alternatives or flag back-order (FR-006, FR-007, FR-020).
```jsonc
// in
{ "lines": [ { "productId": "uuid", "quantity": 24 } ] }
// out
{ "lines": [
   { "productId": "uuid", "quantity": 24, "stockStatus": "InStock" },
   { "productId": "uuid2", "quantity": 5, "stockStatus": "AlternativeSuggested",
     "alternative": { "productId": "uuid3", "sku": "SKU-ALT" } },
   { "productId": "uuid4", "quantity": 3, "stockStatus": "BackOrder" }   // no alt in stock
] }
```

## POST /internal/tools/resolve-discount

Resolve the customer's usual discount (FR-005; missing rule handled).
```jsonc
// in  { "customerId": "uuid" }
// out { "status": "resolved", "percentage": 10.0, "ruleKey": "wholesale-bulk" }
//  or { "status": "no_rule" }
```

## POST /internal/tools/recommend-products

Rank complementary products from history (FR-009).
```jsonc
// in  { "customerId": "uuid", "draftProductIds": ["uuid"] }
// out { "recommendations": [ { "productId": "uuid9", "sku": "SKU-9", "basis": "co-purchased with SKU-1 in 4/6 invoices" } ] }
```

## POST /internal/tools/build-draft

Persist a Draft invoice + line items; compute totals (FR-008, FR-010).
```jsonc
// in
{ "workflowRunId": "uuid", "customerId": "uuid",
  "lines": [ { "productId": "uuid", "quantity": 24, "stockStatus": "InStock" } ],
  "discountPercentage": 10.0 }
// out
{ "invoiceId": "uuid", "subtotal": 240.00, "discountAmount": 24.00,
  "taxAmount": 32.40, "total": 248.40, "status": "Draft" }
```

## POST /internal/tools/record-step

Persist a WorkflowStep as it occurs (FR-013, FR-014). Called by the engine per node.
```jsonc
// in
{ "workflowRunId": "uuid", "sequence": 3, "name": "inventory_validation",
  "toolInvoked": "validate-inventory", "input": { }, "output": { }, "isException": false }
// out { "stepId": "uuid" }
```

---

### Error semantics
All tools return `400` problem+json on invalid input and `502` if a downstream dependency
fails; the engine surfaces these as `exception` workflow events (FR + US2 scenario 3).
