# Hướng dẫn sử dụng QALY theo scope nghiệm thu hiện tại

## 1. Mục đích

Tài liệu này hỗ trợ demo/nghiệm thu bằng tiếng Việt, bám theo bằng chứng QA tuần 03/06/2026 - 09/06/2026. Không dùng tài liệu này để khẳng định các chức năng chưa có bằng chứng đầy đủ.

Nguồn liên quan:

- `docs/task/test-plan-tuan-2026-06-03.md`
- `docs/task/qa-evidence/meeting-import-qa-2026-06-03.md`
- `docs/task/Bao_cao_kiem_thu_tuan_2026-06-03.md`
- `docs/task/checklist-nghiem-thu-2026-06-09.csv`

## 2. Thuật ngữ thống nhất

| Thuật ngữ code/Anh | Thuật ngữ tiếng Việt dùng khi demo |
|---|---|
| group | nhóm |
| project | dự án |
| task | công việc |
| poll | bình chọn |
| meeting | cuộc họp |
| import | nhập tài liệu |
| permission | phân quyền |
| evidence | bằng chứng kiểm thử |
| realtime | thời gian thực |
| AI fallback | phản hồi dự phòng AI |

## 3. Luồng có thể demo theo bằng chứng hiện tại

| Luồng | Trạng thái | Cách mô tả khi demo | Bằng chứng |
|---|---|---|---|
| Login và phân quyền cơ bản | Có thể demo | Đăng nhập, phân biệt quyền admin/member/outside user ở các luồng đã kiểm thử | E2E smoke DH-02, backend regression DH-04 |
| Nhập tài liệu DOCX | Có thể demo | DOCX được preview và tạo Wiki page; parser có thể bỏ qua bảng/ảnh và hiển thị warning | DH-03 manual QA pass |
| Nhập ZIP phase 1 | Có thể demo | ZIP hỗ trợ `.md`, `.txt`, `.html`, `.docx`; file unsupported trong ZIP được bỏ qua có cảnh báo | DH-03 manual QA pass |
| PDF import | Demo nhánh unsupported/roadmap | PDF hiện chưa có parser; UI/API báo unsupported rõ ràng, không tạo dữ liệu rác | DH-03 manual QA pass |
| Bình chọn nhóm | Có smoke E2E | Có thể demo ở mức luồng smoke đã pass; cần tránh claim mọi tình huống realtime nâng cao nếu chưa có evidence riêng | DH-02 E2E pass |
| Backend regression | Có thể dùng làm bằng chứng kỹ thuật | Build pass, integration/unit filter pass cho Import/Meeting/Auth/AI | DH-04 |

## 4. Giới hạn hiện tại phải nói rõ

| Khu vực | Giới hạn | Mức ảnh hưởng |
|---|---|---|
| Cuộc họp nhóm | Participant realtime/count không cập nhật khi member join meeting (`DH03-BUG-MTG-001`) | P0 nếu demo participant count/list |
| Chia sẻ màn hình | Browser unsupported chỉ log console, UI feedback chưa rõ (`DH03-BUG-MTG-002`) | P2; cần browser thật/headful để xác minh positive case |
| Deploy config | Chưa có bằng chứng nghiệm thu đầy đủ cho checklist deploy tuần này | Không claim deploy hoàn chỉnh |
| AI analytics | E2E smoke và backend fallback/schema có bằng chứng; manual deep check chưa đầy đủ | Không claim AI analytics đã nghiệm thu toàn diện |
| Group AI | Có unit/backend liên quan nhưng chưa có manual/E2E đầy đủ | Không claim Group AI đã nghiệm thu đầy đủ |
| AI provider/RAG | Phụ thuộc cấu hình OpenAI/Gemini/Ollama/Qdrant hoặc fallback/mock | Cần ghi rõ provider/fallback khi demo |

## 5. Hướng dẫn thao tác demo ngắn

### 5.1 Đăng nhập

1. Mở app tại base URL demo.
2. Đăng nhập bằng tài khoản seed được nhóm cung cấp.
3. Kiểm tra vào được dashboard hoặc menu user.

Không ghi mật khẩu/token/cookie vào tài liệu hoặc ảnh chụp.

### 5.2 Nhập tài liệu

1. Mở một dự án demo.
2. Mở modal nhập tài liệu.
3. Upload DOCX hoặc ZIP fixture nhỏ.
4. Kiểm tra preview, warning và kết quả tạo Wiki page.
5. Với PDF, chỉ demo trạng thái roadmap/unsupported nếu chưa có parser.

Ảnh minh họa hiện có:

- `docs/task/evidence/2026-06-03_2026-06-09/import-document/20260604_duyhoang_import_DH01-IMP-001_docx-preview.png`
- `docs/task/evidence/2026-06-03_2026-06-09/import-document/20260604_duyhoang_import_DH01-IMP-002_zip-success.png`
- `docs/task/evidence/2026-06-03_2026-06-09/import-document/20260604_duyhoang_import_DH01-IMP-003_pdf-unsupported-ui.png`

### 5.3 Nhóm, chat và bình chọn

1. Mở trang nhóm.
2. Tạo hoặc mở nhóm demo.
3. Gửi tin nhắn.
4. Tạo/vote bình chọn nếu dữ liệu demo sẵn sàng.

Nếu không có bằng chứng realtime nâng cao cho một hành vi cụ thể, chỉ mô tả ở mức smoke đã pass.

### 5.4 Cuộc họp nhóm

Có thể trình bày:

- thành viên trong nhóm được join;
- outside user bị chặn;
- owner/admin có thể kết thúc cuộc họp theo rule backend.

Không nên trình bày participant realtime/count/list là tính năng ổn định khi bug P0 chưa fix.

Ảnh bằng chứng lỗi hiện có:

- `docs/task/evidence/2026-06-03_2026-06-09/meeting/20260604_duyhoang_meeting_DH01-MTG-004_participant-after-member-join.png`
- `docs/task/evidence/2026-06-03_2026-06-09/meeting/20260604_duyhoang_meeting_DH01-MTG-005_screen-share-denied-headless.png`

### 5.5 AI analytics và Group AI

Nếu demo:

1. Nói rõ provider đang dùng hoặc đang chạy phản hồi dự phòng AI.
2. Chỉ demo câu hỏi/output đã chuẩn bị dữ liệu.
3. Không khẳng định RAG/tool calling đầy đủ nếu chưa có bằng chứng riêng.
4. Nếu output lỗi/schema bất thường, dùng kết quả backend regression để giải thích hệ thống có fallback controlled.

## 6. Ảnh minh họa cần bổ sung

| Ảnh | Trạng thái |
|---|---|
| Dashboard/analytics page trong trạng thái demo | Cần bổ sung ảnh nếu đưa vào user guide chính thức |
| Group AI panel với provider thật hoặc fallback | Cần bổ sung ảnh nếu demo Group AI |
| Browser headful screen share positive | Cần bổ sung ảnh sau khi xác minh |
| Deploy config/log Docker nghiệm thu | Cần bổ sung nếu chạy checklist deploy |

