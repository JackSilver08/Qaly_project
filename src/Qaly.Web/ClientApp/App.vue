<script setup lang="ts">
import { computed, nextTick, onMounted, ref, watch } from 'vue'
import {
  ClipboardList,
  FolderKanban,
  LayoutDashboard,
  Users,
  X,
} from 'lucide-vue-next'
import AppShell from './components/AppShell.vue'
import ChatbotAvatar from './components/ChatbotAvatar.vue'
import DashboardSummaryCards from './components/DashboardSummaryCards.vue'
import ProjectList from './components/ProjectList.vue'
import ProjectToolbar from './components/ProjectToolbar.vue'
import TeamMiniSection from './components/TeamMiniSection.vue'
import { fallbackDashboard } from './fallback-dashboard'
import type { DashboardNotification, DashboardProject, DashboardResponse } from './types'
import type { ProjectCardModel, SummaryCardModel, TeamMiniMemberModel } from './components/dashboard-models'
import type { ShellNavItem } from './components/shell-models'

interface ChatMessage {
  id: string
  role: 'assistant' | 'user'
  text: string
}

type ProjectFilter = 'all' | 'active' | 'planned' | 'at-risk'
type ProjectSort = 'recent' | 'risk' | 'progress' | 'name'

const navigation: ShellNavItem[] = [
  { label: 'Tổng quan', target: 'overview', icon: LayoutDashboard },
  { label: 'Dự án', target: 'projects', icon: FolderKanban },
  { label: 'Công việc', target: 'tasks', icon: ClipboardList },
  { label: 'Đội ngũ', target: 'team', icon: Users },
]

const knownTextMap: Record<string, string> = {
  'Admin User': 'Quản trị viên',
  'Nguyen Van A': 'Nguyễn Văn A',
  'Tran Thi B': 'Trần Thị B',
  'Identity Hardening': 'Gia cố định danh',
  'Design Language': 'Ngôn ngữ thiết kế',
  'Internal project management workspace for sprint execution, team planning, and AI-assisted delivery.':
    'Không gian quản lý công việc nội bộ cho triển khai chu kỳ làm việc, lập kế hoạch đội ngũ và hỗ trợ quyết định bằng AI.',
  'Role-based access, session safety, and login/register experience.':
    'Phân quyền theo vai trò, an toàn phiên đăng nhập và trải nghiệm đăng nhập/đăng ký.',
  'Landing the visual system, typography, and reusable UI patterns for admin workflows.':
    'Hoàn thiện hệ thống hình ảnh, kiểu chữ và các mẫu giao diện tái sử dụng cho quy trình quản trị.',
  'Qaly MVP': 'Qaly MVP',
  'Design workspace shell': 'Thiết kế khung làm việc',
  'Implement dashboard metrics API': 'Triển khai API chỉ số bảng điều khiển',
  'Create Kanban interaction states': 'Tạo trạng thái tương tác Kanban',
  'Integrate AI insight surfaces': 'Tích hợp bề mặt gợi ý AI',
  'Project coordination': 'Điều phối dự án',
  'Capacity available': 'Còn năng lực tiếp nhận',
  'Execution stream': 'Luồng triển khai',
  'Support lane': 'Hỗ trợ vận hành',
  'Design systems': 'Hệ thống thiết kế',
}

const dashboard = ref<DashboardResponse>(fallbackDashboard)
const isLoading = ref(true)
const usingFallback = ref(true)
const chatOpen = ref(false)
const notificationsOpen = ref(false)
const createProjectOpen = ref(false)
const projectBeingEditedId = ref<string | null>(null)
const activeNavTarget = ref('overview')
const searchQuery = ref('')
const projectFilter = ref<ProjectFilter>('all')
const projectSort = ref<ProjectSort>('recent')
const activeProjectId = ref<string | null>(null)
const newProjectName = ref('')
const editProjectName = ref('')
const actionNotice = ref('')
const chatDraft = ref('')
const chatBodyRef = ref<HTMLElement | null>(null)
const isAssistantThinking = ref(false)
const chatMessages = ref<ChatMessage[]>([
  {
    id: 'assistant-welcome',
    role: 'assistant',
    text: 'Tôi có thể tóm tắt rủi ro, việc quá hạn và bước tiếp theo cho dự án đang chọn.',
  },
])

