# Bộ prompt và các bước kiểm tra UI — Luồng quản lý dự án

Cập nhật: 05/09/2026. Mục đích: thao tác trên preview, kiểm tra card/form/popup rồi annotation. Đây là checklist để chạy, **không phải chứng nhận UI đã PASS**. Không cần gửi mọi prompt trong một lần.

## 1. Chuẩn bị và cách phân biệt yêu cầu

- Khởi động lại backend sau khi lấy build mới; xem [SETUP_AND_RUN.md](SETUP_AND_RUN.md) để chạy preview thủ công.
- Dùng Admin hoặc Project Manager cho tạo/sửa/phân công. Dùng tài khoản Member thật cho thực hiện task và Reviewer có quyền cho duyệt. “Xem vai trò” chỉ đọc không thay thế kiểm thử đăng nhập thật.
- Chọn một Project test có member, Sprint và capacity. Có thể dùng **Qaly Work OS - Customer Demo** nếu seed này còn trong database, hoặc Project vừa tạo ở bước 2.
- Thay `[NGƯỜI NHẬN]` bằng họ tên thực trong tab **Thành viên** của chính Project, ví dụ `Hoàng Tuấn Kiệt` nếu người này có trong danh sách. Không dán nguyên placeholder. Tên ngắn chỉ được dùng khi khớp duy nhất; không tìm thấy/trùng tên thì bổ sung họ tên.
- Giữ cùng session trong mỗi chuỗi. Luôn nhìn chip Project trước khi gửi. Không lặp prompt tạo mới để kiểm tra retry; dùng lại card/receipt cũ.
- Mỗi lần xác nhận: mở link kết quả → reload → kiểm tra dữ liệu thật. Toast không đủ để kết luận đã lưu.

| Ý định | Cách nói | Kết quả đúng |
| --- | --- | --- |
| Tạo mới và giao | `Tạo 3 task: Sửa đăng nhập, Viết testcase, Kiểm tra thanh toán; giao cho [NGƯỜI NHẬN].` | Ba task riêng; tên người ở ô Người thực hiện, không nằm trong tên/mô tả task. |
| Giao task có sẵn | `Giao Task đang mở cho [NGƯỜI NHẬN]; mở card review.` | Card phân công task hiện có; không mở Task Composer và không tạo task mới. |
| Giao nhiều task có sẵn | `Giao 3 task đã chọn cho [NGƯỜI NHẬN].` | Chỉ xử lý đúng selection gồm ba task. Nếu bề mặt chưa gửi selection, AI phải hướng dẫn chọn task ở Phân công & Capacity; không tự lấy ba task bất kỳ hay chỉ xử lý task đầu tiên. |
| Thiếu nội dung tạo mới | `Tạo 3 task giao cho [NGƯỜI NHẬN].` | Hỏi các task làm gì; không tạo task tên “giao 3 task…”. |
| Giữ backlog | `Tạo 1 task: Rà soát thông báo lỗi; để chưa giao.` | Không tự chọn assignee. |

**Lưu ý nghiệp vụ:** người được chỉ định trong prompt tạo task là lựa chọn của bạn để review, không có nghĩa AI chứng nhận người đó đủ skill/capacity. Card phân công task có sẵn vẫn kiểm tra evidence/capacity/lịch; nếu không phù hợp phải giữ lựa chọn, báo blocker và cho bạn chọn phương án khác.

## 2. Tạo Project → phạm vi → đội hình → xác nhận

Trang `/dashboard` hoặc `/projects` → Trợ lý AI → Phiên mới → scope **Tất cả dự án**.

### Q01 — Project brief

```text
Khởi chạy Project DEMO-UI-20260905 cho website đặt dịch vụ. Thời hạn 12 tuần; người dùng chính là khách hàng cá nhân. Phạm vi bắt buộc: đăng ký/đăng nhập, đặt dịch vụ, thanh toán và dashboard quản lý. Mục tiêu: luồng đặt dịch vụ E2E đạt 95% và không còn lỗi Critical khi nghiệm thu. Mở Launch Brief để tôi chỉnh; chưa tạo dữ liệu. Chỉ hỏi tối đa ba thông tin thực sự còn thiếu.
```

