# Kịch bản kiểm thử End-to-End (E2E) Đa vai trò - Dự án QaLy

## 1. Giới thiệu
Tài liệu này mô tả chi tiết các kịch bản kiểm thử End-to-End (E2E) cho hệ thống QaLy, tập trung vào sự phối hợp giữa các vai trò khác nhau (Admin, Project Manager, Member, QA) trong các luồng nghiệp vụ chính.

## 2. Các luồng nghiệp vụ chính (Main Flows)

### 2.1 Luồng 1: Khởi tạo dự án và Lập kế hoạch AI (AI Project Launch Orchestration)
- **Mục tiêu**: Đảm bảo Project Manager có thể tạo dự án, nhận gợi ý từ AI Action Composer và phân công công việc.
- **Role tham gia**: Project Manager (PM), Admin
- **Các bước**:
  1. [PM] Đăng nhập vào hệ thống.
  2. [PM] Truy cập màn hình tạo Project mới và điền thông tin cơ bản.
  3. [PM] Kích hoạt "AI Assistant Goal Planner" để tự động tạo cấu trúc Sprint/Task.
  4. [PM] Lưu dự án và kiểm tra Workspace.
- **Kết quả mong đợi**: Dự án được tạo thành công, các Task/Sprint được AI sinh ra đầy đủ và hiển thị trên Roadmap UI.

### 2.2 Luồng 2: Phân công kỹ năng & Giao việc (Member Skill Assignment)
- **Mục tiêu**: Kiểm tra việc phân quyền và gán task tự động theo kỹ năng.
- **Role tham gia**: Project Manager (PM), Member, AI Assistant
- **Các bước**:
  1. [PM] Gán "Skill" cho các thành viên trong dự án.
  2. [PM] Sử dụng "Task Skills AI" để tự động assign các task phù hợp cho Member.
  3. [Member] Đăng nhập và kiểm tra Dashboard/Task list.
  4. [Member] Cập nhật tiến độ task (Progress).
- **Kết quả mong đợi**: Member nhận đúng task dựa trên Skill. AI Assistant tối ưu hóa việc phân công chính xác. Tiến độ hiển thị đúng trên Dashboard.

### 2.3 Luồng 3: Quy trình đảm bảo an toàn, bảo mật & Cập nhật tiến độ (RC Safety Workflow & Privacy)
- **Mục tiêu**: Đảm bảo dữ liệu riêng tư không bị rò rỉ và quy trình Review Code/Task diễn ra đúng chuẩn.
- **Role tham gia**: Member, QA, Project Manager
- **Các bước**:
  1. [Member] Hoàn thành task và push lên (Trigger Safety Workflow).
  2. [QA] Nhận thông báo review, kiểm tra theo Checklist/Quy chuẩn (Privacy Retention).
  3. [QA] Approve/Reject task.
  4. [PM] Xem "Project Progress AI" dashboard để theo dõi tiến trình thực thời.
- **Kết quả mong đợi**: Không vi phạm privacy, workflow safety hoạt động chặn các hành vi sai, Progress Dashboard update real-time.

## 3. Cấu trúc Báo cáo Test (Excel)
Hệ thống đã sinh động một file `testmoinhat.xlsx` trong thư mục gốc. File này bao gồm:
- **Sheet `Test_Results`**: Template theo dõi Kết quả Test (Pass/Fail) và Evidence.
- **Sheet `Demo_Script`**: Kịch bản thực hiện Demo hệ thống.

## 4. Tự động hóa (Automation)
Dự án sử dụng Playwright cho E2E Testing (`tests/e2e/`).
Các bài test có thể được chạy tự động qua lệnh:
`npm run test:e2e` hoặc `npm run test:e2e:basic`.
Báo cáo sẽ được xuất ra tại thư mục `playwright-report/` hoặc `test-results/`.
