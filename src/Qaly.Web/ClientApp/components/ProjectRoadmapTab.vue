<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import {
  Compass,
  CheckCircle2,
  Clock,
  Plus,
  Sparkles,
  Calendar,
  Edit3,
  Trash2,
  FolderKanban,
  Navigation,
  AlertTriangle,
  Layers,
  Flag,
  X,
  Check,
  Eye,
  Link as LinkIcon,
  UserCheck,
  ShieldCheck,
  User,
  Shield,
  RefreshCw,
  ChevronRight,
  PlayCircle,
  Lock,
  ListTodo,
  CheckSquare,
  Users,
  Award,
  Zap,
  LayoutGrid,
  Search,
  Filter,
  ArrowRight,
  TrendingUp,
  Target
} from 'lucide-vue-next'
import { apiResult, apiCommand } from '../utils/api-client'
import { showError, showSuccess } from '../composables/use-toast'
import { useDashboardContext } from '../composables/dashboard-context'
import type { SprintDto, DashboardTask } from '../types'
import ProjectProgressAiCard from './ProjectProgressAiCard.vue'
import { useRoute, useRouter } from 'vue-router'

const props = defineProps<{
  projectId: string
  projectName?: string
  canGenerateAi: boolean
}>()

const { selectedProject, activeProjectTab, isProjectAdmin, loadDashboard } = useDashboardContext()
const route = useRoute()
const router = useRouter()

// Sprint list & loading state
const sprints = ref<SprintDto[]>([])
const isLoading = ref(false)
const selectedSprintId = ref<string | null>(null)

// View modes: Executive Client View vs Management View
const isClientViewMode = ref(false)

// Node Filter & Search
const milestoneFilter = ref<'all' | 'active' | 'completed' | 'overdue'>('all')
const milestoneSearchQuery = ref('')

// Modals state
const showCreateModal = ref(false)
const showEditModal = ref(false)
const showPresetModal = ref(false)
const showTaskAssignModal = ref(false)
const showQuickCreateTaskModal = ref(false)

// Form states for milestone creation/editing
const milestoneName = ref('')
const milestoneStartDate = ref('')
const milestoneEndDate = ref('')
const milestoneGoal = ref('')
const milestoneStatus = ref('Planning')
const editingSprintId = ref<string | null>(null)

// Form states for Quick Task Creation in milestone
const quickTaskTitle = ref('')
const quickTaskPriority = ref('Medium')
const quickTaskAssigneeId = ref('')
const quickTaskDueDate = ref('')

// Selected tasks IDs for Task Assignment Modal
const selectedTaskIdsForSprint = ref<string[]>([])
const isSavingTaskAssignment = ref(false)
const isGeneratingPreset = ref(false)

// Task status filter inside milestone detail
const milestoneTaskSearch = ref('')
const milestoneTaskStatusFilter = ref<string>('all')

onMounted(() => {
  loadSprints()
})

watch(() => props.projectId, () => {
  selectedSprintId.value = null
  loadSprints()
})

watch(selectedSprintId, sprintId => {
  if (!sprintId) return
  const hash = `#milestone-${sprintId}`
  if (route.hash !== hash) void router.replace({ hash })
})

async function loadSprints() {
  isLoading.value = true
  try {
    const result = await apiResult<SprintDto[]>(`/api/projects/${props.projectId}/sprints`)
    // Sort sprints chronologically by StartDate
    sprints.value = (result || []).sort((a, b) => new Date(a.startDate).getTime() - new Date(b.startDate).getTime())
    
    const requestedSprintId = route.hash.startsWith('#milestone-')
      ? route.hash.slice('#milestone-'.length)
      : null
    if (sprints.value.length > 0 &&
        (!selectedSprintId.value || !sprints.value.some(s => s.id === selectedSprintId.value))) {
      const current = sprints.value.find(s => s.id === requestedSprintId) ||
        sprints.value.find(s => isCurrentMilestone(s)) ||
        sprints.value[0]
      selectedSprintId.value = current.id
    }
  } catch (error) {
    showError('Không thể tải lộ trình dự án.')
  } finally {
    isLoading.value = false
  }
}

// Milestone state calculation helpers
function isCompletedMilestone(sprint: SprintDto): boolean {
  if (sprint.status === 'Completed') return true
  if (sprint.taskCount > 0 && sprint.completedTaskCount === sprint.taskCount) return true
  if (sprint.progress >= 100) return true
  return false
}

function isCurrentMilestone(sprint: SprintDto): boolean {
  if (isCompletedMilestone(sprint)) return false
  if (sprint.status === 'Active') return true
  
  const now = new Date().getTime()
  const start = new Date(sprint.startDate).getTime()
  const end = new Date(sprint.endDate).getTime()
  
  if (now >= start && now <= end) return true

  const uncompleted = sprints.value.filter(s => !isCompletedMilestone(s))
  return uncompleted.length > 0 && uncompleted[0].id === sprint.id
}

function isOverdueMilestone(sprint: SprintDto): boolean {
  if (isCompletedMilestone(sprint)) return false
  const now = new Date().getTime()
  const end = new Date(sprint.endDate).getTime()
  return now > end && sprint.progress < 100
}

const activeMilestone = computed(() => {
  return sprints.value.find(s => s.id === selectedSprintId.value) || null
})

const currentPositionMilestone = computed(() => {
  return sprints.value.find(s => isCurrentMilestone(s)) || null
})

// Filtered milestones list
const filteredSprints = computed(() => {
  let list = sprints.value
  if (milestoneFilter.value === 'active') {
    list = list.filter(s => isCurrentMilestone(s))
  } else if (milestoneFilter.value === 'completed') {
    list = list.filter(s => isCompletedMilestone(s))
  } else if (milestoneFilter.value === 'overdue') {
    list = list.filter(s => isOverdueMilestone(s))
  }

  if (milestoneSearchQuery.value.trim()) {
    const q = milestoneSearchQuery.value.trim().toLowerCase()
    list = list.filter(s => s.name.toLowerCase().includes(q) || (s.goal && s.goal.toLowerCase().includes(q)))
  }
  return list
})

// Overall project health summary
const overallProgress = computed(() => {
  if (sprints.value.length === 0) return selectedProject.value?.progressPercentage || 0
  const totalTasks = sprints.value.reduce((sum, s) => sum + s.taskCount, 0)
  const completedTasks = sprints.value.reduce((sum, s) => sum + s.completedTaskCount, 0)
  if (totalTasks > 0) return Math.round((completedTasks / totalTasks) * 100)
  
  const completedMilestones = sprints.value.filter(s => isCompletedMilestone(s)).length
  return Math.round((completedMilestones / sprints.value.length) * 100)
})

const projectHealthStatus = computed(() => {
  if (sprints.value.length === 0) return { label: 'Chưa có mốc', tone: 'muted' }
  const overdueCount = sprints.value.filter(s => isOverdueMilestone(s)).length
  if (overdueCount > 0) return { label: `Có ${overdueCount} mốc trễ hạn`, tone: 'danger' }
  const allCompleted = sprints.value.every(s => isCompletedMilestone(s))
  if (allCompleted) return { label: 'Đã hoàn thành toàn bộ mốc', tone: 'success' }
  return { label: 'Đang theo đúng tiến độ', tone: 'primary' }
})

// Milestone tasks
const allProjectTasks = computed<DashboardTask[]>(() => {
  return selectedProject.value?.tasks || []
})

const milestoneTasks = computed<DashboardTask[]>(() => {
  if (!selectedSprintId.value || !selectedProject.value?.tasks) return []
  return selectedProject.value.tasks.filter((t: DashboardTask) => t.sprintId === selectedSprintId.value)
})

