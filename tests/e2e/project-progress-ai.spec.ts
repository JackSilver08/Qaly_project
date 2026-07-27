import { expect, test, type Page, type Route } from '@playwright/test'

test.describe.configure({ mode: 'serial' })
test.setTimeout(90_000)

const adminEmail = process.env.E2E_ADMIN_EMAIL ?? 'admin@qaly.dev'
const adminPassword = process.env.E2E_ADMIN_PASSWORD ?? 'Qaly@E2E2026!'
const projectId = '11111111-1111-4111-8111-111111111111'
const jobId = '22222222-2222-4222-8222-222222222222'
const taskId = '33333333-3333-4333-8333-333333333333'
const userId = '44444444-4444-4444-8444-444444444444'

type Mode = 'idle' | 'queued' | 'running' | 'succeeded' | 'failed' | 'empty'
type MockState = {
  mode: Mode
  detailCalls: number
  createBodies: Record<string, unknown>[]
  createHeaders: Record<string, string>[]
  retryCalls: number
}

function apiResult(data: unknown) {
  return JSON.stringify({ data, error: null, isSuccess: true })
}

async function fulfill(route: Route, data: unknown, status = 200) {
  await route.fulfill({ status, contentType: 'application/json', body: apiResult(data) })
}

async function login(page: Page, returnUrl: string) {
  await page.goto(`/Account/Login?returnUrl=${encodeURIComponent(returnUrl)}`, { waitUntil: 'domcontentloaded' })
  await page.locator('input[name="Email"]').fill(adminEmail)
  await page.locator('input[name="Password"]').fill(adminPassword)
  await page.locator('#loginForm button[type="submit"]').click()
  await expect(page.locator('.shell-header')).toBeVisible({ timeout: 45_000 })
  await page.locator('.welcome-overlay').waitFor({ state: 'hidden', timeout: 5_000 }).catch(() => undefined)
}

function dashboard() {
  const task = (id: string, title: string, status: string) => ({
    id,
    title,
    status,
    priority: 'High',
    dueDate: '2026-07-25T10:00:00Z',
    sprintId: null,
    assigneeId: userId,
    assigneeName: 'E2E Admin',
    reporterName: 'E2E Admin',
    projectName: 'AI Progress Project',
    sortOrder: 0,
    rowVersion: 'AQID',
    isPrivate: false,
    isRestricted: false,
    isPinned: false,
    contributesToProgress: true,
    upvoteCount: 0,
    downvoteCount: 0,
    commentCount: 0,
    attachmentCount: 0,
  })
  const tasks = [
    task(taskId, 'Resolve overdue release risk', 'InProgress'),
    task('55555555-5555-4555-8555-555555555555', 'Completed foundation', 'Done'),
  ]
  return {
    generatedAt: '2026-07-27T10:00:00Z',
    stats: {
      activeProjects: 1,
      totalTasks: 2,
      overdueTasks: 1,
      teamMembers: 1,
      completedTasks: 1,
      completionRate: 50,
      tasksAtRisk: 1,
    },
    summary: '',
    riskDigest: '',
    projects: [{
      id: projectId,
      name: 'AI Progress Project',
      code: 'AIP',
      description: 'Grounded project progress E2E',
      logoUrl: null,
      status: 'Active',
      organizationId: null,
      ownerId: userId,
      ownerName: 'E2E Admin',
      memberCount: 1,
      taskCount: 2,
      completedTaskCount: 1,
      overdueTaskCount: 1,
      progressPercentage: 50,
      members: [{
        userId,
        fullName: 'E2E Admin',
        role: 'Owner',
        email: adminEmail,
        canViewProjectTimeline: true,
        canViewTaskRisk: true,
        canNudgeAssignee: true,
        canViewUnseenTaskSignal: true,
      }],
      tasks,
      createdAt: '2026-07-01T10:00:00Z',
      endDate: '2026-08-01T10:00:00Z',
      enableOnHold: true,
      enableInReview: true,
      requireEvidenceToDone: false,
      restrictTransitionsToAdmin: false,
    }],
    team: [],
    notifications: [],
  }
}

