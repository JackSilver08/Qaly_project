# 🎭 KỊCH BẢN CHUẨN BỊ DEMO AI NÂNG CAO (AI DEMO SCRIPT)

> [!NOTE]  
> Tài liệu này chuẩn bị kịch bản nghiệm thu cho 5 tính năng AI nâng cao của hệ thống trợ lý ảo Erumi AI và Group AI, bao gồm các câu hỏi mẫu, dữ liệu cần thiết để chuẩn bị trước, kết quả mong đợi và đánh giá rủi ro về hiệu năng (độ trễ khi chạy AI local).

---

## 📋 1. SCENARIO 1: ANALYTICS (Phân tích hiệu suất & so sánh Workspace)

*   **Mục tiêu:** Kiểm tra khả năng thu thập dữ liệu toàn hệ thống, tự động vẽ biểu đồ và phân tích tiến độ dự án của Erumi.
*   **Prompt mẫu:** 
    > *"So sánh tiến độ và hiệu suất của các dự án đang hoạt động trong Workspace."*
*   **Chuẩn bị dữ liệu:**
    *   Tạo ít nhất 2 dự án đang hoạt động (ví dụ: `DATN` và `QALY`).
    *   `DATN`: Có 4 nhiệm vụ (2 Hoàn thành, 1 Đang làm, 1 Quá hạn).
    *   `QALY`: Có 3 nhiệm vụ (1 Hoàn thành, 2 Đang làm, 0 Quá hạn).
*   **Kết quả mong đợi:**
    *   Erumi AI phản hồi bằng văn bản phân tích so sánh 2 dự án bằng tiếng Việt.
    *   Bảng dữ liệu động hiển thị danh sách các dự án kèm số lượng task, tiến độ % và chỉ số rủi ro.
    *   Biểu đồ dạng cột (Bar chart) trực quan hiển thị tiến độ % hoàn thành của từng dự án.
    *   Metric cards hiển thị: Tổng dự án, Tổng số task đang có rủi ro quá hạn.
*   **Đánh giá rủi ro & Cách khắc phục:**
    *   *Rủi ro:* Mô hình AI chạy local trên Ollama (Llama 3.2) có thể tốn từ 3-8 giây để phân tích và sinh mã JSON cấu trúc đầy đủ.
    *   *Khắc phục:* Giao diện Analytics hiển thị trạng thái `isChatting` với hiệu ứng typing indicator rõ ràng, đồng thời hiển thị thông số độ trễ thực tế ở góc phản hồi (`⏱️ Phản hồi 4200ms`) để người nghiệm thu hiểu và thông cảm.

---

## ⚠️ 2. SCENARIO 2: RISK ANALYSIS (Phân tích rủi ro tiến độ dự án)

*   **Mục tiêu:** AI tự động phát hiện các điểm nghẽn tiến độ, nhiệm vụ quá hạn và đưa ra đề xuất hành động.
*   **Prompt mẫu:**
    > *"Dự án DATN hiện tại đang có những rủi ro nào về mặt tiến độ?"*
*   **Chuẩn bị dữ liệu:**
    *   Chọn dự án `DATN` trong dropdown của Erumi chat.
    *   Tạo 1 nhiệm vụ có hạn chót (due date) trong quá khứ (ví dụ: Task *"Thiết kế Database"* hạn chót 3 ngày trước, trạng thái Đang làm).
*   **Kết quả mong đợi:**
    *   Hệ thống hiển thị cảnh báo an toàn dữ liệu nếu dự án có dưới 3 nhiệm vụ (*"Cảnh báo dữ liệu: Dự án này hiện tại có quá ít dữ liệu..."*).
    *   Erumi AI nhận diện chính xác nhiệm vụ *"Thiết kế Database"* đang bị quá hạn.
    *   Trả về mức độ rủi ro chung của dự án (Trung bình/Cao) và đề xuất 2-3 hành động cụ thể (ví dụ: san sẻ task, họp khẩn).
    *   Badge xuất xứ hiển thị rõ ràng: `🤖 Trả lời bởi Erumi AI` kèm theo tỷ lệ tự tin (`🎯 90% tin cậy`).
*   **Đánh giá rủi ro & Cách khắc phục:**
    *   *Rủi ro:* AI local có thể không tính toán được khoảng cách ngày trễ cụ thể.
    *   *Khắc phục:* Backend đã chuyển toàn bộ thông tin chi tiết về số ngày trễ thực tế từ C# database context vào prompt ngữ cảnh để AI chỉ việc trích xuất và trình bày.

---

## 📊 3. SCENARIO 3: WORKLOAD (Đánh giá năng suất & phân bổ công việc)

*   **Mục tiêu:** AI phân tích mức độ phân chia công việc giữa các thành viên và đề xuất người thực hiện phù hợp.
*   **Prompt mẫu:**
    > *"Thành viên nào đang chịu tải công việc cao nhất trong dự án DATN và giải pháp là gì?"*
