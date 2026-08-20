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

const sessionId = '10101010-1010-1010-1010-101010101010'
const organizationId = '20202020-2020-2020-2020-202020202020'
const sourceRefs = [
  `qaly://organization/${organizationId}/organization.summary@e2e`,
  `qaly://organization/${organizationId}/organization.rulebook@v3`,
]

function questions(includeDeadline: boolean) {
  return [
    ...(includeDeadline ? [{
      id: 'launch.deadline',
      text: 'Mốc hoàn thành hoặc timebox mong muốn là khi nào?',
      blocking: true,
      reason: 'Deadline thay đổi phạm vi và tính khả thi.',
      quickReplies: [{ value: '8_weeks', label: '8 tuần' }],
      allowFreeText: true,
    }] : []),
    {
      id: 'launch.team_size',
      text: 'Quy mô nhóm mong muốn là bao nhiêu?',
      blocking: false,
      reason: 'Dùng để hiệu chỉnh phương án.',
      quickReplies: [{ value: 'small', label: '3–5 người' }],
      allowFreeText: true,
    },
  ]
}

function launchBrief(revision: number, includeDeadline: boolean) {
  return {
    briefId: `30303030-3030-3030-3030-30303030303${revision}`,
    schemaId: 'project_launch_brief.v1',
    revision,
    state: 'needs_input',
    organizationId,
    organizationName: 'Qaly Studio',
    objective: 'Khởi chạy web SPA trong 8 tuần',
    proposedProjectName: 'Customer Portal SPA',
    scope: ['SPA responsive', 'Đăng nhập và dashboard'],
    exclusions: ['Không tự tạo Project trong CAND-023A'],
    successMeasures: ['Luồng chính chạy end-to-end'],
    facts: ['Organization Rulebook v3 đang hiệu lực'],
    assumptions: ['API nghiệp vụ hiện hữu được tái sử dụng'],
    unknowns: includeDeadline ? ['Deadline chưa được xác nhận'] : ['Quy mô nhóm chưa được xác nhận'],
    questions: questions(includeDeadline),
    rulebookStatus: 'effective',
    ruleSetId: '40404040-4040-4040-4040-404040404040',
    ruleSetVersion: 3,
    ruleDecisions: [{
      ruleKey: 'max_active_projects',
      result: 'pass',
      severity: 'block',
      explanation: 'Số dự án đang hoạt động nằm trong giới hạn Rulebook.',
    }],
    sourceRefs,
    actualProvider: 'DeepSeek',
    actualModel: 'deepseek-v4-pro',
    promptVersion: 'project_launch_brief.v1.0.0',
    createdAt: new Date().toISOString(),
  }
}

function conversation(revision: number, includeDeadline: boolean) {
  return {
    schemaId: 'assistant_conversation_turn.v2',
    conversationDisposition: 'provisional_answer_with_questions',
    actionDisposition: 'artifact_only',
    answer: revision === 1
      ? 'Mình đã lập brief ban đầu và cần chốt một vài thông tin.'
      : 'Đã ghi nhận timebox 8 tuần và cập nhật brief.',
    questions: questions(includeDeadline),
    guidance: null,
    capabilityGap: null,
    proposedActions: ['Review Project Launch Brief'],
    sources: sourceRefs,
    confidence: 0.91,
    actualProvider: 'DeepSeek',
    actualModel: 'deepseek-v4-pro',
    promptVersion: 'assistant_answer_first.v1.0.0',
  }
}

