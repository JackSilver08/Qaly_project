# Kịch bản demo số 1 hoàn chỉnh - QALY

## 1. Tên kịch bản

**Kiểm soát trách nhiệm và chất lượng xử lý hotfix NovaPay**

Tên gọi thể hiện hai giá trị nghiệp vụ chính:

- QALY phát hiện công việc bị bỏ bê để PM can thiệp kịp thời.
- QALY kiểm soát minh chứng trước khi cho phép báo cáo hoàn thành.

## 2. Mục tiêu demo

Kịch bản chứng minh QALY không chỉ hỗ trợ thêm, sửa và xóa task, mà còn quản lý trách nhiệm xuyên suốt quá trình thực hiện:

- Quản lý project và task theo trạng thái nghiệp vụ thực tế.
- Phát hiện task quá hạn, chưa bắt đầu và chưa được assignee xem qua Dashboard/Attention.
- Cho phép PM nhắc người phụ trách và đổi assignee thủ công khi cần.
- Yêu cầu thành viên nộp evidence cho kết quả công việc.
- Cho phép PM duyệt hoặc từ chối evidence kèm lý do.
- Chặn task chuyển sang `Done` khi chưa có evidence `Approved`.
- Gửi notification cho các sự kiện nhắc việc và review evidence.
- Lưu audit cho các thao tác quan trọng đã được xác minh như cập nhật task, review evidence và đổi trạng thái.
- Kiểm tra quyền trước thao tác, không cho user thực hiện hành động ngoài phạm vi được cấp.

Thông điệp cần chốt với giảng viên:

> QALY không chỉ ghi nhận công việc, mà còn phát hiện trách nhiệm bị bỏ quên, hỗ trợ PM can thiệp, kiểm soát chất lượng đầu ra và lưu dấu vết xử lý.

## 3. Bối cảnh nghiệp vụ

Dự án **NovaPay - Hotfix thanh toán trùng** đang xử lý lỗi một giao dịch có thể bị ghi nhận hai lần trước đợt phát hành thử nghiệm. Trong lúc thời hạn dự án chỉ còn hai ngày, task `T1 - Xác nhận nguyên nhân giao dịch trùng` do An Trần phụ trách đã quá hạn một giờ nhưng vẫn ở trạng thái `Todo`. Dữ liệu hệ thống cũng cho thấy An chưa mở task kể từ khi được giao.

QALY đưa task này vào danh sách Attention với các tín hiệu quá hạn, chưa bắt đầu và chưa xem. Lan Nguyễn, Project Owner kiêm PM, gửi nudge và ghi chú yêu cầu phản hồi. Vì An vẫn không phản hồi trong tình huống demo, Lan chủ động đổi assignee sang Chi Lê để dự án tiếp tục vận hành.

Song song đó, task `T3 - Chạy regression và nộp log` đang ở `InReview`. Chi đã nộp evidence nhưng file chưa thể hiện idempotency key và mã giao dịch trả về. Lan từ chối evidence, ghi rõ lý do và hệ thống thông báo cho người liên quan. Chỉ sau khi Chi nộp lại evidence đầy đủ và Lan phê duyệt, task mới đủ điều kiện chuyển sang `Done`.

Như vậy, cùng một dự án thể hiện được hai lớp kiểm soát: **kiểm soát trách nhiệm trong quá trình thực hiện** và **kiểm soát chất lượng trước khi hoàn tất**.

## 4. Nhân vật tham gia demo

