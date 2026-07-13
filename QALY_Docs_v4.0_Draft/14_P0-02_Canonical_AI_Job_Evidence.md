# P0-02 Canonical AI Job Platform Evidence

Evidence date: 2026-07-13 (Asia/Saigon)
Baseline commit: `b9fda76c510988c46b7a3bcceb0d7209f44397aa`
Evidence state: feature-branch implementation; hosted GitHub CI and staging rollout not executed
Decision source: [ADR-003](05_Architecture_and_ADR/ADR-003_Asynchronous_AI_Jobs.md)

## 1. Verdict and claim boundary

P0-02 is implemented and verified in the local working tree across unit, API, real SQL Server integration, browser E2E, migration-script, runtime, and dependency evidence. `G-PLAT-01` and `G-PLAT-02` remain `Implemented-Unverified`, not `Verified`, until the same gates pass in hosted CI and the additive migration is reconciled in a staged target environment.

This evidence does not upgrade AI-01 through AI-08. Canonical transport and review infrastructure do not prove each capability's locked schema, grounding, privacy, and workflow acceptance. P0-03 was subsequently implemented under evidence document 15; P0-04 and P0-05 remain separate required packages.

## 2. Implemented platform

### Canonical domain and transaction boundary

- `AiJob` is the business source of truth: `src/Qaly.Domain/Entities/AiJob.cs:3`.
- Execution states are limited to queued, running, retrying, succeeded, failed, and canceled; draft states are separate in `src/Qaly.Domain/Entities/AI/AiJobStates.cs:3`.
- Delivery-only state is stored in `AiJobDispatch`; source identity, attempt evidence, and migration disposition are stored in `AiJobSource`, `AiProviderAttempt`, and `AiJobMigrationRecord`.
- Job creation requires an idempotency key, immutable request hash, schema identity, authorized/fresh sources, policy, and budget checks in `src/Qaly.Application/Services/AiWorkflowService.cs:93`.
- Job, source, and dispatch rows are committed together. A concurrent duplicate re-reads the winner and returns replay or `AI_IDEMPOTENCY_CONFLICT` rather than creating a second job.
- Draft confirmation requires idempotency and row version, rechecks source/policy state, claims the confirmation before mutation, and returns a stable replay result in `src/Qaly.Application/Services/AiWorkflowService.cs:767`.

### Worker, gateway, and lifecycle

- `AiJobWorker` is a hosted service in the modular monolith: `src/Qaly.Infrastructure/Services/AI/AiJobWorker.cs:9`.
- `AiJobDispatchStore` uses SQL Server `UPDLOCK`, `READPAST`, and `ROWLOCK` for exclusive claims, heartbeat renewal, expired-lease recovery, and retry availability: `src/Qaly.Infrastructure/Services/AI/AiJobDispatchStore.cs:31`.
- `AiJobProcessor` rechecks source access, observes cancellation before commit, validates JSON, writes at most one draft, records provider attempts, and releases or completes the lease: `src/Qaly.Infrastructure/Services/AI/AiJobProcessor.cs:21`.
- Worker pause releases work as retryable `AI_WORKER_PAUSED`; it does not misclassify an in-flight job as user-canceled.
- Provider routing uses a configured fallback chain. Compliance and budget blocks return structured errors; degraded mock output requires explicit policy and is labeled.
- Usage, audit, provider attempt, canonical job, and draft records use GUID linkage. Full aggregation and policy editing remain P0-04.

### API and UI

- Canonical job create/list/detail/result/retry/cancel routes are in `src/Qaly.Web/Controllers/AiController.cs:44`.
- Draft list/detail/edit/confirm/reject routes are in `src/Qaly.Web/Controllers/AiController.cs:94`.
- Usage, budget, and health reads are exposed at `/api/ai/usage`, `/api/ai/budget`, and `/api/ai/health`.
- All new AI mutation routes use antiforgery validation; the frontend obtains and sends `X-CSRF-TOKEN` from `src/Qaly.Web/ClientApp/utils/api-client.ts`.
- Eight locked-function wrappers enqueue canonical jobs behind `AI_JOB_V4_ENABLED`; legacy routes remain for the compatibility window.
- `AiActivityPanel.vue` provides polling, manual refresh, job status/result, cancel/retry, draft original-versus-working comparison, edit/reject/confirm, and URL deep links (`aiJob`/`aiDraft`).
- Meetily remains draft-first: regression tests prove task creation occurs only after authorized confirmation.

