<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import {
  Bell,
  Bot,
  BriefcaseBusiness,
  ChartNoAxesCombined,
  CheckCheck,
  ChevronRight,
  ClipboardList,
  Clock3,
  FolderKanban,
  LayoutDashboard,
  Menu,
  MessageSquareText,
  Search,
  ShieldAlert,
  Sparkles,
  Users,
  X,
  Zap,
} from 'lucide-vue-next'
import { fallbackDashboard } from './fallback-dashboard'
import type {
  DashboardMember,
  DashboardNotification,
  DashboardProject,
  DashboardResponse,
  DashboardTask,
} from './types'

interface ChatMessage {
  id: string
  role: 'assistant' | 'user'
  text: string
}

interface NavItem {
  label: string
  target: string
  icon: object
}

interface MetricCard {
  label: string
  value: string
  detail: string
  tone: string
  icon: object
}

interface RiskSignal {
  title: string
  detail: string
  tone: string
}

const navigation: NavItem[] = [
  { label: 'Overview', target: 'overview', icon: LayoutDashboard },
  { label: 'Projects', target: 'projects', icon: FolderKanban },
  { label: 'Task Board', target: 'tasks', icon: ClipboardList },
  { label: 'Team Pulse', target: 'team', icon: Users },
  { label: 'AI Signals', target: 'insights', icon: Sparkles },
]

const dashboard = ref<DashboardResponse>(fallbackDashboard)
const isLoading = ref(true)
const usingFallback = ref(true)
const sidebarOpen = ref(false)
const chatOpen = ref(false)
const notificationsOpen = ref(false)
const searchQuery = ref('')
const activeProjectId = ref<string | null>(null)
const chatDraft = ref('')
const chatMessages = ref<ChatMessage[]>([
  {
    id: 'assistant-welcome',
    role: 'assistant',
    text: 'I can summarize delivery risk, surface overdue work, and point you to the next best action for the active project.',
  },
])

const allProjects = computed(() => dashboard.value.projects)
const sortedTeam = computed(() =>
  [...dashboard.value.team].sort((left, right) => right.capacityPercent - left.capacityPercent),
)

const filteredProjects = computed(() => {
  const query = searchQuery.value.trim().toLowerCase()

  if (!query) {
    return allProjects.value
  }

  return allProjects.value.filter((project) => {
    const projectText = [
      project.name,
      project.description ?? '',
      project.ownerName,
      ...project.memberNames,
    ]
      .join(' ')
      .toLowerCase()

    if (projectText.includes(query)) {
      return true
    }

    return project.tasks.some((task) =>
      [
        task.title,
        task.priority,
        task.status,
        task.assigneeName ?? '',
        task.reporterName,
      ]
        .join(' ')
        .toLowerCase()
        .includes(query),
    )
  })
})

watch(
  filteredProjects,
  (projects) => {
    if (!projects.some((project) => project.id === activeProjectId.value)) {
      activeProjectId.value = projects[0]?.id ?? allProjects.value[0]?.id ?? null
    }
  },
  { immediate: true },
)

const selectedProject = computed(() => {
  if (activeProjectId.value) {
    const active = filteredProjects.value.find((project) => project.id === activeProjectId.value)
    if (active) {
      return active
    }
  }

  return filteredProjects.value[0] ?? allProjects.value[0] ?? null
})

const visibleTasks = computed(() => {
  const project = selectedProject.value
  const query = searchQuery.value.trim().toLowerCase()

  if (!project) {
    return []
  }

  if (!query) {
    return project.tasks
  }

  return project.tasks.filter((task) =>
    [
      task.title,
      task.priority,
      task.status,
      task.assigneeName ?? '',
      task.reporterName,
      task.projectName,
    ]
      .join(' ')
      .toLowerCase()
      .includes(query),
  )
})

