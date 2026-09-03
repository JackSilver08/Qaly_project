<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import {
  AlertTriangle,
  Check,
  CheckCircle2,
  ChevronRight,
  Circle,
  Clock3,
  ExternalLink,
  LoaderCircle,
  RotateCcw,
  Sparkles,
  Square,
  X,
} from 'lucide-vue-next'
import { apiResult, errorMessage } from '../utils/api-client'
import { showError, showSuccess } from '../composables/use-toast'

interface ProjectMemberOption {
  userId?: string
  id?: string
  fullName: string
}

interface ProjectOption {
  id: string
  name: string
  organizationId?: string | null
  code?: string | null
  status?: string | null
  members?: ProjectMemberOption[]
}

interface OrganizationSkillOption {
  id: string
  name: string
}

interface AiJobCreated {
  jobId: string
  status: string
}

interface AiJobDetail {
  jobId: string
  projectId: string | null
  status: string
  progressPercent: number
  attemptCount: number
  maxAttempts: number
  createdAt: string
  startedAt: string | null
  finishedAt: string | null
  lastErrorCode: string | null
  lastErrorMessage: string | null
  lastErrorRetryable: boolean
  selectedProvider: string | null
  selectedModel: string | null
  draftIds: string[]
}

interface AiActivityEvent {
  eventId: string
  sequence: number
  stage: string
  status: string
  publicLabel: string
  safeDetail?: Record<string, unknown> | null
  current?: number | null
  total?: number | null
  startedAt: string
  completedAt?: string | null
  durationMs?: number | null
  retryable: boolean
  receiptLink?: string | null
}

interface AiActivityFeed {
  jobId: string
  jobStatus: string
  startedAt: string | null
  finishedAt: string | null
  lastSequence: number
  cancellable: boolean
  events: AiActivityEvent[]
}

interface AiTaskCommand {
  commandId: string
  toolName: string
  toolVersion: string
  title: string
  description: string | null
  acceptanceCriteria: string[]
  priority: string
  dueDate: string | null
  estimatedHours: number | null
  assigneeId: string | null
  assigneeMode: string
  requiredSkills: Array<{ skillId: string; requiredLevel: string }>
  sourceRefs: string[]
}

interface AiActionOption {
  optionId: string
  label: string
  summary: string
  tradeOffs: string[]
  commands: AiTaskCommand[]
}

interface AiActionPlan {
  schemaId: string
  schemaVersion: string
  projectId: string
  sourceVersion: string
  userIntent: string
  intentType: string
  confidence: number
  assumptions: string[]
  missingFields: string[]
  warnings: string[]
  options: AiActionOption[]
  review: {
    selectedOptionId: string
    selectedCommandIds: string[]
  }
  generatedAt: string
  [key: string]: unknown
}

interface AiJobResult {
  result: AiActionPlan
  draftIds: string[]
  sourceStale: boolean
}

interface AiDraftDetail {
  draftId: string
  status: string
  workingPayload: AiActionPlan
  rowVersion: string
  confirmationResult?: AiDraftConfirmResult | null
}

interface AiCommandReceipt {
  commandId: string
  status: string
  entityId: string | null
  entityLabel: string | null
  entityUrl: string | null
  errorCode: string | null
  errorMessage: string | null
  appliedSkillCount: number
}

interface AiActionReceipt {
  executionId: string
  status: string
  provider: string | null
  model: string | null
  executedAt: string
  commandResults: AiCommandReceipt[]
  readBackLinks: string[]
}

interface AiDraftConfirmResult {
  status: string
  createdTaskCount: number
  actionReceipt: AiActionReceipt | null
}

const props = defineProps<{
  projectId?: string | null
  projects: ProjectOption[]
  embedded?: boolean
  artifactOnly?: boolean
  initialPrompt?: string
  autoStart?: boolean
  providerHint?: string
  modelProfile?: string
}>()

const emit = defineEmits<{
  close: []
  completed: [projectId: string]
  sessionReset: []
  started: [jobId: string]
}>()

const storageKey = 'qaly-ai-action-composer-session-v1'
const promptText = ref('')
const selectedProjectId = ref(props.projectId || '')
const job = ref<AiJobDetail | null>(null)
const jobId = ref('')
const draft = ref<AiDraftDetail | null>(null)
const plan = ref<AiActionPlan | null>(null)
const receipt = ref<AiActionReceipt | null>(null)
const recoveryPlan = ref<AiActionPlan | null>(null)
const skillCatalog = ref<OrganizationSkillOption[]>([])
const activityEvents = ref<AiActivityEvent[]>([])
const activityLastSequence = ref(0)
const activityCancellable = ref(false)
const requestError = ref('')
const sourceStale = ref(false)
const submitting = ref(false)
const draftSaveState = ref<'idle' | 'saving' | 'saved' | 'error'>('idle')
const now = ref(Date.now())
let pollTimer: number | null = null
let clockTimer: number | null = null
let autosaveTimer: number | null = null
let mounted = false
let autoStartConsumed = false
let refreshInFlight = false
let lastSavedPlanJson = ''
let restoredDraft: { draftId: string; rowVersion: string; plan: AiActionPlan } | null = null

const selectedProject = computed(() =>
  props.projects.find(project => project.id === selectedProjectId.value) ?? null,
)
const memberOptions = computed(() => selectedProject.value?.members ?? [])
const selectedOption = computed(() =>
  plan.value?.options.find(option => option.optionId === plan.value?.review.selectedOptionId) ?? null,
)
const selectedCommandIds = computed(() => new Set(plan.value?.review.selectedCommandIds ?? []))
const selectedCount = computed(() => selectedOption.value?.commands.filter(command => selectedCommandIds.value.has(command.commandId)).length ?? 0)
const isTerminalFailure = computed(() => ['failed', 'canceled'].includes(job.value?.status ?? ''))
const isProcessing = computed(() => Boolean(jobId.value) && !plan.value && !receipt.value && !isTerminalFailure.value)
const canRetry = computed(() => Boolean(job.value?.lastErrorRetryable) && (job.value?.attemptCount ?? 0) < (job.value?.maxAttempts ?? 0))
const modelLabel = computed(() => {
  const provider = receipt.value?.provider || job.value?.selectedProvider
  const model = receipt.value?.model || job.value?.selectedModel
  if (provider || model) return [provider, model].filter(Boolean).join(' · ')
  if (receipt.value) return 'Không rõ model'
  return jobId.value ? 'Đang xác định model' : 'Ưu tiên DeepSeek V4 Pro'
})
const workedFor = computed(() => {
  const startValue = job.value?.startedAt || job.value?.createdAt
  if (!startValue) return '0 giây'
  const end = job.value?.finishedAt ? new Date(job.value.finishedAt).getTime() : now.value
  const seconds = Math.max(0, Math.floor((end - new Date(startValue).getTime()) / 1000))
  if (seconds < 60) return `${seconds} giây`
  const minutes = Math.floor(seconds / 60)
  const rest = seconds % 60
  return `${minutes} phút ${rest.toString().padStart(2, '0')} giây`
})
const queueWaitSeconds = computed(() => {
  if (job.value?.status !== 'queued') return 0
  const createdAt = new Date(job.value.createdAt).getTime()
  return Number.isNaN(createdAt) ? 0 : Math.max(0, Math.floor((now.value - createdAt) / 1000))
})
const queueDelayed = computed(() => queueWaitSeconds.value >= 15)
const receiptVerified = computed(() => receipt.value?.status === 'succeeded')

