# Test Plan tuần 03/06/2026 - 09/06/2026

**Task:** DH-01  
**Người phụ trách QA + tài liệu:** Duy Hoàng  
**Thời gian:** 03/06/2026 - 09/06/2026  
**Trạng thái tài liệu:** Kế hoạch kiểm thử tổng; chưa phải báo cáo kết quả chạy test.

## 1. Căn cứ rà soát nhanh repo

Tài liệu này được lập sau khi rà soát nhanh repo để tránh ghi quá chức năng hiện có.

| Nhóm | Hiện trạng đã thấy trong repo |
|---|---|
| Backend module | Có controller/API cho `Meetings`, `Import`, `Ai`, `Analytics`, `DashboardV2`, `Groups`, `GroupAi`, `Auth`, `AdminUsers`. |
| Frontend module | Có trang/component liên quan `GroupMeetingPage.vue`, `GroupPollPage.vue`, `AnalyticsPage.vue`, `DashboardPage.vue`, `ImportModal.vue`, `GroupAiPanel.vue`. |
| Test hiện có | Có unit test cho AI, import, meeting action item, group, auth, permission, dashboard; có integration test cho meeting action items, dashboard, project, wiki visibility; có Playwright smoke cho group chat, poll, meeting. |
| Tài liệu hiện có | Có `docs/task_week4.md`, `docs/demo-seed.md`, `docs/06_Docker_Setup.md`, `docs/07_CICD_Workflow.md`, `docs/08_AI_Integration.md`, các plan import/group/AI trong `docs/task`. |
| Ghi chú import document | Backend hiện thấy hỗ trợ document `.md`, `.markdown`, `.txt`, `.html`, `.htm`, `.docx`; ZIP hỗ trợ các file con cùng nhóm này. PDF phải được test theo trạng thái build tại thời điểm chạy: nếu parser PDF chưa merge thì kỳ vọng đúng là báo lỗi rõ ràng và không tạo dữ liệu. |
| Ghi chú AI | README mô tả AI provider có thể fallback/mock khi thiếu API key/provider. Vì vậy test plan không mặc định yêu cầu cloud AI luôn hoạt động. |

## 2. Mục tiêu kiểm thử tuần này

1. Xác định các luồng P0 bắt buộc pass để demo/nghiệm thu tuần 03/06/2026 - 09/06/2026.
2. Kiểm tra bảy module trọng tâm: Meeting, Import document, Deploy config, AI analytics, Group AI, Auth/permission, Regression core.
3. Thu thập bằng chứng kiểm thử đủ để phục vụ DH-03, DH-04, DH-05 và nghiệm thu cuối tuần.
4. Phân loại rõ lỗi chặn demo, lỗi quan trọng và lỗi polish/edge case.
5. Ghi nhận rủi ro môi trường thay vì đánh dấu fail sai khi thiếu Docker, AI provider, SignalR hoặc file mẫu.

## 3. Phạm vi kiểm thử

### 3.1 Trong phạm vi

- Manual QA cho các luồng demo chính.
- E2E smoke cho group chat, poll, meeting và các luồng UI có thể tự động hóa.
- Integration/API test cho permission, import, meeting action item, dashboard/analytics.
- Unit/regression test hiện có để bảo vệ core.
- Kiểm tra deploy config ở mức cấu hình, biến môi trường, service dependency và log.

### 3.2 Ngoài phạm vi

- Không sửa code core.
- Không refactor.
- Không mở rộng chức năng mới.
- Không cam kết pass cho tính năng chưa merge hoặc chưa có provider/môi trường.
- Không làm performance/load test sâu; chỉ smoke hoặc sanity nếu cần.
- Không làm security pentest đầy đủ; chỉ kiểm tra quyền truy cập theo flow nghiệp vụ.

## 4. Quy ước ưu tiên

| Ưu tiên | Ý nghĩa | Tiêu chí xử lý |
|---|---|---|
| P0 | Bắt buộc pass để demo/nghiệm thu | Fail P0 phải tạo blocker, có owner fix hoặc waiver rõ ràng trước demo. |
| P1 | Quan trọng nhưng không chặn demo ngay | Phải triage, có workaround hoặc kế hoạch fix trước cuối tuần. |
| P2 | Polish/edge case | Có thể đưa backlog nếu không ảnh hưởng demo chính. |

## 5. Dữ liệu chuẩn bị

### 5.1 Tài khoản

