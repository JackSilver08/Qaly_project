# Qaly v4.0 Goal và Kế hoạch Thực thi Hai Tuần

- Trạng thái tài liệu: Execution plan sau audit, chưa phải bằng chứng hoàn thành.
- Audit baseline: `0d9c5f159a7935d12f298487726ef40b65a2b306`.
- Nhánh audit: `codex/p0-00-p0-03-merged-upstream`.
- Ngày audit: 2026-07-13, Asia/Saigon.
- Goal dài hạn: đang `paused`, không được đánh dấu Complete bởi kế hoạch này.

## 1. Goal và Definition of Done

Đưa Qaly từ hệ thống demonstrable hiện tại thành nền tảng cộng tác và vận hành
dự án v4.0 có thể kiểm chứng: hoàn tất P0/P1 đã khóa, 20 use case P0, 8 chức
năng AI, AI job/draft lifecycle, privacy/retention/DSAR, budget, resilience,
recovery, GitHub governance, canonical entity graph/QoL, scheduling có human
approval, versioning/rollback và release evidence. Không mở P2 sớm, không cho
AI tự mutation và không tách microservice thiếu bằng chứng.

Một chức năng chỉ Done khi:

1. Chạy end-to-end từ UI/API qua rule nghiệp vụ đến persistence/output.
2. Synthetic data đi qua cùng schema, validation, authorization, transaction và
   service path sẽ dùng cho dữ liệu thật.
3. Không phụ thuộc ID, thứ tự hoặc nội dung đặc biệt của seed.
4. Create/read/update-transition/reload-readback hoạt động; rollback/disable/delete
   được kiểm tra nếu nghiệp vụ có.
5. Allow và deny path, duplicate, stale version, retry, partial failure và restart
   có kết quả đúng, không success giả hoặc lỗi im lặng.
6. Database constraint bảo vệ invariant quan trọng; UI validation không phải lớp
   bảo vệ duy nhất.
7. External fake/sandbox chỉ thay transport, không bỏ qua domain flow; có contract
   test chứng minh payload và error semantics tương thích adapter thật.
8. AI output đúng schema, trace được source và cần human approval trước mutation.
9. UI loading/error/empty/success trung thực; canonical URL, refresh và Back đúng.
10. Build, migration, test liên quan và runtime smoke chạy lại được trong clean
    environment bằng lệnh ghi trong task.

## 2. Executive Verdict

Qaly có implementation thật ở Project, Task, Group, Meeting session, AI job,
draft confirmation, Privacy API/worker và phần lớn nền tảng P0-01 đến P0-03.
Revalidation hiện tại pass `npm run typecheck`, 270/270 unit, 45/45 integration
và 9/9 web-feature tests.

Tuy nhiên, luồng người dùng chưa khép kín hoàn toàn. Các lỗi/gap chặn cao nhất:

- task mở từ Project chưa luôn đưa URL về canonical task;
- Project và Group có `SourceGroupId` nhưng thiếu liên kết hai chiều rõ trên UI;
- Meeting dùng query route thay vì canonical meeting/action route, LiveKit chưa có
  test adapter tái lập;
- AI-03/04/06/07/08 còn thiếu capability-specific schema/source/confirm evidence;
- budget read đã có nhưng mutation/audit/product UI và hard-stop evaluation chưa đủ;
- Privacy backend ghi database thật nhưng nút tạo policy đã thất bại trong runtime;
  E2E hiện mock toàn bộ Privacy API nên không bắt lỗi này;
- Redis degraded path còn chậm; EF soft-delete warning, clean restore, CODEOWNERS,
  protected-main evidence và release identity còn mở;
- P1 entity graph, scheduling và entity versioning vẫn là target behavior.

Kết luận: nền tảng có thật, nhưng chưa đạt release candidate. Hai tuần này cam
kết đóng P0 functional/integration gaps có thể làm trong 39 person-days; P1 lớn
được giữ trong backlog có owner và dependency, không được báo hoàn thành giả.

## 3. Baseline và nguồn bằng chứng

| Nguồn | Mẫu số/biên audit | Ghi chú |
|---|---:|---|
| Goal dài hạn | 1 compound objective | Được phân rã vào REQ universe bên dưới |
| Scope gốc/v3.2 | `ProjectHub.docx` + baseline v3.2 (158 file) | Dùng làm anchor khi v4 chưa rõ; 20 P0 và 8 AI lock được đối chiếu qua traceability v4 |
| Doc v4 baseline | 23 file, không tính prompt và file output này | Đã đọc index, SRS, 7 ADR + README, contract, UX, NFR, 3 CSV, roadmap, P0 evidence |
| Execution input | 1 prompt đã chuẩn hóa | Dùng làm closure contract, không tính là requirement baseline độc lập |
| P0 use case | 20 | `P0-UC-01..20` |
| AI function lock | 8 | `AI-01..08` |
| Acceptance gate | 41 | 12 Verified, 9 Implemented-Unverified, 16 Partial, 3 Missing, 1 Spec-only trước audit |
| Roadmap package | 17 | P0-00..08, P1-01..06, P2-01..02 |
| Risk record | 14 | R-001..R-014 |
| Source/test/config/CI files | 763 | Loại `bin`, `obj`, `node_modules`, generated `wwwroot/dist` khỏi file inventory |
| Controllers | 30 | 229 HTTP action attributes được phát hiện |
| Frontend pages | 12 | Cộng router, component groups, composables và API client |
| Domain migrations | 33 | Bao gồm P0-02, Task Key, GitHub schema và P0-03 |
| Current verification | typecheck + 270 unit + 45 integration + 9 web feature | Chạy ngày audit; preview cũ đã dừng sau runtime audit trước đó |

Lưu ý trạng thái tài liệu: evidence 13–15 có giá trị cho commit/feature branch đã
ghi, nhưng một số câu trong SRS/CSV đã lỗi thời sau merge. Runtime và test hiện
tại được ưu tiên hơn nhãn cũ.

## 4. Git Ownership Map

| Thành viên | Alias/evidence | Vùng đã làm | Phù hợp giao | Vùng cần reviewer mạnh |
|---|---|---|---|---|
| Trung | `jackhanma123 <trungnguyendoan9@gmail.com>`; `967f5cb`, `1687b5a`, `5e13d77`, `9d98190` | App/router, Task, Meeting UI, test case, E2E smoke, seed/config | QA, Playwright, regression và integration acceptance | Backend policy/migration cần Duy/Khang review |
| Khang | `khangphma2511 <khangplctb01620@gmail.com>`; `21ab726`, `41a6341`, `e844ba7` | AI Gateway/Service, Meeting AI, Import, Project UI, TaskService | AI capability, budget, schema, source/draft flow | Privacy/data migration cần Duy review |
| Duy Hoàng | `duyh-dev`; `3319a96`, `ab29606`, `3910530`, `1e8c18e` | Group backend, invite/notification/poll, permission, meeting/import, unit/integration QA | Group relationship, Redis/data integrity, backend source resolver | Frontend UX cần Minh/Long review |
| Minh | Không có Git alias/commit chắc chắn; hồ sơ cũ giao `VM-01..06` | Kế hoạch trước: Import UI/parser, frontend typecheck, Docker/appsettings, bugfix | Frontend task nhỏ, route, truthful settings, shared resolver P0 | Mọi PR cần Trung test và Long frontend review; không giao migration độc lập |
| Long | `GiaLong281 <longngtb01597@gmail.com>`; `ed63745`, `469dd70`, `7851181`, `9f4f361` | Dashboard/Project/Group/Meeting UI, CSS, config; hồ sơ cũ giao docs/DevOps | Meeting UX, budget UI, recovery/Git governance artifacts | Backend recovery script cần Duy review; CI policy cần Trung review |

Không gán alias Tuan/Quang/XernesLucid cho Minh. Ownership của Minh được ghi
`ownership evidence unavailable`; effort và blast radius đã giảm tương ứng.

## 5. Requirement Universe (59/59 inventoried)

### 5.1 P0 use cases

| REQ-ID | Requirement | Audit status | Disposition |
|---|---|---|---|
| REQ-P0-01 | Auth/session lifecycle | Partial | GAP-002, GAP-021; T1-DH-02 |
| REQ-P0-02 | Project/membership | Verified | VER-001; giữ regression |
| REQ-P0-03 | Tenant/project RBAC | Verified | VER-002; mở rộng meeting/AI negative tests trong T2-TR-03 |
| REQ-P0-04 | Manual task lifecycle | Partial | GAP-003; T1-MN-01, T1-TR-01 |
| REQ-P0-05 | Kanban/concurrency/audit | Verified | VER-003; giữ unit/integration |
| REQ-P0-06 | Comment/attachment/evidence | Implemented-Unverified | GAP-004; T1-TR-02 |
| REQ-P0-07 | Project collaboration group | Partial | GAP-005; T1-DH-01, T2-MN-03 |
| REQ-P0-08 | Permission-aware notification | Partial | GAP-006; T1-TR-02, T2-MN-04 |
| REQ-P0-09 | Meetily intake/privacy | Implemented-Unverified | GAP-009, GAP-018; T2-TR-03 |
| REQ-P0-10 | Meeting AI extraction | Verified-local | VER-004; synthetic provider accepted, retain regression |
| REQ-P0-11 | Confirm AI draft once | Verified-local | VER-005; T2-TR-03 regression |
| REQ-P0-12 | Chat range summary | Partial | GAP-011; T1-KH-02 |
| REQ-P0-13 | Task draft from chat | Partial | GAP-012; T1-KH-02 |
| REQ-P0-14 | Assignee recommendation | Verified-baseline | VER-006; advanced signals are P1 |
| REQ-P0-15 | Breakdown/checklist drafts | Partial | GAP-013, GAP-014; T2-KH-03 |
| REQ-P0-16 | Project/sprint grounded summary | Partial | GAP-015; T2-KH-04 |
| REQ-P0-17 | Usage/budget/warning/hard stop/UI | Partial | GAP-016; T1-KH-01, T2-LG-03 |
| REQ-P0-18 | Provider fallback/degraded | Verified-local | VER-007; GAP-010 clean-env replay |
| REQ-P0-19 | Consent/DSAR/retention/legal hold | Partial | GAP-017, GAP-018, GAP-019; T1-MN-02, T2-TR-03 |
| REQ-P0-20 | Backup/clean restore/recovery | Partial | GAP-025; T1-LG-01 |

### 5.2 Locked AI functions và platform

| REQ-ID | Requirement | Audit status | Disposition |
|---|---|---|---|
| REQ-AI-01 | Meetily import/manual sync | Implemented-Unverified | GAP-009, GAP-018 |
| REQ-AI-02 | Meeting extraction | Verified-local | VER-004 |
| REQ-AI-03 | Selected chat range summary | Partial | GAP-011; T1-KH-02 |
| REQ-AI-04 | Source-linked task draft | Partial | GAP-012; T1-KH-02 |
| REQ-AI-05 | Explainable assignee ranking | Verified-baseline | VER-006 |
| REQ-AI-06 | Selective task breakdown | Partial | GAP-013; T2-KH-03 |
| REQ-AI-07 | Acceptance checklist | Missing capability-specific behavior | GAP-014; T2-KH-03 |
| REQ-AI-08 | Reconciled project/sprint summary | Partial | GAP-015; T2-KH-04 |
| REQ-PLAT-01 | Canonical async AI job/draft lifecycle | Implemented-Unverified | GAP-010; T2-TR-03/T2-LG-04 |

### 5.3 P1 packages

| REQ-ID | Requirement | Audit status | Disposition |
|---|---|---|---|
| REQ-P1-01 | Complete AI-03/04/06/07/08 | Partial | GAP-011..015; phần P0 slice committed |
| REQ-P1-02 | Full entity graph/previews/backlinks/accessibility | Spec-only | GAP-033; P1 backlog sau T2-MN-04 foundation |
| REQ-P1-03 | Skills/proficiency/capacity/availability | Missing | GAP-034; Post-2-week |
| REQ-P1-04 | Deterministic constraint scheduling | Missing | GAP-035; Post-2-week sau REQ-P1-03 |
| REQ-P1-05 | Wiki/Project/Task version snapshots and rollback | Missing | GAP-036; Post-2-week |
| REQ-P1-06 | Decompose large modules/bundle performance | Partial | GAP-029, GAP-030; Post-2-week |

