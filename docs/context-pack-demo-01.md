# Context Pack cho kịch bản demo số 1

> Phạm vi xác minh: source code và tài liệu trong repository QALY tại ngày 09/06/2026.  
> Mục đích: chuẩn hóa context để dùng ở prompt tiếp theo; tài liệu này **không phải kịch bản demo**.  
> Quy ước:
>
> - **Đã có**: có entity, service/API hoặc giao diện tương ứng trong source hiện tại.
> - **Chưa xác định**: chưa đủ bằng chứng để khẳng định hành vi end-to-end.
> - **Cần bổ sung**: chưa có trong hệ thống hoặc chưa đủ dữ liệu chuẩn bị demo.

## A. Thông tin tổng quan hệ thống

- **Tên hệ thống:** QALY.
- **Mục tiêu hệ thống:** quản lý dự án nội bộ theo một luồng thống nhất từ tạo dự án, tổ chức thành viên, phân công task, theo dõi tiến độ/rủi ro, cộng tác, nộp và duyệt minh chứng đến báo cáo và truy vết thao tác.
- **Đối tượng sử dụng thực tế:** quản trị hệ thống, chủ dự án/PM, quản lý dự án/Scrum Master, thành viên thực hiện, tester, người duyệt, viewer/customer và thành viên nhóm làm việc.
- **Bài toán hệ thống giải quyết:** dữ liệu dự án và trách nhiệm thường phân tán; người quản lý khó biết task đã được tiếp nhận chưa, task nào sắp trễ/quá hạn, ai cần được nhắc, kết quả nào đủ điều kiện nghiệm thu và ai đã thực hiện thay đổi.
- **Giá trị chính:**
  - Tập trung project, task, thành viên, deadline và trao đổi.
  - Phát hiện task cần chú ý: sắp tới hạn, quá hạn, chưa bắt đầu, làm quá lâu, người phụ trách chưa xem.
  - Kiểm soát quyền tại API và dữ liệu task riêng tư.
  - Buộc task có minh chứng được duyệt trước khi chuyển sang `Done`.
  - Gửi notification và ghi audit log cho các thao tác quan trọng.

## B. Danh sách vai trò trong hệ thống

### 1. System Admin

- **Tên role:** `Admin` ở cấp hệ thống.
- **Trách nhiệm:** quản trị người dùng và có quyền truy cập/quản lý rộng trên workspace.
- **Quyền chính:** bỏ qua nhiều kiểm tra membership; truy cập project/task; quản lý và duyệt minh chứng.
- **Không được phép làm:** vẫn phải tuân theo validation nghiệp vụ như trạng thái task hợp lệ, row version và yêu cầu evidence trước `Done`.
- **Tham gia demo số 1:** không bắt buộc; tránh dùng Admin để việc demo phân quyền có ý nghĩa.

### 2. Project Owner / PM

- **Tên role:** `Owner`; source cũng nhận diện các role quản lý như `Manager`, `ScrumMaster`, `PM`, `ProjectOwner`.
- **Trách nhiệm:** tạo và điều phối dự án, quản lý thành viên, giao việc, theo dõi rủi ro, nhắc người phụ trách, duyệt kết quả.
- **Quyền chính:** quản lý project/task, xem timeline và task risk, xem tín hiệu chưa đọc, nudge assignee, duyệt/từ chối evidence.
- **Không được phép làm:** không thể ép task sang trạng thái không hợp lệ; không thể đưa task sang `Done` khi chưa có evidence `Approved`.
- **Tham gia demo số 1:** có, là người điều phối chính.

### 3. Thành viên thực hiện

- **Tên role:** có thể lưu là `Member`, `Developer` hoặc `Tester`; quyền task thực tế còn phụ thuộc việc là reporter/assignee.
- **Trách nhiệm:** tiếp nhận task, cập nhật trạng thái, bình luận, tải kết quả/minh chứng.
- **Quyền chính:** xem project khi là thành viên; xem task công khai; quản lý task khi là reporter hoặc assignee; tải attachment và đánh dấu evidence khi có quyền task.
- **Không được phép làm:** không được xem task private nếu không phải owner/reporter/assignee; không được duyệt evidence chỉ dựa trên role `Developer`, `Tester` hoặc `Reviewer`; không được truy cập project ngoài membership.
- **Tham gia demo số 1:** có.

### 4. Reviewer

- **Tên role:** `Reviewer` tồn tại trong danh mục role.
- **Trách nhiệm dự kiến:** xem xét kết quả và phản hồi chất lượng.
- **Quyền chính hiện tại:** **chưa xác định theo role riêng**. Source duyệt evidence chỉ cho System Admin, Project Owner hoặc role quản lý dự án.
- **Không được phép làm:** một user chỉ mang role `Reviewer` chưa chắc có quyền duyệt evidence.
- **Tham gia demo số 1:** không dùng role `Reviewer` độc lập; dùng PM/Owner làm reviewer để đúng hành vi hiện tại.

### 5. Viewer / Customer

