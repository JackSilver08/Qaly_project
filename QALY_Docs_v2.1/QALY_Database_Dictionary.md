# QALY Workspace — Database Dictionary & Data Model v2.0

**Phiên bản:** v2.0 — Production/Japan-style specification  
**Ngày:** 16/05/2026  
**Phạm vi:** Dự án web quản lý dự án phần mềm cho doanh nghiệp outsource vừa và nhỏ.  
**Định hướng:** Jira + Notion + Mini Zalo + AI Assistant + Document Mining.  
**Ghi chú kiến trúc:** Tài liệu v2.0 mở rộng từ SRS/TKHT v1.0. Các phần dưới đây là target design để nhóm có thể triển khai 80–90% chức năng web trong 10–11 tuần, không bắt buộc implement toàn bộ bảng P2 nếu thiếu thời gian.

---


## 0. Quyết định thiết kế database

### 0.1 Phạm vi thiết kế
- Tổng số bảng thiết kế: **188 bảng** chia thành 12 domain triển khai.
- Mục tiêu không phải cố đạt số lượng bảng lớn, mà là **đủ bao phủ nghiệp vụ, có thể tách P0/P1/P2**, tránh schema quá cứng khiến demo khó hiểu.
- **P0**: cần implement để web chạy 80–90% core. **P1**: nâng cao nhưng nên làm nếu còn thời gian. **P2**: production/roadmap, có thể mock hoặc để tài liệu.

### 0.2 DB engine và mapping công nghệ
- Logical design ưu tiên **PostgreSQL** vì mạnh về JSONB, full-text, partition và chi phí thấp cho SaaS B2B.
- Codebase hiện tại đang dùng **ASP.NET Core + EF Core + SQL Server 2022**; nếu không đủ thời gian migration, giữ SQL Server cho demo và dùng tài liệu này như **logical schema**, mapping PostgreSQL `JSONB` → SQL Server `nvarchar(max)`/JSON, `INET` → `varchar(45)`, partial index → filtered index.
- File object nên dùng **MinIO/S3-compatible** thay vì lưu toàn bộ trong DB. Redis chỉ giữ cache/session/presence, không giữ dữ liệu nguồn.

### 0.3 Quy tắc chuẩn Nhật áp dụng
1. Bảng nghiệp vụ có `id`, `tenant_id` khi liên quan tenant, `created_at`, `updated_at`, `deleted_at`.
2. Log/audit immutable không soft delete; retention bằng archive/export.
3. Mọi FK phải có index tương ứng.
4. Tên bảng snake_case, số nhiều, rõ nghĩa nghiệp vụ.
5. JSONB chỉ dùng cho metadata linh hoạt hoặc snapshot; dữ liệu chính vẫn normalize.
6. Mọi dữ liệu customer/internal/private phải có visibility hoặc policy liên quan.
7. Mọi hành động quan trọng phải map được đến audit log, test case và owner.

### 0.4 Priority triển khai schema
| Priority | Ý nghĩa | Cách triển khai |
|---|---|---|
| P0 | Bắt buộc để core web chạy | migration thật, UI/API thật, test thật |
| P1 | Nâng cao để đồ án nổi bật | migration thật nếu có thời gian; ít nhất API/service draft |
| P2 | Production/roadmap | thiết kế bảng, có thể mock hoặc để feature flag off |

---

# D01_SYSTEM_TENANT

| Bảng | Priority | Mục đích ngắn |
|---|---|---|
| `system_settings` | P2 | Lưu dữ liệu nghiệp vụ cho system settings; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `feature_flags` | P0 | Lưu dữ liệu nghiệp vụ cho feature flags; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `system_announcements` | P2 | Lưu dữ liệu nghiệp vụ cho system announcements; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `maintenance_windows` | P2 | Lưu dữ liệu nghiệp vụ cho maintenance windows; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `rate_limit_configs` | P2 | Lưu dữ liệu nghiệp vụ cho rate limit configs; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `email_templates` | P2 | Lưu dữ liệu nghiệp vụ cho email templates; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `email_template_versions` | P2 | Lưu dữ liệu nghiệp vụ cho email template versions; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `system_health_logs` | P2 | Lưu dữ liệu nghiệp vụ cho system health logs; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `tenants` | P2 | Lưu dữ liệu nghiệp vụ cho tenants; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `tenant_settings` | P2 | Lưu dữ liệu nghiệp vụ cho tenant settings; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `tenant_subscriptions` | P2 | Lưu dữ liệu nghiệp vụ cho tenant subscriptions; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `tenant_subscription_plans` | P2 | Lưu dữ liệu nghiệp vụ cho tenant subscription plans; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `tenant_billing_history` | P2 | Lưu dữ liệu nghiệp vụ cho tenant billing history; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `tenant_feature_overrides` | P2 | Lưu dữ liệu nghiệp vụ cho tenant feature overrides; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `tenant_storage_quotas` | P2 | Lưu dữ liệu nghiệp vụ cho tenant storage quotas; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `tenant_security_policies` | P2 | Lưu dữ liệu nghiệp vụ cho tenant security policies; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |


## [D01_SYSTEM_TENANT] Tên bảng: `system_settings`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho system settings; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** High Read / Low Write  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_system_settings_created_at ON system_settings(created_at DESC) — phân trang/audit/log.`
- `idx_system_settings_status ON system_settings(status) — filter theo trạng thái.`

**Constraints:**
- CHECK ck_system_settings_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**

---

## [D01_SYSTEM_TENANT] Tên bảng: `feature_flags`

**Priority:** P0  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho feature flags; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** High Read / Low Write  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_feature_flags_created_at ON feature_flags(created_at DESC) — phân trang/audit/log.`
- `idx_feature_flags_status ON feature_flags(status) — filter theo trạng thái.`

**Constraints:**
- UNIQUE uq_feature_flags_code_scope — code unique trong scope tenant/org/project
- CHECK ck_feature_flags_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**

---

## [D01_SYSTEM_TENANT] Tên bảng: `system_announcements`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho system announcements; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_system_announcements_created_at ON system_announcements(created_at DESC) — phân trang/audit/log.`
- `idx_system_announcements_status ON system_announcements(status) — filter theo trạng thái.`

**Constraints:**
- CHECK ck_system_announcements_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**

---

## [D01_SYSTEM_TENANT] Tên bảng: `maintenance_windows`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho maintenance windows; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_maintenance_windows_created_at ON maintenance_windows(created_at DESC) — phân trang/audit/log.`
- `idx_maintenance_windows_status ON maintenance_windows(status) — filter theo trạng thái.`

**Constraints:**
- CHECK ck_maintenance_windows_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**

---

## [D01_SYSTEM_TENANT] Tên bảng: `rate_limit_configs`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho rate limit configs; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** High Read / Low Write  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_rate_limit_configs_created_at ON rate_limit_configs(created_at DESC) — phân trang/audit/log.`
- `idx_rate_limit_configs_status ON rate_limit_configs(status) — filter theo trạng thái.`

**Constraints:**
- CHECK ck_rate_limit_configs_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**

---

## [D01_SYSTEM_TENANT] Tên bảng: `email_templates`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho email templates; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** High Read / Low Write  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_email_templates_created_at ON email_templates(created_at DESC) — phân trang/audit/log.`
- `idx_email_templates_status ON email_templates(status) — filter theo trạng thái.`

**Constraints:**
- CHECK ck_email_templates_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**

---

## [D01_SYSTEM_TENANT] Tên bảng: `email_template_versions`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho email template versions; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_email_template_versions_created_at ON email_template_versions(created_at DESC) — phân trang/audit/log.`
- `idx_email_template_versions_status ON email_template_versions(status) — filter theo trạng thái.`

**Constraints:**
- CHECK ck_email_template_versions_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**

---

## [D01_SYSTEM_TENANT] Tên bảng: `system_health_logs`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho system health logs; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** High Write  
**Phân vùng:** Có — partition theo created_at tháng/quý; tenant_id là sub-filter bắt buộc.  
**Soft delete:** Không — log/audit immutable, chỉ archive theo retention policy.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo log |
| `archived_at` | TIMESTAMPTZ | NULL | NULL | Thời điểm archive theo retention nếu có |

**Indexes:**
- `idx_system_health_logs_created_at ON system_health_logs(created_at DESC) — phân trang/audit/log.`
- `idx_system_health_logs_status ON system_health_logs(status) — filter theo trạng thái.`

**Constraints:**
- CHECK ck_system_health_logs_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Log là immutable, không soft delete; chỉ archive theo retention policy.
- Mọi thao tác admin hoặc thay đổi quyền/status/deadline phải ghi log.

**Quan hệ:**

---

