# Hướng dẫn sử dụng QALY cho người dùng cuối

> Tài liệu hướng dẫn thực tế, bằng tiếng Việt, phù hợp với trạng thái hiện tại của hệ thống.

## 1. Mục tiêu

Hướng dẫn này dành cho người sử dụng cuối muốn làm việc với:

- tạo và quản lý nhóm,
- chat nội bộ,
- bình chọn (poll),
- cuộc họp nhóm,
- nhập tài liệu vào Wiki,
- hỏi AI,
- xem dashboard.

Tài liệu cố gắng thể hiện đúng khả năng hiện tại. Những tính năng chưa hoàn thiện sẽ được ghi rõ giới hạn.

## 2. Truy cập và đăng nhập

1. Mở ứng dụng QALY bằng URL demo.
2. Chọn `Đăng nhập`.
3. Nhập email và mật khẩu của tài khoản demo.
4. Sau khi đăng nhập, bạn sẽ vào trang `Dashboard` hoặc menu chính.

> Lưu ý: Nếu không đăng nhập, hệ thống sẽ không cho phép truy cập API, nhập tài liệu và chat.

## 3. Tạo nhóm và quản lý nhóm

### 3.1 Tạo nhóm mới

1. Chọn menu `Nhóm`.
2. Nhấn `Tạo nhóm mới`.
3. Nhập tên nhóm, mô tả, màu sắc và avatar nếu cần.
4. Nhấn `Lưu`.

Kết quả: nhóm mới được tạo và bạn mặc định là `Owner`.

### 3.2 Thêm hoặc xóa thành viên

1. Mở nhóm đã tạo.
2. Chọn tab `Thành viên`.
3. Thêm user bằng email hoặc userId.
4. Chọn role `Member` hoặc `Admin`.
5. Xóa thành viên hoặc thay đổi role nếu cần.

> Lưu ý: Chỉ `Owner`/`Admin` có quyền quản lý thành viên.

## 4. Chat nhóm

1. Vào trang nhóm.
2. Gõ tin nhắn vào ô chat.
3. Nhấn `Gửi`.

Tin nhắn sẽ hiển thị trong lịch sử chat và được cập nhật theo thời gian thực ở mức scope đã demo.

## 5. Tạo poll (bình chọn)

1. Vào nhóm.
2. Chọn `Bình chọn`.
3. Nhập tiêu đề, mô tả và các lựa chọn.
4. Đặt thời hạn nếu cần.
5. Nhấn `Tạo poll`.
6. Mời thành viên vote.

> Lưu ý: Poll đã được smoke test; không claim mọi tình huống realtime nâng cao.

## 6. Cuộc họp nhóm

### 6.1 Bắt đầu cuộc họp

1. Vào nhóm.
2. Chọn `Cuộc họp`.
3. Nhấn `Bắt đầu cuộc họp`.

### 6.2 Tham gia cuộc họp

1. Thành viên nhóm bấm `Tham gia`.
2. Outside user không phải thành viên sẽ bị chặn.

### 6.3 Kết thúc cuộc họp

1. Owner/Admin chọn `Kết thúc`.

> Giới hạn:
>
> - Tính năng participant realtime/count đang có lỗi P0 `DH03-BUG-MTG-001`.
> - Screen share chưa có positive case headful; nếu gặp lỗi, đây là trạng thái unsupported.

## 7. Import tài liệu vào Wiki

### 7.1 Import DOCX

1. Mở dự án.
2. Chọn `Import tài liệu`.
3. Upload file DOCX.
4. Xem preview nội dung.
5. Nhấn `Import`.

Kết quả: tạo trang Wiki hoặc nội dung Wiki mới.

### 7.2 Import ZIP bundle

1. Chọn `Upload ZIP`.
2. Chọn file ZIP chứa `.md`, `.txt`, `.html`, `.docx`.
3. Xem preview file.
4. Nhấn `Import`.

> Giới hạn:
>
> - File tối đa 5MB.
> - ZIP hỗ trợ `.md`, `.txt`, `.html`, `.docx`.
> - File unsupported sẽ bị skip và báo warning.

### 7.3 PDF

PDF hiện được xem là unsupported/roadmap nếu chưa có parser.

## 8. Hỏi AI

1. Mở `AI` hoặc `Erumi`.
2. Nhập câu hỏi.
3. Nhấn `Gửi`.
4. Chờ kết quả trả về.

Ví dụ:

- `Tóm tắt tiến độ dự án QALY.`
- `Nêu 3 rủi ro chính cho sprint này.`

> Lưu ý:
>
> - AI có thể chạy qua provider hoặc phản hồi fallback.
> - Nếu AI chậm, đây có thể là cấu hình provider chưa đầy đủ.

## 9. Xem dashboard

1. Chọn `Dashboard`.
2. Xem chỉ số dự án, công việc, tiến độ.
3. Đọc phần analytics nếu có.

## 10. Lưu ý

- Demo những tính năng đã có bằng chứng: nhóm, chat, poll, import DOCX/ZIP, meeting start/join/end, AI chat cơ bản.
- Không trình bày screen share hoặc PDF import như tính năng hoàn chỉnh.
- Nếu gặp lỗi `401` hoặc `403`, kiểm tra quyền đăng nhập và membership nhóm.
- Nếu AI chậm, báo cáo là `provider chưa cấu hình` hoặc `fallback đang hoạt động`.