watch(() => props.projectId, value => {
  if (!jobId.value && value) selectedProjectId.value = value
})

watch(selectedProjectId, value => {
  void loadSkillCatalog(value)
})

watch(
  () => [props.projectId, props.initialPrompt, props.autoStart] as const,
  () => {
    if (mounted) void applyInitialRequest()
  },
)

watch(plan, () => {
  if (!plan.value || !draft.value || receipt.value || draft.value.status.toLowerCase() !== 'pending_review') return
  persistSession()
  scheduleDraftAutosave()
}, { deep: true })

onMounted(() => {
  mounted = true
  clockTimer = window.setInterval(() => { now.value = Date.now() }, 1000)
  if (!props.autoStart) restoreSession()
  void applyInitialRequest()
  void loadSkillCatalog(selectedProjectId.value)
})

onBeforeUnmount(() => {
  stopPolling()
  stopAutosave()
  persistSession()
  if (clockTimer != null) window.clearInterval(clockTimer)
})

async function applyInitialRequest() {
  if (jobId.value) return
  if (props.projectId) selectedProjectId.value = props.projectId
  if (props.initialPrompt?.trim()) promptText.value = props.initialPrompt.trim()
  if (
    props.autoStart
    && !autoStartConsumed
    && selectedProjectId.value
    && promptText.value.trim()
  ) {
    autoStartConsumed = true
    await compose()
  }
}

function newIdempotencyKey(prefix: string) {
  return `${prefix}:${crypto.randomUUID()}`
}

function persistSession() {
  if (!jobId.value) {
    localStorage.removeItem(storageKey)
    return
  }
  localStorage.setItem(storageKey, JSON.stringify({
    jobId: jobId.value,
    projectId: selectedProjectId.value,
    prompt: promptText.value,
    draftId: draft.value?.draftId || null,
    rowVersion: draft.value?.rowVersion || null,
    plan: plan.value,
    recoveryPlan: recoveryPlan.value,
    sourceStale: sourceStale.value,
    savedAt: new Date().toISOString(),
  }))
}

function restoreSession() {
  try {
    const raw = localStorage.getItem(storageKey)
    if (!raw) return
    const saved = JSON.parse(raw) as {
      jobId?: string
      projectId?: string
      prompt?: string
      draftId?: string | null
      rowVersion?: string | null
      plan?: AiActionPlan | null
      recoveryPlan?: AiActionPlan | null
      sourceStale?: boolean
    }
    if (!saved.jobId) return
    if (props.projectId && saved.projectId && saved.projectId !== props.projectId) return
    jobId.value = saved.jobId
    selectedProjectId.value = saved.projectId || props.projectId || ''
    promptText.value = saved.prompt || ''
    if (saved.plan && saved.draftId) {
      restoredDraft = {
        draftId: saved.draftId,
        rowVersion: saved.rowVersion || '',
        plan: structuredClone(saved.plan),
      }
    }
    recoveryPlan.value = saved.recoveryPlan ? structuredClone(saved.recoveryPlan) : null
    sourceStale.value = Boolean(saved.sourceStale)
    startPolling()
  } catch {
    localStorage.removeItem(storageKey)
  }
}

function resetSession() {
  stopPolling()
  stopAutosave()
  job.value = null
  jobId.value = ''
  draft.value = null
  plan.value = null
  receipt.value = null
  recoveryPlan.value = null
  activityEvents.value = []
  activityLastSequence.value = 0
  activityCancellable.value = false
  requestError.value = ''
  sourceStale.value = false
  draftSaveState.value = 'idle'
  lastSavedPlanJson = ''
  restoredDraft = null
  promptText.value = ''
  selectedProjectId.value = props.projectId || selectedProjectId.value
  localStorage.removeItem(storageKey)
  if (props.artifactOnly) emit('sessionReset')
}

function stopAutosave() {
  if (autosaveTimer != null) window.clearTimeout(autosaveTimer)
  autosaveTimer = null
}

function scheduleDraftAutosave() {
  if (!plan.value || !draft.value || receipt.value || submitting.value) return
  const payloadJson = JSON.stringify(plan.value)
  if (payloadJson === lastSavedPlanJson) {
    draftSaveState.value = 'saved'
    return
  }
  stopAutosave()
  autosaveTimer = window.setTimeout(() => { void saveDraftNow() }, 650)
}

async function saveDraftNow() {
  stopAutosave()
  if (!plan.value || !draft.value || receipt.value || draft.value.status.toLowerCase() !== 'pending_review') return true
  const payloadJson = JSON.stringify(plan.value)
  if (payloadJson === lastSavedPlanJson) return true
  if (!draft.value.rowVersion?.trim()) {
    draftSaveState.value = 'error'
    persistSession()
    return false
  }

  draftSaveState.value = 'saving'
  try {
    const patched = await apiResult<AiDraftDetail>(`/api/ai/drafts/${draft.value.draftId}`, {
      method: 'PATCH',
      body: JSON.stringify({
        workingPayloadJson: payloadJson,
        rowVersion: draft.value.rowVersion,
      }),
    })
    draft.value = { ...patched, workingPayload: plan.value }
    lastSavedPlanJson = payloadJson
    draftSaveState.value = 'saved'
    persistSession()
    if (JSON.stringify(plan.value) !== payloadJson) scheduleDraftAutosave()
    return true
  } catch (error) {
    draftSaveState.value = 'error'
    persistSession()
    requestError.value = errorMessage(
      error,
      'Không thể đồng bộ bản tùy chỉnh lên máy chủ. Bản khôi phục cục bộ vẫn được giữ; hãy tải lại bản mới trước khi xác nhận.',
    )
    return false
  }
}