| Nhân vật | Role trong hệ thống | Trách nhiệm nghiệp vụ | Quyền liên quan | Hành động trong kịch bản | Lý do xuất hiện |
|---|---|---|---|---|---|
| Lan Nguyễn | `Owner`/PM | Điều phối dự án và kiểm soát kết quả | Xem Attention, nudge, cập nhật task/assignee, review evidence | Phát hiện T1, nudge An, đổi assignee sang Chi, reject/approve evidence | Nhân vật điều phối chính, thể hiện quyền PM có kiểm soát |
| An Trần | `Member` hoặc `Developer` | Xử lý task được giao | Xem và cập nhật task khi là assignee | Là assignee ban đầu nhưng chưa xem/chưa cập nhật T1 | Tạo tình huống trách nhiệm bị bỏ bê |
| Chi Lê | `Member` hoặc `Tester` | Tiếp nhận xử lý thay thế và cung cấp kết quả kiểm thử | Xem/cập nhật task thuộc quyền, upload và đánh dấu evidence | Nhận T1 thay An; là assignee T3 và nộp evidence | Thể hiện luồng thực thi và bổ sung kết quả sau khi bị từ chối |
| Hạnh Phạm | `Viewer` hoặc `Customer` | Theo dõi tiến độ được phép xem | Chỉ đọc dữ liệu phù hợp membership/quyền | Chỉ dùng ở bản mở rộng để thử thao tác sửa hoặc truy cập task private | Chứng minh permission/`403`, không làm gián đoạn demo ngắn |

Không dùng `Admin` trong luồng chính vì quyền quá rộng sẽ làm giảm ý nghĩa của phần phân quyền. Không dùng role `Reviewer` độc lập để duyệt evidence vì quyền này chưa được xác minh riêng cho role đó.

## 5. Điều kiện trước khi demo

1. Đăng nhập sẵn tài khoản Lan Nguyễn với role `Owner` hoặc role quản lý dự án hợp lệ.
2. Project `NovaPay - Hotfix thanh toán trùng` đã tồn tại, trạng thái `Active`.
3. Lan, An và Chi đã là thành viên project.
4. Hạnh đã là `Viewer`/`Customer` nếu dùng bản mở rộng.
5. `T1` đã được giao cho An:
   - Status `Todo`.
   - Start date đã qua.
   - Due date đã qua một giờ.
   - Không có `TaskViewEvent` của An sau `AssignedAt`.
6. `T2` đang `InProgress`, do Chi phụ trách, dùng làm đối chứng cho task hoạt động bình thường.
7. `T3` đang `InReview`, có evidence thiếu dữ liệu ở trạng thái `Pending`.
8. Chuẩn bị hai file nhỏ, upload nhanh:
   - `regression-log-thieu-idempotency.txt`.
   - `regression-log-day-du.txt`.
9. Chuẩn bị comment:
   - `Task đã quá hạn và chưa được tiếp nhận. Cần phản hồi trước 10:30.`
10. Chuẩn bị review note:
   - `Log chưa thể hiện idempotency key và mã giao dịch trả về.`
11. Dashboard/Attention phải hiển thị T1 với các lý do `QuaHan`, `ChuaBatDau`, `ChuaXem`.
12. T3 chưa được có evidence `Approved` trước bước chứng minh completion gate.
13. Notification và Audit Log/Recent Activity có dữ liệu phù hợp để mở nhanh nếu còn thời gian.

## 6. Dữ liệu demo

### Project

| Trường | Giá trị |
|---|---|
| Name | `NovaPay - Hotfix thanh toán trùng` |
| Code | `NOVAPAY-HOTFIX` |
| Description | `Xử lý lỗi giao dịch bị ghi nhận hai lần; mọi kết quả phải có minh chứng được duyệt.` |
| Status | `Active` |
| Owner | Lan Nguyễn |
| EndDate | Ngày demo + 2 ngày, 17:00 |

### User

| User | Project role | Trạng thái | Ghi chú |
|---|---|---|---|
| Lan Nguyễn | `Owner` | Active | Có quyền quản lý và review evidence |
| An Trần | `Member` hoặc `Developer` | Active | Assignee ban đầu của T1 |
| Chi Lê | `Member` hoặc `Tester` | Active | Assignee T2, T3 và người thay thế T1 |
| Hạnh Phạm | `Viewer` hoặc `Customer` | Active | Chỉ dùng trong bản mở rộng |

### Task

