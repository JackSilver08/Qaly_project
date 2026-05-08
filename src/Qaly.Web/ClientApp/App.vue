<script setup lang="ts">
import { computed, onMounted, provide, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { HubConnectionBuilder } from '@microsoft/signalr'
import {
  ClipboardList,
  FolderKanban,
  LayoutDashboard,
  Users,
  X,
} from 'lucide-vue-next'
import AppShell from './components/AppShell.vue'
import FloatingChatbot from './components/chat/FloatingChatbot.vue'
import { dashboardContextKey } from './composables/dashboard-context'
import { showError, showInfo, showSuccess } from './composables/use-toast'
import { fallbackDashboard } from './fallback-dashboard'
import type { ProjectCardModel, SummaryCardModel, TaskListItemModel } from './components/dashboard-models'
import type { ShellNavItem } from './components/shell-models'
import type {
  ApiResult,
  AttachmentDto,
  CommentDto,
  DashboardNotification,
  DashboardProject,
  DashboardResponse,
  DashboardTask,
  NotificationDto,
  ProjectDto,
  TaskItemDto,
  UserDto,
  WikiPageDto,
  TimeEntryDto,
} from './types'

type ProjectFilter = 'all' | 'active' | 'planned' | 'at-risk'
type ProjectSort = 'recent' | 'risk' | 'progress' | 'name'

const navigation: ShellNavItem[] = [
  { label: 'Tổng quan', to: '/dashboard', icon: LayoutDashboard },
  { label: 'Dự án', to: '/projects', icon: FolderKanban },
  { label: 'Nhiệm vụ', to: '/tasks', icon: ClipboardList },
  { label: 'Nhóm', to: '/teams', icon: Users },
]

const statusColumns = ['Todo', 'InProgress', 'InReview', 'Done']
const priorities = ['Low', 'Medium', 'High', 'Critical']

const dashboard = ref<DashboardResponse>(fallbackDashboard)
const currentUser = ref<UserDto | null>(null)
const users = ref<UserDto[]>([])
const notifications = ref<NotificationDto[]>([])
const comments = ref<CommentDto[]>([])
const attachments = ref<AttachmentDto[]>([])
const wikiPages = ref<WikiPageDto[]>([])
const timeEntries = ref<TimeEntryDto[]>([])
const activeTimer = ref<TimeEntryDto | null>(null)

const isLoading = ref(true)
const usingFallback = ref(true)
const notificationsOpen = ref(false)
const createProjectOpen = ref(false)
const createTaskOpen = ref(false)
const projectBeingEditedId = ref<string | null>(null)
const selectedTaskId = ref<string | null>(null)
const taskSearchQuery = ref('')
const taskBeingQuickEditedId = ref<string | null>(null)
const activeTaskMenu = ref<string | null>(null)

function toggleTaskMenu(taskId: string) {
  activeTaskMenu.value = activeTaskMenu.value === taskId ? null : taskId
}
const searchQuery = ref('')
const projectFilter = ref<ProjectFilter>('all')
const projectSort = ref<ProjectSort>('recent')
const activeProjectId = ref<string | null>(null)
const activeProjectTab = ref('stats')
const projectName = ref('')
const projectDescription = ref('')
const projectEndDate = ref('')

const tabs = [
  { id: 'stats', label: 'Thống kê' },
  { id: 'tasks', label: 'Task' },
  { id: 'members', label: 'Member' },
  { id: 'wiki', label: 'Wiki' },
]

const editProjectName = ref('')
const editProjectDescription = ref('')
const newTaskTitle = ref('')
const newTaskDescription = ref('')
const newTaskPriority = ref('Medium')
const newTaskAssigneeId = ref('')
const newTaskDueDate = ref('')
const newComment = ref('')

let notificationConnectionStarted = false
const router = useRouter()
const route = useRoute()

const projects = computed(() => dashboard.value.projects)
const team = computed(() => dashboard.value.team)
const activeProjectsCount = computed(() => projects.value.filter((project) => project.status !== 'Archived').length)
const totalTasks = computed(() => projects.value.reduce((sum, project) => sum + project.tasks.length, 0))
const completedTasks = computed(() =>
  projects.value.reduce((sum, project) => sum + project.tasks.filter((task) => task.status === 'Done').length, 0),
)
const overdueTasks = computed(() =>
  projects.value.reduce((sum, project) => sum + project.tasks.filter((task) => isTaskOverdue(task)).length, 0),
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

const filteredProjects = computed(() => {
  const query = searchQuery.value.trim().toLowerCase()

  return projects.value
    .filter((project) => {
      if (projectFilter.value === 'active' && !['Active', 'InProgress'].includes(project.status)) return false
      if (projectFilter.value === 'planned' && project.status !== 'Planned') return false
      if (projectFilter.value === 'at-risk' && project.overdueTaskCount === 0) return false

      if (!query) return true

      return [project.name, project.description, project.ownerName, ...project.members.map((member) => member.fullName)]
        .join(' ')
        .toLowerCase()
        .includes(query)
    })
    .sort((left, right) => {
      if (projectSort.value === 'name') return left.name.localeCompare(right.name)
      if (projectSort.value === 'progress') return right.progressPercentage - left.progressPercentage
      if (projectSort.value === 'risk') return right.overdueTaskCount - left.overdueTaskCount
      return new Date(right.createdAt).getTime() - new Date(left.createdAt).getTime()
    })
})

function toProjectCard(project: DashboardProject): ProjectCardModel {
  return {
    id: project.id,
    name: project.name,
    description: project.description || 'No description yet.',
    status: project.status,
    statusLabel: displayStatus(project.status),
    statusTone: statusTone(project.status),
    ownerId: project.ownerId,
    ownerName: project.ownerName,
    dueDateLabel: project.endDate ? `Due ${formatDate(project.endDate)}` : 'No due date',
    completedTaskCount: project.completedTaskCount,
    taskCount: project.taskCount,
    overdueTaskCount: project.overdueTaskCount,
    progressPercentage: project.progressPercentage,
    memberInitials: (project.members || []).slice(0, 4).map(m => initials(m.fullName)),
  }
}

const projectCards = computed<ProjectCardModel[]>(() =>
  filteredProjects.value.map(toProjectCard),
)

const activeProjectCards = computed<ProjectCardModel[]>(() =>
  filteredProjects.value
    .filter((project) => project.status !== 'Archived')
    .map(toProjectCard),
)

const archivedProjectCards = computed<ProjectCardModel[]>(() =>
  filteredProjects.value
    .filter((project) => project.status === 'Archived')
    .map(toProjectCard),
)

const assignedTaskCards = computed<TaskListItemModel[]>(() =>
  projects.value.flatMap((project) =>
    project.tasks
      .filter((task) => !currentUser.value || task.assigneeName === currentUser.value.fullName)
      .map((task) => ({
        id: task.id,
        projectId: project.id,
        title: task.title,
        projectName: project.name,
        assignedAtLabel: formatDate(project.createdAt),
        priority: task.priority,
        dueDateLabel: formatDate(task.dueDate),
        reporterName: task.reporterName,
        reporterInitials: initials(task.reporterName),
        statusLabel: displayStatus(task.status),
        isOverdue: isTaskOverdue(task),
      })),
  ),
)

const selectedProject = computed(() => {
  if (activeProjectId.value) {
    const active = projects.value.find((project) => project.id === activeProjectId.value)
    if (active) return active
  }

  return filteredProjects.value[0] ?? projects.value[0] ?? null
})

const selectedProjectTasks = computed(() => selectedProject.value?.tasks ?? [])
const selectedTask = computed(() => {
  if (!selectedProjectTasks.value.length) return null
  return selectedProjectTasks.value.find((task) => task.id === selectedTaskId.value) ?? selectedProjectTasks.value[0]
})

const isProjectAdmin = computed(() => {
  const project = selectedProject.value
  const user = currentUser.value
  if (!project || !user) return false
  
  const userRole = String(user.role || '').toLowerCase()
  if (userRole === 'admin' || user.email === 'admin@qaly.dev') return true
  
  const userId = String(user.id || '').toLowerCase()
  if (project.ownerId?.toLowerCase() === userId) return true
  
  const member = project.members?.find(m => String(m.userId || '').toLowerCase() === userId)
  if (member) {
    const memberRole = String(member.role || '').toLowerCase()
    if (memberRole === 'owner' || memberRole === 'manager') return true
  }
  
  return false
})

const selectedProjectMembers = computed(() => {
  const project = selectedProject.value
  if (!project) return []

  return project.members.map((member) => ({
    id: member.userId,
    fullName: member.fullName,
    role: member.role,
    email: member.email,
    initials: initials(member.fullName),
  }))
})


const selectedProjectStats = computed(() => {
  const project = selectedProject.value
  if (!project) return { total: 0, todo: 0, inProgress: 0, inReview: 0, done: 0, overdue: 0, completionRate: 0 }

  const tasks = project.tasks
  const total = tasks.length
  const todo = tasks.filter(t => t.status === 'Todo').length
  const inProgress = tasks.filter(t => t.status === 'InProgress').length
  const inReview = tasks.filter(t => t.status === 'InReview').length
  const done = tasks.filter(t => t.status === 'Done').length
  const overdue = tasks.filter(t => isTaskOverdue(t)).length
  const completionRate = total > 0 ? Math.round((done / total) * 100) : 0

  return {
    total,
    todo,
    inProgress,
    inReview,
    done,
    overdue,
    completionRate
  }
})

const notificationItems = computed<DashboardNotification[]>(() => {
  const realtime = notifications.value.map(toDashboardNotification)
  const dashboardItems = dashboard.value.notifications
  const seen = new Set<string>()

  return [...realtime, ...dashboardItems]
    .filter((item) => {
      if (seen.has(item.id)) return false
      seen.add(item.id)
      return true
    })
    .sort((left, right) => new Date(right.createdAt).getTime() - new Date(left.createdAt).getTime())
})

const notificationCount = computed(
  () => notifications.value.filter((notification) => !notification.isRead).length +
    dashboard.value.notifications.filter((notification) => notification.tone !== 'info').length,
)

watch(
  filteredProjects,
  (items) => {
    if (!['dashboard', 'projects'].includes(String(route.name ?? ''))) return

    if (!items.some((project) => project.id === activeProjectId.value)) {
      activeProjectId.value = items[0]?.id ?? projects.value[0]?.id ?? null
    }
  },
  { immediate: true },
)

watch(
  () => route.params.projectId,
  (projectId) => {
    if (typeof projectId === 'string') {
      activeProjectId.value = projectId
    }
  },
  { immediate: true },
)

watch(
  () => route.params.taskId,
  (taskId) => {
    if (typeof taskId === 'string') {
      selectedTaskId.value = taskId
      activeProjectTab.value = 'tasks'
    }
  },
  { immediate: true },
)

watch(
  selectedTask,
  (task) => {
    selectedTaskId.value = task?.id ?? null
    if (task && !usingFallback.value) {
      void loadComments(task.id)
      void loadAttachments(task.id)
      void loadTimeEntries(task.id)
    } else {
      comments.value = []
      attachments.value = []
      timeEntries.value = []
    }
  },
  { immediate: true },
)

watch(
  () => [activeProjectId.value, activeProjectTab.value],
  ([projectId, tab]) => {
    if (projectId && tab === 'wiki' && !usingFallback.value) {
      void loadWikiPages(String(projectId))
    }
  },
  { immediate: true }
)

onMounted(async () => {
  await Promise.all([loadMe(), loadDashboard(), loadUsers(), loadNotifications()])
  await connectNotifications()
})

async function loadDashboard() {
  isLoading.value = true
  const previousProjectId = activeProjectId.value

  try {
    dashboard.value = await apiJson<DashboardResponse>('/api/dashboard/overview')
    usingFallback.value = false
  } catch (error) {
    console.warn('Using fallback dashboard data.', error)
    dashboard.value = fallbackDashboard
    usingFallback.value = true
  } finally {
    const routeProjectId = typeof route.params.projectId === 'string' ? route.params.projectId : null
    const preferredProjectId = routeProjectId ?? previousProjectId

    activeProjectId.value = dashboard.value.projects.some((project) => project.id === preferredProjectId)
      ? preferredProjectId
      : dashboard.value.projects[0]?.id ?? null
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

async function loadNotifications() {
  try {
    notifications.value = await apiResult<NotificationDto[]>('/api/notifications')
  } catch (error) {
    console.warn('Could not load notifications.', error)
  }
}

async function loadComments(taskId: string) {
  try {
    comments.value = await apiResult<CommentDto[]>(`/api/comments/task/${taskId}`)
  } catch (error) {
    console.warn('Could not load comments.', error)
    comments.value = []
  }
}

async function loadAttachments(taskId: string) {
  try {
    attachments.value = await apiResult<AttachmentDto[]>(`/api/attachments/task/${taskId}`)
  } catch (error) {
    console.warn('Could not load attachments.', error)
    attachments.value = []
  }
}

async function loadTimeEntries(taskId: string) {
  try {
    const entries = await apiJson<TimeEntryDto[]>(`/api/tasks/${taskId}/time-entries`)
    timeEntries.value = entries
    activeTimer.value = entries.find(e => e.endedAt === null) ?? null
  } catch (error) {
    console.warn('Could not load time entries.', error)
    timeEntries.value = []
    activeTimer.value = null
  }
}

async function startTimer(taskId: string) {
  try {
    const entry = await apiJson<TimeEntryDto>(`/api/tasks/${taskId}/time-entries`, { method: 'POST' })
    activeTimer.value = entry
    await loadTimeEntries(taskId)
    showSuccess('Đã bắt đầu ghi thời gian')
  } catch (error) {
    showError(errorMessage(error, 'Không thể bắt đầu ghi thời gian'))
  }
}

async function stopTimer(entryId: string) {
  try {
    await apiJson<TimeEntryDto>(`/api/time-entries/${entryId}/stop`, { method: 'PATCH' })
    activeTimer.value = null
    if (selectedTaskId.value) await loadTimeEntries(selectedTaskId.value)
    showSuccess('Đã dừng ghi thời gian')
  } catch (error) {
    showError(errorMessage(error, 'Không thể dừng ghi thời gian'))
  }
}

async function addManualTimeEntry(taskId: string, manualMinutes: number, note: string) {
  if (!taskId || manualMinutes <= 0) return false

  try {
    await apiJson<TimeEntryDto>(`/api/tasks/${taskId}/time-entries/manual`, {
      method: 'POST',
      body: JSON.stringify({
        taskId,
        startedAt: new Date().toISOString(),
        manualMinutes,
        note: note.trim() || null,
      }),
    })

    await loadTimeEntries(taskId)
    await loadDashboard()
    showSuccess('Ghi thời gian thủ công thành công')
    return true
  } catch (error) {
    showError(errorMessage(error, 'Không thể ghi thời gian thủ công'))
    return false
  }
}

async function connectNotifications() {
  if (notificationConnectionStarted) return
  notificationConnectionStarted = true

  const connection = new HubConnectionBuilder()
    .withUrl('/hubs/notification')
    .withAutomaticReconnect()
    .build()

  connection.on('notificationReceived', (notification: NotificationDto) => {
    notifications.value = [notification, ...notifications.value.filter((item) => item.id !== notification.id)]
    showInfo(notification.message)
    void loadDashboard()
  })

  try {
    await connection.start()
  } catch (error) {
    console.warn('SignalR notification connection failed.', error)
  }
}

function selectProject(projectId: string) {
  activeProjectId.value = projectId
  selectedTaskId.value = projects.value.find((project) => project.id === projectId)?.tasks[0]?.id ?? null
  activeProjectTab.value = 'stats'
  void router.push(`/projects/${projectId}`)
}

function closeProjectDetails() {
  void router.push('/projects')
}

function selectTaskInProject(taskId: string) {
  const projectId = selectedProject.value?.id
  selectedTaskId.value = taskId
  activeProjectTab.value = 'tasks'

  if (projectId) {
    void router.push(`/projects/${projectId}/tasks/${taskId}`)
  }
}

function openTask(projectId: string, taskId: string) {
  activeProjectId.value = projectId
  selectedTaskId.value = taskId
  activeProjectTab.value = 'tasks'
  void router.push(`/projects/${projectId}/tasks/${taskId}`)
}

function openCreateProject() {
  createProjectOpen.value = true
  projectBeingEditedId.value = null
  void router.push('/projects')
}

async function createProject() {
  const name = projectName.value.trim()
  if (!name) return

  try {
    const project = await apiResult<ProjectDto>('/api/projects', {
      method: 'POST',
      body: JSON.stringify({
        name,
        description: projectDescription.value.trim() || null,
        startDate: null,
        endDate: projectEndDate.value ? new Date(projectEndDate.value).toISOString() : null,
      }),
    })

    clearProjectForm()
    createProjectOpen.value = false
    await loadDashboard()
    activeProjectId.value = project.id
    void router.push(`/projects/${project.id}`)
    showSuccess(`Thêm dự án "${project.name}" thành công`)
  } catch (error) {
    showError(errorMessage(error, 'Không thể thêm dự án'))
  }
}

function beginEditProject(projectId: string) {
  const project = projects.value.find((item) => item.id === projectId)
  if (!project) return

  activeProjectId.value = projectId
  createProjectOpen.value = false
  projectBeingEditedId.value = projectId
  editProjectName.value = project.name
  editProjectDescription.value = project.description ?? ''
}

async function saveProjectEdit() {
  const project = projects.value.find((item) => item.id === projectBeingEditedId.value)
  const name = editProjectName.value.trim()

  if (!project || !name) return

  try {
    await apiResult<ProjectDto>(`/api/projects/${project.id}`, {
      method: 'PUT',
      body: JSON.stringify({
        name,
        description: editProjectDescription.value.trim() || null,
        status: project.status,
        startDate: null,
        endDate: project.endDate,
      }),
    })

    projectBeingEditedId.value = null
    await loadDashboard()
    activeProjectId.value = project.id
    showSuccess(`Cập nhật dự án "${name}" thành công`)
  } catch (error) {
    showError(errorMessage(error, 'Không thể cập nhật dự án'))
  }
}

async function deleteProject(projectId: string) {
  const project = projects.value.find((item) => item.id === projectId)
  if (!project) return

  try {
    await apiCommand(`/api/projects/${projectId}`, { method: 'DELETE' })
    await loadDashboard()
    showSuccess(`Xóa dự án "${project.name}" thành công`)
  } catch (error) {
    showError(errorMessage(error, 'Không thể xóa dự án'))
  }
}

async function createTask() {
  const project = selectedProject.value
  const title = newTaskTitle.value.trim()
  if (!project || !title) return

  if (taskBeingEdited.value) {
    await saveTaskEdit()
    return
  }

  try {
    const task = await apiResult<{ aiPrioritySuggestion: string | null }>('/api/tasks', {
      method: 'POST',
      body: JSON.stringify({
        title,
        description: newTaskDescription.value.trim() || null,
        priority: newTaskPriority.value,
        dueDate: newTaskDueDate.value ? new Date(newTaskDueDate.value).toISOString() : null,
        estimatedHours: null,
        projectId: project.id,
        assigneeId: newTaskAssigneeId.value || null,
        isPrivate: false,
      }),
    })

    clearTaskForm()
    createTaskOpen.value = false
    await loadDashboard()
    showSuccess(task.aiPrioritySuggestion ? `Thêm nhiệm vụ thành công. ${task.aiPrioritySuggestion}` : 'Thêm nhiệm vụ thành công')
  } catch (error) {
    showError(errorMessage(error, 'Không thể thêm nhiệm vụ'))
  }
}

async function moveTask(task: DashboardTask, status: string) {
  try {
    await apiCommand(`/api/tasks/${task.id}/status`, {
      method: 'PATCH',
      body: JSON.stringify({ status }),
    })

    await loadDashboard()
    selectedTaskId.value = task.id
    showSuccess(`Đã chuyển nhiệm vụ sang ${displayStatus(status)}`)
  } catch (error) {
    showError(errorMessage(error, 'Không thể cập nhật trạng thái nhiệm vụ'))
  }
}

const taskBeingEdited = ref<DashboardTask | null>(null)

function beginEditTask(task: DashboardTask) {
  taskBeingEdited.value = task
  newTaskTitle.value = task.title
  newTaskDescription.value = '' 
  newTaskPriority.value = task.priority
  newTaskAssigneeId.value = '' 
  newTaskDueDate.value = task.dueDate ? new Date(task.dueDate).toISOString().split('T')[0] : ''
  createTaskOpen.value = true
}

async function saveTaskEdit() {
  if (!taskBeingEdited.value) return
  
  try {
    await apiResult<TaskItemDto>(`/api/tasks/${taskBeingEdited.value.id}`, {
      method: 'PUT',
      body: JSON.stringify({
        title: newTaskTitle.value.trim(),
        description: newTaskDescription.value.trim() || null,
        status: taskBeingEdited.value.status,
        priority: newTaskPriority.value,
        dueDate: newTaskDueDate.value ? new Date(newTaskDueDate.value).toISOString() : null,
        assigneeId: newTaskAssigneeId.value || null,
      }),
    })

    clearTaskForm()
    createTaskOpen.value = false
    taskBeingEdited.value = null
    await loadDashboard()
    showSuccess('Cập nhật nhiệm vụ thành công')
  } catch (error) {
    showError(errorMessage(error, 'Không thể cập nhật nhiệm vụ'))
  }
}

async function quickEditTaskTitle(taskId: string, title: string) {
  const nextTitle = title.trim()
  if (!taskId || !nextTitle) return false

  try {
    const currentTask = await apiResult<TaskItemDto>(`/api/tasks/${taskId}`)
    await apiResult<TaskItemDto>(`/api/tasks/${taskId}`, {
      method: 'PUT',
      body: JSON.stringify({
        title: nextTitle,
        description: currentTask.description,
        status: currentTask.status,
        priority: currentTask.priority,
        dueDate: currentTask.dueDate,
        estimatedHours: currentTask.estimatedHours,
        actualHours: currentTask.actualHours,
        assigneeId: currentTask.assigneeId,
        isPrivate: currentTask.isPrivate,
      }),
    })

    await loadDashboard()
    selectedTaskId.value = taskId
    showSuccess('Cập nhật nhiệm vụ thành công')
    return true
  } catch (error) {
    showError(errorMessage(error, 'Không thể cập nhật nhiệm vụ'))
    return false
  }
}

async function deleteTask(taskId: string) {
  if (!confirm('Bạn có chắc chắn muốn xóa task này?')) return

  try {
    await apiCommand(`/api/tasks/${taskId}`, { method: 'DELETE' })
    await loadDashboard()
    showSuccess('Xóa nhiệm vụ thành công')
  } catch (error) {
    showError(errorMessage(error, 'Không thể xóa nhiệm vụ'))
  }
}

async function submitComment() {
  const task = selectedTask.value
  const content = newComment.value.trim()
  if (!task || !content) return

  try {
    await apiResult<CommentDto>('/api/comments', {
      method: 'POST',
      body: JSON.stringify({
        taskItemId: task.id,
        content,
      }),
    })

    newComment.value = ''
    await loadComments(task.id)
    await loadDashboard()
    showSuccess('Thêm bình luận thành công')
  } catch (error) {
    showError(errorMessage(error, 'Không thể thêm bình luận'))
  }
}

async function deleteComment(commentId: string) {
  if (!confirm('Bạn có chắc chắn muốn xóa bình luận này?')) return

  try {
    await apiCommand(`/api/comments/${commentId}`, { method: 'DELETE' })
    if (selectedTask.value) await loadComments(selectedTask.value.id)
    await loadDashboard()
    showSuccess('Xóa bình luận thành công')
  } catch (error) {
    showError(errorMessage(error, 'Không thể xóa bình luận'))
  }
}

async function addMember(userId: string) {
  const project = selectedProject.value
  if (!project) return

  try {
    await apiCommand(`/api/projects/${project.id}/members`, {
      method: 'POST',
      body: JSON.stringify({ userId, role: 'Member' }),
    })
    await loadDashboard()
    showSuccess('Thêm thành viên thành công')
  } catch (error) {
    showError(errorMessage(error, 'Không thể thêm thành viên'))
  }
}

async function removeMember(userId: string) {
  const project = selectedProject.value
  if (!project) return
  if (!confirm('Bạn có chắc chắn muốn xóa thành viên này khỏi dự án?')) return

  try {
    await apiCommand(`/api/projects/${project.id}/members/${userId}`, { method: 'DELETE' })
    await loadDashboard()
    showSuccess('Xóa thành viên khỏi dự án thành công')
  } catch (error) {
    showError(errorMessage(error, 'Không thể xóa thành viên khỏi dự án'))
  }
}

async function updateMemberRole(userId: string, role: string) {
  const project = selectedProject.value
  if (!project) return

  try {
    await apiCommand(`/api/projects/${project.id}/members`, {
      method: 'POST',
      body: JSON.stringify({ userId, role }),
    })
    await loadDashboard()
    showSuccess(`Cập nhật vai trò thành ${role} thành công`)
  } catch (error) {
    showError(errorMessage(error, 'Không thể cập nhật vai trò thành viên'))
  }
}

async function loadWikiPages(projectId: string) {
  try {
    wikiPages.value = await apiResult<WikiPageDto[]>(`/api/projects/${projectId}/wiki`)
  } catch (error) {
    console.warn('Could not load wiki pages.', error)
    wikiPages.value = []
  }
}

async function createWikiPage(title: string, content: string = '') {
  const project = selectedProject.value
  if (!project || !title) return

  try {
    await apiResult<WikiPageDto>(`/api/projects/${project.id}/wiki`, {
      method: 'POST',
      body: JSON.stringify({ title, content }),
    })
    await loadWikiPages(project.id)
    showSuccess(`Tạo trang Wiki "${title}" thành công`)
  } catch (error) {
    showError(errorMessage(error, 'Không thể tạo trang Wiki'))
  }
}

async function updateWikiPage(pageId: string, title: string, content: string) {
  const project = selectedProject.value
  if (!project || !title) return

  try {
    await apiCommand(`/api/projects/${project.id}/wiki/${pageId}`, {
      method: 'PUT',
      body: JSON.stringify({ title, content }),
    })
    await loadWikiPages(project.id)
    showSuccess(`Cập nhật trang Wiki "${title}" thành công`)
  } catch (error) {
    showError(errorMessage(error, 'Không thể cập nhật trang Wiki'))
  }
}

async function deleteWikiPage(pageId: string) {
  const project = selectedProject.value
  if (!project) return

  try {
    await apiCommand(`/api/projects/${project.id}/wiki/${pageId}`, { method: 'DELETE' })
    await loadWikiPages(project.id)
    showSuccess('Xóa trang Wiki thành công')
  } catch (error) {
    showError(errorMessage(error, 'Không thể xóa trang Wiki'))
  }
}

async function uploadAttachment(event: Event) {
  const task = selectedTask.value
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  if (!task || !file) return

  const formData = new FormData()
  formData.append('file', file)

  try {
    await apiResult<AttachmentDto>(`/api/attachments/task/${task.id}`, {
      method: 'POST',
      body: formData,
    })

    input.value = ''
    await loadAttachments(task.id)
    await loadDashboard()
    showSuccess(`Tải tệp "${file.name}" lên thành công`)
  } catch (error) {
    showError(errorMessage(error, 'Không thể tải tệp lên'))
  }
}

async function deleteAttachment(attachment: AttachmentDto) {
  const taskId = selectedTask.value?.id

  try {
    await apiCommand(`/api/attachments/${attachment.id}`, { method: 'DELETE' })
    if (taskId) await loadAttachments(taskId)
    await loadDashboard()
    showSuccess(`Xóa tệp "${attachment.fileName}" thành công`)
  } catch (error) {
    showError(errorMessage(error, 'Không thể xóa tệp'))
  }
}

async function dismissNotification(notificationId: string) {
  notifications.value = notifications.value.filter((notification) => notification.id !== notificationId)
  dashboard.value.notifications = dashboard.value.notifications.filter((notification) => notification.id !== notificationId)

  if (isGuid(notificationId)) {
    try {
      await apiCommand(`/api/notifications/${notificationId}/read`, { method: 'PATCH' })
    } catch (error) {
      console.warn('Could not mark notification as read.', error)
      showError(errorMessage(error, 'Không thể đánh dấu thông báo đã đọc'))
    }
  }
}

async function clearActionableNotifications() {
  notifications.value = []
  dashboard.value.notifications = dashboard.value.notifications.filter((notification) => notification.tone === 'info')
  notificationsOpen.value = false

  try {
    await apiCommand('/api/notifications/read-all', { method: 'PATCH' })
    showSuccess('Đã đánh dấu tất cả thông báo là đã đọc')
  } catch (error) {
    console.warn('Could not mark notifications as read.', error)
    showError(errorMessage(error, 'Không thể đánh dấu tất cả thông báo đã đọc'))
  }
}

async function logout() {
  try {
    await apiCommand('/api/auth/logout', { method: 'POST' })
  } finally {
    window.location.href = '/Account/Login'
  }
}

async function apiJson<T>(url: string, options: RequestInit = {}): Promise<T> {
  const headers = new Headers(options.headers)

  if (options.body && !(options.body instanceof FormData)) {
    headers.set('Content-Type', 'application/json')
  }

  headers.set('Accept', 'application/json')

  const response = await fetch(url, {
    credentials: 'same-origin',
    ...options,
    headers,
  })

  if (response.status === 401) {
    window.location.href = `/Account/Login?returnUrl=${encodeURIComponent(window.location.pathname)}`
    throw new Error('Authentication required.')
  }

  const text = await response.text()
  const payload = parseApiPayload(text)

  if (!response.ok) {
    throw new Error(apiPayloadError(payload, response.status))
  }

  return payload as T
}

async function apiResult<T>(url: string, options: RequestInit = {}): Promise<T> {
  const result = await apiJson<ApiResult<T> | T>(url, options)

  if (isApiResult<T>(result)) {
    if (!result.isSuccess || result.data == null) {
      throw new Error(result.error ?? 'Không thể hoàn tất yêu cầu.')
    }

    return result.data
  }

  return result as T
}

async function apiCommand(url: string, options: RequestInit = {}) {
  const result = await apiJson<ApiResult<unknown> | { ok: boolean } | null>(url, options)

  if (isApiResult(result) && !result.isSuccess) {
    throw new Error(result.error ?? 'Không thể hoàn tất yêu cầu.')
  }

  if (result && typeof result === 'object' && 'ok' in result && result.ok === false) {
    throw new Error('Không thể hoàn tất yêu cầu.')
  }
}

function parseApiPayload(text: string) {
  if (!text) return null

  try {
    return JSON.parse(text)
  } catch {
    return text
  }
}

function apiPayloadError(payload: unknown, status: number) {
  if (typeof payload === 'string' && payload.trim()) return payload.trim()

  if (payload && typeof payload === 'object') {
    if ('error' in payload && typeof payload.error === 'string' && payload.error.trim()) return payload.error
    if ('message' in payload && typeof payload.message === 'string' && payload.message.trim()) return payload.message
    if ('title' in payload && typeof payload.title === 'string' && payload.title.trim()) {
      const validationMessage = validationErrorMessage(payload)
      return validationMessage ?? payload.title
    }
  }

  return `Không thể hoàn tất yêu cầu (mã ${status}).`
}

function validationErrorMessage(payload: object) {
  if (!('errors' in payload) || !payload.errors || typeof payload.errors !== 'object') return null

  for (const value of Object.values(payload.errors)) {
    if (Array.isArray(value) && typeof value[0] === 'string') return value[0]
    if (typeof value === 'string') return value
  }

  return null
}

function isApiResult<T>(payload: unknown): payload is ApiResult<T> {
  return Boolean(payload && typeof payload === 'object' && 'isSuccess' in payload)
}

function tasksByStatus(status: string) {
  const query = taskSearchQuery.value.trim().toLowerCase()
  return selectedProjectTasks.value.filter((task) => {
    if (task.status !== status) return false
    if (!query) return true
    return task.title.toLowerCase().includes(query)
  })
}

function nextStatuses(status: string) {
  switch (status) {
    case 'Todo':
      return ['InProgress']
    case 'InProgress':
      return ['InReview', 'Done']
    case 'InReview':
      return ['InProgress', 'Done']
    case 'Done':
      return ['InReview']
    default:
      return ['Todo']
  }
}

function clearProjectForm() {
  projectName.value = ''
  projectDescription.value = ''
  projectEndDate.value = ''
}

function clearTaskForm() {
  newTaskTitle.value = ''
  newTaskDescription.value = ''
  newTaskPriority.value = 'Medium'
  newTaskAssigneeId.value = ''
  newTaskDueDate.value = ''
}

function displayStatus(status: string | null | undefined) {
  switch (status) {
    case 'Active':
      return 'Active'
    case 'InProgress':
      return 'In progress'
    case 'InReview':
      return 'In review'
    case 'Done':
      return 'Done'
    case 'Planned':
      return 'Planned'
    case 'Archived':
      return 'Archived'
    case 'Todo':
      return 'Todo'
    case 'Cancelled':
      return 'Cancelled'
    default:
      return status || 'Unknown'
  }
}

function displayRole(role: string | null | undefined) {
  return role === 'Admin' ? 'Admin' : role === 'Member' ? 'Member' : role || ''
}

function statusTone(status: string) {
  switch (status) {
    case 'Active':
    case 'InProgress':
      return 'active'
    case 'Planned':
      return 'planned'
    case 'Archived':
      return 'archived'
    default:
      return 'neutral'
  }
}

function formatDate(value: string | null) {
  if (!value) return 'No date'

  return new Intl.DateTimeFormat('en', {
    month: 'short',
    day: 'numeric',
  }).format(new Date(value))
}

function formatTime(value: string) {
  return new Intl.DateTimeFormat('en', {
    hour: 'numeric',
    minute: '2-digit',
  }).format(new Date(value))
}

function formatFileSize(bytes: number) {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${Math.round(bytes / 1024)} KB`
  return `${(bytes / 1024 / 1024).toFixed(1)} MB`
}

function initials(name: string) {
  return name
    .split(' ')
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase() ?? '')
    .join('')
}

function isTaskOverdue(task: { dueDate: string | null; status: string }) {
  return Boolean(task.dueDate) && new Date(task.dueDate as string).getTime() < Date.now() && task.status !== 'Done'
}

function notificationClass(notification: DashboardNotification) {
  return `notice notice--${notification.tone}`
}

function toDashboardNotification(notification: NotificationDto): DashboardNotification {
  return {
    id: notification.id,
    title: notification.type,
    message: notification.message,
    tone: notification.type === 'DueDateReminder' ? 'critical' : notification.type === 'Info' ? 'info' : 'warning',
    createdAt: notification.createdAt,
  }
}

function isGuid(value: string) {
  return /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value)
}

function errorMessage(error: unknown, fallback = 'Đã xảy ra lỗi khi lưu dữ liệu') {
  const message = error instanceof Error ? error.message.trim() : ''

  if (
    !message ||
    message === 'Request failed.' ||
    message.startsWith('Request failed with status') ||
    message.startsWith('Không thể hoàn tất yêu cầu')
  ) {
    return fallback
  }

  return message
}

provide(dashboardContextKey, {
  activeProjectCards,
  activeProjectId,
  activeProjectTab,
  activeTaskMenu,
  addManualTimeEntry,
  addMember,
  archivedProjectCards,
  assignedTaskCards,
  attachments,
  beginEditProject,
  beginEditTask,
  clearActionableNotifications,
  closeProjectDetails,
  comments,
  createProject,
  createProjectOpen,
  createTask,
  createTaskOpen,
  currentUser,
  deleteAttachment,
  deleteComment,
  deleteProject,
  deleteTask,
  displayRole,
  displayStatus,
  editProjectDescription,
  editProjectName,
  filteredProjects,
  formatDate,
  formatFileSize,
  formatTime,
  isLoading,
  isProjectAdmin,
  isTaskOverdue,
  logout,
  moveTask,
  newComment,
  newTaskAssigneeId,
  newTaskDescription,
  newTaskDueDate,
  newTaskPriority,
  newTaskTitle,
  nextStatuses,
  openCreateProject,
  openTask,
  priorities,
  projectBeingEditedId,
  projectCards,
  projectDescription,
  projectEndDate,
  projectFilter,
  projectName,
  projectSort,
  projects,
  quickEditTaskTitle,
  removeMember,
  saveProjectEdit,
  searchQuery,
  selectProject,
  selectedProject,
  selectedProjectMembers,
  selectedProjectStats,
  selectedTask,
  selectedTaskId,
  selectTaskInProject,
  statusColumns,
  statusTone,
  submitComment,
  summaryCards,
  tabs,
  tasksByStatus,
  team,
  toggleTaskMenu,
  updateMemberRole,
  uploadAttachment,
  users,
  wikiPages,
  loadWikiPages,
  createWikiPage,
  updateWikiPage,
  deleteWikiPage,
  taskSearchQuery,
  taskBeingQuickEditedId,
  timeEntries,
  activeTimer,
  startTimer,
  stopTimer,
  loadTimeEntries,
})
</script>

<template>
  <AppShell
    :nav-items="navigation"
    :notification-count="notificationCount"
    :user-name="currentUser?.fullName || currentUser?.email || 'Qaly user'"
    :user-initials="initials(currentUser?.fullName || currentUser?.email || 'QU')"
    @notifications="notificationsOpen = !notificationsOpen"
    @assistant="() => {}"
    @logout="logout"
  >
    <RouterView />

      <div v-if="notificationsOpen" class="notification-popover glass-card home-notification-popover">
        <div class="panel-heading">
          <div>
            <span>Notifications</span>
            <h2>Current signals</h2>
          </div>
          <div class="popover-actions">
            <button class="text-button" type="button" @click="clearActionableNotifications">Read all</button>
            <button class="icon-button icon-button--small" type="button" @click="notificationsOpen = false">
              <X :size="16" />
            </button>
          </div>
        </div>
        <article
          v-for="notification in notificationItems.slice(0, 6)"
          :key="notification.id"
          :class="notificationClass(notification)"
        >
          <div class="notice__top">
            <strong>{{ notification.title }}</strong>
            <button type="button" aria-label="Dismiss notification" @click="dismissNotification(notification.id)">
              <X :size="14" />
            </button>
          </div>
          <p>{{ notification.message }}</p>
          <span>{{ formatTime(notification.createdAt) }}</span>
        </article>
      </div>

    <FloatingChatbot />
  </AppShell>
</template>
