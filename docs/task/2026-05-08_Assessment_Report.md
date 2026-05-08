# 📊 Qaly Project Assessment Report - 2026-05-08

## 1. Tổng quan Dự án (Project Overview)
Dự án Qaly hiện đang ở trạng thái khá hoàn thiện về mặt tính năng (prototype cao cấp). Hệ thống đã triển khai đầy đủ các layer theo Clean Architecture, tích hợp AI (RAG, Tool Calling), Real-time (SignalR) và đã có khung Frontend với Vue.js 3.

## 2. Các vấn đề & Lỗ hổng (Bugs & Gaps)

### 🔴 Mức độ Nghiêm trọng (High Priority)
1.  **Frontend Bloat (`App.vue`):** File `src/Qaly.Web/ClientApp/App.vue` quá lớn (~41KB). Mọi logic từ Dashboard, Project, Task đến AI Chat đều đang nằm trong một component duy nhất. Điều này gây khó khăn cực lớn cho việc bảo trì, debug và mở rộng.
    *   **Giải pháp:** Cần refactor tách thành các component nhỏ (ProjectBoard, TaskDetails, AiChatOverlay, v.v.).
2.  **Weak AI Prompts:** Một số prompt trong `AiService.cs` còn quá đơn giản. Ví dụ: `SuggestTaskAssignmentAsync` yêu cầu đề xuất thành viên nhưng không cung cấp danh sách thành viên khả dụng trong prompt, dẫn đến AI sẽ trả lời sai hoặc chung chung.
3.  **Inconsistent DI:** `DependencyInjection.cs` của Application đăng ký `NullNotificationPublisher`, trong khi `Program.cs` đăng ký `SignalRNotificationPublisher`. Mặc dù hoạt động đúng do thứ tự ưu tiên, nhưng cấu trúc này gây nhầm lẫn.

### 🟡 Mức độ Trung bình (Medium Priority)
1.  **AI Audit Logs:** Hiện tại các tương tác với AI chưa được ghi lại vào bảng `AuditLogs`. Việc track AI actions là cần thiết để kiểm soát chi phí và độ chính xác.
2.  **Hardcoded Vector Size:** Trong `AiIngestionService.cs`, `VectorSize` được hardcode là 768. Nếu đổi sang một Embedding Model khác, hệ thống sẽ gặp lỗi khởi tạo Collection trong Qdrant.
3.  **Missing Tests:** Thiếu unit tests cho `ProjectService`, `TaskService` và phần lớn logic xử lý RAG trong `AiService`. Hiện chỉ có test cho một số guardrail cơ bản.
4.  **Outdated Docs:** File `docs/04_Implementation_Plan.md` hiển thị trạng thái Phase 0-1 là "Chưa bắt đầu", trong khi dự án đã đi đến Phase 4-5.

### 🟢 Mức độ Thấp (Low Priority)
1.  **Hardcoded Vietnamese Prompts:** Các prompt AI hoàn toàn bằng tiếng Việt. Nếu dự án muốn mở rộng ra quốc tế, cần cơ chế localization cho Prompt Template.
2.  **Streaming Error Handling:** `ChatStreamingAsync` chưa có cơ chế xử lý lỗi tường minh nếu kết nối với Ollama bị ngắt giữa chừng.

## 3. Danh sách Công việc Đề xuất (Next Tasks)

- [ ] **Refactor Frontend:** Chia nhỏ `App.vue` thành các component chuyên biệt.
- [ ] **Enhance AI Prompts:** Cập nhật prompt trong `AiService` để cung cấp đủ context (như danh sách thành viên dự án khi assign task).
- [ ] **AI Auditing:** Tích hợp `IAuditLogService` vào `AiService` để ghi nhận các hành động quan trọng do AI thực hiện.
- [ ] **Configurable AI:** Đưa `VectorSize` và các tham số AI khác vào `appsettings.json`.
- [ ] **Update Documentation:** Cập nhật lại toàn bộ file trong `docs/` để phản ánh đúng hiện trạng dự án.
- [ ] **Increase Test Coverage:** Viết unit tests cho các Service lõi.

---
*Người thực hiện: Gemini CLI Agent*
*Ngày: 08/05/2026*
