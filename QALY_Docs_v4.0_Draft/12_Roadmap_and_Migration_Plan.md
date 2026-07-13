# Qaly v4.0 Draft Roadmap and Migration Plan

## 1. Planning rules

- Close safety, integrity, and release-truth gaps before adding product breadth.
- Preserve working behavior through compatibility adapters and feature flags.
- Use expand, migrate, verify, and contract for schema changes.
- Keep AI advisory and draft-only until confirmation.
- Every work package closes named acceptance gates and risks.
- Effort uses S, M, L, and XL as relative planning bands, not calendar commitments.

## 2. Dependency sequence

| Order | Package | Priority | Effort | Depends on | Exit evidence |
|---:|---|---|---:|---|---|
| 1 | P0-00 Approve v4 scope, ADRs, ownership, and traceability | P0 | S | Product and engineering review | Draft approved; owners assigned; no status-count contradiction |
| 2 | P0-01 Repair CI typecheck and dependency security gate | P0 | M | P0-00 | Required CI green; no unapproved High; exception records expire |
| 3 | P0-02 Canonical AI job, dispatch worker, errors, and draft APIs | P0 | L | ADR-003; P0-01 | Worker lifecycle, duplicate delivery, retry, cancel, idempotency, schema, audit tests |
| 4 | P0-03 Privacy consent, retention, cloud policy, and DSAR | P0 | L | ADR-005; P0-02 identity fields | Consent, sensitive default, export, delete, legal hold, worker evidence |
| 5 | P0-04 AI daily and monthly usage, budget, warning, and UI | P0 | M | P0-02; P0-03 provider policy | Usage reconciliation, warning, hard stop, role, audit, UI evidence |
| 6 | P0-05 EF soft-delete integrity and Redis degraded resilience | P0 | M-L | P0-01 | No unresolved model warning; fail-fast fault injection and health evidence |
| 7 | P0-06 Canonical entity links and critical QoL truthfulness | P0 | M | ADR-004; entity registry design | Task URL, source links, reciprocal Group-Project, partial-save E2E |
| 8 | P0-07 Backup restore release tags and GitHub governance | P0 | M-L | P0-01; migration inventory | Clean restore, smoke, RPO/RTO, protected main, CODEOWNERS, tag, image digest |
| 9 | P0-08 Complete and sign P0 acceptance evidence | P0 | M | P0-01 through P0-07 | All blocking P0 gates Verified; no Critical risk; known issues approved |
| 10 | P1-01 Complete AI-03, AI-04, AI-06, AI-07, and AI-08 | P1 | L | P0-02 through P0-04 | Locked schemas, source refs, draft review, project and sprint evidence |
| 11 | P1-02 Full entity graph, previews, backlinks, and accessibility | P1 | M-L | P0-06 | Permission-aware preview, back behavior, mobile and keyboard evidence |
| 12 | P1-03 Skills, capacity, availability, and task requirements | P1 | L | Product policy; privacy review | Data correction, freshness, permission, fairness, migration evidence |
| 13 | P1-04 Constraint scheduling scenarios and AI explanation | P1 | XL | P1-03; ADR-006 | Feasibility, repeatability, no over-allocation, diff and confirmation tests |
| 14 | P1-05 Wiki, project-settings, and task versioning | P1 | L | ADR-007; P0-05 | Snapshot, diff, conflict, rollback, audit, retention, recovery tests |
| 15 | P1-06 Decompose oversized modules and improve bundle performance | P1 | L-XL | Stable P0 regression suite; ownership | Architecture tests, unchanged contracts, performance and regression evidence |
| 16 | P2-01 Semantic AI and portfolio experiments | P2 | XL | P0 and relevant P1 gates | Experiment ADR, privacy review, measurable benefit, kill switch |
| 17 | P2-02 Evaluate multiple groups per project | P2 | L | Real usage evidence from ADR-004 model | Approved workflows, role semantics, migration and navigation evidence |

## 3. P0 work-package detail

### P0-01 CI and dependency security

- Fix the route-parameter type error at App.vue line 508.
- Upgrade the Microsoft.OpenApi dependency path.
- Upgrade fixable npm findings.
- Replace, isolate, or document a time-bounded exception for xlsx.
- Make the vulnerability policy enforce no Critical and no unapproved High.
- Record audit date and package graph rather than copying a permanent zero-vulnerability claim.

