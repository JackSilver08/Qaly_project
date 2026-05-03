# 📝 BÁO CÁO NGHIÊN CỨU AI (AI RESEARCH REPORT)

> **Người thực hiện:** Gemini CLI  
> **Ngày:** 03/05/2026  
> **Dự án:** Qaly Erumi Assistant

---

## 1. TÓM TẮT QUÁ TRÌNH NGHIÊN CỨU
Tôi đã tiến hành rà soát cấu trúc mã nguồn hiện tại của Qaly và đối chiếu với các tiêu chuẩn phát triển AI hiện đại của Microsoft và cộng đồng mã nguồn mở (Ollama, Qdrant). 

**Kết quả:** Dự án đã có nền tảng rất tốt với `Microsoft.Extensions.AI` và kiến trúc RAG sơ khai. Tuy nhiên, vẫn còn thiếu các lớp bảo mật, cơ chế Tool Calling và sự tối ưu trong việc quản lý Vector Metadata.

---

## 2. CÁC NGUỒN TÀI LIỆU ĐÃ LỰC CHỌN
- **Chính thống:** Microsoft.Extensions.AI Docs, dotnet/ai-samples (Để đảm bảo code đúng chuẩn .NET).
- **Hạ tầng:** Qdrant & Ollama Docs (Để tối ưu hiệu năng local).
- **Học thuật:** ML Cơ Bản - Vũ Hữu Tiệp (Để giải thích thuật ngữ cho team Việt Nam).
- **Bảo mật:** OWASP LLM Top 10 (Để phòng ngừa rủi ro rò rỉ dữ liệu).

---

## 3. BÀI HỌC RÚT RA & KHUYẾN NGHỊ

### Bài học chính:
1. **Trừu tượng hóa là quan trọng:** Việc dùng `IChatClient` giúp Qaly linh hoạt giữa mô hình local (miễn phí) và cloud (thông minh hơn).
2. **Dữ liệu quyết định chất lượng:** RAG chỉ tốt khi dữ liệu được Index sạch và có Metadata rõ ràng.
3. **AI không được tự quyết:** Mọi hành động ghi dữ liệu phải thông qua sự xác nhận của con người.

### Khuyến nghị lộ trình học:
Developer nên bắt đầu từ việc hiểu **Embeddings** và **Vector Search** trước khi chuyển sang các kỹ thuật phức tạp như **Tool Calling**. Hiểu cách dữ liệu được tìm kiếm là chìa khóa để debug AI.

### Khuyến nghị triển khai:
Nên tập trung hoàn thiện **Metadata Filtering** đầu tiên. Đây là lỗ hổng lớn nhất về bảo mật hiện tại. Sau đó mới phát triển các tính năng "màu mè" như Chatbot.

---

## 4. RỦI RO & GIẢ ĐỊNH
- **Giả định:** Ollama có đủ tài nguyên phần cứng (RAM/GPU) để chạy llama3.2 mượt mà.
- **Rủi ro:** Mô hình 1B/3B có thể không hiểu tốt các Tool Calling phức tạp, dẫn đến gọi sai hàm.
- **Rủi ro:** Token limit của mô hình local thường nhỏ, cần chiến lược chunking và culling context tốt.

---

## 5. CÁC CÂU HỎI CẦN QUYẾT ĐỊNH (HUMAN DECISIONS)
1. Chúng ta sẽ dùng mô hình local vĩnh viễn hay có kế hoạch chuyển sang Azure OpenAI khi dự án lớn mạnh?
2. Mức độ ưu tiên của việc "AI tự tạo task" là bao nhiêu so với "AI tra cứu thông tin"?
3. Team có muốn triển khai thêm một giao diện quản lý (Admin UI) để xem Audit Log của AI không?
