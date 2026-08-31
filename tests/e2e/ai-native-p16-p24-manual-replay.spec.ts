import { expect, test, type Page } from '@playwright/test'
import { adminEmail, adminPassword } from './support/credentials'

test.use({ trace: 'on', video: 'on', screenshot: 'on' })
test.describe.configure({ mode: 'serial', timeout: 120_000 })

async function login(page: Page) {
  await page.goto('/Account/Login', { waitUntil: 'domcontentloaded' })
  await page.locator('input[name="Email"]').fill(adminEmail)
  await page.locator('input[name="Password"]').fill(adminPassword)
  await page.locator('button[type="submit"]').click()
  await page.waitForURL(url => !url.pathname.startsWith('/Account/Login'), { timeout: 30_000 })
  await page.locator('.welcome-overlay').waitFor({ state: 'hidden', timeout: 5_000 }).catch(() => undefined)
}

async function openAssistant(page: Page) {
  await page.getByRole('button', { name: 'Mở Trợ lý AI' }).last().click()
  const assistant = page.getByRole('dialog', { name: 'Trợ lý AI' })
  await expect(assistant).toBeVisible()
  return assistant
}

async function startFreshConversation(assistant: ReturnType<Page['getByRole']>) {
  await assistant.getByRole('button', { name: 'Công cụ và tùy chọn Trợ lý AI' }).first().click()
  await assistant.getByRole('menuitem', { name: 'Cuộc trò chuyện mới' }).click()
}

async function sendPrompt(page: Page, assistant: ReturnType<Page['getByRole']>, prompt: string) {
  const responsePromise = page.waitForResponse(response =>
    response.request().method() === 'POST' && /\/api\/ai\/assistant\/turns$/.test(new URL(response.url()).pathname),
  )
  const composer = assistant.getByRole('textbox', { name: /Nhập yêu cầu.*Trợ lý AI/ }).last()
  await composer.fill(prompt)
  await composer.press('Enter')
  const response = await responsePromise
  expect(response.ok(), await response.text()).toBeTruthy()
  return response.json()
}

test('P16-P17 live natural prompts render editable typed review cards instead of prose fallback', async ({ page }) => {
  await login(page)
  const dashboard = await page.evaluate(async () => {
    const response = await fetch('/api/dashboard/overview')
    return response.json()
  })
  const managedProject = dashboard.projects.find((project: any) =>
    project.permissions?.aiTier === 'Full' && project.permissions?.canManageAllTasks && project.tasks?.length)
  expect(managedProject, 'rich seed must expose a managed Project with at least one Task').toBeTruthy()
  const task = managedProject.tasks[0]

  await page.goto(`/projects/${managedProject.id}/tasks/${task.id}`, { waitUntil: 'domcontentloaded' })
  const assistant = await openAssistant(page)
  await startFreshConversation(assistant)

  const p16 = await sendPrompt(
    page,
    assistant,
    'Với Task đang mở, soạn 5 mục acceptance checklist kiểm chứng được, cho phép sửa từng mục và chờ một xác nhận trước khi lưu.',
  )
  expect(p16.disposition).toBe('native_action_draft')
  expect(p16.nativeActionDraft?.capabilityId).toBe('task.acceptance_checklist.v1')
  expect(p16.nativeActionDraft?.payload?.items).toHaveLength(5)
  const p16Card = assistant.getByTestId('native-action-draft').last()
  await expect(p16Card).toContainText('Checklist nghiệm thu')
  await expect(p16Card.locator('.native-action-editor > label')).toHaveCount(5)
  await expect(p16Card.getByTestId('native-action-save')).toBeVisible()
  await expect(p16Card.getByTestId('native-action-confirm')).toBeVisible()

  const p17 = await sendPrompt(
    page,
    assistant,
    'Tách Task đang mở thành 4 subtask theo thứ tự thực hiện, có dependency, estimate và required skill; mở card review trước khi tạo.',
  )
  expect(p17.disposition).toBe('native_action_draft')
  expect(p17.nativeActionDraft?.capabilityId).toBe('task.breakdown.v1')
  expect(p17.nativeActionDraft?.payload?.subtasks).toHaveLength(4)
  const p17Card = assistant.getByTestId('native-action-draft').last()
  await expect(p17Card).toContainText('Tách task thành các subtask')
  await expect(p17Card.locator('.native-action-editor > label')).toHaveCount(4)
  await expect(p17Card).toContainText('Kỹ năng bắt buộc')
  await expect(p17Card.getByTestId('native-action-save')).toBeVisible()
  await expect(p17Card.getByTestId('native-action-confirm')).toBeVisible()
})

