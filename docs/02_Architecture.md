# 🏗️ QALY PROJECT – THIẾT KẾ KIẾN TRÚC

> **Ngày tạo:** 01/05/2026 | **Phiên bản:** 1.0 | **Trạng thái:** Draft

---

## I. KIẾN TRÚC TỔNG QUAN

### Pattern: Clean Architecture + Modular Monolith

```
┌─────────────────────────────────────────────┐
│              Qaly.Web                       │
│         (Razor Pages + Vue Islands)         │
├─────────────────────────────────────────────┤
│            Qaly.Application                 │
│         Services, DTOs, Validation          │
├─────────────────────────────────────────────┤
│            Qaly.Infrastructure              │
│         DbContext, Repositories, Identity   │
├─────────────────────────────────────────────┤
│              Qaly.Domain                    │
│         Entities, Enums, Interfaces         │
│         *** KHÔNG PHỤ THUỘC GÌ ***          │
└─────────────────────────────────────────────┘
```

### Dependency Rule

```
Web → Application → Domain
Web → Infrastructure → Domain
Application ✗→ Infrastructure   (chỉ qua interface)
Domain ✗→ bất kỳ layer nào
```

---

## II. CẤU TRÚC THƯ MỤC

```
c:\Qaly_project\
├── Qaly_project.slnx
├── src/
│   ├── Qaly.Domain/           ← Entities, Enums, Interfaces
│   ├── Qaly.Application/      ← Services, DTOs, Exceptions, Mappings
│   ├── Qaly.Infrastructure/   ← DbContext, Configs, Repos, Migrations, Seeds
│   └── Qaly.Web/              ← Razor Pages, Hubs, Filters, wwwroot
├── tests/
│   ├── Qaly.UnitTests/
│   └── Qaly.IntegrationTests/
├── docs/
├── .gitignore
└── README.md
```

Xem chi tiết từng file trong tài liệu phân tích `01_Analysis.md`.

---

## III. NUGET PACKAGES

| Layer | Package | Version |
|---|---|---|
| Domain | *(Không có)* | – |
| Application | AutoMapper.Extensions.Microsoft.DependencyInjection | 12.* |
| Application | FluentValidation | 11.* |
| Infrastructure | Microsoft.EntityFrameworkCore.SqlServer | 10.* |
| Infrastructure | Microsoft.EntityFrameworkCore.Tools | 10.* |
| Infrastructure | Microsoft.AspNetCore.Identity.EntityFrameworkCore | 10.* |
| Web | Microsoft.EntityFrameworkCore.Design | 10.* |
| UnitTests | xunit, Moq, FluentAssertions | latest |
| UnitTests | Microsoft.EntityFrameworkCore.InMemory | 10.* |

---

## IV. CONVENTIONS

- **Entity**: PascalCase, singular (`TaskItem`, `Project`)
- **Table**: PascalCase, plural (`TaskItems`, `Projects`)
- **Interface**: Prefix `I` (`IRepository`, `ITaskService`)
- **DTO**: Suffix `Dto` (`TaskItemDto`, `CreateProjectDto`)
- **Git branches**: `feature/`, `bugfix/`, `hotfix/`
- **Commits**: conventional commits (`feat:`, `fix:`, `docs:`)

---

## V. GHI CHÚ NGHIỆM THU HIỆN TẠI

Phần kiến trúc mô tả định hướng và cấu trúc kỹ thuật. Không dùng tài liệu này để khẳng định mọi chức năng đã nghiệm thu đầy đủ nếu chưa có bằng chứng QA đi kèm.

| Khu vực | Trạng thái theo bằng chứng tuần 03/06/2026 - 09/06/2026 |
|---|---|
| Nhập tài liệu | Có bằng chứng pass cho DOCX, ZIP chứa file hỗ trợ và PDF unsupported/roadmap rõ ràng; chưa ghi nhận hỗ trợ mọi định dạng. |
| Cuộc họp nhóm | Backend start/join/end và phân quyền có test; participant realtime/count còn lỗi P0 `DH03-BUG-MTG-001`. |
| Chia sẻ màn hình | Browser unsupported không crash nhưng UI feedback chưa rõ, lỗi P2 `DH03-BUG-MTG-002`; cần xác minh bằng browser thật/headful cho positive case. |
| AI analytics / Group AI | Có test smoke/fallback/schema; manual deep check và Group AI E2E chưa có bằng chứng đầy đủ. |
| Deploy config | Chưa có bằng chứng nghiệm thu đầy đủ cho checklist deploy tuần này. |
| Phản hồi dự phòng AI | AI có thể phụ thuộc provider/local config; khi thiếu provider phải ghi rõ fallback/mock thay vì claim AI thật đầy đủ. |

### Thuật ngữ dùng trong tài liệu nghiệm thu

| Thuật ngữ code/Anh | Thuật ngữ tiếng Việt ưu tiên |
|---|---|
| group | nhóm |
| project | dự án |
| task | công việc |
| poll | bình chọn |
| meeting | cuộc họp |
| import | nhập tài liệu |
| permission | phân quyền |
| evidence | bằng chứng kiểm thử |
| realtime | thời gian thực |
| AI fallback | phản hồi dự phòng AI |

---

*Cập nhật khi có thay đổi trong quá trình triển khai.*
