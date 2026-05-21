# QALY Compliance UI/API Flows v3.2

> Tài liệu này là checklist kỹ thuật cho đồ án, không thay thế tư vấn pháp lý chính thức.

## 1. Văn bản cần xem xét

| Nhóm | Văn bản | Ảnh hưởng đến hệ thống |
|---|---|---|
| Dữ liệu cá nhân | Nghị định 13/2023/NĐ-CP | purpose, consent, data subject rights, security measures |
| Dữ liệu cá nhân | Luật Bảo vệ dữ liệu cá nhân 91/2025/QH15 | luật có hiệu lực từ 01/01/2026; cần lưu ý khi mô tả production |
| An ninh mạng | Luật An ninh mạng 24/2018/QH14 | bảo vệ hệ thống, xử lý nội dung vi phạm, lưu/log theo quy định nếu production public |
| Internet/thông tin mạng | Nghị định 147/2024/NĐ-CP | chỉ áp dụng đầy đủ nếu mở rộng thành nền tảng/dịch vụ công khai; đồ án ghi là internal collaboration MVP |

## 2. Privacy notice khi import Meetily

UI bắt buộc hiển thị trước khi import:

```text
Bạn đang import transcript/summary cuộc họp. Nội dung có thể chứa dữ liệu cá nhân như tên, giọng nói đã chuyển thành text, ý kiến, nhiệm vụ, deadline. Dữ liệu sẽ được dùng để tạo meeting note, action item, task draft và báo cáo tiến độ trong project này. Bạn cần xác nhận đã có quyền/đồng thuận phù hợp từ người tham gia trước khi import.
```

Checkbox:

- [ ] Tôi xác nhận có quyền import và xử lý transcript cho mục đích quản lý project.
- [ ] Đánh dấu nội dung này là sensitive, không gửi lên cloud AI API.

## 3. API flow consent

```http
POST /api/privacy/consents
```

Request:

```json
{
  "project_id": 1,
  "consent_type": "meeting_import",
  "purpose": "meeting_note_and_task_extraction",
  "scope": {"meeting_title": "Sprint Planning"},
  "status": "granted"
}
```

Response:

```json
{"consent_id": 501, "status": "granted"}
```

`consent_id` phải được gắn vào `meetily_import_sessions` và `ai_job_queue` nếu job dùng transcript.

## 4. Sensitive data rule

| Case | Provider allowed |
|---|---|
| `sensitive=false` | AI API primary, cache, mock, local fallback |
| `sensitive=true` + no override | mock/local only; cloud blocked |
| `sensitive=true` + admin override | cloud allowed only if audit reason provided |

Audit event:

```json
{
  "event_type": "AI_SENSITIVE_CLOUD_OVERRIDE",
  "before_json": {"allow_cloud_for_sensitive": false},
  "after_json": {"allow_cloud_for_sensitive": true, "reason": "demo non-real data"}
}
```

## 5. Export data flow

```http
POST /api/privacy/data-requests/export
```

- User chọn scope: meeting transcript, chat messages, AI drafts, audit summary.
- System tạo request `pending`.
- PM/Admin approve nếu scope thuộc project.
- Worker tạo JSON/CSV export.
- Audit event `DATA_EXPORT_COMPLETED`.

## 6. Delete/anonymize flow

```http
POST /api/privacy/data-requests/delete
```

P0 behavior:

- Nếu meeting transcript chưa tạo task chính thức: có thể delete transcript.
- Nếu action item đã tạo task: không xóa task audit; thay vào đó anonymize source quote hoặc detach transcript reference.
- Prompt cache chứa transcript phải xóa theo `cache_key`/`source_hash`.
- Vector sync P1 nếu có phải mark `delete_pending`.

## 7. Retention policy P0

| Data | Default retention | P0 behavior |
|---|---:|---|
| Meetily transcript raw | 180 ngày | `delete_after` field |
| AI prompt cache | 30 ngày | shorter for sensitive data |
| AI usage ledger | 12 tháng | không chứa full transcript |
| AI audit events | 12 tháng | metadata only |
| Generated drafts rejected | 30 ngày | auto expire |

## 8. UI pages required P0

| Page/Dialog | Purpose |
|---|---|
| Meetily Import Dialog | notice + consent + sensitive toggle |
| AI Draft Review | edit before confirm/reject |
| AI Usage Dashboard | cost/usage/budget |
| Privacy Request Page | export/delete/anonymize request |
| Audit View | AI confirm/override/delete/export evidence |

## 9. Không làm trong P0

- Legal-grade consent management cho public SaaS.
- Full automated data localization compliance.
- Advanced content moderation theo mạng xã hội public.
- Processing special sensitive categories beyond project meeting/task text.
