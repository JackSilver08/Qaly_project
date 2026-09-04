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

test('admin opens the read-only user view from the account menu and can exit it', async ({ page }) => {
  await loginAsAdmin(page)

  await expect(page.getByText('Kiểm tra giao diện theo người dùng')).toHaveCount(0)

  const accountMenu = page.locator('.shell-user-menu')
  await accountMenu.click()
  await page.locator('.shell-user-dropdown-item--simulation').click()

  const dialog = page.getByRole('dialog', { name: 'Xem với vai trò người dùng' })
  await expect(dialog).toBeVisible()
  await expect(dialog.getByText('Chế độ chỉ đọc')).toBeVisible()
  await expect(dialog.locator('.simulation-user')).not.toHaveCount(0)

  const selectedName = await dialog.locator('.simulation-user__identity strong').first().innerText()
  await dialog.locator('.simulation-user').first().click()

  await page.waitForLoadState('domcontentloaded')
  const activeChip = page.locator('.simulation-active-chip')
  await expect(activeChip).toBeVisible()
  await expect(activeChip).toContainText(selectedName)

  await activeChip.getByRole('button', { name: 'Thoát chế độ xem với vai trò' }).click()
  await page.waitForLoadState('domcontentloaded')
  await expect(page.locator('.simulation-active-chip')).toHaveCount(0)
  await expect(page.locator('.shell-header')).toBeVisible()
})

test('user picker supports search and keyboard dismissal', async ({ page }) => {
  await loginAsAdmin(page)

  const accountMenu = page.locator('.shell-user-menu')
  await accountMenu.click()
  await page.locator('.shell-user-dropdown-item--simulation').click()

  const dialog = page.getByRole('dialog', { name: 'Xem với vai trò người dùng' })
  const initialCount = await dialog.locator('.simulation-user').count()
  expect(initialCount).toBeGreaterThan(1)

  const firstName = await dialog.locator('.simulation-user__identity strong').first().innerText()
  await dialog.getByRole('searchbox').fill(firstName)
  await expect(dialog.locator('.simulation-user')).toHaveCount(1)

  await page.keyboard.press('Escape')
  await expect(dialog).toHaveCount(0)
  await expect(accountMenu).toBeFocused()
})

test('user picker becomes a bottom sheet on a mobile viewport', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 })
  await loginAsAdmin(page)

  await page.locator('.shell-user-menu').click()
  await page.locator('.shell-user-dropdown-item--simulation').click()

  const dialog = page.getByRole('dialog', { name: 'Xem với vai trò người dùng' })
  await expect(dialog).toBeVisible()
  await page.waitForTimeout(300)
  const box = await dialog.boundingBox()

  expect(box).not.toBeNull()
  expect(Math.round(box!.width)).toBe(390)
  expect(Math.round(box!.x)).toBe(0)
  expect(Math.round(box!.y + box!.height)).toBe(844)
})
