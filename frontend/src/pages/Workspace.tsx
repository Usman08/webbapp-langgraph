import { useState, useCallback } from "react";
import { useQuery } from "@tanstack/react-query";
import { RequestBox } from "../components/RequestBox";
import { InvoicePreview } from "../components/InvoicePreview";
import { DisambiguationDialog } from "../components/DisambiguationDialog";
import { apiClient } from "../services/apiClient";

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

export function Workspace() {
  const [runId, setRunId] = useState<string | null>(null);
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  const { data: runState, isLoading: runLoading } = useQuery<RunState>({
    queryKey: ["run", runId],
    queryFn: () => apiClient.get<RunState>(`/api/invoices/requests/${runId}`),
    enabled: !!runId,
    refetchInterval: (query) => {
      const status = query.state.data?.status;
      return status === "Running" ? 2000 : false;
    },
  });

  type Invoice = Parameters<typeof InvoicePreview>[0]["invoice"];
  const { data: invoice } = useQuery<Invoice>({
    queryKey: ["invoice", runState?.draftInvoiceId],
    queryFn: () => apiClient.get<Invoice>(`/api/invoices/${runState!.draftInvoiceId}`),
    enabled: !!runState?.draftInvoiceId,
  });

  const handleSubmit = useCallback(async (text: string) => {
    setSubmitError(null);
    setSubmitting(true);
    setRunId(null);
    try {
      const result = await apiClient.post<RunResponse>("/api/invoices/requests", { requestText: text });
      setRunId(result.runId);
    } catch (err) {
      setSubmitError(err instanceof Error ? err.message : "Failed to submit request.");
    } finally {
      setSubmitting(false);
    }
  }, []);

  const handleDisambiguate = useCallback(async (customerId: string) => {
    if (!runId) return;
    await apiClient.post(`/api/invoices/requests/${runId}/disambiguate`, { customerId });
  }, [runId]);

  const isAmbiguous = runState?.status === "Running" && !runState?.customer;

  return (
    <div className="min-h-screen bg-background p-4 xs:p-6">
      <header className="mb-6">
        <h1 className="text-xl font-bold text-foreground">AI Sales Invoice</h1>
        <p className="text-foreground-muted text-sm">Describe your order in plain language</p>
      </header>

      <main className="max-w-3xl mx-auto space-y-4">
        <RequestBox
          onSubmit={handleSubmit}
          loading={submitting || runLoading || runState?.status === "Running"}
          error={submitError}
        />

        {runId && runState?.status === "Running" && !isAmbiguous && (
          <div role="status" aria-live="polite" className="flex items-center gap-3 text-sm text-foreground-muted">
            <svg className="animate-spin h-4 w-4 text-primary" viewBox="0 0 24 24" fill="none">
              <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
              <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8H4z" />
            </svg>
            AI workflow running…
          </div>
        )}

        {isAmbiguous && (
          <DisambiguationDialog
            candidates={[]}
            onSelect={handleDisambiguate}
          />
        )}

        {invoice && (
          <InvoicePreview invoice={invoice} />
        )}

        {runState?.status === "Failed" && (
          <div role="alert" className="text-error text-sm bg-error/10 rounded-lg p-3">
            The workflow failed. Please try again or rephrase your request.
          </div>
        )}
      </main>
    </div>
  );
}
