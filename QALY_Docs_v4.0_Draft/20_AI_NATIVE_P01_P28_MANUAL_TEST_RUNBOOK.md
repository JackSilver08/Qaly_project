# Qaly V4 — Runbook test thủ công AI Native P01–P28

## 1. Phạm vi và cách chạy

Runbook này chỉ thay cho **phần 3 — Prompt test AI Native**. Phần 1–2 của kịch bản nghiệm thu tổng đã được xem là đạt về phạm vi/hướng dẫn.

- Chạy theo thứ tự P01 → P28; không dán nhiều prompt cùng lúc.
- Dùng Admin/Organization Admin cho P01–P24 và P26–P28; dùng tài khoản Member thật cho P25.
- P04–P05 phải dùng cùng một session. P06–P10 phải dùng cùng một session khác. P11–P17 dùng cùng Project/Task test.
- Trước mỗi nhóm, nhìn scope ở composer: `Tất cả dự án` hoặc đúng Project được yêu cầu. Đổi Project không được tự tạo session mới.
- Với mutation: sửa card nếu cần → bấm xác nhận đúng một lần → mở deep-link → reload → đọc lại dữ liệu canonical. Toast/text “thành công” chưa đủ để PASS.
- Nếu không chuẩn bị được điều kiện lỗi provider/stale/capacity thì ghi `NOT_VERIFIED`, không đánh PASS theo suy đoán.
- Dùng tiền tố `E2E-AI-<ngày>-` cho dữ liệu phát sinh.

### Dữ liệu cần chuẩn bị

| Mã | Dữ liệu/điều kiện |
|---|---|
| D01 | Một Project có Sprint, Task, dependency, member, workload và deadline thật |
| D02 | Một Task mở có required skill; một Task đã Done; một ứng viên thiếu capacity/đang vắng |
| D03 | Skill catalog và skill evidence đã xác nhận cho một số thành viên |
| D04 | Một Wiki có section; một Group có quyền tạo Poll; một Meeting có transcript |
| D05 | Một tài khoản Member chỉ có quyền đọc/phân tích, không có quyền mutation |
| D06 | Cách fault-inject provider ở local/dev hoặc một provider chủ động tắt; không phá/xóa API key thật |

## 2. Ma trận trang — tác dụng — prompt — tiêu chí PASS

### A. Khả năng, dữ liệu và hội thoại

| ID | Trang bắt đầu | Tác dụng cần kiểm | Prompt để dán | PASS cần nhìn thấy |
|---|---|---|---|---|
| P01 | `/dashboard` → mở **Trợ lý AI**, scope `Tất cả dự án` | Capability theo role/ngữ cảnh và navigation | **Dựa trên role và ngữ cảnh hiện tại, hãy cho tôi biết bạn làm được gì. Trả bằng các card hành động ngắn, chia rõ: chỉ xem, tạo bản nháp, cần xác nhận và chưa được hỗ trợ. Mỗi card có nút mở đúng màn hình.** | Không trả pitch chung; nhóm quyền đúng role; card/nút mở đúng màn hình; không có nút mutation ngoài quyền. |
| P02 | `/dashboard` → Trợ lý AI; sau đó chạy lại ở `/analytics` với cùng filter | Dữ liệu thật và parity giữa Assistant/Phân tích | **Tóm tắt workspace hiện tại: số Project đang hoạt động, tiến độ, task quá hạn, workload cao và ba việc cần chú ý. Dùng metric/table/card phù hợp, có link nguồn; phần quy trình collapse mặc định.** | Số liệu khớp Dashboard/Projects/Tasks; hai bề mặt không lệch số; có source/deep-link; không có `not_reached` nhưng lại báo 100%. |
| P03 | `/projects/{projectId}` → chọn đúng Project trong composer; chạy đối chiếu ở `/analytics` | Project context thắng workspace/group context; kiểm privacy | **Phân tích Project đang chọn: mục tiêu, tiến độ Sprint, task nghẽn, dependency, workload, rủi ro deadline và ba hành động ưu tiên. Chỉ dùng dữ liệu tôi được phép xem.** | Tên/số liệu đúng Project; Task/Sprint card mở đúng đối tượng; Assistant và Phân tích thống nhất; không rò nguồn private. |
| P04 | `/dashboard` → tạo **Phiên mới** | Memory thật của session, không hỏi lại dữ kiện | **Ghi nhớ rằng trong phiên này “MVP” nghĩa là ba chức năng: đăng ký, đặt lịch và thanh toán. Chưa tạo dữ liệu; chỉ xác nhận ngắn.** Sau đó dán: **Với MVP vừa nói, đề xuất thước đo thành công và các khối công việc chính. Không hỏi lại ba chức năng.** | Không hỏi lại; reload trang rồi mở lại đúng phiên vẫn hiểu MVP; chưa có mutation. |
| P05 | Cùng session P04 → nút **Phiên** | Lịch sử session thật, title và decision summary | **Đặt tiêu đề phiên này là “E2E-AI kiểm thử toàn luồng”, sau đó tóm tắt các quyết định đã thống nhất trong phiên.** | Danh sách Phiên có một session riêng; title đúng; mở lại sau reload có đủ context, không chỉ là cuộn tin nhắn cũ. |