const filteredMilestoneTasks = computed<DashboardTask[]>(() => {
  let list = milestoneTasks.value
  if (milestoneTaskStatusFilter.value !== 'all') {
    list = list.filter(t => t.status.toLowerCase() === milestoneTaskStatusFilter.value.toLowerCase())
  }
  if (milestoneTaskSearch.value.trim()) {
    const q = milestoneTaskSearch.value.trim().toLowerCase()
    list = list.filter(t => t.title.toLowerCase().includes(q) || (t.key && t.key.toLowerCase().includes(q)))
  }
  return list
})

// Assigned members breakdown in active milestone
const milestoneAssignedMembers = computed(() => {
  if (!activeMilestone.value || milestoneTasks.value.length === 0) return []
  const memberMap = new Map<string, { userId: string; name: string; taskCount: number; completedCount: number }>()
  
  for (const t of milestoneTasks.value) {
    if (t.assigneeId && t.assigneeName) {
      const existing = memberMap.get(t.assigneeId) || { userId: t.assigneeId, name: t.assigneeName, taskCount: 0, completedCount: 0 }
      existing.taskCount++
      if (t.status === 'Done') existing.completedCount++
      memberMap.set(t.assigneeId, existing)
    }
  }
  return Array.from(memberMap.values())
})

const trackFillPercentage = computed(() => {
  if (sprints.value.length <= 1) return overallProgress.value
  const currentIndex = sprints.value.findIndex(s => isCurrentMilestone(s))
  if (currentIndex === -1) {
    const allDone = sprints.value.every(s => isCompletedMilestone(s))
    return allDone ? 100 : 0
  }
  return Math.round(((currentIndex + 0.5) / sprints.value.length) * 100)
})

// Action: Jump to Kanban Board for selected milestone
function jumpToKanban(sprintId?: string) {
  const targetId = sprintId || selectedSprintId.value
  if (!targetId) return
  activeProjectTab.value = 'tasks'
}

// Action: Quick change milestone status
async function quickChangeMilestoneStatus(sprint: SprintDto, newStatus: string) {
  try {
    await apiCommand(`/api/sprints/${sprint.id}`, {
      method: 'PATCH',
      body: JSON.stringify({
        name: sprint.name,
        startDate: sprint.startDate,
        endDate: sprint.endDate,
        status: newStatus,
        goal: sprint.goal
      })
    })
    await loadSprints()
    showSuccess(`Đã chuyển mốc "${sprint.name}" sang trạng thái "${newStatus}".`)
  } catch (error) {
    showError('Không thể cập nhật trạng thái mốc.')
  }
}

// Action: Sign-off / Complete Milestone
async function markMilestoneCompleted(sprint: SprintDto) {
  if (!confirm(`Bạn có chắc chắn muốn Nghiệm thu Hoàn thành mốc "${sprint.name}"?`)) return
  await quickChangeMilestoneStatus(sprint, 'Completed')
}

// Action: Generate Roadmap Preset (Scrum, Outsource, Waterfall)
async function handleGeneratePreset(presetType: 'scrum' | 'outsource' | 'waterfall') {
  isGeneratingPreset.value = true
  try {
    await apiCommand(`/api/projects/${props.projectId}/sprints/presets`, {
      method: 'POST',
      body: JSON.stringify({ presetType })
    })
    showPresetModal.value = false
    await loadSprints()
    showSuccess(`Đã tự động khởi tạo Mẫu Lộ trình (${presetType.toUpperCase()}) thành công!`)
  } catch (error) {
    showError('Không thể khởi tạo mẫu mốc tiến độ.')
  } finally {
    isGeneratingPreset.value = false
  }
}

// Action: Create manual milestone
async function handleCreateMilestone() {
  if (!milestoneName.value.trim() || !milestoneStartDate.value || !milestoneEndDate.value) {
    showError('Vui lòng nhập đầy đủ Tên mốc, Ngày bắt đầu và Ngày kết thúc.')
    return
  }

  try {
    await apiCommand(`/api/projects/${props.projectId}/sprints`, {
      method: 'POST',
      body: JSON.stringify({
        name: milestoneName.value.trim(),
        startDate: new Date(milestoneStartDate.value).toISOString(),
        endDate: new Date(milestoneEndDate.value).toISOString(),
        goal: milestoneGoal.value.trim() || null
      })
    })

    showCreateModal.value = false
    resetMilestoneForm()
    await loadSprints()
    showSuccess('Đã thêm mốc tiến độ mới.')
  } catch (error) {
    showError('Không thể tạo mốc tiến độ.')
  }
}

// Action: Edit milestone
function openEdit(sprint: SprintDto) {
  editingSprintId.value = sprint.id
  milestoneName.value = sprint.name
  milestoneStartDate.value = sprint.startDate.slice(0, 10)
  milestoneEndDate.value = sprint.endDate.slice(0, 10)
  milestoneGoal.value = sprint.goal || ''
  milestoneStatus.value = sprint.status || 'Planning'
  showEditModal.value = true
}

async function handleUpdateMilestone() {
  if (!editingSprintId.value || !milestoneName.value.trim()) return
  try {
    await apiCommand(`/api/sprints/${editingSprintId.value}`, {
      method: 'PATCH',
      body: JSON.stringify({
        name: milestoneName.value.trim(),
        startDate: new Date(milestoneStartDate.value).toISOString(),
        endDate: new Date(milestoneEndDate.value).toISOString(),
        status: milestoneStatus.value,
        goal: milestoneGoal.value.trim() || null
      })
    })

    showEditModal.value = false
    editingSprintId.value = null
    resetMilestoneForm()
    await loadSprints()
    showSuccess('Đã cập nhật thông tin mốc tiến độ.')
  } catch (error) {
    showError('Không thể cập nhật mốc tiến độ.')
  }
}

// Action: Delete milestone
async function handleDeleteMilestone(sprintId: string) {
  if (!confirm('Bạn có chắc chắn muốn xóa mốc này? Các task liên kết sẽ không bị xóa.')) return
  try {
    await apiCommand(`/api/sprints/${sprintId}`, { method: 'DELETE' })
    if (selectedSprintId.value === sprintId) selectedSprintId.value = null
    await loadSprints()
    showSuccess('Đã xóa mốc tiến độ.')
  } catch (error) {
    showError('Không thể xóa mốc tiến độ.')
  }
}

// Action: Open Task Assignment Modal
function openTaskAssignModal() {
  if (!selectedSprintId.value) return
  selectedTaskIdsForSprint.value = milestoneTasks.value.map(t => t.id)
  showTaskAssignModal.value = true
}

function toggleTaskSelection(taskId: string) {
  const index = selectedTaskIdsForSprint.value.indexOf(taskId)
  if (index >= 0) {
    selectedTaskIdsForSprint.value.splice(index, 1)
  } else {
    selectedTaskIdsForSprint.value.push(taskId)
  }
}

async function handleSaveTaskAssignments() {
  if (!selectedSprintId.value) return
  isSavingTaskAssignment.value = true
  try {
    await apiCommand(`/api/sprints/${selectedSprintId.value}/tasks`, {
      method: 'PUT',
      body: JSON.stringify({ taskIds: selectedTaskIdsForSprint.value })
    })
    showTaskAssignModal.value = false
    await loadDashboard()
    await loadSprints()
    showSuccess('Đã cập nhật danh sách công việc thuộc mốc.')
  } catch (error) {
    showError('Không thể cập nhật phân công công việc vào mốc.')
  } finally {
    isSavingTaskAssignment.value = false
  }
}

// Action: Quick Create Task inside selected milestone
async function handleQuickCreateTask() {
  if (!selectedSprintId.value || !quickTaskTitle.value.trim()) {
    showError('Vui lòng nhập tiêu đề nhiệm vụ.')
    return
  }

  try {
    await apiCommand('/api/tasks', {
      method: 'POST',
      body: JSON.stringify({
        title: quickTaskTitle.value.trim(),
        priority: quickTaskPriority.value,
        projectId: props.projectId,
        assigneeId: quickTaskAssigneeId.value || null,
        dueDate: quickTaskDueDate.value ? new Date(quickTaskDueDate.value).toISOString() : null,
        sprintId: selectedSprintId.value
      })
    })

    showQuickCreateTaskModal.value = false
    quickTaskTitle.value = ''
    quickTaskPriority.value = 'Medium'
    quickTaskAssigneeId.value = ''
    quickTaskDueDate.value = ''

    await loadDashboard()
    await loadSprints()
    showSuccess('Đã tạo nhiệm vụ mới trực tiếp trong mốc này.')
  } catch (error) {
    showError('Không thể tạo nhiệm vụ.')
  }
}

