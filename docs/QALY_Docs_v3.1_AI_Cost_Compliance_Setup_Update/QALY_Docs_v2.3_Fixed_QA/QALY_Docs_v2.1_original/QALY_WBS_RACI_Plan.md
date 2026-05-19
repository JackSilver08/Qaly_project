# QALY Workspace — WBS + RACI + 11-week Delivery Plan v2.0

**Phiên bản:** v2.0 — Production/Japan-style specification  
**Ngày:** 16/05/2026  
**Phạm vi:** Dự án web quản lý dự án phần mềm cho doanh nghiệp outsource vừa và nhỏ.  
**Định hướng:** Jira + Notion + Mini Zalo + AI Assistant + Document Mining.  
**Ghi chú kiến trúc:** Tài liệu v2.0 mở rộng từ SRS/TKHT v1.0. Các phần dưới đây là target design để nhóm có thể triển khai 80–90% chức năng web trong 10–11 tuần, không bắt buộc implement toàn bộ bảng P2 nếu thiếu thời gian.

---

# PHẦN 1 — Work Breakdown Structure

## 1.0 Initiation & Specification
- 1.0.1 **Chốt scope v2.0** — Documenter — 3 ngày — Dependency: None
- 1.0.2 **Thiết kế ERD/RBAC** — Web Dev 1 — 3 ngày — Dependency: 1.1
- 1.0.3 **Tạo backlog import** — Documenter — 2 ngày — Dependency: 1.1
- 1.0.4 **Test plan draft** — Tester — 2 ngày — Dependency: 1.1

## 2.0 Core Architecture
- 2.0.1 **BaseEntity/Tenant/RBAC policy** — Web Dev 1 — 4 ngày — Dependency: 1.2
- 2.0.2 **EF configurations P0** — Web Dev 1 — 4 ngày — Dependency: 2.1
- 2.0.3 **Frontend shell/router/stores** — Web Dev 3 — 4 ngày — Dependency: 1.1
- 2.0.4 **CI build/test docker** — Web Dev 3 — 2 ngày — Dependency: None

## 3.0 Project & Organization
- 3.0.1 **Organization CRUD/invite** — Web Dev 1 — 5 ngày — Dependency: 2.1
- 3.0.2 **Project CRUD/settings/member** — Web Dev 1 — 5 ngày — Dependency: 3.1
- 3.0.3 **Project UI/member UI** — Web Dev 3 — 4 ngày — Dependency: 3.2
- 3.0.4 **Integration tests** — Tester — 3 ngày — Dependency: 3.2

## 4.0 Task/Kanban/Approval
- 4.0.1 **Task CRUD/assignment** — Web Dev 2 — 5 ngày — Dependency: 3.2
- 4.0.2 **Workflow/status rules** — Web Dev 2 — 4 ngày — Dependency: 4.1
- 4.0.3 **Evidence/approval** — Web Dev 2 — 4 ngày — Dependency: 4.2
- 4.0.4 **Kanban drag/drop UI** — Web Dev 3 — 5 ngày — Dependency: 4.1
- 4.0.5 **Task workflow tests** — Tester — 4 ngày — Dependency: 4.3

## 5.0 Sprint/Timeline/Report
- 5.0.1 **Sprint/phase API** — Web Dev 2 — 4 ngày — Dependency: 4.1
- 5.0.2 **Timeline/Gantt query** — Web Dev 2 — 4 ngày — Dependency: 5.1
- 5.0.3 **Stats/report dashboard** — Web Dev 3 — 4 ngày — Dependency: 5.2
- 5.0.4 **Progress tests** — Tester — 3 ngày — Dependency: 5.2

## 6.0 Chat/Notification/Webhook
- 6.0.1 **Chat room/message API** — Web Dev 3 — 4 ngày — Dependency: 3.2
- 6.0.2 **SignalR ChatHub** — Web Dev 3 — 4 ngày — Dependency: 6.1
- 6.0.3 **Task from chat** — Web Dev 3 — 3 ngày — Dependency: 4.1
- 6.0.4 **Notification + digest** — Web Dev 3 — 3 ngày — Dependency: 4.1
- 6.0.5 **Webhook delivery/retry** — Web Dev 3 — 3 ngày — Dependency: 6.4

## 7.0 Wiki/Import/Customer
- 7.0.1 **Wiki versioning** — AI Dev 2 — 4 ngày — Dependency: 3.2
- 7.0.2 **File storage/MinIO adapter** — AI Dev 2 — 4 ngày — Dependency: 2.1
- 7.0.3 **CSV/Markdown import preview** — AI Dev 2 — 5 ngày — Dependency: 7.1
- 7.0.4 **Customer portal filter** — Web Dev 1 — 4 ngày — Dependency: 3.2
- 7.0.5 **UI wiki/customer** — Web Dev 3 — 4 ngày — Dependency: 7.4

