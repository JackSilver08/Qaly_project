import { expect, test, type Page } from '@playwright/test'
import { browserApiRequest } from './support/browser-api'
import { adminEmail, adminPassword } from './support/credentials'

// These browser journeys deliberately create and remove shared in-memory Group/Project data.
// Keep them serial so one journey cannot change another journey's dashboard tenant selector.
test.describe.configure({ mode: 'serial' })

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

async function createLinkedGroupProject(page: Page, suffix: string) {
  const group = await apiData<{ id: string; name: string }>(await browserApiRequest(page, 'POST', '/api/groups', {
    data: { name: `${suffix} group ${Date.now()}`, color: '#2563eb' },
  }))
  const project = await apiData<{ id: string; name: string }>(await browserApiRequest(page, 'POST', '/api/projects', {
    data: {
      name: `${suffix} project ${Date.now()}`,
      code: null,
      description: 'AI-native browser evidence',
      logoUrl: null,
      startDate: null,
      endDate: null,
      organizationId: null,
      sourceGroupId: group.id,
    },
  }))
  return { group, project }
}

async function cleanupLinkedGroupProject(page: Page, groupId?: string, projectId?: string) {
  if (projectId) await browserApiRequest(page, 'DELETE', `/api/projects/${projectId}`).catch(() => undefined)
  if (groupId) await browserApiRequest(page, 'DELETE', `/api/groups/${groupId}`).catch(() => undefined)
}

test('TEST-CAND-009-E2E renders and restores a tenant-scoped grounded strategic brief', async ({ page }) => {
  await login(page)
  const overviewResponse = await page.request.get('/api/dashboard/overview')
  expect(overviewResponse.ok()).toBeTruthy()
  const overviewBody = await overviewResponse.json()
  const overview = overviewBody.data ?? overviewBody
  const project = overview.projects[0]
  expect(project).toBeTruthy()
  const organizationId = project.organizationId ?? project.id
  const jobId = '90000000-0000-0000-0000-000000000009'
  let submittedOrganizationId = ''

  await page.evaluate(() => {
    for (const key of Object.keys(localStorage)) {
      if (key.startsWith('qaly:dashboard-strategic-brief:')) localStorage.removeItem(key)
    }
  })
  await page.route('**/api/ai/dashboard/strategic-brief', async route => {
    submittedOrganizationId = route.request().postDataJSON().organizationId
    await route.fulfill({ contentType: 'application/json', body: JSON.stringify(envelope({ jobId, status: 'queued', isExisting: false })) })
  })
  await page.route(`**/api/ai/jobs/${jobId}/result`, route => route.fulfill({
    contentType: 'application/json',
    body: JSON.stringify(envelope({
      schemaId: 'dashboard_strategic_brief.v1',
      cacheHit: false,
      isMock: false,
      sourceStale: false,
      result: {
        schemaId: 'dashboard_strategic_brief.v1', organizationId, requestedById: 'browser-user', snapshotAt: new Date().toISOString(),
        coverage: { visibleProjectCount: 1, includedTaskCount: 4, excludedPrivateTaskCount: 1, visibility: 'authorized_tenant_non_private' },
        metrics: { projectCount: 1, overdue: 1, completionRate: 50 },
        summaryPoints: [{ text: 'Một dự án có một công việc quá hạn cần xử lý.', metricRefs: ['overdue'], sourceRefs: [`project:${project.id}`] }],
        risks: [{ severity: 'high', title: 'Deadline đang có rủi ro.', metricRefs: ['overdue'], sourceRefs: [] }],
        priorities: [{ title: 'Xử lý việc quá hạn', rationale: 'Snapshot có overdue=1.', metricRefs: ['overdue'], sourceRefs: [] }],
        sourceRefs: [{ key: `project:${project.id}`, type: 'project', entityId: project.id, projectId: project.id, label: project.name, url: `/projects/${project.id}`, version: 'v1' }],
        warnings: [],
      },
    })),
  }))
  await page.route(`**/api/ai/jobs/${jobId}`, route => route.fulfill({
    contentType: 'application/json',
    body: JSON.stringify(envelope({ jobId, status: 'succeeded', progressPercent: 100, selectedProvider: 'DeepSeek', selectedModel: 'deepseek-v4-pro', lastErrorCode: null, lastErrorMessage: null, lastErrorRetryable: false, isMock: false })),
  }))

  await page.goto('/dashboard', { waitUntil: 'domcontentloaded' })
  const card = page.getByTestId('dashboard-strategic-brief')
  await expect(card).toBeVisible()
  await card.locator('.ai-button').click()
  await expect(card.getByText('Một dự án có một công việc quá hạn cần xử lý.')).toBeVisible()
  await expect(card.getByText('deepseek-v4-pro', { exact: false })).toBeVisible()
  expect(submittedOrganizationId).toBe(organizationId)

  await page.reload({ waitUntil: 'domcontentloaded' })
  await expect(card.getByText('Một dự án có một công việc quá hạn cần xử lý.')).toBeVisible()
})