| Vai trò test | Tài khoản đề xuất | Nguồn/ghi chú |
|---|---|---|
| Owner/Admin | `admin@qaly.dev` | Seeder có admin khi DB rỗng. Mật khẩu lấy từ `QALY_SEED_ADMIN_PASSWORD` hoặc `E2E_ADMIN_PASSWORD`; không ghi mật khẩu thật vào tài liệu/evidence. |
| Member/Manager | `nguyenvana@qaly.dev` | Seeder tạo user member; trong project seed có thể là Manager của `qaly-mvp`. Mật khẩu lấy từ `QALY_SEED_DEFAULT_USER_PASSWORD`. |
| Member thường | `tranthib@qaly.dev`, `levancuong@qaly.dev` | Dùng để test join group, vote poll, chat, quyền member. Mật khẩu lấy từ `QALY_SEED_DEFAULT_USER_PASSWORD`. |
| Outside user | `outside-qa-2026@qaly.dev` | Nếu chưa có thì tạo bằng admin API/UI; không add vào project/group đang test. |
| User bị khóa | `inactive-qa-2026@qaly.dev` | Chỉ tạo nếu cần test P2 auth; không bắt buộc cho demo P0. |

### 5.2 Group và project mẫu

| Loại dữ liệu | Giá trị đề xuất | Ghi chú |
|---|---|---|
| Group seed | `Demo Group` | Seeder tạo group demo, message, poll và meeting session placeholder. |
| Group QA mới | `DH01 QA Group 2026-06-03` | Tạo mới để tránh ảnh hưởng dữ liệu demo. Owner là admin; thêm 2-3 member; outside user không thuộc group. |
| Project seed | `Hệ thống Quản lý Qaly MVP` / code `qaly-mvp` | Dùng cho dashboard, analytics, import và regression task. |
| Project QA mới | `DH01 QA Project 2026-06-03` | Dùng riêng cho import document/CSV/XLSX để dễ undo và thu evidence. |

### 5.3 File mẫu

| File mẫu | Mục đích | Ghi chú |
|---|---|---|
| `ProjectHub.docx` | DOCX import smoke | File DOCX có sẵn ở repo root; nếu quá lớn cho demo, QA nên chuẩn bị thêm DOCX nhỏ. |
| `qa-import-docx-basic.docx` | DOCX positive | Cần người phụ trách import cung cấp nếu muốn test nhanh và ổn định. |
| `qa-import-pdf-basic.pdf` | PDF state check | Chỉ pass positive nếu PDF parser đã merge. Nếu chưa, pass khi hệ thống trả lỗi định dạng rõ ràng và không tạo dữ liệu. |
| `qa-import-doc-bundle.zip` | ZIP positive | ZIP nên chứa `.md`, `.txt`, `.html`, `.docx`; không trộn PDF trừ khi test unsupported entry. |
| `qa-import-tasks.csv` | CSV task import | Dùng cho regression import task bảng. |
| `qa-import-tasks.xlsx` | XLSX task import | Dùng cho regression import task bảng. |

### 5.4 Dữ liệu AI/analytics

| Dữ liệu | Mục đích | Ghi chú |
|---|---|---|
| Seed tasks trong `qaly-mvp` | Dashboard, analytics, AI summary/risk | Không chỉnh trực tiếp DB trừ khi cần reset. |
| Group chat mẫu | Group AI summary/action items/draft project | Tạo 5-10 message có quyết định, owner, deadline, rủi ro. |
| `tests/Qaly.UnitTests/Fixtures/ai-golden-dataset.json` | Tham chiếu prompt AI có sẵn | Dùng cho evaluation khi chạy unit hoặc so sánh output. |
| 20 prompt evaluation của QB-05 | AI demo/evaluation | Cần Quốc Bảo/Chí Khang cung cấp nếu chưa có. |

## 6. Cách lưu bằng chứng test

**Thư mục evidence đề xuất:** `docs/task/evidence/2026-06-03_2026-06-09/`

| Module | Thư mục con |
|---|---|
| Meeting | `meeting/` |
| Import document | `import-document/` |
| Deploy config | `deploy-config/` |
| AI analytics | `ai-analytics/` |
| Group AI | `group-ai/` |
| Auth/permission | `auth-permission/` |
| Regression core | `regression-core/` |

**Naming convention:**

```text
YYYYMMDD_<tester>_<module>_<test-case-id>_<short-desc>.<ext>
```

Ví dụ:

- `20260605_duyhoang_meeting_DH01-MTG-001_start-join-end.png`
- `20260606_duyhoang_import_DH01-IMP-002_zip-preview-execute.webm`
- `20260608_duyhoang_regression_DH01-REG-001_dotnet-test.trx`
- `20260608_duyhoang_deploy_DH01-DEP-001_docker-compose-config.log`

## 7. Tiêu chí pass/fail

| Trạng thái | Tiêu chí |
|---|---|
| Pass | Tất cả bước chạy được, kết quả đúng kỳ vọng, không phát sinh lỗi nghiêm trọng, có evidence đúng naming convention. |
| Fail | Sai kết quả kỳ vọng, crash, mất dữ liệu, leak dữ liệu, sai quyền truy cập, hoặc không thể hoàn thành luồng P0 khi môi trường đã đủ điều kiện. |
| Blocked | Không chạy được vì thiếu môi trường/dữ liệu/provider/SignalR/file mẫu hoặc chức năng chưa merge. Phải ghi rõ lý do và người cần cung cấp. |
| N/A | Không còn thuộc scope tuần này hoặc bị thay thế bằng quyết định kỹ thuật mới đã được chốt. |

**Tiêu chí nghiệm thu tuần:** toàn bộ P0 phải Pass hoặc có waiver được nhóm chấp thuận; P1 phải được triage; P2 có thể đưa backlog.

## 8. Test cases

### 8.1 Meeting

| Mã test case | Module | Mục tiêu kiểm thử | Điều kiện chuẩn bị | Các bước thực hiện | Kết quả mong đợi | Loại test | Người chạy test | Bằng chứng cần lưu | Ưu tiên |
|---|---|---|---|---|---|---|---|---|---|
| DH01-MTG-001 | Meeting | Kiểm tra luồng tạo/start/join/end meeting trong group | Admin và member thuộc `DH01 QA Group 2026-06-03`; app chạy local hoặc docker | 1. Login admin. 2. Vào group meeting. 3. Start meeting. 4. Login member ở tab khác. 5. Join meeting. 6. Admin end meeting. | Meeting được tạo; member join được; danh sách participant cập nhật; end meeting đóng session hoặc chuyển trạng thái kết thúc; UI không crash. | Manual / E2E | Duy Hoàng | Screenshot/video UI, log app nếu có | P0 |
| DH01-MTG-002 | Meeting | Chặn outside user join/end meeting ngoài group | Outside user không thuộc group; có active meeting | 1. Login outside user. 2. Truy cập route/API meeting của group. 3. Thử join. 4. Thử end meeting nếu có endpoint/UI. | Outside user bị chặn bằng 401/403 hoặc UI không cho thao tác; không xuất hiện trong participant; không đổi trạng thái meeting. | Manual / Integration | Duy Hoàng | Screenshot lỗi quyền, API log | P0 |
| DH01-MTG-003 | Meeting | Kiểm tra import Meetily/action items và tạo/link task | Có project mẫu; có transcript/action item hợp lệ | 1. Gọi/import Meetily meeting. 2. Xem action items. 3. Tạo task từ action item. 4. Link action item với task có sẵn. 5. Xem lại task-link. | Action items được parse/lưu; task tạo đúng project; link trả về ổn định; không tạo trùng ngoài ý muốn. | Integration / Manual | Duy Hoàng | API response/log, screenshot task | P1 |
| DH01-MTG-004 | Meeting | Kiểm tra realtime join/leave/update participant | Redis/SignalR hoạt động; 2 browser/tab đăng nhập khác user | 1. Start meeting. 2. Member join/leave. 3. Quan sát tab admin không refresh. 4. Lặp lại với tab member khác. | UI nhận sự kiện realtime; participant count đúng; nếu SignalR mất kết nối thì UI có trạng thái lỗi hoặc fallback rõ. | Manual / E2E | Duy Hoàng | Video ngắn, browser console nếu lỗi | P1 |
| DH01-MTG-005 | Meeting | Kiểm tra xử lý browser không hỗ trợ hoặc từ chối screen share | Browser có thể deny permission; không cần meeting provider thật | 1. Start meeting. 2. Bấm share screen. 3. Từ chối quyền hoặc dùng browser không hỗ trợ. | UI không crash; có thông báo lỗi/trạng thái rõ; meeting session không bị kết thúc sai. | Manual | Duy Hoàng | Screenshot thông báo, console log nếu có | P2 |