### 5.4 Non-functional requirements

| REQ-ID | Requirement | Audit status | Disposition |
|---|---|---|---|
| REQ-NFR-01 | Performance | Partial | GAP-021, GAP-027, GAP-029 |
| REQ-NFR-02 | Security | Implemented-Unverified | GAP-026, GAP-028; local scans green |
| REQ-NFR-03 | Privacy | Partial | GAP-017..019, GAP-037 |
| REQ-NFR-04 | Dependency degraded states | Partial | GAP-008, GAP-010, GAP-021 |
| REQ-NFR-05 | Data integrity | Partial | GAP-020, GAP-025 |
| REQ-NFR-06 | AI controls | Partial | GAP-010..016 |
| REQ-NFR-07 | UX truth/canonical/accessibility | Partial | GAP-003, GAP-005..007, GAP-017, GAP-022..024 |
| REQ-NFR-08 | Operations/observability/release | Partial | GAP-025..028 |
| REQ-NFR-09 | Recovery/RPO/RTO | Missing | GAP-019, GAP-025 |
| REQ-NFR-10 | Maintainability/ownership/contracts | Partial | GAP-026, GAP-029, GAP-030 |

### 5.5 ADR, P2 và exclusions

| REQ-ID | Requirement | Audit status | Disposition |
|---|---|---|---|
| REQ-ADR-01 | Modular monolith/owned boundaries | Accepted-Partial | GAP-026, GAP-030 |
| REQ-ADR-02 | Cookie session | Accepted-Partial | GAP-002, GAP-021 |
| REQ-ADR-03 | Canonical AI jobs | Accepted-Implemented-Unverified | GAP-010 |
| REQ-ADR-04 | Primary Group relationship | Accepted-Partial | GAP-005 |
| REQ-ADR-05 | Meeting privacy/retention | Accepted-Partial runtime | GAP-017..019 |
| REQ-ADR-06 | Deterministic scheduling | Accepted-Spec-only | GAP-034, GAP-035 |
| REQ-ADR-07 | Entity snapshots/rollback | Accepted-Spec-only | GAP-036 |
| REQ-P2-01 | Semantic/portfolio experiments | Out-of-scope | GAP-038; giữ flag Off |
| REQ-P2-02 | Multiple groups per project | Out-of-scope | GAP-038; cần workflow evidence mới mở |
| REQ-EX-01 | No autonomous mutation | Verified policy | VER-008; giữ confirmation tests |
| REQ-EX-02 | No generic event sourcing | Out-of-scope | VER-009 disposition |
| REQ-EX-03 | No unjustified microservices | Out-of-scope | VER-010 disposition |
| REQ-EX-04 | No replacement of Meetily core | Out-of-scope | VER-011 disposition |
| REQ-EX-05 | No third-party feature parity/subjective readiness | Out-of-scope | GAP-031/GAP-032 governance |

## 6. Implementation Surface Universe (56/56 inventoried)

Quy tắc nhóm: một surface là entry point hoặc vertical module có thể kiểm thử độc
lập. Helper/DTO/configuration nội bộ được roll-up vào surface chủ; mọi controller,
page, worker, test layer và operational surface đều có disposition.

| IMP-ID | Surface | Evidence chính | Status | Mapping |
|---|---|---|---|---|
| IMP-001 | Auth/session/CSRF API | `AuthController`, `SecurityController`, `AuthService` | Partial | REQ-P0-01; GAP-002 |
| IMP-002 | Organization/user/admin API | `OrganizationsController`, `UsersController`, `AdminUsersController` | Implemented | REQ-P0-02/03 |
| IMP-003 | Project/membership API | `ProjectsController`, `ProjectService` | Verified-local | REQ-P0-02/03 |
| IMP-004 | Project GitHub metadata API | `ProjectGitHubController`, GitHub entities/services | Implemented-Unverified | REQ-NFR-08/10; GAP-026 |
| IMP-005 | Task lifecycle API/domain | `TasksController`, `TaskService` | Verified-local | REQ-P0-04/05 |
| IMP-006 | Comment/attachment/evidence/time API | four controllers/services | Implemented-Unverified | REQ-P0-06; GAP-004 |
| IMP-007 | Sprint/timeline/workload/attention | `SprintsController`, task workers, Project tabs | Implemented | REQ-P0-05/16 |
| IMP-008 | Group/member/invitation API | `GroupsController`, `GroupsService` | Implemented | REQ-P0-07; GAP-005 |
| IMP-009 | Group message/realtime/file/reaction | `GroupsController`, `GroupHub`, chat components | Implemented | REQ-P0-07/08 |
| IMP-010 | Poll/vote | Groups/Votes controllers, GroupPoll page | Implemented | REQ-P0-07 |
| IMP-011 | Meeting session/SignalR/LiveKit token | Groups service/controller, `GroupMeetingPage` | Partial | GAP-007, GAP-008 |
| IMP-012 | Meeting import/action mapping | `MeetingImportService`, `MeetingsController` | Verified-local core | REQ-P0-09..11 |
| IMP-013 | Wiki | `WikiController`, `WikiService`, Wiki pages | Implemented | REQ-P1-05; GAP-036 |
| IMP-014 | Notification/push | Notifications controllers/services | Implemented-Partial links | GAP-006 |
| IMP-015 | Search | `SearchController`, App search | Implemented-Unverified canonical | GAP-023/024 |
| IMP-016 | Dashboard/analytics/report | Dashboard/Analytics controllers/services/pages | Implemented-Partial grounding | GAP-015 |
| IMP-017 | Import | Import controller/services/components | Implemented | REQ-P0-09 |
| IMP-018 | AI gateway/provider/compliance/cost ledger | Infrastructure AI services | Implemented | REQ-NFR-06 |
| IMP-019 | Canonical AI job/dispatch/worker | P0-02 entities/services/worker | Implemented-Unverified | REQ-PLAT-01; GAP-010 |
| IMP-020 | AI wrapper endpoints | 8 wrappers in `AiController` | Partial by capability | GAP-011..015 |
| IMP-021 | AI draft review/confirm | `AiWorkflowService`, AI API | Verified-local generic | REQ-P0-11 |
| IMP-022 | AI Activity UI | `AiActivityPanel.vue` | Implemented | REQ-PLAT-01 |
| IMP-023 | Erumi/Analyst tool flow | `ErumiChatService`, Analytics AI components | Partial source resolver | GAP-023/030 |
| IMP-024 | Privacy policy/consent API | `PrivacyController`, `PrivacyService` | Verified backend, UI gap | GAP-017 |
| IMP-025 | Retention/DSAR/legal-hold workers | Privacy worker/store/processor | Implemented-Unverified environment | GAP-018/019 |
| IMP-026 | Audit API/service | `AuditLogsController`, `AuditLogService` | Implemented-Partial links | GAP-023 |
| IMP-027 | Storage/vector/outbox | storage controller, CAS, vector workers | Implemented/deferred semantic | REQ-P2-01; GAP-038 |
| IMP-028 | Redis ticket store | `RedisTicketStore` fallback | Partial/slow degraded | GAP-021 |
| IMP-029 | LiveKit configuration | appsettings, Groups meeting token | Environment-Blocked | GAP-008 |
| IMP-030 | SQL model/migrations | 33 migrations, DbContext | Partial warnings | GAP-020 |
| IMP-031 | P0-02 migration/reconciliation scripts | `scripts/ai-jobs` | Verified-local | GAP-010 staging disposition |
| IMP-032 | P0-03 migration/reconciliation scripts | `scripts/privacy` | Verified-local | GAP-018 staging disposition |
| IMP-033 | Backup script | `backup-sqlserver.ps1` | Implemented-Unverified | GAP-025 |
| IMP-034 | Restore workflow | no restore script | Missing | GAP-025 |
| IMP-035 | Frontend router/canonical history | `router/index.ts`, `App.vue` | Partial | GAP-003/007/023 |
| IMP-036 | Project UI | Project pages/components | Partial collaboration links | GAP-003/005 |
| IMP-037 | Tasks UI/QoL | `TasksPage.vue` 3598 lines | Implemented-Partial | GAP-024/030 |
| IMP-038 | Groups UI | `TeamsPage.vue`, chat components | Implemented-Partial links | GAP-005/007 |
| IMP-039 | Meeting UI | `GroupMeetingPage.vue` 2783 lines | Partial/degraded | GAP-007/008/030 |
| IMP-040 | Analytics UI | `AnalyticsPage`, 9 AI components | Implemented-Partial sources | GAP-015/023 |
| IMP-041 | Settings/Privacy UI | `SettingsPage`, `PrivacySettingsTab` | Product bug/partial truth | GAP-017/022 |
| IMP-042 | Import UI | five import components | Implemented | REQ-P0-09 |
| IMP-043 | Wiki UI | Project Wiki tab/detail | Implemented, no versions | GAP-036 |
| IMP-044 | Shell/search/notification UI | AppShell/sidebar/top header/App | Partial canonical targets | GAP-006/023/024 |
| IMP-045 | Frontend API/toast client | `api-client.ts`, `use-toast.ts` | Implemented, integration gap exposed | GAP-017/022 |
| IMP-046 | Unit test layer | 30 test classes; 270 current pass | Implemented | all unit-verifiable requirements |
| IMP-047 | Integration test layer | 12 behavior test classes; 45 pass | Implemented-Partial coverage | GAP-004/017/020 |
| IMP-048 | Web-feature layer | smoke host; 9 pass | Implemented | P0 regression |
| IMP-049 | Browser E2E layer | 5 specs | Partial; Privacy API mocked | GAP-003..008/017/024 |
| IMP-050 | CI workflows | `ci.yml`, daily E2E | Implemented-Unverified hosted governance | GAP-026 |
| IMP-051 | CD/container release | `cd.yml`, Docker, validation scripts | Partial identity/restore | GAP-025..028 |
| IMP-052 | Configuration/feature flags | appsettings/env/compose | Partial production parity | GAP-028 |
| IMP-053 | Git governance/release identity | no CODEOWNERS/tags/protection evidence | Missing/Environment-Blocked | GAP-026 |
| IMP-054 | Capability/scheduling model | no required domain tables/solver | Missing | GAP-034/035 |
| IMP-055 | Entity versioning model | no snapshot/diff/rollback domain | Missing | GAP-036 |
| IMP-056 | Shared entity resolver | no canonical registry/routes for all entities | Missing | GAP-023/033 |

## 7. Full Gap Register

