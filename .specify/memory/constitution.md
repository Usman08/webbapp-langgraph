<!--
  SYNC IMPACT REPORT
  ==================
  Version change: (unversioned) → 1.0.0
  Modified principles: N/A (initial constitution)
  Added sections:
    - I. Mobile First
    - II. Security First
    - III. Intuitive UX
    - IV. Monolithic N-Layer Architecture
    - Quality & Testing Standards
    - Development Workflow
    - Governance
  Removed sections: N/A
  Templates checked:
    - .specify/templates/plan-template.md ✅ aligned (Constitution Check gate present)
    - .specify/templates/spec-template.md ✅ aligned (mobile/security requirements will flow into FR/SC sections)
    - .specify/templates/tasks-template.md ✅ aligned (security hardening and mobile tasks in Phase N)
  Deferred TODOs:
    - RATIFICATION_DATE set to today (2026-06-24); update if project has an earlier adoption date.
-->

# LangGraph WebApp Constitution

## Core Principles

### I. Mobile First

Every UI feature MUST be designed and implemented for mobile viewports before desktop.
Responsive breakpoints MUST be defined at 375 px (mobile), 768 px (tablet), and 1280 px (desktop).
Touch targets MUST meet a minimum size of 44 × 44 px.
No feature ships without a passing mobile-viewport test (real device or emulator).
Desktop enhancements are additive — they MUST NOT break the mobile baseline.

**Rationale**: The primary user base accesses the application on mobile devices. Retrofitting
mobile support after desktop-first development is consistently more costly and error-prone.

### II. Security First

Authentication and authorization MUST be validated on every request at the server layer — never
rely solely on client-side guards.
All user-supplied input MUST be validated and sanitized before processing or persistence (OWASP
Top 10 compliance is non-negotiable).
Secrets (API keys, tokens, credentials) MUST never appear in source code, logs, or client
bundles — use environment variables or a secrets manager.
HTTPS MUST be enforced in all environments; HTTP endpoints MUST redirect to HTTPS.
Dependencies MUST be audited for known CVEs before each release (`npm audit` / `pip audit`
or equivalent with zero HIGH/CRITICAL threshold).
Security-sensitive operations (auth, payments, data export) MUST produce structured audit log
entries.

**Rationale**: A single security breach can destroy user trust and incur regulatory liability.
Security controls applied retroactively are far more expensive than those built in from day one.

### III. Intuitive UX

Every user-facing flow MUST be completable without consulting documentation.
Error messages MUST be actionable: tell the user what went wrong AND what to do next.
Loading states MUST be surfaced for any operation exceeding 300 ms.
Destructive actions (delete, reset, revoke) MUST require explicit confirmation.
Consistency MUST be enforced through a shared component/design-token library — ad-hoc styles
are forbidden outside that system.

**Rationale**: Cognitive load reduction directly correlates with adoption and retention.
Consistent, self-explanatory interfaces reduce support burden and build trust.

### IV. Monolithic N-Layer Architecture

The application MUST follow a strict N-layer separation:
- **Presentation Layer** — UI components, routing, view models (no business logic).
- **Application Layer** — Use cases / service orchestration (no direct DB or external-API calls).
- **Domain Layer** — Core entities, business rules, domain events (framework-agnostic).
- **Infrastructure Layer** — Database adapters, external API clients, file storage, messaging.

Cross-layer imports MUST flow inward only (Presentation → Application → Domain ←
Infrastructure); circular dependencies are forbidden.
The monolith ships as a single deployable unit; premature service extraction is forbidden
unless explicitly approved via a constitution amendment.
Layer boundaries MUST be enforced by linting rules or module-boundary tooling.

**Rationale**: A well-structured monolith is faster to develop, easier to debug, and simpler
to deploy than a distributed system at this stage. N-layer separation keeps the codebase
testable and the domain logic portable if extraction becomes necessary later.

## Quality & Testing Standards

Unit tests MUST cover all Domain and Application layer logic (target ≥ 80 % branch coverage).
Integration tests MUST cover all Infrastructure adapters and critical Application flows.
End-to-end tests MUST cover every P1 user story acceptance scenario.
No PR merges with failing tests or linting errors.
Performance: P95 API response time MUST remain below 500 ms under expected load.

## Development Workflow

All features MUST be specced in `.specify/specs/` before implementation begins.
The Constitution Check gate in `plan.md` MUST be completed and passed before Phase 0 research.
PRs MUST reference the relevant spec and task IDs.
Security review MUST be conducted for any PR touching auth, data access, or external integrations.
Mobile viewport screenshots MUST accompany UI PRs.

## Governance

This constitution supersedes all other practices and informal agreements.
Amendments require: (1) documented rationale, (2) version bump per semantic versioning below,
(3) propagation to all dependent templates, (4) team acknowledgement.

**Versioning policy**:
- MAJOR — principle removed, renamed, or fundamentally redefined (backward-incompatible).
- MINOR — new principle or section added, or material guidance expansion.
- PATCH — clarification, wording, or typo fix with no semantic change.

All PRs and code reviews MUST verify compliance with the four core principles.
Complexity deviations from Principle IV MUST be justified in the plan's Complexity Tracking table.

**Version**: 1.0.0 | **Ratified**: 2026-06-24 | **Last Amended**: 2026-06-24
