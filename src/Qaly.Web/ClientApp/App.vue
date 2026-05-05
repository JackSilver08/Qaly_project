<script setup lang="ts">
import { computed, nextTick, onMounted, provide, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { HubConnectionBuilder } from '@microsoft/signalr'
import MarkdownIt from 'markdown-it'
import DOMPurify from 'dompurify'
import {
  ClipboardList,
  FolderKanban,
  LayoutDashboard,
  Users,
  X,
} from 'lucide-vue-next'
import AppShell from './components/AppShell.vue'
import ChatbotAvatar from './components/ChatbotAvatar.vue'
import { dashboardContextKey } from './composables/dashboard-context'
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
} from './types'

interface ChatMessage {
  id: string
  role: 'assistant' | 'user'
  text: string
}

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
const isLoading = ref(true)
const usingFallback = ref(true)
const chatOpen = ref(false)
const notificationsOpen = ref(false)
const createProjectOpen = ref(false)
const createTaskOpen = ref(false)
const projectBeingEditedId = ref<string | null>(null)
const selectedTaskId = ref<string | null>(null)
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
const actionNotice = ref('')
const chatDraft = ref('')
const showProjectSuggestions = ref(false)
const chatBodyRef = ref<HTMLElement | null>(null)

const projectSuggestions = computed(() => {
  const parts = chatDraft.value.split(' ')
  const lastPart = parts[parts.length - 1]
  if (lastPart.startsWith('@')) {
    const query = lastPart.slice(1).toLowerCase()
    return projects.value.filter(p => p.name.toLowerCase().includes(query))
  }
  return []
})

watch(chatDraft, (val) => {
  const parts = val.split(' ')
  const lastPart = parts[parts.length - 1]
  showProjectSuggestions.value = lastPart.startsWith('@')
})

function tagProject(project: DashboardProject) {
  const parts = chatDraft.value.split(' ')
  parts[parts.length - 1] = `@${project.name} `
  chatDraft.value = parts.join(' ')
  showProjectSuggestions.value = false
}

const markdownRenderer = new (MarkdownIt as any)({
  html: false,
  linkify: true,
  typographer: true
})

function renderMarkdown(content: string) {
  return DOMPurify.sanitize(markdownRenderer.render(content))
}

const isAssistantThinking = ref(false)
const chatMessages = ref<ChatMessage[]>([
  {
    id: 'assistant-welcome',
    role: 'assistant',
    text: 'Ask me about project risk, overdue work, priority, or assignment suggestions.',
  },
])

let actionNoticeTimer: number | undefined
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

