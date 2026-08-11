import { expect, test } from '@playwright/test'

const adminEmail = process.env.E2E_ADMIN_EMAIL ?? 'admin@qaly.dev'
const adminPassword = process.env.E2E_ADMIN_PASSWORD ?? 'Admin@123456'

test('tab lộ trình và popup mẫu dùng giao diện gọn với bảng màu milk/sapphire', async ({ page }, testInfo) => {
  await page.goto('/Account/Login', { waitUntil: 'domcontentloaded' })
  await page.locator('input[name="Email"]').fill(adminEmail)
  await page.locator('input[name="Password"]').fill(adminPassword)
  await page.getByRole('button', { name: /Đăng nhập|Dang nhap|Login/i }).click()
  await page.waitForURL(url => !url.pathname.startsWith('/Account/Login'))
  await page.locator('.welcome-overlay').waitFor({ state: 'hidden', timeout: 5_000 }).catch(() => undefined)

  await page.goto('/projects', { waitUntil: 'domcontentloaded' })
  const project = page.locator('.project-grid-card, .project-list-item').filter({ hasText: 'Qaly Release 4.0' }).first()
  await expect(project).toBeVisible()
  await project.click()
  await page.locator('.project-tabs').getByRole('button', { name: 'Lộ Trình Dự Án' }).click()

  const roadmapHeader = page.locator('.roadmap-header')
  await expect(roadmapHeader).toBeVisible()
  expect((await roadmapHeader.boundingBox())?.height).toBeLessThanOrEqual(120)

  await page.getByRole('button', { name: /Khởi Tạo Mẫu Lộ Trình/i }).first().click()
  const modal = page.locator('.preset-modal')
  await expect(modal).toBeVisible()
  await expect(modal).toHaveCSS('background-color', 'rgb(255, 250, 240)')
  await expect(modal.getByRole('button', { name: /Quy Trình Outsource/i })).toBeVisible()
  await expect(modal.getByRole('button', { name: /Scrum \/ Agile/i })).toBeVisible()
  await expect(modal.getByRole('button', { name: /Waterfall \/ Truyền thống/i })).toBeVisible()

  await page.screenshot({ path: testInfo.outputPath('roadmap-preset-modal.png'), fullPage: true })
})
