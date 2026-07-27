<script setup lang="ts">
import { computed, onBeforeUnmount, watch } from 'vue'
import { ref } from 'vue'
import {
  AlertTriangle,
  Ban,
  CheckCircle2,
  Clock3,
  Database,
  LoaderCircle,
  RefreshCw,
  RotateCcw,
  Sparkles,
} from 'lucide-vue-next'
import { ApiError, apiResult, errorMessage } from '../utils/api-client'

type JobSummary = {
  jobId: string
  jobType: string
  projectId: string | null
  status: string
  progressPercent: number
  attemptCount: number
  maxAttempts: number
  createdAt: string
  lastErrorCode: string | null
  isMock: boolean
  scopeSourceType?: string | null
  scopeSourceEntityId?: string | null
}

type JobDetail = JobSummary & {
  lastErrorMessage: string | null
  lastErrorRetryable: boolean
  cacheHit: boolean
  selectedProvider: string | null
  selectedModel: string | null
}

type SourceRef = {
  key: string
  type: 'project' | 'sprint' | 'task'
  entityId: string
  label: string
  url: string
  version: string | null
}

type GroundedPoint = {
  text: string
  metricRefs: string[]
  sourceRefs: string[]
}

type RiskPoint = {
  code: string
  severity: 'low' | 'medium' | 'high'
  title: string
  metricRefs: string[]
  sourceRefs: string[]
}

type NextAction = {
  title: string
  rationale: string
  metricRefs: string[]
  sourceRefs: string[]
}

type ProgressSummary = {
  scope:
    | { projectId: string; projectName: string; projectCode: string }
    | {
        type: 'sprint'
        projectId: string
        projectName: string
        projectCode: string
        sprintId: string
        sprintName: string
      }
  period: { kind: 'current_snapshot'; snapshotAt: string }
  coverage: {
    dataState: 'empty' | 'sufficient'
    visibility: 'manager_full_project'
    includedTaskCount: number
    excludedTaskCount: number
  }
  metrics: {
    total: number
    done: number
    inProgress: number
    todo: number
    overdue: number
    dueSoon: number
    completionRate: number
  }
  summaryPoints: GroundedPoint[]
  risks: RiskPoint[]
  nextActions: NextAction[]
  sourceRefs: SourceRef[]
  warnings: string[]
}

type JobResult = {
  jobId: string
  schemaId: string
  schemaVersion: string
  result: ProgressSummary
  cacheHit: boolean
  isMock: boolean
  mockReason: string | null
  sourceStale: boolean
}

const props = defineProps<{
  projectId: string
  canGenerate: boolean
  sprintId?: string
  sprintName?: string
}>()

const job = ref<JobDetail | null>(null)
const result = ref<JobResult | null>(null)
const loading = ref(false)
const actionPending = ref(false)
const message = ref('')
let pollTimer: number | null = null

const isSprint = computed(() => Boolean(props.sprintId))
const capabilityJobType = computed(() =>
  isSprint.value ? 'sprint_progress_summary' : 'project_progress_summary',
)
const scopeKey = computed(() => `${props.projectId}:${props.sprintId ?? 'project'}`)
const cardTestId = computed(() =>
  isSprint.value ? 'sprint-progress-ai-card' : 'project-progress-ai-card',
)
const generateTestId = computed(() =>
  isSprint.value ? 'generate-sprint-progress' : 'generate-project-progress',
)
const emptyTestId = computed(() =>
  isSprint.value ? 'sprint-progress-empty' : 'project-progress-empty',
)
const errorTestId = computed(() =>
  isSprint.value ? 'sprint-progress-error' : 'project-progress-error',
)
const scopeNoun = computed(() => isSprint.value ? 'mốc' : 'dự án')
const scopeTitle = computed(() =>
  isSprint.value
    ? `Tóm tắt mốc${props.sprintName ? ` · ${props.sprintName}` : ''}`
    : 'Tóm tắt có căn cứ',
)
const isActive = computed(() =>
  Boolean(job.value && ['queued', 'running', 'retrying'].includes(job.value.status)),
)
const isEmpty = computed(() => result.value?.result.coverage.dataState === 'empty')
const canRetry = computed(() =>
  Boolean(job.value &&
    ['failed', 'canceled'].includes(job.value.status) &&
    job.value.attemptCount < job.value.maxAttempts),
)
const sourceByKey = computed(() =>
  new Map((result.value?.result.sourceRefs ?? []).map(source => [source.key, source])),
)
const statusLabel = computed(() => {
  if (result.value?.sourceStale) return 'Dữ liệu nguồn đã thay đổi'
  return ({
    queued: 'Đang chờ',
    running: 'Đang phân tích',
    retrying: 'Đang thử lại',
    succeeded: isEmpty.value ? 'Không có dữ liệu' : 'Đã tạo',
    failed: 'Không thể tạo',
    canceled: 'Đã hủy',
  } as Record<string, string>)[job.value?.status ?? ''] ?? 'Chưa chạy'
})

