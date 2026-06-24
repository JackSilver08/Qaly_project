# Kế hoạch Test Playwright (E2E) — Toàn bộ tính năng Qaly

> Phiên bản: 2026-06-24 · Phạm vi: toàn bộ tính năng Web (`src/Qaly.Web`) + Frontend Vue (`ClientApp`)
> Mục tiêu: thiết lập bộ E2E Playwright bao phủ toàn bộ feature, có thể chạy được trong CI, dữ liệu tự dọn, hỗ trợ realtime đa context.

---

## 1. Hiện trạng & nguyên tắc

**Đã có sẵn:**
- `playwright.config.ts` (chromium, baseURL `http://127.0.0.1:5000`, trace/screenshot/video on failure).
- `tests/e2e/qaly.smoke.spec.ts` — smoke cho login, group, chat realtime, poll, analytics, meeting 2-context, import wiki.
- `tests/e2e/browser-console.spec.ts`, `tests/e2e/fixtures/`.
- Backend là ASP.NET Core 10, frontend Razor Pages + Vue Islands, SignalR realtime.

**Nguyên tắc chủ đạo:**
1. **UI-first cho luồng người dùng chính**, **API-first cho setup/teardown dữ liệu** (giống pattern smoke hiện tại: tạo project/group qua `page.request.post`, kiểm chứng qua UI).
2. **Mỗi test tự dọn dữ liệu** trong `finally` (đã có `cleanupGroup`/`cleanupProject`).
3. **Đa context** cho mọi tính năng realtime (chat, poll, meeting, notification, presence).
4. **Phủ phân quyền**: Admin / Member / Outsider (người ngoài project/group) — backend đã có boundary tests, cần soi gương ở E2E.
5. **Song ngữ**: UI dùng vi/en → matcher dùng regex `/Đăng nhập|Login/i` như smoke hiện tại.
6. Ưu tiên **`data-*` / `role` selector** thay vì class dễ vỡ; bổ sung `data-testid` vào component khi cần.

---

## 2. Cấu trúc thư mục đề xuất

```
tests/e2e/
├── fixtures/
│   ├── auth.fixture.ts          # login as admin/member/outsider, storageState reuse
│   ├── api.helpers.ts           # apiResult/apiCommand + create/cleanup project, group, task...
│   ├── data.factory.ts          # uniqueName, seed task/sprint/wiki/label...
│   └── files/                   # tasks.csv, tasks.xlsx, doc.docx, bundle.zip, sample.pdf, image.png
├── pages/                       # Page Objects (tùy chọn, cho trang phức tạp)
│   ├── ProjectDetailPage.po.ts
│   ├── TeamsPage.po.ts
│   └── MeetingPage.po.ts
└── specs/
    ├── 01-auth/                 # login, register, logout, profile, password, sessions
    ├── 02-dashboard/
    ├── 03-projects/
    ├── 04-tasks/
    ├── 05-sprints/
    ├── 06-comments-attachments/
    ├── 07-wiki/
    ├── 08-import/
    ├── 09-teams-chat/
    ├── 10-polls/
    ├── 11-meetings/
    ├── 12-ai/
    ├── 13-analytics/
    ├── 14-notifications-search/
    ├── 15-settings/             # api-keys, webhooks
    ├── 16-admin-org/            # admin users, organizations, audit logs
    └── 17-cross-cutting/        # auth boundary, i18n, responsive, console-errors
```

**Tối ưu tốc độ:** dùng `storageState` để tái sử dụng phiên đăng nhập (project setup trong `global.setup.ts`) thay vì login UI mỗi test. Giữ login-UI test riêng ở suite `01-auth`.

---

## 3. Ma trận tính năng → Test Suite

Mức ưu tiên: **P0** = luồng nghiệp vụ cốt lõi (phải xanh), **P1** = quan trọng, **P2** = phụ/biên.

### Suite 01 — Authentication & Account (P0)
Routes: `/Account/Login`, `/Account/Register`, `/Account/AccessDenied`; API `api/auth/*`, `api/auth/api-keys`.
- TC: Login hợp lệ → vào dashboard; sai mật khẩu → báo lỗi; field rỗng → validation.
- TC: Register user mới → đăng nhập được; email trùng → lỗi.
- TC: Logout → quay về Login, session bị xóa (truy cập `/dashboard` redirect Login).
- TC: Đổi mật khẩu (`change-password`) → login lại bằng mật khẩu mới.
- TC: Cập nhật profile (`PUT api/auth/profile`) → hiển thị tên/avatar mới.
- TC: Thu hồi session (`DELETE api/auth/sessions`) → context cũ bị đăng xuất.
- TC: Truy cập trang yêu cầu quyền admin bằng member → `AccessDenied`.

