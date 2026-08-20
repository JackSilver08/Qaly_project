<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import {
  AlertTriangle,
  CalendarClock,
  CheckCircle2,
  CircleAlert,
  Gauge,
  Link2,
  Loader2,
  PencilLine,
  Plus,
  RefreshCw,
  Save,
  ShieldCheck,
  Sparkles,
  Trash2,
  UserRound,
  X,
} from 'lucide-vue-next'
import { showError, showSuccess } from '../composables/use-toast'
import { apiResult, errorMessage } from '../utils/api-client'
import type {
  DashboardTask,
  MemberAvailabilityWindowDto,
  MemberWorkloadDto,
  PortfolioCapacityDto,
  PortfolioMemberCapacityDto,
  PortfolioScheduleProposalDto,
  PortfolioScheduleProposalItemDto,
  ProjectWorkloadDto,
} from '../types'

const props = defineProps<{
  projectId: string
  tasks?: DashboardTask[]
  compact?: boolean
  initialTaskId?: string | null
}>()
const emit = defineEmits<{ applied: [] }>()

const today = new Date()
const defaultEnd = new Date(today)
defaultEnd.setDate(defaultEnd.getDate() + 14)

const isLoading = ref(true)
const portfolioLoading = ref(false)
const proposalBusy = ref(false)
const workload = ref<ProjectWorkloadDto | null>(null)
const portfolio = ref<PortfolioCapacityDto | null>(null)
const portfolioError = ref('')
const windowStart = ref(toDateInput(today))
const windowEnd = ref(toDateInput(defaultEnd))
const selectedTaskIds = ref<string[]>([])
const proposal = ref<PortfolioScheduleProposalDto | null>(null)
const editingMember = ref<PortfolioMemberCapacityDto | null>(null)
const capacityHours = ref(40)
const availabilityWindows = ref<MemberAvailabilityWindowDto[]>([])

const openTasks = computed(() => (props.tasks ?? [])
  .filter(task => !['done', 'completed', 'cancelled', 'canceled'].includes(task.status.toLowerCase()))
  .filter(task => !props.compact || !props.initialTaskId || task.id === props.initialTaskId))
const members = computed(() => workload.value?.membersWorkload ?? [])
const totalTasks = computed(() => members.value.reduce((sum, item) => sum + item.taskCount, 0))
const totalEstimated = computed(() => members.value.reduce((sum, item) => sum + item.estimatedHours, 0))
const totalActual = computed(() => members.value.reduce((sum, item) => sum + item.actualHours, 0))
const sourceByKey = computed(() => new Map((proposal.value?.sources ?? []).map(source => [source.key, source])))

async function loadWorkload() {
  isLoading.value = true
  try {
    workload.value = await apiResult<ProjectWorkloadDto>(`/api/projects/${props.projectId}/workload`)
  } catch (error) {
    workload.value = null
    showError(errorMessage(error, 'Không thể tải workload của dự án.'))
  } finally {
    isLoading.value = false
  }
}

async function loadPortfolio() {
  portfolioLoading.value = true
  portfolioError.value = ''
  try {
    portfolio.value = await apiResult<PortfolioCapacityDto>(
      `/api/projects/${props.projectId}/portfolio-capacity?from=${encodeURIComponent(toIso(windowStart.value))}&to=${encodeURIComponent(toIso(windowEnd.value, true))}`,
    )
    await restoreProposal()
  } catch (error) {
    portfolio.value = null
    portfolioError.value = errorMessage(error, 'Bạn cần quyền quản lý Organization để xem workload đa dự án.')
  } finally {
    portfolioLoading.value = false
  }
}

async function restoreProposal() {
  const draftId = localStorage.getItem(proposalStorageKey())
  if (!draftId) return
  try {
    proposal.value = await apiResult<PortfolioScheduleProposalDto>(`/api/projects/${props.projectId}/schedule-proposals/${draftId}`)
  } catch {
    localStorage.removeItem(proposalStorageKey())
    proposal.value = null
  }
}

