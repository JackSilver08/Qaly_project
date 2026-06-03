# Quang Tuan Import, AI Review, Final Verification - 2026-06-03

## Scope

This note closes the remaining Quang Tuan ownership items from
`docs/task/2026-06-03_phan_cong_tuan_nay_7_thanh_vien.csv`:

- `QT-04` Import nang cao review
- `QT-05` Review AI nang cao
- `QT-07` Final integration verification, with current blockers recorded

## QT-04 - Import Review

Reviewed implementation areas:

- `src/Qaly.Application/Services/FileImportService.cs`
- `src/Qaly.Web/Controllers/ImportController.cs`
- `src/Qaly.Web/ClientApp/components/import/ImportUploadStep.vue`
- `src/Qaly.Web/ClientApp/components/import/ImportModal.vue`
- `tests/Qaly.UnitTests/ImportEnhancementTests.cs`

Decision:

- Import document currently supports Markdown, TXT, HTML, DOCX.
- ZIP bundle import supports supported child files and creates multiple Wiki pages.
- PDF and EPUB are intentionally left as roadmap formats in UI copy until parsers are ready.
- DOCX parser is minimal by design: heading/paragraph conversion, with warnings for skipped tables/images.

Acceptance evidence:

- Focused import tests passed as part of `ImportEnhancementTests`.
- Full unit/integration test run passed.

## QT-05 - AI Review And Blocker Fix

Reviewed implementation areas:

- `src/Qaly.Infrastructure/Services/AI/AiGateway.cs`
- `src/Qaly.Application/Services/ErumiChatService.cs`
- `src/Qaly.Application/Services/GroupAiService.cs`
- `src/Qaly.Web/ClientApp/pages/AnalyticsPage.vue`
- `src/Qaly.Web/ClientApp/components/chat/GroupAiPanel.vue`
- `tests/Qaly.UnitTests/ErumiChatServiceTests.cs`
- `tests/Qaly.UnitTests/GroupAiServiceTests.cs`
- `tests/Qaly.UnitTests/AiGatewayEvidenceTests.cs`

Blocker fixed:

- `AiGateway` fallback for `TextAnswer.v1` used to return `{"result": ...}`.
- `ErumiChatService` and Analytics UI expect a structured response with `reply`, `metrics`, `tables`, `charts`, `actions`, and `files`.
- The fallback now returns a UI-safe `TextAnswer.v1` JSON shape so provider outages do not break the analytics chat surface.
- Added regression test:
  - `ExecuteAsync_WhenProviderFailsForTextAnswer_ReturnsUiSafeTextAnswerFallback`

Remaining AI review note:

- `ErumiChatService` now goes through `IAiGateway` for analytics chat, with context, history, cache, compliance, budget, and usage ledger flow.
- `GroupAiService` still uses `IChatClient` directly for group summary/action item/draft project flows. It has JSON parsing and safe fallback behavior, but does not yet route those calls through `IAiGateway`. This is not a merge blocker for the current demo, but should be backlog if the team wants all AI usage to share budget/cache/audit controls.

## QT-07 - Verification Log

Commands run:

```powershell
dotnet test tests/Qaly.UnitTests/Qaly.UnitTests.csproj --filter "FullyQualifiedName~AiGatewayEvidenceTests|FullyQualifiedName~ImportEnhancementTests"
npm run build
dotnet test Qaly_project.slnx --no-restore
npm run typecheck
```

Results:

- Focused AI/import tests: passed, 25 total.
- Frontend production build: passed.
- Full .NET tests: passed, 180 unit tests and 18 integration tests.
- Frontend typecheck: passed.
- Docker infrastructure status: SQL Server, Redis, Seq, MailHog, Ollama, Qdrant running; SQL/Redis healthy.

E2E smoke:

- Attempted to start local `Qaly.Web` with SQL container connection on `localhost,1434`.
- The app did not become reachable at `http://127.0.0.1:5000/Account/Login` within 3 minutes, so Playwright was not executed.
- Last observed logs showed database activity during startup/seed/attention-signal processing, with no fatal stderr output.
- Status: documented environment/startup blocker, not a functional E2E pass.

## Task Mapping

- `QT-04`: Done.
- `QT-05`: Done, with AI fallback blocker fixed and regression test added.
- `QT-07`: Verification completed for build/unit/integration/typecheck; E2E startup blocker documented.
