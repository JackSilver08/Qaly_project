<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import {
  ArrowRight,
  BadgeAlert,
  BellRing,
  CheckCheck,
  CheckSquare2,
  Circle,
  Clock3,
  Filter,
  ListFilter,
  Loader2,
  Pin,
  RefreshCw,
  Search,
  Send,
  SquareCheckBig,
  Trash2,
  Waypoints,
  X,
} from 'lucide-vue-next'
import { useDashboardContext } from '../composables/dashboard-context'
import { apiCommand, apiJson, apiResult, errorMessage } from '../utils/api-client'
import { showError, showSuccess } from '../composables/use-toast'
import type {
  AttachmentDto,
  CommentDto,
  DashboardProject,
  DashboardTask,
  PagedResult,
  TaskAttentionDto,
  TaskItemDto,
  TaskMeetingSourceDto,
  TimeEntryDto,
} from '../types'

type TaskScope = 'mine' | 'all'
type TaskSort = 'risk' | 'dueDate' | 'priority' | 'status' | 'project' | 'alpha'
type TaskFocus = 'all' | 'overdue' | 'dueSoon' | 'pinned' | 'high' | 'blocked'
type TriageMode = 'all' | 'urgent' | 'atRisk' | 'watchlist' | 'stable'

type SavedTaskView = {
  id: string
  name: string
  scope: TaskScope
  searchQuery: string
  statusFilter: string
  priorityFilter: string
  projectFilter: string
  focusFilter: TaskFocus
  triageMode: TriageMode
  sortBy: TaskSort
}

type HubTask = DashboardTask & {
  projectId: string
  projectCode: string
  projectOwnerName: string
  projectStatus: string
  projectMemberCount: number
  projectProgressPercentage: number
  projectCreatedAt: string
}

const {
  currentUser,
  displayStatus,
  formatDate,
  formatTime,
  isTaskOverdue,
  loadDashboard,
  nextStatuses,
  openTask,
  projects,
} = useDashboardContext()

const taskScope = ref<TaskScope>('mine')
const searchQuery = ref('')
const statusFilter = ref<string>('all')
const priorityFilter = ref<string>('all')
const projectFilter = ref<string>('all')
const focusFilter = ref<TaskFocus>('all')
const triageMode = ref<TriageMode>('all')
const sortBy = ref<TaskSort>('risk')
const savedViews = ref<SavedTaskView[]>([])
const activeSavedViewId = ref<string | null>(null)

const savedViewsStorageKey = 'qaly.task-hub.saved-views'

const attentionItems = ref<TaskAttentionDto[]>([])
const attentionLoading = ref(false)
const selectedTaskId = ref<string | null>(null)
const selectedTaskDetail = ref<TaskItemDto | null>(null)
const selectedTaskComments = ref<CommentDto[]>([])
const selectedTaskAttachments = ref<AttachmentDto[]>([])
const selectedTaskTimeEntries = ref<TimeEntryDto[]>([])
const selectedTaskMeetingSource = ref<TaskMeetingSourceDto | null>(null)
const selectedTaskLoading = ref(false)

const selectedTaskIds = ref<string[]>([])

const statusOptions = ['Todo', 'InProgress', 'InReview', 'OnHold', 'Done', 'Cancelled']
const priorityOptions = ['Low', 'Medium', 'High', 'Critical']
const focusOptions: Array<{ label: string; value: TaskFocus }> = [
  { label: 'Tất cả', value: 'all' },
  { label: 'Quá hạn', value: 'overdue' },
  { label: 'Sắp đến hạn', value: 'dueSoon' },
  { label: 'Ghim', value: 'pinned' },
  { label: 'Ưu tiên cao', value: 'high' },
  { label: 'Đang chờ', value: 'blocked' },
]
const sortOptions: Array<{ label: string; value: TaskSort }> = [
  { label: 'Rủi ro', value: 'risk' },
  { label: 'Đến hạn', value: 'dueDate' },
  { label: 'Ưu tiên', value: 'priority' },
  { label: 'Trạng thái', value: 'status' },
  { label: 'Dự án', value: 'project' },
  { label: 'A-Z', value: 'alpha' },
]

const allTasks = computed<HubTask[]>(() =>
  projects.value.flatMap((project: DashboardProject) =>
    (project.tasks ?? []).map((task: DashboardTask) => ({
      ...task,
      projectId: project.id,
      projectCode: project.code,
      projectOwnerName: project.ownerName,
      projectStatus: project.status,
      projectMemberCount: project.memberCount,
      projectProgressPercentage: project.progressPercentage,
      projectCreatedAt: project.createdAt,
    })),
  ),
)

const mineTasks = computed(() => {
  const user = currentUser.value
  if (!user) {
    return allTasks.value
  }

  return allTasks.value.filter((task) =>
    task.assigneeId === user.id ||
    task.assigneeName === user.fullName ||
    task.reporterName === user.fullName,
  )
})

const visibleTaskBase = computed(() => (taskScope.value === 'mine' ? mineTasks.value : allTasks.value))

const filteredTasks = computed(() => {
  const query = searchQuery.value.trim().toLowerCase()

  return visibleTaskBase.value
    .filter((task) => {
      if (projectFilter.value !== 'all' && task.projectId !== projectFilter.value) return false
      if (statusFilter.value !== 'all' && task.status !== statusFilter.value) return false
      if (priorityFilter.value !== 'all' && task.priority !== priorityFilter.value) return false
      if (focusFilter.value !== 'all' && !matchesFocus(task, focusFilter.value)) return false
      if (triageMode.value !== 'all' && taskRiskBucket(task) !== triageMode.value) return false
      if (!query) return true

      return [
        task.title,
        task.projectName,
        task.reporterName,
        task.assigneeName,
        task.status,
        task.priority,
        task.projectCode,
      ]
        .filter(Boolean)
        .join(' ')
        .toLowerCase()
        .includes(query)
    })
    .sort(compareTasks)
})

const attentionSummary = computed(() => ({
  total: attentionItems.value.length,
  overdue: attentionItems.value.filter((item) => item.isOverdue).length,
  dueSoon: attentionItems.value.filter((item) => item.isDueSoon).length,
  stale: attentionItems.value.filter((item) => item.isStaleTodo || item.isStaleInProgress).length,
  unseen: attentionItems.value.filter((item) => item.isUnseenByAssignee).length,
}))

const taskSummary = computed(() => {
  const tasks = visibleTaskBase.value
  return {
    total: tasks.length,
    overdue: tasks.filter((task) => isTaskOverdue(task)).length,
    dueSoon: tasks.filter((task) => isDueSoon(task)).length,
    pinned: tasks.filter((task) => task.isPinned).length,
    high: tasks.filter((task) => ['High', 'Critical'].includes(task.priority)).length,
  }
})

const attentionItemMap = computed(() =>
  new Map(attentionItems.value.map((item) => [item.id, item])),
)

const triageSourceTasks = computed(() => visibleTaskBase.value)

const riskRadar = computed(() => {
  const tasks = triageSourceTasks.value
  const total = Math.max(1, tasks.length)

  const axes = [
    {
      id: 'deadline',
      label: 'Deadline',
      score: Math.round((tasks.filter((task) => isTaskOverdue(task)).length * 100 + tasks.filter((task) => isDueSoon(task)).length * 60) / total),
      caption: 'Task quá hạn và sắp đến hạn',
    },
    {
      id: 'blocker',
      label: 'Blockers',
      score: Math.round((tasks.filter((task) => ['Blocked', 'OnHold'].includes(task.status)).length * 100 + tasks.filter((task) => taskRiskReasons(task).some((reason) => reason === 'stale')).length * 45) / total),
      caption: 'Task đang bị chặn hoặc quá lâu',
    },
    {
      id: 'priority',
      label: 'Priority',
      score: Math.round(tasks.reduce((sum, task) => sum + priorityPressure(task), 0) / total),
      caption: 'Mức ưu tiên của task đang mở',
    },
    {
      id: 'ownership',
      label: 'Ownership',
      score: Math.round(tasks.filter((task) => !task.assigneeId).length * 100 / total),
      caption: 'Task chưa có người phụ trách',
    },
    {
      id: 'freshness',
      label: 'Freshness',
      score: Math.round(tasks.reduce((sum, task) => sum + freshnessPressure(task), 0) / total),
      caption: 'Task thiếu tín hiệu cập nhật',
    },
  ]

  return axes.map((axis) => ({
    ...axis,
    active: axis.score >= 70,
  }))
})

