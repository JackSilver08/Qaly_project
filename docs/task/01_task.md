Được rồi, mình sẽ viết thẳng ra dạng text chi tiết, rõ ràng.

QALY — Báo Cáo Kỹ Thuật Nâng Cấp Toàn Diện

CHƯƠNG 1 — NHỮNG GÌ CẦN SỬA NGAY (Critical Fixes)

1.1 AI Security — Metadata Filtering trong Qdrant
Vấn đề: Đây là lỗ hổng nghiêm trọng nhất. Nếu vector search không bắt buộc filter theo project_id + user_id, một member của dự án A có thể nhận kết quả RAG chứa thông tin nhạy cảm từ dự án B thông qua chatbot Erumi. Nguy hiểm hơn nữa nếu task có IsPrivate = true nhưng vector vẫn được index không có flag đó.
Cần làm:
Mỗi vector point khi ingestion vào Qdrant phải có đủ payload metadata sau: project_id, task_id, owner_id, is_private (bool), visibility (enum: private/member/public), content_type (task/comment/attachment), created_at.
Tại tầng Infrastructure, hàm search phải bắt buộc nhận projectId và userId làm tham số, không bao giờ cho phép gọi search mà không có filter. Filter phải có ít nhất 2 điều kiện: project_id khớp với dự án hiện tại, và visibility phù hợp với quyền của user đang đăng nhập.
Khi một task bị xóa hoặc bị set IsPrivate = true, phải xóa/cập nhật vector tương ứng trong Qdrant ngay lập tức (xem thêm mục 1.2 về sync).

1.2 Vector Sync Consistency — SQL Server ↔ Qdrant
Vấn đề: Khi task/comment bị cập nhật hoặc xóa trong SQL Server nhưng Qdrant chưa sync kịp, Erumi sẽ trả về thông tin lỗi thời hoặc đề xuất dựa trên dữ liệu đã không còn tồn tại. Trường hợp nguy hiểm nhất: task được set private nhưng vector vẫn còn public trong Qdrant.
Giải pháp đề xuất — Outbox Pattern:
Tạo một bảng OutboxMessages trong SQL Server với các cột: Id, EventType, Payload (JSON), CreatedAt, ProcessedAt (nullable), RetryCount. Mỗi khi có thay đổi dữ liệu (task tạo/sửa/xóa, comment thêm/xóa, project thay đổi visibility), ghi một record vào bảng này trong cùng transaction với thao tác chính. Một Background Worker (VectorSyncWorker kế thừa BackgroundService) chạy mỗi 5 giây, đọc các message chưa xử lý, gọi Qdrant để upsert hoặc delete vector tương ứng, rồi đánh dấu ProcessedAt. Nếu xử lý thất bại thì tăng RetryCount, retry tối đa 3 lần rồi mới bỏ qua và alert.
Các event cần handle: TaskCreated → upsert vector mới. TaskUpdated → upsert lại với nội dung mới. TaskDeleted → delete tất cả vectors có task_id tương ứng. TaskPrivacyChanged → nếu set private thì delete hoặc update visibility = "private". CommentAdded/Deleted → tương tự.

1.3 Cookie Auth — Tăng cường bảo mật Session
Vấn đề: Cookie-based auth hiện tại không có cơ chế server-side revoke. Nếu tài khoản bị compromise hoặc user đổi mật khẩu, session cũ vẫn còn hiệu lực cho đến khi hết hạn.
Cần làm: Lưu session vào Redis thay vì chỉ dùng cookie. Cookie chỉ chứa session ID, data thực tế ở Redis. Khi user đổi mật khẩu, xóa tất cả session của user đó trong Redis. Thêm endpoint DELETE /api/auth/sessions để user tự revoke tất cả session (đăng xuất mọi thiết bị). Đặt Cookie.HttpOnly = true, Cookie.SecurePolicy = Always, Cookie.SameSite = Strict. Session timeout sliding 30 phút, absolute max 8 giờ.

CHƯƠNG 2 — NHỮNG GÌ CẦN TỐI ƯU HÓA (Optimization)

