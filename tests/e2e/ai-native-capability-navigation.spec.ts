import { expect, test, type Page } from '@playwright/test'
import { adminEmail, adminPassword } from './support/credentials'

async function login(page: Page) {
  await page.goto('/Account/Login', { waitUntil: 'domcontentloaded' })
  await page.locator('input[name="Email"]').fill(adminEmail)
  await page.locator('input[name="Password"]').fill(adminPassword)
  await page.getByRole('button', { name: /Đăng nhập|Dang nhap|Login/i }).click()
  await page.waitForURL(url => !url.pathname.startsWith('/Account/Login'), { timeout: 30_000 })
  await page.locator('.welcome-overlay').waitFor({ state: 'hidden', timeout: 5_000 }).catch(() => undefined)
}

test('TEST-AI-NATIVE-NAV-01 capability overview renders consistent navigation cards and opens the target route', async ({ page }) => {
  await login(page)
  const now = new Date().toISOString()
  const sessionId = '71717171-7171-7171-7171-717171717171'

  await page.route('**/api/ai/assistant/sessions/recent', route => route.fulfill({
    contentType: 'application/json',
    body: JSON.stringify({
      sessionId,
      title: 'Khả năng của Trợ lý AI',
      status: 'active',
      version: 1,
      projectId: null,
      createdAt: now,
      updatedAt: now,
      turns: [{
        turnId: '72727272-7272-7272-7272-727272727272',
        sequence: 1,
        clientTurnId: '73737373-7373-7373-7373-737373737373',
        userMessage: 'Bạn có thể giúp cho tôi những gì?',
        status: 'completed',
        correlationId: 'capability-navigation-e2e',
        createdAt: now,
        completedAt: now,
        processEvents: [],
        response: {
          schemaId: 'assistant_turn.v1',
          disposition: 'grounded_answer',
          intent: 'grounded.read.v1',
          executionPolicy: 'read_only',
          assistantMessage: '**Mình có thể hỗ trợ bạn theo 5 hướng chính.** Chọn một lối tắt bên dưới.',
          confidence: 1,
          clarification: null,
          artifact: null,
          sourceRefs: ['Qaly capability registry'],
          answer: {
            reply: '**Mình có thể hỗ trợ bạn theo 5 hướng chính.** Chọn một lối tắt bên dưới.',
            metrics: [],
            tables: [],
            charts: [],
            actions: [
              { type: 'assistant_navigation', label: 'Dự án', payload: { route: '/projects', description: 'Khởi chạy hoặc tiếp tục một dự án.' }, requiresConfirmation: false },
              { type: 'assistant_navigation', label: 'Nhiệm vụ', payload: { route: '/tasks', description: 'Xem và quản lý công việc.' }, requiresConfirmation: false },
              { type: 'assistant_navigation', label: 'Nhóm & kỹ năng', payload: { route: '/teams', description: 'Kiểm tra nhân sự và kỹ năng.' }, requiresConfirmation: false },
              { type: 'assistant_navigation', label: 'Phân tích', payload: { route: '/analytics', description: 'Phân tích tiến độ và rủi ro.' }, requiresConfirmation: false },
              { type: 'assistant_navigation', label: 'Dashboard', payload: { route: '/dashboard', description: 'Xem tổng quan điều hành.' }, requiresConfirmation: false },
            ],
            files: [],
            sources: ['Qaly capability registry'],
            confidence: 1,
            usedAi: false,
            intent: 'capability_overview',
            latencyMs: 0,
            confidenceReason: 'Menu dựng từ capability registry.',
            model: { id: 'qaly-native', label: 'Qaly Native', provider: 'Qaly', status: 'live' },
          },
          sessionId,
          turnId: '72727272-7272-7272-7272-727272727272',
          sequence: 1,
          sessionVersion: 1,
          clientTurnId: '73737373-7373-7373-7373-737373737373',
          turnStatus: 'completed',
          correlationId: 'capability-navigation-e2e',
          replayed: false,
          processEvents: [],
          capabilities: [],
          sourceDisclosures: [],
        },
      }],
    }),
  }))

  await page.goto('/dashboard', { waitUntil: 'domcontentloaded' })
  await page.getByRole('button', { name: /Mở Trợ lý AI/i }).first().click()
  const assistant = page.getByRole('dialog', { name: 'Trợ lý AI' })

  await expect(assistant.getByRole('button', { name: /^Dự án:/ })).toBeVisible()
  await expect(assistant.locator('.assistant-navigation-action')).toHaveCount(5)
  await assistant.getByRole('button', { name: /^Phân tích:/ }).click()
  await expect(page).toHaveURL(/\/analytics$/)
  await expect(assistant).toBeVisible()
})