async function createProposal() {
  if (!selectedTaskIds.value.length || proposalBusy.value) return
  proposalBusy.value = true
  try {
    proposal.value = await apiResult<PortfolioScheduleProposalDto>(`/api/projects/${props.projectId}/schedule-proposals`, {
      method: 'POST',
      headers: { 'Idempotency-Key': `portfolio-create:${props.projectId}:${selectedTaskIds.value.slice().sort().join(',')}:${windowStart.value}:${windowEnd.value}` },
      body: JSON.stringify({
        taskIds: selectedTaskIds.value,
        windowStart: toIso(windowStart.value),
        windowEnd: toIso(windowEnd.value, true),
      }),
    })
    localStorage.setItem(proposalStorageKey(), proposal.value.draftId)
    showSuccess('Đã lập bản nháp phân công và lịch đa dự án. Chưa có dữ liệu nào bị sửa.')
  } catch (error) {
    showError(errorMessage(error, 'Không thể lập schedule proposal.'))
  } finally {
    proposalBusy.value = false
  }
}

async function saveProposal() {
  if (!proposal.value || proposal.value.status !== 'pending_review' || proposalBusy.value) return
  proposalBusy.value = true
  try {
    proposal.value = await apiResult<PortfolioScheduleProposalDto>(
      `/api/projects/${props.projectId}/schedule-proposals/${proposal.value.draftId}`,
      {
        method: 'PATCH',
        body: JSON.stringify({ items: proposal.value.items, rowVersion: proposal.value.rowVersion }),
      },
    )
    showSuccess('Đã lưu bản nháp để có thể tiếp tục sau khi reload.')
  } catch (error) {
    showError(errorMessage(error, 'Không thể lưu bản nháp; hãy tải lại để tránh ghi đè thay đổi mới hơn.'))
  } finally {
    proposalBusy.value = false
  }
}

async function confirmProposal() {
  if (!proposal.value || proposalBusy.value) return
  const selectedIds = proposal.value.items.filter(item => item.selected).map(item => item.itemId)
  if (!selectedIds.length) {
    showError('Hãy chọn ít nhất một thay đổi cần áp dụng.')
    return
  }
  proposalBusy.value = true
  try {
    const key = `portfolio-confirm:${proposal.value.draftId}`
    proposal.value = await apiResult<PortfolioScheduleProposalDto>(
      `/api/projects/${props.projectId}/schedule-proposals/${proposal.value.draftId}/confirm`,
      {
        method: 'POST',
        headers: { 'Idempotency-Key': key },
        body: JSON.stringify({
          selectedItemIds: selectedIds,
          rowVersion: proposal.value.rowVersion,
          idempotencyKey: key,
          confirmed: true,
        }),
      },
    )
    if (!proposal.value.receipt?.readBackVerified)
      throw new Error('Máy chủ chưa đọc lại và xác minh Task sau khi ghi.')
    showSuccess(`Đã áp dụng và đọc lại ${proposal.value.receipt.appliedCount} thay đổi được chọn.`)
    emit('applied')
    await Promise.all([loadWorkload(), loadPortfolio()])
  } catch (error) {
    showError(errorMessage(error, 'Không thể xác nhận. Task có thể đã thay đổi; chưa có proposal nào được áp dụng dở dang.'))
  } finally {
    proposalBusy.value = false
  }
}

async function rejectProposal() {
  if (!proposal.value || proposalBusy.value) return
  proposalBusy.value = true
  try {
    const key = crypto.randomUUID()
    proposal.value = await apiResult<PortfolioScheduleProposalDto>(
      `/api/projects/${props.projectId}/schedule-proposals/${proposal.value.draftId}/reject`,
      {
        method: 'POST',
        headers: { 'Idempotency-Key': key },
        body: JSON.stringify({ reason: 'Người quản lý từ chối bản nháp.', rowVersion: proposal.value.rowVersion, idempotencyKey: key }),
      },
    )
    showSuccess('Đã từ chối bản nháp; không có task nào bị thay đổi.')
  } catch (error) {
    showError(errorMessage(error, 'Không thể từ chối bản nháp.'))
  } finally {
    proposalBusy.value = false
  }
}

function openCapacityEditor(member: PortfolioMemberCapacityDto) {
  editingMember.value = member
  capacityHours.value = member.weeklyCapacityHours
  availabilityWindows.value = member.availabilityWindows.map(item => ({ ...item }))
}

