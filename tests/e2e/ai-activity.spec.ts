import { expect, test, type Page, type Route } from '@playwright/test'
import { adminEmail, adminPassword } from './support/credentials'

test.describe.configure({ mode: 'serial' })
test.setTimeout(110_000)

const jobId = '11111111-1111-4111-8111-111111111111'
const draftId = '22222222-2222-4222-8222-222222222222'
const projectId = '33333333-3333-4333-8333-333333333333'

type MockState = {
  jobListCalls: number
  confirmBodies: Array<Record<string, unknown>>
  confirmHeaders: Array<Record<string, string>>
  draftConfirmed: boolean
}

async function login(page: Page, returnUrl: string) {
  await page.goto(`/Account/Login?returnUrl=${encodeURIComponent(returnUrl)}`, { waitUntil: 'domcontentloaded' })
  await page.locator('input[name="Email"]').fill(adminEmail)
  await page.locator('input[name="Password"]').fill(adminPassword)
  await page.locator('#loginForm button[type="submit"]').click()
  await expect(page.locator('.shell-header')).toBeVisible({ timeout: 45_000 })
  await page.locator('.welcome-overlay').waitFor({ state: 'hidden', timeout: 5_000 }).catch(() => undefined)
}

function apiResult(data: unknown) {
  return JSON.stringify({ data, error: null, isSuccess: true })
}

function jobSummary(state: MockState) {
  const succeeded = state.jobListCalls >= 2
  return {
    jobId,
    jobType: 'project_progress_summary',
    projectId,
    status: succeeded ? 'succeeded' : 'queued',
    progressPercent: succeeded ? 100 : 0,
    attemptCount: succeeded ? 1 : 0,
    maxAttempts: 3,
    createdAt: '2026-07-12T01:00:00Z',
    startedAt: succeeded ? '2026-07-12T01:00:01Z' : null,
    finishedAt: succeeded ? '2026-07-12T01:00:02Z' : null,
    lastErrorCode: null,
    isMock: false,
    draftIds: [],
  }
}

function draftSummary(status = 'pending_review') {
  return {
    draftId,
    aiJobId: jobId,
    projectId,
    draftType: 'task_draft',
    status,
    confidence: 0.91,
    createdAt: '2026-07-12T01:00:02Z',
    expiresAt: null,
  }
}

async function fulfill(route: Route, data: unknown, status = 200) {
  await route.fulfill({ status, contentType: 'application/json', body: apiResult(data) })
}

async function installAiMocks(page: Page) {
  const state: MockState = {
    jobListCalls: 0,
    confirmBodies: [],
    confirmHeaders: [],
    draftConfirmed: false,
  }

  await page.route('**/api/security/csrf', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({ token: 'p002-e2e-csrf-token' }),
  }))

  const currentUser = {
    id: '55555555-5555-4555-8555-555555555555',
    fullName: 'P0-02 E2E Admin',
    email: adminEmail,
    role: 'Admin',
    isActive: true,
    avatarUrl: null,
    createdAt: '2026-07-12T00:00:00Z',
  }

  await page.route('**/api/auth/me', route => fulfill(route, currentUser))
  await page.route('**/api/users', route => fulfill(route, [currentUser]))
  await page.route('**/api/notifications', route => fulfill(route, []))
  await page.route('**/api/dashboard/overview', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({}),
  }))

  await page.route(/\/api\/ai(?:\/|$)/, async route => {
    const request = route.request()
    const path = new URL(request.url()).pathname
    const method = request.method()

    if (method === 'GET' && path === '/api/ai/jobs') {
      state.jobListCalls += 1
      await fulfill(route, [jobSummary(state)])
      return
    }

    if (method === 'GET' && path === `/api/ai/jobs/${jobId}/result`) {
      await fulfill(route, {
        jobId,
        schemaId: 'qaly.project-progress',
        schemaVersion: '1.0',
        result: { summary: 'Milestone is on track', completion: 72 },
        isMock: false,
        mockReason: null,
      })
      return
    }

    if (method === 'GET' && path === `/api/ai/jobs/${jobId}`) {
      await fulfill(route, jobSummary(state))
      return
    }

    if (method === 'GET' && path === '/api/ai/drafts') {
      await fulfill(route, state.draftConfirmed ? [] : [draftSummary()])
      return
    }

    if (method === 'GET' && path === `/api/ai/drafts/${draftId}`) {
      await fulfill(route, {
        ...draftSummary(),
        originalPayload: { tasks: [{ title: 'Review scope' }] },
        workingPayload: { tasks: [{ title: 'Review scope' }] },
        warnings: [],
        schemaId: 'qaly.task-draft',
        rowVersion: 'AAAAAAAAB9E=',
      })
      return
    }

    if (method === 'POST' && path === `/api/ai/drafts/${draftId}/confirm`) {
      state.confirmBodies.push(request.postDataJSON() as Record<string, unknown>)
      state.confirmHeaders.push(request.headers())
      state.draftConfirmed = true
      await fulfill(route, {
        draftId,
        status: 'confirmed',
        confirmAction: 'create_tasks',
        createdTaskCount: 1,
        createdTaskIds: ['44444444-4444-4444-8444-444444444444'],
      })
      return
    }

    if (method === 'GET' && path === '/api/ai/health') {
      await fulfill(route, {
        status: 'healthy',
        platformEnabled: true,
        workerEnabled: true,
        queueDepth: state.jobListCalls >= 2 ? 0 : 1,
        runningCount: 0,
        retryCount: 0,
        failedLast24Hours: 0,
        expiredLeaseCount: 0,
        oldestQueuedAgeSeconds: null,
        degradedReason: null,
      })
      return
    }

    await route.fulfill({ status: 404, contentType: 'application/json', body: JSON.stringify({ error: `Unhandled E2E route: ${method} ${path}` }) })
  })

  return state
}

