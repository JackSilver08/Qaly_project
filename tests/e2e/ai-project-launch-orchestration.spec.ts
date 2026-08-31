import { expect, test, type Page } from '@playwright/test'
import { adminEmail, adminPassword } from './support/credentials'

async function login(page: Page) {
  await page.goto('/Account/Login', { waitUntil: 'domcontentloaded' })
  await page.locator('input[name="Email"]').fill(adminEmail)
  await page.locator('input[name="Password"]').fill(adminPassword)
  await page.locator('button[type="submit"]').click()
  await page.waitForURL(url => !url.pathname.startsWith('/Account/Login'), { timeout: 30_000 })
  await page.locator('.welcome-overlay').waitFor({ state: 'hidden', timeout: 5_000 }).catch(() => undefined)
}

const sessionId = '11111111-1111-1111-1111-111111111111'
const turnId = '22222222-2222-2222-2222-222222222222'
const clientTurnId = '33333333-3333-3333-3333-333333333333'
const organizationId = '44444444-4444-4444-4444-444444444444'
const planId = '55555555-5555-5555-5555-555555555555'
const projectId = '66666666-6666-6666-6666-666666666666'
const receiptId = '77777777-7777-7777-7777-777777777777'
const userId = '88888888-8888-8888-8888-888888888888'

function envelope<T>(data: T) {
  return { isSuccess: true, data, error: null, errorCode: null, statusCode: 200 }
}

