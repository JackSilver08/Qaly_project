# QALY Workspace — AI Endpoint Contract v3.0

**Mục tiêu:** chuẩn hóa API để QALY gọi AI API, Meetily Connector, Ollama local hoặc Mock Provider mà không đổi business code.

---

## 1. Nguyên tắc endpoint

- Tất cả endpoint yêu cầu JWT user + tenant/project permission, trừ webhook nội bộ có HMAC/token.
- Output AI là draft/suggestion.
- Ghi task/deadline/assign chính thức phải qua endpoint confirm riêng.
- Request/response phải có `correlationId` để trace log.
- JSON output phải validate schema trước khi lưu.

---

## 2. AI Gateway endpoints

| API | Method | Path | Priority | Mô tả |
|---|---|---|---|---|
| AI-EP-001 | POST | `/api/v1/ai/meeting-notes/import` | P0 | Import transcript/summary từ Meetily/export file |
| AI-EP-002 | POST | `/api/v1/ai/meeting-notes/{meetingId}/extract` | P0 | Extract keyword/action item/decision/deadline |
| AI-EP-003 | POST | `/api/v1/ai/chat/threads/{threadId}/summarize` | P0 | Tóm tắt đoạn chat dài |
| AI-EP-004 | POST | `/api/v1/ai/chat/messages/{messageId}/task-candidate` | P0 | Tạo task draft từ tin nhắn |
| AI-EP-005 | POST | `/api/v1/ai/tasks/{taskId}/recommend-assignees` | P0 | Gợi ý người làm task |
| AI-EP-006 | POST | `/api/v1/ai/tasks/{taskId}/breakdown` | P0 | Chia nhỏ task/checklist |
| AI-EP-007 | POST | `/api/v1/ai/projects/{projectId}/progress-summary` | P0 | Tóm tắt tiến độ project/sprint |
| AI-EP-008 | POST | `/api/v1/ai/search` | P1 | Semantic search/Q&A có source |
| AI-EP-009 | POST | `/api/v1/ai/tasks/{taskId}/similar` | P1 | Tìm task trùng/tương tự |
| AI-EP-010 | POST | `/api/v1/ai/documents/import-draft` | P1 | Import tài liệu thành draft tasks |

---

## 3. Meetily import schema

```json
{
  "external_source": "meetily",
  "external_meeting_id": "local-uuid-or-file-hash",
  "project_id": "uuid",
  "title": "Sprint Planning 2026-05-19",
  "started_at": "2026-05-19T09:00:00+07:00",
  "ended_at": "2026-05-19T10:00:00+07:00",
  "participants": [
    {"display_name": "Nam", "user_id": "uuid-or-null"}
  ],
  "transcript": [
    {"start": "00:00:12", "end": "00:00:25", "speaker": "unknown", "text": "..."}
  ],
  "summary": "...",
  "raw_export_format": "json|md|txt",
  "checksum": "sha256"
}
```

---

## 4. Meeting extract output schema

```json
{
  "summary": "string",
  "keywords": ["string"],
  "decisions": [
    {"title": "string", "detail": "string", "confidence": 0.0}
  ],
  "action_items": [
    {
      "title": "string",
      "description": "string",
      "assignee_hint": "string|null",
      "due_date_hint": "YYYY-MM-DD|null",
      "priority_hint": "low|medium|high|critical|null",
      "source_quote": "string",
      "confidence": 0.0
    }
  ],
  "risks": ["string"],
  "open_questions": ["string"]
}
```

---

## 5. Task recommendation output schema

```json
{
  "task_id": "uuid",
  "recommendations": [
    {
      "user_id": "uuid",
      "display_name": "string",
      "score": 0.0,
      "reasons": ["skill match", "similar task history"],
      "risks": ["current workload high"],
      "confidence": 0.0
    }
  ],
  "decision_policy": "AI only suggests; PM confirms"
}
```

---

## 6. Provider routing

```text
if request.type in [meeting_extract, task_recommend, progress_report]
  use AI_API primary
  fallback mock/cache/local
else if request.type in [rewrite, summarize_short]
  use local Ollama if available
  fallback AI_API/mock
```

---

## 7. Audit events

| Event | Khi nào ghi |
|---|---|
| AI_MEETING_IMPORTED | Import transcript/summary |
| AI_MEETING_EXTRACTED | Extract keyword/action items |
| AI_TASK_RECOMMENDATION_GENERATED | Gợi ý assignee |
| AI_TASK_DRAFT_CREATED | Tạo task draft từ chat/meeting |
| AI_PROVIDER_FAILED | Provider lỗi/timeout |
| AI_FALLBACK_USED | Dùng fallback provider |
| AI_OUTPUT_CONFIRMED | User xác nhận áp dụng output |
| AI_OUTPUT_REJECTED | User từ chối output |
