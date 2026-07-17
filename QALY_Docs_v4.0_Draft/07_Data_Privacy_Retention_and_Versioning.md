# Qaly Data, Privacy, Retention, and Versioning v4.0 Draft

Implementation note: the P0 privacy, retention, and DSAR subset is implemented and verified in the local working tree under [P0-03 evidence](15_P0-03_Privacy_Retention_DSAR_Evidence.md). Hosted CI, staged target policy, production worker topology, and backup expiry remain unverified. P1 entity versioning remains target behavior.

## 1. Purpose

This document defines target controls for sensitive collaboration data, consent, retention, data-subject requests, immutable audit, entity snapshots, concurrency, and rollback.

Current implementation contains privacy and data-request entities, compliance checks, audit, soft delete, task RowVersion, import undo, and project restore. Complete privacy APIs, retention processing, and version snapshots are not yet verified.

## 2. Data classification

| Class | Examples | Default handling |
|---|---|---|
| Public | Explicitly published project description | Normal authorization and integrity controls. |
| Internal | Standard task title, project status, non-sensitive Wiki | Tenant and project authorization; normal retention. |
| Confidential | Private tasks, files, member workload, audit details | Least privilege, restricted logs, controlled export. |
| Sensitive Collaboration | Transcript, recording metadata, message excerpts, AI prompts and outputs | Sensitive by default, purpose-bound processing, consent and retention policy. |
| Secret | Password, session ticket, API key, signing key | Never stored in documents, prompts, logs, or client storage; secret manager only. |

Classification propagates to derived drafts, summaries, embeddings, cache entries, exports, snapshots, and backups unless a documented transformation lowers sensitivity.

## 3. Data ownership and canonical identity

Every protected record resolves to tenant ID and, where applicable, project ID, source entity type, source entity ID, creator or subject, classification, retention policy, and legal-hold state.

Canonical entity IDs are used in audit, AI source references, notifications, exports, deletion plans, and version snapshots. Free-text labels are not sufficient identity.

## 4. Consent model

PrivacyConsent records:

- tenant, project, user or subject;
- consent type and processing purpose;
- source scope and provider class;
- policy and notice version;
- granted, revoked, expired, or denied status;
- granted and revoked timestamps;
- retention policy reference;
- actor, request ID, and minimal evidence metadata.

Consent is specific, purpose-bound, revocable, and evaluated at processing time. Consent does not replace project authorization or legal basis. Revocation blocks new processing and starts the configured deletion or retention-review workflow.

## 5. Meeting ingestion policy

1. Transcript and meeting data start as Sensitive Collaboration.
2. The UI displays purpose, provider class, retention selection, and consent state before capture or import.
3. Import rejects missing required consent and unsupported retention.
4. Source hash supports dedupe but does not replace a source ID or consent record.
5. Payload limits are enforced before provider processing.
6. Participant metadata is minimized and normalized to known users only when authorized.
7. A local-only policy cannot silently fall back to cloud.

## 6. Retention

RetentionPolicy defines class, purpose, allowed durations, default duration, expiry action, legal-hold behavior, and approval owner.

The product does not assert one universal legal period. A missing sensitive-data policy blocks new ingestion. Draft values require infrastructure and Product Owner confirmation.

| Data | Draft expiry action |
|---|---|
| Raw transcript | Delete or redact content; retain minimal source and audit metadata. |
| Meeting summary and action evidence | Retain only while linked work or policy requires; reclassify where justified. |
| AI prompt and response | Delete content on policy expiry; retain usage and safe audit metadata. |
| AI cache and embeddings | Invalidate and delete when source expires or access is revoked. |
| Draft | Expire unconfirmed draft and prevent confirmation. |
| Export artifact | Short-lived encrypted object with explicit expiry. |
| Version snapshot | Follow entity retention and privacy class. |
| Backup | Follow recovery retention; deletion completes through backup-expiry policy rather than silent mutation. |

A scheduled worker reports pending, completed, failed, and legally held expiry actions. Failures are retried and visible to an operator.

