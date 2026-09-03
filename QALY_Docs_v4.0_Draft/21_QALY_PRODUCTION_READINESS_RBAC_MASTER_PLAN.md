# Qaly V4 — Demo/Thesis Readiness & RBAC Master Plan

> **Authoritative status:** `DEMO_READY_THESIS — ACCEPTED 2026-09-02`
> This file supersedes informal readiness claims for the graduation-demo goal. Historical PASS records remain evidence inputs, not current acceptance. A row is PASS only when the current working tree has source/test/runtime evidence described here. Full-production work that is not required to demonstrate the product honestly is tracked separately in `22_QALY_POST_THESIS_PRODUCTION_BACKLOG.md`.

## 1. Audit identity and evidence rules

| Field | Value |
|---|---|
| Goal | Đưa Qaly V4 thành `DEMO_READY_THESIS`: các workflow trọng tâm chạy end-to-end bằng dữ liệu thật trong database demo; RBAC và AI đúng role/scope; UI không dead-end; mutation có canonical read-back; build/test và một lượt demo replay có bằng chứng. |
| Audit time | 2026-09-01 00:18:58 +07:00 (Asia/Saigon) |
| Branch | `codex/recent-feature-gap-closure` |
| Base commit | `795fa7ea2d0b737b345b584cd937a93d17f8a09d` |
| Initial worktree | Clean; the audit subsequently changed regression tests and this document. |
| Repository scale | 17 Vue pages; 73 Vue components; 43 API controller files; 357 HTTP action attributes after Professional Profile API; inventory remains versioned with this working tree. |
| Current automatic evidence | Client unit `270/270 PASS`; backend Unit `821/821 PASS`; Integration `331/331 PASS`; WebFeature `49/49 PASS`; Vue typecheck PASS; frontend production build PASS; solution build `0 warning / 0 error`. |
| Browser evidence | Final bounded Chromium replay: P16–P17 live natural prompts `1/1 PASS`; P18–P24 typed review-card matrix `1/1 PASS`; P25–P27 read-only/fallback/renderer/navigation `1/1 PASS`. The P18–P27 UI matrix uses persisted-session fixtures while canonical service/mutation/read-back is covered by Integration. |
| Commit/push | Forbidden unless the user explicitly requests it. |

### 1.1 Scope revision — 2026-09-02

Mốc nghiệm thu hiện tại là **demo báo cáo tốt nghiệp**, không phải chứng nhận vận hành thương mại. Việc thu hẹp này nhằm tăng tốc nhưng không cho phép che lỗi hoặc mô phỏng thành công giả.

**Bắt buộc để đạt `DEMO_READY_THESIS`:**

- không còn tenant/private-data leak, wrong role/scope, fake success, duplicate mutation, stale draft ghi được hoặc dead-end trong runbook demo;
- các persona chính truy cập đúng chức năng và AI chỉ đọc/đề xuất/thao tác trong quyền hiệu lực;
- luồng đăng nhập → Dashboard/Analytics → Project Launch → Sprint/Task → collaboration/reporting chạy bằng seed có thể reset và tái lập;
- mutation quan trọng có confirm contract, canonical read-back sau reload và deep-link/receipt phù hợp;
- UI của màn, drawer, popup và error/empty/loading/disabled state xuất hiện trong kịch bản demo phải rõ blocker và điều hướng được tới nơi sửa;
- focused tests theo capability, full automated batch, typecheck/build và đúng **một** final targeted browser replay đều có bằng chứng.

**Không phải gate của báo cáo, nhưng phải công khai là `DEFERRED_POST_THESIS`:** live external adapters; triển khai production bằng secret thật; load/chaos/fleet rehearsal; ma trận WCAG/toàn bộ breakpoint/dark-mode cho mọi surface; legal/license procurement; SLO, alerting và vận hành dài hạn. Danh sách và điều kiện đóng nằm trong file backlog sau tốt nghiệp.

Evidence hierarchy:

1. canonical database read-back after mutation/reload;
2. current positive and negative integration/contract test;
3. current focused unit test for a pure rule;
4. current source inspection;
5. historical document or screenshot, which can only justify `NOT_VERIFIED` until replayed.

HTTP 2xx, a toast, optimistic state, a provider-authored success sentence, or the existence of a component is not completion evidence.

## 2. System map

### 2.1 Browser routes and primary pages

| Route/surface | Page | Main job | Current gate |
|---|---|---|---|
| `/`, `/dashboard` | `DashboardPage.vue` | Workspace summary, attention, progress and AI entry | `DEMO_GATE_PASS` — P01–P05 scope/session/analytics evidence; exhaustive persona/browser matrix deferred |
| `/profile` | `ProfilePage.vue` | Current-user profile | `DEFERRED_POST_THESIS` — outside the scripted demo; full error/loading/a11y replay remains open |
| `/projects`, `/projects/archived` | `ProjectsPage.vue`, `ArchivedProjectsPage.vue` | Create/list/archive/restore/delete Project | `DEMO_GATE_PASS` — lifecycle Integration plus Project Launch canonical read-back; exhaustive role replay deferred |
| `/projects/{projectId}` | `ProjectDetailPage.vue` | Project tabs: task, member, stats, roadmap, Gantt, workload, wiki, integrations, activity | `DEMO_GATE_PASS` for P06–P24 surfaces; all-tab/device matrix deferred |
| `/projects/{projectId}/tasks/{taskId}` | `TasksPage.vue` | Canonical task detail/workspace | `DEMO_GATE_PASS` — P11–P17 typed draft/mutation/read-back and task authorization evidence |
| `/projects/{projectId}/wiki/{wikiId}` | `WikiDetailPage.vue` | Wiki read/edit | `DEMO_GATE_PASS` for P18 and visibility policy; exhaustive page loading/error/a11y states deferred |
| `/tasks` | `TasksPage.vue` | Cross-project task workspace | Task policy/unit/integration PASS; standalone cross-project persona replay deferred |
| `/teams` | `TeamsPage.vue` | Team/group overview | Group service/source gate PASS; non-demo presentation matrix deferred |
| `/groups`, `/groups/{groupId}` | `TeamsPage.vue` plus chat components | Group membership/chat/file/poll/AI | `DEMO_GATE_PASS` — P19–P21 plus Group/Poll/Meeting authorization batches |
| `/groups/{groupId}/meeting` | `GroupMeetingPage.vue` | Meeting/media/transcript/checknote | `DEMO_GATE_PASS` for internal transcript/action flow; production LiveKit remains external-deferred |
| `/groups/{groupId}/polls`, `/groups/{groupId}/polls/{pollId}` | `GroupPollPage.vue`, `PollCard.vue` | Poll CRUD/vote/realtime | `DEMO_GATE_PASS` — atomic Poll/message mutation and positive/negative vote evidence |
| `/analytics` | `AnalyticsPage.vue` + shared Assistant renderer | Canonical analytics and Assistant parity | `DEMO_GATE_PASS` — P02/P03 canonical filter/metric/source parity |
| `/settings` | `SettingsPage.vue` | Profile/workspace/task preferences, API key, webhook, privacy, AI budget | Focused API key/webhook/privacy/config gates PASS; live external-provider acceptance deferred |
| `/admin/users`, `/admin/moderators` | `AdminUsersPage.vue`, `ModeratorAssignmentsPage.vue` | Platform user and moderator administration | Authorization/deny-wins Integration PASS; exhaustive production persona UI matrix deferred |
| `/organizations`, `/organizations/users` | `OrganizationsPage.vue`, `OrganizationUsersPage.vue` | Organization CRUD/member access role plus independent professional profiles | Focused API/service/client and RBAC evidence PASS; exhaustive production persona UI matrix deferred |
| malformed GUID and catch-all routes | `RouteErrorPage.vue` | Actionable route error without leaking object existence | Source/WebFeature contract PASS; broad browser navigation matrix deferred |

Razor routing authorizes SPA pages server-side. Vue route metadata is presentation/navigation only. API authorization remains authoritative.

### 2.2 UI component and popup inventory

The 73 Vue components are grouped below; every named modal/drawer/card remains part of the final UI gate.

| Group | Surfaces |
|---|---|
| App shell/navigation | `AppShell`, `SidebarNav`, `TopHeader`, `SimulationHeaderBanner`, `WelcomeOverlay`, `PageStatePanel`, `ToastContainer`, `ConfirmDialogHost`, `BootstrapIcon` |
| Project | `ProjectGrid`, `ProjectList`, `ProjectListItem`, `ProjectToolbar`, `ProjectDetailHeader`, `ProjectStatsTab`, `ProjectMembersTab`, `ProjectRoleCapabilityCard`, `ProjectRoleManagerPanel`, `RoleHistoryModal`, `AssignRoleOverlapModal`, `AssignRoleSystemConflictModal`, `ProjectActivityTab` |
| Planning/work | `ProjectRoadmapTab`, `ProjectGanttTab`, `ProjectWorkloadTab`, `ProjectProgressAiCard`, `TaskList`, `TaskItem`, `TaskSkillsAiCard`, `TaskCompletionContributorsCard`, `TaskDevelopmentPanel`, `TeamMiniSection` |
| Wiki/import/integration | `ProjectWikiTab`, `ImportModal`, `ImportUploadStep`, `ImportMappingStep`, `ImportConfirmStep`, `ImportUndoBanner`, `GitHubProjectIntegration`, `GitHubProjectManagement`, `WebhooksTab`, `ApiKeysTab` |
| Assistant/analytics | `ErumiChatPanel`, `FloatingChatbot`, `ChatbotAvatar`, `AiActivityPanel`, `AiActionComposerDrawer`, `ErumiDiffPreviewModal`, `AiOnboardingGuideModal`, `OnboardingGuideCard`, `AnalyticsSideDrawer`, `AiModelSelector`, `AiQuickToolbar`, `AiAnswerMetaChips`, `ComposerPlusMenu`, `ConversationHistoryDrawer`, `SourceRefsDrawer`, `OverflowMenu` |
| Group/collaboration | `ChatSidebar`, `ChatWindow`, `MessageItem`, `GroupAiPanel`, `PollCard`, `MeetingControls`, `ScreenSharePanel` |
| Dashboard/settings | `DashboardSummaryCards`, `AttentionRiskCard`, `RecentActivityWidget`, `StrategicOverviewAI`, `AiUsageBudgetSettingsTab`, `PrivacySettingsTab`, `MemberProfessionalProfileDrawer` |

