# Kịch bản Test Case E2E cơ bản bằng Playwright

- Dự án: Qaly
- Phạm vi: smoke E2E cơ bản cho server local `http://127.0.0.1:5000`
- Môi trường: `ASPNETCORE_ENVIRONMENT=Development`, `UseInMemoryDatabase=true`
- Tài khoản seed mặc định: `admin@qaly.dev` / `Admin@123456`

## Bộ test: E2E Basic Seed & Navigation

| ID | Tên kịch bản | Các bước thực hiện | Kết quả mong đợi | Ưu tiên |
|---|---|---|---|---|
| TC-E2E-001 | Public login page renders correctly | 1. Mở `/Account/Login`.<br>2. Kiểm tra form đăng nhập.<br>3. Kiểm tra ô Email, Password và nút đăng nhập. | Trang Login hiển thị đầy đủ, không bị trắng trang, có đủ input và nút đăng nhập. | P0 |
| TC-E2E-002 | Seeded admin can login | 1. Mở `/Account/Login`.<br>2. Nhập `admin@qaly.dev` / `Admin@123456`.<br>3. Bấm đăng nhập. | Người dùng được chuyển khỏi trang Login, shell/header sau đăng nhập hiển thị, menu user xuất hiện. | P0 |
| TC-E2E-003 | Dashboard renders seeded demo data | 1. Đăng nhập bằng admin seed.<br>2. Mở `/dashboard`.<br>3. Kiểm tra nội dung dashboard. | Dashboard hiển thị shell/header và có dữ liệu hoặc nhãn nghiệp vụ từ seed demo như Qaly, Dự án, Nhiệm vụ. | P0 |
| TC-E2E-004 | Primary authenticated navigation routes render | 1. Đăng nhập bằng admin seed.<br>2. Điều hướng lần lượt `/dashboard`, `/projects`, `/tasks`, `/teams`, `/analytics`.<br>3. Kiểm tra URL và shell/header. | Mỗi route chính mở được, URL đúng, layout authenticated không bị crash. | P0 |
| TC-E2E-005 | Projects page shows seeded project or safe empty state | 1. Đăng nhập bằng admin seed.<br>2. Mở `/projects`.<br>3. Kiểm tra nội dung trang dự án. | Trang Projects hiển thị dữ liệu seed hoặc empty state an toàn; không trắng trang, không crash. | P1 |

## Quy tắc phân loại kết quả

| Trạng thái | Ý nghĩa |
|---|---|
| Pass | Kịch bản chạy đúng theo kết quả mong đợi. |
| Fail | Server sẵn sàng nhưng hành vi UI/API sai so với kỳ vọng; cần phân tích như lỗi sản phẩm hoặc test drift. |
| Blocked | Không thể test do hạ tầng: server không lên, không kết nối được cổng 5000, thiếu dependency, hoặc môi trường CI lỗi. Không ghi nhận là bug sản phẩm. |

## Evidence khi test fail

Playwright tự lưu evidence trong `test-results/` và `playwright-report/`:

- `test-failed.png`: ảnh màn hình tại thời điểm fail.
- `video.webm`: video tái hiện test fail.
- `trace.zip`: trace để mở bằng `npx playwright show-trace`.
- `e2e-results.json`: kết quả JSON khi chạy trong CI.