// Action: Inline Update Task Status (For members)
async function updateTaskStatusInline(task: DashboardTask, newStatus: string) {
  if (task.status === newStatus) return
  try {
    await apiCommand(`/api/tasks/${task.id}/status`, {
      method: 'PATCH',
      body: JSON.stringify({
        status: newStatus,
        rowVersion: task.rowVersion
      })
    })
    await loadDashboard()
    await loadSprints()
    showSuccess(`Đã chuyển trạng thái task sang "${newStatus}".`)
  } catch (error) {
    showError('Không thể cập nhật trạng thái nhiệm vụ.')
  }
}

function resetMilestoneForm() {
  milestoneName.value = ''
  milestoneStartDate.value = ''
  milestoneEndDate.value = ''
  milestoneGoal.value = ''
  milestoneStatus.value = 'Planning'
  editingSprintId.value = null
}

function formatDateRange(start: string, end: string) {
  if (!start || !end) return ''
  const d1 = new Date(start)
  const d2 = new Date(end)
  return `${d1.getDate()}/${d1.getMonth() + 1} - ${d2.getDate()}/${d2.getMonth() + 1}/${d2.getFullYear()}`
}

function getDaysRemaining(endDateStr: string): { text: string; isOverdue: boolean } {
  if (!endDateStr) return { text: '', isOverdue: false }
  const now = new Date().getTime()
  const end = new Date(endDateStr).getTime()
  const diffDays = Math.ceil((end - now) / (1000 * 60 * 60 * 24))
  if (diffDays < 0) return { text: `Trễ ${Math.abs(diffDays)} ngày`, isOverdue: true }
  if (diffDays === 0) return { text: 'Hạn chót hôm nay', isOverdue: false }
  return { text: `Còn ${diffDays} ngày`, isOverdue: false }
}
</script>

