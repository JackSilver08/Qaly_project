# KẾ HOẠCH PHÂN CÔNG CÔNG VIỆC TUẦN NÀY - DỰ ÁN QALY

**Tuần làm việc:** 03/06/2026 - 09/06/2026  
**Nhóm trưởng / Code chính:** Quang Tuấn

### Định hướng mới

- Giảm số người trực tiếp code core.
- Tập trung nhân sự phù hợp vào: phần còn thiếu, AI nâng cao, kiểm thử và tài liệu nghiệm thu.

### Ghi chú hiển thị

CSV không lưu cỡ chữ. Khi mở bằng Excel/Google Sheets nên:

- Đặt font 12-13
- Bật **Wrap text**
- Tăng **row height** 32-48
- **Freeze** hàng tiêu đề

---

## NHÓM 1 - CODE CHÍNH VÀ ĐIỀU PHỐI

| Mã việc | Ngày bắt đầu | Deadline | Thành viên | Vai trò tuần này        | Nhóm việc          | Công việc chi tiết                                                  | Khu vực/File liên quan                                  | Deliverable cần nộp                 | Tiêu chí hoàn thành                                             | Phụ thuộc    | Ưu tiên | Trạng thái |
| ------- | ------------ | -------- | ---------- | ----------------------- | ------------------ | ------------------------------------------------------------------- | ------------------------------------------------------- | ----------------------------------- | --------------------------------------------------------------- | ------------ | ------- | ---------- |
| QT-01   | 03/06        | 03/06    | Quang Tuấn | Nhóm trưởng, code chính | Điều phối sprint   | Chốt phạm vi tuần này: không mở rộng core mới, chỉ xử lý phần thiếu | docs/task/...csv                                        | Scope note + checklist              | Cả nhóm hiểu rõ ai làm gì, không trùng việc                     | -            | P0      | Planned    |
| QT-02   | 03/06        | 04/06    | Quang Tuấn | Nhóm trưởng, code chính | Kiến trúc backend  | Review hướng triển khai meeting thật (WebRTC/Jitsi/LiveKit)         | GroupMeetingPage.vue, GroupHub.cs, GroupsService.cs     | Decision note + khung kỹ thuật      | Có phương án rõ ràng cho meeting thật                           | -            | P0      | Planned    |
| QT-03   | 04/06        | 05/06    | Quang Tuấn | Code chính              | Backend blocker    | Triển khai/review room lifecycle, quyền join/end, realtime event    | GroupsController.cs, GroupsService.cs, GroupHub.cs      | Backend meeting flow ổn định        | User ngoài group bị chặn, member join được, owner kết thúc được | QT-02        | P0      | Planned    |
| QT-04   | 04/06        | 06/06    | Quang Tuấn | Code chính              | Import nâng cao    | Review hướng import PDF/DOCX/ZIP, parser tối thiểu                  | FileImportService.cs, ImportController.cs, Import\*.vue | Quyết định kỹ thuật + PR review     | Không còn mismatch giữa UI và backend                           | -            | P0      | Planned    |
| QT-05   | 05/06        | 07/06    | Quang Tuấn | Code chính              | Review AI nâng cao | Code review AI của Quốc Bảo & Chí Khang                             | AiGateway.cs, AiService.cs, AnalyticsPage.vue           | Review comments + merge blocker fix | Không merge AI chỉ rule cứng                                    | QB-01, CK-01 | P0      | Planned    |
| QT-06   | 06/06        | 08/06    | Quang Tuấn | Code chính              | Deploy readiness   | Sửa cấu hình Docker & appsettings production                        | docker-compose.yml, appsettings\*.json                  | Cấu hình deploy sạch hơn            | Container kết nối đúng service                                  | GL-05, VM-05 | P0      | Planned    |
| QT-07   | 08/06        | 09/06    | Quang Tuấn | Tổng kiểm tra           | Final integration  | Build/test cuối tuần, review PR, gom backlog                        | Toàn bộ repo                                            | Final verification log              | Build & test pass                                               | DH-05, DT-05 | P0      | Planned    |

---

## NHÓM 2 - LÀM HẾT PHẦN CÒN THIẾU

**Gia Long**