### Suite 02 — Dashboard (P0)
Page `DashboardPage.vue`; API `api/dashboard/{overview, attention-summary, recent-activities, strategic-overview}`, `ai-strategy`, `api/dashboard/v2/projects/{id}/summary`.
- TC: Dashboard load → summary cards có số liệu; widget Recent Activity hiển thị.
- TC: Attention/Risk card render; click item → điều hướng đúng task/project.
- TC: Strategic Overview AI render (chấp nhận fallback khi chưa cấu hình AI).
- TC: Nút "AI strategy" → trả response (mock/fallback OK), không vỡ UI.
- TC: WelcomeOverlay xuất hiện lần đầu rồi ẩn.

### Suite 03 — Projects (P0)
Pages `ProjectsPage`, `ProjectDetailPage` (tabs: Activity, Gantt, Members, Stats, Wiki, Workload), `ArchivedProjectsPage`. API `api/projects/*`.
- TC: Tạo project (UI modal) → xuất hiện trong grid/list; toggle grid↔list view.
- TC: Sửa project (tên, mô tả, ngày, logo) → cập nhật hiển thị.
- TC: Xóa mềm → vào `/projects/archived` (trash); Restore → quay lại; Hard delete → biến mất hẳn.
- TC: Thêm/xóa member; chỉnh permission member (`PATCH members/{id}/permissions`).
- TC: Labels CRUD trong project.
- TC: Tab Gantt render timeline; Tab Workload hiển thị tải theo người; Tab Stats hiển thị biểu đồ.
- TC: Tab Activity hiển thị audit/timeline; `mine` chỉ project của tôi.

### Suite 04 — Tasks (P0)
Pages `TasksPage`, `TaskList`, `TaskItem`; tab Tasks trong ProjectDetail. API `api/tasks/*`.
- TC: Tạo task → hiển thị; sửa task (PUT); xóa task.
- TC: Kanban board: kéo-thả đổi cột (`kanban/move`) → trạng thái cập nhật, bền sau reload.
- TC: Đổi status nhanh (`PATCH status`); sort-order; dates (`PATCH dates`).
- TC: Batch select → batch-delete / batch-status.
- TC: Dependencies: thêm phụ thuộc → hiển thị trên Gantt; xóa phụ thuộc.
- TC: Task attention/overdue: task quá hạn hiển thị cảnh báo; "nudge" gửi nhắc; "viewed" đánh dấu đã xem.
- TC: Time entries: start timer → stop → có bản ghi; nhập manual; xem theo project.
- TC: Lọc theo assignee (`assignee/{id}`); my tasks.

### Suite 05 — Sprints (P1)
API `api/projects/{id}/sprints`, `sprints/{id}`, timeline.
- TC: Tạo sprint cho project; sửa (PATCH); xóa.
- TC: Gán task vào sprint; xem sprint timeline.

### Suite 06 — Comments & Attachments / Evidence (P1)
API `api/comments`, `api/attachments`, `api/storage`.
- TC: Thêm comment vào task → hiển thị realtime; xóa comment.
- TC: Upload attachment vào task → list; tải về; xóa.
- TC: Đánh dấu evidence (`PATCH evidence`) và review evidence (`POST evidence/review`).
- TC: Phát hiện trùng (`duplicates`) → deduplicate; storage dedup.

### Suite 07 — Wiki (P1)
Page `WikiDetailPage`; tab Wiki; API `api/projects/{id}/wiki`.
- TC: Tạo wiki page → hiển thị trong tab; mở detail (`/projects/:id/wiki/:wikiId`).
- TC: Sửa nội dung (md-editor) → lưu; xóa.
- TC: Phân quyền visibility (đã có integration test) → member ngoài không thấy wiki riêng tư.

### Suite 08 — Import (P0 — tính năng nổi bật)
Components `import/*`; API `api/import/*`. Files fixture cần: csv, xlsx, docx, zip, pdf.
- TC: Import task CSV: upload → mapping step → preview → execute → task xuất hiện; Undo banner hoàn tác.
- TC: Import task XLSX tương tự; tải template `templates/tasks.csv` & `.xlsx`.
- TC: Import document DOCX → preview → tạo Wiki page (đã có ở smoke).
- TC: Import ZIP chứa `.md/.txt/.html` → preview nhiều file → execute.
- TC: Import PDF → báo unsupported/roadmap rõ ràng (không crash).
- TC: Mapping sai cột → validation; file rỗng/định dạng lạ → lỗi thân thiện.
- TC: Sessions: list import session theo project; xóa session.

