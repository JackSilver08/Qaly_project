import { expect, test, type Page, type Route, type TestInfo } from '@playwright/test'

test.describe.configure({ mode: 'serial' })
test.setTimeout(110_000)

const adminEmail = process.env.E2E_ADMIN_EMAIL ?? 'admin@qaly.dev'
const adminPassword = process.env.E2E_ADMIN_PASSWORD ?? 'Admin@123456'
const projectId = 'd3000000-0000-4000-8000-000000000101'
const tenantId = 'd3000000-0000-4000-8000-000000000102'
const policyId = 'd3000000-0000-4000-8000-000000000103'
const meetingId = 'd3000000-0000-4000-8000-000000000104'
const groupId = 'd3000000-0000-4000-8000-000000000105'

type MockState = {
  policies: Array<Record<string, unknown>>
  consents: Array<Record<string, unknown>>
  requests: Array<Record<string, unknown>>
  policyWrites: Array<Record<string, unknown>>
  consentWrites: Array<Record<string, unknown>>
  requestWrites: Array<Record<string, unknown>>
  mutationHeaders: Array<Record<string, string>>
}

function apiResult(data: unknown) {
  return JSON.stringify({ data, error: null, errorCode: null, isSuccess: true, statusCode: 200 })
}

async function fulfill(route: Route, data: unknown, status = 200) {
  await route.fulfill({ status, contentType: 'application/json', body: apiResult(data) })
}

function retentionPolicy(id = policyId, name = 'Meeting data policy') {
  return {
    id,
    tenantId,
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
    isActive: true,
    policyVersion: 'privacy-v4-e2e.1',
    effectiveFrom: '2026-07-13T00:00:00Z',
    effectiveUntil: null,
    rowVersion: 'AAAAAAAAP03=',
  }
}

async function login(page: Page, returnUrl: string) {
  await page.goto(`/Account/Login?returnUrl=${encodeURIComponent(returnUrl)}`, { waitUntil: 'domcontentloaded' })
  await page.locator('input[name="Email"]').fill(adminEmail)
  await page.locator('input[name="Password"]').fill(adminPassword)
  await page.locator('#loginForm button[type="submit"]').click()
  await expect(page.locator('.shell-header')).toBeVisible({ timeout: 45_000 })
  await page.locator('.welcome-overlay').waitFor({ state: 'hidden', timeout: 5_000 }).catch(() => undefined)
}

