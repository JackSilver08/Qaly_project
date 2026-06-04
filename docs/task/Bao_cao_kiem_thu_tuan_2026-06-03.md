# Báo cáo kiểm thử tuần 03/06/2026 - 09/06/2026

## 1. Thông tin chung

| Hạng mục | Nội dung |
|---|---|
| Tuần kiểm thử | 03/06/2026 - 09/06/2026 |
| Người phụ trách | Duy Hoàng |
| Phạm vi | Meeting, Import document, Deploy config, AI analytics, Group AI, Auth/permission, Regression core |
| Nguồn tổng hợp | DH-01 test plan, DH-02 Playwright E2E result, DH-03 manual QA evidence, DH-04 backend regression result |
| Nguyên tắc ghi nhận | Chỉ ghi pass/fail theo bằng chứng đã có; phần thiếu evidence được ghi rõ “chưa có bằng chứng” |
| Ghi chú | Không đưa secret/password/token vào báo cáo |

## 2. Tóm tắt kết quả

| Nguồn | Nội dung | Kết quả | Evidence/file liên quan |
|---|---|---:|---|
| DH-01 | Test plan tổng tuần | 37 test case: P0 = 17, P1 = 13, P2 = 7 | `docs/task/test-plan-tuan-2026-06-03.md` |
| DH-02 | Playwright E2E smoke | Pass 7/7 flow | `tests/e2e/qaly.smoke.spec.ts`, `tests/e2e/fixtures/wiki-smoke.md`, `playwright-report/`, `test-results/` |
| DH-02 | Frontend build | Pass, theo kết quả DH-02 đã báo cáo | Chưa có log build riêng trong `docs/task/evidence` |
| DH-03 | Manual QA Meeting + Import document | Pass 5, Fail 3, Blocked 0, Not Tested 0 | `docs/task/qa-evidence/meeting-import-qa-2026-06-03.md` |
| DH-04 | Backend build | Pass, 0 warning, 0 error | Console output DH-04; chưa có file log riêng trong `docs/task/evidence` |
| DH-04 | Integration tests filter | Pass 14/14 | `tests/Qaly.IntegrationTests/` |
| DH-04 | Unit tests filter | Pass 92/92 | `tests/Qaly.UnitTests/` |

### 2.1 Tổng số liệu thực thi có bằng chứng

Không cộng 37 test case trong DH-01 vào pass/fail vì DH-01 là test plan, không phải test execution.

| Nhóm thực thi | Pass | Fail | Blocked | Not Tested | Ghi chú |
|---|---:|---:|---:|---:|---|
| E2E smoke DH-02 | 7 | 0 | 0 | 0 | 7 flow Playwright |
| Manual QA DH-03 | 5 | 3 | 0 | 0 | 8 case Meeting/Import |
| Backend integration DH-04 | 14 | 0 | 0 | 0 | Filter Import/Meeting/Auth/AI |
| Backend unit DH-04 | 92 | 0 | 0 | 0 | Filter Import/Meeting/Auth/AI/Schema/Fallback |
| Tổng thực thi | 118 | 3 | 0 | 0 | Chỉ tính kết quả đã chạy và đã báo cáo |

## 3. Bảng tổng hợp theo module

