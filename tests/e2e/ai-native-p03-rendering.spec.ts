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

test('TEST-AI-P03-RENDER-01 presents a readable result and suppresses malformed charts', async ({ page }) => {
  await login(page)
  const now = new Date().toISOString()
  const sessionId = '30303030-3030-3030-3030-303030303030'
  const turnId = '31313131-3131-3131-3131-313131313131'
  const reply = [
    '### Kết luận',
    '**Qaly Release 4.0 đang ở mức rủi ro cao** — hoàn thành **3/28 task (10.7%)**.',
    '',
    '### Ba việc ưu tiên',
    '1. Xử lý task quá hạn trước.',
    '2. Cân lại workload theo capacity thật.',
    '3. Rà Sprint và dependency gần nhất.',
  ].join('\n')

  await page.route('**/api/ai/assistant/sessions/recent', route => route.fulfill({
    contentType: 'application/json',
    body: JSON.stringify({
      sessionId,
      title: 'P03 Project analysis',
      status: 'active',
      version: 1,
      projectId: null,
      createdAt: now,
      updatedAt: now,
      turns: [{
        turnId,
        sequence: 1,
        clientTurnId: '32323232-3232-3232-3232-323232323232',
        userMessage: 'Phân tích Project đang chọn và ba hành động ưu tiên.',
        status: 'completed',
        correlationId: 'p03-rendering-e2e',
        createdAt: now,
        completedAt: now,
        processEvents: [],
        response: {
          schemaId: 'assistant_turn.v1',
          disposition: 'grounded_answer',
          intent: 'grounded.read.v1',
          executionPolicy: 'read_only',
          assistantMessage: reply,
          confidence: 0.95,
          clarification: null,
          artifact: null,
          sourceRefs: ['AnalyticsService', 'Tasks'],
          answer: {
            reply,
            metrics: [
              { label: 'Tổng task', value: '28', tone: 'neutral' },
              { label: 'Quá hạn', value: '20', tone: 'danger' },
            ],
            tables: [],
            charts: [
              { type: 'doughnut', title: 'Trạng thái task lỗi', labels: [], values: [], unit: 'task' },
              { type: 'doughnut', title: 'Phân bố trạng thái task', labels: ['Hoàn thành', 'Đang làm', 'Khác/chưa bắt đầu'], values: [3, 4, 21], unit: 'task' },
            ],
            actions: [],
            files: [],
            sources: ['AnalyticsService', 'Tasks'],
            confidence: 0.95,
            usedAi: true,
            intent: 'project_analysis',
            latencyMs: 20,
            confidenceReason: 'Dữ liệu được đọc trực tiếp từ Qaly.',
            model: { id: 'deepseek-chat', label: 'DeepSeek / deepseek-chat', provider: 'DeepSeek', status: 'live' },
          },
          sessionId,
          turnId,
          sequence: 1,
          sessionVersion: 1,
          clientTurnId: '32323232-3232-3232-3232-323232323232',
          turnStatus: 'completed',
          correlationId: 'p03-rendering-e2e',
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

  await expect(assistant.getByRole('heading', { name: 'Kết luận' })).toBeVisible()
  await expect(assistant.getByRole('heading', { name: 'Ba việc ưu tiên' })).toBeVisible()
  await expect(assistant.locator('.erumi-chart-card')).toHaveCount(1)
  await expect(assistant.getByText('Phân bố trạng thái task', { exact: true })).toBeVisible()
  await expect(assistant.getByText('Trạng thái task lỗi', { exact: true })).toHaveCount(0)
  await expect(assistant.locator('.erumi-chart-card.is-circular canvas')).toBeVisible()
})