const metrics = computed<MetricCard[]>(() => [
  {
    label: 'Active Projects',
    value: String(dashboard.value.stats.activeProjects),
    detail: 'Cross-functional streams currently moving',
    tone: 'amber',
    icon: BriefcaseBusiness,
  },
  {
    label: 'Completion Rate',
    value: `${dashboard.value.stats.completionRate}%`,
    detail: `${dashboard.value.stats.completedTasks} tasks closed across the board`,
    tone: 'teal',
    icon: CheckCheck,
  },
  {
    label: 'Tasks At Risk',
    value: String(dashboard.value.stats.tasksAtRisk),
    detail: `${dashboard.value.stats.overdueTasks} item(s) already overdue`,
    tone: 'coral',
    icon: ShieldAlert,
  },
  {
    label: 'Team Pulse',
    value: String(dashboard.value.stats.teamMembers),
    detail: 'People contributing to the active workspace',
    tone: 'ink',
    icon: Users,
  },
])

const kanbanColumns = computed(() => {
  const columns = [
    { key: 'Todo', label: 'Backlog', items: [] as DashboardTask[] },
    { key: 'InProgress', label: 'In Progress', items: [] as DashboardTask[] },
    { key: 'Done', label: 'Done', items: [] as DashboardTask[] },
  ]

  for (const task of visibleTasks.value) {
    const match = columns.find((column) => normalizeStatus(task.status) === column.key) ?? columns[0]
    match.items.push(task)
  }

  return columns
})

const riskSignals = computed<RiskSignal[]>(() => {
  const project = selectedProject.value

  if (!project) {
    return []
  }

  const unassigned = project.tasks.filter(
    (task) => normalizeStatus(task.status) !== 'Done' && !task.assigneeName,
  )
  const overdue = project.tasks.filter((task) => isTaskOverdue(task))
  const highPriority = project.tasks.filter(
    (task) => normalizeStatus(task.status) !== 'Done' && isHighPriority(task.priority),
  )

  const signals: RiskSignal[] = []

  if (overdue.length > 0) {
    signals.push({
      title: 'Deadline slippage',
      detail: `${overdue.length} overdue task(s) are slowing ${project.name}.`,
      tone: 'critical',
    })
  }

  if (unassigned.length > 0) {
    signals.push({
      title: 'Ownership gap',
      detail: `${unassigned.length} open item(s) still need a clear assignee.`,
      tone: 'warning',
    })
  }

  if (highPriority.length > 0) {
    signals.push({
      title: 'Escalation lane',
      detail: `${highPriority.length} high-priority task(s) deserve daily follow-up.`,
      tone: 'info',
    })
  }

  if (signals.length === 0) {
    signals.push({
      title: 'Stable execution',
      detail: `${project.name} has no immediate delivery red flags in the current snapshot.`,
      tone: 'positive',
    })
  }

  return signals.slice(0, 3)
})

const quickPrompts = computed(() => {
  const project = selectedProject.value
  const projectName = project?.name ?? 'this workspace'

  return [
    `Summarize risk for ${projectName}`,
    'Which tasks are overdue right now?',
    'Who has the highest workload?',
  ]
})

const highlightedMembers = computed(() => sortedTeam.value.slice(0, 4))
const notificationCount = computed(
  () => dashboard.value.notifications.filter((notification) => notification.tone !== 'info').length,
)

const projectNarrative = computed(() => {
  const project = selectedProject.value

  if (!project) {
    return 'No project is selected yet.'
  }

  const dueSoon = project.tasks
    .filter((task) => normalizeStatus(task.status) !== 'Done' && task.dueDate)
    .sort((left, right) => new Date(left.dueDate ?? '').getTime() - new Date(right.dueDate ?? '').getTime())
    .at(0)

  if (!dueSoon) {
    return `${project.name} is ${project.progressPercentage}% complete with no urgent due date pressure right now.`
  }

  return `${project.name} is ${project.progressPercentage}% complete. The next critical checkpoint is "${dueSoon.title}" due ${formatRelativeDate(dueSoon.dueDate)}.`
})

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
      throw new Error(`Dashboard request failed with ${response.status}`)
    }

    const data = (await response.json()) as DashboardResponse

    if (!data.projects?.length) {
      throw new Error('Dashboard returned no projects')
    }

    dashboard.value = data
    usingFallback.value = false
  } catch (error) {
    console.warn('Falling back to design-time dashboard data.', error)
    dashboard.value = fallbackDashboard
    usingFallback.value = true
  } finally {
    activeProjectId.value = dashboard.value.projects[0]?.id ?? null
    isLoading.value = false
  }
}

