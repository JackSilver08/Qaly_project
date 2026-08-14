import * as xlsx from 'xlsx';
import fs from 'fs';

// Tạo Workbook mới
const wb = xlsx.utils.book_new();

// 1. Sheet: Test Results
const testResultsData = [
    ['Test Case ID', 'Module', 'Scenario', 'Role', 'Expected Result', 'Actual Result', 'Status (Pass/Fail)', 'Evidence (Link/Img)', 'Tester', 'Date'],
    ['TC_E2E_001', 'Auth', 'public login page renders correctly', 'Guest', 'Trang hiển thị đầy đủ', 'Đúng như mong đợi', 'Pass', 'Playwright Automated', 'Bot', new Date().toLocaleDateString()],
    ['TC_E2E_002', 'Auth', 'seeded admin can login and see authenticated shell', 'Admin', 'Đăng nhập thành công', 'Đúng như mong đợi', 'Pass', 'Playwright Automated', 'Bot', new Date().toLocaleDateString()],
    ['TC_E2E_003', 'Dashboard', 'dashboard renders seeded demo data', 'Admin', 'Hiển thị dữ liệu demo', 'Đúng như mong đợi', 'Pass', 'Playwright Automated', 'Bot', new Date().toLocaleDateString()],
    ['TC_E2E_004', 'Navigation', 'primary authenticated navigation routes render', 'Admin', 'Các trang chuyển hướng thành công', 'Đúng như mong đợi', 'Pass', 'Playwright Automated', 'Bot', new Date().toLocaleDateString()],
    ['TC_E2E_005', 'Project', 'projects page shows seeded project or empty-state safely', 'Admin', 'Hiển thị danh sách dự án', 'Đúng như mong đợi', 'Pass', 'Playwright Automated', 'Bot', new Date().toLocaleDateString()],
    ['TC_E2E_006', 'Project Launch', 'AI Planner creates project & sprint', 'PM', 'Dự án và Sprint được tạo', 'Lỗi hiển thị receipt', 'Fail', 'ai-action-composer.spec.ts', 'Bot', new Date().toLocaleDateString()],
    ['TC_E2E_007', 'Skill Assignment', 'AI auto-assign task theo skill', 'PM, Member', 'Task gán đúng người', 'Đúng như mong đợi', 'Pass', 'member-skill-assignment.spec.ts', 'Bot', new Date().toLocaleDateString()],
    ['TC_E2E_008', 'Safety Workflow', 'QA review và duyệt task', 'QA, Member', 'Trạng thái cập nhật đúng', 'Đúng như mong đợi', 'Pass', 'rc-safety-workflow.spec.ts', 'Bot', new Date().toLocaleDateString()],
    ['TC_E2E_009', 'Privacy Retention', 'Kiểm tra mask dữ liệu nhạy cảm', 'Admin, Member', 'Dữ liệu được che giấu', 'Đúng như mong đợi', 'Pass', 'privacy-retention.spec.ts', 'Bot', new Date().toLocaleDateString()],
    ['TC_E2E_010', 'AI Grounding', 'AI preserves exact selected-message grounding', 'Member', 'AI đọc đúng message', 'Fail do model timeout', 'Fail', 'ai-native-grounded-surfaces.spec.ts', 'Bot', new Date().toLocaleDateString()],
    ['TC_E2E_011', 'Demo Script', 'Chạy xuyên suốt kịch bản 30p', 'All', 'Hoàn thành đúng kịch bản', 'Fail (Expected <=8 members, got 10)', 'Fail', 'demo-script-30-minutes.spec.ts', 'Bot', new Date().toLocaleDateString()],
    ['TC_E2E_012', 'General', 'Tổng hợp 53 test cases khác', 'All', 'Các luồng cơ bản hoạt động tốt', 'Thành công', 'Pass', 'playwright-report', 'Bot', new Date().toLocaleDateString()]
];
const wsTestResults = xlsx.utils.aoa_to_sheet(testResultsData);

// Styling cho header Test Results (đơn giản qua text)
wsTestResults['!cols'] = [
    {wch: 15}, {wch: 20}, {wch: 40}, {wch: 15}, {wch: 30}, {wch: 30}, {wch: 15}, {wch: 30}, {wch: 15}, {wch: 15}
];
xlsx.utils.book_append_sheet(wb, wsTestResults, 'Test_Results');

// 2. Sheet: Demo Script
const demoScriptData = [
    ['Step', 'Actor', 'Action', 'System Response', 'Notes'],
    ['1', 'PM', 'Đăng nhập vào hệ thống bằng tài khoản PM', 'Hiển thị Dashboard của PM', 'Nhấn mạnh giao diện Dashboard AI'],
    ['2', 'PM', 'Nhấn "Create Project", dùng AI Action Composer', 'AI tự động sinh cấu trúc Task và Sprint', 'Demo tốc độ AI generating'],
    ['3', 'PM', 'Dùng Task Skills AI phân công tự động', 'Hệ thống map task với skill của Member', ''],
    ['4', 'Member', 'Đăng nhập, xem task được giao, cập nhật tiến độ', 'Task chuyển sang In Progress/Done', ''],
    ['5', 'QA', 'Review kết quả, Approve', 'Cập nhật Project Progress AI real-time', 'Xem biểu đồ tiến độ']
];
const wsDemoScript = xlsx.utils.aoa_to_sheet(demoScriptData);
wsDemoScript['!cols'] = [
    {wch: 10}, {wch: 15}, {wch: 50}, {wch: 50}, {wch: 30}
];
xlsx.utils.book_append_sheet(wb, wsDemoScript, 'Demo_Script');

const buffer = xlsx.write(wb, { type: 'buffer', bookType: 'xlsx' });
fs.writeFileSync('testmoinhat.xlsx', buffer);
console.log('Đã tạo thành công file testmoinhat.xlsx');
