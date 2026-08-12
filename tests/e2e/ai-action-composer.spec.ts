import { expect, test, type Page } from '@playwright/test'
import { adminEmail, adminPassword } from './support/credentials'

async function login(page: Page) {
  await page.goto('/Account/Login', { waitUntil: 'domcontentloaded' })
  await page.locator('input[name="Email"]').fill(adminEmail)
  await page.locator('input[name="Password"]').fill(adminPassword)
  await page.getByRole('button', { name: /Đăng nhập|Dang nhap|Login/i }).click()
  await page.waitForURL(url => !url.pathname.startsWith('/Account/Login'), { timeout: 30_000 })
  await page.locator('.welcome-overlay').waitFor({ state: 'hidden', timeout: 5_000 }).catch(() => undefined)
  await expect(page.locator('.shell-header')).toBeVisible()
}

function envelope<T>(data: T, statusCode = 200) {
  return { data, error: null, errorCode: null, isSuccess: true, statusCode }
}

test('TEST-ACTION-E2E unified AI assistant shows real progress, editable review and execution receipt', async ({ page }) => {
  await login(page)
  await page.goto('/dashboard', { waitUntil: 'domcontentloaded' })

  const jobId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'
  const draftId = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb'
  const taskId = 'cccccccc-cccc-cccc-cccc-cccccccccccc'
  let projectId = ''
  let jobPolls = 0
  let confirmed = false
  const assistantSessionId = 'dddddddd-dddd-dddd-dddd-dddddddddddd'

  await page.route('**/api/security/csrf', async route => {
    await route.fulfill({
      contentType: 'application/json',
      body: JSON.stringify({ token: 'playwright-csrf-token' }),
    })
  })

  await page.route('**/api/ai/assistant/sessions/recent', async route => {
    await route.fulfill({ contentType: 'application/json', body: 'null' })
  })

  await page.route('**/api/ai/assistant/sessions', async route => {
    await route.fulfill({
      status: 201,
      contentType: 'application/json',
      body: JSON.stringify({
        sessionId: assistantSessionId,
        title: 'Cuộc trò chuyện Trợ lý AI',
        status: 'active',
        version: 0,
        projectId: null,
        createdAt: new Date().toISOString(),
        updatedAt: new Date().toISOString(),
        turns: [],
      }),
    })
  })

  await page.route('**/api/ai/assistant/turns', async route => {
    const body = route.request().postDataJSON()
    expect(body.sessionId).toBe(assistantSessionId)
    expect(body.expectedVersion).toBe(0)
    expect(body.clientTurnId).toBeTruthy()
    expect(route.request().headers()['idempotency-key']).toContain(assistantSessionId)
    projectId = body.context.projectId
    await route.fulfill({
      contentType: 'application/json',
      body: JSON.stringify({
        schemaId: 'assistant_turn.v1',
        disposition: 'registered_action',
        intent: 'task.create.v1',
        executionPolicy: 'draft_then_confirm',
        assistantMessage: 'Mình đã hiểu yêu cầu và sẽ soạn bản nháp để bạn duyệt.',
        confidence: 0.94,
        clarification: null,
        artifact: {
          kind: 'task_action_plan',
          schemaId: 'ai_action_intent_envelope.v1',
          message: body.message,
          projectId,
        },
        sourceRefs: [`/projects/${projectId}`],
        answer: null,
        sessionId: assistantSessionId,
        turnId: 'eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee',
        sequence: 1,
        sessionVersion: 1,
        clientTurnId: body.clientTurnId,
        turnStatus: 'completed',
        correlationId: body.clientTurnId,
        replayed: false,
        processEvents: [
          { sequence: 1, stage: 'accepted', status: 'completed', publicLabel: 'Đã tiếp nhận yêu cầu', startedAt: new Date().toISOString(), completedAt: new Date().toISOString(), retryable: false },
          { sequence: 2, stage: 'routing', status: 'completed', publicLabel: 'Đã xác định action an toàn', startedAt: new Date().toISOString(), completedAt: new Date().toISOString(), retryable: false },
        ],
      }),
    })
  })

  await page.route('**/api/ai/actions/compose', async route => {
    const body = route.request().postDataJSON()
    projectId = body.context.projectId
    await route.fulfill({
      status: 202,
      contentType: 'application/json',
      body: JSON.stringify(envelope({ jobId, status: 'queued' }, 202)),
    })
  })

  await page.route(`**/api/ai/jobs/${jobId}`, async route => {
    jobPolls += 1
    const succeeded = jobPolls > 1
    await route.fulfill({
      contentType: 'application/json',
      body: JSON.stringify({
        jobId,
        projectId,
        status: succeeded ? 'succeeded' : 'running',
        progressPercent: succeeded ? 100 : 35,
        attemptCount: 1,
        maxAttempts: 3,
        createdAt: new Date(Date.now() - 2_000).toISOString(),
        startedAt: new Date(Date.now() - 1_500).toISOString(),
        finishedAt: succeeded ? new Date().toISOString() : null,
        lastErrorCode: null,
        lastErrorMessage: null,
        lastErrorRetryable: false,
        selectedProvider: 'DeepSeek',
        selectedModel: 'deepseek-v4-pro',
        draftIds: succeeded ? [draftId] : [],
      }),
    })
  })

  await page.route(`**/api/ai/jobs/${jobId}/activity**`, async route => {
    const events = confirmed
      ? [
          activity(4, 'execute_commands', 'succeeded', 'Đã thực hiện hành động'),
          activity(5, 'persist_receipt', 'succeeded', 'Đã lưu biên nhận'),
          activity(6, 'read_back', 'succeeded', 'Đã đọc lại kết quả'),
        ]
      : [
          activity(1, 'understand_intent', 'succeeded', 'Đã hiểu yêu cầu'),
          activity(2, 'route_model', 'running', 'Đang định tuyến DeepSeek V4 Pro'),
          activity(3, 'await_confirmation', 'waiting_user', 'Đang chờ bạn xác nhận'),
        ]
    await route.fulfill({
      contentType: 'application/json',
      body: JSON.stringify(envelope({
        jobId,
        jobStatus: confirmed ? 'succeeded' : (jobPolls > 1 ? 'succeeded' : 'running'),
        startedAt: new Date(Date.now() - 2_000).toISOString(),
        finishedAt: jobPolls > 1 ? new Date().toISOString() : null,
        lastSequence: confirmed ? 6 : 3,
        cancellable: jobPolls <= 1,
        events,
      })),
    })
  })

  const plan = () => ({
    schemaId: 'ai_action_intent_envelope.v1',
    schemaVersion: '1.0',
    projectId,
    sourceVersion: 'source-v1',
    userIntent: 'Tạo task frontend có tiêu chí nghiệm thu.',
    intentType: 'task.create',
    confidence: 0.92,
    targetEntities: [{ type: 'project', id: projectId, label: 'Demo project' }],
    assumptions: [],
    missingFields: [],
    warnings: [],
    options: [{
      optionId: 'balanced',
      label: 'Cân bằng',
      summary: 'Một task frontend có thể duyệt.',
      tradeOffs: ['Ưu tiên tốc độ triển khai.'],
      commands: [{
        commandId: 'task-1',
        toolName: 'task.create.v1',
        toolVersion: '1.0',
        title: 'AI draft task',
        description: 'Draft only.',
        acceptanceCriteria: ['UI hoạt động', 'Có test'],
        priority: 'High',
        dueDate: new Date(Date.now() + 86_400_000 * 7).toISOString(),
        estimatedHours: 8,
        assigneeId: null,
        assigneeMode: 'unassigned',
        requiredSkills: [],
        sourceRefs: [`/projects/${projectId}`],
      }],
    }],
    review: { selectedOptionId: 'balanced', selectedCommandIds: ['task-1'] },
    generatedAt: new Date().toISOString(),
  })

  await page.route(`**/api/ai/jobs/${jobId}/result`, async route => {
    await route.fulfill({
      contentType: 'application/json',
      body: JSON.stringify(envelope({ result: plan(), draftIds: [draftId], sourceStale: false })),
    })
  })
  await page.route(`**/api/ai/drafts/${draftId}`, async route => {
    const actionReceipt = confirmed ? receipt(projectId, taskId) : null
    await route.fulfill({
      contentType: 'application/json',
      body: JSON.stringify(envelope({
        draftId,
        status: confirmed ? 'confirmed' : 'pending_review',
        workingPayload: plan(),
        rowVersion: 'AQID',
        confirmationResult: confirmed
          ? { status: 'confirmed', createdTaskCount: 1, actionReceipt }
          : null,
      })),
    })
  })
  await page.route(`**/api/ai/drafts/${draftId}/confirm`, async route => {
    confirmed = true
    const edited = JSON.parse(route.request().postDataJSON().editedPayloadJson)
    expect(edited.options[0].commands[0].title).toBe('Reviewed task from browser')
    await route.fulfill({
      contentType: 'application/json',
      body: JSON.stringify(envelope({
        status: 'confirmed',
        createdTaskCount: 1,
        actionReceipt: receipt(projectId, taskId),
      })),
    })
  })

  await page.getByRole('button', { name: /Mở Trợ lý AI/i }).first().click()
  await expect(page.getByText('Bạn muốn Qaly giúp gì?')).toBeVisible()
  await expect(page.getByText('Bạn muốn Qaly chuẩn bị việc gì?')).toHaveCount(0)
  await page.getByLabel('Nhập yêu cầu cho Trợ lý AI').fill('Tạo task frontend có tiêu chí nghiệm thu.')
  await page.getByRole('button', { name: 'Gửi câu hỏi' }).click()

  await expect(page.getByText(/Đã chạy \d+ giây/)).toBeVisible()
  await expect(page.getByText('Đang định tuyến DeepSeek V4 Pro')).toBeVisible()
  await expect(page.getByText('Đã soạn xong — chưa thay đổi dữ liệu')).toBeVisible({ timeout: 10_000 })

  const artifactSplitter = page.getByRole('separator', { name: 'Thay đổi độ rộng hội thoại và bản nháp' })
  await expect(artifactSplitter).toBeVisible()
  const initialConversationRatio = Number(await artifactSplitter.getAttribute('aria-valuenow'))
  await artifactSplitter.press('ArrowRight')
  await expect(artifactSplitter).toHaveAttribute('aria-valuenow', String(initialConversationRatio + 3))
  await page.getByRole('button', { name: 'Thu gọn bản nháp AI' }).click()
  await expect(page.locator('.assistant-artifact-pane')).toHaveCount(0)
  await page.getByRole('button', { name: 'Mở bản nháp AI' }).click()
  await expect(page.locator('.assistant-artifact-pane')).toBeVisible()

  await page.getByLabel('Tiêu đề').fill('Reviewed task from browser')
  await page.getByRole('button', { name: 'Xác nhận và tạo task' }).click()

  await expect(page.getByText('Đã thực hiện sau khi bạn xác nhận')).toBeVisible()
  await expect(page.getByText('Reviewed task from browser')).toBeVisible()
  await expect(page.getByText('Đã đọc lại kết quả')).toBeVisible()

  await page.reload({ waitUntil: 'domcontentloaded' })
  await page.locator('.welcome-overlay').waitFor({ state: 'hidden', timeout: 5_000 }).catch(() => undefined)
  await page.getByRole('button', { name: /Mở Trợ lý AI/i }).first().click()
  await page.getByRole('button', { name: 'Mở bản nháp AI' }).click()
  await expect(page.getByText('Đã thực hiện sau khi bạn xác nhận')).toBeVisible({ timeout: 10_000 })
  await expect(page.locator('.receipt-meta').getByText('DeepSeek · deepseek-v4-pro')).toBeVisible()
  await expect(page.getByText('Reviewed task from browser')).toBeVisible()
})

