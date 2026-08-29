import { expect, test, type Page } from '@playwright/test'
import { browserApiRequest } from './support/browser-api'
import { adminEmail, adminPassword } from './support/credentials'

function envelope<T>(data: T) {
  return { isSuccess: true, data, error: null, errorCode: null, statusCode: 200 }
}

async function login(page: Page) {
  await page.goto('/Account/Login', { waitUntil: 'domcontentloaded' })
  await page.locator('input[name="Email"]').fill(adminEmail)
  await page.locator('input[name="Password"]').fill(adminPassword)
  await page.locator('button[type="submit"]').click()
  await page.waitForURL(url => !url.pathname.startsWith('/Account/Login'), { timeout: 30_000 })
  await page.locator('.welcome-overlay').waitFor({ state: 'hidden', timeout: 5_000 }).catch(() => undefined)
}

async function apiData<T>(response: Awaited<ReturnType<typeof browserApiRequest>>) {
  const body = await response.json() as { isSuccess?: boolean; data?: T } | T
  if (body && typeof body === 'object' && 'isSuccess' in body) {
    expect(body.isSuccess).toBeTruthy()
    return body.data as T
  }
  return body as T
}

test('TEST-PORTFOLIO-SCHEDULE-E2E native capacity board keeps schedule changes in review until confirm', async ({ page }) => {
  await login(page)
  const dashboardResponse = await page.request.get('/api/dashboard/overview')
  expect(dashboardResponse.ok()).toBeTruthy()
  const dashboardBody = await dashboardResponse.json()
  const dashboard = dashboardBody.data ?? dashboardBody
  const project = dashboard.projects.find((item: any) => item.tasks.some((task: any) => !['Done', 'Completed'].includes(task.status)))
  expect(project).toBeTruthy()
  const task = project.tasks.find((item: any) => !['Done', 'Completed'].includes(item.status))
  const member = project.members[0] ?? { userId: project.ownerId, fullName: project.ownerName ?? 'Project Owner' }
  const now = new Date()
  const end = new Date(now)
  end.setDate(end.getDate() + 14)
  const itemId = '11111111-1111-1111-1111-111111111111'
  const draftId = '22222222-2222-2222-2222-222222222222'
  const jobId = '33333333-3333-3333-3333-333333333333'
  const organizationId = '44444444-4444-4444-4444-444444444444'
  let proposalStatus = 'pending_review'

  const capacity = {
    projectId: project.id,
    organizationId,
    windowStart: now.toISOString(),
    windowEnd: end.toISOString(),
    scoringVersion: 'portfolio-capacity-scheduler.v1',
    visibilityState: 'partial_private_aggregate',
    canManageCapacity: true,
    canGenerateProposal: true,
    generatedAt: now.toISOString(),
    members: [{
      userId: member.userId,
      fullName: member.fullName,
      avatarUrl: null,
      weeklyCapacityHours: 40,
      capacityState: 'declared',
      windowCapacityHours: 80,
      assignedHours: 52,
      remainingHours: 28,
      utilizationPercent: 65,
      openTaskCount: 4,
      missingEstimateCount: 1,
      deadlineCollisionCount: 1,
      hasRestrictedLoad: true,
      projectLoads: [{ projectId: project.id, projectName: project.name, assignedHours: 28, openTaskCount: 2, sourcesRestricted: false }],
      availabilityWindows: [],
      profileRowVersion: 'AQID',
    }],
  }
  const proposal = () => ({
    draftId,
    jobId,
    projectId: project.id,
    organizationId,
    status: proposalStatus,
    schemaId: 'assignment_schedule_proposal.v1',
    scoringVersion: 'portfolio-capacity-scheduler.v1',
    windowStart: now.toISOString(),
    windowEnd: end.toISOString(),
    rowVersion: 'AQID',
    providerName: 'LocalRules',
    modelName: 'portfolio-capacity-scheduler.v1',
    generatedAt: now.toISOString(),
    warnings: ['Một phần tải riêng tư chỉ được dùng dưới dạng tổng hợp.'],
    sources: [
      { key: `task:${task.id}`, type: 'task', entityId: task.id, label: task.title, url: `/projects/${project.id}/tasks/${task.id}`, restricted: false },
      { key: `capacity:${member.userId}`, type: 'member_capacity', entityId: member.userId, label: `Capacity của ${member.fullName}`, url: null, restricted: true },
    ],
    items: [{
      itemId,
      taskId: task.id,
      taskTitle: task.title,
      currentProjectId: project.id,
      projectName: project.name,
      currentAssigneeId: task.assigneeId,
      currentAssigneeName: task.assigneeName,
      proposedAssigneeId: member.userId,
      proposedAssigneeName: member.fullName,
      proposedStart: now.toISOString(),
      proposedDue: end.toISOString(),
      skillCoveragePercent: 100,
      evidenceConfidence: 0.75,
      loadBeforeHours: 52,
      loadAfterHours: 62,
      capacityHours: 80,
      dependencyConflicts: [],
      deadlineRisks: ['Cần kiểm tra deadline với project khác'],
      alternatives: [],
      sourceRefs: [`task:${task.id}`, `capacity:${member.userId}`],
      taskRowVersion: task.rowVersion,
      selected: true,
    }],
    receipt: proposalStatus === 'confirmed' ? {
      draftId,
      executionId: 'schedule:e2e',
      appliedCount: 1,
      appliedTaskIds: [task.id],
      readBackLinks: [`/projects/${project.id}/tasks/${task.id}`],
      confirmedAt: new Date().toISOString(),
    } : null,
  })

  await page.route(`**/api/projects/${project.id}/workload`, route => route.fulfill({
    contentType: 'application/json',
    body: JSON.stringify(envelope({ projectId: project.id, membersWorkload: [{ userId: member.userId, userName: member.fullName, avatarUrl: null, taskCount: 2, estimatedHours: 16, actualHours: 8, completedTaskCount: 1 }] })),
  }))
  await page.route(`**/api/projects/${project.id}/portfolio-capacity**`, route => route.fulfill({ contentType: 'application/json', body: JSON.stringify(envelope(capacity)) }))
  await page.route(`**/api/projects/${project.id}/schedule-proposals`, async route => {
    expect(route.request().method()).toBe('POST')
    await route.fulfill({ contentType: 'application/json', body: JSON.stringify(envelope(proposal())) })
  })
  await page.route(`**/api/projects/${project.id}/schedule-proposals/${draftId}`, async route => {
    await route.fulfill({ contentType: 'application/json', body: JSON.stringify(envelope(proposal())) })
  })
  await page.route(`**/api/projects/${project.id}/schedule-proposals/${draftId}/confirm`, async route => {
    const body = route.request().postDataJSON()
    expect(body.confirmed).toBe(true)
    expect(body.selectedItemIds).toEqual([itemId])
    proposalStatus = 'confirmed'
    await route.fulfill({ contentType: 'application/json', body: JSON.stringify(envelope(proposal())) })
  })

  await page.goto(`/projects/${project.id}`, { waitUntil: 'domcontentloaded' })
  await page.getByRole('button', { name: 'Phân công & Capacity' }).click()
  const copilot = page.getByTestId('portfolio-capacity-copilot')
  await expect(copilot).toBeVisible()
  await expect(copilot.getByText('Có tải riêng tư được tổng hợp và ẩn nguồn')).toBeVisible()
  await copilot.getByRole('button', { name: 'Chọn tất cả task mở' }).click()
  await copilot.getByRole('button', { name: 'Lập phương án' }).click()
  await expect(copilot.getByText('assignment_schedule_proposal.v1')).toBeVisible()
  await expect(copilot.getByText(task.title, { exact: false }).first()).toBeVisible()
  await copilot.getByRole('button', { name: 'Xác nhận phần đã chọn' }).click()
  await expect(copilot.getByText('Đã áp dụng 1 thay đổi')).toBeVisible()
  await expect(copilot.getByRole('link', { name: 'Mở task đã cập nhật' })).toBeVisible()
})