### Suite 09 — Teams / Groups & Chat (P0 — realtime)
Pages `TeamsPage`, components `chat/*`; API `api/groups/*`; SignalR `GroupHub`.
- TC: Tạo group (UI) → mở group page; sửa; xóa; dissolve.
- TC: Đổi avatar / background / background-image group.
- TC: Members: thêm/xóa, đổi role (`PATCH role`), rời nhóm (`members/me`).
- TC: Invitations: tạo lời mời → accept/reject bằng token (context thứ 2).
- TC: **Chat realtime 2-context**: gửi message → context kia nhận (đã có); gửi attachment.
- TC: Message ops: edit (`PATCH`), recall, pin, reaction, forward, delete-for-me.
- TC: Read receipt (`POST read`) cập nhật unread; notification-preference.
- TC: Tạo project từ group (`create-project`).

### Suite 10 — Polls (P1 — realtime)
Page `GroupPollPage`, `PollCard`; API `api/groups/{id}/polls/*`.
- TC: Tạo poll (đã có ở smoke) → vote → đếm phiếu cập nhật.
- TC: Vote ở context 2 → context 1 thấy số phiếu tăng (realtime).
- TC: Đóng poll (`close`) → không vote được nữa; xem results.
- TC: Sửa/xóa poll.

### Suite 11 — Meetings & Screen Share (P0 — realtime)
Page `GroupMeetingPage`, `MeetingControls`, `ScreenSharePanel`; API `api/groups/{id}/meetings/*`, `api/meetings/*`; `GroupMeetingPresenceTracker`.
- TC: Start meeting → status active; 2-context join → cả hai thấy participant (đã có; media count cần LiveKit thật).
- TC: End meeting → status kết thúc ở cả 2 context.
- TC: Presence/reconnect: đóng context 2 → participant list cập nhật.
- TC: Action items: lấy danh sách (`action-items`), tạo task từ action item (`create-task`), link task, auto-checknote.
- TC: Import Meetily (`import/meetily`) → action items hiển thị.
- TC: Screen share: nhánh unsupported/lỗi không crash, có toast rõ (positive case cần headful + LiveKit).

### Suite 12 — AI (Erumi / Group AI / Chatbot) (P1)
Components `chat/ErumiChatPanel`, `FloatingChatbot`, `GroupAiPanel`; API `api/ai/*`, `api/groups/{id}/ai/*`, `AiHub`.
- TC: FloatingChatbot mở → gửi câu hỏi → nhận trả lời (mock/fallback chấp nhận).
- TC: AI chat stream (`chat/stream`) → text stream dần qua AiHub.
- TC: AI subtasks: sinh subtask cho task → confirm draft (`drafts/{id}/confirm`).
- TC: AI priority / summary / risks / insights / assignment-insight cho project/task → render, fallback OK.
- TC: AI search (`api/ai/search`); export project (`export/{projectId}`).
- TC: Group AI: summary cuộc trò chuyện, action-items, draft-project từ group.

### Suite 13 — Analytics (P1)
Page `AnalyticsPage` (Erumi); API `api/analytics/{projects/{id}, workspace}`.
- TC: Mở `/analytics` → heading "Hôm nay Erumi", composer hiển thị (đã có smoke).
- TC: Analytics workspace render charts; analytics theo project.

### Suite 14 — Notifications & Search (P1)
`NotificationHub`, `TopHeader`; API `api/notifications/*`, `api/search`, `api/push`.
- TC: Hành động (gán task/comment) → notification realtime xuất hiện, unread-count tăng.
- TC: Đánh dấu đã đọc 1 cái / read-all → badge về 0.
- TC: Global search → trả kết quả project/task/wiki; click → điều hướng.
- TC: Push subscribe (`api/push/subscribe`) đăng ký thành công (có thể chỉ API-level).

### Suite 15 — Settings: API Keys & Webhooks (P2)
Page `SettingsPage`, `ApiKeysTab`, `WebhooksTab`; API `api/auth/api-keys`, `api/projects/{id}/webhooks`.
- TC: Tạo API key → hiển thị (1 lần), thu hồi (DELETE).
- TC: Webhook CRUD; "Test" webhook (`POST {id}/test`) → kết quả gửi.

