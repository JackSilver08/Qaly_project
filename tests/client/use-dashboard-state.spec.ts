import { beforeEach, describe, expect, it, vi } from 'vitest'
import { useDashboard } from '@/composables/use-dashboard-state'

const mocks = vi.hoisted(() => ({ apiJson: vi.fn(), apiResult: vi.fn() }))
vi.mock('@/utils/api-client', () => ({ apiJson: mocks.apiJson, apiResult: mocks.apiResult }))

function dashboard(overrides: Record<string, unknown> = {}) {
  return {
    generatedAt: '2026-08-31T00:00:00Z',
    stats: { activeProjects: 1, totalTasks: 4, overdueTasks: 1, teamMembers: 1, completedTasks: 1, completionRate: 25, tasksAtRisk: 1 },
    summary: 'Summary', riskDigest: 'Risk', notifications: [], team: [],
    projects: [{
      id: 'project-1', name: 'Project', description: null, status: 'Active', endDate: null,
      members: [], tasks: [
        { id: 'todo', status: 'Todo', dueDate: '2000-01-01T00:00:00Z' },
        { id: 'done', status: 'Done', dueDate: '2000-01-01T00:00:00Z' },
        { id: 'cancelled', status: 'Cancelled', dueDate: '2000-01-01T00:00:00Z' },
        { id: 'progress', status: 'InProgress', dueDate: '2999-01-01T00:00:00Z' },
      ],
    }],
    ...overrides,
  }
}

function deferred<T>() {
  let resolve!: (value: T) => void
  let reject!: (reason?: unknown) => void
  const promise = new Promise<T>((res, rej) => { resolve = res; reject = rej })
  return { promise, resolve, reject }
}

beforeEach(() => {
  vi.clearAllMocks()
  // Failure-path cases intentionally exercise the composable's diagnostic log.
  // Silence that expected output so CI remains actionable; state/error assertions below
  // still fail if the failure contract regresses.
  vi.spyOn(console, 'warn').mockImplementation(() => undefined)
})

