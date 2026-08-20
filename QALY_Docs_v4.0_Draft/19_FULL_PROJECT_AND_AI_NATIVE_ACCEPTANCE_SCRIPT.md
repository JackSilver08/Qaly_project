# Qaly V4 — Kịch bản nghiệm thu toàn dự án và AI Native

## 1. Cách dùng

- Chạy từng prompt riêng, theo thứ tự. Không dán cả tài liệu trong một lượt.
- Dùng Admin/Organization Admin cho nhóm A–D; lặp lại nhóm E bằng Member để kiểm quyền.
- Với thao tác ghi dữ liệu: chỉnh card nếu cần, xác nhận đúng một lần, mở deep-link và reload để kiểm dữ liệu còn tồn tại.
- Mọi kết luận `PASS` phải có dữ liệu canonical sau reload. Card, toast hoặc text “thành công” không phải bằng chứng đủ.
- Dùng tiền tố `E2E-AI-<ngày>-` cho Project/Task test để dễ dọn dữ liệu.

## 2. AI nằm ở đâu ngoài Trợ lý AI và trang Phân tích?

| Bề mặt | Vai trò AI | Mức thực thi hiện tại | Bằng chứng cần kiểm |
|---|---|---|---|
| Dashboard | Strategic Overview, nút hỏi theo Project/Task/Team | Đọc/phân tích, mở Assistant | Kết quả có nguồn và navigation |
| Project Launch | Launch Brief, Rulebook, staffing, phase/Sprint/Task | Draft → một xác nhận → ghi graph Project thật | Receipt + Project/Sprint/Task/member tồn tại sau reload |
| Project Capacity | Portfolio Capacity & Schedule Copilot | Draft người/lịch → xác nhận → ghi Task thật | Receipt `readBackVerified=true` |
| Kanban/Task detail | Skill evidence, ứng viên, tự giao có kiểm soát | Chọn thủ công hoặc dùng cùng Schedule executor | Assignee/lịch thật; estimate/label/Sprint không bị mất |
| Task form | Gợi ý priority | Gợi ý hỗ trợ, người dùng quyết định | Giá trị gợi ý có thể áp dụng/chỉnh |
| Task Hub | Acceptance checklist, breakdown subtask/dependency | Native draft → confirm → canonical rows | Đúng số row/subtask/dependency |
| Task hoàn tất | Completion attribution → skill evidence | Review/xác nhận evidence | Evidence không sinh từ label/chat riêng tư |
| Roadmap | Đề xuất roadmap/Sprint/mốc | Reviewable proposal | Không ghi ngầm; dữ liệu sau confirm khớp |
| Wiki | Tóm tắt có nguồn, Wiki brief → Task | Native action có source revision | Task tùy chọn và link Wiki đọc lại được |
| Group/Poll | Gợi ý/tạo Poll có cấu trúc | Native draft → confirm | Poll/options thật sau reload |
| Meeting | Auto Checknote/action extraction | Hỗ trợ review, privacy gate | Không tự tạo Task trước xác nhận |
| Project Digest | Cấu hình weekly digest | Native preference draft → confirm | Subscription/revision thật |
| Settings | Model, AI budget, privacy | Cấu hình AI | Đọc lại đúng model/policy đã lưu |

Calendar/repository/invitation/webhook/deployment bên ngoài chỉ được ghi `EXTERNAL_DEFERRED` khi chưa có adapter thật; không tính là PASS.

## 3. Prompt test AI Native — chạy từng prompt một

> **Trạng thái kế hoạch:** toàn bộ P01–P28 được theo dõi riêng tại §39 của `18_AI_NATIVE_MODULE_COVERAGE_AND_INCREMENT_PLAN.md`. Phần 1–2 đã được Product Owner chấp nhận làm hướng dẫn/phạm vi; không dùng các tuyên bố `INTERNAL_COMPLETE` lịch sử để bỏ qua prompt nào trong phần 3. Mỗi prompt chỉ PASS khi đủ routing, context/source, renderer, interaction và canonical read-back tương ứng.