## 8.0 AI Modules
- 8.0.1 **IAiProvider/Ollama/Mock** — AI Dev 1 — 4 ngày — Dependency: 2.1
- 8.0.2 **Qdrant sync job** — AI Dev 1 — 5 ngày — Dependency: 8.1
- 8.0.3 **RAG permission filter** — AI Dev 1 — 4 ngày — Dependency: 8.2
- 8.0.4 **AI report/summarize** — AI Dev 1 — 4 ngày — Dependency: 8.3
- 8.0.5 **Document mining adapter** — AI Dev 2 — 5 ngày — Dependency: 7.3

## 9.0 Hardening & Release
- 9.0.1 **Security/IDOR tests** — Tester — 5 ngày — Dependency: All
- 9.0.2 **Regression/UAT** — Tester — 5 ngày — Dependency: All
- 9.0.3 **Bugfix and performance** — All Devs — 5 ngày — Dependency: 9.2
- 9.0.4 **Final thesis/docs/slide** — Documenter — 7 ngày — Dependency: All

# PHẦN 2 — RACI Matrix

| Deliverable | Web Dev 1 | Web Dev 2 | Web Dev 3 | AI Dev 1 | AI Dev 2 | Tester | Documenter |
|---|---|---|---|---|---|---|---|
| Initiation & Specification | C | I | I | I | I | C | A/R |
| Core Architecture | A/R | I | I | I | I | C | I |
| Project & Organization | A/R | I | I | I | I | C | I |
| Task/Kanban/Approval | I | A/R | C | I | I | C | I |
| Sprint/Timeline/Report | I | A/R | C | I | I | C | I |
| Chat/Notification/Webhook | I | C | A/R | I | I | C | I |
| Wiki/Import/Customer | I | I | C | I | A/R | C | I |
| AI Modules | I | I | I | A/R | C/R | C | I |
| Hardening & Release | C | C | C | C | C | A/R | R |

# PHẦN 3 — Timeline 11 tuần

| Tuần | Web Dev 1 | Web Dev 2 | Web Dev 3 | AI Dev 1 | AI Dev 2 | Tester | Documenter |
|---|---|---|---|---|---|---|---|
| 1 | Scope/RBAC draft | Review task design | UI shell plan | AI architecture | Import architecture | Test plan draft | SRS v2.0 |
| 2 | Tenant/RBAC base | Task entity draft | Vue shell/router | IAiProvider draft | File storage draft | Auth/RBAC tests | ERD/API docs |
| 3 | Org/Project API | Task CRUD API | Project UI | Qdrant setup | Wiki draft | Project tests | Weekly report |
| 4 | Project settings | Workflow/Approval | Kanban UI | Vector sync | File upload | Task tests | Use cases |
| 5 | Customer policy | Sprint/Timeline | Dashboard stats | RAG filter | Import preview | Progress tests | BR/workflow docs |
| 6 | Customer portal API | Dependency/Gantt | Chat/SignalR | AI prompt templates | Markdown import | Realtime tests | WBS/RACI update |
| 7 | Security policy | Report export | Chat task link | AI report | MinerU adapter/mock | Import/customer tests | User guide draft |
| 8 | Admin config | Bugfix backend | Wiki/customer UI | Meeting summary | Draft task gen | AI tests | Slide draft |
| 9 | Integration fix | Integration fix | Responsive/PWA | Fallback/cache | Import hardening | Security/IDOR | Deployment guide |
| 10 | UAT fixes | UAT fixes | UI polish | AI polish | Doc mining polish | Regression/UAT | Final report |
| 11 | Demo support | Demo support | Demo support | Demo support | Demo support | Smoke/UAT signoff | Defense package |

# PHẦN 4 — Individual Work Packages

## Web Dev 1

**Mục tiêu cá nhân:** Hoàn thành module được giao với code, test, evidence và tài liệu khớp đặc tả.

**Deliverable theo tuần:** xem Timeline 11 tuần.

**Definition of Done cá nhân:**
- [ ] Code build pass.
- [ ] Unit/integration/manual test liên quan pass.
- [ ] API/DB/UI thay đổi đã cập nhật tài liệu.
- [ ] PR được review.
- [ ] Có evidence screenshot/log/video ngắn nếu cần.

**Rủi ro:** quá tải scope, dependency người khác, merge conflict; mitigation: chia PR nhỏ, daily sync, feature flag.

## Web Dev 2

**Mục tiêu cá nhân:** Hoàn thành module được giao với code, test, evidence và tài liệu khớp đặc tả.