| Task | Assignee ban đầu | Status | Thời hạn | Dữ liệu đặc biệt |
|---|---|---|---|---|
| `T1 - Xác nhận nguyên nhân giao dịch trùng` | An | `Todo` | Đã quá hạn 1 giờ | Start date đã qua; An chưa có view event sau khi được giao |
| `T2 - Sửa cơ chế idempotency` | Chi | `InProgress` | Còn 8 giờ | Task đối chứng đang vận hành bình thường |
| `T3 - Chạy regression và nộp log` | Chi | `InReview` | Ngày demo + 1 ngày | Có evidence thiếu dữ liệu ở trạng thái `Pending` |

### Evidence

| File | Task | Nội dung | Trạng thái ban đầu | Kết quả mong đợi |
|---|---|---|---|---|
| `regression-log-thieu-idempotency.txt` | T3 | Không thể hiện idempotency key và mã giao dịch | `Pending` | Bị Lan reject |
| `regression-log-day-du.txt` | T3 | Có idempotency key, request và mã giao dịch trả về | Chưa upload hoặc đã chuẩn bị sẵn | Được Lan approve |

### Comment

| Người viết | Task | Nội dung |
|---|---|---|
| Lan | T1 | `Task đã quá hạn và chưa được tiếp nhận. Cần phản hồi trước 10:30.` |

### Notification dự kiến

| Người nhận | Loại/sự kiện | Nội dung nghiệp vụ |
|---|---|---|
| An | `TaskAttentionNudge` | T1 đang cần được kiểm tra và phản hồi |
| Chi và các bên liên quan | `ReviewCompleted` | Evidence T3 bị từ chối kèm review note |
| Chi và các bên liên quan | `ReviewCompleted` | Evidence mới của T3 đã được phê duyệt |

### Audit Log dự kiến

| Action nghiệp vụ | Entity | Dữ liệu cần chỉ ra |
|---|---|---|
| Cập nhật task/assignee | T1 | Actor Lan, assignee từ An sang Chi, thời điểm cập nhật |
| `ReviewEvidence` - reject | Evidence thiếu của T3 | Actor Lan, trạng thái `Rejected`, review note |
| `ReviewEvidence` - approve | Evidence đúng của T3 | Actor Lan, trạng thái `Approved`, reviewer và thời điểm |
| Đổi trạng thái task | T3 | Chuyển sang `Done` sau khi đủ điều kiện |

Không khẳng định Audit Log có bản ghi riêng cho nudge hoặc access denied vì Context Pack chưa xác minh hai loại audit này.

## 7. Luồng demo chính 3–4 phút