test('TEST-ACTION-LIVE-WORKER-01 live worker claims Action Composer job and persists a reviewable draft', async ({ page }) => {
  test.setTimeout(70_000)
  await login(page)
  const projectsResponse = await page.request.get('/api/projects?page=1&pageSize=20')
  expect(projectsResponse.ok()).toBeTruthy()
  const projectsPayload = await projectsResponse.json()
  const project = (projectsPayload.data?.items ?? projectsPayload.items ?? [])
    .find((item: { status?: string }) => item.status !== 'Archived')
  expect(project?.id).toBeTruthy()

  const csrfResponse = await page.request.get('/api/security/csrf')
  expect(csrfResponse.ok()).toBeTruthy()
  const csrf = await csrfResponse.json()
  const composeResponse = await page.request.post('/api/ai/actions/compose', {
    headers: {
      'X-CSRF-TOKEN': csrf.token,
      'Idempotency-Key': `live-action-compose-${Date.now()}`,
    },
    data: {
      message: 'Soạn hai task chi tiết cho Sprint 1: hoàn thiện API dịch vụ và kiểm thử luồng thanh toán. Chỉ tạo bản nháp để duyệt.',
      context: {
        route: `/projects/${project.id}`,
        module: 'project_tasks',
        projectId: project.id,
        entityType: 'project',
        entityId: project.id,
      },
      language: 'vi',
      modelProfile: 'action_composer_strong',
      maximumOptions: 2,
      maximumEstimatedCostUsd: 0.08,
      cacheMode: 'bypass',
    },
  })
  expect(composeResponse.status()).toBe(202)
  const createdPayload = await composeResponse.json()
  const created = createdPayload.data ?? createdPayload
  expect(created.jobId).toBeTruthy()

  async function jobDetail() {
    const response = await page.request.get(`/api/ai/jobs/${created.jobId}`)
    expect(response.ok()).toBeTruthy()
    const payload = await response.json()
    return payload.data ?? payload
  }

  await expect.poll(async () => (await jobDetail()).status, {
    message: 'Worker phải claim job tương tác thay vì để queued vô hạn',
    timeout: 15_000,
    intervals: [250, 500, 1_000],
  }).not.toBe('queued')

  await expect.poll(async () => (await jobDetail()).status, {
    message: 'Action Composer phải tạo xong bản nháp hoặc trả trạng thái terminal rõ ràng',
    timeout: 50_000,
    intervals: [1_000, 2_000, 3_000],
  }).toBe('succeeded')

  const completed = await jobDetail()
  expect(completed.draftIds?.length).toBeGreaterThan(0)
  expect(completed.selectedProvider).toBeTruthy()
  expect(completed.selectedModel).toBeTruthy()
  const resultResponse = await page.request.get(`/api/ai/jobs/${created.jobId}/result`)
  expect(resultResponse.ok()).toBeTruthy()
  const resultPayload = await resultResponse.json()
  const result = resultPayload.data ?? resultPayload
  expect(result.draftIds?.length).toBeGreaterThan(0)
  expect(result.result?.options?.length).toBeGreaterThan(0)
})