function clearPoll() {
  if (pollTimer != null) {
    window.clearTimeout(pollTimer)
    pollTimer = null
  }
}

function schedulePoll() {
  clearPoll()
  if (!isActive.value) return
  pollTimer = window.setTimeout(() => void hydrateJob(job.value!.jobId, true), 2500)
}

async function restore() {
  const expectedScope = scopeKey.value
  const expectedSprintId = props.sprintId
  const expectedJobType = capabilityJobType.value
  clearPoll()
  loading.value = true
  message.value = ''
  job.value = null
  result.value = null
  try {
    const jobs = await apiResult<JobSummary[]>(`/api/ai/jobs?projectId=${props.projectId}`)
    const latest = jobs.find(item =>
      item.jobType === expectedJobType &&
      (!expectedSprintId ||
        (item.scopeSourceType?.toLowerCase() === 'sprint' &&
          item.scopeSourceEntityId === expectedSprintId)),
    )
    if (scopeKey.value !== expectedScope) return
    if (latest) await hydrateJob(latest.jobId, true, expectedScope)
  } catch (cause) {
    if (scopeKey.value === expectedScope) {
      message.value = friendlyError(cause, 'Không thể khôi phục bản phân tích gần nhất.')
    }
  } finally {
    if (scopeKey.value === expectedScope) loading.value = false
  }
}

async function hydrateJob(jobId: string, silent = false, expectedScope = scopeKey.value) {
  if (!silent) loading.value = true
  try {
    const hydratedJob = await apiResult<JobDetail>(`/api/ai/jobs/${jobId}`)
    if (scopeKey.value !== expectedScope) return
    job.value = hydratedJob
    result.value = null
    if (job.value.status === 'succeeded') {
      const persisted = await apiResult<JobResult>(`/api/ai/jobs/${jobId}/result`)
      if (scopeKey.value !== expectedScope) return
      if (persisted.schemaId !== 'progress_summary.v4' || persisted.isMock) {
        message.value = 'Kết quả này không đạt contract AI-native và sẽ không được hiển thị như dữ liệu thật.'
      } else {
        result.value = persisted
        message.value = ''
      }
    } else if (job.value.status === 'failed') {
      message.value = friendlyJobFailure(job.value)
    } else if (job.value.status === 'canceled') {
      message.value = 'Tác vụ đã được hủy. Bạn có thể thử lại khi sẵn sàng.'
    } else {
      message.value = ''
    }
  } catch (cause) {
    if (scopeKey.value === expectedScope) {
      message.value = friendlyError(cause, 'Không thể tải trạng thái phân tích.')
    }
  } finally {
    if (scopeKey.value === expectedScope) {
      loading.value = false
      schedulePoll()
    }
  }
}

