# QALY End-to-End Setup Guide v3.1

## 0. Mục tiêu môi trường

Dự án có 3 môi trường:

| Environment | Mục tiêu | AI mode | Ghi chú |
|---|---|---|---|
| Local Dev | code hằng ngày | Mock + Ollama optional | tránh tốn token |
| Demo/Staging | bảo vệ giảng viên | AI API primary + Meetily local | ổn định nhất |
| Production-like VPS | app chính | Backend/DB/Redis/Qdrant optional | không chạy LLM trên VPS 8GB |

## 1. Cấu hình máy cá nhân RTX4060/16GB RAM

### 1.1. Nguyên tắc chạy mượt

Không chạy tất cả cùng lúc trên laptop. Với 16GB RAM, ưu tiên:

```text
Meetily + Ollama 7B/8B + browser demo
```

Không nên đồng thời chạy:

```text
Meetily + Ollama 14B + Docker SQL Server + IDE nặng + browser nhiều tab
```

### 1.2. Phân bổ RAM khuyến nghị

| Thành phần | RAM target | Ghi chú |
|---|---:|---|
| Windows/OS | 3-4GB | không tránh được |
| Browser + IDE | 2-3GB | đóng tab thừa khi demo |
| Meetily | 1-3GB | tùy model transcription |
| Ollama 7B/8B Q4 | 5-8GB RAM/VRAM | chạy 1 model/lần |
| Docker Desktop | 0 hoặc tắt khi demo AI local | tránh ăn RAM |
| SQL Server local | không khuyến nghị khi demo AI | chuyển sang VPS |

### 1.3. Model local khuyến nghị

| Model local | Nên dùng? | Lý do |
|---|---|---|
| 7B/8B Q4 instruct | Có | ổn định hơn với 16GB RAM |
| 14B Q4 | Chỉ benchmark | dễ pressure RAM/VRAM |
| 14B Q8 | Không | quá nặng |
| embedding local nhỏ | Có thể | nếu bật Qdrant P1 |

### 1.4. Ollama environment

```bash
OLLAMA_CONTEXT_LENGTH=8192
OLLAMA_KEEP_ALIVE=5m
OLLAMA_MAX_LOADED_MODELS=1
OLLAMA_NUM_PARALLEL=1
OLLAMA_NO_CLOUD=1
```

### 1.5. Benchmark local trước khi commit dùng Ollama

1. Chạy `ollama ps` để kiểm tra model có chạy GPU không.
2. Test prompt JSON extraction 20 cases.
3. Đo latency P50/P95.
4. Nếu P95 > 20s hoặc JSON fail > 10%, không dùng Ollama cho demo chính.

## 2. VPS 8GB từ trường

### 2.1. Cài đặt base

OS khuyến nghị: Ubuntu 24.04 LTS.

```bash
sudo apt update && sudo apt upgrade -y
sudo apt install -y curl git ufw ca-certificates gnupg nginx
```

Firewall:

```bash
sudo ufw allow OpenSSH
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp
sudo ufw enable
sudo ufw status
```

Docker:

```bash
curl -fsSL https://get.docker.com | sudo sh
sudo usermod -aG docker $USER
newgrp docker
```

### 2.2. Không chạy Ollama trên VPS 8GB

VPS 8GB dùng cho app chính, không dùng chạy LLM. Lý do:

- Không có GPU.
- RAM 8GB phải chia cho SQL Server/backend/Redis/Nginx/Qdrant.
- LLM CPU sẽ chậm và dễ làm app chính thiếu RAM.

### 2.3. Phân bổ tài nguyên VPS 8GB

| Service | RAM limit | CPU limit | Bắt buộc? |
|---|---:|---:|---|
| Nginx | 128MB | 0.25 CPU | Có |
| Backend API | 768MB-1GB | 1 CPU | Có |
| Frontend static | qua Nginx | thấp | Có |
| SQL Server | 2.5-3GB | 1.5 CPU | Có nếu dùng SQL Server |
| Redis | 256MB | 0.25 CPU | Có cho queue/cache |
| Qdrant | 512MB-1GB | 0.5 CPU | Optional P1 |
| OS reserve | 1GB+ | - | Bắt buộc |

Nếu RAM > 80% liên tục: tắt Qdrant P1 trước, không tắt SQL/backend.

### 2.4. docker-compose production-like

