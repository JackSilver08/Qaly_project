# Báo cáo kết quả Test Playwright (E2E) — Qaly

- **Ngày chạy:** 2026-06-24
- **Người chạy:** QA (tuantqtb01555@gmail.com)
- **Suite:** `tests/e2e/qaly.smoke.spec.ts` + `tests/e2e/browser-console.spec.ts`
- **Browser:** Chromium (Playwright v1223 / Chrome for Testing 148)
- **Tổng kết (lần đầu):** 7 PASS / 2 FAIL (9 test)
- **Tổng kết (sau khi fix):** ✅ **9 PASS / 0 FAIL** (9 test) — xem [§6 Khắc phục](#6-khắc-phục-2-test-fail)

## 1. Môi trường

| Thành phần | Cấu hình |
|---|---|
| App URL | `http://localhost:5000` (`dotnet run`, `ASPNETCORE_URLS=http://localhost:5000`, `ASPNETCORE_ENVIRONMENT=Development`) |
| SQL Server | container `qaly-sqlserver` `localhost,1434` (SA), DB `QalyDb` — override `ConnectionStrings__DefaultConnection` |
| Redis | `localhost:6380` |
| Tài khoản | `admin@qaly.dev` / `Admin@123456` (seed dev) qua `E2E_ADMIN_EMAIL` / `E2E_ADMIN_PASSWORD` |

> Ghi chú: connection string mặc định trong `appsettings.json` dùng `Server=localhost;Trusted_Connection=True` không khớp container DB nên phải override qua biến môi trường khi chạy.

**Lệnh chạy:**
```bash
export E2E_BASE_URL="http://localhost:5000"
export E2E_ADMIN_EMAIL="admin@qaly.dev"
export E2E_ADMIN_PASSWORD="Admin@123456"
npx playwright test --reporter=list
# Vì spec để mode "serial", các test sau bị skip khi 1 test fail.
# Đã chạy lại từng test độc lập bằng: npx playwright test -g "<title>"
```

## 2. Kết quả chi tiết (chạy độc lập từng test)

| # | Test | Tính năng | Kết quả | Thời gian |
|---|---|---|---|---|
| 1 | public authentication pages have no browser errors | Console sạch trang Login/Register | ✅ PASS | 10.4s |
| 2 | should login successfully | Đăng nhập + Đăng xuất | ✅ PASS | 12.4s |
| 3 | main authenticated routes have no browser errors | Console sạch các route chính sau đăng nhập | ✅ PASS | 20.1s |
| 4 | should create a group and open group page | Tạo nhóm (UI) | ✅ PASS¹ | 4.9s |
| 5 | should send a chat message and receive it in a second context | Chat realtime 2 context (SignalR) | ✅ PASS | 8.3s |
| 6 | should vote in a group poll | Tạo & bình chọn poll | ❌ FAIL | 30.7s (timeout) |
| 7 | should open analytics page | Trang Analytics/Erumi | ✅ PASS | 4.3s |
| 8 | should render meeting page in two authenticated contexts | Cuộc họp nhóm 2 context | ✅ PASS | 10.3s |
| 9 | should preview and import a Wiki document | Import tài liệu → Wiki | ❌ FAIL | 31.5s (timeout) |

¹ Test #4 **fail khi chạy song song** (full run) do tranh chấp realtime/state, nhưng **pass khi chạy độc lập** → flaky, cần làm ổn định.

## 3. Phân tích lỗi

### ❌ #6 — Vote in a group poll (test drift, không phải bug sản phẩm)
- **Lỗi:** timeout khi `click` nút `.group-poll-composer >> "Gửi poll"` (dòng 309).
- **Nguyên nhân:** UI poll đã thay đổi. Markup `.group-poll-composer`, `.group-poll-options` và nút **"Gửi poll"** không còn trong code hiện tại; `PollCard.vue` nay dùng nhãn **"Bình chọn nhóm"**. Selector trong smoke test đã lỗi thời.
- **Khuyến nghị:** cập nhật selector/nhãn theo UI mới; thêm `data-testid` cho composer & nút gửi để bền vững.

### ❌ #9 — Preview & import a Wiki document (test drift)
- **Lỗi:** timeout khi `setInputFiles` trên `.import-modal input[type="file"]` (dòng 406). Modal **có** hiển thị (assert `toBeVisible` ở dòng 405 pass) nhưng không tìm thấy file input ở bước hiện tại.
- **Nguyên nhân:** `input[type="file"]` vẫn tồn tại (`ImportUploadStep.vue:142`, ẩn) nhưng luồng `ImportModal` đã đổi (có bước/landing trước Upload, `detectImportMode`, danh sách import session…), nên input không nằm ở view đầu tiên.
- **Khuyến nghị:** điều hướng đúng tới bước Upload trước khi set file; thêm `data-testid="import-file-input"`.

## 4. Điểm tích cực
- Hạ tầng (DB container, Redis, seed dev) hoạt động; app khởi động & seed thành công.
- Các luồng cốt lõi **đăng nhập, tạo nhóm, chat realtime 2 context, cuộc họp 2 context, analytics** đều xanh.
- **Không có lỗi console** ở cả trang công khai lẫn các route đã đăng nhập.

## 6. Khắc phục 2 test fail

Sau khi điều tra sâu (dump DOM + bắt console), kết luận **khác với phán đoán ban đầu**: một trong hai là **bug sản phẩm thật**, không phải test drift.

### 🐞 #9 Import — BUG SẢN PHẨM (đã fix)
- **Triệu chứng:** mở modal import, bước Upload (`ImportUploadStep`) **không render** (Vue trả về `<!---->`), không có `input[type=file]`.
- **Nguyên nhân gốc:** `ImportUploadStep.vue` dùng `computed(...)` (dòng 27) nhưng **không import `computed`** từ `vue` (dòng 2 chỉ import `ref`). Console báo `ReferenceError: computed is not defined` → component crash khi render → **tính năng import tài liệu hỏng hoàn toàn trên UI**, không chỉ test.
- **Fix:** `src/Qaly.Web/ClientApp/components/import/ImportUploadStep.vue`
  ```diff
  - import { ref } from 'vue'
  + import { computed, ref } from 'vue'
  ```
- **Cập nhật test:** vì luồng import trước đây không bao giờ render nên các selector tiếng Anh trong test (`Continue`, `Preview Wiki page import`, `Create Wiki page`...) đã lỗi thời. Đã đổi sang nhãn tiếng Việt thực tế: `Tiếp tục` → `Xem trước trang Wiki sẽ nhập` → `Xác nhận nhập tài liệu` → `Tạo trang Wiki` → `Nhập dữ liệu hoàn tất`; assert tiêu đề wiki qua `input.import-input` (value) ở bước preview.
- **Phải `npm run build`** lại frontend để bản vá vào `wwwroot/dist`.

### 🔧 #6 Poll — TEST DRIFT (đã fix)
- **Nguyên nhân:** nút gửi đổi nhãn từ "Gửi poll" → **"Gửi bình chọn"** (`TeamsPage.vue:1675`). Composer và phần assert vote (`.poll-option`, "1 chọn", "1 người tham gia") vẫn khớp.
- **Fix:** `tests/e2e/qaly.smoke.spec.ts` — đổi `/Gửi poll/i` → `/Gửi bình chọn/i`.

### Kết quả sau fix (chạy lại độc lập từng test)
| Test | Trước | Sau |
|---|---|---|
| login / create group / chat / analytics / meeting | ✅ | ✅ |
| vote in a group poll | ❌ | ✅ |
| preview & import a Wiki document | ❌ | ✅ |
| browser-console (×2) | ✅ | ✅ |

→ **9/9 PASS.** Không đụng tới workflow nào khác (chỉ thêm 1 import thiếu + 1 nhãn nút trong test; build FE không đổi logic khác).

## 7. Việc cần làm tiếp
1. Sửa 2 test lỗi thời (poll, import) theo UI hiện tại + thêm `data-testid`.
2. Khắc phục flaky của test tạo nhóm khi chạy song song (chờ điều kiện rõ ràng thay vì URL race).
3. Cân nhắc bỏ `mode: "serial"` hoặc tách file để 1 fail không skip phần còn lại.
4. Thêm `webServer` vào `playwright.config.ts` để CI tự khởi động app.
5. Mở rộng coverage theo [playwright-test-plan-full.md](./playwright-test-plan-full.md) (17 suite).