## 3. Additive migration and reconciliation

Migration: `src/Qaly.Infrastructure/Data/Migrations/20260711192726_P002CanonicalAiJobPlatform.cs`.

The migration:

- adds canonical fields and row versions without renaming or dropping legacy fields;
- creates `AiJobDispatches`, `AiJobSources`, `AiProviderAttempts`, and `AiJobMigrationRecords`;
- adds GUID linkage to usage and audit evidence while retaining legacy bigint columns;
- maps known canonical job and draft states;
- creates source and dispatch records for eligible canonical jobs;
- writes a migration record for every legacy `AiJobQueue` row;
- quarantines active legacy rows without a proven handler and classifies terminal rows as historical-unlinked;
- does not fabricate a canonical mapping or duplicate unknown raw payloads.

Operational scripts:

- `scripts/ai-jobs/p002-preflight.sql`
- `scripts/ai-jobs/p002-postflight.sql`
- `scripts/ai-jobs/p002-reconciliation.sql`

Generated final script: `.test-results/p002-migration-final.sql`. Inspection found all four new table creates and no `DROP TABLE` or `sp_rename` operation.

## 4. Executed evidence

| Gate | Command or evidence | Result |
|---|---|---|
| Backend build | `dotnet build Qaly_project.slnx -c Release --no-restore` | Passed; 0 warnings, 0 errors |
| Unit regression | `dotnet test tests/Qaly.UnitTests/Qaly.UnitTests.csproj -c Release --no-build` | 235 passed, 0 failed |
| SQL/API integration | `dotnet test tests/Qaly.IntegrationTests/Qaly.IntegrationTests.csproj -c Release --no-build` | 37 passed, 0 failed |
| Web feature regression | `dotnet test tests/Qaly.WebFeatureTests/Qaly.WebFeatureTests.csproj -c Release --no-build` | 9 passed, 0 failed |
| Frontend typecheck | `npm.cmd run typecheck` | Passed |
| Frontend build | `npm.cmd run build` | Passed; 3,906 modules transformed |
| AI Activity E2E | `playwright test tests/e2e/ai-activity.spec.ts --project=chromium` with local URL and credentials supplied by environment | 2 passed, 0 failed |
| EF snapshot parity | `dotnet ef migrations has-pending-model-changes ... --configuration Release --no-build` | No model changes since the last migration |
| NuGet advisory scan | `dotnet list Qaly_project.slnx package --vulnerable --include-transitive` | No vulnerable package in seven projects |
| npm production scan | `npm.cmd audit --omit=dev` | 0 vulnerabilities |
| Patch hygiene | `git diff --check` | Passed |

TRX artifacts are under `.test-results/full-regression/`. The first sandboxed integration run passed 28 tests and failed three before test logic because the sandbox could not create/access the Windows LocalDB automatic instance. After creating and starting `MSSQLLocalDB` outside the sandbox, the full real-SQL suite passed. The final run added explicit SQL coverage for exclusive claims, owner-bound heartbeat renewal, restart through lease expiry, retry/backoff, cancel-after-claim, duplicate delivery, and migration reconciliation.

Key negative and lifecycle tests:

