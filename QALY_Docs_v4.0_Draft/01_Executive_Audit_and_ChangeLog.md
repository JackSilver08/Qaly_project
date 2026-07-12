# Qaly v4.0 Draft Executive Audit and Change Log

Evidence date: 2026-07-13
Evidence commit: b9fda76c510988c46b7a3bcceb0d7209f44397aa

Working-tree supersession: P0-01, P0-02, and P0-03 local evidence is recorded in documents 13 through 15. Hosted CI and staged target rollout remain pending. Sections that describe missing CI, canonical AI, or privacy runtime preserve the Phase A baseline and are superseded only for those named packages.

## 1. Executive verdict

Qaly is moving in the correct product direction. The current system addresses the original problem of fragmented work across chat, spreadsheets, email, and task tools more completely than the original ProjectHub concept.

The product is no longer accurately described as a lightweight Jira replacement. It is becoming an integrated collaboration platform in which projects, tasks, groups, meetings, knowledge, analytics, and AI share operational context.

The main weakness is sequencing, not product intent. Feature breadth expanded before all locked v3.2 release foundations were completed. Canonical AI jobs and privacy/DSAR are now implemented and verified locally under P0-02 and P0-03, but hosted rollout remains unverified. AI usage and budget surfaces, Redis and data integrity, restore evidence, Git governance, and several cross-module navigation contracts remain incomplete.

The recommended response is to re-baseline as v4.0, retain the modular monolith, close P0 platform risks, and then improve the integrated workflow. A microservice rewrite is not justified.

## 2. Baseline evolution

### 2.1 Original ProjectHub

The original scope focused on:

- Admin, Manager, and Member roles;
- project and task management;
- labels, private tasks, and task status;
- comments, votes, attachments, and notifications;
- Wiki content;
- audit and search;
- a simple ASP.NET application and relational database;
- Git branches and pull-request collaboration.

AI, meeting transcript processing, realtime group collaboration, resource scheduling, and complete object rollback were not core requirements.

### 2.2 v3.2 locked baseline

v3.2 added a ten-week P0 contract with 20 use cases and 8 AI functions. It introduced Meetily import, AI drafts, human confirmation, provider fallback, cost control, privacy, backup and restore, and an asynchronous AI job contract.

The v3.2 package closed gaps at specification level. Its acceptance records were ready for implementation and UAT; they were not proof that the runtime already met every requirement.

### 2.3 Current implementation

The repository now contains:

- a .NET 10 modular monolith split into Domain, Application, Infrastructure, and Web projects;
- a Vue 3 and Vite frontend;
- SQL Server, Redis, SignalR, optional Qdrant, AI providers, and container workflows;
- project, task, Kanban, Gantt, dependency, sprint, workload, time, and attention workflows;
- group chat, invitations, attachments, reactions, polls, and meetings;
- meeting transcript, AI checknote, action-item extraction, and task linking;
- Analyst and Erumi workflows backed by project data;
- import preview, undo, soft delete, archive restore, audit, and optimistic task concurrency;
- CI for frontend, backend, tests, security, coverage, E2E, and container build.

## 3. Verified baseline snapshot

For the 20 P0 use cases:

| Status | Count | IDs |
|---|---:|---|
| Verified | 9 | P0-UC-02, 03, 04, 05, 08, 10, 11, 14, 18 |
| Implemented-Unverified | 1 | P0-UC-06 |
| Partial | 8 | P0-UC-01, 07, 09, 12, 13, 15, 16, 20 |
| Missing | 2 | P0-UC-17, 19 |

For the 8 AI functions:

| Status | Count | IDs |
|---|---:|---|
| Verified | 2 | AI-02, AI-05 |
| Partial | 5 | AI-01, AI-03, AI-04, AI-06, AI-08 |
| Missing | 1 | AI-07 |

These counts are not a production-readiness percentage. They describe traceability status at the evidence commit.

## 4. Improvements accepted into v4.0

| Improvement | Decision | Reason |
|---|---|---|
| Clean modular-monolith architecture | Keep | Better ownership and testability than the original single-project concept. |
| SQL Server, Redis, SignalR, and container support | Keep | Supports the current collaboration and operational model. |
| Group chat and meeting workspace | Keep | Directly reduces context fragmentation. |
| Meeting action item to task mapping | Keep | Provides one of the strongest end-to-end product flows. |
| Gantt, dependency, sprint, workload, and attention views | Keep | Improves planning and daily operational control. |
| Analyst and contextual AI | Keep and improve | Useful when answers are grounded and link to source entities. |
| Import preview and undo | Keep | Reduces migration risk and user effort. |
| Soft delete and optimistic concurrency | Keep and harden | Useful foundations, but they do not replace version history. |
| CI, coverage gates, E2E workflow, and container build | Keep and repair | Good release foundation; current typecheck and security gates must be green. |

