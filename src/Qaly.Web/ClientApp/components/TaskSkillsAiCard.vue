<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue'
import {
  AlertTriangle,
  Ban,
  Check,
  CheckCircle2,
  ExternalLink,
  LoaderCircle,
  Plus,
  RefreshCw,
  RotateCcw,
  Save,
  Sparkles,
  Tags,
  X,
} from 'lucide-vue-next'
import { ApiError, apiResult, errorMessage } from '../utils/api-client'

type SkillLevel = 'Familiar' | 'Proficient' | 'Expert'

type OrganizationSkill = {
  id: string
  organizationId: string
  name: string
  normalizedName: string
  description: string | null
  isActive: boolean
  rowVersion: string
}

type TaskSkillRequirement = {
  id: string
  skillId: string
  name: string
  description: string | null
  requiredLevel: SkillLevel
  provenance: 'MANUAL' | 'AI_CONFIRMED'
  confirmedByUserId: string
  confirmedAt: string
  rowVersion: string
}

type TaskSkills = {
  taskId: string
  projectId: string
  organizationId: string | null
  availability: 'ready' | 'catalog_empty' | 'organization_required'
  canManage: boolean
  canManageCatalog: boolean
  taskRowVersion: string
  requirements: TaskSkillRequirement[]
}

type ManualSelection = {
  skillId: string
  requiredLevel: SkillLevel
}

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
  draftIds: string[]
  scopeSourceType?: string | null
  scopeSourceEntityId?: string | null
}

type JobDetail = JobSummary & {
  lastErrorMessage: string | null
  lastErrorRetryable: boolean
  cacheHit: boolean
  selectedProvider: string | null
  selectedModel: string | null
  mockReason: string | null
  rowVersion: string
}

type SkillSuggestion = {
  skillId: string
  canonicalName: string
  requiredLevel: SkillLevel
  confidence: number
  rationale: string
  sourceRefs: string[]
}

type SkillSuggestionOutput = {
  schemaId: 'task_skill_suggestion.v1'
  taskId: string
  sourceVersion: string
  dataState: 'ready' | 'empty'
  suggestions: SkillSuggestion[]
  unmappedTerms: string[]
  generatedAt: string
}

type JobResult = {
  jobId: string
  schemaId: string
  schemaVersion: string
  result: SkillSuggestionOutput
  draftIds: string[]
  cacheHit: boolean
  isMock: boolean
  mockReason: string | null
  sourceStale: boolean
}

type DraftDetail = {
  draftId: string
  aiJobId: string
  projectId: string
  draftType: string
  status: string
  originalPayload: SkillSuggestionOutput
  workingPayload: SkillSuggestionOutput
  schemaId: string | null
  rowVersion: string
}

type ReviewSuggestion = SkillSuggestion & { selected: boolean }

const props = defineProps<{
  taskId: string
  projectId: string
  organizationId: string | null
}>()

const levels: SkillLevel[] = ['Familiar', 'Proficient', 'Expert']
const taskSkills = ref<TaskSkills | null>(null)
const catalog = ref<OrganizationSkill[]>([])
const manualSelections = ref<ManualSelection[]>([])
const selectedCatalogSkillId = ref('')
const selectedCatalogLevel = ref<SkillLevel>('Proficient')
const newSkillName = ref('')
const newSkillDescription = ref('')
const loading = ref(false)
const manualSaving = ref(false)
const catalogSaving = ref(false)
const actionPending = ref(false)
const job = ref<JobDetail | null>(null)
const result = ref<JobResult | null>(null)
const draft = ref<DraftDetail | null>(null)
const reviewSuggestions = ref<ReviewSuggestion[]>([])
const message = ref('')
const successMessage = ref('')
const manualOnly = ref(false)
let pollTimer: number | null = null

const scopeKey = computed(() => `${props.projectId}:${props.taskId}:${props.organizationId ?? 'none'}`)
const isActive = computed(() =>
  Boolean(job.value && ['queued', 'running', 'retrying'].includes(job.value.status)),
)
const canRetry = computed(() =>
  Boolean(
    job.value &&
      ['failed', 'canceled'].includes(job.value.status) &&
      job.value.attemptCount < job.value.maxAttempts,
  ),
)
const canReviewDraft = computed(() =>
  Boolean(
    draft.value &&
      ['pending', 'pendingreview'].includes(draft.value.status.toLowerCase()),
  ),
)
const selectedReviewCount = computed(() =>
  reviewSuggestions.value.filter(item => item.selected).length,
)
const availableCatalog = computed(() => {
  const selected = new Set(manualSelections.value.map(item => item.skillId))
  return catalog.value.filter(item => item.isActive && !selected.has(item.id))
})
const currentRequirementsBySkill = computed(
  () => new Map((taskSkills.value?.requirements ?? []).map(item => [item.skillId, item])),
)
const statusLabel = computed(() => {
  if (result.value?.sourceStale) return 'Nguồn đã thay đổi'
  if (draft.value?.status.toLowerCase() === 'confirmed') return 'Đã áp dụng'
  if (draft.value?.status.toLowerCase() === 'rejected') return 'Đã từ chối'
  return (
    {
      queued: 'Đang chờ',
      running: 'Đang phân tích',
      retrying: 'Đang thử lại',
      succeeded: result.value?.result.dataState === 'empty' ? 'Không có đề xuất' : 'Chờ duyệt',
      failed: 'Không thể tạo',
      canceled: 'Đã hủy',
    } as Record<string, string>
  )[job.value?.status ?? ''] ?? 'Chưa chạy'
})

