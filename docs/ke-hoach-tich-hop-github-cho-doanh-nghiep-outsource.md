# KẾ HOẠCH TÍCH HỢP GITHUB CHO QALY

## 1. Bối cảnh

Qaly được định hướng là hệ thống quản lý dự án phần mềm dành cho doanh nghiệp outsource vừa và nhỏ. Với mô hình này, dữ liệu quản lý thường bị tách rời:

- Yêu cầu, nhiệm vụ và deadline nằm trong hệ thống quản lý dự án.
- Branch, commit, pull request và review nằm trên GitHub.
- Quyết định và action item nằm trong cuộc họp hoặc tài liệu.
- Báo cáo tiến độ được Project Manager tổng hợp thủ công.

Việc thiếu liên kết giữa các nguồn dữ liệu khiến nhà quản lý khó xác định trạng thái kỹ thuật thực tế, khó truy vết thay đổi và mất nhiều thời gian lập báo cáo cho khách hàng.

## 2. Mục tiêu

Tích hợp GitHub vào Qaly để tạo chuỗi truy vết thống nhất:

```text
Yêu cầu khách hàng
    -> Nhiệm vụ Qaly
    -> Branch
    -> Commit
    -> Pull request
    -> Review
    -> Merge
    -> Release
    -> Báo cáo khách hàng
```

Hệ thống cần giúp người quản lý trả lời:

- Nhiệm vụ nào đang có hoạt động phát triển?
- Nhiệm vụ nào đã hoàn thành trên Qaly nhưng chưa có PR được merge?
- PR nào đang chờ review quá lâu hoặc đang chặn release?
- Thay đổi kỹ thuật nào liên quan đến một yêu cầu cụ thể?
- Tiến độ báo cáo có khớp với hoạt động repository không?
- Release gần nhất gồm những nhiệm vụ và thay đổi nào?

## 3. Nguyên tắc sản phẩm

### 3.1. Qaly không phải GitHub client

Qaly không cần sao chép toàn bộ giao diện và chức năng của GitHub. GitHub tiếp tục là nơi developer làm việc với source code. Qaly sử dụng metadata kỹ thuật để phục vụ:

- Quản lý tiến độ.
- Truy vết thay đổi.
- Phát hiện rủi ro.
- Tổng hợp báo cáo.
- Hỗ trợ ra quyết định.

### 3.2. Read-only trước, tự động hóa sau

Phiên bản đầu chỉ đọc dữ liệu GitHub. Qaly không được tự động:

- Push code.
- Sửa source code.
- Merge hoặc đóng pull request.
- Xóa branch.
- Thay đổi repository settings.

Các hành động làm thay đổi trạng thái task trong Qaly phải được người dùng xác nhận hoặc bật bằng rule rõ ràng.

### 3.3. Không dùng số commit để chấm năng suất

Số lượng commit chỉ là tín hiệu hoạt động, không phải thước đo năng suất cá nhân. Hệ thống không xây dựng bảng xếp hạng developer theo số commit.

Các chỉ số phù hợp hơn:

- Cycle time của task.
- Thời gian từ bắt đầu task đến mở PR.
- Thời gian PR chờ review.
- Thời gian từ review đến merge.
- Tỷ lệ task có liên kết PR.
- Số PR bị block.
- Review workload.
- Release frequency.

### 3.4. Bằng chứng phải truy cập được

Mọi nhận định hoặc cảnh báo từ Qaly và Erumi phải kèm liên kết đến task, commit, PR, review hoặc release tương ứng.

## 4. Phạm vi MVP

MVP tập trung vào một vertical slice hoàn chỉnh:

1. Organization kết nối GitHub App.
2. Chọn repository được phép truy cập.
3. Ánh xạ repository với project Qaly.
4. Nhận webhook cho push, pull request, review và release.
5. Đồng bộ metadata lịch sử ban đầu.
6. Liên kết branch, commit và PR với task Qaly.
7. Hiển thị development activity trong task.
8. Hiển thị project activity timeline hợp nhất.
9. Phát hiện một số trạng thái lệch cơ bản.
10. Cho Erumi tóm tắt hoạt động kỹ thuật và tạo báo cáo tuần có nguồn.