function scrollToSection(sectionId: string) {
  sidebarOpen.value = false
  document.getElementById(sectionId)?.scrollIntoView({ behavior: 'smooth', block: 'start' })
}

function openChatWithPrompt(prompt?: string) {
  chatOpen.value = true

  if (prompt) {
    submitChat(prompt)
  }
}

function submitChat(explicitPrompt?: string) {
  const prompt = (explicitPrompt ?? chatDraft.value).trim()

  if (!prompt) {
    return
  }

  chatMessages.value.push({
    id: `user-${Date.now()}`,
    role: 'user',
    text: prompt,
  })

  chatMessages.value.push({
    id: `assistant-${Date.now() + 1}`,
    role: 'assistant',
    text: createAssistantReply(prompt),
  })

  chatDraft.value = ''
}

function createAssistantReply(prompt: string) {
  const query = prompt.toLowerCase()
  const project = selectedProject.value
  const topMember = sortedTeam.value[0]

  if (query.includes('overdue')) {
    return `${dashboard.value.stats.overdueTasks} task(s) are overdue. Focus first on ${riskSignals.value[0]?.title.toLowerCase() ?? 'clearing blocked work'} in ${project?.name ?? 'the workspace'}.`
  }

  if (query.includes('workload') || query.includes('highest')) {
    return `${topMember?.fullName ?? 'The team lead'} currently carries the highest visible load at ${topMember?.capacityPercent ?? 0}% capacity. Consider redistributing one in-progress item if risk stays elevated.`
  }

  if (query.includes('risk') || query.includes('block')) {
    return `${dashboard.value.riskDigest} The strongest signal inside ${project?.name ?? 'the active project'} is ${riskSignals.value[0]?.detail.toLowerCase() ?? 'stable execution'}.`
  }

  if (query.includes('summary')) {
    return dashboard.value.summary
  }

  return `For ${project?.name ?? 'the active workspace'}, I would prioritize overdue items, confirm ownership on unassigned tasks, and review whether the next milestone still matches current team capacity.`
}

function normalizeStatus(status: string) {
  return status === 'InProgress' ? 'InProgress' : status === 'Done' ? 'Done' : 'Todo'
}

function isHighPriority(priority: string) {
  return priority === 'High' || priority === 'Critical'
}

function isTaskOverdue(task: DashboardTask) {
  return Boolean(task.dueDate) && new Date(task.dueDate as string).getTime() < Date.now() && normalizeStatus(task.status) !== 'Done'
}

function formatRelativeDate(value: string | null) {
  if (!value) {
    return 'without a due date'
  }

  const date = new Date(value)
  const deltaDays = Math.round((date.getTime() - Date.now()) / 86400000)

  if (deltaDays === 0) {
    return 'today'
  }

  if (deltaDays === 1) {
    return 'tomorrow'
  }

  if (deltaDays === -1) {
    return 'yesterday'
  }

  if (deltaDays < 0) {
    return `${Math.abs(deltaDays)} day(s) ago`
  }

  return `in ${deltaDays} day(s)`
}

function formatDate(value: string | null) {
  if (!value) {
    return 'No date'
  }

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

function initials(name: string) {
  return name
    .split(' ')
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase() ?? '')
    .join('')
}

function memberTone(index: number) {
  return ['ember', 'teal', 'ink', 'gold'][index % 4]
}

function metricToneClass(tone: string) {
  return `metric-card--${tone}`
}

