# OpenAPI Notes - QALY Project

> Tài liệu API chi tiết cho phạm vi P0: group, meeting, poll, import document, AI analytics.

## Chú ý chung

- Tất cả endpoint đều yêu cầu `Authorization: Bearer <token>` hoặc cookie đăng nhập hợp lệ.
- Backend sử dụng `Authorize` cho hầu hết API, nên nếu không login sẽ trả `401 Unauthorized`.
- Các endpoint nhóm/meeting/poll yêu cầu user là member/owner/admin của nhóm; nếu không có quyền sẽ trả `403 Forbidden`.
- Với tài liệu import, file giới hạn 5MB.
- AI analytics và group AI có thể trả lỗi do provider/fallback nếu backend chưa có provider cấu hình.

---

## 1. Group API (P0)

### 1.1 Danh sách nhóm của tôi

`GET /api/groups?page=1&pageSize=20&search={keyword}`

Authorization: bearer token.

Response lỗi phổ biến:

- `401 Unauthorized` nếu token/cookie không hợp lệ.
- `403 Forbidden` nếu user bị khóa hoặc không có quyền truy cập.

---

### 1.2 Chi tiết nhóm

`GET /api/groups/{groupId}`

Authorization: bearer token.

Response lỗi phổ biến:

- `400 Bad Request` nếu `groupId` không phải GUID hợp lệ.
- `401 Unauthorized` nếu chưa đăng nhập.
- `403 Forbidden` nếu user không phải thành viên/owner của nhóm.
- `404 Not Found` nếu nhóm không tồn tại.

---

### 1.3 Tạo nhóm

`POST /api/groups`

Payload mẫu:

```json
{
    "name": "Nhóm thảo luận QALY",
    "description": "Nhóm để chuẩn bị demo",
    "organizationId": null,
    "avatarUrl": null,
    "color": "#107C41"
}
```

Quyền truy cập: mọi user đã đăng nhập.

Response lỗi phổ biến:

- `400 Bad Request` nếu thiếu `name` hoặc payload sai định dạng.
- `401 Unauthorized` nếu chưa đăng nhập.

---

### 1.4 Cập nhật nhóm

`PUT /api/groups/{groupId}`

Payload mẫu:

```json
{
    "name": "Nhóm bàn luận ERUMI",
    "description": "Cập nhật mô tả",
    "avatarUrl": null,
    "color": "#0052CC",
    "status": "Active"
}
```

Quyền truy cập: chỉ `Owner/Admin` của nhóm.

Response lỗi phổ biến:

- `400 Bad Request` nếu payload sai định dạng.
- `401 Unauthorized` nếu chưa đăng nhập.
- `403 Forbidden` nếu user không đủ quyền.
- `404 Not Found` nếu nhóm không tồn tại.

---

### 1.5 Xóa nhóm

`DELETE /api/groups/{groupId}`

Quyền truy cập: chỉ `Owner/Admin`.

Response lỗi phổ biến:

- `401 Unauthorized` nếu chưa đăng nhập.
- `403 Forbidden` nếu không đủ quyền.
- `404 Not Found` nếu nhóm không tồn tại.

---

### 1.6 Quản lý thành viên

`GET /api/groups/{groupId}/members`

`POST /api/groups/{groupId}/members`

```json
{
    "userId": "00000000-0000-0000-0000-000000000000",
    "role": "Member"
}
```

`PUT /api/groups/{groupId}/members/{userId}` hoặc `PATCH /api/groups/{groupId}/members/{userId}/role`

```json
{
    "role": "Admin"
}
```

`DELETE /api/groups/{groupId}/members/{userId}`

Quyền truy cập: `Owner/Admin` mới có thể quản lý thành viên khác.

Response lỗi phổ biến:

- `400 Bad Request` nếu payload thiếu trường cần thiết.
- `401 Unauthorized` nếu chưa đăng nhập.
- `403 Forbidden` nếu không phải Owner/Admin.
- `404 Not Found` nếu group hoặc user không tồn tại.

---

### 1.7 Lịch sử chat nhóm

`GET /api/groups/{groupId}/messages?page=1&pageSize=50`

Quyền truy cập: thành viên nhóm.

Response lỗi phổ biến:

- `401 Unauthorized` nếu chưa đăng nhập.
- `403 Forbidden` nếu không phải thành viên.
- `404 Not Found` nếu nhóm không tồn tại.

---

### 1.8 Gửi message nhóm

`POST /api/groups/{groupId}/messages`

Payload mẫu:

```json
{
    "content": "Mọi người chốt scope P0 hôm nay nhé.",
    "messageType": "Text"
}
```

Quyền truy cập: thành viên nhóm.

Response lỗi phổ biến:

- `400 Bad Request` nếu `content` quá dài hoặc thiếu.
- `401 Unauthorized` nếu chưa đăng nhập.
- `403 Forbidden` nếu không phải thành viên.
- `404 Not Found` nếu nhóm không tồn tại.

---

### 1.9 Poll nhóm

`POST /api/groups/{groupId}/polls`

Payload mẫu:

```json
{
    "title": "Chọn ưu tiên sprint",
    "description": "Vote tính năng nào cần làm trước",
    "options": ["Option A", "Option B", "Option C"],
    "expiresAt": "2026-06-09T17:00:00+07:00"
}
```

`PUT /api/groups/{groupId}/polls/{pollId}`

`DELETE /api/groups/{groupId}/polls/{pollId}`

`POST /api/groups/{groupId}/polls/{pollId}/vote`

Payload mẫu:

```json
{
    "optionId": "11111111-1111-1111-1111-111111111111"
}
```

`PUT /api/groups/{groupId}/polls/{pollId}/close`

`GET /api/groups/{groupId}/polls/{pollId}/results`

Quyền truy cập: `Owner/Admin/Member` trong nhóm.

Response lỗi phổ biến:

- `400 Bad Request` nếu payload vote sai.
- `401 Unauthorized` nếu chưa đăng nhập.
- `403 Forbidden` nếu không phải thành viên nhóm.
- `404 Not Found` nếu group/poll không tìm thấy.

---

### 1.10 Tạo project từ nhóm

`POST /api/groups/{groupId}/create-project`

Payload mẫu:

```json
{
    "name": "Project Demo QALY",
    "code": "QALY-DEMO",
    "description": "Project tạo từ nhóm thảo luận",
    "startDate": "2026-06-09T08:00:00+07:00",
    "endDate": "2026-07-09T18:00:00+07:00"
}
```

Quyền truy cập: `Owner/Admin/Member`.

Response lỗi phổ biến:

- `400 Bad Request` nếu payload thiếu trường.
- `401 Unauthorized` nếu chưa đăng nhập.
- `403 Forbidden` nếu không phải thành viên.
- `404 Not Found` nếu group không tồn tại.

---

## 2. Meeting API (P0)

### 2.1 Start meeting

`POST /api/groups/{groupId}/meetings/start`

Quyền truy cập: thành viên nhóm có quyền mở cuộc họp.

Response lỗi phổ biến:

- `401 Unauthorized` nếu chưa đăng nhập.
- `403 Forbidden` nếu không phải thành viên hoặc không có quyền mở cuộc họp.
- `404 Not Found` nếu group không tồn tại.

---

### 2.2 Get active meeting

`GET /api/groups/{groupId}/meetings/active`

Quyền truy cập: thành viên nhóm.

Response lỗi phổ biến:

- `401 Unauthorized` nếu chưa đăng nhập.
- `403 Forbidden` nếu không phải thành viên.
- `404 Not Found` nếu group không tồn tại hoặc không có cuộc họp active.

---

### 2.3 Join meeting

`POST /api/groups/{groupId}/meetings/{meetingId}/join`

Quyền truy cập: thành viên nhóm.

Response lỗi phổ biến:

- `401 Unauthorized` nếu chưa đăng nhập.
- `403 Forbidden` nếu không phải thành viên.
- `404 Not Found` nếu group hoặc meeting không tồn tại.

---

### 2.4 End meeting

`POST /api/groups/{groupId}/meetings/{meetingId}/end`

Quyền truy cập: owner/admin hoặc người có quyền tương tự.

Response lỗi phổ biến:

- `401 Unauthorized` nếu chưa đăng nhập.
- `403 Forbidden` nếu không có quyền end meeting.
- `404 Not Found` nếu meeting không tồn tại.

---

### 2.5 Import Meetily meeting

`POST /api/meetings/import/meetily`

Payload mẫu:

```json
{
    "sourceUrl": "https://meetily.example.com/meeting/123",
    "accessToken": "..."
}
```

Quyền truy cập: đã đăng nhập.

Response lỗi phổ biến:

- `400 Bad Request` nếu payload thiếu thông tin.
- `401 Unauthorized` nếu chưa đăng nhập.
- `404 Not Found` nếu meeting/import source không tồn tại.

---

## 3. Import Document API (P0)

### 3.1 Preview document

`POST /api/import/documents/preview`

Form data:

- `file`: file upload

Quyền truy cập: đã đăng nhập.

Response lỗi phổ biến:

- `400 Bad Request` nếu file trống hoặc > 5MB.
- `401 Unauthorized` nếu chưa đăng nhập.
- `415 Unsupported Media Type` nếu định dạng file không được hỗ trợ.

---

### 3.2 Execute document import

`POST /api/import/documents/execute`

Form data:

- `file`: file upload
- `projectId`: GUID project
- `title`: optional title

Quyền truy cập: đã đăng nhập.

Response lỗi phổ biến:

- `400 Bad Request` nếu file trống, `projectId` không hợp lệ, hoặc payload JSON request không đúng.
- `401 Unauthorized` nếu chưa đăng nhập.
- `404 Not Found` nếu project không tồn tại.

---

### 3.3 Preview ZIP bundle

`POST /api/import/documents/zip/preview`

Form data:

- `file`: ZIP upload

Quyền truy cập: đã đăng nhập.

Response lỗi phổ biến:

- `400 Bad Request` nếu file trống hoặc > 5MB.
- `401 Unauthorized` nếu chưa đăng nhập.
- `415 Unsupported Media Type` nếu file không phải ZIP.