const selectedProjectSummary = computed(() => {
  const project = selectedProject.value
  if (!project) return 'Select a project to inspect its work.'
  return `${project.name}: ${project.completedTaskCount}/${project.taskCount} tasks complete.`
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

const quickPrompts = computed(() => {
  const prompts: string[] = []
  
  // 1. Gợi ý cho dự án đang chọn
  if (selectedProject.value) {
    prompts.push(`Tóm tắt dự án ${selectedProject.value.name}`)
  }

  // 2. Gợi ý cho dự án MỚI NHẤT
  const latestProject = [...projects.value]
    .sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime())[0]
  
  if (latestProject && latestProject.id !== selectedProject.value?.id) {
    prompts.push(`Xem dự án mới: ${latestProject.name}`)
  }

  // 3. Gợi ý dựa trên rủi ro (quá hạn)
  const highRiskProject = projects.value.find(p => p.overdueTaskCount > 0)
  if (highRiskProject) {
    prompts.push(`Phân tích rủi ro ${highRiskProject.name}`)
  }

  // 4. Gợi ý chung
  prompts.push('Tôi nên làm gì tiếp theo?')
  prompts.push('Phân bổ công việc có đều không?')

  // Trả về tối đa 3 gợi ý để đảm bảo giao diện đẹp
  return prompts.slice(0, 3)
})

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
    } else {
      comments.value = []
      attachments.value = []
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

watch(
  () => [chatMessages.value.length, isAssistantThinking.value],
  () => {
    void scrollChatToBottom()
  },
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

async function connectNotifications() {
  if (notificationConnectionStarted) return
  notificationConnectionStarted = true

  const connection = new HubConnectionBuilder()
    .withUrl('/hubs/notification')
    .withAutomaticReconnect()
    .build()

  connection.on('notificationReceived', (notification: NotificationDto) => {
    notifications.value = [notification, ...notifications.value.filter((item) => item.id !== notification.id)]
    showActionNotice(notification.message)
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
    showActionNotice(`Created project "${project.name}".`)
  } catch (error) {
    showActionNotice(errorMessage(error))
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
    showActionNotice(`Updated project "${name}".`)
  } catch (error) {
    showActionNotice(errorMessage(error))
  }
}

async function deleteProject(projectId: string) {
  const project = projects.value.find((item) => item.id === projectId)
  if (!project) return

  try {
    await apiCommand(`/api/projects/${projectId}`, { method: 'DELETE' })
    await loadDashboard()
    showActionNotice(`Deleted project "${project.name}".`)
  } catch (error) {
    showActionNotice(errorMessage(error))
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
    showActionNotice(task.aiPrioritySuggestion ?? 'Task created.')
  } catch (error) {
    showActionNotice(errorMessage(error))
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
    showActionNotice(`Moved "${task.title}" to ${displayStatus(status)}.`)
  } catch (error) {
    showActionNotice(errorMessage(error))
  }
}

const taskBeingEdited = ref<DashboardTask | null>(null)

function beginEditTask(task: DashboardTask) {
  taskBeingEdited.value = task
  newTaskTitle.value = task.title
  newTaskDescription.value = '' // We don't have desc in DashboardTask, but we could load it if needed
  newTaskPriority.value = task.priority
  newTaskAssigneeId.value = '' // Need to find assignee ID
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
    showActionNotice('Task updated.')
  } catch (error) {
    showActionNotice(errorMessage(error))
  }
}

async function deleteTask(taskId: string) {
  if (!confirm('Bạn có chắc chắn muốn xóa task này?')) return

  try {
    await apiCommand(`/api/tasks/${taskId}`, { method: 'DELETE' })
    await loadDashboard()
    showActionNotice('Task deleted.')
  } catch (error) {
    showActionNotice(errorMessage(error))
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
  } catch (error) {
    showActionNotice(errorMessage(error))
  }
}

async function deleteComment(commentId: string) {
  if (!confirm('Bạn có chắc chắn muốn xóa bình luận này?')) return

  try {
    await apiCommand(`/api/comments/${commentId}`, { method: 'DELETE' })
    if (selectedTask.value) await loadComments(selectedTask.value.id)
    await loadDashboard()
    showActionNotice('Comment deleted.')
  } catch (error) {
    showActionNotice(errorMessage(error))
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
    showActionNotice('Member added.')
  } catch (error) {
    showActionNotice(errorMessage(error))
  }
}

async function removeMember(userId: string) {
  const project = selectedProject.value
  if (!project) return
  if (!confirm('Bạn có chắc chắn muốn xóa thành viên này khỏi dự án?')) return

  try {
    await apiCommand(`/api/projects/${project.id}/members/${userId}`, { method: 'DELETE' })
    await loadDashboard()
    showActionNotice('Member removed.')
  } catch (error) {
    showActionNotice(errorMessage(error))
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
    showActionNotice(`Updated role to ${role}.`)
  } catch (error) {
    showActionNotice(errorMessage(error))
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
    showActionNotice(`Created wiki page "${title}".`)
  } catch (error) {
    showActionNotice(errorMessage(error))
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
    showActionNotice(`Updated wiki page "${title}".`)
  } catch (error) {
    showActionNotice(errorMessage(error))
  }
}

async function deleteWikiPage(pageId: string) {
  const project = selectedProject.value
  if (!project) return

  try {
    await apiCommand(`/api/projects/${project.id}/wiki/${pageId}`, { method: 'DELETE' })
    await loadWikiPages(project.id)
    showActionNotice('Wiki page deleted.')
  } catch (error) {
    showActionNotice(errorMessage(error))
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
    showActionNotice(`Uploaded "${file.name}".`)
  } catch (error) {
    showActionNotice(errorMessage(error))
  }
}

async function deleteAttachment(attachment: AttachmentDto) {
  const taskId = selectedTask.value?.id

  try {
    await apiCommand(`/api/attachments/${attachment.id}`, { method: 'DELETE' })
    if (taskId) await loadAttachments(taskId)
    await loadDashboard()
    showActionNotice(`Deleted "${attachment.fileName}".`)
  } catch (error) {
    showActionNotice(errorMessage(error))
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
    }
  }
}

async function clearActionableNotifications() {
  notifications.value = []
  dashboard.value.notifications = dashboard.value.notifications.filter((notification) => notification.tone === 'info')
  notificationsOpen.value = false

  try {
    await apiCommand('/api/notifications/read-all', { method: 'PATCH' })
  } catch (error) {
    console.warn('Could not mark notifications as read.', error)
  }
}

async function logout() {
  try {
    await apiCommand('/api/auth/logout', { method: 'POST' })
  } finally {
    window.location.href = '/Account/Login'
  }
}

function openChatWithPrompt(prompt?: string) {
  chatOpen.value = true
  if (prompt) void submitChat(prompt)
  else void scrollChatToBottom()
}

async function submitChat(explicitPrompt?: string) {
  const prompt = (explicitPrompt ?? chatDraft.value).trim()
  if (!prompt || isAssistantThinking.value) return

  chatMessages.value.push({ id: `user-${Date.now()}`, role: 'user', text: prompt })
  chatDraft.value = ''
  isAssistantThinking.value = true

  // Extract project ID if tagged with @
  let taggedProjectId: string | null = null
  const tagMatch = prompt.match(/@([\w\s]+)/)
  if (tagMatch) {
    const taggedName = tagMatch[1].trim().toLowerCase()
    const project = projects.value.find(p => p.name.toLowerCase() === taggedName)
    if (project) {
      taggedProjectId = project.id
    }
  }

  try {
    const assistantMsgId = `assistant-${Date.now()}`
    chatMessages.value.push({ id: assistantMsgId, role: 'assistant', text: '' })
    
    const response = await fetch('/api/ai/chat/stream', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        message: prompt,
        projectId: taggedProjectId ?? selectedProject.value?.id ?? null,
      }),
    })

    if (!response.ok) throw new Error('Streaming failed')

    const reader = response.body?.getReader()
    const decoder = new TextDecoder()
    let assistantReply = ''

    if (reader) {
      isAssistantThinking.value = false
      
      while (true) {
        const { done, value } = await reader.read()
        if (done) break
        
        const chunk = decoder.decode(value, { stream: true })
        assistantReply += chunk
        
        const msgIndex = chatMessages.value.findIndex(m => m.id === assistantMsgId)
        if (msgIndex !== -1) {
          chatMessages.value[msgIndex].text = assistantReply
        }
        void scrollChatToBottom()
      }
    }
  } catch (error) {
    chatMessages.value.push({ id: `assistant-${Date.now()}`, role: 'assistant', text: createAssistantReply(prompt) })
  } finally {
    isAssistantThinking.value = false
  }
}

