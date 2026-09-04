// `page.waitForResponse` resolves with a network Response (which carries `.request()`),
// not the APIResponse returned by `page.request.*`. Alias it so the DOM `Response` global
// stays available to the in-browser `page.evaluate` callbacks below.
import { expect, test, type Locator, type Page, type Response as NetworkResponse, type TestInfo } from '@playwright/test'
import { openSeededProject } from './support/seeded-project'

test.describe.configure({ mode: 'serial' })

const adminEmail = process.env.E2E_ADMIN_EMAIL ?? 'admin@qaly.dev'
const adminPassword = process.env.E2E_ADMIN_PASSWORD ?? 'Admin@123456'
const configuredDemoProject = process.env.E2E_DEMO_PROJECT?.trim()
const configuredAiModel = process.env.E2E_AI_MODEL?.trim()
const assistantSessionStorageKey = 'qaly.ai-native.active-session.v1'
const demoSessionResetMarker = 'qaly.e2e.demo-session-reset.v1'
// “Qaly Work OS - Customer Demo” was the pre-Release-4 seed name. Keep old
// developer shells and CI variables compatible after the canonical rename.
const demoProjectName = !configuredDemoProject || configuredDemoProject === 'Qaly Work OS - Customer Demo'
  ? 'Qaly Release 4.0'
  : configuredDemoProject

type BrowserIssue = {
  kind: 'console' | 'pageerror' | 'http' | 'requestfailed'
  message: string
  url?: string
}

type VisualHealth = {
  viewportWidth: number
  documentWidth: number
  brokenImages: string[]
}

async function captureVisualCheckpoint(page: Page, testInfo: TestInfo, name: string) {
  await page.evaluate(() => document.fonts.ready)
  // Give charts, lazy panels and CSS transitions one short frame to settle before capturing evidence.
  await page.waitForTimeout(250)

  const health = await page.evaluate<VisualHealth>(() => {
    const isVisible = (element: HTMLElement) => {
      const style = getComputedStyle(element)
      const box = element.getBoundingClientRect()
      return style.display !== 'none' && style.visibility !== 'hidden' && box.width > 0 && box.height > 0
    }

    const brokenImages = Array.from(document.images)
      .filter(image => isVisible(image) && Boolean(image.currentSrc || image.src) && image.complete && image.naturalWidth === 0)
      .map(image => image.currentSrc || image.src)

    return {
      viewportWidth: document.documentElement.clientWidth,
      documentWidth: Math.max(document.documentElement.scrollWidth, document.body.scrollWidth),
      brokenImages,
    }
  })

  await testInfo.attach(`${name}.png`, {
    body: await page.screenshot({ animations: 'disabled', caret: 'hide', fullPage: false }),
    contentType: 'image/png',
  })
  await testInfo.attach(`${name}-visual-health.json`, {
    body: Buffer.from(JSON.stringify(health, null, 2)),
    contentType: 'application/json',
  })

  expect(
    health.documentWidth,
    `${name}: giao diện tràn ngang ${health.documentWidth - health.viewportWidth}px ở viewport ${health.viewportWidth}px`,
  ).toBeLessThanOrEqual(health.viewportWidth + 1)
  expect(health.brokenImages, `${name}: có ảnh hiển thị nhưng tải thất bại`).toEqual([])
}

async function login(page: Page) {
  await page.goto('/Account/Login', { waitUntil: 'domcontentloaded' })
  await expect(page.locator('#loginForm')).toBeVisible()
  await page.locator('input[name="Email"]').fill(adminEmail)
  await page.locator('input[name="Password"]').fill(adminPassword)
  await page.getByRole('button', { name: /Đăng nhập|Dang nhap|Login/i }).click()
  await page.waitForURL(url => !url.pathname.startsWith('/Account/Login'), {
    timeout: 20_000,
    waitUntil: 'domcontentloaded',
  })
  await page.locator('.welcome-overlay').waitFor({ state: 'hidden', timeout: 5_000 }).catch(() => undefined)
  await expect(page.locator('.shell-header')).toBeVisible()
}

