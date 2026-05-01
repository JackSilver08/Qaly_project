<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import {
  Bell,
  Bot,
  BriefcaseBusiness,
  CalendarDays,
  ChartNoAxesCombined,
  CheckCheck,
  ChevronRight,
  ClipboardList,
  FolderKanban,
  LayoutDashboard,
  Menu,
  MessageSquareText,
  Search,
  ShieldAlert,
  SlidersHorizontal,
  Sparkles,
  Users,
  X,
  Zap,
} from 'lucide-vue-next'
import { fallbackDashboard } from './fallback-dashboard'
import type {
  DashboardMember,
  DashboardNotification,
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

interface CalendarDay {
  key: string
  label: number
  isCurrentMonth: boolean
  isToday: boolean
  isSelected: boolean
  hasDueTask: boolean
}

const navigation: NavItem[] = [
  { label: 'Tổng quan', target: 'overview', icon: LayoutDashboard },
  { label: 'Dự án', target: 'projects', icon: FolderKanban },
  { label: 'Bảng công việc', target: 'tasks', icon: ClipboardList },
  { label: 'Nhịp đội ngũ', target: 'team', icon: Users },
  { label: 'Tín hiệu AI', target: 'insights', icon: Sparkles },
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
    text: 'Tôi có thể tóm tắt rủi ro triển khai, chỉ ra công việc quá hạn và gợi ý hành động tiếp theo cho dự án đang chọn.',
  },
])

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
  'Dự án quản lý công việc nội bộ - Minimum Viable Product':
    'Dự án quản lý công việc nội bộ - sản phẩm khả dụng tối thiểu',
  'Design workspace shell': 'Thiết kế khung làm việc',
  'Implement dashboard metrics API': 'Triển khai API chỉ số bảng điều khiển',
  'Create Kanban interaction states': 'Tạo trạng thái tương tác Kanban',
  'Integrate AI insight surfaces': 'Tích hợp bề mặt gợi ý AI',
  'Register page validation': 'Xác thực trang đăng ký',
  'Permission middleware rollout': 'Triển khai middleware phân quyền',
  'Craft color system': 'Xây dựng hệ màu',
  'Responsive shell audit': 'Rà soát khung tương thích đa màn hình',
  'Thiết kế database schema': 'Thiết kế lược đồ cơ sở dữ liệu',
  'Implement Authentication': 'Triển khai xác thực',
  'Tạo Dashboard UI': 'Tạo giao diện bảng điều khiển',
  'Tích hợp AI Assistant': 'Tích hợp trợ lý AI',
  'Viết Unit Tests': 'Viết kiểm thử đơn vị',
  'Project coordination': 'Điều phối dự án',
  'Capacity available': 'Còn năng lực tiếp nhận',
  'Execution stream': 'Luồng triển khai',
  'Support lane': 'Hỗ trợ vận hành',
  'Design systems': 'Hệ thống thiết kế',
}

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
      displayText(project.name),
      displayText(project.description ?? ''),
      displayName(project.ownerName),
      ...project.memberNames.map(displayName),
    ]
      .join(' ')
      .toLowerCase()

    if (projectText.includes(query)) {
      return true
    }

    return project.tasks.some((task) =>
      [
        displayText(task.title),
        task.priority,
        task.status,
        displayPriority(task.priority),
        displayStatus(task.status),
        displayName(task.assigneeName),
        displayName(task.reporterName),
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
      displayText(task.title),
      task.priority,
      task.status,
      displayPriority(task.priority),
      displayStatus(task.status),
      displayName(task.assigneeName),
      displayName(task.reporterName),
      displayText(task.projectName),
    ]
      .join(' ')
      .toLowerCase()
      .includes(query),
  )
})

const metrics = computed<MetricCard[]>(() => [
  {
    label: 'Dự án đang chạy',
    value: String(dashboard.value.stats.activeProjects),
    detail: 'Luồng công việc liên phòng ban đang tiến triển',
    tone: 'amber',
    icon: BriefcaseBusiness,
  },
  {
    label: 'Tỷ lệ hoàn thành',
    value: `${dashboard.value.stats.completionRate}%`,
    detail: `${dashboard.value.stats.completedTasks} công việc đã hoàn tất trên toàn bảng`,
    tone: 'teal',
    icon: CheckCheck,
  },
  {
    label: 'Công việc rủi ro',
    value: String(dashboard.value.stats.tasksAtRisk),
    detail: `${dashboard.value.stats.overdueTasks} mục đã quá hạn`,
    tone: 'coral',
    icon: ShieldAlert,
  },
  {
    label: 'Nhịp đội ngũ',
    value: String(dashboard.value.stats.teamMembers),
    detail: 'Thành viên đang đóng góp trong không gian làm việc',
    tone: 'ink',
    icon: Users,
  },
])

