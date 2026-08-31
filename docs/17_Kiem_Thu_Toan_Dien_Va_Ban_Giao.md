# Kiểm thử toàn diện & Bàn giao — Qaly Release 4.0

> Tài liệu công việc kiểm thử cuối kỳ. Dùng để chạy lại toàn bộ các lớp kiểm thử, đối chiếu
> checklist theo module, và theo dõi các lỗi giao diện/chức năng đã phát hiện.
>
> Cập nhật lần cuối: 2026-08-29 · Nhánh: `main`

---

## 1. Kiến trúc kiểm thử (test pyramid)

| Lớp | Vị trí | Phạm vi | Lệnh chạy |
| --- | --- | --- | --- |
| Unit — Backend | `tests/Qaly.UnitTests/` | Domain rules, service logic, AI contract, RBAC, privacy | `dotnet test tests/Qaly.UnitTests/Qaly.UnitTests.csproj -c Release` |
| **Unit — Frontend** | `tests/client/` | utils, composables, component thuần (Vue) | `npm run test:unit` |
| Integration | `tests/Qaly.IntegrationTests/` | API + EF Core + policy, tenant isolation | `dotnet test tests/Qaly.IntegrationTests/Qaly.IntegrationTests.csproj -c Release` |
| Web feature | `tests/Qaly.WebFeatureTests/` | Endpoint surface, routing, auth pipeline | `dotnet test tests/Qaly.WebFeatureTests/Qaly.WebFeatureTests.csproj -c Release` |
| E2E | `tests/e2e/` | Luồng người dùng thật trên trình duyệt | `npm run test:e2e` |

Chạy toàn bộ .NET một lượt: `dotnet test Qaly_project.slnx -c Release`

### Lớp unit frontend (mới bổ sung)

Trước đây frontend chỉ có `typecheck` + `build` + E2E — **không có lớp unit nào**. Mọi lỗi logic
thuần (định dạng, ánh xạ quyền, xử lý lỗi API, quản lý theme/toast) chỉ bị phát hiện khi chạy E2E,
tức là chậm và khó khoanh vùng. Lớp `tests/client/` lấp đúng khoảng trống đó.

- Cấu hình: `vitest.config.ts` (jsdom + `@vue/test-utils`), setup tại `tests/client/setup.ts`.
- Đã nối vào CI: bước `Run frontend unit tests` trong `.github/workflows/ci.yml`.
- Coverage: `npm run test:unit:coverage` → `TestResults/client-coverage/`.

| File | Đối tượng kiểm thử |
| --- | --- |
| `tests/client/formatters.spec.ts` | Định dạng ngày/giờ/dung lượng, viết tắt tên, nhãn trạng thái & vai trò, **quy tắc quá hạn** |
| `tests/client/project-roles.spec.ts` | Danh mục vai trò dự án, nhãn/gợi ý, `fallbackProjectPermissions`, ma trận AI tier |
| `tests/client/api-client.spec.ts` | CSRF, cache GET, redirect 401, ánh xạ lỗi HTTP → tiếng Việt, bóc envelope |
| `tests/client/use-toast.spec.ts` | Hàng đợi toast, giới hạn 5, tự đóng theo `duration`, huỷ timer |
| `tests/client/use-theme.spec.ts` | Sáng/tối, localStorage, đồng bộ đa tab, theo `prefers-color-scheme` |
| `tests/client/use-permissions.spec.ts` | Simulation Mode, header giả lập, `canAccessModule`, AI tier |
| `tests/client/use-confirm-dialog.spec.ts` | Hộp thoại confirm/prompt, dialog chồng nhau, reset trạng thái |
| `tests/client/components.spec.ts` | `PageStatePanel`, `TaskItem`, `ToastContainer` (render + a11y + sự kiện) |

---

## 2. Baseline hiện tại