### A. Khả năng, dữ liệu và hội thoại

**P01 — Khả năng theo quyền**

> Dựa trên role và ngữ cảnh hiện tại, hãy cho tôi biết bạn làm được gì. Trả bằng các card hành động ngắn, chia rõ: chỉ xem, tạo bản nháp, cần xác nhận và chưa được hỗ trợ. Mỗi card có nút mở đúng màn hình.

PASS: không trả pitch chung; capability đúng role; nút điều hướng chạy được.

**P02 — Workspace bằng dữ liệu thật**

> Tóm tắt workspace hiện tại: số Project đang hoạt động, tiến độ, task quá hạn, workload cao và ba việc cần chú ý. Dùng metric/table/card phù hợp, có link nguồn; phần quy trình collapse mặc định.

PASS: số liệu khớp Dashboard/Projects/Tasks; không hiện `not_reached` với độ tin cậy 100%.

**P03 — Phân tích một Project**

> Phân tích Project đang chọn: mục tiêu, tiến độ Sprint, task nghẽn, dependency, workload, rủi ro deadline và ba hành động ưu tiên. Chỉ dùng dữ liệu tôi được phép xem.

PASS: Project context đúng; card mở đúng Task/Sprint; private source không rò nội dung.

**P04 — Nhớ ngữ cảnh phiên**

> Ghi nhớ rằng trong phiên này “MVP” nghĩa là ba chức năng: đăng ký, đặt lịch và thanh toán. Chưa tạo dữ liệu; chỉ xác nhận ngắn.

Sau đó chạy:

> Với MVP vừa nói, đề xuất thước đo thành công và các khối công việc chính. Không hỏi lại ba chức năng.

PASS: không hỏi lại; reload rồi mở lại phiên vẫn giữ đúng context.

**P05 — Lịch sử session**

> Đặt tiêu đề phiên này là “E2E-AI kiểm thử toàn luồng”, sau đó tóm tắt các quyết định đã thống nhất trong phiên.

PASS: danh sách Phiên có session riêng, mở lại được sau reload; không chỉ là cuộn tin nhắn cũ.

### B. Khởi chạy Project từ đầu đến cuối

**P06 — Thu thập unknown có cấu trúc**

> Khởi chạy Project `E2E-AI-SPA-Dịch-vụ` cho web SPA đặt dịch vụ theo gói. Hãy tái sử dụng dữ kiện tôi đã nói, chỉ hỏi tối đa ba unknown thực sự chặn việc lập phương án. Câu hỏi phải là form/card có option phổ biến và “Khác”, cho phép nhập tự do, lưu nhiều câu trả lời trước khi gửi.

PASS: không trả textarea thuần; gõ không tự gửi; câu trả lời còn sau reload.

**P07 — Trả lời unknown**

> Thời hạn 12 tuần; người dùng chính là khách hàng cá nhân; bắt buộc có đăng ký/đăng nhập, đặt dịch vụ + phòng, thanh toán và dashboard quản lý. Mục tiêu: 95% luồng đặt dịch vụ E2E pass, p95 API dưới 500ms, không có lỗi Critical khi nghiệm thu.

PASS: các mục tiêu định lượng được map vào Launch Brief; không hỏi lại.

**P08 — Staffing và delivery plan**

> Lập ba phương án manager/team dựa trên skill evidence, capacity đã khai báo, lịch vắng và tải đa dự án. Sau đó chia phase, Sprint, Task, dependency, estimate, required skill và assignee. Cho phép thay người, thêm/bớt người, đổi độ dài Sprint và sửa Task trước xác nhận. Không coi chỗ trống là capacity.

PASS: số người phụ thuộc scope, không cố định 3; có lý do/trade-off; thiếu capacity thật phải chặn và hướng dẫn bổ sung.

**P09 — Ghi Project graph thật**

> Dùng phương án đang chọn. Trước khi ghi hãy hiện một card review cuối gồm Project, manager/team, phase, Sprint và tổng số Task. Chờ đúng một xác nhận của tôi.

