# 📋 Báo cáo Đồng bộ Tiến độ & Kế hoạch Phân công Nhiệm vụ Nhóm (Group Workflow Sync & Tasks)

> **Lưu ý:** Đây là snapshot phân công/QA trước các phase ổn định hóa ngày 08/06/2026. Trạng thái hiện tại nằm tại [13_Project_Status_2026-06-08.md](./13_Project_Status_2026-06-08.md); không dùng các tỷ lệ và bug mở trong file này làm kết luận mới nhất.

Tài liệu này tổng hợp toàn bộ hiện trạng mã nguồn thực tế của dự án QALY, đối chiếu chi tiết với kế hoạch tuần của nhóm (`10_group_workflow_week_plan.xlsx`), và thiết lập hướng dẫn phân công nhiệm vụ cụ thể cho từng thành viên.

Tài liệu này dùng làm cơ sở tham khảo nội bộ và giao việc. Khi dùng cho nghiệm thu, phải đọc kèm bằng chứng QA mới nhất trong `docs/task/qa-evidence/` và báo cáo kiểm thử cuối tuần.

---

## I. HIỆN TRẠNG TIẾN ĐỘ THỰC TẾ & KHÓA PHẠM VI (REALITY CHECK)

### 1. Scope thời gian và Kiến trúc Cơ sở dữ liệu:

- **Mục tiêu thời hạn:** Dự án hướng tới hoàn thành trong 10 tuần theo kế hoạch. Mức độ nghiệm thu thực tế phụ thuộc bằng chứng kiểm thử và các bug còn mở.
- **Khóa phạm vi dữ liệu (Scope Lock v3.2):** Để đảm bảo dự án chạy mượt mà trên VPS 8GB và không bị vỡ tiến độ, tài liệu đặc tả [QALY_MVP_P0_Scope_Lock_v3.2.md](file:///C:/Users/Lenovo/Documents/Qaly/Qaly_project/QALY_Docs_v3.2_Gap_Closure_Proceed_Ready/QALY_Docs_v2.3_Fixed_QA/QALY_MVP_P0_Scope_Lock_v3.2.md) đã chính thức khóa scope P0 yêu cầu tối thiểu **21 bảng cơ sở dữ liệu**.
- **Trạng thái code hiện tại:** Đã có cấu trúc dữ liệu và migration cho nhiều module, bao gồm nhóm, chat thời gian thực, bình chọn, cuộc họp và audit log. Không ghi nhận là “100% database hoàn thiện” nếu chưa có đối chiếu nghiệm thu đầy đủ cho từng use case.

### 2. Trạng thái kiểm thử tự động (Test Verification):

- Số liệu test cũ trong tài liệu này không thay thế kết quả QA hiện tại.
- Theo DH-04, backend build pass 0 warning/0 error; integration filter pass 14/14; unit filter pass 92/92 cho các nhóm Import/Meeting/Auth/AI liên quan.
- Kết quả test xanh không đồng nghĩa mọi luồng UI/realtime đã nghiệm thu. DH-03 vẫn ghi nhận lỗi P0 participant realtime/count trong cuộc họp.

---

## II. ĐỐI CHIẾU CHI TIẾT VỚI FILE EXCEL (`10_group_workflow_week_plan.xlsx`)

Mã nguồn đã có nền backend và dữ liệu cho nhiều Epic từ G0 đến G10. Phần còn lại cần được đánh giá theo bằng chứng QA, đặc biệt với các luồng UI/thời gian thực và AI phụ thuộc provider/fallback.

### 1. Epic G0: Scope & Architecture (Owner: Quang Tuấn)

- **Trạng thái thực tế:** Có nền Clean Architecture và API contract chính.
- **Kết quả:** Cấu trúc dự án đang theo Clean Architecture; cần tiếp tục đối chiếu với checklist nghiệm thu trước khi claim hoàn tất toàn bộ.

### 2. Epic G1: Backend Group Core (Owner: Quang Minh)

- **Trạng thái thực tế:** Có backend core cho nhóm.
- **Kết quả:** Entity `WorkGroup` và `WorkGroupMember` đã migrate. APIs Tạo nhóm (`POST /api/groups`), Danh sách nhóm (`GET /api/groups`), và Chi tiết nhóm (`GET /api/groups/{id}`) đã có trong backend; trạng thái nghiệm thu cần đọc kèm checklist hiện tại.

### 3. Epic G2: Invite & Notification (Owner: Duy Hoàng)

- **Trạng thái thực tế:** **Đạt 90%**.
- **Kết quả:**
    - Entity `GroupInvitation` và các APIs mời thành viên (`POST /api/groups/{id}/invitations`), accept/reject invitation đã có trong backend.
    - API quản lý member (đổi role, xóa member) đã viết xong.
    - **Còn thiếu (Gia Long làm ở UI):** Giao diện popup mời thành viên và nút đổi role/xóa member.

### 4. Epic G3: Realtime Chat (Owner: Quang Tuấn & Quang Minh)

- **Trạng thái thực tế:** **Đạt 95%**.
- **Kết quả:**
    - SignalR `GroupHub` có xác thực và cô lập nhóm (chặn cross-group) hoạt động tốt.
    - API lấy lịch sử tin nhắn chat nhóm (`GET /api/groups/{id}/messages`) đã hoàn thành.
    - **Trì hoãn (Scope Freeze):** Tính năng typing indicator và read receipt (`G3-04`, `G3-05`) được đóng băng tạm thời để tối ưu hiệu năng VPS 8GB.

### 5. Epic G4: Poll & Vote (Owner: Duy Hoàng)

- **Trạng thái thực tế:** **Đạt 85%**.
- **Kết quả:**
    - Entities `GroupPoll`, `GroupPollOption`, `GroupPollVote` đã migrate.
    - APIs Tạo Poll (`POST /api/groups/{id}/polls`), Vote và xem kết quả bình chọn đã hoàn tất.
    - **Hoàn thành (Đoàn Trung):** Card bình chọn tương tác động trên UI (Vue).

### 6. Epic G5: Meeting & Screen Share (Owner: Quang Minh & Quốc Bảo)

- **Trạng thái thực tế:** Backend cuộc họp có API và regression tests; UI realtime còn rủi ro.
- **Kết quả:**
    - Đã có bộ 3 APIs nghiệp vụ họp trực tuyến nhúng Jitsi Meet: **Bắt đầu cuộc họp** (`POST /api/groups/{id}/meetings/start`), **Tham gia cuộc họp** (`POST /api/groups/{groupId}/meetings/{meetingId}/join`), và **Kết thúc cuộc họp** (`POST /api/groups/{groupId}/meetings/{meetingId}/end`).
    - API Link Meeting Summary và Transcript (`G5-05`) của Quốc Bảo đã được tích hợp hook lưu trữ an toàn.
    - **Giới hạn QA hiện tại:** Participant realtime/count đang có bug P0 `DH03-BUG-MTG-001`; screen share unsupported chưa có UI feedback rõ (`DH03-BUG-MTG-002`, P2). Không demo participant count/list hoặc screen share như tính năng ổn định nếu chưa fix/xác minh lại.

### 7. Epic G6: Frontend Group Page (Owner: Gia Long & Đoàn Trung)

- **Trạng thái thực tế:** **Đạt 65%**.
- **Kết quả:** Đã có khung màn hình `TeamsPage.vue` và realtime chat.
- **Nhiệm vụ trọng tâm:** Ghép nối các giao diện mời thành viên, card bình chọn động, tab nhúng iframe họp trực tuyến với các endpoints backend thực tế.

### 8. Epic G7: Create Project From Group (Owner: Quang Minh & Gia Long)

- **Trạng thái thực tế:** Có backend API tạo dự án từ nhóm.
- **Kết quả:** API tạo dự án từ nhóm (`POST /api/groups/{id}/create-project`) và map tự động các role đã có; cần kiểm thử theo checklist nếu đưa vào nghiệm thu.

### 9. Epic G8: AI Group Workflow (Owner: Quốc Bảo & Chí Khang)

- **Trạng thái thực tế:** Có backend/service và unit tests liên quan; chưa có bằng chứng manual/E2E đầy đủ cho nghiệm thu Group AI.
- **Kết quả:**
    - APIs AI Tóm tắt thảo luận (`POST /api/groups/{groupId}/ai/summary`), sinh Dự thảo dự án/công việc nháp từ thảo luận chat (`POST /api/groups/{groupId}/ai/draft-project`), và trích xuất action items từ chat (`POST /api/groups/{groupId}/ai/action-items`) đã có trong phạm vi backend/service.
    - Khi demo cần ghi rõ AI có thể dùng provider thật hoặc phản hồi dự phòng AI tùy cấu hình; chưa claim Group AI manual/E2E đã nghiệm thu đầy đủ.

### 10. Epic G9 & G10: Testing, QA & Demo (Owner: Toàn đội)

- **Trạng thái thực tế:** Có test tự động và báo cáo QA tuần. Demo/UAT cần bám checklist nghiệm thu hiện tại, không mặc định tất cả Pass.
- **Kết quả QA liên quan:** Tổng thực thi có bằng chứng: Pass 118, Fail 3, Blocked 0, Not Tested 0. Lỗi P0 còn mở: `DH03-BUG-MTG-001`.

---

## III. PHÂN CÔNG NHIỆM VỤ CHI TIẾT (ASSIGNMENT BACKLOG)

Dưới đây là backlog nhiệm vụ cụ thể giao cho từng thành viên để ghép nối Frontend Vue UI với các API backend hiện có:

### 1. GIA LONG (Frontend Lead)

- **Task G6-05: Modal mời thành viên qua Email**
    - _Mục tiêu:_ Thiết kế popup/modal mời thành viên mới.
    - _Yêu cầu kỹ thuật:_ Nhập email -> Bấm Gửi -> Gọi API `POST /api/groups/{groupId}/invitations` -> Hiển thị danh sách pending invitations.
- **Task G6-06: UI Quản lý thành viên & Đổi vai trò (Role)**
    - _Mục tiêu:_ Cho phép Owner/Admin quản lý danh sách thành viên nhóm.
    - _Yêu cầu kỹ thuật:_ Render danh sách member kèm theo avatar, joined date, và role hiện tại. Owner/Admin có nút chỉnh sửa role (Owner/Admin/Member) gọi API `PUT /api/groups/{groupId}/members/{userId}` hoặc xóa member gọi API `DELETE`.

### 2. ĐOÀN TRUNG (Frontend & QA)

- **Task G6-07: Nâng cấp Polls thành Card tương tác động (Dynamic Vote Cards)**
    - _Mục tiêu:_ Thay thế hiển thị văn bản thô của Poll bằng một widget bình chọn sinh động.
    - _Yêu cầu kỹ thuật:_ Khi tin nhắn có loại là `'Poll'`, render một card hiển thị câu hỏi và danh sách các phương án có nút radio/checkbox. Bấm bình chọn sẽ gọi API `POST /api/groups/{groupId}/polls/{pollId}/vote` và cập nhật realtime biểu đồ % phiếu bầu của mỗi phương án qua SignalR.
    - _Trạng thái:_ Hoàn thành.
- **Task G6-08: Tích hợp tab cuộc họp Jitsi Meeting Iframe**
    - _Mục tiêu:_ Cho phép người dùng bắt đầu và tham gia họp trực tuyến ngay trong giao diện nhóm.
    - _Yêu cầu kỹ thuật:_ Thêm Tab phụ "Meetings" bên cạnh tab "Tin nhắn". Khi bấm "Bắt đầu họp" -> Gọi API `POST /api/groups/{id}/meetings/start` để lấy `JoinUrl` -> Nhúng thẻ `<iframe src="https://meet.jit.si/{RoomId}" allow="camera; microphone; display-capture"></iframe>` chạy trực tiếp. Có nút "Kết thúc cuộc họp" gọi API `end`.
    - _Trạng thái:_ Hoàn thành.

### 3. CHÍ KHANG (AI Dev)

- **Task G8-05: Ghép nút bấm AI Summarize & AI Draft Project vào UI**
    - _Mục tiêu:_ Cung cấp cho người dùng khả năng kích hoạt trợ lý AI của Quốc Bảo.
    - _Yêu cầu kỹ thuật:_
        1.  **Nút tóm tắt thảo luận (Summarize):** Bấm nút -> Gọi API `POST /api/groups/{groupId}/ai/summary` -> Hiển thị popup chứa: Đoạn tóm tắt, Quyết định chính (Key Decisions) và Câu hỏi chưa giải quyết (Unresolved Questions).
        2.  **Nút tạo Kế hoạch nháp (Draft Project):** Bấm nút -> Gọi API `POST /api/groups/{groupId}/ai/draft-project` -> Render danh sách task nháp đẹp mắt (kèm Priority, Estimate Days, Owner gợi ý) có checkbox cho phép PM tích chọn xác nhận trước khi tạo Project thật.

### 4. DUY HOÀNG (Backend Expansion) & QUANG MINH (Backend Core)

- **Hỗ trợ Frontend:** Sẵn sàng kiểm tra, debug dữ liệu và phối hợp cùng Gia Long, Đoàn Trung trong quá trình tích hợp APIs.
- **Bảo vệ mã nguồn:** Không tự ý sửa đổi cấu trúc database hoặc sửa các class Service cốt lõi mà không chạy kiểm thử tự động `dotnet test` trước để đảm bảo an toàn tuyệt đối.

---

## IV. HƯỚNG DẪN KẾT NỐI API & KIỂM THỬ (BASE CODE REFERENCE)

Mọi endpoint và DTO phục vụ cho quá trình ghép nối frontend đều đã được code thật và test xanh. Dưới đây là các file nghiệp vụ cụ thể làm tài liệu tham khảo:

### 1. APIs Group Meeting (`G5-03`):

- **Service Class:** [GroupsService.cs](file:///C:/Users/Lenovo/Documents/Qaly/Qaly_project/src/Qaly.Application/Services/GroupsService.cs#L1631-L1745)
- **Controller Route:** [GroupsController.cs](file:///C:/Users/Lenovo/Documents/Qaly/Qaly_project/src/Qaly.Web/Controllers/GroupsController.cs#L157-L175)
    - `POST /api/groups/{id}/meetings/start`
    - `POST /api/groups/{groupId}/meetings/{meetingId}/join`
    - `POST /api/groups/{groupId}/meetings/{meetingId}/end`
- **Unit Tests bảo chứng:** [GroupsServiceTests.cs](file:///C:/Users/Lenovo/Documents/Qaly/Qaly_project/tests/Qaly.UnitTests/GroupsServiceTests.cs#L1691-L1763)

### 2. APIs AI Summaries & Draft Projects (`G8-02`, `G8-04`):

- **Service Class:** [GroupAiService.cs](file:///C:/Users/Lenovo/Documents/Qaly/Qaly_project/src/Qaly.Application/Services/GroupAiService.cs)
- **Controller Route:** [GroupAiController.cs](file:///C:/Users/Lenovo/Documents/Qaly/Qaly_project/src/Qaly.Web/Controllers/GroupAiController.cs)
    - `POST /api/groups/{groupId}/ai/summary`
    - `POST /api/groups/{groupId}/ai/draft-project`
- **Unit Tests bảo chứng:** [GroupAiServiceTests.cs](file:///C:/Users/Lenovo/Documents/Qaly/Qaly_project/tests/Qaly.UnitTests/GroupAiServiceTests.cs)

### 3. Cách chạy kiểm thử tự động hệ thống:

Trước khi commit code mới lên GitHub, đề nghị thành viên chạy lệnh sau ở root folder để đảm bảo không phá vỡ logic cũ:

```bash
dotnet test
```

---

## V. HƯỚNG DẪN TRÌNH BÀY TRƯỚC GIẢNG VIÊN (STAGE STRATEGY)

Để trình bày trung thực và tránh nói quá phạm vi đã kiểm thử:

1.  **Hồ sơ báo cáo tuần:** Báo cáo tiến độ hoàn toàn tuân thủ theo đúng WBS đặc tả v3.2 (Báo cáo hoàn thành Core Skeleton, Auth, RBAC đúng tuần).
2.  **Buổi demo trực tiếp:** Ưu tiên demo các luồng có bằng chứng pass: nhập tài liệu, login/phân quyền cơ bản, E2E smoke và backend regression.
3.  **Cuộc họp nhóm:** Chỉ demo start/join/end và phân quyền nếu cần. Không demo participant realtime/count như tính năng ổn định khi bug P0 chưa fix.
4.  **AI:** Nếu demo AI analytics/Group AI, nói rõ đây là luồng phụ thuộc provider/local config và có phản hồi dự phòng AI; không claim RAG/tooling đầy đủ nếu chưa có evidence riêng.
5.  **Deploy:** Nếu chưa chạy checklist deploy, ghi rõ “chưa có bằng chứng nghiệm thu đầy đủ”.

Thông điệp nên dùng: hệ thống có nền tảng backend và QA regression tốt, nhưng một số luồng thời gian thực/AI/deploy vẫn cần fix hoặc xác minh thêm trước khi claim nghiệm thu đầy đủ.