## 5. Scope drift and scope creep

### Accepted scope drift

- Moving from a Jira-lite tool to integrated project collaboration.
- Introducing group and meeting modules.
- Moving from a single application project to a layered modular monolith.
- Replacing the documented PostgreSQL direction with the implemented SQL Server stack.
- Using same-origin cookie authentication for the web application.

These changes require ADRs and updated contracts rather than being treated as defects.

### Scope creep requiring sequencing control

- Additional AI risk, priority, Analyst, group-project drafting, and semantic capabilities appeared before all eight locked AI functions were green.
- Group communication features expanded while Project-to-Group ownership and navigation remained incomplete.
- Advanced scheduling expectations appeared before a skills, capacity, availability, and calendar data model existed.
- Readiness percentages were published without a stable traceability denominator.

## 6. Material implementation gaps

### AI platform

- POST /api/ai/jobs creates an AiJob and draft immediately with status DraftReady.
- The configured AiJobItem queue entity has no active application worker path.
- Status, result, retry, cancel, usage, budget, provider-health, draft-list, edit, and reject routes from v3.2 are incomplete.
- AiJob and AiJobItem overlap and can drift into two sources of truth.
- Budget or compliance blocks can return mock output instead of a structured policy failure.

### Privacy and cost

- Phase A found only PrivacyConsent and DataSubjectRequest entities without a complete privacy API or UI. P0-03 now supersedes this implementation finding locally; see evidence document 15.
- Phase A found incomplete meeting consent and retention inputs. P0-03 now enforces the complete local contract behind feature flags; hosted target policy remains unverified.
- The cost ledger and daily hard stop exist, but monthly budget, warning threshold, usage API, and user-facing view are absent.

### Integrated UX

- Task detail routes exist, but several task-opening paths keep only the project ID in the URL.
- Analyst source references may be labels without entity URLs.
- Group-to-Project creation exists, but linked projects are not consistently discoverable from the group.
- Notification and audit records do not consistently provide permission-aware entity navigation.
- Saved task views, bulk actions, import undo, and archived-project restore are strong QoL patterns that should be applied consistently.

### Data and operations

- Generic soft-delete filters produce required-navigation model warnings.
- Redis fallback is functional but not fail-fast when Redis is unavailable.
- Backup creation exists, but restore and recovery evidence are missing.
- No release tags or CODEOWNERS file were found; local source cannot prove branch protection.
- Several frontend pages and application services exceed two thousand lines, increasing regression and ownership risk.

## 7. Current quality evidence

| Check | Result | Interpretation |
|---|---|---|
| Backend Release build | Passed with NU1903 warnings | Buildable, but dependency gate is not clean. |
| Frontend production build | Passed | Bundling succeeds. |
| Frontend typecheck | Failed at App.vue:508 | CI frontend gate is currently blocking. |
| Unit tests | 226 of 226 passed | Strong focused regression evidence. |
| Integration tests | 24 of 24 passed | Core HTTP and database paths have evidence. |
| Web-feature tests | 9 of 9 passed | Selected web flows have evidence. |
| npm production audit | 3 High, 2 Moderate | Dependency remediation or explicit exception is required. |
| NuGet vulnerable package scan | Microsoft.OpenApi 2.0.0 High | Upgrade the transitive path before release. |
| Local UI inspection | Completed with in-memory DB and offline AI | Useful UX evidence, not production-provider evidence. |

## 8. Change impact summary

| Area | Change required | Compatibility approach |
|---|---|---|
| Authentication | Replace token language with cookie-session contract | ADR documents current behavior; API clients remain an explicit future decision. |
| AI jobs | Migrate to one job and queue lifecycle | Introduce worker and compatibility mapping before retiring legacy statuses. |
| Privacy | Add consent, retention, export, and delete paths | Sensitive-by-default for new meeting data; migrate existing records with an Unknown state. |
| Group and Project | Formalize primary-group relationship | Reuse SourceGroupId through a named migration or compatibility adapter. |
| Navigation | Add canonical entity routes and source URLs | Preserve old project URLs and redirect to canonical targets. |
| Scheduling | Add capability and constraint data | Advisory-only feature flag until data quality and solver tests pass. |
| Versioning | Add entity-specific snapshots | Keep audit logs immutable; rollback issues compensating domain commands. |
| Operations | Add restore, fail-fast Redis, tags, and release evidence | Roll out behind health gates with documented rollback. |

## 9. Production statement

Qaly is demonstrable and has meaningful regression coverage. It is not verified production-ready because hosted privacy and AI rollout, cost controls, resilience, recovery, Git governance, and remaining blocking P0 gates are open.

No future status report may replace those gates with a subjective readiness percentage.
