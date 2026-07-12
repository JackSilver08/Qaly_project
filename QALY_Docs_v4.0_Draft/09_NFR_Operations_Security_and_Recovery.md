# Qaly NFR, Operations, Security, and Recovery v4.0 Draft

## 1. Purpose

This document converts non-functional expectations into measurable release gates. Values are targets unless marked Verified by dated evidence.

## 2. Performance targets

| ID | Target | Measurement |
|---|---|---|
| PERF-01 | Normal authenticated JSON API p95 below 500 ms and p99 below 1 s | Representative load against release-candidate topology, excluding AI execution, upload, export, and recovery |
| PERF-02 | AI job enqueue p95 below 500 ms | POST job returns after durable enqueue, not provider completion |
| PERF-03 | Redis-unavailable authenticated request fails over or fails explicitly within 1 s | Fault-injection test with connection removed before request |
| PERF-04 | Main user workflow remains interactive without long main-thread task over 200 ms | Browser performance trace on Dashboard, Tasks, Project, Group, Meeting, Analyst |
| PERF-05 | Route bundles have recorded budgets and regression diff | Build artifact report reviewed in CI |

Current evidence does not satisfy PERF-03. Local requests incurred approximately ten seconds when Redis was unavailable.

## 3. Availability and degraded dependencies

Health distinguishes live, ready, healthy, degraded, and unavailable.

| Dependency | Required degraded behavior |
|---|---|
| SQL Server | Readiness fails; no fake success or in-memory business writes. |
| Redis ticket store | Fail-fast controlled fallback or explicit authentication degradation; health reports state. |
| SignalR or realtime | Persisted operations continue; UI labels delayed realtime and can refresh. |
| AI provider | Queue, retry, eligible fallback, cache, or explicit 503; no hidden policy bypass. |
| Qdrant | Semantic features disable cleanly; core project and AI baseline remain usable. |
| LiveKit | Meeting join reports unavailable; existing notes and task links remain accessible. |
| Object storage | Upload blocks safely; existing metadata does not imply downloadable content. |

Circuit breakers, bounded retries, timeouts, and bulkheads are configured by dependency class and recorded in telemetry.

## 4. Security gates

| Gate | Requirement |
|---|---|
| Authentication | Secure cookie, CSRF, expiry, rotation, revocation, rate limit, and audit. |
| Authorization | Deny by default; tenant, project, private task, meeting, source, file, and admin negative tests. |
| Secrets | No secret in repository, frontend bundle, docs, logs, test artifact, or container layer. |
| Input | Structured validation, payload and file limits, output encoding, safe Markdown and HTML sanitization. |
| Files | Type and size policy, safe filename, content-addressed integrity, download authorization, malware control decision. |
| Dependencies | No Critical; no High without dated owner-approved exception, compensating control, expiry, and replacement plan. |
| Container | Non-root runtime, minimal image, pinned base strategy, health check, SBOM and vulnerability scan. |
| Logging | No password, API key, session ticket, raw sensitive transcript, or unbounded prompt content. |

Current blocking evidence includes Microsoft.OpenApi 2.0.0 High and npm production findings for xlsx, linkify-it, ws, dompurify, and markdown-it. xlsx currently has no npm fix and requires replacement, isolation, or an approved temporary exception.

## 5. Data integrity and EF query filters

- Required principal and dependent relationships use compatible soft-delete semantics.
- Model creation produces no unresolved required-navigation query-filter warning.
- Every filter-sensitive relationship has tests for active parent, deleted parent, deleted child, restore, and administrative query.
- IgnoreQueryFilters is limited to named recovery or administration services and always followed by explicit tenant policy.
- Task RowVersion and future versioned entities return 409 with current safe state on conflict.
- Domain update, version snapshot, audit reference, and outbox event commit atomically where required.
- Imports and AI confirmations are idempotent.

## 6. AI operational gates

- Queue depth, oldest queued age, running age, retry rate, failure code, provider latency, schema failure, cache hit, policy block, and cost are observable.
- Worker lease and duplicate delivery are tested.
- Provider model and pricing-table versions are recorded.
- Budget and compliance failure never returns successful mock content.
- Sensitive input never reaches an ineligible provider.
- Draft confirmation reconciles exactly one domain command and audit chain.

## 7. Privacy gates

Local P0-03 implementation and evidence now cover the controls below; target-environment verification remains open under `G-NFR-09` and evidence document 15.