| Module | Loại test | Số case/flow | Pass | Fail | Blocked | Not Tested | Evidence/file liên quan |
|---|---|---:|---:|---:|---:|---:|---|
| Meeting | Manual QA | 4 | 1 | 3 | 0 | 0 | `docs/task/qa-evidence/meeting-import-qa-2026-06-03.md` |
| Meeting | E2E smoke | 1 flow | 1 | 0 | 0 | 0 | `tests/e2e/qaly.smoke.spec.ts`, `playwright-report/` |
| Meeting | Backend permission/regression | 5 test mới | 5 | 0 | 0 | 0 | `tests/Qaly.UnitTests/GroupsServiceTests.cs` |
| Meeting | SignalR participant end-to-end | Chưa tách case automation riêng | 0 | 0 | 0 | 1 | Chưa có bằng chứng E2E SignalR participant count ổn định; manual đang fail |
| Import document | Manual QA | 4 | 4 | 0 | 0 | 0 | `docs/task/qa-evidence/meeting-import-qa-2026-06-03.md` |
| Import document | E2E smoke | 1 flow | 1 | 0 | 0 | 0 | `tests/e2e/qaly.smoke.spec.ts`, `tests/e2e/fixtures/wiki-smoke.md` |
| Import document | Backend regression | 5 test mới | 5 | 0 | 0 | 0 | `tests/Qaly.UnitTests/ImportEnhancementTests.cs` |
| Deploy config | Manual/config review theo DH-01 | 5 planned | 0 | 0 | 0 | 5 | Chưa có bằng chứng chạy riêng cho `DH01-DEP-*`; DH-02 build pass không thay thế full deploy config test |
| AI analytics | E2E smoke | 1 flow | 1 | 0 | 0 | 0 | `tests/e2e/qaly.smoke.spec.ts` |
| AI analytics | Backend fallback/schema | 2 test mới | 2 | 0 | 0 | 0 | `tests/Qaly.UnitTests/ErumiChatServiceTests.cs` |
| AI analytics | Manual analytics/AI deep check theo DH-01 | 5 planned | 0 | 0 | 0 | 5 | Chưa có bằng chứng manual riêng cho toàn bộ `DH01-AIAN-*` |
| Group AI | Unit/backend hiện hữu trong filter DH-04 | Có test chạy trong unit filter | Pass trong tổng 92/92 | 0 | 0 | Không tách số riêng | `tests/Qaly.UnitTests/GroupAiServiceTests.cs`; chưa có evidence manual/E2E riêng theo `DH01-GAI-*` |
| Auth/permission | E2E login smoke | 1 flow | 1 | 0 | 0 | 0 | `tests/e2e/qaly.smoke.spec.ts` |
| Auth/permission | Backend auth boundary | 4 test mới | 4 | 0 | 0 | 0 | `tests/Qaly.IntegrationTests/AuthBoundaryIntegrationTests.cs` |
| Auth/permission | Manual auth deep check theo DH-01 | 6 planned | 0 | 0 | 0 | 6 | Chưa có bằng chứng manual riêng cho toàn bộ `DH01-AUTH-*` |
| Regression core | Frontend build | 1 command | 1 | 0 | 0 | 0 | Kết quả DH-02: `npm run build` pass; chưa có log file riêng |
| Regression core | Playwright smoke | 7 flow | 7 | 0 | 0 | 0 | `tests/e2e/qaly.smoke.spec.ts`, `playwright-report/`, `test-results/` |
| Regression core | Backend build/tests | 1 build + 106 tests | 107 | 0 | 0 | 0 | Kết quả DH-04 build + integration/unit filters |
| Regression core | Manual CRUD/audit/polish theo DH-01 | 3 planned | 0 | 0 | 0 | 3 | Chưa có bằng chứng riêng cho CRUD core, audit/log, UI polish responsive |

## 4. Danh sách lỗi còn mở

| Bug ID | Module | Severity | Trạng thái | Mô tả ngắn | Evidence | Assignee đề xuất |
|---|---|---|---|---|---|---|
| DH03-BUG-MTG-001 | Meeting | P0 | Open | Participant realtime/count không cập nhật khi member join meeting; API start/join/end OK nhưng UI admin/member vẫn giữ count `1` | `docs/task/evidence/2026-06-03_2026-06-09/meeting/20260604_duyhoang_meeting_DH01-MTG-004_participant-after-member-join.png`, `docs/task/evidence/2026-06-03_2026-06-09/meeting/20260604_duyhoang_meeting_DH03_manual-qa-api-log.json` | Gia Long |
| DH03-BUG-MTG-002 | Meeting | P2 | Open | Screen share unsupported chỉ log console `NotSupportedError`, chưa có UI feedback rõ; UI không crash | `docs/task/evidence/2026-06-03_2026-06-09/meeting/20260604_duyhoang_meeting_DH01-MTG-005_screen-share-denied-headless.png`, `docs/task/evidence/2026-06-03_2026-06-09/meeting/20260604_duyhoang_meeting_DH03_manual-qa-api-log.json` | Gia Long |

