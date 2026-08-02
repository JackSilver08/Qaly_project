# RC Manual Test Checklist

## Mục tiêu

Bảo đảm các luồng P0/P1 có bằng chứng kiểm thử dù E2E automation chưa chạy được trên môi trường local.

| ID     | Luồng           | Priority | Kết quả mong đợi                                            | Trạng thái                      | Evidence                          |
| ------ | --------------- | -------- | ----------------------------------------------------------- | ------------------------------- | --------------------------------- |
| MT-001 | Đăng nhập       | P0       | Người dùng đăng nhập thành công và thấy shell               | Manual / Blocked until app runs | Cần chạy app local hoặc CI        |
| MT-002 | Chọn tổ chức    | P0       | Người dùng nhìn thấy tổ chức được phép và mở đúng workspace | Manual / Blocked until app runs | Cần chạy app local hoặc CI        |
| MT-003 | Tạo dự án       | P0       | Dự án được tạo đúng và hiển thị trong danh sách             | Manual / Blocked until app runs | Cần chạy app local hoặc CI        |
| MT-004 | Thêm thành viên | P0       | Thành viên được thêm và có quyền đúng                       | Manual / Blocked until app runs | Cần chạy app local hoặc CI        |
| MT-005 | Giao nhiệm vụ   | P1       | Nhiệm vụ được tạo và gán đúng người                         | Manual / Blocked until app runs | Cần chạy app local hoặc CI        |
| MT-006 | Xem AI          | P1       | AI panel hoặc analytics hiển thị kết quả hợp lệ             | Manual / Blocked until app runs | Cần chạy app local hoặc CI        |
| MT-007 | Thu hồi quyền   | P0       | Quyền bị thu hồi và truy cập bị chặn                        | Automated + Manual              | Integration test và manual retest |