async function compose() {
  if (!selectedProjectId.value || !promptText.value.trim()) return
  submitting.value = true
  requestError.value = ''
  try {
    const sprintId = window.location.hash.startsWith('#milestone-')
      ? window.location.hash.slice('#milestone-'.length)
      : ''
    const created = await apiResult<AiJobCreated>('/api/ai/actions/compose', {
      method: 'POST',
      headers: { 'Idempotency-Key': newIdempotencyKey('action-compose') },
      body: JSON.stringify({
        message: promptText.value.trim(),
        context: {
          route: `${window.location.pathname}${window.location.hash}`,
          module: 'project_tasks',
          projectId: selectedProjectId.value,
          entityType: sprintId ? 'sprint' : 'project',
          entityId: sprintId || selectedProjectId.value,
        },
        language: 'vi',
        modelProfile: props.modelProfile || 'balanced',
        providerHint: props.providerHint || 'auto',
        maximumOptions: 3,
        maximumEstimatedCostUsd: 0.08,
        cacheMode: 'bypass',
      }),
    })
    jobId.value = created.jobId
    persistSession()
    emit('started', created.jobId)
    await startPolling()
  } catch (error) {
    requestError.value = errorMessage(error, 'Không thể bắt đầu AI Action Composer.')
  } finally {
    submitting.value = false
  }
}

async function startPolling() {
  stopPolling()
  await refreshWorkflow()
  if (!plan.value && !receipt.value && !isTerminalFailure.value) {
    pollTimer = window.setInterval(() => { void refreshWorkflow() }, 900)
  }
}

function stopPolling() {
  if (pollTimer != null) window.clearInterval(pollTimer)
  pollTimer = null
}

async function refreshWorkflow() {
  if (!jobId.value || refreshInFlight) return
  refreshInFlight = true
  try {
    const [jobDetail, feed] = await Promise.all([
      apiResult<AiJobDetail>(`/api/ai/jobs/${jobId.value}`),
      apiResult<AiActivityFeed>(`/api/ai/jobs/${jobId.value}/activity?afterSequence=${activityLastSequence.value}`),
    ])
    job.value = jobDetail
    mergeActivity(feed)
    persistSession()
    if (jobDetail.status === 'succeeded') {
      await loadPlanAndDraft(jobDetail)
      stopPolling()
    } else if (['failed', 'canceled'].includes(jobDetail.status)) {
      requestError.value = jobDetail.lastErrorMessage || 'AI không thể hoàn tất yêu cầu này.'
      stopPolling()
    }
  } catch (error) {
    requestError.value = errorMessage(error, 'Mất kết nối khi theo dõi tiến trình AI.')
  } finally {
    refreshInFlight = false
  }
}

function mergeActivity(feed: AiActivityFeed) {
  const byId = new Map(activityEvents.value.map(event => [event.eventId, event]))
  for (const event of feed.events) byId.set(event.eventId, event)
  activityEvents.value = [...byId.values()].sort((left, right) => left.sequence - right.sequence)
  activityLastSequence.value = Math.max(activityLastSequence.value, feed.lastSequence)
  activityCancellable.value = feed.cancellable
}

async function loadPlanAndDraft(jobDetail: AiJobDetail) {
  if (receipt.value) return
  const result = await apiResult<AiJobResult>(`/api/ai/jobs/${jobDetail.jobId}/result`)
  sourceStale.value = result.sourceStale
  const draftId = result.draftIds[0] || jobDetail.draftIds[0]
  if (!draftId) throw new Error('AI đã hoàn tất nhưng không tạo được bản nháp để duyệt.')
  const draftDetail = await apiResult<AiDraftDetail>(`/api/ai/drafts/${draftId}`)
  draft.value = draftDetail
  const serverPlan = structuredClone(draftDetail.workingPayload || result.result)
  lastSavedPlanJson = JSON.stringify(serverPlan)
  if (
    restoredDraft?.draftId === draftDetail.draftId
    && restoredDraft.plan.projectId === serverPlan.projectId
    && restoredDraft.plan.sourceVersion === serverPlan.sourceVersion
  ) {
    if (restoredDraft.rowVersion === draftDetail.rowVersion) {
      plan.value = structuredClone(restoredDraft.plan)
      if (JSON.stringify(plan.value) !== lastSavedPlanJson) scheduleDraftAutosave()
    } else {
      recoveryPlan.value = structuredClone(restoredDraft.plan)
      plan.value = serverPlan
      requestError.value = 'Máy chủ có phiên bản mới hơn. Qaly giữ riêng bản tùy chỉnh trên máy này để bạn chọn khôi phục hoặc bỏ qua.'
    }
  } else {
    plan.value = serverPlan
  }
  restoredDraft = null
  receipt.value = draftDetail.confirmationResult?.actionReceipt ?? null
  persistSession()
}

function applyRecoveryPlan() {
  if (!recoveryPlan.value || !draft.value) return
  plan.value = structuredClone(recoveryPlan.value)
  recoveryPlan.value = null
  requestError.value = ''
  persistSession()
  scheduleDraftAutosave()
}

function discardRecoveryPlan() {
  recoveryPlan.value = null
  requestError.value = ''
  persistSession()
}

function chooseOption(option: AiActionOption) {
  if (!plan.value) return
  plan.value.review.selectedOptionId = option.optionId
  plan.value.review.selectedCommandIds = option.commands.map(command => command.commandId)
}

function toggleCommand(commandId: string) {
  if (!plan.value) return
  const ids = plan.value.review.selectedCommandIds
  plan.value.review.selectedCommandIds = ids.includes(commandId)
    ? ids.filter(id => id !== commandId)
    : [...ids, commandId]
}

function memberId(member: ProjectMemberOption) {
  return member.userId || member.id || ''
}

function memberName(id: string | null) {
  if (!id) return 'Chưa giao'
  return memberOptions.value.find(member => memberId(member) === id)?.fullName || 'Thành viên dự án'
}

function skillName(id: string) {
  return skillCatalog.value.find(skill => skill.id === id)?.name || `Kỹ năng ${id.slice(0, 6)}`
}

async function loadSkillCatalog(projectId: string) {
  skillCatalog.value = []
  if (!projectId) return
  try {
    let organizationId = props.projects.find(project => project.id === projectId)?.organizationId || null
    if (!organizationId) {
      const project = await apiResult<{ organizationId?: string | null }>(`/api/projects/${projectId}`)
      organizationId = project.organizationId || null
    }
    if (!organizationId) return
    skillCatalog.value = await apiResult<OrganizationSkillOption[]>(`/api/organizations/${organizationId}/skills`)
  } catch {
    // A missing catalog must not block review; the skill id remains visible as a truthful fallback.
    skillCatalog.value = []
  }
}

function updateAssignee(command: AiTaskCommand, value: string) {
  command.assigneeId = value || null
  command.assigneeMode = value ? 'user_selected' : 'unassigned'
}

function dueDateValue(value: string | null) {
  return value ? value.slice(0, 10) : ''
}

function updateDueDate(command: AiTaskCommand, value: string) {
  command.dueDate = value ? new Date(`${value}T23:59:59.000Z`).toISOString() : null
}

function criteriaValue(command: AiTaskCommand) {
  return command.acceptanceCriteria.join('\n')
}

function updateCriteria(command: AiTaskCommand, value: string) {
  command.acceptanceCriteria = value.split('\n').map(item => item.trim()).filter(Boolean)
}

