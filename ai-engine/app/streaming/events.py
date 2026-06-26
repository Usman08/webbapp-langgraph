"""SSE event types and per-run event queue management."""
import asyncio
import json
from dataclasses import dataclass, field


@dataclass
class SSEEvent:
    event_type: str
    data: dict = field(default_factory=dict)


def format_sse(event: SSEEvent) -> str:
    return f"event: {event.event_type}\ndata: {json.dumps(event.data)}\n\n"


# In-memory per-run queues (PoC: single-process)
_run_queues: dict[str, asyncio.Queue] = {}

SENTINEL = object()  # signals end-of-stream


def create_queue(run_id: str) -> asyncio.Queue:
    q: asyncio.Queue = asyncio.Queue()
    _run_queues[run_id] = q
    return q


def get_queue(run_id: str) -> asyncio.Queue | None:
    return _run_queues.get(run_id)


def remove_queue(run_id: str) -> None:
    _run_queues.pop(run_id, None)


# ── Event builders ────────────────────────────────────────────────────────────

def run_started(run_id: str, request_text: str) -> SSEEvent:
    return SSEEvent("run_started", {"runId": run_id, "requestText": request_text})


def node_started(sequence: int, name: str) -> SSEEvent:
    return SSEEvent("node_started", {"sequence": sequence, "name": name})


def tool_invoked(sequence: int, tool: str, input_data: dict) -> SSEEvent:
    return SSEEvent("tool_invoked", {"sequence": sequence, "tool": tool, "input": input_data})


def tool_result(sequence: int, tool: str, output_data: dict) -> SSEEvent:
    return SSEEvent("tool_result", {"sequence": sequence, "tool": tool, "output": output_data})


def decision(sequence: int, summary: str) -> SSEEvent:
    return SSEEvent("decision", {"sequence": sequence, "summary": summary})


def exception_event(sequence: int, name: str, detail: str, resolution: str = "") -> SSEEvent:
    return SSEEvent("exception", {"sequence": sequence, "name": name, "detail": detail, "resolution": resolution})


def needs_input(kind: str, candidates: list) -> SSEEvent:
    return SSEEvent("needs_input", {"kind": kind, "candidates": candidates})


def draft_ready(invoice_id: str, total: float) -> SSEEvent:
    return SSEEvent("draft_ready", {"invoiceId": invoice_id, "total": total})


def workflow_complete(run_id: str) -> SSEEvent:
    return SSEEvent("workflow_complete", {"runId": run_id, "status": "AwaitingApproval"})


def workflow_failed(run_id: str, reason: str) -> SSEEvent:
    return SSEEvent("workflow_failed", {"runId": run_id, "reason": reason})


def recommendation(recommendation_id: str, sku: str, basis: str) -> SSEEvent:
    return SSEEvent("recommendation", {"recommendationId": recommendation_id, "sku": sku, "basis": basis})


def parse_error(message: str, suggestion: str = "") -> SSEEvent:
    return SSEEvent("parse_error", {"message": message, "suggestion": suggestion})
