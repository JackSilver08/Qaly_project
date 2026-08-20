# Qaly Software Requirements Specification v4.0 Draft

## 1. Document control

Status: Draft
Evidence baseline: 2026-07-11 at commit b9fda76c510988c46b7a3bcceb0d7209f44397aa
Normative scope: 02_Product_Vision_and_Scope_Lock.md

The status attached to each requirement describes implementation evidence at the baseline. Target behavior remains mandatory for its assigned release even when the current status is Partial or Missing.

## 2. System context

Qaly is a same-origin web application implemented as a modular monolith. The web client calls Qaly backend APIs. The backend owns authorization, persistence, realtime events, AI-provider access, compliance, usage, and audit. Browser code must never call an AI provider directly.

Primary entities are Organization, User, Project, ProjectMember, Task, Group, Message, Meeting, MeetingActionItem, WikiPage, Notification, AuditEvent, AiJob, AiDraft, PrivacyConsent, DataSubjectRequest, and VersionSnapshot.

## 3. Roles and authorization

| Role | Baseline authority |
|---|---|
| Organization Admin | Organization policy, users, privacy, budget, recovery, and all authorized projects. |
| Project Manager or Lead | Project settings, members, assignments, schedule, AI confirmation, and reports. |
| Member | Authorized project work, collaboration, meetings, comments, evidence, and draft review where permitted. |
| Reviewer or QA | Evidence review and acceptance access granted by project policy. |
| DevOps or Maintainer | Deployment, health, backup, restore, release, and rollback; no implicit business-data access. |

All authorization is evaluated server-side. UI hiding is not an authorization control.

## 4. P0 functional requirements

### QLY-FR-AUTH-001 Authentication and session lifecycle

- Source: P0-UC-01. Current status: Partial.
- Actor: User.
- Preconditions: Active account; same-origin browser client; session store available or explicitly degraded.
- Flow: Submit credentials; establish secure cookie session; retrieve current user; renew sliding session; logout or revoke sessions.
- Permission: A user may inspect and revoke only their sessions unless an Admin policy grants broader authority.
- Errors: Invalid credentials, disabled account, expired session, unavailable session dependency, or CSRF failure return explicit non-success results.
- Acceptance: Login, current-user, renewal, logout, and revoke paths pass integration tests; cookies are Secure in production, HttpOnly, SameSite, and not exposed to JavaScript.
- NFR: QLY-NFR-SEC-001, QLY-NFR-PERF-001, QLY-NFR-REL-001.

### QLY-FR-PRJ-001 Project and membership management

- Source: P0-UC-02. Current status: Verified.
- Actor: Organization Admin or authorized Project Manager.
- Preconditions: Authenticated user belongs to the organization.
- Flow: Create or update project; add, update, or remove project members; view current membership.
- Permission: Only authorized roles mutate project or membership; members can view only permitted fields.
- Errors: Duplicate code, invalid date range, unknown user, cross-tenant request, or insufficient permission.
- Acceptance: Project and member changes persist, emit audit events, and are isolated by organization and project.
- NFR: QLY-NFR-SEC-001, QLY-NFR-DATA-001.

### QLY-FR-RBAC-001 Project and tenant isolation

- Source: P0-UC-03. Current status: Verified.
- Actor: All authenticated users.
- Preconditions: A request references an organization, project, group, task, meeting, Wiki page, or source entity.
- Flow: Resolve tenant and project membership; apply entity-specific policy; return only authorized data.
- Permission: Deny by default. Private tasks and sensitive meeting data require additional policy checks.
- Errors: Use 403 when identity is known but forbidden; use 404 where existence must not be disclosed.
- Acceptance: Negative integration tests cover cross-project, cross-tenant, private-task, Wiki, meeting, AI-source, and attachment access.
- NFR: QLY-NFR-SEC-001, QLY-NFR-PRIV-001.

### QLY-FR-TASK-001 Manual task lifecycle

