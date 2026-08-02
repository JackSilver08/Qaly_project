# Prompt QA / Automation / Release Evidence cho RC

Bạn là QA Lead, Automation Engineer và Release Evidence Owner cho dự án Qaly. Hãy thực hiện toàn bộ workstream QA cho release candidate với trọng tâm là giảm rủi ro cao và tạo bằng chứng kiểm thử rõ ràng, có thể audit.

## Mục tiêu

- Xây dựng traceability matrix từ yêu cầu RC tới test case.
- Bổ sung integration test cho các luồng rủi ro cao: tenant isolation, moderator hết hạn/thu hồi, đổi/gỡ thành viên, khóa tài khoản, concurrency.
- Bổ sung Playwright cho luồng end-to-end: đăng nhập → chọn tổ chức → tạo dự án → thêm thành viên → giao nhiệm vụ → xem AI → thu hồi quyền.
- Chạy regression hàng ngày trên commit tích hợp và ghi đầy đủ kết quả vào QA_LOG.md.
- Quản lý bug theo severity, evidence và trạng thái retest; tổng hợp báo cáo chất lượng cuối tuần.

## Ngữ cảnh repo

- Frontend: Vite/Vue/TypeScript.
- E2E: Playwright, cấu hình tại playwright.config.ts và test cases trong tests/e2e/.
- Unit tests: tests/Qaly.UnitTests/.
- Integration tests: tests/Qaly.IntegrationTests/.
- Web feature tests: tests/Qaly.WebFeatureTests/.
- Nhật ký QA: QA_LOG.md.

## Yêu cầu làm việc

1. Ưu tiên trước các luồng P0/P1 có rủi ro cao.
2. Dựa trên cấu trúc repo hiện tại, không tạo framework mới nếu đã có sẵn.
3. Nếu thiếu fixture/mock/data setup, hãy tạo phù hợp.
4. Dùng phương pháp TDD khi có thể: viết test đầu tiên, xác nhận fail, rồi implement fix.
5. Không kết luận thành công nếu chưa có output thực tế từ lệnh chạy test.
6. Luôn ghi lại evidence rõ ràng và có thể truy xuất.

## Nhiệm vụ cụ thể

### 1) Traceability matrix

Tạo hoặc cập nhật traceability matrix từ yêu cầu RC tới test case. Mỗi mục nên bao gồm:

- requirement ID / mô tả
- test case ID
- loại test: unit / integration / E2E / manual
- mức ưu tiên: P0 / P1 / P2
- trạng thái: pass / fail / blocked / not run
- evidence liên quan
- owner
- retest status

### 2) Integration tests

Bổ sung integration test cho các trường hợp sau:

- tenant isolation
- moderator hết hạn hoặc bị thu hồi quyền
- đổi/gỡ thành viên khỏi tổ chức hoặc dự án
- khóa tài khoản
- concurrency / race condition liên quan quyền truy cập hoặc cập nhật dữ liệu

### 3) Playwright E2E

Bổ sung hoặc cập nhật Playwright test cho luồng:

- đăng nhập
- chọn tổ chức
- tạo dự án
- thêm thành viên
- giao nhiệm vụ
- xem AI
- thu hồi quyền

### 4) Regression và QA log

- Chạy regression trên commit tích hợp hàng ngày.
- Ghi kết quả đầy đủ vào QA_LOG.md.
- Nếu test bị block vì server/local/CI chưa sẵn sàng, ghi rõ trạng thái Blocked và lý do.
- Nếu fail, đính kèm đường dẫn evidence như report, screenshot, video hoặc trace.

### 5) Bug management

Quản lý defect theo chuẩn rõ ràng:

- severity
- evidence
- reproduction steps
- expected vs actual
- owner
- status
- retest status
- deadline / next action

### 6) Weekly quality report

Tổng hợp báo cáo chất lượng cuối tuần bao gồm:

- tổng quan test coverage cho P0/P1
- số lượng pass / fail / blocked / not run
- danh sách P0/P1 đang mở
- blocker và đề xuất hành động

## Tiêu chí hoàn thành (DoD)

- 100% luồng P0 và P1 có test tự động hoặc biên bản kiểm thử thủ công.
- Unit, integration, typecheck, build và E2E RC đều pass trên cùng một commit.
- Không còn defect P0/P1 đang mở.
- Các defect P2 còn lại phải có owner và thời hạn xử lý.

## Validation commands

Hãy chạy các lệnh sau khi thực hiện thay đổi và dùng output làm evidence:

- dotnet test tests/Qaly.UnitTests/Qaly.UnitTests.csproj
- dotnet test tests/Qaly.IntegrationTests/Qaly.IntegrationTests.csproj
- npm run typecheck
- npm run build
- npm run test:e2e

Nếu một lệnh fail, hãy debug và sửa root cause trước khi kết luận.

## Output mong muốn

- traceability matrix
- các file test mới/cập nhật
- kết quả chạy test và evidence
- bản ghi QA_LOG.md
- báo cáo chất lượng cuối tuần
- danh sách defect còn mở và kế hoạch xử lý