const triageBuckets = computed(() => {
  const tasks = triageSourceTasks.value
  const buckets = {
    urgent: tasks.filter((task) => taskRiskBucket(task) === 'urgent'),
    atRisk: tasks.filter((task) => taskRiskBucket(task) === 'atRisk'),
    watchlist: tasks.filter((task) => taskRiskBucket(task) === 'watchlist'),
    stable: tasks.filter((task) => taskRiskBucket(task) === 'stable'),
  }

  return [
    {
      key: 'urgent' as const,
      label: 'Urgent',
      count: buckets.urgent.length,
      description: 'Quá hạn, blocked hoặc sắp bùng rủi ro.',
      tone: 'danger',
    },
    {
      key: 'atRisk' as const,
      label: 'At risk',
      count: buckets.atRisk.length,
      description: 'Nên xử lý trong ngày để tránh trễ nhịp.',
      tone: 'warning',
    },
    {
      key: 'watchlist' as const,
      label: 'Watchlist',
      count: buckets.watchlist.length,
      description: 'Cần theo dõi thêm nhưng chưa đến mức gấp.',
      tone: 'info',
    },
    {
      key: 'stable' as const,
      label: 'Stable',
      count: buckets.stable.length,
      description: 'Đang ổn, tiếp tục giữ nhịp hiện tại.',
      tone: 'success',
    },
  ]
})

const topRiskTasks = computed(() =>
  triageSourceTasks.value
    .map((task) => ({
      ...task,
      riskScore: taskRiskScore(task),
      riskBucket: taskRiskBucket(task),
      riskReasons: taskRiskReasons(task),
      nextAction: nextBestAction(task),
    }))
    .sort((left, right) => right.riskScore - left.riskScore)
    .slice(0, 5),
)

const taskProjectOptions = computed(() => {
  const seen = new Map<string, { id: string; label: string }>()

  for (const project of projects.value) {
    if ((project.tasks ?? []).length === 0) continue
    seen.set(project.id, {
      id: project.id,
      label: `${project.name} · ${project.code}`,
    })
  }

  return [...seen.values()].sort((left, right) => left.label.localeCompare(right.label))
})

const selectedTaskSummary = computed(() => {
  if (!selectedTaskId.value) return null
  return allTasks.value.find((task) => task.id === selectedTaskId.value) ?? attentionItems.value.find((item) => item.id === selectedTaskId.value) ?? null
})

const selectedTaskProject = computed(() => {
  const summary = selectedTaskSummary.value
  if (!summary) return null
  return projects.value.find((project: DashboardProject) => project.id === summary.projectId) ?? null
})

const selectedCount = computed(() => selectedTaskIds.value.length)
const isAllVisibleSelected = computed(() =>
  filteredTasks.value.length > 0 &&
  filteredTasks.value.every((task) => selectedTaskIds.value.includes(task.id)),
)

const currentViewSignature = computed(() =>
  JSON.stringify({
    scope: taskScope.value,
    searchQuery: searchQuery.value.trim(),
    statusFilter: statusFilter.value,
    priorityFilter: priorityFilter.value,
    projectFilter: projectFilter.value,
    focusFilter: focusFilter.value,
    triageMode: triageMode.value,
    sortBy: sortBy.value,
  }),
)

onMounted(async () => {
  loadSavedViews()
  await Promise.all([refreshAttentionInbox(), refreshDashboard()])
})

watch(
  () => selectedTaskId.value,
  async (taskId) => {
    if (!taskId) {
      resetTaskDrawer()
      return
    }

    await loadTaskDetail(taskId)
  },
)

function compareTasks(left: HubTask, right: HubTask) {
  if (sortBy.value === 'alpha') return left.title.localeCompare(right.title)
  if (sortBy.value === 'project') return left.projectName.localeCompare(right.projectName) || left.title.localeCompare(right.title)
  if (sortBy.value === 'status') return statusRank(left.status) - statusRank(right.status) || priorityRank(left.priority) - priorityRank(right.priority)
  if (sortBy.value === 'priority') return priorityRank(left.priority) - priorityRank(right.priority) || dueDateRank(left) - dueDateRank(right)
  if (sortBy.value === 'dueDate') return dueDateRank(left) - dueDateRank(right) || priorityRank(left.priority) - priorityRank(right.priority)
  return riskRank(left) - riskRank(right) || dueDateRank(left) - dueDateRank(right) || priorityRank(left.priority) - priorityRank(right.priority)
}

function statusRank(status: string) {
  return statusOptions.indexOf(status)
}

function priorityRank(priority: string) {
  const ranking: Record<string, number> = { Critical: 0, High: 1, Medium: 2, Low: 3 }
  return ranking[priority] ?? 99
}

function riskRank(task: HubTask) {
  let score = 0
  if (isTaskOverdue(task)) score -= 100
  if (isDueSoon(task)) score -= 40
  if (task.isPinned) score -= 15
  if (['High', 'Critical'].includes(task.priority)) score -= 10
  if (task.status === 'OnHold') score -= 6
  if (task.status === 'Done') score += 20
  return score
}

function priorityPressure(task: HubTask) {
  const ranking: Record<string, number> = { Critical: 100, High: 72, Medium: 38, Low: 16 }
  return ranking[task.priority] ?? 24
}

function freshnessPressure(task: HubTask) {
  const attention = attentionItemMap.value.get(task.id)
  let score = 0
  if (task.status === 'Todo' && (attention?.isStaleTodo ?? false)) score += 100
  if (task.status === 'InProgress' && (attention?.isStaleInProgress ?? false)) score += 100
  if (attention?.isUnseenByAssignee) score += 42
  if (!task.assigneeId) score += 18
  if (task.isPinned) score -= 4
  return Math.max(0, score)
}

function taskRiskReasons(task: HubTask) {
  const attention = attentionItemMap.value.get(task.id)
  const reasons: Array<'deadline' | 'blocker' | 'priority' | 'ownership' | 'stale' | 'unseen'> = []

  if (isTaskOverdue(task) || isDueSoon(task)) reasons.push('deadline')
  if (['Blocked', 'OnHold'].includes(task.status)) reasons.push('blocker')
  if (['High', 'Critical'].includes(task.priority)) reasons.push('priority')
  if (!task.assigneeId) reasons.push('ownership')
  if (attention?.isStaleTodo || attention?.isStaleInProgress) reasons.push('stale')
  if (attention?.isUnseenByAssignee) reasons.push('unseen')

  return reasons
}

function taskRiskScore(task: HubTask) {
  const attention = attentionItemMap.value.get(task.id)
  let score = 0

  if (isTaskOverdue(task)) score += 100
  else if (isDueSoon(task)) score += 42
  if (['Blocked', 'OnHold'].includes(task.status)) score += 28
  if (['Critical', 'High'].includes(task.priority)) score += task.priority === 'Critical' ? 18 : 10
  if (!task.assigneeId) score += 12
  if (task.status === 'Todo' && (attention?.isStaleTodo ?? false)) score += 14
  if (task.status === 'InProgress' && (attention?.isStaleInProgress ?? false)) score += 14
  if (attention?.isUnseenByAssignee) score += 8
  if (task.isPinned) score += 5
  if (task.status === 'Done' || task.status === 'Cancelled') score = Math.max(0, score - 30)

  return score
}

function taskRiskBucket(task: HubTask): TriageMode {
  const score = taskRiskScore(task)
  if (score >= 80) return 'urgent'
  if (score >= 45) return 'atRisk'
  if (score >= 20) return 'watchlist'
  return 'stable'
}

function nextBestAction(task: HubTask) {
  const attention = attentionItemMap.value.get(task.id)
  if (isTaskOverdue(task)) return 'Ưu tiên xử lý ngay'
  if (['Blocked', 'OnHold'].includes(task.status)) return 'Gỡ blocker / làm rõ phụ thuộc'
  if (!task.assigneeId) return 'Gán người phụ trách'
  if (attention?.isStaleInProgress || attention?.isStaleTodo) return 'Nhắc cập nhật tiến độ'
  if (isDueSoon(task)) return 'Đẩy lên đầu danh sách'
  if (['High', 'Critical'].includes(task.priority)) return 'Theo dõi sát'
  return 'Giữ trong watchlist'
}

function dueDateRank(task: HubTask) {
  const raw = task.dueDate ? new Date(task.dueDate).getTime() : Number.POSITIVE_INFINITY
  return Number.isNaN(raw) ? Number.POSITIVE_INFINITY : raw
}

function isDueSoon(task: HubTask) {
  if (!task.dueDate || isTaskOverdue(task) || task.status === 'Done' || task.status === 'Cancelled') return false
  const due = new Date(task.dueDate).getTime()
  const now = Date.now()
  return due > now && due - now <= 1000 * 60 * 60 * 48
}

