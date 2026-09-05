# Rà soát hiểu prompt — Assistant và Analytics

Ngày: 04/09/2026. Phạm vi: sửa lỗi có bằng chứng trong routing, fallback, context và diễn giải lựa chọn người dùng; không viết lại AI Native V4, không đổi kết luận nghiệm thu P01–P28.

## Các lỗi đã sửa

| Nhóm | Bằng chứng / ví dụ tái hiện | Thay đổi |
| --- | --- | --- |
| Phủ định, câu hỏi và từ gần giống | “Đừng khởi tạo dự án”, “Ai tạo Project này?”, “Tóm tắt weekly digest”, “task đào tạo nhân sự” bị nhận nhầm thành action; “Nhờ AI soạn…” lại bị nhận thành câu hỏi “ai” | Nhận diện ranh giới từ, phủ định theo mệnh đề, câu hỏi về thao tác; phân biệt dùng/dừng và lời nhờ trợ lý với câu hỏi về người thực hiện. |
| Nội dung được trích dẫn | Tên task hoặc câu ví dụ chứa “Create a project” có thể chiếm intent | Chỉ che nội dung trích dẫn trong bản sao dùng để phân loại; giữ nguyên prompt gốc và dữ liệu. |
| Phân biệt Task / Project / thao tác phụ | “Tạo task để theo dõi Project”, “tạo task để chạy test CAND”, task có acceptance criteria | Giữ mục tiêu thao tác đầu tiên, không để mục đích hoặc thuộc tính phía sau đổi thành Monitor, test runner, Launch hay Checklist. |
| Bản nháp và quyền ghi | “Soạn 4 task, chưa ghi dữ liệu” bị coi là chỉ đọc; P25 Member yêu cầu vẫn được phân tích bị coi là tạo/giao task | Tách yêu cầu soạn bản nháp khỏi quyền thực thi. Phủ định tạo/giao trong câu hỏi phân quyền không sinh card mutation. |
| Lịch sử hội thoại | “OK, phân tích…” bị kéo về Launch cũ; lời gợi ý của assistant thành ý định người dùng | Yêu cầu hiện tại thắng lịch sử. Continuation chỉ dùng chủ đề user gần nhất trong cửa sổ lịch sử; bỏ qua các lượt “tiếp tục” trung gian, không coi lời bot là chỉ thị. |
| Đếm số Task | “Đang có 10 task; thêm 3 task” lấy 10; “muoi hai task” lấy 2; số bốn chữ số bị bỏ qua | Đếm tại đúng đối tượng tạo mới, hỗ trợ chữ không dấu. Giữ số lớn để trả lỗi giới hạn batch thay vì âm thầm giảm số lượng. |
| Model / fallback | Model chọn Launch dù người dùng chỉ yêu cầu phân tích; fallback có bộ keyword riêng làm sống lại action đã phủ định | Cùng dùng quyết định routing của server. Không thay action bị từ chối quyền bằng action khác. Reader không bị nâng thành draft chỉ vì draft có risk class read-only. |
| Scope Group / Project | Đã chọn Project nhưng đang đứng ở Group: discovery lấy quyền Group còn resolve đọc Project | Đồng bộ ưu tiên scope và lịch sử ở discovery. Explicit Group/Poll vẫn kiểm quyền Group riêng; không mượn quyền Project để đọc Group riêng tư. |
| Lời chào kèm công việc | “Xin chào, phân tích workspace…” chỉ nhận câu chào | Chỉ chào đơn thuần mới dùng câu trả lời chào; câu có công việc tiếp tục đến nguồn Analytics. |
| Yêu cầu trình bày | “Không cần biểu đồ” làm mất cả bảng/số liệu; “ngắn gọn, kèm bảng” thành text thuần; nhánh Project nhận yêu cầu bảng nhưng không dựng bảng | Tắt riêng từng loại kết quả. Yêu cầu ngắn gọn không hủy bảng/số liệu được chỉ định. Dựng bảng trạng thái từ Analytics canonical, giải thích quá hạn không phải nhóm cộng thêm; không dựng chart rỗng khi thiếu dữ liệu chuỗi/thành viên. |
| Form Launch có ưu tiên | Lưu form tên “Qaly SPA Services” bị caption “Cập nhật Project Launch Brief…” suy diễn lại tên | Form đầy đủ hiện tại là nguồn authoritative; không chạy suy luận từ caption để ghi đè các trường đã review. |
| Công cụ chat cũ | Tool list của lượt chat chứa CreateTask/AssignTask/UpdateTaskStatus… | Chỉ đưa các tool đọc đã biết vào conversation. Ghi dữ liệu đi qua typed draft/review/confirm; lọc quyền hiện có vẫn được giữ. Đây là bảo vệ ở tầng service, không khẳng định endpoint legacy đang mở. |

