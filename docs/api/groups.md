# API Workflow Trang Nhóm

Deadline P0: Chủ nhật 2026-05-31.

Mục tiêu của module nhóm là tạo một không gian bàn luận trước khi tạo dự án. Nhóm có thể chat realtime, quản lý thành viên, mở vote, mở meeting và tạo project mới từ toàn bộ thành viên trong nhóm.

## Backend Nền Đã Có

- Domain entities: `WorkGroup`, `WorkGroupMember`, `GroupInvitation`, `GroupMessage`, `GroupPoll`, `GroupPollOption`, `GroupPollVote`, `GroupMeetingSession`.
- Service contract: `IGroupsService`.
- Controller: `GroupsController`.
- SignalR hub: `GroupHub`.
- EF migration: `AddWorkGroups`.
- Project có thêm `SourceGroupId` để biết project được tạo từ nhóm nào.

## Role Nhóm

| Role | Quyền |
| --- | --- |
| Owner | Tạo/sửa/xóa nhóm, quản lý member, tạo project từ nhóm, mở vote/meeting |
| Admin | Quản lý member, tạo project từ nhóm, mở vote/meeting |
| Member | Xem nhóm, chat, vote, join meeting |

Mapping khi tạo project từ nhóm:

| Group role | Project role |
| --- | --- |
| Owner | Owner |
| Admin | Manager |
| Member | Member |

## REST API P0

### Danh sách nhóm của tôi

`GET /api/groups?page=1&pageSize=20&search=keyword`

Chỉ trả về nhóm mà user hiện tại là owner/member. System Admin có thể thấy tất cả.

### Chi tiết nhóm

`GET /api/groups/{groupId}`

Yêu cầu user là thành viên nhóm hoặc system admin.

### Tạo nhóm

`POST /api/groups`

```json
{
  "name": "Nhóm bàn dự án ERUMI",
  "description": "Bàn scope trước khi tạo project",
  "organizationId": null,
  "avatarUrl": null,
  "color": "#107C41"
}
```

User tạo nhóm tự động trở thành `Owner`.

### Cập nhật nhóm

`PUT /api/groups/{groupId}`

```json
{
  "name": "Nhóm bàn dự án ERUMI",
  "description": "Cập nhật mô tả",
  "avatarUrl": null,
  "color": "#107C41",
  "status": "Active"
}
```

Chỉ `Owner/Admin` được cập nhật.

### Xóa nhóm

`DELETE /api/groups/{groupId}`

Chỉ `Owner/Admin` được xóa. Project đã tạo từ nhóm sẽ được giữ lại.

### Danh sách thành viên

`GET /api/groups/{groupId}/members`

### Thêm user đã tồn tại vào nhóm

`POST /api/groups/{groupId}/members`

```json
{
  "userId": "00000000-0000-0000-0000-000000000000",
  "role": "Member"
}
```

Luồng invite bằng email sẽ dùng thêm `GroupInvitation` ở task của Duy Hoàng.

### Đổi role thành viên

`PATCH /api/groups/{groupId}/members/{userId}/role`

```json
{
  "role": "Admin"
}
```

Không được đổi role của owner nhóm qua endpoint này.

### Xóa thành viên / rời nhóm

`DELETE /api/groups/{groupId}/members/{userId}`

Member được rời nhóm bằng chính userId của mình. `Owner/Admin` được xóa member khác. Không được xóa owner nhóm.

### Lịch sử chat

`GET /api/groups/{groupId}/messages?page=1&pageSize=50`

Trả về message theo thứ tự cũ → mới trong trang hiện tại.

### Gửi message qua REST

`POST /api/groups/{groupId}/messages`

```json
{
  "content": "Mọi người chốt scope P0 trong hôm nay nhé.",
  "messageType": "Text"
}
```

Giới hạn `content` tối đa 4000 ký tự.

### Tạo project từ nhóm

`POST /api/groups/{groupId}/create-project`

```json
{
  "name": "ERUMI Group Workflow",
  "code": "ERUMI-GROUP",
  "description": "Project được tạo từ nhóm bàn luận",
  "startDate": null,
  "endDate": "2026-05-31T23:59:59+07:00"
}
```

Backend sẽ:

- tạo project mới;
- set `SourceGroupId`;
- tự thêm member trong nhóm vào project;
- deduplicate qua logic `ProjectService.AddMemberAsync`;
- trả về `warnings` nếu có user không add được.

## SignalR Hub

Hub URL:

`/hubs/groups`

### Join room nhóm

Client gọi:

```ts
connection.invoke("JoinGroup", groupId)
```

Server trả về cho caller:

`groupJoined`

```json
{
  "groupId": "..."
}
```

### Leave room nhóm

Client gọi:

```ts
connection.invoke("LeaveGroup", groupId)
```

Server trả về cho caller:

`groupLeft`

### Gửi message realtime

Client gọi:

```ts
connection.invoke("SendMessage", groupId, "Nội dung tin nhắn", "Text")
```

Server broadcast tới group:

`groupMessageReceived`

Payload là `GroupMessageDto`.

### Typing indicator

Client gọi:

```ts
connection.invoke("TypingStarted", groupId)
connection.invoke("TypingStopped", groupId)
```

Server gửi cho các client khác trong nhóm:

- `typingStarted`
- `typingStopped`

## Việc Team Khác Làm Tiếp

- Quang Minh: hoàn thiện invite email, meeting API, poll API, member management nâng cao.
- Duy Hoàng: notification/invite/poll vote.
- Gia Long + Đoàn Trung: frontend `/groups`, group detail, chat UI, member picker, meeting/vote UI.
- Quốc Bảo + Chí Khang: AI summary/action items từ chat/meeting, draft project/task từ thảo luận nhóm.
