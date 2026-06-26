import { useState, type FormEvent } from "react";
import { Button } from "../design-system/Button";

const MAX_CHARS = 2000;

interface RequestBoxProps {
  onSubmit: (text: string) => void;
  loading?: boolean;
  error?: string | null;
}

export function RequestBox({ onSubmit, loading, error }: RequestBoxProps) {
  const [text, setText] = useState("");

  function handleSubmit(e: FormEvent) {
    e.preventDefault();
    const trimmed = text.trim();
    if (!trimmed || loading) return;
    onSubmit(trimmed);
  }

  return (
    <form onSubmit={handleSubmit} aria-label="Invoice request">
      <div className="flex flex-col gap-2">
        <label htmlFor="request-input" className="text-sm font-medium text-foreground">
          Describe your order
        </label>
        <textarea
          id="request-input"
          value={text}
          onChange={(e) => setText(e.target.value.slice(0, MAX_CHARS))}
          placeholder='e.g. "Create an invoice for ABC Traders. Same products as last month, +20%, apply usual discount."'
          rows={4}
          className="w-full rounded-lg bg-surface border border-muted/60 text-foreground placeholder:text-foreground-muted
            p-3 text-sm resize-none focus:outline-none focus:ring-2 focus:ring-primary"
          disabled={loading}
          aria-describedby={error ? "request-error" : undefined}
        />
        <div className="flex items-center justify-between">
          <span className="text-xs text-foreground-muted" aria-live="polite">
            {text.length} / {MAX_CHARS}
          </span>
          {error && (
            <p id="request-error" role="alert" className="text-xs text-error">
              {error}
            </p>
          )}
          <Button type="submit" loading={loading} disabled={!text.trim()}>
            Submit Request
          </Button>
        </div>
      </div>
    </form>
  );
}
