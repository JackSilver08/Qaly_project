import { expect, test, type Page, type Route } from '@playwright/test'
import { adminEmail, adminPassword } from './support/credentials'

test.describe.configure({ mode: 'serial' })
test.setTimeout(90_000)

const projectId = '71111111-1111-4111-8111-111111111111'
const taskId = '72222222-2222-4222-8222-222222222222'
const organizationId = '73333333-3333-4333-8333-333333333333'
const userId = '74444444-4444-4444-8444-444444444444'
const frontendSkillId = '75555555-5555-4555-8555-555555555555'
const backendSkillId = '76666666-6666-4666-8666-666666666666'
const jobId = '77777777-7777-4777-8777-777777777777'
const draftId = '78888888-8888-4888-8888-888888888888'

type Requirement = {
  id: string
  skillId: string
  name: string
  description: string | null
  requiredLevel: 'Familiar' | 'Proficient' | 'Expert'
  provenance: 'MANUAL' | 'AI_CONFIRMED'
  confirmedByUserId: string
  confirmedAt: string
  rowVersion: string
}

type MockState = {
  mode: 'idle' | 'queued' | 'running' | 'succeeded'
  detailCalls: number
  aiCreateBodies: Record<string, unknown>[]
  aiCreateHeaders: Record<string, string>[]
  manualBodies: Record<string, unknown>[]
  patchBodies: Record<string, unknown>[]
  confirmBodies: Record<string, unknown>[]
  confirmHeaders: Record<string, string>[]
  requirements: Requirement[]
  draftStatus: 'PendingReview' | 'Confirmed' | 'Rejected'
}

const catalog = [
  {
    id: frontendSkillId,
    organizationId,
    name: 'Vue.js',
    normalizedName: 'vue.js',
    description: 'Frontend component engineering',
    isActive: true,
    rowVersion: 'AQID',
  },
  {
    id: backendSkillId,
    organizationId,
    name: 'ASP.NET Core',
    normalizedName: 'asp.net core',
    description: 'Backend API engineering',
    isActive: true,
    rowVersion: 'BAUG',
  },
]

function envelope(data: unknown) {
  return JSON.stringify({ data, error: null, errorCode: null, isSuccess: true, statusCode: 200 })
}

async function fulfill(route: Route, data: unknown, status = 200) {
  await route.fulfill({ status, contentType: 'application/json', body: envelope(data) })
}

async function login(page: Page, returnUrl: string) {
  await page.goto(`/Account/Login?returnUrl=${encodeURIComponent(returnUrl)}`, {
    waitUntil: 'domcontentloaded',
  })
  await page.locator('input[name="Email"]').fill(adminEmail)
  await page.locator('input[name="Password"]').fill(adminPassword)
  await page.locator('#loginForm button[type="submit"]').click()
  await expect(page.locator('.shell-header')).toBeVisible({ timeout: 45_000 })
  await page.locator('.welcome-overlay').waitFor({ state: 'hidden', timeout: 5_000 }).catch(() => undefined)
}

function dashboard() {
  const task = {
    id: taskId,
    key: 'SKL-1',
    title: 'Build native task skill review',
    description: 'Implement a Vue component and ASP.NET Core API.',
    status: 'Todo',
    priority: 'High',
    dueDate: '2026-08-01T10:00:00Z',
    sprintId: null,
    assigneeId: userId,
    assigneeName: 'E2E Admin',
    reporterName: 'E2E Admin',
    projectName: 'Task Skill Project',
    sortOrder: 0,
    rowVersion: 'AQID',
    isPrivate: false,
    isRestricted: false,
    isPinned: false,
    contributesToProgress: true,
    upvoteCount: 0,
    downvoteCount: 0,
    commentCount: 0,
    attachmentCount: 0,
  }
  return {
    generatedAt: '2026-07-27T10:00:00Z',
    stats: {
      activeProjects: 1,
      totalTasks: 1,
      overdueTasks: 0,
      teamMembers: 1,
      completedTasks: 0,
      completionRate: 0,
      tasksAtRisk: 0,
    },
    summary: '',
    riskDigest: '',
    projects: [
      {
        id: projectId,
        name: 'Task Skill Project',
        code: 'SKL',
        description: 'Native task skills E2E',
        logoUrl: null,
        status: 'Active',
        organizationId,
        ownerId: userId,
        ownerName: 'E2E Admin',
        memberCount: 1,
        taskCount: 1,
        completedTaskCount: 0,
        overdueTaskCount: 0,
        progressPercentage: 0,
        members: [
          {
            userId,
            fullName: 'E2E Admin',
            role: 'Owner',
            email: adminEmail,
            canViewProjectTimeline: true,
            canViewTaskRisk: true,
            canNudgeAssignee: true,
            canViewUnseenTaskSignal: true,
          },
        ],
        tasks: [task],
        createdAt: '2026-07-01T10:00:00Z',
        endDate: '2026-08-10T10:00:00Z',
        enableOnHold: true,
        enableInReview: true,
        requireEvidenceToDone: false,
        restrictTransitionsToAdmin: false,
      },
    ],
    team: [],
    notifications: [],
  }
}

