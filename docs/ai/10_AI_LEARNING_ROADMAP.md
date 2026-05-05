# 🎓 LỘ TRÌNH HỌC TẬP AI (8 TUẦN) CHO DEVELOPER QALY

> **Mục tiêu:** Giúp một Web Developer thuần .NET/Vue.js trở thành một AI Integration Engineer có khả năng phát triển và vận hành Erumi.

---

## 📋 YÊU CẦU NỀN TẢNG
- Thành thạo C#/.NET 8+.
- Hiểu biết về REST API và Async programming.
- Có kiến thức cơ bản về Database (SQL).
- Không yêu cầu kiến thức Toán cao cấp, nhưng cần tư duy logic tốt.

---

## 🗓️ LỘ TRÌNH CHI TIẾT

### Tuần 1: Nền tảng Machine Learning
- **Nội dung:** Hiểu ML là gì? Phân biệt Supervised/Unsupervised Learning. Quy trình từ dữ liệu đến model.
- **Tại sao cần:** Để hiểu Erumi không phải là code logic "If-Else" mà là dựa trên dữ liệu.
- **Tài nguyên:** [Machine Learning Cơ Bản - Vũ Hữu Tiệp](https://machinelearningcoban.com/), [Google ML Crash Course](https://developers.google.com/machine-learning/crash-course).
- **Bài tập:** Chạy một ví dụ Linear Regression đơn giản bằng Python hoặc ML.NET.
- **Kết quả:** Hiểu được khái niệm Weights, Features, và Training.

### Tuần 2: NLP và LLM (Large Language Models)
- **Nội dung:** Tokenization, Context Window, Temperature, Top-P. Cách LLM dự đoán từ tiếp theo.
- **Tại sao cần:** Để hiểu giới hạn của mô hình llama3.2 trong Qaly.
- **Tài nguyên:** [Hugging Face NLP Course (Chapter 1-2)](https://huggingface.co/learn/nlp-course).
- **Bài tập:** Sử dụng Ollama CLI để thử nghiệm các Prompt khác nhau với Temperature khác nhau.
- **Kết quả:** Viết được System Prompt cơ bản cho Erumi.

### Tuần 3: Embeddings và Semantic Search
- **Nội dung:** Chuyển đổi văn bản thành Vector. Khoảng cách Cosine (Cosine Similarity).
- **Tại sao cần:** Đây là cốt lõi của việc tìm kiếm Task theo ý nghĩa trong Qaly.
- **Tài nguyên:** [Ollama Embeddings Docs](https://docs.ollama.com/capabilities/embeddings).
- **Bài tập:** Dùng `nomic-embed-text` để so sánh độ tương đồng giữa 2 task title.
- **Kết quả:** Hiểu tại sao "fix bug" và "sửa lỗi" lại có vector gần nhau.

### Tuần 4: RAG Pipeline (Retrieval-Augmented Generation)
- **Nội dung:** Quy trình: Retrieve -> Augment -> Generate. Prompt Grounding.
- **Tại sao cần:** Để Erumi trả lời đúng dữ liệu của dự án Qaly thay vì nói lung tung.
- **Tài nguyên:** [Microsoft Docs on RAG](https://learn.microsoft.com/en-us/azure/ai-services/openai/concepts/retrieval-augmented-generation).
- **Bài tập:** Vẽ sơ đồ luồng dữ liệu từ SQL -> Vector DB -> LLM.
- **Kết quả:** Hiểu rõ cách Erumi "đọc" dữ liệu dự án trước khi trả lời.

### Tuần 5: Qdrant và Vector Database Integration
- **Nội dung:** Collection, Point, Payload, Metadata Filtering.
- **Tại sao cần:** Để cô lập dữ liệu giữa các Project (Security).
- **Tài nguyên:** [Qdrant Documentation](https://qdrant.tech/documentation/).
- **Bài tập:** Tạo một collection trong Qdrant và thực hiện search có dùng Filter Metadata.
- **Kết quả:** Thành thạo việc quản lý dữ liệu vector trong .NET.

### Tuần 6: Tool Calling và Structured Output
- **Nội dung:** Function Calling, JSON Schema output.
- **Tại sao cần:** Để Erumi có thể tự tạo Task hoặc chuyển trạng thái Task.
- **Tài nguyên:** [Ollama Tool Calling](https://docs.ollama.com/capabilities/tool-calling).
- **Bài tập:** Viết một tool đơn giản cho LLM gọi để lấy thời gian hiện tại hoặc thời tiết.
- **Kết quả:** LLM có thể trả về JSON chuẩn thay vì văn bản thô.

### Tuần 7: AI Security và Permission Checks
- **Nội dung:** Prompt Injection, Data Leakage, Metadata filtering safety.
- **Tại sao cần:** Đảm bảo Erumi không tiết lộ bí mật kinh doanh giữa các khách hàng.
- **Tài nguyên:** [OWASP Top 10 for LLM](https://genai.owasp.org/llm-top-10/).
- **Bài tập:** Thử tấn công Erumi bằng prompt injection (jailbreak) và tìm cách ngăn chặn.
- **Kết quả:** Thiết lập lớp bảo mật Filtering chắc chắn cho RAG.

### Tuần 8: Đánh giá (Evaluation) và Production Hardening
- **Nội dung:** Rate limiting, Caching, LLM Evaluation (Accuracy, Hallucination).
- **Tại sao cần:** Đảm bảo hệ thống ổn định và AI không trả lời sai.
- **Tài nguyên:** [dotnet/ai-samples (Evaluation)](https://github.com/dotnet/ai-samples).
- **Bài tập:** Tạo bộ test gồm 20 câu hỏi mẫu và chấm điểm câu trả lời của Erumi.
- **Kết quả:** Bản báo cáo chất lượng AI trước khi Release.
