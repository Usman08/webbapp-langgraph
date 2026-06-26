const BASE_URL = import.meta.env.VITE_API_URL ?? "";

export type WorkflowEventType =
  | "run_started"
  | "node_started"
  | "tool_invoked"
  | "tool_result"
  | "decision"
  | "needs_input"
  | "recommendation"
  | "exception"
  | "draft_ready"
  | "parse_error"
  | "workflow_complete"
  | "workflow_failed";

export interface WorkflowEvent {
  type: WorkflowEventType;
  data: Record<string, unknown>;
}

export type WorkflowEventHandler = (event: WorkflowEvent) => void;

const TERMINAL_EVENTS: WorkflowEventType[] = ["workflow_complete", "workflow_failed", "parse_error"];
const ALL_EVENT_TYPES: WorkflowEventType[] = [
  "run_started", "node_started", "tool_invoked", "tool_result",
  "decision", "needs_input", "recommendation", "exception",
  "draft_ready", "parse_error", "workflow_complete", "workflow_failed",
];

export function connectWorkflowStream(
  path: string,
  onEvent: WorkflowEventHandler,
  onError?: (e: Event) => void,
): () => void {
  const es = new EventSource(`${BASE_URL}${path}`);

  for (const type of ALL_EVENT_TYPES) {
    es.addEventListener(type, (e: MessageEvent) => {
      try {
        const data = JSON.parse(e.data) as Record<string, unknown>;
        onEvent({ type, data });
        if (TERMINAL_EVENTS.includes(type)) {
          es.close();
        }
      } catch {
        // ignore malformed events
      }
    });
  }

  if (onError) {
    es.onerror = onError;
  }

  return () => es.close();
}
