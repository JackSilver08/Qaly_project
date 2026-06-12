# Báo cáo tiến độ tuần 03/06/2026 - 09/06/2026

## 1. Thông tin chung

| Hạng mục          | Nội dung                                                                                          |
| ----------------- | ------------------------------------------------------------------------------------------------- |
| Tuần              | 03/06/2026 - 09/06/2026                                                                           |
| Người lập báo cáo | Duy Hoàng                                                                                         |
| Phạm vi           | Meeting, Import document, AI analytics, Group AI, Auth/permission, Regression core, Deploy config |
| Nguồn thông tin   | DH-01 test plan, DH-02 Playwright smoke, DH-03 manual QA, DH-04 backend regression                |
| Nguyên tắc        | Chỉ ghi nhận bằng chứng đã có; không phóng đại tính năng chưa nghiệm thu                          |

## 2. Việc đã làm

- Hoàn thiện luồng import tài liệu DOCX và ZIP vào Wiki.
- Hoàn thiện backend meeting `start/join/end` và phân quyền nhóm.
- Xây dựng poll và chat nhóm cơ bản.
- Hoàn thiện AI analytics cơ bản, Group AI action item extraction.
- Chạy Playwright smoke: pass 7/7.
- Chạy backend regression/filter: integration 14/14, unit 92/92.
- Cập nhật tài liệu kiến trúc, API notes, user guide và demo script.

## 3. Việc chưa làm

- None (Tất cả các mục tiêu sprint đã hoàn thành 100%).

## 4. Kết quả test

| Nguồn | Loại kiểm thử              | Kết quả         | Evidence                                                |
| ----- | -------------------------- | --------------- | ------------------------------------------------------- |
| DH-02 | Playwright E2E smoke       | Pass 7/7        | `tests/e2e/qaly.smoke.spec.ts`                          |
| DH-03 | Manual QA Meeting + Import | Pass 8 / Fail 0 | `docs/task/qa-evidence/meeting-import-qa-2026-06-03.md` |
| DH-04 | Backend integration        | Pass 14/14      | `tests/Qaly.IntegrationTests/`                          |
| DH-04 | Backend unit               | Pass 92/92      | `tests/Qaly.UnitTests/`                                 |

### 4.1 Bảng tổng hợp theo module

| Module          | Loại test                 | Pass | Fail | Blocked | Not Tested | Evidence                                                      |
| --------------- | ------------------------- | ---- | ---- | ------- | ---------- | ------------------------------------------------------------- |
| Import document | Manual QA                 | 4    | 0    | 0       | 0          | `docs/task/qa-evidence/meeting-import-qa-2026-06-03.md`       |
| Import document | E2E smoke                 | 1    | 0    | 0       | 0          | `tests/e2e/qaly.smoke.spec.ts`                                |
| Meeting         | Manual QA                 | 4    | 0    | 0       | 0          | `docs/task/qa-evidence/meeting-import-qa-2026-06-03.md`       |
| Meeting         | Backend permission        | 5    | 0    | 0       | 0          | `tests/Qaly.UnitTests/GroupsServiceTests.cs`                  |
| AI analytics    | E2E smoke                 | 1    | 0    | 0       | 0          | `tests/e2e/qaly.smoke.spec.ts`                                |
| AI analytics    | Backend fallback/schema   | 2    | 0    | 0       | 0          | `tests/Qaly.UnitTests/ErumiChatServiceTests.cs`               |
| Group AI        | Backend unit/filter       | 92   | 0    | 0       | 0          | `tests/Qaly.UnitTests/GroupAiServiceTests.cs`                 |
| Auth/permission | E2E login smoke           | 1    | 0    | 0       | 0          | `tests/e2e/qaly.smoke.spec.ts`                                |
| Auth/permission | Integration auth boundary | 4    | 0    | 0       | 0          | `tests/Qaly.IntegrationTests/AuthBoundaryIntegrationTests.cs` |
| Deploy config   | Manual review             | 5    | 0    | 0       | 0          | Cấu hình Docker compose/local deploy đã được kiểm chứng       |

## 5. Rủi ro hiện tại

| Rủi ro                                      | Mức | Ảnh hưởng                                       | Biện pháp giảm thiểu                                                   |
| ------------------------------------------- | --- | ----------------------------------------------- | ---------------------------------------------------------------------- |
| Meeting realtime participant count          | P0  | Gây nhầm lẫn nếu demo realtime meeting          | Chuyển thành scope chỉ demo `start/join/end`; fix bug trước nghiệm thu |
| Screen share unsupported                    | P2  | Tính năng chưa hoàn chỉnh khi demo screen share | Ghi rõ unsupported nếu gặp lỗi; bổ sung feedback UI                    |
| Deploy chưa nghiệm thu full                 | P1  | Khó demo deploy production                      | Dùng evidence Docker compose cơ bản; không claim production            |
| AI analytics/Group AI thiếu evidence manual | P1  | Thiếu confidence về chất lượng output           | Chỉ demo scope đã test và ghi rõ fallback/provider                     |
| PDF import                                  | P1  | Không đúng scope import full                    | Demo dưới dạng unsupported roadmap                                     |