test('TEST-UA-E2E chat function call opens the canonical task composer without mutating data', async ({ page }) => {
  await login(page)
  await page.goto('/dashboard', { waitUntil: 'domcontentloaded' })

  const jobId = '99999999-9999-9999-9999-999999999999'
  const clarifiedProjectId = '88888888-8888-8888-8888-888888888888'
  const assistantSessionId = '77777777-7777-7777-7777-777777777777'
  let assistantSessionVersion = 0
  let routedMessage = ''
  let routedProjectId = ''

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
      sessionId: assistantSessionId,
      title: 'Cuộc trò chuyện Trợ lý AI',
      status: 'active',
      version: 0,
      projectId: null,
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
      turns: [],
    }),
  }))

  await page.route('**/api/ai/assistant/turns', async route => {
    const request = route.request().postDataJSON()
    expect(request.sessionId).toBe(assistantSessionId)
    expect(request.expectedVersion).toBe(assistantSessionVersion)
    assistantSessionVersion += 1
    routedMessage = request.message
    if (!request.context.projectId) {
      await route.fulfill({
        contentType: 'application/json',
        body: JSON.stringify({
          schemaId: 'assistant_turn.v1',
          disposition: 'clarification_required',
          intent: 'clarification.v1',
          executionPolicy: 'none',
          assistantMessage: 'Mình hiểu bạn muốn tạo task. Hãy chọn dự án.',
          confidence: 1,
          clarification: {
            questionId: 'task-create-project',
            field: 'projectId',
            prompt: 'Bạn muốn tạo các task này trong dự án nào?',
            choices: [{ id: clarifiedProjectId, label: 'Qaly Native AI', description: 'QALY-AI' }],
            allowFreeText: false,
            turn: 1,
            maxTurns: 3,
          },
          artifact: null,
          sourceRefs: ['Erumi intent router'],
          answer: null,
          sessionId: assistantSessionId,
          turnId: crypto.randomUUID(),
          sequence: assistantSessionVersion,
          sessionVersion: assistantSessionVersion,
          clientTurnId: request.clientTurnId,
          turnStatus: 'completed',
          correlationId: request.clientTurnId,
          replayed: false,
          processEvents: [],
        }),
      })
      return
    }
    routedProjectId = request.context.projectId
    await route.fulfill({
      contentType: 'application/json',
      body: JSON.stringify({
        schemaId: 'assistant_turn.v1',
        disposition: 'registered_action',
        intent: 'task.create.v1',
        executionPolicy: 'draft_then_confirm',
        assistantMessage: 'Mình đã hiểu và đang soạn phương án task để bạn duyệt.',
        confidence: 0.94,
        clarification: null,
        artifact: {
          kind: 'task_action_plan',
          schemaId: 'ai_action_intent_envelope.v1',
          message: request.message,
          projectId: routedProjectId,
        },
        sourceRefs: [`/projects/${routedProjectId}`],
        answer: null,
        sessionId: assistantSessionId,
        turnId: crypto.randomUUID(),
        sequence: assistantSessionVersion,
        sessionVersion: assistantSessionVersion,
        clientTurnId: request.clientTurnId,
        turnStatus: 'completed',
        correlationId: request.clientTurnId,
        replayed: false,
        processEvents: [],
      }),
    })
  })

  const composeRequest = page.waitForRequest('**/api/ai/actions/compose')
  await page.route('**/api/ai/actions/compose', route => route.fulfill({
    status: 202,
    contentType: 'application/json',
    body: JSON.stringify(envelope({ jobId, status: 'queued' }, 202)),
  }))
  await page.route(`**/api/ai/jobs/${jobId}`, route => route.fulfill({
    contentType: 'application/json',
    body: JSON.stringify(envelope({
      jobId,
      status: 'failed',
      progressPercent: 0,
      attemptCount: 1,
      maxAttempts: 3,
      createdAt: new Date().toISOString(),
      startedAt: new Date().toISOString(),
      finishedAt: new Date().toISOString(),
      lastErrorCode: 'provider_unavailable',
      lastErrorMessage: 'Provider chưa được cấu hình trong preview.',
      lastErrorRetryable: true,
      selectedProvider: null,
      selectedModel: null,
      draftIds: [],
    })),
  }))
  await page.route(`**/api/ai/jobs/${jobId}/activity**`, route => route.fulfill({
    contentType: 'application/json',
    body: JSON.stringify(envelope({
      jobId,
      jobStatus: 'failed',
      startedAt: new Date().toISOString(),
      finishedAt: new Date().toISOString(),
      lastSequence: 1,
      cancellable: false,
      events: [activity(1, 'provider_failure', 'failed', 'Provider chưa sẵn sàng')],
    })),
  }))

  await page.getByRole('button', { name: /Mở Trợ lý AI/i }).first().click()
  const contextChip = page.locator('.ctx-chip')
  if (await contextChip.isVisible()) await contextChip.click()
  await page.getByLabel(/Nhập yêu cầu/).fill('Tạo 3 task frontend, backend và QA cho đăng nhập')
  await page.getByRole('button', { name: 'Gửi câu hỏi' }).click()

  await expect(page.getByText('Bạn muốn tạo các task này trong dự án nào?')).toBeVisible()
  await page.getByRole('button', { name: /Qaly Native AI/ }).click()

  const request = await composeRequest
  const body = request.postDataJSON()
  expect(routedProjectId).toBeTruthy()
  expect(routedMessage).toContain('Tạo 3 task')
  expect(body.context.projectId).toBe(routedProjectId)
  expect(body.message).toBe(routedMessage)
  await expect(page.locator('.erumi-side-drawer.is-workspace')).toBeVisible()
  await expect(page.getByText('Provider chưa được cấu hình trong preview.')).toBeVisible()
})

function receipt(projectId: string, taskId: string) {
  return {
    executionId: 'dddddddd-dddd-dddd-dddd-dddddddddddd',
    status: 'succeeded',
    provider: 'DeepSeek',
    model: 'deepseek-v4-pro',
    executedAt: new Date().toISOString(),
    commandResults: [{
      commandId: 'task-1',
      status: 'succeeded',
      entityId: taskId,
      entityLabel: 'Reviewed task from browser',
      entityUrl: `/projects/${projectId}?taskId=${taskId}`,
      errorCode: null,
      errorMessage: null,
      appliedSkillCount: 0,
    }],
    readBackLinks: [`/projects/${projectId}?taskId=${taskId}`],
  }
}

function activity(sequence: number, stage: string, status: string, publicLabel: string) {
  return {
    eventId: `eeeeeeee-eeee-eeee-eeee-${sequence.toString().padStart(12, '0')}`,
    sequence,
    stage,
    status,
    publicLabel,
    current: null,
    total: null,
    startedAt: new Date(Date.now() - 1_000).toISOString(),
    completedAt: status === 'running' ? null : new Date().toISOString(),
    durationMs: status === 'running' ? null : 125,
    retryable: false,
    receiptLink: null,
  }
}
