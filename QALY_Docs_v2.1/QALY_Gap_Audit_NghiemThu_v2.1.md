# QALY v2.1 — Gap Audit, Nghiệm Thu & Plan Update

**Mục tiêu:** kiểm tra toàn bộ plan/context hiện tại, cập nhật hướng SQL Server, và tạo bộ tiêu chí nghiệm thu để nhóm có thể triển khai 80–90% web một cách có kiểm soát.

---

## 1. Kết luận audit

| Hạng mục | Đánh giá | Kết luận |
|---|---|---|
| Scope tổng | Rộng nhưng hợp lý cho 7 người nếu chia P0/P1/P2 | Giữ scope, không code toàn bộ 188 bảng ngay. |
| AI | Ollama + Qdrant là đúng hướng | Model nhỏ chỉ dùng tác vụ nhẹ; RAG cần permission filter. |
| Database | SQL Server phù hợp hơn codebase hiện tại | Chốt SQL Server 2022 cho đồ án, Qdrant giữ vector. |
| RBAC | Đúng nhưng rủi ro cao | Cần policy service + test matrix. |
| Mini Zalo | Là điểm khác biệt | P1 phải có group chat + task from message. |
| Customer Portal | Rất quan trọng | Phải demo được filtered view, không lộ dữ liệu internal/private. |
| Document Mining | Có giá trị nhưng dễ quá tải | MVP bằng Markdown/CSV import; MinerU adapter P2 nếu không kịp. |
| Japan-style process | Hướng đúng | Cần gate nghiệm thu và evidence mỗi sprint. |

---

## 2. Quyết định cập nhật plan

### 2.1 Database

- Trước: v2.0 dùng PostgreSQL làm logical DB target.
- Sau update: **SQL Server 2022 là implementation target chính**.
- PostgreSQL trong v2.0 chỉ còn là reference logical design; mọi migration/code demo dùng SQL Server.

### 2.2 AI

- Giữ Ollama + Qdrant.
- Không trình bày rằng “Ollama 1B là AI chính”.
- Trình bày là: **Ollama là runtime local; model có thể thay đổi; Qdrant là vector store; External API là optional fallback**.

### 2.3 Scope 7 người

Đúng cấu hình nhóm:

1. Web Dev 1 — Auth/RBAC/Organization/Project/SQL Server migration.
2. Web Dev 2 — Task/Kanban/Sprint/Timeline/Approval/Evidence.
3. Web Dev 3 — Frontend/Realtime/Mini Zalo/Notification/Customer Portal UI.
4. AI Dev 1 — Qdrant/RAG/Ollama provider/AI permission filter/weekly report.
5. AI Dev 2 — Document Mining/Import pipeline/Meeting summary/action item.
6. Tester — Test plan/test cases/security/UAT/evidence.
7. Documenter — SRS, ERD/UML, traceability, weekly report, user guide, demo script.

---

## 3. Nghiệm thu theo Gate

### Gate 0 — Documentation Baseline

**Điều kiện đạt:**

- [ ] SRS v2.1 có quyết định SQL Server.
- [ ] DB dictionary có mapping PostgreSQL → SQL Server.
- [ ] WBS/RACI đúng 7 người.
- [ ] Use case + business rules + test cases đã map traceability.
- [ ] UML tối thiểu: use case, class, activity task lifecycle, sequence create task/update status, component, deployment.

**Evidence:** file tài liệu, link repo docs, checklist ký duyệt tuần.

---

### Gate 1 — Database & Architecture

**Điều kiện đạt:**

- [ ] SQL Server Docker chạy ổn định.
- [ ] EF Core migration tạo được database rỗng.
- [ ] P0 tables có migration thật.
- [ ] Seed data có đủ role: admin, org owner, PM, developer, reviewer, customer.
- [ ] Query filter tenant/project hoạt động.
- [ ] Health check SQL/Redis/Qdrant/App xanh.

**Evidence:** migration log, screenshot DB, health check JSON, seed account list.

---

### Gate 2 — RBAC & Security

**Điều kiện đạt:**