2.1 AI Performance — Streaming Response
Vấn đề: Hiện tại Erumi phải đợi Ollama generate xong toàn bộ response rồi mới trả về client. Với Llama 3.2 chạy local, điều này có thể mất 15–30 giây, user nhìn màn hình trống trong suốt thời gian đó — UX rất tệ.
Giải pháp: Dùng IAsyncEnumerable để stream token từ Ollama qua Microsoft.Extensions.AI, rồi push từng token về client qua SignalR. Client nhận được token nào hiển thị ngay token đó, giống ChatGPT.
Luồng kỹ thuật: AiHub (SignalR Hub) nhận request từ client → gọi ErumiStreamingService.StreamResponseAsync() trả về IAsyncEnumerable<string> → mỗi token yield ra thì gọi Clients.Caller.SendAsync("ReceiveToken", token) → khi xong gọi SendAsync("StreamComplete"). Client Vue.js lắng nghe event ReceiveToken và append vào reactive string, StreamComplete thì tắt loading indicator.

2.2 Database — Index & Query Optimization
Cần thêm các index sau (hiện tại nhiều khả năng chưa có, dễ gây full table scan khi data lớn hơn):
Cho bảng TaskItem: index composite trên (ProjectId, Status) include thêm Title, Priority, AssigneeId, DueDate, với filter WHERE IsDeleted = 0. Đây là query chạy nhiều nhất khi load Kanban board.
Cho bảng Notification: index trên (UserId, IsRead) include CreatedAt, Message với filter WHERE IsRead = 0. Query đếm unread count chạy cho mỗi user mỗi khi refresh trang.
Cho bảng AuditLog: index trên (ProjectId, CreatedAt DESC) include UserId, Action. Activity feed của project cần query này rất thường xuyên.
Cho bảng TaskComment: index trên (TaskId, CreatedAt DESC) để pagination comment nhanh.
Pagination: Chuyển từ OFFSET/FETCH sang Keyset Pagination (còn gọi là cursor-based). Với OFFSET, query page 100 vẫn phải đọc qua 99 page trước. Với Keyset, dùng WHERE Id > lastSeenId ORDER BY Id thì luôn là O(1) bất kể page nào.
EF Core: Đảm bảo dùng AsNoTracking() cho tất cả read-only queries (Kanban board, danh sách task, notifications). Không dùng Include() dây chuyền quá 3 cấp — dùng projection sang DTO thay thế.

2.3 Caching Strategy — Redis Multi-layer
Hiện tại Redis đang được dùng nhưng chưa rõ chiến lược caching. Đề xuất áp dụng 2 tầng cache:
Tầng 1 là IMemoryCache (in-process, dưới 1ms) cho các dữ liệu thay đổi rất ít như: danh sách member của project (TTL 5 phút), cấu hình project, role của user trong project. Tầng 2 là Redis (distributed, dưới 5ms) cho: task list theo Kanban (TTL 30 giây), unread notification count của user (TTL 10 giây), kết quả AI search thường gặp (TTL 2 phút).
Cache invalidation: Khi có write operation liên quan (task thêm/sửa/xóa), phải xóa cache key tương ứng ngay lập tức. Dùng pattern cache key dạng project:{projectId}:tasks:kanban và user:{userId}:notifications:unread-count để dễ invalidate theo nhóm.

2.4 RAG Pipeline — Nâng chất lượng retrieval
Chunking strategy: Hiện tại nếu đang chunk cả task content thành một vector thì chưa tối ưu. Nên tách ra: một vector cho title + description ngắn gọn (context chunk), một vector cho toàn bộ comments (conversation chunk). Mỗi chunk phải có chunk_type trong metadata.
Hybrid search: Kết hợp vector search (semantic) với keyword search (BM25) để tăng recall. Qdrant hỗ trợ hybrid search từ v1.7. Query đến với cả dense vector lẫn sparse vector, kết quả được re-rank bằng Reciprocal Rank Fusion.
Re-ranking: Sau khi lấy top-20 kết quả từ Qdrant, chạy thêm một bước re-rank nhẹ bằng cross-encoder model nhỏ (ví dụ ms-marco-MiniLM chạy qua Ollama) để chọn ra top-5 thực sự liên quan nhất đưa vào context window. Điều này cải thiện đáng kể chất lượng câu trả lời của Erumi.