Working-tree status on 2026-07-11: implementation complete with local typecheck, build, regression, vulnerability-scan, report-script, and route-smoke evidence in [13_P0-01_CI_Security_Evidence.md](13_P0-01_CI_Security_Evidence.md). The hosted GitHub required check remains pending, so `G-NFR-01` is `Implemented-Unverified`; this status is not a production-readiness claim.

Rollback: revert dependency or type patch while keeping the old build artifact; do not waive a failing gate silently.

### P0-02 AI platform

- Add canonical status and dispatch structures.
- Implement durable worker lease, retry, cancel, and reconciliation.
- Implement job, result, draft, usage, budget, and health endpoints.
- Route current wrappers to jobs behind a feature flag.
- Separate provider degradation from compliance and budget errors.

Working-tree status on 2026-07-13: implementation and local evidence are complete in [14_P0-02_Canonical_AI_Job_Evidence.md](14_P0-02_Canonical_AI_Job_Evidence.md). Build, unit, real SQL Server integration, web-feature, frontend, browser E2E, migration-script, and dependency gates pass locally. `G-PLAT-01` and `G-PLAT-02` remain `Implemented-Unverified` because hosted CI and staged migration reconciliation have not run. AI-01 through AI-08 retain their capability-specific statuses.

Rollback: disable AI_JOB_V4_ENABLED, stop workers, preserve queued rows, and use compatibility reads. No destructive status migration occurs before reconciliation.

### P0-03 privacy

- Add consent and retention inputs to meeting capture and import.
- Add privacy controllers and UI.
- Add retention and DSAR workers.
- Mark historical meeting content UnknownSensitive and generate reconciliation report.
- Invalidate cache and vector data when source access or retention changes.

Working-tree status on 2026-07-13: implementation and local evidence are complete in [15_P0-03_Privacy_Retention_DSAR_Evidence.md](15_P0-03_Privacy_Retention_DSAR_Evidence.md). Additive migration and isolated LocalDB rehearsal, policy/consent/cloud enforcement, SQL-leased retention and DSAR work, encrypted exports, legal holds, API/UI, frontend, E2E, console, regression, and dependency gates pass locally. `G-UC-09`, `G-UC-19`, `G-AI-01`, `G-PRIV-01`, and `G-PRIV-02` remain `Implemented-Unverified` because hosted CI, staged reconciliation, target legal policy, continuous worker operation, and live-provider topology have not run.

Rollback: disable new processing, retain audit and migration markers, and return to read-only access. Do not reinterpret unknown historical data as consented.

### P0-04 cost and budget

- Add daily and monthly aggregation.
- Add warning threshold and hard-stop evaluation.
- Add organization and project policy ownership.
- Add usage and budget product surface.
- Reconcile provider attempts, cache, and mock cost.

Rollback: keep ledger writes, disable editing UI, and apply the last valid policy snapshot.

### P0-05 data and resilience

- Inventory every soft-deleted principal and dependent.
- Align optionality or matching filters and add restore tests.
- Configure Redis fail-fast behavior, bounded fallback, circuit state, and health.
- Run fault injection under authenticated load.

Rollback: feature-flag changed query behavior where possible; retain migration compatibility; revert Redis policy without changing session format.

### P0-06 entity graph and QoL

- Introduce shared entity resolver.
- Make task and meeting-source routes canonical.
- Formalize PrimaryGroupId semantics and reciprocal navigation.
- Return structured AI source references.
- Make Settings aggregate failures and distinguish device-only preferences.

Rollback: keep legacy routes as redirects and disable previews or new resolvers independently.

### P0-07 recovery and Git governance

- Add restore script and runbook for isolated target.
- Record schema and image compatibility.
- Configure protected main, required checks, review ownership, and CODEOWNERS.
- Create immutable release tags and image digests.
- Add migration ownership and release evidence template.

Rollback: use previous immutable image when schema-compatible; otherwise execute the approved compensating migration or recovery plan.

## 4. P1 design sequence

Complete locked AI wrappers before adding advanced AI. Build capability and availability data before scheduling. Enable snapshot writes before exposing rollback. Refactor large modules only with stable behavioral tests and disjoint ownership.