### 2.3 API map

All normal controllers require an authenticated cookie/API-key principal. An executable reflection contract scans every concrete HTTP action and fails when an action has neither `Authorize` nor `AllowAnonymous`; a reviewed allowlist fixes anonymous access to login, register, CSRF bootstrap and the signed GitHub webhook receiver. `GitHubWebhookController` is intentionally anonymous at HTTP authentication level and relies on signature verification. `ProjectGitHubManagementController` uses the valid combined `[ApiController, Authorize]` form.

| Controller route | Actions | Boundary / business area |
|---|---:|---|
| `api/admin/users` | 10 | Platform user lifecycle and successor handling |
| `api/admin/moderator-assignments` | 3 | Moderator delegation |
| `api/auth`, `api/auth/api-keys`, `api/security` | 11 | Login/session/current user/API key/CSRF |
| `api/Organizations` | 23 | Organization/member/access-role CRUD plus independent professional-profile taxonomy/assignments |
| `api/organizations/{organizationId}/work-rulebook` | 4 | Versioned staffing/work policy |
| `api/Projects`, `api/ProjectRoles`, `api` role-definition routes | 40 | Project lifecycle/member/custom role/permission |
| `api` Sprint routes | 7 | Sprint CRUD/order/status |
| `api/Tasks`, `api/tasks/{taskId}/development` | 29 | Task CRUD/status/assignment/dependency/checklist/skill/dev evidence |
| `api` TimeEntry routes | 4 | Time tracking |
| `api/comments`, `api/attachments`, `api/storage`, `api/votes` | 14 | Collaboration/file/vote/storage lifecycle |
| `api/projects/{projectId}/wiki` | 4 | Wiki visibility/read/write |
| `api/groups`, `api/groups/{groupId}/ai` | 51 | Groups/chat/invitation/poll/meeting/AI |
| `api/meetings` | 7 | Transcript/import/checknote/action mapping |
| `api/Import` | 10 | File/table/document import, preview, execute and undo |
| `api/dashboard`, `api/dashboard/v2/projects`, `api/Analytics`, `api/search` | 9 | Canonical dashboard/analytics/search reads |
| `api/notifications`, `api/push`, `api/audit-logs` | 9 | Notification/push/audit |
| `api/privacy` | 19 | Policy/consent/DSAR/legal hold/worker health |
| `api/projects/{projectId}/webhooks` | 5 | Outbound webhook CRUD/delivery |
| GitHub setup/install/project/management/webhook routes | 12 | GitHub App connection/read-back/write and inbound webhook |
| `api/ai` | 66 | Assistant/session/history/job/action composer/export/usage |
| `api/ai/native-actions` | 7 | Typed P16–P24 draft/review/confirm/read-back |
| `api/ai/project-launch` | 5 | Project Launch plan/review/execute/rollback/monitor |
| `api/erumi-roadmap` | 6 | Roadmap AI preview/apply |

### 2.4 Application/infrastructure services

| Area | Current implementation |
|---|---|
| Identity and authorization | `AuthService`, `UserService`, `OrganizationService`, role rules/catalog/services, `ProjectPermissionRules`, `TaskAccessPolicy`, `GitHubAccessGuard`, `AiNativeAuthorizationService`, `CurrentUserService` |
| Project/work management | `ProjectService`, `TaskService`, `TaskStatusRules`, `TaskSkillService`, `PortfolioScheduleService`, `TimeTrackingService`, `CommentService`, `AttachmentService`, `WikiService`, `ImportService`, `DashboardSummaryService`, `AnalyticsService` |
| Collaboration | `GroupsService`, `GroupAiService`, `GroupAttachmentService`, `MeetingImportService`, `LiveKitTokenService`, notification/email/push/realtime publishers |
| AI orchestration | goal planner, context registry/source guard, session service, bounded loop, gateway/providers, job processor/store/worker, Action Composer/native actions, Project Launch brief/planning/review/execution/rollback/monitoring, safe test orchestrator, quality evaluator and typed contracts |
| Privacy/operations | compliance, consent, DSAR, legal hold, retention worker/store/processor, audit log, health checks, project cleanup/visibility, attention signal, vector sync |
| External | GitHub App/client/webhook worker, outbound webhooks, SMTP, WebPush/VAPID, LiveKit, Redis, SQL Server, Qdrant/Ollama/DeepSeek/OpenAI/Gemini |

### 2.5 Background workers

| Worker | Responsibility | Readiness |
|---|---|---|
| `DatabaseMigrationHostedService` | Applies/validates schema startup | Current source; production migration rehearsal pending |
| `AiJobWorker` | Lease/retry AI job queue | Exclusive renewable lease, expired-owner rejection before and after provider work, bounded shutdown release, real batch scheduling and readiness backlog/lease health; fleet kill replay pending |
| `ProjectOperationMonitorWorker` | Launch execution monitoring/replan signals | Theo dõi cả Project vừa soft-delete để phát replan signal; một baseline lỗi được log/defer riêng và không dừng batch; concurrency giữ state mới hơn; focused recovery test PASS |
| `PrivacyWorker` | Retention/DSAR work | DSAR-first/deadline order, exclusive renewable lease, stale-writer guard before canonical commit, bounded shutdown release, real batch scheduling and readiness overdue/failure health; disabled by default; fleet replay pending |
| `EmailDigestWorker` | Scheduled digest and delivery state | Canonical open-task rules; user inactive hoặc mất quyền Project bị vô hiệu subscription; provider timeout được ghi retry; SMTP failure được propagate nên không có delivered giả. Live SMTP receipt vẫn external-deferred |
| `TaskAttentionSignalWorker` | Derived risk/unseen signals | Stable batched scan runs to exhaustion and closes signals for closed/deleted/unassigned Tasks; production-volume runtime pending |
| `ProjectTrashCleanupWorker` | Retention cleanup for deleted Projects | Physical delete failure preserves canonical Project/file metadata for a later retry; target-storage retention runtime pending |
| `ProjectVisibilityHealthCheckWorker` | Repairs/observes Project-member visibility invariants | Runtime log in tests; production telemetry pending |
| `VectorSyncWorker` | Ordered outbox to vector storage | P026 canonical event/aggregate/sequence migration; exclusive lease + heartbeat, per-aggregate predecessor order, exponential retry/dead-letter, stale-owner guard and bounded shutdown release have Unit + real SQL evidence. Interceptor is disabled with semantic mode so no consumerless queue accumulates. Live Qdrant write/read-back/fleet kill replay remains external-deferred |
| `GitHubWebhookWorker` | Durable GitHub webhook processing | SQL conditional claim + owner/expiry + heartbeat/backoff; stale owner cannot commit processor mutations; expired final attempt is terminalized; cancellation releases via separate bounded context. Live adapter/fleet replay remains external-deferred |
| `WebhookOutboxWorker` | Durable outbound webhook delivery | Conditional claim lặp lại retry/due predicates; host cancellation trả claim bằng recovery context giới hạn hai giây và không ghi false failure; retry/dead-letter/operator read-back đã có source evidence. Live endpoint receipt vẫn external-deferred |
| `WebhookOperationsRetentionWorker` | Webhook operation retention | Xóa delivery history theo policy nhưng giữ dead-letter/outbox cần điều tra; target-volume retention rehearsal còn pending |

### 2.6 Domain/data inventory

| Aggregate | Entities |
|---|---|
| Identity/tenant/RBAC | `User`, `Organization`, `OrganizationMember`, `ModeratorAssignment`, `SystemModulePermission`, `ProjectMember`, `ProjectCustomRole`, `ProjectRoleDefinition`, `ProjectMemberRoleHistory` |
| Project/work | `Project`, `ProjectLabel`, `Sprint`, `TaskItem`, `TaskAssignment`, `TaskDependency`, `TaskLabel`, `TaskAcceptanceChecklistItem`, `TaskSkillRequirement`, `TaskAttentionSignal`, `TaskViewEvent`, `TaskCompletionAttribution`, `TimeEntry` |
| Staffing/skill | `OrganizationSkill`, `OrganizationMemberCapacityProfile`, `MemberAvailabilityWindow`, `ProfessionalProfileDefinition`, `OrganizationMemberProfessionalProfile`; authorization does not consult professional profiles |
| Collaboration/content | `TaskComment`, `TaskAttachment`, `PhysicalFile`, `WikiPage`, `WorkGroup`, `WorkGroupMember`, `GroupMessage`, `GroupMessageUserState`, `GroupAttachment`, `GroupInvitation`, `GroupPoll`, `GroupPollOption`, `GroupPollVote`, `GroupMeetingSession`, `MeetingImport`, `MeetingActionItemMapping`, `ImportSession`, `Vote` |
| Operations | `Notification`, `PushSubscription`, `AuditLog`, `ApiKey`, `WebhookSubscription`, `WebhookDeliveryLog`, `VectorSyncOutbox`, `ProjectDigestSubscription` |
| GitHub | installation, repository connection, commit, PR, review, release, workflow run, webhook inbox and task-development link entities |
| AI/Privacy | job/queue/dispatch/activity/source/provider-attempt/config/cache/usage/audit/draft/session/turn/artifact/process/test-run, Project Launch brief/plan/execution/task-trace/replan, Rulebook/decisions, consent/retention/legal-hold/DSAR entities |