### B. Khởi chạy Project end-to-end

| ID | Trang bắt đầu | Tác dụng cần kiểm | Prompt để dán | PASS cần nhìn thấy |
|---|---|---|---|---|
| P06 | `/dashboard` → Phiên mới → scope `Tất cả dự án` | Intent Project Launch và form unknown nhiều câu | **Khởi chạy Project `E2E-AI-SPA-Dịch-vụ` cho web SPA đặt dịch vụ theo gói. Hãy tái sử dụng dữ kiện tôi đã nói, chỉ hỏi tối đa ba unknown thực sự chặn việc lập phương án. Câu hỏi phải là form/card có option phổ biến và “Khác”, cho phép nhập tự do, lưu nhiều câu trả lời trước khi gửi.** | Đi vào Launch Brief, không trả câu pitch/fallback chung; tối đa 3 câu hỏi; gõ không tự gửi; nhiều câu trả lời còn sau reload. |
| P07 | Cùng session/card P06 | Map dữ kiện vào goal/success metrics/scope | **Thời hạn 12 tuần; người dùng chính là khách hàng cá nhân; bắt buộc có đăng ký/đăng nhập, đặt dịch vụ + phòng, thanh toán và dashboard quản lý. Mục tiêu: 95% luồng đặt dịch vụ E2E pass, p95 API dưới 500ms, không có lỗi Critical khi nghiệm thu.** | Launch Brief có deadline, persona, must-have, 3 chỉ số đo; cho sửa bằng control phù hợp; không hỏi lại dữ kiện vừa trả lời. |
| P08 | Cùng session P06–P07 → card staffing/delivery | Staffing theo scope, skill, lịch và tải thật; kế hoạch có thể tùy chỉnh | **Lập ba phương án manager/team dựa trên skill evidence, capacity đã khai báo, lịch vắng và tải đa dự án. Sau đó chia phase, Sprint, Task, dependency, estimate, required skill và assignee. Cho phép thay người, thêm/bớt người, đổi độ dài Sprint và sửa Task trước xác nhận. Không coi chỗ trống là capacity.** | Có 3 phương án kèm lý do/trade-off; team size không cố định 3; thay người/thêm-bớt/Sprint/Task được; thiếu capacity thì chặn và hướng dẫn, không dùng mặc định 40h. |
| P09 | Cùng session → card review cuối | Một xác nhận tạo Project graph canonical thật | **Dùng phương án đang chọn. Trước khi ghi hãy hiện một card review cuối gồm Project, manager/team, phase, Sprint và tổng số Task. Chờ đúng một xác nhận của tôi.** | Trước click chưa có row mới. Sau một click: receipt succeeded + `readBackVerified=true` + deep-link; Project/member/Sprint/Task/dependency/skill requirement tồn tại sau reload. |
| P10 | Mở deep-link Project vừa tạo; quay lại cùng session/receipt | Idempotency, không tạo trùng khi retry/reload | **Mở lại kết quả thực thi Project vừa rồi và kiểm tra xem retry cùng yêu cầu có tạo trùng Project, Sprint hoặc Task không. Chỉ báo theo dữ liệu đọc lại.** | Cùng execution/idempotency key trả cùng receipt; số Project/Sprint/Task không tăng; không cần spam click mới thấy kết quả. |

