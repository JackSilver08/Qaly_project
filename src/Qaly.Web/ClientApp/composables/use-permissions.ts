import { ref, computed } from 'vue'
import { apiResult } from '../utils/api-client'
import { showSuccess, showError } from './use-toast'

interface SystemPermission {
  moduleKey: string
  isAllowed: boolean
  aiTier: string
  source?: string
}

type PermissionLoadState = 'not_loaded' | 'loading' | 'loaded' | 'error'

const simulationUserIdKey = 'qaly-simulated-user-id'
const simulationUserNameKey = 'qaly-simulated-user-name'

function readSessionValue(key: string) {
  if (typeof window === 'undefined') return null
  try {
    return window.sessionStorage.getItem(key)
  } catch {
    return null
  }
}

// Global simulation state, restored after the reload that applies the new effective principal.
const simulatedUserId = ref<string | null>(readSessionValue(simulationUserIdKey))
const simulatedUserName = ref<string | null>(readSessionValue(simulationUserNameKey))
const isSimulationActive = ref(Boolean(simulatedUserId.value))
const systemPermissions = ref<SystemPermission[]>([])
const permissionLoadState = ref<PermissionLoadState>('not_loaded')

export function usePermissions() {
  const startSimulation = (userId: string, userName: string) => {
    simulatedUserId.value = userId
    simulatedUserName.value = userName
    isSimulationActive.value = true
    if (typeof window !== 'undefined') {
      window.sessionStorage.setItem(simulationUserIdKey, userId)
      window.sessionStorage.setItem(simulationUserNameKey, userName)
    }
    showSuccess(`Bật Simulation Mode: Đang xem dưới danh nghĩa ${userName}`)
  }

  const stopSimulation = () => {
    isSimulationActive.value = false
    simulatedUserId.value = null
    simulatedUserName.value = null
    if (typeof window !== 'undefined') {
      window.sessionStorage.removeItem(simulationUserIdKey)
      window.sessionStorage.removeItem(simulationUserNameKey)
    }
    showSuccess('Đã thoát khỏi Simulation Mode')
  }

  const getSimulationHeaders = (): Record<string, string> => {
    if (isSimulationActive.value && simulatedUserId.value) {
      return { 'X-Simulate-User-Id': simulatedUserId.value }
    }
    return {}
  }

  const loadSystemPermissions = async (systemRole?: string) => {
    permissionLoadState.value = 'loading'
    try {
      const url = systemRole
        ? `/api/ProjectRoles/system-permissions?systemRole=${encodeURIComponent(systemRole)}`
        : '/api/ProjectRoles/effective-system-permissions'
      systemPermissions.value = await apiResult<SystemPermission[]>(url)
      permissionLoadState.value = 'loaded'
      return true
    } catch {
      systemPermissions.value = []
      permissionLoadState.value = 'error'
      showError('Không tải được quyền hệ thống.')
      return false
    }
  }

  const canAccessModule = (moduleKey: string): boolean => {
    if (permissionLoadState.value !== 'loaded') return false
    const perm = systemPermissions.value.find(p => p.moduleKey === moduleKey)
    return perm?.isAllowed === true
  }

  const getAiTier = (moduleKey = 'AiHub'): string => {
    if (permissionLoadState.value !== 'loaded') return 'Restricted'
    const perm = systemPermissions.value.find(p => p.moduleKey === moduleKey)
    return perm?.isAllowed === true ? perm.aiTier : 'Restricted'
  }

  return {
    isSimulationActive: computed(() => isSimulationActive.value),
    simulatedUserId: computed(() => simulatedUserId.value),
    simulatedUserName: computed(() => simulatedUserName.value),
    systemPermissions,
    permissionLoadState: computed(() => permissionLoadState.value),
    startSimulation,
    stopSimulation,
    getSimulationHeaders,
    loadSystemPermissions,
    canAccessModule,
    getAiTier
  }
}