CHƯƠNG 3 — NHỮNG GÌ CẦN NÂNG CẤP (Enhancement)

3.1 Hoàn thiện AI Tool Calling — Erumi Agent
Đây là tính năng quan trọng nhất, biến Erumi từ chatbot thành AI agent thực sự. Danh sách tools cần implement theo thứ tự ưu tiên:
Nhóm Task Management (ưu tiên cao nhất):

create_task: nhận title, description, assignee_id (optional), priority (Low/Medium/High/Critical), due_date (optional), project_id. Trả về task_id và confirmation message.
update_task_status: nhận task_id, new_status. Validate transition hợp lệ (không được nhảy từ Todo thẳng sang Done nếu có rule).
assign_task: nhận task_id, assignee_user_id. Check user có phải member của project không.
set_task_priority: nhận task_id, priority.
add_due_date: nhận task_id, due_date ISO 8601.

Nhóm Information Retrieval:

get_project_summary: trả về số task theo status, số task overdue, member active nhất, progress % tổng thể.
list_overdue_tasks: filter task quá hạn, có thể filter thêm theo assignee.
list_tasks_by_assignee: lấy task của một người cụ thể.
search_tasks: gọi Qdrant semantic search, trả về danh sách task liên quan.

Nhóm Reporting:

generate_excel_report: gọi service xuất file Excel với data của project, trả về download URL.
generate_word_report: tương tự nhưng Word format.
add_comment: thêm comment vào task, hữu ích khi AI muốn ghi chú kết quả phân tích vào task.

Pattern kỹ thuật: Mỗi tool function được đánh dấu [Description("...")] trên cả function lẫn từng parameter. Dùng AIFunctionFactory.Create() từ Microsoft.Extensions.AI để đăng ký. Phải có lớp permission check bắt buộc trong mỗi tool: lấy userId từ context, kiểm tra user có quyền thực hiện action trên projectId đó không trước khi execute. Tool phải trả về structured result gồm Success (bool), Message (string mô tả kết quả cho AI đọc để tạo response), và Data (optional, object chứa data thực tế).
System prompt cho Erumi cần được cập nhật để: nêu rõ Erumi là AI assistant của Qaly, liệt kê tools có sẵn và khi nào nên dùng, đặt rule không được tự động tạo/xóa task mà không confirm với user trước (trừ khi user ra lệnh rõ ràng), và format response bằng tiếng Việt.

3.2 Kanban Board — UX Nâng cấp
Tính năng cần thêm theo thứ tự ưu tiên:
Drag & Drop giữa các cột là ưu tiên số 1. Dùng thư viện @dnd-kit/core (React-friendly) hoặc vue-draggable-plus cho Vue.js 3. Khi drop card sang cột khác, gọi PATCH /api/tasks/{id}/status với body { "status": "InProgress" }. Khi sắp xếp lại trong cùng một cột, gọi PATCH /api/tasks/{id}/sort-order với body { "sortOrder": 2 }.
Quick edit inline: click vào title của card thì biến thành input field, Enter để save, Esc để cancel. Gọi PATCH /api/tasks/{id} với chỉ field thay đổi (partial update).
Card filtering & search: filter by assignee, priority, label, due date range. Search text trong title. Các filter state lưu vào URL query string để có thể share link.
Swimlanes: toggle để group card theo Assignee hoặc Priority thay vì chỉ theo Status.
Batch actions: checkbox trên mỗi card, khi chọn nhiều card thì hiện toolbar batch action phía trên (change status, change assignee, change priority, delete).
Keyboard shortcuts: N để tạo task mới trong column hiện tại, Enter để mở task, E để quick edit, Esc để đóng, / để focus search bar.

