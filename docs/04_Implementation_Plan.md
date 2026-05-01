# 🚀 QALY PROJECT – KẾ HOẠCH TRIỂN KHAI

> **Ngày tạo:** 01/05/2026 | **Phiên bản:** 1.0

---

## I. TIMELINE TỔNG QUAN (8–10 TUẦN)

| Phase | Tên | Thời gian | Trạng thái |
|---|---|---|---|
| 0 | Setup môi trường + Init project | 1–2 ngày | ⬜ Chưa bắt đầu |
| 1 | Domain Layer | 3 ngày | ⬜ Chưa bắt đầu |
| 2 | Infrastructure + DB Migration | 1–2 tuần | ⬜ Chưa bắt đầu |
| 3 | Application Layer (Services) | 2–3 tuần | ⬜ Chưa bắt đầu |
| 4 | Web Layer (UI + Realtime) | 2 tuần | ⬜ Chưa bắt đầu |
| 5 | Testing + Hardening | 2 tuần | ⬜ Chưa bắt đầu |

---

## II. PHÂN BỔ TEAM (7 NGƯỜI)

| Role | Số người | Phụ trách |
|---|---|---|
| Tech Lead | 1 | Review code, thiết kế DB, merge PR |
| Backend Dev | 3 | User+Auth / Project+Task / Notification+Audit |
| Frontend Dev | 2 | Dashboard / Task UI + Vue components |
| QA | 1 | Test flow lifecycle, permission |

---

## III. CHI TIẾT TỪNG PHASE

### Phase 0 – Setup (1–2 ngày)
- [ ] Fix PATH cho dotnet CLI
- [ ] Khởi tạo solution Clean Architecture (4 projects)
- [ ] Cài NuGet packages
- [ ] Cấu hình .gitignore, .editorconfig
- [ ] Setup SQL Server database
- [ ] Commit initial structure

### Phase 1 – Domain Layer (3 ngày)
- [ ] BaseEntity (Id, CreatedAt, UpdatedAt)
- [ ] User entity
- [ ] Project + ProjectMember entities
- [ ] TaskItem + TaskComment + TaskAttachment entities
- [ ] Notification entity
- [ ] AuditLog entity
- [ ] Enums (TaskStatus, TaskPriority, ProjectRole, NotificationType, AuditAction)
- [ ] Interfaces (IRepository, IUnitOfWork, ICurrentUserService)

### Phase 2 – Infrastructure + Migration (1–2 tuần)
- [ ] QalyDbContext
- [ ] Entity Configurations (Fluent API)
- [ ] GenericRepository + UnitOfWork
- [ ] Initial Migration
- [ ] DataSeeder (admin user, sample data)
- [ ] CurrentUserService
- [ ] Verify migration trên SQL Server

### Phase 3 – Application Layer (2–3 tuần)

#### Sprint 3.1 – User + Auth (Backend 1)
- [ ] UserDto, RegisterDto
- [ ] Auth service (login, register, password hash)
- [ ] RBAC middleware

#### Sprint 3.2 – Project + Task (Backend 2)
- [ ] ProjectService (CRUD + member management)
- [ ] TaskService (CRUD + workflow: Todo → InProgress → Done)
- [ ] CommentService

#### Sprint 3.3 – Notification + Audit (Backend 3)
- [ ] NotificationService
- [ ] AuditLogService
- [ ] AuditActionFilter

### Phase 4 – Web Layer (2 tuần)

#### Sprint 4.1 – Layout + Auth Pages (Frontend 1)
- [ ] _Layout.cshtml (sidebar, topbar)
- [ ] Login / Register pages
- [ ] Dashboard page

#### Sprint 4.2 – Project + Task Pages (Frontend 2)
- [ ] Project list + create + details
- [ ] Task list + Kanban board (Vue)
- [ ] Task details + comments (Vue)
- [ ] Notification bell (Vue + SignalR)

### Phase 5 – Testing + Hardening (2 tuần)
- [ ] Unit tests (Services)
- [ ] Integration tests (API + DB)
- [ ] Security: private task, RBAC
- [ ] Performance: SQL Server indexes
- [ ] Load testing (optional)
- [ ] Bug fixing

---

## IV. TASK ASSIGNMENT CHI TIẾT

### Backend Dev 1 – User & Auth
| Task | Priority | Est. |
|---|---|---|
| User entity + configuration | High | 4h |
| ASP.NET Identity setup | High | 8h |
| Login/Register service | High | 8h |
| RBAC middleware | High | 4h |
| Profile service | Medium | 4h |

### Backend Dev 2 – Project & Task
| Task | Priority | Est. |
|---|---|---|
| Project service CRUD | High | 8h |
| ProjectMember management | High | 4h |
| Task service CRUD | High | 8h |
| Task workflow (status transitions) | High | 8h |
| Comment service | Medium | 4h |
| Attachment service | Medium | 4h |

### Backend Dev 3 – Notification & Audit
| Task | Priority | Est. |
|---|---|---|
| Notification service | High | 8h |
| SignalR hub | High | 8h |
| AuditLog service | High | 4h |
| AuditActionFilter | Medium | 4h |
| Search service (LIKE) | Low | 4h |

### Frontend Dev 1 – Layout & Dashboard
| Task | Priority | Est. |
|---|---|---|
| Layout (sidebar, topbar) | High | 8h |
| Login/Register pages | High | 8h |
| Dashboard page | High | 8h |
| Profile page | Medium | 4h |
| CSS design system | High | 8h |

### Frontend Dev 2 – Task UI
| Task | Priority | Est. |
|---|---|---|
| Project list/create pages | High | 8h |
| Task Kanban board (Vue) | High | 16h |
| Task details + comments (Vue) | High | 8h |
| Notification bell (Vue) | Medium | 8h |
| File upload UI | Medium | 4h |

### QA
| Task | Priority | Est. |
|---|---|---|
| Test plan creation | High | 4h |
| Task lifecycle testing | High | 8h |
| Permission testing | High | 8h |
| Cross-browser testing | Medium | 4h |
| Performance testing | Low | 4h |

---

## V. MILESTONE CHECKPOINTS

| Milestone | Ngày dự kiến | Criteria |
|---|---|---|
| M1: Project builds | Tuần 1 | Solution compiles, migration runs |
| M2: Auth works | Tuần 2 | Login/Register functional |
| M3: CRUD complete | Tuần 4 | Project + Task CRUD working |
| M4: UI complete | Tuần 6 | All pages functional |
| M5: Beta release | Tuần 8 | All features + basic testing |
| M6: Production | Tuần 10 | Hardened, load tested |

---

*Cập nhật trạng thái khi hoàn thành từng task.*