<template>
  <div class="project-roadmap-shell">
    
    <!-- Role & Mode Indicator Banner -->
    <div class="role-mode-bar glass-card">
      <div class="role-badge-box">
        <span v-if="isProjectAdmin" class="role-chip chip-admin">
          <ShieldCheck :size="15" /> Quyền Quản Lý (Project Leader)
        </span>
        <span v-else class="role-chip chip-member">
          <UserCheck :size="15" /> Giao diện Theo dõi Tiến độ (Thành viên & Stakeholders)
        </span>
        <span class="mode-text ms-2">
          {{ isClientViewMode ? '👀 Đang ở chế độ xem Khách hàng / Stakeholder' : '⚙️ Đang ở chế độ xem Quản trị (Đầy đủ cấu hình & thao tác)' }}
        </span>
      </div>

      <div class="view-mode-toggle">
        <button
          type="button"
          class="toggle-btn"
          :class="{ 'is-active': !isClientViewMode }"
          @click="isClientViewMode = false"
        >
          <LayoutGrid :size="14" />
          <span>Quản trị</span>
        </button>
        <button
          type="button"
          class="toggle-btn"
          :class="{ 'is-active': isClientViewMode }"
          @click="isClientViewMode = true"
        >
          <Eye :size="14" />
          <span>Khách hàng</span>
        </button>
      </div>
    </div>

    <!-- Top Executive Overview Header -->
    <div class="roadmap-header glass-card">
      <div class="roadmap-header__title">
        <div class="title-with-icon">
          <div class="icon-glow-box">
            <Compass :size="26" class="text-primary" />
          </div>
          <div>
            <h3>Lộ Trình Dự Án & Mốc Tiến Độ</h3>
            <p class="text-muted text-sm">
              Theo dõi tiến độ nghiệm thu giai đoạn, quản lý mốc bàn giao và kiểm soát rủi ro dự án.
            </p>
          </div>
        </div>
      </div>

      <div class="roadmap-header__actions">
        <button
          v-if="isProjectAdmin && !isClientViewMode"
          type="button"
          class="primary-button btn-outsource-preset"
          @click="showPresetModal = true"
        >
          <Sparkles :size="16" />
          <span>Khởi Tạo Mẫu Lộ Trình</span>
        </button>

        <button
          v-if="isProjectAdmin && !isClientViewMode"
          type="button"
          class="secondary-button"
          @click="showCreateModal = true"
        >
          <Plus :size="16" />
          <span>Thêm mốc mới</span>
        </button>
      </div>
    </div>

    <!-- Empty State if no milestones exist -->
    <div v-if="sprints.length === 0 && !isLoading" class="empty-roadmap-card glass-card">
      <div class="empty-roadmap-content">
        <Layers :size="52" class="text-primary opacity-60 mb-3" />
        <h4>Chưa có mốc tiến độ nào được thiết lập</h4>
        <p>
          Dự án này chưa có mốc tiến độ nào. Bạn có thể sử dụng các <strong>Mẫu Quy trình Chuẩn (Scrum, Outsource, Waterfall)</strong>
          hoặc tự thêm các mốc quan trọng để theo dõi tiến độ bàn giao.
        </p>

        <div class="empty-actions mt-4" v-if="isProjectAdmin">
          <button
            type="button"
            class="primary-button primary-button--lg"
            @click="showPresetModal = true"
          >
            <Sparkles :size="18" />
            <span>Chọn Bộ Mẫu Lộ Trình (Scrum / Outsource / Waterfall)</span>
          </button>

          <button type="button" class="secondary-button secondary-button--lg ms-3" @click="showCreateModal = true">
            <Plus :size="18" />
            <span>Tự nhập mốc thủ công</span>
          </button>
        </div>
        <p v-else class="text-muted text-sm mt-3">
          Vui lòng liên hệ Người quản lý dự án (Project Leader) để thiết lập lộ trình dự án.
        </p>
      </div>
    </div>

    <!-- MAIN ROADMAP TIMELINE TRACK -->
    <template v-else>
      <div class="roadmap-track-card glass-card">
        <!-- Client Executive Executive Bar & Filters -->
        <div class="roadmap-metrics-bar">
          <div class="metric-pill">
            <span class="metric-label">Trạng thái Sức khỏe Dự án</span>
            <span :class="`badge-tag tag-${projectHealthStatus.tone}`" class="health-tag">
              <Zap :size="13" /> {{ projectHealthStatus.label }}
            </span>
          </div>

          <div class="metric-pill">
            <span class="metric-label">Mốc Tập trung Hiện tại</span>
            <strong class="metric-value text-primary">
              {{ currentPositionMilestone ? currentPositionMilestone.name : 'Đã hoàn thành tất cả mốc' }}
            </strong>
          </div>

          <div class="metric-pill">
            <span class="metric-label">Tiến độ Nghiệm thu Tổng thể</span>
            <div class="metric-progress-wrap">
              <div class="mini-progress-rail">
                <div class="mini-progress-fill" :style="{ width: `${overallProgress}%` }"></div>
              </div>
              <strong class="metric-value text-success ms-2">{{ overallProgress }}%</strong>
            </div>
          </div>

          <div class="metric-pill">
            <span class="metric-label">Tổng quy mô Mốc</span>
            <strong class="metric-value">{{ sprints.length }} Giai đoạn</strong>
          </div>

          <!-- Filter Pills -->
          <div class="milestone-filter-group">
            <button
              type="button"
              class="filter-pill"
              :class="{ 'active': milestoneFilter === 'all' }"
              @click="milestoneFilter = 'all'"
            >
              Tất cả ({{ sprints.length }})
            </button>
            <button
              type="button"
              class="filter-pill"
              :class="{ 'active': milestoneFilter === 'active' }"
              @click="milestoneFilter = 'active'"
            >
              ⚡ Đang chạy
            </button>
            <button
              type="button"
              class="filter-pill"
              :class="{ 'active': milestoneFilter === 'overdue' }"
              @click="milestoneFilter = 'overdue'"
            >
              ⚠️ Trễ hạn
            </button>
            <button
              type="button"
              class="filter-pill"
              :class="{ 'active': milestoneFilter === 'completed' }"
              @click="milestoneFilter = 'completed'"
            >
              ✓ Đã xong
            </button>
          </div>
        </div>

        <!-- Horizontal Connected Milestone Journey Track -->
        <div class="roadmap-scroll-wrapper no-scrollbar">
          <div class="roadmap-visual-container">
            <!-- Connecting Line -->
            <div class="connecting-line">
              <div class="connecting-line-fill" :style="{ width: `${trackFillPercentage}%` }"></div>
            </div>

            <!-- Milestone Nodes -->
            <div class="milestones-nodes-row">
              <div
                v-for="(sprint, index) in filteredSprints"
                :key="sprint.id"
                class="milestone-node"
                :class="{
                  'is-completed': isCompletedMilestone(sprint),
                  'is-current': isCurrentMilestone(sprint),
                  'is-overdue': isOverdueMilestone(sprint),
                  'is-selected': selectedSprintId === sprint.id,
                }"
                @click="selectedSprintId = sprint.id"
              >
                <!-- Node Icon & Badge -->
                <div class="node-circle">
                  <div v-if="isCurrentMilestone(sprint)" class="current-location-flag">
                    <Navigation :size="12" />
                    <span>VỊ TRÍ HIỆN TẠI</span>
                  </div>

                  <CheckCircle2 v-if="isCompletedMilestone(sprint)" :size="24" class="text-success" />
                  <span v-else class="node-number">{{ index + 1 }}</span>
                </div>

                <!-- Milestone Summary Card -->
                <div class="milestone-node-card">
                  <div class="node-status-badge">
                    <span v-if="isCompletedMilestone(sprint)" class="badge-tag tag-success">✓ Đã nghiệm thu</span>
                    <span v-else-if="isCurrentMilestone(sprint)" class="badge-tag tag-primary">⚡ Đang làm</span>
                    <span v-else-if="isOverdueMilestone(sprint)" class="badge-tag tag-danger">⚠️ Trễ hạn</span>
                    <span v-else class="badge-tag tag-muted">📅 Chưa tới</span>
                  </div>

                  <h5 class="node-title">{{ sprint.name }}</h5>
                  <div class="node-dates">
                    <Calendar :size="12" />
                    <span>{{ formatDateRange(sprint.startDate, sprint.endDate) }}</span>
                  </div>

                  <div class="node-days-info" :class="{ 'is-overdue': getDaysRemaining(sprint.endDate).isOverdue }">
                    <Clock :size="11" />
                    <span>{{ getDaysRemaining(sprint.endDate).text }}</span>
                  </div>

                  <div class="node-progress-rail">
                    <div
                      class="node-progress-fill"
                      :class="{ 'bg-success': isCompletedMilestone(sprint), 'bg-primary': isCurrentMilestone(sprint) }"
                      :style="{ width: `${sprint.progress}%` }"
                    ></div>
                  </div>
                  <div class="node-task-count">
                    <strong>{{ sprint.completedTaskCount }}/{{ sprint.taskCount }}</strong> tasks ({{ sprint.progress }}%)
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- SELECTED MILESTONE DETAIL CONTROL PANEL -->
      <div
        v-if="activeMilestone"
        :id="`milestone-${activeMilestone.id}`"
        class="milestone-detail-panel glass-card"
      >
        <!-- Fast Access Command Toolbar -->
        <div class="fast-access-toolbar mb-4">
          <div class="toolbar-left">
            <span class="toolbar-title">⚡ Thao Tác Nhanh Cho Mốc "{{ activeMilestone.name }}"</span>
          </div>

          <div class="toolbar-right">
            <!-- Quick Add Task -->
            <button
              v-if="!isClientViewMode"
              type="button"
              class="toolbar-btn btn-primary-gradient"
              @click="showQuickCreateTaskModal = true"
            >
              <Plus :size="14" />
              <span>+ Tạo Task Mốc Này</span>
            </button>

            <!-- One-click Sign-off / Complete Milestone -->
            <button
              v-if="isProjectAdmin && !isClientViewMode && !isCompletedMilestone(activeMilestone)"
              type="button"
              class="toolbar-btn btn-success-light"
              @click="markMilestoneCompleted(activeMilestone)"
              title="Đánh dấu nghiệm thu hoàn thành mốc này"
            >
              <CheckCircle2 :size="14" />
              <span>Nghiệm Thu Mốc</span>
            </button>

            <!-- Quick Status Change dropdown -->
            <div v-if="isProjectAdmin && !isClientViewMode" class="quick-status-dropdown-wrap">
              <select
                class="quick-status-select"
                :value="activeMilestone.status || 'Planning'"
                @change="quickChangeMilestoneStatus(activeMilestone, ($event.target as HTMLSelectElement).value)"
              >
                <option value="Planning">Trạng thái: Planning</option>
                <option value="Active">Trạng thái: Active (⚡)</option>
                <option value="Completed">Trạng thái: Completed (✓)</option>
                <option value="Paused">Trạng thái: Paused</option>
              </select>
            </div>

            <!-- Task Assignment -->
            <button
              v-if="isProjectAdmin && !isClientViewMode"
              type="button"
              class="toolbar-btn btn-outline"
              @click="openTaskAssignModal"
            >
              <LinkIcon :size="14" />
              <span>Gán/Gỡ Task ({{ milestoneTasks.length }})</span>
            </button>

            <!-- Kanban Jump -->
            <button type="button" class="toolbar-btn btn-kanban" @click="jumpToKanban(activeMilestone.id)">
              <FolderKanban :size="14" />
              <span>Mở Bảng Kanban ➔</span>
            </button>
          </div>
        </div>

        <!-- Detail Header -->
        <div class="detail-header">
          <div class="detail-header-left">
            <div class="d-flex align-items-center gap-2 mb-1">
              <span class="badge-tag tag-primary">Mốc Đang Chọn</span>
              <span v-if="isCompletedMilestone(activeMilestone)" class="badge-tag tag-success">✓ Nghiệm thu xong</span>
              <span v-else-if="isOverdueMilestone(activeMilestone)" class="badge-tag tag-danger">⚠️ Trễ hạn</span>
              <span v-else class="badge-tag tag-primary">⚡ Đang triển khai</span>
            </div>

            <h4>{{ activeMilestone.name }}</h4>
            <p v-if="activeMilestone.goal" class="detail-goal">
              🎯 <strong>Mục tiêu & Hạng mục nghiệm thu:</strong> {{ activeMilestone.goal }}
            </p>

            <div class="detail-dates-info">
              <Clock :size="14" />
              <span>Thời gian thực hiện: <strong>{{ formatDateRange(activeMilestone.startDate, activeMilestone.endDate) }}</strong></span>
              <span class="ms-3 badge-tag tag-muted">{{ getDaysRemaining(activeMilestone.endDate).text }}</span>
            </div>
          </div>

          <div class="detail-header-actions">
            <!-- Manager actions -->
            <template v-if="isProjectAdmin && !isClientViewMode">
              <button type="button" class="btn btn-outline" @click="openEdit(activeMilestone)" title="Chỉnh sửa tên, deadline, goal">
                <Edit3 :size="15" />
                <span>Sửa mốc</span>
              </button>
              <button type="button" class="btn btn-danger-ghost" @click="handleDeleteMilestone(activeMilestone.id)" title="Xóa mốc">
                <Trash2 :size="15" />
              </button>
            </template>
          </div>
        </div>

        <!-- Milestone Key Deliverables Checklist (Calculated from Goal & Tasks) -->
        <div class="milestone-deliverables-box mt-3">
          <div class="deliverables-header">
            <Target :size="16" class="text-primary" />
            <strong>Tiêu Chí & Hạng Mục Nghiệm Thu (Key Deliverables)</strong>
            <span class="deliverable-badge">{{ activeMilestone.completedTaskCount }}/{{ activeMilestone.taskCount }} Đã Đạt</span>
          </div>

          <div class="deliverables-grid">
            <div class="deliverable-item" :class="{ 'is-done': activeMilestone.progress >= 100 }">
              <CheckSquare v-if="activeMilestone.progress >= 100" :size="16" class="text-success" />
              <Clock v-else :size="16" class="text-muted" />
              <span>Hoàn thành 100% nhiệm vụ trong mốc ({{ activeMilestone.completedTaskCount }}/{{ activeMilestone.taskCount }} tasks)</span>
            </div>

            <div class="deliverable-item" :class="{ 'is-done': !isOverdueMilestone(activeMilestone) }">
              <CheckSquare v-if="!isOverdueMilestone(activeMilestone)" :size="16" class="text-success" />
              <AlertTriangle v-else :size="16" class="text-danger" />
              <span>Đảm bảo đúng mốc thời hạn deadline: {{ formatDateRange(activeMilestone.startDate, activeMilestone.endDate) }}</span>
            </div>

            <div v-if="activeMilestone.goal" class="deliverable-item is-done">
              <CheckSquare :size="16" class="text-primary" />
              <span>Mục tiêu cam kết: {{ activeMilestone.goal }}</span>
            </div>
          </div>
        </div>

        <!-- AI Milestone Summary & Risk Card -->
        <ProjectProgressAiCard
          class="sprint-ai-summary"
          :project-id="projectId"
          :sprint-id="activeMilestone.id"
          :sprint-name="activeMilestone.name"
          :can-generate="canGenerateAi"
        />

        <!-- Team Workload Distribution in Milestone -->
        <div v-if="milestoneAssignedMembers.length > 0" class="milestone-members-bar mt-4">
          <span class="section-label mb-2"><Users :size="14" /> Nhân sự phụ trách mốc này ({{ milestoneAssignedMembers.length }} thành viên):</span>
          <div class="members-chips-row">
            <div v-for="m in milestoneAssignedMembers" :key="m.userId" class="member-chip">
              <User :size="13" />
              <span class="member-name">{{ m.name }}</span>
              <span class="member-task-badge">{{ m.completedCount }}/{{ m.taskCount }} tasks</span>
            </div>
          </div>
        </div>

        <!-- Milestone Task List Section -->
        <div class="detail-body mt-4">
          <div class="tasks-section-header mb-3">
            <h5>
              <ListTodo :size="18" class="text-primary me-1" />
              Danh sách công việc thuộc mốc này ({{ milestoneTasks.length }})
            </h5>

            <div class="tasks-filter-tools">
              <div class="search-box-wrap">
                <Search :size="14" class="search-icon" />
                <input
                  v-model="milestoneTaskSearch"
                  type="text"
                  placeholder="Lọc task mốc..."
                  class="task-search-input"
                />
              </div>
              <select v-model="milestoneTaskStatusFilter" class="task-status-filter">
                <option value="all">Tất cả trạng thái</option>
                <option value="todo">Cần làm (Todo)</option>
                <option value="inprogress">Đang làm (InProgress)</option>
                <option value="inreview">Đánh giá (InReview)</option>
                <option value="done">Hoàn thành (Done)</option>
              </select>
            </div>
          </div>

          <!-- Empty Tasks Box -->
          <div v-if="filteredMilestoneTasks.length === 0" class="empty-tasks-box">
            <p v-if="milestoneTasks.length === 0">
              Chưa có task nào được gán vào mốc này. 
              <span v-if="isProjectAdmin">Bạn có thể bấm <strong>"+ Tạo Task Mốc Này"</strong> hoặc <strong>"Gán Nhiệm Vụ"</strong> trên thanh công cụ để bắt đầu.</span>
            </p>
            <p v-else>Không tìm thấy nhiệm vụ phù hợp với bộ lọc.</p>
          </div>

          <!-- Milestone Task Grid -->
          <div v-else class="milestone-tasks-grid">
            <div
              v-for="task in filteredMilestoneTasks"
              :key="task.id"
              class="milestone-task-item"
              :class="`status-${task.status.toLowerCase()}`"
            >
              <div class="task-item-header">
                <span class="task-number">{{ task.key || `#${task.number}` }}</span>
                <span :class="`priority priority--${task.priority.toLowerCase()}`">{{ task.priority }}</span>
              </div>

              <div class="task-item-title">{{ task.title }}</div>

              <div class="task-item-footer">
                <span v-if="task.assigneeName" class="task-item-assignee">
                  👤 {{ task.assigneeName }}
                </span>
                <span v-else class="task-item-assignee text-muted">
                  👤 Chưa giao
                </span>

                <!-- Inline Status Selector (Allows Members to Update Task Status) -->
                <select
                  class="task-inline-status-select"
                  :value="task.status"
                  @change="updateTaskStatusInline(task, ($event.target as HTMLSelectElement).value)"
                >
                  <option value="Todo">Todo</option>
                  <option value="InProgress">InProgress</option>
                  <option value="InReview">InReview</option>
                  <option value="Done">Done</option>
                </select>
              </div>
            </div>
          </div>
        </div>
      </div>
    </template>

    <!-- MODAL: Select Preset Template (Scrum, Outsource, Waterfall) -->
    <Teleport to="body">
      <div v-if="showPresetModal" class="modal-backdrop" @click.self="showPresetModal = false">
        <div class="preset-modal glass-card">
          <div class="modal-header">
            <h4><Sparkles :size="18" class="text-primary me-2" /> Khởi Tạo Mẫu Lộ Trình Dự Án</h4>
            <button type="button" class="icon-button" @click="showPresetModal = false"><X :size="18" /></button>
          </div>

          <div class="preset-modal-body">
            <p class="text-muted text-sm mb-4">
              Chọn phương pháp quản trị dự án phù hợp để hệ thống tự động khởi tạo lộ trình mốc tiến độ tối ưu:
            </p>

            <div class="preset-options-grid">
              <div class="preset-option-card" @click="handleGeneratePreset('outsource')">
                <div class="preset-card-header">
                  <Award :size="24" class="text-primary" />
                  <h5>Mẫu Quy Trình Outsource (6 Mốc)</h5>
                </div>
                <p class="preset-desc">
                  Phù hợp dự án phần mềm cho khách hàng. Chia rõ mốc Scope ➔ Prototype ➔ Core Dev ➔ AI Integration ➔ UAT ➔ Go-Live.
                </p>
                <button type="button" class="btn btn-outline-primary w-100" :disabled="isGeneratingPreset">
                  Tạo Mẫu Outsource
                </button>
              </div>

              <div class="preset-option-card" @click="handleGeneratePreset('scrum')">
                <div class="preset-card-header">
                  <RefreshCw :size="24" class="text-success" />
                  <h5>Mẫu Scrum / Agile (Sprint 1...4)</h5>
                </div>
                <p class="preset-desc">
                  Phù hợp quản trị Agile. Chia lộ trình thành các Sprint 2 tuần song song với tiêu chí bàn giao liên tục.
                </p>
                <button type="button" class="btn btn-outline-success w-100" :disabled="isGeneratingPreset">
                  Tạo Mẫu Scrum (4 Sprints)
                </button>
              </div>

              <div class="preset-option-card" @click="handleGeneratePreset('waterfall')">
                <div class="preset-card-header">
                  <Layers :size="24" class="text-warning" />
                  <h5>Mẫu Waterfall / Truyền thống (4 Pha)</h5>
                </div>
                <p class="preset-desc">
                  Phù hợp dự án yêu cầu quy trình tuyến tính: Khảo sát ➔ Thiết kế ➔ Phát triển ➔ Bàn giao.
                </p>
                <button type="button" class="btn btn-outline-warning w-100" :disabled="isGeneratingPreset">
                  Tạo Mẫu Waterfall
                </button>
              </div>
            </div>
          </div>
        </div>
      </div>
    </Teleport>

    <!-- MODAL: Create / Edit Milestone -->
    <Teleport to="body">
      <div v-if="showCreateModal || showEditModal" class="modal-backdrop" @click.self="showCreateModal = showEditModal = false">
        <div class="milestone-modal glass-card">
          <div class="modal-header">
            <h4>{{ showEditModal ? 'Chỉnh sửa Mốc Tiến Độ' : 'Thêm Mốc Tiến Độ Mới' }}</h4>
            <button type="button" class="icon-button" @click="showCreateModal = showEditModal = false"><X :size="18" /></button>
          </div>

          <form class="modal-body" @submit.prevent="showEditModal ? handleUpdateMilestone() : handleCreateMilestone()">
            <div class="form-group">
              <label>Tên mốc / Giai đoạn *</label>
              <input v-model="milestoneName" type="text" placeholder="Ví dụ: Mốc 1: Scope Alignment & Prototype UI..." required class="modal-input" />
            </div>

            <div class="form-row">
              <div class="form-group">
                <label>Ngày bắt đầu *</label>
                <input v-model="milestoneStartDate" type="date" required class="modal-input" />
              </div>
              <div class="form-group">
                <label>Ngày kết thúc *</label>
                <input v-model="milestoneEndDate" type="date" required class="modal-input" />
              </div>
            </div>

            <div v-if="showEditModal" class="form-group">
              <label>Trạng thái mốc</label>
              <select v-model="milestoneStatus" class="modal-input">
                <option value="Planning">Planning (Lên kế hoạch)</option>
                <option value="Active">Active (Đang thực hiện)</option>
                <option value="Completed">Completed (Hoàn thành)</option>
                <option value="Paused">Paused (Tạm dừng)</option>
              </select>
            </div>

            <div class="form-group">
              <label>Mục tiêu nghiệm thu của mốc (Goal Statement)</label>
              <textarea v-model="milestoneGoal" rows="3" placeholder="Mô tả cụ thể tiêu chí để nghiệm thu hoàn thành mốc này..." class="modal-input"></textarea>
            </div>

            <div class="modal-actions mt-4">
              <button type="button" class="btn btn--ghost" @click="showCreateModal = showEditModal = false">Hủy</button>
              <button type="submit" class="btn btn--primary">
                {{ showEditModal ? 'Cập nhật Mốc' : 'Tạo Mốc Mới' }}
              </button>
            </div>
          </form>
        </div>
      </div>
    </Teleport>

    <!-- MODAL: Assign / Unassign Tasks to Milestone -->
    <Teleport to="body">
      <div v-if="showTaskAssignModal" class="modal-backdrop" @click.self="showTaskAssignModal = false">
        <div class="task-assign-modal glass-card">
          <div class="modal-header">
            <h4><LinkIcon :size="18" class="text-primary me-2" /> Gán Công Việc Vào Mốc: {{ activeMilestone?.name }}</h4>
            <button type="button" class="icon-button" @click="showTaskAssignModal = false"><X :size="18" /></button>
          </div>

          <div class="modal-body">
            <p class="text-muted text-sm mb-3">
              Tích chọn các công việc trong dự án thuộc về mốc tiến độ này. Tiến độ mốc sẽ tự động tính dựa trên các task được gán.
            </p>

            <div v-if="allProjectTasks.length === 0" class="empty-tasks-box">
              <p>Dự án chưa có task nào. Hãy tạo task trước trên bảng Kanban.</p>
            </div>

            <div v-else class="assign-tasks-list no-scrollbar">
              <label
                v-for="task in allProjectTasks"
                :key="task.id"
                class="assign-task-row"
                :class="{ 'is-selected': selectedTaskIdsForSprint.includes(task.id) }"
              >
                <input
                  type="checkbox"
                  :checked="selectedTaskIdsForSprint.includes(task.id)"
                  @change="toggleTaskSelection(task.id)"
                />
                <div class="task-info">
                  <strong>{{ task.key || `#${task.number}` }} — {{ task.title }}</strong>
                  <small class="text-muted ms-2">({{ task.status }} • {{ task.assigneeName || 'Chưa giao' }})</small>
                </div>
              </label>
            </div>

            <div class="modal-actions mt-4">
              <button type="button" class="btn btn--ghost" @click="showTaskAssignModal = false">Hủy</button>
              <button
                type="button"
                class="btn btn--primary"
                :disabled="isSavingTaskAssignment"
                @click="handleSaveTaskAssignments"
              >
                {{ isSavingTaskAssignment ? 'Đang lưu...' : 'Lưu Phân Công Mốc' }}
              </button>
            </div>
          </div>
        </div>
      </div>
    </Teleport>

    <!-- MODAL: Quick Create Task inside Milestone -->
    <Teleport to="body">
      <div v-if="showQuickCreateTaskModal" class="modal-backdrop" @click.self="showQuickCreateTaskModal = false">
        <div class="milestone-modal glass-card">
          <div class="modal-header">
            <h4><Plus :size="18" class="text-primary me-2" /> Tạo Nhiệm Vụ Mới Thuộc Mốc: {{ activeMilestone?.name }}</h4>
            <button type="button" class="icon-button" @click="showQuickCreateTaskModal = false"><X :size="18" /></button>
          </div>

          <form class="modal-body" @submit.prevent="handleQuickCreateTask">
            <div class="form-group">
              <label>Tiêu đề nhiệm vụ *</label>
              <input v-model="quickTaskTitle" type="text" placeholder="Nhập tiêu đề nhiệm vụ mới..." required class="modal-input" />
            </div>

            <div class="form-row">
              <div class="form-group">
                <label>Mức độ ưu tiên</label>
                <select v-model="quickTaskPriority" class="modal-input">
                  <option value="Low">Low</option>
                  <option value="Medium">Medium</option>
                  <option value="High">High</option>
                  <option value="Critical">Critical</option>
                </select>
              </div>

              <div class="form-group">
                <label>Hạn chót</label>
                <input v-model="quickTaskDueDate" type="date" class="modal-input" />
              </div>
            </div>

            <div class="form-group">
              <label>Người phụ trách</label>
              <select v-model="quickTaskAssigneeId" class="modal-input">
                <option value="">Chưa giao</option>
                <option v-for="user in (selectedProject?.members || [])" :key="user.userId" :value="user.userId">
                  {{ user.fullName }}
                </option>
              </select>
            </div>

            <div class="modal-actions mt-4">
              <button type="button" class="btn btn--ghost" @click="showQuickCreateTaskModal = false">Hủy</button>
              <button type="submit" class="btn btn--primary">
                Tạo Task Mốc Này
              </button>
            </div>
          </form>
        </div>
      </div>
    </Teleport>

  </div>
