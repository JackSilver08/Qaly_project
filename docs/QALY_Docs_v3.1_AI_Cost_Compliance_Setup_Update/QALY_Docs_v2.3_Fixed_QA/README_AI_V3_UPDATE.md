# README — AI v3.0 Meetily Production Update

Gói này bổ sung lớp đặc tả AI v3.0 lên trên bộ `QALY_Docs_v2.3_Fixed_QA`.

## Nên đọc theo thứ tự

1. `QALY_AI_Production_Strategy_v3.0_Meetily_First.md`
2. `QALY_AI_Function_Catalog_v3.0.csv`
3. `QALY_AI_Endpoint_Contract_v3.0.md`
4. `QALY_AI_Deployment_Runbook_v3.0.md`
5. `QALY_10_Week_Implementation_Plan_v3.0.md`
6. `QALY_AI_Module_Specification_v3.0.md`
7. `QALY_UML_Diagram_Specification_v3.0_AI_Meetily_Supplement.md`

## Tóm tắt thay đổi

- Dùng Meetily cho meeting note thay vì tự code audio/transcription.
- Dùng AI API làm provider chính cho demo để tránh rủi ro chất lượng local model.
- Dùng Ollama local 7B/8B làm fallback, không bắt buộc 14B.
- Dùng Qdrant như semantic memory P1/optional, không làm source of truth.
- Thêm mock/cache/quota/golden dataset để kiểm soát token.
- Thêm runbook VPS 8GB + laptop RTX4060 16GB RAM.
