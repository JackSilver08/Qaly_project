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
import WelcomeOverlay from './components/WelcomeOverlay.vue'
import { dashboardContextKey } from './composables/dashboard-context'
import { showError, showInfo, showSuccess } from './composables/use-toast'
import { useDashboard } from './composables/use-dashboard-state'
import { useProjectActions } from './composables/use-project-actions'
import { useTaskActions } from './composables/use-task-actions'
import { apiCommand, apiJson, apiResult, errorMessage } from './utils/api-client'
import {
  displayRole,
  displayStatus,
  formatDate,
  formatFileSize,
  formatTime,
  initials,
  isTaskOverdue,
  statusTone,
} from './utils/formatters'
import type { ProjectCardModel, SummaryCardModel, TaskListItemModel } from './components/dashboard-models'
import type { ShellNavItem } from './components/shell-models'
import type {
  AttachmentDto,
  CommentDto,
  DashboardNotification,
  DashboardProject,
  DashboardTask,
  NotificationDto,
  WikiPageDto,
  TimeEntryDto,
} from './types'

const {
  dashboard,
  currentUser,
  users,
  isLoading,
  usingFallback,
  projects,
  team,
  summaryCards,
  loadDashboard,
  loadMe,
  loadUsers,
} = useDashboard()

const activeProjectId = ref<string | null>(null)
const selectedTaskId = ref<string | null>(null)

const {
  createProjectOpen,
  projectName,
  projectDescription,
  projectEndDate,
  editProjectName,
  editProjectDescription,
  projectBeingEditedId,
  openCreateProject,
  createProject,
  beginEditProject,
  saveProjectEdit,
  deleteProject,
  selectProject: baseSelectProject,
} = useProjectActions(projects, activeProjectId, loadDashboard)

const {
  createTaskOpen,
  taskBeingEdited,
  newTaskTitle,
  newTaskDescription,
  newTaskPriority,
  newTaskAssigneeId,
  newTaskDueDate,
  createTask: baseCreateTask,
  moveTask,
  beginEditTask,
  saveTaskEdit,
  deleteTask,
} = useTaskActions(selectedTaskId, loadDashboard)

const navigation: ShellNavItem[] = [
  { label: 'Tổng quan', to: '/dashboard', icon: LayoutDashboard },
  { label: 'Dự án', to: '/projects', icon: FolderKanban },
  { label: 'Nhiệm vụ', to: '/tasks', icon: ClipboardList },
  { label: 'Nhóm', to: '/teams', icon: Users },
]

const statusColumns = ['Todo', 'InProgress', 'InReview', 'Done']
const priorities = ['Low', 'Medium', 'High', 'Critical']

const notifications = ref<NotificationDto[]>([])
const comments = ref<CommentDto[]>([])
const attachments = ref<AttachmentDto[]>([])
const wikiPages = ref<WikiPageDto[]>([])
const timeEntries = ref<TimeEntryDto[]>([])
const activeTimer = ref<TimeEntryDto | null>(null)

const notificationsOpen = ref(false)
const taskSearchQuery = ref('')
const taskBeingQuickEditedId = ref<string | null>(null)
const activeTaskMenu = ref<string | null>(null)

function toggleTaskMenu(taskId: string) {
  activeTaskMenu.value = activeTaskMenu.value === taskId ? null : taskId
}
const searchQuery = ref('')
const projectFilter = ref<'all' | 'active' | 'planned' | 'at-risk'>('all')
const projectSort = ref<'recent' | 'risk' | 'progress' | 'name'>('recent')
const activeProjectTab = ref('stats')

const tabs = [
  { id: 'stats', label: 'Thống kê' },
  { id: 'tasks', label: 'Task' },
  { id: 'members', label: 'Member' },
  { id: 'wiki', label: 'Wiki' },
]

const newComment = ref('')

let notificationConnectionStarted = false
const router = useRouter()
const route = useRoute()

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

const projectCards = computed<ProjectCardModel[]>(() => filteredProjects.value.map(toProjectCard))
const activeProjectCards = computed<ProjectCardModel[]>(() => filteredProjects.value.filter((p) => p.status !== 'Archived').map(toProjectCard))
const archivedProjectCards = computed<ProjectCardModel[]>(() => filteredProjects.value.filter((p) => p.status === 'Archived').map(toProjectCard))

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
  return member ? ['owner', 'manager'].includes(String(member.role || '').toLowerCase()) : false
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
  const done = tasks.filter(t => t.status === 'Done').length
  return {
    total,
    todo: tasks.filter(t => t.status === 'Todo').length,
    inProgress: tasks.filter(t => t.status === 'InProgress').length,
    inReview: tasks.filter(t => t.status === 'InReview').length,
    done,
    overdue: tasks.filter(t => isTaskOverdue(t)).length,
    completionRate: total > 0 ? Math.round((done / total) * 100) : 0
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
  () => notifications.value.filter((n) => !n.isRead).length +
    dashboard.value.notifications.filter((n) => n.tone !== 'info').length,
)

