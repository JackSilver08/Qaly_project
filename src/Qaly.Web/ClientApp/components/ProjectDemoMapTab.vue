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
} from 'lucide-vue-next'
import { apiResult, apiCommand } from '../utils/api-client'
import { showError, showSuccess } from '../composables/use-toast'
import { useDashboardContext } from '../composables/dashboard-context'
import type { SprintDto, DashboardTask } from '../types'

const props = defineProps<{
  projectId: string
  projectName?: string
}>()

const { selectedProject, activeProjectTab, taskSearchQuery } = useDashboardContext()

const sprints = ref<SprintDto[]>([])
const isLoading = ref(false)
const selectedSprintId = ref<string | null>(null)
const showCreateModal = ref(false)
const showEditModal = ref(false)

// Form states for milestone creation/editing
const milestoneName = ref('')
const milestoneStartDate = ref('')
const milestoneEndDate = ref('')
const milestoneGoal = ref('')
const editingSprintId = ref<string | null>(null)

// Outsource Presets generator
const isGeneratingOutsource = ref(false)

onMounted(() => {
  loadSprints()
})

watch(() => props.projectId, () => {
  loadSprints()
})

async function loadSprints() {
  isLoading.value = true
  try {
    const result = await apiResult<SprintDto[]>(`/api/projects/${props.projectId}/sprints`)
    // Sort sprints chronologically by StartDate
    sprints.value = (result || []).sort((a, b) => new Date(a.startDate).getTime() - new Date(b.startDate).getTime())
    if (sprints.value.length > 0 && !selectedSprintId.value) {
      // Auto select current active or first sprint
      const current = sprints.value.find(s => isCurrentMilestone(s)) || sprints.value[0]
      selectedSprintId.value = current.id
    }
  } catch (error) {
    showError('Không thể tải sơ đồ mốc hành trình dự án.')
  } finally {
    isLoading.value = false
  }
}

// Compute Milestone States
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

  // Fallback: First uncompleted milestone in sequence
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

const overallProgress = computed(() => {
  if (sprints.value.length === 0) return 0
  const totalTasks = sprints.value.reduce((sum, s) => sum + s.taskCount, 0)
  const completedTasks = sprints.value.reduce((sum, s) => sum + s.completedTaskCount, 0)
  if (totalTasks > 0) return Math.round((completedTasks / totalTasks) * 100)
  
  const completedMilestones = sprints.value.filter(s => isCompletedMilestone(s)).length
  return Math.round((completedMilestones / sprints.value.length) * 100)
})

// Tasks belonging to selected milestone
const milestoneTasks = computed<DashboardTask[]>(() => {
  if (!selectedSprintId.value || !selectedProject.value?.tasks) return []
  return selectedProject.value.tasks.filter((t: DashboardTask) => t.sprintId === selectedSprintId.value)
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

// Action: Auto-generate standard Outsource Roadmap Presets
async function generateOutsourceRoadmap() {
  isGeneratingOutsource.value = true
  try {
    const now = new Date()
    const endDate = selectedProject.value?.endDate ? new Date(selectedProject.value.endDate) : new Date(now.getTime() + 60 * 24 * 60 * 60 * 1000)
    const totalDays = Math.max(30, Math.ceil((endDate.getTime() - now.getTime()) / (1000 * 3600 * 24)))
    const stepDays = Math.floor(totalDays / 5)

    const addDays = (d: Date, days: number) => {
      const res = new Date(d)
      res.setDate(res.getDate() + days)
      return res.toISOString()
    }

    const outsourcePhases = [
      {
        name: 'Mốc 1: Khảo sát & Khởi tạo Yêu cầu (Scope Alignment)',
        start: now.toISOString(),
        end: addDays(now, stepDays),
        goal: 'Thống nhất yêu cầu chi tiết của khách hàng, chốt Scope & Ký biên bản khởi tạo dự án.'
      },
      {
        name: 'Mốc 2: Thiết kế Prototype UI/UX & Architecture',
        start: addDays(now, stepDays + 1),
        end: addDays(now, stepDays * 2),
        goal: 'Chốt Wireframe, UI/UX prototype Figma & Thiết kế Kiến trúc Database/API.'
      },
      {
        name: 'Mốc 3: Phát triển Core Modules & Backend Services',
        start: addDays(now, stepDays * 2 + 1),
        end: addDays(now, stepDays * 3),
        goal: 'Lập trình các tính năng cốt lõi (Authentication, Core Domain, Integration APIs).'
      },
      {
        name: 'Mốc 4: Tích hợp Giao diện & AI Services',
        start: addDays(now, stepDays * 3 + 1),
        end: addDays(now, stepDays * 4),
        goal: 'Hoàn thiện giao diện Frontend, tích hợp SignalR, AI Assistant & các dịch vụ bên ngoài.'
      },
      {
        name: 'Mốc 5: Kiểm thử UAT, Sửa lỗi & Demo Khách hàng',
        start: addDays(now, stepDays * 4 + 1),
        end: addDays(now, totalDays - 5),
        goal: 'Tiến hành UAT với khách hàng, sửa lỗi phát sinh và chốt chấp thuận nghiệm thu.'
      },
      {
        name: 'Mốc 6: Bàn giao, Deploy Go-Live & Đào tạo',
        start: addDays(now, totalDays - 4),
        end: endDate.toISOString(),
        goal: 'Triển khai Docker/Kubernetes lên Server Production, bàn giao tài liệu và nghiệm thu hoàn tất.'
      }
    ]

    for (const phase of outsourcePhases) {
      await apiCommand(`/api/projects/${props.projectId}/sprints`, {
        method: 'POST',
        body: JSON.stringify({
          name: phase.name,
          startDate: phase.start,
          endDate: phase.end,
          goal: phase.goal
        })
      })
    }

    await loadSprints()
    showSuccess('Đã tự động khởi tạo Sơ đồ quy trình Outsource chuẩn 6 Mốc!')
  } catch (error) {
    showError('Không thể khởi tạo sơ đồ Outsource tự động.')
  } finally {
    isGeneratingOutsource.value = false
  }
}

// Create new manual milestone
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
    milestoneName.value = ''
    milestoneStartDate.value = ''
    milestoneEndDate.value = ''
    milestoneGoal.value = ''
    await loadSprints()
    showSuccess('Đã thêm mốc tiến độ mới.')
  } catch (error) {
    showError('Không thể tạo mốc tiến độ.')
  }
}

