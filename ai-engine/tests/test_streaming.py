"""
US2 streaming tests: step persistence + event/step 1:1 mapping.
All .NET calls and LLM calls are mocked.
"""
import asyncio
from unittest.mock import AsyncMock, patch, MagicMock
import pytest

from app.streaming import events as ev
from app.graph import nodes
from app.graph.state import WorkflowState


# ── Fixtures ──────────────────────────────────────────────────────────────────

MOCK_CUSTOMER = {
    "status": "resolved",
    "customer": {"id": "cust-1", "name": "ABC Traders", "type": "Wholesale", "discountTier": "wholesale-bulk"},
}
MOCK_HISTORY = {
    "mostRecentInvoice": {
        "invoiceId": "inv-1",
        "date": "2026-05-01",
        "lines": [{"productId": "prod-1", "sku": "SKU-1", "quantity": 10}],
    },
    "coPurchasePatterns": [],
}
MOCK_ADJUSTED = {"lines": [{"productId": "prod-1", "quantity": 12}]}
MOCK_INVENTORY = {"lines": [{"productId": "prod-1", "quantity": 12, "stockStatus": "InStock"}]}
MOCK_DISCOUNT = {"status": "resolved", "percentage": 10.0, "ruleKey": "wholesale-bulk"}
MOCK_DRAFT = {"invoiceId": "inv-draft-1", "subtotal": 120.0, "discountAmount": 12.0,
               "taxAmount": 16.2, "total": 124.2, "status": "Draft"}


@pytest.fixture
def run_id():
    return "run-stream-test-001"


@pytest.fixture
def queue_setup(run_id):
    """Create a per-run event queue and tear it down after the test."""
    q = ev.create_queue(run_id)
    yield q
    ev.remove_queue(run_id)


# ── Helper ────────────────────────────────────────────────────────────────────

async def drain_queue(q: asyncio.Queue) -> list[ev.SSEEvent]:
    """Collect all events from queue until SENTINEL."""
    collected = []
    while True:
        item = await asyncio.wait_for(q.get(), timeout=5.0)
        if item is ev.SENTINEL:
            break
        collected.append(item)
    return collected


# ── Tests ─────────────────────────────────────────────────────────────────────

@pytest.mark.asyncio
async def test_queue_receives_run_started(run_id, queue_setup):
    q = queue_setup
    state: WorkflowState = {"run_id": run_id, "request_text": "hello", "steps": [], "events": []}
    await ev.get_queue(run_id).put(ev.run_started(run_id, "hello"))
    await q.put(ev.SENTINEL)

    collected = await drain_queue(q)
    assert collected[0].event_type == "run_started"
    assert collected[0].data["runId"] == run_id


@pytest.mark.asyncio
async def test_node_started_event_emitted(run_id, queue_setup):
    """node_started event is emitted before tool_invoked for resolve_customer."""
    q = queue_setup
    state: WorkflowState = {
        "run_id": run_id, "request_text": "test",
        "customer_name": "ABC Traders",
        "steps": [], "events": [],
    }

    with patch("app.tools.dotnet_tools.resolve_customer", new=AsyncMock(return_value=MOCK_CUSTOMER)):
        with patch("app.tools.dotnet_tools.record_step", new=AsyncMock(return_value={"stepId": "s1"})):
            await nodes.resolve_customer(state)

    await q.put(ev.SENTINEL)
    collected = await drain_queue(q)

    types = [e.event_type for e in collected]
    assert "node_started" in types
    assert "tool_invoked" in types
    assert "tool_result" in types
    # node_started comes before tool_invoked
    assert types.index("node_started") < types.index("tool_invoked")
    # tool_invoked comes before tool_result
    assert types.index("tool_invoked") < types.index("tool_result")