const sessionId = '30000000-0000-0000-0000-000000000001'
const projectId = '30000000-0000-0000-0000-000000000002'
const targetId = '30000000-0000-0000-0000-000000000003'

const matrix = [
  {
    capabilityId: 'wiki.brief_task.v1',
    title: 'Brief và task từ Wiki',
    payload: {
      summary: 'Tóm tắt có nguồn để người dùng kiểm tra.',
      sectionRefs: [`/projects/${projectId}/wiki/${targetId}#scope`],
      taskCandidates: [{ clientId: 'wiki-task-1', title: 'Task từ Wiki', description: 'Task có thể bỏ chọn.', selected: false, sourceRef: `/projects/${projectId}/wiki/${targetId}#scope` }],
    },
  },
  {
    capabilityId: 'group.poll.create.v1',
    title: 'Poll của nhóm',
    payload: { question: 'Chọn phương án triển khai?', options: ['A', 'B', 'C', 'D'], allowMultiple: false, expiredAt: '2026-09-02T09:00' },
  },
  {
    capabilityId: 'meeting.actions.review.v1',
    title: 'Quyết định và action item cuộc họp',
    payload: {
      summary: 'Cuộc họp thống nhất bước tiếp theo.', decisions: ['Phát hành theo giai đoạn'], blockers: ['Thiếu QA'],
      actionItems: [{ itemIndex: 0, title: 'Chuẩn bị QA handoff', mappingMode: 'none', existingTaskId: null, description: 'Chuẩn bị dữ liệu QA.', sourceEvidence: 'Đoạn transcript 01' }],
      existingTaskOptions: [{ taskId: targetId, title: 'Task hiện có' }],
    },
  },
  {
    capabilityId: 'project.roadmap.adjust.v1',
    title: 'Điều chỉnh Roadmap/Sprint',
    payload: { summary: 'Dời Sprint có kiểm soát.', adjustments: [{ sprintId: targetId, sprintName: 'Sprint 2', beforeStart: '2026-09-01', beforeEnd: '2026-09-14', afterStart: '2026-09-08', afterEnd: '2026-09-21', reason: 'Dependency chưa hoàn tất', selected: false }] },
  },
  {
    capabilityId: 'project.digest.configure.v1',
    title: 'Lịch gửi tổng hợp dự án',
    payload: { isEnabled: true, dayOfWeek: 1, localTimeMinutes: 540, timeZoneId: 'Asia/Ho_Chi_Minh' },
  },
  {
    capabilityId: 'task.skill_evidence.confirm.v1',
    title: 'Đóng góp và bằng chứng kỹ năng',
    payload: { acceptanceEvidence: ['Checklist đã hoàn tất'], skills: [{ skillId: targetId, name: 'QA Automation', requiredLevel: 'Proficient' }], contributors: [{ userId: targetId, name: 'Thành viên kiểm thử', selected: false }] },
  },
  {
    capabilityId: 'project.roadmap.adjust.v1',
    title: 'Điều chỉnh Roadmap/Sprint',
    payload: { summary: 'Phương án replan có thể bỏ chọn.', adjustments: [{ sprintId: targetId, sprintName: 'Sprint rủi ro', beforeStart: '2026-09-01', beforeEnd: '2026-09-14', afterStart: '2026-09-15', afterEnd: '2026-09-28', reason: 'Giảm tải đa dự án', selected: false }] },
  },
]

