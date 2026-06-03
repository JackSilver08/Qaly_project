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

### 2. Run App
```powershell
dotnet restore
dotnet run --project src/Qaly.Web
```

### 3. Open Browser
- **App:** http://localhost:5000
- **Seq Logs:** http://localhost:8081
- **MailHog:** http://localhost:8025

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

## 👥 Team

Qaly Team - 7 members

## 📄 License

Private - Internal Use Only
