import { expect, test, type Page } from '@playwright/test'
import { adminEmail, adminPassword } from './support/credentials'

test.describe.configure({ mode: 'serial' })
test.setTimeout(90_000)

async function login(page: Page) {
  await page.goto('/Account/Login', { waitUntil: 'domcontentloaded' })
  await page.locator('input[name="Email"]').fill(adminEmail)
  await page.locator('input[name="Password"]').fill(adminPassword)
  await page.getByRole('button', { name: /Dang nhap|Đăng nhập|Login/i }).click()
  await page.waitForURL((url) => !url.pathname.startsWith('/Account/Login'))
  await page.locator('.welcome-overlay').waitFor({ state: 'hidden', timeout: 5_000 }).catch(() => undefined)
  await expect(page.locator('.shell-header')).toBeVisible()
}

test('create, edit, archive, reload and restore use canonical Project state', async ({ page }) => {
  const runtimeErrors: string[] = []
  page.on('pageerror', (error) => runtimeErrors.push(error.message))

  await login(page)
  await page.goto('/projects', { waitUntil: 'domcontentloaded' })

  const suffix = `${Date.now()}`
  const originalName = `QB6 Project ${suffix}`
  const updatedName = `${originalName} Updated`

  await page.locator('.projects-overview__cta').click()
  const createDialog = page.getByRole('dialog', { name: 'Tạo dự án mới' })
  await createDialog.getByLabel('Tên dự án mới').fill(originalName)
  await createDialog.getByLabel('Mô tả dự án mới').fill('Bằng chứng vòng đời Project qua giao diện thật.')
  await createDialog.getByRole('button', { name: 'Tạo dự án', exact: true }).click()

  await page.waitForURL((url) => /^\/projects\/[0-9a-f-]{36}$/i.test(url.pathname))
  const projectId = new URL(page.url()).pathname.split('/')[2]

  await page.reload({ waitUntil: 'domcontentloaded' })
  await expect(page.locator('.project-home-main')).toContainText(originalName)

  await page.goto('/projects', { waitUntil: 'domcontentloaded' })
  const originalCard = page.locator('.project-grid-card').filter({ hasText: originalName })
  await expect(originalCard).toHaveCount(1)
  await originalCard.getByRole('button', { name: 'Sửa dự án' }).click()

  const editDialog = page.getByRole('dialog', { name: 'Chỉnh sửa dự án' })
  await editDialog.getByLabel('Tên dự án', { exact: true }).fill(updatedName)
  await editDialog.getByRole('button', { name: 'Lưu thay đổi' }).click()
  await expect(page.locator('.project-grid-card').filter({ hasText: updatedName })).toHaveCount(1)

  const updatedCard = page.locator('.project-grid-card').filter({ hasText: updatedName })
  await updatedCard.getByRole('button', { name: 'Lưu trữ dự án' }).click()
  const archiveDialog = page.getByRole('dialog', { name: 'Lưu trữ dự án?' })
  await archiveDialog.getByRole('button', { name: 'Lưu trữ', exact: true }).click()
  await expect(updatedCard).toHaveCount(0)

  await page.reload({ waitUntil: 'domcontentloaded' })
  await expect(page.locator('.project-grid-card').filter({ hasText: updatedName })).toHaveCount(0)

  await page.goto('/projects/archived', { waitUntil: 'domcontentloaded' })
  const archivedCard = page.locator('.archived-project-card').filter({ hasText: updatedName })
  await expect(archivedCard).toHaveCount(1)
  await archivedCard.getByRole('button', { name: 'Khôi phục', exact: true }).click()
  const restoreDialog = page.getByRole('dialog', { name: 'Khôi phục dự án?' })
  await restoreDialog.getByRole('button', { name: 'Khôi phục', exact: true }).click()
  await expect(archivedCard).toHaveCount(0)

  await page.reload({ waitUntil: 'domcontentloaded' })
  await expect(page.locator('.archived-project-card').filter({ hasText: updatedName })).toHaveCount(0)

  await page.goto('/projects', { waitUntil: 'domcontentloaded' })
  await expect(page.locator('.project-grid-card').filter({ hasText: updatedName })).toHaveCount(1)
  await expect.poll(async () => {
    return await page.evaluate(async (id) => {
      const response = await fetch(`/api/projects/${id}`)
      if (!response.ok) return null
      const payload = await response.json()
      return payload.data ?? payload
    }, projectId)
  }).toMatchObject({ id: projectId, name: updatedName, status: 'Active' })

  expect(runtimeErrors).toEqual([])
})
