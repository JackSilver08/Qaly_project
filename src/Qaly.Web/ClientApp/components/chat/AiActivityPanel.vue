<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { AlertTriangle, Ban, Check, ChevronRight, Clock3, FileCheck2, LoaderCircle, RefreshCw, RotateCcw, Save, X } from 'lucide-vue-next'
import { useDashboardContext } from '../../composables/dashboard-context'
import { apiResult, errorMessage } from '../../utils/api-client'

type JobSummary = {
  jobId: string; jobType: string; projectId: string | null; status: string; progressPercent: number
  attemptCount: number; maxAttempts: number; createdAt: string; lastErrorCode: string | null
  isMock: boolean; draftIds: string[]
}
type JobResult = { jobId: string; schemaId: string; schemaVersion: string; result: unknown; isMock: boolean; mockReason: string | null }
type DraftSummary = {
  draftId: string; aiJobId: string; projectId: string; draftType: string; status: string
  confidence: number | null; createdAt: string; expiresAt: string | null
}
type DraftDetail = {
  draftId: string; aiJobId: string; projectId: string; draftType: string; status: string
  originalPayload: unknown; workingPayload: unknown; warnings: unknown; schemaId: string | null
  confidence: number | null; rowVersion: string
}
type PlatformHealth = {
  status: string; platformEnabled: boolean; workerEnabled: boolean; queueDepth: number
  runningCount: number; retryCount: number; failedLast24Hours: number; expiredLeaseCount: number
  oldestQueuedAgeSeconds: number | null; degradedReason: string | null
}

const { selectedProject } = useDashboardContext()
const route = useRoute()
const router = useRouter()
const activeTab = ref<'jobs' | 'drafts'>('jobs')
const jobs = ref<JobSummary[]>([])
const drafts = ref<DraftSummary[]>([])
const health = ref<PlatformHealth | null>(null)
const selectedJob = ref<JobSummary | null>(null)
const selectedResult = ref<JobResult | null>(null)
const selectedDraft = ref<DraftDetail | null>(null)
const draftPayload = ref('')
const rejectionReason = ref('')
const loading = ref(false)
const actionPending = ref(false)
const error = ref('')
let pollTimer: number | null = null

const projectQuery = computed(() => selectedProject.value?.id ? `?projectId=${selectedProject.value.id}` : '')
const hasActiveJobs = computed(() => jobs.value.some(job => ['queued', 'running', 'retrying'].includes(job.status)))
const originalPayloadText = computed(() => selectedDraft.value ? JSON.stringify(selectedDraft.value.originalPayload, null, 2) : '')
const draftPayloadChanged = computed(() => {
  if (!selectedDraft.value) return false

  try {
    return JSON.stringify(JSON.parse(draftPayload.value)) !== JSON.stringify(selectedDraft.value.originalPayload)
  } catch {
    return draftPayload.value.trim() !== originalPayloadText.value.trim()
  }
})

const showRawJson = ref(false)
const parsedActions = ref<any[]>([])

function formatDateTimeLocal(value: string) {
  if (!value) return ''
  try {
    const d = new Date(value)
    if (isNaN(d.getTime())) return ''
    const year = d.getFullYear()
    const month = String(d.getMonth() + 1).padStart(2, '0')
    const day = String(d.getDate()).padStart(2, '0')
    const hours = String(d.getHours()).padStart(2, '0')
    const minutes = String(d.getMinutes()).padStart(2, '0')
    return `${year}-${month}-${day}T${hours}:${minutes}`
  } catch {
    return ''
  }
}

watch(() => selectedDraft.value, (newDraft) => {
  if (newDraft) {
    showRawJson.value = false
    try {
      const parsed = JSON.parse(draftPayload.value)
      if (parsed && Array.isArray(parsed.actions)) {
        parsedActions.value = parsed.actions.map((act: any) => ({
          ...act,
          checked: true
        }))
      } else {
        parsedActions.value = []
      }
    } catch {
      parsedActions.value = []
    }
  } else {
    parsedActions.value = []
  }
})

watch(parsedActions, (newActions) => {
  if (!selectedDraft.value) return
  try {
    const currentPayload = JSON.parse(draftPayload.value || '{}')
    currentPayload.actions = newActions.filter((act: any) => act.checked).map((act: any) => {
      const { checked, ...rest } = act
      return rest
    })
    draftPayload.value = JSON.stringify(currentPayload, null, 2)
  } catch {
    // ignore
  }
}, { deep: true })

