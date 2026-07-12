# P0-03 Privacy, Retention, and DSAR Evidence

Evidence date: 2026-07-13 (Asia/Saigon)
Baseline commit: `b9fda76c510988c46b7a3bcceb0d7209f44397aa`
Integrated upstream commit: `761a53a47bd7ffb4560bbe836e37e83db62257e1`
Evidence state: feature-branch implementation; hosted GitHub CI and staged target rollout not executed
Decision source: [ADR-005](05_Architecture_and_ADR/ADR-005_Meeting_Privacy_and_Retention.md)

## 1. Verdict and claim boundary

P0-03 is implemented and verified in the local working tree across domain, API, policy enforcement, durable SQL work claiming, migration rehearsal, frontend, browser E2E, regression, and dependency evidence. The related release gates remain `Implemented-Unverified`, not `Verified`, until hosted CI passes and the additive migration, policy ownership, worker topology, and live-provider configuration are reconciled in a staged target environment.

This evidence does not assert a universal legal retention period, production legal compliance, production-ready provider topology, or production backup deletion. It does not upgrade unrelated AI capabilities. `G-NFR-09` remains `Spec-only`.

## 2. Implemented privacy platform

### Policy, consent, and sensitive processing

- Privacy classes, provider classes, consent states, retention actions, DSAR states, legal-hold states, and processing modes are canonical constants in `src/Qaly.Domain/Entities/AI/PrivacyStates.cs:1`.
- Versioned `RetentionPolicy`, purpose-bound `PrivacyConsent`, durable `DataSubjectRequest`, `PrivacyRetentionAction`, and `PrivacyLegalHold` records are configured in the EF model.
- `AiComplianceService` evaluates tenant, project, classification, purpose, policy version, notice version, retention, provider class, source, and effective consent. Cloud is allowed only by the intersection of retention policy, AI budget policy, and effective provider-scoped consent: `src/Qaly.Infrastructure/Services/AI/AiComplianceService.cs:85`.
- Legacy consent values stay ineffective. Local-only input cannot be promoted to cloud, and policy blocks return structured errors rather than mock success.
- Privacy audit metadata is content-minimized and redacts transcript, prompt, token, password, payload, and content keys.

### Meeting and AI enforcement

- Meetily import and Auto Checknote accept consent, retention policy, processing mode, retention days, and notice version.
- `MeetingImportService` validates payload size, project authorization, privacy decision, retention expiry, and audit evidence before sensitive storage or provider execution: `src/Qaly.Application/Services/MeetingImportService.cs:79` and `:934`.
- Auto Checknote propagates the exact privacy context into the canonical AI request. Draft expiry is bounded by retention, and task mutation remains human-confirmed.
- `AiGateway` and `AiWorkflowService` re-evaluate structured privacy context at execution and confirmation boundaries.
- Meeting import, Auto Checknote, task creation, and task linking mutation routes require `X-CSRF-TOKEN`.

### DSAR, retention, and legal hold

- `PrivacyService` provides policy versioning/disable, self-granted and revocable consent, privacy decisions, DSAR submit/list/read/accept/reject/download, legal holds, and health: `src/Qaly.Infrastructure/Services/Privacy/PrivacyService.cs:15`.
- DSAR submit is idempotent. Acceptance performs identity verification; the worker only claims accepted requests.
- Export payloads are encrypted with ASP.NET Data Protection, time-limited, authorization-rechecked, decrypted only for download, and returned with no-store semantics.
- Delete processing anonymizes user content where ownership permits, preserves shared task integrity, revokes credentials, clears notifications, invalidates project prompt cache and vector records, and reports backup-expiry work as partial rather than claiming immediate backup deletion.
- Legal holds stop destructive processing and retain explicit evidence without placing the raw reason in ordinary audit metadata.
- `PrivacyWorkStore` uses SQL Server `UPDLOCK`, `READPAST`, and `ROWLOCK` claims, leases, expiry recovery, retry/backoff, and maximum attempts: `src/Qaly.Infrastructure/Services/Privacy/PrivacyWorkStore.cs:29`.
- `PrivacyWorker` remains inside the modular monolith and has an independent kill switch: `src/Qaly.Infrastructure/Services/Privacy/PrivacyWorker.cs:9`.

### API and UI

