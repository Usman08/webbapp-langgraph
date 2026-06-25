# Feature Specification: AI-Native Sales Invoice PoC

**Feature Branch**: `001-ai-sales-invoice-poc`

**Created**: 2026-06-25

**Status**: Draft

**Input**: User description: "Build an AI-native Sales Invoice Proof of Concept that demonstrates how artificial intelligence can actively participate in a business workflow..."

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Natural Language Invoice Request (Priority: P1)

A sales operator types a free-form request such as:
*"Create an invoice for ABC Traders. Same products as last month. Increase quantities by 20% and apply the usual discount."*
The system interprets the intent, identifies the customer, retrieves the prior invoice, adjusts line-item quantities, applies the customer's standard discount tier, validates stock, and presents a fully-formed invoice draft — all without the operator manually filling a form.

**Why this priority**: This is the core differentiator of the PoC. Every other story depends on this capability being demonstrably end-to-end.

**Independent Test**: Provide the example request above; verify the system produces an invoice draft with correct customer, adjusted quantities, applied discount, and no manual data entry.

**Acceptance Scenarios**:

1. **Given** a registered customer "ABC Traders" with a prior invoice, **When** the operator submits the natural language request, **Then** the system identifies the customer, retrieves last month's invoice lines, increases each quantity by 20 %, applies the configured discount, and presents a draft invoice within 30 seconds.
2. **Given** a product on the draft has zero stock, **When** the AI validates inventory, **Then** it flags the line as unavailable, suggests an alternative product, and marks the exception in the workflow log.
3. **Given** the AI has produced a draft, **When** the operator clicks "Approve", **Then** the invoice is finalised and added to invoice history.

---

### User Story 2 — Workflow Transparency & Execution Log (Priority: P2)

The operator can watch each step the AI takes in real time: customer lookup, history retrieval, quantity adjustment, product recommendation, inventory check, pricing calculation, and draft generation. Each step is labelled with the tool invoked, the input, and the output.

**Why this priority**: Demonstrating *how* the AI reasons is the primary PoC objective — the workflow log is what makes this an "AI-native" showcase rather than a black-box integration.

**Independent Test**: Submit any valid request and confirm the UI renders at least 6 discrete workflow steps with tool names, inputs, and outputs visible before the invoice draft appears.

**Acceptance Scenarios**:

1. **Given** a request is submitted, **When** the AI workflow executes, **Then** each agent node (customer lookup, history analysis, quantity adjustment, inventory validation, pricing, draft generation) appears in the progress panel in order.
2. **Given** the workflow is complete, **When** the operator reviews the execution history, **Then** every tool call is listed with its invocation parameters and result.
3. **Given** an exception occurs (e.g. out-of-stock), **When** it is handled, **Then** the exception, the AI's reasoning, and the chosen resolution appear in the log.

---

### User Story 3 — Product Recommendation Based on Buying Patterns (Priority: P3)

After retrieving the customer's purchasing history, the AI recommends complementary or frequently co-purchased products not present in the current draft and asks the operator whether to include them.

**Why this priority**: Demonstrates AI-driven upsell reasoning — a high-value business use-case extension beyond simple repetition.

**Independent Test**: Use a seeded customer with a known buying pattern; confirm the recommendation panel lists at least one relevant product with a justification message.

**Acceptance Scenarios**:

1. **Given** a customer regularly buys Product A alongside Product B, **When** the draft contains Product A but not B, **Then** the AI recommends Product B and explains why.
2. **Given** the operator accepts a recommendation, **When** the invoice is re-calculated, **Then** the recommended product appears as a new line item with correct price and quantity.
3. **Given** the operator declines all recommendations, **When** they proceed, **Then** the draft continues without the recommended items.

---

### User Story 4 — Human-in-the-Loop Approval Gate (Priority: P4)

Before the invoice is finalised, the operator must explicitly review and approve (or reject and edit) the AI-generated draft. The approval action is irreversible for finalisation; rejection returns to an editable draft state.

**Why this priority**: Human oversight of AI-generated financial documents is a non-negotiable governance requirement and a key PoC talking point.