async function generate() {
  if (!props.canGenerate || actionPending.value || isActive.value) return
  actionPending.value = true
  message.value = ''
  try {
    const created = await apiResult<{ jobId: string }>(
      isSprint.value
        ? `/api/ai/projects/${props.projectId}/sprints/${props.sprintId}/progress-summary`
        : `/api/ai/projects/${props.projectId}/progress-summary`,
      {
        method: 'POST',
        headers: { 'Idempotency-Key': crypto.randomUUID() },
        body: JSON.stringify({
          period: 'current_snapshot',
          language: 'vi',
          providerHint: 'auto',
          maximumEstimatedCostUsd: 0.25,
          cacheMode: 'use',
        }),
      },
    )
    await hydrateJob(created.jobId)
  } catch (cause) {
    message.value = friendlyError(cause, 'Không thể bắt đầu phân tích tiến độ.')
  } finally {
    actionPending.value = false
  }
}

async function cancel() {
  if (!job.value || actionPending.value) return
  actionPending.value = true
  try {
    await apiResult(`/api/ai/jobs/${job.value.jobId}/cancel`, {
      method: 'POST',
      body: JSON.stringify({ reason: `Canceled from native ${scopeNoun.value} progress card` }),
    })
    await hydrateJob(job.value.jobId)
  } catch (cause) {
    message.value = friendlyError(cause, 'Không thể hủy tác vụ.')
  } finally {
    actionPending.value = false
  }
}

async function retry() {
  if (!job.value || actionPending.value) return
  actionPending.value = true
  try {
    await apiResult(`/api/ai/jobs/${job.value.jobId}/retry`, {
      method: 'POST',
      body: JSON.stringify({}),
    })
    await hydrateJob(job.value.jobId)
  } catch (cause) {
    message.value = friendlyError(cause, 'Không thể thử lại tác vụ.')
  } finally {
    actionPending.value = false
  }
}

function friendlyJobFailure(value: JobDetail) {
  const code = value.lastErrorCode ?? ''
  if (code.includes('SCHEMA')) return 'Nhà cung cấp không trả về dữ liệu đúng schema sau các lần sửa tự động.'
  if (code.includes('SOURCE_STALE')) return `Dữ liệu ${scopeNoun.value} đã đổi trong lúc phân tích. Hãy tạo một bản mới.`
  if (code.includes('BUDGET')) return 'Ngân sách AI hiện tại không đủ để chạy phân tích.'
  if (code.includes('SENSITIVE') || code.includes('CONSENT')) return 'Chính sách riêng tư không cho phép xử lý nguồn hạn chế này.'
  if (code.includes('PROVIDER') || code.includes('TIMEOUT')) return 'Nhà cung cấp AI đang không khả dụng hoặc đã hết thời gian chờ.'
  return value.lastErrorMessage || 'Tác vụ AI không thể hoàn tất.'
}

function friendlyError(cause: unknown, fallback: string) {
  if (cause instanceof ApiError && cause.payload && typeof cause.payload === 'object') {
    const code = String((cause.payload as any).errorCode ?? '')
    if (code.includes('PERMISSION')) return 'Bạn không có quyền quản lý dự án để chạy hoặc đọc phân tích này.'
    if (code.includes('PLATFORM')) return 'Tính năng AI tiến độ đang được tắt. Dữ liệu dự án không bị thay đổi.'
    if (code.includes('BUDGET')) return 'Ngân sách AI hiện tại không đủ để chạy phân tích.'
    if (code.includes('SENSITIVE') || code.includes('CONSENT')) return 'Nguồn hạn chế bị từ chối theo chính sách riêng tư.'
  }
  return errorMessage(cause, fallback)
}

function formatTime(value: string) {
  return new Intl.DateTimeFormat('vi-VN', {
    dateStyle: 'short',
    timeStyle: 'short',
  }).format(new Date(value))
}

function citedSources(refs: string[]) {
  return refs.map(key => sourceByKey.value.get(key)).filter((source): source is SourceRef => Boolean(source))
}

