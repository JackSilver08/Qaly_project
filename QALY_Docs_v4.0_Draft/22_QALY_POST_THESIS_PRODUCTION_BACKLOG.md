# Qaly V4 — Post-thesis Production Backlog

> **Status:** `DEFERRED_POST_THESIS`
> **Purpose:** giữ lại các yêu cầu cần cho vận hành thương mại nhưng không chặn mốc `DEMO_READY_THESIS`. Mọi mục trong file này vẫn là việc thật chưa hoàn tất; không được đổi thành PASS chỉ vì demo dùng adapter giả lập, local seed hoặc toast thành công.

## 1. Ranh giới hoãn

Một hạng mục chỉ được chuyển vào backlog này khi thỏa cả ba điều kiện:

1. không ảnh hưởng tính đúng đắn, bảo mật, phân quyền hoặc khả năng hoàn tất workflow trong kịch bản demo;
2. sản phẩm mô tả trung thực trạng thái disabled/degraded/external-deferred, không giả lập thành công;
3. đã có tiêu chí nghiệm thu production cụ thể để thực hiện sau báo cáo.

Tenant leak, private-data leak, wrong role/scope, fake success, duplicate mutation, stale write, dữ liệu demo không tái lập hoặc dead-end trong runbook **không được phép hoãn**.

## 2. Backlog production-only

| ID | Hạng mục gác lại | Vì sao không chặn demo | Bằng chứng cần trước production | Ưu tiên sau báo cáo |
|---|---|---|---|---|
| POST-EXT-001 | Live GitHub adapter | Demo có thể chứng minh capability và trạng thái chưa cấu hình một cách trung thực | OAuth/app credential thật, write/read-back canonical, retry/idempotency, revoke và audit | P1 |
| POST-EXT-002 | SMTP/invitation delivery thật | Demo dùng local/controlled adapter và không tuyên bố email đã đến | provider credential, delivery/bounce receipt, retry, suppression và audit | P1 |
| POST-EXT-003 | WebPush/VAPID | Push không thuộc câu chuyện nghiệp vụ bắt buộc của demo | VAPID secret thật, browser subscription, delivery receipt/failure, revoke và fallback | P2 |
| POST-EXT-004 | LiveKit production meeting infrastructure | Demo có thể chạy degraded/local UI contract nếu ghi rõ ranh giới | production token/room lifecycle, reconnect, multi-user load, recording/transcript retention và incident recovery | P1 |
| POST-EXT-005 | Qdrant/vector retrieval production | Demo chỉ cần authorized source và fallback đúng; không được gắn nhãn live khi chưa cấu hình | indexed corpus thật, tenant isolation, freshness/delete propagation, retrieval quality và failure fallback | P1 |
| POST-EXT-006 | External webhook endpoint acceptance | Durable local outbox, retry và receipt đã có focused evidence | endpoint thật, signed request, write/read-back, dead-letter replay, alert và occurrence idempotency | P1 |
| POST-DEPLOY-001 | Secret-injected deployment rehearsal | Local thesis profile là đủ cho demo | clean environment deploy, secret rotation, exact hosts/TLS, health/readiness, rollback và deployment record | P0 production |
| POST-DATA-001 | Restored production-like SQL migration rehearsal | Demo database có thể reset/seed; schema correctness vẫn được build/test | backup restore, full migration chain, data cleanup verification, rollback/forward-fix và timing | P0 production |
| POST-OPS-001 | Dead-letter inspection/alert UI | Không chặn local mutation khi outbox state được phản ánh trung thực | operator dashboard, alert thresholds, replay authorization, audit, runbook và retention | P1 |
| POST-OPS-002 | Long-term SLO/metrics/log retention | Demo chỉ cần log sạch và health cơ bản | SLI/SLO, dashboards, alert routes, PII-safe logs, retention/cost policy và incident drill | P1 |
| POST-PERF-001 | Exhaustive EF/query-plan qualification | Demo gate chỉ kiểm tra workflow chính không warning/đứng UI | top-query plans, command-count budgets, pagination/cache validation và regression thresholds | P1 |
| POST-PERF-002 | Load, concurrency, soak and fleet shutdown tests | Không cần để trình diễn đơn phiên có kiểm soát | target traffic model, p95/p99, multi-instance lease, cancellation, graceful shutdown, soak và capacity report | P1 |
| POST-RES-001 | Chaos, backup/restore and disaster recovery | Ngoài phạm vi buổi bảo vệ | dependency outage matrix, RPO/RTO, restore drill, regional/fleet recovery và signed results | P1 |
| POST-A11Y-001 | Full accessibility matrix | Demo gate vẫn yêu cầu keyboard/focus/labels trên màn trình diễn | WCAG audit cho toàn bộ page/component/modal/drawer, 200% zoom, screen reader, reduced motion và remediation report | P1 |
| POST-UI-001 | All breakpoints, dark themes and peripheral surfaces | Chỉ các surface thuộc runbook là demo gate | browser/device matrix cho toàn bộ 17 pages và 73 components, visual regression và overflow report | P2 |
| POST-LEGAL-001 | Dependency/license review | Không thay đổi hành vi phần mềm trong demo học thuật nhưng phải ghi nhận cảnh báo | SBOM, Fluent Assertions decision/replacement, OSS notices và legal approval | P0 commercial |
| POST-SEC-001 | Independent security review/penetration test | Source/integration negative tests vẫn là demo gate; chứng nhận độc lập thì không | threat model sign-off, SAST/DAST/dependency scan, penetration report và remediation evidence | P0 production |

