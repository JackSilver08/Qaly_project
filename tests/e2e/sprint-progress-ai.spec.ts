import { expect, test, type Page, type Route } from '@playwright/test'
import { adminEmail, adminPassword } from './support/credentials'

test.describe.configure({ mode: 'serial' })
test.setTimeout(90_000)

const projectId = '61111111-1111-4111-8111-111111111111'
const sprintA = '62222222-2222-4222-8222-222222222222'
const sprintB = '63333333-3333-4333-8333-333333333333'
const jobA = '64444444-4444-4444-8444-444444444444'
const jobB = '65555555-5555-4555-8555-555555555555'
const projectJob = '66666666-6666-4666-8666-666666666666'
const taskA = '67777777-7777-4777-8777-777777777777'
const userId = '68888888-8888-4888-8888-888888888888'

type Mode = 'idle' | 'queued' | 'running' | 'succeeded' | 'failed' | 'canceled' | 'empty'
type State = {
  modeA: Mode
  detailCalls: number
  createBodies: Record<string, unknown>[]
  createHeaders: Record<string, string>[]
  retryCalls: number
  cancelCalls: number
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

function task(id: string, title: string, status: string, sprintId: string) {
  return {
    id,
    title,
    status,
    priority: 'High',
    dueDate: '2026-07-25T10:00:00Z',
    sprintId,
    assigneeId: userId,
    assigneeName: 'E2E Admin',
    reporterName: 'E2E Admin',
    projectName: 'Sprint Progress Project',
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
  }
}

function dashboard() {
  const tasks = [
    task(taskA, 'Unblock release milestone', 'InProgress', sprintA),
    task('69999999-9999-4999-8999-999999999999', 'Finish milestone B', 'Done', sprintB),
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
      name: 'Sprint Progress Project',
      code: 'SPR',
      description: 'Grounded sprint progress E2E',
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

function sprints() {
  return [
    {
      id: sprintA,
      projectId,
      name: 'Release milestone A',
      startDate: '2026-07-20T00:00:00Z',
      endDate: '2026-08-02T00:00:00Z',
      status: 'Active',
      goal: 'Ship the grounded sprint slice',
      taskCount: 1,
      completedTaskCount: 0,
      progress: 0,
    },
    {
      id: sprintB,
      projectId,
      name: 'Release milestone B',
      startDate: '2026-08-03T00:00:00Z',
      endDate: '2026-08-10T00:00:00Z',
      status: 'Planning',
      goal: 'Validate the following slice',
      taskCount: 1,
      completedTaskCount: 1,
      progress: 100,
    },
  ]
}

function ganttTasks() {
  return [{
    id: taskA,
    title: 'Unblock release milestone',
    status: 'InProgress',
    startDate: '2026-07-20T00:00:00Z',
    endDate: '2026-07-25T10:00:00Z',
    progress: 45,
    isCriticalPath: true,
    dependencies: [],
  }]
}

function job(jobId: string, sprintId: string | null, status: string) {
  return {
    jobId,
    jobType: sprintId ? 'sprint_progress_summary' : 'project_progress_summary',
    projectId,
    status,
    progressPercent: status === 'succeeded' ? 100 : status === 'running' ? 55 : 0,
    attemptCount: status === 'queued' ? 0 : 1,
    maxAttempts: 3,
    createdAt: sprintId === sprintB ? '2026-07-27T11:00:00Z' : '2026-07-27T10:00:00Z',
    lastErrorCode: status === 'failed' ? 'AI_PROVIDER_UNAVAILABLE' : null,
    lastErrorMessage: status === 'failed' ? 'provider timeout' : null,
    lastErrorRetryable: status === 'failed' || status === 'canceled',
    cacheHit: false,
    selectedProvider: status === 'succeeded' ? 'e2e-provider' : null,
    selectedModel: status === 'succeeded' ? 'e2e-model' : null,
    isMock: false,
    draftIds: [],
    scopeSourceType: sprintId ? 'sprint' : 'project',
    scopeSourceEntityId: sprintId ?? projectId,
  }
}

function result(jobId: string, sprintId: string, sprintName: string, empty = false) {
  const taskId = sprintId === sprintA ? taskA : '69999999-9999-4999-8999-999999999999'
  const text = sprintId === sprintA
    ? 'Milestone A có một task đang làm và cần can thiệp.'
    : 'Milestone B đã hoàn thành theo snapshot riêng.'
  return {
    jobId,
    schemaId: 'progress_summary.v4',
    schemaVersion: '4.0',
    result: {
      scope: {
        type: 'sprint',
        projectId,
        projectName: 'Sprint Progress Project',
        projectCode: 'SPR',
        sprintId,
        sprintName,
      },
      period: { kind: 'current_snapshot', snapshotAt: '2026-07-27T10:00:00Z' },
      coverage: {
        dataState: empty ? 'empty' : 'sufficient',
        visibility: 'manager_full_project',
        includedTaskCount: empty ? 0 : 1,
        excludedTaskCount: 0,
      },
      metrics: {
        total: empty ? 0 : 1,
        done: empty || sprintId === sprintA ? 0 : 1,
        inProgress: empty || sprintId === sprintB ? 0 : 1,
        todo: 0,
        overdue: empty || sprintId === sprintB ? 0 : 1,
        dueSoon: 0,
        completionRate: empty || sprintId === sprintA ? 0 : 100,
      },
      summaryPoints: empty ? [] : [{
        text,
        metricRefs: ['total'],
        sourceRefs: [`sprint:${sprintId}`],
      }],
      risks: [],
      nextActions: [],
      sourceRefs: [{
        key: `sprint:${sprintId}`,
        type: 'sprint',
        entityId: sprintId,
        label: sprintName,
        url: `/projects/${projectId}#milestone-${sprintId}`,
        version: 'sprint-source-v1',
      }, {
        key: `task:${taskId}`,
        type: 'task',
        entityId: taskId,
        label: sprintId === sprintA ? 'Unblock release milestone' : 'Finish milestone B',
        url: `/projects/${projectId}/tasks/${taskId}`,
        version: 'task-v1',
      }],
      warnings: empty ? ['NO_PROGRESS_TASKS'] : [],
    },
    cacheHit: false,
    isMock: false,
    mockReason: null,
    sourceStale: false,
  }
}

async function installMocks(page: Page, initialMode: Mode) {
  const state: State = {
    modeA: initialMode,
    detailCalls: 0,
    createBodies: [],
    createHeaders: [],
    retryCalls: 0,
    cancelCalls: 0,
  }
  await page.route('**/api/security/csrf', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({ token: 'sprint-e2e-csrf' }),
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
  await page.route(`**/api/projects/${projectId}/sprints`, route => fulfill(route, sprints()))
  await page.route(`**/api/tasks/project/${projectId}/gantt`, route => fulfill(route, ganttTasks()))

  await page.route(/\/api\/ai(?:\/|$)/, async route => {
    const request = route.request()
    const path = new URL(request.url()).pathname
    const method = request.method()
    const statusA = state.modeA === 'empty' ? 'succeeded' : state.modeA

    if (method === 'GET' && path === '/api/ai/jobs') {
      const jobs = [
        job(jobB, sprintB, 'succeeded'),
        job(projectJob, null, 'succeeded'),
      ]
      if (state.modeA !== 'idle') jobs.push(job(jobA, sprintA, statusA))
      await fulfill(route, jobs)
      return
    }
    if (method === 'POST' && path === `/api/ai/projects/${projectId}/sprints/${sprintA}/progress-summary`) {
      state.createBodies.push(request.postDataJSON() as Record<string, unknown>)
      state.createHeaders.push(request.headers())
      state.modeA = 'queued'
      state.detailCalls = 0
      await fulfill(route, { jobId: jobA, status: 'queued' }, 202)
      return
    }
    if (method === 'GET' && path === `/api/ai/jobs/${jobA}`) {
      state.detailCalls += 1
      if (state.modeA === 'queued' && state.detailCalls === 2) state.modeA = 'running'
      else if (state.modeA === 'running' && state.detailCalls >= 3) state.modeA = 'succeeded'
      await fulfill(route, job(jobA, sprintA, state.modeA === 'empty' ? 'succeeded' : state.modeA))
      return
    }
    if (method === 'GET' && path === `/api/ai/jobs/${jobB}`) {
      await fulfill(route, job(jobB, sprintB, 'succeeded'))
      return
    }
    if (method === 'GET' && path === `/api/ai/jobs/${jobA}/result`) {
      await fulfill(route, result(jobA, sprintA, 'Release milestone A', state.modeA === 'empty'))
      return
    }
    if (method === 'GET' && path === `/api/ai/jobs/${jobB}/result`) {
      await fulfill(route, result(jobB, sprintB, 'Release milestone B'))
      return
    }
    if (method === 'POST' && path === `/api/ai/jobs/${jobA}/retry`) {
      state.retryCalls += 1
      state.modeA = 'succeeded'
      await fulfill(route, job(jobA, sprintA, 'retrying'))
      return
    }
    if (method === 'POST' && path === `/api/ai/jobs/${jobA}/cancel`) {
      state.cancelCalls += 1
      state.modeA = 'canceled'
      await fulfill(route, job(jobA, sprintA, 'canceled'))
      return
    }

    await route.fulfill({
      status: 404,
      contentType: 'application/json',
      body: JSON.stringify({ error: `Unhandled sprint progress E2E route: ${method} ${path}` }),
    })
  })

  return state
}

async function openDemoMap(page: Page) {
  await page.getByRole('button', { name: 'Lộ Trình Dự Án' }).click()
  await expect(page.getByTestId('sprint-progress-ai-card')).toBeVisible()
}

test('timeline renders the real Gantt contract instead of dashboard task dates', async ({ page }) => {
  await installMocks(page, 'idle')
  await login(page, `/projects/${projectId}`)
  await openDemoMap(page)

  await page.getByRole('button', { name: 'Timeline View' }).click()
  await expect(page.locator('.timeline-task-label')).toContainText('Unblock release milestone')
  await expect(page.locator('.timeline-bar')).toBeVisible()
  await expect(page.locator('.timeline-bar')).toContainText('45%')
})

test('native sprint card creates a minimal request, renders grounded output, and restores the exact milestone', async ({ page }) => {
  const state = await installMocks(page, 'idle')
  await login(page, `/projects/${projectId}`)
  await openDemoMap(page)
  const card = page.getByTestId('sprint-progress-ai-card')

  await page.getByTestId('generate-sprint-progress').dblclick()
  await expect.poll(() => state.createBodies.length).toBe(1)
  expect(state.createBodies[0]).toEqual({
    period: 'current_snapshot',
    language: 'vi',
    providerHint: 'auto',
    maximumEstimatedCostUsd: 0.25,
    cacheMode: 'use',
  })
  expect(state.createHeaders[0]['idempotency-key']).toBeTruthy()
  expect(state.createHeaders[0]['x-csrf-token']).toBe('sprint-e2e-csrf')
  await expect(card).toContainText('Milestone A có một task đang làm', { timeout: 12_000 })
  await expect(card.getByRole('link', { name: 'Release milestone A' })).toHaveAttribute(
    'href',
    `/projects/${projectId}#milestone-${sprintA}`,
  )

  await page.reload({ waitUntil: 'domcontentloaded' })
  await expect(page.getByTestId('sprint-progress-ai-card')).toContainText('Milestone A có một task đang làm')
  expect(state.createBodies).toHaveLength(1)

  await page.locator('.milestone-node-card', { hasText: 'Release milestone B' }).click()
  await expect(page.getByTestId('sprint-progress-ai-card')).toContainText('Milestone B đã hoàn thành')
  await expect(page.getByTestId('sprint-progress-ai-card')).not.toContainText('Milestone A có một task')
  await page.reload({ waitUntil: 'domcontentloaded' })
  await expect(page.getByTestId('sprint-progress-ai-card')).toContainText('Milestone B đã hoàn thành')

  await expect(page.getByText('Finish milestone B', { exact: true })).toBeVisible()
  await page.getByRole('button', { name: 'Sửa mốc' }).click()
  await expect(page.getByRole('heading', { name: 'Chỉnh sửa Mốc Tiến Độ' })).toBeVisible()
  await page.getByRole('button', { name: 'Hủy' }).click()
})

test('native sprint card reports provider failure honestly and retries the same persisted job', async ({ page }) => {
  const state = await installMocks(page, 'failed')
  await login(page, `/projects/${projectId}`)
  await openDemoMap(page)
  const card = page.getByTestId('sprint-progress-ai-card')

  await expect(card.getByTestId('sprint-progress-error')).toContainText('không khả dụng')
  await card.getByRole('button', { name: 'Thử lại' }).click()
  await expect.poll(() => state.retryCalls).toBe(1)
  await expect(card).toContainText('Milestone A có một task đang làm')
  expect(state.createBodies).toHaveLength(0)
})

test('native sprint card supports cancel and deterministic empty without fabricated narrative', async ({ page }) => {
  const running = await installMocks(page, 'running')
  await login(page, `/projects/${projectId}`)
  await openDemoMap(page)
  const card = page.getByTestId('sprint-progress-ai-card')

  await card.getByRole('button', { name: 'Hủy' }).click()
  await expect.poll(() => running.cancelCalls).toBe(1)
  await expect(card).toContainText('Tác vụ đã được hủy')

  await page.unrouteAll({ behavior: 'wait' })
  await installMocks(page, 'empty')
  await page.reload({ waitUntil: 'domcontentloaded' })
  const emptyCard = page.getByTestId('sprint-progress-ai-card')
  await expect(emptyCard.getByTestId('sprint-progress-empty')).toContainText('Chưa có task đóng góp')
  await expect(emptyCard).not.toContainText('Milestone A có một task đang làm')
})
