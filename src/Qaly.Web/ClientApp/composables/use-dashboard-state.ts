import { ref, computed } from 'vue'
import type { DashboardResponse, UserDto } from '../types'
import { fallbackDashboard } from '../fallback-dashboard'
import { apiJson, apiResult } from '../utils/api-client'
import { isTaskOverdue } from '../utils/formatters'
import type { SummaryCardModel } from '../components/dashboard-models'

export function useDashboard() {
  const dashboard = ref<DashboardResponse>(fallbackDashboard)
  const currentUser = ref<UserDto | null>(null)
  const users = ref<UserDto[]>([])
  const isLoading = ref(true)
  const usingFallback = ref(true)

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
    isLoading.value = true
    try {
      dashboard.value = await apiJson<DashboardResponse>('/api/dashboard/overview')
      usingFallback.value = false
    } catch (error) {
      console.warn('Using fallback dashboard data.', error)
      dashboard.value = fallbackDashboard
      usingFallback.value = true
    } finally {
      isLoading.value = false
    }
  }

  async function loadMe() {
    try {
      currentUser.value = await apiResult<UserDto>('/api/auth/me')
    } catch (error) {
      console.warn('Could not load current user.', error)
    }
  }

  async function loadUsers() {
    try {
      users.value = await apiResult<UserDto[]>('/api/users')
    } catch (error) {
      console.warn('Could not load users.', error)
    }
  }

  return {
    dashboard,
    currentUser,
    users,
    isLoading,
    usingFallback,
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