watch(
  () => [props.projectId, props.sprintId],
  () => void restore(),
  { immediate: true },
)
onBeforeUnmount(clearPoll)
</script>

<template>
  <section
    class="ai-progress-card"
    :aria-labelledby="`ai-progress-title-${props.sprintId ?? 'project'}`"
    :data-testid="cardTestId"
  >
    <header>
      <div>
        <span class="eyebrow"><Sparkles :size="15" /> AI-native · tiến độ {{ scopeNoun }}</span>
        <h3 :id="`ai-progress-title-${props.sprintId ?? 'project'}`">{{ scopeTitle }}</h3>
        <p>Metric do Qaly tính từ task đúng phạm vi hiện tại; AI chỉ diễn giải và đề xuất có trích nguồn.</p>
      </div>
      <div class="header-actions">
        <span class="status" :class="job?.status ?? 'idle'">
          <LoaderCircle v-if="isActive" :size="14" class="spin" />
          <CheckCircle2 v-else-if="job?.status === 'succeeded' && !result?.sourceStale" :size="14" />
          <AlertTriangle v-else-if="job?.status === 'failed' || result?.sourceStale" :size="14" />
          <Clock3 v-else :size="14" />
          {{ statusLabel }}
        </span>
        <button
          v-if="canGenerate"
          type="button"
          class="primary"
          :disabled="actionPending || isActive"
          :data-testid="generateTestId"
          @click="generate"
        >
          <LoaderCircle v-if="actionPending" :size="15" class="spin" />
          <RefreshCw v-else-if="job" :size="15" />
          <Sparkles v-else :size="15" />
          {{ job ? 'Tạo bản mới' : 'Tạo tóm tắt' }}
        </button>
      </div>
    </header>

    <div v-if="loading && !job" class="state-box" aria-live="polite">
      <LoaderCircle :size="20" class="spin" /> Đang khôi phục tác vụ gần nhất…
    </div>

    <div v-else-if="!canGenerate && !job" class="state-box">
      <Database :size="20" />
      Chỉ Owner, Manager hoặc Scrum Master có thể tạo và đọc bản phân tích {{ scopeNoun }}.
    </div>

    <div v-else-if="!job" class="state-box">
      <Sparkles :size="20" />
      Chưa có bản phân tích. Qaly sẽ dùng snapshot task của {{ scopeNoun }} tại thời điểm chạy, không tự sửa dữ liệu.
    </div>

    <div v-else-if="isActive" class="running-state" aria-live="polite">
      <div class="progress"><span :style="{ width: `${Math.max(8, job.progressPercent)}%` }" /></div>
      <p>{{ statusLabel }} · lần chạy {{ job.attemptCount }}/{{ job.maxAttempts }}</p>
      <button type="button" class="secondary" :disabled="actionPending" @click="cancel">
        <Ban :size="14" /> Hủy
      </button>
    </div>

    <div v-else-if="isEmpty && result" class="state-box is-empty" :data-testid="emptyTestId">
      <Database :size="22" />
      <div>
        <strong>Chưa có task đóng góp vào tiến độ {{ scopeNoun }}</strong>
        <p>Không gọi provider, không phát sinh token/cost. Hãy thêm task hoặc bật “Đóng góp tiến độ”.</p>
      </div>
    </div>

    <template v-else-if="result && !result.isMock">
      <div v-if="result.sourceStale" class="notice stale" role="status">
        <AlertTriangle :size="16" />
        Snapshot này vẫn được lưu để đọc lại, nhưng nguồn đã đổi. Hãy tạo bản mới trước khi ra quyết định.
      </div>
      <div v-if="result.cacheHit" class="notice cache">
        <Database :size="15" /> Dùng kết quả cache đã được kiểm lại với đúng snapshot nguồn.
      </div>

      <div class="metric-strip">
        <div><span>Hoàn thành</span><strong>{{ result.result.metrics.completionRate }}%</strong></div>
        <div><span>Đã xong</span><strong>{{ result.result.metrics.done }}/{{ result.result.metrics.total }}</strong></div>
        <div><span>Đang làm</span><strong>{{ result.result.metrics.inProgress }}</strong></div>
        <div :class="{ risk: result.result.metrics.overdue > 0 }"><span>Quá hạn</span><strong>{{ result.result.metrics.overdue }}</strong></div>
        <div><span>Sắp đến hạn</span><strong>{{ result.result.metrics.dueSoon }}</strong></div>
      </div>

      <div class="result-grid">
        <article>
          <h4>Điểm chính</h4>
          <div v-for="(point, index) in result.result.summaryPoints" :key="index" class="grounded-item">
            <p>{{ point.text }}</p>
            <div class="refs">
              <span v-for="metric in point.metricRefs" :key="metric">{{ metric }}</span>
              <a v-for="source in citedSources(point.sourceRefs)" :key="source.key" :href="source.url">{{ source.label }}</a>
            </div>
          </div>
        </article>

        <article>
          <h4>Rủi ro</h4>
          <p v-if="!result.result.risks.length" class="muted">Không có rủi ro nào đủ căn cứ để nêu.</p>
          <div v-for="risk in result.result.risks" :key="risk.code" class="grounded-item risk-item" :data-severity="risk.severity">
            <strong>{{ risk.title }}</strong>
            <div class="refs">
              <span v-for="metric in risk.metricRefs" :key="metric">{{ metric }}</span>
              <a v-for="source in citedSources(risk.sourceRefs)" :key="source.key" :href="source.url">{{ source.label }}</a>
            </div>
          </div>
        </article>

        <article>
          <h4>Hành động tiếp theo</h4>
          <p v-if="!result.result.nextActions.length" class="muted">Chưa có đề xuất nào đủ căn cứ.</p>
          <div v-for="(action, index) in result.result.nextActions" :key="index" class="grounded-item">
            <strong>{{ action.title }}</strong>
            <p>{{ action.rationale }}</p>
            <div class="refs">
              <span v-for="metric in action.metricRefs" :key="metric">{{ metric }}</span>
              <a v-for="source in citedSources(action.sourceRefs)" :key="source.key" :href="source.url">{{ source.label }}</a>
            </div>
          </div>
        </article>
      </div>

      <footer>
        Snapshot {{ formatTime(result.result.period.snapshotAt) }} ·
        {{ result.result.coverage.includedTaskCount }} task được tính ·
        {{ result.result.coverage.excludedTaskCount }} task loại khỏi phạm vi
      </footer>
    </template>

    <div v-if="message && !isActive" class="notice error" role="alert" :data-testid="errorTestId">
      <AlertTriangle :size="16" />
      <span>{{ message }}</span>
      <button v-if="canRetry" type="button" class="secondary" :disabled="actionPending" @click="retry">
        <RotateCcw :size="14" /> Thử lại
      </button>
    </div>
  </section>