function clearPoll() {
  if (pollTimer != null) {
    window.clearTimeout(pollTimer)
    pollTimer = null
  }
}

function schedulePoll(expectedScope: string) {
  clearPoll()
  if (!isActive.value) return
  pollTimer = window.setTimeout(
    () => void hydrateJob(job.value!.jobId, true, expectedScope),
    2000,
  )
}

function newIdempotencyKey() {
  return window.crypto.randomUUID()
}

async function restore() {
  const expectedScope = scopeKey.value
  clearPoll()
  loading.value = true
  message.value = ''
  successMessage.value = ''
  manualOnly.value = false
  taskSkills.value = null
  catalog.value = []
  manualSelections.value = []
  job.value = null
  result.value = null
  draft.value = null
  reviewSuggestions.value = []

  try {
    const loadedSkills = await apiResult<TaskSkills>(`/api/tasks/${props.taskId}/skills`)
    if (scopeKey.value !== expectedScope) return
    taskSkills.value = loadedSkills
    manualSelections.value = loadedSkills.requirements.map(item => ({
      skillId: item.skillId,
      requiredLevel: item.requiredLevel,
    }))

    if (props.organizationId) {
      catalog.value = await apiResult<OrganizationSkill[]>(
        `/api/organizations/${props.organizationId}/skills`,
      )
      if (scopeKey.value !== expectedScope) return
    }

    if (loadedSkills.canManage) {
      await restoreLatestJob(expectedScope)
    }
  } catch (cause) {
    if (scopeKey.value === expectedScope) {
      message.value = friendlyError(cause, 'Không thể tải kỹ năng của nhiệm vụ.')
    }
  } finally {
    if (scopeKey.value === expectedScope) loading.value = false
  }
}

async function restoreLatestJob(expectedScope: string) {
  const jobs = await apiResult<JobSummary[]>(`/api/ai/jobs?projectId=${props.projectId}`)
  if (scopeKey.value !== expectedScope) return
  const latest = jobs.find(
    item =>
      item.jobType === 'task_skill_suggestion' &&
      item.scopeSourceType?.toLowerCase() === 'task' &&
      item.scopeSourceEntityId === props.taskId,
  )
  if (latest) await hydrateJob(latest.jobId, true, expectedScope)
}

async function hydrateJob(
  jobId: string,
  silent = false,
  expectedScope = scopeKey.value,
) {
  if (!silent) loading.value = true
  try {
    const hydrated = await apiResult<JobDetail>(`/api/ai/jobs/${jobId}`)
    if (scopeKey.value !== expectedScope) return
    job.value = hydrated
    result.value = null
    draft.value = null
    reviewSuggestions.value = []

    if (hydrated.status === 'succeeded') {
      const persisted = await apiResult<JobResult>(`/api/ai/jobs/${jobId}/result`)
      if (scopeKey.value !== expectedScope) return
      if (
        persisted.schemaId !== 'task_skill_suggestion.v1' ||
        persisted.isMock ||
        persisted.result?.schemaId !== 'task_skill_suggestion.v1'
      ) {
        message.value =
          'Kết quả không đạt contract AI-native nên không được hiển thị như dữ liệu thật.'
        return
      }
      result.value = persisted
      if (persisted.draftIds.length === 0) {
        message.value = 'Kết quả thiếu draft duyệt bắt buộc nên chưa thể áp dụng.'
        return
      }
      await hydrateDraft(persisted.draftIds[0], expectedScope)
      if (!persisted.sourceStale) message.value = ''
    } else if (hydrated.status === 'failed') {
      message.value = friendlyJobFailure(hydrated)
    } else if (hydrated.status === 'canceled') {
      message.value = 'Tác vụ đã được hủy. Không có kỹ năng nào bị thay đổi.'
    } else {
      message.value = ''
    }
  } catch (cause) {
    if (scopeKey.value === expectedScope) {
      message.value = friendlyError(cause, 'Không thể tải trạng thái gợi ý AI.')
    }
  } finally {
    if (scopeKey.value === expectedScope) {
      loading.value = false
      schedulePoll(expectedScope)
    }
  }
}

async function hydrateDraft(draftId: string, expectedScope = scopeKey.value) {
  const persisted = await apiResult<DraftDetail>(`/api/ai/drafts/${draftId}`)
  if (scopeKey.value !== expectedScope) return
  draft.value = persisted
  reviewSuggestions.value = (persisted.workingPayload.suggestions ?? []).map(item => ({
    ...item,
    selected: true,
  }))
}

async function generateSuggestions() {
  if (!taskSkills.value?.canManage || actionPending.value || isActive.value) return
  actionPending.value = true
  message.value = ''
  successMessage.value = ''
  manualOnly.value = false
  try {
    const created = await apiResult<{ jobId: string }>(
      `/api/ai/tasks/${props.taskId}/skill-suggestions`,
      {
        method: 'POST',
        headers: { 'Idempotency-Key': newIdempotencyKey() },
        body: JSON.stringify({
          language: 'vi',
          providerHint: 'deepseek',
          maximumEstimatedCostUsd: 0.15,
          cacheMode: 'use',
        }),
      },
    )
    await hydrateJob(created.jobId)
  } catch (cause) {
    const text = friendlyError(cause, 'Không thể bắt đầu gợi ý kỹ năng.')
    message.value = text
    manualOnly.value =
      cause instanceof ApiError &&
      String((cause.payload as any)?.errorCode ?? '').includes('PLATFORM')
  } finally {
    actionPending.value = false
  }
}