const kanbanColumns = computed(() => {
  const columns = [
    { key: 'Todo', label: 'Cần làm', items: [] as DashboardTask[] },
    { key: 'InProgress', label: 'Đang làm', items: [] as DashboardTask[] },
    { key: 'Done', label: 'Hoàn tất', items: [] as DashboardTask[] },
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
      title: 'Trễ hạn triển khai',
      detail: `${overdue.length} công việc quá hạn đang làm chậm ${displayText(project.name)}.`,
      tone: 'critical',
    })
  }

  if (unassigned.length > 0) {
    signals.push({
      title: 'Thiếu người phụ trách',
      detail: `${unassigned.length} mục đang mở vẫn cần người phụ trách rõ ràng.`,
      tone: 'warning',
    })
  }

  if (highPriority.length > 0) {
    signals.push({
      title: 'Luồng ưu tiên cao',
      detail: `${highPriority.length} công việc ưu tiên cao cần được theo dõi hằng ngày.`,
      tone: 'info',
    })
  }

  if (signals.length === 0) {
    signals.push({
      title: 'Triển khai ổn định',
      detail: `${displayText(project.name)} chưa có cảnh báo giao hàng khẩn cấp trong ảnh chụp hiện tại.`,
      tone: 'positive',
    })
  }

  return signals.slice(0, 3)
})

const quickPrompts = computed(() => {
  const project = selectedProject.value
  const projectName = project ? displayText(project.name) : 'không gian làm việc này'

  return [
    `Tóm tắt rủi ro của ${projectName}`,
    'Công việc nào đang quá hạn?',
    'Ai đang có tải công việc cao nhất?',
  ]
})

const highlightedMembers = computed(() => sortedTeam.value.slice(0, 4))
const notificationCount = computed(
  () => dashboard.value.notifications.filter((notification) => notification.tone !== 'info').length,
)

const projectNarrative = computed(() => {
  const project = selectedProject.value

  if (!project) {
    return 'Chưa chọn dự án nào.'
  }

  const dueSoon = project.tasks
    .filter((task) => normalizeStatus(task.status) !== 'Done' && task.dueDate)
    .sort((left, right) => new Date(left.dueDate ?? '').getTime() - new Date(right.dueDate ?? '').getTime())
    .at(0)

  if (!dueSoon) {
    return `${displayText(project.name)} đã hoàn thành ${project.progressPercentage}% và hiện chưa có áp lực hạn chót khẩn cấp.`
  }

  return `${displayText(project.name)} đã hoàn thành ${project.progressPercentage}%. Mốc quan trọng tiếp theo là "${displayText(dueSoon.title)}" đến hạn ${formatRelativeDate(dueSoon.dueDate)}.`
})

const activeTasks = computed(() =>
  visibleTasks.value
    .filter((task) => normalizeStatus(task.status) !== 'Done')
    .slice(0, 5),
)

const completedPreviewTasks = computed(() =>
  visibleTasks.value
    .filter((task) => normalizeStatus(task.status) === 'Done')
    .slice(0, 3),
)

const selectedProgress = computed(() => selectedProject.value?.progressPercentage ?? dashboard.value.stats.completionRate)

const donutStyle = computed(() => ({
  '--progress': `${selectedProgress.value}%`,
}))

const chartBars = computed(() => {
  const total = Math.max(visibleTasks.value.length, 1)

  return kanbanColumns.value.map((column, index) => ({
    label: column.label,
    value: column.items.length,
    percent: Math.max(10, Math.round((column.items.length / total) * 100)),
    tone: ['blue', 'violet', 'mint'][index],
  }))
})

