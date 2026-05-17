# Ke hoach: Quan ly timeline va canh bao task trong tung du an

> Yeu cau bat buoc khi trien khai code: tat ca ten hien thi, toast, thong bao loi, nhan nut, tooltip, empty state, notification va noi dung nghiep vu cua module nay phai viet bang tieng Viet ro rang. Khong de UI hien chu tieng Anh cho nguoi dung cuoi.

## 1. Dinh huong san pham

Module timeline task chi nam trong pham vi tung project. Khong them menu rieng vao sidebar.

Huong phan quyen moi:

- Role he thong chi co `Admin` va `User`.
- Vai tro cong viec nam theo tung project.
- Mot user co the la Developer o project nay, Tester o project khac, Scrum Master o project khac nua.
- Nguoi tao project mac dinh la PM hoac Project Owner cua project do.
- PM/Project Owner co quyen cap them quyen xem timeline/rui ro task cho mot so thanh vien trong project.

Vi vay, timeline task phai duoc thiet ke theo `projectId`, khong theo global user role.

## 2. Vi tri UI

Khong gan vao sidebar.

Them vao trang chi tiet project, duoi dang tab:

```text
Thong ke | Nhiem vu | Timeline | Thanh vien | Wiki | Webhooks
```

Tab `Timeline` gom:

1. Tong quan canh bao trong project.
2. Bang `Cong viec can chu y`.
3. Bieu do timeline/Gantt.
4. Bo loc theo assignee, reporter, status, priority, loai rui ro, khoang ngay.

Neu can route ky thuat, dung route theo project:

```text
/projects/{projectId}/timeline
/projects/{projectId}/tasks/attention
```

Nguoi dung van truy cap qua tab trong project detail, khong qua sidebar.

## 3. Phan quyen

### 3.1 Role he thong

Chi dung:

- `Admin`: toan quyen he thong.
- `User`: user binh thuong.

Khong dung role he thong de dien ta PM, Dev, Tester, Scrum Master.

### 3.2 Role trong project

Project role de xuat:

- `ProjectOwner`
- `PM`
- `ScrumMaster`
- `Developer`
- `Tester`
- `Reviewer`
- `Member`
- `Viewer`
- `Customer`

Day la vai tro theo tung project, khong phai role toan he thong.

### 3.3 Quyen xem timeline

Quyen xem chia thanh 2 muc:

1. Quyen xem task lien quan toi minh.
2. Quyen xem timeline/rui ro toan project.

Mac dinh:

| Doi tuong | Quyen mac dinh |
|---|---|
| System Admin | Xem tat ca |
| PM/Project creator | Xem timeline toan project va cau hinh quyen |
| ProjectOwner | Xem timeline neu la owner mac dinh hoac duoc PM cap |
| ScrumMaster | Xem timeline neu PM cap |
| Developer/Tester/Member | Chi xem task lien quan toi minh |
| Viewer/Customer | Khong xem rui ro noi bo, tru khi co che do rieng |

## 4. Co che cap quyen

### 4.1 MVP bang field tren ProjectMember

Co the them cac field vao `ProjectMember`:

```text
CanViewProjectTimeline bit default 0
CanViewTaskRisk bit default 0
CanNudgeAssignee bit default 0
CanViewUnseenTaskSignal bit default 0
```

PM/nguoi tao project mac dinh co cac quyen nay.

### 4.2 Nguong chuyen sang bang permission rieng

Field bit tren `ProjectMember` chi nen dung cho MVP.

Chot nguyen tac:

- Neu can them quyen thu 5 tro len, chuyen sang bang `ProjectMemberPermissions`.
- Neu quyen bat dau co scope phuc tap hon boolean, vi du theo module/task type/customer visibility, chuyen sang bang `ProjectMemberPermissions`.
- Neu can audit chi tiet tung lan cap/quyen bi thu hoi, chuyen sang bang `ProjectMemberPermissions`.

Bang de xuat:

```text
ProjectMemberPermissions
- Id uniqueidentifier
- ProjectId uniqueidentifier
- UserId uniqueidentifier
- PermissionKey nvarchar(100)
- IsEnabled bit
- GrantedByUserId uniqueidentifier
- GrantedAt datetimeoffset
- RevokedByUserId uniqueidentifier null
- RevokedAt datetimeoffset null
```