| Bộ test | Kết quả | Ghi chú |
| --- | --- | --- |
| Backend unit | **595 / 595 pass** | 590 sẵn có + 5 test mới cho routing provider AI |
| Frontend unit | **155 / 155 pass** | 8 file spec mới |
| Integration | **166 / 166 pass** | Chạy lại 29/08. Ngưỡng coverage CI: line ≥ 16% |
| Web feature | **38 / 38 pass** | Chạy lại 29/08 |
| E2E | **61 pass / 6 fail** (4 worker)<br>7/7 spec mục tiêu pass khi `--workers=1` | 25 spec, chromium. Xem mục 4.3 & 4.4 |
| Typecheck | ✅ Pass | `npm run typecheck` |
| Build FE | ✅ Pass | `npm run build` — nhớ commit `src/Qaly.Web/wwwroot/dist` |
| Console trình duyệt | **3 / 3 pass** | `browser-console.spec.ts`, đã mở rộng từ 5 lên **12 route** |
| Responsive 1440/768/390 | **4 / 4 pass** | `responsive-audit.spec.ts` (mới) — không có tràn ngang |
| Dark theme | **6 / 6 pass** | `dark-theme.spec.ts` — 12 route + mobile + overlay + chi tiết dự án + họp/poll |

Ngưỡng coverage CI đang áp: unit ≥ **43%** line, integration ≥ **16%** line
(`scripts/check-coverage.ps1`).

---

## 3. Checklist kiểm thử theo module

Ký hiệu: ☐ chưa kiểm · ☑ đã kiểm & đạt · ⚠ đạt nhưng có ghi chú · ✗ lỗi

**Trạng thái ngày 29/08 — 42 ☑ đạt · 1 ⚠ · 16 ☐ (tổng 59):** các mục ☑ đã được **kiểm chứng bằng
test tự động** — cột "Bằng chứng" ghi rõ file test nào chứng minh. Các mục ☐ cần **người thao tác
tay**, chưa ai làm.

### 3.1 Xác thực & phiên đăng nhập

| | Hạng mục | Bằng chứng |
| --- | --- | --- |
| ☑ | Đăng nhập đúng mật khẩu vào được hệ thống | `qaly.smoke.spec.ts` — *should login successfully* |
| ☐ | Đăng nhập **sai** mật khẩu báo lỗi tiếng Việt | Cần kiểm tay |
| ☑ | Tài khoản bị vô hiệu hoá bị chặn (mức API) | `RcSafetyRegressionTests` (integration) |
| ☐ | Tài khoản bị vô hiệu hoá bị chặn (mức UI) | Cần kiểm tay |
| ☑ | API trả 401 → chuyển `/Account/Login?returnUrl=...` giữ đúng trang đang xem | `tests/client/api-client.spec.ts` |
| ☑ | CSRF: mọi POST/PUT/DELETE đều kèm `X-CSRF-TOKEN`, không ghi đè token do caller truyền | `tests/client/api-client.spec.ts` |
| ☑ | Trang đăng nhập/đăng ký không có lỗi console | `browser-console.spec.ts` |
| ☐ | Đăng xuất xoá sạch phiên, back-button không vào lại trang nội bộ | Cần kiểm tay |

### 3.2 Phân quyền (RBAC) & Tenant isolation

| | Hạng mục | Bằng chứng |
| --- | --- | --- |
| ☑ | 9 vai trò dự án hiển thị đúng nhãn tiếng Việt | `tests/client/project-roles.spec.ts` · `demo-script-30-minutes.spec.ts` (assert đủ ma trận vai trò) |
| ☑ | Viewer/Customer không ghi được task, bình luận, time entry, wiki | `tests/client/project-roles.spec.ts` (client) + `TaskAccessPolicyIsolationTests`, `ProjectRoleServiceAuthorizationTests` (unit) |
| ☑ | Customer không thấy wiki nội bộ | `tests/client/project-roles.spec.ts` · `NotificationAndWikiPolicyTests` |
| ☑ | Không rò rỉ dữ liệu chéo tenant | `DashboardTenantIsolationIntegrationTests` |
| ☑ | Moderator hết hạn / bị thu hồi mất quyền ngay | `OrganizationUsersAuthorizationTests` (integration) |
| ☑ | Simulation Mode gửi đúng `X-Simulate-User-Id`, thoát sạch trạng thái | `tests/client/use-permissions.spec.ts` |
| ☐ | Đổi vai trò thành viên trên UI (chỉ Manager/Owner/ScrumMaster làm được) | Cần kiểm tay |
| ☐ | Owner không bị gỡ khỏi dự án trên UI | Cần kiểm tay |

