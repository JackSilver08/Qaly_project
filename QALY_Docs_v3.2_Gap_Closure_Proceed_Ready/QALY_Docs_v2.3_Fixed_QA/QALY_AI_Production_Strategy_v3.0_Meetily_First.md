# QALY Workspace — AI Production Strategy v3.0: Meetily-first + API-first + Local Fallback

**Phiên bản:** v3.0 — cập nhật định hướng AI sau review giảng viên  
**Ngày:** 19/05/2026  
**Áp dụng cho:** QALY Workspace / Mini Zalo / Task Management / Meeting Note / AI Assistant  
**Mục tiêu thực tế:** hoàn thành trong 10 tuần, giảm rủi ro kỹ thuật, giảm chi phí, demo ổn định, đúng tư duy production/Japan-style.

---

## 1. Kết luận thay đổi định hướng

Tài liệu v2.3 đã có hướng **Qdrant + Ollama**. Tuy nhiên sau review rủi ro với giảng viên, định hướng v3.0 được điều chỉnh thành:

> **Meetily-first cho AI Meeting Note, AI API-first cho tác vụ cần chất lượng ổn định, Ollama local làm fallback/dev/offline, Qdrant làm semantic memory có thể bật theo phase.**

Không còn trình bày Qdrant + Ollama như “xương sống duy nhất”. Thay vào đó, hệ thống dùng kiến trúc **AI Provider Gateway** để có thể chuyển giữa:

1. **Meetily Local Worker**: ghi âm, transcribe, summary meeting.
2. **AI API Provider**: extract JSON, task suggestion, report, chat summary, reasoning ổn định khi bảo vệ.
3. **Ollama Local Provider**: chạy 7B/8B Q4 cho dev/offline/light tasks.
4. **Mock Provider**: test/demo fallback không tốn token.
5. **Qdrant Semantic Store**: tìm kiếm ngữ nghĩa và memory dài hạn, không phải nguồn sự thật chính.

Nguồn sự thật nghiệp vụ luôn là **SQL Server**. Qdrant chỉ là index có thể rebuild.

---

## 2. Vì sao không code lại Meetily từ đầu

Phần rủi ro nhất của AI Meeting Note không phải prompt LLM, mà là:

- audio capture microphone/system audio;
- realtime transcription;
- device handling theo Windows/macOS/Linux;
- xử lý audio clipping/noise;
- timestamp transcript;
- summary dài không timeout;
- lưu meeting và export ổn định.

Meetily đã xử lý phần này theo hướng desktop local-first. Vì còn 10 tuần, **không nên tự code lại audio/transcription pipeline**. Chiến lược đúng:

```text
Meetily xử lý meeting audio/transcript/summary
→ QALY import/sync transcript + summary
→ QALY extract keyword/action item/deadline/decision
→ User xác nhận
→ QALY tạo task/wiki/report/semantic memory
```

---

## 3. Quyết định kiến trúc chính thức v3.0

| Hạng mục | Quyết định v3.0 | Lý do |
|---|---|---|
| Meeting note | Áp dụng Meetily làm Local Meeting Worker/Connector | Giảm rủi ro audio + transcription |
| LLM chính khi demo | AI API qua AiGateway | Chất lượng ổn định, không phụ thuộc máy local |
| LLM local | Ollama 7B/8B Q4 cho dev/fallback | RTX4060 + 16GB RAM không nên gánh 14B liên tục |
| Vector DB | Qdrant bật ở P1 cho semantic search/memory | Có thể rebuild, không chặn MVP |
| DB nghiệp vụ | SQL Server 2022 | Đã thống nhất trong v2.3 |
| VPS 8GB | Chạy app/backend/SQL/Redis nhỏ, không chạy LLM | 8GB không phù hợp Ollama 14B/meeting transcription |
| Laptop RTX4060 | Chạy Meetily + Ollama nhẹ khi cần | Phù hợp local AI worker/demo |
| Ngrok | Dùng cho laptop AI Gateway khi demo | Tiết kiệm GPU VPS, nhanh set up |
| Human approval | Bắt buộc với task/deadline/assign | Tránh AI tự ghi sai dữ liệu |

---

## 4. Kiến trúc tổng thể đề xuất

```text
[User Browser]
   ↓ HTTPS/WebSocket
[VPS 8GB: QALY Web/API + SignalR + Workers]
   ├─ SQL Server: source of truth
   ├─ Redis: cache/queue/rate-limit
   ├─ Optional Qdrant: semantic index nhỏ/P1
   └─ AiGateway
        ├─ AI API Provider: primary for JSON extraction/reasoning
        ├─ Meetily Connector: import/sync meeting transcript/summary
        ├─ Ollama Provider: local fallback via ngrok/laptop
        └─ Mock Provider: deterministic QA/demo fallback

[Laptop RTX4060 + 16GB RAM]
   ├─ Meetily Desktop / Meetily Worker
   ├─ Ollama local model 7B/8B Q4 only when needed
   └─ Local AI Gateway exposed by ngrok during demo
```

