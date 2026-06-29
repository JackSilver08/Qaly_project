# Analytics AI Implementation Prompt Acceptance Review

Date: 2026-06-28

## Verdict

The prompt is strong, but not yet 100% implementation-safe as written.

Estimated coverage: 92%.

Accepted only if the amendments below are added before the first implementation pass. The largest risks are shared-component regression, header clipping scope, JSON contract compatibility, and verification ambiguity.

## What Is Already Covered Well

- Keeps `/analytics` chat-first instead of turning it into a KPI dashboard.
- Preserves `ErumiChatPanel` as the core experience.
- Keeps changes additive and rollback-friendly.
- Defines compact cockpit utilities: model selector, toolbar, drawer/bottom sheet, sources drawer, metadata chips.
- Explicitly avoids real provider integration, migrations, commits, direct write actions, NL SQL, forecasts, and large dashboard rebuilds.
- Requires source compatibility with current `sources: string[]`.
- Requires responsive screenshots for desktop, tablet, and mobile.
- Requires an implementation summary artifact.

## Blocking Gaps To Fix Before Implementation

### 1. Header Fix Scope Is Incomplete

The prompt asks to fix tablet/mobile header clipping, but its scan list does not explicitly include the actual header component.

Must add to scan/edit scope:

- `src/Qaly.Web/ClientApp/components/TopHeader.vue`
- `src/Qaly.Web/ClientApp/App.vue`
- Any shell/layout CSS affecting `.shell-header`, `.shell-header-actions`, `.header-search`, and user menu clipping.

Reason: otherwise the implementer may try to solve clipping inside `/analytics` only and miss the actual source of the bug.

### 2. Shared `ErumiChatPanel` Regression Risk

`ErumiChatPanel.vue` is shared by the full analytics page and `FloatingChatbot.vue`. The prompt says to scan `FloatingChatbot.vue`, but the acceptance criteria should explicitly require the floating/drawer chat mode to keep working.

Must add:

- Preserve `isDrawer` behavior and compact drawer dimensions.
- Cockpit utilities should be hidden, collapsed, or density-adjusted when `ErumiChatPanel` is used inside `FloatingChatbot`.
- Verify that global floating chat can still open, send, attach, and close after the integration.

### 3. Verification Commands Need To Be Concrete

The prompt says "if command exists", which is not strict enough for handoff.

Must require:

- Inspect `src/Qaly.Web/ClientApp/package.json`.
- Run `npm run typecheck` from `src/Qaly.Web/ClientApp`.
- Run `npm run build` from `src/Qaly.Web/ClientApp`.
- Run E2E or browser checks only if Playwright/browser tooling is installed and available.
- If any command fails because of missing dependencies or unrelated pre-existing errors, record the exact failure in the summary instead of masking it.

### 4. Responsive Acceptance Needs A Measurable Overflow Check

The prompt mentions no overflow, but implementation should verify it quantitatively.

Must add:

- For `1440x900`, `768x1024`, and `390x844`, check `document.documentElement.scrollWidth <= window.innerWidth + 1`.
- Check header actions, project selector, composer input, toolbar, and drawer/bottom sheet visually in each viewport.
- Use `min-width: 0`, `max-width: 100%`, stable sizing, and horizontal scroll only for chip/tool rows.

### 5. JSON/DTO Compatibility Needs More Precision

The prompt asks to scan `ErumiChatResponse`, but the frontend metadata implementation needs a compatibility rule.

Must add:

- Keep `sources?: string[]` fully supported.
- Optional new metadata fields must be optional and null-safe.
- Prefer existing ASP.NET/camelCase JSON names such as `confidenceReason`; only support snake_case defensively if already present.
- Do not require backend changes in pass 1.

### 6. Encoding And Vietnamese Text Safety

The repo contains Vietnamese UI copy. Terminal output may display mojibake, so source edits must preserve UTF-8.

Must add:

- Preserve existing UTF-8 Vietnamese strings.
- Avoid large text rewrites in existing components unless necessary.
- Do not introduce mojibake by copying terminal-rendered text back into source files.

### 7. Toolbar Write-Like Actions Must Not Auto-Execute

The prompt says toolbar items can fill or send prompts. For `Actions`, sending could trigger draft/change flows and surprise the user.

Must add:

- `Actions` and any write-like prompt should fill the composer by default, not auto-send.
- Non-mutating insights/report/risk prompts may fill or send only if existing chat behavior supports it safely.
- No direct mutation or confirmation action is added in pass 1.

### 8. Accessibility Guardrails Are Missing

The prompt mentions tooltips and close behavior, but icon-only controls need explicit accessibility criteria.

Must add:

- Every icon-only button has `type="button"` and an `aria-label`.
- Drawer/bottom sheet has accessible title association if practical.
- Overlay click and Escape close should not break keyboard users.
- Focus styles remain visible.

### 9. Dark Theme Needs A Sanity Check

Existing chat styles include dark theme selectors. New cockpit components should not look broken in dark mode.

Must add:

- Use existing CSS variables/tokens where possible.
- Add scoped dark theme rules only where needed.
- Do a basic dark-mode visual sanity check if the app exposes a dark theme state.

### 10. Dependency Policy Should Be Explicit

Pass 1 should not add package risk.

Must add:

- Do not add new npm packages.
- Use existing `lucide-vue-next` icons if icons are needed.
- Use local Vue/CSS patterns already present in the repo.

### 11. Summary Artifact Should Include Diff Context

The prompt requires a summary, but should also require diff visibility.

Must add to `implementation-pass-1-summary.md`:

- `git diff --stat`
- Changed files list
- Verification commands and results
- Screenshot paths
- Known remaining issues
- Confirmation: no migration, no commit, no backend provider activation

## Recommended Prompt Addendum

Append this block to the implementation prompt before coding:

```text
Additional mandatory guardrails before implementation:

1. Also scan `src/Qaly.Web/ClientApp/components/TopHeader.vue`, `src/Qaly.Web/ClientApp/App.vue`, and `src/Qaly.Web/ClientApp/package.json`.
2. Preserve `ErumiChatPanel` shared usage. Verify both `/analytics` full-page mode and `FloatingChatbot.vue` / drawer mode. Cockpit utilities must not overcrowd or break drawer mode.
3. Do not add npm dependencies. Use existing `lucide-vue-next` and local Vue/CSS patterns.
4. Preserve UTF-8 Vietnamese UI text. Do not copy mojibake terminal output back into source files.
5. Keep frontend DTO additions optional and null-safe. `sources?: string[]` remains supported. Prefer camelCase fields such as `confidenceReason`; no backend change is required in pass 1.
6. Quick toolbar write-like actions, especially `Actions`, must fill the composer by default and must not auto-execute direct writes.
7. All icon-only controls must have `type="button"` and `aria-label`; drawer close should support overlay/Escape where practical.
8. Verify responsive overflow with `document.documentElement.scrollWidth <= window.innerWidth + 1` at 1440x900, 768x1024, and 390x844.
9. Include a basic dark-theme sanity check if the app exposes dark theme state.
10. Run `npm run typecheck` and `npm run build` from `src/Qaly.Web/ClientApp`. If browser/E2E tooling is unavailable, document the reason. The summary must include `git diff --stat`, changed files, command results, screenshot paths, remaining issues, and confirmations that there was no migration, no commit, and no live provider activation.
```

## Acceptance Decision

After adding the addendum above, the prompt is acceptable for implementation pass 1.

Without the addendum, it is likely to miss shared chat mode, header layout scope, or verification details, which can cause regressions after implementation.
