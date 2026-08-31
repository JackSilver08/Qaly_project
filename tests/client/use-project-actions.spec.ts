import { ref } from 'vue'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { useProjectActions } from '@/composables/use-project-actions'

const mocks = vi.hoisted(() => ({
  apiCommand: vi.fn(), apiResult: vi.fn(), confirmDialog: vi.fn(),
  showError: vi.fn(), showSuccess: vi.fn(), push: vi.fn(),
}))
vi.mock('vue-router', () => ({ useRouter: () => ({ push: mocks.push }) }))
vi.mock('@/utils/api-client', () => ({
  apiCommand: mocks.apiCommand,
  apiResult: mocks.apiResult,
  errorMessage: (_error: unknown, fallback: string) => fallback,
}))
vi.mock('@/composables/use-confirm-dialog', () => ({ confirmDialog: mocks.confirmDialog }))
vi.mock('@/composables/use-toast', () => ({ showError: mocks.showError, showSuccess: mocks.showSuccess }))

function project(overrides: Record<string, unknown> = {}) {
  return {
    id: 'project-1', name: 'Project one', description: 'Description', status: 'Active',
    startDate: null, endDate: '2026-12-01T00:00:00Z', ownerId: 'owner-1', ownerName: 'Owner',
    organizationId: null, organizationName: null, memberCount: 0, taskCount: 0,
    completedTaskCount: 0, progressPercentage: 0, members: [], tasks: [],
    ...overrides,
  }
}

function setup() {
  const projects = ref([project()] as never[])
  const activeProjectId = ref<string | null>(null)
  const loadDashboard = vi.fn().mockResolvedValue(undefined)
  const actions = useProjectActions(projects, activeProjectId, loadDashboard)
  return { actions, projects, activeProjectId, loadDashboard }
}

function deferred<T>() {
  let resolve!: (value: T) => void
  const promise = new Promise<T>(res => { resolve = res })
  return { promise, resolve }
}

beforeEach(() => {
  vi.clearAllMocks()
  mocks.confirmDialog.mockResolvedValue(true)
  mocks.apiCommand.mockResolvedValue(undefined)
})

describe('useProjectActions', () => {
  it('opens the create form and navigates to the project list', () => {
    const { actions } = setup()
    actions.openCreateProject()
    expect(actions.createProjectOpen.value).toBe(true)
    expect(mocks.push).toHaveBeenCalledWith('/projects')
  })

  it('does not create a project with a blank name', async () => {
    const { actions } = setup()
    await actions.createProject()
    expect(mocks.apiResult).not.toHaveBeenCalled()
  })

  it('creates, reads back and navigates to the canonical project', async () => {
    const canonical = project({ name: 'New project' })
    mocks.apiResult.mockResolvedValueOnce(canonical).mockResolvedValueOnce(canonical)
    const { actions, activeProjectId, loadDashboard } = setup()
    actions.projectName.value = ' New project '
    actions.projectDescription.value = ' Details '
    await actions.createProject()
    expect(mocks.apiResult.mock.calls[0][0]).toBe('/api/projects')
    expect(mocks.apiResult.mock.calls[1][0]).toBe('/api/projects/project-1')
    expect(loadDashboard).toHaveBeenCalledOnce()
    expect(activeProjectId.value).toBe('project-1')
    expect(mocks.push).toHaveBeenCalledWith('/projects/project-1')
    expect(actions.projectName.value).toBe('')
  })

  it('keeps the create form when canonical read-back disagrees', async () => {
    mocks.apiResult.mockResolvedValueOnce(project({ name: 'New project' })).mockResolvedValueOnce(project({ name: 'Other' }))
    const { actions } = setup()
    actions.projectName.value = 'New project'
    await actions.createProject()
    expect(actions.projectName.value).toBe('New project')
    expect(actions.createProjectOpen.value).toBe(false)
    expect(mocks.showError).toHaveBeenCalled()
  })

  it('prevents duplicate project creation while the first request is pending', async () => {
    const pending = deferred<ReturnType<typeof project>>()
    mocks.apiResult.mockReturnValueOnce(pending.promise)
    const { actions } = setup()
    actions.projectName.value = 'New project'
    const first = actions.createProject()
    const second = actions.createProject()
    expect(mocks.apiResult).toHaveBeenCalledTimes(1)
    mocks.apiResult.mockResolvedValueOnce(project({ name: 'New project' }))
    pending.resolve(project({ name: 'New project' }))
    await Promise.all([first, second])
  })

  it('populates edit state only for an existing project', () => {
    const { actions, activeProjectId } = setup()
    actions.beginEditProject('missing')
    expect(actions.projectBeingEditedId.value).toBeNull()
    actions.beginEditProject('project-1')
    expect(actions.projectBeingEditedId.value).toBe('project-1')
    expect(actions.editProjectName.value).toBe('Project one')
    expect(activeProjectId.value).toBe('project-1')
  })

  it('saves an edit only after canonical read-back confirms it', async () => {
    const updated = project({ name: 'Renamed' })
    mocks.apiResult.mockResolvedValueOnce(updated).mockResolvedValueOnce(updated)
    const { actions, loadDashboard } = setup()
    actions.beginEditProject('project-1')
    actions.editProjectName.value = 'Renamed'
    await actions.saveProjectEdit()
    expect(actions.projectBeingEditedId.value).toBeNull()
    expect(loadDashboard).toHaveBeenCalledOnce()
  })

  it('keeps edit state when read-back does not confirm the name', async () => {
    mocks.apiResult.mockResolvedValueOnce(project({ name: 'Renamed' })).mockResolvedValueOnce(project())
    const { actions } = setup()
    actions.beginEditProject('project-1')
    actions.editProjectName.value = 'Renamed'
    await actions.saveProjectEdit()
    expect(actions.projectBeingEditedId.value).toBe('project-1')
    expect(mocks.showError).toHaveBeenCalled()
  })

  it('does not delete when confirmation is cancelled', async () => {
    mocks.confirmDialog.mockResolvedValue(false)
    const { actions } = setup()
    await actions.deleteProject('project-1')
    expect(mocks.apiCommand).not.toHaveBeenCalled()
  })

  it('verifies deletion against the refreshed project collection', async () => {
    const { actions, projects, loadDashboard } = setup()
    loadDashboard.mockImplementation(async () => { projects.value = [] })
    await actions.deleteProject('project-1')
    expect(mocks.apiCommand).toHaveBeenCalledWith('/api/projects/project-1', { method: 'DELETE' })
    expect(mocks.showSuccess).toHaveBeenCalled()
  })

  it('archives only after the canonical status is Archived', async () => {
    mocks.apiResult.mockResolvedValueOnce(project({ status: 'Archived' })).mockResolvedValueOnce(project({ status: 'Archived' }))
    const { actions, loadDashboard } = setup()
    await actions.archiveProject('project-1')
    expect(loadDashboard).toHaveBeenCalledOnce()
    expect(mocks.showSuccess).toHaveBeenCalled()
  })

  it('restores only after the canonical status is Active', async () => {
    const { actions, projects, loadDashboard } = setup()
    projects.value = [project({ status: 'Archived' })] as never[]
    mocks.apiResult.mockResolvedValueOnce(project({ status: 'Active' })).mockResolvedValueOnce(project({ status: 'Active' }))
    await actions.restoreProject('project-1')
    expect(loadDashboard).toHaveBeenCalledOnce()
    expect(mocks.showSuccess).toHaveBeenCalled()
  })
})