3.3 Module Time Tracking
Cho phép member ghi nhận thời gian thực tế làm việc trên từng task. Data này cực kỳ có giá trị để AI phân tích năng suất và đưa ra estimate chính xác hơn cho các task tương tự trong tương lai.
Domain model: Entity TimeEntry gồm Id, TaskId, UserId, StartedAt, EndedAt (nullable — null khi timer đang chạy), ManualMinutes (nullable — cho phép nhập tay), Note. ActualMinutes là computed property: ưu tiên ManualMinutes nếu có, không thì tính (EndedAt - StartedAt).TotalMinutes.
API endpoints: POST /api/tasks/{taskId}/time-entries để bắt đầu timer (tạo entry với StartedAt = now, EndedAt = null). PATCH /api/time-entries/{id}/stop để dừng timer. POST /api/tasks/{taskId}/time-entries/manual để nhập thời gian tay với body { "minutes": 90, "note": "..." }. GET /api/projects/{id}/time-report?from=...&to=... để lấy báo cáo tổng hợp.
Business rules: Mỗi user chỉ được có 1 timer đang chạy tại một thời điểm. Nếu bắt đầu timer mới thì tự động stop timer cũ. Không cho phép nhập time entry trong tương lai. Time entry có thể edit trong vòng 24 giờ sau khi tạo.
Tích hợp AI: Khi Erumi phân tích task hoặc project, đưa thêm totalLoggedHours vào context để AI có thể so sánh estimated vs actual, phát hiện task nào đang tiêu tốn thời gian bất thường.

3.4 Nâng cấp Notification System
Hiện tại SignalR realtime đang hoạt động tốt, nhưng cần mở rộng:
In-app notification center: panel hiện danh sách tất cả notification với phân loại (task assigned, comment added, due date reminder, AI suggestion). Hỗ trợ mark as read, mark all as read, delete.
Push notifications (trình duyệt): dùng Web Push API + Service Worker để gửi notification ngay cả khi tab không active hoặc trình duyệt minimize. Cần backend lưu PushSubscription của từng thiết bị/trình duyệt, dùng thư viện WebPush (có sẵn cho .NET) để gửi.
Email notifications (digest): không gửi email cho từng action nhỏ, mà gửi digest hàng ngày (7AM) tóm tắt: task sắp đến hạn trong 2 ngày tới, task được assign cho mình, mentions (@username trong comment). Dùng Hangfire hoặc .NET BackgroundService với timer để schedule job này.
Notification preferences: mỗi user có thể tắt/bật từng loại notification, chọn channel (in-app / push / email) cho từng loại.

CHƯƠNG 4 — NHỮNG GÌ CẦN CẢI THIỆN (Improvement)

4.1 Frontend — Giải quyết UX Hybrid Razor Pages + Vue.js
Vấn đề gốc rễ: Islands architecture (Razor Pages SSR + Vue.js islands) gây ra navigation không mượt vì mỗi lần chuyển trang là full page reload của Razor Pages, mất đi trải nghiệm SPA.
Hướng giải quyết theo 3 mức độ:
Mức 1 (ít breaking nhất): Thêm View Transitions API cho navigation giữa các Razor Pages để có animation mượt giữa các trang. Chỉ cần CSS và vài dòng JS, không thay đổi kiến trúc.
Mức 2 (khuyến nghị): Dùng HTMX cho các partial updates. Thay vì load lại cả trang khi submit form hay click action nhỏ, HTMX cho phép swap chỉ phần HTML cần thay đổi. Kết hợp với Razor Pages rất tự nhiên. Vue.js islands giữ nguyên cho các component phức tạp như Kanban board, AI chat.
Mức 3 (dài hạn, khi có resource): Migrate hoàn toàn sang Vue.js SPA với Inertia.js làm adapter giữa ASP.NET Core backend và Vue.js frontend. Inertia.js cho phép giữ nguyên routing và controller của ASP.NET Core nhưng render bằng Vue.js, không cần build API riêng. Navigation trở thành SPA-style không reload trang.

4.2 Testing Strategy
Hiện trạng: Có unit tests và integration tests nhưng coverage chưa được enforce.
Đề xuất target: Domain layer và Application layer tối thiểu 80% coverage. Infrastructure layer 60%. Web/API layer 70% cho các critical endpoints (auth, task CRUD, AI tools).
Test nào cần viết ngay:

Unit tests cho tất cả AI Tool functions (mock ITaskService, IReportService)
Unit tests cho domain logic: task status transition rules, permission checks, time entry validation
Integration tests với TestContainers: khởi chạy SQL Server và Redis trong Docker container thật, test EF Core queries và caching thực tế
API tests với WebApplicationFactory: test authentication flow, CRUD endpoints, rate limiting

