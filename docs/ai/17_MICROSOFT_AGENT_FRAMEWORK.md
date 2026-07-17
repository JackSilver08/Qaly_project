# Microsoft Agent Framework trong Qaly

Qaly tích hợp `Microsoft.Agents.AI` qua một adapter ở Infrastructure. Luồng Erumi
hiện tại vẫn là fallback, vì vậy có thể bật/tắt agent mà không đổi API frontend.

## Bật agent

Thiết lập bằng environment variables:

```text
Ai__AgentFramework__Enabled=true
Ai__AgentFramework__TimeoutSeconds=45
```

Agent hiện dùng `IChatClient` được đăng ký cho Ollama. Đảm bảo Ollama chạy và đã
có model được khai báo tại `Ai:ChatModel` trước khi bật. Nếu agent lỗi hoặc hết
thời gian, Erumi tự động gọi `AiGateway` như trước.

## Ranh giới an toàn

- Chỉ request có `mode: agent` mới đi qua Agent Framework.
- Greeting, file preview và write confirmation vẫn được xử lý xác định.
- Tool được tạo theo request sau khi kiểm tra quyền project/member.
- Tool ghi dữ liệu vẫn phải trả draft để người dùng xác nhận.
- Mỗi agent run có timeout từ 5 đến 120 giây.
- Không đưa `DbContext` trực tiếp cho agent; agent chỉ dùng Application tools.

## Kiểm tra

```powershell
npm run typecheck
npm run build
dotnet build Qaly_project.slnx -c Release
dotnet test tests/Qaly.UnitTests/Qaly.UnitTests.csproj -c Release --no-build --filter "FullyQualifiedName~ErumiChatServiceTests|FullyQualifiedName~AiGatewayRouterTests|FullyQualifiedName~AiSecurityGuardTests"
```

## Mở rộng tiếp theo

1. Persist `AgentSession` theo user/conversation thay vì chỉ gửi lịch sử gần nhất.
2. Thêm OpenTelemetry span cho agent run và từng tool call.
3. Tách tool đọc và tool tạo draft thành các registry riêng.
4. Chỉ thêm workflow/multi-agent sau khi single-agent có evaluation ổn định.

## AI-native task planning

Erumi now supports a prompt-only task workflow:

1. The user describes a goal in the analytics chat.
2. Microsoft Agent Framework reads selected project context and produces 1-8 structured tasks.
3. Qaly persists an `AiJob` and an `AiGeneratedDraft`.
4. The chat presents confirm/reject controls.
5. Only confirmation invokes the existing task workflow, permission checks, validation, numbering, and audit path.

The planning agent receives no mutation tools. Invalid model output and provider failures fall back to deterministic draft generation. Local development enables Agent Framework by default; production remains opt-in through `Ai__AgentFramework__Enabled=true`.

## Durable agent runs

Autonomous write requests are checkpointed in the existing `AiJobQueue` table. No additional database migration is required.

- `POST /api/ai/agent-runs` starts a run from a project goal.
- `GET /api/ai/agent-runs/{runId}` returns progress and run events.
- `POST /api/ai/agent-runs/{runId}/approve` resumes or rejects an approval-gated run.

The lifecycle is `planning -> awaiting_approval -> running -> succeeded|failed|canceled`. Read-only analysis continues automatically through scoped tools. Mutations remain approval-gated and execute through `IAiWorkflowService`.