function addAvailabilityWindow() {
  const starts = new Date()
  starts.setHours(9, 0, 0, 0)
  const ends = new Date(starts)
  ends.setDate(ends.getDate() + 1)
  availabilityWindows.value.push({
    id: null,
    startsAt: starts.toISOString(),
    endsAt: ends.toISOString(),
    kind: 'Unavailable',
    availableHours: null,
    rowVersion: null,
  })
}

async function saveCapacity() {
  if (!editingMember.value || !portfolio.value || proposalBusy.value) return
  proposalBusy.value = true
  try {
    await apiResult<PortfolioMemberCapacityDto>(
      `/api/organizations/${portfolio.value.organizationId}/members/${editingMember.value.userId}/capacity`,
      {
        method: 'PUT',
        body: JSON.stringify({
          weeklyCapacityHours: capacityHours.value,
          timeZoneId: Intl.DateTimeFormat().resolvedOptions().timeZone || 'Asia/Ho_Chi_Minh',
          availabilityWindows: availabilityWindows.value.map(item => ({
            ...item,
            startsAt: new Date(item.startsAt).toISOString(),
            endsAt: new Date(item.endsAt).toISOString(),
          })),
          rowVersion: editingMember.value.profileRowVersion,
          confirmed: true,
        }),
      },
    )
    editingMember.value = null
    showSuccess('Đã cập nhật capacity và khoảng vắng mặt.')
    await loadPortfolio()
  } catch (error) {
    showError(errorMessage(error, 'Không thể cập nhật capacity.'))
  } finally {
    proposalBusy.value = false
  }
}

function selectAllOpenTasks() {
  selectedTaskIds.value = selectedTaskIds.value.length === openTasks.value.length ? [] : openTasks.value.map(task => task.id)
}

function memberWorkloadScore(member: MemberWorkloadDto) {
  return member.taskCount + member.estimatedHours / 8 + member.actualHours / 10
}

function loadPercent(member: MemberWorkloadDto) {
  const max = Math.max(1, ...members.value.map(item => memberWorkloadScore(item)))
  return Math.round((memberWorkloadScore(member) / max) * 100)
}

function statusLabel(member: MemberWorkloadDto) {
  if (member.taskCount === 0) return 'Nhàn'
  if (loadPercent(member) >= 85) return 'Tải cao trong dự án'
  return 'Đang có việc'
}

function utilizationTone(member: PortfolioMemberCapacityDto) {
  if (member.remainingHours < 0 || member.utilizationPercent > 100) return 'danger'
  if (member.utilizationPercent >= 80) return 'warning'
  return 'healthy'
}

function optionMembers(item: PortfolioScheduleProposalItemDto) {
  return [{ userId: item.proposedAssigneeId, fullName: item.proposedAssigneeName }, ...item.alternatives]
    .filter((candidate, index, all) => all.findIndex(other => other.userId === candidate.userId) === index)
}

function setProposedAssignee(item: PortfolioScheduleProposalItemDto, userId: string) {
  const option = optionMembers(item).find(candidate => candidate.userId === userId)
  item.proposedAssigneeId = userId
  item.proposedAssigneeName = option?.fullName ?? item.proposedAssigneeName
  const alternative = item.alternatives.find(candidate => candidate.userId === userId)
  if (alternative) {
    const estimate = Math.max(0, item.loadAfterHours - item.loadBeforeHours)
    item.skillCoveragePercent = alternative.skillCoveragePercent
    item.evidenceConfidence = alternative.evidenceConfidence
    item.loadBeforeHours = alternative.loadBeforeHours
    item.loadAfterHours = alternative.loadBeforeHours + estimate
    item.capacityHours = alternative.capacityHours
  }
}

function taskDate(value: string) {
  return value.slice(0, 10)
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat('vi-VN').format(new Date(value))
}

function setTaskDate(item: PortfolioScheduleProposalItemDto, field: 'proposedStart' | 'proposedDue', value: string) {
  item[field] = toIso(value, field === 'proposedDue')
}

