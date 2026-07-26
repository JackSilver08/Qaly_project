# Kết quả triển khai quản lý người dùng và phân quyền

**Phiên bản:** QALY v4.0 Draft  
**Ngày hoàn thành:** 22/07/2026  
**Trạng thái:** Hoàn thành phạm vi P0–P2  
**Migration mới nhất:** `20260722145803_AddModeratorAssignments`

## 1. Mục tiêu

Tính năng được xây dựng để quản lý tài khoản toàn hệ thống, thành viên theo tổ chức và Moderator hỗ trợ có giới hạn. Thiết kế bảo đảm hệ thống chỉ có một Root Admin, đồng thời không biến Moderator thành quản trị viên toàn hệ thống.

## 2. Mô hình phân quyền đã triển khai

### 2.1. Cấp nền tảng

| Role | Phạm vi |
|---|---|
| `Admin` | Root Admin duy nhất, quản trị tài khoản và cơ chế ủy quyền toàn hệ thống |
| `Moderator` | Chỉ thao tác trong tổ chức và capability được Root Admin cấp |
| `Member` | Người dùng thông thường |

Database duy trì unique filtered index `UX_Users_SingleAdmin`, bảo đảm chỉ tồn tại một tài khoản có role `Admin`.

### 2.2. Cấp tổ chức

| Role | Ý nghĩa |
|---|---|
| `Owner` | Chủ sở hữu tổ chức; không thể bị hạ role hoặc gỡ bằng API thành viên thông thường |
| `OrganizationAdmin` | Quản lý tổ chức và thành viên |
| `PrivacyOperator` | Vai trò chuyên trách quyền riêng tư |
| `BillingAdmin` | Vai trò chuyên trách thanh toán |
| `Member` | Thành viên thông thường |

Các role tổ chức cũ `Admin` và `Manager` tiếp tục được nhận diện để tương thích dữ liệu, đồng thời được chuẩn hóa thành `OrganizationAdmin` khi cập nhật.

### 2.3. Capability của Moderator

| Capability | Quyền |
|---|---|
| `organization.users.view` | Xem danh sách thành viên của tổ chức được giao |
| `organization.users.invite` | Thêm tài khoản đang hoạt động vào tổ chức |
| `organization.users.update_role` | Cập nhật role thành viên |
| `organization.users.remove` | Gỡ thành viên khỏi tổ chức |

Mỗi assignment bao gồm Moderator, tổ chức, capability, người cấp quyền, thời hạn, trạng thái hoạt động và thời điểm thu hồi.

## 3. Chức năng đã hoàn thành

### 3.1. Quản lý người dùng toàn hệ thống

- Danh sách, tìm kiếm, lọc và phân trang người dùng.
- Tạo tài khoản `Member` hoặc `Moderator`.
- Cập nhật hồ sơ, role và trạng thái tài khoản.
- Khóa hoặc mở khóa tài khoản.
- Thu hồi toàn bộ phiên đăng nhập.
- Xem và quản lý membership dự án qua domain service.
- Import danh sách người dùng.
- Chuyển giao Root Admin bằng transaction riêng.
- Chỉ Root Admin truy cập được `/admin/users` và `api/admin/users`.

### 3.2. Quản lý thành viên tổ chức

- Trang giao diện: `/organizations/users`.
- Xem các tổ chức người dùng được phép truy cập.
- Xem và tìm kiếm thành viên.
- Thêm thành viên bằng email tài khoản đang hoạt động.
- Thay đổi role tổ chức.
- Gỡ thành viên bằng popup xác nhận.
- Không cho gán `Owner` qua API thông thường.
- Không cho sử dụng role dự án làm role tổ chức.
- Có loading state, empty state, responsive mobile và thông báo lỗi/thành công.

### 3.3. Quản lý phạm vi Moderator

- Trang dành cho Root Admin: `/admin/moderators`.
- Chọn Moderator, tổ chức và từng capability.
- Cấu hình thời điểm hết hạn hoặc không giới hạn thời gian.
- Thu hồi từng capability và có hiệu lực ngay.
- Hiển thị chỉ các thao tác tương ứng capability trên giao diện Moderator.
- Ghi audit log khi cấp hoặc thu hồi quyền.

## 4. API chính

### Root Admin

