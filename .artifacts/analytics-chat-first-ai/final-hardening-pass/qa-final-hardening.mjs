import { chromium } from 'playwright'
import { mkdir, writeFile } from 'node:fs/promises'
import path from 'node:path'

const baseUrl = process.env.QALY_BASE_URL || 'http://localhost:5000'
const outDir = path.resolve('.artifacts/analytics-chat-first-ai/final-hardening-pass')
const historyKey = 'qaly.analytics.erumi.history.v1'

const results = {
  startedAt: new Date().toISOString(),
  baseUrl,
  screenshots: {},
  checks: [],
  consoleErrors: [],
  knownEnvironmentConsoleErrors: [],
  pageErrors: [],
  notes: []
}

function addCheck(name, pass, details = {}) {
  results.checks.push({ name, pass: Boolean(pass), details })
}

function screenshotPath(name) {
  const filePath = path.join(outDir, name)
  results.screenshots[name] = filePath
  return filePath
}

function isKnownEnvironmentConsoleError(message) {
  return (
    message.includes('Failed to complete negotiation with the server') ||
    message.includes('Failed to start the connection') ||
    (message.includes('TypeError: Failed to fetch') && message.includes('DashboardPage.js'))
  )
}

async function firstVisible(page, selectors) {
  for (const selector of selectors) {
    const locator = page.locator(selector).first()
    if (await locator.count()) {
      return locator
    }
  }
  return null
}

async function ensureLoggedIn(page, targetPath = '/analytics') {
  await page.goto(`${baseUrl}${targetPath}`, { waitUntil: 'domcontentloaded' })

  if (page.url().includes('/Account/Login') || (await page.locator('input[type="password"]').count())) {
    const email = await firstVisible(page, ['input[type="email"]', 'input[name="Email"]', '#Email'])
    const password = await firstVisible(page, ['input[type="password"]', 'input[name="Password"]', '#Password'])
    if (!email || !password) {
      throw new Error('Login form was shown but email/password fields were not found.')
    }

    await email.fill(process.env.QALY_ADMIN_EMAIL || 'admin@qaly.dev')
    await password.fill(process.env.QALY_ADMIN_PASSWORD || 'Admin@123456')
    await Promise.all([
      page.waitForURL(url => !String(url).includes('/Account/Login'), { timeout: 20000 }).catch(() => null),
      page.locator('button[type="submit"], input[type="submit"]').first().click()
    ])
    await page.goto(`${baseUrl}${targetPath}`, { waitUntil: 'domcontentloaded' })
  }
}

async function waitForAnalytics(page) {
  await page.locator('.analytics-chat-portal').waitFor({ state: 'visible', timeout: 20000 })
}

async function waitForDashboard(page) {
  await page.locator('.shell-main').waitFor({ state: 'visible', timeout: 20000 })
}

async function capture(page, name) {
  await page.screenshot({ path: screenshotPath(name), fullPage: false })
}

async function pageMetrics(page) {
  return page.evaluate(() => {
    const doc = document.documentElement
    const body = document.body
    const visibleFileInputs = Array.from(document.querySelectorAll('input[type="file"]')).filter(el => {
      const rect = el.getBoundingClientRect()
      const style = getComputedStyle(el)
      return style.display !== 'none' && style.visibility !== 'hidden' && rect.width > 0 && rect.height > 0
    })

    return {
      url: location.href,
      width: innerWidth,
      height: innerHeight,
      overflowX: Math.max(doc.scrollWidth, body?.scrollWidth || 0) - doc.clientWidth,
      nativeFileInputVisible: visibleFileInputs.length > 0,
      analyticsPortal: Boolean(document.querySelector('.analytics-chat-portal')),
      floatingChatbot: Boolean(document.querySelector('.global-erumi-chatbot-widget')),
      analyticsToolbar: Boolean(document.querySelector('.ai-quick-toolbar')),
      drawerCount: document.querySelectorAll('.analytics-drawer-shell').length
    }
  })
}

async function assertNoHorizontalOverflow(page, name) {
  const metrics = await pageMetrics(page)
  addCheck(name, metrics.overflowX <= 2, metrics)
}