- Source: P0-UC-04. Current status: Verified.
- Actor: Project Manager or Member with task-create permission.
- Preconditions: Authorized active project.
- Flow: Create task with title, description, priority, due date, estimate, reporter, and optional assignee; edit allowed fields; view canonical detail.
- Permission: Project policy controls create and edit; private tasks restrict detail and related records.
- Errors: Invalid fields, invalid project, unauthorized assignee, or concurrency conflict.
- Acceptance: Required fields persist; assignee belongs to the project; audit and notifications are emitted; canonical URL opens the same task.
- NFR: QLY-NFR-DATA-001, QLY-NFR-UX-001.

### QLY-FR-TASK-002 Kanban transition and concurrency

- Source: P0-UC-05. Current status: Verified.
- Actor: Authorized Project Manager or Member.
- Preconditions: Existing task and current row version.
- Flow: Request status transition or Kanban move; validate workflow and completion policy; save; audit; notify.
- Permission: Project workflow may restrict transitions to privileged roles.
- Errors: Invalid transition, missing completion evidence, invalid row version, or concurrency conflict.
- Acceptance: Every accepted status change records old and new status; stale writers receive 409 and current state; drag and API paths share rules.
- NFR: QLY-NFR-DATA-001, QLY-NFR-REL-001.

### QLY-FR-TASK-003 Comments, attachments, and evidence

- Source: P0-UC-06. Current status: Implemented-Unverified.
- Actor: Authorized project member or evidence reviewer.
- Preconditions: Visible task; attachment policy permits content and size.
- Flow: Add comment, mentions, link or upload; mark attachment as evidence; review, approve, or reject evidence.
- Permission: Private-task policy applies to every child record; evidence review requires project permission.
- Errors: Forbidden task, blocked file type, size limit, duplicate content, malware-policy failure, or invalid evidence transition.
- Acceptance: Text, link, upload, mention, evidence state, review reason, audit, and permission paths pass integration and E2E tests.
- NFR: QLY-NFR-SEC-001, QLY-NFR-DATA-001, QLY-NFR-UX-001.

### QLY-FR-COLLAB-001 Primary project collaboration group

- Source: P0-UC-07. Current status: Partial.
- Actor: Project Manager and project member.
- Preconditions: Project has zero or one primary group; user is authorized for both entities.
- Flow: Link or create primary group; open group from project; send and receive messages; open linked projects from group.
- Permission: Linking requires project-management and group-management authority; membership mismatch is resolved explicitly, never silently widened.
- Errors: Conflicting primary group, unauthorized group, dissolved group, or realtime transport failure.
- Acceptance: Project and group expose reciprocal canonical links; realtime and persisted messages converge; degraded polling is explicit.
- NFR: QLY-NFR-REL-001, QLY-NFR-UX-001.

### QLY-FR-NOTIF-001 Mention and assignment notifications

- Source: P0-UC-08. Current status: Verified.
- Actor: Member receiving or causing a notification.
- Preconditions: Authorized source action and valid recipient.
- Flow: Create idempotent notification; publish realtime or push where enabled; display unread state; navigate to authorized source.
- Permission: Source authorization is rechecked when opened; notification text must not leak hidden entity data.
- Errors: Duplicate idempotency key, deleted source, revoked access, or unavailable realtime channel.
- Acceptance: Assignment, comment mention, group mention, important status, read, read-all, and canonical target behavior pass tests.
- NFR: QLY-NFR-SEC-001, QLY-NFR-UX-001.

### QLY-FR-MEET-001 Meetily import and meeting-data intake

- Source: P0-UC-09 and AI-01. Current status: Partial.
- Actor: Authorized Project Manager or Member.
- Preconditions: Project access; consent for personal or sensitive transcript data; supported import schema.
- Flow: Validate metadata and source hash; classify sensitivity; record consent and retention; deduplicate; store transcript and summary; suggest extraction.
- Permission: Project meeting-data policy and cloud-processing policy apply.
- Errors: Missing consent, duplicate source, invalid schema, oversized payload, forbidden project, or unsupported retention.
- Acceptance: Import, dedupe, consent, sensitive default, retention, audit, payload limit, and unauthorized tests pass.
- NFR: QLY-NFR-PRIV-001, QLY-NFR-DATA-001.