Sau khi review, bấm xác nhận trên card — không nhắn “xác nhận” nhiều lần.

PASS: Project, membership, Sprint, Task, dependency và skill requirement tồn tại; receipt có deep-link và read-back; reload không tạo bản sao.

**P10 — Idempotency**

> Mở lại kết quả thực thi Project vừa rồi và kiểm tra xem retry cùng yêu cầu có tạo trùng Project, Sprint hoặc Task không. Chỉ báo theo dữ liệu đọc lại.

PASS: cùng execution/idempotency key trả cùng receipt; không tăng số row.

### C. Task creation, breakdown và phân công

**P11 — Tạo đúng số lượng Task**

> Trong Project đang chọn, soạn đúng 10 Task cho Sprint 1: khảo sát, user flow, UI kit, API contract, database, auth, booking, payment, test E2E và tài liệu vận hành. Mỗi Task có mô tả, acceptance criteria, estimate, dependency, priority và required skill. Mở bản nháp để tôi chỉnh; chưa ghi dữ liệu.

PASS: card có đúng 10 Task, không rút còn 3; confirm tạo đúng số đã chọn.

**P12 — Tự giao Task từ Assistant**

> Với Task đang mở, hãy lập phương án giao việc và lịch. Đối chiếu required skill, evidence đã xác nhận, capacity thật, availability, deadline và tải ở tất cả Project; cho tôi đổi ứng viên hoặc ngày trước khi xác nhận.

PASS: intent là `task.assignment_schedule.v1`, mở card/Task đúng — không sang tạo Project hay tạo Task mới.

**P13 — Xác nhận tự giao**

> Giữ phương án hiện tại và cho tôi card xác nhận cuối. Không ghi trước khi tôi bấm xác nhận.

PASS: sau bấm xác nhận, receipt `succeeded`, `readBackVerified=true`; Task có đúng một assignee; retry không tạo lặp.

**P14 — Chặn lịch/capacity sai**

> Thử đề xuất giao Task này cho một người đang không sẵn sàng hoặc không đủ capacity trong cửa sổ hiện tại; giải thích ngắn phương án thay thế.

PASS: không cho confirm bằng capacity mặc định 40h, không nhét việc vào phút trống; không mutation.

**P15 — Stale source**

> Lập bản nháp giao Task, nhưng trước khi xác nhận hãy thay đổi Task ở màn hình khác; sau đó xác nhận bản nháp cũ.

PASS: trả conflict/stale-source và yêu cầu lập lại; không áp dụng dở dang.

**P16 — Acceptance checklist**

> Với Task đang mở, soạn 5 mục acceptance checklist kiểm chứng được, cho phép sửa từng mục và chờ một xác nhận trước khi lưu.

PASS: đúng 5 canonical checklist rows và receipt đọc lại đủ.

**P17 — Breakdown**

> Tách Task đang mở thành 4 subtask theo thứ tự thực hiện, có dependency, estimate và required skill; mở card review trước khi tạo.

PASS: đúng 4 subtask, parent/dependency đúng, reload còn dữ liệu.

### D. Các AI surface khác

**P18 — Wiki**

> Tóm tắt Wiki đang mở thành brief có link tới section nguồn; đề xuất tối đa 3 Task tùy chọn. Chỉ tạo các Task tôi tick chọn sau một xác nhận.

**P19 — Poll**

> Trong Group đang mở, soạn Poll chọn phương án triển khai với 4 option rõ ràng, deadline 3 ngày và cho sửa trước khi xác nhận.

**P20 — Meeting**

> Từ transcript cuộc họp đang mở, trích quyết định, blocker và action item. Không tạo Task tự động; cho phép map từng action item sang Task có sẵn hoặc bản nháp Task mới.

**P21 — Roadmap/Sprint**

> Đánh giá roadmap Project hiện tại và đề xuất điều chỉnh Sprint theo dependency, capacity và deadline. Hiện before/after và không ghi trước xác nhận.

