import { expect, test, type Page, type Route } from '@playwright/test'

test.describe.configure({ mode: 'serial' })
test.setTimeout(90_000)

const adminEmail = process.env.E2E_ADMIN_EMAIL ?? 'admin@qaly.dev'
const adminPassword = process.env.E2E_ADMIN_PASSWORD ?? 'Admin@123456'
const organizationId = '71111111-1111-4111-8111-111111111111'
const projectId = '72222222-2222-4222-8222-222222222222'
const userId = '73333333-3333-4333-8333-333333333333'

type MockOptions = {
  denyScopes?: boolean
  usageFailures?: number
  emptyUsage?: boolean
  editingEnabled?: boolean
  conflictNext?: boolean
}

type MockState = {
  usageFailures: number
  conflictNext: boolean
  putBodies: Record<string, unknown>[]
  putHeaders: Record<string, string>[]
  dailyBudgetUsd: number
  monthlyBudgetUsd: number
  warningAtPercent: number
  version: string
}

function apiResult(data: unknown) {
  return JSON.stringify({ data, error: null, errorCode: null, isSuccess: true, statusCode: 200 })
}

function apiFailure(error: string, errorCode: string, statusCode: number) {
  return JSON.stringify({ data: null, error, errorCode, isSuccess: false, statusCode })
}

async function fulfill(route: Route, data: unknown, status = 200) {
  await route.fulfill({ status, contentType: 'application/json', body: apiResult(data) })
}

async function login(page: Page) {
  await page.goto('/Account/Login?returnUrl=%2Fsettings', { waitUntil: 'domcontentloaded' })
  await page.locator('input[name="Email"]').fill(adminEmail)
  await page.locator('input[name="Password"]').fill(adminPassword)
  await page.locator('#loginForm button[type="submit"]').click()
  await expect(page.locator('.shell-header')).toBeVisible({ timeout: 45_000 })
  await page.locator('.welcome-overlay').waitFor({ state: 'hidden', timeout: 5_000 }).catch(() => undefined)
}

function dashboard() {
  return {
    generatedAt: '2026-07-27T10:00:00Z',
    stats: {
      activeProjects: 1,
      totalTasks: 0,
      overdueTasks: 0,
      teamMembers: 1,
      completedTasks: 0,
      completionRate: 0,
      tasksAtRisk: 0,
    },
    summary: '',
    riskDigest: '',
    projects: [{
      id: projectId,
      name: 'AI Budget Project',
      code: 'AIB',
      description: 'AI budget E2E',
      logoUrl: null,
      status: 'Active',
      organizationId,
      ownerId: userId,
      ownerName: 'E2E Admin',
      memberCount: 1,
      taskCount: 0,
      completedTaskCount: 0,
      overdueTaskCount: 0,
      progressPercentage: 0,
      members: [{
        userId,
        fullName: 'E2E Admin',
        email: adminEmail,
        role: 'Owner',
        canViewProjectTimeline: true,
        canViewTaskRisk: true,
        canNudgeAssignee: true,
        canViewUnseenTaskSignal: true,
      }],
      tasks: [],
      createdAt: '2026-07-01T00:00:00Z',
      enableOnHold: true,
      enableInReview: true,
      requireEvidenceToDone: false,
      restrictTransitionsToAdmin: false,
    }],
    team: [],
    notifications: [],
  }
}

function usage(empty: boolean) {
  const breakdown = (key: string, attempts: number, cost: number, cache = 0) => ({
    key,
    attemptCount: attempts,
    succeededCount: Math.max(0, attempts - 1),
    failedCount: attempts ? 1 : 0,
    inputTokens: attempts * 100,
    outputTokens: attempts * 50,
    estimatedCostUsd: cost,
    effectiveCostUsd: cost,
    cacheHitCount: cache,
  })
  return {
    scopeType: 'organization',
    scopeId: organizationId,
    organizationId,
    projectId: null,
    scopeName: 'AI Budget Organization',
    from: '2026-06-27T10:00:00Z',
    to: '2026-07-27T10:00:00Z',
    attemptCount: empty ? 0 : 3,
    succeededCount: empty ? 0 : 2,
    failedCount: empty ? 0 : 1,
    inputTokens: empty ? 0 : 300,
    outputTokens: empty ? 0 : 150,
    estimatedCostUsd: empty ? 0 : 7.5,
    effectiveCostUsd: empty ? 0 : 7.5,
    actualCostCount: empty ? 0 : 2,
    estimatedOnlyCount: empty ? 0 : 1,
    cacheHitCount: empty ? 0 : 1,
    daily: empty ? [] : [breakdown('2026-07-27', 3, 7.5, 1)],
    byProvider: empty ? [] : [breakdown('provider-a', 2, 5, 1), breakdown('provider-b', 1, 2.5)],
    byFunction: empty ? [] : [
      breakdown('project_progress_summary', 2, 5, 1),
      breakdown('sprint_progress_summary', 1, 2.5),
    ],
    byStatus: empty ? [] : [breakdown('succeeded', 2, 6, 1), breakdown('failed', 1, 1.5)],
    byCache: empty ? [] : [breakdown('hit', 1, 2, 1), breakdown('miss', 2, 5.5)],
    calculatedAt: '2026-07-27T10:00:00Z',
  }
}

