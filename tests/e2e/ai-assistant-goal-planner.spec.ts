import { expect, test, type Page } from '@playwright/test'
import { adminEmail, adminPassword } from './support/credentials'

async function login(page: Page) {
  await page.goto('/Account/Login', { waitUntil: 'domcontentloaded' })
  await page.locator('input[name="Email"]').fill(adminEmail)
  await page.locator('input[name="Password"]').fill(adminPassword)
  await page.locator('button[type="submit"]').click()
  await page.waitForURL(url => !url.pathname.startsWith('/Account/Login'), { timeout: 30_000 })
}

test('TEST-GS-E2E reload renders goal, selected skill, work plan and safe activity', async ({ page }) => {
  await login(page)
  const now = new Date().toISOString()
  const scope = { scopeType: 'workspace', projectId: null, entityType: null, entityId: null, label: 'Không gian làm việc được phép', confidence: 1, reason: 'Server scope' }
  await page.route('**/api/ai/assistant/sessions/recent', route => route.fulfill({
    contentType: 'application/json',
    body: JSON.stringify({
      sessionId: '11111111-1111-1111-1111-111111111111', title: 'Goal planner demo', status: 'active', version: 1,
      projectId: null, createdAt: now, updatedAt: now,
      turns: [{
        turnId: '22222222-2222-2222-2222-222222222222', sequence: 1,
        clientTurnId: '33333333-3333-3333-3333-333333333333', userMessage: 'Tóm tắt workspace của tôi',
        status: 'completed', correlationId: 'goal-demo', createdAt: now, completedAt: now,
        processEvents: [
          { sequence: 1, stage: 'accepted', status: 'completed', publicLabel: 'Đã tiếp nhận yêu cầu', startedAt: now, completedAt: now },
          { sequence: 2, stage: 'goal_analysis', status: 'completed', publicLabel: 'Đã hiểu mục tiêu và khoanh vùng skill', startedAt: now, completedAt: now },
          { sequence: 3, stage: 'context_authorization', status: 'completed', publicLabel: 'Đã kiểm tra quyền và nguồn', startedAt: now, completedAt: now },
          { sequence: 4, stage: 'capability_handoff', status: 'completed', publicLabel: 'Đã giao cho skill phù hợp', startedAt: now, completedAt: now },
          { sequence: 5, stage: 'answer', status: 'completed', publicLabel: 'Đã kiểm chứng và hoàn tất', startedAt: now, completedAt: now },
        ],
        response: {
          schemaId: 'assistant_turn.v1', disposition: 'grounded_answer', intent: 'grounded.read.v1', executionPolicy: 'read_only',
          assistantMessage: 'Đây là tóm tắt đã được kiểm chứng.', confidence: 0.92, clarification: null, artifact: null,
          sourceRefs: ['qaly://workspace/projects@demo'], answer: null,
          goalAnalysis: {
            schemaId: 'assistant_goal_analysis.v1', objective: 'Tóm tắt workspace của tôi', userJob: 'Hiểu tình hình công việc hiện tại',
            intentFacets: ['summarize'], scopes: [scope],
            selectedSkills: [{ skillId: 'grounded.read.v1', title: 'Tra cứu có căn cứ', fitReason: 'Cần dữ liệu workspace đã kiểm quyền', confidence: 0.92, riskClass: 'read_only', confirmationPolicy: 'none' }],
            missingSkills: [], disposition: 'answerable', confidence: 0.92, warnings: [],
            actualProvider: 'DeepSeek', actualModel: 'deepseek-v4-pro', usedFallback: false,
          },
          workPlan: {
            schemaId: 'assistant_work_plan.v1', objective: 'Tóm tắt workspace của tôi', scope,
            selectedSkillIds: ['grounded.read.v1'], requiresPlanApproval: false,
            steps: [
              { stepId: 'S1', kind: 'analyze', publicLabel: 'Hiểu mục tiêu', skillId: null, dependencyIds: [], verificationIds: ['goal_contract_valid'], mutationClass: 'none', state: 'completed' },
              { stepId: 'S2', kind: 'call_skill', publicLabel: 'Tra cứu dữ liệu được phép', skillId: 'grounded.read.v1', dependencyIds: ['S1'], verificationIds: ['source_grounded'], mutationClass: 'none', state: 'completed' },
            ],
          },
          capabilities: [], sourceDisclosures: [],
        },
      }, {
        turnId: '44444444-4444-4444-4444-444444444444', sequence: 2,
        clientTurnId: '55555555-5555-5555-5555-555555555555', userMessage: 'Tạo một dự án web SPA trong 8 tuần',
        status: 'completed', correlationId: 'guided-demo', createdAt: now, completedAt: now,
        processEvents: [
          { sequence: 1, stage: 'accepted', status: 'completed', publicLabel: 'Đã tiếp nhận yêu cầu', startedAt: now, completedAt: now },
          { sequence: 2, stage: 'goal_analysis', status: 'completed', publicLabel: 'Đã hiểu mục tiêu và giới hạn thao tác', startedAt: now, completedAt: now },
          { sequence: 5, stage: 'answer', status: 'completed', publicLabel: 'Đã trả lời và hướng dẫn bước tiếp theo', startedAt: now, completedAt: now },
        ],
        response: {
          schemaId: 'assistant_turn.v1', disposition: 'guided_answer', intent: 'advisory.answer.v1', executionPolicy: 'analyze_only',
          assistantMessage: 'Mình có thể giúp bạn chuẩn bị dự án web SPA: chốt MVP, milestone và backlog trước khi phân bổ team. Bạn cần phục vụ nhóm người dùng nào, deadline có cố định không và ai duyệt phạm vi?\n\n> Hiện mình chưa thể tự thực hiện trực tiếp trong chat; chưa có dữ liệu nào được thay đổi.',
          confidence: 0.88, clarification: null, artifact: null, sourceRefs: [],
          answer: {
            reply: 'Mình có thể giúp bạn chuẩn bị dự án web SPA.', metrics: [], tables: [], charts: [], actions: [], files: [], sources: [],
            confidence: 0.88, usedAi: true, intent: 'workspace_analytics', latencyMs: 840,
            model: { id: 'deepseek-v4-pro', label: 'DeepSeek / deepseek-v4-pro', provider: 'DeepSeek', status: 'live' },
          },
          goalAnalysis: {
            schemaId: 'assistant_goal_analysis.v1', objective: 'Tạo một dự án web SPA trong 8 tuần', userJob: 'Khởi động dự án web SPA',
            intentFacets: ['project_create'], scopes: [scope], selectedSkills: [],
            missingSkills: [{ skillId: 'project.create.v1', title: 'Soạn dự án', reason: 'Thao tác tạo dự án trực tiếp chưa khả dụng.', suggestedPath: 'Internal only' }],
            disposition: 'unsupported_but_analyzed', confidence: 0.9, warnings: [],
            actualProvider: 'Qaly policy router', actualModel: 'known-missing-skill-v1', usedFallback: false,
          },
          workPlan: {
            schemaId: 'assistant_work_plan.v1', objective: 'Tạo một dự án web SPA trong 8 tuần', scope,
            selectedSkillIds: [], requiresPlanApproval: false,
            steps: [{ stepId: 'S1', kind: 'analyze', publicLabel: 'Hiểu mục tiêu', skillId: null, dependencyIds: [], verificationIds: ['goal_contract_valid'], mutationClass: 'none', state: 'completed' }],
          },
          capabilities: [], sourceDisclosures: [],
        },
      }],
    }),
  }))

  await page.goto('/dashboard', { waitUntil: 'domcontentloaded' })
  await page.getByRole('button', { name: 'Mở Trợ lý AI' }).first().click()
  const dialog = page.getByRole('dialog', { name: 'Trợ lý AI' })
  await expect(dialog.getByTestId('assistant-work-plan').first()).toBeVisible()
  await expect(dialog.getByText('Tra cứu có căn cứ', { exact: true })).toBeVisible()
  await expect(dialog.getByText('Đã hiểu mục tiêu và khoanh vùng skill')).toBeVisible()
  await expect(dialog.getByText('Tra cứu dữ liệu được phép')).toBeVisible()
  await expect(dialog.getByText(/Mình có thể giúp bạn chuẩn bị dự án web SPA/)).toBeVisible()
  await expect(dialog.getByText('Có thể tư vấn · chưa thể tự thao tác')).toBeVisible()
  await expect(dialog.getByText('DeepSeek / deepseek-v4-pro').last()).toBeVisible()
  const guidedPlan = dialog.getByTestId('assistant-work-plan').last()
  await expect(dialog.locator('.assistant-body > .assistant-primary-answer + .assistant-work-plan-card').last()).toBeVisible()
  await expect(guidedPlan.locator('details.assistant-missing-skill')).not.toHaveAttribute('open', '')
  await expect(dialog.getByText('project.create.v1', { exact: true })).not.toBeVisible()
})