### Ngoài phạm vi MVP

- Đọc hoặc lưu toàn bộ source code.
- Lưu full patch/diff lâu dài.
- Merge PR từ Qaly.
- Quản lý GitHub Actions đầy đủ.
- Tự động đánh giá chất lượng code bằng AI.
- GitLab, Bitbucket và Azure DevOps.
- Tính lương hoặc đánh giá KPI developer từ Git activity.

## 5. Phương án tích hợp

### 5.1. Sử dụng GitHub App

GitHub App phù hợp hơn OAuth App cho tích hợp ở cấp doanh nghiệp vì:

- Cài đặt theo organization hoặc account.
- Chọn chính xác repository được cấp quyền.
- Áp dụng principle of least privilege.
- Sử dụng installation token có thời hạn ngắn.
- Nhận webhook theo installation.
- Có thể thu hồi quyền tập trung.
- Không phụ thuộc personal access token của một nhân viên.

OAuth vẫn có thể được dùng riêng cho chức năng đăng nhập bằng GitHub, nhưng không phải cơ chế chính để đồng bộ repository.

### 5.2. Quyền tối thiểu đề xuất

GitHub App chỉ yêu cầu quyền đọc:

- Repository metadata: Read.
- Contents: Read, để đọc metadata commit.
- Pull requests: Read.
- Issues: Read, nếu đồng bộ issue.
- Actions: Read, chỉ khi MVP cần trạng thái workflow.

Sự kiện webhook:

- `installation`
- `installation_repositories`
- `push`
- `pull_request`
- `pull_request_review`
- `issues`, nếu dùng GitHub Issue
- `release`
- `repository`

## 6. Kiến trúc tổng thể

```text
GitHub App
   |
   | Webhook có chữ ký
   v
Qaly Webhook Endpoint
   |
   | Xác thực, chống replay, ghi event inbox
   v
Background Processor
   |
   +-> Chuẩn hóa commit, PR, review, release
   +-> Chống xử lý trùng
   +-> Liên kết với task
   +-> Cập nhật project activity timeline
   +-> Sinh cảnh báo trạng thái lệch
   |
   v
Qaly Database
   |
   +-> Task Development Activity
   +-> Project Timeline
   +-> Dashboard
   +-> Erumi và báo cáo
```

### Thành phần backend đề xuất

- `GitHubInstallationService`
- `GitHubTokenService`
- `GitHubRepositorySyncService`
- `GitHubWebhookController`
- `GitHubWebhookVerifier`
- `GitHubEventProcessor`
- `DevelopmentLinkService`
- `ProjectActivityService`
- Background job hoặc queue xử lý webhook

### Nguyên tắc xử lý webhook

Webhook endpoint chỉ thực hiện nhanh:

1. Đọc delivery ID.
2. Xác thực chữ ký `X-Hub-Signature-256` bằng raw request body.
3. Kiểm tra event type và installation.
4. Lưu payload hoặc phần metadata cần thiết vào inbox.
5. Trả HTTP `2xx` sớm.
6. Xử lý nghiệp vụ ở background.

Không thực hiện đồng bộ nặng trong request webhook.

## 7. Mô hình dữ liệu đề xuất

### 7.1. GitHubInstallation

| Trường | Ý nghĩa |
|---|---|
| `Id` | ID nội bộ |
| `OrganizationId` | Tenant Qaly sở hữu kết nối |
| `InstallationId` | Installation ID từ GitHub |
| `AccountId` | GitHub account ID |
| `AccountLogin` | Organization hoặc username GitHub |
| `AccountType` | Organization/User |
| `InstalledByUserId` | Người thực hiện kết nối |
| `Status` | Active, Suspended, Removed |
| `CreatedAt` | Thời điểm kết nối |
| `UpdatedAt` | Thời điểm cập nhật |

