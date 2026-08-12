const XLSX = require('xlsx')
const path = require('path')

const summary = [
  ['Mã', 'Nhân sự', 'Nhóm', 'Vai trò trong sprint', 'Mục tiêu chính', 'Ưu tiên', 'Thời hạn', 'Trạng thái'],
  ['AI-QB', 'Quốc Bảo', 'AI', 'AI Permission Owner', 'Chốt phân quyền AI theo role; Member chỉ được xem tiến độ và tóm tắt', 'P0', 'Ngày 1–3', 'Chưa bắt đầu'],
  ['AI-CK', 'Chí Khang', 'AI', 'AI Workflow Owner', 'Hoàn thiện 4 luồng AI trọng tâm và các trạng thái vận hành', 'P0/P1', 'Ngày 2–5', 'Chưa bắt đầu'],
  ['DEV-VM', 'Viết Minh', 'Dev', 'RBAC & Custom Role Owner', 'Hoàn thiện RBAC và custom role end-to-end', 'P0', 'Ngày 1–4', 'Chưa bắt đầu'],
  ['DEV-GL', 'Gia Long', 'Dev', 'Project UI & Onboarding Owner', 'Đóng lỗi member visibility, Project Detail và onboarding', 'P0', 'Ngày 1–5', 'Chưa bắt đầu'],
  ['QA-DT', 'Đoàn Trung', 'Test', 'QA & Acceptance Owner', 'E2E đa role, Excel kết quả, evidence và tài liệu demo', 'P0', 'Ngày 1–7', 'Chưa bắt đầu'],
]