**Independent Test**: Submit a request, receive a draft, click "Reject", confirm the draft re-enters editable state; then approve and confirm the invoice appears in history.

**Acceptance Scenarios**:

1. **Given** a draft is ready, **When** the operator clicks "Approve", **Then** the invoice status changes to "Finalised" and the record is persisted.
2. **Given** a draft is ready, **When** the operator clicks "Reject / Edit", **Then** all line items become editable and the AI reasoning panel remains visible for reference.
3. **Given** an edited draft is re-submitted for approval, **When** approved, **Then** the finalised invoice reflects the operator's edits, not the original AI output.

---

### Edge Cases

- What happens when the customer name in the request is ambiguous (partial match or multiple customers)?
- When the AI cannot extract any meaningful intent from the request, the system MUST display an inline error message identifying the failure (e.g., "No customer found") and suggest a corrected phrasing example; the original input MUST remain editable so the operator can revise and resubmit without retyping from scratch.
- How does the system respond when there is no prior invoice for the customer (first-time order)?
- What if the requested quantity increase results in a fractional unit count (e.g. 3 × 1.2 = 3.6 units)?
- What happens when the AI cannot determine a "usual discount" because no discount rule exists for the customer?
- What if all alternative products for an out-of-stock item are also out of stock? → The line item is retained in the draft with a "Back-Order / No Stock" flag; the operator decides at the approval gate whether to remove it or proceed with the back-order.

---

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST accept a free-form natural language sales request as the primary input.
- **FR-002**: The system MUST identify the referenced customer from the request text and retrieve their profile.
- **FR-003**: The system MUST retrieve the customer's most recent prior invoice when the request references previous orders.
- **FR-004**: The system MUST adjust line-item quantities by the percentage specified in the natural language request.
- **FR-005**: The system MUST apply the customer's configured discount tier when the request references "usual discount" or equivalent phrasing.
- **FR-006**: The system MUST validate current inventory availability for every product on the draft.
- **FR-007**: When a product is out of stock, the system MUST suggest at least one alternative product and present it to the operator.
- **FR-008**: The system MUST calculate final line totals and invoice subtotal, tax, and total according to seeded business rules.
- **FR-009**: The system MUST recommend complementary products based on the customer's historical purchasing patterns.
- **FR-010**: The system MUST present a structured invoice draft before finalisation.
- **FR-011**: The system MUST require explicit operator approval before an invoice is finalised.
- **FR-012**: The operator MUST be able to reject or edit the AI-generated draft and resubmit for approval.
- **FR-013**: The system MUST display each workflow step (tool invocation, input, output, decisions) in real time as the AI processes the request.
- **FR-013a**: The system MUST associate all Workflow Steps from a request with the resulting Invoice upon finalisation, so the full AI reasoning trail is accessible from the invoice history view.
- **FR-014**: The system MUST persist Workflow Step records to backend storage so the execution history log survives page refreshes, browser clears, and new sessions on any device.
- **FR-015**: The system MUST be seeded with sample customers (Retail, Wholesale, VIP), products, inventory levels, pricing rules, discount schedules, and historical invoices.
- **FR-016**: The seeded data MUST include at least two out-of-stock products to demonstrate exception handling.
- **FR-017**: Fractional unit quantities resulting from percentage adjustments MUST be rounded to the nearest whole unit.
- **FR-018**: When the customer cannot be uniquely identified, the system MUST present a disambiguation list and wait for operator selection before continuing.
- **FR-019**: When the AI cannot extract any meaningful intent from the request, the system MUST display an inline, actionable error message identifying what could not be understood and provide a corrected phrasing example; the original input MUST remain editable for revision and resubmission.
- **FR-020**: When an out-of-stock product has no available alternatives, the system MUST retain the line item in the draft marked as "Back-Order / No Stock" rather than removing it; the operator MUST be able to remove or retain the flagged line at the approval gate.
- **FR-021**: All entities (customers, products, inventory, invoices, invoice line items, discount rules, workflow steps, product recommendations) MUST be persisted in PostgreSQL. Seed data MUST be loaded into PostgreSQL at application startup via a deterministic seeding mechanism that is safe to re-run.

### Key Entities