async function confirmPlan() {
  if (!draft.value || !plan.value || selectedCount.value === 0 || submitting.value) return
  const confirmingDraftId = draft.value.draftId
  submitting.value = true
  requestError.value = ''
  try {
    if (!await saveDraftNow()) {
      throw new Error('Bản tùy chỉnh chưa đồng bộ được với máy chủ nên Qaly chưa tạo Task. Bản khôi phục vẫn được giữ nguyên.')
    }
    // Always reconcile the server-owned draft immediately before a mutation. This
    // rehydrates legacy/restored UI state that did not retain a concurrency token.
    const latest = await apiResult<AiDraftDetail>(`/api/ai/drafts/${confirmingDraftId}`)
    if (latest.status.toLowerCase() === 'confirmed' && latest.confirmationResult?.actionReceipt) {
      draft.value = latest
      receipt.value = latest.confirmationResult.actionReceipt
      await loadLatestActivity()
      if (receipt.value.status === 'succeeded') {
        showSuccess(`Bản nháp đã tạo ${latest.confirmationResult.createdTaskCount} nhiệm vụ; Qaly đã đọc lại receipt thay vì tạo trùng.`)
        emit('completed', selectedProjectId.value)
      } else {
        requestError.value = 'Task đã được ghi nhưng chưa đối chiếu canonical thành công. Hãy dùng “Đối chiếu lại”; Qaly sẽ không tạo thêm Task.'
      }
      return
    }
    if (!latest.rowVersion?.trim()) {
      throw new Error('Bản nháp trên máy chủ chưa có rowVersion. Hãy bỏ bản nháp legacy và soạn lại; chưa có Task nào được tạo.')
    }
    if (draft.value.rowVersion?.trim() && draft.value.rowVersion !== latest.rowVersion) {
      draft.value = latest
      plan.value = structuredClone(latest.workingPayload)
      throw new Error('Bản nháp đã thay đổi trên máy chủ. Qaly đã tải phiên bản mới; vui lòng xem lại trước khi xác nhận.')
    }
    draft.value = { ...draft.value, rowVersion: latest.rowVersion }

    // Stable across retries: a lost HTTP response must replay the same server execution.
    const key = `action-confirm:${confirmingDraftId}`
    const result = await apiResult<AiDraftConfirmResult>(`/api/ai/drafts/${confirmingDraftId}/confirm`, {
      method: 'POST',
      headers: { 'Idempotency-Key': key },
      body: JSON.stringify({
        editedPayloadJson: JSON.stringify(plan.value),
        confirmAction: 'execute_action_set',
        confirmationNote: 'Đã xem, chỉnh sửa và xác nhận từ AI Action Composer.',
        rowVersion: latest.rowVersion,
        idempotencyKey: key,
      }),
    })
    receipt.value = result.actionReceipt
    draft.value.status = result.status
    await loadLatestActivity()
    persistSession()
    if (receipt.value?.status === 'succeeded') {
      showSuccess(`Đã tạo và đối chiếu ${result.createdTaskCount} nhiệm vụ sau khi bạn xác nhận.`)
      emit('completed', selectedProjectId.value)
    } else {
      requestError.value = 'Task đã được ghi nhưng bước đọc lại dữ liệu thật chưa khớp. Qaly đã khóa tạo lặp; hãy đối chiếu lại.'
      showError(requestError.value)
    }
  } catch (error) {
    // The transaction may have committed even when the browser lost its response.
    // Read the canonical draft before allowing another mutation attempt.
    try {
      const latest = await apiResult<AiDraftDetail>(`/api/ai/drafts/${confirmingDraftId}`)
      if (latest.status.toLowerCase() === 'confirmed' && latest.confirmationResult?.actionReceipt) {
        draft.value = latest
        receipt.value = latest.confirmationResult.actionReceipt
        requestError.value = ''
        await loadLatestActivity()
        persistSession()
        if (receipt.value.status === 'succeeded') {
          showSuccess(`Đã tạo ${latest.confirmationResult.createdTaskCount} nhiệm vụ và đối chiếu lại dữ liệu thành công.`)
          emit('completed', selectedProjectId.value)
        } else {
          requestError.value = 'Task đã được ghi nhưng chưa đối chiếu canonical thành công. Qaly sẽ chỉ đọc lại, không tạo lần hai.'
        }
        return
      }
    } catch {
      // Preserve the original execution error when canonical reconciliation is unavailable.
    }
    requestError.value = errorMessage(error, 'Không thể thực thi bản nháp. Dữ liệu chưa bị thay đổi.')
    showError(requestError.value)
  } finally {
    submitting.value = false
  }
}

async function retryVerification() {
  if (!draft.value || !receipt.value || submitting.value || receiptVerified.value) return
  submitting.value = true
  requestError.value = ''
  try {
    const key = `action-confirm:${draft.value.draftId}`
    const result = await apiResult<AiDraftConfirmResult>(`/api/ai/drafts/${draft.value.draftId}/confirm`, {
      method: 'POST',
      headers: { 'Idempotency-Key': key },
      body: JSON.stringify({
        editedPayloadJson: JSON.stringify(plan.value || draft.value.workingPayload),
        confirmAction: 'execute_action_set',
        confirmationNote: 'Chỉ đối chiếu lại lần thực thi đã có; không tạo thêm Task.',
        rowVersion: draft.value.rowVersion,
        idempotencyKey: key,
      }),
    })
    receipt.value = result.actionReceipt
    await loadLatestActivity()
    persistSession()
    if (receipt.value?.status === 'succeeded') {
      showSuccess(`Đã đối chiếu ${result.createdTaskCount} Task với dữ liệu thật; không tạo thêm Task.`)
      emit('completed', selectedProjectId.value)
    } else {
      requestError.value = 'Dữ liệu vẫn chưa khớp receipt. Qaly tiếp tục khóa tạo lặp; hãy mở liên kết Task hoặc kiểm tra nhật ký.'
    }
  } catch (error) {
    requestError.value = errorMessage(error, 'Không thể đối chiếu lại lúc này. Không có Task mới nào được tạo.')
    showError(requestError.value)
  } finally {
    submitting.value = false
  }
}

async function rejectPlan() {
  if (!draft.value) return
  submitting.value = true
  try {
    const key = newIdempotencyKey('action-reject')
    await apiResult(`/api/ai/drafts/${draft.value.draftId}/reject`, {
      method: 'POST',
      headers: { 'Idempotency-Key': key },
      body: JSON.stringify({
        reason: 'Người dùng bỏ bản nháp trong AI Action Composer.',
        rowVersion: draft.value.rowVersion,
        idempotencyKey: key,
      }),
    })
    showSuccess('Đã bỏ bản nháp. Không có dữ liệu nào bị thay đổi.')
    resetSession()
  } catch (error) {
    requestError.value = errorMessage(error, 'Không thể bỏ bản nháp.')
  } finally {
    submitting.value = false
  }
}