function windowDateTime(value: string) {
  const date = new Date(value)
  const offset = date.getTimezoneOffset()
  return new Date(date.getTime() - offset * 60_000).toISOString().slice(0, 16)
}

function updateWindowDate(index: number, field: 'startsAt' | 'endsAt', value: string) {
  availabilityWindows.value[index][field] = new Date(value).toISOString()
}

function toDateInput(value: Date) {
  const offset = value.getTimezoneOffset()
  return new Date(value.getTime() - offset * 60_000).toISOString().slice(0, 10)
}

function toIso(value: string, endOfDay = false) {
  const date = new Date(`${value}T${endOfDay ? '23:59:59' : '00:00:00'}`)
  return date.toISOString()
}

function proposalStorageKey() {
  return `qaly:portfolio-proposal:${props.projectId}:${props.initialTaskId ?? 'all'}`
}

watch(
  () => [props.projectId, props.initialTaskId],
  async () => {
    selectedTaskIds.value = props.initialTaskId ? [props.initialTaskId] : []
    proposal.value = null
    await Promise.all([loadWorkload(), loadPortfolio()])
  },
  { immediate: true },
)
</script>

<template>
  <section class="workload-tab glass-card reveal" :class="{ 'workload-tab--compact': compact }" data-testid="portfolio-capacity-copilot">
    <div class="workload-hero">
      <div>
        <div class="eyebrow"><Gauge :size="14" /> Capacity & Schedule Copilot</div>
        <h2>{{ compact ? 'Phương án tự giao có kiểm soát' : 'Phân bổ nguồn lực đa dự án' }}</h2>
        <p>Đối soát capacity, lịch vắng mặt, skill evidence và tải công việc trước khi tạo bản nháp phân công. Không tự sửa task.</p>
      </div>
      <div class="window-controls">
        <label>Từ ngày <input v-model="windowStart" type="date" /></label>
        <label>Đến ngày <input v-model="windowEnd" type="date" /></label>
        <button type="button" class="ghost-button" :disabled="portfolioLoading" @click="loadPortfolio">
          <RefreshCw :size="15" :class="{ spin: portfolioLoading }" /> Đối soát
        </button>
      </div>
    </div>

    <div v-if="isLoading" class="workload-empty"><Loader2 :size="20" class="spin" /><strong>Đang tải workload dự án</strong></div>
    <div v-else-if="!compact" class="workload-stats">
      <article><span>Task trong dự án</span><strong>{{ totalTasks }}</strong></article>
      <article><span>Giờ ước lượng</span><strong>{{ totalEstimated }}h</strong></article>
      <article><span>Giờ thực tế</span><strong>{{ totalActual }}h</strong></article>
    </div>

    <div v-if="portfolioLoading" class="workload-empty"><Loader2 :size="20" class="spin" /><strong>Đang đối soát portfolio được cấp quyền</strong></div>
    <div v-else-if="portfolioError" class="portfolio-notice" role="status">
      <ShieldCheck :size="20" />
      <div><strong>Portfolio scope chưa khả dụng</strong><p>{{ portfolioError }}</p></div>
    </div>

    <template v-else-if="portfolio">
      <div class="truth-strip">
        <span><ShieldCheck :size="15" /> {{ portfolio.visibilityState === 'partial_private_aggregate' ? 'Có tải riêng tư được tổng hợp và ẩn nguồn' : 'Đủ nguồn trong Organization được cấp quyền' }}</span>
        <span>Scoring: {{ portfolio.scoringVersion }}</span>
        <span>Không dùng LLM cho hard constraint</span>
      </div>

      <div v-if="!compact" class="capacity-grid">
        <article v-for="member in portfolio.members" :key="member.userId" class="capacity-card" :data-tone="utilizationTone(member)">
          <header>
            <div class="capacity-person"><UserRound :size="18" /><div><strong>{{ member.fullName }}</strong><span>{{ member.capacityState === 'assumed_default' ? 'Đang dùng mặc định 40h/tuần' : 'Capacity đã khai báo' }}</span></div></div>
            <button type="button" class="icon-button" aria-label="Sửa capacity" @click="openCapacityEditor(member)"><PencilLine :size="15" /></button>
          </header>
          <div class="capacity-meter"><span :style="{ width: `${Math.min(100, member.utilizationPercent)}%` }"></span></div>
          <div class="capacity-numbers">
            <strong>{{ member.assignedHours }}h / {{ member.windowCapacityHours }}h</strong>
            <span>{{ member.remainingHours >= 0 ? `Còn ${member.remainingHours}h` : `Quá tải ${Math.abs(member.remainingHours)}h` }}</span>
          </div>
          <div class="capacity-signals">
            <span>{{ member.openTaskCount }} task mở</span>
            <span v-if="member.missingEstimateCount">{{ member.missingEstimateCount }} thiếu estimate</span>
            <span v-if="member.deadlineCollisionCount">{{ member.deadlineCollisionCount }} deadline va chạm</span>
            <span v-if="member.hasRestrictedLoad">Có tải hạn chế</span>
          </div>
          <details v-if="member.projectLoads.length">
            <summary>Tải theo dự án</summary>
            <div v-for="load in member.projectLoads" :key="load.projectId" class="project-load-row">
              <span>{{ load.projectName }}{{ load.sourcesRestricted ? ' · nguồn ẩn' : '' }}</span><strong>{{ load.assignedHours }}h</strong>
            </div>
          </details>
        </article>
      </div>

      <section class="proposal-builder" data-testid="portfolio-schedule-proposal">
        <div class="section-heading">
          <div><span class="eyebrow"><Sparkles :size="14" /> Bản nháp có kiểm soát</span><h3>Đề xuất người phụ trách và lịch</h3></div>
          <span class="honest-label">Chỉ áp dụng sau xác nhận</span>
        </div>
        <div v-if="!openTasks.length" class="workload-empty"><CircleAlert :size="20" /><strong>Không có task mở trong Project hiện tại</strong></div>
        <template v-else>
          <div class="task-selector">
            <button type="button" class="text-button" @click="selectAllOpenTasks">{{ selectedTaskIds.length === openTasks.length ? 'Bỏ chọn tất cả' : 'Chọn tất cả task mở' }}</button>
            <label v-for="task in openTasks" :key="task.id" class="task-option">
              <input v-model="selectedTaskIds" type="checkbox" :value="task.id" />
              <span><strong>{{ task.title }}</strong><small>{{ task.priority }} · {{ task.dueDate ? `hạn ${formatDate(task.dueDate)}` : 'chưa có hạn' }}</small></span>
            </label>
          </div>
          <button type="button" class="primary-button" :disabled="!selectedTaskIds.length || proposalBusy" @click="createProposal">
            <Loader2 v-if="proposalBusy" :size="16" class="spin" /><Sparkles v-else :size="16" /> Lập phương án
          </button>
        </template>

        <div v-if="proposal" class="proposal-review">
          <div class="proposal-meta">
            <span>{{ proposal.schemaId }}</span><span>{{ proposal.providerName }} · {{ proposal.modelName }}</span><strong>{{ proposal.status }}</strong>
          </div>
          <div v-for="warning in proposal.warnings" :key="warning" class="warning-row"><AlertTriangle :size="15" />{{ warning }}</div>

          <article v-for="item in proposal.items" :key="item.itemId" class="proposal-item" :class="{ muted: !item.selected }">
            <header>
              <label><input v-model="item.selected" type="checkbox" :disabled="proposal.status !== 'pending_review'" /><strong>{{ item.taskTitle }}</strong></label>
              <span>{{ item.skillCoveragePercent }}% skill · {{ Math.round(item.evidenceConfidence * 100) }}% confidence</span>
            </header>
            <div class="proposal-fields">
              <label>Người phụ trách
                <select :value="item.proposedAssigneeId" :disabled="proposal.status !== 'pending_review'" @change="setProposedAssignee(item, ($event.target as HTMLSelectElement).value)">
                  <option v-for="candidate in optionMembers(item)" :key="candidate.userId" :value="candidate.userId">{{ candidate.fullName }}</option>
                </select>
              </label>
              <label>Bắt đầu <input type="date" :value="taskDate(item.proposedStart)" :disabled="proposal.status !== 'pending_review'" @input="setTaskDate(item, 'proposedStart', ($event.target as HTMLInputElement).value)" /></label>
              <label>Hạn <input type="date" :value="taskDate(item.proposedDue)" :disabled="proposal.status !== 'pending_review'" @input="setTaskDate(item, 'proposedDue', ($event.target as HTMLInputElement).value)" /></label>
            </div>
            <div class="before-after"><span>Tải trước {{ item.loadBeforeHours }}h</span><strong>→ {{ item.loadAfterHours }}h / {{ item.capacityHours }}h</strong></div>
            <div v-if="item.deadlineRisks.length || item.dependencyConflicts.length" class="risk-list">
              <span v-for="risk in [...item.deadlineRisks, ...item.dependencyConflicts]" :key="risk"><AlertTriangle :size="13" />{{ risk }}</span>
            </div>
            <details v-if="item.alternatives.length"><summary>Phương án khác</summary><p v-for="alternative in item.alternatives" :key="alternative.userId"><strong>{{ alternative.fullName }}</strong> — {{ alternative.tradeOff }}</p></details>
            <div class="source-links">
              <template v-for="key in item.sourceRefs" :key="key">
                <a v-if="sourceByKey.get(key)?.url" :href="sourceByKey.get(key)?.url ?? '#'" target="_blank"><Link2 :size="12" />{{ sourceByKey.get(key)?.label }}</a>
                <span v-else><ShieldCheck :size="12" />{{ sourceByKey.get(key)?.label ?? key }}</span>
              </template>
            </div>
          </article>

          <div v-if="proposal.status === 'pending_review'" class="proposal-actions">
            <button type="button" class="ghost-button" :disabled="proposalBusy" @click="rejectProposal"><X :size="15" />Từ chối</button>
            <button type="button" class="ghost-button" :disabled="proposalBusy" @click="saveProposal"><Save :size="15" />Lưu bản nháp</button>
            <button type="button" class="primary-button" :disabled="proposalBusy" @click="confirmProposal"><CheckCircle2 :size="16" />Xác nhận phần đã chọn</button>
          </div>
          <div v-else-if="proposal.receipt" class="receipt"><CheckCircle2 :size="18" /><div><strong>Đã áp dụng {{ proposal.receipt.appliedCount }} thay đổi</strong><a v-for="link in proposal.receipt.readBackLinks" :key="link" :href="link">Mở task đã cập nhật</a></div></div>
        </div>
      </section>
    </template>

    <div v-if="editingMember" class="modal-backdrop" @click.self="editingMember = null">
      <section class="capacity-modal" role="dialog" aria-modal="true" aria-label="Cập nhật capacity">
        <header><div><span class="eyebrow">Capacity khai báo</span><h3>{{ editingMember.fullName }}</h3></div><button type="button" class="icon-button" @click="editingMember = null"><X :size="18" /></button></header>
        <label>Giờ làm việc mỗi tuần <input v-model.number="capacityHours" type="number" min="1" max="168" step="0.5" /></label>
        <div class="availability-heading"><strong>Khoảng không sẵn sàng / giảm capacity</strong><button type="button" class="text-button" @click="addAvailabilityWindow"><Plus :size="14" />Thêm khoảng</button></div>
        <article v-for="(item, index) in availabilityWindows" :key="item.id ?? index" class="availability-row">
          <select v-model="item.kind"><option value="Unavailable">Không sẵn sàng</option><option value="ReducedCapacity">Giảm capacity</option></select>
          <input type="datetime-local" :value="windowDateTime(item.startsAt)" @input="updateWindowDate(index, 'startsAt', ($event.target as HTMLInputElement).value)" />
          <input type="datetime-local" :value="windowDateTime(item.endsAt)" @input="updateWindowDate(index, 'endsAt', ($event.target as HTMLInputElement).value)" />
          <input v-if="item.kind === 'ReducedCapacity'" v-model.number="item.availableHours" type="number" min="0" placeholder="Giờ còn lại" />
          <button type="button" class="icon-button danger" @click="availabilityWindows.splice(index, 1)"><Trash2 :size="15" /></button>
        </article>
        <p class="modal-note">Thông tin này chỉ dùng cho capacity. Không nhập lý do nghỉ hoặc dữ liệu nhạy cảm.</p>
        <footer><button type="button" class="ghost-button" @click="editingMember = null">Hủy</button><button type="button" class="primary-button" :disabled="proposalBusy" @click="saveCapacity"><Save :size="15" />Xác nhận lưu</button></footer>
      </section>
    </div>
  </section>
