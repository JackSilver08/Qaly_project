# QALY UML/Diagram Specification v3.1 - Affected AI Diagrams

Bản v3.1 không xóa sơ đồ cũ. Các sơ đồ sau được thêm để phản ánh thay đổi Meetily-first/API-primary/cost-control/compliance.

## Diagram list

| Diagram | File | Mục đích |
|---|---|---|
| ARCH_AI_01 | `docs/diagrams/mermaid/ARCH_AI_01_Meetily_API_Primary_v3_1.mmd` | kiến trúc AI mới |
| DEPLOY_AI_01 | `docs/diagrams/mermaid/DEPLOY_AI_01_VPS8GB_LaptopWorker_v3_1.mmd` | triển khai VPS 8GB + laptop RTX4060 |
| SEQ_AI_01 | `docs/diagrams/mermaid/SEQ_AI_01_Meetily_Import_Action_Items_v3_1.mmd` | luồng import Meetily và trích action item |
| SEQ_AI_02 | `docs/diagrams/mermaid/SEQ_AI_02_Task_Recommendation_HITL_v3_1.mmd` | gợi ý giao task có human confirm |
| SEQ_AI_03 | `docs/diagrams/mermaid/SEQ_AI_03_Provider_Fallback_Cache_Budget_v3_1.mmd` | fallback/cache/quota/cost control |
| DATA_AI_01 | `docs/diagrams/mermaid/DATA_AI_01_AI_Audit_Cost_ERD_v3_1.mmd` | bảng AI usage/cache/audit/draft |
| DFD_AI_01 | `docs/diagrams/mermaid/DFD_AI_01_Compliance_Data_Flow_v3_1.mmd` | luồng dữ liệu và compliance |

## Mermaid render note

Render bằng Mermaid CLI hoặc Markdown viewer hỗ trợ Mermaid. Các file `.mmd` giữ độc lập để copy vào báo cáo/slide.