### 8.2 Import document

| Mã test case | Module | Mục tiêu kiểm thử | Điều kiện chuẩn bị | Các bước thực hiện | Kết quả mong đợi | Loại test | Người chạy test | Bằng chứng cần lưu | Ưu tiên |
|---|---|---|---|---|---|---|---|---|---|
| DH01-IMP-001 | Import document | Import DOCX tạo nội dung wiki/page đúng | Project QA mới; file `ProjectHub.docx` hoặc `qa-import-docx-basic.docx` | 1. Login user có quyền project. 2. Mở import document. 3. Upload DOCX preview. 4. Execute import. 5. Mở page được tạo. | Preview có title/content/block count; execute tạo page đúng project; warning về bảng/ảnh nếu parser tối thiểu bỏ qua; không crash. | Manual / Integration | Duy Hoàng | Screenshot preview/page, API response | P0 |
| DH01-IMP-002 | Import document | Import ZIP chứa nhiều document được hỗ trợ | ZIP chứa `.md`, `.txt`, `.html`, `.docx`; project QA mới | 1. Upload ZIP preview. 2. Kiểm tra danh sách entry. 3. Execute ZIP. 4. Mở các page được tạo. | Entry hỗ trợ được preview/import; unsupported count đúng nếu có file không hỗ trợ; tạo nhiều page; không tạo page rỗng. | Manual / Integration | Duy Hoàng | Video/screenshot, response JSON | P0 |
| DH01-IMP-003 | Import document | Xác định trạng thái PDF trung thực theo build hiện tại | File `qa-import-pdf-basic.pdf`; biết rõ PDF parser đã merge hay chưa | 1. Upload PDF preview. 2. Nếu preview pass thì execute. 3. Nếu preview fail thì kiểm tra thông báo và DB/page. | Nếu PDF parser chưa merge: lỗi định dạng rõ ràng, không tạo dữ liệu. Nếu parser đã merge: preview/execute đúng nội dung tối thiểu đã scope. | Manual / Integration | Duy Hoàng | Screenshot lỗi hoặc page, log API | P0 |
| DH01-IMP-004 | Import document | Regression CSV/XLSX task import vẫn hoạt động | Project QA; file `qa-import-tasks.csv` và `qa-import-tasks.xlsx` | 1. Parse CSV. 2. Execute import. 3. Parse XLSX. 4. Execute import. 5. Kiểm tra task/label/assignee nếu có mapping. | Dòng hợp lệ được import; dòng lỗi/skipped được báo rõ; không phá luồng document import. | Manual / Integration | Duy Hoàng | Response JSON, screenshot task list | P1 |
| DH01-IMP-005 | Import document | Undo import session và report kết quả | Có session import mới tạo từ CSV/XLSX/document nếu UI hỗ trợ | 1. Xem danh sách session theo project. 2. Undo session còn hạn. 3. Refresh dữ liệu. 4. Thử undo lại. | Session hiển thị đúng; undo xóa/đánh dấu dữ liệu import theo thiết kế; undo lần hai không gây lỗi dữ liệu. | Manual / Integration | Duy Hoàng | Screenshot session, API log | P1 |
| DH01-IMP-006 | Import document | Edge case file rỗng/ZIP lỗi/mixed unsupported | File rỗng, ZIP hỏng, ZIP chỉ có PDF hoặc file không hỗ trợ | 1. Upload từng file. 2. Quan sát preview/error. 3. Kiểm tra không tạo page/task. | Hệ thống báo lỗi rõ; không crash; không tạo dữ liệu rác; log đủ để debug. | Manual | Duy Hoàng | Screenshot lỗi, log app | P2 |

### 8.3 Deploy config