---

## 5. Vai trò từng thành phần

### 5.1 Meetily

Dùng cho:

- ghi âm meeting;
- transcription realtime/local;
- summary meeting ban đầu;
- export/import transcript và summary vào QALY.

Không dùng Meetily để thay thế:

- quản lý user/organization/project;
- phân quyền QALY;
- quản lý task/workload;
- gợi ý assign task;
- semantic search toàn hệ thống.

### 5.2 AI API

Dùng làm primary cho các tác vụ cần chất lượng ổn định:

- extract action item/deadline/assignee từ transcript/chat;
- gợi ý task breakdown/checklist;
- gợi ý giao task có giải thích;
- tóm tắt tiến độ/sprint;
- sinh báo cáo tiếng Việt theo format Nhật;
- rewrite tin nhắn/báo cáo.

### 5.3 Ollama local

Dùng khi:

- phát triển offline;
- demo không muốn tốn API;
- tác vụ nhỏ: summarize ngắn, keyword, rewrite ngắn;
- fallback khi API lỗi.

Không cam kết Ollama local 14B chạy mượt 100% trên laptop 16GB RAM. Với RTX4060 phổ biến 8GB VRAM, target an toàn là model 7B/8B Q4 và context 4K–8K.

### 5.4 Qdrant

Dùng cho:

- tìm kiếm ngữ nghĩa trong meeting/task/chat/wiki;
- phát hiện task trùng/tương tự;
- truy xuất context cho project Q&A;
- memory dài hạn từ transcript/summary.

Không dùng Qdrant làm DB nghiệp vụ. Nếu mất Qdrant, hệ thống rebuild từ SQL Server/outbox.

---

## 6. Cấu hình khuyến nghị theo tài nguyên hiện có

### 6.1 Laptop RTX4060 + 16GB RAM

| Thành phần | Khuyến nghị |
|---|---|
| Meetily | Chạy chính cho meeting note |
| Ollama | Chạy 7B/8B Q4, không chạy 14B liên tục |
| Context | 4096–8192 tokens cho local |
| Concurrent AI job | 1 job tại một thời điểm |
| SQL Server/Qdrant | Không nên chạy cùng lúc với meeting AI nếu RAM căng |
| Browser/dev server | Đóng bớt app nặng khi demo AI |

Cấu hình Ollama local đề xuất:

```env
OLLAMA_CONTEXT_LENGTH=8192
OLLAMA_KEEP_ALIVE=5m
OLLAMA_MAX_LOADED_MODELS=1
OLLAMA_NUM_PARALLEL=1
OLLAMA_NO_CLOUD=1
```

Model gợi ý:

| Nhu cầu | Model local đề xuất | Ghi chú |
|---|---|---|
| extract/rewrite nhẹ | llama3.1:8b hoặc qwen2.5:7b | an toàn cho RAM/VRAM |
| tiếng Việt + JSON | qwen2.5:7b | ổn cho prompt có schema |
| embedding local | nomic-embed-text hoặc nomic-embed-text-v2-moe | dùng cho Qdrant |
| 14B | chỉ test riêng, không đặt làm yêu cầu demo | có thể spill CPU/RAM |

### 6.2 VPS 8GB từ trường

VPS 8GB **đủ cho web app demo**, không đủ để chạy LLM local mạnh.

| Service | Chạy trên VPS 8GB? | Ghi chú tối ưu |
|---|---:|---|
| Backend ASP.NET/Nest/Node | Có | giới hạn log, health check |
| Frontend static | Có | Nginx reverse proxy |
| SQL Server dev/small | Có nhưng cần giới hạn RAM | cap memory khoảng 2.5–3GB |
| Redis | Có | maxmemory 256–512MB |
| Qdrant small | Có nếu dataset nhỏ | cap RAM 512MB–1GB, on-disk nếu cần |
| Ollama 7B/14B | Không nên | dùng laptop/API |
| Meetily | Không phù hợp | desktop/local worker |

Docker resource limit gợi ý:

```yaml
services:
  sqlserver:
    environment:
      MSSQL_MEMORY_LIMIT_MB: 3072
    mem_limit: 3500m
  redis:
    mem_limit: 512m
  qdrant:
    mem_limit: 1024m
  backend:
    mem_limit: 1024m
  nginx:
    mem_limit: 128m
```

---

## 7. Phân tầng chức năng AI

### P0 — phải làm trong 10 tuần

1. Meetily-based meeting import/sync.
2. Extract keyword/action item/deadline/decision từ meeting.
3. Tóm tắt chat dài.
4. Extract task candidate từ chat.
5. Gợi ý giao task bằng hybrid scoring.
6. Chia nhỏ task + checklist/acceptance criteria.
7. Tóm tắt tiến độ project/sprint.
8. AI API Gateway + quota + fallback mock.

### P1 — nên làm nếu P0 ổn

