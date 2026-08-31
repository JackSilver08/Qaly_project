# Kế hoạch công việc trước bàn giao — Qaly Release 4.0

**Hạn chót: 01/09/2026** · Lập ngày 29/08/2026 · Nhánh gốc: `main`

Nhân sự: **Chí Khang · Quốc Bảo · Viết Minh · Gia Long**
Tài liệu kiểm thử nền: [`docs/17_Kiem_Thu_Toan_Dien_Va_Ban_Giao.md`](./17_Kiem_Thu_Toan_Dien_Va_Ban_Giao.md)

---

## 1. Phạm vi

### 1.1 Ngoài phạm vi sửa chức năng

**Trang Dự án (`ProjectsPage.vue`) và Chi tiết dự án (`ProjectDetailPage.vue`) do Duy Hoàng phụ
trách.** Nhóm 4 người **không sửa logic/chức năng** hai trang này.

Với hai trang đó nhóm **chỉ được**:

- Kiểm thử và ghi nhận lỗi (báo lại cho Duy Hoàng, không tự sửa).
- Sửa **giao diện thuần**: màu sắc/dark theme, khoảng cách, `aria-label`, bỏ emoji, chữ tràn.

> Mọi thay đổi chạm vào `ProjectsPage.vue` / `ProjectDetailPage.vue` phải ghi rõ trong mô tả PR là
> **"chỉ sửa giao diện"** và tag Duy Hoàng review.

Ngoại lệ đã xử lý xong: lỗi **QALY-UI-03** (`ProjectDetailPage.vue` ném `TypeError` khi mở trang)
đã được vá và build lại trước khi kế hoạch này bắt đầu — không ai cần đụng lại.

### 1.2 Trong phạm vi

Toàn bộ phần còn lại: Dashboard, Nhiệm vụ, Nhóm/Họp/Poll, Wiki, Import, Trợ lý AI & AI-native,
Phân tích, Cài đặt, Hồ sơ, Quản trị/Tổ chức/Moderator, Dự án đã lưu trữ, khung ứng dụng (shell),
và toàn bộ hạ tầng kiểm thử.

---

## 2. Trạng thái nền (đã đo ngày 29/08)

| Hạng mục | Kết quả | Ghi chú |
| --- | --- | --- |
| Backend unit | ✅ 595/595 pass | |
| Frontend unit | ✅ 155/155 pass | Lớp `tests/client/` vừa được dựng |
| Typecheck + Build FE | ✅ Pass | |
| E2E `--workers=1` | ✅ Pass | Đã sửa 7 spec hỏng |
| E2E 4 worker | ⚠️ 61 pass / 6 fail | **Cả 6 đều pass khi chạy tuần tự → flaky do song song, không phải lỗi chức năng** |
| Integration | ✅ 166/166 pass | Đã chạy 29/08 |
| Web feature | ✅ 38/38 pass | Đã chạy 29/08 |
| Dark theme | ✅ 6/6 pass | 12 route + mobile + overlay + chi tiết dự án + họp/poll |
| Console trình duyệt | ✅ 3/3 pass | Đã mở rộng từ 5 lên 12 route |
| Responsive 1440/768/390 | ✅ 4/4 pass | Spec mới `responsive-audit.spec.ts`, không có tràn ngang |
| Checklist QA theo module | ◐ 42/59 tick | 16 mục còn lại cần người thao tác tay |

### Lỗi đã sửa sẵn (không cần làm lại, chỉ cần verify)

| ID | Lỗi |
| --- | --- |
| QALY-UI-01 | Số "quá hạn" Dashboard lệch trang Nhiệm vụ (task `Cancelled` bị đếm nhầm) |
| QALY-UI-02 | Dòng nhiệm vụ không dùng được bằng bàn phím |
| QALY-UI-03 | Mở trang chi tiết dự án ném `TypeError` mỗi lần |
| QALY-BE-01 | Hint `deepseek-v4-pro` định tuyến nhầm sang Ollama |

---

## 3. Nợ kỹ thuật đã đo được (dữ liệu thật, không phải ước lượng)