function launchPlan(executed: boolean, monitored: boolean) {
  const now = new Date().toISOString()
  return {
    planId,
    schemaId: 'project_launch_plan.v1',
    revision: 1,
    state: executed ? 'executed' : 'pending_review',
    briefId: '99999999-9999-9999-9999-999999999999',
    organizationId,
    organizationName: 'Qaly Studio',
    ruleSetId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
    ruleSetVersion: 3,
    scoringVersion: 'project-staffing-deterministic@1.1.0',
    sourceVersionHash: 'source-hash',
    staffingScenarios: [{
      scenarioId: 'balanced', title: 'Cân bằng', description: 'Hard gates trước, trade-off sau.', feasible: true, score: 92,
      managerUserId: userId, managerName: 'Qaly Owner',
      members: [{
        userId, displayName: 'Qaly Owner', proposedRole: 'Manager', proposedHours: 24,
        coveredSkills: [], missingSkills: [], loadAfterPercent: 30,
        decisionReasons: ['declared_capacity'], reviewerCoordinationHours: 2.4,
        weeklyAllocation: [{
          weekKey: 'W01', startsAt: now, endsAt: new Date(Date.now() + 7 * 86_400_000).toISOString(),
          declaredCapacityHours: 40, availabilityReductionHours: 0, existingCommittedHours: 4,
          focusReserveHours: 6, effectiveAvailableHours: 30, proposedDeliveryHours: 12,
          reviewerCoordinationHours: 1.2, loadAfterPercent: 43,
        }],
      }],
      managerCandidates: [{
        userId, displayName: 'Qaly Owner', organizationRole: 'Owner', managerEligible: true,
        staffingEligible: true, hardRejects: [], evidenceSkills: [], evidenceConfidence: 0.9,
        weeklyCapacityHours: 40, windowCapacityHours: 320, existingCommittedHours: 4,
        focusReserveHours: 48, availableHours: 268, proposedHours: 24,
        loadAfterPercent: 30, activeProjectCount: 1, timeZoneId: 'Asia/Ho_Chi_Minh',
        capacityState: 'declared', sourceRefs: [], weeklyCapacity: [{
          weekKey: 'W01', startsAt: now, endsAt: new Date(Date.now() + 7 * 86_400_000).toISOString(),
          declaredCapacityHours: 40, availabilityReductionHours: 0, existingCommittedHours: 4,
          focusReserveHours: 6, effectiveAvailableHours: 30, proposedDeliveryHours: 0,
          reviewerCoordinationHours: 0, loadAfterPercent: 10,
        }],
      }],
      missingSkills: [], blockingReasons: [], risks: [], assumptions: [], ruleDecisions: [], sourceRefs: [], scoringVersion: 'project-staffing-deterministic@1.1.0',
    }],
    selectedScenarioId: executed ? 'balanced' : null,
    deliveryPlan: {
      proposedProjectName: 'Customer Portal SPA', proposedProjectCode: 'CUSTOMER', objective: 'Launch the reviewed SPA flow.',
      startDate: now, endDate: new Date(Date.now() + 56 * 86_400_000).toISOString(),
      scope: ['Authentication', 'Dashboard'], skillGaps: [], scheduleRisks: [],
      externalDeferred: ['Repository and deployment require separate scoped adapters.'],
      sprints: [{ clientId: 'sprint-1', name: 'Foundation', objective: 'Deliver a vertical slice.', startDate: now, endDate: new Date(Date.now() + 14 * 86_400_000).toISOString(), selected: true, tasks: [
        { clientId: 'task-1', title: 'Build vertical slice', description: 'Build the reviewed vertical slice.', acceptanceCriteria: ['The vertical slice is verifiable.'], definitionOfDone: ['Read-back succeeds.'], priority: 'High', estimatedHours: 16, proposedAssigneeId: userId, proposedReviewerId: null, requiredSkillIds: [], requiredSkillNames: [], dependencyClientIds: [], objectiveMetricIds: [], sourceRefs: [], selected: true },
        { clientId: 'task-2', title: 'Verify operations', description: 'Verify the operational path.', acceptanceCriteria: ['The operational path is verified.'], definitionOfDone: ['Evidence is recorded.'], priority: 'Medium', estimatedHours: 8, proposedAssigneeId: userId, proposedReviewerId: null, requiredSkillIds: [], requiredSkillNames: [], dependencyClientIds: ['task-1'], objectiveMetricIds: [], sourceRefs: [], selected: true },
      ] }],
    },
    blockingReasons: [], warnings: [], sourceRefs: [], actualProvider: 'DeepSeek', actualModel: 'deepseek-v4-pro',
    promptVersion: 'project_launch_delivery_plan@1.0.0', createdAt: now, rowRevision: executed ? 2 : 1,
    executionReceipt: executed ? {
      receiptId, schemaId: 'project_launch_execution_receipt.v1', state: 'executed', planId, projectId,
      idempotencyKey: 'e2e', internalTransactionCommitted: true, readBackVerified: true,
      commands: [
        { commandId: 'project', adapterId: 'project.create.v1', status: 'applied', summary: 'Created Project.', deepLink: `/projects/${projectId}` },
        { commandId: 'external', adapterId: 'external.adapters', status: 'external_deferred', summary: 'External adapters were not called.', deepLink: null },
      ],
      createdEntityLinks: [`/projects/${projectId}`], deferredExternalActions: ['Repository provisioning'], rollbackAvailable: true,
      rollbackBlockReason: null, executedAt: now, revision: monitored ? 2 : 1,
    } : null,
    latestReplanProposal: monitored ? {
      proposalId: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', schemaId: 'project_replan_proposal.v1', revision: 1,
      state: 'pending_review', planId, executionId: receiptId, projectId, triggerCodes: ['tasks_overdue'],
      changes: [{ changeType: 'tasks_overdue', severity: 'critical', summary: '1 task is overdue.', baselineValue: '0', currentValue: '1', suggestedAction: 'Review blockers before changing dates.' }],
      blockingUnknowns: [], sourceRefs: [], baselineHash: 'base', currentHash: 'current', requiresConfirmation: true, createdAt: now, rowRevision: 1,
    } : null,
  }
}

function assistantResponse(plan: ReturnType<typeof launchPlan>) {
  return {
    schemaId: 'assistant_turn.v1', disposition: 'project_launch_plan', intent: 'project.staffing.plan.v1',
    executionPolicy: 'read_only_proposal', assistantMessage: 'Review the staffing and delivery plan before confirmation.', confidence: 0.92,
    clarification: null, artifact: null, sourceRefs: [], answer: null, sessionId, turnId, clientTurnId, sequence: 1,
    sessionVersion: 1, turnStatus: 'completed', correlationId: 'e2e-launch', replayed: false, processEvents: [],
    capabilities: [
      { capabilityId: 'project.staffing.plan.v1' },
      { capabilityId: 'project.launch.execute.v1' },
    ],
    sourceDisclosures: [], conversation: null, projectLaunchPlan: plan,
  }
}