- **Tên role:** `Viewer`, `Customer`.
- **Trách nhiệm:** theo dõi thông tin được phép xem.
- **Quyền chính:** xem project/task công khai khi có membership; `Customer` không được đọc Wiki nội bộ.
- **Không được phép làm:** không có quyền quản lý project; không được xem task private nếu không thuộc nhóm được phép; không nên được dùng để chỉnh sửa dữ liệu.
- **Tham gia demo số 1:** tùy chọn, chỉ dùng cho một bước kiểm tra `403` nếu đủ thời gian.

### 6. Group Owner / Admin / Member

- **Tên role:** `Owner`, `Admin`, `Member` ở module Nhóm.
- **Trách nhiệm:** tổ chức nhóm, thành viên, poll, chat và meeting.
- **Quyền chính:** Owner/Admin quản lý nhóm và tạo poll; Member tham gia nội dung nhóm.
- **Không được phép làm:** Member không quản lý role/thành viên; outside user không được dùng tài nguyên nhóm.
- **Tham gia demo số 1:** không cần, vì demo 3–4 phút nên ưu tiên core Project/Task.

## C. Danh sách module/chức năng hiện có

| Module | Mục đích | Role được dùng | Dữ liệu vào | Dữ liệu ra/thay đổi | Có trong demo #1 |
|---|---|---|---|---|---|
| Authentication/User | Đăng nhập và xác định danh tính/role | Tất cả user | Email, mật khẩu | Phiên đăng nhập, current user | Có, nhưng đăng nhập nên chuẩn bị trước |
| Dashboard | Tổng hợp project, task, overdue, blocked, attention | User có quyền dữ liệu | Dữ liệu project/task hiện có | Chỉ số và cảnh báo | Có |
| Project | Tạo/sửa/lưu trữ dự án, quản lý thành viên | Owner/Manager/Admin | Tên, mô tả, ngày, member | Project và membership | Có, ưu tiên dùng seed |
| Task/Kanban | Tạo, giao, cập nhật và di chuyển trạng thái | Owner, manager, reporter, assignee | Tiêu đề, mô tả, priority, assignee, deadline, status | Task, row version, Kanban | Có |
| Task Attention/Gantt | Phát hiện due soon, overdue, stale, unseen; nhắc assignee | Người có quyền timeline/risk/nudge | Deadline, start date, status, assignment, view event | Attention item, notification nudge | Có |
| Comment | Trao đổi và ghi lý do xử lý | User có quyền task | Nội dung comment | Comment và activity liên quan | Có, nếu đủ thời gian |
| Attachment/Evidence | Nộp file và kiểm soát điều kiện hoàn tất | Người có quyền task; manager duyệt | File, cờ evidence, review note | `Pending/Approved/Rejected`; notification | Có |
| Notification | Báo giao task, đổi trạng thái, review, nudge, deadline | User nhận thông báo | Event nghiệp vụ | Notification read/unread | Có |
| Audit Log/Recent Activity | Truy vết thao tác | User có quyền endpoint tương ứng | Hành động hệ thống | Action, entity, changes, actor, time | Có thể chỉ nêu/cho xem nhanh |
| Sprint | Gom task theo khoảng thời gian | User có quyền project | Tên, ngày, goal, status | Sprint và task membership | Không cần |
| Dependency/Gantt | Biểu diễn quan hệ task, chặn vòng lặp | Người quản lý task/project | Predecessor, successor, type | Dependency và critical path/timeline | Không cần cho demo ngắn |
| Time Entry/Timer | Theo dõi thời gian thực tế | User có quyền task | Start/stop hoặc số giờ | Time entry, actual hours | Không cần |
| Analytics/Report | Tổng hợp tiến độ, workload, overdue | User có quyền project | Dữ liệu hiện có | Chỉ số/báo cáo | Chỉ dùng Dashboard |
| Group/Chat/Poll/Meeting | Cộng tác nhóm và tạo project từ group | Group members | Group, message, poll, meeting | Dữ liệu cộng tác | Tránh trong demo #1 |
| Import | Nhập task/wiki từ file, preview và undo | User có quyền đích | CSV/XLSX/JSON/DOCX/ZIP... | Task hoặc Wiki, import session | Tránh trong demo #1 |
| Wiki | Tài liệu dự án | Project members theo quyền | Nội dung/file import | Wiki page | Tránh trong demo #1 |
| AI/Erumi | Tóm tắt, phân tích và gợi ý | User có quyền context | Prompt và dữ liệu dự án | Phản hồi AI/fallback | Tránh vì không phải core bắt buộc |
| Webhook | Gửi sự kiện ra ngoài | Role quản lý | Endpoint/event | Delivery log | Tránh |
| Milestone | Theo dõi mốc nghiệp vụ | **Không có entity/module hiện hành** | Không có | Không có | Không |

## D. Các entity/dữ liệu chính

### User

