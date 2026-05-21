# QALY AI Production Strategy v3.1 - Meetily-first, API-primary, Cost-controlled

## 1. Quyết định mới

Sau phản hồi từ giảng viên, hướng triển khai AI được điều chỉnh từ **Qdrant + Ollama làm trục chính** sang:

```text
Meetily-first + AI API primary + Ollama fallback + Qdrant optional semantic memory
```

Nghĩa là:

- **Meetily** xử lý bài toán khó nhất của meeting: capture/audio/transcription/summary/export.
- **AI API key** là provider chính cho các tác vụ cần chất lượng ổn định khi demo/bảo vệ.
- **Ollama** chỉ dùng local dev, fallback offline, demo privacy-first hoặc xử lý prompt nhỏ.
- **Qdrant** không bị loại bỏ, nhưng chuyển thành P1 semantic memory/search, không chặn MVP.

## 2. Vì sao đổi hướng là đúng thực tế

### 2.1. Giảm risk kỹ thuật trong 10 tuần

Tự code audio/transcription từ đầu có risk cao: lỗi mic/system audio, lag realtime, sai format audio, lỗi GPU, sai timestamp, lỗi Windows/Linux. Meetily đã có sẵn pipeline meeting AI local-first nên dùng được ngay làm engine/connector.

### 2.2. Giảm risk chất lượng AI

Giảng viên không tin Ollama 14B đủ ổn cho mọi task là hợp lý. Với laptop RTX4060/16GB RAM, 7B/8B Q4 có thể chạy ổn hơn 14B. Tác vụ quan trọng nên gọi AI API để đảm bảo JSON extraction, reasoning và Vietnamese output.

### 2.3. Chi phí API không lớn nếu kiểm soát đúng

Dự án không cần gửi toàn bộ chat/meeting thô lên API liên tục. Chỉ gửi context đã lọc/tóm tắt, bật cache, mock mode, quota và golden dataset. Với khối lượng demo 10 tuần, ngân sách API base có thể giữ ở mức rất thấp.

## 3. Kiến trúc target

```text
User / Team
  ↓
QALY Web + Mobile
  ↓
QALY Backend API
  ├─ SQL Server: source of truth
  ├─ Redis/BullMQ: queue, cache, rate limit
  ├─ AI Gateway: provider router + quota + audit
  ├─ Qdrant: semantic memory optional P1
  └─ Meetily Connector
        ↓
Laptop Local Worker
  ├─ Meetily Desktop / Export
  ├─ Local AI Gateway
  ├─ Ollama 7B/8B fallback
  └─ ngrok/private tunnel for demo only
```

## 4. Provider strategy

| Layer | Primary | Fallback | Không dùng |
|---|---|---|---|
| Meeting capture/transcription | Meetily | OpenAI/Gemini transcription nếu Meetily lỗi | Tự code realtime STT P0 |
| JSON extraction | AI API | Ollama 7B/8B | Ollama 14B bắt buộc |
| Task recommendation | Rule-score + SQL | Qdrant similarity + AI explain | LLM tự quyết 100% |
| Semantic search | Qdrant P1 | SQL keyword search | Nhét toàn bộ context vào prompt |
| Demo/bảo vệ | Cached AI API | Mock result + Ollama | Live unbounded token usage |

## 5. Nguyên tắc AI production

1. AI chỉ tạo **draft/proposal**, không tự thay đổi dữ liệu quan trọng.
2. Mọi task/assignee/deadline do AI gợi ý phải qua **user confirm**.
3. Không gửi PII/raw secret lên AI API nếu không cần.
4. Tất cả request AI phải đi qua **AI Gateway**, không gọi provider trực tiếp từ client.
5. Mọi response dạng JSON phải validate schema trước khi lưu.
6. Có quota theo user/project/ngày.
7. Có cache prompt/result để test không tốn token.
8. Có mock provider cho test CI, demo offline, và fallback khi hết quota.
9. Có audit log cho provider, token, cost, input hash, output hash.
10. Không expose trực tiếp Ollama qua internet.

## 6. Scope theo 10 tuần

### P0 - bắt buộc hoàn thành

- AI Gateway provider router.
- Meetily import/sync transcript/summary.
- Extract keyword/action item/deadline/decision từ meeting.
- Chat summarization.
- Create task draft from chat/meeting.
- Task recommendation using rule-score + AI explanation.
- Task breakdown + acceptance checklist.
- Sprint/project progress summary.
- AI usage ledger + prompt cache + daily/monthly budget.
- Demo seed data + golden dataset.

### P1 - nếu P0 ổn

- Qdrant semantic search.
- Duplicate task detection.
- Project Q&A with sources.
- Risk-of-delay detection.

### P2 - roadmap

- Full realtime AI meeting inside QALY.
- Speaker diarization production.
- Multi-tenant enterprise AI admin.
- Advanced compliance automation.

## 7. Chốt cho giảng viên

Hệ thống không phụ thuộc vào một hướng duy nhất. Meetily giúp chứng minh open-source AI meeting assistant thực tế. AI API giúp đảm bảo chất lượng demo và giảm rủi ro model local. Ollama giữ vai trò local fallback để tiết kiệm và chứng minh privacy-first. Qdrant được dùng khi cần semantic memory/search, nhưng không chặn tiến độ MVP.