> ### ⚠️ Đính chính ngày 29/08 — đọc trước khi nhận việc
>
> Bản đầu của kế hoạch này xếp **dark theme là ưu tiên số 1** với ~130 chỗ màu hardcode, mô tả là
> "nguy cơ vỡ dark theme". **Điều đó sai.** Sau khi chạy `tests/e2e/dark-theme.spec.ts`:
> **6/6 pass** trên 12 route + mobile + overlay + chi tiết dự án + họp/poll.
>
> Lý do: `style.css` đã có sẵn khối "dark-mode surface corrections" với **338 rule
> `:root[data-theme='dark']`** vá lại toàn bộ các màu hardcode đó.
>
> **Dark theme đang chạy đúng.** Các mục 3.1 dưới đây là **nợ kỹ thuật về độ bền** (thêm màu mới mà
> quên override thì vỡ âm thầm), **không phải lỗi đang hiện hữu**.
>
> **Hệ quả cho kế hoạch:** các việc dark theme (CK-1, QB-1, VM-1, VM-2, GL-4 phần theme) **hạ xuống
> ưu tiên thấp nhất, để sau bàn giao**. Thời gian giải phóng dồn vào **accessibility, unit test và
> chạy checklist** — đó mới là phần thật sự còn trống.

### 3.1 Màu nền sáng hardcode — nợ kỹ thuật, KHÔNG phải lỗi đang hiện hữu

Số lần khai báo `background: #fff…/#f8…/#f9…` cố định trong từng file:

| File | Số chỗ | Người phụ trách |
| --- | ---: | --- |
| `pages/TeamsPage.vue` | 25 | Viết Minh |
| `components/chat/ChatWindow.vue` | 13 | Viết Minh |
| `pages/TasksPage.vue` | 12 | Quốc Bảo |
| `components/GitHubProjectManagement.vue` | 10 | Gia Long |
| `components/import/ImportModal.vue` | 9 | Viết Minh |
| `components/chat/MessageItem.vue` | 8 | Viết Minh |
| `components/chat/GroupAiPanel.vue` | 8 | Chí Khang |
| `components/chat/ErumiChatPanel.vue` | 7 | Chí Khang |
| `components/chat/AiActivityPanel.vue` | 7 | Chí Khang |
| `components/AiPlannerModal.vue` | 7 | Chí Khang |
| `App.vue` | 6 | Quốc Bảo |
| `components/chat/PollCard.vue` | 5 | Viết Minh |
| `components/ProjectRoadmapTab.vue` | 35 | ⚠️ Duy Hoàng — chỉ báo, không sửa |
| `pages/ProjectsPage.vue` | 10 | ⚠️ chỉ sửa giao diện |

Cách sửa: thay bằng biến CSS đã có sẵn trong dự án — `var(--panel)`, `var(--bg-soft)`,
`var(--line)`, `var(--text-strong)`, `var(--muted)` (xem `PageStatePanel.vue` làm mẫu chuẩn).

### 3.2 Emoji lẫn trong giao diện

Dự án đã thống nhất dùng **Bootstrap Icons / lucide**, không dùng emoji (spec
`roadmap-ui.spec.ts` đã có assert chặn emoji trong nút).

| File | Số chỗ | Người phụ trách |
| --- | ---: | --- |
| `pages/SettingsPage.vue` | 8 | Gia Long |
| `components/import/ImportConfirmStep.vue` | 8 | Viết Minh |
| `pages/GroupMeetingPage.vue` | 7 | Viết Minh |
| `components/import/ImportMappingStep.vue` | 7 | Viết Minh |
| `components/chat/ErumiChatPanel.vue` | 3 | Chí Khang |
| `components/chat/AiActivityPanel.vue` | 2 | Chí Khang |
| `components/ErumiDiffPreviewModal.vue` | 2 | Chí Khang |
| `components/AiOnboardingGuideModal.vue` | 2 | Chí Khang |
| `components/import/ImportModal.vue` | 2 | Viết Minh |
| `components/import/ImportUndoBanner.vue` | 1 | Viết Minh |
| `components/chat/MessageItem.vue` | 1 | Viết Minh |
| `components/chat/ChatWindow.vue` | 1 | Viết Minh |

### 3.3 Nút icon cần rà `aria-label` / `title`

Toàn dự án hiện có 54 file dùng `aria-label`. Các file nhiều nút icon nhất cần rà:

`TeamsPage.vue` (10) · `PrivacySettingsTab.vue` (8) · `FloatingChatbot.vue` (8) ·
`AiPlannerModal.vue` (6) · `OrganizationsPage.vue` (5) · `ChatWindow.vue` (5) ·
`OrganizationUsersPage.vue` (4)

### 3.4 Composable chưa có unit test

