# QALY v2.1 — Database Decision & SQL Server Update

**Ngày cập nhật:** 16/05/2026  
**Quyết định:** Chuyển database target từ PostgreSQL logical design sang **SQL Server 2022/2025-compatible design** để khớp codebase hiện tại và năng lực triển khai của nhóm.  
**Trạng thái:** Accepted for Graduation Implementation.

---

## 1. Kết luận ngắn

Việc nhóm trưởng muốn dùng **SQL Server** là **hợp lý và không gây hại lớn cho QALY**, miễn là tài liệu và schema được cập nhật nhất quán.

Lý do chính:

- Codebase hiện tại đã chạy theo ASP.NET Core + EF Core + SQL Server trong Docker.
- Nhóm quen SQL Server, nên giảm rủi ro triển khai, migration, debug, demo.
- QALY vẫn giữ Qdrant cho vector/RAG, nên không phụ thuộc vào khả năng vector native của SQL Server.
- Các phần flexible như settings/custom fields/audit snapshot có thể map bằng `nvarchar(max)` + `ISJSON()` hoặc bảng key-value chuẩn hóa.

**Quyết định cuối:**

```text
Primary DB cho đồ án: SQL Server 2022 Developer Edition trong Docker.
Roadmap: SQL Server 2025 nếu muốn native AI/vector về sau.
Vector DB: Qdrant vẫn giữ nguyên.
Cache/session/realtime support: Redis vẫn giữ nguyên.
File storage: Local storage ở MVP, MinIO/S3-compatible ở P1/P2.
```

---

## 2. Có nhược điểm gì nếu chuyển từ PostgreSQL sang SQL Server?

| Nhóm | Rủi ro / nhược điểm | Mức ảnh hưởng | Cách xử lý |
|---|---|---:|---|
| JSON/flexible schema | PostgreSQL `JSONB` tiện và mạnh hơn cho query/index JSON phức tạp. SQL Server dùng JSON trong `nvarchar(max)`, cần `ISJSON`, `JSON_VALUE`, computed column nếu muốn index. | Trung bình | Không lạm dụng JSON. Dữ liệu lõi normalize thành bảng; JSON chỉ dùng cho metadata/snapshot. |
| Full-text/search | PostgreSQL có ecosystem tốt cho full-text + GIN. SQL Server cũng có Full-Text Search nhưng setup khác. | Thấp–Trung bình | Search nghiệp vụ dùng SQL Server FTS; semantic search dùng Qdrant. |
| Hosting/cost | SQL Server production có licensing nếu dùng bản thương mại. | Trung bình về lâu dài | Đồ án dùng Developer/Express/local demo. Nếu SaaS thật, tính license hoặc Azure SQL. |
| Vendor lock-in | T-SQL, filtered index, computed column có thể khóa vào SQL Server. | Trung bình | Giữ business logic ở C#/EF Core, không viết stored procedure nặng. |
| Migration sau này | Nếu về sau đổi DB sẽ cần chuyển type/index/query. | Trung bình | Dùng EF Core migration, repository/service abstraction, hạn chế raw SQL. |
| Case-sensitivity/collation | SQL Server mặc định thường case-insensitive; PostgreSQL khác hành vi. | Thấp | Chốt collation từ đầu: `Vietnamese_100_CI_AI_SC_UTF8` hoặc collation phù hợp. |
| 188 bảng | Dùng SQL Server không làm giảm độ phức tạp schema. | Cao nếu code toàn bộ | Chỉ migrate P0 trước, P1/P2 để roadmap hoặc feature flag. |

---

## 3. Mapping kiểu dữ liệu PostgreSQL → SQL Server

| PostgreSQL trong tài liệu v2.0 | SQL Server v2.1 | Ghi chú |
|---|---|---|
| `UUID` | `uniqueidentifier` | Default dùng `NEWSEQUENTIALID()` cho PK nếu có thể. |
| `gen_random_uuid()` | `NEWID()` / `NEWSEQUENTIALID()` | `NEWSEQUENTIALID()` tốt hơn cho clustered PK. |
| `TIMESTAMPTZ` | `datetimeoffset(7)` | Giữ timezone-aware. Có thể dùng `datetime2(7)` nếu toàn hệ thống chuẩn UTC. |
| `NOW()` | `SYSDATETIMEOFFSET()` | Nếu dùng UTC tuyệt đối: `SYSUTCDATETIME()`. |
| `BOOLEAN` | `bit` | 0/1. |
| `TEXT` | `nvarchar(max)` | Unicode tiếng Việt. |
| `VARCHAR(n)` | `nvarchar(n)` | Ưu tiên Unicode. |
| `JSONB` | `nvarchar(max)` + `CHECK (ISJSON(col)=1)` | Thêm computed column cho field cần index. |
| `INET` | `varchar(45)` | IPv4/IPv6. |
| `BIGSERIAL` | `bigint identity(1,1)` hoặc `uniqueidentifier` | Chuẩn QALY giữ UUID cho nhất quán. |
| Partial index | Filtered index | Ví dụ: `WHERE deleted_at IS NULL`. |
| GIN index | Full-text index / computed-column index / Qdrant | Không map 1-1. |
| `ILIKE` | `LIKE` với case-insensitive collation | Hoặc normalize search column. |

