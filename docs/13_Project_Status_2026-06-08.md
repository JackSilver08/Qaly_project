# Qaly Project Status

> Đánh giá ngày 08/06/2026 sau các phase ổn định hóa.

## Mức độ hoàn thiện ước tính

| Góc nhìn | Hoàn thiện |
| --- | ---: |
| MVP/demo có kiểm chứng | 94% |
| Toàn bộ phạm vi repository | 88% |
| Production readiness | 80% |

Đây là ước tính kỹ thuật dựa trên code, test, CI/CD và evidence hiện có; không phải phần trăm theo số dòng code.

## Phần đã chắc chắn

- Backend/frontend build pass.
- Unit 214/214 và integration 24/24 pass.
- Playwright smoke 7/7 pass.
- Meeting SignalR presence/reconnect có regression.
- Import DOCX/ZIP và nhánh lỗi ZIP có regression.
- Production Docker image build pass và chạy non-root.
- Production container khởi động thành công, `/health` trả HTTP 200.
- Ollama local đã smoke test thật với model `llama3.2:1b`.
- Quy trình candidate/rollback local đã được diễn tập bằng image tag.
- NuGet transitive scan và npm production audit không có advisory.
- Secrets LiveKit không còn nằm trong tracked configuration.

## Phần còn thiếu

- Rotate LiveKit credential đã từng xuất hiện trong lịch sử Git.
- Nghiệm thu media meeting/screen share với LiveKit thật và browser headful.
- Nghiệm thu AI cloud với credential/mô hình của môi trường đích; local Ollama đã hoàn tất.
- Target staging/production thật và chạy lại health check/rollback tại target đó.
- Tiếp tục nâng coverage ở các service chưa có test; baseline đã chuẩn hóa thành unit 44,02% và integration 16,50%.

## Kết luận

Dự án đã vượt mức demo/MVP cơ bản và có regression tốt cho các luồng chính. Điểm ngăn production-ready hoàn chỉnh hiện nằm chủ yếu ở hạ tầng triển khai thật, secret rotation, media/AI provider evidence và coverage, không phải ở việc thiếu skeleton hay CRUD cốt lõi.