### 3.3 Dự án & Nhiệm vụ

| | Hạng mục | Bằng chứng |
| --- | --- | --- |
| ☑ | **Số "quá hạn" Dashboard khớp trang Nhiệm vụ, task `Cancelled` không bị tính** | `tests/client/formatters.spec.ts` (QALY-UI-01) |
| ☑ | Concurrency: 2 người sửa cùng task → 409 | `TaskConcurrencyTests` |
| ☑ | Gantt hiển thị ngày lấy từ database | `qaly.smoke.spec.ts` |
| ☑ | Tìm task và mở chi tiết, hiển thị đủ trạng thái/ưu tiên/người phụ trách/hạn/bình luận | `demo-script-30-minutes.spec.ts` |
| ☐ | Tạo/sửa/lưu trữ/khôi phục dự án | Cần kiểm tay |
| ☐ | Kanban kéo thả đúng luồng chuyển trạng thái | Cần kiểm tay |
| ☐ | Cột `OnHold` ẩn khi dự án tắt `enableOnHold` | Cần kiểm tay |
| ☐ | Lọc task theo trạng thái / người phụ trách / ưu tiên / quá hạn | Cần kiểm tay |
| ☐ | Đính kèm, evidence, lịch sử task | Cần kiểm tay |

### 3.4 Lộ trình & Sprint

| | Hạng mục | Bằng chứng |
| --- | --- | --- |
| ☑ | Tab Lộ trình hiển thị milestone và panel chi tiết | `roadmap-ui.spec.ts` |
| ☑ | Modal khởi tạo mẫu: Outsource / Scrum-Agile / Waterfall | `roadmap-ui.spec.ts` |
| ☑ | Giao diện sáng, dùng Bootstrap Icons, không tải font ngoài, không emoji trong nút | `roadmap-ui.spec.ts` |
| ☐ | Sprint: tạo, gán task, đóng sprint, báo cáo tiến độ | Cần kiểm tay |

### 3.5 Trợ lý AI & AI-native

| | Hạng mục | Bằng chứng |
| --- | --- | --- |
| ☑ | Mở Trợ lý AI, gửi yêu cầu, nhận phản hồi có căn cứ | `ai-assistant-workspace.spec.ts` |
| ☑ | Khối "Các bước đã thực hiện" và "Chi tiết lập kế hoạch" mở/đóng được, giữ nội dung sau reload | `ai-assistant-goal-planner.spec.ts` · `ai-assistant-workspace.spec.ts` |
| ☑ | Action Composer: tiến trình thật → bản nháp sửa được → xác nhận → biên nhận | `ai-action-composer.spec.ts` |
| ☑ | **Không thay đổi dữ liệu trước khi người dùng xác nhận** | `ai-action-composer.spec.ts` (TEST-UA-E2E) · `ai-native-next-candidates.spec.ts` |
| ☑ | Provider/model thực tế hiển thị đúng; hint model công bố định tuyến đúng provider | `AiGatewayRouterTests` (QALY-BE-01) · `ai-native-grounded-surfaces.spec.ts` |
| ☑ | Task draft từ tin nhắn nhóm giữ đúng nguồn sau reload | `ai-native-next-candidates.spec.ts` |
| ☑ | Ngân sách/chi phí AI chặn khi vượt hạn mức | `AiCostServiceTests` · `ai-usage-budget.spec.ts` |
| ☑ | Privacy: dữ liệu nhạy cảm không gửi cloud khi thiếu consent | `PrivacyComplianceTests` · `MeetingPrivacyEnforcementTests` · `privacy-retention.spec.ts` |
| ☐ | Kiểm thử tay với API key DeepSeek thật (luồng live) | Cần key + ngân sách |

