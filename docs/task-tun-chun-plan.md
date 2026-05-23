# Task Tún Chừn

## Mục tiêu

Ổn định nền tảng Task/Kanban trước, sau đó mở rộng Sprint/Timeline, workload, dashboard, realtime, notification, wiki, AI gateway và Meetily import. Kế hoạch này ưu tiên các phần đang là dependency của nhiều module khác để tránh sửa ngược API và UI nhiều lần.

## Hiện trạng nhanh

- Task API đã có nền: `TasksController`, `TaskService`, `TaskItem`, `TaskDependency`, `TaskStatusRules`, Gantt endpoint, attention signal, audit log và notification khi đổi trạng thái.
- Kanban hiện có cập nhật `status` và `sort-order` riêng lẻ, nhưng chưa có endpoint move atomic.
- Chưa có optimistic concurrency cho task, nên có rủi ro ghi đè dữ liệu khi nhiều người cập nhật cùng lúc.
- Dependency đã có schema cơ bản, nhưng chưa có cảnh báo blocked đầy đủ và chưa detect circular dependency.
- Chưa thấy schema Sprint riêng.
- Dashboard hiện tính nhiều số liệu sau khi include dữ liệu lớn, cần tách aggregate query.
- Notification chưa có dedupe/idempotency key.
- Wiki chưa có visibility/public policy.
- Chat frontend đã có component, nhưng backend room/message/RBAC chưa rõ.
- AI job đã có nền, nhưng thiếu usage ledger, budget guard và gateway contract đầy đủ.

## Phase 1: Task API và Kanban ổn định

### Mục tiêu

API Task/Kanban ổn định, tài liệu request/response chuẩn hóa, kéo-thả đúng workflow rule và không mất dữ liệu khi cập nhật đồng thời.

### Công việc

1. Chuẩn hóa contract Task API.
   - Thống nhất response envelope: `isSuccess`, `data`, `error`, `statusCode`, `traceId`.
   - Chuẩn hóa lỗi validation, forbidden, conflict, not found.
   - Viết tài liệu endpoint trong `docs/api/tasks.md`.

2. Thêm optimistic concurrency.
   - Thêm `RowVersion` vào `TaskItem`.
   - Response task trả `rowVersion`.
   - Update/move nhận `rowVersion` hoặc `If-Match`.
   - Stale update trả `409 Conflict` kèm task mới nhất.

3. Làm endpoint Kanban atomic.
   - `GET /api/projects/{projectId}/kanban`
   - `PATCH /api/projects/{projectId}/kanban/move`
   - Request gồm `taskId`, `fromStatus`, `toStatus`, `beforeTaskId`, `afterTaskId`, `rowVersion`.
   - Validate bằng `TaskStatusRules.CanTransition`.
   - Cập nhật status và sort order trong một transaction.
   - Giữ rule không cho chuyển `Done` nếu thiếu approved evidence.

4. Test bắt buộc.
   - Move đúng rule thành công.
   - Move sai rule trả `400`.
   - Hai client update cùng task: request stale trả `409`.
   - Sort order không duplicate sau nhiều lần kéo-thả.

### Tiêu chí nghiệm thu

- Kanban kéo-thả không làm mất field khác của task.
- Cập nhật đồng thời không ghi đè im lặng.
- API Task/Kanban có tài liệu request/response rõ ràng.
- Unit/integration tests phủ workflow rule và concurrency.

## Phase 2: Sprint, Timeline và Dependency

### Mục tiêu

Có API Sprint/Timeline, biểu diễn dependency rõ, tính đúng tiến độ theo hạn và trạng thái, cảnh báo dependency bị nghẽn.

### Công việc

1. Thêm schema Sprint.
   - `Sprint`: `ProjectId`, `Name`, `StartDate`, `EndDate`, `Status`.
   - `TaskItem.SprintId` nullable.
   - Index theo `ProjectId`, `SprintId`, `Status`.

2. Chuẩn hóa timeline API.
   - `GET /api/projects/{projectId}/timeline`
   - `GET /api/projects/{projectId}/sprints`
   - `GET /api/projects/{projectId}/sprints/{sprintId}/timeline`

3. Mở rộng dependency.
   - Response có `predecessors`, `successors`, `blockedBy`, `isBlocked`, `blockingReason`.
   - Detect circular dependency khi tạo dependency.
   - Cảnh báo successor bị nghẽn khi predecessor chưa `Done`.

4. Tính progress.
   - Chỉ tính task có `ContributesToProgress = true`.
   - Công thức mặc định: `done / total`.
   - Có thể mở rộng weighted progress theo `EstimatedHours`.

### Tiêu chí nghiệm thu

- Timeline hiển thị đúng task, hạn, dependency.
- Không tạo được dependency vòng.
- Blocked warning khớp dữ liệu thực tế.
- Progress project/sprint kiểm tra chéo được bằng query SQL.

## Phase 3: Workload, Dashboard và Performance

### Mục tiêu

Bổ sung trường dữ liệu workload và endpoint tổng hợp cho AI recommendation. Dashboard đúng số liệu, API trọng yếu đạt mục tiêu hiệu năng nội bộ.

### Công việc

1. Workload API.
   - `GET /api/projects/{projectId}/workload`
   - `GET /api/users/{userId}/workload`
   - Trường: `assignedOpen`, `inProgress`, `overdue`, `dueSoon`, `estimatedRemainingHours`, `actualHours7d`, `actualHours30d`, `completed7d`, `completed30d`, `blockedCount`.

2. Deterministic scoring.
   - Tạo `WorkloadScoringService`.
   - Score có breakdown: capacity, urgency, risk, history.
   - Test bằng golden dataset để tái lập điểm.

