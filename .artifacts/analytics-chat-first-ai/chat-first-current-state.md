# Chat-First AI Analyst Cockpit - Current State

Generated: 2026-06-28

Scope: frontend/AI UX research and component planning only. No production code, migrations, or commits were changed.

## Design Read

This should remain a chat-first AI analyst cockpit for PM/project users. The visual language should be compact, operational, icon-led, and progressive. It must not become a full KPI dashboard.

## Preview Evidence

The in-app browser was already open at:

`http://localhost:5000/analytics`

Screenshots captured for this pass:

- `chat-first-before-desktop.png`
- `chat-first-before-tablet.png`
- `chat-first-before-mobile.png`

No "after" screenshot was created because this pass did not implement production UI changes.

## Files And Routes Found

Route:

- `src/Qaly.Web/ClientApp/router/index.ts`
  - `/analytics` maps to `AnalyticsPage`.

Page:

- `src/Qaly.Web/ClientApp/pages/AnalyticsPage.vue`
  - imports `ErumiChatPanel`
  - renders only `<ErumiChatPanel />`
  - no additional shell, toolbar, tabs, side drawer, model selector, or source drawer exists at page level.

Main chat component:

- `src/Qaly.Web/ClientApp/components/chat/ErumiChatPanel.vue`
  - used as full analytics page
  - also used as drawer through `FloatingChatbot.vue`
  - imports `lucide-vue-next`, `MarkdownIt`, `DOMPurify`, `Chart.js`, `vue-chartjs`

Floating chat:

- `src/Qaly.Web/ClientApp/components/chat/FloatingChatbot.vue`
  - hides on `/analytics`
  - renders `ErumiChatPanel :is-drawer="true"` elsewhere

Backend endpoints:

- `POST /api/ai/chat/fast`
  - `src/Qaly.Web/Controllers/AiController.cs`
  - calls `ErumiChatService.ChatFastAsync`
- `POST /api/ai/drafts/{draftId}/confirm`
  - calls `AiWorkflowService.ConfirmDraftAsync`

## Existing Chat Capabilities

`ErumiChatPanel.vue` already supports:

- chat-first empty state with avatar, welcome heading, prompt chips and centered composer
- workspace/project selector
- last refreshed text
- file attachment parsing
- slash commands
- suggested prompts based on route/context
- markdown rendering through `markdown-it` and `DOMPurify`
- API call to `/api/ai/chat/fast`
- message history
- assistant response card
- metric grid
- table card stack
- chart card grid
- actions
- files
- sources as plain string list
- confidence as percent label
- latency
- copy action
- markdown export action
- inline draft confirmation buttons

## Current Answer Contract

Frontend response type in `ErumiChatPanel.vue`:

```ts
type ErumiChatResponse = {
  reply: string
  metrics: ErumiMetric[]
  tables: ErumiTable[]
  charts: ErumiChart[]
  actions: ErumiAction[]
  files: ErumiFile[]
  sources: string[]
  confidence: number
  usedAi: boolean
  intent: string
  latencyMs: number
}
```

Backend DTO in `ErumiChatDtos.cs`:

```csharp
public sealed record ErumiChatResponseDto(
    string Reply,
    IReadOnlyList<ErumiMetricDto> Metrics,
    IReadOnlyList<ErumiTableDto> Tables,
    IReadOnlyList<ErumiChartDto> Charts,
    IReadOnlyList<ErumiActionDto> Actions,
    IReadOnlyList<ErumiFileDto> Files,
    IReadOnlyList<string> Sources,
    double Confidence,
    bool UsedAi,
    string Intent,
    int LatencyMs,
    string? ConfidenceReason = null);
```

Missing from current contract:

- `sourceRefs`
- `freshness`
- `model`
- `provider`
- `modelMode`
- `budgetStatus`
- `privacyStatus`
- `costEstimate`
- `cacheStatus`
- `confidence_reason` in frontend type

## Backend Behavior Notes

`ErumiChatService.ChatFastAsync` currently:

- rejects empty messages
- handles file uploads
- returns static greeting for greetings
- detects write intent and returns a draft confirmation action
- routes workspace/project requests to analytics-based response builders
- has AI Gateway execution methods for structured AI analytics chat, but current fast path often returns deterministic/local structured responses

`ParseStructuredAiResponse` can parse:

- `reply`
- `metrics`
- `tables`
- `charts`
- `actions`
- `files`
- `confidence`
- `confidence_reason`

It does not yet parse `sourceRefs`, `freshness`, model/provider metadata, budget metadata, or privacy metadata.

## Current Draft Flow

Current frontend path:

- assistant action type `draft_change`
- inline `Xác nhận` and `Hủy` buttons
- `handleDraftAction` calls `/api/ai/drafts/{draftId}/confirm`
- success appends a new assistant message

Important gap:

- `BuildWriteConfirmationResponse` currently creates an action payload with `{ message, projectId }`, not necessarily a persisted `draftId`.
- `handleDraftAction` requires `action.payload?.draftId`, so draft confirmation only works when the action came from a real persisted draft.

## UX Gaps

1. No model/provider selector.
2. No visible model status such as Live, Mock, Fallback, Budget blocked, Privacy blocked.
3. No compact AI toolbar.
4. No secondary tabs for Insights, Metrics, Sources, Reports, Actions, History.
5. No side drawer/bottom sheet for sources or audit/cost.
6. Sources are label-only and not actionable.
7. Confidence reason exists in backend but is not represented in frontend state/rendering.
8. Message cards are payload-type based, but not domain-intent based: no explicit risk card, source card, report preview card, action draft card family.
9. Tablet/mobile screenshots show right-side header clipping and mobile horizontal overflow risk.
10. Empty state is friendly but uses a large hero block; it can support a compact model/toolbar layer without losing chat-first intent.

## Design Constraints To Preserve

- Do not remove `ErumiChatPanel`.
- Do not break the existing input/composer.
- Do not convert `/analytics` into a dashboard-first page.
- Do not create direct write actions.
- Additive/refactor-light changes only.
- Keep current API compatibility.
- Use existing `lucide-vue-next` icon family because the app already standardizes on it.