| GAP-ID | Loại / Severity | Hiện tại -> Mong muốn | Evidence | Disposition |
|---|---|---|---|---|
| GAP-001 | Documentation / High | Scope/owner chưa được phê duyệt chính thức -> signed v4 ownership | Roadmap P0-00 | Post-2-week PO decision; Product Owner |
| GAP-002 | Product/Verification / High | Session renewal/revoke/fault path chưa đủ -> full lifecycle tests | SRS AUTH; acceptance G-UC-01 | T1-DH-02 + Trung review |
| GAP-003 | UX/Product / High | Task drawer click không luôn đổi URL -> canonical open/refresh/back/share | router lines 31-34; runtime audit | T1-MN-01, T1-TR-01 |
| GAP-004 | Verification / High | Comment/upload/evidence review chưa E2E thật -> persisted allow/deny flow | G-UC-06 | T1-TR-02 |
| GAP-005 | Integration/UX / High | `SourceGroupId` có nhưng thiếu semantics/link hai chiều -> PrimaryGroup contract | ADR-004; runtime audit | T1-DH-01, T2-MN-03 |
| GAP-006 | Integration/UX / Medium | Notification có record nhưng canonical permission target không đều -> resolver target | G-UC-08 | T1-TR-02, T2-MN-04 |
| GAP-007 | UX/Integration / High | Meeting query route và action index/source lỏng -> canonical meeting/action routes/backlink | router lines 54-57; UX map | T2-DH-04, T2-MN-03 |
| GAP-008 | Environment/Contract / Medium | LiveKit trống, chỉ unavailable message -> configured/unconfigured contract + test adapter | appsettings Development; runtime | T2-LG-04, T2-TR-03 |
| GAP-009 | Verification / Medium | Meetily synthetic path tốt, clean import-to-job replay chưa là release gate -> repeatable test | G-UC-09/G-AI-01 | T2-TR-03; staged environment backlog |
| GAP-010 | Environment/Verification / High | AI job code có, worker preview off -> one-command synthetic queued-to-confirm smoke | P0-02 evidence/runtime | T2-LG-04, T2-TR-03 |
| GAP-011 | Product / High | Chat summary chưa locked selected-range/cache/source contract | G-AI-03 | T1-KH-02 |
| GAP-012 | Product / High | Chat/manual source chưa editable persisted task draft đầy đủ | G-AI-04 | T1-KH-02 |
| GAP-013 | Product / High | Breakdown generic draft chưa selective subtask confirm/schema evidence | G-AI-06 | T2-KH-03 |
| GAP-014 | Product / High | AI-07 wrapper có nhưng capability-specific validation/confirm/E2E thiếu | AiController 248; processor generic draft | T2-KH-03 |
| GAP-015 | Product / High | Progress summary chưa reconcile SQL project+sprint metrics/source | G-AI-08 | T2-KH-04 |
| GAP-016 | Product/UX / High | Budget reads có; policy mutation/audit/UI và monthly hard-stop service thiếu | AiPlatformQueryService 125; AiCostService 23 | T1-KH-01, T2-LG-03 |
| GAP-017 | Product/Verification / Critical | Privacy API ghi DB thật nhưng UI create policy không ghi; E2E mock API | runtime audit; privacy E2E lines 63-159 | T1-MN-02, T1-TR-01 |
| GAP-018 | Environment/Verification / High | Privacy worker/migration local tốt, clean hosted-like replay/continuous health thiếu | P0-03 evidence | T2-TR-03; staging backlog |
| GAP-019 | Operations / High | DSAR backup expiry chỉ báo pending -> backup lifecycle/recovery evidence | P0-03 evidence/R-001 | T1-LG-01 + post-2-week policy |
| GAP-020 | Data / High | EF filter warning/relationship restore edge còn mở -> zero warning + relationship tests | G-NFR-03; DbContext | T2-DH-03 |
| GAP-021 | Resilience / High | Redis unavailable gây multi-second latency -> <=1s fail-fast/circuit/health | NFR PERF-03; R-006 | T1-DH-02 |
| GAP-022 | UX/Product / High | Settings vẫn báo global success sau partial failure -> per-operation truth | SettingsPage line 299 | T1-MN-02 |
| GAP-023 | Architecture/UX / High | Không có shared entity resolver/source URLs -> canonical registry, auth preview/tombstone | UX map; router | T2-DH-04, T2-MN-04; P1 remainder |
| GAP-024 | UX/QoL / Medium | Search/saved/bulk/undo/loading/error có rải rác, chưa contract/E2E thống nhất | UX map; Tasks page | T2-MN-04 + Post-2-week accessibility |
| GAP-025 | Recovery / Critical | Có backup, không có clean restore/rollback script evidence | `RESTORE=NO`, R-007 | T1-LG-01 |
| GAP-026 | Governance / High | Không CODEOWNERS/tag/branch protection evidence | `CODEOWNERS=NO`, R-011 | T1-LG-02; hosted setting by PO/admin |
| GAP-027 | NFR / Medium | Performance/observability release metrics chưa đủ | NFR PERF/Observability | Post-2-week; Long owner |
| GAP-028 | Configuration / High | Production example thiếu AI/Privacy flags và config validation parity | appsettings Production | T1-LG-02 |
| GAP-029 | Maintainability / Medium | 7,387 tracked node_modules files, large bundles | git inventory; P0-01 residual | Post-2-week; Long/Minh |
| GAP-030 | Maintainability / Medium | Tasks 3598, GroupsService 2801, Meeting 2783, Erumi 2259 lines | source count/R-009 | Post-2-week; Long điều phối cùng Khang/Duy/Minh sau stable tests |
| GAP-031 | Documentation / High | SRS/CSV status không khớp post-merge/runtime | P0 evidence vs SRS text | T2-TR-04 |
| GAP-032 | Verification/Governance / High | Chưa sign toàn bộ P0 acceptance/release candidate | P0-08, 41 gates | T2-TR-04 + PO signoff |
| GAP-033 | P1 Product / Medium | Full preview/backlink/mobile/a11y graph chưa có | REQ-P1-02 | Post-2-week; Minh + Duy sau T2-MN-04 |
| GAP-034 | P1 Data / Medium | Skills/capacity/availability model thiếu | ADR-006 | Post-2-week; Duy + Product |
| GAP-035 | P1 Product / Medium | Deterministic constraint scheduler thiếu | ADR-006 | Post-2-week after GAP-034; Khang |
| GAP-036 | P1 Product/Data / Medium | Snapshot/diff/rollback cho Wiki/Project/Task thiếu | ADR-007 | Post-2-week; Duy + Minh |
| GAP-037 | Environment/Policy / High | Legal policy/live provider/production topology chưa được phê duyệt | G-NFR-09/R-001 | Post-2-week; Product Owner + Long |
| GAP-038 | Scope / Deferred | Semantic/portfolio/multi-group có surface/ý tưởng nhưng không được mở sớm | P2 scope/flags | Out-of-scope; Product Owner giữ scope, không giao task |

## 8. Phạm vi cam kết và backlog

### Cam kết hai tuần

- 20 task, 39 person-days.
- Trung 7.5; Khang 8; Duy Hoàng 8; Minh 8; Long 7.5.
- 11 person-days còn lại là review, conflict, CI chờ, rework và release smoke.
- Chỉ đóng task khi Definition of Done ở mục 1 đạt; test mock API đơn lẻ không đủ
  cho vertical flow cần persistence.

### Post-2-week remaining backlog

| Backlog | Gap | Dependency | Owner đề xuất |
|---|---|---|---|
| PO phê duyệt scope/owner/legal policy | GAP-001, GAP-037 | PO/Privacy/Infrastructure | Product Owner |
| Hosted branch protection và staged rollout evidence | GAP-010, GAP-018, GAP-026, GAP-032 | repo/hosting admin | Long + Trung |
| Full performance/observability/load | GAP-027 | P0 stable | Long + Duy |
| Untrack node_modules/bundle decomposition | GAP-029 | dedicated migration | Long + Minh |
| Decompose oversized modules | GAP-030 | stable behavior tests | module owners |
| Full entity preview/backlink/a11y | GAP-033 | T2-MN-04 | Minh + Trung |
| Capability data model | GAP-034 | Product/privacy policy | Duy |
| Constraint scheduler | GAP-035 | GAP-034 | Khang |
| Entity snapshots/diff/rollback | GAP-036 | GAP-020 recovery stable | Duy + Minh |
| Semantic/portfolio/multi-group | GAP-038 | all P0/relevant P1 green | Deferred |

## 9. Task Board Hai Đợt

### Đợt 1 / Tuần 1

| Task | Owner | Reviewer | Effort | Ngày | Gap | Depends |
|---|---|---|---:|---|---|---|
| T1-TR-01 Runtime regression cho task URL, Privacy UI, Group link | Trung | Minh/Duy | 2.0 | D1-D2 | 003,005,017 | none |
| T1-TR-02 Comment/evidence/notification allow-deny E2E | Trung | Duy | 2.0 | D3-D4 | 004,006 | none |
| T1-KH-01 Budget policy mutation, audit và hard-stop | Khang | Duy | 2.0 | D1-D2 | 016 | none |
| T1-KH-02 AI-03/AI-04 selected source và persisted draft | Khang | Trung | 2.0 | D3-D4 | 011,012 | P0-02 present |
| T1-DH-01 PrimaryGroup backend semantics/reconciliation | Duy Hoàng | Trung | 2.0 | D1-D2 | 005 | ADR-004 |
| T1-DH-02 Redis fail-fast/session resilience | Duy Hoàng | Trung/Long | 2.0 | D3-D4 | 002,021 | none |
| T1-MN-01 Canonical task open/refresh/back/share | Minh | Long/Trung | 2.0 | D1-D2 | 003 | none |
| T1-MN-02 Privacy create và truthful Settings save | Minh | Trung/Duy | 2.0 | D3-D4 | 017,022 | none |
| T1-LG-01 Clean restore/recovery drill | Long | Duy/Trung | 2.0 | D1-D2 | 019,025 | backup script |
| T1-LG-02 CODEOWNERS/release/config parity | Long | Trung/Duy | 1.5 | D3-D4 | 026,028 | T1-DH-02 before config merge |

Ngày 5: không mở task mới. Trung làm integration owner; cả nhóm rebase/sync,
chạy full suite, preview smoke và sửa regression trong buffer.

### Đợt 2 / Tuần 2

| Task | Owner | Reviewer | Effort | Ngày | Gap | Depends |
|---|---|---|---:|---|---|---|
| T2-TR-03 Project→Group→Meeting→AI→Task vertical E2E | Trung | Khang/Duy | 2.0 | D6 harness + D9 final | 007-010,011-016,018 | contracts D6; feature PRs merged trước final |
| T2-TR-04 Traceability/acceptance/release evidence closure | Trung | Long | 1.5 | D9.5-D10 | 031,032 | all committed PRs |
| T2-KH-03 AI-06/AI-07 locked schema/selective confirm | Khang | Trung/Duy | 2.0 | D6-D7 | 013,014 | T1-KH-02 |
| T2-KH-04 AI-08 SQL metric reconciliation/source refs | Khang | Trung | 2.0 | D8-D9 | 015 | T2-DH-04 contract |
| T2-KH-05 Proactive Automation (Đề xuất xử lý) | Khang | Trung/Duy | 1.5 | D8-D9 | 039 | T2-KH-04 |
| T2-DH-03 Soft-delete relationship integrity | Duy Hoàng | Trung | 2.0 | D8-D9 | 020 | none |
| T2-DH-04 Canonical meeting/action/source resolver backend | Duy Hoàng | Minh/Khang | 2.0 | D6-D7 | 007,023 | T1-DH-01 |
| T2-MN-03 Group↔Project UI và canonical meeting route | Minh | Long/Trung | 2.0 | D6-D7 | 005,007 | T1-DH-01 |
| T2-MN-04 P0 entity resolver UI/QoL truth | Minh | Trung/Duy | 2.0 | D8-D9 | 006,023,024 | T2-DH-04 contract |
| T2-LG-03 Budget UI/warning/hard-stop/degraded | Long | Khang/Trung | 2.0 | D6-D7 | 016 | T1-KH-01 |
| T2-LG-04 Meeting configured/unconfigured + synthetic worker smoke | Long | Khang/Trung | 2.0 | D8-D9 | 008,010 | T2-MN-03 |

Ngày 10: release-candidate smoke, migration/recovery evidence, closure count và
remaining-risk review. Trung là integration owner; Long là release evidence owner.

## 10. Context Pack Cho Từng Task

Mỗi pack dưới đây là context tối thiểu bắt buộc. Agent phải đọc đúng file/commit
được chỉ định trước; không scan lại toàn repo nếu không có evidence mới buộc mở rộng.

### T1-TR-01 — Runtime regression cho ba gap đã tái hiện

- **Owner/reviewer/effort:** Trung; Minh + Duy; 2 ngày.
- **Maps:** REQ-P0-04/07/19, GAP-003/005/017, G-UC-04/07/19.
- **Hiện trạng:** task click không luôn đổi URL; Group không list linked Project;
  Privacy create UI không ghi DB, trong khi E2E hiện mock `/api/privacy/**`.
- **Kết quả:** ba test tái hiện đi qua real local API/SQL; không intercept API cần
  kiểm chứng. Seed/synthetic records được tạo trong setup và cleanup.
- **Đọc trước:** `tests/e2e/privacy-retention.spec.ts:63`, router, `ProjectDetailPage.vue:188`,
  `PrivacySettingsTab.vue:177`, `TeamsPage.vue`; commit `9d98190` và evidence 13/15.
- **Được sửa:** `tests/e2e/*`, test helpers/config; không sửa product source.
- **Cách làm:** thêm test create/read-back policy, click task URL/back, linked project;
  assert request failure/toast và DB/API read-back, không chỉ DOM text.
- **Acceptance:** test fail đúng gap trước fix, pass sau T1-MN-01/T1-MN-02/T1-DH-01;
  refresh giữ entity; không console/page/request error ngoài failure cố ý.
