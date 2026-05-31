# QALY AI Endpoint Contract v3.2

## 1. Nguyên tắc API

- Frontend không gọi AI provider trực tiếp.
- Mọi AI request đi qua Backend AI Gateway.
- AI job xử lý async qua `ai_job_queue`.
- AI output phải validate JSON schema trước khi lưu draft.
- `sensitive=true` chặn cloud provider nếu không có admin override.
- Mọi confirm/reject/edit của AI draft phải ghi audit.

## 2. Common headers

```http
Authorization: Bearer <access_token>
X-Project-Id: <project_id>
X-Request-Id: <uuid>
Content-Type: application/json
```

## 3. Common error format

```json
{
  "error": {
    "code": "AI_BUDGET_EXCEEDED",
    "message": "Daily AI budget exceeded for this project.",
    "details": {
      "daily_budget_usd": 2.0,
      "current_usage_usd": 2.13
    },
    "request_id": "uuid"
  }
}
```

### Error codes

| Code | HTTP | Ý nghĩa |
|---|---:|---|
| AI_SCHEMA_INVALID | 422 | Provider output không đạt JSON schema |
| AI_BUDGET_EXCEEDED | 402/429 | Vượt budget ngày/tháng |
| AI_SENSITIVE_BLOCKED | 403 | Dữ liệu sensitive không được gửi cloud |
| AI_PROVIDER_UNAVAILABLE | 503 | Provider lỗi và fallback không khả dụng |
| AI_JOB_NOT_FOUND | 404 | Không tìm thấy job |
| AI_PERMISSION_DENIED | 403 | User không có quyền source/project |
| AI_PAYLOAD_TOO_LARGE | 413 | Input vượt token/size limit |
| AI_RATE_LIMITED | 429 | Vượt rate limit local/API gateway |
| AI_CACHE_MISS | 404 | Request cache-only nhưng không có cache |
| AI_DRAFT_ALREADY_CONFIRMED | 409 | Draft đã confirm trước đó |

## 4. AI job lifecycle

```text
queued -> running -> succeeded
queued -> running -> failed -> retrying -> running -> succeeded|failed
queued|running -> canceled
succeeded -> draft_pending_review -> confirmed|rejected|expired
```

## 5. Create AI job

```http
POST /api/ai/jobs
```

Request:

```json
{
  "job_type": "meeting_action_extract",
  "project_id": 1,
  "source_type": "meetily_import",
  "source_id": 1001,
  "provider_hint": "auto",
  "sensitive": false,
  "cache_mode": "prefer_cache",
  "max_estimated_cost_usd": 0.05,
  "options": {
    "language": "vi",
    "create_draft": true,
    "schema_version": "v3.2"
  }
}
```

Response `202 Accepted`:

```json
{
  "job_id": 9001,
  "status": "queued",
  "job_type": "meeting_action_extract",
  "estimated_cost_usd": 0.002,
  "cache_key": "sha256...",
  "poll_url": "/api/ai/jobs/9001",
  "result_url": "/api/ai/jobs/9001/result"
}
```

## 6. Get job status

```http
GET /api/ai/jobs/{job_id}
```

Response:

```json
{
  "job_id": 9001,
  "job_type": "meeting_action_extract",
  "status": "running",
  "progress": 60,
  "provider_name": "openai",
  "model_name": "gpt-5.4-mini",
  "created_at": "2026-05-20T02:00:00Z",
  "started_at": "2026-05-20T02:00:02Z",
  "finished_at": null,
  "cost": {
    "input_tokens": 1200,
    "output_tokens": 0,
    "estimated_cost_usd": 0.0009
  }
}
```

## 7. Get job result

```http
GET /api/ai/jobs/{job_id}/result
```

Response:

```json
{
  "job_id": 9001,
  "status": "succeeded",
  "schema_id": "meeting_action_extract.v3.2",
  "result_json": {},
  "draft_ids": [501, 502],
  "usage_ledger_id": 30001,
  "cache_hit": false
}
```

## 8. Retry/cancel job

```http
POST /api/ai/jobs/{job_id}/retry
POST /api/ai/jobs/{job_id}/cancel
```

Rules:

- Retry chỉ cho `failed` hoặc `canceled` nếu `retry_count < max_retry`.
- Cancel chỉ cho `queued/running`.
- Retry phải giữ `source_id`, `job_type`, `schema_version` cũ; chỉ được đổi `provider_hint` nếu có quyền PM/Admin.

## 9. Budget endpoints

```http
GET /api/ai/usage?project_id=1&period=2026-05
GET /api/ai/budget?project_id=1
PUT /api/ai/budget?project_id=1
```