| File | Số dòng | Người phụ trách |
| --- | ---: | --- |
| `composables/use-task-actions.ts` | 265 | Quốc Bảo |
| `composables/use-project-actions.ts` | 181 | Quốc Bảo |
| `composables/use-dashboard-state.ts` | 142 | Quốc Bảo |
| `composables/use-speech-recognition.ts` | 106 | Chí Khang |
| `composables/use-meeting-recovery.ts` | 92 | Viết Minh |
| `utils/github-api.ts` | 86 | Gia Long |
| `composables/use-erumi-context.ts` | 50 | Chí Khang |

---

## 4. Phân công theo người

Mã công việc: `CK` Chí Khang · `QB` Quốc Bảo · `VM` Viết Minh · `GL` Gia Long

### 4.1 Chí Khang — Trợ lý AI & AI-native

| Mã | Công việc | Tiêu chí hoàn thành | Hạn |
| --- | --- | --- | --- |
| CK-1 | Dark theme: `ErumiChatPanel`, `GroupAiPanel`, `AiActivityPanel`, `AiPlannerModal` (29 chỗ hardcode) | Bật/tắt theme tối không còn khối nền sáng; tương phản chữ đạt WCAG AA | 30/08 |
| CK-2 | Bỏ emoji trong `ErumiChatPanel`, `AiActivityPanel`, `ErumiDiffPreviewModal`, `AiOnboardingGuideModal` (9 chỗ) | Thay bằng lucide/Bootstrap Icons; không còn emoji trong nút/nhãn | 30/08 |
| CK-3 | `aria-label` cho nút icon trong `FloatingChatbot` (8), `AiPlannerModal` (6) | Mọi nút icon đọc được bằng screen reader | 31/08 |
| CK-4 | Unit test `use-erumi-context.ts`, `use-speech-recognition.ts` | ≥ 10 test, `npm run test:unit` xanh | 31/08 |
| CK-5 | Kiểm thử luồng Trợ lý AI theo checklist mục 3.5 tài liệu 17 | Tick hết checklist; lỗi mới ghi vào QA_LOG | 31/08 |
| CK-6 | Xác minh QALY-BE-01: provider/model thực tế hiển thị đúng, fallback có nhãn rõ | Có ảnh chụp minh chứng | 01/09 |

### 4.2 Quốc Bảo — Nhiệm vụ & Dashboard

| Mã | Công việc | Tiêu chí hoàn thành | Hạn |
| --- | --- | --- | --- |
| QB-1 | Dark theme: `TasksPage.vue` (12), `App.vue` (6) | Kanban + danh sách task hiển thị đúng ở cả 2 theme | 30/08 |
| QB-2 | Unit test `use-task-actions.ts` (265 dòng) | Phủ nhánh thành công + nhánh lỗi, ≥ 15 test | 30/08 |
| QB-3 | Unit test `use-dashboard-state.ts`, `use-project-actions.ts` | Phủ `loadDashboard` khi lỗi → trạng thái rỗng trung thực, ≥ 15 test | 31/08 |
| QB-4 | **Xác minh QALY-UI-01**: số "quá hạn" ở Dashboard **khớp** trang Nhiệm vụ, kể cả khi có task `Cancelled` quá hạn | Tạo 1 task `Cancelled` quá hạn → 2 nơi hiển thị cùng số | 30/08 |
| QB-5 | Rà bàn phím cho mọi dòng/thẻ click được ngoài `TaskItem` (23 chỗ toàn dự án) | Tab tới được + Enter/Space kích hoạt được | 31/08 |
| QB-6 | Kiểm thử checklist mục 3.3 (Dự án & Nhiệm vụ) — phần Nhiệm vụ | Tick hết checklist | 31/08 |

### 4.3 Viết Minh — Nhóm / Họp / Wiki / Import

| Mã | Công việc | Tiêu chí hoàn thành | Hạn |
| --- | --- | --- | --- |
| VM-1 | Dark theme: `TeamsPage.vue` (25 — nhiều nhất dự án) | Trang Nhóm hiển thị đúng ở cả 2 theme | 30/08 |
| VM-2 | Dark theme: `ChatWindow` (13), `MessageItem` (8), `PollCard` (5), `ImportModal` (9) | Chat + poll + import đúng theme | 31/08 |
| VM-3 | Bỏ emoji: `ImportConfirmStep` (8), `ImportMappingStep` (7), `GroupMeetingPage` (7), `ImportModal` (2), `ImportUndoBanner` (1), `MessageItem` (1), `ChatWindow` (1) | Không còn emoji trong nút/nhãn | 30/08 |
| VM-4 | `aria-label` nút icon `TeamsPage` (10), `ChatWindow` (5) | Mọi nút icon có nhãn | 31/08 |
| VM-5 | Unit test `use-meeting-recovery.ts` | ≥ 8 test | 31/08 |
| VM-6 | Kiểm thử checklist mục 3.6 (Nhóm, Họp & Wiki) — đặc biệt **chat realtime 2 phiên** | Tick hết checklist; xác nhận SignalR đồng bộ | 31/08 |

