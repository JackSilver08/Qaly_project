# DH-03 Manual QA Evidence Log - Meeting & Import Document

## 1. Thông tin chạy QA

| Hạng mục | Giá trị |
|---|---|
| Người chạy test | Duy Hoàng |
| Thời gian chạy | 04/06/2026 |
| Tuần QA | 03/06/2026 - 09/06/2026 |
| Base URL | `http://127.0.0.1:5000` |
| App server | Đã chạy, `GET /Account/Login` trả HTTP 200 và có `loginForm` |
| Docker phụ trợ | SQL Server và Redis đang healthy trong Docker |
| Phương pháp | Manual/bán tự động bằng Playwright runner tạm trong `.tmp`, API request, screenshot thật |
| Không thực hiện | Không sửa production code, không refactor, không commit, không tạo evidence giả |

## 2. Dữ liệu chuẩn bị

| Loại dữ liệu | Giá trị đã dùng | Ghi chú |
|---|---|---|
| Admin/owner | `admin@qaly.dev` | Login OK, không ghi mật khẩu trong evidence |
| Member | `nguyenvana@qaly.dev` | Được add vào group QA Meeting |
| Outside user | `hoangthuha@qaly.dev` | Login OK, không thuộc group QA Meeting |
| Group Meeting | `DH03 QA Meeting 1780549745279` | ID `bfaa868c-7db7-4212-becb-ab73c3025728` |
| Meeting session | `b78339d8-d998-4bb4-ad03-b4a9cdf9a8c9` | Status API ban đầu `Active`, sau end không còn active meeting |
| Project Import | `DH03 QA Import 1780549763237` | ID `7b934c94-ffd2-46e5-9a5b-4844067681b9` |
| DOCX | `ProjectHub.docx` | File có sẵn trong repo, preview ra 538 blocks |
| PDF fixture | `docs/task/evidence/2026-06-03_2026-06-09/import-document/fixtures/qa-import-pdf-basic.pdf` | Fixture QA tối thiểu, PDF hiện là roadmap/unsupported |
| ZIP fixture | `docs/task/evidence/2026-06-03_2026-06-09/import-document/fixtures/qa-import-zip-basic.zip` | Có `.md`, `.txt`, `.html`, và `.pdf` unsupported |
| Edge fixtures | `qa-empty.txt`, `qa-unsupported.xyz`, `qa-corrupt.zip` | Dùng cho DH01-IMP-006 |

## 3. Lệnh đã chạy

| Mục đích | Lệnh |
|---|---|
| Kiểm tra app server | `Invoke-WebRequest http://127.0.0.1:5000/Account/Login` |
| Chạy QA Meeting/Import chính | `node .tmp/dh03-qa-runner.cjs` |
| Xác minh Wiki list/detail | `node .tmp/dh03-wiki-verify.cjs` |
| Xác minh mở từng Wiki page từ list | `node .tmp/dh03-wiki-list-click-check.cjs` |
| Cập nhật screenshot detail DOCX | `node .tmp/dh03-wiki-detail-screenshot.cjs` |

## 4. Kết quả Meeting QA