## 7. Data-subject request lifecycle

| State | Meaning |
|---|---|
| submitted | Request received with minimal identifying information. |
| identity_verification | Identity and authority are being validated. |
| accepted | Scope and deadline are established. |
| collecting | Authorized live data, snapshots, derived AI data, and references are collected. |
| review_required | Legal hold, shared data, or deletion exception requires review. |
| completed | Export delivered securely or deletion actions completed. |
| partially_completed | Some data is retained with explicit reason and policy. |
| rejected | Request is invalid or unauthorized with safe explanation. |
| failed | Operational failure requires retry or escalation. |

Exports are machine-readable and human-readable, encrypted in transit and at rest, time-limited, and audited. Download authorization is rechecked.

Deletion uses a plan of affected live data, snapshots, caches, embeddings, drafts, attachments, and future backup expiry. Shared project records are anonymized or retained according to policy rather than corrupting other users' work.

## 8. Cloud-processing policy

Cloud eligibility is the intersection of tenant policy, project policy, data classification, effective consent, provider capability, regional or contractual limits, and current user purpose.

If cloud is not eligible, Qaly uses an approved local provider only when available and suitable. Otherwise it returns AI_SENSITIVE_BLOCKED. A mock response is not processing and cannot conceal the block.

## 9. Audit

Privacy audit is append-only and records actor, subject, purpose, policy version, source IDs, classification, provider class, consent decision, request ID, timestamp, outcome, and safe failure code.

Audit does not store passwords, API keys, session tickets, full transcript content, or unbounded prompt bodies. Audit retention and access are stricter than normal project content.

## 10. Versioning model

Versioning is P1 and entity-specific.

| Entity | Snapshot payload | Trigger |
|---|---|---|
| WikiPage | Title, normalized content, parent or navigation metadata | Create, material edit, rollback |
| ProjectSettings | Workflow states, evidence requirement, permissions, schedule settings | Accepted settings update, rollback |
| Task | Approved business fields and relationships, excluding immutable audit | Accepted edit, assignment or date change, rollback |

VersionSnapshot contains snapshot ID, entity type and ID, monotonically increasing version, schema version, normalized payload, payload hash, actor, timestamp, reason, base version, and correlation ID.

Large files use content-addressed references rather than copying binary data into snapshots.

## 11. Diff and rollback

The rollback UI displays field-level changes, affected dependencies, permissions, irreversible side effects, and current row version.

Rollback creates a new normal update from historical state. It never deletes versions or rewrites audit. It validates current business rules, ownership, references, and optimistic concurrency before commit.

If the requested historical state references deleted or unauthorized data, Qaly blocks the affected field or requires an explicit safe substitute. Partial rollback must be visible and audited.

## 12. Concurrency and consistency

- Task and versioned settings use optimistic concurrency tokens.
- Draft edit and confirmation use draft version plus idempotency key.
- Snapshot and domain update commit in one transaction.
- Outbox events are emitted from the same transaction.
- Global query filters must not silently hide required principals or create orphan interpretation.
- Administrative recovery can use explicit IgnoreQueryFilters only through reviewed services and tests.

## 13. Migration

1. Add classification and policy references without changing current access.
2. Mark existing meeting content UnknownSensitive.
3. Generate a reconciliation report for missing consent and retention.
4. Introduce privacy APIs and workers behind feature flags.
5. Add version tables per entity; backfill version zero from current state with Migration actor.
6. Enable snapshot writes before enabling rollback.
7. Validate export and delete across live data, snapshots, cache, vector storage, and backups.

## 14. Blocking evidence

P0 privacy requires tests for consent grant and revoke, sensitive default, cloud block, local-only path, retention expiry, DSAR export and delete, legal hold, shared data, tenant isolation, audit redaction, cache invalidation, and worker retry.

P1 versioning requires tests for snapshot creation, schema migration, diff, permission, stale concurrency, rollback, partial conflict, irreversible warning, retention, export, and recovery.
