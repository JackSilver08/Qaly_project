# QALY SRS Master v3.2 - Proceed Locked

## 1. Mục đích

Tài liệu này là bản SRS khóa phạm vi triển khai 10 tuần cho QALY/Mini Zalo. Các tài liệu v2.x/v3.0/v3.1 vẫn giữ vai trò reference, nhưng khi có mâu thuẫn về phạm vi, **v3.2 Proceed Locked được ưu tiên**.

## 2. Product positioning

QALY là hệ thống cộng tác nhóm kiểu Mini Zalo kết hợp task/project management và AI assistant. Hệ thống tập trung vào:

- chat nhóm realtime,
- quản lý project/task/sprint cơ bản,
- meeting note/import từ Meetily,
- AI hỗ trợ tạo draft, tóm tắt, trích action item, gợi ý assignee,
- audit/cost/privacy control để phù hợp quy trình Nhật và production-oriented.

## 3. Architecture decision record

| ADR | Quyết định | Lý do |
|---|---|---|
| ADR-AI-01 | Meetily-first cho meeting note | Giảm rủi ro tự code audio/STT, tận dụng open-source hiện hữu |
| ADR-AI-02 | AI API primary | Đảm bảo chất lượng demo, không phụ thuộc toàn bộ vào local model |
| ADR-AI-03 | Ollama fallback | Tiết kiệm khi dev/offline, không dùng làm core bắt buộc |
| ADR-AI-04 | Qdrant P1 optional | Có giá trị semantic memory nhưng không chặn MVP |
| ADR-AI-05 | Human-in-the-loop | AI không tự tạo/giao task chính thức, giảm rủi ro sai lệch |
| ADR-OPS-01 | VPS 8GB chỉ chạy app chính | Không chạy LLM/Meetily trên VPS 8GB vì thiếu RAM/GPU |
| ADR-SEC-01 | Privacy-by-design | Có consent, sensitive flag, audit, export/delete flow |

## 4. P0 functional scope

### 4.1 Core collaboration

- Login/logout, refresh token cơ bản.
- Organization/project/member cơ bản.
- RBAC theo role và project.
- Task CRUD, Kanban status, deadline, priority, assignee.
- Comment/evidence trên task.
- Chat room theo project/group.
- Realtime message MVP qua WebSocket.
- Notification cơ bản: mention, assign, status/deadline.
- Audit log cho hành động quan trọng.

### 4.2 AI P0

- AI Gateway provider router.
- Mock provider, cache, budget guard, usage ledger.
- Meetily manual import transcript/summary.
- Meeting keyword/action/deadline/decision extraction.
- Chat thread summarization.
- Create task draft from chat/meeting.
- Recommend assignee bằng rule-score + AI explanation.
- Task breakdown + acceptance checklist.
- Sprint/project progress summary.

### 4.3 Security/compliance P0

- Privacy notice cho meeting import.
- Consent checkbox khi import transcript có dữ liệu cá nhân.
- Sensitive toggle để chặn cloud API nếu chưa có override.
- Audit AI jobs/drafts/confirm events.
- Delete/export transcript flow ở mức MVP.
- API key không hardcode, chỉ đặt trong environment/secrets.

## 5. P1/P2 explicitly out of P0

- Full realtime STT tự viết trong QALY.
- Fork sâu Meetily hoặc sửa core audio.
- Qdrant semantic search full project.
- Duplicate task detection bằng vector.
- Project Q&A có citations.
- Document mining PDF/MinerU full.
- Customer portal nâng cao.
- Webhook production marketplace.
- Multi-region/multi-VPS HA.

## 6. Non-functional requirements P0

| NFR | Target P0 |
|---|---|
| Availability demo | chạy ổn trong buổi demo với cache/mock fallback |
| Response time non-AI | API chính < 500ms với seed demo |
| AI job response | tạo job < 1s; kết quả async trong worker |
| Budget | daily hard limit configurable; không gọi provider nếu vượt limit |
| Auditability | mọi AI draft confirm có audit event |
| Data isolation | mọi query P0 lọc theo tenant/project/user permission |
| Recoverability | có backup/restore SQL seed demo |
| Resource fit | VPS 8GB chạy app chính; laptop chạy Meetily/Ollama fallback |

## 7. Proceed rule

Một chức năng chỉ được tính done khi có:

1. API endpoint hoặc UI path,
2. schema/validation,
3. test case trong P0 test catalog,
4. audit/log nếu liên quan AI/security,
5. screenshot hoặc evidence trong demo folder.
