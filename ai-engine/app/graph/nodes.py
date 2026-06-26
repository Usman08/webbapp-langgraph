"""LangGraph nodes for the US1 workflow: NL → Draft invoice."""
import json
import re
from app.graph.state import WorkflowState
from app.graph.llm import get_llm
from app.tools import dotnet_tools


async def _record(state: WorkflowState, seq: int, name: str, tool: str | None, inp: dict, out: dict) -> None:
    """Persist a workflow step."""
    run_id = state.get("run_id", "")
    if run_id:
        await dotnet_tools.record_step(run_id, seq, name, tool, inp, out)


# ── Node 1: Parse intent from NL request ──────────────────────────────────────

async def parse_intent(state: WorkflowState) -> WorkflowState:
    """Use the LLM to extract structured intent from the NL request."""
    llm = get_llm()
    request_text = state.get("request_text", "")

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

    # Strip markdown code fences if present
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

    await _record(state, 1, "intent_parse", None, {"requestText": request_text}, parsed)
    return {**state, **parsed}


# ── Node 2: Resolve customer ───────────────────────────────────────────────────

async def resolve_customer(state: WorkflowState) -> WorkflowState:
    customer_name = state.get("customer_name", "")
    result = await dotnet_tools.resolve_customer(customer_name)
    await _record(state, 2, "customer_lookup", "resolve-customer",
                  {"nameHint": customer_name}, result)

    if result.get("status") == "resolved":
        c = result["customer"]
        return {**state, "customer_id": c["id"], "customer_name": c["name"]}
    elif result.get("status") == "ambiguous":
        return {**state, "disambiguation_options": result.get("candidates", []),
                "error": "customer_ambiguous"}
    else:
        return {**state, "error": "customer_not_found"}


# ── Node 3: Get purchase history ───────────────────────────────────────────────

async def get_history(state: WorkflowState) -> WorkflowState:
    customer_id = state.get("customer_id", "")
    result = await dotnet_tools.get_purchase_history(customer_id)
    await _record(state, 3, "purchase_history", "get-purchase-history",
                  {"customerId": customer_id}, result)

    items_to_use = state.get("requested_items", [])

    # If user wants same products as last order, use previous invoice lines
    use_prev = state.get("use_previous_order", False)
    if use_prev and result.get("mostRecentInvoice"):
        prev_lines = result["mostRecentInvoice"].get("lines", [])
        items_to_use = [{"productId": l["productId"], "quantity": l["quantity"]} for l in prev_lines]

    return {**state, "purchase_history": result, "requested_items": items_to_use}


# ── Node 4: Adjust quantities ──────────────────────────────────────────────────

async def adjust_quantities(state: WorkflowState) -> WorkflowState:
    items = state.get("requested_items", [])
    delta = state.get("quantity_adjustment_percent", 0)

    if not items or delta == 0:
        await _record(state, 4, "quantity_adjustment", None,
                      {"delta": delta}, {"lines": items, "skipped": True})
        return {**state, "adjusted_items": items}

    result = await dotnet_tools.adjust_quantities(items, float(delta))
    await _record(state, 4, "quantity_adjustment", "adjust-quantities",
                  {"lines": items, "deltaPercent": delta}, result)
    return {**state, "adjusted_items": result.get("lines", items)}


# ── Node 5: Validate inventory ─────────────────────────────────────────────────

async def validate_inventory(state: WorkflowState) -> WorkflowState:
    items = state.get("adjusted_items") or state.get("requested_items", [])
    result = await dotnet_tools.validate_inventory(items)
    await _record(state, 5, "inventory_validation", "validate-inventory",
                  {"lines": items}, result)
    return {**state, "inventory_result": result.get("lines", [])}


# ── Node 6: Resolve discount ───────────────────────────────────────────────────

async def resolve_discount(state: WorkflowState) -> WorkflowState:
    customer_id = state.get("customer_id", "")
    apply = state.get("apply_usual_discount", True)

    if not apply:
        await _record(state, 6, "discount_resolution", None,
                      {}, {"status": "skipped"})
        return {**state, "discount": {"status": "no_rule"}}

    result = await dotnet_tools.resolve_discount(customer_id)
    await _record(state, 6, "discount_resolution", "resolve-discount",
                  {"customerId": customer_id}, result)
    return {**state, "discount": result}


# ── Node 7: Build draft invoice ────────────────────────────────────────────────

async def build_draft(state: WorkflowState) -> WorkflowState:
    run_id = state.get("run_id", "")
    customer_id = state.get("customer_id", "")
    inv_lines = state.get("inventory_result", [])
    discount = state.get("discount") or {}

    discount_pct = discount.get("percentage", 0) if discount.get("status") == "resolved" else 0

    # Build the line list for the draft — map inventory result to draft format
    draft_lines = [
        {
            "productId": l["productId"],
            "quantity": l["quantity"],
            "stockStatus": l["stockStatus"],
        }
        for l in inv_lines
    ]

    result = await dotnet_tools.build_draft(run_id, customer_id, draft_lines, discount_pct)
    await _record(state, 7, "build_draft", "build-draft",
                  {"workflowRunId": run_id, "lines": draft_lines, "discountPercentage": discount_pct},
                  result)
    return {**state, "draft_invoice": result}
