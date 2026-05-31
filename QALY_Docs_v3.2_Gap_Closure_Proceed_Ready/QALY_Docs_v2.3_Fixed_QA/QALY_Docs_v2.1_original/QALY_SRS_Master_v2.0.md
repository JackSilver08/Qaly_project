# QALY Workspace — SRS Master v2.0 — Index

**Phiên bản:** v2.0 — Production/Japan-style specification  
**Ngày:** 16/05/2026  
**Phạm vi:** Dự án web quản lý dự án phần mềm cho doanh nghiệp outsource vừa và nhỏ.  
**Định hướng:** Jira + Notion + Mini Zalo + AI Assistant + Document Mining.  
**Ghi chú kiến trúc:** Tài liệu v2.0 mở rộng từ SRS/TKHT v1.0. Các phần dưới đây là target design để nhóm có thể triển khai 80–90% chức năng web trong 10–11 tuần, không bắt buộc implement toàn bộ bảng P2 nếu thiếu thời gian.

---


## 1. Bộ tài liệu v2.0

Bộ này mở rộng v1.0 thành các phụ lục đủ để nhóm 7 người triển khai 80–90% web, bao gồm **188 bảng logical schema**, **167 endpoints**, **68 use cases**, **68 business rules**, **240 test cases**, traceability matrix và WBS/RACI.

## 2. Thứ tự đọc/triển khai

1. `QALY_Project_Structure_Mapping.md` — map module vào codebase.
2. `QALY_Database_Dictionary.md` — schema logical + priority P0/P1/P2.
3. `QALY_API_Specification.md` — endpoint contract.
4. `QALY_Business_Rules_Workflow.md` — workflow và rule cần test.
5. `QALY_Use_Case_Specification.md` — kịch bản nghiệp vụ.
6. `QALY_WBS_RACI_Plan.md` — chia việc 7 người trong 11 tuần.
7. `QALY_AI_Module_Specification.md` — RAG/Document Miner.
8. `QALY_Test_Plan_QA.md` + CSV — QA/UAT/traceability.

## 3. Chiến lược triển khai để không quá tải

- **P0 phải code thật:** Auth/RBAC, Organization, Project, Task, Kanban, Approval/Evidence, Wiki cơ bản, Chat cơ bản, Notification, Customer filtered view, Audit.
- **P1 nên code hoặc demo thật:** Sprint/Timeline, Webhook, AI local RAG, Import CSV/Markdown, Meeting summary.
- **P2 có thể mock/roadmap:** Billing, advanced analytics, GitHub/GitLab deep sync, GDPR full flow, advanced AI evaluation.

## 4. Lưu ý conflict trong prompt bạn đưa

Prompt-02 có nhắc NestJS/JWT/BullMQ, nhưng codebase và tài liệu v1.0 hiện đang theo ASP.NET Core 10, Cookie Session, SignalR và BackgroundService. Vì mục tiêu là hoàn thiện 80–90% web dựa trên code hiện tại, bộ v2.0 này **giữ ASP.NET Core/Clean Architecture** và chỉ mượn ý tưởng queue/job, không ép migrate sang NestJS để tránh mất thời gian.

## 5. Artifact CSV/SQL đi kèm

- `QALY_Table_Catalog.csv` — catalog bảng để lọc P0/P1/P2.
- `QALY_Endpoint_Catalog.csv` — endpoint catalog.
- `QALY_Use_Case_Catalog.csv` — catalog UC.
- `QALY_Business_Rules_Catalog.csv` — catalog rule.
- `QALY_Test_Cases.csv` — 240 test cases.
- `QALY_Traceability_Matrix.csv` — mapping requirement → UC → API → DB → UI → TC.
- `QALY_v2_PostgreSQL_Schema_Skeleton.sql` — skeleton schema logical.


---

## v2.1 Update — SQL Server & Gap Acceptance

Bản v2.1 chốt **SQL Server 2022** là database implementation target cho đồ án vì khớp codebase hiện tại, Docker dev environment và năng lực nhóm. PostgreSQL trong v2.0 chỉ còn là logical/reference schema; khi code dùng `QALY_v2_1_SQLServer_Schema_Skeleton.sql` và `QALY_Database_Decision_SQLServer_v2.1.md`.

File bổ sung v2.1:

1. `QALY_Database_Decision_SQLServer_v2.1.md`
2. `QALY_Gap_Audit_NghiemThu_v2.1.md`
3. `QALY_Gap_Register_v2.1.csv`
4. `QALY_Acceptance_Checklist_v2.1.csv`
5. `QALY_WBS_RACI_Update_7People_v2.1.md`
6. `QALY_v2_1_SQLServer_Schema_Skeleton.sql`

Quy tắc triển khai sau update: **P0 code thật trước, P1 demo thật nếu còn thời gian, P2 chỉ roadmap/feature flag/mock**.
