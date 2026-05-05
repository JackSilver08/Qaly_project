# 📊 PHÂN TÍCH TÀI NGUYÊN HỌC TẬP AI (RESOURCES ANALYSIS)

> **Mục tiêu:** Đánh giá tính ứng dụng của các nguồn tài nguyên đối với việc phát triển trợ lý Erumi trong dự án Qaly.

---

## 1. NỀN TẢNG ML & NLP

### [Machine Learning Cơ Bản - Vũ Hữu Tiệp](https://machinelearningcoban.com/)
- **Loại:** Khóa học/Blog (Tiếng Việt)
- **Độ khó:** Beginner - Intermediate
- **Ứng dụng cho Qaly:** Giúp hiểu bản chất của Vector và khoảng cách (Similarity). Cực kỳ hữu ích để giải thích cho team về cách Semantic Search hoạt động.
- **Cách dùng:** Đọc các chương về "K-nearest neighbors" và "Matrix Factorization".

### [Hugging Face NLP Course](https://huggingface.co/learn/nlp-course)
- **Loại:** Khóa học thực hành (Tiếng Anh)
- **Độ khó:** Intermediate
- **Ứng dụng cho Qaly:** Hiểu về Tokenization (LLM tính phí theo token hoặc giới hạn token). Hiểu cách xử lý văn bản thô thành Embedding.
- **Cách dùng:** Học Chapter 1, 2, 3 để nắm quy trình xử lý ngôn ngữ.

---

## 2. TRIỂN KHAI .NET & MICROSOFT STACK

### [Microsoft.Extensions.AI Docs](https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai)
- **Loại:** Tài liệu chính thức
- **Độ khó:** Beginner (cho .NET Dev)
- **Ứng dụng cho Qaly:** Đây là "xương sống" của Backend Qaly. Giúp code không bị phụ thuộc vào Ollama (có thể đổi sang OpenAI dễ dàng).
- **Cách dùng:** Tham chiếu khi viết `AiService.cs`. Tập trung vào `IChatClient` và `IEmbeddingGenerator`.

### [dotnet/ai-samples](https://github.com/dotnet/ai-samples)
- **Loại:** Mã nguồn mẫu (GitHub)
- **Độ khó:** Intermediate
- **Ứng dụng cho Qaly:** Chứa các ví dụ về **RAG** và **Tool Calling** viết bằng C#. Đây là nguồn copy code uy tín nhất.
- **Cách dùng:** Xem thư mục `RAG` và `FunctionCalling`.

---

## 3. INFRASTRUCTURE (OLLAMA & QDRANT)

### [Ollama Documentation](https://docs.ollama.com/)
- **Loại:** Tài liệu chính thức
- **Độ khó:** Beginner
- **Ứng dụng cho Qaly:** Cách config mô hình local. Cách dùng **Structured Output** để lấy JSON từ AI.
- **Cách dùng:** Tra cứu cách viết Prompt để LLM trả về JSON chuẩn.

### [Qdrant .NET SDK](https://github.com/qdrant/qdrant-dotnet)
- **Loại:** Thư viện/Docs
- **Độ khó:** Intermediate
- **Ứng dụng cho Qaly:** Quản lý Vector Database. Rất quan trọng cho phần **Metadata Filtering** (lọc theo ProjectId).
- **Cách dùng:** Xem phần "Filtering" để áp dụng permission check.

---

## 4. BẢO MẬT & ĐÁNH GIÁ

### [OWASP Top 10 for LLM](https://genai.owasp.org/llm-top-10/)
- **Loại:** Tiêu chuẩn bảo mật
- **Độ khó:** Intermediate - Advanced
- **Ứng dụng cho Qaly:** Ngăn chặn người dùng "lừa" AI cung cấp dữ liệu của dự án khác.
- **Cách dùng:** Áp dụng Checklist bảo mật trong `14_AI_SECURITY_AND_EVALUATION_PLAN.md`.

---

## ⚖️ TỔNG KẾT ĐÁNH GIÁ

| Nhóm | Nguồn tốt nhất | Hình thức học | Trọng tâm cho Erumi |
|---|---|---|---|
| **Lý thuyết** | ML Cơ Bản (Vũ Hữu Tiệp) | Đọc & Hiểu | Vectors & Similarity |
| **Thực thi** | Microsoft.Extensions.AI | Code-along | Abstraction & Services |
| **Dữ liệu** | Qdrant Docs | Thực hành | Metadata Filtering |
| **Nâng cao** | dotnet/ai-samples | Tham chiếu | Tool Calling & RAG |