Không lưu installation token lâu dài trong database. Token phải được tạo khi cần và cache trong thời gian ngắn hơn hạn token.

### 7.2. GitHubRepositoryConnection

| Trường | Ý nghĩa |
|---|---|
| `Id` | ID nội bộ |
| `OrganizationId` | Tenant Qaly |
| `ProjectId` | Project được ánh xạ |
| `GitHubInstallationId` | Installation sở hữu repository |
| `RepositoryExternalId` | Repository ID từ GitHub |
| `Owner` | Repository owner |
| `Name` | Repository name |
| `FullName` | `owner/repository` |
| `DefaultBranch` | Branch mặc định |
| `IsPrivate` | Repository private/public |
| `IsActive` | Trạng thái đồng bộ |
| `LastSyncedAt` | Lần đồng bộ gần nhất |

Mỗi project có thể liên kết nhiều repository nếu dự án có frontend, backend và infrastructure tách riêng.

### 7.3. GitHubCommit

| Trường | Ý nghĩa |
|---|---|
| `Id` | ID nội bộ |
| `OrganizationId` | Tenant Qaly |
| `RepositoryConnectionId` | Repository nguồn |
| `Sha` | Commit SHA |
| `Message` | Commit message |
| `AuthorLogin` | GitHub login nếu ánh xạ được |
| `AuthorEmailHash` | Hash email, không bắt buộc lưu email thô |
| `CommittedAt` | Thời điểm commit |
| `BranchName` | Branch nhận từ event |
| `Url` | Link đến GitHub |

Unique constraint đề xuất: `(RepositoryConnectionId, Sha)`.

### 7.4. GitHubPullRequest

| Trường | Ý nghĩa |
|---|---|
| `Id` | ID nội bộ |
| `OrganizationId` | Tenant Qaly |
| `RepositoryConnectionId` | Repository nguồn |
| `Number` | Số PR |
| `Title` | Tiêu đề |
| `State` | Open, Closed, Merged |
| `AuthorLogin` | Người mở PR |
| `HeadBranch` | Branch nguồn |
| `BaseBranch` | Branch đích |
| `IsDraft` | PR nháp |
| `OpenedAt` | Thời điểm mở |
| `UpdatedAt` | Thời điểm cập nhật |
| `MergedAt` | Thời điểm merge |
| `MergedByLogin` | Người merge |
| `Url` | Link GitHub |

Unique constraint đề xuất: `(RepositoryConnectionId, Number)`.

### 7.5. GitHubPullRequestReview

| Trường | Ý nghĩa |
|---|---|
| `Id` | ID nội bộ |
| `PullRequestId` | PR liên quan |
| `ReviewExternalId` | Review ID GitHub |
| `ReviewerLogin` | Reviewer |
| `State` | Approved, ChangesRequested, Commented, Dismissed |
| `SubmittedAt` | Thời điểm review |
| `Url` | Link GitHub |

### 7.6. GitHubRelease

| Trường | Ý nghĩa |
|---|---|
| `Id` | ID nội bộ |
| `RepositoryConnectionId` | Repository nguồn |
| `ReleaseExternalId` | Release ID GitHub |
| `TagName` | Tag phiên bản |
| `Name` | Tên release |
| `IsDraft` | Trạng thái draft |
| `IsPrerelease` | Prerelease |
| `PublishedAt` | Thời điểm phát hành |
| `Url` | Link GitHub |

### 7.7. TaskDevelopmentLink

| Trường | Ý nghĩa |
|---|---|
| `Id` | ID nội bộ |
| `OrganizationId` | Tenant Qaly |
| `TaskId` | Task Qaly |
| `EntityType` | Branch, Commit, PullRequest, Issue, Release |
| `ExternalEntityId` | ID entity đã chuẩn hóa |
| `LinkSource` | Manual, TaskKey, BranchName, CommitMessage, PullRequest |
| `Confidence` | Độ tin cậy của liên kết tự động |
| `LinkedByUserId` | Người xác nhận nếu liên kết thủ công |
| `CreatedAt` | Thời điểm liên kết |

