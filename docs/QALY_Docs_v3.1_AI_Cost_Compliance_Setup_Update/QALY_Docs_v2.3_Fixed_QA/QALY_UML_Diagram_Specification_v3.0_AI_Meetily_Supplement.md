# QALY UML / Diagram Specification v3.0 — AI Meetily Supplement

**Ngày:** 19/05/2026  
**Bổ sung cho:** `QALY_UML_Diagram_Specification_v2.3.md`

---

## 1. Mục tiêu bổ sung

Bổ sung các sơ đồ làm rõ chiến lược AI v3.0:

- Meetily-first deployment.
- Meetily sync meeting note sequence.
- AI provider routing/fallback.
- Hybrid task recommendation.
- Meetily → SQL → Qdrant semantic memory data flow.
- AI resource/cost control flow.

---

## 2. Danh sách sơ đồ mới

| ID | File | Mục đích |
|---|---|---|
| ARCH_06 | `docs/diagrams/mermaid/ARCH_06_AI_Meetily_First_Deployment.mmd` | Kiến trúc triển khai AI với VPS + laptop + ngrok + API provider |
| SEQ_11 | `docs/diagrams/mermaid/SEQ_11_Meetily_Sync_Meeting_Note.mmd` | Luồng sync transcript/summary từ Meetily vào QALY |
| SEQ_12 | `docs/diagrams/mermaid/SEQ_12_AI_API_Fallback_Routing.mmd` | Luồng route API/local/mock/cache/quota |
| SEQ_13 | `docs/diagrams/mermaid/SEQ_13_Task_Recommendation_Hybrid.mmd` | Gợi ý assignee bằng score + AI explanation |
| DATA_03 | `docs/diagrams/mermaid/DATA_03_Meetily_Qdrant_Memory_DFD.mmd` | Dòng dữ liệu Meetily sang SQL/Qdrant |
| OPS_03 | `docs/diagrams/mermaid/OPS_03_AI_Resource_Control.mmd` | Kiểm soát tài nguyên, token, fallback |

---

## 3. Quy ước mới

- AI API là primary cho demo chất lượng cao.
- Ollama local là fallback/dev/offline, không là SPOF.
- Meetily là meeting worker, không thay business backend.
- Qdrant optional/P1, có thể tắt mà MVP vẫn chạy.
- Task/assignee/deadline do AI sinh đều cần human confirmation.
