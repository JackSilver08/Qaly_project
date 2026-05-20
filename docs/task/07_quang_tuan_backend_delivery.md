# Quang Tuấn - Backend Delivery (v3.1)

## 1) Foundation: Organization/Tenant + Project/Member

### API mới
- `GET /api/organizations`
- `GET /api/organizations/{id}`
- `POST /api/organizations`
- `PUT /api/organizations/{id}`
- `DELETE /api/organizations/{id}`
- `GET /api/organizations/{id}/members`
- `POST /api/organizations/{id}/members`
- `DELETE /api/organizations/{id}/members/{userId}`

### Thay đổi dữ liệu
- Thêm bảng: `Organizations`, `OrganizationMembers`
- Thêm cột: `Projects.OrganizationId`

## 2) RBAC + Audit

### Bổ sung
- Kiểm soát truy cập theo tổ chức tại `ProjectService` và `TaskAccessPolicy`.
- Audit cho thao tác quản trị user tại `AdminUsersController`:
  - `AdminCreateUser`
  - `AdminUpdateUser`
  - `AdminImportUsers`

## 3) Task Lifecycle + Evidence Approval

### Luật chuyển trạng thái
- `TaskStatusRules.CanTransition` chuyển từ kiểm tra hợp lệ đơn giản sang state machine cụ thể.
- Chặn chuyển sang `Done` nếu chưa có evidence được duyệt.

### API evidence mới
- `PATCH /api/attachments/{id}/evidence`
- `POST /api/attachments/{id}/evidence/review`

### Thay đổi dữ liệu
- Thêm cột cho `TaskAttachments`:
  - `IsEvidence`
  - `EvidenceApprovalStatus`
  - `EvidenceReviewedById`
  - `EvidenceReviewedAt`
  - `EvidenceReviewNote`

## 4) AI Draft Confirm Workflow (HITL)

### API mới
- `POST /api/ai/jobs`
- `POST /api/ai/drafts/{draftId}/confirm`

### Thay đổi dữ liệu
- Thêm bảng: `AiJobs`, `AiGeneratedDrafts`

## 5) Migration

- Migration mới:
  - `20260519121743_AddOrganizationAiWorkflowAndEvidenceApproval`

## 6) Verify

- `dotnet build Qaly_project.slnx`
- `dotnet test tests/Qaly.UnitTests/Qaly.UnitTests.csproj --no-build`
- `dotnet test tests/Qaly.IntegrationTests/Qaly.IntegrationTests.csproj --no-build`
