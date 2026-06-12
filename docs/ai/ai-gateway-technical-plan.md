# Kế hoạch kỹ thuật AI Gateway — Dự án QALY

## 1. Hiện trạng AI hiện tại
Hệ thống AI của Qaly hiện thời sử dụng `AiGateway` làm trung gian để điều hướng các tác vụ AI. Tuy nhiên, nó đang gặp phải một số giới hạn và hành vi cần chuẩn hóa:
- **Mock/Fallback/Rule-based**: Gateway có cơ chế fallback trả về nội dung mock cứng khi gặp lỗi hoặc không thể kết nối tới mô hình thật.
- **Provider Routing**: Có cấu hình `AiSettings` trong `appsettings.Development.json` nhưng chưa kiểm thử toàn diện khả năng tự động chuyển vùng, xử lý timeout, retry và ghi log chi tiết cho từng loại provider (Ollama, OpenAI, Gemini).
- **RAG & Tool Calling**: Có cấu trúc nhưng cần đảm bảo lấy được dữ liệu thật từ database (Project, Task, Wiki, Comment, Group) một cách an toàn và đúng thẩm quyền (User Permission Context), không được giả định hay bịa ra câu trả lời.
- **Schema Validation**: Cần cơ chế chặt chẽ hơn để validate cấu trúc JSON đầu ra cho các tác vụ sinh action items, summary, dự thảo project hoặc khối analytics nhằm tránh làm hỏng giao diện.

---

## 2. Luồng xử lý đề xuất (Proposed Flow)

Khi một yêu cầu (Request) đi qua AI Gateway, nó sẽ được xử lý qua tuần tự các bước sau:

```mermaid
flowchart TD
  Request([AI Request]) --> AuthFilter[1. Phân quyền truy cập & Kiểm tra Project/User Context]
  AuthFilter --> Compliance[2. Compliance & Safety Guard: CanProcessInCloud]
  Compliance -- Blocked / No Consent --> FallbackMock[Trả về Fallback / Mock Response + Audit Log]
  Compliance -- Allowed --> Budget[3. Budget Guard: EnsureBudgetAvailable]
  Budget -- Over Budget --> FallbackMock
  Budget -- Budget OK --> Cache[4. Cache Lookup: AI Prompt Cache]
  Cache -- Hit --> RecordCacheUsage[Ghi log Cache Hit + Trả về cached response]
  Cache -- Miss --> ProviderRouting[5. Provider Routing: Ollama / OpenAI / Gemini]
  ProviderRouting --> CallProvider[6. Thực hiện gọi Provider (Timeout + Retry)]
  CallProvider -- Thất bại (Retry hết lượt) --> FallbackMock
  CallProvider -- Thành công --> ParseToolCall{7. AI có yêu cầu Tool Call / RAG?}
  ParseToolCall -- Yes --> SecurityGuard[8. Tool Parameter Security Guard & Access Check]
  SecurityGuard -- Denied --> ToolFailed[Trả về lỗi quyền truy cập cho AI]
  SecurityGuard -- Approved --> ExecuteTool[9. Thực thi Tool / RAG truy xuất dữ liệu thật]
  ExecuteTool --> AppendHistory[Đưa kết quả tool vào Lịch sử & Loop lại AI]
  AppendHistory --> CallProvider
  ParseToolCall -- No / Output Ready --> SchemaValidation{10. Cần Validate Schema?}
  SchemaValidation -- No --> SaveCache[11. Lưu Cache & Ghi Audit Log / Usage Ledger]
  SchemaValidation -- Yes --> CheckSchema[Validate JSON Schema]
  CheckSchema -- Hợp lệ --> SaveCache
  CheckSchema -- Không hợp lệ & Còn lượt retry --> RetryFixSchema[Yêu cầu AI sửa JSON format]
  RetryFixSchema --> CallProvider
  CheckSchema -- Không hợp lệ & Hết lượt retry --> FallbackMock
  SaveCache --> ReturnResponse([AI Response])
```

### Chi tiết các bước:
1. **User/Project Permission Context**: Xác minh người dùng hiện tại có quyền truy cập vào các dự án/dữ liệu được yêu cầu hay không. Nếu không, chặn ngay từ gateway hoặc filter dữ liệu đầu vào.
2. **Compliance/Safety Guard**: Kiểm tra xem dữ liệu nhạy cảm có được phép xử lý trên cloud không. Nếu không, bắt buộc chạy model local (Ollama) hoặc trả về fallback an toàn.
3. **Budget Guard**: Đảm bảo Tenant/Project chưa vượt quá ngân sách AI cho phép.
4. **Cache Lookup**: Tìm kiếm trong bảng `AiPromptCache` bằng mã hash của prompt + lịch sử. Nếu có và chưa hết hạn, trả về ngay lập tức để tiết kiệm chi phí và tăng tốc độ.
5. **Provider Routing**: Dựa trên cấu hình trong `appsettings.json` để chọn nhà cung cấp:
   - **Ollama**: Phù hợp cho xử lý local, bảo mật dữ liệu nhạy cảm.
   - **OpenAI / Gemini**: Phù hợp cho các tác vụ suy luận phức tạp, phân tích chuyên sâu.
   - **Offline Fallback**: Tự động kích hoạt khi cấu hình mock hoặc không có kết nối internet/API key lỗi.