function matchesFocus(task: HubTask, focus: TaskFocus) {
  if (focus === 'overdue') return isTaskOverdue(task)
  if (focus === 'dueSoon') return isDueSoon(task)
  if (focus === 'pinned') return task.isPinned
  if (focus === 'high') return ['High', 'Critical'].includes(task.priority)
  if (focus === 'blocked') return ['Blocked', 'OnHold'].includes(task.status)
  return true
}

function selectScope(scope: TaskScope) {
  taskScope.value = scope
  selectedTaskIds.value = []
}

function toggleSelectTask(taskId: string) {
  if (selectedTaskIds.value.includes(taskId)) {
    selectedTaskIds.value = selectedTaskIds.value.filter((id) => id !== taskId)
    return
  }

  selectedTaskIds.value = [...selectedTaskIds.value, taskId]
}

function toggleSelectAllVisible() {
  if (isAllVisibleSelected.value) {
    const visibleIds = new Set(filteredTasks.value.map((task) => task.id))
    selectedTaskIds.value = selectedTaskIds.value.filter((id) => !visibleIds.has(id))
    return
  }

  const visibleIds = filteredTasks.value.map((task) => task.id)
  selectedTaskIds.value = [...new Set([...selectedTaskIds.value, ...visibleIds])]
}

function clearFilters() {
  searchQuery.value = ''
  statusFilter.value = 'all'
  priorityFilter.value = 'all'
  projectFilter.value = 'all'
  focusFilter.value = 'all'
  triageMode.value = 'all'
  sortBy.value = 'risk'
}

function clearSelection() {
  selectedTaskIds.value = []
}

function resetDefaultView() {
  selectScope('mine')
  clearFilters()
  clearSelection()
  activeSavedViewId.value = null
}

function loadSavedViews() {
  try {
    const raw = window.localStorage.getItem(savedViewsStorageKey)
    if (!raw) {
      savedViews.value = []
      return
    }

    const parsed = JSON.parse(raw) as SavedTaskView[]
    savedViews.value = Array.isArray(parsed) ? parsed.slice(0, 8) : []
  } catch {
    savedViews.value = []
  }
}

function persistSavedViews() {
  window.localStorage.setItem(savedViewsStorageKey, JSON.stringify(savedViews.value.slice(0, 8)))
}

function captureCurrentView(name: string): SavedTaskView {
  return {
    id: crypto.randomUUID(),
    name,
    scope: taskScope.value,
    searchQuery: searchQuery.value.trim(),
    statusFilter: statusFilter.value,
    priorityFilter: priorityFilter.value,
    projectFilter: projectFilter.value,
    focusFilter: focusFilter.value,
    triageMode: triageMode.value,
    sortBy: sortBy.value,
  }
}

function saveCurrentView() {
  const name = window.prompt('Đặt tên cho saved view này', 'Việc cần làm hôm nay')?.trim()
  if (!name) return

  const nextView = captureCurrentView(name)
  savedViews.value = [nextView, ...savedViews.value.filter((view) => view.name !== name)].slice(0, 8)
  activeSavedViewId.value = nextView.id
  persistSavedViews()
}

function applySavedView(view: SavedTaskView) {
  taskScope.value = view.scope
  searchQuery.value = view.searchQuery
  statusFilter.value = view.statusFilter
  priorityFilter.value = view.priorityFilter
  projectFilter.value = view.projectFilter
  focusFilter.value = view.focusFilter
  triageMode.value = view.triageMode ?? 'all'
  sortBy.value = view.sortBy
  activeSavedViewId.value = view.id
}

function removeSavedView(viewId: string) {
  savedViews.value = savedViews.value.filter((view) => view.id !== viewId)
  if (activeSavedViewId.value === viewId) {
    activeSavedViewId.value = null
  }
  persistSavedViews()
}

function isSavedViewActive(view: SavedTaskView) {
  return currentViewSignature.value === JSON.stringify({
    scope: view.scope,
    searchQuery: view.searchQuery.trim(),
    statusFilter: view.statusFilter,
    priorityFilter: view.priorityFilter,
    projectFilter: view.projectFilter,
    focusFilter: view.focusFilter,
    triageMode: view.triageMode ?? 'all',
    sortBy: view.sortBy,
  })
}

function describeSavedViewStatus(status: string) {
  return status === 'all' ? 'Mọi trạng thái' : displayStatus(status)
}

async function refreshDashboard() {
  try {
    await loadDashboard()
  } catch (error) {
    console.warn(error)
  }
}

async function refreshAttentionInbox() {
  attentionLoading.value = true
  try {
    const result = await apiResult<PagedResult<TaskAttentionDto>>('/api/tasks/attention?page=1&pageSize=8&sort=risk')
    attentionItems.value = result.items ?? []
  } catch (error) {
    attentionItems.value = []
  } finally {
    attentionLoading.value = false
  }
}

async function refreshAll() {
  await Promise.all([refreshDashboard(), refreshAttentionInbox()])
  if (selectedTaskId.value) {
    await loadTaskDetail(selectedTaskId.value)
  }
}

async function loadTaskDetail(taskId: string) {
  selectedTaskLoading.value = true
  try {
    const [detail, comments, attachments, timeEntries, meetingSource] = await Promise.all([
      apiResult<TaskItemDto>(`/api/tasks/${taskId}`),
      apiResult<CommentDto[]>(`/api/comments/task/${taskId}`),
      apiResult<AttachmentDto[]>(`/api/attachments/task/${taskId}`),
      apiJson<TimeEntryDto[]>(`/api/tasks/${taskId}/time-entries`),
      apiResult<TaskMeetingSourceDto>(`/api/tasks/${taskId}/meeting-source`).catch(() => null),
    ])

    selectedTaskDetail.value = detail
    selectedTaskComments.value = comments ?? []
    selectedTaskAttachments.value = attachments ?? []
    selectedTaskTimeEntries.value = timeEntries ?? []
    selectedTaskMeetingSource.value = meetingSource
  } catch (error) {
    selectedTaskDetail.value = null
    selectedTaskComments.value = []
    selectedTaskAttachments.value = []
    selectedTaskTimeEntries.value = []
    selectedTaskMeetingSource.value = null
    showError(errorMessage(error, 'Không thể tải chi tiết nhiệm vụ.'))
  } finally {
    selectedTaskLoading.value = false
  }
}

function resetTaskDrawer() {
  selectedTaskDetail.value = null
  selectedTaskComments.value = []
  selectedTaskAttachments.value = []
  selectedTaskTimeEntries.value = []
  selectedTaskMeetingSource.value = null
  selectedTaskLoading.value = false
}

function openTaskDrawer(taskId: string) {
  selectedTaskId.value = taskId
}

function closeTaskDrawer() {
  selectedTaskId.value = null
}

function taskActionLabel(status: string) {
  return displayStatus(status)
}

async function updateSingleTaskStatus(task: Pick<DashboardTask, 'id'>, status: string) {
  try {
    await apiCommand('/api/tasks/batch-status', {
      method: 'POST',
      body: JSON.stringify({ ids: [task.id], status }),
    })
    showSuccess(`Đã chuyển sang ${displayStatus(status)}`)
    await refreshAll()
  } catch (error) {
    showError(errorMessage(error, 'Không thể đổi trạng thái task.'))
  }
}

async function bulkUpdateStatus(status: string) {
  if (selectedTaskIds.value.length === 0) return
  try {
    await apiCommand('/api/tasks/batch-status', {
      method: 'POST',
      body: JSON.stringify({ ids: selectedTaskIds.value, status }),
    })
    showSuccess(`Đã đổi ${selectedTaskIds.value.length} task sang ${displayStatus(status)}`)
    clearSelection()
    await refreshAll()
  } catch (error) {
    showError(errorMessage(error, 'Không thể cập nhật hàng loạt.'))
  }
}

async function bulkDeleteTasks() {
  if (selectedTaskIds.value.length === 0) return
  if (!confirm(`Xóa ${selectedTaskIds.value.length} task đã chọn?`)) return
  try {
    await apiCommand('/api/tasks/batch-delete', {
      method: 'POST',
      body: JSON.stringify({ ids: selectedTaskIds.value }),
    })
    showSuccess(`Đã xóa ${selectedTaskIds.value.length} task`)
    clearSelection()
    await refreshAll()
  } catch (error) {
    showError(errorMessage(error, 'Không thể xóa hàng loạt.'))
  }
}

async function nudgeTask(task: HubTask | TaskAttentionDto) {
  try {
    await apiCommand(`/api/projects/${task.projectId}/tasks/${task.id}/nudge`, {
      method: 'POST',
    })
    showSuccess('Đã gửi nhắc việc')
    await refreshAttentionInbox()
  } catch (error) {
    showError(errorMessage(error, 'Không thể nhắc người phụ trách.'))
  }
}

