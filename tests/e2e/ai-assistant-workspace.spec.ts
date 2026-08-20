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

async function openAssistant(page: Page) {
  await page.goto('/dashboard', { waitUntil: 'domcontentloaded' })
  await page.getByRole('button', { name: /Mở Trợ lý AI/i }).first().click()
  return page.getByRole('dialog', { name: 'Trợ lý AI' })
}

async function mockEmptyAssistantSession(page: Page) {
  const sessionId = 'dddddddd-dddd-dddd-dddd-dddddddddddd'
  await page.route('**/api/security/csrf', route => route.fulfill({
    contentType: 'application/json',
    body: JSON.stringify({ token: 'assistant-workspace-csrf' }),
  }))
  await page.route('**/api/ai/assistant/sessions/recent', route => route.fulfill({
    contentType: 'application/json',
    body: 'null',
  }))
  await page.route('**/api/ai/assistant/sessions', route => route.fulfill({
    status: 201,
    contentType: 'application/json',
    body: JSON.stringify({
      sessionId,
      title: 'Cuộc trò chuyện Trợ lý AI',
      status: 'active',
      version: 0,
      projectId: null,
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
      turns: [],
    }),
  }))
}

test('TEST-AW-01..07 desktop workspace resizes, persists, resets and keeps the empty composer at the bottom', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 1000 })
  await login(page)
  await mockEmptyAssistantSession(page)
  await page.addInitScript(() => {
    window.localStorage.removeItem('qaly-ai-agent-workspace-layout-v1')
    window.localStorage.removeItem('qaly-ai-action-composer-session-v1')
  })

  const assistant = await openAssistant(page)
  await expect(assistant).toBeVisible()
  await expect.poll(async () => {
    const box = await assistant.boundingBox()
    return box ? Math.abs(box.x - (1440 - 16 - box.width)) : Number.POSITIVE_INFINITY
  }).toBeLessThan(3)

  const initialBox = await assistant.boundingBox()
  const composer = assistant.locator('.chat-empty .composer')
  const composerBox = await composer.boundingBox()
  expect(initialBox).not.toBeNull()
  expect(composerBox).not.toBeNull()
  expect(composerBox!.y + composerBox!.height).toBeGreaterThan(initialBox!.y + initialBox!.height - 48)
  await expect(assistant.locator('.composer:visible')).toHaveCount(1)

  const leftResize = assistant.getByRole('separator', { name: 'Thay đổi chiều rộng Trợ lý AI' })
  const topResize = assistant.getByRole('separator', { name: 'Thay đổi chiều cao Trợ lý AI' })
  const leftBox = await leftResize.boundingBox()
  const topBox = await topResize.boundingBox()
  expect(leftBox).not.toBeNull()
  expect(topBox).not.toBeNull()

  await page.mouse.move(leftBox!.x + 2, leftBox!.y + leftBox!.height / 2)
  await page.mouse.down()
  await page.mouse.move(leftBox!.x - 140, leftBox!.y + leftBox!.height / 2)
  await page.mouse.up()
  await page.mouse.move(topBox!.x + topBox!.width / 2, topBox!.y + 2)
  await page.mouse.down()
  await page.mouse.move(topBox!.x + topBox!.width / 2, topBox!.y - 70)
  await page.mouse.up()

  const resizedBox = await assistant.boundingBox()
  expect(resizedBox!.width).toBeGreaterThan(initialBox!.width + 100)
  expect(resizedBox!.height).toBeGreaterThan(initialBox!.height + 40)

  await assistant.getByRole('button', { name: 'Đóng' }).click()
  const launcher = page.getByRole('button', { name: /Mở Trợ lý AI/i }).first()
  await expect(launcher).toBeFocused()
  await launcher.click()
  const reopenedBox = await assistant.boundingBox()
  expect(Math.abs(reopenedBox!.width - resizedBox!.width)).toBeLessThan(3)
  expect(Math.abs(reopenedBox!.height - resizedBox!.height)).toBeLessThan(3)

  await assistant.getByRole('button', { name: 'Đặt lại kích thước Trợ lý AI' }).click()
  const resetBox = await assistant.boundingBox()
  expect(resetBox!.width).toBeLessThan(resizedBox!.width)
  await expect(assistant.locator('.composer:visible')).toHaveCount(1)
})