function queryValue(value: unknown) {
  return typeof value === 'string' ? value : null
}

async function replaceActivityQuery(tab: 'jobs' | 'drafts', id?: string) {
  const query = {
    ...route.query,
    aiActivity: '1',
    aiTab: tab,
    aiJob: undefined as string | undefined,
    aiDraft: undefined as string | undefined,
  }
  if (tab === 'jobs' && id) query.aiJob = id
  if (tab === 'drafts' && id) query.aiDraft = id
  await router.replace({ query })
}

async function refresh(silent = false) {
  if (!silent) loading.value = true
  error.value = ''
  try {
    const [jobItems, draftItems, healthStatus] = await Promise.all([
      apiResult<JobSummary[]>(`/api/ai/jobs${projectQuery.value}`),
      apiResult<DraftSummary[]>(`/api/ai/drafts${projectQuery.value}`),
      apiResult<PlatformHealth>('/api/ai/health'),
    ])
    jobs.value = jobItems
    drafts.value = draftItems
    health.value = healthStatus
    if (selectedJob.value) {
      const refreshedJob = jobItems.find(job => job.jobId === selectedJob.value?.jobId)
      if (refreshedJob) await showJob(refreshedJob, false)
      else if (!route.query.aiJob) selectedJob.value = null
    }
    await applyDeepLink(true)
  } catch (cause) {
    error.value = errorMessage(cause, 'Không thể tải hoạt động AI.')
  } finally {
    loading.value = false
  }
}

async function showJob(job: JobSummary, syncRoute = true) {
  selectedJob.value = job
  selectedResult.value = null
  selectedDraft.value = null
  activeTab.value = 'jobs'
  if (syncRoute) void replaceActivityQuery('jobs', job.jobId)
  if (job.status !== 'succeeded') return
  try {
    selectedResult.value = await apiResult<JobResult>(`/api/ai/jobs/${job.jobId}/result`)
  } catch (cause) {
    error.value = errorMessage(cause, 'Không thể tải kết quả AI.')
  }
}

async function openJob(job: JobSummary) {
  await showJob(job)
}

async function loadDraft(draftId: string, syncRoute = true) {
  error.value = ''
  try {
    selectedDraft.value = await apiResult<DraftDetail>(`/api/ai/drafts/${draftId}`)
    selectedJob.value = null
    selectedResult.value = null
    activeTab.value = 'drafts'
    if (syncRoute) void replaceActivityQuery('drafts', draftId)
    draftPayload.value = JSON.stringify(selectedDraft.value.workingPayload, null, 2)
    rejectionReason.value = ''
  } catch (cause) {
    error.value = errorMessage(cause, 'Không thể tải bản nháp AI.')
  }
}

async function openDraft(draft: DraftSummary) {
  await loadDraft(draft.draftId)
}

async function applyDeepLink(force = false) {
  const draftId = queryValue(route.query.aiDraft)
  if (draftId) {
    activeTab.value = 'drafts'
    if (force || selectedDraft.value?.draftId !== draftId) await loadDraft(draftId, false)
    return
  }

  const jobId = queryValue(route.query.aiJob)
  if (jobId) {
    activeTab.value = 'jobs'
    if (!force && selectedJob.value?.jobId === jobId) return
    const listedJob = jobs.value.find(job => job.jobId === jobId)
    const job = listedJob ?? await apiResult<JobSummary>(`/api/ai/jobs/${jobId}`)
    await showJob(job, false)
    return
  }

  activeTab.value = route.query.aiTab === 'drafts' ? 'drafts' : 'jobs'
}

function selectTab(tab: 'jobs' | 'drafts') {
  activeTab.value = tab
  closeDetail(false)
  void replaceActivityQuery(tab)
}

async function runAction(action: () => Promise<void>) {
  actionPending.value = true
  error.value = ''
  try {
    await action()
    await refresh(true)
  } catch (cause) {
    error.value = errorMessage(cause, 'Không thể hoàn tất thao tác AI.')
  } finally {
    actionPending.value = false
  }
}