watch(filteredProjects, (items) => {
  if (!['dashboard', 'projects'].includes(String(route.name ?? ''))) return
  if (!items.some((p) => p.id === activeProjectId.value)) {
    activeProjectId.value = items[0]?.id ?? projects.value[0]?.id ?? null
  }
}, { immediate: true })

watch(() => route.params.projectId, (id) => { if (typeof id === 'string') activeProjectId.value = id }, { immediate: true })
watch(() => route.params.taskId, (id) => { if (typeof id === 'string') { selectedTaskId.value = id; activeProjectTab.value = 'tasks' } }, { immediate: true })

watch(selectedTask, (task) => {
  selectedTaskId.value = task?.id ?? null
  if (task && !usingFallback.value) {
    void loadComments(task.id); void loadAttachments(task.id); void loadTimeEntries(task.id)
  } else {
    comments.value = []; attachments.value = []; timeEntries.value = []
  }
}, { immediate: true })

watch(() => [activeProjectId.value, activeProjectTab.value], ([id, tab]) => {
  if (id && tab === 'wiki' && !usingFallback.value) void loadWikiPages(String(id))
}, { immediate: true })

onMounted(async () => {
  await Promise.all([loadMe(), loadDashboard(), loadUsers(), loadNotifications()])
  await connectNotifications()
})

async function loadNotifications() {
  try { notifications.value = await apiResult<NotificationDto[]>('/api/notifications') } catch (e) { console.warn(e) }
}

async function loadComments(taskId: string) {
  try { comments.value = await apiResult<CommentDto[]>(`/api/comments/task/${taskId}`) } catch (e) { comments.value = [] }
}

async function loadAttachments(taskId: string) {
  try { attachments.value = await apiResult<AttachmentDto[]>(`/api/attachments/task/${taskId}`) } catch (e) { attachments.value = [] }
}

async function loadTimeEntries(taskId: string) {
  try {
    const entries = await apiJson<TimeEntryDto[]>(`/api/tasks/${taskId}/time-entries`)
    timeEntries.value = entries
    activeTimer.value = entries.find(e => e.endedAt === null) ?? null
  } catch (e) { timeEntries.value = []; activeTimer.value = null }
}

async function startTimer(taskId: string) {
  try {
    activeTimer.value = await apiJson<TimeEntryDto>(`/api/tasks/${taskId}/time-entries`, { method: 'POST' })
    await loadTimeEntries(taskId)
    showSuccess('Đã bắt đầu ghi thời gian')
  } catch (e) { showError(errorMessage(e, 'Không thể bắt đầu')) }
}

async function stopTimer(entryId: string) {
  try {
    await apiJson<TimeEntryDto>(`/api/time-entries/${entryId}/stop`, { method: 'PATCH' })
    activeTimer.value = null
    if (selectedTaskId.value) await loadTimeEntries(selectedTaskId.value)
    showSuccess('Đã dừng ghi thời gian')
  } catch (e) { showError(errorMessage(e, 'Không thể dừng')) }
}

async function addManualTimeEntry(taskId: string, minutes: number, note: string) {
  if (!taskId || minutes <= 0) return false
  try {
    await apiJson<TimeEntryDto>(`/api/tasks/${taskId}/time-entries/manual`, {
      method: 'POST',
      body: JSON.stringify({ taskId, startedAt: new Date().toISOString(), manualMinutes: minutes, note: note.trim() || null }),
    })
    await loadTimeEntries(taskId); await loadDashboard(); showSuccess('Thành công')
    return true
  } catch (e) { showError(errorMessage(e, 'Lỗi')); return false }
}

async function connectNotifications() {
  if (notificationConnectionStarted) return
  notificationConnectionStarted = true
  const connection = new HubConnectionBuilder().withUrl('/hubs/notification').withAutomaticReconnect().build()
  connection.on('notificationReceived', (n: NotificationDto) => {
    notifications.value = [n, ...notifications.value.filter((item) => item.id !== n.id)]
    showInfo(n.message); void loadDashboard()
  })
  try { await connection.start() } catch (e) { console.warn(e) }
}