Mutation testing: Sau khi coverage đạt target, chạy Stryker.NET để đảm bảo tests thực sự detect bugs, không chỉ execute code.

4.3 DevOps — CI/CD Nâng cấp
GitHub Actions pipeline cần bổ sung:
Bước code quality: tích hợp SonarCloud hoặc chạy dotnet format --verify-no-changes để enforce code style. Fail build nếu có violation.
Bước security scan: dùng dotnet list package --vulnerable để detect NuGet packages có CVE. Tích hợp Trivy để scan Docker image trước khi push.
Coverage gate: sau khi chạy tests, đọc file coverage report, fail build nếu line coverage thấp hơn threshold (70%). Dùng ReportGenerator để generate HTML report và upload lên GitHub Pages.
Staging environment: thêm workflow deploy lên staging server tự động sau khi merge vào develop branch. Chạy smoke tests sau deploy (ping /health endpoint, login flow, tạo một task test rồi xóa đi).

4.4 Error Handling & Observability
Global exception handler: Implement IExceptionHandler (ASP.NET Core 8+) để catch tất cả unhandled exceptions, log structured với Serilog kèm userId, requestPath, correlationId, trả về RFC 7807 Problem Details format thống nhất thay vì các format error khác nhau.
Correlation ID: Mỗi request sinh ra một X-Correlation-Id header, propagate qua tất cả logs trong request đó. Khi AI gọi tool và tool gọi service thì tất cả đều cùng correlationId. Client nhận correlationId trong response header để dùng khi report bug.
Distributed tracing: Tích hợp OpenTelemetry (OpenTelemetry.Instrumentation.AspNetCore, OpenTelemetry.Instrumentation.Http, OpenTelemetry.Instrumentation.SqlClient) để trace request từ Web layer → Application → Infrastructure → Database. Export sang Jaeger hoặc Zipkin (cả hai đều có Docker image nhẹ). Đặc biệt hữu ích để trace độ trễ của AI pipeline: bao nhiêu ms cho vector search, bao nhiêu ms cho Ollama inference.
Seq dashboard: Tận dụng Seq đang có, tạo các saved queries và dashboards cho: error rate theo thời gian, AI response latency p50/p95/p99, top 10 slowest API endpoints, failed login attempts.

CHƯƠNG 5 — HƯỚNG MỞ RỘNG (Expansion Roadmap)

5.1 Gantt Chart & Timeline View
Mô tả: Thêm góc nhìn Gantt bên cạnh Kanban, cho phép Project Manager thấy timeline, dependency giữa các task và critical path.
Kỹ thuật Frontend: Dùng thư viện dhtmlx-gantt (có bản Community miễn phí) hoặc frappe-gantt (MIT license, nhẹ hơn). Nếu muốn control hoàn toàn thì tự build bằng SVG + Vue.js — đủ khả thi vì Gantt về cơ bản là timeline bars.
API cần thêm: GET /api/projects/{id}/gantt trả về mảng tasks với startDate, endDate, progress (0–100), dependencies (mảng task_id cha), isCriticalPath (bool). PATCH /api/tasks/{id}/dates để cập nhật startDate/endDate khi kéo thả trên Gantt. POST /api/tasks/{id}/dependencies để thêm dependency. DELETE /api/tasks/{taskId}/dependencies/{dependencyId} để xóa.
Critical path calculation: Implement thuật toán CPM (Critical Path Method) trong Application layer: với mỗi task tính earliestStart, earliestFinish, latestStart, latestFinish, float = latestStart - earliestStart. Task có float = 0 nằm trên critical path — highlight đỏ trên Gantt.
AI integration: Erumi có thể cảnh báo "Task B phụ thuộc vào Task A nhưng Task A đang trễ 3 ngày, Gantt chart sẽ bị đẩy lùi. Bạn có muốn tôi reassign Task A để đảm bảo deadline không?"

