# Kịch bản tích hợp nghiệp vụ Qaly

## Chiến dịch 72 giờ cứu lễ ra mắt NovaPay

## 1. Mục tiêu

Đây là **một kịch bản nghiệp vụ xuyên suốt**, không phải tập hợp các màn demo tính năng.

Mỗi hành động trong Qaly tạo dữ liệu hoặc tình huống cho hành động tiếp theo:

`Nhóm khẩn cấp -> Dự án -> Import công việc -> Phân công -> Sprint/Gantt -> Họp xử lý sự cố -> Task phát sinh -> Thực thi -> Minh chứng -> Duyệt -> Quyết định phát hành -> Hậu kiểm`

## 2. Bối cảnh

09:00 sáng thứ Sáu, công ty Nova dự kiến phát hành nền tảng thanh toán **NovaPay** vào 09:00 thứ Hai.

Ngân hàng đối tác bất ngờ gửi đặc tả API mới. Cùng lúc, môi trường staging ghi nhận giao dịch bị trừ tiền hai lần. Ngân hàng yêu cầu Nova:

- Gửi kế hoạch xử lý trong 30 phút.
- Hoàn thành bản vá trong 48 giờ.
- Cung cấp minh chứng kiểm thử được phê duyệt.
- Cho phép truy vết đầy đủ người thực hiện và quyết định phát hành.

Nếu không đáp ứng, cửa sổ triển khai sẽ bị hủy.

## 3. Vai diễn

| Nhân vật | Vai trò trong Qaly | Trách nhiệm |
|---|---|---|
| Lan | Owner/PM | Điều phối, phân quyền và quyết định phát hành |
| Minh | Manager/Scrum Master | Lập kế hoạch, dependency và theo dõi rủi ro |
| An | Developer | Sửa lỗi thanh toán |
| Bình | Developer | Chuyên gia Bank API nhưng đang quá tải |
| Chi | Tester | Kiểm thử và nộp minh chứng |
| Dũng | Reviewer | Duyệt minh chứng |
| Hạnh | Customer/Viewer | Theo dõi tiến độ thay mặt ngân hàng |
| Tài khoản lạ | Outside user | Tạo tình huống truy cập trái phép |

## 4. Luồng kịch bản xuyên suốt

### 09:05 - Kích hoạt phòng xử lý khủng hoảng

Lan tạo nhóm **NovaPay War Room**, mời Minh, An, Bình, Chi, Dũng và Hạnh.

Lan tải đặc tả ngân hàng lên nhóm, ghim thông báo:

> 09:00 thứ Hai phải phát hành. Mọi quyết định và bằng chứng phải được ghi nhận trên Qaly.

Các thành viên reaction để xác nhận đã đọc. Lan tạo poll:

- Giữ lịch phát hành và vá nóng.
- Lùi toàn bộ lễ ra mắt.
- Phát hành giới hạn, tạm tắt thanh toán nhanh.

Kết quả nghiêng về vá nóng. Từ chính nhóm này, Lan tạo dự án **NovaPay - Emergency Release**; thành viên nhóm được đưa vào dự án để không phải khai báo lại.

Lan cấp:

- Minh quyền quản lý dự án, timeline, rủi ro và nhắc việc.
- An, Bình, Chi quyền thực hiện công việc.
- Dũng quyền review.
- Hạnh quyền Viewer/Customer.

Hạnh thử sửa dự án để yêu cầu thêm chức năng. Qaly chặn thao tác vì Hạnh chỉ có quyền xem. Lan chuyển yêu cầu đó thành nội dung trao đổi thay vì để khách hàng tự sửa kế hoạch.

**Dữ liệu sinh ra cho bước sau:** nhóm, thành viên, vai trò, tài liệu nguồn, kết quả poll và dự án.

---

### 09:15 - Biến kế hoạch hỗn loạn thành backlog

Ngân hàng gửi thêm file `NovaPay-emergency-plan.xlsx` gồm hàng trăm dòng công việc.

Lan import file vào dự án. Qaly preview và gợi ý mapping các cột:

- Tiêu đề.
- Mô tả.
- Trạng thái.
- Mức ưu tiên.
- Deadline.
- Người phụ trách.

Kết quả thử nhập cho thấy:

- Có task trùng.
- Một số dòng không có tiêu đề.
- Trạng thái `WAITING_FOR_BANK` chưa được ánh xạ.
- Một số task không có assignee.

