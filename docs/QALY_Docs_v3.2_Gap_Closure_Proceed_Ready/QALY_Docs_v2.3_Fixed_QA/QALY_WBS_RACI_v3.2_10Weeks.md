# QALY WBS/RACI v3.2 - 10 Weeks Meetily/API-first

## 1. Team roles giả định

| Role | Mô tả |
|---|---|
| PM/BA | khóa scope, làm việc với giảng viên, nghiệm thu |
| Tech Lead | quyết định kiến trúc, review code, tích hợp end-to-end |
| Backend Dev | API, DB, RBAC, task/chat domain, AI Gateway |
| Frontend Dev | UI project/task/chat/AI review/privacy |
| AI Dev | Meetily import, prompt/schema, provider router, golden dataset |
| DevOps | VPS, Docker, backup, healthcheck, ngrok/local worker |
| QA/Doc | test cases, evidence, demo script, tài liệu/progress |

## 2. RACI theo workstream

| Workstream | R | A | C | I |
|---|---|---|---|---|
| Scope lock/SRS | PM/BA | Tech Lead | QA/Doc | Team |
| Auth/RBAC | Backend | Tech Lead | Frontend, QA | PM |
| Project/Task/Kanban | Backend + Frontend | Tech Lead | QA | PM |
| Chat realtime MVP | Backend + Frontend | Tech Lead | DevOps, QA | PM |
| AI Gateway/router/cache/quota | Backend + AI Dev | Tech Lead | DevOps, QA | PM |
| Meetily import | AI Dev + Frontend | Tech Lead | Backend, QA | PM |
| AI schemas/prompts/golden | AI Dev | Tech Lead | QA | PM |
| Draft review/confirm UI | Frontend | Tech Lead | Backend, AI Dev | PM |
| Compliance UI/API | Backend + Frontend | PM/BA | QA, Tech Lead | Team |
| VPS/local setup | DevOps | Tech Lead | Backend, AI Dev | PM |
| Test/evidence/demo | QA/Doc | PM/BA | Tech Lead | Team |

## 3. Week-by-week WBS

### Week 1 - Gate closure and skeleton

- Freeze P0 scope.
- Create repo branches/environment files.
- Setup SQL Server dev, Redis, backend skeleton, frontend skeleton.
- Implement health endpoints.
- Lock provider/model/env policy.
- Deliverables: running skeleton, `.env.example`, scope sign-off.

### Week 2 - Core auth/RBAC + AI Gateway foundation

- Auth/login/logout.
- Project/member permission guard.
- AI Gateway routes: create job, status, result, mock provider.
- Create AI tables migration v3.2.
- Budget/cache/usage ledger basic.
- Deliverables: Postman collection, migration log, AI mock job successful.

### Week 3 - Task/Kanban + Meetily import MVP

- Task CRUD/Kanban.
- Meetily import endpoint/UI manual form/upload.
- Privacy notice + consent checkbox.
- Job creation from meeting import.
- Deliverables: import transcript -> meeting note stored.

### Week 4 - Meeting AI extraction

- Prompt/schema for meeting extract.
- Draft action items UI.
- Confirm draft -> create tasks.
- Audit events.
- Golden dataset first 20 cases.
- Deliverables: end-to-end meeting -> tasks flow.

### Week 5 - Chat realtime + task from chat

- Chat room/message realtime MVP.
- Chat summary by message range.
- Task draft from selected messages.
- Cache by message_range_hash.
- Deliverables: 2-browser chat demo + AI summary/task draft.

### Week 6 - Assignee recommendation + task breakdown/checklist

- Skill/workload/history inputs from SQL.
- Rule-score candidate ranking.
- AI explanation.
- Breakdown/checklist generation.
- Deliverables: PM chooses task -> recommend assignee -> confirm.

### Week 7 - Sprint/project summary + hardening

- Aggregated metrics.
- Progress summary narrative.
- Notifications for assign/mention/deadline.
- Rate limit and budget guard tests.
- Deliverables: project summary + notification demo.

### Week 8 - Staging freeze

- VPS 8GB deploy.
- Demo seed data.
- Backup/restore drill.
- Offline/mock demo fallback.
- Golden dataset >=50 cases.
- Deliverables: staging link + freeze tag.

### Week 9 - QA/UAT/fix

- Execute P0 test suite.
- Fix critical/high bugs.
- Capture evidence screenshots/logs.
- Advisor review rehearsal.
- Deliverables: UAT report, evidence folder.

### Week 10 - Final defense package

- Final demo script.
- Warm cache.
- Update docs/screenshots.
- Final backup and rollback plan.
- Deliverables: defense-ready system + docs package.

## 4. Weekly gate rule

Không chuyển tuần nếu critical gate tuần trước chưa có evidence. Nếu trễ trên 2 ngày, cắt P1/P2 ngay, không cắt P0 security/cost/audit.