**P22 — Weekly digest**

> Cấu hình weekly digest cho Project này vào 09:00 thứ Hai theo timezone của tổ chức; hiện card review và đọc lại subscription sau xác nhận.

**P23 — Skill evidence**

> Với Task vừa hoàn tất, đề xuất attribution và skill evidence theo tiêu chí nghiệm thu đã xác nhận. Không dùng label hoặc tin nhắn riêng làm bằng chứng.

**P24 — Monitor/replan**

> So sánh Project hiện tại với baseline đã xác nhận, chỉ ra drift về scope, lịch, staffing và task; đề xuất replan có before/after nhưng chưa mutation.

### E. Quyền, fallback, UX và tính trung thực

**P25 — Member read-only**

> Tóm tắt Project và tài liệu tôi được phép xem, sau đó đề xuất ba việc nên làm. Nếu tôi không có quyền tạo/giao Task, vẫn trả phân tích hữu ích và nút mở dữ liệu nguồn; không hiện nút xác nhận mutation.

**P26 — Provider failure**

> Phân tích Project hiện tại và đề xuất bước tiếp theo. Nếu model lỗi, dùng fallback server có ích, ghi actual provider/model và tiếp tục; không báo thành công cho thao tác chưa ghi.

**P27 — Renderer và navigation**

> Liệt kê ba Task quá hạn, workload ba người cao nhất và Sprint có nguy cơ. Dùng metric/table/task cards phù hợp; mỗi item có nút mở đúng đối tượng, không trả một khối text dài.

**P28 — External adapters**

> Kiểm tra khả năng đồng bộ calendar, repository, invitation, webhook và deployment cho Project này. Chỉ đánh dấu hoàn thành nếu adapter thật đã đọc/ghi và read-back; phần chưa có phải ghi EXTERNAL_DEFERRED.

## 4. Test thủ công bắt buộc ngoài chat

1. Auth: đăng nhập/sai mật khẩu/đăng xuất/session timeout/refresh.
2. Organization: member, custom role, Rulebook draft/activate, quyền cross-tenant.
3. Project: tạo/sửa/archive/restore, member/role, settings và deep-link.
4. Kanban: tạo/sửa/xóa, drag/drop hợp lệ và bị chặn, batch action, filter/search.
5. Giao thủ công tại Kanban: chọn assignee → Lưu → reload; kiểm assignee đổi nhưng estimate, actual hours, label và Sprint giữ nguyên.
6. Tự giao tại Kanban: Task detail → Tự giao có kiểm soát → sửa ứng viên/lịch → xác nhận → reload; kiểm receipt và đúng một TaskAssignment.
7. Sprint/Roadmap: CRUD Sprint/milestone, link Task, dependency, nghiệm thu mốc.
8. Time tracking/comment/file/evidence: start/stop/manual log, permission, upload/delete, review evidence.
9. Team/skill: taxonomy mặc định, custom skill, evidence provenance, capacity và availability.
10. Group/Poll/Meeting: membership, chat, poll/vote, transcript/action item và privacy.
11. Wiki: CRUD, visibility, source link, Task from Wiki.
12. Analytics: metric/table/chart, filter, source, export, parity với Assistant.
13. Notification/digest/settings: lưu, reload, permission, model selection, AI budget/privacy.
14. Import/GitHub/webhook: success, duplicate, undo, credential failure và external-deferred truthfulness.
15. Accessibility/responsive: keyboard, focus, ESC, screen width 1280/768/390, text không tràn, process collapse mặc định.

## 5. Cổng kết luận

Chỉ ghi `COMPLETE` khi:

- typecheck/build và test tập trung PASS;
- mutation chính có integration read-back từ DB;
- manual Kanban và Assistant dùng cùng business constraints;
- Member không nhận mutation capability;
- duplicate/stale/provider failure không tạo dữ liệu sai hoặc dead-end;
- external adapter chưa có được ghi `EXTERNAL_DEFERRED`.
