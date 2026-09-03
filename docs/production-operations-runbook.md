# Qaly production operations runbook

> Trạng thái bằng chứng: source + automated local gates có thể PASS; deployment, alert route và external adapter chỉ PASS sau runtime read-back từ môi trường đích.

## 1. Health contract

| Endpoint | Mục đích | Check | Cách dùng |
|---|---|---|---|
| `/health/live` | Tiến trình còn phục vụ | `self` | Docker/Kubernetes liveness; chỉ restart khi tiến trình thật sự hỏng |
| `/health/ready` | Instance có thể nhận traffic | `self`, SQL Server, Redis, vector/webhook outbox, AI job queue, Privacy work queue | readiness/load balancer/release validation |
| `/health` | Tương thích monitor cũ | giống `/health/ready` | chuyển monitor cũ dần sang endpoint rõ nghĩa |

Health response là anonymous nhưng được redact: chỉ có `status`, `deploymentId`, `version`, thời lượng tổng và tên/status/thời lượng từng check. Không thêm exception, connection string, provider response, payload hoặc secret vào response này.

Trước release phải chạy `scripts/check-security-hygiene.ps1`. Gate này chặn private-key/token pattern có độ tin cậy cao và placeholder PII/secret trong log template; nó không thay repository-host secret scanning hoặc việc kiểm tra mẫu log thật. Threat model và residual risk nằm tại `docs/security-threat-model.md`.

Baseline response security gồm `X-Content-Type-Options: nosniff`, deny framing, strict referrer, Permissions Policy và CSP tối thiểu cho `base-uri`, `frame-ancestors`, `object-src`; Kestrel tắt server banner. Không mở rộng CSP bằng wildcard để làm test xanh. Target CSP cho script/style/connect phải được dựng từ endpoint LiveKit/provider thực và xác minh bằng browser/DAST.

Login mặc định giới hạn 30 request/phút/IP; đăng ký 10 request/giờ/IP. Vượt ngưỡng trả `429`, `Retry-After` và body không lộ tài khoản có tồn tại hay không. Đây là application safety net, không thay cho edge/WAF rate limiting hoặc credential-stuffing monitoring.

Khi chạy sau load balancer/reverse proxy, đặt `ReverseProxy:Mode=trusted-proxy`, liệt kê IP proxy cụ thể và `ForwardLimit` đúng topology. Forwarded headers được xử lý trước HTTPS/auth/limiter. Mode `direct` từ chối danh sách proxy; mode trusted thiếu/sai/trùng IP làm Production fail startup, tránh spoof `X-Forwarded-For` hoặc gom toàn bộ người dùng vào một limiter partition.

`webhook_outbox` chuyển `degraded` khi có dead-letter, pending đạt threshold hoặc bản pending già hơn giới hạn. Khi semantic search được bật, `vector_outbox` chuyển `degraded` nếu có dead-letter, pending vượt `OperationalHealth:VectorOutboxPendingThreshold` hoặc row pending cũ nhất vượt `OperationalHealth:VectorOutboxMaxPendingAgeMinutes`; dead-letter không bị tính lẫn vào pending. `ai_job_queue` quan sát worker-enabled, backlog/tuổi pending và lease hết hạn. `privacy_work_queue` quan sát thêm failed work và DSAR quá deadline. Các queue được cấu hình disabled được báo `healthy/disabled` và không coi row dormant là backlog; lỗi truy vấn dependency mới là `unhealthy`.

## 2. Telemetry và SLO ban đầu

Qaly có exporter OpenTelemetry OTLP tùy chọn cho ASP.NET Core request, outbound HTTP metrics và sampled distributed traces. Mặc định exporter tắt; ứng dụng không mở endpoint `/metrics`. Health probes không tạo trace nhưng vẫn có thể dùng làm availability signal riêng ở load balancer/monitor.

Production bật bằng:

```text
Telemetry__OtlpEnabled=true
Telemetry__OtlpEndpoint=https://otel-collector.example:4317
Telemetry__ServiceName=qaly-web
Telemetry__TraceSampleRatio=0.1
```

Endpoint HTTP plaintext bị từ chối trừ khi operator đặt rõ `Telemetry__AllowInsecureOtlp=true`. Credential/header của collector phải đi qua `OTEL_EXPORTER_OTLP_HEADERS` trong secret store và không được ghi vào file hay ticket.

