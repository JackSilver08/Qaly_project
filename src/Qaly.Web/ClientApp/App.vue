<script setup lang="ts">
import { computed, nextTick, onMounted, ref, watch } from 'vue'
import { HubConnectionBuilder } from '@microsoft/signalr'
import MarkdownIt from 'markdown-it'
import DOMPurify from 'dompurify'
import {
  ClipboardList,
  FolderKanban,
  LayoutDashboard,
  MessageSquare,
  MoreHorizontal,
  Plus,
  Send,
  Users,
  X,
} from 'lucide-vue-next'
import AppShell from './components/AppShell.vue'
import ChatbotAvatar from './components/ChatbotAvatar.vue'
import DashboardSummaryCards from './components/DashboardSummaryCards.vue'
import ProjectList from './components/ProjectList.vue'
import ProjectToolbar from './components/ProjectToolbar.vue'
import TeamMiniSection from './components/TeamMiniSection.vue'
import ProjectDetailHeader from './components/ProjectDetailHeader.vue'
import ProjectStatsTab from './components/ProjectStatsTab.vue'
import ProjectMembersTab from './components/ProjectMembersTab.vue'
import ProjectWikiTab from './components/ProjectWikiTab.vue'
import { fallbackDashboard } from './fallback-dashboard'
import type { ProjectCardModel, SummaryCardModel, TeamMiniMemberModel } from './components/dashboard-models'
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
  UserDto,
} from './types'

interface ChatMessage {
  id: string
  role: 'assistant' | 'user'
  text: string
}

type ProjectFilter = 'all' | 'active' | 'planned' | 'at-risk'
type ProjectSort = 'recent' | 'risk' | 'progress' | 'name'

const navigation: ShellNavItem[] = [
  { label: 'Overview', target: 'overview', icon: LayoutDashboard },
  { label: 'Projects', target: 'projects', icon: FolderKanban },
  { label: 'Tasks', target: 'tasks', icon: ClipboardList },
  { label: 'Team', target: 'team', icon: Users },
]

const statusColumns = ['Todo', 'InProgress', 'InReview', 'Done']
const priorities = ['Low', 'Medium', 'High', 'Critical']

const dashboard = ref<DashboardResponse>(fallbackDashboard)
const currentUser = ref<UserDto | null>(null)
const users = ref<UserDto[]>([])
const notifications = ref<NotificationDto[]>([])
const comments = ref<CommentDto[]>([])
const attachments = ref<AttachmentDto[]>([])
const isLoading = ref(true)
const usingFallback = ref(true)
const chatOpen = ref(false)
const notificationsOpen = ref(false)
const createProjectOpen = ref(false)
const createTaskOpen = ref(false)
const projectBeingEditedId = ref<string | null>(null)
const selectedTaskId = ref<string | null>(null)
const activeTaskMenu = ref<string | null>(null)
const activeNavTarget = ref('overview')

function toggleTaskMenu(taskId: string) {
  activeTaskMenu.value = activeTaskMenu.value === taskId ? null : taskId
}
const searchQuery = ref('')
const projectFilter = ref<ProjectFilter>('all')
const projectSort = ref<ProjectSort>('recent')
const activeProjectId = ref<string | null>(null)
const viewingProjectDetails = ref(false)
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

      return [project.name, project.description, project.ownerName, ...project.memberNames]
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