function selectProject(id: string) {
  baseSelectProject(id)
  selectedTaskId.value = projects.value.find((p) => p.id === id)?.tasks[0]?.id ?? null
  activeProjectTab.value = 'stats'
}

function closeProjectDetails() { void router.push('/projects') }
function selectTaskInProject(id: string) {
  selectedTaskId.value = id; activeProjectTab.value = 'tasks'
  if (selectedProject.value?.id) void router.push(`/projects/${selectedProject.value.id}/tasks/${id}`)
}

function openTask(pId: string, tId: string) {
  activeProjectId.value = pId; selectedTaskId.value = tId; activeProjectTab.value = 'tasks'
  void router.push(`/projects/${pId}/tasks/${tId}`)
}

async function createTask() { if (selectedProject.value) await baseCreateTask(selectedProject.value.id) }

async function quickEditTaskTitle(taskId: string, title: string) {
  const t = title.trim()
  if (!taskId || !t) return false
  try {
    const current = await apiResult<any>(`/api/tasks/${taskId}`)
    await apiResult<any>(`/api/tasks/${taskId}`, { method: 'PUT', body: JSON.stringify({ ...current, title: t }) })
    await loadDashboard(); selectedTaskId.value = taskId; showSuccess('Thành công')
    return true
  } catch (e) { showError(errorMessage(e, 'Lỗi')); return false }
}

async function submitComment() {
  const t = selectedTask.value; const c = newComment.value.trim()
  if (!t || !c) return
  try {
    await apiResult<any>('/api/comments', { method: 'POST', body: JSON.stringify({ taskItemId: t.id, content: c }) })
    newComment.value = ''; await loadComments(t.id); await loadDashboard(); showSuccess('Thành công')
  } catch (e) { showError(errorMessage(e, 'Lỗi')) }
}

async function deleteComment(id: string) {
  if (!confirm('Xóa?')) return
  try {
    await apiCommand(`/api/comments/${id}`, { method: 'DELETE' })
    if (selectedTask.value) await loadComments(selectedTask.value.id)
    await loadDashboard(); showSuccess('Thành công')
  } catch (e) { showError(errorMessage(e, 'Lỗi')) }
}

async function addMember(uId: string) {
  if (!selectedProject.value) return
  try {
    await apiCommand(`/api/projects/${selectedProject.value.id}/members`, { method: 'POST', body: JSON.stringify({ userId: uId, role: 'Member' }) })
    await loadDashboard(); showSuccess('Thành công')
  } catch (e) { showError(errorMessage(e, 'Lỗi')) }
}

async function removeMember(uId: string) {
  if (!selectedProject.value || !confirm('Xóa?')) return
  try {
    await apiCommand(`/api/projects/${selectedProject.value.id}/members/${uId}`, { method: 'DELETE' })
    await loadDashboard(); showSuccess('Thành công')
  } catch (e) { showError(errorMessage(e, 'Lỗi')) }
}

async function updateMemberRole(uId: string, role: string) {
  if (!selectedProject.value) return
  try {
    await apiCommand(`/api/projects/${selectedProject.value.id}/members`, { method: 'POST', body: JSON.stringify({ userId: uId, role }) })
    await loadDashboard(); showSuccess('Thành công')
  } catch (e) { showError(errorMessage(e, 'Lỗi')) }
}

async function loadWikiPages(id: string) {
  try { wikiPages.value = await apiResult<WikiPageDto[]>(`/api/projects/${id}/wiki`) } catch (e) { wikiPages.value = [] }
}

async function createWikiPage(title: string, content: string = '') {
  if (!selectedProject.value || !title) return
  try {
    await apiResult<any>(`/api/projects/${selectedProject.value.id}/wiki`, { method: 'POST', body: JSON.stringify({ title, content }) })
    await loadWikiPages(selectedProject.value.id); showSuccess('Thành công')
  } catch (e) { showError(errorMessage(e, 'Lỗi')) }
}

async function updateWikiPage(id: string, title: string, content: string) {
  if (!selectedProject.value || !title) return
  try {
    await apiCommand(`/api/projects/${selectedProject.value.id}/wiki/${id}`, { method: 'PUT', body: JSON.stringify({ title, content }) })
    await loadWikiPages(selectedProject.value.id); showSuccess('Thành công')
  } catch (e) { showError(errorMessage(e, 'Lỗi')) }
}