Lan không nhập mù. Cô bật bỏ qua bản ghi trùng, chọn trạng thái mặc định `Todo`, rồi thực thi import.

Sau khi import, Lan nhận ra đây là bản Excel cũ vì chưa có yêu cầu kiểm tra idempotency. Cô dùng **Undo Import**, sau đó nhập lại đúng phiên bản.

Backlog chính được hình thành:

1. `T1 - Xác nhận đặc tả Bank API`.
2. `T2 - Sửa lỗi giao dịch thanh toán trùng`.
3. `T3 - Kiểm thử regression payment`.
4. `T4 - Duyệt minh chứng kiểm thử`.
5. `T5 - Triển khai production`.

Lan gắn nhãn `Bank API`, `Critical`, `Security`, ghim `T2` và đặt deadline theo yêu cầu của ngân hàng.

**Dữ liệu sinh ra cho bước sau:** backlog chuẩn hóa, nhãn, deadline, mức ưu tiên và lịch sử phiên import.

---

### 09:30 - Kế hoạch tưởng khả thi nhưng nguồn lực không cho phép

Lan định giao `T2` cho Bình vì Bình hiểu Bank API nhất.

Trước khi giao, Minh mở Workload. Dữ liệu cho thấy Bình đang có 11 task mở, 4 task quá hạn và số giờ ước tính vượt xa các thành viên khác.

Lan mở AI Assignment Insight. AI đề xuất An là người thực hiện chính, Bình chỉ hỗ trợ review, dựa trên:

- Tải công việc hiện tại.
- Số task quá hạn.
- Lịch sử công việc liên quan.
- Tín hiệu kỹ năng.

Lan giao `T2` cho An và Bình, trong đó An chịu trách nhiệm chính. Chi nhận `T3`, Dũng nhận `T4`.

Nếu AI provider lỗi, màn hình vẫn còn dữ liệu workload thật để Lan quyết định. AI không trở thành điểm chặn của quy trình.

Minh tạo sprint **Emergency Release - 72 Hours** và liên kết:

`T1 -> T2 -> T3 -> T4 -> T5`

Khi Minh thử nối `T5 -> T1`, Qaly phát hiện dependency vòng và từ chối. Nhờ đó, kế hoạch không bị tạo thành một chu trình không thể hoàn tất.

Gantt cho thấy `T5` phụ thuộc toàn bộ chuỗi phía trước. Chỉ cần `T2` trễ, thời điểm phát hành lập tức bị đe dọa.

**Dữ liệu sinh ra cho bước sau:** assignee, sprint, dependency, workload và đường găng.

---

### 12:00 - Công việc được giao nhưng chưa ai thật sự bắt đầu

Dashboard Attention báo:

- `T2` là task Critical sắp quá hạn.
- An đã được giao nhưng chưa mở task.
- Một số task của Bình đã trì trệ.
- `T3`, `T4`, `T5` đang bị chặn bởi dependency.

Minh gửi nudge cho An. An nhận notification, mở task và tín hiệu “chưa xem” biến mất.

An chuyển task từ `Todo` sang `InProgress`, bật timer và đọc tài liệu đã ghim trong nhóm. Khi An vô tình bật timer ở một task khác, timer cũ tự dừng, bảo đảm tại một thời điểm không ghi nhận hai công việc song song.

Hạnh muốn xem phân tích rủi ro nội bộ nhưng chưa được cấp quyền. Qaly không hiển thị dữ liệu nhạy cảm. Lan chỉ chia sẻ dashboard tiến độ phù hợp với vai trò Customer.

**Dữ liệu sinh ra cho bước sau:** lượt xem task, notification, trạng thái thực thi và time entry.

---

### 16:00 - Một câu nói trong cuộc họp làm thay đổi toàn bộ kế hoạch

Ngân hàng yêu cầu họp khẩn. Minh bắt đầu cuộc họp ngay trong nhóm; các thành viên tham gia và presence được cập nhật.

Trong cuộc họp, đại diện ngân hàng nói:

> API mới không chỉ cần chống gửi trùng. Mỗi request còn phải có idempotency key và cơ chế retry an toàn.

Nhóm import biên bản Meetily. Group AI tóm tắt và trích xuất:

- Quyết định: bắt buộc bổ sung idempotency key.
- Action item: cập nhật API client.
- Action item: bổ sung test retry.
- Câu hỏi chưa giải quyết: thời gian lưu idempotency key.
- Nguồn bằng chứng: nội dung phát biểu trong biên bản.

Action item “cập nhật API client” trùng với phạm vi `T2`, nên Minh **liên kết vào task hiện có** thay vì tạo task mới.

Action item “bổ sung test retry” chưa tồn tại, nên Minh tạo:

`T2.1 - Kiểm thử retry và idempotency`

Task mới được chèn vào dependency:

`T2 -> T2.1 -> T3`

Gantt lập tức cho thấy đường găng dài hơn, deadline phát hành có nguy cơ trượt.

Nếu AI không trích xuất được assignee hoặc deadline, Minh xác nhận thủ công trước khi tạo task. Dữ liệu AI không tự động trở thành cam kết chính thức.

**Dữ liệu sinh ra cho bước sau:** quyết định cuộc họp, task mới, nguồn truy vết và đường găng cập nhật.

---

### 20:30 - Hai người cùng sửa một sự thật

An phát hiện cần chờ ngân hàng xác nhận thời gian lưu idempotency key. Anh định chuyển `T2` sang `OnHold`.

Cùng lúc, ở trình duyệt khác, Minh vừa nhận được câu trả lời từ ngân hàng và chuyển `T2` sang `InReview`.

An gửi cập nhật dựa trên phiên bản task cũ. Qaly phát hiện xung đột `rowVersion`, không cho dữ liệu cũ ghi đè lên thay đổi mới và yêu cầu tải lại.

Sau khi refresh, An thấy câu trả lời mới, giữ trạng thái `InReview` và tiếp tục bàn giao cho Chi.

Ngay sau đó, Lan thử kéo `T1` trực tiếp từ `Todo` sang `Done`. Qaly chặn vì đây không phải chuyển trạng thái hợp lệ. Lan phải đưa task qua luồng thực tế thay vì làm đẹp báo cáo.

**Dữ liệu sinh ra cho bước sau:** trạng thái đáng tin cậy, lịch sử thay đổi và phiên bản task mới nhất.

---

### Thứ Bảy 08:00 - Bản vá “đã xong” nhưng chưa thể đóng

An hoàn thành code, dừng timer và bình luận trong `T2`:

- Phạm vi thay đổi.
- Cách tái hiện lỗi cũ.
- Commit/build dùng để kiểm thử.

Chi chạy regression, thêm time entry thủ công và tải lên:

- Ảnh kết quả kiểm thử.
- Log request/response.
- Báo cáo test.

Chi đánh dấu báo cáo là **Evidence**. Trạng thái bằng chứng là `Pending`.

An thử chuyển `T2` sang `Done`. Qaly từ chối vì task chưa có evidence được duyệt.

Dũng review và từ chối bằng chứng với lý do:

> Log đã che mất idempotency key nên chưa chứng minh được hai request trả về cùng một giao dịch.

Chi nhận notification, chạy lại test và tải bằng chứng mới. Dũng duyệt bản mới. Lúc này `T2` mới đủ điều kiện chuyển sang `Done`.

Việc `T2` hoàn thành giải phóng `T2.1`, sau đó mới tới `T3`, `T4` và `T5`. Evidence không phải một file trang trí; nó trực tiếp điều khiển tiến độ của chuỗi dependency.

**Dữ liệu sinh ra cho bước sau:** bằng chứng được phê duyệt, thời gian thực tế, task hoàn thành và các task kế tiếp được mở khóa.

---

### Thứ Bảy 18:00 - Tài liệu triển khai trở thành một phần của kiểm soát

Minh import tài liệu rollback từ DOCX vào Wiki của dự án. Một ZIP khác chứa:

- Hướng dẫn Markdown hợp lệ.
- File TXT checklist.
- File HTML runbook.
- Một PDF chưa được hỗ trợ trong luồng này.

Qaly preview các file sẽ nhập và cảnh báo PDF bị bỏ qua, thay vì báo thành công giả.

Lan tạo task riêng tư `Rotate production credentials`, chỉ giao cho người cần biết. Hạnh vẫn xem được tiến độ chung nhưng không nhìn thấy nội dung task bảo mật.