### 4.4 Gia Long — Quản trị / Cài đặt / Hạ tầng kiểm thử

| Mã | Công việc | Tiêu chí hoàn thành | Hạn |
| --- | --- | --- | --- |
| GL-1 | **Chạy lại Integration + Web feature test**, ghi số liệu vào tài liệu 17 mục 2 | 0 fail; coverage integration ≥ 16% line | **29/08** |
| GL-2 | **Ổn định E2E chạy song song** — 6 spec flaky ở 4 worker (đã xác nhận pass khi `--workers=1`) | Chạy `npm run test:e2e` 2 lần liên tiếp đều 0 fail, hoặc chốt hạ `E2E_WORKERS=2` trong CI kèm lý do | 31/08 |
| GL-3 | Thêm `playwright-report/` vào `.gitignore` + `git rm -r --cached playwright-report` | `git status` sạch sau khi chạy E2E | 29/08 |
| GL-4 | Bỏ emoji `SettingsPage.vue` (8); dark theme `GitHubProjectManagement.vue` (10) | Không còn emoji; theme đúng | 30/08 |
| GL-5 | `aria-label`: `PrivacySettingsTab` (8), `OrganizationsPage` (5), `OrganizationUsersPage` (4) | Mọi nút icon có nhãn | 31/08 |
| GL-6 | Unit test `utils/github-api.ts` | ≥ 8 test | 31/08 |
| GL-7 | Kiểm thử checklist mục 3.1, 3.2, 3.7 (Xác thực, RBAC, GitHub) | Tick hết checklist | 31/08 |

---

## 5. Việc chung — cả nhóm

| Mã | Công việc | Ai làm | Hạn | Trạng thái |
| --- | --- | --- | --- | --- |
| G-1 | **Kiểm thử chéo**: mỗi người test khu vực của người khác theo vòng CK→QB→VM→GL→CK | Cả 4 | 01/09 sáng | ⏸ **Chưa làm được** — chưa có gì để test chéo cho tới khi CK/QB/VM/GL xong việc của mình |
| G-2 | Console trình duyệt phải sạch | Gia Long tổng hợp | 31/08 | ☑ **Xong 29/08** — đã mở rộng `browser-console.spec.ts` từ 5 lên **12 route**, chạy 3/3 pass |
| G-3 | Rà responsive 1440 / 768 / 390px | Mỗi người khu vực mình | 31/08 | ☑ **Xong 29/08** — đã dựng `tests/e2e/responsive-audit.spec.ts` tự động hoá, 4/4 pass, không có tràn ngang |
| G-4 | Cập nhật `QA_LOG.md` cho commit bàn giao | Gia Long | 01/09 | ☑ **Xong 29/08** — đã ghi baseline; cần cập nhật lại ở commit bàn giao cuối |
| G-5 | Tick checklist mục 3 tài liệu 17; mục nào ⚠/✗ phải ghi lý do | Cả 4 | 01/09 sáng | ◐ **42/59 mục đã tick** bằng bằng chứng test tự động; 16 mục cần người thao tác tay vẫn để trống |
| G-6 | Báo lỗi trang Dự án / Chi tiết dự án cho **Duy Hoàng** | Cả 4 | Liên tục | ☑ **Xong 29/08** — [`docs/19_Bao_loi_Trang_Du_an_gui_Duy_Hoang.md`](./19_Bao_loi_Trang_Du_an_gui_Duy_Hoang.md) |

### Đã bổ sung vào bộ kiểm thử khi làm việc chung

| File | Nội dung |
| --- | --- |
| `tests/e2e/responsive-audit.spec.ts` | **Mới.** Kiểm tra tràn ngang ở 3 breakpoint trên 11 trang nhóm phụ trách. Khi fail thì in ra đúng phần tử nào tràn, rộng bao nhiêu, kèm ảnh — không phải đi dò |
| `tests/e2e/browser-console.spec.ts` | Mở rộng từ 5 lên 12 route: bổ sung `/projects/archived`, `/organizations`, `/organizations/users`, `/admin/users`, `/admin/moderators`, `/settings`, `/profile` |

