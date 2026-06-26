"""LangGraph nodes for the US1/US2 workflow: NL → Draft invoice with SSE streaming."""
import json
import re
from app.graph.state import WorkflowState
from app.graph.llm import get_llm
from app.tools import dotnet_tools
from app.streaming import events as ev


async def _emit(state: WorkflowState, event: ev.SSEEvent) -> None:
    """Put an SSE event on the per-run queue (no-op if no queue registered)."""
    run_id = state.get("run_id", "")
    q = ev.get_queue(run_id)
    if q:
        await q.put(event)


async def _record(
    state: WorkflowState,
    seq: int,
    name: str,
    tool: str | None,
    inp: dict,
    out: dict,
    is_exception: bool = False,
) -> None:
    """Persist a WorkflowStep and emit corresponding SSE tool_result/exception event."""
    run_id = state.get("run_id", "")
    if run_id:
        await dotnet_tools.record_step(run_id, seq, name, tool, inp, out, is_exception)

    if tool:
        event = (
            ev.exception_event(seq, name, str(out.get("detail", "")), str(out.get("resolution", "")))
            if is_exception
            else ev.tool_result(seq, tool, out)
        )
        await _emit(state, event)


# ── Node 1: Parse intent from NL request ──────────────────────────────────────

async def parse_intent(state: WorkflowState) -> WorkflowState:
    """Use the LLM to extract structured intent from the NL request."""
    run_id = state.get("run_id", "")
    request_text = state.get("request_text", "")

    await _emit(state, ev.run_started(run_id, request_text))
    await _emit(state, ev.node_started(1, "intent_parse"))

    llm = get_llm()

    prompt = f"""Extract structured intent from this sales request. Return a JSON object with:
- "customer_name": the customer name (string)
- "items": list of objects with "product_hint" (string, product name/SKU hint) and "quantity" (int, 0 if unspecified)
- "quantity_adjustment_percent": percentage to adjust quantities (number, 0 if none, e.g. 20 for +20%)
- "use_previous_order": boolean, true if they want same products as last order
- "apply_usual_discount": boolean

Request: {request_text}

Return only valid JSON, no markdown."""

    response = await llm.ainvoke(prompt)
    content = str(response.content).strip()

    content = re.sub(r"^```(?:json)?", "", content).strip()
    content = re.sub(r"```$", "", content).strip()

    try:
        parsed = json.loads(content)
    except json.JSONDecodeError:
        parsed = {
            "customer_name": "",
            "items": [],
            "quantity_adjustment_percent": 0,
            "use_previous_order": False,
            "apply_usual_discount": False,
        }

    await _emit(state, ev.decision(1, f"Parsed intent: customer={parsed.get('customer_name')}, use_previous={parsed.get('use_previous_order')}, adjust={parsed.get('quantity_adjustment_percent')}%"))
    await _record(state, 1, "intent_parse", None, {"requestText": request_text}, parsed)
    return {**state, **parsed}


# ── Node 2: Resolve customer ───────────────────────────────────────────────────

async def resolve_customer(state: WorkflowState) -> WorkflowState:
    customer_name = state.get("customer_name", "")
    await _emit(state, ev.node_started(2, "customer_lookup"))
    await _emit(state, ev.tool_invoked(2, "resolve-customer", {"nameHint": customer_name}))

    result = await dotnet_tools.resolve_customer(customer_name)
    await _record(state, 2, "customer_lookup", "resolve-customer",
                  {"nameHint": customer_name}, result)

    if result.get("status") == "resolved":
        c = result["customer"]
        await _emit(state, ev.decision(2, f"Customer resolved: {c['name']}"))
        return {**state, "customer_id": c["id"], "customer_name": c["name"]}
    elif result.get("status") == "ambiguous":
        await _emit(state, ev.needs_input("customer", result.get("candidates", [])))
        return {**state, "disambiguation_options": result.get("candidates", []),
                "error": "customer_ambiguous"}
    else:
        await _emit(state, ev.parse_error("No customer found in request",
                                           "Try: 'Create an invoice for <customer name> ...'"))
        return {**state, "error": "customer_not_found"}


# ── Node 3: Get purchase history ───────────────────────────────────────────────

async def get_history(state: WorkflowState) -> WorkflowState:
    customer_id = state.get("customer_id", "")
    await _emit(state, ev.node_started(3, "purchase_history"))
    await _emit(state, ev.tool_invoked(3, "get-purchase-history", {"customerId": customer_id}))

    result = await dotnet_tools.get_purchase_history(customer_id)
    await _record(state, 3, "purchase_history", "get-purchase-history",
                  {"customerId": customer_id}, result)

    items_to_use = state.get("requested_items", [])

    use_prev = state.get("use_previous_order", False)
    if use_prev and result.get("mostRecentInvoice"):
        prev_lines = result["mostRecentInvoice"].get("lines", [])
        items_to_use = [{"productId": l["productId"], "quantity": l["quantity"]} for l in prev_lines]
        await _emit(state, ev.decision(3, f"Using {len(items_to_use)} lines from previous order"))

    return {**state, "purchase_history": result, "requested_items": items_to_use}


