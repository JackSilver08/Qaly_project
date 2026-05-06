# 🚀 KẾ HOẠCH TÍCH HỢP DUAL AI: ERUMI (OLLAMA) & CHATGPT2API

> **Mô tả:** Tài liệu này vạch ra chiến lược và các bước thực thi để tích hợp `chatgpt2api` vào dự án Qaly, hoạt động song song cùng Erumi (Ollama). Mục tiêu là tạo ra một hệ thống AI mạnh mẽ, an toàn, có khả năng tự động dự phòng (graceful fallback) và bảo vệ tuyệt đối dữ liệu nhạy cảm của doanh nghiệp.

---

## 1. KIẾN TRÚC & CHIẾN LƯỢC (DUAL AI ARCHITECTURE)

Hệ thống sẽ sử dụng cơ chế **Router (Định tuyến)** để quyết định AI nào sẽ xử lý tác vụ dựa trên loại tác vụ (Task Type) và mức độ bảo mật:

*   **Erumi (Local Ollama):**
    *   **Nhiệm vụ:** RAG (Đọc dữ liệu nội bộ), Tool Calling (Thay đổi/Ghi dữ liệu vào DB).
    *   **Lý do:** Hoạt động hoàn toàn ở Local, không đẩy dữ liệu mật ra ngoài Internet.
*   **GPT Assistant (Cloud chatgpt2api):**
    *   **Nhiệm vụ:** Free Chat (Hỏi đáp chung), Report Analysis (Phân tích báo cáo), Image Generation (Sinh infographic).
    *   **Lý do:** Mô hình thông minh hơn, suy luận logic tốt hơn, sinh ảnh xuất sắc.
*   **Bảo mật dữ liệu (Data Privacy Boundary):** GPT Assistant **KHÔNG BAO GIỜ** được cấp quyền truy cập trực tiếp vào CSDL. Mọi thông tin GPT nhận được đều phải đi qua `ProjectContextBuilder` (chỉ cung cấp số liệu thống kê tổng hợp và ẩn danh thông tin nhạy cảm).
*   **Graceful Fallback:** Nếu `chatgpt2api` mất kết nối hoặc bị tắt, `AiProviderRouter` sẽ tự động chuyển hướng toàn bộ tác vụ về lại Ollama.

---

## 2. LỘ TRÌNH THỰC THI (IMPLEMENTATION PLAN)

### 🟢 Giai đoạn 1: Hạ tầng & Cấu hình (Infrastructure & Config)
1. **Cập nhật Docker Compose:**
   - Thêm service `chatgpt2api` (image: `basketikun/chatgpt2api:latest`).
   - Cấu hình port `3040:3040` và mount volume cho thư mục data.
2. **Biến môi trường (.env):**
   - Bổ sung `CHATGPT2API_AUTH_KEY` và `CHATGPT2API_ENABLED`.
3. **Cập nhật ASP.NET Core (`appsettings.json`):**
   - Thêm section `AI:ChatGpt2Api` với các thông số: `BaseUrl`, `ApiKey`, `TextModel` (gpt-5-mini), `ImageModel` (gpt-image-2), `Enabled`.

### 🟡 Giai đoạn 2: Lõi Xử lý & Abstraction (Core Application Layer)
1. **Thiết kế Interface `IAiProvider`:**
   - Bao gồm các hàm cơ bản: `ChatAsync`, `StreamChatAsync`, `GenerateImageAsync`.
2. **Xây dựng `AiProviderRouter`:**
   - Cài đặt logic định tuyến (Routing) theo `AiTaskType`.
   - Cài đặt cơ chế Fallback: Nếu tác vụ cần GPT nhưng cấu hình `Enabled = false` hoặc `IsAvailable = false`, tự động trả về `OllamaProvider`.
3. **Triển khai `ChatGpt2ApiProvider`:**
   - Inject `HttpClient`, gọi API `/v1/chat/completions` và `/v1/images/generations` của chatgpt2api.
4. **Xây dựng "Lá chắn dữ liệu" - `ProjectContextBuilder`:**
   - Tạo class đóng vai trò làm Data Masking.
   - Hàm `BuildProjectContextAsync`: Lấy số lượng task todo/done/overdue, danh sách thành viên đang quá tải (Workload). Tuyệt đối không query nội dung chi tiết của comment hay description.

### 🟠 Giai đoạn 3: Tính năng cụ thể (Feature Development)
1. **SignalR Hub Routing (`AiHub.cs`):**
   - Phân luồng tin nhắn từ Client: Nếu mode là `erumi`, dùng Ollama. Nếu mode là `chatgpt`, gọi `ChatGpt2ApiProvider` kèm System Prompt chứa context dự án từ `ProjectContextBuilder`.
2. **Cảnh báo thông minh (Proactive Alerts Job):**
   - Tạo Background Job (Quartz.NET hoặc Hangfire) chạy vào buổi sáng.
   - Job sẽ dùng `ProjectContextBuilder` lấy số liệu -> Gửi cho GPT phân tích -> Lưu cảnh báo vào DB -> Push Notification qua SignalR cho Project Manager.
3. **Sinh ảnh Báo cáo (Infographic Generator):**
   - Gom số liệu thống kê (Velocity, Cycle Time) đẩy vào GPT-Image-2.
   - Tải ảnh báo cáo về server, lưu vào Blob Storage.

### 🔴 Giai đoạn 4: Giao diện người dùng (Vue.js Frontend)
1. **UI Switcher:**
   - Trong component `FloatingChatbot.vue` (hoặc `ErumiChat.vue`), thêm nút chuyển đổi giữa `🤖 Erumi (Local)` và `✨ GPT Assistant`.
2. **State Management:**
   - Lấy biến `chatGptEnabled` từ Backend Config để hiển thị/ẩn tab GPT.
3. **UI Indicators:**
   - Thêm badge trạng thái: "● Online (GPT)" hoặc "● Local (Erumi)" để user biết họ đang nói chuyện với AI nào.
4. **Xử lý Input/Output:**
   - Chỉnh sửa placeholder của ô chat dựa theo mode đang chọn.
   - Bind biến `mode` vào payload gửi qua websocket.

---

## 3. CHECKLIST ĐỂ BẮT ĐẦU NGAY (QUICK START)
- [ ] Mở file `docker-compose.yml` để thêm service `chatgpt2api`.
- [ ] Cập nhật `appsettings.json` và `appsettings.Development.json`.
- [ ] Tạo folder `src/Qaly.Application/AI/Providers` và file `IAiProvider.cs`.
- [ ] Tạo folder `src/Qaly.Application/AI/Context` và file `ProjectContextBuilder.cs`.