5.2 Webhook System — Tích hợp ra ngoài
Mô tả: Cho phép Qaly gửi event notifications đến Slack, Microsoft Teams, hay bất kỳ URL nào khi có sự kiện xảy ra. Đây là nền tảng để tích hợp với hệ sinh thái tools của công ty.
Domain model: Entity WebhookSubscription gồm Id, ProjectId, Name (tên dễ nhớ như "Slack #dev-channel"), TargetUrl, Provider (enum: Slack/Teams/Custom), Events (JSON array các event name), Secret (dùng để ký HMAC-SHA256), IsActive, LastTriggeredAt, FailureCount.
Event catalog cần định nghĩa rõ: task.created, task.updated, task.completed, task.overdue, task.assigned, comment.added, project.member_added, project.completed.
Payload format chuẩn cho mọi event: { "event": "task.completed", "timestamp": "ISO8601", "project": { id, name }, "actor": { id, name, email }, "data": { ...event-specific fields }, "signature": "sha256=HMAC" }. Client verify signature bằng cách tính HMAC-SHA256(payload_body, webhook_secret) và so sánh với header X-Qaly-Signature.
Retry logic: Nếu target URL trả về non-2xx hoặc timeout, retry với exponential backoff: lần 1 sau 1 phút, lần 2 sau 5 phút, lần 3 sau 30 phút. Sau 3 lần thất bại liên tiếp thì tạm disable webhook và gửi email alert cho owner.
Slack integration template: Khi provider là Slack, tự động format payload thành Slack Block Kit message thay vì raw JSON, giúp message hiển thị đẹp trên Slack.
API: POST /api/projects/{id}/webhooks để đăng ký. GET /api/projects/{id}/webhooks để liệt kê. PATCH /api/webhooks/{id} để sửa. DELETE /api/webhooks/{id} để xóa. POST /api/webhooks/{id}/test để gửi test payload ngay lập tức. GET /api/webhooks/{id}/deliveries để xem lịch sử deliveries và status.

5.3 Public API + API Key Management
Mục tiêu: Mở REST API ra cho CI/CD pipelines, scripts nội bộ, hoặc các công cụ khác của công ty tích hợp với Qaly mà không cần đăng nhập bằng UI.
API Key entity: Id, UserId (key thuộc về user nào), Name (label dễ nhớ), KeyHash (BCrypt hash — tuyệt đối không lưu plaintext), Prefix (8 ký tự đầu để hiển thị trong UI, ví dụ qaly_sk_), Scopes (JSON array: tasks:read, tasks:write, projects:read, comments:write), ExpiresAt (nullable), LastUsedAt, CreatedAt, IsRevoked.
Key format: qaly_sk_<32-char-random> — prefix qaly_sk_ giúp detect key bị leak trong code repository (dùng GitHub secret scanning).
Authentication middleware: Khi request có header Authorization: Bearer qaly_sk_..., hệ thống tách prefix 8 ký tự để lookup key trong DB (tránh full table scan), sau đó BCrypt verify toàn bộ key, check scope, check expiry, update L  astUsedAt.
Rate limiting: 100 requests/phút per API key cho standard scope. Dùng ASP.NET Core Rate Limiter với Redis làm backing store để rate limit hoạt động đúng trên multi-instance deployment.
API versioning: Implement từ đầu bằng Asp.Versioning.Http. Mọi endpoint bắt đầu từ /api/v1/.... Khi có breaking change thì ra /api/v2/... và maintain v1 ít nhất 6 tháng với deprecation notice trong response header.
OpenAPI docs: Nâng cấp Swagger/Scalar hiện có để có: authentication example (Bearer token), request/response examples cho mọi endpoint, error response schemas, rate limit headers documentation.

5.4 Analytics Dashboard
Mô tả: Thêm trang Analytics cho Admin và Project Owner xem các metrics quan trọng.
Metrics cần có:
Project health: tỉ lệ task hoàn thành theo thời gian (burndown chart), velocity theo sprint/tuần (số task hoàn thành), cycle time trung bình (từ lúc tạo đến lúc Done), lead time (từ lúc Todo đến Done).
Team performance: số task hoàn thành theo người (bar chart), average response time (từ lúc task assigned đến lúc bắt đầu làm — InProgress), overtime rate (số task hoàn thành sau due date / tổng task).
AI usage: số lần Erumi được gọi, breakdown theo loại action (search/tool calling/chat), accuracy feedback (thumbs up/down cho AI responses).
Kỹ thuật: Không query trực tiếp từ bảng operational — tạo một bảng ProjectMetricsSnapshot được tính toán sẵn mỗi giờ bởi background job. Dashboard query từ snapshot này để không ảnh hưởng performance của hệ thống chính. Frontend dùng Chart.js hoặc ApexCharts trong Vue.js component.
AI-generated insights: Erumi đọc metrics và tự động generate text insights: "Sprint này team hoàn thành 23 tasks, tăng 15% so với sprint trước. Tuy nhiên 4 tasks bị overdue đều thuộc về module Payment — đây là bottleneck cần chú ý."

