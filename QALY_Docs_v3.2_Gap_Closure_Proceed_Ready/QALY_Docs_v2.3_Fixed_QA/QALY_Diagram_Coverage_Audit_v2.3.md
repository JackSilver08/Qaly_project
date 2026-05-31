# QALY Diagram Coverage Audit v2.3 — Fixed QA

## Kết luận nhanh

Tài liệu v2.1 **chưa đầy đủ sơ đồ đặc tả/UML production**. Gói cũ đã có dữ liệu nền rất tốt: `68` use cases, `167` endpoints, `188` bảng logical, business rules, test cases và traceability. Tuy nhiên, trong file gốc không có file `.mmd`, `.puml`, `.drawio`, `.svg`, `.png` cho sơ đồ cuối cùng.

## Bằng chứng trong gói v2.1

| Nguồn trong gói cũ | Tình trạng |
|---|---|
| `QALY_Acceptance_Checklist_v2.1.csv` | Dòng `G0_DOCUMENTATION-05` ghi: `UML tối thiểu có đủ 6 sơ đồ` đang `Todo`. |
| `QALY_Gap_Audit_NghiemThu_v2.1.md` | Checklist yêu cầu tối thiểu: use case, class, activity task lifecycle, sequence create task/update status, component, deployment. |
| `QALY_Gap_Register_v2.1.csv` | `GAP-031` ghi: `Chưa có sơ đồ UML cuối cùng`, action: vẽ use case, class, sequence, activity, component, deployment. |
| `QALY_Database_Dictionary.md` | Có relationship map rút gọn dạng text, chưa phải ERD/renderable diagram. |
| `QALY_Project_Structure_Mapping.md` | Có thư mục mục tiêu `docs/diagrams/`, nhưng chưa có sơ đồ trong package. |

## Gói bổ sung này đã thêm

- Mermaid diagrams: `33` file.
- PlantUML diagrams: `8` file.
- Master specification: `QALY_UML_Diagram_Specification_v2.3.md`.
- Traceability: `QALY_Diagram_Traceability_v2.3.csv`.
- Hướng dẫn render: `README_Rendering_Diagrams.md`.

## Mức đáp ứng checklist tối thiểu

| Checklist UML tối thiểu | File đáp ứng | Status đề xuất |
|---|---|---|
| Use case diagram | `UC_00_Overall_68_Use_Cases.puml/.mmd` + module diagrams | Done |
| Class diagram | `UML_01_Core_Domain_Class_Diagram.mmd` | Done |
| Activity task lifecycle | `ACT_01_Task_Lifecycle_End_To_End.mmd` | Done |
| Sequence create task | `SEQ_03_Create_Task.mmd` | Done |
| Sequence update status | `SEQ_04_Task_Status_Approval.mmd` | Done |
| Component diagram | `ARCH_03_Backend_Component_Clean_Architecture.mmd/.puml` | Done |
| Deployment diagram | `ARCH_05_Deployment_Production.mmd/.puml` | Done |
| ERD bổ sung | `DATA_01_Core_ERD_SQLServer.mmd` | Done |
| Security/RBAC flow bổ sung | `SEC_01_Authorization_Tenant_Flow.mmd` | Done |
| AI/RAG flow bổ sung | `SEQ_07_AI_RAG_Project_Progress.mmd`, `DATA_02_AI_Knowledge_Sync_DFD.mmd` | Done |


---

## 6. QA fixes applied in v2.3

| Nhóm lỗi | Trạng thái | Ghi chú |
|---|---:|---|
| README Markdown fence | Fixed | Bỏ literal triple-backtick gây lệch kiểm tra tự động. |
| Traceability diagram paths | Fixed | Chuẩn hóa `file` thành path thật dưới `docs/diagrams/...`. |
| Mermaid renderer compatibility | Fixed | Bỏ `<small>` trong node labels và chuẩn hóa nullable attributes trong class diagram. |
| PlantUML compatibility | Fixed | Bỏ `actorStyle awesome` để tương thích renderer PlantUML cũ hơn. |
| GAP-031 UML | Closed-Doc | Đã có 33 Mermaid + 8 PlantUML + master spec. |
| Gate 0 documentation checklist | Verified-Doc | G0 đã có evidence file; các gate code vẫn cần bằng chứng chạy/test thật. |