```yaml
services:
  backend:
    image: qaly-backend:latest
    restart: unless-stopped
    env_file: .env.production
    mem_limit: 1024m
    depends_on:
      - sqlserver
      - redis

  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    restart: unless-stopped
    environment:
      ACCEPT_EULA: "Y"
      MSSQL_PID: "Developer"
      MSSQL_SA_PASSWORD: "${MSSQL_SA_PASSWORD}"
    mem_limit: 3072m
    volumes:
      - sql_data:/var/opt/mssql

  redis:
    image: redis:7-alpine
    restart: unless-stopped
    command: redis-server --maxmemory 256mb --maxmemory-policy allkeys-lru
    mem_limit: 300m

  qdrant:
    image: qdrant/qdrant:latest
    restart: unless-stopped
    mem_limit: 1024m
    profiles: ["p1-vector"]
    volumes:
      - qdrant_data:/qdrant/storage

volumes:
  sql_data:
  qdrant_data:
```

## 3. AI API setup

### 3.1. `.env.production`

```env
AI_PROVIDER_PRIMARY=openai
AI_PROVIDER_FALLBACK=mock
AI_ENABLE_OLLAMA=false
AI_ENABLE_QDRANT=false
AI_DAILY_BUDGET_USD=2
AI_MONTHLY_BUDGET_USD=20
AI_MAX_INPUT_TOKENS=8000
AI_MAX_OUTPUT_TOKENS=1500
AI_CACHE_ENABLED=true
AI_MOCK_ENABLED=true
OPENAI_API_KEY=***
GEMINI_API_KEY=***
```

### 3.2. Không gọi API từ frontend

Frontend chỉ gọi:

```http
POST /api/ai/jobs
GET /api/ai/jobs/{id}
POST /api/ai/drafts/{id}/confirm
```

Backend/AI Gateway mới gọi provider.

### 3.3. Budget guard

Pseudo-flow:

```text
receive AI job
→ check user/project permission
→ normalize input
→ hash prompt
→ return cache if exists
→ estimate tokens and cost
→ reject if over budget
→ call provider
→ validate JSON schema
→ save usage ledger
→ return draft
```

## 4. Meetily local + ngrok setup

### 4.1. Không expose Ollama trực tiếp

Sai:

```bash
ngrok http 11434
```

Đúng:

```text
ngrok → Local AI Gateway → Ollama/Meetily export reader
```

### 4.2. Local AI Gateway `.env`

```env
LOCAL_AI_PORT=8001
LOCAL_AI_SECRET=long-random-secret
OLLAMA_URL=http://127.0.0.1:11434
MEETILY_EXPORT_DIR=C:\Users\<you>\Documents\Meetily\exports
MAX_PAYLOAD_MB=10
RATE_LIMIT_PER_MINUTE=30
```

### 4.3. Chạy tunnel

```bash
ngrok http 8001
```

Nếu có static domain:

```bash
ngrok http --domain=<your-domain>.ngrok.app 8001
```

### 4.4. VPS gọi Local AI Gateway

```env
LOCAL_AI_GATEWAY_URL=https://<your-domain>.ngrok.app
LOCAL_AI_GATEWAY_SECRET=long-random-secret
LOCAL_AI_TIMEOUT_MS=60000
```

Header bắt buộc:

```http
Authorization: Bearer <LOCAL_AI_GATEWAY_SECRET>
```

## 5. Qdrant setup optional P1

Chỉ bật sau khi P0 ổn.

```bash
docker compose --profile p1-vector up -d qdrant
```

Collection đề xuất:

| Collection | Dữ liệu |
|---|---|
| `qaly_meeting_chunks` | transcript/summary chunks |
| `qaly_task_chunks` | task title/description/comments |
| `qaly_member_skill_evidence` | evidence skill từ task/meeting |
| `qaly_document_chunks` | file/document text |

Field cần payload index:

- `project_id`
- `tenant_id`
- `source_type`
- `source_id`
- `created_at`
- `member_id`
- `skills`

## 6. Demo checklist

Trước ngày bảo vệ:

1. Seed data đầy đủ project/member/task/chat.
2. Meetily có sẵn 1-2 transcript thật hoặc transcript demo.
3. API cache đã warm cho 10 use case chính.
4. Mock provider bật sẵn nếu mất mạng/API quota.
5. Không chạy Ollama 14B khi demo nếu chưa benchmark ổn.
6. VPS RAM dưới 75% trước khi demo.
7. Backup database trước demo.
8. Test ngrok tunnel 30 phút trước demo.
9. Có script demo offline nếu API lỗi.
10. Có screenshot/log chi phí AI usage ledger.
