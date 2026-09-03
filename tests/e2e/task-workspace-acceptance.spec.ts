import { expect, test, type Page } from '@playwright/test'
import { adminEmail, adminPassword } from './support/credentials'

test.describe.configure({ mode: 'serial' })
test.setTimeout(90_000)

async function login(page: Page) {
  await page.goto('/Account/Login', { waitUntil: 'domcontentloaded' })
  await page.locator('input[name="Email"]').fill(adminEmail)
  await page.locator('input[name="Password"]').fill(adminPassword)
  await page.getByRole('button', { name: /Dang nhap|Đăng nhập|Login/i }).click()
  await page.waitForURL((url) => !url.pathname.startsWith('/Account/Login'))
  await page.locator('.welcome-overlay').waitFor({ state: 'hidden', timeout: 5_000 }).catch(() => undefined)
}

function tomorrowIsoDate() {
  const value = new Date()
  value.setDate(value.getDate() + 1)
  return value.toISOString().slice(0, 10)
}

test('task create, edit, workflow, filters, attachment and history survive reload', async ({ page }) => {
  const runtimeErrors: string[] = []
  page.on('pageerror', (error) => runtimeErrors.push(error.message))

  await login(page)
  await page.goto('/projects', { waitUntil: 'domcontentloaded' })
  const projectCard = page.locator('.project-grid-card').first()
  await expect(projectCard).toBeVisible()
  await projectCard.locator('.project-grid-card__open-surface').click()
  await page.waitForURL((url) => /^\/projects\/[0-9a-f-]{36}$/i.test(url.pathname))
  const projectId = new URL(page.url()).pathname.split('/')[2]

  await page.goto(`/projects/${projectId}?tab=tasks`, { waitUntil: 'domcontentloaded' })
  await expect(page.locator('#tasks')).toBeVisible()

  const suffix = `${Date.now()}`
  const originalTitle = `QB6 Task ${suffix}`
  const updatedTitle = `${originalTitle} Updated`
  const historyNote = `QB6 read-back ${suffix}`

  await page.locator('.board-actions').getByRole('button', { name: 'Nhiệm vụ', exact: true }).click()
  const createDialog = page.getByRole('dialog', { name: 'Tạo nhiệm vụ mới' })
  await createDialog.getByLabel('Tiêu đề nhiệm vụ').fill(originalTitle)
  await createDialog.getByLabel('Mô tả chi tiết nhiệm vụ').fill('Kiểm tra toàn bộ workspace nhiệm vụ bằng UI thật.')
  await createDialog.getByLabel('Độ ưu tiên nhiệm vụ').selectOption('High')
  await createDialog.getByLabel('Hạn chót nhiệm vụ').fill(tomorrowIsoDate())
  const createResponse = page.waitForResponse((response) =>
    response.request().method() === 'POST' && /\/api\/tasks(?:\?|$)/.test(response.url()),
  )
  await createDialog.getByRole('button', { name: 'Tạo nhiệm vụ', exact: true }).click()
  expect((await createResponse).ok()).toBe(true)

  const createdCard = page.locator('.kanban-card').filter({ hasText: originalTitle })
  await expect(createdCard).toHaveCount(1)
  const taskId = await createdCard.getAttribute('data-id')
  expect(taskId).toMatch(/^[0-9a-f-]{36}$/i)

  await page.reload({ waitUntil: 'domcontentloaded' })
  const reloadedCard = page.locator('.kanban-card').filter({ hasText: originalTitle })
  await expect(reloadedCard).toHaveCount(1)

  const inProgressColumn = page.locator('.kanban-column[data-kanban-status="InProgress"] .kanban-column__list')
  await expect(inProgressColumn).toBeVisible()
  const moveResponse = page.waitForResponse((response) =>
    response.request().method() === 'PATCH' && response.url().includes(`/api/tasks/project/${projectId}/kanban/move`),
  )
  await reloadedCard.dragTo(inProgressColumn)
  expect((await moveResponse).ok()).toBe(true)

  await page.reload({ waitUntil: 'domcontentloaded' })
  await page.getByRole('button', { name: 'List', exact: true }).click()
  let taskRow = page.locator('.task-list-row').filter({ hasText: originalTitle })
  await expect(taskRow.locator('.task-status-select')).toHaveValue('InProgress')

  await taskRow.getByRole('button', { name: `Mở thao tác cho nhiệm vụ ${originalTitle}` }).click()
  await taskRow.getByRole('button', { name: 'Sửa', exact: true }).click()
  const editDialog = page.getByRole('dialog', { name: 'Chỉnh sửa nhiệm vụ' })
  await editDialog.getByLabel('Tiêu đề nhiệm vụ').fill(updatedTitle)
  const editResponse = page.waitForResponse((response) =>
    response.request().method() === 'PUT' && response.url().includes(`/api/tasks/${taskId}`),
  )
  await editDialog.getByRole('button', { name: 'Lưu thay đổi' }).click()
  expect((await editResponse).ok()).toBe(true)
  taskRow = page.locator('.task-list-row').filter({ hasText: updatedTitle })
  await expect(taskRow).toHaveCount(1)

  await taskRow.click()
  let drawer = page.getByRole('dialog', { name: 'Chi tiết nhiệm vụ' })
  const uploadResponse = page.waitForResponse((response) =>
    response.request().method() === 'POST' && response.url().includes(`/api/attachments/task/${taskId}`),
  )
  await drawer.locator('.attachment-section input[type="file"]').setInputFiles({
    name: 'qb6-evidence.txt',
    mimeType: 'text/plain',
    buffer: Buffer.from('Qaly canonical task evidence'),
  })
  expect((await uploadResponse).ok()).toBe(true)
  const attachment = drawer.locator('.attachment-card').filter({ hasText: 'qb6-evidence.txt' })
  await expect(attachment).toHaveCount(1)
  const evidenceResponse = page.waitForResponse((response) =>
    response.request().method() === 'PATCH' && /\/api\/attachments\/[0-9a-f-]{36}\/evidence(?:\?|$)/i.test(response.url()),
  )
  await attachment.locator('.evidence-toggle').click()
  expect((await evidenceResponse).ok()).toBe(true)
  await expect(attachment.locator('.evidence-toggle input[type="checkbox"]')).toBeChecked()

  await drawer.getByRole('button', { name: 'Nhập tay' }).click()
  await drawer.getByLabel('Số phút làm việc thủ công').fill('15')
  await drawer.getByLabel('Ghi chú thời gian làm việc').fill(historyNote)
  const timeResponse = page.waitForResponse((response) =>
    response.request().method() === 'POST' && response.url().includes(`/api/tasks/${taskId}/time-entries/manual`),
  )
  await drawer.getByRole('button', { name: 'Ghi nhận' }).click()
  expect((await timeResponse).ok()).toBe(true)
  await expect(drawer.locator('.activity-timeline')).toContainText(historyNote)

  await page.reload({ waitUntil: 'domcontentloaded' })
  drawer = page.getByRole('dialog', { name: 'Chi tiết nhiệm vụ' })
  await expect(drawer.locator('.attachment-card').filter({ hasText: 'qb6-evidence.txt' })).toHaveCount(1)
  await expect(drawer.locator('.evidence-toggle input[type="checkbox"]')).toBeChecked()
  await expect(drawer.locator('.activity-timeline')).toContainText(historyNote)

  await page.goto('/tasks', { waitUntil: 'domcontentloaded' })
  await page.locator('.filter-grid input[type="search"]').fill(updatedTitle)
  const filters = page.locator('.filter-grid select')
  await filters.nth(0).selectOption(projectId)
  await filters.nth(1).selectOption('InProgress')
  await filters.nth(4).selectOption('High')
  const filteredCard = page.locator('.task-card').filter({ hasText: updatedTitle })
  await expect(filteredCard).toHaveCount(1)
  await expect(page.locator('.task-card')).toHaveCount(1)

  const canonical = await page.evaluate(async (id) => {
    const response = await fetch(`/api/tasks/${id}`)
    if (!response.ok) return null
    const payload = await response.json()
    return payload.data ?? payload
  }, taskId)
  expect(canonical).toMatchObject({
    id: taskId,
    projectId,
    title: updatedTitle,
    status: 'InProgress',
    priority: 'High',
  })
  expect(runtimeErrors).toEqual([])
})