function matrixSession() {
  const now = new Date().toISOString()
  return {
    sessionId,
    title: 'P18-P24 structured renderer matrix',
    status: 'active',
    version: matrix.length,
    projectId,
    createdAt: now,
    updatedAt: now,
    turns: matrix.map((item, index) => ({
      turnId: `31000000-0000-0000-0000-${String(index + 1).padStart(12, '0')}`,
      sequence: index + 1,
      clientTurnId: `32000000-0000-0000-0000-${String(index + 1).padStart(12, '0')}`,
      userMessage: `P${index + 18}`,
      status: 'completed', correlationId: `p${index + 18}-renderer`, createdAt: now, completedAt: now, processEvents: [],
      response: {
        schemaId: 'assistant_turn.v1', disposition: 'native_action_draft', intent: item.capabilityId,
        executionPolicy: 'explicit_batch_confirm', assistantMessage: `Review ${item.title}.`, confidence: 0.95,
        clarification: null, artifact: null, sourceRefs: [`/projects/${projectId}`], answer: null,
        sessionId,
        turnId: `31000000-0000-0000-0000-${String(index + 1).padStart(12, '0')}`,
        clientTurnId: `32000000-0000-0000-0000-${String(index + 1).padStart(12, '0')}`,
        sequence: index + 1, sessionVersion: index + 1, turnStatus: 'completed', correlationId: `p${index + 18}-renderer`, replayed: false,
        processEvents: [], capabilities: [], sourceDisclosures: [], conversation: null, actualProvider: 'Qaly', actualModel: 'qaly-native',
        nativeActionDraft: {
          draftId: `33000000-0000-0000-0000-${String(index + 1).padStart(12, '0')}`,
          capabilityId: item.capabilityId, schemaId: `${item.capabilityId}.draft`, rendererId: 'native-action-review.v1',
          targetType: 'project', targetId, projectId, status: 'pending_review', revision: 1, rowVersion: `row-${index + 1}`,
          payload: item.payload, sourceVersion: `source-${index + 1}`, expiresAt: new Date(Date.now() + 86_400_000).toISOString(), createdAt: now, receipt: null,
        },
      },
    })),
  }
}

test('P18-P24 each use a structured review card with editable/selectable controls', async ({ page }) => {
  await login(page)
  const session = matrixSession()
  await page.route('**/api/ai/assistant/sessions/recent', route => route.fulfill({ contentType: 'application/json', body: JSON.stringify(session) }))
  await page.route(`**/api/ai/assistant/sessions/${sessionId}`, route => route.fulfill({ contentType: 'application/json', body: JSON.stringify(session) }))
  await page.evaluate(() => window.localStorage.setItem('qaly.ai-native.active-session.v1', '30000000-0000-0000-0000-000000000001'))
  await page.goto('/dashboard', { waitUntil: 'domcontentloaded' })
  const assistant = await openAssistant(page)
  const cards = assistant.getByTestId('native-action-draft')
  await expect(cards).toHaveCount(7)
  for (const item of matrix) await expect(assistant.getByRole('heading', { name: item.title }).first()).toBeVisible()
  await expect(cards.nth(0).getByRole('textbox', { name: 'Tóm tắt có nguồn' })).toBeEditable()
  await expect(cards.nth(1).locator('input')).not.toHaveCount(0)
  await expect(cards.nth(2).locator('select')).not.toHaveCount(0)
  await expect(cards.nth(3).getByText('Áp dụng điều chỉnh này')).toBeVisible()
  await expect(cards.nth(4).locator('select')).not.toHaveCount(0)
  await expect(cards.nth(5).getByText('Thành viên kiểm thử')).toBeVisible()
  await expect(cards.nth(6).getByTestId('native-action-confirm')).toBeVisible()
})