## 3. Main workflow contracts

Every row must ultimately prove the entire chain: persona → route → scope → visible control → client validation/pending → API → authz → rule → transaction/idempotency → mutation → canonical GET → reload/retry → receipt/audit/deep-link.

| Workflow | Canonical success | Required negative/recovery proof | Status |
|---|---|---|---|
| Login/session/profile | Cookie + Redis ticket survives valid reload; disabled/role-changed user is rejected | invalid CSRF, expired/revoked session, inactive user | `DEMO_GATE_PASS` — P04/P05 plus auth/session Integration; target Redis deployment replay deferred |
| Organization/member | Authorized owner/admin creates/updates member and read-back returns exact access role; professional profile assignment returns canonical profile set without changing access role | cross-org ID, non-manager, self-verification, last-owner/successor case, duplicate invite | `DEMO_GATE_PASS` — Profile API/service/client and full regression; exhaustive production persona matrix deferred |
| Platform user/moderator | Admin-only mutation with audit and successor rules | Member/Moderator denied, target isolation | Integration evidence exists |
| Project lifecycle | Create/edit/archive/restore/delete with canonical list/detail read-back | duplicate submit, stale update, forbidden org/project, undo/restore | `DEMO_GATE_PASS` — lifecycle Integration and Project Launch read-back |
| Project members/roles | Built-in/custom access role resolves to bounded base permission | unknown/deleted custom role fails closed for writes; ownership transfer separate | Source/unit/integration evidence |
| Sprint/task/dependency | Valid status/order/dependency/assignee/deadline persists and reloads | cycle, invalid status, stale rowversion, private/restricted deny | `DEMO_GATE_PASS` — P11–P17 plus task/resource/concurrency tests |
| Checklist/skill/evidence | Editable typed draft, one confirm, verified evidence read-back | stale task/source, unauthorized mutation, unverified evidence not promoted | `DEMO_GATE_PASS` — P16/P17 typed renderer plus canonical Integration |
| Workload/capacity/time | Availability and cross-project load minus review/coordination reserve; professional fit is a separate verified signal; no gap-as-capacity | restricted Project names redacted, no-capacity-profile = unknown, unverified profile cannot satisfy skill/capacity, overload blocked/explained | `DEMO_GATE_PASS` — P06–P10 staffing replay and profile/capacity tests; scale qualification deferred |
| Roadmap/Gantt/milestone | Preview → explicit apply → canonical Project/Sprint/Task/dependency read-back | stale preview, cycle, unauthorized apply, reload/undo | `DEMO_GATE_PASS` for P21/P24 native-action flow; exhaustive production UX/load replay deferred |
| Group/chat/poll | Membership-scoped CRUD/vote/realtime | non-member/private deny, duplicate vote, reconnect | `DEMO_GATE_PASS` — P19 plus atomicity/authorization/realtime regression |
| Meeting/transcript/checknote | Authorized transcript import → review → saved checknote/action mappings | privacy/consent/provider failure, reconnect, no fake media success | `DEMO_GATE_PASS` for P20 internal flow; LiveKit production adapter external-deferred |
| Wiki/import/export | Visibility-aware read/edit; import preview/confirm/undo; export contains authorized source only | private/customer deny, bad file/mapping, duplicate confirm, undo after reload | `DEMO_GATE_PASS` for P18 and controlled import/export paths; broad format/provider matrix deferred |
| Settings/API key/webhook/privacy/budget | Scoped config mutation + masked secret/read-back/audit | forbidden role, SSRF/signature, stale policy, worker disabled | Focused source/Unit/Integration/WebFeature gates PASS; external credential/endpoint acceptance deferred |
| Assistant read | Selected scope wins route ambiguity; canonical sources match UI/Analytics | wrong Project, private source, provider failure, session reload | `DEMO_GATE_PASS` — P01–P05/P25–P27 automated plus targeted replay |
| Assistant mutation | Typed editable draft → one explicit confirmation → idempotent transaction → canonical receipt/deep-link | stale draft, duplicate key, unauthorized control, retry/provider failure | `DEMO_GATE_PASS` — P11–P24 automated plus P16–P24 targeted replay |
| Project Launch | ≤3 blocking unknowns → Brief → Rulebook → staffing/delivery review → one confirm → graph read-back | missing skill/capacity, overload, stale source, duplicate confirm, rollback | `DEMO_GATE_PASS` — verified professional profiles, weekly capacity/overhead and canonical graph read-back; production scale/deployment deferred |
| External GitHub/webhook/push/email/media/vector | Adapter write/read-back or honest deferred/disabled state | bad signature/secret, timeout/retry, rate limit, replay | `EXTERNAL_DEFERRED` until configured runtime evidence |

## 4. RBAC and resource policy

### 4.1 Current role taxonomy

| Layer | Current roles | Intended boundary | Gap |
|---|---|---|---|
| System/platform | `Admin`, `Moderator`, `Member` | Platform operations only | Module permission defaults are not a complete role policy; admin bypass needs explicit documented tenant-admin use cases |
| Organization access | `Owner`, `OrganizationAdmin`, `PrivacyOperator`, `BillingAdmin`, `Member`; legacy `Admin`/`Manager` are read-path migration aliases only | Tenant membership, configuration, privacy, billing and management | Unknown/blank writes now fail closed. External users deliberately use Project `Viewer`/`Customer`, not an Organization Guest role. Moderator is a separate scoped platform delegation. |
| Project access | `Owner`, `Manager`, `ScrumMaster`, `Developer`, `Tester`, `Reviewer`, `Member`, `Viewer`, `Customer`, plus custom role inheriting one built-in base | Resource access and mutation | Custom role `SkillTags` currently mixes staffing hints into an access-role definition |
| Resource policy | owner/manager, member, assignee/reporter, private/restricted/customer visibility, group membership, moderator assignment | Specific object-level allow/deny | Demo resource families now have the focused matrix below; exhaustive production endpoint certification remains post-thesis work |

### 4.1.1 Organization access and delegated Moderator matrix

Legend: `M` native Organization manager, `S` exact delegated scope, `R` member read/self-service, `—` deny. Professional Profiles describe vocational fit and never grant any access shown here.

| Actor | Member directory | Invite | Change access role | Remove member | View professional profiles | Manage/verify professional profiles |
|---|---:|---:|---:|---:|---:|---:|
| System Admin / Organization Owner / OrganizationAdmin | M | M | M | M | M | M |
| PrivacyOperator | R | — | — | — | R | — |
| BillingAdmin | R | — | — | — | R | — |
| Member | R | — | — | — | R | self-declare only |
| Platform Moderator without assignment | — | — | — | — | — | — |
| Platform Moderator with assignment | `organization.users.view` | `organization.users.invite` | `organization.users.update_role` | `organization.users.remove` | `organization.professional_profiles.view` or `.manage` | `organization.professional_profiles.manage` |

Assignments are Organization-specific, active, non-revoked and time-bounded. Empty assignment sets and unknown capabilities fail closed. Native Organization authority is evaluated separately, so an actual Organization Owner/Admin does not depend on a Moderator assignment. The client consumes the same exact-capability model and no longer treats an empty capability list as unrestricted access.

### 4.2 Effective project capability matrix

Legend: `M` manage/all; `W` scoped write; `R` read-only; `S` shared/customer-visible only; `—` deny. Exact resource privacy rules can further reduce access.

| Role | Project/member | Task create | Own task/comment/time | Review evidence | Internal Wiki | Integration | AI |
|---|---:|---:|---:|---:|---:|---:|---|
| System Admin / Project Owner / Manager / ScrumMaster | M | W | W | W | R/W | M | Full, subject to privacy/budget |
| Developer | R | W | W | — | R/W | — | Specialist |
| Tester | R | W | W | W | R/W | — | Specialist |
| Reviewer | R | W | W | W | R/W | — | Specialist |
| Member | R | — | W | — | R/W | — | Contributor/read |
| Viewer | R | — | — | — | R | — | Read-only |
| Customer | S | — | — | — | shared only | — | Read-only on shared sources |
| Non-member | — unless Organization policy grants read | — | — | — | — | — | None |

### 4.3 Required deny-wins evaluation order

1. authenticated, active principal;
2. platform explicit deny/module deny;
3. tenant membership and active Organization;
4. Project/object belongs to tenant and is active/visible;
5. private/restricted/customer/resource-specific deny;
6. custom access role resolves to an active built-in base role;
7. action permission and ownership/assignee/reviewer condition;
8. AI privacy/budget/provider/source policy;
9. confirmation, freshness, rowversion and idempotency contract.

The backend must execute all applicable steps. Frontend visibility must consume server-returned capabilities and fail closed when they cannot be loaded; it must never independently widen access.

### 4.4 Graduation-demo resource boundary evidence