test('TEST-CAND-007-E2E preserves exact selected-message grounding and reload read-back', async ({ page }) => {
  await login(page)
  const { group, project } = await createLinkedGroupProject(page, 'Selected summary')
  const jobId = '70000000-0000-0000-0000-000000000007'
  let selectedMessageIds: string[] = []

  try {
    await page.route(`**/api/ai/groups/${group.id}/summaries`, async route => {
      const body = route.request().postDataJSON()
      expect(body.projectId).toBe(project.id)
      expect(body.providerHint).toBe('deepseek-chat')
      selectedMessageIds = body.messageIds
      await route.fulfill({ contentType: 'application/json', body: JSON.stringify(envelope({ jobId, status: 'queued', isExisting: false })) })
    })
    await page.route(`**/api/ai/jobs/${jobId}/result`, route => {
      const messageId = selectedMessageIds[0]
      const sourceKey = `message:${messageId}`
      return route.fulfill({
        contentType: 'application/json',
        body: JSON.stringify(envelope({
          schemaId: 'group_selected_summary.v1', cacheHit: false, isMock: false, sourceStale: false,
          result: {
            schemaId: 'group_selected_summary.v1', groupId: group.id, projectId: project.id,
            messageRange: { messageIds: selectedMessageIds, fromMessageId: messageId, toMessageId: messageId },
            summary: 'Nhóm đã thống nhất kiểm tra API theo đúng tin nhắn được chọn.', summarySourceRefs: [sourceKey],
            keyDecisions: [{ text: 'Ưu tiên kiểm tra API.', sourceRefs: [sourceKey] }], openQuestions: [], actionCandidates: [],
            sourceRefs: [{ key: sourceKey, messageId, url: `/groups/${group.id}?messageId=${messageId}` }], warnings: [],
          },
        })),
      })
    })
    await page.route(`**/api/ai/jobs/${jobId}`, route => route.fulfill({
      contentType: 'application/json',
      body: JSON.stringify(envelope({ jobId, status: 'succeeded', progressPercent: 100, draftIds: [], selectedProvider: 'DeepSeek', selectedModel: 'deepseek-v4-pro', lastErrorCode: null, lastErrorMessage: null, lastErrorRetryable: false, isMock: false })),
    }))

    await page.goto(`/groups/${group.id}`, { waitUntil: 'domcontentloaded' })
    const messageText = `Grounded selected summary ${Date.now()}`
    await browserApiRequest(page, 'POST', `/api/groups/${group.id}/messages`, {
      data: { content: messageText, messageType: 'Text', replyToMessageId: null },
    })
    await page.reload({ waitUntil: 'domcontentloaded' })
    const message = page.locator('.team-message').filter({ hasText: messageText })
    await expect(message).toBeVisible()
    await message.hover()
    await message.locator('.team-message__more').click()
    await page.getByRole('button', { name: 'Chọn nhiều tin nhắn' }).click()
    await page.getByTitle('Summarize selected messages').click()

    const review = page.getByTestId('group-selected-summary-review')
    await expect(review).toBeVisible()
    await expect(review.getByText('Nhóm đã thống nhất kiểm tra API theo đúng tin nhắn được chọn.')).toBeVisible()
    await expect(review.getByText('deepseek-v4-pro', { exact: true })).toBeVisible()
    expect(selectedMessageIds).toHaveLength(1)

    await page.reload({ waitUntil: 'domcontentloaded' })
    await page.getByRole('button', { name: 'AI', exact: true }).click()
    await expect(review.getByText('Nhóm đã thống nhất kiểm tra API theo đúng tin nhắn được chọn.')).toBeVisible()
  } finally {
    await cleanupLinkedGroupProject(page, group.id, project.id)
  }
})

