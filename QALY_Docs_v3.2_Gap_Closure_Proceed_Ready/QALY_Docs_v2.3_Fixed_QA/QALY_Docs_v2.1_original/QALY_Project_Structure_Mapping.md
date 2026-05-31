# QALY Workspace — Project Structure Mapping v2.0

**Phiên bản:** v2.0 — Production/Japan-style specification  
**Ngày:** 16/05/2026  
**Phạm vi:** Dự án web quản lý dự án phần mềm cho doanh nghiệp outsource vừa và nhỏ.  
**Định hướng:** Jira + Notion + Mini Zalo + AI Assistant + Document Mining.  
**Ghi chú kiến trúc:** Tài liệu v2.0 mở rộng từ SRS/TKHT v1.0. Các phần dưới đây là target design để nhóm có thể triển khai 80–90% chức năng web trong 10–11 tuần, không bắt buộc implement toàn bộ bảng P2 nếu thiếu thời gian.

---


## 1. Mục tiêu mapping

Tài liệu này giúp nhóm map đặc tả v2.0 vào codebase Clean Architecture hiện tại, tránh tình trạng tài liệu đi một đường, code đi một đường.

## 2. Cấu trúc repository mục tiêu

```text
Qaly_project/
├── src/
│   ├── Qaly.Domain/
│   │   ├── Common/                # BaseEntity, IHasTenant, AuditableEntity
│   │   ├── Tenancy/               # Tenant, TenantSetting
│   │   ├── Identity/              # User, Session, ApiKey
│   │   ├── Organizations/         # Organization, OrganizationMember, Role
│   │   ├── Projects/              # Project, ProjectMember, ProjectSetting
│   │   ├── Tasks/                 # Task, Assignment, Dependency, Evidence, Approval
│   │   ├── Boards/                # KanbanBoard, Column, CardOrder
│   │   ├── Sprints/               # Sprint, SprintTask, Capacity
│   │   ├── Collaboration/         # Comment, ChatRoom, ChatMessage, Meeting
│   │   ├── Knowledge/             # WikiPage, WikiVersion, File, ImportJob
│   │   ├── Notifications/         # Notification, Preference, Digest
│   │   ├── Integrations/          # Webhook, GitHub/GitLab records
│   │   ├── Ai/                    # AiConversation, KnowledgeDocument, Chunk
│   │   └── Audit/                 # AuditLog, DataAccessLog
│   ├── Qaly.Application/
│   │   ├── Common/                # Result, Pagination, CurrentUser, Clock
│   │   ├── DTOs/{Module}/
│   │   ├── Validators/{Module}/
│   │   ├── Services/{Module}/     # interfaces + application services
│   │   ├── Policies/              # RBAC 3 tầng, resource access policies
│   │   ├── Workflows/             # TaskWorkflowService, SprintWorkflowService
│   │   ├── Events/                # domain/application events
│   │   └── Mappings/
│   ├── Qaly.Infrastructure/
│   │   ├── Data/                  # QalyDbContext, migrations, seed
│   │   ├── Data/Configurations/   # EF configurations theo module
│   │   ├── Repositories/
│   │   ├── Services/FileStorage/  # Local/MinIO/S3 adapter
│   │   ├── Services/Ai/           # Ollama/OpenAICompatible/Mock provider
│   │   ├── Services/Realtime/
│   │   ├── Services/Email/
│   │   ├── Services/Import/
│   │   └── Workers/               # deadline, digest, vector sync, import jobs
│   └── Qaly.Web/
│       ├── Controllers/v1/{Module}Controller.cs
│       ├── Hubs/NotificationHub.cs, ChatHub.cs, ProjectHub.cs, AiHub.cs
│       ├── Middleware/CorrelationId, ExceptionHandler, TenantResolver
│       ├── Pages/Account, Index shell
│       └── ClientApp/
│           ├── src/app/           # router, api client, signalr client
│           ├── src/modules/       # dashboard, projects, tasks, chat, wiki, admin
│           ├── src/shared/        # UI components, composables, stores
│           └── src/styles/
├── tests/
│   ├── Qaly.UnitTests/
│   ├── Qaly.IntegrationTests/
│   ├── Qaly.E2ETests/
│   └── Qaly.TestData/
├── docs/
│   ├── QALY_Docs_v2.0/
│   └── diagrams/
├── docker-compose.yml
├── Dockerfile
└── .github/workflows/
```

## 3. Mapping module → code → DB → API → UI → test

| Module | Domain entities | Application services | Infrastructure | Web/API | UI routes/components | Test scope | Owner đề xuất |
|---|---|---|---|---|---|---|---|
| Auth/RBAC | User, UserSession, ApiKey | AuthService, RbacPolicyService | CookieTicketStore, RedisSession | AuthController, UsersController | Login, Profile | Auth/RBAC integration | Web Dev 1 |
| Organization | Organization, OrgMember, Invitation | OrganizationService | EF configs, email invitation | OrganizationsController | Org settings/members | Org CRUD + invite | Web Dev 1 |
| Project | Project, ProjectMember, ProjectSetting | ProjectService, ProjectAccessPolicy | Project repo, seed | ProjectsController | ProjectsPage, ProjectDetail | Project CRUD/member | Web Dev 1 + FE |
| Task/Workflow | Task, Assignment, Dependency, Approval | TaskService, TaskWorkflowService | EF configs, outbox | TasksController | Kanban, TaskDetail | Workflow/security | Web Dev 2 |
| Kanban/Sprint/Timeline | Board, Column, Sprint, Milestone | BoardService, SprintService, TimelineService | Gantt query/index | Kanban/Sprints/Timeline endpoints | Board, Timeline, Stats | Drag/drop, progress | Web Dev 2 + FE |
| Chat/Realtime | ChatRoom, ChatMessage | ChatService | SignalR backplane/Redis | ChatController, ChatHub | Teams/Chat panel | Socket tests | Web Dev 3 |
| Wiki/File/Import | WikiPage, File, ImportJob | WikiService, FileService, ImportService | MinIO/Local, parser | WikiController, FilesController, ImportController | Wiki, Import wizard | File/security/import | AI Dev 2 + FE |
| AI Assistant | AiConversation, KnowledgeChunk | AiService, RAGService | Ollama/Qdrant providers | AiController, AiHub | AI Assistant drawer | RAG permission tests | AI Dev 1 |
| Admin/Audit | AuditLog, FeatureFlag | AdminService, AuditService | Seq/log export | AdminController | Admin pages | Audit/feature flag | Web Dev 3 |

## 4. Mapping DB priority sang migration

| Priority | Cách làm trong code |
|---|---|
| P0 | Tạo entity + EF configuration + migration + seed + integration test |
| P1 | Entity + migration nếu UI/API cần; nếu chưa kịp thì feature flag off nhưng schema vẫn có thể tạo |
| P2 | Chỉ tạo khi thật sự cần; còn lại để docs/roadmap để không làm cứng hệ thống |

## 5. Quy tắc code production/Japan-style

- Mỗi service method phải nhận `CancellationToken`.
- Mỗi command thay đổi dữ liệu phải có audit event hoặc audit log.
- Mỗi API write phải có validator, permission check, transaction boundary.
- Không trả entity trực tiếp ra API; dùng DTO/ViewModel.
- Không dùng string literal role/status rải rác; đưa vào enum/value object/constant.
- Query customer phải đi qua `VisibilityPolicy` và `ProjectAccessPolicy`.
- AI retrieval phải gọi `AiPermissionFilter` trước khi build context.
- Mọi lỗi trả format thống nhất, có `correlationId`.