- **Lệnh:** `npx playwright test <new-spec> --project=chromium`; full E2E liên quan.
- **Evidence:** trace/screenshot khi fail, final HTML report, IDs synthetic, cleanup log.
- **Dependency/merge:** có thể viết D1; merge sau ba feature PR để main vẫn xanh.
- **Rủi ro/rollback:** selector bám role/contract, không CSS ngẫu nhiên; revert test PR.
- **Out-of-scope:** LiveKit/media, AI provider, sửa UI.
- **Branch/PR:** `codex/trung-t1-runtime-regression`; `test(P0): prove canonical, group and privacy writes`.

### T1-TR-02 — Comment, evidence và notification permission flow

- **Owner/reviewer/effort:** Trung; Duy; 2 ngày.
- **Maps:** REQ-P0-06/08, GAP-004/006, G-UC-06/08.
- **Hiện trạng:** controller/service có nhưng thiếu executed upload-to-review E2E và
  canonical permission target coverage.
- **Kết quả:** synthetic allow/deny flow: comment mention, upload/link, mark evidence,
  approve/reject, notification open/revoked access/private task.
- **Đọc trước:** Comments/Attachments/Notifications controllers, `TaskService`,
  `NotificationAndWikiPolicyTests`, `TaskAccessPolicyIsolationTests`.
- **Được sửa:** unit/integration/E2E test files và fixture helper; product bug phát
  hiện phải mở finding riêng, không vá lẫn trong test PR.
- **Acceptance:** record persist, review reason/audit đúng, duplicate mention không
  duplicate notification, revoked user không thấy title/excerpt/source.
- **Lệnh:** focused unit + integration; Playwright spec nếu UI surface ổn định.
- **Evidence:** TRX, request/response status, storage cleanup, canonical target output.
- **Dependency/merge:** độc lập; merge trước Day-5 full suite nếu test xanh.
- **Rủi ro/rollback:** file upload phải dùng temp sandbox và cleanup; revert tests.
- **Out-of-scope:** malware engine production, push vendor delivery.
- **Branch/PR:** `codex/trung-t1-evidence-notification-tests`; `test(P0): close evidence and notification boundaries`.

### T1-KH-01 — Budget policy mutation và enforcement

- **Owner/reviewer/effort:** Khang; Duy; 2 ngày.
- **Maps:** REQ-P0-17, REQ-NFR-06, GAP-016, G-UC-17.
- **Hiện trạng:** usage/budget GET và daily/monthly snapshot có; `AiCostService`
  hard-stop chỉ dùng usage hôm nay; thiếu PUT, ownership, audit và concurrency.
- **Kết quả:** tenant/project effective policy create/update/read; daily + monthly
  warning/hard-stop; structured error; usage/provider/cache/mock reconciliation.
- **Đọc trước:** `AiCostService.cs`, `AiPlatformQueryService.cs:85`, `AiController.cs:150`,
  budget entity/config, `AiPlatformQueryServiceTests`; commits `41a6341`, `e844ba7`.
- **Được sửa:** AI DTO/service/controller/configuration và focused tests; migration
  chỉ khi field thật sự thiếu và phải additive.
- **Acceptance:** admin/delegated PM allow; member deny; stale version 409; warning
  không block; hard stop không gọi provider/mock; audit lưu actor/policy/version.
- **Lệnh:** AI budget unit/integration, Release build, EF pending-model check.
- **Evidence:** test names, API examples, SQL read-back, no mock-success negative proof.
- **Dependency/merge:** merge trước T2-LG-03; không chạm budget UI.
- **Rủi ro/rollback:** giữ ledger; disable mutation/UI, dùng last valid policy.
- **Out-of-scope:** billing/payment, currency khác USD.
- **Branch/PR:** `codex/khang-t1-budget-policy`; `feat(P0-04): enforce versioned AI budgets`.

### T1-KH-02 — AI-03 và AI-04 selected-source workflow

- **Owner/reviewer/effort:** Khang; Trung; 2 ngày.
- **Maps:** REQ-P0-12/13, REQ-AI-03/04, GAP-011/012.
- **Hiện trạng:** group summary/action extraction có; selected range identity, cache
  invalidation, structured refs và persisted editable draft chưa đủ.
- **Kết quả:** message IDs/range tạo immutable source hash; summary read-only có
  source URLs; task draft pending review, edit/reject/confirm exactly once.
- **Đọc trước:** `GroupAiController`, `GroupAiService`, `AiWorkflowService`,
  `GroupAiPanel.vue`, `ChatWindow.vue`, AI contract sections 10/12.
- **Được sửa:** Group AI + canonical AI workflow/controller, locked schema, minimal
  chat UI action và focused tests.
- **Acceptance:** unauthorized/deleted/stale message deny; cache invalidates on edit;
  no task before confirm; edited payload + original audited; replay idempotent.
- **Lệnh:** Group AI/AiJob unit + integration; focused frontend typecheck.
- **Evidence:** source IDs/hash/URL, draft state chain, task read-back.
- **Dependency/merge:** P0-02 platform; merge before T2-KH-03 and T2-TR-03.
- **Rủi ro/rollback:** wrapper flag to legacy read; retain draft rows.
- **Out-of-scope:** semantic RAG, arbitrary chat history.
- **Branch/PR:** `codex/khang-t1-chat-source-draft`; `feat(AI-03-04): persist selected-source drafts`.

### T1-DH-01 — PrimaryGroup backend semantics

- **Owner/reviewer/effort:** Duy Hoàng; Trung; 2 ngày.
- **Maps:** REQ-P0-07, REQ-ADR-04, GAP-005, G-UC-07.
- **Hiện trạng:** `Project.SourceGroupId` và create-project-from-group có; không có
  contract list/link/unlink/dissolve/membership mismatch hoàn chỉnh.
- **Kết quả:** compatibility `PrimaryGroupId` semantics; permission-filtered linked
  project list; link/unlink; action-required khi group dissolve; reconciliation query.
- **Đọc trước:** ADR-004, `Project.cs`, `GroupsService.cs:1907`, Groups/Projects
  controllers, GroupsService tests; commits G2/G4 của `duyh-dev`.
- **Được sửa:** domain DTO/service/controller/config/test; schema rename không bắt
  buộc, ưu tiên alias/additive compatibility.
- **Acceptance:** authority cả project/group; mismatch preview không auto-add;
  unlink không delete; dissolve giữ project; cross-tenant deny; existing links list đủ.
- **Lệnh:** Groups unit/integration, EF model parity, Release build.
- **Evidence:** API contract, test matrix, reconciliation row counts.
- **Dependency/merge:** contract phải merge trước T2-MN-03/T2-DH-04.
- **Rủi ro/rollback:** flag/compat DTO; không drop `SourceGroupId`.
- **Out-of-scope:** many-to-many/multiple groups.
- **Branch/PR:** `codex/duyhoang-t1-primary-group`; `feat(P0-06): formalize primary collaboration group`.

### T1-DH-02 — Redis fail-fast và session lifecycle

- **Owner/reviewer/effort:** Duy Hoàng; Trung + Long; 2 ngày.
- **Maps:** REQ-P0-01, REQ-NFR-01/04, REQ-ADR-02, GAP-002/021.
- **Hiện trạng:** fallback hoạt động nhưng unavailable Redis từng gây nhiều giây mỗi
  authenticated request; renewal/revoke/fault coverage chưa đủ.
- **Kết quả:** bounded timeout/circuit state/fallback health; request degraded <=1s;
  login/current/renew/logout/revoke behavior không success giả.
- **Đọc trước:** `RedisTicketStore`, DI Redis config, Auth tests, ADR-002, NFR PERF-03.
- **Được sửa:** Redis auth/config/health và focused resilience tests; không đổi cookie
  format hoặc auth architecture.
- **Acceptance:** fault before request và mid-session; circuit giảm repeated calls;
  fallback bounded/observable; cookie flags/config tests; revoke semantics rõ.
- **Lệnh:** Auth unit/integration + fault test; `scripts/load-smoke.ps1` scoped.
- **Evidence:** p95/max degraded latency, health payload, fallback/circuit logs.
- **Dependency/merge:** merge trước T1-LG-02 config parity.
- **Rủi ro/rollback:** revert timeout/circuit config, giữ ticket serialization.
- **Out-of-scope:** bearer token/external API auth.
- **Branch/PR:** `codex/duyhoang-t1-redis-session`; `fix(P0-05): bound Redis session degradation`.

### T1-MN-01 — Canonical task navigation

- **Owner/reviewer/effort:** Minh; Long + Trung; 2 ngày.
- **Maps:** REQ-P0-04, REQ-NFR-07, GAP-003.
- **Hiện trạng:** direct `/projects/:projectId/tasks/:taskId` rehydrate được; task card
  click có path chỉ gọi `selectTaskInProject`, URL có thể giữ project route.
- **Kết quả:** mọi open path dùng một route action; refresh/share/back/close giữ tab,
  filter, query và đúng task; badge dùng stable task key khi có.
- **Đọc trước:** router, `App.vue:496-545`, `ProjectDetailPage.vue:184-190`, P0-01
  route evidence; commit `9d98190`.
- **Được sửa:** router/App/project/task UI/composable liên quan; không đổi Task API.
- **Acceptance:** Dashboard/Project/Search/Notification open canonical; Back/close đúng;
  invalid/forbidden 403/404/tombstone, không default project sai.
- **Lệnh:** typecheck/build + T1-TR-01 focused E2E.
- **Evidence:** URL sequence, refresh screenshot, no layout/console error desktop/mobile.
- **Dependency/merge:** merge trước test PR T1-TR-01.
- **Rủi ro/rollback:** legacy project route vẫn redirect/compat; revert UI route patch.
- **Out-of-scope:** full resolver for all entities.
- **Branch/PR:** `codex/minh-t1-canonical-task`; `fix(P0-06): canonicalize every task opening`.

### T1-MN-02 — Privacy write và truthful Settings

- **Owner/reviewer/effort:** Minh; Trung + Duy; 2 ngày.
- **Maps:** REQ-P0-19, REQ-NFR-07, GAP-017/022.
- **Hiện trạng:** direct Privacy API create/disable persists; UI create không tạo record
  trong runtime; Settings line 299 báo global success sau lỗi thành phần.
- **Kết quả:** UI mutations đi real API, hiển thị response/error; settings aggregate
  per-operation result và phân biệt device preference/server policy.
- **Đọc trước:** `PrivacySettingsTab.vue:147-218`, `api-client.ts`, `use-toast.ts`,
  `SettingsPage.vue:221-300`, Privacy API integration tests.
- **Được sửa:** Settings/Privacy frontend + real-API E2E phối hợp; không đổi privacy
  domain rule.
- **Acceptance:** create/read-back/disable; invalid/403/CSRF/500 giữ input và không
  success; mixed save liệt kê fail/success; reload phản ánh server state.
- **Lệnh:** typecheck/build, T1-TR-01 real-API Playwright.
- **Evidence:** network status, API/DB read-back ID, toast truth table.
- **Dependency/merge:** merge trước T1-TR-01; backend finding chuyển Duy nếu cần.
- **Rủi ro/rollback:** isolate Privacy tab; revert UI without data migration.
- **Out-of-scope:** tenant legal retention decision.
- **Branch/PR:** `codex/minh-t1-privacy-settings-truth`; `fix(P0-03): persist privacy UI commands truthfully`.

### T1-LG-01 — Clean restore và recovery drill

- **Owner/reviewer/effort:** Long; Duy + Trung; 2 ngày.
- **Maps:** REQ-P0-20, REQ-NFR-08/09, GAP-019/025.
- **Hiện trạng:** backup checksum script có; restore script/clean drill/schema-image
  compatibility/RPO-RTO evidence thiếu.
- **Kết quả:** restore vào isolated target, checksum, migrate, smoke, cleanup; machine
  readable evidence gồm backup/schema/commit/image/timing/result.
- **Đọc trước:** `backup-sqlserver.ps1`, NFR sections 9/12, `docs/12_Release_Readiness.md`,
  P0-03 backup-expiry boundary.
- **Được sửa:** new restore script, recovery runbook/evidence template, safe tests.
- **Acceptance:** clean target success; bad checksum/incompatible schema fail safe;
  source DB không bị overwrite; RPO/RTO recorded; cleanup verified.