function taskSkills(state: MockState) {
  return {
    taskId,
    projectId,
    organizationId,
    availability: 'ready',
    canManage: true,
    canManageCatalog: true,
    taskRowVersion: 'AQID',
    requirements: state.requirements,
  }
}

function job(status: string) {
  return {
    jobId,
    jobType: 'task_skill_suggestion',
    projectId,
    tenantId: organizationId,
    requestedById: userId,
    status,
    progressPercent: status === 'succeeded' ? 100 : status === 'running' ? 55 : 0,
    attemptCount: status === 'queued' ? 0 : 1,
    maxAttempts: 3,
    createdAt: '2026-07-27T10:00:00Z',
    availableAt: '2026-07-27T10:00:00Z',
    lastErrorCode: null,
    lastErrorMessage: null,
    lastErrorRetryable: false,
    cacheHit: false,
    selectedProvider: status === 'succeeded' ? 'e2e-provider' : null,
    selectedModel: status === 'succeeded' ? 'e2e-model' : null,
    mockReason: null,
    isMock: false,
    draftIds: status === 'succeeded' ? [draftId] : [],
    scopeSourceType: 'task',
    scopeSourceEntityId: taskId,
    rowVersion: 'AQID',
  }
}

function suggestionOutput() {
  return {
    schemaId: 'task_skill_suggestion.v1',
    taskId,
    sourceVersion: 'source-v1',
    dataState: 'ready',
    suggestions: [
      {
        skillId: frontendSkillId,
        canonicalName: 'Vue.js',
        requiredLevel: 'Proficient',
        confidence: 0.91,
        rationale: 'Task yêu cầu xây dựng component Vue ở đúng ngữ cảnh.',
        sourceRefs: [`task:${taskId}`],
      },
      {
        skillId: backendSkillId,
        canonicalName: 'ASP.NET Core',
        requiredLevel: 'Expert',
        confidence: 0.86,
        rationale: 'Task yêu cầu API backend có authorization.',
        sourceRefs: [`task:${taskId}`],
      },
    ],
    unmappedTerms: [],
    generatedAt: '2026-07-27T10:02:00Z',
  }
}

function draft(state: MockState) {
  return {
    draftId,
    aiJobId: jobId,
    projectId,
    draftType: 'TaskSkillSuggestion',
    status: state.draftStatus,
    originalPayload: suggestionOutput(),
    workingPayload: suggestionOutput(),
    warnings: null,
    schemaId: 'task_skill_suggestion.v1',
    confidence: null,
    sources: [],
    rowVersion: 'AQID',
  }
}

