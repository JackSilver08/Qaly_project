# Kế hoạch công việc Qaly cho 7 người

**Thời gian:** 28/07/2026 – 03/08/2026  
**Căn cứ:** Báo cáo thay đổi tích cực tuần 21–27/07/2026 và trạng thái sẵn sàng phát hành hiện tại.  
**Mục tiêu tuần:** Hoàn thiện một bản **Release Candidate (RC)** ổn định, an toàn cho nhiều tổ chức, có AI kiểm soát được, giao diện nhất quán và đủ bằng chứng kiểm thử để demo/nghiệm thu.

## 1. Kết quả bắt buộc cuối tuần

1. Không có lỗi P0/P1 trong các luồng đăng nhập, tổ chức, dự án, thành viên và phân quyền.
2. Dữ liệu của hai tổ chức được cách ly ở API, dịch vụ nền và giao diện.
3. Các luồng AI tiến độ/kỹ năng có nguồn, schema, giới hạn ngân sách, timeout và fallback rõ ràng.
4. Các màn hình chính dùng tốt trên desktop và mobile, có loading/empty/error/success state.
5. Toàn bộ gate build, typecheck, unit, integration và E2E quan trọng đều xanh.
6. Có bản RC chạy trên môi trường staging hoặc môi trường tương đương, kèm biên bản backup/restore và rollback.
7. Có release note, danh sách giới hạn còn lại và quyết định Go/No-Go.

## 2. Thứ tự ưu tiên

| Mức | Phạm vi |
|---|---|
| P0 | Rò rỉ dữ liệu liên tổ chức, vượt quyền, mất dữ liệu, không đăng nhập/không khởi động được hệ thống |
| P1 | Luồng tổ chức/dự án/nhiệm vụ/AI chính sai hoặc không hoàn tất; build/test/release gate đỏ |
| P2 | Responsive, accessibility, thông báo và tính nhất quán giao diện |
| Chưa làm tuần này | Mở rộng RAG, PWA, báo cáo nâng cao hoặc tính năng mới chưa phục vụ trực tiếp RC |

## 3. Phân công theo thành viên

### 1. Quang Tuấn — Tech Lead, tích hợp và phát hành

**Trọng tâm:** Khóa phạm vi RC, xử lý rủi ro kiến trúc và điều phối tích hợp.

- Rà soát ranh giới quyền hệ thống → tổ chức → dự án → capability; chốt ma trận quyền chuẩn.
- Review các thay đổi liên quan auth, tenant isolation, migration và AI gateway.
- Chuẩn hóa branch/PR, yêu cầu evidence và checklist merge; xử lý xung đột tích hợp hằng ngày.
- Điều phối bug triage; quyết định P0/P1 và người xử lý.
- Chủ trì kiểm tra release, gắn nhãn RC và họp Go/No-Go.

**Đầu ra/DoD:**

- Ma trận quyền được cả Backend, QA và Frontend dùng chung.
- 100% PR rủi ro cao có review; không merge khi gate bắt buộc đỏ.
- Có RC, release note, danh sách known issues và quyết định Go/No-Go.

### 2. Duy Hoàng — Backend, đa tổ chức và tính toàn vẹn dữ liệu

**Trọng tâm:** Làm kín toàn bộ đường dữ liệu theo tổ chức.

- Audit controller/service/query/job nền để bảo đảm mọi truy vấn nghiệp vụ đều bị giới hạn theo tổ chức và dự án.
- Hoàn thiện vòng đời tổ chức/thành viên: thêm, đổi vai trò, gỡ, vô hiệu hóa, chuyển chủ sở hữu và thu hồi session.
- Bổ sung validation, concurrency handling, audit log và lỗi nghiệp vụ dễ hiểu cho các thao tác quan trọng.
- Kiểm tra migration/idempotency và seed dữ liệu cho ít nhất hai tổ chức độc lập.
- Sửa các lỗi backend P0/P1 do QA phát hiện.

**Đầu ra/DoD:**

- Không đọc/ghi chéo dữ liệu giữa hai tổ chức trong test tích hợp.
- API trả đúng `401/403/404/409`, không lộ chi tiết nội bộ.
- Các thao tác nguy hiểm có transaction/audit và test hồi quy tương ứng.

### 3. Viết Minh — Frontend, UX và responsive

**Trọng tâm:** Hoàn thiện trải nghiệm của các luồng chính thay vì mở thêm màn hình.

- Chuẩn hóa loading, skeleton, empty, error, success và confirm dialog cho Tổ chức, Thành viên, Dự án, Nhiệm vụ và AI.
- Đồng bộ hiển thị quyền: ẩn/khóa đúng thao tác; xử lý `401/403/409` thân thiện nhưng không thay thế kiểm tra backend.
- Hoàn thiện responsive ở các mốc mobile/tablet/desktop cho sidebar, bảng, dialog và toolbar.
- Kiểm tra keyboard navigation, focus, label, contrast và thông báo cho screen reader ở luồng chính.
- Loại lỗi console, request lặp và trạng thái cũ sau mutation.