| Bước | Người thực hiện | Màn hình/module | Hành động demo | Dữ liệu/thao tác cụ thể | Kết quả hệ thống | Ý nghĩa trình bày |
|---|---|---|---|---|---|---|
| 1 | Lan | Dashboard/Attention | Mở danh sách task cần chú ý | Chọn T1 có `QuaHan`, `ChuaBatDau`, `ChuaXem` | T1 được ưu tiên trong Attention | Hệ thống chủ động tổng hợp rủi ro từ dữ liệu nghiệp vụ |
| 2 | Lan | Project NovaPay | Mở project và Kanban/task list | Chỉ ra T1 `Todo`, T2 `InProgress`, T3 `InReview` | Thấy tổng thể tiến độ cùng một dự án | Chứng minh đây là luồng project/task thực tế |
| 3 | Lan | Task T1 detail | Kiểm tra assignee và deadline | Assignee An, due date đã qua, chưa có dấu hiệu được xem | Các lý do Attention khớp với dữ liệu task | Cảnh báo có căn cứ, không phải cảnh báo tùy ý |
| 4 | Lan | Task T1/Attention | Gửi nudge và thêm comment | Dùng comment đã chuẩn bị | An nhận `TaskAttentionNudge`; task vẫn `Todo` | Attention hỗ trợ PM can thiệp, không tự đổi status |
| 5 | Lan | Task T1 edit | Đổi assignee thủ công | Từ An sang Chi | T1 được cập nhật; Chi trở thành người xử lý | Reassignment là quyết định có chủ đích của PM |
| 6 | Lan | Task T3 detail | Mở evidence đang chờ duyệt | File thiếu ở trạng thái `Pending` | Hiển thị metadata và trạng thái review | Chuyển từ kiểm soát trách nhiệm sang kiểm soát chất lượng |
| 7 | Lan | Evidence review | Reject evidence thiếu | Nhập review note đã chuẩn bị | Evidence thành `Rejected`; notification được gửi | Kết quả không đạt bị trả lại có lý do |
| 8 | Lan | Task T3 | Thử chuyển task sang `Done` | Chọn `Done` khi chưa có evidence Approved | Hệ thống từ chối thao tác | Business rule chống báo cáo hoàn thành giả |
| 9 | Chi hoặc dữ liệu chuẩn bị | Attachment/Evidence | Nộp file đúng và đánh dấu evidence | `regression-log-day-du.txt` | Evidence mới ở trạng thái `Pending` | Thành viên phải bổ sung kết quả theo phản hồi |
| 10 | Lan | Evidence review | Approve evidence đúng | Chọn approve | Evidence thành `Approved`; notification được gửi | Kết quả đã đạt điều kiện nghiệm thu |
| 11 | Lan | Task T3/Kanban | Chuyển T3 sang `Done` | Transition `InReview -> Done` | Task hoàn tất thành công | Done là kết quả của kiểm soát, không chỉ là thao tác kéo thẻ |
| 12 | Lan | Dashboard/Kanban | Quay lại tổng quan | T3 đã Done; T1 đã có assignee mới | Tiến độ và rủi ro được cập nhật | Khép kín vòng phát hiện, can thiệp và kiểm soát |
| 13 | Lan | Audit/Recent Activity | Mở nhanh nếu còn thời gian | Xem update task, review evidence, status change | Hiện actor, entity và thời điểm | Chứng minh khả năng truy vết trách nhiệm |

Để giữ đúng thời lượng, bước nộp evidence đúng có thể được chuẩn bị bằng profile Chi ở tab khác hoặc seed trước và chỉ refresh. Không cần logout/login liên tục.

## 8. Luồng ngoại lệ chính: Assignee bỏ bê task

### Tình huống

An được giao T1 nhưng không mở và không cập nhật task. Start date đã qua, due date đã quá một giờ nhưng status vẫn là `Todo`.

### Vì sao đây là lỗi nghiệp vụ thực tế

Trong vận hành dự án, “đã giao việc” không đồng nghĩa với “đã được tiếp nhận”. Nếu PM chỉ nhìn số lượng task, rủi ro này có thể bị bỏ qua cho tới khi ảnh hưởng deadline chung.

### Dữ liệu dùng để phát hiện

- `DueDate < thời điểm hiện tại`.
- Status vẫn là `Todo`.
- `StartDate` đã qua.
- Không có `TaskViewEvent` của An sau `AssignedAt`.

### Attention/Dashboard hiển thị

- `QuaHan`.
- `ChuaBatDau`.
- `ChuaXem`.

Attention chỉ phát hiện và hiển thị tín hiệu. Hệ thống không tự đổi trạng thái, tự xóa assignee hoặc tự giao việc cho người khác.

### Quyền xử lý của PM

Lan có thể:

- Mở task để kiểm tra chi tiết.
- Gửi nudge cho assignee.
- Bình luận yêu cầu phản hồi.
- Đổi assignee thủ công.
- Điều chỉnh task theo quyền quản lý và quy tắc trạng thái.

### Notification

Khi Lan gửi nudge, An nhận notification `TaskAttentionNudge`. Đây là tín hiệu vận hành để người phụ trách biết task cần được xử lý.

### Can thiệp

Vì An vẫn không phản hồi trong tình huống demo, Lan đổi assignee từ An sang Chi. Đây là thao tác thủ công của PM, không phải workflow escalation tự động nhiều cấp.

### Kết quả

- T1 không còn phụ thuộc vào phản hồi của An.
- Chi trở thành người xử lý mới.
- Thay đổi task/assignee có thể được truy vết qua dữ liệu cập nhật và Audit Log tương ứng.