| Mã test case | Module | Mục tiêu kiểm thử | Điều kiện chuẩn bị | Các bước thực hiện | Kết quả mong đợi | Loại test | Người chạy test | Bằng chứng cần lưu | Ưu tiên |
|---|---|---|---|---|---|---|---|---|---|
| DH01-DEP-001 | Deploy config | Kiểm tra cấu hình Docker infra cơ bản | Docker Desktop sẵn sàng; repo có `.env` hoặc `.env.example` | 1. Chạy `docker compose config`. 2. Chạy infra nếu được phép. 3. Kiểm tra SQL Server, Redis, Seq, MailHog port. | Compose config hợp lệ; service infra dùng đúng port/env; không thiếu secret seed bắt buộc. | Manual | Duy Hoàng | `docker-compose-config.log`, screenshot container | P0 |
| DH01-DEP-002 | Deploy config | App chạy được với DB/Redis và seeding account | Docker infra hoặc local SQL/Redis; `QALY_SEED_ADMIN_PASSWORD` và `QALY_SEED_DEFAULT_USER_PASSWORD` có giá trị | 1. Start infra. 2. Run app hoặc docker web. 3. Mở app. 4. Login admin. 5. Kiểm tra seed project/group. | App lên được; DB migration/seed chạy; admin login được; group/project seed tồn tại. | Manual / E2E | Duy Hoàng | App log, screenshot login/dashboard | P0 |
| DH01-DEP-003 | Deploy config | Thiếu AI provider không làm app/deploy fail | Không set OpenAI/Gemini key hoặc tắt Ollama theo kịch bản | 1. Start app không có cloud key. 2. Mở dashboard/analytics. 3. Gọi một AI endpoint/demo. | App vẫn chạy; AI trả fallback/mock hoặc lỗi có kiểm soát; không crash toàn app. | Manual / Integration | Duy Hoàng | Screenshot UI, API/log provider | P1 |
| DH01-DEP-004 | Deploy config | Kiểm tra production config không phụ thuộc placeholder nguy hiểm | Có `appsettings.Production.example.json`, `.env.example`, docker config | 1. Review biến env cần set. 2. Đối chiếu connection string/Redis/Seq/Email/AI. 3. Ghi thiếu sót nếu có. | Danh sách biến bắt buộc rõ; không dùng nhầm placeholder cho nghiệm thu; secret thật không đưa vào evidence công khai. | Manual | Duy Hoàng | Checklist config, ảnh/log nếu cần | P1 |
| DH01-DEP-005 | Deploy config | Ghi nhận xử lý port conflict/log collection | Có máy dev có thể kiểm tra port; không bắt buộc gây conflict thật | 1. Kiểm tra port SQL/Redis/Seq/MailHog/app. 2. Nếu conflict, ghi workaround. 3. Kiểm tra lệnh lấy log. | Có hướng dẫn workaround; log lấy được từ service liên quan; không đánh fail nếu máy cá nhân thiếu port trống nhưng phải ghi blocked. | Manual | Duy Hoàng | Log/screenshot terminal | P2 |

### 8.4 AI analytics

| Mã test case | Module | Mục tiêu kiểm thử | Điều kiện chuẩn bị | Các bước thực hiện | Kết quả mong đợi | Loại test | Người chạy test | Bằng chứng cần lưu | Ưu tiên |
|---|---|---|---|---|---|---|---|---|---|
| DH01-AIAN-001 | AI analytics | Dashboard/analytics load dữ liệu project/workspace | Project seed có task; user có quyền project | 1. Login admin/member có quyền. 2. Mở dashboard/analytics. 3. Gọi `/api/analytics/projects/{projectId}` hoặc UI tương ứng. | Summary/cards/chart hiển thị dữ liệu hợp lệ; không lỗi 500; số liệu không âm hoặc vô lý. | Manual / Integration | Duy Hoàng | Screenshot dashboard, response JSON | P0 |
| DH01-AIAN-002 | AI analytics | AI summary/risk/insight không crash khi provider fallback | Seed project; provider thật hoặc fallback/mock | 1. Gọi project summary. 2. Gọi risks. 3. Gọi insights. 4. Quan sát UI hoặc API. | Có phản hồi đúng cấu trúc; nếu fallback thì thể hiện an toàn; không trả dữ liệu dự án khác; không crash UI. | Manual / Integration | Duy Hoàng | API response, screenshot UI | P0 |
| DH01-AIAN-003 | AI analytics | Permission filter cho analytics/AI theo project | Member có quyền project A; outside user không có quyền | 1. Login member gọi analytics/AI project A. 2. Login outside user gọi cùng project. | User có quyền xem được; outside bị chặn hoặc không có dữ liệu; không leak tên/task/risk của project. | Integration / Manual | Duy Hoàng | API log, screenshot 403/empty | P1 |
| DH01-AIAN-004 | AI analytics | Chat/stream xử lý câu hỏi demo và trạng thái loading | Có analytics page; nếu provider thật thì cấu hình sẵn; nếu không thì fallback | 1. Mở AI chat/analytics. 2. Hỏi câu demo về project. 3. Quan sát loading/stream/final answer. | UI có loading rõ; phản hồi hoàn tất hoặc fallback; không bị treo vô hạn; lỗi provider có thông báo. | Manual / E2E | Duy Hoàng | Video ngắn, console/log nếu lỗi | P1 |
| DH01-AIAN-005 | AI analytics | Edge case prompt rỗng/quá dài/schema bất thường | Tài khoản có quyền; chuẩn bị prompt rỗng và prompt dài | 1. Gửi prompt rỗng. 2. Gửi prompt dài. 3. Nếu có output structured thì kiểm tra render. | Validation rõ; không vỡ layout; schema sai được guard hoặc hiển thị an toàn. | Manual / Unit | Duy Hoàng | Screenshot lỗi, test output nếu có | P2 |