test('TEST-AI-NATIVE-NAV-LIVE-01 live session routes capability overview then Project Launch without grounded fallback', async ({ page }) => {
  await login(page)
  const csrfResponse = await page.request.get('/api/security/csrf')
  expect(csrfResponse.ok()).toBeTruthy()
  const csrf = await csrfResponse.json()
  const csrfHeaders = { 'X-CSRF-TOKEN': csrf.token }
  const sessionResponse = await page.request.post('/api/ai/assistant/sessions', {
    headers: csrfHeaders,
    data: {
      context: { route: '/dashboard', module: 'workspace', projectId: null, entityType: null, entityId: null, selectionIds: [] },
      title: 'Live routing regression',
    },
  })
  expect(sessionResponse.ok()).toBeTruthy()
  const session = await sessionResponse.json()

  const capabilityResponse = await page.request.post('/api/ai/assistant/turns', {
    headers: { ...csrfHeaders, 'Idempotency-Key': `live-capability-${Date.now()}` },
    data: {
      message: 'bạn có thể giúp cho tôi những gì',
      context: { route: '/dashboard', module: 'workspace', projectId: null, entityType: null, entityId: null, selectionIds: [] },
      mode: 'agent',
      language: 'vi',
      providerHint: 'deepseek-chat',
      files: [],
      sessionId: session.sessionId,
      expectedVersion: session.version,
      clientTurnId: '74747474-7474-7474-7474-747474747474',
    },
  })
  expect(capabilityResponse.ok()).toBeTruthy()
  const capabilityTurn = await capabilityResponse.json()

  expect(capabilityTurn.intent).toBe('grounded.read.v1')
  expect(capabilityTurn.answer?.intent).toBe('capability_overview')
  expect(capabilityTurn.answer?.actions).toHaveLength(5)

  const launchResponse = await page.request.post('/api/ai/assistant/turns', {
    headers: { ...csrfHeaders, 'Idempotency-Key': `live-launch-${Date.now()}` },
    data: {
      message: 'bạn có thể giúp tôi khởi tạo 1 dự án về web cung cấp dịch vụ spa theo gói được ko',
      context: { route: '/dashboard', module: 'workspace', projectId: null, entityType: null, entityId: null, selectionIds: [] },
      mode: 'agent',
      language: 'vi',
      providerHint: 'deepseek-chat',
      files: [],
      sessionId: session.sessionId,
      expectedVersion: capabilityTurn.sessionVersion,
      clientTurnId: '75757575-7575-7575-7575-757575757575',
    },
  })
  expect(launchResponse.ok()).toBeTruthy()
  const launchTurn = await launchResponse.json()

  expect(launchTurn.intent).toBe('project.launch.analyze.v1')
  expect(launchTurn.answer?.intent).not.toBe('capability_overview')
  expect(launchTurn.projectLaunchBrief || launchTurn.conversation?.questions?.length).toBeTruthy()
})

test('TEST-AI-NATIVE-SESSION-LIVE-01 lists real sessions, switches context, and restores the selected session after reload', async ({ page }) => {
  await login(page)
  const csrfResponse = await page.request.get('/api/security/csrf')
  expect(csrfResponse.ok()).toBeTruthy()
  const csrf = await csrfResponse.json()
  const headers = { 'X-CSRF-TOKEN': csrf.token }
  const marker = Date.now()
  const firstTitle = `Phiên dự án SPA ${marker}`
  const secondTitle = `Phiên phân tích nguồn lực ${marker}`
  const context = { route: '/dashboard', module: 'workspace', projectId: null, entityType: null, entityId: null, selectionIds: [] }

  const firstResponse = await page.request.post('/api/ai/assistant/sessions', {
    headers,
    data: { context, title: firstTitle },
  })
  const secondResponse = await page.request.post('/api/ai/assistant/sessions', {
    headers,
    data: { context, title: secondTitle },
  })
  expect(firstResponse.ok()).toBeTruthy()
  expect(secondResponse.ok()).toBeTruthy()
  const firstSession = await firstResponse.json()
  const secondSession = await secondResponse.json()

  await page.evaluate(sessionId => localStorage.setItem('qaly.ai-native.active-session.v1', sessionId), secondSession.sessionId)
  await page.goto('/dashboard', { waitUntil: 'domcontentloaded' })
  await page.getByRole('button', { name: /Mở Trợ lý AI/i }).first().click()
  const assistant = page.getByRole('dialog', { name: 'Trợ lý AI' })
  await assistant.getByTestId('assistant-session-history-toolbar').click()

  let history = page.getByRole('dialog', { name: 'Lịch sử phiên Trợ lý AI' })
  await expect(history.getByText(firstTitle, { exact: true })).toBeVisible()
  await expect(history.getByText(secondTitle, { exact: true })).toBeVisible()
  await expect(history.locator(`[data-session-id="${secondSession.sessionId}"]`)).toContainText('Đang mở')

  await history.locator(`[data-session-id="${firstSession.sessionId}"] .history-main`).click()
  await expect(history).toBeHidden()
  await page.reload({ waitUntil: 'domcontentloaded' })
  await page.getByRole('button', { name: /Mở Trợ lý AI/i }).first().click()
  await page.getByTestId('assistant-session-history-toolbar').click()

  history = page.getByRole('dialog', { name: 'Lịch sử phiên Trợ lý AI' })
  await expect(history.locator(`[data-session-id="${firstSession.sessionId}"]`)).toContainText('Đang mở')
  await expect(history.getByRole('button', { name: 'Phiên mới' })).toBeVisible()
})