</template>

<style scoped>
.project-roadmap-shell {
  display: flex;
  flex-direction: column;
  gap: 20px;
}

/* Role & Mode Banner */
.role-mode-bar {
  padding: 12px 20px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  border-radius: var(--radius-shell);
  background: var(--panel);
  border: 1px solid var(--line);
}

.role-badge-box {
  display: flex;
  align-items: center;
  gap: 8px;
}

.role-chip {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  font-size: 12px;
  font-weight: 700;
  padding: 4px 10px;
  border-radius: 20px;
}

.chip-admin {
  background: rgba(37, 99, 235, 0.15);
  color: var(--qaly-primary);
  border: 1px solid rgba(37, 99, 235, 0.3);
}

.chip-member {
  background: rgba(22, 163, 74, 0.15);
  color: var(--qaly-success);
  border: 1px solid rgba(22, 163, 74, 0.3);
}

.mode-text {
  font-size: 12px;
  color: var(--muted);
}

.view-mode-toggle {
  display: flex;
  background: var(--bg);
  padding: 3px;
  border-radius: 8px;
  border: 1px solid var(--line);
}

.toggle-btn {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 4px 12px;
  border: none;
  background: transparent;
  color: var(--muted);
  font-size: 12px;
  font-weight: 600;
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.2s ease;
}