- **Trường quan trọng:** `Id`, `FullName`, `Email`, `Role`, `IsActive`, `AvatarUrl`.
- **Trạng thái:** active/inactive qua `IsActive`.
- **Quan hệ:** project owner/member, task reporter/assignee, comment author, uploader/reviewer, notification receiver, audit actor.

### Project

- **Trường quan trọng:** `Id`, `Name`, `Code`, `Description`, `Status`, `StartDate`, `EndDate`, `OwnerId`, `OrganizationId`, `SourceGroupId`.
- **Trạng thái:** mặc định `Active`; UI hiện dùng `Archived`. Các trạng thái project khác chưa thấy validation workflow trong source chính.
- **Quan hệ:** Owner, Members, Tasks, Sprints, Labels, Attachments.

### ProjectMember

- **Trường quan trọng:** `ProjectId`, `UserId`, `Role`, `JoinedAt`, `CanViewProjectTimeline`, `CanViewTaskRisk`, `CanNudgeAssignee`, `CanViewUnseenTaskSignal`.
- **Trạng thái:** không có lifecycle riêng.
- **Quan hệ:** nối User với Project và lưu quyền bổ sung.

### TaskItem

- **Trường quan trọng:** `Title`, `Description`, `Status`, `Priority`, `StartDate`, `DueDate`, `EstimatedHours`, `ActualHours`, `IsPrivate`, `IsPinned`, `RowVersion`, `ProjectId`, `SprintId`, `AssigneeId`, `ReporterId`.
- **Trạng thái:** `Todo`, `InProgress`, `OnHold`, `InReview`, `Done`, `Cancelled`.
- **Quan hệ:** Project, Sprint, reporter, assignee(s), comments, attachments, labels, dependencies, view events.

### TaskAssignment

- **Trường quan trọng:** `TaskItemId`, `UserId`, `AssignedAt`, `AssignedByUserId`.
- **Trạng thái:** không có trạng thái nhận/từ chối assignment.
- **Quan hệ:** Task, assignee, người giao.

### TaskViewEvent

- **Trường quan trọng:** `TaskItemId`, `UserId`, `ViewedAt`, `ViewCount`.
- **Trạng thái:** không có.
- **Quan hệ:** dùng để xác định assignee đã xem task sau thời điểm được giao hay chưa.

### TaskAttentionSignal

- **Trường quan trọng:** `TaskItemId`, `UserId`, `SignalType`, `FirstDetectedAt`, `LastSentAt`, `CooldownHours`, `ResolvedAt`.
- **Trạng thái:** active khi `ResolvedAt` rỗng; resolved khi có thời điểm xử lý.
- **Quan hệ:** Task và User.

### TaskComment

- **Trường quan trọng:** `Content`, `TaskItemId`, `AuthorId`, `ParentCommentId`, `CreatedAt`, `IsDeleted`.
- **Trạng thái:** active/soft-deleted.
- **Quan hệ:** Task, author, replies, attachments.

### TaskAttachment

- **Trường quan trọng:** `FileName`, `FilePath`, `FileSize`, `ContentType`, `Scope`, `IsEvidence`, `EvidenceApprovalStatus`, `EvidenceReviewedById`, `EvidenceReviewedAt`, `EvidenceReviewNote`, `UploadedById`.
- **Trạng thái evidence:** `None`, `Pending`, `Approved`, `Rejected`.
- **Quan hệ:** Task hoặc Project hoặc Comment; uploader; reviewer.

### Notification

- **Trường quan trọng:** `Message`, `Type`, `Tone`, `IsRead`, `RelatedEntityId`, `RelatedEntityType`, `IdempotencyKey`, `UserId`.
- **Trạng thái:** unread/read.
- **Quan hệ:** User và entity nghiệp vụ liên quan.

### AuditLog

- **Trường quan trọng:** `Action`, `EntityType`, `EntityId`, `ChangesJson`, `UserId`, `IpAddress`, `Timestamp`.
- **Trạng thái:** append-only theo thiết kế sử dụng; không có trạng thái nghiệp vụ.
- **Quan hệ:** actor User và entity được ghi bằng type/id.

### Sprint

- **Trường quan trọng:** `Name`, `StartDate`, `EndDate`, `Status`, `Goal`, `ProjectId`.
- **Trạng thái:** `Planning`, `Active`, `Completed`, `Cancelled`.
- **Quan hệ:** Project và Tasks.

### TaskDependency

- **Trường quan trọng:** `PredecessorId`, `SuccessorId`, `DependencyType`.
- **Trạng thái:** không có.
- **Quan hệ:** nối hai Task trong cùng project.

### Milestone

- **Kết luận:** không có entity `Milestone` trong domain hiện tại; không dùng trong luồng demo chính.

## E. Trạng thái nghiệp vụ quan trọng

### Project

- **`Active`:** trạng thái mặc định khi tạo project.
- **`Archived`:** được UI dùng để tách dự án lưu trữ và có luồng restore.
- **Ai thay đổi:** Owner/role quản lý project theo các API hiện hành.
- **Điều kiện chuyển:** chưa thấy state machine project được enforce tương tự task; các trạng thái `OnHold`, `Completed`, `Cancelled` xuất hiện trong tài liệu thiết kế nhưng **chưa xác định là workflow production hiện tại**.