## 3. Những phần vẫn phải hiện đúng trong demo

- Integration chưa cấu hình phải hiện `Chưa cấu hình`, `Tạm vô hiệu` hoặc `External deferred`; không hiện toast “đã gửi/đồng bộ” nếu chưa có receipt.
- VAPID local có thể ghi rõ disabled nếu WebPush không nằm trong kịch bản nghiệm thu.
- Provider fallback phải ghi đúng provider/model thực tế và không ngụy tạo dữ liệu ngoài hệ thống.
- P28 giữ `EXTERNAL_DEFERRED` cho tới khi adapter write/read-back thật có bằng chứng.
- Cấu hình development/demo không được dùng làm bằng chứng rằng production secret, TLS hoặc deployment đã sẵn sàng.

## 4. Điều kiện mở lại mục production

Sau báo cáo, mở lại backlog theo thứ tự:

1. P0 production: deployment/secrets, migration/restore, legal và independent security review;
2. P1: live adapters, operational visibility, performance/load và resilience;
3. P2: toàn bộ browser/device/dark/accessibility matrix ngoài các surface demo.

Mỗi mục chỉ đóng khi có source/config tương ứng, automated evidence thích hợp và runtime read-back từ môi trường thật. Ảnh chụp, toast hoặc response 2xx đơn lẻ không đủ.

## 5. Tiến độ production-hardening ngày 02/09/2026

Các trạng thái dưới đây chỉ thu hẹp phần còn thiếu; file vẫn là backlog production và không tự nâng sản phẩm thành `PRODUCTION_READY`.

| ID | Trạng thái hiện tại | Bằng chứng mới | Phần còn thiếu để đóng |
|---|---|---|---|
| POST-DATA-001 | `LOCAL_REHEARSAL_PASS` | Full chain 57 migration đến P024 trên LocalDB, backup SHA-256, `RESTORE VERIFYONLY`, clean restore, `DBCC CHECKDB`, 95-table smoke, dọn cả source/restore DB; evidence JSON/MD trong `docs/task/qa-evidence/recovery` | Lặp lại trên target SQL Server 2022/production-like host, xác nhận forward-fix/rollback policy và timing được owner ký |
| POST-OPS-001 | `SOURCE_AUTOMATED_PASS_ALERT_PENDING` | Typed operator API/UI, authorized project scope, redacted output, idempotent dead-letter replay, audit, readiness thresholds, daily retention; focused Unit `6/6`, validator `8/8`, WebFeature health/operator `4/4` | Route alert tới operator thật, incident drill và external endpoint receipt |
| POST-DEPLOY-001 | `SOURCE_CONTRACT_PASS_RUNTIME_PENDING` | Docker non-root/health contract; CI source chạy SQL Server + Redis + `/health/ready`; CD chỉ publish exact SHA sau CI success, provenance/SBOM; direct/trusted-proxy contract fail-closed theo IP/hop và negative test chứng minh spoof `X-Forwarded-For` không né limiter ở direct mode | Một CI/container run thành công trên commit đích, secret-injected environment deployment, TLS/host/readiness và rollback record |
| POST-EXT-006 | `CONTROLLED_PATH_PASS_EXTERNAL_PENDING` | Durable occurrence outbox, restart retry/dead-letter, operator replay/read-back và payload redaction | Endpoint thật, chữ ký verified, external read-back và alert delivery |
| POST-EXT-005 | `SOURCE_DURABILITY_PASS_EXTERNAL_PENDING` | Vector outbox P026 có canonical event/aggregate/sequence, exclusive lease/heartbeat, per-aggregate ordering, exponential retry/dead-letter, stale-owner guard, cancellation propagation và degraded health; focused Unit `26/26`, validator + SQL `49/49`, full Unit `801/801`, Integration `304/304` | Qdrant + embedding endpoint/credential thật, corpus tenant-scoped, create/update/delete write/read-back, retrieval-quality baseline, process-loss fleet replay và alert receipt |
| POST-SEC-001 | `LOCAL_BASELINE_PASS_INDEPENDENT_REVIEW_PENDING` | Fresh NuGet transitive scan và npm production audit đều không có vulnerability; CI enforce cả hai; response header + anonymous account throttle có WebFeature evidence; CodeQL C#/JS workflow đã khai báo; 16-row evidence-linked threat model và secret/PII-log hygiene gate đã PASS local | Một CodeQL run sạch trên commit đích, edge/WAF policy, deployed-target threat-model sign-off, authenticated DAST, independent penetration report và remediation evidence |
| POST-OPS-002 | `SOURCE_BASELINE_PASS_RUNTIME_PENDING` | Optional OTLP ASP.NET Core/HTTP request metrics và sampled traces; custom durable AI first-progress/first-answer/terminal SLI có bounded tags; không mở public metrics endpoint; enabled config fail-closed; validator `22/22`, registration `2/2`, metric Unit `1/1`, durable-turn Integration `1/1` | Target collector + dashboard, alert route/receipt, retention/cost policy, baseline/load calibration và incident drill |
| POST-PERF-001 | `SOURCE_QUERY_SAFETY_PASS_PLAN_PENDING` | Đã kiểm kê toàn bộ runtime `Skip`; mọi EF paging có stable unique order bằng `Id`; các capped query chính có deterministic tie-breaker; SQL Server runtime/design-time dùng `SplitQuery`; Task list/detail/Kanban không còn kiểm quyền lặp theo từng dòng sau canonical visibility filter; notification và recent-audit authorization/read count dùng stable batched resolver thay vì N+1; query/service focused Unit `130/130`, Integration `24/24`, registration `1/1`, Task visibility `6/6`, notification/policy `20/20`, audit `3/3`, full Unit `801/801`, Integration `304/304`, WebFeature `49/49` | Chạy top-query execution plans trên dữ liệu production-like, đặt command-count/cache/pagination regression budgets và đo threshold trên target |
| POST-PERF-002 | `SOURCE_HARNESS_PASS_RUNTIME_PENDING` | Read-only load probe có warmup, secret-safe auth, p50/p95/p99, throughput/error threshold và JSON/Markdown; remote plaintext fail-closed; host có shutdown budget Production 5–300 giây. AI/Privacy/GitHub/Vector/outbound webhook có cancellation/shutdown recovery; AI/Privacy thêm stale-writer guard, bounded batch và readiness queue health; Privacy ưu tiên accepted DSAR theo deadline trước retention và operator API/UI hiện cùng signal. Task attention quét stable batch đến hết và đóng stale signal; trash cleanup giữ metadata để retry storage delete. Email digest không false-success khi SMTP lỗi và Project monitor cô lập/defer poison row. Focused affected worker/health Unit `28/28`, Privacy API `4/4`, validator + real SQL `73/73`, auth Integration `12/12`; full client `250/250`, Unit `821/821`, Integration `331/331`, WebFeature `49/49`, build clean | Chạy traffic model authenticated theo role/surface trên release candidate, soak dài, multi-instance process-loss takeover, readiness drain + SIGTERM, target-storage failure recovery và capacity report có deployment/commit receipt |