async function scrollChatToBottom() {
  await nextTick()
  chatBodyRef.value?.scrollTo({ top: chatBodyRef.value.scrollHeight, behavior: 'smooth' })
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
  const payload = text ? JSON.parse(text) : null

  if (!response.ok) {
    throw new Error(payload?.error ?? `Request failed with status ${response.status}`)
  }

  return payload as T
}

async function apiResult<T>(url: string, options: RequestInit = {}): Promise<T> {
  const result = await apiJson<ApiResult<T>>(url, options)
  if (!result.isSuccess || result.data == null) {
    throw new Error(result.error ?? 'Request failed.')
  }

  return result.data
}

async function apiCommand(url: string, options: RequestInit = {}) {
  const result = await apiJson<ApiResult<unknown> | { ok: boolean }>(url, options)

  if ('isSuccess' in result && !result.isSuccess) {
    throw new Error(result.error ?? 'Request failed.')
  }
}

function tasksByStatus(status: string) {
  return selectedProjectTasks.value.filter((task) => task.status === status)
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

function createAssistantReply(prompt: string) {
  const query = prompt.toLowerCase()
  const project = selectedProject.value

  const keywords = [
    'risk', 'rủi ro', 'rui ro', 'summary', 'tóm tắt', 'tom tat', 'overdue', 'quá hạn', 'qua han',
    'priority', 'ưu tiên', 'u tien', 'assignment', 'phân công', 'phan cong', 'task', 'công việc', 'cong viec',
    'project', 'dự án', 'du an', 'status', 'trạng thái', 'trang thai', 'deadline', 'hạn', 'han chot',
    'progress', 'tiến độ', 'tien do', 'member', 'thành viên', 'thanh vien', 'done', 'hoàn thành', 'hoan thanh',
    'todo', 'cần làm', 'can lam', 'doing', 'đang làm', 'dang lam'
  ]
  const isRelevant = keywords.some(k => query.includes(k))

  if (!isRelevant) {
    return 'Tao đéo biết'
  }

  if (query.includes('risk') || query.includes('rủi ro') || query.includes('rui ro')) {
    return `${overdueTasks.value} tasks are overdue across the workspace. Review ${project?.name ?? 'the highest-risk project'} first.`
  }

  const task = selectedProjectTasks.value.find((item) => item.status !== 'Done')
  return task ? `Next candidate: "${task.title}" in ${project?.name}.` : selectedProjectSummary.value
}

function showActionNotice(message: string) {
  actionNotice.value = message

  if (actionNoticeTimer) window.clearTimeout(actionNoticeTimer)
  actionNoticeTimer = window.setTimeout(() => {
    actionNotice.value = ''
  }, 3800)
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
  return /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value)
}