// Edit existing milestone
function openEdit(sprint: SprintDto) {
  editingSprintId.value = sprint.id
  milestoneName.value = sprint.name
  milestoneStartDate.value = sprint.startDate.slice(0, 10)
  milestoneEndDate.value = sprint.endDate.slice(0, 10)
  milestoneGoal.value = sprint.goal || ''
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
        status: activeMilestone.value?.status || 'Planning',
        goal: milestoneGoal.value.trim() || null
      })
    })

    showEditModal.value = false
    editingSprintId.value = null
    await loadSprints()
    showSuccess('Đã cập nhật mốc tiến độ.')
  } catch (error) {
    showError('Không thể cập nhật mốc tiến độ.')
  }
}

// Delete milestone
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

function formatDateRange(start: string, end: string) {
  if (!start || !end) return ''
  const d1 = new Date(start)
  const d2 = new Date(end)
  return `${d1.getDate()}/${d1.getMonth() + 1} - ${d2.getDate()}/${d2.getMonth() + 1}/${d2.getFullYear()}`
}
</script>

<template>
  <div class="project-demo-map-shell">
    
    <!-- Top Overview Header -->
    <div class="demo-map-header glass-card mb-4">
      <div class="demo-map-header__title">
        <div class="title-with-icon">
          <div class="icon-glow-box">
            <Compass :size="24" class="text-primary" />
          </div>
          <div>
            <h3>Sơ Đồ Demo — Hành Trình Tiến Độ Dự Án</h3>
            <p class="text-muted text-sm">
              Theo dõi lộ trình các mốc lớn (Milestone/Outsource Stage) của dự án theo dạng con đường hành trình thực tế.
            </p>
          </div>
        </div>
      </div>

      <div class="demo-map-header__actions">
        <button
          v-if="sprints.length === 0"
          type="button"
          class="primary-button btn-outsource-preset"
          :disabled="isGeneratingOutsource"
          @click="generateOutsourceRoadmap"
        >
          <Sparkles :size="16" />
          <span>{{ isGeneratingOutsource ? 'Đang tạo mẫu...' : 'Tạo Mẫu Outsource Chuẩn' }}</span>
        </button>

        <button type="button" class="secondary-button" @click="showCreateModal = true">
          <Plus :size="16" />
          <span>Thêm mốc mới</span>
        </button>
      </div>
    </div>

    <!-- Empty State if no milestones exist -->
    <div v-if="sprints.length === 0 && !isLoading" class="empty-map-card glass-card">
      <div class="empty-map-content">
        <Layers :size="48" class="text-primary opacity-60 mb-3" />
        <h4>Chưa có mốc tiến độ nào được khai báo</h4>
        <p>
          Dự án này chưa có sơ đồ mốc hành trình. Bạn có thể sử dụng <strong>Mẫu Quy trình Outsource Chuẩn (6 mốc)</strong>
          hoặc tự thêm các mốc quan trọng để theo dõi tiến độ cấp cao.
        </p>

        <div class="empty-actions mt-4">
          <button
            type="button"
            class="primary-button primary-button--lg"
            :disabled="isGeneratingOutsource"
            @click="generateOutsourceRoadmap"
          >
            <Sparkles :size="18" />
            <span>Tạo Sơ Đồ Outsource Chuẩn (Tự động)</span>
          </button>

          <button type="button" class="secondary-button secondary-button--lg ms-3" @click="showCreateModal = true">
            <Plus :size="18" />
            <span>Tự nhập mốc thủ công</span>
          </button>
        </div>
      </div>
    </div>

    <!-- MAIN ROADMAP TIMELINE TRACK -->
    <template v-else>
      <div class="roadmap-track-card glass-card mb-4">
        <div class="roadmap-metrics-bar">
          <div class="metric-pill">
            <span class="metric-label">Vị trí hiện tại</span>
            <strong class="metric-value text-primary">
              {{ currentPositionMilestone ? currentPositionMilestone.name : 'Đã hoàn thành tất cả' }}
            </strong>
          </div>
          <div class="metric-pill">
            <span class="metric-label">Tiến độ tổng thể</span>
            <strong class="metric-value text-success">{{ overallProgress }}% hoàn thành</strong>
          </div>
          <div class="metric-pill">
            <span class="metric-label">Tổng số mốc</span>
            <strong class="metric-value">{{ sprints.length }} Mốc</strong>
          </div>
        </div>

        <!-- Horizontal Connected Milestone Track -->
        <div class="roadmap-scroll-wrapper no-scrollbar">
          <div class="roadmap-visual-container">
            <!-- Connecting Line -->
            <div class="connecting-line">
              <div class="connecting-line-fill" :style="{ width: `${trackFillPercentage}%` }"></div>
            </div>

            <!-- Milestone Nodes -->
            <div class="milestones-nodes-row">
              <div
                v-for="(sprint, index) in sprints"
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
                    <span v-if="isCompletedMilestone(sprint)" class="badge-tag tag-success">Đã xong</span>
                    <span v-else-if="isCurrentMilestone(sprint)" class="badge-tag tag-primary">⚡ Đang làm</span>
                    <span v-else-if="isOverdueMilestone(sprint)" class="badge-tag tag-danger">⚠️ Trễ hạn</span>
                    <span v-else class="badge-tag tag-muted">Chưa tới</span>
                  </div>

                  <h5 class="node-title">{{ sprint.name }}</h5>
                  <div class="node-dates">
                    <Calendar :size="13" />
                    <span>{{ formatDateRange(sprint.startDate, sprint.endDate) }}</span>
                  </div>

                  <div class="node-progress-rail">
                    <div
                      class="node-progress-fill"
                      :class="{ 'bg-success': isCompletedMilestone(sprint), 'bg-primary': isCurrentMilestone(sprint) }"
                      :style="{ width: `${sprint.progress}%` }"
                    ></div>
                  </div>
                  <div class="node-task-count">
                    {{ sprint.completedTaskCount }}/{{ sprint.taskCount }} tasks ({{ sprint.progress }}%)
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- SELECTED MILESTONE DETAIL DRAWER / PANEL -->
      <div v-if="activeMilestone" class="milestone-detail-panel glass-card">
        <div class="detail-header">
          <div class="detail-header-left">
            <span class="badge-tag tag-primary mb-1">Mốc Đang Chọn</span>
            <h4>{{ activeMilestone.name }}</h4>
            <p v-if="activeMilestone.goal" class="detail-goal">
              🎯 <strong>Mục tiêu:</strong> {{ activeMilestone.goal }}
            </p>
            <div class="detail-dates-info">
              <Clock :size="14" />
              <span>Thời gian: {{ formatDateRange(activeMilestone.startDate, activeMilestone.endDate) }}</span>
            </div>
          </div>

          <div class="detail-header-actions">
            <button type="button" class="btn btn-outline" @click="openEdit(activeMilestone)">
              <Edit3 :size="15" />
              <span>Sửa mốc</span>
            </button>
            <button type="button" class="btn btn-danger-ghost" @click="handleDeleteMilestone(activeMilestone.id)">
              <Trash2 :size="15" />
            </button>
            <button type="button" class="primary-button" @click="jumpToKanban(activeMilestone.id)">
              <FolderKanban :size="16" />
              <span>Xem Nhiệm Vụ Mốc Này Trên Kanban ➔</span>
            </button>
          </div>
        </div>

        <!-- Task List Preview inside Milestone -->
        <div class="detail-body mt-4">
          <h5 class="section-subheading mb-3">Danh sách công việc thuộc mốc này ({{ milestoneTasks.length }})</h5>

          <div v-if="milestoneTasks.length === 0" class="empty-tasks-box">
            <p>Chưa có task nào được gán vào mốc này. Bạn có thể gán task từ Kanban board hoặc tạo task mới.</p>
          </div>

          <div v-else class="milestone-tasks-grid">
            <div
              v-for="task in milestoneTasks"
              :key="task.id"
              class="milestone-task-item"
              :class="`status-${task.status.toLowerCase()}`"
            >
              <div class="task-item-header">
                <span class="task-number">{{ task.key || `#${task.number}` }}</span>
                <span class="task-status-pill">{{ task.status }}</span>
              </div>
              <div class="task-item-title">{{ task.title }}</div>
              <div v-if="task.assigneeName" class="task-item-assignee">
                👤 {{ task.assigneeName }}
              </div>
            </div>
          </div>
        </div>
      </div>
    </template>

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
              <input v-model="milestoneName" type="text" placeholder="Ví dụ: Mốc 1: Khảo sát & Prototype UI/UX..." required class="modal-input" />
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

            <div class="form-group">
              <label>Mục tiêu mốc (Goal)</label>
              <textarea v-model="milestoneGoal" rows="3" placeholder="Mô tả mục tiêu nghiệm thu của mốc này..." class="modal-input"></textarea>
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

  </div>