SLO khởi điểm để dựng dashboard trên target (chưa phải runtime PASS):

| Signal | Cửa sổ/target ban đầu | Nguồn |
|---|---|---|
| API availability | 30 ngày, `>= 99.5%`; loại health probe, expected 4xx và maintenance đã phê duyệt | ASP.NET Core request metrics |
| API server error | 5 phút cảnh báo khi 5xx `> 1%`; 30 ngày mục tiêu `< 0.5%` | status-code request metrics |
| API latency | p95 `< 1 s`, p99 `< 2.5 s` cho route không streaming/AI | request duration theo route |
| Dependency latency/error | cảnh báo khi outbound HTTP p95 hoặc error rate lệch baseline 15 phút | HTTP client metrics/traces |
| Canonical dependency health | readiness fail lập tức; outbox degraded theo threshold cấu hình | `/health/ready` và check detail đã redact |
| AI first durable progress | p95 `< 2 s`, tách theo capability | `qaly.ai.assistant.first_progress.duration` |
| AI first durable answer | p95 `< 30 s`, tách theo capability/provider/fallback | `qaly.ai.assistant.first_answer.duration` |
| AI terminal completion | 30 ngày `>= 98%`, tách completed/failed/canceled/state-changed | `qaly.ai.assistant.turns` và `qaly.ai.assistant.turn.duration` |

Dashboard phải group theo `service.name`, deployment/version, route template, method và status; AI view chỉ dùng capability/outcome/provider/fallback/project-scoped có cardinality hữu hạn. Không dùng raw query string, entity/user/project ID, request/response body, email, token, prompt hoặc AI payload làm attribute. First progress chỉ ghi sau khi process event đầu tiên đã lưu; first answer/completion chỉ ghi sau terminal response canonical đã lưu. Các target trên vẫn phải được hiệu chỉnh bằng load profile thật, alert phải route tới operator và incident drill phải có receipt trước khi đóng `POST-OPS-002`.

### Load smoke có bằng chứng

`scripts/load-smoke.ps1` chạy bằng PowerShell 7, chỉ gửi `GET`, có warmup riêng và đo p50/p95/p99, throughput, status-code, failure kind. Script fail khi vượt failure budget hoặc latency threshold; mỗi run ghi một JSON máy đọc được và một Markdown tóm tắt vào `docs/task/qa-evidence/performance` (hoặc thư mục được chỉ định). Credential không xuất hiện trong console hay artifact.

Readiness rehearsal không cần đăng nhập:

```powershell
.\scripts\load-smoke.ps1 `
  -BaseUrl https://qaly.example `
  -Path /health/ready `
  -Scenario readiness `
  -Requests 500 `
  -Concurrency 20
```

API nghiệp vụ phải dùng một principal test đúng role và truyền secret qua môi trường, không ghi vào command/history hay file. Có thể đặt một trong `QALY_LOAD_BEARER_TOKEN`, `QALY_LOAD_API_KEY`, `QALY_LOAD_COOKIE`; `-RequireAuthentication` fail-closed nếu không có credential:

```powershell
$env:QALY_LOAD_API_KEY = '<secret-from-approved-store>'
.\scripts\load-smoke.ps1 `
  -BaseUrl https://qaly.example `
  -Path /api/dashboard `
  -Scenario member-dashboard `
  -RequireAuthentication `
  -Requests 1000 `
  -Concurrency 25 `
  -MaxFailureRatePercent 0.5 `
  -MaxP95Milliseconds 1000 `
  -MaxP99Milliseconds 2500
Remove-Item Env:QALY_LOAD_API_KEY
```

Mỗi role/surface trọng yếu cần một artifact riêng; health-only không chứng minh API nghiệp vụ. Script chặn HTTP plaintext ngoài loopback trừ khi operator chủ động xác nhận `-AllowInsecureHttp`. Run chỉ là target-runtime evidence khi origin là release candidate thật và `sourceCommit` khớp deployment. Traffic mix, soak dài, nhiều instance và autoscaling vẫn cần orchestration ngoài script này.

### Graceful shutdown