async function installPrivacyMocks(page: Page) {
  const state: MockState = {
    policies: [retentionPolicy()],
    consents: [],
    requests: [],
    policyWrites: [],
    consentWrites: [],
    requestWrites: [],
    mutationHeaders: [],
  }

  await page.route('**/api/security/csrf', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({ token: 'p003-e2e-csrf-token' }),
  }))

  await page.route(/\/api\/privacy(?:\/|$)/, async route => {
    const request = route.request()
    const path = new URL(request.url()).pathname
    const method = request.method()

    if (method === 'GET' && path === '/api/privacy/policies') return fulfill(route, state.policies)
    if (method === 'GET' && path === '/api/privacy/consents') return fulfill(route, state.consents)
    if (method === 'GET' && path === '/api/privacy/data-subject-requests') return fulfill(route, state.requests)
    if (method === 'GET' && path === '/api/privacy/legal-holds') return fulfill(route, [])
    if (method === 'GET' && path === '/api/privacy/health') {
      return fulfill(route, {
        enabled: true,
        enforcementEnabled: true,
        workerEnabled: false,
        status: 'degraded',
        pendingRetentionActions: 1,
        failedRetentionActions: 0,
        pendingDataSubjectRequests: 0,
        failedDataSubjectRequests: 0,
      })
    }

    if (method === 'POST' && path === '/api/privacy/policies') {
      const body = request.postDataJSON() as Record<string, unknown>
      state.policyWrites.push(body)
      state.mutationHeaders.push(request.headers())
      const created = retentionPolicy('d3000000-0000-4000-8000-000000000106', String(body.name))
      state.policies.push(created)
      return fulfill(route, created, 201)
    }

    if (method === 'POST' && path === '/api/privacy/consents') {
      const body = request.postDataJSON() as Record<string, unknown>
      state.consentWrites.push(body)
      state.mutationHeaders.push(request.headers())
      const created = {
        id: 'd3000000-0000-4000-8000-000000000107',
        projectId: body.projectId,
        sourceEntityId: body.sourceEntityId ?? null,
        retentionPolicyId: body.retentionPolicyId,
        purpose: body.purpose,
        providerClass: body.providerClass,
        policyVersion: 'privacy-v4-e2e.1',
        noticeVersion: body.noticeVersion,
        status: 'granted',
        grantedAt: '2026-07-13T01:00:00Z',
        revokedAt: null,
        expiresAt: body.expiresAt ?? null,
        rowVersion: 'AAAAAAAAP04=',
      }
      state.consents.push(created)
      return fulfill(route, created, 201)
    }

    if (method === 'POST' && path === '/api/privacy/data-subject-requests') {
      const body = request.postDataJSON() as Record<string, unknown>
      state.requestWrites.push(body)
      state.mutationHeaders.push(request.headers())
      const created = {
        id: 'd3000000-0000-4000-8000-000000000108',
        requestType: body.requestType,
        scope: body.scope,
        status: 'submitted',
        requestedAt: '2026-07-13T01:05:00Z',
        deadlineAt: '2026-08-12T01:05:00Z',
        completedAt: null,
        resultExpiresAt: null,
        legalHoldDetected: false,
        lastErrorCode: null,
      }
      state.requests.push(created)
      return fulfill(route, created, 202)
    }

    return route.fulfill({
      status: 404,
      contentType: 'application/json',
      body: JSON.stringify({ error: `Unhandled P0-03 E2E route: ${method} ${path}` }),
    })
  })

  return state
}

async function capture(page: Page, testInfo: TestInfo, name: string) {
  await page.screenshot({ path: testInfo.outputPath(name), fullPage: true })
}

test('privacy settings writes policy, consent and DSAR with CSRF', async ({ page }, testInfo) => {
  const consoleErrors: string[] = []
  const failedRequests: string[] = []
  page.on('console', message => {
    if (message.type() === 'error') consoleErrors.push(message.text())
  })
  page.on('requestfailed', request => failedRequests.push(request.url()))
  const state = await installPrivacyMocks(page)

  await login(page, '/settings')
  await page.locator('.settings-nav-item').nth(5).click()
  await expect(page.locator('.privacy-surface')).toBeVisible()
  await expect(page.locator('.privacy-loading')).toHaveCount(0)
  await expect(page.locator('.health-line')).toContainText('degraded')

  await page.locator('.privacy-modes button').filter({ hasText: 'Retention' }).click()
  await page.locator('.privacy-editor input[maxlength="120"]').fill('P0-03 E2E retention')
  await page.locator('.privacy-editor .action-button').click()
  await expect.poll(() => state.policyWrites.length).toBe(1)
  expect(state.policyWrites[0]).toMatchObject({
    dataClassification: 'sensitive_collaboration',
    purpose: 'meeting_action_extraction',
    defaultRetentionDays: 30,
    expiryAction: 'redact',
  })
  await expect(page.locator('.record-list')).toContainText('P0-03 E2E retention')

  await page.locator('.privacy-modes button').filter({ hasText: 'Consent' }).click()
  await page.locator('.consent-notice input[type="checkbox"]').check()
  await page.locator('.privacy-editor .action-button').click()
  await expect.poll(() => state.consentWrites.length).toBe(1)
  expect(state.consentWrites[0]).toMatchObject({
    purpose: 'meeting_action_extraction',
    providerClass: 'local',
    sourceType: 'meeting',
    sourceEntityId: null,
    noticeVersion: 'qaly-meeting-privacy-v4.0',
  })

  await page.locator('.privacy-modes button').filter({ hasText: 'Data requests' }).click()
  await page.locator('.privacy-editor .action-button').click()
  await expect.poll(() => state.requestWrites.length).toBe(1)
  expect(state.requestWrites[0]).toMatchObject({ requestType: 'export', scope: 'all' })

  expect(state.mutationHeaders).toHaveLength(3)
  for (const headers of state.mutationHeaders) {
    expect(headers['x-csrf-token']).toBe('p003-e2e-csrf-token')
  }

  await expect(page.locator('.toast-card')).toHaveCount(0, { timeout: 10_000 })
  await capture(page, testInfo, 'privacy-settings-desktop.png')
  await page.setViewportSize({ width: 390, height: 844 })
  await expect(page.locator('.privacy-surface')).toBeVisible()
  const mobileWidth = await page.locator('.privacy-surface').evaluate(element => ({
    left: element.getBoundingClientRect().left,
    right: element.getBoundingClientRect().right,
    viewport: window.innerWidth,
  }))
  expect(mobileWidth.left).toBeGreaterThanOrEqual(0)
  expect(mobileWidth.right).toBeLessThanOrEqual(mobileWidth.viewport + 1)
  await capture(page, testInfo, 'privacy-settings-mobile.png')

  expect(failedRequests).toEqual([])
  expect(consoleErrors).toEqual([])
})