function budget(state: MockState, editingEnabled: boolean) {
  return {
    scopeType: 'organization',
    scopeId: organizationId,
    organizationId,
    projectId: null,
    scopeName: 'AI Budget Organization',
    policyId: '74444444-4444-4444-8444-444444444444',
    effectivePolicyId: '74444444-4444-4444-8444-444444444444',
    policySource: 'organization',
    isInherited: false,
    hasEffectivePolicy: true,
    canEdit: true,
    editingEnabled,
    dailyBudgetUsd: state.dailyBudgetUsd,
    monthlyBudgetUsd: state.monthlyBudgetUsd,
    warningAtPercent: state.warningAtPercent,
    hardStopEnabled: true,
    dailyUsageUsd: 7.5,
    monthlyUsageUsd: 42,
    dailyRemainingUsd: Math.max(0, state.dailyBudgetUsd - 7.5),
    monthlyRemainingUsd: Math.max(0, state.monthlyBudgetUsd - 42),
    warningActive: 7.5 / state.dailyBudgetUsd * 100 >= state.warningAtPercent,
    hardStopActive: 7.5 >= state.dailyBudgetUsd,
    allowCloudForSensitive: false,
    version: state.version,
    effectiveVersion: state.version,
    calculatedAt: '2026-07-27T10:00:00Z',
  }
}

async function installMocks(page: Page, options: MockOptions = {}) {
  const state: MockState = {
    usageFailures: options.usageFailures ?? 0,
    conflictNext: options.conflictNext ?? false,
    putBodies: [],
    putHeaders: [],
    dailyBudgetUsd: 10,
    monthlyBudgetUsd: 100,
    warningAtPercent: 70,
    version: 'policy-v1',
  }
  const editingEnabled = options.editingEnabled ?? true
  const user = {
    id: userId,
    fullName: 'E2E Admin',
    email: adminEmail,
    role: 'Admin',
    isActive: true,
    avatarUrl: null,
    createdAt: '2026-07-01T00:00:00Z',
  }

  await page.route('**/api/security/csrf', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({ token: 'budget-e2e-csrf' }),
  }))
  await page.route('**/api/auth/me', route => fulfill(route, user))
  await page.route('**/api/users', route => fulfill(route, [user]))
  await page.route('**/api/notifications', route => fulfill(route, []))
  await page.route('**/api/dashboard/overview', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify(dashboard()),
  }))
  await page.route('**/api/organizations**', route => fulfill(route, {
    items: [{
      id: organizationId,
      name: 'AI Budget Organization',
      code: 'AIBO',
      ownerId: userId,
      isActive: true,
    }],
  }))

  await page.route(/\/api\/ai(?:\/|$)/, async route => {
    const request = route.request()
    const url = new URL(request.url())
    const path = url.pathname
    const method = request.method()

    if (method === 'GET' && path === '/api/ai/budget/scopes') {
      if (options.denyScopes) {
        await route.fulfill({
          status: 403,
          contentType: 'application/json',
          body: apiFailure('Forbidden', 'AI_PERMISSION_DENIED', 403),
        })
        return
      }
      await fulfill(route, [
        { scopeType: 'organization', scopeId: organizationId, organizationId, projectId: null, name: 'AI Budget Organization' },
        { scopeType: 'project', scopeId: projectId, organizationId, projectId, name: 'AI Budget Project' },
      ])
      return
    }

    if (method === 'GET' && path === '/api/ai/usage') {
      if (state.usageFailures > 0) {
        state.usageFailures -= 1
        await route.fulfill({
          status: 503,
          contentType: 'application/json',
          body: apiFailure('Ledger unavailable', 'AI_PROVIDER_UNAVAILABLE', 503),
        })
        return
      }
      await fulfill(route, usage(Boolean(options.emptyUsage)))
      return
    }

    if (method === 'GET' && path === '/api/ai/budget') {
      await fulfill(route, budget(state, editingEnabled))
      return
    }

    if (method === 'PUT' && path === '/api/ai/budget') {
      state.putBodies.push(request.postDataJSON() as Record<string, unknown>)
      state.putHeaders.push(request.headers())
      if (state.conflictNext) {
        state.conflictNext = false
        state.dailyBudgetUsd = 9
        state.monthlyBudgetUsd = 90
        state.warningAtPercent = 65
        state.version = 'policy-v2-concurrent'
        await route.fulfill({
          status: 409,
          contentType: 'application/json',
          body: apiFailure('Policy changed', 'AI_BUDGET_POLICY_CONFLICT', 409),
        })
        return
      }

      const body = request.postDataJSON() as Record<string, unknown>
      state.dailyBudgetUsd = Number(body.dailyBudgetUsd)
      state.monthlyBudgetUsd = Number(body.monthlyBudgetUsd)
      state.warningAtPercent = Number(body.warningAtPercent)
      state.version = `policy-v${state.putBodies.length + 1}`
      await fulfill(route, budget(state, editingEnabled))
      return
    }

    if (method === 'GET' && path === '/api/ai/health') {
      await fulfill(route, {
        status: 'degraded',
        platformEnabled: true,
        workerEnabled: false,
        degradedReason: 'worker_disabled',
        checkedAt: '2026-07-27T10:00:00Z',
      })
      return
    }

    await route.continue()
  })
  return state
}