describe('useDashboard', () => {
  it('starts with an honest empty dashboard', () => {
    const state = useDashboard()
    expect(state.projects.value).toEqual([])
    expect(state.usingFallback.value).toBe(true)
    expect(state.isLoading.value).toBe(true)
  })

  it('normalizes missing dashboard collections', async () => {
    mocks.apiJson.mockResolvedValue(dashboard({ projects: null, team: null, notifications: null }))
    const state = useDashboard()
    await state.loadDashboard()
    expect(state.projects.value).toEqual([])
    expect(state.team.value).toEqual([])
    expect(state.dashboard.value.notifications).toEqual([])
  })

  it('normalizes missing project members and tasks', async () => {
    mocks.apiJson.mockResolvedValue(dashboard({ projects: [{ id: 'p', status: 'Active', members: null, tasks: null }] }))
    const state = useDashboard()
    await state.loadDashboard()
    expect(state.projects.value[0].members).toEqual([])
    expect(state.projects.value[0].tasks).toEqual([])
  })

  it('computes totals with Cancelled excluded from overdue', async () => {
    mocks.apiJson.mockResolvedValue(dashboard())
    const state = useDashboard()
    await state.loadDashboard()
    expect(state.totalTasks.value).toBe(4)
    expect(state.completedTasks.value).toBe(1)
    expect(state.overdueTasksCount.value).toBe(1)
  })

  it('clears stale dashboard data when loading fails', async () => {
    mocks.apiJson.mockResolvedValueOnce(dashboard()).mockRejectedValueOnce(new Error('offline'))
    const state = useDashboard()
    await state.loadDashboard()
    const result = await state.loadDashboard()
    expect(result).toBe(false)
    expect(state.projects.value).toEqual([])
    expect(state.loadError.value).toContain('trạng thái trống')
    expect(state.usingFallback.value).toBe(true)
  })

  it('clears the dashboard error after a successful retry', async () => {
    mocks.apiJson.mockRejectedValueOnce(new Error('offline')).mockResolvedValueOnce(dashboard())
    const state = useDashboard()
    await state.loadDashboard()
    await state.loadDashboard()
    expect(state.loadError.value).toBeNull()
    expect(state.usingFallback.value).toBe(false)
  })

  it('ignores an older dashboard response that arrives last', async () => {
    const oldRequest = deferred<ReturnType<typeof dashboard>>()
    const newRequest = deferred<ReturnType<typeof dashboard>>()
    mocks.apiJson.mockReturnValueOnce(oldRequest.promise).mockReturnValueOnce(newRequest.promise)
    const state = useDashboard()
    const first = state.loadDashboard()
    const second = state.loadDashboard()
    newRequest.resolve(dashboard({ summary: 'new' }))
    await second
    oldRequest.resolve(dashboard({ summary: 'old' }))
    await first
    expect(state.dashboard.value.summary).toBe('new')
  })

  it('keeps loading true while the latest dashboard request is pending', async () => {
    const oldRequest = deferred<ReturnType<typeof dashboard>>()
    const newRequest = deferred<ReturnType<typeof dashboard>>()
    mocks.apiJson.mockReturnValueOnce(oldRequest.promise).mockReturnValueOnce(newRequest.promise)
    const state = useDashboard()
    const first = state.loadDashboard()
    const second = state.loadDashboard()
    oldRequest.resolve(dashboard())
    await first
    expect(state.isLoading.value).toBe(true)
    newRequest.resolve(dashboard())
    await second
    expect(state.isLoading.value).toBe(false)
  })

  it('loads and records the current user', async () => {
    mocks.apiResult.mockResolvedValue({ id: 'user-1', fullName: 'Bảo' })
    const state = useDashboard()
    expect(await state.loadMe()).toBe(true)
    expect(state.currentUser.value?.id).toBe('user-1')
    expect(state.identityLoadError.value).toBeNull()
    expect(state.currentUserLoaded.value).toBe(true)
  })

  it('clears stale current-user data when identity loading fails', async () => {
    mocks.apiResult.mockResolvedValueOnce({ id: 'user-1' }).mockRejectedValueOnce(new Error('unauthorized'))
    const state = useDashboard()
    await state.loadMe()
    expect(await state.loadMe()).toBe(false)
    expect(state.currentUser.value).toBeNull()
    expect(state.identityLoadError.value).toBeTruthy()
  })

  it('ignores an older current-user response', async () => {
    const oldRequest = deferred<Record<string, string>>()
    const newRequest = deferred<Record<string, string>>()
    mocks.apiResult.mockReturnValueOnce(oldRequest.promise).mockReturnValueOnce(newRequest.promise)
    const state = useDashboard()
    const first = state.loadMe()
    const second = state.loadMe()
    newRequest.resolve({ id: 'new' })
    await second
    oldRequest.resolve({ id: 'old' })
    await first
    expect(state.currentUser.value?.id).toBe('new')
  })

  it('loads users and normalizes a non-array response', async () => {
    mocks.apiResult.mockResolvedValueOnce([{ id: 'u1' }]).mockResolvedValueOnce(null)
    const state = useDashboard()
    expect(await state.loadUsers()).toBe(true)
    expect(state.users.value).toHaveLength(1)
    await state.loadUsers()
    expect(state.users.value).toEqual([])
  })

  it('clears stale users and exposes an honest error', async () => {
    mocks.apiResult.mockResolvedValueOnce([{ id: 'u1' }]).mockRejectedValueOnce(new Error('offline'))
    const state = useDashboard()
    await state.loadUsers()
    expect(await state.loadUsers()).toBe(false)
    expect(state.users.value).toEqual([])
    expect(state.usersLoadError.value).toBeTruthy()
  })

  it('ignores an older users response', async () => {
    const oldRequest = deferred<Record<string, string>[]>()
    const newRequest = deferred<Record<string, string>[]>()
    mocks.apiResult.mockReturnValueOnce(oldRequest.promise).mockReturnValueOnce(newRequest.promise)
    const state = useDashboard()
    const first = state.loadUsers()
    const second = state.loadUsers()
    newRequest.resolve([{ id: 'new' }])
    await second
    oldRequest.resolve([{ id: 'old' }])
    await first
    expect(state.users.value[0].id).toBe('new')
  })
})
