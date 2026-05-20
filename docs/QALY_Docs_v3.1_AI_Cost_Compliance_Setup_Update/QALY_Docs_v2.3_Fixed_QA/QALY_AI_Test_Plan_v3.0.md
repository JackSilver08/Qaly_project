# QALY Workspace — AI Test Plan v3.0

**Mục tiêu:** kiểm thử AI tiết kiệm token, ổn định demo, đúng bảo mật.

---

## 1. Test strategy

- Unit test: dùng Mock Provider, không gọi API thật.
- Integration test: dùng 5–10 sample cố định, có thể gọi API nếu bật `AI_TEST_REAL_PROVIDER=true`.
- E2E demo: dùng cache/golden output để tránh flaky.
- Security test: kiểm tra tenant/project/visibility trước và sau retrieval.
- Performance smoke test: timeout/retry/provider fallback.

---

## 2. Golden dataset tối thiểu

| Dataset | Số mẫu | Dùng cho |
|---|---:|---|
| Meeting transcript sample | 5 | extract action item/decision/keyword |
| Chat thread sample | 5 | summarize + task candidate |
| Task recommendation sample | 5 | score + explanation |
| Progress summary sample | 3 | report generation |
| Permission leak sample | 5 | internal/private/customer-safe |

---

## 3. Test cases chính

| TC ID | Scenario | Expected |
|---|---|---|
| TC-AI-001 | Import Meetily transcript sample | Meeting note saved, checksum stored |
| TC-AI-002 | Extract action items from transcript | JSON valid, action_items not empty, confidence present |
| TC-AI-003 | User rejects action item | No task created, audit rejected |
| TC-AI-004 | User confirms action item | Task created with source link |
| TC-AI-005 | Summarize chat with private messages | Only allowed messages included |
| TC-AI-006 | Task candidate from chat | Draft task generated, not committed |
| TC-AI-007 | Recommend assignee | Candidate score breakdown present |
| TC-AI-008 | AI API timeout | Fallback local/mock used, UI not crash |
| TC-AI-009 | Quota exceeded | Mock/cache/quota message returned |
| TC-AI-010 | Qdrant disabled | MVP still works with SQL search |
| TC-AI-011 | Ollama disabled | API/mock flow still works |
| TC-AI-012 | Customer asks AI about internal data | No leak; answer says no accessible context |

---

## 4. Acceptance metrics

| Metric | Target |
|---|---|
| JSON schema valid rate | >= 95% for golden samples |
| AI output human approval required | 100% for task/deadline/assign |
| Permission leakage | 0 critical issue |
| Demo AI flow success | >= 90% with API/cache/mock |
| Real API calls in CI | 0 by default |
| Average P0 AI response | < 15s with API; local can be slower |

---

## 5. Demo fallback matrix

| Failure | Expected behavior |
|---|---|
| AI API rate limit | Use cached/mock response |
| Laptop/ngrok offline | Manual upload Meetily export |
| Ollama slow | Disable local provider |
| Qdrant down | Disable semantic flag, SQL search only |
| Meetily live fail | Use pre-recorded export sample |