function openProjectTask(task: HubTask | TaskAttentionDto) {
  openTask(task.projectId, task.id)
}

function openDetailAndFocus(task: HubTask | TaskAttentionDto) {
  openTaskDrawer(task.id)
}

function humanizeAttentionReason(value: string) {
  const labels: Record<string, string> = {
    QuaHan: 'Quá hạn',
    SapToiHan: 'Sắp đến hạn',
    ChuaBatDau: 'Chưa bắt đầu',
    DangLamQuaLau: 'Đang làm quá lâu',
    ChuaXem: 'Chưa được xem',
  }

  return labels[value] ?? value
}

function humanizeRiskReason(value: string) {
  const labels: Record<string, string> = {
    deadline: 'Deadline',
    blocker: 'Blocker',
    priority: 'Priority',
    ownership: 'Chưa giao',
    stale: 'Stale',
    unseen: 'Chưa xem',
  }

  return labels[value] ?? value
}

function humanizeAllowedAction(value: string) {
  const labels: Record<string, string> = {
    MoChiTiet: 'Mở chi tiết',
    BinhLuan: 'Bình luận',
    NhacNguoiPhuTrach: 'Nhắc',
    BatDauLam: 'Bắt đầu',
  }

  return labels[value] ?? value
}

function formatMeetingDate(value: string | null) {
  return value ? formatTime(value) : 'Chưa rõ'
}

function totalTrackedMinutes(entries: TimeEntryDto[]) {
  return entries.reduce((total, entry) => total + (entry.totalMinutes || 0), 0)
}

function formatMinutes(total: number) {
  if (!total) return '0m'
  const hours = Math.floor(total / 60)
  const minutes = total % 60
  return hours > 0 ? `${hours}h ${minutes}m` : `${minutes}m`
}

function projectLabel(projectId: string) {
  const project = projects.value.find((item: DashboardProject) => item.id === projectId)
  return project ? `${project.name} · ${project.code}` : 'Unknown project'
}

function showQuickStatusButtons(task: HubTask) {
  return nextStatuses(task.status).slice(0, 3)
}

function canNudge(task: HubTask | TaskAttentionDto) {
  const current = currentUser.value
  if (!current) return false
  return task.assigneeId != null && task.assigneeId !== current.id
}

function attentionDotClass(item: TaskAttentionDto) {
  if (item.isOverdue) return 'is-overdue'
  if (item.isDueSoon) return 'is-due-soon'
  if (item.isStaleInProgress || item.isStaleTodo) return 'is-stale'
  return 'is-normal'
}
</script>