const rightPanelStats = computed(() => [
  {
    value: dashboard.value.stats.overdueTasks,
    label: 'Quá hạn',
    detail: 'Cần xử lý',
    tone: 'peach',
  },
  {
    value: dashboard.value.stats.completedTasks,
    label: 'Hoàn tất',
    detail: 'Đã đóng',
    tone: 'violet',
  },
  {
    value: dashboard.value.stats.tasksAtRisk,
    label: 'Rủi ro',
    detail: 'Theo dõi',
    tone: 'mint',
  },
  {
    value: dashboard.value.stats.totalTasks,
    label: 'Tổng việc',
    detail: 'Toàn hệ thống',
    tone: 'blue',
  },
])

const calendarMonth = computed(() =>
  new Intl.DateTimeFormat('vi-VN', {
    month: 'long',
    year: 'numeric',
  }).format(new Date(dashboard.value.generatedAt)),
)

const calendarDays = computed<CalendarDay[]>(() => {
  const base = new Date(dashboard.value.generatedAt)
  const year = base.getFullYear()
  const month = base.getMonth()
  const firstDay = new Date(year, month, 1)
  const mondayOffset = (firstDay.getDay() + 6) % 7
  const start = new Date(year, month, 1 - mondayOffset)
  const dueDates = new Set(visibleTasks.value.filter((task) => task.dueDate).map((task) => dateKey(new Date(task.dueDate as string))))

  return Array.from({ length: 35 }, (_, index) => {
    const date = new Date(start)
    date.setDate(start.getDate() + index)
    const key = dateKey(date)

    return {
      key,
      label: date.getDate(),
      isCurrentMonth: date.getMonth() === month,
      isToday: key === dateKey(new Date()),
      isSelected: key === dateKey(base),
      hasDueTask: dueDates.has(key),
    }
  })
})

const todayAgenda = computed(() => {
  const urgent = activeTasks.value[0]

  return {
    title: urgent ? displayText(urgent.title) : 'Không có việc khẩn cấp',
    caption: urgent ? `${displayPriority(urgent.priority)} - ${formatRelativeDate(urgent.dueDate)}` : 'Đội có thể tập trung vào cải thiện chất lượng.',
  }
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
      throw new Error(`Yêu cầu bảng điều khiển thất bại với mã ${response.status}`)
    }

    const data = (await response.json()) as DashboardResponse

    if (!data.projects?.length) {
      throw new Error('Bảng điều khiển không trả về dự án nào')
    }

    dashboard.value = data
    usingFallback.value = false
  } catch (error) {
    console.warn('Đang dùng dữ liệu mẫu của bảng điều khiển.', error)
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

  if (includesAny(query, ['overdue', 'quá hạn', 'qua han'])) {
    return `${dashboard.value.stats.overdueTasks} công việc đang quá hạn. Hãy ưu tiên ${riskSignals.value[0]?.title.toLowerCase() ?? 'gỡ các việc đang bị chặn'} trong ${project ? displayText(project.name) : 'không gian làm việc'}.`
  }

  if (includesAny(query, ['workload', 'highest', 'tải', 'tai', 'cao nhất', 'cao nhat'])) {
    return `${displayName(topMember?.fullName) || 'Trưởng nhóm'} đang có tải công việc cao nhất ở mức ${topMember?.capacityPercent ?? 0}%. Nếu rủi ro còn cao, nên điều phối lại một việc đang làm.`
  }

  if (includesAny(query, ['risk', 'block', 'rủi ro', 'rui ro', 'chặn', 'chan'])) {
    return `${dashboard.value.riskDigest} Tín hiệu mạnh nhất trong ${project ? displayText(project.name) : 'dự án đang chọn'} là ${riskSignals.value[0]?.detail.toLowerCase() ?? 'triển khai ổn định'}.`
  }

  if (includesAny(query, ['summary', 'tóm tắt', 'tom tat'])) {
    return dashboard.value.summary
  }

  return `Với ${project ? displayText(project.name) : 'không gian làm việc đang chọn'}, tôi sẽ ưu tiên việc quá hạn, xác nhận người phụ trách cho các mục chưa giao và rà lại mốc tiếp theo so với năng lực hiện tại của đội.`
}