async function cancelJob() {
  if (!job.value || actionPending.value) return
  actionPending.value = true
  try {
    await apiResult(`/api/ai/jobs/${job.value.jobId}/cancel`, {
      method: 'POST',
      body: JSON.stringify({ reason: 'Canceled from native task skills card' }),
    })
    await hydrateJob(job.value.jobId)
  } catch (cause) {
    message.value = friendlyError(cause, 'Không thể hủy tác vụ AI.')
  } finally {
    actionPending.value = false
  }
}

async function retryJob() {
  if (!job.value || actionPending.value) return
  actionPending.value = true
  message.value = ''
  try {
    await apiResult(`/api/ai/jobs/${job.value.jobId}/retry`, {
      method: 'POST',
      body: JSON.stringify({}),
    })
    await hydrateJob(job.value.jobId)
  } catch (cause) {
    message.value = friendlyError(cause, 'Không thể thử lại tác vụ AI.')
  } finally {
    actionPending.value = false
  }
}

function addManualSkill() {
  if (!selectedCatalogSkillId.value) return
  if (manualSelections.value.some(item => item.skillId === selectedCatalogSkillId.value)) return
  manualSelections.value.push({
    skillId: selectedCatalogSkillId.value,
    requiredLevel: selectedCatalogLevel.value,
  })
  selectedCatalogSkillId.value = ''
  selectedCatalogLevel.value = 'Proficient'
}

function isSkillAssigned(skillId: string) {
  return manualSelections.value.some(item => item.skillId === skillId)
}

async function assignSuggestedSkill(suggestion: SkillSuggestion) {
  if (manualSaving.value || isSkillAssigned(suggestion.skillId)) return
  manualSelections.value.push({
    skillId: suggestion.skillId,
    requiredLevel: suggestion.requiredLevel,
  })
  await saveManualSkills()
}

function removeManualSkill(skillId: string) {
  manualSelections.value = manualSelections.value.filter(item => item.skillId !== skillId)
}

function resetManualSkills() {
  manualSelections.value = (taskSkills.value?.requirements ?? []).map(item => ({
    skillId: item.skillId,
    requiredLevel: item.requiredLevel,
  }))
}

async function saveManualSkills() {
  if (!taskSkills.value?.canManage || manualSaving.value) return
  manualSaving.value = true
  message.value = ''
  successMessage.value = ''
  try {
    const saved = await apiResult<TaskSkills>(`/api/tasks/${props.taskId}/skills`, {
      method: 'PUT',
      body: JSON.stringify({
        taskRowVersion: taskSkills.value.taskRowVersion,
        skills: manualSelections.value,
      }),
    })
    taskSkills.value = saved
    manualSelections.value = saved.requirements.map(item => ({
      skillId: item.skillId,
      requiredLevel: item.requiredLevel,
    }))
    successMessage.value = 'Đã lưu kỹ năng thủ công.'
  } catch (cause) {
    message.value = friendlyError(cause, 'Không thể lưu kỹ năng thủ công.')
    if (isConcurrencyError(cause)) await reloadTaskSkills()
  } finally {
    manualSaving.value = false
  }
}

async function createCatalogSkill() {
  if (
    !props.organizationId ||
    !taskSkills.value?.canManageCatalog ||
    !newSkillName.value.trim() ||
    catalogSaving.value
  ) {
    return
  }
  catalogSaving.value = true
  message.value = ''
  successMessage.value = ''
  try {
    const created = await apiResult<OrganizationSkill>(
      `/api/organizations/${props.organizationId}/skills`,
      {
        method: 'POST',
        body: JSON.stringify({
          name: newSkillName.value.trim(),
          description: newSkillDescription.value.trim() || null,
        }),
      },
    )
    catalog.value = [...catalog.value, created].sort((a, b) => a.name.localeCompare(b.name))
    selectedCatalogSkillId.value = created.id
    newSkillName.value = ''
    newSkillDescription.value = ''
    successMessage.value = 'Đã thêm kỹ năng vào catalog tổ chức.'
  } catch (cause) {
    message.value = friendlyError(cause, 'Không thể tạo kỹ năng trong catalog.')
  } finally {
    catalogSaving.value = false
  }
}