<template>
  <div class="task-hub-page dashboard-scroll dashboard-scroll--embedded no-scrollbar">
    <div class="dashboard-main task-hub-main no-scrollbar">
      <section class="task-hub-hero glass-card reveal">
        <div class="task-hub-hero__copy">
          <span>Trung tâm nhiệm vụ</span>
          <h2>Trung tâm xử lý nhiệm vụ của bạn</h2>
          <p>
            Theo dõi task, tín hiệu rủi ro, thao tác nhanh và xử lý hàng loạt ngay trong một màn hình.
          </p>
        </div>

        <div class="task-hub-hero__actions">
          <button class="task-chip" type="button" :class="{ 'is-active': taskScope === 'mine' }" @click="selectScope('mine')">
            Của tôi
          </button>
          <button class="task-chip" type="button" :class="{ 'is-active': taskScope === 'all' }" @click="selectScope('all')">
            Tất cả
          </button>
          <button class="task-chip" type="button" @click="refreshAll">
            <RefreshCw :size="15" />
            Làm mới
          </button>
        </div>
      </section>

      <section class="task-panel glass-card reveal delay-1">
        <div class="panel-heading task-section-heading">
          <div>
            <span>Saved views</span>
            <h2>Bộ lọc dùng nhanh</h2>
          </div>
          <div class="saved-views-actions">
            <button class="secondary-button" type="button" @click="resetDefaultView">
              <SquareCheckBig :size="16" />
              <span>Default view</span>
            </button>
            <button class="secondary-button" type="button" @click="saveCurrentView">
              <Pin :size="16" />
              <span>Lưu view</span>
            </button>
          </div>
        </div>

        <div v-if="savedViews.length" class="saved-views-list">
          <article
            v-for="view in savedViews"
            :key="view.id"
            class="saved-view-pill"
            :class="{ 'is-active': isSavedViewActive(view) }"
            @click="applySavedView(view)"
          >
            <strong>{{ view.name }}</strong>
            <span>{{ view.scope === 'mine' ? 'Của tôi' : 'Tất cả' }} · {{ describeSavedViewStatus(view.statusFilter) }}</span>
            <small>{{ view.sortBy }}</small>
            <button type="button" class="saved-view-pill__remove" aria-label="Xóa saved view" @click.stop="removeSavedView(view.id)">×</button>
          </article>
        </div>
        <div v-else class="saved-views-empty">
          Chưa có view nào được lưu. Hãy lọc xong rồi bấm “Lưu view”.
        </div>
      </section>

      <section class="task-hub-metrics">
        <article class="task-metric glass-card reveal delay-1">
          <span>Tổng task</span>
          <strong>{{ taskSummary.total }}</strong>
          <small>{{ taskScope === 'mine' ? 'Những task đang liên quan trực tiếp tới bạn' : 'Task bạn có thể truy cập' }}</small>
        </article>
        <article class="task-metric glass-card reveal delay-2">
          <span>Quá hạn</span>
          <strong>{{ taskSummary.overdue }}</strong>
          <small>{{ attentionSummary.overdue }} tín hiệu trong inbox</small>
        </article>
        <article class="task-metric glass-card reveal delay-3">
          <span>Sắp đến hạn</span>
          <strong>{{ taskSummary.dueSoon }}</strong>
          <small>{{ attentionSummary.dueSoon }} task cần chú ý</small>
        </article>
        <article class="task-metric glass-card reveal delay-4">
          <span>Attention inbox</span>
          <strong>{{ attentionSummary.total }}</strong>
          <small>{{ attentionSummary.stale }} task đang stale, {{ attentionSummary.unseen }} task chưa được xem</small>
        </article>
      </section>

      <section class="task-panel glass-card reveal delay-2">
        <div class="panel-heading task-section-heading">
          <div>
            <span>Theo dõi rủi ro nhiệm vụ</span>
            <h2>Auto triage thông minh</h2>
          </div>
          <div class="task-section-heading__meta">
            <span>{{ triageBuckets.find((bucket) => bucket.key === 'urgent')?.count ?? 0 }} urgent</span>
            <span>{{ triageBuckets.find((bucket) => bucket.key === 'atRisk')?.count ?? 0 }} at risk</span>
          </div>
        </div>

        <div class="risk-radar-shell">
          <div class="risk-radar-axes">
            <article v-for="axis in riskRadar" :key="axis.id" class="risk-radar-axis" :class="{ 'is-active': axis.active }">
              <div class="risk-radar-axis__head">
                <div>
                  <strong>{{ axis.label }}</strong>
                  <span>{{ axis.caption }}</span>
                </div>
                <small>{{ axis.score }}%</small>
              </div>
              <div class="risk-radar-axis__track">
                <span :style="{ width: `${Math.min(100, axis.score)}%` }"></span>
              </div>
            </article>
          </div>

          <div class="risk-buckets">
            <article
              v-for="bucket in triageBuckets"
              :key="bucket.key"
              class="risk-bucket"
              :class="[`is-${bucket.tone}`, { 'is-active': triageMode === bucket.key }]"
            >
              <div class="risk-bucket__head">
                <strong>{{ bucket.label }}</strong>
                <span>{{ bucket.count }} task</span>
              </div>
              <p>{{ bucket.description }}</p>
              <button class="task-chip" type="button" @click="triageMode = bucket.key">
                Xem {{ bucket.label }}
              </button>
            </article>
          </div>

          <div class="risk-toplist">
            <div class="risk-toplist__head">
              <div>
                <span>Ưu tiên xử lý</span>
                <h3>Top task rủi ro nhất</h3>
              </div>
              <button class="task-chip task-chip--ghost" type="button" @click="triageMode = 'all'">
                Reset triage
              </button>
            </div>

            <article v-for="task in topRiskTasks" :key="task.id" class="risk-top-card">
              <div class="risk-top-card__score">{{ task.riskScore }}</div>
              <div class="risk-top-card__body">
                <strong>{{ task.title }}</strong>
                <small>{{ task.projectName }} · {{ task.assigneeName || 'Chưa giao' }}</small>
                <div class="risk-top-card__tags">
                  <span>{{ task.riskBucket }}</span>
                  <span v-for="reason in task.riskReasons" :key="`${task.id}-${reason}`">{{ humanizeRiskReason(reason) }}</span>
                </div>
                <p>{{ task.nextAction }}</p>
              </div>
              <button class="icon-pill" type="button" @click="openTaskDrawer(task.id)">
                <ArrowRight :size="15" />
                Mở
              </button>
            </article>
          </div>
        </div>

        <div class="panel-heading task-section-heading">
          <div>
            <span>Attention inbox</span>
            <h2>Việc cần xử lý ngay</h2>
          </div>
          <button class="secondary-button" type="button" @click="refreshAttentionInbox">
            <BellRing :size="16" />
            <span>Tải lại</span>
          </button>
        </div>

        <div v-if="attentionLoading" class="task-empty-state">
          <Loader2 :size="20" class="is-spinning" />
          <strong>Đang tải tín hiệu</strong>
          <p>Đang đồng bộ danh sách task cần chú ý từ hệ thống.</p>
        </div>

        <div v-else-if="attentionItems.length === 0" class="task-empty-state">
          <CheckCheck :size="22" />
          <strong>Không có tín hiệu khẩn</strong>
          <p>Tất cả task đang ở trạng thái tương đối ổn định.</p>
        </div>

        <div v-else class="attention-grid">
          <article
            v-for="item in attentionItems"
            :key="item.id"
            class="attention-card"
            :class="{ 'is-overdue': item.isOverdue, 'is-due-soon': item.isDueSoon }"
          >
            <div class="attention-card__header">
              <div class="attention-card__dot" :class="attentionDotClass(item)"></div>
              <div class="attention-card__title">
                <strong>{{ item.title }}</strong>
                <small>{{ item.projectName }} · {{ item.assigneeName || 'Chưa giao' }}</small>
              </div>
              <span class="attention-card__priority">{{ item.priority }}</span>
            </div>

            <div class="attention-card__badges">
              <span v-if="item.isOverdue">Quá hạn</span>
              <span v-if="item.isDueSoon">Sắp đến hạn</span>
              <span v-if="item.isStaleTodo">Stale TODO</span>
              <span v-if="item.isStaleInProgress">Stale In Progress</span>
              <span v-if="item.isUnseenByAssignee">Chưa xem</span>
            </div>

            <div class="attention-card__reasons">
              <span v-for="reason in item.reasons" :key="reason">{{ humanizeAttentionReason(reason) }}</span>
            </div>

            <div class="attention-card__actions">
              <button class="text-button" type="button" @click="openDetailAndFocus(item)">
                <Circle :size="14" />
                Mở chi tiết
              </button>
              <button
                v-if="item.allowedActions.includes('NhacNguoiPhuTrach') && canNudge(item)"
                class="text-button"
                type="button"
                @click="nudgeTask(item)"
              >
                <Send :size="14" />
                Nhắc việc
              </button>
              <button
                v-if="item.allowedActions.includes('BatDauLam')"
                class="text-button"
                type="button"
                @click="updateSingleTaskStatus(item, 'InProgress')"
              >
                <Waypoints :size="14" />
                Bắt đầu
              </button>
            </div>
          </article>
        </div>
      </section>

      <section class="task-panel glass-card reveal delay-3">
        <div class="panel-heading task-section-heading">
          <div>
            <span>Bộ lọc</span>
            <h2>Tìm kiếm, lọc và sắp xếp</h2>
          </div>
          <div class="task-section-heading__meta">
            <span>{{ filteredTasks.length }} task</span>
            <span>{{ selectedCount }} đã chọn</span>
          </div>
        </div>

        <div class="task-toolbar">
          <label class="task-search">
            <Search :size="16" />
            <input v-model="searchQuery" type="search" placeholder="Tìm task, dự án, người giao..." />
          </label>

          <label class="task-select">
            <ListFilter :size="15" />
            <select v-model="projectFilter">
              <option value="all">Tất cả dự án</option>
              <option v-for="project in taskProjectOptions" :key="project.id" :value="project.id">
                {{ project.label }}
              </option>
            </select>
          </label>

          <label class="task-select">
            <Filter :size="15" />
            <select v-model="statusFilter">
              <option value="all">Tất cả trạng thái</option>
              <option v-for="status in statusOptions" :key="status" :value="status">
                {{ displayStatus(status) }}
              </option>
            </select>
          </label>

          <label class="task-select">
            <SquareCheckBig :size="15" />
            <select v-model="priorityFilter">
              <option value="all">Tất cả ưu tiên</option>
              <option v-for="priority in priorityOptions" :key="priority" :value="priority">
                {{ priority }}
              </option>
            </select>
          </label>

          <label class="task-select">
            <BadgeAlert :size="15" />
            <select v-model="focusFilter">
              <option v-for="focus in focusOptions" :key="focus.value" :value="focus.value">
                {{ focus.label }}
              </option>
            </select>
          </label>

          <label class="task-select">
            <Clock3 :size="15" />
            <select v-model="sortBy">
              <option v-for="sort in sortOptions" :key="sort.value" :value="sort.value">
                {{ sort.label }}
              </option>
            </select>
          </label>

          <button class="task-chip task-chip--ghost" type="button" @click="clearFilters">
            Xóa lọc
          </button>
        </div>
      </section>

      <section class="task-panel glass-card reveal delay-4">
        <div class="panel-heading task-section-heading">
          <div>
            <span>Danh sách task</span>
            <h2>Đang hiển thị {{ filteredTasks.length }} nhiệm vụ</h2>
          </div>
          <div class="task-section-heading__meta">
            <label class="task-inline-check">
              <input type="checkbox" :checked="isAllVisibleSelected" @change="toggleSelectAllVisible" />
              <span>Chọn tất cả</span>
            </label>
            <button class="secondary-button" type="button" :disabled="selectedCount === 0" @click="clearSelection">
              <X :size="15" />
              <span>Bỏ chọn</span>
            </button>
          </div>
        </div>

        <div v-if="selectedCount > 0" class="bulk-action-bar">
          <div>
            <strong>{{ selectedCount }} task đã chọn</strong>
            <small>Áp dụng thao tác hàng loạt cho các task đang được đánh dấu.</small>
          </div>
          <div class="bulk-action-bar__actions">
            <button class="task-chip" type="button" @click="bulkUpdateStatus('InProgress')">Sang In Progress</button>
            <button class="task-chip" type="button" @click="bulkUpdateStatus('Done')">Đánh dấu Done</button>
            <button class="task-chip task-chip--danger" type="button" @click="bulkDeleteTasks">
              <Trash2 :size="14" />
              Xóa
            </button>
          </div>
        </div>

        <div v-if="filteredTasks.length === 0" class="task-empty-state task-empty-state--wide">
          <ListFilter :size="24" />
          <strong>Không có task phù hợp</strong>
          <p>Thử mở rộng bộ lọc hoặc đổi sang phạm vi khác.</p>
        </div>

        <div v-else class="task-list">
          <article
            v-for="task in filteredTasks"
            :key="task.id"
            class="task-row"
            :class="{ 'is-selected': selectedTaskIds.includes(task.id), 'is-overdue': isTaskOverdue(task) }"
          >
            <label class="task-row__check">
              <input :checked="selectedTaskIds.includes(task.id)" type="checkbox" @change="toggleSelectTask(task.id)" />
            </label>

            <button class="task-row__content" type="button" @click="openTaskDrawer(task.id)">
              <div class="task-row__headline">
                <strong>{{ task.title }}</strong>
                <div class="task-row__badges">
                  <span class="task-badge task-badge--priority" :class="`is-${task.priority.toLowerCase()}`">{{ task.priority }}</span>
                  <span v-if="task.isPinned" class="task-badge task-badge--pin">
                    <Pin :size="12" />
                    Ghim
                  </span>
                  <span v-if="task.isPrivate" class="task-badge task-badge--private">Riêng tư</span>
                  <span v-if="isTaskOverdue(task)" class="task-badge task-badge--danger">Quá hạn</span>
                  <span v-else-if="isDueSoon(task)" class="task-badge task-badge--warning">Sắp đến hạn</span>
                </div>
              </div>

              <div class="task-row__meta">
                <span>{{ task.projectName }} · {{ task.projectCode }}</span>
                <span>{{ task.status }}</span>
                <span>{{ task.assigneeName || 'Chưa giao' }}</span>
                <span>{{ task.reporterName }}</span>
                <span>{{ task.dueDate ? `Hạn ${formatDate(task.dueDate)}` : 'Chưa có hạn' }}</span>
              </div>

              <div class="task-row__signals">
                <span>{{ task.commentCount }} comment</span>
                <span>{{ task.attachmentCount }} file</span>
                <span>{{ task.upvoteCount - task.downvoteCount }} score</span>
              </div>
            </button>

            <div class="task-row__actions">
              <button class="icon-pill" type="button" @click.stop="openProjectTask(task)">
                <ArrowRight :size="15" />
                Mở
              </button>

              <button
                v-for="status in showQuickStatusButtons(task)"
                :key="`${task.id}-${status}`"
                class="icon-pill"
                type="button"
                @click.stop="updateSingleTaskStatus(task, status)"
              >
                <CheckSquare2 :size="14" />
                {{ taskActionLabel(status) }}
              </button>

              <button
                v-if="canNudge(task)"
                class="icon-pill icon-pill--ghost"
                type="button"
                @click.stop="nudgeTask(task)"
              >
                <Send :size="14" />
                Nhắc
              </button>
            </div>
          </article>
        </div>
      </section>
    </div>

    <aside class="task-detail-drawer glass-card" :class="{ 'is-open': Boolean(selectedTaskId) }">
      <div v-if="selectedTaskId" class="task-detail">
        <div class="task-detail__header">
          <div>
            <span>Chi tiết task</span>
            <h2>{{ selectedTaskSummary?.title || 'Đang tải...' }}</h2>
          </div>
          <button class="icon-pill icon-pill--ghost" type="button" @click="closeTaskDrawer">
            <X :size="16" />
          </button>
        </div>

        <div v-if="selectedTaskLoading" class="task-empty-state task-empty-state--compact">
          <Loader2 :size="20" class="is-spinning" />
          <strong>Đang tải chi tiết</strong>
        </div>

        <template v-else>
          <section v-if="selectedTaskDetail" class="task-detail__summary">
            <div class="task-detail__title-row">
              <span class="task-detail__project">{{ selectedTaskDetail.projectName }}</span>
              <span class="task-detail__status">{{ displayStatus(selectedTaskDetail.status) }}</span>
            </div>

            <p class="task-detail__description">
              {{ selectedTaskDetail.description || 'Task này chưa có mô tả chi tiết.' }}
            </p>

            <div class="task-detail__facts">
              <div>
                <span>Priority</span>
                <strong>{{ selectedTaskDetail.priority }}</strong>
              </div>
              <div>
                <span>Assignee</span>
                <strong>{{ selectedTaskDetail.assigneeName || 'Chưa giao' }}</strong>
              </div>
              <div>
                <span>Reporter</span>
                <strong>{{ selectedTaskDetail.reporterName }}</strong>
              </div>
              <div>
                <span>Due date</span>
                <strong>{{ selectedTaskDetail.dueDate ? formatDate(selectedTaskDetail.dueDate) : 'Chưa có' }}</strong>
              </div>
            </div>

            <div v-if="selectedTaskMeetingSource" class="task-detail__meeting">
              <span>Nguồn cuộc họp</span>
              <strong>{{ selectedTaskMeetingSource.meetingTitle }}</strong>
              <p>{{ selectedTaskMeetingSource.actionItemTitle || selectedTaskMeetingSource.actionItemDescription || selectedTaskMeetingSource.sourceQuote || 'Không có trích dẫn bổ sung.' }}</p>
              <small>{{ formatMeetingDate(selectedTaskMeetingSource.meetingStartedAt) }}</small>
            </div>

            <div class="task-detail__action-row">
              <button
                v-for="status in showQuickStatusButtons({ ...selectedTaskSummary, ...selectedTaskDetail } as HubTask)"
                :key="status"
                class="task-chip"
                type="button"
                @click="updateSingleTaskStatus({ ...selectedTaskSummary, ...selectedTaskDetail } as HubTask, status)"
              >
                {{ taskActionLabel(status) }}
              </button>
              <button
                v-if="canNudge({ ...selectedTaskSummary, ...selectedTaskDetail } as HubTask)"
                class="task-chip"
                type="button"
                @click="nudgeTask({ ...selectedTaskSummary, ...selectedTaskDetail } as HubTask)"
              >
                <Send :size="14" />
                Nhắc
              </button>
              <button class="task-chip task-chip--ghost" type="button" @click="openProjectTask({ ...selectedTaskSummary, ...selectedTaskDetail } as HubTask)">
                Mở project
              </button>
            </div>
          </section>

          <section class="task-detail__blocks">
            <div class="task-detail-block">
              <div class="task-detail-block__header">
                <h3>Comment</h3>
                <span>{{ selectedTaskComments.length }}</span>
              </div>
              <article v-for="comment in selectedTaskComments.slice(0, 4)" :key="comment.id" class="task-mini-item">
                <strong>{{ comment.authorName }}</strong>
                <p>{{ comment.content }}</p>
                <small>{{ formatTime(comment.createdAt) }}</small>
              </article>
              <div v-if="selectedTaskComments.length === 0" class="task-mini-empty">Chưa có comment.</div>
            </div>

            <div class="task-detail-block">
              <div class="task-detail-block__header">
                <h3>File đính kèm</h3>
                <span>{{ selectedTaskAttachments.length }}</span>
              </div>
              <article v-for="attachment in selectedTaskAttachments.slice(0, 4)" :key="attachment.id" class="task-mini-item">
                <strong>{{ attachment.fileName }}</strong>
                <p>{{ attachment.uploadedByName }} · {{ formatDate(attachment.uploadedAt) }}</p>
              </article>
              <div v-if="selectedTaskAttachments.length === 0" class="task-mini-empty">Chưa có file đính kèm.</div>
            </div>

            <div class="task-detail-block">
              <div class="task-detail-block__header">
                <h3>Time tracking</h3>
                <span>{{ formatMinutes(totalTrackedMinutes(selectedTaskTimeEntries)) }}</span>
              </div>
              <article v-for="entry in selectedTaskTimeEntries.slice(0, 4)" :key="entry.id" class="task-mini-item">
                <strong>{{ entry.userName }}</strong>
                <p>{{ entry.note || 'Không có ghi chú' }}</p>
                <small>{{ formatTime(entry.startedAt) }}</small>
              </article>
              <div v-if="selectedTaskTimeEntries.length === 0" class="task-mini-empty">Chưa ghi nhận thời gian.</div>
            </div>
          </section>
        </template>
      </div>

      <div v-else class="task-detail task-detail--placeholder">
        <Waypoints :size="26" />
        <h3>Chọn một task để xem chi tiết</h3>
        <p>
          Drawer này sẽ hiển thị mô tả, comment, file đính kèm, time tracking và meeting source.
        </p>
        <button class="task-chip task-chip--ghost" type="button" @click="selectScope('mine')">
          Quay về task của tôi
        </button>
      </div>
    </aside>
  </div>
