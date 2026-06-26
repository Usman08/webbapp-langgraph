import { useState } from "react";
import { ChevronDown, ChevronRight, AlertTriangle } from "lucide-react";
import type { WorkflowEvent } from "../services/sseClient";

interface Props {
  events: WorkflowEvent[];
}

const EVENT_LABELS: Record<string, string> = {
  run_started: "Run Started",
  node_started: "Node Started",
  tool_invoked: "Tool Invoked",
  tool_result: "Tool Result",
  decision: "Decision",
  needs_input: "Needs Input",
  recommendation: "Recommendation",
  exception: "Exception",
  draft_ready: "Draft Ready",
  parse_error: "Parse Error",
  workflow_complete: "Workflow Complete",
  workflow_failed: "Workflow Failed",
};

function LogEntry({ event }: { event: WorkflowEvent }) {
  const [open, setOpen] = useState(false);
  const isException = event.type === "exception" || event.type === "workflow_failed" || event.type === "parse_error";
  const isDecision = event.type === "decision";

  const summary =
    event.type === "tool_invoked"
      ? `→ ${event.data.tool as string}`
      : event.type === "tool_result"
      ? `← ${event.data.tool as string}`
      : event.type === "decision"
      ? (event.data.summary as string)
      : event.type === "exception"
      ? `${event.data.detail as string}`
      : event.type === "node_started"
      ? `[${event.data.name as string}]`
      : null;

  return (
    <li className={`text-xs rounded-lg border ${isException ? "border-error/40 bg-error/5" : "border-border bg-surface"}`}>
      <button
        type="button"
        onClick={() => setOpen((o) => !o)}
        className="w-full flex items-start gap-2 p-2 text-left min-h-[44px]"
        aria-expanded={open}
      >
        {open ? (
          <ChevronDown className="h-3.5 w-3.5 mt-0.5 flex-shrink-0 text-foreground-muted" aria-hidden />
        ) : (
          <ChevronRight className="h-3.5 w-3.5 mt-0.5 flex-shrink-0 text-foreground-muted" aria-hidden />
        )}
        {isException && (
          <AlertTriangle className="h-3.5 w-3.5 mt-0.5 flex-shrink-0 text-error" aria-hidden />
        )}
        <span className={`font-medium ${isException ? "text-error" : isDecision ? "text-primary" : "text-foreground"}`}>
          {EVENT_LABELS[event.type] ?? event.type}
        </span>
        {summary && !open && (
          <span className="text-foreground-muted truncate flex-1">{summary}</span>
        )}
      </button>

      {open && (
        <pre className="px-3 pb-2 text-foreground-muted whitespace-pre-wrap break-all overflow-auto max-h-48">
          {JSON.stringify(event.data, null, 2)}
        </pre>
      )}
    </li>
  );
}

export function ExecutionLog({ events }: Props) {
  if (events.length === 0) return null;

  return (
    <section aria-label="Execution log" className="rounded-xl border border-border bg-background p-4 space-y-2">
      <h2 className="text-sm font-semibold text-foreground">Execution Log</h2>
      <ol className="space-y-1.5">
        {events.map((event, i) => (
          <LogEntry key={i} event={event} />
        ))}
      </ol>
    </section>
  );
}