- [ ] Backend enforce quyền, không chỉ ẩn nút frontend.
- [ ] Cross-tenant/cross-project access bị 403/404.
- [ ] Customer không xem internal/private data.
- [ ] Admin action có audit log.
- [ ] Upload file nguy hiểm bị chặn.

**Evidence:** TC-RBAC/TC-SECURITY pass, audit screenshot.

---

### Gate 3 — Core Project & Task Workflow

**Điều kiện đạt:**

- [ ] Tạo organization/project/member/customer.
- [ ] Tạo task, assign nhiều người, due date, priority, visibility.
- [ ] Kanban kéo thả status hợp lệ.
- [ ] Evidence + approval request + approve/reject hoạt động.
- [ ] Progress cập nhật đúng.
- [ ] Timeline/Gantt MVP có task start/due/dependency.

**Evidence:** demo script + screenshots + test cases.

---

### Gate 4 — Collaboration & Customer Transparency

**Điều kiện đạt:**

- [ ] Project chat room gửi/nhận realtime.
- [ ] Tạo task từ message.
- [ ] Notification assign/mention/review/deadline hoạt động.
- [ ] Customer portal filtered view hoạt động.
- [ ] Wiki public/internal phân quyền đúng.

**Evidence:** video demo hoặc live demo + audit/task link.

---

### Gate 5 — AI & Import

**Điều kiện đạt:**

- [ ] Qdrant sync project/task/wiki/meeting metadata.
- [ ] RAG trả lời có source/citation nội bộ.
- [ ] AI không leak private/internal data.
- [ ] Ollama local chạy được hoặc MockProvider fallback.
- [ ] Import Markdown/CSV preview → approve → commit thành task/wiki.
- [ ] Meeting note summary/action item ở mức MVP.

**Evidence:** AI test dataset, prompt output, import job log.

---

### Gate 6 — QA/UAT/Defense Readiness

**Điều kiện đạt:**

- [ ] Smoke test pass toàn bộ demo path.
- [ ] Critical/High bug = 0 trước ngày bảo vệ.
- [ ] Test evidence có screenshot/log.
- [ ] Backup/restore runbook có thể trình bày.
- [ ] Slide + script demo + video backup sẵn sàng.

**Evidence:** UAT sign-off, test report, release checklist.

---

## 4. P0/P1/P2 sau khi audit

### P0 — Bắt buộc code thật

- Auth/session/profile.
- Organization + Project + ProjectMember.
- RBAC 3 tầng mức core.
- Task CRUD + assignment + status flow.
- Evidence + approval request + approve/reject.
- Kanban board cơ bản.
- Wiki cơ bản.
- Notification cơ bản.
- Customer filtered view cơ bản.
- Audit log core.
- SQL Server migration/seed/health check.
- Test cases và demo data.

### P1 — Nên code thật để nổi bật

- Sprint/Timeline/Gantt.
- Mini Zalo group chat + task from message.
- AI RAG local with Qdrant.
- Import Markdown/CSV to task/wiki.
- Webhook basic with delivery log.
- Report export weekly.

### P2 — Roadmap/mô phỏng nếu thiếu thời gian

- Full MinerU PDF/scan pipeline.
- Meeting transcript audio.
- S3/MinIO production storage.
- Advanced analytics warehouse.
- Billing/subscription thật.
- Read receipt/typing indicator nâng cao.
- SQL Server 2025 native vector/AI.

---

## 5. Gap Register

Xem file `QALY_Gap_Register_v2.1.csv` để tracking 35 gap chính, owner, severity, evidence và status.

---

## 6. Điều kiện để nói “đã sẵn sàng code 80–90% web”

Nhóm có thể bắt đầu code mạnh khi đạt 5 điều kiện sau:

1. DB target SQL Server được chốt và migration P0 list được duyệt.
2. RBAC matrix được chuyển thành policy service/test cases.
3. API P0 được chốt contract trong OpenAPI.
4. WBS/RACI đúng 7 người và mỗi người có deliverable theo tuần.
5. Demo script cuối kỳ được viết trước, rồi code bám theo script.