async function cancelJob() {
  if (!jobId.value) return
  submitting.value = true
  try {
    job.value = await apiResult<AiJobDetail>(`/api/ai/jobs/${jobId.value}/cancel`, {
      method: 'POST',
      body: JSON.stringify({ reason: 'Người dùng hủy từ AI Action Composer.' }),
    })
    await loadLatestActivity()
    requestError.value = 'Đã hủy tác vụ. Không có dữ liệu nào bị thay đổi.'
    stopPolling()
  } catch (error) {
    requestError.value = errorMessage(error, 'Không thể hủy tác vụ AI.')
  } finally {
    submitting.value = false
  }
}

async function retryJob() {
  if (!jobId.value) return
  submitting.value = true
  requestError.value = ''
  try {
    job.value = await apiResult<AiJobDetail>(`/api/ai/jobs/${jobId.value}/retry`, {
      method: 'POST',
      body: JSON.stringify({ providerOverride: 'deepseek-chat' }),
    })
    await startPolling()
  } catch (error) {
    requestError.value = errorMessage(error, 'Không thể thử lại tác vụ AI.')
  } finally {
    submitting.value = false
  }
}

async function loadLatestActivity() {
  if (!jobId.value) return
  const feed = await apiResult<AiActivityFeed>(`/api/ai/jobs/${jobId.value}/activity?afterSequence=${activityLastSequence.value}`)
  mergeActivity(feed)
}

function eventIcon(event: AiActivityEvent) {
  if (event.status === 'succeeded') return Check
  if (event.status === 'failed' || event.status === 'warning') return AlertTriangle
  if (event.status === 'waiting_user') return Clock3
  if (event.status === 'running') return LoaderCircle
  return Circle
}

function eventDuration(event: AiActivityEvent) {
  if (event.durationMs == null) return ''
  if (event.durationMs < 1000) return `${event.durationMs} ms`
  return `${(event.durationMs / 1000).toFixed(1)} s`
}

function isInternalLink(value: string) {
  return value.startsWith('/')
}
</script>

