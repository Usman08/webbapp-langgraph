from contextlib import asynccontextmanager
from fastapi import FastAPI, HTTPException
from pydantic import BaseModel
from app.client import dotnet_client
from app.graph.build import compiled_graph
from app.graph.state import WorkflowState


@asynccontextmanager
async def lifespan(app: FastAPI):
    yield
    await dotnet_client.aclose()


app = FastAPI(title="Sales Invoice AI Engine", lifespan=lifespan)


class RunRequest(BaseModel):
    run_id: str
    request_text: str


class DisambiguateRequest(BaseModel):
    customer_id: str


@app.get("/health")
async def health():
    return {"status": "ok"}


@app.post("/run")
async def run_workflow(body: RunRequest):
    """Execute the LangGraph workflow for a NL invoice request."""
    initial_state: WorkflowState = {
        "run_id": body.run_id,
        "request_text": body.request_text,
        "steps": [],
        "events": [],
    }

    try:
        final_state = await compiled_graph.ainvoke(initial_state)
        return {"runId": body.run_id, "status": "completed", "state": final_state}
    except Exception as exc:
        raise HTTPException(status_code=500, detail=str(exc)) from exc