function includesAny(value: string, needles: string[]) {
  return needles.some((needle) => value.includes(needle))
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
    return 'khi chưa có hạn chót'
  }

  const date = new Date(value)
  const deltaDays = Math.round((date.getTime() - Date.now()) / 86400000)

  if (deltaDays === 0) {
    return 'hôm nay'
  }

  if (deltaDays === 1) {
    return 'ngày mai'
  }

  if (deltaDays === -1) {
    return 'hôm qua'
  }

  if (deltaDays < 0) {
    return `${Math.abs(deltaDays)} ngày trước`
  }

  return `sau ${deltaDays} ngày`
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

function dateKey(date: Date) {
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`
}

function formatTime(value: string) {
  return new Intl.DateTimeFormat('vi-VN', {
    hour: 'numeric',
    minute: '2-digit',
  }).format(new Date(value))
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

function displayPriority(priority: string | null | undefined) {
  switch (priority) {
    case 'Critical':
      return 'Khẩn cấp'
    case 'High':
      return 'Cao'
    case 'Medium':
      return 'Trung bình'
    case 'Low':
      return 'Thấp'
    default:
      return priority ? displayText(priority) : 'Không xác định'
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
    return 'Đang chịu áp lực'
  }

  if (member.capacityPercent >= 60) {
    return 'Tải công việc cân bằng'
  }

  return 'Còn năng lực tiếp nhận'
}
</script>

<template>
  <div class="dashboard-shell">
    <aside class="app-sidebar" :class="{ 'is-open': sidebarOpen }">
      <div class="brand-row">
        <div class="brand-bars" aria-hidden="true">
          <span></span>
          <span></span>
          <span></span>
        </div>
        <strong>QALY</strong>
      </div>

      <section class="profile-card">
        <div class="profile-avatar">QT</div>
        <strong>Quản trị viên</strong>
        <span>Chủ không gian làm việc</span>
        <small>{{ usingFallback ? 'Dữ liệu mẫu' : 'Dữ liệu trực tiếp' }}</small>
      </section>

      <nav class="sidebar-section" aria-label="Điều hướng chính">
        <p>Dự án & công việc</p>
        <button
          v-for="item in navigation"
          :key="item.target"
          class="sidebar-link"
          type="button"
          @click="scrollToSection(item.target)"
        >
          <component :is="item.icon" :size="17" />
          <span>{{ item.label }}</span>
        </button>
      </nav>

      <section class="sidebar-section sidebar-section--bottom">
        <p>Đội ngũ</p>
        <div class="team-preview">
          <span
            v-for="(member, index) in highlightedMembers"
            :key="member.id"
            class="avatar-token"
            :class="`avatar-token--${memberTone(index)}`"
          >
            {{ initials(displayName(member.fullName)) }}
          </span>
        </div>
        <button class="ai-chip" type="button" @click="openChatWithPrompt()">
          <Bot :size="16" />
          <span>Trợ lý Qaly</span>
        </button>
      </section>
    </aside>

    <div v-if="sidebarOpen" class="workspace-backdrop" @click="sidebarOpen = false"></div>

    <main class="dashboard-main">
      <header class="topbar">
        <div class="topbar-title">
          <button class="icon-button mobile-only" type="button" @click="sidebarOpen = true">
            <Menu :size="18" />
          </button>
          <div>
            <h1>Xin chào, Qaly.</h1>
            <p>{{ projectNarrative }}</p>
          </div>
        </div>

        <label class="search-box" aria-label="Tìm kiếm dashboard">
          <Search :size="17" />
          <input v-model="searchQuery" type="search" placeholder="Tìm dự án, công việc, thành viên..." />
          <SlidersHorizontal :size="16" />
        </label>
      </header>

      <section id="overview" class="hero-grid">
        <article class="project-hero glass-card">
          <div class="card-icon card-icon--sun">
            <Sparkles :size="18" />
          </div>
          <button class="more-button" type="button" aria-label="Mở tùy chọn">•••</button>
          <div class="project-hero__body">
            <span>Dự án trọng tâm</span>
            <h2>{{ selectedProject ? displayText(selectedProject.name) : 'Chưa có dự án' }}</h2>
            <p>{{ selectedProject?.description ? displayText(selectedProject.description) : dashboard.summary }}</p>
          </div>
          <div class="avatar-row">
            <span
              v-for="(memberName, index) in selectedProject?.memberNames.slice(0, 4) ?? []"
              :key="memberName"
              class="avatar-token"
              :class="`avatar-token--${memberTone(index)}`"
            >
              {{ initials(displayName(memberName)) }}
            </span>
          </div>
        </article>

        <article class="chart-card glass-card">
          <div class="card-icon card-icon--violet">
            <ChartNoAxesCombined :size="18" />
          </div>
          <div class="donut-chart" :style="donutStyle">
            <strong>{{ selectedProgress }}%</strong>
          </div>
          <div class="chart-copy">
            <span>Tỷ lệ hoàn thành</span>
            <h2>{{ selectedProject ? displayText(selectedProject.name) : 'Toàn bộ workspace' }}</h2>
            <p>{{ dashboard.riskDigest }}</p>
          </div>
        </article>
      </section>

      <section class="metric-grid">
        <article
          v-for="(metric, index) in metrics"
          :key="metric.label"
          class="metric-card glass-card"
          :class="[metricToneClass(metric.tone), loadClass(index)]"
        >
          <div class="metric-card__icon">
            <component :is="metric.icon" :size="18" />
          </div>
          <span>{{ metric.label }}</span>
          <strong>{{ metric.value }}</strong>
          <p>{{ metric.detail }}</p>
        </article>
      </section>

      <section class="content-grid">
        <article id="tasks" class="task-panel glass-card">
          <div class="panel-heading">
            <div>
              <span>Công việc</span>
              <h2>Active Tasks</h2>
            </div>
            <div class="task-tabs">
              <button class="is-active" type="button">Đang làm</button>
              <button type="button">Hoàn tất {{ completedPreviewTasks.length }}</button>
            </div>
          </div>

          <div class="task-list">
            <article v-for="(task, index) in activeTasks" :key="task.id" class="task-row">
              <div class="task-logo" :class="`task-logo--${memberTone(index)}`">
                {{ initials(displayText(task.title)) }}
              </div>
              <div class="task-row__content">
                <strong>{{ displayText(task.title) }}</strong>
                <span>{{ task.assigneeName ? displayName(task.assigneeName) : 'Chưa giao' }} - {{ formatRelativeDate(task.dueDate) }}</span>
              </div>
              <div class="task-row__meta">
                <span :class="priorityClass(task.priority)">{{ displayPriority(task.priority) }}</span>
                <div class="mini-avatars">
                  <span>{{ initials(displayName(task.reporterName)) }}</span>
                  <span v-if="task.assigneeName">{{ initials(displayName(task.assigneeName)) }}</span>
                </div>
              </div>
            </article>

            <div v-if="activeTasks.length === 0" class="empty-state">
              Không có công việc đang mở trong chế độ xem hiện tại.
            </div>
          </div>
        </article>

        <article id="projects" class="project-panel glass-card">
          <div class="panel-heading">
            <div>
              <span>Danh mục</span>
              <h2>Dự án</h2>
            </div>
            <span class="count-pill">{{ filteredProjects.length }}</span>
          </div>

          <div class="project-list">
            <button
              v-for="project in filteredProjects"
              :key="project.id"
              type="button"
              class="project-strip"
              :class="{ 'is-active': selectedProject?.id === project.id }"
              @click="activeProjectId = project.id"
            >
              <div>
                <strong>{{ displayText(project.name) }}</strong>
                <span>{{ displayName(project.ownerName) }} - {{ project.completedTaskCount }}/{{ project.taskCount }} hoàn tất</span>
              </div>
              <div class="strip-progress">
                <span :style="{ width: `${project.progressPercentage}%` }"></span>
              </div>
            </button>
          </div>
        </article>
      </section>

      <section id="team" class="team-panel glass-card">
        <div class="panel-heading">
          <div>
            <span>Nhịp đội ngũ</span>
            <h2>Năng lực hiện tại</h2>
          </div>
          <Users :size="18" />
        </div>

        <div class="team-grid">
          <article v-for="(member, index) in sortedTeam" :key="member.id" class="team-card">
            <span class="avatar-token" :class="`avatar-token--${memberTone(index)}`">
              {{ initials(displayName(member.fullName)) }}
            </span>
            <div>
              <strong>{{ displayName(member.fullName) }}</strong>
              <p>{{ displayRole(member.role) }} - {{ displayText(member.focusArea) }}</p>
            </div>
            <div class="strip-progress">
              <span :style="{ width: `${member.capacityPercent}%` }"></span>
            </div>
          </article>
        </div>
      </section>
    </main>

    <aside class="dashboard-aside">
      <div class="aside-actions">
        <button class="icon-button" type="button" @click="notificationsOpen = !notificationsOpen">
          <Bell :size="18" />
          <span v-if="notificationCount > 0" class="action-badge">{{ notificationCount }}</span>
        </button>
        <button class="icon-button" type="button" @click="openChatWithPrompt()">
          <MessageSquareText :size="18" />
        </button>
        <div class="user-avatar">QT</div>
      </div>

      <section class="meeting-card glass-card">
        <span>{{ isLoading ? 'Đang đồng bộ dữ liệu' : 'Cập nhật lúc ' + formatTime(dashboard.generatedAt) }}</span>
        <h2>{{ todayAgenda.title }}</h2>
        <p>{{ todayAgenda.caption }}</p>
        <button class="call-button" type="button" @click="openChatWithPrompt('Tóm tắt rủi ro của không gian làm việc')">
          <Sparkles :size="17" />
          <span>Phân tích nhanh</span>
          <ChevronRight :size="16" />
        </button>
      </section>

      <section class="aside-stat-grid">
        <article
          v-for="stat in rightPanelStats"
          :key="stat.label"
          class="aside-stat glass-card"
          :class="`aside-stat--${stat.tone}`"
        >
          <strong>{{ stat.value }}</strong>
          <span>{{ stat.label }}</span>
          <p>{{ stat.detail }}</p>
        </article>
      </section>

      <section class="calendar-card glass-card">
        <div class="panel-heading">
          <div>
            <span>Lịch</span>
            <h2>{{ calendarMonth }}</h2>
          </div>
          <CalendarDays :size="18" />
        </div>

        <div class="calendar-grid calendar-grid--weekdays">
          <span>T2</span>
          <span>T3</span>
          <span>T4</span>
          <span>T5</span>
          <span>T6</span>
          <span>T7</span>
          <span>CN</span>
        </div>
        <div class="calendar-grid">
          <span
            v-for="day in calendarDays"
            :key="day.key"
            class="calendar-day"
            :class="{
              'is-muted': !day.isCurrentMonth,
              'is-today': day.isToday,
              'is-selected': day.isSelected,
              'has-task': day.hasDueTask,
            }"
          >
            {{ day.label }}
          </span>
        </div>
      </section>

      <section id="insights" class="insight-card glass-card">
        <div class="panel-heading">
          <div>
            <span>Tín hiệu AI</span>
            <h2>Rủi ro & luồng việc</h2>
          </div>
          <Zap :size="18" />
        </div>

        <div class="bar-chart">
          <div v-for="bar in chartBars" :key="bar.label" class="bar-row">
            <span>{{ bar.label }}</span>
            <div>
              <i :class="`bar-fill bar-fill--${bar.tone}`" :style="{ width: `${bar.percent}%` }"></i>
            </div>
            <strong>{{ bar.value }}</strong>
          </div>
        </div>

        <div class="signal-list">
          <article v-for="signal in riskSignals" :key="signal.title" class="signal-item" :class="`signal-item--${signal.tone}`">
            <strong>{{ signal.title }}</strong>
            <p>{{ signal.detail }}</p>
          </article>
        </div>
      </section>

      <div v-if="notificationsOpen" class="notification-popover glass-card">
        <div class="panel-heading">
          <div>
            <span>Thông báo</span>
            <h2>Tín hiệu hiện tại</h2>
          </div>
          <button class="icon-button icon-button--small" type="button" @click="notificationsOpen = false">
            <X :size="16" />
          </button>
        </div>
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
    </aside>

    <aside class="chat-drawer glass-card" :class="{ 'is-open': chatOpen }">
      <div class="chat-drawer__header">
        <div>
          <span>Trợ lý AI</span>
          <h2>Trợ lý Qaly</h2>
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

      <div class="prompt-list">
        <button v-for="prompt in quickPrompts" :key="prompt" type="button" class="prompt-chip" @click="submitChat(prompt)">
          {{ prompt }}
        </button>
      </div>

      <form class="chat-drawer__composer" @submit.prevent="submitChat()">
        <input v-model="chatDraft" type="text" placeholder="Hỏi về rủi ro, tải công việc..." />
        <button class="primary-button" type="submit">Hỏi</button>
      </form>
    </aside>
  </div>
</template>
