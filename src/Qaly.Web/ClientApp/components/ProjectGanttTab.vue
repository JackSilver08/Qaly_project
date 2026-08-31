<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import {
  AlertCircle,
  Bell,
  CalendarDays,
  ChevronDown,
  ChevronRight,
  CircleCheck,
  Clock3,
  EyeOff,
  Filter,
  Flag,
  RotateCcw,
  ShieldAlert,
  UserRound,
} from 'lucide-vue-next'
import { apiCommand, apiResult, errorMessage } from '../utils/api-client'
import type { PagedResult, ProjectTimelineDto, TaskAttentionDto } from '../types'

const props = defineProps<{
  projectId: string
  projectMembers?: { userId: string; fullName: string; email?: string }[]
}>()

const emit = defineEmits<{
  openTask: [id: string]
}>()

const tasks = ref<any[]>([])
const attentionItems = ref<TaskAttentionDto[]>([])
const timelineSummary = ref<ProjectTimelineDto | null>(null)
const isLoading = ref(true)
const filtersOpen = ref(false)
const riskType = ref('all')
const assigneeId = ref('')
const reporterId = ref('')
const statusFilter = ref('')
const priorityFilter = ref('')
const fromDate = ref('')
const toDate = ref('')
const sortMode = ref('risk')
const nudgePendingKey = ref<string | null>(null)
const nudgeFeedback = ref('')
const commentTaskId = ref<string | null>(null)
const commentText = ref('')
const commentFeedback = ref('')
const DAY_MS = 24 * 60 * 60 * 1000

const riskOptions = [
  { value: 'all', label: 'Tất cả cảnh báo' },
  { value: 'overdue', label: 'Quá hạn' },
  { value: 'duesoon', label: 'Sắp tới hạn' },
  { value: 'staletodo', label: 'Chưa bắt đầu' },
  { value: 'staleinprogress', label: 'Đang làm quá lâu' },
  { value: 'unseen', label: 'Chưa xem' },
]

const statusOptions = [
  { value: '', label: 'Tất cả trạng thái' },
  { value: 'Todo', label: 'Cần làm' },
  { value: 'InProgress', label: 'Đang làm' },
  { value: 'OnHold', label: 'Tạm dừng' },
  { value: 'InReview', label: 'Đang duyệt' },
  { value: 'Done', label: 'Hoàn thành' },
]

const priorityOptions = [
  { value: '', label: 'Tất cả ưu tiên' },
  { value: 'Critical', label: 'Khẩn cấp' },
  { value: 'High', label: 'Cao' },
  { value: 'Medium', label: 'Trung bình' },
  { value: 'Low', label: 'Thấp' },
]

const sortOptions = [
  { value: 'risk', label: 'Rủi ro cao trước' },
  { value: 'dueDate', label: 'Hạn gần nhất' },
  { value: 'priority', label: 'Ưu tiên cao trước' },
  { value: 'assignee', label: 'Theo người phụ trách' },
  { value: 'status', label: 'Theo trạng thái' },
]

async function fetchGanttData() {
  isLoading.value = true
  try {
    const params = new URLSearchParams({ page: '1', pageSize: '100', sort: sortMode.value })
    if (riskType.value !== 'all') params.set('riskType', riskType.value)
    if (assigneeId.value) params.set('assigneeId', assigneeId.value)
    if (reporterId.value) params.set('reporterId', reporterId.value)
    if (statusFilter.value) params.set('status', statusFilter.value)
    if (priorityFilter.value) params.set('priority', priorityFilter.value)
    if (fromDate.value) params.set('from', new Date(fromDate.value).toISOString())
    if (toDate.value) params.set('to', new Date(toDate.value).toISOString())

    const [gantt, attention, timeline] = await Promise.all([
      apiResult<any[]>(`/api/tasks/project/${props.projectId}/gantt`),
      apiResult<PagedResult<TaskAttentionDto>>(`/api/projects/${props.projectId}/task-attention?${params.toString()}`),
      apiResult<ProjectTimelineDto>(`/api/projects/${props.projectId}/timeline`),
    ])
    tasks.value = gantt
    attentionItems.value = attention.items
    timelineSummary.value = timeline
  } catch {
    tasks.value = []
    attentionItems.value = []
    timelineSummary.value = null
  } finally {
    isLoading.value = false
  }
}

const filteredAttentionItems = computed(() => attentionItems.value)

const summary = computed(() => ({
  total: attentionItems.value.length,
  overdue: attentionItems.value.filter((item) => item.isOverdue).length,
  staleTodo: attentionItems.value.filter((item) => item.isStaleTodo).length,
  unseen: attentionItems.value.filter((item) => item.isUnseenByAssignee).length,
}))

const completionRate = computed(() => {
  const total = timelineSummary.value?.totalTasks ?? 0
  const done = timelineSummary.value?.doneTasks ?? 0
  return total ? Math.round((done / total) * 100) : 0
})