```text
GET    /api/admin/users
GET    /api/admin/users/{userId}
POST   /api/admin/users
PATCH  /api/admin/users/{userId}
POST   /api/admin/users/{userId}/revoke-sessions
GET    /api/admin/users/{userId}/projects
PATCH  /api/admin/users/{userId}/projects/{projectId}
DELETE /api/admin/users/{userId}/projects/{projectId}
POST   /api/admin/users/transfer-admin
POST   /api/admin/users/import

GET    /api/admin/moderator-assignments
POST   /api/admin/moderator-assignments
DELETE /api/admin/moderator-assignments/{assignmentId}
```

### Tổ chức

```text
GET    /api/organizations/{organizationId}/users
POST   /api/organizations/{organizationId}/users
PATCH  /api/organizations/{organizationId}/users/{userId}
DELETE /api/organizations/{organizationId}/users/{userId}
GET    /api/organizations/{organizationId}/moderator-capabilities
```

Các endpoint `/members` cũ vẫn được giữ để tương thích.

## 5. Kiểm soát bảo mật

- Moderator bị từ chối truy cập API quản lý người dùng toàn hệ thống.
- Middleware phía server chặn Moderator truy cập `/admin`.
- Menu quản trị toàn hệ thống chỉ hiển thị cho Root Admin.
- Assignment được kiểm tra trực tiếp từ database trên mỗi thao tác.
- Assignment hết hạn, bị vô hiệu hóa hoặc có `RevokedAt` sẽ không còn hiệu lực.
- Capability chỉ có hiệu lực trong đúng `OrganizationId`.
- Moderator không thể tự cấp quyền cho mình.
- Chỉ Root Admin có thể cấp hoặc thu hồi assignment.
- Thay đổi membership dự án từ trang Admin đi qua `IProjectService`.
- Quy tắc role tổ chức đã được tách khỏi `ProjectRoleRules`.
- Thao tác cấp và thu hồi quyền được ghi audit log.

## 6. Database và migration

Migration `20260722145803_AddModeratorAssignments` đã được tạo và áp dụng thành công.

Bảng `ModeratorAssignments` có:

- Foreign key tới Moderator, Organization và Root Admin cấp quyền.
- Unique index trên `ModeratorUserId + OrganizationId + Capability`.
- Index phục vụ truy vấn assignment đang hoạt động theo tổ chức và thời hạn.
- Quan hệ tổ chức dùng cascade delete; quan hệ người dùng dùng restrict delete.

Migration bảo đảm một Root Admin trước đó là `20260722141358_EnforceSingleAdminRole` và vẫn được giữ nguyên.

## 7. Kết quả kiểm thử

| Hạng mục | Kết quả |
|---|---|
| .NET solution build | Thành công, 0 warning, 0 error |
| Unit test phân quyền liên quan | 29/29 đạt |
| Integration test liên quan | 6/6 đạt |
| Vue TypeScript typecheck | Đạt |
| Vite production build | Đạt |
| `git diff --check` | Đạt |
| Migration database | Đã áp dụng thành công |

Các tình huống integration quan trọng đã được kiểm tra:

- Root Admin truy cập được API toàn hệ thống.
- Moderator và Member bị từ chối tại API toàn hệ thống.
- Thành viên tổ chức A không đọc được người dùng của tổ chức B.
- Moderator có capability `view` truy cập được đúng tổ chức được giao.
- Assignment của một tổ chức không mở quyền sang tổ chức khác.

## 8. Phạm vi chưa bao gồm

Phạm vi P0–P2 của tính năng quản lý người dùng đã hoàn thành. Các hạng mục sau thuộc pha bảo mật nâng cao riêng, không phải lỗi còn thiếu của P0–P2:

- MFA hoặc re-authentication bắt buộc trước khi chuyển Root Admin.
- Quy trình hai người phê duyệt cho phục hồi, hard-delete hoặc truy cập dữ liệu đặc biệt.
- Break-glass access có thời hạn.
- Cảnh báo tự động khi Moderator có hành vi bất thường.
- Báo cáo định kỳ về quyền sắp hết hạn.

## 9. Kết luận

Hệ thống hiện có một Root Admin duy nhất, quản trị người dùng theo cấp nền tảng, quản trị thành viên theo tổ chức và Moderator được ủy quyền theo nguyên tắc quyền tối thiểu. Ranh giới tenant và capability được cưỡng chế ở backend; frontend chỉ phản ánh những thao tác người dùng được phép thực hiện.