| Demo resource family | Positive contract | Required negative contract | Current evidence |
|---|---|---|---|
| Project/Sprint/workload | authorized manager/member receives only canonical Project rows and valid Sprint mutations persist atomically | unrelated/private tasks excluded; Customer internal workload denied; invalid/foreign Sprint rejected without partial assignment | `ProjectResourceBoundaryIntegrationTests` PASS |
| Task/comment/time/attachment | permitted assignee/reporter/contributor operations persist and read back | former member, Viewer/Customer, unrelated private Task and foreign Project fail closed | `TaskAccessPolicyIsolationTests`, `TimeTrackingAccessPolicyTests`, `AttachmentServiceAuthorizationTests`, `NotificationAndWikiPolicyTests` PASS in authorization Unit batch `54/54` |
| Wiki/Search | public/internal/own-private visibility matches canonical Wiki policy | Customer sees only shared content; another author's private Wiki never appears in Search | `SearchBoundaryIntegrationTests` `3/3` plus Wiki policy Unit PASS |
| Group/Poll | group roles can read, create/vote/close only within their declared capability | non-member, ordinary member mutation and invalid/duplicate vote paths fail without canonical side effects | `GroupsServiceTests` included in Group/Poll/Meeting Unit batch `121/121` |
| Meeting/transcript/action mapping | starter or Group manager may attach summary/transcript; authorized Project manager may map/create selected action items | ordinary Group member cannot overwrite another starter's meeting; Project outsider cannot read/link/create and database remains unchanged | `GroupAiServiceTests` and `MeetingActionItemsControllerTests` `13/13` PASS |
| AI source/action | authorized selected scope returns typed source/card and confirmed canonical mutation | foreign Project, Member mutation, stale source, duplicate idempotency and retired bypass endpoints fail closed | AI P06–P28 Integration `72/72` PASS |
| Directory/vote/navigation support | authorized collaborator directory and writable member vote return canonical state | ordinary member cannot enumerate system roles; Viewer/Customer cannot vote | `UserDirectoryBoundaryIntegrationTests`, `VoteBoundaryIntegrationTests` PASS |

This closes the focused demo boundary inventory, not an assertion that every peripheral production endpoint has undergone independent certification. Final persona/browser replay remains required before D2 is accepted.

## 5. Professional profile and skill matrix

### 5.1 Implemented boundary (focused evidence PASS)

The working tree now implements a separate many-to-many Organization Professional Profile layer. `OrganizationMember.Role` remains access-only. `ProfessionalProfileDefinition` and `OrganizationMemberProfessionalProfile` carry taxonomy, proficiency, effective dates, source, verification/audit metadata and rowversion. Members may self-declare but cannot self-verify; Organization managers verify/reject. Staffing reads only active, verified, time-valid profiles as vocational fit while still requiring canonical task skill evidence, declared availability, cross-Project load and Rulebook constraints. Neither module authorization nor Organization/Project resource authorization reads these profiles.

### 5.2 Target model (does not grant access)

| Object | Required fields/rules |
|---|---|
| `ProfessionalProfileDefinition` | OrganizationId, stable key, localized name/description, category, active/version; built-in taxonomy plus Organization custom definitions |
| `OrganizationMemberProfessionalProfile` | OrganizationId, UserId, ProfileId, proficiency, effective dates, source, verification status/by/at, rowversion |
| Member skill evidence link | SkillId, proficiency, evidence source/ref, verification, expiry/freshness; private evidence redacted |
| Capacity | weekly declared hours, availability windows, timezone, source/effective period |
| Workload | cross-Project allocated hours with restricted-source redaction |
| Overhead | reviewer and coordination reserve as explicit Rulebook inputs |

Baseline taxonomy has 20 seeded definitions: Product/Project Manager; Business Analyst; Scrum Master/Agile Coach; UX/UI Product Designer; Frontend/Backend/Full-stack/Mobile Engineer; QA Manual/Automation/Test Lead; DevOps/SRE; Cloud/Platform; Security; Data Engineer/Analyst; AI/ML Engineer; Solution/System Architect; Technical Writer; Customer Success/Support; plus custom Organization definitions.

Staffing may use this evidence. Authorization may not.

## 6. AI capability by role/scope

| Effective tier | Read/analyze | Draft | Confirm mutation | UI controls/navigation |
|---|---|---|---|---|
| Full manager | Authorized workspace/Organization/Project/resource sources | All registered typed drafts in scope | Only mutations also allowed by canonical domain policy | Show review/confirm/deep-links; hide forbidden resources |
| Specialist | Authorized Project/resource sources | Task/checklist/breakdown/wiki/poll/meeting drafts where resource policy permits | Own/specialist actions only; no Project/member/integration management | Helpful analysis plus only permitted edits |
| Contributor | Authorized Project/own/shared resources | Non-privileged content/own-task drafts | Only explicit allowed own-resource mutations | No manager controls; navigation to own actionable work |
| Read-only/customer | Authorized/redacted/shared reads | No domain mutation draft | Never | Metrics/tables/cards and safe deep-links only |
| Restricted/none | No source envelope | No | No | Explain unavailable capability without leaking object existence |

Cross-cutting contracts:

- selected Project scope updates the same durable session immediately;
- server session owns history, title, decision summary, answers and scope across reload;
- provider cannot grant a capability; reconciliation uses registered server policy;
- all writes have typed editable artifacts, a single explicit confirmation, idempotency and canonical read-back;
- member still receives useful authorized analysis without out-of-role mutation controls;
- fallback reports actual `Qaly/qaly-native/server_fallback` (or actual adapter) and never claims an unverified write;
- external calendar/repository/invitation/webhook/deployment states remain honest `disabled`, `unconfigured`, `cached` or `EXTERNAL_DEFERRED` until adapter read-back.

## 7. UI state matrix

| Surface family | Required states | Current evidence/status |
|---|---|---|
| App route/page shell | initial/loading/empty/error/unauthorized/partial/ready; retain selected scope | `DEMO_GATE_PASS` on scripted surfaces; all-page production matrix deferred |
| CRUD modal/form | labels/descriptions/examples, required/optional, taxonomy picker + Other, inline actionable errors, pending/dedup, retained input, cancel/Escape/focus return | `DEMO_GATE_PASS` for runbook forms/cards; peripheral-form WCAG/device certification deferred |
| Disabled/blocked control | reason visible on hover/click and link/focus to exact blocker | `DEMO_GATE_PASS` for Project Launch and typed AI cards; repository-wide production certification deferred |
| Drawer/modal | dialog semantics, focus trap, Escape, scroll lock, focus restoration, responsive/zoom/dark contrast | `DEMO_GATE_PASS` on Assistant/roadmap/shared confirm surfaces; full modal/zoom/screen-reader matrix deferred |
| Table/chart/metric | unit/source/scope, unambiguous subset relationships, responsive overflow, tooltip details, empty chart suppression | `DEMO_GATE_PASS` — P03 renderer and Analytics/Assistant canonical parity |
| Long-running AI/import/integration | queued/running/retryable/failed/canceled/completed/verified; reload persistence | `DEMO_GATE_PASS` for session/job/import contracts; production soak/fleet shutdown deferred |
| Destructive/archive/undo | clear impact, explicit confirmation, recoverability window and canonical read-back | Project/import canonical paths PASS; exhaustive production role/reload matrix deferred |

Page-specific source heuristics that require focused inspection/replay rather than an automatic FAIL: `WikiDetailPage` and `ProfilePage` do not expose a conventional page-level loading/error vocabulary; `AnalyticsPage` delegates most states to the shared Assistant renderer; `GroupPollPage` delegates poll states to `PollCard`.

## 8. Gap register