let actionNoticeTimer: ReturnType<typeof window.setTimeout> | undefined
let assistantThinkingTimer: ReturnType<typeof window.setTimeout> | undefined

const projects = computed(() => dashboard.value.projects)
const team = computed(() => dashboard.value.team)
const activeProjectsCount = computed(() => projects.value.filter((project) => project.status !== 'Archived').length)
const totalTasks = computed(() => projects.value.reduce((sum, project) => sum + project.tasks.length, 0))
const completedTasks = computed(() =>
  projects.value.reduce((sum, project) => sum + project.tasks.filter((task) => normalizeStatus(task.status) === 'Done').length, 0),
)
const overdueTasks = computed(() =>
  projects.value.reduce((sum, project) => sum + project.tasks.filter((task) => isTaskOverdue(task)).length, 0),
)

// Homepage hierarchy: three high-signal KPIs, then project controls and the project list.
const summaryCards = computed<SummaryCardModel[]>(() => [
  {
    key: 'projects',
    label: 'Total Projects',
    value: String(projects.value.length),
    detail: `${activeProjectsCount.value} đang chạy`,
    tone: 'blue',
  },
  {
    key: 'tasks',
    label: 'Total Tasks',
    value: String(totalTasks.value),
    detail: `${completedTasks.value} đã hoàn tất`,
    tone: 'mint',
  },
  {
    key: 'team',
    label: 'Team Members',
    value: String(team.value.length),
    detail: 'đang tham gia workspace',
    tone: 'violet',
  },
])

const filteredProjects = computed(() => {
  const query = searchQuery.value.trim().toLowerCase()

  return projects.value
    .filter((project) => {
      if (projectFilter.value === 'active' && !['Active', 'InProgress'].includes(project.status)) {
        return false
      }

      if (projectFilter.value === 'planned' && project.status !== 'Planned') {
        return false
      }

      if (projectFilter.value === 'at-risk' && project.overdueTaskCount === 0) {
        return false
      }

      if (!query) {
        return true
      }

      return [
        displayText(project.name),
        displayText(project.description),
        displayName(project.ownerName),
        ...project.memberNames.map(displayName),
      ]
        .join(' ')
        .toLowerCase()
        .includes(query)
    })
    .sort((left, right) => {
      if (projectSort.value === 'name') {
        return displayText(left.name).localeCompare(displayText(right.name), 'vi')
      }

      if (projectSort.value === 'progress') {
        return right.progressPercentage - left.progressPercentage
      }

      if (projectSort.value === 'risk') {
        return right.overdueTaskCount - left.overdueTaskCount
      }

      return new Date(right.createdAt).getTime() - new Date(left.createdAt).getTime()
    })
})

const projectCards = computed<ProjectCardModel[]>(() =>
  filteredProjects.value.map((project) => ({
    id: project.id,
    name: displayText(project.name),
    description: displayText(project.description) || 'Chưa có mô tả ngắn cho dự án này.',
    status: project.status,
    statusLabel: displayStatus(project.status),
    statusTone: statusTone(project.status),
    ownerName: displayName(project.ownerName),
    dueDateLabel: project.endDate ? `Hạn ${formatDate(project.endDate)}` : 'Chưa có hạn',
    completedTaskCount: project.completedTaskCount,
    taskCount: project.taskCount,
    overdueTaskCount: project.overdueTaskCount,
    progressPercentage: project.progressPercentage,
    memberInitials: project.memberNames.slice(0, 4).map((name) => initials(displayName(name))),
  })),
)

const selectedProject = computed(() => {
  if (activeProjectId.value) {
    const active = projects.value.find((project) => project.id === activeProjectId.value)
    if (active) {
      return active
    }
  }

  return filteredProjects.value[0] ?? projects.value[0] ?? null
})