## Kiểm chứng

Kết quả lượt xác minh cuối trên code hiện tại:

| Kiểm tra | Kết quả |
| --- | --- |
| Toàn bộ backend UnitTests | **928/928 PASS**, 0 skipped |
| Ba lớp API liên quan đến Assistant/native actions/composer | **67/67 PASS**, 0 skipped |
| Build `Qaly_project.slnx` | **PASS**, 0 warning / 0 error |
| Frontend `npm run typecheck` | **PASS** |
| `git diff --check` các file trong phạm vi sửa | **PASS** |

Số test là tổng các suite được chạy, bao gồm kiểm thử có sẵn; không phải 995 test mới. Lỗi hồi quy được xác minh trước và sau sửa, gồm hai case “Nhờ AI soạn…” / “Tôi muốn trợ lý AI tạo…” trước đó nhận nhầm thành read.

Các lớp kiểm tra:

- Unit: `AiPromptRoutingRegressionTests`, `AiAssistantGoalPlanningContractTests`, `AiActionComposerContractTests`, `AiAssistantContextRegistryTests`, `ErumiChatServiceTests`.
- Fixture routing tiếng Việt: tất cả trường hợp có expected capability phải đúng, không chỉ đạt tỷ lệ tổng; count của Task create được assert thực sự.
- API: `AiAssistantTurnApiTests`, `AiNativeDomainActionsApiTests`, `AiActionComposerApiTests`. Bao gồm card P16–P24, xác nhận, canonical read-back, retry, stale draft và kiểm quyền.
- Build solution và frontend typecheck.

Lệnh xác minh (PowerShell, từ root dự án):

```powershell
dotnet build Qaly_project.slnx --no-restore --verbosity minimal
dotnet test tests/Qaly.UnitTests/Qaly.UnitTests.csproj --no-build --no-restore --logger "console;verbosity=minimal"
dotnet test tests/Qaly.IntegrationTests/Qaly.IntegrationTests.csproj --no-build --no-restore --filter "FullyQualifiedName~AiAssistantTurnApiTests|FullyQualifiedName~AiNativeDomainActionsApiTests|FullyQualifiedName~AiActionComposerApiTests" --logger "console;verbosity=minimal"
npm run typecheck
```

API tests dùng database InMemory cô lập và provider fixture của `IntegrationTestFactory`; không ghi database demo, không chứng minh chất lượng ngôn ngữ của provider thật.

## Giới hạn và replay ngắn

Không chạy Chromium, không gọi provider trả phí và không commit/push trong lượt này. Chưa đánh `PRODUCT_ACCEPTED` hay manual PASS mới. Ngôn ngữ tự nhiên ngoài bộ hồi quy vẫn cần theo dõi; không khẳng định đã hiểu đúng mọi prompt.

Sau khi backend dùng build mới, có thể thử các prompt sau trên cả Assistant và Analytics:

1. `Không tạo Project; chỉ phân tích tiến độ Project hiện tại.` → đọc dữ liệu, không có Launch/draft tạo mới.
2. `Tạo đúng 10 Task có mô tả và tiêu chí nghiệm thu, chưa ghi dữ liệu.` → Task draft, không phải Checklist/Launch.
3. `Đang có 10 task; hãy thêm 3 task mới.` → số yêu cầu mới là 3.
4. `Phân tích Project chi tiết, không cần biểu đồ.` → giữ bảng/số liệu, không chart.
5. `Với Task đang mở, soạn 5 mục acceptance checklist kiểm chứng được.` → card checklist.
6. `Tách Task đang mở thành 4 subtask.` → card breakdown có 4 mục.
7. Đổi từ chủ đề Launch sang tóm tắt Wiki, rồi `tạo task từ đó`; sau đó hỏi câu phân tích khác → không kẹt ở chủ đề Launch.
8. Sửa tên/phạm vi trong form Launch rồi lưu → giữ nguyên giá trị đã nhập sau reload.
9. `Nhờ AI soạn 3 task cho Sprint 1` → Task draft; `Ai tạo Project này?` hoặc `Không nhờ AI tạo Project mới` → chỉ đọc, không mở Launch.

Áp dụng skill `ai-engineer` theo hướng kiểm tra hợp đồng đầu ra, giới hạn tool/quyền và test hồi quy theo bằng chứng. Các thay đổi khác đã có sẵn trong worktree được giữ nguyên.
