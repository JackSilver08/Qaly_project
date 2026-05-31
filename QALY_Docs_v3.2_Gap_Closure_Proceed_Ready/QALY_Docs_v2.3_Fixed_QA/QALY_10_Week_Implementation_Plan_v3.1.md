# QALY 10-Week Implementation Plan v3.1

## Nguyên tắc

- Không mở rộng scope nếu P0 chưa xong.
- AI API dùng chính thức cho demo, nhưng test bằng mock/cache/golden dataset.
- Meetily integration đi theo import/connector trước, không refactor core.
- Qdrant là P1, bật sau khi P0 ổn.

## Week-by-week

| Week | Mục tiêu | Deliverable | Risk gate |
|---:|---|---|---|
| 1 | Freeze architecture v3.1 | docs approved, AI provider env, cost budget | giảng viên duyệt hướng Meetily/API |
| 2 | AI Gateway foundation | provider router, mock provider, usage ledger, prompt cache | không gọi API trực tiếp từ frontend |
| 3 | Meetily import MVP | manual import transcript/summary, schema validation | không sửa core Meetily |
| 4 | Meeting AI extraction | keywords/action items/deadlines/decisions draft | JSON schema pass >= 90% golden cases |
| 5 | Task AI P0 | task draft, breakdown, checklist | human confirm mandatory |
| 6 | Assignee recommendation | rule-score + workload + AI explanation | AI không tự gán task |
| 7 | Chat summary + sprint report | chat summarization, project progress report | cache by message range |
| 8 | Hardening | rate limit, quota, audit, privacy notice, seed demo data | daily budget guard works |
| 9 | Optional Qdrant P1 | semantic search/duplicate task if stable | disable toggle if RAM high |
| 10 | Final QA/demo | warm cache, fallback mock, demo script, backup | offline demo works |

## Definition of Done P0

- P0 AI functions work end-to-end.
- API cost ledger shows estimated spending.
- User can review/edit/confirm all AI generated task drafts.
- Meetily transcript can be imported and converted to action items.
- System runs on VPS 8GB without Ollama.
- Laptop can run Meetily local worker separately.
- Demo can run even if AI API fails using cache/mock.
