from typing import Any
from typing_extensions import TypedDict


class WorkflowState(TypedDict, total=False):
    run_id: str
    request_text: str
    customer_id: str | None
    customer_name: str | None
    disambiguation_options: list[dict]
    purchase_history: dict | None
    requested_items: list[dict]
    adjusted_items: list[dict]
    inventory_result: list[dict]
    discount: dict | None
    draft_invoice: dict | None
    recommendations: list[dict]
    error: str | None
    steps: list[dict]
    events: list[Any]