## [D01_SYSTEM_TENANT] Tên bảng: `tenants`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho tenants; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_tenants_tenant ON tenants(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_tenants_created_at ON tenants(created_at DESC) — phân trang/audit/log.`
- `idx_tenants_status ON tenants(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_tenants_tenant → tenants(id) ON DELETE RESTRICT
- UNIQUE uq_tenants_code_scope — code unique trong scope tenant/org/project
- CHECK ck_tenants_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants

---

## [D01_SYSTEM_TENANT] Tên bảng: `tenant_settings`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho tenant settings; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** High Read / Low Write  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_tenant_settings_tenant ON tenant_settings(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_tenant_settings_created_at ON tenant_settings(created_at DESC) — phân trang/audit/log.`
- `idx_tenant_settings_status ON tenant_settings(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_tenant_settings_tenant → tenants(id) ON DELETE RESTRICT
- CHECK ck_tenant_settings_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants

---

## [D01_SYSTEM_TENANT] Tên bảng: `tenant_subscriptions`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho tenant subscriptions; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_tenant_subscriptions_tenant ON tenant_subscriptions(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_tenant_subscriptions_created_at ON tenant_subscriptions(created_at DESC) — phân trang/audit/log.`
- `idx_tenant_subscriptions_status ON tenant_subscriptions(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_tenant_subscriptions_tenant → tenants(id) ON DELETE RESTRICT
- CHECK ck_tenant_subscriptions_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants

---

## [D01_SYSTEM_TENANT] Tên bảng: `tenant_subscription_plans`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho tenant subscription plans; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_tenant_subscription_plans_tenant ON tenant_subscription_plans(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_tenant_subscription_plans_created_at ON tenant_subscription_plans(created_at DESC) — phân trang/audit/log.`
- `idx_tenant_subscription_plans_status ON tenant_subscription_plans(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_tenant_subscription_plans_tenant → tenants(id) ON DELETE RESTRICT
- CHECK ck_tenant_subscription_plans_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants

---

## [D01_SYSTEM_TENANT] Tên bảng: `tenant_billing_history`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho tenant billing history; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** High Write  
**Phân vùng:** Có — partition theo created_at tháng/quý; tenant_id là sub-filter bắt buộc.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_tenant_billing_history_tenant ON tenant_billing_history(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_tenant_billing_history_created_at ON tenant_billing_history(created_at DESC) — phân trang/audit/log.`
- `idx_tenant_billing_history_status ON tenant_billing_history(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_tenant_billing_history_tenant → tenants(id) ON DELETE RESTRICT
- CHECK ck_tenant_billing_history_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants

---

## [D01_SYSTEM_TENANT] Tên bảng: `tenant_feature_overrides`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho tenant feature overrides; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_tenant_feature_overrides_tenant ON tenant_feature_overrides(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_tenant_feature_overrides_created_at ON tenant_feature_overrides(created_at DESC) — phân trang/audit/log.`
- `idx_tenant_feature_overrides_status ON tenant_feature_overrides(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_tenant_feature_overrides_tenant → tenants(id) ON DELETE RESTRICT
- CHECK ck_tenant_feature_overrides_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants

---

## [D01_SYSTEM_TENANT] Tên bảng: `tenant_storage_quotas`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho tenant storage quotas; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_tenant_storage_quotas_tenant ON tenant_storage_quotas(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_tenant_storage_quotas_created_at ON tenant_storage_quotas(created_at DESC) — phân trang/audit/log.`
- `idx_tenant_storage_quotas_status ON tenant_storage_quotas(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_tenant_storage_quotas_tenant → tenants(id) ON DELETE RESTRICT
- CHECK ck_tenant_storage_quotas_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants

---

## [D01_SYSTEM_TENANT] Tên bảng: `tenant_security_policies`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho tenant security policies; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_tenant_security_policies_tenant ON tenant_security_policies(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_tenant_security_policies_created_at ON tenant_security_policies(created_at DESC) — phân trang/audit/log.`
- `idx_tenant_security_policies_status ON tenant_security_policies(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_tenant_security_policies_tenant → tenants(id) ON DELETE RESTRICT
- CHECK ck_tenant_security_policies_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants

---

# D02_IDENTITY_AUTH

| Bảng | Priority | Mục đích ngắn |
|---|---|---|
| `users` | P0 | Lưu tài khoản đăng nhập và trạng thái hệ thống của người dùng. |
| `user_profiles` | P0 | Lưu dữ liệu nghiệp vụ cho user profiles; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `user_preferences` | P2 | Lưu dữ liệu nghiệp vụ cho user preferences; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `user_sessions` | P2 | Lưu dữ liệu nghiệp vụ cho user sessions; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `user_refresh_tokens` | P2 | Lưu dữ liệu nghiệp vụ cho user refresh tokens; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `user_password_history` | P2 | Lưu dữ liệu nghiệp vụ cho user password history; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `user_mfa_configs` | P2 | Lưu dữ liệu nghiệp vụ cho user mfa configs; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `user_mfa_backup_codes` | P2 | Lưu dữ liệu nghiệp vụ cho user mfa backup codes; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `user_login_history` | P2 | Lưu dữ liệu nghiệp vụ cho user login history; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `user_email_verifications` | P2 | Lưu dữ liệu nghiệp vụ cho user email verifications; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `user_password_resets` | P2 | Lưu dữ liệu nghiệp vụ cho user password resets; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `oauth_providers` | P2 | Lưu dữ liệu nghiệp vụ cho oauth providers; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `oauth_connections` | P2 | Lưu dữ liệu nghiệp vụ cho oauth connections; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `api_keys` | P2 | Lưu dữ liệu nghiệp vụ cho api keys; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `api_key_permissions` | P2 | Lưu dữ liệu nghiệp vụ cho api key permissions; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `user_devices` | P2 | Lưu dữ liệu nghiệp vụ cho user devices; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `user_notification_tokens` | P2 | Lưu dữ liệu nghiệp vụ cho user notification tokens; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |


## [D02_IDENTITY_AUTH] Tên bảng: `users`

**Priority:** P0  
**Mô tả:** Lưu tài khoản đăng nhập và trạng thái hệ thống của người dùng.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `organization_id` | UUID | NULL |  | FK organizations.id nếu bản ghi thuộc organization cụ thể |
| `user_id` | UUID | NULL |  | FK users.id nếu bản ghi gắn với một user |
| `email` | VARCHAR(320) | NOT NULL |  | Email đăng nhập, unique theo tenant/system |
| `password_hash` | TEXT | NOT NULL |  | Mật khẩu đã hash |
| `system_role` | VARCHAR(30) | NOT NULL | 'guest' | admin/customer/guest |
| `is_active` | BOOLEAN | NOT NULL | TRUE | Có cho phép đăng nhập hay không |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_users_tenant ON users(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_users_organization ON users(organization_id) — load dữ liệu theo organization.`
- `idx_users_user ON users(user_id) — load dữ liệu theo user.`
- `idx_users_created_at ON users(created_at DESC) — phân trang/audit/log.`
- `idx_users_status ON users(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_users_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_users_organization → organizations(id) ON DELETE RESTRICT
- FK fk_users_user → users(id) ON DELETE RESTRICT
- CHECK ck_users_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với organizations
- N-1 với users

---

## [D02_IDENTITY_AUTH] Tên bảng: `user_profiles`

**Priority:** P0  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho user profiles; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `user_id` | UUID | NULL |  | FK users.id nếu bản ghi gắn với một user |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_user_profiles_tenant ON user_profiles(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_user_profiles_user ON user_profiles(user_id) — load dữ liệu theo user.`
- `idx_user_profiles_created_at ON user_profiles(created_at DESC) — phân trang/audit/log.`
- `idx_user_profiles_status ON user_profiles(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_user_profiles_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_user_profiles_user → users(id) ON DELETE RESTRICT
- CHECK ck_user_profiles_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với users

---

## [D02_IDENTITY_AUTH] Tên bảng: `user_preferences`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho user preferences; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `user_id` | UUID | NULL |  | FK users.id nếu bản ghi gắn với một user |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_user_preferences_tenant ON user_preferences(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_user_preferences_user ON user_preferences(user_id) — load dữ liệu theo user.`
- `idx_user_preferences_created_at ON user_preferences(created_at DESC) — phân trang/audit/log.`
- `idx_user_preferences_status ON user_preferences(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_user_preferences_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_user_preferences_user → users(id) ON DELETE RESTRICT
- CHECK ck_user_preferences_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với users

---

## [D02_IDENTITY_AUTH] Tên bảng: `user_sessions`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho user sessions; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** High Write  
**Phân vùng:** Có — partition theo created_at tháng/quý; tenant_id là sub-filter bắt buộc.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `user_id` | UUID | NULL |  | FK users.id nếu bản ghi gắn với một user |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_user_sessions_tenant ON user_sessions(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_user_sessions_user ON user_sessions(user_id) — load dữ liệu theo user.`
- `idx_user_sessions_created_at ON user_sessions(created_at DESC) — phân trang/audit/log.`
- `idx_user_sessions_status ON user_sessions(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_user_sessions_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_user_sessions_user → users(id) ON DELETE RESTRICT
- CHECK ck_user_sessions_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với users

---

## [D02_IDENTITY_AUTH] Tên bảng: `user_refresh_tokens`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho user refresh tokens; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `user_id` | UUID | NULL |  | FK users.id nếu bản ghi gắn với một user |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_user_refresh_tokens_tenant ON user_refresh_tokens(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_user_refresh_tokens_user ON user_refresh_tokens(user_id) — load dữ liệu theo user.`
- `idx_user_refresh_tokens_created_at ON user_refresh_tokens(created_at DESC) — phân trang/audit/log.`
- `idx_user_refresh_tokens_status ON user_refresh_tokens(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_user_refresh_tokens_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_user_refresh_tokens_user → users(id) ON DELETE RESTRICT
- CHECK ck_user_refresh_tokens_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với users

---

## [D02_IDENTITY_AUTH] Tên bảng: `user_password_history`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho user password history; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** High Write  
**Phân vùng:** Có — partition theo created_at tháng/quý; tenant_id là sub-filter bắt buộc.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `user_id` | UUID | NULL |  | FK users.id nếu bản ghi gắn với một user |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_user_password_history_tenant ON user_password_history(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_user_password_history_user ON user_password_history(user_id) — load dữ liệu theo user.`
- `idx_user_password_history_created_at ON user_password_history(created_at DESC) — phân trang/audit/log.`
- `idx_user_password_history_status ON user_password_history(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_user_password_history_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_user_password_history_user → users(id) ON DELETE RESTRICT
- CHECK ck_user_password_history_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với users

---

## [D02_IDENTITY_AUTH] Tên bảng: `user_mfa_configs`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho user mfa configs; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** High Read / Low Write  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `user_id` | UUID | NULL |  | FK users.id nếu bản ghi gắn với một user |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_user_mfa_configs_tenant ON user_mfa_configs(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_user_mfa_configs_user ON user_mfa_configs(user_id) — load dữ liệu theo user.`
- `idx_user_mfa_configs_created_at ON user_mfa_configs(created_at DESC) — phân trang/audit/log.`
- `idx_user_mfa_configs_status ON user_mfa_configs(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_user_mfa_configs_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_user_mfa_configs_user → users(id) ON DELETE RESTRICT
- CHECK ck_user_mfa_configs_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với users

---

## [D02_IDENTITY_AUTH] Tên bảng: `user_mfa_backup_codes`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho user mfa backup codes; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `user_id` | UUID | NULL |  | FK users.id nếu bản ghi gắn với một user |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_user_mfa_backup_codes_tenant ON user_mfa_backup_codes(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_user_mfa_backup_codes_user ON user_mfa_backup_codes(user_id) — load dữ liệu theo user.`
- `idx_user_mfa_backup_codes_created_at ON user_mfa_backup_codes(created_at DESC) — phân trang/audit/log.`
- `idx_user_mfa_backup_codes_status ON user_mfa_backup_codes(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_user_mfa_backup_codes_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_user_mfa_backup_codes_user → users(id) ON DELETE RESTRICT
- CHECK ck_user_mfa_backup_codes_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với users

---

## [D02_IDENTITY_AUTH] Tên bảng: `user_login_history`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho user login history; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** High Write  
**Phân vùng:** Có — partition theo created_at tháng/quý; tenant_id là sub-filter bắt buộc.  
**Soft delete:** Không — log/audit immutable, chỉ archive theo retention policy.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `user_id` | UUID | NULL |  | FK users.id nếu bản ghi gắn với một user |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo log |
| `archived_at` | TIMESTAMPTZ | NULL | NULL | Thời điểm archive theo retention nếu có |

**Indexes:**
- `idx_user_login_history_tenant ON user_login_history(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_user_login_history_user ON user_login_history(user_id) — load dữ liệu theo user.`
- `idx_user_login_history_created_at ON user_login_history(created_at DESC) — phân trang/audit/log.`
- `idx_user_login_history_status ON user_login_history(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_user_login_history_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_user_login_history_user → users(id) ON DELETE RESTRICT
- CHECK ck_user_login_history_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với users

---

## [D02_IDENTITY_AUTH] Tên bảng: `user_email_verifications`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho user email verifications; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `user_id` | UUID | NULL |  | FK users.id nếu bản ghi gắn với một user |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_user_email_verifications_tenant ON user_email_verifications(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_user_email_verifications_user ON user_email_verifications(user_id) — load dữ liệu theo user.`
- `idx_user_email_verifications_created_at ON user_email_verifications(created_at DESC) — phân trang/audit/log.`
- `idx_user_email_verifications_status ON user_email_verifications(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_user_email_verifications_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_user_email_verifications_user → users(id) ON DELETE RESTRICT
- CHECK ck_user_email_verifications_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với users

---

## [D02_IDENTITY_AUTH] Tên bảng: `user_password_resets`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho user password resets; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `user_id` | UUID | NULL |  | FK users.id nếu bản ghi gắn với một user |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_user_password_resets_tenant ON user_password_resets(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_user_password_resets_user ON user_password_resets(user_id) — load dữ liệu theo user.`
- `idx_user_password_resets_created_at ON user_password_resets(created_at DESC) — phân trang/audit/log.`
- `idx_user_password_resets_status ON user_password_resets(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_user_password_resets_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_user_password_resets_user → users(id) ON DELETE RESTRICT
- CHECK ck_user_password_resets_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với users

---

## [D02_IDENTITY_AUTH] Tên bảng: `oauth_providers`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho oauth providers; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_oauth_providers_tenant ON oauth_providers(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_oauth_providers_created_at ON oauth_providers(created_at DESC) — phân trang/audit/log.`
- `idx_oauth_providers_status ON oauth_providers(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_oauth_providers_tenant → tenants(id) ON DELETE RESTRICT
- CHECK ck_oauth_providers_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants

---

## [D02_IDENTITY_AUTH] Tên bảng: `oauth_connections`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho oauth connections; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_oauth_connections_tenant ON oauth_connections(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_oauth_connections_created_at ON oauth_connections(created_at DESC) — phân trang/audit/log.`
- `idx_oauth_connections_status ON oauth_connections(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_oauth_connections_tenant → tenants(id) ON DELETE RESTRICT
- CHECK ck_oauth_connections_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants

---

## [D02_IDENTITY_AUTH] Tên bảng: `api_keys`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho api keys; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_api_keys_tenant ON api_keys(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_api_keys_created_at ON api_keys(created_at DESC) — phân trang/audit/log.`
- `idx_api_keys_status ON api_keys(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_api_keys_tenant → tenants(id) ON DELETE RESTRICT
- CHECK ck_api_keys_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants

---

## [D02_IDENTITY_AUTH] Tên bảng: `api_key_permissions`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho api key permissions; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** High Read / Low Write  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_api_key_permissions_tenant ON api_key_permissions(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_api_key_permissions_created_at ON api_key_permissions(created_at DESC) — phân trang/audit/log.`
- `idx_api_key_permissions_status ON api_key_permissions(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_api_key_permissions_tenant → tenants(id) ON DELETE RESTRICT
- CHECK ck_api_key_permissions_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants

---

## [D02_IDENTITY_AUTH] Tên bảng: `user_devices`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho user devices; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `user_id` | UUID | NULL |  | FK users.id nếu bản ghi gắn với một user |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_user_devices_tenant ON user_devices(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_user_devices_user ON user_devices(user_id) — load dữ liệu theo user.`
- `idx_user_devices_created_at ON user_devices(created_at DESC) — phân trang/audit/log.`
- `idx_user_devices_status ON user_devices(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_user_devices_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_user_devices_user → users(id) ON DELETE RESTRICT
- CHECK ck_user_devices_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với users

---

## [D02_IDENTITY_AUTH] Tên bảng: `user_notification_tokens`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho user notification tokens; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `user_id` | UUID | NULL |  | FK users.id nếu bản ghi gắn với một user |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_user_notification_tokens_tenant ON user_notification_tokens(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_user_notification_tokens_user ON user_notification_tokens(user_id) — load dữ liệu theo user.`
- `idx_user_notification_tokens_created_at ON user_notification_tokens(created_at DESC) — phân trang/audit/log.`
- `idx_user_notification_tokens_status ON user_notification_tokens(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_user_notification_tokens_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_user_notification_tokens_user → users(id) ON DELETE RESTRICT
- CHECK ck_user_notification_tokens_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với users

---

# D03_ORGANIZATION

| Bảng | Priority | Mục đích ngắn |
|---|---|---|
| `organizations` | P0 | Lưu doanh nghiệp/không gian làm việc cấp tenant mà user tham gia. |
| `organization_settings` | P2 | Lưu dữ liệu nghiệp vụ cho organization settings; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `organization_members` | P0 | Lưu dữ liệu nghiệp vụ cho organization members; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `organization_member_invitations` | P2 | Lưu dữ liệu nghiệp vụ cho organization member invitations; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `organization_roles` | P2 | Lưu dữ liệu nghiệp vụ cho organization roles; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `organization_role_permissions` | P2 | Lưu dữ liệu nghiệp vụ cho organization role permissions; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `organization_departments` | P2 | Lưu dữ liệu nghiệp vụ cho organization departments; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `organization_tags` | P2 | Lưu dữ liệu nghiệp vụ cho organization tags; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `organization_custom_fields` | P1 | Lưu dữ liệu nghiệp vụ cho organization custom fields; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `organization_custom_field_values` | P2 | Lưu dữ liệu nghiệp vụ cho organization custom field values; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `organization_working_calendars` | P2 | Lưu dữ liệu nghiệp vụ cho organization working calendars; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `organization_holidays` | P2 | Lưu dữ liệu nghiệp vụ cho organization holidays; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |


## [D03_ORGANIZATION] Tên bảng: `organizations`

**Priority:** P0  
**Mô tả:** Lưu doanh nghiệp/không gian làm việc cấp tenant mà user tham gia.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `name` | VARCHAR(200) | NOT NULL |  | Tên doanh nghiệp/workspace |
| `code` | VARCHAR(80) | NOT NULL |  | Mã unique dùng URL/import |
| `status` | VARCHAR(30) | NOT NULL | 'active' | active/suspended/archived |
| `owner_id` | UUID | NOT NULL |  | FK users.id của org_owner |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_organizations_tenant ON organizations(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_organizations_created_at ON organizations(created_at DESC) — phân trang/audit/log.`
- `idx_organizations_status ON organizations(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_organizations_tenant → tenants(id) ON DELETE RESTRICT
- UNIQUE uq_organizations_code_scope — code unique trong scope tenant/org/project
- CHECK ck_organizations_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- 1-N với organization_members, projects, organization_roles, organization_settings

---

## [D03_ORGANIZATION] Tên bảng: `organization_settings`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho organization settings; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** High Read / Low Write  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `organization_id` | UUID | NULL |  | FK organizations.id nếu bản ghi thuộc organization cụ thể |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_organization_settings_tenant ON organization_settings(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_organization_settings_organization ON organization_settings(organization_id) — load dữ liệu theo organization.`
- `idx_organization_settings_created_at ON organization_settings(created_at DESC) — phân trang/audit/log.`
- `idx_organization_settings_status ON organization_settings(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_organization_settings_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_organization_settings_organization → organizations(id) ON DELETE RESTRICT
- CHECK ck_organization_settings_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với organizations

---

## [D03_ORGANIZATION] Tên bảng: `organization_members`

**Priority:** P0  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho organization members; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `organization_id` | UUID | NULL |  | FK organizations.id nếu bản ghi thuộc organization cụ thể |
| `user_id` | UUID | NULL |  | FK users.id nếu bản ghi gắn với một user |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_organization_members_tenant ON organization_members(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_organization_members_organization ON organization_members(organization_id) — load dữ liệu theo organization.`
- `idx_organization_members_user ON organization_members(user_id) — load dữ liệu theo user.`
- `idx_organization_members_created_at ON organization_members(created_at DESC) — phân trang/audit/log.`
- `idx_organization_members_status ON organization_members(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_organization_members_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_organization_members_organization → organizations(id) ON DELETE RESTRICT
- FK fk_organization_members_user → users(id) ON DELETE RESTRICT
- CHECK ck_organization_members_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với organizations
- N-1 với users

---

## [D03_ORGANIZATION] Tên bảng: `organization_member_invitations`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho organization member invitations; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `organization_id` | UUID | NULL |  | FK organizations.id nếu bản ghi thuộc organization cụ thể |
| `user_id` | UUID | NULL |  | FK users.id nếu bản ghi gắn với một user |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_organization_member_invitations_tenant ON organization_member_invitations(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_organization_member_invitations_organization ON organization_member_invitations(organization_id) — load dữ liệu theo organization.`
- `idx_organization_member_invitations_user ON organization_member_invitations(user_id) — load dữ liệu theo user.`
- `idx_organization_member_invitations_created_at ON organization_member_invitations(created_at DESC) — phân trang/audit/log.`
- `idx_organization_member_invitations_status ON organization_member_invitations(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_organization_member_invitations_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_organization_member_invitations_organization → organizations(id) ON DELETE RESTRICT
- FK fk_organization_member_invitations_user → users(id) ON DELETE RESTRICT
- CHECK ck_organization_member_invitations_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với organizations
- N-1 với users

---

## [D03_ORGANIZATION] Tên bảng: `organization_roles`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho organization roles; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** High Read / Low Write  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `organization_id` | UUID | NULL |  | FK organizations.id nếu bản ghi thuộc organization cụ thể |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_organization_roles_tenant ON organization_roles(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_organization_roles_organization ON organization_roles(organization_id) — load dữ liệu theo organization.`
- `idx_organization_roles_created_at ON organization_roles(created_at DESC) — phân trang/audit/log.`
- `idx_organization_roles_status ON organization_roles(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_organization_roles_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_organization_roles_organization → organizations(id) ON DELETE RESTRICT
- UNIQUE uq_organization_roles_code_scope — code unique trong scope tenant/org/project
- CHECK ck_organization_roles_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với organizations

---

## [D03_ORGANIZATION] Tên bảng: `organization_role_permissions`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho organization role permissions; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** High Read / Low Write  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `organization_id` | UUID | NULL |  | FK organizations.id nếu bản ghi thuộc organization cụ thể |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_organization_role_permissions_tenant ON organization_role_permissions(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_organization_role_permissions_organization ON organization_role_permissions(organization_id) — load dữ liệu theo organization.`
- `idx_organization_role_permissions_created_at ON organization_role_permissions(created_at DESC) — phân trang/audit/log.`
- `idx_organization_role_permissions_status ON organization_role_permissions(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_organization_role_permissions_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_organization_role_permissions_organization → organizations(id) ON DELETE RESTRICT
- CHECK ck_organization_role_permissions_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với organizations

---

## [D03_ORGANIZATION] Tên bảng: `organization_departments`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho organization departments; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `organization_id` | UUID | NULL |  | FK organizations.id nếu bản ghi thuộc organization cụ thể |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_organization_departments_tenant ON organization_departments(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_organization_departments_organization ON organization_departments(organization_id) — load dữ liệu theo organization.`
- `idx_organization_departments_created_at ON organization_departments(created_at DESC) — phân trang/audit/log.`
- `idx_organization_departments_status ON organization_departments(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_organization_departments_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_organization_departments_organization → organizations(id) ON DELETE RESTRICT
- CHECK ck_organization_departments_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với organizations

---

## [D03_ORGANIZATION] Tên bảng: `organization_tags`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho organization tags; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `organization_id` | UUID | NULL |  | FK organizations.id nếu bản ghi thuộc organization cụ thể |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_organization_tags_tenant ON organization_tags(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_organization_tags_organization ON organization_tags(organization_id) — load dữ liệu theo organization.`
- `idx_organization_tags_created_at ON organization_tags(created_at DESC) — phân trang/audit/log.`
- `idx_organization_tags_status ON organization_tags(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_organization_tags_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_organization_tags_organization → organizations(id) ON DELETE RESTRICT
- CHECK ck_organization_tags_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với organizations

---

## [D03_ORGANIZATION] Tên bảng: `organization_custom_fields`

**Priority:** P1  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho organization custom fields; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `organization_id` | UUID | NULL |  | FK organizations.id nếu bản ghi thuộc organization cụ thể |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_organization_custom_fields_tenant ON organization_custom_fields(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_organization_custom_fields_organization ON organization_custom_fields(organization_id) — load dữ liệu theo organization.`
- `idx_organization_custom_fields_created_at ON organization_custom_fields(created_at DESC) — phân trang/audit/log.`
- `idx_organization_custom_fields_status ON organization_custom_fields(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_organization_custom_fields_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_organization_custom_fields_organization → organizations(id) ON DELETE RESTRICT
- CHECK ck_organization_custom_fields_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với organizations

---

## [D03_ORGANIZATION] Tên bảng: `organization_custom_field_values`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho organization custom field values; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `organization_id` | UUID | NULL |  | FK organizations.id nếu bản ghi thuộc organization cụ thể |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_organization_custom_field_values_tenant ON organization_custom_field_values(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_organization_custom_field_values_organization ON organization_custom_field_values(organization_id) — load dữ liệu theo organization.`
- `idx_organization_custom_field_values_created_at ON organization_custom_field_values(created_at DESC) — phân trang/audit/log.`
- `idx_organization_custom_field_values_status ON organization_custom_field_values(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_organization_custom_field_values_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_organization_custom_field_values_organization → organizations(id) ON DELETE RESTRICT
- CHECK ck_organization_custom_field_values_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với organizations

---

## [D03_ORGANIZATION] Tên bảng: `organization_working_calendars`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho organization working calendars; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `organization_id` | UUID | NULL |  | FK organizations.id nếu bản ghi thuộc organization cụ thể |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_organization_working_calendars_tenant ON organization_working_calendars(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_organization_working_calendars_organization ON organization_working_calendars(organization_id) — load dữ liệu theo organization.`
- `idx_organization_working_calendars_created_at ON organization_working_calendars(created_at DESC) — phân trang/audit/log.`
- `idx_organization_working_calendars_status ON organization_working_calendars(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_organization_working_calendars_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_organization_working_calendars_organization → organizations(id) ON DELETE RESTRICT
- CHECK ck_organization_working_calendars_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với organizations

---

## [D03_ORGANIZATION] Tên bảng: `organization_holidays`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho organization holidays; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `organization_id` | UUID | NULL |  | FK organizations.id nếu bản ghi thuộc organization cụ thể |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_organization_holidays_tenant ON organization_holidays(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_organization_holidays_organization ON organization_holidays(organization_id) — load dữ liệu theo organization.`
- `idx_organization_holidays_created_at ON organization_holidays(created_at DESC) — phân trang/audit/log.`
- `idx_organization_holidays_status ON organization_holidays(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_organization_holidays_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_organization_holidays_organization → organizations(id) ON DELETE RESTRICT
- CHECK ck_organization_holidays_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với organizations

---

# D04_PROJECT

| Bảng | Priority | Mục đích ngắn |
|---|---|---|
| `projects` | P0 | Lưu dự án outsource trong organization: thông tin, code, trạng thái, owner, ngày bắt đầu/kết thúc, progress và policy tổng. |
| `project_settings` | P0 | Lưu dữ liệu nghiệp vụ cho project settings; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `project_members` | P0 | Lưu dữ liệu nghiệp vụ cho project members; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `project_member_roles` | P2 | Lưu dữ liệu nghiệp vụ cho project member roles; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `project_invitations` | P2 | Lưu dữ liệu nghiệp vụ cho project invitations; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `project_labels` | P1 | Lưu dữ liệu nghiệp vụ cho project labels; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `project_custom_fields` | P1 | Lưu dữ liệu nghiệp vụ cho project custom fields; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `project_custom_field_values` | P2 | Lưu dữ liệu nghiệp vụ cho project custom field values; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `project_templates` | P2 | Lưu dữ liệu nghiệp vụ cho project templates; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `project_template_tasks` | P0 | Lưu dữ liệu nghiệp vụ cho project template tasks; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `project_health_snapshots` | P2 | Lưu dữ liệu nghiệp vụ cho project health snapshots; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `project_status_reports` | P1 | Lưu dữ liệu nghiệp vụ cho project status reports; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `project_archived_reasons` | P2 | Lưu dữ liệu nghiệp vụ cho project archived reasons; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `project_customer_access_policies` | P2 | Lưu dữ liệu nghiệp vụ cho project customer access policies; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `project_source_links` | P2 | Lưu dữ liệu nghiệp vụ cho project source links; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `project_change_requests` | P2 | Lưu dữ liệu nghiệp vụ cho project change requests; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `project_decision_logs` | P2 | Lưu dữ liệu nghiệp vụ cho project decision logs; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |


## [D04_PROJECT] Tên bảng: `projects`

**Priority:** P0  
**Mô tả:** Lưu dự án outsource trong organization: thông tin, code, trạng thái, owner, ngày bắt đầu/kết thúc, progress và policy tổng.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `organization_id` | UUID | NULL |  | FK organizations.id nếu bản ghi thuộc organization cụ thể |
| `name` | VARCHAR(200) | NOT NULL |  | Tên dự án |
| `code` | VARCHAR(80) | NOT NULL |  | Mã dự án unique trong organization |
| `description` | TEXT | NULL |  | Mô tả dự án |
| `status` | VARCHAR(30) | NOT NULL | 'active' | planned/active/on_hold/completed/archived |
| `owner_id` | UUID | NOT NULL |  | PM/owner chính |
| `start_date` | DATE | NULL |  | Ngày bắt đầu dự kiến |
| `end_date` | DATE | NULL |  | Ngày kết thúc dự kiến |
| `visibility` | VARCHAR(30) | NOT NULL | 'internal' | internal/customer_safe/public/private |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_projects_tenant ON projects(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_projects_organization ON projects(organization_id) — load dữ liệu theo organization.`
- `idx_projects_created_at ON projects(created_at DESC) — phân trang/audit/log.`
- `idx_projects_status ON projects(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_projects_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_projects_organization → organizations(id) ON DELETE RESTRICT
- UNIQUE uq_projects_code_scope — code unique trong scope tenant/org/project
- CHECK ck_projects_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với organizations
- 1-N với project_members, tasks, sprints, wiki_spaces, chat_rooms, meetings, webhooks

---

## [D04_PROJECT] Tên bảng: `project_settings`

**Priority:** P0  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho project settings; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** High Read / Low Write  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_project_settings_tenant ON project_settings(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_project_settings_project ON project_settings(project_id) — load dữ liệu theo project.`
- `idx_project_settings_created_at ON project_settings(created_at DESC) — phân trang/audit/log.`
- `idx_project_settings_status ON project_settings(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_project_settings_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_project_settings_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_project_settings_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects

---

## [D04_PROJECT] Tên bảng: `project_members`

**Priority:** P0  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho project members; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `user_id` | UUID | NULL |  | FK users.id nếu bản ghi gắn với một user |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_project_members_tenant ON project_members(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_project_members_project ON project_members(project_id) — load dữ liệu theo project.`
- `idx_project_members_user ON project_members(user_id) — load dữ liệu theo user.`
- `idx_project_members_created_at ON project_members(created_at DESC) — phân trang/audit/log.`
- `idx_project_members_status ON project_members(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_project_members_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_project_members_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- FK fk_project_members_user → users(id) ON DELETE RESTRICT
- CHECK ck_project_members_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects
- N-1 với users

---

## [D04_PROJECT] Tên bảng: `project_member_roles`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho project member roles; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** High Read / Low Write  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `user_id` | UUID | NULL |  | FK users.id nếu bản ghi gắn với một user |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_project_member_roles_tenant ON project_member_roles(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_project_member_roles_project ON project_member_roles(project_id) — load dữ liệu theo project.`
- `idx_project_member_roles_user ON project_member_roles(user_id) — load dữ liệu theo user.`
- `idx_project_member_roles_created_at ON project_member_roles(created_at DESC) — phân trang/audit/log.`
- `idx_project_member_roles_status ON project_member_roles(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_project_member_roles_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_project_member_roles_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- FK fk_project_member_roles_user → users(id) ON DELETE RESTRICT
- UNIQUE uq_project_member_roles_code_scope — code unique trong scope tenant/org/project
- CHECK ck_project_member_roles_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects
- N-1 với users

---

## [D04_PROJECT] Tên bảng: `project_invitations`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho project invitations; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_project_invitations_tenant ON project_invitations(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_project_invitations_project ON project_invitations(project_id) — load dữ liệu theo project.`
- `idx_project_invitations_created_at ON project_invitations(created_at DESC) — phân trang/audit/log.`
- `idx_project_invitations_status ON project_invitations(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_project_invitations_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_project_invitations_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_project_invitations_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects

---

## [D04_PROJECT] Tên bảng: `project_labels`

**Priority:** P1  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho project labels; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_project_labels_tenant ON project_labels(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_project_labels_project ON project_labels(project_id) — load dữ liệu theo project.`
- `idx_project_labels_created_at ON project_labels(created_at DESC) — phân trang/audit/log.`
- `idx_project_labels_status ON project_labels(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_project_labels_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_project_labels_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_project_labels_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects

---

## [D04_PROJECT] Tên bảng: `project_custom_fields`

**Priority:** P1  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho project custom fields; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_project_custom_fields_tenant ON project_custom_fields(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_project_custom_fields_project ON project_custom_fields(project_id) — load dữ liệu theo project.`
- `idx_project_custom_fields_created_at ON project_custom_fields(created_at DESC) — phân trang/audit/log.`
- `idx_project_custom_fields_status ON project_custom_fields(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_project_custom_fields_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_project_custom_fields_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_project_custom_fields_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects

---

## [D04_PROJECT] Tên bảng: `project_custom_field_values`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho project custom field values; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_project_custom_field_values_tenant ON project_custom_field_values(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_project_custom_field_values_project ON project_custom_field_values(project_id) — load dữ liệu theo project.`
- `idx_project_custom_field_values_created_at ON project_custom_field_values(created_at DESC) — phân trang/audit/log.`
- `idx_project_custom_field_values_status ON project_custom_field_values(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_project_custom_field_values_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_project_custom_field_values_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_project_custom_field_values_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects

---

## [D04_PROJECT] Tên bảng: `project_templates`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho project templates; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** High Read / Low Write  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `organization_id` | UUID | NULL |  | FK organizations.id nếu bản ghi thuộc organization cụ thể |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_project_templates_tenant ON project_templates(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_project_templates_organization ON project_templates(organization_id) — load dữ liệu theo organization.`
- `idx_project_templates_project ON project_templates(project_id) — load dữ liệu theo project.`
- `idx_project_templates_created_at ON project_templates(created_at DESC) — phân trang/audit/log.`
- `idx_project_templates_status ON project_templates(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_project_templates_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_project_templates_organization → organizations(id) ON DELETE RESTRICT
- FK fk_project_templates_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_project_templates_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với organizations
- N-1 với projects

---

## [D04_PROJECT] Tên bảng: `project_template_tasks`

**Priority:** P0  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho project template tasks; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `organization_id` | UUID | NULL |  | FK organizations.id nếu bản ghi thuộc organization cụ thể |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `task_id` | UUID | NULL |  | FK tasks.id nếu bản ghi gắn với task |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_project_template_tasks_tenant ON project_template_tasks(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_project_template_tasks_organization ON project_template_tasks(organization_id) — load dữ liệu theo organization.`
- `idx_project_template_tasks_project ON project_template_tasks(project_id) — load dữ liệu theo project.`
- `idx_project_template_tasks_task ON project_template_tasks(task_id) — load dữ liệu theo task.`
- `idx_project_template_tasks_created_at ON project_template_tasks(created_at DESC) — phân trang/audit/log.`
- `idx_project_template_tasks_status ON project_template_tasks(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_project_template_tasks_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_project_template_tasks_organization → organizations(id) ON DELETE RESTRICT
- FK fk_project_template_tasks_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- FK fk_project_template_tasks_task → tasks(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_project_template_tasks_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với organizations
- N-1 với projects
- N-1 với tasks

---

## [D04_PROJECT] Tên bảng: `project_health_snapshots`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho project health snapshots; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** High Write  
**Phân vùng:** Có — partition theo created_at tháng/quý; tenant_id là sub-filter bắt buộc.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_project_health_snapshots_tenant ON project_health_snapshots(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_project_health_snapshots_project ON project_health_snapshots(project_id) — load dữ liệu theo project.`
- `idx_project_health_snapshots_created_at ON project_health_snapshots(created_at DESC) — phân trang/audit/log.`
- `idx_project_health_snapshots_status ON project_health_snapshots(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_project_health_snapshots_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_project_health_snapshots_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_project_health_snapshots_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects

---

## [D04_PROJECT] Tên bảng: `project_status_reports`

**Priority:** P1  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho project status reports; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_project_status_reports_tenant ON project_status_reports(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_project_status_reports_project ON project_status_reports(project_id) — load dữ liệu theo project.`
- `idx_project_status_reports_created_at ON project_status_reports(created_at DESC) — phân trang/audit/log.`
- `idx_project_status_reports_status ON project_status_reports(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_project_status_reports_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_project_status_reports_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_project_status_reports_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects

---

## [D04_PROJECT] Tên bảng: `project_archived_reasons`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho project archived reasons; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_project_archived_reasons_tenant ON project_archived_reasons(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_project_archived_reasons_project ON project_archived_reasons(project_id) — load dữ liệu theo project.`
- `idx_project_archived_reasons_created_at ON project_archived_reasons(created_at DESC) — phân trang/audit/log.`
- `idx_project_archived_reasons_status ON project_archived_reasons(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_project_archived_reasons_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_project_archived_reasons_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_project_archived_reasons_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects

---

## [D04_PROJECT] Tên bảng: `project_customer_access_policies`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho project customer access policies; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_project_customer_access_policies_tenant ON project_customer_access_policies(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_project_customer_access_policies_project ON project_customer_access_policies(project_id) — load dữ liệu theo project.`
- `idx_project_customer_access_policies_created_at ON project_customer_access_policies(created_at DESC) — phân trang/audit/log.`
- `idx_project_customer_access_policies_status ON project_customer_access_policies(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_project_customer_access_policies_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_project_customer_access_policies_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_project_customer_access_policies_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects

---

## [D04_PROJECT] Tên bảng: `project_source_links`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho project source links; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_project_source_links_tenant ON project_source_links(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_project_source_links_project ON project_source_links(project_id) — load dữ liệu theo project.`
- `idx_project_source_links_created_at ON project_source_links(created_at DESC) — phân trang/audit/log.`
- `idx_project_source_links_status ON project_source_links(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_project_source_links_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_project_source_links_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_project_source_links_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects

---

## [D04_PROJECT] Tên bảng: `project_change_requests`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho project change requests; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_project_change_requests_tenant ON project_change_requests(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_project_change_requests_project ON project_change_requests(project_id) — load dữ liệu theo project.`
- `idx_project_change_requests_created_at ON project_change_requests(created_at DESC) — phân trang/audit/log.`
- `idx_project_change_requests_status ON project_change_requests(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_project_change_requests_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_project_change_requests_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_project_change_requests_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects

---

## [D04_PROJECT] Tên bảng: `project_decision_logs`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho project decision logs; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** High Write  
**Phân vùng:** Có — partition theo created_at tháng/quý; tenant_id là sub-filter bắt buộc.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_project_decision_logs_tenant ON project_decision_logs(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_project_decision_logs_project ON project_decision_logs(project_id) — load dữ liệu theo project.`
- `idx_project_decision_logs_created_at ON project_decision_logs(created_at DESC) — phân trang/audit/log.`
- `idx_project_decision_logs_status ON project_decision_logs(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_project_decision_logs_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_project_decision_logs_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_project_decision_logs_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Log là immutable, không soft delete; chỉ archive theo retention policy.
- Mọi thao tác admin hoặc thay đổi quyền/status/deadline phải ghi log.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects

---

# D05_TASK_WORKFLOW

| Bảng | Priority | Mục đích ngắn |
|---|---|---|
| `tasks` | P0 | Lưu thực thể công việc trung tâm: mô tả, trạng thái, priority, visibility, deadline, progress weight và liên kết project/sprint/parent task. |
| `task_assignments` | P0 | Lưu dữ liệu nghiệp vụ cho task assignments; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `task_labels` | P1 | Lưu dữ liệu nghiệp vụ cho task labels; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `task_watchers` | P2 | Lưu dữ liệu nghiệp vụ cho task watchers; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `task_custom_field_values` | P2 | Lưu dữ liệu nghiệp vụ cho task custom field values; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `task_checklists` | P1 | Lưu dữ liệu nghiệp vụ cho task checklists; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `task_checklist_items` | P1 | Lưu dữ liệu nghiệp vụ cho task checklist items; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `task_time_logs` | P1 | Lưu dữ liệu nghiệp vụ cho task time logs; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `task_estimated_times` | P2 | Lưu dữ liệu nghiệp vụ cho task estimated times; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `task_actual_times` | P2 | Lưu dữ liệu nghiệp vụ cho task actual times; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `task_comments_count_cache` | P1 | Lưu dữ liệu nghiệp vụ cho task comments count cache; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `task_activity_logs` | P2 | Lưu dữ liệu nghiệp vụ cho task activity logs; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `task_subtasks` | P0 | Lưu dữ liệu nghiệp vụ cho task subtasks; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `workflow_definitions` | P1 | Lưu dữ liệu nghiệp vụ cho workflow definitions; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `workflow_states` | P1 | Lưu dữ liệu nghiệp vụ cho workflow states; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `workflow_transitions` | P1 | Lưu dữ liệu nghiệp vụ cho workflow transitions; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `workflow_transition_conditions` | P1 | Lưu dữ liệu nghiệp vụ cho workflow transition conditions; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `workflow_transition_actions` | P1 | Lưu dữ liệu nghiệp vụ cho workflow transition actions; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `task_workflow_states` | P1 | Lưu dữ liệu nghiệp vụ cho task workflow states; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `task_status_change_requests` | P0 | Lưu yêu cầu chuyển trạng thái của assignee, đặc biệt khi cần PM/reviewer duyệt. |
| `task_status_change_approvals` | P1 | Lưu dữ liệu nghiệp vụ cho task status change approvals; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `task_review_cycles` | P1 | Lưu dữ liệu nghiệp vụ cho task review cycles; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `task_review_feedbacks` | P1 | Lưu dữ liệu nghiệp vụ cho task review feedbacks; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |


## [D05_TASK_WORKFLOW] Tên bảng: `tasks`

**Priority:** P0  
**Mô tả:** Lưu thực thể công việc trung tâm: mô tả, trạng thái, priority, visibility, deadline, progress weight và liên kết project/sprint/parent task.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `title` | VARCHAR(300) | NOT NULL |  | Tiêu đề task |
| `description_md` | TEXT | NULL |  | Mô tả Markdown đã sanitize khi render |
| `status` | VARCHAR(40) | NOT NULL | 'todo' | todo/in_progress/on_hold/in_review/done/cancelled |
| `priority` | VARCHAR(20) | NOT NULL | 'medium' | low/medium/high/critical |
| `visibility` | VARCHAR(30) | NOT NULL | 'internal' | public/customer_safe/internal/private |
| `due_at` | TIMESTAMPTZ | NULL |  | Deadline |
| `start_at` | TIMESTAMPTZ | NULL |  | Ngày bắt đầu |
| `estimated_hours` | NUMERIC(8,2) | NULL |  | Estimate |
| `actual_hours` | NUMERIC(8,2) | NULL |  | Actual |
| `weight` | NUMERIC(8,2) | NOT NULL | 1 | Trọng số tính tiến độ |
| `contributes_to_progress` | BOOLEAN | NOT NULL | TRUE | Có tính vào % project không |
| `parent_task_id` | UUID | NULL |  | FK tasks.id cho subtask/epic |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_tasks_tenant ON tasks(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_tasks_project ON tasks(project_id) — load dữ liệu theo project.`
- `idx_tasks_created_at ON tasks(created_at DESC) — phân trang/audit/log.`
- `idx_tasks_status ON tasks(status) — filter theo trạng thái.`
- `idx_tasks_project_status_order ON tasks(project_id, status, updated_at DESC) — tải Kanban/list.`
- `idx_tasks_due_open ON tasks(project_id, due_at) WHERE status NOT IN ('done','cancelled') — quét deadline.`

**Constraints:**
- FK fk_tasks_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_tasks_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_tasks_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Assignee phải thuộc project_members.
- Task không được Done nếu workflow yêu cầu approval và chưa có approval.
- Private task chỉ trả masked metadata cho user không đủ quyền.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects
- 1-N với task_assignments, task_evidences, task_status_change_requests, comments, task_dependencies

---

## [D05_TASK_WORKFLOW] Tên bảng: `task_assignments`

**Priority:** P0  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho task assignments; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `task_id` | UUID | NULL |  | FK tasks.id nếu bản ghi gắn với task |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_task_assignments_tenant ON task_assignments(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_task_assignments_project ON task_assignments(project_id) — load dữ liệu theo project.`
- `idx_task_assignments_task ON task_assignments(task_id) — load dữ liệu theo task.`
- `idx_task_assignments_created_at ON task_assignments(created_at DESC) — phân trang/audit/log.`
- `idx_task_assignments_status ON task_assignments(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_task_assignments_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_task_assignments_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- FK fk_task_assignments_task → tasks(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_task_assignments_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects
- N-1 với tasks

---

## [D05_TASK_WORKFLOW] Tên bảng: `task_labels`

**Priority:** P1  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho task labels; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `task_id` | UUID | NULL |  | FK tasks.id nếu bản ghi gắn với task |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_task_labels_tenant ON task_labels(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_task_labels_project ON task_labels(project_id) — load dữ liệu theo project.`
- `idx_task_labels_task ON task_labels(task_id) — load dữ liệu theo task.`
- `idx_task_labels_created_at ON task_labels(created_at DESC) — phân trang/audit/log.`
- `idx_task_labels_status ON task_labels(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_task_labels_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_task_labels_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- FK fk_task_labels_task → tasks(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_task_labels_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects
- N-1 với tasks

---

## [D05_TASK_WORKFLOW] Tên bảng: `task_watchers`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho task watchers; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `task_id` | UUID | NULL |  | FK tasks.id nếu bản ghi gắn với task |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_task_watchers_tenant ON task_watchers(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_task_watchers_project ON task_watchers(project_id) — load dữ liệu theo project.`
- `idx_task_watchers_task ON task_watchers(task_id) — load dữ liệu theo task.`
- `idx_task_watchers_created_at ON task_watchers(created_at DESC) — phân trang/audit/log.`
- `idx_task_watchers_status ON task_watchers(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_task_watchers_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_task_watchers_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- FK fk_task_watchers_task → tasks(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_task_watchers_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects
- N-1 với tasks

---

## [D05_TASK_WORKFLOW] Tên bảng: `task_custom_field_values`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho task custom field values; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `task_id` | UUID | NULL |  | FK tasks.id nếu bản ghi gắn với task |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_task_custom_field_values_tenant ON task_custom_field_values(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_task_custom_field_values_project ON task_custom_field_values(project_id) — load dữ liệu theo project.`
- `idx_task_custom_field_values_task ON task_custom_field_values(task_id) — load dữ liệu theo task.`
- `idx_task_custom_field_values_created_at ON task_custom_field_values(created_at DESC) — phân trang/audit/log.`
- `idx_task_custom_field_values_status ON task_custom_field_values(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_task_custom_field_values_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_task_custom_field_values_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- FK fk_task_custom_field_values_task → tasks(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_task_custom_field_values_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects
- N-1 với tasks

---

## [D05_TASK_WORKFLOW] Tên bảng: `task_checklists`

**Priority:** P1  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho task checklists; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `task_id` | UUID | NULL |  | FK tasks.id nếu bản ghi gắn với task |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_task_checklists_tenant ON task_checklists(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_task_checklists_project ON task_checklists(project_id) — load dữ liệu theo project.`
- `idx_task_checklists_task ON task_checklists(task_id) — load dữ liệu theo task.`
- `idx_task_checklists_created_at ON task_checklists(created_at DESC) — phân trang/audit/log.`
- `idx_task_checklists_status ON task_checklists(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_task_checklists_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_task_checklists_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- FK fk_task_checklists_task → tasks(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_task_checklists_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects
- N-1 với tasks

---

## [D05_TASK_WORKFLOW] Tên bảng: `task_checklist_items`

**Priority:** P1  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho task checklist items; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `task_id` | UUID | NULL |  | FK tasks.id nếu bản ghi gắn với task |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_task_checklist_items_tenant ON task_checklist_items(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_task_checklist_items_project ON task_checklist_items(project_id) — load dữ liệu theo project.`
- `idx_task_checklist_items_task ON task_checklist_items(task_id) — load dữ liệu theo task.`
- `idx_task_checklist_items_created_at ON task_checklist_items(created_at DESC) — phân trang/audit/log.`
- `idx_task_checklist_items_status ON task_checklist_items(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_task_checklist_items_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_task_checklist_items_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- FK fk_task_checklist_items_task → tasks(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_task_checklist_items_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects
- N-1 với tasks

---

## [D05_TASK_WORKFLOW] Tên bảng: `task_time_logs`

**Priority:** P1  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho task time logs; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** High Write  
**Phân vùng:** Có — partition theo created_at tháng/quý; tenant_id là sub-filter bắt buộc.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `task_id` | UUID | NULL |  | FK tasks.id nếu bản ghi gắn với task |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_task_time_logs_tenant ON task_time_logs(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_task_time_logs_project ON task_time_logs(project_id) — load dữ liệu theo project.`
- `idx_task_time_logs_task ON task_time_logs(task_id) — load dữ liệu theo task.`
- `idx_task_time_logs_created_at ON task_time_logs(created_at DESC) — phân trang/audit/log.`
- `idx_task_time_logs_status ON task_time_logs(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_task_time_logs_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_task_time_logs_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- FK fk_task_time_logs_task → tasks(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_task_time_logs_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Log là immutable, không soft delete; chỉ archive theo retention policy.
- Mọi thao tác admin hoặc thay đổi quyền/status/deadline phải ghi log.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects
- N-1 với tasks

---

## [D05_TASK_WORKFLOW] Tên bảng: `task_estimated_times`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho task estimated times; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `task_id` | UUID | NULL |  | FK tasks.id nếu bản ghi gắn với task |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_task_estimated_times_tenant ON task_estimated_times(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_task_estimated_times_project ON task_estimated_times(project_id) — load dữ liệu theo project.`
- `idx_task_estimated_times_task ON task_estimated_times(task_id) — load dữ liệu theo task.`
- `idx_task_estimated_times_created_at ON task_estimated_times(created_at DESC) — phân trang/audit/log.`
- `idx_task_estimated_times_status ON task_estimated_times(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_task_estimated_times_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_task_estimated_times_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- FK fk_task_estimated_times_task → tasks(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_task_estimated_times_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects
- N-1 với tasks

---

## [D05_TASK_WORKFLOW] Tên bảng: `task_actual_times`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho task actual times; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `task_id` | UUID | NULL |  | FK tasks.id nếu bản ghi gắn với task |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_task_actual_times_tenant ON task_actual_times(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_task_actual_times_project ON task_actual_times(project_id) — load dữ liệu theo project.`
- `idx_task_actual_times_task ON task_actual_times(task_id) — load dữ liệu theo task.`
- `idx_task_actual_times_created_at ON task_actual_times(created_at DESC) — phân trang/audit/log.`
- `idx_task_actual_times_status ON task_actual_times(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_task_actual_times_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_task_actual_times_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- FK fk_task_actual_times_task → tasks(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_task_actual_times_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects
- N-1 với tasks

---

## [D05_TASK_WORKFLOW] Tên bảng: `task_comments_count_cache`

**Priority:** P1  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho task comments count cache; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** High Write  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `task_id` | UUID | NULL |  | FK tasks.id nếu bản ghi gắn với task |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_task_comments_count_cache_tenant ON task_comments_count_cache(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_task_comments_count_cache_project ON task_comments_count_cache(project_id) — load dữ liệu theo project.`
- `idx_task_comments_count_cache_task ON task_comments_count_cache(task_id) — load dữ liệu theo task.`
- `idx_task_comments_count_cache_created_at ON task_comments_count_cache(created_at DESC) — phân trang/audit/log.`
- `idx_task_comments_count_cache_status ON task_comments_count_cache(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_task_comments_count_cache_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_task_comments_count_cache_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- FK fk_task_comments_count_cache_task → tasks(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_task_comments_count_cache_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects
- N-1 với tasks

---

## [D05_TASK_WORKFLOW] Tên bảng: `task_activity_logs`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho task activity logs; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** High Write  
**Phân vùng:** Có — partition theo created_at tháng/quý; tenant_id là sub-filter bắt buộc.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `task_id` | UUID | NULL |  | FK tasks.id nếu bản ghi gắn với task |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_task_activity_logs_tenant ON task_activity_logs(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_task_activity_logs_project ON task_activity_logs(project_id) — load dữ liệu theo project.`
- `idx_task_activity_logs_task ON task_activity_logs(task_id) — load dữ liệu theo task.`
- `idx_task_activity_logs_created_at ON task_activity_logs(created_at DESC) — phân trang/audit/log.`
- `idx_task_activity_logs_status ON task_activity_logs(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_task_activity_logs_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_task_activity_logs_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- FK fk_task_activity_logs_task → tasks(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_task_activity_logs_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Log là immutable, không soft delete; chỉ archive theo retention policy.
- Mọi thao tác admin hoặc thay đổi quyền/status/deadline phải ghi log.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects
- N-1 với tasks

---

## [D05_TASK_WORKFLOW] Tên bảng: `task_subtasks`

**Priority:** P0  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho task subtasks; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `task_id` | UUID | NULL |  | FK tasks.id nếu bản ghi gắn với task |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_task_subtasks_tenant ON task_subtasks(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_task_subtasks_project ON task_subtasks(project_id) — load dữ liệu theo project.`
- `idx_task_subtasks_task ON task_subtasks(task_id) — load dữ liệu theo task.`
- `idx_task_subtasks_created_at ON task_subtasks(created_at DESC) — phân trang/audit/log.`
- `idx_task_subtasks_status ON task_subtasks(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_task_subtasks_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_task_subtasks_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- FK fk_task_subtasks_task → tasks(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_task_subtasks_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects
- N-1 với tasks

---

## [D05_TASK_WORKFLOW] Tên bảng: `workflow_definitions`

**Priority:** P1  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho workflow definitions; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_workflow_definitions_tenant ON workflow_definitions(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_workflow_definitions_created_at ON workflow_definitions(created_at DESC) — phân trang/audit/log.`
- `idx_workflow_definitions_status ON workflow_definitions(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_workflow_definitions_tenant → tenants(id) ON DELETE RESTRICT
- CHECK ck_workflow_definitions_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants

---

## [D05_TASK_WORKFLOW] Tên bảng: `workflow_states`

**Priority:** P1  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho workflow states; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_workflow_states_tenant ON workflow_states(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_workflow_states_created_at ON workflow_states(created_at DESC) — phân trang/audit/log.`
- `idx_workflow_states_status ON workflow_states(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_workflow_states_tenant → tenants(id) ON DELETE RESTRICT
- CHECK ck_workflow_states_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants

---

## [D05_TASK_WORKFLOW] Tên bảng: `workflow_transitions`

**Priority:** P1  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho workflow transitions; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_workflow_transitions_tenant ON workflow_transitions(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_workflow_transitions_created_at ON workflow_transitions(created_at DESC) — phân trang/audit/log.`
- `idx_workflow_transitions_status ON workflow_transitions(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_workflow_transitions_tenant → tenants(id) ON DELETE RESTRICT
- CHECK ck_workflow_transitions_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants

---

## [D05_TASK_WORKFLOW] Tên bảng: `workflow_transition_conditions`

**Priority:** P1  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho workflow transition conditions; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_workflow_transition_conditions_tenant ON workflow_transition_conditions(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_workflow_transition_conditions_created_at ON workflow_transition_conditions(created_at DESC) — phân trang/audit/log.`
- `idx_workflow_transition_conditions_status ON workflow_transition_conditions(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_workflow_transition_conditions_tenant → tenants(id) ON DELETE RESTRICT
- CHECK ck_workflow_transition_conditions_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants

---

## [D05_TASK_WORKFLOW] Tên bảng: `workflow_transition_actions`

**Priority:** P1  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho workflow transition actions; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_workflow_transition_actions_tenant ON workflow_transition_actions(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_workflow_transition_actions_created_at ON workflow_transition_actions(created_at DESC) — phân trang/audit/log.`
- `idx_workflow_transition_actions_status ON workflow_transition_actions(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_workflow_transition_actions_tenant → tenants(id) ON DELETE RESTRICT
- CHECK ck_workflow_transition_actions_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants

---

## [D05_TASK_WORKFLOW] Tên bảng: `task_workflow_states`

**Priority:** P1  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho task workflow states; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `task_id` | UUID | NULL |  | FK tasks.id nếu bản ghi gắn với task |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_task_workflow_states_tenant ON task_workflow_states(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_task_workflow_states_project ON task_workflow_states(project_id) — load dữ liệu theo project.`
- `idx_task_workflow_states_task ON task_workflow_states(task_id) — load dữ liệu theo task.`
- `idx_task_workflow_states_created_at ON task_workflow_states(created_at DESC) — phân trang/audit/log.`
- `idx_task_workflow_states_status ON task_workflow_states(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_task_workflow_states_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_task_workflow_states_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- FK fk_task_workflow_states_task → tasks(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_task_workflow_states_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects
- N-1 với tasks

---

## [D05_TASK_WORKFLOW] Tên bảng: `task_status_change_requests`

**Priority:** P0  
**Mô tả:** Lưu yêu cầu chuyển trạng thái của assignee, đặc biệt khi cần PM/reviewer duyệt.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `task_id` | UUID | NULL |  | FK tasks.id nếu bản ghi gắn với task |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_task_status_change_requests_tenant ON task_status_change_requests(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_task_status_change_requests_project ON task_status_change_requests(project_id) — load dữ liệu theo project.`
- `idx_task_status_change_requests_task ON task_status_change_requests(task_id) — load dữ liệu theo task.`
- `idx_task_status_change_requests_created_at ON task_status_change_requests(created_at DESC) — phân trang/audit/log.`
- `idx_task_status_change_requests_status ON task_status_change_requests(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_task_status_change_requests_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_task_status_change_requests_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- FK fk_task_status_change_requests_task → tasks(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_task_status_change_requests_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects
- N-1 với tasks

---

## [D05_TASK_WORKFLOW] Tên bảng: `task_status_change_approvals`

**Priority:** P1  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho task status change approvals; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `task_id` | UUID | NULL |  | FK tasks.id nếu bản ghi gắn với task |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_task_status_change_approvals_tenant ON task_status_change_approvals(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_task_status_change_approvals_project ON task_status_change_approvals(project_id) — load dữ liệu theo project.`
- `idx_task_status_change_approvals_task ON task_status_change_approvals(task_id) — load dữ liệu theo task.`
- `idx_task_status_change_approvals_created_at ON task_status_change_approvals(created_at DESC) — phân trang/audit/log.`
- `idx_task_status_change_approvals_status ON task_status_change_approvals(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_task_status_change_approvals_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_task_status_change_approvals_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- FK fk_task_status_change_approvals_task → tasks(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_task_status_change_approvals_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects
- N-1 với tasks

---

## [D05_TASK_WORKFLOW] Tên bảng: `task_review_cycles`

**Priority:** P1  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho task review cycles; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `task_id` | UUID | NULL |  | FK tasks.id nếu bản ghi gắn với task |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_task_review_cycles_tenant ON task_review_cycles(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_task_review_cycles_project ON task_review_cycles(project_id) — load dữ liệu theo project.`
- `idx_task_review_cycles_task ON task_review_cycles(task_id) — load dữ liệu theo task.`
- `idx_task_review_cycles_created_at ON task_review_cycles(created_at DESC) — phân trang/audit/log.`
- `idx_task_review_cycles_status ON task_review_cycles(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_task_review_cycles_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_task_review_cycles_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- FK fk_task_review_cycles_task → tasks(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_task_review_cycles_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects
- N-1 với tasks

---

## [D05_TASK_WORKFLOW] Tên bảng: `task_review_feedbacks`

**Priority:** P1  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho task review feedbacks; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `task_id` | UUID | NULL |  | FK tasks.id nếu bản ghi gắn với task |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_task_review_feedbacks_tenant ON task_review_feedbacks(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_task_review_feedbacks_project ON task_review_feedbacks(project_id) — load dữ liệu theo project.`
- `idx_task_review_feedbacks_task ON task_review_feedbacks(task_id) — load dữ liệu theo task.`
- `idx_task_review_feedbacks_created_at ON task_review_feedbacks(created_at DESC) — phân trang/audit/log.`
- `idx_task_review_feedbacks_status ON task_review_feedbacks(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_task_review_feedbacks_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_task_review_feedbacks_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- FK fk_task_review_feedbacks_task → tasks(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_task_review_feedbacks_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects
- N-1 với tasks

---

# D06_EVIDENCE_APPROVAL_DEPENDENCY

| Bảng | Priority | Mục đích ngắn |
|---|---|---|
| `task_evidences` | P0 | Lưu bằng chứng hoàn thành task: link PR, file, ảnh, URL deploy, mô tả kết quả. |
| `task_evidence_attachments` | P2 | Lưu dữ liệu nghiệp vụ cho task evidence attachments; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `task_evidence_reviews` | P1 | Lưu dữ liệu nghiệp vụ cho task evidence reviews; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `task_evidence_review_comments` | P1 | Lưu dữ liệu nghiệp vụ cho task evidence review comments; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `evidence_approval_audit_logs` | P0 | Lưu dữ liệu nghiệp vụ cho evidence approval audit logs; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `task_dependencies` | P0 | Lưu dữ liệu nghiệp vụ cho task dependencies; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `task_dependency_types` | P2 | Lưu dữ liệu nghiệp vụ cho task dependency types; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `dependency_impact_analysis` | P2 | Lưu dữ liệu nghiệp vụ cho dependency impact analysis; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `task_blockers` | P2 | Lưu dữ liệu nghiệp vụ cho task blockers; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `task_risk_flags` | P2 | Lưu dữ liệu nghiệp vụ cho task risk flags; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |


## [D06_EVIDENCE_APPROVAL_DEPENDENCY] Tên bảng: `task_evidences`

**Priority:** P0  
**Mô tả:** Lưu bằng chứng hoàn thành task: link PR, file, ảnh, URL deploy, mô tả kết quả.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `task_id` | UUID | NULL |  | FK tasks.id nếu bản ghi gắn với task |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_task_evidences_tenant ON task_evidences(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_task_evidences_project ON task_evidences(project_id) — load dữ liệu theo project.`
- `idx_task_evidences_task ON task_evidences(task_id) — load dữ liệu theo task.`
- `idx_task_evidences_created_at ON task_evidences(created_at DESC) — phân trang/audit/log.`
- `idx_task_evidences_status ON task_evidences(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_task_evidences_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_task_evidences_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- FK fk_task_evidences_task → tasks(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_task_evidences_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Evidence bắt buộc trước khi submit review nếu project_settings.evidence_required = true.
- File evidence kế thừa visibility của task trừ khi PM đổi.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects
- N-1 với tasks

---

## [D06_EVIDENCE_APPROVAL_DEPENDENCY] Tên bảng: `task_evidence_attachments`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho task evidence attachments; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `task_id` | UUID | NULL |  | FK tasks.id nếu bản ghi gắn với task |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_task_evidence_attachments_tenant ON task_evidence_attachments(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_task_evidence_attachments_project ON task_evidence_attachments(project_id) — load dữ liệu theo project.`
- `idx_task_evidence_attachments_task ON task_evidence_attachments(task_id) — load dữ liệu theo task.`
- `idx_task_evidence_attachments_created_at ON task_evidence_attachments(created_at DESC) — phân trang/audit/log.`
- `idx_task_evidence_attachments_status ON task_evidence_attachments(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_task_evidence_attachments_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_task_evidence_attachments_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- FK fk_task_evidence_attachments_task → tasks(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_task_evidence_attachments_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects
- N-1 với tasks

---

## [D06_EVIDENCE_APPROVAL_DEPENDENCY] Tên bảng: `task_evidence_reviews`

**Priority:** P1  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho task evidence reviews; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `task_id` | UUID | NULL |  | FK tasks.id nếu bản ghi gắn với task |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_task_evidence_reviews_tenant ON task_evidence_reviews(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_task_evidence_reviews_project ON task_evidence_reviews(project_id) — load dữ liệu theo project.`
- `idx_task_evidence_reviews_task ON task_evidence_reviews(task_id) — load dữ liệu theo task.`
- `idx_task_evidence_reviews_created_at ON task_evidence_reviews(created_at DESC) — phân trang/audit/log.`
- `idx_task_evidence_reviews_status ON task_evidence_reviews(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_task_evidence_reviews_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_task_evidence_reviews_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- FK fk_task_evidence_reviews_task → tasks(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_task_evidence_reviews_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects
- N-1 với tasks

---

## [D06_EVIDENCE_APPROVAL_DEPENDENCY] Tên bảng: `task_evidence_review_comments`

**Priority:** P1  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho task evidence review comments; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `task_id` | UUID | NULL |  | FK tasks.id nếu bản ghi gắn với task |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_task_evidence_review_comments_tenant ON task_evidence_review_comments(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_task_evidence_review_comments_project ON task_evidence_review_comments(project_id) — load dữ liệu theo project.`
- `idx_task_evidence_review_comments_task ON task_evidence_review_comments(task_id) — load dữ liệu theo task.`
- `idx_task_evidence_review_comments_created_at ON task_evidence_review_comments(created_at DESC) — phân trang/audit/log.`
- `idx_task_evidence_review_comments_status ON task_evidence_review_comments(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_task_evidence_review_comments_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_task_evidence_review_comments_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- FK fk_task_evidence_review_comments_task → tasks(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_task_evidence_review_comments_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects
- N-1 với tasks

---

## [D06_EVIDENCE_APPROVAL_DEPENDENCY] Tên bảng: `evidence_approval_audit_logs`

**Priority:** P0  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho evidence approval audit logs; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** High Write  
**Phân vùng:** Có — partition theo created_at tháng/quý; tenant_id là sub-filter bắt buộc.  
**Soft delete:** Không — log/audit immutable, chỉ archive theo retention policy.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `task_id` | UUID | NULL |  | FK tasks.id nếu bản ghi gắn với task |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo log |
| `archived_at` | TIMESTAMPTZ | NULL | NULL | Thời điểm archive theo retention nếu có |

**Indexes:**
- `idx_evidence_approval_audit_logs_tenant ON evidence_approval_audit_logs(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_evidence_approval_audit_logs_task ON evidence_approval_audit_logs(task_id) — load dữ liệu theo task.`
- `idx_evidence_approval_audit_logs_created_at ON evidence_approval_audit_logs(created_at DESC) — phân trang/audit/log.`
- `idx_evidence_approval_audit_logs_status ON evidence_approval_audit_logs(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_evidence_approval_audit_logs_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_evidence_approval_audit_logs_task → tasks(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_evidence_approval_audit_logs_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Log là immutable, không soft delete; chỉ archive theo retention policy.
- Mọi thao tác admin hoặc thay đổi quyền/status/deadline phải ghi log.

**Quan hệ:**
- N-1 với tenants
- N-1 với tasks

---

## [D06_EVIDENCE_APPROVAL_DEPENDENCY] Tên bảng: `task_dependencies`

**Priority:** P0  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho task dependencies; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `task_id` | UUID | NULL |  | FK tasks.id nếu bản ghi gắn với task |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_task_dependencies_tenant ON task_dependencies(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_task_dependencies_project ON task_dependencies(project_id) — load dữ liệu theo project.`
- `idx_task_dependencies_task ON task_dependencies(task_id) — load dữ liệu theo task.`
- `idx_task_dependencies_created_at ON task_dependencies(created_at DESC) — phân trang/audit/log.`
- `idx_task_dependencies_status ON task_dependencies(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_task_dependencies_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_task_dependencies_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- FK fk_task_dependencies_task → tasks(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_task_dependencies_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Không cho self-dependency.
- Không cho circular dependency.
- Chỉ liên kết task trong cùng project trừ khi bật cross-project dependency.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects
- N-1 với tasks

---

## [D06_EVIDENCE_APPROVAL_DEPENDENCY] Tên bảng: `task_dependency_types`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho task dependency types; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `task_id` | UUID | NULL |  | FK tasks.id nếu bản ghi gắn với task |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_task_dependency_types_tenant ON task_dependency_types(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_task_dependency_types_project ON task_dependency_types(project_id) — load dữ liệu theo project.`
- `idx_task_dependency_types_task ON task_dependency_types(task_id) — load dữ liệu theo task.`
- `idx_task_dependency_types_created_at ON task_dependency_types(created_at DESC) — phân trang/audit/log.`
- `idx_task_dependency_types_status ON task_dependency_types(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_task_dependency_types_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_task_dependency_types_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- FK fk_task_dependency_types_task → tasks(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_task_dependency_types_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects
- N-1 với tasks

---

## [D06_EVIDENCE_APPROVAL_DEPENDENCY] Tên bảng: `dependency_impact_analysis`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho dependency impact analysis; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `task_id` | UUID | NULL |  | FK tasks.id nếu bản ghi gắn với task |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_dependency_impact_analysis_tenant ON dependency_impact_analysis(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_dependency_impact_analysis_task ON dependency_impact_analysis(task_id) — load dữ liệu theo task.`
- `idx_dependency_impact_analysis_created_at ON dependency_impact_analysis(created_at DESC) — phân trang/audit/log.`
- `idx_dependency_impact_analysis_status ON dependency_impact_analysis(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_dependency_impact_analysis_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_dependency_impact_analysis_task → tasks(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_dependency_impact_analysis_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với tasks

---

## [D06_EVIDENCE_APPROVAL_DEPENDENCY] Tên bảng: `task_blockers`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho task blockers; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `task_id` | UUID | NULL |  | FK tasks.id nếu bản ghi gắn với task |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_task_blockers_tenant ON task_blockers(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_task_blockers_project ON task_blockers(project_id) — load dữ liệu theo project.`
- `idx_task_blockers_task ON task_blockers(task_id) — load dữ liệu theo task.`
- `idx_task_blockers_created_at ON task_blockers(created_at DESC) — phân trang/audit/log.`
- `idx_task_blockers_status ON task_blockers(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_task_blockers_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_task_blockers_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- FK fk_task_blockers_task → tasks(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_task_blockers_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects
- N-1 với tasks

---

## [D06_EVIDENCE_APPROVAL_DEPENDENCY] Tên bảng: `task_risk_flags`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho task risk flags; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** High Read / Low Write  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `task_id` | UUID | NULL |  | FK tasks.id nếu bản ghi gắn với task |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_task_risk_flags_tenant ON task_risk_flags(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_task_risk_flags_project ON task_risk_flags(project_id) — load dữ liệu theo project.`
- `idx_task_risk_flags_task ON task_risk_flags(task_id) — load dữ liệu theo task.`
- `idx_task_risk_flags_created_at ON task_risk_flags(created_at DESC) — phân trang/audit/log.`
- `idx_task_risk_flags_status ON task_risk_flags(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_task_risk_flags_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_task_risk_flags_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- FK fk_task_risk_flags_task → tasks(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- UNIQUE uq_task_risk_flags_code_scope — code unique trong scope tenant/org/project
- CHECK ck_task_risk_flags_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects
- N-1 với tasks

---

# D07_KANBAN_SPRINT_TIMELINE

| Bảng | Priority | Mục đích ngắn |
|---|---|---|
| `kanban_boards` | P2 | Lưu dữ liệu nghiệp vụ cho kanban boards; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `kanban_columns` | P0 | Lưu dữ liệu nghiệp vụ cho kanban columns; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `kanban_column_limits` | P2 | Lưu dữ liệu nghiệp vụ cho kanban column limits; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `kanban_cards` | P2 | Lưu dữ liệu nghiệp vụ cho kanban cards; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `kanban_card_orders` | P2 | Lưu dữ liệu nghiệp vụ cho kanban card orders; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `kanban_swimlanes` | P2 | Lưu dữ liệu nghiệp vụ cho kanban swimlanes; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `kanban_filters` | P2 | Lưu dữ liệu nghiệp vụ cho kanban filters; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `kanban_saved_filters` | P2 | Lưu dữ liệu nghiệp vụ cho kanban saved filters; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `sprints` | P0 | Lưu dữ liệu nghiệp vụ cho sprints; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `sprint_goals` | P2 | Lưu dữ liệu nghiệp vụ cho sprint goals; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `sprint_tasks` | P0 | Lưu dữ liệu nghiệp vụ cho sprint tasks; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `sprint_capacity_plans` | P2 | Lưu dữ liệu nghiệp vụ cho sprint capacity plans; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `sprint_member_capacities` | P2 | Lưu dữ liệu nghiệp vụ cho sprint member capacities; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `sprint_velocity_history` | P2 | Lưu dữ liệu nghiệp vụ cho sprint velocity history; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `sprint_retrospectives` | P2 | Lưu dữ liệu nghiệp vụ cho sprint retrospectives; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `sprint_review_notes` | P1 | Lưu dữ liệu nghiệp vụ cho sprint review notes; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `timeline_views` | P2 | Lưu dữ liệu nghiệp vụ cho timeline views; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `timeline_milestones` | P2 | Lưu dữ liệu nghiệp vụ cho timeline milestones; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `timeline_baseline_snapshots` | P2 | Lưu dữ liệu nghiệp vụ cho timeline baseline snapshots; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `timeline_critical_paths` | P2 | Lưu dữ liệu nghiệp vụ cho timeline critical paths; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `timeline_buffer_times` | P2 | Lưu dữ liệu nghiệp vụ cho timeline buffer times; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |


## [D07_KANBAN_SPRINT_TIMELINE] Tên bảng: `kanban_boards`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho kanban boards; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_kanban_boards_tenant ON kanban_boards(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_kanban_boards_project ON kanban_boards(project_id) — load dữ liệu theo project.`
- `idx_kanban_boards_created_at ON kanban_boards(created_at DESC) — phân trang/audit/log.`
- `idx_kanban_boards_status ON kanban_boards(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_kanban_boards_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_kanban_boards_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_kanban_boards_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects

---

## [D07_KANBAN_SPRINT_TIMELINE] Tên bảng: `kanban_columns`

**Priority:** P0  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho kanban columns; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_kanban_columns_tenant ON kanban_columns(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_kanban_columns_project ON kanban_columns(project_id) — load dữ liệu theo project.`
- `idx_kanban_columns_created_at ON kanban_columns(created_at DESC) — phân trang/audit/log.`
- `idx_kanban_columns_status ON kanban_columns(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_kanban_columns_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_kanban_columns_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_kanban_columns_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects

---

## [D07_KANBAN_SPRINT_TIMELINE] Tên bảng: `kanban_column_limits`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho kanban column limits; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_kanban_column_limits_tenant ON kanban_column_limits(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_kanban_column_limits_project ON kanban_column_limits(project_id) — load dữ liệu theo project.`
- `idx_kanban_column_limits_created_at ON kanban_column_limits(created_at DESC) — phân trang/audit/log.`
- `idx_kanban_column_limits_status ON kanban_column_limits(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_kanban_column_limits_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_kanban_column_limits_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_kanban_column_limits_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects

---

## [D07_KANBAN_SPRINT_TIMELINE] Tên bảng: `kanban_cards`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho kanban cards; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_kanban_cards_tenant ON kanban_cards(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_kanban_cards_project ON kanban_cards(project_id) — load dữ liệu theo project.`
- `idx_kanban_cards_created_at ON kanban_cards(created_at DESC) — phân trang/audit/log.`
- `idx_kanban_cards_status ON kanban_cards(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_kanban_cards_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_kanban_cards_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_kanban_cards_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects

---

## [D07_KANBAN_SPRINT_TIMELINE] Tên bảng: `kanban_card_orders`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho kanban card orders; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_kanban_card_orders_tenant ON kanban_card_orders(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_kanban_card_orders_project ON kanban_card_orders(project_id) — load dữ liệu theo project.`
- `idx_kanban_card_orders_created_at ON kanban_card_orders(created_at DESC) — phân trang/audit/log.`
- `idx_kanban_card_orders_status ON kanban_card_orders(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_kanban_card_orders_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_kanban_card_orders_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_kanban_card_orders_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects

---

## [D07_KANBAN_SPRINT_TIMELINE] Tên bảng: `kanban_swimlanes`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho kanban swimlanes; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_kanban_swimlanes_tenant ON kanban_swimlanes(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_kanban_swimlanes_project ON kanban_swimlanes(project_id) — load dữ liệu theo project.`
- `idx_kanban_swimlanes_created_at ON kanban_swimlanes(created_at DESC) — phân trang/audit/log.`
- `idx_kanban_swimlanes_status ON kanban_swimlanes(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_kanban_swimlanes_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_kanban_swimlanes_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_kanban_swimlanes_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects

---

## [D07_KANBAN_SPRINT_TIMELINE] Tên bảng: `kanban_filters`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho kanban filters; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_kanban_filters_tenant ON kanban_filters(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_kanban_filters_project ON kanban_filters(project_id) — load dữ liệu theo project.`
- `idx_kanban_filters_created_at ON kanban_filters(created_at DESC) — phân trang/audit/log.`
- `idx_kanban_filters_status ON kanban_filters(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_kanban_filters_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_kanban_filters_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_kanban_filters_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects

---

## [D07_KANBAN_SPRINT_TIMELINE] Tên bảng: `kanban_saved_filters`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho kanban saved filters; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_kanban_saved_filters_tenant ON kanban_saved_filters(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_kanban_saved_filters_project ON kanban_saved_filters(project_id) — load dữ liệu theo project.`
- `idx_kanban_saved_filters_created_at ON kanban_saved_filters(created_at DESC) — phân trang/audit/log.`
- `idx_kanban_saved_filters_status ON kanban_saved_filters(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_kanban_saved_filters_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_kanban_saved_filters_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_kanban_saved_filters_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects

---

## [D07_KANBAN_SPRINT_TIMELINE] Tên bảng: `sprints`

**Priority:** P0  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho sprints; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_sprints_tenant ON sprints(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_sprints_project ON sprints(project_id) — load dữ liệu theo project.`
- `idx_sprints_created_at ON sprints(created_at DESC) — phân trang/audit/log.`
- `idx_sprints_status ON sprints(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_sprints_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_sprints_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_sprints_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects

---

## [D07_KANBAN_SPRINT_TIMELINE] Tên bảng: `sprint_goals`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho sprint goals; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_sprint_goals_tenant ON sprint_goals(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_sprint_goals_project ON sprint_goals(project_id) — load dữ liệu theo project.`
- `idx_sprint_goals_created_at ON sprint_goals(created_at DESC) — phân trang/audit/log.`
- `idx_sprint_goals_status ON sprint_goals(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_sprint_goals_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_sprint_goals_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_sprint_goals_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects

---

## [D07_KANBAN_SPRINT_TIMELINE] Tên bảng: `sprint_tasks`

**Priority:** P0  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho sprint tasks; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `task_id` | UUID | NULL |  | FK tasks.id nếu bản ghi gắn với task |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_sprint_tasks_tenant ON sprint_tasks(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_sprint_tasks_project ON sprint_tasks(project_id) — load dữ liệu theo project.`
- `idx_sprint_tasks_task ON sprint_tasks(task_id) — load dữ liệu theo task.`
- `idx_sprint_tasks_created_at ON sprint_tasks(created_at DESC) — phân trang/audit/log.`
- `idx_sprint_tasks_status ON sprint_tasks(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_sprint_tasks_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_sprint_tasks_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- FK fk_sprint_tasks_task → tasks(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_sprint_tasks_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects
- N-1 với tasks

---

## [D07_KANBAN_SPRINT_TIMELINE] Tên bảng: `sprint_capacity_plans`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho sprint capacity plans; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_sprint_capacity_plans_tenant ON sprint_capacity_plans(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_sprint_capacity_plans_project ON sprint_capacity_plans(project_id) — load dữ liệu theo project.`
- `idx_sprint_capacity_plans_created_at ON sprint_capacity_plans(created_at DESC) — phân trang/audit/log.`
- `idx_sprint_capacity_plans_status ON sprint_capacity_plans(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_sprint_capacity_plans_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_sprint_capacity_plans_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_sprint_capacity_plans_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects

---

## [D07_KANBAN_SPRINT_TIMELINE] Tên bảng: `sprint_member_capacities`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho sprint member capacities; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `user_id` | UUID | NULL |  | FK users.id nếu bản ghi gắn với một user |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_sprint_member_capacities_tenant ON sprint_member_capacities(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_sprint_member_capacities_project ON sprint_member_capacities(project_id) — load dữ liệu theo project.`
- `idx_sprint_member_capacities_user ON sprint_member_capacities(user_id) — load dữ liệu theo user.`
- `idx_sprint_member_capacities_created_at ON sprint_member_capacities(created_at DESC) — phân trang/audit/log.`
- `idx_sprint_member_capacities_status ON sprint_member_capacities(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_sprint_member_capacities_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_sprint_member_capacities_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- FK fk_sprint_member_capacities_user → users(id) ON DELETE RESTRICT
- CHECK ck_sprint_member_capacities_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects
- N-1 với users

---

## [D07_KANBAN_SPRINT_TIMELINE] Tên bảng: `sprint_velocity_history`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho sprint velocity history; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** High Write  
**Phân vùng:** Có — partition theo created_at tháng/quý; tenant_id là sub-filter bắt buộc.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_sprint_velocity_history_tenant ON sprint_velocity_history(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_sprint_velocity_history_project ON sprint_velocity_history(project_id) — load dữ liệu theo project.`
- `idx_sprint_velocity_history_created_at ON sprint_velocity_history(created_at DESC) — phân trang/audit/log.`
- `idx_sprint_velocity_history_status ON sprint_velocity_history(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_sprint_velocity_history_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_sprint_velocity_history_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_sprint_velocity_history_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects

---

## [D07_KANBAN_SPRINT_TIMELINE] Tên bảng: `sprint_retrospectives`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho sprint retrospectives; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_sprint_retrospectives_tenant ON sprint_retrospectives(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_sprint_retrospectives_project ON sprint_retrospectives(project_id) — load dữ liệu theo project.`
- `idx_sprint_retrospectives_created_at ON sprint_retrospectives(created_at DESC) — phân trang/audit/log.`
- `idx_sprint_retrospectives_status ON sprint_retrospectives(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_sprint_retrospectives_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_sprint_retrospectives_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_sprint_retrospectives_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects

---

## [D07_KANBAN_SPRINT_TIMELINE] Tên bảng: `sprint_review_notes`

**Priority:** P1  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho sprint review notes; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `task_id` | UUID | NULL |  | FK tasks.id nếu bản ghi gắn với task |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_sprint_review_notes_tenant ON sprint_review_notes(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_sprint_review_notes_project ON sprint_review_notes(project_id) — load dữ liệu theo project.`
- `idx_sprint_review_notes_task ON sprint_review_notes(task_id) — load dữ liệu theo task.`
- `idx_sprint_review_notes_created_at ON sprint_review_notes(created_at DESC) — phân trang/audit/log.`
- `idx_sprint_review_notes_status ON sprint_review_notes(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_sprint_review_notes_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_sprint_review_notes_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- FK fk_sprint_review_notes_task → tasks(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_sprint_review_notes_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects
- N-1 với tasks

---

## [D07_KANBAN_SPRINT_TIMELINE] Tên bảng: `timeline_views`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho timeline views; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_timeline_views_tenant ON timeline_views(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_timeline_views_project ON timeline_views(project_id) — load dữ liệu theo project.`
- `idx_timeline_views_created_at ON timeline_views(created_at DESC) — phân trang/audit/log.`
- `idx_timeline_views_status ON timeline_views(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_timeline_views_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_timeline_views_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_timeline_views_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects

---

## [D07_KANBAN_SPRINT_TIMELINE] Tên bảng: `timeline_milestones`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho timeline milestones; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_timeline_milestones_tenant ON timeline_milestones(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_timeline_milestones_project ON timeline_milestones(project_id) — load dữ liệu theo project.`
- `idx_timeline_milestones_created_at ON timeline_milestones(created_at DESC) — phân trang/audit/log.`
- `idx_timeline_milestones_status ON timeline_milestones(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_timeline_milestones_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_timeline_milestones_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_timeline_milestones_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects

---

## [D07_KANBAN_SPRINT_TIMELINE] Tên bảng: `timeline_baseline_snapshots`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho timeline baseline snapshots; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** High Write  
**Phân vùng:** Có — partition theo created_at tháng/quý; tenant_id là sub-filter bắt buộc.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_timeline_baseline_snapshots_tenant ON timeline_baseline_snapshots(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_timeline_baseline_snapshots_project ON timeline_baseline_snapshots(project_id) — load dữ liệu theo project.`
- `idx_timeline_baseline_snapshots_created_at ON timeline_baseline_snapshots(created_at DESC) — phân trang/audit/log.`
- `idx_timeline_baseline_snapshots_status ON timeline_baseline_snapshots(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_timeline_baseline_snapshots_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_timeline_baseline_snapshots_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_timeline_baseline_snapshots_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects

---

## [D07_KANBAN_SPRINT_TIMELINE] Tên bảng: `timeline_critical_paths`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho timeline critical paths; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_timeline_critical_paths_tenant ON timeline_critical_paths(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_timeline_critical_paths_project ON timeline_critical_paths(project_id) — load dữ liệu theo project.`
- `idx_timeline_critical_paths_created_at ON timeline_critical_paths(created_at DESC) — phân trang/audit/log.`
- `idx_timeline_critical_paths_status ON timeline_critical_paths(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_timeline_critical_paths_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_timeline_critical_paths_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_timeline_critical_paths_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects

---

## [D07_KANBAN_SPRINT_TIMELINE] Tên bảng: `timeline_buffer_times`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho timeline buffer times; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_timeline_buffer_times_tenant ON timeline_buffer_times(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_timeline_buffer_times_project ON timeline_buffer_times(project_id) — load dữ liệu theo project.`
- `idx_timeline_buffer_times_created_at ON timeline_buffer_times(created_at DESC) — phân trang/audit/log.`
- `idx_timeline_buffer_times_status ON timeline_buffer_times(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_timeline_buffer_times_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_timeline_buffer_times_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_timeline_buffer_times_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects

---

# D08_COMMENT_CHAT_REALTIME

| Bảng | Priority | Mục đích ngắn |
|---|---|---|
| `comments` | P1 | Lưu dữ liệu nghiệp vụ cho comments; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `comment_reactions` | P2 | Lưu dữ liệu nghiệp vụ cho comment reactions; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `comment_mentions` | P2 | Lưu dữ liệu nghiệp vụ cho comment mentions; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `comment_attachments` | P2 | Lưu dữ liệu nghiệp vụ cho comment attachments; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `comment_edit_history` | P2 | Lưu dữ liệu nghiệp vụ cho comment edit history; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `chat_rooms` | P0 | Lưu dữ liệu nghiệp vụ cho chat rooms; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `chat_room_members` | P2 | Lưu dữ liệu nghiệp vụ cho chat room members; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `chat_room_settings` | P2 | Lưu dữ liệu nghiệp vụ cho chat room settings; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `chat_room_pinned_messages` | P2 | Lưu dữ liệu nghiệp vụ cho chat room pinned messages; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `chat_messages` | P0 | Lưu nội dung tin nhắn Mini Zalo, dùng làm nguồn tạo task/action item nếu được xác nhận. |
| `chat_message_reactions` | P2 | Lưu dữ liệu nghiệp vụ cho chat message reactions; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `chat_message_mentions` | P2 | Lưu dữ liệu nghiệp vụ cho chat message mentions; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `chat_message_attachments` | P2 | Lưu dữ liệu nghiệp vụ cho chat message attachments; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `chat_message_task_links` | P2 | Lưu dữ liệu nghiệp vụ cho chat message task links; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `chat_message_edit_history` | P2 | Lưu dữ liệu nghiệp vụ cho chat message edit history; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `chat_message_read_receipts` | P2 | Lưu dữ liệu nghiệp vụ cho chat message read receipts; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `chat_direct_messages` | P2 | Lưu dữ liệu nghiệp vụ cho chat direct messages; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `chat_dm_participants` | P2 | Lưu dữ liệu nghiệp vụ cho chat dm participants; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `realtime_presence_sessions` | P2 | Lưu dữ liệu nghiệp vụ cho realtime presence sessions; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `realtime_typing_indicators` | P2 | Lưu dữ liệu nghiệp vụ cho realtime typing indicators; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |


## [D08_COMMENT_CHAT_REALTIME] Tên bảng: `comments`

**Priority:** P1  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho comments; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `task_id` | UUID | NULL |  | FK tasks.id nếu bản ghi gắn với task |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_comments_tenant ON comments(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_comments_task ON comments(task_id) — load dữ liệu theo task.`
- `idx_comments_created_at ON comments(created_at DESC) — phân trang/audit/log.`
- `idx_comments_status ON comments(status) — filter theo trạng thái.`
- `idx_comments_entity_time ON comments(entity_type, entity_id, created_at) — load thread theo đối tượng.`

**Constraints:**
- FK fk_comments_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_comments_task → tasks(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_comments_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với tasks

---

## [D08_COMMENT_CHAT_REALTIME] Tên bảng: `comment_reactions`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho comment reactions; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `task_id` | UUID | NULL |  | FK tasks.id nếu bản ghi gắn với task |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_comment_reactions_tenant ON comment_reactions(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_comment_reactions_task ON comment_reactions(task_id) — load dữ liệu theo task.`
- `idx_comment_reactions_created_at ON comment_reactions(created_at DESC) — phân trang/audit/log.`
- `idx_comment_reactions_status ON comment_reactions(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_comment_reactions_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_comment_reactions_task → tasks(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_comment_reactions_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với tasks

---

## [D08_COMMENT_CHAT_REALTIME] Tên bảng: `comment_mentions`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho comment mentions; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `task_id` | UUID | NULL |  | FK tasks.id nếu bản ghi gắn với task |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_comment_mentions_tenant ON comment_mentions(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_comment_mentions_task ON comment_mentions(task_id) — load dữ liệu theo task.`
- `idx_comment_mentions_created_at ON comment_mentions(created_at DESC) — phân trang/audit/log.`
- `idx_comment_mentions_status ON comment_mentions(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_comment_mentions_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_comment_mentions_task → tasks(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_comment_mentions_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với tasks

---

## [D08_COMMENT_CHAT_REALTIME] Tên bảng: `comment_attachments`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho comment attachments; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `task_id` | UUID | NULL |  | FK tasks.id nếu bản ghi gắn với task |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_comment_attachments_tenant ON comment_attachments(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_comment_attachments_task ON comment_attachments(task_id) — load dữ liệu theo task.`
- `idx_comment_attachments_created_at ON comment_attachments(created_at DESC) — phân trang/audit/log.`
- `idx_comment_attachments_status ON comment_attachments(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_comment_attachments_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_comment_attachments_task → tasks(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_comment_attachments_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với tasks

---

## [D08_COMMENT_CHAT_REALTIME] Tên bảng: `comment_edit_history`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho comment edit history; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** High Write  
**Phân vùng:** Có — partition theo created_at tháng/quý; tenant_id là sub-filter bắt buộc.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `task_id` | UUID | NULL |  | FK tasks.id nếu bản ghi gắn với task |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_comment_edit_history_tenant ON comment_edit_history(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_comment_edit_history_task ON comment_edit_history(task_id) — load dữ liệu theo task.`
- `idx_comment_edit_history_created_at ON comment_edit_history(created_at DESC) — phân trang/audit/log.`
- `idx_comment_edit_history_status ON comment_edit_history(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_comment_edit_history_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_comment_edit_history_task → tasks(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_comment_edit_history_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với tasks

---

## [D08_COMMENT_CHAT_REALTIME] Tên bảng: `chat_rooms`

**Priority:** P0  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho chat rooms; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_chat_rooms_tenant ON chat_rooms(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_chat_rooms_project ON chat_rooms(project_id) — load dữ liệu theo project.`
- `idx_chat_rooms_created_at ON chat_rooms(created_at DESC) — phân trang/audit/log.`
- `idx_chat_rooms_status ON chat_rooms(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_chat_rooms_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_chat_rooms_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_chat_rooms_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Customer chỉ truy cập room/message nếu project policy cho phép.
- Message xóa mềm, không hard delete nội dung nếu cần audit.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects

---

## [D08_COMMENT_CHAT_REALTIME] Tên bảng: `chat_room_members`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho chat room members; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `user_id` | UUID | NULL |  | FK users.id nếu bản ghi gắn với một user |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_chat_room_members_tenant ON chat_room_members(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_chat_room_members_project ON chat_room_members(project_id) — load dữ liệu theo project.`
- `idx_chat_room_members_user ON chat_room_members(user_id) — load dữ liệu theo user.`
- `idx_chat_room_members_created_at ON chat_room_members(created_at DESC) — phân trang/audit/log.`
- `idx_chat_room_members_status ON chat_room_members(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_chat_room_members_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_chat_room_members_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- FK fk_chat_room_members_user → users(id) ON DELETE RESTRICT
- CHECK ck_chat_room_members_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Customer chỉ truy cập room/message nếu project policy cho phép.
- Message xóa mềm, không hard delete nội dung nếu cần audit.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects
- N-1 với users

---

## [D08_COMMENT_CHAT_REALTIME] Tên bảng: `chat_room_settings`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho chat room settings; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** High Read / Low Write  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_chat_room_settings_tenant ON chat_room_settings(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_chat_room_settings_project ON chat_room_settings(project_id) — load dữ liệu theo project.`
- `idx_chat_room_settings_created_at ON chat_room_settings(created_at DESC) — phân trang/audit/log.`
- `idx_chat_room_settings_status ON chat_room_settings(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_chat_room_settings_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_chat_room_settings_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_chat_room_settings_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Customer chỉ truy cập room/message nếu project policy cho phép.
- Message xóa mềm, không hard delete nội dung nếu cần audit.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects

---

## [D08_COMMENT_CHAT_REALTIME] Tên bảng: `chat_room_pinned_messages`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho chat room pinned messages; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** High Write  
**Phân vùng:** Có — partition theo created_at tháng/quý; tenant_id là sub-filter bắt buộc.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_chat_room_pinned_messages_tenant ON chat_room_pinned_messages(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_chat_room_pinned_messages_project ON chat_room_pinned_messages(project_id) — load dữ liệu theo project.`
- `idx_chat_room_pinned_messages_created_at ON chat_room_pinned_messages(created_at DESC) — phân trang/audit/log.`
- `idx_chat_room_pinned_messages_status ON chat_room_pinned_messages(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_chat_room_pinned_messages_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_chat_room_pinned_messages_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_chat_room_pinned_messages_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Customer chỉ truy cập room/message nếu project policy cho phép.
- Message xóa mềm, không hard delete nội dung nếu cần audit.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects

---

## [D08_COMMENT_CHAT_REALTIME] Tên bảng: `chat_messages`

**Priority:** P0  
**Mô tả:** Lưu nội dung tin nhắn Mini Zalo, dùng làm nguồn tạo task/action item nếu được xác nhận.  
**Tần suất đọc/ghi:** High Write  
**Phân vùng:** Có — partition theo created_at tháng/quý; tenant_id là sub-filter bắt buộc.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `room_id` | UUID | NOT NULL |  | FK chat_rooms.id |
| `sender_id` | UUID | NOT NULL |  | FK users.id |
| `content_md` | TEXT | NULL |  | Nội dung message |
| `message_type` | VARCHAR(30) | NOT NULL | 'text' | text/file/image/system/task_link |
| `visibility` | VARCHAR(30) | NOT NULL | 'internal' | internal/customer_safe/private |
| `pinned_at` | TIMESTAMPTZ | NULL |  | Thời điểm pin |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_chat_messages_tenant ON chat_messages(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_chat_messages_project ON chat_messages(project_id) — load dữ liệu theo project.`
- `idx_chat_messages_created_at ON chat_messages(created_at DESC) — phân trang/audit/log.`
- `idx_chat_messages_room_time ON chat_messages(room_id, created_at DESC) — paginate room.`

**Constraints:**
- FK fk_chat_messages_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_chat_messages_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Customer chỉ truy cập room/message nếu project policy cho phép.
- Message xóa mềm, không hard delete nội dung nếu cần audit.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects

---

## [D08_COMMENT_CHAT_REALTIME] Tên bảng: `chat_message_reactions`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho chat message reactions; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_chat_message_reactions_tenant ON chat_message_reactions(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_chat_message_reactions_project ON chat_message_reactions(project_id) — load dữ liệu theo project.`
- `idx_chat_message_reactions_created_at ON chat_message_reactions(created_at DESC) — phân trang/audit/log.`
- `idx_chat_message_reactions_status ON chat_message_reactions(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_chat_message_reactions_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_chat_message_reactions_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_chat_message_reactions_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Customer chỉ truy cập room/message nếu project policy cho phép.
- Message xóa mềm, không hard delete nội dung nếu cần audit.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects

---

## [D08_COMMENT_CHAT_REALTIME] Tên bảng: `chat_message_mentions`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho chat message mentions; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_chat_message_mentions_tenant ON chat_message_mentions(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_chat_message_mentions_project ON chat_message_mentions(project_id) — load dữ liệu theo project.`
- `idx_chat_message_mentions_created_at ON chat_message_mentions(created_at DESC) — phân trang/audit/log.`
- `idx_chat_message_mentions_status ON chat_message_mentions(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_chat_message_mentions_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_chat_message_mentions_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_chat_message_mentions_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Customer chỉ truy cập room/message nếu project policy cho phép.
- Message xóa mềm, không hard delete nội dung nếu cần audit.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects

---

## [D08_COMMENT_CHAT_REALTIME] Tên bảng: `chat_message_attachments`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho chat message attachments; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_chat_message_attachments_tenant ON chat_message_attachments(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_chat_message_attachments_project ON chat_message_attachments(project_id) — load dữ liệu theo project.`
- `idx_chat_message_attachments_created_at ON chat_message_attachments(created_at DESC) — phân trang/audit/log.`
- `idx_chat_message_attachments_status ON chat_message_attachments(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_chat_message_attachments_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_chat_message_attachments_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_chat_message_attachments_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Customer chỉ truy cập room/message nếu project policy cho phép.
- Message xóa mềm, không hard delete nội dung nếu cần audit.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects

---

## [D08_COMMENT_CHAT_REALTIME] Tên bảng: `chat_message_task_links`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho chat message task links; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `task_id` | UUID | NULL |  | FK tasks.id nếu bản ghi gắn với task |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_chat_message_task_links_tenant ON chat_message_task_links(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_chat_message_task_links_project ON chat_message_task_links(project_id) — load dữ liệu theo project.`
- `idx_chat_message_task_links_task ON chat_message_task_links(task_id) — load dữ liệu theo task.`
- `idx_chat_message_task_links_created_at ON chat_message_task_links(created_at DESC) — phân trang/audit/log.`
- `idx_chat_message_task_links_status ON chat_message_task_links(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_chat_message_task_links_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_chat_message_task_links_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- FK fk_chat_message_task_links_task → tasks(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_chat_message_task_links_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Customer chỉ truy cập room/message nếu project policy cho phép.
- Message xóa mềm, không hard delete nội dung nếu cần audit.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects
- N-1 với tasks

---

## [D08_COMMENT_CHAT_REALTIME] Tên bảng: `chat_message_edit_history`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho chat message edit history; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** High Write  
**Phân vùng:** Có — partition theo created_at tháng/quý; tenant_id là sub-filter bắt buộc.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_chat_message_edit_history_tenant ON chat_message_edit_history(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_chat_message_edit_history_project ON chat_message_edit_history(project_id) — load dữ liệu theo project.`
- `idx_chat_message_edit_history_created_at ON chat_message_edit_history(created_at DESC) — phân trang/audit/log.`
- `idx_chat_message_edit_history_status ON chat_message_edit_history(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_chat_message_edit_history_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_chat_message_edit_history_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_chat_message_edit_history_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Customer chỉ truy cập room/message nếu project policy cho phép.
- Message xóa mềm, không hard delete nội dung nếu cần audit.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects

---

## [D08_COMMENT_CHAT_REALTIME] Tên bảng: `chat_message_read_receipts`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho chat message read receipts; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_chat_message_read_receipts_tenant ON chat_message_read_receipts(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_chat_message_read_receipts_project ON chat_message_read_receipts(project_id) — load dữ liệu theo project.`
- `idx_chat_message_read_receipts_created_at ON chat_message_read_receipts(created_at DESC) — phân trang/audit/log.`
- `idx_chat_message_read_receipts_status ON chat_message_read_receipts(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_chat_message_read_receipts_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_chat_message_read_receipts_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_chat_message_read_receipts_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Customer chỉ truy cập room/message nếu project policy cho phép.
- Message xóa mềm, không hard delete nội dung nếu cần audit.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects

---

## [D08_COMMENT_CHAT_REALTIME] Tên bảng: `chat_direct_messages`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho chat direct messages; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** High Write  
**Phân vùng:** Có — partition theo created_at tháng/quý; tenant_id là sub-filter bắt buộc.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_chat_direct_messages_tenant ON chat_direct_messages(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_chat_direct_messages_project ON chat_direct_messages(project_id) — load dữ liệu theo project.`
- `idx_chat_direct_messages_created_at ON chat_direct_messages(created_at DESC) — phân trang/audit/log.`
- `idx_chat_direct_messages_status ON chat_direct_messages(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_chat_direct_messages_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_chat_direct_messages_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_chat_direct_messages_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Customer chỉ truy cập room/message nếu project policy cho phép.
- Message xóa mềm, không hard delete nội dung nếu cần audit.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects

---

## [D08_COMMENT_CHAT_REALTIME] Tên bảng: `chat_dm_participants`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho chat dm participants; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `user_id` | UUID | NULL |  | FK users.id nếu bản ghi gắn với một user |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_chat_dm_participants_tenant ON chat_dm_participants(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_chat_dm_participants_project ON chat_dm_participants(project_id) — load dữ liệu theo project.`
- `idx_chat_dm_participants_user ON chat_dm_participants(user_id) — load dữ liệu theo user.`
- `idx_chat_dm_participants_created_at ON chat_dm_participants(created_at DESC) — phân trang/audit/log.`
- `idx_chat_dm_participants_status ON chat_dm_participants(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_chat_dm_participants_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_chat_dm_participants_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- FK fk_chat_dm_participants_user → users(id) ON DELETE RESTRICT
- CHECK ck_chat_dm_participants_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Customer chỉ truy cập room/message nếu project policy cho phép.
- Message xóa mềm, không hard delete nội dung nếu cần audit.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects
- N-1 với users

---

## [D08_COMMENT_CHAT_REALTIME] Tên bảng: `realtime_presence_sessions`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho realtime presence sessions; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** High Write  
**Phân vùng:** Có — partition theo created_at tháng/quý; tenant_id là sub-filter bắt buộc.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_realtime_presence_sessions_tenant ON realtime_presence_sessions(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_realtime_presence_sessions_created_at ON realtime_presence_sessions(created_at DESC) — phân trang/audit/log.`
- `idx_realtime_presence_sessions_status ON realtime_presence_sessions(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_realtime_presence_sessions_tenant → tenants(id) ON DELETE RESTRICT
- CHECK ck_realtime_presence_sessions_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants

---

## [D08_COMMENT_CHAT_REALTIME] Tên bảng: `realtime_typing_indicators`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho realtime typing indicators; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_realtime_typing_indicators_tenant ON realtime_typing_indicators(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_realtime_typing_indicators_created_at ON realtime_typing_indicators(created_at DESC) — phân trang/audit/log.`
- `idx_realtime_typing_indicators_status ON realtime_typing_indicators(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_realtime_typing_indicators_tenant → tenants(id) ON DELETE RESTRICT
- CHECK ck_realtime_typing_indicators_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants

---

# D09_MEETING_WIKI_FILE

| Bảng | Priority | Mục đích ngắn |
|---|---|---|
| `meetings` | P1 | Lưu dữ liệu nghiệp vụ cho meetings; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `meeting_participants` | P1 | Lưu dữ liệu nghiệp vụ cho meeting participants; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `meeting_agendas` | P1 | Lưu dữ liệu nghiệp vụ cho meeting agendas; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `meeting_agenda_items` | P1 | Lưu dữ liệu nghiệp vụ cho meeting agenda items; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `meeting_notes` | P1 | Lưu dữ liệu nghiệp vụ cho meeting notes; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `meeting_note_sections` | P1 | Lưu dữ liệu nghiệp vụ cho meeting note sections; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `meeting_action_items` | P1 | Lưu dữ liệu nghiệp vụ cho meeting action items; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `meeting_recordings` | P1 | Lưu dữ liệu nghiệp vụ cho meeting recordings; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `meeting_ai_summaries` | P1 | Lưu dữ liệu nghiệp vụ cho meeting ai summaries; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `meeting_transcripts` | P1 | Lưu dữ liệu nghiệp vụ cho meeting transcripts; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `wiki_spaces` | P2 | Lưu dữ liệu nghiệp vụ cho wiki spaces; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `wiki_pages` | P0 | Lưu metadata trang wiki; nội dung dài và version được tách riêng để dễ audit và rollback. |
| `wiki_page_versions` | P2 | Lưu dữ liệu nghiệp vụ cho wiki page versions; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `wiki_page_contents` | P2 | Lưu dữ liệu nghiệp vụ cho wiki page contents; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `wiki_page_permissions` | P2 | Lưu dữ liệu nghiệp vụ cho wiki page permissions; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `wiki_page_watchers` | P2 | Lưu dữ liệu nghiệp vụ cho wiki page watchers; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `wiki_page_comments` | P1 | Lưu dữ liệu nghiệp vụ cho wiki page comments; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `wiki_page_links` | P2 | Lưu dữ liệu nghiệp vụ cho wiki page links; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `wiki_page_attachments` | P2 | Lưu dữ liệu nghiệp vụ cho wiki page attachments; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `wiki_page_templates` | P2 | Lưu dữ liệu nghiệp vụ cho wiki page templates; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `wiki_page_tags` | P2 | Lưu dữ liệu nghiệp vụ cho wiki page tags; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `wiki_page_export_history` | P2 | Lưu dữ liệu nghiệp vụ cho wiki page export history; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `files` | P0 | Lưu dữ liệu nghiệp vụ cho files; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `file_versions` | P2 | Lưu dữ liệu nghiệp vụ cho file versions; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |


## [D09_MEETING_WIKI_FILE] Tên bảng: `meetings`

**Priority:** P1  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho meetings; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_meetings_tenant ON meetings(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_meetings_project ON meetings(project_id) — load dữ liệu theo project.`
- `idx_meetings_created_at ON meetings(created_at DESC) — phân trang/audit/log.`
- `idx_meetings_status ON meetings(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_meetings_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_meetings_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_meetings_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects

---

## [D09_MEETING_WIKI_FILE] Tên bảng: `meeting_participants`

**Priority:** P1  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho meeting participants; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `user_id` | UUID | NULL |  | FK users.id nếu bản ghi gắn với một user |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_meeting_participants_tenant ON meeting_participants(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_meeting_participants_project ON meeting_participants(project_id) — load dữ liệu theo project.`
- `idx_meeting_participants_user ON meeting_participants(user_id) — load dữ liệu theo user.`
- `idx_meeting_participants_created_at ON meeting_participants(created_at DESC) — phân trang/audit/log.`
- `idx_meeting_participants_status ON meeting_participants(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_meeting_participants_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_meeting_participants_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- FK fk_meeting_participants_user → users(id) ON DELETE RESTRICT
- CHECK ck_meeting_participants_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects
- N-1 với users

---

## [D09_MEETING_WIKI_FILE] Tên bảng: `meeting_agendas`

**Priority:** P1  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho meeting agendas; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_meeting_agendas_tenant ON meeting_agendas(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_meeting_agendas_project ON meeting_agendas(project_id) — load dữ liệu theo project.`
- `idx_meeting_agendas_created_at ON meeting_agendas(created_at DESC) — phân trang/audit/log.`
- `idx_meeting_agendas_status ON meeting_agendas(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_meeting_agendas_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_meeting_agendas_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_meeting_agendas_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects

---

## [D09_MEETING_WIKI_FILE] Tên bảng: `meeting_agenda_items`

**Priority:** P1  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho meeting agenda items; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_meeting_agenda_items_tenant ON meeting_agenda_items(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_meeting_agenda_items_project ON meeting_agenda_items(project_id) — load dữ liệu theo project.`
- `idx_meeting_agenda_items_created_at ON meeting_agenda_items(created_at DESC) — phân trang/audit/log.`
- `idx_meeting_agenda_items_status ON meeting_agenda_items(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_meeting_agenda_items_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_meeting_agenda_items_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_meeting_agenda_items_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects

---

## [D09_MEETING_WIKI_FILE] Tên bảng: `meeting_notes`

**Priority:** P1  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho meeting notes; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_meeting_notes_tenant ON meeting_notes(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_meeting_notes_project ON meeting_notes(project_id) — load dữ liệu theo project.`
- `idx_meeting_notes_created_at ON meeting_notes(created_at DESC) — phân trang/audit/log.`
- `idx_meeting_notes_status ON meeting_notes(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_meeting_notes_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_meeting_notes_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_meeting_notes_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects

---

## [D09_MEETING_WIKI_FILE] Tên bảng: `meeting_note_sections`

**Priority:** P1  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho meeting note sections; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_meeting_note_sections_tenant ON meeting_note_sections(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_meeting_note_sections_project ON meeting_note_sections(project_id) — load dữ liệu theo project.`
- `idx_meeting_note_sections_created_at ON meeting_note_sections(created_at DESC) — phân trang/audit/log.`
- `idx_meeting_note_sections_status ON meeting_note_sections(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_meeting_note_sections_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_meeting_note_sections_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_meeting_note_sections_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects

---

## [D09_MEETING_WIKI_FILE] Tên bảng: `meeting_action_items`

**Priority:** P1  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho meeting action items; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_meeting_action_items_tenant ON meeting_action_items(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_meeting_action_items_project ON meeting_action_items(project_id) — load dữ liệu theo project.`
- `idx_meeting_action_items_created_at ON meeting_action_items(created_at DESC) — phân trang/audit/log.`
- `idx_meeting_action_items_status ON meeting_action_items(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_meeting_action_items_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_meeting_action_items_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_meeting_action_items_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects

---

## [D09_MEETING_WIKI_FILE] Tên bảng: `meeting_recordings`

**Priority:** P1  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho meeting recordings; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_meeting_recordings_tenant ON meeting_recordings(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_meeting_recordings_project ON meeting_recordings(project_id) — load dữ liệu theo project.`
- `idx_meeting_recordings_created_at ON meeting_recordings(created_at DESC) — phân trang/audit/log.`
- `idx_meeting_recordings_status ON meeting_recordings(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_meeting_recordings_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_meeting_recordings_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_meeting_recordings_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects

---

## [D09_MEETING_WIKI_FILE] Tên bảng: `meeting_ai_summaries`

**Priority:** P1  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho meeting ai summaries; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_meeting_ai_summaries_tenant ON meeting_ai_summaries(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_meeting_ai_summaries_project ON meeting_ai_summaries(project_id) — load dữ liệu theo project.`
- `idx_meeting_ai_summaries_created_at ON meeting_ai_summaries(created_at DESC) — phân trang/audit/log.`
- `idx_meeting_ai_summaries_status ON meeting_ai_summaries(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_meeting_ai_summaries_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_meeting_ai_summaries_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_meeting_ai_summaries_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects

---

## [D09_MEETING_WIKI_FILE] Tên bảng: `meeting_transcripts`

**Priority:** P1  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho meeting transcripts; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_meeting_transcripts_tenant ON meeting_transcripts(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_meeting_transcripts_project ON meeting_transcripts(project_id) — load dữ liệu theo project.`
- `idx_meeting_transcripts_created_at ON meeting_transcripts(created_at DESC) — phân trang/audit/log.`
- `idx_meeting_transcripts_status ON meeting_transcripts(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_meeting_transcripts_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_meeting_transcripts_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_meeting_transcripts_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects

---

## [D09_MEETING_WIKI_FILE] Tên bảng: `wiki_spaces`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho wiki spaces; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_wiki_spaces_tenant ON wiki_spaces(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_wiki_spaces_project ON wiki_spaces(project_id) — load dữ liệu theo project.`
- `idx_wiki_spaces_created_at ON wiki_spaces(created_at DESC) — phân trang/audit/log.`
- `idx_wiki_spaces_status ON wiki_spaces(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_wiki_spaces_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_wiki_spaces_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_wiki_spaces_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects

---

## [D09_MEETING_WIKI_FILE] Tên bảng: `wiki_pages`

**Priority:** P0  
**Mô tả:** Lưu metadata trang wiki; nội dung dài và version được tách riêng để dễ audit và rollback.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `space_id` | UUID | NULL |  | FK wiki_spaces.id |
| `parent_page_id` | UUID | NULL |  | Trang cha |
| `title` | VARCHAR(250) | NOT NULL |  | Tiêu đề trang |
| `slug` | VARCHAR(250) | NOT NULL |  | Slug URL |
| `visibility` | VARCHAR(30) | NOT NULL | 'internal' | internal/customer_safe/public/private |
| `current_version_no` | INTEGER | NOT NULL | 1 | Version hiện hành |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_wiki_pages_tenant ON wiki_pages(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_wiki_pages_project ON wiki_pages(project_id) — load dữ liệu theo project.`
- `idx_wiki_pages_created_at ON wiki_pages(created_at DESC) — phân trang/audit/log.`

**Constraints:**
- FK fk_wiki_pages_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_wiki_pages_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects

---

## [D09_MEETING_WIKI_FILE] Tên bảng: `wiki_page_versions`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho wiki page versions; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_wiki_page_versions_tenant ON wiki_page_versions(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_wiki_page_versions_project ON wiki_page_versions(project_id) — load dữ liệu theo project.`
- `idx_wiki_page_versions_created_at ON wiki_page_versions(created_at DESC) — phân trang/audit/log.`
- `idx_wiki_page_versions_status ON wiki_page_versions(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_wiki_page_versions_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_wiki_page_versions_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_wiki_page_versions_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects

---

## [D09_MEETING_WIKI_FILE] Tên bảng: `wiki_page_contents`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho wiki page contents; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_wiki_page_contents_tenant ON wiki_page_contents(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_wiki_page_contents_project ON wiki_page_contents(project_id) — load dữ liệu theo project.`
- `idx_wiki_page_contents_created_at ON wiki_page_contents(created_at DESC) — phân trang/audit/log.`
- `idx_wiki_page_contents_status ON wiki_page_contents(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_wiki_page_contents_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_wiki_page_contents_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_wiki_page_contents_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects

---

## [D09_MEETING_WIKI_FILE] Tên bảng: `wiki_page_permissions`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho wiki page permissions; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** High Read / Low Write  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_wiki_page_permissions_tenant ON wiki_page_permissions(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_wiki_page_permissions_project ON wiki_page_permissions(project_id) — load dữ liệu theo project.`
- `idx_wiki_page_permissions_created_at ON wiki_page_permissions(created_at DESC) — phân trang/audit/log.`
- `idx_wiki_page_permissions_status ON wiki_page_permissions(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_wiki_page_permissions_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_wiki_page_permissions_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_wiki_page_permissions_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects

---

## [D09_MEETING_WIKI_FILE] Tên bảng: `wiki_page_watchers`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho wiki page watchers; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_wiki_page_watchers_tenant ON wiki_page_watchers(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_wiki_page_watchers_project ON wiki_page_watchers(project_id) — load dữ liệu theo project.`
- `idx_wiki_page_watchers_created_at ON wiki_page_watchers(created_at DESC) — phân trang/audit/log.`
- `idx_wiki_page_watchers_status ON wiki_page_watchers(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_wiki_page_watchers_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_wiki_page_watchers_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_wiki_page_watchers_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects

---

## [D09_MEETING_WIKI_FILE] Tên bảng: `wiki_page_comments`

**Priority:** P1  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho wiki page comments; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `task_id` | UUID | NULL |  | FK tasks.id nếu bản ghi gắn với task |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_wiki_page_comments_tenant ON wiki_page_comments(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_wiki_page_comments_project ON wiki_page_comments(project_id) — load dữ liệu theo project.`
- `idx_wiki_page_comments_task ON wiki_page_comments(task_id) — load dữ liệu theo task.`
- `idx_wiki_page_comments_created_at ON wiki_page_comments(created_at DESC) — phân trang/audit/log.`
- `idx_wiki_page_comments_status ON wiki_page_comments(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_wiki_page_comments_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_wiki_page_comments_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- FK fk_wiki_page_comments_task → tasks(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_wiki_page_comments_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects
- N-1 với tasks

---

## [D09_MEETING_WIKI_FILE] Tên bảng: `wiki_page_links`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho wiki page links; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_wiki_page_links_tenant ON wiki_page_links(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_wiki_page_links_project ON wiki_page_links(project_id) — load dữ liệu theo project.`
- `idx_wiki_page_links_created_at ON wiki_page_links(created_at DESC) — phân trang/audit/log.`
- `idx_wiki_page_links_status ON wiki_page_links(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_wiki_page_links_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_wiki_page_links_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_wiki_page_links_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects

---

## [D09_MEETING_WIKI_FILE] Tên bảng: `wiki_page_attachments`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho wiki page attachments; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_wiki_page_attachments_tenant ON wiki_page_attachments(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_wiki_page_attachments_project ON wiki_page_attachments(project_id) — load dữ liệu theo project.`
- `idx_wiki_page_attachments_created_at ON wiki_page_attachments(created_at DESC) — phân trang/audit/log.`
- `idx_wiki_page_attachments_status ON wiki_page_attachments(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_wiki_page_attachments_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_wiki_page_attachments_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_wiki_page_attachments_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects

---

## [D09_MEETING_WIKI_FILE] Tên bảng: `wiki_page_templates`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho wiki page templates; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** High Read / Low Write  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_wiki_page_templates_tenant ON wiki_page_templates(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_wiki_page_templates_project ON wiki_page_templates(project_id) — load dữ liệu theo project.`
- `idx_wiki_page_templates_created_at ON wiki_page_templates(created_at DESC) — phân trang/audit/log.`
- `idx_wiki_page_templates_status ON wiki_page_templates(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_wiki_page_templates_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_wiki_page_templates_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_wiki_page_templates_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects

---

## [D09_MEETING_WIKI_FILE] Tên bảng: `wiki_page_tags`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho wiki page tags; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_wiki_page_tags_tenant ON wiki_page_tags(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_wiki_page_tags_project ON wiki_page_tags(project_id) — load dữ liệu theo project.`
- `idx_wiki_page_tags_created_at ON wiki_page_tags(created_at DESC) — phân trang/audit/log.`
- `idx_wiki_page_tags_status ON wiki_page_tags(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_wiki_page_tags_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_wiki_page_tags_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_wiki_page_tags_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects

---

## [D09_MEETING_WIKI_FILE] Tên bảng: `wiki_page_export_history`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho wiki page export history; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** High Write  
**Phân vùng:** Có — partition theo created_at tháng/quý; tenant_id là sub-filter bắt buộc.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_wiki_page_export_history_tenant ON wiki_page_export_history(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_wiki_page_export_history_project ON wiki_page_export_history(project_id) — load dữ liệu theo project.`
- `idx_wiki_page_export_history_created_at ON wiki_page_export_history(created_at DESC) — phân trang/audit/log.`
- `idx_wiki_page_export_history_status ON wiki_page_export_history(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_wiki_page_export_history_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_wiki_page_export_history_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_wiki_page_export_history_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects

---

## [D09_MEETING_WIKI_FILE] Tên bảng: `files`

**Priority:** P0  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho files; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `storage_provider` | VARCHAR(30) | NOT NULL | 'minio' | local/minio/s3 |
| `bucket_name` | VARCHAR(120) | NOT NULL |  | Bucket/container |
| `object_key` | VARCHAR(700) | NOT NULL |  | Object key không chứa tên file gốc |
| `original_name` | VARCHAR(255) | NOT NULL |  | Tên gốc |
| `mime_type` | VARCHAR(150) | NOT NULL |  | MIME |
| `size_bytes` | BIGINT | NOT NULL | 0 | Dung lượng |
| `visibility` | VARCHAR(30) | NOT NULL | 'internal' | public/customer_safe/internal/private |
| `checksum_sha256` | CHAR(64) | NULL |  | Hash kiểm tra toàn vẹn |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_files_tenant ON files(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_files_created_at ON files(created_at DESC) — phân trang/audit/log.`

**Constraints:**
- FK fk_files_tenant → tenants(id) ON DELETE RESTRICT

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants

---

## [D09_MEETING_WIKI_FILE] Tên bảng: `file_versions`

**Priority:** P2  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho file versions; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_file_versions_tenant ON file_versions(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_file_versions_created_at ON file_versions(created_at DESC) — phân trang/audit/log.`
- `idx_file_versions_status ON file_versions(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_file_versions_tenant → tenants(id) ON DELETE RESTRICT
- CHECK ck_file_versions_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants

---

# D10_NOTIFICATION_WEBHOOK_INTEGRATION

| Bảng | Priority | Mục đích ngắn |
|---|---|---|
| `notifications` | P0 | Lưu dữ liệu nghiệp vụ cho notifications; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `webhooks` | P1 | Lưu dữ liệu nghiệp vụ cho webhooks; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `webhook_events` | P1 | Lưu dữ liệu nghiệp vụ cho webhook events; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `webhook_deliveries` | P1 | Lưu dữ liệu nghiệp vụ cho webhook deliveries; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `webhook_delivery_retries` | P1 | Lưu dữ liệu nghiệp vụ cho webhook delivery retries; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `webhook_secrets` | P1 | Lưu dữ liệu nghiệp vụ cho webhook secrets; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `webhook_ip_whitelist` | P1 | Lưu dữ liệu nghiệp vụ cho webhook ip whitelist; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `integrations` | P1 | Lưu dữ liệu nghiệp vụ cho integrations; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `integration_configs` | P1 | Lưu dữ liệu nghiệp vụ cho integration configs; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `integration_sync_logs` | P1 | Lưu dữ liệu nghiệp vụ cho integration sync logs; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `integration_field_mappings` | P1 | Lưu dữ liệu nghiệp vụ cho integration field mappings; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |


## [D10_NOTIFICATION_WEBHOOK_INTEGRATION] Tên bảng: `notifications`

**Priority:** P0  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho notifications; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_notifications_tenant ON notifications(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_notifications_created_at ON notifications(created_at DESC) — phân trang/audit/log.`
- `idx_notifications_status ON notifications(status) — filter theo trạng thái.`
- `idx_notifications_user_unread ON notifications(user_id, is_read, created_at DESC) — notification center.`

**Constraints:**
- FK fk_notifications_tenant → tenants(id) ON DELETE RESTRICT
- CHECK ck_notifications_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants

---

## [D10_NOTIFICATION_WEBHOOK_INTEGRATION] Tên bảng: `webhooks`

**Priority:** P1  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho webhooks; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_webhooks_tenant ON webhooks(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_webhooks_project ON webhooks(project_id) — load dữ liệu theo project.`
- `idx_webhooks_created_at ON webhooks(created_at DESC) — phân trang/audit/log.`
- `idx_webhooks_status ON webhooks(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_webhooks_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_webhooks_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_webhooks_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects

---

## [D10_NOTIFICATION_WEBHOOK_INTEGRATION] Tên bảng: `webhook_events`

**Priority:** P1  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho webhook events; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_webhook_events_tenant ON webhook_events(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_webhook_events_project ON webhook_events(project_id) — load dữ liệu theo project.`
- `idx_webhook_events_created_at ON webhook_events(created_at DESC) — phân trang/audit/log.`
- `idx_webhook_events_status ON webhook_events(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_webhook_events_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_webhook_events_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_webhook_events_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects

---

## [D10_NOTIFICATION_WEBHOOK_INTEGRATION] Tên bảng: `webhook_deliveries`

**Priority:** P1  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho webhook deliveries; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Không — log/audit immutable, chỉ archive theo retention policy.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo log |
| `archived_at` | TIMESTAMPTZ | NULL | NULL | Thời điểm archive theo retention nếu có |

**Indexes:**
- `idx_webhook_deliveries_tenant ON webhook_deliveries(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_webhook_deliveries_project ON webhook_deliveries(project_id) — load dữ liệu theo project.`
- `idx_webhook_deliveries_created_at ON webhook_deliveries(created_at DESC) — phân trang/audit/log.`
- `idx_webhook_deliveries_status ON webhook_deliveries(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_webhook_deliveries_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_webhook_deliveries_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_webhook_deliveries_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects

---

## [D10_NOTIFICATION_WEBHOOK_INTEGRATION] Tên bảng: `webhook_delivery_retries`

**Priority:** P1  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho webhook delivery retries; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** High Write  
**Phân vùng:** Có — partition theo created_at tháng/quý; tenant_id là sub-filter bắt buộc.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_webhook_delivery_retries_tenant ON webhook_delivery_retries(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_webhook_delivery_retries_project ON webhook_delivery_retries(project_id) — load dữ liệu theo project.`
- `idx_webhook_delivery_retries_created_at ON webhook_delivery_retries(created_at DESC) — phân trang/audit/log.`
- `idx_webhook_delivery_retries_status ON webhook_delivery_retries(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_webhook_delivery_retries_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_webhook_delivery_retries_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_webhook_delivery_retries_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects

---

## [D10_NOTIFICATION_WEBHOOK_INTEGRATION] Tên bảng: `webhook_secrets`

**Priority:** P1  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho webhook secrets; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_webhook_secrets_tenant ON webhook_secrets(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_webhook_secrets_project ON webhook_secrets(project_id) — load dữ liệu theo project.`
- `idx_webhook_secrets_created_at ON webhook_secrets(created_at DESC) — phân trang/audit/log.`
- `idx_webhook_secrets_status ON webhook_secrets(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_webhook_secrets_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_webhook_secrets_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_webhook_secrets_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects

---

## [D10_NOTIFICATION_WEBHOOK_INTEGRATION] Tên bảng: `webhook_ip_whitelist`

**Priority:** P1  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho webhook ip whitelist; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_webhook_ip_whitelist_tenant ON webhook_ip_whitelist(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_webhook_ip_whitelist_project ON webhook_ip_whitelist(project_id) — load dữ liệu theo project.`
- `idx_webhook_ip_whitelist_created_at ON webhook_ip_whitelist(created_at DESC) — phân trang/audit/log.`
- `idx_webhook_ip_whitelist_status ON webhook_ip_whitelist(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_webhook_ip_whitelist_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_webhook_ip_whitelist_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_webhook_ip_whitelist_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects

---

## [D10_NOTIFICATION_WEBHOOK_INTEGRATION] Tên bảng: `integrations`

**Priority:** P1  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho integrations; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_integrations_tenant ON integrations(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_integrations_created_at ON integrations(created_at DESC) — phân trang/audit/log.`
- `idx_integrations_status ON integrations(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_integrations_tenant → tenants(id) ON DELETE RESTRICT
- CHECK ck_integrations_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants

---

## [D10_NOTIFICATION_WEBHOOK_INTEGRATION] Tên bảng: `integration_configs`

**Priority:** P1  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho integration configs; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** High Read / Low Write  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_integration_configs_tenant ON integration_configs(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_integration_configs_created_at ON integration_configs(created_at DESC) — phân trang/audit/log.`
- `idx_integration_configs_status ON integration_configs(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_integration_configs_tenant → tenants(id) ON DELETE RESTRICT
- CHECK ck_integration_configs_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants

---

## [D10_NOTIFICATION_WEBHOOK_INTEGRATION] Tên bảng: `integration_sync_logs`

**Priority:** P1  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho integration sync logs; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** High Write  
**Phân vùng:** Có — partition theo created_at tháng/quý; tenant_id là sub-filter bắt buộc.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_integration_sync_logs_tenant ON integration_sync_logs(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_integration_sync_logs_created_at ON integration_sync_logs(created_at DESC) — phân trang/audit/log.`
- `idx_integration_sync_logs_status ON integration_sync_logs(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_integration_sync_logs_tenant → tenants(id) ON DELETE RESTRICT
- CHECK ck_integration_sync_logs_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Log là immutable, không soft delete; chỉ archive theo retention policy.
- Mọi thao tác admin hoặc thay đổi quyền/status/deadline phải ghi log.

**Quan hệ:**
- N-1 với tenants

---

## [D10_NOTIFICATION_WEBHOOK_INTEGRATION] Tên bảng: `integration_field_mappings`

**Priority:** P1  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho integration field mappings; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_integration_field_mappings_tenant ON integration_field_mappings(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_integration_field_mappings_created_at ON integration_field_mappings(created_at DESC) — phân trang/audit/log.`
- `idx_integration_field_mappings_status ON integration_field_mappings(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_integration_field_mappings_tenant → tenants(id) ON DELETE RESTRICT
- CHECK ck_integration_field_mappings_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants

---

# D11_AI_IMPORT

| Bảng | Priority | Mục đích ngắn |
|---|---|---|
| `ai_knowledge_documents` | P1 | Lưu dữ liệu nghiệp vụ cho ai knowledge documents; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `ai_knowledge_chunks` | P1 | Lưu chunk tri thức được index vào Qdrant phục vụ RAG sau khi đã gắn quyền truy cập. |
| `ai_knowledge_embeddings` | P1 | Lưu dữ liệu nghiệp vụ cho ai knowledge embeddings; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `ai_knowledge_sync_jobs` | P1 | Lưu dữ liệu nghiệp vụ cho ai knowledge sync jobs; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `ai_knowledge_permissions` | P1 | Lưu dữ liệu nghiệp vụ cho ai knowledge permissions; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `document_import_jobs` | P1 | Theo dõi pipeline import tài liệu PDF/DOCX/Markdown/CSV sang Wiki/Task backlog. |
| `document_import_sources` | P1 | Lưu dữ liệu nghiệp vụ cho document import sources; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `document_draft_tasks` | P0 | Lưu dữ liệu nghiệp vụ cho document draft tasks; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `document_draft_task_reviews` | P1 | Lưu dữ liệu nghiệp vụ cho document draft task reviews; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `document_import_audit_logs` | P0 | Lưu dữ liệu nghiệp vụ cho document import audit logs; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `document_draft_wiki_pages` | P0 | Lưu dữ liệu nghiệp vụ cho document draft wiki pages; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |


## [D11_AI_IMPORT] Tên bảng: `ai_knowledge_documents`

**Priority:** P1  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho ai knowledge documents; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_ai_knowledge_documents_tenant ON ai_knowledge_documents(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_ai_knowledge_documents_created_at ON ai_knowledge_documents(created_at DESC) — phân trang/audit/log.`
- `idx_ai_knowledge_documents_status ON ai_knowledge_documents(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_ai_knowledge_documents_tenant → tenants(id) ON DELETE RESTRICT
- CHECK ck_ai_knowledge_documents_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- AI chỉ được index/trả lời dữ liệu mà requester có quyền xem.
- Kết quả AI quan trọng phải yêu cầu human approval.

**Quan hệ:**
- N-1 với tenants

---

## [D11_AI_IMPORT] Tên bảng: `ai_knowledge_chunks`

**Priority:** P1  
**Mô tả:** Lưu chunk tri thức được index vào Qdrant phục vụ RAG sau khi đã gắn quyền truy cập.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_ai_knowledge_chunks_tenant ON ai_knowledge_chunks(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_ai_knowledge_chunks_created_at ON ai_knowledge_chunks(created_at DESC) — phân trang/audit/log.`
- `idx_ai_knowledge_chunks_status ON ai_knowledge_chunks(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_ai_knowledge_chunks_tenant → tenants(id) ON DELETE RESTRICT
- CHECK ck_ai_knowledge_chunks_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- AI chỉ được index/trả lời dữ liệu mà requester có quyền xem.
- Kết quả AI quan trọng phải yêu cầu human approval.

**Quan hệ:**
- N-1 với tenants

---

## [D11_AI_IMPORT] Tên bảng: `ai_knowledge_embeddings`

**Priority:** P1  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho ai knowledge embeddings; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_ai_knowledge_embeddings_tenant ON ai_knowledge_embeddings(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_ai_knowledge_embeddings_created_at ON ai_knowledge_embeddings(created_at DESC) — phân trang/audit/log.`
- `idx_ai_knowledge_embeddings_status ON ai_knowledge_embeddings(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_ai_knowledge_embeddings_tenant → tenants(id) ON DELETE RESTRICT
- CHECK ck_ai_knowledge_embeddings_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- AI chỉ được index/trả lời dữ liệu mà requester có quyền xem.
- Kết quả AI quan trọng phải yêu cầu human approval.

**Quan hệ:**
- N-1 với tenants

---

## [D11_AI_IMPORT] Tên bảng: `ai_knowledge_sync_jobs`

**Priority:** P1  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho ai knowledge sync jobs; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_ai_knowledge_sync_jobs_tenant ON ai_knowledge_sync_jobs(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_ai_knowledge_sync_jobs_created_at ON ai_knowledge_sync_jobs(created_at DESC) — phân trang/audit/log.`
- `idx_ai_knowledge_sync_jobs_status ON ai_knowledge_sync_jobs(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_ai_knowledge_sync_jobs_tenant → tenants(id) ON DELETE RESTRICT
- CHECK ck_ai_knowledge_sync_jobs_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- AI chỉ được index/trả lời dữ liệu mà requester có quyền xem.
- Kết quả AI quan trọng phải yêu cầu human approval.

**Quan hệ:**
- N-1 với tenants

---

## [D11_AI_IMPORT] Tên bảng: `ai_knowledge_permissions`

**Priority:** P1  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho ai knowledge permissions; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** High Read / Low Write  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_ai_knowledge_permissions_tenant ON ai_knowledge_permissions(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_ai_knowledge_permissions_created_at ON ai_knowledge_permissions(created_at DESC) — phân trang/audit/log.`
- `idx_ai_knowledge_permissions_status ON ai_knowledge_permissions(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_ai_knowledge_permissions_tenant → tenants(id) ON DELETE RESTRICT
- CHECK ck_ai_knowledge_permissions_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- AI chỉ được index/trả lời dữ liệu mà requester có quyền xem.
- Kết quả AI quan trọng phải yêu cầu human approval.

**Quan hệ:**
- N-1 với tenants

---

## [D11_AI_IMPORT] Tên bảng: `document_import_jobs`

**Priority:** P1  
**Mô tả:** Theo dõi pipeline import tài liệu PDF/DOCX/Markdown/CSV sang Wiki/Task backlog.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `source_file_id` | UUID | NULL |  | FK files.id |
| `status` | VARCHAR(40) | NOT NULL | 'uploaded' | uploaded/processing/extracted/reviewed/imported/failed |
| `import_mode` | VARCHAR(30) | NOT NULL | 'preview' | preview/commit |
| `progress_percent` | INTEGER | NOT NULL | 0 | Tiến độ job |
| `error_message` | TEXT | NULL |  | Lỗi cuối nếu có |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_document_import_jobs_tenant ON document_import_jobs(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_document_import_jobs_created_at ON document_import_jobs(created_at DESC) — phân trang/audit/log.`
- `idx_document_import_jobs_status ON document_import_jobs(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_document_import_jobs_tenant → tenants(id) ON DELETE RESTRICT
- CHECK ck_document_import_jobs_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants

---

## [D11_AI_IMPORT] Tên bảng: `document_import_sources`

**Priority:** P1  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho document import sources; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_document_import_sources_tenant ON document_import_sources(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_document_import_sources_created_at ON document_import_sources(created_at DESC) — phân trang/audit/log.`
- `idx_document_import_sources_status ON document_import_sources(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_document_import_sources_tenant → tenants(id) ON DELETE RESTRICT
- CHECK ck_document_import_sources_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants

---

## [D11_AI_IMPORT] Tên bảng: `document_draft_tasks`

**Priority:** P0  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho document draft tasks; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `task_id` | UUID | NULL |  | FK tasks.id nếu bản ghi gắn với task |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_document_draft_tasks_tenant ON document_draft_tasks(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_document_draft_tasks_project ON document_draft_tasks(project_id) — load dữ liệu theo project.`
- `idx_document_draft_tasks_task ON document_draft_tasks(task_id) — load dữ liệu theo task.`
- `idx_document_draft_tasks_created_at ON document_draft_tasks(created_at DESC) — phân trang/audit/log.`
- `idx_document_draft_tasks_status ON document_draft_tasks(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_document_draft_tasks_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_document_draft_tasks_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- FK fk_document_draft_tasks_task → tasks(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_document_draft_tasks_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects
- N-1 với tasks

---

## [D11_AI_IMPORT] Tên bảng: `document_draft_task_reviews`

**Priority:** P1  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho document draft task reviews; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `task_id` | UUID | NULL |  | FK tasks.id nếu bản ghi gắn với task |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_document_draft_task_reviews_tenant ON document_draft_task_reviews(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_document_draft_task_reviews_project ON document_draft_task_reviews(project_id) — load dữ liệu theo project.`
- `idx_document_draft_task_reviews_task ON document_draft_task_reviews(task_id) — load dữ liệu theo task.`
- `idx_document_draft_task_reviews_created_at ON document_draft_task_reviews(created_at DESC) — phân trang/audit/log.`
- `idx_document_draft_task_reviews_status ON document_draft_task_reviews(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_document_draft_task_reviews_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_document_draft_task_reviews_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- FK fk_document_draft_task_reviews_task → tasks(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_document_draft_task_reviews_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects
- N-1 với tasks

---

## [D11_AI_IMPORT] Tên bảng: `document_import_audit_logs`

**Priority:** P0  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho document import audit logs; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** High Write  
**Phân vùng:** Có — partition theo created_at tháng/quý; tenant_id là sub-filter bắt buộc.  
**Soft delete:** Không — log/audit immutable, chỉ archive theo retention policy.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo log |
| `archived_at` | TIMESTAMPTZ | NULL | NULL | Thời điểm archive theo retention nếu có |

**Indexes:**
- `idx_document_import_audit_logs_tenant ON document_import_audit_logs(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_document_import_audit_logs_created_at ON document_import_audit_logs(created_at DESC) — phân trang/audit/log.`
- `idx_document_import_audit_logs_status ON document_import_audit_logs(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_document_import_audit_logs_tenant → tenants(id) ON DELETE RESTRICT
- CHECK ck_document_import_audit_logs_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Log là immutable, không soft delete; chỉ archive theo retention policy.
- Mọi thao tác admin hoặc thay đổi quyền/status/deadline phải ghi log.

**Quan hệ:**
- N-1 với tenants

---

## [D11_AI_IMPORT] Tên bảng: `document_draft_wiki_pages`

**Priority:** P0  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho document draft wiki pages; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_document_draft_wiki_pages_tenant ON document_draft_wiki_pages(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_document_draft_wiki_pages_project ON document_draft_wiki_pages(project_id) — load dữ liệu theo project.`
- `idx_document_draft_wiki_pages_created_at ON document_draft_wiki_pages(created_at DESC) — phân trang/audit/log.`
- `idx_document_draft_wiki_pages_status ON document_draft_wiki_pages(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_document_draft_wiki_pages_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_document_draft_wiki_pages_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_document_draft_wiki_pages_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects

---

# D12_AUDIT_ANALYTICS_REPORTING

| Bảng | Priority | Mục đích ngắn |
|---|---|---|
| `audit_logs` | P0 | Lưu dữ liệu nghiệp vụ cho audit logs; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `compliance_reports` | P1 | Lưu dữ liệu nghiệp vụ cho compliance reports; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `project_analytics_snapshots` | P1 | Lưu dữ liệu nghiệp vụ cho project analytics snapshots; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `task_analytics_snapshots` | P1 | Lưu dữ liệu nghiệp vụ cho task analytics snapshots; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `sprint_analytics` | P1 | Lưu dữ liệu nghiệp vụ cho sprint analytics; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |
| `release_checklists` | P1 | Lưu dữ liệu nghiệp vụ cho release checklists; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization. |


## [D12_AUDIT_ANALYTICS_REPORTING] Tên bảng: `audit_logs`

**Priority:** P0  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho audit logs; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** High Write  
**Phân vùng:** Có — partition theo created_at tháng/quý; tenant_id là sub-filter bắt buộc.  
**Soft delete:** Không — log/audit immutable, chỉ archive theo retention policy.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `actor_id` | UUID | NULL |  | User thực hiện |
| `action_type` | VARCHAR(80) | NOT NULL |  | Loại hành động |
| `entity_type` | VARCHAR(80) | NOT NULL |  | Loại đối tượng |
| `entity_id` | UUID | NULL |  | ID đối tượng |
| `correlation_id` | VARCHAR(80) | NULL |  | Trace request |
| `ip_address` | INET | NULL |  | IP |
| `user_agent` | TEXT | NULL |  | User agent |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo log |
| `archived_at` | TIMESTAMPTZ | NULL | NULL | Thời điểm archive theo retention nếu có |

**Indexes:**
- `idx_audit_logs_tenant ON audit_logs(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_audit_logs_created_at ON audit_logs(created_at DESC) — phân trang/audit/log.`

**Constraints:**
- FK fk_audit_logs_tenant → tenants(id) ON DELETE RESTRICT

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Log là immutable, không soft delete; chỉ archive theo retention policy.
- Mọi thao tác admin hoặc thay đổi quyền/status/deadline phải ghi log.

**Quan hệ:**
- N-1 với tenants

---

## [D12_AUDIT_ANALYTICS_REPORTING] Tên bảng: `compliance_reports`

**Priority:** P1  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho compliance reports; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_compliance_reports_tenant ON compliance_reports(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_compliance_reports_project ON compliance_reports(project_id) — load dữ liệu theo project.`
- `idx_compliance_reports_created_at ON compliance_reports(created_at DESC) — phân trang/audit/log.`
- `idx_compliance_reports_status ON compliance_reports(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_compliance_reports_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_compliance_reports_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_compliance_reports_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects

---

## [D12_AUDIT_ANALYTICS_REPORTING] Tên bảng: `project_analytics_snapshots`

**Priority:** P1  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho project analytics snapshots; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** High Write  
**Phân vùng:** Có — partition theo created_at tháng/quý; tenant_id là sub-filter bắt buộc.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_project_analytics_snapshots_tenant ON project_analytics_snapshots(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_project_analytics_snapshots_project ON project_analytics_snapshots(project_id) — load dữ liệu theo project.`
- `idx_project_analytics_snapshots_created_at ON project_analytics_snapshots(created_at DESC) — phân trang/audit/log.`
- `idx_project_analytics_snapshots_status ON project_analytics_snapshots(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_project_analytics_snapshots_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_project_analytics_snapshots_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_project_analytics_snapshots_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects

---

## [D12_AUDIT_ANALYTICS_REPORTING] Tên bảng: `task_analytics_snapshots`

**Priority:** P1  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho task analytics snapshots; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** High Write  
**Phân vùng:** Có — partition theo created_at tháng/quý; tenant_id là sub-filter bắt buộc.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `task_id` | UUID | NULL |  | FK tasks.id nếu bản ghi gắn với task |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_task_analytics_snapshots_tenant ON task_analytics_snapshots(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_task_analytics_snapshots_project ON task_analytics_snapshots(project_id) — load dữ liệu theo project.`
- `idx_task_analytics_snapshots_task ON task_analytics_snapshots(task_id) — load dữ liệu theo task.`
- `idx_task_analytics_snapshots_created_at ON task_analytics_snapshots(created_at DESC) — phân trang/audit/log.`
- `idx_task_analytics_snapshots_status ON task_analytics_snapshots(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_task_analytics_snapshots_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_task_analytics_snapshots_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- FK fk_task_analytics_snapshots_task → tasks(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_task_analytics_snapshots_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects
- N-1 với tasks

---

## [D12_AUDIT_ANALYTICS_REPORTING] Tên bảng: `sprint_analytics`

**Priority:** P1  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho sprint analytics; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** High Write  
**Phân vùng:** Có — partition theo created_at tháng/quý; tenant_id là sub-filter bắt buộc.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `project_id` | UUID | NULL |  | FK projects.id nếu bản ghi thuộc project |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_sprint_analytics_tenant ON sprint_analytics(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_sprint_analytics_project ON sprint_analytics(project_id) — load dữ liệu theo project.`
- `idx_sprint_analytics_created_at ON sprint_analytics(created_at DESC) — phân trang/audit/log.`
- `idx_sprint_analytics_status ON sprint_analytics(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_sprint_analytics_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_sprint_analytics_project → projects(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_sprint_analytics_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với projects

---

## [D12_AUDIT_ANALYTICS_REPORTING] Tên bảng: `release_checklists`

**Priority:** P1  
**Mô tả:** Lưu dữ liệu nghiệp vụ cho release checklists; hỗ trợ audit, phân quyền tenant và mở rộng cấu hình theo project/organization.  
**Tần suất đọc/ghi:** Balanced  
**Phân vùng:** Không ở MVP; có thể partition theo tenant_id/created_at khi dữ liệu lớn.  
**Soft delete:** Có — dùng deleted_at, không hard delete dữ liệu nghiệp vụ.

| Cột | Kiểu dữ liệu | Null | Default | Mô tả |
|---|---|---|---|---|
| `id` | UUID | NOT NULL | gen_random_uuid() | Primary key |
| `tenant_id` | UUID | NOT NULL |  | FK tenants.id, phục vụ multi-tenant isolation |
| `task_id` | UUID | NULL |  | FK tasks.id nếu bản ghi gắn với task |
| `name` | VARCHAR(200) | NULL |  | Tên hiển thị hoặc nhãn nghiệp vụ |
| `code` | VARCHAR(100) | NULL |  | Mã định danh tùy chọn |
| `status` | VARCHAR(40) | NULL |  | Trạng thái nghiệp vụ |
| `metadata_json` | JSONB | NULL |  | Thông tin mở rộng có kiểm soát, dùng khi schema flexible |
| `created_by` | UUID | NULL |  | User tạo bản ghi |
| `updated_by` | UUID | NULL |  | User cập nhật gần nhất |
| `created_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL | NOW() | Thời điểm cập nhật |
| `deleted_at` | TIMESTAMPTZ | NULL | NULL | Soft delete |

**Indexes:**
- `idx_release_checklists_tenant ON release_checklists(tenant_id) — lọc theo tenant, chống cross-tenant access.`
- `idx_release_checklists_task ON release_checklists(task_id) — load dữ liệu theo task.`
- `idx_release_checklists_created_at ON release_checklists(created_at DESC) — phân trang/audit/log.`
- `idx_release_checklists_status ON release_checklists(status) — filter theo trạng thái.`

**Constraints:**
- FK fk_release_checklists_tenant → tenants(id) ON DELETE RESTRICT
- FK fk_release_checklists_task → tasks(id) ON DELETE CASCADE/RESTRICT tùy nghiệp vụ
- CHECK ck_release_checklists_status_known — status thuộc enum được định nghĩa ở Business Rules

**Business rules gắn với bảng:**
- Mọi truy vấn phải filter theo tenant_id từ session/claim, không nhận tenant_id tin cậy từ client nếu user không phải admin.
- Tạo/sửa/xóa phải kiểm tra quyền ở service layer.
- Bản ghi soft-deleted không xuất hiện trong query mặc định.

**Quan hệ:**
- N-1 với tenants
- N-1 với tasks

---

# TỔNG KẾT DATABASE

- Tổng số bảng: **188**.

## Bảng theo domain

- **D01_SYSTEM_TENANT:** 16 bảng.
- **D02_IDENTITY_AUTH:** 17 bảng.
- **D03_ORGANIZATION:** 12 bảng.
- **D04_PROJECT:** 17 bảng.
- **D05_TASK_WORKFLOW:** 23 bảng.
- **D06_EVIDENCE_APPROVAL_DEPENDENCY:** 10 bảng.
- **D07_KANBAN_SPRINT_TIMELINE:** 21 bảng.
- **D08_COMMENT_CHAT_REALTIME:** 20 bảng.
- **D09_MEETING_WIKI_FILE:** 24 bảng.
- **D10_NOTIFICATION_WEBHOOK_INTEGRATION:** 11 bảng.
- **D11_AI_IMPORT:** 11 bảng.
- **D12_AUDIT_ANALYTICS_REPORTING:** 6 bảng.

## ERD relationship map rút gọn

```text
tenants → organizations → projects → tasks
users ↔ organizations qua organization_members
users ↔ projects qua project_members
projects → sprints → sprint_tasks ↔ tasks
projects → kanban_boards → kanban_columns → kanban_cards ↔ tasks
tasks ↔ users qua task_assignments
tasks ↔ tasks qua task_dependencies
tasks → task_evidences → task_evidence_reviews
tasks → task_status_change_requests → task_status_change_approvals
projects → chat_rooms → chat_messages → chat_message_task_links ↔ tasks
projects → wiki_spaces → wiki_pages → wiki_page_versions
projects → meetings → meeting_action_items → tasks
files ↔ tasks/comments/wiki/chat/meeting qua target_entity/target_id hoặc attachment tables
projects/wiki/tasks/meetings → ai_knowledge_documents → ai_knowledge_chunks → Qdrant points
all business entities → audit_logs/audit_log_details
```

## Top 10 bảng quan trọng nhất

1. `tenants` — gốc multi-tenant, chống lẫn dữ liệu.
2. `users` — identity, session, quyền hệ thống.
3. `organizations` — workspace doanh nghiệp.
4. `projects` — đơn vị quản lý outsource.
5. `project_members` — quyền project, nền tảng RBAC tầng 3.
6. `tasks` — trung tâm tiến độ, giao việc, báo cáo.
7. `task_status_change_requests` — kiểm soát approval Nhật, không chuyển Done tùy tiện.
8. `chat_messages` — Mini Zalo và nguồn tạo task từ trao đổi.
9. `wiki_pages` / `wiki_page_versions` — knowledge base và versioning.
10. `audit_logs` — chứng cứ nghiệm thu, truy vết và chống bắt bẻ.

## Quyết định đặc biệt

- Dữ liệu workflow có bảng `workflow_*` để linh hoạt nhưng vẫn map về status chuẩn để báo cáo dễ hiểu.
- Dữ liệu AI tách `ai_knowledge_documents/chunks/permissions` để RAG không leak thông tin customer/internal/private.
- Document mining dùng `document_import_jobs + draft tables` để bắt buộc preview/review trước khi import thật.
- Chat/presence realtime không lưu typing vào DB lâu dài; `realtime_typing_indicators` chỉ là mô hình Redis key, không phải bảng PostgreSQL bắt buộc.