### Giá trị production

Cơ chế giúp PM phát hiện sớm công việc đã giao nhưng chưa được tiếp nhận, từ đó can thiệp trước khi sự chậm trễ lan rộng.

## 9. Luồng ngoại lệ phụ: Evidence không đạt yêu cầu

Chi nộp `regression-log-thieu-idempotency.txt` cho T3 và đánh dấu là evidence. Evidence chuyển sang `Pending`.

Lan kiểm tra và nhận thấy file chưa thể hiện:

- Idempotency key đã sử dụng.
- Mã giao dịch trả về.
- Căn cứ để xác nhận hai request không tạo hai giao dịch.

Lan reject evidence với review note:

> Log chưa thể hiện idempotency key và mã giao dịch trả về.

Sau khi reject:

- Evidence chuyển sang `Rejected`.
- Người nộp, reporter hoặc assignee liên quan nhận notification `ReviewCompleted`.
- T3 không thể chuyển sang `Done` vì chưa có evidence `Approved`.

Chi nộp `regression-log-day-du.txt` và đánh dấu evidence. Lan kiểm tra, approve, evidence chuyển sang `Approved`. Khi đó T3 mới đủ điều kiện để chuyển từ `InReview` sang `Done`.

Giá trị production của cơ chế này là ngăn tình trạng báo cáo hoàn thành chỉ bằng lời nói hoặc một file không chứng minh được kết quả.

## 10. Điểm phân quyền cần thể hiện

- Không dùng `Admin` trong demo chính để quyền hạn của Project Owner có ý nghĩa.
- Lan với role `Owner`/PM có quyền xem Attention, nudge, đổi assignee và review evidence.
- An và Chi với role thành viên chỉ xử lý task thuộc quyền; không tự duyệt evidence.
- Không dùng role `Reviewer` độc lập vì quyền review evidence riêng cho role này chưa được xác minh.
- Hạnh với role `Viewer`/`Customer` chỉ xem dữ liệu được phép; có thể dùng ở bản mở rộng để chứng minh thao tác sửa bị `403`.
- User ngoài membership không được truy cập project hoặc task ngoài quyền.
- Task private không được hiển thị cho user không phải owner, reporter hoặc assignee phù hợp.
- Không khẳng định access denied có audit riêng nếu chưa kiểm tra được bản ghi.

## 11. Thứ tự màn hình nên mở

1. **Dashboard/Attention**
   - Mục đích: mở đầu bằng rủi ro thay vì danh sách CRUD.
   - Dữ liệu: T1 có `QuaHan`, `ChuaBatDau`, `ChuaXem`.
   - Câu chuyển: “Từ Dashboard, PM có thể thấy ngay công việc nào đang bị bỏ quên.”

2. **Project NovaPay detail**
   - Mục đích: đặt cảnh báo vào bối cảnh dự án.
   - Dữ liệu: project Active, ba task ở ba trạng thái.
   - Câu chuyển: “Tôi mở dự án để xác định task nào đang ảnh hưởng luồng hotfix.”

3. **Task/Kanban**
   - Mục đích: so sánh T1 bất thường với T2 đang vận hành bình thường.
   - Dữ liệu: T1 `Todo`, T2 `InProgress`, T3 `InReview`.
   - Câu chuyển: “Cùng một dự án nhưng mỗi task đang ở một giai đoạn trách nhiệm khác nhau.”

4. **Task T1 detail**
   - Mục đích: chứng minh căn cứ của Attention.
   - Dữ liệu: assignee An, deadline quá hạn, chưa xem.
   - Câu chuyển: “Attention không tự suy diễn vô căn cứ; cảnh báo được tạo từ deadline, trạng thái, assignment và view event.”

5. **Notification panel**
   - Mục đích: cho thấy nudge tạo tác động tới người nhận.
   - Dữ liệu: `TaskAttentionNudge`.
   - Câu chuyển: “PM nhắc việc và hệ thống chuyển lời nhắc thành notification có thể theo dõi.”