# ── Node 4: Adjust quantities ──────────────────────────────────────────────────

async def adjust_quantities(state: WorkflowState) -> WorkflowState:
    items = state.get("requested_items", [])
    delta = state.get("quantity_adjustment_percent", 0)

    await _emit(state, ev.node_started(4, "quantity_adjustment"))

    if not items or delta == 0:
        await _record(state, 4, "quantity_adjustment", None,
                      {"delta": delta}, {"lines": items, "skipped": True})
        return {**state, "adjusted_items": items}

    await _emit(state, ev.tool_invoked(4, "adjust-quantities", {"lines": items, "deltaPercent": delta}))
    result = await dotnet_tools.adjust_quantities(items, float(delta))
    await _record(state, 4, "quantity_adjustment", "adjust-quantities",
                  {"lines": items, "deltaPercent": delta}, result)
    await _emit(state, ev.decision(4, f"Quantities adjusted by {delta}%"))
    return {**state, "adjusted_items": result.get("lines", items)}


# ── Node 5: Validate inventory ─────────────────────────────────────────────────

async def validate_inventory(state: WorkflowState) -> WorkflowState:
    items = state.get("adjusted_items") or state.get("requested_items", [])
    await _emit(state, ev.node_started(5, "inventory_validation"))
    await _emit(state, ev.tool_invoked(5, "validate-inventory", {"lines": items}))

    result = await dotnet_tools.validate_inventory(items)
    lines = result.get("lines", [])

    out_of_stock = [l for l in lines if l.get("stockStatus") != "InStock"]
    if out_of_stock:
        for line in out_of_stock:
            status = line.get("stockStatus", "")
            sku = line.get("sku", line.get("productId", "?"))
            alt = line.get("alternative", {})
            if status == "AlternativeSuggested":
                await _emit(state, ev.exception_event(
                    5, "inventory_validation",
                    f"{sku} out of stock",
                    f"Alternative {alt.get('sku', '?')} suggested"
                ))
            else:
                await _emit(state, ev.exception_event(
                    5, "inventory_validation",
                    f"{sku} out of stock",
                    "Back-order — no alternative in stock"
                ))

    await _record(state, 5, "inventory_validation", "validate-inventory",
                  {"lines": items}, result)
    return {**state, "inventory_result": lines}


# ── Node 6: Resolve discount ───────────────────────────────────────────────────

async def resolve_discount(state: WorkflowState) -> WorkflowState:
    customer_id = state.get("customer_id", "")
    apply = state.get("apply_usual_discount", True)

    await _emit(state, ev.node_started(6, "discount_resolution"))

    if not apply:
        await _record(state, 6, "discount_resolution", None,
                      {}, {"status": "skipped"})
        return {**state, "discount": {"status": "no_rule"}}

    await _emit(state, ev.tool_invoked(6, "resolve-discount", {"customerId": customer_id}))
    result = await dotnet_tools.resolve_discount(customer_id)
    await _record(state, 6, "discount_resolution", "resolve-discount",
                  {"customerId": customer_id}, result)

    if result.get("status") == "resolved":
        await _emit(state, ev.decision(6, f"Discount resolved: {result.get('percentage')}% ({result.get('ruleKey')})"))
    return {**state, "discount": result}


# ── Node 7: Build draft invoice ────────────────────────────────────────────────

async def build_draft(state: WorkflowState) -> WorkflowState:
    run_id = state.get("run_id", "")
    customer_id = state.get("customer_id", "")
    inv_lines = state.get("inventory_result", [])
    discount = state.get("discount") or {}

    discount_pct = discount.get("percentage", 0) if discount.get("status") == "resolved" else 0

    draft_lines = [
        {
            "productId": l["productId"],
            "quantity": l["quantity"],
            "stockStatus": l["stockStatus"],
        }
        for l in inv_lines
    ]

    await _emit(state, ev.node_started(7, "build_draft"))
    await _emit(state, ev.tool_invoked(7, "build-draft", {
        "workflowRunId": run_id, "lines": draft_lines, "discountPercentage": discount_pct
    }))

    result = await dotnet_tools.build_draft(run_id, customer_id, draft_lines, discount_pct)
    await _record(state, 7, "build_draft", "build-draft",
                  {"workflowRunId": run_id, "lines": draft_lines, "discountPercentage": discount_pct},
                  result)

    invoice_id = result.get("invoiceId", "")
    total = result.get("total", 0.0)
    await _emit(state, ev.draft_ready(invoice_id, total))
    await _emit(state, ev.workflow_complete(run_id))

    return {**state, "draft_invoice": result}
