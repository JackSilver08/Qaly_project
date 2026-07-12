# Qaly AI-Native and Endpoint Contract v4.0 Draft

## 1. Scope and current status

This contract covers the eight locked AI functions and the shared AI platform. It is a target contract. Current implementation is 2 Verified, 5 Partial, and 1 Missing across AI-01 through AI-08.

Current strengths include backend-only provider calls, provider adapters, caching, usage records, compliance checks, structured-output validation, offline fallback, meeting extraction, and explainable baseline assignment insight.

Current blocking gaps include immediate DraftReady jobs, overlapping AiJob and AiJobItem roles, missing job status and control APIs, incomplete privacy and budget surfaces, policy blocks returned as mock output, missing acceptance-checklist generation, and incomplete source URLs.

## 2. Mandatory principles

1. Frontend code never calls an AI provider directly.
2. Every request passes authentication, source authorization, privacy, provider eligibility, budget, schema, usage, and audit controls.
3. Long-running work is asynchronous.
4. Every source is identified by canonical type and ID; copied text alone is not sufficient when a source entity exists.
5. AI output is untrusted until schema validation succeeds.
6. Domain mutations are drafts until an authorized user confirms a visible editable payload.
7. Compliance and budget blocks are explicit errors, never successful mock content.
8. Offline and provider-degraded mock results are visibly labeled and cannot be confirmed as real extraction without review.
9. Raw hidden chat and private-task content are excluded unless explicitly authorized and required.
10. Every status and cost claim is attributable to one job and provider attempt.

## 3. Canonical job model

AiJob is the business source of truth.

| Field group | Required values |
|---|---|
| Identity | job_id, tenant_id, project_id, requested_by_user_id, job_type |
| Source | source_type, source_ids, source_version, source_hash |
| Contract | schema_id, schema_version, options_json, idempotency_key |
| Policy | sensitive, consent_id, retention_policy_id, cloud_eligible, budget_policy_id |
| Lifecycle | status, progress, created_at, available_at, started_at, finished_at, canceled_at |
| Retry | attempt_count, max_attempts, next_retry_at, last_error_code |
| Result | result_json, result_hash, cache_key, cache_hit, draft_ids |
| Provider | selected_provider, selected_model, provider_request_id |
| Usage | input_tokens, output_tokens, estimated_cost, actual_cost, usage_ledger_id |

AiJobDispatch stores queue delivery only: dispatch_id, job_id, lease owner, lease expiry, delivery count, available time, and last dispatch error.

Provider attempts may be stored separately when retry evidence is required. An attempt never replaces the canonical job status.

## 4. Lifecycle

| From | Event | To | Rule |
|---|---|---|---|
| none | accepted | queued | Authorization, source, policy, and request validation pass. |
| queued | worker lease | running | Lease is exclusive and time-bounded. |
| running | valid result | succeeded | Schema and output policy pass. |
| running | retryable failure | retrying | Attempts remain below maximum. |
| retrying | available | queued | Backoff time reached. |
| running | terminal failure | failed | Error code and safe detail recorded. |
| queued or running | authorized cancel | canceled | Worker observes cancellation before committing result. |
| succeeded | draft created | pending_review | Draft stores original output and editable working payload. |
| pending_review | authorized confirm | confirmed | Normal domain policy and concurrency checks pass. |
| pending_review | reject | rejected | Reason and reviewer are audited. |
| pending_review | retention expiry | expired | No domain mutation occurs. |

Terminal transitions are idempotent. Duplicate queue delivery cannot create duplicate drafts, tasks, assignments, or usage records.

## 5. HTTP conventions

- Browser authentication uses the same-origin session cookie.
- State-changing routes require CSRF protection.
- X-Request-Id is accepted or generated and returned.
- Idempotency-Key is required for job creation and draft confirmation.
- X-Project-Id may be supplied for diagnostics but never replaces route or body authorization.
- Timestamps use UTC ISO 8601.
- Money uses decimal USD fields and a recorded pricing-table version.
- API responses use the existing Qaly result envelope while preserving the error codes below.

## 6. Error contract

| Code | HTTP | Retryable | Meaning |
|---|---:|---|---|
| AI_JOB_NOT_FOUND | 404 | No | Job is absent or not visible. |
| AI_PERMISSION_DENIED | 403 | No | Source, project, draft, or action is forbidden. |
| AI_SENSITIVE_BLOCKED | 403 | No | Sensitive input has no eligible provider or override. |
| AI_CONSENT_REQUIRED | 403 | No | Required consent is missing, revoked, or expired. |
| AI_BUDGET_EXCEEDED | 402 or 429 | Policy | Daily or monthly hard stop is active. |
| AI_BUDGET_WARNING | 200 metadata | No | Usage crossed warning threshold but processing is allowed. |
| AI_PROVIDER_UNAVAILABLE | 503 | Yes | No eligible provider completed the request. |
| AI_RATE_LIMITED | 429 | Yes | Gateway or provider rate limit. |
| AI_PAYLOAD_TOO_LARGE | 413 | No | Input must be reduced or server-chunked. |
| AI_SCHEMA_INVALID | 422 | Conditional | Output failed the locked schema after allowed repair attempts. |
| AI_SOURCE_STALE | 409 | No | Source changed after job creation and requires review or rerun. |
| AI_DRAFT_ALREADY_CONFIRMED | 409 | No | Confirmation is idempotently complete or conflicts. |
| AI_JOB_NOT_CANCELABLE | 409 | No | Job is already terminal. |
| AI_CACHE_MISS | 404 | No | Cache-only request has no valid entry. |

Mock fallback is not an error code substitute. A blocked request remains blocked.

## 7. Core job endpoints

### POST /api/ai/jobs

Required request fields:

