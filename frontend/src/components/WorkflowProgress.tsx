import { CheckCircle, Circle, AlertCircle, Loader2 } from "lucide-react";

export interface WorkflowStep {
  sequence: number;
  name: string;
  status: "pending" | "running" | "completed" | "failed";
  tool?: string;
  summary?: string;
}

interface Props {
  steps: WorkflowStep[];
  isRunning: boolean;
}

const NODE_LABELS: Record<string, string> = {
  intent_parse: "Parse Intent",
  customer_lookup: "Resolve Customer",
  purchase_history: "Fetch Purchase History",
  quantity_adjustment: "Adjust Quantities",
  inventory_validation: "Validate Inventory",
  discount_resolution: "Resolve Discount",
  build_draft: "Build Draft Invoice",
};

function StepIcon({ status }: { status: WorkflowStep["status"] }) {
  const motionClass =
    typeof window !== "undefined" &&
    window.matchMedia("(prefers-reduced-motion: reduce)").matches
      ? ""
      : "animate-spin";

  switch (status) {
    case "completed":
      return <CheckCircle className="h-5 w-5 text-success flex-shrink-0" aria-hidden />;
    case "failed":
      return <AlertCircle className="h-5 w-5 text-error flex-shrink-0" aria-hidden />;
    case "running":
      return <Loader2 className={`h-5 w-5 text-primary flex-shrink-0 ${motionClass}`} aria-hidden />;
    default:
      return <Circle className="h-5 w-5 text-foreground-muted flex-shrink-0" aria-hidden />;
  }
}

export function WorkflowProgress({ steps, isRunning }: Props) {
  if (steps.length === 0 && !isRunning) return null;

  return (
    <section aria-label="Workflow progress" className="rounded-xl border border-border bg-surface p-4 space-y-1">
      <h2 className="text-sm font-semibold text-foreground mb-3">Workflow Steps</h2>
      <ol className="space-y-2">
        {steps.map((step) => (
          <li
            key={step.sequence}
            className="flex items-start gap-3 text-sm"
            aria-label={`${NODE_LABELS[step.name] ?? step.name}: ${step.status}`}
          >
            <StepIcon status={step.status} />
            <div className="flex-1 min-w-0">
              <span
                className={
                  step.status === "failed"
                    ? "text-error font-medium"
                    : step.status === "completed"
                    ? "text-foreground"
                    : "text-foreground-muted"
                }
              >
                {NODE_LABELS[step.name] ?? step.name}
              </span>
              {step.summary && (
                <p className="text-xs text-foreground-muted mt-0.5 truncate">{step.summary}</p>
              )}
            </div>
          </li>
        ))}
        {isRunning && steps.length === 0 && (
          <li className="flex items-center gap-3 text-sm text-foreground-muted">
            <Loader2 className="h-5 w-5 text-primary animate-spin flex-shrink-0" aria-hidden />
            Starting workflow…
          </li>
        )}
      </ol>
    </section>
  );
}