### 8.5 Group AI

| Mã test case | Module | Mục tiêu kiểm thử | Điều kiện chuẩn bị | Các bước thực hiện | Kết quả mong đợi | Loại test | Người chạy test | Bằng chứng cần lưu | Ưu tiên |
|---|---|---|---|---|---|---|---|---|---|
| DH01-GAI-001 | Group AI | Tóm tắt thảo luận group | Group QA có 5-10 message có quyết định/rủi ro/deadline | 1. Login member có quyền. 2. Mở Group AI panel hoặc gọi summary API. 3. Chạy summarize. | Trả summary/decision/question theo cấu trúc hiện có; không tự tạo dữ liệu ngoài ý muốn; fallback rõ nếu thiếu provider. | Manual / Integration | Duy Hoàng | Screenshot panel, response JSON | P0 |
| DH01-GAI-002 | Group AI | Sinh draft project/task từ thảo luận group | Group có message chứa task, owner, deadline; user có quyền | 1. Chạy draft project. 2. Kiểm tra payload. 3. Nếu UI có confirm thì chọn một số task tạo project. | Draft có title/task/priority/owner gợi ý ở mức hợp lý; không tự ghi project nếu chưa confirm; dữ liệu tạo thật đúng group/project scope. | Manual / Integration | Duy Hoàng | Video/screenshot, API response | P0 |
| DH01-GAI-003 | Group AI | Chặn outside user truy cập Group AI context/summary/draft | Outside user không thuộc group | 1. Login outside user. 2. Gọi context/summary/draft/action-items của group. | Outside user bị chặn; không leak message/context/draft/action items. | Integration / Manual | Duy Hoàng | API response 401/403, log | P0 |
| DH01-GAI-004 | Group AI | Trích xuất action items từ chat | Group có message dạng “A làm X trước ngày Y” | 1. Chạy extract action items. 2. Đối chiếu output với message. 3. Kiểm tra owner/due/priority nếu có. | Output có action item đúng nội dung chính; thiếu dữ liệu thì để trống/cảnh báo thay vì bịa chắc chắn. | Manual / Integration | Duy Hoàng | Response JSON, screenshot panel | P1 |
| DH01-GAI-005 | Group AI | Edge case chat ít dữ liệu hoặc mơ hồ | Group chỉ có 1-2 message ngắn hoặc nội dung xã giao | 1. Chạy summary/draft/action-items. 2. Quan sát warning/output. | Hệ thống trả kết quả rỗng/cảnh báo phù hợp; không sinh kế hoạch giả quá tự tin. | Manual | Duy Hoàng | Screenshot output/warning | P2 |

### 8.6 Auth/permission