- **Lệnh:** backup + restore in disposable LocalDB/container; smoke script.
- **Evidence:** IDs/checksum/timestamps/schema/smoke/cleanup, not credentials.
- **Dependency/merge:** independent; Duy reviews DB safety before execution.
- **Rủi ro/rollback:** absolute target validation; no destructive command on source.
- **Out-of-scope:** production retention/legal approval.
- **Branch/PR:** `codex/long-t1-clean-restore`; `ops(P0-07): prove isolated SQL restore`.

### T1-LG-02 — Git governance và config parity

- **Owner/reviewer/effort:** Long; Trung + Duy; 1.5 ngày.
- **Maps:** REQ-NFR-02/08/10, GAP-026/028.
- **Hiện trạng:** CI/CD có, nhưng CODEOWNERS/tag/protection evidence thiếu; production
  example không khai báo AI/Privacy options đầy đủ.
- **Kết quả:** CODEOWNERS theo module; required-check/branch-protection checklist;
  release evidence template; production config/env parity + fail-safe validation.
- **Đọc trước:** `.github/workflows`, PR template, NFR section 11, DI options,
  appsettings/env/compose, `check-configuration.ps1`.
- **Được sửa:** `.github`, config examples, config validation, docs release template.
- **Acceptance:** no placeholder accepted as secret; missing required config fails/labeled;
  CODEOWNERS paths valid; checks map build/test/security/migration/container.
- **Lệnh:** configuration/container validators, YAML parse, full build smoke.
- **Evidence:** validation output; hosted branch setting remains explicit manual admin item.
- **Dependency/merge:** merge after T1-DH-02 if same Redis/config lines.
- **Rủi ro/rollback:** examples only; revert policy files; never commit credentials.
- **Out-of-scope:** claiming branch protection locally verified.
- **Branch/PR:** `codex/long-t1-git-config-governance`; `chore(P0-07): codify owners and release configuration`.

### T2-TR-03 — Vertical Project→Group→Meeting→AI→Task E2E

- **Owner/reviewer/effort:** Trung; Khang + Duy; 2 ngày.
- **Maps:** REQ-P0-03/07/09..19, GAP-007..018.
- **Hiện trạng:** từng đoạn có test nhưng chưa một replay dùng real app/API/SQL và
  deterministic provider từ project đến source-linked confirmed task.
- **Kết quả:** tạo synthetic project/group/meeting/transcript/policy; enqueue worker;
  review/edit/confirm task; open source backlink; cleanup.
- **Đọc trước:** AI/Privacy E2E, integration factory, meeting/action tests, new contracts
  từ T1-KH-02/T1-DH-01/T2-DH-04.
- **Được sửa:** E2E/test-host adapters/fixtures; không mock Qaly API hoặc persistence.
- **Acceptance:** unauthorized path deny; no task pre-confirm; exactly one task after
  replay; source/back URL works; worker restart/retry and LiveKit unavailable truthful.
- **Lệnh:** focused Playwright + integration; run twice against clean DB.
- **Evidence:** state IDs, DB/API read-back, job/draft/task/audit chain, cleanup.
- **Dependency/merge:** merge after all vertical feature PRs; release blocker.
- **Rủi ro/rollback:** deterministic provider only at transport; no production success claim.
- **Out-of-scope:** real user data/live cloud credential.
- **Branch/PR:** `codex/trung-t2-vertical-e2e`; `test(v4): prove project-to-AI-to-task workflow`.

### T2-TR-04 — Traceability và release evidence closure

- **Owner/reviewer/effort:** Trung; Long; 1.5 ngày.
- **Maps:** REQ-NFR-08/10, REQ-EX-05, GAP-031/032, roadmap P0-08.
- **Hiện trạng:** Doc v4 statuses predate merged/runtime fixes; release gates chưa signed.
- **Kết quả:** update traceability/acceptance/risk/evidence với commit, environment,
  feature flags, test counts, known issues và owner; no unsupported percentages.
- **Đọc trước:** toàn bộ v4 package và merged PR evidence; file plan này.
- **Được sửa:** Doc v4 status/evidence files sau code merge; không đổi requirement scope.
- **Acceptance:** 41 gates mỗi gate có current evidence/status; counts reconcile; no
  Critical unresolved without reject; remaining environment items named.
- **Lệnh:** CSV parse/count script, link/path check, `git diff --check`.
- **Evidence:** closure report and signed decision record; PO signoff may remain external.
- **Dependency/merge:** near-last PR, after full suite and T1-LG-01/02.
- **Rủi ro/rollback:** revert docs only; never upgrade status without artifact.
- **Out-of-scope:** marking Goal Complete.
- **Branch/PR:** `codex/trung-t2-acceptance-closure`; `docs(P0-08): reconcile v4 release evidence`.

### T2-KH-03 — AI-06/AI-07 locked capability flows

- **Owner/reviewer/effort:** Khang; Trung + Duy; 2 ngày.
- **Maps:** REQ-P0-15, REQ-AI-06/07, GAP-013/014.
- **Hiện trạng:** wrappers và generic pending-review draft có; capability validator,
  selective confirm, dependency/checklist mutation evidence thiếu.
- **Kết quả:** versioned `task_breakdown.v4` và `acceptance_checklist.v4`; edit/reorder/
  reject/selective confirm qua normal task policy and transaction.
- **Đọc trước:** AiController wrapper lines 228-262, processor 170/370, validators,
  schemas under `docs/schemas/ai`, task/checklist model.
- **Được sửa:** AI schema/processor/workflow/controller, minimal review UI, tests.
- **Acceptance:** invalid/circular/duplicate schema fail; no pre-confirm writes; selected
  items only; idempotent replay; stale row 409; audit original/working payload.
- **Lệnh:** AI validator/processor/API tests, typecheck, focused E2E.
- **Evidence:** schema fixtures, draft diff, created subtask/checklist read-back.
- **Dependency/merge:** after T1-KH-02 shared source/draft contract.
- **Rủi ro/rollback:** disable wrappers; preserve unconfirmed drafts.
- **Out-of-scope:** autonomous task planning.
- **Branch/PR:** `codex/khang-t2-breakdown-checklist`; `feat(AI-06-07): confirm selective task drafts`.

### T2-KH-04 — AI-08 grounded metric reconciliation

- **Owner/reviewer/effort:** Khang; Trung; 2 ngày.
- **Maps:** REQ-P0-16, REQ-AI-08, GAP-015.
- **Hiện trạng:** project/sprint wrappers có, summary implementations không dùng một
  reconciled metric contract/source refs thống nhất.
- **Kết quả:** SQL metric snapshot cho project/sprint/range; provider chỉ nhận facts;
  narrative + fallback reconcile totals and canonical refs.
- **Đọc trước:** `AiService`, `DashboardSummaryService`, progress wrappers, analytics
  service/tests, T2-DH-04 source contract.
- **Được sửa:** analytics/AI summary DTO/service/schema/tests; UI chỉ khi source display cần.
- **Acceptance:** totals match direct SQL fixture; hidden/private excluded; no-data and
  stale range explicit; deterministic fallback; source IDs/versions/URLs valid.
- **Lệnh:** dashboard/analytics unit + AI integration, focused E2E source open.
- **Evidence:** expected-versus-actual metric table and source links.
- **Dependency/merge:** T2-DH-04 resolver contract before final UI integration.
- **Rủi ro/rollback:** fallback to current read-only summary; no mutation.
- **Out-of-scope:** portfolio forecast/scheduling.
- **Branch/PR:** `codex/khang-t2-grounded-summary`; `feat(AI-08): reconcile project and sprint facts`.

### T2-KH-05 — Proactive Automation & Human-in-the-loop Review (Đề xuất xử lý)

- **Owner/reviewer/effort:** Khang; Trung/Duy; 1.5 ngày.
- **Maps:** REQ-AI-09, GAP-039.
- **Hiện trạng:** Hệ thống AI native có UI draft review, nhưng chưa hỗ trợ luồng proactive automation review để thực thi nhiều action.
- **Kết quả:** Implement backend trigger và draft confirmation cho resolution của delayed projects. Bấm nút "Đề xuất xử lý" sẽ gọi AI tạo một list action (gửi email thông báo, điều chỉnh độ ưu tiên task), cho phép xem lại (review) trước khi đồng ý thực hiện (confirm/execute).
- **Đọc trước:** `AiController`, `AiWorkflowService`, `ConfirmDraftAsync`, `IEmailService`, `INotificationService`.
- **Được sửa:** `AiController`, `AiWorkflowService`, `AiJobProcessor`, `AiGateway` (GetFallbackResponse), và frontend `AiActivityPanel.vue`.
- **Acceptance:**
  - POST `/api/ai/projects/{projectId}/suggest-resolution` tạo job `project_delay_resolution` và trả về 202.
  - Mock/real AI trả về schema `project_delay_resolution.v4` chứa các đề xuất `SendNotification` và `UpdateTask`.
  - Review UI hiển thị thông tin bản nháp rõ ràng.
  - Confirm draft với action `execute_action` sẽ duyệt qua các actions, gửi email, tạo thông báo và cập nhật trạng thái tasks.
- **Branch/PR:** `codex/khang-t2-proactive-automation`; `feat(AI-automation): delayed project resolution flow`.

### T2-DH-03 — Soft-delete relationship integrity

- **Owner/reviewer/effort:** Duy Hoàng; Trung; 2 ngày.
- **Maps:** REQ-NFR-05, GAP-020, G-NFR-03.
- **Hiện trạng:** global filter được áp động; warning/required relationship edge và
  restore/admin query coverage chưa đóng.
- **Kết quả:** inventory principal-dependent; align optionality/filter; zero unresolved
  model warning; active/deleted/restore/admin counts predictable.
- **Đọc trước:** `QalyDbContext` filter setup, entity configurations,
  `SoftDeleteQueryFilterTests`, R-005.
- **Được sửa:** EF configurations/domain optionality/migration nếu bắt buộc, focused
  model + SQL integration tests.
- **Acceptance:** each filter-sensitive relation covers five states; IgnoreQueryFilters
  only named admin/recovery with explicit tenant filter.
- **Lệnh:** model warning capture, unit/integration, EF pending changes/migration script.
- **Evidence:** relationship inventory and count matrix.
- **Dependency/merge:** before versioning backlog; migration owner Duy.
- **Rủi ro/rollback:** expand-compatible migration/flag; no destructive change.
- **Out-of-scope:** generic repository rewrite.
- **Branch/PR:** `codex/duyhoang-t2-soft-delete-integrity`; `fix(P0-05): align soft-delete relationships`.

### T2-DH-04 — Meeting/action/source resolver backend

- **Owner/reviewer/effort:** Duy Hoàng; Minh + Khang; 2 ngày.
- **Maps:** REQ-NFR-07, REQ-P1-02 foundation, GAP-007/023.
- **Hiện trạng:** meeting route uses query ID; action may use index; AI/audit/notification
  sources do not share one permission-aware backend resolver.
- **Kết quả:** P0 resolver contract for Task/Group/Message/Meeting/Action/Wiki/
  Notification/Audit/AI source; stable action ID, safe tombstone and permissions.
- **Đọc trước:** UX map sections 2-5, action mappings, notification/audit/source DTOs,
  `AiSourceGuard`, T1-DH-01.
- **Được sửa:** application resolver contract/service/DTO/controller and tests; no hover UI.
- **Acceptance:** known authorized route/label/actions; forbidden no title/excerpt; deleted
  tombstone; unknown safe; action links exact meeting/task; open rechecks access.
- **Lệnh:** resolver unit/integration and cross-tenant negative tests.
- **Evidence:** entity matrix with allow/deny/deleted outputs.
- **Dependency/merge:** after PrimaryGroup; before T2-MN-04/T2-KH-04.
- **Rủi ro/rollback:** additive endpoint/registry, legacy labels retained unresolved.
- **Out-of-scope:** all P1 hover fields/actions.
- **Branch/PR:** `codex/duyhoang-t2-entity-resolver`; `feat(P0-06): resolve canonical meeting and AI sources`.

### T2-MN-03 — Reciprocal Group/Project UI và meeting route

- **Owner/reviewer/effort:** Minh; Long + Trung; 2 ngày.
- **Maps:** REQ-P0-07, REQ-ADR-04, GAP-005/007.
- **Hiện trạng:** Group Project tab chủ yếu tạo project; linked projects không hiện;
  meeting route `/meeting?meetingId=` không theo v4 target.
