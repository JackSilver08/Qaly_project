# Chat-First UI Plan

Generated: 2026-06-28

## Product Direction

Keep `/analytics` as the Erumi AI analyst surface. The user should still feel they are talking to a modern AI assistant. The added analytics power should appear as compact controls, message cards, drawers, chips and tabs around the chat.

This is not a dashboard replacement.

## Layout Principle

Primary surface:

- Chat composer
- Project selector
- Prompt chips
- Assistant responses

Secondary surfaces:

- model/provider selector
- compact icon toolbar
- mini tabs
- source drawer
- report/action draft drawer
- metadata chips

## Proposed Page Structure

`AnalyticsPage.vue` should remain a thin shell, not a heavy dashboard page.

```text
AnalyticsPage
└─ ChatFirstAnalyticsShell
   ├─ ErumiChatPanel as main surface
   ├─ AiModelSelector
   ├─ AiQuickToolbar
   ├─ AnalyticsMiniTabs
   ├─ AnalyticsSideDrawer
   ├─ SourceRefsDrawer
   └─ AiDraftReviewModal or inline draft review
```

Short-term alternative:

- Add props/slots to `ErumiChatPanel` for toolbar, model selector and drawers.
- Keep `AnalyticsPage.vue` as wrapper.
- Avoid moving composer logic until behavior tests exist.

## Desktop Wireframe

```text
┌──────────────────────────────────────────────────────────────────────────┐
│ App header                                                               │
├──────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│      [Model: Fast Mock ▾] [Live chip] [Cost ok] [Privacy local]          │
│                                                                          │
│      Erumi avatar                                                        │
│      Hôm nay Erumi có thể giúp gì cho bạn?                               │
│                                                                          │
│      [Đánh giá hiệu suất] [Dự án nào rủi ro?] [So sánh dự án]           │
│                                                                          │
│      [Project selector] [Fresh 19:30]                                    │
│      ┌────────────────────────────────────────────────────────────┐      │
│      │ Ask anything...                                      Send  │      │
│      └────────────────────────────────────────────────────────────┘      │
│                                                                          │
│      Icon toolbar: Insights Metrics Risks Sources Report Actions Model   │
│                                                                          │
│                                ┌────────────────────────────────────┐    │
│                                │ Drawer when opened                 │    │
│                                │ sources, report preview, settings  │    │
│                                └────────────────────────────────────┘    │
└──────────────────────────────────────────────────────────────────────────┘
```

## Active Chat Wireframe

```text
Assistant response
┌───────────────────────────────────────────────────────────────┐
│ Erumi             System data · 92% · Fresh 19:30 · Fast Mock │
│                                                               │
│ Markdown answer                                               │
│                                                               │
│ [Risk card] [Metric card] [Source card]                       │
│ [Chart card if present]                                       │
│                                                               │
│ Tiny actions: Why? Sources Ask follow-up Copy Draft Report    │
└───────────────────────────────────────────────────────────────┘
```

## Mobile Wireframe

```text
┌──────────────────────────────┐
│ App header compact           │
├──────────────────────────────┤
│ Model Fast ▾  Live  Fresh    │
│ Erumi avatar                 │
│ Hôm nay Erumi...             │
│ prompt chips scroll row      │
│ project selector             │
│ compact composer             │
│ toolbar icons scroll row     │
├──────────────────────────────┤
│ Bottom sheet when opened     │
│ Chat | Insights | Sources    │
└──────────────────────────────┘
```

Mobile rules:

- No fixed-width composer.
- Header user info collapses to avatar/menu.
- Prompt chips horizontal scroll with snap.
- Toolbar horizontal scroll with icon buttons.
- Source drawer becomes bottom sheet.
- Long project names truncate to one line.
- Freshness text becomes chip.

## Model Selector

Component: `AiModelSelector`

Compact display examples:

- `Model: Fast`
- `Mock`
- `Fallback`
- `Privacy blocked`
- `Budget blocked`

Do not hard-code DeepSeek as a live provider until backend has that provider. Use a registry abstraction:

```ts
type AiModelOption = {
  id: string
  label: string
  provider: 'mock' | 'local' | 'openai' | 'gemini' | 'deepseek' | 'custom'
  mode: 'fast' | 'deep' | 'fallback' | 'mock'
  enabled: boolean
  status: 'live' | 'mock' | 'fallback' | 'budget_blocked' | 'privacy_blocked'
}
```

