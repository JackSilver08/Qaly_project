# BÁO CÁO CẢI TIẾN DỰ ÁN QALY

## 1. Phạm vi báo cáo

- Commit bắt đầu: `468b956a8722cf4156a8d666b59541d0a0dcf699`
- Commit kết thúc: `cf8e24aff2c1f335238f3dcae4f3d8d11bbed158` (`HEAD` tại thời điểm lập báo cáo)
- Khoảng thời gian: 01/07/2026 - 12/07/2026
- Phạm vi Git: `468b956^..HEAD`, bao gồm commit mốc
- Tổng số commit: **10**
- Commit thay đổi nội dung: **8**
- Merge commit: **2**
- Tổng số file thay đổi: **34**
- Quy mô thay đổi ròng: **5.072 dòng thêm, 3.011 dòng xóa**

> Báo cáo chỉ thống kê nội dung đã commit. Các thay đổi chưa commit trong working tree không được tính vào số liệu và nhận định dưới đây.

## 2. Tóm tắt điều hành

Trong giai đoạn này, Qaly được cải tiến tập trung vào bốn hướng chính:

1. Nâng cấp trợ lý phân tích Erumi và hạ tầng AI theo hướng cấu hình được, có kiểm soát tenant và ổn định hơn khi gọi provider.
2. Bổ sung phụ đề thời gian thực và luồng tạo biên bản AI cho phòng họp nhóm.
3. Thiết kế lại giao diện chatbot phân tích theo hướng tối giản, gom chức năng phụ vào menu và giảm mật độ điều khiển trên màn hình.
4. Cải thiện khả năng chạy dự án trong môi trường phát triển, hỗ trợ truy cập qua ngrok và bổ sung hướng dẫn khởi động cục bộ.

## 3. Các cải tiến nổi bật

### 3.1. Trợ lý AI và AI Gateway

Các thay đổi trong `AiService`, `ErumiChatService`, `AiGateway` và cấu hình ứng dụng giúp luồng AI rõ ràng và an toàn hơn:

- Gắn `TenantId` vào các yêu cầu AI liên quan đến dự án, bao gồm tóm tắt dự án, phân tích rủi ro, gợi ý giao việc, chat, chat streaming và analytics insight.
- Bổ sung phương thức lấy organization của dự án trước khi gửi yêu cầu AI.
- Giới hạn lịch sử chat theo tổng số ký tự, mặc định tối đa 12.000 ký tự, bằng cách loại bỏ các tin nhắn cũ nhất trước khi gọi AI.
- Cho phép cấu hình timeout và số lần retry qua `AiSettings:TimeoutSeconds` và `AiSettings:MaxRetries` thay vì cố định trong mã nguồn.
- Bổ sung cấu hình Gemini bên cạnh Ollama và OpenAI.
- Đơn giản hóa đăng ký dependency injection của `IAiGateway`.
- Loại bỏ constructor tương thích cũ dựa trên `IChatClient`, đưa test sang mô hình mock `IAiProvider` sát với kiến trúc runtime hơn.

Tác động:

- Giảm nguy cơ gửi sai ngữ cảnh giữa các organization.
- Hạn chế prompt quá dài khi hội thoại kéo dài.
- Dễ điều chỉnh độ ổn định và thời gian chờ của provider theo từng môi trường.
- Kiến trúc test và production thống nhất hơn.

### 3.2. Kiểm thử AI

`AiGatewayEvidenceTests` được cập nhật để kiểm tra kiến trúc provider mới và các hành vi vừa bổ sung:

- Kiểm tra retry theo cấu hình tùy chỉnh.
- Kiểm tra fallback khi provider timeout hoặc không khả dụng.
- Kiểm tra lịch sử dài được cắt về giới hạn 12.000 ký tự.
- Kiểm tra lịch sử ngắn được giữ nguyên.
- Kiểm tra đầu vào lịch sử `null` được xử lý an toàn.
- Tạo helper mock provider và gateway dùng chung cho test.

Tác động:

- Tăng độ tin cậy cho các tình huống lỗi provider.
- Bảo vệ hành vi cắt lịch sử chat khỏi regression.
- Giảm phụ thuộc của unit test vào implementation cũ.

### 3.3. Phòng họp và biên bản AI

Luồng họp nhóm được mở rộng đáng kể tại `GroupMeetingPage.vue`, `MeetingControls.vue` và `use-speech-recognition.ts`:

- Bổ sung nút bật/tắt phụ đề AI trên thanh điều khiển cuộc họp.
- Hiển thị trạng thái đang ghi nhận giọng nói và lỗi quyền microphone.
- Tự thử khởi động lại speech recognition khi phù hợp.
- Bổ sung tab phụ đề thời gian thực, số lượng đoạn transcript và tự cuộn theo nội dung mới.
- Bổ sung tab biên bản AI.
- Cho phép AI phân tích transcript, tạo phần tóm tắt và danh sách công việc cần làm.
- Cho phép tạo task từ action item được AI đề xuất.
- Bổ sung trạng thái thiếu transcript, đang phân tích, kết quả và phân tích lại.
- Cải thiện bố cục phòng họp, khu vực video, sidebar và thanh điều khiển.

Tác động:

- Chuyển dữ liệu hội thoại trong cuộc họp thành thông tin có thể hành động.
- Giảm thao tác ghi biên bản thủ công.
- Kết nối trực tiếp kết quả họp với workflow quản lý nhiệm vụ.

### 3.4. Thiết kế lại chatbot phân tích Erumi

Commit `cf8e24a` thực hiện đợt tái cấu trúc UI lớn cho chatbot:

- Tạo `ComposerPlusMenu.vue` để gom các chức năng vào nút `+` theo progressive disclosure.
- Tạo `OverflowMenu.vue` cho các hành động phụ, giảm số nút hiển thị thường trực.
- Tái cấu trúc `ErumiChatPanel.vue`, giảm các toolbar, pill và control cạnh tranh sự chú ý.
- Chuyển câu hỏi gợi ý thành lựa chọn điền nội dung vào composer để người dùng có thể chỉnh trước khi gửi.
- Tích hợp lựa chọn dự án, đính kèm file, công cụ phân tích, lịch sử, nguồn và thiết lập model vào luồng menu.
- Tách rõ empty state và active chat state.
- Cải thiện cấu trúc response, metrics, bảng, biểu đồ, file và hành động AI.
- Bổ sung menu overflow cho cuộc trò chuyện và từng phản hồi.
- Duy trì giao diện mobile, dark mode, keyboard focus và reduced motion.
- Cập nhật các bundle frontend tương ứng trong `wwwroot/dist`.

Tác động:

- Giảm mật độ nút trên màn hình phân tích.
- Tăng mức tập trung vào câu hỏi và câu trả lời chính.
- Các chức năng vẫn tồn tại nhưng chỉ hiển thị khi người dùng cần.
- Giao diện phù hợp hơn với mô hình chat composer hiện đại.

### 3.5. Truy cập môi trường phát triển và ngrok

Các thay đổi liên quan đến khởi động web và shell ứng dụng gồm:

- Điều chỉnh cấu hình web application để hỗ trợ truy cập qua tunnel/ngrok.
- Cập nhật frontend shell và bundle tương ứng.
- Bổ sung hướng dẫn chạy nhanh bằng SQL Server Express trong README.
- Ghi rõ lệnh restore, chạy backend và tài khoản demo phục vụ phát triển.

Tác động:

- Thuận tiện hơn khi demo hoặc kiểm thử từ thiết bị/mạng bên ngoài.
- Giảm thời gian thiết lập môi trường cho thành viên dùng SQL Server cục bộ.

### 3.6. Tài liệu định hướng AI-native dashboard

Tài liệu `docs/ai-native-dashboard-plan.html` được bổ sung với quy mô 1.337 dòng để mô tả kế hoạch phát triển dashboard theo hướng AI-native.

Tác động:

- Tạo tài liệu tham chiếu cho các đợt phát triển tiếp theo.
- Giúp thống nhất định hướng sản phẩm, UX và kiến trúc AI trước khi triển khai.

## 4. Thống kê theo người đóng góp

| Người đóng góp | Số commit | Ghi chú |
|---|---:|---|
| Tuan Tran Quang | 5 | Bao gồm 2 merge commit; tập trung ngrok, cấu hình web và chatbot UI |
| khangphma2511 | 5 | Tập trung transcript cuộc họp, biên bản AI, AI Gateway và tài liệu kế hoạch |
| **Tổng cộng** | **10** | 8 commit nội dung và 2 merge commit |

## 5. Danh sách commit

