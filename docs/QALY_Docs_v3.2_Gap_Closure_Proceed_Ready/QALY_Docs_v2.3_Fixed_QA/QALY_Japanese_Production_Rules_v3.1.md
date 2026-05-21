# QALY Japanese-style Production Rules v3.1

## 1. Nguyên tắc quản trị

| Rule | Áp dụng trong QALY | Acceptance |
|---|---|---|
| Horen-so | AI action item/task draft phải có nguồn, người phụ trách, deadline, trạng thái xác nhận | mỗi AI draft có `source_type/source_id` |
| Genchi Genbutsu | Người dùng xem lại đoạn chat/meeting gốc trước khi confirm | UI có link/source excerpt |
| Kaizen | Ghi feedback đúng/sai của AI để cải thiện prompt/rule-score | có bảng/audit feedback |
| Mieruka | Trực quan hóa cost/usage/risk | có dashboard AI usage |
| Pokayoke | Chặn thao tác sai bằng validation/schema/confirm | AI JSON schema + human confirm |
| Nemawashi | Thay đổi lớn phải có review trước khi merge | PR checklist + reviewer |
| PDCA | Mỗi sprint review AI quality/cost/risk | báo cáo sprint có AI metrics |

## 2. Bộ luật nội bộ cho AI

1. AI không tự tạo task chính thức nếu user chưa confirm.
2. AI không tự giao task nếu PM/leader chưa xác nhận.
3. AI không tự đổi deadline/status/priority của task đã tồn tại.
4. AI không được gọi trực tiếp từ frontend.
5. AI không được xử lý meeting `sensitive=true` bằng cloud API nếu không có override.
6. AI không được lưu raw prompt chứa secret/password/token.
7. AI response phải validate schema, fail thì reject/repair tối đa 1 lần.
8. Mọi AI job phải ghi `ai_usage_ledger`.
9. Budget vượt ngưỡng thì tự động chuyển mock/local/template.
10. Demo phải có fallback offline.

## 3. Production readiness checklist

| Area | Checklist |
|---|---|
| Security | secrets in env/secret store, no hardcoded key, RBAC on AI endpoints |
| Observability | AI latency, error rate, cache hit rate, token/cost dashboard |
| Resilience | provider fallback, circuit breaker, retry max 1, queue async |
| Data | backup DB, migration rollback, source traceability |
| Compliance | privacy notice, retention policy, audit log, data minimization |
| Performance | VPS memory < 80%, no LLM on 8GB VPS, Qdrant optional toggle |
| Demo | cache warmed, seed data, script, screenshots, mock fallback |