const activeFilterCount = computed(() =>
  [
    riskType.value !== 'all',
    assigneeId.value,
    reporterId.value,
    statusFilter.value,
    priorityFilter.value,
    fromDate.value,
    toDate.value,
    sortMode.value !== 'risk',
  ].filter(Boolean).length,
)

function resetFilters() {
  riskType.value = 'all'
  assigneeId.value = ''
  reporterId.value = ''
  statusFilter.value = ''
  priorityFilter.value = ''
  fromDate.value = ''
  toDate.value = ''
  sortMode.value = 'risk'
  fetchGanttData()
}

function toValidTimestamp(value: string | null | undefined) {
  if (!value) return null
  const time = new Date(value).getTime()
  return Number.isFinite(time) ? time : null
}

const timelineBounds = computed(() => {
  const now = Date.now()
  const points = tasks.value
    .flatMap((task) => [toValidTimestamp(task.startDate), toValidTimestamp(task.endDate)])
    .filter((time): time is number => time !== null)

  if (!points.length) return { min: new Date(now), max: new Date(now + 7 * DAY_MS) }

  const min = Math.min(...points)
  const max = Math.max(...points)
  return { min: new Date(min), max: new Date(max >= min ? max : min + 7 * DAY_MS) }
})

const minDate = computed(() => timelineBounds.value.min)
const maxDate = computed(() => timelineBounds.value.max)
const totalDays = computed(() => {
  const spanDays = Math.ceil((maxDate.value.getTime() - minDate.value.getTime()) / DAY_MS) + 3
  return Math.max(1, Math.min(366, spanDays))
})

const timelineDays = computed(() =>
  Array.from({ length: totalDays.value }, (_, index) => {
    const date = new Date(minDate.value.getTime() + index * DAY_MS)
    return {
      key: date.toISOString(),
      day: date.getDate(),
      weekday: new Intl.DateTimeFormat('vi-VN', { weekday: 'short' }).format(date),
      month: date.getDate() === 1 || index === 0
        ? new Intl.DateTimeFormat('vi-VN', { month: 'short' }).format(date)
        : '',
      isWeekend: date.getDay() === 0 || date.getDay() === 6,
      isToday: new Date().toDateString() === date.toDateString(),
    }
  }),
)

function getTaskStyle(task: any) {
  const startTime = toValidTimestamp(task.startDate) ?? toValidTimestamp(task.endDate)
  const endTime = toValidTimestamp(task.endDate) ?? startTime
  if (startTime === null || endTime === null) return { display: 'none' }

  const normalizedEnd = Math.max(startTime, endTime)
  const left = Math.floor((startTime - minDate.value.getTime()) / DAY_MS) + 1
  const width = Math.ceil((normalizedEnd - startTime) / DAY_MS) + 1
  const startColumn = Math.max(1, left)
  const span = Math.max(1, Math.min(width, totalDays.value - startColumn + 1))
  return { gridColumn: `${startColumn} / span ${span}` }
}

function taskTone(task: any) {
  const risk = attentionItems.value.find((item) => item.id === task.id)
  if (risk?.isOverdue) return 'is-overdue'
  if (risk?.isStaleTodo) return 'is-stale'
  if (risk?.isDueSoon) return 'is-due'
  if (task.isCriticalPath) return 'is-critical'
  return 'is-normal'
}

function reasonLabel(reason: string) {
  const labels: Record<string, string> = {
    QuaHan: 'Quá hạn',
    SapToiHan: 'Sắp tới hạn',
    ChuaBatDau: 'Chưa bắt đầu',
    DangLamQuaLau: 'Đang làm quá lâu',
    ChuaXem: 'Chưa xem',
  }
  return labels[reason] ?? reason
}

function statusLabel(status: string) {
  return statusOptions.find((option) => option.value === status)?.label ?? status
}

function initials(name: string | null) {
  if (!name) return '?'
  return name.split(' ').filter(Boolean).slice(-2).map((part) => part[0]).join('').toUpperCase()
}

async function nudgeAssignee(item: TaskAttentionDto) {
  const key = `${item.id}-${item.assigneeId ?? 'all'}`
  nudgePendingKey.value = key
  nudgeFeedback.value = ''
  try {
    await apiCommand(`/api/projects/${props.projectId}/tasks/${item.id}/nudge`, {
      method: 'POST',
      body: JSON.stringify({ assigneeId: item.assigneeId }),
    })
    nudgeFeedback.value = 'Đã gửi nhắc việc cho người phụ trách.'
  } catch (error) {
    nudgeFeedback.value = errorMessage(error, 'Không thể gửi nhắc việc.')
  } finally {
    nudgePendingKey.value = null
  }
}

function openComment(item: TaskAttentionDto) {
  commentTaskId.value = item.id
  commentText.value = ''
  commentFeedback.value = ''
}

