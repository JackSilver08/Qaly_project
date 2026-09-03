# Qaly Deployment Guide

> Cập nhật: 02/09/2026

## 1. Nguyên tắc cấu hình

- Không commit password, API key, token hoặc connection string production.
- Development local dùng `.env`, biến môi trường hoặc .NET User Secrets.
- CI/CD và production dùng secret store của nền tảng triển khai.
- Biến môi trường ASP.NET Core dùng dấu `__` để phân cấp, ví dụ `LiveKit__ApiSecret`.

## 2. Khởi động local

```powershell
Copy-Item .env.example .env
docker compose up -d
dotnet run --project src/Qaly.Web
```

Chạy ứng dụng web trong Docker:

```powershell
docker compose --profile docker-web up -d
```

## 3. Cấu hình bắt buộc theo môi trường

| Biến | Development | Production |
| --- | --- | --- |
| `ConnectionStrings__DefaultConnection` | Có mặc định local | Bắt buộc |
| `QALY_SEED_ADMIN_PASSWORD` | Bắt buộc khi seed | Không seed tự động |
| `QALY_SEED_DEFAULT_USER_PASSWORD` | Bắt buộc khi seed | Không seed tự động |
| `LiveKit__ServerUrl` | Tùy chọn | Bắt buộc nếu bật media meeting |
| `LiveKit__ApiKey` | Tùy chọn | Bắt buộc nếu bật media meeting |
| `LiveKit__ApiSecret` | Tùy chọn | Bắt buộc nếu bật media meeting |
| `OPENAI_API_KEY` | Tùy chọn | Bắt buộc nếu dùng OpenAI |
| `GEMINI_API_KEY` | Tùy chọn | Bắt buộc nếu dùng Gemini |
| `InvitationLink__FrontendBaseUrl` | Có mặc định local | Bắt buộc |
| `OperationalHealth__WebhookOutboxPendingThreshold` | Mặc định `200` | 1–100000 |
| `OperationalHealth__WebhookOutboxMaxPendingAgeMinutes` | Mặc định `15` | 1–1440 |
| `OperationalHealth__WebhookDeliveryRetentionDays` | Mặc định `30` | 7–365 |
| `OperationalHealth__WebhookProcessedOutboxRetentionDays` | Mặc định `30` | 7–365 |
| `Security__LoginRequestsPerMinute` | Mặc định `30` | 5–1000 |
| `Security__RegistrationRequestsPerHour` | Mặc định `10` | 1–100 |
| `ReverseProxy__Mode` | `direct` | `direct` hoặc `trusted-proxy` |
| `ReverseProxy__KnownProxies__0` | Trống | IP cụ thể khi dùng `trusted-proxy` |
| `ReverseProxy__ForwardLimit` | `1` | Số proxy hop tin cậy, 1–5 |
| `Telemetry__OtlpEnabled` | `false` | Bật khi có collector được vận hành |
| `Telemetry__OtlpEndpoint` | Trống | URL OTLP HTTP/HTTPS tuyệt đối khi bật |
| `Telemetry__AllowInsecureOtlp` | `false` | Chỉ `true` khi chấp nhận rõ collector HTTP trong mạng tin cậy |
| `Telemetry__ServiceName` | `qaly-web` | Tên service ổn định trong collector |
| `Telemetry__TraceSampleRatio` | `0.1` | Số lớn hơn 0 và không quá 1 |

`appsettings.Production.example.json` là mẫu để dựng secret-injected profile, không được runtime tự nạp. Production khởi động fail-closed khi thiếu infrastructure, host/TLS, Data Protection, quyết định AI/privacy hoặc integration credential đã bật.

## 4. Kiểm tra trước triển khai

```powershell
docker compose config --quiet
./scripts/check-configuration.ps1
./scripts/check-config-parity.ps1
./scripts/check-security-hygiene.ps1
dotnet test tests/Qaly.UnitTests/Qaly.UnitTests.csproj
dotnet test tests/Qaly.IntegrationTests/Qaly.IntegrationTests.csproj
npm --prefix src/Qaly.Web/ClientApp run typecheck
npm --prefix src/Qaly.Web/ClientApp run build
```

Xác nhận thêm:

- `/health/live` trả về trạng thái tiến trình; Docker `HEALTHCHECK` dùng endpoint này để tránh restart loop chỉ vì dependency tạm lỗi.
- `/health/ready` kiểm tra SQL Server, Redis, vector outbox và webhook outbox; `/health` là alias tương thích có cùng readiness semantics.
- Payload health công khai chỉ có deployment/version/tên check/status/duration, không chứa exception, connection string, secret hoặc provider response.
- Mọi response có `nosniff`, deny framing, strict referrer, Permissions Policy tối thiểu và CSP bảo vệ `base-uri`/`frame-ancestors`/`object-src`; Kestrel không phát `Server` header. CSP connect/script/style đầy đủ phải được chốt theo target LiveKit/provider bằng browser/DAST evidence trước production.
- Login và đăng ký dùng limiter tách riêng theo client IP; khi vượt ngưỡng trả `429` + `Retry-After` và thông báo rõ ràng. Edge/WAF vẫn phải có rate limit riêng cho target public.
- Nếu TLS kết thúc ở reverse proxy, đặt mode `trusted-proxy`, khai báo từng IP proxy và số hop; Qaly chỉ xử lý `X-Forwarded-For/Proto` từ các địa chỉ này trước HTTPS redirect, secure cookie và limiter. Không clear trust list hoặc tin wildcard network.
- Khi bật OTLP, Qaly xuất ASP.NET Core/HTTP client metrics và sampled traces thẳng tới collector; không mở `/metrics` công khai. Nếu collector yêu cầu header, inject `OTEL_EXPORTER_OTLP_HEADERS` từ secret store, không ghi token vào `appsettings` hay log.
- Migration đã áp dụng cho đúng database.
- Không có placeholder `<...>` trong cấu hình production.
- Log không in secret, token hoặc connection string đầy đủ.
- Raw email/full name/phone/address chỉ được lưu ở canonical data/audit theo quyền; operational log dùng entity/correlation ID khi đủ. Xem threat/control/residual risk trong `docs/security-threat-model.md`.
- LiveKit join và AI provider được smoke test nếu các tính năng này được bật.

Smoke test Ollama local:

```powershell
.\scripts\smoke-ai-provider.ps1
```

Clean restore evidence từ backup SQL Server:

```powershell
$env:SQLSERVER_SA_PASSWORD = "<local-or-secret-store-value>"
.\scripts\restore-sqlserver-clean.ps1 `
  -Server "localhost,1433" `
  -BackupFile ".backups\QalyDb-YYYYMMDD-HHMMSS.bak" `
  -User "sa" `
  -Password $env:SQLSERVER_SA_PASSWORD
```

Script này restore vào database sạch riêng, chạy `RESTORE VERIFYONLY`, `DBCC CHECKDB`, smoke query và ghi evidence trong `docs/task/qa-evidence/recovery`. Xem chi tiết tại `docs/recovery-clean-restore-runbook.md`.

Diễn tập đầy đủ migration đầu tiên → mới nhất, backup, clean restore, integrity check và tự dọn hai database rehearsal:

```powershell
.\scripts\rehearse-sqlserver-migrations.ps1
```

Với SQL authentication:

```powershell
$env:SQLSERVER_SA_PASSWORD = "<secret-store-value>"
.\scripts\rehearse-sqlserver-migrations.ps1 `
  -Server "localhost,1433" `
  -UseSqlAuthentication
```

Script chỉ được phép tạo/xóa database có prefix sinh tự động `QalyMigrationRehearsal_` và `QalyRestoreRehearsal_`; không được đổi sang database người dùng.

Xác minh candidate image và rollback về image ổn định trước đó:

```powershell
$env:SQLSERVER_SA_PASSWORD = "<local-or-secret-store-value>"
.\scripts\validate-container-release.ps1 `
  -CandidateImage "qaly:candidate" `
  -RollbackImage "qaly:stable"
```

Image dùng làm rollback phải từng vượt qua `/health/ready`; không gắn tag ổn định cho image chỉ mới build thành công. CI kiểm tra thêm image chạy non-root, readiness trên SQL Server + Redis thật và trạng thái Docker health; workflow CD chỉ publish đúng commit SHA đã có CI kết luận thành công. Publish image không đồng nghĩa đã deploy môi trường.

## 5. Vận hành webhook outbox

- Owner/Admin/Project Manager mở Project → Cài đặt → Webhook để xem số pending/dead-letter, lịch sử outbox và delivery gần nhất.
- `webhook_outbox = degraded` hoặc dead-letter > 0 yêu cầu operator kiểm tra; trạng thái degraded không tự restart tiến trình.
- Replay chỉ mở với dead-letter, dùng cùng occurrence id và có audit `ReplayWebhookDeadLetter`; click/retry lặp không tạo thêm mutation.
- UI/API vận hành không trả request payload, response body hoặc webhook secret.
- Worker retention chạy mỗi 24 giờ. Delivery log cũ và outbox đã xử lý được dọn theo cấu hình; dead-letter không bị retention tự xóa để operator còn điều tra/replay.
- Alert route ngoài hệ thống và endpoint webhook thật vẫn phải có runtime receipt trước khi nghiệm thu production.

Chi tiết và checklist sự cố ở [production operations runbook](production-operations-runbook.md).

## 6. Xử lý secret bị lộ

1. Thu hồi hoặc rotate secret trên hệ thống cung cấp.
2. Cập nhật secret store của môi trường.
3. Triển khai lại ứng dụng.
4. Kiểm tra audit log và quyền truy cập liên quan.

Xóa secret khỏi commit mới không làm secret cũ biến mất khỏi lịch sử Git; rotation vẫn là bước bắt buộc.