- **Kết quả:** project opens primary group; group lists linked projects/status; create/link/
  unlink UI truthful; canonical `/meetings/:meetingId` with legacy redirect.
- **Đọc trước:** T1-DH-01 API, router, Teams/Project/Meeting pages, ADR-004/UX map.
- **Được sửa:** router + Group/Project/Meeting UI; không đổi backend relation.
- **Acceptance:** create/link/share/refresh/back/unlink/dissolve/mismatch/deny states;
  existing legacy join URLs redirect without loss.
- **Lệnh:** typecheck/build, T1-TR-01 and T2-TR-03 focused E2E.
- **Evidence:** desktop/mobile URLs and linked project read-back.
- **Dependency/merge:** T1-DH-01; merge before T2-LG-04.
- **Rủi ro/rollback:** keep query route redirect; feature flag new links if needed.
- **Out-of-scope:** multiple groups per project.
- **Branch/PR:** `codex/minh-t2-group-project-links`; `feat(P0-06): add reciprocal project collaboration links`.

### T2-MN-04 — P0 entity resolver UI/QoL truth

- **Owner/reviewer/effort:** Minh; Trung + Duy; 2 ngày.
- **Maps:** REQ-NFR-07, REQ-P1-02 foundation, GAP-006/023/024.
- **Hiện trạng:** search/notification/audit/AI sources use heterogeneous labels/routes;
  loading/error/back/accessibility behavior không thống nhất.
- **Kết quả:** shared frontend registry consuming T2-DH-04 for P0 entities; canonical
  open/copy link; safe tombstone; fixed preview/loading/error primitives.
- **Đọc trước:** resolver backend contract, App search, notification UI,
  `SourceRefsDrawer.vue`, UX map sections 3/4/9.
- **Được sửa:** shared resolver composable/components and P0 call sites; avoid full page redesign.
- **Acceptance:** authorized links open; forbidden preview no fetch/leak; keyboard/focus/
  Escape/mobile bounds; partial bulk/error remains truthful; Back stable.
- **Lệnh:** typecheck/build, focused Playwright keyboard/mobile/deny.
- **Evidence:** entity coverage table, accessibility assertions, source-open URLs.
- **Dependency/merge:** T2-DH-04; merge after T2-MN-03 router changes.
- **Rủi ro/rollback:** resolver flag/legacy labels; no client authorization assumption.
- **Out-of-scope:** full P1 rich hover actions/member skills.
- **Branch/PR:** `codex/minh-t2-p0-entity-resolver-ui`; `feat(P0-06): unify permission-aware entity links`.

### T2-LG-03 — Budget product surface

- **Owner/reviewer/effort:** Long; Khang + Trung; 2 ngày.
- **Maps:** REQ-P0-17, GAP-016, G-UC-17.
- **Hiện trạng:** AI Activity chỉ jobs/drafts/health; usage/budget GET chưa có admin
  product flow; warning/hard-stop chưa trực quan.
- **Kết quả:** daily/monthly totals, breakdown, remaining, warning/hard-stop, authorized
  policy edit; loading/error/degraded/zero state trung thực.
- **Đọc trước:** T1-KH-01 API, AiActivity panel, Settings patterns, AI contract section 9.
- **Được sửa:** AI activity/settings budget components, API client, focused E2E.
- **Acceptance:** admin/PM role visibility; member deny; policy edit read-back; warning
  allows run; hard stop blocks without fake result; mobile/no overflow.
- **Lệnh:** typecheck/build + budget E2E against real API.
- **Evidence:** API/UI values reconcile, screenshots, deny path.
- **Dependency/merge:** only after T1-KH-01 contract.
- **Rủi ro/rollback:** server flag hides editor, ledger writes stay.
- **Out-of-scope:** invoicing/payment.
- **Branch/PR:** `codex/long-t2-budget-ui`; `feat(P0-04): expose truthful AI budget controls`.

### T2-LG-04 — Meeting degraded/configured và worker smoke

- **Owner/reviewer/effort:** Long; Khang + Trung; 2 ngày.
- **Maps:** REQ-NFR-04, REQ-PLAT-01, GAP-008/010.
- **Hiện trạng:** LiveKit empty displays unavailable; AI worker preview off. Không có một
  one-command clean smoke cho configured/unconfigured transport + synthetic AI worker.
- **Kết quả:** local/CI profile dùng deterministic transport/provider adapter contract;
  unavailable remains error/degraded, configured path creates/join/leave events;
  AI queue reaches succeeded/draft without bypass domain.
- **Đọc trước:** GroupMeeting page, meeting token service/config, AI worker options,
  `.env.example`, compose, existing E2E adapters.
- **Được sửa:** config/test adapters/smoke script + minimal meeting UI degraded state;
  không nhúng credential.
- **Acceptance:** no config -> explicit unavailable; fake config -> contract events; two
  sessions converge; AI job restart/retry/confirm; synthetic labels visible.
- **Lệnh:** one smoke script + T2-TR-03; config validator.
- **Evidence:** config class, event/job timeline, no real-data/provider claim.
- **Dependency/merge:** T2-MN-03 route; T1-KH AI contracts.
- **Rủi ro/rollback:** adapter registered only test/local profile; production fail-safe.
- **Out-of-scope:** production LiveKit SLA/cloud AI topology.
- **Branch/PR:** `codex/long-t2-meeting-ai-smoke`; `test(P0): make meeting and AI degradation reproducible`.

## 11. File Overlap, Conflict và Merge Order

### 11.1 Vùng dễ conflict

| Vùng/file | Task chạm vào | Mức conflict | Quy tắc phối hợp |
|---|---|---:|---|
| `src/Qaly.Web/ClientApp/router/index.ts` | T1-MN-01, T2-MN-03, T2-MN-04 | Cao | Minh giữ ownership; merge đúng thứ tự task; mỗi PR rebase sau PR trước |
| `ProjectDetailPage.vue` và Project components | T1-MN-01, T2-MN-03 | Cao | Tách commit canonical task trước, reciprocal group sau; Long review visual |
| `GroupsService.cs`/`IGroupsService.cs`/Group DTOs | T1-DH-01, T2-DH-04 | Cao | Duy sở hữu cả hai; PrimaryGroup contract merge trước resolver |
| `GroupMeetingPage.vue` | T2-MN-03, T2-LG-04 | Cao | Minh merge route trước; Long chỉ sửa degraded/configured state sau rebase |
| `AiController.cs`/AI DTO/processor | T1-KH-02, T2-KH-03, T2-KH-04 | Cao | Khang làm tuần tự; không mở đồng thời ba branch từ baseline cũ |
| AI cost/query services | T1-KH-01, T2-LG-03 | Trung bình | API contract của Khang merge trước; Long không đổi server DTO |
| Privacy components/API client | T1-MN-02, T1-TR-01 | Trung bình | Minh sửa product; Trung chỉ thêm regression và evidence, không sửa UI trong cùng PR |
| E2E fixtures/specs | T1-TR-01, T1-TR-02, T2-TR-03 | Cao | Trung giữ test harness; mỗi spec có namespace/fixture riêng, vertical E2E merge cuối |
| appsettings/compose/workflows | T1-LG-02, T2-LG-04 | Trung bình | Long giữ config; test adapter chỉ được bật ở test/local profile |
| migration/model snapshot | T1-KH-01, T1-DH-01, T2-DH-03 | Cao | Duy là migration owner; mỗi PR phải rebase và tạo lại idempotent script |
| Evidence/traceability docs | T2-TR-04 và mọi task | Trung bình | Task owner chỉ nộp evidence artifact; Trung cập nhật tài liệu ở PR cuối |

### 11.2 Thứ tự merge bắt buộc

1. **Wave A - test/release guard:** T1-LG-02 phần config validator và T1-TR-01 test tái hiện ở trạng thái đỏ có chủ đích.
2. **Wave B - backend foundations:** T1-DH-01, T1-DH-02, T1-KH-01, T1-KH-02, T1-LG-01.
3. **Wave C - product fixes:** T1-MN-01, T1-MN-02, T1-TR-02; test đỏ phải chuyển xanh bằng implementation thật.
4. **Wave D - capability completion:** T2-KH-03, T2-KH-04, T2-DH-03, T2-DH-04.
5. **Wave E - integration UI:** T2-MN-03, T2-MN-04, T2-LG-03, T2-LG-04.
6. **Wave F - full vertical proof:** T2-TR-03, sau đó T2-TR-04 reconcile CSV/evidence và release verdict.

Không merge PR có migration khi migration owner chưa xác nhận thứ tự. Không squash mất
evidence migration/rollback. Mỗi PR phải rebase trên `main` mới nhất trước required checks;
không dùng force-push lên `main` và không dùng chung working tree.

## 12. Ownership của Implementation Surface

56 `IMP-ID` ở mục 6 là nhóm kiểm kê của 763 file source/test/config sau khi loại
`bin`, `obj`, `dist`, `wwwroot` bundle sinh ra và `node_modules` khỏi source audit. Bảng
sau đóng trường owner module còn thiếu trong bảng inventory; ownership là người điều
phối/review, không phải bằng chứng người đó đã viết mọi file.

| IMP-ID | Owner module | Reviewer/backup | Disposition |
|---|---|---|---|
| IMP-001..003 | Duy Hoàng - auth/core backend | Trung | regression/closure theo task |
| IMP-004 | Long - GitHub/config integration | Duy Hoàng | T1-LG-02 hoặc backlog governance |
| IMP-005..007 | Trung - task QA contract | Duy Hoàng | giữ verified + T1-TR-01/02 |
| IMP-008..012 | Duy Hoàng - Group/Meeting backend | Trung/Khang | T1-DH-01, T2-DH-04, T2-TR-03 |
| IMP-013 | Duy Hoàng - Wiki backend | Minh | GAP-036 backlog |
| IMP-014..017 | Trung - cross-module verification | Duy Hoàng/Long | E2E/reconciliation |
| IMP-018..023 | Khang - AI platform/capability | Trung | T1-KH-01/02, T2-KH-03/04 |
| IMP-024..025 | Duy Hoàng - privacy backend | Trung/Minh | T1-MN-02, T2-TR-03 |
| IMP-026 | Duy Hoàng - audit backend | Trung | resolver integration |
| IMP-027 | Long - optional infrastructure | Khang | GAP-038, flag Off |
| IMP-028..032 | Duy Hoàng - resilience/data | Long/Trung | T1-DH-02, T2-DH-03 |
| IMP-033..034 | Long - recovery | Duy Hoàng | T1-LG-01 |
| IMP-035..045 | Minh - frontend implementation | Long/Trung/Duy | ownership evidence unavailable; reviewer bắt buộc |
| IMP-046..049 | Trung - automated verification | module owner | các task T1/T2 của Trung |
| IMP-050..053 | Long - CI/CD/config/governance | Trung/Duy | T1-LG-02, T2-LG-04 |
| IMP-054 | Duy Hoàng + Khang | Product Owner | GAP-034/035 backlog |
| IMP-055 | Duy Hoàng + Minh | Product Owner | GAP-036 backlog |
| IMP-056 | Duy Hoàng backend, Minh frontend | Trung | T2-DH-04, T2-MN-04 |

## 13. Acceptance và Evidence Matrix (41/41)

`Trạng thái gốc` được giữ nguyên từ `10_Acceptance_Checklist_v4.0.csv`; cột
verification chỉ là cách đóng gate, không tự nâng trạng thái. Synthetic data phải đi qua
API, authorization, transaction và persistence thật; mock chỉ được đứng ở biên provider.