5.5 Mobile Experience — Progressive Web App (PWA)
Mô tả: Không cần build native app ngay, PWA cho phép user cài Qaly lên màn hình điện thoại và dùng như app thật, với offline capability cho một số tính năng cơ bản.
Cần làm: Tạo manifest.json với name, icons (192x192 và 512x512), theme color, display mode standalone. Implement Service Worker: cache static assets (JS, CSS, fonts) với Cache First strategy. Cache API responses /api/projects và /api/tasks với Network First strategy (thử network trước, nếu offline thì serve cache). Offline indicator: khi mất mạng, hiện banner thông báo và disable các actions cần network như tạo task mới.
Push notifications (Web Push): Khi user cho phép notification, backend lưu PushSubscription object. Dùng thư viện WebPush (.NET) để gửi push notification qua browser's push service ngay cả khi user không đang mở tab Qaly. Hữu ích cho: task được assign, đến hạn task, có mention trong comment.

5.6 Multi-tenant Architecture (Long-term)
Mô tả: Nếu Qaly muốn mở rộng ra nhiều công ty khác sử dụng (SaaS), cần thiết kế multi-tenant. Với Modular Monolith hiện tại, có 2 hướng:
Hướng 1 — Database per tenant (mạnh về isolation): mỗi công ty có database riêng trên cùng SQL Server instance. Khi request đến, middleware đọc tenant_id từ subdomain (ví dụ companya.qaly.io) hoặc JWT claim, resolve đúng connection string. Hoàn toàn isolated về data, dễ backup/restore per tenant. Nhược điểm: khó run cross-tenant reports.
Hướng 2 — Row-level security (đơn giản hơn để migrate): thêm TenantId column vào tất cả bảng, thêm global query filter trong EF Core modelBuilder.Entity<TaskItem>().HasQueryFilter(t => t.TenantId == _currentTenant.Id). Dữ liệu chung một database nhưng tự động filter. Nhược điểm: rủi ro data leak nếu filter bị bypass, performance kém hơn ở scale lớn.
Khuyến nghị: Nếu chỉ dùng nội bộ 1 công ty thì chưa cần. Nếu muốn mở rộng SaaS trong 12 tháng tới thì nên plan ngay từ bây giờ và chọn Hướng 1 vì isolation tốt hơn.

CHƯƠNG 6 — TECH PROPOSALS & API DESIGN TỔNG HỢP

6.1 API Endpoints cần thêm (chưa có trong hệ thống hiện tại)
Auth & Session:

DELETE /api/auth/sessions — revoke tất cả sessions của current user
GET /api/auth/sessions — liệt kê active sessions (device, IP, last seen)
POST /api/auth/api-keys — tạo API key mới
GET /api/auth/api-keys — liệt kê API keys của user
DELETE /api/auth/api-keys/{id} — revoke API key

Tasks (bổ sung):

PATCH /api/tasks/{id}/status — cập nhật status (dùng cho drag & drop Kanban)
PATCH /api/tasks/{id}/sort-order — sắp xếp trong cột
POST /api/tasks/{id}/time-entries — bắt đầu timer
PATCH /api/time-entries/{id}/stop — dừng timer
GET /api/projects/{id}/gantt — dữ liệu Gantt chart
POST /api/tasks/{id}/dependencies — thêm task dependency
GET /api/projects/{id}/tasks/overdue — danh sách task quá hạn

AI (bổ sung):

POST /api/ai/chat/stream — SSE/WebSocket endpoint cho streaming (alternative cho SignalR nếu cần)
POST /api/ai/feedback — user feedback cho AI response (thumbs up/down + comment)
GET /api/projects/{id}/ai/suggestions — proactive suggestions của AI cho project

