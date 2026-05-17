<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { AlertCircle, Bell, Calendar, EyeOff, Filter, MessageSquareWarning } from 'lucide-vue-next'
import { apiCommand, apiResult, errorMessage } from '../utils/api-client'
import type { PagedResult, TaskAttentionDto } from '../types'

const props = defineProps<{
  projectId: string
  projectMembers?: { userId: string; fullName: string; email?: string }[]
}>()

const emit = defineEmits<{
  openTask: [id: string]
}>()

const tasks = ref<any[]>([])
const attentionItems = ref<TaskAttentionDto[]>([])
const isLoading = ref(true)
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

const riskOptions = [
  { value: 'all', label: 'Tất cả cảnh báo' },
  { value: 'overdue', label: 'Quá hạn' },
  { value: 'duesoon', label: 'Sắp tới hạn' },
  { value: 'staletodo', label: 'Chưa bắt đầu' },
  { value: 'staleinprogress', label: 'Đang làm quá lâu' },
  { value: 'unseen', label: 'Chưa xem' },
]

async function fetchGanttData() {
  isLoading.value = true
  try {
    const params = new URLSearchParams({
      page: '1',
      pageSize: '100',
      sort: sortMode.value,
    })
    if (riskType.value !== 'all') params.set('riskType', riskType.value)
    if (assigneeId.value) params.set('assigneeId', assigneeId.value)
    if (reporterId.value) params.set('reporterId', reporterId.value)
    if (statusFilter.value) params.set('status', statusFilter.value)
    if (priorityFilter.value) params.set('priority', priorityFilter.value)
    if (fromDate.value) params.set('from', new Date(fromDate.value).toISOString())
    if (toDate.value) params.set('to', new Date(toDate.value).toISOString())

    const [gantt, attention] = await Promise.all([
      apiResult<any[]>(`/api/tasks/project/${props.projectId}/gantt`),
      apiResult<PagedResult<TaskAttentionDto>>(`/api/projects/${props.projectId}/task-attention?${params.toString()}`),
    ])
    tasks.value = gantt
    attentionItems.value = attention.items
  } finally {
    isLoading.value = false
  }
}

const filteredAttentionItems = computed(() => {
  return attentionItems.value
})

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

const summary = computed(() => ({
  total: attentionItems.value.length,
  overdue: attentionItems.value.filter((item) => item.isOverdue).length,
  staleTodo: attentionItems.value.filter((item) => item.isStaleTodo).length,
  unseen: attentionItems.value.filter((item) => item.isUnseenByAssignee).length,
}))

const minDate = computed(() => {
  const dates = tasks.value.filter(t => t.startDate).map(t => new Date(t.startDate).getTime())
  return dates.length ? new Date(Math.min(...dates)) : new Date()
})

const maxDate = computed(() => {
  const dates = tasks.value.filter(t => t.endDate).map(t => new Date(t.endDate).getTime())
  return dates.length ? new Date(Math.max(...dates)) : new Date(new Date().getTime() + 7 * 24 * 60 * 60 * 1000)
})

const totalDays = computed(() => {
  return Math.ceil((maxDate.value.getTime() - minDate.value.getTime()) / (24 * 60 * 60 * 1000)) + 5
})

function getTaskStyle(task: any) {
  if (!task.startDate || !task.endDate) return { display: 'none' }

  const start = new Date(task.startDate)
  const end = new Date(task.endDate)
  const left = Math.ceil((start.getTime() - minDate.value.getTime()) / (24 * 60 * 60 * 1000))
  const width = Math.ceil((end.getTime() - start.getTime()) / (24 * 60 * 60 * 1000))
  const risk = attentionItems.value.find((item) => item.id === task.id)

  return {
    gridColumnStart: left + 1,
    gridColumnEnd: `span ${Math.max(1, width)}`,
    backgroundColor: risk?.isOverdue ? '#dc2626' : risk?.isStaleTodo ? '#ea580c' : risk?.isDueSoon ? '#f59e0b' : task.isCriticalPath ? 'var(--peach-500)' : 'var(--primary-soft)',
    color: risk ? 'white' : task.isCriticalPath ? 'white' : 'var(--primary)'
  }
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

function formatDate(value: string | null) {
  if (!value) return 'Chưa đặt'
  return new Intl.DateTimeFormat('vi-VN', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  }).format(new Date(value))
}