**Đầu ra/DoD:**

- Năm màn hình chính hoạt động ở 360 px, 768 px và desktop.
- Không có lỗi console trong E2E smoke; mọi mutation có phản hồi và khả năng retry phù hợp.
- Hoàn tất checklist accessibility cơ bản cho các luồng RC.

### 4. Đoàn Trung — QA, tự động hóa và release evidence

**Trọng tâm:** Xây lưới an toàn tập trung vào rủi ro cao.

- Lập traceability matrix từ yêu cầu RC tới test case.
- Bổ sung integration test cho tenant isolation, moderator hết hạn/thu hồi, đổi/gỡ thành viên, khóa tài khoản và concurrency.
- Bổ sung Playwright cho: đăng nhập → chọn tổ chức → tạo dự án → thêm thành viên → giao nhiệm vụ → xem AI → thu hồi quyền.
- Chạy regression hằng ngày trên commit tích hợp; ghi đầy đủ kết quả vào `QA_LOG.md`.
- Quản lý bug theo severity, evidence và trạng thái retest; tổng hợp báo cáo chất lượng cuối tuần.

**Đầu ra/DoD:**

- 100% luồng P0 và P1 có test tự động hoặc biên bản kiểm thử thủ công.
- Unit, integration, typecheck, build và E2E RC đều pass trên cùng commit.
- Không còn P0/P1 mở; lỗi P2 còn lại có owner và thời hạn.

### 5. Gia Long — DevOps, staging và khả năng phục hồi

**Trọng tâm:** Biến tài liệu vận hành thành bằng chứng chạy được.

- Cập nhật pipeline để build, test, scan, đóng image và lưu artifact/evidence theo commit.
- Chuẩn bị staging hoặc môi trường tương đương; cấu hình secret store, health/readiness check và log không lộ bí mật.
- Diễn tập deploy RC, backup, clean restore và rollback trên target đã chọn.
- Xác minh parity cấu hình Linux/container, Redis/session và hành vi khi AI provider/Redis suy giảm.
- Cập nhật deployment guide, runbook sự cố và checklist phát hành.

**Đầu ra/DoD:**

- RC image bất biến, truy vết được về commit và khởi động non-root.
- Có biên bản deploy/health/backup/restore/rollback kèm thời gian và kết quả.
- Không có secret trong repository, log hoặc artifact.

### 6. Quốc Bảo — AI Platform, độ tin cậy và chi phí

**Trọng tâm:** Ổn định nền tảng AI cho môi trường thật.

- Kiểm tra router/provider selection, timeout, retry có giới hạn, circuit breaker và fallback.
- Củng cố job idempotency, chống xử lý trùng, trạng thái thất bại và cơ chế retry/reconciliation.
- Xác minh budget theo tổ chức/người dùng/tính năng; số liệu usage/cost nhất quán khi request lỗi hoặc retry.
- Bảo vệ prompt/source, lọc dữ liệu nhạy cảm và bảo đảm log không chứa credential hoặc nội dung không cần thiết.
- Cung cấp dashboard/log tối thiểu cho latency, lỗi, token và chi phí.

**Đầu ra/DoD:**

- Test pass cho success, timeout, malformed output, hết ngân sách, provider down và duplicate job.
- Không vượt ngân sách do retry; mọi phản hồi AI truy vết được model/provider/source ở mức an toàn.
- AI hỏng không làm hỏng luồng quản lý dự án cốt lõi.

### 7. Chí Khang — AI Features, chất lượng đầu ra và trải nghiệm

**Trọng tâm:** Hoàn thiện hai tính năng AI đã có: tóm tắt tiến độ và gợi ý kỹ năng.

- Chuẩn hóa prompt và schema; làm rõ dữ kiện nguồn, độ mới và trường hợp không đủ dữ liệu.
- Tạo golden dataset gồm dự án bình thường, trễ hạn, thiếu dữ liệu, dữ liệu mâu thuẫn và nội dung tiếng Việt.
- Đánh giá tính đúng, hữu ích, có căn cứ và ổn định; sửa hallucination hoặc gợi ý quá chung.
- Hoàn thiện trạng thái UI/API: đang xử lý, hết ngân sách, timeout, lỗi provider, kết quả cũ và tạo lại.
- Phối hợp với Viết Minh để hiển thị nguồn/cảnh báo; phối hợp Quốc Bảo để đo usage/cost.

**Đầu ra/DoD:**

- Hai tính năng AI trả đúng schema trên toàn bộ golden dataset.
- Kết quả phân biệt rõ dữ kiện và đề xuất; không khẳng định khi thiếu căn cứ.
- Có bảng đánh giá trước/sau và ngưỡng chấp nhận được Tech Lead duyệt.