**Deliverable theo tuần:** xem Timeline 11 tuần.

**Definition of Done cá nhân:**
- [ ] Code build pass.
- [ ] Unit/integration/manual test liên quan pass.
- [ ] API/DB/UI thay đổi đã cập nhật tài liệu.
- [ ] PR được review.
- [ ] Có evidence screenshot/log/video ngắn nếu cần.

**Rủi ro:** quá tải scope, dependency người khác, merge conflict; mitigation: chia PR nhỏ, daily sync, feature flag.

## Web Dev 3

**Mục tiêu cá nhân:** Hoàn thành module được giao với code, test, evidence và tài liệu khớp đặc tả.

**Deliverable theo tuần:** xem Timeline 11 tuần.

**Definition of Done cá nhân:**
- [ ] Code build pass.
- [ ] Unit/integration/manual test liên quan pass.
- [ ] API/DB/UI thay đổi đã cập nhật tài liệu.
- [ ] PR được review.
- [ ] Có evidence screenshot/log/video ngắn nếu cần.

**Rủi ro:** quá tải scope, dependency người khác, merge conflict; mitigation: chia PR nhỏ, daily sync, feature flag.

## AI Dev 1

**Mục tiêu cá nhân:** Hoàn thành module được giao với code, test, evidence và tài liệu khớp đặc tả.

**Deliverable theo tuần:** xem Timeline 11 tuần.

**Definition of Done cá nhân:**
- [ ] Code build pass.
- [ ] Unit/integration/manual test liên quan pass.
- [ ] API/DB/UI thay đổi đã cập nhật tài liệu.
- [ ] PR được review.
- [ ] Có evidence screenshot/log/video ngắn nếu cần.

**Rủi ro:** quá tải scope, dependency người khác, merge conflict; mitigation: chia PR nhỏ, daily sync, feature flag.

## AI Dev 2

**Mục tiêu cá nhân:** Hoàn thành module được giao với code, test, evidence và tài liệu khớp đặc tả.

**Deliverable theo tuần:** xem Timeline 11 tuần.

**Definition of Done cá nhân:**
- [ ] Code build pass.
- [ ] Unit/integration/manual test liên quan pass.
- [ ] API/DB/UI thay đổi đã cập nhật tài liệu.
- [ ] PR được review.
- [ ] Có evidence screenshot/log/video ngắn nếu cần.

**Rủi ro:** quá tải scope, dependency người khác, merge conflict; mitigation: chia PR nhỏ, daily sync, feature flag.

## Tester

**Mục tiêu cá nhân:** Hoàn thành module được giao với code, test, evidence và tài liệu khớp đặc tả.

**Deliverable theo tuần:** xem Timeline 11 tuần.

**Definition of Done cá nhân:**
- [ ] Code build pass.
- [ ] Unit/integration/manual test liên quan pass.
- [ ] API/DB/UI thay đổi đã cập nhật tài liệu.
- [ ] PR được review.
- [ ] Có evidence screenshot/log/video ngắn nếu cần.

**Rủi ro:** quá tải scope, dependency người khác, merge conflict; mitigation: chia PR nhỏ, daily sync, feature flag.

## Documenter

**Mục tiêu cá nhân:** Hoàn thành module được giao với code, test, evidence và tài liệu khớp đặc tả.

**Deliverable theo tuần:** xem Timeline 11 tuần.

**Definition of Done cá nhân:**
- [ ] Code build pass.
- [ ] Unit/integration/manual test liên quan pass.
- [ ] API/DB/UI thay đổi đã cập nhật tài liệu.
- [ ] PR được review.
- [ ] Có evidence screenshot/log/video ngắn nếu cần.

**Rủi ro:** quá tải scope, dependency người khác, merge conflict; mitigation: chia PR nhỏ, daily sync, feature flag.

# PHẦN 5 — Risk Register

| Risk ID | Mô tả | Probability | Impact | Mitigation | Owner |
|---|---|---|---|---|---|
| R-001 | Scope quá lớn | High | High | P0/P1/P2, P0 trước | PM/Documenter |
| R-002 | AI local chậm/chất lượng thấp | Medium | High | Mock/fallback/cache/quota | AI Dev 1 |
| R-003 | RBAC 3 tầng lỗi | Medium | High | Test matrix IDOR/cross-tenant | Web Dev 1 + Tester |
| R-004 | Chat realtime tốn thời gian | Medium | Medium | Tối thiểu group chat + task from message | Web Dev 3 |
| R-005 | MinerU khó tích hợp | Medium | Medium | CSV/Markdown pipeline trước, MinerU adapter sau | AI Dev 2 |
| R-006 | Demo Docker lỗi | Medium | High | Seed script, video dự phòng, health check | Tester |
