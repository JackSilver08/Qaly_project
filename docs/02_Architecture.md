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
├── Qaly_project.sln
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

*Cập nhật khi có thay đổi trong quá trình triển khai.*
