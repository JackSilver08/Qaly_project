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

## Tổng hợp trong ngày

| Ngày       | Tổng số test | Pass | Fail | Blocked | Commit đã kiểm thử | Người thực hiện | Nhận xét                                                                     |
| ---------- | -----------: | ---: | ---: | ------: | ------------------ | --------------- | ---------------------------------------------------------------------------- |
| 2026-08-02 |            5 |    4 |    0 |       1 | `local-baseline`   | QA automation   | Core P0/P1 regression coverage added; live E2E remains environment-dependent |

## Báo cáo chất lượng tuần

- Coverage P0/P1: đã có automated coverage cho tenant isolation, moderator expiry/revocation, member removal, inactive-user denial, và concurrency.
- P0/P1 open defects: 0 trong phạm vi hiện tại; E2E workflow được ghi nhận là blocked pending môi trường chạy app.
- Hành động tiếp theo: chạy Playwright trên môi trường có app đang chạy và cập nhật evidence chi tiết.

## Evidence thường dùng

| Loại evidence       | Đường dẫn local/CI artifact                                                |
| ------------------- | -------------------------------------------------------------------------- |
| HTML report         | `playwright-report/index.html`                                             |
| JSON report         | `test-results/e2e-results.json`                                            |
| Screenshot khi fail | `test-results/**/test-failed.png` hoặc `test-results/**/test-failed-*.png` |
| Video khi fail      | `test-results/**/video.webm`                                               |
| Trace khi fail      | `test-results/**/trace.zip`                                                |
| Server log trong CI | `server.log`                                                               |