async function openDemoProject(page: Page) {
  // Resolve by id through the API: the /projects grid is paginated by recency, so a project
  // created by a spec running in parallel can push the seeded demo project off the first page.
  await openSeededProject(page, demoProjectName)
}

async function openInitializedAssistant(page: Page, open: () => Promise<void>) {
  // ErumiChatPanel restores the recent durable session when it is mounted. Wait for
  // that lifecycle to finish before clicking "Phiên mới"; otherwise the late restore
  // can race with the create request and put an old prompt beside the new response.
  const initialSession = page.waitForResponse(async response => {
    const url = new URL(response.url())
    if (response.request().method() === 'POST' && url.pathname === '/api/ai/assistant/sessions') {
      return response.ok()
    }
    const isRecentSession = url.pathname === '/api/ai/assistant/sessions/recent'
    const isSessionRead = /^\/api\/ai\/assistant\/sessions\/[0-9a-f-]{36}$/i.test(url.pathname)
    if (response.request().method() !== 'GET' || (!isRecentSession && !isSessionRead)) {
      return false
    }

    const payload = await response.json().catch(() => null) as { sessionId?: string } | null
    return response.ok() && Boolean(payload?.sessionId)
  })

  await open()
  const assistant = page.getByRole('dialog', { name: /Trợ lý AI/i })
    .or(page.locator('[aria-label="Trợ lý AI"]'))
    .first()
  await expect(assistant).toBeVisible()

  const response = await initialSession
  const session = await response.json() as { sessionId: string }
  await expect.poll(
    () => page.evaluate(key => window.localStorage.getItem(key), assistantSessionStorageKey),
    { message: 'Assistant phải hoàn tất khôi phục phiên ban đầu trước khi tạo phiên demo' },
  ).toBe(session.sessionId)

  return assistant
}

async function startFreshAssistantConversation(page: Page, assistant: Locator) {
  await assistant.getByTestId('assistant-session-history-toolbar').click()
  const createConversation = assistant.getByTestId('assistant-session-create')
  await expect(createConversation).toBeVisible()
  const created = page.waitForResponse(response => {
    const url = new URL(response.url())
    return url.pathname === '/api/ai/assistant/sessions' && response.request().method() === 'POST'
  })
  await createConversation.click()
  const createdResponse = await created
  expect(createdResponse.ok(), 'Phải tạo được phiên Trợ lý AI sạch cho checkpoint hiện tại').toBeTruthy()
  const createdSession = await createdResponse.json() as { sessionId: string }
  await expect.poll(
    () => page.evaluate(key => window.localStorage.getItem(key), assistantSessionStorageKey),
    { message: 'Phiên mới phải trở thành phiên Assistant đang hoạt động' },
  ).toBe(createdSession.sessionId)
  await expect(assistant.locator('.msg-user')).toHaveCount(0)
  await expect(assistant.getByTestId('project-launch-brief')).toHaveCount(0)
}

function waitForAssistantTurn(page: Page) {
  // The turn request stays open until the model finishes, so this wait has to match the
  // assistant budget rather than Playwright's 30s default for waitForResponse.
  return page.waitForResponse(response => {
    const url = new URL(response.url())
    return url.pathname === '/api/ai/assistant/turns' && response.request().method() === 'POST'
  }, { timeout: 240_000 })
}