---

## 4. Quy ước SQL Server cho QALY

### 4.1 Naming

- Schema: `dbo` cho MVP. P2 có thể tách `auth`, `project`, `task`, `chat`, `ai`, `audit`.
- Table: `snake_case`, số nhiều, giữ như tài liệu: `projects`, `task_items`, `chat_messages`.
- PK: `pk_<table>`.
- FK: `fk_<table>_<ref_table>`.
- Index: `ix_<table>_<columns>`.
- Unique: `uq_<table>_<columns>`.
- Check: `ck_<table>_<rule>`.

### 4.2 Cột chuẩn

Bảng nghiệp vụ:

```sql
id uniqueidentifier NOT NULL CONSTRAINT pk_table PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
tenant_id uniqueidentifier NULL,
created_at datetimeoffset(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
updated_at datetimeoffset(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
deleted_at datetimeoffset(7) NULL
```

Bảng log/audit high-write:

```sql
id uniqueidentifier NOT NULL CONSTRAINT pk_table PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
tenant_id uniqueidentifier NULL,
created_at datetimeoffset(7) NOT NULL DEFAULT SYSDATETIMEOFFSET(),
archived_at datetimeoffset(7) NULL
```

**Không soft-delete audit log.** Audit chỉ archive theo retention policy.

---

## 5. Quyết định DB cho từng domain

| Domain | SQL Server có phù hợp không? | Ghi chú |
|---|---:|---|
| System/Tenant/Auth/Org | Rất phù hợp | Relational chuẩn, RBAC dễ enforce. |
| Project/Task/Kanban/Sprint | Rất phù hợp | EF Core + SQL Server mạnh cho CRUD/workflow. |
| Evidence/File metadata | Rất phù hợp | File binary không lưu trong DB; chỉ metadata/path. |
| Chat messages | Phù hợp ở đồ án | Nếu scale thật lớn, cân nhắc partition theo `room_id/created_at`. |
| Audit logs | Phù hợp | Cần partition/archival khi dữ liệu lớn. |
| Analytics snapshot | Phù hợp | Snapshot table + index theo project/time. |
| AI knowledge metadata | Phù hợp | Vector embedding không lưu chính trong SQL Server ở MVP. |
| Vector search/RAG | Không dùng SQL Server làm chính | Dùng Qdrant. SQL Server chỉ lưu mapping/outbox. |
| Document mining | Phù hợp | Lưu job/source/result; file/text lớn lưu file storage/object storage. |

---

## 6. Scope cập nhật tài liệu v2.1

Các tài liệu v2.0 có nhắc PostgreSQL được xem là **logical schema reference**. Từ v2.1 trở đi:

- `QALY_SQLServer_Schema_Skeleton.sql` là skeleton chính.
- `QALY_Database_Decision_SQLServer_v2.1.md` là quyết định kiến trúc DB chính thức.
- `QALY_Database_Dictionary.md` vẫn dùng được nhưng cần đọc theo mapping type ở mục 3.
- Khi code, ưu tiên implement bảng P0 trước; không tạo toàn bộ 188 bảng nếu chưa có module dùng tới.

---

## 7. Câu trả lời cho giảng viên

> Nhóm chọn SQL Server vì codebase hiện tại đã dùng ASP.NET Core, EF Core và SQL Server trong Docker; nhóm cũng quen SQL Server nên giảm rủi ro triển khai. PostgreSQL có lợi thế JSONB/full-text/open-source cost, nhưng QALY không phụ thuộc vào JSONB vì dữ liệu lõi được normalize, còn vector/RAG dùng Qdrant. Do đó chuyển sang SQL Server không gây hại cho đồ án; điểm cần kiểm soát là không lạm dụng T-SQL đặc thù, chuẩn hóa type mapping, và giữ Qdrant cho AI search.

---

## 8. Acceptance Criteria cho quyết định DB

- [ ] Docker Compose chạy SQL Server ổn định.
- [ ] EF Core provider là SQL Server, migration chạy sạch từ database rỗng.
- [ ] Seed data có đủ admin, org owner, PM, developer, reviewer, customer.
- [ ] Tất cả bảng P0 có `tenant_id` hoặc cơ chế suy ra tenant qua project/org.
- [ ] Tất cả bảng P0 có `created_at`, `updated_at`, `deleted_at` nếu là nghiệp vụ.
- [ ] Audit/log table không hard-delete tùy tiện; có retention/archive policy.
- [ ] JSON column có `CHECK (ISJSON(...)=1)` nếu dùng.
- [ ] Query P0 có index: tenant/org/project/user/status/created_at.
- [ ] Qdrant sync có outbox để rebuild vector khi mất dữ liệu Qdrant.
- [ ] Tài liệu ERD/API/Test case đã đổi wording từ PostgreSQL target sang SQL Server target.