function errorMessage(error: unknown) {
  return error instanceof Error ? error.message : 'Request failed.'
}

provide(dashboardContextKey, {
  actionNotice,
  activeProjectCards,
  activeProjectId,
  activeProjectTab,
  activeTaskMenu,
  addMember,
  archivedProjectCards,
  assignedTaskCards,
  attachments,
  beginEditProject,
  beginEditTask,
  chatDraft,
  chatMessages,
  chatOpen,
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
  isAssistantThinking,
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
  openChatWithPrompt,
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
  projectSuggestions,
  projects,
  quickPrompts,
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
  showProjectSuggestions,
  statusColumns,
  statusTone,
  submitChat,
  submitComment,
  summaryCards,
  tabs,
  tagProject,
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
})
</script>

<template>
  <AppShell
    v-model:search="searchQuery"
    :nav-items="navigation"
    :notification-count="notificationCount"
    :user-name="currentUser?.fullName ?? 'Qaly user'"
    :user-initials="initials(currentUser?.fullName ?? 'QU')"
    @create="openCreateProject"
    @notifications="notificationsOpen = !notificationsOpen"
    @assistant="openChatWithPrompt()"
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

    <div v-if="actionNotice" class="action-toast">{{ actionNotice }}</div>

    <aside class="chat-drawer glass-card" :class="{ 'is-open': chatOpen }">
      <div class="chat-drawer__header">
        <div class="chat-drawer__identity">
          <ChatbotAvatar size="medium" />
          <div>
            <span>AI assistant</span>
            <h2>Qaly assistant</h2>
          </div>
        </div>
        <button class="icon-button" type="button" @click="chatOpen = false">
          <X :size="18" />
        </button>
      </div>

      <div ref="chatBodyRef" class="chat-drawer__body no-scrollbar">
        <article
          v-for="message in chatMessages"
          :key="message.id"
          class="chat-message"
          :class="`chat-message--${message.role}`"
        >
          <div v-if="message.role === 'assistant'" class="chat-avatar chat-avatar--robot" aria-hidden="true">
            <ChatbotAvatar size="small" />
          </div>

          <div class="chat-bubble" :class="`chat-bubble--${message.role}`">{{ message.text }}</div>

          <div v-if="message.role === 'user'" class="chat-avatar chat-avatar--user" aria-hidden="true">
            {{ initials(currentUser?.fullName ?? 'QU') }}
          </div>
        </article>

        <article v-if="isAssistantThinking" class="chat-message chat-message--assistant">
          <div class="chat-avatar chat-avatar--robot is-thinking" aria-hidden="true">
            <ChatbotAvatar size="small" />
          </div>
          <div class="chat-bubble chat-bubble--assistant chat-bubble--thinking" aria-label="Assistant is thinking">
            <span></span>
            <span></span>
            <span></span>
          </div>
        </article>
      </div>

      <div class="prompt-list">
        <button
          v-for="prompt in quickPrompts"
          :key="prompt"
          type="button"
          class="prompt-chip"
          :disabled="isAssistantThinking"
          @click="submitChat(prompt)"
        >
          {{ prompt }}
        </button>
      </div>

      <div v-if="showProjectSuggestions && projectSuggestions.length > 0" class="project-suggestions glass-card">
        <button 
          v-for="p in projectSuggestions" 
          :key="p.id" 
          type="button"
          @click="tagProject(p)"
        >
          <strong>@{{ p.name }}</strong>
          <span>{{ p.status }}</span>
        </button>
      </div>

      <form class="chat-drawer__composer" @submit.prevent="submitChat()">
        <input v-model="chatDraft" type="text" :disabled="isAssistantThinking" placeholder="Ask about risk, priority, work... Use @ to tag project" />
        <button class="primary-button" type="submit" :disabled="isAssistantThinking || !chatDraft.trim()">Ask</button>
      </form>
    </aside>
  </AppShell>
</template>
