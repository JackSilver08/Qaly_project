# Kịch bản demo nghiệm thu QALY - 09/06/2026

## 1. Nguyên tắc demo

- Chỉ demo các luồng có bằng chứng pass hoặc mô tả rõ là fallback/limited/planned.
- Không demo participant realtime/count/list của cuộc họp như tính năng ổn định nếu `DH03-BUG-MTG-001` chưa fix.
- Không demo screen share positive nếu chưa có browser thật/headful và bằng chứng mới.
- Không nói “deploy hoàn chỉnh”, “AI đầy đủ”, “RAG thật đầy đủ”, “import mọi định dạng” khi chưa có bằng chứng tương ứng.
- Không hiển thị secret/password/token/cookie.

## 2. Thông điệp mở đầu

QALY hiện có nền tảng quản lý dự án, nhóm, nhập tài liệu, phân quyền và kiểm thử regression. Tuần 03/06/2026 - 09/06/2026 đã có:

- Playwright smoke pass 7/7.
- Manual QA pass 5, fail 3, blocked 0.
- Backend build pass, integration pass 14/14, unit pass 92/92 theo filter DH-04.
- Import document đủ điều kiện demo theo scope hiện tại.
- Meeting còn bug P0 ở participant realtime/count nên không claim realtime participant ổn định.

## 3. Chuẩn bị trước demo

| Hạng mục | Trạng thái yêu cầu |
|---|---|
| App server | Chạy đúng base URL demo |
| Tài khoản demo | Có admin/member/outside user; không ghi mật khẩu vào script |
| Dự án demo | Có dự án dùng cho nhập tài liệu |
| Nhóm demo | Có nhóm với owner/admin/member |
| File demo | DOCX nhỏ, ZIP nhỏ chứa `.md/.txt/.html`; PDF chỉ dùng để demo unsupported |
| AI provider | Ghi rõ dùng provider thật hay phản hồi dự phòng AI |

## 4. Luồng demo đề xuất

### 4.1 Login và dashboard cơ bản

Thời lượng: 1 phút.

1. Mở app.
2. Đăng nhập bằng tài khoản demo.
3. Mở dashboard hoặc trang dự án.

Nói rõ: login và smoke UI đã có bằng chứng E2E pass.

### 4.2 Nhập tài liệu vào Wiki

Thời lượng: 3 phút.

1. Mở dự án demo.
2. Mở modal nhập tài liệu.
3. Upload DOCX.
4. Xem preview và warning nếu có.
5. Confirm import.
6. Mở Wiki page được tạo.
7. Upload ZIP fixture nhỏ nếu còn thời gian.
8. Với PDF, chỉ demo thông báo unsupported/roadmap.

Nói rõ:

- DOCX/ZIP pass theo evidence DH-03.
- ZIP phase 1 hỗ trợ file con `.md`, `.txt`, `.html`, `.docx`; unsupported được skip có warning.
- PDF chưa claim hỗ trợ đầy đủ.

Evidence:

- `docs/task/qa-evidence/meeting-import-qa-2026-06-03.md`
- `docs/task/evidence/2026-06-03_2026-06-09/import-document/`

### 4.3 Nhóm, chat và bình chọn

Thời lượng: 2 phút.

1. Mở trang nhóm.
2. Tạo hoặc mở nhóm demo.
3. Gửi tin nhắn.
4. Tạo/vote bình chọn.

Nói rõ: E2E smoke đã pass các flow tạo nhóm, chat realtime và poll vote. Nếu một hiệu ứng thời gian thực nâng cao chưa có evidence riêng, không claim quá phạm vi smoke.

### 4.4 Phân quyền

Thời lượng: 1 phút.

1. Minh họa member/outside user bị chặn ở luồng có quyền.
2. Nếu demo meeting, chỉ minh họa outside user bị chặn hoặc backend permission.

Nói rõ: backend regression DH-04 đã cover auth boundary, outside user, admin-only/member boundary.

### 4.5 Cuộc họp nhóm

Thời lượng: 1 phút, chỉ demo nếu cần.

Có thể demo:

- Start meeting.
- Member join.
- Owner/admin end meeting.
- Outside user bị chặn.

Không demo như ổn định:

- Participant realtime/count/list.
- Screen share positive.

Lý do: `DH03-BUG-MTG-001` P0 còn mở; `DH03-BUG-MTG-002` P2 còn mở.

### 4.6 AI analytics / Group AI

Thời lượng: 1-2 phút, tùy cấu hình.

Nếu provider thật đã cấu hình:

1. Mở analytics hoặc Group AI.
2. Gửi prompt demo ngắn.
3. Quan sát kết quả.

Nếu không có provider:

1. Nói rõ hệ thống chạy phản hồi dự phòng AI.
2. Không claim chất lượng RAG/tooling đầy đủ.

Nói rõ: backend regression đã cover schema/fallback; manual deep check AI analytics và Group AI E2E chưa có bằng chứng đầy đủ.

## 5. Luồng không nên demo nếu chưa fix/xác minh

| Luồng | Lý do |
|---|---|
| Participant realtime/count/list trong cuộc họp | Bug P0 `DH03-BUG-MTG-001` |
| Screen share positive | Chưa có bằng chứng headful/browser thật; UI feedback unsupported còn P2 |
| Deploy production/full Docker acceptance | Chưa có bằng chứng nghiệm thu đầy đủ |
| Group AI full E2E | Chưa có bằng chứng manual/E2E đầy đủ |
| AI analytics deep/manual claims | Chưa có bằng chứng manual deep check đầy đủ |
| PDF import positive | PDF hiện là roadmap/unsupported theo evidence hiện tại |

## 6. Kết luận dùng khi kết thúc demo

QALY có thể demo ổn định ở scope nhập tài liệu, phân quyền cơ bản, smoke nhóm/bình chọn và backend regression. Cuộc họp nhóm cần fix participant realtime/count trước khi nghiệm thu nếu phần realtime participant là yêu cầu demo. AI và deploy cần được mô tả theo trạng thái provider/fallback và bằng chứng hiện có.