| ID | Sev | Root cause and impact | Concrete evidence | Remediation | Regression gate | Status |
|---|---|---|---|---|---|---|
| PR-RBAC-001 | P1 | Professional capability previously was not independent from access role. | Separate domain/config/DTO/service/API/UI; 20-profile migration baseline; 27 verified idempotent demo assignments; staffing consumes verified/time-valid profile fit without granting access; exact Moderator view/manage scopes | Restored production-like SQL rehearsal remains post-thesis | Unit service `7/7`; API positive/negative `3/3`; client drawer/access policy focused PASS; P06–P10 staffing `1/1`; full Unit/Integration/WebFeature batches PASS | DEMO_GATE_PASS_PRODUCTION_DEFERRED |
| PR-RBAC-002 | P1 | System module/AI permission was an override table without a complete default role policy. Missing/unknown records and frontend modules failed open; Roadmap and role-conflict checks resolved different scopes. | Central `SystemModuleAuthorizationService`, effective-current-user endpoint, route/sidebar consumption, scope constraints/migration; focused Unit/API/client evidence | Production restore/migration and exhaustive persona/browser matrix remain post-thesis | Unit matrix for Admin/Moderator/Member/legacy User/unknown + override deny; API positive/negative; full regression PASS | DEMO_GATE_PASS_PRODUCTION_DEFERRED |
| PR-RBAC-005 | P0 | View-as previously changed only `UserId`, retained the Admin role, ran after authorization, used fake client IDs and was not attached to API requests. | Middleware now resolves an active real user, replaces the full principal before authorization, retains real actor id, rejects all mutation methods; client uses real users/session header and discloses read-only mode | Exhaustive browser persona matrix remains post-thesis | Integration identity/admin-deny/mutation-deny `3/3`; full auth boundary and full regression PASS; P25 read-only renderer replay PASS | DEMO_GATE_PASS_PRODUCTION_DEFERRED |
| PR-RBAC-003 | P1 | Organization role and Moderator delegation previously diverged: unknown roles could normalize broadly, empty client scopes implied every control, and professional-profile delegation was absent. | Explicit taxonomy/matrix; unknown/blank writes fail closed; legacy Manager/Admin retained only as read aliases; exact six-scope Moderator contract; backend/UI deny-by-default; CSRF on Organization and assignment mutation routes | Demo full regression and targeted role surfaces completed; exhaustive production persona/browser matrix remains post-thesis | Focused role/profile Unit matrix `58/58`; Organization/profile/Rc integration `20/20`; client access/profile/permission/api `45/45`; full regression PASS | DEMO_GATE_PASS_PRODUCTION_DEFERRED |
| PR-RBAC-004 | P1 | Resource policies are distributed; deny-wins previously lacked a focused matrix for every Project/Task/Wiki/Group/Meeting/AI source family used by the demo. | Added section 4.4 inventory; Project/Sprint, Task, Wiki/Search, Group/Poll, Meeting, directory/vote and AI source/action positive/negative cases. Meeting summary/transcript mutation was tightened to starter-or-manager, and Project outsiders are denied read/link/create with zero rows changed. | Focused demo matrix and targeted replay completed; exhaustive peripheral endpoint certification remains post-thesis. | Boundary Integration `34/34`; authorization Unit `54/54`; Group/Poll/Meeting Unit `121/121`; AI P06–P28 Integration `72/72`; full regression PASS | DEMO_GATE_PASS_PRODUCTION_DEFERRED |
| PR-AI-001 | P1 | Full integration gate had one loaded-host timing failure while durable cancel/reload/resume focused replay passed. | Scheduler assertion retained and the current expanded Integration suite is green | Full suite rerun completed | Full Integration `255/255 PASS` | CLOSED_DEMO_GATE |
| PR-AI-002 | P2 | Two stale unit fixtures contradicted current honest chart/fallback contracts. | Fixtures now include real chart series and assert structured Qaly fallback metadata | Full suite rerun completed | Full Unit `773/773 PASS`; P26 browser fallback badge PASS | CLOSED_DEMO_GATE |
| PR-OPS-001 | P2 | Realtime publisher previously started fire-and-forget `Task.Run`, discarded caller cancellation/delivery completion, and contained an unused throwing `IsDuplicate`. | `SignalRNotificationPublisher.cs` | Publisher now awaits distributed dedupe and delivery, retries three times, releases the dedupe key after failed delivery, and honors cancellation before work starts | Focused realtime + dashboard/audit boundary integration `11/11 PASS`; full fleet shutdown/soak replay remains post-thesis | DEMO_GATE_PASS_PRODUCTION_DEFERRED |
| PR-CONFIG-001 | P1 | Default production config disables AI Native and Privacy V4 and uses wildcard hosts/placeholders; development includes known demo credentials. This is safe only with an explicit deployment profile and secret injection. | `src/Qaly.Web/appsettings*.json`, environment overrides | Production now fails fast unless DB/Redis, exact hosts, HTTPS public/invitation URLs, deployment id, absolute durable Data Protection key path, explicit AI/privacy decision, operational thresholds/retention, account throttle limits, bounded host shutdown, direct/trusted-proxy topology and enabled integration credentials are concrete and internally consistent; enabled GitHub also requires bounded poll/batch/attempt/lease/heartbeat/backoff values; demo seed/passwords are rejected | Validator/lifecycle contract `33/33 PASS`; config/parity source gate PASS. Demo uses the declared local profile; live secret-injected deployment rehearsal is `DEFERRED_POST_THESIS` | DEMO_GATE_PASS_PRODUCTION_DEFERRED |
| PR-SEC-APIKEY-001 | P0 | API Key create/revoke and `LastUsedAt` previously updated only the EF change tracker; cookie remained the default authenticate scheme and issued scopes were not enforced. This allowed false-success mutation and, if authenticated explicitly, role-wide access beyond the selected scope. | `ApiKeyService`, authentication handler/scheme, pipeline and old scope-less UI | Canonical SaveChanges; forwarded bearer authentication; fail-closed route/method scope middleware; API-key view-as denial; bearer-aware CSRF policy; supported-scope/expiry/key-count validation; unique hash migration; guided typed UI | Service persistence/validation tests; handler LastUsed read-back; exact/missing/unpublished/nested/view-as scope tests; full regression + Web/client builds PASS; deployed secret rotation/independent review deferred | DEMO_GATE_PASS_PRODUCTION_DEFERRED |
| PR-SEC-AUTH-001 | P0 | Controller authorization was convention-based, so a new action could accidentally become public without a failing gate. | `ControllerAuthorizationContractTests` reflects the compiled Web assembly, requires an explicit authentication boundary on every HTTP action and locks anonymous actions to a reviewed allowlist. | Keep the allowlist minimal; signed webhook verification and endpoint-specific resource authorization remain independently required. | Contract `2/2`; full Integration `288/288`; solution build `0 warning / 0 error` | DEMO_GATE_PASS_PRODUCTION_DEFERRED |
| PR-OPS-CORRELATION-001 | P1 | Untrusted or duplicate `X-Correlation-Id` values could previously be reflected into the response/log, and request-completion logging executed outside the correlation context. | Middleware now accepts exactly one trimmed ASCII-safe value up to 64 characters, otherwise creates a server id; it sets `TraceIdentifier`, overwrites one response header and runs before Serilog request logging. | Keep target log sampling and distributed trace correlation in the post-thesis operations gate. | WebFeature positive/invalid/oversize/multi-value `4/4`; full WebFeature `49/49`; build clean | DEMO_GATE_PASS_PRODUCTION_DEFERRED |
| PR-EXT-WEBHOOK-001 | P1 | Webhook test previously returned success without checking delivery, exposed unsupported events, and Task committed before outbound dispatch. | Task mutations now transactionally enqueue an occurrence-scoped durable outbox row; a multi-instance-safe lease worker retries after restart, preserves per-occurrence idempotency, records canonical delivery logs and dead-letters after the retry ceiling. Authorized operator UI/API provides redacted inspection, idempotent replay/audit, health thresholds and retention that preserves dead-letters. | Demo must show the honest disabled/degraded state and canonical local receipt. Configured live endpoint read-back plus external alert delivery remain post-thesis. | Focused outbox/retention `6/6`; health/operator WebFeature `4/4`; live adapter remains `EXTERNAL_DEFERRED` | DEMO_GATE_PASS_PRODUCTION_DEFERRED |
| PR-DATA-001 | P1 | Rich demo seed previously lacked professional-profile assignments and did not have one executable manifest proving the graduation scenarios. | Idempotent baseline for every active Organization plus 27 verified demo assignments across all 12 members; access roles remain unchanged. The seed manifest proves Sprint/dependency/workload, skills/evidence/capacity, Wiki/Poll/Meeting and read-only personas; live P16/P17 replay resolved a managed seeded Project/Task without database repair. | Keep the idempotent seed; production-like restore rehearsal remains post-thesis. Never destructively reset user data. | `RichDemoSeedTests 3/3 PASS`; full Unit/Integration/WebFeature PASS; P16/P17 live seed replay PASS | CLOSED_DEMO_GATE |
| PR-DATA-ATOMIC-001 | P1 | Three required graphs were split across commits: Organization before owner membership, Group before owner membership, and Meeting session before its canonical message. A second-save failure could leave an unusable or invisible partial aggregate. | Each graph is staged through its repositories and persisted by one UnitOfWork save; result/read-back still resolves the canonical aggregate. | Cross-service AI/provider state machines retain intentional phase commits with durable running/failed/verification-pending states. Target crash/process-kill injection remains post-thesis. | Atomic save-count/read-back plus audit fault-injection Unit `7/7`; full Unit `785/785` | CLOSED_DEMO_GATE |
| PR-DATA-AUDIT-ATOMIC-001 | P0 | Canonical mutation was often committed before `AuditLogService.LogAsync`; an audit failure could return HTTP 500 after data/outbox had already changed, inviting a duplicate retry and leaving no matching audit row. | Added non-committing `StageAsync` plus shared `SaveChangesWithAuditAsync`; migrated Organization, Project, Group/Meeting/Poll, Task/Sprint, Wiki, Comment, Attachment, role/profile/skill, capacity/portfolio, meeting import, webhook replay, AI job/draft/roadmap and admin scope mutations. Batch Task operations stage every audit before one save; explicit AI phase commits remain recoverable state-machine boundaries. | Login/session and read-only AI usage telemetry remain intentionally best effort; production crash/process-kill verification and retention/alerting remain post-thesis. | Audit-stage rollback Unit `4/4`; staging visibility Integration `1/1`; source scan has no active mutation pattern `SaveChanges → LogAsync`; full Unit `785/785`, Integration `288/288`, WebFeature `49/49`; build clean | CLOSED_DEMO_GATE |
| PR-NOTIF-001 | P1 | Notification list only scanned the newest 100 rows before authorization filtering and unread count resolved every row separately; hidden recent records could mask visible older records and large inboxes caused unbounded N+1 access checks. | Stable batched paging continues until 50 authorized rows or exhaustion; unread counting is exact over all authorized batches; resolver preloads target graphs and recognizes active custom Project manager roles consistently with Task policy. | Production-volume query-plan and alert/retention qualification remain post-thesis. | Notification/policy focused `20/20`; full Unit `785/785`; Integration `288/288` | DEMO_GATE_PASS_PRODUCTION_DEFERRED |
| PR-AUDIT-001 | P1 | Recent workspace activity scanned only 500 newest audit rows and resolved access per row; noisy foreign-tenant history could hide older authorized activity and create N+1 queries. | Stable 200-row paging continues until the requested authorized limit or exhaustion; Task/Sprint/Project targets and Project access graphs resolve in batches; invalid JSON metadata fails closed while an actor still sees their own log; staged rows are invisible to a separate context until the shared commit. | Production-volume query-plan, audit retention and target log sampling remain post-thesis. | Cross-tenant + 520-hidden-row + staging Integration `3/3`; full Integration `288/288` | DEMO_GATE_PASS_PRODUCTION_DEFERRED |
| PR-UI-001 | P1 | Repository-wide disabled-control → reason → exact blocker navigation contract is not yet proven. A meeting control also conflated “leave” with privileged “end for everyone” and previously swallowed end failures after clearing local recovery state. | Vue source inventory, Project Launch runtime reports, `GroupMeetingPage`/`MeetingControls`, P16–P27 targeted replay | Meeting actions are separated; runbook typed controls expose exact blockers/targets; canonical meeting end is required before success. Exhaustive non-demo forms remain post-thesis. | Meeting RBAC Unit PASS; P16–P27 browser replay `3/3 PASS`; typecheck/build PASS | DEMO_GATE_PASS_PRODUCTION_DEFERRED |
| PR-UI-002 | P2 | Accessibility/zoom/dark/responsive evidence is incomplete across all 17 pages and 73 components. | Targeted demo surfaces passed the final bounded Chromium replay | Full 200% zoom, screen-reader, dark-mode and breakpoint matrix across every surface is deferred. | Demo replay PASS; full WCAG/device matrix is tracked in backlog | DEMO_GATE_PASS_PRODUCTION_DEFERRED |
| PR-PERF-001 | P2 | Known EF Skip/Take warning paths are ordered and multi-collection hotspots split, but all Includes have not been query-plan/load-tested. | Every runtime page has a unique `Id` tie-breaker; important capped queries are deterministic; SQL Server uses `SplitQuery`; redundant per-Task authorization queries were removed from list/detail/Kanban after the canonical visibility filter; notification and recent-audit authorization resolve per stable batch instead of per row. | Exhaustive query-plan, command-count budget, concurrency and load qualification is deferred. | Focused query/service Unit `130/130`, Integration `24/24`, split registration `1/1`, Task visibility `6/6`, notification/policy `20/20`, recent audit `3/3`; full Unit `785/785`, Integration `288/288`, WebFeature `49/49`; build clean | DEMO_GATE_PASS_PRODUCTION_DEFERRED |
| PR-PERF-002 | P1 production gate | The old load smoke measured `Start-Job` startup against health only, omitted p50/p99/error/throughput thresholds and produced no durable report; host had no explicit shutdown budget and several workers could classify host cancellation as provider/worker failure. AI/Privacy also exposed an unused batch setting, could keep processing after lease loss, had no readiness check, and retention could starve statutory DSAR work. Task attention silently stopped after 500 rows, storage-delete failure could make trash cleanup lose its retry metadata, outbound webhook could retain a claim on shutdown, SMTP failure could become delivered giả, and one poisoned Project monitor row could stop its batch. | PowerShell 7 probe now has warmup, secret-safe auth, percentiles/throughput/error thresholds and durable reports. AI/Privacy/GitHub/Vector/outbound webhook propagate cancellation and recover ownership; AI/Privacy re-check lease before commit, process bounded batches, expose readiness signals, and Privacy selects DSAR before retention by earliest legal deadline. Privacy operator health matches readiness; task attention runs stable batches to exhaustion; trash cleanup preserves metadata; digest delivery propagates SMTP failures and retries provider timeout; Project monitoring observes soft-delete and isolates/defer failures per execution. | Representative authenticated traffic mix, long soak, multi-instance lease takeover under process loss, SIGTERM/load-balancer drain and capacity report must run on the release candidate. | Focused affected worker/processor/health Unit `28/28`; Privacy API `4/4`; AI/Privacy validator + real SQL `73/73`; auth boundary focused Integration `12/12`; full client `250/250`, Unit `821/821`, Integration `331/331`, WebFeature `49/49`; solution build `0 warning / 0 error` | SOURCE_HARNESS_PASS_RUNTIME_PENDING |
| PR-EXT-001 | P1 production gate | GitHub, SMTP, WebPush, LiveKit, Qdrant and external webhooks lack configured live adapter write/read-back in this audit. | default disabled/placeholders; tests use controlled adapters | Preserve honest disabled/degraded states; never claim external success. Configured live adapter acceptance is post-thesis work. | Demo disclosure plus controlled-adapter evidence; live read-back later | DEFERRED_POST_THESIS |
| PR-LEGAL-001 | P2 production gate | Test output reports Fluent Assertions commercial-license requirement; legal suitability is not code-tested. | current test runner warning | Record the warning for the thesis build; confirm license or replace dependency before commercial production. | Dependency/license review record | DEFERRED_POST_THESIS |

