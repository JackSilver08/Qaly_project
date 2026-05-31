# QALY Docs v3.1 - AI Costed Production Update

Bản v3.1 cập nhật từ v3.0 theo hướng: **Meetily-first + AI API primary + Ollama local fallback + Qdrant optional semantic memory**.

## Mục tiêu update

1. Cập nhật tất cả thay đổi có ảnh hưởng tới bảng, schema, endpoint, sequence, deployment và risk.
2. Bổ sung cost model thực tế cho 10 tuần còn lại, gồm best/base/worst/risk scenario.
3. Bổ sung bộ luật/quy tắc triển khai AI: pháp lý Việt Nam, bảo mật, privacy, license, human-in-the-loop.
4. Bổ sung setup guide cực chi tiết cho laptop RTX4060/16GB RAM và VPS 8GB.
5. Chốt hướng thực tế: không ép Qdrant + Ollama làm core, dùng AI API để đảm bảo chất lượng demo, dùng Meetily để giảm risk audio/transcription.

## File mới quan trọng

- `QALY_AI_Production_Strategy_v3.1_Meetily_API_Costed.md`
- `QALY_AI_Cost_Estimation_v3.1.md`
- `QALY_AI_Cost_Model_v3.1.csv`
- `QALY_Budget_Scenarios_v3.1.csv`
- `QALY_AI_Provider_Decision_Matrix_v3.1.csv`
- `QALY_AI_Function_Catalog_v3.1.csv`
- `QALY_AI_Setup_Guide_End_To_End_v3.1.md`
- `QALY_Compliance_Law_Checklist_VN_v3.1.md`
- `QALY_AI_Risk_Register_v3.1.csv`
- `QALY_Change_Impact_Matrix_v3.1.csv`
- `QALY_AI_Data_Schema_Migration_v3.1.sql`
- `QALY_UML_Diagram_Specification_v3.1_Affected_Diagrams.md`
- `QALY_10_Week_Implementation_Plan_v3.1.md`

## Kết luận kiến trúc

- **P0 trong 10 tuần:** Meetily import/sync, AI Gateway, AI API key primary, cost control, task draft, chat summary, meeting action items, task recommendation bằng rule-score + AI explain.
- **P1 nếu dư thời gian:** Qdrant semantic search, duplicate detection, project Q&A.
- **Không làm trong P0:** tự viết realtime audio transcription từ đầu, production speaker diarization, chạy Ollama 14B làm core trên VPS 8GB.