</template>

<style scoped>
.project-demo-map-shell {
  display: flex;
  flex-direction: column;
  gap: 20px;
}

.demo-map-header {
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

.demo-map-header h3 {
  font-size: 18px;
  font-weight: 700;
  margin-bottom: 4px;
}

.demo-map-header__actions {
  display: flex;
  gap: 12px;
}

/* Empty Map */
.empty-map-card {
  padding: 48px 32px;
  text-align: center;
  background: var(--panel);
  border: 1px dashed var(--line);
  border-radius: var(--radius-shell);
}

.empty-map-content {
  max-width: 560px;
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
  gap: 24px;
  margin-bottom: 36px;
  padding-bottom: 20px;
  border-bottom: 1px solid var(--line);
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

/* Scroll Container for Node Path */
.roadmap-scroll-wrapper {
  overflow-x: auto;
  padding: 40px 10px 20px 10px;
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
  width: 170px;
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
  margin-bottom: 8px;
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

/* Detail Panel */
.milestone-detail-panel {
  padding: 24px;
  background: var(--panel);
  border: 1px solid var(--line);
  border-radius: var(--radius-shell);
}

.detail-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  padding-bottom: 16px;
  border-bottom: 1px solid var(--line);
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
}

.milestone-tasks-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(240px, 1fr));
  gap: 12px;
}