9. Qdrant semantic search toàn project.
10. Phát hiện task trùng/tương tự.
11. Project Q&A có source/citation.
12. Import tài liệu Markdown/CSV/PDF text sang draft task.

### P2 — không nên cam kết trong 10 tuần

13. Full realtime transcription tự viết.
14. Speaker diarization chuẩn production.
15. Multi-tenant self-hosted Meetily server.
16. AI tự động assign task không cần xác nhận.
17. Sentiment/stress analytics nhạy cảm.

---

## 8. Luồng xử lý AI production/Japan-style

### 8.1 Nguyên tắc chung

- AI chỉ tạo **draft/suggestion**.
- Ghi DB thật phải qua người dùng xác nhận.
- Mọi request AI phải có tenant_id/project_id/user_id.
- Prompt không được chứa dữ liệu user không có quyền xem.
- Output JSON phải validate bằng schema.
- Lỗi AI không làm sập luồng nghiệp vụ chính.
- Có retry, timeout, audit log, quota, feature flag.

### 8.2 Luồng Meeting Note

```text
Meetily records/transcribes/summarizes
→ User exports/syncs to QALY
→ QALY validates meeting/project/participants
→ AiGateway extracts structured JSON
→ User reviews keyword/action items
→ User confirms create tasks/wiki decisions
→ QALY stores DB + optional Qdrant chunks
```

### 8.3 Luồng gợi ý giao task

```text
Task mới
→ Rule engine tính skill/workload/role/deadline/performance
→ Optional Qdrant tìm task/meeting tương tự
→ AiGateway giải thích top candidates
→ PM xác nhận assignee
→ Task assigned + audit log
```

Scoring deterministic:

```text
score = skill_match * 0.35
      + similar_experience * 0.25
      + availability * 0.20
      + role_fit * 0.10
      + recent_performance * 0.10
```

LLM chỉ giải thích, không quyết định hoàn toàn.

---

## 9. Chiến lược chi phí token

Để giảng viên không lo test ngốn token:

| Biện pháp | Cách làm |
|---|---|
| Mock provider | test E2E không gọi API thật |
| Prompt nhỏ | chỉ gửi context đã lọc, không gửi toàn bộ chat/project |
| Cache | hash(input+schema+model+version), TTL 1–24h |
| Batch/async | meeting/report chạy nền, không realtime nếu không cần |
| Model routing | task nhỏ dùng model rẻ, task khó dùng model mạnh |
| Quota | giới hạn user/project/day |
| Human review | không gọi lại AI nhiều lần vô nghĩa |
| Golden dataset | 20–30 input mẫu để test cố định |

Trong demo chính thức, có thể bật API key cho 5–10 flow quan trọng; các test lặp lại dùng mock/cache/local.

---

## 10. Health check và vận hành

| Component | Health check |
|---|---|
| Backend | `/health` |
| SQL Server | connect + simple query |
| Redis | PING |
| Qdrant | `/healthz` hoặc collection info |
| AI API | lightweight model request hoặc provider status |
| Meetily Connector | local heartbeat/sync status |
| Ollama | `/api/tags`, `ollama ps` local |
| Ngrok | tunnel URL + auth header test |

---

## 11. Kết luận bảo vệ với giảng viên

Câu trả lời chuẩn:

> Dự án không phụ thuộc cứng vào Qdrant + Ollama. Với 10 tuần, em chọn hướng Meetily-first cho meeting note vì Meetily đã giải phần audio/transcription khó và ít rủi ro hơn tự code. Với các tác vụ cần chất lượng ổn định khi bảo vệ, em dùng AI API qua AiGateway có quota/cache/mock để kiểm soát token. Ollama local chỉ là fallback/dev/offline, dùng model 7B/8B phù hợp laptop RTX4060 16GB RAM. Qdrant được giữ như semantic memory để tìm kiếm ngữ nghĩa và task similarity, có thể bật P1 và rebuild từ SQL Server, không phải nguồn dữ liệu chính. Cách này tối ưu nhất về thời gian, chi phí, tài nguyên và độ ổn định demo.

---

## 12. Nguồn tham khảo kỹ thuật

- Meetily GitHub: https://github.com/Zackriya-Solutions/meetily
- Meetily Architecture: https://github.com/Zackriya-Solutions/meetily/blob/main/docs/architecture.md
- Ollama context length: https://docs.ollama.com/context-length
- Ollama FAQ / keep_alive / host / no cloud: https://docs.ollama.com/faq
- Ollama GPU support: https://docs.ollama.com/gpu
- Ollama generate API structured output: https://docs.ollama.com/api/generate
- OpenAI API pricing: https://openai.com/api/pricing/
- OpenAI Batch API: https://developers.openai.com/api/docs/guides/batch
- Qdrant manage data/indexing/quantization: https://qdrant.tech/documentation/manage-data/
- ngrok OAuth/access control: https://ngrok.com/docs/guides/identity-aware-proxy/securing-with-oauth
