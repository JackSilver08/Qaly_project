# QALY Workspace — SRS Master v3.0 AI Production Update

**Phiên bản:** v3.0  
**Ngày:** 19/05/2026  
**Định hướng sản phẩm:** Jira + Notion + Mini Zalo + Meetily-powered AI Meeting Note + AI Assistant  
**Bối cảnh tài nguyên:** laptop RTX4060 + 16GB RAM; có thể có VPS 8GB; còn 10 tuần triển khai.  

---

## 1. Phạm vi cập nhật v3.0

Bản v3.0 cập nhật toàn bộ chiến lược AI để phù hợp rủi ro thực tế:

- Không tự code pipeline audio/transcription từ đầu.
- Áp dụng Meetily làm open-source meeting worker/connector.
- Sử dụng AI API làm provider chính cho tác vụ cần chất lượng khi demo.
- Dùng Ollama local như fallback/dev/offline, target 7B/8B thay vì bắt buộc 14B.
- Dùng Qdrant như semantic memory optional/P1, không làm source of truth.
- Tối ưu triển khai với VPS 8GB và laptop local.

---

## 2. Stakeholder và mục tiêu

| Stakeholder | Nhu cầu |
|---|---|
| Giảng viên | Hướng AI đáng tin, có open-source rõ, không mơ hồ về local model |
| PM/Leader | Quản lý project/task/chat/meeting, có báo cáo và gợi ý thông minh |
| Member | Nhận task rõ, theo dõi chat/meeting, không bỏ lỡ action item |
| Customer | Xem tiến độ customer-safe, không lộ dữ liệu nội bộ |
| Nhóm phát triển | Có scope thực tế trong 10 tuần, dễ setup, dễ demo |

---

## 3. Functional Requirements — AI

### FR-AI-001 Meetily Meeting Import

Hệ thống cho phép PM/member import transcript/summary từ Meetily vào một meeting thuộc project.

**Acceptance:**

- Chọn project/meeting hoặc tạo meeting mới.
- Upload/sync transcript định dạng JSON/Markdown/Text.
- Validate file size, checksum, project permission.
- Lưu transcript/summary vào SQL Server.
- Hiển thị trạng thái `Imported`, `NeedsReview`, `Extracted`.

### FR-AI-002 Meeting Action Extraction

Hệ thống trích xuất keyword, decision, action item, deadline, assignee hint từ meeting transcript/summary.

**Acceptance:**

- Output đúng schema JSON.
- Mỗi action item có `source_quote` và `confidence`.
- User có thể sửa/xóa trước khi tạo task.
- Không tự tạo task nếu user chưa xác nhận.

### FR-AI-003 Chat Summary

Hệ thống tóm tắt đoạn chat dài trong room/project.

**Acceptance:**

- User chọn phạm vi: 50 tin gần nhất, theo ngày, hoặc thread.
- AI tóm tắt nội dung chính, blocker, decisions, next actions.
- Chỉ dùng tin nhắn user có quyền xem.

### FR-AI-004 Task Candidate From Chat

Hệ thống phát hiện câu giao việc trong chat và tạo task draft.

**Acceptance:**

- Có nút “Tạo task đề xuất”.
- Draft gồm title, description, assignee_hint, deadline_hint, source_message_id.
- User confirm mới tạo task chính thức.

### FR-AI-005 Assignee Recommendation

Hệ thống gợi ý thành viên phù hợp cho task dựa trên skill, role, workload, lịch sử task và optional semantic evidence.

**Acceptance:**

- Trả về 1–3 candidate.
- Có score breakdown deterministic.
- Có AI explanation ngắn.
- PM confirm mới assign.

### FR-AI-006 Task Breakdown & Checklist

Hệ thống chia task lớn thành subtasks/checklist/acceptance criteria.

**Acceptance:**

- Output editable.
- Có acceptance criteria rõ ràng.
- Không tạo hàng loạt subtasks nếu user chưa xác nhận.

### FR-AI-007 Project/Sprint Progress Summary

Hệ thống sinh báo cáo tiến độ theo phong cách Nhật: số liệu, vấn đề, nguyên nhân, đối sách, kế hoạch.

**Acceptance:**

- Số liệu done/in progress/overdue lấy deterministic từ SQL.
- AI chỉ diễn giải và tổng hợp risk/action.
- Có nguồn task/meeting/chat liên quan.

### FR-AI-008 AI Provider Gateway

Hệ thống route request qua AI API, Ollama local, Meetily Connector hoặc Mock Provider.