### 7.8. GitHubWebhookInbox

| Trường | Ý nghĩa |
|---|---|
| `Id` | ID nội bộ |
| `DeliveryId` | `X-GitHub-Delivery`, unique |
| `EventName` | Loại event |
| `InstallationId` | Installation nguồn |
| `RepositoryExternalId` | Repository nguồn |
| `Payload` | JSON hoặc metadata cần thiết |
| `Status` | Pending, Processing, Processed, Failed |
| `AttemptCount` | Số lần xử lý |
| `LastError` | Lỗi gần nhất |
| `ReceivedAt` | Thời điểm nhận |
| `ProcessedAt` | Thời điểm hoàn thành |

Inbox giúp idempotency, retry và audit webhook.

## 8. Liên kết task với GitHub

### 8.1. Task key

Mỗi task cần mã ổn định, ví dụ:

```text
QALY-142
```

Task key có thể xuất hiện trong:

- Branch: `feature/QALY-142-invitation-validation`
- Commit: `QALY-142 Fix invitation validation`
- PR title: `QALY-142 Validate invitation token`
- PR description: `Closes QALY-142`

### 8.2. Thứ tự nhận diện

1. Liên kết thủ công đã được xác nhận.
2. Task key trong PR title hoặc description.
3. Task key trong branch name.
4. Task key trong commit message.
5. Gợi ý AI hoặc heuristic có confidence thấp, bắt buộc người dùng xác nhận.

Không tự động liên kết chỉ dựa trên nội dung ngữ nghĩa khi không có task key rõ ràng.

### 8.3. Hiển thị trong task

```text
Development activity

Branch       feature/QALY-142-invitation-validation
Commits      4 commits
Pull request #86 - Changes requested
Reviews      2
Last update  16:42, 12/07/2026
```

Mỗi mục phải liên kết về GitHub.

## 9. Project Activity Timeline

Timeline dự án hợp nhất các sự kiện nghiệp vụ và kỹ thuật:

```text
09:10  Khách hàng yêu cầu thay đổi luồng thanh toán
10:05  PM cập nhật deadline task QALY-142
11:20  Branch feature/QALY-142 được tạo
14:30  Có 3 commit mới
16:10  Pull request #86 được mở
17:25  Reviewer yêu cầu chỉnh sửa
09:40  Pull request #86 được merge
10:00  Task QALY-142 được xác nhận hoàn thành
```

Timeline có thể chứa:

- Task được tạo, cập nhật trạng thái hoặc deadline.
- Thành viên được gán hoặc gỡ khỏi task.
- Tài liệu hoặc wiki thay đổi.
- Cuộc họp, quyết định và action item.
- Branch, commit, PR, review và release.
- Cảnh báo hoặc đề xuất do Erumi tạo.

## 10. Workflow automation

### Giai đoạn đầu

Qaly chỉ đưa ra đề xuất:

- Có PR mới: đề xuất chuyển task sang `In Review`.
- PR được merge: đề xuất chuyển task sang `Done`.
- Review yêu cầu sửa: đánh dấu task cần chú ý.
- Task `Done` nhưng chưa có PR merge: cảnh báo trạng thái lệch.
- Task đang thực hiện nhưng không có Git activity trong thời gian cấu hình: cảnh báo đình trệ.

### Giai đoạn sau

Organization có thể bật rule tự động cho từng project. Mọi rule cần:

- Có mô tả hành vi rõ ràng.
- Ghi audit log.
- Cho phép tắt.
- Không được thay đổi dữ liệu ngoài tenant/project tương ứng.
- Có cơ chế xử lý sự kiện đến sai thứ tự.

## 11. Erumi và báo cáo

### Câu hỏi hỗ trợ

- “Dự án nào có nhiều task Done nhưng chưa merge?”
- “PR nào đang chặn release tuần này?”
- “Task nào có commit mới nhưng chưa cập nhật trạng thái?”
- “Ai đang có nhiều PR chờ review?”
- “Tóm tắt thay đổi kỹ thuật của dự án trong tuần.”
- “Tạo release note từ các PR đã merge.”
- “Tạo báo cáo tiến độ cho khách hàng, không đưa chi tiết kỹ thuật nội bộ.”