onMounted(fetchGanttData)
</script>

<template>
  <div class="timeline-shell reveal">
    <section class="attention-panel glass-card">
      <div class="panel-header">
        <div class="title-group">
          <MessageSquareWarning :size="20" class="icon-primary" />
          <div>
            <h3>Công việc cần chú ý</h3>
            <p>Những task có dấu hiệu trễ hạn, chưa bắt đầu hoặc chưa được người nhận xem.</p>
          </div>
        </div>
        <div class="timeline-filters">
          <label class="risk-filter">
            <Filter :size="15" />
            <select v-model="riskType" @change="fetchGanttData">
              <option v-for="option in riskOptions" :key="option.value" :value="option.value">{{ option.label }}</option>
            </select>
          </label>
          <label class="risk-filter">
            <select v-model="assigneeId" @change="fetchGanttData">
              <option value="">Tất cả người phụ trách</option>
              <option v-for="member in projectMembers" :key="member.userId" :value="member.userId">{{ member.fullName }}</option>
            </select>
          </label>
          <label class="risk-filter">
            <select v-model="reporterId" @change="fetchGanttData">
              <option value="">Tất cả người giao</option>
              <option v-for="member in projectMembers" :key="member.userId" :value="member.userId">{{ member.fullName }}</option>
            </select>
          </label>
          <label class="risk-filter">
            <select v-model="statusFilter" @change="fetchGanttData">
              <option v-for="option in statusOptions" :key="option.value" :value="option.value">{{ option.label }}</option>
            </select>
          </label>
          <label class="risk-filter">
            <select v-model="priorityFilter" @change="fetchGanttData">
              <option v-for="option in priorityOptions" :key="option.value" :value="option.value">{{ option.label }}</option>
            </select>
          </label>
          <label class="risk-filter">
            <select v-model="sortMode" @change="fetchGanttData">
              <option v-for="option in sortOptions" :key="option.value" :value="option.value">{{ option.label }}</option>
            </select>
          </label>
          <input v-model="fromDate" class="date-filter" type="date" @change="fetchGanttData" />
          <input v-model="toDate" class="date-filter" type="date" @change="fetchGanttData" />
        </div>
      </div>

      <div class="attention-summary">
        <div class="summary-tile summary-tile--danger">
          <strong>{{ summary.overdue }}</strong>
          <span>Quá hạn</span>
        </div>
        <div class="summary-tile summary-tile--warning">
          <strong>{{ summary.staleTodo }}</strong>
          <span>Chưa bắt đầu</span>
        </div>
        <div class="summary-tile summary-tile--muted">
          <strong>{{ summary.unseen }}</strong>
          <span>Chưa xem</span>
        </div>
        <div class="summary-tile">
          <strong>{{ summary.total }}</strong>
          <span>Tổng cảnh báo</span>
        </div>
      </div>

      <div v-if="filteredAttentionItems.length" class="attention-table">
        <article v-for="item in filteredAttentionItems" :key="`${item.id}-${item.assigneeId ?? 'none'}`" class="attention-row">
          <div class="attention-main">
            <button type="button" class="task-link" @click="emit('openTask', item.id)">{{ item.title }}</button>
            <div class="reason-list">
              <span v-for="reason in item.reasons" :key="reason" class="reason-chip">{{ reasonLabel(reason) }}</span>
            </div>
          </div>
          <div class="attention-meta">
            <span>{{ item.assigneeName || 'Chưa giao' }}</span>
            <span>{{ item.status }}</span>
            <span>Hạn: {{ formatDate(item.dueDate) }}</span>
          </div>
          <div class="attention-actions">
            <button
              v-if="item.allowedActions.includes('BinhLuan')"
              type="button"
              class="ghost-button ghost-button--compact"
              @click="openComment(item)"
            >
              Bình luận
            </button>
            <button type="button" class="ghost-button ghost-button--compact" @click="emit('openTask', item.id)">Mở chi tiết</button>
            <button
              v-if="item.allowedActions.includes('NhacNguoiPhuTrach')"
              type="button"
              class="ghost-button ghost-button--compact"
              :disabled="nudgePendingKey === `${item.id}-${item.assigneeId ?? 'all'}`"
              @click="nudgeAssignee(item)"
            >
              <Bell :size="14" />
              {{ nudgePendingKey === `${item.id}-${item.assigneeId ?? 'all'}` ? 'Đang nhắc' : 'Nhắc' }}
            </button>
            <span v-if="item.isUnseenByAssignee" class="unseen-note"><EyeOff :size="14" /> Chưa xem</span>
          </div>
          <form v-if="commentTaskId === item.id" class="attention-comment" @submit.prevent="submitAttentionComment(item)">
            <input v-model="commentText" type="text" placeholder="Nhập lý do hoặc cập nhật tiến độ..." />
            <button class="ghost-button ghost-button--compact" type="submit">Gửi</button>
          </form>
        </article>
        <p v-if="nudgeFeedback" class="nudge-feedback">{{ nudgeFeedback }}</p>
        <p v-if="commentFeedback" class="nudge-feedback">{{ commentFeedback }}</p>
      </div>

      <div v-else class="empty-state">
        <AlertCircle :size="36" />
        <p>Không có công việc cần chú ý theo bộ lọc hiện tại.</p>
      </div>
    </section>

    <section class="gantt-chart glass-card">
      <div class="panel-header">
        <div class="title-group">
          <Calendar :size="20" class="icon-primary" />
          <h3>Timeline & Gantt</h3>
        </div>
      </div>

      <div v-if="isLoading" class="loading-state">Đang tải timeline...</div>

      <div v-else-if="tasks.length === 0" class="empty-state">
        <AlertCircle :size="48" />
        <p>Chưa có dữ liệu ngày tháng cho các task trong dự án này.</p>
      </div>

      <div v-else class="gantt-container no-scrollbar">
        <div class="gantt-grid" :style="{ gridTemplateColumns: `repeat(${totalDays}, 40px)` }">
          <div v-for="d in totalDays" :key="d" class="grid-header">
            {{ new Date(minDate.getTime() + (d-1) * 24 * 60 * 60 * 1000).getDate() }}
          </div>

          <template v-for="task in tasks" :key="task.id">
            <div class="task-label-row">
              <span :class="{ 'critical': task.isCriticalPath }">{{ task.title }}</span>
            </div>
            <div class="task-bar-row">
              <button v-if="task.startDate" type="button" class="task-bar" :style="getTaskStyle(task)" @click="emit('openTask', task.id)">
                <div class="progress-inner" :style="{ width: task.progress + '%' }"></div>
                <span class="bar-text">{{ task.progress }}%</span>
              </button>
            </div>
          </template>
        </div>
      </div>

      <div class="gantt-legend">
        <div class="legend-item"><span class="box normal"></span> Bình thường</div>
        <div class="legend-item"><span class="box due"></span> Sắp tới hạn</div>
        <div class="legend-item"><span class="box stale"></span> Chưa bắt đầu</div>
        <div class="legend-item"><span class="box overdue"></span> Quá hạn</div>
      </div>
    </section>
  </div>