const tasks = [
  ['QB-01', 'Quốc Bảo', 'AI', 'Lập và chốt AI capability matrix cho Owner/Manager/Specialist/Member/Viewer', 'P0', 'Ngày 1', 'AI capability matrix được review', 'Viết Minh', 'Chưa bắt đầu'],
  ['QB-02', 'Quốc Bảo', 'AI', 'Chuyển Member sang AI read-only: chỉ progress và summary', 'P0', 'Ngày 2', 'Member không còn AI write tools', 'QB-01', 'Chưa bắt đầu'],
  ['QB-03', 'Quốc Bảo', 'AI', 'Giữ Developer/Tester/Reviewer ở Specialist và Owner/Manager ở Full', 'P0', 'Ngày 2', 'Tier đúng với mọi role demo', 'QB-01', 'Chưa bắt đầu'],
  ['QB-04', 'Quốc Bảo', 'AI', 'Bổ sung unit/integration test: allowed, denied, outsider, cross-organization', 'P0', 'Ngày 3', 'Toàn bộ test AI permission pass', 'QB-02, QB-03', 'Chưa bắt đầu'],
  ['QB-05', 'Quốc Bảo', 'AI', 'Bàn giao contract capability và danh sách tool cho Chí Khang/Đoàn Trung', 'P0', 'Ngày 3', 'Contract và test cases được bàn giao', 'QB-04', 'Chưa bắt đầu'],

  ['CK-01', 'Chí Khang', 'AI', 'Hoàn thiện AI tóm tắt tiến độ dự án có nguồn tham chiếu', 'P0', 'Ngày 2–3', 'Happy/error/empty/retry hoạt động', 'QB-01', 'Chưa bắt đầu'],
  ['CK-02', 'Chí Khang', 'AI', 'Hoàn thiện phân tích task chậm và rủi ro', 'P1', 'Ngày 3–4', 'Kết quả grounded, có source reference', 'CK-01', 'Chưa bắt đầu'],
  ['CK-03', 'Chí Khang', 'AI', 'Hoàn thiện gợi ý assignee theo role, skill và workload', 'P1', 'Ngày 3–4', 'Không rò dữ liệu workload trái quyền', 'QB-03, Viết Minh', 'Chưa bắt đầu'],
  ['CK-04', 'Chí Khang', 'AI', 'Hoàn thiện task draft có bước review/sửa/xác nhận trước mutation', 'P0', 'Ngày 4–5', 'Không ghi dữ liệu trước xác nhận', 'CK-01', 'Chưa bắt đầu'],
  ['CK-05', 'Chí Khang', 'AI', 'Bổ sung audit log, provider fallback và integration test cho 4 luồng', 'P0', 'Ngày 5', 'Test pass; audit và fallback có evidence', 'CK-01..04', 'Chưa bắt đầu'],

  ['VM-01', 'Viết Minh', 'Dev', 'Hoàn thiện CRUD custom role cho Organization Owner', 'P0', 'Ngày 1–2', 'Create/edit/deactivate hoạt động đúng quyền', 'Không', 'Chưa bắt đầu'],
  ['VM-02', 'Viết Minh', 'Dev', 'Bổ sung tên, mô tả, base role, skill tags, AI tier và preview quyền', 'P0', 'Ngày 2–3', 'UI/API trả cùng permission matrix', 'VM-01, Quốc Bảo', 'Chưa bắt đầu'],
  ['VM-03', 'Viết Minh', 'Dev', 'Chặn role trùng/built-in spoof và không cho vượt quyền base role', 'P0', 'Ngày 3', 'Negative tests pass', 'VM-01', 'Chưa bắt đầu'],
  ['VM-04', 'Viết Minh', 'Dev', 'Xử lý đổi/vô hiệu hóa role đang được sử dụng và audit thay đổi', 'P1', 'Ngày 3–4', 'Không tạo membership mồ côi', 'VM-02', 'Chưa bắt đầu'],
  ['VM-05', 'Viết Minh', 'Dev', 'Kiểm tra API trực tiếp: Member không thể đổi role/quản lý thành viên', 'P0', 'Ngày 4', 'API trái quyền trả 403', 'VM-01..04', 'Chưa bắt đầu'],

  ['GL-01', 'Gia Long', 'Dev', 'Đóng luồng Manager thêm Member và Member nhìn thấy project ngay', 'P0', 'Ngày 1–2', 'Không cần sửa DB thủ công', 'Viết Minh', 'Chưa bắt đầu'],
  ['GL-02', 'Gia Long', 'Dev', 'Sửa Activity tab chỉ lấy dữ liệu đúng project', 'P0', 'Ngày 2–3', 'Không lẫn activity project khác', 'Không', 'Chưa bắt đầu'],
  ['GL-03', 'Gia Long', 'Dev', 'Khôi phục khả năng truy cập Workload và Gantt/Attention tabs', 'P0', 'Ngày 3', 'Tabs hiển thị và điều hướng được', 'Không', 'Chưa bắt đầu'],
  ['GL-04', 'Gia Long', 'Dev', 'Hoàn thiện onboarding theo role: tối đa 5 bước, CTA, câu hỏi AI mẫu', 'P1', 'Ngày 4', 'Guide ngắn và đúng quyền', 'Quốc Bảo', 'Chưa bắt đầu'],
  ['GL-05', 'Gia Long', 'Dev', 'Thêm đóng/không hiện lại/mở lại guide; chuẩn hóa demo seed và tài khoản', 'P1', 'Ngày 5', 'Seed lặp lại được; tài khoản login được', 'GL-04', 'Chưa bắt đầu'],

  ['DT-01', 'Đoàn Trung', 'Test', 'Viết acceptance criteria và testcase cho toàn bộ role demo', 'P0', 'Ngày 1–2', 'Testcase được review trước khi dev hoàn tất', 'Quốc Bảo, Viết Minh', 'Chưa bắt đầu'],
  ['DT-02', 'Đoàn Trung', 'Test', 'Viết E2E Manager thêm Member → Member thấy/mở project', 'P0', 'Ngày 2–3', 'E2E pass ổn định', 'GL-01', 'Chưa bắt đầu'],
  ['DT-03', 'Đoàn Trung', 'Test', 'Kiểm tra UI ẩn thao tác và API trái quyền trả 403 cho từng role', 'P0', 'Ngày 3–5', 'Có screenshot/API evidence', 'QB-04, VM-05', 'Chưa bắt đầu'],
  ['DT-04', 'Đoàn Trung', 'Test', 'Regression 4 luồng AI, custom role, tabs và onboarding', 'P0', 'Ngày 5–6', 'Không còn defect P0/P1', 'Tất cả nhóm', 'Chưa bắt đầu'],
  ['DT-05', 'Đoàn Trung', 'Test', 'Cập nhật traceability, defect log, evidence index và kết quả test', 'P0', 'Ngày 6', 'Workbook đầy đủ bằng chứng', 'DT-01..04', 'Chưa bắt đầu'],
  ['DT-06', 'Đoàn Trung', 'Test', 'Cập nhật kịch bản/PDF demo và tổ chức rehearsal 2 lần', 'P0', 'Ngày 7', 'Hai lần demo trọn luồng; biên bản pass', 'DT-05', 'Chưa bắt đầu'],
]

const timeline = [
  ['Ngày', 'Trọng tâm', 'Quốc Bảo', 'Chí Khang', 'Viết Minh', 'Gia Long', 'Đoàn Trung'],
  ['Ngày 1', 'Chốt contract', 'AI matrix', 'Đọc contract/chuẩn bị luồng', 'Custom role contract', 'Kiểm tra visibility', 'Acceptance criteria'],
  ['Ngày 2', 'Core fixes', 'Sửa Member tier', 'Progress summary', 'CRUD role', 'Member visibility', 'Viết testcase/E2E'],
  ['Ngày 3', 'Permission & UI', 'Permission tests', 'Risk/assignment', 'Role guard/preview', 'Activity/Workload/Gantt', 'Chạy RBAC tests'],
  ['Ngày 4', 'AI workflow', 'Hỗ trợ capability', 'Assignment/task draft', 'Role lifecycle', 'Onboarding', 'Negative/API tests'],
  ['Ngày 5', 'Integration', 'Review permission', 'Audit/fallback/tests', 'Fix defect', 'Seed/fix defect', 'Regression'],
  ['Ngày 6', 'Acceptance', 'Fix defect', 'Fix defect', 'Fix defect', 'Fix defect', 'Excel/evidence/docs'],
  ['Ngày 7', 'Release gate', 'Hỗ trợ demo', 'Hỗ trợ demo', 'Hỗ trợ demo', 'Hỗ trợ demo', '2 lần rehearsal'],
]

