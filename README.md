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
docker compose --profile dev up -d
```

### Dev vs Full
- `dev`: core stack for day-to-day development
- `full`: core stack plus AI services (`qdrant`, `ollama`)

To run the full stack:
```powershell
docker compose --profile full up -d
```

### 2. Configure Environment Variables (Bắt buộc cho AI Cloud)

Các nhà cung cấp AI trong AI Gateway (như OpenAI, Gemini) sẽ đọc API Key từ biến môi trường của hệ thống. Nếu không cấu hình, hệ thống sẽ tự động đi vào chế độ fallback/mock data an toàn (mặc định cho môi trường phát triển local).

Để chạy thật hoặc demo với AI Cloud, hãy set API Key:

- **Windows (PowerShell):**
  ```powershell
  $env:OPENAI_API_KEY="your_real_openai_api_key"
  $env:GEMINI_API_KEY="your_real_gemini_api_key"
  ```
- **Windows (Command Prompt):**
  ```cmd
  set OPENAI_API_KEY=your_real_openai_api_key
  set GEMINI_API_KEY=your_real_gemini_api_key
  ```
- **Linux/macOS:**
  ```bash
  export OPENAI_API_KEY="your_real_openai_api_key"
  export GEMINI_API_KEY="your_real_gemini_api_key"
  ```

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
- **Cuộc họp nhóm:** API start/join/end và phân quyền có bằng chứng, nhưng participant realtime/count đang có lỗi P0 `DH03-BUG-MTG-001`; không demo participant count/list như tính năng ổn định trước khi fix.
- **Chia sẻ màn hình:** nhánh browser unsupported không crash, nhưng UI feedback chưa rõ (`DH03-BUG-MTG-002`, P2); cần kiểm thử lại bằng browser thật/headful nếu đưa vào demo.
- **AI analytics / Group AI:** có smoke/fallback/schema tests, nhưng manual deep check và Group AI E2E chưa có bằng chứng nghiệm thu đầy đủ; cần cấu hình provider hoặc chấp nhận phản hồi dự phòng AI.
- **Deploy config:** chưa có bằng chứng nghiệm thu đầy đủ cho checklist deploy tuần này.

Tài liệu QA liên quan:

- [`docs/task/test-plan-tuan-2026-06-03.md`](./docs/task/test-plan-tuan-2026-06-03.md)
- [`docs/task/qa-evidence/meeting-import-qa-2026-06-03.md`](./docs/task/qa-evidence/meeting-import-qa-2026-06-03.md)
- [`docs/task/Bao_cao_kiem_thu_tuan_2026-06-03.md`](./docs/task/Bao_cao_kiem_thu_tuan_2026-06-03.md)
- [`docs/task/checklist-nghiem-thu-2026-06-09.csv`](./docs/task/checklist-nghiem-thu-2026-06-09.csv)

## 👥 Team

Qaly Team - 7 members

## 📄 License

Private - Internal Use Only