| Test case ID | Module | Preconditions | Steps đã chạy | Expected result | Actual result | Status | Severity | Evidence path | Suggested assignee | Notes |
|---|---|---|---|---|---|---|---|---|---|---|
| DH01-MTG-001 | Meeting | Admin và member login OK; member thuộc group QA | Admin mở meeting page, start meeting; member mở context khác và join; admin end meeting | Meeting tạo được, member join được, participant cập nhật, end meeting không crash | API/UI start OK; member join OK; admin end OK; participant count ở admin và member đều giữ `1` sau khi member join | Fail | P0 | `docs/task/evidence/2026-06-03_2026-06-09/meeting/20260604_duyhoang_meeting_DH01-MTG-001_admin-started.png`<br>`docs/task/evidence/2026-06-03_2026-06-09/meeting/20260604_duyhoang_meeting_DH01-MTG-001_member-joined.png`<br>`docs/task/evidence/2026-06-03_2026-06-09/meeting/20260604_duyhoang_meeting_DH01-MTG-001_admin-ended.png`<br>`docs/task/evidence/2026-06-03_2026-06-09/meeting/20260604_duyhoang_meeting_DH03_manual-qa-api-log.json` | Gia Long | Fail do participant UI/realtime không cập nhật, không phải do API start/join/end |
| DH01-MTG-002 | Meeting | Outside user login OK, không thuộc group QA; meeting đang active | Outside user gọi API join/end và mở route meeting group | Outside user bị chặn 401/403 hoặc UI không cho thao tác; không xuất hiện participant; không đổi trạng thái meeting | API join/end trả `403`; UI hiển thị toast không thể tham gia, meeting status vẫn `Sẵn sàng`; không đổi trạng thái meeting | Pass | P0 | `docs/task/evidence/2026-06-03_2026-06-09/meeting/20260604_duyhoang_meeting_DH01-MTG-002_outside-user-blocked.png`<br>`docs/task/evidence/2026-06-03_2026-06-09/meeting/20260604_duyhoang_meeting_DH03_manual-qa-api-log.json` | Quang Tuấn | Permission backend hoạt động đúng |
| DH01-MTG-004 | Meeting | 2 browser/context đã join cùng meeting; SignalR group hub connect OK | Member join; quan sát participant count ở admin không refresh | Participant count/trạng thái cập nhật realtime, hoặc ghi fail nếu chưa hỗ trợ | Console cho thấy WebSocket `/hubs/groups` connected, nhưng participant count admin vẫn `1`, member vẫn `1`; không thấy cập nhật realtime | Fail | P1 | `docs/task/evidence/2026-06-03_2026-06-09/meeting/20260604_duyhoang_meeting_DH01-MTG-004_participant-after-member-join.png`<br>`docs/task/evidence/2026-06-03_2026-06-09/meeting/20260604_duyhoang_meeting_DH03_manual-qa-api-log.json` | Gia Long | Khả năng UI chưa invoke/listen đúng meeting participant events |
| DH01-MTG-005 | Meeting | Meeting active; headless browser không hỗ trợ screen share permission | Bấm nút share screen trong meeting | UI không crash; nếu browser không hỗ trợ thì có thông báo rõ hoặc ghi bug | UI không crash; console warning `NotSupportedError: Not supported`; panel vẫn `Sẵn sàng chia sẻ`; chưa có thông báo lỗi rõ trên UI | Fail | P2 | `docs/task/evidence/2026-06-03_2026-06-09/meeting/20260604_duyhoang_meeting_DH01-MTG-005_screen-share-denied-headless.png`<br>`docs/task/evidence/2026-06-03_2026-06-09/meeting/20260604_duyhoang_meeting_DH03_manual-qa-api-log.json` | Gia Long | Cần test lại positive case bằng browser thật có quyền screen share |

## 5. Kết quả Import Document QA