### 3.6 Nhóm, Họp & Wiki

| | Hạng mục | Bằng chứng |
| --- | --- | --- |
| ☑ | Chat nhóm realtime đồng bộ giữa 2 phiên (SignalR) | `qaly.smoke.spec.ts` — *chat message received in a second context* |
| ☑ | Trang họp render được ở 2 phiên đã đăng nhập | `qaly.smoke.spec.ts` |
| ☑ | Bình chọn trong nhóm | `qaly.smoke.spec.ts` |
| ☑ | Wiki: xem trước và import tài liệu | `qaly.smoke.spec.ts` |
| ☑ | Trang chi tiết nhóm không trắng màn hình | `browser-console.spec.ts` |
| ☐ | Import biên bản họp → sinh checknote (kiểm tay) | Cần kiểm tay thủ công. Các bước: mở một cuộc họp có ≥ 1 đoạn transcript → vào tab "Checknote" trong `GroupMeetingPage` → bấm "Tạo biên bản AI" → xác nhận job trả về tóm tắt/quyết định/rủi ro/action items có trích dẫn nguồn, và sau khi reload trang vẫn đọc lại được kết quả đã lưu (`GET /api/meetings/{id}/auto-checknote`). Cần server + AI provider thật, chưa thực hiện được trong lần rà soát 2026-08-31 |

> **Ghi chú 2026-08-31**: đã thử chạy lại `qaly.smoke.spec.ts` (4 test realtime chat/meeting/poll/wiki) để re-verify sau đợt sửa dark-theme/emoji/nhãn nút, nhưng môi trường rà soát này không có Docker/DB chạy sẵn nên `dotnet run` (webServer của Playwright) không lên được — lệnh bị treo chờ health-check và phải hủy. Chưa re-run được E2E ở đây; đề nghị người có môi trường đủ Docker/DB chạy `npx playwright test tests/e2e/qaly.smoke.spec.ts --workers=1` để xác nhận trước khi bàn giao.

### 3.7 Tích hợp GitHub

| | Hạng mục | Bằng chứng |
| --- | --- | --- |
| ☑ | Trạng thái tích hợp đúng theo từng `state` | `GitHubInstallationStatusTests` |
| ☑ | Kết nối/ngắt repository | `GitHubRepositoryConnectionServiceTests` |
| ☑ | Commit, PR, workflow run liên kết đúng task | `GitHubProjectManagementServiceTests` |
| ☑ | Webhook nhận và xử lý sự kiện; secret không bị lộ | `GitHubWebhookIntegrationTests` · `WebhookEndpointPolicyTests` · `qaly.smoke.spec.ts` |
| ☐ | Kiểm thử tay với GitHub App thật | Cần cấu hình App |

### 3.8 Giao diện & Trải nghiệm

| | Hạng mục | Bằng chứng |
| --- | --- | --- |
| ☑ | **Chế độ tối: không còn khối nền sáng lọt** — 12 route + mobile + overlay + chi tiết dự án + họp/poll | `dark-theme.spec.ts` — 6/6 pass |
| ☑ | Theme giữ nguyên sau reload, đồng bộ giữa các tab | `dark-theme.spec.ts` · `tests/client/use-theme.spec.ts` |
| ☑ | **Responsive 1440 / 768 / 390px — không tràn ngang** trên 11 trang nhóm phụ trách | `responsive-audit.spec.ts` — 4/4 pass |
| ☑ | Trợ lý AI toàn màn hình trên mobile, không có nút resize | `ai-assistant-workspace.spec.ts` |
| ☑ | Toast: tối đa 5, tự đóng, đóng thủ công được, lỗi dùng `role="alert"` | `tests/client/use-toast.spec.ts` · `tests/client/components.spec.ts` |
| ☑ | Trạng thái rỗng/đang tải/lỗi có thông điệp tiếng Việt rõ ràng | `tests/client/components.spec.ts` · `qaly.smoke.spec.ts` — *honest empty state* |
| ☑ | **Console trình duyệt sạch trên 12 route chính** | `browser-console.spec.ts` — 3/3 pass |
| ⚠ | Bàn phím: phần tử bấm được phải tab tới và kích hoạt được | `TaskItem` đã đạt (`components.spec.ts`). **Còn ~22 chỗ khác chưa rà** — QB-5 |
| ☐ | Nút icon có `aria-label` / `title` — ~46 nút trong 7 file | CK-3, VM-4, GL-5 |
| ☐ | Bỏ emoji khỏi nút và nhãn — ~44 chỗ | CK-2, VM-3, GL-4 |