- **Customer**: Name, type (Retail / Wholesale / VIP), discount tier, contact details, purchase history reference.
- **Product**: SKU, name, category, unit price, inventory quantity, alternative product references.
- **Invoice**: ID, customer reference, date, line items, subtotal, discount applied, tax, total, status (Draft | Finalised). Operator rejection reverts status to Draft; there is no intermediate status.
- **Invoice Line Item**: Product reference, quantity, unit price, line total.
- **Discount Rule**: Customer type or customer ID, discount percentage, validity conditions.
- **Workflow Step**: Step name, tool invoked, input payload, output/result, timestamp, exception flag, invoice reference (populated once the invoice is finalised; null while draft).
- **Product Recommendation**: Recommended product, basis (co-purchase pattern), acceptance status.

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A valid natural language invoice request produces a complete draft invoice within 30 seconds of submission.
- **SC-002**: The workflow log displays a minimum of 6 discrete, labelled AI steps for every completed request.
- **SC-003**: Out-of-stock exceptions are detected and an alternative product is presented 100 % of the time a stock-unavailable product is encountered.
- **SC-004**: The operator can complete the full journey — submit request → review AI steps → approve invoice — in under 2 minutes.
- **SC-005**: At least one product recommendation is surfaced for customers with two or more prior purchases containing co-purchased products.
- **SC-006**: Discount and pricing calculations match the seeded business rules with zero rounding errors greater than 0.01 currency units.
- **SC-007**: The human approval gate prevents invoice finalisation in 100 % of cases where the operator has not explicitly approved the draft.
- **SC-008**: The execution history for any past request remains fully accessible and scrollable across sessions and devices without data loss.

---

## Clarifications

### Session 2026-06-25

- Q: How long must Workflow Step records survive? → A: Backend-persisted — survives across sessions, devices, and browser clears.
- Q: Are Workflow Steps linked to the resulting Invoice or only to the request session? → A: Linked to Invoice — each finalised invoice retains a reference to its Workflow Steps.
- Q: What should the system do when the AI fails to parse the NL request entirely? → A: Show an actionable inline error explaining what failed and offer a corrected phrasing example; do not halt or clear the input.
- Q: When an out-of-stock product has no available alternatives, what happens to that line item? → A: Keep with back-order flag — mark the line as "Back-Order / No Stock" and let the operator decide at approval.
- Q: After an operator rejects the AI draft and edits it, what is the invoice's status label? → A: Revert to Draft — edit is a UI mode, not a distinct lifecycle state; status model remains Draft → Finalised only.

### Session 2026-06-25 (constraint)

- Constraint: PostgreSQL MUST be used as the persistent data store for all entities (customers, products, invoices, workflow steps, etc.). Seed data is loaded into PostgreSQL at startup, not held in memory.

---

## Assumptions

- The PoC targets a single-user desktop browser session; multi-user concurrency and authentication are out of scope.
- **Technical constraint (user-mandated)**: PostgreSQL MUST be used as the persistent data store for all entities — customers, products, inventory, invoices, invoice line items, discount rules, workflow steps, and product recommendations. Seed data is loaded into PostgreSQL at application startup; no in-memory-only or flat-file storage is permitted for any entity. No external ERP integration is required.
- Tax rate is a single fixed percentage applied uniformly to all invoices (seeded value; no multi-jurisdiction tax logic needed).
- "Usual discount" in the request maps to the customer's pre-configured discount tier; no dynamic negotiation logic is required.
- Quantity rounding follows standard half-up rules (3.5 → 4, 3.4 → 3).
- The AI workflow engine is LangGraph; the orchestration model is the project's chosen LLM (seeded with tool definitions matching the entities above).
- Product recommendations are derived from co-purchase frequency in historical invoices, not from an external recommendation engine.
- The UI is a single-page application; navigation between the request panel, workflow log, and invoice preview occurs within one screen.
- Mobile viewport support is required per the project constitution (Principle I — Mobile First); all UI panels must be usable on a 375 px wide screen.
- Security controls (input sanitisation, no secrets in client bundles) apply per the project constitution (Principle II — Security First), even in PoC scope.
