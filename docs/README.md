# 📖 QALY PROJECT – DOCS INDEX

> Thư mục tài liệu dự án Qaly Project

---

## Danh sách tài liệu

| # | File | Nội dung | Trạng thái |
|---|---|---|---|
| 1 | [01_Analysis.md](./01_Analysis.md) | Phân tích dự án: môi trường, hiện trạng, mapping DB | ✅ Hoàn thành |
| 2 | [02_Architecture.md](./02_Architecture.md) | Thiết kế kiến trúc: Clean Architecture, cấu trúc thư mục, NuGet | ✅ Hoàn thành |
| 3 | [03_Database_Design.md](./03_Database_Design.md) | Thiết kế DB: schema SQL Server, indexes, mapping Postgres | ✅ Hoàn thành |
| 4 | [04_Implementation_Plan.md](./04_Implementation_Plan.md) | Kế hoạch triển khai: timeline, phân công, milestones | ✅ Hoàn thành |
| 5 | [05_Risk_Analysis.md](./05_Risk_Analysis.md) | Phân tích rủi ro: risk matrix, mitigation | ✅ Hoàn thành |
| 6 | [06_Docker_Setup.md](./06_Docker_Setup.md) | Docker: services, commands, connection strings | ✅ Hoàn thành |
| 7 | [07_CICD_Workflow.md](./07_CICD_Workflow.md) | CI/CD pipeline, Git workflow, branch protection | ✅ Hoàn thành |
| 8 | [08_AI_Integration.md](./08_AI_Integration.md) | Kế hoạch tích hợp AI: 7 điểm tích hợp, lộ trình, chi phí; cần đọc kèm ghi chú giới hạn nghiệm thu | Planning / cập nhật DH-06 |
| 9 | [user-guide-qaly.md](./user-guide-qaly.md) | Hướng dẫn sử dụng tiếng Việt theo scope đã có bằng chứng QA | ✅ DH-06 |
| 10 | [demo-script-2026-06-09.md](./demo-script-2026-06-09.md) | Kịch bản demo nghiệm thu, không demo quá phạm vi đã kiểm thử | ✅ DH-06 |

---

## Tài liệu QA và nghiệm thu tuần 03/06/2026 - 09/06/2026

| File | Nội dung | Trạng thái |
|---|---|---|
| [task/test-plan-tuan-2026-06-03.md](./task/test-plan-tuan-2026-06-03.md) | Test plan tổng: 37 case, P0/P1/P2 | Đã tạo |
| [task/qa-evidence/meeting-import-qa-2026-06-03.md](./task/qa-evidence/meeting-import-qa-2026-06-03.md) | Bằng chứng kiểm thử manual Meeting và Import document | Đã tạo |
| [task/Bao_cao_kiem_thu_tuan_2026-06-03.md](./task/Bao_cao_kiem_thu_tuan_2026-06-03.md) | Báo cáo kiểm thử cuối tuần | Đã tạo |
| [task/checklist-nghiem-thu-2026-06-09.csv](./task/checklist-nghiem-thu-2026-06-09.csv) | Checklist nghiệm thu Pass/Fail/Not Tested theo bằng chứng hiện có | ✅ DH-06 |

### Giới hạn cần đọc trước demo/nghiệm thu

- Meeting participant realtime/count đang có lỗi P0 `DH03-BUG-MTG-001`.
- Screen share unsupported chưa có UI feedback rõ, lỗi P2 `DH03-BUG-MTG-002`.
- Deploy config, AI analytics manual deep check và Group AI manual/E2E chưa có bằng chứng nghiệm thu đầy đủ.
- Import document pass theo bằng chứng hiện tại, nhưng PDF đang là roadmap/unsupported; không ghi là hỗ trợ PDF đầy đủ.

---

## Quy ước đặt tên

- Prefix số thứ tự: `01_`, `02_`, ... để sắp xếp theo thứ tự logic
- Tên file tiếng Anh, nội dung song ngữ Anh-Việt
- Cập nhật `README.md` này khi thêm tài liệu mới

---

## Tài liệu sẽ bổ sung

| # | File (dự kiến) | Nội dung |
|---|---|---|
| 11 | `11_Deployment_Guide.md` | Hướng dẫn deploy; chưa có bằng chứng nghiệm thu đầy đủ trong tuần này |
| 12 | `12_Changelog.md` | Lịch sử thay đổi |
