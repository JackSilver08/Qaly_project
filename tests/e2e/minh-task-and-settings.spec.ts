import { expect, test, type Page } from '@playwright/test'

test.describe.configure({ mode: 'serial' })

const adminEmail = process.env.E2E_ADMIN_EMAIL ?? 'admin@qaly.dev'
const adminPassword = process.env.E2E_ADMIN_PASSWORD ?? 'Admin@123456'

function uniqueName(prefix: string) {
  return `${prefix} ${Date.now()} ${Math.random().toString(36).slice(2, 8)}`
}

type ApiResult<T> = {
  isSuccess: boolean
  data: T | null
  error: string | null
}

async function responseBody(response: { json?(): Promise<unknown>; text(): Promise<string> }) {
  try {
    return await response.json?.()
  } catch {
    const text = await response.text()
    if (!text.trim()) return null

    try {
      return JSON.parse(text) as unknown
    } catch {
      return text
    }
  }
}

async function apiResult<T>(response: { ok(): boolean; status(): number; url(): string; json?(): Promise<unknown>; text(): Promise<string> }): Promise<T> {
  const body = await responseBody(response)
  expect(response.ok(), `API ${response.url()} trả về ${response.status()}`).toBeTruthy()

  if (body && typeof body === 'object' && 'isSuccess' in body) {
    const result = body as ApiResult<T>
    expect(result.isSuccess, result.error ?? 'API result failed').toBeTruthy()
    expect(result.data, 'API result must include data').not.toBeNull()
    return result.data as T
  }

  return body as T
}

async function login(page: Page) {
  await page.goto('/Account/Login', { waitUntil: 'domcontentloaded' })
  await expect(page.locator('#loginForm')).toBeVisible()
  await page.locator('input[name="Email"]').fill(adminEmail)
  await page.locator('input[name="Password"]').fill(adminPassword)
  await page.getByRole('button', { name: /Đăng nhập|Login/i }).click()
  await page.waitForURL((url) => !url.pathname.startsWith('/Account/Login'), {
    timeout: 20_000,
    waitUntil: 'domcontentloaded',
  })
  await page.locator('.welcome-overlay').waitFor({ state: 'hidden', timeout: 5_000 }).catch(() => undefined)
  await expect(page.locator('.shell-header')).toBeVisible()
}

async function createProject(page: Page, name: string) {
  return apiResult<{ id: string; name: string }>(await page.request.post('/api/projects', {
    data: {
      name,
      code: null,
      description: 'E2E project for Minh task/settings coverage',
      logoUrl: null,
      startDate: null,
      endDate: null,
      organizationId: null,
      sourceGroupId: null,
    },
  }))
}

async function createTask(page: Page, projectId: string, title: string) {
  return apiResult<{ id: string; title: string }>(await page.request.post('/api/tasks', {
    data: {
      title,
      description: 'E2E task for canonical navigation',
      priority: 'Medium',
      dueDate: null,
      estimatedHours: null,
      projectId,
      assigneeId: null,
      isPrivate: false,
      isPinned: false,
      contributesToProgress: true,
      assigneeIds: [],
      labelIds: [],
    },
  }))
}

test('canonical task opens from project and tasks pages, refresh and back keep the canonical URL', async ({ page }) => {
  let projectId = ''
  let taskId = ''
  const projectName = uniqueName('E2E Canonical Project')
  const taskTitle = uniqueName('E2E Canonical Task')

  try {
    await login(page)
    const project = await createProject(page, projectName)
    projectId = project.id
    const task = await createTask(page, projectId, taskTitle)
    taskId = task.id

    await page.goto(`/projects/${projectId}`, { waitUntil: 'domcontentloaded' })
    await expect(page.getByRole('heading', { name: projectName })).toBeVisible()
    await page.getByRole('button', { name: /Nhiệm vụ/i }).click()
    await page.locator('.kanban-card, .task-list-row').filter({ hasText: taskTitle }).first().click()

    await expect(page).toHaveURL(new RegExp(`/projects/${projectId}/tasks/${taskId}$`))
    await expect(page.locator('.task-detail-drawer')).toContainText(taskTitle)

    await page.reload({ waitUntil: 'domcontentloaded' })
    await expect(page).toHaveURL(new RegExp(`/projects/${projectId}/tasks/${taskId}$`))
    await expect(page.locator('.task-detail-drawer')).toContainText(taskTitle)

    await page.goBack({ waitUntil: 'domcontentloaded' })
    await expect(page).toHaveURL(new RegExp(`/projects/${projectId}$`))

    await page.goto('/tasks', { waitUntil: 'domcontentloaded' })
    await expect(page.locator('.task-card').filter({ hasText: taskTitle }).first()).toBeVisible()
    await page.locator('.task-card').filter({ hasText: taskTitle }).first().click()
    await expect(page).toHaveURL(new RegExp(`/projects/${projectId}/tasks/${taskId}$`))
    await expect(page.locator('.task-detail-drawer')).toContainText(taskTitle)
  } finally {
    if (taskId) {
      await page.request.delete(`/api/tasks/${taskId}`).catch(() => undefined)
    }
    if (projectId) {
      await page.request.delete(`/api/projects/${projectId}`).catch(() => undefined)
    }
  }
})

test('privacy policy writes through API and Settings stays truthful on partial failure', async ({ page }) => {
  let projectId = ''
  const projectName = uniqueName('E2E Privacy Project')
  const policyName = uniqueName('E2E Privacy Policy')
  const renamedUser = uniqueName('E2E Settings User')

  try {
    await login(page)
    const project = await createProject(page, projectName)
    projectId = project.id

    await page.goto(`/projects/${projectId}`, { waitUntil: 'domcontentloaded' })
    await page.goto('/settings', { waitUntil: 'domcontentloaded' })

    await page.route(new RegExp(`/api/projects/${projectId}$`), async route => {
      if (route.request().method() === 'PUT') {
        await route.fulfill({
          status: 500,
          contentType: 'application/json',
          body: JSON.stringify({ error: 'Injected project settings failure' }),
        })
        return
      }

      await route.continue()
    })

    await page.getByLabel('Họ và tên').fill(renamedUser)
    await page.locator('.settings-nav-item').filter({ hasText: 'Quyền riêng tư & dữ liệu' }).click()
    await page.getByRole('button', { name: /Retention/i }).click()
    await page.locator('.privacy-editor input').first().fill(policyName)
    await page.getByRole('button', { name: /Tạo policy/i }).click()

    await expect(page.locator('.record-list')).toContainText(policyName)
    await page.reload({ waitUntil: 'domcontentloaded' })
    await page.locator('.settings-nav-item').filter({ hasText: 'Quyền riêng tư & dữ liệu' }).click()
    await page.getByRole('button', { name: /Retention/i }).click()
    await expect(page.locator('.record-list')).toContainText(policyName)

    await page.goto('/settings', { waitUntil: 'domcontentloaded' })
    await page.getByLabel('Họ và tên').fill(renamedUser + ' v2')
    await page.getByRole('button', { name: /Lưu cài đặt/i }).click()

    await expect(page.locator('.toast-card')).toContainText('Không thể cập nhật cấu hình bảng công việc.')
    await expect(page.locator('.toast-card')).not.toContainText('Đã lưu tất cả cấu hình thành công!')
  } finally {
    if (projectId) {
      await page.request.delete(`/api/projects/${projectId}`).catch(() => undefined)
    }
  }
})
