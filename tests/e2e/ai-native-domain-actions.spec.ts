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

const sessionId = '10000000-0000-0000-0000-000000000001'
const turnId = '10000000-0000-0000-0000-000000000002'
const clientTurnId = '10000000-0000-0000-0000-000000000003'
const projectId = '10000000-0000-0000-0000-000000000004'
const parentTaskId = '10000000-0000-0000-0000-000000000005'
const draftId = '10000000-0000-0000-0000-000000000006'
const backendSkillId = '10000000-0000-0000-0000-000000000009'
const qaSkillId = '10000000-0000-0000-0000-000000000010'

function nativeDraft() {
  return {
    draftId,
    capabilityId: 'task.breakdown.v1',
    schemaId: 'task_breakdown_draft.v1',
    rendererId: 'native-action-review.v1',
    targetType: 'task',
    targetId: parentTaskId,
    projectId,
    status: 'pending_review',
    revision: 1,
    rowVersion: 'AQID',
    payload: {
      parentTaskId,
      parentTaskTitle: 'Canonical parent',
      sourceRef: `/projects/${projectId}/tasks/${parentTaskId}`,
      skillOptions: [
        { skillId: backendSkillId, name: 'Backend / .NET APIs' },
        { skillId: qaSkillId, name: 'QA / Test Engineering' },
      ],
      subtasks: Array.from({ length: 10 }, (_, index) => ({
        title: `Subtask ${index + 1}`,
        description: `Canonical step ${index + 1}`,
        priority: 'Medium',
        estimatedHours: 4,
        dependsOnPrevious: index > 0,
        requiredSkillId: backendSkillId,
        requiredSkillName: 'Backend / .NET APIs',
      })),
    },
    sourceVersion: 'task-source-v1',
    expiresAt: new Date(Date.now() + 86_400_000).toISOString(),
    createdAt: new Date().toISOString(),
    receipt: null,
  }
}

function assistantResponse(extra: Record<string, unknown>) {
  return {
    schemaId: 'assistant_turn.v1',
    disposition: 'native_action_draft',
    intent: 'task.breakdown.v1',
    executionPolicy: 'explicit_batch_confirm',
    assistantMessage: 'Review the ten subtasks, then confirm once.',
    confidence: 0.96,
    clarification: null,
    artifact: null,
    sourceRefs: [`/projects/${projectId}/tasks/${parentTaskId}`],
    answer: null,
    sessionId,
    turnId,
    clientTurnId,
    sequence: 1,
    sessionVersion: 1,
    turnStatus: 'completed',
    correlationId: 'native-e2e',
    replayed: false,
    processEvents: [],
    capabilities: [],
    sourceDisclosures: [],
    conversation: null,
    actualProvider: 'DeepSeek',
    actualModel: 'deepseek-v4-pro',
    ...extra,
  }
}

function storedSession(response: Record<string, unknown>, title: string) {
  const now = new Date().toISOString()
  return {
    sessionId,
    title,
    status: 'active',
    version: 1,
    projectId,
    createdAt: now,
    updatedAt: now,
    turns: [{
      turnId,
      sequence: 1,
      clientTurnId,
      userMessage: title,
      status: 'completed',
      correlationId: 'native-e2e',
      createdAt: now,
      completedAt: now,
      processEvents: [],
      response,
    }],
  }
}