Một tài khoản ngoài dự án lấy được ID task từ ảnh chụp và gọi trực tiếp API. Qaly từ chối truy cập. Tình huống được kiểm tra lại qua audit log, chứng minh phân quyền không chỉ nằm ở giao diện.

Wiki rollback vừa nhập được liên kết trong task triển khai `T5`, để người thực hiện không phải tìm tài liệu ở hệ thống khác.

**Dữ liệu sinh ra cho bước sau:** runbook triển khai, task bảo mật, dấu vết truy cập và phương án rollback.

---

### Chủ Nhật 22:00 - Dashboard đẹp nhưng sự thật vẫn nguy hiểm

Dashboard báo dự án hoàn thành 92%. Tuy nhiên:

- `T5 - Triển khai production` vẫn bị chặn.
- Một test retry đang quá hạn.
- Actual hours đã vượt estimated hours.
- Bình vẫn có workload cao.
- Task Critical còn nằm trên đường găng.

Lan hỏi Erumi:

> Tóm tắt rủi ro phát hành NovaPay và cho biết task nào đang chặn production.

AI tổng hợp từ dữ liệu dự án, chỉ ra test retry là nút thắt và đề xuất phát hành giới hạn. Lan không chấp nhận kết luận chỉ vì AI nói vậy; cô mở Gantt, workload, evidence và analytics để đối chiếu.

Nhóm quay lại poll ban đầu và chọn phương án thứ ba:

**Phát hành giới hạn, tạm tắt thanh toán nhanh.**

Lan:

1. Chuyển phần tính năng chưa an toàn sang `OnHold`.
2. Ghi lý do trong comment.
3. Cập nhật quyết định vào Wiki.
4. Điều chỉnh phạm vi `T5`.
5. Gửi notification cho các bên liên quan.

Dashboard thay đổi theo dữ liệu thực, không ép các task chưa hoàn thành thành `Done`.

**Dữ liệu sinh ra cho bước sau:** quyết định phát hành có căn cứ, phạm vi triển khai mới và thông báo chính thức.

---

### Thứ Hai 08:45 - Phát hành và xử lý ngoại lệ cuối cùng

Dũng xác nhận evidence cuối. Minh kiểm tra:

- Không còn task bắt buộc nào chặn `T5`.
- Runbook và rollback đã có trong Wiki.
- Người triển khai có quyền phù hợp.
- Time entry và audit trail đầy đủ.

Lan test webhook thông báo phát hành. Endpoint đối tác đầu tiên timeout. Qaly ghi nhận lỗi giao nhận nhưng không làm hỏng giao dịch cập nhật dự án.

Lan sửa endpoint và test lại thành công.

Minh chuyển `T5` qua `InProgress`, thực hiện runbook, rồi đưa sang `InReview`. Sau bước xác nhận production, evidence triển khai được duyệt và `T5` chuyển sang `Done`.

Dashboard, analytics, recent activity và notification đồng thời phản ánh kết quả phát hành.

## 5. Hậu kiểm: Toàn bộ câu chuyện phải truy vết được

Sau phát hành, kiểm toán hỏi:

1. Vì sao task kiểm thử retry được tạo?
2. Câu nói nào trong cuộc họp dẫn tới yêu cầu đó?
3. Ai được giao task và khi nào họ thực sự mở nó?
4. Vì sao không giao lỗi chính cho Bình?
5. Ai thay đổi trạng thái và deadline?
6. Có ai ghi đè dữ liệu của người khác không?
7. Bằng chứng nào bị từ chối và lý do là gì?
8. Ai duyệt bằng chứng cuối?
9. Thời gian thực tế vượt ước tính bao nhiêu?
10. Vì sao công ty chọn phát hành giới hạn?
11. Tài khoản ngoài dự án đã cố truy cập gì?
12. Webhook phát hành đã thất bại và được gửi lại ra sao?

Người demo trả lời bằng dữ liệu đã hình thành tự nhiên trong cùng kịch bản:

- Meeting source và action-item mapping.
- Task, comment và notification.
- Workload và AI assignment insight.
- Gantt, dependency và attention signal.
- Row version và audit log.
- Attachment/evidence review.
- Time entry và analytics.
- Poll, Wiki quyết định và webhook delivery log.

## 6. Quan hệ phối hợp giữa các chức năng

