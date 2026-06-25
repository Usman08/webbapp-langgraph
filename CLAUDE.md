<!-- SPECKIT START -->
For additional context about technologies to be used, project structure,
shell commands, and other important information, read the current plan:
specs/001-ai-sales-invoice-poc/plan.md

Active feature: AI-Native Sales Invoice PoC (001-ai-sales-invoice-poc)
Stack: .NET 8 Web API (N-layer monolith, owns domain + Postgres) · React 18 + Vite + Tailwind
(frontend) · Python 3.12 LangGraph service (AI workflow engine, calls .NET agent-tool endpoints)
· PostgreSQL 16 (EF Core) · Claude claude-sonnet-4-6 (LLM) · SSE for real-time workflow streaming.
Design system: design-system/ai-sales-invoice-poc/ (from ui-ux-pro-max skill).
<!-- SPECKIT END -->