### Milestone

- **Trạng thái:** không áp dụng vì chưa có module/entity.

### Task

| Trạng thái | Khi xuất hiện | Ai có thể thay đổi | Chuyển hợp lệ tiếp theo |
|---|---|---|---|
| `Todo` | Task mới/chưa bắt đầu | User có quyền quản lý task | `InProgress`, `OnHold`, `Cancelled` |
| `InProgress` | Đã bắt đầu thực hiện | User có quyền quản lý task | `InReview`, `OnHold`, `Cancelled`, `Done` |
| `InReview` | Chờ kiểm tra kết quả | User có quyền quản lý task | `InProgress`, `Done`, `OnHold`, `Cancelled` |
| `OnHold` | Bị chặn/tạm dừng | User có quyền quản lý task | `Todo`, `InProgress`, `Cancelled` |
| `Done` | Hoàn tất và có evidence `Approved` | User có quyền quản lý task | Có thể quay lại `InReview` |
| `Cancelled` | Hủy bỏ | User có quyền quản lý task | Có thể quay lại `Todo` |

- Mọi lần chuyển sang `Done` từ trạng thái khác đều bị chặn nếu task chưa có attachment được đánh dấu evidence và có `EvidenceApprovalStatus = Approved`.
- Cập nhật Kanban dùng `RowVersion`; dữ liệu cũ gây `409 Conflict` và yêu cầu refresh.

### Notification

- **Unread:** `IsRead = false` khi được tạo.
- **Read:** user đánh dấu đã đọc.
- **Ai thay đổi:** người nhận notification.
- **Điều kiện:** notification được sinh từ event như giao task, đổi trạng thái, comment, review evidence, nudge, deadline reminder.

### Review/Approval

- **`None`:** attachment chưa phải evidence.
- **`Pending`:** attachment được đánh dấu evidence và chờ duyệt.
- **`Approved`:** manager/owner/admin chấp nhận evidence; task đủ một điều kiện để chuyển `Done`.
- **`Rejected`:** evidence bị từ chối, có thể kèm `EvidenceReviewNote`; người nộp/reporter/assignee nhận notification.
- **Ai thay đổi:** System Admin, Project Owner, project manager hoặc organization manager; role `Reviewer` độc lập chưa được cấp quyền riêng.

## F. Luồng nghiệp vụ chính hiện tại

1. **Owner/PM tạo hoặc mở project** tại module Project.
   - Dữ liệu: tên, mô tả, thời gian, thành viên.
   - Hệ thống: lưu Project, Owner, ProjectMember và audit liên quan.
   - Kết quả: project `Active`.

2. **Owner/PM tạo và phân công task** tại Task/Kanban.
   - Dữ liệu: title, description, priority, start date, due date, assignee.
   - Hệ thống: kiểm tra project/membership, lưu reporter/assignee/assignment, tạo notification.
   - Kết quả: task `Todo`.

3. **Hệ thống/PM theo dõi Attention.**
   - Dữ liệu: status, start date, due date, assignment time, task view.
   - Hệ thống xác định: `SapToiHan`, `QuaHan`, `ChuaBatDau`, `DangLamQuaLau`, `ChuaXem`.
   - Kết quả: task xuất hiện trong danh sách cần chú ý và Dashboard/Gantt.

4. **PM nhắc người phụ trách.**
   - Dữ liệu: task và assignee cần nhắc.
   - Hệ thống: kiểm tra quyền `CanNudgeAssignee` hoặc reporter; tạo notification `TaskAttentionNudge`, broadcast project.
   - Kết quả: assignee nhận cảnh báo; task không tự đổi trạng thái.

5. **Assignee mở task và bắt đầu làm.**
   - Dữ liệu: thao tác xem và đổi trạng thái.
   - Hệ thống: cập nhật view event; kiểm tra transition và row version.
   - Kết quả: tín hiệu chưa xem hết hiệu lực; task `InProgress`.

6. **Assignee nộp kết quả.**
   - Dữ liệu: comment, file, cờ `IsEvidence`.
   - Hệ thống: lưu attachment; evidence chuyển `Pending`.
   - Kết quả: task có kết quả chờ duyệt.

7. **PM/Owner review evidence.**
   - Dữ liệu: approve/reject và review note.
   - Hệ thống: cập nhật trạng thái evidence, reviewer/time, audit log; gửi notification cho uploader/reporter/assignee.
   - Kết quả: evidence `Approved` hoặc `Rejected`.

8. **Task hoàn tất hoặc quay lại xử lý.**
   - Nếu evidence bị từ chối: task giữ/quay về `InProgress`, assignee bổ sung kết quả.
   - Nếu evidence được duyệt: user có quyền chuyển task sang `Done`.
   - Hệ thống cập nhật Dashboard, Analytics, Notification và Audit Log.