6. **Evidence/Attachment review của T3**
   - Mục đích: trình bày kiểm soát chất lượng.
   - Dữ liệu: evidence `Pending`, review note, sau đó `Rejected`/`Approved`.
   - Câu chuyển: “Sau khi kiểm soát người thực hiện, hệ thống tiếp tục kiểm soát chất lượng kết quả.”

7. **Kanban/Dashboard sau xử lý**
   - Mục đích: khép kín luồng.
   - Dữ liệu: T1 đã đổi assignee; T3 đã `Done`.
   - Câu chuyển: “Kết quả xử lý được phản ánh trở lại tiến độ dự án.”

8. **Audit Log/Recent Activity**
   - Mục đích: chứng minh truy vết nếu còn thời gian.
   - Dữ liệu: update task, review evidence, status change.
   - Câu chuyển: “Các thao tác quan trọng không chỉ thay đổi dữ liệu mà còn để lại dấu vết trách nhiệm.”

## 12. Script thuyết trình 3–4 phút

### Mở đầu

“Kịch bản này mô phỏng dự án NovaPay đang xử lý lỗi một giao dịch bị ghi nhận hai lần trước đợt phát hành thử nghiệm. Mục tiêu không chỉ là quản lý danh sách task, mà là phát hiện trách nhiệm bị bỏ quên và kiểm soát chất lượng trước khi công việc được công nhận hoàn thành.”

### Dashboard/Attention

“Tại Dashboard, QALY đang cảnh báo task T1 cần chú ý. Task này đã quá hạn một giờ, start date đã qua nhưng vẫn ở Todo, đồng thời An chưa mở task kể từ khi được giao. Vì vậy hệ thống hiển thị ba tín hiệu: quá hạn, chưa bắt đầu và chưa xem.”

“Attention chỉ phát hiện rủi ro và hỗ trợ PM ra quyết định. Hệ thống không tự ý đổi trạng thái hoặc tự giao task cho người khác.”

### PM can thiệp

“Tôi mở chi tiết T1 để kiểm tra. Lan là Project Owner nên có thể gửi nudge và ghi chú yêu cầu An phản hồi. Nudge tạo notification cho An, nhưng task vẫn giữ nguyên trạng thái.”

“Trong tình huống này An tiếp tục không phản hồi, nên PM đổi assignee thủ công sang Chi. Đây là quyết định của PM, chưa phải workflow escalation tự động nhiều cấp.”

### Kiểm soát evidence

“Tiếp theo là task T3 đang chờ kiểm tra kết quả. Chi đã nộp file regression log và đánh dấu là evidence. Tuy nhiên file chưa thể hiện idempotency key và mã giao dịch trả về, nên Lan từ chối và ghi rõ lý do.”

“Sau khi evidence bị từ chối, hệ thống gửi notification ReviewCompleted cho người liên quan. Nếu tôi thử đưa task sang Done lúc này, QALY chặn thao tác vì chưa có evidence Approved.”

### Approve và hoàn tất

“Chi nộp lại file đầy đủ. Lan kiểm tra và approve evidence. Chỉ từ thời điểm này, task mới đủ điều kiện chuyển từ InReview sang Done.”

### Kết luận

“Qua kịch bản này, QALY thể hiện bốn lớp kiểm soát: phát hiện task bị bỏ bê, hỗ trợ PM can thiệp, kiểm soát chất lượng bằng evidence và lưu dữ liệu notification/audit để truy vết trách nhiệm. Vì vậy hệ thống không chỉ là CRUD task, mà hỗ trợ vận hành dự án gần với thực tế production.”

## 13. Bản demo rút gọn đúng 3–4 phút

