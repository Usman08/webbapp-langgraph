"""Tool wrappers that call .NET agent-tool endpoints."""
from app.client import dotnet_client


async def resolve_customer(name_hint: str) -> dict:
    return await dotnet_client.post_tool("resolve-customer", {"nameHint": name_hint})


async def get_purchase_history(customer_id: str, lookback: str = "last_month") -> dict:
    return await dotnet_client.post_tool(
        "get-purchase-history", {"customerId": customer_id, "lookback": lookback}
    )


async def adjust_quantities(lines: list[dict], delta_percent: float) -> dict:
    return await dotnet_client.post_tool(
        "adjust-quantities", {"lines": lines, "deltaPercent": delta_percent}
    )


async def validate_inventory(lines: list[dict]) -> dict:
    return await dotnet_client.post_tool("validate-inventory", {"lines": lines})


async def resolve_discount(customer_id: str) -> dict:
    return await dotnet_client.post_tool("resolve-discount", {"customerId": customer_id})


async def build_draft(
    workflow_run_id: str,
    customer_id: str,
    lines: list[dict],
    discount_percentage: float,
) -> dict:
    return await dotnet_client.post_tool(
        "build-draft",
        {
            "workflowRunId": workflow_run_id,
            "customerId": customer_id,
            "lines": lines,
            "discountPercentage": discount_percentage,
        },
    )


async def record_step(
    workflow_run_id: str,
    sequence: int,
    name: str,
    tool_invoked: str | None,
    input_data: dict | None,
    output_data: dict | None,
    is_exception: bool = False,
) -> dict:
    return await dotnet_client.post_tool(
        "record-step",
        {
            "workflowRunId": workflow_run_id,
            "sequence": sequence,
            "name": name,
            "toolInvoked": tool_invoked,
            "input": input_data,
            "output": output_data,
            "isException": is_exception,
        },
    )


async def recommend_products(customer_id: str, draft_product_ids: list[str]) -> dict:
    return await dotnet_client.post_tool(
        "recommend-products",
        {"customerId": customer_id, "draftProductIds": draft_product_ids},
    )


async def save_recommendation(workflow_run_id: str, product_id: str, sku: str, basis: str) -> dict:
    return await dotnet_client.post_tool(
        "save-recommendation",
        {"workflowRunId": workflow_run_id, "productId": product_id, "sku": sku, "basis": basis},
    )
