# Kiểm tra trực quan kịch bản demo bằng Playwright

Test `tests/e2e/demo-script-30-minutes.spec.ts` đi qua đúng hành trình trình diễn: Dashboard, dự án mẫu, thành viên, task, roadmap, GitHub và hai bước AI. Mỗi mốc tạo ảnh chụp viewport 1440×900, kiểm tra tràn ngang, ảnh hỏng, lỗi JavaScript và HTTP 4xx/5xx.

## Chạy với database thật trên máy

Đây là lệnh nên dùng để duyệt trực quan trước buổi demo:

```powershell
npm run test:e2e:demo:database:headed
```

Runner tự đọc `ConnectionStrings:DefaultConnection`, truy vấn `QalyDB` để chọn tài khoản Admin đang hoạt động và project có mã `qaly-workos-demo`, sau đó đối chiếu password hash với seed credential local. Không cần tự đặt `E2E_BASE_URL`, email, mật khẩu hay tên project; mật khẩu cũng không được in ra terminal. Qaly được mở riêng tại `http://127.0.0.1:5097` trong thời gian test và Playwright tự dừng server khi hoàn tất. Với dữ liệu dự án nhạy cảm, runner dùng router `auto`, giữ dữ liệu tại Ollama local và mặc định chọn model nhẹ `llama3.2:1b` để phù hợp máy demo chạy CPU.

Chạy headless nhưng vẫn lưu đủ ảnh, video và trace khi lỗi:

```powershell
npm run test:e2e:demo:database
```

## Chạy nhanh trên local

```powershell
# Xóa các biến môi trường cũ nếu trước đó đã thử lệnh staging.
Remove-Item Env:E2E_BASE_URL,Env:E2E_ADMIN_EMAIL,Env:E2E_ADMIN_PASSWORD -ErrorAction SilentlyContinue
npm run test:e2e:demo:local
```

Lệnh này tự khởi động Qaly bằng database in-memory, phù hợp để kiểm tra selector và luồng chức năng.

Muốn nhìn trình duyệt thao tác trực tiếp trên local:

```powershell
npm run test:e2e:demo:local:headed
```

## Chạy trên môi trường demo/staging thật

```powershell
$env:E2E_BASE_URL = 'https://staging.ten-mien-cua-ban.vn'
$env:E2E_ADMIN_EMAIL = 'email-that-cua-tai-khoan-demo'
$env:E2E_ADMIN_PASSWORD = 'mat-khau-that-cua-tai-khoan-demo'
$env:E2E_DEMO_PROJECT = 'Qaly Release 4.0'
npm run test:e2e:demo:real
```

Các URL, email và mật khẩu trong ví dụ là placeholder: phải thay bằng giá trị đang hoạt động trên staging/production, không copy nguyên văn.

Không ghi credentials vào repository. Cấu hình `playwright.demo.config.ts` cố ý không tự dựng server và sẽ dừng ngay nếu thiếu `E2E_BASE_URL`, bảo đảm kết quả phản ánh đúng môi trường được chỉ định.

Để nhìn Playwright thao tác như khi thuyết trình:

```powershell
$env:E2E_DEMO_SLOW_MO = '250'
npm run test:e2e:demo:headed
```

Mở báo cáo sau khi chạy:

```powershell
npx playwright show-report playwright-report/demo
```

Báo cáo chứa tám ảnh checkpoint và video toàn bộ hành trình. Trace được giữ lại khi test lỗi. Kịch bản tạo phiên/lượt hội thoại cho hai prompt AI trong database, nhưng không bấm nút lưu Launch Brief nên không tạo dự án mới.