| Test case ID | Module | Preconditions | Steps đã chạy | Expected result | Actual result | Status | Severity | Evidence path | Suggested assignee | Notes |
|---|---|---|---|---|---|---|---|---|---|---|
| DH01-IMP-001 | Import document | Admin có quyền project/wiki; project QA đã tạo; có `ProjectHub.docx` | Mở project, mở import modal, upload DOCX, preview, confirm, execute, mở Wiki page từ list | Preview có nội dung tối thiểu; execute tạo page đúng project; warning rõ nếu mất bảng/ảnh; không crash | Preview DOCX OK với 538 blocks; có warning bỏ qua bảng/ảnh; execute tạo Wiki page `CHƯƠNG 1: TỔNG QUAN DỰ ÁN`; mở được từ Wiki list | Pass | P0 | `docs/task/evidence/2026-06-03_2026-06-09/import-document/20260604_duyhoang_import_DH01-IMP-001_docx-preview.png`<br>`docs/task/evidence/2026-06-03_2026-06-09/import-document/20260604_duyhoang_import_DH01-IMP-001_docx-success.png`<br>`docs/task/evidence/2026-06-03_2026-06-09/import-document/20260604_duyhoang_import_DH01-IMP-001_wiki-list-after-imports.png`<br>`docs/task/evidence/2026-06-03_2026-06-09/import-document/20260604_duyhoang_import_DH01-IMP-001_wiki-detail-opened.png` | Viết Minh | Direct deep-link được kiểm tra thêm nhưng không dùng làm tiêu chí pass; mở từ Wiki list OK |
| DH01-IMP-002 | Import document | Project QA đã tạo; ZIP fixture có `.md`, `.txt`, `.html`, `.pdf` | Upload ZIP, preview bundle, execute import, kiểm tra Wiki list và mở từng page từ list | File hỗ trợ được import; unsupported count rõ; không tạo page rỗng | Preview có 4 entries, supported 3, warning bỏ qua PDF unsupported; execute tạo 3 Wiki pages: `page`, `notes`, `DH03 ZIP Markdown`; mở được từng page từ Wiki list | Pass | P0 | `docs/task/evidence/2026-06-03_2026-06-09/import-document/20260604_duyhoang_import_DH01-IMP-002_zip-preview.png`<br>`docs/task/evidence/2026-06-03_2026-06-09/import-document/20260604_duyhoang_import_DH01-IMP-002_zip-success.png`<br>`docs/task/evidence/2026-06-03_2026-06-09/import-document/20260604_duyhoang_import_DH03_wiki-list-click-log.json` | Viết Minh | ZIP behavior đúng scope hiện tại |
| DH01-IMP-003 | Import document | Project QA đã tạo; PDF fixture tối thiểu | Upload PDF qua UI; gọi API preview document bằng PDF | Nếu PDF parser chưa có: báo unsupported rõ ràng, không tạo dữ liệu | UI toast: PDF nằm trong roadmap, chưa có parser; API preview trả `400` với danh sách định dạng hỗ trợ hiện tại; không có Wiki page PDF được tạo | Pass | P0 | `docs/task/evidence/2026-06-03_2026-06-09/import-document/20260604_duyhoang_import_DH01-IMP-003_pdf-unsupported-ui.png`<br>`docs/task/evidence/2026-06-03_2026-06-09/import-document/20260604_duyhoang_import_DH03_manual-qa-api-log.json` | Viết Minh | Kết quả không nói quá là PDF đã hỗ trợ |
| DH01-IMP-006 | Import document | Có file rỗng, file unsupported, ZIP lỗi | Gọi API preview cho `qa-empty.txt`, `qa-unsupported.xyz`, `qa-corrupt.zip` | Báo lỗi rõ; không crash; không tạo dữ liệu rác | Empty file trả `400 Vui long chon file`; unsupported file trả `400` với danh sách định dạng hỗ trợ; corrupt ZIP trả `400 File ZIP khong hop le`; không crash | Pass | P2 | `docs/task/evidence/2026-06-03_2026-06-09/import-document/20260604_duyhoang_import_DH03_manual-qa-api-log.json` | Viết Minh | Evidence là API log, không tạo screenshot giả |

## 6. Bug ghi nhận

### DH03-BUG-MTG-001 - Participant realtime/count không cập nhật khi member join meeting

| Trường | Nội dung |
|---|---|
| Severity | P0 |
| Environment | `http://127.0.0.1:5000`, Docker SQL/Redis healthy, headless Chromium |
| Steps to reproduce | 1. Login admin. 2. Tạo/start meeting trong group. 3. Login member ở context khác. 4. Member join cùng `meetingId`. 5. Quan sát participant count/list ở tab admin. |
| Expected | Admin thấy participant count/list cập nhật khi member join; member không cần refresh. |
| Actual | Admin participant count vẫn `1`; member participant count cũng `1`; API join thành công và cả hai UI đều ở trạng thái active. |
| Evidence | `docs/task/evidence/2026-06-03_2026-06-09/meeting/20260604_duyhoang_meeting_DH01-MTG-004_participant-after-member-join.png`<br>`docs/task/evidence/2026-06-03_2026-06-09/meeting/20260604_duyhoang_meeting_DH03_manual-qa-api-log.json` |
| Suggested assignee | Gia Long |
| Suggested fix area | `GroupMeetingPage.vue` realtime participant handling; kiểm tra invoke/listen `JoinMeeting`, `LeaveMeeting`, `meetingParticipantJoined`, `meetingParticipantLeft` so với `GroupHub` |

