import { expect, test } from '@playwright/test'
import { openSeededProject } from './support/seeded-project'

const adminEmail = process.env.E2E_ADMIN_EMAIL ?? 'admin@qaly.dev'
const adminPassword = process.env.E2E_ADMIN_PASSWORD ?? 'Admin@123456'

test('tab lộ trình dùng giao diện sáng và Bootstrap Icons nhất quán', async ({ page }, testInfo) => {
  const bootstrapFontRequests: string[] = []
  page.on('request', request => {
    if (/bootstrap-icons\.(woff2?|ttf)/i.test(request.url())) {
      bootstrapFontRequests.push(request.url())
    }
  })

  await page.goto('/Account/Login', { waitUntil: 'domcontentloaded' })
  await page.locator('input[name="Email"]').fill(adminEmail)
  await page.locator('input[name="Password"]').fill(adminPassword)
  await page.getByRole('button', { name: /Đăng nhập|Dang nhap|Login/i }).click()
  await page.waitForURL(url => !url.pathname.startsWith('/Account/Login'))
  await page.locator('.welcome-overlay').waitFor({ state: 'hidden', timeout: 5_000 }).catch(() => undefined)

  // Resolve the seeded project by id: the /projects grid is paginated by recency, so projects
  // created by tests running in parallel used to displace it and this spec silently asserted
  // roadmap content against an empty project.
  await openSeededProject(page)
  await page.locator('.project-tabs').getByRole('button', { name: 'Lộ Trình Dự Án' }).click()

  const roadmapHeader = page.locator('.roadmap-header')
  await expect(roadmapHeader).toBeVisible()
  await expect(page.locator('.roadmap-ai-fastbar')).toBeVisible()
  await expect(page.locator('.ai-action-card')).toHaveCount(6)
  await expect(page.locator('.ai-action-card .bs-icon > svg')).toHaveCount(6)
  await expect(page.locator('.milestone-node').first()).toBeVisible()
  await expect(page.locator('.milestone-detail-panel')).toBeVisible()

  await expect(page.locator('.ai-action-card .bs-icon').first()).toBeVisible()
  expect(bootstrapFontRequests).toEqual([])

  const buttonText = await page.locator('.project-roadmap-shell button').allTextContents()
  expect(buttonText.join(' ')).not.toMatch(/[\u{1F300}-\u{1FAFF}\u{2600}-\u{27BF}]/u)

  const aiSurfaceColor = await page.locator('.roadmap-ai-fastbar').evaluate(element =>
    window.getComputedStyle(element).backgroundColor,
  )
  expect(aiSurfaceColor).not.toBe('rgb(15, 23, 42)')

  await page.locator('.project-roadmap-shell').screenshot({
    path: testInfo.outputPath('roadmap-light-workspace.png'),
  })

  await page.getByRole('button', { name: /Khởi tạo từ mẫu|Khởi Tạo Mẫu Lộ Trình/i }).first().click()
  const modal = page.locator('.preset-modal')
  await expect(modal).toBeVisible()
  await expect(modal).toHaveCSS('background-color', 'rgb(255, 250, 240)')
  await expect(modal.getByRole('button', { name: /Quy Trình Outsource/i })).toBeVisible()
  await expect(modal.getByRole('button', { name: /Scrum \/ Agile/i })).toBeVisible()
  await expect(modal.getByRole('button', { name: /Waterfall \/ Truyền thống/i })).toBeVisible()

  await page.screenshot({ path: testInfo.outputPath('roadmap-preset-modal.png'), fullPage: true })
})
