# Qaly Deployment Guide

> Cập nhật: 08/06/2026

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

## 4. Kiểm tra trước triển khai

```powershell
docker compose config --quiet
dotnet test tests/Qaly.UnitTests/Qaly.UnitTests.csproj
dotnet test tests/Qaly.IntegrationTests/Qaly.IntegrationTests.csproj
npm --prefix src/Qaly.Web/ClientApp run typecheck
npm --prefix src/Qaly.Web/ClientApp run build
```

Xác nhận thêm:

- `/health` trả về trạng thái healthy.
- Migration đã áp dụng cho đúng database.
- Không có placeholder `<...>` trong cấu hình production.
- Log không in secret, token hoặc connection string đầy đủ.
- LiveKit join và AI provider được smoke test nếu các tính năng này được bật.

Smoke test Ollama local:

```powershell
.\scripts\smoke-ai-provider.ps1
```

Xác minh candidate image và rollback về image ổn định trước đó:

```powershell
$env:SQLSERVER_SA_PASSWORD = "<local-or-secret-store-value>"
.\scripts\validate-container-release.ps1 `
  -CandidateImage "qaly:candidate" `
  -RollbackImage "qaly:stable"
```

Image dùng làm rollback phải từng vượt qua `/health`; không gắn tag ổn định cho image chỉ mới build thành công.

## 5. Xử lý secret bị lộ

1. Thu hồi hoặc rotate secret trên hệ thống cung cấp.
2. Cập nhật secret store của môi trường.
3. Triển khai lại ứng dụng.
4. Kiểm tra audit log và quyền truy cập liên quan.

Xóa secret khỏi commit mới không làm secret cũ biến mất khỏi lịch sử Git; rotation vẫn là bước bắt buộc.
