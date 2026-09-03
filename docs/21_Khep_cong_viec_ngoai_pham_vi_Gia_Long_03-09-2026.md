# Biên bản khép công việc ngoài phạm vi Gia Long — 03/09/2026

## Phạm vi

Biên bản này khép các bằng chứng sản phẩm còn thiếu của Chí Khang, Huỳnh Quốc Bảo, Viết Minh và các gate chung phục vụ demo. Theo chỉ đạo, không sửa hoặc nhận hoàn thành thay các mục của Gia Long: GL-2, GL-4, GL-5, GL-6, GL-7 và G-4.

Không thay đổi lịch sử người đã trực tiếp thao tác. “Xong” trong biên bản này nghĩa là tiêu chí sản phẩm/gate demo có evidence tái lập được, không phải giả chữ ký hoặc ảnh bàn giao cá nhân.

## Kết quả theo phạm vi

| Phạm vi | Kết quả | Evidence |
| --- | --- | --- |
| CK-5 | PASS | AI Native P01–P27 PASS và targeted browser replay đã ghi trong runbook; full regression 03/09 PASS. |
| CK-6 | PASS | Provider/model/fallback truth có automated và targeted replay evidence; full regression 03/09 PASS. |
| QB-1–QB-6 | PASS | Deliverable và focused test đã đủ; full regression 03/09 PASS. |
| VM-6 | PASS | Realtime/chat/poll/wiki, P18–P24 và canonical read-back có acceptance evidence; full regression 03/09 PASS. |
| G-1 | PASS_DEMO_GATE | Regression chéo toàn bộ module ở backend và frontend đều xanh. Vòng ký nhận bốn cá nhân không bị giả lập; phần phụ thuộc Gia Long nằm ngoài phạm vi. |
| G-5 | PASS_DEMO_GATE | P01–P27 PASS; P28 `EXTERNAL_DEFERRED_VERIFIED`; disposition `DEMO_READY_THESIS`. Không coi external adapter chưa tích hợp là thành công. |

## Verification cùng lượt trước khi push

| Gate | Kết quả |
| --- | --- |
| `dotnet test Qaly_project.slnx -c Release --nologo` | PASS — Unit 821/821, Integration 331/331, WebFeature 49/49 |
| `npm run test:unit` | PASS — 270/270 |
| `npm run typecheck` | PASS |
| `npm run build` | PASS — 4044 modules transformed |
| `scripts/check-configuration.ps1` | PASS |
| `scripts/check-config-parity.ps1` | PASS |
| `scripts/check-security-hygiene.ps1` | PASS |
| `docker compose config --quiet` | PASS |

Chromium/preview không được chạy trong lượt này. Browser evidence được kế thừa từ targeted replay đã ghi ngày 02/09; không suy diễn thành kiểm thử production.

## Phần giữ lại cho Gia Long

- GL-2: ổn định full Playwright khi chạy song song.
- GL-4: hoàn tất theme GitHub UI.
- GL-5: audit accessible name cho Privacy/Organization/User.
- GL-6: bổ sung frontend unit cho GitHub API.
- GL-7: checklist thủ công/live GitHub App.
- G-4: cập nhật `QA_LOG.md` theo ownership đã giao.

Các mục này không chặn kết luận `DEMO_READY_THESIS`, nhưng vẫn chặn việc ghi toàn bộ kế hoạch bốn người đã hoàn thành 100%.
