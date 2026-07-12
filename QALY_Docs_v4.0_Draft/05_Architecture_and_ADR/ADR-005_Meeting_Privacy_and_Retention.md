# ADR-005: Sensitive Meeting Data, Consent, and Retention

Status: Accepted; implemented and verified in the local P0-03 working tree; hosted CI and staged rollout pending

## Context

Transcripts may contain personal, confidential, or project-sensitive data. The Phase A baseline had entities and partial compliance checks but lacked a complete consent, retention, export, and delete workflow.

## Decision

Meeting transcript, recording metadata, summary, action evidence, and derived AI prompts are sensitive by default.

Processing requires:

- identified tenant, project, user, purpose, and source;
- explicit consent where required by product policy;
- a configured retention policy;
- a cloud-processing decision before any provider request;
- immutable audit of consent, policy, provider, purpose, and result;
- data-subject export and deletion workflow with legal-hold handling.

No universal legal retention period is asserted. Tenant policy selects an allowed value. Missing policy blocks new sensitive ingestion rather than applying an undocumented infinite retention.

## Data minimization

- Send only the transcript range needed for the requested function.
- Prefer aggregated metrics over raw chat for progress reporting.
- Exclude unrelated participants and hidden tasks.
- Store prompt and response content only when required for review or audit; otherwise store hashes and metadata.
- Separate operational logs from sensitive content.

## Provider behavior

Sensitive input may use a cloud provider only when policy and effective consent allow it. Otherwise use an approved local provider or return AI_SENSITIVE_BLOCKED. A mock result must not imply that processing occurred.

## Existing-data migration

Existing transcripts receive classification UnknownSensitive and a migration retention state. They are not silently considered consented. Administrators receive a reconciliation report and choose retain, re-consent, export, or delete.

## Acceptance evidence

- Integration and E2E tests cover grant, revoke, sensitive default, local-only, cloud block, retention expiry, export, delete, legal hold, and tenant isolation.
- Audit proves purpose, actor, policy version, provider, and affected source IDs.

## Implementation evidence

P0-03 implements this decision through versioned policies and consent, strict provider eligibility, sensitive meeting gates, durable SQL-leased retention and DSAR work, encrypted exports, legal holds, additive historical classification, feature flags, APIs, Settings UI, and browser E2E. Local unit, real SQL Server integration, migration rehearsal, runtime, and rollback evidence are recorded in [15_P0-03_Privacy_Retention_DSAR_Evidence.md](../15_P0-03_Privacy_Retention_DSAR_Evidence.md).

This is not a production legal-compliance claim. Hosted CI, staged reconciliation, live provider topology, tenant legal policy, backup expiry, and production worker operations remain required before target-environment gates can become Verified.
