import { expect, test, type Page } from '@playwright/test'
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

test('TEST-STAFFING-E2E evidence profile and grounded assignee draft remain human controlled', async ({ page }) => {
  await login(page)
  const dashboardResponse = await page.request.get('/api/dashboard/overview')
  expect(dashboardResponse.ok()).toBeTruthy()
  const dashboard = await dashboardResponse.json()
  const project = dashboard.projects.find((item: any) =>
    item.tasks.some((task: any) => !task.isRestricted) && item.members.length > 0)
  expect(project).toBeTruthy()
  const task = project.tasks.find((item: any) => !item.isRestricted)
  const member = project.members[0]
  const now = new Date().toISOString()

  await page.route(`**/api/tasks/${task.id}/completion-contributors`, route => route.fulfill({
    contentType: 'application/json',
    body: JSON.stringify(envelope({
      taskId: task.id,
      taskStatus: 'Done',
      canManage: true,
      isEligibleForAttribution: true,
      taskRowVersion: task.rowVersion,
      eligibleContributors: [{ userId: member.userId, fullName: member.fullName, email: member.email }],
      attributions: [{
        id: '11111111-1111-1111-1111-111111111111',
        contributorUserId: member.userId,
        contributorName: member.fullName,
        status: 'Confirmed',
        completedAt: now,
        confirmedAt: now,
        confirmedByUserId: member.userId,
        correctionReason: null,
        correctionRequestedAt: null,
        rowVersion: 'AQID',
        canRequestCorrection: true,
      }],
      notice: null,
    })),
  }))

  await page.route(`**/api/ai/tasks/${task.id}/assignment-insight**`, route => route.fulfill({
    contentType: 'application/json',
    body: JSON.stringify(envelope({
      taskId: task.id,
      projectId: project.id,
      taskTitle: task.title,
      taskDescription: task.description,
      taskPriority: task.priority,
      taskStatus: task.status,
      dueDate: task.dueDate,
      recommendedUserId: member.userId,
      recommendedUserName: member.fullName,
      recommendationSummary: `Ưu tiên xem xét ${member.fullName}: phủ 100% kỹ năng yêu cầu, confidence 75%.`,
      generatedAt: now,
      scoringVersion: 'assignee-evidence-score.v1',
      evidenceState: 'ready',
      workloadScope: 'authorized_project_tasks',
      taskRowVersion: task.rowVersion,
      requiredSkills: ['Vue Native UI'],
      candidates: [{
        userId: member.userId,
        fullName: member.fullName,
        role: member.role,
        activeTaskCount: 1,
        overdueTaskCount: 0,
        recentCompletionCount: 1,
        skillMatchScore: 40,
        historyScore: 10,
        workloadScore: 36,
        totalScore: 86,
        skillSignals: ['Vue Native UI'],
        recentSignals: ['1 bằng chứng hoàn thành trong 90 ngày'],
        skillCoveragePercent: 100,
        evidenceConfidence: 0.75,
        evidenceBand: 'practiced',
        missingSkills: [],
        evidenceSourceCount: 1,
        restrictedEvidenceCount: 0,
        evidenceSources: [{
          taskId: task.id,
          taskTitle: task.title,
          taskUrl: `/projects/${project.id}/tasks/${task.id}`,
          completedAt: now,
          matchedSkills: ['Vue Native UI'],
        }],
      }],
    })),
  }))

  await page.goto(`/projects/${project.id}/tasks/${task.id}`, { waitUntil: 'domcontentloaded' })
  const contributorCard = page.getByTestId('task-completion-contributors-card')
  await expect(contributorCard).toBeVisible()
  await expect(contributorCard.getByText(member.fullName).first()).toBeVisible()
  await expect(contributorCard.getByText('Đã xác nhận')).toBeVisible()

  const recommendation = page.getByTestId('evidence-assignee-recommendation')
  await recommendation.getByRole('button', { name: 'Tải gợi ý' }).click()
  await expect(recommendation.getByText('Phủ skill: 100%')).toBeVisible()
  await expect(recommendation.getByText(task.title, { exact: false })).toBeVisible()
  await recommendation.getByRole('button', { name: 'Chọn thủ công' }).click()

  const taskModal = page.locator('.task-modal')
  await expect(taskModal.getByRole('heading', { name: 'Chỉnh sửa nhiệm vụ' })).toBeVisible()
  await expect(taskModal.locator('.form-group').filter({ hasText: 'Người thực hiện' }).locator('select')).toHaveValue(member.userId)
  await expect(taskModal.locator('.form-group').filter({ hasText: 'Mô tả chi tiết' }).locator('textarea')).toHaveValue(task.description ?? '')
  await expect(taskModal.getByRole('button', { name: 'Lưu thay đổi' })).toBeVisible()
  await taskModal.getByRole('button', { name: 'Hủy' }).click()
  await expect(taskModal).toBeHidden()

  const organizationId = '22222222-2222-2222-2222-222222222222'
  await page.route('**/api/organizations?pageSize=100', route => route.fulfill({
    contentType: 'application/json',
    body: JSON.stringify(envelope({ items: [{ id: organizationId, name: 'Evidence Org', code: 'EVID', ownerId: member.userId, memberCount: 1, projectCount: 1, isActive: true }], totalCount: 1 })),
  }))
  await page.route(`**/api/organizations/${organizationId}/users`, route => route.fulfill({
    contentType: 'application/json',
    body: JSON.stringify(envelope([{ userId: member.userId, fullName: member.fullName, email: member.email, role: 'Member', joinedAt: now }])),
  }))
  await page.route(`**/api/organizations/${organizationId}/moderator-capabilities`, route => route.fulfill({
    contentType: 'application/json', body: JSON.stringify(envelope([])),
  }))
  await page.route(`**/api/organizations/${organizationId}/members/${member.userId}/skill-evidence`, route => route.fulfill({
    contentType: 'application/json',
    body: JSON.stringify(envelope({
      organizationId,
      memberId: member.userId,
      memberName: member.fullName,
      isSelf: false,
      canManageEvidence: true,
      evidenceMethodVersion: 'member-skill-evidence.v1',
      pendingCorrectionCount: 0,
      emptyState: '',
      skills: [{
        skillId: '33333333-3333-3333-3333-333333333333',
        skillName: 'Vue Native UI',
        evidenceBand: 'practiced',
        confidence: 0.75,
        verifiedTaskCount: 2,
        restrictedTaskCount: 0,
        mostRecentCompletedAt: now,
        isStale: false,
        sources: [{ attributionId: '44444444-4444-4444-4444-444444444444', taskId: task.id, taskTitle: task.title, taskUrl: `/projects/${project.id}/tasks/${task.id}`, isRestricted: false, completedAt: now, requiredLevel: 'Proficient' }],
      }],
    })),
  }))

  await page.goto(`/organizations/users?organization=${organizationId}`, { waitUntil: 'domcontentloaded' })
  await page.getByRole('button', { name: `Xem bằng chứng kỹ năng ${member.fullName}` }).click()
  const evidenceDialog = page.locator('.evidence-drawer')
  const drawer = page.getByTestId('member-skill-evidence-drawer')
  await expect(drawer).toBeVisible()
  await expect(drawer.getByText('Vue Native UI')).toBeVisible()
  await expect(drawer.getByText('75%')).toBeVisible()
  await expect(drawer.getByText(task.title, { exact: false })).toBeVisible()
  await expect(evidenceDialog.getByText(/không phải điểm hiệu suất/i)).toBeVisible()
})