- Sensitive meeting data requires policy and consent before ingestion or processing.
- Retention workers are observable and retryable.
- DSAR export and delete include live data, versions, AI drafts, cache, embeddings, and future backup expiry handling.
- Legal hold and shared-record exceptions are explicit.
- Privacy audit is access-controlled and content-minimized.

## 8. Observability

Every request carries a request or correlation ID through controller, application use case, database, outbox, worker, provider attempt, and audit.

Required signals:

- HTTP rate, latency, status, and route class;
- database latency, concurrency conflict, and query-filter warning;
- Redis timeout and fallback count;
- realtime connection and delivery failure;
- AI job and provider metrics listed above;
- import and retention worker backlog;
- backup, restore, migration, and smoke result;
- release version, commit, image digest, schema version, and feature flags.

Alerts link to owned runbooks. Logs and traces are sampled without losing errors or privacy events.

## 9. Backup and restore

Draft P0 targets pending infrastructure approval:

- Recovery Point Objective: 24 hours.
- Recovery Time Objective: 2 hours.
- Backup: SQL Server full backup with checksum and recorded metadata.
- Restore test: at least before each release candidate and on a scheduled cadence in an isolated target.

Recovery evidence records backup ID, checksum, encrypted storage location reference, database and schema version, application image digest, start and finish time, operator, restore target, migration result, smoke result, RPO achieved, RTO achieved, and cleanup.

The runbook covers database, object storage references, secrets, Redis non-authoritative state, vector rebuild, background jobs, and feature flags. A backup without a successful clean restore is not a Verified recovery gate.

## 10. Migration safety

1. Prefer expand-migrate-contract changes.
2. Backward-compatible application versions run during the rollback window.
3. Data backfills are restartable, idempotent, observable, and bounded.
4. Destructive schema changes require a verified backup and restore point.
5. Feature flags separate deployment from activation.
6. Rollback documents whether application-only rollback is compatible with the migrated schema.
7. Migration ownership and review are declared in the pull request.

## 11. GitHub collaboration and release governance

- GitHub is the canonical source for code and versioned documentation.
- Runtime databases remain the canonical source for runtime business data.
- Use short-lived feature or fix branches and pull requests into protected main.
- Required checks include frontend typecheck and build, configuration safety, backend build, unit, integration, required E2E, security, migration, and container validation.
- CODEOWNERS assigns modules, migrations, security, AI, privacy, and documentation.
- At least one qualified review is required; sensitive, migration, or recovery changes require the designated owner.
- No force-push or direct unreviewed change to protected main.
- Release tags use a documented semantic version and point to an immutable commit and image digest.
- Release notes link requirements, migrations, risks, evidence, known issues, feature flags, and rollback.

Current repository evidence includes CI and container publication workflows, but no CODEOWNERS or release tags. Branch-protection settings cannot be proven locally.

## 12. Release and rollback

Release sequence:

1. Build and test immutable candidate.
2. Scan dependencies, image, secrets, and configuration.
3. Verify migration compatibility and recovery point.
4. Deploy with inactive or scoped feature flags.
5. Run health, smoke, privacy, AI policy, and critical workflow checks.
6. Observe defined window and activate incrementally.
7. Record evidence and promote tag.

Rollback sequence selects one or more of feature disable, application image rollback, compensating migration, or database restore. The owner must know which option is safe for the specific schema change. Database restore is the last recovery mechanism, not a routine undo button.

## 13. Maintainability

- Oversized services and pages receive use-case decomposition plans with tests before movement.
- Do not set an arbitrary line-count release gate; review coupling, responsibilities, change frequency, and defect history.
- Shared UI state, API contracts, and entity resolution have one owner and test surface.
- New abstractions must remove real duplication or enforce a boundary.
- Architecture records and traceability change in the same pull request as affected behavior.

## 14. Blocking evidence set

A production candidate must attach:

- clean required CI checks;
- dependency and container scan;
- authorization and privacy negative tests;
- Redis and provider fault injection;
- AI worker and policy tests;
- query-filter model and relationship tests;
- backup and clean restore evidence;
- migration and image rollback evidence;
- canonical-link E2E and mobile accessibility evidence;
- release tag, image digest, schema version, flags, owners, and known issues.

Until this evidence exists, Qaly remains a draft or staging candidate rather than verified production-ready.