<template>
  <Teleport to="body" :disabled="props.embedded">
    <div
      class="action-composer-backdrop"
      :class="{ 'is-embedded': props.embedded }"
      @click.self="!props.embedded && emit('close')"
    >
      <aside
        class="action-composer"
        :class="{ 'is-embedded': props.embedded }"
        :role="props.embedded ? 'region' : 'dialog'"
        :aria-modal="props.embedded ? undefined : true"
        aria-label="AI Action Composer"
      >
        <header class="action-composer__header">
          <div class="action-composer__heading">
            <span class="action-composer__mark"><Sparkles :size="18" /></span>
            <div>
              <h2>AI Action Composer</h2>
              <p>Soạn hành động, bạn duyệt rồi hệ thống mới thực hiện</p>
            </div>
          </div>
          <div class="action-composer__header-actions">
            <span class="model-chip" title="Model thực tế sẽ được backend xác nhận sau khi định tuyến">
              <span></span> {{ modelLabel }}
            </span>
            <button class="icon-close" type="button" aria-label="Đóng" @click="emit('close')"><X :size="18" /></button>
          </div>
        </header>

        <div class="action-composer__body">
          <section v-if="!jobId" class="artifact-waiting" aria-live="polite">
            <span class="artifact-waiting__icon"><Sparkles :size="22" /></span>
            <div>
              <strong>Bản nháp sẽ xuất hiện ở đây</strong>
              <p>Hãy mô tả mục tiêu trong khung chat. Trợ lý AI sẽ hỏi phần còn thiếu, sau đó tự mở bản nháp có cấu trúc để bạn kiểm tra.</p>
            </div>
            <span class="safety-note"><CheckCircle2 :size="15" /> Không thay đổi dữ liệu trước khi bạn xác nhận</span>
          </section>

          <section v-else-if="isProcessing || isTerminalFailure" class="composer-progress" role="status" aria-live="polite">
            <div class="worked-chip"><Clock3 :size="16" /> Đã chạy {{ workedFor }} <ChevronRight :size="15" /></div>
            <div class="progress-heading">
              <div>
                <span class="eyebrow">JOB {{ jobId.slice(0, 8) }}</span>
                <h3>{{ isTerminalFailure ? 'AI chưa hoàn tất' : 'AI đang thực hiện yêu cầu' }}</h3>
              </div>
              <span class="status-chip" :class="`is-${job?.status || 'queued'}`">{{ job?.status || 'queued' }}</span>
            </div>

            <ol class="activity-timeline" aria-live="polite">
              <li v-for="event in activityEvents" :key="event.eventId" :class="`is-${event.status}`">
                <span class="activity-icon"><component :is="eventIcon(event)" :class="{ spin: event.status === 'running' }" :size="15" /></span>
                <div>
                  <strong>{{ event.publicLabel }}</strong>
                  <small v-if="event.current != null && event.total != null">{{ event.current }}/{{ event.total }}</small>
                  <small v-else-if="eventDuration(event)">{{ eventDuration(event) }}</small>
                  <small v-else>{{ event.status }}</small>
                </div>
              </li>
              <li v-if="activityEvents.length === 0" class="is-running">
                <span class="activity-icon"><LoaderCircle class="spin" :size="15" /></span>
                <div><strong>Đang đưa yêu cầu vào hàng đợi an toàn</strong><small>queued</small></div>
              </li>
            </ol>

            <div v-if="queueDelayed && !requestError" class="queue-delayed-warning" role="status">
              <AlertTriangle :size="16" />
              <div>
                <strong>Worker chưa nhận yêu cầu sau {{ queueWaitSeconds }} giây</strong>
                <span>Qaly vẫn đang kiểm tra tự động. Nếu trạng thái không đổi, hãy hủy an toàn và thử lại sau khi kiểm tra worker.</span>
              </div>
            </div>
            <div v-if="requestError" class="truthful-error" role="alert"><AlertTriangle :size="16" /> {{ requestError }}</div>
            <div class="composer-actions">
              <button class="secondary-action" type="button" @click="resetSession"><RotateCcw :size="15" /> Phiên mới</button>
              <button v-if="activityCancellable && !isTerminalFailure" class="danger-action" type="button" :disabled="submitting" @click="cancelJob"><Square :size="14" /> Hủy</button>
              <button v-if="isTerminalFailure && canRetry" class="primary-action" type="button" :disabled="submitting" @click="retryJob"><RotateCcw :size="15" /> Thử lại</button>
            </div>
          </section>

          <section v-else-if="plan && draft && !receipt" class="composer-review">
            <div class="review-banner">
              <CheckCircle2 :size="18" />
              <div><strong>Đã soạn xong — chưa thay đổi dữ liệu</strong><p>Chọn phương án, sửa nội dung và chỉ xác nhận các task bạn muốn tạo.</p></div>
            </div>

            <div v-if="recoveryPlan" class="recovery-banner" role="status">
              <AlertTriangle :size="17" />
              <div>
                <strong>Có bản tùy chỉnh chưa đồng bộ từ phiên trước</strong>
                <p>Bản trên máy chủ đang được giữ nguyên. Bạn có thể khôi phục bản của mình hoặc bỏ qua.</p>
                <div class="recovery-actions">
                  <button type="button" class="secondary-action" @click="applyRecoveryPlan">Khôi phục bản của tôi</button>
                  <button type="button" class="secondary-action" @click="discardRecoveryPlan">Dùng bản máy chủ</button>
                </div>
              </div>
            </div>

            <div v-if="sourceStale" class="truthful-error" role="alert"><AlertTriangle :size="16" /> Nguồn dữ liệu đã thay đổi. Hãy tạo phiên mới trước khi xác nhận.</div>

            <div class="intent-card">
              <span class="eyebrow">Ý định đã hiểu</span>
              <strong>{{ plan.userIntent }}</strong>
              <small>{{ Math.round(plan.confidence * 100) }}% confidence · {{ selectedProject?.name }}</small>
            </div>

            <div class="option-tabs" role="tablist" aria-label="Phương án AI">
              <button
                v-for="option in plan.options"
                :key="option.optionId"
                type="button"
                :class="{ 'is-active': option.optionId === plan.review.selectedOptionId }"
                :aria-pressed="option.optionId === plan.review.selectedOptionId"
                @click="chooseOption(option)"
              >
                <strong>{{ option.label }}</strong><small>{{ option.commands.length }} task</small>
              </button>
            </div>

            <div v-if="selectedOption" class="option-summary">
              <p>{{ selectedOption.summary }}</p>
              <ul v-if="selectedOption.tradeOffs.length"><li v-for="item in selectedOption.tradeOffs" :key="item">{{ item }}</li></ul>
            </div>

            <div class="command-list">
              <article v-for="(command, index) in selectedOption?.commands" :key="command.commandId" class="command-card" :class="{ 'is-selected': selectedCommandIds.has(command.commandId) }">
                <label class="command-select">
                  <input type="checkbox" :checked="selectedCommandIds.has(command.commandId)" @change="toggleCommand(command.commandId)" />
                  <span>Task {{ index + 1 }}</span>
                </label>
                <div class="command-fields">
                  <label>Tiêu đề<input v-model.trim="command.title" maxlength="240" /></label>
                  <label>Mô tả<textarea v-model="command.description" rows="2" maxlength="4000"></textarea></label>
                  <label class="wide-field">Tiêu chí nghiệm thu<textarea :value="criteriaValue(command)" rows="2" @input="updateCriteria(command, ($event.target as HTMLTextAreaElement).value)"></textarea></label>
                  <label>Ưu tiên<select v-model="command.priority"><option>Low</option><option>Medium</option><option>High</option><option>Critical</option></select></label>
                  <label>Hạn chót<input type="date" :value="dueDateValue(command.dueDate)" @input="updateDueDate(command, ($event.target as HTMLInputElement).value)" /></label>
                  <label>Ước lượng (giờ)<input v-model.number="command.estimatedHours" type="number" min="1" max="1000" /></label>
                  <label>Người thực hiện
                    <select :value="command.assigneeId || ''" @change="updateAssignee(command, ($event.target as HTMLSelectElement).value)">
                      <option value="">Chưa giao</option>
                      <option v-for="member in memberOptions" :key="memberId(member)" :value="memberId(member)">{{ member.fullName }}</option>
                    </select>
                    <small v-if="command.assigneeMode === 'system_suggested'" class="assignment-hint">Qaly đề xuất từ kỹ năng đã xác nhận, lịch Sprint và capacity đa dự án.</small>
                    <small v-else-if="!command.assigneeId" class="assignment-hint">Chưa đủ bằng chứng an toàn để tự giao; bạn vẫn có thể chọn người trước khi xác nhận.</small>
                  </label>
                </div>
                <div v-if="command.requiredSkills.length" class="skill-row">
                  <span v-for="skill in command.requiredSkills" :key="skill.skillId">{{ skillName(skill.skillId) }} · {{ skill.requiredLevel }}</span>
                </div>
                <div class="source-row">
                  <span>Nguồn:</span>
                  <template v-for="source in command.sourceRefs" :key="source">
                    <RouterLink v-if="isInternalLink(source)" :to="source">{{ source }}</RouterLink>
                  </template>
                </div>
              </article>
            </div>

            <details class="activity-details">
              <summary><Clock3 :size="15" /> Tiến trình AI · đã chạy {{ workedFor }}</summary>
              <ol class="activity-timeline is-compact">
                <li v-for="event in activityEvents" :key="event.eventId" :class="`is-${event.status}`">
                  <span class="activity-icon"><component :is="eventIcon(event)" :size="14" /></span>
                  <div><strong>{{ event.publicLabel }}</strong><small>{{ eventDuration(event) || event.status }}</small></div>
                </li>
              </ol>
            </details>

            <div v-if="requestError" class="truthful-error" role="alert"><AlertTriangle :size="16" /> {{ requestError }}</div>
            <div class="composer-actions composer-actions--sticky">
              <button class="danger-action" type="button" :disabled="submitting" @click="rejectPlan">Bỏ bản nháp</button>
              <span class="selection-count">
                Đã chọn {{ selectedCount }} task ·
                {{ draftSaveState === 'saving' ? 'đang lưu' : draftSaveState === 'error' ? 'có bản khôi phục' : 'đã lưu' }}
              </span>
              <button class="primary-action" type="button" :disabled="submitting || sourceStale || selectedCount === 0" @click="confirmPlan">
                <LoaderCircle v-if="submitting" class="spin" :size="16" /><Check v-else :size="16" /> Xác nhận và tạo task
              </button>
            </div>
          </section>

          <section v-else-if="receipt" class="composer-receipt">
            <div v-if="receiptVerified" class="receipt-success"><CheckCircle2 :size="28" /><h3>Đã thực hiện sau khi bạn xác nhận</h3><p>Mỗi kết quả bên dưới đã được đọc lại từ dữ liệu thật.</p></div>
            <div v-else class="receipt-verification-warning" role="alert"><AlertTriangle :size="26" /><h3>Đã khóa tạo lặp — cần đối chiếu lại</h3><p>Task có thể đã được ghi, nhưng receipt chưa khớp dữ liệu canonical. Nút bên dưới chỉ đọc lại, không tạo thêm.</p></div>
            <div v-if="requestError" class="truthful-error" role="alert"><AlertTriangle :size="16" /> {{ requestError }}</div>
            <div class="receipt-meta">
              <span>Execution {{ receipt.executionId.slice(0, 8) }}</span>
              <span>{{ modelLabel }}</span>
            </div>
            <div class="receipt-list">
              <article v-for="item in receipt.commandResults" :key="item.commandId" :class="`is-${item.status}`">
                <CheckCircle2 v-if="item.status === 'succeeded'" :size="18" />
                <AlertTriangle v-else :size="18" />
                <div><strong>{{ item.entityLabel || item.commandId }}</strong><small>{{ item.appliedSkillCount }} skill tag đã áp dụng · {{ item.status }}</small></div>
                <RouterLink v-if="item.entityUrl" :to="item.entityUrl" title="Mở task"><ExternalLink :size="16" /></RouterLink>
              </article>
            </div>
            <details class="activity-details" open>
              <summary><Clock3 :size="15" /> Nhật ký thực thi</summary>
              <ol class="activity-timeline is-compact">
                <li v-for="event in activityEvents" :key="event.eventId" :class="`is-${event.status}`">
                  <span class="activity-icon"><component :is="eventIcon(event)" :size="14" /></span>
                  <div><strong>{{ event.publicLabel }}</strong><small>{{ eventDuration(event) || event.status }}</small></div>
                </li>
              </ol>
            </details>
            <div class="composer-actions">
              <button class="secondary-action" type="button" @click="emit('close')">Đóng</button>
              <button v-if="!receiptVerified" class="primary-action" type="button" :disabled="submitting" @click="retryVerification"><RotateCcw :size="15" /> Đối chiếu lại</button>
              <button v-else class="primary-action" type="button" @click="resetSession"><Sparkles :size="15" /> Soạn yêu cầu mới</button>
            </div>
          </section>
        </div>
      </aside>
    </div>
  </Teleport>