### QLY-FR-AI-002 Meeting extraction

- Source: P0-UC-10 and AI-02. Current status: Verified for the use case; platform lifecycle remains Partial.
- Actor: Authorized meeting participant or project member.
- Preconditions: Visible imported or captured meeting and effective processing policy.
- Flow: Create asynchronous extraction job; extract keywords, decisions, action items, owners, evidence, and deadline suggestions; validate schema; store reviewable draft.
- Permission: Access to the meeting and project is checked at enqueue, execution, result, and review.
- Errors: Policy block, budget block, payload limit, provider unavailable, schema invalid, canceled job, or stale source.
- Acceptance: Job, schema, draft, source evidence, permission, failure, and manual-review confidence paths pass tests.
- NFR: QLY-NFR-AI-001, QLY-NFR-PRIV-001, QLY-NFR-REL-001.

### QLY-FR-AI-004 AI task draft and confirmation

- Source: P0-UC-11, P0-UC-13, and AI-04. Current status: Verified for meeting confirmation and Partial for chat or manual source.
- Actor: Authorized Project Manager or Member.
- Preconditions: Authorized source IDs or manual text and a succeeded AI job.
- Flow: Build immutable source reference; create editable task draft; edit or reject; confirm to create or link a task; audit original and edited payload.
- Permission: Task creation and assignment permissions are checked at confirmation, not inherited from job creation.
- Errors: Missing source, already confirmed draft, stale source, invalid assignee, forbidden action, or concurrency conflict.
- Acceptance: Meeting, selected-chat, and manual-source drafts support edit, reject, confirm, idempotency, audit, and no-write-before-confirm tests.
- NFR: QLY-NFR-AI-001, QLY-NFR-DATA-001.

### QLY-FR-AI-003 Chat range summary

- Source: P0-UC-12 and AI-03. Current status: Partial.
- Actor: Authorized group or project member.
- Preconditions: Visible room and ordered message range.
- Flow: Select start and end messages; enqueue summary; validate result; return narrative, decisions, actions, unresolved items, and source references; cache by immutable range hash.
- Permission: Every source message remains visible to the requester at result time.
- Errors: Invalid range, deleted message, forbidden source, payload limit, cache miss in cache-only mode, or provider failure.
- Acceptance: Range, cache hit, cache invalidation, source URL, privacy, and unauthorized tests pass.
- NFR: QLY-NFR-AI-001, QLY-NFR-PRIV-001.

### QLY-FR-AI-005 Assignee recommendation

- Source: P0-UC-14 and AI-05. Current status: Verified for baseline heuristic.
- Actor: Project Manager or Lead.
- Preconditions: Visible task and authorized project-member candidates.
- Flow: Derive candidates and deterministic workload signals; rank with reasons and confidence; display conflicts; user selects or rejects recommendation.
- Permission: Recommendation does not grant assignment rights; final assignment uses normal task policy.
- Errors: No candidate, incomplete capacity data, private signals, stale workload, or forbidden task.
- Acceptance: Candidate isolation, ranking determinism, explanation, no automatic assignment, and human-confirmation tests pass.
- NFR: QLY-NFR-AI-001, QLY-NFR-PRIV-001.

### QLY-FR-AI-006 Task breakdown

- Source: P0-UC-15 and AI-06. Current status: Partial.
- Actor: Authorized Project Manager or Member.
- Preconditions: Visible parent task with sufficient description.
- Flow: Enqueue breakdown; validate structured subtasks; edit, reorder, reject, or confirm selected drafts.
- Permission: Confirm uses task-create and parent-edit policy.
- Errors: Empty source, invalid schema, circular dependency, duplicate draft, or forbidden parent.
- Acceptance: Draft-only generation, schema, edit, selective confirm, dependency, audit, and no-write-before-confirm tests pass.
- NFR: QLY-NFR-AI-001, QLY-NFR-DATA-001.

### QLY-FR-AI-007 Acceptance checklist generation