.toggle-btn.is-active {
  background: var(--panel);
  color: var(--text);
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.15);
}

/* Header */
.roadmap-header {
  padding: 24px 32px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  border-radius: var(--radius-shell);
  background: var(--panel);
  border: 1px solid var(--line);
}

.title-with-icon {
  display: flex;
  align-items: center;
  gap: 16px;
}

.icon-glow-box {
  width: 48px;
  height: 48px;
  border-radius: 12px;
  background: rgba(37, 99, 235, 0.1);
  display: flex;
  align-items: center;
  justify-content: center;
  border: 1px solid rgba(37, 99, 235, 0.2);
}

.roadmap-header h3 {
  font-size: 18px;
  font-weight: 700;
  margin-bottom: 4px;
}

.roadmap-header__actions {
  display: flex;
  gap: 12px;
}

/* Empty Map */
.empty-roadmap-card {
  padding: 48px 32px;
  text-align: center;
  background: var(--panel);
  border: 1px dashed var(--line);
  border-radius: var(--radius-shell);
}

.empty-roadmap-content {
  max-width: 580px;
  margin: 0 auto;
}

/* Roadmap Track Card */
.roadmap-track-card {
  padding: 28px;
  background: var(--panel);
  border: 1px solid var(--line);
  border-radius: var(--radius-shell);
}

