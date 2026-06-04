# Demo seed data và API snippets

Tài liệu này chứa payload mẫu để tạo tổ chức, nhóm, thành viên, tin nhắn mẫu, bình chọn và dữ liệu hỗ trợ demo. Các snippet chỉ mang tính minh họa; cần thay `{orgId}`, `{groupId}`, `{userId}` theo dữ liệu thật trong cơ sở dữ liệu demo.

## 1. Tạo tổ chức

`POST /api/organizations`

```json
{
  "name": "Demo Org",
  "code": "demo-org"
}
```

## 2. Tạo nhóm

`POST /api/groups`

```json
{
  "name": "Demo Group",
  "description": "Nhóm dùng cho demo nghiệm thu",
  "organizationId": "{orgId}"
}
```

## 3. Thêm thành viên

Gọi một lần cho mỗi thành viên.

`POST /api/groups/{groupId}/members`

```json
{
  "userId": "{userId}",
  "role": "Member"
}
```

## 4. Gửi tin nhắn mẫu

`POST /api/groups/{groupId}/messages`

```json
{
  "content": "Chào nhóm demo, mình chốt scope P0 hôm nay nhé.",
  "messageType": "Text"
}
```

## 5. Tạo bình chọn mẫu

`POST /api/groups/{groupId}/polls`

```json
{
  "question": "Ngày nào phù hợp nhất để demo?",
  "options": ["Thứ hai", "Thứ năm", "Thứ sáu"],
  "allowMultiple": false
}
```

## 6. Cuộc họp

Có thể dùng UI tại `/groups/{groupId}/meeting` để kiểm tra luồng start/join/end cuộc họp theo quyền nhóm.

Giới hạn cần nói rõ khi demo:

- Participant realtime/count đang có lỗi P0 `DH03-BUG-MTG-001`; không demo participant count/list như tính năng ổn định nếu chưa fix.
- Screen share phụ thuộc browser/provider. Headless Chromium đã ghi nhận nhánh unsupported không crash nhưng UI feedback chưa rõ (`DH03-BUG-MTG-002`, P2).
- Nếu cần minh họa screen share positive, chạy bằng browser thật/headful và lưu bằng chứng riêng.

## Ghi chú

- Dùng tài khoản admin đã xác thực để gọi API.
- Không ghi mật khẩu, token, cookie hoặc secret vào tài liệu/evidence.
- Các snippet là minh họa; cần điều chỉnh theo organization, nhóm và user thật trong database demo.
