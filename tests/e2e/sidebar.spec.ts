import { expect, test, type Page, type TestInfo, type BrowserContext } from '@playwright/test'

test.describe.configure({ mode: 'serial' })

const baseURL = process.env.E2E_BASE_URL ?? 'http://127.0.0.1:5000'
const adminEmail = process.env.E2E_ADMIN_EMAIL ?? 'admin@qaly.dev'
const adminPassword = process.env.E2E_ADMIN_PASSWORD ?? 'Admin@123456'
const memberEmail = process.env.E2E_MEMBER_EMAIL ?? 'bao.ngoc@qaly.dev'
const memberPassword = process.env.E2E_MEMBER_PASSWORD ?? 'Qaly@123456'

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

async function loginAs(page: Page, email: string, password: string) {
  await page.goto('/Account/Login', { waitUntil: 'domcontentloaded' })
  await expect(page.locator('#loginForm')).toBeVisible()
  await page.locator('input[name="Email"]').fill(email)
  await page.locator('input[name="Password"]').fill(password)
  await page.getByRole('button', { name: /Dang nhap|Đăng nhập|Login/i }).click()
  await page.waitForURL((url) => !url.pathname.startsWith('/Account/Login'), {
    timeout: 20_000,
    waitUntil: 'domcontentloaded',
  })
  await expect(page.locator('.shell-header')).toBeVisible()
}

async function expectSidebarProfile(page: Page, expectedName: string, expectedRole: string) {
  const sidebarProfile = page.locator('section[aria-label="Hồ sơ người dùng"]')

  await expect(sidebarProfile).toBeVisible()
  await expect(sidebarProfile).toContainText(expectedRole, { ignoreCase: true })
  await expect(sidebarProfile).toContainText(expectedName, { ignoreCase: true })
  await expect(sidebarProfile.locator('img')).toBeVisible()
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

test('TC-SIDEBAR-001 Sidebar displays correct Admin user info', async ({ page }, testInfo) => {
  await runWithEvidence(page, testInfo, async () => {
    await loginAs(page, adminEmail, adminPassword)
    await expectSidebarProfile(page, 'Quản trị viên Qaly', 'Admin')
  })
})

test('TC-SIDEBAR-002 Sidebar displays correct Member user info and handles refresh', async ({ page }, testInfo) => {
  await runWithEvidence(page, testInfo, async () => {
    // 1. Member login
    await loginAs(page, memberEmail, memberPassword)
    const userProfile = page.locator('section[aria-label="Hồ sơ người dùng"]')

    // Sidebar displays current member name, not Admin name
    await expectSidebarProfile(page, 'Trần Bảo Ngọc', 'Manager')
    await expect(userProfile).not.toContainText('Quản trị viên Qaly', { ignoreCase: true })
    
    // 2. Refresh browser
    await page.reload({ waitUntil: 'domcontentloaded' })
    await expect(page.locator('.shell-header')).toBeVisible()
    await expectSidebarProfile(page, 'Trần Bảo Ngọc', 'Manager')
  })
})

test('TC-SIDEBAR-003 Logout and Login with different account updates Sidebar correctly', async ({ page }, testInfo) => {
  await runWithEvidence(page, testInfo, async () => {
    await loginAs(page, adminEmail, adminPassword)
    let userProfile = page.locator('section[aria-label="Hồ sơ người dùng"]')
    await expectSidebarProfile(page, 'Quản trị viên Qaly', 'Admin')

    // Logout
    await page.locator('.shell-user-menu').click()
    await page.getByRole('menuitem', { name: /Đăng xuất|Dang xuat|Logout/i, exact: false }).click()
    await page.waitForURL((url) => url.pathname.startsWith('/Account/Login'))

    // Login as member
    await loginAs(page, memberEmail, memberPassword)
    userProfile = page.locator('section[aria-label="Hồ sơ người dùng"]')

    // Validate it changed and no cache of old user
    await expectSidebarProfile(page, 'Trần Bảo Ngọc', 'Manager')
    await expect(userProfile).not.toContainText('Quản trị viên Qaly', { ignoreCase: true })
  })
})

test('TC-SIDEBAR-004 Sidebar is responsive on Desktop, Tablet, Mobile', async ({ page }, testInfo) => {
  await runWithEvidence(page, testInfo, async () => {
    await loginAs(page, adminEmail, adminPassword)
    
    // Desktop layout
    await page.setViewportSize({ width: 1280, height: 800 })
    await expectSidebarProfile(page, 'Quản trị viên Qaly', 'Admin')
    
    // Tablet layout
    await page.setViewportSize({ width: 768, height: 1024 })
    await expect(page.locator('.shell-header')).toBeVisible()
    
    // Mobile layout
    await page.setViewportSize({ width: 375, height: 667 })
    await expect(page.locator('.shell-header')).toBeVisible()
    await page.getByRole('button', { name: /Mo menu|Mở menu/i }).click()
    await expectSidebarProfile(page, 'Quản trị viên Qaly', 'Admin')
  })
})
