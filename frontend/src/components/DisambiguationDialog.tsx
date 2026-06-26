import { Button } from "../design-system/Button";

interface Candidate {
  id: string;
  name: string;
  type: string;
}

interface DisambiguationDialogProps {
  candidates: Candidate[];
  onSelect: (customerId: string) => void;
  loading?: boolean;
}

export function DisambiguationDialog({ candidates, onSelect, loading }: DisambiguationDialogProps) {
  return (
    <div role="dialog" aria-modal="true" aria-label="Select customer"
      className="bg-surface border border-muted/40 rounded-xl p-4">
      <h2 className="font-semibold text-foreground mb-1">Ambiguous Customer</h2>
      <p className="text-sm text-foreground-muted mb-4">
        Multiple customers matched. Please select one to continue.
      </p>
      <ul className="space-y-2">
        {candidates.map((c) => (
          <li key={c.id}>
            <Button
              variant="secondary"
              className="w-full text-left justify-start"
              onClick={() => onSelect(c.id)}
              disabled={loading}
            >
              <span className="font-medium">{c.name}</span>
              <span className="ml-2 text-xs text-foreground-muted">({c.type})</span>
            </Button>
          </li>
        ))}
      </ul>
    </div>
  );
}
