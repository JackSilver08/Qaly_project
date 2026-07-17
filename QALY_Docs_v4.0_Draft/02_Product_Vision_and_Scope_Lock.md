# Qaly v4.0 Draft Product Vision and Scope Lock

## 1. Product definition

Qaly is a project collaboration and operations platform that connects work planning, team communication, meetings, knowledge, analytics, and AI-assisted decisions around shared project entities.

Qaly does not aim to reproduce every Jira, Microsoft Project, chat, or video-conference feature. Its differentiator is a coherent workflow in which a discussion or meeting can become an auditable task, a project risk can be traced to source data, and AI reduces coordination effort without making unreviewed business decisions.

## 2. Product outcomes

1. Reduce duplicate entry between chat, meeting notes, task boards, and reports.
2. Make every operational insight traceable to a project, task, member, message, meeting, Wiki page, notification, or audit event.
3. Help teams plan and prioritize using reliable metrics and explainable recommendations.
4. Keep users in control of assignments, deadlines, task creation, privacy, and rollback.
5. Support safe multi-person delivery through a canonical GitHub repository and reproducible release evidence.

## 3. Primary personas

| Persona | Primary needs | Required controls |
|---|---|---|
| Organization Admin | Workspace policy, users, cost, privacy, recovery | Tenant isolation, budget, audit, DSAR, backup and restore |
| Project Manager or Lead | Scope, assignments, schedule, risk, reporting | Project RBAC, review, scenario planning, version restore |
| Project Member | Tasks, comments, files, chat, meetings, Wiki | Fast navigation, mentions, evidence, privacy visibility |
| QA or Reviewer | Acceptance evidence, audit, reproducibility | Traceability, immutable evidence, explicit status |
| DevOps or Maintainer | Deployment, observability, security, rollback | Required checks, health, versioned release, recovery runbook |

## 4. Product principles

- Entity-centered: every action and insight resolves to canonical IDs.
- Human-in-the-loop: AI outputs are drafts or recommendations until confirmed.
- Privacy-by-default: transcript data is sensitive unless a policy explicitly says otherwise.
- Explainable automation: rules, metrics, constraints, and source evidence remain visible.
- Modular monolith first: improve boundaries and ownership before distributing the system.
- Evidence over claims: release status comes from executable gates and dated evidence.

## 5. P0 release scope

P0 retains all 20 locked v3.2 use cases:

| ID | Capability | P0 target |
|---|---|---|
| P0-UC-01 | Authentication | Login, logout, current user, session renewal and revocation using cookie-session architecture. |
| P0-UC-02 | Project management | Create and update project; add and remove members. |
| P0-UC-03 | Project RBAC | Deny access across project and tenant boundaries. |
| P0-UC-04 | Manual task creation | Persist required task fields and assignment. |
| P0-UC-05 | Kanban status | Validate transitions, concurrency, audit, and notification. |
| P0-UC-06 | Comment and evidence | Persist text, link, upload, evidence state, and review. |
| P0-UC-07 | Project collaboration room | Link a project to its primary group and realtime messages. |
| P0-UC-08 | Mention and assignment notification | Persist and navigate permission-aware notifications. |
| P0-UC-09 | Meetily import | Validate, deduplicate, consent, classify, and retain transcript data. |
| P0-UC-10 | AI meeting extraction | Produce reviewable decisions and action-item drafts. |
| P0-UC-11 | Confirm AI draft | Create or link tasks only after authorized confirmation. |
| P0-UC-12 | Chat summary | Summarize a selected message range with cache and sources. |
| P0-UC-13 | Task draft from chat | Convert selected messages into an editable task draft. |
| P0-UC-14 | Assignee recommendation | Rank authorized candidates with reasons; PM confirms. |
| P0-UC-15 | Breakdown and checklist | Produce editable subtask and acceptance-checklist drafts. |
| P0-UC-16 | Project or sprint summary | Combine SQL metrics and grounded narrative. |
| P0-UC-17 | AI usage and cost | Show daily and monthly usage, warning, budget, and hard-stop state. |
| P0-UC-18 | AI fallback | Support offline or provider-degraded demonstration without hiding policy errors. |
| P0-UC-19 | Privacy and data requests | Consent, export, delete, status, and audit paths. |
| P0-UC-20 | Backup and restore | Execute and evidence backup, restore, migration, and smoke validation. |