## G. Các tình huống ngoại lệ cần demo

### 1. Thành viên không nhận/không mở task

- **Nguyên nhân:** assignee bỏ sót notification hoặc không phản hồi.
- **Dấu hiệu phát hiện:** không có `TaskViewEvent` sau `AssignedAt`; Attention có lý do `ChuaXem`.
- **Thay đổi trạng thái:** task vẫn `Todo`; không tự chuyển.
- **Thông báo:** PM có thể gửi `TaskAttentionNudge` cho assignee.
- **Ai xử lý:** Owner/PM, reporter hoặc người có `CanNudgeAssignee`.
- **Cách xử lý:** nudge, bình luận, sau đó cân nhắc đổi assignee thủ công.
- **Log/báo cáo:** notification; attention item. Audit riêng cho hành động nudge **chưa xác định**.

### 2. Thành viên nhận task nhưng không cập nhật tiến độ

- **Nguyên nhân:** không bắt đầu hoặc cập nhật chậm.
- **Dấu hiệu:** start date đã qua nhưng task còn `Todo` (`ChuaBatDau`), hoặc `InProgress` đã tiêu thụ ít nhất 70% khoảng start–due (`DangLamQuaLau`).
- **Thay đổi trạng thái:** không tự đổi.
- **Thông báo:** PM dùng nudge/comment.
- **Ai xử lý:** PM/reporter.
- **Cách xử lý:** yêu cầu cập nhật, chuyển `OnHold`, chỉnh deadline hoặc đổi assignee thủ công.
- **Log/báo cáo:** Attention/Gantt/Dashboard; task update audit.

### 3. Task bị trễ

- **Nguyên nhân:** `DueDate < now` và task chưa hoàn tất.
- **Dấu hiệu:** `QuaHan`, overdue count trên Dashboard.
- **Thay đổi trạng thái:** không tự đổi; task giữ trạng thái hiện tại.
- **Thông báo:** có loại `DueDateReminder`; việc worker gửi tự động trong mọi trường hợp **chưa xác định**.
- **Ai xử lý:** PM/Owner và assignee.
- **Cách xử lý:** nudge, comment, đổi assignee/deadline hoặc `OnHold`.
- **Log/báo cáo:** Dashboard overdue, task attention, Analytics, Audit Log khi dữ liệu được sửa.

### 4. Upload kết quả sai hoặc thiếu bằng chứng

- **Nguyên nhân:** file không chứng minh tiêu chí hoàn thành.
- **Dấu hiệu:** reviewer kiểm tra file và ghi review note.
- **Thay đổi trạng thái:** evidence `Pending -> Rejected`; task không thể `Done`.
- **Thông báo:** uploader, reporter và assignee nhận `ReviewCompleted`.
- **Ai xử lý:** Project Owner/manager/admin.
- **Cách xử lý:** reject, ghi rõ lý do; assignee nộp file mới hoặc sửa evidence.
- **Log/báo cáo:** `ReviewEvidence` audit, notification, review note.

### 5. Task bị reviewer/team lead từ chối

- **Nguyên nhân:** evidence không đạt.
- **Dấu hiệu:** `EvidenceApprovalStatus = Rejected`.
- **Thay đổi trạng thái:** evidence bị từ chối; task tiếp tục `InProgress`/`InReview`.
- **Thông báo:** gửi cho các bên liên quan.
- **Ai xử lý:** PM/Owner đóng vai reviewer trong demo hiện tại.
- **Cách xử lý:** trả việc bằng review note, bổ sung evidence, duyệt lại.
- **Log/báo cáo:** evidence review history qua audit và metadata attachment.

### 6. Task trễ ảnh hưởng milestone

- **Kết luận:** Milestone chưa có trong hệ thống.
- **Cách biểu diễn hợp lệ:** dùng Sprint, dependency/Gantt hoặc deadline Project.
- **Không đưa vào luồng chính:** không claim hệ thống tự cập nhật milestone.

### 7. PM phải can thiệp

- **Nguyên nhân:** unseen, stale, overdue hoặc evidence rejected.
- **Dấu hiệu:** Attention/Dashboard và notification.
- **Thay đổi trạng thái:** tùy quyết định của PM; không tự động.
- **Thông báo:** nudge/review notification.
- **Ai xử lý:** Owner/Manager/ScrumMaster hoặc reporter có quyền phù hợp.
- **Cách xử lý:** nhắc, comment, đổi assignee, đổi deadline, `OnHold`, review evidence.
- **Log/báo cáo:** task/audit/notification/dashboard.

### 8. User truy cập dữ liệu không có quyền

