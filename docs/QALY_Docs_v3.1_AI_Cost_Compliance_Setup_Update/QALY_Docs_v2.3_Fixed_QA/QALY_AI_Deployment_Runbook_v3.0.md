# QALY Workspace — AI Deployment & Resource Runbook v3.0

**Ngày:** 19/05/2026  
**Mục tiêu:** chạy mượt trong điều kiện laptop RTX4060 + 16GB RAM và có thể có VPS 8GB từ trường.

---

## 1. Topology khuyến nghị cho 10 tuần

```text
Production-like Demo

Internet
  ↓
Nginx on VPS 8GB
  ↓
QALY Backend/API + SignalR
  ├─ SQL Server 2022 small/dev
  ├─ Redis
  ├─ Qdrant optional small/P1
  └─ AiGateway
       ├─ AI API primary
       ├─ Mock provider for test
       └─ ngrok → Laptop Local AI Gateway
                     ├─ Meetily
                     └─ Ollama 7B/8B Q4 fallback
```

---

## 2. Docker Compose resource limits gợi ý

```yaml
services:
  nginx:
    image: nginx:alpine
    mem_limit: 128m

  backend:
    image: qaly-backend:latest
    mem_limit: 1024m
    environment:
      AI_PROVIDER_PRIMARY: openai_compatible
      AI_PROVIDER_FALLBACK: mock,ollama_local
      AI_TIMEOUT_MS: 60000
      AI_DAILY_TOKEN_BUDGET: 300000

  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    mem_limit: 3500m
    environment:
      ACCEPT_EULA: Y
      MSSQL_MEMORY_LIMIT_MB: 3072

  redis:
    image: redis:7-alpine
    command: redis-server --maxmemory 384mb --maxmemory-policy allkeys-lru
    mem_limit: 512m

  qdrant:
    image: qdrant/qdrant:latest
    mem_limit: 1024m
    volumes:
      - qdrant_storage:/qdrant/storage
```

Nếu VPS 8GB bị thiếu RAM, ưu tiên tắt Qdrant và dùng SQL search trước. Qdrant bật lại cho demo semantic search P1.

---

## 3. Laptop Local AI Gateway

Không expose trực tiếp Ollama. Dùng gateway riêng:

```text
ngrok public URL
→ Local AI Gateway
→ auth token / rate limit / timeout
→ Meetily export/sync or Ollama local
```

`.env` VPS:

```env
AI_MEETILY_SYNC_URL=https://your-ai.ngrok.app
AI_MEETILY_SYNC_TOKEN=change-me
AI_LOCAL_OLLAMA_URL=https://your-ai.ngrok.app/ollama-proxy
AI_API_KEY=***
AI_PROVIDER_PRIMARY=openai_compatible
AI_PROVIDER_FALLBACK=mock,ollama_local
```

`.env` laptop:

```env
LOCAL_AI_PORT=8001
OLLAMA_HOST=http://127.0.0.1:11434
LOCAL_AI_SECRET=change-me
MAX_REQUEST_BODY_MB=5
AI_JOB_CONCURRENCY=1
```

---

## 4. Ollama local settings cho RTX4060 + 16GB RAM

```env
OLLAMA_CONTEXT_LENGTH=8192
OLLAMA_KEEP_ALIVE=5m
OLLAMA_MAX_LOADED_MODELS=1
OLLAMA_NUM_PARALLEL=1
OLLAMA_NO_CLOUD=1
```

Lệnh kiểm tra:

```bash
ollama ps
ollama list
```

Quy tắc demo:

- Chỉ load một model tại một thời điểm.
- Đóng game/IDE nặng khi chạy Meetily + Ollama.
- Tránh 14B trong demo chính nếu chỉ có 16GB RAM.
- Meeting note dùng Meetily; JSON extraction dùng AI API để ổn định.
- Nếu API lỗi, dùng mock/cached output để không vỡ demo.

---

## 5. AI API cost control

```text
Request → Cache lookup → Quota check → Provider route → Timeout → Validate JSON → Save draft
```

Bắt buộc:

- Cache response theo input hash.
- Không gọi API trong unit test mặc định.
- Dùng golden dataset cho 20–30 case.
- Meeting/report dài chạy async queue.
- Chỉ gửi context đã lọc, không gửi toàn bộ DB.
- Dùng model nhỏ/rẻ cho extract/rewrite; model mạnh chỉ dùng final demo hoặc task phức tạp.

---

## 6. Qdrant optimization

Nếu bật Qdrant trên VPS 8GB:

- collection nhỏ: `qaly_knowledge_chunks`;
- metadata tối thiểu: tenant_id, project_id, source_type, source_id, visibility, updated_at;
- payload index cho tenant_id, project_id, source_type, visibility;
- chunk 300–800 tokens;
- topK 5–8;
- rebuild từ SQL Server qua outbox;
- không lưu audio/raw file trong Qdrant.

---

## 7. Health check checklist trước demo

| Check | Pass criteria |
|---|---|
| `/health` backend | 200 OK |
| SQL Server | simple query OK |
| Redis | PING OK |
| Login demo accounts | PM/member/customer OK |
| Chat realtime | gửi/nhận OK |
| Meetily | tạo transcript/summary sample OK |
| AI API | extract JSON sample OK |
| Mock provider | bật được khi mất API |
| Qdrant optional | search sample OK |
| Ngrok | URL ổn định + auth header OK |

---

## 8. Điều kiện fallback

| Lỗi | Fallback |
|---|---|
| AI API quota/rate limit | Mock cached output hoặc Ollama local |
| Laptop tắt/ngrok lỗi | Manual import transcript file |
| Qdrant down | SQL search + disable semantic feature flag |
| Ollama chậm | Tắt local provider, dùng API |
| Meetily export lỗi | Upload transcript `.txt/.md/.json` thủ công |