## 6. P0 AI function lock

Only these AI functions are release-blocking P0 capabilities:

| ID | Capability | Mutation rule |
|---|---|---|
| AI-01 | Meetily import and manual sync | No AI mutation; import requires validation and consent. |
| AI-02 | Meeting keyword, decision, action, and deadline extraction | Save as draft only. |
| AI-03 | Chat thread summary | Read-only output with source range. |
| AI-04 | Task draft from chat, meeting, or manual source | Draft only; user edits and confirms. |
| AI-05 | Assignee recommendation | Recommendation only; authorized user confirms. |
| AI-06 | Task breakdown | Draft subtasks only. |
| AI-07 | Acceptance checklist generation | Draft checklist only. |
| AI-08 | Project or sprint progress summary | Read-only grounded report. |

## 7. Additional P0 platform closure

The v4.0 release gate also requires:

- one asynchronous AI job and queue model;
- explicit budget and compliance errors;
- daily and monthly AI budget endpoints and UI;
- sensitive-by-default meeting consent and retention;
- no unreviewed AI writes;
- clean frontend typecheck and blocking dependency policy;
- resolved EF soft-delete relationship warnings;
- fail-fast Redis degraded behavior and health status;
- tested restore workflow;
- protected main, required checks, ownership, release tags, and rollback evidence;
- canonical task, group, meeting, Wiki, notification, audit, and AI-source links.

## 8. P1 scope

- Complete entity hover previews and contextual actions.
- Persist user skills, proficiency, weekly capacity, availability, leave, and timezone.
- Persist task skill requirements, effort, dependencies, constraints, and project calendars.
- Add deterministic scheduling scenarios with AI explanations and human approval.
- Add Wiki, Project settings, and Task field version snapshots, diff, and rollback.
- Complete the v4 AI wrappers, draft review surface, and source evidence.
- Refactor oversized pages and services into owned use-case modules.
- Improve bundle size, visual regression, accessibility, and error-state consistency.

## 9. P2 scope

- Cross-project portfolio optimization.
- Optional semantic search and richer RAG after P0 AI functions are green.
- Multiple group links per project if validated by real workflows.
- Advanced resource leveling, forecasting, and what-if simulation.
- External API authentication profile if a non-browser client is approved.

## 10. Explicitly out of scope

- Autonomous assignment, deadline, task creation, deletion, or project mutation.
- Generic event sourcing for every entity.
- A microservice migration without measured scaling or ownership need.
- Replacing Meetily core; Qaly consumes an export or supported connector.
- Full Jira, Microsoft Project, Slack, Teams, or video-platform parity.
- Production readiness based only on a demo or subjective percentage.

## 11. Scope freeze rules

1. No new P0 AI function before AI-01 through AI-08 are Verified.
2. No cloud processing of sensitive input without effective policy and consent.
3. No AI-created assignee, deadline, or domain mutation before explicit confirmation.
4. No semantic or portfolio expansion while P0 privacy, cost, job, or restore gates are open.
5. No generic rollback platform before the three P1 entity-versioning cases are accepted.
6. No microservice split before module ownership, performance evidence, and operational benefit are documented.
7. Every scope addition requires traceability, acceptance, risk, migration, and rollback updates.

## 12. Release gates

A v4.0 production candidate requires:

- every blocking P0 acceptance record to be Verified;
- no unresolved Critical risk;
- no unresolved High dependency vulnerability without a dated, approved exception and compensating control;
- frontend typecheck, backend build, unit, integration, web-feature, and required E2E checks passing;
- privacy, budget, degraded-mode, restore, and rollback evidence from the release candidate;
- an immutable release tag and image digest;
- an approved known-issues list and rollback owner.

## 13. Scope change process

The proposer records the requirement IDs, business reason, priority, affected entities, API and data changes, privacy impact, test plan, rollout, and rollback. Product, engineering, QA, and security owners then approve or reject the change through an ADR or scope-change record.