function job(status: string) {
  return {
    jobId,
    jobType: 'project_progress_summary',
    projectId,
    status,
    progressPercent: status === 'succeeded' ? 100 : status === 'running' ? 55 : 0,
    attemptCount: status === 'queued' ? 0 : 1,
    maxAttempts: 3,
    createdAt: '2026-07-27T10:00:00Z',
    lastErrorCode: status === 'failed' ? 'AI_PROVIDER_UNAVAILABLE' : null,
    lastErrorMessage: status === 'failed' ? 'provider timeout' : null,
    lastErrorRetryable: status === 'failed',
    cacheHit: false,
    selectedProvider: status === 'succeeded' ? 'e2e-provider' : null,
    selectedModel: status === 'succeeded' ? 'e2e-model' : null,
    isMock: false,
    draftIds: [],
  }
}

function persistedResult(empty = false) {
  return {
    jobId,
    schemaId: 'progress_summary.v4',
    schemaVersion: '4.0',
    result: {
      scope: { projectId, projectName: 'AI Progress Project', projectCode: 'AIP' },
      period: { kind: 'current_snapshot', snapshotAt: '2026-07-27T10:00:00Z' },
      coverage: {
        dataState: empty ? 'empty' : 'sufficient',
        visibility: 'manager_full_project',
        includedTaskCount: empty ? 0 : 2,
        excludedTaskCount: 0,
      },
      metrics: {
        total: empty ? 0 : 2,
        done: empty ? 0 : 1,
        inProgress: empty ? 0 : 1,
        todo: 0,
        overdue: empty ? 0 : 1,
        dueSoon: 0,
        completionRate: empty ? 0 : 50,
      },
      summaryPoints: empty ? [] : [{
        text: 'Một trong hai task đã hoàn thành; task còn lại đang quá hạn.',
        metricRefs: ['done', 'total', 'overdue'],
        sourceRefs: [`project:${projectId}`],
      }],
      risks: empty ? [] : [{
        code: 'OVERDUE_TASK',
        severity: 'high',
        title: 'Rủi ro bàn giao từ task quá hạn.',
        metricRefs: ['overdue'],
        sourceRefs: [`task:${taskId}`],
      }],
      nextActions: empty ? [] : [{
        title: 'Rà soát task quá hạn',
        rationale: 'Giảm điểm nghẽn trước mốc bàn giao.',
        metricRefs: ['overdue'],
        sourceRefs: [`task:${taskId}`],
      }],
      sourceRefs: [
        {
          key: `project:${projectId}`,
          type: 'project',
          entityId: projectId,
          label: 'AI Progress Project',
          url: `/projects/${projectId}`,
          version: 'source-v1',
        },
        {
          key: `task:${taskId}`,
          type: 'task',
          entityId: taskId,
          label: 'Resolve overdue release risk',
          url: `/projects/${projectId}/tasks/${taskId}`,
          version: 'task-v1',
        },
      ],
      warnings: empty ? ['NO_PROGRESS_TASKS'] : [],
    },
    cacheHit: false,
    isMock: false,
    mockReason: null,
    sourceStale: false,
  }
}