## 4. Kết quả E2E & lỗi đã xử lý

Lần chạy đầy đủ trước đó ghi nhận **7 spec fail**. Sau khi phân tích evidence
(`test-results/**/error-context.md`, screenshot, trace), các lỗi được phân loại và xử lý như sau.

### 4.1 Lỗi sản phẩm (đã sửa mã nguồn)

| ID | Mô tả | Nguyên nhân gốc | Sửa tại |
| --- | --- | --- | --- |
| **QALY-UI-01** | Số task "quá hạn" trên Dashboard lệch với trang Nhiệm vụ | Client chỉ loại `Done`, server loại cả `Cancelled` (`OpenStatuses` = Todo/InProgress/InReview/OnHold). Task đã huỷ quá hạn bị đếm ở client mà không đếm ở server | `src/Qaly.Web/ClientApp/utils/formatters.ts` |
| **QALY-UI-02** | Dòng nhiệm vụ không dùng được bằng bàn phím | `<article>` gắn `@click` nhưng không có `role`, `tabindex`, hay handler Enter/Space | `src/Qaly.Web/ClientApp/components/TaskItem.vue` |
| **QALY-UI-03** | Mở trang chi tiết dự án ném `TypeError: Cannot read properties of null (reading 'id')` | Nhánh `v-else` của template cũng render khi dự án **đang tải** (`isLoading = true`, `selectedProject = null`). Ba tab `stats`, `roadmap`, `members` chỉ gác theo id tab mà không gác `selectedProject`, trong khi các tab còn lại đã dùng `canShow*`. Tab mặc định là `stats` nên lỗi phát sinh ở **mọi lần mở dự án** | `src/Qaly.Web/ClientApp/pages/ProjectDetailPage.vue` |
| **QALY-BE-01** | Hint model `deepseek-v4-pro` định tuyến nhầm sang Ollama | `NormalizeProviderName` thiếu ánh xạ cho ID model mà tài liệu v4.0 công bố; hint lạ rơi vào nhánh mặc định, `GetProviderSetting` trả về `settings.Ollama`. Với `StrictProvider = true`, yêu cầu cloud bị chạy trên model local | `src/Qaly.Infrastructure/Services/AI/AiGateway.cs` |

Test chặn hồi quy tương ứng:

- QALY-UI-01 → `tests/client/formatters.spec.ts`
- QALY-UI-02 → `tests/client/components.spec.ts`
- QALY-UI-03 → cổng "không có lỗi trình duyệt" trong `tests/e2e/demo-script-30-minutes.spec.ts`
  (chính cổng này phát hiện ra lỗi sau khi các assert lỗi thời phía trên được sửa)
- QALY-BE-01 → `tests/Qaly.UnitTests/AiGatewayRouterTests.cs`
  (`ExecuteAsync_WithPublishedDeepSeekModelId_*` — đã kiểm chứng **fail 3/5 case khi gỡ bản vá**,
  tức là test bắt đúng lỗi chứ không chỉ chạy cho có)

### 4.2 Lỗi kiểm thử (test sai, sản phẩm đúng)