## 9. Persona demo matrix

| Persona | Must complete | AI assistance | Must be denied | Seed needed |
|---|---|---|---|---|
| Platform Admin | User/moderator admin, health/audit, simulation with disclosure | Diagnose authorized platform state; navigate to exact controls | Silent tenant impersonation; unlogged destructive action | active/inactive users, moderator scopes, audit events |
| Organization Owner/Admin | Organization members/access policy, Project portfolio, Rulebook | Portfolio analysis, staffing/project launch, settings drafts | Other Organization/private resources | multi-Project load, skills/evidence/capacity/profiles |
| Privacy Operator | Policy/consent/DSAR/legal hold | Explain privacy state and guide authorized operations | Billing/user/project mutation outside role | consent/hold/DSAR pending and completed |
| Billing Admin | AI budget/usage settings | Budget analysis and safe model guidance | Member/Project content beyond access | budget thresholds/usage/provider attempts |
| Project Manager/ScrumMaster | Sprint/task/member/roadmap/workload/wiki/integration management | Risk, replan, Project Launch, bulk typed drafts | Cross-Project/private tenant resources | overloaded/unassigned/blocked/dependency/deadline cases |
| Developer/Tester/Reviewer | Assigned/own tasks, comments/time/evidence/review | Draft task/checklist/breakdown, analyze authorized workload | Project/member/integration admin | role-appropriate tasks and verified skill evidence |
| Member | Own/shared work and useful Project analysis | Read/draft within allowed scope; direct navigation | Manager mutation controls | own/unassigned/shared/private-negative tasks |
| Viewer/Customer/Guest target | Shared/read-only Project data | Helpful read-only answer/cards | Any write; internal/private data | customer-visible Wiki/task plus internal negative case |

## 10. Accelerated stop-the-line execution plan

1. **Freeze scope and baseline:** preserve current evidence, record the working tree, and classify every open item as demo gate or `DEFERRED_POST_THESIS`.
2. **Close P0/P1 demo boundaries:** tenant/private denial, wrong scope, role conflict, fake success, duplicate mutation, stale draft and dead-end in the scripted workflows.
3. **Close the main graduation story:** login → Dashboard/Analytics → Project Launch P06–P10 → Task P11–P17 → Wiki/Poll/Meeting/Roadmap/Digest/Skill/Replan P18–P24 → role/fallback/renderer/navigation P25–P27. P28 remains an honest external boundary unless a live adapter is supplied.
4. **Verify RBAC and AI personas:** Admin, Organization Owner/Admin, Project Manager, Developer/Tester and Member each get the right data, controls, denial and AI capability; professional skill roles never grant access by themselves.
5. **Make demo data deterministic:** one reset/seed path must produce all positive, negative, overloaded, unassigned, private and dependency cases used by the runbook.
6. **Polish only demo surfaces:** clear labels, structured cards/tables, loading/empty/error states, disabled-control reason and exact blocker navigation at the presentation resolutions; no clipped composer/modal or keyboard trap.
7. **Run verification once in batches:** focused tests while fixing; then full client/backend tests, typecheck/build once; finally one targeted browser replay with screenshots, reload and canonical read-back.
8. **Publish thesis evidence:** mark each scripted case PASS/FAIL/BLOCKED/NOT_VERIFIED, list exact deferred production work, and set `DEMO_READY_THESIS` only when every gate below passes.

## 11. `DEMO_READY_THESIS` acceptance gates

| Gate | Required evidence | Current status |
|---|---|---|
| D1 Build/static | frontend unit + typecheck + production build; backend Unit/Integration/WebFeature build/test | `PASS` — client `270/270`; Unit `821/821`; Integration `331/331`; WebFeature `49/49`; typecheck/build PASS; solution `0 warning / 0 error` |
| D2 Tenant/RBAC | Positive/negative coverage for every resource family touched by the demo; deny-wins; no view-as mutation | `PASS` — focused deny-wins/resource matrices plus full automated batches; P25 read-only surface has no mutation draft/control |
| D3 Main workflows | Scripted Project, Sprint, Task, Wiki/Poll/Meeting/Roadmap/Digest/Skill/Replan flow; no dead-end | `PASS` — canonical Integration evidence plus final P16–P24 typed-card replay |
| D4 AI role/scope | Project switch/session reload, correct source/capability/control, provider fallback truth, no out-of-role mutation | `PASS` — session/scope Integration evidence plus P25–P27 fallback/renderer/navigation replay |
| D5 Demo data | Idempotent reset/seed covers each persona and positive/negative scenario without manual database repair | `PASS` — D01–D05 manifest `3/3`; full tests green; live P16/P17 found the managed seeded Project/Task without repair |
| D6 Demo UI/UX | Runbook pages/popups have clear states, blocker navigation, keyboard/focus and fit target presentation resolutions | `PASS` — P16–P27 targeted Chromium replay `3/3` across live prompts and persisted typed UI matrices |
| D7 Mutation integrity | Confirm contract, idempotent retry, no stale/duplicate write and canonical read-back after reload | `PASS` — native action Integration `9/9`; SQL migration/retry subset `15/15`; Poll/message atomicity and Roadmap no-selection rejection asserted |
| D8 Final evidence | One targeted browser replay plus thesis acceptance report with per-case status and screenshots/receipts | `PASS` — this file is the acceptance record; browser replay completed, with one harness-only value assertion corrected then the previously unverified P18–P27 slice rerun `2/2 PASS` |