test('TEST-PL-BCD-E2E review card confirms with idempotency receipt then produces review-only replan', async ({ page }) => {
  await login(page)
  let currentPlan = launchPlan(false, false)
  let confirmRequests = 0
  const now = new Date().toISOString()
  const session = () => ({
    sessionId, title: 'AI-native Project launch', status: 'active', version: 1, projectId: null, createdAt: now, updatedAt: now,
    turns: [{ turnId, sequence: 1, clientTurnId, userMessage: 'Lập staffing và delivery plan', status: 'completed', correlationId: 'e2e-launch', createdAt: now, completedAt: now, processEvents: [], response: assistantResponse(currentPlan) }],
  })

  await page.route('**/api/ai/assistant/sessions/recent', route => route.fulfill({ contentType: 'application/json', body: JSON.stringify(session()) }))
  await page.route(`**/api/ai/assistant/sessions/${sessionId}`, route => route.fulfill({ contentType: 'application/json', body: JSON.stringify(session()) }))
  await page.route(`**/api/ai/project-launch/plans/${planId}/confirm`, async route => {
    confirmRequests += 1
    const body = route.request().postDataJSON()
    expect(body).toEqual({ confirmed: true, expectedRevision: 1, selectedScenarioId: 'balanced' })
    expect(route.request().headers()['idempotency-key']).toContain(`project-launch:${planId}:1:balanced`)
    currentPlan = launchPlan(true, false)
    await route.fulfill({ status: 502, contentType: 'application/json', body: JSON.stringify({ error: 'Simulated response loss after commit.' }) })
  })
  await page.route(`**/api/ai/project-launch/plans/${planId}`, route =>
    route.fulfill({ contentType: 'application/json', body: JSON.stringify(envelope(currentPlan)) }))
  await page.route(`**/api/ai/project-launch/executions/${receiptId}/monitor`, async route => {
    expect(route.request().postDataJSON()).toEqual({ expectedRevision: 1 })
    currentPlan = launchPlan(true, true)
    await route.fulfill({ contentType: 'application/json', body: JSON.stringify(envelope(currentPlan)) })
  })

  await page.goto('/dashboard', { waitUntil: 'domcontentloaded' })
  await page.getByRole('button', { name: 'Mở Trợ lý AI' }).last().click()
  const assistant = page.getByRole('dialog', { name: 'Trợ lý AI' })
  await expect(assistant).toBeVisible()
  const card = assistant.getByTestId('project-launch-plan')
  await expect(card).toContainText('Customer Portal SPA')
  await expect(card).toContainText('Qaly Owner')
  await expect(card).toContainText('2.4h review')
  await expect(card.getByText('Xem capacity theo từng tuần')).toBeVisible()
  await expect(card.getByText('W01')).toBeHidden()
  await expect(card).toContainText('Foundation · 2 task')
  await expect(card).toContainText('Kết nối bên ngoài chưa được thực hiện')

  page.once('dialog', dialog => dialog.accept())
  await card.getByTestId('project-launch-confirm').click()
  await expect(card.getByTestId('project-launch-receipt')).toContainText('Đọc lại: đã xác minh')
  await expect(card.getByTestId('project-launch-receipt')).toContainText('external_deferred')
  expect(confirmRequests).toBe(1)

  await card.getByTestId('project-launch-monitor').click()
  const proposal = card.getByTestId('project-replan-proposal')
  await expect(proposal).toContainText('Replan proposal rev 1 · chỉ review')
  await expect(proposal).toContainText('Qaly không tự sửa Task, assignee hoặc deadline.')
  await expect(proposal).toContainText('1 task is overdue.')
})
