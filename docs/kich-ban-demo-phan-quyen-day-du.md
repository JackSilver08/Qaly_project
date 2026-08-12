# Kịch bản demo phân quyền đầy đủ — Qaly

> Cập nhật: 11/08/2026
> Thay thế phần "Điểm phân quyền cần thể hiện" trong `kich-ban-demo-01-hoan-chinh.md`,
> vốn né Reviewer/Viewer/Customer vì chưa xác minh được quyền.

Mục tiêu: demo **được** từng vai trò làm gì, thay vì mô tả bằng lời.

---

## 1. Nguyên tắc demo

- Mỗi vai trò mở bằng **một tài khoản riêng**, không giả lập bằng cách ẩn nút.
- Mở tab **Thành viên** đầu tiên ở mỗi tài khoản: thẻ *"Vai trò của bạn trong dự án"*
  liệt kê đúng những gì API sẽ cho phép.
- Khi cần chứng minh chặn, thao tác thật và cho xem lỗi `403`.

## 2. Tài khoản demo

Dự án: **Qaly Work OS - Customer Demo** (`qaly-workos-demo`)
Mật khẩu: `Qaly@123456` (admin: `Admin@123456`)

| Vai trò | Tài khoản | Bậc AI |
|---|---|---|
| Chủ dự án | `admin@qaly.dev` | Full |
| Quản lý dự án | `minh.anh@qaly.dev` | Full |
| Scrum Master | `bao.ngoc@qaly.dev` | Full |
| Lập trình viên | `linh.chi@qaly.dev` | Specialist |
| Kiểm thử viên | `tuan.kiet@qaly.dev` | Specialist |
| Người review | `thanh.tam@qaly.dev` | Specialist |
| Thành viên | `mai.phuong@qaly.dev` | Contributor |
| Người xem | `yen.nhi@qaly.dev` | ReadOnly |
| Khách hàng | `viet.long@qaly.dev` | ReadOnly |

## 3. Ma trận quyền

| Thao tác | Owner / Manager / Scrum | Dev / Tester / Reviewer | Member | Viewer / Customer |
|---|:---:|:---:|:---:|:---:|
| Sửa cấu hình dự án | ✅ | ❌ | ❌ | ❌ |
| Thêm / đổi vai trò thành viên | ✅ | ❌ | ❌ | ❌ |
| Giao và sắp xếp mọi task | ✅ | ❌ | ❌ | ❌ |
| Tạo task mới | ✅ | ✅ | ❌ | ❌ |
| Cập nhật task của mình | ✅ | ✅ | ✅ | ❌ |
| Bình luận / chấm công | ✅ | ✅ | ✅ | ❌ |
| Duyệt evidence | ✅ | Tester, Reviewer | ❌ | ❌ |
| Xem wiki nội bộ | ✅ | ✅ | ✅ | Viewer ✅ / Customer ❌ |
| Sửa wiki | ✅ | ✅ | ✅ | ❌ |
| Quản lý GitHub / webhook | ✅ | ❌ | ❌ | ❌ |

## 4. Ma trận AI

| Năng lực AI | Full | Specialist | Contributor | ReadOnly |
|---|:---:|:---:|:---:|:---:|
| Xem tiến độ, tóm tắt dự án | ✅ | ✅ | ✅ | ✅ |
| Tìm kiếm tri thức dự án | ✅ | ✅ | ✅ | ✅ |
| Chấm công, bình luận qua AI | ✅ | ✅ | ✅ | ❌ |
| Cập nhật trạng thái task của mình | ✅ | ✅ | ✅ (khi là assignee) | ❌ |
| Xem khối lượng công việc cả nhóm | ✅ | ✅ | ❌ | ❌ |
| Tạo task, đặt ưu tiên, đặt hạn | ✅ | ✅ | ❌ | ❌ |
| Đề xuất phân công, giao việc | ✅ | ❌ | ❌ | ❌ |
| Lập lại kế hoạch, xử lý chậm tiến độ | ✅ | ❌ | ❌ | ❌ |

> Điểm cần nhấn với khách: **mọi vai trò đều xem được tiến độ và tóm tắt** —
> kể cả Viewer và Customer. Trước đây tính năng này bị khoá ở mức quản lý.

## 5. Luồng demo 12 phút

**Bước 1 — Manager (`minh.anh@qaly.dev`) · 3 phút**
Tab Thành viên → thẻ vai trò hiện 11/11 quyền. Thêm một thành viên, chọn vai trò
*Lập trình viên* trong danh sách phân nhóm. Nhấn: vai trò chuyên sâu chọn ngay tại
màn hình dự án, không cần vòng qua trang admin.

**Bước 2 — Developer (`linh.chi@qaly.dev`) · 3 phút**
Thẻ vai trò hiện 7/11 quyền, phần quản lý bị gạch. Tạo task được, đổi vai trò
người khác thì không thấy nút. Hỏi AI *"khối lượng công việc nhóm thế nào?"* → trả lời được.

**Bước 3 — Member (`mai.phuong@qaly.dev`) · 3 phút**
Thẻ vai trò hiện 5/11. Điểm mấu chốt: **dự án hiện ra bình thường** (trước đây
danh sách rỗng do thiếu bản ghi thành viên tổ chức). Hỏi AI *"tiến độ dự án?"* → trả lời.
Hỏi *"ai đang rảnh nhất?"* → AI không có công cụ đó ở bậc này.

**Bước 4 — Viewer (`yen.nhi@qaly.dev`) · 2 phút**
Thẻ vai trò hiện 1/11. Vẫn xem được tiến độ và tóm tắt AI. Thử bình luận → không có ô nhập.

**Bước 5 — Customer (`viet.long@qaly.dev`) · 1 phút**
Giống Viewer nhưng wiki nội bộ bị ẩn hoàn toàn.

## 6. Bằng chứng tự động

| Bất biến | Test |
|---|---|
| Thành viên dự án thấy được dự án của tổ chức | `ProjectServiceTests.AddMemberAsync_WhenProjectBelongsToOrganization_GrantsOrganizationMembership` |
| Backfill không hạ quyền tổ chức sẵn có | `ProjectServiceTests.AddMemberAsync_WhenUserAlreadyHasElevatedOrganizationRole_DoesNotDowngradeIt` |
| Backfill không thành đường vòng cho non-manager | `ProjectServiceTests.AddMemberAsync_WhenCallerIsPlainMember_IsStillForbidden` |
| Vai trò lạ bị từ chối thay vì hạ ngầm về Member | `ProjectRoleRulesTests.TryNormalizeAssignableRole_ShouldRejectUnknownRoleInsteadOfDowngradingToMember` |
| Mọi vai trò đọc được tiến độ / tóm tắt | `AiProgressSummaryApiTests.Enqueue_AnyProjectMember_CanReadProgressSummary` |
| Người ngoài dự án vẫn bị chặn | `AiProgressSummaryApiTests.Enqueue_UserOutsideProject_IsDenied` |
| Viewer / Customer không có công cụ AI ghi | `AiCapabilityRulesTests.ReadOnlyRolesGetNoWriteTools` |
| Member không thấy khối lượng người khác | `AiCapabilityRulesTests.MemberSeesOwnWorkButNotOtherPeoplesWorkload` |