function launchResponse(revision: number, includeDeadline: boolean, turnId: string, clientTurnId: string) {
  const now = new Date().toISOString()
  return {
    schemaId: 'assistant_turn.v1',
    disposition: 'project_launch_brief',
    intent: 'project.launch.analyze.v1',
    executionPolicy: 'read_only_proposal',
    assistantMessage: revision === 1
      ? 'Mình đã lập Project Launch Brief để bạn xem lại.'
      : 'Đã cập nhật Project Launch Brief theo timebox 8 tuần.',
    confidence: 0.91,
    clarification: null,
    artifact: null,
    sourceRefs,
    answer: {
      reply: 'Project Launch Brief', metrics: [], tables: [], charts: [], actions: [], files: [], sources: sourceRefs,
      confidence: 0.91, usedAi: true, intent: 'project.launch.analyze.v1', latencyMs: 220,
      model: { id: 'deepseek-v4-pro', label: 'DeepSeek / deepseek-v4-pro', provider: 'DeepSeek', status: 'live' },
    },
    sessionId,
    turnId,
    clientTurnId,
    sequence: revision,
    sessionVersion: revision,
    turnStatus: 'completed',
    correlationId: `launch-${revision}`,
    replayed: false,
    processEvents: [
      { sequence: 1, stage: 'retrieve', status: 'completed', publicLabel: 'Đã đọc nguồn được cấp quyền', stepId: 'retrieve', attempt: 1, sourceRefs, startedAt: now, completedAt: now },
      { sequence: 2, stage: 'analyze', status: 'completed', publicLabel: 'Đã phân tích bằng model đã chọn', stepId: 'analyze', attempt: 1, actualProvider: 'DeepSeek', actualModel: 'deepseek-v4-pro', startedAt: now, completedAt: now },
      { sequence: 3, stage: 'verify', status: 'completed', publicLabel: 'Đã kiểm tra schema, nguồn và chính sách', stepId: 'verify', attempt: 1, sourceRefs, startedAt: now, completedAt: now },
      { sequence: 4, stage: 'present', status: 'completed', publicLabel: 'Đã chuẩn bị artifact chỉ xem lại', stepId: 'present', attempt: 1, startedAt: now, completedAt: now },
    ],
    capabilities: [],
    sourceDisclosures: sourceRefs.map((sourceRef, index) => ({ sourceId: index ? 'organization.rulebook' : 'organization.summary', status: 'read', label: 'Đã đọc nguồn được cấp quyền', sourceRef })),
    conversation: conversation(revision, includeDeadline),
    projectLaunchBrief: launchBrief(revision, includeDeadline),
  }
}