async function cancelJob(job: JobSummary) {
  await runAction(() => apiResult(`/api/ai/jobs/${job.jobId}/cancel`, {
    method: 'POST', body: JSON.stringify({ reason: 'Canceled from AI Activity' }),
  }).then(() => undefined))
}

async function retryJob(job: JobSummary) {
  await runAction(() => apiResult(`/api/ai/jobs/${job.jobId}/retry`, {
    method: 'POST', body: JSON.stringify({}),
  }).then(() => undefined))
}

async function saveDraft() {
  if (!selectedDraft.value) return
  await runAction(async () => {
    selectedDraft.value = await apiResult<DraftDetail>(`/api/ai/drafts/${selectedDraft.value!.draftId}`, {
      method: 'PATCH',
      body: JSON.stringify({ workingPayloadJson: draftPayload.value, rowVersion: selectedDraft.value!.rowVersion }),
    })
    draftPayload.value = JSON.stringify(selectedDraft.value.workingPayload, null, 2)
  })
}

async function confirmDraft() {
  if (!selectedDraft.value) return
  const idempotencyKey = crypto.randomUUID()
  await runAction(async () => {
    await apiResult(`/api/ai/drafts/${selectedDraft.value!.draftId}/confirm`, {
      method: 'POST', headers: { 'Idempotency-Key': idempotencyKey },
      body: JSON.stringify({
        editedPayloadJson: draftPayload.value,
        confirmAction: confirmAction(selectedDraft.value!.draftType),
        confirmationNote: 'Confirmed from AI Activity',
        rowVersion: selectedDraft.value!.rowVersion,
        idempotencyKey,
      }),
    })
    selectedDraft.value = null
    await replaceActivityQuery('drafts')
  })
}

async function rejectDraft() {
  if (!selectedDraft.value || !rejectionReason.value.trim()) return
  const idempotencyKey = crypto.randomUUID()
  await runAction(async () => {
    await apiResult(`/api/ai/drafts/${selectedDraft.value!.draftId}/reject`, {
      method: 'POST', headers: { 'Idempotency-Key': idempotencyKey },
      body: JSON.stringify({ reason: rejectionReason.value.trim(), rowVersion: selectedDraft.value!.rowVersion, idempotencyKey }),
    })
    selectedDraft.value = null
    await replaceActivityQuery('drafts')
  })
}

function confirmAction(draftType: string) {
  const value = draftType.toLowerCase()
  if (value.includes('breakdown')) return 'create_subtasks'
  if (value.includes('checklist')) return 'save_checklist'
  if (value.includes('report')) return 'save_report'
  if (value.includes('resolution') || value.includes('delay')) return 'execute_action'
  return 'create_tasks'
}

function statusLabel(status: string) {
  return ({ queued: 'Đang chờ', running: 'Đang xử lý', retrying: 'Đang thử lại', succeeded: 'Hoàn tất', failed: 'Thất bại',
    canceled: 'Đã hủy', pending_review: 'Chờ duyệt', confirmed: 'Đã xác nhận', rejected: 'Đã từ chối', expired: 'Hết hạn' } as Record<string, string>)[status] ?? status
}
function formatTime(value: string) { return new Intl.DateTimeFormat('vi-VN', { dateStyle: 'short', timeStyle: 'short' }).format(new Date(value)) }
function closeDetail(syncRoute = true) {
  selectedJob.value = null
  selectedResult.value = null
  selectedDraft.value = null
  if (syncRoute) void replaceActivityQuery(activeTab.value)
}

watch(() => selectedProject.value?.id, () => { closeDetail(); void refresh() })
watch(() => [route.query.aiJob, route.query.aiDraft, route.query.aiTab], () => { void applyDeepLink() })
onMounted(() => {
  void refresh()
  pollTimer = window.setInterval(() => {
    if (document.visibilityState === 'visible' && (hasActiveJobs.value || activeTab.value === 'jobs')) void refresh(true)
  }, 3000)
})
onBeforeUnmount(() => { if (pollTimer != null) window.clearInterval(pollTimer) })
</script>

