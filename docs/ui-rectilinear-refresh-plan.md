# QALY Rectilinear UI Refresh

## Mục tiêu

- Đưa các card và panel về cùng một ngôn ngữ hình khối vuông, gọn và rõ.
- Giảm khoảng cách giữa các card để tăng mật độ thông tin nhưng vẫn giữ viền phân tách.
- Giữ giao diện sáng/tối nhất quán và không thay đổi luồng nghiệp vụ.
- Giữ bo góc mềm cho bình luận, hội thoại, vùng soạn nội dung và thao tác gửi file.

## Nguyên tắc thiết kế

1. Card và panel chính dùng góc vuông 0px, viền 1px và bóng nhẹ.
2. Khoảng cách card trong cùng cụm dùng 8px trên desktop, 6px trên mobile.
3. Khoảng cách giữa các phần nội dung dùng 10px trên desktop, 8px trên mobile.
4. Hover ưu tiên đổi viền và bóng; không nâng card bằng chuyển động theo trục dọc.
5. Avatar, badge trạng thái, progress bar và nút dạng pill không bị ép vuông.
6. Comment/message bubble dùng 14px; attachment/upload/dropzone dùng 12px.

## Phạm vi bản thử nghiệm

- Dashboard: summary, chart, attention, activity và performance surfaces.
- Projects: toolbar, grid/list card, detail panel, tabs và form panel.
- Tasks: board, kanban, list item và detail panel.
- Teams/Groups: workspace, sidebar, member/poll cards và chat shell.
- Analytics, Wiki, AI panels, search overlay, modal và toast.
- Light mode, dark mode và breakpoint mobile.
- Dashboard, Projects, Tasks và Wiki dùng toàn bộ chiều ngang khả dụng.
- Analytics mở rộng vùng hội thoại chính; Tasks ưu tiên chiều rộng cho danh sách.

## Lộ trình

### Giai đoạn 1 - Bản nhánh phụ

- Thêm token hình khối và mật độ ở stylesheet riêng.
- Chuẩn hóa card/panel phổ biến.
- Áp dụng danh sách ngoại lệ comment và gửi file.
- Chạy build, typecheck và smoke test.

### Giai đoạn 2 - Rà soát trực quan

- Chụp các màn hình Dashboard, Projects, Tasks, Teams, Analytics và Wiki.
- Kiểm tra card lồng nhau, overflow, focus ring và trạng thái empty/loading/error.
- Tinh chỉnh padding theo từng trang nếu mật độ quá cao.

### Giai đoạn 3 - Hợp nhất

- Thu phản hồi trên nhánh thử nghiệm.
- Chốt token vào design system chính.
- Loại bỏ các khai báo `border-radius` trùng hoặc xung đột trong stylesheet cũ.
- Merge sau khi smoke test desktop/mobile và light/dark đều đạt.

## Tiêu chí nghiệm thu

- Các card chính có góc 0px và khoảng cách cụm không vượt quá 8px trên desktop.
- Card vẫn phân biệt rõ nhờ viền, nền và elevation nhẹ.
- Comment, chat bubble, composer, attachment và upload không bị ép vuông.
- Không có thay đổi API, dữ liệu hoặc hành vi nghiệp vụ.
- `npm run typecheck`, `npm run build` và smoke test hoàn tất.