async function applyDraft() {
  if (!draft.value || !canReviewDraft.value || selectedReviewCount.value === 0 || actionPending.value) {
    return
  }
  actionPending.value = true
  message.value = ''
  successMessage.value = ''
  try {
    const payload: SkillSuggestionOutput = {
      ...draft.value.workingPayload,
      dataState: 'ready',
      suggestions: reviewSuggestions.value
        .filter(item => item.selected)
        .map(({ selected: _selected, ...item }) => item),
    }
    const payloadJson = JSON.stringify(payload)
    const patched = await apiResult<DraftDetail>(`/api/ai/drafts/${draft.value.draftId}`, {
      method: 'PATCH',
      body: JSON.stringify({
        workingPayloadJson: payloadJson,
        rowVersion: draft.value.rowVersion,
      }),
    })
    draft.value = patched
    const confirmationKey = newIdempotencyKey()
    const confirmed = await apiResult<{ appliedSkillCount: number }>(
      `/api/ai/drafts/${patched.draftId}/confirm`,
      {
        method: 'POST',
        headers: { 'Idempotency-Key': confirmationKey },
        body: JSON.stringify({
          editedPayloadJson: payloadJson,
          confirmAction: 'apply_task_skills',
          confirmationNote: 'Reviewed in native task skills card',
          rowVersion: patched.rowVersion,
          idempotencyKey: confirmationKey,
        }),
      },
    )
    successMessage.value = `Đã áp dụng ${confirmed.appliedSkillCount} kỹ năng đã chọn.`
    await Promise.all([reloadTaskSkills(), hydrateDraft(patched.draftId)])
  } catch (cause) {
    message.value = friendlyError(cause, 'Không thể áp dụng draft kỹ năng.')
    if (isConcurrencyError(cause)) {
      message.value += ' Dữ liệu mới nhất đã được tải lại; hãy tạo hoặc duyệt lại draft.'
      await Promise.all([reloadTaskSkills(), draft.value ? hydrateDraft(draft.value.draftId) : Promise.resolve()])
    }
  } finally {
    actionPending.value = false
  }
}

async function rejectDraft() {
  if (!draft.value || !canReviewDraft.value || actionPending.value) return
  actionPending.value = true
  message.value = ''
  successMessage.value = ''
  try {
    const rejectKey = newIdempotencyKey()
    await apiResult(`/api/ai/drafts/${draft.value.draftId}/reject`, {
      method: 'POST',
      headers: { 'Idempotency-Key': rejectKey },
      body: JSON.stringify({
        reason: 'Reviewer rejected the task skill suggestions',
        rowVersion: draft.value.rowVersion,
        idempotencyKey: rejectKey,
      }),
    })
    await hydrateDraft(draft.value.draftId)
    successMessage.value = 'Đã từ chối draft; nhiệm vụ không bị thay đổi.'
  } catch (cause) {
    message.value = friendlyError(cause, 'Không thể từ chối draft.')
  } finally {
    actionPending.value = false
  }
}

async function reloadTaskSkills() {
  const loaded = await apiResult<TaskSkills>(`/api/tasks/${props.taskId}/skills`)
  taskSkills.value = loaded
  manualSelections.value = loaded.requirements.map(item => ({
    skillId: item.skillId,
    requiredLevel: item.requiredLevel,
  }))
}

function skillName(skillId: string) {
  return (
    catalog.value.find(item => item.id === skillId)?.name ??
    currentRequirementsBySkill.value.get(skillId)?.name ??
    'Kỹ năng không khả dụng'
  )
}

function levelLabel(level: SkillLevel) {
  return (
    {
      Familiar: 'Cơ bản',
      Proficient: 'Thành thạo',
      Expert: 'Chuyên sâu',
    } as Record<SkillLevel, string>
  )[level]
}

function confidenceLabel(value: number) {
  return `${Math.round(value * 100)}%`
}

function friendlyJobFailure(value: JobDetail) {
  const code = value.lastErrorCode ?? ''
  if (code.includes('SCHEMA')) return 'Nhà cung cấp không trả dữ liệu đúng schema sau các lần sửa.'
  if (code.includes('SOURCE_STALE')) return 'Nhiệm vụ hoặc catalog đã đổi; hãy tạo draft mới.'
  if (code.includes('BUDGET')) return 'Ngân sách AI hiện không đủ. Bạn vẫn có thể gắn kỹ năng thủ công.'
  if (code.includes('SENSITIVE') || code.includes('CONSENT')) {
    return 'Nguồn hạn chế bị từ chối theo chính sách riêng tư; không có dữ liệu nào bị thay đổi.'
  }
  if (code.includes('PROVIDER') || code.includes('TIMEOUT')) {
    return 'Nhà cung cấp AI không khả dụng hoặc đã hết thời gian chờ.'
  }
  return value.lastErrorMessage || 'Tác vụ AI không thể hoàn tất.'
}

function friendlyError(cause: unknown, fallback: string) {
  if (cause instanceof ApiError && cause.payload && typeof cause.payload === 'object') {
    const code = String((cause.payload as any).errorCode ?? '')
    if (code.includes('PERMISSION')) return 'Bạn không có quyền thực hiện thao tác này.'
    if (code.includes('PLATFORM')) {
      return 'AI gợi ý kỹ năng đang tắt. Gắn kỹ năng thủ công vẫn hoạt động.'
    }
    if (code.includes('SOURCE_STALE')) return 'Nhiệm vụ hoặc catalog đã thay đổi; draft cũ không thể áp dụng.'
    if (code.includes('CONCURRENCY')) return 'Dữ liệu đã được người khác cập nhật.'
    if (code.includes('BUDGET')) return 'Ngân sách AI hiện không đủ. Gắn thủ công vẫn hoạt động.'
    if (code.includes('SENSITIVE') || code.includes('CONSENT')) {
      return 'Nguồn hạn chế bị từ chối theo chính sách riêng tư.'
    }
  }
  return errorMessage(cause, fallback)
}

function isConcurrencyError(cause: unknown) {
  return (
    cause instanceof ApiError &&
    (cause.status === 409 ||
      String((cause.payload as any)?.errorCode ?? '').includes('CONCURRENCY') ||
      String((cause.payload as any)?.errorCode ?? '').includes('SOURCE_STALE'))
  )
}