async function installMocks(page: Page, featureDisabled = false) {
  const state: MockState = {
    mode: 'idle',
    detailCalls: 0,
    aiCreateBodies: [],
    aiCreateHeaders: [],
    manualBodies: [],
    patchBodies: [],
    confirmBodies: [],
    confirmHeaders: [],
    requirements: [],
    draftStatus: 'PendingReview',
  }

  await page.route('**/api/security/csrf', route =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ token: 'task-skill-e2e-csrf' }),
    }),
  )
  const user = {
    id: userId,
    fullName: 'E2E Admin',
    email: adminEmail,
    role: 'Admin',
    isActive: true,
    avatarUrl: null,
    createdAt: '2026-07-01T00:00:00Z',
  }
  await page.route('**/api/auth/me', route => fulfill(route, user))
  await page.route('**/api/users', route => fulfill(route, [user]))
  await page.route('**/api/notifications', route => fulfill(route, []))
  await page.route('**/api/dashboard/overview', route =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(dashboard()),
    }),
  )
  await page.route(`**/api/comments/task/${taskId}`, route => fulfill(route, []))
  await page.route(`**/api/attachments/task/${taskId}`, route => fulfill(route, []))
  await page.route(`**/api/tasks/${taskId}/time-entries`, route => fulfill(route, []))

  await page.route(`**/api/tasks/${taskId}/skills`, async route => {
    if (route.request().method() === 'PUT') {
      const body = route.request().postDataJSON() as {
        taskRowVersion: string
        skills: Array<{ skillId: string; requiredLevel: Requirement['requiredLevel'] }>
      }
      state.manualBodies.push(body as unknown as Record<string, unknown>)
      state.requirements = body.skills.map((item, index) => {
        const skill = catalog.find(entry => entry.id === item.skillId)!
        return {
          id: `79999999-9999-4999-8999-99999999999${index}`,
          skillId: item.skillId,
          name: skill.name,
          description: skill.description,
          requiredLevel: item.requiredLevel,
          provenance: 'MANUAL',
          confirmedByUserId: userId,
          confirmedAt: '2026-07-27T10:03:00Z',
          rowVersion: 'AQID',
        }
      })
      await fulfill(route, taskSkills(state))
      return
    }
    await fulfill(route, taskSkills(state))
  })
  await page.route(`**/api/organizations/${organizationId}/skills`, route => fulfill(route, catalog))

  await page.route(/\/api\/ai(?:\/|$)/, async route => {
    const request = route.request()
    const path = new URL(request.url()).pathname
    const method = request.method()

    if (method === 'GET' && path === '/api/ai/jobs') {
      await fulfill(route, state.mode === 'idle' ? [] : [job(state.mode)])
      return
    }
    if (method === 'POST' && path === `/api/ai/tasks/${taskId}/skill-suggestions`) {
      state.aiCreateBodies.push(request.postDataJSON() as Record<string, unknown>)
      state.aiCreateHeaders.push(request.headers())
      if (featureDisabled) {
        await route.fulfill({
          status: 503,
          contentType: 'application/json',
          body: JSON.stringify({
            data: null,
            error: 'Task skill suggestions are disabled.',
            errorCode: 'AI_PLATFORM_DISABLED',
            isSuccess: false,
            statusCode: 503,
          }),
        })
        return
      }
      state.mode = 'queued'
      state.detailCalls = 0
      await fulfill(route, { jobId, status: 'queued' }, 202)
      return
    }
    if (method === 'GET' && path === `/api/ai/jobs/${jobId}`) {
      state.detailCalls += 1
      if (state.mode === 'queued' && state.detailCalls === 2) state.mode = 'running'
      else if (state.mode === 'running' && state.detailCalls >= 3) state.mode = 'succeeded'
      await fulfill(route, job(state.mode))
      return
    }
    if (method === 'GET' && path === `/api/ai/jobs/${jobId}/result`) {
      await fulfill(route, {
        jobId,
        schemaId: 'task_skill_suggestion.v1',
        schemaVersion: '1.0',
        result: suggestionOutput(),
        draftIds: [draftId],
        cacheHit: false,
        isMock: false,
        mockReason: null,
        sourceStale: false,
      })
      return
    }
    if (method === 'GET' && path === `/api/ai/drafts/${draftId}`) {
      await fulfill(route, draft(state))
      return
    }
    if (method === 'PATCH' && path === `/api/ai/drafts/${draftId}`) {
      state.patchBodies.push(request.postDataJSON() as Record<string, unknown>)
      await fulfill(route, draft(state))
      return
    }
    if (method === 'POST' && path === `/api/ai/drafts/${draftId}/confirm`) {
      const body = request.postDataJSON() as {
        editedPayloadJson: string
        confirmAction: string
      }
      state.confirmBodies.push(body as unknown as Record<string, unknown>)
      state.confirmHeaders.push(request.headers())
      const edited = JSON.parse(body.editedPayloadJson) as ReturnType<typeof suggestionOutput>
      state.requirements = edited.suggestions.map((item, index) => ({
        id: `7aaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa${index}`,
        skillId: item.skillId,
        name: item.canonicalName,
        description: catalog.find(skill => skill.id === item.skillId)?.description ?? null,
        requiredLevel: item.requiredLevel,
        provenance: 'AI_CONFIRMED',
        confirmedByUserId: userId,
        confirmedAt: '2026-07-27T10:04:00Z',
        rowVersion: 'AQID',
      }))
      state.draftStatus = 'Confirmed'
      await fulfill(route, {
        draftId,
        status: 'Confirmed',
        confirmAction: 'apply_task_skills',
        createdTaskCount: 0,
        createdTaskIds: [],
        appliedSkillCount: edited.suggestions.length,
      })
      return
    }

    await route.fulfill({
      status: 404,
      contentType: 'application/json',
      body: JSON.stringify({ error: `Unhandled task skill E2E route: ${method} ${path}` }),
    })
  })

  return state
}

