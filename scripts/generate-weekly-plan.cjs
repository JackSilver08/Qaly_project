const XLSX = require('xlsx')
const path = require('path')

const assignments = [
  ['STT', 'Nhân sự', 'Vai trò tuần này', 'Hạng mục phụ trách', 'Công việc chi tiết', 'Phạm vi code chính', 'Đầu ra bắt buộc', 'Tiêu chí nghiệm thu', 'Ưu tiên', 'Bắt đầu', 'Deadline', 'Trạng thái', 'Phụ thuộc / phối hợp'],
  [1, 'Gia Long', 'Frontend / Navigation', 'Chuẩn hóa cấu trúc điều hướng', 'Gom nhóm menu Quản trị; thống nhất Nhóm/Teams; tổ chức lại tab dự án theo Tổng quan, Công việc, Cộng tác, Tích hợp; giữ redirect cho URL cũ.', 'App.vue; router/index.ts; AppShell/SidebarNav; project tabs', 'Navigation mới hoạt động; URL cũ được redirect; không mất quyền theo role.', 'Build frontend thành công; Admin/Member thấy đúng menu; link cũ không lỗi; mobile navigation dùng được.', 'P0', '04/08/2026 14:00', '05/08/2026 17:00', 'Chưa bắt đầu', 'Trao đổi với Quốc Bảo để tránh cùng sửa ProjectDetailPage.vue.'],
  [2, 'Viết Minh', 'Full-stack / Integration', 'Liên kết GitHub với công việc Qaly', 'Nhận diện task key trong PR/branch; liên kết PR/CI về task; thêm trạng thái PR/CI; hoàn thiện empty/loading/error.', 'GitHubProjectIntegration.vue; GitHubProjectManagement.vue; ProjectGitHub controllers/services', 'PR/workflow có task key mở đúng task Qaly; dữ liệu không có task key vẫn hiển thị.', 'Giữ quyền read-only; backend/frontend build sạch; có test mapping task key; sync cũ không hỏng.', 'P0', '04/08/2026 14:00', '05/08/2026 17:00', 'Chưa bắt đầu', 'Chốt convention task key với Quốc Bảo; báo sớm nếu API thiếu trường.'],
  [3, 'Quốc Bảo', 'Frontend / Task workflow', 'Thống nhất trải nghiệm nhiệm vụ', 'Chuẩn hóa filter, status, priority và deep link giữa trang Nhiệm vụ toàn cục với tab dự án; sửa fallback task key; giữ Kanban/List.', 'TasksPage.vue; task components/composables; phần task trong ProjectDetailPage.vue', 'Bộ lọc và URL mở đúng project/task; trạng thái nhất quán; không còn #undefined.', 'Kanban kéo thả vẫn chạy; List cập nhật status; reload deep link không mất task; build thành công.', 'P0', '04/08/2026 14:00', '05/08/2026 17:00', 'Chưa bắt đầu', 'Chốt vùng sửa ProjectDetailPage với Gia Long trước khi code.'],
  [4, 'Chí Khang', 'Frontend / AI UX', 'Hợp nhất điểm vào Trợ lý Qaly', 'Tạo menu AI gồm Hỏi dữ liệu, Lập kế hoạch, Tạo task, Phân tích rủi ro, Gợi ý phân công; điều hướng tới capability hiện có.', 'AiActionComposerDrawer.vue; analytics-ai components; TopHeader/Floating assistant', 'Một điểm vào AI; mỗi lựa chọn mở đúng luồng; giữ project/task context.', 'Không nhân đôi API; quyền theo role đúng; loading/error có feedback; build thành công.', 'P1', '04/08/2026 14:00', '05/08/2026 17:00', 'Chưa bắt đầu', 'Không sửa model/provider/backend AI trong đợt này.'],
  [5, 'Đoàn Trung', 'Frontend / Project planning', 'Hợp nhất Roadmap và Gantt', 'Đưa Gantt thành chế độ Timeline trong Lộ trình; thêm toggle Journey/Timeline; dùng chung sprint/task data; giữ deep link milestone.', 'ProjectRoadmapTab.vue; ProjectGanttTab.vue; roadmap routing/hash', 'Chuyển được Journey/Timeline; mở task từ timeline; giữ mốc khi đổi view.', 'Không tạo tab Gantt riêng; CRUD/nghiệm thu vẫn chạy; mobile cuộn ngang; build thành công.', 'P1', '04/08/2026 14:00', '05/08/2026 17:00', 'Chưa bắt đầu', 'Phối hợp Quốc Bảo về callback mở task.'],
]

const completed = [
  ['STT', 'Nhân sự', 'Vai trò', 'Công việc', 'Trạng thái', 'Ghi chú'],
  [1, 'Quang Tuấn', 'Trưởng nhóm', 'Phần việc phát triển của trưởng nhóm trong tuần', 'Đã hoàn thành', 'Review và nghiệm thu PR của nhóm.'],
  [2, 'Duy Hoàng', 'Tài liệu', 'Hoàn thiện tài liệu dự án', 'Đã hoàn thành', 'Cập nhật tài liệu nếu code thay đổi hướng dẫn hoặc contract.'],
]

const rules = [
  ['Hạng mục', 'Quy định'],
  ['Mốc thời gian', 'Bắt đầu chiều Thứ Ba 04/08/2026; deadline 17:00 Thứ Tư 05/08/2026, trước Thứ Năm.'],
  ['Nhánh làm việc', 'Mỗi người dùng một feature branch; không commit trực tiếp lên main.'],
  ['Pull request', 'Mở Draft PR trước 11:30 ngày 05/08; chuyển Ready for review trước 15:30.'],
  ['Nghiệm thu', 'Tự kiểm tra build/test; ghi file thay đổi, ảnh UI hoặc API evidence và rủi ro còn lại.'],
  ['Merge conflict', 'Không sửa ngoài phạm vi khi chưa báo; Gia Long và Quốc Bảo chốt vùng ProjectDetailPage trước.'],
  ['Definition of Done', 'Code chạy được; không warning/error mới; workflow cũ không hỏng; có hướng dẫn kiểm thử ngắn.'],
  ['Người nghiệm thu', 'Quang Tuấn review và quyết định merge.'],
  ['Tài liệu', 'Duy Hoàng cập nhật tài liệu khi PR làm đổi hành vi người dùng hoặc contract.'],
]

const workbook = XLSX.utils.book_new()
function addSheet(name, data, widths) {
  const sheet = XLSX.utils.aoa_to_sheet(data)
  sheet['!cols'] = widths.map(wch => ({ wch }))
  sheet['!autofilter'] = { ref: XLSX.utils.encode_range({ s: { r: 0, c: 0 }, e: { r: data.length - 1, c: data[0].length - 1 } }) }
  sheet['!freeze'] = { xSplit: 0, ySplit: 1, topLeftCell: 'A2', activePane: 'bottomLeft', state: 'frozen' }
  sheet['!rows'] = [{ hpt: 28 }, ...data.slice(1).map(() => ({ hpt: 72 }))]
  XLSX.utils.book_append_sheet(workbook, sheet, name)
}

addSheet('Phan_cong_tuan', assignments, [6, 14, 20, 25, 55, 42, 48, 58, 10, 18, 18, 16, 42])
addSheet('Da_hoan_thanh', completed, [6, 18, 20, 50, 18, 55])
addSheet('Quy_uoc_nghiem_thu', rules, [24, 105])

const output = path.resolve('Ke_hoach_phan_cong_Qaly_04-05-08-2026.xlsx')
XLSX.writeFile(workbook, output, { compression: true })
console.log(output)
