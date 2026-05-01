# 📋 QALY PROJECT – PHÂN TÍCH DỰ ÁN

> **Ngày tạo:** 01/05/2026  
> **Phiên bản:** 1.0  
> **Trạng thái:** Draft

---

## I. TỔNG QUAN DỰ ÁN

### 1.1 Mô tả
Qaly là hệ thống quản lý dự án nội bộ (Project Management System) hỗ trợ:
- Quản lý Project / Task / Comment
- Thông báo realtime (SignalR)
- Phân quyền RBAC (Role-Based Access Control)
- Audit Log cho mọi thao tác

### 1.2 Mục tiêu
- Xây dựng sản phẩm SaaS nội bộ có khả năng thương mại hóa
- Kiến trúc Clean Architecture, dễ mở rộng
- Sử dụng SQL Server thay vì PostgreSQL

---

## II. KIỂM TRA MÔI TRƯỜNG

### 2.1 Kết quả scan (01/05/2026)

| Thành phần | Trạng thái | Chi tiết |
|---|---|---|
| .NET SDK | ✅ Đã cài | `10.0.203` |
| `dotnet` CLI | ⚠️ Lỗi PATH | Tồn tại tại `C:\Program Files\dotnet\dotnet.exe` nhưng PowerShell không nhận |
| SQL Server | ✅ Đang chạy | Service `MSSQLSERVER` – Running |
| `sqlcmd` | ✅ Sẵn sàng | Version 15.0.1300.359 |
| Target Framework | ℹ️ .NET 10 | Được set trong `.csproj` |

### 2.2 Fix PATH cho dotnet

```powershell
# Tạm thời (session hiện tại)
$env:PATH += ";C:\Program Files\dotnet"

# Vĩnh viễn (chạy PowerShell as Admin)
[Environment]::SetEnvironmentVariable("PATH", $env:PATH + ";C:\Program Files\dotnet", "Machine")

# Hoặc dùng full path
& "C:\Program Files\dotnet\dotnet.exe" --version
```

---

## III. HIỆN TRẠNG SOURCE CODE

### 3.1 Cấu trúc ban đầu (từ VS template)

```
c:\Qaly_project\
├── Qaly_project.slnx          ← Solution (format mới .NET 10)
├── Qaly_project.csproj         ← Single Razor Pages project
├── Program.cs                  ← Minimal hosting
├── appsettings.json
├── Pages/                      ← Index, Privacy, Error (mặc định)
├── wwwroot/                    ← css, js, lib (jQuery, Bootstrap)
└── Properties/
```

### 3.2 Đánh giá

| Tiêu chí | Nhận xét |
|---|---|
| Template | Default ASP.NET Razor Pages |
| Framework | .NET 10.0 |
| Solution format | `.slnx` (format mới) |
| Business logic | ❌ Chưa có |
| Database | ❌ Chưa cấu hình |
| Authentication | ❌ Chưa có |
| Architecture | Monolith đơn project |

> **Kết luận:** Project hoàn toàn mới từ template. Sẽ xóa và khởi tạo lại theo Clean Architecture.

---

## IV. MAPPING POSTGRES → SQL SERVER

| Postgres | SQL Server | EF Core Config | Ghi chú |
|---|---|---|---|
| `UUID` | `UNIQUEIDENTIFIER` | `.HasDefaultValueSql("NEWID()")` | Tự động generate |
| `SERIAL` / `BIGSERIAL` | `INT/BIGINT IDENTITY(1,1)` | `.ValueGeneratedOnAdd()` | Cho AuditLog.Id |
| `JSONB` | `NVARCHAR(MAX)` | `.HasColumnType("nvarchar(max)")` | Serialize bằng `System.Text.Json` |
| `TIMESTAMPTZ` | `DATETIMEOFFSET` | Mặc định mapping | Giữ timezone info |
| `TEXT` | `NVARCHAR(MAX)` | Mặc định mapping | |
| `VARCHAR(n)` | `NVARCHAR(n)` | `.HasMaxLength(n)` | Unicode support |
| `BOOLEAN` | `BIT` | Mặc định mapping | |
| `GIN INDEX` (FTS) | `LIKE` → `FULLTEXT INDEX` | Phase 1: `.Contains()` | Phase 2: raw SQL fulltext |
| `xmin` (concurrency) | `ROWVERSION` | `.IsRowVersion()` | Optimistic concurrency |

---

## V. PHÂN TÍCH RỦI RO

### 5.1 Rủi ro cao

| Rủi ro | Impact | Giải pháp |
|---|---|---|
| AuditLog JSONB → string | Mất query JSON path | Dùng `OPENJSON()` SQL Server. Phase 1: C# deserialize |
| Full-text Search | Performance kém với `LIKE` | Phase 1: `LIKE` + pagination. Phase 2: Full-Text Catalog |
| `.slnx` format | Không tương thích CI/CD | Chuyển sang `.sln` chuẩn |
| Code spaghetti | Khó maintain | Clean Architecture + Code Review |

### 5.2 Rủi ro trung bình

| Rủi ro | Impact | Giải pháp |
|---|---|---|
| .NET 10 preview | Breaking changes | Theo dõi release notes, pin version |
| Vue islands + Razor | Phức tạp debug | Tách rõ API endpoints cho Vue |
| SignalR scaling | Single server bottleneck | Phase 1: In-process. Phase 2: Redis backplane |

---

## VI. CÁC QUYẾT ĐỊNH KIẾN TRÚC

| # | Quyết định | Lựa chọn | Lý do |
|---|---|---|---|
| 1 | Xử lý project cũ | Xóa sạch, tạo lại | Project chưa có logic, tạo mới sạch hơn |
| 2 | Authentication | ASP.NET Identity | Đầy đủ, có sẵn migration, RBAC tích hợp |
| 3 | Solution format | `.sln` classic | Tương thích cao với CI/CD và tooling |
| 4 | Target Framework | .NET 10.0 | SDK đã cài, giữ mới nhất |
| 5 | DB Approach | Code-First (EF) | Greenfield project, linh hoạt |

---

*Tài liệu này sẽ được cập nhật khi có thay đổi trong quá trình triển khai.*