**Acceptance:**

- Có feature flags cho provider.
- Có timeout/retry/cache/quota.
- Có audit provider_used.
- Tắt Ollama không làm hệ thống chết.
- Tắt Qdrant không làm MVP chết.

### FR-AI-009 Semantic Search / Qdrant Optional

Hệ thống cho phép tìm kiếm semantic trong meeting/task/chat/wiki khi bật Qdrant.

**Acceptance:**

- Search theo project/tenant permission.
- Kết quả có source entity.
- Nếu Qdrant down, fallback SQL search.

---

## 4. Non-functional Requirements — AI/Production

| NFR | Mục tiêu |
|---|---|
| Security | 0 case cross-tenant leak; AI không thấy dữ liệu ngoài quyền |
| Cost | Test mặc định không gọi API thật; có mock/cache/quota |
| Reliability | AI lỗi không làm mất chức năng core task/chat/project |
| Performance | AI request async với meeting/report; UI không block vô hạn |
| Maintainability | AiGateway interface tách provider; dễ đổi API/Ollama/Mock |
| Auditability | Log input hash, provider_used, output status, user confirmation |
| Explainability | Gợi ý assign phải có score breakdown và lý do |
| Data Governance | SQL Server là source of truth; Qdrant rebuildable |
| Demo Stability | Có cached/mock output cho các flow bảo vệ chính |

---

## 5. Production/Japan-style business rules

| Rule ID | Rule |
|---|---|
| BR-AI-001 | AI output luôn là draft/suggestion, không tự commit dữ liệu quan trọng. |
| BR-AI-002 | Task assignment/deadline/action item phải được PM/member xác nhận. |
| BR-AI-003 | Tất cả request AI phải có tenant_id, project_id, user_id, correlation_id. |
| BR-AI-004 | Prompt chỉ chứa dữ liệu đã qua permission filter. |
| BR-AI-005 | Output JSON phải validate schema trước khi lưu. |
| BR-AI-006 | Provider lỗi phải fallback hoặc trả thông báo an toàn, không crash. |
| BR-AI-007 | Token budget được giới hạn theo user/project/ngày. |
| BR-AI-008 | Meetily import phải lưu checksum/source để truy vết. |
| BR-AI-009 | Qdrant không lưu dữ liệu bí mật nếu chưa gắn ACL metadata. |
| BR-AI-010 | Customer không được xem internal/private meeting note/action item. |

---

## 6. MVP scope trong 10 tuần

### Must Have

- Auth/RBAC/tenant isolation.
- Project/member/task/Kanban.
- Mini Zalo chat realtime MVP.
- Task from chat draft.
- Meetily import meeting transcript/summary.
- Meeting keyword/action item extraction.
- Task recommendation hybrid.
- Progress summary.
- AI Provider Gateway + API/mock/local fallback.

### Should Have

- Qdrant semantic search.
- Duplicate task detection.
- Document import Markdown/CSV.
- Weekly report export.

### Won't Have trong 10 tuần

- Tự viết realtime audio capture/transcription.
- Full Meetily fork/refactor thành backend service.
- 14B local là bắt buộc.
- Speaker diarization chuẩn production.
- AI auto-assign không cần confirm.

---

## 7. Deployment environments

| Environment | Mục đích | Provider AI |
|---|---|---|
| Local Dev | Dev nhanh, không tốn token | Mock + Ollama 7B/8B optional |
| AI Dev Laptop | Meetily/Ollama test | Meetily + Ollama + ngrok |
| VPS Demo | App public cho giảng viên | AI API primary + mock fallback |
| Defense Mode | Demo ổn định | AI API + cached outputs + Meetily sample export |
| Future Production | Mở rộng thật | GPU server/API managed + Qdrant tuned |

---

## 8. Traceability AI requirement → artifact

| Requirement | Artifact |
|---|---|
| FR-AI-001 | Endpoint Contract, Meetily sequence diagram, Acceptance G5 |
| FR-AI-002 | Function Catalog, Test Plan, Endpoint Contract |
| FR-AI-003 | Function Catalog, Endpoint Contract |
| FR-AI-004 | Chat-to-task flow, Function Catalog |
| FR-AI-005 | Hybrid Recommendation sequence, Risk Register |
| FR-AI-006 | Function Catalog, Acceptance Checklist |
| FR-AI-007 | 10-week plan, AI Module Spec |
| FR-AI-008 | Runbook, Provider routing sequence |
| FR-AI-009 | Qdrant DFD, Deployment Runbook |