<template>
  <section class="ai-activity" aria-label="Hoạt động AI">
    <div class="activity-toolbar">
      <div class="activity-tabs" role="tablist">
        <button :class="{ active: activeTab === 'jobs' }" role="tab" @click="selectTab('jobs')">Jobs <span>{{ jobs.length }}</span></button>
        <button :class="{ active: activeTab === 'drafts' }" role="tab" @click="selectTab('drafts')">Bản nháp <span>{{ drafts.length }}</span></button>
      </div>
      <button class="icon-button" title="Làm mới" :disabled="loading" @click="refresh()"><RefreshCw :size="16" :class="{ spinning: loading }" /></button>
    </div>
    <div v-if="health" class="health-strip" :class="health.status"><span class="health-dot" /><span>{{ health.status === 'healthy' ? 'Worker sẵn sàng' : `Degraded: ${health.degradedReason}` }}</span><span class="health-count">{{ health.queueDepth }} chờ</span></div>
    <div v-if="error" class="activity-error" role="alert"><AlertTriangle :size="16" /><span>{{ error }}</span></div>

    <div v-if="selectedJob" class="activity-detail">
      <header><button class="icon-button" title="Quay lại" @click="closeDetail()"><X :size="16" /></button><div><strong>{{ selectedJob.jobType }}</strong><span>{{ statusLabel(selectedJob.status) }} · lần {{ selectedJob.attemptCount }}/{{ selectedJob.maxAttempts }}</span></div></header>
      <div class="progress-track"><span :style="{ width: `${selectedJob.progressPercent}%` }" /></div>
      <p v-if="selectedJob.lastErrorCode" class="detail-warning">{{ selectedJob.lastErrorCode }}</p>
      <pre v-if="selectedResult" class="job-result">{{ JSON.stringify(selectedResult.result, null, 2) }}</pre>
      <div class="detail-actions">
        <button v-if="['queued', 'running', 'retrying'].includes(selectedJob.status)" class="secondary-action" :disabled="actionPending" @click="cancelJob(selectedJob)"><Ban :size="15" /> Hủy</button>
        <button v-if="['failed', 'canceled'].includes(selectedJob.status)" class="primary-action" :disabled="actionPending" @click="retryJob(selectedJob)"><RotateCcw :size="15" /> Thử lại</button>
      </div>
    </div>

    <div v-else-if="selectedDraft" class="activity-detail draft-detail">
      <header><button class="icon-button" title="Quay lại" @click="closeDetail()"><X :size="16" /></button><div><strong>{{ selectedDraft.draftType }}</strong><span>{{ statusLabel(selectedDraft.status) }}</span></div></header>
      <div class="draft-change-state" :class="{ changed: draftPayloadChanged }">
        {{ draftPayloadChanged ? 'Đã chỉnh sửa' : 'Chưa chỉnh sửa' }}
      </div>
      <!-- Custom visual editor for ProjectDelayResolution -->
      <div v-if="selectedDraft.draftType === 'ProjectDelayResolution' && !showRawJson" class="visual-draft-editor">
        <div class="visual-editor-header">
          <span>📋 Đề xuất xử lý tiến độ (AI)</span>
          <button class="toggle-raw-btn" type="button" @click="showRawJson = true">
            Xem JSON gốc
          </button>
        </div>

        <div class="actions-list">
          <div v-for="(act, idx) in parsedActions" :key="idx" class="action-card" :class="{ 'is-unchecked': !act.checked }">
            <div class="action-card-header">
              <label class="checkbox-container">
                <input type="checkbox" v-model="act.checked" />
                <span class="action-type-badge" :class="act.type.toLowerCase()">
                  {{ act.type === 'SendNotification' ? '📧 Gửi thông báo' : '📝 Cập nhật Task' }}
                </span>
              </label>
            </div>

            <div v-if="act.checked" class="action-card-body">
              <!-- SendNotification fields -->
              <template v-if="act.type === 'SendNotification'">
                <div class="form-group-row">
                  <div class="form-group">
                    <label>Người nhận</label>
                    <input type="text" v-model="act.recipientName" placeholder="Tên thành viên" />
                  </div>
                  <div class="form-group">
                    <label>Email</label>
                    <input type="email" v-model="act.recipientEmail" placeholder="email@example.com" />
                  </div>
                </div>
                <div class="form-group">
                  <label>Tiêu đề email</label>
                  <input type="text" v-model="act.subject" />
                </div>
                <div class="form-group">
                  <label>Nội dung cảnh báo</label>
                  <textarea v-model="act.message" rows="3"></textarea>
                </div>
              </template>

              <!-- UpdateTask fields -->
              <template v-if="act.type === 'UpdateTask'">
                <div class="form-group">
                  <label>Tên công việc</label>
                  <input type="text" v-model="act.title" readonly class="readonly-input" />
                </div>
                <div class="form-group-row">
                  <div class="form-group">
                    <label>Trạng thái</label>
                    <select v-model="act.status">
                      <option value="Todo">Todo</option>
                      <option value="In Progress">In Progress</option>
                      <option value="In Review">In Review</option>
                      <option value="Done">Done</option>
                    </select>
                  </div>
                  <div class="form-group">
                    <label>Độ ưu tiên</label>
                    <select v-model="act.priority">
                      <option value="Low">Low</option>
                      <option value="Medium">Medium</option>
                      <option value="High">High</option>
                      <option value="Critical">Critical</option>
                    </select>
                  </div>
                </div>
                <div class="form-group">
                  <label>Hạn chót đề xuất</label>
                  <input type="datetime-local" :value="formatDateTimeLocal(act.dueDate)" @input="act.dueDate = ($event.target as HTMLInputElement).value" />
                </div>
              </template>
            </div>
          </div>
        </div>
      </div>

      <div v-else class="draft-compare" aria-label="So sánh bản nháp AI">
        <header v-if="selectedDraft.draftType === 'ProjectDelayResolution'" style="display:flex; justify-content:flex-end; padding:0; margin:0; width:100%;">
          <button class="toggle-raw-btn" type="button" @click="showRawJson = false" style="margin-bottom:8px;">
            Quay lại xem trực quan
          </button>
        </header>
        <section>
          <strong>Bản AI gốc</strong>
          <pre>{{ originalPayloadText }}</pre>
        </section>
        <section>
          <strong>Bản đang duyệt</strong>
          <textarea v-model="draftPayload" spellcheck="false" aria-label="Nội dung bản nháp" />
        </section>
      </div>
      <input v-model="rejectionReason" type="text" placeholder="Lý do từ chối" />
      <div class="detail-actions">
        <button class="secondary-action" :disabled="actionPending" @click="saveDraft"><Save :size="15" /> Lưu</button>
        <button class="danger-action" :disabled="actionPending || !rejectionReason.trim()" @click="rejectDraft"><X :size="15" /> Từ chối</button>
        <button class="primary-action" :disabled="actionPending" @click="confirmDraft"><Check :size="15" /> Xác nhận</button>
      </div>
    </div>

    <div v-else class="ai-activity-list">
      <div v-if="loading && jobs.length === 0 && drafts.length === 0" class="empty-state"><LoaderCircle :size="22" class="spinning" /><span>Đang tải hoạt động AI...</span></div>
      <template v-else-if="activeTab === 'jobs'">
        <button v-for="job in jobs" :key="job.jobId" class="activity-row" @click="openJob(job)">
          <span class="row-icon" :class="job.status"><LoaderCircle v-if="['running', 'retrying'].includes(job.status)" :size="16" class="spinning" /><Clock3 v-else-if="job.status === 'queued'" :size="16" /><Check v-else-if="job.status === 'succeeded'" :size="16" /><AlertTriangle v-else-if="job.status === 'failed'" :size="16" /><Ban v-else :size="16" /></span>
          <span class="row-copy"><strong>{{ job.jobType }}</strong><small>{{ statusLabel(job.status) }} · {{ formatTime(job.createdAt) }}</small></span><ChevronRight :size="16" />
        </button>
        <div v-if="!loading && jobs.length === 0" class="empty-state"><Clock3 :size="22" /><span>Chưa có AI job.</span></div>
      </template>
      <template v-else>
        <button v-for="draft in drafts" :key="draft.draftId" class="activity-row" @click="openDraft(draft)"><span class="row-icon pending_review"><FileCheck2 :size="16" /></span><span class="row-copy"><strong>{{ draft.draftType }}</strong><small>{{ statusLabel(draft.status) }} · {{ formatTime(draft.createdAt) }}</small></span><ChevronRight :size="16" /></button>
        <div v-if="!loading && drafts.length === 0" class="empty-state"><FileCheck2 :size="22" /><span>Không có bản nháp chờ duyệt.</span></div>
      </template>
    </div>
  </section>
