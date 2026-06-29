# CSS Regression Notes

Date: 2026-06-29

## Areas Hardened

- Project dropdown:
  - Parent dropdown now receives an `is-open` class.
  - Open dropdown z-index is above the composer and input focus stacking contexts.
  - Menu width is viewport-constrained and supports open-up placement near the bottom bar.

- Model selector:
  - Menu placement is recalculated on open and resize.
  - Bottom composer opens the model menu upward when there is not enough room below.
  - Open selector/menu z-index is raised without affecting closed layout.

- Analytics drawer:
  - Drawer shell is `fixed` relative to the viewport with app-shell desktop/tablet/mobile offsets.
  - Panel width uses remaining viewport space, preventing right-edge clipping.
  - Enter/leave transform was reduced to stay inside the viewport during animation.
  - Final screenshots wait for transition settlement to avoid ghosted drawer evidence.

- Quick toolbar:
  - Palette open state raises toolbar stacking only while the palette is visible.
  - Palette remains viewport-bounded on desktop and fixed bottom-sheet style on mobile.

- Chat surface:
  - Hidden file input remains `display: none`; visible attachment control is custom.
  - Horizontal overflow checked at desktop `1440x900`, tablet `834x1112`, and mobile `390x844`.
  - LocalStorage corrupt-history reload path confirmed to not crash the page.

## Final QA Coverage

- Desktop default analytics: no horizontal overflow.
- Desktop active chat: send flow works and no horizontal overflow.
- Slash command popup: visible and within viewport.
- Shift+Enter: inserts newline.
- Enter: sends prompt.
- Tool palette: visible and within viewport.
- Project dropdown: visible and within viewport.
- Model selector: visible and within viewport.
- History drawer: visible, non-ghosted, and within viewport.
- Sources drawer: visible, non-ghosted, and within viewport.
- Tablet/mobile: no horizontal overflow.
- Dashboard: floating chatbot remains present; analytics cockpit controls do not leak.
- Reload/cache/static dist: static manifest served, reload after corrupt history verified.

## Remaining External Risk

`npm run typecheck` is still blocked by existing `DashboardPage.vue` and `TasksPage.vue` type errors outside this analytics hardening pass. No new analytics-related typecheck errors were introduced by this pass.
