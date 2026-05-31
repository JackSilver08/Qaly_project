# QALY MVP P0 Scope Lock v3.2

## 1. Mục tiêu 10 tuần

Hoàn thành ít nhất 90% hệ thống demonstrable P0: user có thể đăng nhập, chat nhóm, tạo/quản lý task, import meeting note từ Meetily, dùng AI để trích action item/tạo draft/gợi ý assignee/tóm tắt tiến độ, có audit/cost/privacy guard.

## 2. Core P0 use cases

| UC | Tên | Actor | Done khi |
|---|---|---|---|
| P0-UC-01 | Login/logout | User | token hợp lệ, refresh/logout chạy |
| P0-UC-02 | Quản lý project cơ bản | Admin/PM | tạo project, thêm member |
| P0-UC-03 | RBAC project | Admin/PM/User | user không truy cập project không thuộc quyền |
| P0-UC-04 | Tạo task thủ công | PM/Member | title/description/priority/deadline/assignee lưu DB |
| P0-UC-05 | Kanban status | PM/Member | đổi status có audit |
| P0-UC-06 | Comment/evidence task | Member | upload/link/text evidence lưu được |
| P0-UC-07 | Chat room project | Member | gửi/nhận message realtime MVP |
| P0-UC-08 | Mention/assign notification | Member | notification lưu và hiển thị |
| P0-UC-09 | Meetily import | PM/Member | import transcript/summary, validate schema |
| P0-UC-10 | AI meeting extraction | PM/Member | tạo draft action items, user review được |
| P0-UC-11 | Create task from AI draft | PM/Member | confirm draft mới tạo task chính thức |
| P0-UC-12 | Chat summarization | Member | tóm tắt message range, có cache |
| P0-UC-13 | Task draft from chat | Member | chọn message -> draft task |
| P0-UC-14 | Assignee recommendation | PM | rank candidates + lý do, PM confirm |
| P0-UC-15 | Task breakdown/checklist | PM/Member | sinh subtasks/checklist draft |
| P0-UC-16 | Sprint/project summary | PM | báo cáo từ task metrics + narrative |
| P0-UC-17 | AI usage/cost view | PM/Admin | xem cost/usage theo ngày/tháng |
| P0-UC-18 | AI fallback/mock | QA/Dev | tắt API vẫn demo bằng cache/mock |
| P0-UC-19 | Privacy consent/delete/export | User/Admin | consent, export, delete transcript path |
| P0-UC-20 | Backup/restore demo | DevOps | seed/backup/restore chạy được |

## 3. AI P0 function lock

Chỉ 8 chức năng AI P0 được làm trong 10 tuần:

1. Meetily import/sync manual.
2. Meeting keywords/action/deadline/decision extraction.
3. Chat thread summarization.
4. Create task draft from chat/meeting.
5. Recommend assignee.
6. Break down task into subtasks.
7. Generate acceptance checklist.
8. Sprint/project progress summary.

## 4. Bảng P0 cần triển khai thực tế

Không cần hiện thực hóa toàn bộ 188 bảng logical ở P0. Bảng P0 nên gồm:

### Core

- users
- organizations/tenants
- projects
- project_members
- roles/permissions hoặc role enum đủ dùng
- tasks
- task_comments
- task_status_history
- chat_rooms
- chat_messages
- notifications
- audit_logs

### AI/Meeting

- ai_provider_config
- ai_usage_ledger
- ai_prompt_cache
- ai_job_queue
- meetily_import_sessions
- ai_generated_drafts
- ai_audit_events
- privacy_consents
- data_subject_requests
- vector_sync_state optional/P1

## 5. Freeze rules

- Không thêm AI function mới trước khi 8 AI P0 xanh.
- Không bật Qdrant semantic search nếu Week 7 chưa xong chat summary + sprint summary.
- Không sửa core Meetily; chỉ dùng export/import hoặc connector nhẹ.
- Không gọi AI API từ frontend.
- Không lưu API key trong repo.
- Không để AI tạo task/assignee/deadline chính thức nếu user chưa confirm.
