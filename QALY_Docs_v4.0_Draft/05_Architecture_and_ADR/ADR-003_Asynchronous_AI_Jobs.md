# ADR-003: Canonical Asynchronous AI Job Lifecycle

Status: Accepted; implemented and verified in the local P0-02 working tree; hosted CI and staged rollout pending

## Context

v3.2 defines queued asynchronous AI work. The pre-P0-02 baseline contained overlapping AiJob and AiJobItem models, while POST /api/ai/jobs created a DraftReady job and heuristic draft immediately. AiJobItem was configured and seeded but had no active application worker path.

Two overlapping job records can diverge in status, provider, usage, retries, and audit. Immediate execution also makes cancellation, provider retry, policy recheck, and progress reporting unreliable.

## Decision

Use one canonical AiJob business record and one dispatch record.

AiJob owns:

- tenant, project, user, job type, source type and IDs;
- immutable request hash and schema version;
- sensitivity and policy decision;
- lifecycle status, timestamps, retry counters, and error;
- selected provider attempt and usage reference;
- result and draft references.

AiJobDispatch owns queue delivery state only:

- job ID, available time, lease, delivery count, and last dispatch error.

It is not a second business job and is deleted or archived after terminal processing.

## Lifecycle

- queued to running to succeeded or failed;
- failed to retrying to running when policy permits;
- queued or running to canceled;
- succeeded result with draft to pending_review;
- pending_review to confirmed, rejected, or expired.

Terminal domain actions are idempotent. A job result never directly assigns a member, commits a deadline, or creates a task without draft confirmation.

## Policy behavior

Authorization, source visibility, consent, sensitivity, budget, and provider eligibility are checked when enqueued and rechecked before execution or confirmation where state can change.

Compliance and budget blocks return explicit terminal errors. They must not be converted to successful mock output. Mock output is allowed only in labeled demo, offline, or provider-degraded modes permitted by policy.

## Migration

1. Add canonical statuses without deleting legacy fields.
2. Map DraftReady to succeeded plus pending_review where a valid draft exists.
3. Migrate AiJobItem rows into dispatch records linked to canonical jobs or retire seed-only rows.
4. Deploy worker and read APIs behind a feature flag.
5. Dual-read during one compatibility release; do not dual-write independent status.
6. Remove legacy status paths after migration reconciliation and rollback window.

## Acceptance evidence

- Integration tests cover enqueue, poll, result, retry, cancel, duplicate delivery, worker restart, idempotency, authorization, policy block, budget block, and schema failure.
- Reconciliation proves one terminal status and one usage record per attempt.
- Rollback can disable the worker and restore legacy reads without losing queued requests.

## Implementation evidence

P0-02 implements the decision through an additive migration, canonical service and APIs, a SQL-leased hosted worker, provider-attempt evidence, separate draft review states, feature flags, reconciliation scripts, and AI Activity UI. Local unit, real SQL Server integration, browser E2E, runtime, and dependency gates are recorded in [14_P0-02_Canonical_AI_Job_Evidence.md](../14_P0-02_Canonical_AI_Job_Evidence.md).

The implementation is not a production-readiness claim. Hosted CI, staged migration reconciliation, live provider topology, P0-03 privacy controls, and P0-04 budget product controls remain required before their corresponding gates can become Verified.
