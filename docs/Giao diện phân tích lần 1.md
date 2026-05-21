# Giao diện phân tích lần 1

Ngày cập nhật: 2026-05-21  
Phạm vi: Web app Qaly (Razor auth pages + Vue dashboard app)

## 1. Mục tiêu của lần phân tích này

Qaly là sản phẩm quản lý dự án và công việc, nên giao diện cần ưu tiên:

1. Rõ dữ liệu, thao tác nhanh, ít nhiễu thị giác.
2. Nhất quán giữa các trang Dashboard, Projects, Tasks, Teams, Profile.
3. Chuyên nghiệp theo chất B2B SaaS, không quá "marketing/hiệu ứng".
4. Đủ khả năng mở rộng cho bảng dữ liệu dày và nhiều vai trò người dùng.

## 2. Quan sát hiện trạng (từ codebase)

Các điểm nhận thấy:

1. Hệ giao diện đã có shell tốt (`TopHeader`, `SidebarNav`, `AppShell`) nhưng visual language chưa thống nhất hoàn toàn.
2. Token màu/spacing hiện đang nằm ở nhiều nơi (`ClientApp/style.css`, `ClientApp/style_header.css`, auth CSS inline), dễ lệch phong cách.
3. Trang đăng nhập có nhiều hiệu ứng 3D/gradient, mạnh về trang trí nhưng chưa đồng bộ với tinh thần "workspace vận hành".
4. Một số màn hình data-heavy (Task board, list dự án, task list) còn thiên về card, thiếu "chế độ vận hành dày dữ liệu" kiểu table-first khi cần.
5. Có tiềm năng rất tốt để nâng cấp theo hướng "enterprise control center".

## 3. Hướng phong cách đề xuất duy nhất

## Tên phong cách
**Precision Ops**

## Định vị
"Calm, data-first, enterprise clarity"  
Tức là nền trung tính dịu mắt + nhấn màu thương hiệu vừa đủ + tập trung vào luồng công việc.

## Vì sao phù hợp với Qaly

1. Người dùng quản lý dự án thường ở app nhiều giờ, cần nền dịu và phân cấp rõ hơn là hiệu ứng.
2. Màn hình nghiệp vụ (dự án, nhiệm vụ, thành viên) cần đọc nhanh, so sánh nhanh, thao tác nhanh.
3. Dễ mở rộng thêm phân tích rủi ro, timeline, KPI, AI suggestions mà không rối.

## 4. Tông nền chính và hệ màu đề xuất

## Kết luận tông nền chính
Chọn **Neutral Slate Light** làm nền gốc.

1. `--bg-app`: `#F4F6FA` (nền tổng thể)
2. `--bg-surface`: `#FFFFFF` (card/panel chính)
3. `--bg-sunken`: `#EEF2F7` (vùng kanban/list nền phụ)
4. `--line`: `#D8E0EA` (border/divider)
5. `--text-strong`: `#0F172A`
6. `--text`: `#334155`
7. `--text-muted`: `#64748B`

## Màu thương hiệu và semantic

1. `--brand`: `#2563EB` (CTA chính, selected state)
2. `--brand-hover`: `#1D4ED8`
3. `--brand-soft`: `#DBEAFE`
4. `--success`: `#0F766E`
5. `--warning`: `#B45309`
6. `--danger`: `#B42318`
7. `--info`: `#0C4A6E`

## Nguyên tắc dùng màu

1. Nền dùng neutral là chính, không dùng màu thương hiệu cho mảng lớn.
2. Brand color chỉ dùng cho hành động chính và trạng thái active.
3. Màu semantic luôn đi kèm icon/label, không truyền nghĩa chỉ bằng màu.

## 5. Typography đề xuất

## Cặp font

1. Heading/UI emphasis: `Plus Jakarta Sans` (600/700)
2. Body/data: `IBM Plex Sans` (400/500)

## Quy chuẩn chữ

