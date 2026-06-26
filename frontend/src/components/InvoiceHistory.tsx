import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { ChevronDown, ChevronRight, Clock } from "lucide-react";
import { apiClient } from "../services/apiClient";

interface InvoiceSummary {
  id: string;
  customer: { id: string; name: string; type: string };
  date: string;
  total: number;
  status: string;
}

interface WorkflowStep {
  id: string;
  sequence: number;
  name: string;
  toolInvoked: string | null;
  isException: boolean;
  timestamp: string;
}

function fmt(n: number) {
  return n.toLocaleString("en-US", { style: "currency", currency: "USD" });
}

function fmtDate(s: string) {
  return new Date(s).toLocaleDateString("en-US", { month: "short", day: "numeric", year: "numeric" });
}

function WorkflowTrail({ invoiceId }: { invoiceId: string }) {
  const { data: steps, isLoading } = useQuery<WorkflowStep[]>({
    queryKey: ["workflow-trail", invoiceId],
    queryFn: () => apiClient.get<WorkflowStep[]>(`/api/invoices/${invoiceId}/workflow`),
  });

  if (isLoading) return <p className="text-xs text-foreground-muted p-2">Loading trail…</p>;
  if (!steps?.length) return <p className="text-xs text-foreground-muted p-2">No workflow steps recorded.</p>;

  return (
    <ol className="mt-2 space-y-1 border-l-2 border-border ml-3 pl-3">
      {steps.map((step) => (
        <li key={step.id} className="text-xs text-foreground-muted flex items-start gap-2">
          <span className={`mt-0.5 h-2 w-2 rounded-full flex-shrink-0 ${step.isException ? "bg-error" : "bg-success"}`} />
          <span>
            <span className="font-medium text-foreground">{step.name}</span>
            {step.toolInvoked && <span className="ml-1 opacity-70">→ {step.toolInvoked}</span>}
          </span>
        </li>
      ))}
    </ol>
  );
}

function InvoiceRow({ inv }: { inv: InvoiceSummary }) {
  const [open, setOpen] = useState(false);
  const [showTrail, setShowTrail] = useState(false);

  return (
    <li className="border-b border-border last:border-0">
      <button
        type="button"
        className="w-full flex items-center gap-3 px-3 py-3 text-left hover:bg-surface/60 transition-colors min-h-[44px]"
        onClick={() => setOpen((o) => !o)}
        aria-expanded={open}
        aria-label={`${open ? "Collapse" : "Expand"} invoice for ${inv.customer.name} — ${fmtDate(inv.date)}`}
      >
        {open ? (
          <ChevronDown className="h-4 w-4 text-foreground-muted flex-shrink-0" aria-hidden />
        ) : (
          <ChevronRight className="h-4 w-4 text-foreground-muted flex-shrink-0" aria-hidden />
        )}
        <div className="flex-1 min-w-0">
          <span className="text-sm font-medium text-foreground">{inv.customer.name}</span>
          <span className="ml-2 text-xs text-foreground-muted">{fmtDate(inv.date)}</span>
        </div>
        <span className="text-sm font-semibold text-foreground">{fmt(inv.total)}</span>
        <span className={`text-xs px-2 py-0.5 rounded-full ml-2 ${
          inv.status === "Finalised" ? "bg-success/20 text-green-400" : "bg-primary/20 text-primary"
        }`}>
          {inv.status}
        </span>
      </button>

      {open && (
        <div className="px-10 pb-3 space-y-2">
          <button
            type="button"
            onClick={() => setShowTrail((t) => !t)}
            className="flex items-center gap-1 text-xs text-primary hover:underline min-h-[44px]"
          >
            <Clock className="h-3.5 w-3.5" aria-hidden />
            {showTrail ? "Hide" : "View"} Workflow Trail
          </button>
          {showTrail && <WorkflowTrail invoiceId={inv.id} />}
        </div>
      )}
    </li>
  );
}

export function InvoiceHistory() {
  const { data: invoices, isLoading } = useQuery<InvoiceSummary[]>({
    queryKey: ["invoices"],
    queryFn: () => apiClient.get<InvoiceSummary[]>("/api/invoices"),
    refetchInterval: 30_000,
  });

  return (
    <section aria-label="Invoice history" className="rounded-xl border border-border bg-background">
      <h2 className="text-sm font-semibold text-foreground px-4 py-3 border-b border-border">Invoice History</h2>

      {isLoading && (
        <p className="text-sm text-foreground-muted px-4 py-3">Loading…</p>
      )}

      {!isLoading && (!invoices || invoices.length === 0) && (
        <p className="text-sm text-foreground-muted px-4 py-3">No invoices yet.</p>
      )}

      {invoices && invoices.length > 0 && (
        <ul>
          {invoices.map((inv) => (
            <InvoiceRow key={inv.id} inv={inv} />
          ))}
        </ul>
      )}
    </section>
  );
}
