# QALY AI Endpoint Contract v3.1

## 1. AI job endpoint

```http
POST /api/ai/jobs
Authorization: Bearer <access_token>
Content-Type: application/json
```

Request:

```json
{
  "job_type": "meeting_action_extract",
  "project_id": 1,
  "source_type": "meetily_import",
  "source_id": 1001,
  "provider_hint": "auto",
  "sensitive": false
}
```

Response:

```json
{
  "job_id": 9001,
  "status": "queued",
  "estimated_cost_usd": 0.002,
  "cache_key": "..."
}
```

## 2. AI draft confirm

```http
POST /api/ai/drafts/{draft_id}/confirm
```

Request:

```json
{
  "edited_payload": {},
  "confirm_action": "create_tasks"
}
```

Rule: draft confirmation must create audit event.

## 3. Meetily import

```http
POST /api/meetings/import/meetily
```

Request:

```json
{
  "project_id": 1,
  "meeting_title": "Sprint Planning",
  "started_at": "2026-05-19T09:00:00+07:00",
  "participants": ["Nam", "Huy", "An"],
  "transcript_text": "...",
  "summary_text": "...",
  "source_hash": "sha256..."
}
```

Response:

```json
{
  "import_id": 1001,
  "status": "imported",
  "next_suggested_action": "extract_action_items"
}
```

## 4. AI usage summary

```http
GET /api/ai/usage?project_id=1&period=month
```

Response:

```json
{
  "period": "2026-05",
  "total_cost_usd": 1.25,
  "daily_budget_usd": 2,
  "monthly_budget_usd": 20,
  "provider_breakdown": [
    {"provider": "openai", "cost_usd": 1.10},
    {"provider": "mock", "cost_usd": 0}
  ]
}
```

## 5. Validation rules

- `sensitive=true` must not call cloud API unless admin override exists.
- `job_type` must map to approved schema.
- `max_input_tokens` enforced before provider call.
- `source_id` permission checked before prompt construction.
- AI response must validate JSON schema.
