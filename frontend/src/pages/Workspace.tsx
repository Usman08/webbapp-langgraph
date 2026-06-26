import { useState, useCallback, useRef } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { RequestBox } from "../components/RequestBox";
import { InvoicePreview } from "../components/InvoicePreview";
import { DisambiguationDialog } from "../components/DisambiguationDialog";
import { WorkflowProgress, type WorkflowStep } from "../components/WorkflowProgress";
import { ExecutionLog } from "../components/ExecutionLog";
import { RecommendationPanel, type Recommendation } from "../components/RecommendationPanel";
import { ApprovalGate, type EditableLine } from "../components/ApprovalGate";
import { InvoiceHistory } from "../components/InvoiceHistory";
import { apiClient } from "../services/apiClient";
import { connectWorkflowStream, type WorkflowEvent } from "../services/sseClient";

type RunStatus = "Running" | "AwaitingApproval" | "Completed" | "Failed";

interface RunResponse {
  runId: string;
  status: RunStatus;
  streamUrl: string;
}

interface RunState {
  runId: string;
  status: RunStatus;
  customer?: { id: string; name: string; type: string };
  draftInvoiceId?: string;
  steps: unknown[];
}

const NODE_ORDER = [
  "intent_parse",
  "customer_lookup",
  "purchase_history",
  "quantity_adjustment",
  "inventory_validation",
  "recommend_products",
  "discount_resolution",
  "build_draft",
];