watch(scopeKey, () => void restore(), { immediate: true })
onBeforeUnmount(clearPoll)
</script>

<template>
  <section class="task-skills-card glass-card" data-testid="task-skills-ai-card">
    <header class="task-skills-card__header">
      <div>
        <span class="eyebrow"><Tags :size="14" /> Kỹ năng cần thiết</span>
        <h3>Yêu cầu kỹ năng của nhiệm vụ</h3>
        <p>Chỉ catalog của tổ chức được dùng. AI tạo draft để bạn duyệt, không tự gắn thẻ.</p>
      </div>
      <span class="status-pill" :class="{ active: isActive }" data-testid="task-skill-ai-status">
        <LoaderCircle v-if="isActive" :size="14" class="spin" />
        <CheckCircle2 v-else-if="draft?.status.toLowerCase() === 'confirmed'" :size="14" />
        <Sparkles v-else :size="14" />
        {{ statusLabel }}
      </span>
    </header>

    <div v-if="loading && !taskSkills" class="state-row" data-testid="task-skills-loading">
      <LoaderCircle :size="18" class="spin" /> Đang tải catalog và kỹ năng…
    </div>

    <template v-else-if="taskSkills">
      <div v-if="taskSkills.availability === 'organization_required'" class="notice warning">
        <AlertTriangle :size="17" />
        Dự án phải thuộc một tổ chức trước khi có thể dùng taxonomy kỹ năng.
      </div>

      <div v-else class="current-skills">
        <div class="subhead">
          <strong>Đã xác nhận</strong>
          <span>{{ taskSkills.requirements.length }} kỹ năng</span>
        </div>
        <div v-if="taskSkills.requirements.length" class="skill-chips" data-testid="confirmed-task-skills">
          <span
            v-for="requirement in taskSkills.requirements"
            :key="requirement.id"
            class="skill-chip"
            :title="requirement.description ?? requirement.name"
          >
            {{ requirement.name }}
            <small>{{ levelLabel(requirement.requiredLevel) }}</small>
            <em>{{ requirement.provenance === 'AI_CONFIRMED' ? 'AI đã duyệt' : 'Thủ công' }}</em>
          </span>
        </div>
        <p v-else class="empty-copy" data-testid="task-skills-empty">
          Chưa có kỹ năng nào được xác nhận cho nhiệm vụ này.
        </p>
      </div>

      <details v-if="taskSkills.canManage && taskSkills.availability !== 'organization_required'" class="manual-editor">
        <summary>Gắn hoặc sửa thủ công</summary>
        <div class="task-based-suggestions" data-testid="task-based-skill-suggestions">
          <div class="subhead">
            <div>
              <strong>Gợi ý theo nhiệm vụ</strong>
              <span>Chỉ dùng kỹ năng có thật trong catalog; bạn có thể gắn từng kỹ năng.</span>
            </div>
            <button
              v-if="!reviewSuggestions.length"
              type="button"
              class="secondary-button"
              :disabled="actionPending || isActive || taskSkills.availability === 'catalog_empty'"
              @click="generateSuggestions"
            >
              <LoaderCircle v-if="actionPending || isActive" :size="15" class="spin" />
              <Sparkles v-else :size="15" />
              {{ isActive ? 'Đang phân tích…' : 'Phân tích task' }}
            </button>
          </div>
          <div v-if="reviewSuggestions.length" class="quick-suggestion-list">
            <article v-for="suggestion in reviewSuggestions" :key="`quick-${suggestion.skillId}`">
              <div>
                <strong>{{ suggestion.canonicalName }}</strong>
                <small>{{ levelLabel(suggestion.requiredLevel) }} · tin cậy {{ confidenceLabel(suggestion.confidence) }}</small>
                <p>{{ suggestion.rationale }}</p>
              </div>
              <button
                type="button"
                class="secondary-button"
                :disabled="manualSaving || isSkillAssigned(suggestion.skillId)"
                @click="assignSuggestedSkill(suggestion)"
              >
                <Check v-if="isSkillAssigned(suggestion.skillId)" :size="15" />
                <Plus v-else :size="15" />
                {{ isSkillAssigned(suggestion.skillId) ? 'Đã gắn' : 'Gắn kỹ năng' }}
              </button>
            </article>
          </div>
        </div>
        <div v-if="catalog.length" class="manual-list">
          <div v-for="selection in manualSelections" :key="selection.skillId" class="manual-row">
            <span>{{ skillName(selection.skillId) }}</span>
            <select v-model="selection.requiredLevel" :aria-label="`Mức ${skillName(selection.skillId)}`">
              <option v-for="level in levels" :key="level" :value="level">
                {{ levelLabel(level) }}
              </option>
            </select>
            <button type="button" class="icon-button" aria-label="Bỏ kỹ năng" @click="removeManualSkill(selection.skillId)">
              <X :size="15" />
            </button>
          </div>
          <div v-if="availableCatalog.length" class="manual-add">
            <select v-model="selectedCatalogSkillId" aria-label="Chọn kỹ năng">
              <option value="">Chọn từ catalog…</option>
              <option v-for="skill in availableCatalog" :key="skill.id" :value="skill.id">
                {{ skill.name }}
              </option>
            </select>
            <select v-model="selectedCatalogLevel" aria-label="Chọn mức kỹ năng">
              <option v-for="level in levels" :key="level" :value="level">
                {{ levelLabel(level) }}
              </option>
            </select>
            <button type="button" class="secondary-button" :disabled="!selectedCatalogSkillId" @click="addManualSkill">
              <Plus :size="15" /> Thêm
            </button>
          </div>
          <div class="button-row">
            <button type="button" class="secondary-button" :disabled="manualSaving" @click="resetManualSkills">
              <RotateCcw :size="15" /> Hoàn tác
            </button>
            <button
              type="button"
              class="primary-button"
              data-testid="save-manual-task-skills"
              :disabled="manualSaving"
              @click="saveManualSkills"
            >
              <LoaderCircle v-if="manualSaving" :size="15" class="spin" />
              <Save v-else :size="15" />
              Lưu thủ công
            </button>
          </div>
        </div>
        <p v-else class="empty-copy">Catalog tổ chức đang trống.</p>

        <form
          v-if="taskSkills.canManageCatalog"
          class="catalog-create"
          data-testid="create-organization-skill"
          @submit.prevent="createCatalogSkill"
        >
          <strong>Thêm vào catalog tổ chức</strong>
          <input v-model="newSkillName" aria-label="Tên kỹ năng mới" maxlength="100" placeholder="Ví dụ: Vue.js, ASP.NET Core…" required />
          <input v-model="newSkillDescription" aria-label="Mô tả kỹ năng mới" maxlength="500" placeholder="Mô tả ngắn (không bắt buộc)" />
          <button type="submit" class="secondary-button" :disabled="catalogSaving || !newSkillName.trim()">
            <LoaderCircle v-if="catalogSaving" :size="15" class="spin" />
            <Plus v-else :size="15" />
            Tạo kỹ năng
          </button>
        </form>
        <p v-else-if="catalog.length === 0" class="empty-copy">
          Quản trị viên tổ chức cần tạo catalog; AI không tự tạo taxonomy.
        </p>
      </details>

      <div v-if="taskSkills.canManage && taskSkills.availability !== 'organization_required'" class="ai-panel">
        <div class="subhead">
          <div>
            <strong>Draft gợi ý bằng AI</strong>
            <span>Dựa trên tiêu đề/mô tả task và catalog hiện tại</span>
          </div>
          <div class="button-row compact">
            <button
              v-if="isActive"
              type="button"
              class="secondary-button danger"
              :disabled="actionPending"
              data-testid="cancel-task-skill-ai"
              @click="cancelJob"
            >
              <Ban :size="15" /> Hủy
            </button>
            <button
              v-else-if="canRetry"
              type="button"
              class="secondary-button"
              :disabled="actionPending"
              data-testid="retry-task-skill-ai"
              @click="retryJob"
            >
              <RefreshCw :size="15" /> Thử lại
            </button>
            <button
              type="button"
              class="primary-button"
              :disabled="actionPending || isActive || taskSkills.availability === 'catalog_empty'"
              data-testid="generate-task-skill-ai"
              @click="generateSuggestions"
            >
              <LoaderCircle v-if="actionPending || isActive" :size="15" class="spin" />
              <Sparkles v-else :size="15" />
              {{ job ? 'Tạo draft mới' : 'Gợi ý kỹ năng' }}
            </button>
          </div>
        </div>

        <div v-if="taskSkills.availability === 'catalog_empty'" class="notice">
          Catalog đang trống. Tạo ít nhất một kỹ năng trước khi chạy AI.
        </div>
        <div v-else-if="manualOnly" class="notice">
          AI đang ở chế độ tắt/degraded; luồng gắn thủ công phía trên vẫn dùng được.
        </div>

        <div v-if="isActive" class="progress-state" role="status" aria-live="polite" data-testid="task-skill-ai-running">
          <div class="progress-track"><span :style="{ width: `${Math.max(job?.progressPercent ?? 5, 5)}%` }" /></div>
          <small>{{ statusLabel }} · lần {{ job?.attemptCount ?? 0 }}/{{ job?.maxAttempts ?? 0 }}</small>
        </div>

        <div
          v-if="result?.result.dataState === 'empty'"
          class="notice"
          data-testid="task-skill-ai-empty"
        >
          AI không tìm thấy kỹ năng đủ căn cứ trong catalog. Không có thẻ nào được tự tạo.
        </div>

        <div
          v-if="canReviewDraft && reviewSuggestions.length"
          class="draft-review"
          data-testid="task-skill-draft-review"
        >
          <div class="review-heading">
            <div>
              <strong>Duyệt từng đề xuất</strong>
              <span>Chọn/bỏ chọn và chỉnh mức trước khi áp dụng.</span>
            </div>
            <span>{{ selectedReviewCount }}/{{ reviewSuggestions.length }} đã chọn</span>
          </div>
          <article v-for="suggestion in reviewSuggestions" :key="suggestion.skillId" class="suggestion">
            <label class="suggestion__select">
              <input v-model="suggestion.selected" type="checkbox" />
              <span>
                <strong>{{ suggestion.canonicalName }}</strong>
                <small>Độ tin cậy {{ confidenceLabel(suggestion.confidence) }}</small>
              </span>
            </label>
            <select v-model="suggestion.requiredLevel" :aria-label="`Mức yêu cầu cho kỹ năng ${suggestion.canonicalName}`" :disabled="!suggestion.selected">
              <option v-for="level in levels" :key="level" :value="level">
                {{ levelLabel(level) }}
              </option>
            </select>
            <p>{{ suggestion.rationale }}</p>
            <div class="source-row">
              <a :href="`/projects/${projectId}`" target="_self">
                <ExternalLink :size="13" /> Task {{ taskId.slice(0, 8) }}
              </a>
              <code v-for="sourceRef in suggestion.sourceRefs" :key="sourceRef">{{ sourceRef }}</code>
            </div>
          </article>
          <div v-if="result?.result.unmappedTerms.length" class="unmapped">
            Không có trong catalog:
            <span v-for="term in result.result.unmappedTerms" :key="term">{{ term }}</span>
          </div>
          <div class="button-row">
            <button
              type="button"
              class="secondary-button danger"
              :disabled="actionPending"
              data-testid="reject-task-skill-draft"
              @click="rejectDraft"
            >
              <Ban :size="15" /> Từ chối draft
            </button>
            <button
              type="button"
              class="primary-button"
              :disabled="actionPending || selectedReviewCount === 0 || result?.sourceStale"
              data-testid="confirm-task-skill-draft"
              @click="applyDraft"
            >
              <LoaderCircle v-if="actionPending" :size="15" class="spin" />
              <Check v-else :size="15" />
              Áp dụng {{ selectedReviewCount }} kỹ năng
            </button>
          </div>
        </div>

        <div v-else-if="draft?.status.toLowerCase() === 'confirmed'" class="notice success">
          <CheckCircle2 :size="17" /> Draft đã được xác nhận và có thể đọc lại sau khi tải trang.
        </div>
        <div v-else-if="draft?.status.toLowerCase() === 'rejected'" class="notice">
          Draft đã bị từ chối; không có thay đổi nào được áp dụng.
        </div>
      </div>

      <div v-if="message" class="notice error" role="alert" data-testid="task-skill-error">
        <AlertTriangle :size="17" /> {{ message }}
      </div>
      <div v-if="successMessage" class="notice success" role="status">
        <CheckCircle2 :size="17" /> {{ successMessage }}
      </div>
    </template>
  </section>