.roadmap-metrics-bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  margin-bottom: 24px;
  padding-bottom: 16px;
  border-bottom: 1px solid var(--line);
  flex-wrap: wrap;
}

.metric-pill {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.metric-label {
  font-size: 12px;
  color: var(--muted);
}

.metric-value {
  font-size: 15px;
  font-weight: 700;
}

.health-tag {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  font-size: 12px;
}

.metric-progress-wrap {
  display: flex;
  align-items: center;
}

.mini-progress-rail {
  width: 100px;
  height: 8px;
  background: var(--line);
  border-radius: 4px;
  overflow: hidden;
}

.mini-progress-fill {
  height: 100%;
  background: var(--qaly-success);
  border-radius: 4px;
}

.milestone-filter-group {
  display: flex;
  gap: 6px;
  background: var(--bg);
  padding: 3px;
  border-radius: 8px;
  border: 1px solid var(--line);
}

.filter-pill {
  padding: 4px 10px;
  font-size: 11px;
  font-weight: 600;
  border: none;
  background: transparent;
  color: var(--muted);
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.2s ease;
}

.filter-pill.active {
  background: var(--panel);
  color: var(--text-strong);
  box-shadow: 0 1px 4px rgba(0,0,0,0.1);
}

/* Scroll Container for Node Path */
.roadmap-scroll-wrapper {
  overflow-x: auto;
  padding: 30px 10px 20px 10px;
}

.roadmap-visual-container {
  position: relative;
  min-width: 850px;
}

.connecting-line {
  position: absolute;
  top: 26px;
  left: 60px;
  right: 60px;
  height: 6px;
  background: var(--line);
  border-radius: 3px;
  z-index: 1;
}

.connecting-line-fill {
  height: 100%;
  background: linear-gradient(90deg, var(--qaly-success), var(--qaly-primary), var(--qaly-ai));
  border-radius: 3px;
  transition: width 0.4s ease;
}

.milestones-nodes-row {
  display: flex;
  justify-content: space-between;
  position: relative;
  z-index: 2;
}

.milestone-node {
  display: flex;
  flex-direction: column;
  align-items: center;
  width: 185px;
  cursor: pointer;
  transition: transform 0.2s ease;
}

.milestone-node:hover {
  transform: translateY(-4px);
}

.node-circle {
  width: 52px;
  height: 52px;
  border-radius: 50%;
  background: var(--panel);
  border: 3px solid var(--line);
  display: flex;
  align-items: center;
  justify-content: center;
  font-weight: 700;
  position: relative;
  transition: all 0.3s ease;
}

.milestone-node.is-completed .node-circle {
  border-color: var(--qaly-success);
  background: rgba(22, 163, 74, 0.1);
}

.milestone-node.is-current .node-circle {
  border-color: var(--qaly-primary);
  background: rgba(37, 99, 235, 0.15);
  box-shadow: 0 0 20px rgba(37, 99, 235, 0.4);
  animation: pulseGlow 2s infinite alternate;
}

@keyframes pulseGlow {
  0% { transform: scale(1); box-shadow: 0 0 10px rgba(37, 99, 235, 0.3); }
  100% { transform: scale(1.08); box-shadow: 0 0 22px rgba(37, 99, 235, 0.8); }
}

.current-location-flag {
  position: absolute;
  top: -32px;
  background: linear-gradient(135deg, var(--qaly-primary), var(--qaly-ai));
  color: #fff;
  font-size: 10px;
  font-weight: 700;
  padding: 2px 8px;
  border-radius: 10px;
  white-space: nowrap;
  display: flex;
  align-items: center;
  gap: 4px;
  box-shadow: 0 4px 10px rgba(37, 99, 235, 0.3);
}

.current-location-flag::after {
  content: '';
  position: absolute;
  bottom: -4px;
  left: 50%;
  transform: translateX(-50%);
  border-width: 4px 4px 0;
  border-style: solid;
  border-color: var(--qaly-ai) transparent;
}

.milestone-node-card {
  background: var(--panel-soft);
  border: 1px solid var(--line);
  border-radius: 12px;
  padding: 14px;
  width: 100%;
  margin-top: 16px;
  text-align: center;
  transition: all 0.2s ease;
}

.milestone-node.is-selected .milestone-node-card {
  border-color: var(--qaly-primary);
  box-shadow: 0 4px 16px rgba(37, 99, 235, 0.2);
}

.node-title {
  font-size: 13px;
  font-weight: 700;
  margin: 6px 0;
  line-height: 1.3;
}

.node-dates {
  font-size: 11px;
  color: var(--muted);
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 4px;
  margin-bottom: 4px;
}

.node-days-info {
  font-size: 10px;
  font-weight: 600;
  color: var(--qaly-primary);
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 3px;
  margin-bottom: 8px;
}

.node-days-info.is-overdue {
  color: var(--qaly-danger);
}

.node-progress-rail {
  height: 5px;
  background: var(--line);
  border-radius: 3px;
  overflow: hidden;
  margin-bottom: 6px;
}

.node-progress-fill {
  height: 100%;
  border-radius: 3px;
}

.node-task-count {
  font-size: 11px;
  color: var(--muted);
}

.badge-tag {
  font-size: 10px;
  font-weight: 700;
  padding: 2px 6px;
  border-radius: 4px;
  display: inline-block;
}

.tag-success { background: rgba(22, 163, 74, 0.15); color: var(--qaly-success); }
.tag-primary { background: rgba(37, 99, 235, 0.15); color: var(--qaly-primary); }
.tag-danger { background: rgba(220, 38, 38, 0.15); color: var(--qaly-danger); }
.tag-muted { background: rgba(148, 163, 184, 0.15); color: var(--muted); }

/* Fast Access Toolbar */
.fast-access-toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 12px 16px;
  background: linear-gradient(90deg, rgba(37, 99, 235, 0.08), rgba(99, 102, 241, 0.04));
  border: 1px solid rgba(37, 99, 235, 0.2);
  border-radius: 12px;
  flex-wrap: wrap;
  gap: 12px;
}

.toolbar-title {
  font-size: 13px;
  font-weight: 700;
  color: var(--qaly-primary);
}