async function assertVisibleWithinViewport(page, selector, name) {
  const locator = page.locator(selector).first()
  await locator.waitFor({ state: 'visible', timeout: 8000 })
  await page.waitForTimeout(250)
  const rect = await locator.boundingBox()
  const viewport = page.viewportSize()
  const pass = Boolean(
    rect &&
    viewport &&
    rect.x >= -1 &&
    rect.y >= -1 &&
    rect.x + rect.width <= viewport.width + 1 &&
    rect.y + rect.height <= viewport.height + 1
  )
  addCheck(name, pass, { rect, viewport })
}

async function closeTransientUi(page) {
  await page.keyboard.press('Escape').catch(() => null)
  const closeButton = page.locator('.analytics-drawer-close').first()
  if (await closeButton.count()) {
    await closeButton.click().catch(() => null)
  }
  await page.waitForTimeout(150)
}

async function sendAnalyticsPrompt(page) {
  const input = page.locator('.erumi-message-input').first()
  await input.waitFor({ state: 'visible', timeout: 10000 })

  await input.fill('/risks')
  await page.locator('.slash-commands-popup').waitFor({ state: 'visible', timeout: 5000 })
  await assertVisibleWithinViewport(page, '.slash-commands-popup', 'slash command popup stays in viewport')
  await input.fill('')

  await input.fill('Dòng 1')
  await input.press('Shift+Enter')
  await input.type('Dòng 2')
  const inputValue = await input.inputValue()
  addCheck('Shift+Enter inserts newline', inputValue.includes('\n'), { inputValue })

  await input.fill('Đánh giá hiệu suất tuần qua')
  const beforeUserMessages = await page.locator('.msg-bubble-user').count()
  await input.press('Enter')
  await page.waitForFunction(
    before => document.querySelectorAll('.msg-bubble-user').length > before,
    beforeUserMessages,
    { timeout: 10000 }
  )
  await page.waitForFunction(
    () => !document.querySelector('.typing-loader'),
    null,
    { timeout: 30000 }
  ).catch(() => null)
  addCheck('Enter sends a chat prompt', (await page.locator('.msg-bubble-user').count()) > beforeUserMessages)
}

async function clickToolbarByText(page, selector, text) {
  const locator = page.locator(selector).filter({ hasText: text }).first()
  await locator.waitFor({ state: 'visible', timeout: 8000 })
  await locator.click()
}