test('TEST-AI-NATIVE-LOOP-E2E reload renders verified launch brief and progressive reply creates revision', async ({ page }) => {
  const rendererErrors: string[] = []
  page.on('console', message => {
    if (message.type() === 'error') rendererErrors.push(message.text())
  })
  await login(page)
  const firstTurnId = '50505050-5050-5050-5050-505050505050'
  const firstClientTurnId = '60606060-6060-6060-6060-606060606060'
  const firstResponse = launchResponse(1, true, firstTurnId, firstClientTurnId)
  const now = new Date().toISOString()
  const session = {
    sessionId, title: 'Khởi chạy web SPA', status: 'active', version: 1, projectId: null, createdAt: now, updatedAt: now,
    turns: [{
      turnId: firstTurnId, sequence: 1, clientTurnId: firstClientTurnId,
      userMessage: 'Khởi chạy dự án web SPA', status: 'completed', correlationId: 'launch-1',
      createdAt: now, completedAt: now, processEvents: firstResponse.processEvents, response: firstResponse,
    }],
  }
  let savedDraft: any = null

  await page.route('**/api/ai/assistant/sessions?includeArchived=true', route => route.fulfill({
    contentType: 'application/json',
    body: JSON.stringify([{
      sessionId, title: 'Khởi chạy web SPA', status: 'active', version: 1, projectId: null,
      createdAt: now, updatedAt: now, archivedAt: null, turnCount: 1,
      lastMessage: 'Mình đã lập Project Launch Brief để bạn xem lại.',
    }]),
  }))
  await page.route('**/api/ai/assistant/sessions/recent', route => route.fulfill({ contentType: 'application/json', body: JSON.stringify({ ...session, clarificationDraft: savedDraft }) }))
  await page.route(`**/api/ai/assistant/sessions/${sessionId}`, route => route.fulfill({ contentType: 'application/json', body: JSON.stringify({ ...session, clarificationDraft: savedDraft }) }))
  await page.route(`**/api/ai/assistant/sessions/${sessionId}/clarification-draft*`, async route => {
    if (route.request().method() === 'DELETE') {
      savedDraft = null
      await route.fulfill({ contentType: 'application/json', body: JSON.stringify({ ...session, version: 2, clarificationDraft: null }) })
      return
    }
    const body = route.request().postDataJSON()
    savedDraft = {
      originTurnId: body.originTurnId,
      originalMessage: body.originalMessage,
      requestedCapabilityId: body.requestedCapabilityId,
      questions: body.questions,
      answers: body.answers,
      updatedAt: new Date().toISOString(),
    }
    await route.fulfill({
      contentType: 'application/json',
      body: JSON.stringify({
        ...session,
        clarificationDraft: savedDraft,
      }),
    })
  })
  await page.route('**/api/ai/assistant/turns', async route => {
    const body = route.request().postDataJSON()
    expect(body.progressiveReply).toBeNull()
    expect(body.progressiveReplies).toEqual([
      { questionId: 'launch.deadline', value: '8_weeks', label: '8 tuần' },
      { questionId: 'launch.team_size', value: '5 people', label: '5 people' },
    ])
    expect(body.requestedCapabilityId).toBe('project.launch.analyze.v1')
    expect(body.expectedVersion).toBe(1)
    await route.fulfill({
      contentType: 'application/json',
      body: JSON.stringify(launchResponse(2, false, '70707070-7070-7070-7070-707070707070', body.clientTurnId)),
    })
  })

  await page.goto('/dashboard', { waitUntil: 'domcontentloaded' })
  await page.getByRole('button', { name: 'Mở Trợ lý AI' }).last().click()
  const assistant = page.getByRole('dialog', { name: 'Trợ lý AI' })
  await expect(assistant).toBeVisible()
  const firstBrief = assistant.getByTestId('project-launch-brief').first()
  await expect(firstBrief).toContainText('Customer Portal SPA')
  await expect(firstBrief).toContainText('Bản 1')
  await expect(firstBrief).toContainText('Quy tắc làm việc của tổ chức')
  await expect(firstBrief).toContainText('Bản 3 đang áp dụng')
  await expect(firstBrief).toContainText('DeepSeek / deepseek-v4-pro')
  await expect(firstBrief).toContainText('Chưa tạo dự án hoặc phân công công việc')
  expect(rendererErrors.filter(message => message.includes('DataCloneError'))).toEqual([])
  await expect(assistant.getByText('Các bước Trợ lý AI đã thực hiện (4)')).toBeVisible()
  await expect(assistant.getByText('Đã kiểm tra schema, nguồn và chính sách')).toBeHidden()
  await expect(assistant.getByText('Cách làm tạm thời')).toHaveCount(0)

  await assistant.getByTestId('assistant-session-history-toolbar').click()
  const historyDrawer = page.getByRole('dialog', { name: 'Lịch sử phiên Trợ lý AI' })
  await expect(historyDrawer).toBeVisible()
  await expect(historyDrawer.getByText('Lịch sử phiên Trợ lý AI', { exact: true }).last()).toBeVisible()
  await expect(historyDrawer.getByText('Khởi chạy web SPA')).toBeVisible()
  await expect(historyDrawer.getByText('Đang mở')).toBeVisible()
  await historyDrawer.getByRole('button', { name: 'Đóng bảng phụ' }).click()

  const deadlineQuestion = assistant.locator('.assistant-progressive-questions article').filter({
    hasText: 'Mốc hoàn thành hoặc timebox mong muốn là khi nào?',
  })
  await deadlineQuestion.getByRole('button', { name: '8 tuần', exact: true }).click()
  const teamAnswer = assistant.getByRole('textbox', { name: 'Trả lời: Quy mô nhóm mong muốn là bao nhiêu?' })
  await teamAnswer.fill('')
  await teamAnswer.pressSequentially('5 people', { delay: 25 })
  await expect(teamAnswer).toBeVisible()
  await expect(teamAnswer).toHaveValue('5 people')
  await page.waitForTimeout(500)
  await expect.poll(() => savedDraft?.answers?.length || 0).toBe(2)

  await page.reload({ waitUntil: 'domcontentloaded' })
  await page.getByRole('button', { name: 'Mở Trợ lý AI' }).last().click()
  const restoredAssistant = page.getByRole('dialog', { name: 'Trợ lý AI' })
  const restoredTeamAnswer = restoredAssistant.getByRole('textbox', { name: 'Trả lời: Quy mô nhóm mong muốn là bao nhiêu?' })
  await expect(restoredTeamAnswer).toHaveValue('5 people')
  await restoredAssistant.getByRole('button', { name: 'Gửi tất cả câu trả lời' }).click()
  const revisedBrief = restoredAssistant.getByTestId('project-launch-brief').last()
  await expect(revisedBrief).toContainText('Bản 2')
  await expect(restoredAssistant.getByText('Đã cập nhật Project Launch Brief theo timebox 8 tuần')).toBeVisible()
  await expect(restoredAssistant.getByText('Quy mô nhóm mong muốn là bao nhiêu?').last()).toBeVisible()
})