---

## 6. Lịch theo ngày

| Ngày | Trọng tâm |
| --- | --- |
| **29/08 (Thứ Bảy)** | GL-1 lấy số liệu integration/web-feature · GL-3 dọn git · Cả nhóm đọc tài liệu 17, nhận việc, tạo nhánh |
| **30/08 (Chủ Nhật)** | Dứt điểm **dark theme + bỏ emoji** (CK-1, CK-2, QB-1, QB-4, VM-1, VM-3, GL-4) · QB-2 |
| **31/08 (Thứ Hai)** | Dứt điểm **a11y + unit test + kiểm thử theo checklist** (CK-3→5, QB-3, QB-5, QB-6, VM-2, VM-4→6, GL-2, GL-5→7, G-2, G-3) |
| **01/09 (Thứ Ba)** | Sáng: kiểm thử chéo (G-1), tick checklist (G-5), sửa lỗi phát sinh · Chiều: **đóng băng code**, chạy full test, cập nhật QA_LOG, bàn giao |

> **Mốc đóng băng: 12:00 ngày 01/09.** Sau mốc này chỉ sửa lỗi chặn (blocker), mọi thay đổi khác dời
> sang sau bàn giao.

---

## 7. Quy tắc làm việc

1. **Mỗi người một nhánh**: `fix/<tên>-<mã-công-việc>`, ví dụ `fix/vietminh-VM-1-dark-theme-teams`.
2. **Trước khi push, bắt buộc chạy**:
   ```bash
   npm run test:unit
   npm run typecheck
   npm run build
   ```
3. **Bắt buộc commit `src/Qaly.Web/wwwroot/dist`** sau khi `npm run build` — CI có bước
   `git diff --exit-code -- src/Qaly.Web/wwwroot/dist`, quên commit là CI đỏ.
4. **Sửa CSS phải dùng biến theme**, không hardcode màu. Mẫu chuẩn: `PageStatePanel.vue`.
5. **Không sửa logic `ProjectsPage.vue` / `ProjectDetailPage.vue`** (mục 1.1).
6. **Chạy E2E gỡ lỗi thì dùng `--workers=1`** để tránh nhiễu do song song:
   ```bash
   npx playwright test tests/e2e/<spec> --workers=1
   ```
7. Lỗi mới phát hiện → ghi ngay vào `QA_LOG.md` kèm đường dẫn evidence trong `test-results/`.

---

## 8. Tiêu chí nghiệm thu (Definition of Done)

Phải đạt **trong cùng một lần chạy** trên nhánh bàn giao:

1. ☐ `dotnet test Qaly_project.slnx -c Release` — 0 fail
2. ☐ `npm run test:unit` — 0 fail (mục tiêu ≥ 200 test sau khi bổ sung)
3. ☐ `npm run typecheck` — 0 lỗi
4. ☐ `npm run build` + `git diff --exit-code -- src/Qaly.Web/wwwroot/dist` sạch
5. ☐ `npm run test:e2e` — 0 fail (hoặc 0 fail với `E2E_WORKERS=2` đã chốt)
6. ☐ Coverage unit ≥ 43% line, integration ≥ 16% line
7. ☐ Console trình duyệt sạch trên các luồng chính
8. ☐ Không còn emoji trong nút/nhãn giao diện
9. ☐ Dark theme không còn khối nền sáng lọt
10. ☐ Mọi nút icon có `aria-label`; mọi phần tử click được đều dùng được bằng bàn phím
11. ☐ Checklist mục 3 tài liệu 17 tick hết
12. ☐ `QA_LOG.md` cập nhật cho commit bàn giao

---

## 9. Rủi ro & phương án dự phòng

