# Báo cáo những thay đổi tích cực của Qaly trong tuần

**Thời gian tổng hợp:** 21/07/2026 – 27/07/2026  
**Nguồn đánh giá:** Lịch sử commit, mã nguồn giao diện, API, migration và kiểm thử trong repository Qaly.

## 1. Tổng quan

Trong tuần qua, Qaly có bước tiến rõ rệt từ một ứng dụng quản lý dự án đơn lẻ sang một nền tảng có khả năng phục vụ nhiều tổ chức, quản lý người dùng chặt chẽ hơn và sử dụng AI thực tế hơn trong công việc.

Các thay đổi tích cực tập trung vào năm hướng chính:

1. Hoàn thiện quản lý tổ chức và thành viên.
2. Tăng cường phân quyền và bảo mật.
3. Đưa AI vào theo dõi tiến độ và phân tích kỹ năng.
4. Cải thiện trải nghiệm giao diện và luồng thao tác.
5. Bổ sung kiểm thử, khả năng phục hồi và tài liệu vận hành.

## 2. Quản lý tổ chức được hoàn thiện

Qaly đã bổ sung trang **Quản lý tổ chức** và **Thành viên tổ chức**, cùng với API và mô hình dữ liệu tương ứng.

### Chức năng mới

- Tạo, chỉnh sửa và vô hiệu hóa tổ chức.
- Chọn chủ sở hữu cho tổ chức.
- Xem số lượng thành viên và dự án của từng tổ chức.
- Tìm kiếm và lọc tổ chức theo trạng thái.
- Thêm thành viên bằng email.
- Thay đổi vai trò hoặc gỡ thành viên khỏi tổ chức.
- Tách quyền trong tổ chức khỏi quyền quản trị toàn hệ thống.

### Giá trị mang lại

- Cho phép một hệ thống Qaly phục vụ nhiều công ty, phòng ban hoặc nhóm khách hàng.
- Giúp dữ liệu, thành viên và trách nhiệm được tổ chức rõ ràng.
- Giảm thao tác cấp quyền lặp lại trên từng dự án.
- Tạo nền tảng để phát triển Qaly thành sản phẩm SaaS dành cho doanh nghiệp.

## 3. Phân quyền và bảo mật rõ ràng hơn

Hệ thống đã bổ sung các trang **Quản lý người dùng** và **Ủy quyền Moderator**, đồng thời hoàn thiện các quy tắc quyền ở backend.

### Chức năng mới

- Admin có thể tạo, cập nhật, khóa hoặc mở tài khoản.
- Có thể thu hồi các phiên đăng nhập của người dùng.
- Có thể xem dự án mà một người sở hữu hoặc tham gia.
- Có thể điều chỉnh vai trò của thành viên trong từng dự án.
- Hệ thống duy trì nguyên tắc chỉ có một Admin cao nhất.
- Moderator chỉ được cấp quyền trên một tổ chức cụ thể.
- Quyền Moderator được chia nhỏ thành xem, thêm, đổi vai trò và gỡ thành viên.
- Quyền được đặt thời hạn và có thể thu hồi ngay.

### Giá trị mang lại

- Hạn chế việc cấp quyền Admin quá rộng chỉ để xử lý một công việc hỗ trợ.
- Giảm nguy cơ truy cập hoặc thay đổi dữ liệu ngoài phạm vi trách nhiệm.
- Hỗ trợ tốt hơn khi có nhân viên mới, nhân viên nghỉ việc hoặc tài khoản gặp sự cố.
- Làm rõ ba lớp quyền: toàn hệ thống, trong tổ chức và trong dự án.

## 4. AI gắn chặt hơn với công việc thực tế

Các tính năng AI đã được mở rộng theo hướng có cấu trúc và hữu ích cho quản lý dự án.

### Thay đổi chính

- Bổ sung AI tóm tắt tiến độ dự án và sprint.
- Bổ sung gợi ý kỹ năng cần thiết cho nhiệm vụ.
- Thêm thẻ AI về tiến độ dự án và kỹ năng nhiệm vụ trên giao diện.
- Cải thiện AI Planner và các luồng AI trong chat.
- Bổ sung schema kiểm tra đầu ra AI cho phần tóm tắt tiến độ và đề xuất kỹ năng.
- Bổ sung kiểm soát nguồn, ngân sách sử dụng và chi phí AI.
- Cải thiện cơ chế xử lý job và lựa chọn nhà cung cấp AI.

### Giá trị mang lại