Scheduling implementation order:

1. capability taxonomy and user-maintained profiles;
2. capacity, availability, leave, timezone, and project calendar;
3. task effort, skills, dependencies, fixed dates, and constraints;
4. deterministic feasibility and scenario ranking;
5. visible diff and authorized confirmation;
6. AI explanation and follow-up questions;
7. telemetry, fairness review, and staged rollout.

Versioning implementation order:

1. per-entity version schema and normalized payload;
2. snapshot writes and history reads;
3. field diff and permission handling;
4. rollback preview and concurrency;
5. compensating update, audit, and recovery tests.

## 5. Feature flags

| Flag | Default before evidence | Purpose |
|---|---|---|
| AI_JOB_V4_ENABLED | Off | Route wrappers to canonical asynchronous jobs. |
| PRIVACY_V4_ENFORCED | Off for migrated reads; On for new sensitive ingestion after readiness | Enforce consent and retention. |
| AI_BUDGET_UI_ENABLED | Off | Expose policy editing after authorization and reconciliation tests. |
| CANONICAL_ENTITY_LINKS_ENABLED | Scoped | Enable resolver, routes, reciprocal links, and previews incrementally. |
| SCHEDULING_SCENARIOS_ENABLED | Off | P1 advisory scenarios only. |
| ENTITY_VERSIONING_WRITE_ENABLED | Off then shadow | Write snapshots without exposing rollback. |
| ENTITY_ROLLBACK_ENABLED | Off | Enable only after snapshot, diff, permission, and recovery evidence. |
| SEMANTIC_AI_ENABLED | Off | P2 experiment after entry gates. |

Flags are server-controlled, environment-aware, observable, and included in release evidence. A client-only flag is not a security or privacy control.

## 6. Data migration inventory

| Migration | Strategy | Reconciliation |
|---|---|---|
| AiJob and AiJobItem | Expand canonical fields; map legacy status; convert queue rows to dispatch | One job, terminal status, result, usage, and draft chain per request |
| Project SourceGroupId | Rename or compatibility-map to PrimaryGroupId | Every existing relationship listed and reciprocal navigation verified |
| Meeting privacy | Add classification, consent, retention, policy version | Existing rows reported as UnknownSensitive until reviewed |
| Budget policy | Add monthly and warning fields with explicit defaults | Effective tenant and project policy matches expected calculation |
| Soft delete | Change relationship optionality or filters only after query inventory | Active, deleted, restore, and administrative query counts compared |
| Version snapshots | Add per-entity tables and backfill version zero | Payload hash matches current authorized state |
| Capability and capacity | Add declared profile and task requirement tables | Users can inspect and correct imported defaults |

No migration stores secrets or treats seed data as production truth.

## 7. GitHub workflow

- Work in short-lived branches tied to a requirement or risk ID.
- Pull requests list affected docs, ADR, migration, feature flag, acceptance gates, and rollback.
- Required owners review identity, privacy, AI, migrations, recovery, and release changes.
- Main remains releasable; incomplete behavior stays behind a server-controlled flag.
- Release candidate tags bind commit, image digest, schema version, flags, evidence, and known issues.

## 8. Test and evidence strategy

| Layer | Purpose |
|---|---|
| Unit | Policies, status transitions, ranking, constraints, schema validation, query filters |
| Integration | Authorization, API contract, transaction, worker, idempotency, privacy, budget |
| Web feature | Cross-service behavior using application host and database |
| E2E | Canonical navigation, review and confirm, settings truth, accessibility, mobile |
| Resilience | Redis, provider, realtime, worker restart, retry, timeout, circuit behavior |
| Recovery | Backup, restore, migration compatibility, smoke, image rollback |
| Security | Dependency, secret, auth, CSRF, tenant isolation, file and content controls |

Evidence names the commit, environment, configuration class, database schema, provider mode, feature flags, timestamps, and artifact location.

## 9. Release decision

The Product Owner, engineering lead, QA, Privacy or Security owner, and DevOps owner review blocking acceptance records. A release is rejected when any Critical risk remains, a blocking P0 gate is not Verified, required evidence is stale, or rollback is not credible.

The v4.0-draft package itself is documentation evidence, not proof of implementation.