| Gate | Trạng thái gốc | Verification bắt buộc | Task/disposition |
|---|---|---|---|
| G-UC-01 | Partial | integration cookie renewal/revoke + Redis fault/latency | T1-DH-02 |
| G-UC-02 | Verified | giữ project/member allow-deny integration | Regression hiện có |
| G-UC-03 | Verified | cross-tenant direct-ID deny cho Task/Meeting/AI source | T2-TR-03 |
| G-UC-04 | Verified | open/share/refresh/back canonical URL bằng real API | T1-MN-01, T1-TR-01 |
| G-UC-05 | Verified | transition/concurrency/audit/notification regression | Suite hiện có + T2-TR-03 |
| G-UC-06 | Implemented-Unverified | comment/upload/evidence read-back, private deny | T1-TR-02 |
| G-UC-07 | Partial | link/unlink/reciprocal navigation/realtime persistence | T1-DH-01, T2-MN-03, T2-TR-03 |
| G-UC-08 | Verified | persisted idempotent notification + canonical permission target | T1-TR-02, T2-MN-04 |
| G-UC-09 | Implemented-Unverified | import schema/dedupe/privacy/limit trên clean DB | T2-TR-03 |
| G-UC-10 | Verified | extraction draft schema/source regression | Suite hiện có + T2-TR-03 |
| G-UC-11 | Verified | edit/reject/confirm twice tạo đúng một task | T2-TR-03 |
| G-UC-12 | Partial | selected range/cache invalidation/source URL | T1-KH-02 |
| G-UC-13 | Partial | selected messages -> persisted editable draft -> confirm | T1-KH-02 |
| G-UC-14 | Verified | authorized ranking/reason + human confirm | Regression hiện có |
| G-UC-15 | Partial | selective subtask/checklist confirm và duplicate guard | T2-KH-03 |
| G-UC-16 | Partial | SQL metric reconciliation + source links | T2-KH-04 |
| G-UC-17 | Missing | daily/monthly ledger, policy mutation, warning/hard-stop, UI read-back | T1-KH-01, T2-LG-03 |
| G-UC-18 | Verified | timeout/fallback/cache/policy block labels khác nhau | T2-LG-04 regression |
| G-UC-19 | Implemented-Unverified | UI/API/persistence/worker/legal-hold allow-deny | T1-MN-02, T1-TR-01, T2-TR-03 |
| G-UC-20 | Partial | checksum backup -> clean restore -> migration/app smoke | T1-LG-01 |
| G-AI-01 | Implemented-Unverified | manual Meetily import trên clean fixture | T2-TR-03 |
| G-AI-02 | Verified | meeting extraction draft/source regression | T2-TR-03 |
| G-AI-03 | Partial | selected-range summary contract integration | T1-KH-02 |
| G-AI-04 | Partial | source-linked persisted task draft E2E | T1-KH-02 |
| G-AI-05 | Verified | candidate authorization/reason/confirm regression | Suite hiện có |
| G-AI-06 | Partial | persisted breakdown draft/selective confirm | T2-KH-03 |
| G-AI-07 | Missing | capability schema/edit/reject/selective confirm E2E | T2-KH-03 |
| G-AI-08 | Partial | SQL reconciled project/sprint report | T2-KH-04 |
| G-PLAT-01 | Implemented-Unverified | queue/run/result/retry/cancel/restart/idempotency worker smoke | T2-LG-04, T2-TR-03 |
| G-PLAT-02 | Implemented-Unverified | compliance/budget block trả explicit error, không mock success | T1-KH-01, T2-LG-04 |
| G-PRIV-01 | Implemented-Unverified | purpose/consent/provider gate trên mọi meeting path | T1-MN-02, T2-TR-03 |
| G-PRIV-02 | Implemented-Unverified | leased retry/restart/encryption/legal-hold/reconcile | T2-TR-03, T1-LG-01 |
| G-NFR-01 | Implemented-Unverified | typecheck/unit/integration/web-feature trong required CI | T1-LG-02 |
| G-NFR-02 | Verified | dependency scan + approved exception policy | T1-LG-02 regression |
| G-NFR-03 | Partial | zero EF warning + five-state relation matrix | T2-DH-03 |
| G-NFR-04 | Partial | Redis/LiveKit/provider unavailable timeout và truthful health | T1-DH-02, T2-LG-04 |
| G-NFR-05 | Partial | canonical links, keyboard/mobile/deny/tombstone E2E | T1-MN-01, T2-MN-03/04 |
| G-NFR-06 | Partial | CODEOWNERS/protected main/review/check/tag/digest audit | T1-LG-02 + repository admin |
| G-NFR-07 | Missing | application/database backward-compatible recovery drill | T1-LG-01 |
| G-NFR-08 | Partial | module contract/regression trước decomposition | Post-2-week; Long + module owners |
| G-NFR-09 | Spec-only | staging privacy/provider/topology evidence | Post-2-week; Product Owner + Long |

## 14. Business Scenario Matrix

| Luồng | Happy path phải chứng minh | Deny/boundary | Failure/retry/restart |
|---|---|---|---|
| Auth/session | login -> refresh -> current user -> logout/revoke | CSRF sai, user disabled, tenant đổi | Redis down fail-fast; cookie cũ không hồi sinh |
| Project/member | create -> add role -> reload | cross-tenant/direct ID, duplicate member | concurrent role update; audit không mất |
| Task/Kanban | create -> open canonical -> move -> reload | stale row version, private task deny | duplicate submit/idempotency; partial notification |
| Comment/evidence | post/upload/mark reviewed -> read-back | MIME/size/null, non-member/private deny | upload lỗi không tạo success/evidence rỗng |
| Group/Project | link primary group -> navigate hai chiều | second primary, archived/unlinked entity | retry link không duplicate; unlink truthful |
| Message/notification | mention -> persisted notification -> open target | deleted/forbidden source không rò tiêu đề | reconnect/replay không duplicate |
| Meeting | create/join/leave -> stable meeting ID | no consent, missing group/project, bad timezone | LiveKit absent degraded; reconnect converges |
| Import | import valid fixture -> dedupe -> meeting source | malformed/large/Unicode/duplicate | transaction rollback; replay same external ID |
| AI job | queue -> running -> result/draft -> confirm | unauthorized source, invalid schema, budget/compliance block | timeout/retry/cancel/worker restart/idempotency |
| AI-03/04 | select sources -> summary/draft -> source open | empty range, mixed tenant, deleted message | cache invalidates; confirm twice creates once |
| AI-06/07/08 | editable output -> selective confirm/reconciled metrics | malformed model JSON, stale project/sprint | fallback explicit; no invented metric/source |
| Privacy/DSAR | create policy -> read-back -> disable/export/delete | legal hold, no consent, wrong tenant | leased retry/restart, encrypted artifact expiry |
| Recovery/release | backup/checksum -> clean restore -> smoke | wrong version/config/empty backup | failed migration rolls app/data to compatible state |

Các edge case chung bắt buộc cho test phù hợp: `null`, empty, duplicate, stale,
deleted/archived, pagination/filter/sort, timezone, Unicode, malformed payload, large
payload, direct-ID unauthorized, concurrent submit, process restart và partial failure.

## 15. Real-Data Readiness Checklist

Một task không được đóng chỉ vì fixture đẹp. Reviewer dùng checklist sau:

- ID/user/tenant/project/meeting/task được tạo trong arrange/API, không hard-code seed order.
- Fixture dùng cùng DTO/schema/validation/authorization/domain service/transaction với runtime.
- Create -> read-back -> update/transition -> reload hoặc restart -> read-back đã qua.
- Unique/FK/index/concurrency token bảo vệ invariant; duplicate có kết quả xác định.
- Allow và deny dùng hai principal/tenant thật trong test host; direct ID không rò metadata.
- UI success chỉ xuất hiện sau HTTP success và read-back phù hợp; partial failure hiển thị riêng.
- Provider fake chỉ thay external boundary; queue, worker, parser, persistence và confirm vẫn thật.
- Cache/Redis tắt không biến lỗi policy thành success; degraded state có mã và health rõ.
- Worker retry/restart không nhân đôi side effect; cancel/timeout có terminal state hợp lệ.
- Dữ liệu deleted/archived/legal-hold có hành vi đọc/restore/xóa đúng contract.
- Migration chạy từ baseline sạch và từ schema hiện tại; rollback/forward compatibility có evidence.
- Config production thiếu secret/flag bắt buộc phải fail validation, không tự bật mock provider.
- Audit ghi actor, tenant, action, entity, correlation/job ID và không chứa secret/raw sensitive data.
- Evidence ghi rõ `synthetic`, command, commit SHA, config class và thời điểm chạy.

## 16. Cách Làm Việc Trong Hai Tuần

### Nhịp hằng ngày

- 09:00: mỗi người báo `đã xong / đang làm / blocker / file sẽ chạm` trong 5 phút.
- 13:30: module owner kiểm tra contract/migration thay đổi; cập nhật conflict matrix nếu cần.
- 17:00: owner đẩy draft PR nhỏ, gắn GAP/REQ/Gate và evidence hiện có; reviewer phản hồi trong ngày.
- Cuối ngày: Trung cập nhật gate dashboard; Long cập nhật CI/config/recovery; không đổi Doc v4 trong feature PR.

### PR contract

Mỗi PR phải có: task ID; REQ/GAP/Gate; current/target behavior; files; migration/config
impact; automated commands; evidence allow/deny/failure; manual-only step; rollback; out-of-scope.
Giới hạn mục tiêu 1-2 ngày và một outcome. PR không được tự ghi `production-ready`.

### Giới hạn agent/quota

- Một task tương ứng một task/chat agent, dùng Context Pack thay vì yêu cầu scan lại toàn repo.
- Mỗi task có tối đa một outcome backend hoặc frontend chính; thay đổi vượt 2 ngày phải tách PR.
- Codex thường hoặc Gemini Pro phải đủ để thực hiện; không task nào phụ thuộc Codex Plus/Ultra.
- Agent chỉ mở rộng file list khi có compile/test evidence chỉ ra dependency mới và phải ghi vào PR.
- Reviewer chạy verification độc lập; không dùng kết luận tự báo của agent làm acceptance evidence.

### Checkpoint

- **Cuối ngày 2:** test tái hiện GAP-003/GAP-017, contract PrimaryGroup/Budget/Redis được duyệt.
- **Cuối ngày 5:** toàn bộ Wave B/C merge; CI xanh; clean restore đạt hoặc có failure evidence rõ.
- **Cuối ngày 7:** AI-06/07, reciprocal Group UI và resolver backend merge; contract sẵn cho các task D8-D9.
- **Cuối ngày 9:** AI-08, soft-delete integrity, entity/budget UI và worker/configured/degraded smoke merge; vertical E2E final xanh.
- **Ngày 10:** reconcile 41 gates, risk/change log, recovery evidence và PO release decision.

Rollback mặc định là revert PR theo wave và tắt additive feature flag. Migration phải
expand-compatible; destructive cleanup chỉ chạy ở release riêng sau backup/restore proof.

## 17. Product Owner Decisions

Các task kỹ thuật có thể bắt đầu ngay; các quyết định sau chặn sign-off/release, không chặn audit:

| Decision | Cần xác nhận trước | Default an toàn nếu chưa trả lời |
|---|---|---|
| Ai có quyền sửa budget và mức daily/monthly? | T2-LG-03 | chỉ Org Admin; hard stop deny |
| Chính sách consent/retention/legal hold đích? | G-NFR-09 | processing Off khi policy thiếu |
| `PrimaryGroup` có cho relink sau unlink không? | T1-DH-01 acceptance | cho relink có audit; một primary tại một thời điểm |
| RPO/RTO và nơi lưu backup/DSAR artifact? | T1-LG-01 sign-off | không tuyên bố production-ready |
| Ai là repository admin bật protected main/required checks? | T1-LG-02 | không merge release vào unprotected main |
| Có xác nhận Minh là frontend contributor không? | phân quyền review | vẫn giao task frontend bounded; reviewer bắt buộc |

## 18. Bidirectional Traceability Closure

### 18.1 P0 và AI

