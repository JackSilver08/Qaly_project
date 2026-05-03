# 🚀 KẾ HOẠCH TRIỂN KHAI ERUMI AI (IMPLEMENTATION PLAN)

> **Mục tiêu:** Chuyển đổi từ mô hình AI cơ bản hiện tại sang một hệ thống Trợ lý thông minh, an toàn và có khả năng thực thi tác vụ.

---

## 🏗️ KIẾN TRÚC MỤC TIÊU (TARGET ARCHITECTURE)

Hệ thống sẽ tuân thủ mô hình phân lớp để đảm bảo tính module hóa:

```
[Vue.js Erumi Widget] 
      ↕ (Streaming JSON)
[AiController] -> (Permission Checks)
      ↕
[IAiChatService] 
   ├─ [IAiRetrievalService] -> [Qdrant] (Dữ liệu dự án)
   ├─ [IAiToolService] -> [Application Services] (Hành động: tạo task, v.v.)
   └─ [Ollama / Microsoft.Extensions.AI] (Brain)
```

---

## 📅 CÁC GIAI ĐOẠN TRIỂN KHAI (10 PHASES)

### Phase 1: Cleanup & Documentation (Hiện tại)
- **Mục tiêu:** Chuẩn bị tài liệu và làm sạch kiến trúc hiện tại.
- **Nhiệm vụ:** Hoàn thiện bộ tài liệu `/docs/ai`.
- **Rủi ro:** Tài liệu không sát với thực tế code.

### Phase 2: AI Provider Abstraction
- **Mục tiêu:** Hoàn thiện việc sử dụng `Microsoft.Extensions.AI` để tách biệt logic AI khỏi Controller.
- **Nhiệm vụ:** Refactor `AiService` để dùng `IChatClient` một cách triệt để.
- **Files:** `AiService.cs`, `DependencyInjection.cs`.

### Phase 3: Embeddings & Qdrant Optimization
- **Mục tiêu:** Thiết lập Schema Qdrant chuẩn với đầy đủ Metadata.
- **Nhiệm vụ:** Thêm `ProjectId`, `UserId`, `EntityType` vào Payload.
- **Files:** `QdrantVectorStorageService.cs`, `AiIngestionService.cs`.

### Phase 4: RAG Ingestion Pipeline
- **Mục tiêu:** Tự động hóa việc đưa dữ liệu vào "não" của Erumi.
- **Nhiệm vụ:** Cải thiện `VectorSyncInterceptor` để handle các trường hợp Delete/Update phức tạp.
- **Risk:** Tải nặng Server khi sync hàng loạt.

### Phase 5: Semantic Search with Permission
- **Mục tiêu:** Tìm kiếm thông minh nhưng an toàn.
- **Nhiệm vụ:** Implement filtering theo `ProjectId` trong lệnh search vector.
- **Acceptance Criteria:** User không thấy kết quả từ project họ không tham gia.

### Phase 6: Read-only Tool Calling
- **Mục tiêu:** Erumi có thể "nhìn" vào hệ thống qua code.
- **Nhiệm vụ:** Khai báo các Tool: `GetProjectSummary`, `GetOverdueTasks`.
- **Tech:** `AIFunctionFactory` của Microsoft.

### Phase 7: Erumi Chat with Grounding
- **Mục tiêu:** Chatbot trả lời dựa trên sự thật (Grounding).
- **Nhiệm vụ:** Kết hợp kết quả từ Semantic Search vào System Prompt một cách chuyên nghiệp.

### Phase 8: Security & Audit Logging
- **Mục tiêu:** Kiểm soát và bảo mật.
- **Nhiệm vụ:** Ghi log mọi câu hỏi/trả lời vào SQL AuditLog. Lọc dữ liệu nhạy cảm (PII).

### Phase 9: Background Intelligence
- **Mục tiêu:** AI chủ động làm việc.
- **Nhiệm vụ:** Chạy background job phân tích rủi ro định kỳ và gửi thông báo.

### Phase 10: Evaluation & Hardening
- **Mục tiêu:** Kiểm định chất lượng.
- **Nhiệm vụ:** Chạy bộ test suite đánh giá độ chính xác. Tối ưu prompt.

---

## ✅ TIÊU CHÍ NGHIỆM THU (ACCEPTANCE CRITERIA)
1. Erumi trả lời được câu hỏi: "Task nào của tôi đang quá hạn?" (RAG + Tool).
2. Erumi có thể tạo task mới khi được yêu cầu: "Tạo giúp tôi task fix bug CSS" (Tool Calling).
3. Tuyệt đối không lộ dữ liệu giữa các Project khác nhau.
4. Phản hồi mượt mà qua cơ chế Streaming (SSE hoặc SignalR).