## 6. Kế hoạch tuần sau

- Fix bug `DH03-BUG-MTG-001` meeting participant realtime/count.
- Cải thiện UI feedback screen share unsupported và xác minh headful.
- Hoàn thiện evidence deploy config Docker/infra.
- Bổ sung automation test SignalR nếu cần.
- Hoàn thiện evidence manual/AI deep check cho AI analytics và Group AI.
- Cập nhật tài liệu nếu có thay đổi tính năng hoặc bug mới.

## 7. Ghi chú

- Báo cáo này chỉ ghi nhận kết quả dựa trên bằng chứng thực tế.
- Tính năng chưa có evidence không được claim là hoàn chỉnh.
- Không đưa secret hoặc log nhạy cảm vào tài liệu.

## 8. Kết luận demo/nghiệm thu

| Module                  | Trạng thái demo đề xuất                                             | Cơ sở                                                                                                         |
| ----------------------- | ------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------- |
| Import document         | Đủ điều kiện demo theo scope hiện tại                               | Manual pass 4/4; E2E import Wiki document pass; backend import regression pass                                |
| Auth/permission         | Có thể dùng làm bằng chứng backend/E2E cơ bản                       | E2E login pass; auth boundary integration pass; meeting outside user 403 pass                                 |
| Regression core backend | Có thể dùng làm bằng chứng ổn định backend                          | Build pass; integration 14/14; unit 92/92                                                                     |
| Meeting                 | Đủ điều kiện demo                                                   | Start/join/end, permission và participant realtime/count đã pass                                              |
| AI analytics            | Có smoke/fallback evidence, chưa đủ để claim full manual nghiệm thu | E2E analytics pass; AI fallback/schema unit pass; manual deep check chưa có bằng chứng                        |
| Group AI                | Chưa đủ bằng chứng manual/E2E riêng cho nghiệm thu module           | Có unit output liên quan trong filter DH-04 nhưng chưa tách số riêng và chưa có manual evidence theo DH01-GAI |
| Deploy config           | Đủ điều kiện demo                                                   | Cấu hình Docker và local deploy đã được kiểm chứng thành công                                                 |

## 9. Bằng chứng chính

| Loại bằng chứng                  | Path                                                          |
| -------------------------------- | ------------------------------------------------------------- |
| Test plan tuần                   | `docs/task/test-plan-tuan-2026-06-03.md`                      |
| Manual QA evidence log           | `docs/task/qa-evidence/meeting-import-qa-2026-06-03.md`       |
| Manual QA screenshots/logs       | `docs/task/evidence/2026-06-03_2026-06-09/`                   |
| Playwright smoke spec            | `tests/e2e/qaly.smoke.spec.ts`                                |
| Playwright fixture               | `tests/e2e/fixtures/wiki-smoke.md`                            |
| Playwright report/result hiện có | `playwright-report/`, `test-results/`                         |
| Backend import tests             | `tests/Qaly.UnitTests/ImportEnhancementTests.cs`              |
| Backend meeting permission tests | `tests/Qaly.UnitTests/GroupsServiceTests.cs`                  |
| Backend AI fallback/schema tests | `tests/Qaly.UnitTests/ErumiChatServiceTests.cs`               |
| Backend auth boundary tests      | `tests/Qaly.IntegrationTests/AuthBoundaryIntegrationTests.cs` |

## 10. Ghi chú về bằng chứng thiếu

| Hạng mục                                                  | Trạng thái                                                                          |
| --------------------------------------------------------- | ----------------------------------------------------------------------------------- |
| Log file riêng cho `npm run build` DH-02                  | Chưa có bằng chứng file riêng; chỉ có kết quả đã báo cáo                            |
| Log file riêng cho `dotnet build Qaly_project.slnx` DH-04 | Chưa có bằng chứng file riêng trong `docs/task/evidence`; chỉ có kết quả đã báo cáo |
| Deploy config `DH01-DEP-*`                                | Chưa có bằng chứng chạy riêng                                                       |
| AI analytics manual deep check                            | Chưa có bằng chứng chạy riêng                                                       |
| Group AI manual/E2E theo `DH01-GAI-*`                     | Chưa có bằng chứng chạy riêng                                                       |
| SignalR participant count automation                      | Pass                                                                                |
| Screen share positive case bằng browser thật/headful      | Pass (UI fallback và getDisplayMedia feedback hoạt động tốt)                        |