const projectCards = computed<ProjectCardModel[]>(() =>
  filteredProjects.value.map((project) => ({
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
  })),
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
  return project.ownerId === user.id
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

const teamMiniMembers = computed<TeamMiniMemberModel[]>(() =>
  [...team.value]
    .sort((left, right) => right.capacityPercent - left.capacityPercent)
    .slice(0, 4)
    .map((member) => ({
      id: member.id,
      name: member.fullName,
      role: displayRole(member.role),
      focusArea: member.focusArea,
      capacityPercent: member.capacityPercent,
      initials: initials(member.fullName),
    })),
)

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
    if (!items.some((project) => project.id === activeProjectId.value)) {
      activeProjectId.value = items[0]?.id ?? projects.value[0]?.id ?? null
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
    activeProjectId.value = dashboard.value.projects.some((project) => project.id === previousProjectId)
      ? previousProjectId
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

function scrollToSection(sectionId: string) {
  activeNavTarget.value = sectionId
  document.getElementById(sectionId)?.scrollIntoView({ behavior: 'smooth', block: 'start' })
}

function selectProject(projectId: string) {
  activeProjectId.value = projectId
  selectedTaskId.value = projects.value.find((project) => project.id === projectId)?.tasks[0]?.id ?? null
  viewingProjectDetails.value = true
  activeProjectTab.value = 'stats'
}

function closeProjectDetails() {
  viewingProjectDetails.value = false
}

function openCreateProject() {
  activeNavTarget.value = 'projects'
  createProjectOpen.value = true
  projectBeingEditedId.value = null
  document.getElementById('projects')?.scrollIntoView({ behavior: 'smooth', block: 'start' })
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
</script>

<template>
  <AppShell
    v-model:search="searchQuery"
    :nav-items="navigation"
    :active-target="activeNavTarget"
    :notification-count="notificationCount"
    :using-fallback="usingFallback"
    :team-initials="teamMiniMembers.map((member) => member.initials)"
    :user-name="currentUser?.fullName ?? 'Qaly user'"
    :user-initials="initials(currentUser?.fullName ?? 'QU')"
    @navigate="scrollToSection"
    @create="openCreateProject"
    @notifications="notificationsOpen = !notificationsOpen"
    @assistant="openChatWithPrompt()"
  >
    <div v-if="!viewingProjectDetails" class="dashboard-scroll dashboard-scroll--embedded no-scrollbar">
      <div class="dashboard-main project-home-main no-scrollbar">
        <header class="home-topbar">
          <div class="topbar-title">
            <div>
              <span>Workspace</span>
              <h1>Qaly project cockpit</h1>
              <p>{{ isLoading ? 'Syncing live data...' : 'Select a project to view detailed work.' }}</p>
            </div>
          </div>

          <button class="secondary-button" type="button" @click="openChatWithPrompt()">
            <ChatbotAvatar size="launcher" />
            <span>Qaly assistant</span>
          </button>
          <button class="text-button" type="button" @click="logout">Sign out</button>
        </header>

        <DashboardSummaryCards id="overview" :cards="summaryCards" />

        <section id="projects" class="project-workspace glass-card">
          <div class="project-workspace__header">
            <div>
              <span>Projects</span>
              <h2>Project portfolio</h2>
            </div>
            <p>{{ filteredProjects.length }} of {{ projects.length }} projects</p>
          </div>

          <ProjectToolbar
            v-model:search="searchQuery"
            v-model:sort="projectSort"
            v-model:filter="projectFilter"
            :project-count="filteredProjects.length"
            @create="openCreateProject"
          />

          <form v-if="createProjectOpen" class="project-inline-form project-inline-form--stacked" @submit.prevent="createProject">
            <input v-model="projectName" type="text" placeholder="Project name" />
            <input v-model="projectDescription" type="text" placeholder="Short description" />
            <input v-model="projectEndDate" type="date" />
            <button class="primary-button primary-button--compact" type="submit" :disabled="!projectName.trim()">
              Create
            </button>
            <button class="text-button" type="button" @click="createProjectOpen = false">Cancel</button>
          </form>

          <form v-if="projectBeingEditedId" class="project-inline-form project-inline-form--stacked" @submit.prevent="saveProjectEdit">
            <input v-model="editProjectName" type="text" aria-label="Project name" />
            <input v-model="editProjectDescription" type="text" aria-label="Project description" />
            <button class="primary-button primary-button--compact" type="submit" :disabled="!editProjectName.trim()">
              Save
            </button>
            <button class="text-button" type="button" @click="projectBeingEditedId = null">Cancel</button>
          </form>

          <ProjectList
            :projects="projectCards"
            :active-project-id="selectedProject?.id ?? null"
            @view="selectProject"
            @edit="beginEditProject"
            @delete="deleteProject"
          />
        </section>
      </div>
    </div>

    <div v-else class="dashboard-scroll dashboard-scroll--embedded no-scrollbar">
      <div class="dashboard-main project-home-main no-scrollbar">
        <ProjectDetailHeader
          v-if="selectedProject"
          :project-name="selectedProject.name"
          :description="selectedProject.description"
          :status-label="displayStatus(selectedProject.status)"
          :status-tone="statusTone(selectedProject.status)"
          :progress-label="`${selectedProject.completedTaskCount}/${selectedProject.taskCount} task hoàn thành`"
          :progress-percentage="selectedProject.progressPercentage"
          @back="closeProjectDetails"
          @assistant="openChatWithPrompt()"
        />

        <nav class="project-tabs glass-card">
          <button
            v-for="tab in tabs"
            :key="tab.id"
            type="button"
            class="tab-link"
            :class="{ 'is-active': activeProjectTab === tab.id }"
            @click="activeProjectTab = tab.id"
          >
            {{ tab.label }}
          </button>
        </nav>

        <div v-if="activeProjectTab === 'stats'" class="tab-pane reveal">
          <ProjectStatsTab :stats="selectedProjectStats" />
        </div>

        <div v-if="activeProjectTab === 'tasks'">
          <section id="tasks" class="task-board-shell glass-card">
            <div class="panel-heading">
              <div>
                <span>Tasks</span>
                <h2>Board</h2>
              </div>
              <button class="primary-button primary-button--compact" type="button" @click="createTaskOpen = !createTaskOpen">
                <Plus :size="16" />
                <span>Task</span>
              </button>
            </div>

            <form v-if="createTaskOpen" class="task-create-form" @submit.prevent="createTask">
              <input v-model="newTaskTitle" type="text" placeholder="Task title" />
              <input v-model="newTaskDescription" type="text" placeholder="Description" />
              <select v-model="newTaskPriority" aria-label="Priority">
                <option v-for="priority in priorities" :key="priority" :value="priority">{{ priority }}</option>
              </select>
              <select v-model="newTaskAssigneeId" aria-label="Assignee">
                <option value="">Unassigned</option>
                <option v-for="user in users" :key="user.id" :value="user.id">{{ user.fullName }}</option>
              </select>
              <input v-model="newTaskDueDate" type="date" />
              <button class="primary-button primary-button--compact" type="submit" :disabled="!newTaskTitle.trim()">Create</button>
            </form>

            <div class="kanban-board">
              <section v-for="status in statusColumns" :key="status" class="kanban-column">
                <div class="kanban-column__header">
                  <strong>{{ displayStatus(status) }}</strong>
                  <span>{{ tasksByStatus(status).length }}</span>
                </div>

                <article
                  v-for="task in tasksByStatus(status)"
                  :key="task.id"
                  class="kanban-card"
                  :class="{ 'is-selected': selectedTask?.id === task.id }"
                  @click="selectedTaskId = task.id"
                >
                  <div class="kanban-card__top">
                    <strong>{{ task.title }}</strong>
                    <div style="display: flex; align-items: center; gap: 8px;">
                      <span :class="`priority priority--${task.priority.toLowerCase()}`">{{ task.priority }}</span>
                      
                      <!-- Task Context Menu (Admin Only) -->
                      <div v-if="isProjectAdmin" class="task-menu-dropdown">
                        <button class="icon-button icon-button--small" type="button" @click.stop="toggleTaskMenu(task.id)">
                          <MoreHorizontal :size="14" />
                        </button>
                        <div v-if="activeTaskMenu === task.id" class="dropdown-content glass-card">
                          <button type="button" @click.stop="beginEditTask(task)">Sửa</button>
                          <button type="button" style="color: var(--peach-500)" @click.stop="deleteTask(task.id)">Xóa</button>
                        </div>
                      </div>
                    </div>
                  </div>
                  <p>{{ task.assigneeName || 'Unassigned' }} - {{ formatDate(task.dueDate) }}</p>
                  <div class="kanban-card__meta">
                    <span>{{ task.commentCount }} comments</span>
                    <span v-if="task.isPrivate">Private</span>
                    <span v-if="isTaskOverdue(task)" class="project-risk">Overdue</span>
                  </div>
                  <div class="kanban-card__actions">
                    <button
                      v-for="nextStatus in nextStatuses(task.status)"
                      :key="nextStatus"
                      type="button"
                      @click.stop="moveTask(task, nextStatus)"
                    >
                      {{ displayStatus(nextStatus) }}
                    </button>
                  </div>
                </article>

                <div v-if="tasksByStatus(status).length === 0" class="empty-state">No tasks</div>
              </section>
            </div>
          </section>

          <section class="task-detail-panel glass-card" style="margin-top: 24px;">
            <div class="panel-heading">
              <div>
                <span>Task detail</span>
                <h2>{{ selectedTask?.title ?? 'No task selected' }}</h2>
              </div>
              <MessageSquare :size="18" />
            </div>

            <div v-if="selectedTask" class="comment-list">
              <div class="attachment-panel">
                <div class="attachment-panel__header">
                  <strong>Attachments</strong>
                  <label class="attachment-upload">
                    <input type="file" @change="uploadAttachment" />
                    <span>Upload</span>
                  </label>
                </div>
                <article v-for="attachment in attachments" :key="attachment.id" class="attachment-row">
                  <div>
                    <strong>{{ attachment.fileName }}</strong>
                    <span>{{ formatFileSize(attachment.fileSize) }} - {{ attachment.uploadedByName }}</span>
                  </div>
                  <button type="button" @click="deleteAttachment(attachment)">Delete</button>
                </article>
                <div v-if="attachments.length === 0" class="empty-state">No attachments.</div>
              </div>

              <article v-for="comment in comments" :key="comment.id" class="comment-row">
                <div style="display: flex; justify-content: space-between; align-items: flex-start; width: 100%;">
                  <strong>{{ comment.authorName }}</strong>
                  <button 
                    v-if="isProjectAdmin || comment.authorId === currentUser?.id" 
                    class="text-button" 
                    style="color: var(--peach-500); padding: 0 4px; height: auto;" 
                    type="button" 
                    @click="deleteComment(comment.id)"
                  >
                    Xóa
                  </button>
                </div>
                <p>{{ comment.content }}</p>
                <span>{{ formatTime(comment.createdAt) }}</span>
              </article>
              <div v-if="comments.length === 0" class="empty-state">No comments yet.</div>

              <form class="comment-form" @submit.prevent="submitComment">
                <input v-model="newComment" type="text" placeholder="Add a comment..." />
                <button class="primary-button primary-button--compact" type="submit" :disabled="!newComment.trim()">
                  <Send :size="15" />
                </button>
              </form>
            </div>

            <div v-else class="empty-state">Select a task from the board.</div>
          </section>
        </div>

        <div v-if="activeProjectTab === 'members'" class="tab-pane reveal">
          <ProjectMembersTab 
            :members="selectedProjectMembers" 
            :users="users"
            :is-admin="isProjectAdmin"
            @add="addMember"
            @remove="removeMember"
            @update-role="updateMemberRole"
          />
        </div>

        <div v-if="activeProjectTab === 'wiki'" class="tab-pane reveal">
          <ProjectWikiTab 
            :project-name="selectedProject?.name ?? ''" 
            :is-admin="isProjectAdmin"
          />
        </div>
      </div>
    </div>

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