Analytics:

GET /api/projects/{id}/analytics?from=...&to=... — metrics dashboard
GET /api/projects/{id}/burndown — burndown chart data
GET /api/users/{id}/activity — activity report của một user

Webhooks:

POST /api/projects/{id}/webhooks
GET /api/projects/{id}/webhooks
PATCH /api/webhooks/{id}
DELETE /api/webhooks/{id}
POST /api/webhooks/{id}/test
GET /api/webhooks/{id}/deliveries


6.2 Tech Stack Bổ sung Đề xuất
Các thư viện/công nghệ nên thêm vào project:
Backend: Hangfire hoặc Quartz.NET cho scheduled jobs (daily digest email, metrics snapshot, overdue task detection). FluentValidation nếu chưa có, để validation logic tách khỏi controller. Polly cho resilience khi gọi Ollama hoặc external webhooks (retry, circuit breaker, timeout). OpenTelemetry (.NET SDK) cho distributed tracing. Stryker.NET cho mutation testing.
Frontend: @vueuse/core — collection utilities cho Vue.js (useLocalStorage, useDebounce, useIntersectionObserver, rất hữu ích). vue-draggable-plus cho drag & drop Kanban. Chart.js hoặc ApexCharts cho Analytics dashboard. vite-plugin-pwa cho PWA support.
Infrastructure: Jaeger hoặc Zipkin (Docker container) nhận telemetry từ OpenTelemetry. Mailpit (Docker) cho development email testing thay vì gửi email thật.

CHƯƠNG 7 — TIMELINE & THỨ TỰ ƯU TIÊN

Sprint 1 (1–2 tuần) — Critical Security Fixes
Làm ngay, không thể trì hoãn: Metadata Filtering bắt buộc trong Qdrant search (mục 1.1), implement Outbox Pattern cho vector sync (mục 1.2), nâng cấp session security với Redis (mục 1.3). Viết integration tests cho 3 mục này trước khi merge.
Sprint 2 (2–3 tuần) — Core Performance
AI Streaming response qua SignalR (mục 2.1), thêm database indexes (mục 2.2 — chỉ cần viết migration, rất nhanh), caching strategy cho Kanban queries (mục 2.3), RAG pipeline improvements: hybrid search + re-ranking (mục 2.4).
Sprint 3 (3–4 tuần) — AI Agent & UX
Hoàn thiện Tool Calling cho Erumi — implement đủ 8 tools nhóm Task Management và Reporting (mục 3.1), Kanban drag & drop + quick edit (mục 3.2), global error handler + correlation ID + OpenTelemetry setup (mục 4.4).
Sprint 4 (3–4 tuần) — New Features
Time Tracking module (mục 3.3), nâng cấp Notification system với push notifications và email digest (mục 3.4), CI/CD improvements: security scan + coverage gate + staging deploy (mục 4.3).
Sprint 5–6 (4–6 tuần) — Expansion
Gantt chart view (mục 5.1), Webhook system (mục 5.2), Public API + API Key management (mục 5.3), Analytics dashboard (mục 5.4).
Long-term (3–6 tháng) — Scale & Platform
PWA mobile experience (mục 5.5), multi-tenant architecture nếu có kế hoạch mở rộng SaaS (mục 5.6), migrate frontend từ Razor Pages hybrid sang Vue.js SPA với Inertia.js (mục 4.1 mức 3).

TÓM TẮT ĐIỂM MẤU CHỐT
Qaly có nền tảng kỹ thuật tốt — Clean Architecture, AI-native design, Docker-ready. Ba việc phải làm ngay trước khi production là: bắt buộc metadata filter trong Qdrant (rủi ro data leak), implement Outbox Pattern cho vector sync (rủi ro data stale), và AI streaming response (UX). Tính năng tạo ra giá trị cao nhất là hoàn thiện Tool Calling để Erumi trở thành agent thực sự — đây là điểm khác biệt lớn nhất so với mọi PM tool khác trên thị trường. Hướng mở rộng tự nhiên nhất là Gantt view + Webhook system + Public API, biến Qaly từ standalone tool thành hub tích hợp trung tâm trong workflow của team.