</template>

<style scoped>
.task-hub-page {
  display: grid;
  grid-template-columns: minmax(0, 1fr) 360px;
  gap: 18px;
  align-items: start;
}

.task-hub-main {
  min-width: 0;
  display: grid;
  gap: 16px;
}

.task-hub-hero,
.task-panel,
.task-detail-drawer {
  border-radius: 22px;
}

.task-hub-hero {
  display: flex;
  justify-content: space-between;
  gap: 18px;
  padding: 22px 24px;
  background: linear-gradient(135deg, rgba(15, 82, 186, 0.12), rgba(255, 255, 255, 0.88));
}

.task-hub-hero__copy {
  display: grid;
  gap: 8px;
}

.task-hub-hero__copy span,
.panel-heading span,
.task-detail__header span {
  color: var(--primary);
  font-size: 12px;
  font-weight: 800;
  letter-spacing: 0.08em;
  text-transform: uppercase;
}

.task-hub-hero__copy h2,
.panel-heading h2,
.task-detail__header h2 {
  color: var(--text-strong);
  font-size: clamp(24px, 3vw, 32px);
  font-weight: 850;
}

.task-hub-hero__copy p {
  max-width: 68ch;
  color: var(--muted);
}

.task-hub-hero__actions {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: flex-end;
  gap: 10px;
}

.task-chip {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  min-height: 40px;
  padding: 0 14px;
  border: 1px solid rgba(193, 211, 232, 0.95);
  border-radius: 999px;
  color: var(--primary);
  background: rgba(255, 255, 255, 0.94);
  font-weight: 800;
}

.task-chip.is-active {
  border-color: rgba(31, 128, 255, 0.34);
  color: white;
  background: var(--primary);
}

.task-chip--ghost {
  background: transparent;
}

.task-chip--danger {
  color: #dc2626;
  border-color: rgba(239, 68, 68, 0.24);
  background: rgba(254, 242, 242, 0.96);
}