</template>

<style scoped>
.ai-progress-card{display:grid;gap:18px;border:1px solid color-mix(in srgb,var(--primary) 24%,var(--line));border-radius:var(--radius-shell);padding:24px;background:linear-gradient(145deg,color-mix(in srgb,var(--primary) 5%,var(--panel)),var(--panel) 48%);box-shadow:var(--qaly-shadow-md)}
header{display:flex;align-items:flex-start;justify-content:space-between;gap:24px}header h3{margin:6px 0 4px;color:var(--text-strong);font-size:21px}header p,.state-box p,.running-state p,.grounded-item p{margin:0;color:var(--muted);font-size:12px;line-height:1.55}.eyebrow{display:inline-flex;align-items:center;gap:7px;color:var(--primary);font-size:11px;font-weight:900;letter-spacing:.07em;text-transform:uppercase}.header-actions{display:flex;align-items:center;justify-content:flex-end;gap:9px;flex-wrap:wrap}.status,.primary,.secondary{display:inline-flex;align-items:center;justify-content:center;gap:7px;border-radius:999px;font:inherit;font-size:11px;font-weight:800}.status{border:1px solid var(--line);padding:7px 10px;color:var(--muted);background:var(--panel)}.status.succeeded{color:#047857;background:#ecfdf5}.status.failed,.status.canceled{color:#b42318;background:#fff1f1}.status.running,.status.queued,.status.retrying{color:#1d4ed8;background:#eff6ff}.primary,.secondary{min-height:36px;padding:0 13px;cursor:pointer}.primary{border:1px solid var(--primary);color:#fff;background:var(--primary)}.secondary{border:1px solid var(--line);color:var(--text);background:var(--panel)}button:disabled{opacity:.55;cursor:not-allowed}.state-box{min-height:92px;display:flex;align-items:center;justify-content:center;gap:12px;border:1px dashed var(--line);border-radius:var(--qaly-radius-lg);padding:18px;color:var(--muted);text-align:left}.state-box strong{display:block;margin-bottom:3px;color:var(--text-strong)}.running-state{display:grid;grid-template-columns:minmax(0,1fr) auto;align-items:center;gap:10px 16px;border:1px solid var(--line);border-radius:var(--qaly-radius-lg);padding:16px}.progress{grid-column:1/-1;height:6px;overflow:hidden;border-radius:99px;background:var(--line-light)}.progress span{display:block;height:100%;border-radius:inherit;background:linear-gradient(90deg,var(--primary),#38bdf8);transition:width .25s}.notice{display:flex;align-items:center;gap:9px;border-radius:10px;padding:10px 12px;font-size:11px;font-weight:700}.notice.stale,.notice.error{color:#9a3412;background:#fff7ed}.notice.cache{color:#1d4ed8;background:#eff6ff}.notice.error span{flex:1}.metric-strip{display:grid;grid-template-columns:repeat(5,minmax(0,1fr));overflow:hidden;border:1px solid var(--line);border-radius:var(--qaly-radius-lg);background:var(--panel)}.metric-strip div{display:flex;flex-direction:column;gap:4px;padding:14px 16px;border-right:1px solid var(--line)}.metric-strip div:last-child{border-right:0}.metric-strip span{color:var(--muted);font-size:10px;font-weight:750}.metric-strip strong{color:var(--text-strong);font-size:20px}.metric-strip .risk strong{color:#dc2626}.result-grid{display:grid;grid-template-columns:1.05fr .95fr 1.05fr;gap:12px}.result-grid article{min-width:0;border:1px solid var(--line);border-radius:var(--qaly-radius-lg);padding:16px;background:var(--panel)}.result-grid h4{margin:0 0 13px;color:var(--text-strong);font-size:13px}.grounded-item{display:grid;gap:6px;padding:10px 0;border-top:1px solid var(--line-light)}.grounded-item:first-of-type{border-top:0;padding-top:0}.grounded-item strong{color:var(--text-strong);font-size:12px}.risk-item[data-severity=high] strong{color:#b42318}.risk-item[data-severity=medium] strong{color:#a15c00}.refs{display:flex;gap:5px;flex-wrap:wrap}.refs span,.refs a{border-radius:999px;padding:3px 7px;color:var(--primary);background:var(--primary-soft);font-size:9px;font-weight:800;text-decoration:none}.refs a{text-decoration:underline;text-underline-offset:2px}.muted{color:var(--muted);font-size:11px}footer{color:var(--text-muted);font-size:10px}.spin{animation:spin 1s linear infinite}@keyframes spin{to{transform:rotate(360deg)}}@media(max-width:920px){header{flex-direction:column}.header-actions{justify-content:flex-start}.metric-strip{grid-template-columns:repeat(3,1fr)}.metric-strip div{border-bottom:1px solid var(--line)}.result-grid{grid-template-columns:1fr}}@media(max-width:560px){.ai-progress-card{padding:18px}.metric-strip{grid-template-columns:repeat(2,1fr)}}
</style>