async function installMocks(page: Page, initialMode: Mode) {
  const state: MockState = {
    mode: initialMode,
    detailCalls: 0,
    createBodies: [],
    createHeaders: [],
    retryCalls: 0,
  }

  await page.route('**/api/security/csrf', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({ token: 'progress-e2e-csrf' }),
  }))
  const user = {
    id: userId,
    fullName: 'E2E Admin',
    email: adminEmail,
    role: 'Admin',
    isActive: true,
    avatarUrl: null,
    createdAt: '2026-07-01T00:00:00Z',
  }
  await page.route('**/api/auth/me', route => fulfill(route, user))
  await page.route('**/api/users', route => fulfill(route, [user]))
  await page.route('**/api/notifications', route => fulfill(route, []))
  await page.route('**/api/dashboard/overview', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify(dashboard()),
  }))

  await page.route(/\/api\/ai(?:\/|$)/, async route => {
    const request = route.request()
    const path = new URL(request.url()).pathname
    const method = request.method()

    if (method === 'GET' && path === '/api/ai/jobs') {
      await fulfill(route, state.mode === 'idle' ? [] : [job(state.mode === 'empty' ? 'succeeded' : state.mode)])
      return
    }
    if (method === 'POST' && path === `/api/ai/projects/${projectId}/progress-summary`) {
      state.createBodies.push(request.postDataJSON() as Record<string, unknown>)
      state.createHeaders.push(request.headers())
      state.mode = 'queued'
      state.detailCalls = 0
      await fulfill(route, { jobId, status: 'queued' }, 202)
      return
    }
    if (method === 'GET' && path === `/api/ai/jobs/${jobId}`) {
      state.detailCalls += 1
      if (state.mode === 'queued' && state.detailCalls === 2) state.mode = 'running'
      else if (state.mode === 'running' && state.detailCalls >= 3) state.mode = 'succeeded'
      await fulfill(route, job(state.mode === 'empty' ? 'succeeded' : state.mode))
      return
    }
    if (method === 'GET' && path === `/api/ai/jobs/${jobId}/result`) {
      await fulfill(route, persistedResult(state.mode === 'empty'))
      return
    }
    if (method === 'POST' && path === `/api/ai/jobs/${jobId}/retry`) {
      state.retryCalls += 1
      state.mode = 'succeeded'
      await fulfill(route, job('retrying'))
      return
    }

    await route.fulfill({
      status: 404,
      contentType: 'application/json',
      body: JSON.stringify({ error: `Unhandled project progress E2E route: ${method} ${path}` }),
    })
  })

  return state
}

test('native project card creates one minimal request, polls, grounds output, and restores after reload', async ({ page }) => {
  const state = await installMocks(page, 'idle')
  await login(page, `/projects/${projectId}`)
  const card = page.getByTestId('project-progress-ai-card')
  await expect(card).toBeVisible()

  const generate = page.getByTestId('generate-project-progress')
  await generate.dblclick()
  await expect.poll(() => state.createBodies.length).toBe(1)
  expect(state.createBodies[0]).toEqual({
    period: 'current_snapshot',
    language: 'vi',
    providerHint: 'auto',
    maximumEstimatedCostUsd: 0.25,
    cacheMode: 'use',
  })
  expect(state.createHeaders[0]['idempotency-key']).toBeTruthy()
  expect(state.createHeaders[0]['x-csrf-token']).toBe('progress-e2e-csrf')

  await expect(card).toContainText('Một trong hai task đã hoàn thành', { timeout: 12_000 })
  await expect(card).toContainText('Rủi ro bàn giao từ task quá hạn')
  await expect(card.getByRole('link', { name: 'Resolve overdue release risk' }).first()).toHaveAttribute(
    'href',
    `/projects/${projectId}/tasks/${taskId}`,
  )

  await page.reload({ waitUntil: 'domcontentloaded' })
  await expect(page.getByTestId('project-progress-ai-card')).toContainText('Một trong hai task đã hoàn thành')
  expect(state.createBodies).toHaveLength(1)
})

test('native project card labels provider failure honestly and retries the persisted job', async ({ page }) => {
  const state = await installMocks(page, 'failed')
  await login(page, `/projects/${projectId}`)
  const card = page.getByTestId('project-progress-ai-card')

  await expect(card.getByTestId('project-progress-error')).toContainText('không khả dụng')
  await card.getByRole('button', { name: 'Thử lại' }).click()
  await expect.poll(() => state.retryCalls).toBe(1)
  await expect(card).toContainText('Một trong hai task đã hoàn thành')
  expect(state.createBodies).toHaveLength(0)
})

test('native project card shows deterministic empty state without fabricated insight', async ({ page }) => {
  await installMocks(page, 'empty')
  await login(page, `/projects/${projectId}`)

  const card = page.getByTestId('project-progress-ai-card')
  await expect(card.getByTestId('project-progress-empty')).toContainText('Chưa có task đóng góp')
  await expect(card).not.toContainText('Một trong hai task đã hoàn thành')
})
