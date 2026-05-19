# QALY QA Fix Report v2.3

**Ngày rà soát:** 19/05/2026  
**Input:** `QALY_Docs_v2.2_With_Production_Diagrams`  
**Output:** `QALY_Docs_v2.3_Fixed_QA`

## 1. Lỗi đã phát hiện và đã fix

| ID | Nhóm | Lỗi/rủi ro | Cách fix trong v2.3 | Kết quả |
|---|---|---|---|---|
| FIX-001 | Markdown | `README_Rendering_Diagrams.md` có literal triple-backtick trong inline text, làm bộ đếm fence báo lẻ. | Viết lại thành `code fence dạng mermaid`. | Fixed |
| FIX-002 | Traceability | 2 dòng component/deployment dùng chuỗi `.mmd/.puml`, không map được path thật khi kiểm tra tự động. | Chuẩn hóa toàn bộ cột `file` thành path thật dưới `docs/diagrams/...`. | Fixed |
| FIX-003 | Mermaid | Use case labels dùng `<small>...</small>`, có thể không render ở một số Mermaid renderer hoặc khi tắt HTML labels. | Bỏ tag `<small>`, giữ nội dung priority dạng text. | Fixed |
| FIX-004 | Mermaid class | Nullable type dùng `?` trong class diagram có thể bị parser cũ hiểu sai. | Đổi sang dạng `nullable` dễ đọc và an toàn hơn. | Fixed |
| FIX-005 | PlantUML | `skinparam actorStyle awesome` phụ thuộc phiên bản PlantUML. | Bỏ dòng này để tăng tương thích. | Fixed |
| FIX-006 | DB docs | Database Dictionary v2.0 vẫn mở đầu theo hướng PostgreSQL-primary trong khi dự án đã chốt SQL Server. | Patch header/section 0.2 và mapping type/default sang SQL Server trong bản v2.3. | Fixed-Doc |
| FIX-007 | Gap/Checklist | GAP-031 và Gate 0 vẫn `Open/Todo` trong file gốc dù v2.2 đã bổ sung diagram. | Thêm gap register/checklist current status v2.3, đóng phần tài liệu và giữ code gaps ở trạng thái cần evidence. | Fixed-Doc |

## 2. Điều chưa thể đóng nếu chỉ có file tài liệu

Các gap về code/runtime như RBAC enforcement, tenant filter, migration chạy thật, CI/CD, upload security, AI permission filter, Qdrant rebuild, notification realtime... **không được đánh dấu Closed** trong v2.3 vì cần repo, log build/test, screenshot demo hoặc UAT evidence. Chúng đã được giữ trong `QALY_Gap_Register_v2.3_Current_Status.csv` với trạng thái `Open-Code-Backlog` hoặc `Code/Project-Evidence-Still-Required`.

## 3. Kết quả validation gói v2.3

- CSV parse: pass.
- Markdown fence balance: pass.
- Mermaid files present: 33.
- PlantUML files present: 8.
- Traceability file paths exist: pass.
- GAP-031 documentation: closed by diagram package.
- Gate 0 documentation checklist: verified with evidence links.

## 4. Khuyến nghị dùng khi nộp/bảo vệ

Dùng `QALY_UML_Diagram_Specification_v2.3.md` làm file master để copy sơ đồ vào báo cáo. Khi giảng viên hỏi “đã fix hết gap chưa”, trả lời rõ: **gap tài liệu/UML đã fix; gap triển khai cần chứng minh bằng code/test/demo theo Gate 1–6**.
