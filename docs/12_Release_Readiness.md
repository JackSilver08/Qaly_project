# Release Readiness

> Cập nhật: 03/09/2026. Bảng chỉ ghi bằng chứng fresh của working tree hiện tại; runtime chưa chạy không được kế thừa PASS cũ.

## Trạng thái tự động

| Gate | Kết quả local |
| --- | --- |
| Frontend unit tests | 250/250 pass |
| Backend Unit tests | 801/801 pass |
| Integration tests | 304/304 pass |
| WebFeature tests | 49/49 pass |
| Coverage | CI gate giữ baseline Unit 43% / Integration 16%; chưa chạy lại coverage trong local batch này |
| Playwright E2E smoke | Demo targeted replay đã có evidence riêng; production browser matrix chưa chạy lại |
| Frontend typecheck | Pass |
| Frontend production build | Pass |
| Docker Compose validation | Pass |
| Configuration safety check | Pass |
| Secret and PII log hygiene check | Pass |
| Threat model | Source-reviewed; independent/deployed-target sign-off pending |
| OpenTelemetry export contract | Optional OTLP request/HTTP + AI durable-progress/answer/terminal metrics and sampled traces; validator 22/22, registration 2/2, bounded-tag Unit 1/1 and durable-turn Integration 1/1 pass; collector/dashboard/alert runtime pending |
| EF query safety baseline | Mọi runtime `Skip/Take` dùng stable ordering có khóa `Id`; các danh sách giới hạn quan trọng có deterministic tie-breaker; SQL Server runtime và design-time dùng `SplitQuery`; Task list/detail/Kanban bỏ kiểm quyền lặp theo từng dòng sau canonical visibility filter; focused query/service Unit 130/130, Integration 24/24, split-query registration 1/1 và Task visibility 6/6 pass |
| HTTP authentication boundary | Reflection gate quét mọi concrete controller action: action phải có `Authorize` hoặc `AllowAnonymous`; danh sách anonymous được khóa đúng login, register, CSRF bootstrap và signed GitHub webhook; 2/2 pass |
| Correlation/log hygiene | Chỉ nhận đúng một client correlation id an toàn, tối đa 64 ký tự; giá trị rỗng, nhiều header, quá dài hoặc chứa ký tự không an toàn được thay bằng server id; response, trace identifier và request log dùng cùng canonical value; 4/4 pass |
| Notification authorization/read-back | Danh sách tiếp tục quét qua các notification mới nhưng không được phép xem; unread count đếm đủ toàn bộ bản ghi nhìn thấy bằng batch resolver; custom Project manager role khớp Task visibility canonical; focused 20/20 pass |
| Recent audit activity boundary | Stable batched paging không để hàng trăm log tenant khác che mất hoạt động hợp lệ cũ hơn; Task/Sprint/Project target và Project access được resolve theo batch, malformed target metadata fail closed; audit staging chỉ hiện sau shared UnitOfWork commit; focused 3/3 pass |
| Canonical graph + audit atomicity | Organization/Project/Group/Task và các graph nghiệp vụ chính stage audit trước cùng một UnitOfWork commit; audit staging lỗi không để lại aggregate/outbox nửa vời; fault-injection Unit 4/4, staging Integration 1/1 pass |
| Load/shutdown/claim source gate | Load probe PowerShell 7 có warmup, auth fail-closed, p50/p95/p99, throughput/error threshold và JSON/Markdown không chứa credential; host shutdown budget 5–300 giây. AI/Privacy có exclusive renewable lease, stale-writer guard, bounded shutdown release, real BatchSize scheduling và readiness queue checks; Privacy ưu tiên DSAR theo deadline trước retention và product API/UI hiện cùng overdue/expired-lease signal. Task attention quét hết mọi batch và đóng stale signal; trash cleanup giữ metadata khi storage delete lỗi để retry. GitHub inbox, outbound webhook và Vector outbox có claim/lease recovery khi host dừng; GitHub/Vector giữ stale-owner guard và Vector giữ total order theo aggregate. Email digest không ghi delivered khi SMTP/provider lỗi, retry provider timeout và thu hồi subscription của user inactive. Project monitor quan sát cả Project vừa soft-delete, cô lập/defer poison row và không làm dừng cả batch. Full client 250/250, Unit 821/821, Integration 331/331, WebFeature 49/49, build 0 warning/error. Target load/soak/fleet SIGTERM vẫn pending |
| Vector synchronization contract | Interceptor chỉ phát canonical event khi semantic mode bật; P026 chuẩn hóa/requeue event legacy, backfill aggregate/sequence và dead-letter contract không nhận diện; SQL chứng minh exclusive claim, per-aggregate ordering, lease takeover, stale-owner rejection, retry/dead-letter và migration P025→P026. Qdrant live write/read-back vẫn external-deferred |
| NuGet vulnerability scan | Pass, including transitive packages |
| npm production dependency audit | Pass, 0 vulnerabilities |
| Solution Release build | Pass, 0 warning / 0 error |
| Production container source contract | CI đã kiểm tra non-root + SQL/Redis + `/health/ready` + Docker health; host hiện tại không có Docker daemon nên runtime run còn pending |
| SQL migration/clean restore rehearsal | Pass local: 57 migration, backup/verify/restore, `DBCC CHECKDB`, 95-table smoke và cleanup |
| Local Ollama provider smoke | Không chạy lại trong production-hardening batch này |
| Target release/rollback rehearsal | Pending target environment + secret store |

