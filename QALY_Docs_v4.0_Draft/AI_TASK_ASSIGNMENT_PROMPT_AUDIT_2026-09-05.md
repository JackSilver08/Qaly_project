# Prompt tạo Task và giao người — 05/09/2026

Phạm vi: sửa cách hiểu yêu cầu tạo/giao Task và cung cấp kịch bản kiểm tra UI quản lý dự án. Không thay bố cục UI, không ghi database demo, không gọi provider thật, không commit/push. Các thay đổi từ lượt trước trong worktree được giữ nguyên.

## Nguyên nhân và cách sửa

| Bằng chứng source | Sửa trong lượt này |
| --- | --- |
| Task fallback dùng nguyên `UserIntent` làm nội dung; người nhận chưa có trường riêng trong snapshot. | Tách nội dung, số lượng, người nhận và lựa chọn chưa giao; thêm trường server-owned vào snapshot. Fallback không dùng câu điều khiển làm business content; giữ danh sách tiêu đề khi số mục khớp yêu cầu. |
| Tên người ở prompt chưa được đối chiếu trước khi soạn draft. | Resolve trong danh sách member/owner đang hoạt động của Project đã được kiểm quyền. Tên không dấu được hỗ trợ; tên ngắn phải khớp duy nhất. Không tìm thấy/trùng tên thì hỏi lại, không chọn người đầu tiên. |
| Câu có số lượng/người nhận nhưng không có nội dung vẫn có thể được soạn thành Task chung chung. | Yêu cầu tạo mới kiểu `Tạo 3 task giao cho Kiệt` cần bổ sung nội dung trước khi enqueue. `Giao 3 task…` là phân công task hiện có, không phải tạo mới. |
| Các nhánh chat phân công dùng logic lặp; một nhánh chỉ lấy task đầu trong selection. | Dùng chung luồng phân công; truyền toàn bộ task được chọn, kiểm tra explicit count và selected scope. Thiếu selection phải hướng dẫn, không tự lấy task bất kỳ. |
| Phương án phân công xếp hạng theo điểm, chưa giữ người được yêu cầu. | Giữ người chỉ định ở phương án chính; nếu thiếu capacity/evidence thì hiển thị blocker và bỏ chọn mutation, không tự tráo người. Các ứng viên khác vẫn là phương án để review. |
| Phân công batch có thể tái sử dụng phần capacity còn lại cho từng task. | Trừ phần giờ đã dành cho task trước trong cùng proposal trước khi đánh giá task tiếp theo. |
| Idempotency key của proposal chưa gắn đủ payload yêu cầu. | Hash gồm Project, các Task, khoảng thời gian và người nhận. Dùng lại key với payload khác trả conflict, không trả proposal của người cũ. |
| Từ có dấu sau normalize dễ trùng động từ (`gần`/`gán`, `giao diện`/`giao`). | Phân loại theo cấu trúc hành động–đối tượng; không nhận cụm `giao diện cho người dùng` là chỉ dẫn giao việc. Có ca hồi quy cho dấu tiếng Việt, nội dung đặt trong dấu nháy và emoji. |

Chuỗi kiểm tra: prompt → scope/selected Task → capability → Project members được phép → typed draft/proposal → chỉnh và xác nhận → canonical Task/TaskAssignment read-back → retry.

## Hợp đồng nghiệp vụ giữ nguyên

- Model không được tự phát sinh assignee ID. Người chỉ định được máy chủ resolve và đưa lên card dưới dạng lựa chọn của người dùng (`user_selected`), giống lựa chọn thủ công trong bước review.
- **Chỉ định người khi tạo Task không phải bằng chứng AI đã xác minh skill/capacity.** Card có cảnh báo rõ; quyền tạo/giao và kiểm tra ở bước confirm vẫn áp dụng. Không bổ sung bằng chứng kỹ năng giả hay suy luận capacity từ khoảng trống.
- Với phân công Task có sẵn, giữ các gate skill/capacity/lịch/stale draft; không bỏ blocker để làm nút xác nhận sáng.
- `để chưa giao` giữ backlog. Nhiều chỉ dẫn giao người trong một prompt không bị gom thành một người; người dùng cần chỉ rõ batch hoặc chỉnh từng card.
- Chưa hỗ trợ mọi cách diễn đạt tự nhiên. Cách ổn định để test: `Tạo 3 task: A, B, C; giao cho [họ tên]`. Khi thiếu nội dung, gửi lại yêu cầu đầy đủ theo hướng dẫn; không hứa tự ghép một câu trả lời ngắn vào batch trước.

## Xác minh

Kết quả trên bản source cuối của lượt này:

- Solution build: **PASS**, 0 warning, 0 error.
- Toàn bộ Unit: **956/956 PASS**, 0 skipped.
- API tập trung ba nhóm dưới đây: **69/69 PASS**, 0 skipped.
- `git diff --check` trên các file tracked liên quan: **PASS**.

```powershell
dotnet build Qaly_project.slnx --no-restore --verbosity minimal
dotnet test tests/Qaly.UnitTests/Qaly.UnitTests.csproj --no-build --no-restore --logger "console;verbosity=minimal"
dotnet test tests/Qaly.IntegrationTests/Qaly.IntegrationTests.csproj --no-build --no-restore --filter "FullyQualifiedName~AiActionComposerApiTests|FullyQualifiedName~AiAssistantTurnApiTests|FullyQualifiedName~PortfolioScheduleApiTests" --logger "console;verbosity=minimal"
```

API integration dùng database InMemory/provider fixture cô lập: kiểm tra ba Task có title/assignee đúng sau confirm, đọc lại ở DbContext mới, retry không trùng; clarification không tạo job/task; phân công nhiều task đúng selection; người thiếu capacity được giữ nhưng bị chặn; key trùng payload khác bị từ chối. Đây không phải manual E2E trên database demo hay chứng nhận chất lượng provider thật.

Không chạy lại frontend typecheck/build vì lượt này không sửa frontend. Chưa chạy browser, chưa đánh UI PASS hoặc PRODUCT_ACCEPTED. Áp dụng skill `ai-engineer` để kiểm tra hợp đồng đầu ra, quyền và regression tests theo bằng chứng.

## Bàn giao UI

Mở [Bộ prompt và các bước kiểm tra UI](../PROMPTS_DEMO_QUAN_LY_DU_AN.md): Q01–Q15, từ tạo Project/Task đến phân công, checklist, subtask, gửi duyệt/trả lại/hoàn thành, báo cáo và quyền Member. Người dùng chạy trên preview và annotation vị trí lỗi; không tự sửa bố cục trước khi có phản hồi.