test('TEST-CAND-010-E2E renders grounded meeting checknote from durable read-back', async ({ page }) => {
  await login(page)
  const { group, project } = await createLinkedGroupProject(page, 'Meeting checknote')
  let meetingId: string | undefined

  try {
    const meeting = await apiData<{ id: string }>(await browserApiRequest(page, 'POST', `/api/groups/${group.id}/meetings/start`))
    meetingId = meeting.id
    const checknote = {
      meetingImportId: '10000000-0000-0000-0000-000000000010', aiJobId: '11000000-0000-0000-0000-000000000010', draftId: '12000000-0000-0000-0000-000000000010',
      summary: 'Cuộc họp thống nhất hoàn thành API trước thứ sáu.',
      summaryEvidence: [{ quote: 'Hoàn thành API trước thứ sáu.', sourceStart: 20, sourceEnd: 52 }],
      decisions: [{ text: 'Chốt deadline thứ sáu.', reason: null, sourceEvidence: 'Hoàn thành API trước thứ sáu.', sourceStart: 20, sourceEnd: 52 }],
      risks: [],
      actionItems: [{ title: 'Hoàn thành API', description: 'Kiểm tra contract.', suggestedOwner: 'Test User', dueDate: null, priority: 'High', sourceEvidence: 'Hoàn thành API trước thứ sáu.', sourceStart: 20, sourceEnd: 52, mappingStatus: 'Pending', mappedTaskId: null }],
      processingMode: 'local', privacyStatus: 'Allowed', provider: 'DeepSeek', model: 'deepseek-v4-pro', cacheHit: false, isMock: false,
    }
    await page.route(`**/api/meetings/${meetingId}/auto-checknote?projectId=${project.id}`, route => route.fulfill({
      contentType: 'application/json', body: JSON.stringify(envelope(checknote)),
    }))

    await page.goto(`/groups/${group.id}/meeting?meetingId=${meetingId}`, { waitUntil: 'domcontentloaded' })
    await page.locator('.gm-tab').filter({ hasText: 'AI' }).click()
    await page.locator('.gm-select').selectOption(project.id)
    const result = page.getByTestId('meeting-checknote-results')
    await expect(result).toBeVisible()
    await expect(result.getByText('Cuộc họp thống nhất hoàn thành API trước thứ sáu.')).toBeVisible()
    await expect(result.getByText('deepseek-v4-pro', { exact: true })).toBeVisible()

    await page.reload({ waitUntil: 'domcontentloaded' })
    await page.locator('.gm-tab').filter({ hasText: 'AI' }).click()
    await page.locator('.gm-select').selectOption(project.id)
    await expect(result.getByText('Cuộc họp thống nhất hoàn thành API trước thứ sáu.')).toBeVisible()
  } finally {
    if (meetingId) await browserApiRequest(page, 'POST', `/api/groups/${group.id}/meetings/${meetingId}/end`).catch(() => undefined)
    await cleanupLinkedGroupProject(page, group.id, project.id)
  }
})
