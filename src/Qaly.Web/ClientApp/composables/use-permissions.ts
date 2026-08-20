import { ref, computed } from 'vue'
import { apiResult } from '../utils/api-client'
import { showSuccess, showError } from './use-toast'

interface SystemPermission {
  moduleKey: string
  isAllowed: boolean
  aiTier: string
}

// Global simulation state
const isSimulationActive = ref(false)
const simulatedUserId = ref<string | null>(null)
const simulatedUserName = ref<string | null>(null)
const systemPermissions = ref<SystemPermission[]>([])

export function usePermissions() {
  const startSimulation = (userId: string, userName: string) => {
    simulatedUserId.value = userId
    simulatedUserName.value = userName
    isSimulationActive.value = true
    showSuccess(`Bật Simulation Mode: Đang xem dưới danh nghĩa ${userName}`)
  }

  const stopSimulation = () => {
    isSimulationActive.value = false
    simulatedUserId.value = null
    simulatedUserName.value = null
    showSuccess('Đã thoát khỏi Simulation Mode')
  }

  const getSimulationHeaders = (): Record<string, string> => {
    if (isSimulationActive.value && simulatedUserId.value) {
      return { 'X-Simulate-User-Id': simulatedUserId.value }
    }
    return {}
  }

  const loadSystemPermissions = async (systemRole?: string) => {
    systemPermissions.value = await apiResult<SystemPermission[]>(`/api/ProjectRoles/system-permissions?systemRole=${systemRole || ''}`)
  }

  const canAccessModule = (moduleKey: string): boolean => {
    const perm = systemPermissions.value.find(p => p.moduleKey === moduleKey)
    return perm ? perm.isAllowed : true
  }

  const getAiTier = (moduleKey = 'AiHub'): string => {
    const perm = systemPermissions.value.find(p => p.moduleKey === moduleKey)
    return perm ? perm.aiTier : 'Full'
  }

  return {
    isSimulationActive: computed(() => isSimulationActive.value),
    simulatedUserId: computed(() => simulatedUserId.value),
    simulatedUserName: computed(() => simulatedUserName.value),
    systemPermissions,
    startSimulation,
    stopSimulation,
    getSimulationHeaders,
    loadSystemPermissions,
    canAccessModule,
    getAiTier
  }
}