Host dùng `Hosting:ShutdownTimeoutSeconds` (mặc định 30 giây; Production chỉ chấp nhận 5–300). Background worker phải truyền stopping token qua query, provider và delay; cancellation do host không được ghi thành provider/worker failure. AI, Privacy, GitHub, Vector và outbound webhook chủ động trả claim về trạng thái retryable qua context tách biệt có timeout hai giây; nếu bước release bất khả dụng thì lease-expiry recovery vẫn là đường an toàn cuối. Processor AI/Privacy kiểm lại owner và lease chưa hết hạn ngay trước commit để worker cũ không thể ghi stale result. GitHub inbox dùng owner + lease expiry, heartbeat và exponential backoff: conditional update chỉ cho một instance claim; processor và terminal state commit cùng context; owner cũ bị concurrency token chặn ghi; lease cuối hết hạn được terminalize thay vì mắc `Processing`.

AI/Privacy Production bắt buộc poll 100–60000 ms, batch 1–100, max-attempt 1–20, lease 30–900 giây, heartbeat 5–300 giây và nhỏ hơn lease, base retry 1–300 giây. `BatchSize` là số work item tối đa xử lý liên tiếp trong một scheduling pass; không phải mức song song. Privacy ưu tiên DSAR đã accepted trước retention maintenance, rồi sắp DSAR theo deadline hợp lệ gần nhất để backlog retention không làm đói yêu cầu pháp lý.

Màn Privacy dùng cùng tín hiệu canonical với readiness: worker tắt, failed work, DSAR quá deadline và lease hết hạn phải hiện rõ cho operator; không được rút gọn thành `healthy`. Task-attention scan chạy theo batch ổn định cho đến hết tập Task mở và đóng signal khi Task đã đóng/xóa hoặc người nhận không còn được giao. Project trash cleanup chỉ xóa metadata sau khi xóa file vật lý thành công; lỗi storage giữ nguyên Project/file metadata để lượt sau retry, không tạo orphan không còn dấu vết. Email digest chỉ đánh dấu delivered sau khi transport hoàn tất; SMTP/provider timeout phải trở thành retry và user inactive hoặc mất quyền Project phải bị vô hiệu subscription. Project operation monitor phải đọc cả Project vừa soft-delete, cô lập/defer từng execution lỗi và tiếp tục batch; xung đột concurrency giữ state mới hơn.

Khi bật GitHub, Production bắt buộc `LeaseSeconds` 30–900, `HeartbeatSeconds` 5–300 và nhỏ hơn lease, `BaseRetrySeconds` 1–300, `BatchSize` 1–100, `MaxAttempts` 1–20. Không giảm lease thấp hơn thời gian xử lý tối đa quan sát được nếu chưa điều chỉnh heartbeat. SQL concurrency gate phải chứng minh hai instance chỉ một bên nhận cùng delivery, lease hết hạn được takeover và owner cũ không thể commit side effect stale.

Vector sync dùng cùng nguyên tắc ownership nhưng thêm total order `SequenceNumber` và khóa predecessor theo `AggregateType/AggregateId`: event sau của cùng aggregate không được vượt event trước, còn aggregate độc lập vẫn có thể tiến triển. Production chỉ chấp nhận `VectorSync:BatchSize` 1–100, `MaxAttempts` 1–20, `LeaseSeconds` 30–900, `HeartbeatSeconds` 5–300 và nhỏ hơn lease, `BaseRetrySeconds` 1–300. Worker claim bằng conditional update, renew lease bằng context riêng, truyền cancellation tới embedding/vector provider, release lease khi host dừng, dùng exponential backoff và dead-letter terminal ở lần cuối. Owner cũ không được complete/retry sau khi lease đã đổi chủ.

Migration P026 chuẩn hóa event legacy `TaskItem*`/`TaskComment*`, khôi phục các row từng bị worker cũ đánh dấu processed sai, backfill aggregate/order và dead-letter contract không nhận diện được thay vì báo thành công giả. Sau rollout, theo dõi `vector_outbox`; dead-letter legacy phải được điều tra và re-ingest theo aggregate canonical, không sửa `ProcessedAt` thủ công.

Trước khi đóng fleet gate, gửi SIGTERM cho từng instance release candidate khi đang có AI job, privacy work, GitHub inbox và webhook/vector outbox; xác nhận process dừng trong budget, không có false failure, lease/claim được instance khác nhận lại, không duplicate external effect, và readiness rời load balancer trước khi process kết thúc. Source/unit evidence không thay rehearsal này.

## 3. Webhook operator flow