| Mã test case | Module | Mục tiêu kiểm thử | Điều kiện chuẩn bị | Các bước thực hiện | Kết quả mong đợi | Loại test | Người chạy test | Bằng chứng cần lưu | Ưu tiên |
|---|---|---|---|---|---|---|---|---|---|
| DH01-AUTH-001 | Auth/permission | Login, `/me`, logout và session cơ bản | Admin/member account có mật khẩu đúng | 1. Login admin. 2. Gọi/mở `/me`. 3. Logout. 4. Truy cập trang/API cần auth. | Login thành công; `/me` trả đúng user/role; logout xóa phiên; API sau logout bị chặn. | Manual / Integration | Duy Hoàng | Screenshot, response JSON | P0 |
| DH01-AUTH-002 | Auth/permission | Admin API bị chặn với member thường | Admin và member thường | 1. Login member. 2. Gọi danh sách admin users. 3. Login admin gọi lại. | Member bị 403; admin xem được danh sách; không leak dữ liệu admin cho member. | Integration / Manual | Duy Hoàng | API response/log | P0 |
| DH01-AUTH-003 | Auth/permission | Project permission chặn outside user | Project QA có member; outside user không thuộc project | 1. Login member xem project/task/wiki/import. 2. Login outside gọi cùng route/API. | Member hợp lệ xem được; outside bị chặn hoặc không có dữ liệu; không leak task/wiki/import session. | Integration / Manual | Duy Hoàng | Screenshot 403/empty, API log | P0 |
| DH01-AUTH-004 | Auth/permission | Group role owner/admin/member thao tác đúng quyền | Group QA có owner/admin/member | 1. Owner mời/thêm member. 2. Admin đổi role nếu được phép. 3. Member thử xóa/đổi role. | Owner/admin thao tác theo rule; member bị chặn thao tác quản trị; audit/log nếu có. | Manual / Integration | Duy Hoàng | Screenshot member list, API log | P1 |
| DH01-AUTH-005 | Auth/permission | Change password và revoke sessions | Tài khoản member QA có thể đổi mật khẩu | 1. Login member. 2. Change password. 3. Logout/login lại bằng password mới. 4. Delete sessions nếu có. | Password mới dùng được; password cũ bị chặn; session revoke không làm hỏng user khác. | Manual / Integration | Duy Hoàng | Screenshot, response JSON | P1 |
| DH01-AUTH-006 | Auth/permission | Edge case credential sai/user inactive/validation | Có user inactive nếu cần; có password sai | 1. Login sai password. 2. Login email không tồn tại. 3. Login user inactive nếu có. | Trả lỗi rõ, không lộ thông tin nhạy cảm quá mức, không crash UI. | Manual / Unit | Duy Hoàng | Screenshot lỗi | P2 |

### 8.7 Regression core

| Mã test case | Module | Mục tiêu kiểm thử | Điều kiện chuẩn bị | Các bước thực hiện | Kết quả mong đợi | Loại test | Người chạy test | Bằng chứng cần lưu | Ưu tiên |
|---|---|---|---|---|---|---|---|---|---|
| DH01-REG-001 | Regression core | Unit và integration test hiện có pass | .NET SDK phù hợp; DB/Redis cho integration nếu chạy full | 1. Chạy unit test. 2. Chạy integration test. 3. Lưu TRX/log. | Test pass hoặc fail được triage; không bỏ qua fail P0 liên quan module tuần này. | Unit / Integration | Duy Hoàng | `.trx`, console log | P0 |
| DH01-REG-002 | Regression core | Playwright smoke group chat/poll/meeting pass | App chạy ở base URL; admin E2E account có mật khẩu đúng | 1. Chạy `npm run test:e2e`. 2. Kiểm tra report. 3. Lưu video/report nếu fail. | Smoke test tạo group/chat/poll/meeting pass; fail phải phân loại blocker nếu ảnh hưởng demo. | E2E | Duy Hoàng | Playwright report, screenshot/video | P0 |
| DH01-REG-003 | Regression core | CRUD core project/task/comment/notification không bị ảnh hưởng | Project QA; admin/member có quyền | 1. Tạo task. 2. Cập nhật status/priority. 3. Thêm comment. 4. Kiểm tra notification/dashboard. | Core task/project vẫn hoạt động; dashboard cập nhật hợp lý; không lỗi dữ liệu cơ bản. | Manual / Integration | Duy Hoàng | Screenshot task/comment/dashboard | P1 |
| DH01-REG-004 | Regression core | Audit/log cho thao tác nhạy cảm | Seq/log app hoạt động nếu có; admin/member | 1. Thực hiện login, admin user update, import, Group AI. 2. Kiểm tra log/audit nếu có UI/API. | Log đủ truy vết thao tác nhạy cảm; không ghi secret/password/API key ra evidence. | Manual / Integration | Duy Hoàng | Log đã che secret, screenshot Seq nếu có | P1 |
| DH01-REG-005 | Regression core | UI polish/responsive smoke cho màn hình demo | Browser desktop; nếu có mobile viewport thì kiểm tra thêm | 1. Mở dashboard, teams, meeting, import, analytics. 2. Resize viewport. 3. Kiểm tra layout chính. | Không vỡ layout nghiêm trọng; lỗi nhỏ ghi P2; không chặn demo nếu có workaround. | Manual | Duy Hoàng | Screenshot trước/sau resize | P2 |