test('TEST-AI-NATIVE-DOMAIN-E2E exact ten subtasks remain editable and one confirm returns canonical receipt', async ({ page }) => {
  await login(page)
  const response = assistantResponse({ nativeActionDraft: nativeDraft() })
  const session = storedSession(response, 'Create exactly ten subtasks')
  await page.route('**/api/ai/assistant/sessions/recent', route => route.fulfill({
    contentType: 'application/json', body: JSON.stringify(session),
  }))
  await page.route(`**/api/ai/assistant/sessions/${sessionId}`, route => route.fulfill({
    contentType: 'application/json', body: JSON.stringify(session),
  }))

  let confirms = 0
  await page.route(`**/api/ai/native-actions/${draftId}/confirm`, async route => {
    confirms += 1
    expect(route.request().headers()['idempotency-key']).toBe(`native-action:${draftId}:AQID`)
    const body = route.request().postDataJSON()
    expect(body.expectedRevision).toBe(1)
    expect(body.rowVersion).toBe('AQID')
    expect(body.payload.subtasks).toHaveLength(10)
    expect(body.payload.subtasks[9].title).toBe('Final verification')
    expect(body.payload.subtasks[9].requiredSkillId).toBe(qaSkillId)
    expect(body.payload.subtasks[9].requiredSkillName).toBe('QA / Test Engineering')
    await route.fulfill({
      contentType: 'application/json',
      body: JSON.stringify({
        schemaId: 'ai_native_action_receipt.v1',
        receiptId: '10000000-0000-0000-0000-000000000007',
        draftId,
        capabilityId: 'task.breakdown.v1',
        status: 'confirmed',
        items: [{
          entityType: 'task',
          entityId: '10000000-0000-0000-0000-000000000008',
          label: 'Final verification',
          url: `/projects/${projectId}/tasks/10000000-0000-0000-0000-000000000008`,
        }],
        readBackLinks: [`/projects/${projectId}/tasks/10000000-0000-0000-0000-000000000008`],
        confirmedAt: new Date().toISOString(),
        replayed: false,
      }),
    })
  })

  await page.goto('/dashboard', { waitUntil: 'domcontentloaded' })
  await page.getByRole('button', { name: 'Mở Trợ lý AI' }).last().click()
  const assistant = page.getByRole('dialog', { name: 'Trợ lý AI' })
  const card = assistant.getByTestId('native-action-draft')
  await expect(card).toBeVisible()
  await expect(card).toContainText('Đích: Canonical parent')
  await expect(card.locator('.native-action-editor > label')).toHaveCount(10)
  await expect(assistant).toContainText('DeepSeek / deepseek-v4-pro')
  const finalSubtask = card.locator('.native-action-editor > label').nth(9)
  await finalSubtask.locator('input').first().fill('Final verification')
  const skillSelect = finalSubtask.getByRole('combobox').last()
  await expect(skillSelect).toBeEditable()
  await skillSelect.selectOption(qaSkillId)
  await card.getByTestId('native-action-confirm').click()
  await expect(card.getByTestId('native-action-receipt')).toContainText('read-back verified')
  await expect(card.getByRole('button', { name: /Final verification/ })).toBeVisible()
  expect(confirms).toBe(1)
})