export function Workspace() {
  const queryClient = useQueryClient();
  const [runId, setRunId] = useState<string | null>(null);
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [sseEvents, setSseEvents] = useState<WorkflowEvent[]>([]);
  const [steps, setSteps] = useState<WorkflowStep[]>([]);
  const [streamDone, setStreamDone] = useState(false);
  const [disambigCandidates, setDisambigCandidates] = useState<{ id: string; name: string; type: string }[]>([]);
  const [recommendations, setRecommendations] = useState<Recommendation[]>([]);
  const [invoiceStatus, setInvoiceStatus] = useState<string>("Draft");
  const closeStreamRef = useRef<(() => void) | null>(null);

  const { data: runState } = useQuery<RunState>({
    queryKey: ["run", runId],
    queryFn: () => apiClient.get<RunState>(`/api/invoices/requests/${runId}`),
    enabled: !!runId && streamDone,
    refetchInterval: false,
  });

  type Invoice = Parameters<typeof InvoicePreview>[0]["invoice"];
  const { data: invoice, refetch: refetchInvoice } = useQuery<Invoice>({
    queryKey: ["invoice", runState?.draftInvoiceId],
    queryFn: () => apiClient.get<Invoice>(`/api/invoices/${runState!.draftInvoiceId}`),
    enabled: !!runState?.draftInvoiceId,
  });

  // Keep invoiceStatus in sync with fetched invoice
  const invoiceId = runState?.draftInvoiceId ?? null;
  const currentInvoiceStatus = invoice?.status ?? invoiceStatus;

  const updateStep = useCallback((name: string, patch: Partial<WorkflowStep>) => {
    setSteps((prev) => {
      const idx = prev.findIndex((s) => s.name === name);
      if (idx >= 0) {
        const next = [...prev];
        next[idx] = { ...next[idx], ...patch };
        return next;
      }
      const sequence = NODE_ORDER.indexOf(name) + 1;
      return [...prev, { sequence, name, status: "pending", ...patch }];
    });
  }, []);

  const handleSseEvent = useCallback(
    (event: WorkflowEvent) => {
      setSseEvents((prev) => [...prev, event]);

      switch (event.type) {
        case "node_started":
          updateStep(event.data.name as string, { status: "running" });
          break;
        case "tool_result":
          updateStep(event.data.name as string ?? "", { status: "completed" });
          break;
        case "decision": {
          const name = event.data.name as string | undefined;
          if (name) updateStep(name, { summary: event.data.summary as string });
          break;
        }
        case "exception":
          updateStep(event.data.name as string, { status: "failed" });
          break;
        case "needs_input":
          setDisambigCandidates(event.data.candidates as { id: string; name: string; type: string }[]);
          break;
        case "recommendation":
          setRecommendations((prev) => [
            ...prev,
            {
              recommendationId: event.data.recommendationId as string,
              sku: event.data.sku as string,
              basis: event.data.basis as string,
              status: "pending",
            },
          ]);
          break;
        case "draft_ready":
        case "workflow_complete":
          setStreamDone(true);
          setSteps((prev) =>
            prev.map((s) => (s.status === "running" ? { ...s, status: "completed" } : s))
          );
          break;
        case "workflow_failed":
        case "parse_error":
          setStreamDone(true);
          setSteps((prev) =>
            prev.map((s) => (s.status === "running" ? { ...s, status: "failed" } : s))
          );
          break;
      }
    },
    [updateStep]
  );

  const handleRecommendationAction = useCallback(async (recId: string, accepted: boolean) => {
    if (!runId) return;
    const result = await apiClient.post<{ updatedInvoice?: unknown }>(
      `/api/invoices/requests/${runId}/recommendations/${recId}`,
      { accepted }
    );
    setRecommendations((prev) =>
      prev.map((r) => r.recommendationId === recId ? { ...r, status: accepted ? "accepted" : "declined" } : r)
    );
    if (accepted && result.updatedInvoice) {
      await refetchInvoice();
    }
  }, [runId, refetchInvoice]);

  const handleApprove = useCallback(async () => {
    if (!invoiceId) return;
    await apiClient.post(`/api/invoices/${invoiceId}/approve`, {});
    setInvoiceStatus("Finalised");
    await refetchInvoice();
    await queryClient.invalidateQueries({ queryKey: ["invoices"] });
  }, [invoiceId, refetchInvoice, queryClient]);

  const handleReject = useCallback(async () => {
    if (!invoiceId) return;
    await apiClient.post(`/api/invoices/${invoiceId}/reject`, {});
    // Status stays Draft — editing mode is entered by ApprovalGate
  }, [invoiceId]);

  const handleSaveEdits = useCallback(async (editedLines: EditableLine[]) => {
    if (!invoiceId) return;
    const payload = editedLines.map((l) => ({
      productId: l.productId,
      quantity: l.quantity,
      stockStatus: l.stockStatus,
    }));
    await apiClient.put(`/api/invoices/${invoiceId}/lines`, payload);
    await refetchInvoice();
  }, [invoiceId, refetchInvoice]);

  const handleSubmit = useCallback(async (text: string) => {
    setSubmitError(null);
    setSubmitting(true);
    setRunId(null);
    setSseEvents([]);
    setSteps([]);
    setStreamDone(false);
    setDisambigCandidates([]);
    setRecommendations([]);
    setInvoiceStatus("Draft");
    closeStreamRef.current?.();

    try {
      const result = await apiClient.post<RunResponse>("/api/invoices/requests", { requestText: text });
      setRunId(result.runId);

      const close = connectWorkflowStream(
        result.streamUrl,
        handleSseEvent,
        () => setStreamDone(true),
      );
      closeStreamRef.current = close;
    } catch (err) {
      setSubmitError(err instanceof Error ? err.message : "Failed to submit request.");
    } finally {
      setSubmitting(false);
    }
  }, [handleSseEvent]);

  const handleDisambiguate = useCallback(async (customerId: string) => {
    if (!runId) return;
    setDisambigCandidates([]);
    await apiClient.post(`/api/invoices/requests/${runId}/disambiguate`, { customerId });
  }, [runId]);

  const parseError = sseEvents.find((e) => e.type === "parse_error");
  const isRunning = !!runId && !streamDone;
  const isFailed = sseEvents.some((e) => e.type === "workflow_failed") && streamDone;

  const approvalLines: EditableLine[] = invoice?.lineItems?.map((l) => ({
    id: l.id,
    productId: l.productId,
    sku: l.sku,
    name: l.name,
    quantity: l.quantity,
    unitPrice: l.unitPrice,
    lineTotal: l.lineTotal,
    stockStatus: l.stockStatus,
  })) ?? [];

  return (
    <div className="min-h-screen bg-background p-4 xs:p-6">
      <header className="mb-6">
        <h1 className="text-xl font-bold text-foreground">AI Sales Invoice</h1>
        <p className="text-foreground-muted text-sm">Describe your order in plain language</p>
      </header>

      <main className="max-w-3xl mx-auto space-y-4">
        <RequestBox
          onSubmit={handleSubmit}
          loading={submitting || isRunning}
          error={submitError}
        />

        {parseError && (
          <div role="alert" className="text-error text-sm bg-error/10 rounded-lg p-3">
            <strong>Could not understand request:</strong>{" "}
            {String(parseError.data.message ?? "")}
            {parseError.data.suggestion != null && (
              <p className="mt-1 text-foreground-muted">{String(parseError.data.suggestion)}</p>
            )}
          </div>
        )}

        {disambigCandidates.length > 0 && (
          <DisambiguationDialog
            candidates={disambigCandidates}
            onSelect={handleDisambiguate}
          />
        )}

        <WorkflowProgress steps={steps} isRunning={isRunning} />

        <RecommendationPanel
          recommendations={recommendations}
          onAccept={(id) => handleRecommendationAction(id, true)}
          onDecline={(id) => handleRecommendationAction(id, false)}
        />

        {invoice && <InvoicePreview invoice={invoice} />}

        {invoice && currentInvoiceStatus === "Draft" && streamDone && (
          <ApprovalGate
            invoiceId={invoice.id}
            invoiceStatus={currentInvoiceStatus}
            lines={approvalLines}
            onApprove={handleApprove}
            onReject={handleReject}
            onSaveEdits={handleSaveEdits}
          />
        )}

        {invoice && currentInvoiceStatus === "Finalised" && (
          <div role="status" className="text-success text-sm bg-success/10 rounded-lg p-3 flex items-center gap-2">
            <span className="font-medium">Invoice finalised.</span>
            <span className="text-foreground-muted">Total: {invoice.total.toLocaleString("en-US", { style: "currency", currency: "USD" })}</span>
          </div>
        )}

        {isFailed && (
          <div role="alert" className="text-error text-sm bg-error/10 rounded-lg p-3">
            The workflow failed. Please try again or rephrase your request.
          </div>
        )}

        <ExecutionLog events={sseEvents} />

        <InvoiceHistory />
      </main>
    </div>
  );
}