function statusClass(status: string) {
  switch (normalizeStatus(status)) {
    case 'Done':
      return 'badge badge--done'
    case 'InProgress':
      return 'badge badge--progress'
    default:
      return 'badge badge--todo'
  }
}

function priorityClass(priority: string) {
  switch (priority) {
    case 'Critical':
      return 'badge badge--critical'
    case 'High':
      return 'badge badge--warning'
    case 'Medium':
      return 'badge badge--info'
    default:
      return 'badge badge--neutral'
  }
}

function notificationClass(notification: DashboardNotification) {
  return `notice notice--${notification.tone}`
}

function loadClass(index: number) {
  return `reveal delay-${Math.min(index + 1, 6)}`
}

function memberLoad(member: DashboardMember) {
  if (member.capacityPercent >= 80) {
    return 'Under pressure'
  }

  if (member.capacityPercent >= 60) {
    return 'Balanced load'
  }

  return 'Has spare capacity'
}
</script>

<template>
  <div class="workspace-shell">
    <aside class="workspace-sidebar" :class="{ 'is-open': sidebarOpen }">
      <div class="brand-block">
        <div class="brand-mark">Q</div>
        <div>
          <p class="eyebrow">Internal delivery system</p>
          <h1>Qaly Control Room</h1>
        </div>
      </div>

      <div class="sidebar-card">
        <p class="sidebar-card__label">Workspace mode</p>
        <p class="sidebar-card__value">{{ usingFallback ? 'Design fallback' : 'Live seeded data' }}</p>
        <p class="sidebar-card__detail">
          {{ usingFallback ? 'UI is showing resilient sample data while APIs warm up.' : 'Connected to the current SQL Server seed set.' }}
        </p>
      </div>

      <nav class="sidebar-nav">
        <button
          v-for="item in navigation"
          :key="item.target"
          class="nav-link"
          type="button"
          @click="scrollToSection(item.target)"
        >
          <component :is="item.icon" :size="18" />
          <span>{{ item.label }}</span>
          <ChevronRight :size="16" />
        </button>
      </nav>

      <div class="sidebar-footer">
        <div class="avatar-stack">
          <span
            v-for="(member, index) in highlightedMembers"
            :key="member.id"
            class="avatar-dot"
            :class="`avatar-dot--${memberTone(index)}`"
          >
            {{ initials(member.fullName) }}
          </span>
        </div>
        <p class="sidebar-footer__title">Core contributors online</p>
        <p class="sidebar-footer__detail">
          {{ dashboard.stats.teamMembers }} team member(s) are visible in the active workspace.
        </p>
      </div>
    </aside>

    <div v-if="sidebarOpen" class="workspace-backdrop" @click="sidebarOpen = false"></div>

    <div class="workspace-main">
      <header class="workspace-header">
        <div class="header-intro">
          <button class="icon-button mobile-only" type="button" @click="sidebarOpen = true">
            <Menu :size="20" />
          </button>
          <div>
            <p class="eyebrow">Sprint command center</p>
            <h2>Build, monitor, and steer delivery from one surface.</h2>
          </div>
        </div>

        <label class="workspace-search" aria-label="Search projects and tasks">
          <Search :size="18" />
          <input
            v-model="searchQuery"
            type="search"
            placeholder="Search projects, tasks, people..."
          />
        </label>

        <div class="header-actions">
          <button class="icon-button" type="button" @click="notificationsOpen = !notificationsOpen">
            <Bell :size="18" />
            <span v-if="notificationCount > 0" class="action-badge">{{ notificationCount }}</span>
          </button>
          <button class="icon-button" type="button" @click="openChatWithPrompt()">
            <Bot :size="18" />
          </button>
          <div class="user-chip">
            <span class="user-chip__avatar">AD</span>
            <div>
              <strong>Admin User</strong>
              <small>Workspace owner</small>
            </div>
          </div>

          <div v-if="notificationsOpen" class="notification-popover surface">
            <div class="section-heading">
              <div>
                <p class="eyebrow">Notifications</p>
                <h3>Current signals</h3>
              </div>
            </div>
            <div class="notice-list">
              <article
                v-for="notification in dashboard.notifications.slice(0, 4)"
                :key="notification.id"
                :class="notificationClass(notification)"
              >
                <strong>{{ notification.title }}</strong>
                <p>{{ notification.message }}</p>
                <span>{{ formatTime(notification.createdAt) }}</span>
              </article>
            </div>
          </div>
        </div>
      </header>

      <main class="workspace-content">
        <section id="overview" class="hero-grid">
          <article class="hero-panel reveal">
            <div class="hero-copy">
              <p class="eyebrow">Operational cockpit</p>
              <h3>UI built around the Qaly docs: dashboard, task board, team pulse, and AI guidance.</h3>
              <p>
                {{ dashboard.summary }}
              </p>

              <div class="hero-actions">
                <button class="primary-button" type="button" @click="openChatWithPrompt('Summarize risk for the active workspace')">
                  <Sparkles :size="18" />
                  <span>Open AI Copilot</span>
                </button>
                <button class="secondary-button" type="button" @click="scrollToSection('tasks')">
                  <ClipboardList :size="18" />
                  <span>Review Task Board</span>
                </button>
              </div>
            </div>

            <div class="hero-aside">
              <div class="hero-signal">
                <span class="hero-signal__label">Risk digest</span>
                <p>{{ dashboard.riskDigest }}</p>
              </div>
              <div class="hero-signal hero-signal--light">
                <span class="hero-signal__label">Active project</span>
                <strong>{{ selectedProject?.name ?? 'No project selected' }}</strong>
                <p>{{ projectNarrative }}</p>
              </div>
            </div>
          </article>

          <article class="surface workspace-sync reveal delay-2">
            <div class="section-heading">
              <div>
                <p class="eyebrow">Sync status</p>
                <h3>{{ isLoading ? 'Refreshing workspace...' : 'Dashboard ready' }}</h3>
              </div>
              <span class="sync-pill" :class="{ 'sync-pill--fallback': usingFallback }">
                {{ usingFallback ? 'Fallback data' : 'Live data' }}
              </span>
            </div>
            <p>
              Generated {{ formatTime(dashboard.generatedAt) }}. This surface uses a Vue shell on top of the existing Razor host so the layout stays fast while the feature UI can evolve independently.
            </p>
          </article>
        </section>

        <section class="metric-grid">
          <article
            v-for="(metric, index) in metrics"
            :key="metric.label"
            class="metric-card"
            :class="[metricToneClass(metric.tone), loadClass(index)]"
          >
            <div class="metric-card__icon">
              <component :is="metric.icon" :size="20" />
            </div>
            <div>
              <span class="metric-card__label">{{ metric.label }}</span>
              <strong class="metric-card__value">{{ metric.value }}</strong>
              <p class="metric-card__detail">{{ metric.detail }}</p>
            </div>
          </article>
        </section>

        <section class="two-column-grid">
          <article class="surface spotlight-card reveal delay-2">
            <div class="section-heading">
              <div>
                <p class="eyebrow">Project spotlight</p>
                <h3>{{ selectedProject?.name ?? 'Waiting for project data' }}</h3>
              </div>
              <span :class="statusClass(selectedProject?.status ?? 'Todo')">
                {{ selectedProject?.status ?? 'Unknown' }}
              </span>
            </div>

            <div class="project-switcher">
              <button
                v-for="project in filteredProjects"
                :key="project.id"
                type="button"
                class="project-switcher__chip"
                :class="{ 'is-active': selectedProject?.id === project.id }"
                @click="activeProjectId = project.id"
              >
                {{ project.name }}
              </button>
            </div>

            <div v-if="selectedProject" class="spotlight-body">
              <p class="spotlight-description">
                {{ selectedProject.description || 'Execution stream for the current sprint.' }}
              </p>

              <div class="progress-rail">
                <div class="progress-rail__bar" :style="{ width: `${selectedProject.progressPercentage}%` }"></div>
              </div>

              <div class="spotlight-metrics">
                <div>
                  <span>Progress</span>
                  <strong>{{ selectedProject.progressPercentage }}%</strong>
                </div>
                <div>
                  <span>Tasks</span>
                  <strong>{{ selectedProject.taskCount }}</strong>
                </div>
                <div>
                  <span>Members</span>
                  <strong>{{ selectedProject.memberCount }}</strong>
                </div>
                <div>
                  <span>Owner</span>
                  <strong>{{ selectedProject.ownerName }}</strong>
                </div>
              </div>

              <div class="member-cloud">
                <span
                  v-for="(memberName, index) in selectedProject.memberNames"
                  :key="memberName"
                  class="member-cloud__item"
                  :class="`member-cloud__item--${memberTone(index)}`"
                >
                  {{ initials(memberName) }} {{ memberName }}
                </span>
              </div>
            </div>
          </article>

          <article id="insights" class="surface insight-card reveal delay-3">
            <div class="section-heading">
              <div>
                <p class="eyebrow">AI signals</p>
                <h3>Derived guidance for the current sprint.</h3>
              </div>
              <Zap :size="18" />
            </div>

            <div class="signal-list">
              <article
                v-for="signal in riskSignals"
                :key="signal.title"
                class="signal"
                :class="`signal--${signal.tone}`"
              >
                <strong>{{ signal.title }}</strong>
                <p>{{ signal.detail }}</p>
              </article>
            </div>

            <div class="prompt-list">
              <button
                v-for="prompt in quickPrompts"
                :key="prompt"
                type="button"
                class="prompt-chip"
                @click="openChatWithPrompt(prompt)"
              >
                {{ prompt }}
              </button>
            </div>
          </article>
        </section>

        <section id="projects" class="surface section-block reveal delay-3">
          <div class="section-heading">
            <div>
              <p class="eyebrow">Project catalog</p>
              <h3>Portfolio view aligned to the implementation roadmap.</h3>
            </div>
            <span class="muted-note">{{ filteredProjects.length }} project(s) visible</span>
          </div>

          <div class="project-grid">
            <article
              v-for="(project, index) in filteredProjects"
              :key="project.id"
              class="project-card"
              :class="loadClass(index)"
            >
              <div class="project-card__topline">
                <span :class="statusClass(project.status)">{{ project.status }}</span>
                <span class="muted-note">{{ project.memberCount }} people</span>
              </div>
              <h4>{{ project.name }}</h4>
              <p>{{ project.description || 'Execution track in the Qaly workspace.' }}</p>

              <div class="project-card__meta">
                <span>Owner {{ project.ownerName }}</span>
                <span>{{ project.completedTaskCount }}/{{ project.taskCount }} done</span>
              </div>

              <div class="progress-rail progress-rail--compact">
                <div class="progress-rail__bar" :style="{ width: `${project.progressPercentage}%` }"></div>
              </div>

              <button class="text-button" type="button" @click="activeProjectId = project.id; scrollToSection('tasks')">
                Open board
                <ChevronRight :size="16" />
              </button>
            </article>
          </div>
        </section>

        <section id="tasks" class="surface section-block reveal delay-4">
          <div class="section-heading">
            <div>
              <p class="eyebrow">Task board</p>
              <h3>{{ selectedProject?.name ?? 'Current project' }} execution lanes.</h3>
            </div>
            <span class="muted-note">{{ visibleTasks.length }} task(s) in view</span>
          </div>

          <div class="kanban-grid">
            <article v-for="column in kanbanColumns" :key="column.key" class="kanban-column">
              <div class="kanban-column__header">
                <strong>{{ column.label }}</strong>
                <span>{{ column.items.length }}</span>
              </div>

              <div class="kanban-stack">
                <article v-for="task in column.items" :key="task.id" class="task-card">
                  <div class="task-card__row">
                    <span :class="priorityClass(task.priority)">{{ task.priority }}</span>
                    <span v-if="task.isPrivate" class="badge badge--neutral">Private</span>
                  </div>

                  <h4>{{ task.title }}</h4>
                  <p>{{ task.assigneeName || 'Unassigned' }} · {{ task.reporterName }}</p>

                  <div class="task-card__meta">
                    <span><Clock3 :size="14" /> {{ formatDate(task.dueDate) }}</span>
                    <span>{{ task.commentCount }} comments</span>
                  </div>
                </article>

                <div v-if="column.items.length === 0" class="empty-state">
                  No matching tasks in this lane.
                </div>
              </div>
            </article>
          </div>
        </section>

        <section id="team" class="team-grid">
          <article class="surface section-block reveal delay-5">
            <div class="section-heading">
              <div>
                <p class="eyebrow">Team pulse</p>
                <h3>Visible workload and capacity across contributors.</h3>
              </div>
              <ChartNoAxesCombined :size="18" />
            </div>

            <div class="team-list">
              <article v-for="(member, index) in sortedTeam" :key="member.id" class="team-card" :class="loadClass(index)">
                <div class="team-card__identity">
                  <span class="team-card__avatar" :class="`team-card__avatar--${memberTone(index)}`">
                    {{ initials(member.fullName) }}
                  </span>
                  <div>
                    <strong>{{ member.fullName }}</strong>
                    <p>{{ member.role }} · {{ member.focusArea }}</p>
                  </div>
                </div>

                <div class="team-card__stats">
                  <span>{{ member.assignedTaskCount }} assigned</span>
                  <span>{{ member.completedTaskCount }} done</span>
                  <span>{{ memberLoad(member) }}</span>
                </div>

                <div class="progress-rail progress-rail--compact">
                  <div class="progress-rail__bar progress-rail__bar--soft" :style="{ width: `${member.capacityPercent}%` }"></div>
                </div>
              </article>
            </div>
          </article>

          <article class="surface section-block reveal delay-6">
            <div class="section-heading">
              <div>
                <p class="eyebrow">Notification lane</p>
                <h3>Signals worth bringing into standup.</h3>
              </div>
              <MessageSquareText :size="18" />
            </div>

            <div class="notice-list">
              <article
                v-for="notification in dashboard.notifications"
                :key="notification.id"
                :class="notificationClass(notification)"
              >
                <strong>{{ notification.title }}</strong>
                <p>{{ notification.message }}</p>
                <span>{{ formatTime(notification.createdAt) }}</span>
              </article>
            </div>
          </article>
        </section>
      </main>
    </div>

    <button class="chat-launcher" type="button" @click="chatOpen = true">
      <Bot :size="18" />
      <span>AI Copilot</span>
    </button>

    <aside class="chat-drawer" :class="{ 'is-open': chatOpen }">
      <div class="chat-drawer__header">
        <div>
          <p class="eyebrow">AI assistant</p>
          <h3>Qaly Bot</h3>
        </div>
        <button class="icon-button" type="button" @click="chatOpen = false">
          <X :size="18" />
        </button>
      </div>

      <div class="chat-drawer__body">
        <article
          v-for="message in chatMessages"
          :key="message.id"
          class="chat-bubble"
          :class="`chat-bubble--${message.role}`"
        >
          {{ message.text }}
        </article>
      </div>

      <div class="prompt-list prompt-list--stacked">
        <button v-for="prompt in quickPrompts" :key="prompt" type="button" class="prompt-chip" @click="submitChat(prompt)">
          {{ prompt }}
        </button>
      </div>

      <form class="chat-drawer__composer" @submit.prevent="submitChat()">
        <input v-model="chatDraft" type="text" placeholder="Ask about risks, workload, or next actions..." />
        <button class="primary-button primary-button--compact" type="submit">
          Ask
        </button>
      </form>
    </aside>
  </div>
</template>