test('TEST-AI-NATIVE-SAFE-E2E fixed Dev Test manifest requires confirm and renders its report', async ({ page }) => {
  await login(page)
  const runId = '20000000-0000-0000-0000-000000000001'
  const preview = {
    schemaId: 'safe_test_run_preview.v1',
    runId,
    manifestId: 'demo.test.run.v1',
    title: 'AI Native acceptance manifest',
    status: 'review_required',
    environment: 'Development',
    suites: [
      { id: 'unit', label: 'Unit contracts', project: 'Qaly.UnitTests', scope: 'registered filters', estimatedSeconds: 30 },
      { id: 'integration', label: 'Canonical integration', project: 'Qaly.IntegrationTests', scope: 'registered filters', estimatedSeconds: 60 },
      { id: 'chromium', label: 'Chromium journey', project: 'Playwright', scope: 'registered specs', estimatedSeconds: 30 },
    ],
    estimatedSeconds: 120,
    estimatedExternalCost: 0,
    requiresConfirmation: true,
    revision: 1,
    createdAt: new Date().toISOString(),
  }
  const response = assistantResponse({
    disposition: 'safe_test_run_preview',
    intent: 'demo.test.run.v1',
    executionPolicy: 'explicit_confirm_dev_test_only',
    assistantMessage: 'Review the fixed manifest before execution.',
    nativeActionDraft: null,
    safeTestRunPreview: preview,
  })
  const session = storedSession(response, 'Run AI Native acceptance manifest')
  await page.route('**/api/ai/assistant/sessions/recent', route => route.fulfill({
    contentType: 'application/json', body: JSON.stringify(session),
  }))
  await page.route(`**/api/ai/assistant/sessions/${sessionId}`, route => route.fulfill({
    contentType: 'application/json', body: JSON.stringify(session),
  }))
  await page.route(`**/api/ai/assistant/test-runs/${runId}/confirm`, async route => {
    expect(route.request().headers()['idempotency-key']).toBe(`safe-test:${runId}:1`)
    expect(route.request().postDataJSON()).toEqual({ expectedRevision: 1 })
    await route.fulfill({
      contentType: 'application/json',
      body: JSON.stringify({
        schemaId: 'safe_test_run_report.v1',
        runId,
        manifestId: 'demo.test.run.v1',
        status: 'passed',
        revision: 2,
        startedAt: new Date().toISOString(),
        completedAt: new Date().toISOString(),
        events: [{ sequence: 1, suiteId: 'unit', publicLabel: 'Unit contracts passed', status: 'passed', startedAt: new Date().toISOString(), completedAt: new Date().toISOString(), exitCode: 0 }],
        passedSuites: 3,
        failedSuites: 0,
        safeSummary: 'All registered acceptance suites passed.',
      }),
    })
  })

  await page.goto('/dashboard', { waitUntil: 'domcontentloaded' })
  await page.getByRole('button', { name: 'Mở Trợ lý AI' }).last().click()
  const card = page.getByRole('dialog', { name: 'Trợ lý AI' }).getByTestId('safe-test-run-preview')
  await expect(card).toBeVisible()
  await expect(card.locator('.safe-test-suite-list').first().locator('li')).toHaveCount(3)
  page.once('dialog', dialog => dialog.accept())
  await card.getByTestId('safe-test-run-confirm').click()
  await expect(card).toContainText('Unit contracts passed')
  await expect(card).toContainText('All registered acceptance suites passed.')
})

test('TEST-AI-NATIVE-TASK-HUB-E2E manager launcher navigates to canonical task context with a prepared prompt', async ({ page }) => {
  await login(page)
  await page.goto('/tasks', { waitUntil: 'domcontentloaded' })
  await expect(page).toHaveURL(/\/tasks$/)
  await expect(page.getByRole('heading', { name: 'Nhiệm vụ của tôi' })).toBeVisible()
  const dashboard = await page.evaluate(async () => {
    const response = await fetch('/api/dashboard/overview')
    return response.json()
  })
  const managedProject = dashboard.projects.find((project: any) =>
    project.permissions?.aiTier === 'Full' && project.permissions?.canManageAllTasks && project.tasks?.length)
  expect(managedProject, 'seed must include at least one managed Project with a task').toBeTruthy()
  const managedTask = managedProject.tasks[0]
  const taskCard = page.locator('.task-card')
    .filter({ hasText: managedProject.name })
    .filter({ hasText: managedTask.title })
    .locator('.task-card__body')
    .first()
  await expect(taskCard).toBeVisible()
  await taskCard.click()
  await expect(page).toHaveURL(new RegExp(`/projects/${managedProject.id}/tasks/${managedTask.id}$`))
  const taskDetail = page.locator('.task-detail-drawer')
  await expect(taskDetail).toContainText(managedTask.title)
  await expect(page.getByRole('heading', { name: managedProject.name })).toBeVisible()
  await expect(page.getByTestId('task-ai-checklist-launcher')).toBeVisible()
  await expect(page.getByTestId('task-ai-breakdown-launcher')).toBeVisible()
  await page.getByTestId('task-ai-checklist-launcher').click()
  await expect(page).toHaveURL(/\/projects\/[0-9a-f-]{36}\/tasks\/[0-9a-f-]{36}$/i)
  const assistant = page.getByRole('dialog', { name: 'Trợ lý AI' })
  await expect(assistant).toBeVisible()
  await expect(assistant.locator('textarea').last()).toHaveValue(/acceptance checklist/i)
})
