# Qaly AI-native Module Coverage and Increment Plan

**Audit date:** 2026-07-26 (Asia/Saigon)
**Audit baseline:** branch `codex/merge-main-ai-week1`; HEAD `a1367b2ca3b8d2439cefd60a8c8d73f88f06a5f0` (`Merge origin/main while preserving AI and Week 1 flows`)
**Remote relation:** `HEAD == origin/main` tại thời điểm chụp baseline; working tree sạch trước audit.
**Change boundary:** lượt này chỉ tạo file này. Không merge, reset, rebase, push, migration, build frontend, source edit, test edit hoặc sửa tài liệu v4 hiện hữu.

**Current plan amendment baseline (2026-08-01, Asia/Saigon):** branch `main`; HEAD `8ec2e926d23f022da76d35f4a95db3fc024fe9aa`. Working tree trước amendment có sẵn 5 ảnh modified dưới `src/Qaly.Web/ClientApp`; đây là thay đổi của người dùng và không thuộc amendment. Lượt amendment chỉ sửa file plan này, không chạy build/test và không thay đổi source/generated bundle.

## 1. Goal statement và định nghĩa coverage 100%

Goal đang hoạt động:

> Audit 100% bề mặt module/page/card của Qaly để xác định AI-native capability hiện có, capability thiếu và integration gap; tạo một execution plan duy nhất, tự chọn một Primary Slice và tối đa một Stretch Slice đủ nhỏ để implement sâu end-to-end trong một lượt quota tiếp theo.

Trong báo cáo này, “100% coverage” chỉ có nghĩa là toàn bộ inventory tìm thấy đã có disposition; không có nghĩa là 100% chức năng đã được implement. Một capability chỉ được tính là AI native khi đồng thời có entrypoint đúng ngữ cảnh, job/decision cụ thể, input được authorize, output có schema, lifecycle trung thực, grounding, human confirmation cho mutation, canonical platform/privacy/budget/audit/usage, read-back và verification lặp lại được.

Mẫu số audit được khóa như sau:

- 24/24 route records trong router.
- 16/16 page SFC.
- 80/80 embedded tab/card/panel/modal/drawer/flow đáng kể.
- 104/104 `SURF-ID` có đúng một status và rationale.
- 58/58 controller routes thuộc AI và các controller dashboard/group/meeting liên quan.
- 35/35 logical `AI-CAP-ID`.
- 8/8 canonical wrapper job types; 8/8 JSON schema files; 7/7 draft-type families.
- 26/26 file trong `QALY_Docs_v4.0_Draft` được đối chiếu bằng source-first reconciliation.

## 2. Executive verdict

**Coverage gate: PASS. Product completeness: NOT PASS.**

Source hiện tại có canonical AI platform khá đầy đủ, nhưng chưa có capability AI-native nào của module đạt toàn bộ 10 điều kiện ở đầu audit mà không còn verification hoặc integration gap. Các điểm quan trọng nhất:

1. AI-08 project/sprint progress summary có wrapper, job type và validator nhưng không có frontend caller; prompt không được tạo từ snapshot SQL authoritative; project/sprint source hash hiện không bao gồm task state; output không có metric-to-source reconciliation.
2. UI `AiActivityPanel` gửi `create_subtasks` và `save_checklist`, trong khi `AiWorkflowService.ConfirmDraftAsync` chỉ chấp nhận `reject`, `execute_action`, `create_tasks`. AI-06/AI-07 vì vậy không thể confirm end-to-end.
3. `TaskItem` không có parent/subtask relation và không có persisted acceptance-checklist model. AI-06/AI-07 cần prerequisite dữ liệu/domain; không phù hợp slice 1–2 ngày không migration.
4. Tám file schema có `$id` v3.2, trong khi wrapper runtime yêu cầu `*.v4`; validator là nhánh C# theo substring chứ chưa load/validate JSON Schema tương ứng.
5. Project planner, dashboard strategy, meeting checknote và một phần group AI vẫn đi qua synchronous/legacy route song song với canonical platform.
6. `/api/ai/usage`, `/api/ai/budget` và `/api/ai/health` có backend, nhưng chưa có product surface. Toggle “weekly AI report” ở Settings chỉ lưu `localStorage`.
7. Project Workload và Gantt component tồn tại nhưng `tabs` không có id `capacity`/`gantt`; Project Activity nhận `projectId` nhưng gọi global recent-activity endpoint.

Disposition của 104 surfaces: 67 `NO_AI_JUSTIFIED`, 15 `PRESENT_PARTIAL`, 7 `MISSING_HIGH_VALUE`, 7 `GENERIC_CHAT_ONLY`, 5 `FRONTEND_ONLY`, 2 `LEGACY_OR_DUPLICATE`, 1 `BACKEND_ONLY`.

**Primary Slice:** AI-08 Project Progress Summary, canonical và grounded trong Project Stats.
**Stretch Slice:** AI-08 Sprint Progress Summary trong Demo Map, chỉ làm sau khi Primary xanh toàn bộ gate.

## 3. Page/Module/Card Surface Universe

### 3.1 Route và page universe — 24/24

| SURF-ID | Route / page | Role và entity context | Action / data source | AI hiện có và disposition |
|---|---|---|---|---|
| SURF-001 | `/` → `/dashboard` | Authenticated user; none | Redirect từ router | `NO_AI_JUSTIFIED` — navigation deterministic. |
| SURF-002 | `/dashboard` — `DashboardPage.vue` | Member/manager/admin; visible workspace projects/tasks | Workspace metrics, risk, activity | `PRESENT_PARTIAL` — generic “Hỏi AI” và synchronous strategy card, chưa canonical/read-back/grounded sources. |
| SURF-003 | `/profile` — `ProfilePage.vue` | Current user | Read identity/profile | `NO_AI_JUSTIFIED` — deterministic account data tốt hơn. |
| SURF-004 | `/projects` — `ProjectsPage.vue` | Project-capable user; project collection | List/create/edit/import/project planner | `PRESENT_PARTIAL` — planner có review UI nhưng legacy generate/create và mutation không qua canonical draft confirmation. |
| SURF-005 | `/projects/archived` — `ArchivedProjectsPage.vue` | Admin/owner; archive/storage | Restore/delete/deduplicate/storage | `LEGACY_OR_DUPLICATE` — “AI deduplicator” thực tế là deterministic duplicate API; không tính AI native. |
| SURF-006 | `/projects/:projectId` — `ProjectDetailPage.vue` | Project member/manager; project | Stats, tasks, timeline, wiki, members | `PRESENT_PARTIAL` — delay resolution và assignment insight có caller nhưng contract/lifecycle chưa đủ. |
| SURF-007 | `/projects/:projectId/tasks/:taskId` | Authorized task viewer; task/project | Deep-link task drawer | `PRESENT_PARTIAL` — assignment insight native card; breakdown/checklist entrypoint thiếu. |
| SURF-008 | `/projects/:projectId/wiki/:wikiId` — `WikiDetailPage.vue` | Authorized wiki reader; wiki/project | Read document and TOC | `MISSING_HIGH_VALUE` — có thể grounded summary/draft task từ wiki; hiện không có caller. |
| SURF-009 | malformed project-task route | Any | Truthful route error | `NO_AI_JUSTIFIED`. |
| SURF-010 | malformed project route | Any | Truthful route error | `NO_AI_JUSTIFIED`. |
| SURF-011 | `/tasks` — `TasksPage.vue` | User; visible assigned/reported tasks | Global task hub and drawer | `MISSING_HIGH_VALUE` — chưa có native task AI actions; backend wrappers AI-05/06/07 không được gọi ở đây. |
| SURF-012 | `/teams` — `TeamsPage.vue` | Group member/admin/owner; work group | Chat, member, poll, meeting, project, AI tabs | `PRESENT_PARTIAL` — canonical và legacy group AI chạy song song. |
| SURF-013 | `/analytics` — `AnalyticsPage.vue` | Authenticated user; optional project | Full-screen Erumi chat | `GENERIC_CHAT_ONLY` — rich chat shell không phải module-specific analytics capability. |
| SURF-014 | `/admin/users` — `AdminUsersPage.vue` | System Admin; users | User/session/role/project access | `NO_AI_JUSTIFIED` — security mutations phải deterministic và explicit. |
| SURF-015 | `/organizations/users` — `OrganizationUsersPage.vue` | Org owner/admin/moderator scope; org members | Invite/role/remove | `NO_AI_JUSTIFIED` — deterministic authorization workflow. |
| SURF-016 | `/admin/moderators` — `ModeratorAssignmentsPage.vue` | System Admin; delegated scopes | Grant/revoke scoped capability | `NO_AI_JUSTIFIED` — high-risk permission mutation. |
| SURF-017 | `/groups` — `TeamsPage.vue` | Group member; group list | Select/create/chat | `PRESENT_PARTIAL` — shares group AI panel gaps. |
| SURF-018 | `/groups/:groupId` — `TeamsPage.vue` | Group member/admin/owner; group | Group native tabs | `PRESENT_PARTIAL` — AI-03/04 caller exists but range/source/editor evidence partial. |
| SURF-019 | `/groups/:groupId/meeting` — `GroupMeetingPage.vue` | Group participant/manager; meeting | Live meeting, transcript, checknote | `PRESENT_PARTIAL` — privacy-aware AI checknote exists but uses legacy synchronous path. |
| SURF-020 | `/groups/:groupId/polls` — `GroupPollPage.vue` | Group member; polls | Create/vote/read/close | `NO_AI_JUSTIFIED` — vote arithmetic and close policy deterministic. |
| SURF-021 | `/groups/:groupId/polls/:pollId` | Group member; poll | Poll detail/result | `NO_AI_JUSTIFIED`. |
| SURF-022 | malformed group route | Any | Truthful route error | `NO_AI_JUSTIFIED`. |
| SURF-023 | `/settings` — `SettingsPage.vue` | User/project manager/org admin | Settings/privacy/logs/API keys | `FRONTEND_ONLY` — weekly AI report chỉ localStorage; usage/budget product card absent. |
| SURF-024 | catch-all route | Any | 404 truth state | `NO_AI_JUSTIFIED`. |

Page-file closure: `AdminUsersPage`, `AnalyticsPage`, `ArchivedProjectsPage`, `DashboardPage`, `GroupMeetingPage`, `GroupPollPage`, `ModeratorAssignmentsPage`, `OrganizationUsersPage`, `ProfilePage`, `ProjectDetailPage`, `ProjectsPage`, `RouteErrorPage`, `SettingsPage`, `TasksPage`, `TeamsPage`, `WikiDetailPage` = **16/16**.

### 3.2 Embedded tabs, cards, panels và flows — 80/80

| SURF-ID | Module / card / flow | Role; entity; primary action / data source | Status và rationale |
|---|---|---|---|
| SURF-025 | App shell navigation/header | Auth user; route/session; navigate/account action | `NO_AI_JUSTIFIED`. |
| SURF-026 | Global search modal | Auth user; loaded project/task/member collections; filter/open/create | `NO_AI_JUSTIFIED` — current exact/filter search is deterministic; semantic backend remains orphan separately. |
| SURF-027 | Notification popover | Auth user; notification feed; read/dismiss/open | `NO_AI_JUSTIFIED`. |
| SURF-028 | Floating assistant | Auth user; current route prompt | `GENERIC_CHAT_ONLY`. |
| SURF-029 | Erumi composer/model/history/source drawers | Auth user; optional project/chat; ask/upload/open refs | `GENERIC_CHAT_ONLY` — useful transport, not module capability contract. |
| SURF-030 | AI Activity jobs/drafts/health drawer | Project user; canonical jobs/drafts; poll/retry/cancel/edit/confirm | `PRESENT_PARTIAL` — lifecycle/read-back có; renderer generic và confirm-action mismatch cho AI-06/07. |
| SURF-031 | Dashboard active-project metric card | User workspace; dashboard overview; open projects/ask AI | `GENERIC_CHAT_ONLY` — AI action chỉ prefilled generic prompt. |
| SURF-032 | Dashboard open-task metric card | User workspace; dashboard overview; open tasks/ask AI | `GENERIC_CHAT_ONLY`. |
| SURF-033 | Dashboard team-capacity metric card | User workspace; member workload; inspect/ask AI | `GENERIC_CHAT_ONLY`. |
| SURF-034 | Dashboard project-progress chart | User workspace; deterministic series; inspect | `NO_AI_JUSTIFIED` — chart calculation should remain deterministic. |
| SURF-035 | Dashboard Attention Risk card | User; `/api/dashboard/attention-summary`; open risk target | `NO_AI_JUSTIFIED` — rule-based risk is transparent. |
| SURF-036 | Dashboard Recent Activity card | User; `/api/dashboard/recent-activities`; open activity | `NO_AI_JUSTIFIED`. |
| SURF-037 | Dashboard Strategic Overview metrics + AI brief | User; client-posted workspace metrics; generate brief | `PRESENT_PARTIAL` — schema check exists but synchronous, client-trusted input, no canonical job/source/read-back. |
| SURF-038 | Projects spotlight/list/grid | Project user; dashboard/project data; filter/open | `NO_AI_JUSTIFIED`. |
| SURF-039 | Create/edit project modal | Project creator/manager; project DTO; save | `NO_AI_JUSTIFIED` for core CRUD. |
| SURF-040 | AI Planner modal | Project creator; natural-language plan; generate/review/create | `PRESENT_PARTIAL` — legacy synchronous plan and direct mutation; no canonical draft/idempotent confirm. |
| SURF-041 | Import upload/mapping/confirm/undo | Project manager; files/import session; preview/execute/undo | `NO_AI_JUSTIFIED` for parsing/mapping core; import truth states are deterministic. |
| SURF-042 | Archive storage usage card | Admin/owner; `/api/storage/stats`; inspect quota | `NO_AI_JUSTIFIED`. |
| SURF-043 | Archive storage breakdown card | Admin/owner; storage stats; inspect distribution | `NO_AI_JUSTIFIED`. |
| SURF-044 | Archive retention policy card | Admin/owner; retention state; inspect policy | `NO_AI_JUSTIFIED`. |
| SURF-045 | Storage deduplicator card | Admin/owner; duplicate hashes; preview/delete duplicates | `LEGACY_OR_DUPLICATE` — AI branding sai; deterministic hash logic đúng hơn. |
| SURF-046 | Project Trash cards | Project owner/admin; trashed projects; restore/hard-delete | `NO_AI_JUSTIFIED`. |
| SURF-047 | Archived project cards | Project owner/member; archived projects; open/restore | `NO_AI_JUSTIFIED`. |
| SURF-048 | Project Stats health panel | Project member; computed project stats; inspect health | `NO_AI_JUSTIFIED` for score; Primary adds a separate narrative card, không thay score. |
| SURF-049 | Project Stats current metric cards | Project member; task aggregates; inspect counts | `NO_AI_JUSTIFIED` for values. |
| SURF-050 | Project Stats status distribution | Project member; task status aggregate | `NO_AI_JUSTIFIED`. |
| SURF-051 | Project Stats delivery progress | Project member; completion/open metrics | `NO_AI_JUSTIFIED` for metric; native grounded AI-08 card is missing beside it. |
| SURF-052 | Demo Map roadmap/sprint track | Project member; sprint/milestone/task APIs; inspect timeline | `NO_AI_JUSTIFIED` for timeline computation. |
| SURF-053 | Milestone detail + create/edit modal | Project manager; sprint/milestone; edit | `NO_AI_JUSTIFIED`. |
| SURF-054 | Project task board/list | Project member; project tasks; move/open/filter | `MISSING_HIGH_VALUE` — no contextual AI-05/06/07 entrypoint. |
| SURF-055 | Create task + import controls | Project manager/member per role; task/import DTO; create | `NO_AI_JUSTIFIED` for manual create. |
| SURF-056 | Delayed-project resolution trigger/review | Project manager; project; enqueue and review proposed actions | `PRESENT_PARTIAL` — canonical job/draft exists; schema validator/source grounding/fallback content incomplete. |
| SURF-057 | Project task detail core | Task viewer/editor; task; inspect/edit | `MISSING_HIGH_VALUE` — breakdown/checklist surfaces absent. |
| SURF-058 | Assignment Insight card | Task viewer/manager; task + project members/workload; inspect candidates | `PRESENT_PARTIAL` — native caller and explainable output, nhưng synchronous endpoint bypasses canonical wrapper and confirm. |
| SURF-059 | Task timer/attachments/comments cards | Task collaborator; task artifacts; log/upload/comment | `NO_AI_JUSTIFIED` for core actions. |
| SURF-060 | Project Members tab | Project manager/member; project members; add/role/remove | `NO_AI_JUSTIFIED`. |
| SURF-061 | Project Activity tab | Project member; receives projectId but calls global activity endpoint | `FRONTEND_ONLY` — integration context sai; deterministic scoping fix required, không phải AI. |
| SURF-062 | Project Wiki tab | Wiki author/reader; wiki pages; create/read/edit | `MISSING_HIGH_VALUE` — grounded summarize/to-task candidate; no native caller. |
| SURF-063 | Project Workload tab | Project member; `/api/projects/{id}/workload` | `FRONTEND_ONLY` — component có code nhưng tab id `capacity` không có trong `tabs`, nên unreachable. |
| SURF-064 | Project Gantt/Attention/Timeline tab | Project member; gantt/attention/timeline APIs | `FRONTEND_ONLY` — component có code nhưng tab id `gantt` không có trong `tabs`. |
| SURF-065 | Project Webhooks tab | Project manager; webhook APIs; create/test/delete | `NO_AI_JUSTIFIED`. |
| SURF-066 | Tasks “Nhiệm vụ của tôi” stat card | User; visible task aggregate | `NO_AI_JUSTIFIED`. |
| SURF-067 | Tasks overdue stat card | User; deterministic due-date aggregate | `NO_AI_JUSTIFIED`. |
| SURF-068 | Tasks due-soon stat card | User; deterministic due-date aggregate | `NO_AI_JUSTIFIED`. |
| SURF-069 | Tasks completed stat card | User; status aggregate | `NO_AI_JUSTIFIED`. |
| SURF-070 | Tasks filter/list/cards | User; visible tasks; filter/open/complete | `NO_AI_JUSTIFIED` for filter/status calculation. |
| SURF-071 | Global Tasks detail drawer | Authorized task user; task/comments/files/time | `MISSING_HIGH_VALUE` — native task AI entrypoint absent. |
| SURF-072 | Teams chat sidebar/window | Group member; group messages; send/read/search | `NO_AI_JUSTIFIED` for core chat; AI actions live in separate native tab. |
| SURF-073 | Group Members tab | Group admin/member; members; add/role/remove/leave | `NO_AI_JUSTIFIED`. |
| SURF-074 | Group Invites tab | Group admin; invitations; invite/read | `NO_AI_JUSTIFIED`. |
| SURF-075 | Group Polls tab/card | Group member; poll messages; create/vote/close | `NO_AI_JUSTIFIED`. |
| SURF-076 | Group Meeting launcher tab | Group member; meeting session; start | `NO_AI_JUSTIFIED` for launch. |
| SURF-077 | Group Project tab | Group admin/member; group→project DTO; create/open | `NO_AI_JUSTIFIED` for explicit create. |
| SURF-078 | Group AI tab (`GroupAiPanel`) | Group member; selected messages/group; summarize/task draft/action items/project draft | `PRESENT_PARTIAL` — canonical AI-03/04 plus legacy actions; selected-range/source/editor tests incomplete. |
| SURF-079 | Shared attachments panel | Group member; message attachments; open/download/hide | `NO_AI_JUSTIFIED`. |
| SURF-080 | Create Group modal | User; group DTO; create | `NO_AI_JUSTIFIED`. |
| SURF-081 | Meeting prejoin/live media/screen share | Meeting participant; realtime/media | `NO_AI_JUSTIFIED`. |
| SURF-082 | Meeting privacy/consent dialog | Meeting participant/admin; policy/consent | `NO_AI_JUSTIFIED` — policy decision must remain deterministic. |
| SURF-083 | Meeting transcript panel | Authorized participant; transcript | `NO_AI_JUSTIFIED` for transcript capture; source for checknote. |
| SURF-084 | AI Checknote/action-item cards | Authorized participant/manager; transcript/meeting; generate/link/create task | `PRESENT_PARTIAL` — source/consent handling tốt, nhưng legacy sync, no canonical reload/cancel/retry. |
| SURF-085 | Group Poll create form | Group member; poll | `NO_AI_JUSTIFIED`. |
| SURF-086 | Poll result/vote/close panel | Group member/creator; poll options/votes | `NO_AI_JUSTIFIED`. |
| SURF-087 | Wiki document + TOC | Wiki reader; wiki content; read/navigate | `MISSING_HIGH_VALUE` — grounded brief/draft candidate deferred after locked AI-01..08. |
| SURF-088 | Settings profile/workspace/password | User/org/project manager; settings DTOs | `NO_AI_JUSTIFIED`. |
| SURF-089 | Settings Appearance tab | User; local theme | `NO_AI_JUSTIFIED`. |
| SURF-090 | Settings Workflow tab | Project manager; workflow settings | `NO_AI_JUSTIFIED` — state-transition policy deterministic. |
| SURF-091 | Settings Notifications / weekly AI report toggle | User; localStorage only; toggle digest | `FRONTEND_ONLY` — text promises backend AI email without backend schedule/read-back. |
| SURF-092 | Settings API Keys/integration info | User; API key APIs | `NO_AI_JUSTIFIED`. |
| SURF-093 | Settings Privacy tab | Org/project admin; privacy/retention/DSAR | `NO_AI_JUSTIFIED` for policy operations. |
| SURF-094 | Settings personal audit log | User; audit API; inspect | `NO_AI_JUSTIFIED`. |
| SURF-095 | Settings AI usage/budget card (required, absent) | Org Admin/project manager; AI ledger/policy | `BACKEND_ONLY` — GET/PUT endpoints exist; no product surface. |
| SURF-096 | Admin user table/filter | System Admin; users; inspect/filter | `NO_AI_JUSTIFIED`. |
| SURF-097 | Admin user/project-role drawer | System Admin; user/project memberships; edit/revoke sessions | `NO_AI_JUSTIFIED`. |
| SURF-098 | Admin create/transfer flow | System Admin; user/admin ownership; mutate | `NO_AI_JUSTIFIED`. |
| SURF-099 | Organization context/member list | Org member/admin; org members | `NO_AI_JUSTIFIED`. |
| SURF-100 | Organization invite/role flow | Org admin/moderator; memberships | `NO_AI_JUSTIFIED`. |
| SURF-101 | Moderator assignment table | System Admin; delegated scopes | `NO_AI_JUSTIFIED`. |
| SURF-102 | Moderator grant/revoke modal | System Admin; capability scope | `NO_AI_JUSTIFIED`. |
| SURF-103 | Analytics full-screen Erumi panel | Auth user; optional project/chat history | `GENERIC_CHAT_ONLY`. |
| SURF-104 | Profile identity detail cards | Current user; identity/email/role | `NO_AI_JUSTIFIED`. |

State audit rule: mỗi surface có loading/empty/error/permission/degraded state được tính trong chính `SURF-ID`. Các surface deterministic nhìn chung có loading/empty/error; các AI partial thiếu ít nhất một trong queued/running/cancel/retry/degraded/read-back/source-open. Những thiếu này được ghi trong Gap Register.

### 3.3 Current-source delta — 2026-08-01

Các row sau mở rộng inventory lịch sử lên **110 surface records**. `SURF-105/106` là surface runtime xuất hiện sau baseline; `SURF-107..110` là surface của Action Composer và đã được reconciled lại sau implementation closure tại §20.

| SURF-ID | Route / module / card | Role; entity context; primary action / source | Status và rationale |
|---|---|---|---|
| SURF-105 | `/organizations` — `OrganizationsPage.vue` | System Admin; organization collection; create/select organization | `NO_AI_JUSTIFIED` — organization CRUD và ownership là deterministic, explicit security operation. |
| SURF-106 | Project Task Detail — “Kỹ năng cần thiết” / `TaskSkillsAiCard.vue` | Task manager; task + organization skill catalog; manual tag hoặc AI suggestion/review/confirm | `NATIVE_COMPLETE` theo source/test artifacts của CAND-015; release run vẫn phải rerun verification gates. |
| SURF-107 | AppShell compact model control + global `AI Hành động` trigger | Authenticated user; current route and optional project; open action composer | `NATIVE_COMPLETE` cho Task-create v1 — AppShell có shared trigger; chip ghi rõ DeepSeek V4 Pro là model **ưu tiên**, sau khi route thì drawer/receipt hiển thị provider/model thực tế. Runtime model registry/preference selector vẫn là backlog platform, không được giả là đã có. |
| SURF-108 | Contextual `Thực hiện với AI` entrypoint trong Task/Project/Group/Meeting | Authorized module user; current entity/selection; compose module-native action | `PRESENT_PARTIAL` — Project Task context dùng shared context envelope/caller và manager permission; Group/Meeting/Schedule adapters được defer có chủ ý sau Task-create Primary. |
| SURF-109 | AI Action Composer drawer: intent, assumptions, 1–3 options, editable commands, confirm và receipt | Authorized actor; persisted job/draft/action receipt | `NATIVE_COMPLETE` cho `task.create.v1` — structured options, editable/selective task rows, explicit confirmation, atomic idempotent execution và receipt/deep links/read-back. |
| SURF-110 | AI Process Activity chip + expandable operational timeline trong Composer/AppShell | Authorized actor; current/persisted AI job, stage events, elapsed time, retry/cancel/read-back | `NATIVE_COMPLETE` — persisted ordered safe activity events drive elapsed chip/timeline; reload restores stage history; raw reasoning/private payload is not rendered. |

Current route/page delta: router có **25/25** records và page folder có **17/17** SFC sau khi thêm `/organizations`/`OrganizationsPage.vue`. Các route/page còn lại giữ disposition lịch sử; current inventory closure được cập nhật ở §16.1.

## 4. Existing AI Capability Universe

### 4.1 Controller route reconciliation — 58/58

Mỗi route dưới đây nằm trong đúng một `AI-CAP-ID`; cột caller liệt kê caller thật, không suy ra từ docs.

| AI-CAP-ID | Endpoint(s) | Job/schema/draft | Frontend caller / output UI | Source, mutation, tests | Status |
|---|---|---|---|---|---|
| AI-CAP-001 | `POST /api/ai/generate-plan`; `POST /api/ai/create-plan` | Legacy `GenerateProjectPlan`; inline plan DTO | `AiPlannerModal` | Input manual; create-plan mutates project/tasks directly; planner tests only indirect | `PRESENT_PARTIAL` |
| AI-CAP-002 | `POST /api/ai/sync` | Vector ingestion | None | Backend ingestion/outbox; no product status | `BACKEND_ONLY` |
| AI-CAP-003 | `POST/GET /api/ai/jobs`; `GET /jobs/{id}`; `/result`; `POST /retry`; `/cancel` | Canonical `AiJob`, dispatch, attempts, usage | `AiActivityPanel` list/detail | Source guard, privacy, budget, audit, idempotency; unit/integration/E2E shell evidence | `VERIFICATION_GAP` — local platform tests pass, hosted/capability evidence absent |
| AI-CAP-004 | `GET /api/ai/drafts`; `GET/PATCH /drafts/{id}`; `POST /confirm`; `/reject` | `AiGeneratedDraft`; multiple draft types | `AiActivityPanel`, Erumi approval | Supports only `reject`, `execute_action`, `create_tasks`; task draft/meeting tests | `PRESENT_PARTIAL` |
| AI-CAP-005 | `POST /api/ai/agent-runs`; `GET /agent-runs/{id}`; `POST /approve` | `Qaly.AgentRun.v1`, draft-change | Erumi actions | Generic agent flow, explicit approval; unit evidence | `GENERIC_CHAT_ONLY` |
| AI-CAP-006 | `GET /api/ai/usage`; `GET/PUT /api/ai/budget`; `GET /api/ai/health` | Usage ledger/budget policy/platform health | Health only in `AiActivityPanel`; no usage/budget UI | Backend unit evidence; no product E2E | `BACKEND_ONLY` |
| AI-CAP-007 | `POST /api/ai/meetings/{meetingId}/extract-actions` | `meeting_action_extract`, `meeting_action_extract.v4`, `MeetingActionItems` | No caller; meeting page calls another route | Meeting source guard; draft can `create_tasks`; platform tests not wrapper E2E | `BACKEND_ONLY` |
| AI-CAP-008 | `POST /api/ai/groups/{groupId}/summaries` | `chat_summary`, `chat_summary.v4` | `GroupAiPanel` selected-message summary | Message source URLs assembled; range/cache/reload evidence incomplete | `PRESENT_PARTIAL` |
| AI-CAP-009 | `POST /api/ai/task-drafts/from-source` | `task_draft`, `task_draft.v4`, `TaskDraft` | `GroupAiPanel` | Source guard + draft/create_tasks; editor mostly generic JSON | `PRESENT_PARTIAL` |
| AI-CAP-010 | `POST /api/ai/tasks/{taskId}/recommend-assignees` | `assignee_recommendation.v4` | None; Project Detail calls sync insight instead | Task source guard; read-only recommendation | `BACKEND_ONLY` |
| AI-CAP-011 | `POST /api/ai/tasks/{taskId}/breakdown` | `task_breakdown.v4`, `TaskBreakdown` | No native caller; generic draft may list | Confirm action emitted by UI unsupported; no persisted subtask relation | `BACKEND_ONLY` |
| AI-CAP-012 | `POST /api/ai/tasks/{taskId}/acceptance-checklist` | `acceptance_checklist.v4`, `AcceptanceChecklist` | No native caller; generic draft may list | Confirm action unsupported; no checklist persistence model | `BACKEND_ONLY` |
| AI-CAP-013 | `POST /api/ai/projects/{projectId}/progress-summary` | `progress_summary`, `progress_summary.v4` | None | Project source hash omits task state; generic prompt; no metric reconciliation/test | `BACKEND_ONLY` |
| AI-CAP-014 | `POST /api/ai/projects/{projectId}/suggest-resolution` | `project_delay_resolution.v4`, `ProjectDelayResolution` | Project Detail trigger + visual draft editor | Canonical draft/confirm exists; unknown schema passes generic validator; fallback contains invented/hard-coded content | `PRESENT_PARTIAL` |
| AI-CAP-015 | `POST /api/ai/projects/{projectId}/sprints/{sprintId}/progress-summary` | Same generic `progress_summary.v4` | None | Sprint hash omits task state; no renderer/test | `BACKEND_ONLY` |
| AI-CAP-016 | `POST /api/ai/priority` | Legacy `SuggestTaskPriority` | None | Manual text; sync gateway | `BACKEND_ONLY` |
| AI-CAP-017 | `GET /api/ai/projects/{id}/summary`; `/risks`; `/insights` | Legacy sync jobs | None | Backend-only, overlapping AI-08/dashboard | `BACKEND_ONLY` |
| AI-CAP-018 | `GET /api/ai/tasks/{id}/assignment`; `/assignment-insight` | Sync heuristic/AI service | Project task assignment card calls insight | Authorized task/project query; read-only output; no canonical job/confirm | `PRESENT_PARTIAL` |
| AI-CAP-019 | `GET /api/ai/search` | Semantic/vector search | None; global search is client filter | Vector auth/visibility needs product proof | `BACKEND_ONLY` |
| AI-CAP-020 | `POST /api/ai/subtasks` | Legacy `GenerateSubtasks` | None | Free string output, no source/task authorization/draft | `BACKEND_ONLY` |
| AI-CAP-021 | `POST /api/ai/chat`; `/chat/fast`; `/chat/stream` | `Chat`, `ChatStream`, `TextAnswer.v1` | Erumi/Floating assistant | Generic route/project context; not per-module contract | `GENERIC_CHAT_ONLY` |
| AI-CAP-022 | `GET /api/ai/export/{projectId}` | Deterministic Word/Excel export | No current caller found | Not an AI decision; project auth required | `NO_AI_JUSTIFIED` |
| AI-CAP-023 | `POST /api/groups/{groupId}/ai/action-items` | Legacy `ActionItem` | `GroupAiPanel` | Group source, sync output; no canonical draft/read-back | `PRESENT_PARTIAL` |
| AI-CAP-024 | `POST /api/groups/{groupId}/ai/../meetings/{meetingId}/hook` | Legacy meeting hook | None | Odd relative route; no caller | `BACKEND_ONLY` |
| AI-CAP-025 | `GET /api/groups/{groupId}/ai/context` | Deterministic group context | None | Support endpoint only | `BACKEND_ONLY` |
| AI-CAP-026 | `POST /api/groups/{groupId}/ai/summary` | Legacy `DiscussionSummary` | `GroupAiPanel` | Duplicates canonical AI-CAP-008 | `LEGACY_OR_DUPLICATE` |
| AI-CAP-027 | `POST /api/groups/{groupId}/ai/draft-project` | Legacy `DraftProject` | `GroupAiPanel` | Review-only payload, no canonical confirm | `PRESENT_PARTIAL` |
| AI-CAP-028 | `POST /api/meetings/import/meetily` | `meetily_import.v4`, canonical meeting records | Import/meeting flows indirectly | Privacy/source/audit present; mixed legacy/canonical orchestration | `PRESENT_PARTIAL` |
| AI-CAP-029 | `POST /api/meetings/{sessionId}/auto-checknote` | `AutoChecknote`; meeting extract records | `GroupMeetingPage` | Privacy consent/policy; synchronous run; no retry/cancel/read-back job | `PRESENT_PARTIAL` |
| AI-CAP-030 | `POST create-task`; `GET action-items`; `POST link-task`; `GET task-link` under `/api/meetings/{id}/action-items` | Human confirmation/link workflow | Checknote action-item cards | Explicit mutation and mapping; capability support, not model invocation | `PRESENT_PARTIAL` |
| AI-CAP-031 | `GET /api/dashboard/overview` | Deterministic aggregate | Dashboard/App | No AI | `NO_AI_JUSTIFIED` |
| AI-CAP-032 | `GET /api/dashboard/attention-summary` | Deterministic attention rules | `AttentionRiskCard` | No AI | `NO_AI_JUSTIFIED` |
| AI-CAP-033 | `GET /api/dashboard/recent-activities` | Deterministic audit/activity read | Dashboard + incorrectly Project Activity | No AI; project-scope integration gap | `NO_AI_JUSTIFIED` |
| AI-CAP-034 | `GET /api/dashboard/strategic-overview` | Deterministic metrics | `StrategicOverviewAI` | Server-authorized metrics; correct deterministic layer | `NO_AI_JUSTIFIED` |
| AI-CAP-035 | `POST /api/dashboard/ai-strategy` | Sync `WorkspaceStrategy.v1` | `StrategicOverviewAI` | Client posts metrics; validator exists; no canonical source/job/usage read-back | `PRESENT_PARTIAL` |

Route total check: `AiController` 42 + `GroupAiController` 5 + `MeetingsController` 6 + `DashboardController` 5 = **58/58**.

### 4.2 Service, platform, provider, schema và configuration inventory

| Layer | Runtime evidence | Audit disposition |
|---|---|---|
| Application facade | `IAiService`/`AiService` | Nhiều legacy sync function còn song song canonical workflow. |
| Canonical workflow | `IAiWorkflowService`/`AiWorkflowService`, `AiPlatformQueryService` | Job/source/privacy/budget/cache/idempotency/draft/read-back mạnh; capability confirm và schema-specific validation chưa đều. |
| Group/chat | `IGroupAiService`/`GroupAiService`, `ErumiChatService`, `AgentRunService`, `AiTools` | Generic chat và group legacy/canonical overlap. |
| Gateway/router | `IAiGateway`/`AiGateway`, `MicrosoftAgentOrchestrator`, `AiProviderFactory` | Backend-only provider access; repair/fallback/label support. |
| Providers | Ollama, DeepSeek, OpenAI, Gemini | Routed server-side; policy/budget/provider settings exist. |
| Worker/lifecycle | `AiJobWorker`, `AiJobDispatchStore`, `AiJobProcessor` | Queue/lease/retry/cancel/usage/audit implemented and locally tested. |
| Guard/control | `AiSourceGuard`, `AiComplianceService`, `AiCostService`, `AiOutputValidator` | Authorization/freshness/privacy/budget present; entity hash coverage và schema loader incomplete. |
| Feature flags | `AI_JOB_V4_ENABLED`, `AI_JOB_V4_WORKER_ENABLED`, platform `Enabled`, `WorkerEnabled`, `AllowProviderDegradedMock` | Có disable/degraded path; native card phải surface state trung thực. |
| Vector support | `IAiIngestionService`, vector outbox/worker, Qdrant/Ollama embeddings | Semantic search backend exists; no native caller. |

Schema file closure:

| Schema file | Runtime wrapper ID | Finding |
|---|---|---|
| `acceptance_checklist.schema.json` (`$id ...v3.2`) | `acceptance_checklist.v4` | Version identity mismatch; hardcoded validator only. |
| `assignee_recommendation.schema.json` (`v3.2`) | `assignee_recommendation.v4` | Same mismatch. |
| `chat_summary.schema.json` (`v3.2`) | `chat_summary.v4` | Same mismatch; source-range evidence partial. |
| `meeting_action_extract.schema.json` (`v3.2`) | `meeting_action_extract.v4` | Same mismatch. |
| `privacy_data_request.schema.json` (`v3.2`) | no product wrapper in `AiController` | Schema orphan has explicit disposition: platform/privacy support, not current module AI. |
| `progress_summary.schema.json` (`v3.2`) | `progress_summary.v4` | Primary contract target; lacks structured source refs/reconciliation fields. |
| `task_breakdown.schema.json` (`v3.2`) | `task_breakdown.v4` | Draft generated; mutation target absent. |
| `task_draft.schema.json` (`v3.2`) | `task_draft.v4` | Canonical wrapper/caller exists, product editor evidence partial. |

Canonical wrapper job types = `meeting_action_extract`, `chat_summary`, `task_draft`, `assignee_recommendation`, `task_breakdown`, `acceptance_checklist`, `progress_summary`, `project_delay_resolution` = **8/8**.

Draft families = `MeetingActionItems`, `TaskDraft`, `TaskBreakdown`, `AcceptanceChecklist`, `DraftChange`, `ProjectDelayResolution`, dynamic agent tool draft = **7/7**. Backend confirmation supports only `reject`, `execute_action`, `create_tasks`; frontend additionally emits unsupported `create_subtasks`, `save_checklist`, `save_report`.

### 4.3 Current-source capability delta — 2026-08-01

| AI-CAP-ID | Endpoint/job/schema/caller | Runtime evidence và disposition |
|---|---|---|
| AI-CAP-036 | `POST /api/ai/tasks/{taskId}/skill-suggestions`; canonical `task_skill_suggestion`; `task_skill_suggestion.v1`; `TaskSkillsAiCard.vue` | `NATIVE_COMPLETE` theo implementation closure CAND-015: authorized task/catalog context, schema + semantic reconciliation, persisted draft, `apply_task_skills`, source/privacy/budget/usage/audit/read-back và native review. |
| AI-CAP-037 | `POST /api/ai/actions/compose`; `GET /api/ai/jobs/{jobId}/activity`; `action_intent_compose`; `ai_action_intent_envelope.v1`; `AiActionPlan`; `execute_action_set`; AppShell/Project Task callers | `NATIVE_COMPLETE` cho bounded Task-create v1 — authorized server snapshot, versioned `task.create.v1`, canonical job/draft/activity, semantic validation, edit/selective confirm, atomic/idempotent mutation, receipt/audit/usage/read-back và repeatable unit/integration/E2E evidence. Project/Group/Meeting/Schedule tool adapters không được tính vào capability này. |

Current controller-route reconciliation sau closure: `AiController` 46 + `GroupAiController` 5 + `MeetingsController` 6 + `DashboardController` 5 = **62/62 runtime routes**. Hai route mới của CAND-018 là compose và authorized incremental activity read-back. Current schema folder có **10/10 runtime schema files**, gồm `ai_action_intent_envelope.v1` của AI-CAP-037.

## 5. Bidirectional UI ↔ API/job/schema/test matrix

| Flow | UI → backend reconciliation | Backend → UI reconciliation | Verdict |
|---|---|---|---|
| Dashboard generic metric “Hỏi AI” | Opens generic chat with prompt; no capability request schema | Chat endpoints have generic renderer only | GAP-017; `GENERIC_CHAT_ONLY`. |
| Dashboard Strategic Brief | Real caller to `/api/dashboard/ai-strategy`, loading/error/result UI | Sync endpoint has caller, but trusts client metrics and bypasses canonical job/source/read-back | GAP-011; `PRESENT_PARTIAL`. |
| Project AI planner | Generate then create caller; UI review before create | Legacy endpoints both have caller; create mutates directly | GAP-010; `PRESENT_PARTIAL`. |
| Project delay resolution | Native trigger enqueues canonical job; review opens AI Activity | Wrapper has caller and visual editor; schema-specific validator/source-grounded fallback tests absent | GAP-018/019/021; `PRESENT_PARTIAL`. |
| Task assignment insight | Native card calls sync insight | Sync endpoint has caller; canonical recommendation wrapper orphan | GAP-006; sync capability partial + canonical backend-only. |
| AI-06 Task breakdown | No native caller; generic draft renderer may receive result | Wrapper/schema/draft exist; UI confirm action rejected by backend; no subtask model | GAP-004; `BACKEND_ONLY`. |
| AI-07 Acceptance checklist | No native caller | Wrapper/schema/draft exist; UI confirm action rejected; no checklist model | GAP-005; `BACKEND_ONLY`. |
| AI-08 Project summary | No native caller | Endpoint/job/schema exist; no SQL snapshot prompt/reconciliation/read-back evidence | GAP-001/002/003; `BACKEND_ONLY`. |
| AI-08 Sprint summary | No native caller | Same, plus sprint source hash omits task state | GAP-001/003; `BACKEND_ONLY`. |
| Group selected summary (AI-03) | `GroupAiPanel` sends selected message sources | Canonical wrapper has caller/result path; selected-range/cache/source-open tests incomplete | GAP-007; `PRESENT_PARTIAL`. |
| Group task draft (AI-04) | `GroupAiPanel` enqueues source-linked draft | Wrapper has caller and draft confirmation; native structured editor/selective evidence partial | GAP-008; `PRESENT_PARTIAL`. |
| Group legacy summary/action/project | Real callers | Duplicates canonical transport or bypasses it | GAP-020; partial/legacy. |
| Meeting AI checknote | Privacy dialog → sync auto-checknote → summary/action cards | Legacy endpoint has caller; canonical extract wrapper orphan | GAP-009; `PRESENT_PARTIAL`. |
| AI usage/budget | No UI caller except health | GET/PUT/health backend and tests exist | GAP-012; `BACKEND_ONLY`. |
| Weekly AI report | Toggle writes localStorage only | No schedule/config/read-back endpoint | GAP-013; `FRONTEND_ONLY`. |
| Analytics | Full-screen Erumi only | Generic chat backend has caller and source drawer | GAP-014; `GENERIC_CHAT_ONLY`. |
| Semantic search | Global search uses loaded deterministic arrays | `/api/ai/search` has no caller | Explicit defer: exact search remains `NO_AI_JUSTIFIED`; backend route is `BACKEND_ONLY`. |
| Project Activity | Component passes projectId but ignores it in API call | Dashboard recent activity serves global authorized feed | GAP-015; deterministic integration fix, not AI candidate. |
| Workload/Gantt | Components implemented | Backend endpoints have UI code, but no reachable tab id | GAP-016; navigation fix, not AI candidate. |
| Conversational write/action intent | Legacy Erumi still has a keyword/`erumi_autonomous_tasks` path, while the new shared caller uses explicit `action_intent_compose` | Native Task-create v1 now has schema/job/draft/confirm/receipt evidence; legacy path is `LEGACY_OR_DUPLICATE`, and missing Project/Group/Meeting/Schedule catalog is explicit deferred breadth | GAP-026/027 closed for bounded path; AI-CAP-037 / CAND-018 `NATIVE_COMPLETE` for `task.create.v1`. |
| Global/contextual AI Action UX + process visibility | AppShell and Project Task now use the shared composer; other contextual adapters are deferred | Persisted safe stage events drive elapsed timeline; truthful strong-profile/actual model is shown. Shared runtime model registry/preference API remains partial platform work | GAP-028/030 closed for bounded path; GAP-029 `PRESENT_PARTIAL`; SURF-107/109/110 complete, SURF-108 partial. |

Orphan reconciliation result: mọi caller-less backend route đã nhận `BACKEND_ONLY`, `LEGACY_OR_DUPLICATE`, `NO_AI_JUSTIFIED` hoặc explicit defer; mọi UI-only promise đã nhận `FRONTEND_ONLY`; **undispositioned orphan = 0**.

## 6. AI-native coverage matrix theo route/page/tab/card

| Module | SURF count | Native/partial signal | Missing/high-value | Explicit no-AI / integration-only |
|---|---:|---|---|---|
| App shell/search/assistant | 6 | Generic chat + partial AI Activity | Capability-specific entrypoints absent by design | Search/notifications deterministic. |
| Dashboard | 13 | Strategic brief partial; generic prompts | Canonical, source-grounded workspace brief deferred | Metrics/charts/risk/activity deterministic. |
| Projects/archive/import | 10 | Legacy AI planner | Planner canonicalization candidate | Storage/dedup/import should remain deterministic. |
| Project Detail/Task | 18 | Delay resolution + assignment insight partial | AI-05 canonical UI, AI-06/07, AI-08 project summary, wiki brief | CRUD/stats/timeline/member/webhook deterministic; 3 non-AI integration defects. |
| Global Tasks | 7 | None native | Native task action hub | Counts/filter/status deterministic. |
| Teams/Groups | 12 | AI-03/04 + legacy group actions partial | Source-range/editor closure | Member/invite/poll/meeting launch deterministic. |
| Meeting | 4 | Privacy-aware checknote partial | Canonical migration/read-back | Media/transcript/policy deterministic. |
| Poll | 4 | None | No forced AI | Vote/result deterministic. |
| Wiki | 3 | None | Grounded brief/to-task candidate | Authoring/navigation deterministic. |
| Settings | 8 | Health visible elsewhere | AI usage/budget missing; weekly report UI-only | Policy/account/log settings deterministic. |
| Admin/Organization/Moderator | 10 | None | No AI proposed | High-risk role/permission operations deterministic. |
| Analytics/Profile/Error routes | 9 | Generic Erumi only | Analytics native capability deferred pending real job definition | Profile/error deterministic. |
| **Total** | **104** | **15 partial + 7 generic** | **7 high-value surfaces** | **67 no-AI + 5 frontend-only + 2 legacy + 1 backend-only** |

## 7. Gap Register

| GAP-ID | Severity | Finding / runtime evidence | Resolution disposition |
|---|---|---|---|
| GAP-001 | P0 Product | AI-08 project/sprint wrappers have no caller or native result card. | CAND-001 Primary; CAND-002 Stretch. |
| GAP-002 | P0 Contract | Schema files declare v3.2 while runtime wrapper requests v4; JSON Schema is not loaded as authority. | CAND-001 establishes real `progress_summary.v4`; other schemas deferred by dependency. |
| GAP-003 | P0 Grounding | Generic processor prompt does not hydrate project/task/sprint entity content; project/sprint source hashes omit task state. | CAND-001/002. |
| GAP-004 | P0 Product/Data | AI-06 emits `create_subtasks`, backend rejects it; no `ParentTaskId`/subtask relation. | CAND-004, vetoed until domain/storage decision and migration. |
| GAP-005 | P0 Product/Data | AI-07 emits `save_checklist`, backend rejects it; no checklist persistence model. | CAND-003, vetoed until domain/storage decision and migration. |
| GAP-006 | P1 Integration | Native assignment card calls sync insight while canonical assignee wrapper has no caller. | CAND-006 backlog. |
| GAP-007 | P1 Contract | AI-03 selected range/source/cache/reload/source-open evidence incomplete. | CAND-007 backlog. |
| GAP-008 | P1 UX/Contract | AI-04 source selection and draft creation exist; capability-native structured edit/reject/confirm E2E incomplete. | CAND-008 backlog. |
| GAP-009 | P1 Integration | Meeting page uses legacy auto-checknote; canonical meeting extract endpoint is orphan. | CAND-010 backlog. |
| GAP-010 | P1 Safety | AI planner creates project/tasks through legacy mutation instead of persisted draft + idempotent confirmation. | CAND-011 backlog. |
| GAP-011 | P1 Platform | Dashboard strategy trusts client-posted metrics and bypasses canonical job/source/usage/read-back. | CAND-009 backlog. |
| GAP-012 | P0 Requirement | AI usage/budget endpoints have no product UI/read-back workflow. | CAND-005 backlog, dependency after Primary due locked AI-08 tie-break. |
| GAP-013 | P1 Truthfulness | Weekly AI report toggle is localStorage-only; UI promises email behavior not configured server-side. | CAND-013 backlog or remove label; feature-off until backend exists. |
| GAP-014 | P2 Product | Analytics page is only generic chat; no analytics-specific job/decision contract. | Explicit defer; require validated user job before AI proposal. |
| GAP-015 | P1 Integration | Project Activity calls global activity endpoint despite project context. | Deterministic fix; `NO_AI_JUSTIFIED`, not in AI goal. |
| GAP-016 | P1 Navigation | Workload/Gantt components are unreachable because tab ids are absent. | Deterministic navigation fix; not in AI goal. |
| GAP-017 | P2 UX | Dashboard metric-card AI buttons only prefill generic chat. | Keep generic label or remove; no native credit. CAND-009 covers composite brief, not three duplicate buttons. |
| GAP-018 | P0 Validation | Unknown schemas such as project-delay-resolution can pass after JSON parse; validator is substring-based, not schema-authoritative. | CAND-001 closes progress schema only; platform-wide loader remains backlog. |
| GAP-019 | P0 Grounding | Some results/fallbacks lack source links or include invented/hard-coded identity/content. | CAND-001 mandates no fabricated fallback; capability owners must remediate others. |
| GAP-020 | P1 Architecture | Legacy/canonical endpoint pairs coexist for planner, group summary, meeting, assignment, progress-like reads. | Strangler backlog by capability; no big-bang removal. |
| GAP-021 | P0 Evidence | Current E2E AI Activity uses mocked APIs; no real capability E2E for AI-06/07/08 or provider failure/read-back. | CAND-001 test matrix; others backlog. |
| GAP-022 | P1 Lifecycle | Most partial AI cards omit at least one of queued/running/cancel/retry/degraded/source-open/reload states. | CAND-001 establishes reusable card state pattern; reuse in backlog. |
| GAP-023 | P0 Product/Data | Task labels hiện là project-local labels dùng chung cho risk/domain/category; chưa có organization skill taxonomy, proficiency requirement, provenance hoặc AI-review draft. Vì vậy không thể coi label `Frontend`/`Backend` là skill evidence đáng tin cậy. | CAND-015 — implementation closure đã được ghi nhận tại §17.8; release evidence vẫn phải rerun. |
| GAP-024 | P0 Evidence/Fairness | Assignment hiện tại chỉ biết người được giao; `TaskItem` không có completion attribution. Không thể kết luận ai “mạnh” chỉ vì họ từng nằm trong assignee list, đặc biệt với task nhiều assignee. Thiếu confidence, source evidence, recency, self-declared/manager-endorsed signal và correction/appeal path. | CAND-016; blocked until CAND-015 and completion-attribution policy/persistence are approved. |
| GAP-025 | P0 Product/Safety | Workload chỉ aggregate trong một project; chưa có working capacity, availability, leave/calendar hoặc portfolio permission contract. Chưa thể đề xuất assignee/deadline xuyên nhiều project mà không gây overload hoặc rò rỉ task riêng tư. | CAND-017; depends on CAND-015/016 plus deterministic capacity/availability foundation. |
| GAP-026 | P0 Runtime/Product | Legacy Erumi write-intent gọi một job type không có schema mapping và đòi draft trước khi worker tạo xong. | **CLOSED cho native Task-create path** — shared UI không gọi legacy flow; explicit `action_intent_compose` dùng canonical async job → result → draft. Legacy path còn lại mang disposition `LEGACY_OR_DUPLICATE` và không được tính native. |
| GAP-027 | P0 Contract/Safety | Generic XML/regex/ad-hoc tool path thiếu typed registry, selective confirmation và receipt. | **CLOSED cho Task-create v1** — fixed schema/tool/version allowlist, semantic reconciliation, editable selection, atomic/idempotent `execute_action_set` và read-back receipt; generic legacy executor không được mở rộng. |
| GAP-028 | P0 UX/Integration | Thiếu global/contextual trigger, option/review/receipt surface. | **CLOSED cho AppShell + Project Task scope** — shared composer và contextual Project Task caller đã có. Group/Meeting/Schedule breadth vẫn explicit defer, không phải orphan. |
| GAP-029 | P0 Model/Truthfulness | Static model labels có thể nói “live” khi runtime chưa xác nhận; thiếu registry/preference API. | **PRESENT_PARTIAL / platform backlog** — Action Composer dùng server-owned strong profile ưu tiên DeepSeek V4 Pro, chip trước-run ghi “Ưu tiên”, sau-run/receipt ghi actual provider/model và không giả fallback. Runtime model registry + user preference API dùng chung toàn hệ thống chưa được implement trong slice này. |
| GAP-030 | P0 UX/Observability | Thiếu persisted ordered operational steps; spinner/client timer không đủ và raw chain-of-thought không được lộ. | **CLOSED** — additive `AiJobActivityEvent`, authorized incremental feed, backend-derived stage labels, elapsed chip, retry/cancel states và reload/read-back timeline. |

Gap closure accounting after current amendment: **30/30** gaps have a candidate, explicit deterministic/no-AI disposition, or explicit defer.

## 8. Candidate catalog và scoring

### 8.1 Score table

Score = outcome 25 + requirement closure 20 + AI-native fit 15 + canonical reuse 15 + testability 10 + quota fit 15.

| CAND-ID | Candidate | Outcome | Gap | Fit | Reuse | Test | Quota | Total | Risk veto / decision |
|---|---|---:|---:|---:|---:|---:|---:|---:|---|
| CAND-001 | Grounded Project Progress Summary card | 24 | 20 | 15 | 15 | 10 | 15 | **99** | None; **PRIMARY**. |
| CAND-018 | Conversational Intent-to-Action / AI Action Composer | 25 | 20 | 15 | 15 | 9 | 12 | **96** | **IMPLEMENTED for Task-create v1**; full Project/Group/Meeting/Schedule adapters remain separately gated backlog. |
| CAND-002 | Grounded Sprint Progress Summary card | 22 | 20 | 15 | 15 | 9 | 12 | **93** | None if Primary green; **STRETCH**. |
| CAND-015 | Task Skill Taxonomy + AI Skill Tag Draft | 25 | 20 | 15 | 14 | 9 | 12 | **95** | **IMPLEMENTED**; bounded additive migration, release evidence vẫn phải rerun. |
| CAND-008 | AI-04 native source-linked task-draft review | 23 | 18 | 15 | 15 | 8 | 8 | 87 | No migration, but larger cross-module editor/source work. |
| CAND-005 | AI usage/budget Settings card | 23 | 20 | 8 | 15 | 10 | 10 | 86 | Platform control, not a module AI decision; defer behind locked AI-08. |
| CAND-007 | AI-03 selected-range group summary closure | 20 | 18 | 15 | 15 | 8 | 9 | 85 | Source-range/editor breadth exceeds Primary. |
| CAND-006 | Canonical skill/evidence-aware assignee recommendation in task drawer | 23 | 17 | 14 | 15 | 9 | 7 | 85 | Depends on CAND-015/016; confirm-assignment mutation must be explicit. |
| CAND-014 | Task hub native AI-06/07 launcher/review | 22 | 20 | 15 | 14 | 9 | 4 | 84 | **VETO:** depends on GAP-004/005 domain migrations. |
| CAND-003 | Persisted acceptance checklist draft | 21 | 20 | 15 | 14 | 8 | 5 | 83 | **VETO:** missing persistence model/migration; cannot close in one run. |
| CAND-004 | Persisted selective task breakdown | 22 | 18 | 15 | 14 | 8 | 5 | 82 | **VETO:** missing parent/subtask model/migration. |
| CAND-016 | Evidence-backed Member Skill Profile | 25 | 18 | 13 | 13 | 8 | 5 | 82 | **VETO for next run:** completion attribution and fairness/privacy policy are prerequisites. |
| CAND-017 | Cross-project Assignment & Schedule Copilot | 25 | 18 | 15 | 13 | 8 | 3 | 82 | **VETO for next run:** needs CAND-015/016, availability/capacity and portfolio permission; too broad for one slice. |
| CAND-010 | Canonical Meeting Checknote | 22 | 15 | 15 | 14 | 8 | 8 | 82 | Privacy/import/action mapping breadth; defer. |
| CAND-009 | Canonical Dashboard Strategic Brief | 20 | 10 | 14 | 15 | 9 | 11 | 79 | Good partial closure, but locked AI-08 wins tie-break. |
| CAND-011 | Canonical AI Project Planner draft | 22 | 12 | 14 | 13 | 8 | 7 | 76 | **SUPERSEDED by CAND-023 (§31); do not implement as a standalone duplicate.** |
| CAND-012 | Grounded Wiki Brief / task draft entry | 18 | 8 | 14 | 15 | 8 | 9 | 72 | New P2 breadth blocked until locked AI-01..08 green. |
| CAND-013 | Persisted weekly AI progress digest | 17 | 10 | 12 | 15 | 8 | 8 | 70 | Scheduler/email preference and AI-08 dependency; defer. |

### 8.2 Decision-ready candidate definitions

**CAND-001 — Project Progress Summary.** User job: project manager quyết định việc nào cần can thiệp dựa trên tiến độ hiện tại. AI cần để tổng hợp narrative/risk/action từ nhiều metric và task signal; metric calculation vẫn deterministic. Trigger: card mới trong Project Stats. Authorized inputs: project và toàn bộ non-deleted, progress-contributing tasks mà project manager được phép xem; private data tự động đặt `Sensitive=true`. Output: `progress_summary.v4` với scope, snapshot period, authoritative metrics, grounded summary points, risk/action objects và `source_refs`. UI: native card có generate/poll/cancel/retry/read-back và source links. Endpoint: existing project progress route, server-owned context builder, canonical job. Mutation: none. States: idle/queued/running/success/empty/degraded/error/canceled/stale; retry explicit. Tests: happy/deny/private/provider/schema/idempotency/cache/stale/reload/audit/usage. Dependency: canonical platform only; no migration. Effort: 1–2 person-days. Rollback: feature flag/card hidden; endpoint returns platform-disabled without falling back to fake success.

**CAND-002 — Sprint Progress Summary.** User job: project manager/scrum master quyết định sprint intervention. Trigger: active milestone detail in Demo Map. Inputs: project, sprint, sprint tasks; same privacy/source rules. Output: same schema with `scope.type=sprint`. UI: same renderer and lifecycle. Endpoint: existing sprint progress route, reuse Primary context/reconciler. Mutation: none. Tests mirror Primary plus wrong-project sprint deny. Dependency: Primary must be fully green; no migration. Effort: 0.5–1 day. Rollback: independent sprint feature flag/entrypoint hidden.

**CAND-003 — Acceptance Checklist.** User job: task author tạo draft validation criteria có cấu trúc. Inputs: task description/comments/wiki refs explicitly selected. Output: checklist items with type, required flag, rationale, source refs. UI: task drawer structured editable rows, selective confirm/reject. Endpoint/job: existing AI-07 wrapper plus new persistence/confirmation action. Controls: project/task permission, private-source policy, idempotent confirm, audit. States/tests: full draft lifecycle, cross-tenant/private/stale/concurrent confirm. Dependency: approve checklist storage model and migration. Effort: 3–5 days including migration/evidence. Rollback: disable generation, preserve saved checklist. Vetoed for next run.

**CAND-004 — Task Breakdown.** User job: task owner tách một task lớn thành ordered child tasks. Inputs: parent task and selected context. Output: child title/description/estimate/order/dependency/source refs. UI: structured draft rows with selection/edit/reject/confirm. Endpoint/job: existing AI-06 wrapper plus `create_subtasks`. Controls: task-manage permission, same-project parent, idempotency/concurrency/audit. Dependency: parent-child domain relation/migration and progress semantics. Effort: 3–5 days. Rollback: disable generator, keep existing children. Vetoed for next run.

**CAND-005 — AI Usage/Budget Settings.** User job: Org Admin kiểm soát cost/hard-stop. AI is not used to calculate cost; this is required AI platform product control. Inputs: ledger/policy APIs. Output: daily/monthly/provider/function/cache breakdown and warning/hard-stop state. UI: Settings card, editable policy with row version/confirm. Tests: role/tenant/concurrency/ledger unavailable/read-back. Dependency: ownership semantics verification, no migration expected. Effort: 1–2 days. Rollback: read-only mode/hide editor.

**CAND-006 — Canonical Skill/Evidence-aware Assignee Recommendation.** User job: project manager chọn assignee phù hợp cho một task cụ thể. Inputs: task skill requirements từ CAND-015, authorized evidence bands từ CAND-016, current assignments/due dates/estimated hours và project membership; không dùng protected attributes, private messages, sentiment hoặc raw task content từ project mà người gọi không được xem. Output: ranked candidates với deterministic skill-coverage/workload/availability sub-scores, evidence confidence, overload/conflict risks, reasons và source metric refs; AI chỉ tổng hợp trade-off/explanation. UI: replace sync card with canonical lifecycle; selecting candidate creates editable assignment draft, explicit confirm. Tests: private task, external member, insufficient evidence, stale workload, cross-project aggregate privacy, concurrent assignment. Dependency: CAND-015/016, availability policy and `assign_task` confirm action. Effort after dependencies: 2–3 days. Rollback: retain current deterministic insight read-only.

**CAND-007 — AI-03 Group Summary closure.** User job: group member hiểu một selected message range. Inputs: exact ordered selected message IDs only. Output: range identity, summary, decisions/questions/action candidates, per-item message source URLs. UI: Group AI tab with source open/cache/stale/read-back. Tests: deleted/private/mixed-group message, empty range, stale cache. No mutation. Effort: 2 days. Rollback: disable canonical summary action, leave chat intact.

**CAND-008 — AI-04 Task Draft closure.** User job: convert selected chat/meeting/wiki/manual source into task draft. Inputs: authorized selected sources. Output: structured task draft plus source refs/confidence. UI: native form fields, edit/reject/confirm; no raw JSON. Tests: selective source, private deny, stale confirm, duplicate confirm. Dependency: reuse existing `create_tasks`; no migration. Effort: 2–3 days. Rollback: disable generator, manual task creation remains.

**CAND-009 — Dashboard Strategic Brief canonicalization.** User job: workspace manager chọn top intervention across visible projects. Inputs must be server-built strategic metrics, never client-trusted. Output: summary/risks/priorities with metric refs and project/task links. UI: existing Strategic Overview card gains canonical lifecycle/read-back. No mutation. Tests: role-scoped metrics, zero data, provider/schema failures. Effort: 1–2 days. Rollback: keep deterministic strategic metrics, hide AI brief.

**CAND-010 — Canonical Meeting Checknote.** User job: participant review meeting summary/action items. Inputs: authorized transcript/import and consent/policy. Output: summary, decisions, risks, action drafts with quotes/source offsets. UI: existing checknote cards backed by job/draft, retry/cancel/read-back and selective create/link. Tests: consent deny/revoke, private transcript, stale source, duplicate action mapping. Effort: 2–4 days. Rollback: retain transcript and manual action-item creation.

**CAND-011 — Canonical Project Planner.** User job: creator draft project/sprints/tasks. Inputs: manual brief and allowed organization/group context. Output: structured multi-entity plan draft. UI: existing modal, editable tree, explicit atomic confirm. Tests: idempotent atomic create, partial failure rollback, permissions, stale group. Dependency: multi-entity transaction/confirm action. Effort: 3–4 days. Rollback: manual project create.

**CAND-012 — Wiki Brief / Task Draft.** User job: reader hiểu dài document hoặc tạo task từ selected section. Inputs: authorized wiki ID plus selected heading/range. Output: grounded summary or existing task-draft schema with heading anchors. UI: Wiki detail side card/review. Tests: private wiki, edited/deleted source, source link, confirm. Dependency: locked AI-01..08 policy gate. Effort: 2 days. Rollback: hide AI entrypoint; wiki unaffected.

**CAND-013 — Weekly AI Digest.** User job: project manager nhận digest tiến độ có nguồn. Inputs: selected projects and server schedule/preference. Output: AI-08 summaries with links, never ungrounded email. UI: Settings persists cadence/projects/opt-in and last-run status. Tests: unsubscribe, no data, provider failure, tenant isolation, send idempotency. Dependency: CAND-001 and scheduler/email config. Effort: 2–3 days. Rollback: disable scheduler and label toggle unavailable.

**CAND-014 — Task Hub AI launcher.** User job: open AI-05/06/07 directly from global task drawer. Inputs/output inherit capability contracts. UI: native action strip + review surfaces. Tests: same task deep link in global/project contexts. Dependency: CAND-003/004/006; no independent implementation allowed. Effort after dependencies: 1 day. Rollback: hide strip.

**CAND-015 — Task Skill Taxonomy + AI Skill Tag Draft.** User job: task author/manager mô tả các kỹ năng thực sự cần để hoàn thành task, theo cách có thể dùng lại xuyên các project trong cùng organization. Manual tagging luôn khả dụng; AI chỉ đề xuất mapping từ task title/description, accepted checklist và selected authorized sources vào organization skill catalog. Output `task_skill_suggestion.v1` gồm `taskId`, `sourceVersion`, `suggestions[{skillId, canonicalName, requiredLevel, confidence, rationale, sourceRefs}]`, `unmappedTerms` và `generatedAt`; AI không tự tạo skill mới hoặc tự gắn tag. UI: native “Kỹ năng cần thiết” card trong Project Task Detail, structured rows cho add/edit/remove, review/reject/selective confirm và source-open. Persistence: additive `OrganizationSkill` + `TaskSkillRequirement`; confirmed AI row lưu provenance `AI_CONFIRMED`, manual row lưu `MANUAL`; unique tenant-normalized skill name và task-skill pair, row-version concurrency. Endpoint/job: canonical `task_skill_suggestion`, schema validation, source guard, budget/usage/audit/read-back; manual PUT dùng domain API riêng. Permission: task-manage/project-manage; organization isolation bắt buộc. States/tests: full canonical lifecycle, empty/unmapped, private source deny, provider/timeout/schema-invalid, retry/cancel/cache/stale, duplicate/stale confirm and manual-only feature-off. Dependency: bounded additive migration; no dependency on CAND-016/017. Effort: 1.5–2 person-days when limited to Project Task Detail. Rollback: disable AI suggestion while retaining manual skill tags and stored taxonomy.

**CAND-016 — Evidence-backed Member Skill Profile.** User job: member và authorized manager hiểu member đã có bằng chứng thực hành kỹ năng nào để hỗ trợ phát triển và staffing; đây không phải performance score. Deterministic inputs: confirmed task skill requirements, explicit completion contributors, completion timestamp, task complexity/required level, outcome state và optional self-declared/manager-endorsed signals. Missing evidence means “chưa đủ dữ liệu”, không có nghĩa là “không có kỹ năng”. Output: per-skill evidence band (`emerging`/`practiced`/`experienced`), confidence, recency, verified task count, source links user is allowed to open and explanation; no opaque global ranking. UI: member skill profile with evidence drawer, correction/appeal and visibility controls. AI may summarize evidence but cannot fabricate or promote a band; band calculation is versioned deterministic logic. Privacy: no private task title/source leakage across project boundaries; aggregate only when viewer lacks source permission; no protected attributes, peer sentiment or message surveillance. Dependency: CAND-015 plus explicit `TaskCompletionAttribution` semantics for multi-assignee work. Effort: 3–5 days. Rollback: disable AI narrative, preserve deterministic evidence ledger/profile.

**CAND-017 — Cross-project Assignment & Schedule Copilot.** User job: portfolio/org manager cân bằng task, assignee, start/due date khi một member tham gia nhiều project. Authorized inputs: open task requirements, task dependencies, estimates, current assignments, CAND-016 evidence bands, working capacity/availability/leave windows and only projects within an authorized organization scope. Hard constraints and overload math are deterministic; AI proposes and explains trade-offs. Output `assignment_schedule_proposal.v1` gồm per-task candidate, proposed assignee/start/due, skill coverage, load before/after, dependency conflicts, deadline risk, alternatives and metric/source refs. UI: Workload/portfolio review board with before/after timeline; manager can edit, select, reject and explicitly confirm each change. AI never auto-assigns or silently changes deadlines. Confirmation requires per-project permission, source row versions, idempotency, transaction/audit and partial-selection rules. Tests: cross-tenant/project deny, private aggregate redaction, no availability, infeasible deadline, overload, stale/concurrent change, selective confirm/rollback/read-back. Dependency: CAND-015 → CAND-016 → CAND-006, organization portfolio permission and deterministic availability/capacity model. Effort: 5–8 days minimum; must be split before implementation. Rollback: disable proposal generation, keep deterministic workload/calendar and manual assignment.

**CAND-018 — Conversational Intent-to-Action / AI Action Composer.** User job: nói điều muốn đạt được thay vì tự tìm module và điền nhiều form; Qaly hiểu intent, tự hoàn thiện một structured draft, đưa 1–3 option có trade-off thật, cho sửa/chọn từng command và chỉ thực thi sau explicit confirmation. Global trigger nằm tại AppShell; contextual trigger truyền route/module/entity/selection nhưng server phải re-resolve authorization. Primary chỉ hỗ trợ tạo một hoặc nhiều Task trong một Project đã resolve; input là message + context identifiers, authorized project/task/member/workload/skill/deadline sources. Output là `ai_action_intent_envelope.v1` với intent/confidence/assumptions/missing fields/grounded action sets và registered `task.create.v1` commands. Assignee suggestion chỉ được ghi là `workload_only` khi CAND-016/006 chưa tồn tại; không được tuyên bố skill-fit. UI là shared composer drawer với honest lifecycle, editable task rows, option selection, selective confirm, execution receipt/deep links và compact process chip mở được timeline operational theo backend event; timeline không lộ chain-of-thought. Backend dùng canonical job/draft/source/privacy/budget/usage/audit/read-back, persisted `ai_action_activity_event.v1`, strong-model profile ưu tiên DeepSeek V4 Pro khi runtime registry xác nhận eligible, schema + semantic reconciliation và `execute_action_set` idempotent. Full project/group/meeting/schedule scope là phased backlog dùng cùng framework; không gộp vào Primary. Dependency Primary: existing TaskService, CAND-015 skill catalog, canonical job/draft và một bounded additive `AiJobActivityEvent` migration vì `AiJob` hiện không có ordered event collection. Effort: 1.75–2 person-days với task-only bounds. Rollback: disable `AiActionComposer:Enabled`, giữ manual forms/chat/read-only AI và persisted receipts/activity.

## 9. Primary Slice được chọn và lý do

### PRIMARY SLICE — CAND-001: AI-08 Grounded Project Progress Summary

Maps: `SURF-006`, `SURF-048..051`; `AI-CAP-003`, `AI-CAP-013`; `GAP-001`, `GAP-002`, `GAP-003`, `GAP-018`, `GAP-019`, `GAP-021`, `GAP-022`.

Lý do chọn:

1. Đóng một locked AI contract đang Partial theo source, không tạo capability mới trước AI-01..08.
2. Không cần migration hoặc destructive mutation.
3. Tận dụng canonical job/source/privacy/budget/cache/usage/audit/read-back hiện có.
4. Có native context rõ: Project Stats, nơi manager đang xem đúng metrics.
5. Có thể chứng minh hoàn chỉnh bằng unit + integration + web-feature + browser E2E trong 1–2 person-days.
6. Làm sâu một workflow: deterministic SQL metrics → AI narrative grounded → native lifecycle card → source open/read-back.

Không dùng Dashboard synchronous strategy làm Primary vì nó không đóng locked AI-08 trực tiếp. Không dùng AI-06/07 vì domain persistence chưa tồn tại và sẽ vi phạm quota-fit/no-stub gate.

## 10. Stretch Slice

### STRETCH SLICE — CAND-002: AI-08 Grounded Sprint Progress Summary

Maps: `SURF-052`, `SURF-053`; `AI-CAP-003`, `AI-CAP-015`; cùng `GAP-001/002/003/021/022`.

Stretch qua gate vì dùng cùng `progress_summary.v4`, context builder, metric reconciler, job lifecycle client và renderer với Primary; chỉ khác scope query `sprint`. Không có migration hoặc confirmation action riêng. Chỉ bắt đầu khi mọi Primary test xanh và không có open defect P0/P1. Nếu quota không đủ, bỏ Stretch hoàn toàn; không để button/stub/partial state.

## 11. Decision-complete contract cho selected slices

### 11.1 API, I/O và schema

**Primary endpoint giữ route:** `POST /api/ai/projects/{projectId}/progress-summary`.

Request body tối thiểu:

```json
{
  "period": "current_snapshot",
  "language": "vi",
  "providerHint": "auto",
  "maximumEstimatedCostUsd": 0.05,
  "cacheMode": "use"
}
```

Không nhận `sourceText`, metric, source IDs hoặc prompt do client cung cấp. Header bắt buộc: `Idempotency-Key`, `X-CSRF-TOKEN`; `X-Request-Id` optional. Role: Project Owner/Manager/ScrumMaster hoặc system/org admin có project-manage permission. Read-only project members không được tạo summary toàn dự án vì aggregate private-task leakage; họ chỉ có thể đọc job do chính họ tạo nếu sau này policy mở rộng.

Accepted response dùng canonical `AiJobCreatedDto` với `jobId`, `status=queued`, `pollUrl`, `resultUrl`, `requestId`.

Runtime job types tách rõ:

- Primary: `project_progress_summary`.
- Stretch: `sprint_progress_summary`.
- Cả hai resolve `schemaId=progress_summary.v4`, `schemaVersion=4.0`.

`progress_summary.v4` đề xuất:

```json
{
  "scope": {
    "type": "project",
    "id": "guid",
    "name": "Qaly v4"
  },
  "period": {
    "kind": "current_snapshot",
    "snapshotAt": "2026-07-26T16:00:00Z",
    "timezone": "Asia/Saigon"
  },
  "coverage": {
    "taskCount": 18,
    "visibility": "manager_full_project",
    "dataState": "sufficient"
  },
  "metrics": {
    "total": 18,
    "done": 7,
    "inProgress": 5,
    "todo": 6,
    "overdue": 3,
    "dueSoon": 2,
    "completionRate": 38.89
  },
  "summaryPoints": [
    {
      "text": "Tiến độ cần can thiệp vì có 3 nhiệm vụ quá hạn.",
      "metricRefs": ["overdue", "completionRate"],
      "sourceRefs": ["project:guid", "task:guid"]
    }
  ],
  "risks": [
    {
      "code": "OVERDUE_CONCENTRATION",
      "severity": "high",
      "title": "Công việc quá hạn tập trung ở milestone hiện tại",
      "metricRefs": ["overdue"],
      "sourceRefs": ["task:guid"]
    }
  ],
  "nextActions": [
    {
      "title": "Rà soát ba nhiệm vụ quá hạn",
      "rationale": "Giảm rủi ro trễ milestone",
      "metricRefs": ["overdue"],
      "sourceRefs": ["task:guid"]
    }
  ],
  "sourceRefs": [
    {
      "key": "task:guid",
      "type": "task",
      "entityId": "guid",
      "label": "QALY-142 — Fix auth",
      "url": "/projects/guid/tasks/guid",
      "version": "row-version-or-updated-at"
    }
  ],
  "warnings": []
}
```

Rules:

- Server, không phải model, sở hữu `scope`, `period`, `coverage`, `metrics`, `sourceRefs`.
- Model chỉ tạo `summaryPoints`, `risks`, `nextActions` từ snapshot server cấp.
- Post-processor reject output nếu `metricRefs` không tồn tại, `sourceRefs` không thuộc authorized snapshot, severity/code sai enum, hoặc model cố thay metric/scope.
- Mọi factual narrative item cần ít nhất một `metricRef` hoặc `sourceRef`; item không grounded bị reject, không render.
- `dataState=empty` khi không có progress-contributing task: canonical worker tạo deterministic zero-cost result, không gọi provider, vẫn ghi job/audit/usage với provider class `deterministic`.
- File JSON schema phải có `$id=progress_summary.v4` và là authority được test; không chỉ substring validation.

Stretch request giống Primary và thêm route `sprintId`; server không tin `projectId`/`sprintId` trong body. Output `scope.type=sprint`. Wrong-project sprint trả 404/403 không phân biệt existence.

### 11.2 Data flow

```mermaid
flowchart LR
  A["Project Stats native card"] -->|"POST + Idempotency-Key"| B["Progress summary controller"]
  B --> C["Project-manage authorization"]
  C --> D["Server-owned SQL snapshot builder"]
  D --> E["Source capture: project/sprint + task-state hash"]
  E --> F["Privacy + budget + cache + canonical job"]
  F --> G["Worker / provider routing"]
  G --> H["JSON schema validation + metric/source reconciliation"]
  H --> I["Job result + usage + audit"]
  I -->|"poll/read-back"| A
  A --> J["Open project/task source link"]
```

Snapshot rules:

- Query only `!IsDeleted`, `ContributesToProgress=true`, status not `Cancelled`.
- Normalize status through existing task status rules.
- Overdue = not done and due date before server `snapshotAt`; dueSoon uses one documented 48-hour window.
- Project summary requires manage permission, therefore private task aggregate is authorized. If any private/restricted source participates, force `Sensitive=true`; client cannot downgrade.
- Project/sprint source state hash must include task id, row version/updated timestamp, status, priority, due date and progress contribution flag in stable order. A task mutation makes cache/source stale.
- Prompt contains only server-built metric snapshot and bounded top-risk task facts. No client prompt passthrough.
- Cache key includes job type, scope id, schema version, language and source hashes.

### 11.3 Native UI và honest lifecycle

Primary card nằm trong `ProjectStatsTab`, nhận `projectId` và permission capability từ `ProjectDetailPage`.

| State | Required UI behavior |
|---|---|
| Idle | “Tạo báo cáo tiến độ AI”; giải thích dữ liệu nào sẽ được dùng. |
| Queued | Job id ngắn, timestamp, spinner, nút Cancel. |
| Running | Progress/attempt state thật từ job; không hiển thị seed/mock như result. |
| Success | Authoritative metrics, grounded narrative, risk/action cards, source links, generated time, cache/provider label an toàn. |
| Empty | “Chưa đủ task đóng góp tiến độ”; zero metrics; không gọi/giả model. |
| Degraded/mock | Chỉ render nếu policy cho phép và `isMock=true`; badge “Mô phỏng/Degraded”, không gọi đó là live insight. |
| Error | Map permission, privacy, budget, provider timeout/unavailable, schema invalid, source stale; retry chỉ khi backend `LastErrorRetryable`. |
| Canceled | Giữ lịch sử/read-back; cho tạo job mới, không reuse result. |
| Reload | On mount gọi list jobs theo project, chọn latest `project_progress_summary`, rồi get detail/result. URL/deep-link optional nhưng state phải phục hồi. |
| Stale result | Khi source guard hoặc current snapshot hash khác, badge “Dữ liệu đã thay đổi”; không dùng cache cũ; offer generate new. |

Không có mutation trong Primary/Stretch, vì vậy không tạo draft và không có confirm action. Test phải assert `draftIds=[]`, không domain row bị sửa. Narrative “next action” chỉ là recommendation/link, không phải executable action.

### 11.4 Permissions, privacy, budget, audit và compatibility

- Create: project-manage permission; read/cancel/retry: canonical job visibility plus existing role checks.
- Tenant ID lấy từ project organization server-side. Mọi task/sprint query buộc project scope.
- Private/restricted task → sensitive processing policy; local/cloud eligibility và consent/retention được evaluate trước dispatch và khi retry.
- Budget/hard-stop errors giữ structured code, không đổi thành success/mock.
- Audit events: request accepted, source/policy decision, provider attempt, success/failure/cancel/retry, source-stale. Usage ledger: một row mỗi provider attempt; deterministic empty path zero-cost row.
- Idempotency replay cùng request/hash trả cùng job; khác snapshot/request với cùng key trả conflict.
- Không migration. Existing generic `progress_summary` seeded jobs vẫn readable; UI chỉ auto-restores new explicit job types. Legacy route payload fields không được silently trusted.
- Feature-disable: `AI_JOB_V4_ENABLED=false` hoặc capability flag `AiFeatures:ProjectProgressSummary=false`; card hiển thị unavailable, deterministic stats vẫn hoạt động.

## 12. Implementation task breakdown

### Primary — tối đa ba task

| Task | Scope | Trace mapping | Acceptance |
|---|---|---|---|
| TASK-P1 | Khóa `progress_summary.v4`; thêm server snapshot/context builder, project-manage/privacy rules, source-state hash, capability prompt, deterministic empty path và metric/source reconciler; enqueue explicit project job type. | SURF-006/048..051; AI-CAP-003/013; CAND-001; GAP-001/002/003/018/019; TEST-P01..P10 | Endpoint không nhận client metric/prompt; source stale/cache/schema/privacy/budget behavior có unit/integration proof. |
| TASK-P2 | Thêm native Project Progress AI card trong Stats; API client/poll/cancel/retry/read-back/source-open; đủ lifecycle và feature-off/degraded label. | SURF-006/048..051; AI-CAP-003/013; CAND-001; GAP-001/022; TEST-P11..P15 | Reload phục hồi latest job; no stub/raw JSON/fake success; deterministic stats không regression. |
| TASK-P3 | Thêm unit, integration, web-feature và Playwright evidence; chạy full relevant gates và record exact output trong PR/goal handoff. | Tất cả mapping Primary; GAP-021; TEST-P01..P15 | Tất cả test matrix xanh; no draft/domain mutation; AI/chat/Week 1 regression xanh. |

### Stretch — tối đa ba task, chỉ sau Primary xanh

| Task | Scope | Trace mapping | Acceptance |
|---|---|---|---|
| TASK-S1 | Extend context builder/reconciler cho sprint, explicit `sprint_progress_summary`, wrong-project/private/stale checks. | SURF-052/053; AI-CAP-003/015; CAND-002; GAP-001/003; TEST-S01..S06 | Reuse schema/platform; không branch logic copy-paste hoặc migration. |
| TASK-S2 | Reuse renderer/lifecycle client trong active milestone detail; read-back latest job đúng sprint. | SURF-052/053; AI-CAP-015; CAND-002; GAP-001/022; TEST-S07..S10 | Full states; không ảnh hưởng milestone CRUD. |
| TASK-S3 | Add sprint integration/E2E/regression tests và chạy Definition of Done. | Same mappings; GAP-021; TEST-S01..S10 | Chỉ merge khi toàn bộ Stretch xanh; nếu không, revert/hide Stretch hoàn toàn. |

## 13. Test/evidence matrix

Các file test dưới đây là target của lượt implementation tiếp theo; **chưa được tạo trong lượt audit**.

| TEST-ID | Scenario | Target project/spec và assertion |
|---|---|---|
| TEST-P01 | Authorized happy path | New `tests/Qaly.IntegrationTests/AiProgressSummaryApiTests.cs`: manager POST → queued → validated result; metric values reconcile SQL. |
| TEST-P02 | Empty/minimal data | Unit + integration: zero progress task → deterministic `dataState=empty`, no provider call, zero-cost usage/audit. |
| TEST-P03 | Cross-project/cross-tenant deny | Integration: manager A cannot request/read/cancel/retry project B; route does not leak existence. |
| TEST-P04 | Private/restricted source | Unit `AiProgressSummaryContextTests`: non-manager denied; manager request forced sensitive; policy/consent deny is explicit. |
| TEST-P05 | Provider unavailable/timeout | Extend `AiJobProcessorTests`/gateway tests: retryable code, queued→retrying/failed truth, UI retry gating. |
| TEST-P06 | Invalid schema + repair exhaustion | Extend `AiGatewayRouterTests`: bad metric/source refs and malformed JSON fail `AI_SCHEMA_INVALID`; no unvalidated render. |
| TEST-P07 | Retry/cancel/idempotency | Integration: cancel idempotent, retry policy/budget/source rechecked, duplicate key same hash one job, different hash conflict. |
| TEST-P08 | Cache hit + stale source | Unit/integration: unchanged task state cache hit; task status/row-version mutation invalidates source/cache and returns stale. |
| TEST-P09 | Audit + usage | Integration/database assertion: one attempt ledger, linked job/attempt/audit; failure and deterministic empty recorded correctly. |
| TEST-P10 | No mutation/draft | Integration: `draftIds=[]`; task/project/sprint rows and row versions unchanged after success. |
| TEST-P11 | Native happy/queued/running/success | New `tests/e2e/project-progress-ai.spec.ts`, preferably real test API; render metrics/narrative/source links. |
| TEST-P12 | Reload/read-back | E2E: reload Project Stats while queued and after success; latest `project_progress_summary` restored. |
| TEST-P13 | Error/degraded/offline labels | E2E route fixtures or controlled test provider: provider/budget/privacy/schema/platform-disabled/mock states visibly distinct. |
| TEST-P14 | Cancel/retry and double click | E2E: button disabled/one idempotency key; cancel terminal; retry only when allowed. |
| TEST-P15 | Regression | Existing `ai-activity.spec.ts`, project/task/group navigation and Week 1 task flow remain green. |
| TEST-S01 | Authorized sprint happy path | Integration: manager gets sprint-only reconciled metrics. |
| TEST-S02 | Wrong project/tenant sprint deny | Integration: sprint belonging to another project is 404/403 without leakage. |
| TEST-S03 | Empty sprint | Deterministic empty result, no provider. |
| TEST-S04 | Private/sensitive sprint source | Policy forced sensitive and deny behavior. |
| TEST-S05 | Sprint stale/cache/idempotency | Task move/status change invalidates sprint source; replay semantics correct. |
| TEST-S06 | Schema/provider/audit/usage | Reuse Primary suite parametrized by scope. |
| TEST-S07 | Demo Map native states | E2E renders idle→success and source links in milestone detail. |
| TEST-S08 | Sprint reload/read-back | E2E restores latest matching sprint, not project/other sprint job. |
| TEST-S09 | Degraded/offline/cancel/retry | Same truthful lifecycle behavior as Primary. |
| TEST-S10 | Milestone regression | Sprint/milestone create/edit/task navigation remains green. |

Mutation-only cases `draft edit/reject/selective confirm/concurrent stale confirmation` are **not applicable** to selected read-only slices. Verification method: assert no draft is created and no confirm endpoint/action is presented. This is intentional, not a missing test.

Exact implementation verification commands:

```powershell
npm run typecheck
npm run build
dotnet test tests/Qaly.UnitTests/Qaly.UnitTests.csproj --no-restore --filter "FullyQualifiedName~AiProgressSummary|FullyQualifiedName~AiJobProcessorTests|FullyQualifiedName~AiGatewayRouterTests|FullyQualifiedName~AiSourceGuardTests"
dotnet test tests/Qaly.IntegrationTests/Qaly.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~AiProgressSummaryApiTests|FullyQualifiedName~AiJobApiTests"
dotnet test tests/Qaly.WebFeatureTests/Qaly.WebFeatureTests.csproj --no-restore --filter "FullyQualifiedName~AiProgressSummary"
npx playwright test tests/e2e/project-progress-ai.spec.ts tests/e2e/ai-activity.spec.ts tests/e2e/minh-task-and-settings.spec.ts
dotnet test Qaly_project.slnx --no-restore
git diff --check
git status --short
```

Audit-only checks đã chạy:

- `npm run typecheck` — PASS.
- Focused AI unit tests (`AiJobProcessorTests`, `AiSourceGuardTests`, `AiGatewayRouterTests`, `AiPlatformQueryServiceTests`) — 31/31 PASS.
- `AiJobApiTests` integration — 6/6 PASS; có hai analyzer warnings hiện hữu ở `DashboardController`, không phải audit modification.
- Không chạy frontend build hoặc E2E trong audit.

## 14. File-overlap và merge order

Expected implementation files, không phải files đã sửa trong audit:

| Area | Likely files | Overlap/risk |
|---|---|---|
| Contract/DTO | `docs/schemas/ai/progress_summary.schema.json`, AI workflow DTOs, new progress context DTO/service | Schema ID compatibility with seeded v4 job and existing v3.2 doc. |
| API/workflow | `AiController.cs`, `AiWorkflowService.cs`, source guard/context builder | Preserve canonical idempotency/privacy/budget behavior and existing Week 1 authorization. |
| Worker/validation | `AiJobProcessor.cs`, `AiOutputValidator.cs` or new schema/reconciler service | Shared hotspot with all AI work; Primary must merge before Stretch/other AI capability work. |
| UI | `ProjectDetailPage.vue`, `ProjectStatsTab.vue`, new reusable progress-summary card/client | Preserve task drawer, delay-resolution trigger, AI Activity and Project Stats deterministic cards. |
| Tests | AI unit/integration/web-feature and new E2E spec | Parametrize scope so Stretch reuses, not duplicates. |

Merge order:

1. TASK-P1 contract/backend + unit/integration.
2. TASK-P2 native UI + typecheck/browser spec.
3. TASK-P3 full evidence and regression.
4. Only then TASK-S1 → S2 → S3.
5. Do not parallel-merge AI-03/04/06/07 or budget UI changes into shared `AiWorkflowService`, `AiJobProcessor`, `AiOutputValidator`, `AiActivityPanel` without rebasing on Primary and rerunning all selected gates.

Protected regression scope: current AI chat, AI Activity deep link, task/project/group flow và Week 1 fixes. No legacy route deletion in this increment; deprecation occurs only after its native replacement has production evidence.

## 15. Deferred AI-native backlog

| Priority | Candidate / module | Dependency | Backlog outcome |
|---|---|---|---|
| **IMPLEMENTED** | **CAND-018 AI Action Composer v1 — Task create action set** | Canonical job/draft, Task domain, CAND-015; typed action schema/tool registry; additive activity-event migration | Global/contextual natural-language request → backend-derived process → grounded editable task options → explicit/selective confirm → idempotent create + receipt/read-back. |
| **P0 selection gate next** | CAND-018 Project setup bundle | Primary green + multi-entity atomic/compensation policy | Candidate kế tiếp trong Action Composer network, nhưng phải decision-complete lại atomicity/rollback và quota fit trước khi implementation. |
| P1 | CAND-018 Group/team action adapter | Primary green + group-admin permission/tool contracts | Draft group and membership options; no invite/role mutation without explicit per-command confirmation. |
| P1 | CAND-018 Meeting/schedule action adapter | Primary green + scheduling/timezone/conflict source contract | Draft meeting/schedule and kickoff options; external calendar is blocked until a real integration exists. |
| IMPLEMENTED | CAND-015 Task Skill Taxonomy + AI Skill Tag Draft | Bounded additive taxonomy/task-skill migration; existing canonical job/draft platform | Manual or AI-assisted, human-confirmed required-skill tags on task; foundation for evidence-based staffing. |
| P0 | CAND-016 Evidence-backed Member Skill Profile | CAND-015 + completion attribution/fairness policy | Grounded skill evidence bands with confidence, recency, source visibility and correction path; not a performance score. |
| P0 | CAND-006 Skill/evidence-aware assignee recommendation | CAND-015/016 + assignment confirm action | Recommend the right member for one task using skill coverage and workload, with explanation and explicit confirmation. |
| P0 | CAND-017 Cross-project Assignment & Schedule Copilot | CAND-015/016/006 + capacity/availability + portfolio permission | Editable assignment/deadline plan across authorized projects; deterministic constraints and human-confirmed mutation. |
| P0 | CAND-005 Settings AI usage/budget | Canonical ledger/policy ownership verification | Close P0-04 product surface and remove backend-only status. |
| P0 | CAND-003 AI-07 checklist | Approved persistence/domain model + migration | Native structured checklist draft and selective confirm. |
| P0 | CAND-004 AI-06 breakdown | Parent-child task semantics + migration | Native structured subtasks and idempotent selective create. |
| P1 | CAND-008 AI-04 task draft | Existing canonical create_tasks | Native field editor/source refs/read-back E2E. |
| P1 | CAND-007 AI-03 group summary | Message-range/cache contract | Exact range identity and source-open evidence. |
| P1 | CAND-010 Meeting Checknote | Privacy/import/action mapping | Migrate legacy sync flow to canonical job/draft. |
| P1 | CAND-009 Dashboard Strategic Brief | Reuse CAND-001 context/reconciler pattern | Server-owned workspace snapshot + canonical lifecycle. |
| P1 | CAND-011 Project Planner | Atomic multi-entity draft confirm | Remove direct AI mutation. |
| P1 | CAND-013 Weekly digest | CAND-001 + scheduler/preference API | Replace localStorage-only promise with persisted truth or remove toggle. |
| P2 | CAND-012 Wiki Brief/task draft | Locked AI-01..08 green | Grounded section-aware summary/draft. |
| P2 | CAND-014 Global Task Hub launcher | CAND-003/004/006 | Reuse complete task capabilities; no new backend. |
| P1 non-AI | Project Activity | Project-scoped activity endpoint/query | Fix GAP-015 deterministically. |
| P1 non-AI | Workload/Gantt navigation | Add/authorize tab ids and route state | Fix GAP-016; do not label AI. |
| P2 no-AI | Poll, admin users, org roles, moderator scopes, profile, archive/storage, webhooks | None | Retain deterministic logic; AI not justified. |
| Architecture | Legacy endpoint strangler | Per-capability native replacement green | Remove duplicate planner/group/meeting/assignment/progress routes gradually. |
| Platform | Schema authority/source refs | Primary pattern | Apply actual v4 schema loader/reconciler to remaining capabilities. |

## 16. Coverage Closure Report

| Closure condition | Numerator / denominator | Result |
|---|---:|---|
| Router records inventoried | 24/24 | PASS |
| Page `.vue` inventoried | 16/16 | PASS |
| Embedded tab/card/panel/modal/drawer/flow inventoried | 80/80 | PASS |
| All surfaces dispositioned | 104/104 | PASS |
| AI + dashboard/group/meeting controller routes inventoried | 58/58 | PASS |
| Logical AI capabilities inventoried | 35/35 | PASS |
| Canonical wrapper job types inventoried | 8/8 | PASS |
| JSON schema files inventoried | 8/8 | PASS |
| Draft families inventoried | 7/7 | PASS |
| v4 draft docs reconciled | 26/26 | PASS |
| SURF-ID without status/rationale | 0 | PASS |
| AI-CAP-ID without caller or orphan disposition | 0 | PASS |
| Backend AI orphan without disposition | 0 | PASS |
| Frontend AI promise/orphan without disposition | 0 | PASS |
| Gaps without candidate/defer/no-AI disposition | 0/25 | PASS |
| Selected task without SURF/CAP/CAND/GAP/TEST mapping | 0/6 | PASS |
| Selected acceptance item without repeatable verification | 0 | PASS |
| Primary decision-complete | 1/1 | PASS |
| Stretch selection gate | 1/1; conditional after Primary | PASS |
| Files changed by audit outside this plan | 0 | PASS |

**Coverage gate PASSED.** Mẫu số đã xác định; orphan count bằng 0 sau disposition; Primary đủ decision-complete để giao agent khác mà không cần hỏi lại.

### 16.1 Current amendment closure — 2026-08-01

Phần này supersede **các con số tổng hiện hành** nhưng không xóa baseline lịch sử ở trên.

| Current closure condition | Numerator / denominator | Result |
|---|---:|---|
| Router records inventoried | 25/25 | PASS |
| Page `.vue` inventoried | 17/17 | PASS |
| Historical + post-baseline surface records dispositioned | 110/110 | PASS |
| Runtime AI/dashboard/group/meeting controller routes inventoried | 62/62 | PASS |
| Runtime logical AI capabilities including Task Skill and Task Action Composer | 37/37 | PASS |
| Required-but-missing Action Composer capability | 0; AI-CAP-037 Task-create v1 implemented | PASS |
| Current JSON schema files inventoried | 10/10 | PASS |
| Runtime Action Composer schema inventoried | 1/1 | PASS |
| New gaps without candidate/defer | 0/5 | PASS |
| Total gaps without candidate/defer/no-AI disposition | 0/30 | PASS |
| Current backend or frontend orphan without disposition | 0 | PASS |
| CAND-018 Primary acceptance item without verification method | 0 | PASS |
| Implementation files/evidence without disposition | 0 | PASS; see §20 |

**Current coverage gate: PASS. Product completeness: NOT PASS.** Coverage có nghĩa là mọi runtime/post-baseline/planned required surface và capability đã có disposition. AI-CAP-037 is complete only for bounded Task-create v1; Group/Meeting/Schedule adapters and runtime model registry remain dispositioned backlog.

## 17. Priority amendment — Skill-aware task assignment and cross-project scheduling

**Amendment date:** 2026-07-27 (Asia/Saigon)
**Reason:** Product owner ưu tiên chuỗi capability gắn kỹ năng cho task → ghi nhận năng lực có bằng chứng → đề xuất assignee → cân bằng lịch/deadline xuyên nhiều project. Audit baseline ở đầu file không bị viết lại; phần này bổ sung requirement mới và cập nhật execution priority.

### 17.1 Kết luận và runtime evidence

Plan trước amendment **mới tính một phần** qua CAND-006/AI-CAP-010/018: card hiện tại xếp hạng assignee dựa trên workload trong project, label/keyword của task đã hoàn thành và một số history signal. Nó chưa đáp ứng đầy đủ ý tưởng vì:

- `ProjectLabel`/`TaskLabel` là nhãn tự do theo từng project, có thể là `High Risk`, `Customer Demo`, `Frontend`, `Backend`; chưa có loại `Skill`, canonical identity xuyên project, proficiency hoặc provenance.
- Heuristic hiện coi label của task `Done` được assign cho member là `SkillSignals`; không có completion contributor nên có thể ghi công sai cho task nhiều assignee.
- `ProjectMember` không có capacity/availability/working-hours; workload endpoint chỉ nhìn một project.
- Chưa có evidence profile, confidence/recency, correction path, portfolio permission, cross-project privacy aggregation hoặc assignment/deadline draft confirmation.

Tại thời điểm skill-assignment amendment, catalog có **17 candidate**. §18 bổ sung CAND-018, nên catalog hiện tại có **18 candidate tổng cộng**: CAND-001, CAND-002, CAND-005, CAND-015 và bounded CAND-018 Task-create v1 đã được triển khai; **13 candidate còn lại deferred**. CAND-016/CAND-006/CAND-017 vẫn là chuỗi staffing/scheduling ưu tiên sau Action Composer, phải qua selection gate mới và không được gộp vào cùng implementation run.

### 17.2 Sau mỗi capability, người dùng làm được gì?

| Thứ tự | Capability | Chức năng có thể dùng ngay sau khi hoàn thành | Giới hạn an toàn |
|---:|---|---|---|
| 1 | **CAND-015 — Task Skill Tagging** | Tạo/chỉnh task và gắn thủ công các skill chuẩn như Frontend, Vue, Accessibility, Backend, .NET, PostgreSQL; hoặc nhờ AI đọc nội dung task để đề xuất skill + level, rồi người có quyền sửa/chọn/xác nhận. Skill dùng lại được giữa các project cùng organization. | AI không tự tạo taxonomy, không tự gắn tag và không giao task. |
| 2 | **CAND-016 — Member Skill Evidence** | Member/manager xem từng skill đã có bao nhiêu task hoàn thành được xác nhận, mức evidence, độ mới và link nguồn được phép xem; có thể bổ sung/correct attribution. | Không gọi đây là điểm hiệu suất; thiếu evidence không có nghĩa là yếu; không dùng dữ liệu nhạy cảm/sentiment. |
| 3 | **CAND-006 — Smart Assignee Recommendation** | Tại một task, AI đề xuất người phù hợp dựa trên skill coverage có bằng chứng + workload/availability, hiển thị lý do, confidence, rủi ro quá tải và phương án thay thế; manager review rồi mới assign. | Không auto-assign; mọi mutation là draft, có quyền, row-version và confirm. |
| 4 | **CAND-017 — Cross-project Schedule Copilot** | Với nhiều project trong cùng phạm vi quản lý, AI đề xuất ai làm task nào, start/deadline nào, cảnh báo conflict/overload/dependency và cho phép chỉnh/chọn từng thay đổi trước khi áp dụng. | Hard constraints do deterministic engine kiểm tra; AI không tự đổi assignee/deadline và không lộ task riêng tư giữa project. |

### 17.3 IMPLEMENTED PRIMARY closure

**Implemented:** `CAND-015 — Task Skill Taxonomy + AI Skill Tag Draft`.

Maps: `SURF-006`, `SURF-007`, `SURF-054`, `SURF-057`; reuse `AI-CAP-003`, `AI-CAP-004`; `CAND-015`; `GAP-023`; `TEST-SKILL-01..12`.

Lý do:

1. Là dữ liệu nền bắt buộc cho member evidence, assignee recommendation và cross-project scheduling.
2. Tạo giá trị độc lập ngay: manual skill tag và AI-assisted tag dùng được trước khi có auto-assignment.
3. Migration có phạm vi nhỏ, additive và rollback được; không cần availability/calendar hoặc thay assignment semantics.
4. Reuse canonical job/draft/source/privacy/budget/audit/usage/read-back đã có.
5. Có thể giữ slice trong khoảng 1.5–2 person-days bằng cách chỉ làm native card ở Project Task Detail; Global Task drawer reuse được defer.

**Không chọn Stretch.** CAND-016 cần completion-attribution migration/policy độc lập; CAND-006 chưa được phép suy luận skill từ nhãn cũ; CAND-017 cần capacity/availability và portfolio authorization. Bắt đầu một trong ba capability này cùng lượt sẽ làm Primary nông hoặc tạo profile/schedule không đáng tin cậy.

### 17.4 Decision-complete contract — CAND-015

#### Domain and persistence

- `OrganizationSkill`: `Id`, `OrganizationId`, `Name`, `NormalizedName`, optional `Description`, `IsActive`, timestamps và row version. Unique `(OrganizationId, NormalizedName)`.
- `TaskSkillRequirement`: `TaskItemId`, `OrganizationSkillId`, `RequiredLevel` (`Familiar|Proficient|Expert`), `Provenance` (`MANUAL|AI_CONFIRMED`), `ConfirmedByUserId`, `ConfirmedAt`, timestamps và row version. Unique `(TaskItemId, OrganizationSkillId)`.
- Project không thuộc organization: feature ở trạng thái unavailable/degraded có nhãn rõ; không tạo hidden global tenant và không tự đoán tenant.
- AI suggestion dùng existing persisted `AiJob` + `AiGeneratedDraft` với draft type `TaskSkillSuggestion`; không tạo bảng job song song.

Migration là additive. Nếu persistence thực tế biểu diễn được cùng invariants mà không migration thì ghi evidence và dùng model hiện có; không được đổi mọi `ProjectLabel` thành skill vì label hiện chứa cả risk/category/domain.

#### API, job and schema

1. `GET /api/organizations/{organizationId}/skills?query=&includeInactive=false` — authorized catalog read.
2. `POST/PUT /api/organizations/{organizationId}/skills` — deterministic catalog manage, organization owner/admin only; normalized-name conflict trả `409`.
3. `GET /api/tasks/{taskId}/skills` — authorized task viewer; trả confirmed requirements và row versions.
4. `PUT /api/tasks/{taskId}/skills` — manual replace/selective update cho task manager, yêu cầu task row version.
5. `POST /api/ai/tasks/{taskId}/skill-suggestions` — enqueue canonical job; body chỉ nhận optional authorized selected source IDs, không nhận client-supplied task text, skill names, prompt hoặc tenant/project identity. `Idempotency-Key` và CSRF bắt buộc.
6. Existing job/result/retry/cancel/read-back routes xử lý lifecycle.
7. Existing draft patch/reject/confirm được mở rộng với `apply_task_skills`; confirmation mang selected/edited rows + expected task/draft versions và idempotency key.

Canonical job type: `task_skill_suggestion`. Schema ID: `task_skill_suggestion.v1`.

```json
{
  "schemaId": "task_skill_suggestion.v1",
  "taskId": "guid",
  "sourceVersion": "opaque-server-version",
  "dataState": "ready",
  "suggestions": [
    {
      "skillId": "guid",
      "canonicalName": "ASP.NET Core",
      "requiredLevel": "Proficient",
      "confidence": 0.86,
      "rationale": "Task requires an authenticated API endpoint and EF Core query.",
      "sourceRefs": ["task:guid"]
    }
  ],
  "unmappedTerms": ["specialized framework not present in catalog"],
  "generatedAt": "2026-07-27T00:00:00Z"
}
```

Contract rules:

- `skillId` phải active và thuộc task organization; schema-valid nhưng foreign/unknown ID vẫn fail semantic validation.
- `canonicalName` được reconcile từ database, không được override stored identity.
- `requiredLevel` là enum; confidence trong `[0,1]`; mỗi suggestion có ít nhất một authorized source ref.
- `dataState=empty` khi task không có signal hoặc catalog rỗng. Không fabricated fallback `Frontend`/`Backend`.
- Unmapped terms chỉ là review hint; AI không được tạo organization skill.
- Source-state hash gồm task ID/row version/title/description, confirmed checklist version nếu dùng, selected-source versions và active-catalog version. Thay đổi bất kỳ nguồn nào làm draft stale.

#### Native UI and lifecycle

Native entrypoint: card “Kỹ năng cần thiết” trong Project Task Detail.

- Confirmed skill chips hiển thị level và provenance.
- Manual “Thêm kỹ năng” chỉ search authorized organization catalog.
- “AI đề xuất” hiển thị queued/running/cancel/retry.
- Success mở structured review rows; user edit level, remove/select, reject all hoặc confirm selected.
- Empty phân biệt “catalog chưa có skill” và “AI không tìm thấy skill phù hợp”.
- Provider unavailable/timeout/schema-invalid/policy deny/budget hard-stop được ghi đúng; manual tagging vẫn dùng được khi domain permission cho phép.
- Reload đọc confirmed skills, latest job/draft và stale status; không raw JSON/fake success.
- Source links chỉ mở entity người xem vẫn được authorize.

#### Authorization, privacy and mutation

- Catalog manage: organization owner/admin. Project/task manager được chọn skill có sẵn nhưng không được ngầm create/rename organization skill.
- Suggest/review/confirm: dùng cùng task-manage/project-manage permission với task editing; ordinary viewer chỉ đọc confirmed tags.
- Tenant/project derive server-side từ task → project → organization; cross-tenant mismatch là nondisclosing `404`.
- Private/restricted source buộc sensitive routing/policy; client không thể downgrade.
- Không có member/protected-attribute data trong input CAND-015.
- Manual update và AI confirm đều có optimistic concurrency, idempotency khi cần và before/after audit.
- Feature disable ẩn AI trigger, reject job mới nhưng giữ catalog/manual tags/read-back.

#### Errors and edge cases

- Organization-less project, empty catalog, inactive skill, duplicated normalized name, deleted task, archived project, removed membership.
- Task/source đổi khi job queued/running hoặc draft đang mở.
- Provider unavailable/timeout, retry exhaustion, invalid JSON/schema, unknown skill ID, source-ref mismatch.
- Duplicate idempotency key với request khác, repeated confirm, concurrent manual update và stale draft.
- Skill bị deactivate sau suggestion nhưng trước confirmation.
- AI platform/budget disabled: manual path vẫn truthful và functional.

### 17.5 Implementation breakdown — tối đa ba task

| Task | Scope | Traceability | Done gate |
|---|---|---|---|
| TASK-SKILL-1 | Add organization skill/task requirement model, config, additive migration, repositories/domain APIs, permissions, normalization, concurrency và audit. | SURF-006/007/057; CAND-015; GAP-023; TEST-SKILL-01/02/04/05/10 | Manual skill CRUD/select tenant-safe; migration không destructive; duplicate/stale writes deterministic. |
| TASK-SKILL-2 | Add schema/context/source hash/reconciler, canonical job + draft `apply_task_skills`, privacy/budget/cache/usage/audit/read-back và failure behavior. | AI-CAP-003/004; CAND-015; GAP-023; TEST-SKILL-03..11 | Không trust client prompt/content, không fabricated/foreign skill, selective confirm idempotent/stale-safe. |
| TASK-SKILL-3 | Add Project Task Detail card/structured review; complete lifecycle, manual fallback, feature flag, unit/integration/web-feature/E2E và regressions. | SURF-006/007/054/057; AI-CAP-003/004; CAND-015; GAP-023; TEST-SKILL-01..12 | Reload/read-back works; không UI-only stub; progress/budget/Week 1 flows không regression. |

### 17.6 Test/evidence matrix

| TEST-ID | Required scenario and repeatable evidence |
|---|---|
| TEST-SKILL-01 | Authorized happy path: org admin manages catalog; project manager manually tags task and runs AI suggestion; edits/selects/confirms; reload shows persisted provenance. |
| TEST-SKILL-02 | Tenant/project permission: cross-tenant, wrong-project, removed member and ordinary viewer mutation deny without data disclosure. |
| TEST-SKILL-03 | Structured grounding: only active same-org skills and authorized source refs pass schema + semantic reconciliation. |
| TEST-SKILL-04 | Empty/minimal: empty catalog and content with no skill signal render distinct honest states; no fake fallback. |
| TEST-SKILL-05 | Private/restricted source: sensitive policy route enforced; denied source absent from prompt/output/source links. |
| TEST-SKILL-06 | Provider unavailable/timeout: job fails/degrades honestly; manual tagging remains; retry follows policy. |
| TEST-SKILL-07 | Invalid schema/repair exhaustion: foreign/inactive skill, invalid level/confidence or missing source ref never becomes valid review. |
| TEST-SKILL-08 | Retry/cancel/idempotency/cache: duplicate request same job; changed request conflict; cancel terminal; task/catalog change invalidates cache. |
| TEST-SKILL-09 | Draft edit/remove/select/reject/selective confirm; AI cannot create catalog skill. |
| TEST-SKILL-10 | Stale task/draft/catalog and duplicate confirm return conflict, reload winner and require new review. |
| TEST-SKILL-11 | Reload restores latest job/draft/confirmed tags; before/after audit and usage ledger queryable. |
| TEST-SKILL-12 | Regression: CAND-001/002/005, AI Activity/chat, task CRUD/labels/assignment, task drawers và Week 1 auth remain green. |

Planned commands:

```powershell
dotnet build Qaly_project.slnx --no-restore
dotnet test tests/Qaly.UnitTests/Qaly.UnitTests.csproj --filter "FullyQualifiedName~TaskSkill"
dotnet test tests/Qaly.IntegrationTests/Qaly.IntegrationTests.csproj --filter "FullyQualifiedName~TaskSkill"
dotnet test tests/Qaly.WebFeatureTests/Qaly.WebFeatureTests.csproj --filter "FullyQualifiedName~TaskSkill"
dotnet test Qaly_project.slnx --no-build
dotnet ef migrations has-pending-model-changes --project src/Qaly.Infrastructure --startup-project src/Qaly.Web
Push-Location src/Qaly.Web/ClientApp
npm run typecheck
npm run build
npx playwright test tests/e2e/task-skills-ai.spec.ts tests/e2e/project-progress-ai.spec.ts tests/e2e/sprint-progress-ai.spec.ts tests/e2e/ai-usage-budget.spec.ts tests/e2e/minh-task-and-settings.spec.ts
Pop-Location
```

### 17.7 File overlap, merge order and rollback

Likely hotspots: new domain entities/configuration/migration; `QalyDbContext`; task DTO/service/controller; AI workflow/job processor/source guard/validator/schema registry; `AiController`; Project Task Detail UI; AI platform unit/integration/E2E tests.

Merge order:

1. TASK-SKILL-1 domain/persistence/manual API and tests.
2. TASK-SKILL-2 canonical AI contract/job/draft and tests.
3. TASK-SKILL-3 UI/E2E/full regression.

Không implement CAND-016/006/017 song song trên task/assignment/workload files. Rollback: disable `TaskSkillSuggestionEnabled`, giữ manual taxonomy/confirmed tags; migration additive và không drop dữ liệu đã xác nhận.

### 17.8 Implementation closure — 2026-07-27

CAND-015 hiện có đủ persistence, manual organization/task APIs, canonical AI job, runtime schema artifact, source/privacy/budget/cache/usage/audit controls, persisted draft, selective confirmation, native Project Task Detail card và reload/read-back. Confirmation chỉ được giữ provenance `AI_CONFIRMED` cho skill nằm trong output AI gốc; skill khác phải đi qua manual tagging. Feature flag chỉ tắt AI suggestion, không làm mất manual taxonomy hoặc confirmed tags.

Release gate phải xác minh migration P004/P005, full .NET suite, frontend typecheck/build, full Playwright suite, secret/artifact scan và remote-main reconciliation trước khi commit/push.

## 18. Priority amendment — Conversational Intent-to-Action / AI Action Composer

**Amendment date:** 2026-08-01 (Asia/Saigon)
**Normative priority:** phần này supersede next-primary selection ở §17.8/old NEXT_IMPLEMENTATION_GOAL. CAND-016 vẫn ở backlog và không bị xóa; product owner đã đặt CAND-018 thành capability chưa triển khai có ưu tiên cao nhất. §20 records the later implementation closure.

### 18.1 Executive verdict và runtime evidence

Tại planning baseline trước §20, Qaly đã có các primitive đáng tái sử dụng: canonical `AiJob`/dispatch/retry/cancel, `AiGeneratedDraft`, source guard, privacy/budget/cache/usage/audit, draft edit/reject/confirm, `ToolParameterGuard`, task domain service, task skill taxonomy và selected-provider/model receipt. Khi đó chúng **chưa tạo thành Action Composer**:

1. `TopHeader.vue`/`AppShell.vue` chưa có global `AI Hành động` hoặc global runtime model control. `AiModelSelector` chỉ nằm trong `ErumiChatPanel`.
2. `AI_MODEL_OPTIONS` là static frontend list; nó không chứng minh API key/provider health/model availability. DeepSeek V4 Pro được ghi `live` dù checked-in config chỉ chứa placeholder key.
3. `ErumiChatService.IsWriteIntent` là keyword router cho `tạo/cập nhật/phân công task`; không hiểu project/group/meeting/schedule intent và không có confidence/missing-field contract.
4. Write path gọi `erumi_autonomous_tasks`, nhưng `AiWorkflowService.ResolveSchemaId` không map job type này. Canonical request vì vậy không thể enqueue hợp lệ nếu không có explicit schema.
5. `AgentRunService.StartAsync` yêu cầu `DraftId` ngay từ create response, trong khi canonical job chỉ tạo draft sau worker success. Unit test mock `DraftReady`/`DraftId`; không có runtime integration proof.
6. Generic `AiGateway` tool calling parse XML/regex/ad-hoc JSON, hardcode một write tool, wrap một draft và bypass schema validation cho wrapper response. Nó không hỗ trợ 1–3 option, multi-command preconditions, selective confirm hoặc action-set receipt.
7. Existing `AiTools` có task read/write tools nhưng không có project/group/meeting/schedule action catalog; write tools không được version như domain contract.
8. Client fallback tự tạo quick-action label từ keyword trong câu trả lời; đó không phải callable native action và không được tính capability.

Kết luận tại planning baseline: không sửa flow cũ bằng cách chỉ gắn thêm button. CAND-018 phải tạo một typed orchestration layer mới. §20 confirms that bounded Task-create v1 now uses this new layer; the generic legacy path remains isolated rather than silently counted as native.

### 18.2 Người dùng làm được gì và rollout theo module

| Phase | Module/surface | Ví dụ người dùng nói | AI soạn và đưa option | Mutation gate / dependency |
|---|---|---|---|---|
| **Primary** | Global/AppShell hoặc Project Task context | “Tạo các task để hoàn thiện đăng nhập trước thứ Sáu; chia frontend/backend.” | 1–3 task action sets: title, description/acceptance, priority, estimate, due date, authorized assignee mode, required skill tags và trade-off | `execute_action_set` sau edit/selective confirm; reuse TaskService + CAND-015. |
| Next | Projects | “Tạo dự án landing page trong hai tuần và chia task.” | Project draft + initial task tree + milestones/options | Requires atomic multi-entity create/compensation; không thuộc Primary. |
| Next | Groups/Teams | “Tạo group mobile và đề xuất thành viên.” | Group metadata + proposed membership options | Group-admin policy; invitation/role actions confirm riêng. |
| Next | Meeting/Schedule | “Tạo lịch kickoff tuần sau khi mọi người rảnh.” | Meeting/schedule options, timezone, conflicts, agenda | Requires persisted scheduling/availability source; external calendar không được giả định. |
| Later | Cross-project assignment | “Sắp lại việc tuần này cho Minh trên ba dự án.” | Assignment/deadline action sets with before/after load | Depends on CAND-016 → CAND-006 → CAND-017 and portfolio permission/capacity model. |

“Tự động hoàn thành” trong mọi phase có nghĩa: AI tự hoàn thiện **draft có cấu trúc**, ghi assumption và đưa option; không có nghĩa AI tự ghi dữ liệu.

### 18.3 Unified UI contract

#### Global entrypoint — SURF-107

- AppShell có một cụm nhỏ ở góc phải: runtime model pill + button `AI Hành động`.
- Default label đơn giản: `AI Hành động`; tooltip: “Mô tả mục tiêu, Qaly sẽ soạn phương án để bạn duyệt.”
- Mở shared `AiActionComposerDrawer`; không mở một chatbot thứ hai.
- Current route được prefill. Nếu không resolve được đúng một project, hiển thị authorized project picker hoặc `needs_input`; không gửi toàn workspace cho model chỉ để đoán project.
- Model pill đọc registry server-side. Nó hiển thị requested profile và actual provider/model sau execution; không dùng static `Live` claim.

#### Contextual entrypoint — SURF-108

- Label thống nhất: `Thực hiện với AI`.
- Primary placements: Project Task board/list toolbar và Task Detail drawer/card; cả hai mở cùng drawer.
- Context envelope từ client chỉ là hint: `route`, `module`, `projectId`, `entityType`, `entityId`, `selectedEntityIds`. Server re-resolve toàn bộ entity và quyền.
- Các module Project/Group/Meeting được phép render disabled/feature-planned state chỉ trong dev/admin discovery; production không hiện stub button trước khi adapter tương ứng hoàn chỉnh.

#### Composer/review surface — SURF-109

Default view chỉ cần bốn khối dễ hiểu:

1. Ô “Bạn muốn Qaly làm gì?”.
2. “AI đã hiểu” — intent, target, confidence, assumption và field còn thiếu.
3. “Chọn phương án” — 1–3 option khác nhau thực sự.
4. “Kiểm tra và xác nhận” — editable rows, per-command checkbox, warnings và CTA `Xác nhận thực hiện`.

Advanced detail (sources, permissions, model, usage, request/job ID) nằm trong expandable section, không làm flow chính phức tạp.

#### AI Process Activity — SURF-110

- Khi job bắt đầu, drawer và AppShell hiển thị compact chip dạng `Đang thực hiện · 16 giây ›`; khi kết thúc đổi thành `Hoàn thành trong 1 phút 42 giây ›`, `Cần bạn bổ sung ›`, `Đã hủy ›` hoặc `Không thành công · Thử lại ›` theo trạng thái thật.
- Bấm chip mở timeline theo thứ tự thời gian. Mỗi row có icon/status, nhãn dễ hiểu, thời điểm hoặc duration và safe detail: `Đang hiểu yêu cầu`, `Đang xác định dự án và quyền`, `Đang thu thập dữ liệu được phép`, `Đang soạn phương án bằng DeepSeek V4 Pro`, `Đang kiểm tra schema và ràng buộc`, `Đang chờ bạn xác nhận`, `Đang tạo task 2/4`, `Đã ghi nhận kết quả`.
- Chip chỉ đếm elapsed time từ `startedAt` backend. Stage/status/command count phải đến từ persisted backend event; frontend không tự suy ra bước từ timer, text streaming hoặc animation.
- Khi tổng command xác định được, hiển thị tiến độ định lượng như `2/4 task`; khi không xác định được, chỉ hiển thị stage và elapsed time, không tạo phần trăm giả.
- Timeline phân biệt `queued`, `running`, `waiting_user`, `succeeded`, `warning`, `failed`, `cancelled`, `skipped`; stage retry tạo attempt row mới, không rewrite lịch sử như chưa từng lỗi.
- Chỉ hiển thị **operational trace** đã sanitize: tool/stage công khai, nguồn ở mức label/count được phép, model thực tế, warning và receipt link. Không hiển thị chain-of-thought, hidden reasoning, system/developer prompt, raw provider payload, secret, token, stack trace hoặc nội dung private mà viewer không được xem.
- Active job có CTA `Hủy` chỉ khi backend báo cancellable. Failed/retryable có `Thử lại`; `waiting_user` đưa focus tới field/confirmation đang chờ. Completed activity có thể collapse nhưng vẫn đọc lại được sau reload và từ AI Activity.
- Nhiều job đồng thời: chip hiển thị job đang active gần nhất và badge số lượng; menu liệt kê job theo status, không trộn event giữa tenant/project/job.
- Accessibility: `aria-live=polite` chỉ announce stage transition, không announce timer mỗi giây; keyboard mở/đóng timeline, icon luôn kèm text, duration dùng định dạng locale và không phụ thuộc màu.

### 18.4 Khung tiêu chuẩn bắt buộc cho mọi AI Action

| Stage | Contract và gate bắt buộc |
|---|---|
| 1. Understand | Natural-language request → allowlisted `intentType`, target module/entities, confidence, assumptions, missing required fields. Confidence thấp/target mơ hồ chuyển `needs_input`; không đoán entity ID. |
| 2. Resolve Authorized Context | Derive user/tenant/project server-side; capture versioned authorized sources; classify privacy; entity content là untrusted data, không phải instruction. |
| 3. Compose Draft | Strong-profile model nhận bounded context và registered tool definitions; output structured intent envelope. Deterministic schema + semantic/domain validator chạy sau model. |
| 4. Generate Options | Tạo 1–3 action sets có trade-off thật. Nếu chỉ có một phương án hợp lệ thì trả một; không nhân bản wording để đủ ba. |
| 5. Review | Persist `AiActionPlan` draft; render native fields; cho edit/remove/select/reject. Source/version đổi làm draft stale. Chưa mutation. |
| 6. Confirm and Execute | Recheck permission/privacy/source/concurrency/budget-independent domain rules; execute only selected registered commands with idempotency. LLM không chạy trong transaction và không chạm repository/DbContext. |
| 7. Receipt and Read-back | Persist truthful success/partial/failed receipt, created/updated entity links, audit/usage/model receipt; reload restores job/draft/receipt. |

Khung này là Definition of AI Action. Capability/module adapter nào thiếu một stage không được gọi `NATIVE_COMPLETE`.

Mỗi stage phải phát ít nhất một activity event khi bắt đầu và một terminal event khi kết thúc, bỏ qua hoặc thất bại. Activity là telemetry nghiệp vụ có thể kiểm chứng, không phải bản ghi suy nghĩ của model. Event được append-only theo sequence; nhãn public do server map từ allowlist stage code để provider/user content không thể tự chèn HTML hoặc giả trạng thái hệ thống.

### 18.5 API, job, draft và I/O contract

#### Compose endpoint

`POST /api/ai/actions/compose`

Headers: `X-CSRF-TOKEN`, `Idempotency-Key`; optional `X-Request-Id`.

Primary request:

```json
{
  "message": "Tạo task sửa màn hình đăng nhập trước thứ Sáu",
  "context": {
    "route": "/projects/{projectId}",
    "module": "tasks",
    "projectId": "guid",
    "entityType": "project",
    "entityId": "guid",
    "selectedEntityIds": []
  },
  "language": "vi",
  "modelProfile": "action_composer_strong",
  "maximumOptions": 3,
  "maximumEstimatedCostUsd": 0.08
}
```


Rules:

- `message` 1–4,000 characters after normalization; control-character and abuse/rate limits apply.
- Client cannot send `tenantId`, `userId`, raw authorized context, permissions, tool definitions, source versions or system prompt.
- `projectId/entityId` are selectors only. Unknown/cross-tenant/unviewable IDs return nondisclosing 404/403.
- Primary requires exactly one authorized current Project before AI dispatch. Ambiguous global project returns structured `needs_input` choices without provider call when possible.
- Accepted response reuses `AiJobCreatedDto`: `jobId`, queued status, poll/result URLs and request ID.

Canonical identities:

- Job type: `action_intent_compose`.
- Schema: `ai_action_intent_envelope.v1`.
- Draft type: `AiActionPlan`.
- Confirm action: `execute_action_set`.
- Receipt schema: `ai_action_execution_receipt.v1`; persisted in draft confirmation result/read-back without a new table in Primary.
- Activity schema: `ai_action_activity_event.v1`; persisted/readable theo job, không phụ thuộc kết nối browser còn mở. Source audit khóa một bounded additive `AiJobActivityEvent` entity/table vì `AiJob.ProgressPercent` và audit rows hiện tại không bảo đảm ordered per-stage sequence/read-back contract.
- Feature flag: `AiActionComposer:Enabled`; capability flag for Primary: `AiActionComposer:TaskCreateEnabled`.

#### Activity/read-back endpoint và event contract

Primary có thể enrich canonical job read DTO hoặc dùng endpoint tương đương:

`GET /api/ai/jobs/{jobId}/activity?afterSequence={n}`

SSE/WebSocket là tối ưu tùy chọn; polling incremental vẫn là baseline bắt buộc. Cả hai phải đọc cùng persisted event source và tuân thủ cùng job authorization.

```json
{
  "jobId": "guid",
  "status": "Running",
  "startedAt": "2026-08-01T09:00:00Z",
  "lastSequence": 6,
  "cancellable": true,
  "events": [
    {
      "eventId": "guid",
      "sequence": 6,
      "stage": "compose_options",
      "status": "running",
      "publicLabel": "Đang soạn phương án",
      "safeDetail": "Model: DeepSeek V4 Pro",
      "current": null,
      "total": null,
      "attempt": 1,
      "startedAt": "2026-08-01T09:00:09Z",
      "completedAt": null,
      "durationMs": null,
      "retryable": false,
      "receiptLink": null
    }
  ]
}
```

Contract rules:

- `stage` là enum allowlist: `understand_intent`, `resolve_context`, `collect_sources`, `route_model`, `compose_options`, `validate_output`, `await_confirmation`, `execute_commands`, `persist_receipt`, `read_back`.
- `status` là enum allowlist: `queued`, `running`, `waiting_user`, `succeeded`, `warning`, `failed`, `cancelled`, `skipped`.
- `(jobId, sequence)` unique và tăng đơn điệu; client deduplicate theo `eventId/sequence`. Out-of-order delivery được sort, sequence gap được refetch.
- `publicLabel/safeDetail` là server-generated localization key/render data; không nhận HTML và không chứa raw model reasoning. `current/total` chỉ dùng cho bounded command execution.
- Authorization giống job detail: owner/admin theo policy hiện có, tenant/project recheck ở mỗi read; expired/unauthorized job không leak existence.
- Retention theo canonical AI audit policy. Usage/activity có correlation ID nhưng activity endpoint không expose token cost/private audit payload nếu role không đủ quyền.

#### Intent envelope

```json
{
  "schemaVersion": "1.0",
  "requestId": "opaque-request-id",
  "conversationId": "opaque-conversation-id",
  "userIntent": "Tạo task sửa màn hình đăng nhập trước thứ Sáu",
  "intentType": "task.create",
  "confidence": 0.94,
  "targetModule": "tasks",
  "targetEntities": [
    { "type": "project", "id": "guid", "label": "Qaly Web" }
  ],
  "authorizedContextReferences": ["project:guid", "project-members:guid", "skill-catalog:guid"],
  "assumptions": [
    { "field": "priority", "value": "High", "reason": "Deadline gần", "editable": true }
  ],
  "missingRequiredFields": [],
  "proposedActionSets": [
    {
      "optionId": "balanced",
      "label": "Cân bằng tiến độ",
      "rationale": "Tách frontend và backend để có thể làm song song.",
      "tradeOffs": ["Cần hai lượt review trước khi merge"],
      "commands": [
        {
          "commandId": "cmd-1",
          "toolName": "task.create.v1",
          "arguments": {
            "projectId": "guid",
            "title": "Hoàn thiện UI đăng nhập",
            "description": "...\n\nAcceptance criteria:\n- ...",
            "priority": "High",
            "dueDate": "2026-08-07T10:00:00+07:00",
            "estimatedHours": 8,
            "assigneeMode": "workload_only_suggestion",
            "assigneeIds": ["guid"],
            "skillRequirements": [
              { "skillId": "guid", "requiredLevel": "Proficient" }
            ]
          },
          "preconditions": [
            { "type": "project.version", "value": "opaque" },
            { "type": "member.active", "value": "guid" }
          ],
          "sourceRefs": ["project:guid", "workload:guid", "skill-catalog:guid"],
          "requiredPermission": "task.create",
          "requiresConfirmation": true
        }
      ],
      "expectedOutcome": "Hai task có acceptance rõ và deadline trong phạm vi dự án.",
      "affectedEntities": [{ "type": "task", "state": "new", "count": 2 }],
      "conflicts": [],
      "requiredPermissions": ["task.create"],
      "estimatedImpact": { "taskCreates": 2, "memberLoadDeltaHours": 16 }
    }
  ],
  "warnings": [
    {
      "code": "ASSIGNEE_SKILL_EVIDENCE_UNAVAILABLE",
      "message": "Phân công chỉ dựa trên workload hiện tại; chưa có member skill evidence.",
      "blocking": false
    }
  ],
  "sourceGrounding": [
    {
      "key": "workload:guid",
      "type": "project_workload",
      "entityId": "guid",
      "label": "Workload hiện tại",
      "url": "/projects/guid?tab=capacity",
      "version": "opaque-server-version",
      "observedAt": "2026-08-01T09:00:00Z"
    }
  ],
  "confirmationRequirement": {
    "required": true,
    "mode": "selective",
    "confirmAction": "execute_action_set"
  }
}
```

Schema/semantic rules:

- `intentType`, `toolName`, permission, warning/precondition types và enums là closed allowlists.
- Server owns/reconciles target labels, permissions, source grounding, member/skill identity and versions. Model cannot introduce IDs absent from authorized context.
- Every factual rationale/conflict/assignee/deadline claim has source refs or deterministic metric refs.
- `maximumOptions` is 1–3; output with zero valid option becomes `needs_input`/empty, not success.
- Task titles are unique within action set after normalized comparison; 1–5 tasks per Primary request.
- Project deadline, task date, priority, estimated hours, assignee membership, skill tenant and privacy constraints are revalidated deterministically.
- Until CAND-016/006 is complete, `assigneeMode` may be `unassigned`, `user_selected` or `workload_only_suggestion`; `skill_fit` is rejected.
- Prompt/model text never becomes direct HTML; UI renders escaped structured fields.

### 18.6 Versioned AI Action Tool Contract và catalog

Mỗi tool registration bắt buộc có:

- `toolName`, semantic version, description.
- `inputSchema` và `outputSchema` authority.
- `readOnly` hoặc `mutation`; mutation luôn `planOnlyDuringComposition=true`.
- `tenantScope` và entity-scope resolver.
- `requiredPermission` và authorization handler.
- `privacyClassification` và allowed provider classes.
- `requiresConfirmation`.
- `idempotencyPolicy`.
- `concurrency/versionPolicy`.
- `auditEvent`.
- `readBackMethod` và deep-link builder.
- rollback/compensation behavior.
- registered application command handler; không có handler thì tool không được expose cho model.

Primary read catalog:

| Tool | Purpose | Primary status |
|---|---|---|
| `project.context.read.v1` | Project identity, lifecycle, dates and authorized summary | Required; server may prehydrate. |
| `project.members.read.v1` | Active project members and allowed assignment identities | Required; no protected attributes. |
| `project.workload.read.v1` | Deterministic active-task/hour load within current project | Required for workload-only option. |
| `organization.skills.read.v1` | Active CAND-015 catalog identities/levels | Required when organization exists; honest empty otherwise. |
| `project.deadline_constraints.read.v1` | Project/sprint/dependency date boundaries | Required for proposed due dates. |

Primary mutation catalog:

| Tool | Input/output | Execution rule |
|---|---|---|
| `task.create.v1` | Input maps to validated `CreateTaskDto` plus confirmed skill requirements; output contains task ID/key/row version/deep link | During compose: only create command draft. During confirm: application handler rechecks context and creates selected tasks; no direct `AiTools.CreateTask` invocation. |

Deferred adapters, not exposed until complete:

- `project.create.v1` and `project.bootstrap_tasks.v1`.
- `group.create.v1` and `group.membership.propose.v1`.
- `meeting.create.v1` and `schedule.propose.v1`.
- `task.assign.v1` and `task.reschedule.v1` after CAND-016/006/017.

Provider-native function calling may be used, but all providers must normalize to the same registry contract. Provider without reliable function calling may return schema-constrained JSON; XML/regex parser is not authority for AI-CAP-037.

### 18.7 Execution, receipt, idempotency và read-back

Confirm request uses existing draft endpoint:

`POST /api/ai/drafts/{draftId}/confirm`

```json
{
  "confirmAction": "execute_action_set",
  "rowVersion": "draft-row-version",
  "idempotencyKey": "action-confirm-opaque",
  "confirmationNote": "Chọn phương án cân bằng",
  "editedPayloadJson": "{ selectedOptionId, selectedCommandIds, editedCommands, expectedSourceVersions }"
}
```

Execution rules:

1. Draft phải `PendingReview`, không expired/stale; confirmation key claim dùng optimistic concurrency.
2. Recheck current user permission, tenant/project, privacy policy, current member/skill state và every precondition.
3. Revalidate edited commands with registered input schema; user không thể đổi `toolName`, project, permission hoặc source identity qua payload edit.
4. Execute only selected `task.create.v1` commands through a bounded application action handler. Primary aims all-or-nothing for database writes; if infrastructure side effect cannot join transaction, persist a truthful compensation/pending-side-effect receipt and never retry-create tasks blindly.
5. Same confirmation key + same payload returns the same receipt. Same key + different payload returns 409. A second key on confirmed draft returns the original receipt/409 without mutation.
6. Persist original option, user edits, selected command IDs, before/after audit, created entity IDs and actual model/provider.

`ai_action_execution_receipt.v1` minimum:

```json
{
  "schemaVersion": "1.0",
  "executionId": "draft-or-confirmation-id",
  "selectedOptionId": "balanced",
  "confirmedCommands": ["cmd-1", "cmd-2"],
  "commandResults": [
    {
      "commandId": "cmd-1",
      "toolName": "task.create.v1",
      "status": "succeeded",
      "entity": {
        "type": "task",
        "id": "guid",
        "label": "QALY-151 — Hoàn thiện UI đăng nhập",
        "url": "/projects/guid/tasks/guid",
        "rowVersion": "opaque"
      }
    }
  ],
  "createdOrUpdatedEntities": ["task:guid"],
  "partialFailures": [],
  "auditId": "guid",
  "provider": "DeepSeek",
  "model": "deepseek-v4-pro",
  "usage": { "ledgerId": "guid" },
  "executedAt": "2026-08-01T09:05:00Z",
  "readBackLinks": ["/projects/guid/tasks/guid"]
}
```

Draft detail/read-back DTO phải expose confirmation receipt cho confirmed Action Plan; reload drawer bằng route query `aiAction=1&aiJob=&aiDraft=` hoặc equivalent stable state.

### 18.8 Data flow

```mermaid
flowchart LR
  A["Global/contextual AI Hành động"] --> B["Compose API + idempotency"]
  B --> C["Resolve user, project, permission"]
  C --> D["Capture authorized versioned context"]
  D --> E["Privacy, budget, strong-model routing"]
  E --> F["Function/schema constrained composition"]
  F --> G["Schema + semantic + domain reconciliation"]
  G --> H["Persist AiActionPlan draft + 1–3 options"]
  H --> I["Editable review/select/reject"]
  I -->|"explicit confirm"| J["Recheck permission/source/concurrency"]
  J --> K["Registered application command handler"]
  K --> L["Receipt + audit + read-back links"]
```

No edge lets the model call a mutation handler directly. Read tool results are bounded, authorized and recorded as sources.

### 18.9 Honest lifecycle

`idle → understanding → resolving_context → needs_input | queued → drafting → options_ready → awaiting_confirmation → executing → success | partial_success | failed → read_back`

| State/error | Required UI behavior |
|---|---|
| Idle | Show examples and current context; manual forms remain available. |
| Understanding/resolving | Step label and cancel; no fabricated progress percentage. |
| Needs input | Ask only blocking field; project ambiguity offers authorized choices. |
| Queued/running/drafting | Poll canonical job; show retry/cancel only when backend allows. |
| Options ready | Show 1–3 meaningful options, assumptions, sources, warnings and editable commands. |
| Awaiting confirmation | Persist draft; no mutation; show stale/source state. |
| Executing | Disable duplicate confirm; show exact selected command count. |
| Success | Receipt, created task links, actual provider/model, audit/usage metadata. |
| Partial success | Exact succeeded/failed commands and safe next action; never generic success toast. |
| Empty | “Chưa đủ thông tin để tạo phương án”; no fake default task. |
| Permission/privacy deny | Explain safe reason without leaking entity existence/private source. |
| Provider unavailable/timeout | Retryable state or approved fallback; manual form remains. |
| Invalid schema/repair exhaustion/tool hallucination | Terminal validation error; nothing confirmable is rendered. |
| Stale source/concurrent edit | Block confirm, reload latest context and require regenerated/re-reviewed draft. |
| Degraded/offline | Label requested vs actual model; no write-plan generation with unqualified weak fallback. |

### 18.10 Model routing và truthfulness

- Add server-owned model/feature registry or extend health API so UI receives model ID, provider, capability (`structured_output`, `function_calling`), availability, privacy class, budget state and disabled reason.
- Primary model profile `action_composer_strong` prefers configured DeepSeek V4 Pro. The literal UI ID `deepseek-v4-pro` maps server-side to `DeepSeek`; UI never sends API key/base URL.
- Explicit DeepSeek selection is strict only when user intentionally locks it. Default profile may fallback to another policy-approved **strong** model and must display actual provider/model.
- Erumi/Ollama small model is not allowed to make mutation plans merely to keep the feature “working”. It may classify a low-risk route only if evaluation evidence meets threshold; otherwise capability returns degraded/manual-only.
- Sensitive/private context cannot silently fall back to cloud. If DeepSeek is privacy-ineligible and no qualified local model exists, block generation and keep manual task creation.
- Static `AI_MODEL_OPTIONS` must be replaced/overlaid by registry response. `Live`, `Fallback`, `Privacy`, `Budget`, `Unavailable` labels derive from runtime state.
- Usage ledger records every attempt and actual model; action receipt references the winning attempt.

### 18.11 Permissions, privacy, prompt injection và compatibility

- Primary compose/review/confirm is limited to project Owner/Manager/ScrumMaster/system admin until a documented member task-create policy is intentionally opened. Manual task CRUD behavior remains unchanged.
- Tenant/project derives from authorized project. Cross-tenant or foreign member/skill/source IDs are rejected without disclosure.
- Private/restricted task/wiki/chat/file content is excluded unless explicitly selected and policy permits. Sensitive flag is server-forced and cannot be downgraded.
- Every entity field, chat message, wiki text, task description, attachment parse and tool result is delimited/tagged as **untrusted data**. Instructions inside source data cannot change system rules, tool registry or confirmation policy.
- Tool least privilege: Primary request exposes only task-create-related read/plan tools. Project/group/meeting mutation tools do not exist in Primary registry.
- Output is schema validated, reconciled against authorized IDs and HTML escaped. Unknown tool/version or parameter is rejected; no fuzzy matching to a similarly named mutation.
- Preserve CAND-001/002/005/015, AI Activity, Erumi read-only chat, Week 1 auth/task/project/group flows. Legacy generic write-intent path remains feature-disabled or routed to Action Composer only after Primary integration evidence; no big-bang deletion.
- Primary reuse `AiJob`, `AiGeneratedDraft`, task, task assignment và task skill tables, nhưng thêm đúng một bounded additive `AiJobActivityEvent` migration với FK job, unique `(AiJobId, Sequence)`, stage/status allowlist fields, safe detail JSON, attempt/timestamps và retention-compatible indexes. Không sửa/destructive existing rows. Nếu receipt không thể biểu diễn an toàn trong `ConfirmationResultJson`, hoặc activity cần platform redesign ngoài table này, dừng và cập nhật plan trước khi mở rộng persistence.

### 18.12 PRIMARY SLICE — CAND-018 Task Action Composer v1

Maps: `SURF-054`, `SURF-057`, `SURF-071`, `SURF-107..110`; `AI-CAP-003`, `AI-CAP-004`, `AI-CAP-036`, `AI-CAP-037`; `GAP-026..030`; `CAND-018`; `TEST-ACTION-01..19`.

Primary boundaries:

- One resolved current Project.
- Intent allowlist: `task.create` only.
- 1–5 new task commands; 1–3 option sets.
- Editable title, description/acceptance, priority, due date, estimate, assignee and confirmed CAND-015 skill rows.
- No project/group/meeting creation, no update/delete, no cross-project reassign/reschedule, no external calendar.
- No autonomous follow-up loop after execution.

Selection rationale:

1. Highest product outcome: turns chat from answer surface into safe completion surface.
2. Closes a proven broken/illusory write path rather than adding another UI-only button.
3. Reuses shipped canonical platform and task-skill capability; persistence mới chỉ là một ordered activity-event table additive, không đổi task/job semantics.
4. Task-only slice can complete end-to-end in 1.5–2 person-days with at most three implementation tasks.
5. Establishes the shared tool/schema/review/receipt standard for every later module.

Definition of Done:

1. Global and contextual native entrypoints use one shared drawer.
2. Request/response/schema/tool versions are fixed and validated.
3. Project/tenant authorization is server-owned.
4. Canonical asynchronous job, draft and read-back are used.
5. Every ID/tool/source is semantically reconciled.
6. Factual/assignment/deadline claims are grounded.
7. Full honest lifecycle is visible through backend-derived elapsed-time chip and persisted operational timeline; no fake percentage or exposed chain-of-thought.
8. User can edit/reject/selectively confirm; no pre-confirm mutation.
9. Confirmation is idempotent/concurrency-safe and audited.
10. Execution receipt links every created task.
11. Provider timeout/unavailable, invalid schema/repair exhaustion, unknown tool, policy/budget deny and stale source are truthful.
12. Reload restores job/draft/receipt.
13. Actual provider/model is shown; strong-model fallback is truthful.
14. Unit + integration + web-feature + E2E evidence passes.
15. CAND-001/002/005/015, AI chat/Activity and Week 1 flows do not regress.
16. Feature flags disable compose/execute without disabling manual task creation.

### 18.13 Stretch decision

**Không chọn Stretch trong NEXT_IMPLEMENTATION_GOAL.** Project setup bundle shares the framework but adds project authorization, multi-entity dependency ordering, atomic creation/compensation and rollback. It is only eligible after Primary is fully green, runtime evidence is recorded and a new selection gate confirms it still fits one independent quota run. Group/Meeting/Schedule adapters are further backlog, not hidden Stretch work.

### 18.14 Implementation breakdown — tối đa ba task

| Task | Scope | Traceability | Done gate |
|---|---|---|---|
| TASK-ACTION-1 | Add `ai_action_intent_envelope.v1`, execution receipt + `ai_action_activity_event.v1` contracts, bounded additive `AiJobActivityEvent` entity/config/migration, typed tool registry, server-owned project/task/member/workload/skill/deadline context builder, `action_intent_compose` job, persisted stage event/read-back, strong-model registry routing and schema/semantic validator. | SURF-107..110; AI-CAP-003/037; GAP-026/027/029/030; CAND-018; TEST-ACTION-01..10, TEST-ACTION-19 | Runtime no longer uses unmapped `erumi_autonomous_tasks`; no XML/regex authority; foreign/unknown tool/ID/source cannot become reviewable draft; activity state comes from authorized append-only backend events. |
| TASK-ACTION-2 | Add `AiActionPlan` draft + `execute_action_set`, editable/selective payload validation, task-create application handler, CAND-015 skill application, idempotency/concurrency/stale checks, receipt/audit/read-back. | SURF-054/057/071/109; AI-CAP-004/036/037; GAP-027; CAND-018; TEST-ACTION-03..15 | No mutation before confirm; same confirmation replays same receipt; created tasks/skills/read-back are correct; partial/failed execution is truthful. |
| TASK-ACTION-3 | Add AppShell model/action control, contextual Task entrypoints, shared composer/options/review/receipt UI, compact elapsed-time activity chip + expandable safe timeline, complete lifecycle/error states, feature flags and unit/integration/web-feature/Playwright regression evidence. | SURF-107..110; all selected IDs; GAP-028/029/030; TEST-ACTION-01..19 | No stub/fake quick action/progress; responsive/light/dark/a11y UI; actual model and backend stage shown; reload restores activity/job/draft/receipt; all selected and regression gates green. |

### 18.15 Test/evidence matrix

| TEST-ID | Required scenario and repeatable evidence |
|---|---|
| TEST-ACTION-01 | Authorized contextual happy path: manager opens Project Task context, asks for tasks, receives valid options and no mutation before confirm. |
| TEST-ACTION-02 | Authorized global path: project picker/resolution selects exactly one visible project; ambiguous name returns `needs_input` without leakage. |
| TEST-ACTION-03 | Minimal input: “Tạo task sửa màn hình đăng nhập” produces editable title/description/acceptance/skill draft and only asks truly blocking fields. |
| TEST-ACTION-04 | Multi-task/options: 1–3 options differ by deadline/workload trade-off; duplicate wording/options rejected; max 5 task commands enforced. |
| TEST-ACTION-05 | Cross-tenant/project/foreign member/foreign skill IDs denied nondisclosing at compose, draft edit and confirm. |
| TEST-ACTION-06 | Private/restricted source deny and sensitive routing; source content/prompt injection cannot add tool or override confirmation. |
| TEST-ACTION-07 | Provider unavailable/timeout: correct retryability; manual task form remains; no fake option or success. |
| TEST-ACTION-08 | Invalid JSON/schema and repair exhaustion: no draft/options rendered as valid. |
| TEST-ACTION-09 | Unknown/unregistered/version-mismatched tool and invalid parameters rejected; no fuzzy fallback or generic execute. |
| TEST-ACTION-10 | Cancel/retry/idempotency/cache: same compose key/hash one job; changed payload conflict; cancel terminal; stale source invalidates cache. |
| TEST-ACTION-11 | Draft edit/remove/reject/selective confirm: only selected validated commands execute; edited tool/project/permission fields cannot escalate. |
| TEST-ACTION-12 | Assignee evidence honesty: before CAND-016/006 only `unassigned`, user-selected or workload-only labels; skill-fit claim is rejected. |
| TEST-ACTION-13 | Stale/concurrent confirmation: project/member/catalog/task source changes block old draft; double click/second key does not duplicate tasks. |
| TEST-ACTION-14 | Execution failure/compensation: exact command receipt, no blind retry-created duplicates, truthful partial/failed UI. |
| TEST-ACTION-15 | Receipt/audit/usage: created IDs/deep links, actual provider/model, ledger/audit correlation and before/after payload are queryable. |
| TEST-ACTION-16 | Reload/read-back: queued job, pending edited draft, confirmed receipt and stale status restore after page reload/deep link. |
| TEST-ACTION-17 | Model truth: DeepSeek eligible route, privacy block, budget block, strong fallback and unqualified local fallback all display actual state/model. |
| TEST-ACTION-18 | Regression: CAND-001/002/005/015, AI Activity/Erumi read chat, task CRUD/skills/assignment, project/group/meeting navigation and Week 1 authorization remain green. |
| TEST-ACTION-19 | Process Activity: ordered backend events render the correct compact label/duration and expanded timeline; unknown duration shows no fake percent; command execution shows `n/total`; retry/sequence gap/duplicate/out-of-order/reload/multiple jobs/cancel/a11y are correct; unauthorized viewer, raw chain-of-thought/private payload and provider HTML never render. |

Planned test locations:

- `tests/Qaly.UnitTests/AiActionComposerServiceTests.cs`
- `tests/Qaly.UnitTests/AiActionToolRegistryTests.cs`
- `tests/Qaly.UnitTests/AiActionOutputValidatorTests.cs`
- `tests/Qaly.IntegrationTests/AiActionComposerApiTests.cs`
- `tests/Qaly.IntegrationTests/AiActionComposerActivityMigrationSqlServerTests.cs`
- `tests/Qaly.WebFeatureTests/AiActionComposerControllerTests.cs`
- `tests/e2e/ai-action-composer.spec.ts`

Planned verification commands, **không chạy trong amendment plan-only này**:

```powershell
dotnet build Qaly_project.slnx --no-restore
dotnet test tests/Qaly.UnitTests/Qaly.UnitTests.csproj --no-restore --filter "FullyQualifiedName~AiAction"
dotnet test tests/Qaly.IntegrationTests/Qaly.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~AiActionComposer"
dotnet test tests/Qaly.WebFeatureTests/Qaly.WebFeatureTests.csproj --no-restore --filter "FullyQualifiedName~AiActionComposer"
dotnet test Qaly_project.slnx --no-build
Push-Location src/Qaly.Web/ClientApp
npm run typecheck
npm run build
npx playwright test tests/e2e/ai-action-composer.spec.ts tests/e2e/task-skills-ai.spec.ts tests/e2e/project-progress-ai.spec.ts tests/e2e/sprint-progress-ai.spec.ts tests/e2e/ai-usage-budget.spec.ts tests/e2e/ai-activity.spec.ts tests/e2e/minh-task-and-settings.spec.ts
Pop-Location
git diff --check
git status --short
```

### 18.16 File overlap, merge order và rollback

Likely implementation hotspots:

| Area | Likely files | Merge/compatibility risk |
|---|---|---|
| Contracts/schema | New AI action DTOs and `docs/schemas/ai/ai_action_intent_envelope.schema.json`; receipt/activity schemas/validators | Do not weaken existing schema validators or reuse `draft_change.v4` as untyped catch-all. |
| Activity persistence | New `AiJobActivityEvent` entity/configuration/additive migration/repository-read contract | Unique ordered sequence per job; safe public payload only; retention/tenant/job authorization; no reuse of unrestricted audit JSON as user-facing text. |
| Workflow/API | `AiController.cs`, `IAiWorkflowService`, `AiWorkflowService`, `AiJobProcessor`, source guard/context builder | Shared hotspots with every AI capability; preserve canonical idempotency/privacy/budget/cache/audit semantics. |
| Tool broker | New typed registry/handlers; limited interaction with `AiTools`, `ToolParameterGuard`, `AiGateway` | Do not add more hardcoded switches/regex parsing; legacy path remains isolated until strangled. |
| Domain execution | Task application service/action handler and CAND-015 task skill service | Preserve manual task policy, row versions, notifications/webhooks and AI-confirmed provenance. |
| Model registry | Gateway settings/health/provider availability DTO/API and frontend model client | Checked-in placeholder keys must never be interpreted as live provider evidence. |
| UI | `AppShell.vue`, `TopHeader.vue`, Task Project/Detail entrypoints, new shared `AiActionComposerDrawer.vue` | Avoid header/sidebar overlap regression; one drawer shared across entrypoints; responsive/dark mode. |
| Tests | New unit/integration/web-feature/E2E plus existing AI/task/Week 1 suites | Replace mocked DraftReady-only proof with runtime create→worker→draft→confirm→receipt evidence. |

Merge order:

1. TASK-ACTION-1 contracts/context/registry/job + unit/integration validation.
2. TASK-ACTION-2 draft/confirmation/execution/receipt + mutation/read-back evidence.
3. TASK-ACTION-3 global/contextual UI + browser/full regressions.
4. Do not merge Project/Group/Meeting/Schedule adapters before Primary closure and a new plan selection gate.

Rollback:

- `AiActionComposer:Enabled=false` hides global/contextual trigger and rejects new compose requests with truthful platform-disabled state.
- `TaskCreateEnabled=false` can disable mutation while leaving previously created job/draft/receipt readable.
- Manual task forms, AI read capabilities and CAND-015 manual skills remain operational.
- Activity migration is additive and retained on rollback; code/flag rollback stops new event writes and hides the chip without deleting receipt/activity/audit/usage history.

## 19. NEXT_IMPLEMENTATION_GOAL

> Implement only `CAND-018 — AI Action Composer v1: Task create action set` as specified in §18 on branch `main` after reconciling the latest remote without overwriting user changes. Preserve CAND-001/002/005/015 and Week 1 flows. Complete at most TASK-ACTION-1..3 end-to-end: typed `ai_action_intent_envelope.v1` and tool registry; canonical `action_intent_compose` job with authorized Project context and strong-model profile preferring runtime-eligible DeepSeek V4 Pro; persisted `AiActionPlan` with 1–3 meaningful editable task options; `execute_action_set` selective/idempotent confirmation through application services; truthful receipt/read-back/audit/usage; global AppShell `AI Hành động` plus contextual Task entrypoints; and bounded additive `AiJobActivityEvent` persistence with `ai_action_activity_event.v1`, rendered as a compact elapsed-time process chip plus expandable, accessible, backend-derived operational timeline that survives reload and never exposes chain-of-thought/private payload. Do not implement Project/Group/Meeting/Schedule adapters, CAND-016/006/017, autonomous mutation, generic XML/regex execution, fake client progress or UI-only stubs. Run TEST-ACTION-01..19 and the exact regression commands in §18.15. Choose no Stretch; stop and update this plan if safe multi-task confirmation/activity persistence requires persistence or platform redesign beyond the one planned additive activity-event table.

**Status:** executed and closed by §20; do not reuse this prompt for a new implementation run.

## 20. CAND-018 Task-create v1 implementation closure — 2026-08-01

### 20.1 Outcome

`CAND-018` Primary is implemented for the bounded `task.create` action set. User can open **AI hành động** globally or from Project Task context, describe desired work in Vietnamese, watch persisted operational steps, review one to three structured options, edit/select task commands and explicitly confirm. Only confirmed commands create tasks; the result contains deep links and can be restored after reload.

DeepSeek V4 Pro is the preferred strong profile, not a fake availability claim. Before routing, UI says **Ưu tiên DeepSeek V4 Pro**. After routing, job/draft/receipt show the actual provider/model returned by the backend. Erumi/local weak fallback cannot be persisted as a grounded native action result.

### 20.2 Traceability and status

| Item | Closure |
|---|---|
| TASK-ACTION-1 | `IMPLEMENTED` — `ai_action_intent_envelope.v1`, authorized Project/member/workload/skill snapshot, `action_intent_compose`, semantic validator, `task.create.v1` allowlist, additive ordered activity persistence/read API and DeepSeek strong-profile hint. |
| TASK-ACTION-2 | `IMPLEMENTED` — persisted `AiActionPlan`, edit/selective `execute_action_set`, protected-field/source/member/skill reconciliation, atomic task/assignment/skill persistence, idempotency/stale guard, audit and execution receipt/read-back. |
| TASK-ACTION-3 | `IMPLEMENTED` — AppShell + Project Task entrypoints, shared responsive drawer, honest elapsed activity timeline, options/editor/confirm/receipt UI, session restore, feature flags and generated production bundle. |
| SURF-107/109/110 | `NATIVE_COMPLETE` for Task-create v1. |
| SURF-108 | `PRESENT_PARTIAL` by design: Project Task complete; Group/Meeting/Schedule adapters deferred. |
| AI-CAP-037 | `NATIVE_COMPLETE` for `task.create.v1`; no credit for unimplemented tool adapters. |
| GAP-026/027/028/030 | Closed for the bounded native path; legacy generic write flow remains isolated/duplicate. |
| GAP-029 | Partial platform gap: truthful strong-profile/actual receipt complete; shared runtime model registry/preference API deferred. |

### 20.3 Security and mutation boundary

- Project/organization and caller permission are resolved server-side; only manager-class roles can compose/confirm in v1.
- Cross-project member/skill/source IDs and protected edits to project/source/intent/tool/version fail closed.
- Provider sees a bounded server snapshot; task private content is not injected. Sensitive source state cannot be downgraded by client input.
- AI only creates a structured draft. Domain rows are written once, atomically, after editable/selective human confirmation.
- Same confirmation idempotency key replays the same receipt; stale source blocks mutation.
- Activity public labels/details are server allowlisted; no chain-of-thought, raw prompt, private source body or provider HTML is exposed.

### 20.4 Evidence executed

| Evidence | Result | TEST-ACTION coverage |
|---|---|---|
| `dotnet test tests/Qaly.UnitTests/Qaly.UnitTests.csproj --no-restore --filter FullyQualifiedName~AiActionComposerContractTests` | **7/7 PASS** | Authorized structured plan; hallucinated tool; foreign member/skill/source; user-selected assignment; protected project/source/intent edits. |
| Related unit regression filter: Action contract + `AiJobProcessorTests` + task-skill contract | **26/26 PASS** | Provider retry/cancel/no fake result, schema/semantic failure, source grounding and CAND-015 compatibility. |
| `dotnet test tests/Qaly.IntegrationTests/Qaly.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~AiActionComposerApiTests` | **3/3 PASS** | Compose/activity→draft→editable confirm→task/assignment/AI-confirmed skill→receipt/audit/read-back; viewer/foreign project deny; stale-source deny; idempotent replay. |
| Related integration filter: Action Composer + Task Skill APIs | **10/10 PASS** | Tenant/private/catalog/cancel/cache/idempotency/feature-disable regression. |
| `AiActionComposerMigrationTests` | **1/1 PASS** | Idempotent SQL contains additive event table, FK, unique `(AiJobId, Sequence)`, read index and no drop table/column. |
| `npm run typecheck` | **PASS** | Vue/TypeScript contracts compile. `vue-tsc` moved from broken published 3.3.8 package to 3.3.9. |
| `npm run build` | **PASS** | Production bundle generated; only relevant `main` and Project Detail tracked artifacts retained. |
| `npx playwright test tests/e2e/ai-action-composer.spec.ts --project=chromium --reporter=list` | **1/1 PASS** | Real login + native header button; mocked AI transport; backend stage timeline/elapsed chip; editable review; confirm payload; receipt/deep link; page reload and receipt restoration; actual model display. |
| `dotnet build Qaly_project.slnx --no-restore` | **PASS, 0 warnings / 0 errors** | Compile/regression gate. |
| `git diff --check` | **PASS** | No whitespace error; only repository line-ending notices. |

### 20.5 Coverage closure for this implementation slice

| Gate | Result |
|---|---|
| Selected tasks mapped to SURF + AI-CAP + GAP + TEST | 3/3 — PASS |
| Selected API/job/schema/caller/renderer without disposition | 0 — PASS |
| Selected acceptance item without repeatable verification | 0 — PASS |
| Pre-confirm or autonomous mutation path added | 0 — PASS |
| Untruthful fake success/progress/model label | 0 — PASS |
| Unbounded Project/Group/Meeting/Schedule adapter hidden in Primary | 0 — PASS |
| Existing user image modifications overwritten | 0 — PASS |

**Implementation coverage gate: PASS.** This means 100% of the bounded Task-create inventory has a disposition and verification method; it does not claim that every deferred Action Composer adapter is implemented.

### 20.6 Rollback and next priority

- `AiJobPlatform:ActionComposerEnabled=false` disables compose/entrypoint while leaving manual Task CRUD and prior read-back intact.
- `AiJobPlatform:ActionComposerTaskCreateEnabled=false` blocks execution without deleting job/draft/activity/audit history.
- Migration is additive and retained on rollback.
- No Stretch was selected or implemented.

`NEXT_IMPLEMENTATION_GOAL` is intentionally not auto-set to another mutation adapter. The next plan gate must compare the **CAND-018 Project setup bundle** against **CAND-008 native task-draft closure** and **CAND-005 AI usage/budget control**, and choose exactly one decision-complete Primary. Project setup wins only if atomic Project + initial Task creation, compensation/read-back and rollback fit one quota run without weakening CAND-018 Task-create v1.

## 21. Priority amendment — Unified Trợ lý AI Workspace, conversational artifact UX và CSRF preview gate

**Amendment date:** 2026-08-02 (Asia/Saigon)
**Request interpretation:** product owner không muốn thêm một form AI riêng cạnh chatbot. Qaly phải có một **Trợ lý AI duy nhất**: người dùng nói mục tiêu, hệ thống hiểu intent trong context hiện tại, chỉ hỏi phần thực sự thiếu, tạo artifact có cấu trúc để xem/sửa/chọn, và chỉ mutation sau explicit confirmation. Ảnh Mentimeter/Typeform được dùng để học interaction pattern `conversation → clarification → suggested artifact → preview → create`, không copy thương hiệu hoặc ép Qaly thành công cụ presentation/form.

Phần này supersede `NEXT_IMPLEMENTATION_GOAL` ở cuối §20. Nó chỉ đặc tả/audit/selection; tại thời điểm amendment này **chưa sửa source** cho Unified Assistant.

### 21.1 Baseline và bằng chứng runtime

- Branch `main`, HEAD `8ec2e926d23f022da76d35f4a95db3fc024fe9aa`, bằng `origin/main` trước các thay đổi đang nằm trong working tree.
- Working tree đã dirty bởi CAND-018 và các ảnh người dùng; amendment này chỉ sửa file plan hiện tại, không overwrite ảnh hoặc source.
- Preview đang được mở tại `http://127.0.0.1:5010`.
- Reproduction thật với login `admin@qaly.dev`:
  - cùng binary, `Production` + HTTP tại cổng 5012: `GET /api/security/csrf` trả **500**;
  - cùng binary, `Development` + HTTP tại cổng 5011: endpoint trả **200** và request token hợp lệ.
- `QalyWebServiceExtensions` đặt antiforgery/auth cookie `SecurePolicy.Always` ngoài Development. Repo không có `launchSettings.json`, nên `dotnet run` local mặc định Production. ASP.NET không thể phát secure antiforgery cookie trên HTTP và controller trở thành 500.
- Data Protection hiện bị đăng ký hai lần, ghi cả `.keys` và `dp-keys`. Đây là config ambiguity cần dọn, nhưng reproduction environment chứng minh trigger trực tiếp của lỗi hiện tại là local preview chạy Production qua HTTP.
- Production vẫn phải giữ secure cookie. Không sửa bằng cách đổi Production sang `SameAsRequest`, bỏ CSRF, tạo token giả hoặc catch exception rồi cho mutation chạy.

### 21.2 Current UX verdict

Hiện có ba đường trải nghiệm không tạo thành một hệ thống thống nhất:

1. `FloatingChatbot.vue` mở drawer 380 px tên **Trợ lý Erumi**, có tab chat và Activity.
2. `/analytics` chứa một Erumi cockpit lớn; header event cũ điều hướng tới route này.
3. `TopHeader.vue` có nút **AI hành động**, mở một `AiActionComposerDrawer` độc lập 680 px với form Project + textarea.

Runtime gap cụ thể:

- Erumi gửi `/api/ai/chat/fast`; write intent vẫn đi qua `AgentRunService`/`erumi_autonomous_tasks`, job không có schema mapping và service đòi `DraftId` ngay khi job vừa enqueue.
- CAND-018 Task Action Composer đã có canonical job/schema/draft/confirm, nhưng Erumi không route vào capability này.
- Frontend tự suy keyword trong reply rồi bịa quick action như “Giao việc cho tôi”, “Gia hạn thêm 3 ngày”; những nút đó không chứng minh callable contract và không được tính AI native.
- Generic chat confirmation gửi `execute_action`, không gửi đầy đủ row version/idempotency/edit payload của CAND-018; không phải native review flow.
- Action Composer là form-first: không có conversational clarification, không giữ assistant response cạnh artifact preview và không cho người dùng thấy “AI hiểu gì / còn thiếu gì” theo turn.
- Chat history chủ yếu ở local browser; không có canonical assistant session/turn read-back.
- Model selector/static labels phân tán giữa analytics/chat/header. Actual model chỉ đáng tin sau job/receipt.
- Poll domain hiện chỉ có một `GroupPoll.Question`, `Options`, `AllowMultiple`, `ExpiredAt`; không có multi-question form, correct answer, score, branching, anonymous mode hoặc presentation deck.

### 21.3 Pattern học từ Mentimeter và Typeform screenshots

| Pattern quan sát | Cách áp dụng đúng cho Qaly | Không được copy/hiểu sai |
|---|---|---|
| AI hỏi một câu làm rõ ngắn trước khi tạo | Hỏi đúng blocking field: Project nào, deadline nào, muốn task hay poll; cung cấp option chip + free text. | Không hỏi lại dữ liệu đã có từ route/entity/permission. |
| Conversation ở cạnh artifact/editor | Desktop split view: transcript bên trái, Task/Poll preview bên phải; mobile chuyển theo step. | Không trả raw JSON hoặc bắt người dùng rời assistant sang modal khác. |
| “Suggested changes” khác “applied result” | Badge rõ `Bản nháp — chưa thay đổi dữ liệu`; edit/select rồi mới `Xác nhận và tạo`. | Không render seed/mock như kết quả vừa chạy. |
| Preview trước CTA cuối | Task cards/poll question/options hiển thị gần giống kết quả domain thật và có validation inline. | Preview không được tự gọi mutation khi mở/chọn option. |
| Một CTA rõ ràng | `Tạo N task`, `Tạo poll` hoặc `Áp dụng thay đổi đã chọn` với confirmation summary. | Không dùng “Yes” mơ hồ cho destructive/multi-command action. |
| Feedback/disclaimer/history | Thumbs feedback gắn job/model/schema; disclaimer ngắn; job/draft/receipt đọc lại được. | Feedback không thay cho test, source grounding hoặc audit. |

### 21.4 Target information architecture

Chỉ còn một global product surface mang tên **Trợ lý AI**:

- Floating launcher hiện tại được đổi accessible name, tooltip và visible compact label thành `Trợ lý AI`.
- Nút `AI hành động` độc lập trên header bị loại bỏ. Nếu giữ shortcut ở header/mobile thì nó chỉ mở **cùng một singleton workspace**, không tạo overlay/state khác.
- `FloatingChatbot` được thay bằng `AiAssistantWorkspace`; tên Erumi có thể giữ như avatar/persona nhỏ (`Powered by Erumi`) nhưng không phải tên capability hoặc model.
- Analytics full page trở thành chế độ `Expand` của cùng workspace; conversation/job/draft không bị reset khi chuyển route.
- Model/privacy/budget control nằm trong assistant header menu. Trước-run chỉ ghi profile ưu tiên; sau-run hiển thị actual provider/model từ job/receipt.
- Activity không còn là silo. Mỗi assistant turn/job có compact process row; tab Activity vẫn dùng cho lịch sử cross-job và deep link.

Desktop layout:

```text
┌ Trợ lý AI ─ context ─ model/profile ─ history ─ close ┐
│ Conversation 38%       │ Artifact / Preview 62%       │
│ user + assistant turns │ Task plan / Poll draft       │
│ clarification cards    │ option tabs + editable rows  │
│ process activity       │ sources + warnings           │
│                        │ review summary + confirm CTA  │
├────────────────────────┴───────────────────────────────┤
│ attachment · prompt composer · send                   │
└────────────────────────────────────────────────────────┘
```

- Khi chưa có artifact, conversation dùng toàn width hợp lý (không để pane trắng).
- Khi artifact xuất hiện, workspace mở rộng tối đa khoảng 1040–1120 px nhưng không che navigation thiết yếu; ở viewport hẹp dùng tabs `Trao đổi` / `Bản nháp`.
- Focus trap, Escape, restore focus, aria-live chỉ announce safe status; không announce mỗi token hoặc raw reasoning.

### 21.5 End-to-end assistant state machine

| Step | Backend truth | UI behavior |
|---:|---|---|
| 1. Open | Route/module/entity IDs từ client chỉ là hint; server chưa tin. | Hiện context chip như `Project · Qaly Native AI`; user có thể đổi entity được phép. |
| 2. User goal | Bounded message, attachments metadata, language, mode=`auto`. | Thêm user bubble; chưa nói “đang tạo task” trước khi route xong. |
| 3. Resolve + authorize | Re-resolve user/tenant/entity/permission và allowed capability adapters. | Process: `Đang kiểm tra context và quyền`. Deny nondisclosing. |
| 4. Intent route | Structured `assistant_turn.v1`: `answer`, `clarify`, `artifact_job`, `unsupported`, `blocked`. | Không dùng client keyword quick action làm authority. |
| 5. Clarify when needed | Trả `questionId`, blocking field, 2–5 allowed choices, free-text policy và max turn. | Hỏi một câu ngắn; chips chỉ điền answer, không mutation. Tối đa 3 clarification turns rồi yêu cầu manual scope. |
| 6. Compose | Adapter Task v1 gọi canonical CAND-018 job; future adapters dùng schema/tool riêng. | Hiện backend-derived activity + elapsed time, cancel/retry/degraded. |
| 7. Suggested artifact | Schema + semantic + source validation thành công, draft persisted. | Split preview; badge `Bản nháp`; show assumptions/missing/warnings/sources và 1–3 meaningful options. |
| 8. Edit/select | Patch chỉ editable fields; protected project/tool/schema/source stay server-owned. | Inline validation; preview cập nhật; remove/select command rõ ràng. |
| 9. Confirm | Re-authorize, stale/concurrency/idempotency check; atomic domain handler. | Confirmation summary nêu chính xác N action, project, assignees, deadline; explicit CTA. |
| 10. Receipt | Persist actual outcome, IDs/links, model/usage/audit; read-back domain entities. | Success/partial/failed truthful; mỗi entity có deep link; reload restores job/draft/receipt. |

### 21.6 Unified assistant contracts

#### `assistant_turn.v1`

Proposed endpoint: `POST /api/ai/assistant/turns` with CSRF, auth, rate/budget/privacy enforcement.

Request:

```json
{
  "previousTurnJobId": null,
  "message": "Tạo các task frontend/backend để hoàn tất đăng nhập trước thứ Sáu",
  "context": {
    "route": "/projects/{id}",
    "module": "project_tasks",
    "entityType": "project",
    "entityId": "{id}",
    "selectionIds": []
  },
  "mode": "auto",
  "language": "vi",
  "modelProfile": "assistant_strong",
  "maximumEstimatedCostUsd": 0.08
}
```

Structured result:

```json
{
  "schemaId": "assistant_turn.v1",
  "disposition": "artifact_job",
  "intent": "task.create",
  "confidence": 0.94,
  "assistantMessage": "Mình sẽ soạn các task để bạn duyệt.",
  "clarification": null,
  "artifact": {
    "kind": "task_action_plan",
    "jobId": "...",
    "schemaId": "ai_action_intent_envelope.v1"
  },
  "sourceRefs": ["/projects/{id}"],
  "actualProvider": null,
  "actualModel": null
}
```

Rules:

- `previousTurnJobId` phải thuộc caller và cùng authorized scope; server đọc previous request/result/draft thay vì tin transcript client gửi lại.
- `clarify` không được kèm domain command. `artifact_job` phải tham chiếu registered adapter/schema/job.
- Model output không được tự chọn tool ID, tenant, project permission hoặc confirmation policy ngoài registry.
- `answer` factual phải có verified source refs/metric grounding; nếu không có thì confidence/degraded state phải trung thực.
- Unknown disposition/intent/artifact/schema fails closed; không fallback sang regex/XML executor.
- Prompt template/version/evaluation case được version-control; no raw chain-of-thought in turn/activity/audit payload.

#### Adapter registry v1

| Adapter | Status in Primary | Job/schema/draft/confirm |
|---|---|---|
| `task.create.v1` | Enabled; reuse CAND-018 | `action_intent_compose` / `ai_action_intent_envelope.v1` / `AiActionPlan` / `execute_action_set` |
| `read.answer.v1` | Reuse existing grounded chat paths only when source contract is satisfied | capability-specific result, no mutation |
| `group.poll.create.v1` | Deferred CAND-020 | `group_poll_compose` / `group_poll_draft.v1` / `AiGroupPollDraft` / `create_group_poll` |
| Project/Group/Meeting/Schedule mutation | Disabled in Primary | no advertised quick action until adapter is complete |

### 21.7 New surface/capability inventory

| SURF-ID | Surface | Disposition |
|---|---|---|
| SURF-111 | Global floating `Trợ lý AI` launcher / optional same-state header shortcut | `MISSING_HIGH_VALUE`; current launcher is Erumi-only and header action is duplicate. |
| SURF-112 | Unified conversation + structured clarification turns | `MISSING_HIGH_VALUE`; current write route is broken legacy and client keywords are not authority. |
| SURF-113 | Split artifact preview/review workspace | `FRONTEND_ONLY/PRESENT_PARTIAL`; Task editor exists in separate composer, not unified with conversation. |
| SURF-114 | Per-turn safe process activity + elapsed time | `PRESENT_PARTIAL`; CAND-018 activity exists but not inside unified assistant turn. |
| SURF-115 | Unified job/draft/receipt history/read-back | `PRESENT_PARTIAL`; Activity panel exists but session/turn linkage and artifact reopen are incomplete. |
| SURF-116 | Group Poll native AI draft preview | `MISSING_HIGH_VALUE`; manual single-poll form only. |
| SURF-117 | Multi-question Form/Quiz/interactive deck builder | `BLOCKED_BY_PLATFORM_OR_POLICY`; domain/storage/results model does not exist. |

| AI-CAP-ID | Contract | Disposition |
|---|---|---|
| AI-CAP-038 | Planned `assistant_turn.v1` router + unified shell, reusing CAND-018 Task adapter | `MISSING_HIGH_VALUE`; Primary candidate CAND-019. |
| AI-CAP-039 | Planned `group_poll_draft.v1` + `create_group_poll` native review | `MISSING_HIGH_VALUE`; CAND-020, after Primary. Multi-question quiz is explicitly not included. |

### 21.8 Gap register

| GAP-ID | Severity | Finding | Resolution disposition |
|---|---|---|---|
| GAP-031 | P0 Blocking | Local `dotnet run` defaults Production+HTTP; CSRF token endpoint returns 500. Data Protection has duplicate key repositories. | Deterministic preflight in CAND-019: explicit Development launch profile, one Data Protection registration, HTTPS/proxy-safe production behavior and real-login CSRF tests. |
| GAP-032 | P0 UX | Separate Erumi floating chat, Analytics chat and header Action Composer create conflicting state/mental models. | CAND-019 singleton `Trợ lý AI` workspace; remove independent header action overlay. |
| GAP-033 | P0 Runtime | Erumi write intent uses broken legacy agent job instead of AI-CAP-037. | CAND-019 route registered `task.create.v1` to CAND-018; legacy write executor disabled/isolated. |
| GAP-034 | P0 Contract | No structured intent/clarification turn; form requires user to know Project/action fields upfront. | `assistant_turn.v1`, server route and max-three blocking clarification turns. |
| GAP-035 | P1 UX | Conversation and artifact cannot be compared/edited side-by-side. | Shared split preview with mobile step fallback and honest draft/apply distinction. |
| GAP-036 | P0 Truthfulness | Frontend fabricates quick actions from reply keywords. | Delete heuristic mutation-like actions; only backend registered adapter action renders. |
| GAP-037 | P1 Lifecycle | Conversation snippets are local; no canonical turn chain/read-back. | Reuse canonical jobs: each turn/result references authorized `previousTurnJobId`; latest job/draft/receipt stored for reload. A dedicated session migration is deferred unless this chain cannot meet retention/query needs. |
| GAP-038 | P1 Product | Group Poll has no AI-native draft/preview/confirm. | CAND-020 single-poll adapter; question/options/multi-select/expiry only. |
| GAP-039 | P0 Product/Data | Mentimeter/Typeform-like multi-question quiz/form cannot be represented. | New later candidate only after `Form/Question/Response` domain, scoring/anonymity/branching policy and migration are approved; no fake bundle of unrelated polls. |
| GAP-040 | P1 Model/QA | Model/profile controls and evidence are fragmented; no end-to-end assistant route evaluation. | CAND-019 server registry truth, actual model receipt, prompt version/eval fixtures and browser matrix. |

### 21.9 Candidate scoring and selection

| CAND-ID | Candidate | Outcome | Gap | Fit | Reuse | Test | Quota | Total | Decision |
|---|---|---:|---:|---:|---:|---:|---:|---:|---|
| CAND-019 | Unified Trợ lý AI Workspace v1 — Task intent/clarification/artifact | 25 | 20 | 15 | 15 | 10 | 13 | **98** | **PRIMARY**; bounded to read answer + `task.create.v1`, one shared shell. |
| CAND-020 | Native Group Poll Draft v1 | 20 | 16 | 14 | 15 | 9 | 12 | **86** | Backlog after Primary; no Stretch because different group permission/schema/domain UI. |

Catalog after amendment: **20 candidates total**. Five bounded capabilities are implemented, CAND-019 is selected Primary, and 14 candidates remain deferred/dispositioned. CAND-020 does not imply multi-question Form/Quiz support.

### 21.10 PRIMARY SLICE — CAND-019 Unified Trợ lý AI Workspace v1

Boundaries:

- One singleton global assistant workspace named `Trợ lý AI`.
- Intent allowlist: grounded read answer, `task.create`, clarify, unsupported/blocked.
- Task action reuses CAND-018 without weakening schema/permission/source/confirm/receipt behavior.
- At most three clarification turns; one blocking question per turn.
- No Project/Group/Meeting/Schedule mutation adapter, no Poll adapter, no multi-question Form/Quiz, no autonomous execution.
- No new persistence migration unless canonical previous-job chain demonstrably cannot restore/retain turns. If migration becomes necessary, stop and re-gate scope before implementation.

Definition of Done:

1. Local preview CSRF works after real login without weakening Production secure cookie.
2. One shared `Trợ lý AI` state; no independent `AI hành động` overlay.
3. Header/floating/context triggers open the same workspace with same current job/draft.
4. Server-owned structured turn route; no client keyword authority.
5. Ambiguous task request yields persisted/reloadable clarification, not guessed mutation.
6. Complete task intent enqueues CAND-018 and shows safe process timeline.
7. Conversation and editable artifact appear in one responsive workspace.
8. No mutation before explicit, precise confirmation summary.
9. Task receipt/deep links/model/usage/audit/read-back survive reload.
10. Legacy Erumi write path and fabricated quick actions cannot execute/render as native.
11. Provider unavailable/timeout/schema-invalid/policy-budget deny/cancel/retry states are truthful.
12. Read-only chat, CAND-001/002/005/015/018, Group chat/AI Activity and Week 1 flows do not regress.
13. Light/dark, keyboard/focus, 1280/1024/768/390 px verification passes.
14. Feature flags can restore current manual/read-only experience without losing history.

### 21.11 Implementation breakdown — maximum three tasks

| Task | Scope | Traceability | Done gate |
|---|---|---|---|
| TASK-UA-1 | Fix local CSRF/runtime config: explicit Development launch profile for HTTP preview, single Data Protection application/key repository, production HTTPS/forwarded-header guard design; add real-auth endpoint tests. Add `assistant_turn.v1` schema/DTO/router contract and registered disposition/adapter validation. | GAP-031/034/036/040; AI-CAP-038; TEST-UA-01..08 | CSRF 200 in supported local profile; Production security not weakened; unknown route/tool/previous job fails closed; no client heuristic authority. |
| TASK-UA-2 | Replace duplicate entrypoints with singleton `AiAssistantWorkspace`; rename UI to `Trợ lý AI`; integrate conversation, clarification, CAND-018 Task preview, process activity, Activity/history and model truth in responsive split/step layout. | SURF-111..115; GAP-032/035/037/040; CAND-019; TEST-UA-09..16 | Same state from every trigger; conversation→clarify/compose→review stays in one surface; no blank pane/overlay collision/header-sidebar regression. |
| TASK-UA-3 | Route `task.create` turns to canonical CAND-018, remove/disable legacy write and fake quick actions, complete confirmation/receipt/reload wiring plus unit/integration/Playwright/regression evidence. | AI-CAP-037/038; GAP-033/036/037; CAND-018/019; TEST-UA-17..26 | No mutation from generic chat; only registered task plan is confirmable; reload/read-back and all safety/failure gates green. |

**Stretch decision:** không chọn Stretch. AI Poll Draft dùng group permission/schema/preview khác; multi-question Form/Quiz cần domain migration độc lập. Gộp chúng sẽ làm Unified Assistant Primary nông.

### 21.12 Test/evidence matrix

| TEST-ID | Required verification |
|---|---|
| TEST-UA-01 | Real login on supported local preview → `/api/security/csrf` 200 + non-empty token; POST with token succeeds. |
| TEST-UA-02 | Production secure-cookie configuration is unchanged; HTTPS request succeeds; untrusted forwarded headers cannot spoof HTTPS. |
| TEST-UA-03 | Data Protection restart/key persistence: auth + CSRF token remains decryptable across normal app restart; one configured repository/application name. |
| TEST-UA-04 | `assistant_turn.v1` valid read/task/clarify/unsupported dispositions pass JSON + semantic validation. |
| TEST-UA-05 | Unknown intent/disposition/schema/tool/adapter and malformed JSON/repair exhaustion fail without renderable artifact. |
| TEST-UA-06 | Previous turn belongs to caller/same tenant/entity; foreign/cross-project ID is nondisclosing deny. |
| TEST-UA-07 | Prompt/source injection cannot change adapter registry, project, permission, confirmation or expose system prompt/raw reasoning. |
| TEST-UA-08 | Budget/privacy/model/provider unavailable/timeout returns honest blocked/degraded/retryable behavior and actual model truth. |
| TEST-UA-09 | Floating launcher is named `Trợ lý AI`; independent `AI hành động` overlay is absent. |
| TEST-UA-10 | Header/context/floating triggers open same singleton state and restore focus on close. |
| TEST-UA-11 | Ambiguous request asks one blocking question with allowed choices; known route fields are not re-asked; max 3 turns. |
| TEST-UA-12 | Clear task request routes to CAND-018 with no legacy `erumi_autonomous_tasks` call. |
| TEST-UA-13 | Split preview shows exact option/task fields, assumptions, warnings and source links; mobile step layout retains edits. |
| TEST-UA-14 | Client reply keyword cannot create mutation-like quick action; backend unregistered action never renders. |
| TEST-UA-15 | Cancel/retry/network loss/sequence duplicate/out-of-order activity remains truthful and accessible. |
| TEST-UA-16 | Light/dark and 1280/1024/768/390 layouts; no sidebar/header overlap or hidden confirm CTA. |
| TEST-UA-17 | No task/assignment/skill row exists before confirmation. |
| TEST-UA-18 | Edit/remove/selective confirm applies only selected validated commands. |
| TEST-UA-19 | Double-click/same idempotency key replays same receipt; stale source/concurrent row version blocks. |
| TEST-UA-20 | Page reload/reopen restores latest turn/job/activity/draft/receipt and actual provider/model. |
| TEST-UA-21 | Reject draft leaves domain unchanged and remains auditable. |
| TEST-UA-22 | Read-only factual answer includes authorized sources; unsupported request offers safe manual route, not fake success. |
| TEST-UA-23 | Viewer/private/foreign project/member/skill source deny remains nondisclosing. |
| TEST-UA-24 | Usage/audit correlation maps assistant turn → action job → draft → execution receipt. |
| TEST-UA-25 | Regression: CAND-001/002/005/015/018, AI Activity, manual Task CRUD, Project/Group/Meeting navigation and Week 1 auth. |
| TEST-UA-26 | Feature-off hides create adapter but keeps manual Task flow, grounded read chat and historical read-back available. |

Planned test locations and commands:

```powershell
dotnet test tests/Qaly.UnitTests/Qaly.UnitTests.csproj --no-restore --filter "FullyQualifiedName~AiAssistant|FullyQualifiedName~AiActionComposer"
dotnet test tests/Qaly.IntegrationTests/Qaly.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~Csrf|FullyQualifiedName~AiAssistant|FullyQualifiedName~AiActionComposer"
dotnet test tests/Qaly.WebFeatureTests/Qaly.WebFeatureTests.csproj --no-restore --filter "FullyQualifiedName~Security|FullyQualifiedName~AiAssistant"
dotnet build Qaly_project.slnx --no-restore
Push-Location src/Qaly.Web/ClientApp
npm ci
npm run typecheck
npm run build
Pop-Location
$env:E2E_BASE_URL='http://127.0.0.1:5010'
npx playwright test tests/e2e/ai-assistant-workspace.spec.ts tests/e2e/ai-action-composer.spec.ts --project=chromium
git diff --check
git status --short
```

### 21.13 CAND-020 deferred contract — Native Group Poll Draft v1

Supported without domain migration:

- Group Owner/Admin says: “Tạo poll chọn lịch retro tuần sau”.
- AI may ask topic/audience/expiry/multi-select clarification.
- Authorized input: group identity, caller role, optional selected non-private message refs.
- Output `group_poll_draft.v1`: one question ≤500 chars, 2–10 unique options ≤300 chars, `allowMultiple`, optional `expiredAt`, rationale/source refs/warnings.
- Unified assistant renders exact Poll preview; user edits and confirms `create_group_poll`; application service revalidates permission/idempotency/stale source and returns poll/result deep link.

Explicitly unsupported until a separate domain slice:

- multiple questions in one form/deck;
- correct answer, score, leaderboard, timer per question;
- anonymous response, branching/logic jumps, free-text/scale/ranking question types;
- presentation mode or importing Mentimeter/Typeform content.

### 21.14 File overlap, merge order and rollback

Likely hotspots:

- CSRF/config: `QalyWebServiceExtensions.cs`, `QalyWebApplicationExtensions.cs`, new local launch profile, Security integration tests.
- Server turn route: `AiController.cs` or a focused `AiAssistantController.cs`, application DTO/service, schema/validator, workflow processor/DI.
- Shared UI: `App.vue`, `AppShell.vue`, `TopHeader.vue`, `FloatingChatbot.vue`, `ErumiChatPanel.vue`, `AiActionComposerDrawer.vue` and generated `main` assets.
- Tests: new assistant unit/integration/web-feature/E2E plus existing action/chat/activity suites.

Merge order:

1. TASK-UA-1 config + contracts + tests.
2. TASK-UA-2 singleton shell/preview with feature flag, while current paths remain available behind flag.
3. TASK-UA-3 canonical Task route, remove legacy/fake path only after end-to-end evidence passes.

Rollback:

- `AiAssistantWorkspaceEnabled=false` restores current read-only Erumi/Activity surface and direct CAND-018 entrypoint during rollout.
- `AiAssistantTaskAdapterEnabled=false` keeps unified read/clarify UI but removes Task mutation affordance.
- Manual Task/Group Poll forms remain available.
- Never rollback by disabling antiforgery or accepting mutation without confirmation.

### 21.15 Coverage closure

| Gate | Result |
|---|---|
| Historical + new surfaces dispositioned | 117/117 — PASS |
| Runtime AI capabilities inventoried | 37/37 — PASS |
| New planned capabilities dispositioned | 2/2 — PASS |
| Runtime JSON schemas falsely counted | 0 — PASS |
| Total gaps with candidate/defer/block disposition | 40/40 — PASS |
| UI/backend orphan without disposition | 0 — PASS |
| Selected Primary acceptance without verification | 0 — PASS |
| Poll/Form feature claimed beyond domain | 0 — PASS |

**Coverage gate: PASS. Product completeness: NOT PASS.** CAND-019 and CAND-020 remain unimplemented at this amendment point; multi-question Form/Quiz remains blocked rather than hidden as a fake capability.

### 21.16 NEXT_IMPLEMENTATION_GOAL

> Implement only `CAND-019 — Unified Trợ lý AI Workspace v1` from §21 on current `main` without overwriting existing CAND-018 or user image changes. Complete TASK-UA-1..3 and no Stretch: fix supported local preview CSRF through explicit Development launch/runtime configuration while retaining secure Production cookies and one Data Protection key ring; add schema-validated server-owned `assistant_turn.v1` routing for grounded read, clarification, registered `task.create.v1`, unsupported and policy-blocked dispositions; replace separate Erumi/AI Action entrypoints with one singleton `Trợ lý AI` workspace using conversational clarification plus responsive artifact preview; route task creation only through canonical CAND-018 job/draft/activity/edit/selective-confirm/receipt; remove legacy `erumi_autonomous_tasks` write routing and client-fabricated mutation-like quick actions after evidence is green. Do not implement Group Poll, multi-question Form/Quiz, Project/Group/Meeting/Schedule mutation adapters, autonomous execution or a new persistence migration unless the previous-job chain fails the reload/retention gate and the plan is re-approved. Run TEST-UA-01..26 and the exact commands in §21.12; keep manual flows and CAND-001/002/005/015/018 green.

### 21.17 Implementation checkpoint — first deep increment

**Checkpoint date:** 2026-08-02 (Asia/Saigon)

Implemented and verified in this increment:

- Local preview now has an explicit `Qaly.LocalPreview` Development profile on `http://127.0.0.1:5010`; Production still requires secure cookies. Data Protection uses one durable `Qaly` key ring. Authenticated `/api/security/csrf` returned HTTP 200 in runtime verification.
- The header and floating control now expose one product name and one singleton entrypoint: `Trợ lý AI`. The standalone header `AI hành động` overlay was removed.
- Clear task-create/assignment language is routed on the server to a structured `compose_task_plan` function-call payload with `schemaId=assistant_turn.v1`, `intent=task.create.v1`, `disposition=registered_action` and `executionPolicy=draft_then_confirm`.
- Natural phrases containing quantities, for example “Tạo 3 task frontend, backend và QA”, are recognized without requiring the exact adjacent phrase “tạo task”.
- The unified workspace follows the Mentimeter/Typeform interaction pattern without copying their unsupported domain: conversation remains on the left and the structured Task artifact appears on the right. The existing canonical CAND-018 job/draft/activity/edit/selective-confirm/receipt pipeline remains the only write path.
- Client-fabricated mutation-like quick actions and direct confirmation of legacy chat drafts were removed. Legacy cards are read-only and explain why they cannot be confirmed under the current contract.
- The narrow assistant drawer no longer allows the model badge to cover the Send button.
- A formal JSON schema exists at `docs/schemas/ai/assistant_turn.schema.json`; the registered Task action is also guarded by the existing `ai_action_intent_envelope.v1` validation before draft/confirmation.

Evidence completed:

| Evidence | Result |
|---|---|
| Vue/TypeScript typecheck | PASS |
| Solution build | PASS |
| Erumi + Action Composer unit regression | 20/20 PASS |
| Action Composer integration tests | 3/3 PASS |
| Unified assistant manual + chat function-call Chromium E2E | 2/2 PASS |
| Authenticated runtime CSRF request | HTTP 200 PASS |
| `git diff --check` | PASS at checkpoint |

Honest disposition after this increment:

- `task.create.v1` through the unified assistant: `NATIVE_COMPLETE` for the registered Task action path, subject to a configured provider for generation.
- CAND-019 as a whole: `PRESENT_PARTIAL`. Grounded-read responses still use the existing Erumi response contract rather than emitting `assistant_turn.v1` for every disposition; multi-turn structured clarification, explicit `unsupported`/`policy_blocked` envelopes and server-persisted assistant sessions remain for the next CAND-019 closure increment.
- Provider/model runtime: `BLOCKED_BY_PLATFORM_OR_POLICY` in the current local preview until DeepSeek credentials or a reachable approved provider are configured. The UI shows a truthful failed/degraded state; it does not claim generation succeeded.
- CAND-020 Group Poll Draft: still deferred. Multi-question quiz/form, scoring, leaderboard, branching and presentation mode remain blocked by missing domain support and are not claimed.

**Next priority order:** close the remaining CAND-019 routing/clarification/persistence contract first; then implement CAND-020 single Group Poll Draft only; schedule a separate domain slice before any Mentimeter/Typeform-style multi-question Form/Quiz capability.

### 21.18 Implementation checkpoint — chat-first contract closure increment

**Checkpoint date:** 2026-08-02 (Asia/Saigon)

This increment removes the intermediate “chat beside a blank manual AI form” behavior. The unified assistant now starts as one conversation surface; the artifact pane is created and opened only after the server returns a registered action.

Implemented behavior:

- `POST /api/ai/assistant/turns` is the canonical drawer turn endpoint. Every successful turn is represented by `assistant_turn.v1` with one explicit disposition: `grounded_answer`, `registered_action`, `clarification_required`, `unsupported` or `policy_blocked`.
- The server, not a client quick-action heuristic, decides whether a request is the registered `task.create.v1` action. The response carries a structured Task artifact and `draft_then_confirm`; it does not perform a domain mutation.
- When the Task request has no authorized Project context, the server returns at most five authorized Project choices. Selecting one keeps the original user intent, adds the selection to the conversation and resubmits it with the selected Project context.
- A registered Task artifact automatically expands the responsive workspace and starts the existing CAND-018 canonical job. Before an artifact exists there is no blank right pane, no duplicate Project selector and no separate “Bạn muốn Qaly chuẩn bị việc gì?” form.
- Existing-task assignment/status/update requests and unregistered Project, Group, Meeting/Schedule, Poll and Form/Quiz mutations return an honest unsupported response with no artifact, job or mutation. Grounded read requests remain read-only and are wrapped in the same turn contract.
- The artifact pane remains review-first: visible activity stages, provider/degraded failure, structured options, editable fields, selective command confirmation, stale-source/idempotency checks, execution receipt and reload/read-back.
- `assistant_turn.schema.json` is copied into the runtime schema output alongside the Action Composer schema. Endpoint integration tests verify authorized action, authorized clarification and unsupported/no-job behavior.

Evidence for this increment:

| Evidence | Result |
|---|---|
| `npm run typecheck` | PASS |
| `dotnet build Qaly_project.slnx --no-restore` | PASS, 0 warnings / 0 errors |
| Targeted Erumi/router/gateway/composer unit tests | 44/44 PASS |
| Assistant Turn + Action Composer integration tests | 6/6 PASS |
| Unified assistant + canonical composer Chromium E2E | 2/2 PASS |
| `assistant_turn.schema.json` JSON parse and runtime copy declaration | PASS |
| Old blank composer copy in application source/runtime bundle | 0 occurrences — PASS |

Updated disposition:

- `task.create.v1` native entry → server route → clarification → artifact → canonical job/review/confirm/read-back: `NATIVE_COMPLETE` for the currently registered adapter.
- CAND-019 chat-first UI and single-turn disposition contract: `NATIVE_COMPLETE` for the current registered/read/deny capability set.
- CAND-019 durable multi-turn session chain: `PRESENT_PARTIAL`; conversation state is still browser-held and `previousTurnJobId`/server-persisted assistant session reload is not implemented in this increment.
- Project/Group/Meeting/Schedule/Poll/Form mutation adapters: `MISSING_HIGH_VALUE` or deferred exactly as catalogued; the assistant reports them as unsupported rather than fabricating success.

**Next priority order after this checkpoint:** implement the server-persisted assistant turn/session chain as the next CAND-019 depth slice; only after it is green, evaluate CAND-020 single Group Poll Draft. Do not add multiple mutation adapters in one quota slice.

## 22. Amendment — Qaly Agent Workspace and Open Assistant Foundation

**Decision date:** 2026-08-02 (Asia/Saigon)

**Baseline:** branch `main`, commit `8ec2e926d23f022da76d35f4a95db3fc024fe9aa`, equal to `origin/main` before this plan checkpoint.
**Workspace note:** the working tree already contains uncommitted AI-native source, test, configuration, image and generated-bundle changes from prior increments. This amendment does not classify those files as new plan work and must be committed by staging this plan file only.

### 22.1 Product decision and normative priority

The intended product is not a Task form wrapped in chat. Qaly must become an agent workspace where a user can state a short outcome, let the assistant inspect authorized context, receive grounded findings and options, and then convert selected options into typed drafts. The assistant may reason broadly, but execution remains closed to registered capabilities:

> **Open-world understanding, closed-world execution.** Read and analysis tools may run after authorization. Every mutation must resolve to a versioned capability, validated draft, explicit human confirmation, domain permission, idempotency, audit and read-back.

This section supersedes the next-priority sentence at the end of §21.18. The new order is:

1. `CAND-021A` — Resizable Agent Workspace Shell v1.
2. `CAND-021B` — Durable Assistant Session/Turn Chain.
3. `CAND-021C` — Context Source Registry + Capability Registry.
4. `CAND-021D` — Grounded Research Plan + Action Graph.
5. Only then select one additional mutation adapter such as CAND-020 Poll or the staffing/scheduling chain.

The sequence is deliberate: do not add breadth through more ad-hoc intent branches or legacy tools before the common workspace, session, context and capability contracts exist.

### 22.2 User outcome

The target interaction accepts requests such as:

- “Phân tích project Alpha và đề xuất những việc quan trọng cần làm tuần tới.”
- “Đọc Wiki, task và PR gần đây, tìm gap trước release.”
- “Đề xuất ba phương án phân công nhưng chưa áp dụng.”
- “Từ phương án 2, soạn task, cuộc họp và lịch dự kiến để tôi duyệt.”

The assistant must determine what it can inspect, show useful progress, ask only blocking questions, cite sources for factual claims, distinguish fact/inference/assumption, and present editable options. It must never imply that unsupported work was executed.

### 22.3 Assistant UI surface inventory

| SURF-ID | Surface | Current disposition | Required disposition |
|---|---|---|---|
| SURF-118 | Floating `Trợ lý AI` launcher | `NATIVE_COMPLETE` as singleton entrypoint | Preserve route-independent singleton and focus restoration. |
| SURF-119 | Agent workspace window | `PRESENT_PARTIAL`; fixed 380 px drawer or fixed 1080 px artifact workspace | User-resizable desktop workspace with bounded size, reset and persisted preference. |
| SURF-120 | Workspace header | `PRESENT_PARTIAL` | Fixed header with title, context/model truth, layout controls, activity and close; never overlap content. |
| SURF-121 | Empty conversation state | `PRESENT_PARTIAL`; composer is vertically centered | Lightweight welcome/suggestions in scroll area; the same composer remains docked at the bottom from the first turn onward. |
| SURF-122 | Active conversation thread | `PRESENT_PARTIAL` | Independently scrollable transcript, follow-latest behavior only when user is already near the bottom, accessible jump-to-latest. |
| SURF-123 | Chat composer | `PRESENT_PARTIAL` | Sticky bottom composer, auto-growing input, attachments/context/model controls, submit/cancel, safe-area padding and no duplicate composer DOM. |
| SURF-124 | Context/source disclosure | `PRESENT_PARTIAL` | Show selected scope, sources read, skipped/denied sources and freshness without exposing private titles. |
| SURF-125 | Agent process/activity | `PRESENT_PARTIAL`; job activity is a separate tab and Task artifact timeline | Contextual collapsible process strip inside the conversation plus full Activity history; expose stages and tool/result summaries, never hidden chain-of-thought. |
| SURF-126 | Artifact/review rail | `PRESENT_PARTIAL`; Task-specific and appears only for `task.create.v1` | Optional resizable/collapsible artifact rail with typed renderers and conversation remaining usable. |
| SURF-127 | Conversation/artifact splitter | `MISSING_HIGH_VALUE` | Keyboard and pointer accessible splitter with min widths and double-click reset. |
| SURF-128 | Mobile/tablet assistant | `PRESENT_PARTIAL` | Full-screen single-column workspace; conversation/artifact/activity are navigable views, composer is never hidden by virtual keyboard. |
| SURF-129 | Resize/error/degraded accessibility states | `VERIFICATION_GAP` | Honest labels, focus containment/restoration, Escape policy, zoom/reduced-motion/high-contrast verification. |

Assistant UI delta coverage: **12/12 surfaces inventoried and dispositioned**.

### 22.4 Detailed UI contract — CAND-021A

#### Desktop geometry

- The assistant remains anchored to the right/bottom application viewport but behaves as an agent workspace, not a permanent full-height navigation drawer.
- Default chat size: `440 × min(760, viewport height − 32)` CSS pixels.
- Default artifact workspace: `min(1120, viewport width − 32) × min(820, viewport height − 32)`.
- Minimum size: `380 × 520`; maximum size: `calc(100vw − 24px) × calc(100vh − 24px)`.
- The user may resize from the left edge, top edge and top-left corner. Width and height are clamped after viewport/zoom changes so the close button and composer always remain reachable.
- Store only presentation preferences—width, height, artifact ratio and collapsed state—in versioned local storage. Do not put entity data, prompts, source text, jobs or drafts in the layout record.
- Provide “Đặt lại kích thước” in the header overflow menu. A corrupt/out-of-range saved value falls back to defaults.

#### Layout

- Root uses three fixed/scroll regions: fixed header, `min-height: 0` content, fixed bottom composer.
- Empty and active states share one composer instance. Welcome copy and suggestions live in the transcript/empty content region above it.
- The transcript scrolls independently. The artifact rail never causes the composer to scroll off-screen.
- When an artifact exists, a splitter controls conversation/artifact width. Default ratio is 40/60; conversation minimum 360 px and artifact minimum 480 px. If the available width cannot satisfy both, switch to a single-view tab layout.
- Activity appears first as a compact stage card in the relevant assistant turn. The full Activity view remains available from the header.
- No raw model chain-of-thought is rendered. Allowed process data: stage name, status, elapsed time, authorized source/tool label, item count, retry/cancel state and safe error code.

#### Input behavior

- `Enter` submits and `Shift+Enter` inserts a line break; IME composition must not submit early.
- Input grows to a bounded height, then scrolls internally.
- Submit changes to Stop/Cancel only when the server operation is actually cancellable.
- Attachments show upload/scan/index status before being eligible as sources.
- The model control is compact. It displays the actual selected model after execution; “Auto” describes routing policy, not a model identity.
- The composer remains usable for follow-up/refinement while an artifact is open, unless a blocking confirmation transaction is in progress.

#### Responsive behavior

- At widths below 800 px, assistant is full-screen and resizing is disabled.
- Conversation, Artifact and Activity become explicit views with preserved scroll positions.
- The composer uses `env(safe-area-inset-bottom)` and viewport/keyboard-safe height.
- At 200% browser zoom, all controls remain reachable without horizontal document scrolling.

### 22.5 Open-assistant implementation audit and gap register

| GAP-ID | Priority | Finding | Closure requirement |
|---|---|---|---|
| GAP-041 | P0 UX | Workspace width is fixed to 380 px and jumps to a fixed 1080 px only for Task artifacts. | CAND-021A bounded user resize and responsive fallback. |
| GAP-042 | P0 UX | Empty-state composer is centered; active-state composer is separately rendered at the bottom. | One persistent bottom composer for zero-to-many turns. |
| GAP-043 | P1 UX | Spacious workspace is coupled to `activeView=create`, so broad analysis remains narrow. | Workspace size independent from Task artifact existence. |
| GAP-044 | P1 UX | Conversation/artifact ratio is fixed; there is no collapse/split interaction. | Accessible splitter, collapse and reset. |
| GAP-045 | P1 UX | Layout preference is not persisted or validated. | Versioned presentation-only storage with clamps. |
| GAP-046 | P0 Accessibility | Resize keyboard behavior, focus trap/restore, zoom, mobile keyboard and reduced motion lack evidence. | TEST-AW-01..12 coverage. |
| GAP-047 | P1 Trust | Process is split between a generic Activity tab and Task-only activity timeline. | Turn-linked safe progress model; no chain-of-thought. |
| GAP-048 | P0 Platform | Conversation history is browser-held; reopen/reload cannot restore a canonical session. | CAND-021B server-owned session and ordered turns. |
| GAP-049 | P0 Platform | `previousTurnJobId` is not a durable conversation chain or optimistic-concurrency contract. | Session version, sequence, idempotency and nondisclosing ownership validation. |
| GAP-050 | P0 Product | Server routing is centered on Task-create keyword classification. | General planner disposition and registered capability resolution. |
| GAP-051 | P0 Platform | There is no single typed capability registry describing read/artifact/mutation tools. | CAND-021C registry with schemas, scopes, risk, renderer and tests. |
| GAP-052 | P0 Safety | Legacy tools exist but are not equivalent to canonical AI-native adapters. | Keep unavailable to general execution until individually adapterized; XML/regex parsing is never authority. |
| GAP-053 | P0 Retrieval | Project/task/workload, Wiki, meeting, group/chat, attachments and GitHub are assembled through fragmented paths. | Authorized Context Source Registry with common references and freshness. |
| GAP-054 | P0 Runtime | Semantic retrieval is disabled in current configuration. | Explicit feature/config readiness, ingestion health and honest lexical-only degradation. |
| GAP-055 | P1 Retrieval | GitHub integration exposes connection/development metadata but is not an assistant source; raw repository content is absent. | Metadata adapter first; code reader is a separate read-only security slice. |
| GAP-056 | P0 Security | Retrieved content has no unified trust class, injection handling, token budget or source-priority policy. | Sanitize, delimit, classify, rank, cap and record retrieved chunks. |
| GAP-057 | P0 Trust | Broad answers do not yet guarantee claim-level verified source references. | Structured finding → source refs; unsupported facts become assumptions/unknowns. |
| GAP-058 | P0 Product | No general research-plan/action-graph artifact exists. | CAND-021D `assistant_research_plan.v1`. |
| GAP-059 | P0 Model | Task composition targets DeepSeek strong while general agent chat may use the configured local `IChatClient`; actual routing is fragmented. | Server-owned model profiles and actual provider/model truth per turn/job. |
| GAP-060 | P1 UX/Runtime | Chat and job activity primarily wait/poll; no unified reconnectable event stream. | Ordered event contract with polling baseline and optional SSE/SignalR transport. |
| GAP-061 | P1 Quality | General planner prompts and behavioral evaluations are not versioned as a complete product contract. | Prompt IDs/versions, golden cases, adversarial and regression evaluation. |
| GAP-062 | P0 Audit | Session → turn → retrieval → provider attempt → job → draft → receipt is not one correlation chain. | Correlation IDs and read-back audit across every stage. |
| GAP-063 | P0 Privacy | Per-source privacy/retention/cloud-processing rules are not normalized for an open assistant. | Source policy evaluated server-side before retrieval/provider routing. |
| GAP-064 | P0 Authorization | Cross-project analysis/assignment lacks portfolio authorization and deterministic capacity/availability. | Preserve CAND-017 veto until foundation exists. |
| GAP-065 | P1 Reliability | Turn-level cancel, retry, duplicate submit, reconnect and stale-context behavior are incomplete. | Idempotent turn creation, resumable status and source-version checks. |
| GAP-066 | P1 Source | Attachment upload does not itself prove parse/index/authorization readiness. | Explicit upload → scan → parse → index → eligible lifecycle. |
| GAP-067 | P0 Security | A future repository-code reader needs path/size/type allowlists, commit-SHA grounding, secret redaction and indirect-prompt-injection defense. | Separate read-only connector gate; no repository write tool. |
| GAP-068 | P1 UX | Users cannot inspect what the assistant can read/do versus what is unsupported. | Capability/source disclosure and safe manual route. |
| GAP-069 | P1 Operations | No end-to-end targets for first progress, first answer, retrieval quality, completion and user cancellation. | Telemetry and service-level indicators per model profile/capability. |
| GAP-070 | P0 Trust | Offline fallback code contains plausible fixed metrics that can look like fresh analysis. | Remove or label demo fixtures; degraded production output must not present invented operational facts. |
| GAP-071 | P1 Extensibility | Artifact rendering is Task-specific. | Renderer registry keyed by schema ID with unknown-schema safe fallback. |
| GAP-072 | P1 Rollout | Common foundation flags and compatibility order are not explicit. | Independent UI/session/context/planner flags and reversible merge order. |

All 32 newly identified gaps have one owner candidate or explicit prerequisite/defer disposition. None is treated as closed merely because a similarly named service or button exists.

### 22.6 Target foundation architecture

```text
Agent Workspace UI
  ├─ Session/Turn client
  ├─ Process event renderer
  ├─ Source/grounding drawer
  └─ Artifact renderer registry
          │
Assistant Turn API + Session Store
          │
Intent/Research Planner (DeepSeek strong profile for complex work)
  ├─ Context Source Registry
  │    ├─ Project / Task / Workload
  │    ├─ Wiki / Meeting / Group / Attachment
  │    └─ GitHub metadata; repository code only after separate gate
  └─ Capability Registry
       ├─ read.*       → authorized, auditable, no mutation
       ├─ artifact.*   → structured proposal/review
       └─ mutation.*   → typed draft → confirm → domain service
          │
Canonical AI Job / Provider Router / Validator
  ├─ privacy + budget + cache + retries
  ├─ source guard + schema validation
  └─ audit + usage + read-back
```

The model never chooses an arbitrary backend method. It proposes a capability ID and arguments; the server resolves that ID against the caller-specific registry and revalidates every argument.

### 22.7 Capability Registry contract

Every registered capability descriptor must contain:

```json
{
  "capabilityId": "task.create.v1",
  "kind": "mutation_draft",
  "inputSchemaId": "task_create_request.v1",
  "outputSchemaId": "ai_action_intent_envelope.v1",
  "requiredScopes": ["project.read", "task.create"],
  "contextSources": ["project.summary", "project.members", "project.skills"],
  "riskClass": "project_mutation",
  "confirmationPolicy": "explicit_selective_confirm",
  "modelProfile": "reasoning_strong",
  "rendererId": "task-plan-review.v1",
  "featureFlag": "AiAssistantTaskAdapterEnabled"
}
```

Registry rules:

- The server filters descriptors before the model sees them.
- Read capabilities may return structured evidence automatically after authorization.
- Artifact capabilities create proposals but no domain mutation.
- Mutation capabilities can create editable drafts only; domain services execute after confirmation.
- Unknown capability/schema/renderer IDs fail closed and display an honest unsupported state.
- Provider-native function calling or schema JSON may be used, but both normalize to this registry. XML/regex text extraction is not execution authority.

### 22.8 Context Source Registry contract

Each source adapter returns a common envelope:

```json
{
  "sourceRef": "qaly://project/{projectId}/tasks/{taskId}@{rowVersion}",
  "sourceType": "task",
  "title": "Authorized display title",
  "freshnessAt": "2026-08-02T00:00:00Z",
  "trustClass": "qaly_domain_record",
  "privacyClass": "project_private",
  "contentHash": "sha256:...",
  "facts": {},
  "redactions": [],
  "retrievalMethod": "deterministic"
}
```

Required source behavior:

- Authorize tenant, organization, project, entity and private visibility before content is materialized.
- Prefer deterministic domain queries for metrics; use hybrid retrieval only for unstructured text.
- Preserve row version/content hash so stale proposals and confirmations can be rejected.
- Reserve output tokens before selecting context; rank and cap chunks rather than stuffing all content.
- Treat user files, Wiki, chat and repository text as untrusted data, not system instructions.
- Record which sources were read, skipped, redacted, stale or denied using nondisclosing labels.

### 22.9 Durable session and turn contract

`CAND-021B` must introduce a server-owned session without storing raw chain-of-thought:

- `AssistantSession`: tenant/user owner, optional organization/project scope, title, status, version, created/updated/archived times.
- `AssistantTurn`: monotonic sequence, user request, normalized disposition, safe assistant response, artifact refs, source refs, model profile/actual provider, correlation ID and status.
- `AssistantProcessEvent`: safe stage/status/tool/source summary, sequence, timestamps and retry/cancel metadata.
- `AssistantArtifactRef`: schema ID/version, job/draft/receipt IDs and renderer ID.

The client submits `sessionId`, `expectedVersion`, `clientTurnId` and an idempotency key. Duplicate requests replay the same turn. Foreign session/project IDs return a nondisclosing deny. Reload reads ordered turns and reconnects to running work.

### 22.10 Research-plan artifact

`CAND-021D` output schema `assistant_research_plan.v1`:

- `objective` and resolved scope;
- `findings[]`: statement, severity, confidence, source refs;
- `unknowns[]` and blocking/non-blocking clarifications;
- `assumptions[]`, visibly separated from facts;
- `options[]`: outcome, trade-offs, cost/time/risk;
- `recommendedOptionId` with rationale;
- `proposedActions[]`: abstract capability ID, dependency IDs, draft input, source refs and execution eligibility;
- `warnings[]`, `privacyNotes[]`, freshness and actual provider/model.

Actions whose adapter is not registered remain proposals with “Chưa thể áp dụng tự động”; they must not render a confirm button.

### 22.11 Model, retrieval and degraded policy

- `reasoning_strong`: DeepSeek V4 Pro alias/profile for multi-source research, planning and complex draft composition.
- `fast_local`: small local model for cheap classification/summarization only when privacy and quality policy allow.
- `auto`: a server routing policy, never a promise that a specific model ran.
- Every answer/artifact records the actual provider/model or “not reached”.
- Provider unavailable: preserve the session and user request; offer retry/model fallback/manual inspection. Do not generate fake findings.
- Semantic unavailable: deterministic and keyword sources may continue with a visible “semantic search unavailable” limitation.
- Budget hard-stop/policy deny: no provider request; explain the available manual or narrower route.

### 22.12 Security and mutation boundaries

- No autonomous destructive mutation, bulk confirm-by-default or client-supplied permissions.
- Tool results and retrieved text are data; they cannot register capabilities, alter scopes, suppress confirmation or reveal hidden prompts.
- Private source titles/content are excluded before the model request, not merely hidden after generation.
- Confirmation rechecks permission, tenant/project membership, source versions and domain invariants.
- Repository analysis is read-only and commit-SHA grounded; repository write, shell or deployment capabilities are outside this candidate.
- Logs/audit must avoid raw secrets, tokens, attachment contents and unnecessary prompt bodies.

### 22.13 Candidate score and selection

| Candidate | Outcome 25 | Gap closure 20 | AI-native fit 15 | Platform reuse 15 | Testability 10 | Quota fit 15 | Total | Gate |
|---|---:|---:|---:|---:|---:|---:|---:|---|
| CAND-021A Resizable Agent Workspace Shell v1 | 20 | 17 | 11 | 15 | 10 | 15 | **88** | **PRIMARY; no migration, works with current canonical Task path.** |
| CAND-021B Durable Session/Turn Chain | 23 | 20 | 15 | 14 | 9 | 8 | 89 | Next slice; migration and concurrency contract make it ineligible as Stretch. |
| CAND-021C Context + Capability Registry | 25 | 20 | 15 | 15 | 9 | 5 | 89 | Next foundation slice; must not be reduced to placeholder interfaces. |
| CAND-021D Grounded Research Plan | 25 | 20 | 15 | 14 | 9 | 6 | 89 | Depends on B/C and retrieval readiness. |

**Selected Primary:** CAND-021A. It closes the immediately visible UX problem and establishes the stable shell used by every later native capability without changing domain behavior.

**Stretch:** none. Session persistence requires a migration and concurrency/security work; combining it with UI resizing would weaken both verification sets.

Catalog after this amendment: **21 top-level candidates**. CAND-021 is a foundation candidate with independently gated A–D increments; its phases are not counted as four unrelated product capabilities.

### 22.14 CAND-021A implementation tasks — maximum three

1. **TASK-AW-1 — Resizable shell state and controls.** Add bounded left/top/top-left resizing, pointer capture cleanup, viewport clamp, versioned presentation-only persistence, reset action and mobile disable behavior to the singleton assistant shell.
2. **TASK-AW-2 — Bottom composer and adaptive workspace.** Render one composer at the bottom in both empty and active states; keep welcome/suggestions in the scroll region; decouple spacious workspace from Task artifact; add collapsible artifact rail and accessible splitter only when an artifact exists.
3. **TASK-AW-3 — Verification and compatibility.** Add focused component/E2E coverage for resize, persistence, reset, empty/active composer, artifact transition, mobile/zoom/keyboard/focus; run typecheck and existing CAND-019/CAND-018 regression suites. Do not introduce a backend migration in this task.

### 22.15 CAND-021A Definition of Done

1. User can resize width and height on desktop and reset them.
2. Saved geometry is presentation-only, versioned, bounded and survives reopen/reload.
3. Composer is at the bottom before the first message and remains the same functional surface afterward.
4. Chat-only analysis can use the expanded workspace; width is not gated by a Task artifact.
5. Opening an artifact preserves conversation and composer; splitter/collapse cannot hide both panes.
6. Mobile is full-screen single-column and does not expose unusable resize handles.
7. Header, close control, model control, transcript and composer never overlap the application sidebar/header or each other.
8. Keyboard resize/reset and focus restoration work; pointer listeners are removed on end/unmount.
9. Current `assistant_turn.v1`, `task.create.v1`, activity, draft review, selective confirmation and receipt behavior remain unchanged.
10. No generated bundle is hand-edited; any bundle update comes only from the standard frontend build after source verification.
11. Feature disable/rollback is possible by reverting the shell source change without data migration.
12. No claim is made that session, context registry, source-code analysis or new mutation adapters are complete.

### 22.16 Test and evidence matrix

| TEST-ID | Required scenario / evidence |
|---|---|
| TEST-AW-01 | Default desktop opens within viewport; header and bottom composer visible. |
| TEST-AW-02 | Drag left/top/top-left resizes, respects min/max and releases pointer/listeners. |
| TEST-AW-03 | Resize preference survives close/reopen and reload; corrupt/old storage falls back safely. |
| TEST-AW-04 | Reset returns to responsive defaults. |
| TEST-AW-05 | Empty state shows welcome content above one bottom composer; no centered duplicate input. |
| TEST-AW-06 | First send transitions to active thread without composer remount/data loss/focus jump. |
| TEST-AW-07 | Task artifact opens review rail; conversation remains available; split/collapse/reset work. |
| TEST-AW-08 | 1280/1024/800/768/390 widths and 200% zoom have no hidden close/submit/confirm control. |
| TEST-AW-09 | Mobile virtual-keyboard-safe layout and safe-area padding; resizing disabled. |
| TEST-AW-10 | Keyboard-only operation, focus containment/restoration, Escape behavior and reduced motion. |
| TEST-AW-11 | DeepSeek/provider unavailable and semantic-off labels remain honest; layout still usable. |
| TEST-AW-12 | Regression: chat grounded answer, clarification, unsupported, Task compose, activity, edit/selective confirm, reload/read-back. |

Commands for the implementation increment:

```powershell
Push-Location src/Qaly.Web/ClientApp
npm run typecheck
Pop-Location
npx playwright test tests/e2e/ai-assistant-workspace.spec.ts tests/e2e/ai-action-composer.spec.ts --project=chromium
dotnet test tests/Qaly.UnitTests/Qaly.UnitTests.csproj --no-restore --filter "FullyQualifiedName~AiAssistant|FullyQualifiedName~AiActionComposer|FullyQualifiedName~Erumi"
dotnet test tests/Qaly.IntegrationTests/Qaly.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~AiAssistant|FullyQualifiedName~AiActionComposer"
dotnet build Qaly_project.slnx --no-restore
git diff --check
```

The frontend production build is run only after source/type/E2E verification and only if the generated `wwwroot/dist` update is intentionally included; never use a build as an audit step.

### 22.17 File overlap and merge order

Likely CAND-021A files:

- `src/Qaly.Web/ClientApp/components/chat/FloatingChatbot.vue` — shell geometry, resize controls, persistence, artifact splitter.
- `src/Qaly.Web/ClientApp/components/chat/ErumiChatPanel.vue` — one persistent bottom composer and empty-state layout.
- `tests/e2e/ai-assistant-workspace.spec.ts` — primary browser contract.
- Existing `tests/e2e/ai-action-composer.spec.ts` — regression only unless a selector must be stabilized.

Merge order:

1. Commit this plan amendment alone as the remote checkpoint.
2. Implement TASK-AW-1 and verify pointer/viewport/storage behavior.
3. Implement TASK-AW-2 and verify empty/active/artifact layouts.
4. Implement TASK-AW-3 tests; build generated assets only after source gates pass.
5. Do not mix CAND-021B migration/session files into the UI commit.

### 22.18 Deferred foundation backlog

| Priority | Increment | Dependency | Exit outcome |
|---|---|---|---|
| P0 next | CAND-021B Durable Session/Turn Chain | CAND-021A stable shell | Reloadable server-owned conversation with ordered, idempotent turns and activity. |
| P0 next | CAND-021C Context + Capability Registry | B plus privacy/authorization review | One safe discovery/execution contract; legacy tools cannot bypass adapters. |
| P0 next | CAND-021D Grounded Research Plan | B/C + source readiness | Short prompt → authorized research → sourced options/action graph, no mutation. |
| P0 | Remove misleading offline fixed metrics | Can be paired only with a read-answer trust slice | No operational-looking fake facts in degraded mode. |
| P1 | GitHub metadata context adapter | C + repository permission mapping | Commits/PR/release evidence in plans. |
| P1 gated | Read-only repository code analysis | Separate security design | Commit-SHA-grounded code findings with secrets/injection defenses. |
| P0 gated | Staffing/scheduling adapters | CAND-015/016/006 + capacity/portfolio permission | Editable, confirmed assignment/schedule proposals. |
| P1 | CAND-020 Poll draft | C registry + Group permission/schema | One native Group Poll draft; no fake Form/Quiz domain. |

### 22.19 Coverage closure

| Gate | Result |
|---|---|
| Previous global surface inventory | 117/117 dispositioned — retained PASS |
| New/re-audited assistant UI surfaces | 12/12 dispositioned — PASS |
| Runtime AI capability inventory | 37/37 retained with caller/orphan disposition — PASS |
| New foundation contracts A–D | 4/4 selected or dependency-deferred — PASS |
| Previous gaps | 40/40 retained — PASS |
| New gaps GAP-041..GAP-072 | 32/32 candidate/prerequisite/defer disposition — PASS |
| UI orphan without disposition | 0 — PASS |
| Backend/tool/source orphan without disposition | 0; legacy tools explicitly GAP-052 — PASS |
| Primary acceptance item without verification | 0 — PASS |

**Coverage gate: PASS. Product completeness: NOT PASS.** Coverage means every discovered assistant surface and foundation gap has a disposition. It does not mean durable sessions, broad context research, repository-code analysis or additional mutations have been implemented.

### 22.20 NEXT_IMPLEMENTATION_GOAL

> Implement only `CAND-021A — Resizable Agent Workspace Shell v1` on the current checkpointed `main`. Preserve the existing `assistant_turn.v1` and canonical `task.create.v1` job/draft/activity/edit/selective-confirm/read-back behavior. Make the singleton `Trợ lý AI` desktop workspace user-resizable from its left/top/top-left boundaries with bounded, versioned presentation-only persistence and a reset action; decouple spacious chat from Task artifact existence; render one chat composer docked at the bottom for both empty and active conversation states; keep welcome/suggestions in the independently scrolling transcript area; when a Task artifact exists, keep chat available and provide an accessible collapsible/resizable artifact rail. Use full-screen single-view behavior below 800 px, maintain focus/zoom/reduced-motion/mobile-keyboard accessibility, and expose safe process status without chain-of-thought. Complete TASK-AW-1..3 and TEST-AW-01..12. Do not add migrations, session persistence, context/capability registry, repository scanning, new mutation adapters or autonomous execution in this increment. Run the exact commands in §22.16 and report generated-bundle handling explicitly.

## 23. Implementation checkpoint — CAND-021A + CAND-021B

**Checkpoint date:** 2026-08-02 (Asia/Saigon)
**Baseline:** `main` at `60f2cd4e9ae9f21a25855b6fd4cdb781b0beaf46`, equal to `origin/main` before this local implementation increment.
**Workspace note:** this checkpoint is implemented and verified in the existing dirty working tree; it is not a commit/push statement and does not reclassify earlier AI-native changes as newly authored here.

### 23.1 Completed user outcome

- `CAND-021A` is now implemented in source: the singleton Trợ lý AI workspace can resize from the left/top/corner on desktop, persist bounded presentation geometry, reset layout, keep one composer at the bottom, and retain chat while the Task artifact rail opens/collapses/resizes. Mobile remains full-screen without unusable resize controls.
- `CAND-021B` is now implemented end-to-end: the server owns an authorized assistant session; each turn has a monotonic sequence, session version, client turn ID, idempotency key, correlation ID, safe process events, model routing truth, source/artifact references and reloadable response.
- Reload/reopen reads the latest session and ordered turns from the server. The browser no longer supplies conversational history as execution authority; the server rebuilds the bounded history from completed owned turns.
- Duplicate `clientTurnId`/idempotency key with the same request replays the same completed turn. A changed payload or stale `expectedVersion` fails closed with `409` and creates no extra turn.
- A foreign user receives nondisclosing `404` for another user's session. Optional project scope is checked before use; the existing capability-specific Project authorization remains authoritative.
- Process UI displays only safe stages such as accepted/routing/answer/artifact, status and elapsed time. Raw chain-of-thought is not stored or rendered.
- The existing `task.create.v1` path remains draft-first and confirmation-gated. Session persistence does not create a Task, Project, Group, Meeting, Poll or Schedule mutation adapter.

### 23.2 Runtime contract and persistence

New/extended API:

- `POST /api/ai/assistant/sessions` — CSRF-protected creation of an owned active session.
- `GET /api/ai/assistant/sessions/recent` — latest active owned session or `null`.
- `GET /api/ai/assistant/sessions/{sessionId}` — ordered turn/process/artifact read-back with nondisclosing ownership guard.
- `POST /api/ai/assistant/turns` — now requires `sessionId`, `expectedVersion`, `clientTurnId` and `Idempotency-Key`; successful responses return turn/session/version/correlation/process/model metadata.

Migration `20260802063926_P007AssistantSessionTurnChain` adds only:

- `AssistantSessions`;
- `AssistantTurns`;
- `AssistantProcessEvents`;
- `AssistantArtifactRefs`.

Unique constraints close sequence and duplicate-submit races per session. `AssistantSession.Version` is an optimistic concurrency token. P007 is additive; its forward migration contains no drop table/column behavior. The feature can be disabled through `AiJobsV4:AssistantSessionEnabled` / `AI_ASSISTANT_SESSION_ENABLED`.

### 23.3 Security, privacy and failure behavior

- Session queries always include the authenticated owner ID; caller-supplied identity, tenant and permissions are never accepted.
- Stored process records contain public labels and safe error codes, not hidden reasoning, prompts from other sessions, secrets or raw attachment bodies.
- Request hashing includes normalized intent/context and attachment metadata without persisting attachment preview contents in the session record.
- Provider/model is recorded as the actual model when reached, otherwise explicitly `not_reached`; `Auto` remains a routing profile.
- Accepted requests are persisted before provider/intent execution. A failed provider turn remains reloadable with safe failed state and retryability metadata instead of fabricated success.
- Session and turn accepted/completed/failed lifecycle writes `AiAuditEvent` correlation entries without raw prompt bodies.

### 23.4 Verification evidence

| Gate | Result |
|---|---|
| Frontend `npm run typecheck` | PASS |
| Frontend production build from source | PASS; generated `wwwroot/dist` refreshed by Vite, not hand-edited |
| `dotnet build Qaly_project.slnx --no-restore` | PASS, 0 errors |
| Erumi / gateway / Action Composer unit regression | **44/44 PASS** |
| Assistant session/turn + Action Composer integration and migration tests | **12/12 PASS**; targeted session suite **8/8 PASS** |
| CAND-021A workspace + CAND-021B reload + CAND-018/019 composer Chromium E2E | **5/5 PASS** |
| Preview health at `http://127.0.0.1:5010/Account/Login` | HTTP 200 |

Covered scenarios include authorized create/read-back, structured clarification, unsupported no-mutation response, idempotent replay, stale-version conflict, foreign-session deny, additive migration constraints, desktop resize/persistence/reset, mobile layout, reload restoration, safe process rendering and Task draft/confirm receipt regression.

### 23.5 Updated disposition and remaining gaps

- `CAND-019` current registered/read/deny v1 plus durable session closure: `NATIVE_COMPLETE` for its bounded contract.
- `CAND-021A`: `NATIVE_COMPLETE`.
- `CAND-021B`: `NATIVE_COMPLETE` using polling/read-back baseline; reconnectable SSE/SignalR remains GAP-060 and is not falsely claimed.
- `CAND-021` top-level foundation remains `PRESENT_PARTIAL` because CAND-021C Context + Capability Registry and CAND-021D Grounded Research Plan are not implemented.
- Catalog remains **21 top-level candidates**. Six top-level candidates are now closed for their bounded contract, CAND-021 remains partial, and 14 candidates remain deferred/not started. Therefore **15 top-level candidates still contain implementation work**.

### 23.6 Superseding NEXT_IMPLEMENTATION_GOAL

> Implement only `CAND-021C — Authorized Context Source Registry + Capability Registry` on top of the verified CAND-021A/B foundation. Introduce typed, server-owned descriptors for the existing grounded read and `task.create.v1` capabilities; include input/output schema IDs, required scopes, context sources, risk class, confirmation policy, model profile, renderer ID and feature flag. Add authorized source envelopes for the currently supported Project/Task/Workload deterministic data, with source ref, freshness, trust/privacy class, content hash, redactions and retrieval method. Filter sources and capabilities before model routing, fail closed for unknown IDs, and expose read/skipped/denied nondisclosing disclosure in the existing assistant session/turn read-back. Do not add repository-code reading, new mutation adapters, Poll/Form, staffing/scheduling or autonomous execution. Preserve session version/idempotency/audit, Task draft-confirm behavior and all CAND-021A/B tests; add unit, integration and Chromium evidence for authorization, cross-tenant deny, private-source exclusion, unknown-capability deny, source freshness and reload.

## 24. Implementation checkpoint — CAND-021C Authorized Context + Capability Registry

**Checkpoint date:** 2026-08-02 (Asia/Saigon)
**Baseline:** `main` at `60f2cd4e9ae9f21a25855b6fd4cdb781b0beaf46`, equal to `origin/main` before this local increment.
**Scope guard:** this increment implements only the registry foundation. It adds no repository scanner, broad research planner, new mutation adapter, autonomous execution or migration.

### 24.1 Completed user outcome

- Every durable assistant turn now passes through a server-owned authorization/context gate before intent execution or provider routing.
- The registry exposes typed descriptors for only the two existing bounded capabilities: `grounded.read.v1` and `task.create.v1`. Each descriptor includes input/output schema IDs, scopes, source IDs, risk class, confirmation policy, model profile, renderer ID and feature flag.
- `task.create.v1` is available only when the current user can manage the selected Project and both Action Composer flags are enabled. A read-only member receives a structured policy block; the provider is not reached and no artifact/job/task is created.
- Deterministic adapters materialize capped Project, Task, selected Task detail, Workload, Project member, organization skill and authorized workspace Project envelopes. Every envelope has `sourceRef`, freshness, trust/privacy class, SHA-256 content hash, facts, redaction labels and retrieval method.
- Private tasks are excluded before prompt construction unless the authenticated user is entitled to see them. Selected restricted tasks produce a nondisclosing `denied` disclosure without title, facts or source reference.
- Provider-backed assistant reads receive only the filtered envelope JSON as untrusted data and receive no tool list. Unknown capability/source IDs and capability-intent mismatch fail closed before a turn is persisted.
- Completed turn responses persist the filtered capability descriptors and `read`/`skipped`/`denied` disclosures. Reload renders them in an expandable “Ngữ cảnh đã kiểm tra” panel.

### 24.2 Decision-complete runtime contract

Request additions on `POST /api/ai/assistant/turns`:

- optional `requestedCapabilityId`; if supplied it must be registered and match the deterministic intent classifier;
- optional `requestedSourceIds`; unknown IDs are rejected and known but inapplicable IDs are disclosed as `skipped`.

Response/read-back additions on `assistant_turn.v1`:

- `capabilities[]`: authorized descriptors only;
- `sourceDisclosures[]`: safe source ID, `read|skipped|denied`, label, optional authorized source ref and safe reason code;
- `sourceRefs[]`: canonical `qaly://...@version/hash` refs from materialized envelopes, replacing generic service-name grounding for this assistant path.

Execution order is now:

1. validate owned session/version/idempotency/context scope;
2. infer or validate the requested capability;
3. authorize Project/tenant/user and filter registered capabilities;
4. materialize/cap/redact deterministic sources;
5. persist the accepted turn plus safe context event/audit record;
6. route the capability or provider using only authorized envelopes;
7. persist structured response, actual provider/model, process events and disclosures for read-back.

Feature disable path: `AiJobsV4:AssistantContextRegistryEnabled` / `AI_ASSISTANT_CONTEXT_REGISTRY_ENABLED`. Defaults remain off in base/production example and on only in Development/integration evidence configuration.

### 24.3 Security and compatibility closure

- No client-supplied permission, tenant, descriptor, schema, renderer, facts or source reference is trusted.
- Unknown capability/source IDs return safe `400`; foreign Project context returns nondisclosing `404`; neither path creates an AssistantTurn.
- Project Task aggregation uses the same private-task visibility boundary as the task domain path; hidden titles/content are absent before serialization and model routing.
- Context is size-capped at 25 workspace projects, 50 tasks, 100 members and 100 skills with visible `context_limit_applied` disclosure.
- Context payload is explicitly marked untrusted data in the system prompt. The authorized assistant provider request has `Tools = null`; retrieved data cannot register tools, alter scopes or bypass confirmation.
- `task.create.v1` remains `mutation_draft` with `explicit_selective_confirm`; this registry does not mutate Tasks or weaken CAND-019 draft/edit/selective-confirm/read-back behavior.
- CAND-021A resizing/composer and CAND-021B session/version/idempotency contracts remain compatible. No database migration is required because disclosure is stored inside the versioned turn response JSON.

### 24.4 Verification evidence

| Gate | Result |
|---|---|
| Registry + Erumi unit slice | **22/22 PASS**; includes five focused registry authorization/privacy tests |
| Assistant API integration slice | **11/11 PASS**; includes unknown capability, read-only member policy block, foreign Project deny and reload disclosure |
| Full selected AI unit regression | **49/49 PASS** |
| Assistant + Action Composer integration regression | **15/15 PASS** |
| Frontend TypeScript/Vue typecheck | PASS |
| Frontend production build | PASS; generated `wwwroot/dist` refreshed only by Vite |
| Workspace/Action Composer Chromium E2E | **5/5 PASS**; includes reload and disclosure UI |
| Full .NET build, schema JSON and diff hygiene | PASS after final gate |
| Preview `http://127.0.0.1:5010/Account/Login` | HTTP 200 |

### 24.5 Updated disposition

- `CAND-021A`: `NATIVE_COMPLETE`.
- `CAND-021B`: `NATIVE_COMPLETE` for polling/read-back baseline.
- `CAND-021C`: `NATIVE_COMPLETE` for the registered Project/Task/Workload context and the existing read/Task-draft capabilities.
- `CAND-021` top-level remains `PRESENT_PARTIAL`: CAND-021D Grounded Research Plan and broader capability adapters are not implemented.
- Catalog remains 21 top-level candidates; 15 top-level candidates still contain implementation work because the CAND-021 umbrella is not complete.

### 24.6 Superseding NEXT_IMPLEMENTATION_GOAL

> Implement only `CAND-021D — Grounded Research Plan v1` on top of the verified CAND-021A/B/C foundation. Accept a short natural-language objective, resolve only authorized registry capabilities/sources, and produce validated `assistant_research_plan.v1` with facts linked to source refs, unknowns, assumptions separated from facts, options/trade-offs, recommendation, and proposed abstract actions. Unregistered actions must remain non-executable proposals; only the existing `task.create.v1` adapter may open its canonical editable draft/explicit selective-confirm flow. Add clarification bounds, provider/schema-invalid/timeout/budget/policy degraded states, reload/read-back and unit/integration/Chromium evidence. Do not add repository scanning, Project/Group/Meeting/Poll/Form/staffing/scheduling mutation adapters or autonomous execution.

## 25. Implementation checkpoint — CAND-021D Grounded Research Plan v1

**Checkpoint date:** 2026-08-02 (Asia/Saigon)
**Baseline:** `main` at `60f2cd4e9ae9f21a25855b6fd4cdb781b0beaf46`, equal to `origin/main` before this local increment.
**Scope guard:** no migration, repository scanner, new domain mutation adapter or autonomous execution was added. Existing dirty-worktree changes remain outside this checkpoint unless listed by the CAND-021D diff.

### 25.1 Completed user outcome

- A short request such as “Phân tích rủi ro và đề xuất phương án xử lý” now resolves to registered capability `research.plan.v1`, not generic chat or Task-create keyword routing.
- The server materializes only CAND-021C authorized source envelopes, calls the canonical AI gateway with `Tools = null`, and returns structured `assistant_research_plan.v1` rather than unbounded prose.
- The native card separates grounded facts, blocking/non-blocking unknowns, assumptions, 1–3 options with trade-offs/effort/risk, one recommendation, an action dependency graph, warnings, privacy notes, freshness and actual provider/model.
- Every factual finding must cite only an allowed versioned `qaly://...` source ref. A foreign/hallucinated ref, invalid severity/confidence, duplicate ID, missing recommendation, oversized draft input or cyclic action graph fails validation and enters the gateway repair/failure path; no fake plan is rendered.
- Execution is closed-world: only an authorized, registered `task.create.v1` proposed action receives “Mở bản nháp task”. It hands off to the existing editable Task Action Composer and explicit/selective confirmation path. Unknown or unauthorized Project/Group/Meeting/Poll/Schedule actions remain proposals labelled “Chưa thể áp dụng tự động” without a confirm button.
- Research Plan survives session reload inside the canonical turn response. A typed artifact ref records schema `assistant_research_plan.v1` and renderer `research-plan-review.v1`; the safe process timeline records artifact completion without chain-of-thought.
- Provider/model truth, privacy/freshness notes and objective/scope are reconciled server-side. The model cannot promote an action, replace the user's objective, rewrite authorization scope or claim a provider/model.

### 25.2 Decision-complete contract

| Field | Value |
|---|---|
| capability | `research.plan.v1` |
| kind | `artifact` / read-only proposal |
| request schema | `assistant_research_request.v1` |
| output schema | `assistant_research_plan.v1` |
| renderer | `research-plan-review.v1` |
| model profile | `reasoning_strong`; routing may prefer eligible DeepSeek strong profile; response always reports actual provider/model |
| confirmation | `explicit_adapter_handoff`; Research Plan itself cannot mutate |
| feature disable | `AiJobsV4:AssistantResearchPlanEnabled` / `AI_ASSISTANT_RESEARCH_PLAN_ENABLED` |

Canonical synchronous flow is retained for this bounded single-turn artifact because durable AssistantTurn already persists accepted/failed/completed state, request cancellation propagates to the provider, gateway timeout/repair/fallback policy is canonical, and reload reads the stored result. A future multi-minute or repository-scale run must use a background job and is outside this contract.

Execution order:

1. classify direct Task-create vs analysis/option-plan intent;
2. authorize user/project and remove private sources before provider routing;
3. build validation context with objective, server scope, freshness/privacy, allowed source refs and authorized capability IDs;
4. call canonical gateway for provider routing, privacy/budget policy, usage ledger, cache and bounded repair;
5. validate schema, IDs/counts, confidence/severity, source membership, action DAG and draft size;
6. overwrite server-owned objective/scope/freshness/privacy/provider/model and recalculate `executionEligible`;
7. persist response, artifact ref, process events and audit for read-back;
8. render proposals and expose only the registered Task draft handoff.

### 25.3 Security, failure and compatibility closure

- Cross-project/foreign context keeps the CAND-021C nondisclosing `404`; restricted Task facts are removed before prompt creation.
- Permission/sensitive policy, consent/stale source, budget/rate limit, payload size, schema-invalid and provider-unavailable errors map to honest `403/409/429/413/422/503` behavior. Accepted failed turns remain durable; no offline findings are synthesized.
- Prompt `assistant-research-plan@1.0.0` treats retrieved content as untrusted data, forbids tool calls/mutation and requires exact JSON.
- Strong model output cannot register capabilities. Client-supplied `executionEligible`, scope, provider/model and source claims are ignored or revalidated.
- No Task is created by Research Plan. Clicking the eligible handoff still opens CAND-018/019 draft; mutation requires existing permission, edit/selective confirm, idempotency, concurrency, audit and receipt.
- `AssistantResearchPlanEnabled=false` removes this capability while leaving grounded read, manual modules, Task Action Composer and historical response JSON readable.

### 25.4 Verification evidence

| Gate | Result |
|---|---|
| Research contract + Erumi/context unit slice | **29/29 PASS**; strict schema dispatch, hallucinated source deny, cyclic graph deny, server action reconciliation, provider-unavailable and schema-invalid behavior |
| Assistant API integration slice | **11/11 PASS**; authorized plan, no mutation, action eligibility, artifact ref, audit, reload/read-back and existing foreign/private/idempotency/stale regression |
| Frontend Vue/TypeScript typecheck | PASS |
| Frontend production build | PASS; generated `wwwroot/dist` refreshed by Vite only |
| Research Plan Chromium E2E | **1/1 PASS**; card facts/unknowns/model/source render, one eligible Task draft button and one unavailable action gate |
| JSON schema/config parse + full .NET build | PASS after final checkpoint gate |
| Preview `http://127.0.0.1:5010/dashboard` | HTTP 200 after restart on final binary |

### 25.5 Updated disposition and coverage closure

- `CAND-021A/B/C/D`: `NATIVE_COMPLETE` for their bounded contracts.
- Top-level `CAND-021 — Open Assistant Foundation`: `NATIVE_COMPLETE` for shell + durable turn + authorized registry + grounded research plan. Streaming transport, repository reading and each additional mutation adapter remain separately dispositioned backlog.
- GAP-050, GAP-051, GAP-057 and GAP-058 are closed for the current registered source/capability universe. GAP-053/054/055/056/059/060/063/066/067 remain explicit breadth/platform backlog.
- Catalog remains **21 top-level candidates**: seven are closed for their bounded contracts and **14 still contain implementation work**.
- Inventory denominators remain 117/117 surfaces and 37/37 previously audited capabilities dispositioned. New `research.plan.v1` has caller, renderer, schema, source mapping and verification; UI orphan = 0, backend capability orphan = 0, selected acceptance item without verification = 0.

**Coverage gate: PASS. Product completeness: NOT PASS.** The assistant can now research and propose safely over registered Qaly sources, but it cannot execute unregistered domain actions or infer cross-project staffing without the deferred evidence/capacity prerequisites.

### 25.6 Superseding NEXT_IMPLEMENTATION_GOAL

> Implement only the next safe staffing prerequisite under `CAND-016 — Evidence-backed Member Skill Profile`, starting with explicit completion-contributor attribution and tenant/private-source-safe deterministic evidence aggregation on top of completed CAND-015. Keep this bounded to one deep Project/Member workflow and at most three tasks: decision-complete attribution semantics and additive persistence; permission-aware evidence band/read model with recency/confidence/source visibility; native member evidence card with correction path and unit/integration/Chromium evidence. Do not implement opaque performance scoring, protected-attribute/message sentiment, cross-project auto-assignment, scheduling, CAND-006 ranking or CAND-017 mutation until this prerequisite is complete. If completion attribution policy cannot be made decision-complete without a product-owner choice, stop after updating this plan rather than inventing attribution.

## 26. Implementation checkpoint — CAND-016 Member Skill Evidence + CAND-006 Grounded Assignee Recommendation

**Checkpoint date:** 2026-08-02 (Asia/Saigon)
**Baseline:** `main` at `60f2cd4e9ae9f21a25855b6fd4cdb781b0beaf46`, equal to `origin/main` before this local increment.
**Workspace note:** implemented and verified in the existing dirty working tree; this section is not a commit/push statement and does not claim unrelated working-tree files as part of these two candidates.

### 26.1 Completed user outcome

- `CAND-016` is closed for the bounded evidence-profile contract. After a Task is `Done` and has confirmed CAND-015 skill requirements, a Project manager can explicitly select which assigned contributors actually completed the work. Assignment history, labels, chat sentiment and model inference never create evidence by themselves.
- The Task Detail native card shows honest loading/error/ineligible/read-only/confirmed/correction-pending states. A contributor may request correction for their own record; a Project manager may resolve the request by reconfirming or revoking it. A correction-pending/revoked record is immediately excluded from skill evidence.
- The Organization Member evidence drawer aggregates only manager-confirmed completion records into deterministic `emerging|practiced|experienced` bands, confidence, verified-task count, recency/staleness and source links. Missing evidence is explicitly not interpreted as low skill or poor performance.
- Private/restricted source Tasks still contribute to an authorized aggregate, but their title and deep link are redacted for a profile viewer who cannot access the Task. Cross-organization/member access returns nondisclosing `404`.
- `CAND-006` is closed for Project-local, evidence-aware decision support. At a Task, a Project manager receives candidates ranked from confirmed required-skill coverage plus workload visible in the current Project. The response shows coverage, confidence, missing skills, workload, scoring version and authorized Task source links.
- No LLM is used for hard skill/workload scoring. This is intentional: deterministic, versioned matching is more testable and fair than asking DeepSeek or another model to invent a score. A future strong model may explain already-grounded trade-offs, but cannot add evidence, alter bands or override the ranking inputs.
- When Task skills or visible confirmed evidence are missing, the UI says `task_skills_missing` or `insufficient_evidence`; it does not claim skill-fit. Private evidence outside the viewer's Task scope is excluded before scoring.
- “Mở form giao việc” only opens the existing editable Task form with a proposed assignee. The original Task title/description/status/options are preserved, the form is labelled as edit mode, and no mutation occurs until the manager presses “Lưu thay đổi”. Cancel clears the draft. There is no auto-assignment.
- Legacy `GET /api/ai/tasks/{taskId}/assignment` remains only as a compatibility wrapper and delegates to the same grounded result; it no longer calls a free-text LLM assignment prompt. The native caller uses `/assignment-insight`.

### 26.2 Decision-complete contracts

#### CAND-016 evidence ledger

| Contract | Decision |
|---|---|
| persistence | additive `TaskCompletionAttributions`; unique `(TaskItemId, ContributorUserId)`, SQL row-version, status `Confirmed|CorrectionRequested|Revoked`, confirmer, timestamps and policy version |
| eligible input | Task is `Done`; Task has confirmed organization skills; contributor is an explicit primary/multi-assignee; caller explicitly confirms current Task row-version |
| confirmation authority | system admin, Project owner/manager/scrum master or authorized organization manager through canonical `CanManageProjectAsync`; an assignee/reporter alone cannot self-confirm |
| correction authority | attributed contributor for their own record, or Project manager; current attribution row-version and non-empty reason required |
| profile authority | member self or organization manager only; tenant/member existence and Task source visibility are re-resolved server-side |
| calculation | `member-skill-evidence.v1`; deterministic evidence band/confidence, capped at 95%, 180-day stale label; no global performance score |
| source behavior | visible source returns `/projects/{projectId}/tasks/{taskId}`; restricted source returns neither title nor URL |
| rollback | rollback UI/API via deployment/feature release; P008 is additive and can remain dormant without deleting evidence |

API/read-back:

- `GET /api/tasks/{taskId}/completion-contributors`;
- `PUT /api/tasks/{taskId}/completion-contributors` — CSRF + explicit confirmation + Task row-version;
- `POST /api/tasks/{taskId}/completion-contributors/{attributionId}/correction` — CSRF + attribution row-version;
- `GET /api/organizations/{organizationId}/members/{memberId}/skill-evidence`.

Migration `20260802114055_P008MemberSkillEvidence` creates only `TaskCompletionAttributions` plus its foreign keys/indexes. The generated forward migration contains no drop table/column behavior.

#### CAND-006 recommendation

| Contract | Decision |
|---|---|
| API | `GET /api/ai/tasks/{taskId}/assignment-insight?projectId={projectId}` |
| output | typed `TaskAssignmentInsightDto` / `TaskAssignmentCandidateDto`, scoring `assignee-evidence-score.v1`, evidence state, required/missing skills, source links, authorized workload scope and Task row-version |
| inputs | current Task confirmed skill requirements; `Done` Tasks with `Confirmed` completion attribution; Project members/owner; open workload returned by `ApplyVisibilityFilter` |
| forbidden inputs | Task labels/keywords, generic assignee history without attribution, private chat/sentiment, protected attributes, foreign/private Tasks not visible to caller |
| ranking | candidates with at least one confirmed skill match precede workload-only candidates; within that set use deterministic skill coverage, conservative evidence band/confidence, recent verified evidence and visible workload |
| empty/degraded | no required skills or no visible evidence returns no recommendation; no provider timeout/schema-invalid state is applicable because this bounded scorer does not call a provider |
| mutation | none in the insight API; UI opens an editable existing Task form and requires standard domain permission plus explicit save |
| compatibility | old assignment endpoint delegates to the grounded service result; no parallel hallucination-prone runtime path remains |

### 26.3 Security, fairness and failure closure

- Confirmation uses Project-management permission, not the broader Task-management permission; a normal assignee cannot certify their own evidence.
- All Task/profile/recommendation lookups are caller-filtered. Hidden source titles/URLs and cross-tenant identities are not disclosed through counts, recommendation prose or source lists.
- Task and attribution row-versions fail stale writes with `409`. Contributor/task eligibility and the maximum contributor count are revalidated on every write. Every confirm/correction writes an audit record.
- Empty evidence produces no negative member label. The implementation has no protected-attribute, message sentiment, activity surveillance or opaque “performance score”.
- Provider/budget/model routing is not invoked for these deterministic calculations, so a missing DeepSeek key cannot fabricate or block evidence/read-only scoring. This also avoids unnecessary token cost.
- The recommendation never auto-mutates. Existing manual Task authorization remains the final boundary for assignment.

### 26.4 Verification evidence

| Gate | Result |
|---|---|
| CAND-016/006 focused API + deterministic + migration tests | **6/6 PASS** |
| AI/Task Skill/Assistant/Action Composer integration regression | **27/27 PASS** |
| full unit suite | **402/402 PASS** after fixing the calendar-boundary budget fixture discovered by this audit |
| Chromium native staffing flow | **1/1 PASS**; Task evidence card, grounded source, editable assignee form preserving description, cancel behavior and Member evidence drawer |
| Vue/TypeScript typecheck | PASS |
| Vite production build | PASS; generated `wwwroot/dist` refreshed only by Vite |
| full `.NET` solution build | PASS, 0 errors; existing analyzer/performance warnings remain outside the runtime closure and are recorded rather than hidden |
| preview | `http://127.0.0.1:5010/dashboard` running on the final binary |

Covered deny/failure scenarios: manager-only confirmation, assignee write deny, outsider/cross-tenant nondisclosing deny, private-source redaction, explicit confirmation, stale Task/attribution row-version, correction exclusion/read-back, empty evidence honesty, label/description non-evidence, private source excluded from recommendation, source deep-link grounding, no auto-assignment and existing assistant/Task Skill/Action Composer regression.

### 26.5 Updated disposition and coverage closure

- `CAND-016`: `NATIVE_COMPLETE` for explicit completion attribution + deterministic Member skill evidence profile.
- `CAND-006`: `NATIVE_COMPLETE` for bounded Project-local evidence/workload recommendation + human-controlled assignment draft.
- The CAND catalog remains **21 top-level candidates**: **9 are closed** for their bounded contracts and **12 still contain implementation work**.
- Previous inventory denominators remain 117/117 surfaces and 37/37 previously audited capabilities dispositioned. All new endpoints have native callers or an explicit compatibility disposition; new UI orphan = 0, new backend orphan = 0, selected acceptance item without verification = 0.

**Coverage gate: PASS. Product completeness: NOT PASS.** Cross-project capacity/calendar constraints and automatic schedule proposals remain CAND-017; these two candidates do not claim portfolio scheduling or autonomous assignment.

### 26.6 Superseding NEXT_IMPLEMENTATION_GOAL

> Implement only the next safe prerequisite of `CAND-017 — Portfolio Capacity + Schedule Proposal`, bounded to a read-only, deterministic member availability/capacity model before any rescheduling mutation. Reconcile authorized open assignments across Projects that the manager and member may both access; define working capacity, date window, estimated-hours fallback, deadline collisions, source visibility, freshness and unknown-capacity states; render a native member/project capacity card with source links and no protected-attribute/activity-surveillance inference. Add typed API/read-back, tenant/private-source guards, tests for partial portfolio visibility and conflicting deadlines, and Chromium evidence. Do not auto-assign, auto-change dates, introduce a scheduling LLM, or add `task.assign.v1`/`task.reschedule.v1` until the capacity model is complete and review/confirmation semantics pass a later selection gate.

## 27. Implementation checkpoint — CAND-017 Portfolio Schedule Copilot + CAND-008 source-linked Task Draft

**Checkpoint date:** 2026-08-02 (Asia/Saigon)
**Baseline:** `main` at `60f2cd4e9ae9f21a25855b6fd4cdb781b0beaf46`, equal to `origin/main` before this local increment.
**Workspace note:** implemented and verified in the existing dirty working tree; no commit or push is claimed by this checkpoint.

### 27.1 Completed user outcomes

#### CAND-017 — Cross-project Assignment & Schedule Copilot

- Project managers now have a real **Phân công & Capacity** tab in Project Detail. It reads organization-wide capacity and open assignments for the selected window, then shows workload by Project, missing estimates, deadline collisions and privacy-restricted aggregate load.
- A manager or member can maintain explicit weekly capacity, time zone and non-overlapping leave/reduced-capacity windows. The system never infers availability from activity, protected attributes, messages or sentiment.
- The manager selects current-Project open Tasks and requests an `assignment_schedule_proposal.v1`. Deterministic `portfolio-capacity-scheduler.v1` uses confirmed CAND-015/016 skill evidence, cross-project workload, availability, deadlines and dependencies. Missing estimates use a visible 8-hour fallback.
- The proposal shows before/after load, skill coverage, evidence confidence, risks, alternatives and authorized source refs. Private Tasks from another Project affect aggregate load only; identity, title and link are not exposed.
- No Task changes when a proposal is generated. The manager edits rows, selects desired changes and explicitly confirms. Confirmation rechecks permission, Task row version and source version, applies assignment/start/due changes, writes audit/usage/receipt records and is idempotent. Reject keeps Tasks unchanged.
- Reload restores pending or confirmed proposal and receipt from the canonical draft/job; this is not a client-only success state.

#### CAND-008 — AI-04 native selected-message Task Draft review

- In a Group, a user selects authorized messages and chooses **Tạo task draft**. Qaly creates canonical job `task_draft_native`, prefers the eligible strong `deepseek-v4-pro` profile and requires exact schema `task_draft.v5`.
- The model receives only server-authorized Group message snapshots and allowed Project members. Output is limited to 1–20 structured Task drafts, or honest `insufficient_evidence`. Every factual draft carries allowed message source refs and confidence.
- The **Dự thảo** tab renders native editable fields instead of raw JSON: title, description/acceptance, priority, due date, assignee, selection, confidence and source links. Provider/model shown are actual runtime values.
- Users can save, reject or selectively confirm. No Task is created before `create_tasks`. Duplicate confirmation replays the same receipt; stale/deleted/foreign/private sources, stale row versions and unauthorized assignees are rejected without partial mutation.
- Queued/running/retrying/succeeded/failed/canceled states are honest. Provider timeout/unavailable, invalid schema/repair exhaustion, policy/budget deny and empty evidence never render fabricated success. Reload now reopens the native Draft tab and reads back the current job/draft/receipt.
- This closes locked AI-04 for the Group selected-message path. Meeting transcript and Wiki-section adapters remain separately dispositioned under CAND-010 and CAND-012.

### 27.2 Decision-complete contracts

| Candidate | Contract |
|---|---|
| CAND-017 capacity read | `GET /api/projects/{projectId}/portfolio-capacity?from&to`; typed `PortfolioCapacityDto`; 1–90 day window; organization/project-management authorization; active-organization boundary; partial-private aggregate label. |
| CAND-017 capacity write | `PUT /api/organizations/{organizationId}/members/{userId}/capacity`; explicit confirmation, row-version concurrency, 1–168 weekly hours, time-zone string and non-overlapping availability windows. |
| CAND-017 proposal | `POST/GET/PATCH /api/projects/{projectId}/schedule-proposals...`; schema `assignment_schedule_proposal.v1`; local canonical model `portfolio-capacity-scheduler.v1`; draft `AssignmentScheduleProposal`; source/version snapshot, audit and zero-token local usage ledger. |
| CAND-017 mutation | `POST .../{draftId}/confirm` or `/reject`; selected rows only, permission + Task/source concurrency recheck, idempotency key and receipt/read-back links. |
| CAND-008 generation | `POST /api/ai/groups/{groupId}/task-drafts`; job `task_draft_native`; schema `task_draft.v5`; snapshot `task_draft_source_snapshot.v1`; strong-model hint `deepseek-v4-pro`, canonical privacy/budget/provider routing and strict semantic validation. |
| CAND-008 review | Canonical `/api/ai/drafts/{draftId}` GET/PATCH/reject plus confirm action `create_tasks`; Group source refs and Project member IDs are server allowlists; Project-management permission is required. |
| Persistence | P009 additively creates `OrganizationMemberCapacityProfiles` and `MemberAvailabilityWindows`; CAND-008 reuses canonical AI job/draft/source/usage/audit storage and needs no migration. |
| Rollback | Hide/disable Capacity/Schedule while retaining manual Task editing and dormant additive P009 data; disable Group native Task Draft generation while retaining Group messages and manual Task creation. Historical drafts/receipts remain readable. |

### 27.3 Security, privacy and compatibility closure

- Both paths derive tenant, Project, member and source scopes server-side. Cross-tenant IDs return nondisclosing deny results and never create a job/draft.
- CAND-017 uses deterministic logic for capacity, deadlines, dependencies and evidence scoring. A model cannot override hard constraints, manufacture capacity or infer performance. The local canonical job is intentional and has no provider-failure state; persistence, sources, usage, audit and lifecycle still apply.
- CAND-008 rejects hallucinated source refs, unsupported fields/statuses, duplicate client IDs, unauthorized assignees and non-reviewable provider output. No offline/mock fallback is presented as a newly generated draft.
- Both mutation paths require editable draft, explicit selective confirmation, current domain permission, row/source concurrency and idempotency. Neither performs autonomous mutation.
- Browser closure found and fixed a first-open watcher race where Group reset could erase the selected message IDs before the native request was submitted. The captured request is now reapplied only when its Group and nonce still match.
- A regression gate discovered that a deactivated Organization member could still read a Project. `ProjectService` now checks the active-Organization boundary before non-admin Project access/manage decisions; the existing security test is retained as evidence.
- Existing CAND-001/002/005/006/015/016/018/020/021, AI chat/Activity, Task/Project/Group and Week 1 authorization flows remain compatibility requirements.

### 27.4 Verification evidence

| Gate | Result |
|---|---|
| Full unit suite | **407/407 PASS**, including strict `task_draft.v5` contract tests. |
| Focused CAND-017/CAND-008 integration | **5/5 PASS**: happy/edit/selective confirm/read-back/idempotency, private aggregate, cross-tenant and stale source. |
| P009 migration evidence | PASS: `Up` contains only additive capacity/profile tables, indexes and foreign keys; normal rollback drops only those newly added tables. |
| Full WebFeature suite | **37/37 PASS**. |
| Chromium native E2E | **2/2 PASS** after closure: portfolio proposal stays non-mutating until confirm; real Group message selection opens the native draft on first click, reload restores the Draft tab, and edit/selective confirm returns a receipt. |
| Vue/TypeScript typecheck | PASS. |
| Vite production build | PASS; generated `wwwroot/dist` refreshed only by Vite. |
| Full `.NET` solution build | PASS, 0 errors; existing analyzer/performance warnings remain recorded rather than hidden. |
| Full integration regression | **134/134 PASS** after the active-Organization security closure. |
| Preview | `http://127.0.0.1:5010/dashboard` running from the final rebuilt binary with AI worker enabled; login endpoint HTTP 200. |

Named evidence: `TEST-PORTFOLIO-SCHEDULE-E2E`, `TEST-TASK-DRAFT-E2E`, `PortfolioScheduleApiTests`, `PortfolioScheduleMigrationTests`, `SourceLinkedTaskDraftApiTests` and `TaskDraftAiContractTests`.

### 27.5 Updated disposition and coverage closure

- `CAND-017`: `NATIVE_COMPLETE` for declared capacity/availability, authorized cross-project workload, deterministic assignment/date proposal, native edit/selective confirm/reject and durable receipt. External Google/Outlook calendar sync remains optional future integration, not an undispositioned gap in this bounded Qaly contract.
- `CAND-008`: `NATIVE_COMPLETE` for locked AI-04 Group selected-message → structured Task draft → native review/selective confirm/read-back. Meeting/Wiki source adapters retain explicit CAND-010/CAND-012 dispositions.
- GAP-008 and GAP-025 are closed for these bounded contracts. New frontend orphan = 0; new backend route/job/schema orphan = 0; selected acceptance item without verification method = 0.
- The catalog remains **21 top-level candidates**: **11 are closed** for bounded contracts and **10 still contain implementation work**.
- Inventory denominators remain 117/117 previously audited surfaces and 37/37 previously audited capabilities dispositioned. New Capacity and native Task Draft route/schema/caller/renderer/test chains have explicit dispositions.

**Coverage gate: PASS. Product completeness: NOT PASS.** All discovered items remain dispositioned, but 10 candidate areas are intentionally still backlog.

### 27.6 Superseding NEXT_IMPLEMENTATION_GOAL

> Implement only `CAND-007 — AI-03 selected-range Group Summary closure` on the current main baseline while preserving all completed candidates and Week 1 flows. Bind the request to an exact ordered 1–50 message selection from one authorized Group; persist range identity/version/hash and canonical sources; return structured summary, decisions, unresolved questions and action candidates with per-item message source links; render a native Group review card with queued/running/cancel/retry/success/empty/degraded/error, cache/freshness and reload/read-back. Reject deleted, foreign, mixed-group, private or stale sources without fabricated fallback. Add strict schema/semantic validation plus unit, integration and Chromium evidence. Do not add Meeting/Wiki adapters, autonomous actions or another candidate until CAND-007 is green end-to-end.

## 28. Implementation checkpoint — CAND-007 Group Summary + CAND-009 Dashboard Strategic Brief + CAND-010 Meeting Checknote

**Checkpoint date:** 2026-08-02 (Asia/Saigon)
**Baseline:** `main` at `60f2cd4e9ae9f21a25855b6fd4cdb781b0beaf46`, equal to `origin/main` before this local increment.
**Workspace note:** implemented and verified inside the existing dirty working tree. This checkpoint does not claim a commit/push and does not reclassify unrelated local changes.

### 28.1 Completed user outcomes

#### CAND-007 — Selected-message Group Summary

- A Group member selects the exact messages to analyze and opens the native AI Summary action. Qaly sends only those ordered, authorized message IDs; it does not silently summarize the entire conversation.
- The result is a structured `group_selected_summary.v1`: bounded range identity, summary points, decisions, unresolved questions, action candidates and message-level source references. Every factual item can open its originating Group message.
- The Group panel reports honest queued/running/retrying/succeeded/empty/failed/canceled states, provider/model truth, cache/freshness and reload/read-back. It never renders a seed/mock result as a fresh model response.
- This is read-only decision support. An action candidate is not a Task mutation; the existing source-linked Task Draft capability remains the explicit review/confirm path.

#### CAND-009 — Dashboard Strategic Brief

- A workspace manager can generate a native strategic brief from the selected authorized organization/project scope instead of sending client-supplied metrics to a generic prompt.
- The server builds the snapshot, computes authoritative project/task metrics and excludes private Tasks before provider routing. The result returns structured summary points, risks and priorities grounded by metric keys and authorized Project/Task source links.
- The Dashboard card now owns generation, polling, cancel/retry, success/empty/degraded/error, provider/model truth, stale indication and reload/read-back. The deterministic dashboard remains available when AI is disabled or fails.
- The legacy `/api/dashboard/ai-strategy` parallel path is explicitly retired with HTTP `410`; it cannot bypass the canonical job/source/privacy/usage path.

#### CAND-010 — Canonical Meeting Checknote

- An authorized Meeting participant can submit an allowed transcript/import and receive a native checknote containing a grounded summary, decisions, risks and editable action-item drafts with transcript evidence offsets/quotes.
- The canonical `meeting_checknote` AI job is persisted before provider invocation. Success stores the strict provider output and the compatible editable Meeting draft; provider unavailable, timeout, schema-invalid/repair exhaustion or request cancellation persists an honest terminal job rather than fabricating a checknote.
- Reload uses `GET /api/meetings/{meetingId}/auto-checknote?projectId=...` to restore the durable checknote, draft ID, source evidence and actual provider/model. No Task is created automatically; action items remain reviewable Meeting data and any domain mutation still needs the existing explicit flow.
- The orphan generic `/api/ai/meetings/{meetingId}/extract-actions` path is retired with HTTP `410`, so Meeting AI has one canonical behavior rather than two inconsistent transports.

### 28.2 Decision-complete contracts

| Candidate | API / job / schema | Authorized input and grounding | Native output / UI | Mutation and rollback |
|---|---|---|---|---|
| CAND-007 | `POST /api/ai/groups/{groupId}/summaries`; canonical job `group_selected_summary`; schema `group_selected_summary.v1`; standard job detail/result/cancel/retry/read-back | Project ID plus 1–50 exact ordered message IDs from one accessible Group; server snapshot/version/hash and `group_message` job sources; deleted/foreign/mixed Group IDs fail closed | Group AI panel renders range, summary, decisions, questions, actions, message source links, provider/model, freshness and lifecycle | No mutation. Disable native Summary action to roll back; Group chat and manual workflows remain available. |
| CAND-009 | `POST /api/ai/dashboard/strategic-brief`; canonical job `dashboard_strategic_brief`; schema `dashboard_strategic_brief.v1` | Server-resolved organization/project visibility and server-calculated metrics; private Tasks excluded before prompt; metric/source allowlist validated | Existing Strategic Overview card renders typed coverage, metrics, summary, risks, priorities, source links, lifecycle and reload | No mutation. Hide/disable brief while preserving deterministic Dashboard metrics. Legacy route remains `410`. |
| CAND-010 | `POST` and `GET /api/meetings/{meetingId}/auto-checknote`; canonical job `meeting_checknote`; schema `meeting_checknote.v1`; compatible editable draft `meeting_action_extract.v4` | Authorized Meeting/Project, privacy-eligible transcript/import, participant list and transcript snapshot; evidence ranges must be within the submitted transcript | Meeting checknote surface renders safe progress stages, summary evidence, decisions, risks, action drafts, actual provider/model, retry/cancel/failure and durable read-back | No autonomous Task mutation. Disable AI checknote while retaining transcript/manual notes. Orphan generic extraction route remains `410`. |

All three schemas have strict JSON Schema plus semantic validation. Unknown source references, invalid IDs/ranges, malformed structures and unsupported enum values fail before a result is exposed.

### 28.3 Security, privacy, lifecycle and compatibility closure

- Tenant, Project, Group, Meeting and source scope are resolved server-side; client IDs never widen visibility. Cross-tenant/foreign sources return nondisclosing failures and do not create a usable artifact.
- Group Summary read-back is requester-bound for non-admin users. A user who can access the linked Project but is not the requester cannot read summarized Group messages; this closes the project-membership-to-chat leakage found during final reconciliation.
- Dashboard Strategic Brief read-back is requester-bound and its prompt snapshot excludes private Tasks. Metric values shown in the response are reconciled against the server snapshot rather than trusted from model prose.
- Meeting Checknote persists Running before gateway execution and persists Succeeded, Failed or Canceled terminal truth. Its gateway receives the canonical AI job ID, allowing provider routing, usage and audit correlation rather than an unlinked synchronous call.
- Provider unavailable/timeout, invalid schema/repair exhaustion, budget/policy deny and cancellation never become fake success. Actual provider/model and `IsMock` are carried to the native surfaces.
- Existing AI Assistant/Action Composer, Project/Task/Group workflows, staffing/scheduling candidates and Week 1 permission rules remain regression requirements. These candidates add no migration and no autonomous destructive behavior.

### 28.4 Verification evidence

| Gate | Result |
|---|---|
| Strict contract unit tests for Group/Dashboard/Meeting | **3/3 PASS** within the full unit suite |
| Focused native API/lifecycle/security tests | **5/5 PASS**: Group grounded read-back plus outsider/project-only deny; Dashboard private exclusion plus legacy `410`; Meeting success/read-back plus provider/schema failure terminal jobs and legacy `410` |
| Full unit suite | **410/410 PASS** |
| Full integration suite | **139/139 PASS** |
| Full WebFeature suite | **37/37 PASS** |
| Chromium native journeys | **3/3 PASS**: Dashboard generate/reload; exact Group message selection/generate/reload; Meeting durable checknote read-back |
| Vue/TypeScript typecheck | PASS |
| Vite production build | PASS; generated `wwwroot/dist` refreshed by the standard build |
| Full `.NET` solution build | PASS, **0 warnings / 0 errors** on the final build |
| Diff hygiene | `git diff --check` PASS; only existing line-ending notices were emitted |
| Preview | `http://127.0.0.1:5010/dashboard` running from the final rebuilt binary with canonical AI and privacy workers enabled |

Named evidence: `TEST-CAND-007-GROUP-SUMMARY`, `TEST-CAND-009-DASHBOARD-BRIEF`, `TEST-CAND-010-MEETING-CHECKNOTE`, `TEST-CAND-010-MEETING-FAILURE-LIFECYCLE`, `TEST-CAND-007-E2E`, `TEST-CAND-009-E2E` and `TEST-CAND-010-E2E`.

### 28.5 Updated disposition and coverage closure

- `CAND-007`: `NATIVE_COMPLETE` for exact selected-message Group summary, canonical lifecycle, source-open and durable read-back.
- `CAND-009`: `NATIVE_COMPLETE` for a server-snapshot Dashboard Strategic Brief with private-data exclusion, metric grounding and canonical lifecycle.
- `CAND-010`: `NATIVE_COMPLETE` for the bounded privacy-gated Meeting transcript → strict checknote/draft → durable read-back contract. Selective Task creation remains a separate explicit capability and is not falsely claimed here.
- GAP-007, GAP-009 and GAP-011 are closed for these bounded contracts. The two displaced legacy endpoints have explicit `LEGACY_OR_DUPLICATE`/HTTP `410` disposition rather than remaining backend orphans.
- The catalog remains **21 top-level candidates**: **14 are closed** for their bounded contracts and **7 still contain implementation work**.
- Inventory denominators remain 117/117 audited surfaces and 37/37 previously audited capabilities dispositioned. Each new route/job/schema has a native caller, renderer and verification; new UI orphan = 0, new backend orphan = 0, selected acceptance item without verification = 0.

**Coverage gate: PASS. Product completeness: NOT PASS.** All discovered surfaces/capabilities retain a disposition; the seven remaining candidate areas are intentional backlog, not hidden coverage gaps.

### 28.6 Superseding NEXT_IMPLEMENTATION_GOAL

> Implement only `CAND-020 — Native Group Poll Draft v1` on top of the completed Assistant Foundation and Group permission/source contracts. From the singleton `Trợ lý AI` and the native Group Poll surface, accept a short poll intent, clarify only missing required fields, and create a structured editable `group_poll_draft.v1` containing one question, 2–10 options, multi-select flag and optional expiry. Route through the registered capability/context registry, canonical AI job, strict schema/semantic validation, actual model/provider receipt, privacy/budget/usage/audit and durable session/draft read-back. Require Group-management permission, explicit confirm, idempotency and concurrency before creating the poll; support edit/reject/retry/cancel and honest provider/timeout/schema-invalid/policy-deny states. Add unit, integration and Chromium evidence for cross-Group/tenant deny, stale Group/source, duplicate confirm and reload. Do not implement multi-question Form/Quiz, scoring, leaderboard, branching, presentation mode, Project creation or another mutation adapter in this slice.

## 29. Plan amendment — CAND-022 Adaptive Skill-Aware AI Work Orchestrator

**Amendment date:** 2026-08-03 (Asia/Saigon)
**Baseline inspected:** `main` at `60f2cd4e9ae9f21a25855b6fd4cdb781b0beaf46`; existing local implementation remains uncommitted at this amendment point.
**Change boundary:** planning only. No source, schema file, migration, component, test, generated bundle, commit or push is created by this amendment.

### 29.1 Product intent

Qaly Assistant must stop behaving like a narrow keyword dispatcher and become a bounded native work orchestrator:

1. understand the user's outcome, not only match a noun/verb;
2. identify entity scope, constraints, unknowns and risk;
3. discover and rank the authorized AI skills/capabilities that could help;
4. build a visible, structured work plan;
5. retrieve only authorized sources and execute only registered tools;
6. verify schema, sources and domain results after every step;
7. answer with what was learned, what was executed, what remains unknown and what needs confirmation;
8. preserve/reload the plan and safe activity timeline without exposing chain-of-thought.

“AI skill” in this section means an assistant reasoning/retrieval/action capability such as `research.plan.v1` or `task.create.v1`. It is distinct from the member programming-skill taxonomy delivered by CAND-015/016, though an authorized staffing skill may consume that evidence later.

This does not mean autonomous unrestricted execution. A model may propose a skill or plan step, but the server-owned registry decides whether it exists, whether the caller is authorized and whether it is read-only, draft-only or executable after confirmation.

### 29.2 Verified current-state gaps

| GAP-ID | Runtime evidence | Consequence | Required disposition |
|---|---|---|---|
| GAP-073 | `AiAssistantCapabilityIntentClassifier.Infer` uses deterministic Vietnamese/English phrase matching. | Broad or compound goals are reduced to one of three intents; wording outside the phrase list is misrouted. | Replace as authority with structured goal analysis plus deterministic server reconciliation. Keep rules only as a low-cost hint/fallback. |
| GAP-074 | `AiAssistantCapabilityCatalog` is a static dictionary containing grounded read, Research Plan and Task create only. | The assistant cannot discover the 14 completed native workflows or explain which skill/surface can complete a request. | Add versioned discovery descriptors; only eligible workflows receive executable adapters, while other surfaces remain navigation/manual guidance. Native workflows remain the execution authority. |
| GAP-075 | Context registry infers one capability before materializing sources. | The model cannot decompose one request into retrieve → compare → recommend → draft → verify steps. | Introduce a bounded `assistant_work_plan.v1` DAG and step reconciler. |
| GAP-076 | `AssistantTurnAsync` returns unsupported mutation before general analysis. | Unsupported requests become dead ends instead of an analysis, safe manual path or implementation-gap proposal. | Add `unsupported_but_analyzed` with understood goal, missing skill, safe alternatives and no fabricated execution. |
| GAP-077 | Research Plan explicitly runs with `Tools = null`; only `task.create.v1` can hand off from its action graph. | Research is grounded but cannot iteratively retrieve missing authorized evidence or verify a proposed step. | Add a read-only executor loop after the plan contract is stable. |
| GAP-078 | Legacy `AiTools` exposes multiple reads/writes outside the canonical capability catalog. | Opening those tools directly would bypass native draft/confirmation contracts and create duplicate mutation paths. | Quarantine legacy write tools; wrap each allowed operation in a canonical skill adapter or retire it. |
| GAP-079 | No repository/source-code or test-manifest context adapter exists in the product assistant. | “Run tests for all CAND” cannot be fulfilled or even mapped to repeatable evidence. | Add a separate developer/demo-only allowlisted Test Orchestrator after the read-only loop. Never expose free-form shell. |
| GAP-080 | Current UI exposes operational activity but not a decision-complete goal/scope/selected-skill/work-plan artifact. | The user sees routing failure without knowing what AI understood or what capability is missing. | Add a native Work Plan card and safe step timeline to the existing singleton workspace. |
| GAP-081 | There is no evaluated prompt corpus for compound intent, skill ranking, plan convergence or unsupported-goal usefulness. | A more agentic router could look flexible while becoming nondeterministic and unsafe. | Version prompts and add offline/live-contract evals, tool traces and regression thresholds. |

The desired behavior is therefore not “let DeepSeek call every method”. It is “let a strong model propose a structured goal and plan, then let deterministic policy narrow it to safe registered skills”.

### 29.3 Candidate definition and score

**CAND-022 — Adaptive Skill-Aware AI Work Orchestrator.**

- User job: describe an outcome naturally, even when it spans several Qaly modules, and receive either a grounded answer, a safe executable plan or a precise explanation of the missing capability.
- Why AI: goal decomposition, ambiguous-language interpretation, option generation and skill ranking benefit from a strong reasoning model; permission checks, hard constraints, calculations, execution and verification remain deterministic.
- Trigger: the existing singleton `Trợ lý AI` composer from any route; native page/card buttons may prefill context but do not create a separate assistant.
- Model routing: `reasoning_strong`/preferred `deepseek-v4-pro` for ambiguous or multi-step goal analysis and plan synthesis; deterministic/small routing for greeting, exact commands and already-known navigation. The actual provider/model must be shown; user model choice cannot bypass policy.
- Rollback: feature-disable adaptive planning and fall back to the current registered single-capability paths. Existing native cards and manual workflows remain intact.

Score: outcome **25/25** + gap closure **20/20** + AI-native fit **15/15** + canonical reuse **15/15** + testability **10/10** + quota fit **13/15** = **98/100** for Phase A. Full A–D delivery has a quota veto and must not be attempted in one run.

Risk vetoes for the whole candidate:

- unrestricted shell, dynamic code execution or model-authored SQL;
- tool/capability use before server authorization;
- autonomous Project/Group/Meeting/Poll/Task mutation;
- model-selected cross-tenant sources;
- hidden recursion, unlimited steps or unbounded token/cost consumption;
- storing or displaying private chain-of-thought;
- self-installing/self-modifying skills without reviewed code and contracts;
- production test execution against real tenant data.

### 29.4 Target architecture

```text
User message + current route + durable session
                  |
                  v
       Goal Interpreter (strong model)
       assistant_goal_analysis.v1
                  |
                  v
 Server Skill Discovery + Policy Filter
  permission / tenant / feature / budget
                  |
                  v
       Work Planner + Reconciler
         assistant_work_plan.v1
                  |
          +-------+-------+
          |               |
   Read/research steps   Mutation proposal
          |               |
 authorized adapters    editable draft only
          |               |
          +-------+-------+
                  v
 Schema/source/domain verifier
                  |
                  v
 Answer + sources + safe activity + read-back
```

The model never receives an unrestricted tool list. Discovery follows this order:

1. server filters skill descriptors by authenticated user, tenant, route/entity, feature flag, privacy and budget;
2. model ranks only the filtered descriptor metadata and explains the fit;
3. server validates selected skill IDs, input schemas, dependencies, maximum steps and confirmation policy;
4. context sources are materialized after the selected plan is authorized;
5. executor invokes adapters; every result is schema/source/domain validated;
6. mutation steps stop at an editable draft and require explicit domain confirmation.

### 29.5 Skill descriptor standard

Every assistant skill must declare:

| Field | Required meaning |
|---|---|
| `skillId`, `version`, `status` | Stable identity, semantic version and enabled/degraded/disabled state. |
| `title`, `description`, `userJobs` | What decision/outcome it supports; not generic “AI insights”. |
| `positiveExamples`, `negativeExamples` | Routing evidence and explicit non-goals. |
| `inputSchemaId`, `outputSchemaId`, `rendererId` | Machine-valid contract and native UI renderer. |
| `requiredScopes`, `entityTypes`, `contextSourceIds` | Authorization and data boundaries. |
| `riskClass`, `confirmationPolicy` | `read_only`, `artifact`, `mutation_draft`; none/handoff/explicit confirm. |
| `executorKind`, `adapterId` | Deterministic read, canonical AI job, native draft action or demo manifest. |
| `modelProfile`, `maxTokens`, `maxCost`, `timeout`, `maxAttempts` | Budget and latency envelope. |
| `verificationPolicy` | Schema, source, domain invariant, read-back and evidence requirements. |
| `featureFlag`, `rollbackPath`, `owner` | Operational ownership and safe disable path. |

Unknown or disabled skill IDs fail closed. A model-proposed skill that is absent from the registry becomes a visible capability-gap proposal, never an invented successful action.

### 29.6 Goal-analysis and work-plan contracts

`assistant_goal_analysis.v1` must return:

- normalized objective and user job;
- read/analysis/artifact/mutation/demo intent facets rather than one flat keyword label;
- candidate entity scopes with confidence and why each scope applies;
- explicit constraints, unknowns and assumptions;
- required data classes and capability traits;
- ranked candidate skill IDs with fit reason;
- risk level and whether confirmation is required;
- disposition: `answerable`, `clarification_required`, `plannable`, `unsupported_but_analyzed` or `policy_blocked`.

`assistant_work_plan.v1` must return a bounded DAG:

```json
{
  "schemaId": "assistant_work_plan.v1",
  "objective": "...",
  "scope": { "type": "workspace|project|task|group|meeting", "entityIds": [] },
  "selectedSkills": [{ "skillId": "research.plan.v1", "version": "1", "reason": "..." }],
  "steps": [{
    "stepId": "S1",
    "kind": "retrieve|analyze|call_skill|verify|present",
    "skillId": null,
    "sourceIds": [],
    "dependencyIds": [],
    "expectedOutputSchemaId": "...",
    "verificationIds": [],
    "mutationClass": "none|draft|confirm",
    "state": "planned"
  }],
  "blockingUnknowns": [],
  "maxSteps": 8,
  "maxAttemptsPerStep": 2,
  "stopConditions": ["objective_satisfied", "blocking_unknown", "policy_denied", "budget_exhausted"],
  "requiresPlanApproval": false
}
```

Phase A does not execute an arbitrary multi-step loop. It analyzes/reconciles the plan, then hands off to exactly one already-registered bounded capability (`grounded.read`, `research.plan.v1` or `task.create.v1`) or returns `unsupported_but_analyzed`. Phase B introduces bounded read-only iteration only after Phase A is green.

### 29.7 Native workspace interaction

After the user sends a prompt, the existing Assistant workspace shows:

1. **Đã hiểu mục tiêu** — one-sentence objective, detected scope and confidence;
2. **Kỹ năng được chọn** — skill name, why selected, permission/risk badge and actual model profile;
3. **Kế hoạch thực hiện** — expandable ordered/DAG steps with queued/running/verified/skipped/blocked/failed states;
4. **Nguồn đã dùng** — read/redacted/skipped/denied disclosures and freshness;
5. **Kết quả** — grounded answer or existing native artifact renderer;
6. **Chưa thể làm tự động** — missing skill plus manual path and optional “Đưa vào backlog”, never a generic failure;
7. **Xác nhận** — only for a canonical editable mutation draft.

The activity timeline may expose safe operational facts such as “đang xác định phạm vi” or “đã kiểm quyền 3 nguồn”. It must not show hidden chain-of-thought, system prompts, secrets or raw private payloads.

Example behavior for the screenshot request:

- before CAND-022C: understand “demo all implemented CAND”, identify `demo.test.run.v1` as missing, list the 14 completed CAND from the plan/evidence source if authorized, provide current repeatable commands/manual demo paths and offer the capability-gap plan;
- after CAND-022C in Development/Test only: select the allowlisted manifest, display the proposed suites and cost/time, require confirmation, run in isolated test data, stream safe stages and produce a durable PASS/FAIL evidence report;
- in Production: return `policy_blocked` with no test process started.

### 29.8 Phased execution plan

| Phase | Outcome | Scope guard | Status |
|---|---|---|---|
| CAND-022A — Goal Understanding + Skill Discovery | Free-form prompt becomes validated goal analysis, ranked authorized skills and visible work plan; handoff to one existing capability or useful unsupported analysis. | No migration, recursive loop, new mutation or shell. | **PRIMARY** |
| CAND-022B — Bounded Read-only Agent Loop | Execute up to eight dependency-ordered retrieval/analysis/verification steps with retry/cancel/resume/read-back. | Read-only skills first; no write tool. | Deferred behind A |
| CAND-022C — Safe Demo/Test Orchestrator | Development/Test-only allowlisted manifests can run selected CAND test/demo evidence with progress and report. | No arbitrary command, production deny, isolated data. | Deferred behind B |
| CAND-022D — Native Skill Packs | Register eligible completed CAND as discoverable read/artifact/mutation-draft adapters; register non-executable native surfaces as navigation/manual guidance only. | One adapter per reviewed slice; mutation draft/confirm rules unchanged. | Incremental backlog |

### 29.9 PRIMARY SLICE — CAND-022A

Selection rationale: it closes the architectural cause of the screenshot failure while reusing CAND-021 session/context/capability/research infrastructure. It is implementable in 1–2 person-days, needs no migration and makes unsupported requests useful without pretending to execute them. No Stretch is selected because the recursive executor and test runner introduce independent security/operational gates.

Maximum three implementation tasks:

1. **TASK-SO-1 — Contracts and reconciler.** Add strict `assistant_goal_analysis.v1`, `assistant_work_plan.v1` and versioned skill descriptors for only the three existing canonical assistant capabilities. Use a strong-model planner with prompt versioning, schema repair limit and deterministic permission/source/risk reconciliation.
2. **TASK-SO-2 — Router and native Work Plan UI.** Replace keyword-first authority with analyze → reconcile → one-capability handoff; preserve deterministic fast hints/fallback. Render goal/scope/selected skills/plan/disclosures and `unsupported_but_analyzed` inside the existing durable session and bottom-composer workspace.
3. **TASK-SO-3 — Evaluation and regression closure.** Add prompt fixtures, unit/integration/Chromium evidence, provider/schema/budget/policy failure behavior and regression for Task draft, Research Plan, context registry, reload and Week 1 authorization.

### 29.10 CAND-022A Definition of Done

- A natural-language request may express multiple intent facets; it is not forced through a keyword rule before analysis.
- Only server-authorized descriptor metadata is offered for selection; model-proposed unknown skills fail closed.
- The selected skill and why it fits are visible without revealing chain-of-thought.
- Work plan is schema-valid, acyclic, capped and persisted in the current Assistant turn/session read-back.
- Phase A invokes at most one existing bounded capability and never calls a legacy write tool.
- Unsupported requests return understood goal, missing capability, safe alternatives and no fabricated result.
- Grounded factual answers retain source refs/freshness/redaction disclosure.
- Task mutation retains editable draft, selective confirm, idempotency, concurrency, audit and usage behavior.
- Provider unavailable/timeout, schema repair exhaustion, budget deny and context-policy deny have distinct honest states.
- Actual provider/model, prompt version, selected skill IDs, cost/usage and correlation IDs are auditable.
- Reload restores goal, selected skill, plan state, disclosures and resulting artifact/answer.
- Feature disable restores current single-capability router without breaking existing native pages/cards.

### 29.11 Required verification

Minimum prompt/evidence matrix:

- simple grounded question selects `grounded.read.v1`;
- ambiguous risk/option prompt selects `research.plan.v1`;
- Task creation prompt selects `task.create.v1`, clarifies missing Project and stops at draft;
- compound “analyze risk then create tasks” produces a bounded plan and Phase-A single handoff instead of autonomous two-step mutation;
- “run all 14 CAND tests” returns `unsupported_but_analyzed` and identifies missing `demo.test.run.v1` before CAND-022C;
- create Project/Group/Meeting/Poll produces a useful gap/manual-path response, not fake completion;
- unknown skill, forged requested capability and forged source ID fail closed;
- cross-tenant/private source is excluded before model routing;
- prompt injection inside retrieved source cannot alter the work plan or tools;
- provider unavailable/timeout, invalid goal schema and invalid work-plan DAG;
- step/budget/token/repair limit exhaustion;
- cancel, duplicate client turn, stale session version and reload/read-back;
- Vietnamese, English, typo, short prompt and compound prompt corpus;
- existing Assistant Task draft, Research Plan, Dashboard/Group/Meeting native cards and Week 1 permission regression.

Evaluation release gates:

- known-intent top-3 skill recall ≥ 95% on the versioned fixture set;
- unauthorized skill selection accepted by reconciler = 0;
- unsupported prompt with useful goal/gap/manual-path response ≥ 90%;
- factual claims without an authorized source ref = 0 for grounded modes;
- autonomous mutation before explicit confirmation = 0;
- plan cycle, step overflow or unknown schema accepted = 0.

### 29.12 Coverage and priority update

- Runtime status remains unchanged: the 14 previously closed candidates stay `NATIVE_COMPLETE` for their bounded contracts.
- `CAND-022` is newly dispositioned `MISSING_HIGH_VALUE`; this amendment does not claim implementation.
- The catalog is now **22 top-level candidates**: **14 closed** and **8 containing implementation work**.
- Existing runtime inventory denominators remain 117/117 audited surfaces and 37/37 previously audited capabilities dispositioned. Planned CAND-022 surfaces/contracts are not counted as runtime implementations.
- CAND-022A becomes the next Primary because it is a platform prerequisite for broad natural-language use of CAND-020 and every later skill adapter. CAND-020 remains the highest-priority domain mutation adapter after the orchestrator foundation is green.
- New discovered planning gaps GAP-073..081 all map to CAND-022 phases; unowned new gap = 0.

**Coverage gate: PASS. Product completeness: NOT PASS.** This is 100% disposition coverage, not an implementation claim.

### 29.13 Superseding NEXT_IMPLEMENTATION_GOAL

> Implement only `CAND-022A — Goal Understanding + Skill Discovery` from §29 on the current verified workspace. Add strict versioned `assistant_goal_analysis.v1`, `assistant_work_plan.v1` and skill descriptors for only `grounded.read.v1`, `research.plan.v1` and `task.create.v1`; use the preferred strong reasoning profile for ambiguous/compound goals, then deterministically reconcile selected skills against authenticated tenant/entity permission, feature, source, privacy, risk and budget policy. Replace keyword-first authority with analyze → reconcile → exactly one existing bounded capability handoff or `unsupported_but_analyzed`; retain rules only as a cheap hint/fallback. Render and persist goal, scope, selected skills, bounded acyclic plan, source disclosures, safe activity and actual provider/model in the existing singleton Assistant session/workspace. Preserve canonical Task editable/selective-confirm behavior and all existing native cards. Complete TASK-SO-1..3 and the §29.11 matrix. Do not add a recursive executor, free-form shell, repository scanner, test runner, new domain mutation, migration or CAND-020 in this slice.

## 30. Implementation checkpoint — CAND-022A Goal Understanding + Skill Discovery

**Implementation date:** 2026-08-03 (Asia/Saigon)
**Baseline:** `main` at `60f2cd4e9ae9f21a25855b6fd4cdb781b0beaf46`; this checkpoint is still an uncommitted local workspace and has not been pushed.

### 30.1 Delivered behavior

- A free-form Assistant turn now enters `assistant_goal_analysis.v1` before context materialization and capability execution when `AiJobsV4:AssistantGoalPlannerEnabled` is on.
- The preferred provider hint for this strong reasoning step is `deepseek-v4-pro`. Actual provider/model remain runtime receipts; unavailable, mock or schema-invalid output becomes an explicitly labelled deterministic fallback rather than fake model success.
- The server exposes descriptor metadata for exactly `grounded.read.v1`, `research.plan.v1` and `task.create.v1`. Each descriptor now carries version, product job, entity scope, schemas, risk, confirmation, renderer, verification, rollback and owner metadata.
- The model may rank only discovered descriptors. The server then replaces model-authored entity IDs with the authorized client scope, rejects unknown/unauthorized skill IDs, caps selection at one skill and rebuilds an acyclic `assistant_work_plan.v1` with at most eight steps and two attempts per step.
- The selected skill is handed to one existing bounded path without keyword-first authority. Task remains draft/selective-confirm; Research remains read-only proposal; grounded read retains authorized sources. Legacy `AiTools`, shell, SQL, recursive execution and new mutation adapters are not exposed.
- Unsupported goals such as Project creation or “run every CAND demo/test” return `unsupported_but_analyzed`, a concrete missing skill (`project.create.v1` or `demo.test.run.v1`), a safe implementation/manual path and zero mutation.
- Goal analysis, selected/missing skill, Work Plan, source disclosures, five safe activity stages and actual provider/model are stored in the existing durable turn JSON and restored after reload without a migration.
- The singleton Assistant renders a native `assistant-work-plan` card above the answer/artifact. It shows objective, user job, authorized scope, disposition, selected or missing skill, verification-labelled steps and honest fallback warnings without chain-of-thought.
- A dedicated feature flag and environment override (`AI_ASSISTANT_GOAL_PLANNER_ENABLED`) provide rollback to the previous single-capability flow.

### 30.2 Contract and security closure

| Control | Implemented evidence |
|---|---|
| Model contract | Strict schema/prompt/version identity, bounded fields and enums for `assistant_goal_analysis.v1`. |
| Work-plan safety | Unique known step IDs, known dependencies, cycle rejection, one selected skill maximum, allowed step/state/mutation enums, 8-step/2-attempt caps. |
| Authorization | Discovery returns descriptor metadata only; source facts are materialized after selection. Project/tenant access remains server-owned and nondisclosing. |
| Model distrust | Model scope IDs and provider/model claims are not trusted; selected descriptor and plan are reconstructed from server registry/context. |
| Mutation safety | Phase A invokes no new write path. `task.create.v1` still stops at the existing editable draft and explicit selective confirmation. |
| Provider/schema failure | No mock is accepted. Failures produce `UsedFallback=true`, `not_reached` receipts and a warning containing the safe failure/schema reason. |
| Unsupported request | Missing capability is visible and no legacy tool, shell or guessed endpoint is called. |
| Durability/audit | Goal/plan survive session read-back; `assistant_goal.planned` is written beside accepted/context/completed audit events. |
| Rollback | Disable `AiJobsV4:AssistantGoalPlannerEnabled`; existing native cards and legacy bounded Assistant router remain available. |

### 30.3 Verification evidence

| Gate | Result |
|---|---|
| Goal/reconciler unit plus Assistant/context regression | **29/29 PASS** focused; includes authorized selection, model scope replacement, invented skill rejection, demo missing-skill fallback and cyclic plan rejection. |
| Assistant integration | **12/12 PASS**; includes task/research/grounded handoff, read-only deny, unknown capability/source, unsupported Project/demo goal, five-stage activity, audit and reload. |
| Full unit suite | **414/414 PASS** |
| Full integration suite | **140/140 PASS** |
| Full WebFeature suite | **37/37 PASS** |
| Chromium evidence | **7/7 PASS** across new Goal/Skill/Work Plan reload, Assistant workspace/read-back, Research Plan and Action Composer/Task handoff. A parallel login run temporarily locked the in-memory test account; the affected Action Composer specs passed 2/2 after restarting the clean preview and running serially. |
| Vue/TypeScript typecheck | PASS |
| Vite production build | PASS; generated `wwwroot/dist` refreshed. |
| Release `.NET` build | PASS, **0 errors**. There are **29 non-blocking analyzer warnings** in the accumulated workspace, chiefly existing performance/style findings and test fixture arrays; this checkpoint does not claim a zero-warning baseline. |
| Diff hygiene | `git diff --check` PASS; only line-ending notices. |
| Preview | `http://127.0.0.1:5010/dashboard` running from the rebuilt Development binary. |

Named new evidence: `TEST-GS-01`, `TEST-GS-E2E` and `AiAssistantGoalPlanningContractTests`.

### 30.4 Updated disposition and remaining gaps

- `CAND-022A`: `NATIVE_COMPLETE` for goal understanding, three-skill discovery/reconciliation, exactly-one-capability handoff, useful unsupported analysis, durable Work Plan UI and feature rollback.
- Top-level `CAND-022`: `PRESENT_PARTIAL`, because the read-only multi-step executor (B), safe demo/test orchestrator (C) and remaining native skill packs (D) are intentionally not implemented.
- GAP-073, GAP-076 and GAP-080 are closed for Phase A. GAP-074 and GAP-075 are partial by design: the descriptor/reconciler base is live for three skills, while broader packs and multi-step execution are deferred. GAP-077, GAP-078, GAP-079 and the full evaluation corpus in GAP-081 remain explicit backlog.
- Catalog count remains **22 top-level CAND**: **14 top-level candidates closed**, **CAND-022A closed inside a partial CAND-022**, and **8 top-level candidate areas still containing work**. No new UI/backend orphan was introduced.

**Coverage gate: PASS. Product completeness: NOT PASS.** CAND-022A is decision-complete and verified; remaining breadth is explicit, not hidden.

### 30.5 Superseding NEXT_IMPLEMENTATION_GOAL

> Implement only `CAND-022B — Bounded Read-only Agent Loop` on top of the completed CAND-022A contracts. Add a server-owned executor for a maximum of eight dependency-ordered `retrieve`, `analyze`, `verify` and `present` steps using only registered read-only adapters and the sources authorized after planning. Persist step queued/running/verified/skipped/blocked/failed state, attempts, safe error codes, provider/model, usage and source receipts in the existing durable Assistant session; support cancel, retry, resume and reload without exposing chain-of-thought. Enforce tenant/entity/privacy/budget checks before every adapter call, reject cycles/unknown skills/stale or private sources, and stop on policy deny, blocking unknown, budget exhaustion or schema repair exhaustion. Render live safe progress in the existing Work Plan card and preserve the current grounded read, Research Plan, Task draft/selective-confirm and Week 1 flows. Add unit, integration and Chromium evidence for bounded convergence, source grounding, provider timeout, invalid schema, retry exhaustion, cancellation, stale read-back and prompt injection in source content. Do not expose write tools, shell, SQL, repository scanning, production test execution, a new mutation adapter, CAND-022C, CAND-022D or CAND-020 in this slice.

### 30.6 Routing correction for natural demo/test wording

The live preview exposed a phrase-shape gap after the original checkpoint. The request `chạy tự động để test các CAND đã implement` did not contain the older contiguous hints `chạy test` or `test CAND`. A provider-produced plan could therefore rank `grounded.read.v1`, causing an unrelated grounded-answer call and a failed `capability_handoff` event instead of the required missing-skill analysis.

The closure has two server-owned layers:

1. known CAND test-execution wording is resolved before provider routing to `unsupported_but_analyzed` with missing `demo.test.run.v1`; neither provider nor executor is called;
2. the output reconciler independently vetoes any model-selected read/research/task skill for the same controlled missing intent, so provider variance cannot reopen the handoff.

The resulting Work Plan has no `call_skill` step, the final process event is completed rather than failed, the user receives the safe adapter/sandbox path, and production behavior still exposes no arbitrary shell or unrestricted test runner.

Verification evidence:

| Gate | Result |
|---|---|
| Exact-phrase contract/provider-bypass unit evidence | **6/6 PASS** in `AiAssistantGoalPlanningContractTests`; the strict gateway mock confirms zero provider calls. |
| Exact-phrase API/session evidence | **2/2 PASS** for `TEST-GS-01` and new `TEST-GS-02`; no failed process event and no selected/called skill. |
| Related Assistant regression | **35/35 unit PASS**, **13/13 integration PASS**. |
| Debug build | PASS, **0 errors**; existing analyzer warnings remain non-blocking. |
| Live preview | Exact phrase returned in **167 ms** with `unsupported_but_analyzed`, `demo.test.run.v1`, `capability_handoff=completed`, `failed events=0`; preview restarted at `http://127.0.0.1:5010/dashboard`. |

`CAND-022A` remains `NATIVE_COMPLETE`; this is a correctness closure, not a new candidate or a claim that `CAND-022C` is implemented.

### 30.7 Real relational demo source packs for five AI-native scenarios

The canonical rich seed now supplies decision-grade relational source data instead of frontend mocks or pre-baked AI answers. It runs for both the InMemory preview and SQL databases after migration, and it idempotently enriches an existing `qaly-demo-2026` database without overwriting user-edited records.

| Demo scenario | Real seeded source | Native behavior enabled |
|---|---|---|
| Dashboard / grounded research | Five active projects with tasks, risks, comments, meetings, groups and operational history already present in the rich seed | Strategic brief, grounded answer and Research Plan can cite current project facts. |
| Group and meeting intelligence | Two populated work groups plus poll, messages, meeting session, transcript import and action mappings already present | Group selected-summary and Meeting Checknote have native context rather than placeholder cards. |
| Member Skill Evidence | Nine organization skills, ten confirmed skill requirements on five completed tasks and five manager-confirmed completion attributions | A member profile can show evidence-backed skills and source tasks across all five active projects. |
| Assignee Recommendation | The same confirmed evidence plus ten skill requirements on five open target tasks | Recommendation can compare required skills with demonstrated delivery evidence instead of guessing from role/title. |
| Portfolio Schedule / Capacity | Twelve organization-member capacity profiles and five future reduced/unavailable windows | Portfolio scheduling can detect cross-project load and availability conflicts with a durable read-back source. |

Data integrity rules:

- the five completed evidence tasks cover `qaly-workos-demo`, `erumi-local-analytics`, `nova-retail-pilot`, `field-ops-mobile` and `ops-compliance-readiness`;
- Nova receives one additional realistic completed task, `Xác nhận dữ liệu POS Wave 1`, because that project previously had no completed evidence source;
- skill evidence is created only from a Done task, a confirmed skill requirement and a named eligible contributor;
- capacity and availability are stored in the canonical P008/P009 entities and therefore work in SQL as well as the local InMemory preview;
- the extension adds only missing records. A second seed produces no duplicate task, skill, requirement, attribution, profile or window;
- no provider result, AI success state or recent model output is seeded. DeepSeek and other configured providers must still generate runtime results from these sources with normal permission, grounding, usage and failure controls.

Verification evidence: `RichDemoSeedTests` **2/2 PASS**, including exact counts (5 active projects, 9 skills, 20 requirements, 5 confirmed attributions, 12 profiles and 5 availability windows), project-by-project evidence closure and second-run idempotency. Infrastructure Debug build PASS with 0 errors; accumulated analyzer warnings remain pre-existing and non-blocking.

## 31. Plan amendment — CAND-023 Governed Project Launch & Operating Orchestrator

**Amendment date:** 2026-08-10 (Asia/Saigon)
**Baseline inspected:** `codex/recent-feature-gap-closure` at `24a66ec84e0f12de13bcd69701411bda7cad39ec`, equal to the fetched `origin/main` before this planning amendment; the existing implementation workspace remains uncommitted.
**Change boundary:** planning and disposition only. This amendment does not claim that Project launch, Rulebook, multi-entity execution or autonomous monitoring is implemented.

### 31.1 Product decision and current verdict

The target user job is no longer only “draft a Project plan.” A user must be able to state an outcome such as “khởi chạy một dự án web SPA production-ready trong 12 tuần,” then let the singleton Qaly Assistant:

1. understand scope, constraints and blocking unknowns;
2. ask only decision-relevant clarification questions;
3. load the effective organization rules and authorized operating facts;
4. evaluate manager/team eligibility, skill evidence and portfolio capacity;
5. create feasible staffing, delivery and schedule scenarios;
6. produce an editable Project/Sprint/Task launch plan;
7. show one reviewable internal mutation batch and separately classified external effects;
8. execute only after explicit authorization;
9. read back every created/updated entity and return durable receipts;
10. monitor the approved baseline and propose, but never silently apply, a replan.

**Current runtime verdict: `MISSING_HIGH_VALUE`.** CAND-022A can understand this goal and correctly report missing `project.create.v1`; CAND-017 can generate a bounded assignment/schedule proposal inside an existing Project; CAND-018 can create Task drafts inside a resolved Project. These are necessary foundations, but they do not compose into an executable Project-launch workflow today.

This candidate must not be described as “fully autonomous AI.” The intended contract is **goal-driven, server-governed orchestration with bounded autonomy and explicit mutation approval**.

### 31.2 Candidate relationship and duplicate disposition

| Existing candidate | Reuse in CAND-023 | Disposition |
|---|---|---|
| CAND-011 — Canonical Project Planner | Original brief → Project/Sprint/Task draft product intent | **SUPERSEDED/SUBSUMED.** CAND-011 is historical scope and receives no separate implementation or coverage credit after this amendment. |
| CAND-015 — Task Skill Taxonomy | Canonical required-skill identities/levels for planned work | Reuse; no inferred or model-created skills. |
| CAND-016 — Member Skill Evidence | Evidence-backed staffing inputs and correction semantics | Reuse; missing evidence remains unknown, not “unskilled.” |
| CAND-006 — Assignee Recommendation | Project-local explainable candidate scoring pattern | Reuse and extend only through deterministic CAND-023 policy. |
| CAND-017 — Portfolio Schedule Copilot | Capacity, availability, workload, dependency and selective-confirm scheduling | Required scheduling foundation; do not create a second capacity engine. |
| CAND-018 — AI Action Composer | Versioned tool registry, editable action set, confirmation, idempotency and receipt | Required mutation framework; expand through reviewed adapters only. |
| CAND-021 — Assistant Foundation | Singleton workspace, durable sessions/turns, context/capability registry and artifact rendering | Required host surface and durability boundary. |
| CAND-022A — Goal Understanding | Natural goal analysis, skill discovery and bounded Work Plan | Required entry contract; already complete for the bounded three-skill catalog. |
| CAND-022B — Read-only Agent Loop | Multi-step authorized retrieval/analysis/verification | **Immediate platform prerequisite** before CAND-023A; Project launch must not invent a second executor. |
| CAND-022D — Native Skill Packs | Registers Project-launch analysis/artifact/mutation-draft skills | Incrementally closes as each CAND-023 phase passes its own gate. |

CAND-023 is therefore a **domain composite capability** on top of the CAND-022 platform orchestrator. CAND-022 remains responsible for how safe plans execute; CAND-023 owns the business meaning, rules, artifacts and actions of launching and operating a Project.

### 31.3 Verified current-state gaps

| GAP-ID | Current evidence | Business consequence | Required disposition |
|---|---|---|---|
| GAP-082 | No versioned Organization Work Rulebook is present in the current CAND catalog. | AI cannot prove manager eligibility, utilization policy, maximum concurrent Projects, separation of duties, required approvals or exception handling. | CAND-023A — server-owned effective Rulebook and rule-decision receipts. |
| GAP-083 | CAND-022A understands goals, but there is no strict durable Project-launch brief or grouped clarification contract. | A broad goal becomes prose/unsupported analysis rather than a reviewable scope baseline. | CAND-023A — `project_launch_brief.v1` plus clarification state. |
| GAP-084 | CAND-017 schedules Tasks inside an existing Project; no launch-time manager/team formation policy exists. | “Available” members may be selected without role eligibility, team coverage, continuity, focus cost or fairness constraints. | CAND-023B — deterministic manager/team scenario engine using CAND-015/016/017 facts. |
| GAP-085 | `assistant_work_plan.v1` is an Assistant process plan, not a domain Project delivery plan. | Users cannot review a versioned scope → milestone/sprint → task/dependency delivery artifact. | CAND-023B — `project_launch_plan.v1` with schema/domain validation. |
| GAP-086 | `project.create.v1`, membership/role, milestone/sprint and dependency adapters are still deferred. | The Assistant cannot execute the approved launch plan. | CAND-023C — reviewed canonical adapters and one bounded launch command handler. |
| GAP-087 | CAND-018 atomicity is task-create bounded; external effects cannot share the database transaction. | Partial Project launch could leave misleading membership, collaboration or integration state. | CAND-023C — transactional internal core, compensation/outbox and truthful partial-effect receipt. |
| GAP-088 | No approved-baseline monitor links Project/Sprint progress, capacity change and rule exceptions to a replan draft. | The Assistant cannot safely adapt when workload, leave, deadline or scope changes. | CAND-023D — event/schedule-triggered monitoring and human-confirmed replan. |
| GAP-089 | There is no one-sentence-goal → real Project/team/sprints/tasks E2E suite. | Product may appear agentic while only rendering plans. | Phase-specific integration/E2E gates plus final composite acceptance journey. |
| GAP-090 | CAND-017 declared capacity does not by itself define meeting/support duty, focus reserve or concurrent-Project policy. | Nominal free hours can still produce unreasonable staffing. | CAND-023A Rulebook + CAND-023B capacity extension; unknown inputs remain visible blockers/risks. |
| GAP-091 | Calendar provider sync, GitHub repository creation and outbound invitations are external side effects with separate credentials/scopes. | A single generic confirmation cannot safely promise completion. | CAND-023C classifies internal and external commands, exposes exact authorization, and persists pending/failed compensation state. |

Unowned new gap after this amendment = 0. The gaps are intentionally phased; they are not implementation claims.

### 31.4 Candidate definition and score

**CAND-023 — Governed Project Launch & Operating Orchestrator.**

- **User job:** turn a business/product outcome into a feasible, staffed, scheduled and reviewable Qaly Project, then operate against the approved baseline.
- **Why AI:** ambiguous goal interpretation, clarification, solution options, work decomposition and trade-off explanation benefit from a strong reasoning model.
- **Why deterministic services:** rule eligibility, permissions, skill evidence bands, capacity, calendar feasibility, dependencies, scoring, concurrency, execution and read-back must be repeatable and auditable.
- **Trigger:** existing singleton `Trợ lý AI`; contextual Project/Organization entry points may prefill authorized scope but do not create another assistant.
- **Primary artifacts:** `project_launch_brief.v1`, `organization_work_rule_decision.v1`, `project_staffing_scenario.v1`, `project_launch_plan.v1`, `project_launch_action_set.v1`, `project_launch_execution_receipt.v1`.
- **Mutation boundary:** no Project/member/sprint/task/integration mutation before an editable batch is explicitly confirmed.
- **Rollback:** phase flags disable launch generation/execution/monitoring independently; manual Project creation and all existing native cards remain available; historical drafts/receipts remain readable.

Full-candidate score: outcome **25/25** + gap closure **20/20** + AI-native fit **15/15** + canonical reuse **15/15** + testability **9/10** + quota fit **3/15** = **87/100**, with a **full-scope quota veto**.
CAND-023A score with its bounded no-domain-mutation scope: **98/100** and is the next domain Primary after CAND-022B is green.

### 31.5 End-to-end state machine

```text
INTAKE
  → CLARIFICATION_REQUIRED | BRIEF_READY
  → RULE_VALIDATION
  → STAFFING_SIMULATION
  → DELIVERY_PLAN
  → PLAN_REVIEW
  → AWAITING_CONFIRMATION
  → EXECUTING_INTERNAL
  → EXECUTING_EXTERNAL | EXTERNAL_DEFERRED
  → READ_BACK_VERIFICATION
  → ACTIVE_MONITORING
  → REPLAN_PROPOSED
  → AWAITING_REPLAN_CONFIRMATION
```

Terminal/honest states include `insufficient_information`, `rule_blocked`, `no_feasible_staffing`, `no_feasible_schedule`, `permission_denied`, `budget_blocked`, `provider_failed`, `stale_plan`, `partial_external_failure`, `canceled`, `rejected` and `rolled_back_or_compensated`.

Rules:

1. Clarification questions are grouped, deduplicated and limited to fields that materially change scope, deadline, budget, compliance, architecture or feasibility.
2. Data already available through authorized Qaly sources is not asked again.
3. Assumptions are visibly separated from facts and require acknowledgement when they affect feasibility.
4. A domain plan and an Assistant process plan are different artifacts linked by IDs; one must not masquerade as the other.
5. Internal mutation and external side effects have separate execution classes and receipts.
6. Monitoring is event/schedule bounded, never an unbounded recursive agent loop.

### 31.6 CAND-023A — Organization Work Rulebook + Project Launch Brief

#### Organization Work Rulebook

The Rulebook is a server-owned, versioned business-policy aggregate. A model may explain a rule or identify a missing policy, but it cannot author, activate, suppress or override an effective rule during Project launch.

Minimum `organization_work_rule_set.v1` fields:

| Rule class | Required contract |
|---|---|
| Identity/effective version | `ruleSetId`, organization, semantic/schema version, status, effective range, approver, row version and superseded version. |
| Role eligibility | Manager/lead/member eligibility, active-membership requirement, required role/scope and separation-of-duties constraints. |
| Skill coverage | Required canonical skills/levels, certification/evidence policy, minimum confidence/recency and allowed unknown-evidence behavior. |
| Capacity/utilization | Weekly capacity authority, maximum planned utilization, operational/risk buffer and approved overtime/exception policy. |
| Concurrent work | Maximum active Projects, focus/context-switch reserve and continuity preference. |
| Calendar collaboration | Required timezone overlap, leave/reduced-capacity treatment, meeting/support duty source policy and Project calendar boundaries. |
| Fairness | Rotation/load-balance rule, prohibited factors, correction/appeal path and no protected-attribute/message-sentiment inference. |
| Delivery governance | Deadline hardness, minimum review/QA/security ownership, Definition of Ready/Done and required approval gates. |
| External effects | Who may invite members, create/link repository, configure webhook, notify external parties or deploy. |
| Exception policy | Blocking vs warning rule, authorized exception approver, reason, expiry and audit requirements. |

If no effective Rulebook exists, Qaly may show a proposed baseline template, but it must remain `policy_missing` and cannot silently become execution authority. An authorized Organization owner must explicitly activate a version.

Every evaluation returns `organization_work_rule_decision.v1` containing rule ID/version, result (`pass|warning|block|unknown`), affected proposal rows, deterministic facts, safe explanation, exception eligibility and source freshness.

#### Project launch brief

`project_launch_brief.v1` contains:

- objective, target users and measurable outcomes;
- MVP/in-scope/out-of-scope deliverables;
- fixed vs negotiable deadline and budget/cost envelope when applicable;
- required technology/integration constraints without presenting generated code as a domain fact;
- security, privacy, accessibility, quality, testing, observability and documentation gates;
- organization, preferred start window and relevant Project/Group/Wiki/manual sources;
- blocking/non-blocking unknowns, grouped clarification questions, facts, assumptions and user acknowledgements;
- source versions/hashes, actual provider/model, prompt version and correlation ID.

CAND-023A performs no Project mutation and no staffing assignment. It ends at a durable, reviewable `BRIEF_READY` plus Rulebook decision artifact.

### 31.7 CAND-023B — Deterministic staffing and delivery scenarios

#### Capacity and team formation

CAND-023B extends, but does not duplicate, `portfolio-capacity-scheduler.v1`.

```text
effective capacity
= contracted/declared work capacity
- approved leave or reduced-capacity windows
- authorized meeting/support duty commitments
- existing cross-Project committed effort
- Rulebook focus/context-switch reserve
- operational and risk buffer
```

Unknown estimate, missing capacity, stale availability, private aggregate or unsupported calendar source is not converted into fabricated free time. Current CAND-017's visible 8-hour task fallback may be used only as an explicitly labelled scenario assumption and must be sensitivity-tested before launch approval.

Manager/team selection order:

1. hard eligibility and active Organization membership;
2. required skill/evidence/certification coverage;
3. capacity and date-window feasibility;
4. concurrent-Project/focus policy;
5. delivery-role coverage and separation of duties;
6. continuity/domain context when supported by authorized deterministic records;
7. fairness/load distribution and exception policy;
8. ranked soft trade-offs and alternatives.

Hard failures disqualify a candidate; a high soft score can never override them. Weights and thresholds come from the effective Rulebook/scoring version, not from a prompt. The AI explains the deterministic scenario and asks follow-up questions; it does not invent performance or personality claims.

`project_staffing_scenario.v1` includes:

- manager candidates, eligibility result, hard rejects and alternatives;
- proposed member/role rows, required-skill coverage, evidence confidence/recency and missing skills;
- load before/after by time bucket, concurrent Projects, focus reserve and privacy-restricted aggregate load;
- timezone/collaboration overlap, leave/reduced-capacity windows and deadline/dependency conflicts;
- fairness/rule decisions, unresolved exceptions and sensitivity scenarios;
- exact source refs or privacy-safe aggregate refs and scoring/rule versions.

#### Delivery plan

`project_launch_plan.v1` is a domain artifact containing:

- Project identity draft, objective, scope, exclusions, lifecycle dates and success measures;
- architecture/quality proposal clearly labelled as proposal rather than existing fact;
- milestones and Sprints, each with objective, dates, capacity and exit criteria;
- Epics/Tasks with acceptance criteria, Definition of Done, estimate, priority, dependencies, required skills, proposed assignee/reviewer and source refs;
- critical path, fixed dates, schedule risks, unallocated work and skill gaps;
- collaboration setup proposal: primary Group, Wiki structure, meeting cadence, notification/analytics setup;
- external integration proposal: GitHub/repository/webhook/deployment entries with credential/scope readiness;
- one to three meaningful scenarios such as fastest feasible, balanced and lowest-risk; no cosmetically different duplicates.

“Sprint” must bind to the existing canonical Milestone/Sprint semantics. CAND-023 must not introduce a duplicate Sprint entity merely to satisfy model output; if the domain mapping is not decision-complete, Sprint mutation remains blocked while the rest of the plan stays reviewable.

### 31.8 CAND-023 capability and action catalog

Discovery descriptors are registered only after their phase passes verification:

| Skill/capability | Risk class | Phase behavior |
|---|---|---|
| `project.launch.analyze.v1` | `artifact` | CAND-023A: create/edit durable brief and Rulebook decisions; zero domain mutation. |
| `project.staffing.plan.v1` | `artifact` | CAND-023B: deterministic staffing/capacity scenarios and delivery plan; zero domain mutation. |
| `project.launch.execute.v1` | `mutation_draft` | CAND-023C: compose one registered action set; explicit batch confirmation required. |
| `project.operation.monitor.v1` | `read_only`/`artifact` | CAND-023D: compare actual state with approved baseline and create replan proposal only. |

Required internal action adapters for CAND-023C:

| Adapter | Contract boundary |
|---|---|
| `project.create.v1` | Create one authorized Project from reviewed identity/lifecycle fields. |
| `project.membership.upsert.v1` | Add an active Organization member to the new Project with reviewed membership role. |
| `project.role.assign.v1` | Assign manager/lead/reviewer roles under Rulebook and current permission. |
| `milestone.create.v1` / canonical Sprint adapter | Create reviewed delivery buckets only after domain mapping is confirmed. |
| `task.create.v1` | Reuse CAND-018; create selected reviewed Tasks and confirmed CAND-015 requirements. |
| `task.dependency.set.v1` | Apply an acyclic same-Project dependency graph after all referenced Tasks exist. |
| `group.create_or_link.v1` | Optional internal collaboration setup with exact membership/source policy. |
| `wiki.bootstrap.v1` | Optional reviewed Wiki structure/content stubs; no fabricated completion claim. |
| `notification.policy.configure.v1` | Optional internal notification settings under owner policy. |

External adapters such as `github.repository.create_or_link.v1`, webhook configuration, external invitation, calendar-provider sync and deployment remain unavailable until credentials, scopes, provider-specific idempotency and compensation are separately decision-complete. Their absence yields `external_deferred`, not fake launch success.

Unknown, disabled, forged or model-invented adapter IDs fail closed. Legacy `AiTools` are not a fallback execution path.

### 31.9 CAND-023C — Batch execution, receipt and rollback

The user reviews one internal launch batch. Commands are grouped by dependency:

```text
Project
  → membership/roles
  → milestones/Sprints
  → Tasks
  → Task skill requirements/dependencies
  → internal collaboration/configuration
  → separately authorized external effects
```

Confirmation requirements:

1. draft is current, pending review and not expired;
2. authenticated actor still has Organization/Project creation and every affected action permission;
3. Rulebook version remains effective and all blocking decisions still pass or have valid approved exceptions;
4. member, skill, capacity, availability, source and plan row versions remain current;
5. edited commands are schema/domain validated and cannot change tenant, adapter, scope or protected source identities;
6. dependency graph is acyclic and every selected downstream command has a selected/satisfied prerequisite;
7. idempotency key + normalized payload owns one execution receipt;
8. internal database writes are all-or-nothing where supported;
9. external effects use outbox/operation state and compensation; they never cause blind replay of internal creation;
10. read-back validates entity IDs, roles, dates, dependencies, links and current row versions before reporting success.

`project_launch_execution_receipt.v1` includes internal transaction outcome, command-by-command status, created/updated entity deep links, skipped/deferred commands, external operation state, compensation/rollback availability, rule/source versions, audit/usage IDs, actual provider/model and verified-at timestamps.

Rollback does not erase audit or completed external effects. Before rollback, Qaly computes an impact diff, checks whether the new Project has accrued user work, rechecks permission/concurrency/rules, and performs normal compensating updates/soft-delete where eligible. Irreversible or manually compensated effects are listed explicitly.

### 31.10 CAND-023D — Active monitoring and governed replan

Monitoring compares the approved launch baseline with deterministic current facts from Project/Sprint progress, Task dependencies, CAND-017 capacity/availability, leave changes, Rulebook updates and authorized operational signals.

Triggers are bounded events or schedules, for example:

- a blocking dependency, milestone risk or fixed-date breach;
- member capacity/availability or active-membership change;
- Rulebook version superseded or exception expired;
- material scope/estimate change;
- provider-independent deterministic progress threshold.

The Assistant may summarize impact and create `project_replan_proposal.v1`. It must not silently change assignee, date, scope, role or deadline. Replan reuses the same visible diff, stale-source, permission, confirmation, receipt and rollback boundaries as launch execution.

### 31.11 Native Assistant UX

The singleton Assistant remains the only global entry point. Its existing Work Plan card hosts a Project-launch artifact rail:

1. **Mục tiêu & câu hỏi còn thiếu**;
2. **Bộ luật đang áp dụng** with version and pass/warn/block decisions;
3. **Phương án đội ngũ** with hard rejects, alternatives and before/after load;
4. **Kế hoạch Project/Sprint/Task** with editable tree and dependencies;
5. **Diff sẽ áp dụng** separating internal and external commands;
6. **Xác nhận** with exact command/risk counts;
7. **Biên nhận & liên kết** after server read-back;
8. **Theo dõi/Replan** after launch.

Canonical entity links, stable back behavior and reload read-back are mandatory. No raw JSON, generic success toast, hidden chain-of-thought or duplicate modal-only planner earns native coverage.

### 31.12 Phased execution plan

| Phase | Outcome | Scope guard | Dependency | Status |
|---|---|---|---|---|
| CAND-023A — Rulebook + Launch Brief | Durable brief, grouped clarification and effective rule decisions. | No staffing assignment, Project mutation or external effect. | CAND-022B | **NEXT DOMAIN PRIMARY after CAND-022B** |
| CAND-023B — Staffing + Delivery Scenario | Feasible manager/team/capacity and Project/Sprint/Task scenarios. | Artifact only; no domain mutation. | 023A + CAND-015/016/017 | Deferred behind A |
| CAND-023C — Confirmed Launch Execution | Internal Project/team/milestone/task launch with receipt; external operations truthfully separated. | Registered adapters only; one reviewed internal batch; no unrestricted tools. | 023B + CAND-018/022D | Deferred behind B |
| CAND-023D — Monitor + Replan | Baseline monitoring and confirmed replan drafts. | No silent mutation; bounded triggers; no autonomous recursion. | 023C + progress/capacity signals | Deferred behind C |

`CAND-020 — Native Group Poll Draft` remains valid backlog but is reprioritized behind CAND-023A because Project launch is now the selected product outcome. It may still be implemented independently if the Product Owner changes the outcome priority; it must not be bundled into a CAND-023 phase.

### 31.13 PRIMARY implementation breakdown — CAND-023A only

Maximum three implementation tasks after CAND-022B is fully green:

1. **TASK-PL-1 — Rulebook decision-complete contract and persistence.** Reconcile current Organization roles/policies, decide missing product semantics with the Product Owner, add versioned effective Rulebook storage/read/admin activation, deterministic evaluator and rule-decision receipts. Do not let AI activate policy.
2. **TASK-PL-2 — Launch Brief skill and durable clarification.** Add strict schemas, prompt/version/eval corpus, authorized context adapters and `project.launch.analyze.v1`; persist brief revisions, grouped blocking questions, assumptions/acknowledgements and source versions in the current Assistant session/artifact framework.
3. **TASK-PL-3 — Native review and evidence closure.** Render Brief/Rulebook tabs in the singleton workspace; add feature rollback, unit/integration/Chromium evidence, current Assistant/CAND regression and honest policy/provider/budget/stale states. Stop at artifact/read-back.

No CAND-023B staffing scenario, Project mutation, new Project action adapter, migration outside approved Rulebook/brief persistence, external integration or monitoring is allowed in this Primary slice.

### 31.14 CAND-023A Definition of Done

- A broad Vietnamese or English Project-launch goal yields `project_launch_brief.v1`, not a generic answer or fake Project success.
- Qaly asks at most eight grouped blocking questions in one round where possible and does not ask authorized facts already present in Qaly.
- Facts, assumptions, unknowns and user acknowledgements are distinct and persist after reload.
- An effective Organization Rulebook version is resolved server-side; missing/expired/conflicting Rulebook states are honest.
- Every rule decision cites rule/version and deterministic source facts without exposing private content.
- Model output cannot create/activate rules, grant exceptions, choose tenant scope or mark a blocking rule as passed.
- Cross-tenant/foreign Organization, forged source, prompt injection and private title/content fail closed before provider/context use.
- Provider unavailable/timeout/schema-invalid, budget deny, cancellation, duplicate turn and stale source have distinct durable states.
- `project.create.v1` remains unavailable; domain mutation count is zero.
- Feature disable returns to the CAND-022 grounded/research/task paths and manual Project workflow.

### 31.15 Full CAND-023 Definition of Done

The top-level candidate remains incomplete until one composite E2E proves all of the following:

- one natural-language objective reaches an approved real Project without hidden manual data surgery;
- manager/team selection uses active membership, Rulebook, CAND-015/016 evidence and CAND-017 capacity rather than title/empty-slot guessing;
- no hard rule, capacity, calendar or dependency constraint is overridden by model scoring;
- skill gaps, unknown capacity, unavailable candidates and infeasible deadlines remain visible and produce alternatives or blockers;
- Project, roles, members, milestones/Sprints, Tasks, skills and dependencies match the reviewed action set after read-back;
- no internal mutation occurs before confirmation; duplicate confirm is idempotent;
- permission revoke, Rulebook/source/version change or workload change before confirm blocks stale execution;
- internal partial failure rolls back transactionally; external partial failure is exact, durable and compensatable;
- reload/resume restores brief, rule decisions, scenarios, plan, execution progress and receipt;
- monitoring detects a controlled deviation and creates a replan proposal without applying it;
- canonical links/navigation, audit, usage, privacy, fairness, feature rollback and manual workflows remain valid.

### 31.16 Required verification matrix

| Layer | Minimum evidence |
|---|---|
| Rulebook unit | Effective-version selection, block/warn/unknown, role eligibility, maximum utilization/concurrent Projects, focus buffer, timezone overlap, required skills, separation of duties, exception expiry and deterministic repeatability. |
| Staffing/schedule unit | Skill gap, missing/stale evidence, leave/reduced capacity, meeting/support reserve, cross-Project load, private aggregate, context-switch policy, fairness, no feasible manager/team, deadline/dependency infeasibility and sensitivity analysis. |
| Contract/schema | Strict brief/rule/scenario/plan/action/receipt schemas, unknown fields/adapters, graph cycles, unsupported Sprint mapping, invalid source refs and semantic reconciliation. |
| Authorization/privacy | Cross-tenant/Organization/Project/member deny, inactive membership, private Task aggregate, forged IDs, revoke between plan/confirm, prompt injection in Wiki/chat/manual source and protected-factor exclusion. |
| Mutation integration | No-write-before-confirm, one internal transaction, dependency ordering, selective command validation, idempotency replay/conflict, concurrency, rollback/compensation, read-back mismatch and audit/receipt. |
| Provider/budget | Strong-model success, unavailable/timeout, schema repair exhaustion, budget hard stop, requested-vs-actual model and zero fabricated fallback. |
| E2E | Goal → clarification → Rulebook → staffing scenarios → editable plan → confirm → Project/team/Sprints/Tasks → receipt/reload; overload/no-feasible, stale/revoke, partial external failure and monitor/replan journeys. |
| Regression | CAND-001/002/006/008/009/010/015/016/017/018/021/022, manual Project/Task/member flows, canonical navigation, browser console/network, migrations and full serial/parallel suites. |

Release evaluation thresholds:

- blocking hard-rule violation accepted = 0;
- unauthorized or inactive member assigned = 0;
- planned load above policy without an approved visible exception = 0;
- model-created skill/evidence/rule/capacity fact accepted = 0;
- domain mutation before explicit confirmation = 0;
- duplicate entities from idempotent replay = 0;
- success receipt with failed read-back = 0;
- grounded staffing/schedule claim without authorized source/rule version = 0;
- unbounded tool/step/external retry path = 0.

### 31.17 Security, privacy and fairness boundaries

- Models never see protected attributes, private foreign-Project details, raw secrets, credentials or unrestricted member activity history.
- Chat/message sentiment, presence surveillance and inferred health/personality are prohibited staffing inputs.
- Private cross-Project work contributes only authorized aggregate load unless the viewer can open the source.
- Evidence correction/appeal and declared capacity/availability correction remain available.
- The caller's authority is re-evaluated before every adapter and at batch confirmation; a generated plan grants no permission.
- Model-generated architecture, estimate and decomposition are proposals. Deterministic domain/rule validators own feasibility.
- Repository creation, webhook, invitation, calendar sync and deployment require explicit registered adapter scopes and separate effect receipts.
- No shell, SQL, dynamic code, self-installed skill, self-modifying Rulebook or unrestricted provider tool list.

### 31.18 Ownership, merge order and rollback

Planned ownership zones:

| Zone | Owner boundary |
|---|---|
| Rulebook/domain/data | Rule entities, evaluator, migration, admin API and policy tests. |
| Assistant orchestration | CAND-022B executor integration, launch skill descriptors, brief/scenario/plan adapters and durable artifact refs. |
| Action execution | Registered adapters, application command handler, idempotency, transaction/outbox, compensation and receipt. |
| Native UI | Existing singleton workspace artifact rail, review/diff/receipt and canonical links; no second assistant. |
| Verification/docs | Schema fixtures, eval corpus, unit/integration/E2E, migration/recovery evidence and CAND disposition. |

Merge order: CAND-022B → CAND-023A contracts/persistence → A UI/evidence → CAND-023B deterministic services/artifacts → CAND-023C internal adapters/execution → optional external adapters → CAND-023D monitoring. Each phase has an independent feature flag and must be green before the next mutation breadth is exposed.

### 31.19 Coverage and priority update

- `CAND-023` is newly dispositioned `MISSING_HIGH_VALUE`; no implementation credit is claimed.
- `CAND-011` is `SUPERSEDED_BY_CAND_023` and no longer an active standalone backlog area or independent coverage denominator.
- The catalog now contains **23 CAND IDs**: **14 top-level candidates closed**, **8 active candidate areas containing work**, and **1 superseded candidate (CAND-011)**. CAND-022A remains complete inside partial CAND-022.
- Runtime surface/capability denominators do not change because this amendment adds no route, schema runtime, renderer or action adapter.
- GAP-082..091 map to CAND-023A–D; unowned new gap = 0.
- Immediate platform priority remains CAND-022B. Immediate domain priority after it becomes CAND-023A. CAND-020 moves behind that outcome without being canceled.

**Coverage gate: PASS. Product completeness: NOT PASS.** The desired Project-launch behavior is now decision-complete enough to phase and verify, but none of CAND-023A–D is implemented by this amendment.

### 31.20 Superseding NEXT_IMPLEMENTATION_GOAL

> First implement only `CAND-022B — Bounded Read-only Agent Loop` exactly as specified in §30.5; do not combine it with a Project mutation. After CAND-022B is green, implement only `CAND-023A — Organization Work Rulebook + Project Launch Brief` from §31. Add a versioned server-owned effective Rulebook and deterministic rule-decision receipts; add strict durable `project_launch_brief.v1` with grouped blocking clarification, facts/assumptions/unknowns, authorized source versions and actual provider/model; register `project.launch.analyze.v1` as an artifact-only skill in the existing capability registry and render Brief/Rulebook review in the singleton Assistant workspace. Preserve CAND-015/016/017/018/021/022 and manual Project flows. Complete TASK-PL-1..3 and §31.16 evidence applicable to Phase A. Do not create a Project, assign a manager/member, generate an executable staffing schedule, expose `project.create.v1`, add external integrations, start monitoring, or claim top-level CAND-023 completion in this slice.

---

## 32. Corrective amendment — CAND-024 Native Conversation Freedom & Graceful Capability Degradation

**Amendment date:** 2026-08-10 (Asia/Saigon)

**Product-owner outcome:** Qaly Assistant must feel like a capable native chat, especially with the configured strong profile preferring DeepSeek V4 Pro. A missing execution skill may prevent a domain mutation, but it must not reduce the whole response to “Qaly chưa có skill”, expose developer implementation instructions to the user, or stop the Assistant from answering, asking useful questions and guiding a safe temporary workflow.

**Planning-only boundary:** this amendment specifies the corrective architecture, contracts, UX, rollout and evidence. It does not claim the behavior is already implemented.

### 32.1 Current-source diagnosis and verdict

Current runtime has three cumulative gates that create the dead-end visible in the Assistant UI:

1. `AiAssistantGoalPlanner.PlanAsync` resolves known missing execution skills before provider routing and returns a controlled plan without asking DeepSeek to help with the user's broader goal.
2. `AiAssistantGoalPlanningOutputContract` makes “no selected skill” become `unsupported_but_analyzed` unless the turn is a blocking clarification case.
3. `ErumiChatService.AssistantPlannedTurnAsync` returns a fixed missing-skill sentence when `SelectedCapabilityId` is empty; it does not invoke the general conversational answer path.

The current behavior is safe for mutations but over-constrains conversation. It incorrectly couples two different questions:

- **Can Qaly help the user think, understand, plan or navigate?** Usually yes.
- **Can Qaly execute this exact domain mutation now?** Only when a registered, authorized and verified capability exists.

**Verdict:** `MISSING_HIGH_VALUE / P0 corrective`. CAND-022A remains valuable for goal understanding and skill discovery, but its shipped no-skill handoff is not product-complete until this corrective increment is green.

### 32.2 Non-negotiable product rule

> **Language capability is broad; action capability is allowlisted. Missing action skill blocks only the action, never the useful conversation around it.**

The Assistant must therefore follow **answer-first, act-when-capable**:

1. Understand the user outcome and authorized context.
2. Give the most useful truthful answer available now.
3. Ask only the smallest set of blocking questions needed for a materially better next step.
4. Offer a canonical temporary/manual workflow when direct execution is unavailable.
5. Draft or execute registered actions only through the existing review/confirm/receipt gates.

“Thoáng” means natural language, progressive clarification and useful partial progress. It does not mean bypassing privacy, permission, confirmation, Rulebook, idempotency or read-back controls.

### 32.3 New gaps

| Gap | Current failure | User impact | Required closure |
|---|---|---|---|
| GAP-092 | Missing execution skill short-circuits the conversational model path. | Broad requests end in a one-sentence refusal even when advice/planning is possible. | Dual-lane answer/action orchestration. |
| GAP-093 | One `disposition` conflates answerability with executability. | UI cannot say “I can guide you, but cannot click this action yet.” | Separate `conversationDisposition` and `actionDisposition`. |
| GAP-094 | Missing-skill `suggestedPath` contains developer language such as schema/renderer/endpoint. | End users receive implementation instructions instead of product guidance. | Internal capability-gap diagnostic plus user-safe workaround. |
| GAP-095 | No bounded progressive clarification policy exists for general chat. | AI either guesses too much or presents a long questionnaire. | Ask 1–3 highest-information blocking questions per turn and provide a provisional answer when possible. |
| GAP-096 | No canonical manual-guidance artifact/deep-link contract exists. | “Cannot execute” becomes a dead end rather than guided use of current Qaly features. | `assistant_manual_guidance.v1` with verified routes, permissions and steps. |
| GAP-097 | Goal analysis and final prose are not explicitly separated. | A safe deterministic skill veto can accidentally suppress all model usefulness. | Deterministic execution classification plus independent conversational synthesis. |
| GAP-098 | Strong-model prompt is optimized for bounded JSON planning rather than helpful mixed conversation/action output. | DeepSeek V4 Pro appears rigid despite model capability. | Versioned answer-first prompt and schema-validated action sidecar. |
| GAP-099 | Missing-skill UI is a primary response state. | Diagnostic metadata dominates the conversation. | Natural answer first; capability limitation is a secondary compact disclosure. |
| GAP-100 | Existing tests assert that no provider is called for known missing-skill requests. | Tests lock in the dead-end behavior. | Replace with “no executor called, conversational provider may still answer” evidence. |
| GAP-101 | No quality metric tracks conversational dead ends or clarification usefulness. | The system can be technically safe while failing the user outcome. | Evaluation set and telemetry thresholds for helpfulness, dead ends and false action claims. |

### 32.4 Candidate definition and CAND relationship

**CAND-024 — Native Conversation Freedom & Graceful Capability Degradation.**

This is a corrective platform increment, not another domain skill. It changes how every current and future CAND behaves when exact execution is unavailable.

| Existing CAND | Relationship to CAND-024 |
|---|---|
| CAND-021 — Unified Assistant Foundation | Reuse singleton workspace, session/history, source disclosure and operational timeline. |
| CAND-022A — Goal Understanding + Skill Discovery | Correct the no-skill handoff; keep goal/scope/risk analysis and deterministic execution boundary. |
| CAND-022B — Read-only Agent Loop | Must consume the new conversation/action split instead of treating “no tool” as a failed loop. |
| CAND-022D — Native Skill Packs | Capability gaps remain discoverable backlog, but are not user-facing refusal templates. |
| CAND-018 — Action Composer | Preserve draft/review/confirm/receipt for mutation; CAND-024 does not weaken it. |
| CAND-023 — Project Launch Orchestrator | Until Project actions ship, users still receive a real launch plan, grouped questions and verified manual guidance rather than only “missing project.create”. |

Score: outcome **25/25** + gap closure **20/20** + AI-native fit **15/15** + reuse **15/15** + testability **10/10** + bounded delivery **13/15** = **98/100**. No full-scope quota veto applies to Phase A below.

### 32.5 Dual-lane turn contract

Introduce `assistant_conversation_turn.v2` as an additive response contract during migration. The server owns its reconciliation.

```json
{
  "schemaId": "assistant_conversation_turn.v2",
  "conversationDisposition": "answered|clarification|guided|degraded|policy_blocked",
  "actionDisposition": "not_requested|available|confirmation_required|unavailable|permission_blocked|policy_blocked",
  "answer": "Natural, useful user-facing response",
  "questions": [
    { "id": "Q1", "text": "...", "blocking": true, "reason": "..." }
  ],
  "guidance": {
    "schemaId": "assistant_manual_guidance.v1",
    "temporary": true,
    "summary": "What the user can do now",
    "steps": [
      { "sequence": 1, "label": "...", "route": "/projects", "requiredPermission": "..." }
    ]
  },
  "capabilityGap": {
    "capabilityId": "project.create.v1",
    "executionUnavailable": true,
    "userMessage": "Mình chưa thể tạo dự án trực tiếp trong chat ở phiên bản này.",
    "internalReasonCode": "capability_not_registered"
  },
  "proposedActions": [],
  "sources": [],
  "confidence": 0.0,
  "actualProvider": "DeepSeek",
  "actualModel": "deepseek-v4-pro"
}
```

Contract rules:

- `answer` is non-empty unless a real safety/policy block prevents even a response or all eligible providers fail and no deterministic guidance exists.
- `questions` contains at most three questions per turn, ordered by information gain. Do not ask for data already available in authorized context.
- A question may coexist with a provisional answer/plan; clarification is progressive, not a blank form.
- `capabilityGap` is secondary metadata. `internalReasonCode` and implementation advice are not rendered as primary user prose.
- `guidance.steps[].route` must come from a server-owned route/action registry; the model cannot invent a URL or permission.
- `proposedActions` remain empty or non-executable when the required capability is unavailable.
- Final provider/model fields record the model that produced the answer, independently of the deterministic router that classified action eligibility.

### 32.6 Required decision matrix

| User intent / runtime state | Conversation lane | Action lane |
|---|---|---|
| Advice, explanation or ideation; no mutation requested | Answer naturally with relevant context and optional sources. | `not_requested`; skill absence is not mentioned. |
| Mutation requested and registered capability is available | Explain the proposed result and any important assumption. | Produce structured draft; require existing confirmation policy. |
| Mutation requested but capability is missing | Answer the broader goal, ask up to three useful questions, produce a non-executable proposal and verified temporary workflow. | `unavailable`; no executor/tool call and no false success. |
| Request is ambiguous | Give a provisional interpretation and ask the smallest clarifying set. | No mutation until resolved. |
| Capability exists but user lacks permission | Explain what can still be done and how access can legitimately be requested. | `permission_blocked`; do not leak inaccessible data. |
| DeepSeek is privacy-ineligible for selected sources | Answer only from eligible/non-sensitive context, request explicit consent when policy supports it, or use an approved provider. | Do not silently send private context to DeepSeek. |
| Provider/model is unavailable | Use an approved strong fallback when `auto`; otherwise return honest degraded/manual guidance. | Never relabel fallback output as DeepSeek. |
| Genuine safety or governance prohibition | Give the permitted explanation and safe alternative. | `policy_blocked`; this is the only intended hard stop. |

### 32.7 DeepSeek V4 Pro conversation profile

Create a server-owned profile `assistant_conversation_strong`:

- Preferred configured model: `DeepSeek/deepseek-v4-pro`.
- `auto` means prefer DeepSeek V4 Pro, then use only another privacy/budget/quality-eligible strong model. It is not strict provider lock.
- Explicit user model lock is strict and must surface availability/privacy errors without silently switching providers.
- Provider eligibility, retention/consent, budget and health are checked before private context is included. CAND-024 does not bypass the current privacy gateway.
- UI before execution says “Ưu tiên DeepSeek V4 Pro”; completed response says the actual provider/model.
- Stream answer tokens or meaningful response sections to the client. Operational progress remains safe summaries, not hidden chain-of-thought.
- Reserve output tokens for the final answer; rank and limit retrieved context rather than stuffing all workspace data into the prompt.
- Track latency, token usage, fallback, schema retry and user-visible failure per turn.

The provider prompt must be versioned as code, for example `assistant_answer_first.v1`, and include these behavioral rules:

1. Help with the user's outcome before discussing internal capability limitations.
2. If direct execution is unavailable, still answer, reason from authorized facts, ask concise questions and provide a usable temporary method.
3. Never tell an end user to “add a schema/renderer/endpoint”; translate it into what Qaly can/cannot do now.
4. Never claim that data changed without a successful registered action receipt and read-back.
5. Separate facts, assumptions and unknowns when they materially affect the recommendation.
6. Ask no more than three blocking questions in one turn; prefer one coherent group.
7. Use structured JSON only for the contract/action sidecar. The visible answer must remain natural rather than sounding like an internal state machine.

### 32.8 Server orchestration changes

Required control flow:

```text
authorize context
  -> understand goal and classify action eligibility
  -> synthesize useful conversational response
  -> if needed, ask progressive clarification
  -> if capability is available, build validated draft
  -> preview / explicit confirm / execute / read-back
  -> otherwise attach verified manual guidance and capability-gap telemetry
```

Concrete source dispositions:

- `AiAssistantGoalPlanner`: known missing-skill detection may classify action eligibility deterministically, but must no longer be treated as a completed user response. The conversational lane still runs when policy permits.
- `AiAssistantGoalPlanningOutputContract`: preserve `missingSkills` for diagnostics while adding separate conversation/action dispositions. “No selected skill” is not automatically “unsupported conversation”.
- `ErumiChatService.AssistantPlannedTurnAsync`: when no executor is selected, invoke an advisory-only conversational synthesis with authorized context; attach limitations/guidance afterward. On provider failure, return deterministic user guidance rather than developer instructions.
- Assistant DTOs/controller: add v2 fields additively; preserve v1 compatibility until frontend and clients migrate.
- Route registry: provide server-verified temporary steps/deep links for Project, Task, Team, Meeting, Group, Calendar, Wiki and settings surfaces. Unknown routes are omitted rather than generated.
- Telemetry: record missing capability and action denial internally without turning it into the main answer.

No arbitrary shell, database query, hidden browser action or unregistered tool is introduced by this candidate.

### 32.9 Native chat UX

The message body is always the primary surface. Supporting controls appear only when relevant:

- `Mình có thể làm ngay`: answer, analysis, draft or supported read.
- `Mình cần bạn xác nhận`: mutation preview using existing action contracts.
- `Mình cần biết thêm`: one group of up to three contextual questions with quick-reply/free-text support.
- `Cách làm tạm thời`: verified Qaly steps and deep links, clearly labeled as manual/temporary.
- `Chưa thể tự thực hiện`: compact execution limitation; expandable technical diagnostic only for authorized admin/developer roles.

The five operational progress rows may remain in the timeline, but the completed chat must not look like a pipeline log followed by a refusal. Missing skill title, reason code and suggested implementation path must not dominate the final message.

### 32.10 Phased execution plan

| Phase | Deliverable | Boundary | Priority |
|---|---|---|---|
| CAND-024A — Answer-first fallback | Dual-lane server behavior for no-selected-skill turns; DeepSeek advisory response; user-safe limitation; tests. | No new domain mutation, no arbitrary tool, no route generation. | **IMMEDIATE CORRECTIVE PRIMARY** |
| CAND-024B — Progressive interaction | v2 response fields, 1–3 question UI, quick replies, provisional answer and verified manual guidance registry. | Guidance/read-only only. | After A |
| CAND-024C — Streaming + quality evaluation | Streamed visible response, versioned prompt evaluation set, dead-end/latency/fallback telemetry and quality gate. | No mutation breadth increase. | After B; may share transport work with CAND-022B |

### 32.11 PRIMARY implementation breakdown — CAND-024A only

Maximum three primary tasks:

| Task | Deliverable | Required evidence |
|---|---|---|
| TASK-CF-1 | Split execution eligibility from conversation response; missing skill still forbids executor but permits advisory synthesis. | Unit tests for goal planner/output contract and no-executor invariant. |
| TASK-CF-2 | Replace selected-capability-null canned refusal with DeepSeek-preferred answer-first response plus user-safe capability limitation and deterministic provider-failure guidance. | Integration tests with actual provider/model truth and zero mutation. |
| TASK-CF-3 | Update Assistant rendering and E2E fixtures so answer is primary and missing-skill metadata is secondary. | E2E for missing Project action, broad CAND test request, normal advice and permission block. |

CAND-024B fields may be added as an additive skeleton only when required for A compatibility. Do not bundle a new domain action, CAND-022B multi-step loop, CAND-023 Project mutation, arbitrary tool execution or external integration into this Primary.

### 32.12 Acceptance examples

| Prompt | Required behavior |
|---|---|
| “Khởi chạy dự án web SPA cho khách hàng trong 8 tuần.” | Give a provisional launch approach, identify known facts/assumptions, ask at most three high-value questions, and explain current direct-execution status. Until CAND-023C, offer verified Project/Task manual steps; never stop at `project.create.v1` missing. |
| “Chạy tự động để test các CAND đã implement.” | Explain what can be verified, what access/runtime is needed and a safe temporary verification method. Do not run shell/tests without a registered adapter; do not show “add schema/endpoint” as the answer. |
| “Tôi nên chia team thế nào?” | Answer from authorized skill/capacity evidence when present, clearly flag missing data and ask only the most material questions. No assignment mutation. |
| “Tạo 5 task này.” with registered capability | Use CAND-018 draft/review/confirm/receipt. Conversation freedom must not bypass the action gate. |
| “Tạo project” without permission | Explain permitted preparation/manual route and legitimate access request; do not reveal projects or members outside scope. |
| DeepSeek timeout under `auto` | Use only an eligible strong fallback and show its real identity, or return useful deterministic guidance. No fake DeepSeek success. |

### 32.13 Verification and release gates

Required evidence:

- Unit: known missing skill does not select or call an executor; it also does not suppress eligible conversational synthesis.
- Unit: internal `SuggestedPath`/reason codes are not rendered as end-user primary text.
- Unit: no selected skill produces separate action-unavailable state rather than an unsupported whole-turn state.
- Integration: `project.create` missing returns a substantive answer and zero database mutations.
- Integration: broad CAND test request may call the conversational provider but never test/shell executor; actual answer provider/model is truthful.
- Integration: privacy-ineligible DeepSeek receives zero protected source content.
- Integration: provider failure returns user-safe deterministic guidance; no HTTP 500 and no false completion.
- E2E: answer is visually primary; capability limitation is secondary; temporary guidance links resolve to registered routes.
- E2E: progressive questions contain 1–3 items, accept reply and retain session context.
- Regression: CAND-018 confirmation/idempotency/read-back remains green; CAND-021 singleton history/session remains green.

Zero-tolerance gates:

- False mutation-success claim: **0**.
- Unregistered executor/tool call: **0**.
- Private source sent to privacy-ineligible provider: **0**.
- Internal developer remediation shown as the sole user answer: **0**.
- Known missing-skill prompt ending only in a capability refusal when useful guidance is possible: **0** in the locked evaluation set.
- Provider/model mislabel: **0**.

Quality release thresholds for the locked Vietnamese evaluation set:

- Useful answer or useful progressive question on non-policy-blocked turns: **≥ 95%**.
- Clarification groups with more than three questions: **0%**.
- Unsupported dead-end rate: **≤ 2%**, with every remaining case reviewed.
- Manual guidance route validity: **100%**.
- Schema-valid action sidecar after bounded retry: **≥ 99%**; otherwise action lane degrades safely while conversation remains available.

### 32.14 Coverage and priority update

- Top-level catalog becomes **24 CAND IDs**: 14 closed, 9 active work and 1 superseded (`CAND-011`).
- GAP-092..101 are owned by CAND-024A–C; unowned new gap = 0.
- CAND-022A implementation credit is retained for goal analysis/skill discovery, but its user-facing no-skill path is explicitly under corrective work.
- CAND-024 is not a promise that every domain action exists. It is the platform guarantee that absence of an action never makes the Assistant needlessly useless.

**Coverage gate: PASS. Product completeness: NOT PASS.** The desired conversational behavior is now decision-complete and testable, but CAND-024A–C are not implemented by this amendment.

### 32.15 Superseding NEXT_IMPLEMENTATION_GOAL

> Implement only `CAND-024A — Answer-first fallback` before CAND-022B. Preserve deterministic capability discovery and all CAND-018 mutation controls, but decouple conversational usefulness from executor availability. A known missing skill may block the executor; it must not finish the user turn. For a no-selected-skill, non-policy-prohibited request, invoke an advisory-only response through the server-owned `assistant_conversation_strong` profile preferring configured DeepSeek V4 Pro, render a substantive answer or useful clarification, attach a concise user-safe execution limitation, record actual provider/model, and guarantee zero domain mutation. Replace tests that require “provider never called” with tests requiring “executor never called; conversational provider may be called safely.” Complete TASK-CF-1..3 and the Phase A evidence in §32.13. Do not add a new domain action, arbitrary tool/shell access, CAND-022B multi-step execution, CAND-023 Project mutation, external integration or privacy bypass. After CAND-024A is green, resume CAND-022B; after CAND-022B is green, resume CAND-023A.

## 33. Implementation checkpoint — CAND-022B + CAND-024B + CAND-023A

**Checkpoint date:** 2026-08-10 (Asia/Saigon)

**Implementation boundary:** this checkpoint closes three bounded increments in the required order on top of the existing CAND-024A answer-first baseline. It does not claim CAND-022C/D, CAND-023B/C/D, CAND-024C, arbitrary tool execution or full top-level CAND-022/023/024 completion.

### 33.1 CAND-022B — bounded read-only agent loop

Delivered runtime behavior:

- The durable Assistant turn now executes a server-owned dependency-ordered read-only loop with four canonical stages — `retrieve`, `analyze`, `verify`, `present` — inside the contract limit of eight steps.
- Only registered descriptors with `read_only` or `read_only_proposal` risk are eligible. Unknown skills, write/mutation descriptors, unapproved sources, privacy/trust violations and invalid dependency state stop before adapter execution.
- Tenant/entity authorization, source eligibility, privacy/trust and budget/provider eligibility are rechecked at the relevant step boundary. Model output is treated as untrusted until schema, source references and mutation/confirmation invariants pass verification.
- Attempts are bounded at two per step. Queued/running/completed/verified/skipped/blocked/failed/canceled public states, safe error codes, attempt number, source receipts and actual provider/model are persisted without chain-of-thought.
- `POST /api/ai/assistant/turns/{turnId}/cancel` requests cancellation between safe boundaries. `POST /api/ai/assistant/turns/{turnId}/resume` creates a linked durable turn from the stored request snapshot; reload restores both history and operational receipts.
- The loop exposes no shell, SQL, repository scanner, production test runner or write tool.

**Disposition:** `CAND-022B = NATIVE_COMPLETE` for this bounded registered read/artifact contract. Top-level CAND-022 remains partial because CAND-022C/D are outside this increment.

### 33.2 CAND-024B — progressive interaction v2

Delivered runtime behavior:

- `assistant_conversation_turn.v2` is additive to the existing turn response and separates useful answer text, conversation disposition, action disposition, progressive questions, manual guidance, capability gap, sources, confidence and actual provider/model.
- The server emits no more than three highest-value questions in one turn. A provisional answer remains primary; questions support registered quick replies and free text rather than forcing a blank-form stop.
- Quick replies are posted back with stable `questionId`, value and optional label. Project-launch follow-ups preserve the capability/session context, generate a new durable brief revision and omit already answered questions.
- `assistant_manual_guidance.v1` is generated only from the server-owned route registry for `/projects`, `/tasks`, `/teams`, `/meetings`, `/groups`, `/calendar`, `/wiki` and `/settings`; the client repeats the allowlist before navigation. The model cannot invent routes.
- The singleton Assistant renders answer-first text, progressive questions, quick replies/free-text, verified temporary guidance and a secondary capability-gap disclosure. Completed responses show actual provider/model rather than the preferred model label.

**Disposition:** `CAND-024B = NATIVE_COMPLETE` for the additive progressive conversation and verified manual-guidance contract. CAND-024A remains the retained answer-first baseline; top-level CAND-024 remains partial because CAND-024C streaming/evaluation is not implemented.

### 33.3 CAND-023A — Organization Rulebook + Project Launch Brief

Delivered runtime behavior:

- A server-owned versioned Organization Work Rulebook supports durable draft versions, manager-authorized activation, one effective version at a time and superseded history. Effective-rule read-back is exposed through organization-scoped APIs.
- `project.launch.analyze.v1` is registered as `artifact` / `read_only_proposal` with strict input/output schema, renderer and feature flag. It consumes authorized `organization.summary` and `organization.rulebook` source packs through the existing context registry and CAND-022B executor.
- `project_launch_brief.v1` persists revision history, facts, assumptions, unknowns, source versions, Rulebook status/version, deterministic per-rule decision receipts, actual provider/model and prompt version. Missing policy or facts remain `policy_missing`/`unknown`; they are not guessed into a pass.
- The native review card renders scope, success measures, assumptions, unknowns, Rulebook decisions, provider/model, prompt version and revision. It explicitly states that no Project is created and no member is assigned.
- Integration evidence asserts zero `Project` count change across initial brief and progressive revision. No manager/team selection, Sprint/Task generation, Project mutation or staffing schedule is exposed in this phase.
- Persistence is covered by migration `20260809190001_P010AiNativeReadLoopConversationLaunchBrief` for Assistant turn resume metadata, Organization Rulebook versions, Project Launch Brief revisions and rule-decision receipts.

**Disposition:** `CAND-023A = NATIVE_COMPLETE` for Rulebook + artifact-only launch brief. Top-level CAND-023 remains **PARTIAL / NOT COMPLETE**; CAND-023B/C/D are still required for staffing/delivery scenarios, confirmed execution and monitoring.

### 33.4 Feature flags and rollback

The three increments are independently guarded by:

- `AiJobsV4:AssistantReadOnlyLoopEnabled`
- `AiJobsV4:AssistantProgressiveInteractionEnabled`
- `AiJobsV4:ProjectLaunchBriefEnabled`

They are enabled in Development, disabled by default/Production example, and support rollback without removing durable records. Existing manual Project flows, CAND-018 mutation confirmation and CAND-021 session/history remain unchanged.

### 33.5 Verification evidence

| Gate | Result |
|---|---|
| Frontend typecheck | `npm run typecheck` — PASS. |
| Frontend production build | `npm run build` — PASS; 3,975 modules transformed. |
| Full unit suite | `Qaly.UnitTests` — 466 passed, 0 failed, 0 skipped. |
| Full integration suite | `Qaly.IntegrationTests` — 147 passed, 0 failed, 0 skipped. |
| Assistant API focused regression | `AiAssistantTurnApiTests` — 15 passed, including bounded loop, provider/schema handling, cancel/reload/resume and launch revision with zero Project mutation. |
| Full web-feature suite | `Qaly.WebFeatureTests` — 38 passed, 0 failed, 0 skipped. |
| Chromium AI regression | Assistant workspace, goal planner, research plan, action composer and new loop/launch/resume coverage — 9 passed. |
| Chromium console/navigation smoke | Public auth, authenticated main routes and Group detail — 3 passed. |
| Preview | Development in-memory host serves `http://127.0.0.1:5000`; authenticated routes redirect through the normal login flow and the browser suites run against this host. |

Provider evidence in unit/integration/E2E uses controlled gateway fixtures so it can prove routing, schema reconciliation and provider/model truth without making a billable external request. Runtime still requires a valid, privacy/budget-eligible DeepSeek configuration to produce live `DeepSeek / deepseek-v4-pro`; failure or fallback must retain the actual identity and must not be presented as DeepSeek success.

### 33.6 Remaining boundary and superseding NEXT_IMPLEMENTATION_GOAL

The next implementation goal is `CAND-023B — Deterministic staffing and delivery scenarios`. It must consume the effective CAND-023A Rulebook plus the existing CAND-015/016/017 skill/capacity facts to produce reviewable manager/team/capacity and scope→milestone→Sprint→Task/dependency scenarios. CAND-023B remains artifact-only: no Project/member/Sprint/Task mutation, no “fill every free slot” heuristic, no unavailable/private fact inference and no top-level CAND-023 completion claim.

> Implement only `CAND-023B — Deterministic staffing and delivery scenarios` on top of the completed CAND-022B/CAND-023A/CAND-024B contracts. Reuse the effective versioned Organization Rulebook, authorized member skill evidence, declared availability and cross-Project load to build deterministic, explainable manager/team scenarios with eligibility, skill coverage, continuity, focus cost, fairness, concurrent-Project limits and schedule-conflict decisions. Add a strict durable `project_launch_plan.v1` that maps approved scope to milestones, Sprints, Tasks, dependencies, estimates, risks, capacity windows, alternatives and source/version receipts; keep facts, assumptions and blocking unknowns separate. Render side-by-side reviewable scenarios and progressive questions in the singleton Assistant. Persist revisions and decision receipts, record actual provider/model only for model-authored content, and verify reload/idempotency/privacy/fairness/stale-source behavior. Do not create a Project, assign a manager/member, create Sprints/Tasks, expose `project.create.v1`, call external calendars, bypass confirmation, implement CAND-023C/D or claim full CAND-023 completion.

## 34. Implementation checkpoint — CAND-023B + CAND-023C + CAND-023D

**Checkpoint date:** 2026-08-10 (Asia/Saigon)

**Implementation boundary:** this checkpoint completes the governed internal Project-launch loop after CAND-023A. It covers deterministic staffing/delivery planning, explicit-confirmation creation of a real canonical Project graph, and baseline monitoring with review-only replan proposals. It does not add arbitrary tools or silently perform external repository, webhook, calendar, invitation or deployment effects.

### 34.1 CAND-023B — deterministic staffing and delivery plan

Delivered runtime behavior:

- `project.staffing.plan.v1` is registered only for actors who can manage an authorized Organization. Capability discovery now derives this permission from the Organization even when it has no Project yet; Project-only permissions are not incorrectly required for the first launch.
- DeepSeek or another eligible gateway model may propose only the strict bounded delivery decomposition. The server treats that output as untrusted, rejects unknown fields, limits the plan to eight Sprints and eighty Tasks, validates IDs/references, and rejects cyclic dependencies.
- Manager/team eligibility, capacity, availability, cross-Project commitments, concurrent-Project limits, focus reserve, fairness and Rulebook decisions are recomputed deterministically from canonical Qaly data. The model cannot turn a rejected candidate into an eligible member.
- Missing capacity is a visible hard reject rather than an inferred free slot. Skill evidence is based on confirmed Qaly evidence; unknown skills remain explicit gaps. Private cross-Project details are reduced to authorized aggregate load facts.
- `project_launch_plan.v1` is durable and revisioned with staffing alternatives, hard rejects, capacity windows, delivery Sprints/Tasks/dependencies, risks, assumptions, source hash, Rulebook version, provider/model and prompt version. Generation is idempotent for the same durable Assistant turn.
- The singleton Assistant renders reviewable staffing scenarios and the Project/Sprint/Task tree, including dependencies, gaps and deferred external work. No Project mutation occurs during this phase.

**Disposition:** `CAND-023B = NATIVE_COMPLETE` for deterministic staffing and reviewable delivery artifacts.

### 34.2 CAND-023C — confirmed canonical Project launch

Delivered runtime behavior:

- `project.launch.execute.v1` is exposed as an explicit batch-confirm mutation. Chat text alone cannot confirm the launch; the user selects a feasible scenario and confirms the exact reviewed plan through the native card.
- Confirmation rechecks authenticated Organization-management permission, feature state, plan state/revision, selected scenario, effective Rulebook, active membership and the complete staffing/capacity/source hash. Any stale Rulebook, member or capacity fact returns a conflict before domain mutation.
- An idempotency key owns one execution. The internal command graph runs in one database transaction and creates the canonical `Project`, `ProjectMember`/role assignments, existing canonical `Sprint`, selected `TaskItem` records, Task skill requirements and an acyclic dependency graph.
- Post-commit read-back verifies the created entity counts and IDs before success is reported. `project_launch_execution_receipt.v1` persists command status, deep links, rule/source versions, audit references, actual provider/model, verification time, revision and rollback availability.
- Repository, webhook, calendar, invitation and deployment operations remain `external_deferred`; the receipt never labels them successful. No legacy `AiTools`, shell or unrestricted executor is used as a fallback.
- Impact-checked rollback is available only before the Project accumulates user work. It soft-deletes the internal launch graph while retaining the execution receipt and audit trail; stale or unsafe rollback fails closed.

**Disposition:** `CAND-023C = NATIVE_COMPLETE` for the reviewed canonical internal launch batch and truthful external deferral.

### 34.3 CAND-023D — monitored baseline and governed replan

Delivered runtime behavior:

- `project.operation.monitor.v1` can be invoked from the confirmed receipt and by a bounded five-minute worker schedule; evaluation itself records the next six-hour check and is a no-op when its feature flag is disabled.
- The deterministic monitor compares the confirmed baseline with current Project, Task, dependency, staffing, capacity and Rulebook facts. It detects missing/deleted Project state, Rulebook change, scope drift, staffing drift, overdue/unassigned work, progress lag and effort overrun.
- Material drift creates a durable `project_replan_proposal.v1` in `pending_review` state with trigger codes, baseline/current hashes, source references and an explicit confirmation requirement.
- Monitoring never changes assignee, date, scope, role, deadline, Project or Task state. Repeated checks for the same current hash do not create duplicate proposals.
- Reload restores the plan, execution receipt and latest replan proposal in the same Assistant artifact rail.

**Disposition:** `CAND-023D = NATIVE_COMPLETE` for bounded monitoring and review-only replan generation.

### 34.4 Persistence, flags and API surface

Migration `20260809201444_P011ProjectLaunchOrchestration` adds durable Project launch plan artifacts, execution receipts and replan proposals with Organization/Project/Assistant relationships and required indexes.

Independent flags are enabled in Development and disabled by default/Production example:

- `AiJobsV4:ProjectLaunchPlanningEnabled`
- `AiJobsV4:ProjectLaunchExecutionEnabled`
- `AiJobsV4:ProjectOperationMonitoringEnabled`

The authorized API surface provides plan read-back, explicit confirm, impact-checked rollback and manual monitor commands. Mutation endpoints require CSRF protection; confirm/rollback additionally require idempotency keys and row revisions.

### 34.5 Verification evidence

| Gate | Result |
|---|---|
| Full solution build | `dotnet build Qaly_project.slnx --no-restore` — PASS, 0 warnings, 0 errors. |
| Frontend typecheck | `npm run typecheck` — PASS. |
| Frontend production build | `npm run build` — PASS; 3,975 modules transformed. |
| Full unit suite | `Qaly.UnitTests` — 471 passed, 0 failed, 0 skipped. |
| Full integration suite | `Qaly.IntegrationTests` — 149 passed, 0 failed, 0 skipped. |
| Full web-feature suite | `Qaly.WebFeatureTests` — 38 passed, 0 failed, 0 skipped. |
| Composite domain evidence | Launch Brief → staffing plan with zero Project mutation → explicit/idempotent confirm → real Project/member/Sprint/Task/skill/dependency read-back → controlled overdue drift → replan proposal with zero silent Task change → audited rollback. |
| Stale-source evidence | Capacity changes after plan generation return HTTP conflict and create neither Project nor execution receipt. |
| Chromium AI + console gate | 9 passed: goal planner, bounded loop/reload/resume, existing native candidates, CAND-023B/C/D card/confirm/receipt/replan and public/authenticated console routes. |
| Preview | Development in-memory host health is 200 at `http://127.0.0.1:5000`. |

The browser orchestration test uses controlled API fixtures to verify the UI contract deterministically. The integration test executes the real server orchestration and canonical EF domain writes against an isolated in-memory database. Live DeepSeek use remains subject to configured provider availability plus privacy, budget and retention eligibility; actual provider/model identity is always retained.

### 34.6 Coverage disposition and remaining platform work

The primary governed internal Project-launch loop is now native end to end: objective/brief → staffing/delivery plan → explicit confirmation → real Project graph → receipt/navigation → monitor/replan. Qaly no longer answers this flow with “missing project.create”; it creates the Project only after deterministic business checks and user confirmation.

`CAND-023B`, `CAND-023C` and `CAND-023D` are complete within their declared boundaries. Top-level `CAND-023` is **INTERNAL-CORE COMPLETE / overall PARTIAL** until provider-specific external operation adapters and their outbox/compensation evidence are implemented; external effects remain honestly deferred rather than simulated.

This does not make the whole Qaly AI-native program complete. The next broad coverage work is `CAND-022D — Native Skill Packs` for the remaining main Qaly flows, followed by `CAND-024C — Streaming + quality evaluation`; `CAND-022C` remains a separate Development/Test-only safe demo/test orchestrator and must not broaden production execution authority.

## 35. Corrective implementation checkpoint — Native conversation to governed launch without dead ends

**Checkpoint date:** 2026-08-11 (Asia/Saigon)

The live Assistant exposed four cross-layer defects after §34: a create-Project sentence containing staffing details could route directly to staffing before a Brief existed; explicit continuation still depended on a second goal-planner model ranking; the final clarification answer hid its own submit control; and a delayed draft-save response could overwrite a newer local answer. A complete natural-language request also asked for scope again even when the message already supplied must-haves.

The corrective implementation now:

- routes registered action intents through the authenticated server capability registry without a redundant planner-provider round trip; open-ended requests still use the strong conversational model;
- gives `project.launch.analyze.v1` precedence for “create/launch Project + staffing” composite goals, then automatically chains to governed staffing/delivery planning when the Brief has no blocking product unknown and an effective Rulebook is available;
- recognizes deadline, audience and explicit must-have scope from the current utterance or durable prior replies instead of asking again;
- stores up to three progressive replies as one durable draft, preserves newer local edits while earlier saves are in flight, keeps the final submit control visible and clears the server draft only after a successful turn;
- presents the result first; process, authorized context and AI work-plan details remain collapsed by default;
- retains one explicit batch confirmation before canonical Project/member/Sprint/Task/dependency mutation, followed by read-back receipt and monitor/replan controls.

Corrective evidence: solution build PASS with zero warnings/errors; targeted unit 20/20 PASS; real orchestration integration 5/5 PASS including auto-plan and canonical execution; Chromium clarification/resume/confirm/monitor 3/3 PASS; frontend typecheck and production build PASS.

## 36. Unified durable conversation and execution checkpoint

**Checkpoint date:** 2026-08-11 (Asia/Saigon)

This checkpoint removes the remaining split between the Analytics chat and the native Assistant. Both surfaces now use the same durable Assistant-turn API, capability registry, server-owned session history and governed Project-launch executor. Short follow-ups such as "thử luôn" inherit only the relevant recent launch intent; an unrelated new request is not made sticky by old Project messages.

Delivered corrections:

- session history is loaded before capability discovery and goal planning, scoped by authenticated user and optional Project, restored after reload, and available from the native history drawer;
- progressive questions keep their inputs mounted while being answered, debounce-save all replies as one server draft, survive reload, and submit as one batch without the first-character disappearance/race;
- both Analytics and native chat can continue through Launch Brief, Rulebook, staffing/delivery planning and the single explicit confirmation into the same canonical Project graph;
- results render first while process, checked context, work plan and temporary manual guidance are collapsed by default;
- useful deterministic server fallback remains available when a provider/planner fails, while receipts retain the actual provider/model identity;
- manual navigation guidance is not presented as the primary outcome when the governed native launch capability is available.

Verification evidence for this checkpoint:

| Gate | Result |
|---|---|
| Full solution build | PASS, 0 warnings, 0 errors. |
| Frontend typecheck and production build | PASS; 3,975 modules transformed. |
| Full unit suite | 482 passed, 0 failed. |
| Full integration suite | 155 passed, 0 failed. |
| Assistant-turn integration suite | 22 passed, including durable short-follow-up context. |
| Chromium focused E2E | 3 passed: multi-answer/reload/history, session resume, canonical confirm/receipt/monitor/replan. |
| Canonical persistence proof | 1 ProjectMember, 1 Sprint, 2 TaskItems and 1 TaskDependency persisted; transaction receipt was committed, read back and verified idempotent. |
| Preview | HTTP 200 at `http://127.0.0.1:5000`. |

**Disposition:** the governed internal Project-launch core and its durable conversation path are complete. Provider-specific calendar, repository, invitation, webhook and deployment adapters remain explicitly `external_deferred`; they are not simulated or reported as successful.

### 36.1 Reusable implementation goal prompt

```text
Đặt một goal duy nhất và triển khai đến khi có evidence end-to-end: hoàn thiện Qaly AI Native thành trợ lý tự động hóa dùng dữ liệu thật, hội thoại tự nhiên và không dead-end. Với yêu cầu khởi chạy dự án, AI phải hiểu câu tự nhiên, chỉ hỏi tối đa ba unknown thực sự còn thiếu và lưu được nhiều câu trả lời qua reload; không hỏi lại dữ kiện người dùng đã nêu. Khi đủ dữ kiện, tự tạo Launch Brief, đối chiếu Organization Rulebook, kỹ năng/evidence, capacity, availability, lịch và tải đa dự án; lập các phương án manager/team có giải thích, phase/Sprint/Task/dependency/estimate/assignee; sau đúng một xác nhận rõ ràng phải tạo Project graph canonical thật, read-back và trả receipt/deep-link. Không được coi chỗ trống là capacity, không bịa skill/lịch, không báo thành công khi chưa đọc lại dữ liệu, không để provider/planner failure trở thành ngõ cụt; dùng fallback server có ích và ghi actual provider/model. Câu trả lời hiển thị kết quả trước, process/context/work-plan collapse mặc định. Rà cả business logic, navigation, history, model selection, multi-answer race, idempotency, stale-source, rollback và monitor/replan. Chỉ kết luận hoàn thành khi build/typecheck/unit/integration/Chromium PASS và integration chứng minh dữ liệu canonical đã persisted; phân biệt rõ internal-core complete với external adapters còn deferred.
```
## 37. Corrective runtime audit — session, authorization and mutation integrity

**Checkpoint date:** 2026-08-13 (Asia/Saigon)

**Authority:** this section is the canonical current disposition when it conflicts with historical completion statements in §33–36. `NATIVE_COMPLETE` from an older checkpoint is not automatically promoted to `NATIVE_COMPLETE_VERIFIED`; current source, API/database and Chromium evidence are required.

### 37.1 Reopened runtime gaps and ownership

| Gap | Runtime contradiction | Owner | Current disposition |
|---|---|---|---|
| GAP-102 | Task review could call confirm without a canonical `rowVersion` or stable logical idempotency key and showed a combined, misleading error. | CAND-018 | Fixed: pre-confirm GET/rehydration, separate validation, stable retry key, stale review stop and lost-response reconciliation. |
| GAP-103 | An explicit “10 task” request could degrade to a three-row fallback. | CAND-018 | Fixed: requested count is parsed, bounded to 20, enforced by output reconciliation and proven with exactly ten canonical Tasks. |
| GAP-104 | §36 claimed durable history while the visible runtime did not prove a real session browser. | CAND-019 + CAND-021B | Reverified: server list/get/create/rename/archive/delete, transcript/artifact restoration and reload/switch Chromium evidence. |
| GAP-105 | New system-role/user override/custom Project-role data was not a shared Assistant authorization boundary. | CAND-021C + CAND-018 + CAND-023C | Fixed through `IAiNativeAuthorizationService`; discovery and mutation recheck the same server-owned decision. |
| GAP-106 | A model-authored Task option could select a member from workload-only evidence, which is not capacity/availability/skill proof. | CAND-006/017 + CAND-018 | Fixed boundary: model-authored Action Composer tasks are unassigned; only explicit reviewer selection is accepted, and active Project membership is rechecked immediately before commit. Automated staffing remains CAND-017/CAND-023 policy work. |
| GAP-107 | EF model contained RBAC/custom-role entities without a matching latest migration, preventing a clean relational startup. | Platform prerequisite for all AI CAND | Fixed by P015; clean SQL Server migration and relational Task-confirm scenario pass. |
| GAP-108 | Playwright launch-profile precedence could silently switch tests from isolated in-memory data to the developer SQL instance. | Release evidence | Fixed: test web server explicitly sets Development and selects in-memory unless `E2E_USE_SQL` is intentionally enabled. |

No CAND-025 is added: these gaps belong to the existing CAND-018/019/021/023 contracts. Adding another session or mutation candidate would duplicate ownership.

### 37.2 Canonical CAND-001…024 inventory

Status vocabulary in this table is the vocabulary mandated by the corrective audit. “Source” means the currently checked-out implementation; “runtime” means evidence rerun on 2026-08-13.

| CAND | Business outcome / architectural role | Action class | Plan/source disposition | Current runtime disposition | Gap / next priority |
|---|---|---|---|---|---|
| CAND-001 — Project Progress Summary | Grounded Project progress card; first canonical read artifact. | read/analysis | Source-complete historical capability. | `PRESENT_PARTIAL` | No fresh dedicated Chromium rerun in this checkpoint; retain, do not claim newly verified. |
| CAND-002 — Sprint Progress Summary | Grounded Sprint progress and risk summary. | read/analysis | Source-complete historical capability. | `PRESENT_PARTIAL` | Same release-evidence gap as CAND-001. |
| CAND-003 — Acceptance Checklist | Persisted structured acceptance checklist draft. | mutation draft/confirmed mutation | Domain/storage veto remains. | `MISSING_HIGH_VALUE` | Add canonical checklist model/migration before implementation. |
| CAND-004 — Task Breakdown | Ordered parent/child task decomposition. | mutation draft/confirmed mutation | Parent/subtask domain veto remains. | `MISSING_HIGH_VALUE` | Define progress/dependency semantics and migration first. |
| CAND-005 — AI Usage/Budget | Admin cost, warning and hard-stop controls. | admin AI control | Source-complete historical platform surface. | `PRESENT_PARTIAL` | Fresh role/concurrency Chromium evidence still required. |
| CAND-006 — Assignee Recommendation | Explainable Project-local skill/evidence/workload recommendation. | analysis/artifact proposal | Source-complete historical bounded recommendation. | `PRESENT_PARTIAL` | Keep separate from assignment mutation; rerun fairness/private-source matrix. |
| CAND-007 — Group Summary | Exact selected-message grounded summary. | read/analysis artifact | Source-complete historical capability. | `PRESENT_PARTIAL` | Dedicated current Chromium evidence not rerun. |
| CAND-008 — Group → Task Draft | Source-linked selected-message Task draft. | mutation draft/confirmed mutation | Source-complete bounded flow. | `PRESENT_PARTIAL` | Reverify Group source-open and Task read-back on current shell. |
| CAND-009 — Dashboard Strategic Brief | Server-snapshot executive brief. | read/analysis artifact | Source-complete historical capability. | `PRESENT_PARTIAL` | Dedicated current Chromium evidence not rerun. |
| CAND-010 — Meeting Checknote | Privacy-gated transcript checknote. | read/artifact proposal | Source-complete bounded flow. | `PRESENT_PARTIAL` | Selective Task creation remains another capability. |
| CAND-011 — Project Planner | Historical Project draft concept. | proposal | Subsumed by CAND-023. | `SUPERSEDED` | No separate implementation or coverage credit. |
| CAND-012 — Wiki Brief / Task Draft | Grounded section-aware Wiki summary/draft. | read/artifact/mutation draft | Wiki CRUD exists; AI adapter remains deferred. | `MISSING_HIGH_VALUE` | Implement as a CAND-022D skill pack with exact section sources. |
| CAND-013 — Weekly Digest | Persisted scheduled project digest. | read/external effect | Scheduler/preference/email contract absent. | `MISSING_HIGH_VALUE` | Do not present a localStorage toggle as delivery. |
| CAND-014 — Task Hub AI Launcher | Shared Task hub entry for checklist/breakdown. | launcher/renderer | Blocked by CAND-003/004. | `MISSING_HIGH_VALUE` | Implement only after both domain capabilities exist. |
| CAND-015 — Task Skill Taxonomy | Canonical required-skill identities and reviewed AI tags. | artifact proposal/confirmed mutation | Source-complete historical capability. | `PRESENT_PARTIAL` | Fresh full role/browser release matrix not rerun. |
| CAND-016 — Member Skill Evidence | Evidence-backed member skill profile. | read/analysis | Source-complete historical capability. | `PRESENT_PARTIAL` | Retain unknown≠unskilled invariant; rerun privacy/fairness UI evidence. |
| CAND-017 — Portfolio Assignment/Schedule Copilot | Capacity, availability, multi-Project load and schedule proposal. | analysis/artifact proposal/confirmed mutation | Source-complete bounded internal contract; external calendars deferred. | `PRESENT_PARTIAL` | Current browser matrix not rerun; external Google/Outlook adapters stay deferred. |
| CAND-018 — Action Composer | Natural intent → editable Task plan → one confirm → atomic mutation/receipt. | mutation draft/confirmed mutation | Regression fixed in this checkpoint. | `NATIVE_COMPLETE_VERIFIED` for `task.create.v1` | Other domain tools require independent adapters. |
| CAND-019 — Unified Assistant Workspace | One chat-first shell and routing boundary. | orchestration surface | Shared `ErumiChatPanel` and durable APIs. | `NATIVE_COMPLETE_VERIFIED` for registered capabilities | Keep legacy executors isolated. |
| CAND-020 — Native Group Poll Draft | One editable, confirmed Group poll. | mutation draft/confirmed mutation | Contract remains backlog. | `MISSING_HIGH_VALUE` | Next bounded Group mutation; no fake multi-question form/quiz. |
| CAND-021A–D — Assistant Foundation | Workspace, durable sessions, context/capability registry, grounded research. | platform read/artifact | Source-complete; authorization corrected. | `NATIVE_COMPLETE_VERIFIED` for bounded foundation | Repository/GitHub read adapters are separate skill packs. |
| CAND-022A–D — Goal/Agent/Skill Platform | Goal discovery, bounded loop, safe test runner, skill packs. | orchestration/read/dev-test | A/B source-complete; C implemented and Dev/Test-gated; D incremental. | `PRESENT_PARTIAL` top-level | Add Chromium for 022C and finish 022D coverage of remaining main flows. |
| CAND-023A–D — Governed Project Lifecycle | Brief → staffing/delivery → canonical Project graph → monitor/replan. | artifact + confirmed mutation | Internal core implemented; external effects honest. | `EXTERNAL_DEFERRED` overall; internal core reverified | Calendar/repository/invite/webhook/deploy need provider-specific outbox/compensation. |
| CAND-024A–C — Open Conversation | Answer-first, progressive clarification, streaming/quality. | conversation/platform | A/B implemented; stream/metrics source exists but the complete C quality gate is not proven. | `PRESENT_PARTIAL` | Finish versioned eval set, latency/dead-end telemetry and Chromium streaming evidence. |

Current top-level count: **3 `NATIVE_COMPLETE_VERIFIED`**, **13 `PRESENT_PARTIAL`**, **6 `MISSING_HIGH_VALUE`**, **1 `EXTERNAL_DEFERRED` with verified internal core**, and **1 `SUPERSEDED`**. Inventory coverage is 24/24; verified native product coverage is not 100%.

### 37.3 One orchestration core: Analytics and Assistant

`AnalyticsPage.vue` and the global Assistant both render the shared `ErumiChatPanel.vue`. The shared component owns model selection, session pointer, history browser, progressive clarification, artifact renderers and Action Composer handoff. The server owns session/turn state through `/api/ai/assistant/*`; localStorage is limited to presentation/model preference and the last session pointer, never the canonical history.

Both surfaces therefore use the same:

- Assistant session/turn endpoints and idempotency contract;
- capability/context registry and `IAiNativeAuthorizationService`;
- goal planning, bounded read loop and deterministic fallback;
- model-profile request and actual provider/model response metadata;
- progressive question draft persistence and batch submission;
- artifact renderers and explicit-confirm mutation endpoints;
- read-back receipts, activity events and error/degraded states.

No page-specific write executor receives implementation credit. A legacy path is `LEGACY_OR_DUPLICATE` until removed or proven to delegate to the same registered capability.

### 37.4 Server-owned session model

Canonical entities remain distinct: `AssistantSession` groups context and turns; `AssistantTurn` stores each user/assistant exchange; `AiJob` stores execution; `AiGeneratedDraft`/launch artifacts store review state; execution receipts store committed outcomes. `GET /api/ai/assistant/sessions` is user-scoped and capped at 100 current items; `GET .../{sessionId}` restores ordered turns, drafts/artifacts/jobs/receipts. Create, rename, archive and soft-delete have real CSRF/concurrency-checked APIs. Project/session isolation returns not-found/deny without leaking hidden titles.

### 37.5 Interaction inventory

| Interaction | Real use case / persistence | Permission and states | Current evidence |
|---|---|---|---|
| Phiên/Lịch sử | List/get server sessions; switch and reload full transcript/artifacts. | Current user only; loading/empty/error/active/archived. | Integration session lifecycle + Chromium `SESSION-LIVE-01`. |
| Cuộc trò chuyện mới | POST session; old sessions remain. | Authenticated user, CSRF. | Shared panel source + session integration. |
| Rename/archive/delete | PATCH/POST/DELETE with expected version. | Session owner; conflict/deny handled. | `SessionHistory_CanListRenameArchiveAndSoftDeleteWithVersionChecks`. |
| Nguồn dữ liệu | Render server-provided source disclosures/refs; no invented route. | Source authorization already applied; empty state. | Shared `SourceRefsDrawer`; context registry tests. |
| Activity/process | Persisted safe job/turn stages, collapsed by default. | No chain-of-thought/private payload. | Action Composer Chromium + activity integration assertions. |
| Continue/retry/cancel/resume | Durable turn control and linked resume. | Session owner, safe execution boundaries. | Turn API integration and bounded-loop Chromium. |
| Export | Download latest answer as Markdown; Project export API remains a separate authorized route. | Disabled with clear error when no result. | Implemented source; dedicated Chromium still backlog. |
| Settings/model selector | Request carries selected profile; response shows actual provider/model; settings navigates to real privacy controls. | Server privacy/budget/provider policy is authoritative. | Typecheck/build + shared component source. |
| Quick reply/free text | Stores answer by stable question ID without implicit submit. | Max three questions; user explicitly submits the batch. | Progressive reload Chromium. |
| Gửi tất cả/Xóa bản nháp | PUT/DELETE server clarification draft with version. | Owner/CSRF/concurrency; save-race protected. | Turn integration + loop Chromium. |
| Open/close artifact | Opens canonical persisted draft/brief/plan/receipt renderer. | Capability/resource permission. | Action/launch Chromium. |
| Confirm/reject | Exact reviewed payload, stable idempotency and row version. | Server rechecks feature, system tier, Project role and source freshness. | SQL integration + Action Composer Chromium. |
| Receipt/deep-link | Read-back verified created IDs/links. | Only after commit/read-back succeeds. | Integration canonical assertions + Chromium receipt. |
| Monitor/replan/rollback | Governed Project baseline controls; replan is review-only. | Organization/project manager, stale/impact checks. | Project-launch integration + Chromium B/C/D. |
| Actions drawer suggestions | Prompt/draft suggestions only, explicitly labeled non-mutating. | No direct write event. | UI text prevents false success; real mutation opens a registered composer. |

Any new interaction must add event → use case/API → authorization → persistence/reload → state handling → automated evidence before release. Otherwise it must be hidden or disabled with an explicit unavailable reason.

### 37.6 AI permission matrix

The backend resolves user-specific `SystemModulePermission` before system-role defaults, then resolves active custom Project roles to their built-in base role. Unknown/removed custom roles fail closed for writes. UI visibility is advisory only.

| Actor / capability | Read/analysis | Artifact proposal | Mutation draft | Confirmed mutation | External/admin effect | Denial behavior |
|---|---|---|---|---|---|---|
| AI Hub `Restricted` user override | Deny | Deny | Deny | Deny | Deny | No capability/source materialization; safe reason only. |
| AI Hub `SummaryOnly` | Authorized sources only | Read-only research/summary | Deny | Deny | Deny | Useful analysis remains available; no mutation card. |
| Project Member / custom Member | Authorized Project/private boundary only | Personal/read-only proposal | Deny by default | Deny | Deny | Not-found/permission deny without hidden source titles. |
| Project Manager / custom Manager | Authorized managed Project | Yes | Yes for registered adapters | One explicit confirm, concurrency/idempotency/read-back | No unless separately granted | 403/409 with draft retained for review. |
| Organization Admin/Owner | Authorized Organization/Projects | Yes | Yes for registered admin adapters | Explicit confirmation for material mutation | Separate adapter/consent required | Tenant/privacy/audit boundaries cannot be bypassed. |
| System Admin | Authorized administrative scope | Yes | Registered adapters only | Still confirmation/idempotency/audit controlled | `admin_ai_control` only | No unrestricted shell/SQL/tool authority. |

Authorization is rechecked at discovery, source materialization, draft creation, immediately before mutation and read-back. The AI never has more authority than its caller.

### 37.7 Task create and assignment integrity

The canonical confirm sequence is now:

1. GET the current draft and reconcile confirmed/pending/stale state.
2. Require a non-empty server `rowVersion`; legacy drafts without it are invalidated with review guidance.
3. Use one stable `action-confirm:{draftId}` key in header and body for the logical confirmation. `rowVersion` remains an optimistic-concurrency precondition, not part of the operation identity.
4. Disable duplicate submit; stale version or source returns 409 and requires fresh review.
5. Revalidate schema, exact selected commands, registered tool, system tier, custom/base Project role and current active assignee membership.
6. Execute selected Task/assignment/skill rows atomically; persist activity, audit and receipt.
7. Read back canonical Task identity, content, assignment, Sprint, estimate, priority, due date and skill rows before success.
8. Replay/lost response returns the stored receipt and creates no duplicate.

The Action Composer model is no longer allowed to assign from `workload_only` data. Model-authored Tasks are unassigned. A reviewer may explicitly select a current Project member; removal/deactivation before confirm produces a stale conflict and zero mutation. Explainable automatic staffing must use CAND-017/CAND-023, which owns skill evidence, declared availability, capacity, multi-Project load, focus cost, concurrency limit, fairness and Rulebook decisions.

### 37.8 Persistence and migration correction

Migration `20260812165330_P015AiRolePermissionPersistence` brings `ProjectCustomRoles`, `SystemModulePermissions` and `ProjectMemberRoleHistories` into the migration chain and fixes the active-role filtered index for SQL Server. `dotnet ef migrations has-pending-model-changes` returns no pending model change after the build. A clean isolated SQL Server LocalDB applies the full migration chain and executes the real Task confirmation path.

### 37.9 Evidence matrix — 2026-08-13

| Acceptance criterion | Evidence |
|---|---|
| Release build | `dotnet build Qaly_project.slnx -c Release --no-restore`: PASS, 0 warnings, 0 errors. |
| Frontend contract | `npm run typecheck`: PASS; `npm run build`: PASS, 3,998 modules. |
| Unit | Full `Qaly.UnitTests`: 591/591 PASS, including system-tier/custom-role discovery and Action Composer contracts. |
| Integration | Final full `Qaly.IntegrationTests`: 168/168 PASS, including isolated SQL Server migration/canonical persistence. |
| Web features | Full `Qaly.WebFeatureTests`: 38/38 PASS. |
| Session history | `AiAssistantTurnApiTests` session create/list/rename/archive/delete/draft/reload + Chromium `TEST-AI-NATIVE-SESSION-LIVE-01`: PASS. |
| Task idempotency/count | `TEST-ACTION-COUNT-10`: draft contains 10 commands, confirm persists 10 unique Tasks, replay returns same IDs. |
| Relational canonical persistence | `TEST-ACTION-SQL-COUNT-10`: clean isolated SQL Server migration + HTTP compose/confirm/replay + canonical Task count 10: PASS. |
| Assignee freshness | `TEST-ACTION-ASSIGNEE-STALE-01`: removed member returns 409/source-stale and creates zero Task. |
| Action UI | Chromium `TEST-ACTION-E2E`: progress, editable review, single confirm and receipt PASS. |
| Durable launch conversation | Chromium `TEST-AI-NATIVE-LOOP-E2E`: reload, verified brief and progressive revision PASS. |
| Governed Project lifecycle UI | Chromium `TEST-PL-BCD-E2E`: plan confirm/receipt/monitor/replan PASS; integration suite supplies the non-mock canonical graph proof. |
| Runtime preview smoke | Release host with Development + isolated in-memory data returned HTTP 200 at `/health`. |

### 37.10 Current completion boundary and next priorities

- **Inventory coverage:** 24/24 CAND dispositioned.
- **Verified native coverage:** not 100%; only rows explicitly marked `NATIVE_COMPLETE_VERIFIED` have fresh current evidence.
- **Main-flow product completeness:** partial. Task-create and governed internal Project launch are verified; checklist/subtask, Wiki AI, digest and Group Poll remain material gaps.
- **Internal Project-launch core:** verified complete within its bounded contract.
- **External adapter completeness:** not complete; calendar, repository, invitation, webhook and deployment remain `EXTERNAL_DEFERRED`.

Priority after this corrective checkpoint:

1. Complete/reverify CAND-022D skill packs for all existing main read/proposal/mutation surfaces and add the missing 022C Chromium gate.
2. Implement CAND-020 Group Poll Draft.
3. Approve domain semantics and implement CAND-003 + CAND-004, then CAND-014.
4. Implement CAND-012 Wiki adapter and CAND-013 persisted digest.
5. Finish CAND-024C streaming/quality release evidence.
6. Add external CAND-023 adapters one provider at a time with outbox, compensation and explicit authority.

**Disposition:** the Task mutation regression, session-history proof gap, Analytics/Assistant split, current AI authorization gap and relational migration drift are closed by implementation and evidence in this checkpoint. The overall AI Native program is not labeled 100% complete; remaining internal skill/domain gaps and external adapters retain explicit dispositions above.

## 38. Authoritative internal main-flow closure and non-browser release gate

**Checkpoint date:** 2026-08-13 (Asia/Saigon)

**Authority:** this section supersedes the runtime dispositions and verification policy in §37 whenever they conflict. Historical Chromium evidence remains historical only. By explicit Product Owner instruction, Chromium/Playwright is permanently excluded from this goal and from every future completion gate for this workstream. It must not be run, retried, or treated as missing evidence. Completion is decided once, after the full objective is implemented, by the non-browser gate in §38.5.

### 38.1 One objective and one orchestration boundary

The single objective is to close the complete internal Qaly AI Native main-flow surface before testing: natural conversation and durable sessions; authorized grounded reads; registered editable drafts; save/reject/one-confirm; canonical transactional mutation; read-back/receipt/deep-link; monitor/replan; and useful provider degradation. Analytics and the global Assistant continue to use the same `ErumiChatPanel`, server session APIs, capability/context registry, model policy, authorization boundary and artifact renderers.

No legacy planner, autonomous approval endpoint, task-breakdown job or acceptance-checklist job may mutate or generate a competing review contract. Those legacy mutation paths return HTTP 410 and direct callers to the registered unified Assistant capability. Existing page-specific read/proposal surfaces remain valid only when they use canonical authorization and do not bypass a native confirmation boundary.

### 38.2 Canonical CAND-001…024 disposition

`NATIVE_COMPLETE_VERIFIED` below means the internal contract is source-complete and must pass the single §38.5 non-browser gate. It does not mean that a read-only feature gains an unnecessary mutation adapter, nor that an external provider integration is simulated.

| CAND | Role in Qaly AI Native | Authoritative disposition |
|---|---|---|
| CAND-001 | Grounded Project progress summary and canonical source receipt. | `NATIVE_COMPLETE_VERIFIED` |
| CAND-002 | Grounded Sprint progress/risk summary. | `NATIVE_COMPLETE_VERIFIED` |
| CAND-003 | Editable acceptance-checklist draft, one confirm, canonical rows, Task Hub read-back and concurrency-safe completion toggle. | `NATIVE_COMPLETE_VERIFIED` |
| CAND-004 | Exact-count ordered subtask/dependency graph with preserved estimate and leaf-only progress semantics. | `NATIVE_COMPLETE_VERIFIED` |
| CAND-005 | Server-owned AI usage, budget, warning and hard-stop controls. | `NATIVE_COMPLETE_VERIFIED` |
| CAND-006 | Explainable Project-local assignee recommendation; unknown skill/capacity is never fabricated. | `NATIVE_COMPLETE_VERIFIED` |
| CAND-007 | Selected-message grounded Group summary. | `NATIVE_COMPLETE_VERIFIED` |
| CAND-008 | Source-linked Group-to-Task reviewed draft. | `NATIVE_COMPLETE_VERIFIED` |
| CAND-009 | Server-snapshot Dashboard strategic brief. | `NATIVE_COMPLETE_VERIFIED` |
| CAND-010 | Privacy-gated Meeting checknote/action proposal. | `NATIVE_COMPLETE_VERIFIED` |
| CAND-011 | Historical Project Planner concept replaced by CAND-023. | `SUPERSEDED_BY_CAND_023`; excluded from the active denominator |
| CAND-012 | Section-grounded Wiki brief plus optional reviewed Task mutation. | `NATIVE_COMPLETE_VERIFIED` |
| CAND-013 | Persisted per-user Project digest schedule, enable/disable intent, authorized delivery worker and delivery state. | `NATIVE_COMPLETE_VERIFIED` for the internal scheduler; configured email transport remains an environment dependency |
| CAND-014 | Task Hub launchers plus canonical checklist read-back. | `NATIVE_COMPLETE_VERIFIED` |
| CAND-015 | Canonical Task skill taxonomy and reviewed requirements. | `NATIVE_COMPLETE_VERIFIED` |
| CAND-016 | Evidence-backed member skill profile; missing evidence remains unknown. | `NATIVE_COMPLETE_VERIFIED` |
| CAND-017 | Portfolio capacity/availability/load-aware assignment and schedule proposal. | `NATIVE_COMPLETE_VERIFIED` for internal facts; external calendars remain deferred |
| CAND-018 | Natural Task intent to editable exact-count draft, save/reject, stable idempotent confirm and canonical receipt. | `NATIVE_COMPLETE_VERIFIED` |
| CAND-019 | Unified chat-first Assistant shell, real server session browser and navigation. | `NATIVE_COMPLETE_VERIFIED` |
| CAND-020 | Editable native Group Poll draft, one confirm and canonical Poll/options read-back. | `NATIVE_COMPLETE_VERIFIED` |
| CAND-021A–D | Durable sessions/turns, context registry, source disclosure, model selection and grounded research foundation. | `NATIVE_COMPLETE_VERIFIED` |
| CAND-022A–D | Goal understanding, bounded read executor, Development/Test-only safe test capability and registered main-flow skill packs. | `NATIVE_COMPLETE_VERIFIED`; 022C never grants production shell/SQL/unregistered-tool authority |
| CAND-023A–D | Rulebook → Launch Brief → deterministic staffing/delivery → confirmed canonical Project graph → monitor/replan. | `INTERNAL_CORE_COMPLETE_VERIFIED`; overall `EXTERNAL_DEFERRED` |
| CAND-024A–C | Answer-first conversation, progressive multi-answer interaction, streaming deltas and versioned quality metrics/evaluation set. | `NATIVE_COMPLETE_VERIFIED` |

Internal active coverage is therefore **23/23 active CAND IDs** (CAND-011 is superseded) with **zero `MISSING_HIGH_VALUE` and zero `PRESENT_PARTIAL` internal rows**, subject to the final gate. CAND-023 external repository, calendar, invitation, webhook and deployment adapters are deliberately outside this internal denominator and remain explicit rather than mocked.

### 38.3 Business-logic closure

- Intent classification prefers the action object actually requested. “Create Tasks for starting a Project” routes to Task Composer; “start a Project, then create Tasks” routes to Project Launch. Checklist, breakdown, Wiki Task, Group Poll and digest enable/disable have explicit registered routes and evaluation cases.
- A Project Member/custom Member may use authorized grounded reads, summaries, Wiki/document analysis and session history, but cannot receive a mutation draft or confirm one. Full system AI tier and the canonical Project/Group role are rechecked at discovery, draft load/save/reject, confirm, read-back and scheduled delivery.
- Native drafts are server-persisted and restored inside the owning Assistant turn. Edits increment revision; reject is durable; confirm requires the exact reviewed payload, source version, row version and one stable logical idempotency key. A different key cannot replay a committed draft.
- Provider/model failure cannot become a conversational dead end or a false mutation success. The deterministic server fallback remains useful, actual provider/model is recorded, and canonical mutation success is claimed only after database read-back.
- Exact requested Task/subtask counts are preserved within contract bounds. Breaking down a Task distributes its existing estimate instead of inventing capacity, makes the parent non-progress-contributing and keeps leaf subtasks progress-contributing, preventing Dashboard/Analytics double counting.
- Acceptance checklist items are visible in Task Hub after confirmation and can be completed only by an authorized manager with a current concurrency token. Manual completion changes the source version, so any older AI draft becomes stale.
- Digest enable/disable is inferred from natural language, persisted per user/Project, and delivered only after the worker rechecks current system tier and Project read access. Revoked access disables the subscription rather than leaking a later digest.
- Process, source context and work-plan detail remain collapsed by default; the answer/result remains first. Session pointer/model preference may use local storage only as UI preference; transcript, drafts, artifacts, answers and receipts remain server canonical.

### 38.4 Remaining boundary

The internal goal does not authorize or claim provider-specific Google/Outlook calendar sync, repository creation, outbound invitation, webhook or deployment execution. Each requires its own credential/scope, outbox, compensation, receipt and authorization review. Until then, CAND-023 reports these operations as `EXTERNAL_DEFERRED` and never reports them successful.

### 38.5 Single final verification gate — Chromium permanently excluded

Run this gate only after implementation and documentation closure:

1. solution Release build with no restore;
2. frontend typecheck and production build;
3. focused Unit tests for routing, capability/role discovery, conversation quality and digest delivery;
4. focused API/relational Integration tests for exact-count Task creation, native draft confirmation, concurrency/idempotency, rollback and canonical read-back;
5. EF pending-model-change check.

Full Unit, Integration and WebFeature suites are not part of this gate unless a focused failure points to a shared-infrastructure regression. There is no Chromium/Playwright step, no browser replay substitute and no requirement to repeat previously historical browser evidence. At the time this section was written, the gate is `PENDING_FINAL_NON_BROWSER_GATE`; §38.6 records the one final result.

### 38.6 Final non-browser evidence

**Result:** `ACCEPTED_INTERNAL_CORE` with the external boundary in §38.4 still `EXTERNAL_DEFERRED`.

The closure batch fixed five concrete gaps found by focused evidence: native breakdown phase labels are unique for exact-count drafts; Group Poll separates the question from its options; digest content is re-authorized and scoped to the reader's Project role; a concrete task route/launcher now takes precedence over a stale Project selection in the global Assistant; and the local/dev launcher now connects to the Docker SQL Server with the same port/database/credential settings instead of incorrectly attempting Windows Integrated Security.

| Gate | Evidence actually run | Result |
|---|---|---|
| Focused Unit | Routing/goal planning, context/role discovery, conversation quality, launch contracts and digest worker; focused digest retry after its change | `58/58 PASS`; digest rerun `2/2 PASS` |
| Native domain API | Checklist, exact 10-subtask graph, Wiki Task, Group Poll, digest, stale-source rollback, durable draft/reject, Member denial, streaming quality and legacy-path retirement | All affected cases pass after the duplicate-phase fix; exact 10-subtask and Group Poll cases were rerun directly |
| Task Composer API | `ExplicitTenTaskRequest_ConfirmPersistsExactlyTenAndReplayCreatesNoDuplicate` | `1/1 PASS`; 10 reviewed commands, 10 canonical Tasks, stable replay |
| Project Launch/session | Task-vs-Project disambiguation, durable follow-up context, atomic confirm/monitor/rollback/idempotency and stale-capacity zero-mutation | `4/4 PASS` |
| Relational canonical read-back | Latest P016 migration plus checklist and exact 10-subtask persistence/read-back on SQL Server/LocalDB | `1/1 PASS` |
| Schema consistency | EF `migrations has-pending-model-changes` for Infrastructure/Web Release artifacts | PASS, no pending model change |
| Final build | `dotnet build Qaly_project.slnx -c Release --no-restore` | PASS, 0 warnings, 0 errors |
| Frontend | `npm run typecheck`; `npm run build` | PASS; production bundle generated |
| Local/dev runtime | `npm run dev:all:ai`, migrations/seed, `GET /health`, login surface and startup log | PASS; SQL/Redis/Seq/MailHog/Qdrant/Ollama healthy, migrations and seed complete, `/health` 200, no unhandled startup exception |

Chromium/Playwright, full suites and duplicate WebFeature coverage were not run, by the explicit non-browser and high-signal verification policy in §38.5. Canonical internal data creation is accepted; repository/calendar/invitation/webhook/deployment providers remain honestly deferred until their credentialed adapters, outboxes and compensation receipts exist.

### 38.7 Cross-surface business-flow acceptance — Analytics + global Assistant

**Checkpoint:** 2026-08-13. This pass validates the user outcome, not merely whether each isolated branch compiles. Analytics and the global Assistant must produce the same canonical turn, preserve its Project/session context, render the artifact appropriate to the outcome and keep every visible control connected to a real use case.

| User outcome | Required response artifact | Interaction / navigation contract | Acceptance disposition |
|---|---|---|---|
| Ask a grounded status, risk, workload or comparison question | Markdown answer plus server-grounded metric/table/chart/source disclosures as applicable | Suggested follow-ups remain editable chat prompts; sources open the source drawer | `ACCEPTED_INTERNAL` |
| Ask what Qaly can do | Formatted capability summary plus navigation cards | Cards open only registered top-level Qaly routes | `ACCEPTED_INTERNAL` |
| Create Tasks with a known Project | Task Composer handoff card, not a Project Launch Brief | Analytics and global Assistant both open the same persisted Action Composer; one confirm and receipt remain mandatory | `ACCEPTED_INTERNAL` |
| Create Tasks without a Project | Real Project-choice card using authorized active Projects | Choosing a Project sets the conversation context and opens Task Composer directly; it no longer resubmits “Chọn dự án” as a looping prompt | `ACCEPTED_INTERNAL` |
| Launch a new Project | Progressive multi-answer card → Launch Brief → staffing/delivery plan → one confirm → canonical receipt/deep-link | Answers persist by stable ID; Project launch is not confused with “create Tasks for an existing Project” | `ACCEPTED_INTERNAL` |
| Run a registered native domain action | Capability-specific editable card plus save/reject/confirm states | Checklist, breakdown, Wiki Task, Group Poll and digest use server drafts, row version, idempotency, read-back and valid Project/Task/Wiki/Group routes | `ACCEPTED_INTERNAL` |
| Research or compare options | Findings, unknowns, alternatives, recommendation and eligible action cards | An executable Task proposal opens Task Composer; non-executable proposals remain honestly read-only | `ACCEPTED_INTERNAL` |
| Return to earlier work | Server session list and restored ordered turns/artifacts/drafts/receipts | Restored Project context is retained; component mount no longer resets it to workspace | `ACCEPTED_INTERNAL` |
| Provider/planner degradation | Useful deterministic result or durable retry/resume card | No false mutation success and no privileged/dead action synthesized by the model | `ACCEPTED_INTERNAL` |

Concrete gaps closed in this pass:

1. `AnalyticsPage` now bridges `compose-action` to the same global Action Composer used by the floating Assistant; the prior Task card on Analytics was visually valid but behaviorally disconnected.
2. A restored session keeps its canonical Project target instead of being overwritten to `workspace` after load.
3. `select_project_for_task_plan` renders a Project selector and opens Task Composer with the original request; it no longer becomes another plain-text prompt.
4. Card-only/structured turns count as the latest Assistant result, so the cockpit does not silently bind to an older text response.
5. The Actions drawer preserves action semantics: navigation opens a route, composer opens the structured draft, resume calls the durable resume API, and complex clarification actions return the user to their interactive card.
6. Provider-authored action types are normalized to non-privileged `suggested_action`. Only deterministic server code may issue navigation, resume, composer or mutation controls with validated payloads.
7. Provider-authored file URLs are ignored. Download cards are issued only by Qaly's deterministic authorized export flow, preventing a model from showing a fake or unverified file.

Focused evidence: frontend `npm run typecheck` PASS; three affected Erumi routing/clarification Unit contracts PASS; the provider-action/file trust-boundary Unit contract rerun PASS; five focused API integrations for Task-vs-Project routing, capability navigation, durable turn reload, session lifecycle and exact-ten-Task idempotent persistence PASS from the existing integration build while the local preview retained the Web DLL lock. No Chromium/Playwright or broad duplicate suite was run, per §38.5. Local `/health` remains HTTP 200 with SQL Server, Redis and vector outbox healthy.

### 38.8 Compact newcomer QOL and contextual tool acceptance

**Checkpoint:** 2026-08-13. This pass audits discoverability and interaction integrity across the shared Analytics/global-Assistant surface. It does not add another toolbar or duplicate the chat contract.

Gaps closed:

1. The header overflow menu is now a real contextual tool index: new session, server session history, compact AI tool hub, current structured data/actions when present, sources, model, text export when exportable, and direct AI privacy navigation. Empty/dead response-specific items are not rendered.
2. The same overflow menu is available in the empty state. Three compact newcomer shortcuts explain common outcomes and only fill the composer; they never auto-send or mutate data.
3. The AI tool hub shows the active Project/workspace scope, separates quick-start prompts from analysis/result tools, and labels whether a choice opens an existing result, fills an editable question or prepares a reviewed draft.
4. Per-response overflow actions are scoped to the selected response. Data, actions and sources open that response rather than silently using the latest one; copy/export appears only when text exists. Card-only turns still show provider/confidence metadata without an empty overflow control.
5. Complex interactive actions in the Actions drawer return to the exact originating message card. Navigation, durable resume and Task Composer actions keep their original semantics instead of degrading into a plain-text prompt.
6. The data drawer now recognizes metrics, tables and charts. Empty drawers offer one useful editable prompt rather than becoming a dead end.
7. Source follow-up closes the drawer and focuses an editable composer prompt. Labels across the `+` menu, tool hub and header now consistently use “Công cụ AI”, “Lịch sử phiên” and “Model AI”.
8. `/settings?tab=privacy` is now a real deep link: Settings reads and maintains the requested tab instead of always opening Profile. This closes the prior behaviorally-wrong “Quyền riêng tư AI” navigation.
9. Overflow groups have visual separators while retaining keyboard/menu semantics; process/context/work-plan detail remains collapsed by default so the answer/result stays first.

Fresh focused evidence after the implementation: frontend `npm run typecheck` PASS; production `npm run build` PASS (3,995 modules); targeted `git diff --check` PASS. Chromium/Playwright and broad duplicate suites were not run, per §38.5.

Remaining optional QOL, explicitly outside this compact closure: organization-curated/pinned prompt packs, cross-device persistence for purely presentational preferences, and a bundled export format for card-only structured artifacts. None blocks canonical analysis, reviewed mutation, session restore, navigation or the internal AI Native completion boundary; external CAND-023 adapters remain `EXTERNAL_DEFERRED` under §38.4.

### 38.9 Live clarification/state-authority and editable Launch Brief correction

**Checkpoint:** 2026-08-13. A live Assistant replay exposed three defects that the earlier contract-level acceptance did not catch:

1. When a selected conversational provider failed during a grounded read, `AssistantPlannedTurnAsync` replaced the requested answer with a generic capability pitch and surfaced `not_reached / not_reached` as if it were a model identity. The corrected route retries through the deterministic canonical Qaly reader, returns the requested workspace/Project result when possible, records `Qaly / qaly-native`, and emits a specific degraded navigation card only when both provider and server reader fail.
2. The client inferred whether Launch questions were answered by scanning arbitrary transcript text. This could hide the question card while the durable server Brief still contained blocking questions. The server is now the sole clarification-state authority; the client renders exactly the questions returned by the current durable turn.
3. Project Launch was a read-only summary plus scattered question inputs. It is now a typed review form for Project name, objective, timebox/deadline, primary audience and must-have scope, with optional success measures and exclusions. The form sends one structured `launch.brief_form` reply, creates a durable Brief revision and proceeds to staffing only when all required review fields are valid. Model-proposed scope never counts as user confirmation. The canonical Project mutation still requires the existing explicit plan confirmation and read-back receipt.

The clarification question contract now carries server-owned `inputType` and `placeholder` hints so select/text/textarea rendering follows the information type without client keyword routing. Launch Brief persistence also retains the reviewed `targetTimebox` and `primaryAudience`; later staffing/delivery uses the revised canonical Brief rather than the fallback baseline.

Fresh focused evidence: solution Web build PASS with zero warnings/errors; frontend typecheck PASS; focused Unit `4/4 PASS` for capability menu and grounded provider-to-server fallback; focused Integration `5/5 PASS` for typed Launch review persistence/unlock, complete auto-plan, multi-answer reload, capability navigation and provider-failure Brief fallback. The typed-form integration read back revision 2 with the customized Project name/timebox/audience/scope, produced a staffing plan under an effective Rulebook, and verified zero Project rows before explicit confirmation. No Chromium/Playwright or broad duplicate suite was run, per §38.5.

### 38.10 Structured Project planning and customization closure

**Checkpoint:** 2026-08-13. A product-level replay showed that §38.9 closed the text-form dead end but did not yet make Project planning professionally structured. The Brief still treated objectives, KPI, features and customization mostly as strings; staffing alternatives were fixed renderings; Sprint edits had no durable revalidation route; and canonical Tasks did not retain an explicit Objective/Feature trace. This checkpoint corrects those gaps end to end rather than adding UI-only controls.

No CAND-025 is introduced. Every change has an existing authoritative owner, so a new number would duplicate the inventory and hide responsibility:

| Existing owner | Increment completed in this checkpoint | Completion boundary |
|---|---|---|
| CAND-023A | Structured `ObjectiveProfile`, KPI metrics with nullable baseline/target, guardrails/assumptions/non-goals, selected `Feature` objects and a typed review workspace. | Brief/review artifact only; zero early Project mutation. |
| CAND-015 | Canonical skill catalog metadata: category, aliases, default proficiency and system-seed provenance; API create/update/search/read-back; P017 migration and idempotent realistic demo taxonomy. | Skill evidence remains separately verified by CAND-016; absence of evidence is not converted to absence of skill. |
| CAND-017 + CAND-023B | Scope-dependent Lean/Balanced/Accelerated team sizing plus editable staffing allocation/manager/role, server revalidation and editable Sprint/Task plan. | Hard capacity/availability/concurrency/Rulebook constraints remain server-owned; editing never mutates canonical Project data. |
| CAND-019/021/024 | Result-first Vietnamese renderer, structured cards/forms, compact details, explicit disabled reasons and real save/retry/navigation controls. | Provider/model, schema, scoring and decision trace remain collapsed technical metadata. |
| CAND-018 + CAND-023C | One-confirm atomic execution retains idempotency/read-back/rollback and now persists `ProjectLaunchTaskTrace` rows from canonical Tasks to the reviewed Brief, Feature, KPI IDs and source refs through P018. | External repository/calendar/invitation/webhook/deployment effects remain `EXTERNAL_DEFERRED`. |
| CAND-019/021 + CAND-023 | Server plan revisions persist staffing and Sprint/Task customization; GET/session artifact restoration returns the latest revision. | Local-only presentational preferences are not treated as business state. |

#### 38.10.1 Structured business trace

The durable flow is now:

`Problem/Outcome → KPI → selected Feature → Sprint → Task/dependency → required OrganizationSkill → reviewed assignee → ProjectLaunchTaskTrace`.

KPI baseline and target are nullable and explicitly labeled `needs_confirmation`/reviewed; the server never fills unknown numbers. Feature cards retain category, priority, audience, description, acceptance criteria and required skill names. Model skill names that do not resolve to an active Organization catalog row remain visible skill gaps and cannot become member evidence. Canonical Task skill rows are still created only for reviewed catalog IDs.

#### 38.10.2 Customization and revalidation contract

`PUT /api/ai/project-launch/plans/{planId}` accepts the exact expected revision, selected staffing scenario, included members/manager/role/allocation and the edited Sprint/Task graph. The server rejects stale source hashes, stale row revisions, invalid date windows, empty selections, duplicate Task IDs, missing dependencies, dependency cycles, unauthorized/ineligible members, unavailable hours, missing required-skill evidence, insufficient total allocation and Rulebook max-utilization violations. A successful save creates a new durable Plan revision only; the Project graph remains absent until the existing explicit confirm.

The workspace supports KPI and Feature ordering, common templates plus custom values, rich Feature acceptance data and specialized skill gaps, Lean/Balanced/Accelerated comparison, member/manager/allocation/role changes, Sprint/Task add/disable/reorder, date/goal/title/estimate/assignee edits, and one “save and check again” action. The confirm control explains that edited data must first be saved and revalidated.

#### 38.10.3 Flow × role × state × renderer × action × evidence matrix

| Flow | Role / state | Renderer | Real action | Focused evidence |
|---|---|---|---|---|
| Natural launch request → reviewed Brief | Authorized organization manager; clarification or ready | Objective/KPI + Scope/Feature workspace | One structured reply creates Brief revision; zero Project rows | `TEST-AI-NATIVE-LAUNCH-FORM-01` |
| Skill selection/customization | Organization manager; catalog active/inactive | Skill chips plus custom specialized skill input | Catalog POST/PATCH persists category/aliases/default level; unknown project skill remains a gap | `TEST-SKILL-01/09/11`; `RichDemoSeedTests` |
| Staffing alternatives | Manager; feasible/blocked/stale | Three comparison cards plus customizer | PUT revalidates permission, evidence, capacity, availability, load and Rulebook | `TEST-AI-NATIVE-PLAN-CUSTOMIZE-01`; existing stale-capacity contract |
| Sprint/Task customization | Manager; clean/dirty/invalid | Editable Sprint and Task cards | PUT saves one Plan revision; GET/session reload reads the same graph | `TEST-AI-NATIVE-PLAN-CUSTOMIZE-01` |
| Create canonical Project graph | Authorized manager; pending review/executing/executed | Explicit confirm then receipt/deep links | Atomic Project/member/Sprint/Task/dependency/skill/trace write, read-back and stable replay | `TEST-PL-BCD-01` |
| Read/analyze/propose | Member with source access | Short answer, metric/table/card as appropriate | Read/proposal only; mutation discovery and confirm remain denied | Existing CAND-021C/CAND-018 Member authorization evidence |
| Provider degradation | Any authorized reader | Useful deterministic result plus compact limitation | Server fallback; no false mutation success | Existing provider-failure Brief and grounded-reader evidence |
| Resume earlier work | Owning user/session | Server session browser and restored cards/forms | Latest Brief/Plan/draft/receipt reload; frontend does not infer server answer state | Existing session lifecycle plus Plan GET read-back evidence |

#### 38.10.4 Acceptance disposition

The internal structured planning slice is `NATIVE_COMPLETE_VERIFIED` inside its declared boundary. Fresh non-browser evidence on 2026-08-13 is exact and repeatable: `dotnet build Qaly_project.slnx -c Release --no-restore` PASS with 0 warnings/0 errors; frontend `npm run typecheck` PASS; frontend production `npm run build` PASS (3,995 modules); EF pending-model check PASS after applying P017/P018 to local/dev; focused `RichDemoSeedTests` PASS 2/2; structured Brief, durable Plan customization/read-back and canonical confirm/idempotency/read-back/rollback integrations PASS 4/4 in aggregate. The canonical execution integration persisted and read back the real Project/member/Sprint/Task/dependency/skill/`ProjectLaunchTaskTrace` graph before exercising audited rollback. `git diff --check` PASS. Chromium/Playwright remains excluded by §38.5 and was not run.

External calendar/repository/invitation/webhook/deployment adapters are unchanged and remain `EXTERNAL_DEFERRED`; no simulated success is introduced. Optional higher-order UX such as drag-and-drop animation, automatic semantic merge of two user-authored Features and provider-specific calendar impact previews does not block the canonical structured flow and is not claimed as implemented.

### 38.11 Project-launch business-integrity corrective closure

**Checkpoint:** 2026-08-14. A second business-flow audit found cases where the implementation could compile and create rows while still violating the reviewed operating intent. These corrections remain owned by CAND-023A–D; no new CAND is introduced.

| Existing owner | Business gap closed | Enforced outcome |
|---|---|---|
| CAND-023A | Rulebook drafts previously accepted arbitrary keys/values and the Assistant offered a one-click preset without a real review surface. | The server accepts only implemented rule schemas, validates units and bounded numeric ranges, and rejects unsupported rules. The Assistant now exposes a compact editable review, saves a draft, shows the saved rules and requires a separate activation action. |
| CAND-023B | Aggregate team hours could pass while one member received more Task hours than their reviewed allocation; Sprint validation did not reject overlap, Project-timebox escape or a predecessor placed in a later Sprint. | Assignment is balanced and revalidated per person. Every selected Task has an eligible assignee; reviewer separation is enforced. Sprint/dependency temporal invariants fail closed before any Project mutation. |
| CAND-023C | Reviewer selection was discarded; acceptance criteria and Definition of Done were flattened into Task description text; count-only read-back could miss semantically wrong fields; a session-projection failure after commit could make an already-created Project appear failed. | P019 persists `ReviewerId`, typed canonical checklist rows and stable Sprint/Task client trace IDs. Confirmation commits once, then performs semantic read-back over Project fields, roles, Sprints, Tasks, assignment rows, reviewer, skills, checklist/DoD, dependencies and Feature/KPI/source traces. Receipt state is `verification_pending` until this succeeds. Session synchronization is best-effort and cannot reverse or falsely fail a committed canonical graph; retry resumes verification through the same idempotency key. |
| CAND-023D | Monitoring covered broad counts/status but could miss role, Sprint, assignment, skill, dependency and trace drift or newly invalid capacity/availability. | Replan detection now compares those semantic baselines and re-evaluates each selected member's declared capacity, availability reduction, cross-Project commitments and focus reserve. It only creates a review proposal; it never silently repairs canonical work. |

Migration `P019ProjectLaunchSemanticIntegrity` is additive. Existing launch traces receive deterministic legacy client IDs before the new unique index is created, so upgrading a database with P018 data does not fail on duplicate empty defaults. Existing checklist rows are classified as acceptance items; reviewer remains optional for legacy Tasks.

Fresh focused evidence: Release solution build PASS with 0 warnings/0 errors; frontend typecheck PASS; production build PASS with 3,995 modules; focused Project-launch/Rulebook Integration `6/6 PASS`; focused launch-contract Unit `10/10 PASS`; EF pending-model check PASS; focused diff check PASS. The canonical integration verifies stable idempotent replay, semantic receipt, reviewer and typed checklist/DoD persistence, stale-source zero mutation, per-person overload and Sprint-overlap rejection, Rulebook bounds, plus monitor detection for role/Sprint/assignment/dependency/trace drift. Chromium/Playwright was intentionally not run under §38.5.

**Disposition:** CAND-023 remains `INTERNAL_CORE_COMPLETE_VERIFIED` for the governed internal flow with the stronger semantic contract above. Calendar, repository, invitation, webhook and deployment adapters remain `EXTERNAL_DEFERRED`; no external success is claimed or simulated.

### 38.12 Unified runtime, recoverable customization and truthful Task confirmation

**Checkpoint:** 2026-08-14. This follow-up closes the gaps where individually correct capabilities still produced an incoherent user flow. No new CAND is introduced; the changes extend the existing authoritative owners.

| Existing owner | Gap closed | Enforced outcome |
|---|---|---|
| CAND-018/019/021 | `/analytics` could instantiate both the page assistant and the floating assistant against the same session/version. | Analytics now owns one primary conversation runtime. Global assistant actions focus that runtime while the shared Action Composer remains available as an artifact surface. |
| CAND-019/021/024 | A confirmed Task draft could display successful command rows before canonical semantic read-back, and a retry could receive a new idempotency key after `rowVersion` changed. | Confirmation uses one stable key per draft. Receipt and every command remain verification-failed/pending until Tasks, assignment, Sprint, estimate, priority, due date and skill rows match canonical data. Retry re-runs read-back only and cannot create duplicates. |
| CAND-019/021 | Action Composer customization could be lost on reload or a stale browser draft could overwrite a newer server revision. | Edits autosave with optimistic `rowVersion`; browser recovery is user-scoped. Newer server state wins by default and the local copy is offered explicitly as a recoverable alternative. |
| CAND-017/023B | Automatic balancing was treated as the only valid workflow and overlapping Sprints were always rejected. | Review now separates hard invariants from preferences: `auto_balance` or `preserve_assignments`, and `sequential_sprints` or `parallel_workstreams`. Preserve mode permits an explicit unassigned backlog and never silently replaces a user selection; rights, skill evidence, capacity, availability and dependency order remain server-enforced. |
| CAND-019/023A-D | Project Launch working edits had durable server revisions only after save; unsaved browser work had no recovery layer. | User-scoped browser recovery restores Brief and dirty Plan customization after reload. Canonical Project state is still mutated only by the reviewed server plan and explicit confirm. |
| CAND-019/021/024 | Model selection was visible but Action Composer still used a fixed provider hint; explicit Task counts could be silently shortened. | Provider/model selection now reaches Action Composer routing and receipts retain actual provider/model. Numeric and Vietnamese/English word counts are honored through 20; larger batches are rejected with a split-batch instruction instead of silent truncation. |

The hard boundary is explicit: permission, organization scope, stale source/revision, idempotency, skill evidence, legal/Rulebook limits, real capacity/availability, dependency correctness and canonical read-back are non-bypassable. Assignment strategy, backlog, staffing selection, reviewer choice, Sprint/workstream layout and editable content remain user-controlled within those invariants. Server revisions are authoritative business state; browser backup is recovery state only and never constitutes successful execution.

Fresh focused evidence: frontend typecheck PASS; targeted diff check PASS; Action Composer Integration `7/7 PASS`; preserve-assignment/parallel-workstream canonical Project integration `1/1 PASS`; focused Action Composer contract Unit `16/16 PASS`; Release Web build PASS from the same checkpoint. Chromium/Playwright was intentionally not repeated under §38.5. The running Debug preview was not stopped; integration evidence was produced in Release to avoid replacing a loaded Debug assembly.

**Disposition:** the internal unified assistant/customization/Task-confirmation slice is `INTERNAL_CORE_COMPLETE_VERIFIED` within the declared boundary. Time-phased weekly staffing and reviewer-overhead optimization remain a documented planning refinement. Calendar, repository, invitation, webhook and deployment adapters remain `EXTERNAL_DEFERRED`; no external success is claimed or simulated.

## 39. Acceptance-driven reopening — Section 3 prompt coverage (P01–P28)

**Checkpoint:** 2026-08-16. Product Owner accepts sections 1 and 2 of the full-project acceptance script as the operating instructions and surface inventory. This amendment is therefore scoped only to section 3, prompts P01–P28. It does not repeat the general inventory or the manual checklist.

### 39.1 Corrected completion statement

The historical `INTERNAL_CORE_COMPLETE_VERIFIED` dispositions in §38 remain useful implementation and integration evidence, but they are not sufficient evidence that the public Assistant prompt flow is complete. Runtime replay has shown that a capability can exist and its API tests can pass while the natural-language turn is routed to the wrong capability, receives the wrong source scope, renders as generic text, loses session scope, or never exposes the real confirm/read-back action.

Until every row in §39.4 passes, the authoritative product disposition is:

`INTERNAL_COMPONENTS_PRESENT / SECTION_3_ACCEPTANCE_REOPENED / AI_NATIVE_PRODUCT_NOT_YET_ACCEPTED`.

No CAND-025 is introduced. The work reopens the existing CAND that owns each outcome. A prompt is not complete merely because its underlying endpoint, service, DTO, card shell or isolated integration test exists.

### 39.2 Runtime evidence that forces the reopening

1. **P02 wrong lane:** a workspace read request produced the hard-coded task-mutation limitation (“Trợ lý AI hiện chỉ hỗ trợ soạn bản nháp để tạo task mới…”). The same response is emitted by the generic write fallback in `ErumiChatService`. A noun such as Task inside a read/summary request must never be enough to select a mutation lane.
2. **P03 scope loss:** the UI showed selected Project `Qaly Release 4.0`, but the response said no authorized Project context existed. The client sends both `projectId` and the current route entity. In `AiAssistantContextRegistry.ResolveAsync`, a grounded-read turn on a Group route is currently resolved as Group before the selected Project is resolved; the explicit Project selection is therefore ignored and only one unrelated source reaches the turn.
3. **Session/scope mismatch:** selecting a Project while a workspace session is open is intentionally allowed without starting a new session, but the persisted session scope can remain workspace. Reload can consequently restore the transcript while losing the effective Project target. Conversation history and business scope must be persisted separately and restored together.
4. **Renderer/action mismatch:** several implemented capabilities still reach the user as generic prose or a navigation hint. Presence of a native action service does not prove that the public prompt opens its editable artifact and reaches confirm/read-back.

These are business-flow defects, not cosmetic test failures. They invalidate a blanket P01–P28 completion claim.

### 39.3 One corrective goal and execution rules

**Single goal:** make all P01–P28 in `19_FULL_PROJECT_AND_AI_NATIVE_ACCEPTANCE_SCRIPT.md` reach the correct authorized capability, preserve the intended workspace/Project/Task/Wiki/Group/Meeting scope across reload, render the appropriate compact interactive artifact, execute only through the governed confirmation path, and prove canonical read-back or an honest non-mutation/deferred result.

Rules for this goal:

1. Keep `routeEntity` and `selectedBusinessScope` as distinct server-owned concepts. A Group/Wiki/Task route may provide a source entity while an explicitly selected Project remains the analysis and session scope.
2. Explicit user selection wins over ambient route inference for generic Project/workspace analysis. A capability-specific target wins only for an explicit Group Poll, Wiki, Meeting or Task action.
3. Persist a scope change on the current session with optimistic concurrency; do not silently create a new session and do not discard the existing transcript. Reload must restore both the session and the selected scope.
4. Classify by requested outcome and action object, not by isolated nouns. Read prompts that mention Task, Project or “việc cần chú ý” remain reads unless an explicit create/update/assign action is present.
5. Every turn resolves to a registered tuple: `intent + capability + authorized sources + renderer + allowed actions + evidence contract`. Unknown tuples degrade to a useful read/fallback response; they never become a dead-end pitch.
6. Structured outcomes use typed metrics, tables, forms, review cards, before/after cards or receipts. Long prose, provider trace and process details remain collapsed. Text remains valid only for a genuinely narrative answer.
7. Mutation always follows `durable editable draft → one explicit confirm → idempotent execution → semantic read-back → receipt/deep-link`. A card or toast alone is not success.
8. Hard constraints remain non-bypassable: authorization, tenant scope, skill evidence, declared capacity/availability, Rulebook/legal limits, dependency validity, stale source/version and idempotency. Preferences and editable content remain user-controlled.
9. Do not run Chromium after every change. Use focused Unit/Integration tests per wave and one final targeted browser/manual replay for the interaction-heavy prompts. Re-run a broad suite only when a focused failure implicates shared infrastructure.

### 39.4 P01–P28 ownership and concrete fix plan

Status vocabulary:

- `RUNTIME_FAIL`: reproduced product behavior contradicts the prompt contract.
- `PARTIAL`: meaningful implementation exists, but prompt-to-artifact-to-evidence is incomplete.
- `REVERIFY`: backend evidence exists; the exact public prompt still requires an acceptance fixture and replay.
- `EXPECTED_DEFERRED`: the correct result is an honest deferred card, not an implemented external side effect.

| Prompt | Current disposition | Existing owner reopened | Required corrective increment | Acceptance evidence required |
|---|---|---|---|---|
| P01 — capabilities by role | `PARTIAL` | CAND-019/021/024 | Build cards from server-discovered capabilities and authorization state; separate read, draft, confirm and unsupported; every visible navigation is server-registered. | Admin and Member fixtures show different cards; each button opens the declared route; no dead or privileged action. |
| P02 — real workspace summary | `RUNTIME_FAIL` | CAND-001/009/019/021 | Add an explicit workspace-summary read intent; prevent Task nouns from entering write fallback; return canonical Project/task/workload metrics with source refs and typed blocks. | Exact P02 fixture matches Dashboard/API counts; renderer has metric/table/cards; no task-mutation limitation or `not_reached/100%`. |
| P03 — selected Project analysis | `RUNTIME_FAIL` | CAND-001/002/019/021/024 | Make explicit selected Project precede ambient Group route for generic analysis; authorize Project sources independently from the surface entity; retain deep-links to canonical Task/Sprint rows. | On a Group route with Project chip selected, exact P03 reads that Project; source set contains the Project packs; private sources remain redacted. |
| P04 — durable session memory | `PARTIAL` | CAND-021A/024B | Persist user-stated session facts as ordered durable turn/context data; restore after reload; do not confuse facts with instructions or Project mutation. | Two-turn MVP fixture passes before and after reload without asking for the three features again. |
| P05 — session history/title | `PARTIAL` | CAND-021A/024B | Add/finish session title mutation with row version, real session list, resume and scope restoration. History must list sessions, not only old messages. | Rename, reload, list and resume the same session ID; summary and selected scope match; no duplicate session. |
| P06 — structured unknowns | `REVERIFY` | CAND-023A/019/024B | Route launch intent to typed multi-answer form; at most three blocking unknowns; options plus “Khác”; no send-on-first-keystroke; server owns answered state. | Exact P06 renders the form, retains all answers across reload and creates no Project. |
| P07 — unknown answers to Brief | `REVERIFY` | CAND-023A | Map timebox, audience, scope and quantitative success metrics into a new durable Brief revision; reuse supplied facts. | Read-back shows 12 weeks, target audience, four must-haves and all stated KPI/quality targets; no repeated question. |
| P08 — staffing/delivery scenarios | `REVERIFY` | CAND-015/016/017/023B | Recompute scope-sized alternatives from verified skills, declared availability/capacity, cross-Project load and effective Rulebook; expose editable team/Sprint/Task controls and blocked reasons. | Three distinct scenarios with trade-offs; team size is scope-derived; invalid capacity cannot be confirmed; edits persist as a Plan revision. |
| P09 — canonical Project graph | `REVERIFY` | CAND-018/023C | Connect the selected reviewed plan to one final review card and one confirm; persist Project, roles, Sprints, Tasks, dependencies, skill requirements and traces atomically; select the new Project in the same session. | Canonical DB/API read-back, semantic receipt and working deep-link after reload; no second Project on retry. |
| P10 — idempotency | `REVERIFY` | CAND-018/023C | Reuse the execution idempotency key and resume verification rather than re-executing; expose stable receipt state. | Same key returns the same receipt and unchanged row counts for Project/Sprint/Task/dependency. |
| P11 — exactly 10 Tasks | `REVERIFY` | CAND-018/019 | Preserve explicit count and named Task set through model/fallback normalization; open editable Action Composer rather than a three-item generic draft. | Exact P11 yields ten selectable commands; confirm creates exactly selected rows and replay creates zero duplicates. |
| P12 — assignment schedule | `PARTIAL` | CAND-006/017/019/021 | Route to `task.assignment_schedule.v1` with the open Task ID; open editable candidate/date card; score verified skills, availability, capacity, deadline and portfolio load. | Intent, Project and Task IDs are exact; changing candidate/date revalidates; no Project Launch or Task-create artifact. |
| P13 — assignment confirm | `PARTIAL` | CAND-017/018 | Supply rowVersion/idempotency internally from the durable draft; one confirm executes exactly one assignment and performs semantic read-back. | Receipt is `succeeded`, `readBackVerified=true`; exactly one canonical assignee/assignment; retry is stable. |
| P14 — invalid capacity | `REVERIFY` | CAND-017/023B | Treat absent capacity/availability as unknown, not 40h; fail closed on unavailable/overloaded candidates and offer valid alternatives. | Exact negative fixture disables confirm, explains the binding constraint and performs zero mutation. |
| P15 — stale source | `REVERIFY` | CAND-017/018/023B | Bind Task/project/capacity source revisions into the assignment draft and compare them at confirm. | Concurrent Task edit returns stale/conflict, preserves canonical state and offers regenerate/review. |
| P16 — acceptance checklist | `PARTIAL` | CAND-003/018/019/022D | Register prompt handoff to native checklist draft; render exactly five editable rows; keep row version and Task scope. | Exact P16 creates five canonical typed checklist rows only after one confirm and reads all five back after reload. |
| P17 — breakdown | `PARTIAL` | CAND-004/018/019/022D | Register prompt handoff to native breakdown artifact; preserve exact count, order, parent, dependencies, estimates and catalog skills. | Exact P17 creates four subtasks and the expected DAG; reload and receipt agree. |
| P18 — Wiki brief/Tasks | `PARTIAL` | CAND-012/018/019/022D | Prefer explicit Wiki target, capture page revision and section anchors, render sourced brief plus at most three optional Task cards; create only checked Tasks. | Section links reopen the source; selected Tasks persist once; stale Wiki revision blocks confirm. |
| P19 — Group Poll | `PARTIAL` | CAND-020/018/019/022D | Prefer explicit Group Poll capability on a Group route; render editable question, four options and deadline; connect confirm to native Poll action. | Poll and four options survive reload; duplicate confirm is idempotent; Member permissions are enforced. |
| P20 — Meeting actions | `PARTIAL` | CAND-010/018/019/022D | Produce decision/blocker/action-item cards from authorized transcript; each action item can link to an existing Task or prepare a new Task draft; never auto-create. | Transcript revision/source shown; per-item mapping is editable; only confirmed mappings mutate canonical data. |
| P21 — Roadmap/Sprint | `PARTIAL` | CAND-014/017/019/022D/023D | Add a durable before/after proposal using dependencies, capacity and deadline; expose Sprint edits and revalidation; no implicit save. | Exact P21 shows before/after; one confirm persists the reviewed revision; stale baseline blocks. |
| P22 — weekly digest | `PARTIAL` | CAND-013/018/019/022D | Route to native digest preference draft; use organization timezone; review schedule/recipients/scope; confirm and read back subscription revision. | 09:00 Monday schedule and timezone survive reload; permission and duplicate-confirm tests pass. |
| P23 — skill evidence | `PARTIAL` | CAND-015/016/019/022D | Build evidence proposal only from verified completion/acceptance provenance; expose attribution review; exclude labels/private chat as sole evidence. | Confirmed evidence links Task, acceptance item, reviewer and skill; invalid provenance is rejected. |
| P24 — monitor/replan | `REVERIFY` | CAND-017/023D | Route exact prompt to baseline comparison; show scope/schedule/staffing/task drift in before/after cards; produce a review proposal only. | Drift fixture is detected with source revisions; zero canonical repair occurs before a later governed confirm flow. |
| P25 — Member read-only | `PARTIAL` | CAND-019/021C/024 | Preserve useful grounded read and source navigation when mutation capability is denied; suppress confirm controls, not the answer. | Member sees only authorized sources and useful recommendations; mutation APIs/cards are absent or denied server-side. |
| P26 — provider failure | `PARTIAL` | CAND-021/024A/C | Fall back to the deterministic grounded server reader/planner, record actual provider/model, retain the requested outcome and never emit false mutation success. | Forced provider failure returns useful scoped data or resumable draft; provider metadata is truthful; no dead-end capability pitch. |
| P27 — renderer/navigation | `PARTIAL` | CAND-001/002/019/021/024 | Define typed metric/table/Task/Member/Sprint result blocks and validated per-row actions; avoid a long text dump; keep technical trace collapsed. | Exact P27 renders three overdue Tasks, three high-load members and at-risk Sprint with correct canonical links. |
| P28 — external adapters | `EXPECTED_DEFERRED` | CAND-023 external boundary | Return an adapter-status card derived from real configuration/health/receipt state. Calendar, repository, invitation, webhook and deployment remain `EXTERNAL_DEFERRED` unless credentialed adapters perform read/write/read-back. | No simulated success. Each unavailable adapter is named and deferred; any available adapter must show a verifiable receipt. |

### 39.5 Delivery waves and stop-the-line order

The implementation must follow dependency order; later artifacts are not useful while context routing is wrong.

| Wave | Prompts | Outcome | Exit gate |
|---|---|---|---|
| W0 — context/router blocker | P02, P03 | Separate selected Project scope from route entity; correct read-vs-write classification; persist scope. | Exact API fixtures for P02/P03 plus frontend typecheck. Do not continue if either returns generic mutation limitation or wrong source. |
| W1 — conversation shell | P01, P04, P05, P25, P26, P27 | Role-aware cards, durable session/title/scope, useful fallback, typed renderers and navigation. | Focused Unit/Integration plus one manual reload/session replay. |
| W2 — Project launch | P06–P10 | Structured unknowns through idempotent canonical Project graph. | Existing Project-launch integration extended with exact prompt fixtures and semantic DB read-back. |
| W3 — Task operations | P11–P17 | Exact Task count, assignment schedule, invalid/stale protection, checklist and breakdown. | Focused Action Composer/native-action/assignment integrations; canonical count and DAG read-back. |
| W4 — remaining native surfaces | P18–P24 | Wiki, Poll, Meeting, Roadmap, digest, skill evidence and monitor/replan become real prompt-reachable artifacts. | One focused integration per capability; route/source/revision/permission/confirm evidence. |
| W5 — truth boundary | P28 and final matrix | Honest external status and final P01–P28 replay. | All rows PASS or explicitly `EXTERNAL_DEFERRED`; no `PARTIAL`, generic fallback or dead navigation remains. |

W3 is the immediate next implementation slice after the verified W0–W2 checkpoints below. Task operations must reuse the corrected Project/session scope and governed confirmation contracts instead of introducing isolated keyword-only paths.

### 39.6 Required acceptance fixture contract

Each P01–P28 prompt becomes a versioned fixture with these assertions:

1. exact normalized intent and selected capability;
2. session ID, selected Project/surface entity and organization scope before and after reload;
3. authorized source IDs, source revisions and redaction result;
4. renderer type and required typed fields;
5. visible action IDs, target routes and permission state;
6. mutation mode (`none`, `draft_then_confirm`, or `external_deferred`);
7. expected canonical count/semantic read-back for mutation prompts;
8. idempotency, stale-source and provider-failure outcome where applicable;
9. actual provider/model, never `not_reached/not_reached` with fabricated confidence;
10. concise Vietnamese result first; process/context/trace collapsed by default.

The fixture catalog is the release authority. Ad-hoc keyword tests, DTO existence, UI screenshots without action evidence and historical broad “complete” labels cannot close a row.

### 39.7 Final gate for section 3

Section 3 is accepted only when:

- all P01–P27 fixtures pass end to end, with canonical read-back for every mutation;
- P28 reports real adapter receipts or `EXTERNAL_DEFERRED` without simulated success;
- the exact P02 and P03 regressions in §39.2 are covered by automated tests;
- Member, stale-source, duplicate confirm and provider-failure cases cause no unauthorized or duplicate mutation;
- frontend typecheck/build and focused affected backend tests pass;
- one final targeted manual/browser replay covers session reload, multi-answer form, Project selection from a non-Project route, editable confirm cards and deep-link navigation.

No repeated per-change Chromium run is required. The final report must list P01–P28 individually as `PASS`, `FAIL` or `EXTERNAL_DEFERRED`; aggregate percentages or CAND-level completion labels are not substitutes.

### 39.8 W0 implementation checkpoint — P02/P03 router and source scope

**Checkpoint:** 2026-08-16. The first stop-the-line slice is implemented and has focused automated evidence. This checkpoint closes the reproduced routing/source defects; it does not yet close the separate session-scope persistence requirement assigned to W1.

| Prompt | Corrective result | Evidence | Disposition |
|---|---|---|---|
| P02 | Goal-planner reconciliation now prevents a model-ranked mutation capability from overriding a server-classified read/research request. Canonical workspace metric/table/chart requests are built by the deterministic server reader before provider prose can redirect the turn. | Exact prompt Unit plus API Integration; canonical Project/overdue-Task fixture; typed metrics, table and source assertions. | `AUTOMATED_W0_PASS / FINAL_REPLAY_PENDING` |
| P03 | Explicit selected Project now wins over an ambient Group route for generic Project analysis. An explicit Group request still uses Group scope, preserving capability-specific targets without discarding user selection. Planner reconciliation also selects the authorized read route when the model proposed an unavailable mutation capability. | Exact prompt Goal-planner Unit, Context Registry Unit for both precedence branches, and API Integration asserting Project summary/tasks sources with no Group-source substitution. | `AUTOMATED_W0_PASS / FINAL_REPLAY_PENDING` |

Focused evidence from the same working tree: Goal-planner/context Unit `4/4 PASS`; exact P02/P03 API Integration `2/2 PASS` in Release. Release output was used because the active preview legitimately held the Debug Web assemblies; the preview was not stopped. Existing analyzer warnings in `ErumiRoadmapAiService` are outside this focused slice and did not fail the run.

### 39.9 W1 implementation checkpoint — conversation shell, role-aware fallback and typed navigation

**Checkpoint:** 2026-08-16. W1 now has focused automated evidence for P01, P04, P05 and P25–P27. This closes the server/API and renderer contracts in this wave; one final manual session reload/navigation replay remains part of the section-level gate in §39.7.

| Prompt | Corrective result | Evidence | Disposition |
|---|---|---|---|
| P01 | Capability overview is derived from the authorized server registry. Admin receives draft/confirm affordances; Member receives read-only capability and navigation cards without Task-create/assignment leakage. External adapters are labelled `EXTERNAL_DEFERRED`. | Exact API fixtures for Admin and Member `2/2 PASS`. | `AUTOMATED_W1_PASS / FINAL_REPLAY_PENDING` |
| P04 | Session facts are acknowledged and recalled from canonical server history after reload; no workspace query or mutation is used for memory recall. | Exact Unit `1/1 PASS`; API reload/recall Integration `1/1 PASS`. | `AUTOMATED_W1_PASS / FINAL_REPLAY_PENDING` |
| P05 | Project scope changes in place on the current session with optimistic concurrency. Transcript, title and clarification state are retained; stale updates return conflict instead of silently forking context. | Session scope/history/context Integration `3/3 PASS`; frontend typecheck PASS. | `AUTOMATED_W1_PASS / FINAL_REPLAY_PENDING` |
| P25 | A read-only Member request cannot be misclassified as Task assignment merely because it describes denied mutation. The response still contains canonical Project metrics, three useful recommendations and source-navigation cards; no mutation/confirm artifact is rendered. | Exact P25 API Integration `1/1 PASS`. | `AUTOMATED_W1_PASS / FINAL_REPLAY_PENDING` |
| P26 | Research-provider outage now degrades to the deterministic Project/workspace reader for retryable provider failures. The returned metadata states actual `Qaly / qaly-native`; no unpersisted action is reported as successful. Policy, consent and schema failures remain fail-closed. | Exact P26 provider-failure Unit `1/1 PASS`. | `AUTOMATED_W1_PASS / FINAL_REPLAY_PENDING` |
| P27 | Table contract supports a validated per-row navigation action. The exact prompt returns bounded Task, member-workload and at-risk Sprint tables; each row carries a canonical internal route and the UI renderer displays its open button. | Exact P27 API Integration `1/1 PASS`; frontend typecheck PASS. | `AUTOMATED_W1_PASS / FINAL_REPLAY_PENDING` |

W1 does not mark Section 3 complete. P06–P24 still require their wave-specific fixtures and canonical mutation read-back, and P28 remains `EXTERNAL_DEFERRED` unless real external adapters provide receipts. The next active slice is W2 (P06–P10): Project Launch brief → editable staffing/delivery proposal → one confirm → canonical graph read-back, including exact task counts, skill/capacity/availability checks and stale/duplicate safety.

### 39.10 W2 implementation checkpoint — governed Project launch P06–P10

**Checkpoint:** 2026-08-16. The complete Project-launch wave now has exact prompt routing plus canonical mutation evidence. This checkpoint does not close Section 3; W3–W5 remain open.

| Prompt | Corrective result | Evidence | Disposition |
|---|---|---|---|
| P06 | Generic product wording no longer falsely satisfies detailed scope. The form asks at most three actual blockers, supplies common options plus `Khác…`, accepts free text and retains multi-answer server drafts. The quoted Project name is preserved by the deterministic fallback. | Exact P06/P07/P08/P09 API sequence `1/1 PASS`; existing clarification reload fixture; frontend typecheck PASS. | `AUTOMATED_W2_PASS / FINAL_REPLAY_PENDING` |
| P07 | Natural Vietnamese timebox, individual-customer audience, four must-haves and quantitative E2E/p95/Critical targets update the durable Brief revision directly and do not re-ask answered facts. | Exact API assertions for Brief revision, scope and three typed metrics. | `AUTOMATED_W2_PASS / FINAL_REPLAY_PENDING` |
| P08 | The exact staffing prompt routes to `project.staffing.plan.v1`, returns three scenarios and typed Sprint/Task/dependency/estimate/skill data. Missing skill catalog is now a hard blocker; missing capacity is never converted into availability. Editable staffing/Sprint/Task changes persist as a reviewed Plan revision. | Exact prompt integration plus focused customization, overload and Sprint validation regressions `3/3 PASS`. | `AUTOMATED_W2_PASS / FINAL_REPLAY_PENDING` |
| P09 | The selected plan is returned again on the confirmation turn instead of pointing to an older chat message. The UI shows a compact final summary for Project, manager/team, Sprint count and Task count; the existing governed confirm remains the sole mutation action. | Exact P09 session/plan identity assertion; canonical confirm/read-back regression `2/2 PASS`; frontend typecheck PASS. | `AUTOMATED_W2_PASS / FINAL_REPLAY_PENDING` |
| P10 | Retry with the same idempotency key returns the same receipt. The exact prompt routes to read-back monitoring and reports duplicate safety only when the canonical graph still matches the confirmed baseline. | Exact P10 Integration `1/1 PASS`, asserting unchanged Project/Sprint/Task/dependency row counts and stable receipt ID. | `AUTOMATED_W2_PASS / FINAL_REPLAY_PENDING` |

Focused evidence from this working tree: P06–P09 exact sequence Integration `1/1 PASS`; P10 exact canonical/idempotency Integration `1/1 PASS`; P08–P10 intent Unit `3/3 PASS`; Plan customization/stale/capacity/confirm/monitor regression Integration `5/5 PASS`; frontend typecheck PASS; Release Infrastructure build PASS with only four pre-existing `ErumiRoadmapAiService` analyzer warnings. Chromium was intentionally not repeated under §39.3 rule 9.

The next active wave is W3 (P11–P17): exact Task batch creation, assignment scheduling and its invalid/stale gates, then checklist and subtask DAG confirmation/read-back.

### 39.11 W3 implementation checkpoint — Task graph, assignment and Task completion P11–P17

**Checkpoint:** 2026-08-16. W3 is implemented through the same public Assistant contracts and canonical executors used by the manual Task/Kanban surfaces.

| Prompt | Corrective result | Evidence | Disposition |
|---|---|---|---|
| P11 | An explicit request for ten Tasks produces exactly ten editable commands. Every command carries description, acceptance criteria, estimate, priority and required skill; the reviewed graph preserves dependencies. Confirmation persists exactly ten canonical Tasks and replay creates no duplicate. | Exact-count Action Composer Integration plus count/parser Contract Unit. | `AUTOMATED_W3_PASS / FINAL_REPLAY_PENDING` |
| P12 | The opened Task is ranked against confirmed skill evidence, declared capacity, availability and multi-Project load. Alternatives and the proposed time window remain editable; no assignee or deadline is written while reviewing. | Exact P12/P13 Assistant Integration and portfolio scheduling Integration. | `AUTOMATED_W3_PASS / FINAL_REPLAY_PENDING` |
| P13 | The same durable assignment proposal is reopened for final review. One confirmation writes exactly one `TaskAssignment`, reads it back and uses a stable idempotency key. | Assignment proposal/confirm/read-back and replay Integration. | `AUTOMATED_W3_PASS / FINAL_REPLAY_PENDING` |
| P14 | Missing or insufficient declared capacity and unavailable windows are hard blockers. Qaly does not invent 40h capacity or treat an empty calendar minute as usable capacity; alternatives remain read-only. | `P14_NoDeclaredCapacityAvailable...` plus invalid-capacity portfolio fixtures. | `AUTOMATED_W3_PASS / FINAL_REPLAY_PENDING` |
| P15 | Task/source changes after proposal cause stale-source conflict and zero partial mutation. The user must regenerate the proposal. | Cross-tenant/stale-source portfolio Integration. | `AUTOMATED_W3_PASS / FINAL_REPLAY_PENDING` |
| P16 | The exact request creates five editable acceptance rows only after one confirmation and reads all five canonical rows back. | P16/P17 native-domain Integration. | `AUTOMATED_W3_PASS / FINAL_REPLAY_PENDING` |
| P17 | The exact request creates four canonical subtasks with parent links, estimates, required skills and an ordered dependency graph; reload preserves the graph. | P16/P17 native-domain Integration. | `AUTOMATED_W3_PASS / FINAL_REPLAY_PENDING` |

### 39.12 W4 implementation checkpoint — Wiki, Poll, Meeting, Roadmap, Digest, evidence and replan P18–P24

**Checkpoint:** 2026-08-16. W4 replaces generic-text fallbacks with typed, server-persisted review cards and registered native action capabilities.

| Prompt | Corrective result | Evidence | Disposition |
|---|---|---|---|
| P18 | Wiki brief is grounded to section references. Up to three Task candidates are editable and unchecked by default; confirmation creates only selected Tasks and reads them back. | Exact P18/P19/P22 native-domain Integration. | `AUTOMATED_W4_PASS / FINAL_REPLAY_PENDING` |
| P19 | Poll draft contains exactly four clear options, editable question/options and a three-day expiry. A single confirm creates and reads back the canonical Poll. | Exact P18/P19/P22 native-domain Integration. | `AUTOMATED_W4_PASS / FINAL_REPLAY_PENDING` |
| P20 | The open meeting transcript resolves to the canonical import/extraction. Decisions, blockers and action items are displayed; every action defaults to no mapping. Only selected existing-Task mappings or new Task drafts are persisted. | Exact P20/P21/P23 native-domain Integration. | `AUTOMATED_W4_PASS / FINAL_REPLAY_PENDING` |
| P21 | Roadmap analysis compares dependency, capacity and deadline facts and renders selected Sprint changes as before/after rows. Only selected adjustments are applied and read back. | Exact P20/P21/P23 native-domain Integration. | `AUTOMATED_W4_PASS / FINAL_REPLAY_PENDING` |
| P22 | Weekly Digest persists Monday 09:00 using the current user's Organization timezone and returns the canonical subscription state. Source version includes the timezone profile. | Exact P18/P19/P22 native-domain Integration. | `AUTOMATED_W4_PASS / FINAL_REPLAY_PENDING` |
| P23 | Skill evidence is available only for a Done Task with completed confirmed acceptance, a real assignee/assignment and confirmed required skill. Confirmation delegates to the canonical evidence service; label and private chat are never evidence. | Exact P20/P21/P23 native-domain Integration. | `AUTOMATED_W4_PASS / FINAL_REPLAY_PENDING` |
| P24 | Monitor compares the confirmed launch baseline with current scope, schedule, staffing, Task, dependency and trace facts. It returns meaningful drift rows with baseline/current and a review-only replan; no silent repair occurs. | Project launch confirm/monitor/rollback Integration with exact P24 prompt. | `AUTOMATED_W4_PASS / FINAL_REPLAY_PENDING` |

Routing regressions found by the aggregate gate were fixed at the semantic boundary: a read request mentioning Sprint/dependency no longer becomes a Roadmap mutation draft; a Project launch request mentioning staffing still starts with the Launch Brief; staffing text mentioning skill evidence no longer becomes skill-attribution; and customization wording such as “thêm/bớt người ... sửa Task” no longer becomes Task creation. Exact P18–P24 intent fixtures now lock those distinctions.

### 39.13 W5 checkpoint — truthful external-adapter status P28 and complete automated matrix

**Checkpoint:** 2026-08-16. P28 now has a deterministic server-owned status route. It does not call a model and cannot expose a mutation action. The response contains exactly Calendar, Repository, Invitation, Webhook and Deployment; each row is `EXTERNAL_DEFERRED` because no provider-specific adapter currently has both a write receipt and provider read-back. Existing GitHub links or credentials do not count as an AI Native write/read-back adapter.

Focused P28 Integration proves `UsedAi=false`, five deferred rows, internal navigation only and unchanged Project/Task counts. P28 therefore passes the truthfulness requirement as `EXTERNAL_DEFERRED_VERIFIED`; it is not counted as an implemented external integration.

| Prompt | Automated disposition | Prompt | Automated disposition |
|---|---|---|---|
| P01 | PASS | P15 | PASS |
| P02 | PASS | P16 | PASS |
| P03 | PASS | P17 | PASS |
| P04 | PASS | P18 | PASS |
| P05 | PASS | P19 | PASS |
| P06 | PASS | P20 | PASS |
| P07 | PASS | P21 | PASS |
| P08 | PASS | P22 | PASS |
| P09 | PASS | P23 | PASS |
| P10 | PASS | P24 | PASS |
| P11 | PASS | P25 | PASS |
| P12 | PASS | P26 | PASS |
| P13 | PASS | P27 | PASS |
| P14 | PASS | P28 | `EXTERNAL_DEFERRED_VERIFIED` |

Fresh aggregate evidence from the same working tree: focused Section-3 Integration `22/22 PASS`; goal-planning/capability/coverage Unit `41/41 PASS`; Release Web build PASS; frontend typecheck PASS. Four existing `ErumiRoadmapAiService` analyzer warnings may appear on a non-incremental test-project build and are not hidden as product evidence. The aggregate suite caught and closed the semantic routing conflicts described above. Chromium/Playwright was intentionally not repeated, following §39.3 rule 9 and the Product Owner's cost constraint.

**Authoritative state:** `SECTION_3_AUTOMATED_GATE_PASS / FINAL_TARGETED_MANUAL_REPLAY_PENDING`. The implementation goal has automated evidence for every P01–P27 outcome and truthful P28 deferral. This does not waive the one final manual/browser replay required by §39.7 for visual session reload, multi-answer editing, Project switching, card controls and deep-link navigation; until that replay is recorded, the broader product label remains pending rather than silently promoted to fully accepted.
