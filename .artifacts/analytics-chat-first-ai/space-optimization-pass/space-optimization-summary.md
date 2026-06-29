# Analytics AI Space Optimization Pass Summary

Date: 2026-06-29
Scope: `/analytics` chat-first AI cockpit, progressive disclosure and space optimization pass.

## Files changed

- `src/Qaly.Web/ClientApp/components/chat/ErumiChatPanel.vue`
  - Added compact context row, palette-open awareness, local conversation history integration, context-aware suggestion pools, freshness chip, project dropdown flip/max-height handling, and compact active chat bottom bar spacing.
  - Routed toolbar actions to drawers or prompt-fill only. No direct write action is executed.
- `src/Qaly.Web/ClientApp/components/analytics-ai/AiQuickToolbar.vue`
  - Reworked the toolbar into primary actions plus utility controls and a progressive tool palette.
  - Fixed click behavior so the palette opens reliably and does not close immediately on focus/click.
- `src/Qaly.Web/ClientApp/components/analytics-ai/AiModelSelector.vue`
  - Added compact model labels for toolbar use.
- `src/Qaly.Web/ClientApp/components/analytics-ai/ConversationHistoryDrawer.vue`
  - New local-only conversation history drawer with search, restore, copy, delete, and clear.
- `src/Qaly.Web/ClientApp/components/analytics-ai/types.ts`
  - Added `history` and `settings` mini-tabs, compact model labels, toolbar action metadata, and conversation history item type.
- `src/Qaly.Web/wwwroot/dist/assets/*`
  - Rebuilt by `npm run build`.

## Behavior implemented

- `/analytics` remains chat-first. The main screen is still Erumi/chat centered, not a dashboard grid.
- Permanent controls were reduced to compact primary tools plus utility buttons.
- Secondary tools now live in a palette: Insight, Metrics, Actions, Model, Settings, History.
- Project dropdown now calculates whether to open up or down and keeps a bounded max height.
- Conversation history is stored locally in `localStorage` under `qaly.analytics.erumi.history.v1`, capped at 20 items.
- History stores prompt metadata and assistant snippet only. It does not store raw uploaded file content.
- Suggestions are context-aware and capped visually to 3 chips, with a compact "More" affordance.
- Planned providers remain UI-only and disabled.
- Slash command popup and file attach input remain reachable.
- `/dashboard` is not converted into the analytics cockpit.

## Commands run

- `npm run build`
  - Result: PASS
- `npm run typecheck`
  - Result: FAIL due existing out-of-scope errors:
    - `src/Qaly.Web/ClientApp/pages/DashboardPage.vue` lines 234, 241, 248, 250, 255: `TS2538`
    - `src/Qaly.Web/ClientApp/pages/TasksPage.vue` lines 279, 1249, 1289: `TS2339` / `TS2551`
  - No typecheck errors point to analytics AI files changed in this pass.
- `git diff --check`
  - Result: PASS
  - Only LF/CRLF warnings were printed.

## Browser QA

QA was run with Playwright Chromium against `http://localhost:5000/analytics` using the admin seed account. `qa-results.json` reports all assertions passed and no console/page errors.

Assertions covered:

- Login and load `/analytics`.
- Compact toolbar mounted with 6 visible buttons.
- Empty-state onboarding suggestions limited to 3.
- Compact model label visible.
- Planned provider options disabled.
- Palette action fills/opens UI only and does not auto-send.
- Project dropdown visible within viewport.
- Chat prompt creates active state and local history entry.
- History drawer restores local prompt record.
- Sources drawer reachable.
- Slash command popup visible.
- File input present.
- Mobile/tablet have no horizontal overflow.
- `/dashboard` has no analytics toolbar/model selector.

## Screenshots

- `analytics-desktop-space-pass.png`
- `analytics-desktop-tool-palette.png`
- `analytics-desktop-project-dropdown.png`
- `analytics-desktop-history-drawer.png`
- `analytics-mobile-space-pass.png`
- `analytics-tablet-space-pass.png`
- Extra: `analytics-desktop-space-pass-active.png`

## Remaining issues

- `npm run typecheck` is still blocked by pre-existing type errors in `DashboardPage.vue` and `TasksPage.vue`.
- The build updates many `wwwroot/dist/assets/*` files because Vite rewrites bundled outputs.

## Guardrails confirmed

- No migration added.
- No commit created.
- No new live provider integrated.
- No direct write action added.
- No raw upload content stored in local conversation history.
- Chat-first `/analytics` posture preserved.