const selectedProjectTasks = computed(() => selectedProject.value?.tasks ?? [])
const selectedOpenTasks = computed(() =>
  selectedProjectTasks.value.filter((task) => normalizeStatus(task.status) !== 'Done').slice(0, 4),
)
const selectedProjectSummary = computed(() => {
  const project = selectedProject.value

  if (!project) {
    return 'Chọn một dự án để xem công việc trọng tâm.'
  }

  return `${displayText(project.name)} có ${project.completedTaskCount}/${project.taskCount} công việc đã hoàn tất.`
})

const teamMiniMembers = computed<TeamMiniMemberModel[]>(() =>
  [...team.value]
    .sort((left, right) => right.capacityPercent - left.capacityPercent)
    .slice(0, 4)
    .map((member) => ({
      id: member.id,
      name: displayName(member.fullName),
      role: displayRole(member.role),
      focusArea: displayText(member.focusArea),
      capacityPercent: member.capacityPercent,
      initials: initials(displayName(member.fullName)),
    })),
)

const notificationCount = computed(
  () => dashboard.value.notifications.filter((notification) => notification.tone !== 'info').length,
)

const quickPrompts = computed(() => {
  const projectName = selectedProject.value ? displayText(selectedProject.value.name) : 'workspace này'

  return [
    `Tóm tắt rủi ro của ${projectName}`,
    'Dự án nào đang có rủi ro?',
    'Việc nào nên ưu tiên hôm nay?',
  ]
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
  () => [chatMessages.value.length, isAssistantThinking.value],
  () => {
    void scrollChatToBottom()
  },
)

onMounted(async () => {
  await loadDashboard()
})

async function loadDashboard() {
  isLoading.value = true

  try {
    const response = await fetch('/api/dashboard/overview', {
      headers: {
        Accept: 'application/json',
      },
    })

    if (!response.ok) {
      throw new Error(`Dashboard request failed with status ${response.status}`)
    }

    const data = (await response.json()) as DashboardResponse

    if (!data.projects?.length) {
      throw new Error('Dashboard did not return projects')
    }

    dashboard.value = data
    usingFallback.value = false
  } catch (error) {
    console.warn('Using fallback dashboard data.', error)
    dashboard.value = fallbackDashboard
    usingFallback.value = true
  } finally {
    activeProjectId.value = dashboard.value.projects[0]?.id ?? null
    isLoading.value = false
  }
}

function scrollToSection(sectionId: string) {
  activeNavTarget.value = sectionId
  document.getElementById(sectionId)?.scrollIntoView({ behavior: 'smooth', block: 'start' })
}

function selectProject(projectId: string) {
  activeProjectId.value = projectId
  activeNavTarget.value = 'tasks'
  document.getElementById('tasks')?.scrollIntoView({ behavior: 'smooth', block: 'nearest' })
}

function openCreateProject() {
  activeNavTarget.value = 'projects'
  createProjectOpen.value = true
  projectBeingEditedId.value = null
  document.getElementById('projects')?.scrollIntoView({ behavior: 'smooth', block: 'start' })
}

function createProject() {
  const name = newProjectName.value.trim()

  if (!name) {
    return
  }

  const ownerName = dashboard.value.team[0]?.fullName ?? 'Quản trị viên'
  const memberNames = dashboard.value.team.slice(0, 3).map((member) => member.fullName)
  const project: DashboardProject = {
    id: `local-project-${Date.now()}`,
    name,
    description: 'Dự án mới đang chờ bổ sung phạm vi và kế hoạch triển khai.',
    status: 'Active',
    ownerName,
    memberCount: Math.max(memberNames.length, 1),
    taskCount: 0,
    completedTaskCount: 0,
    overdueTaskCount: 0,
    progressPercentage: 0,
    memberNames: memberNames.length ? memberNames : [ownerName],
    tasks: [],
    createdAt: new Date().toISOString(),
    endDate: null,
  }

  dashboard.value.projects.unshift(project)
  activeProjectId.value = project.id
  newProjectName.value = ''
  createProjectOpen.value = false
  showActionNotice(`Đã tạo dự án "${displayText(project.name)}".`)
}

function beginEditProject(projectId: string) {
  const project = projects.value.find((item) => item.id === projectId)

  if (!project) {
    return
  }

  activeProjectId.value = projectId
  createProjectOpen.value = false
  projectBeingEditedId.value = projectId
  editProjectName.value = displayText(project.name)
}

function saveProjectEdit() {
  const project = projects.value.find((item) => item.id === projectBeingEditedId.value)
  const name = editProjectName.value.trim()

  if (!project || !name) {
    return
  }

  project.name = name
  projectBeingEditedId.value = null
  editProjectName.value = ''
  showActionNotice(`Đã cập nhật tên dự án thành "${displayText(project.name)}".`)
}

function deleteProject(projectId: string) {
  const project = projects.value.find((item) => item.id === projectId)

  if (!project) {
    return
  }

  dashboard.value.projects = dashboard.value.projects.filter((item) => item.id !== projectId)
  activeProjectId.value = dashboard.value.projects[0]?.id ?? null
  showActionNotice(`Đã ẩn dự án "${displayText(project.name)}" khỏi danh sách.`)
}

function dismissNotification(notificationId: string) {
  dashboard.value.notifications = dashboard.value.notifications.filter(
    (notification) => notification.id !== notificationId,
  )
}

function clearActionableNotifications() {
  dashboard.value.notifications = dashboard.value.notifications.filter(
    (notification) => notification.tone === 'info',
  )
  notificationsOpen.value = false
}

function openChatWithPrompt(prompt?: string) {
  chatOpen.value = true

  if (prompt) {
    submitChat(prompt)
  } else {
    void scrollChatToBottom()
  }
}

function submitChat(explicitPrompt?: string) {
  const prompt = (explicitPrompt ?? chatDraft.value).trim()

  if (!prompt) {
    return
  }

  if (isAssistantThinking.value) {
    return
  }

  chatMessages.value.push({
    id: `user-${Date.now()}`,
    role: 'user',
    text: prompt,
  })

  chatDraft.value = ''
  isAssistantThinking.value = true

  if (assistantThinkingTimer) {
    window.clearTimeout(assistantThinkingTimer)
  }

  assistantThinkingTimer = window.setTimeout(() => {
    chatMessages.value.push({
      id: `assistant-${Date.now()}`,
      role: 'assistant',
      text: createAssistantReply(prompt),
    })
    isAssistantThinking.value = false
  }, 850)
}

async function scrollChatToBottom() {
  await nextTick()
  chatBodyRef.value?.scrollTo({
    top: chatBodyRef.value.scrollHeight,
    behavior: 'smooth',
  })
}

function createAssistantReply(prompt: string) {
  const query = prompt.toLowerCase()
  const project = selectedProject.value

  if (query.includes('rủi ro') || query.includes('risk')) {
    return `${overdueTasks.value} công việc đang quá hạn trên toàn workspace. Dự án nên xem trước là ${project ? displayText(project.name) : 'dự án có nhiều việc quá hạn nhất'}.`
  }

  if (query.includes('ưu tiên') || query.includes('priority')) {
    const task = selectedOpenTasks.value[0]
    return task
      ? `Nên ưu tiên "${displayText(task.title)}" vì đây là việc mở gần nhất trong ${displayText(project?.name)}.`
      : 'Không có công việc mở trong dự án đang chọn.'
  }

  return selectedProjectSummary.value
}

function showActionNotice(message: string) {
  actionNotice.value = message

  if (actionNoticeTimer) {
    window.clearTimeout(actionNoticeTimer)
  }

  actionNoticeTimer = window.setTimeout(() => {
    actionNotice.value = ''
  }, 3000)
}

function normalizeStatus(status: string) {
  return status === 'InProgress' ? 'InProgress' : status === 'Done' ? 'Done' : 'Todo'
}

function isTaskOverdue(task: { dueDate: string | null; status: string }) {
  return Boolean(task.dueDate) && new Date(task.dueDate as string).getTime() < Date.now() && normalizeStatus(task.status) !== 'Done'
}

function displayText(value: string | null | undefined) {
  if (!value) {
    return ''
  }

  return knownTextMap[value] ?? value
}

function displayName(value: string | null | undefined) {
  return displayText(value)
}

function displayStatus(status: string | null | undefined) {
  switch (status) {
    case 'Active':
      return 'Đang chạy'
    case 'InProgress':
      return 'Đang làm'
    case 'Done':
      return 'Hoàn tất'
    case 'Planned':
      return 'Đã lên kế hoạch'
    case 'Archived':
      return 'Lưu trữ'
    case 'Todo':
      return 'Cần làm'
    default:
      return status ? displayText(status) : 'Không xác định'
  }
}

function displayRole(role: string | null | undefined) {
  switch (role) {
    case 'Admin':
      return 'Quản trị viên'
    case 'Member':
      return 'Thành viên'
    default:
      return role ? displayText(role) : ''
  }
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
  if (!value) {
    return 'Chưa có ngày'
  }

  return new Intl.DateTimeFormat('vi-VN', {
    month: 'short',
    day: 'numeric',
  }).format(new Date(value))
}

function formatTime(value: string) {
  return new Intl.DateTimeFormat('vi-VN', {
    hour: 'numeric',
    minute: '2-digit',
  }).format(new Date(value))
}

function initials(name: string) {
  return name
    .split(' ')
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase() ?? '')
    .join('')
}