### Yêu cầu an toàn

- Mọi truy vấn phải được scope theo `OrganizationId` và `ProjectId`.
- Cache AI phải tách biệt theo tenant/project.
- Prompt không chứa source code nếu chưa có sự đồng ý rõ ràng.
- Response phải dẫn nguồn task, PR hoặc release.
- Không dùng Git activity để tự động kết luận hiệu suất cá nhân.
- Báo cáo gửi khách hàng phải loại bỏ dữ liệu nội bộ không phù hợp.

## 12. Bảo mật và quyền riêng tư

### Bắt buộc

- Xác thực chữ ký webhook bằng constant-time comparison.
- Lưu GitHub App private key trong secret manager hoặc environment secret.
- Không commit private key, webhook secret hoặc token.
- Installation token chỉ tồn tại ngắn hạn.
- Kiểm tra `OrganizationId` ở mọi query.
- Chống replay bằng delivery ID unique.
- Xử lý webhook idempotent.
- Ghi audit log khi kết nối, gỡ kết nối hoặc thay đổi repository.
- Cho phép organization xóa toàn bộ dữ liệu GitHub đã đồng bộ.
- Không log raw token hoặc secret.

### Dữ liệu không nên lưu trong MVP

- Toàn bộ nội dung source code.
- Full diff lâu dài.
- Secret scanning result chứa dữ liệu nhạy cảm.
- Email commit dạng thô nếu không cần thiết.

## 13. Đồng bộ dữ liệu

### Initial sync

Khi liên kết repository:

1. Kiểm tra installation còn quyền truy cập.
2. Lấy repository metadata.
3. Đồng bộ lịch sử giới hạn, ví dụ 90 ngày gần nhất.
4. Lấy open PR và PR cập nhật gần đây.
5. Lấy review của các PR trong phạm vi.
6. Lấy release gần đây.
7. Tạo task link từ task key.
8. Ghi `LastSyncedAt`.

Không cần đồng bộ toàn bộ lịch sử repository ngay từ đầu.

### Incremental sync

- Webhook là nguồn cập nhật chính.
- Scheduled reconciliation chạy định kỳ để bù sự kiện bị mất.
- Sử dụng GitHub API conditional request nếu phù hợp.
- Tôn trọng rate limit và `Retry-After`.

## 14. Giao diện đề xuất

### Organization Settings

Khu vực `Tích hợp`:

- Trạng thái GitHub App.
- Account đã kết nối.
- Repository được cấp quyền.
- Người kết nối.
- Lần đồng bộ gần nhất.
- Nút quản lý quyền trên GitHub.
- Nút ngắt kết nối có xác nhận.

### Project Settings

- Chọn một hoặc nhiều repository.
- Chọn default branch.
- Quy tắc task key.
- Bật/tắt automation.
- Trạng thái đồng bộ và lỗi gần nhất.

### Project Overview

- Technical activity timeline.
- PR đang mở.
- PR chờ review.
- Task và PR lệch trạng thái.
- Release gần nhất.

### Task Detail

- Branch liên quan.
- Commit liên quan.
- PR và review state.
- Link GitHub.
- Liên kết/tháo liên kết thủ công.

## 15. API nội bộ dự kiến

```text
POST   /api/integrations/github/installations/callback
POST   /api/integrations/github/webhooks
GET    /api/integrations/github/status
DELETE /api/integrations/github/installations/{id}

GET    /api/projects/{projectId}/github/repositories
POST   /api/projects/{projectId}/github/repositories
DELETE /api/projects/{projectId}/github/repositories/{connectionId}
POST   /api/projects/{projectId}/github/sync

GET    /api/projects/{projectId}/development-activity
GET    /api/tasks/{taskId}/development-links
POST   /api/tasks/{taskId}/development-links
DELETE /api/tasks/{taskId}/development-links/{linkId}
```

