import { expect, test, type Browser, type BrowserContext, type Page } from '@playwright/test'
import { browserApiRequest, type BrowserApiResponse } from './support/browser-api'
import { adminEmail, adminPassword, memberEmail, memberPassword } from './support/credentials'

test.setTimeout(90_000)

type ApiResult<T> = {
  isSuccess: boolean
  data: T | null
  error: string | null
}

type Entity = { id: string; name: string }

async function login(page: Page, email: string, password: string) {
  await page.goto('/Account/Login', { waitUntil: 'domcontentloaded' })
  await page.locator('input[name="Email"]').fill(email)
  await page.locator('input[name="Password"]').fill(password)
  await page.getByRole('button', { name: /Đăng nhập|Login/i }).click()
  await page.waitForURL(url => !url.pathname.startsWith('/Account/Login'), { timeout: 20_000 })
  await expect(page.locator('.shell-header')).toBeVisible()
}

async function apiResult<T>(response: BrowserApiResponse): Promise<T> {
  const body = (await response.json()) as ApiResult<T> | T
  expect(response.ok(), `${response.url()} returned ${response.status()}`).toBeTruthy()
  if (body && typeof body === 'object' && 'isSuccess' in body) {
    const result = body as ApiResult<T>
    expect(result.isSuccess, result.error ?? 'API result failed').toBeTruthy()
    expect(result.data).not.toBeNull()
    return result.data as T
  }

  return body as T
}

function unique(prefix: string) {
  return `${prefix} ${Date.now()} ${Math.random().toString(36).slice(2, 8)}`
}

async function createMemberContext(browser: Browser) {
  const context = await browser.newContext({
    baseURL: process.env.E2E_BASE_URL ?? 'http://127.0.0.1:5000',
  })
  const page = await context.newPage()
  await login(page, memberEmail, memberPassword)
  return { context, page }
}