- job_type;
- project_id when project-scoped;
- source_type and source_ids;
- source_version or source_hash;
- schema_id and schema_version;
- sensitive, consent_id, and retention policy where applicable;
- cache_mode;
- maximum estimated cost;
- language and function options.

Returns 202 with job_id, queued status, estimated cost, poll URL, result URL, and request ID.

### GET /api/ai/jobs/{jobId}

Returns lifecycle status, progress, safe provider metadata, timestamps, retry count, warning metadata, and estimated or actual usage. It never returns hidden source content.

### GET /api/ai/jobs/{jobId}/result

Available only after succeeded. Returns schema ID, validated result, draft IDs, usage ledger ID, cache status, source references, and policy metadata safe for the caller.

### POST /api/ai/jobs/{jobId}/retry

Allowed for failed or canceled jobs when policy, source, budget, and retry count remain valid. It preserves source and schema identity. Provider override requires delegated authority.

### POST /api/ai/jobs/{jobId}/cancel

Allowed for queued or running jobs. Cancellation is best-effort for an in-flight provider call but must prevent domain confirmation from a canceled result.

## 8. Draft review endpoints

| Method and route | Behavior |
|---|---|
| GET /api/ai/drafts | Filter visible drafts by project, type, status, source, and owner. |
| GET /api/ai/drafts/{draftId} | Return original output, editable payload, diff, sources, confidence, and warnings. |
| PATCH /api/ai/drafts/{draftId} | Validate and save an editable working payload with optimistic concurrency. |
| POST /api/ai/drafts/{draftId}/confirm | Execute an authorized domain command using edited payload and idempotency key. |
| POST /api/ai/drafts/{draftId}/reject | Record reason and close the draft without mutation. |

Confirmation actions are create_tasks, link_task, assign_task, create_subtasks, save_checklist, and save_report. Each action uses the normal domain permission and business rules.

## 9. Usage, budget, and health endpoints

| Method and route | Required behavior |
|---|---|
| GET /api/ai/usage | Daily or monthly aggregation by tenant, project, function, provider, model, status, and cache. |
| GET /api/ai/budget | Effective tenant and project policy with current usage and remaining amount. |
| PUT /api/ai/budget | Update daily, monthly, warning, hard-stop, and sensitive-cloud policy with audit. |
| GET /api/ai/health | Gateway and eligible-provider health, latency class, queue depth, oldest job age, and degraded reason. |

Budget evaluation uses both daily and monthly limits. Warning does not stop processing. Hard stop returns AI_BUDGET_EXCEEDED and does not generate mock content.

## 10. Function wrappers

Wrappers create the same canonical job rather than executing a parallel path.

| AI ID | Route | Schema |
|---|---|---|
| AI-01 | POST /api/meetings/import/meetily | meetily_import.v4 |
| AI-02 | POST /api/ai/meetings/{meetingId}/extract-actions | meeting_action_extract.v4 |
| AI-03 | POST /api/ai/groups/{groupId}/summaries | chat_summary.v4 |
| AI-04 | POST /api/ai/task-drafts/from-source | task_draft.v4 |
| AI-05 | POST /api/ai/tasks/{taskId}/recommend-assignees | assignee_recommendation.v4 |
| AI-06 | POST /api/ai/tasks/{taskId}/breakdown | task_breakdown.v4 |
| AI-07 | POST /api/ai/tasks/{taskId}/acceptance-checklist | acceptance_checklist.v4 |
| AI-08 | POST /api/ai/projects/{projectId}/progress-summary and sprint variant | progress_summary.v4 |

## 11. Provider routing

| Condition | Result |
|---|---|
| Sensitive and cloud not allowed | Eligible approved local provider or AI_SENSITIVE_BLOCKED. |
| Hard budget stop | AI_BUDGET_EXCEEDED. |
| Valid cache and policy allows | Return validated cached result and record cache usage. |
| Preferred provider healthy and eligible | Execute with timeout and schema repair limit. |
| Preferred provider retryable failure | Try the next eligible provider when policy permits. |
| All eligible providers unavailable | Labeled cache or demo mock only when requested policy permits; otherwise 503. |
| Offline demo mode | Labeled deterministic mock with provider OfflineMock and non-production flag. |

Provider selection, model, timeout, fallback chain, and pricing are configuration, not frontend input. Provider hints are advisory and permission-filtered.

## 12. Source references

Every result source reference contains type, canonical ID, label, canonical URL, evidence excerpt or metric, source version, timestamp where relevant, and confidence where meaningful.

The entity resolver authorizes the URL when generated and authorization is checked again when opened. Source labels without IDs are allowed only for legacy results and are marked unresolved.

## 13. Audit and retention

Audit events cover request, policy decision, provider attempt, schema validation, retry, cancel, result, draft edit, reject, confirm, cost, and data deletion. Audit avoids secrets and minimizes transcript content.

Job, prompt, result, and draft retention follow data classification and tenant policy. Deleting source data invalidates or redacts derived data according to the privacy contract while preserving minimum legal or security audit metadata.

## 14. Compatibility and migration

- Keep current wrapper routes during one compatibility period.
- Route wrappers to canonical jobs behind AI_JOB_V4_ENABLED.
- Map legacy DraftReady to succeeded plus pending_review.
- Reconcile AiJobItem with dispatch records and remove independent business status.
- Version schemas and accept old result reads without allowing old confirmation after expiry.
- Expose compatibility metrics before legacy removal.

## 15. Blocking acceptance

P0 requires worker restart, duplicate delivery, idempotency, cancel, retry, policy, budget, source permission, stale source, schema failure, cache, provider fallback, draft review, confirmation, audit, usage reconciliation, and privacy tests. No AI function is release-green solely because a provider returns text.