Tên endpoint có thể điều chỉnh theo convention hiện tại của Qaly.

## 16. Lộ trình triển khai

### Milestone 0: Audit và thiết kế

- Audit organization, project, task, permission và audit log hiện tại.
- Chọn task key format.
- Xác định hosting URL cho callback/webhook.
- Thiết kế threat model.
- Hoàn thiện schema và migration plan.

Kết quả: tài liệu kiến trúc và migration được duyệt.

### Milestone 1: Kết nối GitHub App

- Tạo GitHub App cho môi trường development.
- Xây installation callback.
- Lưu installation và repository metadata.
- Sinh installation token ngắn hạn.
- Trang trạng thái kết nối trong Organization Settings.

Kết quả: organization có thể kết nối, chọn repository và ngắt kết nối.

### Milestone 2: Webhook và đồng bộ

- Webhook signature verification.
- Webhook inbox với delivery ID unique.
- Background processor và retry.
- Initial sync giới hạn.
- Đồng bộ push, PR, review và release.
- Scheduled reconciliation.

Kết quả: dữ liệu GitHub được đồng bộ ổn định và chống trùng.

### Milestone 3: Task Development Link

- Bổ sung task key.
- Parser task key từ branch, commit và PR.
- Tạo link tự động và thủ công.
- Hiển thị development activity trong task.
- Xử lý unlink và repository disconnect.

Kết quả: task Qaly truy vết được hoạt động phát triển liên quan.

### Milestone 4: Project Timeline và cảnh báo

- Timeline hợp nhất.
- Bộ lọc theo loại hoạt động.
- PR chờ review.
- Task Done chưa merge.
- Task có code activity nhưng trạng thái chưa cập nhật.
- Xác nhận trước khi đổi trạng thái task.

Kết quả: PM theo dõi được tiến độ kỹ thuật mà không cần mở từng repository.

### Milestone 5: Erumi và báo cáo

- Tool/query read-only cho GitHub activity.
- Tenant-safe AI cache.
- Trả lời kèm source link.
- Tóm tắt tuần.
- Release note.
- Báo cáo khách hàng với bộ lọc dữ liệu nội bộ.

Kết quả: Erumi tổng hợp được dữ liệu quản lý và kỹ thuật có bằng chứng.

### Milestone 6: Hardening

- Load test webhook.
- Kiểm thử event đến trùng và sai thứ tự.
- Kiểm thử tenant isolation.
- Rate limit và backoff.
- Observability dashboard.
- Data retention và deletion.
- Security review.

Kết quả: tích hợp đủ an toàn để demo và thử nghiệm thực tế.

## 17. Chiến lược kiểm thử

### Unit test

- Xác thực webhook signature đúng/sai.
- Parse task key.
- Idempotency theo delivery ID.
- Mapping event thành entity nội bộ.
- Automation rule.
- Installation token caching.

### Integration test

- Hai tenant không đọc được installation/repository của nhau.
- Hai tenant có cùng prompt không dùng chung AI cache.
- Webhook trùng không tạo dữ liệu trùng.
- Event PR đến trước push vẫn xử lý được.
- Repository bị gỡ quyền chuyển connection sang trạng thái phù hợp.
- Disconnect xóa hoặc vô hiệu hóa dữ liệu đúng chính sách.

### End-to-end test

1. Kết nối GitHub App.
2. Chọn repository.
3. Tạo task có task key.
4. Gửi event push và PR giả lập.
5. Xác nhận activity xuất hiện trong task.
6. Merge PR.
7. Xác nhận Qaly đề xuất cập nhật task.
8. Hỏi Erumi và kiểm tra source link.

## 18. Observability

Các metric cần theo dõi:

- Số webhook nhận theo event type.
- Tỷ lệ webhook xử lý thành công/thất bại.
- Độ trễ từ GitHub event đến Qaly timeline.
- Số lần retry và dead-letter.
- GitHub API rate limit còn lại.
- Initial sync duration.
- Số link tự động và link được người dùng xác nhận/hủy.
- Số cảnh báo trạng thái lệch.
- AI query có/không có nguồn.