</template>

<style scoped>
.action-composer-backdrop { position: fixed; inset: 0; z-index: 120; display: flex; justify-content: flex-end; background: rgba(15, 23, 42, .42); }
.action-composer { width: min(680px, 100vw); height: 100dvh; display: grid; grid-template-rows: auto minmax(0, 1fr); color: var(--text); background: var(--panel); border-left: 1px solid var(--line); box-shadow: -24px 0 60px rgba(15, 23, 42, .18); }
.action-composer-backdrop.is-embedded { position: static; inset: auto; z-index: auto; width: 100%; height: 100%; min-height: 0; background: transparent; }
.action-composer.is-embedded { width: 100%; height: 100%; min-height: 0; border-left: 0; box-shadow: none; }
.action-composer.is-embedded .action-composer__header { display: none; }
.action-composer__header { min-height: 72px; display: flex; align-items: center; justify-content: space-between; gap: 16px; padding: 14px 18px; border-bottom: 1px solid var(--line); background: var(--panel); }
.action-composer__heading, .action-composer__header-actions, .composer-actions, .review-banner, .worked-chip, .receipt-meta, .command-select, .safety-note { display: flex; align-items: center; }
.action-composer__heading { gap: 11px; min-width: 0; }
.action-composer__heading h2 { font-size: 17px; }
.action-composer__heading p { font-size: 11px; }
.action-composer__mark { width: 36px; height: 36px; display: grid; place-items: center; flex: 0 0 auto; border-radius: 12px; color: white; background: linear-gradient(135deg, #2563eb, #7c3aed); }
.action-composer__header-actions { gap: 8px; }
.model-chip { display: inline-flex; align-items: center; gap: 6px; padding: 7px 10px; border: 1px solid #bfdbfe; border-radius: 999px; color: #1d4ed8; background: #eff6ff; font-size: 11px; font-weight: 800; white-space: nowrap; }
.model-chip > span { width: 7px; height: 7px; border-radius: 50%; background: #10b981; }
.icon-close { width: 34px; height: 34px; display: grid; place-items: center; border: 1px solid var(--line); border-radius: 10px; color: var(--text); background: var(--panel-soft); }
.action-composer__body { min-height: 0; overflow: auto; padding: 20px; }
.composer-input, .composer-progress, .composer-review, .composer-receipt, .artifact-waiting { display: grid; gap: 16px; }
.artifact-waiting { min-height: 100%; place-content: center; justify-items: center; padding: 32px; color: var(--muted); text-align: center; }
.artifact-waiting__icon { width: 52px; height: 52px; display: grid; place-items: center; border-radius: 16px; color: var(--primary); background: color-mix(in srgb, var(--primary) 10%, var(--panel)); }
.artifact-waiting strong { display: block; margin-bottom: 6px; color: var(--text-strong); font-size: 18px; }
.artifact-waiting p { max-width: 48ch; line-height: 1.55; }
.composer-intro { display: grid; gap: 6px; padding: 18px; border: 1px solid #dbeafe; border-radius: 16px; background: linear-gradient(135deg, #eff6ff, #f5f3ff); }
.composer-intro strong { font-size: 18px; }
.field-label { margin-bottom: -9px; color: var(--text); font-size: 12px; font-weight: 800; }
.composer-field, .command-fields input, .command-fields select, .command-fields textarea { width: 100%; border: 1px solid var(--line); border-radius: 11px; padding: 10px 12px; color: var(--text); background: var(--panel-soft); outline: 0; }
.composer-field:focus, .command-fields input:focus, .command-fields select:focus, .command-fields textarea:focus { border-color: #60a5fa; box-shadow: 0 0 0 3px rgba(59, 130, 246, .12); }
.composer-prompt { resize: vertical; min-height: 142px; line-height: 1.55; }
.field-help { margin-top: -12px; color: var(--muted); text-align: right; }
.example-list { display: grid; gap: 7px; }
.example-list button { display: flex; align-items: flex-start; gap: 8px; padding: 9px 11px; border: 1px solid var(--line); border-radius: 10px; color: var(--muted); background: var(--panel-soft); text-align: left; }
.example-list button:hover { border-color: #93c5fd; color: var(--text); }
.composer-actions { justify-content: flex-end; gap: 10px; padding-top: 4px; }
.composer-actions--sticky { position: sticky; bottom: -20px; margin: 0 -20px -20px; padding: 14px 20px; border-top: 1px solid var(--line); background: color-mix(in srgb, var(--panel) 94%, transparent); backdrop-filter: blur(12px); }
.safety-note, .selection-count { margin-right: auto; gap: 6px; color: #047857; font-size: 11px; font-weight: 700; }
.primary-action, .secondary-action, .danger-action { display: inline-flex; align-items: center; justify-content: center; gap: 7px; min-height: 38px; padding: 8px 13px; border-radius: 10px; font-weight: 800; }
.primary-action { border: 1px solid #2563eb; color: white; background: #2563eb; }
.secondary-action { border: 1px solid var(--line); color: var(--text); background: var(--panel-soft); }
.danger-action { border: 1px solid #fecaca; color: #b91c1c; background: #fff1f2; }
button:disabled { cursor: not-allowed; opacity: .55; }
.worked-chip { width: fit-content; gap: 7px; padding: 8px 11px; border: 1px solid #93c5fd; border-radius: 999px; color: #1d4ed8; background: #eff6ff; font-weight: 800; }
.progress-heading { display: flex; align-items: center; justify-content: space-between; gap: 12px; }
.eyebrow { color: #2563eb; font-size: 10px; font-weight: 900; letter-spacing: .08em; text-transform: uppercase; }
.status-chip { padding: 5px 9px; border-radius: 999px; color: #1d4ed8; background: #dbeafe; font-size: 10px; font-weight: 900; text-transform: uppercase; }
.status-chip.is-failed, .status-chip.is-canceled { color: #b91c1c; background: #fee2e2; }
.activity-timeline { display: grid; gap: 0; margin: 0; padding: 0; list-style: none; }
.activity-timeline li { position: relative; display: grid; grid-template-columns: 30px minmax(0, 1fr); gap: 9px; min-height: 54px; }
.activity-timeline li:not(:last-child)::after { content: ''; position: absolute; top: 28px; bottom: 0; left: 14px; width: 1px; background: var(--line); }
.activity-icon { width: 29px; height: 29px; display: grid; place-items: center; z-index: 1; border: 1px solid var(--line); border-radius: 50%; color: var(--muted); background: var(--panel); }
.activity-timeline li > div { display: flex; align-items: baseline; justify-content: space-between; gap: 8px; padding-top: 5px; }
.activity-timeline small { color: var(--muted); white-space: nowrap; }
.activity-timeline .is-succeeded .activity-icon { border-color: #a7f3d0; color: #059669; background: #ecfdf5; }
.activity-timeline .is-running .activity-icon { border-color: #93c5fd; color: #2563eb; background: #eff6ff; }
.activity-timeline .is-failed .activity-icon, .activity-timeline .is-warning .activity-icon { border-color: #fecaca; color: #dc2626; background: #fff1f2; }
.activity-timeline.is-compact li { min-height: 44px; }
.truthful-error { display: flex; align-items: flex-start; gap: 8px; padding: 11px 12px; border: 1px solid #fecaca; border-radius: 11px; color: #b91c1c; background: #fff1f2; }
.queue-delayed-warning { display: flex; align-items: flex-start; gap: 8px; padding: 11px 12px; border: 1px solid #fde68a; border-radius: 11px; color: #92400e; background: #fffbeb; }
.queue-delayed-warning>div { display: grid; gap: 3px; }
.queue-delayed-warning span { font-size: 12px; line-height: 1.45; }
.review-banner { align-items: flex-start; gap: 10px; padding: 13px; border: 1px solid #a7f3d0; border-radius: 12px; color: #047857; background: #ecfdf5; }
.review-banner p { margin-top: 3px; color: #047857; }
.recovery-banner, .receipt-verification-warning { display: flex; align-items: flex-start; gap: 10px; padding: 14px; border: 1px solid #fbbf24; border-radius: 12px; color: #92400e; background: #fffbeb; }
.recovery-banner > div, .receipt-verification-warning { gap: 5px; }
.recovery-banner p, .receipt-verification-warning p { margin-top: 3px; line-height: 1.45; }
.recovery-actions { display: flex; flex-wrap: wrap; gap: 8px; margin-top: 10px; }
.receipt-verification-warning { display: grid; justify-items: center; padding: 22px; text-align: center; }
.intent-card { display: grid; gap: 5px; padding: 13px; border: 1px solid var(--line); border-radius: 12px; background: var(--panel-soft); }
.option-tabs { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 8px; }
.option-tabs button { display: grid; gap: 2px; padding: 10px; border: 1px solid var(--line); border-radius: 11px; color: var(--text); background: var(--panel-soft); text-align: left; }
.option-tabs button.is-active { border-color: #60a5fa; color: #1d4ed8; background: #eff6ff; box-shadow: inset 0 0 0 1px #60a5fa; }
.option-tabs small { color: var(--muted); }
.option-summary { display: grid; gap: 6px; }
.option-summary ul { margin: 0; padding-left: 18px; color: var(--muted); }
.command-list { display: grid; gap: 12px; }
.command-card { display: grid; gap: 11px; padding: 14px; border: 1px solid var(--line); border-radius: 14px; background: var(--panel); opacity: .7; }
.command-card.is-selected { border-color: #93c5fd; opacity: 1; box-shadow: 0 8px 22px rgba(37, 99, 235, .08); }
.command-select { gap: 8px; font-weight: 900; }
.command-fields { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 10px; }
.command-fields label { display: grid; gap: 5px; color: var(--muted); font-size: 10px; font-weight: 800; }
.command-fields label:first-child, .command-fields .wide-field { grid-column: 1 / -1; }
.command-fields textarea { resize: vertical; }
.skill-row, .source-row { display: flex; flex-wrap: wrap; align-items: center; gap: 6px; }
.skill-row span { padding: 4px 7px; border-radius: 999px; color: #6d28d9; background: #ede9fe; font-size: 10px; font-weight: 800; }
.assignment-hint { color: var(--muted); font-size: 9px; font-weight: 600; line-height: 1.35; }
.source-row { color: var(--muted); font-size: 10px; }
.source-row a { max-width: 230px; overflow: hidden; color: #2563eb; text-overflow: ellipsis; white-space: nowrap; }
.activity-details { border: 1px solid var(--line); border-radius: 12px; padding: 11px 12px; background: var(--panel-soft); }
.activity-details summary { display: flex; align-items: center; gap: 7px; cursor: pointer; font-weight: 800; }
.activity-details[open] summary { margin-bottom: 12px; }
.receipt-success { display: grid; justify-items: center; gap: 6px; padding: 24px; color: #059669; text-align: center; }
.receipt-success h3 { color: var(--text); font-size: 19px; }
.receipt-meta { justify-content: space-between; gap: 10px; padding: 10px 12px; border-radius: 10px; color: var(--muted); background: var(--panel-soft); }
.receipt-list { display: grid; gap: 9px; }
.receipt-list article { display: grid; grid-template-columns: auto minmax(0, 1fr) auto; align-items: center; gap: 10px; padding: 12px; border: 1px solid #a7f3d0; border-radius: 11px; color: #047857; background: #ecfdf5; }
.receipt-list article:not(.is-succeeded) { border-color: #fbbf24; color: #b45309; background: #fffbeb; }
.receipt-list article > div { display: grid; gap: 2px; color: var(--text); }
.receipt-list a { color: #2563eb; }
.spin { animation: spin .8s linear infinite; }
@keyframes spin { to { transform: rotate(360deg); } }
@media (max-width: 640px) {
  .action-composer__header { align-items: flex-start; }
  .action-composer__heading p, .model-chip { display: none; }
  .action-composer__body { padding: 14px; }
  .option-tabs { grid-template-columns: 1fr; }
  .command-fields { grid-template-columns: 1fr; }
  .command-fields label:first-child, .command-fields .wide-field { grid-column: auto; }
  .composer-actions--sticky { bottom: -14px; margin: 0 -14px -14px; padding: 12px 14px; flex-wrap: wrap; }
}
</style>