</template>

<style scoped>
.workload-tab{padding:24px;display:grid;gap:20px;border:1px solid var(--line);border-radius:var(--radius-shell);background:var(--panel)}
.workload-tab--compact{padding:14px;margin-top:12px;border-radius:14px}.workload-tab--compact .workload-hero{display:grid}.workload-tab--compact .workload-hero h2{font-size:18px}.workload-tab--compact .window-controls{width:100%}.workload-tab--compact .proposal-builder{padding-top:12px}.workload-tab--compact .task-selector{grid-template-columns:1fr}
.workload-hero,.section-heading,.proposal-actions,.capacity-modal header,.capacity-modal footer{display:flex;justify-content:space-between;gap:16px;align-items:flex-start}
.eyebrow{display:inline-flex;align-items:center;gap:7px;color:var(--primary);font-size:11px;font-weight:900;letter-spacing:.08em;text-transform:uppercase}
h2,h3,p{margin:0}.workload-hero h2{margin:8px 0 6px;color:var(--text-strong);font-size:24px}.workload-hero p{max-width:72ch;color:var(--muted)}
.window-controls{display:flex;align-items:end;gap:8px;flex-wrap:wrap}.window-controls label,.proposal-fields label,.capacity-modal>label{display:grid;gap:5px;color:var(--muted);font-size:11px;font-weight:800}
input,select{min-height:38px;padding:8px 10px;border:1px solid var(--line);border-radius:10px;background:var(--panel);color:var(--text-strong)}
.ghost-button,.primary-button,.text-button,.icon-button{display:inline-flex;align-items:center;justify-content:center;gap:7px;border:0;cursor:pointer;font:inherit;font-weight:800}.ghost-button{min-height:38px;padding:0 13px;border:1px solid var(--line);border-radius:10px;background:var(--bg-soft);color:var(--text-strong)}.primary-button{min-height:40px;padding:0 16px;border-radius:10px;background:var(--primary);color:#fff}.text-button{padding:5px;background:transparent;color:var(--primary)}.icon-button{width:34px;height:34px;border-radius:9px;background:var(--bg-soft);color:var(--text-strong)}button:disabled{opacity:.55;cursor:not-allowed}
.workload-stats{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:10px}.workload-stats article{padding:14px;border:1px solid var(--line);border-radius:12px;background:var(--bg-soft)}.workload-stats span{display:block;color:var(--muted);font-size:11px;font-weight:700}.workload-stats strong{color:var(--text-strong);font-size:20px}
.workload-empty{min-height:110px;display:grid;place-items:center;gap:8px;padding:18px;border:1px dashed var(--line);border-radius:12px;color:var(--muted);text-align:center;background:var(--bg-soft)}
.portfolio-notice,.truth-strip,.warning-row,.receipt{display:flex;align-items:center;gap:10px;padding:12px;border:1px solid var(--line);border-radius:12px;background:var(--bg-soft)}.portfolio-notice p{margin-top:4px;color:var(--muted)}.truth-strip{flex-wrap:wrap;justify-content:space-between;font-size:12px;color:var(--muted)}.truth-strip span{display:inline-flex;align-items:center;gap:5px}
.capacity-grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:12px}.capacity-card{padding:16px;border:1px solid var(--line);border-radius:14px;background:var(--panel)}.capacity-card[data-tone=warning]{border-color:#f59e0b}.capacity-card[data-tone=danger]{border-color:#ef4444}.capacity-card header,.capacity-person,.capacity-numbers,.capacity-signals,.project-load-row{display:flex;align-items:center;justify-content:space-between;gap:10px}.capacity-person{justify-content:flex-start}.capacity-person div{display:grid;gap:2px}.capacity-person span,.capacity-signals,.project-load-row{font-size:11px;color:var(--muted)}.capacity-meter{height:8px;margin:14px 0 9px;border-radius:99px;overflow:hidden;background:var(--bg-soft)}.capacity-meter span{display:block;height:100%;background:var(--primary)}.capacity-card[data-tone=warning] .capacity-meter span{background:#f59e0b}.capacity-card[data-tone=danger] .capacity-meter span{background:#ef4444}.capacity-signals{justify-content:flex-start;flex-wrap:wrap;margin-top:10px}.capacity-signals span{padding:4px 7px;border-radius:99px;background:var(--bg-soft)}details{margin-top:10px}summary{cursor:pointer;color:var(--primary);font-size:12px;font-weight:800}.project-load-row{padding-top:7px}
.proposal-builder{display:grid;gap:14px;padding-top:20px;border-top:1px solid var(--line)}.section-heading h3{margin-top:6px;font-size:20px}.honest-label{padding:6px 9px;border-radius:99px;background:#ecfdf5;color:#047857;font-size:11px;font-weight:900}.task-selector{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:8px}.task-selector>.text-button{grid-column:1/-1;justify-self:start}.task-option{display:flex;align-items:center;gap:9px;padding:10px;border:1px solid var(--line);border-radius:10px}.task-option span{display:grid}.task-option small{color:var(--muted)}
.proposal-review{display:grid;gap:10px}.proposal-meta{display:flex;flex-wrap:wrap;gap:8px}.proposal-meta>*{padding:5px 8px;border-radius:99px;background:var(--bg-soft);font-size:11px}.warning-row{border-color:#f59e0b;color:#92400e}.proposal-item{display:grid;gap:11px;padding:15px;border:1px solid var(--line);border-radius:14px;background:var(--panel)}.proposal-item.muted{opacity:.58}.proposal-item header,.proposal-item header label,.before-after,.risk-list span,.source-links a,.source-links span{display:flex;align-items:center;gap:8px}.proposal-item header{justify-content:space-between}.proposal-item header>span{color:var(--muted);font-size:11px}.proposal-fields{display:grid;grid-template-columns:2fr 1fr 1fr;gap:9px}.before-after{justify-content:flex-end;color:var(--muted)}.risk-list{display:flex;flex-wrap:wrap;gap:6px}.risk-list span{padding:5px 7px;border-radius:8px;background:#fff7ed;color:#9a3412;font-size:11px}.source-links{display:flex;flex-wrap:wrap;gap:6px}.source-links a,.source-links span{padding:5px 7px;border-radius:8px;background:var(--bg-soft);color:var(--primary);font-size:11px;text-decoration:none}.proposal-actions{justify-content:flex-end}.receipt{border-color:#10b981;background:#ecfdf5;color:#065f46}.receipt div{display:grid;gap:4px}.receipt a{color:#047857}
.modal-backdrop{position:fixed;inset:0;z-index:1000;display:grid;place-items:center;padding:20px;background:rgba(15,23,42,.45)}.capacity-modal{width:min(760px,100%);max-height:85vh;overflow:auto;display:grid;gap:15px;padding:20px;border:1px solid var(--line);border-radius:16px;background:var(--panel);box-shadow:0 24px 70px rgba(15,23,42,.28)}.availability-heading{display:flex;justify-content:space-between;align-items:center}.availability-row{display:grid;grid-template-columns:1fr 1.4fr 1.4fr 1fr auto;gap:8px;align-items:center}.modal-note{color:var(--muted);font-size:12px}.danger{color:#dc2626}
.spin{animation:spin 1s linear infinite}@keyframes spin{to{transform:rotate(360deg)}}
:global(:root[data-theme='dark'] .honest-label),:global(:root[data-theme='dark'] .receipt){background:rgba(16,185,129,.13)}:global(:root[data-theme='dark'] .warning-row),:global(:root[data-theme='dark'] .risk-list span){background:rgba(245,158,11,.12);color:#fbbf24}
@media(max-width:900px){.workload-hero,.section-heading{flex-direction:column}.workload-stats,.capacity-grid,.task-selector,.proposal-fields{grid-template-columns:1fr}.availability-row{grid-template-columns:1fr}.window-controls{width:100%}.proposal-actions{flex-wrap:wrap}}
</style>