### C. Task creation, breakdown và phân công

| ID | Trang bắt đầu | Tác dụng cần kiểm | Prompt để dán | PASS cần nhìn thấy |
|---|---|---|---|---|
| P11 | `/projects/{projectId}?tab=tasks` → Trợ lý AI, scope đúng Project | Tạo đúng số lượng Task, lập hạn theo Sprint và đề xuất người bằng dữ liệu thật | **Trong Project đang chọn, soạn đúng 10 Task cho Sprint 1: khảo sát, user flow, UI kit, API contract, database, auth, booking, payment, test E2E và tài liệu vận hành. Mỗi Task có mô tả, acceptance criteria, estimate, dependency, priority và required skill. Tự đặt hạn trong Sprint theo dependency; chỉ đề xuất người khi có đủ skill evidence, declared availability/capacity và tải đa Project, nếu thiếu thì để chưa giao và nói rõ. Mở bản nháp để tôi chỉnh; chưa ghi dữ liệu.** | Draft có đúng 10 card, không rút còn 3; hạn nằm trong Sprint và không sớm hơn dependency; assignee `system_suggested` có evidence/capacity thật hoặc để chưa giao trung thực; mỗi card sửa/chọn được; một lần confirm tạo đúng số được tick; reload còn đủ trường. |
| P12 | `/projects/{projectId}/tasks/{taskId}` với D02 | Route đúng intent giao việc, không nhảy sang Project Launch | **Với Task đang mở, hãy lập phương án giao việc và lịch. Đối chiếu required skill, evidence đã xác nhận, capacity thật, availability, deadline và tải ở tất cả Project; cho tôi đổi ứng viên hoặc ngày trước khi xác nhận.** | Intent/capability là `task.assignment_schedule.v1`; card gắn đúng Task; có candidates/reason/schedule; đổi người/ngày được; không mở tạo Project/Task mới. |
| P13 | Cùng Task và draft P12 | Xác nhận assignment canonical, idempotent | **Giữ phương án hiện tại và cho tôi card xác nhận cuối. Không ghi trước khi tôi bấm xác nhận.** | Sau một click có receipt succeeded/read-back; đúng một assignee/TaskAssignment; estimate, label, Sprint, actual hours không mất; retry không tạo lặp. |
| P14 | Task detail D02; chọn ứng viên đang vắng/thiếu capacity | Business constraint chống overbooking | **Thử đề xuất giao Task này cho một người đang không sẵn sàng hoặc không đủ capacity trong cửa sổ hiện tại; giải thích ngắn phương án thay thế.** | Không cho confirm sai; không coi phút trống là capacity; chỉ ra blocker và ứng viên/lịch thay thế; DB không đổi. |
| P15 | Task detail → tạo draft; mở Task ở tab thứ hai và sửa deadline/rowVersion trước confirm | Stale-source/concurrency, không ghi dở dang | **Lập bản nháp giao Task, nhưng trước khi xác nhận hãy thay đổi Task ở màn hình khác; sau đó xác nhận bản nháp cũ.** | Confirm draft cũ trả conflict/stale-source, yêu cầu refresh/replan; không tạo assignment nửa chừng; draft mới dùng rowVersion mới. |
| P16 | Task detail | Acceptance checklist canonical | **Với Task đang mở, soạn 5 mục acceptance checklist kiểm chứng được, cho phép sửa từng mục và chờ một xác nhận trước khi lưu.** | Card có đúng 5 mục sửa được; trước confirm chưa ghi; sau confirm có 5 canonical rows và receipt/read-back; reload còn đủ. |
| P17 | Task detail | Breakdown subtask/dependency có cấu trúc | **Tách Task đang mở thành 4 subtask theo thứ tự thực hiện, có dependency, estimate và required skill; mở card review trước khi tạo.** | Đúng 4 subtask; parent/dependency/estimate/skill đúng; cho sửa/tick; sau một confirm và reload dữ liệu còn nguyên, không trùng. |

### D. Các AI surface native khác