- Người quản lý có thể nhận biết nhanh tiến độ, điểm nghẽn và rủi ro.
- Việc giao nhiệm vụ có thêm căn cứ về kỹ năng cần thiết.
- Đầu ra AI nhất quán và dễ kiểm tra hơn nhờ schema.
- Kiểm soát tốt hơn chi phí và mức sử dụng AI.
- Giảm khả năng AI đưa ra kết quả thiếu căn cứ hoặc sai định dạng.

## 5. Giao diện và trải nghiệm sử dụng được cải thiện

Nhiều màn hình quan trọng như Dự án, Chi tiết dự án, Nhiệm vụ, AI Planner và thanh điều hướng đã được chỉnh sửa.

### Thay đổi chính

- Hoàn thiện giao diện trang Nhiệm vụ.
- Cải thiện danh sách, lưới và thanh công cụ dự án.
- Cải thiện tab bản đồ demo của dự án.
- Sửa luồng hiển thị người dùng đã đăng nhập trên sidebar.
- Thêm hộp thoại xác nhận dùng chung cho thao tác quan trọng.
- Thêm trạng thái tải, trạng thái rỗng, thông báo thành công và thông báo lỗi.
- Bổ sung trang báo lỗi cho URL sai hoặc không tồn tại.
- Cải thiện khả năng hiển thị trên màn hình nhỏ.

### Giá trị mang lại

- Người dùng hiểu rõ hệ thống đang tải, đã hoàn thành hay gặp lỗi.
- Các thao tác nguy hiểm như gỡ thành viên hoặc thu hồi quyền khó bị bấm nhầm.
- Điều hướng nhất quán hơn giữa các khu vực.
- Giảm tình trạng trang trắng hoặc thông báo kỹ thuật khó hiểu.
- Trải nghiệm sử dụng trên điện thoại và màn hình nhỏ tốt hơn.

## 6. Chất lượng và khả năng vận hành được nâng cao

Tuần qua không chỉ bổ sung tính năng mà còn tăng cường kiểm thử và khả năng phục hồi của hệ thống.

### Thay đổi chính

- Bổ sung kiểm thử phân quyền người dùng và thành viên tổ chức.
- Bổ sung kiểm thử AI: ngân sách, tiến độ, kỹ năng, router, bảo mật và nguồn dữ liệu.
- Mở rộng kiểm thử E2E cho sidebar, cài đặt, quyền riêng tư và các luồng AI.
- Bổ sung kiểm tra tính toàn vẹn cho chính sách ngân sách AI.
- Thêm tài liệu phục hồi clean restore và quản trị phát hành.
- Đồng bộ frontend bundle với môi trường Linux CI.
- Cải thiện Redis session và khả năng hoạt động khi một số dịch vụ suy giảm.

### Giá trị mang lại

- Giảm nguy cơ lỗi cũ quay trở lại sau khi cập nhật.
- Tăng độ tin cậy khi triển khai trên môi trường khác nhau.
- Có quy trình rõ ràng hơn để phục hồi khi build hoặc dependency gặp sự cố.
- Các ranh giới bảo mật quan trọng được kiểm tra tự động thay vì chỉ dựa vào thao tác thủ công.

## 7. Tác động tổng thể

Những thay đổi trong tuần giúp Qaly tiến bộ trên cả ba mặt:

| Mặt cải thiện | Kết quả tích cực |
|---|---|
| Sản phẩm | Có thêm quản lý tổ chức, người dùng và AI hỗ trợ công việc |
| Bảo mật | Quyền được giới hạn theo hệ thống, tổ chức, dự án và capability |
| Vận hành | Có thêm kiểm thử, kiểm soát chi phí AI và tài liệu phục hồi |

Điểm đáng chú ý nhất là các tính năng mới không tồn tại riêng lẻ. Quản lý tổ chức kết nối với quản lý thành viên; phân quyền Moderator kết nối với capability; AI kết nối với tiến độ và kỹ năng nhiệm vụ; còn các luồng này đều được bổ sung kiểm thử tương ứng.

## 8. Kết luận

Tuần 21–27/07/2026 là một tuần phát triển tích cực của Qaly. Dự án đã:

- Có cấu trúc phù hợp hơn để phục vụ nhiều tổ chức.
- Quản lý tài khoản và quyền truy cập an toàn hơn.
- Biến AI thành công cụ hỗ trợ quản lý tiến độ và nhân lực.
- Cải thiện rõ trải nghiệm trên các màn hình chính.
- Tăng mức độ sẵn sàng cho kiểm thử và triển khai thực tế.

Nhóm thay đổi này tạo ra giá trị dài hạn: Qaly dễ mở rộng hơn, an toàn hơn và gần với một sản phẩm doanh nghiệp hoàn chỉnh hơn so với đầu tuần.
