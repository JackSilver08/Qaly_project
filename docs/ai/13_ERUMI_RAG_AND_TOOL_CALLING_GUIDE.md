# 🧠 HƯỚNG DẪN RAG & TOOL CALLING TRONG QALY

> **Mô tả:** Tài liệu này giải thích cách Erumi "đọc" dữ liệu và "thực hiện" hành động trong hệ thống Qaly.

---

## 1. RAG (RETRIEVAL-AUGMENTED GENERATION)

### Tại sao Qaly cần RAG?
LLM (như llama3.2) không biết về dữ liệu nội bộ của công ty bạn. RAG giúp "nhồi" dữ liệu dự án vào cửa sổ ngữ cảnh (context window) của AI trước khi nó trả lời.

### Nguồn dữ liệu cần Index (Data Sources)
1.  **Projects:** Tên, mô tả, trạng thái, khách hàng.
2.  **Tasks:** Tiêu đề, mô tả chi tiết, người thực hiện, hạn chót.
3.  **Comments:** Các thảo luận trong task (chứa nhiều kiến thức thực tế).
4.  **Wiki:** Các trang hướng dẫn nghiệp vụ của dự án.

### Chiến lược Chunking (Phân mảnh dữ liệu)
- Đối với Task/Comment ngắn: Index nguyên văn.
- Đối với Wiki dài: Cắt nhỏ mỗi 500-1000 ký tự, có gối đầu (overlap) 10% để không mất ngữ cảnh giữa các đoạn.

### Qdrant Schema (Payload)
Mỗi Vector Point phải chứa:
```json
{
  "Id": "GUID",
  "Vector": [0.12, -0.05, ...],
  "Payload": {
    "ProjectId": "GUID",
    "EntityType": "Task",
    "EntityId": "GUID",
    "Content": "Nội dung văn bản thô...",
    "PermissionLevel": "Public/Private"
  }
}
```

---

## 2. TOOL CALLING (FUNCTION CALLING)

### Nguyên tắc vàng (Golden Rules)
1.  **Không SQL thô:** AI tuyệt đối không được sinh hoặc chạy câu lệnh SQL trực tiếp.
2.  **Thông qua Service:** AI chỉ được gọi các hàm C# đã được định nghĩa sẵn trong Application Layer.
3.  **Read-only mặc định:** Hầu hết các tool nên là đọc dữ liệu. Các tool ghi dữ liệu (tạo, sửa) phải có bước xác nhận.
4.  **Kiểm tra quyền:** Mọi Tool phải nhận `CurrentUserId` làm tham số ngầm định để check permission.

### Danh mục Tool được phê duyệt (Approved Tools)

| Tool Name | Mô tả | Loại |
|---|---|---|
| `GetProjectSummary` | Lấy tóm tắt thống kê dự án | Read |
| `GetOverdueTasks` | Danh sách task quá hạn của 1 project | Read |
| `GetMemberWorkload` | Kiểm tra xem ai đang quá tải | Read |
| `SearchKnowledge` | Tìm kiếm ngữ nghĩa trong Wiki/Tasks | Read |
| `CreateTask` | Tạo task mới (Cần User confirm trên UI) | Write |
| `UpdateTaskStatus` | Đổi trạng thái task | Write |

### Luồng thực thi Tool
1. User: "Ai đang rảnh để làm task này?"
2. Erumi: Nhận dạng cần gọi `GetMemberWorkload`.
3. Backend: Thực thi hàm C# -> trả về danh sách member & % workload.
4. Erumi: Tổng hợp dữ liệu và trả lời: "Anh A đang rảnh nhất với chỉ 20% workload..."

---

## 3. PROMPT GROUNDING (RÀNG BUỘC PHẢN HỒI)

Để tránh AI "chém gió" (hallucination), System Prompt phải có quy định:
- "Chỉ trả lời dựa trên dữ liệu được cung cấp trong [CONTEXT]."
- "Nếu không có thông tin, hãy lịch sự nói rằng bạn không biết thay vì tự chế câu trả lời."
- "Luôn trích dẫn nguồn (ví dụ: Task #123) khi đưa ra thông tin."
