# Release Readiness

> Cập nhật: 08/06/2026

## Trạng thái tự động

| Gate | Kết quả local |
| --- | --- |
| Unit tests | 214/214 pass |
| Integration tests | 24/24 pass |
| Unit line coverage | 44,02% (gate 43%) |
| Integration line coverage | 16,50% (gate 16%) |
| Playwright E2E smoke | 7/7 pass |
| Frontend typecheck | Pass |
| Frontend production build | Pass |
| Docker Compose validation | Pass |
| Configuration safety check | Pass |
| NuGet vulnerability scan | Pass, including transitive packages |
| npm production dependency audit | Pass, 0 vulnerabilities |
| Production container build | Pass, chạy non-root UID 1654 |
| Production container startup | Pass, `/health` trả HTTP 200 |
| Local Ollama provider smoke | Pass với `llama3.2:1b` |
| Local release/rollback rehearsal | Pass, candidate và rollback baseline đều healthy |

## Gate bắt buộc trước merge

- Vue typecheck và production build.
- Bundle trong `src/Qaly.Web/wwwroot/dist` phải khớp source.
- .NET Release build, unit tests và coverage baseline.
- Integration tests và coverage baseline.
- Playwright E2E smoke.
- Configuration safety và Docker Compose validation.
- Production Docker image build.

Coverage loại generated source và EF migrations qua `coverlet.runsettings`; code nghiệp vụ và DTO vẫn nằm trong phép đo.

## Phạm vi đã có regression

- Meeting LiveKit token/fallback.
- Meeting realtime join/leave, reconnect và disconnect cleanup.
- Import DOCX/ZIP và ZIP lỗi.
- Auth boundary, project permissions, wiki visibility và meeting action items.

## Việc cần xác nhận thủ công

- Hai browser thật hiển thị participant meeting đúng sau reconnect.
- Screen share trên browser hỗ trợ và thông báo trên browser không hỗ trợ.
- AI cloud provider bằng credential của môi trường demo/production. Ollama local đã được xác minh.
- Backup/restore database trên hạ tầng triển khai thật.

## Giới hạn phát hành hiện tại

CD hiện publish production image lên GHCR nhưng chưa deploy tự động. Local release/rollback rehearsal đã có script và evidence; chưa thể đánh dấu production-ready hoàn chỉnh cho đến khi có:

1. Target staging/production.
2. Secret store của môi trường.
3. Health check và rollback được chạy trên target thật.