- **Nguyên nhân:** outside user hoặc Viewer/Customer truy cập task private/chỉnh dữ liệu.
- **Dấu hiệu:** access policy không tìm thấy project membership hoặc quyền task.
- **Thay đổi trạng thái:** không thay đổi dữ liệu; API trả `403` hoặc không trả task.
- **Thông báo:** không xác định.
- **Ai xử lý:** hệ thống chặn; Admin/Owner kiểm tra nếu cần.
- **Cách xử lý:** cấp membership/quyền hợp lệ hoặc từ chối yêu cầu.
- **Log/báo cáo:** audit cho lần truy cập bị từ chối **chưa xác định**; có thể chứng minh bằng response `403`.

### 9. Người phụ trách rời dự án/không còn khả dụng

- **Nguyên nhân:** user bị inactive hoặc bị xóa khỏi project.
- **Dấu hiệu:** kiểm tra membership/`IsActive`; cơ chế tự phát hiện assignment mồ côi **chưa xác định**.
- **Thay đổi trạng thái:** không có workflow tự động.
- **Thông báo:** chưa xác định.
- **Ai xử lý:** Owner/PM.
- **Cách xử lý hiện tại:** đổi assignee thủ công trước hoặc sau khi cập nhật membership.
- **Log/báo cáo:** audit thay đổi member/task nếu các service tương ứng ghi log.
- **Phân loại:** không chọn làm ngoại lệ chính của demo #1.

## H. Cơ chế production hiện có hoặc nên có

### Đã có trong hệ thống

- **Notification:** có entity, loại sự kiện, read/unread, idempotency key và realtime publisher.
- **Activity Log:** có recent workspace activity dựa trên Audit Log.
- **Permission Check:** có ở project/task/private task/wiki/group và service layer.
- **Dashboard Alert:** có overdue, blocked/OnHold, priority và Attention summary.
- **Report/Analytics:** có Dashboard, Analytics, workload và project summary.
- **Reassignment:** có thể sửa assignee/task assignment thủ công; chưa có workflow `Reassign` chuyên biệt.
- **Escalation:** có nudge và attention; chưa có escalation nhiều cấp tự động.
- **Audit Trail:** có `AuditLog` và logging ở các service quan trọng.
- **Deadline Tracking:** có due soon trong 24 giờ, overdue, stale Todo và stale InProgress.
- **Concurrency Control:** có `RowVersion` và phản hồi `409 Conflict`.
- **Completion Gate:** bắt buộc evidence `Approved` trước `Done`.

### Chưa có hoặc nên bổ sung

1. **Task acceptance/decline**
   - Vì sao cần: phân biệt “đã được giao” với “đã cam kết nhận”.
   - Mô phỏng demo #1: dùng `TaskViewEvent` + chuyển `Todo -> InProgress` làm dấu hiệu tiếp nhận.

2. **Escalation policy nhiều cấp**
   - Vì sao cần: tự động nhắc assignee, sau đó báo PM, sau đó đề xuất reassign theo thời gian.
   - Mô phỏng: seed task `ChuaXem/QuaHan`, PM bấm nudge và đổi assignee thủ công.

3. **Unavailable user/orphan assignment detection**
   - Vì sao cần: tránh task vẫn gắn cho user inactive/rời project.
   - Mô phỏng: PM kiểm tra member và đổi assignee bằng tay.

4. **Milestone**
   - Vì sao cần: thể hiện ảnh hưởng của task đến mốc bàn giao.
   - Mô phỏng: dùng Sprint hoặc deadline Project; phải nói rõ đây không phải Milestone.

5. **Role Reviewer có quyền riêng**
   - Vì sao cần: tách người quản lý và người nghiệm thu.
   - Mô phỏng: Owner/Manager thực hiện review.

6. **Audit sự kiện truy cập bị từ chối và nudge**
   - Vì sao cần: truy vết security/operational đầy đủ.
   - Mô phỏng: lưu response `403` hoặc ảnh màn hình; không claim đã có audit nếu chưa kiểm tra DB.

## I. Dữ liệu mẫu cần chuẩn bị cho kịch bản đầu tiên

- **Tên dự án:** `NovaPay - Hotfix thanh toán trùng`.
- **Mô tả:** `Xử lý lỗi giao dịch bị ghi nhận hai lần trước đợt phát hành thử nghiệm; mọi kết quả phải có minh chứng được duyệt.`
- **Deadline project:** ngày demo + 2 ngày, 17:00.
- **Danh sách thành viên:**
  - Lan Nguyễn: Project `Owner`/PM.
  - An Trần: `Member` hoặc `Developer`, người bỏ bê task ban đầu.
  - Chi Lê: `Member` hoặc `Tester`, người xử lý thay thế.
  - Hạnh Phạm: `Viewer` hoặc `Customer`, dùng tùy chọn để kiểm tra quyền.
- **Milestone:** không tạo vì hệ thống chưa có Milestone.
- **Sprint tùy chọn:** `Hotfix 48 giờ`, trạng thái `Active`.

### Danh sách task seed

