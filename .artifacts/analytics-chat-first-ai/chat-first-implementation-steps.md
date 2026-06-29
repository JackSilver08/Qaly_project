# Chat-First Implementation Steps

Generated: 2026-06-28

## Implementation Philosophy

Make the cockpit feel richer without taking the chat away. Ship in small reversible slices. Each slice should keep current chat behavior working.

## Step 1 - Add Types And Static Registry

Files:

- `src/Qaly.Web/ClientApp/components/analytics-ai/types.ts`

Add:

- `AiModelOption`
- `AiModelStatus`
- `AnalyticsMiniTab`
- `SourceRef`
- `AiAnswerMetadata`
- `InsightCardAction`

Create static model registry:

- `fast-current`
- `mock`
- `deep-planned`
- `provider-planned`

No backend change required.

## Step 2 - Add Model Selector

Files:

- `AiModelSelector.vue`

Render in `ErumiChatPanel` empty state near top of composer area or in a thin shell wrapper.

Behavior:

- Selected value stored locally.
- Disabled planned providers show tooltip.
- Status chip shows Live/Mock/Fallback/Budget/Privacy state.

Verification:

- Desktop no layout shift.
- Mobile no horizontal overflow.

## Step 3 - Add Quick Toolbar

Files:

- `AiQuickToolbar.vue`

First behavior:

- Insights sends or fills existing insight prompt.
- Risks sends or fills risk prompt.
- Metrics opens drawer with latest metrics if any.
- Sources opens drawer.
- Report fills report prompt.
- Actions fills draft/action prompt.
- Model opens model selector/settings.

No new backend endpoint.

## Step 4 - Add Drawer And Mini Tabs

Files:

- `AnalyticsSideDrawer.vue`
- `AnalyticsMiniTabs.vue`

Behavior:

- Right drawer on desktop.
- Bottom sheet on mobile/tablet.
- Tabs switch content only inside drawer.
- Does not navigate away from `/analytics`.

## Step 5 - Add Source Drawer Backward Compatible

Files:

- `SourceRefsDrawer.vue`

Input:

- current `sources: string[]`
- future `sourceRefs?: SourceRef[]`

Behavior:

- If only strings exist, show "system source labels".
- If structured refs exist, render grouped citations.

## Step 6 - Upgrade Metadata Rendering

Files:

- `AiAnswerMetaChips.vue`
- small change in `ErumiChatPanel.vue`

Add frontend type fields:

- `confidenceReason?: string | null`
- `freshness?: string | null`
- `sourceRefs?: SourceRef[]`
- `model?: AiModelMetadata`
- `cost?: AiCostMetadata`

Keep all optional.

## Step 7 - Add Message Insight Cards

Files:

- `ChatMessageInsightCard.vue`

Initial mapping:

- each metric becomes `kind=metric`
- warning/danger metrics can render as `kind=risk`
- each action becomes `kind=action`
- report files/actions become `kind=report`

Keep current tables/charts as-is.

## Step 8 - Backend Optional Contract

Files:

- `ErumiChatDtos.cs`
- `ErumiChatService.cs`

Add optional fields only:

- `SourceRefs`
- `Freshness`
- `Model`
- `Cost`
- keep existing `Sources`
- keep existing `ConfidenceReason`

Parser:

- parse `sourceRefs`
- parse `freshness`
- parse model/provider metadata
- parse budget/cache metadata

Backward compatibility:

- old response payloads still render.
- existing tests can pass with default null/empty values.

## Step 9 - Draft Review Hardening

Frontend:

- If action has `requiresConfirmation` but no `payload.draftId`, show a draft preparation card, not confirm/reject buttons.
- Confirm/reject only enabled for persisted `draftId`.

Backend:

- Ensure write intent can produce persisted draft through existing `AiWorkflowService` or tool-calling path before showing confirm UI.

## Step 10 - Responsive Fixes

Fix observed preview issues:

- tablet header user area clipping
- mobile header overflow
- mobile last updated text clipping
- mobile composer placeholder clipping
- project selector overflow
- prompt chip wrapping/scrolling

Regression checks:

- 1440 x 900
- 768 x 1024
- 390 x 844

## Step 11 - Tests

Frontend:

- typecheck
- component render smoke if available
- Playwright route smoke for `/analytics`
- mobile overflow check via screenshot/manual Playwright

Backend:

- existing ErumiChatService tests
- DTO serialization backward compatibility
- draft confirm path unchanged

Manual QA:

- ask "Dự án nào đang rủi ro?"
- ask "So sánh các dự án"
- ask "Tạo báo cáo tuần này"
- trigger source drawer
- trigger model selector
- trigger draft action path

## Suggested First PR Scope

Best first implementation PR:

1. Add `AiModelSelector`.
2. Add `AiQuickToolbar`.
3. Add drawer shell and source drawer using current `sources`.
4. Add metadata chips for current latency/confidence/sources.
5. Fix mobile/tablet overflow.

Do not change backend in first PR unless needed for type compatibility.

## Acceptance Criteria

- `/analytics` still opens to Erumi chat-first cockpit.
- Existing chat input, prompts, file attachment and send still work.
- Toolbar icons open drawers/tabs or fill prompts without navigation.
- Source drawer works with current `sources: string[]`.
- No direct write action is introduced.
- Model selector shows planned providers without pretending they are live.
- Mobile and tablet have no horizontal overflow.
- No migration, no commit required.