- **0:00–0:30:** Giới thiệu lỗi thanh toán trùng, deadline và mục tiêu quản lý trách nhiệm.
- **0:30–1:10:** Mở Dashboard/Attention; chỉ ra T1 `QuaHan`, `ChuaBatDau`, `ChuaXem`.
- **1:10–1:50:** Mở T1; gửi nudge/comment; giải thích Attention không tự đổi status.
- **1:50–2:20:** Đổi assignee T1 từ An sang Chi; nhấn mạnh PM xử lý thủ công.
- **2:20–2:50:** Mở T3 và reject evidence thiếu với review note.
- **2:50–3:10:** Thử chuyển T3 sang `Done`; hệ thống chặn vì chưa có evidence Approved.
- **3:10–3:35:** Mở evidence đúng đã chuẩn bị, approve và chuyển T3 sang `Done`.
- **3:35–4:00:** Quay lại Kanban/Dashboard; chốt notification, audit và giá trị production.

## 14. Bản mở rộng 10–15 phút cho báo cáo lần 2

### Đăng nhập nhiều role

- Profile 1: Lan/Owner thực hiện quản lý và review.
- Profile 2: Chi/Member nhận task, upload evidence và xem notification.
- Profile 3: Hạnh/Viewer thử thao tác sửa để nhận `403`.

### Kiểm tra permission

- Hạnh xem project công khai trong membership.
- Hạnh thử sửa task hoặc project và bị chặn.
- Tạo hoặc dùng một task private để chứng minh Viewer không thấy nội dung ngoài quyền.
- Không claim access denied có audit riêng nếu chưa xác minh.

### Notification phía người nhận

- An xem notification `TaskAttentionNudge`.
- Chi xem `ReviewCompleted` sau reject.
- Đánh dấu notification đã đọc để thể hiện trạng thái unread/read.

### Audit Log/Recent Activity

- Xem thay đổi assignee T1.
- Xem reject/approve evidence T3.
- Xem chuyển trạng thái T3 sang `Done`.
- Chỉ ra actor, entity, thời điểm và nội dung thay đổi.

### RowVersion/409 Conflict

- Mở cùng một task ở hai trình duyệt.
- Trình duyệt thứ nhất cập nhật task.
- Trình duyệt thứ hai gửi dữ liệu cũ.
- QALY trả `409 Conflict` và yêu cầu refresh.
- Chỉ thực hiện nếu đã kiểm tra ổn định trước buổi báo cáo.

### Giới hạn hiện tại

- Attention chưa tự động escalation nhiều cấp.
- PM đổi assignee thủ công.
- Chưa có task acceptance/decline.
- Chưa có module Milestone.
- Reviewer chưa có quyền review độc lập đã được xác minh.

## 15. Checklist chuẩn bị trước demo

- [ ] Seed project và task bằng thời gian tương đối so với thời điểm demo.
- [ ] Đăng nhập sẵn Lan với role `Owner`/Manager hợp lệ.
- [ ] Kiểm tra T1 chắc chắn hiện `QuaHan`, `ChuaBatDau`, `ChuaXem`.
- [ ] Kiểm tra `T1.Status = Todo`.
- [ ] Kiểm tra `T1.StartDate` và `T1.DueDate` đã qua.
- [ ] Kiểm tra An chưa có `TaskViewEvent` sau `AssignedAt`.
- [ ] Kiểm tra T2 đang `InProgress` để làm đối chứng.
- [ ] Kiểm tra T3 đang `InReview`.
- [ ] Kiểm tra evidence thiếu của T3 đang `Pending`.
- [ ] Kiểm tra T3 chưa có bất kỳ evidence `Approved` nào.
- [ ] Upload thử hai file evidence trước buổi demo.
- [ ] Kiểm tra PM có thể reject/approve evidence.
- [ ] Kiểm tra task bị chặn khi chuyển `Done` trước approval.
- [ ] Kiểm tra notification nudge và review xuất hiện đúng user.
- [ ] Mở sẵn Dashboard, Project, Kanban và T3 ở các tab.
- [ ] Chuẩn bị profile Chi nếu muốn upload trực tiếp.
- [ ] Chuẩn bị profile Hạnh cho bản mở rộng permission.
- [ ] Có ảnh hoặc video backup cho Attention, `403`, evidence reject và completion gate.
- [ ] Không dùng dữ liệu, token hoặc credential production thật.
- [ ] Chuẩn bị script reset/reseed dữ liệu sau mỗi lần tập.
- [ ] Tập thao tác để hoàn tất luồng chính trong tối đa bốn phút.