Không thấy bug P1 riêng trong evidence log DH-03. Case `DH01-MTG-004` fail P1 có cùng nguyên nhân gần nhất với bug P0 `DH03-BUG-MTG-001`.

## 5. Phân tích rủi ro nghiệm thu

| Rủi ro | Mức | Ảnh hưởng | Bằng chứng hiện có | Nhận định |
|---|---|---|---|---|
| Meeting participant realtime/count không cập nhật | P0 | Chặn nếu demo meeting realtime participant list/count như tính năng ổn định | DH-03 manual fail 2 tab/context | Cần fix trước demo nếu phần meeting realtime nằm trong kịch bản nghiệm thu |
| SignalR participant end-to-end chưa có automation ổn định | P1/P0 tùy scope demo | Có thể tái lỗi sau fix nếu không có E2E/integration realtime coverage | Chưa có bằng chứng automation SignalR participant count | Nên bổ sung sau khi fix bug P0; hiện backend permission đã có coverage |
| Screen share positive permission chưa xác minh | P2 | Nếu demo screen share thật có thể thiếu confidence | DH-03 chỉ chạy headless unsupported/deny path | Cần browser thật/headful để xác minh positive case |
| Deploy config chưa có evidence riêng | P1 | Có thể phát sinh lỗi môi trường khi nghiệm thu/deploy | Chưa có bằng chứng cho `DH01-DEP-*` | Nên chạy checklist deploy config trước nghiệm thu nếu demo phụ thuộc Docker/infra |
| AI analytics/Group AI manual deep chưa có evidence riêng | P1 | Có thể thiếu confidence về UX/output thực tế dù backend fallback/schema có test | Backend test pass; manual/E2E deep chưa có bằng chứng | Không nên cam kết chất lượng output AI ngoài phạm vi fallback/schema đã test |

## 6. Đề xuất xử lý

| Ưu tiên | Đề xuất | Owner đề xuất | Ghi chú |
|---|---|---|---|
| P0 | Kiểm tra `GroupMeetingPage.vue` và `GroupHub` event handling cho participant join/leave/count | Gia Long | Đối chiếu event `JoinMeeting`, `LeaveMeeting`, `meetingParticipantJoined`, `meetingParticipantLeft`; xác minh UI subscribe đúng group/session |
| P0 | Fix meeting participant realtime trước demo nếu demo meeting realtime | Gia Long | Nếu chưa fix, không demo participant count/list như tính năng ổn định |
| P1 | Bổ sung E2E hoặc integration realtime SignalR sau khi fix bug P0 | Duy Hoàng/Gia Long | Backend permission đã pass, thiếu coverage end-to-end cho realtime participant |
| P2 | Thêm UI feedback rõ cho screen share unsupported/deny | Gia Long | Có thể đưa backlog nếu không demo screen share |
| P1 | Chạy checklist deploy config nếu cần nghiệm thu môi trường | Duy Hoàng/DevOps | Hiện chưa có bằng chứng riêng cho Docker compose/config |

## 7. Đề xuất cắt scope nếu cần