test('task detail creates a canonical draft, supports selective review, confirms, and restores', async ({ page }) => {
  const state = await installMocks(page)
  await login(page, `/projects/${projectId}/tasks/${taskId}`)

  const card = page.getByTestId('task-skills-ai-card')
  await expect(card).toBeVisible()
  await expect(card.getByTestId('task-skills-empty')).toBeVisible()
  await card.getByTestId('generate-task-skill-ai').dblclick()

  await expect.poll(() => state.aiCreateBodies.length).toBe(1)
  expect(state.aiCreateBodies[0]).toEqual({
    language: 'vi',
    providerHint: 'deepseek',
    maximumEstimatedCostUsd: 0.15,
    cacheMode: 'use',
  })
  expect(state.aiCreateHeaders[0]['idempotency-key']).toBeTruthy()
  expect(state.aiCreateHeaders[0]['x-csrf-token']).toBe('task-skill-e2e-csrf')

  const review = card.getByTestId('task-skill-draft-review')
  await expect(review).toBeVisible({ timeout: 12_000 })
  const backendSuggestion = review.locator('.suggestion').filter({ hasText: 'ASP.NET Core' })
  await backendSuggestion.getByRole('checkbox').uncheck()
  const frontendSuggestion = review.locator('.suggestion').filter({ hasText: 'Vue.js' })
  await expect(frontendSuggestion.getByRole('link')).toHaveAttribute('href', `/projects/${projectId}`)
  await frontendSuggestion.locator('select').selectOption('Familiar')
  await review.getByTestId('confirm-task-skill-draft').click()

  await expect.poll(() => state.confirmBodies.length).toBe(1)
  expect(state.patchBodies).toHaveLength(1)
  const patchPayload = JSON.parse(String(state.patchBodies[0].workingPayloadJson))
  expect(patchPayload.suggestions).toHaveLength(1)
  expect(patchPayload.suggestions[0]).toMatchObject({
    skillId: frontendSkillId,
    requiredLevel: 'Familiar',
  })
  expect(state.confirmBodies[0].confirmAction).toBe('apply_task_skills')
  expect(state.confirmHeaders[0]['idempotency-key']).toBeTruthy()
  expect(state.confirmHeaders[0]['x-csrf-token']).toBe('task-skill-e2e-csrf')
  await expect(card).toContainText('Đã áp dụng 1 kỹ năng')
  await expect(card.getByTestId('confirmed-task-skills')).toContainText('Vue.js')
  await expect(card.getByTestId('confirmed-task-skills')).toContainText('AI đã duyệt')

  await page.reload({ waitUntil: 'domcontentloaded' })
  const restored = page.getByTestId('task-skills-ai-card')
  await expect(restored).toContainText('Đã áp dụng')
  await expect(restored.getByTestId('confirmed-task-skills')).toContainText('Vue.js')
  expect(state.aiCreateBodies).toHaveLength(1)
})

test('feature-off state is honest and manual task tagging remains usable', async ({ page }) => {
  const state = await installMocks(page, true)
  await login(page, `/projects/${projectId}/tasks/${taskId}`)

  const card = page.getByTestId('task-skills-ai-card')
  await card.getByTestId('generate-task-skill-ai').click()
  await expect(card.getByTestId('task-skill-error')).toContainText('Gắn kỹ năng thủ công vẫn hoạt động')
  await expect(card).toContainText('luồng gắn thủ công phía trên vẫn dùng được')

  await card.locator('details.manual-editor summary').click()
  await card.getByLabel('Chọn kỹ năng').selectOption(frontendSkillId)
  await card.getByRole('button', { name: 'Thêm' }).click()
  await card.getByTestId('save-manual-task-skills').click()

  await expect.poll(() => state.manualBodies.length).toBe(1)
  expect(state.manualBodies[0]).toEqual({
    taskRowVersion: 'AQID',
    skills: [{ skillId: frontendSkillId, requiredLevel: 'Proficient' }],
  })
  await expect(card.getByTestId('confirmed-task-skills')).toContainText('Vue.js')
  await expect(card.getByTestId('confirmed-task-skills')).toContainText('Thủ công')
})