Voi plan hien tai, MVP co the dung 4 bit field. Nhung tai lieu code phai ghi ro day la MVP, khong mo rong vo han bang cach them lien tuc column moi.

## 5. Logic timeline task

Khong them `Overdue` vao `TaskItem.Status`. Overdue la trang thai suy dien tu ngay gio.

Tat ca timestamp luu trong database bang UTC:

- `StartDate`
- `DueDate`
- `AssignedAt`
- `ViewedAt`
- `LastSentAt`
- `ResolvedAt`

Hien thi theo timezone cua project neu co cau hinh `Project.TimeZoneId`; neu project chua co timezone thi hien theo timezone cua user; neu user chua co timezone thi fallback theo timezone server/dev. API nen tra UTC kem timezone display metadata neu can.

### 5.1 Cac flag rui ro

| Flag | Dieu kien |
|---|---|
| `IsDueSoon` | `DueDate` con trong nguong cau hinh, mac dinh 24 gio, va task chua `Done` |
| `IsOverdue` | `DueDate < nowUtc` va task chua `Done` |
| `IsStaleTodo` | `StartDate < nowUtc` va `Status = Todo` |
| `IsStaleInProgress` | `Status = InProgress` va da qua nguong tien do thoi gian, mac dinh 70% khoang `StartDate` -> `DueDate` |
| `IsUnseenByAssignee` | assignee duoc giao nhung chua co view hop le sau thoi diem assign gan nhat |
| `NeedsAttention` | co bat ky flag rui ro nao o tren |

### 5.2 Quy tac tinh "chua xem"

Khong chi check user da tung xem task hay chua. Phai tinh theo thoi diem assign.

Ly do: task co the ton tai tu truoc, user A tung xem task, sau do task duoc giao lai cho user A hoac cho user B. View cu khong duoc tinh la view cua lan assign moi.

De xuat model:

```text
TaskAssignments
- Id
- TaskItemId
- UserId
- AssignedAt datetimeoffset
- AssignedByUserId uniqueidentifier null
```

```text
TaskViewEvents
- Id
- TaskItemId
- UserId
- ViewedAt datetimeoffset
- ViewCount int
```

View hop le khi:

```text
TaskViewEvents.UserId = TaskAssignments.UserId
TaskViewEvents.TaskItemId = TaskAssignments.TaskItemId
TaskViewEvents.ViewedAt >= TaskAssignments.AssignedAt
```

Neu task bi remove assignee roi assign lai, `AssignedAt` phai cap nhat theo lan assign moi. Khi do view cu truoc `AssignedAt` khong con hop le.

## 6. Ai thay gi

### 6.1 Nguoi duoc giao task

Trong tab Timeline cua project, user duoc giao thay:

- Task cua minh sap toi han.
- Task cua minh da qua han.
- Task da toi ngay bat dau nhung van o `Todo`.
- Task dang o `InProgress` qua lau.

User khong can co quyen xem timeline toan project de xem task lien quan toi minh.

### 6.2 Nguoi giao task

Reporter thay:

- Task minh giao dang tre.
- Assignee chua mo task sau khi duoc giao.
- Task da toi start date nhung van chua bat dau.
- Task giu trang thai qua lau.

### 6.3 PM hoac nguoi duoc cap quyen

Nguoi co `CanViewProjectTimeline` va `CanViewTaskRisk` thay bang tong hop toan project:

- Tat ca task can chu y.
- Nhom theo assignee.
- Nhom theo reporter.
- Nhom theo risk type.
- Nhom theo priority/status.

## 7. Quick action trong Timeline

Tat ca quick action trong tab Timeline phai dung chung permission voi task detail. Khong duoc vi nam tren Timeline ma bo qua rule quan ly task.

Quy tac:

- Neu user co quyen thuc hien action, hien nut truc tiep.
- Neu user chi co quyen xem, hien nut `Mo chi tiet`.
- Neu action can them ngu canh hoac co rui ro, redirect sang task detail thay vi thao tac inline.

Action de xuat:

| Action | Ai duoc lam | Cach xu ly |
|---|---|---|
| `Bat dau lam` | Assignee hoac nguoi co quyen manage task | Goi update status neu hop le |
| `Chuyen cho duyet` | Assignee/PM theo workflow | Goi update status neu hop le |
| `Binh luan ly do tre` | Nguoi co quyen comment task | Mo modal comment hoac task detail |
| `Nhac nguoi phu trach` | PM/reporter/nguoi co `CanNudgeAssignee` | Tao notification rieng |
| `Doi han` | PM/nguoi co quyen manage task | Mo task detail hoac modal edit date |