async function deleteWikiPage(id: string) {
  if (!selectedProject.value) return
  try {
    await apiCommand(`/api/projects/${selectedProject.value.id}/wiki/${id}`, { method: 'DELETE' })
    await loadWikiPages(selectedProject.value.id); showSuccess('Thành công')
  } catch (e) { showError(errorMessage(e, 'Lỗi')) }
}

async function uploadAttachment(e: Event) {
  const t = selectedTask.value; const f = (e.target as HTMLInputElement).files?.[0]
  if (!t || !f) return
  const data = new FormData(); data.append('file', f)
  try {
    await apiResult<any>(`/api/attachments/task/${t.id}`, { method: 'POST', body: data })
    await loadAttachments(t.id); await loadDashboard(); showSuccess('Thành công')
  } catch (err) { showError(errorMessage(err, 'Lỗi')) }
}

async function deleteAttachment(a: any) {
  try {
    await apiCommand(`/api/attachments/${a.id}`, { method: 'DELETE' })
    if (selectedTask.value) await loadAttachments(selectedTask.value.id)
    await loadDashboard(); showSuccess('Thành công')
  } catch (err) { showError(errorMessage(err, 'Lỗi')) }
}

async function dismissNotification(id: string) {
  notifications.value = notifications.value.filter((n) => n.id !== id)
  dashboard.value.notifications = dashboard.value.notifications.filter((n) => n.id !== id)
  if (isGuid(id)) try { await apiCommand(`/api/notifications/${id}/read`, { method: 'PATCH' }) } catch (e) { console.warn(e) }
}

async function clearActionableNotifications() {
  notifications.value = []; dashboard.value.notifications = dashboard.value.notifications.filter((n) => n.tone === 'info')
  notificationsOpen.value = false
  try { await apiCommand('/api/notifications/read-all', { method: 'PATCH' }); showSuccess('Thành công') } catch (e) { console.warn(e) }
}

async function logout() { try { await apiCommand('/api/auth/logout', { method: 'POST' }) } finally { window.location.href = '/Account/Login' } }

function tasksByStatus(status: string) {
  const query = taskSearchQuery.value.trim().toLowerCase()
  return selectedProjectTasks.value.filter((t) => (t.status === status) && (!query || t.title.toLowerCase().includes(query)))
}

function nextStatuses(status: string) {
  const map: Record<string, string[]> = { Todo: ['InProgress'], InProgress: ['InReview', 'Done'], InReview: ['InProgress', 'Done'], Done: ['InReview'] }
  return map[status] || ['Todo']
}

function toDashboardNotification(n: NotificationDto): DashboardNotification {
  return { id: n.id, title: n.type, message: n.message, tone: n.type === 'DueDateReminder' ? 'critical' : n.type === 'Info' ? 'info' : 'warning', createdAt: n.createdAt }
}

function isGuid(v: string) { return /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(v) }

provide(dashboardContextKey, {
  activeProjectCards, activeProjectId, activeProjectTab, activeTaskMenu, addManualTimeEntry, addMember, archivedProjectCards, assignedTaskCards,
  attachments, beginEditProject, beginEditTask, clearActionableNotifications, closeProjectDetails, comments, createProject, createProjectOpen,
  createTask, createTaskOpen, currentUser, deleteAttachment, deleteComment, deleteProject, deleteTask, displayRole, displayStatus,
  editProjectDescription, editProjectName, filteredProjects, formatDate, formatFileSize, formatTime, isLoading, isProjectAdmin, isTaskOverdue,
  logout, moveTask, newComment, newTaskAssigneeId, newTaskDescription, newTaskDueDate, newTaskPriority, newTaskTitle, nextStatuses,
  openCreateProject, openTask, priorities, projectBeingEditedId, projectCards, projectDescription, projectEndDate, projectFilter,
  projectName, projectSort, projects, quickEditTaskTitle, removeMember, saveProjectEdit, searchQuery, selectProject, selectedProject,
  selectedProjectMembers, selectedProjectStats, selectedTask, selectedTaskId, selectTaskInProject, statusColumns, statusTone,
  submitComment, summaryCards, tabs, tasksByStatus, team, toggleTaskMenu, updateMemberRole, uploadAttachment, users, wikiPages,
  loadWikiPages, createWikiPage, updateWikiPage, deleteWikiPage, taskSearchQuery, taskBeingQuickEditedId, timeEntries, activeTimer,
  startTimer, stopTimer, loadTimeEntries,
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
          :class="`notice notice--${notification.tone}`"
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
    <WelcomeOverlay />
  </AppShell>
</template>