---

### 3.4 Execute ZIP bundle import

`POST /api/import/documents/zip/execute`

Form data:

- `file`: ZIP upload
- `projectId`: GUID project

Quyền truy cập: đã đăng nhập.

Response lỗi phổ biến:

- `400 Bad Request` nếu file trống, ZIP sai định dạng, hoặc projectId không hợp lệ.
- `401 Unauthorized` nếu chưa đăng nhập.
- `404 Not Found` nếu project không tồn tại.

---

### 3.5 Undo import session

`DELETE /api/import/sessions/{id}`

Quyền truy cập: đã đăng nhập.

Response lỗi phổ biến:

- `401 Unauthorized` nếu chưa đăng nhập.
- `403 Forbidden` nếu không có quyền undo session.
- `404 Not Found` nếu session không tìm thấy.

---

### 3.6 Import session history

`GET /api/import/sessions/{projectId}`

Quyền truy cập: đã đăng nhập.

Response lỗi phổ biến:

- `401 Unauthorized` nếu chưa đăng nhập.
- `404 Not Found` nếu project không tồn tại.

---

## 4. AI Analytics và Group AI API (P0)

### 4.1 AI project summary

`GET /api/ai/projects/{projectId}/summary`

Authorization: bearer token.

Response lỗi phổ biến:

- `401 Unauthorized` nếu chưa đăng nhập.
- `403 Forbidden` nếu user không có quyền xem project.
- `404 Not Found` nếu project không tồn tại.

---

### 4.2 AI project risks

`GET /api/ai/projects/{projectId}/risks`

Quyền truy cập: user có quyền project.

Response lỗi phổ biến:

- `401 Unauthorized`.
- `403 Forbidden`.
- `404 Not Found`.

---

### 4.3 AI project insights

`GET /api/ai/projects/{projectId}/insights`

Quyền truy cập: user có quyền project.

Response lỗi phổ biến:

- `401 Unauthorized`.
- `403 Forbidden`.
- `404 Not Found`.
- `503 Service Unavailable` nếu provider/fallback chưa sẵn sàng.

---

### 4.4 AI chat

`POST /api/ai/chat`

Payload mẫu:

```json
{
    "message": "Tóm tắt tiến độ dự án QALY",
    "projectId": "00000000-0000-0000-0000-000000000000",
    "mode": "erumi",
    "history": []
}
```

Response lỗi phổ biến:

- `400 Bad Request` nếu thiếu `message`.
- `401 Unauthorized` nếu chưa đăng nhập.
- `403 Forbidden` nếu không có quyền.
- `503 Service Unavailable` nếu AI provider không khả dụng.

---

### 4.5 AI smart search

`GET /api/ai/search?query={query}&projectId={projectId}`

Quyền truy cập: đã đăng nhập.

Response lỗi phổ biến:

- `400 Bad Request` nếu query trống.
- `401 Unauthorized`.
- `404 Not Found` nếu projectId không tồn tại.

---

### 4.6 Group AI action items

`POST /api/groups/{groupId}/ai/action-items`

Payload mẫu:

```json
{
    "context": "Họp nhóm thảo luận sprint 12",
    "message": "Tạo danh sách action items từ cuộc thảo luận"
}
```

Quyền truy cập: thành viên nhóm.

Response lỗi phổ biến:

- `400 Bad Request` nếu payload thiếu thông tin.
- `401 Unauthorized` nếu chưa đăng nhập.
- `403 Forbidden` nếu không phải thành viên nhóm.
- `404 Not Found` nếu nhóm không tồn tại.
- `503 Service Unavailable` nếu AI provider/fallback không sẵn sàng.

---

### 4.7 Group AI summarize discussion

`POST /api/groups/{groupId}/ai/summary`

Payload mẫu:

```json
{
    "discussionText": "Nội dung thảo luận..."
}
```

Quyền truy cập: thành viên nhóm.

Response lỗi phổ biến tương tự `action-items`.

---

### 4.8 Generate draft project payload

`POST /api/groups/{groupId}/ai/draft-project`

Payload mẫu:

```json
{
    "projectName": "Dự án mẫu",
    "description": "Tạo payload dự án từ thảo luận nhóm"
}
```

Quyền truy cập: thành viên nhóm.

Response lỗi phổ biến tương tự `action-items`.

---

## 5. Lưu ý thực tế cho API P0

- `401 Unauthorized` xảy ra khi token hết hạn hoặc chưa đăng nhập.
- `403 Forbidden` xảy ra khi người dùng không phải thành viên hoặc không có quyền hành động.
- `404 Not Found` thường do group/project/meeting/poll không tồn tại.
- Import document giới hạn file 5MB và có thể trả `415 Unsupported Media Type` cho định dạng chưa support.
- AI analytics có thể bị `503 Service Unavailable` nếu provider chưa cấu hình hoặc đang trong chế độ fallback.
- Các route group/poll/meeting phụ thuộc quyền nhóm nên cần kiểm tra role trước khi gọi API.