| ID | Trang bắt đầu | Tác dụng cần kiểm | Prompt để dán | PASS cần nhìn thấy |
|---|---|---|---|---|
| P18 | `/projects/{projectId}/wiki/{wikiId}` | Wiki brief có source và tạo Task có chọn lọc | **Tóm tắt Wiki đang mở thành brief có link tới section nguồn; đề xuất tối đa 3 Task tùy chọn. Chỉ tạo các Task tôi tick chọn sau một xác nhận.** | Summary trỏ đúng section/revision; tối đa 3 Task có checkbox; chỉ Task được tick được tạo; deep-link và reload đọc lại được. |
| P19 | `/groups/{groupId}/polls` | Poll draft có cấu trúc, sửa trước confirm | **Trong Group đang mở, soạn Poll chọn phương án triển khai với 4 option rõ ràng, deadline 3 ngày và cho sửa trước khi xác nhận.** | Card Poll có title/4 option/deadline, sửa được; không ghi trước confirm; sau confirm Poll/options canonical tồn tại sau reload. |
| P20 | `/groups/{groupId}/meeting` với transcript D04 | Meeting extraction + map action có kiểm soát | **Từ transcript cuộc họp đang mở, trích quyết định, blocker và action item. Không tạo Task tự động; cho phép map từng action item sang Task có sẵn hoặc bản nháp Task mới.** | Trích đúng nguồn; không tự tạo Task; từng action item chọn map existing/new/ignore; chỉ lựa chọn đã confirm được ghi. |
| P21 | `/projects/{projectId}?tab=roadmap` | Roadmap/Sprint proposal, before/after, không ghi ngầm | **Đánh giá roadmap Project hiện tại và đề xuất điều chỉnh Sprint theo dependency, capacity và deadline. Hiện before/after và không ghi trước xác nhận.** | Có before/after và lý do; sửa/tắt từng thay đổi được; trước confirm canonical roadmap không đổi; sau confirm chỉ ghi phần đã chọn và reload khớp. |
| P22 | `/projects/{projectId}` → Trợ lý AI scope đúng Project | Weekly digest preference và timezone | **Cấu hình weekly digest cho Project này vào 09:00 thứ Hai theo timezone của tổ chức; hiện card review và đọc lại subscription sau xác nhận.** | Review card ghi rõ Project/time/timezone/channel; sau confirm subscription/revision đọc lại đúng; retry không nhân đôi. |
| P23 | `/projects/{projectId}/tasks/{doneTaskId}` | Skill evidence chỉ từ provenance hợp lệ | **Với Task vừa hoàn tất, đề xuất attribution và skill evidence theo tiêu chí nghiệm thu đã xác nhận. Không dùng label hoặc tin nhắn riêng làm bằng chứng.** | Chỉ đề xuất khi Task Done/đủ quyền; source là acceptance/evidence đã xác nhận; không lấy label/private chat; confirm tạo evidence canonical có provenance. |
| P24 | `/projects/{projectId}?tab=roadmap` hoặc tab Thống kê | Drift detection và replan an toàn | **So sánh Project hiện tại với baseline đã xác nhận, chỉ ra drift về scope, lịch, staffing và task; đề xuất replan có before/after nhưng chưa mutation.** | Drift bám dữ liệu thật; before/after rõ; không mutation trước confirm; source đổi thì proposal cũ bị stale; thao tác đã confirm có receipt/read-back. |

### E. Quyền, fallback, renderer và adapter ngoài