- explicit compliance, budget, provider-unavailable, fallback, schema-repair, and labeled-degraded behavior in `tests/Qaly.UnitTests/AiGatewayEvidenceTests.cs` and `AiGatewayRouterTests.cs`;
- retry, cancel, worker-pause, one-result, and one-draft behavior in `tests/Qaly.UnitTests/AiJobProcessorTests.cs`;
- stale source hashes and private-source invisibility in `tests/Qaly.UnitTests/AiSourceGuardTests.cs`;
- confirmation replay idempotency, stale row-version rejection, and no task before confirmation in `tests/Qaly.UnitTests/MeetilyImportTests.cs`;
- real SQL lease/restart/retry/cancel/duplicate behavior in `tests/Qaly.IntegrationTests/AiJobDispatchStoreSqlServerTests.cs`;
- idempotent enqueue, list/poll/result, visibility, retry/cancel, draft edit/reject/confirm, CSRF, row version, confirmation replay, and canonical wrapper behavior in `tests/Qaly.IntegrationTests/AiJobApiTests.cs`;
- migration/backfill/classification in `tests/Qaly.IntegrationTests/AiJobMigrationSqlServerTests.cs`;
- polling, refresh/deep-link restoration, draft comparison, and explicit human confirmation in `tests/e2e/ai-activity.spec.ts`.

## 5. Runtime evidence

Local runtime: `http://127.0.0.1:5187` using SQL Server LocalDB with `AI_JOB_V4_ENABLED=true` and worker disabled for rollout safety.

- Authenticated AI Activity loaded with `Degraded: worker_disabled`, queue depth zero, and a truthful empty state.
- Desktop drawer rendered without overlap.
- Mobile viewport 390 x 844 had drawer bounds 0..390, document `scrollWidth=390`, and no horizontal overflow.
- The list pseudo-element was `none`; the prior global timeline-class collision is removed.
- Browser console error count was zero.
- The E2E browser flow separately proved queued-to-succeeded polling and draft confirmation behavior.

Redis was unavailable during this runtime check. Authenticated requests remained functional through fallback but incurred repeated multi-second delay. This is recorded under R-006 and remains P0-05 work; it is not concealed as P0-02 success.

## 6. Rollback readiness

Operational rollback is non-destructive:

1. set `AI_JOB_V4_ENABLED=false` to stop canonical wrapper ingress;
2. set the worker kill switch off and allow active leases to expire;
3. preserve queued, dispatch, attempt, audit, usage, draft, and migration rows;
4. use compatibility reads during the agreed release window;
5. reconcile row counts and dispositions with the supplied SQL scripts before re-enabling;
6. do not use migration `Down` as an operational rollback because it can destroy evidence.

The migration remains binary-compatible with the old schema surface and does not drop `AiJobQueue` or bigint legacy linkage.

## 7. Residual risks and next package

- Hosted CI, branch protection, and staging migration/reconciliation are not verified; both P0-02 platform gates remain `Implemented-Unverified`.
- Live provider credentials, cloud topology, and target-environment policy are not proven. P0-03 local privacy controls are recorded separately in evidence document 15.
- Full daily/monthly usage aggregation, policy ownership, warning thresholds, and budget editing UI remain P0-04.
- Redis fail-fast/circuit behavior remains P0-05.
- Legacy queue contraction must wait for one compatibility release plus reconciliation; no legacy table is removed in P0-02.

P0-03 was not part of this evidence run and was subsequently implemented; see [15_P0-03_Privacy_Retention_DSAR_Evidence.md](15_P0-03_Privacy_Retention_DSAR_Evidence.md). The next dependency-ordered package after P0-03 is P0-04.

## 8. Post-merge revalidation

On 2026-07-13 the P0-02 working tree was integrated onto upstream commit `761a53a47bd7ffb4560bbe836e37e83db62257e1`, which adds Task Key numbering, GitHub metadata schema/services, and chatbot changes. Conflict resolution preserved immediate provider failover when another provider is available, configured retries for the final available provider, explicit degraded-mock policy, canonical AI evidence linkage, and human-confirmed drafts.

The integrated tree passed Release build with zero warnings/errors, `269/269` unit tests, `45/45` SQL/API integration tests, `9/9` WebFeature tests, `2/2` AI Activity E2E tests, frontend typecheck/build, EF snapshot parity, dependency scans, and conflict-marker/diff hygiene. The unit coverage gate passed at `43.56%` (`9,796 / 22,487` executable lines). Hosted CI and staged rollout remain required until the feature-branch workflow completes.
