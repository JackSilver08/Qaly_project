# QALY v2.1 — WBS/RACI Update cho đúng 7 người

Bản update này thay thế mọi phân công cũ chưa khớp với đội hình thực tế.

## 1. Cấu hình nhóm chính thức

| Mã | Vai trò | Phạm vi chính |
|---|---|---|
| WD1 | Web Dev 1 | Backend core: SQL Server, Auth, RBAC, Organization, Project |
| WD2 | Web Dev 2 | Backend feature: Task, Kanban, Sprint, Timeline, Evidence, Approval |
| WD3 | Web Dev 3 | Frontend + Realtime: Vue UI, SignalR, Mini Zalo, Notification, Customer Portal |
| AI1 | AI Dev 1 | Qdrant, Ollama provider, RAG, AI permission filter, weekly report |
| AI2 | AI Dev 2 | Document Mining, Import pipeline, Meeting summary, draft task generation |
| QA | Tester | Test plan, manual/API/security/UAT, evidence, bug tracking |
| DOC | Documenter | SRS, ERD/UML, traceability, weekly report, user guide, demo script |

## 2. RACI rút gọn theo module

| Module | WD1 | WD2 | WD3 | AI1 | AI2 | QA | DOC |
|---|---|---|---|---|---|---|---|
| SQL Server Migration | A/R | C | I | I | I | C | I |
| Auth/RBAC/Org | A/R | C | C | I | I | C | I |
| Project Management | A/R | C | R(UI) | I | I | C | I |
| Task/Kanban/Approval | C | A/R | R(UI) | C | I | C | I |
| Sprint/Timeline | I | A/R | R(UI) | I | I | C | I |
| Mini Zalo/Notification | C | C | A/R | I | I | C | I |
| Customer Portal | R(API) | C | A/R | I | I | C | C |
| Wiki | C | C | R(UI) | C | R(import) | C | C |
| AI RAG | I | C | C | A/R | C | C | I |
| Document Mining/Import | C | C | C | C | A/R | C | C |
| Audit/Webhook | R | C | C | I | I | C | I |
| QA/UAT | C | C | C | C | C | A/R | C |
| Final Documents/Defense | C | C | C | C | C | C | A/R |

## 3. Timeline 11 tuần sau update

| Tuần | WD1 | WD2 | WD3 | AI1 | AI2 | QA | DOC |
|---|---|---|---|---|---|---|---|
| 1 | Chốt SQL Server, P0 migration list | Task workflow design | UI route/layout audit | AI architecture | Import pipeline design | Test plan draft | SRS v2.1, UML draft |
| 2 | Auth/RBAC/Org migration | Task entity/transition rules | Dashboard/project UI cleanup | Qdrant schema/outbox | CSV/Markdown import spec | TC-RBAC/TC-AUTH | ERD/API docs update |
| 3 | Org/Project API | Task CRUD/API | Project member UI | Ollama provider/MockProvider | Import preview parser | API test collection | Weekly report 1 |
| 4 | Project settings/customer policy | Kanban/approval/evidence | Kanban UI | RAG indexing task/wiki | Draft task generation | Workflow tests | Activity/sequence diagrams |
| 5 | Audit core | Sprint/Timeline/progress | Timeline UI | Permission filter for RAG | Meeting note MVP | Security tests | Traceability update |
| 6 | Webhook basic | Dependency/blocker | Mini Zalo room/message | AI summarize/report | Action item to task | Realtime tests | User guide draft |
| 7 | Customer API | Report/dashboard API | Customer portal UI | AI prompt refinement | Import rollback | UAT scenarios | Customer guide |
| 8 | Hardening RBAC | Performance indexes | Notification polish | AI evaluation dataset | MinerU adapter/mock | Regression cycle 1 | Deployment guide |
| 9 | Integration fixes | Integration fixes | Responsive/PWA fixes | AI fallback/cache | Import fixes | Full system test | Final report draft |
| 10 | Release candidate | Release candidate | Release candidate | AI demo freeze | Import demo freeze | UAT + bug triage | Slide/demo script |
| 11 | Demo support | Demo support | Demo support | Demo support | Demo support | Final evidence | Final package |

## 4. Nguyên tắc nghiệm thu cá nhân

Mỗi thành viên khi kéo task sang Done phải có:

- Link commit/PR hoặc file output.
- Screenshot/log test nếu là UI/API.
- Cập nhật tài liệu nếu thay đổi DB/API/UI.
- Reviewer xác nhận.
- Không còn bug Critical/High liên quan task đó.