| ID | Trang bắt đầu | Tác dụng cần kiểm | Prompt để dán | PASS cần nhìn thấy |
|---|---|---|---|---|
| P25 | Đăng nhập Member D05 → `/projects/{projectId}` | Read-only vẫn hữu ích, không lộ mutation capability | **Tóm tắt Project và tài liệu tôi được phép xem, sau đó đề xuất ba việc nên làm. Nếu tôi không có quyền tạo/giao Task, vẫn trả phân tích hữu ích và nút mở dữ liệu nguồn; không hiện nút xác nhận mutation.** | Có phân tích/source/navigation; không có nút confirm/create/assign; gọi trực tiếp endpoint mutation cũng bị chặn; không lộ dữ liệu ngoài quyền. |
| P26 | Admin → Project detail; bật fault D06 rồi gửi prompt | Provider failure không tạo dead-end/false success | **Phân tích Project hiện tại và đề xuất bước tiếp theo. Nếu model lỗi, dùng fallback server có ích, ghi actual provider/model và tiếp tục; không báo thành công cho thao tác chưa ghi.** | Có fallback hữu ích; hiện actual provider/model; không hỏi key sai provider; không báo mutation thành công; request/session còn retry được. Không có fault injection thì `NOT_VERIFIED`. |
| P27 | `/dashboard` và chạy đối chiếu tại `/analytics` cùng filter | Renderer có card/table/metric và navigation đúng | **Liệt kê ba Task quá hạn, workload ba người cao nhất và Sprint có nguy cơ. Dùng metric/table/task cards phù hợp; mỗi item có nút mở đúng đối tượng, không trả một khối text dài.** | Có đúng renderer theo loại dữ liệu; số liệu hai bề mặt khớp; mỗi nút mở đúng Task/member/Sprint; process mặc định collapse. |
| P28 | `/projects/{projectId}` → Trợ lý AI | Tính trung thực của external adapters | **Kiểm tra khả năng đồng bộ calendar, repository, invitation, webhook và deployment cho Project này. Chỉ đánh dấu hoàn thành nếu adapter thật đã đọc/ghi và read-back; phần chưa có phải ghi EXTERNAL_DEFERRED.** | Adapter có thật phải có read/write/read-back evidence; adapter chưa có ghi `EXTERNAL_DEFERRED`, không giả receipt/success. |

## 3. Các chuỗi bắt buộc phải giữ cùng ngữ cảnh

| Chuỗi | Cách chạy | Lý do |
|---|---|---|
| P04 → P05 | Một session, reload giữa các bước rồi mở lại từ **Phiên** | Chứng minh memory/session history persisted |
| P06 → P10 | Một session Project Launch; không đổi scope giữa chừng | Chứng minh unknown → brief → staffing → execution → idempotency là một flow |
| P11 | Project đã chọn rõ trước khi gửi | Tránh router nhầm Task request thành Project Launch |
| P12 → P15 | Cùng Task; P15 dùng tab thứ hai để đổi source | Chứng minh assignment, capacity và stale-source dùng cùng business rule |
| P16 → P17 | Cùng Task hoặc hai Task test riêng, ghi lại ID | Chứng minh checklist/breakdown tạo đúng canonical rows |

## 4. Phiếu ghi kết quả

| ID | Kết quả `PASS/FAIL/BLOCKED/NOT_VERIFIED` | URL/session | Receipt/deep-link/read-back | Sai số UI/nghiệp vụ | Bug/CAND liên quan |
|---|---|---|---|---|---|
| P__ |  |  |  |  |  |

## 5. Cổng kết luận

Chỉ đánh `PASS` cho từng dòng khi:

1. Router/capability đúng intent và đúng context.
2. Dữ liệu nguồn được authorize và renderer phù hợp.
3. Interaction chỉnh sửa/chọn/xác nhận hoạt động; không dead-end.
4. Mutation chỉ xảy ra sau đúng một xác nhận.
5. Có canonical read-back sau reload; retry không tạo trùng.
6. Với lỗi provider/stale/capacity, hệ thống không ghi dở dang và đưa ra bước tiếp tục hữu ích.

Trạng thái toàn runbook chỉ là `ACCEPTED` khi P01–P27 PASS và P28 PASS hoặc `EXTERNAL_DEFERRED` có bằng chứng trung thực. Không suy rộng kết quả unit/integration thành PASS cho UI thủ công chưa chạy.

## 6. Phiếu nghiệm thu cuối — 2026-08-22

Kết quả này dùng exact prompt/public API fixture cho từng dòng, canonical read-back cho mutation và final targeted browser replay cho session reload, multi-answer, Project scope, editable cards, permission gate, idempotent confirm, receipt và navigation. Không dùng toast hoặc text thành công làm bằng chứng độc lập.