## 16. Những câu cần nói để tránh bị bắt lỗi

- “Trong phiên bản hiện tại, Attention chỉ phát hiện rủi ro và hỗ trợ PM ra quyết định; hệ thống chưa tự động đổi trạng thái task.”
- “Reassignment hiện là thao tác thủ công của PM, chưa phải workflow escalation nhiều cấp tự động.”
- “Hệ thống hiện chưa có module Milestone riêng, nên demo này dùng deadline project/task và Attention để thể hiện rủi ro tiến độ.”
- “Role Reviewer có trong danh mục, nhưng quyền duyệt evidence hiện được xác minh cho Owner/Manager/Admin, nên demo dùng PM làm người duyệt.”
- “Task không thể chuyển Done nếu chưa có evidence được Approved; đây là cơ chế chống báo cáo hoàn thành giả.”
- “Nudge tạo notification cho người phụ trách, nhưng audit riêng cho hành động nudge chưa được xác minh nên tôi không khẳng định trong demo.”
- “User không có quyền bị chặn ở access policy; audit riêng cho lần truy cập bị từ chối chưa được xác minh.”
- “Task acceptance/decline và escalation nhiều cấp là hướng phát triển, không phải chức năng hiện tại.”
- “Nếu chức năng nào chưa được xác minh end-to-end, tôi không đưa vào luồng demo chính mà trình bày ở phần định hướng.”

## 17. Tiêu chí đánh giá kịch bản

Kịch bản giúp giảng viên đánh giá:

- Hệ thống có luồng nghiệp vụ project/task thực tế.
- Nhiều vai trò tham gia với trách nhiệm khác nhau.
- QALY phát hiện task bị bỏ bê và quá hạn từ dữ liệu thực.
- PM có công cụ can thiệp thay vì theo dõi thủ công bên ngoài.
- Nudge và review evidence tạo notification.
- Quyền quản lý và quyền thành viên được tách biệt.
- Evidence có trạng thái và quy trình review.
- Business rule chặn `Done` khi evidence chưa `Approved`.
- Các thao tác quan trọng có dữ liệu audit/truy vết.
- Hệ thống có khả năng vận hành gần với production.
- Giá trị hệ thống vượt ra ngoài thao tác thêm, sửa và xóa task.

## 18. Đề xuất bổ sung sau báo cáo lần 1

Các nội dung sau không đưa vào luồng demo chính:

1. **Task acceptance/decline**
   - Cho phép assignee xác nhận nhận hoặc từ chối task.

2. **Escalation policy nhiều cấp**
   - Tự động nhắc assignee, báo PM và đề xuất đổi người theo thời gian.

3. **Unavailable user/orphan assignment detection**
   - Phát hiện task đang gắn với user inactive hoặc đã rời project.

4. **Milestone entity/module**
   - Quản lý mốc bàn giao và ảnh hưởng của task tới từng mốc.

5. **Quyền review riêng cho role Reviewer**
   - Tách rõ trách nhiệm quản lý dự án và nghiệm thu chất lượng.

6. **Audit cho access denied và nudge**
   - Bổ sung truy vết đầy đủ cho sự kiện bảo mật và vận hành nếu hiện chưa có.

## Kết luận

Kịch bản tập trung vào một câu chuyện ngắn nhưng hoàn chỉnh: một task bị bỏ bê được QALY phát hiện, PM can thiệp và đổi người xử lý; một kết quả thiếu chất lượng bị từ chối; task chỉ được hoàn tất sau khi evidence hợp lệ được phê duyệt.

Đây là bằng chứng trực tiếp rằng QALY có khả năng **quản lý trách nhiệm, phát hiện rủi ro, kiểm soát quyền, kiểm soát chất lượng đầu ra và truy vết thao tác**, thay vì chỉ đóng vai trò một bảng CRUD công việc.