test('AI Activity polls a job and restores its deep link after refresh', async ({ page }) => {
  const state = await installAiMocks(page)
  const deepLink = `/?aiActivity=1&aiTab=jobs&aiJob=${jobId}`

  await login(page, deepLink)
  await expect(page.locator('.erumi-side-drawer')).toBeVisible()
  await expect(page.locator('.drawer-title')).toHaveText('Trợ lý AI')
  await expect(page.locator('.activity-detail')).toContainText('project_progress_summary')
  await expect(page).toHaveURL(new RegExp(`aiJob=${jobId}`))

  await expect.poll(() => state.jobListCalls, { timeout: 9_000 }).toBeGreaterThanOrEqual(2)
  await expect(page.locator('.job-result')).toContainText('Milestone is on track')

  const callsBeforeRefresh = state.jobListCalls
  await page.locator('.activity-toolbar .icon-button').click()
  await expect.poll(() => state.jobListCalls).toBeGreaterThan(callsBeforeRefresh)

  await page.reload({ waitUntil: 'domcontentloaded' })
  await expect(page.locator('.erumi-side-drawer')).toBeVisible()
  await expect(page.locator('.job-result')).toContainText('Milestone is on track')
  await expect(page).toHaveURL(new RegExp(`aiJob=${jobId}`))

  await page.locator('.activity-detail header .icon-button').click()
  await expect(page.locator('.ai-activity-list')).toBeVisible()
  const pseudoContent = await page.locator('.ai-activity-list').evaluate(element => getComputedStyle(element, '::before').content)
  expect(pseudoContent).toBe('none')
})

test('draft review shows the original diff and mutates only after confirmation', async ({ page }) => {
  const state = await installAiMocks(page)
  const deepLink = `/?aiActivity=1&aiTab=drafts&aiDraft=${draftId}`

  await login(page, deepLink)
  await expect(page.locator('.draft-compare')).toBeVisible()
  await expect(page.locator('.draft-compare section').first()).toContainText('Review scope')
  await expect(page.locator('.draft-change-state')).toHaveText('Chưa chỉnh sửa')
  expect(state.confirmBodies).toHaveLength(0)

  const editedPayload = { tasks: [{ title: 'Review scope', priority: 'High' }] }
  await page.getByLabel('Nội dung bản nháp').fill(JSON.stringify(editedPayload, null, 2))
  await expect(page.locator('.draft-change-state')).toHaveText('Đã chỉnh sửa')
  expect(state.confirmBodies).toHaveLength(0)

  await page.locator('.detail-actions .primary-action').click()
  await expect.poll(() => state.confirmBodies.length).toBe(1)
  await expect(page).not.toHaveURL(/aiDraft=/)
  await expect(page.locator('.draft-detail')).toHaveCount(0)

  const confirmation = state.confirmBodies[0]
  expect(confirmation.confirmAction).toBe('create_tasks')
  expect(JSON.parse(String(confirmation.editedPayloadJson))).toEqual(editedPayload)
  expect(String(confirmation.idempotencyKey)).not.toHaveLength(0)
  expect(state.confirmHeaders[0]['idempotency-key']).toBe(confirmation.idempotencyKey)
  expect(state.confirmHeaders[0]['x-csrf-token']).toBe('p002-e2e-csrf-token')
})