MVP khuyen nghi: quick action chi nen gom `Mo chi tiet`, `Binh luan`, `Nhac nguoi phu trach`. Cac action doi status/date co the lam sau de tranh sai workflow.

## 8. API

### 8.1 Lay task can chu y theo project

```http
GET /api/projects/{projectId}/task-attention
```

Query:

```text
assigneeId
reporterId
status
priority
riskType
from
to
page
pageSize
sort
```

Pagination:

- MVP dung offset pagination: `page`, `pageSize`.
- `pageSize` mac dinh 25, toi da 100.
- Neu project lon va can infinite scroll sau nay, them cursor pagination sau, khong lam ngay trong MVP.

Sort mac dinh:

1. Task qua han lau nhat len truoc.
2. Task `Critical`/`High` len truoc.
3. `DueDate` ASC.
4. `StartDate` ASC.
5. `CreatedAt` DESC.

Gia tri `sort` de xuat:

| sort | Y nghia |
|---|---|
| `risk` | Mac dinh, sap theo muc do rui ro |
| `dueDate` | Han gan nhat truoc |
| `priority` | Critical/High truoc |
| `assignee` | Nhom theo nguoi phu trach |
| `status` | Nhom theo trang thai |

Response nen dung `PagedResult<TaskAttentionDto>`.

### 8.2 Danh dau da xem task

```http
POST /api/projects/{projectId}/tasks/{taskId}/viewed
```

Chi ghi view neu user co quyen xem task. Endpoint nay khong cap quyen moi.

### 8.3 Cap quyen xem timeline

MVP:

```http
PATCH /api/projects/{projectId}/members/{userId}/permissions
```

Body:

```json
{
  "canViewProjectTimeline": true,
  "canViewTaskRisk": true,
  "canNudgeAssignee": false,
  "canViewUnseenTaskSignal": true
}
```

Chi system admin, project creator, PM hoac ProjectOwner duoc cap quyen. Viec cap quyen phai ghi audit log.

## 9. DTO de xuat

```csharp
public sealed record TaskAttentionDto(
    Guid Id,
    string Title,
    Guid ProjectId,
    string ProjectName,
    string Status,
    string Priority,
    DateTimeOffset? StartDate,
    DateTimeOffset? DueDate,
    Guid ReporterId,
    string ReporterName,
    Guid? AssigneeId,
    string? AssigneeName,
    DateTimeOffset? AssignedAt,
    DateTimeOffset? LastViewedAt,
    bool IsDueSoon,
    bool IsOverdue,
    bool IsStaleTodo,
    bool IsStaleInProgress,
    bool IsUnseenByAssignee,
    string[] Reasons,
    string[] AllowedActions
);
```

`Reasons` tra ve ma ly do de frontend render tieng Viet thong nhat, vi du:

- `QuaHan`
- `SapToiHan`
- `ChuaBatDau`
- `DangLamQuaLau`
- `ChuaXem`

Frontend map sang text tieng Viet.

## 10. Notification va dedupe

### 10.1 Bang signal

```text
TaskAttentionSignals
- Id uniqueidentifier
- TaskItemId uniqueidentifier
- UserId uniqueidentifier
- SignalType nvarchar(50)
- FirstDetectedAt datetimeoffset
- LastSentAt datetimeoffset
- CooldownHours int
- ResolvedAt datetimeoffset null
```

Co the dung `CooldownHours` trong DB de moi signal tu quan ly tan suat gui. Neu muon don gian hon, de cooldown trong config va khong can column nay. Nhung phai chot mot cach, khong de worker moi nguoi implement mot kieu.

### 10.2 Tan suat mac dinh

| SignalType | Nguoi nhan | Cooldown mac dinh |
|---|---|---|
| `DueSoon` | Assignee | 24 gio |
| `Overdue` | Assignee + Reporter | 24 gio |
| `StaleTodo` | Assignee + Reporter | 24 gio |
| `StaleInProgress` | Assignee + Reporter | 48 gio |
| `Unseen` | Assignee + Reporter | 12 gio lan dau, sau do 24 gio |
| `ProjectDigest` | PM/nguoi co quyen | 24 gio |