| REQ-ID | GAP/VER | IMP evidence | Gate | Task/disposition |
|---|---|---|---|---|
| REQ-P0-01 | GAP-002/021 | IMP-001/028 | G-UC-01 | T1-DH-02 |
| REQ-P0-02 | VER-001 | IMP-002/003 | G-UC-02 | regression |
| REQ-P0-03 | VER-002 | IMP-002/003/047 | G-UC-03 | T2-TR-03 |
| REQ-P0-04 | GAP-003 | IMP-005/035/036/049 | G-UC-04 | T1-MN-01, T1-TR-01 |
| REQ-P0-05 | VER-003 | IMP-005/007/046 | G-UC-05 | regression |
| REQ-P0-06 | GAP-004 | IMP-006/047/049 | G-UC-06 | T1-TR-02 |
| REQ-P0-07 | GAP-005 | IMP-008/009/011/036/038 | G-UC-07 | T1-DH-01, T2-MN-03 |
| REQ-P0-08 | GAP-006 | IMP-009/014/044/049 | G-UC-08 | T1-TR-02, T2-MN-04 |
| REQ-P0-09 | GAP-009/018 | IMP-012/017/025/042 | G-UC-09/G-AI-01 | T2-TR-03 |
| REQ-P0-10 | VER-004 | IMP-012/018/019/020 | G-UC-10/G-AI-02 | regression + T2-TR-03 |
| REQ-P0-11 | VER-005 | IMP-012/021 | G-UC-11 | T2-TR-03 |
| REQ-P0-12 | GAP-011 | IMP-009/020/049 | G-UC-12/G-AI-03 | T1-KH-02 |
| REQ-P0-13 | GAP-012 | IMP-020/021/049 | G-UC-13/G-AI-04 | T1-KH-02 |
| REQ-P0-14 | VER-006 | IMP-018/020/021/046 | G-UC-14/G-AI-05 | regression |
| REQ-P0-15 | GAP-013/014 | IMP-020/021/049 | G-UC-15/G-AI-06/G-AI-07 | T2-KH-03 |
| REQ-P0-16 | GAP-015 | IMP-007/016/020/040 | G-UC-16/G-AI-08 | T2-KH-04 |
| REQ-P0-17 | GAP-016 | IMP-018/020/022/041 | G-UC-17 | T1-KH-01, T2-LG-03 |
| REQ-P0-18 | VER-007/GAP-010 | IMP-018/019/022 | G-UC-18/G-PLAT-02 | T2-LG-04 |
| REQ-P0-19 | GAP-017/018/019 | IMP-024/025/041/047/049 | G-UC-19/G-PRIV-01/02 | T1-MN-02, T2-TR-03 |
| REQ-P0-20 | GAP-025 | IMP-033/034/051 | G-UC-20/G-NFR-07 | T1-LG-01 |
| REQ-AI-01 | GAP-009/018 | IMP-012/017/025 | G-AI-01 | T2-TR-03 |
| REQ-AI-02 | VER-004 | IMP-012/019/020 | G-AI-02 | regression |
| REQ-AI-03 | GAP-011 | IMP-009/020 | G-AI-03 | T1-KH-02 |
| REQ-AI-04 | GAP-012 | IMP-020/021 | G-AI-04 | T1-KH-02 |
| REQ-AI-05 | VER-006 | IMP-018/020/021 | G-AI-05 | regression |
| REQ-AI-06 | GAP-013 | IMP-020/021 | G-AI-06 | T2-KH-03 |
| REQ-AI-07 | GAP-014 | IMP-020/021/049 | G-AI-07 | T2-KH-03 |
| REQ-AI-08 | GAP-015 | IMP-007/016/020/040 | G-AI-08 | T2-KH-04 |
| REQ-PLAT-01 | GAP-010 | IMP-018/019/021/022/031 | G-PLAT-01/02 | T2-LG-04, T2-TR-03 |

### 18.2 P1, NFR, ADR và scope disposition

| REQ-ID | GAP/VER | IMP evidence | Verification | Task/disposition |
|---|---|---|---|---|
| REQ-P1-01 | GAP-011..015 | IMP-020/021 | AI gates | committed slices above |
| REQ-P1-02 | GAP-033 | IMP-014/015/023/026/035/044/056 | G-NFR-05 | T2-DH-04/MN-04 foundation; backlog remainder |
| REQ-P1-03 | GAP-034 | IMP-054 missing | schema/constraint/allow-deny | backlog Duy + Product |
| REQ-P1-04 | GAP-035 | IMP-054 missing | deterministic solver scenarios | backlog Khang after P1-03 |
| REQ-P1-05 | GAP-036 | IMP-013/043/055 | snapshot/diff/rollback drill | backlog Duy + Minh |
| REQ-P1-06 | GAP-029/030 | IMP-023/037/039/050 | G-NFR-08 | backlog Long + module owners |
| REQ-NFR-01 | GAP-021/027/029 | IMP-028/037/039/050 | latency/load/bundle gates | T1-DH-02 + backlog Long |
| REQ-NFR-02 | GAP-026/028 | IMP-050/051/052/053 | G-NFR-01/02/06 | T1-LG-02 |
| REQ-NFR-03 | GAP-017..019/037 | IMP-024/025/032/041/049 | G-PRIV-01/02/G-NFR-09 | T1-MN-02, T2-TR-03, backlog policy |
| REQ-NFR-04 | GAP-008/010/021 | IMP-019/028/029/052 | G-NFR-04 | T1-DH-02, T2-LG-04 |
| REQ-NFR-05 | GAP-020/025 | IMP-030/033/034 | G-NFR-03/07 | T2-DH-03, T1-LG-01 |
| REQ-NFR-06 | GAP-010..016 | IMP-018..022 | G-AI-03..08/G-PLAT-01/02 | Khang tasks + T2-LG-04 |
| REQ-NFR-07 | GAP-003/005..007/017/022..024 | IMP-014/015/035..045/056 | G-NFR-05 | Minh/Trung/Duy tasks |
| REQ-NFR-08 | GAP-025..028 | IMP-033/034/050..053 | G-NFR-06/07 | T1-LG-01/02 + backlog metrics |
| REQ-NFR-09 | GAP-019/025 | IMP-025/033/034/051 | G-NFR-07 | T1-LG-01 + policy backlog |
| REQ-NFR-10 | GAP-026/029/030 | IMP-004/023/037/039/050/053 | G-NFR-08 | T1-LG-02 + backlog decomposition |
| REQ-ADR-01 | GAP-026/030 | IMP-004/023/050/053 | architecture regression | backlog after stable tests |
| REQ-ADR-02 | GAP-002/021 | IMP-001/028 | G-UC-01/G-NFR-04 | T1-DH-02 |
| REQ-ADR-03 | GAP-010 | IMP-018/019/021/031 | G-PLAT-01/02 | T2-LG-04/TR-03 |
| REQ-ADR-04 | GAP-005 | IMP-003/008/036/038 | G-UC-07 | T1-DH-01, T2-MN-03 |
| REQ-ADR-05 | GAP-017..019 | IMP-012/024/025/032/041 | G-PRIV-01/02 | privacy tasks + policy backlog |
| REQ-ADR-06 | GAP-034/035 | IMP-054 missing | solver/data contract tests | post-2-week |
| REQ-ADR-07 | GAP-036 | IMP-013/043/055 missing | version/rollback drill | post-2-week |
| REQ-P2-01 | GAP-038 | IMP-027 | flag/config audit | Out-of-scope, Off |
| REQ-P2-02 | GAP-038 | IMP-008/036/038 | scope/relationship test | Out-of-scope, one PrimaryGroup |
| REQ-EX-01 | VER-008 | IMP-021/046 | human-confirm regression | enforce in every AI task |
| REQ-EX-02 | VER-009 | IMP-055 absent by design | architecture audit | no generic event sourcing |
| REQ-EX-03 | VER-010 | IMP-050 monolith build | architecture audit | no unjustified microservices |
| REQ-EX-04 | VER-011 | IMP-012/017 adapter only | import contract test | do not replace Meetily core |
| REQ-EX-05 | GAP-031/032 | IMP-046..053 | evidence/sign-off audit | T2-TR-04 |

Reverse direction đã có tại mục 6: từng `IMP-001` đến `IMP-056` mang REQ hoặc
GAP disposition. Không có endpoint/page/test được tuyên bố verified chỉ vì tồn tại.

## 19. Coverage Closure Report

| Closure metric | Tử số / mẫu số | Kết quả |
|---|---:|---|
| Requirements inventoried | 59 / 59 | 100% |
| Implementation surfaces inventoried | 56 / 56 | 100% |
| Requirement không có GAP/VER disposition | 0 / 59 | 0 |
| Implementation surface không có REQ/GAP/out-of-scope disposition | 0 / 56 | 0 |
| Gap không có owner và sprint/backlog/out-of-scope disposition | 0 / 38 | 0 |
| Acceptance item không có verification method | 0 / 41 | 0 |
| Task cam kết không map REQ và GAP | 0 / 20 | 0 |

**Coverage gate: PASS cho audit inventory và planning closure.** Đây không phải tuyên
bố implementation đã hoàn thành. Trạng thái product vẫn là **chưa đủ release/production
sign-off** cho đến khi 20 task tạo evidence mới, 41 gate được reconcile và các decision
môi trường/chính sách được chủ sở hữu xác nhận.

Các mẫu số được chốt theo snapshot commit `0d9c5f1`: 59 requirement atomic từ scope
v4/ADR/NFR/P1/P2/exclusion; 56 nhóm surface bao trùm 763 file audit-relevant; 38 gap;
41 acceptance gate; 20 task committed. Khi source/doc thay đổi, phải chạy inventory lại
và tăng mẫu số thay vì giữ cứng tỷ lệ 100%.

## 20. Adversarial Review

1. **False-success:** `SettingsPage.vue` có global success sau chuỗi request; runtime Privacy
   create không ghi row. GAP-017/022 và test real API được ưu tiên tuần 1.
2. **Mock blind spot:** Privacy E2E intercept toàn bộ `/api/privacy/**`, vì vậy suite xanh
   không chứng minh UI gọi backend thật. T1-TR-01 phải giữ ít nhất một spec không mock API.
3. **AI-07 overclaim:** wrapper và generic draft mapping không đủ chứng minh validation,
   edit/reject/selective confirm. Trạng thái vẫn Missing/Partial cho tới T2-KH-03.
4. **Environment không phải product bug:** LiveKit/AI worker chưa cấu hình được ghi
   Environment-Blocked; nhưng unavailable/degraded contract và local test adapter vẫn bắt buộc.
5. **Canonical route chưa đủ:** route Task tồn tại nhưng click runtime không luôn push URL;
   Meeting vẫn dùng query route. Code existence không được nâng thành Verified.
6. **PrimaryGroup chưa thành workflow:** FK `SourceGroupId` không chứng minh link hai chiều,
   unlink/relink/invariant hoặc permission. T1-DH-01 đi trước UI.
7. **Recovery overclaim:** backup script không chứng minh restore. G-UC-20/G-NFR-07 vẫn mở.
8. **Governance overclaim:** workflow file không chứng minh branch protection/CODEOWNERS/tag;
   phần hosted cần repository admin evidence.
9. **Ownership uncertainty:** không tìm thấy commit alias chắc chắn của Minh. Plan giới hạn
   blast radius và bắt buộc reviewer, không suy đoán năng lực từ tên.
10. **Test orchestration:** chạy nhiều `dotnet test` song song từng gây DLL lock trong audit;
    đây là lỗi cách chạy local, không phải product regression. Gate dùng lệnh tuần tự.
11. **Doc drift:** CSV/SRS chưa phản ánh trọn P0-02/P0-03 và runtime findings; chỉ T2-TR-04
    được phép reconcile sau evidence, không sửa status trước.
12. **Coverage claim boundary:** 100% chỉ nói mọi item hữu hạn đã có disposition. GAP-027,
    GAP-029/030, GAP-033..037 còn backlog nên không được diễn giải là 100% sản phẩm.

Không phát hiện orphan chưa disposition trong inventory theo grouping rule. Rủi ro còn lại
là surface mới được thêm sau snapshot hoặc runtime path phụ thuộc hạ tầng đích; CI inventory
diff và staging gates phải bắt các thay đổi đó.

## 21. Baseline Verification và Next Action

Audit đã chạy lại tuần tự tại snapshot này:

- Frontend `npm.cmd run typecheck`: **PASS**.
- Unit Release `--no-build --no-restore`: **270/270 PASS**.
- Integration Release: **45/45 PASS**.
- Web-feature Release: **9/9 PASS**.
- Preview cũ tại cổng 5310 không còn chạy khi kết thúc audit; runtime findings trước đó được
  giữ ở trạng thái bằng chứng tái hiện cần T1-TR-01/T2-TR-03, không nâng thành Verified mới.

Hành động tiếp theo là Product Owner phê duyệt board 39 person-days, xác nhận các decision
ở mục 17, rồi tạo 20 task/branch theo đúng Context Pack. Chỉ sau Wave F mới cập nhật status
trong Doc v4 và cân nhắc release candidate.