async function expectAssistantTurnPersisted(page: Page, response: NetworkResponse, prompt: string) {
  expect(response.ok(), 'API Assistant phải hoàn tất lượt hiện tại').toBeTruthy()
  const request = response.request().postDataJSON() as { message?: string }
  expect(request.message, 'Request phải gửi đúng prompt đang hiển thị').toBe(prompt)

  const turn = await response.json() as { sessionId: string; clientTurnId: string }
  const storedResponse = await page.request.get(`/api/ai/assistant/sessions/${turn.sessionId}`)
  expect(storedResponse.ok(), 'Phải đọc lại được phiên Assistant vừa ghi').toBeTruthy()
  const storedSession = await storedResponse.json() as {
    turns?: Array<{ clientTurnId?: string; userMessage?: string }>
  }
  const storedTurn = storedSession.turns?.find(item => item.clientTurnId === turn.clientTurnId)
  expect(storedTurn?.userMessage, 'Database phải lưu đúng prompt của lượt hiện tại').toBe(prompt)

  return turn
}

test('DEMO-30M chạy xuyên suốt đúng kịch bản thuyết trình', async ({ page }, testInfo: TestInfo) => {
  // Two assistant turns run against the locally configured Ollama model on CPU, which is
  // far slower than a hosted provider. The budget covers both turns plus the walkthrough.
  test.setTimeout(600_000)

  await page.addInitScript(({ model, resetMarker, sessionKey }) => {
    if (!window.sessionStorage.getItem(resetMarker)) {
      window.localStorage.removeItem(sessionKey)
      window.sessionStorage.setItem(resetMarker, 'true')
    }
    if (model) window.localStorage.setItem('qaly.ai-native.model.v1', model)
  }, {
    model: configuredAiModel ?? null,
    resetMarker: demoSessionResetMarker,
    sessionKey: assistantSessionStorageKey,
  })

  testInfo.annotations.push({
    type: 'Target',
    description: process.env.E2E_BASE_URL ?? 'local Playwright web server (in-memory database)',
  })

  const issues: BrowserIssue[] = []
  const pendingIssueDetails: Promise<void>[] = []
  page.on('pageerror', error => issues.push({ kind: 'pageerror', message: error.message }))
  page.on('console', message => {
    if (message.type() === 'error') {
      const location = message.location()
      issues.push({ kind: 'console', message: message.text(), url: location.url || undefined })
    }
  })
  page.on('response', response => {
    if (response.status() >= 400) {
      const issue: BrowserIssue = { kind: 'http', message: `HTTP ${response.status()}`, url: response.url() }
      issues.push(issue)
      pendingIssueDetails.push(response.text()
        .then(body => {
          const compactBody = body.trim().replace(/\s+/g, ' ').slice(0, 500)
          if (compactBody) issue.message += ` — ${compactBody}`
        })
        .catch(() => undefined))
    }
  })
  page.on('requestfailed', request => {
    issues.push({
      kind: 'requestfailed',
      message: `${request.resourceType()}: ${request.failure()?.errorText ?? 'unknown failure'}`,
      url: request.url(),
    })
  })

  await test.step('10:00 — Đăng nhập và mở Dashboard', async () => {
    await login(page)
    await page.goto('/dashboard', { waitUntil: 'domcontentloaded' })
    await expect(page.locator('.shell-header')).toBeVisible()
    await expect(page.locator('body')).toContainText(/Dự án|Nhiệm vụ|Hoạt động/i)
    await captureVisualCheckpoint(page, testInfo, '01-dashboard')
  })

  await test.step(`10:30 — Chọn dự án mẫu “${demoProjectName}”`, async () => {
    await openDemoProject(page)
    await captureVisualCheckpoint(page, testInfo, '02-project-overview')
  })

  await test.step('11:00 — Kiểm tra thành viên và vai trò Manager/Member', async () => {
    await page.locator('.project-tabs').getByRole('button', { name: 'Thành viên' }).click()
    const members = page.locator('.member-item')
    await expect(members.first()).toBeVisible()
    // `DataSeeder.Demo` gives the demo project one account per project role on purpose, so the
    // permission matrix can be walked live. Assert that shape instead of an arbitrary head-count.
    const seededRoles = await members.evaluateAll(items =>
      items.map(item => {
        const select = item.querySelector<HTMLSelectElement>('.role-selector select')
        if (select) return select.value
        return item.querySelector('.role-badge')?.className.replace(/.*role-badge--/, '') ?? ''
      }),
    )
    const normalizedRoles = seededRoles.map(role => role.toLowerCase()).filter(Boolean)
    for (const role of ['owner', 'manager', 'scrummaster', 'developer', 'tester', 'reviewer', 'member', 'viewer', 'customer']) {
      // A stale real database should not stop evidence collection for every later screen.
      // Keep the final result red while allowing the presentation journey to continue.
      expect.soft(normalizedRoles, `Kịch bản demo phải có vai trò ${role}`).toContain(role)
    }
    // The role control renders Vietnamese labels (a <select> for manageable members, a badge
    // otherwise) — asserting the English role keys here never matched the rendered text.
    await expect(page.locator('.member-role-actions').first())
      .toContainText(/Quản lý dự án|Chủ dự án|Thành viên|Người xem|Khách hàng/i)
    await page.locator('.members-tab-content').evaluate(element => element.scrollIntoView({ block: 'start' }))
    await captureVisualCheckpoint(page, testInfo, '03-project-members')
  })

  await test.step('12:30 — Tìm task và mở chi tiết', async () => {
    await page.locator('.project-tabs').getByRole('button', { name: 'Nhiệm vụ' }).click()
    await expect(page.locator('#tasks')).toBeVisible()
    const allTasks = page.locator('.kanban-card, .task-list-row')
    expect(await allTasks.count(), 'Kịch bản yêu cầu 12–15 task').toBeGreaterThanOrEqual(12)
    expect(await allTasks.count(), 'Kịch bản yêu cầu 12–15 task').toBeLessThanOrEqual(15)
    const firstTask = page.locator('.kanban-card, .task-list-row').first()
    await expect(firstTask, 'Dự án demo phải có ít nhất một task').toBeVisible()
    const taskTitle = (await firstTask.locator('strong').first().innerText()).trim()
    const search = page.locator('.task-board-search input')
    await search.fill(taskTitle)
    await expect(page.locator('.kanban-card, .task-list-row').filter({ hasText: taskTitle }).first()).toBeVisible()
    await page.locator('.kanban-card, .task-list-row').filter({ hasText: taskTitle }).first().click()
    const detail = page.locator('.task-detail-drawer')
    await expect(detail).toBeVisible()
    await expect(detail).toContainText(new RegExp(taskTitle.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'), 'i'))
    await expect(detail).toContainText(/Trạng thái|Ưu tiên|Người phụ trách|Hạn|Bình luận/i)
    await expect(detail.getByText(/Đang tải/i)).toHaveCount(0, { timeout: 15_000 })
    await captureVisualCheckpoint(page, testInfo, '04-task-detail')
    await page.locator('.task-detail-drawer__close').click()
    await search.fill('')
  })

  await test.step('14:00 — Roadmap, Timeline, bộ lọc và chi tiết milestone', async () => {
    await page.locator('.project-tabs').getByRole('button', { name: /Lộ Trình Dự Án/i }).click()
    const roadmap = page.locator('.project-roadmap-shell')
    await expect(roadmap).toBeVisible()
    await expect(roadmap).toContainText(/Tổng quy mô Mốc|Tiến độ Nghiệm thu|Mốc Tập trung/i)

    const milestone = roadmap.locator('.milestone-node').first()
    await expect(milestone, 'Kịch bản yêu cầu dự án có milestone').toBeVisible()
    expect(await roadmap.locator('.milestone-node').count(), 'Kịch bản yêu cầu đúng 3 milestone').toBe(3)
    await milestone.click()
    await expect(roadmap).toContainText(/Mục tiêu|task|thành viên|ngày|trạng thái/i)

    await roadmap.getByRole('button', { name: /Dòng thời gian|Timeline View/i }).click()
    await expect(roadmap).toContainText(/Dòng thời gian Roadmap|Không có task có ngày/i)
    await roadmap.getByRole('button', { name: /Hành trình|Journey View/i }).click()
    await roadmap.getByRole('button', { name: /Đang chạy/i }).click()
    await expect(roadmap.locator('.milestone-filter-group')).toBeVisible()
    await captureVisualCheckpoint(page, testInfo, '05-project-roadmap')
  })

  await test.step('17:30 — GitHub: repository, PR, CI/CD và Release', async () => {
    await page.locator('.project-tabs').getByRole('button', { name: 'GitHub' }).click()
    const github = page.locator('.tab-pane').filter({ hasText: 'GitHub' }).last()
    await expect(github).toBeVisible()
    await expect(github).toContainText(/GitHub đang hoạt động/i)
    await github.getByRole('button', { name: /Pull requests\s*3/i }).click()
    await expect(github.locator('.item-card')).toHaveCount(3)
    await expect(github).toContainText(/feature\/release-4-roadmap.*→.*main/i)
    await github.getByRole('button', { name: /CI\/CD\s*2/i }).click()
    await expect(github.locator('.item-card')).toHaveCount(2)
    await expect(github).toContainText(/Build & E2E Release 4.0/i)
    await github.getByRole('button', { name: /Releases\s*1/i }).click()
    await expect(github.locator('.item-card')).toHaveCount(1)
    await expect(github).toContainText(/Qaly Release 4.0 RC1/i)
    await captureVisualCheckpoint(page, testInfo, '06-github-release')
  })

  await test.step('21:00 — AI Assistant nhận prompt theo ngữ cảnh dự án', async () => {
    const assistant = await openInitializedAssistant(
      page,
      () => page.getByRole('button', { name: /Mở Trợ lý AI/i }).first().click(),
    )
    await startFreshAssistantConversation(page, assistant)
    const input = assistant.getByRole('textbox', { name: /Nhập yêu cầu.*Trợ lý AI/i })
    const assistantAnswers = assistant.locator('.msg-assistant .assistant-primary-answer')
    const analysisPrompt = 'Phân tích tình trạng hiện tại của dự án này, xác định rủi ro tiến độ và đề xuất ba việc ưu tiên cho tuần tới.'
    await input.fill(analysisPrompt)
    const analysisTurn = waitForAssistantTurn(page)
    await assistant.getByRole('button', { name: /Gửi câu hỏi/i }).click()
    const analysisResponse = await analysisTurn
    await expectAssistantTurnPersisted(page, analysisResponse, analysisPrompt)
    await expect(assistant.locator('.typing-loader')).toHaveCount(0, { timeout: 240_000 })

    const submittedPrompt = assistant.locator('.msg-user').filter({ hasText: analysisPrompt })
    const promptPersisted = await submittedPrompt.last().waitFor({ state: 'visible', timeout: 10_000 })
      .then(() => true)
      .catch(() => false)
    expect.soft(promptPersisted, 'Phiên mới phải hiển thị đúng prompt phân tích vừa gửi').toBeTruthy()

    const answerCount = await assistantAnswers.count()
    expect.soft(answerCount, 'Phiên mới phải có phản hồi riêng cho prompt phân tích').toBeGreaterThan(0)
    const latestAnswer = assistantAnswers.last()
    // Keep walking the remaining presentation even when this checkpoint is unhealthy, so the
    // report still contains evidence for the Launch Brief screen. The test remains red at the end.
    if (answerCount > 0) {
      await expect.soft(latestAnswer, 'AI phải trả về nội dung phân tích, không chỉ trạng thái lỗi/chưa hoàn tất')
        .toContainText(/Work Plan|Kế hoạch|nguồn|provider|mô hình|fallback|rủi ro/i)
      await latestAnswer.scrollIntoViewIfNeeded()
    }
    await captureVisualCheckpoint(page, testInfo, '07-ai-project-analysis')
  })

  await test.step('24:30 — AI Native tạo Launch Brief để con người review, không xác nhận mutation', async () => {
    await page.goto('/projects', { waitUntil: 'domcontentloaded' })
    const assistant = await openInitializedAssistant(
      page,
      () => page.getByRole('button', { name: /Lên kế hoạch AI/i }).click(),
    )
    await startFreshAssistantConversation(page, assistant)
    const prompt = assistant.getByRole('textbox', { name: /Nhập yêu cầu.*Trợ lý AI/i })
    const launchPrompt = 'Khởi chạy Project Qaly Next trong 2 tuần, gồm frontend, backend, kiểm thử và triển khai. Chỉ tạo Launch Brief để tôi review, chưa ghi dữ liệu.'
    await prompt.fill(launchPrompt)
    const launchTurnPromise = waitForAssistantTurn(page)
    await assistant.getByRole('button', { name: /Gửi câu hỏi/i }).click()
    const launchTurn = await launchTurnPromise
    await expectAssistantTurnPersisted(page, launchTurn, launchPrompt)
    await expect(assistant.locator('.typing-loader')).toHaveCount(0, { timeout: 240_000 })

    const submittedPrompt = assistant.locator('.msg-user').filter({ hasText: launchPrompt })
    const promptPersisted = await submittedPrompt.last().waitFor({ state: 'visible', timeout: 10_000 })
      .then(() => true)
      .catch(() => false)
    expect.soft(promptPersisted, 'Phiên mới phải hiển thị đúng prompt Launch Brief vừa gửi').toBeTruthy()

    const brief = assistant.getByTestId('project-launch-brief').last()
    const briefAppeared = await brief.waitFor({ state: 'visible', timeout: launchTurn.ok() ? 45_000 : 5_000 })
      .then(() => true)
      .catch(() => false)
    expect.soft(briefAppeared, 'Prompt hiện tại phải tạo một Launch Brief mới').toBeTruthy()
    if (briefAppeared) {
      await expect(brief).toContainText(/Tóm tắt trước khi xác nhận|Lưu và lập phương án/i)
      const projectName = brief.getByLabel('Tên dự án')
      await expect.soft(projectName).toHaveValue(/Qaly Next/i)
      const originalName = await projectName.inputValue()
      await projectName.fill(`${originalName} — đã review`)
      await expect(brief.getByTestId('project-launch-brief-save')).toBeVisible()
      await brief.locator('.project-launch-brief-header').scrollIntoViewIfNeeded()
    }
    await captureVisualCheckpoint(page, testInfo, '08-ai-launch-brief')
    // Cố ý không lưu/lập phương án và không xác nhận mutation: đúng human-in-the-loop.
  })

  await Promise.allSettled(pendingIssueDetails)

  await testInfo.attach('browser-issues.json', {
    body: Buffer.from(JSON.stringify(issues, null, 2)),
    contentType: 'application/json',
  })

  // Chuyển route trong luồng demo sẽ hủy các fetch/SignalR nền của trang cũ.
  // Giữ chúng trong evidence nhưng không đánh nhầm thành lỗi sản phẩm.
  const actionableIssues = issues.filter(issue => {
    if (issue.kind === 'requestfailed' && issue.message.includes('net::ERR_ABORTED')) return false
    if (issue.kind === 'console' && /Failed to fetch|Failed to complete negotiation|Failed to start the connection/i.test(issue.message)) return false
    if (issue.kind === 'console' && /Failed to load resource/i.test(issue.message) && issue.url
      && issues.some(candidate => candidate.kind === 'http' && candidate.url === issue.url)) return false
    return true
  })

  await testInfo.attach('actionable-browser-issues.json', {
    body: Buffer.from(JSON.stringify(actionableIssues, null, 2)),
    contentType: 'application/json',
  })
  await testInfo.attach('demo-final-state.png', {
    body: await page.screenshot({ fullPage: true }),
    contentType: 'image/png',
  })
  expect(actionableIssues, `Phát hiện lỗi trình duyệt/API:\n${actionableIssues.map(x => `${x.kind}: ${x.message} ${x.url ?? ''}`).join('\n')}`).toEqual([])
})
