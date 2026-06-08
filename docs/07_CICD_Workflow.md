# 🔄 QALY PROJECT – CI/CD & GIT WORKFLOW

> **Ngày tạo:** 01/05/2026 | **Phiên bản:** 1.0

---

## I. TỔNG QUAN PIPELINE

### CI (Continuous Integration) – Khi push hoặc tạo PR

```
Dev push code
    │
    ▼
┌─────────────────────────┐
│  Job 1: Build & Test    │ ← Vue typecheck/build + .NET build/unit
│  (ubuntu-latest)        │
└───────────┬─────────────┘
            │ ✅ Pass
            ▼
┌─────────────────────────┐
│  Job 2: Integration     │ ← Test với SQL Server + Redis containers
│  Tests                  │
└───────────┬─────────────┘
            │
            ▼
┌─────────────────────────┐
│  Job 3: Code Quality    │ ← Code analysis + format check
└─────────────────────────┘
            │
            ▼
┌─────────────────────────┐
│  Job 4: Container Build │ ← Build production Docker image
└─────────────────────────┘
```

CI còn kiểm tra bundle frontend đã đồng bộ, cấu hình không chứa LiveKit secret trong file tracked, Docker Compose hợp lệ và coverage không thấp hơn baseline hiện tại:

- Unit line coverage: tối thiểu `43%` (đo được `44,02%`).
- Integration line coverage: tối thiểu `16%` (đo được `16,50%`).

Coverage dùng `coverlet.runsettings` để loại EF migration/designer và generated source khỏi mẫu số. DTO và code nghiệp vụ vẫn được tính.

### CD (Container Publishing) – Khi merge vào main

```
Merge vào main
    │
    ▼
┌─────────────────────────┐
│  Build Docker Image     │ ← Build production image
│  Push to GHCR           │ ← Push lên GitHub Container Registry
└───────────┬─────────────┘
            │
            ▼
┌─────────────────────────┐
│  Publish Summary        │ ← Ghi image/tag đã publish
└─────────────────────────┘
```

Pipeline hiện chưa có target hosting nên **chưa tự động deploy** staging hoặc production. Khi chốt nền tảng triển khai, cần thêm job deploy thật cùng health check và rollback.

---

## II. GIT BRANCHING STRATEGY

### Branches

| Branch | Mục đích | Ai merge | Protected |
|---|---|---|---|
| `main` | Production | Tech Lead only | ✅ |
| `develop` | Integration | Sau PR review | ✅ |
| `feature/*` | Tính năng mới | Dev tạo | ❌ |
| `bugfix/*` | Sửa lỗi | Dev tạo | ❌ |
| `hotfix/*` | Fix khẩn cấp | Tech Lead | ❌ |

### Quy tắc

1. **KHÔNG push trực tiếp vào `main` hoặc `develop`**
2. Luôn tạo PR
3. PR cần ít nhất 1 reviewer approve
4. CI phải pass trước khi merge

---

## III. COMMIT CONVENTION

```
<type>: <description>

feat:     Tính năng mới
fix:      Sửa lỗi
docs:     Thay đổi documentation
style:    Formatting (không đổi logic)
refactor: Refactor code
test:     Thêm/sửa tests
chore:    Config, build, tooling
```

**Ví dụ:**
```
feat: add project creation page
fix: resolve null reference in TaskService
docs: update database design document
test: add unit tests for NotificationService
chore: update Docker compose config
```

---

## IV. SETUP BRANCH PROTECTION (QUAN TRỌNG)

### Cần làm trên GitHub Settings → Branches:

#### Branch `main`:
- ✅ Require a pull request before merging
- ✅ Require approvals: **2**
- ✅ Require status checks to pass: `Build, Unit Tests, Coverage`, `Integration Tests`, `Playwright E2E Smoke`, `Code Quality`, `Production Container Build`
- ✅ Require branches to be up to date
- ✅ Include administrators

#### Branch `develop`:
- ✅ Require a pull request before merging
- ✅ Require approvals: **1**
- ✅ Require status checks to pass: `Build, Unit Tests, Coverage`, `Integration Tests`, `Code Quality`
- ✅ Require branches to be up to date

---

## V. FILE STRUCTURE

```
.github/
├── workflows/
│   ├── ci.yml              ← CI: Build + Test + Code Quality
│   ├── cd.yml              ← CD: Docker Build + Deploy
│   └── pr-labeler.yml      ← Auto-label PRs
├── ISSUE_TEMPLATE/
│   ├── bug_report.md       ← Template báo lỗi
│   └── feature_request.md  ← Template đề xuất feature
├── pull_request_template.md ← Template PR
└── labeler.yml             ← Label config theo file path
```

---

## VI. DEV TOOLS ĐÃ CÀI ĐẶT

| File | Mục đích |
|---|---|
| `.editorconfig` | Thống nhất code style toàn team |
| `.gitignore` | Ignore build artifacts, secrets |
| `global.json` | Pin SDK version (10.0.103, roll-forward latest feature) |
| `Directory.Build.props` | Shared build properties cho tất cả projects |
| `Dockerfile` | Multi-stage build (dev + production) |
| `docker-compose.yml` | Infrastructure services |
| `scripts/check-configuration.ps1` | Chặn secret LiveKit và `.env` bị track |

---

*Cập nhật khi có thay đổi workflow.*