| Task | Phụ trách ban đầu | Deadline | Trạng thái ban đầu | Mục đích |
|---|---|---|---|---|
| `T1 - Xác nhận nguyên nhân giao dịch trùng` | An | ngày demo - 1 giờ | `Todo` | Task quá hạn, chưa xem/chưa bắt đầu |
| `T2 - Sửa cơ chế idempotency` | Chi | ngày demo + 8 giờ | `InProgress` | Task đang thực hiện bình thường |
| `T3 - Chạy regression và nộp log` | Chi | ngày demo + 1 ngày | `InReview` | Có evidence `Pending` hoặc `Rejected` |

- **Task sẽ bị trễ:** `T1`.
- **Người bỏ bê trách nhiệm:** An.
- **Người xử lý thay thế:** Chi.
- **Trạng thái dữ liệu quan trọng cần seed:**
  - `T1.StartDate` đã qua; `T1.DueDate` đã qua; không có `TaskViewEvent` của An sau `AssignedAt`.
  - `T3` có file `regression-log-thieu-idempotency.txt`, `IsEvidence = true`, trạng thái `Pending` hoặc chuẩn bị để PM reject.
  - Sau reject, có sẵn file hợp lệ `regression-log-day-du.txt` để upload nhanh.
- **Comment cần chuẩn bị:**
  - PM: `Task đã quá hạn và chưa được tiếp nhận. Cần phản hồi trước 10:30.`
  - Review note từ chối: `Log chưa thể hiện idempotency key và mã giao dịch trả về.`
- **Notification cần xuất hiện:**
  - `TaskAttentionNudge` gửi cho An.
  - `ReviewCompleted` sau khi PM reject/approve evidence.
- **Audit cần có:** tạo/giao task, đổi assignee hoặc cập nhật task, review evidence, đổi trạng thái.
- **Tài khoản/phiên trình duyệt:**
  - Phiên 1: Lan/Owner.
  - Phiên 2 tùy chọn: An hoặc Hạnh; có thể chuẩn bị sẵn profile khác.
- **Lưu ý thời gian:** dùng script seed theo thời gian tương đối với lúc demo để Attention luôn hiện đúng; không hard-code một ngày cũ.

## J. Mục tiêu của kịch bản đầu tiên

- **Kịch bản phải chứng minh:** QALY không chỉ lưu Project/Task mà còn phát hiện việc bị bỏ bê, hỗ trợ PM can thiệp, kiểm soát chất lượng đầu ra và lưu dấu vết trách nhiệm.
- **Module nên demo:** Dashboard/Attention, Project Task/Kanban, Notification, Evidence Review; Audit Log chỉ mở nhanh nếu thời gian cho phép.
- **Nên tránh demo:** Group/Poll/Meeting, Import, Wiki, AI, Webhook, Sprint/Dependency phức tạp, Milestone và user leaving workflow.
- **Ngoại lệ chính:** assignee không mở/cập nhật task dẫn đến task quá hạn; PM nudge và đổi người xử lý; kết quả thay thế bị reject vì thiếu evidence, sau đó được bổ sung và approve.
- **Vì sao đủ mạnh để chứng minh không chỉ CRUD:**
  - Rủi ro được suy ra từ deadline, trạng thái, assignment và view event.
  - Hệ thống kiểm tra quyền trước hành động.
  - Nudge sinh notification thay vì chỉ sửa record.
  - Evidence có workflow review và notification.
  - Business rule chặn `Done` nếu evidence chưa `Approved`.
  - Row version bảo vệ cập nhật đồng thời.
  - Audit log lưu actor, entity, thay đổi và thời điểm.

## K. Ràng buộc demo

- **Thời lượng mong muốn:** khoảng 3–4 phút.
- **Hình thức:** demo trực tiếp.
- **Seed data:** nên seed trước; không nhập project/task từ đầu trong buổi báo cáo.
- **Chức năng chưa ổn định cần tránh:** AI provider, LiveKit media/screen share, PDF import; dù người dùng cho rằng “chắc là không có”, repository vẫn ghi rõ các giới hạn này.
- **Chuẩn bị để tránh lỗi:**
  - Đăng nhập sẵn tài khoản Owner.
  - Mở sẵn project và tab Attention/Kanban.
  - Seed deadline theo giờ hiện tại để task chắc chắn `QuaHan/ChuaXem`.
  - Chuẩn bị hai file evidence nhỏ và đúng định dạng.
  - Kiểm tra PM có role `Owner`/`Manager`, không chỉ `Reviewer`.
  - Kiểm tra task chưa có evidence `Approved` trước bước chứng minh bị chặn `Done`.
  - Sau reject, bảo đảm notification xuất hiện cho đúng user.
  - Không dùng credential, token hoặc dữ liệu production thật.
  - Có ảnh/video backup của Attention, `403`, evidence reject và completion gate.

## L. Context Pack hoàn chỉnh có thể copy sang prompt tiếp theo