test('TEST-AW-08..10 mobile workspace is full-screen, has no resize controls and keeps submit reachable', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 })
  await login(page)
  await mockEmptyAssistantSession(page)
  const assistant = await openAssistant(page)
  const box = await assistant.boundingBox()

  expect(box).not.toBeNull()
  expect(Math.abs(box!.width - 390)).toBeLessThan(2)
  expect(Math.abs(box!.height - 844)).toBeLessThan(2)
  await expect(assistant.locator('.workspace-resize-handle:visible')).toHaveCount(0)
  await expect(assistant.getByLabel('Nhập yêu cầu cho Trợ lý AI')).toBeVisible()
  await expect(assistant.getByRole('button', { name: 'Gửi câu hỏi' })).toBeVisible()
})

test('TEST-AS-E2E reload restores the canonical turn and safe process steps from the server', async ({ page }) => {
  await login(page)
  const sessionId = 'dddddddd-dddd-dddd-dddd-dddddddddddd'
  const clientTurnId = 'eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee'
  const now = new Date().toISOString()

  await page.route('**/api/ai/assistant/sessions/recent', route => route.fulfill({
    contentType: 'application/json',
    body: JSON.stringify({
      sessionId,
      title: 'Cuộc trò chuyện Trợ lý AI',
      status: 'active',
      version: 1,
      projectId: null,
      createdAt: now,
      updatedAt: now,
      turns: [{
        turnId: 'ffffffff-ffff-ffff-ffff-ffffffffffff',
        sequence: 1,
        clientTurnId,
        userMessage: 'Tóm tắt workspace của tôi',
        status: 'completed',
        correlationId: clientTurnId,
        createdAt: now,
        completedAt: now,
        processEvents: [
          { sequence: 1, stage: 'accepted', status: 'completed', publicLabel: 'Đã tiếp nhận yêu cầu', startedAt: now, completedAt: now, durationMs: 0, retryable: false },
          { sequence: 2, stage: 'context_authorization', status: 'completed', publicLabel: 'Đã xác định ngữ cảnh được phép', startedAt: now, completedAt: now, durationMs: 20, retryable: false },
          { sequence: 3, stage: 'routing', status: 'completed', publicLabel: 'Đã định tuyến capability', startedAt: now, completedAt: now, durationMs: 10, retryable: false },
          { sequence: 4, stage: 'answer', status: 'completed', publicLabel: 'Đã hoàn tất phản hồi có kiểm soát', startedAt: now, completedAt: now, durationMs: 0, retryable: false },
        ],
        response: {
          schemaId: 'assistant_turn.v1',
          disposition: 'grounded_answer',
          intent: 'grounded.read.v1',
          executionPolicy: 'read_only',
          assistantMessage: 'Đây là phản hồi đã được khôi phục từ session máy chủ.',
          confidence: 0.9,
          clarification: null,
          artifact: null,
          sourceRefs: ['/dashboard'],
          answer: null,
          sessionId,
          turnId: 'ffffffff-ffff-ffff-ffff-ffffffffffff',
          sequence: 1,
          sessionVersion: 1,
          clientTurnId,
          turnStatus: 'completed',
          correlationId: clientTurnId,
          replayed: false,
          capabilities: [{
            capabilityId: 'grounded.read.v1',
            kind: 'read',
            inputSchemaId: 'assistant_grounded_read_request.v1',
            outputSchemaId: 'assistant_turn.v1',
            requiredScopes: ['project.read'],
            contextSources: ['workspace.projects'],
            riskClass: 'read_only',
            confirmationPolicy: 'none',
            modelProfile: 'reasoning_strong',
            rendererId: 'assistant-answer.v1',
            featureFlag: 'AiJobsV4:AssistantContextRegistryEnabled',
          }],
          sourceDisclosures: [{
            sourceId: 'workspace.projects',
            status: 'read',
            label: 'Đã đọc nguồn Qaly đã kiểm quyền',
            sourceRef: 'qaly://workspace/projects@abc123',
            reasonCode: null,
          }],
        },
      }],
    }),
  }))

  const assistant = await openAssistant(page)
  await expect(assistant.getByText('Tóm tắt workspace của tôi')).toBeVisible()
  await expect(assistant.getByText('Đây là phản hồi đã được khôi phục từ session máy chủ.')).toBeVisible()
  const processDisclosure = assistant.locator('details.assistant-process-disclosure')
  await expect(processDisclosure).not.toHaveAttribute('open', '')
  await processDisclosure.locator('summary').click()
  await expect(assistant.getByText('Đã xác định ngữ cảnh được phép')).toBeVisible()
  await assistant.getByText('Ngữ cảnh đã kiểm tra · 1 nguồn').click()
  await expect(assistant.getByText('grounded.read.v1')).toBeVisible()
  await expect(assistant.getByText('workspace.projects')).toBeVisible()
  await expect(assistant.getByText('Đã đọc nguồn Qaly đã kiểm quyền')).toBeVisible()
})
