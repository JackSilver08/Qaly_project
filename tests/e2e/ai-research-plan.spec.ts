import { expect, test, type Page } from '@playwright/test'

const adminEmail = process.env.E2E_ADMIN_EMAIL ?? 'admin@qaly.dev'
const adminPassword = process.env.E2E_ADMIN_PASSWORD ?? 'Qaly@Dev2026!'

async function login(page: Page) {
  await page.goto('/Account/Login', { waitUntil: 'domcontentloaded' })
  await page.locator('input[name="Email"]').fill(adminEmail)
  await page.locator('input[name="Password"]').fill(adminPassword)
  await page.locator('button[type="submit"]').click()
  await page.waitForURL(url => !url.pathname.startsWith('/Account/Login'), { timeout: 30_000 })
  await page.locator('.welcome-overlay').waitFor({ state: 'hidden', timeout: 5_000 }).catch(() => undefined)
}

test('TEST-RP-E2E reload renders grounded plan and gates action adapters', async ({ page }) => {
  await login(page)
  const sessionId = 'abababab-abab-abab-abab-abababababab'
  const projectId = '12121212-1212-1212-1212-121212121212'
  const sourceRef = `qaly://project/${projectId}/project/summary@abc123`
  const now = new Date().toISOString()

  await page.route('**/api/ai/assistant/sessions/recent', route => route.fulfill({
    contentType: 'application/json',
    body: JSON.stringify({
      sessionId,
      title: 'Research Plan demo',
      status: 'active',
      version: 1,
      projectId,
      createdAt: now,
      updatedAt: now,
      turns: [{
        turnId: 'cdcdcdcd-cdcd-cdcd-cdcd-cdcdcdcdcdcd',
        sequence: 1,
        clientTurnId: 'efefefef-efef-efef-efef-efefefefefef',
        userMessage: 'Phân tích rủi ro và đề xuất phương án',
        status: 'completed',
        correlationId: 'research-e2e',
        createdAt: now,
        completedAt: now,
        processEvents: [
          { sequence: 1, stage: 'accepted', status: 'completed', publicLabel: 'Đã tiếp nhận yêu cầu', startedAt: now, completedAt: now, durationMs: 0, retryable: false },
          { sequence: 2, stage: 'context_authorization', status: 'completed', publicLabel: 'Đã xác định nguồn được phép', startedAt: now, completedAt: now, durationMs: 5, retryable: false },
          { sequence: 3, stage: 'routing', status: 'completed', publicLabel: 'Đã lập Research Plan', startedAt: now, completedAt: now, durationMs: 10, retryable: false },
          { sequence: 4, stage: 'artifact', status: 'completed', publicLabel: 'Đã chuẩn bị Research Plan có nguồn để bạn xem lại', startedAt: now, completedAt: now, durationMs: 0, retryable: false },
        ],
        response: {
          schemaId: 'assistant_turn.v1',
          disposition: 'research_plan',
          intent: 'research.plan.v1',
          executionPolicy: 'read_only_proposal',
          assistantMessage: 'Mình đã lập Research Plan có kiểm chứng.',
          confidence: 0.9,
          clarification: null,
          artifact: null,
          sourceRefs: [sourceRef],
          answer: {
            reply: 'Research Plan', metrics: [], tables: [], charts: [], actions: [], files: [],
            sources: [sourceRef], confidence: 0.9, usedAi: true, intent: 'research.plan.v1', latencyMs: 42,
            model: { id: 'deepseek-v4-pro', label: 'DeepSeek / deepseek-reasoner', provider: 'DeepSeek', status: 'live' },
          },
          researchPlan: {
            schemaId: 'assistant_research_plan.v1',
            promptId: 'assistant-research-plan',
            promptVersion: '1.0.0',
            objective: 'Phân tích rủi ro và đề xuất phương án',
            scope: { scopeType: 'project', projectId, label: 'Qaly Native AI', sourceRefs: [sourceRef] },
            findings: [{ findingId: 'F1', statement: 'Có một rủi ro deadline cần ưu tiên.', severity: 'high', confidence: 0.91, sourceRefs: [sourceRef] }],
            unknowns: [{ unknownId: 'U1', question: 'Capacity tuần tới là bao nhiêu?', blocking: true }],
            assumptions: ['Capacity chưa được xác minh.'],
            options: [{ optionId: 'O1', title: 'Ưu tiên rủi ro', outcome: 'Giảm rủi ro deadline.', tradeOffs: ['Lùi việc phụ.'], estimatedEffort: '1 ngày', risk: 'Chậm hạng mục phụ.' }],
            recommendedOptionId: 'O1',
            recommendationRationale: 'Phương án bám fact có nguồn.',
            proposedActions: [
              { actionId: 'A1', capabilityId: 'task.create.v1', title: 'Soạn task xử lý rủi ro', dependencyIds: [], draftInput: { message: 'Tạo task xử lý rủi ro', projectId }, sourceRefs: [sourceRef], executionEligible: true, eligibilityReason: 'registered_draft_adapter' },
              { actionId: 'A2', capabilityId: 'project.create.v1', title: 'Tạo project khác', dependencyIds: ['A1'], draftInput: { name: 'Proposal' }, sourceRefs: [sourceRef], executionEligible: false, eligibilityReason: 'capability_not_registered_or_authorized' },
            ],
            warnings: ['Không action nào được tự động chạy.'],
            privacyNotes: ['Nguồn project.summary đã được kiểm quyền.'],
            freshnessAt: now,
            generatedAt: now,
            actualProvider: 'DeepSeek',
            actualModel: 'deepseek-reasoner',
          },
          sessionId,
          turnId: 'cdcdcdcd-cdcd-cdcd-cdcd-cdcdcdcdcdcd',
          sequence: 1,
          sessionVersion: 1,
          clientTurnId: 'efefefef-efef-efef-efef-efefefefefef',
          turnStatus: 'completed',
          correlationId: 'research-e2e',
          replayed: false,
          capabilities: [],
          sourceDisclosures: [{ sourceId: 'project.summary', status: 'read', label: 'Đã đọc nguồn được cấp quyền', sourceRef }],
        },
      }],
    }),
  }))

  await page.goto('/dashboard', { waitUntil: 'domcontentloaded' })
  await page.getByRole('button', { name: /Trợ lý AI/i }).first().click()
  const assistant = page.getByRole('dialog', { name: 'Trợ lý AI' })
  const plan = assistant.getByTestId('assistant-research-plan')
  await expect(plan).toBeVisible()
  await expect(plan.getByText('Có một rủi ro deadline cần ưu tiên.')).toBeVisible()
  await expect(plan.getByText('Capacity tuần tới là bao nhiêu?')).toBeVisible()
  await expect(plan.getByText('DeepSeek / deepseek-reasoner')).toBeVisible()
  await expect(plan.getByTestId('research-action-open-draft')).toHaveCount(1)
  await expect(plan.getByText('Chưa thể áp dụng tự động')).toHaveCount(1)
  await expect(plan.getByText(/qaly:\/\/project/)).toBeVisible()
})
