import { expect, test } from '@playwright/test'

const adminEmail = process.env.E2E_ADMIN_EMAIL ?? 'admin@qaly.dev'
const adminPassword = process.env.E2E_ADMIN_PASSWORD ?? 'Qaly@E2E2026!'

test('admin can create a group and send a real team chat message', async ({ page }) => {
  await page.goto('/Account/Login')
  await page.locator('input[name="Email"]').fill(adminEmail)
  await page.locator('input[name="Password"]').fill(adminPassword)
  await page.locator('button[type="submit"]').click()
  await page.waitForURL((url) => !url.pathname.startsWith('/Account/Login'), { timeout: 20_000 })

  await page.goto('/teams')
  await expect(page.locator('.team-chat-page')).toBeVisible()

  const groupName = `E2E Smoke ${Date.now()}`
  page.once('dialog', (dialog) => dialog.accept(groupName))
  await page.getByLabel('Create group').click()

  const createdGroup = page.locator('.team-chat-group').filter({ hasText: groupName }).first()
  await expect(createdGroup).toBeVisible()
  await createdGroup.click()

  const message = `Smoke message ${Date.now()}`
  await page.locator('.team-chat-composer input[type="text"]').fill(message)
  await page.locator('.team-chat-composer button[type="submit"]').click()

  await expect(page.locator('.team-chat-body')).toContainText(message)
})