- Source: P0-UC-15 and AI-07. Current status: Missing.
- Actor: Authorized Project Manager, Member, QA, or Reviewer.
- Preconditions: Visible task and acceptance context.
- Flow: Enqueue generation; validate testable checklist items; edit and confirm into the task checklist.
- Permission: Confirm requires task-edit permission; generation alone does not mutate the task.
- Errors: Insufficient context, invalid schema, duplicate item, forbidden task, or already confirmed draft.
- Acceptance: Testable wording, edit, reject, confirm, audit, and no-write-before-confirm tests pass.
- NFR: QLY-NFR-AI-001, QLY-NFR-UX-001.

### QLY-FR-AI-008 Grounded project or sprint summary

- Source: P0-UC-16 and AI-08. Current status: Partial.
- Actor: Project Manager or authorized Member.
- Preconditions: Visible project or sprint and a defined reporting range.
- Flow: Backend computes SQL metrics; AI receives aggregated authorized facts; output includes progress, risk, blockers, confidence, and source links.
- Permission: Raw hidden chat and private-task content are excluded unless explicitly authorized and necessary.
- Errors: Invalid period, no data, stale metrics, budget block, policy block, or provider failure.
- Acceptance: Metric reconciliation, project and sprint routes, source links, deterministic fallback, and permission tests pass.
- NFR: QLY-NFR-AI-001, QLY-NFR-PERF-001.

### QLY-FR-AI-PLAT-002 AI usage and budget control

- Source: P0-UC-17. Current status: Missing at product surface.
- Actor: Organization Admin and authorized Project Manager.
- Preconditions: AI usage ledger and applicable organization or project budget policy.
- Flow: View daily and monthly usage; configure warning threshold and hard stop; receive explicit warning or block; inspect provider and function breakdown.
- Permission: Organization Admin controls tenant policy; project override requires delegated authority.
- Errors: Invalid budget, stale aggregation, forbidden policy, or ledger unavailable.
- Acceptance: Daily, monthly, warning, hard-stop, explicit error, audit, and UI tests pass.
- NFR: QLY-NFR-AI-001, QLY-NFR-OPS-001.

### QLY-FR-AI-PLAT-003 Provider fallback and degraded mode

- Source: P0-UC-18. Current status: Verified for offline mock.
- Actor: End user, QA, or Maintainer.
- Preconditions: Provider policy and health are known.
- Flow: Route to configured eligible provider; retry allowed failures; use cache or labeled mock only for provider degradation or explicit demo mode.
- Permission: Sensitive and budget policy are evaluated before provider selection.
- Errors: Compliance and budget blocks return explicit errors and must never become successful mock content.
- Acceptance: Offline, cache, provider failure, policy block, budget block, schema failure, and labeling tests pass.
- NFR: QLY-NFR-AI-001, QLY-NFR-REL-001.

### QLY-FR-PRIV-001 Consent and data-subject requests

- Source: P0-UC-19. Current status: Missing at runtime surface.
- Actor: User, Organization Admin, or authorized privacy operator.
- Preconditions: Known data subject and tenant policy.
- Flow: Grant or revoke scoped consent; submit export or delete request; validate identity and legal hold; process asynchronously; deliver result or reason; audit every step.
- Permission: Least privilege; privacy operators cannot gain unrelated project access.
- Errors: Identity mismatch, legal hold, unsupported deletion, expired export, or processing failure.
- Acceptance: Consent, revoke, export, delete, retention, legal-hold exception, tenant isolation, and audit tests pass.
- NFR: QLY-NFR-PRIV-001, QLY-NFR-SEC-001.

### QLY-FR-OPS-001 Backup, restore, and release recovery

- Source: P0-UC-20. Current status: Partial.
- Actor: DevOps or Maintainer.
- Preconditions: Authorized infrastructure access, versioned application image, database credentials outside the repository, and recovery target.
- Flow: Create checksum backup; record metadata; restore into an isolated target; apply compatible migrations; run smoke checks; record result; promote or roll back.
- Permission: Production recovery requires two-person approval or equivalent controlled workflow.
- Errors: Missing tool, checksum failure, incompatible schema, failed smoke, insufficient storage, or secret failure.
- Acceptance: Backup and restore runbook succeeds from a clean target and records RPO, RTO, image digest, schema version, and smoke evidence.
- NFR: QLY-NFR-REC-001, QLY-NFR-OPS-001.