### Suite 16 — Admin, Organizations, Audit Logs (P1/P2)
API `api/admin/users`, `api/organizations`, `api/audit-logs`, `api/users`, `api/votes`.
- TC (admin): Liệt kê user, tạo user, sửa (`PATCH`), import user CSV.
- TC: Organizations CRUD + thêm/xóa member.
- TC: Audit logs: xem theo entity / theo user / `mine` / `recent`.
- TC: Votes: upvote target (`api/votes/{type}/{id}`).
- TC (phân quyền): member gọi API admin → 403.

### Suite 17 — Cross-cutting (P0/P1)
- **Auth boundary**: outsider truy cập project/group không thuộc về → 403/redirect (soi gương `AuthBoundaryIntegrationTests`).
- **Console errors**: mở rộng `browser-console.spec.ts` để quét mọi route chính không có lỗi console nghiêm trọng.
- **i18n**: chuyển vi↔en → label đổi đúng.
- **Responsive**: sidebar collapse ở viewport mobile; layout không vỡ.
- **Empty/error states**: list rỗng hiển thị empty state; mất mạng/500 → toast lỗi.
- **Navigation**: SidebarNav tới mọi route; deep-link reload giữ trạng thái; route lạ → redirect `/dashboard`.

---

## 4. Hạ tầng test cần xây trước (theo thứ tự)

1. **`api.helpers.ts`** — trích `apiResult`/`apiCommand`/`responseBody` từ smoke ra dùng chung + factory tạo/dọn project, group, task, sprint, wiki, label, user.
2. **`auth.fixture.ts`** — fixture `adminPage` / `memberPage` / `outsiderPage` dùng `storageState`; `global.setup.ts` đăng nhập 1 lần, seed tài khoản test.
3. **Seed dữ liệu** — script seed admin (`admin@qaly.dev` / env), member, outsider trước khi chạy (DB seed hoặc API register).
4. **Bổ sung `data-testid`** cho các vùng thiếu selector ổn định (kanban cột, task card, message item, poll option, participant).
5. **Fixtures files** — tasks.csv/xlsx, doc.docx, bundle.zip, sample.pdf, image.png trong `fixtures/files/`.

---

## 5. Chạy & CI

```bash
# Local
npm run build                       # build FE vào wwwroot/dist
dotnet run --project src/Qaly.Web   # app tại http://localhost:5000
E2E_BASE_URL=http://localhost:5000 npx playwright test

# Theo suite
npx playwright test tests/e2e/specs/04-tasks
```

**CI (GitHub Actions):**
- Spin up SQL Server + Redis (docker compose services), `dotnet ef database update`, seed test users.
- `dotnet run` (hoặc dùng `webServer` trong `playwright.config.ts` để Playwright tự khởi động app).
- Chạy `chromium` (cân nhắc thêm `firefox`/`webkit` cho P0).
- Artifact: HTML report + trace/video on failure (đã cấu hình).
- Env cần: `E2E_ADMIN_EMAIL/PASSWORD`, `E2E_MEMBER_EMAIL/PASSWORD`, (tùy chọn) `OPENAI_API_KEY`/`GEMINI_API_KEY`/LiveKit cho headful AI/meeting.

**Đề xuất thêm `webServer` block** vào config để CI tự quản app:
```ts
webServer: {
  command: 'dotnet run --project src/Qaly.Web',
  url: 'http://127.0.0.1:5000',
  timeout: 180_000,
  reuseExistingServer: !process.env.CI,
}
```

---

## 6. Giới hạn đã biết (cần môi trường thật)
- **LiveKit media** (đếm participant audio/video, screen share positive) → cần LiveKit thật + chế độ headful với fake media (`--use-fake-device-for-media-stream`).
- **AI thật** → cần API key provider; mặc định nghiệm thu bằng fallback/mock, assert "không crash + có response" thay vì nội dung cụ thể.
- **Push notification thật** → kiểm ở mức API/subscribe.
- **Email** (register/invite) → kiểm qua MailHog (`localhost:8025`).

---

## 7. Lộ trình triển khai đề xuất
| Giai đoạn | Nội dung | Suite |
|---|---|---|
| 1 | Hạ tầng + auth fixtures + mở rộng smoke | §4, 01, 17(console) |
| 2 | Core CRUD: Projects, Tasks, Sprints | 03, 04, 05 |
| 3 | Collaboration: Teams/Chat, Polls, Comments/Attachments | 06, 09, 10 |
| 4 | Import + Wiki + Meetings | 07, 08, 11 |
| 5 | AI + Analytics + Notifications/Search | 12, 13, 14 |
| 6 | Settings + Admin/Org + Cross-cutting đầy đủ | 15, 16, 17 |
```
