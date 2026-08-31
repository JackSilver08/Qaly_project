import type { Locator } from '@playwright/test'

/**
 * Opens the named collapsed disclosures inside the AI assistant dialog.
 *
 * The assistant keeps its planning detail, process trail and context/source list inside
 * `<details>` elements so the answer itself stays the primary content. Tests that assert the
 * restored content have to open them the way a user would.
 *
 * `summary.click()` is dispatched in-page rather than through Playwright's click. The chat body
 * re-renders and auto-scrolls while turns stream in, so on a loaded machine Playwright's
 * actionability wait fails with "element is not stable" / "element was detached from the DOM"
 * before the panel settles. Toggling through the element's own activation behaviour exercises the
 * same disclosure without depending on the panel holding still.
 *
 * Only the classes passed in are opened — nested disclosures such as
 * `details.assistant-missing-skill` must stay closed for the specs that assert their default state.
 */
export async function openAssistantDisclosures(
  dialog: Locator,
  classNames: readonly string[],
): Promise<void> {
  const selector = classNames.map(name => `details.${name}`).join(', ')
  await dialog.locator(selector).evaluateAll(items => {
    for (const item of items) {
      const details = item as HTMLDetailsElement
      if (!details.open) details.querySelector(':scope > summary')?.click()
    }
  })
}