| Hạng mục | Đề xuất |
|---|---|
| Meeting realtime participant/count | Nếu bug P0 chưa fix, không demo participant realtime/count/list như tính năng ổn định |
| Meeting start/join/end backend permission | Có thể dùng làm bằng chứng backend permission vì DH-03 manual outside user pass và DH-04 backend tests pass |
| Import document | Có thể demo DOCX, ZIP, PDF unsupported roadmap rõ ràng vì manual QA pass 4/4 và backend import regression pass |
| Backend regression | Có thể dùng làm bằng chứng ổn định cho import, meeting permission, AI fallback/schema, auth boundary |
| Screen share | Nếu không fix UI feedback và chưa test headful positive case, đưa P2/backlog hoặc không đưa vào demo |
| Deploy config | Nếu chưa chạy `DH01-DEP-*`, không claim deploy config đã nghiệm thu đầy đủ |
| AI analytics/Group AI output chất lượng | Chỉ claim fallback/schema/permission smoke ở mức đã test; không claim chất lượng nội dung AI nếu chưa có manual evidence |

## 8. Kết luận demo/nghiệm thu

| Module | Trạng thái demo đề xuất | Cơ sở |
|---|---|---|
| Import document | Đủ điều kiện demo theo scope hiện tại | Manual pass 4/4; E2E import Wiki document pass; backend import regression pass |
| Auth/permission | Có thể dùng làm bằng chứng backend/E2E cơ bản | E2E login pass; auth boundary integration pass; meeting outside user 403 pass |
| Regression core backend | Có thể dùng làm bằng chứng ổn định backend | Build pass; integration 14/14; unit 92/92 |
| Meeting | Cần fix trước nghiệm thu nếu demo realtime participant/count | Start/join/end và permission có bằng chứng, nhưng participant realtime/count fail P0 |
| AI analytics | Có smoke/fallback evidence, chưa đủ để claim full manual nghiệm thu | E2E analytics pass; AI fallback/schema unit pass; manual deep check chưa có bằng chứng |
| Group AI | Chưa đủ bằng chứng manual/E2E riêng cho nghiệm thu module | Có unit output liên quan trong filter DH-04 nhưng chưa tách số riêng và chưa có manual evidence theo DH01-GAI |
| Deploy config | Chưa đủ bằng chứng nghiệm thu riêng | Chưa có evidence cho checklist `DH01-DEP-*` |

## 9. Bằng chứng chính

| Loại bằng chứng | Path |
|---|---|
| Test plan tuần | `docs/task/test-plan-tuan-2026-06-03.md` |
| Manual QA evidence log | `docs/task/qa-evidence/meeting-import-qa-2026-06-03.md` |
| Manual QA screenshots/logs | `docs/task/evidence/2026-06-03_2026-06-09/` |
| Playwright smoke spec | `tests/e2e/qaly.smoke.spec.ts` |
| Playwright fixture | `tests/e2e/fixtures/wiki-smoke.md` |
| Playwright report/result hiện có | `playwright-report/`, `test-results/` |
| Backend import tests | `tests/Qaly.UnitTests/ImportEnhancementTests.cs` |
| Backend meeting permission tests | `tests/Qaly.UnitTests/GroupsServiceTests.cs` |
| Backend AI fallback/schema tests | `tests/Qaly.UnitTests/ErumiChatServiceTests.cs` |
| Backend auth boundary tests | `tests/Qaly.IntegrationTests/AuthBoundaryIntegrationTests.cs` |

## 10. Ghi chú về bằng chứng thiếu

| Hạng mục | Trạng thái |
|---|---|
| Log file riêng cho `npm run build` DH-02 | Chưa có bằng chứng file riêng; chỉ có kết quả đã báo cáo |
| Log file riêng cho `dotnet build Qaly_project.slnx` DH-04 | Chưa có bằng chứng file riêng trong `docs/task/evidence`; chỉ có kết quả đã báo cáo |
| Deploy config `DH01-DEP-*` | Chưa có bằng chứng chạy riêng |
| AI analytics manual deep check | Chưa có bằng chứng chạy riêng |
| Group AI manual/E2E theo `DH01-GAI-*` | Chưa có bằng chứng chạy riêng |
| SignalR participant count automation | Chưa có bằng chứng automation pass; manual đang fail |
| Screen share positive case bằng browser thật/headful | Chưa có bằng chứng |

