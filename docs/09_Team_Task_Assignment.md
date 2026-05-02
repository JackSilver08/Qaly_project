# 📋 PHÂN BỔ CÔNG VIỆC CHI TIẾT – ĐỘI NGŨ DỰ ÁN QALY

> **Người phê duyệt:** Quang Tuấn (Team Leader)
> **Ngày cập nhật:** 02/05/2026

Dựa trên nền tảng dự án đã được hoàn thành bởi **Quang Tuấn**, dưới đây là kế hoạch phân bổ công việc tiếp theo cho các thành viên. Mục tiêu là đảm bảo mỗi thành viên đóng góp sâu sắc vào hệ thống với khối lượng công việc lớn (định hướng >5000 dòng code/người) và chuyên môn hóa rõ rệt.

---

## 🏗️ 1. NHÓM CODE CHÍNH (CORE)

### 👑 Quang Tuấn – Nhóm trưởng (Tech Lead)
*   **Trạng thái:** Đã hoàn thành nền tảng dự án (Clean Architecture, Auth, Core Entities, Infrastructure).
*   **Công việc tiếp theo:**
    *   Quản trị kiến trúc hệ thống, Review code cho toàn bộ thành viên.
    *   Thiết kế và triển khai các Module phức tạp: Hệ thống Real-time (SignalR), Phân quyền động (Dynamic RBAC).
    *   Tối ưu hóa hiệu năng Database và Core Services.

---

## 🛠️ 2. NHÓM HỖ TRỢ (SUPPORT & FEATURES)

### 🔹 Duy Hoàng – Backend Expansion & Business Logic
*   **Công việc trọng tâm:** Mở rộng hệ thống xử lý nghiệp vụ Backend.
*   **Chi tiết (Target 5000+ LOC):**
    *   Xây dựng hệ thống Reporting & Analytics: Xuất báo cáo PDF/Excel cho hiệu suất dự án.
    *   Triển khai Module quản lý tệp tin nâng cao: Phân loại, versioning và preview file đính kèm.
    *   Xây dựng bộ lọc (Filtering) và tìm kiếm nâng cao (Advanced Search) cho toàn bộ hệ thống.
    *   Viết lại các Service để hỗ trợ Batch Processing (xử lý hàng loạt).

### 🔹 Viết Minh – Frontend Component System & UX
*   **Công việc trọng tâm:** Phát triển thư viện UI nội bộ và trải nghiệm người dùng.
*   **Chi tiết (Target 5000+ LOC):**
    *   Xây dựng thư viện Vue Components tùy chỉnh (thay thế hoặc mở rộng Lucide/Tailwind) để tạo bản sắc riêng cho Qaly.
    *   Triển khai hệ thống Charts & Dashboards tương tác (sử dụng Chart.js hoặc D3.js).
    *   Tối ưu hóa UI cho Mobile (Responsive design) và Progressive Web App (PWA).
    *   Triển khai hệ thống thông báo đa kênh trên giao diện (Toasts, Modals, In-app Notifications).

### 🔹 Đoàn Trung – Quality Assurance & Testing Framework
*   **Công việc trọng tâm:** Đảm bảo độ tin cậy và tự động hóa kiểm thử.
*   **Chi tiết (Target 5000+ LOC):**
    *   Xây dựng bộ Unit Test bao phủ >80% logic nghiệp vụ trong `Qaly.Application`.
    *   Triển khai Integration Tests cho toàn bộ API Controllers sử dụng WebApplicationFactory.
    *   Xây dựng bộ End-to-End (E2E) Testing sử dụng Playwright hoặc Cypress cho các luồng chính (Login -> Create Project -> Assign Task).
    *   Phát triển công cụ Mock Data Generator phục vụ việc demo và stress test.

### 🔹 Gia Long – Documentation & DevOps Engineering
*   **Công việc trọng tâm:** Tài liệu hóa hệ thống và hạ tầng triển khai.
*   **Chi tiết (Target 5000+ LOC):**
    *   Xây dựng trang Wiki nội bộ tích hợp trực tiếp vào dự án (Markdown Viewer).
    *   Tự động hóa tài liệu API (Swagger/OpenAPI custom implementation).
    *   Cấu hình hệ thống CI/CD (GitHub Actions) chi tiết cho nhiều môi trường (Dev, Staging, Prod).
    *   Docker hóa toàn bộ hệ thống với Docker Compose phức tạp (Monitoring, Logging, SQL Cluster).

---

## 🧠 3. NHÓM NGHIÊN CỨU (AI RESEARCH & INTEGRATION)

### 🔹 Quốc Bảo – AI Core Engine
*   **Công việc trọng tâm:** Xây dựng lõi xử lý AI và tích hợp mô hình ngôn ngữ lớn (LLM).
*   **Chi tiết (Target 5000+ LOC):**
    *   Triển khai `AiService` với khả năng kết nối đa mô hình (OpenAI, Gemini, Local LLMs qua Ollama).
    *   Xây dựng hệ thống Prompt Engineering Framework nội bộ để chuẩn hóa các yêu cầu từ phía người dùng.
    *   Triển khai RAG (Retrieval-Augmented Generation) để AI có thể "đọc" và hiểu tài liệu dự án trong `docs/`.
    *   Xây dựng hệ thống quản lý Token và lịch sử hội thoại AI hiệu quả.

### 🔹 Chí Khang – AI Features & Intelligent Assistant
*   **Công việc trọng tâm:** Biến năng lực AI thành các tính năng thực tế cho người dùng.
*   **Chi tiết (Target 5000+ LOC):**
    *   Phát triển trợ lý ảo (Erru-mi Chatbot) trên giao diện với các tính năng: Tóm tắt tiến độ, gợi ý phân bổ công việc.
    *   Triển khai hệ thống "Dự báo rủi ro": AI phân tích lịch sử để cảnh báo các Task có nguy cơ trễ hạn.
    *   Tự động hóa việc tạo Task từ ngôn ngữ tự nhiên (Natural Language to Task).
    *   Xây dựng hệ thống gợi ý gắn nhãn (Labeling) và độ ưu tiên (Priority) tự động cho công việc.

---

## 📈 TỔNG KẾT YÊU CẦU CHUNG
1.  **Chất lượng Code:** Tuân thủ Clean Architecture và các Design Patterns đã được thiết lập bởi Tech Lead.
2.  **Khối lượng:** Mỗi thành viên cần chủ động mở rộng module mình phụ trách để đạt được độ phức tạp và khối lượng code cần thiết.
3.  **Báo cáo:** Cập nhật tiến độ hàng tuần vào file `README.md` hoặc hệ thống quản lý task của nhóm.
