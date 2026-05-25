# Kế Hoạch Tích Hợp AI Chatbot Local (QALY Assistant)

Tài liệu này vạch ra các bước cụ thể để xây dựng và tích hợp trợ lý AI cục bộ (Local AI Chatbot) cho QALY, tuân thủ chặt chẽ tài liệu kiến trúc hệ thống (`QALY_AI_Module_Specification.md`) và các yêu cầu khắt khe về tốc độ, tính chính xác và trải nghiệm người dùng.

## 1. Mục Tiêu Chính
1. **Tốc độ:** Thời gian phản hồi (chữ đầu tiên xuất hiện) phải dưới 2 giây.
2. **Chính xác dữ liệu:** Phản ánh đúng 100% dữ liệu thực tế từ Database của dự án (Tasks, Wiki, Comments).
3. **Trực quan hóa:** AI có khả năng tự động tạo bảng biểu (Tables) và biểu đồ (Charts) dựa trên dữ liệu.
4. **Hội thoại con người (1-on-1):** Có khả năng ghi nhớ ngữ cảnh hội thoại trước đó (Chat History) để đối đáp tự nhiên.
5. **Vận hành Local:** Sử dụng giải pháp Local AI (như Ollama) nhằm bảo mật dữ liệu tuyệt đối và không tốn chi phí API bên ngoài.

---

## 2. Kế Hoạch Triển Khai Từng Bước

### Bước 1: Hoàn thiện tính năng "Trí Nhớ" (Chat History) - Để AI giao tiếp như con người
*Theo như phân tích trước đó, AI hiện tại đang bị "mất trí nhớ" sau mỗi câu hỏi.*
- **Backend:** 
  - Cập nhật DTO `AiChatRequest` nhận thêm mảng `History`.
  - Sửa `AiService.cs` để chèn danh sách tin nhắn cũ vào giữa `SystemPrompt` và `UserMessage`.
- **Frontend:** Cập nhật file `AnalyticsPage.vue` để gửi kèm mảng `chatHistory` (loại bỏ các tin nhắn rỗng) mỗi khi fetch API `/api/ai/chat/stream`.

### Bước 2: Tối ưu Tốc độ Phản hồi (< 2 giây) bằng Streaming và Cache
*Theo kiến trúc 1.5 Performance & Cost, mục tiêu là streaming phải trả về token đầu tiên dưới 2 giây.*
- **Backend:** 
  - Giữ nguyên cơ chế `IAsyncEnumerable<string> ChatStreamingAsync`.
  - Tối ưu khâu truy vấn RAG (Retrieval-Augmented Generation): Đảm bảo việc tìm kiếm vector trên Qdrant DB diễn ra song song hoặc dưới 500ms.
  - Tối ưu hàm `RefineSearchQueriesAsync`: Tránh bắt AI sinh ra các câu query rườm rà làm chậm luồng chính. Có thể dùng mô hình Embedding trực tiếp câu hỏi của người dùng để search nếu cần tốc độ cao nhất.
- **AI Server (Ollama):** 
  - Đảm bảo model được load sẵn trên RAM (keep-alive) để không mất thời gian "cold start".
  - Sử dụng model kích thước nhỏ/vừa (ví dụ Llama-3.2-1B hoặc 3B) chuyên cho tốc độ và tiếng Việt.

### Bước 3: Đảm bảo Tính Chính Xác (RAG Pipeline & Function Calling)
*Cần đảm bảo AI không "bịa" dữ liệu (Hallucination) mà lấy chính xác từ Database (Theo kiến trúc 1.2).*
- **Đồng bộ Vector (Vector Sync):** Hoàn thiện Worker đồng bộ dữ liệu từ Database (PostgreSQL/SQL Server) sang Qdrant. Đảm bảo mọi thay đổi về Task, Comment đều được cập nhật vào Qdrant.
- **Function Calling (Tools):** 
  - Cung cấp cho AI các "công cụ" (Tools/Plugins) trong `AiService.cs` (như `GetOverdueTasks`, `GetProjectSummary`). 
  - Khi người dùng hỏi "Có bao nhiêu task?", AI sẽ không đoán mà gọi hàm truy vấn DB thực tế, sau đó dùng kết quả thật để trả lời.
- **Permission Filter:** Ràng buộc chặt chẽ dữ liệu được trích xuất bằng TenantID và ProjectID. Không để AI lấy dữ liệu dự án khác trả lời cho dự án hiện tại.

### Bước 4: Tích hợp khả năng hiển thị Bảng và Biểu đồ (Charts/Tables)
- **Backend (Prompt Engineering):** 
  - Thêm chỉ thị vào `SystemPrompt`: *"Nếu dữ liệu có tính chất thống kê, hãy trình bày dưới dạng Bảng Markdown hoặc Biểu đồ Mermaid (Mermaid.js)."*
- **Frontend (Vue.js):** 
  - Đảm bảo thư viện render Markdown (ví dụ: `markdown-it`) đang hỗ trợ tốt bảng.
  - Tích hợp thêm plugin `mermaid` vào trình render Markdown của Vue. 
  - Khi AI sinh ra khối code ` ```mermaid `, Frontend sẽ tự động vẽ thành biểu đồ tròn, cột hoặc Gantt chart đẹp mắt cho người dùng.

### Bước 5: Kiểm thử và tinh chỉnh (Evaluation)
*Theo bảng 1.6 Evaluation trong tài liệu thiết kế.*
- Test 1: Đặt các câu hỏi có tính liên kết (Ví dụ: "Lỗi UI nào đang mở?" -> "Ai đang sửa nó?").
- Test 2: Đo lường thời gian token đầu tiên xuất hiện phải < 2s.
- Test 3: Ép AI trả lời các dự án không có quyền truy cập để kiểm tra tính bảo mật.

---

## 3. Tổng kết rủi ro và Giải pháp

1. **Rủi ro quá tải RAM/CPU của server Local:** Do mô hình Local tiêu tốn tài nguyên thật, chúng ta cần dùng cơ chế "Giới hạn History" (chỉ gửi 5-6 tin nhắn gần nhất) để giữ Context Window nhỏ, giảm tải cho Ollama.
2. **Rủi ro UI bị giật/lag khi vẽ Chart:** MermaidJS có thể mất 1-2s để render biểu đồ. Giải pháp là chỉ render khi AI đã stream xong toàn bộ khối code của biểu đồ.

Tài liệu này sẽ làm kim chỉ nam để chúng ta thực thi phần Code. Mọi thay đổi về code sẽ tuân thủ nghiêm ngặt các bước này.
