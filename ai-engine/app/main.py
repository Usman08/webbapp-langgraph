import asyncio
from contextlib import asynccontextmanager
from fastapi import FastAPI, HTTPException
from fastapi.responses import StreamingResponse
from pydantic import BaseModel
from app.client import dotnet_client
from app.graph.build import compiled_graph
from app.graph.state import WorkflowState
from app.streaming import events as ev


@asynccontextmanager
async def lifespan(app: FastAPI):
    yield
    await dotnet_client.aclose()


app = FastAPI(title="Sales Invoice AI Engine", lifespan=lifespan)

# Pending runs awaiting a /stream consumer: run_id → (request_text, asyncio.Queue)
_pending_runs: dict[str, tuple[str, asyncio.Queue]] = {}


class RunRequest(BaseModel):
    run_id: str
    request_text: str


class DisambiguateRequest(BaseModel):
    customer_id: str


@app.get("/health")
async def health():
    return {"status": "ok"}


@app.post("/run")
async def trigger_run(body: RunRequest):
    """Register a pending run; execution starts when /run/{run_id}/stream is consumed."""
    q = ev.create_queue(body.run_id)
    _pending_runs[body.run_id] = (body.request_text, q)
    return {"runId": body.run_id, "status": "queued"}


@app.get("/run/{run_id}/stream")
async def stream_run(run_id: str):
    """Execute the LangGraph workflow and stream SSE events to the caller."""
    entry = _pending_runs.pop(run_id, None)
    if entry is None:
        # Queue may already exist if /run was called but not yet popped
        q = ev.get_queue(run_id)
        if q is None:
            raise HTTPException(status_code=404, detail="Run not found")
        request_text = ""
    else:
        request_text, q = entry

    initial_state: WorkflowState = {
        "run_id": run_id,
        "request_text": request_text,
        "steps": [],
        "events": [],
    }

    async def _run_graph():
        try:
            await compiled_graph.ainvoke(initial_state)
        except Exception as exc:
            await q.put(ev.workflow_failed(run_id, str(exc)))
        finally:
            await q.put(ev.SENTINEL)

    async def event_generator():
        task = asyncio.create_task(_run_graph())
        try:
            while True:
                item = await asyncio.wait_for(q.get(), timeout=120.0)
                if item is ev.SENTINEL:
                    break
                yield ev.format_sse(item)
        except asyncio.TimeoutError:
            yield ev.format_sse(ev.workflow_failed(run_id, "timeout"))
        finally:
            ev.remove_queue(run_id)
            task.cancel()

    return StreamingResponse(
        event_generator(),
        media_type="text/event-stream",
        headers={"Cache-Control": "no-cache", "X-Accel-Buffering": "no"},
    )
