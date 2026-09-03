import { expect, test, type Page, type TestInfo } from '@playwright/test'
import { openSeededProject } from './support/seeded-project'

test.describe.configure({ mode: 'serial' })

const adminEmail = process.env.E2E_ADMIN_EMAIL ?? 'admin@qaly.dev'
const adminPassword = process.env.E2E_ADMIN_PASSWORD ?? 'Admin@123456'
const configuredDemoProject = process.env.E2E_DEMO_PROJECT?.trim()
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

test('DEMO-30M chạy xuyên suốt đúng kịch bản thuyết trình', async ({ page }, testInfo: TestInfo) => {
  test.setTimeout(180_000)

  const issues: BrowserIssue[] = []
  page.on('pageerror', error => issues.push({ kind: 'pageerror', message: error.message }))
  page.on('console', message => {
    if (message.type() === 'error') issues.push({ kind: 'console', message: message.text() })
  })
  page.on('response', response => {
    if (response.status() >= 500) {
      issues.push({ kind: 'http', message: `HTTP ${response.status()}`, url: response.url() })
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
  })

  await test.step(`10:30 — Chọn dự án mẫu “${demoProjectName}”`, async () => {
    await openDemoProject(page)
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
      expect(normalizedRoles, `Kịch bản demo phải có vai trò ${role}`).toContain(role)
    }
    // The role control renders Vietnamese labels (a <select> for manageable members, a badge
    // otherwise) — asserting the English role keys here never matched the rendered text.
    await expect(page.locator('.member-role-actions').first())
      .toContainText(/Quản lý dự án|Chủ dự án|Thành viên|Người xem|Khách hàng/i)
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

    await roadmap.getByRole('button', { name: /Timeline View/i }).click()
    await expect(roadmap).toContainText(/Dòng thời gian Roadmap|Không có task có ngày/i)
    await roadmap.getByRole('button', { name: /Journey View/i }).click()
    await roadmap.getByRole('button', { name: /Đang chạy/i }).click()
    await expect(roadmap.locator('.milestone-filter-group')).toBeVisible()
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
  })

  await test.step('21:00 — AI Assistant nhận prompt theo ngữ cảnh dự án', async () => {
    await page.getByRole('button', { name: /Mở Trợ lý AI/i }).first().click()
    const assistant = page.getByRole('dialog', { name: /Trợ lý AI/i }).or(page.locator('[aria-label="Trợ lý AI"]')).first()
    await expect(assistant).toBeVisible()
    const input = assistant.getByRole('textbox', { name: /Nhập yêu cầu.*Trợ lý AI/i })
    await input.fill('Phân tích tình trạng hiện tại của dự án này, xác định rủi ro tiến độ và đề xuất ba việc ưu tiên cho tuần tới.')
    await assistant.getByRole('button', { name: /Gửi câu hỏi/i }).click()
    await expect(assistant.locator('.msg-assistant').last()).toBeVisible({ timeout: 30_000 })
    await expect(assistant).toContainText(/Work Plan|Kế hoạch|nguồn|provider|mô hình|fallback|rủi ro/i, { timeout: 30_000 })
  })

  await test.step('24:30 — AI Native tạo Launch Brief để con người review, không xác nhận mutation', async () => {
    await page.goto('/projects', { waitUntil: 'domcontentloaded' })
    await page.getByRole('button', { name: /Lên kế hoạch AI/i }).click()
    const assistant = page.getByRole('dialog', { name: 'Trợ lý AI' })
    await expect(assistant).toBeVisible()
    const prompt = assistant.getByRole('textbox', { name: /Nhập yêu cầu.*Trợ lý AI/i })
    await expect(prompt).toHaveValue(/khởi chạy một dự án mới/i)
    await prompt.fill('Khởi chạy Project Qaly Next trong 2 tuần, gồm frontend, backend, kiểm thử và triển khai. Chỉ tạo Launch Brief để tôi review, chưa ghi dữ liệu.')
    await assistant.getByRole('button', { name: /Gửi câu hỏi/i }).click()

    const brief = assistant.getByTestId('project-launch-brief').last()
    await expect(brief).toBeVisible({ timeout: 45_000 })
    await expect(brief).toContainText(/Tóm tắt trước khi xác nhận|Lưu và lập phương án/i)
    const projectName = brief.getByLabel('Tên dự án')
    const originalName = await projectName.inputValue()
    await projectName.fill(`${originalName} — đã review`)
    await expect(brief.getByTestId('project-launch-brief-save')).toBeVisible()
    // Cố ý không lưu/lập phương án và không xác nhận mutation: đúng human-in-the-loop.
  })

  await testInfo.attach('browser-issues.json', {
    body: Buffer.from(JSON.stringify(issues, null, 2)),
    contentType: 'application/json',
  })

  // Chuyển route trong luồng demo sẽ hủy các fetch/SignalR nền của trang cũ.
  // Giữ chúng trong evidence nhưng không đánh nhầm thành lỗi sản phẩm.
  const actionableIssues = issues.filter(issue => {
    if (issue.kind === 'requestfailed' && issue.message.includes('net::ERR_ABORTED')) return false
    if (issue.kind === 'console' && /Failed to fetch|Failed to complete negotiation|Failed to start the connection/i.test(issue.message)) return false
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