### DH03-BUG-MTG-002 - Screen share unsupported chỉ log console, chưa có UI feedback rõ

| Trường | Nội dung |
|---|---|
| Severity | P2 |
| Environment | `http://127.0.0.1:5000`, headless Chromium không hỗ trợ `getDisplayMedia` |
| Steps to reproduce | 1. Login admin. 2. Start meeting. 3. Bấm nút share screen. |
| Expected | UI không crash và hiển thị lỗi/trạng thái rõ khi browser không hỗ trợ hoặc user deny. |
| Actual | UI không crash; console warning `NotSupportedError: Not supported`; panel vẫn hiển thị `Sẵn sàng chia sẻ`, chưa có feedback lỗi trên UI. |
| Evidence | `docs/task/evidence/2026-06-03_2026-06-09/meeting/20260604_duyhoang_meeting_DH01-MTG-005_screen-share-denied-headless.png`<br>`docs/task/evidence/2026-06-03_2026-06-09/meeting/20260604_duyhoang_meeting_DH03_manual-qa-api-log.json` |
| Suggested assignee | Gia Long |
| Suggested fix area | `ScreenSharePanel.vue` UI state/error message cho `getDisplayMedia` failure |

## 7. Tổng hợp trạng thái

| Nhóm | Pass | Fail | Blocked | Not Tested | Tổng |
|---|---:|---:|---:|---:|---:|
| Meeting | 1 | 3 | 0 | 0 | 4 |
| Import document | 4 | 0 | 0 | 0 | 4 |
| Tổng | 5 | 3 | 0 | 0 | 8 |

## 8. Blocker và yêu cầu hỗ trợ

| Hạng mục | Trạng thái | Cần ai hỗ trợ | Ghi chú |
|---|---|---|---|
| Screen share positive case | Cần xác minh thêm trên browser thật/headful có quyền screen share | Gia Long / Duy Hoàng | Headless Chromium chỉ xác minh nhánh unsupported/deny |
| Meeting participant realtime | Fail, cần fix trước khi nghiệm thu participant list/count | Gia Long | API start/join/end OK; vấn đề gần nhất ở UI realtime |
| Outside user | Không blocked | Quang Tuấn nếu cần audit thêm | API 403 đúng kỳ vọng |
| PDF parser | Không blocked | Viết Minh | Build hiện tại báo unsupported/roadmap rõ ràng |

## 9. File không nên commit nếu team không yêu cầu

| Path | Lý do |
|---|---|
| `.tmp/dh03-qa-runner.cjs` | Runner tạm để chạy QA bán tự động |
| `.tmp/dh03-wiki-verify.cjs` | Runner tạm xác minh Wiki |
| `.tmp/dh03-wiki-detail-check.cjs` | Runner tạm kiểm tra route Wiki detail |
| `.tmp/dh03-wiki-list-click-check.cjs` | Runner tạm kiểm tra mở Wiki từ list |
| `.tmp/dh03-wiki-detail-screenshot.cjs` | Runner tạm cập nhật screenshot detail |
| `docs/task/evidence/2026-06-03_2026-06-09/dh03-run-summary.json` | Log tổng hợp runtime, nên commit chỉ khi team muốn lưu raw evidence |
| `docs/task/evidence/2026-06-03_2026-06-09/import-document/fixtures/zip-src/` | Source tạm để tạo ZIP fixture; ZIP fixture chính đã có |
