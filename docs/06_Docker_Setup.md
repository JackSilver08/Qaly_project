# 🐳 QALY PROJECT – DOCKER SETUP GUIDE

> **Ngày tạo:** 01/05/2026 | **Phiên bản:** 1.0

---

## I. TỔNG QUAN SERVICES

| Service             | Image                                        | Port (Host)                | Mô tả                                           |
| ------------------- | -------------------------------------------- | -------------------------- | ----------------------------------------------- |
| **SQL Server 2022** | `mcr.microsoft.com/mssql/server:2022-latest` | `1434`                     | Database chính (tránh conflict port 1433 local) |
| **Redis 7**         | `redis:7-alpine`                             | `6380`                     | Cache + SignalR Backplane                       |
| **Seq**             | `datalust/seq:latest`                        | `8081` (UI), `5341` (API)  | Structured Logging Dashboard                    |
| **MailHog**         | `mailhog/mailhog:latest`                     | `8025` (UI), `1025` (SMTP) | Fake SMTP cho dev                               |
| **Qaly Web**        | Build từ Dockerfile                          | `5000`                     | App (chỉ khi dùng profile `full`)               |

---

## II. CÁCH SỬ DỤNG

### 2.1 Khởi động services hỗ trợ (không chạy app)

```powershell
# Chỉ chạy SQL Server + Redis + Seq + MailHog
docker compose up -d
```

> Đây là mode **thường dùng nhất** khi develop. App chạy local bằng `dotnet run`.

### 2.2 Khởi động toàn bộ (bao gồm app)

```powershell
# Chạy tất cả, bao gồm app trong container
docker compose --profile full up -d
```

### 2.3 Dừng services

```powershell
# Dừng nhưng giữ data
docker compose down

# Dừng VÀ xóa data (reset hoàn toàn)
docker compose down -v
```

### 2.4 Xem logs

```powershell
# Tất cả
docker compose logs -f

# Chỉ SQL Server
docker compose logs -f qaly-sqlserver

# Chỉ app
docker compose logs -f qaly-web
```

---

## III. CONNECTION STRINGS

### Khi chạy app LOCAL (dotnet run)

```json
{
    "ConnectionStrings": {
        "DefaultConnection": "Data Source=CMI\\SQLEXPRESS;Initial Catalog=QalyDb;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
    },
    "Redis": {
        "ConnectionString": "localhost:6380"
    },
    "Seq": {
        "ServerUrl": "http://localhost:5341"
    },
    "Email": {
        "SmtpHost": "localhost",
        "SmtpPort": 1025
    }
}
```

### Khi chạy app TRONG Docker

```json
{
    "ConnectionStrings": {
        "DefaultConnection": "Server=qaly-sqlserver,1433;Database=QalyDb;User Id=sa;Password=Qaly@Dev2026!;TrustServerCertificate=True;MultipleActiveResultSets=true"
    },
    "Redis": {
        "ConnectionString": "qaly-redis:6379"
    },
    "Seq": {
        "ServerUrl": "http://qaly-seq:5341"
    },
    "Email": {
        "SmtpHost": "qaly-mailhog",
        "SmtpPort": 1025
    }
}
```

---

## IV. WEB UI DASHBOARDS

| Service       | URL                   | Mô tả                                      |
| ------------- | --------------------- | ------------------------------------------ |
| Seq Logging   | http://localhost:8081 | Xem structured logs                        |
| MailHog Email | http://localhost:8025 | Xem email test                             |
| Qaly App      | http://localhost:5000 | Web app (khi chạy local hoặc profile full) |

---

## V. FILE DOCKER

```
c:\Qaly_project\
├── Dockerfile                         ← Multi-stage build (dev + production)
├── docker-compose.yml                 ← Services chính
├── docker-compose.override.yml        ← Dev overrides (tự động load)
├── .dockerignore                      ← Exclude files khỏi build context
├── .env.example                       ← Template environment variables
└── docker/
    └── sqlserver/
        └── init/
            └── 01_init-database.sql   ← Script khởi tạo DB
```

---

## VI. TROUBLESHOOTING

### SQL Server không start được

```powershell
# Kiểm tra logs
docker compose logs qaly-sqlserver

# Thường do thiếu RAM (SQL Server cần ít nhất 2GB)
# Tăng RAM cho Docker Desktop: Settings > Resources > Memory
```

### Port conflict

```powershell
# Kiểm tra port đang dùng
netstat -an | findstr "1434"
netstat -an | findstr "6380"

# Nếu conflict: đổi port trong docker-compose.yml
```

### Reset toàn bộ data

```powershell
docker compose down -v
docker compose up -d
```

---

_Cập nhật khi có thay đổi cấu hình Docker._