test('meeting privacy gate binds consent to the meeting before speech capture', async ({ page }, testInfo) => {
  const state = await installPrivacyMocks(page)
  await page.route(`**/api/groups/${groupId}/meetings/start`, route => fulfill(route, {
    id: meetingId,
    roomId: `qaly-${groupId}`,
    joinUrl: `/groups/${groupId}/meeting?meetingId=${meetingId}`,
    providerUrl: null,
    accessToken: null,
    accessTokenExpiresAt: null,
  }, 201))

  await login(page, `/groups/${groupId}/meeting`)
  await expect(page.locator('.gm-prejoin__btn')).toBeVisible()
  await page.locator('.gm-prejoin__btn').click()
  await expect(page.locator('.mc-btn--transcript')).toBeVisible({ timeout: 20_000 })
  await page.locator('.mc-btn--transcript').click()

  const dialog = page.locator('.gm-privacy-dialog')
  await expect(dialog).toBeVisible()
  await expect(dialog).toContainText('Meeting data policy')
  const confirmButton = dialog.locator('.gm-btn-primary')
  await expect(confirmButton).toBeDisabled()

  await dialog.locator('.gm-privacy-segmented button').nth(1).click()
  await dialog.locator('.gm-privacy-field select').nth(2).selectOption('90')
  await dialog.locator('.gm-privacy-consent input[type="checkbox"]').check()
  await expect(confirmButton).toBeEnabled()
  await capture(page, testInfo, 'meeting-privacy-desktop.png')

  await page.setViewportSize({ width: 390, height: 844 })
  const dialogBounds = await dialog.evaluate(element => ({
    top: element.getBoundingClientRect().top,
    bottom: element.getBoundingClientRect().bottom,
    left: element.getBoundingClientRect().left,
    right: element.getBoundingClientRect().right,
    width: window.innerWidth,
    height: window.innerHeight,
  }))
  expect(dialogBounds.top).toBeGreaterThanOrEqual(0)
  expect(dialogBounds.left).toBeGreaterThanOrEqual(0)
  expect(dialogBounds.right).toBeLessThanOrEqual(dialogBounds.width + 1)
  expect(dialogBounds.bottom).toBeLessThanOrEqual(dialogBounds.height + 1)
  await capture(page, testInfo, 'meeting-privacy-mobile.png')

  await confirmButton.click()
  await expect.poll(() => state.consentWrites.length).toBe(1)
  expect(state.consentWrites[0]).toMatchObject({
    retentionPolicyId: policyId,
    purpose: 'meeting_action_extraction',
    providerClass: 'any',
    sourceType: 'meeting',
    sourceEntityId: meetingId,
    noticeVersion: 'qaly-meeting-privacy-v4.0',
  })
  expect(state.mutationHeaders[0]['x-csrf-token']).toBe('p003-e2e-csrf-token')
  await expect(dialog).toHaveCount(0)
})
