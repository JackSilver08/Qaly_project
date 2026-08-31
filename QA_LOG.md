# QA_LOG - Nhật ký kiểm thử E2E hằng ngày

## Hướng dẫn điền nhanh

- Mỗi dòng tương ứng một kịch bản test trên một commit.
- Nếu test không chạy được vì server/local/CI không sẵn sàng, ghi `Blocked` và mô tả lý do ở cột ghi chú.
- Nếu `Fail`, điền đường dẫn evidence trong `test-results/` hoặc artifact CI.

## Nhật ký theo commit

| Ngày       | Mã Commit        | Nhánh | Tên kịch bản test                                     | Kết quả (Pass/Fail/Blocked) | Đường dẫn file Evidence nếu lỗi                                           | Ghi chú                                                               |
| ---------- | ---------------- | ----- | ----------------------------------------------------- | --------------------------- | ------------------------------------------------------------------------- | --------------------------------------------------------------------- |
| 2026-08-02 | `local-baseline` | local | Unit: TaskConcurrencyTests                            | Pass                        | `tests/Qaly.UnitTests/TaskConcurrencyTests.cs`                            | 2/2 tests passed via dotnet test                                      |
| 2026-08-02 | `local-baseline` | local | Integration: OrganizationUsersAuthorizationTests      | Pass                        | `tests/Qaly.IntegrationTests/OrganizationUsersAuthorizationTests.cs`      | Moderator capability expiry/revocation covered                        |
| 2026-08-02 | `local-baseline` | local | Integration: DashboardTenantIsolationIntegrationTests | Pass                        | `tests/Qaly.IntegrationTests/DashboardTenantIsolationIntegrationTests.cs` | Cross-tenant data leakage checks passed                               |
| 2026-08-02 | `local-baseline` | local | Integration: RcSafetyRegressionTests                  | Pass                        | `tests/Qaly.IntegrationTests/RcSafetyRegressionTests.cs`                  | Added coverage for inactive user and member removal                   |
| 2026-08-02 | `local-baseline` | local | E2E: rc-safety-workflow.spec.ts                       | Blocked                     | `tests/e2e/rc-safety-workflow.spec.ts`                                    | Requires running app and environment variables for live UI validation |
| 2026-08-29 | `cf312dac` | main  | Unit BE: toàn bộ Qaly.UnitTests                       | Pass                        | —                                                                         | 595/595 (590 cũ + 5 test mới cho routing provider AI)                 |
| 2026-08-29 | `cf312dac` | main  | Unit FE: tests/client (lớp mới, vitest)               | Pass                        | —                                                                         | 155/155 · 8 file spec · lớp unit frontend lần đầu có                  |
| 2026-08-29 | `cf312dac` | main  | Integration: toàn bộ Qaly.IntegrationTests            | Pass                        | —                                                                         | 166/166                                                               |
| 2026-08-29 | `cf312dac` | main  | Web feature: toàn bộ Qaly.WebFeatureTests             | Pass                        | —                                                                         | 38/38                                                                 |
| 2026-08-29 | `cf312dac` | main  | E2E: toàn suite, 4 worker                             | Fail                        | `test-results/`                                                           | 61 pass / 6 fail — cả 6 đều pass khi `--workers=1`, là flaky do song song |
| 2026-08-29 | `cf312dac` | main  | E2E: 6 spec fail chạy lại tuần tự                     | Pass                        | —                                                                         | 18/18 → xác nhận không phải lỗi chức năng                             |
| 2026-08-29 | `cf312dac` | main  | E2E: dark-theme.spec.ts                               | Pass                        | —                                                                         | 6/6 · 12 route + mobile + overlay + chi tiết dự án + họp/poll         |
| 2026-08-29 | `cf312dac` | main  | E2E: browser-console.spec.ts (G-2)                    | Pass                        | —                                                                         | 3/3 · đã mở rộng từ 5 lên 12 route                                    |
| 2026-08-29 | `cf312dac` | main  | E2E: responsive-audit.spec.ts (G-3, spec mới)         | Pass                        | —                                                                         | 4/4 · 1440/768/390px · không có tràn ngang trên 11 trang              |
| 2026-08-29 | `cf312dac` | main  | Typecheck + build frontend                            | Pass                        | —                                                                         | `npm run typecheck` và `npm run build` đều sạch                       |

## Tổng hợp trong ngày

| Ngày       | Tổng số test | Pass | Fail | Blocked | Commit đã kiểm thử | Người thực hiện | Nhận xét                                                                     |
| ---------- | -----------: | ---: | ---: | ------: | ------------------ | --------------- | ---------------------------------------------------------------------------- |
| 2026-08-02 |            5 |    4 |    0 |       1 | `local-baseline`   | QA automation   | Core P0/P1 regression coverage added; live E2E remains environment-dependent |
| 2026-08-29 |          967 |  961 |    6 |       0 | `cf312dac`         | QA automation   | 6 fail đều là flaky do E2E chạy 4 worker; chạy tuần tự 18/18 pass. 4 lỗi sản phẩm thật đã vá |

## Báo cáo chất lượng tuần

- Coverage P0/P1: đã có automated coverage cho tenant isolation, moderator expiry/revocation, member removal, inactive-user denial, và concurrency.
- **Cập nhật 2026-08-29** — bổ sung lớp unit test frontend (`tests/client/`, 155 test) mà trước đây dự án chưa có, và spec kiểm tra responsive (`responsive-audit.spec.ts`).
- **4 lỗi sản phẩm thật đã phát hiện và vá** trong đợt này:
  - `QALY-UI-01` số "quá hạn" ở Dashboard lệch trang Nhiệm vụ (task `Cancelled` bị đếm nhầm phía client).
  - `QALY-UI-02` dòng nhiệm vụ bấm được bằng chuột nhưng không dùng được bằng bàn phím.
  - `QALY-UI-03` mở trang chi tiết dự án ném `TypeError` ở mọi lần mở (tab mặc định thiếu guard khi đang tải).
  - `QALY-BE-01` hint model `deepseek-v4-pro` định tuyến nhầm sang Ollama thay vì DeepSeek.
- 7 spec E2E hỏng đã được sửa; nguyên nhân là assert lỗi thời chứ không phải lỗi sản phẩm — chi tiết ở `docs/17_Kiem_Thu_Toan_Dien_Va_Ban_Giao.md` mục 4.2.
- P0/P1 open defects: 0.
- Hành động tiếp theo: ổn định E2E khi chạy song song (6 spec flaky ở 4 worker), bổ sung `aria-label` cho ~46 nút icon, và chạy nốt phần checklist cần kiểm thủ công.

## Evidence thường dùng

| Loại evidence       | Đường dẫn local/CI artifact                                                |
| ------------------- | -------------------------------------------------------------------------- |
| HTML report         | `playwright-report/index.html`                                             |
| JSON report         | `test-results/e2e-results.json`                                            |
| Screenshot khi fail | `test-results/**/test-failed.png` hoặc `test-results/**/test-failed-*.png` |
| Video khi fail      | `test-results/**/video.webm`                                               |
| Trace khi fail      | `test-results/**/trace.zip`                                                |
| Server log trong CI | `server.log`                                                               |
