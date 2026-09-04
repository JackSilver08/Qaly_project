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
  await expect(page.locator('.shell-header')).toBeVisible()
}

test('notification count stays inside the button and the button toggles the panel', async ({ page }) => {
  await loginAsAdmin(page)

  const button = page.locator('.header-notification-button')
  const count = button.locator('.header-notification-button__count')
  await expect(button).toHaveAttribute('aria-expanded', 'false')
  await expect(count).toBeVisible()

  const buttonBox = await button.boundingBox()
  const countBox = await count.boundingBox()
  expect(buttonBox).not.toBeNull()
  expect(countBox).not.toBeNull()
  expect(countBox!.x).toBeGreaterThanOrEqual(buttonBox!.x)
  expect(countBox!.y).toBeGreaterThanOrEqual(buttonBox!.y)
  expect(countBox!.x + countBox!.width).toBeLessThanOrEqual(buttonBox!.x + buttonBox!.width)
  expect(countBox!.y + countBox!.height).toBeLessThanOrEqual(buttonBox!.y + buttonBox!.height)

  await button.click()
  const panel = page.locator('#notification-panel')
  await expect(panel).toBeVisible()
  await expect(button).toHaveAttribute('aria-expanded', 'true')

  const headerBox = await page.locator('.shell-header').boundingBox()
  const panelBox = await panel.boundingBox()
  expect(panelBox!.y).toBeGreaterThanOrEqual(headerBox!.y + headerBox!.height)

  await button.click()
  await expect(panel).toHaveCount(0)
  await expect(button).toHaveAttribute('aria-expanded', 'false')
})

test('notification button remains contained in the mobile header', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 })
  await loginAsAdmin(page)

  const headerBox = await page.locator('.shell-header').boundingBox()
  const buttonBox = await page.locator('.header-notification-button').boundingBox()
  expect(headerBox).not.toBeNull()
  expect(buttonBox).not.toBeNull()
  expect(buttonBox!.x).toBeGreaterThanOrEqual(headerBox!.x)
  expect(buttonBox!.x + buttonBox!.width).toBeLessThanOrEqual(headerBox!.x + headerBox!.width)
})