function notificationClass(notification: DashboardNotification) {
  return `notice notice--${notification.tone}`
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
    user-name="Quan tri vien"
    user-initials="QT"
    @navigate="scrollToSection"
    @create="openCreateProject"
    @notifications="notificationsOpen = !notificationsOpen"
    @assistant="openChatWithPrompt()"
  >
    <div class="dashboard-scroll dashboard-scroll--embedded no-scrollbar">
      <div class="dashboard-main project-home-main no-scrollbar">
        <!-- Project-focused homepage: summary, controls, project list, then lightweight secondary context. -->
        <header class="home-topbar">
          <div class="topbar-title">
            <div>
              <span>Project management</span>
              <h1>Quan ly du an</h1>
              <p>{{ isLoading ? 'Dang dong bo du lieu...' : selectedProjectSummary }}</p>
            </div>
          </div>

          <button class="secondary-button" type="button" @click="openChatWithPrompt()">
            <ChatbotAvatar size="launcher" />
            <span>Tro ly Qaly</span>
          </button>
        </header>
        <DashboardSummaryCards id="overview" :cards="summaryCards" />

        <section id="projects" class="project-workspace glass-card">
          <div class="project-workspace__header">
            <div>
              <span>Dự án</span>
              <h2>Danh sách dự án</h2>
            </div>
            <p>{{ filteredProjects.length }} trong {{ projects.length }} dự án</p>
          </div>

          <ProjectToolbar
            v-model:search="searchQuery"
            v-model:sort="projectSort"
            v-model:filter="projectFilter"
            :project-count="filteredProjects.length"
            @create="openCreateProject"
          />

          <form v-if="createProjectOpen" class="project-inline-form" @submit.prevent="createProject">
            <input v-model="newProjectName" type="text" placeholder="Tên dự án mới..." />
            <button class="primary-button primary-button--compact" type="submit" :disabled="!newProjectName.trim()">
              Tạo
            </button>
            <button class="text-button" type="button" @click="createProjectOpen = false">Hủy</button>
          </form>

          <form v-if="projectBeingEditedId" class="project-inline-form" @submit.prevent="saveProjectEdit">
            <input v-model="editProjectName" type="text" aria-label="Tên dự án" />
            <button class="primary-button primary-button--compact" type="submit" :disabled="!editProjectName.trim()">
              Lưu
            </button>
            <button class="text-button" type="button" @click="projectBeingEditedId = null">Hủy</button>
          </form>

          <ProjectList
            :projects="projectCards"
            :active-project-id="selectedProject?.id ?? null"
            @view="selectProject"
            @edit="beginEditProject"
            @delete="deleteProject"
          />
        </section>

        <section class="home-secondary-grid">
          <section id="tasks" class="task-snapshot glass-card">
            <div class="panel-heading">
              <div>
                <span>Công việc</span>
                <h2>{{ selectedProject ? displayText(selectedProject.name) : 'Dự án đang chọn' }}</h2>
              </div>
              <span class="count-pill">{{ selectedOpenTasks.length }}</span>
            </div>

            <div class="task-snapshot-list">
              <article v-for="task in selectedOpenTasks" :key="task.id" class="task-snapshot-row">
                <div>
                  <strong>{{ displayText(task.title) }}</strong>
                  <p>{{ task.assigneeName ? displayName(task.assigneeName) : 'Chưa giao' }} - {{ formatDate(task.dueDate) }}</p>
                </div>
                <span>{{ displayStatus(task.status) }}</span>
              </article>
              <div v-if="selectedOpenTasks.length === 0" class="empty-state">Dự án này không còn công việc đang mở.</div>
            </div>
          </section>

          <TeamMiniSection id="team" :members="teamMiniMembers" />
        </section>
      </div>

      <div v-if="notificationsOpen" class="notification-popover glass-card home-notification-popover">
        <div class="panel-heading">
          <div>
            <span>Thông báo</span>
            <h2>Tín hiệu hiện tại</h2>
          </div>
          <div class="popover-actions">
            <button class="text-button" type="button" @click="clearActionableNotifications">Đã đọc</button>
            <button class="icon-button icon-button--small" type="button" @click="notificationsOpen = false">
              <X :size="16" />
            </button>
          </div>
        </div>
        <article
          v-for="notification in dashboard.notifications.slice(0, 4)"
          :key="notification.id"
          :class="notificationClass(notification)"
        >
          <div class="notice__top">
            <strong>{{ notification.title }}</strong>
            <button type="button" aria-label="Ẩn thông báo" @click="dismissNotification(notification.id)">
              <X :size="14" />
            </button>
          </div>
          <p>{{ notification.message }}</p>
          <span>{{ formatTime(notification.createdAt) }}</span>
        </article>
      </div>
    </div>

    <div v-if="actionNotice" class="action-toast">{{ actionNotice }}</div>

      <aside class="chat-drawer glass-card" :class="{ 'is-open': chatOpen }">
        <div class="chat-drawer__header">
        <div class="chat-drawer__identity">
          <ChatbotAvatar size="medium" />
          <div>
            <span>Trợ lý AI</span>
            <h2>Trợ lý Qaly</h2>
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

          <div class="chat-bubble" :class="`chat-bubble--${message.role}`">
            {{ message.text }}
          </div>

          <div v-if="message.role === 'user'" class="chat-avatar chat-avatar--user" aria-hidden="true">QT</div>
        </article>

        <article v-if="isAssistantThinking" class="chat-message chat-message--assistant">
          <div class="chat-avatar chat-avatar--robot is-thinking" aria-hidden="true">
            <ChatbotAvatar size="small" />
          </div>
          <div class="chat-bubble chat-bubble--assistant chat-bubble--thinking" aria-label="Trợ lý đang suy nghĩ">
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

      <form class="chat-drawer__composer" @submit.prevent="submitChat()">
        <input v-model="chatDraft" type="text" :disabled="isAssistantThinking" placeholder="Hỏi về dự án, rủi ro, ưu tiên..." />
        <button class="primary-button" type="submit" :disabled="isAssistantThinking || !chatDraft.trim()">Hỏi</button>
      </form>
    </aside>
  </AppShell>
</template>
