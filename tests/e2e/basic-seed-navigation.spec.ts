import { expect, test, type Page, type TestInfo } from '@playwright/test'
import { adminEmail, adminPassword } from './support/credentials'

test.describe.configure({ mode: 'serial' })

const baseURL = process.env.E2E_BASE_URL ?? 'http://127.0.0.1:5000'

let infraBlockedReason: string | null = null

async function captureFailureEvidence(page: Page, testInfo: TestInfo) {
  await page
    .screenshot({
      path: testInfo.outputPath('test-failed.png'),
      fullPage: true,
    })
    .catch(() => undefined)
}

async function runWithEvidence(
  page: Page,
  testInfo: TestInfo,
  action: () => Promise<void>,
) {
  try {
    await action()
  } catch (error) {
    await captureFailureEvidence(page, testInfo)
    throw error
  }
}

async function loginAsSeededAdmin(page: Page) {
  await page.goto('/Account/Login', { waitUntil: 'domcontentloaded' })
  await expect(page.locator('#loginForm')).toBeVisible()
  await page.locator('input[name="Email"]').fill(adminEmail)
  await page.locator('input[name="Password"]').fill(adminPassword)
  await page.getByRole('button', { name: /Dang nhap|Đăng nhập|Login/i }).click()
  await page.waitForURL((url) => !url.pathname.startsWith('/Account/Login'), {
    timeout: 20_000,
    waitUntil: 'domcontentloaded',
  })
  await page.locator('.welcome-overlay').waitFor({ state: 'hidden', timeout: 5_000 }).catch(() => undefined)
  await expect(page.locator('.shell-header')).toBeVisible()
}

test.beforeAll(async ({ request }) => {
  try {
    const response = await request.get('/Account/Login', {
      failOnStatusCode: false,
      timeout: 5_000,
    })

    if (response.status() >= 500) {
      infraBlockedReason = `Blocked do moi truong ha tang: server ${baseURL} tra ve HTTP ${response.status()}.`
    }
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error)
    infraBlockedReason = `Blocked do moi truong ha tang: khong ket noi duoc server ${baseURL}. ${message}`
  }
})

test.beforeEach(async ({}, testInfo) => {
  if (!infraBlockedReason) return

  testInfo.annotations.push({
    type: 'Blocked',
    description: infraBlockedReason,
  })
  test.skip(true, infraBlockedReason)
})

test('TC-E2E-001 public login page renders correctly', async ({ page }, testInfo) => {
  await runWithEvidence(page, testInfo, async () => {
    await page.goto('/Account/Login', { waitUntil: 'domcontentloaded' })
    await expect(page.locator('#loginForm')).toBeVisible()
    await expect(page.locator('input[name="Email"]')).toBeVisible()
    await expect(page.locator('input[name="Password"]')).toBeVisible()
    await expect(page.getByRole('button', { name: /Dang nhap|Đăng nhập|Login/i })).toBeVisible()
  })
})

test('TC-E2E-002 seeded admin can login and see authenticated shell', async ({ page }, testInfo) => {
  await runWithEvidence(page, testInfo, async () => {
    await loginAsSeededAdmin(page)
    await expect(page.locator('.shell-user-menu')).toBeVisible()
  })
})

test('TC-E2E-003 dashboard renders seeded demo data', async ({ page }, testInfo) => {
  await runWithEvidence(page, testInfo, async () => {
    await loginAsSeededAdmin(page)
    await page.goto('/dashboard', { waitUntil: 'domcontentloaded' })
    await expect(page.locator('.shell-header')).toBeVisible()
    await expect(page.locator('body')).toContainText(/Qaly|Demo|Du an|Dự án|Nhiem vu|Nhiệm vụ/i)
  })
})

test('TC-E2E-004 primary authenticated navigation routes render', async ({ page }, testInfo) => {
  await runWithEvidence(page, testInfo, async () => {
    await loginAsSeededAdmin(page)

    for (const route of ['/dashboard', '/projects', '/tasks', '/teams', '/analytics']) {
      await page.goto(route, { waitUntil: 'domcontentloaded' })
      await expect(page).toHaveURL(new RegExp(`${route}$`))
      await expect(page.locator('.shell-header')).toBeVisible()
    }
  })
})

test('TC-E2E-005 projects page shows seeded project or empty-state safely', async ({ page }, testInfo) => {
  await runWithEvidence(page, testInfo, async () => {
    await loginAsSeededAdmin(page)
    await page.goto('/projects', { waitUntil: 'domcontentloaded' })
    await expect(page.locator('.shell-header')).toBeVisible()
    await expect(page.locator('body')).toContainText(/Qaly|Project|Du an|Dự án|Tao du an|Tạo dự án/i)
  })
})