1. Body: 14px, line-height 20px.
2. Dense table mode: 13px, line-height 18px.
3. H1 trang: 24px/32px.
4. H2 section: 18px/24px.
5. Số liệu KPI lớn: 28px/32px, bật tabular numbers.

## 6. Spacing, density và nhịp bố cục

1. Base unit: 8px.
2. Scale chính: 4, 8, 12, 16, 24, 32.
3. Padding card chuẩn: 16 hoặc 24.
4. Hỗ trợ 3 mức mật độ:
5. `Comfortable`: form/profile/wiki.
6. `Balanced`: dashboard/project overview.
7. `Compact`: tasks/table/board.

## 7. Layout tổng thể cho ứng dụng

## Desktop (>= 1280)

1. Sidebar trái cố định 240px, có chế độ thu gọn 80px.
2. Top header cao 64px, sticky.
3. Content area full width theo ngữ cảnh:
4. Trang bảng dữ liệu: ưu tiên full width.
5. Trang profile/form: max width 960-1120px để dễ đọc.

## Tablet (768-1279)

1. Sidebar chuyển sang overlay/drawer.
2. Header giữ sticky.
3. Các block KPI từ 4 cột về 2 cột.

## Mobile (< 768)

1. Single column.
2. Priority theo tác vụ: search, filter, action chính luôn ở vùng trên.
3. Bottom action bar cho thao tác thường dùng (tạo task, lọc, view switch).

## 8. Layout từng trang (đề xuất cụ thể)

## 8.1 Dashboard (`/dashboard`)

1. Hàng 1: 4 KPI card (Tiến độ, Quá hạn, Rủi ro, Chờ xử lý).
2. Hàng 2 trái: biểu đồ tiến độ dự án theo tuần.
3. Hàng 2 phải: "Attention feed" (task quá hạn/chưa xem/rủi ro cao).
4. Hàng 3: danh sách dự án active dạng table-lite (name, owner, status, due, progress).
5. Bỏ banner mang tính trang trí nặng, thay bằng module vận hành.

## 8.2 Projects (`/projects`)

1. Header trang: title + mô tả ngắn + primary action "Tạo dự án".
2. Thanh công cụ sticky: search, status filter, owner filter, sort, density toggle.
3. Có 2 chế độ hiển thị:
4. `Table mode` mặc định cho quản trị.
5. `Card mode` cho nhìn nhanh portfolio.
6. Row action rõ ràng: View, Edit, Archive.

## 8.3 Project Detail (`/projects/:projectId`)

1. Hero header rút gọn: tên, trạng thái, progress, mốc ngày.
2. Tab rõ nhóm nghiệp vụ: Tasks, Stats, Members, Wiki, Gantt, Webhooks.
3. Tasks tab dùng layout 2 pane:
4. Trái 65-70%: board/list.
5. Phải 30-35%: task detail inspector.
6. Cho phép chuyển `Board` <-> `Table` trong tab Tasks để làm việc dữ liệu dày.

## 8.4 Tasks (`/tasks`)

1. Mặc định table-first theo cá nhân (My Tasks).
2. Filter nhanh theo trạng thái, ưu tiên, due date.
3. Bulk action: đổi trạng thái, đổi assignee, gắn nhãn.
4. Row height tùy chọn `Balanced/Compact`.

## 8.5 Teams (`/teams`)

1. Chia 2 vùng:
2. Trái: danh sách nhóm.
3. Phải: luồng chat/channels.
4. Thêm module "Team workload snapshot" nhỏ phía trên, giúp gắn chat với năng lực thực thi.

## 8.6 Profile (`/profile`)

1. Dùng form card đơn giản, ít hiệu ứng.
2. Tách rõ 3 khối: Thông tin cá nhân, Bảo mật, Session/Thiết bị.
3. CTA nguy hiểm (đăng xuất tất cả phiên) dùng màu danger rõ ràng.

## 8.7 Archived Projects (`/projects/archived`)