test('TEST-AI-NATIVE-RESUME-E2E canceled durable turn can be resumed after reload', async ({ page }) => {
  await login(page)
  const canceledTurnId = '80808080-8080-8080-8080-808080808080'
  const canceledClientTurnId = '90909090-9090-9090-9090-909090909090'
  const now = new Date().toISOString()
  let resumed = false
  const session = () => resumed
    ? {
        sessionId, title: 'Resume launch', status: 'active', version: 2, projectId: null, createdAt: now, updatedAt: now,
        turns: [{
          turnId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', sequence: 2,
          clientTurnId: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', userMessage: 'Tiếp tục lập brief', status: 'completed',
          correlationId: 'resumed', createdAt: now, completedAt: now,
          processEvents: launchResponse(1, true, 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb').processEvents,
          response: launchResponse(1, true, 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb'),
        }],
      }
    : {
        sessionId, title: 'Resume launch', status: 'active', version: 1, projectId: null, createdAt: now, updatedAt: now,
        turns: [{
          turnId: canceledTurnId, sequence: 1, clientTurnId: canceledClientTurnId,
          userMessage: 'Khởi chạy web SPA', status: 'canceled', correlationId: 'canceled', createdAt: now, completedAt: now,
          processEvents: [{ sequence: 1, stage: 'retrieve', status: 'canceled', publicLabel: 'Đã hủy an toàn', stepId: 'retrieve', attempt: 1, startedAt: now, completedAt: now }],
          response: null,
        }],
      }

  await page.route('**/api/ai/assistant/sessions/recent', route => route.fulfill({ contentType: 'application/json', body: JSON.stringify(session()) }))
  await page.route(`**/api/ai/assistant/turns/${canceledTurnId}/resume`, async route => {
    const body = route.request().postDataJSON()
    expect(body.expectedVersion).toBe(1)
    expect(body.clientTurnId).toBeTruthy()
    resumed = true
    await route.fulfill({ contentType: 'application/json', body: JSON.stringify(launchResponse(1, true, 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', body.clientTurnId)) })
  })

  await page.goto('/dashboard', { waitUntil: 'domcontentloaded' })
  await page.getByRole('button', { name: 'Mở Trợ lý AI' }).last().click()
  const assistant = page.getByRole('dialog', { name: 'Trợ lý AI' })
  await expect(assistant).toBeVisible()
  await assistant.getByRole('button', { name: 'Tiếp tục lượt này' }).click()
  await expect(assistant.getByTestId('project-launch-brief')).toBeVisible()
  await expect(assistant.getByText('Mình đã lập Project Launch Brief để bạn xem lại.')).toBeVisible()
})