| Mã việc | Ngày bắt đầu | Deadline | Công việc chi tiết                  | Khu vực/File                       | Deliverable               | Tiêu chí hoàn thành                             | Phụ thuộc    | Ưu tiên |
| ------- | ------------ | -------- | ----------------------------------- | ---------------------------------- | ------------------------- | ----------------------------------------------- | ------------ | ------- |
| GL-01   | 03/06        | 04/06    | Nghiên cứu phương án meeting thật   | GroupMeetingPage.vue, GroupHub.cs  | Bảng so sánh + đề xuất    | Có đề xuất rõ ràng                              | QT-02        | P0      |
| GL-02   | 04/06        | 06/06    | Triển khai UI meeting               | GroupMeetingPage.vue               | Meeting UI có remote area | Hỗ trợ multi-tab participant                    | GL-01        | P0      |
| GL-03   | 05/06        | 07/06    | Realtime meeting events             | GroupMeetingPage.vue, GroupHub.cs  | Realtime behavior         | Join/leave/update participant không cần refresh | QT-03, GL-02 | P0      |
| GL-04   | 06/06        | 08/06    | UI/UX polish (tiếng Việt nhất quán) | ClientApp/pages & components       | Patch UI text             | Không còn text lẫn Anh/Việt                     | -            | P1      |
| GL-05   | 06/06        | 08/06    | Deploy UI support                   | README.md, docs/06_Docker_Setup.md | Deploy frontend note      | Hướng dẫn build frontend rõ ràng                | QT-06        | P1      |
| GL-06   | 08/06        | 09/06    | Fix bug theo test                   | Issue list QA                      | Bugfix PR                 | Xử lý bug P0/P1                                 | DH-03, DT-03 | P0      |

**Viết Minh**

| Mã việc | Ngày bắt đầu | Deadline | Công việc chi tiết      | Khu vực/File                              | Deliverable              | Tiêu chí hoàn thành                          | Phụ thuộc    | Ưu tiên |
| ------- | ------------ | -------- | ----------------------- | ----------------------------------------- | ------------------------ | -------------------------------------------- | ------------ | ------- |
| VM-01   | 03/06        | 04/06    | Sửa mismatch import UI  | Import\*.vue                              | UI import rõ trạng thái  | Không còn mâu thuẫn acceptedTypes vs backend | QT-04        | P0      |
| VM-02   | 04/06        | 06/06    | Parser DOCX/PDF phase 1 | FileImportService.cs, ImportController.cs | Parser phase 1           | Import file mẫu thành công                   | QT-04        | P0      |
| VM-03   | 05/06        | 07/06    | Import ZIP phase 1      | FileImportService.cs, ImportModal.vue     | ZIP import plan hoặc MVP | ZIP chứa .md/.txt tạo nhiều page             | VM-02        | P1      |
| VM-04   | 06/06        | 08/06    | Frontend typecheck      | package.json, tsconfig.json               | npm run typecheck        | Có script typecheck và pass                  | -            | P0      |
| VM-05   | 06/06        | 08/06    | Docker & appsettings    | docker-compose.yml, .env.example          | Docker compose fix       | Không còn hardcode DB Windows                | QT-06        | P0      |
| VM-06   | 08/06        | 09/06    | Fix theo test           | QA issue list                             | Bugfix PR                | Xử lý lỗi import/deploy                      | DH-04, DT-04 | P0      |

---

## NHÓM 3 - AI NÂNG CAO

**Quốc Bảo**

| Mã việc | Ngày bắt đầu | Deadline | Công việc chi tiết | Deliverable                       | Tiêu chí hoàn thành               | Phụ thuộc    | Ưu tiên |
| ------- | ------------ | -------- | ------------------ | --------------------------------- | --------------------------------- | ------------ | ------- |
| QB-01   | 03/06        | 04/06    | AI Gateway thật    | AI Gateway technical plan         | Có flow chi tiết                  | -            | P0      |
| QB-02   | 04/06        | 06/06    | Provider routing   | Provider routing PR               | Log thấy provider/model được dùng | QB-01        | P0      |
| QB-03   | 05/06        | 07/06    | RAG thật           | RAG/tool flow có test             | Trả lời dựa trên dữ liệu thật     | QB-02        | P0      |
| QB-04   | 06/06        | 08/06    | Schema validation  | Schema validation helper          | AI output sai format không vỡ UI  | QB-03        | P0      |
| QB-05   | 07/06        | 09/06    | Evaluation dataset | AI evaluation checklist + dataset | Ít nhất 20 prompt mẫu             | CK-03        | P1      |
| QB-06   | 08/06        | 09/06    | Fix AI blocker     | Bugfix PR                         | Luồng AI P0 chạy được trong demo  | QT-05, DH-05 | P0      |

**Chí Khang**

