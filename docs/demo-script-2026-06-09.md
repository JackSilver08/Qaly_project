# Kịch bản demo QALY - 09/06/2026

> Thời lượng: 8-10 phút | Mục tiêu: trình bày luồng thực tế và backup plan cho AI/local chậm.

## 1. Mở đầu (1 phút)

- Chào nhanh, giới thiệu mục tiêu demo: `dashboard -> project -> group -> poll -> meeting -> import wiki -> hỏi Erumi -> action items`.
- Nhấn mạnh: đây là luồng core đã được kiểm thử, với giới hạn rõ ràng cho meeting/import/AI.
- Ghi chú: không trình bày screen share hay PDF import như tính năng hoàn chỉnh.

## 2. Chuẩn bị dữ liệu demo

- Tài khoản demo: `user-demo@qaly.local` hoặc tài khoản seed nhóm.
- Project demo: `QALY Demo Project`.
- Group demo: `Nhóm ERUMI Sprint`.
- File demo:
    - `QALY-Demo.docx`.
    - `QALY-WikiBundle.zip`.
- Prompt AI: `Tóm tắt tiến độ sprint QALY và nêu 3 action item cần làm tiếp theo.`
- Backend AI: có provider/fallback; nếu nó chậm, dùng backup plan.

## 3. Luồng demo chính

### 3.1 Đăng nhập và Dashboard (1 phút)

- Mở ứng dụng.
- Đăng nhập bằng tài khoản demo.
- Vào `Dashboard`.
- Nói: `Đăng nhập và Dashboard đã được Playwright smoke pass.`

### 3.2 Project và nhóm (1 phút)

- Mở `QALY Demo Project`.
- Mở `Nhóm ERUMI Sprint`.
- Giới thiệu vai trò Owner/Admin/Member.
- Nói: `Quyền nhóm được kiểm soát và chỉ member mới dùng được poll/meeting/import.`

### 3.3 Poll nhóm (1 phút)

- Tạo poll: `Chọn ưu tiên sprint`.
- Thêm option `A`, `B`, `C`.
- Tạo poll và vote.
- Mở kết quả.
- Nói: `Poll đã được smoke test; đây là flow an toàn để demo.`

### 3.4 Cuộc họp nhóm (1 phút)

- Bắt đầu cuộc họp.
- Member join.
- Owner/Admin kết thúc cuộc họp.
- Nói rõ:
    - `Start/join/end meeting hoạt động.`
    - `SignalR presence/reconnect đã có regression hai context.`
    - `Media participant count và screen share cần LiveKit thật/browser headful để xác nhận.`

### 3.5 Import Wiki (2 phút)

- Chọn `Import tài liệu`.
- Upload `QALY-Demo.docx`.
- Xem preview, confirm import.
- Mở Wiki page mới.
- Nếu còn thời gian, upload `QALY-WikiBundle.zip`.
- Nói:
    - `DOCX/ZIP import đã QA pass.`
    - `PDF hiện chỉ roadmap/unsupported.`

### 3.6 Hỏi Erumi (AI) (1-1.5 phút)

- Mở chức năng AI.
- Nhập prompt: `Tóm tắt tiến độ sprint QALY...`.
- Hiển thị kết quả trả về.
- Nói:
    - `AI đang chạy qua provider/fallback.`
    - `Nếu nó chậm, hệ thống có fallback hoặc sẽ return message lỗi rõ.`

### 3.7 Group AI action items (1 phút)

- Mở `AI action items` tại nhóm.
- Trích xuất action items từ thảo luận.
- Nói: `Đây là luồng Group AI giúp tự động hoá các action item sau cuộc họp.`

## 4. Kịch bản trình bày ngắn gọn

- "Bước 1: Đăng nhập và xác nhận dashboard."
- "Bước 2: Mở project và nhóm để chứng minh phân quyền."
- "Bước 3: Tạo poll và vote để minh hoạ tính năng nhóm."
- "Bước 4: Mở cuộc họp, join ở hai context và kết thúc; nói rõ CI dùng LiveKit fallback."
- "Bước 5: Import DOCX/ZIP vào Wiki và mở trang kết quả."
- "Bước 6: Hỏi Erumi và hiển thị kết quả AI."
- "Bước 7: Trích xuất action items nhóm để minh hoạ Group AI."

## 5. Backup plan

- Nếu AI chậm: chuyển sang slide/bản tóm tắt kỹ thuật và nói rõ backend có fallback AI.
- Nếu local chậm: dùng evidence `Playwright smoke` và `backend regression` để chứng minh hệ thống chạy.
- Nếu import quá lớn: dùng file demo nhỏ 5MB và giải thích giới hạn hiện tại.
- Nếu LiveKit media gặp vấn đề: demo `start/join/end`, SignalR presence và trạng thái fallback rõ ràng.

## 6. Điểm nhấn kỹ thuật

- Kiến trúc `Qaly.Web`, `Qaly.Application`, `Qaly.Infrastructure`, `Qaly.Domain`.
- SignalR cho chat và meeting events.
- Import engine preview + execute.
- AI analytics và Group AI có luồng fallback.
- Local SQL dev link hướng dẫn dùng `CMI\SQLEXPRESS`.
