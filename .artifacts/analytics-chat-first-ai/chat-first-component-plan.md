# Chat-First Component Plan

Generated: 2026-06-28

## Component Strategy

Prefer additive components and small prop/slot extensions around `ErumiChatPanel`. Do not rewrite the chat panel in one pass.

## Proposed Components

### 1. `ChatFirstAnalyticsShell.vue`

Purpose:

- Thin page-level shell for `/analytics`.
- Owns model selector state, active drawer/tab and shared analytics cockpit state.
- Renders `ErumiChatPanel` as the main content.

Responsibilities:

- `activeTab`
- `drawerOpen`
- `selectedModel`
- `lastAssistantMessage`
- `selectedSourceRefs`

First integration:

```vue
<ChatFirstAnalyticsShell>
  <ErumiChatPanel />
</ChatFirstAnalyticsShell>
```

Later integration:

```vue
<ErumiChatPanel
  :model-mode="selectedModel.id"
  @assistant-message="handleAssistantMessage"
  @open-sources="openSources"
  @open-draft="openDraft"
/>
```

### 2. `AiModelSelector.vue`

Purpose:

- Compact UI-ready model/provider selector.
- Does not require backend provider changes in first frontend pass.

Props:

```ts
type Props = {
  modelValue: string
  options: AiModelOption[]
  compact?: boolean
}
```

Events:

- `update:modelValue`
- `open-settings`

States:

- live
- mock
- fallback
- budget blocked
- privacy blocked
- disabled planned provider

UI:

- button/chip: `Model: Fast`
- status dot
- dropdown list
- tooltip explaining disabled providers

### 3. `AiQuickToolbar.vue`

Purpose:

- Small icon toolbar below prompt chips or above composer.

Props:

```ts
type ToolbarItem = {
  key: string
  label: string
  icon: Component
  tab: AnalyticsMiniTab
  prompt?: string
  disabled?: boolean
}
```

Events:

- `select-tab`
- `run-prompt`
- `open-drawer`

Rules:

- Icon buttons must have title/tooltip.
- Text label can be visually hidden on compact mode.
- On mobile, toolbar is horizontal scroll.

### 4. `AnalyticsMiniTabs.vue`

Purpose:

- Secondary mode switch, not primary navigation.

Tabs:

- Chat
- Insights
- Metrics
- Sources
- Reports
- Actions
- History

Desktop:

- Appears inside side drawer header.

Mobile:

- Segmented control inside bottom sheet.

### 5. `AnalyticsSideDrawer.vue`

Purpose:

- Reusable drawer/bottom sheet container for secondary cockpit utilities.

Props:

```ts
type Props = {
  open: boolean
  title: string
  placement?: 'right' | 'bottom'
}
```

Slots:

- `header-actions`
- default content
- `footer`

Behavior:

- right drawer on desktop
- bottom sheet on mobile
- escape/overlay close
- focus trap later if modal semantics are added

### 6. `SourceRefsDrawer.vue`

Purpose:

- Render current `sources: string[]` and future structured `sourceRefs`.

Types:

```ts
type SourceRef = {
  type: 'Project' | 'Task' | 'TimeEntry' | 'Comment' | 'Meeting' | 'Evidence' | 'Audit' | 'System'
  id?: string
  label: string
  url?: string
  evidence?: string
  timestamp?: string
  confidence?: number
}
```

Fallback:

- If only `sources: string[]`, render source labels and explain that detailed citation links are not available yet.

Actions:

- Copy sources
- Ask about source
- Open link if URL exists

### 7. `ChatMessageInsightCard.vue`

Purpose:

- Compact domain-intent card inside assistant messages.

Card variants:

- risk
- metric
- source
- action
- report
- chart
- table

Props:

```ts
type Props = {
  kind: 'risk' | 'metric' | 'source' | 'action' | 'report' | 'chart' | 'table'
  title: string
  value?: string
  tone?: 'neutral' | 'good' | 'warning' | 'danger'
  summary?: string
  sourceRefs?: SourceRef[]
  actions?: InsightCardAction[]
}
```

Tiny actions:

- Why
- Sources
- Ask follow-up
- Copy
- Create draft
- Create report

### 8. `AiDraftReviewModal.vue`

Purpose:

- Review and edit AI-generated write drafts before confirm/reject.

Use only when action contains a persisted `draftId`.

Props:

```ts
type Props = {
  open: boolean
  draftId?: string
  payloadJson?: string
  sourceRefs?: SourceRef[]
}
```

Events:

- `confirm`
- `reject`
- `close`

Guardrails:

- No direct write action.
- Confirm path calls existing `/api/ai/drafts/{draftId}/confirm`.
- If no `draftId`, show "draft not persisted yet" and disable confirm.

### 9. `AiAnswerMetaChips.vue`

Purpose:

- Replace footer text with small, readable chips.

Inputs:

- confidence
- confidenceReason
- latencyMs
- source count
- freshness
- usedAi
- model/provider status
- cost/cache state

### 10. `ReportPreviewCard.vue`

Purpose:

- Show report output as a compact preview, not a full page.

Actions:

- Copy
- Export Markdown
- Export Word/Excel later
- Create draft follow-up action

## Suggested File Placement

```text
src/Qaly.Web/ClientApp/components/analytics-ai/
  ChatFirstAnalyticsShell.vue
  AiModelSelector.vue
  AiQuickToolbar.vue
  AnalyticsMiniTabs.vue
  AnalyticsSideDrawer.vue
  SourceRefsDrawer.vue
  ChatMessageInsightCard.vue
  AiAnswerMetaChips.vue
  ReportPreviewCard.vue
  AiDraftReviewModal.vue
  types.ts
```

Alternative if the team wants fewer folders:

```text
src/Qaly.Web/ClientApp/components/chat/analytics/
```

## Integration Order

1. Add `types.ts` with model options, source refs and metadata types.
2. Add `AiModelSelector` and render it above current empty-state composer.
3. Add `AiQuickToolbar` with UI-only actions that fill/send existing prompts.
4. Add `AnalyticsSideDrawer` and `AnalyticsMiniTabs`.
5. Add `SourceRefsDrawer` reading current `sources`.
6. Add `AiAnswerMetaChips`.
7. Add `ChatMessageInsightCard` for metrics/actions without changing backend.
8. Add optional `sourceRefs` and metadata parsing.
9. Add draft review modal only after persisted draft actions are reliable.

## Compatibility Notes

- Current `ErumiChatPanel` owns local state; extracting all state now is risky.
- Prefer emitting events from `ErumiChatPanel` when assistant response arrives.
- Keep current `metrics`, `tables`, `charts`, `actions`, `files` renderers until replacement cards are stable.
- Do not remove existing fallback answer behavior.
- Keep `isDrawer` behavior intact for `FloatingChatbot`.

## Accessibility Checklist

- Icon-only buttons need `aria-label` and visible tooltip on hover/focus.
- Drawer needs keyboard close.
- Tabs need selected state and keyboard navigation later.
- Chips must not rely on color alone for blocked/warning states.
- Mobile bottom sheet must not trap scroll behind it.

## Responsive Checklist

- Use `min-width: 0` on header child containers.
- Use `overflow-x: auto` only for prompt/toolbar chip rows.
- Truncate project/model labels.
- Hide long user role text under tablet width.
- Reduce placeholder copy on mobile.
- Composer max width should be container-relative, not viewport-breaking.
