import { expect, type Page } from '@playwright/test'

/**
 * Canonical presentation project created by `DataSeeder.PresentationDemo`.
 * `Qaly Work OS - Customer Demo` was the pre-Release-4 name; keep old shells and CI variables working.
 */
export const seededDemoProjectName = (() => {
  const configured = process.env.E2E_DEMO_PROJECT?.trim()
  return !configured || configured === 'Qaly Work OS - Customer Demo' ? 'Qaly Release 4.0' : configured
})()

type ProjectSummary = { id: string; name: string; status?: string }

/**
 * Resolves a seeded project by name through the API rather than the `/projects` grid.
 *
 * The grid is paginated and ordered by recency, so a project created by a test running in
 * parallel can push the seeded one off the first page — which silently turned name lookups
 * into "whatever project happens to be first" and produced misleading UI failures.
 */
export async function resolveSeededProjectId(page: Page, name = seededDemoProjectName): Promise<string> {
  const response = await page.request.get(
    `/api/projects?page=1&pageSize=50&search=${encodeURIComponent(name)}`,
  )
  expect(response.ok(), `Không gọi được /api/projects để tìm “${name}”`).toBeTruthy()

  const payload = await response.json()
  const items: ProjectSummary[] = payload?.data?.items ?? payload?.items ?? payload?.data ?? []
  const project = items.find(item => item.name === name) ?? items.find(item => item.name?.includes(name))

  expect(project?.id, `Dữ liệu seed phải có dự án “${name}”`).toBeTruthy()
  return project!.id
}

/** Opens the seeded project detail page directly, bypassing the paginated project grid. */
export async function openSeededProject(page: Page, name = seededDemoProjectName): Promise<string> {
  const projectId = await resolveSeededProjectId(page, name)
  await page.goto(`/projects/${projectId}`, { waitUntil: 'domcontentloaded' })
  await expect(page.locator('.project-tabs')).toBeVisible()
  await expect(page.getByRole('heading', { name })).toBeVisible()
  return projectId
}