## 6. Gói đầu vào tối thiểu để tiếp tục tới `PRODUCTION_READY`

Đây là danh sách ngắn nhất cần người sở hữu môi trường cung cấp. Cho tới lúc nhận đủ, agent có thể tiếp tục hardening source nhưng không thể trung thực đóng production gate.

| Gate | Đầu vào bên ngoài bắt buộc | Lượt thực thi tiếp theo | Receipt để đóng |
|---|---|---|---|
| Deploy/runtime | Staging/production host, container registry access, exact public hosts, TLS termination mode, trusted proxy IP/hop và secret store | chạy CI exact SHA, deploy clean environment, kiểm tra `/health/live`/`ready`, restart và rollback | deployment record gắn SHA/image digest, health trước/sau rollback và không dùng placeholder/demo seed |
| Database/recovery | Target SQL Server 2022 connection có quyền tạo DB tạm/backup/restore và owner phê duyệt RPO/RTO | chạy `scripts/rehearse-sqlserver-migrations.ps1` trên target, đo timing và diễn tập forward-fix/restore | JSON/MD evidence, backup hash, `DBCC CHECKDB`, schema smoke, timing và owner sign-off |
| Security | GitHub Security/CodeQL enabled, deployed authenticated test account/URL, edge/WAF owner và independent reviewer | chạy CodeQL trên commit đích, authenticated DAST, threat-model review và penetration test | zero open P0/P1 finding hoặc remediation + rerun; WAF/rate-limit policy và reviewer sign-off |
| Legal | Quyết định giấy phép Fluent Assertions và danh tính người duyệt OSS | duyệt SBOM/provenance, thay dependency nếu không có quyền dùng thương mại, xuất notices | license decision, OSS notices và approval record |
| External adapters | Chỉ các adapter sẽ bật ở production: credential/endpoint thật cho GitHub, SMTP, WebPush, LiveKit, Qdrant hoặc webhook | chạy write/read-back, revoke/failure/retry/idempotency và audit theo từng adapter | canonical external receipt; adapter chưa cung cấp giữ `disabled`/`EXTERNAL_DEFERRED`, P28 không được PASS |

Không cần bật mọi adapter để deploy một bản production có phạm vi hẹp; nhưng mọi adapter được quảng bá là hoạt động phải có receipt thật. Việc thiếu một đầu vào ở bảng trên là `BLOCKED_EXTERNAL`, không phải lỗi source và cũng không phải PASS.
