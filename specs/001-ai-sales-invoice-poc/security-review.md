# Security Review — AI-Native Sales Invoice PoC

**Scope**: single-user PoC, no auth/multi-tenancy.  
**Date reviewed**: 2026-06-26

## Threat model

| Actor | Trust level | Notes |
|---|---|---|
| Browser (operator) | Untrusted | All input validated server-side |
| .NET API | Trusted | Owns domain + DB; issues all DB writes |
| AI Engine | Semi-trusted (internal network) | Calls .NET via X-Engine-Token; not browser-reachable |
| Groq API | External | Server-side only; key never in client bundle |

## Controls in place

### Injection

| Risk | Mitigation |
|---|---|
| SQL injection | EF Core parameterised queries only; no raw SQL anywhere |
| Prompt injection (LLM) | `requestText` is trimmed + capped at 2000 chars server-side before forwarding to AI engine; LLM output is parsed into typed structures, not eval'd |
| XSS | React escapes all rendered strings by default; no `dangerouslySetInnerHTML` |

### Authentication / authorisation

| Surface | Control |
|---|---|
| `/internal/tools/*` | `X-Engine-Token` header required; returns 401 if absent or wrong; middleware short-circuits before any handler logic |
| `/api/*` | Single-user PoC — no user auth in scope; add OIDC/bearer before production |
| SSE stream | Run IDs are UUIDs (128-bit unguessable); no user sessions in scope |

### Secrets management

- `GROQ_API_KEY` is set via Docker env var in the AI engine container only — never passed to the browser or logged.
- `ENGINE_TOKEN` is set via Docker env var and mounted via `builder.UseSetting` in tests (never hardcoded beyond test constant `"test-token"`).
- `.env` files are in `.gitignore`; `.env.example` contains only placeholder values.

### Input validation

- `requestText`: `Trim()` + max 2000 chars enforced in `.NET` before persisting or forwarding.
- All request bodies validated via FluentValidation before reaching application logic.
- Quantity edits: integer range enforced (`Math.Max(0, qty)` in domain + `[Range(1, 9999)]` in API DTO).

### Dependency supply chain

- `requirements.txt` pins exact versions for the AI engine.
- NuGet packages restored from nuget.org; no private feeds.
- Node packages locked via `package-lock.json`.

## Known gaps (acceptable for PoC)

| Gap | Risk | Remediation for production |
|---|---|---|
| No user authentication | Any local user can create/approve invoices | Add OIDC/bearer token; scope invoice access by user |
| No rate limiting on `/api/invoices/requests` | Operator could trigger expensive LLM calls rapidly | Add ASP.NET rate-limiting middleware |
| `ENGINE_TOKEN` is a static shared secret | Compromised token grants full tool access | Rotate via secret manager; consider mTLS for the internal network |
| SSE run IDs not tied to a user session | Any client who guesses a UUID can stream another run | Acceptable for single-user PoC; bind run to session token in production |
| LLM output parsed but not sanitised for HTML | If ever rendered as HTML, LLM-generated strings could be injected | Enforce React rendering (no `innerHTML`) — currently satisfied |
