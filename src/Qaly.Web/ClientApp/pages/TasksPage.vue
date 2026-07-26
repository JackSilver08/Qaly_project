<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import {
  ArrowRight,
  BadgeAlert,
  Circle,
  CheckCheck,
  CheckSquare2,
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
  X,
} from 'lucide-vue-next'
import { useDashboardContext } from '../composables/dashboard-context'
import { apiCommand, apiResult, errorMessage } from '../utils/api-client'
import { showError, showSuccess } from '../composables/use-toast'
import { confirmDialog } from '../composables/use-confirm-dialog'
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
type WorkflowStage = 'needsOwner' | 'todo' | 'inProgress' | 'inReview' | 'blocked' | 'done'
type WorkflowMiniStage = 'todo' | 'inProgress' | 'inReview' | 'done'
type SavedTaskView = {
  id: string
  name: string
  scope: TaskScope
  searchQuery: string
  statusFilter: string
  priorityFilter: string
  projectFilter: string
  focusFilter: TaskFocus
  sortBy: TaskSort
  signature: string
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

type TaskSummary = HubTask | TaskAttentionDto
type TaskDisplay = TaskItemDto | TaskSummary

type WorkflowTask = {
  id: string
  status: string
  assigneeId: string | null
  dueDate: string | null
  priority: string
  isPinned?: boolean
  assigneeName?: string | null
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

const router = useRouter()

const taskScope = ref<TaskScope>('all')
const searchQuery = ref('')
const statusFilter = ref<string>('all')
const priorityFilter = ref<string>('all')
const projectFilter = ref<string>('all')
const focusFilter = ref<TaskFocus>('all')
const sortBy = ref<TaskSort>('risk')
const savedTaskViews = ref<SavedTaskView[]>([])
const savedTaskViewsStorageKey = 'qaly.tasks.saved-views.v1'

const attentionItems = ref<TaskAttentionDto[]>([])
const attentionLoading = ref(false)

const selectedTaskId = ref<string | null>(null)
const selectedTaskDetail = ref<TaskItemDto | null>(null)
const selectedTaskComments = ref<CommentDto[]>([])
const selectedTaskAttachments = ref<AttachmentDto[]>([])
const selectedTaskTimeEntries = ref<TimeEntryDto[]>([])
const selectedTaskMeetingSource = ref<TaskMeetingSourceDto | null>(null)
const selectedTaskLoading = ref(false)
const taskDetailCache = ref(new Map<string, TaskItemDto>())

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

const workflowMiniSteps: Array<{
  key: WorkflowMiniStage
  label: string
  status: string
  hint: string
}> = [
  { key: 'todo', label: 'Todo', status: 'Todo', hint: 'Chưa bắt đầu' },
  { key: 'inProgress', label: 'In Progress', status: 'InProgress', hint: 'Đang triển khai' },
  { key: 'inReview', label: 'In Review', status: 'InReview', hint: 'Chờ duyệt' },
  { key: 'done', label: 'Done', status: 'Done', hint: 'Hoàn tất' },
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

const shouldFallbackToAll = computed(
  () => taskScope.value === 'mine' && mineTasks.value.length === 0 && allTasks.value.length > 0,
)

const attentionItemMap = computed(() =>
  new Map(attentionItems.value.map((item) => [item.id, item])),
)

const currentTaskViewSignature = computed(() => buildTaskViewSignature({
  scope: taskScope.value,
  searchQuery: searchQuery.value,
  statusFilter: statusFilter.value,
  priorityFilter: priorityFilter.value,
  projectFilter: projectFilter.value,
  focusFilter: focusFilter.value,
  sortBy: sortBy.value,
}))

const savedTaskViewLookup = computed(() => {
  const map = new Map<string, SavedTaskView>()
  for (const view of savedTaskViews.value) {
    map.set(view.signature, view)
  }
  return map
})

const activeSavedTaskView = computed(() => savedTaskViewLookup.value.get(currentTaskViewSignature.value) ?? null)

const filteredTasks = computed(() => {
  const query = searchQuery.value.trim().toLowerCase()

  return visibleTaskBase.value
    .filter((task) => {
      if (projectFilter.value !== 'all' && task.projectId !== projectFilter.value) return false
      if (statusFilter.value !== 'all' && task.status !== statusFilter.value) return false
      if (priorityFilter.value !== 'all' && task.priority !== priorityFilter.value) return false
      if (focusFilter.value !== 'all' && !matchesFocus(task, focusFilter.value)) return false
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

const taskSummary = computed(() => {
  const tasks = visibleTaskBase.value
  return {
    total: tasks.length,
    overdue: tasks.filter((task) => isTaskOverdue(task)).length,
    dueSoon: tasks.filter((task) => isDueSoon(task)).length,
  }
})

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

const selectedTaskDisplay = computed<TaskDisplay | null>(() => selectedTaskDetail.value ?? selectedTaskSummary.value)

const selectedTaskProjectLine = computed(() => {
  const task = selectedTaskDisplay.value
  if (!task) return ''

  return 'projectCode' in task ? `${task.projectName} · ${task.projectCode}` : task.projectName
})

const selectedTaskDescription = computed(() => {
  const task = selectedTaskDisplay.value
  if (task && 'description' in task && task.description) {
    return task.description
  }

  return 'Task này chưa có mô tả chi tiết.'
})

type SelectedWorkflowTask = {
  id: string
  status: string
  assigneeId: string | null
  dueDate: string | null
  priority: string
  isPinned: boolean
  assigneeName: string | null
}

const selectedWorkflowTask = computed<SelectedWorkflowTask | null>(() => {
  const detail = selectedTaskDetail.value
  const summary = selectedTaskSummary.value

  if (!detail && !summary) return null

  return {
    id: detail?.id ?? summary!.id,
    status: detail?.status ?? summary!.status,
    assigneeId: detail?.assigneeId ?? summary!.assigneeId ?? null,
    dueDate: detail?.dueDate ?? summary!.dueDate ?? null,
    priority: detail?.priority ?? summary!.priority,
    isPinned: summary && 'isPinned' in summary ? summary.isPinned : false,
    assigneeName: detail?.assigneeName ?? summary?.assigneeName ?? null,
  }
})

const selectedWorkflowStage = computed<WorkflowStage | null>(() => {
  const task = selectedWorkflowTask.value
  if (!task) return null
  return deriveWorkflowStage(task)
})

const selectedWorkflowIsUrgent = computed(() => {
  const task = selectedWorkflowTask.value
  if (!task) return false
  return isWorkflowUrgent(task, attentionItemMap.value.get(task.id) ?? null)
})

const selectedWorkflowNextAction = computed(() => {
  const task = selectedWorkflowTask.value
  if (!task) return ''
  return workflowNextAction(task, attentionItemMap.value.get(task.id) ?? null)
})

const selectedWorkflowOwnerName = computed(() => {
  const task = selectedWorkflowTask.value
  if (!task) return 'Chưa giao'
  return task.assigneeName || 'Chưa giao'
})

const selectedWorkflowActiveKey = computed<WorkflowMiniStage | 'overdue'>(() => {
  const task = selectedWorkflowTask.value
  if (!task) return 'todo'
  return workflowProgressKey(task)
})

type WorkflowDetailState = 'pending' | 'current' | 'complete' | 'danger'

const workflowDetailStages = computed(() => {
  const task = selectedWorkflowTask.value
  if (!task) return []

  const activeKey = selectedWorkflowActiveKey.value
  const overdue = isWorkflowUrgent(task, attentionItemMap.value.get(task.id) ?? null)
  return [
    {
      key: 'todo',
      step: '01',
      label: 'Chưa làm',
      note: 'Task mới tạo hoặc chưa bắt đầu triển khai.',
      state: activeKey === 'todo' ? 'current' : 'pending',
      tone: 'neutral',
      active: activeKey === 'todo',
    },
    {
      key: 'inProgress',
      step: '02',
      label: 'Đang làm',
      note: 'Task đang được xử lý trong sprint hiện tại.',
      state: activeKey === 'inProgress' ? 'current' : 'pending',
      tone: 'warning',
      active: activeKey === 'inProgress',
    },
    {
      key: 'done',
      step: '03',
      label: 'Đã làm',
      note: 'Công việc đã hoàn tất và sẵn sàng bàn giao.',
      state: activeKey === 'done' ? 'current' : 'pending',
      tone: 'success',
      active: activeKey === 'done',
    },
    {
      key: 'overdue',
      step: '04',
      label: 'Chậm tiến độ',
      note: overdue ? 'Task đang quá hạn hoặc có rủi ro trễ.' : 'Chỉ bật đỏ khi task bị chậm tiến độ.',
      state: overdue ? 'danger' : 'pending',
      tone: 'danger',
      active: false,
      hot: overdue,
    },
  ] as Array<{
    key: string
    step: string
    label: string
    note: string
    state: WorkflowDetailState
    tone: 'neutral' | 'warning' | 'success' | 'danger'
    active: boolean
    hot?: boolean
  }>
})

const selectedCount = computed(() => selectedTaskIds.value.length)
const isAllVisibleSelected = computed(() =>
  filteredTasks.value.length > 0 &&
  filteredTasks.value.every((task) => selectedTaskIds.value.includes(task.id)),
)

onMounted(async () => {
  loadSavedTaskViews()
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

watch(
  shouldFallbackToAll,
  (value) => {
    if (value) {
      taskScope.value = 'all'
    }
  },
  { immediate: true },
)

watch(
  filteredTasks,
  (tasks) => {
    if (selectedTaskId.value && !tasks.some((task) => task.id === selectedTaskId.value)) {
      closeTaskDrawer()
    }
  },
  { immediate: true },
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

function taskCardProgress(task: HubTask) {
  if (task.status === 'Done') return 100
  if (task.status === 'InReview') return 78
  if (task.status === 'InProgress') return 56
  if (task.status === 'OnHold') return 34
  if (task.status === 'Cancelled') return 12
  return isTaskOverdue(task) ? 18 : 22
}

function taskCardTone(task: HubTask) {
  if (isTaskOverdue(task)) return 'danger'
  if (task.status === 'Done') return 'success'
  if (task.status === 'InReview') return 'info'
  if (task.status === 'InProgress') return 'warning'
  if (task.status === 'OnHold') return 'neutral'
  return 'muted'
}

function taskCardToneLabel(task: HubTask) {
  if (isTaskOverdue(task)) return 'Quá hạn'
  if (task.status === 'Done') return 'Hoàn tất'
  if (task.status === 'InReview') return 'Đang duyệt'
  if (task.status === 'InProgress') return 'Đang làm'
  if (task.status === 'OnHold') return 'Tạm dừng'
  return 'Sẵn sàng'
}

function taskSmartSignals(task: HubTask) {
  const signals: Array<{ label: string; tone: 'neutral' | 'warning' | 'danger' | 'success' }> = []

  if (!task.assigneeId) {
    signals.push({ label: 'Cần owner', tone: 'warning' })
  }

  if (isTaskOverdue(task)) {
    signals.push({ label: 'Quá hạn', tone: 'danger' })
  } else if (isDueSoon(task)) {
    signals.push({ label: 'Sắp đến hạn', tone: 'warning' })
  }

  if (task.status === 'InProgress') {
    signals.push({ label: 'Đang làm', tone: 'neutral' })
  }

  if (task.status === 'Done') {
    signals.push({ label: 'Hoàn tất', tone: 'success' })
  }

  return signals.slice(0, 3)
}

function priorityLabel(priority: string) {
  const labels: Record<string, string> = {
    Low: 'Thấp',
    Medium: 'Trung bình',
    High: 'Cao',
    Critical: 'Khẩn cấp',
  }

  return labels[priority] ?? priority
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
  sortBy.value = 'risk'
}

function clearSelection() {
  selectedTaskIds.value = []
}

function resetDefaultView() {
  selectScope('mine')
  clearFilters()
  clearSelection()
}

function buildTaskViewSignature(view: Pick<SavedTaskView, 'scope' | 'searchQuery' | 'statusFilter' | 'priorityFilter' | 'projectFilter' | 'focusFilter' | 'sortBy'>) {
  return JSON.stringify({
    scope: view.scope,
    searchQuery: view.searchQuery.trim().toLowerCase(),
    statusFilter: view.statusFilter,
    priorityFilter: view.priorityFilter,
    projectFilter: view.projectFilter,
    focusFilter: view.focusFilter,
    sortBy: view.sortBy,
  })
}

function loadSavedTaskViews() {
  if (typeof window === 'undefined') return

  try {
    const raw = window.localStorage.getItem(savedTaskViewsStorageKey)
    if (!raw) {
      savedTaskViews.value = []
      return
    }

    const parsed = JSON.parse(raw)
    if (!Array.isArray(parsed)) {
      savedTaskViews.value = []
      return
    }

    savedTaskViews.value = parsed
      .filter((view) => view && typeof view === 'object')
      .map((view) => ({
        id: String(view.id ?? (typeof crypto !== 'undefined' && crypto.randomUUID ? crypto.randomUUID() : `view-${Date.now()}`)),
        name: String(view.name ?? 'Saved view'),
        scope: view.scope === 'all' ? 'all' : 'mine',
        searchQuery: String(view.searchQuery ?? ''),
        statusFilter: String(view.statusFilter ?? 'all'),
        priorityFilter: String(view.priorityFilter ?? 'all'),
        projectFilter: String(view.projectFilter ?? 'all'),
        focusFilter: (view.focusFilter as TaskFocus) ?? 'all',
        sortBy: (view.sortBy as TaskSort) ?? 'risk',
        signature: String(view.signature ?? ''),
      }))
  } catch {
    savedTaskViews.value = []
  }
}

function persistSavedTaskViews() {
  if (typeof window === 'undefined') return
  try {
    window.localStorage.setItem(savedTaskViewsStorageKey, JSON.stringify(savedTaskViews.value))
  } catch {
    // Best effort only.
  }
}

function saveCurrentView() {
  const signature = currentTaskViewSignature.value
  const existing = savedTaskViews.value.find((view) => view.signature === signature)
  if (existing) {
    existing.name = existing.name || 'Saved view'
    persistSavedTaskViews()
    showSuccess('View đã được lưu')
    return
  }

  const created: SavedTaskView = {
    id: typeof crypto !== 'undefined' && crypto.randomUUID ? crypto.randomUUID() : `view-${Date.now()}`,
    name: `View ${savedTaskViews.value.length + 1}`,
    scope: taskScope.value,
    searchQuery: searchQuery.value,
    statusFilter: statusFilter.value,
    priorityFilter: priorityFilter.value,
    projectFilter: projectFilter.value,
    focusFilter: focusFilter.value,
    sortBy: sortBy.value,
    signature,
  }

  savedTaskViews.value = [created, ...savedTaskViews.value].slice(0, 8)
  persistSavedTaskViews()
  showSuccess('Đã lưu view hiện tại')
}

function applySavedTaskView(view: SavedTaskView) {
  taskScope.value = view.scope
  searchQuery.value = view.searchQuery
  statusFilter.value = view.statusFilter
  priorityFilter.value = view.priorityFilter
  projectFilter.value = view.projectFilter
  focusFilter.value = view.focusFilter
  sortBy.value = view.sortBy
}

function deleteSavedTaskView(viewId: string) {
  savedTaskViews.value = savedTaskViews.value.filter((view) => view.id !== viewId)
  persistSavedTaskViews()
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
  } catch {
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
    const cachedDetail = taskDetailCache.value.get(taskId) ?? null
    if (cachedDetail) {
      selectedTaskDetail.value = cachedDetail
    }

    const detailResult =
      cachedDetail ?? await apiResult<TaskItemDto>(`/api/tasks/${taskId}`)

    if (selectedTaskId.value !== taskId) return

    if (detailResult) {
      taskDetailCache.value.set(taskId, detailResult)
    }

    selectedTaskDetail.value = detailResult ?? (selectedTaskSummary.value as TaskItemDto | null)

    selectedTaskComments.value = []
    selectedTaskAttachments.value = []
    selectedTaskTimeEntries.value = []
    selectedTaskMeetingSource.value = null

    void loadTaskDetailExtras(taskId)
  } catch (error) {
    if (selectedTaskId.value !== taskId) return
    selectedTaskDetail.value = selectedTaskSummary.value as TaskItemDto | null
    selectedTaskComments.value = []
    selectedTaskAttachments.value = []
    selectedTaskTimeEntries.value = []
    selectedTaskMeetingSource.value = null
    console.warn(error)
  } finally {
    selectedTaskLoading.value = false
  }
}

async function loadTaskDetailExtras(taskId: string) {
  try {
    const [commentsResult, attachmentsResult, timeEntriesResult, meetingSourceResult] = await Promise.allSettled([
      apiResult<CommentDto[]>(`/api/comments/task/${taskId}`),
      apiResult<AttachmentDto[]>(`/api/attachments/task/${taskId}`),
      apiResult<TimeEntryDto[]>(`/api/tasks/${taskId}/time-entries`),
      apiResult<TaskMeetingSourceDto>(`/api/tasks/${taskId}/meeting-source`),
    ])

    if (selectedTaskId.value !== taskId) return

    selectedTaskComments.value = commentsResult.status === 'fulfilled' ? commentsResult.value ?? [] : []
    selectedTaskAttachments.value = attachmentsResult.status === 'fulfilled' ? attachmentsResult.value ?? [] : []
    selectedTaskTimeEntries.value = timeEntriesResult.status === 'fulfilled' ? timeEntriesResult.value ?? [] : []
    selectedTaskMeetingSource.value = meetingSourceResult.status === 'fulfilled' ? meetingSourceResult.value : null
  } catch (error) {
    if (selectedTaskId.value !== taskId) return
    console.warn(error)
  }
}

async function prefetchTaskDetail(taskId: string) {
  if (taskDetailCache.value.has(taskId)) return

  try {
    const detailResult = await apiResult<TaskItemDto>(`/api/tasks/${taskId}`)
    if (detailResult) {
      taskDetailCache.value.set(taskId, detailResult)
    }
  } catch {
    // Prefetch is best-effort only.
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

function openTaskDrawer(task: TaskSummary) {
  openTask(task.projectId, task.id)
}

function closeTaskDrawer() {
  const projectId = selectedTaskSummary.value?.projectId
  selectedTaskId.value = null
  resetTaskDrawer()
  if (projectId) {
    void router.replace(`/projects/${projectId}`)
  }
}

function taskActionLabel(status: string) {
  return displayStatus(status)
}

async function updateSingleTaskStatus(task: Pick<DashboardTask, 'id'>, status: string) {
  try {
    await apiCommand(`/api/tasks/${task.id}/status`, {
      method: 'PATCH',
      body: JSON.stringify({ status }),
    })
    showSuccess(`Đã chuyển sang ${displayStatus(status)}`)
    await refreshAll()
  } catch (error) {
    showError(errorMessage(error, 'Không thể đổi trạng thái task.'))
  }
}

async function bulkUpdateStatus(status: string) {
  if (selectedTaskIds.value.length === 0) return
  const total = selectedTaskIds.value.length
  try {
    await apiCommand('/api/tasks/batch-status', {
      method: 'POST',
      body: JSON.stringify({ ids: selectedTaskIds.value, status }),
    })
    showSuccess(`Đã đổi ${total} task sang ${displayStatus(status)}`)
    clearSelection()
    await refreshAll()
  } catch (error) {
    showError(errorMessage(error, 'Không thể cập nhật hàng loạt.'))
  }
}

async function bulkDeleteTasks() {
  if (selectedTaskIds.value.length === 0) return
  if (!await confirmDialog({ tone:'danger', title:`Xóa ${selectedTaskIds.value.length} nhiệm vụ?`, message:'Các nhiệm vụ đã chọn sẽ bị xóa khỏi dự án.', confirmLabel:'Xóa nhiệm vụ' })) return
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

function showQuickStatusButtons(task: DashboardTask) {
  return nextStatuses(task.status).slice(0, 3)
}

function canNudge(task: HubTask | TaskAttentionDto) {
  const current = currentUser.value
  if (!current) return false
  return task.assigneeId != null && task.assigneeId !== current.id
}

function formatMeetingDate(value: string | null) {
  return value ? formatTime(value) : 'Chưa rõ'
}

function totalTrackedMinutes(entries: TimeEntryDto[] | null | undefined) {
  if (!Array.isArray(entries)) return 0
  return entries.reduce((total, entry) => total + (entry.totalMinutes || 0), 0)
}

function formatMinutes(total: number) {
  if (!total) return '0m'
  const hours = Math.floor(total / 60)
  const minutes = total % 60
  return hours > 0 ? `${hours}h ${minutes}m` : `${minutes}m`
}

function workflowMiniStepIndex(step: WorkflowMiniStage) {
  return workflowMiniSteps.findIndex((item) => item.key === step)
}

function workflowProgressKey(task: WorkflowTask): WorkflowMiniStage {
  if (task.status === 'Done') return 'done'
  if (task.status === 'InReview') return 'inReview'
  if (task.status === 'InProgress' || task.status === 'Blocked' || task.status === 'OnHold') return 'inProgress'
  return 'todo'
}

function workflowStageStatus(stage: WorkflowMiniStage) {
  const mapping: Record<WorkflowMiniStage, string> = {
    todo: 'Todo',
    inProgress: 'InProgress',
    inReview: 'InReview',
    done: 'Done',
  }

  return mapping[stage]
}

function workflowStageActionEnabled(stage: WorkflowMiniStage) {
  return stage === 'todo' || stage === 'inProgress' || stage === 'inReview' || stage === 'done'
}

async function setWorkflowStage(stage: string) {
  const task = selectedTaskSummary.value
  if (!task || !workflowStageActionEnabled(stage as WorkflowMiniStage)) return

  try {
    await updateSingleTaskStatus(
      { id: task.id },
      workflowStageStatus(stage as WorkflowMiniStage),
    )
  } catch {
    // handled by updateSingleTaskStatus
  }
}

function deriveWorkflowStage(task: Pick<WorkflowTask, 'status' | 'assigneeId'>): WorkflowStage {
  if (task.status === 'Done') return 'done'
  if (task.status === 'InReview') return 'inReview'
  if (task.status === 'InProgress') return 'inProgress'
  if (['Blocked', 'OnHold'].includes(task.status)) return 'blocked'
  if (!task.assigneeId) return 'needsOwner'
  return 'todo'
}

function describeWorkflowStage(stage: WorkflowStage) {
  const labels: Record<WorkflowStage, string> = {
    needsOwner: 'Needs Owner',
    todo: 'Todo',
    inProgress: 'In Progress',
    inReview: 'In Review',
    blocked: 'Blocked',
    done: 'Done',
  }

  return labels[stage]
}

function isWorkflowUrgent(task: Pick<WorkflowTask, 'status' | 'assigneeId' | 'dueDate' | 'priority'>, attention: TaskAttentionDto | null) {
  if (isTaskOverdue(task)) return true
  if (task.priority === 'Critical') return true
  if (task.status === 'InProgress' && (attention?.isStaleInProgress ?? false)) return true
  if (task.status === 'Todo' && (attention?.isStaleTodo ?? false)) return true
  return false
}

function workflowNextAction(task: Pick<WorkflowTask, 'status' | 'assigneeId' | 'dueDate' | 'priority'>, attention: TaskAttentionDto | null) {
  if (!task.assigneeId && task.status === 'Todo') return 'Gán owner trước để task có bước tiếp theo rõ ràng.'
  if (!task.assigneeId) return 'Gán owner để luồng theo dõi rõ ràng hơn, nhưng trạng thái hiện tại vẫn giữ nguyên.'
  if (isWorkflowUrgent(task, attention)) return 'Ưu tiên xử lý ngay, rồi đẩy task sang In Progress.'
  if (['Blocked', 'OnHold'].includes(task.status)) return 'Gỡ blocker hoặc cập nhật trạng thái chờ xử lý.'
  if (task.status === 'InReview') return 'Đang chờ xác nhận. Nếu ổn, chuyển sang Done.'
  if (task.status === 'InProgress' && (attention?.isStaleInProgress ?? false)) return 'Task đang stale. Cần cập nhật tiến độ.'
  if (task.status === 'Todo') return 'Bắt đầu làm để task đi vào luồng thực thi.'
  return 'Chọn bước kế tiếp phù hợp trong workflow.'
}

</script>

<template>
  <div class="tasks-page" :class="{ 'has-detail': Boolean(selectedTaskId) }">
    <div class="tasks-main">
      <section class="tasks-hero glass-card reveal">
        <div class="tasks-hero__copy">
          <span>Trung tâm nhiệm vụ</span>
          <h2>Nhiệm vụ của tôi</h2>
          <p>Một nơi để lọc nhanh, xem task quan trọng và mở chi tiết chỉ khi bạn thực sự cần.</p>
        </div>

        <div class="tasks-hero__actions">
          <div class="tasks-hero__segmented">
            <button class="pill-button" type="button" :class="{ 'is-active': taskScope === 'mine' }" @click="selectScope('mine')">
              Của tôi
            </button>
            <button class="pill-button" type="button" :class="{ 'is-active': taskScope === 'all' }" @click="selectScope('all')">
              Tất cả
            </button>
          </div>

          <button class="pill-button pill-button--soft" type="button" @click="refreshAll">
            <RefreshCw :size="15" />
            Làm mới
          </button>

          <div class="tasks-hero__summary">
            <span>Hiển thị</span>
            <strong>{{ taskScope === 'mine' ? 'Công việc của bạn' : 'Toàn workspace' }}</strong>
            <small>{{ filteredTasks.length }} task đang hiển thị</small>
          </div>
        </div>
      </section>

      <section class="tasks-stats">
        <article class="stat-card glass-card reveal delay-1">
          <div class="stat-card__head">
            <span>Tổng task</span>
            <span class="stat-card__icon is-blue"><ListFilter :size="15" /></span>
          </div>
          <strong>{{ taskSummary.total }}</strong>
          <small>{{ taskScope === 'mine' ? 'Task đang liên quan trực tiếp tới bạn' : 'Task trong toàn bộ workspace' }}</small>
        </article>
        <article class="stat-card glass-card reveal delay-2">
          <div class="stat-card__head">
            <span>Quá hạn</span>
            <span class="stat-card__icon is-red"><BadgeAlert :size="15" /></span>
          </div>
          <strong>{{ taskSummary.overdue }}</strong>
          <small>Những task cần xử lý ngay</small>
        </article>
        <article class="stat-card glass-card reveal delay-3">
          <div class="stat-card__head">
            <span>Sắp đến hạn</span>
            <span class="stat-card__icon is-amber"><Clock3 :size="15" /></span>
          </div>
          <strong>{{ taskSummary.dueSoon }}</strong>
          <small>Task nên ưu tiên trong 48h tới</small>
        </article>
        <article class="stat-card glass-card reveal delay-4">
          <div class="stat-card__head">
            <span>Đã chọn</span>
            <span class="stat-card__icon is-green"><CheckSquare2 :size="15" /></span>
          </div>
          <strong>{{ selectedCount }}</strong>
          <small>Có thể áp dụng thao tác hàng loạt</small>
        </article>
      </section>

      <section class="panel-card glass-card reveal delay-2">
        <div class="panel-head panel-head--split">
          <div class="panel-head__title">
            <span>Bộ lọc</span>
            <h3>Tìm nhanh và thu hẹp danh sách</h3>
            <p>Giữ mọi thứ gọn hơn bằng một hàng điều khiển ngắn, rõ, dễ quét.</p>
          </div>
          <div class="panel-head__meta">
            <button class="link-button" type="button" @click="resetDefaultView">Default view</button>
            <button class="link-button" type="button" @click="saveCurrentView">Lưu view</button>
            <button class="link-button" type="button" @click="clearFilters">Xóa lọc</button>
          </div>
        </div>

        <div class="saved-views" v-if="savedTaskViews.length > 0">
          <div
            v-for="view in savedTaskViews"
            :key="view.id"
            class="saved-view-chip"
            :class="{ 'is-active': activeSavedTaskView?.id === view.id }"
            role="button"
            tabindex="0"
            @click="applySavedTaskView(view)"
          >
            <span>{{ view.name }}</span>
            <button
              type="button"
              class="saved-view-chip__remove"
              @click.stop="deleteSavedTaskView(view.id)"
              aria-label="Xóa view đã lưu"
            >
              <X :size="12" />
            </button>
          </div>
        </div>

        <div class="filter-grid filter-grid--modern">
          <label class="field field--search">
            <Search :size="16" />
            <input v-model="searchQuery" type="search" placeholder="Tìm task, dự án, người giao..." />
          </label>

          <label class="field">
            <ListFilter :size="15" />
            <select v-model="projectFilter">
              <option value="all">Tất cả dự án</option>
              <option v-for="project in taskProjectOptions" :key="project.id" :value="project.id">
                {{ project.label }}
              </option>
            </select>
          </label>

          <label class="field">
            <Filter :size="15" />
            <select v-model="statusFilter">
              <option value="all">Tất cả trạng thái</option>
              <option v-for="status in statusOptions" :key="status" :value="status">
                {{ displayStatus(status) }}
              </option>
            </select>
          </label>

          <label class="field">
            <Clock3 :size="15" />
            <select v-model="sortBy">
              <option v-for="sort in sortOptions" :key="sort.value" :value="sort.value">
                {{ sort.label }}
              </option>
            </select>
          </label>

          <label class="field">
            <BadgeAlert :size="15" />
            <select v-model="focusFilter">
              <option v-for="focus in focusOptions" :key="focus.value" :value="focus.value">
                {{ focus.label }}
              </option>
            </select>
          </label>

          <label class="field">
            <SquareCheckBig :size="15" />
            <select v-model="priorityFilter">
              <option value="all">Tất cả ưu tiên</option>
              <option v-for="priority in priorityOptions" :key="priority" :value="priority">
                {{ priority }}
              </option>
            </select>
          </label>
        </div>
      </section>

      <section class="panel-card glass-card reveal delay-3">
        <div class="panel-head">
          <div>
            <span>Danh sách</span>
            <h3>{{ filteredTasks.length }} nhiệm vụ đang hiển thị</h3>
          </div>
          <div class="panel-head__meta">
            <label class="select-all">
              <input type="checkbox" :checked="isAllVisibleSelected" @change="toggleSelectAllVisible" />
              <span>Chọn tất cả</span>
            </label>
            <button class="link-button" type="button" :disabled="selectedCount === 0" @click="clearSelection">
              Bỏ chọn
            </button>
          </div>
        </div>

        <div v-if="selectedCount > 0" class="bulk-bar">
          <div>
            <strong>{{ selectedCount }} task đã chọn</strong>
            <small>Áp dụng thay đổi hàng loạt cho các task này.</small>
          </div>
          <div class="bulk-bar__actions">
            <button class="pill-button" type="button" @click="bulkUpdateStatus('InProgress')">Sang In Progress</button>
            <button class="pill-button" type="button" @click="bulkUpdateStatus('Done')">Đánh dấu Done</button>
            <button class="pill-button pill-button--danger" type="button" @click="bulkDeleteTasks">
              <Trash2 :size="14" />
              Xóa
            </button>
          </div>
        </div>

        <div v-if="filteredTasks.length === 0" class="empty-state">
          <ListFilter :size="24" />
          <strong>Không có task phù hợp</strong>
          <p>Thử xóa bớt bộ lọc hoặc đổi sang phạm vi khác.</p>
        </div>

        <div v-else class="task-cards">
          <article
            v-for="task in filteredTasks"
            :key="task.id"
            class="task-card"
            :class="{ 'is-selected': selectedTaskIds.includes(task.id), 'is-overdue': isTaskOverdue(task) }"
            :style="{ '--task-accent': `var(--task-${taskCardTone(task)})` }"
          >
            <label class="task-card__check">
              <input :checked="selectedTaskIds.includes(task.id)" type="checkbox" @change="toggleSelectTask(task.id)" />
            </label>

            <button
              class="task-card__body"
              type="button"
              @click="openTaskDrawer(task)"
              @mouseenter="prefetchTaskDetail(task.id)"
              @focus="prefetchTaskDetail(task.id)"
            >
              <div class="task-card__visual">
                <span class="task-card__marker" :class="`is-${taskCardTone(task)}`" aria-hidden="true"></span>

                <div class="task-card__content">
                  <div class="task-card__eyebrow">
                    <span class="task-card__tag">{{ task.projectCode }}</span>
                    <span class="task-card__priority" :class="`is-${task.priority.toLowerCase()}`">
                      {{ priorityLabel(task.priority) }}
                    </span>
                  </div>

                  <div class="task-card__titleRow">
                    <div class="task-card__titleWrap">
                      <strong>{{ task.title }}</strong>
                      <span class="task-card__subtitle">{{ task.projectName }}</span>
                    </div>
                  </div>

                  <div class="task-card__metaRow">
                    <span class="task-card__meta task-card__meta--assignee">{{ task.assigneeName || 'Chưa giao' }}</span>
                    <span class="task-card__meta task-card__meta--due">
                      {{ task.dueDate ? formatDate(task.dueDate) : 'Chưa có hạn' }}
                    </span>
                    <span class="task-card__meta task-card__meta--status">{{ taskCardToneLabel(task) }}</span>
                  </div>

                  <div v-if="taskSmartSignals(task).length > 0" class="task-card__signalRow">
                    <span
                      v-for="signal in taskSmartSignals(task)"
                      :key="signal.label"
                      class="task-card__signal"
                      :class="`is-${signal.tone}`"
                    >
                      {{ signal.label }}
                    </span>
                  </div>

                  <div class="task-card__progressRow">
                    <div class="task-card__progress" aria-hidden="true">
                      <span class="task-card__progressFill" :style="{ width: `${taskCardProgress(task)}%` }"></span>
                    </div>
                    <span class="task-card__progressText">{{ taskCardProgress(task) }}%</span>
                  </div>
                </div>

                <div class="task-card__side">
                  <span class="task-card__status">{{ taskCardToneLabel(task) }}</span>
                  <span class="task-card__owner">{{ task.assigneeName || 'Chưa giao' }}</span>
                  <span class="task-card__peek" aria-hidden="true">
                    <ArrowRight :size="14" />
                  </span>
                </div>
              </div>
            </button>
          </article>
        </div>
      </section>
    </div>

    <aside v-if="selectedTaskId" class="task-detail-drawer glass-card">
      <div class="task-detail">
        <div class="task-detail__header">
          <div>
            <span>Chi tiết task</span>
            <h2>{{ selectedTaskDisplay?.title || 'Đang tải...' }}</h2>
            <p v-if="selectedTaskDisplay">
              {{ selectedTaskProjectLine }}
            </p>
          </div>
          <button class="icon-button" type="button" @click="closeTaskDrawer">
            <X :size="16" />
          </button>
        </div>

        <section v-if="selectedTaskDisplay" class="task-detail__top">
          <div class="task-detail__summary">
            <div v-if="selectedTaskLoading" class="task-detail__skeleton" aria-hidden="true">
              <div class="skeleton-hero">
                <div class="skeleton-line skeleton-line--short"></div>
                <div class="skeleton-line skeleton-line--title"></div>
                <div class="skeleton-line skeleton-line--body"></div>
              </div>
              <div class="skeleton-grid">
                <div class="skeleton-card skeleton-card--wide"></div>
                <div class="skeleton-card skeleton-card--wide"></div>
                <div class="skeleton-card skeleton-card--wide"></div>
                <div class="skeleton-card skeleton-card--wide"></div>
              </div>
              <div class="skeleton-track">
                <span class="skeleton-track__rail"></span>
                <div v-for="step in 4" :key="step" class="skeleton-track__step">
                  <span class="skeleton-track__dot"></span>
                  <div class="skeleton-track__copy">
                    <span class="skeleton-line skeleton-line--tiny"></span>
                    <span class="skeleton-line skeleton-line--body"></span>
                  </div>
                </div>
              </div>
            </div>

            <div class="task-detail__summary-head">
              <span class="task-detail__project">{{ selectedTaskDisplay.projectName }}</span>
              <span class="task-detail__status">{{ displayStatus(selectedTaskDisplay.status) }}</span>
            </div>

            <p class="task-detail__description">
              {{ selectedTaskDescription }}
            </p>

            <div class="task-detail__facts">
              <div>
                <span>Priority</span>
                <strong>{{ selectedTaskDisplay.priority }}</strong>
              </div>
              <div>
                <span>Assignee</span>
                <strong>{{ selectedTaskDisplay.assigneeName || 'Chưa giao' }}</strong>
              </div>
              <div>
                <span>Reporter</span>
                <strong>{{ selectedTaskDisplay.reporterName }}</strong>
              </div>
              <div>
                <span>Due date</span>
                <strong>{{ selectedTaskDisplay.dueDate ? formatDate(selectedTaskDisplay.dueDate) : 'Chưa có' }}</strong>
              </div>
            </div>

            <div v-if="selectedTaskMeetingSource" class="task-detail__meeting">
              <span>Nguồn cuộc họp</span>
              <strong>{{ selectedTaskMeetingSource.meetingTitle }}</strong>
              <p>
                {{ selectedTaskMeetingSource.actionItemTitle || selectedTaskMeetingSource.actionItemDescription || selectedTaskMeetingSource.sourceQuote || 'Không có trích dẫn bổ sung.' }}
              </p>
              <small>{{ formatMeetingDate(selectedTaskMeetingSource.meetingStartedAt) }}</small>
            </div>

            <div class="task-detail__actions">
              <button
                v-for="status in showQuickStatusButtons({ ...selectedTaskSummary, ...selectedTaskDisplay } as DashboardTask)"
                :key="status"
                class="pill-button"
                type="button"
                @click="updateSingleTaskStatus({ ...selectedTaskSummary, ...selectedTaskDisplay } as DashboardTask, status)"
              >
                {{ taskActionLabel(status) }}
              </button>
              <button
                v-if="canNudge({ ...selectedTaskSummary, ...selectedTaskDisplay } as HubTask)"
                class="pill-button"
                type="button"
                @click="nudgeTask({ ...selectedTaskSummary, ...selectedTaskDisplay } as HubTask)"
              >
                <Send :size="14" />
                Nhắc
              </button>
              <button class="pill-button pill-button--ghost" type="button" @click="openProjectTask({ ...selectedTaskSummary, ...selectedTaskDisplay } as HubTask)">
                Mở project
              </button>
            </div>
          </div>

          <div v-if="selectedWorkflowTask" class="workflow-mini workflow-mini--vertical">
            <div class="workflow-mini__head workflow-mini__head--stacked">
              <div>
                <span>Workflow</span>
                <strong>{{ selectedWorkflowStage ? describeWorkflowStage(selectedWorkflowStage) : 'Chưa bắt đầu' }}</strong>
              </div>
              <div class="workflow-mini__signals">
                <span v-if="selectedWorkflowIsUrgent" class="workflow-signal is-danger">Cần chú ý</span>
                <span v-if="selectedWorkflowTask.assigneeId" class="workflow-signal is-success">Có người được giao</span>
                <span v-else class="workflow-signal is-warning">Chưa có owner</span>
              </div>
            </div>

            <div class="workflow-owner">
              <span>Người được giao</span>
              <strong>{{ selectedWorkflowOwnerName }}</strong>
              <small>{{ selectedWorkflowTask.assigneeId ? 'Task đang có người phụ trách' : 'Task chưa có người phụ trách' }}</small>
            </div>

            <p class="workflow-mini__note">{{ selectedWorkflowNextAction }}</p>

            <div class="workflow-track">
              <div
                v-for="(stage, index) in workflowDetailStages"
                :key="stage.key"
                class="workflow-track__item"
                :class="[`is-${stage.state}`, `is-${stage.tone}`, stage.active ? 'is-active' : 'is-muted', stage.hot ? 'is-hot' : '']"
                :style="{ '--stage-delay': `${index * 150}ms` }"
                role="button"
                tabindex="0"
                @click="setWorkflowStage(stage.key)"
              >
                <span v-if="index < workflowDetailStages.length - 1" class="workflow-track__rail" aria-hidden="true">
                  <span class="workflow-track__rail-flow"></span>
                </span>
                <span class="workflow-track__dot">
                  <span class="workflow-track__pulse" aria-hidden="true"></span>
                  <span class="workflow-track__spark" aria-hidden="true"></span>
                  <Circle v-if="stage.tone === 'neutral'" :size="12" />
                  <Clock3 v-else-if="stage.tone === 'warning'" :size="13" />
                  <CheckCheck v-else-if="stage.tone === 'success'" :size="13" />
                  <BadgeAlert v-else :size="13" />
                </span>
                <div class="workflow-track__body">
                  <span class="workflow-track__step">{{ stage.step }}</span>
                  <strong>{{ stage.label }}</strong>
                  <small>{{ stage.note }}</small>
                </div>
              </div>
            </div>
          </div>
        </section>

        <section v-if="selectedTaskDisplay" class="detail-grid">
          <div class="detail-block">
            <div class="detail-block__header">
              <h3>Comment</h3>
              <span>{{ selectedTaskComments.length }}</span>
            </div>
            <article v-for="comment in selectedTaskComments.slice(0, 4)" :key="comment.id" class="detail-item">
              <strong>{{ comment.authorName }}</strong>
              <p>{{ comment.content }}</p>
              <small>{{ formatTime(comment.createdAt) }}</small>
            </article>
            <div v-if="selectedTaskComments.length === 0" class="detail-empty">Chưa có comment.</div>
          </div>

          <div class="detail-block">
            <div class="detail-block__header">
              <h3>File đính kèm</h3>
              <span>{{ selectedTaskAttachments.length }}</span>
            </div>
            <article v-for="attachment in selectedTaskAttachments.slice(0, 4)" :key="attachment.id" class="detail-item">
              <strong>{{ attachment.fileName }}</strong>
              <p>{{ attachment.uploadedByName }} · {{ formatDate(attachment.uploadedAt) }}</p>
            </article>
            <div v-if="selectedTaskAttachments.length === 0" class="detail-empty">Chưa có file đính kèm.</div>
          </div>

          <div class="detail-block">
            <div class="detail-block__header">
              <h3>Time tracking</h3>
              <span>{{ formatMinutes(totalTrackedMinutes(selectedTaskTimeEntries)) }}</span>
            </div>
            <article v-for="entry in selectedTaskTimeEntries.slice(0, 4)" :key="entry.id" class="detail-item">
              <strong>{{ entry.userName }}</strong>
              <p>{{ entry.note || 'Không có ghi chú' }}</p>
              <small>{{ formatTime(entry.startedAt) }}</small>
            </article>
            <div v-if="selectedTaskTimeEntries.length === 0" class="detail-empty">Chưa ghi nhận thời gian.</div>
          </div>
        </section>
      </div>
    </aside>
  </div>
</template>

<style scoped>
.tasks-page {
  display: grid;
  grid-template-columns: minmax(0, 1fr);
  gap: 18px;
  align-items: start;
  background:
    radial-gradient(circle at 12% 8%, rgba(59, 130, 246, 0.08), transparent 22%),
    radial-gradient(circle at 86% 2%, rgba(16, 185, 129, 0.05), transparent 18%),
    linear-gradient(180deg, #f8fbff 0%, #ffffff 54%, #f8fafc 100%);
}

.tasks-page.has-detail {
  grid-template-columns: minmax(0, 1fr) 368px;
}

.tasks-main {
  min-width: 0;
  display: grid;
  gap: 14px;
}

.tasks-hero,
.panel-card,
.task-detail-drawer {
  border-radius: 24px;
}

.tasks-hero {
  display: grid;
  grid-template-columns: minmax(0, 1.35fr) minmax(340px, 0.95fr);
  gap: 16px;
  padding: 24px;
  background:
    radial-gradient(circle at 82% 10%, rgba(59, 130, 246, 0.08), transparent 26%),
    radial-gradient(circle at 10% 20%, rgba(16, 185, 129, 0.05), transparent 24%),
    linear-gradient(180deg, rgba(255, 255, 255, 0.98), rgba(246, 249, 255, 0.96));
  border: 1px solid rgba(191, 219, 254, 0.72);
  box-shadow: 0 18px 44px rgba(15, 23, 42, 0.06);
  position: relative;
  overflow: hidden;
}

.tasks-hero::before {
  content: '';
  position: absolute;
  inset: 0 0 auto 0;
  height: 3px;
  background: linear-gradient(90deg, #2563eb, #38bdf8, #22c55e);
}

.tasks-hero::after {
  content: '';
  position: absolute;
  inset: auto 0 0 0;
  height: 120px;
  background: radial-gradient(circle at 100% 100%, rgba(59, 130, 246, 0.12), transparent 55%);
  pointer-events: none;
}

.tasks-hero__copy {
  display: grid;
  gap: 8px;
}

.tasks-hero__copy span,
.panel-head span,
.task-detail__header span,
.workflow-mini__head span {
  color: var(--primary);
  font-size: 12px;
  font-weight: 800;
  letter-spacing: 0.08em;
  text-transform: uppercase;
}

.tasks-hero__copy h2,
.panel-head h3,
.task-detail__header h2 {
  color: var(--text-strong);
  font-size: clamp(28px, 3.2vw, 38px);
  font-weight: 900;
  letter-spacing: -0.04em;
}

.tasks-hero__copy p {
  max-width: 62ch;
  color: var(--muted);
  line-height: 1.6;
}

.tasks-hero__actions {
  display: grid;
  align-content: start;
  justify-items: stretch;
  gap: 12px;
}

.tasks-hero__segmented {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 8px;
  padding: 4px;
  border: 1px solid rgba(191, 219, 254, 0.72);
  border-radius: 999px;
  background: rgba(239, 246, 255, 0.72);
  box-shadow: inset 0 1px 0 rgba(255, 255, 255, 0.88);
}

.tasks-hero__summary {
  display: grid;
  gap: 3px;
  padding: 15px 16px;
  border: 1px solid rgba(191, 219, 254, 0.68);
  border-radius: 18px;
  background:
    radial-gradient(circle at top right, rgba(59, 130, 246, 0.08), transparent 28%),
    linear-gradient(180deg, rgba(255, 255, 255, 0.98), rgba(246, 249, 255, 0.94));
  box-shadow: 0 12px 28px rgba(15, 23, 42, 0.05);
}

.tasks-hero__summary span {
  color: var(--muted);
  font-size: 10px;
  font-weight: 800;
  letter-spacing: 0.12em;
  text-transform: uppercase;
}

.tasks-hero__summary strong {
  color: var(--text-strong);
  font-size: 16px;
  font-weight: 900;
}

.tasks-hero__summary small {
  color: var(--muted);
  font-size: 12px;
  line-height: 1.45;
}

.pill-button,
.mini-button,
.link-button,
.icon-button {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  border: 1px solid rgba(193, 211, 232, 0.9);
  border-radius: 999px;
  font-weight: 800;
}

.pill-button {
  min-height: 40px;
  padding: 0 14px;
  color: #1d4ed8;
  background: linear-gradient(180deg, rgba(255, 255, 255, 0.98), rgba(243, 248, 255, 0.98));
  box-shadow: 0 8px 18px rgba(15, 23, 42, 0.06);
}

.pill-button.is-active {
  border-color: rgba(37, 99, 235, 0.26);
  color: white;
  background: linear-gradient(135deg, #2563eb, #1d4ed8);
  box-shadow: 0 14px 26px rgba(37, 99, 235, 0.24);
}

.pill-button--soft {
  color: #0f172a;
  background:
    linear-gradient(180deg, rgba(255, 255, 255, 1), rgba(246, 249, 255, 0.96));
}

.pill-button--ghost,
.link-button {
  background: transparent;
}

.pill-button--danger {
  color: #dc2626;
  border-color: rgba(239, 68, 68, 0.24);
  background: linear-gradient(180deg, rgba(254, 242, 242, 0.98), rgba(255, 247, 247, 0.98));
}

.tasks-stats {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 12px;
}

.stat-card {
  display: grid;
  gap: 9px;
  padding: 15px 16px 14px;
  position: relative;
  overflow: hidden;
}

.stat-card::before {
  content: '';
  position: absolute;
  inset: 0 auto auto 0;
  width: 100%;
  height: 4px;
  background: linear-gradient(90deg, rgba(59, 130, 246, 0.38), rgba(99, 102, 241, 0.2), rgba(59, 130, 246, 0.38));
}

.stat-card:nth-child(2)::before {
  background: linear-gradient(90deg, rgba(248, 113, 113, 0.28), rgba(251, 146, 60, 0.16), rgba(248, 113, 113, 0.28));
}

.stat-card:nth-child(3)::before {
  background: linear-gradient(90deg, rgba(250, 204, 21, 0.26), rgba(245, 158, 11, 0.16), rgba(250, 204, 21, 0.26));
}

.stat-card:nth-child(4)::before {
  background: linear-gradient(90deg, rgba(34, 197, 94, 0.24), rgba(59, 130, 246, 0.16), rgba(34, 197, 94, 0.24));
}

.stat-card span {
  color: var(--muted);
  font-size: 12px;
  font-weight: 800;
  text-transform: uppercase;
}

.stat-card__head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
}

.stat-card__icon {
  display: inline-grid;
  place-items: center;
  width: 28px;
  height: 28px;
  border-radius: var(--qaly-radius-lg);
  background: rgba(255, 255, 255, 0.94);
  box-shadow: inset 0 0 0 1px rgba(226, 232, 240, 0.84);
}

.stat-card__icon.is-blue {
  color: #2563eb;
  background: rgba(239, 246, 255, 0.96);
}

.stat-card__icon.is-red {
  color: #dc2626;
  background: rgba(254, 242, 242, 0.96);
}

.stat-card__icon.is-amber {
  color: #d97706;
  background: rgba(255, 251, 235, 0.96);
}

.stat-card__icon.is-green {
  color: #16a34a;
  background: rgba(236, 253, 245, 0.96);
}

.stat-card strong {
  color: var(--text-strong);
  font-size: 28px;
  line-height: 1;
  font-weight: 900;
}

.stat-card small {
  color: var(--muted);
}

.panel-card {
  padding: 18px 20px 20px;
  display: grid;
  gap: 16px;
  background:
    radial-gradient(circle at top right, rgba(59, 130, 246, 0.06), transparent 28%),
    radial-gradient(circle at left top, rgba(16, 185, 129, 0.04), transparent 24%),
    linear-gradient(180deg, rgba(255, 255, 255, 0.99), rgba(247, 250, 255, 0.94));
  border: 1px solid rgba(223, 231, 242, 0.86);
  box-shadow: 0 16px 36px rgba(15, 23, 42, 0.05);
}

.panel-head {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 12px;
  padding-bottom: 2px;
  border-bottom: 1px solid rgba(226, 232, 240, 0.7);
}

.panel-head--split {
  align-items: center;
}

.panel-head > div:first-child {
  display: grid;
  gap: 6px;
}

.panel-head__title {
  display: grid;
  gap: 4px;
}

.panel-head__title p {
  color: var(--muted);
  line-height: 1.55;
  max-width: 64ch;
}

.panel-head__meta {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: flex-end;
  gap: 8px;
}

.saved-views {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.saved-view-chip {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  min-height: 34px;
  padding: 0 10px 0 12px;
  border: 1px solid rgba(191, 219, 254, 0.86);
  border-radius: 999px;
  color: #1d4ed8;
  background: linear-gradient(180deg, rgba(255, 255, 255, 0.98), rgba(239, 246, 255, 0.94));
  box-shadow: 0 8px 16px rgba(15, 23, 42, 0.04);
  font-size: 13px;
  font-weight: 800;
}

.saved-view-chip.is-active {
  color: white;
  border-color: rgba(31, 128, 255, 0.52);
  background: linear-gradient(135deg, #2563eb, #1d4ed8);
  box-shadow: 0 12px 24px rgba(37, 99, 235, 0.2);
}

.saved-view-chip__remove {
  display: inline-grid;
  place-items: center;
  width: 18px;
  height: 18px;
  border: 0;
  border-radius: 999px;
  color: inherit;
  background: rgba(255, 255, 255, 0.16);
}

.saved-view-chip:not(.is-active) .saved-view-chip__remove {
  background: rgba(148, 163, 184, 0.1);
}

.filter-grid {
  display: grid;
  grid-template-columns: minmax(300px, 2.1fr) repeat(5, minmax(0, 1fr));
  gap: 12px;
}

.filter-grid--modern {
  gap: 12px;
}

.field {
  display: inline-flex;
  align-items: center;
  gap: 10px;
  min-height: 48px;
  padding: 0 13px;
  border: 1px solid rgba(214, 226, 241, 0.92);
  border-radius: var(--qaly-radius-lg);
  background:
    linear-gradient(180deg, rgba(255, 255, 255, 0.99), rgba(246, 249, 255, 0.97)),
    linear-gradient(135deg, rgba(59, 130, 246, 0.025), rgba(99, 102, 241, 0.01));
  color: #64748b;
  box-shadow: 0 10px 24px rgba(15, 23, 42, 0.04);
  transition:
    border-color 0.18s ease,
    box-shadow 0.18s ease,
    transform 0.18s ease;
}

.field--search {
  min-width: 0;
}

.field svg {
  flex: none;
}

.field input,
.field select {
  width: 100%;
  min-width: 0;
  border: 0;
  outline: none;
  background: transparent;
  color: var(--text-strong);
  font: inherit;
}

.field input::placeholder {
  color: #94a3b8;
}

.field input,
.field select {
  font-size: 14px;
}

.field select {
  cursor: pointer;
  appearance: none;
}

.field:focus-within {
  border-color: rgba(59, 130, 246, 0.42);
  box-shadow: 0 14px 28px rgba(37, 99, 235, 0.08);
  transform: translateY(-1px);
}

.field:hover {
  border-color: rgba(191, 219, 254, 0.98);
  box-shadow: 0 12px 26px rgba(15, 23, 42, 0.05);
}

.select-all {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  color: #475569;
  font-size: 14px;
  font-weight: 700;
}

.select-all input {
  width: 16px;
  height: 16px;
  accent-color: var(--primary);
}

.panel-head .link-button {
  min-height: 34px;
  padding: 0 10px;
  border-radius: 999px;
  border: 1px solid rgba(191, 219, 254, 0.9);
  color: #0f172a;
  background: linear-gradient(180deg, rgba(255, 255, 255, 0.98), rgba(247, 250, 255, 0.95));
  box-shadow: 0 8px 16px rgba(15, 23, 42, 0.04);
}

.panel-head .link-button:hover:not(:disabled) {
  border-color: rgba(96, 165, 250, 0.48);
  color: var(--primary);
}

.bulk-bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding: 14px 16px;
  border: 1px solid rgba(191, 219, 254, 0.76);
  border-radius: var(--qaly-radius-lg);
  background:
    linear-gradient(180deg, rgba(239, 246, 255, 0.82), rgba(255, 255, 255, 0.98));
}

.bulk-bar strong {
  color: var(--text-strong);
}

.bulk-bar small {
  color: var(--muted);
}

.bulk-bar__actions {
  display: flex;
  flex-wrap: wrap;
  gap: 10px;
}

.empty-state {
  display: grid;
  gap: 8px;
  justify-items: center;
  padding: 36px 18px;
  border: 1px dashed rgba(191, 219, 254, 0.86);
  border-radius: var(--qaly-radius-lg);
  background:
    radial-gradient(circle at top, rgba(59, 130, 246, 0.04), transparent 34%),
    rgba(248, 250, 252, 0.9);
  color: var(--muted);
  text-align: center;
}

.empty-state strong {
  color: var(--text-strong);
  font-size: 16px;
}

.empty-state--compact {
  min-height: 220px;
  align-content: center;
}

.task-cards {
  display: grid;
  gap: 16px;
}

.task-card {
  display: grid;
  grid-template-columns: 22px minmax(0, 1fr);
  gap: 14px;
  align-items: start;
  padding: 16px 18px 15px;
  border: 1px solid rgba(203, 213, 225, 0.7);
  border-radius: var(--qaly-radius-lg);
  background:
    radial-gradient(circle at 14% 0%, rgba(59, 130, 246, 0.055), transparent 26%),
    radial-gradient(circle at 86% 10%, rgba(16, 185, 129, 0.028), transparent 22%),
    linear-gradient(135deg, rgba(255, 255, 255, 0.995) 0%, rgba(250, 252, 255, 0.985) 100%);
  position: relative;
  overflow: hidden;
  box-shadow: 0 16px 34px rgba(15, 23, 42, 0.05);
  transition:
    transform 0.24s ease,
    box-shadow 0.24s ease,
    border-color 0.24s ease,
    background 0.24s ease;
}

.task-card::before {
  content: '';
  position: absolute;
  inset: 0 auto 0 0;
  width: 4px;
  border-radius: 999px;
  background: linear-gradient(180deg, rgba(37, 99, 235, 0.98), rgba(59, 130, 246, 0.86));
  opacity: 0.98;
  box-shadow: 0 8px 16px rgba(37, 99, 235, 0.18);
}

.task-card:hover {
  transform: translateY(-2px);
  border-color: rgba(96, 165, 250, 0.72);
  box-shadow: 0 18px 38px rgba(15, 23, 42, 0.08);
  background:
    radial-gradient(circle at 10% 0%, rgba(59, 130, 246, 0.08), transparent 24%),
    linear-gradient(135deg, rgba(255, 255, 255, 1) 0%, rgba(249, 252, 255, 0.99) 56%, rgba(246, 249, 255, 0.98) 100%);
}

.task-card.is-selected {
  border-color: rgba(31, 128, 255, 0.3);
  box-shadow: var(--qaly-shadow-md);
}

.task-card.is-selected::before {
  background: linear-gradient(180deg, rgba(31, 128, 255, 1), rgba(99, 102, 241, 0.8));
}

.task-card.is-overdue {
  border-color: rgba(248, 113, 113, 0.26);
  box-shadow: var(--qaly-shadow-md);
}

.task-card.is-overdue::before {
  background: linear-gradient(180deg, rgba(248, 113, 113, 0.96), rgba(251, 146, 60, 0.84));
}

.task-card::after {
  content: '';
  position: absolute;
  inset: 0;
  background:
    radial-gradient(circle at 14% 0%, rgba(255, 255, 255, 0.95), transparent 24%),
    linear-gradient(90deg, rgba(255, 255, 255, 0.16), transparent 16%);
  opacity: 0.2;
  pointer-events: none;
}

.task-card__check {
  padding-top: 0;
  align-self: start;
  display: grid;
  place-items: center;
}

.task-card__check input {
  width: 17px;
  height: 17px;
  accent-color: var(--primary);
}

.task-card__body {
  display: grid;
  gap: 10px;
  text-align: left;
  cursor: pointer;
  background: transparent;
  border: 0;
  padding: 0;
  min-width: 0;
  align-self: stretch;
}

.task-card__visual {
  display: grid;
  grid-template-columns: 14px minmax(0, 1fr) auto;
  gap: 14px;
  align-items: start;
  min-width: 0;
}

.task-card__marker {
  width: 14px;
  height: 14px;
  margin-top: 7px;
  border-radius: 999px;
  box-shadow: 0 0 0 6px rgba(59, 130, 246, 0.06);
}

.task-card__marker.is-success {
  background: linear-gradient(180deg, rgba(34, 197, 94, 0.98), rgba(22, 163, 74, 0.96));
  box-shadow: 0 0 0 6px rgba(34, 197, 94, 0.08);
}

.task-card__marker.is-warning {
  background: linear-gradient(180deg, rgba(250, 204, 21, 0.98), rgba(245, 158, 11, 0.94));
  box-shadow: 0 0 0 6px rgba(245, 158, 11, 0.08);
}

.task-card__marker.is-danger {
  background: linear-gradient(180deg, rgba(248, 113, 113, 0.98), rgba(239, 68, 68, 0.96));
  box-shadow: 0 0 0 6px rgba(248, 113, 113, 0.1);
}

.task-card__marker.is-info,
.task-card__marker.is-muted,
.task-card__marker.is-neutral {
  background: linear-gradient(180deg, rgba(148, 163, 184, 0.96), rgba(100, 116, 139, 0.9));
  box-shadow: 0 0 0 6px rgba(148, 163, 184, 0.08);
}

.task-card__content {
  display: grid;
  gap: 10px;
  min-width: 0;
}

.task-card__titleRow {
  display: block;
  min-width: 0;
  position: relative;
}

.task-card__titleWrap {
  display: grid;
  gap: 6px;
  min-width: 0;
}

.task-card__titleWrap strong {
  color: #0f172a;
  font-size: 17px;
  font-weight: 900;
  letter-spacing: -0.02em;
  line-height: 1.28;
  max-width: 100%;
}

.task-card__subtitle {
  color: #64748b;
  font-size: 12px;
  font-weight: 600;
}

.task-card__eyebrow {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  min-width: 0;
  position: relative;
  z-index: 1;
}

.task-card__eyebrowRight {
  display: inline-flex;
  align-items: center;
  gap: 7px;
  flex-wrap: wrap;
  justify-content: flex-end;
}

.task-card__tag,
.task-card__status {
  display: inline-flex;
  align-items: center;
  min-height: 24px;
  padding: 0 10px;
  border-radius: 999px;
  font-size: 11px;
  font-weight: 800;
}

.task-card__tag {
  color: #1d4ed8;
  background:
    linear-gradient(180deg, rgba(239, 246, 255, 0.98), rgba(248, 251, 255, 0.98)),
    linear-gradient(135deg, rgba(59, 130, 246, 0.08), rgba(99, 102, 241, 0.04));
  box-shadow: 0 8px 16px rgba(15, 23, 42, 0.04);
}

.task-card__status {
  color: #475569;
  background:
    linear-gradient(180deg, rgba(248, 250, 252, 0.98), rgba(242, 246, 252, 0.98)),
    linear-gradient(135deg, rgba(148, 163, 184, 0.06), rgba(148, 163, 184, 0.02));
  box-shadow: 0 8px 16px rgba(15, 23, 42, 0.04);
}

.task-card__priority {
  display: inline-flex;
  align-items: center;
  min-height: 24px;
  padding: 0 10px;
  border-radius: 999px;
  font-size: 11px;
  font-weight: 800;
}

.task-card__priority.is-critical,
.task-card__priority.is-high {
  color: #b91c1c;
  background: linear-gradient(180deg, rgba(254, 226, 226, 0.98), rgba(255, 241, 241, 0.98));
  box-shadow: inset 0 0 0 1px rgba(248, 113, 113, 0.16);
}

.task-card__priority.is-medium {
  color: #a16207;
  background: linear-gradient(180deg, rgba(255, 251, 235, 0.98), rgba(255, 253, 242, 0.98));
  box-shadow: inset 0 0 0 1px rgba(250, 204, 21, 0.16);
}

.task-card__priority.is-low {
  color: #047857;
  background: linear-gradient(180deg, rgba(236, 253, 245, 0.98), rgba(240, 253, 250, 0.98));
  box-shadow: inset 0 0 0 1px rgba(34, 197, 94, 0.14);
}

.task-card__footerline {
  display: flex;
  flex-wrap: wrap;
  gap: 10px 12px;
  align-items: center;
  color: #64748b;
  font-size: 12px;
  position: relative;
  z-index: 1;
}

.task-card__footerline span {
  display: inline-flex;
  align-items: center;
  gap: 6px;
}

.task-card__metaRow {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  align-items: center;
  color: #64748b;
  font-size: 12px;
  position: relative;
  z-index: 1;
}

.task-card__meta {
  display: inline-flex;
  align-items: center;
  min-height: 30px;
  padding: 0 10px;
  border-radius: 999px;
  background:
    linear-gradient(180deg, rgba(250, 252, 255, 0.99), rgba(245, 248, 255, 0.98)),
    linear-gradient(135deg, rgba(59, 130, 246, 0.03), transparent);
  box-shadow: var(--qaly-shadow-md);
}

.task-card__meta--project {
  color: #1d4ed8;
}

.task-card__meta--assignee {
  color: #334155;
}

.task-card__meta--due {
  color: #475569;
}

.task-card__meta--status {
  color: #0f766e;
}

.task-card__signalRow {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  align-items: center;
}

.task-card__signal {
  display: inline-flex;
  align-items: center;
  min-height: 22px;
  padding: 0 9px;
  border-radius: 999px;
  font-size: 11px;
  font-weight: 800;
  letter-spacing: 0.01em;
}

.task-card__signal.is-neutral {
  color: #475569;
  background: rgba(241, 245, 249, 0.98);
}

.task-card__signal.is-warning {
  color: #a16207;
  background: rgba(255, 251, 235, 0.98);
}

.task-card__signal.is-danger {
  color: #dc2626;
  background: rgba(254, 242, 242, 0.98);
}

.task-card__signal.is-success {
  color: #047857;
  background: rgba(236, 253, 245, 0.98);
}

.task-card__progressRow {
  display: flex;
  align-items: center;
  gap: 10px;
  min-width: 0;
  position: relative;
  z-index: 1;
  padding-top: 2px;
}

.task-card__chip {
  min-height: 28px;
  padding: 0 11px;
  border-radius: 999px;
  color: #334155;
  background: linear-gradient(180deg, rgba(255, 255, 255, 0.98), rgba(244, 247, 255, 0.95));
  box-shadow: inset 0 0 0 1px rgba(226, 232, 240, 0.8);
}

.task-card__chip--soft {
  color: #475569;
  background: linear-gradient(180deg, rgba(239, 246, 255, 0.98), rgba(247, 250, 255, 0.98));
  box-shadow: inset 0 0 0 1px rgba(191, 219, 254, 0.7);
}

.task-card__metaTone {
  margin-left: auto;
  color: color-mix(in srgb, var(--task-accent) 90%, #0f172a);
  font-size: 10px;
  font-weight: 900;
  letter-spacing: 0.08em;
  text-transform: uppercase;
}

.task-card__progress {
  position: relative;
  overflow: hidden;
  height: 6px;
  flex: 1;
  border-radius: 999px;
  background:
    linear-gradient(180deg, rgba(226, 232, 240, 0.92), rgba(246, 249, 253, 0.98)),
    linear-gradient(90deg, rgba(255, 255, 255, 0.52), transparent);
  box-shadow:
    inset 0 1px 1px rgba(15, 23, 42, 0.05),
    0 0 0 1px rgba(255, 255, 255, 0.75);
}

.task-card__progress::after {
  content: '';
  position: absolute;
  inset: 0;
  background: linear-gradient(90deg, transparent, rgba(255, 255, 255, 0.8), transparent);
  width: 28%;
  transform: translateX(-120%);
  opacity: 0.72;
  animation: task-card-progress-sheen 2.2s linear infinite;
}

.task-card__progressFill {
  position: absolute;
  inset: 0 auto 0 0;
  border-radius: inherit;
  background:
    linear-gradient(90deg, color-mix(in srgb, var(--task-accent) 96%, white), var(--task-accent)),
    linear-gradient(180deg, rgba(255, 255, 255, 0.42), rgba(255, 255, 255, 0));
  box-shadow: 0 8px 16px rgba(15, 23, 42, 0.05);
}

.task-card__progressText {
  color: color-mix(in srgb, var(--task-accent) 82%, #0f172a);
  font-size: 11px;
  font-weight: 900;
  letter-spacing: 0.02em;
  min-width: 34px;
  text-align: right;
}

.task-card__progressRow::after {
  content: '';
  position: absolute;
  inset: -1px auto 0 0;
  width: 14px;
  border-radius: 999px;
  background: linear-gradient(180deg, color-mix(in srgb, var(--task-accent) 18%, white), transparent);
  opacity: 0.7;
  pointer-events: none;
}

.task-card__peek {
  display: inline-grid;
  place-items: center;
  width: 32px;
  height: 32px;
  border-radius: 999px;
  color: white;
  background:
    linear-gradient(180deg, rgba(37, 99, 235, 0.98), rgba(59, 130, 246, 0.96)),
    linear-gradient(135deg, rgba(96, 165, 250, 0.3), rgba(37, 99, 235, 0.12));
  opacity: 0;
  transform: translateX(-4px) scale(0.96);
  box-shadow: var(--qaly-shadow-md);
  transition: opacity 0.18s ease, transform 0.18s ease, background 0.18s ease, box-shadow 0.18s ease;
  flex: none;
}

.task-card__side {
  display: grid;
  gap: 8px;
  justify-items: end;
  align-content: start;
  min-width: 122px;
  padding-left: 8px;
}

.task-card__owner {
  color: #475569;
  font-size: 12px;
  font-weight: 700;
  text-align: right;
  line-height: 1.4;
}

.task-card:hover .task-card__peek,
.task-card:focus-within .task-card__peek {
  opacity: 1;
  transform: translateX(0) scale(1);
}

.task-card.is-selected .task-card__peek {
  opacity: 1;
}

.task-card__body:hover .task-card__peek {
  background: linear-gradient(180deg, rgba(29, 78, 216, 0.98), rgba(37, 99, 235, 0.98));
  box-shadow: var(--qaly-shadow-md);
}

.mini-button {
  min-height: 36px;
  padding: 0 12px;
  color: var(--primary);
  background:
    linear-gradient(180deg, rgba(255, 255, 255, 1), rgba(241, 248, 255, 0.95));
  box-shadow: var(--qaly-shadow-md);
}

.mini-button--ghost {
  color: #334155;
  background:
    linear-gradient(180deg, rgba(255, 255, 255, 1), rgba(248, 250, 252, 0.96));
  box-shadow: inset 0 0 0 1px rgba(226, 232, 240, 0.85);
}

.icon-button {
  width: 40px;
  height: 40px;
  background: white;
  color: var(--primary);
}

.task-badge {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  min-height: 24px;
  padding: 0 10px;
  border-radius: 999px;
  font-size: 11px;
  font-weight: 800;
}

.task-badge--priority.is-critical,
.task-badge--priority.is-high {
  color: #b91c1c;
  background: linear-gradient(180deg, rgba(254, 226, 226, 0.98), rgba(254, 242, 242, 0.98));
}

.task-badge--priority.is-medium {
  color: #a16207;
  background: linear-gradient(180deg, rgba(254, 249, 195, 0.98), rgba(255, 251, 235, 0.98));
}

.task-badge--priority.is-low {
  color: #047857;
  background: linear-gradient(180deg, rgba(236, 253, 245, 0.98), rgba(240, 253, 250, 0.98));
}

.task-badge--pin {
  color: var(--primary);
  background: linear-gradient(180deg, rgba(239, 246, 255, 0.98), rgba(245, 249, 255, 0.98));
}

.task-badge--danger {
  color: #b91c1c;
  background: linear-gradient(180deg, rgba(254, 226, 226, 0.98), rgba(255, 241, 241, 0.98));
}

.task-badge--warning {
  color: #a16207;
  background: linear-gradient(180deg, rgba(255, 251, 235, 0.98), rgba(255, 253, 242, 0.98));
}

.task-detail-drawer {
  position: sticky;
  top: 16px;
  min-width: 0;
  max-height: calc(100dvh - 104px);
  overflow-x: hidden;
  overflow-y: auto;
  overscroll-behavior: contain;
  -webkit-overflow-scrolling: touch;
  border: 1px solid rgba(223, 231, 242, 0.92);
  box-shadow: 0 18px 38px rgba(15, 23, 42, 0.06);
}

.task-detail {
  display: grid;
  gap: 14px;
  padding: 16px;
}

.task-detail__header {
  display: flex;
  justify-content: space-between;
  gap: 12px;
}

.task-detail__header p {
  color: var(--muted);
  margin-top: 4px;
}

.task-detail__skeleton {
  display: grid;
  gap: 12px;
  padding: 2px 0 6px;
}

.skeleton-line,
.skeleton-card {
  position: relative;
  overflow: hidden;
  background: linear-gradient(90deg, rgba(226, 232, 240, 0.9), rgba(241, 245, 249, 1), rgba(226, 232, 240, 0.9));
  background-size: 200% 100%;
  animation: skeleton-shimmer 1.35s ease-in-out infinite;
}

.skeleton-hero {
  display: grid;
  gap: 10px;
  padding: 14px;
  border-radius: var(--qaly-radius-lg);
  background: linear-gradient(180deg, rgba(248, 250, 252, 0.82), rgba(255, 255, 255, 0.98));
  border: 1px solid rgba(226, 232, 240, 0.9);
}

.skeleton-line {
  height: 12px;
  border-radius: 999px;
}

.skeleton-line--short {
  width: 110px;
}

.skeleton-line--title {
  width: 78%;
  height: 18px;
}

.skeleton-line--body {
  width: 92%;
  height: 12px;
}

.skeleton-line--tiny {
  width: 58%;
  height: 10px;
}

.skeleton-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 10px;
  margin-top: 2px;
}

.skeleton-card {
  height: 54px;
  border-radius: var(--qaly-radius-lg);
}

.skeleton-card--wide {
  height: 48px;
}

.skeleton-track {
  display: grid;
  gap: 12px;
  position: relative;
  padding: 4px 2px 0 28px;
}

.skeleton-track__rail {
  position: absolute;
  left: 16px;
  top: 10px;
  bottom: 10px;
  width: 3px;
  border-radius: 999px;
  background: linear-gradient(180deg, rgba(191, 219, 254, 0.35), rgba(59, 130, 246, 0.2), rgba(191, 219, 254, 0.35));
  overflow: hidden;
}

.skeleton-track__rail::after {
  content: '';
  position: absolute;
  inset: -20% 0 auto;
  height: 30%;
  border-radius: inherit;
  background: linear-gradient(180deg, transparent, rgba(255, 255, 255, 0.96), transparent);
  animation: skeleton-track-flow 1.8s linear infinite;
}

.skeleton-track__step {
  display: grid;
  grid-template-columns: 20px minmax(0, 1fr);
  gap: 12px;
  align-items: center;
}

.skeleton-track__dot {
  width: 20px;
  height: 20px;
  border-radius: 999px;
  background: linear-gradient(180deg, rgba(226, 232, 240, 0.95), rgba(241, 245, 249, 0.98));
  box-shadow: inset 0 0 0 1px rgba(226, 232, 240, 0.9);
}

.skeleton-track__copy {
  display: grid;
  gap: 6px;
}

.task-detail__top {
  display: grid;
  grid-template-columns: minmax(0, 1fr);
  gap: 14px;
  align-items: start;
}

.task-detail__summary {
  display: grid;
  gap: 14px;
  padding: 16px;
  border: 1px solid rgba(226, 232, 240, 0.84);
  border-radius: var(--qaly-radius-lg);
  background:
    radial-gradient(circle at top right, rgba(59, 130, 246, 0.06), transparent 34%),
    linear-gradient(180deg, rgba(253, 254, 255, 0.98), white);
}

.task-detail__summary-head {
  display: flex;
  justify-content: space-between;
  gap: 12px;
  align-items: center;
}

.task-detail__project,
.task-detail__status {
  display: inline-flex;
  align-items: center;
  min-height: 28px;
  padding: 0 10px;
  border-radius: 999px;
  font-size: 12px;
  font-weight: 800;
}

.task-detail__project {
  color: var(--primary);
  background: rgba(239, 246, 255, 0.96);
}

.task-detail__status {
  color: #047857;
  background: rgba(236, 253, 245, 0.96);
}

.task-detail__description {
  color: var(--muted);
  line-height: 1.65;
}

.task-detail__facts {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 10px;
}

.task-detail__facts div,
.detail-block,
.workflow-mini {
  border: 1px solid rgba(226, 232, 240, 0.9);
  border-radius: var(--qaly-radius-lg);
  background: white;
}

.task-detail__facts div {
  padding: 12px;
  display: grid;
  gap: 4px;
}

.task-detail__facts span,
.detail-block__header span {
  color: var(--muted);
  font-size: 11px;
  font-weight: 800;
  letter-spacing: 0.06em;
  text-transform: uppercase;
}

.task-detail__facts strong {
  color: var(--text-strong);
  font-size: 13px;
}

.task-detail__meeting {
  padding: 14px;
  border: 1px solid rgba(191, 219, 254, 0.9);
  border-radius: var(--qaly-radius-lg);
  background: linear-gradient(180deg, rgba(239, 246, 255, 0.88), rgba(255, 255, 255, 0.98));
}

.task-detail__meeting span {
  color: var(--primary);
  font-size: 12px;
  font-weight: 800;
  text-transform: uppercase;
}

.task-detail__meeting strong {
  display: block;
  margin-top: 6px;
  color: var(--text-strong);
}

.task-detail__meeting p {
  margin-top: 8px;
  color: var(--muted);
  line-height: 1.6;
}

.task-detail__meeting small {
  display: block;
  margin-top: 6px;
  color: var(--muted);
}

.task-detail__actions {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.workflow-mini {
  padding: 16px;
  display: grid;
  gap: 14px;
  border: 1px solid rgba(226, 232, 240, 0.88);
  border-radius: var(--qaly-radius-lg);
  background:
    radial-gradient(circle at top right, rgba(59, 130, 246, 0.1), transparent 30%),
    radial-gradient(circle at left top, rgba(16, 185, 129, 0.05), transparent 28%),
    linear-gradient(180deg, rgba(255, 255, 255, 1), rgba(249, 251, 255, 0.98));
}

.workflow-mini--vertical {
  position: relative;
}

.workflow-mini__head {
  display: flex;
  justify-content: space-between;
  gap: 10px;
  align-items: start;
}

.workflow-mini__head--stacked {
  align-items: flex-start;
}

.workflow-mini__head strong {
  display: block;
  color: var(--text-strong);
  font-size: 15px;
  font-weight: 900;
}

.workflow-mini__signals {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  justify-content: flex-end;
}

.workflow-signal {
  display: inline-flex;
  align-items: center;
  min-height: 24px;
  padding: 0 8px;
  border-radius: 999px;
  font-size: 11px;
  font-weight: 800;
}

.workflow-signal.is-danger {
  color: #b91c1c;
  background: rgba(254, 226, 226, 0.96);
}

.workflow-signal.is-warning {
  color: #a16207;
  background: rgba(255, 251, 235, 0.96);
}

.workflow-signal.is-success {
  color: #047857;
  background: rgba(236, 253, 245, 0.96);
}

.workflow-owner {
  display: grid;
  gap: 4px;
  padding: 12px 14px;
  border-radius: var(--qaly-radius-lg);
  border: 1px solid rgba(191, 219, 254, 0.82);
  background:
    radial-gradient(circle at 10% 0%, rgba(59, 130, 246, 0.08), transparent 28%),
    linear-gradient(180deg, rgba(239, 246, 255, 0.98), rgba(255, 255, 255, 0.99));
  box-shadow: var(--qaly-shadow-md);
}

.workflow-owner span {
  color: var(--primary);
  font-size: 11px;
  font-weight: 800;
  letter-spacing: 0.06em;
  text-transform: uppercase;
}

.workflow-owner strong {
  color: #0f172a;
  font-size: 14px;
  font-weight: 900;
}

.workflow-owner small,
.workflow-mini__note {
  color: var(--muted);
  line-height: 1.55;
}

.workflow-track {
  display: grid;
  gap: 12px;
  padding-left: 2px;
}

.workflow-track__item {
  position: relative;
  display: grid;
  grid-template-columns: 40px minmax(0, 1fr);
  gap: 12px;
  align-items: start;
  padding: 12px 12px 12px 0;
  cursor: pointer;
  opacity: 1;
  filter: none;
  transform: translateY(6px);
  animation: workflow-step-in 0.4s ease forwards;
  animation-delay: var(--stage-delay, 0ms);
  transition: opacity 0.18s ease, transform 0.18s ease;
}

.workflow-track__item:hover {
  transform: translateX(2px);
}

.workflow-track__item.is-active::before {
  content: '';
  position: absolute;
  inset: 4px 0 4px 34px;
  border-radius: var(--qaly-radius-lg);
  background:
    linear-gradient(90deg, rgba(255, 255, 255, 0.72), rgba(255, 255, 255, 0.12)),
    linear-gradient(90deg, rgba(239, 246, 255, 0.9), rgba(239, 246, 255, 0));
  opacity: 0.65;
  pointer-events: none;
}

.workflow-track__item:focus-visible {
  outline: none;
}

.workflow-track__item:focus-visible::before {
  opacity: 0.95;
  box-shadow: 0 0 0 3px rgba(59, 130, 246, 0.08);
}

.workflow-track__item.is-active.is-neutral::before {
  background:
    linear-gradient(90deg, rgba(248, 250, 252, 0.9), rgba(248, 250, 252, 0.18)),
    linear-gradient(90deg, rgba(148, 163, 184, 0.08), rgba(148, 163, 184, 0));
}

.workflow-track__item.is-active.is-warning::before {
  background:
    linear-gradient(90deg, rgba(255, 251, 235, 0.94), rgba(255, 251, 235, 0.16)),
    linear-gradient(90deg, rgba(245, 158, 11, 0.1), rgba(245, 158, 11, 0));
}

.workflow-track__item.is-active.is-success::before {
  background:
    linear-gradient(90deg, rgba(236, 253, 245, 0.94), rgba(236, 253, 245, 0.16)),
    linear-gradient(90deg, rgba(34, 197, 94, 0.1), rgba(34, 197, 94, 0));
}

.workflow-track__item.is-active.is-danger::before {
  background:
    linear-gradient(90deg, rgba(254, 242, 242, 0.94), rgba(254, 242, 242, 0.16)),
    linear-gradient(90deg, rgba(239, 68, 68, 0.1), rgba(239, 68, 68, 0));
}

.workflow-track__rail {
  position: absolute;
  left: 18px;
  top: 34px;
  bottom: -14px;
  width: 6px;
  border-radius: 999px;
  background:
    linear-gradient(180deg, rgba(191, 219, 254, 0.2), rgba(59, 130, 246, 0.56), rgba(191, 219, 254, 0.22)),
    linear-gradient(180deg, rgba(255, 255, 255, 0.16), rgba(255, 255, 255, 0));
  background-size: 100% 260%;
  overflow: hidden;
  box-shadow: var(--qaly-shadow-md);
}

.workflow-track__rail-flow {
  position: absolute;
  inset: -26% 0 auto;
  height: 34%;
  border-radius: inherit;
  background:
    linear-gradient(180deg, transparent, rgba(255, 255, 255, 0.98), transparent),
    linear-gradient(180deg, rgba(96, 165, 250, 0.02), rgba(96, 165, 250, 0.36), rgba(96, 165, 250, 0.02));
  filter:
    drop-shadow(0 0 10px rgba(255, 255, 255, 0.92))
    drop-shadow(0 0 14px rgba(59, 130, 246, 0.12));
  animation: workflow-rail-stream 1.35s linear infinite;
}

.workflow-track__rail::before {
  content: '';
  position: absolute;
  inset: 0;
  border-radius: inherit;
  background: linear-gradient(180deg, rgba(255, 255, 255, 0.28), rgba(255, 255, 255, 0));
  filter: blur(0.8px);
  opacity: 0.88;
}

.workflow-track__rail::after {
  content: '';
  position: absolute;
  inset: -30% 0 auto;
  height: 34%;
  border-radius: inherit;
  background:
    linear-gradient(180deg, transparent, rgba(255, 255, 255, 0.98), transparent),
    linear-gradient(90deg, transparent, rgba(255, 255, 255, 0.82), transparent);
  opacity: 0;
  filter: drop-shadow(0 0 12px rgba(255, 255, 255, 0.78));
}

.workflow-track__dot {
  display: grid;
  place-items: center;
  width: 40px;
  height: 40px;
  border-radius: 999px;
  border: 1px solid rgba(203, 213, 225, 0.95);
  color: #64748b;
  background:
    radial-gradient(circle at 30% 28%, rgba(255, 255, 255, 0.98), rgba(248, 250, 252, 0.94)),
    linear-gradient(180deg, rgba(255, 255, 255, 0.98), rgba(245, 248, 255, 0.96));
  box-shadow: var(--qaly-shadow-md);
  transition: transform 0.22s ease, box-shadow 0.22s ease, background 0.22s ease;
}

.workflow-track__pulse {
  position: absolute;
  inset: -6px;
  border-radius: inherit;
  opacity: 0;
  transform: scale(0.72);
  filter: blur(1px);
}

.workflow-track__spark {
  position: absolute;
  left: 50%;
  top: -12px;
  width: 10px;
  height: 10px;
  border-radius: 999px;
  transform: translateX(-50%);
  opacity: 0;
  box-shadow: 0 0 0 0 rgba(255, 255, 255, 0.9);
  filter: blur(0.2px);
}

.workflow-track__body {
  display: grid;
  gap: 3px;
  padding-top: 3px;
}

.workflow-track__step {
  color: #94a3b8;
  font-size: 11px;
  font-weight: 900;
  letter-spacing: 0.08em;
  text-transform: uppercase;
}

.workflow-track__body strong {
  color: #475569;
  font-size: 13px;
  font-weight: 900;
}

.workflow-track__body small {
  color: #94a3b8;
  font-size: 12px;
  line-height: 1.45;
}

.workflow-track__item.is-active {
  opacity: 1;
  filter: none;
  transform: translateX(1px);
  background: linear-gradient(90deg, rgba(239, 246, 255, 0.48), rgba(255, 255, 255, 0));
  border-radius: var(--qaly-radius-lg);
}

.workflow-track__item.is-active .workflow-track__body strong {
  color: #0f172a;
}

.workflow-track__item.is-active .workflow-track__body small {
  color: var(--muted);
}

.workflow-track__item.is-active.is-neutral .workflow-track__step {
  color: #64748b;
}

.workflow-track__item.is-active.is-warning .workflow-track__step {
  color: #d97706;
}

.workflow-track__item.is-active.is-success .workflow-track__step {
  color: #16a34a;
}

.workflow-track__item.is-active.is-danger .workflow-track__step {
  color: #dc2626;
}

.workflow-track__item.is-active.is-neutral .workflow-track__dot {
  color: #64748b;
  background: linear-gradient(180deg, rgba(243, 244, 246, 0.98), rgba(226, 232, 240, 0.94));
}

.workflow-track__item.is-active.is-warning .workflow-track__dot {
  color: white;
  background: linear-gradient(180deg, rgba(250, 204, 21, 0.98), rgba(245, 158, 11, 0.94));
  border-color: transparent;
  box-shadow: var(--qaly-shadow-md);
}

.workflow-track__item.is-active.is-success .workflow-track__dot {
  color: white;
  background: linear-gradient(180deg, rgba(34, 197, 94, 0.98), rgba(22, 163, 74, 0.95));
  border-color: transparent;
  box-shadow: var(--qaly-shadow-md);
  animation: workflow-heartbeat 1.35s ease-in-out infinite;
}

.workflow-track__item.is-active.is-danger .workflow-track__dot {
  color: white;
  background: linear-gradient(180deg, rgba(248, 113, 113, 0.96), rgba(239, 68, 68, 0.94));
  border-color: transparent;
  box-shadow: var(--qaly-shadow-md);
}

.workflow-track__item.is-active.is-warning .workflow-track__pulse {
  background: rgba(245, 158, 11, 0.24);
  animation: workflow-pulse 1.65s ease-in-out infinite;
}

.workflow-track__item.is-active.is-success .workflow-track__pulse {
  background: rgba(22, 163, 74, 0.24);
  animation: workflow-pulse 1.35s ease-in-out infinite;
}

.workflow-track__item.is-active.is-danger .workflow-track__pulse {
  background: rgba(239, 68, 68, 0.24);
  animation: workflow-pulse 1.65s ease-in-out infinite;
}

.workflow-track__item.is-active.is-neutral .workflow-track__pulse {
  background: rgba(148, 163, 184, 0.22);
  animation: workflow-pulse 1.65s ease-in-out infinite;
}

.workflow-track__item.is-active.is-neutral .workflow-track__spark {
  background: rgba(148, 163, 184, 0.96);
  animation: workflow-spark 2.8s ease-in-out infinite;
}

.workflow-track__item.is-active.is-warning .workflow-track__spark {
  background: rgba(245, 158, 11, 0.96);
  animation: workflow-spark 2.8s ease-in-out infinite;
}

.workflow-track__item.is-active.is-success .workflow-track__spark {
  background: rgba(34, 197, 94, 0.96);
  animation: workflow-spark 2.4s ease-in-out infinite;
}

.workflow-track__item.is-active.is-danger .workflow-track__spark {
  background: rgba(239, 68, 68, 0.96);
  animation: workflow-spark 2.8s ease-in-out infinite;
}

.workflow-track__item.is-muted .workflow-track__dot {
  color: #cbd5e1;
  background: linear-gradient(180deg, rgba(248, 250, 252, 0.98), rgba(241, 245, 249, 0.96));
  border-color: rgba(226, 232, 240, 0.96);
  box-shadow: inset 0 1px 0 rgba(255, 255, 255, 0.8);
}

.workflow-track__item.is-muted .workflow-track__rail {
  background: linear-gradient(180deg, rgba(226, 232, 240, 0.42), rgba(226, 232, 240, 0.08));
}

.workflow-track__item.is-muted .workflow-track__rail-flow {
  background:
    linear-gradient(180deg, transparent, rgba(255, 255, 255, 0.96), transparent),
    linear-gradient(180deg, rgba(226, 232, 240, 0.04), rgba(191, 219, 254, 0.16), rgba(226, 232, 240, 0.04));
  animation: workflow-rail-stream 2.2s linear infinite;
}

.workflow-track__item.is-hot .workflow-track__dot {
  color: #dc2626;
  background:
    radial-gradient(circle at 30% 28%, rgba(255, 255, 255, 0.98), rgba(254, 242, 242, 0.98)),
    linear-gradient(180deg, rgba(255, 255, 255, 0.98), rgba(254, 242, 242, 0.98));
  border-color: rgba(248, 113, 113, 0.42);
  box-shadow: var(--qaly-shadow-md);
}

.workflow-track__item.is-hot .workflow-track__step {
  color: #dc2626;
}

.workflow-track__item.is-hot .workflow-track__body strong {
  color: #0f172a;
}

.workflow-track__item.is-hot .workflow-track__body small {
  color: #ef4444;
}

.workflow-track__item.is-hot .workflow-track__rail {
  background: linear-gradient(180deg, rgba(248, 113, 113, 0.42), rgba(248, 113, 113, 0.14));
  animation: workflow-rail-flow 2.2s ease-in-out infinite;
}

.workflow-track__item.is-hot .workflow-track__rail::after {
  opacity: 1;
  animation: workflow-rail-sheen 1.35s linear infinite;
}

.workflow-track__item.is-hot .workflow-track__rail-flow {
  background:
    linear-gradient(180deg, transparent, rgba(255, 255, 255, 0.98), transparent),
    linear-gradient(180deg, rgba(248, 113, 113, 0.08), rgba(248, 113, 113, 0.42), rgba(248, 113, 113, 0.08));
  animation: workflow-rail-stream 1.55s linear infinite;
}

.workflow-track__item.is-active.is-neutral .workflow-track__rail {
  background:
    linear-gradient(180deg, rgba(148, 163, 184, 0.34), rgba(148, 163, 184, 0.08)),
    linear-gradient(180deg, rgba(255, 255, 255, 0.24), transparent);
  animation:
    workflow-rail-flow 2.4s linear infinite,
    workflow-rail-breathe 1.8s ease-in-out infinite;
}

.workflow-track__item.is-active.is-warning .workflow-track__rail {
  background:
    linear-gradient(180deg, rgba(245, 158, 11, 0.44), rgba(245, 158, 11, 0.12)),
    linear-gradient(180deg, rgba(255, 255, 255, 0.26), transparent);
  animation:
    workflow-rail-flow 2.4s linear infinite,
    workflow-rail-breathe 1.8s ease-in-out infinite;
}

.workflow-track__item.is-active.is-success .workflow-track__rail {
  background:
    linear-gradient(180deg, rgba(34, 197, 94, 0.44), rgba(34, 197, 94, 0.12)),
    linear-gradient(180deg, rgba(255, 255, 255, 0.26), transparent);
  animation:
    workflow-rail-flow 2.4s linear infinite,
    workflow-rail-breathe 1.8s ease-in-out infinite;
}

.workflow-track__item.is-active.is-danger .workflow-track__rail {
  background:
    linear-gradient(180deg, rgba(239, 68, 68, 0.44), rgba(239, 68, 68, 0.12)),
    linear-gradient(180deg, rgba(255, 255, 255, 0.26), transparent);
  animation:
    workflow-rail-flow 2.4s linear infinite,
    workflow-rail-breathe 1.8s ease-in-out infinite;
}

.workflow-track__item.is-active .workflow-track__rail::after {
  opacity: 1;
  animation: workflow-rail-sheen 1.1s linear infinite;
}

.workflow-track__item.is-active .workflow-track__rail-flow {
  animation: workflow-rail-stream 1.35s linear infinite;
}

@keyframes skeleton-shimmer {
  0% {
    background-position: 200% 0;
  }
  100% {
    background-position: -200% 0;
  }
}

@keyframes workflow-pulse {
  0% {
    opacity: 0;
    transform: scale(0.72);
  }
  45% {
    opacity: 1;
    transform: scale(1);
  }
  100% {
    opacity: 0;
    transform: scale(1.22);
  }
}

@keyframes workflow-heartbeat {
  0% {
    transform: scale(1);
    box-shadow: var(--qaly-shadow-md);
  }
  18% {
    transform: scale(1.08);
  }
  36% {
    transform: scale(0.98);
  }
  54% {
    transform: scale(1.1);
    box-shadow: var(--qaly-shadow-md);
  }
  72% {
    transform: scale(1.01);
  }
  100% {
    transform: scale(1);
    box-shadow: var(--qaly-shadow-md);
  }
}

@keyframes workflow-spark {
  0% {
    opacity: 0;
    transform: translateX(-50%) translateY(-8px) scale(0.75);
    box-shadow: 0 0 0 0 rgba(255, 255, 255, 0.15);
  }
  20% {
    opacity: 1;
  }
  50% {
    opacity: 1;
    transform: translateX(-50%) translateY(calc(100% + 24px)) scale(1);
    box-shadow: var(--qaly-shadow-md);
  }
  100% {
    opacity: 0;
    transform: translateX(-50%) translateY(calc(100% + 24px)) scale(0.75);
    box-shadow: 0 0 0 0 rgba(255, 255, 255, 0);
  }
}

@keyframes workflow-step-in {
  from {
    opacity: 0;
    transform: translateY(10px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

@keyframes workflow-rail-flow {
  0% {
    background-position: 0 0;
  }
  100% {
    background-position: 0 240%;
  }
}

@keyframes workflow-rail-breathe {
  0% {
    filter: drop-shadow(0 0 6px rgba(255, 255, 255, 0.24));
  }
  50% {
    filter: drop-shadow(0 0 14px rgba(255, 255, 255, 0.5));
  }
  100% {
    filter: drop-shadow(0 0 6px rgba(255, 255, 255, 0.24));
  }
}

@keyframes workflow-rail-sheen {
  0% {
    transform: translateY(-22%);
    opacity: 0;
  }
  12% {
    opacity: 1;
  }
  60% {
    opacity: 1;
  }
  100% {
    transform: translateY(360%);
    opacity: 0;
  }
}

@keyframes workflow-rail-stream {
  0% {
    transform: translateY(-28%);
    opacity: 0;
  }
  10% {
    opacity: 1;
  }
  60% {
    opacity: 1;
  }
  100% {
    transform: translateY(360%);
    opacity: 0;
  }
}

@keyframes task-card-progress-sheen {
  0% {
    transform: translateX(-120%);
  }
  100% {
    transform: translateX(380%);
  }
}

@keyframes skeleton-track-flow {
  0% {
    transform: translateY(-22%);
    opacity: 0;
  }
  15% {
    opacity: 1;
  }
  100% {
    transform: translateY(360%);
    opacity: 0;
  }
}

@media (max-width: 520px) {
  .task-detail__facts {
    grid-template-columns: 1fr;
  }

  .workflow-track__item {
    grid-template-columns: 36px minmax(0, 1fr);
  }

  .workflow-track__dot {
    width: 34px;
    height: 34px;
  }
}

.detail-grid {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 12px;
}

.detail-block {
  padding: 14px;
  display: grid;
  gap: 10px;
}

.detail-block__header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
}

.detail-block__header h3 {
  color: var(--text-strong);
  font-size: 14px;
  font-weight: 900;
}

.detail-item {
  display: grid;
  gap: 4px;
}

.detail-item strong {
  color: var(--text-strong);
  font-size: 13px;
}

.detail-item p,
.detail-empty {
  color: var(--muted);
  font-size: 12px;
  line-height: 1.55;
}

.detail-empty {
  padding: 8px 0 2px;
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
  .tasks-page.has-detail {
    grid-template-columns: minmax(0, 1fr);
  }

  .task-detail-drawer {
    position: static;
    max-height: none;
    overflow: visible;
  }

  .task-detail__top {
    grid-template-columns: 1fr;
  }

  .detail-grid {
    grid-template-columns: 1fr;
  }
}

@media (max-width: 960px) {
  .tasks-stats,
  .task-detail__facts {
    grid-template-columns: 1fr;
  }

  .filter-grid {
    grid-template-columns: 1fr;
  }

  .task-card {
    grid-template-columns: 1fr;
    align-items: start;
  }

  .task-card__visual {
    grid-template-columns: 14px minmax(0, 1fr);
  }

  .task-card__side {
    grid-column: 1 / -1;
    justify-items: start;
    min-width: 0;
    padding-left: 0;
  }

  .task-card__check {
    padding-top: 0;
    align-self: start;
  }

  .task-card__actions {
    justify-content: flex-start;
  }
}

@media (max-width: 720px) {
  .tasks-hero {
    grid-template-columns: 1fr;
  }

  .bulk-bar {
    flex-direction: column;
    align-items: stretch;
  }

  .task-card__head {
    flex-direction: column;
  }

  .panel-head {
    flex-direction: column;
  }

  .task-card {
    padding: 16px;
  }

  .task-card__visual {
    gap: 12px;
  }

  .task-card__titleWrap strong {
    font-size: 17px;
  }

  .task-card__metaRow,
  .task-card__signalRow {
    gap: 6px;
  }

  .tasks-hero__summary {
    padding: 14px 16px;
  }
}

/* Enterprise compact layout: keep the existing task behavior and data flow intact. */
.tasks-page {
  width: 100%;
  max-width: 1480px;
  min-width: 0;
  margin-inline: auto;
  padding: 20px clamp(16px, 2vw, 28px) 32px;
  gap: 12px;
  overflow-x: clip;
  background: #f8fafc;
}

.tasks-main {
  gap: 12px;
}

.tasks-hero,
.panel-card,
.task-detail-drawer,
.stat-card,
.task-card {
  border-radius: 10px;
}

.tasks-hero {
  grid-template-columns: minmax(0, 1fr) minmax(260px, 300px);
  align-items: center;
  gap: 20px;
  padding: 16px 20px;
  border-color: #e2e8f0;
  background: #fff;
  box-shadow: none;
}

.tasks-hero::before {
  height: 2px;
  background: var(--primary);
}

.tasks-hero::after {
  display: none;
}

.tasks-hero__copy {
  gap: 4px;
}

.tasks-hero__copy h2 {
  font-size: clamp(26px, 2.2vw, 32px);
  font-weight: 800;
  letter-spacing: -0.03em;
  line-height: 1.15;
}

.tasks-hero__copy p {
  max-width: 600px;
  font-size: 13px;
  line-height: 1.5;
}

.tasks-hero__actions {
  grid-template-columns: minmax(0, 1fr) auto;
  gap: 8px;
}

.tasks-hero__segmented {
  gap: 4px;
  padding: 2px;
  border-radius: 8px;
  border-color: #dbe4ef;
  background: #f8fafc;
  box-shadow: none;
}

.tasks-hero__summary {
  grid-column: 1 / -1;
  gap: 2px;
  padding: 8px 12px;
  border-radius: 8px;
  border-color: #e2e8f0;
  background: #f8fafc;
  box-shadow: none;
}

.tasks-hero__summary strong {
  font-size: 14px;
}

.tasks-hero__summary small {
  font-size: 11px;
}

.pill-button {
  min-height: 36px;
  padding-inline: 12px;
  border-radius: 8px;
  box-shadow: none;
}

.pill-button.is-active {
  background: var(--primary);
  box-shadow: none;
}

.tasks-stats {
  gap: 12px;
}

.stat-card {
  min-height: 88px;
  align-content: space-between;
  gap: 4px;
  padding: 14px 16px;
  border: 1px solid #e2e8f0;
  background: #fff;
  box-shadow: none;
}

.stat-card::before {
  display: none;
}

.stat-card span {
  font-size: 11px;
}

.stat-card__icon {
  width: 26px;
  height: 26px;
  border-radius: 8px;
  box-shadow: inset 0 0 0 1px #e2e8f0;
}

.stat-card strong {
  font-size: 26px;
  font-weight: 800;
}

.stat-card small {
  overflow: hidden;
  font-size: 11px;
  line-height: 1.25;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.panel-card {
  gap: 12px;
  padding: 16px;
  border-color: #e2e8f0;
  background: #fff;
  box-shadow: none;
}

.panel-head {
  align-items: center;
  padding-bottom: 0;
  border-bottom: 0;
}

.panel-head > div:first-child,
.panel-head__title {
  gap: 2px;
}

.panel-head h3 {
  font-size: clamp(20px, 1.8vw, 24px);
  font-weight: 800;
  letter-spacing: -0.025em;
  line-height: 1.2;
}

.panel-head__title p {
  max-width: 600px;
  font-size: 12px;
  line-height: 1.4;
}

.panel-head__meta {
  gap: 8px;
}

.panel-head .link-button {
  min-height: 32px;
  padding-inline: 10px;
  border-radius: 8px;
  border-color: #e2e8f0;
  background: #fff;
  box-shadow: none;
  font-size: 12px;
}

.saved-views {
  gap: 8px;
}

.saved-view-chip {
  min-height: 30px;
  border-radius: 8px;
  background: #f8fafc;
  box-shadow: none;
  font-size: 12px;
}

.filter-grid {
  grid-template-columns:
    minmax(260px, 1.6fr)
    repeat(4, minmax(130px, 0.8fr))
    minmax(150px, 0.9fr);
  gap: 10px;
}

.field {
  min-height: 36px;
  gap: 8px;
  padding-inline: 10px;
  border-radius: 8px;
  border-color: #dbe4ef;
  background: #fff;
  box-shadow: none;
}

.field input,
.field select {
  font-size: 12px;
}

.field:hover,
.field:focus-within {
  transform: none;
  border-color: #93c5fd;
  box-shadow: 0 0 0 2px rgba(59, 130, 246, 0.08);
}

.task-cards {
  gap: 8px;
}

.task-card {
  grid-template-columns: 28px minmax(0, 1fr);
  min-height: 108px;
  gap: 8px;
  padding: 12px 16px 8px;
  border-color: #e2e8f0;
  background: #fff;
  box-shadow: none;
  transition: border-color 0.16s ease, background 0.16s ease;
}

.task-card::before {
  width: 3px;
  border-radius: 0;
  background: var(--task-accent);
  box-shadow: none;
}

.task-card::after {
  display: none;
}

.task-card:hover {
  transform: none;
  border-color: #bfdbfe;
  background: #fbfdff;
  box-shadow: none;
}

.task-card.is-selected,
.task-card.is-overdue {
  box-shadow: none;
}

.task-card__check {
  padding-top: 3px;
}

.task-card__check input {
  width: 16px;
  height: 16px;
}

.task-card__body {
  gap: 8px;
}

.task-card__visual {
  grid-template-columns: 12px minmax(0, 1fr) minmax(170px, 220px);
  gap: 12px;
  height: 100%;
}

.task-card__marker {
  width: 8px;
  height: 8px;
  margin-top: 6px;
  box-shadow: none !important;
}

.task-card__content {
  align-content: start;
  gap: 6px;
}

.task-card__eyebrow {
  justify-content: flex-start;
  gap: 8px;
}

.task-card__tag,
.task-card__status,
.task-card__priority {
  min-height: 20px;
  padding-inline: 7px;
  border-radius: 6px;
  font-size: 10px;
  box-shadow: none;
}

.task-card__titleWrap {
  gap: 2px;
}

.task-card__titleWrap strong {
  display: block;
  overflow: hidden;
  max-width: 100%;
  color: #0f172a;
  font-size: 15px;
  font-weight: 650;
  line-height: 1.3;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.task-card__subtitle {
  overflow: hidden;
  font-size: 11px;
  line-height: 1.3;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.task-card__metaRow {
  gap: 4px 12px;
  font-size: 11px;
}

.task-card__meta {
  min-height: auto;
  padding: 0;
  border-radius: 0;
  background: transparent;
  box-shadow: none;
}

.task-card__meta--assignee,
.task-card__meta--status,
.task-card__signalRow {
  display: none;
}

.task-card__signalRow {
  gap: 6px;
}

.task-card__signal {
  min-height: 20px;
  padding-inline: 7px;
  border-radius: 6px;
  font-size: 10px;
}

.task-card__progressRow {
  align-self: end;
  gap: 8px;
  padding-top: 0;
}

.task-card__progress {
  height: 4px;
  background: #e2e8f0;
  box-shadow: none;
}

.task-card__progress::after,
.task-card__progressRow::after {
  display: none;
}

.task-card__progressFill {
  background: var(--task-accent);
  box-shadow: none;
}

.task-card__progressText {
  min-width: 32px;
  font-size: 10px;
}

.task-card__side {
  grid-template-columns: minmax(0, 1fr) 28px;
  grid-template-areas:
    'status peek'
    'owner peek';
  align-content: start;
  align-items: center;
  justify-items: end;
  gap: 4px 8px;
  min-width: 170px;
  padding-left: 8px;
}

.task-card__side .task-card__status {
  grid-area: status;
}

.task-card__owner {
  grid-area: owner;
  overflow: hidden;
  max-width: 180px;
  font-size: 11px;
  line-height: 1.3;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.task-card__peek {
  grid-area: peek;
  width: 28px;
  height: 28px;
  border-radius: 8px;
  box-shadow: none;
}

@media (max-width: 1279px) {
  .tasks-stats {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }

  .filter-grid {
    grid-template-columns: minmax(240px, 1.5fr) repeat(2, minmax(160px, 1fr));
  }

  .task-card__visual {
    grid-template-columns: 12px minmax(0, 1fr) minmax(150px, 180px);
  }
}

@media (max-width: 960px) {
  .tasks-page {
    padding-top: 16px;
  }

  .tasks-stats {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }

  .filter-grid {
    grid-template-columns: minmax(220px, 1.4fr) minmax(150px, 1fr);
  }

  .task-card {
    grid-template-columns: 28px minmax(0, 1fr);
  }

  .task-card__visual {
    grid-template-columns: 12px minmax(0, 1fr);
  }

  .task-card__side {
    grid-column: 2;
    grid-template-columns: auto minmax(0, 1fr) 28px;
    grid-template-areas: 'status owner peek';
    justify-items: start;
    min-width: 0;
    padding-left: 0;
  }
}

@media (max-width: 767px) {
  .tasks-page {
    padding: 12px 12px 24px;
  }

  .tasks-hero {
    grid-template-columns: 1fr;
    gap: 12px;
    padding: 16px;
  }

  .tasks-hero__actions {
    grid-template-columns: minmax(0, 1fr) auto;
  }

  .tasks-stats {
    grid-template-columns: repeat(2, minmax(0, 1fr));
    gap: 8px;
  }

  .stat-card {
    min-height: 84px;
    padding: 12px;
  }

  .panel-card {
    padding: 12px;
  }

  .panel-head,
  .panel-head--split {
    align-items: stretch;
  }

  .panel-head__meta {
    justify-content: flex-start;
  }

  .filter-grid {
    grid-template-columns: 1fr;
    gap: 8px;
  }

  .field {
    min-height: 40px;
  }

  .task-card {
    min-height: 0;
    padding: 12px 12px 8px;
  }

  .task-card__visual {
    gap: 8px;
  }

  .task-card__titleWrap strong {
    font-size: 14px;
    white-space: normal;
    display: -webkit-box;
    -webkit-box-orient: vertical;
    -webkit-line-clamp: 2;
  }

  .task-card__side {
    grid-template-columns: auto minmax(0, 1fr) 32px;
  }

  .task-card__peek {
    width: 32px;
    height: 32px;
    opacity: 1;
    transform: none;
  }
}

@media (max-width: 420px) {
  .tasks-stats {
    grid-template-columns: 1fr;
  }

  .tasks-hero__actions {
    grid-template-columns: 1fr;
  }

  .tasks-hero__summary {
    grid-column: 1;
  }

  .task-card__side {
    grid-template-columns: minmax(0, 1fr) 32px;
    grid-template-areas:
      'status peek'
      'owner peek';
  }
}
</style>