```text
Hệ thống: QALY, hệ thống quản lý dự án nội bộ.

Mục tiêu demo số 1:
Chứng minh QALY quản lý trách nhiệm end-to-end, không chỉ CRUD: hệ thống phát hiện task bị bỏ bê/quá hạn, cho PM can thiệp, kiểm soát quyền, yêu cầu evidence được duyệt trước khi hoàn tất và ghi nhận notification/audit.

Thời lượng và hình thức:
- 3–4 phút.
- Demo trực tiếp.
- Dùng seed data chuẩn bị trước.

Vai trò dùng trong demo:
- Lan Nguyễn: Project Owner/PM, có quyền xem Attention, nudge, đổi assignee và review evidence.
- An Trần: assignee ban đầu, không mở/không cập nhật task.
- Chi Lê: thành viên xử lý thay thế và nộp evidence.
- Hạnh Phạm: Viewer/Customer, chỉ dùng nếu cần chứng minh 403.
- Không dùng role Reviewer độc lập để duyệt vì source hiện chỉ cho Admin/Owner/role quản lý project duyệt evidence.

Project mẫu:
- Tên: NovaPay - Hotfix thanh toán trùng.
- Mô tả: Xử lý lỗi giao dịch bị ghi nhận hai lần; mọi kết quả phải có minh chứng được duyệt.
- Deadline: ngày demo + 2 ngày, 17:00.
- Project status: Active.

Task mẫu:
1. T1 - Xác nhận nguyên nhân giao dịch trùng
   - Assignee ban đầu: An.
   - Status: Todo.
   - StartDate đã qua.
   - DueDate đã qua 1 giờ.
   - Không có TaskViewEvent của An sau AssignedAt.
   - Attention phải hiện: QuaHan, ChuaBatDau, ChuaXem.
2. T2 - Sửa cơ chế idempotency
   - Assignee: Chi.
   - Status: InProgress.
   - DueDate: ngày demo + 8 giờ.
3. T3 - Chạy regression và nộp log
   - Assignee: Chi.
   - Status: InReview.
   - Có evidence thiếu dữ liệu để PM reject.

Luồng nghiệp vụ thực tế đã có:
- Project/Task/Kanban.
- Assignment và TaskViewEvent.
- Attention phát hiện SapToiHan, QuaHan, ChuaBatDau, DangLamQuaLau, ChuaXem.
- PM/reporter có thể nudge assignee; hệ thống tạo TaskAttentionNudge notification.
- PM có thể đổi assignee thủ công.
- Assignee tải attachment và đánh dấu Evidence.
- Evidence có trạng thái None, Pending, Approved, Rejected.
- PM/Owner/manager/admin có thể approve/reject và ghi review note.
- Hệ thống gửi ReviewCompleted notification cho uploader/reporter/assignee.
- Task không thể chuyển sang Done nếu chưa có evidence Approved.
- Task status hợp lệ: Todo, InProgress, OnHold, InReview, Done, Cancelled.
- Kanban dùng RowVersion và trả 409 nếu cập nhật từ bản dữ liệu cũ.
- AuditLog lưu Action, EntityType, EntityId, ChangesJson, UserId, IP và Timestamp.

Ngoại lệ chính:
An không mở và không cập nhật T1. Task quá hạn và xuất hiện trong Attention. Lan nudge An. Vì vẫn không phản hồi, Lan đổi người xử lý sang Chi. Chi nộp evidence nhưng thiếu idempotency key. Lan reject với review note. Hệ thống gửi notification và không cho task Done. Chi nộp evidence đúng; Lan approve; khi đó task mới đủ điều kiện Done.

Điểm phải nói đúng:
- Attention không tự đổi status của task.
- Reassignment là thao tác thủ công, chưa có workflow escalation tự động nhiều cấp.
- Không có entity/module Milestone trong source hiện tại; không đưa Milestone vào demo.
- User rời dự án/inactive chưa có cơ chế tự động tìm và xử lý assignment mồ côi.
- Project có Active và Archived đang được dùng; state machine project đầy đủ chưa được xác minh.
- Role Reviewer tồn tại nhưng chưa có quyền review evidence riêng.
- Outside user bị chặn bởi permission policy; audit riêng cho lần truy cập bị từ chối chưa được xác minh.

Module nên xuất hiện:
Dashboard/Attention -> Task/Kanban -> Notification -> Evidence Review -> Done gate.

Module nên tránh:
Group, Poll, Meeting, Import, Wiki, AI, Webhook, Milestone, dependency phức tạp.

File/comment chuẩn bị:
- regression-log-thieu-idempotency.txt
- regression-log-day-du.txt
- Review note: "Log chưa thể hiện idempotency key và mã giao dịch trả về."
- Comment PM: "Task đã quá hạn và chưa được tiếp nhận. Cần phản hồi trước 10:30."

Kết quả cuối cần chứng minh:
- PM nhìn thấy việc bị bỏ bê mà không cần hỏi thủ công.
- Người không có quyền không thể sửa/xem dữ liệu nhạy cảm.
- Việc nhắc và review sinh notification.
- Kết quả kém chất lượng bị từ chối.
- Task không thể báo hoàn thành giả khi evidence chưa được duyệt.
- Các thao tác quan trọng có dữ liệu audit để truy vết.
```