1. Giữ table mode read-only.
2. Thêm filter theo thời gian archive.
3. Có cột "Lý do lưu trữ" để thuận quản trị.

## 8.8 Auth pages (`/Account/Login`, `/Account/Register`)

1. Giảm hiệu ứng 3D và blob animation.
2. Dùng split layout chuyên nghiệp:
3. Trái: form rõ ràng, trust indicators.
4. Phải: visual nhẹ + 2-3 lợi ích sản phẩm.
5. Đồng bộ palette với app chính để cảm giác liền mạch sau login.

## 9. Quy ước component để đồng bộ toàn hệ

1. Button:
2. Primary chỉ 1 loại nổi bật trên mỗi khu vực.
3. Secondary dạng neutral outline.
4. Input/select:
5. Border rõ, focus ring nhất quán.
6. Table:
7. Header sticky, sort rõ trạng thái, zebra rất nhẹ hoặc divider rõ.
8. Card:
9. Độ bo vừa phải (10-12px), shadow nhẹ, ưu tiên border hơn shadow nặng.

## 10. Motion và trạng thái tương tác

1. Duration chuẩn: 120ms, 180ms, 240ms.
2. Easing: ease-out cho mở rộng, ease-in-out cho chuyển trạng thái.
3. Focus state bắt buộc nhìn rõ bằng bàn phím.
4. Tránh animation dài ở trang làm việc chính.

## 11. Accessibility baseline cần chốt ngay

1. Tương phản văn bản tối thiểu theo WCAG AA.
2. Không truyền nghĩa chỉ bằng màu (cần icon/text đi kèm).
3. Focus indicator luôn nhìn thấy, không bị che.
4. Target click đủ lớn trên mobile.

## 12. Lộ trình triển khai đề xuất

## Pha 1: Foundation (1 sprint)

1. Chuẩn hóa design tokens (color, spacing, radius, shadow, typography).
2. Refactor CSS về một nguồn token.
3. Làm mới shell (header/sidebar/content frame).

## Pha 2: Core Screens (1-2 sprint)

1. Dashboard.
2. Projects list.
3. Project detail tasks tab (board + table switch).

## Pha 3: Remaining Screens (1 sprint)

1. Tasks page.
2. Teams.
3. Profile.
4. Archived.
5. Auth pages.

## Pha 4: QA và polish

1. Accessibility audit.
2. Responsive sweep.
3. Hiệu năng và perceived performance.

## 13. Kết luận quyết định cho lần 1

1. Chọn phong cách: **Precision Ops**.
2. Tông nền chính: **Neutral Slate Light** (`#F4F6FA`).
3. Màu nhấn thương hiệu: **Cobalt Blue** (`#2563EB`) dùng tiết chế.
4. Ưu tiên bố cục data-first, rõ hierarchy, giảm trang trí không phục vụ nghiệp vụ.
5. Đây là hướng phù hợp nhất để nâng cấp Qaly lên diện mạo B2B chuyên nghiệp và bền vững khi mở rộng tính năng.

## 14. Tài liệu tham chiếu

1. Atlassian Design - Color: https://atlassian.design/foundations/color
2. Atlassian Design - Spacing: https://atlassian.design/foundations/spacing
3. Atlassian Design - Elevation: https://atlassian.design/foundations/elevation/
4. Fluent 2 - Color: https://fluent2.microsoft.design/color
5. Fluent 2 - Color Tokens: https://fluent2.microsoft.design/color-tokens/
6. Fluent 2 - Layout: https://fluent2.microsoft.design/layout
7. Carbon - Data table usage: https://v10.carbondesignsystem.com/components/data-table/usage/
8. Carbon - 2x grid implementation: https://v10.carbondesignsystem.com/guidelines/2x-grid/implementation/
9. Carbon - Data table accessibility: https://carbondesignsystem.com/components/data-table/accessibility/
10. W3C WCAG 2.2: https://www.w3.org/TR/WCAG22/