PUT request:

```json
{
  "daily_budget_usd": 2.0,
  "monthly_budget_usd": 30.0,
  "hard_stop_enabled": true,
  "warn_at_percent": 80,
  "allow_cloud_for_sensitive": false
}
```

## 10. Provider health

```http
GET /api/ai/health
```

Response:

```json
{
  "gateway": "ok",
  "providers": [
    {"provider": "mock", "status": "ok", "latency_ms": 1},
    {"provider": "openai", "status": "ok", "latency_ms": 420},
    {"provider": "ollama", "status": "degraded", "reason": "local gateway offline"}
  ],
  "budget": {"status": "ok", "daily_remaining_usd": 1.82}
}
```

## 11. Meetily import

```http
POST /api/meetings/import/meetily
```

Request:

```json
{
  "project_id": 1,
  "meeting_title": "Sprint Planning",
  "started_at": "2026-05-19T09:00:00+07:00",
  "ended_at": "2026-05-19T10:00:00+07:00",
  "participants": ["Nam", "Huy", "An"],
  "language": "vi",
  "transcript_text": "...",
  "summary_text": "...",
  "source_hash": "sha256...",
  "sensitive": true,
  "consent_confirmed": true,
  "retention_days": 180,
  "metadata": {
    "source_app": "Meetily",
    "source_version": "locked-version-or-export-date",
    "import_mode": "manual_export"
  }
}
```

Response:

```json
{
  "import_id": 1001,
  "meeting_id": 701,
  "status": "imported",
  "next_suggested_action": "extract_action_items"
}
```

Validation:

- `consent_confirmed=true` required if transcript contains personal data or `sensitive=true`.
- Duplicate `source_hash` in same project returns existing import id.
- Transcript over limit must be chunked server-side or rejected with `AI_PAYLOAD_TOO_LARGE`.

## 12. AI function-specific routes

Các route sau là wrapper tiện cho frontend, backend vẫn tạo job trong `ai_job_queue`.

### AI-02 Meeting extraction

```http
POST /api/ai/meetings/{meeting_id}/extract-actions
```

Output schema: `meeting_action_extract.v3.2`.

### AI-03 Chat summary

```http
POST /api/ai/chat-rooms/{room_id}/summarize
```

Request:

```json
{
  "from_message_id": 100,
  "to_message_id": 180,
  "summary_style": "bullet",
  "cache_mode": "prefer_cache"
}
```

Output schema: `chat_summary.v3.2`.

### AI-04 Task draft from source

```http
POST /api/ai/task-drafts/from-source
```

Request:

```json
{
  "project_id": 1,
  "source_type": "chat_message|meeting_action|manual_text",
  "source_ids": [1001,1002],
  "text": "optional when manual_text"
}
```

Output schema: `task_draft.v3.2`.

### AI-05 Assignee recommendation

```http
POST /api/ai/tasks/{task_id}/recommend-assignees
```

Input must include SQL-derived candidates or backend computes candidates before provider call. Output schema: `assignee_recommendation.v3.2`.

### AI-06/07 Breakdown/checklist

```http
POST /api/ai/tasks/{task_id}/breakdown
POST /api/ai/tasks/{task_id}/checklist
```

Output schemas: `task_breakdown.v3.2`, `acceptance_checklist.v3.2`.

### AI-08 Sprint/project report

```http
POST /api/ai/projects/{project_id}/progress-summary
```

Backend sends aggregated metrics, not raw full chat. Output schema: `progress_summary.v3.2`.

## 13. Draft review endpoints

```http
GET /api/ai/drafts?project_id=1&status=pending_review
GET /api/ai/drafts/{draft_id}
PATCH /api/ai/drafts/{draft_id}
POST /api/ai/drafts/{draft_id}/confirm
POST /api/ai/drafts/{draft_id}/reject
```

Confirm request:

```json
{
  "confirm_action": "create_tasks|assign_task|create_subtasks|save_checklist|save_report",
  "edited_payload": {},
  "review_note": "PM verified assignee and deadline"
}
```

Rules:

- `assign_task` requires PM/Lead permission.
- Deadline/assignee from AI must be visible and editable before confirm.
- Confirm creates `ai_audit_events` and normal domain audit log.

## 14. Privacy endpoints

```http
GET /api/privacy/consents?project_id=1
POST /api/privacy/consents
POST /api/privacy/data-requests/export
POST /api/privacy/data-requests/delete
GET /api/privacy/data-requests/{request_id}
```

Used by compliance UI flows in v3.2.