</template>

<style scoped>
.task-skills-card {
  display: grid;
  gap: 14px;
  padding: 18px;
  border: 1px solid color-mix(in srgb, var(--lavender-400, #8b7cf6) 24%, transparent);
  background:
    radial-gradient(circle at top right, color-mix(in srgb, #8b7cf6 12%, transparent), transparent 42%),
    color-mix(in srgb, var(--surface, #fff) 92%, transparent);
}

.task-skills-card__header,
.subhead,
.review-heading,
.button-row,
.source-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
}

.task-skills-card__header {
  align-items: flex-start;
}

.task-skills-card h3 {
  margin: 4px 0;
  font-size: 1rem;
}

.task-skills-card p {
  margin: 0;
}

.task-skills-card__header p,
.subhead span,
.review-heading span,
.empty-copy {
  color: var(--text-muted, #73738a);
  font-size: 0.78rem;
  line-height: 1.45;
}

.eyebrow,
.status-pill {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  color: var(--lavender-600, #6d5bd0);
  font-size: 0.72rem;
  font-weight: 700;
  letter-spacing: 0.04em;
  text-transform: uppercase;
}

.status-pill {
  flex: none;
  padding: 6px 9px;
  border-radius: 999px;
  background: color-mix(in srgb, #8b7cf6 12%, transparent);
  letter-spacing: 0;
  text-transform: none;
}

.status-pill.active {
  color: #7c3aed;
}

.current-skills,
.manual-editor,
.ai-panel,
.draft-review {
  display: grid;
  gap: 10px;
}

.current-skills,
.ai-panel,
.draft-review {
  padding-top: 12px;
  border-top: 1px solid color-mix(in srgb, currentColor 10%, transparent);
}

.skill-chips {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.skill-chip {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 7px 9px;
  border: 1px solid color-mix(in srgb, #8b7cf6 24%, transparent);
  border-radius: 10px;
  background: color-mix(in srgb, #8b7cf6 8%, transparent);
  font-size: 0.78rem;
  font-weight: 700;
}

.skill-chip small,
.skill-chip em {
  font-size: 0.66rem;
  font-style: normal;
  font-weight: 600;
}

.skill-chip small {
  color: var(--text-muted, #73738a);
}

.skill-chip em {
  padding: 2px 5px;
  border-radius: 999px;
  color: #16794b;
  background: color-mix(in srgb, #2fb879 12%, transparent);
}

.manual-editor {
  padding: 11px 12px;
  border: 1px solid color-mix(in srgb, currentColor 10%, transparent);
  border-radius: 12px;
}

.manual-editor summary {
  cursor: pointer;
  font-size: 0.8rem;
  font-weight: 700;
}

.manual-list,
.catalog-create {
  display: grid;
  gap: 8px;
  margin-top: 10px;
}

.task-based-suggestions {
  display: grid;
  gap: 9px;
  margin-top: 10px;
  padding: 10px;
  border-radius: 10px;
  background: color-mix(in srgb, #8b7cf6 7%, transparent);
}

.task-based-suggestions .subhead > div,
.quick-suggestion-list article > div {
  display: grid;
  gap: 3px;
  min-width: 0;
}

.task-based-suggestions span,
.quick-suggestion-list small,
.quick-suggestion-list p {
  color: var(--text-muted, #73738a);
  font-size: 0.72rem;
}

.quick-suggestion-list {
  display: grid;
  gap: 7px;
}

.quick-suggestion-list article {
  display: grid;
  grid-template-columns: minmax(0, 1fr) auto;
  gap: 10px;
  align-items: center;
  padding: 9px;
  border: 1px solid color-mix(in srgb, #8b7cf6 18%, transparent);
  border-radius: 9px;
  background: color-mix(in srgb, var(--surface, #fff) 94%, transparent);
}

.manual-row,
.manual-add {
  display: grid;
  grid-template-columns: minmax(0, 1fr) 130px auto;
  align-items: center;
  gap: 8px;
}

.manual-row span {
  overflow: hidden;
  font-size: 0.79rem;
  font-weight: 650;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.manual-add {
  grid-template-columns: minmax(0, 1fr) 130px auto;
  padding-top: 4px;
}

select,
input {
  min-width: 0;
  padding: 8px 9px;
  border: 1px solid color-mix(in srgb, currentColor 14%, transparent);
  border-radius: 8px;
  color: inherit;
  background: color-mix(in srgb, var(--surface, #fff) 90%, transparent);
  font: inherit;
  font-size: 0.76rem;
}

.catalog-create {
  padding-top: 10px;
  border-top: 1px dashed color-mix(in srgb, currentColor 14%, transparent);
}

.catalog-create strong {
  font-size: 0.78rem;
}

button {
  font: inherit;
}

.primary-button,
.secondary-button,
.icon-button {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 6px;
  min-height: 34px;
  padding: 7px 10px;
  border: 0;
  border-radius: 9px;
  cursor: pointer;
  font-size: 0.75rem;
  font-weight: 700;
}

.primary-button {
  color: #fff;
  background: linear-gradient(135deg, #7464e8, #5d4ed2);
}

.secondary-button,
.icon-button {
  color: inherit;
  background: color-mix(in srgb, currentColor 7%, transparent);
}

.icon-button {
  width: 34px;
  padding: 0;
}

.danger {
  color: #c03c50;
}

button:disabled,
select:disabled {
  cursor: not-allowed;
  opacity: 0.5;
}

.button-row {
  justify-content: flex-end;
  flex-wrap: wrap;
}

.button-row.compact {
  flex: none;
}

.subhead > div:first-child,
.review-heading > div:first-child {
  display: grid;
  gap: 2px;
}

.subhead strong,
.review-heading strong {
  font-size: 0.82rem;
}

.notice,
.state-row,
.progress-state {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 9px 10px;
  border-radius: 9px;
  color: var(--text-muted, #73738a);
  background: color-mix(in srgb, currentColor 6%, transparent);
  font-size: 0.76rem;
  line-height: 1.4;
}

.notice.error,
.notice.warning {
  color: #b63a4d;
  background: color-mix(in srgb, #e04f65 10%, transparent);
}

.notice.success {
  color: #16794b;
  background: color-mix(in srgb, #2fb879 10%, transparent);
}

.progress-state {
  display: grid;
}

.progress-track {
  height: 5px;
  overflow: hidden;
  border-radius: 999px;
  background: color-mix(in srgb, currentColor 9%, transparent);
}

.progress-track span {
  display: block;
  height: 100%;
  border-radius: inherit;
  background: linear-gradient(90deg, #8b7cf6, #58a6ff);
  transition: width 0.25s ease;
}

.suggestion {
  display: grid;
  grid-template-columns: minmax(0, 1fr) 130px;
  gap: 8px 12px;
  padding: 11px;
  border: 1px solid color-mix(in srgb, currentColor 10%, transparent);
  border-radius: 10px;
}

.suggestion__select {
  display: flex;
  align-items: center;
  gap: 9px;
  cursor: pointer;
}

.suggestion__select span {
  display: grid;
  gap: 2px;
}

.suggestion__select strong {
  font-size: 0.8rem;
}

.suggestion__select small {
  color: var(--text-muted, #73738a);
  font-size: 0.68rem;
}

.suggestion p,
.source-row,
.unmapped {
  grid-column: 1 / -1;
}

.suggestion p {
  color: var(--text-muted, #73738a);
  font-size: 0.75rem;
  line-height: 1.5;
}

.source-row {
  justify-content: flex-start;
  flex-wrap: wrap;
}

.source-row a,
.source-row code,
.unmapped span {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  padding: 3px 6px;
  border-radius: 6px;
  color: #5d4ed2;
  background: color-mix(in srgb, #8b7cf6 10%, transparent);
  font-size: 0.67rem;
  text-decoration: none;
}

.unmapped {
  color: var(--text-muted, #73738a);
  font-size: 0.72rem;
}

.unmapped span {
  margin-left: 5px;
}

.spin {
  animation: spin 0.8s linear infinite;
}

@keyframes spin {
  to {
    transform: rotate(360deg);
  }
}

@media (max-width: 680px) {
  .task-skills-card__header,
  .subhead,
  .review-heading {
    align-items: stretch;
    flex-direction: column;
  }

  .manual-row,
  .manual-add,
  .suggestion {
    grid-template-columns: minmax(0, 1fr);
  }

  .suggestion p,
  .source-row,
  .unmapped {
    grid-column: auto;
  }
}
</style>
