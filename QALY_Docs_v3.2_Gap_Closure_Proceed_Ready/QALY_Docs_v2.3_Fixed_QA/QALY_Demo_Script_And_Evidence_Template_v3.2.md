# QALY Demo Script and Evidence Template v3.2

## 1. Demo path chính 12-15 phút

### Scene 0 - Health and environment

Evidence cần chụp:

- `/health` backend OK.
- `/api/ai/health` OK/mock/API provider visible.
- Docker/VPS resource screenshot.
- Laptop Meetily/local gateway screenshot nếu dùng.

### Scene 1 - Login and project

1. Login PM.
2. Mở project demo.
3. Hiển thị member/role.

Evidence:

- screenshot login success.
- screenshot project member list.

### Scene 2 - Chat Mini Zalo

1. Mở 2 browser/user.
2. Gửi tin nhắn realtime.
3. Mention một member.
4. Notification xuất hiện.

Evidence:

- screenshot 2 browser.
- websocket log hoặc browser console minimal.

### Scene 3 - Meetily import

1. Mở file export Meetily/sample transcript.
2. Import vào meeting note.
3. Tick privacy consent.
4. Submit.

Evidence:

- import form screenshot.
- DB/import id hoặc API response.

### Scene 4 - AI meeting extraction

1. Click “Extract action items”.
2. AI job queued/running/succeeded.
3. Hiển thị keywords/action/deadline/decision.
4. User edit một deadline.
5. Confirm tạo task.

Evidence:

- AI job status screenshot.
- draft before/after edit.
- task created.
- audit event.

### Scene 5 - Task recommendation

1. Mở task “Fix socket reconnect”.
2. Click recommend assignee.
3. Hệ thống hiển thị score/reason/risk.
4. PM confirm assignee.

Evidence:

- recommendation JSON/UI.
- audit log assignment.

### Scene 6 - Chat summary and task draft from chat

1. Chọn range tin nhắn.
2. Click summarize.
3. Chọn một action candidate.
4. Create task draft.
5. Confirm.

Evidence:

- summary UI.
- cache hit khi chạy lại.

### Scene 7 - Task breakdown/checklist

1. Mở task lớn.
2. Click breakdown/checklist.
3. Hiển thị subtasks/acceptance criteria.
4. Confirm lưu checklist.

Evidence:

- checklist UI.
- API result valid schema.

### Scene 8 - Sprint/project progress summary

1. Click Generate Progress Summary.
2. Hiển thị metrics + narrative + risks.

Evidence:

- report screenshot.
- usage ledger row.

### Scene 9 - Cost/budget/offline fallback

1. Mở AI usage dashboard.
2. Hiển thị cost/usage.
3. Chuyển provider sang mock hoặc tắt API key.
4. Chạy lại demo cached/mock.

Evidence:

- budget dashboard.
- cache/mock response.
- no external API call log.

### Scene 10 - Privacy/export/delete

1. Mở meeting transcript.
2. Export data request.
3. Delete/anonymize request.
4. Audit event.

Evidence:

- DSR request status.
- audit event.

## 2. Evidence folder structure

```text
evidence/
  week-08-freeze/
    00-health/
    01-login-project/
    02-chat/
    03-meetily-import/
    04-ai-meeting-extract/
    05-task-recommendation/
    06-chat-summary-task-draft/
    07-breakdown-checklist/
    08-progress-summary/
    09-budget-fallback/
    10-privacy-dsr/
    api-logs/
    db-snapshots/
    test-report/
```

## 3. Evidence acceptance rule

Mỗi scene phải có ít nhất:

- 1 screenshot UI hoặc terminal log,
- 1 API response/log,
- 1 DB/audit evidence nếu có thay đổi dữ liệu,
- status PASS/FAIL và người xác nhận.

## 4. Demo rollback

Trước demo:

1. Restore seed DB.
2. Warm cache bằng 8 AI P0 flows.
3. Export `.env.demo` không chứa secret thật.
4. Check budget còn đủ hoặc chuyển mock.
5. Tắt P1 toggles nếu VPS RAM cao.