async function openBudgetTab(page: Page) {
  await page.goto('/settings', { waitUntil: 'domcontentloaded' })
  await page.locator('.settings-nav-item').filter({ hasText: 'AI Usage & Budget' }).click()
  await expect(page.getByTestId('ai-usage-budget-settings')).toBeVisible()
}

test('admin reviews, confirms and reloads a grounded AI budget policy', async ({ page }) => {
  const state = await installMocks(page)
  await login(page)
  await openBudgetTab(page)

  await expect(page.getByTestId('ai-usage-cost')).toContainText('$7.50')
  await expect(page.getByText('provider-a', { exact: true })).toBeVisible()
  await expect(page.getByText('project_progress_summary', { exact: true })).toBeVisible()
  await expect(page.getByTestId('ai-platform-health')).toContainText('degraded')

  await page.getByLabel('Daily budget (USD)').fill('12')
  await page.getByLabel('Monthly budget (USD)').fill('120')
  await page.getByLabel('Warning threshold (%)').fill('75')
  await page.getByTestId('review-ai-budget-policy').click()
  await expect(page.getByTestId('ai-budget-review')).toContainText('$10.00 → $12.00')
  expect(state.putBodies).toHaveLength(0)

  await page.getByLabel(/Tôi đã kiểm tra phạm vi/).check()
  await page.getByTestId('confirm-ai-budget-policy').click()
  await expect.poll(() => state.putBodies.length).toBe(1)
  expect(state.putBodies[0]).toMatchObject({
    dailyBudgetUsd: 12,
    monthlyBudgetUsd: 120,
    warningAtPercent: 75,
    version: 'policy-v1',
    confirmed: true,
  })
  expect(state.putHeaders[0]['x-csrf-token']).toBe('budget-e2e-csrf')
  await expect(page.getByTestId('ai-budget-status')).toContainText('$12.00')

  await page.reload({ waitUntil: 'domcontentloaded' })
  await page.locator('.settings-nav-item').filter({ hasText: 'AI Usage & Budget' }).click()
  await expect(page.getByLabel('Daily budget (USD)')).toHaveValue('12')
  await expect(page.getByTestId('ai-budget-status')).toContainText('$12.00')
})

test('stale confirmation reloads the winner and requires a new review', async ({ page }) => {
  const state = await installMocks(page, { conflictNext: true })
  await login(page)
  await openBudgetTab(page)

  await page.getByLabel('Daily budget (USD)').fill('14')
  await page.getByLabel('Monthly budget (USD)').fill('140')
  await page.getByTestId('review-ai-budget-policy').click()
  await page.getByLabel(/Tôi đã kiểm tra phạm vi/).check()
  await page.getByTestId('confirm-ai-budget-policy').click()

  await expect(page.getByTestId('ai-budget-conflict')).toContainText('phiên khác')
  await expect(page.getByLabel('Daily budget (USD)')).toHaveValue('9')
  expect(state.putBodies).toHaveLength(1)

  await page.getByLabel('Daily budget (USD)').fill('11')
  await page.getByLabel('Monthly budget (USD)').fill('110')
  await page.getByTestId('review-ai-budget-policy').click()
  await page.getByLabel(/Tôi đã kiểm tra phạm vi/).check()
  await page.getByTestId('confirm-ai-budget-policy').click()
  await expect.poll(() => state.putBodies.length).toBe(2)
  expect(state.putBodies[1].version).toBe('policy-v2-concurrent')
})

test('ledger failure retries to an honest empty and read-only state', async ({ page }) => {
  await installMocks(page, { usageFailures: 1, emptyUsage: true, editingEnabled: false })
  await login(page)
  await openBudgetTab(page)

  await expect(page.getByTestId('ai-budget-error')).toContainText('Ledger unavailable')
  await page.getByRole('button', { name: 'Thử lại' }).click()
  await expect(page.getByTestId('ai-usage-empty')).toContainText('Chưa có usage')
  await expect(page.getByText('READ ONLY', { exact: true })).toBeVisible()
  await expect(page.getByTestId('review-ai-budget-policy')).toBeDisabled()
})

test('unauthorized user sees permission state without ledger disclosure', async ({ page }) => {
  const state = await installMocks(page, { denyScopes: true })
  await login(page)
  await openBudgetTab(page)

  await expect(page.getByTestId('ai-budget-permission')).toContainText('không có phạm vi')
  expect(state.putBodies).toHaveLength(0)
  await expect(page.getByTestId('ai-usage-cost')).toHaveCount(0)
})
