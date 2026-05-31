# QALY Docs v3.2 - Gap Closure / Proceed Ready

**Ngày:** 20/05/2026  
**Nguồn:** v3.1 + gói nghiệm thu gap audit  
**Mục tiêu:** vá toàn bộ blocker ở mức đặc tả để nhóm có thể bắt đầu triển khai P0 trong 10 tuần với xác suất hoàn thành cao.

## 1. Trạng thái sau vá

| Nhóm gap | Trạng thái v3.1 | Trạng thái v3.2 |
|---|---|---|
| Acceptance v3.1 thiếu | Missing | Đã tạo `QALY_Acceptance_Checklist_v3.2_Proceed.csv` |
| Gap register chưa khóa | Open/mixed P0-P2 | Đã tạo `QALY_Gap_Register_v3.2_Closed_Proceed.csv` |
| P0 scope chưa đóng | Quá rộng nếu hiểu theo package cũ | Đã tạo `QALY_MVP_P0_Scope_Lock_v3.2.md` |
| Endpoint AI còn mỏng | Thiếu status/result/retry/cancel/schema | Đã tạo `QALY_AI_Endpoint_Contract_v3.2.md` |
| AI JSON schema thiếu | Missing | Đã tạo `QALY_AI_JSON_Schemas_v3.2.md` và `docs/schemas/ai/*.schema.json` |
| DB thiếu FK/retention/consent | Partial | Đã tạo `QALY_AI_Data_Schema_Migration_v3.2.sql` |
| RACI/WBS lệch AI cũ | Partial | Đã tạo `QALY_WBS_RACI_v3.2_10Weeks.md` |
| Demo/evidence thiếu | Missing | Đã tạo `QALY_Demo_Script_And_Evidence_Template_v3.2.md` |
| Diagram ID mismatch | Có mismatch | Đã tạo `QALY_Change_Impact_Matrix_v3.2.csv` + diagram spec v3.2 |
| Cost/model chưa pin | Partial | Đã tạo `QALY_Provider_Model_Pin_Cost_Policy_v3.2.md` |
| Compliance UI/API chưa map | Partial | Đã tạo `QALY_Compliance_UI_API_Flows_v3.2.md` |
| Backup/healthcheck thiếu | Partial | Đã tạo `QALY_Deployment_Runbook_VPS8GB_Laptop_v3.2.md` |

## 2. Quyết định khóa phạm vi

Từ v3.2 trở đi, package cũ 68 use cases/188 bảng/305 test cases chỉ là **master reference**. Scope triển khai 10 tuần là **P0 locked scope** trong `QALY_MVP_P0_Scope_Lock_v3.2.md`.

Không kéo P1/P2 vào sprint nếu P0 chưa đạt các gate trong `QALY_Proceed_Gate_Checklist_v3.2.csv`.

## 3. Kiến trúc AI được chốt

```text
Meetily-first + AI API primary + Ollama local fallback + Qdrant P1 optional
```

- Meetily: meeting capture/transcript/summary nguồn vào.
- AI API: provider chính cho demo, extraction, summarization, recommendation explanation.
- Ollama: fallback local/dev/offline, ưu tiên model 7B/8B Q4; không bắt buộc 14B.
- Qdrant: semantic memory/search P1; không chặn MVP.
- Human-in-the-loop: AI chỉ tạo draft/proposal, user/PM xác nhận mới ghi task/assignee/deadline.

## 4. Cách dùng gói v3.2

Đọc theo thứ tự:

1. `QALY_SRS_Master_v3.2_Proceed_Locked.md`
2. `QALY_MVP_P0_Scope_Lock_v3.2.md`
3. `QALY_WBS_RACI_v3.2_10Weeks.md`
4. `QALY_AI_Endpoint_Contract_v3.2.md`
5. `QALY_AI_JSON_Schemas_v3.2.md`
6. `QALY_AI_Data_Schema_Migration_v3.2.sql`
7. `QALY_Deployment_Runbook_VPS8GB_Laptop_v3.2.md`
8. `QALY_Acceptance_Checklist_v3.2_Proceed.csv`
9. `QALY_Test_Cases_P0_v3.2.csv`
10. `QALY_Demo_Script_And_Evidence_Template_v3.2.md`

## 5. Cảnh báo phạm vi

Gói v3.2 vá gap tài liệu/đặc tả. Việc đạt 90%+ dự án vẫn phụ thuộc thực thi:

- không mở rộng P0,
- không refactor sâu Meetily,
- không chạy LLM trên VPS 8GB,
- mọi AI call phải qua AI Gateway,
- có mock/cache/budget guard trước khi dùng API thật,
- tuần 5 phải có demo end-to-end nhỏ.