| Rủi ro | Mức | Phương án |
| --- | --- | --- |
| Chỉ còn ~3 ngày, khối lượng dark theme lớn (~130 chỗ hardcode) | **Cao** | Ưu tiên theo lưu lượng dùng: Nhóm → Nhiệm vụ → Chat AI → còn lại. Trang ít dùng có thể chấp nhận nợ, ghi rõ trong QA_LOG |
| E2E song song còn flaky | Trung bình | Đã có phương án dự phòng: hạ `E2E_WORKERS=2` trong CI (GL-2) |
| Phụ thuộc Duy Hoàng cho lỗi trang Dự án | Trung bình | Báo lỗi sớm ngay 29/08, không đợi tới ngày cuối |
| Sửa CSS gây hồi quy chỗ khác | Trung bình | Bắt buộc kiểm thử chéo (G-1) trước khi đóng băng |
| Provider AI thật cần API key + ngân sách | Trung bình | Luồng live chỉ demo khi có key; còn lại dùng fixture. Ghi rõ giới hạn khi bàn giao |
| `canAccessModule` fail-open ở client | Thấp | Không sửa trước bàn giao — server vẫn là biên enforcement. Ghi vào nợ kỹ thuật |

---

## 10. Bảng theo dõi tiến độ

Cập nhật cuối mỗi ngày. Trạng thái: ☐ chưa làm · ◐ đang làm · ☑ xong · ⚠ vướng

| Mã | Người | Công việc | Hạn | 29/08 | 30/08 | 31/08 | 01/09 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| CK-1 | Chí Khang | Dark theme chat AI | 30/08 | ☐ | ☐ | | |
| CK-2 | Chí Khang | Bỏ emoji AI | 30/08 | ☐ | ☐ | | |
| CK-3 | Chí Khang | aria-label AI | 31/08 | | ☐ | ☐ | |
| CK-4 | Chí Khang | Unit test AI composable | 31/08 | | ☐ | ☐ | |
| CK-5 | Chí Khang | Checklist 3.5 | 31/08 | | | ☐ | |
| CK-6 | Chí Khang | Verify QALY-BE-01 | 01/09 | | | | ☐ |
| QB-1 | Quốc Bảo | Dark theme Tasks/App | 30/08 | ☐ | ◐ | ☑ | |
| QB-2 | Quốc Bảo | Unit test use-task-actions | 30/08 | ☐ | ◐ | ☑ | |
| QB-3 | Quốc Bảo | Unit test dashboard/project actions | 31/08 | | ◐ | ☑ | |
| QB-4 | Quốc Bảo | Verify QALY-UI-01 | 30/08 | ☐ | ◐ | ☑ | |
| QB-5 | Quốc Bảo | A11y bàn phím | 31/08 | | | ☑ | |
| QB-6 | Quốc Bảo | Checklist 3.3 | 31/08 | | | ☑ | |
| VM-1 | Viết Minh | Dark theme TeamsPage | 30/08 | ☐ | ☐ | | |
| VM-2 | Viết Minh | Dark theme chat/poll/import | 31/08 | | ☐ | ☐ | |
| VM-3 | Viết Minh | Bỏ emoji import/meeting | 30/08 | ☐ | ☐ | | |
| VM-4 | Viết Minh | aria-label Teams/Chat | 31/08 | | | ☐ | |
| VM-5 | Viết Minh | Unit test meeting-recovery | 31/08 | | | ☐ | |
| VM-6 | Viết Minh | Checklist 3.6 + realtime | 31/08 | | | ☐ | |
| GL-1 | Gia Long | Integration + web feature | **29/08** | ☐ | | | |
| GL-2 | Gia Long | Ổn định E2E song song | 31/08 | | ☐ | ☐ | |
| GL-3 | Gia Long | gitignore playwright-report | 29/08 | ☐ | | | |
| GL-4 | Gia Long | Emoji Settings + theme GitHub | 30/08 | ☐ | ☐ | | |
| GL-5 | Gia Long | aria-label Privacy/Org | 31/08 | | | ☐ | |
| GL-6 | Gia Long | Unit test github-api | 31/08 | | | ☐ | |
| GL-7 | Gia Long | Checklist 3.1/3.2/3.7 | 31/08 | | | ☐ | |
| G-1 | Cả nhóm | Kiểm thử chéo | 01/09 | | | | ☐ |
| G-2 | Gia Long | Console sạch | 31/08 | | | ☐ | |
| G-3 | Cả nhóm | Responsive 3 kích thước | 31/08 | | | ☐ | |
| G-4 | Gia Long | Cập nhật QA_LOG | 01/09 | | | | ☐ |
| G-5 | Cả nhóm | Tick checklist mục 3 | 01/09 | | | | ☐ |
| G-6 | Cả nhóm | Báo lỗi cho Duy Hoàng | Liên tục | ☐ | ☐ | ☐ | ☐ |
