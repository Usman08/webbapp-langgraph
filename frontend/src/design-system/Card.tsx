import { type HTMLAttributes } from "react";

interface CardProps extends HTMLAttributes<HTMLDivElement> {
  title?: string;
}

export function Card({ title, children, className = "", ...props }: CardProps) {
  return (
    <div
      {...props}
      className={`bg-surface border border-muted/40 rounded-xl p-4 ${className}`}
    >
      {title && <h2 className="text-sm font-semibold text-foreground-muted mb-3 uppercase tracking-wide">{title}</h2>}
      {children}
    </div>
  );
}