| Commit | Ngày | Tác giả | Nội dung chính |
|---|---|---|---|
| `468b956` | 01/07/2026 | khangphma2511 | Bổ sung hướng dẫn chạy cục bộ và cập nhật connection string |
| `1c592c8` | 02/07/2026 | Tuan Tran Quang | Hỗ trợ truy cập qua ngrok và cập nhật shell web |
| `a3ce6cc` | 02/07/2026 | Tuan Tran Quang | Merge nhánh `main` |
| `21ab726` | 08/07/2026 | khangphma2511 | Phụ đề thời gian thực và biên bản AI trong cuộc họp |
| `b9fda76` | 09/07/2026 | khangphma2511 | Bổ sung tài liệu kế hoạch AI-native dashboard |
| `41a6341` | 11/07/2026 | khangphma2511 | Cấu hình AI provider, tenant context, retry/timeout và test |
| `e844ba7` | 11/07/2026 | khangphma2511 | Hoàn thiện AI Gateway và điều chỉnh test |
| `2e35ffa` | 12/07/2026 | Tuan Tran Quang | Điều chỉnh đăng ký dịch vụ web và cấu hình runtime |
| `f129927` | 12/07/2026 | Tuan Tran Quang | Merge nhánh `main` |
| `cf8e24a` | 12/07/2026 | Tuan Tran Quang | Thiết kế lại chatbot Erumi theo hướng tối giản |

## 6. Các file có vai trò chính

- `src/Qaly.Web/ClientApp/components/chat/ErumiChatPanel.vue`
- `src/Qaly.Web/ClientApp/components/analytics-ai/ComposerPlusMenu.vue`
- `src/Qaly.Web/ClientApp/components/analytics-ai/OverflowMenu.vue`
- `src/Qaly.Web/ClientApp/pages/GroupMeetingPage.vue`
- `src/Qaly.Web/ClientApp/components/meeting/MeetingControls.vue`
- `src/Qaly.Web/ClientApp/composables/use-speech-recognition.ts`
- `src/Qaly.Application/Services/AiService.cs`
- `src/Qaly.Application/Services/ErumiChatService.cs`
- `src/Qaly.Infrastructure/Services/AI/AiGateway.cs`
- `tests/Qaly.UnitTests/AiGatewayEvidenceTests.cs`
- `src/Qaly.Web/appsettings.json`
- `src/Qaly.Web/appsettings.Development.json`
- `docs/ai-native-dashboard-plan.html`

## 7. Rủi ro và điểm cần theo dõi

- Connection string trong commit mốc mang thông tin máy cá nhân; nên chuyển phần tùy biến máy sang user secrets hoặc cấu hình local không commit.
- Các khóa OpenAI/Gemini đang là placeholder; secret thật không nên lưu trong repository.
- Speech recognition phụ thuộc khả năng hỗ trợ của trình duyệt và quyền microphone, cần kiểm thử trên các trình duyệt mục tiêu.
- Luồng tạo task từ biên bản AI cần tiếp tục kiểm tra quyền người dùng, dữ liệu đầu vào và cơ chế xác nhận trước khi ghi dữ liệu.
- Bundle trong `wwwroot/dist` được commit cùng source; cần thống nhất quy trình build để tránh source và bundle lệch phiên bản.
- Tên một số commit chưa mô tả rõ nội dung (`small up`, `commit xàm`), làm giảm khả năng truy vết lịch sử. Nên dùng commit message theo phạm vi và kết quả thay đổi.

## 8. Đề xuất bước tiếp theo

1. Bổ sung integration test cho luồng Erumi, transcript đến biên bản AI và tạo task.
2. Kiểm thử UI chatbot trên desktop, mobile, dark mode và keyboard-only.
3. Đưa connection string và API key ra khỏi cấu hình dùng chung.
4. Thiết lập quy ước commit message, ví dụ `feat(ai):`, `feat(meeting):`, `refactor(ui):` và `fix(config):`.
5. Thêm CI kiểm tra typecheck, frontend build và unit test trước khi merge.
6. Đo timeout, retry rate, fallback rate và độ dài prompt để theo dõi chất lượng AI Gateway trong thực tế.

## 9. Lệnh tái lập số liệu

```powershell
git rev-list --count 468b956^..HEAD
git rev-list --count --no-merges 468b956^..HEAD
git rev-list --count --merges 468b956^..HEAD
git shortlog -sne 468b956^..HEAD
git diff --shortstat 468b956^ HEAD
git log 468b956^..HEAD --reverse --date=short --pretty=format:"%h`t%ad`t%an`t%s"
```

---

Ngày lập báo cáo: 12/07/2026  
Nguồn dữ liệu: lịch sử Git của repository Qaly.