- Privacy policy, consent, decision, DSAR, download, legal-hold, health, and admin run-once routes are in `src/Qaly.Web/Controllers/PrivacyController.cs:11`; every mutation uses antiforgery validation.
- Settings exposes project-scoped Consent, Retention, Data requests, and Legal holds views in `src/Qaly.Web/ClientApp/components/settings/PrivacySettingsTab.vue:371`.
- The meeting screen blocks speech capture and AI checknote behind a policy and consent gate, supports local-only versus cloud-allowed mode, and binds consent to the meeting source: `src/Qaly.Web/ClientApp/pages/GroupMeetingPage.vue:1109`.
- Browser E2E found and fixed stale project selection after dashboard refresh and an invalid meeting context injection. The meeting modal now sits above the floating assistant.
- Unused HTMX CDN loading and the Google Fonts runtime import were removed. The app uses its existing system-font fallback and no longer emits those external-request console failures.

## 3. Additive migration and reconciliation

Migration: `src/Qaly.Infrastructure/Data/Migrations/20260712185807_P003PrivacyRetentionDsar.cs`.

The migration:

- adds privacy fields to existing meeting, consent, DSAR, and AI audit records;
- creates retention policy, retention action, and legal-hold tables;
- keeps old columns and binary compatibility;
- classifies existing meeting content as `unknown_sensitive` with `migration_review`;
- retains legacy consent as `legacy-unknown` and never makes it effective;
- normalizes known DSAR states and subject identity without fabricating consent;
- does not schedule retention deletion for historical meeting rows;
- contains no `DROP`, `TRUNCATE`, `DELETE`, rename, or `ALTER COLUMN` operation in the generated P0-03 upgrade script.

Operational artifacts:

- `scripts/privacy/p003-preflight.sql`
- `scripts/privacy/p003-postflight.sql`
- `scripts/privacy/p003-reconciliation.sql`
- `scripts/privacy/p003-idempotent.sql`
- `scripts/privacy/Invoke-P003MigrationRehearsal.ps1`

After integrating upstream Task Key and GitHub schema migrations, the isolated LocalDB rehearsal migrated through `20260712064020_AddGitHubIntegrationSchema`, seeded one legacy meeting, one legacy consent, and one pending DSAR, then applied P0-03. Postflight retained row counts `1/1/1`, produced zero policies/actions/holds, classified the meeting as `migration_review / unknown_sensitive / unknown`, retained consent as `granted / unknown / legacy-unknown` but ineffective, and normalized the DSAR to `submitted` with a subject and no lease. The rehearsal database was dropped in `finally`.

## 4. Executed evidence

| Gate | Command or evidence | Result |
|---|---|---|
| Backend build | `dotnet build Qaly_project.slnx -c Release --no-restore` | Passed; 0 warnings, 0 errors |
| Unit regression and coverage | `dotnet test tests/Qaly.UnitTests/Qaly.UnitTests.csproj -c Release --no-build --collect:"XPlat Code Coverage" --settings coverlet.runsettings` | 268 passed, 0 failed; line coverage 43.51% (`9,784 / 22,485`), above the unchanged 43% gate |
| SQL/API integration | `dotnet test tests/Qaly.IntegrationTests/Qaly.IntegrationTests.csproj -c Release --no-build --no-restore` | 45 passed, 0 failed |
| Web feature regression | `dotnet test tests/Qaly.WebFeatureTests/Qaly.WebFeatureTests.csproj -c Release --no-build --no-restore` | 9 passed, 0 failed |
| Frontend typecheck | `npm.cmd run typecheck` | Passed |
| Frontend production build | `npm.cmd run build` | Passed; 3,909 modules transformed |
| Privacy browser E2E | `playwright test tests/e2e/privacy-retention.spec.ts --project=chromium` | 2 passed, 0 failed |
| Browser console sweep | `playwright test tests/e2e/browser-console.spec.ts --project=chromium` | 2 passed, 0 failed across public auth and primary authenticated routes |
| AI Activity browser E2E | `playwright test tests/e2e/ai-activity.spec.ts --project=chromium` | 2 passed, 0 failed after chatbot and provider-routing integration |
| EF snapshot parity | `dotnet ef migrations has-pending-model-changes ... --configuration Release --no-build` | No model changes since the last migration |
| Migration rehearsal | `scripts/privacy/Invoke-P003MigrationRehearsal.ps1` | Passed preflight, full predecessor chain through GitHub schema, P0-03 migration, postflight, reconciliation, and cleanup |
| NuGet advisory scan | `dotnet list Qaly_project.slnx package --vulnerable --include-transitive` | No vulnerable package in seven projects |
| npm advisory scan | `npm.cmd audit --audit-level=high` | 0 vulnerabilities |