| Chức năng đầu vào | Tác động nghiệp vụ tiếp theo |
|---|---|
| Group, invitation, role | Xác định ai được tham gia và thao tác trong dự án |
| Poll | Tạo phương án xử lý ban đầu và căn cứ cho quyết định cuối |
| Import | Tạo backlog dùng cho Kanban, sprint, workload và analytics |
| Workload + AI | Quyết định assignee, ảnh hưởng tốc độ xử lý |
| Sprint + dependency | Tạo đường găng và xác định task bị chặn |
| Task viewed + nudge | Biến việc “đã giao” thành “đã tiếp nhận” |
| Meeting + AI action item | Làm thay đổi backlog và đường găng |
| Row version | Bảo vệ trạng thái task khi nhiều người cùng thao tác |
| Timer/time entry | Cung cấp actual hours cho workload và analytics |
| Attachment + evidence review | Quyết định task có được phép hoàn thành hay không |
| Wiki | Cung cấp runbook và lưu quyết định phát hành |
| Private task + permission | Bảo vệ thông tin nhạy cảm xuyên suốt UI và API |
| Dashboard + analytics + AI | Cung cấp căn cứ tổng hợp để PM ra quyết định |
| Webhook + notification | Phát hành thông tin ra trong và ngoài hệ thống |
| Audit log | Khép kín trách nhiệm và truy vết sau sự cố |

## 7. Các ngoại lệ bắt buộc trong cùng câu chuyện

Không tách ngoại lệ thành phần demo riêng. Chèn chúng đúng thời điểm trong luồng:

1. Viewer sửa dự án bị chặn khi vừa thành lập dự án.
2. Import file cũ rồi Undo khi tạo backlog.
3. Dependency vòng bị chặn khi lập kế hoạch.
4. AI lỗi nhưng PM vẫn phân công bằng workload.
5. Assignee chưa xem task và nhận nudge khi thực thi bắt đầu chậm.
6. AI meeting thiếu dữ liệu và cần người xác nhận.
7. Hai người cập nhật cùng task gây concurrency conflict.
8. Chuyển trạng thái sai workflow bị chặn.
9. Task không thể `Done` khi evidence chưa được duyệt.
10. Evidence bị từ chối và phải nộp lại.
11. File PDF không hỗ trợ bị bỏ qua có cảnh báo.
12. Người ngoài dự án gọi API bị từ chối.
13. Dashboard phần trăm cao nhưng task đường găng vẫn chặn phát hành.
14. Webhook timeout nhưng không làm hỏng nghiệp vụ chính.

## 8. Nhịp trình diễn đề xuất

| Thời gian | Diễn biến |
|---|---|
| 0-5 phút | Nhận khủng hoảng, lập nhóm, poll, tạo dự án và phân quyền |
| 5-10 phút | Import backlog lỗi, Undo và nhập lại |
| 10-16 phút | Workload, AI phân công, sprint, dependency và Gantt |
| 16-20 phút | Attention, nudge, notification và timer |
| 20-27 phút | Họp, AI action item, tạo/link task và cập nhật đường găng |
| 27-31 phút | Hai người sửa cùng lúc và xử lý xung đột |
| 31-38 phút | Kiểm thử, evidence bị từ chối, nộp lại và duyệt |
| 38-43 phút | Wiki, task riêng tư và truy cập trái phép |
| 43-48 phút | Dashboard, analytics, AI và quyết định phát hành giới hạn |
| 48-52 phút | Webhook lỗi, gửi lại, hoàn tất phát hành và hậu kiểm |

## 9. Chuẩn bị dữ liệu demo

- Dùng ít nhất hai trình duyệt hoặc profile để diễn concurrency và phân quyền.
- Chuẩn bị file Excel cũ, file Excel đúng và dữ liệu trùng.
- Cố định deadline để Attention và Gantt hiển thị đúng trong buổi demo.
- Chuẩn bị một biên bản Meetily có action item trùng và action item mới.
- Chuẩn bị hai evidence: một bản thiếu dữ liệu và một bản hợp lệ.
- Chuẩn bị DOCX/Markdown/TXT/HTML hợp lệ và một PDF làm ngoại lệ.
- Chuẩn bị một webhook timeout và một webhook hoạt động.
- Có phương án fallback nếu LiveKit hoặc AI provider không khả dụng.
- Không dùng credential production thật trong task, Wiki, file hoặc màn hình trình chiếu.