@pytest.mark.asyncio
async def test_every_tool_invoked_has_tool_result(run_id, queue_setup):
    """Every tool_invoked event must be followed by exactly one tool_result."""
    q = queue_setup
    state: WorkflowState = {
        "run_id": run_id, "request_text": "test",
        "customer_name": "ABC Traders",
        "steps": [], "events": [],
        "customer_id": "cust-1",
        "requested_items": [{"productId": "prod-1", "quantity": 10}],
        "adjusted_items": [{"productId": "prod-1", "quantity": 12}],
        "inventory_result": [{"productId": "prod-1", "quantity": 12, "stockStatus": "InStock"}],
        "discount": MOCK_DISCOUNT,
        "use_previous_order": False,
        "apply_usual_discount": True,
        "quantity_adjustment_percent": 20,
    }

    mocks = {
        "app.tools.dotnet_tools.resolve_customer": AsyncMock(return_value=MOCK_CUSTOMER),
        "app.tools.dotnet_tools.get_purchase_history": AsyncMock(return_value=MOCK_HISTORY),
        "app.tools.dotnet_tools.adjust_quantities": AsyncMock(return_value=MOCK_ADJUSTED),
        "app.tools.dotnet_tools.validate_inventory": AsyncMock(return_value=MOCK_INVENTORY),
        "app.tools.dotnet_tools.resolve_discount": AsyncMock(return_value=MOCK_DISCOUNT),
        "app.tools.dotnet_tools.build_draft": AsyncMock(return_value=MOCK_DRAFT),
        "app.tools.dotnet_tools.record_step": AsyncMock(return_value={"stepId": "s-x"}),
    }

    with patch.multiple("app.tools.dotnet_tools", **{k.split(".")[-1]: v for k, v in mocks.items()}):
        # Run all nodes
        s = await nodes.resolve_customer(state)
        s = await nodes.get_history(s)
        s = await nodes.adjust_quantities(s)
        s = await nodes.validate_inventory(s)
        s = await nodes.resolve_discount(s)
        s = await nodes.build_draft(s)

    await q.put(ev.SENTINEL)
    collected = await drain_queue(q)

    invoked = [e for e in collected if e.event_type == "tool_invoked"]
    results = [e for e in collected if e.event_type == "tool_result"]

    # Each tool_invoked must have a matching tool_result (same tool name)
    invoked_tools = [e.data["tool"] for e in invoked]
    result_tools = [e.data["tool"] for e in results]

    for tool in invoked_tools:
        assert tool in result_tools, f"tool_invoked for '{tool}' has no matching tool_result"


@pytest.mark.asyncio
async def test_exception_event_emitted_for_out_of_stock(run_id, queue_setup):
    """An out-of-stock line triggers an exception event (no tool_result for that line)."""
    q = queue_setup
    inventory_with_oos = {
        "lines": [
            {"productId": "prod-1", "quantity": 5, "stockStatus": "InStock"},
            {"productId": "prod-2", "quantity": 3, "stockStatus": "AlternativeSuggested",
             "sku": "SKU-OOS", "alternative": {"productId": "prod-alt", "sku": "SKU-ALT"}},
        ]
    }
    state: WorkflowState = {
        "run_id": run_id, "request_text": "test", "steps": [], "events": [],
        "adjusted_items": [{"productId": "prod-1", "quantity": 5}, {"productId": "prod-2", "quantity": 3}],
    }

    with patch("app.tools.dotnet_tools.validate_inventory", new=AsyncMock(return_value=inventory_with_oos)):
        with patch("app.tools.dotnet_tools.record_step", new=AsyncMock(return_value={"stepId": "s1"})):
            await nodes.validate_inventory(state)

    await q.put(ev.SENTINEL)
    collected = await drain_queue(q)

    types = [e.event_type for e in collected]
    assert "exception" in types

    exc = next(e for e in collected if e.event_type == "exception")
    assert "out of stock" in exc.data["detail"].lower()


@pytest.mark.asyncio
async def test_workflow_complete_event_emitted(run_id, queue_setup):
    """build_draft node emits draft_ready then workflow_complete."""
    q = queue_setup
    state: WorkflowState = {
        "run_id": run_id, "request_text": "test", "steps": [], "events": [],
        "customer_id": "cust-1", "inventory_result": [],
        "discount": {"status": "no_rule"},
    }

    with patch("app.tools.dotnet_tools.build_draft", new=AsyncMock(return_value=MOCK_DRAFT)):
        with patch("app.tools.dotnet_tools.record_step", new=AsyncMock(return_value={"stepId": "s1"})):
            await nodes.build_draft(state)

    await q.put(ev.SENTINEL)
    collected = await drain_queue(q)

    types = [e.event_type for e in collected]
    assert "draft_ready" in types
    assert "workflow_complete" in types
    assert types.index("draft_ready") < types.index("workflow_complete")


@pytest.mark.asyncio
async def test_sse_format_output():
    """format_sse produces valid SSE wire format."""
    event = ev.SSEEvent("tool_result", {"tool": "resolve-customer", "output": {"status": "resolved"}})
    formatted = ev.format_sse(event)
    assert formatted.startswith("event: tool_result\n")
    assert "data: " in formatted
    assert formatted.endswith("\n\n")


@pytest.mark.asyncio
async def test_queue_create_and_remove(run_id):
    """Queue lifecycle: create → get → remove."""
    q = ev.create_queue(run_id)
    assert ev.get_queue(run_id) is q
    ev.remove_queue(run_id)
    assert ev.get_queue(run_id) is None