Resolve signal khi:

- Task chuyen `Done`.
- Task khong con qua han/chua bat dau theo dieu kien.
- Assignee da xem task sau `AssignedAt`.
- Task bi xoa.
- Assignee bi remove khoi task.

### 10.3 Noi dung notification tieng Viet

Vi du:

- `Cong viec "X" da qua han 2 ngay. Vui long cap nhat trang thai hoac binh luan ly do.`
- `A chua mo cong viec "X" sau khi duoc giao.`
- `Du an "Y" co 5 cong viec can chu y hom nay.`

Khi code UI that, phai dung tieng Viet co dau.

## 11. Timezone

Quy uoc:

- Luu database bang UTC.
- So sanh deadline bang UTC.
- Hien thi theo timezone cua project neu co.
- Neu project khong co timezone, hien theo timezone cua user.
- Neu ca hai khong co, dung timezone mac dinh cua he thong.

Can tranh text mo ho nhu "hom nay" neu khong biet timezone. UI nen hien:

```text
Hom nay, 17:00 (GMT+7)
Qua han 2 ngay
Con 6 gio
```

Neu sau nay co customer nuoc ngoai, project nen co field:

```text
Project.TimeZoneId
```

## 12. Role 3 tang trong tuong lai

Plan nay chi implement theo project-level permission.

Kien truc tuong lai:

1. System:
   - `Admin`
   - `User`

2. Organization:
   - `OrgOwner`
   - `OrgAdmin`
   - `OrgMember`
   - `OrgViewer`

3. Project:
   - `ProjectOwner`
   - `PM`
   - `ScrumMaster`
   - `Developer`
   - `Tester`
   - `Reviewer`
   - `Member`
   - `Viewer`
   - `Customer`

Org role chi nen quyet dinh user co thuoc organization/project hay khong. Quyen xem timeline van nen theo project permission.

## 13. Lo trinh trien khai

### Phase 1 - Chuan hoa permission project

- Chot global role `Admin/User`.
- Bo sung project role va 4 flag permission MVP.
- Tao policy `ProjectTimelinePolicy`.
- Audit log khi cap/thu hoi quyen.

### Phase 2 - Attention query trong project

- Tao `TaskAttentionDto`.
- Tao endpoint `GET /api/projects/{projectId}/task-attention`.
- Implement filter, sort, offset pagination.
- Dam bao private task va task project khac khong bi leak.

### Phase 3 - Timeline UI trong project

- Them tab `Timeline` trong project detail.
- Them bang `Cong viec can chu y`.
- Them badge rui ro tren task card/list.
- Them Gantt co mau theo rui ro va duong ngay hom nay.

### Phase 4 - Tracking da xem task

- Them `AssignedAt` vao assignment neu chua co.
- Them `TaskViewEvents`.
- Goi mark viewed khi mo task detail.
- Tinh `Unseen` bang `ViewedAt >= AssignedAt`.

### Phase 5 - Notification worker

- Them `TaskAttentionSignals`.
- Them cooldown theo bang hoac config.
- Worker quet due soon/overdue/stale/unseen.
- Gui notification cho assignee, reporter, PM/nguoi co quyen.

## 14. Tieu chi nghiem thu

- Timeline nam trong project detail, khong xuat hien o sidebar.
- PM/nguoi tao project mac dinh xem va cau hinh duoc timeline permission.
- User thuong chi xem task lien quan toi minh neu chua duoc cap quyen toan project.
- User duoc cap `CanViewProjectTimeline` va `CanViewTaskRisk` xem duoc bang canh bao toan project.
- Task qua han hien cho assignee va reporter.
- Task toi start date nhung van `Todo` hien canh bao.
- Task assigned nhung assignee chua xem sau `AssignedAt` hien `Chua xem`.
- Neu assignee da xem task truoc khi duoc assign lai, view cu khong duoc tinh.
- Khi assignee mo task sau lan assign moi, `Chua xem` bien mat.
- Sort mac dinh dua task rui ro nhat len dau.
- Pagination khong tai qua 100 task moi request.
- Notification khong spam, ton trong cooldown.
- Tat ca ngay gio luu UTC va hien thi dung timezone.
- Quick action tren Timeline ton trong permission nhu task detail.
- Toan bo text UI/notification/toast cua module la tieng Viet ro rang.

