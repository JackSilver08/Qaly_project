# Review & Kết quả kiểm thử cho QALY Project

Tài liệu này được tạo dựa trên review file `test-cases-full.md` và bổ sung phần trạng thái kiểm thử hiện tại.

## 1. Review tổng quan

- File `test-cases-full.md` đã có cấu trúc tốt, phân nhóm theo module rõ ràng.
- Các test case đã bao phủ các luồng chính của project: Meeting, Import document, Deploy config, AI analytics, Group AI, Auth/permission và Regression core.
- Các cột `Điều kiện chuẩn bị`, `Các bước thực hiện` và `Kết quả mong đợi` đầy đủ, dễ đọc.
- Đề xuất: thêm cột `Trạng thái` và `Ghi chú` để theo dõi kết quả sau khi chạy thực tế.

## 2. Kết quả kiểm thử hiện tại

> Lưu ý: các case hiện tại chưa được thực thi từng trường hợp thủ công. Trạng thái dưới đây là tạm thời, nhưng đã cập nhật kết quả chạy tự động.

## 2.1 Kết quả chạy tự động

- Lệnh đã chạy: `dotnet test Qaly_project.slnx --no-restore`
- Kết quả: `246` test tự động đã thực thi, `246` passed, `0` failed, `0` skipped.
- Thời gian chạy: `103.1s`.
- Quan sát môi trường:
    - Có nhiều cảnh báo cấu hình EF Core global query filter.
    - Có lỗi seed SQL Server relational provider và lỗi kết nối AI provider (Ollama localhost:11434) trong log, cho thấy môi trường hiện chưa cấu hình đầy đủ SQL/AI provider.
    - Các lỗi này không khiến test suite fail, nhưng cần khắc phục nếu muốn chạy nghiệm thu toàn bộ hệ thống.

## 2.2 Kết quả kiểm thử hiện tại

| Mã test case  | Module          | Trạng thái | Ghi chú                                  |
| ------------- | --------------- | ---------- | ---------------------------------------- |
| DH01-MTG-001  | Meeting         | Chưa chạy  | Chờ thực thi môi trường Meeting/SignalR. |
| DH01-MTG-002  | Meeting         | Chưa chạy  | Chờ thực thi với outside user.           |
| DH01-MTG-003  | Meeting         | Chưa chạy  | Chờ test import Meetily/action items.    |
| DH01-MTG-004  | Meeting         | Chưa chạy  | Cần 2 browser/tab và Redis/SignalR.      |
| DH01-MTG-005  | Meeting         | Chưa chạy  | Kiểm tra browser share screen.           |
| DH01-IMP-001  | Import document | Chưa chạy  | Test import DOCX.                        |
| DH01-IMP-002  | Import document | Chưa chạy  | Test ZIP import đa định dạng.            |
| DH01-IMP-003  | Import document | Chưa chạy  | Test PDF parser.                         |
| DH01-IMP-004  | Import document | Chưa chạy  | Test CSV/XLSX task import.               |
| DH01-IMP-005  | Import document | Chưa chạy  | Test undo import session.                |
| DH01-IMP-006  | Import document | Chưa chạy  | Test file edge case.                     |
| DH01-DEP-001  | Deploy config   | Chưa chạy  | Kiểm tra Docker config.                  |
| DH01-DEP-002  | Deploy config   | Chưa chạy  | Kiểm tra app với DB/Redis seed.          |
| DH01-DEP-003  | Deploy config   | Chưa chạy  | Test fallback AI provider.               |
| DH01-DEP-004  | Deploy config   | Chưa chạy  | Review production config.                |
| DH01-DEP-005  | Deploy config   | Chưa chạy  | Kiểm tra port conflict/log.              |
| DH01-AIAN-001 | AI analytics    | Chưa chạy  | Test dashboard analytics.                |
| DH01-AIAN-002 | AI analytics    | Chưa chạy  | Test AI summary/risk/insight.            |
| DH01-AIAN-003 | AI analytics    | Chưa chạy  | Test permission filter.                  |
| DH01-AIAN-004 | AI analytics    | Chưa chạy  | Test AI chat/stream flow.                |
| DH01-AIAN-005 | AI analytics    | Chưa chạy  | Test prompt edge cases.                  |
| DH01-GAI-001  | Group AI        | Chưa chạy  | Test group summary.                      |
| DH01-GAI-002  | Group AI        | Chưa chạy  | Test draft project/task từ thảo luận.    |
| DH01-GAI-003  | Group AI        | Chưa chạy  | Test quyền truy cập Group AI.            |
| DH01-GAI-004  | Group AI        | Chưa chạy  | Test trích xuất action items.            |
| DH01-GAI-005  | Group AI        | Chưa chạy  | Test edge case chat ít dữ liệu.          |
| DH01-AUTH-001 | Auth/permission | Chưa chạy  | Test login/session.                      |
| DH01-AUTH-002 | Auth/permission | Chưa chạy  | Test admin API block.                    |
| DH01-AUTH-003 | Auth/permission | Chưa chạy  | Test project permission outside user.    |
| DH01-AUTH-004 | Auth/permission | Chưa chạy  | Test group role permission.              |
| DH01-AUTH-005 | Auth/permission | Chưa chạy  | Test password change/revoke session.     |
| DH01-AUTH-006 | Auth/permission | Chưa chạy  | Test login invalid/inactive.             |
| DH01-REG-001  | Regression core | Chưa chạy  | Chạy unit/integration hiện có.           |
| DH01-REG-002  | Regression core | Chưa chạy  | Chạy Playwright smoke.                   |
| DH01-REG-003  | Regression core | Chưa chạy  | Test CRUD core.                          |
| DH01-REG-004  | Regression core | Chưa chạy  | Test audit/log.                          |
| DH01-REG-005  | Regression core | Chưa chạy  | Test UI responsive/demo.                 |

## 3. Gợi ý lấy kết quả thực tế

- Chạy từng case theo module trong môi trường dev/test hiện tại.
- Ghi lại `Passed` / `Failed` / `Blocked` / `N/A` vào cột `Trạng thái` sau khi thực thi.
- Ghi `Ghi chú` nếu cần thông tin thêm về lỗi, môi trường, hay blocked.
- Nếu cần, tôi có thể giúp chuyển bảng này thành file báo cáo kết quả kiểm thử chi tiết hơn.
