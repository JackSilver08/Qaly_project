# QA_LOG - Nhật ký kiểm thử E2E hằng ngày

## Hướng dẫn điền nhanh

- Mỗi dòng tương ứng một kịch bản test trên một commit.
- Nếu test không chạy được vì server/local/CI không sẵn sàng, ghi `Blocked` và mô tả lý do ở cột ghi chú.
- Nếu `Fail`, điền đường dẫn evidence trong `test-results/` hoặc artifact CI.

## Nhật ký theo commit

| Ngày | Mã Commit | Nhánh | Tên kịch bản test | Kết quả (Pass/Fail/Blocked) | Đường dẫn file Evidence nếu lỗi | Ghi chú |
|---|---|---|---|---|---|---|
| 2026-06-28 | `<commit-sha>` | main | TC-E2E-001 public login page renders correctly |  |  |  |
| 2026-06-28 | `<commit-sha>` | main | TC-E2E-002 seeded admin can login and see authenticated shell |  |  |  |
| 2026-06-28 | `<commit-sha>` | main | TC-E2E-003 dashboard renders seeded demo data |  |  |  |
| 2026-06-28 | `<commit-sha>` | main | TC-E2E-004 primary authenticated navigation routes render |  |  |  |
| 2026-06-28 | `<commit-sha>` | main | TC-E2E-005 projects page shows seeded project or empty-state safely |  |  |  |

## Tổng hợp trong ngày

| Ngày | Tổng số test | Pass | Fail | Blocked | Commit đã kiểm thử | Người thực hiện | Nhận xét |
|---|---:|---:|---:|---:|---|---|---|
| 2026-06-28 | 5 |  |  |  | `<commit-sha>` |  |  |

## Evidence thường dùng

| Loại evidence | Đường dẫn local/CI artifact |
|---|---|
| HTML report | `playwright-report/index.html` |
| JSON report | `test-results/e2e-results.json` |
| Screenshot khi fail | `test-results/**/test-failed.png` hoặc `test-results/**/test-failed-*.png` |
| Video khi fail | `test-results/**/video.webm` |
| Trace khi fail | `test-results/**/trace.zip` |
| Server log trong CI | `server.log` |
