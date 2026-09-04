import { expect, test, type Page } from '@playwright/test'
import { adminEmail, adminPassword } from './support/credentials'

test.use({ ignoreHTTPSErrors: true })

async function loginAsAdmin(page: Page) {
  await page.goto('/Account/Login', { waitUntil: 'domcontentloaded' })
  await page.locator('input[name="Email"]').fill(adminEmail)
  await page.locator('input[name="Password"]').fill(adminPassword)
  await page.locator('#loginForm button[type="submit"]').click()
  await page.waitForURL(url => !url.pathname.startsWith('/Account/Login'), {
    timeout: 20_000,
    waitUntil: 'domcontentloaded',
  })
  await page.locator('.welcome-overlay').waitFor({ state: 'hidden', timeout: 5_000 }).catch(() => undefined)
}

test('strategic analysis action uses the short label on one line', async ({ page }) => {
  await page.setViewportSize({ width: 1120, height: 720 })
  await loginAsAdmin(page)

  const card = page.getByTestId('dashboard-strategic-brief')
  const button = card.locator('.ai-button')
  await expect(button).toBeVisible()
  await expect(button).toHaveText('Tạo phân tích')

  const layout = await button.evaluate(element => {
    const box = element.getBoundingClientRect()
    return {
      height: box.height,
      whiteSpace: getComputedStyle(element).whiteSpace,
    }
  })

  expect(layout.whiteSpace).toBe('nowrap')
  expect(layout.height).toBeLessThanOrEqual(48)
  await expect(card.getByText('Tạo phân tích', { exact: true })).toHaveCount(2)
})