.toolbar-right {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}

.toolbar-btn {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 6px 12px;
  font-size: 12px;
  font-weight: 600;
  border-radius: 8px;
  border: 1px solid var(--line);
  background: var(--panel);
  color: var(--text);
  cursor: pointer;
  transition: all 0.2s ease;
}

.toolbar-btn:hover {
  transform: translateY(-1px);
}

.btn-primary-gradient {
  background: linear-gradient(135deg, var(--qaly-primary), var(--qaly-ai));
  color: #fff;
  border: none;
  box-shadow: 0 2px 8px rgba(37, 99, 235, 0.3);
}

.btn-success-light {
  background: rgba(22, 163, 74, 0.15);
  color: var(--qaly-success);
  border-color: rgba(22, 163, 74, 0.3);
}

.btn-kanban {
  background: var(--bg);
  border-color: var(--line);
}

.quick-status-select {
  padding: 6px 10px;
  font-size: 12px;
  font-weight: 600;
  background: var(--panel);
  border: 1px solid var(--line);
  border-radius: 8px;
  color: var(--text);
}

/* Detail Control Panel */
.milestone-detail-panel {
  padding: 24px;
  background: var(--panel);
  border: 1px solid var(--line);
  border-radius: var(--radius-shell);
}

.sprint-ai-summary {
  margin-top: 20px;
}

.detail-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  padding-bottom: 16px;
  border-bottom: 1px solid var(--line);
  flex-wrap: wrap;
  gap: 16px;
}

.detail-header h4 {
  font-size: 18px;
  font-weight: 700;
  margin: 4px 0;
}

.detail-goal {
  font-size: 13px;
  color: var(--text-strong);
  margin-bottom: 6px;
}

.detail-dates-info {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 12px;
  color: var(--muted);
}

.detail-header-actions {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}

/* Deliverables Box */
.milestone-deliverables-box {
  padding: 14px 18px;
  background: var(--panel-soft);
  border: 1px solid var(--line);
  border-radius: 12px;
}

.deliverables-header {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 13px;
  margin-bottom: 10px;
}

.deliverable-badge {
  font-size: 11px;
  font-weight: 700;
  padding: 2px 8px;
  border-radius: 12px;
  background: rgba(37, 99, 235, 0.12);
  color: var(--qaly-primary);
  margin-left: auto;
}

.deliverables-grid {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.deliverable-item {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 12px;
  color: var(--muted);
  padding: 6px 10px;
  background: var(--bg);
  border-radius: 6px;
  border: 1px solid var(--line);
}

.deliverable-item.is-done {
  color: var(--text-strong);
  font-weight: 500;
  border-color: rgba(22, 163, 74, 0.3);
}

.milestone-members-bar {
  padding: 12px 16px;
  background: var(--panel-soft);
  border: 1px solid var(--line);
  border-radius: 8px;
  font-size: 13px;
}

.section-label {
  display: flex;
  align-items: center;
  font-size: 12px;
  font-weight: 700;
  color: var(--muted);
}

.members-chips-row {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}

.member-chip {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 4px 10px;
  background: var(--bg);
  border: 1px solid var(--line);
  border-radius: 16px;
  font-size: 12px;
}

.member-task-badge {
  font-size: 10px;
  font-weight: 700;
  background: rgba(37, 99, 235, 0.15);
  color: var(--qaly-primary);
  padding: 1px 6px;
  border-radius: 10px;
}

/* Tasks Filter & Grid */
.tasks-section-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  flex-wrap: wrap;
  gap: 12px;
}

.tasks-filter-tools {
  display: flex;
  gap: 8px;
}

.search-box-wrap {
  position: relative;
  display: flex;
  align-items: center;
}

.search-icon {
  position: absolute;
  left: 8px;
  color: var(--muted);
}

.task-search-input {
  padding: 6px 10px 6px 28px;
  font-size: 12px;
  background: var(--bg);
  border: 1px solid var(--line);
  border-radius: 6px;
  color: var(--text);
}

.task-status-filter {
  padding: 6px 10px;
  font-size: 12px;
  background: var(--bg);
  border: 1px solid var(--line);
  border-radius: 6px;
  color: var(--text);
}

.milestone-tasks-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(260px, 1fr));
  gap: 14px;
}

.milestone-task-item {
  padding: 14px;
  background: var(--panel-soft);
  border: 1px solid var(--line);
  border-radius: 10px;
  display: flex;
  flex-direction: column;
  justify-content: space-between;
  gap: 10px;
  transition: all 0.2s ease;
}

.milestone-task-item:hover {
  border-color: var(--qaly-primary);
}

.task-item-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  font-size: 11px;
}

.task-number {
  font-family: monospace;
  font-weight: 700;
  color: var(--primary);
}

.task-item-title {
  font-size: 13px;
  font-weight: 600;
  line-height: 1.4;
}

.task-item-footer {
  display: flex;
  justify-content: space-between;
  align-items: center;
  font-size: 11px;
  padding-top: 8px;
  border-top: 1px solid var(--line);
}

.task-inline-status-select {
  padding: 3px 6px;
  font-size: 11px;
  font-weight: 600;
  border-radius: 4px;
  background: var(--bg);
  border: 1px solid var(--line);
  color: var(--text);
}

.empty-tasks-box {
  padding: 24px;
  text-align: center;
  background: var(--panel-soft);
  border: 1px dashed var(--line);
  border-radius: 8px;
  color: var(--muted);
  font-size: 13px;
}

/* Modals */
.modal-backdrop {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.6);
  backdrop-filter: blur(4px);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
  padding: 16px;
}

.preset-modal {
  width: 100%;
  max-width: 820px;
  padding: 24px;
  background: var(--panel);
  border-radius: var(--radius-shell);
  border: 1px solid var(--line);
}

.modal-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 20px;
}

.modal-header h4 {
  margin: 0;
  font-size: 16px;
  font-weight: 700;
}

.preset-options-grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 16px;
}

@media (max-width: 768px) {
  .preset-options-grid {
    grid-template-columns: 1fr;
  }
}

.preset-option-card {
  padding: 20px;
  background: var(--panel-soft);
  border: 1px solid var(--line);
  border-radius: 12px;
  display: flex;
  flex-direction: column;
  justify-content: space-between;
  gap: 14px;
  cursor: pointer;
  transition: all 0.2s ease;
}

.preset-option-card:hover {
  border-color: var(--qaly-primary);
  transform: translateY(-2px);
}

.preset-card-header {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.preset-card-header h5 {
  margin: 0;
  font-size: 14px;
  font-weight: 700;
}

.preset-desc {
  font-size: 12px;
  color: var(--muted);
  line-height: 1.5;
  margin: 0;
}

.milestone-modal, .task-assign-modal {
  width: 100%;
  max-width: 540px;
  padding: 24px;
  background: var(--panel);
  border-radius: var(--radius-shell);
  border: 1px solid var(--line);
}

.modal-body {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.form-group {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.form-group label {
  font-size: 12px;
  font-weight: 600;
}

.form-row {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 12px;
}

.modal-input {
  padding: 8px 12px;
  font-size: 13px;
  background: var(--bg);
  border: 1px solid var(--line);
  border-radius: 6px;
  color: var(--text);
}

.modal-actions {
  display: flex;
  justify-content: flex-end;
  gap: 10px;
}

.assign-tasks-list {
  max-height: 320px;
  overflow-y: auto;
  display: flex;
  flex-direction: column;
  gap: 8px;
  padding-right: 4px;
}

.assign-task-row {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 10px 14px;
  background: var(--panel-soft);
  border: 1px solid var(--line);
  border-radius: 8px;
  cursor: pointer;
  font-size: 13px;
  transition: all 0.2s ease;
}

.assign-task-row.is-selected {
  border-color: var(--qaly-primary);
  background: rgba(37, 99, 235, 0.08);
}
</style>