| ID | Spec | Vấn đề | Cách sửa |
| --- | --- | --- | --- |
| QALY-T-01 | `roadmap-ui.spec.ts` | Tìm dự án seed trên trang `/projects` rồi **fallback `.first()`**. Trang này phân trang theo thời gian, nên dự án do spec khác tạo song song đẩy dự án seed ra khỏi trang 1 → test âm thầm kiểm tra nhầm một dự án rỗng | Thêm `tests/e2e/support/seeded-project.ts`, phân giải project id qua API rồi vào thẳng `/projects/{id}` |
| QALY-T-02 | `demo-script-30-minutes.spec.ts` | Assert 5–8 thành viên, nhưng seed **cố ý** tạo 10 (mỗi vai trò một tài khoản để demo ma trận quyền) | Đổi sang assert đủ 9 vai trò dự án thay vì đếm đầu người |
| QALY-T-03 | `ai-assistant-goal-planner.spec.ts` | Assert khối kế hoạch hiển thị ngay, nhưng nó nằm trong `<details>` thu gọn. Commit thêm `<details>` (`7a166e79`) **mới hơn** commit viết assert (`a6cbe9ab`) → test lỗi thời | Mở disclosure như người dùng thật, đổi selector anh-em liền kề `+` sang anh-em chung `~` |
| QALY-T-04 | `ai-assistant-workspace.spec.ts` | Tương tự: assert nhãn tiến trình khi khối `.assistant-process-disclosure` còn đóng | Click `summary` trước khi assert |
| QALY-T-05 | `ai-native-grounded-surfaces.spec.ts`<br>`ai-native-next-candidates.spec.ts` | Assert `providerHint === 'deepseek-v4-pro'`, nhưng UI thật gửi `'deepseek-chat'` (ID provider chuẩn; nhãn hiển thị mới là "DeepSeek V4 Pro"). Assert này ném lỗi **bên trong route handler** nên request không bao giờ được fulfill → kéo theo lỗi "không tìm thấy testid" | Sửa assert về `'deepseek-chat'` |
| QALY-T-06 | `ai-action-composer.spec.ts` | Test mở Trợ lý AI từ `/dashboard`. Trợ lý luôn khởi tạo ở phạm vi `workspace` (`selectedTarget = 'workspace'`), nên `context.projectId` là `null`. Mock lại **dội đúng giá trị null đó** vào `artifact.projectId`, khiến `auto-start` của composer = false → `/api/ai/actions/compose` **không bao giờ được gọi** → không có chip tiến trình. Trace xác nhận: chỉ có 6 lời gọi `/api/ai/*`, không có `actions/compose` | Phân giải một dự án seed thật rồi gắn vào artifact; thêm assert `compose` nhận đúng `projectId` |
| QALY-T-07 | `demo-script-30-minutes.spec.ts` | Assert `.member-role-actions` chứa `/Manager\|Member\|Owner\|Admin/i`, nhưng control vai trò render nhãn tiếng Việt ("Quản lý dự án", "Chủ dự án"…). Assert này bị che khuất trước đó vì test đã fail sớm hơn ở bước đếm thành viên | Đổi sang khớp nhãn tiếng Việt |

### 4.3 Kết quả sau khi sửa

| Chế độ chạy | Trước | Sau |
| --- | --- | --- |
| 7 spec mục tiêu, `--workers=1` | 7 fail | **7 pass** |
| Toàn bộ suite, 4 worker (mặc định) | 55 pass / 11 fail / 5 không chạy | **61 pass / 6 fail / 4 không chạy** |

### 4.4 Bất ổn khi chạy song song (chưa đóng hoàn toàn)

Suite chạy 4 worker trên **một** dev server dùng chung. Khi máy bận, panel Trợ lý AI vẫn đang
re-render/auto-scroll nên Playwright báo `element is not stable` hoặc
`element was detached from the DOM`, và các journey dài chạm trần timeout 30 giây mặc định.

Đã xử lý trong phạm vi này:

- `tests/e2e/support/assistant-disclosures.ts` — mở `<details>` bằng chính activation behavior của
  `<summary>` thay vì chờ actionability, nên không phụ thuộc vào việc panel đứng yên.
- `ai-action-composer.spec.ts` — nâng timeout lên 90 giây cho journey dài
  (spec live-worker cạnh bên vốn đã tự nâng timeout vì cùng lý do).