| ID | Kết quả | Bằng chứng chính |
|---|---|---|
| P01–P03 | `PASS` | Role cards, workspace/project scope, Assistant/Analytics canonical source and navigation fixtures; targeted capability/navigation replay. |
| P04–P05 | `PASS` | Durable memory/history/scope Integration; real session switch and reload browser replay. |
| P06–P10 | `PASS` | Multi-answer reload; reviewed staffing/delivery card; one-confirm canonical graph; lost-response reconciliation; stable receipt/read-back. |
| P11–P17 | `PASS` | Exact Task count, assignment/capacity/stale gates, checklist/subtask canonical Integration; exact-count editable browser replay. |
| P18–P24 | `PASS` | Native domain typed-artifact and canonical mutation/read-back Integration; review-only replan browser replay. |
| P25–P27 | `PASS` | Member read-only, deterministic provider fallback and typed renderer/navigation public-flow fixtures. |
| P28 | `EXTERNAL_DEFERRED_VERIFIED` | Five server-owned adapter states; no model mutation and no simulated write/read-back receipt; receipt UI shows `external_deferred`. |

**Runbook disposition:** `ACCEPTED / PRODUCT_ACCEPTED`. External calendar/repository/invitation/webhook/deployment remain outside the implemented internal boundary until a credentialed adapter proves write and provider read-back.

## 7. Regression gate P06–P28 — 2026-08-24

Một lỗi runtime thật đã được tái hiện sau phiếu nghiệm thu 2026-08-22: bước xác nhận Project trả `409` trên SQL Server vì transaction được mở ngoài `SqlServerRetryingExecutionStrategy`. Vì evidence runtime mới luôn thắng trạng thái lịch sử, disposition hiện tại được cập nhật thành:

`AUTOMATED_GATE_P06_P28_PASS / TARGETED_MANUAL_REPLAY_PENDING`

Các hiệu chỉnh trong gate này:

- P06–P08: một form Launch Brief duy nhất; câu trả lời clarification đã lưu được hydrate vào form chính, không render thêm khối “Mình cần biết thêm” trùng lặp.
- Danh sách **Qaly đề cử** lấy từ scope/features đã phân tích trong prompt/Launch Brief. Catalog dài là mẫu seed có mô tả để bổ sung, không tự biến thành yêu cầu Project. Có thao tác nhanh giữ đề cử, chọn tất cả mẫu và bỏ chọn tất cả.
- P08: blocker card dẫn tới đúng control nhân sự/Sprint cần sửa; đội hình, backlog chưa giao, assignee/reviewer và phương án phân công vẫn là dữ liệu reviewable trước khi lưu.
- P09–P10: confirm/rollback transaction chạy bên trong SQL execution strategy; SQL integration chứng minh một confirm tạo canonical graph, retry trả cùng receipt và không tạo trùng.
- P26: provider failure luôn có structured `Answer` dự phòng hữu ích và actual provider/model là `Qaly / qaly-native`, không để metadata DeepSeek giả khi DeepSeek không trả lời.
- Session read query dùng split query để không tạo cảnh báo/lượng join nhiều collection không cần thiết.

Evidence tự động hiện tại:

| Phạm vi | Kết quả | Bằng chứng |
|---|---|---|
| P06–P28 internal capabilities | `PASS_AUTOMATED` | 69/69 focused Integration PASS cho Assistant, Action Composer, Native Domain Actions, Portfolio Schedule và Task Skill. Test session cancel/resume `TEST-RO-LOOP-02` thuộc P04–P05 không nằm trong slice này. |
| P09–P10 SQL retry/transaction | `PASS_AUTOMATED` | `TEST-AI-P09-P10-SQL-RETRY-TRANSACTION-01`: 1/1 PASS trên SQL Server migration schema, có canonical read-back và idempotency. |
| Frontend contract | `PASS_AUTOMATED` | Vue typecheck PASS; solution build không warning/error. |
| P28 | `EXTERNAL_DEFERRED_VERIFIED` | Không có credentialed adapter write/read-back mới; không giả lập thành công. |
| UI P06–P28 trên preview mới | `NOT_VERIFIED` | Cần người dùng chạy lại targeted prompt/card sau khi preview được restart; không suy rộng Integration thành manual PASS. |

Không trả lại `PRODUCT_ACCEPTED` chỉ dựa vào bảng này. Cần targeted manual replay tối thiểu P06 → P10 trên preview mới, sau đó tiếp tục P11 → P28 theo các prompt ở mục 2.