`DEMO_READY_THESIS` does not mean `PRODUCTION_READY`. External adapters and production-only operations may remain `DEFERRED_POST_THESIS` only when the UI and report describe that boundary truthfully and do not emit fake success. No historical `PARTIAL` or `PASS` label overrides D1–D8.

## 12. Evidence log

| Time (+07) | Change/evidence | Result |
|---|---|---|
| 2026-09-01 | Locked branch/commit/worktree; inventoried routes/pages/components/controllers/entities/services/workers/integrations | Baseline recorded |
| 2026-09-01 | `npm run test:unit` | `244/244 PASS` |
| 2026-09-01 | Full backend Unit | `658/660 PASS`; two contract fixtures identified as stale |
| 2026-09-01 | Corrected chart fixture to include a real workload series; corrected provider-failure expectation to structured honest server fallback | Focused `2/2 PASS` |
| 2026-09-01 | Full Integration | `202/203 PASS`; loaded-host scheduling window missed durable-running poll |
| 2026-09-01 | Focused cancel/reload/resume replay | `1/1 PASS`; durable snapshot, cancel conflict and resumed completed turn verified |
| 2026-09-01 | Extended only the loaded-host scheduler polling window; retained the durable-persistence assertion | Full rerun pending |
| 2026-09-01 | Replaced unsafe post-authorization ID-only Simulation with a pre-authorization, full-principal, real-user, read-only view-as boundary | Focused Simulation `3/3 PASS`; full auth boundary `10/10 PASS`; Web build `0 warning/0 error` |
| 2026-09-01 | Added explicit System module defaults, legacy-role normalization, user-over-role specificity with deny-wins per scope, effective permission API, strict update validation, DB one-scope/unique constraints and fail-closed navigation | RBAC/System permission focused Unit `26/26 PASS`; client permission `12/12 PASS`; typecheck PASS; full suite/migration rehearsal pending |
| 2026-09-01 | Added independent Organization Professional Profile taxonomy/assignment entities, integrity constraints, canonical service/API, audit/concurrency, 20-profile baseline for existing/new Organizations, idempotent verified demo profiles and a guided picker drawer | Profile/RBAC focused Unit matrix included in `36/36 PASS`; Profile API `2/2 PASS`; client focused `41/41 PASS`; rich seed/profile focused `7/7 PASS` |
| 2026-09-01 | Connected Project Launch staffing to active verified professional profiles for manager fit while preserving task-derived skill evidence, weekly capacity, load, reserve and Rulebook hard gates | Focused P06–P10 Project Launch replay `1/1 PASS`; verified profile source/decision asserted; full AI batch pending |
| 2026-09-01 | Closed Organization taxonomy/delegation fail-open paths: strict role normalization, exact Moderator scopes including separate professional-profile view/manage, backend time/scope checks, shared client access policy, and CSRF on Organization/delegation mutations | Focused Unit `58/58`, Integration `20/20`, client `45/45`, typecheck PASS; one `undefined` owner comparison widening bug was caught by the new client negative test and corrected |
| 2026-09-02 | Replaced API Key false-success/unbounded-role behavior with canonical create/revoke/LastUsed persistence, supported-scope normalization, bearer forwarding, fail-closed route+method scope enforcement, API-key view-as denial, bearer-aware CSRF, unique key-hash migration and a typed scope/expiry/status UI | Focused service `6/6 PASS`; authentication/scope boundary `3/3 PASS`; Web build and client typecheck PASS; restored P022 data-cleaning migration and added P023 unique hash index; full regression/migration rehearsal pending |
| 2026-09-02 | Made outbound webhook configuration and test truthful: only published Task events can be selected, test delivery returns saved receipt/attempt/status/log id, failed endpoints return 502 instead of a success toast, and duplicate confirmed payloads reuse the canonical delivery log | Focused webhook service/publisher `6/6 PASS`; client typecheck PASS. Durable post-commit dispatch remains explicitly open as `PR-EXT-WEBHOOK-001`; live adapter remains `EXTERNAL_DEFERRED` |
| 2026-09-02 | Added a fail-fast Production configuration gate and configurable durable Data Protection key path; wildcard hosts, in-memory/demo seed, bootstrap passwords, placeholder infrastructure/secrets, inconsistent AI/privacy decisions and partially configured GitHub/Push/LiveKit now prevent startup | Production validator contract `4/4 PASS`; Web build PASS; real deployment rehearsal remains pending |
| 2026-09-02 | Split meeting “leave” from privileged “end for everyone” and removed the swallowed-error path: only starter/Owner/Admin gets the end control, server `EndedAt` is required before success, remote end does not issue a duplicate mutation, and recoverable failure preserves session/transcript | Group meeting end RBAC Unit `3/3 PASS`; client typecheck PASS; Web build 0 warnings |
| 2026-09-02 | Replaced post-commit Task webhook dispatch with migration P024 durable outbox: Task + event commit together; occurrence-scoped idempotency, multi-instance lease, restart retry and dead-letter behavior are explicit | Focused webhook/outbox/Task atomicity `18/18 PASS`; P023→P024 SQL script generated; configured live endpoint replay remains external-deferred |
| 2026-09-02 | Made Poll creation atomic with its canonical GroupMessage, changed the client to reload canonical messages after the realtime event and removed the client-side compensating delete path | Focused Poll/outbox/Task batch `29/29 PASS` |
| 2026-09-02 | Made AI clarification drafts fail safe: pending saves flush before submit, failed canonical persistence is visible and blocks submission instead of silently continuing | Client typecheck and Web build PASS |
| 2026-09-02 | Aligned Search with Wiki private-resource policy so a Project member can see own private Wiki content but not another author's private content | Search boundary Integration `3/3 PASS` |
| 2026-09-02 | Revised the acceptance target from full commercial production to `DEMO_READY_THESIS`; moved live adapters, full deployment/load/chaos/accessibility/legal/operations qualification into a separate post-thesis backlog without relabeling them PASS | Scope frozen; final automated batch and targeted browser replay remain |
| 2026-09-02 | Closed the focused demo resource-boundary inventory and fixed a real P20 authorization gap: summary/transcript writes now require the meeting starter or a Group manager; a Project outsider is denied meeting action-item read/link/create with zero canonical mutation | Boundary Integration `34/34`; authorization Unit `54/54`; Group/Poll/Meeting Unit `121/121`; AI P06–P28 Integration `72/72` PASS |
| 2026-09-02 | Added an executable graduation seed manifest and mapped its canonical records into the manual runbook: Project/Sprint/dependency/workload, required-skill open Task, Done Task, confirmed professional evidence, capacity/absence, Wiki/Poll/Meeting transcript and Member/Viewer negative persona | `RichDemoSeedTests 3/3 PASS`; final fresh demo database and browser read-back remain |
| 2026-09-02 | Closed the P16–P27 typed-renderer refinement: editable P17 Organization skill picker persisted to canonical requirements; visible target scope; per-capability blockers and truthful confirm labels; editable P21 reason/date selection; P22 email channel/time/timezone; P26 server-fallback badge; prepared one bounded P16–P27 targeted replay | Vue typecheck PASS; native domain Integration `9/9`; P25/P27/P28 Integration `3/3`; P26 fallback Unit `1/1`; Chromium intentionally pending final replay |
| 2026-09-02 | Aligned AI-created Poll with the canonical Group workflow by persisting the Poll and its visible `GroupMessage` card in the same native-action transaction | Covered in native domain Integration `9/9`; canonical Poll/options/message/read-back asserted |
| 2026-09-02 | Removed the invalid SQL Server filtered unique index on computed `PushSubscriptions.EndpointHash`; kept the unique persisted hash contract unfiltered and aligned migration designers/snapshot | SQL Server + skill-evidence focused subset `15/15 PASS`; full Integration `246/246 PASS` |
| 2026-09-02 | Updated WebFeature fixtures to honor the real CSRF contract and collaborator directory boundary instead of bypassing them | Focused failures `3/3 PASS`; full WebFeature `38/38 PASS` |
| 2026-09-02 | Ran the final bounded P16–P27 Chromium replay. P16–P17 used live natural prompts; P18–P27 replayed persisted typed responses for renderer, read-only, fallback and deep-link contracts. One assertion initially read text instead of an input value; after correcting the harness, the unverified P18–P27 slice passed `2/2`. | Browser evidence `3/3 PASS` in aggregate; no product runtime failure remained |
| 2026-09-02 | Closed the graduation-demo gate after the complete automated batch and production builds | `DEMO_READY_THESIS`; P28 and all live external/deployment/load/accessibility-production work remain explicitly deferred in file 22 |
| 2026-09-02 | Added safe liveness/readiness contracts, production-container CI verification and exact-SHA post-CI image publishing with provenance/SBOM | Workflow/config source gates prepared; local Docker daemon unavailable, so successful CI runtime and environment deployment remain pending |
| 2026-09-02 | Ran a disposable full SQL migration → backup → clean restore rehearsal and added a reusable fail-closed script | 57 migrations through P024, restore verification, `DBCC CHECKDB`, 95-table smoke and cleanup PASS; target SQL Server 2022 replay remains pending |
| 2026-09-02 | Added webhook operations visibility, redacted canonical history, idempotent audited dead-letter replay, readiness thresholds and 7–365 day retention that also cleans soft-deleted parent history without deleting dead-letters | Focused Unit `6/6`, production validator `8/8`, health/operator WebFeature `4/4` PASS; external alert route/endpoint remain pending |
| 2026-09-02 | Reran the complete automated regression after the production operations batch | Client `250/250`, Unit `773/773`, Integration `252/252`, WebFeature `44/44`, frontend production build PASS, solution build `0 warning / 0 error` |
| 2026-09-02 | Added a non-breaking response security baseline, disabled Kestrel server banner, throttled anonymous login/register, enforced npm audit beside NuGet scanning and defined scheduled/PR CodeQL for C# plus JavaScript | Security-header/limiter focused WebFeature `5/5`; fresh NuGet/npm scans contain no vulnerability; CodeQL run and authenticated DAST remain pending external CI/target evidence |
| 2026-09-02 | Closed the trusted-client-IP ambiguity at the application boundary: direct mode rejects a proxy trust list; trusted-proxy mode requires unique concrete proxy IPs and a bounded hop count before forwarded headers may affect HTTPS, secure cookies or account limiter partitions | Production validator `13/13`; spoofed forwarded-IP limiter negative test PASS; full Integration `255/255`; WebFeature `45/45`; solution build `0 warning / 0 error`. Edge/WAF and deployed-target verification remain post-thesis production evidence |
| 2026-09-02 | Removed explicit raw recipient email from login, SMTP, group-invitation and digest operational log templates; added a local/CI high-confidence credential plus PII-log gate and an evidence-linked 16-threat model | Hygiene gate PASS; affected Unit `112/112`; account security WebFeature `3/3`; solution build `0 warning / 0 error`. Repository-host scanning, deployed log sampling, CodeQL/DAST and independent sign-off remain external production evidence |
| 2026-09-02 | Added an optional vendor-neutral OpenTelemetry OTLP baseline for ASP.NET Core/outbound HTTP metrics and sampled traces, with no public metrics endpoint and fail-closed validation for endpoint, service name, sampling and plaintext acknowledgement | Production validator `22/22`, registration `2/2`, full Integration `266/266` and WebFeature `45/45` PASS. Target collector/dashboard/alerts, retention/cost policy, custom AI SLI and incident drill remain `POST-OPS-002` runtime evidence |
| 2026-09-02 | Instrumented the durable Assistant turn boundary with bounded-cardinality first-progress, first-answer, terminal outcome/duration and fallback SLI; no entity/user/project id, prompt, message or payload is exported | Metric Unit `1/1`, durable-turn Integration `1/1`, full Unit `774/774`, full Integration `267/267` PASS. Target collector/dashboard, baseline calibration, alert receipt and incident drill remain runtime evidence |
| 2026-09-02 | Audited every runtime `Skip`, made all EF paging orders fully unique with an `Id` tie-breaker, stabilized capped operational/AI queries, and verified the SQL Server multi-collection policy remains `SplitQuery` without adding a relational dependency to Application | Focused service Unit `130/130`, focused Integration `24/24`, split-query registration `1/1`, full Unit `774/774`, full Integration `268/268`, WebFeature `45/45`, solution build `0 warning / 0 error` PASS. Target query plans, traffic thresholds and load/soak remain `POST-PERF-001/002` |
| 2026-09-02 | Removed redundant per-row Task authorization queries from project list and Kanban plus the duplicate detail check after the canonical visibility filter; added a compiled-assembly contract for every HTTP action and a fixed reviewed anonymous allowlist | Task visibility/Kanban `6/6`, controller auth contract `2/2`, full Unit `775/775`, Integration `270/270`, WebFeature `45/45`, solution build `0 warning / 0 error` PASS |
| 2026-09-02 | Hardened the correlation boundary and pipeline order: unsafe, oversized or multi-valued client ids are replaced; response, trace and Serilog completion log share one bounded canonical id | Focused WebFeature `4/4`, full WebFeature `49/49`, solution build `0 warning / 0 error` PASS; target log/trace sampling remains post-thesis |
| 2026-09-02 | Replaced notification top-100 filtering and per-row access resolution with stable authorized paging plus batched exact unread counting; aligned private Task notification access with active custom manager roles | Notification/policy Unit `20/20`, full Unit `778/778`, Integration `271/271` PASS |
| 2026-09-02 | Made Organization + owner + professional profiles, Group + owner, and Meeting session + canonical message single-commit graphs; classified remaining multi-save AI/agent workflows as explicit durable state machines | Atomic graph Unit `3/3`; combined focused batch `23/23`; full Unit `778/778`, Integration `271/271`, WebFeature `49/49`, build clean and `git diff --check` PASS |
| 2026-09-02 | Replaced the recent-audit top-500/per-row authorization path with stable batched scanning and batched Task/Sprint/Project access resolution; added a 520-hidden-row regression proving authorized older activity is neither lost nor cross-tenant leaked | Focused Integration `2/2`, full Integration `271/271`, Unit `778/778`, WebFeature `49/49`, build clean PASS |
| 2026-09-02 | Closed the post-commit audit failure window across canonical demo mutations with a non-committing audit stage and one shared UnitOfWork commit; batch writes stage all audit rows first, while deliberate AI phase commits keep explicit recoverable states | Fault-injection Unit `4/4`, audit staging Integration `1/1`, full Unit `782/782`, Integration `272/272`, WebFeature `49/49`, solution build `0 warning / 0 error`, `git diff --check` clean |
| 2026-09-03 | Replaced the process-heavy health-only load smoke with a secret-safe concurrent read probe and durable percentile/error evidence; added bounded host shutdown configuration and stopped AI/Privacy/GitHub plus maintenance workers from turning host cancellation into false failures or stuck GitHub claims | Probe validation/execution path PASS; worker cancellation `3/3`; validator/lifecycle `25/25`; full Unit `785/785`, Integration `275/275`, WebFeature `49/49`, solution build `0 warning / 0 error`. Target authenticated load, soak and fleet SIGTERM remain pending |
| 2026-09-03 | Replaced GitHub inbox read-then-write batch ownership with migration P025 durable lease semantics: conditional atomic claim, owner/expiry, heartbeat, exponential retry, shutdown release, expired-final terminalization and stale-owner concurrency rollback of processor mutations | SQL Server concurrency/recovery `5/5`, GitHub shutdown/receiver `6/6`, validator/lifecycle `33/33`; EF model has no pending change and P024→P025 idempotent SQL script generates cleanly; full Unit `785/785`, Integration `288/288`, WebFeature `49/49`, solution build `0 warning / 0 error`. Live GitHub adapter/fleet kill replay remains external evidence |
| 2026-09-03 | Closed VectorSync queue loss/reorder/false-success gaps with migration P026: canonical event contract, database sequence, aggregate identity, conditional lease/heartbeat, predecessor serialization, retry/backoff/dead-letter, stale-owner rejection, cancellation propagation and semantic-disabled enqueue guard; corrected rich-demo outbox seed | Focused Unit `26/26`, validator + real SQL Integration `49/49`; SQL covers exclusive claim, ordering, expired takeover, stale completion, retry/dead-letter, invalid contract and P025→P026 legacy normalization. EF has no pending model change; idempotent script generates. Full client `250/250`, Unit `801/801`, Integration `304/304`, WebFeature `49/49`, typecheck/build and solution Release build `0 warning / 0 error` PASS. Live Qdrant/fleet process-loss evidence remains external-deferred |
| 2026-09-03 | Closed AI/Privacy process-loss and queue-visibility gaps: heartbeat loss cancels work, processors reject expired/stale ownership before commit, host shutdown releases immediately without false provider failure, BatchSize is enforced, readiness reports backlog/expired leases, and Privacy selects accepted DSAR by legal deadline before retention maintenance | Focused worker/processor/health Unit `17/17`; validator + AI/Privacy real SQL `73/73`, including exclusive takeover, stale AI result rejection, shutdown requeue, DSAR priority/deadline. Full client `250/250`, Unit `810/810`, Integration `329/329`, WebFeature `49/49`, typecheck/build and solution Release build `0 warning / 0 error` PASS. Target fleet SIGTERM/soak/alert routing remains external runtime evidence |
| 2026-09-03 | Aligned Privacy operator health with readiness (failed/overdue DSAR/expired lease), removed the 500-Task attention starvation ceiling, resolved closed/deleted/unassigned signals, and made trash cleanup retain canonical metadata when physical storage deletion fails | Focused new worker tests `3/3`, Privacy service `6/6`, Privacy API `4/4`; full client `250/250`, Unit `814/814`, Integration `330/330`, WebFeature `49/49`, typecheck/build and solution Release build `0 warning / 0 error` PASS. Target storage/fleet/load runtime remains external evidence |
| 2026-09-03 | Hardened remaining scheduled workers: outbound webhook releases its claim on host cancellation and repeats due/retry predicates; email digest rejects inactive access, retries provider cancellation and cannot mark an SMTP failure delivered; Project operation monitoring includes soft-deleted Projects, isolates/defer poison rows and preserves newer concurrent state. Corrected an Integration fixture that falsely described its in-memory cache as a Redis-outage latency test; deterministic Redis fail-fast tests remain. | Focused affected worker/cache Unit `28/28`; auth Integration `12/12`; full client `250/250`, Unit `821/821`, Integration `331/331`, WebFeature `49/49`, solution Release build `0 warning / 0 error` PASS. Live SMTP/webhook receipts and target fleet process-loss/load remain external evidence |
| 2026-09-03 | Replayed the complete disposable SQL migration/backup/restore path after P025/P026: generated isolated databases, applied all 59 migrations, verified backup checksum, restored cleanly, ran `DBCC CHECKDB`, validated 95 tables/59 migration rows and removed both rehearsal databases. Also built the current production Docker target and verified declared/runtime UID `1654`; target deployment and distinct-image rollback remain separate gates. | Fresh recovery artifacts `20260903065913-*` and `20260903-065938-*` PASS; latest migration `20260903130000_P026VectorSyncOutboxReliability`; restore RTO 1.27s; no rehearsal database remained |
