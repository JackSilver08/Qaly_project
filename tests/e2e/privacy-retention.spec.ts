import { expect, test, type Browser, type Page, type TestInfo } from '@playwright/test'
import { browserApiRequest, type BrowserApiResponse } from './support/browser-api'
import { adminEmail, adminPassword } from './support/credentials'

test.describe.configure({ mode: 'serial' })
test.setTimeout(120_000)

type ApiResult<T> = {
  isSuccess: boolean
  data: T | null
  error: string | null
  statusCode?: number
}

type ProjectDto = {
  id: string
  name: string
  code: string
}

type TaskItemDto = {
  id: string
  title: string
  projectId: string
  isPrivate: boolean
}

type GroupDto = {
  id: string
  name: string
}

type RetentionPolicyDto = {
  id: string
  name: string
}

type PrivacyConsentDto = {
  id: string
  projectId: string
  sourceEntityId: string | null
  retentionPolicyId: string
  noticeVersion: string
}

function uniqueName(prefix: string) {
  return `${prefix} ${Date.now()} ${Math.random().toString(36).slice(2, 8)}`
}

async function responseBody(response: BrowserApiResponse) {
  const text = await response.text()
  if (!text.trim()) return null

  try {
    return JSON.parse(text) as unknown
  } catch {
    return text
  }
}

async function apiResult<T>(response: BrowserApiResponse): Promise<T> {
  const body = await responseBody(response)
  expect(response.ok(), `API ${response.url()} returned ${response.status()}: ${JSON.stringify(body)}`).toBeTruthy()

  if (body && typeof body === 'object' && 'isSuccess' in body) {
    const result = body as ApiResult<T>
    expect(result.isSuccess, result.error ?? 'API result failed').toBeTruthy()
    expect(result.data, 'API result must include data').not.toBeNull()
    return result.data as T
  }

  return body as T
}

async function csrfToken(page: Page) {
  const payload = await apiResult<{ token: string }>(await browserApiRequest(page, 'GET', '/api/security/csrf'))
  return payload.token
}

async function login(page: Page, returnUrl = '/dashboard') {
  await page.goto(`/Account/Login?returnUrl=${encodeURIComponent(returnUrl)}`, { waitUntil: 'domcontentloaded' })
  await expect(page.locator('#loginForm')).toBeVisible()
  await page.locator('input[name="Email"]').fill(adminEmail)
  await page.locator('input[name="Password"]').fill(adminPassword)
  await page.getByRole('button', { name: /Dang nhap|Đăng nhập|Login/i }).click()
  await page.waitForURL((url) => !url.pathname.startsWith('/Account/Login'), {
    timeout: 25_000,
    waitUntil: 'domcontentloaded',
  })
  await page.locator('.welcome-overlay').waitFor({ state: 'hidden', timeout: 5_000 }).catch(() => undefined)
  await expect(page.locator('.shell-header')).toBeVisible()
}

async function createProject(page: Page, name: string, sourceGroupId: string | null = null) {
  return apiResult<ProjectDto>(
    await browserApiRequest(page, 'POST', '/api/projects', {
      data: {
        name,
        code: null,
        description: 'T1-TR-01 E2E project',
        logoUrl: null,
        startDate: null,
        endDate: null,
        organizationId: null,
        sourceGroupId,
      },
    }),
  )
}

async function createTask(page: Page, projectId: string, title: string) {
  return apiResult<TaskItemDto>(
    await browserApiRequest(page, 'POST', '/api/tasks', {
      data: {
        title,
        description: 'Direct Task URL read-back fixture',
        priority: 'High',
        dueDate: null,
        estimatedHours: null,
        projectId,
        assigneeId: null,
        isPrivate: true,
        isPinned: false,
        contributesToProgress: true,
        assigneeIds: null,
        labelIds: null,
      },
    }),
  )
}

async function createGroup(page: Page, name: string) {
  return apiResult<GroupDto>(
    await browserApiRequest(page, 'POST', '/api/groups', {
      data: {
        name,
        color: '#2563eb',
      },
    }),
  )
}

async function createRetentionPolicy(page: Page, projectId: string, name: string) {
  return apiResult<RetentionPolicyDto>(
    await browserApiRequest(page, 'POST', '/api/privacy/policies', {
      headers: { 'X-CSRF-TOKEN': await csrfToken(page) },
      data: {
        tenantId: projectId,
        projectId,
        name,
        dataClassification: 'sensitive_collaboration',
        purpose: 'meeting_action_extraction',
        allowedRetentionDays: [30, 90],
        defaultRetentionDays: 30,
        expiryAction: 'redact',
        allowCloudProcessing: true,
        allowLocalProcessing: true,
        requireExplicitConsent: true,
        approvalOwnerUserId: null,
        effectiveFrom: null,
        effectiveUntil: null,
      },
    }),
  )
}

async function cleanup(page: Page, projectId?: string, groupId?: string) {
  if (groupId) await browserApiRequest(page, 'DELETE', `/api/groups/${groupId}`).catch(() => undefined)
  if (projectId) await browserApiRequest(page, 'DELETE', `/api/projects/${projectId}`).catch(() => undefined)
}

async function captureFailure(page: Page, testInfo: TestInfo) {
  await page.screenshot({ path: testInfo.outputPath('failure.png'), fullPage: true }).catch(() => undefined)
}

