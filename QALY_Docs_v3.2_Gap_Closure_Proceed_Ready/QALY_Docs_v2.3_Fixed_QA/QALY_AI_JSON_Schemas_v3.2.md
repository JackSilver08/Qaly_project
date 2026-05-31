# QALY AI JSON Schemas v3.2

## 1. Mục tiêu

Chuẩn hóa output của AI để backend có thể validate trước khi lưu `ai_generated_drafts`. Nếu AI trả sai schema, job phải chuyển `failed` với error `AI_SCHEMA_INVALID` và không tạo draft chính thức.

## 2. Schema files

| AI function | Schema ID | File |
|---|---|---|
| AI-02 Meeting extract | `meeting_action_extract.v3.2` | `docs/schemas/ai/meeting_action_extract.schema.json` |
| AI-03 Chat summary | `chat_summary.v3.2` | `docs/schemas/ai/chat_summary.schema.json` |
| AI-04 Task draft | `task_draft.v3.2` | `docs/schemas/ai/task_draft.schema.json` |
| AI-05 Assignee recommendation | `assignee_recommendation.v3.2` | `docs/schemas/ai/assignee_recommendation.schema.json` |
| AI-06 Task breakdown | `task_breakdown.v3.2` | `docs/schemas/ai/task_breakdown.schema.json` |
| AI-07 Acceptance checklist | `acceptance_checklist.v3.2` | `docs/schemas/ai/acceptance_checklist.schema.json` |
| AI-08 Progress summary | `progress_summary.v3.2` | `docs/schemas/ai/progress_summary.schema.json` |
| Privacy DSR | `privacy_data_request.v3.2` | `docs/schemas/ai/privacy_data_request.schema.json` |

## 3. Validation rules

- Backend validates JSON using schema before insert draft.
- `additionalProperties=false` is preferred for P0 schemas to avoid silent prompt drift.
- Confidence under `0.6` must mark draft as `needs_manual_review`.
- Assignee/deadline from AI must never auto-apply without confirm.
- Every schema version must be stored in `ai_generated_drafts.schema_id`.

## 4. Prompt instruction template

```text
Return ONLY valid JSON matching schema_id=<schema>. Do not include Markdown. If information is missing, use null and lower confidence. Never invent assignee or deadline without source evidence.
```
