# BÁO CÁO KẾT QUẢ KIỂM THỬ MÔ-ĐUN PROJECTS (QALY)
**Ngày:** 13/05/2026
**Người báo cáo:** Haken

---

## I. TỔNG QUAN
Đã tiến hành kiểm thử chức năng cho mô-đun `Projects` (bao gồm `ProjectService` và `ProjectsController`). 
- **Kết quả:** Đạt yêu cầu. Các luồng nghiệp vụ chính (CRUD, RBAC, Bảo mật truy cập) hoạt động ổn định.
- **Tình trạng test:** Đã bổ sung 25 Unit Tests và 4 Integration Tests. Tất cả đã thông qua (Passed).

## II. CÁC VẤN ĐỀ CẦN DEV ĐIỀU CHỈNH/LƯU Ý

### 1. Vấn đề lỗi Build (File Locked)
Hiện tại việc chạy test thường xuyên bị lỗi `MSB3026: Could not copy... because it is being used by another process` do tiến trình `Qaly.Web.exe` không tự động giải phóng.
- **Đề xuất:** Xem xét lại cấu hình `dotnet test` hoặc cơ chế tự động dừng web host sau khi chạy integration test để tránh khóa file DLL.

### 2. Cải thiện Code (Code Maintenance)
Dựa trên kết quả chạy code analysis trong quá trình test:
- **`ProjectServiceTests.cs`:** 
  - Đã xuất hiện cảnh báo `CA1816: Change ProjectServiceTests.Dispose() to call GC.SuppressFinalize(object)`. Vui lòng cập nhật lại lớp test này để tuân thủ chuẩn Disposable.
- **`CustomExceptionHandler.cs` & `Program.cs`:** 
  - Có cảnh báo `CA1848: Use LoggerMessage delegates` (thay vì gọi extension `LogError` trực tiếp). Việc này giúp cải thiện hiệu năng logging đáng kể cho hệ thống.
  - Có cảnh báo `CA1847: Use 'string.Contains(char)'` thay vì string ở `Program.cs`.

### 3. Vấn đề Logic (Security/RBAC)
- **Tình trạng:** Hiện tại phân quyền đã được test ổn định thông qua `ProjectRoleRules`.
- **Lưu ý:** Vui lòng kiểm tra lại sự đồng nhất trong cách đặt tên prefix cho Redis Key (`Qaly_`). Hiện tại tôi đã đồng bộ prefix trong test và service, nhưng cần đảm bảo mọi service khác trong hệ thống cũng tuân thủ đúng prefix này để tránh rò rỉ session hoặc lỗi không xóa được key.

## III. DỮ LIỆU KIỂM THỬ ĐÃ THIẾT LẬP
Tôi đã tạo file `tests/Qaly.IntegrationTests/IntegrationTestFactory.cs` để hỗ trợ môi trường `InMemoryDatabase`. Khi cần sửa lỗi trên SQL Server thật, hãy kiểm tra kỹ `UseInMemoryDatabase` trong `DependencyInjection.cs`.

---
*Tài liệu chi tiết các ca kiểm thử đã được lưu tại `tests/Qaly.UnitTests/ProjectServiceTests.cs` và `tests/Qaly.IntegrationTests/ProjectsControllerTests.cs`.*
