import { expect, test } from '@playwright/test'
import { openSeededProject } from './support/seeded-project'

const adminEmail = process.env.E2E_ADMIN_EMAIL ?? 'admin@qaly.dev'
const adminPassword = process.env.E2E_ADMIN_PASSWORD ?? 'Admin@123456'

test('tab lộ trình dùng giao diện sáng, phân cấp rõ và Bootstrap Icons nhất quán', async ({ page }, testInfo) => {
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
  await expect(roadmapHeader.getByRole('heading', { name: 'Lộ trình dự án' })).toBeVisible()
  await expect(page.locator('.journey-section-header')).toBeVisible()
  await expect(page.locator('.roadmap-metrics-bar .metric-pill')).toHaveCount(4)
  await expect(page.locator('.roadmap-ai-fastbar')).toBeVisible()
  await expect(page.locator('.ai-action-card')).toHaveCount(6)
  await expect(page.locator('.ai-action-card .bs-icon > svg')).toHaveCount(6)
  await expect(page.locator('.ai-action-card').first()).toBeHidden()
  await page.getByRole('button', { name: 'Mở 6 công cụ AI' }).click()
  await expect(page.locator('.ai-action-card').first()).toBeVisible()
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

  const editMilestoneButton = page.getByRole('button', { name: 'Sửa mốc' })
  const deleteMilestoneButton = page.getByRole('button', { name: 'Xóa mốc' })
  await expect(editMilestoneButton).toBeVisible()
  await expect(deleteMilestoneButton).toBeVisible()
  await expect(editMilestoneButton).toHaveCSS('border-radius', '10px')
  await expect(deleteMilestoneButton).toHaveCSS('color', 'rgb(181, 45, 53)')

  await deleteMilestoneButton.click()
  const confirmation = page.locator('.confirmation-modal')
  await expect(confirmation).toBeVisible()
  await expect(confirmation).toHaveCSS('border-radius', '20px')
  await expect(confirmation.getByRole('button', { name: 'Xóa mốc' })).toHaveCSS('background-color', 'rgb(201, 54, 63)')
  await confirmation.getByRole('button', { name: 'Hủy' }).click()
  await expect(confirmation).toBeHidden()

  const completeMilestoneButton = page.getByRole('button', { name: 'Nghiệm thu mốc' })
  if (await completeMilestoneButton.count()) {
    await completeMilestoneButton.click()
    await expect(confirmation.getByRole('heading', { name: 'Xác nhận nghiệm thu mốc' })).toBeVisible()
    await expect(confirmation.getByRole('button', { name: 'Xác nhận nghiệm thu' })).toHaveCSS('background-color', 'rgb(8, 122, 85)')
    await confirmation.getByRole('button', { name: 'Hủy' }).click()
  }

  await page.getByRole('button', { name: 'Hướng dẫn sử dụng' }).click()
  const onboardingDialog = page.getByRole('dialog', { name: 'Hướng dẫn lộ trình dự án' })
  await expect(onboardingDialog).toBeVisible()
  await expect(onboardingDialog).toHaveCSS('border-radius', '22px')
  await expect(onboardingDialog.getByRole('button', { name: 'Hoàn thành hướng dẫn' })).toHaveCSS('background-color', 'rgb(8, 122, 85)')
  await onboardingDialog.getByRole('button', { name: 'Đóng hướng dẫn' }).click()
  await expect(onboardingDialog).toBeHidden()

  await page.getByRole('button', { name: /Khởi tạo từ mẫu|Khởi Tạo Mẫu Lộ Trình/i }).first().click()
  const modal = page.locator('.preset-modal')
  await expect(modal).toBeVisible()
  await expect(modal).toHaveCSS('background-color', 'rgb(255, 250, 240)')
  await expect(modal.getByRole('button', { name: /Quy Trình Outsource/i })).toBeVisible()
  await expect(modal.getByRole('button', { name: /Scrum \/ Agile/i })).toBeVisible()
  await expect(modal.getByRole('button', { name: /Waterfall \/ Truyền thống/i })).toBeVisible()

  await page.screenshot({ path: testInfo.outputPath('roadmap-preset-modal.png'), fullPage: true })
})

test('lộ trình giữ bố cục gọn trên màn hình di động', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 })
  await page.goto('/Account/Login', { waitUntil: 'domcontentloaded' })
  await page.locator('input[name="Email"]').fill(adminEmail)
  await page.locator('input[name="Password"]').fill(adminPassword)
  await page.getByRole('button', { name: /Đăng nhập|Dang nhap|Login/i }).click()
  await page.waitForURL(url => !url.pathname.startsWith('/Account/Login'))
  await page.locator('.welcome-overlay').waitFor({ state: 'hidden', timeout: 5_000 }).catch(() => undefined)

  await openSeededProject(page)
  await page.locator('.project-tabs').getByRole('button', { name: 'Lộ Trình Dự Án' }).click()

  await expect(page.locator('.roadmap-header')).toBeVisible()
  await expect(page.locator('.roadmap-metrics-bar .metric-pill')).toHaveCount(4)
  const metricBoxes = await page.locator('.roadmap-metrics-bar .metric-pill').evaluateAll(elements =>
    elements.map(element => element.getBoundingClientRect().top),
  )
  expect(new Set(metricBoxes.map(top => Math.round(top))).size).toBe(4)

  await page.getByRole('button', { name: 'Hướng dẫn sử dụng' }).click()
  const onboardingDialog = page.getByRole('dialog', { name: 'Hướng dẫn lộ trình dự án' })
  await expect(onboardingDialog).toBeVisible()
  const dialogBox = await onboardingDialog.boundingBox()
  expect(dialogBox).not.toBeNull()
  expect(dialogBox!.width).toBeLessThanOrEqual(390)
  await page.keyboard.press('Escape')
  await expect(onboardingDialog).toBeHidden()
})