test('TEST-TASK-DRAFT-E2E reload restores source-linked draft and selective confirm shows receipt', async ({ page }) => {
  await login(page)
  const group = await apiData<{ id: string; name: string }>(await browserApiRequest(page, 'POST', '/api/groups', {
    data: { name: `Native draft E2E ${Date.now()}`, color: '#2563eb' },
  }))
  const project = await apiData<{ id: string; name: string }>(await browserApiRequest(page, 'POST', '/api/projects', {
    data: { name: `Native source project ${Date.now()}`, code: null, description: 'E2E native draft', logoUrl: null, startDate: null, endDate: null, organizationId: null, sourceGroupId: group.id },
  }))
  const jobId = '55555555-5555-5555-5555-555555555555'
  const draftId = '66666666-6666-6666-6666-666666666666'
  const messageId = '77777777-7777-7777-7777-777777777777'
  const createdTaskId = '88888888-8888-8888-8888-888888888888'
  let draftStatus = 'pending_review'
  let reviewedTitle = 'Review API contract from selected message'
  let submittedSourceIds: string[] = []

  await page.route(`**/api/ai/groups/${group.id}/task-drafts`, async route => {
    const body = route.request().postDataJSON()
    submittedSourceIds = body.sources.map((source: { sourceEntityId: string }) => source.sourceEntityId)
    expect(body.projectId).toBe(project.id)
    // `GroupAiPanel` sends the canonical provider id `deepseek-chat` (labelled "DeepSeek V4 Pro"
    // in the picker). `deepseek-v4-pro` is the display model name the job receipt reports back.
    expect(body.providerHint).toBe('deepseek-chat')
    await route.fulfill({
      contentType: 'application/json',
      body: JSON.stringify(envelope({ jobId, status: 'queued', isExisting: false })),
    })
  })

  await page.route(`**/api/ai/jobs/${jobId}`, route => route.fulfill({
    contentType: 'application/json',
    body: JSON.stringify(envelope({
      jobId,
      jobType: 'task_draft_native',
      status: 'succeeded',
      progressPercent: 100,
      draftIds: [draftId],
      selectedProvider: 'DeepSeek',
      selectedModel: 'deepseek-v4-pro',
      lastErrorCode: null,
      lastErrorMessage: null,
      lastErrorRetryable: false,
      isMock: false,
    })),
  }))
  await page.route(`**/api/ai/drafts/${draftId}`, async route => {
    if (route.request().method() === 'PATCH') {
      const body = route.request().postDataJSON()
      reviewedTitle = JSON.parse(body.workingPayloadJson).tasks[0].title
    }
    await route.fulfill({
      contentType: 'application/json',
      body: JSON.stringify(envelope({
        draftId,
        aiJobId: jobId,
        projectId: project.id,
        draftType: 'TaskDraft',
        status: draftStatus,
        schemaId: 'task_draft.v5',
        rowVersion: 'AQID',
        workingPayload: {
          schemaId: 'task_draft.v5',
          dataState: 'ready',
          tasks: [{
            clientId: 'draft-1',
            title: reviewedTitle,
            description: 'Acceptance criteria are editable before confirmation.',
            priority: 'High',
            status: 'Todo',
            dueDate: null,
            assigneeId: null,
            selected: true,
            confidence: 0.84,
            sourceRefs: [`message:${messageId}`],
          }],
        },
        originalPayload: {},
        sources: [{ sourceType: 'message', sourceEntityId: messageId }],
        confirmationResult: draftStatus === 'confirmed' ? { createdTaskIds: [createdTaskId] } : null,
      })),
    })
  })
  await page.route(`**/api/ai/drafts/${draftId}/confirm`, async route => {
    const body = route.request().postDataJSON()
    expect(body.confirmAction).toBe('create_tasks')
    expect(JSON.parse(body.editedPayloadJson).tasks[0].selected).toBe(true)
    draftStatus = 'confirmed'
    await route.fulfill({ contentType: 'application/json', body: JSON.stringify(envelope({ draftId, status: 'confirmed', confirmAction: 'create_tasks', createdTaskCount: 1, createdTaskIds: [createdTaskId] })) })
  })

  await page.goto(`/groups/${group.id}`, { waitUntil: 'domcontentloaded' })
  const messageText = `Implement source-linked API review ${Date.now()}`
  await page.getByRole('textbox', { name: 'Nhập tin nhắn...' }).fill(messageText)
  await page.getByRole('button', { name: 'Gửi tin nhắn' }).click()
  const message = page.locator('.team-message').filter({ hasText: messageText })
  await expect(message).toBeVisible()
  await message.hover()
  await message.locator('.team-message__more').click()
  await page.getByRole('button', { name: 'Chọn nhiều tin nhắn' }).click()
  await page.getByTitle('Create a task draft from selected messages').click()
  const review = page.getByTestId('source-linked-task-draft-review')
  await expect(review).toBeVisible()
  expect(submittedSourceIds).toHaveLength(1)
  await expect(review.getByText('DeepSeek', { exact: true })).toBeVisible()
  await expect(review.getByText('deepseek-v4-pro', { exact: true })).toBeVisible()

  await page.reload({ waitUntil: 'domcontentloaded' })
  await page.getByRole('button', { name: 'AI', exact: true }).click()
  await expect(review).toBeVisible()
  await review.getByLabel('Tiêu đề task draft').fill('Reviewed native task title')
  await review.getByRole('button', { name: 'Lưu draft' }).click()
  await expect(review.getByLabel('Tiêu đề task draft')).toHaveValue('Reviewed native task title')
  await review.getByRole('button', { name: 'Tạo các task đã chọn' }).click()
  await expect(review.getByText('Đã xác nhận và có thể đọc lại')).toBeVisible()
  await expect(review.getByRole('link', { name: /Mở task/ })).toBeVisible()

  await browserApiRequest(page, 'DELETE', `/api/projects/${project.id}`).catch(() => undefined)
  await browserApiRequest(page, 'DELETE', `/api/groups/${group.id}`).catch(() => undefined)
})
