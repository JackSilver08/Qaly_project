import { ref, computed } from 'vue'
import type { DashboardResponse, UserDto } from '../types'
import { apiJson, apiResult } from '../utils/api-client'
import { isTaskOverdue } from '../utils/formatters'
import type { SummaryCardModel } from '../components/dashboard-models'

export function useDashboard() {
  const dashboard = ref<DashboardResponse>(createEmptyDashboard())
  const currentUser = ref<UserDto | null>(null)
  const currentUserLoaded = ref(false)
  const users = ref<UserDto[]>([])
  const isLoading = ref(true)
  const usingFallback = ref(true)
  const loadError = ref<string | null>(null)
  const identityLoadError = ref<string | null>(null)
  const usersLoadError = ref<string | null>(null)
  let dashboardRequestVersion = 0
  let meRequestVersion = 0
  let usersRequestVersion = 0

  const projects = computed(() => dashboard.value.projects)
  const team = computed(() => dashboard.value.team)
  const activeProjectsCount = computed(() => projects.value.filter((p) => p.status !== 'Archived').length)
  const totalTasks = computed(() => projects.value.reduce((sum, p) => sum + p.tasks.length, 0))
  const completedTasks = computed(() =>
    projects.value.reduce((sum, p) => sum + p.tasks.filter((t) => t.status === 'Done').length, 0),
  )
  const overdueTasksCount = computed(() =>
    projects.value.reduce((sum, p) => sum + p.tasks.filter((t) => isTaskOverdue(t)).length, 0),
  )

  const summaryCards = computed<SummaryCardModel[]>(() => [
    {
      key: 'projects',
      label: 'Projects',
      value: String(projects.value.length),
      detail: `${activeProjectsCount.value} active`,
      tone: 'blue',
    },
    {
      key: 'tasks',
      label: 'Tasks',
      value: String(totalTasks.value),
      detail: `${completedTasks.value} done`,
      tone: 'mint',
    },
    {
      key: 'team',
      label: 'Team',
      value: String(team.value.length),
      detail: 'workspace members',
      tone: 'violet',
    },
  ])

  async function loadDashboard() {
    const requestVersion = ++dashboardRequestVersion
    isLoading.value = true
    try {
      const normalized = normalizeDashboard(await apiJson<DashboardResponse>('/api/dashboard/overview'))
      if (requestVersion !== dashboardRequestVersion) return false
      dashboard.value = normalized
      usingFallback.value = false
      loadError.value = null
      return true
    } catch (error) {
      if (requestVersion !== dashboardRequestVersion) return false
      console.warn('Could not load dashboard data.', error)
      dashboard.value = createEmptyDashboard()
      usingFallback.value = true
      loadError.value = 'Không thể tải dữ liệu bảng điều khiển. Qaly đang hiển thị trạng thái trống, không phải dữ liệu mẫu.'
      return false
    } finally {
      if (requestVersion === dashboardRequestVersion) isLoading.value = false
    }
  }

  async function loadMe() {
    const requestVersion = ++meRequestVersion
    try {
      const user = await apiResult<UserDto>('/api/auth/me')
      if (requestVersion !== meRequestVersion) return false
      currentUser.value = user
      identityLoadError.value = null
      return true
    } catch (error) {
      if (requestVersion !== meRequestVersion) return false
      console.warn('Could not load current user.', error)
      currentUser.value = null
      identityLoadError.value = 'Không thể xác minh người dùng hiện tại.'
      return false
    } finally {
      if (requestVersion === meRequestVersion) currentUserLoaded.value = true
    }
  }

  async function loadUsers() {
    const requestVersion = ++usersRequestVersion
    try {
      const loadedUsers = await apiResult<UserDto[]>('/api/users')
      if (requestVersion !== usersRequestVersion) return false
      users.value = Array.isArray(loadedUsers) ? loadedUsers : []
      usersLoadError.value = null
      return true
    } catch (error) {
      if (requestVersion !== usersRequestVersion) return false
      console.warn('Could not load users.', error)
      users.value = []
      usersLoadError.value = 'Không thể tải danh sách thành viên.'
      return false
    }
  }

  return {
    dashboard,
    currentUser,
    currentUserLoaded,
    users,
    isLoading,
    usingFallback,
    loadError,
    identityLoadError,
    usersLoadError,
    projects,
    team,
    activeProjectsCount,
    totalTasks,
    completedTasks,
    overdueTasksCount,
    summaryCards,
    loadDashboard,
    loadMe,
    loadUsers,
  }
}

function normalizeDashboard(dashboard: DashboardResponse): DashboardResponse {
  return {
    ...dashboard,
    projects: Array.isArray(dashboard.projects)
      ? dashboard.projects.map((project) => ({
          ...project,
          members: Array.isArray(project.members) ? project.members : [],
          tasks: Array.isArray(project.tasks) ? project.tasks : [],
        }))
      : [],
    team: Array.isArray(dashboard.team) ? dashboard.team : [],
    notifications: Array.isArray(dashboard.notifications) ? dashboard.notifications : [],
  }
}

function createEmptyDashboard(): DashboardResponse {
  return {
    generatedAt: new Date().toISOString(),
    stats: {
      activeProjects: 0,
      totalTasks: 0,
      overdueTasks: 0,
      teamMembers: 0,
      completedTasks: 0,
      completionRate: 0,
      tasksAtRisk: 0,
    },
    summary: '',
    riskDigest: '',
    projects: [],
    team: [],
    notifications: [],
  }
}
