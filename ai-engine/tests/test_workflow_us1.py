"""
US1 happy-path test with mocked LLM + mocked .NET tools.
"""
from unittest.mock import AsyncMock, patch
import pytest
from app.graph.state import WorkflowState


MOCK_CUSTOMER = {
    "status": "resolved",
    "customer": {"id": "cust-123", "name": "ABC Traders", "type": "Retail", "discountTier": "retail-standard"},
}

MOCK_HISTORY = {
    "mostRecentInvoice": {
        "invoiceId": "inv-001",
        "date": "2026-05-01",
        "lines": [{"productId": "prod-001", "sku": "ELC-001", "quantity": 2}],
    },
    "coPurchasePatterns": [],
}

MOCK_ADJUSTED = {"lines": [{"productId": "prod-001", "quantity": 3}]}

MOCK_INVENTORY = {
    "lines": [{"productId": "prod-001", "quantity": 3, "stockStatus": "InStock"}]
}

MOCK_DISCOUNT = {"status": "resolved", "percentage": 5.0, "ruleKey": "retail-standard"}

MOCK_DRAFT = {
    "invoiceId": "inv-draft-001",
    "subtotal": 3897.0,
    "discountAmount": 194.85,
    "taxAmount": 370.22,
    "total": 4072.37,
    "status": "Draft",
}

MOCK_RECORD_STEP = {"stepId": "step-uuid"}


@pytest.fixture
def mock_llm_response():
    import json
    return json.dumps({
        "customer_name": "ABC Traders",
        "items": [],
        "quantity_adjustment_percent": 20,
        "use_previous_order": True,
        "apply_usual_discount": True,
    })


@pytest.mark.asyncio
async def test_us1_happy_path(mock_llm_response):
    """
    Full workflow graph with mocked LLM and mocked .NET tool calls.
    Verifies intent parse → customer → history → adjust → inventory → discount → draft.
    """
    from langchain_core.messages import AIMessage

    with (
        patch("app.graph.nodes.get_llm") as mock_get_llm,
        patch("app.tools.dotnet_tools.resolve_customer", new_callable=AsyncMock, return_value=MOCK_CUSTOMER),
        patch("app.tools.dotnet_tools.get_purchase_history", new_callable=AsyncMock, return_value=MOCK_HISTORY),
        patch("app.tools.dotnet_tools.adjust_quantities", new_callable=AsyncMock, return_value=MOCK_ADJUSTED),
        patch("app.tools.dotnet_tools.validate_inventory", new_callable=AsyncMock, return_value=MOCK_INVENTORY),
        patch("app.tools.dotnet_tools.resolve_discount", new_callable=AsyncMock, return_value=MOCK_DISCOUNT),
        patch("app.tools.dotnet_tools.build_draft", new_callable=AsyncMock, return_value=MOCK_DRAFT),
        patch("app.tools.dotnet_tools.record_step", new_callable=AsyncMock, return_value=MOCK_RECORD_STEP),
    ):
        mock_llm = AsyncMock()
        mock_llm.ainvoke = AsyncMock(return_value=AIMessage(content=mock_llm_response))
        mock_get_llm.return_value = mock_llm

        from app.graph.build import build_graph
        graph = build_graph()

        initial: WorkflowState = {
            "run_id": "run-test-001",
            "request_text": "Create an invoice for ABC Traders. Same products as last month. Increase by 20%.",
        }

        result = await graph.ainvoke(initial)

        assert result.get("customer_id") == "cust-123"
        assert result.get("customer_name") == "ABC Traders"
        assert result.get("draft_invoice") is not None
        assert result["draft_invoice"]["status"] == "Draft"
        assert result.get("error") is None


@pytest.mark.asyncio
async def test_us1_customer_not_found():
    """Workflow terminates gracefully when customer cannot be resolved."""
    from langchain_core.messages import AIMessage
    import json

    intent = json.dumps({
        "customer_name": "Unknown Corp",
        "items": [],
        "quantity_adjustment_percent": 0,
        "use_previous_order": False,
        "apply_usual_discount": False,
    })

    with (
        patch("app.graph.nodes.get_llm") as mock_get_llm,
        patch("app.tools.dotnet_tools.resolve_customer", new_callable=AsyncMock,
              return_value={"status": "not_found"}),
        patch("app.tools.dotnet_tools.record_step", new_callable=AsyncMock, return_value=MOCK_RECORD_STEP),
    ):
        mock_llm = AsyncMock()
        mock_llm.ainvoke = AsyncMock(return_value=AIMessage(content=intent))
        mock_get_llm.return_value = mock_llm

        from app.graph.build import build_graph
        graph = build_graph()

        result = await graph.ainvoke({
            "run_id": "run-test-002",
            "request_text": "Invoice for Unknown Corp",
        })

        assert result.get("error") == "customer_not_found"
        assert result.get("draft_invoice") is None