Log cần có correlation giữa:

- Delivery ID.
- Installation ID.
- Repository ID.
- Organization ID.
- Project ID.

Không ghi token, private key hoặc webhook secret vào log.

## 19. Tiêu chí nghiệm thu MVP

MVP hoàn thành khi:

- Organization cài đặt GitHub App và chọn repository thành công.
- Qaly chỉ yêu cầu quyền read-only cần thiết.
- Project ánh xạ được một hoặc nhiều repository.
- Webhook được xác thực và xử lý idempotent.
- Commit, PR, review và release được đồng bộ.
- Task key liên kết đúng task với branch/commit/PR.
- Task hiển thị development activity và link GitHub.
- Project timeline hiển thị sự kiện quản lý và kỹ thuật.
- PR merge chỉ đề xuất thay đổi task, không tự đổi khi chưa bật rule.
- Erumi trả lời ít nhất ba câu hỏi GitHub có source link.
- Hai tenant không chia sẻ dữ liệu hoặc AI cache.
- Disconnect GitHub dừng đồng bộ và xử lý dữ liệu theo chính sách.
- Unit, integration và end-to-end test quan trọng đều pass.

## 20. Kịch bản demo bảo vệ

### Bối cảnh

Công ty outsource đang triển khai dự án thương mại điện tử. Task `QALY-142` xử lý lỗi invitation và đã được developer bắt đầu nhưng PM chưa biết trạng thái kỹ thuật.

### Trình tự demo

1. Mở project Qaly đã liên kết GitHub repository.
2. Mở task `QALY-142` và xem branch, commit, PR liên quan.
3. Cho thấy PR đang ở trạng thái `Changes requested`.
4. Project timeline thể hiện yêu cầu, task, commit, PR và review theo thứ tự thời gian.
5. Hỏi Erumi: “Điều gì đang chặn QALY-142?”
6. Erumi trả lời dựa trên review và dẫn link PR.
7. Mô phỏng PR được merge.
8. Qaly đề xuất chuyển task sang `Done`.
9. PM xác nhận cập nhật.
10. Erumi tạo báo cáo tuần hoặc release note có danh sách task và PR.

Kịch bản này chứng minh Qaly nối dữ liệu quản lý với hoạt động phát triển, đúng bài toán doanh nghiệp outsource.

## 21. Rủi ro và biện pháp giảm thiểu

| Rủi ro | Mức độ | Biện pháp |
|---|---|---|
| Rò rỉ dữ liệu giữa tenant | Rất cao | Scope mọi query/cache theo organization và project; integration test bắt buộc |
| Lộ GitHub secret/token | Rất cao | Secret manager, token ngắn hạn, không log secret |
| Webhook giả mạo | Cao | Xác thực HMAC trên raw body và chống replay |
| Event trùng hoặc sai thứ tự | Cao | Inbox, idempotency, upsert, reconciliation |
| Vượt GitHub API rate limit | Trung bình | Webhook-first, incremental sync, backoff |
| Liên kết sai task | Trung bình | Task key rõ ràng, confidence, xác nhận thủ công |
| Đánh giá sai năng suất developer | Cao | Không xếp hạng theo commit count, chỉ dùng process metrics |
| Mở rộng phạm vi quá lớn | Cao | Giữ MVP read-only và vertical slice |
| Repository bị thu hồi quyền | Trung bình | Theo dõi installation event, ngừng sync và báo người dùng |

## 22. Ưu tiên thực hiện

Thứ tự ưu tiên đề xuất:

1. Tenant isolation và AI cache isolation.
2. GitHub App read-only.
3. Repository mapping và webhook inbox.
4. Task key và development link.
5. Task activity và project timeline.
6. Cảnh báo trạng thái lệch.
7. Erumi và báo cáo có nguồn.
8. Automation tùy chọn.

Không bắt đầu bằng dashboard nhiều biểu đồ. Trước tiên cần tạo được dữ liệu đúng, liên kết đúng và an toàn giữa tenant.