6. **Provider Call**: Thực hiện gửi HTTP request với timeout được cấu hình và cơ chế retry nhẹ (exponential backoff).
7. **Tool Call / RAG Detection**: Nhận diện nếu mô hình muốn gọi các tool đọc/ghi dữ liệu.
8. **Tool Parameter Security Guard**: Kiểm soát các tham số truyền vào tool (ví dụ: projectId, userId) để đảm bảo không bị ròỉ dữ liệu chéo dự án.
9. **Write Action Handling**: Với các hành động ghi (CreateTask, UpdateTask, ...), gateway không trực tiếp ghi vào database mà tạo một bản nháp (`AiGeneratedDraft`) dưới dạng chờ xác nhận từ người dùng.
10. **Schema Validation**: Với đầu ra yêu cầu cấu trúc cố định (ví dụ: JSON chứa actions, metrics, tables, charts), chạy validator. Nếu lỗi, thử gửi lại lời nhắc sửa định dạng (retry fix format).
11. **Usage Ledger / Audit Log**: Ghi nhận lượng token sử dụng, chi phí ước tính, thời gian phản hồi vào database (`AiUsageLedger`) phục vụ thống kê và tối ưu chi phí.

---

## 3. Các file cần sửa đổi hoặc tạo mới

Để hiện thực hóa kế hoạch này, chúng ta cần can thiệp vào các file sau:

| File / Đường dẫn | Mô tả thay đổi |
|---|---|
| `src/Qaly.Infrastructure/Services/AI/AiGateway.cs` | Thêm timeout, retry, tích hợp sâu RAG filter, nâng cấp cơ chế Schema validation và Tool calling an sau. |
| `src/Qaly.Infrastructure/DependencyInjection.cs` | Đăng ký `IAiGateway` bằng DI tự động thay vì khởi tạo chay để tiêm đủ các Repository và Service liên quan. |
| `src/Qaly.Web/appsettings.json` và `.Development.json` | Chuẩn hóa cấu hình `AiSettings`, bổ sung provider, model, timeout, retry settings. |
| `src/Qaly.Application/Services/AiService.cs` | Cải thiện các hàm tiện ích gọi qua Gateway để truyền đủ context người dùng và dự án. |
| `src/Qaly.Application/Services/ErumiChatService.cs` | Đảm bảo chat memory gửi ngữ cảnh lịch sử hội thoại đầy đủ qua gateway mà không làm tràn context window. |
| `tests/Qaly.UnitTests/AiGatewayEvidenceTests.cs` | Viết bổ sung các bài test chứng minh lỗi schema được bắt và retry thành công hoặc fallback có cảnh báo. |
| `docs/ai/ai-evaluation-dataset.md` | Bộ câu hỏi kiểm thử AI bằng tiếng Việt với đầy đủ các case ngữ cảnh và phân quyền. |

---

## 4. Rủi ro kỹ thuật & Giới hạn MVP

### Rủi ro kỹ thuật:
- **Tốc độ phản hồi (Latency)**: Các mô hình local chạy Ollama trên máy trạm có thể mất nhiều thời gian phản hồi (chậm).
  - *Giải pháp*: Hiển thị trạng thái đang xử lý trên UI, thiết lập timeout hợp lý (ví dụ: 30 giây) để tự động kích hoạt fallback warning thay vì để treo trình duyệt.
- **Tính chính xác của JSON**: Mô hình có thể không tuân thủ hoàn toàn schema được yêu cầu.
  - *Giải pháp*: Sử dụng system prompt cực kỳ chi tiết, dùng prompt sửa lỗi ở lượt retry và luôn validate trước khi parse.

### Giới hạn MVP tuần này:
- Chưa hỗ trợ RAG phức tạp trên toàn bộ tài liệu PDF/DOCX (vì đang trong phase 1), chỉ hỗ trợ trích xuất dữ liệu thực tế từ database (Tasks, Projects, Wiki Pages) và vector storage hiện trạng.
- Fallback mock sẽ hiển thị cảnh báo rõ ràng trên giao diện (ví dụ: *"Hệ thống đang chạy ở chế độ offline/dự phòng"*), tuyệt đối không giả vờ là kết quả phân tích AI thật để đảm bảo tính minh bạch khi nghiệm thu.