Kiểm tra UI: label và dấu bắt buộc rõ; dropdown không tràn; mô tả field vừa đủ; sửa nhiều ô không tự gửi; không có hai form hỏi trùng nhau. Thử option **Khác**, thêm/bỏ một chức năng, collapse phần nâng cao.

### Q02 — Nhân sự và kế hoạch

```text
Lập ba phương án manager/team dựa trên skill evidence đã xác nhận, capacity được khai báo, lịch vắng và tải đa Project. Chia Sprint, Task, dependency, estimate và required skill theo phạm vi đã duyệt. Cho tôi đổi người, quy mô team, Sprint và Task trước xác nhận. Không coi lịch trống là capacity.
```

Thao tác: đổi một thành viên, mở capacity từng tuần, sửa estimate, đổi cách phân công rồi lưu kiểm tra lại.

Kiểm tra UI: hàng checkbox/tên/vai trò/giờ thẳng hàng; tên dài không đẩy nút ra ngoài; phần cảnh báo gọn; mỗi blocker có cách quay đúng field để sửa; lựa chọn không tự trở về mặc định.

### Q03 — Review trước tạo

```text
Dùng phương án đang chọn. Hiện card review cuối gồm Project, manager/team, Sprint và tổng số Task. Chờ tôi bấm xác nhận; chưa ghi dữ liệu.
```

Chỉ bấm **Xác nhận tạo Project** khi nội dung đúng và hết blocker. Nếu bị khóa, annotation tooltip/lý do/nút dẫn tới blocker thay vì tìm cách bỏ validation.

Sau xác nhận: mở Project từ receipt, reload, kiểm tra member/Sprint/task/dependency. Chú ý footer card không bị ô nhập chat che khuất.

## 3. Tạo Task → gán người → chỉnh bản nháp

Mở Project test → tab **Nhiệm vụ** → chọn đúng Project trong Trợ lý AI. Dùng một trong Q04–Q06 trước; đây là các lượt tạo độc lập.

### Q04 — Ba task có nội dung riêng và cùng một người nhận

```text
Tạo 3 task: Sửa form đăng nhập, Viết testcase đăng nhập, Kiểm tra lỗi phiên đăng nhập; giao cho [NGƯỜI NHẬN]. Mỗi task có mô tả, tiêu chí nghiệm thu, estimate và priority. Mở bản nháp để tôi chỉnh; chưa ghi dữ liệu.
```

Kiểm tra đúng **ba** card. Ô Người thực hiện phải là người đã chỉ định, không phải người AI tự chọn. Tên/mô tả không chứa câu “giao 3 task cho…”. Nếu cần Sprint, chọn/kiểm tra Sprint trong composer trước confirm; đừng chỉ tin tên Sprint được nhắc trong text.

Thử sửa tên task thứ hai, priority task thứ ba, estimate task đầu; bỏ tick một task để xem số lượng xác nhận cập nhật, rồi tick lại. Xác nhận một lần → reload thấy đủ ba task, mỗi task đúng assignee.

### Q05 — Một task, tiêu đề đặt trong dấu ngoặc kép

```text
Tạo 1 task tên "DEMO-UI — Rà soát thông báo lỗi đăng nhập"; giao cho [NGƯỜI NHẬN]. Mở bản nháp, chưa lưu.
```

Kiểm tra không biến thành ba task mặc định; title giữ nguyên dấu tiếng Việt. Người nhận vẫn nằm ở field riêng.

### Q06 — Backlog chưa giao

```text
Tạo 1 task: DEMO-UI — Bổ sung trạng thái rỗng cho danh sách dự án; để chưa giao. Mở bản nháp để tôi duyệt.
```

Kiểm tra assignee **Chưa giao** giữ nguyên; label/date/estimate cùng hàng khi đủ rộng, tự xuống hàng hợp lý khi thu nhỏ panel.

### Q07 — Trường hợp thiếu dữ liệu và sai tên (không xác nhận)

```text
Tạo 3 task giao cho [NGƯỜI NHẬN].
```

Phải hỏi nội dung công việc. Sau đó thử:

```text
Tạo 1 task sửa đăng nhập; giao cho người KHONG-TON-TAI-UI-TEST.
```

Phải báo không tìm thấy thành viên trong Project và hướng dẫn kiểm tra danh sách; không tự chọn người khác. Annotation nếu chỉ hiện toast khó đọc hoặc không có đường tiếp tục.

## 4. Giao task có sẵn → checklist → subtask

Mở chi tiết một task chưa đóng. Không chạy trên task Done.

### Q08 — Phân công đúng người, chưa mutation

```text
Giao Task đang mở cho [NGƯỜI NHẬN]; kiểm tra required skill, evidence đã xác nhận, capacity, availability và tải đa Project. Mở card review để tôi đổi người hoặc lịch trước khi xác nhận.
```

Kiểm tra card trỏ đúng task, đúng người. Khi bị blocker, đọc lý do và thử ứng viên/lịch khác. Khi hợp lệ mới xác nhận → reload → assignee/lịch đúng và các field khác không mất.

### Q09 — Checklist nghiệm thu

```text
Với Task đang mở, soạn 5 mục acceptance checklist kiểm chứng được. Cho phép sửa từng mục và chờ một xác nhận trước khi lưu.
```

Thử nội dung một mục dài hai dòng; kiểm tra checkbox/input/nút xóa căn hàng, không mất focus khi nhập. Confirm → reload phải có năm mục.

### Q10 — Bốn subtask

```text
Tách Task đang mở thành 4 subtask theo thứ tự thực hiện, có dependency, estimate và required skill. Mở card review trước khi tạo.
```

Kiểm tra bốn dòng/card, trường required skill sửa được, dependency không dùng ID khó hiểu nếu có tên task; nút thêm/bỏ/chọn không sát nhau. Confirm → reload đúng parent và số subtask.

## 5. Thực hiện → gửi duyệt → trả lại → hoàn thành

Đoạn này **thao tác bằng UI**, không yêu cầu chatbot bỏ qua review/evidence gate. Dùng task được cấu hình reviewer độc lập và quy trình yêu cầu duyệt/evidence.

1. Đăng nhập Member được giao task → mở Task detail → chuyển **Đang làm**. Kiểm tra dropdown trạng thái và các field chỉ đọc/được sửa.
2. Tick checklist, ghi thời gian nếu cần, thêm evidence thật (file/link hợp lệ). Kiểm tra form upload/link, progress/loading, lỗi validation và nút hủy.
3. Gửi duyệt/chuyển **Đang duyệt**. Thử kéo thẳng sang **Hoàn thành**: không được bỏ qua review gate. Annotation thông báo và nơi dẫn tới bước còn thiếu.
4. Đăng nhập reviewer được cấu hình → mở task → xem evidence → **Từ chối/Yêu cầu chỉnh sửa** với lý do cụ thể. Kiểm tra popup có focus, nút hủy/xác nhận và lỗi khi bỏ trống lý do.
5. Quay lại Member → sửa evidence → gửi duyệt lại. Evidence cũ/lý do trả lại phải đọc được.
6. Reviewer duyệt evidence và đóng task theo workflow. Reload: trạng thái Done, reviewer/evidence/checklist/activity đúng. Thử kéo task Done về cột trước: không được mở lại tùy tiện; nếu có workflow mở lại hợp lệ thì phải có quyền và xác nhận riêng.

Nếu dùng seed demo, có thể mở Wiki **Kịch bản phản biện workflow quản lý dự án** và task `DEMO-QA 01`–`DEMO-QA 07` để xem từng trạng thái. Không sửa/xóa hàng loạt dữ liệu baseline chỉ để “làm đẹp” demo.

## 6. Báo cáo → kế hoạch → nguồn và quyền

### Q11 — Tổng quan Project bằng số liệu

```text
Phân tích Project đang chọn: tiến độ Sprint, task quá hạn, workload và ba việc ưu tiên. Trả ngắn gọn bằng metric, bảng và biểu đồ có dữ liệu, kèm link nguồn. Thu gọn phần quy trình mặc định.
```