async function submitAttentionComment(item: TaskAttentionDto) {
  const content = commentText.value.trim()
  if (!content) return
  try {
    await apiResult('/api/comments', {
      method: 'POST',
      body: JSON.stringify({ taskItemId: item.id, content }),
    })
    commentFeedback.value = 'Đã ghi nhận bình luận.'
    commentTaskId.value = null
    commentText.value = ''
  } catch (error) {
    commentFeedback.value = errorMessage(error, 'Không thể gửi bình luận.')
  }
}

function formatDate(value: string | null, includeTime = false) {
  if (!value) return 'Chưa đặt'
  return new Intl.DateTimeFormat('vi-VN', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    ...(includeTime ? { hour: '2-digit', minute: '2-digit' } : {}),
  }).format(new Date(value))
}

onMounted(fetchGanttData)
</script>

<template>
  <div class="timeline-shell reveal">
    <section v-if="timelineSummary" class="sprint-overview glass-card">
      <div class="overview-main">
        <div class="eyebrow"><Flag :size="14" /> Sprint hiện tại</div>
        <div class="overview-heading">
          <div>
            <h2>{{ timelineSummary.openTasks }} việc đang mở</h2>
            <p>{{ formatDate(timelineSummary.sprintStart) }} - {{ formatDate(timelineSummary.sprintEnd) }}</p>
          </div>
          <div class="completion-ring" :style="{ '--progress': `${completionRate * 3.6}deg` }">
            <div><strong>{{ completionRate }}%</strong><span>hoàn thành</span></div>
          </div>
        </div>
        <div class="progress-track" aria-label="Tiến độ sprint">
          <span :style="{ width: `${completionRate}%` }"></span>
        </div>
        <div class="overview-stats">
          <div><CircleCheck :size="18" /><span><strong>{{ timelineSummary.doneTasks }}</strong> đã xong</span></div>
          <div><Clock3 :size="18" /><span><strong>{{ timelineSummary.openTasks }}</strong> đang mở</span></div>
          <div :class="{ danger: timelineSummary.overdueTasks }"><ShieldAlert :size="18" /><span><strong>{{ timelineSummary.overdueTasks }}</strong> quá hạn</span></div>
          <div :class="{ danger: timelineSummary.blockedTasks }"><AlertCircle :size="18" /><span><strong>{{ timelineSummary.blockedTasks }}</strong> bị chặn</span></div>
        </div>
      </div>

      <div class="overview-side">
        <div class="side-header">
          <div>
            <span>Phân bổ theo sprint</span>
            <small>{{ formatDate(timelineSummary.windowStart) }} - {{ formatDate(timelineSummary.windowEnd) }}</small>
          </div>
          <strong>{{ timelineSummary.totalTasks }} task</strong>
        </div>
        <div class="sprint-buckets">
          <article v-for="bucket in timelineSummary.buckets" :key="bucket.label" class="sprint-bucket">
            <div class="bucket-title">
              <strong>{{ bucket.label }}</strong>
              <span>{{ bucket.doneCount }}/{{ bucket.taskCount }} xong</span>
            </div>
            <div class="bucket-track">
              <span :style="{ width: `${bucket.taskCount ? Math.round(bucket.doneCount / bucket.taskCount * 100) : 0}%` }"></span>
            </div>
            <div class="bucket-meta">
              <span>{{ bucket.activeCount }} đang mở</span>
              <span v-if="bucket.overdueCount" class="danger">{{ bucket.overdueCount }} quá hạn</span>
              <span>{{ bucket.plannedPoints }} điểm</span>
            </div>
          </article>
        </div>
        <button
          v-if="timelineSummary.blockedItems.length"
          type="button"
          class="blocked-callout"
          @click="emit('openTask', timelineSummary.blockedItems[0].taskId)"
        >
          <AlertCircle :size="18" />
          <span><strong>{{ timelineSummary.blockedItems.length }} task đang bị chặn</strong><small>Xem và gỡ vướng ngay</small></span>
          <ChevronRight :size="18" />
        </button>
      </div>
    </section>

    <section class="attention-panel glass-card">
      <div class="section-header">
        <div>
          <div class="eyebrow eyebrow--danger"><ShieldAlert :size="14" /> Cần xử lý</div>
          <h3>Công việc cần chú ý</h3>
          <p>Ưu tiên các việc quá hạn, chưa bắt đầu hoặc chưa được người nhận xem.</p>
        </div>
        <button type="button" class="filter-toggle" :class="{ active: filtersOpen }" :aria-expanded="filtersOpen" @click="filtersOpen = !filtersOpen">
          <Filter :size="16" />
          Bộ lọc
          <span v-if="activeFilterCount">{{ activeFilterCount }}</span>
          <ChevronDown :size="16" :class="{ rotated: filtersOpen }" />
        </button>
      </div>

      <div class="attention-summary">
        <button type="button" :class="{ selected: riskType === 'overdue' }" :aria-pressed="riskType === 'overdue'" @click="riskType = 'overdue'; fetchGanttData()">
          <span class="metric-dot danger"></span><strong>{{ summary.overdue }}</strong><small>Quá hạn</small>
        </button>
        <button type="button" :class="{ selected: riskType === 'staletodo' }" :aria-pressed="riskType === 'staletodo'" @click="riskType = 'staletodo'; fetchGanttData()">
          <span class="metric-dot warning"></span><strong>{{ summary.staleTodo }}</strong><small>Chưa bắt đầu</small>
        </button>
        <button type="button" :class="{ selected: riskType === 'unseen' }" :aria-pressed="riskType === 'unseen'" @click="riskType = 'unseen'; fetchGanttData()">
          <span class="metric-dot muted"></span><strong>{{ summary.unseen }}</strong><small>Chưa xem</small>
        </button>
        <button type="button" :class="{ selected: riskType === 'all' }" :aria-pressed="riskType === 'all'" @click="riskType = 'all'; fetchGanttData()">
          <span class="metric-dot primary"></span><strong>{{ summary.total }}</strong><small>Tất cả cảnh báo</small>
        </button>
      </div>

      <div v-if="filtersOpen" class="filter-panel">
        <label><span>Loại cảnh báo</span><select v-model="riskType" @change="fetchGanttData"><option v-for="option in riskOptions" :key="option.value" :value="option.value">{{ option.label }}</option></select></label>
        <label><span>Người phụ trách</span><select v-model="assigneeId" @change="fetchGanttData"><option value="">Tất cả</option><option v-for="member in projectMembers" :key="member.userId" :value="member.userId">{{ member.fullName }}</option></select></label>
        <label><span>Người giao</span><select v-model="reporterId" @change="fetchGanttData"><option value="">Tất cả</option><option v-for="member in projectMembers" :key="member.userId" :value="member.userId">{{ member.fullName }}</option></select></label>
        <label><span>Trạng thái</span><select v-model="statusFilter" @change="fetchGanttData"><option v-for="option in statusOptions" :key="option.value" :value="option.value">{{ option.label }}</option></select></label>
        <label><span>Ưu tiên</span><select v-model="priorityFilter" @change="fetchGanttData"><option v-for="option in priorityOptions" :key="option.value" :value="option.value">{{ option.label }}</option></select></label>
        <label><span>Sắp xếp</span><select v-model="sortMode" @change="fetchGanttData"><option v-for="option in sortOptions" :key="option.value" :value="option.value">{{ option.label }}</option></select></label>
        <label><span>Từ ngày</span><input v-model="fromDate" type="date" @change="fetchGanttData" /></label>
        <label><span>Đến ngày</span><input v-model="toDate" type="date" @change="fetchGanttData" /></label>
        <button type="button" class="reset-button" @click="resetFilters"><RotateCcw :size="15" /> Đặt lại</button>
      </div>

      <div v-if="isLoading" class="loading-state" role="status" aria-live="polite">Đang tải công việc...</div>
      <div v-else-if="filteredAttentionItems.length" class="attention-list">
        <article v-for="item in filteredAttentionItems" :key="`${item.id}-${item.assigneeId ?? 'none'}`" class="attention-row">
          <span class="risk-rail" :class="{ overdue: item.isOverdue, warning: !item.isOverdue }"></span>
          <div class="attention-main">
            <div class="reason-list">
              <span v-for="reason in item.reasons" :key="reason" class="reason-chip" :class="{ danger: reason === 'QuaHan' }">{{ reasonLabel(reason) }}</span>
            </div>
            <button type="button" class="task-link" @click="emit('openTask', item.id)">{{ item.title }}</button>
            <div class="task-context">
              <span><UserRound :size="14" /> {{ item.assigneeName || 'Chưa giao' }}</span>
              <span><Clock3 :size="14" /> Hạn {{ formatDate(item.dueDate) }}</span>
              <span class="status-pill">{{ statusLabel(item.status) }}</span>
              <span v-if="item.isUnseenByAssignee" class="unseen-note"><EyeOff :size="14" /> Chưa xem</span>
            </div>
          </div>
          <div class="assignee-avatar" :title="item.assigneeName || 'Chưa giao'">{{ initials(item.assigneeName) }}</div>
          <div class="attention-actions">
            <button v-if="item.allowedActions.includes('BinhLuan')" type="button" class="text-button" @click="openComment(item)">Bình luận</button>
            <button v-if="item.allowedActions.includes('NhacNguoiPhuTrach')" type="button" class="nudge-button" :disabled="nudgePendingKey === `${item.id}-${item.assigneeId ?? 'all'}`" @click="nudgeAssignee(item)">
              <Bell :size="14" /> {{ nudgePendingKey === `${item.id}-${item.assigneeId ?? 'all'}` ? 'Đang nhắc' : 'Nhắc việc' }}
            </button>
            <button type="button" class="open-button" aria-label="Mở chi tiết" @click="emit('openTask', item.id)"><ChevronRight :size="18" /></button>
          </div>
          <form v-if="commentTaskId === item.id" class="attention-comment" @submit.prevent="submitAttentionComment(item)">
            <input v-model="commentText" type="text" aria-label="Lý do hoặc cập nhật tiến độ" placeholder="Nhập lý do hoặc cập nhật tiến độ..." autofocus />
            <button class="nudge-button" type="submit">Gửi</button>
          </form>
        </article>
        <p v-if="nudgeFeedback" class="feedback">{{ nudgeFeedback }}</p>
        <p v-if="commentFeedback" class="feedback">{{ commentFeedback }}</p>
      </div>
      <div v-else class="empty-state"><CircleCheck :size="38" /><strong>Không có việc cần chú ý</strong><p>Bộ lọc hiện tại không có cảnh báo nào.</p></div>
    </section>

    <section class="gantt-chart glass-card">
      <div class="section-header section-header--gantt">
        <div>
          <div class="eyebrow"><CalendarDays :size="14" /> Kế hoạch</div>
          <h3>Dòng thời gian</h3>
          <p>Theo dõi thời lượng, tiến độ và các điểm có nguy cơ trễ.</p>
        </div>
        <div class="gantt-legend">
          <span><i class="normal"></i>Bình thường</span>
          <span><i class="due"></i>Sắp hạn</span>
          <span><i class="stale"></i>Chưa bắt đầu</span>
          <span><i class="overdue"></i>Quá hạn</span>
        </div>
      </div>

      <div v-if="isLoading" class="loading-state" role="status" aria-live="polite">Đang tải timeline...</div>
      <div v-else-if="tasks.length === 0" class="empty-state"><AlertCircle :size="38" /><strong>Chưa có dữ liệu timeline</strong><p>Hãy thêm ngày bắt đầu hoặc hạn hoàn thành cho task.</p></div>
      <div v-else class="gantt-frame">
        <div class="gantt-labels">
          <div class="labels-header">Công việc</div>
          <button v-for="task in tasks" :key="task.id" type="button" class="gantt-task-label" @click="emit('openTask', task.id)">
            <span class="task-status-dot" :class="taskTone(task)"></span>
            <span><strong>{{ task.title }}</strong><small>{{ task.progress }}% hoàn thành</small></span>
          </button>
        </div>
        <div class="gantt-scroll no-scrollbar">
          <div class="gantt-timeline" :style="{ width: `${totalDays * 44}px` }">
            <div class="day-header" :style="{ gridTemplateColumns: `repeat(${totalDays}, 44px)` }">
              <div v-for="day in timelineDays" :key="day.key" :class="{ weekend: day.isWeekend, today: day.isToday }">
                <small>{{ day.month || day.weekday }}</small><strong>{{ day.day }}</strong>
              </div>
            </div>
            <div v-for="task in tasks" :key="task.id" class="gantt-row" :style="{ gridTemplateColumns: `repeat(${totalDays}, 44px)` }">
              <span v-for="day in timelineDays" :key="day.key" class="day-cell" :class="{ weekend: day.isWeekend, today: day.isToday }"></span>
              <button v-if="task.startDate || task.endDate" type="button" class="task-bar" :class="taskTone(task)" :style="getTaskStyle(task)" @click="emit('openTask', task.id)">
                <span class="bar-progress" :style="{ width: `${task.progress}%` }"></span>
                <strong>{{ task.progress }}%</strong>
              </button>
            </div>
          </div>
        </div>
      </div>
    </section>
  </div>
