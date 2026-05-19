# QALY AI Cost Estimation v3.1

> Lưu ý: Giá API/VPS thay đổi theo thời gian. Bảng này dùng giá tham chiếu tại ngày 19/05/2026 và phải kiểm tra lại trước khi mua/triển khai thật.

## 1. Giả định workload 10 tuần

| Nhóm tác vụ | Tần suất ước tính | Input/request | Output/request | Tổng input | Tổng output |
|---|---:|---:|---:|---:|---:|
| Meeting extract | 20 meetings | 6,000 tokens | 1,000 tokens | 120,000 | 20,000 |
| Chat summary | 200 summaries | 3,000 | 500 | 600,000 | 100,000 |
| Task recommendation | 300 requests | 1,500 | 500 | 450,000 | 150,000 |
| Task breakdown/checklist | 500 requests | 1,000 | 400 | 500,000 | 200,000 |
| Sprint/project report | 20 reports | 6,000 | 1,000 | 120,000 | 20,000 |
| Project Q&A/search P1 | 500 requests | 2,000 | 700 | 1,000,000 | 350,000 |
| **Tổng base** | 1,540 AI calls |  |  | **2,790,000** | **840,000** |

## 2. Ước tính API cost

### 2.1. OpenAI tham chiếu

| Model class | Input $/1M | Output $/1M | Base cost | 3x safety | Ghi chú |
|---|---:|---:|---:|---:|---|
| gpt-5.4-nano | 0.20 | 1.25 | ~1.61 USD | ~4.83 USD | Dùng cho draft/extract đơn giản |
| gpt-5.4-mini | 0.75 | 4.50 | ~5.87 USD | ~17.61 USD | Khuyến nghị chính cho demo |
| gpt-5.4 | 2.50 | 15.00 | ~19.58 USD | ~58.73 USD | Chỉ dùng cho cases khó |

OpenAI pricing tham chiếu: gpt-5.4-mini 0.75 USD input và 4.50 USD output mỗi 1M tokens; gpt-5.4-nano 0.20/1.25; gpt-5.4 2.50/15.00. Transcription gpt-4o-mini-transcribe khoảng 0.003 USD/phút.

### 2.2. Gemini tham chiếu

| Model class | Input $/1M | Output $/1M | Base cost | 3x safety | Ghi chú |
|---|---:|---:|---:|---:|---|
| Gemini Flash-Lite | 0.10 | 0.40 | ~0.62 USD | ~1.85 USD | Rất rẻ cho tóm tắt/extract |
| Gemini Flash | 0.30 | 2.50 | ~2.94 USD | ~8.82 USD | Cân bằng chất lượng/chi phí |
| Gemini Pro tier | 0.625+ | 5.00+ | ~5.94 USD | ~17.83 USD | Dùng khi cần context lớn |

## 3. Meeting transcription cost nếu không dùng Meetily

Nếu Meetily lỗi hoặc cần cloud transcription:

| Khối lượng | Phút | OpenAI mini transcribe 0.003 USD/phút | Ghi chú |
|---|---:|---:|---|
| Base | 20 meeting x 60 phút | 1,200 phút = 3.60 USD | chỉ dự phòng |
| High | 60 meeting x 60 phút | 3,600 phút = 10.80 USD | vẫn không quá cao |

## 4. VPS/server cost

| Phương án | Cost 10 tuần | Mô tả | Khuyến nghị |
|---|---:|---|---|
| Trường cấp VPS 8GB | 0 VND | Backend + DB + Redis + Qdrant nhỏ | Nên dùng |
| DigitalOcean basic VPS | ~10-50 USD/tháng tùy cấu hình | SSD, public IP, tự quản trị OS | backup nếu trường không cấp |
| Qdrant Cloud free | 0 USD | 1GB RAM + 4GB disk, phù hợp prototype | dùng P1 nếu không muốn self-host |
| ngrok Free/Hobbyist | 0 USD hoặc trả thêm nếu vượt quota | Tunnel laptop local AI gateway | chỉ dùng demo/staging |
| AI API budget | 5-60 USD/10 tuần | tùy model và số lần gọi thật | đặt hard budget 20-30 USD trước |

## 5. Tổng ngân sách khuyến nghị

| Scenario | Server | AI API | Tunnel | Dự phòng | Tổng |
|---|---:|---:|---:|---:|---:|
| Minimum | 0 | 5 USD | 0 | 5 USD | ~10 USD |
| Base recommended | 0 | 20 USD | 0-8 USD | 15 USD | ~35-43 USD |
| Safe demo | 0-50 USD | 30 USD | 8 USD | 30 USD | ~68-118 USD |
| Worst manageable | 50-100 USD | 60 USD | 8-20 USD | 50 USD | ~168-230 USD |

## 6. Cost control bắt buộc

1. `AI_MONTHLY_BUDGET_USD=20` cho giai đoạn dev.
2. `AI_DAILY_BUDGET_USD=2` để tránh cháy tiền.
3. Bật prompt cache theo hash.
4. Bật mock provider trong test và seed demo.
5. Không gọi AI khi input dưới ngưỡng có thể xử lý rule-based.
6. Không gửi transcript thô toàn bộ nếu đã có summary/chunk liên quan.
7. Tách model: nano/flash-lite cho draft, mini/flash cho JSON extraction, model mạnh chỉ dùng khi user bấm “Improve”.
8. Log usage ledger theo user/project/provider/model.
9. Dùng retry tối đa 1 lần cho AI API.
10. Có circuit breaker nếu provider lỗi/hết quota.

## 7. Kết luận chi phí

Với cách triển khai v3.1, lo ngại “test ngốn quá nhiều token” được xử lý bằng cache, mock, quota và workflow review. Chi phí API thực tế cho đồ án 10 tuần có thể giữ dưới 20-30 USD nếu không gọi API bừa bãi. VPS 8GB trường cấp đủ cho app chính, còn laptop RTX4060 chạy Meetily/Ollama fallback.