.task-hub-metrics {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 14px;
}

.task-metric {
  display: grid;
  gap: 6px;
  padding: 18px;
}

.task-metric span {
  color: var(--muted);
  font-size: 12px;
  font-weight: 800;
  text-transform: uppercase;
}

.task-metric strong {
  color: var(--text-strong);
  font-size: 30px;
  line-height: 1;
  font-weight: 900;
}

.task-metric small {
  color: var(--muted);
}

.task-panel {
  padding: 18px;
  display: grid;
  gap: 16px;
}

.risk-radar-shell {
  display: grid;
  grid-template-columns: minmax(0, 1.1fr) minmax(240px, 0.85fr) minmax(280px, 1fr);
  gap: 14px;
}

.risk-radar-axes,
.risk-buckets,
.risk-toplist {
  display: grid;
  gap: 10px;
}

.risk-radar-axes {
  padding: 14px;
  border: 1px solid var(--line);
  border-radius: 18px;
  background: linear-gradient(180deg, rgba(239, 246, 255, 0.72), white);
}

.risk-radar-axis {
  display: grid;
  gap: 8px;
  padding: 12px;
  border: 1px solid transparent;
  border-radius: 14px;
  background: rgba(255, 255, 255, 0.72);
}

.risk-radar-axis.is-active {
  border-color: rgba(15, 76, 255, 0.2);
  background: rgba(239, 246, 255, 0.96);
}

.risk-radar-axis__head {
  display: flex;
  align-items: start;
  justify-content: space-between;
  gap: 12px;
}

.risk-radar-axis__head strong,
.risk-bucket__head strong,
.risk-toplist__head h3 {
  color: var(--text-strong);
  font-size: 14px;
  font-weight: 850;
}

.risk-radar-axis__head span,
.risk-toplist__head span,
.risk-bucket p,
.risk-top-card small,
.risk-top-card p {
  color: var(--muted);
  font-size: 12px;
}

.risk-radar-axis__head small {
  color: var(--primary);
  font-size: 12px;
  font-weight: 900;
}

.risk-radar-axis__track {
  height: 9px;
  overflow: hidden;
  border-radius: 999px;
  background: rgba(148, 163, 184, 0.18);
}