## 5. Shared AI platform requirement

### QLY-FR-AI-PLAT-001 Asynchronous AI job and draft lifecycle

- Current status: Partial.
- One canonical job model owns request, source, policy decision, provider attempt, usage, result, and terminal status.
- One queue or outbox model owns dispatch and retry; it must not become a competing business job record.
- Lifecycle: queued, running, succeeded, failed, retrying, canceled; a succeeded draft may be pending_review, confirmed, rejected, or expired.
- Required APIs: create, status, result, retry, cancel, list drafts, get draft, edit draft, confirm, reject, usage, budget, and health.
- Acceptance: Worker restart, idempotency, duplicate delivery, cancellation, retry limit, policy recheck, source authorization, schema validation, and audit tests pass.

## 6. P1 functional requirements

### QLY-FR-ENTITY-001 Canonical entity graph

Every supported entity has a canonical URL, permission-aware preview, contextual actions, source references, and stable back behavior. Notification, search, audit, and AI output use the same resolver.

### QLY-FR-SCHED-001 Capability and constraint scheduling

Persist skills, proficiency, availability, capacity, timezone, leave, task effort, skill requirements, dependencies, deadlines, and project calendars. A deterministic engine creates feasible scenarios. AI explains tradeoffs. No scenario mutates assignments or dates until an authorized user confirms a visible diff.

### QLY-FR-VERS-001 Entity-specific version and rollback

Wiki content, Project settings, and configured Task fields create immutable snapshots. Authorized users can view diff, preview rollback impact, and issue a compensating update with optimistic concurrency and audit. Audit records themselves are never rolled back.

## 7. Non-functional requirements

| ID | Requirement |
|---|---|
| QLY-NFR-PERF-001 | Normal authenticated JSON APIs target p95 below 500 ms excluding AI execution, upload, export, and recovery; AI enqueue remains below 500 ms and execution is asynchronous. |
| QLY-NFR-SEC-001 | Deny-by-default authorization, secure cookies, CSRF protection, secret isolation, dependency gates, input validation, and tenant isolation are blocking. |
| QLY-NFR-PRIV-001 | Data minimization, sensitive-by-default meeting data, purpose-bound consent, configurable retention, cloud policy, DSAR, and immutable privacy audit are required. |
| QLY-NFR-REL-001 | Redis, AI provider, realtime, and vector dependencies expose explicit healthy, degraded, or unavailable states; fallback must be fail-fast and labeled. |
| QLY-NFR-DATA-001 | Foreign keys, query filters, idempotency, transactions, concurrency, migrations, and audit preserve consistency without hidden orphan behavior. |
| QLY-NFR-AI-001 | All AI requests pass backend policy, budget, schema, usage, source authorization, human-review, and audit controls. |
| QLY-NFR-UX-001 | Canonical navigation, accessibility, loading, empty, error, offline, undo, and permission states are consistent on desktop and mobile. |
| QLY-NFR-OPS-001 | Health, structured logs, metrics, traces, required checks, release tags, image digests, runbooks, and ownership support repeatable operation. |
| QLY-NFR-REC-001 | Backup and restore are tested against declared RPO and RTO targets; application and database rollback compatibility is documented. |
| QLY-NFR-MAINT-001 | Module ownership, bounded services and components, contract tests, and architecture checks limit coupling and regression risk. |

## 8. Assumptions and open operational values

- Draft recovery targets are RPO 24 hours and RTO 2 hours until the infrastructure owner approves stricter values.
- Default transcript retention is an environment or tenant policy value; no universal legal period is asserted here.
- A future external API authentication profile requires a separate ADR.
- Branch protection and production topology require verification through the actual GitHub and hosting environments.