**Đã phân loại xong 6 spec fail của lượt 4 worker**: chạy lại cả 6 với `--workers=1` cho
**18/18 pass**. Kết luận: đây là **bất ổn do chạy song song, không phải lỗi chức năng**. Bao gồm cả
`ai-native-bounded-loop-progressive-launch`, `ai-project-launch-orchestration`, `ai-research-plan`
và `qaly.smoke` (chat realtime giữa 2 context) — các spec không bị sửa gì trong đợt này.

Khuyến nghị vận hành: khi cần kết quả tin cậy để nghiệm thu, chạy `--workers=1`, hoặc đặt
`E2E_WORKERS=2`. CI đã bật `retries: 1`.

---

## 5. Rủi ro & phần chưa phủ

| Rủi ro | Mức | Ghi chú |
| --- | --- | --- |
| E2E phụ thuộc dữ liệu seed dùng chung, chạy song song dễ nhiễu | Cao | Đã giảm bằng `seeded-project.ts`; các spec tạo dự án mới nên dọn dẹp sau khi chạy |
| `canAccessModule` **fail-open** khi thiếu quyền trong danh sách | Trung bình | Client chỉ là lớp hiển thị, server vẫn chặn. Cần giữ nguyên nguyên tắc "server là biên enforcement" |
| Chưa có unit test cho `use-task-actions`, `use-project-actions` | Trung bình | Các composable này gọi API và có nhánh lỗi; nên bổ sung tiếp. `use-meeting-recovery` đã có unit test từ 2026-08-31 (`tests/client/use-meeting-recovery.spec.ts`, 12 test) |
| Chưa có kiểm thử tải/hiệu năng | Trung bình | Chưa nằm trong phạm vi bàn giao hiện tại |
| `playwright-report/` bị commit vào git | Thấp | `.gitignore` đã bỏ qua `test-results/`, `TestResults/` nhưng **chưa có** `playwright-report/`, nên mỗi lần chạy E2E lại tạo hàng chục file thay đổi trong `git status`. Đề xuất: thêm `playwright-report/` vào `.gitignore` rồi `git rm -r --cached playwright-report` (chưa thực hiện vì đụng vào git index) |
| Provider AI thật (DeepSeek) cần API key hợp lệ | Cao | Test dùng fixture/mock; luồng live chỉ chạy được khi có key và ngân sách |

---

## 6. Tiêu chí nghiệm thu trước bàn giao

Tất cả phải đạt trong **cùng một lần chạy** trên nhánh phát hành:

1. ☐ `dotnet test Qaly_project.slnx -c Release` — 0 fail
2. ☐ `npm run test:unit` — 0 fail
3. ☐ `npm run typecheck` — 0 lỗi
4. ☐ `npm run build` — thành công, và `git diff --exit-code -- src/Qaly.Web/wwwroot/dist` sạch
5. ☐ `npm run test:e2e` — 0 fail
6. ☐ Coverage unit ≥ 43% line, integration ≥ 16% line
7. ☐ Console trình duyệt sạch trên các luồng chính
8. ☐ Checklist mục 3 được đánh dấu hết, mọi mục ⚠/✗ có ghi chú lý do
9. ☐ `QA_LOG.md` được cập nhật cho commit phát hành

---

## 7. Quy trình chạy kiểm thử đầy đủ

```bash
# 1. Backend — toàn bộ
dotnet test Qaly_project.slnx -c Release

# 2. Frontend — unit + kiểu + bundle
npm run test:unit
npm run typecheck
npm run build

# 3. E2E — cần server; Playwright tự khởi động nếu chưa có
npm run test:e2e

# Chạy tuần tự khi cần gỡ lỗi (tránh nhiễu do song song)
npx playwright test --workers=1 --reporter=list

# Chạy đúng một spec
npx playwright test tests/e2e/roadmap-ui.spec.ts --workers=1
```

Nếu Playwright báo thiếu trình duyệt (thường sau khi cập nhật dependency):

```bash
npx playwright install chromium
```

Evidence khi fail nằm ở `test-results/<tên-test>/`: `error-context.md` (ảnh chụp cây accessibility),
`test-failed-1.png`, `video.webm`, `trace.zip` (mở bằng `npx playwright show-trace`).