.risk-radar-axis__track span {
  display: block;
  height: 100%;
  border-radius: inherit;
  background: linear-gradient(90deg, #0f4cff, #22d3ee);
}

.risk-buckets {
  grid-template-columns: repeat(2, minmax(0, 1fr));
}

.risk-bucket,
.risk-top-card {
  padding: 14px;
  border: 1px solid var(--line);
  border-radius: 18px;
  background: white;
}

.risk-bucket.is-danger {
  border-color: rgba(239, 68, 68, 0.22);
  background: linear-gradient(180deg, rgba(254, 242, 242, 0.9), white 70%);
}

.risk-bucket.is-warning {
  border-color: rgba(245, 158, 11, 0.22);
  background: linear-gradient(180deg, rgba(255, 251, 235, 0.92), white 70%);
}

.risk-bucket.is-info {
  border-color: rgba(59, 130, 246, 0.2);
  background: linear-gradient(180deg, rgba(239, 246, 255, 0.92), white 70%);
}

.risk-bucket.is-success {
  border-color: rgba(34, 197, 94, 0.2);
  background: linear-gradient(180deg, rgba(240, 253, 244, 0.92), white 70%);
}

.risk-bucket {
  display: grid;
  gap: 10px;
}

.risk-bucket.is-active {
  box-shadow: 0 18px 36px rgba(15, 23, 42, 0.08);
}

.risk-bucket__head {
  display: flex;
  justify-content: space-between;
  gap: 10px;
  align-items: center;
}

.risk-bucket__head span {
  color: var(--primary);
  font-size: 12px;
  font-weight: 900;
}

.risk-toplist {
  padding: 14px;
  border: 1px solid var(--line);
  border-radius: 18px;
  background: var(--bg-soft);
}

.risk-toplist__head {
  display: flex;
  align-items: start;
  justify-content: space-between;
  gap: 12px;
}

.risk-toplist__head span {
  color: var(--primary);
  font-size: 11px;
  font-weight: 800;
  letter-spacing: 0.08em;
  text-transform: uppercase;
}

.risk-toplist__head h3 {
  margin-top: 4px;
  font-size: 16px;
}

.risk-top-card {
  display: grid;
  grid-template-columns: 52px minmax(0, 1fr) auto;
  gap: 12px;
  align-items: center;
}

.risk-top-card__score {
  display: grid;
  place-items: center;
  width: 52px;
  height: 52px;
  border-radius: 16px;
  color: white;
  font-size: 16px;
  font-weight: 900;
  background: linear-gradient(135deg, #0f4cff, #22d3ee);
}

.risk-top-card__body {
  display: grid;
  gap: 5px;
  min-width: 0;
}

.risk-top-card__body strong {
  color: var(--text-strong);
  font-size: 14px;
  font-weight: 850;
}

.risk-top-card__tags {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.risk-top-card__tags span {
  padding: 4px 8px;
  border-radius: 999px;
  color: var(--primary);
  background: rgba(239, 246, 255, 0.92);
  font-size: 11px;
  font-weight: 800;
}

.task-section-heading {
  align-items: start;
}

.saved-views-actions {
  display: inline-flex;
  flex-wrap: wrap;
  gap: 10px;
  justify-content: flex-end;
}

.task-section-heading__meta {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: flex-end;
  gap: 8px;
  color: var(--muted);
  font-weight: 700;
}

.saved-views-list {
  display: flex;
  flex-wrap: wrap;
  gap: 10px;
}

.saved-view-pill {
  position: relative;
  display: grid;
  gap: 4px;
  min-width: 180px;
  padding: 12px 14px;
  border: 1px solid var(--line);
  border-radius: 16px;
  color: var(--text);
  background: var(--panel-soft);
  text-align: left;
  cursor: pointer;
}

.saved-view-pill.is-active {
  border-color: rgba(31, 128, 255, 0.3);
  background: rgba(239, 246, 255, 0.92);
}

.saved-view-pill strong {
  color: var(--text-strong);
  font-size: 13px;
  font-weight: 850;
}

.saved-view-pill span,
.saved-view-pill small {
  color: var(--muted);
  font-size: 11px;
}

.saved-view-pill__remove {
  position: absolute;
  top: 8px;
  right: 8px;
  width: 22px;
  height: 22px;
  border: 0;
  border-radius: 999px;
  color: var(--muted);
  background: white;
  cursor: pointer;
}

.saved-view-pill__remove:hover {
  color: #dc2626;
}

.saved-views-empty {
  padding: 14px 16px;
  border: 1px dashed var(--line);
  border-radius: 16px;
  color: var(--muted);
  background: var(--bg-soft);
}

.task-toolbar {
  display: flex;
  flex-wrap: wrap;
  gap: 10px;
  padding: 14px;
  border: 1px solid var(--line);
  border-radius: 18px;
  background: var(--panel-soft);
}

.task-search,
.task-select {
  min-height: 44px;
  display: inline-flex;
  align-items: center;
  gap: 10px;
  padding: 0 14px;
  border: 1px solid var(--line);
  border-radius: 14px;
  background: white;
  color: var(--muted);
}

.task-search {
  flex: 1 1 320px;
}

.task-search input,
.task-select select {
  width: 100%;
  border: 0;
  outline: 0;
  background: transparent;
  color: var(--text-strong);
}

.task-select {
  flex: 1 1 180px;
}

.task-inline-check {
  display: inline-flex;
  align-items: center;
  gap: 8px;
}

.task-inline-check input {
  accent-color: var(--primary);
}

.bulk-action-bar {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding: 14px 16px;
  border: 1px solid rgba(31, 128, 255, 0.16);
  border-radius: 16px;
  background: rgba(239, 246, 255, 0.84);
}

.bulk-action-bar strong {
  color: var(--text-strong);
}

.bulk-action-bar small {
  color: var(--muted);
}

.bulk-action-bar__actions {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.attention-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 12px;
}

.attention-card {
  padding: 16px;
  border: 1px solid var(--line);
  border-radius: 18px;
  background: var(--panel-soft);
}

.attention-card.is-overdue {
  border-color: rgba(239, 68, 68, 0.24);
  background: rgba(254, 242, 242, 0.72);
}

.attention-card.is-due-soon {
  border-color: rgba(245, 158, 11, 0.22);
  background: rgba(255, 251, 235, 0.78);
}

.attention-card__header {
  display: grid;
  grid-template-columns: 12px minmax(0, 1fr) auto;
  gap: 12px;
  align-items: start;
}

.attention-card__dot {
  width: 12px;
  height: 12px;
  margin-top: 4px;
  border-radius: 50%;
  background: #64748b;
}

.attention-card__dot.is-overdue {
  background: #dc2626;
}

.attention-card__dot.is-due-soon {
  background: #f59e0b;
}

.attention-card__dot.is-stale {
  background: #8b5cf6;
}

.attention-card__title {
  display: grid;
  gap: 4px;
}

.attention-card__title strong {
  color: var(--text-strong);
  font-size: 15px;
  font-weight: 850;
}

.attention-card__title small,
.attention-card__actions,
.task-row__meta,
.task-row__signals,
.task-mini-item small {
  color: var(--muted);
}

.attention-card__priority {
  padding: 5px 10px;
  border-radius: 999px;
  background: rgba(15, 82, 186, 0.1);
  color: var(--primary);
  font-size: 12px;
  font-weight: 800;
}

.attention-card__badges,
.attention-card__reasons,
.task-row__badges,
.task-row__signals {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.attention-card__badges {
  margin-top: 12px;
}

.attention-card__badges span,
.attention-card__reasons span,
.task-badge {
  padding: 5px 9px;
  border-radius: 999px;
  background: rgba(255, 255, 255, 0.84);
  font-size: 11px;
  font-weight: 800;
}

.attention-card__reasons {
  margin-top: 12px;
}

.attention-card__actions {
  display: flex;
  flex-wrap: wrap;
  gap: 10px;
  margin-top: 14px;
}

.text-button {
  display: inline-flex;
  align-items: center;
  gap: 7px;
  border: 0;
  padding: 0;
  color: var(--primary);
  background: transparent;
  font-weight: 800;
}

.task-empty-state {
  min-height: 160px;
  display: grid;
  place-items: center;
  gap: 8px;
  padding: 18px;
  text-align: center;
  border: 1px dashed var(--line);
  border-radius: 18px;
  background: var(--panel-soft);
}

.task-empty-state--wide {
  min-height: 220px;
}

.task-empty-state--compact {
  min-height: 120px;
}

.task-empty-state strong {
  color: var(--text-strong);
  font-size: 15px;
  font-weight: 850;
}

.task-empty-state p {
  color: var(--muted);
}

.task-list {
  display: grid;
  gap: 12px;
}

.task-row {
  display: grid;
  grid-template-columns: auto minmax(0, 1fr) auto;
  gap: 14px;
  align-items: center;
  padding: 16px;
  border: 1px solid var(--line);
  border-radius: 18px;
  background: white;
}

.task-row.is-selected {
  border-color: rgba(31, 128, 255, 0.32);
  box-shadow: 0 14px 34px rgba(15, 23, 42, 0.06);
}

.task-row.is-overdue {
  background: linear-gradient(90deg, rgba(254, 242, 242, 0.9), white 55%);
}

.task-row__check input {
  accent-color: var(--primary);
}

.task-row__content {
  min-width: 0;
  display: grid;
  gap: 8px;
  border: 0;
  padding: 0;
  text-align: left;
  background: transparent;
}

.task-row__headline {
  display: grid;
  gap: 8px;
}

.task-row__headline strong {
  overflow: hidden;
  color: var(--text-strong);
  font-size: 15px;
  font-weight: 850;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.task-badge--priority.is-critical {
  color: #b91c1c;
  background: rgba(254, 226, 226, 0.96);
}

.task-badge--priority.is-high {
  color: #c2410c;
  background: rgba(255, 237, 213, 0.96);
}

.task-badge--priority.is-medium {
  color: #1d4ed8;
  background: rgba(219, 234, 254, 0.96);
}

.task-badge--priority.is-low {
  color: #0f766e;
  background: rgba(204, 251, 241, 0.96);
}

.task-badge--pin {
  color: #7c3aed;
}

.task-badge--private {
  color: #475569;
}

.task-badge--danger {
  color: #b91c1c;
  background: rgba(254, 226, 226, 0.96);
}

.task-badge--warning {
  color: #b45309;
  background: rgba(254, 243, 199, 0.96);
}

.task-row__meta,
.task-row__signals {
  font-size: 12px;
}

.task-row__actions {
  display: flex;
  flex-wrap: wrap;
  justify-content: flex-end;
  gap: 8px;
}

.icon-pill {
  display: inline-flex;
  align-items: center;
  gap: 7px;
  min-height: 36px;
  padding: 0 12px;
  border: 1px solid rgba(193, 211, 232, 0.95);
  border-radius: 999px;
  color: var(--primary);
  background: rgba(239, 246, 255, 0.84);
  font-size: 12px;
  font-weight: 800;
}

.icon-pill--ghost {
  background: white;
}

.task-detail-drawer {
  position: sticky;
  top: 16px;
  min-width: 0;
  padding: 18px;
}

.task-detail {
  display: grid;
  gap: 16px;
}

.task-detail__header {
  display: flex;
  align-items: start;
  justify-content: space-between;
  gap: 12px;
}

.task-detail__summary {
  display: grid;
  gap: 14px;
  padding: 16px;
  border: 1px solid var(--line);
  border-radius: 18px;
  background: var(--panel-soft);
}

.task-detail__title-row {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  align-items: center;
}

.task-detail__project,
.task-detail__status {
  padding: 6px 10px;
  border-radius: 999px;
  font-size: 12px;
  font-weight: 800;
}

.task-detail__project {
  color: var(--primary);
  background: rgba(239, 246, 255, 0.9);
}

.task-detail__status {
  color: #0f172a;
  background: rgba(226, 232, 240, 0.92);
}

.task-detail__description {
  color: var(--text);
}

.task-detail__facts {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 10px;
}

.task-detail__facts div,
.task-detail__meeting {
  padding: 12px;
  border: 1px solid var(--line);
  border-radius: 14px;
  background: white;
}

.task-detail__facts span,
.task-detail__meeting span {
  display: block;
  color: var(--muted);
  font-size: 11px;
  font-weight: 800;
  text-transform: uppercase;
}

.task-detail__facts strong,
.task-detail__meeting strong {
  color: var(--text-strong);
  font-size: 13px;
}

.task-detail__meeting p {
  margin-top: 6px;
}

.task-detail__meeting small {
  color: var(--muted);
}

.task-detail__action-row {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.task-detail__blocks {
  display: grid;
  gap: 12px;
}

.task-detail-block {
  display: grid;
  gap: 10px;
  padding: 14px;
  border: 1px solid var(--line);
  border-radius: 18px;
  background: white;
}

.task-detail-block__header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
}

.task-detail-block__header h3 {
  color: var(--text-strong);
  font-size: 15px;
  font-weight: 850;
}

.task-detail-block__header span {
  color: var(--muted);
  font-size: 12px;
  font-weight: 800;
}

.task-mini-item {
  display: grid;
  gap: 4px;
  padding: 10px 12px;
  border-radius: 14px;
  background: var(--panel-soft);
}

.task-mini-item strong {
  color: var(--text-strong);
  font-size: 13px;
}

.task-mini-item p,
.task-mini-empty {
  color: var(--muted);
  font-size: 12px;
}

.task-mini-empty {
  padding: 8px 2px;
}

.task-detail--placeholder {
  min-height: calc(100dvh - 110px);
  align-content: center;
  justify-items: center;
  text-align: center;
}

.task-detail--placeholder h3 {
  color: var(--text-strong);
  font-size: 18px;
  font-weight: 850;
}

.is-spinning {
  animation: spin 1s linear infinite;
}

@keyframes spin {
  to {
    transform: rotate(360deg);
  }
}

@media (max-width: 1280px) {
  .task-hub-page {
    grid-template-columns: minmax(0, 1fr);
  }

  .task-detail-drawer {
    position: static;
  }

  .risk-radar-shell {
    grid-template-columns: minmax(0, 1fr);
  }
}

@media (max-width: 960px) {
  .task-hub-metrics,
  .attention-grid,
  .task-detail__facts,
  .risk-buckets {
    grid-template-columns: 1fr;
  }

  .task-row {
    grid-template-columns: 1fr;
  }

  .task-row__actions {
    justify-content: flex-start;
  }

  .risk-top-card {
    grid-template-columns: 1fr;
  }

  .risk-top-card__score {
    width: 100%;
    height: 42px;
    border-radius: 14px;
  }
}

@media (max-width: 720px) {
  .task-hub-hero {
    flex-direction: column;
  }

  .task-toolbar {
    padding: 12px;
  }

  .task-search,
  .task-select {
    flex: 1 1 100%;
  }

  .task-section-heading__meta {
    justify-content: flex-start;
  }
}
</style>