</template>

<style scoped>
.ai-activity{height:100%;display:flex;flex-direction:column;background:#fff;color:#17202a}.activity-toolbar{min-height:48px;padding:7px 12px;display:flex;align-items:center;justify-content:space-between;border-bottom:1px solid #e4e7eb}.activity-tabs{display:flex;gap:4px}.activity-tabs button{height:32px;padding:0 10px;border:0;border-bottom:2px solid transparent;background:transparent;color:#59636e;font:inherit;font-size:13px;cursor:pointer}.activity-tabs button.active{color:#17202a;border-bottom-color:#24735b;font-weight:700}.activity-tabs span{margin-left:4px;color:#7b8490;font-size:11px}.icon-button{width:32px;height:32px;display:inline-grid;place-items:center;border:1px solid transparent;background:transparent;color:#59636e;cursor:pointer}.icon-button:hover{border-color:#d7dce1;background:#f6f7f8}.health-strip{min-height:34px;padding:0 14px;display:flex;align-items:center;gap:8px;background:#eef7f2;color:#285c49;font-size:12px}.health-strip.degraded{background:#fff7e8;color:#7a4d0b}.health-dot{width:7px;height:7px;border-radius:50%;background:#31866a}.degraded .health-dot{background:#c47a10}.health-count{margin-left:auto}.activity-error{padding:10px 14px;display:flex;gap:8px;background:#fff0f0;color:#a33535;font-size:12px}.ai-activity-list{flex:1;min-height:0;overflow-y:auto}.activity-row{width:100%;min-height:62px;padding:10px 14px;display:grid;grid-template-columns:32px minmax(0,1fr) 18px;gap:10px;align-items:center;border:0;border-bottom:1px solid #edf0f2;background:#fff;color:inherit;text-align:left;cursor:pointer}.activity-row:hover{background:#f7f9f8}.row-icon{width:30px;height:30px;display:grid;place-items:center;border-radius:6px;background:#eef2f4;color:#53606c}.row-icon.running,.row-icon.retrying,.row-icon.queued{background:#edf3fa;color:#35638b}.row-icon.succeeded,.row-icon.pending_review{background:#eaf6f0;color:#24735b}.row-icon.failed{background:#fff0f0;color:#aa3c3c}.row-copy{min-width:0;display:flex;flex-direction:column;gap:4px}.row-copy strong,.row-copy small{overflow:hidden;text-overflow:ellipsis;white-space:nowrap}.row-copy strong{font-size:13px}.row-copy small{color:#737d87;font-size:11px}.activity-detail{flex:1;min-height:0;padding:12px 14px;display:flex;flex-direction:column;gap:12px;overflow-y:auto}.activity-detail header{display:grid;grid-template-columns:32px minmax(0,1fr);gap:8px;align-items:center}.activity-detail header div{min-width:0;display:flex;flex-direction:column;gap:3px}.activity-detail header strong{overflow-wrap:anywhere;font-size:14px}.activity-detail header span{color:#737d87;font-size:11px}.progress-track{height:5px;overflow:hidden;background:#e9edef}.progress-track span{display:block;height:100%;background:#31866a}.detail-warning{margin:0;padding:8px 10px;background:#fff7e8;color:#7a4d0b;font-size:12px;overflow-wrap:anywhere}.activity-detail pre{flex:1;min-height:180px;margin:0;padding:12px;overflow:auto;background:#f5f7f8;border:1px solid #e0e4e7;font:11px/1.5 ui-monospace,monospace;white-space:pre-wrap;overflow-wrap:anywhere}.draft-detail textarea{flex:1;min-height:260px;resize:vertical;padding:10px;border:1px solid #cfd5da;font:11px/1.5 ui-monospace,monospace}.draft-detail input{min-height:36px;padding:0 10px;border:1px solid #cfd5da;font:inherit;font-size:12px}.detail-actions{display:flex;justify-content:flex-end;gap:7px;flex-wrap:wrap}.detail-actions button{min-height:34px;padding:0 10px;display:inline-flex;align-items:center;gap:6px;border:1px solid transparent;font:inherit;font-size:12px;cursor:pointer}.primary-action{background:#24735b;color:#fff}.secondary-action{background:#fff;border-color:#cfd5da!important;color:#39434d}.danger-action{background:#fff0f0;color:#a33535}.detail-actions button:disabled,.icon-button:disabled{opacity:.5;cursor:not-allowed}.empty-state{min-height:180px;display:grid;place-content:center;justify-items:center;gap:8px;color:#7a838d;font-size:12px}.spinning{animation:spin 1s linear infinite}@keyframes spin{to{transform:rotate(360deg)}}
.draft-change-state{align-self:flex-start;padding:3px 7px;background:#eef2f4;color:#59636e;font-size:11px;font-weight:700}.draft-change-state.changed{background:#fff7e8;color:#7a4d0b}.draft-compare{display:grid;gap:12px}.draft-compare section{display:grid;gap:6px;min-width:0}.draft-compare section>strong{font-size:12px}.draft-compare pre{flex:none;min-height:96px;max-height:160px;padding:10px}.draft-compare textarea{flex:none;min-height:210px;width:100%;box-sizing:border-box}.job-result{min-height:180px}

/* Rich Visual Editor Styles */
.visual-draft-editor {
  display: flex;
  flex-direction: column;
  gap: 12px;
  background: #f8fafc;
  border: 1px solid #cbd5e1;
  border-radius: 8px;
  padding: 12px;
  max-height: 480px;
  overflow-y: auto;
  color: #334155;
}
.visual-editor-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  font-weight: 700;
  font-size: 13px;
  color: #0f172a;
  border-bottom: 1px solid #cbd5e1;
  padding-bottom: 8px;
}
.toggle-raw-btn {
  background: #eff6ff;
  border: 1px solid #bfdbfe;
  color: #1d4ed8;
  font-size: 11px;
  font-weight: 700;
  cursor: pointer;
  padding: 4px 10px;
  border-radius: 6px;
  transition: all 0.2s;
}
.toggle-raw-btn:hover {
  background: #dbeafe;
  border-color: #93c5fd;
}
.actions-list {
  display: flex;
  flex-direction: column;
  gap: 10px;
}
.action-card {
  background: #ffffff;
  border: 1px solid #cbd5e1;
  border-radius: 8px;
  padding: 12px;
  box-shadow: 0 1px 3px rgba(0,0,0,0.05);
  transition: all 0.2s ease;
}
.action-card.is-unchecked {
  opacity: 0.5;
  background: #f8fafc;
  border-color: #e2e8f0;
}
.action-card-header {
  display: flex;
  align-items: center;
}
.action-card.is-unchecked .action-card-body {
  display: none;
}
.checkbox-container {
  display: flex;
  align-items: center;
  gap: 10px;
  font-size: 12px;
  font-weight: 700;
  color: #0f172a;
  cursor: pointer;
}
.action-type-badge {
  padding: 3px 8px;
  border-radius: 9999px;
  font-size: 10px;
  font-weight: 800;
  text-transform: uppercase;
  letter-spacing: 0.025em;
}
.action-type-badge.sendnotification {
  background: #eff6ff;
  color: #1d4ed8;
  border: 1px solid #bfdbfe;
}
.action-type-badge.updatetask {
  background: #fffbeb;
  color: #d97706;
  border: 1px solid #fde68a;
}
.action-card-body {
  display: flex;
  flex-direction: column;
  gap: 8px;
  border-top: 1px dashed #cbd5e1;
  padding-top: 10px;
  margin-top: 10px;
}
.form-group {
  display: flex;
  flex-direction: column;
  gap: 4px;
}
.form-group label {
  font-size: 11px;
  font-weight: 700;
  color: #475569;
}
.form-group input, .form-group textarea, .form-group select {
  border: 1px solid #cbd5e1;
  border-radius: 6px;
  padding: 6px 10px;
  font-size: 12px;
  color: #0f172a;
  background: #ffffff;
  width: 100%;
  box-sizing: border-box;
}
.form-group input:focus, .form-group textarea:focus, .form-group select:focus {
  border-color: #3b82f6;
  outline: none;
  box-shadow: 0 0 0 2px rgba(59, 130, 246, 0.25);
}
.form-group-row {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 10px;
}
.readonly-input {
  background: #f1f5f9 !important;
  color: #475569 !important;
  cursor: not-allowed;
}
</style>