3. Dashboard aggregate.
   - Tách query aggregate thay vì include toàn bộ graph.
   - API metric riêng cho overview, project health, sprint burndown, overdue và throughput.

4. SQL index và benchmark.
   - Index task theo project/status/sort, assignee/status, due date, sprint/status.
   - Index dependency predecessor/successor.
   - Index notification user/read.
   - Seed dữ liệu lớn để đo P95.

### Tiêu chí nghiệm thu

- Workload score deterministic và giải thích được.
- Dashboard metric khớp query SQL kiểm tra chéo.
- P95 API trọng yếu đạt mục tiêu nội bộ.

## Phase 4: Realtime, Notification, Idempotency và Wiki

### Mục tiêu

Chat realtime ổn định, notification đúng người nhận và không trùng, wiki kiểm soát truy cập đúng visibility.

### Công việc

1. Chat backend.
   - Thêm `ChatRoom`, `ChatMessage`.
   - API list room, list message, send message.
   - Hub join room phải kiểm tra project membership.
   - Không lộ room ngoài quyền.

2. Notification template.
   - Tạo registry/template cho event.
   - Event chính: task created, task assigned, status changed, comment, chat mention, dependency blocked.
   - Thêm `DedupeKey` unique.

3. Idempotency.
   - Hỗ trợ `Idempotency-Key` cho import, send message, notification/event publish, AI job.
   - Retry cùng key trả kết quả cũ, không nhân bản dữ liệu.

4. Wiki visibility.
   - Thêm `Visibility`: `Private`, `Project`, `Public`.
   - Customer portal chỉ đọc `Public`.
   - Test RBAC policy.

### Tiêu chí nghiệm thu

- Tin nhắn đồng bộ đúng room.
- User không join/read được room ngoài quyền.
- Notification không gửi trùng khi retry/reconnect.
- Khách hàng chỉ thấy wiki public.

## Phase 5: AI Gateway, Cost, Compliance và Meetily

### Mục tiêu

Toàn bộ tác vụ AI chạy qua gateway, có cost tracking, budget guard, audit trail và fallback. Meetily import ổn định, chống import trùng, output AI đúng schema.

### Công việc

1. AI Gateway.
   - API tạo job, xem job, confirm draft, cancel job.
   - Frontend không gọi AI provider trực tiếp.
   - Fallback provider: Ollama, OpenAI, mock/offline cache.

2. Cost ledger.
   - Thêm `AiUsageLedger`: `ProjectId`, `Provider`, `Model`, `InputTokens`, `OutputTokens`, `EstimatedCost`, `JobId`.
   - Budget theo project/provider.
   - Chặn đúng khi vượt hạn mức.

3. Audit và privacy.
   - Mỗi AI job lưu input hash, source, user, sensitivity, redaction decision.
   - Rule không gửi dữ liệu nhạy cảm ra provider ngoài nếu policy chặn.

4. Meetily import.
   - Ổn định `/api/meetings/import/meetily`.
   - Chống trùng bằng `source_hash`.
   - Mapping `meeting_action_item -> task`.
   - AI extraction tạo draft, không tạo task thật trước khi confirm.
   - Golden dataset test schema pass.

5. Semantic optional.
   - Qdrant bật/tắt bằng config.
   - Tắt Qdrant hệ thống vẫn chạy P0 bình thường.
   - Bật Qdrant trả kết quả có nguồn.

### Tiêu chí nghiệm thu

- AI job có audit trail đầy đủ.
- Theo dõi được chi phí theo project/provider.
- Budget guard chặn đúng.
- Import Meetily mẫu thành công và truy vết được nguồn.
- Không tạo task thật khi chưa confirm.

## Phase 6: UI và Regression Demo

### Mục tiêu

UI đồng nhất, flow rõ, responsive ổn định, danh sách lớn thao tác mượt và demo chính không còn lỗi nghiêm trọng.

### Công việc

1. Token và component core.
   - Chuẩn hóa button, input, modal, table/list, toast, empty/error/loading state.
   - Giảm CSS trùng lặp.

2. Flow chính.
   - Task/Kanban.
   - Project dashboard.
   - Sprint/Timeline.
   - Report.
   - Meetily import/confirm.

3. Responsive.
   - Kiểm tra breakpoint mobile/tablet/desktop.
   - Đảm bảo thao tác chính dùng được trên mobile.

4. Performance UI.
   - Pagination hoặc virtualization cho list lớn.
   - Giảm lag khi lọc, kéo-thả, scroll.

5. QA regression.
   - Checklist smoke test.
   - Danh sách lỗi đã xử lý.
   - Kịch bản demo offline/gián đoạn AI provider.

### Tiêu chí nghiệm thu

- Các màn P0 đồng nhất phong cách.
- Không vỡ layout ở breakpoint chính.
- Task/Kanban/Report phản hồi nhanh với seed lớn.
- Không còn lỗi nghiêm trọng ảnh hưởng demo chính.

## Thứ tự ưu tiên đề xuất

1. Task contract, concurrency và Kanban atomic move.
2. Sprint/timeline/dependency blocked warning.
3. Workload, dashboard aggregate và SQL index.
4. Notification idempotency, chat RBAC.
5. Wiki visibility.
6. AI gateway, cost, compliance, Meetily.
7. UI cleanup và regression checklist.

## Sprint đầu tiên nên làm

Sprint đầu tiên nên khóa phạm vi vào Task/Kanban:

- Chuẩn hóa API contract.
- Thêm `RowVersion`.
- Làm endpoint Kanban move atomic.
- Viết docs request/response.
- Test workflow rule và concurrency.

Đây là nền cho Sprint/Timeline, Dashboard, AI recommendation và UI Kanban, nên hoàn thành phần này trước sẽ giảm rủi ro phải sửa ngược các module sau.