Acceptance TRX files:

- `tests/Qaly.UnitTests/TestResults/p003-unit-acceptance.trx`
- `tests/Qaly.IntegrationTests/TestResults/p003-integration-acceptance.trx`
- `tests/Qaly.WebFeatureTests/TestResults/p003-webfeatures-acceptance.trx`

Focused evidence includes:

- policy, consent, cloud-intersection, revocation, notice, and retention mismatch tests in `tests/Qaly.UnitTests/PrivacyComplianceTests.cs:11`;
- redact/delete/cache/vector and legal-hold processing in `tests/Qaly.UnitTests/PrivacyWorkProcessorTests.cs:15`;
- meeting sensitive-default and policy enforcement in `tests/Qaly.UnitTests/MeetingPrivacyEnforcementTests.cs:17`;
- policy/consent/DSAR authorization and CSRF paths in `tests/Qaly.IntegrationTests/PrivacyApiTests.cs:15`;
- real SQL migration classification in `tests/Qaly.IntegrationTests/PrivacyMigrationSqlServerTests.cs:11`;
- real SQL exclusive leases, expiry recovery, and retry backoff in `tests/Qaly.IntegrationTests/PrivacyWorkStoreSqlServerTests.cs:12`;
- meeting mutation CSRF regression in `tests/Qaly.IntegrationTests/MeetingActionItemsControllerTests.cs:195`;
- Settings policy/consent/DSAR payloads, meeting-bound consent, desktop/mobile bounds, and zero browser errors in `tests/e2e/privacy-retention.spec.ts:168`.

## 5. Runtime evidence

Local runtime: `http://127.0.0.1:5193`, Release build, SQL Server LocalDB database `QalyDb`, privacy enforcement enabled, privacy worker disabled for rollout safety.

- Authenticated Settings loaded a valid project after asynchronous dashboard refresh.
- Policy, consent, and DSAR commands sent the antiforgery header and expected privacy fields.
- The meeting gate required policy plus explicit acceptance before speech capture and bound consent to the meeting ID.
- Desktop and 390 x 844 mobile screenshots show the Settings privacy surface and meeting gate inside viewport bounds without modal-command overlap.
- Browser console, page-error, and same-origin request-failure sweep reported zero problems after unused external CDN dependencies were removed.
- Redis remained unavailable in the local runtime. This causes known degraded latency under R-006 and is not counted as P0-03 success.

Screenshot artifacts are under `test-results/privacy-retention-*`.

## 6. Rollback readiness

Operational rollback is non-destructive:

1. set `PRIVACY_V4_ENFORCED=false` to stop strict sensitive-ingestion enforcement during incident response;
2. set `PRIVACY_V4_WORKER_ENABLED=false` and allow active leases to expire;
3. if required, set `PRIVACY_V4_ENABLED=false` to return the privacy product surface to compatibility mode;
4. preserve policies, consent, DSAR, retention actions, legal holds, audit evidence, and migration markers;
5. run preflight/postflight/reconciliation before re-enabling;
6. do not use migration `Down` as an operational rollback because it can destroy privacy evidence.

Feature flags default off in `src/Qaly.Web/appsettings.json:63`.

## 7. Residual risks and next package

- Hosted CI, branch protection, staged migration reconciliation, continuous worker topology, and live cloud-provider controls are not verified. P0-03 gates remain `Implemented-Unverified`.
- Tenant legal policy, consent notice approval, jurisdiction, retention allowlists, and data-processing agreements require Product/Privacy Owner approval per target environment.
- Backup expiry is reported as pending; P0-07 owns restore and backup lifecycle evidence.
- Redis fail-fast and bounded degradation remain P0-05.
- Daily/monthly cost aggregation, warning thresholds, budget editing, and target budget ownership remain P0-04.
- Full tenant-isolation and production load/fault evidence must run in hosted CI/staging even though local authorization and real-SQL concurrency tests pass.

The next dependency-ordered package is P0-04. The long-lived v4.0 goal remains active.