const acceptance = [
  ['Mã gate', 'Tiêu chí đóng sprint', 'Chủ trì', 'Bằng chứng', 'Trạng thái'],
  ['GATE-01', 'Unit và integration test liên quan RBAC/AI đều pass', 'Quốc Bảo, Viết Minh', 'Test report/log', 'Chưa kiểm tra'],
  ['GATE-02', 'Typecheck và production build pass', 'Gia Long', 'Build log', 'Chưa kiểm tra'],
  ['GATE-03', 'E2E đa role pass trên browser', 'Đoàn Trung', 'Playwright report/video/screenshot', 'Chưa kiểm tra'],
  ['GATE-04', 'Không còn defect P0/P1 trong phạm vi demo', 'Đoàn Trung', 'Defect log', 'Chưa kiểm tra'],
  ['GATE-05', 'Member chỉ dùng AI progress/summary và không vượt quyền qua API', 'Quốc Bảo', 'Integration/E2E evidence', 'Chưa kiểm tra'],
  ['GATE-06', 'Manager thêm Member và Member thấy project ngay', 'Gia Long', 'E2E evidence', 'Chưa kiểm tra'],
  ['GATE-07', 'Custom role hiển thị đúng permission và AI tier', 'Viết Minh', 'API/UI evidence', 'Chưa kiểm tra'],
  ['GATE-08', 'Excel, traceability, guide và PDF demo đã cập nhật', 'Đoàn Trung', 'Danh mục tài liệu', 'Chưa kiểm tra'],
  ['GATE-09', 'Demo rehearsal trọn vẹn hai lần', 'Đoàn Trung', 'Biên bản rehearsal', 'Chưa kiểm tra'],
]

const rules = [
  ['Hạng mục', 'Quy định'],
  ['Definition of Done', 'Mỗi công việc phải có code hoặc tài liệu, automated test phù hợp, evidence và cập nhật trạng thái.'],
  ['Quyền AI', 'Member chỉ xem tiến độ và tóm tắt; các thao tác ghi phải bị chặn cả UI lẫn API.'],
  ['Custom role', 'Role chuyên sâu kế thừa base role; không tự mở rộng quyền ngoài base role.'],
  ['Review', 'Không tự đóng task; người khác trong nhóm review trước khi chuyển Hoàn thành.'],
  ['Defect', 'P0/P1 phải sửa trước demo; P2 phải ghi rõ workaround/known limitation.'],
  ['Evidence', 'Case quan trọng cần screenshot, API response hoặc automated test report.'],
  ['Phạm vi', 'Không mở thêm module mới trong sprint demo acceptance closure.'],
]

const workbook = XLSX.utils.book_new()

function addSheet(name, data, widths, rowHeight = 44) {
  const sheet = XLSX.utils.aoa_to_sheet(data)
  sheet['!cols'] = widths.map((wch) => ({ wch }))
  sheet['!autofilter'] = { ref: XLSX.utils.encode_range({ s: { r: 0, c: 0 }, e: { r: data.length - 1, c: data[0].length - 1 } }) }
  sheet['!freeze'] = { xSplit: 0, ySplit: 1, topLeftCell: 'A2', activePane: 'bottomLeft', state: 'frozen' }
  sheet['!rows'] = [{ hpt: 28 }, ...data.slice(1).map(() => ({ hpt: rowHeight }))]
  XLSX.utils.book_append_sheet(workbook, sheet, name)
}

addSheet('Tong_quan', summary, [10, 18, 12, 25, 65, 12, 14, 18])
addSheet('Cong_viec_chi_tiet', tasks, [10, 18, 12, 70, 10, 15, 55, 28, 18], 54)
addSheet('Timeline_7_ngay', timeline, [12, 25, 28, 28, 28, 28, 30])
addSheet('Tieu_chi_nghiem_thu', acceptance, [12, 65, 24, 40, 18])
addSheet('Quy_uoc', rules, [24, 100])

const output = path.resolve('Ke_hoach_phan_cong_hoan_thien_Qaly_12-08-2026.xlsx')
XLSX.writeFile(workbook, output, { compression: true })
console.log(output)