test('T1-TR-01 Allow: direct Task URL, Group link and Privacy UI use persisted data', async ({ page }, testInfo) => {
  let project: ProjectDto | undefined
  let group: GroupDto | undefined

  try {
    await login(page)
    group = await createGroup(page, uniqueName('T1TR01 Group'))
    project = await createProject(page, uniqueName('T1TR01 Allow Project'), group.id)
    const task = await createTask(page, project.id, uniqueName('T1TR01 private task'))
    const policy = await createRetentionPolicy(page, project.id, uniqueName('T1TR01 meeting policy'))

    const taskReadBack = await apiResult<TaskItemDto>(await browserApiRequest(page, 'GET', `/api/tasks/${task.id}`))
    expect(taskReadBack).toMatchObject({ id: task.id, title: task.title, projectId: project.id, isPrivate: true })

    const groupReadBack = await apiResult<GroupDto>(await browserApiRequest(page, 'GET', `/api/groups/${group.id}`))
    expect(groupReadBack).toMatchObject({ id: group.id, name: group.name })

    const policyReadBack = await apiResult<RetentionPolicyDto[]>(
      await browserApiRequest(page, 'GET', `/api/privacy/policies?tenantId=${project.id}&projectId=${project.id}`),
    )
    expect(policyReadBack.map((item) => item.id)).toContain(policy.id)

    await page.goto(`/projects/${project.id}/tasks/${task.id}`, { waitUntil: 'domcontentloaded' })
    const drawer = page.locator('.task-detail-drawer')
    await expect(drawer).toBeVisible()
    await expect(drawer).toContainText(task.title)
    await expect(page.locator('.kanban-card, .task-list-row').filter({ hasText: task.title }).first()).toContainText('Khóa')

    await page.goto(`/groups/${group.id}`, { waitUntil: 'domcontentloaded' })
    await expect(page.locator('.team-chat-page')).toBeVisible()
    await expect(page.locator('body')).toContainText(group.name)

    await page.goto(`/groups/${group.id}/meeting`, { waitUntil: 'domcontentloaded' })
    await expect(page.locator('.gm-prejoin__btn')).toBeVisible()
    await page.locator('.gm-prejoin__btn').click()
    await expect(page.locator('.mc-btn--transcript')).toBeVisible({ timeout: 25_000 })
    await page.locator('.mc-btn--transcript').click()

    const dialog = page.locator('.gm-privacy-dialog')
    await expect(dialog).toBeVisible()
    await expect(dialog).toContainText(policy.name)
    await dialog.locator('.gm-privacy-consent input[type="checkbox"]').check()
    await dialog.locator('.gm-btn-primary').click()
    await expect(dialog).toHaveCount(0)

    const consents = await apiResult<PrivacyConsentDto[]>(
      await browserApiRequest(page, 'GET', `/api/privacy/consents?projectId=${project.id}`),
    )
    expect(consents).toContainEqual(
      expect.objectContaining({
        projectId: project.id,
        retentionPolicyId: policy.id,
        noticeVersion: 'qaly-meeting-privacy-v4.0',
      }),
    )
  } catch (error) {
    await captureFailure(page, testInfo)
    throw error
  } finally {
    await cleanup(page, project?.id, group?.id)
  }
})

test('T1-TR-01 Deny: unauthenticated Task URL and Group link are blocked', async ({ page, browser }, testInfo) => {
  let project: ProjectDto | undefined
  let group: GroupDto | undefined
  let task: TaskItemDto | undefined
  let anonymous: Awaited<ReturnType<Browser['newContext']>> | undefined

  try {
    await login(page)
    project = await createProject(page, uniqueName('T1TR01 Deny Project'))
    task = await createTask(page, project.id, uniqueName('T1TR01 denied private task'))
    group = await createGroup(page, uniqueName('T1TR01 Deny Group'))

    await apiResult<TaskItemDto>(await browserApiRequest(page, 'GET', `/api/tasks/${task.id}`))
    await apiResult<GroupDto>(await browserApiRequest(page, 'GET', `/api/groups/${group.id}`))

    anonymous = await browser.newContext()
    const anonPage = await anonymous.newPage()

    await anonPage.goto(`/projects/${project.id}/tasks/${task.id}`, { waitUntil: 'domcontentloaded' })
    await expect(anonPage).toHaveURL(/\/Account\/Login/i)
    await expect(anonPage.locator('#loginForm')).toBeVisible()

    await anonPage.goto(`/groups/${group.id}`, { waitUntil: 'domcontentloaded' })
    await expect(anonPage).toHaveURL(/\/Account\/Login/i)
    await expect(anonPage.locator('#loginForm')).toBeVisible()
  } catch (error) {
    await captureFailure(page, testInfo)
    throw error
  } finally {
    await anonymous?.close()
    await cleanup(page, project?.id, group?.id)
  }
})

test('T1-TR-01 Error: malformed URL renders route error page', async ({ page }, testInfo) => {
  try {
    await login(page)

    await page.goto('/projects/not-a-guid/tasks/not-a-guid', { waitUntil: 'domcontentloaded' })
    await expect(page).toHaveURL(/\/projects\/not-a-guid\/tasks\/not-a-guid$/)
    await expect(page.locator('.route-error-page')).toBeVisible()
    await expect(page.locator('.route-error-page')).toContainText('URL nhiệm vụ không hợp lệ')

    await page.goto('/groups/not-a-guid/meeting', { waitUntil: 'domcontentloaded' })
    await expect(page.locator('.route-error-page')).toBeVisible()
    await expect(page.locator('.route-error-page')).toContainText('URL nhóm không hợp lệ')
  } catch (error) {
    await captureFailure(page, testInfo)
    throw error
  }
})
