import { useState } from "react";
import { ThumbsUp, ThumbsDown, Sparkles } from "lucide-react";

export interface Recommendation {
  recommendationId: string;
  sku: string;
  basis: string;
  status: "pending" | "accepted" | "declined";
}

interface Props {
  recommendations: Recommendation[];
  onAccept: (recommendationId: string) => Promise<void>;
  onDecline: (recommendationId: string) => Promise<void>;
}

export function RecommendationPanel({ recommendations, onAccept, onDecline }: Props) {
  const [busy, setBusy] = useState<string | null>(null);

  if (recommendations.length === 0) return null;

  const pending = recommendations.filter((r) => r.status === "pending");
  if (pending.length === 0) return null;

  async function handle(id: string, action: "accept" | "decline") {
    setBusy(id);
    try {
      if (action === "accept") await onAccept(id);
      else await onDecline(id);
    } finally {
      setBusy(null);
    }
  }

  return (
    <section aria-label="Product recommendations" className="rounded-xl border border-primary/30 bg-primary/5 p-4 space-y-3">
      <div className="flex items-center gap-2">
        <Sparkles className="h-4 w-4 text-primary" aria-hidden />
        <h2 className="text-sm font-semibold text-foreground">AI Recommendations</h2>
      </div>

      <ul className="space-y-2">
        {pending.map((rec) => (
          <li
            key={rec.recommendationId}
            className="flex items-start justify-between gap-3 rounded-lg border border-border bg-surface p-3"
          >
            <div className="flex-1 min-w-0">
              <p className="text-sm font-medium text-foreground">{rec.sku}</p>
              <p className="text-xs text-foreground-muted mt-0.5">{rec.basis}</p>
            </div>
            <div className="flex gap-2 flex-shrink-0">
              <button
                type="button"
                onClick={() => handle(rec.recommendationId, "accept")}
                disabled={!!busy}
                aria-label={`Accept recommendation for ${rec.sku}`}
                className="min-h-[44px] min-w-[44px] flex items-center justify-center rounded-lg bg-success/10 text-success hover:bg-success/20 disabled:opacity-50 transition-colors"
              >
                <ThumbsUp className="h-4 w-4" aria-hidden />
              </button>
              <button
                type="button"
                onClick={() => handle(rec.recommendationId, "decline")}
                disabled={!!busy}
                aria-label={`Decline recommendation for ${rec.sku}`}
                className="min-h-[44px] min-w-[44px] flex items-center justify-center rounded-lg bg-error/10 text-error hover:bg-error/20 disabled:opacity-50 transition-colors"
              >
                <ThumbsDown className="h-4 w-4" aria-hidden />
              </button>
            </div>
          </li>
        ))}
      </ul>
    </section>
  );
}
