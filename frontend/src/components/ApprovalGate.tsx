import { useState } from "react";
import { CheckCircle, Edit3, Trash2, AlertTriangle } from "lucide-react";

export interface EditableLine {
  id: string;
  productId: string;
  sku: string;
  name: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
  stockStatus: "InStock" | "AlternativeSuggested" | "BackOrder";
}

interface Props {
  invoiceId: string;
  invoiceStatus: string;
  lines: EditableLine[];
  onApprove: () => Promise<void>;
  onReject: () => Promise<void>;
  onSaveEdits: (lines: EditableLine[]) => Promise<void>;
}

export function ApprovalGate({ invoiceStatus, lines, onApprove, onReject, onSaveEdits }: Props) {
  const [mode, setMode] = useState<"gate" | "editing">("gate");
  const [editLines, setEditLines] = useState<EditableLine[]>([]);
  const [confirmApprove, setConfirmApprove] = useState(false);
  const [busy, setBusy] = useState(false);

  if (invoiceStatus === "Finalised") return null;

  function startEdit() {
    setEditLines(lines.map((l) => ({ ...l })));
    setMode("editing");
  }

  function updateQty(id: string, qty: number) {
    setEditLines((prev) =>
      prev.map((l) => l.id === id ? { ...l, quantity: Math.max(0, qty), lineTotal: Math.max(0, qty) * l.unitPrice } : l)
    );
  }

  function removeLine(id: string) {
    setEditLines((prev) => prev.filter((l) => l.id !== id));
  }

  async function handleSave() {
    setBusy(true);
    try {
      await onSaveEdits(editLines.filter((l) => l.quantity > 0));
      setMode("gate");
    } finally {
      setBusy(false);
    }
  }

  async function handleApprove() {
    setBusy(true);
    try {
      await onApprove();
      setConfirmApprove(false);
    } finally {
      setBusy(false);
    }
  }

  async function handleReject() {
    setBusy(true);
    try {
      await onReject();
      startEdit();
    } finally {
      setBusy(false);
    }
  }

  if (mode === "editing") {
    return (
      <section aria-label="Edit invoice lines" className="rounded-xl border border-border bg-surface p-4 space-y-4">
        <h2 className="text-sm font-semibold text-foreground flex items-center gap-2">
          <Edit3 className="h-4 w-4 text-primary" aria-hidden />
          Edit Lines
        </h2>

        <ul className="space-y-2">
          {editLines.map((line) => (
            <li key={line.id} className={`flex items-center gap-3 p-2 rounded-lg border ${
              line.stockStatus === "BackOrder" ? "border-error/30 bg-error/5" : "border-border"
            }`}>
              {line.stockStatus === "BackOrder" && (
                <AlertTriangle className="h-4 w-4 text-error flex-shrink-0" aria-label="Back order" />
              )}
              <div className="flex-1 min-w-0">
                <p className="text-sm font-medium text-foreground truncate">{line.name}</p>
                <p className="text-xs text-foreground-muted">{line.sku} · ${line.unitPrice.toFixed(2)} each</p>
              </div>
              <label className="sr-only" htmlFor={`qty-${line.id}`}>Quantity for {line.name}</label>
              <input
                id={`qty-${line.id}`}
                type="number"
                min={0}
                value={line.quantity}
                onChange={(e) => updateQty(line.id, parseInt(e.target.value, 10) || 0)}
                className="w-20 text-right bg-background border border-border rounded-lg px-2 py-1 text-sm text-foreground focus:outline-none focus:ring-2 focus:ring-primary min-h-[44px]"
              />
              <button
                type="button"
                onClick={() => removeLine(line.id)}
                aria-label={`Remove ${line.name} from invoice`}
                className="min-h-[44px] min-w-[44px] flex items-center justify-center text-error hover:bg-error/10 rounded-lg transition-colors"
              >
                <Trash2 className="h-4 w-4" aria-hidden />
              </button>
            </li>
          ))}
        </ul>

        {editLines.length === 0 && (
          <p className="text-sm text-foreground-muted text-center py-2">No lines remaining.</p>
        )}

        <div className="flex gap-3 pt-2">
          <button
            type="button"
            onClick={handleSave}
            disabled={busy || editLines.length === 0}
            className="flex-1 min-h-[44px] rounded-lg bg-primary text-white font-medium text-sm hover:bg-primary/90 disabled:opacity-50 transition-colors"
          >
            Save Changes
          </button>
          <button
            type="button"
            onClick={() => setMode("gate")}
            disabled={busy}
            className="flex-1 min-h-[44px] rounded-lg border border-border text-foreground text-sm hover:bg-surface transition-colors"
          >
            Cancel
          </button>
        </div>
      </section>
    );
  }

  return (
    <section aria-label="Approval gate" className="rounded-xl border border-border bg-surface p-4 space-y-3">
      <h2 className="text-sm font-semibold text-foreground">Ready for Approval</h2>
      <p className="text-xs text-foreground-muted">Review the draft invoice above, then approve to finalise or reject to edit.</p>

      {confirmApprove ? (
        <div className="space-y-3">
          <p className="text-sm text-foreground font-medium">Confirm approval? This will finalise the invoice.</p>
          <div className="flex gap-3">
            <button
              type="button"
              onClick={handleApprove}
              disabled={busy}
              className="flex-1 min-h-[44px] rounded-lg bg-success text-white font-medium text-sm hover:bg-success/90 disabled:opacity-50 transition-colors"
            >
              {busy ? "Approving…" : "Yes, Approve"}
            </button>
            <button
              type="button"
              onClick={() => setConfirmApprove(false)}
              disabled={busy}
              className="flex-1 min-h-[44px] rounded-lg border border-border text-foreground text-sm hover:bg-surface transition-colors"
            >
              Cancel
            </button>
          </div>
        </div>
      ) : (
        <div className="flex gap-3">
          <button
            type="button"
            onClick={() => setConfirmApprove(true)}
            disabled={busy}
            className="flex-1 min-h-[44px] rounded-lg bg-success text-white font-medium text-sm hover:bg-success/90 disabled:opacity-50 transition-colors flex items-center justify-center gap-2"
          >
            <CheckCircle className="h-4 w-4" aria-hidden />
            Approve
          </button>
          <button
            type="button"
            onClick={handleReject}
            disabled={busy}
            className="flex-1 min-h-[44px] rounded-lg border border-border text-foreground text-sm hover:bg-surface transition-colors flex items-center justify-center gap-2"
          >
            <Edit3 className="h-4 w-4" aria-hidden />
            Reject &amp; Edit
          </button>
        </div>
      )}
    </section>
  );
}