## 23. Hướng phát triển sau MVP

- GitHub Actions status và deployment tracking.
- DORA metrics ở cấp project/team, không dùng để xếp hạng cá nhân.
- Change request liên kết task, PR và tác động deadline.
- Customer-facing release report.
- GitLab, Bitbucket và Azure DevOps adapter.
- Mapping GitHub team với Qaly team.
- Đồng bộ issue hai chiều có kiểm soát.
- Policy theo repository và project.
- Webhook replay UI cho administrator.

## 24. Kết luận

Tích hợp GitHub là hướng mở rộng phù hợp với định vị Qaly cho doanh nghiệp outsource vừa và nhỏ. Giá trị cốt lõi không nằm ở việc hiển thị danh sách commit, mà ở khả năng liên kết:

```text
Khách hàng -> Yêu cầu -> Task -> Code -> Review -> Release -> Báo cáo
```

MVP nên ưu tiên read-only, tenant-safe, có bằng chứng và triển khai theo một vertical slice hoàn chỉnh. Khi dữ liệu quản lý và dữ liệu kỹ thuật đã được nối đúng, dashboard, cảnh báo và Erumi mới có đủ nền tảng để tạo giá trị thực tế.

## 25. Điều chỉnh phạm vi cho bản đồ án (đã chốt)

Bản kế hoạch gốc giữ nguyên về kiến trúc. Các điều chỉnh phạm vi dưới đây được thống nhất để phù hợp với đồ án tốt nghiệp được chấm theo mức độ **dùng được thực tế ở doanh nghiệp** (ưu tiên "sâu + đúng chuẩn" hơn "rộng").

### Đã hoàn thành trước

- **Task Key (§8.1)** đã triển khai: `TaskItem.Number` cấp tuần tự theo từng project (tập trung trong `QalyDbContext.SaveChanges`, an toàn concurrency), Task Key hiển thị dạng `{Project.Code}-{Number}` (VD `QALY-142`), có backfill task cũ. Đây là tiền đề cho toàn bộ cơ chế liên kết.

### Giữ nguyên (phần tạo uy tín "enterprise", không cắt)

- **GitHub App read-only** (không dùng Personal Access Token) — least-privilege, cấp theo org, thu hồi tập trung.
- **Webhook có verify chữ ký + inbox + idempotency.**
- **Tenant isolation + audit + read-only + bằng chứng có source link.**
- **Chuỗi truy vết** Khách hàng → Task → Branch/Commit/PR/Review → Merge → Báo cáo.

### Điều chỉnh cách làm

- **Tenant guard**: enforce ở tầng data-access/service (bắt buộc `OrganizationId` + kiểm tra membership), kèm integration test hai tenant. Không dùng global query filter ở pass này vì nền tảng chưa có "current organization" ở tầng DbContext.
- **Reconciliation worker**: bổ sung worker đồng bộ định kỳ (tái dùng pattern `VectorSyncWorker`) — vừa đúng chuẩn "bù event webhook bị mất", vừa là phương án dự phòng nếu webhook trục trặc lúc demo.
- **Automation**: chỉ **đề xuất** thay đổi trạng thái task, không tự động đổi — đúng posture doanh nghiệp.
- **Repository mapping**: schema hỗ trợ nhiều repo cho một project; pass đầu chỉ wire một repo.

### Cắt khỏi bản đồ án (đưa vào "hướng phát triển sau" §23)

- Release note tự động, customer report filtering nâng cao.
- GitHub Actions/deployment tracking, DORA metrics.
- GitLab/Bitbucket/Azure DevOps adapter.
- Automation rule tự động đổi trạng thái.

### Thứ tự build đã chốt

`M0.5` schema GitHub + tenant guard + repository mapping (không cần credential) → `M1` kết nối GitHub App + token → `M2` webhook receiver + reconciliation worker → `M3` parser task key + development link + activity trong task → `M4-lite` Erumi 1 tool đọc GitHub, trả lời có source link. Mỗi milestone tự đứng được để bảo vệ.