1. Mở Project → Cài đặt → Webhook bằng tài khoản có `CanManageWebhooks`.
2. Đọc counters pending/dead-letter và danh sách outbox gần nhất.
3. Với dead-letter, kiểm tra event type, occurrence id, retry count, thời điểm và error summary đã redact.
4. Chỉ bấm replay sau khi endpoint/credential đã được sửa. Backend reset lease/retry/dead-letter trên chính canonical outbox row và ghi audit `ReplayWebhookDeadLetter`.
5. Refresh/read-back. Lần replay lặp trên row đã queued trả `ReplayQueued=false`, không ghi audit/mutation thứ hai.
6. Xác nhận delivery log mới; UI/API không được lộ request payload, response body hay secret.

Retention mặc định:

- delivery log: 30 ngày;
- processed outbox: 30 ngày;
- dead-letter: giữ lại cho đến khi operator replay hoặc quy trình sự cố xử lý rõ ràng.

Worker chạy mỗi 24 giờ và bỏ qua product visibility filters để log của webhook/Project đã soft-delete vẫn được dọn. Production validator chỉ chấp nhận retention 7–365 ngày.

## 4. Migration, backup và restore

Chạy từ repository root trên SQL Server disposable hoặc môi trường rehearsal được cho phép:

```powershell
.\scripts\rehearse-sqlserver-migrations.ps1
```

Acceptance evidence phải có:

- `dotnet ef database update` chạy hết migration chain;
- số migration và latest migration;
- backup SHA-256 và `RESTORE VERIFYONLY`;
- clean restore vào database mới;
- `DBCC CHECKDB` và schema smoke;
- source/restore rehearsal database được dọn;
- JSON + Markdown trong `docs/task/qa-evidence/recovery`.

Không dùng database production hoặc tên tùy ý. Script fail-closed nếu tên không thuộc namespace rehearsal.

## 5. Container/release contract

CI production-container gate phải chứng minh:

- production image build được và khai báo non-root user;
- Production configuration validator chấp nhận profile cụ thể;
- SQL Server + Redis thật reachable;
- `/health/ready` trả success và lưu artifact;
- Docker health state thành `healthy`;
- cleanup/log luôn chạy kể cả khi gate fail.

CD chỉ publish image mang exact commit SHA sau CI main thành công, kèm OCI revision/source, provenance và SBOM. Việc publish không phải deployment. Deployment môi trường chỉ được đóng khi có secret injection, TLS/host thật, readiness, migration record và rollback replay.

## 6. Incident minimum

- Readiness unhealthy: lấy correlation/deployment id, kiểm tra dependency tương ứng, không in secret vào ticket/log.
- AI/Privacy queue degraded: kiểm tra worker flag, pending age, expired lease và retry/failure; với Privacy xử lý DSAR overdue trước retention. Không sửa owner/status trực tiếp trong database hoặc coi host shutdown có kiểm soát là provider failure.
- Webhook degraded: dừng replay hàng loạt, sửa endpoint/credential, replay từng occurrence và canonical read-back.
- Vector outbox degraded: kiểm tra pending/dead-letter/tuổi row cũ nhất; xác minh Qdrant/embedding provider, lease owner và predecessor của aggregate. Chỉ re-ingest từ entity canonical sau khi dependency ổn định; không bỏ qua event trước hoặc đánh dấu processed bằng tay.
- Migration failure: không xóa database thật; giữ evidence, forward-fix migration hoặc rollback image theo kế hoạch đã phê duyệt.
- Secret lộ: revoke/rotate tại provider trước, cập nhật secret store, redeploy rồi audit access.
- Collector/OTLP lỗi: ứng dụng vẫn phục vụ; kiểm tra collector/network/credential, giảm sampling nếu cần và không bật log body để điều tra nhanh. Xác nhận telemetry quay lại bằng deployment id và một request kiểm soát.

## 7. Chưa được coi là production evidence

- code review, test double, toast hoặc HTTP 2xx đơn lẻ;
- Docker workflow chưa có một run thành công trên commit đích;
- local LocalDB thay cho target SQL Server version;
- alert chỉ tồn tại trong health JSON mà chưa route tới operator;
- OTLP chỉ build/validate được nhưng chưa có collector, dashboard, alert receipt và retention/cost policy trên target;
- external webhook/GitHub/SMTP/WebPush/LiveKit/Qdrant chưa có write/read-back thật.