*   **Chuẩn bị dữ liệu:**
    *   Dự án `DATN` có ít nhất 2 thành viên: `Chí Khang` và `Quang Tuấn`.
    *   Giao cho `Chí Khang` 3 nhiệm vụ (Tổng số giờ ước lượng là 24h).
    *   Giao cho `Quang Tuấn` 1 nhiệm vụ (Tổng số giờ là 4h).
*   **Kết quả mong đợi:**
    *   Phản hồi Erumi AI chỉ ra `Chí Khang` đang làm nhiều việc nhất.
    *   Bảng workload phân công hiển thị trực quan số lượng task và số giờ ước lượng của từng người.
    *   Đưa ra đề xuất chuyển bớt 1 nhiệm vụ từ `Chí Khang` sang cho `Quang Tuấn` để cân bằng tải.
    *   Cho phép người dùng bấm nút **"Sao chép"** câu trả lời hoặc **"Xuất Markdown"** để làm báo cáo nhanh.
*   **Đánh giá rủi ro & Cách khắc phục:**
    *   *Rủi ro:* AI có thể không ánh xạ được tên hiển thị sang tài khoản email.
    *   *Khắc phục:* Giao diện hỗ trợ chức năng ánh xạ tự động hoặc cho phép chọn lại người phụ trách thông qua Select box ngay trên giao diện chỉnh sửa dự thảo.

---

## 📝 4. SCENARIO 4: GROUP DISCUSSION SUMMARY (Tóm tắt thảo luận nhóm)

*   **Mục tiêu:** Tóm tắt nhanh hàng chục tin nhắn chat của nhóm thành báo cáo súc tích có dẫn chứng nguồn gốc tin nhắn.
*   **Prompt mẫu:** Bấm nút **"Tóm tắt thảo luận"** trên Group AI Panel của phòng họp nhóm.
*   **Chuẩn bị dữ liệu:**
    *   Tải hoặc gửi ít nhất 5 tin nhắn trong nhóm thảo luận với các nội dung chéo nhau (ví dụ: *A bảo sẽ code Frontend xong trước thứ 6, B bảo sẽ lo phần API Backend, C hỏi về thời gian họp tiếp theo*).
*   **Kết quả mong đợi:**
    *   Hiển thị tóm tắt cuộc thảo luận súc tích.
    *   Mục **Quyết định chính** liệt kê: Thiết kế xong Backend API, Hoàn thành Frontend trước thứ 6.
    *   Mục **Câu hỏi chưa giải quyết**: Thời gian diễn ra buổi họp tiếp theo là khi nào?
    *   Hiển thị phần **Nguồn đối chiếu (Message Sources)** chứa các trích dẫn tin nhắn gốc ngắn gọn của từng thành viên để đảm bảo tính minh bạch.
*   **Đánh giá rủi ro & Cách khắc phục:**
    *   *Rủi ro:* Tin nhắn chat quá ngắn hoặc rời rạc có thể khiến AI tóm tắt sơ sài.
    *   *Khắc phục:* Dưới 3 tin nhắn hệ thống sẽ hiển thị cảnh báo không đủ ngữ cảnh để chạy AI nhằm tiết kiệm token.

---

## ⚡ 5. SCENARIO 5: ACTION ITEM & DRAFT PROJECT (Trích xuất việc cần làm & lập dự án nháp)

*   **Mục tiêu:** Trích xuất các đầu việc từ chat nhóm và tạo dự thảo dự án hoàn chỉnh (tên, mô tả, tasks) mà không ghi DB trước khi người dùng bấm xác nhận.
*   **Prompt mẫu:** Bấm nút **"Tạo project nháp"** trong Group AI Panel.
*   **Chuẩn bị dữ liệu:**
    *   Tương tự Scenario 4, thảo luận nhóm có chứa các đầu việc cụ thể và phân vai rõ ràng.
*   **Kết quả mong đợi:**
    *   Hệ thống sinh ra giao diện chỉnh sửa dự thảo (Draft Project) gồm:
        *   Tên dự án đề xuất chỉnh sửa được (ví dụ: `Xây dựng module AI`).
        *   Mô tả dự án đề xuất.
        *   Danh sách nhiệm vụ có thể chọn/bỏ chọn, chỉnh sửa Tiêu đề, Mô tả, Độ ưu tiên, Số ngày ước lượng, và người phụ trách (được mapping tự động từ tên thành viên nhóm).
    *   Không ghi bất cứ thông tin nào xuống Database trước khi người dùng bấm nút **"Xác nhận tạo Project thật"**.
    *   Sau khi bấm xác nhận, hệ thống mới tiến hành tạo Project và các Tasks tương ứng rồi chuyển hướng sang trang chi tiết dự án.
*   **Đánh giá rủi ro & Cách khắc phục:**
    *   *Rủi ro:* Người dùng thay đổi ý định giữa chừng.
    *   *Khắc phục:* Nút **"Hủy dự thảo"** cho phép xóa sạch trạng thái tạm thời trên giao diện mà không ảnh hưởng tới DB.