async function run() {
  await mkdir(outDir, { recursive: true })

  const browser = await chromium.launch({ headless: true })
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } })
  const page = await context.newPage()

  page.on('console', msg => {
    if (msg.type() === 'error') results.consoleErrors.push(msg.text())
  })
  page.on('pageerror', err => results.pageErrors.push(err.message))

  try {
    const manifest = await page.request.get(`${baseUrl}/dist/.vite/manifest.json?qa=${Date.now()}`)
    addCheck('ASP.NET static dist manifest is served', manifest.ok(), { status: manifest.status() })

    await ensureLoggedIn(page, '/analytics')
    await waitForAnalytics(page)
    await page.evaluate(key => window.localStorage.removeItem(key), historyKey)
    await page.reload({ waitUntil: 'domcontentloaded' })
    await waitForAnalytics(page)
    await capture(page, 'analytics-desktop-final.png')
    await assertNoHorizontalOverflow(page, 'desktop analytics has no horizontal overflow')

    const metrics = await pageMetrics(page)
    addCheck('native file input is hidden', !metrics.nativeFileInputVisible, metrics)

    await sendAnalyticsPrompt(page)
    await capture(page, 'analytics-desktop-active-chat-final.png')
    await assertNoHorizontalOverflow(page, 'active desktop chat has no horizontal overflow')

    await clickToolbarByText(page, '.ai-quick-utility', 'Công cụ')
    await assertVisibleWithinViewport(page, '.ai-tool-palette', 'tool palette stays in viewport')
    await capture(page, 'analytics-desktop-tool-palette-final.png')
    await closeTransientUi(page)

    await page.locator('.erumi-project-trigger').first().click()
    await assertVisibleWithinViewport(page, '.erumi-dropdown-menu', 'project dropdown stays in viewport')
    await capture(page, 'analytics-desktop-project-dropdown-final.png')
    await closeTransientUi(page)

    await page.locator('.ai-model-trigger').first().click()
    await assertVisibleWithinViewport(page, '.ai-model-menu', 'model selector stays in viewport')
    await closeTransientUi(page)

    await clickToolbarByText(page, '.ai-quick-utility', 'Lịch sử')
    await assertVisibleWithinViewport(page, '.analytics-drawer-panel', 'history drawer stays in viewport')
    await capture(page, 'analytics-desktop-history-drawer-final.png')
    await closeTransientUi(page)

    await clickToolbarByText(page, '.ai-quick-tool', 'Nguồn')
    await assertVisibleWithinViewport(page, '.analytics-drawer-panel', 'sources drawer stays in viewport')
    await capture(page, 'analytics-desktop-sources-drawer-final.png')
    await closeTransientUi(page)

    await page.setViewportSize({ width: 834, height: 1112 })
    await page.goto(`${baseUrl}/analytics`, { waitUntil: 'domcontentloaded' })
    await waitForAnalytics(page)
    await capture(page, 'analytics-tablet-final.png')
    await assertNoHorizontalOverflow(page, 'tablet analytics has no horizontal overflow')

    await page.setViewportSize({ width: 390, height: 844 })
    await page.goto(`${baseUrl}/analytics`, { waitUntil: 'domcontentloaded' })
    await waitForAnalytics(page)
    await capture(page, 'analytics-mobile-final.png')
    await assertNoHorizontalOverflow(page, 'mobile analytics has no horizontal overflow')

    await page.setViewportSize({ width: 1440, height: 900 })
    await page.goto(`${baseUrl}/dashboard`, { waitUntil: 'domcontentloaded' })
    await waitForDashboard(page)
    await page.locator('.erumi-bubble-trigger').waitFor({ state: 'visible', timeout: 12000 })
    await capture(page, 'dashboard-floating-chatbot-final.png')
    const dashboardMetrics = await pageMetrics(page)
    addCheck('dashboard floating chatbot remains available', dashboardMetrics.floatingChatbot, dashboardMetrics)
    addCheck('analytics cockpit toolbar does not leak onto dashboard', !dashboardMetrics.analyticsToolbar, dashboardMetrics)

    await page.goto(`${baseUrl}/analytics`, { waitUntil: 'domcontentloaded' })
    await waitForAnalytics(page)
    await page.evaluate(key => window.localStorage.setItem(key, '{bad-json'), historyKey)
    await page.reload({ waitUntil: 'domcontentloaded' })
    await waitForAnalytics(page)
    await page.reload({ waitUntil: 'domcontentloaded' })
    await waitForAnalytics(page)
    await capture(page, 'reload-verified-final.png')
    await assertNoHorizontalOverflow(page, 'reload after corrupt history has no horizontal overflow')
    addCheck('corrupt localStorage history does not crash analytics', await page.locator('.analytics-chat-portal').isVisible())

    results.knownEnvironmentConsoleErrors = results.consoleErrors.filter(isKnownEnvironmentConsoleError)
    const actionableConsoleErrors = results.consoleErrors.filter(message => !isKnownEnvironmentConsoleError(message))
    addCheck('no actionable browser console errors during analytics QA', actionableConsoleErrors.length === 0, {
      actionableConsoleErrors,
      knownEnvironmentConsoleErrors: results.knownEnvironmentConsoleErrors
    })
    addCheck('no uncaught page errors during QA', results.pageErrors.length === 0, { pageErrors: results.pageErrors })
  } finally {
    await browser.close()
    results.finishedAt = new Date().toISOString()
    results.failedChecks = results.checks.filter(check => !check.pass)
    await writeFile(path.join(outDir, 'qa-results.json'), `${JSON.stringify(results, null, 2)}\n`, 'utf8')
  }
}

run().catch(async error => {
  results.fatalError = error?.stack || String(error)
  results.finishedAt = new Date().toISOString()
  results.failedChecks = results.checks.filter(check => !check.pass)
  await mkdir(outDir, { recursive: true })
  await writeFile(path.join(outDir, 'qa-results.json'), `${JSON.stringify(results, null, 2)}\n`, 'utf8')
  console.error(error)
  process.exit(1)
})