| Mã việc | Ngày bắt đầu | Deadline | Công việc chi tiết    | Deliverable                 | Tiêu chí hoàn thành                | Phụ thuộc    | Ưu tiên |
| ------- | ------------ | -------- | --------------------- | --------------------------- | ---------------------------------- | ------------ | ------- |
| CK-01   | 03/06        | 04/06    | Audit trang Analytics | Audit note trang phân tích  | Phân loại rule-based vs AI thật    | -            | P0      |
| CK-02   | 04/06        | 06/06    | Chat memory           | Chat history MVP            | Hỏi nối tiếp hiểu ngữ cảnh         | CK-01, QB-02 | P0      |
| CK-03   | 05/06        | 07/06    | Structured analytics  | AI structured response thật | Ít nhất 3 loại block từ AI thật    | CK-02, QB-03 | P0      |
| CK-04   | 06/06        | 08/06    | Group AI nâng cao     | Group AI output giàu hơn    | Có source, confidence, suggestion  | QB-04        | P0      |
| CK-05   | 07/06        | 09/06    | AI UX safety          | AI UX polish                | Có trạng thái, provenance, warning | CK-03, CK-04 | P1      |
| CK-06   | 08/06        | 09/06    | Demo AI               | AI demo script              | 5 câu hỏi demo sẵn sàng            | QB-05        | P1      |

---

## NHÓM 4 - KIỂM THỬ VÀ TÀI LIỆU

**Duy Hoàng**

| Mã việc | Ngày bắt đầu | Deadline | Công việc chi tiết       | Deliverable        | Tiêu chí hoàn thành             |
| ------- | ------------ | -------- | ------------------------ | ------------------ | ------------------------------- |
| DH-01   | 03/06        | 04/06    | Test plan tổng           | Test plan chi tiết | Có test case P0/P1              |
| DH-02   | 04/06        | 06/06    | E2E Playwright           | E2E test PR        | Ít nhất 3 flow mới              |
| DH-03   | 05/06        | 07/06    | Manual QA meeting/import | QA evidence log    | Có bằng chứng + mức độ lỗi      |
| DH-04   | 06/06        | 08/06    | API regression           | Backend test PR    | Test user permission, AI schema |
| DH-05   | 08/06        | 09/06    | Báo cáo test             | Báo cáo kiểm thử   | Đầy đủ cho demo                 |
| DH-06   | 08/06        | 09/06    | Hỗ trợ tài liệu          | Review tài liệu    | Tài liệu đúng thực tế           |

**Đoàn Trung**

| Mã việc | Ngày bắt đầu | Deadline | Công việc chi tiết            | Deliverable              | Tiêu chí hoàn thành                   |
| ------- | ------------ | -------- | ----------------------------- | ------------------------ | ------------------------------------- |
| DT-01   | 03/06        | 04/06    | Tài liệu kiến trúc hiện trạng | Architecture update      | Hiểu hệ thống hiện tại                |
| DT-02   | 04/06        | 06/06    | API documentation             | API docs update          | Mô tả quyền, payload, lỗi             |
| DT-03   | 05/06        | 07/06    | User guide                    | User guide tiếng Việt    | Hướng dẫn sử dụng các chức năng chính |
| DT-04   | 06/06        | 08/06    | Demo script                   | Demo script hoàn chỉnh   | Kịch bản 8-10 phút                    |
| DT-05   | 07/06        | 09/06    | Báo cáo tiến độ               | Báo cáo tiến độ          | Trung thực, có đối chiếu Git & test   |
| DT-06   | 08/06        | 09/06    | Checklist nghiệm thu          | Acceptance checklist CSV | Checklist chi tiết từng module        |

---

## TỔNG KẾT PHÂN VAI

- **Quang Tuấn**: Điều phối tổng thể, review PR, quyết định kỹ thuật, final integration.
- **Gia Long + Viết Minh**: Hoàn thiện phần còn thiếu (Meeting thật, Import document, UI/UX, Deploy).
- **Quốc Bảo + Chí Khang**: Nâng cấp AI lên mức có RAG, Tool calling, Provider thật, Schema validation.
- **Duy Hoàng + Đoàn Trung**: Kiểm thử (E2E + Manual) và Tài liệu (User guide, API docs, Demo script, Báo cáo).

**Mục tiêu cuối tuần:** Có thể demo ổn định các tính năng chính: Meeting, Import document, AI Analytics/Group AI có dữ liệu thật, và tài liệu đầy đủ.

---

**File đã sẵn sàng.** Bạn chỉ cần copy toàn bộ nội dung trên và lưu thành `ke-hoach-tuan-2026-06-03.md`.
Bạn có muốn tôi chỉnh thêm phần nào (ví dụ: thêm bảng tóm tắt theo ngày, hoặc theo ưu tiên P0) không?
