# Kế Hoạch Triển Khai AI Auto Checknote (Qaly Meet)
*Tập trung vào tính ổn định (Reliability) và kiểm soát chi phí (Cost Efficiency)*

---

## 📋 Trạng thái tiến độ chung
- **Tổng số hạng mục:** 6
- **Đã hoàn thành:** 6/6 (100%)
- **Đang thực hiện:** 0/6
- **Trục trặc/Lưu ý:** Không có

---

## 🛠️ Chi tiết các hạng mục triển khai

### 1. Recovery Plan (IndexedDB) [Sprint 1]
- **Mô tả:** Thiết lập một cơ chế lưu rolling buffer (2-3 phút gần nhất của transcript cuộc họp) vào IndexedDB ở client. Nếu tab bị crash hoặc reload, chỉ cần khôi phục đoạn cuối này để tiếp tục cuộc họp mà không bị mất dữ liệu.
- **Vị trí file cần chỉnh sửa/tạo mới:**
  - `src/Qaly.Web/ClientApp/composables/useMeetingRecovery.ts` (Tạo mới)
  - `src/Qaly.Web/ClientApp/pages/GroupMeetingPage.vue` (Cập nhật tích hợp)
- **Trạng thái:** 🟢 Hoàn thành

### 2. Transcript Chunking & Length Hard-cap (Backend) [Sprint 1]
- **Mô tả:** Thiết lập **Hard-cap độ dài transcript** ngay tại API endpoint. Nếu `TranscriptText.Length > 80000` ký tự, trả về lỗi `400 Bad Request` cùng thông tin hướng dẫn người dùng giới hạn lại để tránh timeout API và kiểm soát chi phí LLM.
- **Vị trí file cần chỉnh sửa/tạo mới:**
  - `src/Qaly.Application/Services/MeetingImportService.cs` (Cập nhật logic `CreateAutoChecknoteAsync`)
  - `src/Qaly.Application/DTOs/Meeting/AutoChecknoteDtos.cs` (Tạo mới để định nghĩa request/response)
- **Trạng thái:** 🟢 Hoàn thành

### 3. Xác thực Group Membership tại GroupHub (SignalR) [Sprint 1]
- **Mô tả:** Đảm bảo hàm `SendMeetingSignal` kiểm tra quyền (User có thuộc Group này không) trước khi broadcast tín hiệu/transcript để tránh việc tin nhắn transcript bị rò rỉ chéo phòng.
- **Vị trí file cần chỉnh sửa/tạo mới:**
  - `src/Qaly.Web/Hubs/GroupHub.cs` (Cập nhật `SendMeetingSignal`)
- **Trạng thái:** 🟢 Hoàn thành

### 4. Auto-restart Web Speech API & Visual Indicator [Sprint 2]
- **Mô tả:** Xử lý sự kiện `onend` trong Web Speech API. Nếu trạng thái ghi âm mong muốn vẫn là `active`, tự động gọi `start()` lại. Đồng thời hiển thị icon microphone nhấp nháy hoặc sóng âm nhẹ để biểu thị hệ thống vẫn đang lắng nghe.
- **Vị trí file cần chỉnh sửa/tạo mới:**
  - `src/Qaly.Web/ClientApp/composables/useSpeechRecognition.ts` (Tạo mới)
  - `src/Qaly.Web/ClientApp/pages/GroupMeetingPage.vue` (Tích hợp component hiển thị visual indicator)
- **Trạng thái:** 🟢 Hoàn thành

### 5. Client-side Timestamp & Sorting [Sprint 2]
- **Mô tả:** Đính kèm `clientTimestamp` vào mỗi payload transcript gửi đi qua SignalR. Khi client nhận được, thực hiện sắp xếp (sort) lại danh sách transcript theo timestamp này trước khi render để đảm bảo hội thoại không bị đảo lộn do độ trễ mạng.
- **Vị trí file cần chỉnh sửa/tạo mới:**
  - `src/Qaly.Web/ClientApp/pages/GroupMeetingPage.vue` (Cập nhật cấu trúc transcript và render logic)
- **Trạng thái:** 🟢 Hoàn thành

### 6. Idempotency Check cho Auto-checknote API [Sprint 2]
- **Mô tả:** Trước khi gọi AI và insert bản ghi mới, kiểm tra trong DB xem đã tồn tại `MeetingImport` nào có cùng `SourceId` (meetingSessionId) và `SourceHash` chưa. Nếu có rồi, trả về kết quả cũ thay vì chạy lại AI.
- **Vị trí file cần chỉnh sửa/tạo mới:**
  - `src/Qaly.Application/Services/MeetingImportService.cs` (Cập nhật logic trong `CreateAutoChecknoteAsync`)
- **Trạng thái:** 🟢 Hoàn thành

---

## 📓 Nhật ký tiến độ và Trục trặc (Audit Log)
*Cập nhật liên tục khi có lỗi hoặc thay đổi thiết kế.*

- **13/06/2026:** Khởi tạo kế hoạch triển khai dựa trên spec tinh gọn được user phê duyệt.
- **13/06/2026:** Hoàn thành mục 3: Xác thực Group Membership tại GroupHub (SignalR).
- **13/06/2026:** Hoàn thành mục 2 (Transcript Chunking & Hard-cap) và mục 6 (Idempotency Check cho Auto-checknote API).
- **13/06/2026:** Hoàn thành mục 1 (Recovery Plan IndexedDB), mục 4 (Auto-restart Web Speech API) và mục 5 (Client-side Timestamp & Sorting).