## 4. Lịch thực hiện và điểm bàn giao

| Ngày | Mục tiêu chung | Điểm bàn giao bắt buộc |
|---|---|---|
| 28/07 | Kickoff, khóa RC và baseline | Quang Tuấn chốt scope/ma trận quyền; Trung chốt test matrix; Long chốt target staging; mỗi người có issue và tiêu chí nghiệm thu |
| 29/07 | Audit và triển khai phần rủi ro cao | Hoàng bàn giao danh sách lỗ hổng tenant/quyền; Bảo bàn giao failure matrix AI; Minh bàn giao UX gap list; Khang chốt golden dataset |
| 30/07 | Hoàn thành lát cắt dọc đầu tiên | Luồng tổ chức → dự án → nhiệm vụ → AI chạy end-to-end; QA có test happy path và forbidden path |
| 31/07 | Feature freeze | Merge toàn bộ P0/P1 đã hoàn thành; từ sau mốc này chỉ nhận bug fix, test, tài liệu và thay đổi release |
| 01/08 | Regression và diễn tập vận hành | Full suite trên commit RC; deploy staging; test provider/Redis suy giảm; diễn tập backup/restore/rollback |
| 02/08 | Sửa lỗi và retest | Đóng toàn bộ P0/P1; kiểm tra mobile/accessibility; AI evaluation đạt ngưỡng; tài liệu vận hành hoàn tất |
| 03/08 | Nghiệm thu và phát hành RC | Demo 7 luồng chính, ký checklist, công bố test report/release note/known issues và quyết định Go/No-Go |

## 5. Phụ thuộc và phối hợp

| Bàn giao | Người giao | Người nhận | Hạn |
|---|---|---|---|
| Ma trận quyền chuẩn | Quang Tuấn | Duy Hoàng, Viết Minh, Đoàn Trung | 28/07 |
| Contract API và mã lỗi tổ chức/thành viên | Duy Hoàng | Viết Minh, Đoàn Trung | 30/07 |
| Failure matrix, usage và cost contract AI | Quốc Bảo | Chí Khang, Đoàn Trung | 30/07 |
| Golden dataset và tiêu chí chất lượng AI | Chí Khang | Quốc Bảo, Đoàn Trung | 30/07 |
| Danh sách test RC và bug severity | Đoàn Trung | Toàn đội | Cập nhật hằng ngày |
| Staging URL, log và release evidence | Gia Long | Đoàn Trung, Quang Tuấn | 01/08 |

## 6. Nhịp làm việc

- **08:30:** Daily 15 phút: hôm qua, hôm nay, blocker, rủi ro P0/P1.
- **13:30:** Triage 15 phút giữa Tech Lead, Backend, QA và owner lỗi.
- **16:30:** Merge window; QA chạy smoke trên commit tích hợp.
- Mỗi task phải có owner, mức ưu tiên, tiêu chí nghiệm thu, test/evidence và liên kết PR.
- PR nhỏ, một mục tiêu; thay đổi auth/tenant/migration/AI budget cần ít nhất Tech Lead và đúng domain owner review.
- Blocker quá 2 giờ phải báo ngay, không chờ daily hôm sau.

## 7. Tiêu chí Go/No-Go

**Go khi:**

- Không còn P0/P1.
- Tất cả gate bắt buộc pass trên đúng commit RC.
- Tenant isolation, ma trận quyền và AI budget có test pass.
- Staging healthy; backup/restore và rollback thành công.
- Release note và known issues đã được phê duyệt.

**No-Go khi có một trong các điều kiện:**

- Có khả năng truy cập chéo tổ chức hoặc vượt quyền.
- Mất/ghi sai dữ liệu, migration không an toàn hoặc restore thất bại.
- AI vượt ngân sách, lộ dữ liệu/secret hoặc lỗi AI làm gián đoạn chức năng lõi.
- Không tái hiện được bản build hoặc không có đường rollback đã kiểm chứng.

## 8. Chỉ số báo cáo cuối tuần

| Nhóm chỉ số | Cần báo cáo |
|---|---|
| Chất lượng | Tổng test/pass/fail/blocked; số P0/P1/P2 mở và đã đóng |
| Phát hành | Commit RC, image tag, kết quả deploy/health/rollback |
| Bảo mật | Số case tenant isolation/RBAC đã kiểm tra; kết quả secret/vulnerability scan |
| AI | Tỷ lệ đúng schema, tỷ lệ đạt golden dataset, latency, lỗi, token và chi phí |
| UX | Số luồng hoàn thiện state, số viewport đạt, lỗi console/accessibility còn lại |
| Tiến độ | Hạng mục hoàn thành/chuyển tiếp, nguyên nhân và owner tuần sau |