.milestone-task-item {
  padding: 12px;
  background: var(--panel-soft);
  border: 1px solid var(--line);
  border-radius: 8px;
}

.task-item-header {
  display: flex;
  justify-content: space-between;
  font-size: 11px;
  margin-bottom: 6px;
}

.task-number {
  font-family: monospace;
  font-weight: 700;
  color: var(--primary);
}

.task-item-title {
  font-size: 13px;
  font-weight: 600;
  margin-bottom: 6px;
}

.task-item-assignee {
  font-size: 11px;
  color: var(--muted);
}

/* Modal styles */
.modal-backdrop {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.6);
  backdrop-filter: blur(4px);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
}

.milestone-modal {
  width: 100%;
  max-width: 520px;
  padding: 24px;
  background: var(--panel);
  border: 1px solid var(--line);
  border-radius: var(--radius-shell);
}

.modal-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 20px;
}

.modal-header h4 {
  font-size: 16px;
  font-weight: 700;
}

.form-group {
  display: flex;
  flex-direction: column;
  gap: 6px;
  margin-bottom: 14px;
}

.form-row {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 12px;
}

.modal-input {
  padding: 8px 12px;
  background: var(--bg);
  border: 1px solid var(--line);
  border-radius: 6px;
  color: var(--text);
  font-size: 13px;
}

.modal-actions {
  display: flex;
  justify-content: flex-end;
  gap: 10px;
}
</style>
