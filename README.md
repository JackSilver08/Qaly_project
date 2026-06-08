# 🚀 Qaly Project - Project Management System

[![CI](https://github.com/JackSilver08/Qaly_project/actions/workflows/ci.yml/badge.svg)](https://github.com/JackSilver08/Qaly_project/actions/workflows/ci.yml)
[![CD](https://github.com/JackSilver08/Qaly_project/actions/workflows/cd.yml/badge.svg)](https://github.com/JackSilver08/Qaly_project/actions/workflows/cd.yml)

Hệ thống quản lý dự án nội bộ với Project / Task / Comment / Notification.

## 🏗️ Tech Stack

| Component | Technology |
|---|---|
| Backend | ASP.NET Core 10, C# |
| Frontend | Razor Pages + Vue.js Islands |
| Database | SQL Server 2022 |
| Cache | Redis 7 |
| Realtime | SignalR |
| Logging | Seq |
| CI/CD | GitHub Actions |
| Container | Docker |

## ⚡ Quick Start

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- [Git](https://git-scm.com/)

### 1. Clone & Start Services
```powershell
git clone https://github.com/JackSilver08/Qaly_project.git
cd Qaly_project
Copy-Item .env.example .env
docker compose up -d
```

Lệnh trên chạy hạ tầng local. Ứng dụng web chạy trên host bằng `dotnet run`.

Để chạy cả ứng dụng web trong Docker:
```powershell
docker compose --profile docker-web up -d
```

### 2. Configure Environment Variables

Không ghi credential vào `appsettings*.json`. Điền secret local trong `.env` (file này bị Git ignore) hoặc biến môi trường của hệ thống:

```dotenv
LiveKit__ServerUrl=wss://your-project.livekit.cloud
LiveKit__ApiKey=...
LiveKit__ApiSecret=...
OPENAI_API_KEY=...
GEMINI_API_KEY=...
```

Nếu không cấu hình LiveKit, meeting vẫn dùng luồng fallback hiện có nhưng không cấp media token. Nếu không cấu hình AI cloud, AI Gateway dùng fallback/mock theo cấu hình development.

### 3. Run Database Migrations & Seed
Nếu chạy cơ sở dữ liệu Microsoft SQL Server thật bằng Docker, hãy áp dụng các migrations để tạo bảng dữ liệu:
```powershell
dotnet ef database update --project src/Qaly.Infrastructure --startup-project src/Qaly.Web
```

### 4. Build Frontend & Run App
Frontend của dự án đã được bundle sẵn tại `src/Qaly.Web/wwwroot/dist`. Tuy nhiên, nếu bạn có thay đổi code giao diện Vue.js:
```powershell
# Cài đặt dependency & build UI
npm ci
npm run build

# Chạy kiểm tra kiểu TypeScript (Typecheck)
npm run typecheck
```

Chạy Server Backend:
```powershell
dotnet restore
dotnet run --project src/Qaly.Web
```

### 5. Running Tests
Để kiểm thử hệ thống và xác thực hạ tầng AI Gateway:
```powershell
# Chạy tất cả tests trong Solution
dotnet test Qaly_project.slnx

# Chạy riêng các tests của AI Gateway và Provider Routing
dotnet test tests/Qaly.UnitTests/Qaly.UnitTests.csproj --filter "FullyQualifiedName~AiGateway|FullyQualifiedName~AiProvider"
```

### 6. Open Browser
- **App:** http://localhost:5000
- **Seq Logs (Logging tập trung):** http://localhost:8081
- **MailHog (Bắt email test):** http://localhost:8025

## 📁 Project Structure

```
Qaly_project/
├── src/
│   ├── Qaly.Domain/           ← Entities, Enums, Interfaces
│   ├── Qaly.Application/      ← Services, DTOs, Validation
│   ├── Qaly.Infrastructure/   ← DbContext, EF Config, Repos
│   └── Qaly.Web/              ← Razor Pages, Hubs, wwwroot
├── tests/
│   ├── Qaly.UnitTests/
│   └── Qaly.IntegrationTests/
├── docs/                      ← Project documentation
├── docker/                    ← Docker init scripts
└── .github/workflows/         ← CI/CD pipelines
```

## 🔄 Git Workflow

```
main ──────────────────────────────── (production)
  └── develop ─────────────────────── (integration)
        ├── feature/user-auth ─────── (feature branch)
        ├── feature/task-board ────── (feature branch)
        └── bugfix/login-error ────── (bugfix branch)
```

1. Tạo branch từ `develop`: `git checkout -b feature/ten-feature develop`
2. Code & commit: `git commit -m "feat: add login page"`
3. Push & tạo PR vào `develop`
4. CI tự động chạy build + test
5. Review → Approve → Merge
6. Khi sẵn sàng release: merge `develop` → `main`

## 📖 Documentation

Xem thư mục [`docs/`](./docs/) để biết chi tiết.

## ✅ Trạng Thái Nghiệm Thu Hiện Tại

Theo bằng chứng QA tuần 03/06/2026 - 09/06/2026:

- **Nhập tài liệu:** có thể demo theo scope đã kiểm thử với DOCX, ZIP chứa `.md/.txt/.html`, và luồng PDF báo unsupported/roadmap rõ ràng. Không ghi nhận là hỗ trợ mọi định dạng.
- **Phân quyền cơ bản:** có bằng chứng E2E/backend regression cho login, admin/member boundary và outside user bị chặn ở một số API quan trọng.
- **Cuộc họp nhóm:** API start/join/end, phân quyền, SignalR presence và reconnect đã có regression; Playwright hai context pass. Media participant count vẫn cần LiveKit thật để nghiệm thu headful.
- **Chia sẻ màn hình:** nhánh lỗi/unsupported không crash và có toast rõ; cần kiểm thử positive case bằng browser thật/headful nếu đưa vào demo.
- **AI analytics / Group AI:** có smoke/fallback/schema tests, nhưng manual deep check và Group AI E2E chưa có bằng chứng nghiệm thu đầy đủ; cần cấu hình provider hoặc chấp nhận phản hồi dự phòng AI.
- **Deploy config:** production image build pass và chạy non-root; CD mới publish image lên GHCR, chưa deploy tới hạ tầng thật.

Tài liệu QA liên quan:

- [`docs/task/test-plan-tuan-2026-06-03.md`](./docs/task/test-plan-tuan-2026-06-03.md)
- [`docs/task/qa-evidence/meeting-import-qa-2026-06-03.md`](./docs/task/qa-evidence/meeting-import-qa-2026-06-03.md)
- [`docs/task/Bao_cao_kiem_thu_tuan_2026-06-03.md`](./docs/task/Bao_cao_kiem_thu_tuan_2026-06-03.md)
- [`docs/task/checklist-nghiem-thu-2026-06-09.csv`](./docs/task/checklist-nghiem-thu-2026-06-09.csv)

## 👥 Team

Qaly Team - 7 members

## 📄 License

Private - Internal Use Only
