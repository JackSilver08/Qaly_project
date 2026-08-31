import { beforeEach, describe, expect, it, vi } from 'vitest'

const apiResult = vi.fn()
const showSuccess = vi.fn()
const showError = vi.fn()

vi.mock('@/utils/api-client', () => ({ apiResult: (...args: unknown[]) => apiResult(...args) }))
vi.mock('@/composables/use-toast', () => ({
  showSuccess: (...args: unknown[]) => showSuccess(...args),
  showError: (...args: unknown[]) => showError(...args),
}))

/**
 * Simulation state lives on the module, so each test loads a fresh instance.
 */
async function loadPermissions() {
  vi.resetModules()
  return (await import('@/composables/use-permissions')).usePermissions()
}

beforeEach(() => {
  apiResult.mockReset()
  showSuccess.mockReset()
  showError.mockReset()
})

describe('simulation mode', () => {
  it('starts inactive and sends no impersonation header', async () => {
    const permissions = await loadPermissions()

    expect(permissions.isSimulationActive.value).toBe(false)
    expect(permissions.simulatedUserId.value).toBeNull()
    expect(permissions.simulatedUserName.value).toBeNull()
    expect(permissions.getSimulationHeaders()).toEqual({})
  })

  it('records the simulated user and adds the impersonation header', async () => {
    const permissions = await loadPermissions()

    permissions.startSimulation('user-1', 'Bảo Ngọc')

    expect(permissions.isSimulationActive.value).toBe(true)
    expect(permissions.simulatedUserId.value).toBe('user-1')
    expect(permissions.simulatedUserName.value).toBe('Bảo Ngọc')
    expect(permissions.getSimulationHeaders()).toEqual({ 'X-Simulate-User-Id': 'user-1' })
    expect(showSuccess).toHaveBeenCalledWith(expect.stringContaining('Bảo Ngọc'))
  })

  it('clears every trace of the simulated user on stop', async () => {
    const permissions = await loadPermissions()

    permissions.startSimulation('user-1', 'Bảo Ngọc')
    permissions.stopSimulation()

    expect(permissions.isSimulationActive.value).toBe(false)
    expect(permissions.simulatedUserId.value).toBeNull()
    expect(permissions.simulatedUserName.value).toBeNull()
    expect(permissions.getSimulationHeaders()).toEqual({})
    expect(showSuccess).toHaveBeenLastCalledWith('Đã thoát khỏi Simulation Mode')
  })

  it('shares one simulation state across every caller of the composable', async () => {
    vi.resetModules()
    const { usePermissions } = await import('@/composables/use-permissions')
    const header = usePermissions()
    const sidebar = usePermissions()

    header.startSimulation('user-9', 'Minh')

    expect(sidebar.isSimulationActive.value).toBe(true)
    expect(sidebar.getSimulationHeaders()).toEqual({ 'X-Simulate-User-Id': 'user-9' })
  })
})

describe('loadSystemPermissions', () => {
  it('requests the permissions for the given system role', async () => {
    const permissions = await loadPermissions()
    apiResult.mockResolvedValueOnce([{ moduleKey: 'AiHub', isAllowed: true, aiTier: 'Full' }])

    await permissions.loadSystemPermissions('Admin')

    expect(apiResult).toHaveBeenCalledWith('/api/ProjectRoles/system-permissions?systemRole=Admin')
    expect(permissions.systemPermissions.value).toHaveLength(1)
  })

  it('sends an empty role when none is supplied', async () => {
    const permissions = await loadPermissions()
    apiResult.mockResolvedValueOnce([])

    await permissions.loadSystemPermissions()

    expect(apiResult).toHaveBeenCalledWith('/api/ProjectRoles/system-permissions?systemRole=')
  })

  it('clears the cache and warns the user when the request fails', async () => {
    const permissions = await loadPermissions()
    apiResult.mockResolvedValueOnce([{ moduleKey: 'AiHub', isAllowed: false, aiTier: 'None' }])
    await permissions.loadSystemPermissions('Admin')
    expect(permissions.systemPermissions.value).toHaveLength(1)

    apiResult.mockRejectedValueOnce(new Error('network down'))
    await permissions.loadSystemPermissions('Admin')

    expect(permissions.systemPermissions.value).toEqual([])
    expect(showError).toHaveBeenCalledWith('Không tải được quyền hệ thống.')
  })
})

describe('canAccessModule', () => {
  it('honours an explicit allow or deny', async () => {
    const permissions = await loadPermissions()
    apiResult.mockResolvedValueOnce([
      { moduleKey: 'AiHub', isAllowed: true, aiTier: 'Full' },
      { moduleKey: 'Analytics', isAllowed: false, aiTier: 'None' },
    ])
    await permissions.loadSystemPermissions('Member')

    expect(permissions.canAccessModule('AiHub')).toBe(true)
    expect(permissions.canAccessModule('Analytics')).toBe(false)
  })

  it('fails open for an unlisted module — the server stays the enforcement boundary', async () => {
    const permissions = await loadPermissions()
    apiResult.mockResolvedValueOnce([{ moduleKey: 'AiHub', isAllowed: false, aiTier: 'None' }])
    await permissions.loadSystemPermissions('Member')

    // Documents QALY-UI-04: navigation is only cosmetic here, the API still authorises each call.
    expect(permissions.canAccessModule('UnknownModule')).toBe(true)
  })
})

describe('getAiTier', () => {
  it('defaults to the AiHub module', async () => {
    const permissions = await loadPermissions()
    apiResult.mockResolvedValueOnce([{ moduleKey: 'AiHub', isAllowed: true, aiTier: 'Specialist' }])
    await permissions.loadSystemPermissions('Developer')

    expect(permissions.getAiTier()).toBe('Specialist')
  })

  it('reads the tier of an explicitly named module', async () => {
    const permissions = await loadPermissions()
    apiResult.mockResolvedValueOnce([
      { moduleKey: 'AiHub', isAllowed: true, aiTier: 'Specialist' },
      { moduleKey: 'Analytics', isAllowed: true, aiTier: 'ReadOnly' },
    ])
    await permissions.loadSystemPermissions('Developer')

    expect(permissions.getAiTier('Analytics')).toBe('ReadOnly')
  })

  it('falls back to Full when the module is unknown', async () => {
    const permissions = await loadPermissions()
    expect(permissions.getAiTier('Nowhere')).toBe('Full')
  })
})
