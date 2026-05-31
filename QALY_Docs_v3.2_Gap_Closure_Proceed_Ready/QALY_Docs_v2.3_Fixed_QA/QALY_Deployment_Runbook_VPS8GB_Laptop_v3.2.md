# QALY Deployment Runbook v3.2 - VPS 8GB + Laptop RTX4060

## 1. Deployment target

```text
VPS 8GB:
- Nginx
- Frontend static/app
- Backend API
- SQL Server/PostgreSQL depending final stack
- Redis
- Optional Qdrant P1 only when RAM stable

Laptop RTX4060/16GB:
- Meetily desktop
- Local AI Gateway
- Ollama 7B/8B Q4 fallback
- ngrok/private tunnel for staging only

Cloud AI API:
- Primary provider for demo-quality AI
```

## 2. Environment files

### Backend `.env`

```env
NODE_ENV=production
APP_PORT=8080
DATABASE_URL=sqlserver://user:pass@db:1433/qaly
REDIS_URL=redis://redis:6379
JWT_SECRET=change_me
AI_GATEWAY_MODE=router
AI_DEFAULT_PROVIDER=openai
AI_FALLBACK_PROVIDER=mock
AI_DAILY_BUDGET_USD=2
AI_MONTHLY_BUDGET_USD=30
AI_ALLOW_CLOUD_FOR_SENSITIVE=false
OPENAI_API_KEY=from_secret_store
GEMINI_API_KEY=from_secret_store
LOCAL_AI_GATEWAY_URL=https://your-ngrok-domain.ngrok.app
LOCAL_AI_GATEWAY_TOKEN=change_me
QDRANT_ENABLED=false
```

### Local AI Gateway `.env`

```env
LOCAL_AI_PORT=8001
LOCAL_AI_TOKEN=change_me
OLLAMA_BASE_URL=http://127.0.0.1:11434
OLLAMA_MODEL=llama3.1:8b-instruct-q4_K_M
MAX_CONCURRENT_JOBS=1
MAX_PAYLOAD_MB=2
RATE_LIMIT_PER_MINUTE=20
```

## 3. VPS docker-compose template

```yaml
services:
  nginx:
    image: nginx:stable-alpine
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - ./nginx.conf:/etc/nginx/nginx.conf:ro
    depends_on:
      - backend

  backend:
    image: qaly/backend:latest
    env_file: .env
    ports:
      - "8080:8080"
    depends_on:
      - redis
      - db
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
      interval: 30s
      timeout: 5s
      retries: 3

  frontend:
    image: qaly/frontend:latest
    ports:
      - "3000:3000"
    depends_on:
      - backend

  redis:
    image: redis:7-alpine
    command: redis-server --maxmemory 256mb --maxmemory-policy allkeys-lru
    volumes:
      - redis-data:/data

  db:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      ACCEPT_EULA: "Y"
      MSSQL_SA_PASSWORD: "Change_this_password_123"
      MSSQL_MEMORY_LIMIT_MB: "3072"
    ports:
      - "1433:1433"
    volumes:
      - mssql-data:/var/opt/mssql

  # P1 only - enable after P0 stable
  # qdrant:
  #   image: qdrant/qdrant:latest
  #   ports:
  #     - "6333:6333"
  #   volumes:
  #     - qdrant-data:/qdrant/storage

volumes:
  redis-data:
  mssql-data:
  qdrant-data:
```

## 4. Healthcheck commands

```bash
curl -f http://localhost:8080/health
curl -f http://localhost:8080/ready
curl -f http://localhost:8080/api/ai/health
curl -f http://localhost:8080/api/ai/budget?project_id=1
```

## 5. Laptop local AI setup

```bash
ollama serve
ollama pull llama3.1:8b
npm run start:local-ai-gateway
ngrok http 8001
```

Rules:

- Không expose port `11434` trực tiếp.
- Chỉ expose Local AI Gateway.
- Bắt buộc Bearer token.
- Set max concurrent = 1 vì laptop 16GB RAM.
- Dùng API primary cho demo chính; local fallback chỉ dùng khi API lỗi/offline.

## 6. Backup script

### SQL Server backup

```bash
mkdir -p backups
STAMP=$(date +%Y%m%d_%H%M%S)
docker exec qaly-db /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -Q "BACKUP DATABASE [qaly] TO DISK = N'/var/opt/mssql/backup/qaly_$STAMP.bak' WITH INIT"
docker cp qaly-db:/var/opt/mssql/backup/qaly_$STAMP.bak backups/
```

### Restore drill

```bash
docker cp backups/qaly_demo.bak qaly-db:/var/opt/mssql/backup/qaly_demo.bak
docker exec qaly-db /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -Q "RESTORE DATABASE [qaly] FROM DISK = N'/var/opt/mssql/backup/qaly_demo.bak' WITH REPLACE"
```

## 7. Resource guard VPS 8GB

| Component | Limit |
|---|---:|
| SQL Server | 3GB |
| Redis | 256MB |
| Backend | 512MB-1GB |
| Frontend | 512MB |
| Qdrant P1 | disabled by default |
| Ollama | never on VPS 8GB |

Monitoring commands:

```bash
docker stats --no-stream
free -h
df -h
journalctl -u docker --since "1 hour ago"
```

## 8. Staging checklist

- [ ] `.env.production` filled without committing secrets.
- [ ] DB migration applied.
- [ ] Seed demo data restored.
- [ ] `/health`, `/ready`, `/api/ai/health` pass.
- [ ] AI budget set.
- [ ] Mock provider works.
- [ ] Cloud provider works with small test.
- [ ] Sensitive job blocked from cloud.
- [ ] Backup restore drill done.
- [ ] Evidence folder created.

## 9. Rollback plan

- Keep last stable Docker image tag.
- Keep last DB backup before migration.
- Disable P1 toggles by env.
- Switch AI provider to mock if API quota/provider fails.
- Use warm cache for defense demo.