## 9. Tổng hợp số lượng test case

| Ưu tiên | Số lượng |
|---|---:|
| P0 | 17 |
| P1 | 13 |
| P2 | 7 |
| Tổng | 37 |

| Module | Số test case |
|---|---:|
| Meeting | 5 |
| Import document | 6 |
| Deploy config | 5 |
| AI analytics | 5 |
| Group AI | 5 |
| Auth/permission | 6 |
| Regression core | 5 |

## 10. Rủi ro kiểm thử và cách ghi nhận

| Rủi ro | Ảnh hưởng | Cách xử lý trong test plan |
|---|---|---|
| Thiếu Docker hoặc Docker không chạy được SQL/Redis | Không chạy được app đầy đủ, integration, E2E, deploy smoke | Đánh `Blocked` cho case phụ thuộc Docker; lưu log Docker/port conflict; không đánh fail chức năng nếu môi trường thiếu. |
| Thiếu AI provider hoặc API key | AI analytics/Group AI có thể chỉ fallback/mock hoặc lỗi provider | Chạy kỳ vọng fallback an toàn; chỉ fail nếu app crash, UI treo, leak dữ liệu hoặc không có thông báo lỗi. |
| Thiếu Ollama/Qdrant | RAG/vector/AI local không chạy thật | Ghi `Blocked` cho phần RAG thật; vẫn chạy smoke endpoint/UI nếu fallback có sẵn. |
| SignalR/Redis backplane không hoạt động | Meeting realtime, group chat/poll realtime có thể không cập nhật tức thời | Chạy manual 2 tab; nếu API đúng nhưng realtime lỗi thì ghi rõ P1/P0 tùy flow demo bị ảnh hưởng. |
| Thiếu file mẫu DOCX/PDF/ZIP nhỏ | Import document khó test ổn định | Dùng `ProjectHub.docx` tạm cho DOCX; yêu cầu VM/QT cung cấp bộ file nhỏ; PDF chỉ positive khi parser đã merge. |
| Dữ liệu seed không đúng mật khẩu | Không login được admin/member | Kiểm tra `QALY_SEED_ADMIN_PASSWORD`, `QALY_SEED_DEFAULT_USER_PASSWORD`; nếu DB đã seed với mật khẩu cũ thì reset DB hoặc cung cấp password hiện tại. |
| Tính năng đang chờ PR/merge | Test case có thể chưa chạy được | Đánh `Blocked` hoặc `N/A`; không ghi pass giả và không mô tả như chức năng đã hoàn thiện. |

## 11. Dữ liệu/môi trường cần người khác cung cấp

| Cần cung cấp | Người/nhóm liên quan | Lý do |
|---|---|---|
| Bộ file import nhỏ: DOCX, PDF, ZIP, CSV, XLSX | Người phụ trách import document / QA | Giúp test ổn định, tránh phụ thuộc file lớn `ProjectHub.docx`. |
| Quyết định trạng thái PDF parser trong tuần | Quang Tuấn / Viết Minh | Test PDF P0 cần biết kỳ vọng là positive hay unsupported rõ ràng. |
| AI provider/key hoặc xác nhận dùng fallback | Quốc Bảo / Chí Khang | Chốt kỳ vọng AI analytics/Group AI. |
| 20 prompt evaluation và dữ liệu AI demo | Quốc Bảo / Chí Khang | Phục vụ AI demo/evaluation cuối tuần. |
| Môi trường Docker chạy ổn định | Nhóm deploy / người chạy QA | Cần cho deploy smoke, integration và E2E. |
| Xác nhận phương án meeting thật/realtime | Quang Tuấn / Gia Long | Chốt kỳ vọng P0 cho meeting provider, SignalR và screen share. |

## 12. Lịch chạy đề xuất

| Ngày | Trọng tâm |
|---|---|
| 03/06/2026 - 04/06/2026 | Chốt test plan DH-01; chuẩn bị account, group, project, file mẫu. |
| 05/06/2026 | Chạy manual QA Meeting và Import document; ghi blocker P0/P1. |
| 06/06/2026 | Chạy Deploy config, Auth/permission, API regression. |
| 07/06/2026 | Chạy AI analytics và Group AI theo provider/fallback thực tế. |
| 08/06/2026 | Chạy E2E/regression tổng; xác nhận bugfix P0/P1. |
| 09/06/2026 | Tổng hợp evidence, báo cáo test DH-05 và checklist nghiệm thu. |