test('organization overview supports project, group, member and unauthorized-account workflow', async ({
  browser,
  page,
}) => {
  let organizationId = ''
  let directProjectId = ''
  let groupProjectId = ''
  let groupId = ''
  let memberContext: BrowserContext | undefined

  await login(page, adminEmail, adminPassword)

  try {
    const organizationName = unique('Organization overview E2E')
    const organization = await apiResult<Entity>(
      await browserApiRequest(page, 'POST', '/api/organizations', {
        data: {
          name: organizationName,
          code: null,
          description: 'Organization overview end-to-end verification',
          ownerId: null,
        },
      }),
    )
    organizationId = organization.id

    await page.goto('/organizations')
    await page.getByRole('button', { name: `Mở tổng quan ${organizationName}` }).click()
    await expect(page).toHaveURL(new RegExp(`/organizations/${organizationId}$`))
    await expect(page.getByRole('heading', { name: organizationName })).toBeVisible()
    await expect(page.getByText('Project trong organization')).toBeVisible()
    await expect(page.getByText('Group thuộc organization')).toBeVisible()

    const consolidatedTabs: Array<[string, string]> = [
      ['Skills', 'organization-skills-tab'],
      ['Professional Profiles', 'organization-professional-profiles-tab'],
      ['Capacity', 'organization-capacity-tab'],
      ['AI Budget', 'ai-usage-budget-settings'],
      ['Work Rulebook', 'organization-rulebook-tab'],
      ['Activity', 'organization-activity-tab'],
    ]
    for (const [tabName, testId] of consolidatedTabs) {
      await page.getByRole('button', { name: tabName, exact: true }).click()
      await expect(page.getByTestId(testId)).toBeVisible()
    }
    await page.getByRole('button', { name: 'Tổng quan', exact: true }).click()

    const groupName = unique('Delivery group')
    await page.getByTestId('create-organization-group').click()
    const groupDialog = page.getByRole('dialog', { name: 'Thêm group' })
    await groupDialog.getByLabel('Tên group').fill(groupName)
    await groupDialog.getByTestId('submit-organization-group').click()
    const groupRow = page.locator('.group-row').filter({ hasText: groupName })
    await expect(groupRow).toContainText('Chưa liên kết project')
    groupId = (await apiResult<{ items: Array<Entity & { organizationId?: string }> }>(
      await browserApiRequest(page, 'GET', `/api/groups?pageSize=100&organizationId=${organizationId}`),
    )).items.find(group => group.name === groupName)?.id ?? ''
    expect(groupId).not.toBe('')

    const directProjectName = unique('Direct organization project')
    await page.getByTestId('create-organization-project').click()
    const directProjectDialog = page.getByRole('dialog', { name: 'Tạo project mới' })
    await directProjectDialog.getByLabel('Tên project').fill(directProjectName)
    await directProjectDialog.getByLabel('Mô tả').fill('Created directly from organization overview')
    await directProjectDialog.getByTestId('submit-organization-project').click()
    await expect(page.getByRole('heading', { name: directProjectName })).toBeVisible()
    directProjectId = page.url().match(/\/projects\/([0-9a-f-]+)/i)?.[1] ?? ''
    expect(directProjectId).not.toBe('')

    await page.goto(`/organizations/${organizationId}`)
    await expect(page.getByRole('heading', { name: organizationName })).toBeVisible()
    const refreshedGroupRow = page.locator('.group-row').filter({ hasText: groupName })
    await refreshedGroupRow.getByRole('button', { name: 'Tạo project từ group' }).click()
    const groupProjectName = unique('Group linked project')
    const groupProjectDialog = page.getByRole('dialog', { name: 'Tạo project mới' })
    await groupProjectDialog.getByLabel('Tên project').fill(groupProjectName)
    await groupProjectDialog.getByTestId('submit-organization-project').click()
    await expect(page.getByRole('heading', { name: groupProjectName })).toBeVisible()
    groupProjectId = page.url().match(/\/projects\/([0-9a-f-]+)/i)?.[1] ?? ''
    expect(groupProjectId).not.toBe('')

    await page.goto(`/organizations/${organizationId}`)
    await page.getByRole('button', { name: 'Activity', exact: true }).click()
    const activityTab = page.getByTestId('organization-activity-tab')
    await expect(activityTab).toContainText('đã tạo project')
    await expect(activityTab).toContainText('đã tạo group')
    await page.getByRole('button', { name: 'Thành viên', exact: true }).click()
    const memberTab = page.getByTestId('organization-members-tab')
    await expect(memberTab).toContainText(directProjectName)
    await expect(memberTab).toContainText(groupProjectName)
    await expect(memberTab).toContainText('Owner')
    await expect(memberTab).toContainText(/40h\/tuần|Mặc định/)
    await memberTab.getByRole('button', { name: 'Quản lý role' }).click()
    await expect(page).toHaveURL(new RegExp(`/organizations/users\\?organization=${organizationId}`))
    await expect(page.getByRole('heading', { name: 'Thành viên tổ chức' })).toBeVisible()

    const member = await createMemberContext(browser)
    memberContext = member.context
    const forbiddenApi = await browserApiRequest(
      member.page,
      'GET',
      `/api/organizations/${organizationId}`,
    )
    expect(forbiddenApi.status()).toBe(403)
    await member.page.goto(`/organizations/${organizationId}`)
    await expect(member.page).toHaveURL(/\/access-denied|\/organizations\//)
    await expect(member.page.getByRole('alert')).toContainText(
      /không có quyền|Không thể tải tổ chức/i,
    )
  } finally {
    await memberContext?.close()
    if (groupProjectId) {
      await browserApiRequest(page, 'DELETE', `/api/projects/${groupProjectId}`)
    }
    if (directProjectId) {
      await browserApiRequest(page, 'DELETE', `/api/projects/${directProjectId}`)
    }
    if (groupId) {
      await browserApiRequest(page, 'DELETE', `/api/groups/${groupId}`)
    }
    if (organizationId) {
      await browserApiRequest(page, 'DELETE', `/api/organizations/${organizationId}`)
    }
  }
})
