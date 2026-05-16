# Kế Hoạch Nâng Cấp Tính Năng Import Kanban CSV/Excel

## 1. Mục tiêu
Tối ưu hóa tính năng Import dữ liệu vào bảng Kanban, giúp người dùng có trải nghiệm mượt mà hơn, xử lý lỗi dễ dàng hơn và tự động hóa quy trình phân loại bằng AI nhằm giảm thiểu thao tác thủ công.

## 2. Lộ trình triển khai (Phases)

### Giai đoạn 1: Cải thiện Trải nghiệm Người dùng (UX & Error Handling)
**Trọng tâm:** Giúp người dùng biết chính xác task nào bị lỗi và cho phép cấu hình dữ liệu mặc định để không phải sửa file gốc.
*   **Báo cáo dòng lỗi chi tiết (Detailed Error Reporting):** 
    *   *Hiện tại:* Chỉ đếm số lượng dòng bị bỏ qua (`skippedCount`).
    *   *Nâng cấp:* Ghi nhận số thứ tự dòng trên Excel và lý do lỗi cụ thể (VD: "Dòng 15: Thiếu tiêu đề", "Dòng 20: Trùng lặp"). Trả mảng `SkippedRows` về Frontend và hiển thị danh sách lỗi rõ ràng ở màn hình Kết quả (Bước 4).
*   **Cài đặt Giá trị Mặc định (Default Values Setting):**
    *   *Nâng cấp:* Thêm tuỳ chọn ở màn hình Mapping (Bước 2): "Gán Assignee mặc định", "Priority mặc định" (VD: Nếu dòng nào để trống người làm, tự động assign cho user đang import).

### Giai đoạn 2: Tích hợp AI Tự động Phân loại (AI Auto-Categorization)
**Trọng tâm:** Áp dụng AI (Erumi/Ollama hoặc ChatGPT2API) để tự động hoá việc phân loại Kanban thay vì gom cục bộ vào cột "Todo".
*   **Dự đoán Status:** Gửi các task thiếu trạng thái lên AI (`Title` + `Description`). AI dựa trên context (VD: task "Fix lỗi crash" sẽ vào InProgress, "Kế hoạch tuần" vào Todo) để sắp xếp cột chuẩn xác.
*   **Dự đoán Label & Priority:** Tự động bắt keyword trong tiêu đề/mô tả để gắn thẻ màu (Bug, Feature) và đặt độ ưu tiên thích hợp.

### Giai đoạn 3: Tối ưu hoá Hiệu năng (Performance & Bulk Insert)
**Trọng tâm:** Tăng tốc độ insert vào database cho file lớn (ví dụ >1000 tasks).
*   **Tối ưu Entity Framework:** Thay thế thao tác gọi `AddAsync` 2000 lần trong vòng lặp bằng cách thêm toàn bộ list vào bộ đệm `AddRangeAsync`, sau đó chỉ gọi `SaveChangesAsync` 1 lần, giúp tăng tốc quá trình Insert gấp nhiều lần.

---

## 3. Chi tiết Kỹ thuật (Technical Specification)

### 3.1. Thay đổi Backend (C#)
1.  **DTOs:** 
    *   Sửa đổi `ImportResult`: Bổ sung `List<SkippedRowDto> SkippedRows`.
    *   Sửa đổi `ImportRequest`: Bổ sung `DefaultAssigneeId`, `DefaultPriority`, `EnableAiCategorization`.
2.  **ImportService.cs:**
    *   **Lưu log lỗi dòng:** Thay vì chỉ đếm `skippedCount++`, sẽ ghi log thông báo lỗi cụ thể (Ví dụ dòng đang duyệt là index 15, thiếu title).
    *   **Ưu tiên Default Values:** Sửa logic mapping -> Cột nào trống thì check xem User có gửi kèm DefaultValue không -> Lấy DefaultValue.
    *   **Gọi AI:** Nếu `request.EnableAiCategorization == true`, gom các task chưa có thông tin thành một batch. Gửi list này qua hàm của `IAiService` (sử dụng JSON mode của LLM) để trả về mảng kết quả phân loại.

### 3.2. Thay đổi Frontend (Vue)
1.  **ImportMappingStep.vue:**
    *   Thêm component Select cho `Default Assignee`, `Default Priority`.
    *   Thêm Switch "Sử dụng AI phân loại dữ liệu trống".
2.  **ImportModal.vue & ImportConfirmStep.vue:**
    *   Trường Confirm (bước 3) sẽ tính toán và hiện luôn: "AI sẽ tự phân loại XX task".
3.  **Result Step (Bước 4):**
    *   Hiển thị Warning List dạng Accordion: Liệt kê chi tiết dòng nào không thể đưa vào hệ thống kèm theo lý do.

---

## 4. Kế hoạch Hành động (Action Tasks)

| Task ID | Nội dung công việc | File liên quan | Ước tính |
| :--- | :--- | :--- | :--- |
| **Task 1** | Cập nhật Model & DTOs cho Import | `ImportResult.cs`, `ImportRequest.cs` | 15 phút |
| **Task 2** | Thêm logic "Báo cáo lỗi dòng" & "Default Values" | `ImportService.cs` | 45 phút |
| **Task 3** | Cập nhật UI Mapping: Thêm Select Assignee & Switch AI | `ImportMappingStep.vue` | 45 phút |
| **Task 4** | Cập nhật UI Result: Hiển thị mảng SkippedRows chi tiết | `ImportModal.vue` | 30 phút |
| **Task 5** | Gọi `IAiService` xử lý phân loại tự động (Giai đoạn 2) | `ImportService.cs`, `AiService.cs` | 1-2 giờ |
| **Task 6** | Refactor Insert Database sang `.AddRangeAsync` | `ImportService.cs` | 20 phút |