</template>

<style scoped>
.timeline-shell { display: flex; flex-direction: column; gap: 18px; }
.glass-card { background: var(--panel); border: 1px solid var(--line); border-radius: var(--radius-shell); box-shadow: var(--qaly-shadow-md); }
.sprint-overview { display: grid; grid-template-columns: minmax(0, 1.05fr) minmax(360px, .95fr); overflow: hidden; }
.overview-main { padding: 28px; border-right: 1px solid var(--line); background: linear-gradient(145deg, rgba(37, 99, 235, .07), transparent 55%); }
.overview-side { padding: 24px 28px; display: flex; flex-direction: column; gap: 16px; }
.eyebrow { display: inline-flex; align-items: center; gap: 7px; color: var(--primary); font-size: 11px; font-weight: 900; letter-spacing: .08em; text-transform: uppercase; }
.eyebrow--danger { color: #dc2626; }
.overview-heading { display: flex; align-items: center; justify-content: space-between; gap: 24px; margin: 12px 0 18px; }
.overview-heading h2 { margin: 0 0 6px; color: var(--text-strong); font-size: clamp(25px, 3vw, 34px); letter-spacing: -.04em; }
.overview-heading p { margin: 0; color: var(--muted); font-size: 13px; font-weight: 700; }
.completion-ring { --progress: 0deg; flex: 0 0 92px; width: 92px; height: 92px; padding: 7px; border-radius: 50%; background: conic-gradient(var(--primary) var(--progress), var(--bg-soft) 0); }
.completion-ring > div { width: 100%; height: 100%; border-radius: 50%; background: var(--panel); display: flex; flex-direction: column; align-items: center; justify-content: center; }
.completion-ring strong { color: var(--text-strong); font-size: 20px; }
.completion-ring span { color: var(--muted); font-size: 10px; font-weight: 700; }
.progress-track, .bucket-track { height: 7px; border-radius: 999px; background: var(--bg-soft); overflow: hidden; }
.progress-track > span, .bucket-track > span { display: block; height: 100%; border-radius: inherit; background: linear-gradient(90deg, var(--primary), #22c1dc); }
.overview-stats { display: grid; grid-template-columns: repeat(4, 1fr); gap: 8px; margin-top: 22px; }
.overview-stats > div { display: flex; align-items: center; gap: 9px; color: var(--muted); padding: 10px; border-radius: var(--qaly-radius-lg); background: rgba(255,255,255,.5); }
.overview-stats svg { color: var(--primary); }
.overview-stats span { display: flex; flex-direction: column; font-size: 11px; }
.overview-stats strong { color: var(--text-strong); font-size: 17px; }
.overview-stats .danger svg, .overview-stats .danger strong, .danger { color: #dc2626 !important; }
.side-header, .bucket-title, .bucket-meta { display: flex; align-items: center; justify-content: space-between; gap: 12px; }
.side-header > div { display: flex; flex-direction: column; gap: 3px; }
.side-header span, .side-header strong { color: var(--text-strong); font-size: 13px; font-weight: 800; }
.side-header small, .bucket-meta { color: var(--muted); font-size: 11px; }
.sprint-buckets { display: flex; flex-direction: column; gap: 12px; }
.sprint-bucket { display: flex; flex-direction: column; gap: 8px; }
.bucket-title strong { color: var(--text-strong); font-size: 13px; }
.bucket-title span { color: var(--muted); font-size: 11px; font-weight: 700; }
.bucket-track { height: 5px; }
.bucket-meta { justify-content: flex-start; }
.blocked-callout { margin-top: auto; width: 100%; display: flex; align-items: center; gap: 11px; border: 1px solid rgba(220,38,38,.18); border-radius: var(--qaly-radius-lg); padding: 11px 13px; color: #b91c1c; background: rgba(254,242,242,.8); cursor: pointer; text-align: left; }
.blocked-callout span { display: flex; flex: 1; flex-direction: column; }
.blocked-callout strong { font-size: 12px; }.blocked-callout small { opacity: .75; }
.attention-panel, .gantt-chart { padding: 24px; display: flex; flex-direction: column; gap: 18px; }
.section-header { display: flex; justify-content: space-between; align-items: flex-start; gap: 20px; }
.section-header h3 { margin: 7px 0 4px; color: var(--text-strong); font-size: 20px; letter-spacing: -.02em; }
.section-header p { margin: 0; color: var(--muted); font-size: 13px; }
.filter-toggle { display: flex; align-items: center; gap: 8px; min-height: 40px; border: 1px solid var(--line); border-radius: var(--qaly-radius-lg); padding: 8px 12px; color: var(--text); background: var(--panel); font-weight: 800; cursor: pointer; }
.filter-toggle.active { color: var(--primary); border-color: rgba(37,99,235,.35); background: var(--blue-50); }
.filter-toggle span { min-width: 20px; height: 20px; border-radius: 99px; display: grid; place-items: center; color: white; background: var(--primary); font-size: 11px; }
.filter-toggle .rotated { transform: rotate(180deg); }.filter-toggle svg { transition: transform .2s ease; }
.attention-summary { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); border: 1px solid var(--line); border-radius: var(--qaly-radius-lg); overflow: hidden; }
.attention-summary button { position: relative; display: grid; grid-template-columns: auto auto 1fr; align-items: center; gap: 9px; padding: 14px 16px; border: 0; border-right: 1px solid var(--line); color: var(--text); background: var(--panel); cursor: pointer; text-align: left; }
.attention-summary button:last-child { border-right: 0; }
.attention-summary button:hover, .attention-summary button.selected { background: var(--bg-soft); }
.attention-summary button.selected::after { content: ''; position: absolute; inset: auto 12px 0; height: 2px; border-radius: var(--qaly-radius-lg); background: var(--primary); }
.attention-summary strong { color: var(--text-strong); font-size: 20px; }.attention-summary small { color: var(--muted); font-weight: 700; }
.metric-dot { width: 8px; height: 8px; border-radius: 50%; }.metric-dot.danger { background: #dc2626; }.metric-dot.warning { background: #f59e0b; }.metric-dot.muted { background: #94a3b8; }.metric-dot.primary { background: var(--primary); }
.filter-panel { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 12px; padding: 16px; border-radius: var(--qaly-radius-lg); background: var(--bg-soft); border: 1px solid var(--line); }
.filter-panel label { display: flex; flex-direction: column; gap: 6px; }.filter-panel label span { color: var(--muted); font-size: 11px; font-weight: 800; }
.filter-panel select, .filter-panel input { width: 100%; min-height: 38px; border: 1px solid var(--line); border-radius: var(--qaly-radius-lg); padding: 7px 9px; outline: none; color: var(--text-strong); background: var(--panel); font: inherit; font-size: 12px; font-weight: 700; }
.reset-button { align-self: end; justify-self: start; display: inline-flex; align-items: center; gap: 7px; min-height: 38px; border: 0; color: var(--primary); background: transparent; font-weight: 800; cursor: pointer; }
.attention-list { display: flex; flex-direction: column; gap: 8px; }
.attention-row { position: relative; display: grid; grid-template-columns: minmax(0, 1fr) 36px auto; align-items: center; gap: 14px; overflow: hidden; border: 1px solid var(--line); border-radius: var(--qaly-radius-lg); padding: 15px 14px 15px 18px; background: var(--panel); transition: border-color .2s, box-shadow .2s; }
.attention-row:hover { border-color: rgba(37,99,235,.28); box-shadow: var(--qaly-shadow-md); }
.risk-rail { position: absolute; inset: 0 auto 0 0; width: 4px; background: #f59e0b; }.risk-rail.overdue { background: #dc2626; }
.attention-main { min-width: 0; }.reason-list { display: flex; flex-wrap: wrap; gap: 5px; margin-bottom: 6px; }
.reason-chip { border-radius: 999px; padding: 3px 7px; color: #92400e; background: #fef3c7; font-size: 10px; font-weight: 900; }.reason-chip.danger { color: #b91c1c; background: #fee2e2; }
.task-link { display: block; max-width: 100%; overflow: hidden; border: 0; padding: 0; color: var(--text-strong); background: transparent; font-size: 14px; font-weight: 850; text-align: left; text-overflow: ellipsis; white-space: nowrap; cursor: pointer; }
.task-link:hover { color: var(--primary); }
.task-context { display: flex; flex-wrap: wrap; align-items: center; gap: 12px; margin-top: 7px; color: var(--muted); font-size: 11px; }
.task-context > span { display: inline-flex; align-items: center; gap: 5px; }
.status-pill { padding: 3px 7px; border-radius: 999px; color: var(--primary) !important; background: var(--blue-50); font-weight: 800; }
.unseen-note { color: #c2410c !important; font-weight: 800; }
.assignee-avatar { width: 34px; height: 34px; display: grid; place-items: center; border-radius: 50%; color: var(--primary); background: var(--blue-50); font-size: 11px; font-weight: 900; }
.attention-actions { display: flex; align-items: center; gap: 7px; }
.text-button, .nudge-button, .open-button { min-height: 34px; border-radius: var(--qaly-radius-lg); font-size: 11px; font-weight: 800; cursor: pointer; }
.text-button { border: 0; color: var(--muted); background: transparent; }.text-button:hover { color: var(--primary); }
.nudge-button { display: inline-flex; align-items: center; gap: 6px; border: 1px solid rgba(37,99,235,.2); padding: 7px 10px; color: var(--primary); background: var(--blue-50); }
.nudge-button:disabled { opacity: .55; cursor: wait; }
.open-button { width: 34px; display: grid; place-items: center; border: 1px solid var(--line); color: var(--muted); background: var(--panel); }
.attention-comment { grid-column: 1 / -1; display: flex; gap: 8px; padding-top: 12px; border-top: 1px solid var(--line-light); }
.attention-comment input { flex: 1; min-width: 0; border: 1px solid var(--line); border-radius: var(--qaly-radius-lg); padding: 8px 10px; outline: none; color: var(--text-strong); background: var(--bg-soft); }
.feedback { margin: 4px 0 0; color: var(--muted); font-size: 12px; font-weight: 700; }
.section-header--gantt { align-items: flex-end; }
.gantt-legend { display: flex; flex-wrap: wrap; justify-content: flex-end; gap: 13px; color: var(--muted); font-size: 11px; }
.gantt-legend span { display: inline-flex; align-items: center; gap: 6px; }.gantt-legend i { width: 8px; height: 8px; border-radius: 50%; }
.gantt-legend .normal { background: #3b82f6; }.gantt-legend .due { background: #f59e0b; }.gantt-legend .stale { background: #ea580c; }.gantt-legend .overdue { background: #dc2626; }
.gantt-frame { display: grid; grid-template-columns: 230px minmax(0, 1fr); overflow: hidden; border: 1px solid var(--line); border-radius: var(--qaly-radius-lg); }
.gantt-labels { position: relative; z-index: 4; border-right: 1px solid var(--line); background: var(--panel); box-shadow: var(--qaly-shadow-md); }
.labels-header, .day-header { height: 54px; border-bottom: 1px solid var(--line); }
.labels-header { display: flex; align-items: center; padding: 0 16px; color: var(--muted); font-size: 11px; font-weight: 900; text-transform: uppercase; letter-spacing: .06em; }
.gantt-task-label { width: 100%; height: 58px; display: flex; align-items: center; gap: 10px; border: 0; border-bottom: 1px solid var(--line-light); padding: 0 14px; color: var(--text-strong); background: var(--panel); text-align: left; cursor: pointer; }
.gantt-task-label:hover { background: var(--bg-soft); }.gantt-task-label > span:last-child { min-width: 0; display: flex; flex-direction: column; gap: 3px; }
.gantt-task-label strong { overflow: hidden; font-size: 12px; text-overflow: ellipsis; white-space: nowrap; }.gantt-task-label small { color: var(--muted); font-size: 10px; }
.task-status-dot { flex: 0 0 8px; width: 8px; height: 8px; border-radius: 50%; }
.task-status-dot.is-normal { background: #3b82f6; }.task-status-dot.is-due { background: #f59e0b; }.task-status-dot.is-stale { background: #ea580c; }.task-status-dot.is-overdue { background: #dc2626; }.task-status-dot.is-critical { background: #8b5cf6; }
.gantt-scroll { overflow-x: auto; background: var(--panel); }
.day-header, .gantt-row { display: grid; position: relative; }
.day-header > div { display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 2px; border-right: 1px solid var(--line-light); color: var(--muted); background: var(--panel); }
.day-header small { font-size: 9px; text-transform: capitalize; }.day-header strong { color: var(--text-strong); font-size: 12px; }
.day-header .weekend, .day-cell.weekend { background: rgba(148,163,184,.07); }.day-header .today { color: var(--primary); background: var(--blue-50); }
.day-header .today strong { width: 24px; height: 24px; display: grid; place-items: center; border-radius: 50%; color: white; background: var(--primary); }
.gantt-row { height: 58px; border-bottom: 1px solid var(--line-light); }
.day-cell { grid-row: 1; border-right: 1px solid var(--line-light); }.day-cell.today { border-left: 1px solid rgba(37,99,235,.35); border-right: 1px solid rgba(37,99,235,.35); background: rgba(37,99,235,.025); }
.task-bar { grid-row: 1; position: relative; align-self: center; height: 28px; margin: 0 4px; overflow: hidden; border: 0; border-radius: var(--qaly-radius-lg); padding: 0 9px; color: white; background: #3b82f6; box-shadow: var(--qaly-shadow-md); text-align: right; cursor: pointer; z-index: 2; }
.task-bar.is-due { background: #f59e0b; }.task-bar.is-stale { background: #ea580c; }.task-bar.is-overdue { background: #dc2626; }.task-bar.is-critical { background: #8b5cf6; }
.bar-progress { position: absolute; inset: 0 auto 0 0; background: rgba(255,255,255,.22); }.task-bar strong { position: relative; z-index: 1; font-size: 10px; }
.loading-state, .empty-state { min-height: 150px; display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 7px; color: var(--muted); text-align: center; }
.empty-state strong { color: var(--text-strong); }.empty-state p { margin: 0; font-size: 12px; }
@media (max-width: 1050px) {
  .sprint-overview { grid-template-columns: 1fr; }.overview-main { border-right: 0; border-bottom: 1px solid var(--line); }
  .filter-panel { grid-template-columns: repeat(2, minmax(0, 1fr)); }
}
@media (max-width: 760px) {
  .overview-main, .overview-side, .attention-panel, .gantt-chart { padding: 18px; }
  .overview-stats { grid-template-columns: repeat(2, 1fr); }.attention-summary { grid-template-columns: repeat(2, 1fr); }
  .attention-summary button:nth-child(2) { border-right: 0; }.attention-summary button:nth-child(-n+2) { border-bottom: 1px solid var(--line); }
  .section-header, .section-header--gantt { flex-direction: column; align-items: stretch; }.filter-toggle { align-self: flex-start; }
  .attention-row { grid-template-columns: minmax(0, 1fr) auto; }.assignee-avatar { display: none; }.attention-actions { grid-column: 1 / -1; justify-content: flex-end; }
  .gantt-frame { grid-template-columns: 180px minmax(0, 1fr); }.gantt-legend { justify-content: flex-start; }
}
@media (max-width: 520px) {
  .overview-heading { align-items: flex-start; }.completion-ring { width: 76px; height: 76px; flex-basis: 76px; }
  .filter-panel { grid-template-columns: 1fr; }.attention-summary strong { font-size: 17px; }.attention-summary button { padding: 12px 10px; }
  .gantt-frame { grid-template-columns: 150px minmax(0, 1fr); }
}
</style>