Initial local registry can be UI-only:

- Fast: current `/api/ai/chat/fast`
- Mock: current offline/mock behavior
- Deep: planned, disabled until backend contract exists
- Provider: planned provider registry

## Mini Toolbar

Use icon-only buttons with short tooltips. Existing icon family is `lucide-vue-next`.

Toolbar items:

| Item | Icon idea | Opens | First behavior |
|---|---|---|---|
| Insights | Sparkles or Lightbulb | Insights tab | Sends or primes "Tóm tắt insight chính" |
| Metrics | BarChart3 | Metrics tab | Shows latest metrics payload cards |
| Risks | AlertTriangle | Risks tab | Sends or primes risk prompt |
| Sources | BookOpen or Link | Source drawer | Shows sources/sourceRefs |
| Report | FileText | Reports tab | Report preview/draft flow |
| Checklist | ListChecks | Actions tab | Draft checklist prompt |
| Assignee | UserRound | Actions tab | Assignee recommendation prompt |
| Cost/Audit | ShieldCheck or ReceiptText | History tab | Shows budget/audit metadata |
| Model Settings | SlidersHorizontal | Model settings | Opens model selector drawer |

No toolbar action should navigate away from `/analytics`.

## Mini Tabs

Tabs:

- Chat
- Insights
- Metrics
- Sources
- Reports
- Actions
- History

Desktop:

- Tabs can appear inside right drawer when drawer is opened.
- Chat remains centered and primary.

Tablet/mobile:

- Segmented control in drawer/bottom sheet.
- Never create a horizontal page overflow.

## Message Card Families

Current cards are payload-type based. Add domain-intent skins on top:

1. Risk Card
   - severity
   - affected project/task
   - due date or blocker
   - action buttons: Why, Sources, Draft plan

2. Metric Card
   - label/value/tone/hint
   - action buttons: Why, Trend, Copy

3. Source Card
   - source type
   - label
   - source evidence
   - open/copy action

4. Action Card
   - suggested action
   - draft state
   - confirm/reject if persisted draft

5. Report Preview Card
   - title
   - summary bullets
   - export/copy actions

6. Chart/Table Card
   - existing chart/table renderer
   - add compact source and "Ask about this" actions

## Metadata Chips

Assistant footer should evolve from raw metadata text to compact chips:

- response time
- confidence
- confidence reason tooltip
- source count
- freshness
- model/provider
- cache or live status
- budget/privacy status

Example:

```text
92% tin cậy · Fresh 19:30 · Fast Mock · 320ms · 4 nguồn
```

## Source Drawer

`SourceRefsDrawer` should support both current `sources: string[]` and future `sourceRefs`.

For current API:

- show source labels as simple chips
- explain "Nguồn hiện là nhãn hệ thống, chưa có link chi tiết"

For future API:

- show grouped source refs by type
- include title, evidence, URL, timestamp and confidence
- allow copy source list

## Draft Review

Short term:

- Improve existing inline draft card with clearer "draft only" label.
- Disable confirm if no `draftId`.
- Show message: "Cần tạo bản nháp trước khi xác nhận".

Medium term:

- `AiDraftReviewModal`
- editable payload
- source evidence
- confirm/reject
- audit note

Write actions remain draft-only.

## Contract Extension

Keep existing fields and add optional fields:

```ts
type ErumiChatResponseVNext = ErumiChatResponse & {
  confidenceReason?: string | null
  freshness?: string | null
  sourceRefs?: SourceRef[]
  model?: {
    provider: string
    name: string
    mode: 'fast' | 'deep' | 'mock' | 'fallback'
    status: 'live' | 'mock' | 'fallback' | 'budget_blocked' | 'privacy_blocked'
  }
  cost?: {
    estimatedUsd?: number
    budgetStatus?: string
    cacheStatus?: 'hit' | 'miss' | 'disabled'
  }
}
```

## What Not To Build Now

- Full dashboard page with large KPI grid.
- Forecasting.
- Arbitrary natural-language SQL.
- Provider management admin console.
- Direct task creation from toolbar.
- Heavy right panel always visible on mobile.