</template>

<style scoped>
.timeline-shell { display: flex; flex-direction: column; gap: 20px; }
.attention-panel, .gantt-chart { padding: 24px; display: flex; flex-direction: column; gap: 20px; }
.panel-header { display: flex; justify-content: space-between; align-items: flex-start; gap: 16px; }
.title-group { display: flex; align-items: center; gap: 12px; }
.title-group h3 { margin: 0; font-size: 18px; color: var(--text); }
.title-group p { margin: 4px 0 0; color: var(--muted); font-size: 13px; }
.timeline-filters { display: flex; flex-wrap: wrap; justify-content: flex-end; gap: 8px; max-width: 760px; }
.risk-filter { display: inline-flex; align-items: center; gap: 8px; border: 1px solid var(--line); border-radius: 8px; padding: 8px 10px; color: var(--muted); }
.risk-filter select { border: 0; background: transparent; outline: none; font-weight: 700; color: var(--text); }
.date-filter { border: 1px solid var(--line); border-radius: 8px; padding: 8px 10px; color: var(--text); background: white; font-weight: 700; }
.attention-summary { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 12px; }
.summary-tile { border: 1px solid var(--line); border-radius: 8px; padding: 14px; background: white; display: flex; flex-direction: column; gap: 2px; }
.summary-tile strong { font-size: 24px; color: var(--text); }
.summary-tile span { color: var(--muted); font-size: 12px; font-weight: 700; }
.summary-tile--danger strong { color: #dc2626; }
.summary-tile--warning strong { color: #ea580c; }
.summary-tile--muted strong { color: #64748b; }
.attention-table { display: flex; flex-direction: column; gap: 10px; }
.attention-row { display: grid; grid-template-columns: 1.4fr 1fr auto; gap: 16px; align-items: center; border: 1px solid var(--line); border-radius: 8px; padding: 14px; background: white; }
.task-link { border: 0; background: transparent; color: var(--text); font-weight: 800; padding: 0; text-align: left; cursor: pointer; }
.task-link:hover { color: var(--primary); }
.reason-list { display: flex; flex-wrap: wrap; gap: 6px; margin-top: 8px; }
.reason-chip { background: #fee2e2; color: #b91c1c; border-radius: 6px; padding: 3px 7px; font-size: 11px; font-weight: 800; }
.attention-meta { display: flex; flex-direction: column; gap: 4px; color: var(--muted); font-size: 12px; }
.attention-actions { display: flex; align-items: center; gap: 8px; flex-wrap: wrap; justify-content: flex-end; }
.ghost-button--compact { padding: 6px 10px; font-size: 12px; display: inline-flex; align-items: center; gap: 5px; }
.ghost-button--compact:disabled { opacity: 0.55; cursor: wait; }
.unseen-note { display: inline-flex; align-items: center; gap: 4px; color: #ea580c; font-size: 12px; font-weight: 800; }
.nudge-feedback { margin: 2px 0 0; color: var(--muted); font-size: 12px; font-weight: 700; }
.attention-comment { grid-column: 1 / -1; display: flex; gap: 8px; }
.attention-comment input { flex: 1; border: 1px solid var(--line); border-radius: 8px; padding: 8px 10px; outline: none; }
.gantt-container { overflow-x: auto; padding-bottom: 16px; border: 1px solid var(--line); border-radius: 8px; background: white; }
.gantt-grid { display: grid; position: relative; }
.grid-header { height: 40px; display: flex; align-items: center; justify-content: center; font-size: 11px; font-weight: 700; color: var(--muted); border-right: 1px solid var(--line-light); border-bottom: 2px solid var(--line); background: var(--bg-soft); }
.task-label-row { grid-column: 1 / -1; padding: 12px 16px 4px; font-size: 12px; font-weight: 700; background: #fafafa; }
.task-label-row span.critical { color: var(--peach-500); }
.task-bar-row { grid-column: 1 / -1; height: 32px; position: relative; margin-bottom: 8px; border-bottom: 1px solid var(--line-light); }
.task-bar { position: relative; height: 24px; top: 4px; border-radius: 4px; border: 0; display: flex; align-items: center; padding: 0 8px; font-size: 10px; font-weight: 800; box-shadow: 0 2px 4px rgba(0,0,0,0.05); z-index: 2; cursor: pointer; }
.progress-inner { position: absolute; left: 0; top: 0; bottom: 0; background: rgba(255,255,255,0.3); border-radius: 4px 0 0 4px; }
.bar-text { position: relative; z-index: 3; }
.gantt-legend { display: flex; gap: 20px; font-size: 12px; color: var(--muted); margin-top: 12px; flex-wrap: wrap; }
.legend-item { display: flex; align-items: center; gap: 8px; }
.box { width: 12px; height: 12px; border-radius: 3px; }
.box.normal { background: var(--primary-soft); }
.box.due { background: #f59e0b; }
.box.stale { background: #ea580c; }
.box.overdue { background: #dc2626; }
.loading-state, .empty-state { min-height: 160px; display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 12px; color: var(--muted); text-align: center; }
@media (max-width: 900px) {
  .attention-summary { grid-template-columns: repeat(2, minmax(0, 1fr)); }
  .attention-row { grid-template-columns: 1fr; }
  .attention-actions { justify-content: flex-start; }
}
</style>