Chạy đối chiếu tại **Phân tích** với cùng Project/filter. Kiểm tra cards gọn, cột rõ đơn vị, bảng không tràn, chart hover dễ đọc; **quá hạn là điều kiện thời gian chồng lấp trạng thái**, không cộng riêng vào tổng task.

### Q12 — Kiểm tra tùy chọn trình bày

```text
Phân tích Project chi tiết, không cần biểu đồ; giữ bảng và số liệu.
```

Tiếp theo:

```text
Phân tích Project chi tiết, không cần bảng; giữ số liệu và biểu đồ phù hợp.
```

Không có dữ liệu để vẽ thì không cần chart rỗng. Annotation nếu mất cả metric khi chỉ tắt chart, hoặc khoảng trắng quá lớn.

### Q13 — Đề xuất đổi Sprint

```text
Đánh giá roadmap Project hiện tại và đề xuất điều chỉnh Sprint theo dependency, capacity và deadline. Hiện before/after, cho tôi chọn từng thay đổi; không ghi trước xác nhận.
```

Kiểm tra hai phía before/after cân hàng, ngày/tháng rõ ràng, checkbox và nút xác nhận không bị khuất. Bỏ chọn một thay đổi rồi kiểm tra chỉ phần đã chọn được lưu.

### Q14 — Wiki → đề xuất task

Mở một Wiki thật của Project, rồi gửi:

```text
Tóm tắt Wiki đang mở thành brief có link tới section nguồn; đề xuất tối đa 3 Task tùy chọn. Chỉ tạo các Task tôi tick chọn sau một xác nhận.
```

Kiểm tra source/deep-link, checkbox và nút tạo có đếm số đã chọn. Không tick thì không phát receipt tạo thành công.

### Q15 — Kiểm tra Member chỉ đọc

Đăng nhập Member/Viewer có quyền đọc nhưng không quyền quản lý Project:

```text
Tóm tắt Project và các task tôi được phép xem, nêu ba việc cần chú ý và nút mở nguồn. Nếu tôi không có quyền tạo hoặc giao task, vẫn trả phân tích hữu ích nhưng không hiện nút xác nhận mutation.
```

Kiểm tra vẫn có nội dung hữu ích, không lộ task private và không có control quản lý ngoài quyền.

## 7. Checklist annotation UI

Không cần đánh giá mọi pixel trong một lượt. Ưu tiên theo thứ tự: chặn thao tác → khó hiểu → che/khuất/tràn → căn hàng và độ gọn.

| Chỗ kiểm tra | Cần nhìn |
| --- | --- |
| Card/form | Label, input, checkbox và các nút thẳng hàng; spacing nhất quán; không card cao bất thường chỉ chứa một field. |
| Popup/drawer | Có tiêu đề, nút đóng, footer không bị composer che; cuộn đúng vùng; cancel không lưu nhầm. |
| Text dài | Tên task/member dài xuống dòng đúng; tooltip không bị cắt; không đè các cột/nút. |
| Trạng thái | Loading ngăn double submit; disabled có lý do; lỗi dẫn tới field cần sửa; empty state có bước tiếp theo. |
| Bảng/chart | Header nói rõ số lượng gì, đơn vị giờ/%; chart có label/tooltip dễ hiểu; không trùng lặp dữ liệu làm dài màn hình. |
| Confirm/receipt | Nêu đúng số mục sẽ tạo/sửa; link mở đúng đối tượng; reload vẫn đọc được kết quả. |
| Hai kích thước | Thử panel rộng và thu hẹp; không tràn ngang toàn trang, không che nút quan trọng. |

Mẫu annotation ngắn:

```text
Q04 · Admin · Project DEMO-UI-20260905 · card Task 2
Đang thấy: dropdown Người thực hiện lệch hàng khi tên dài.
Muốn: cùng hàng với Hạn chót ở panel rộng; panel hẹp tự xuống hàng, không cắt tên.
Sau reload: còn/không còn. Có chặn thao tác: có/không.
```

Bạn annotation trực tiếp vào vị trí lỗi trên preview; ghi mã Qxx giúp truy đúng prompt và bước. Chưa cần đổi cấu trúc UI trước khi có ảnh/vị trí cụ thể.
