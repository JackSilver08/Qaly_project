# 📖 QALY PROJECT – DOCS INDEX

> Thư mục tài liệu dự án QALY Project

---

## Danh sách tài liệu chính

| #   | File                                                       | Nội dung                                                              | Trạng thái             |
| --- | ---------------------------------------------------------- | --------------------------------------------------------------------- | ---------------------- |
| 1   | [01_Analysis.md](./01_Analysis.md)                         | Phân tích dự án: môi trường, hiện trạng, mapping DB                   | ✅ Hoàn thành          |
| 2   | [02_Architecture.md](./02_Architecture.md)                 | Kiến trúc thực tế: module, luồng dữ liệu, SignalR, AI, import, deploy | ✅ Cập nhật 05/06/2026 |
| 3   | [03_Database_Design.md](./03_Database_Design.md)           | Thiết kế DB: schema SQL Server, indexes, mapping Postgres             | ✅ Hoàn thành          |
| 4   | [04_Implementation_Plan.md](./04_Implementation_Plan.md)   | Kế hoạch triển khai: timeline, phân công, milestones                  | ✅ Hoàn thành          |
| 5   | [05_Risk_Analysis.md](./05_Risk_Analysis.md)               | Phân tích rủi ro: risk matrix, mitigation                             | ✅ Hoàn thành          |
| 6   | [06_Docker_Setup.md](./06_Docker_Setup.md)                 | Docker: services, commands, connection strings                        | ✅ Hoàn thành          |
| 7   | [07_CICD_Workflow.md](./07_CICD_Workflow.md)               | CI/CD pipeline, Git workflow, branch protection                       | ✅ Hoàn thành          |
| 8   | [08_AI_Integration.md](./08_AI_Integration.md)             | Kế hoạch tích hợp AI: architecture, lộ trình, giới hạn                | ✅ Cập nhật            |
| 9   | [user-guide-qaly.md](./user-guide-qaly.md)                 | Hướng dẫn người dùng cuối tiếng Việt                                  | ✅ Cập nhật 05/06/2026 |
| 10  | [demo-script-2026-06-09.md](./demo-script-2026-06-09.md)   | Kịch bản demo 8-10 phút với backup plan                               | ✅ Cập nhật 05/06/2026 |
| 11  | [apiScalar/OpenAPI notes.md](./apiScalar/OpenAPI notes.md) | Tài liệu API chi tiết P0: group, meeting, poll, import, AI analytics  | ✅ Mới                 |

---

## Tài liệu QA và nghiệm thu tuần 03/06/2026 - 09/06/2026

| File                                                                                                   | Nội dung                                        | Trạng thái  |
| ------------------------------------------------------------------------------------------------------ | ----------------------------------------------- | ----------- |
| [task/test-plan-tuan-2026-06-03.md](./task/test-plan-tuan-2026-06-03.md)                               | Test plan tổng: 37 case                         | Đã tạo      |
| [task/qa-evidence/meeting-import-qa-2026-06-03.md](./task/qa-evidence/meeting-import-qa-2026-06-03.md) | Bằng chứng QA manual Meeting và Import document | Đã tạo      |
| [task/Bao_cao_kiem_thu_tuan_2026-06-03.md](./task/Bao_cao_kiem_thu_tuan_2026-06-03.md)                 | Báo cáo tiến độ & nghiệm thu tuần               | Đã cập nhật |
| [task/checklist-nghiem-thu-2026-06-09.csv](./task/checklist-nghiem-thu-2026-06-09.csv)                 | Checklist nghiệm thu theo module                | Đã cập nhật |

---

## Giới hạn cần đọc trước demo/nghiệm thu

- `DH03-BUG-MTG-001`: meeting participant realtime/count chưa cập nhật ổn định.
- `DH03-BUG-MTG-002`: screen share unsupported thiếu UI feedback rõ.
- PDF import hiện là roadmap/unsupported.
- AI analytics/Group AI có evidence backend/fallback; chưa có evidence manual E2E đầy đủ.
- Deploy config có cấu hình cơ bản; chưa có evidence nghiệm thu production hoàn chỉnh.

---

## Quy ước đặt tên

- Prefix `01_`, `02_`, ... để sắp xếp theo luồng logic.
- File tiếng Anh, nội dung có thể song ngữ.
- Cập nhật `README.md` khi thêm/dời tài liệu.

---

## Tài liệu dự kiến bổ sung

| #   | File (dự kiến)           | Nội dung                                 |
| --- | ------------------------ | ---------------------------------------- | ------- |
| 12  | `11_Deployment_Guide.md` | Hướng dẫn deploy Docker, SQL, Redis, Seq | Dự kiến |
| 13  | `12_Changelog.md`        | Lịch sử thay đổi tài liệu và code        | Dự kiến |