## Gate bắt buộc trước merge

- Vue typecheck và production build.
- Bundle trong `src/Qaly.Web/wwwroot/dist` phải khớp source.
- .NET Release build, unit tests và coverage baseline.
- Integration tests và coverage baseline.
- Playwright E2E smoke.
- Configuration safety và Docker Compose validation.
- Secret/PII log hygiene và threat-model delta review.
- Telemetry configuration validation; nếu bật OTLP thì collector endpoint/sampling phải hợp lệ và credential chỉ đến từ secret store.
- NuGet transitive scan và npm production dependency audit.
- Production Docker image build/runtime contract trên SQL Server + Redis.

Coverage loại generated source và EF migrations qua `coverlet.runsettings`; code nghiệp vụ và DTO vẫn nằm trong phép đo.

## Phạm vi đã có regression

- Meeting LiveKit token/fallback.
- Meeting realtime join/leave, reconnect và disconnect cleanup.
- Import DOCX/ZIP và ZIP lỗi.
- Auth boundary, project permissions, wiki visibility và meeting action items.
- Anonymous login/register throttling, safe health responses và response security headers.
- Reverse-proxy topology fail-closed: chỉ tin `X-Forwarded-For/Proto` từ IP/hop được khai báo; mode direct không nhận trust list.

## Việc cần xác nhận thủ công

- Hai browser thật hiển thị participant meeting đúng sau reconnect.
- Screen share trên browser hỗ trợ và thông báo trên browser không hỗ trợ.
- AI cloud provider bằng credential của môi trường demo/production. Ollama local đã được xác minh.
- Backup/restore database trên hạ tầng triển khai thật.

## Giới hạn phát hành hiện tại

CD chỉ publish exact-SHA production image lên GHCR sau CI main thành công, chưa deploy môi trường. Local SQL migration/restore rehearsal đã có evidence; container runtime contract phải chạy trên CI/host có Docker. Chưa thể đánh dấu production-ready hoàn chỉnh cho đến khi có:

1. Target staging/production.
2. Secret store của môi trường.
3. Health check và rollback được chạy trên target thật.
4. Independent security/license sign-off và các external adapter được bật có canonical receipt.
5. Collector, dashboard, alert route và retention/cost policy có target-runtime receipt; AI SLI đã emit ở source nhưng chưa có target baseline/alert receipt.
6. Representative authenticated load profile, soak và multi-instance graceful-shutdown/lease takeover có artifact từ release candidate thật; source harness không thay target evidence.
