<script setup lang="ts">
import { computed, ref, watch, onBeforeUnmount } from 'vue'
import { useRouter } from 'vue-router'
import {
  ListChecks,
  Loader2,
  Sparkles,
  FileText,
  CheckCircle2,
  HelpCircle,
  FolderKanban,
  Trash2,
  Check,
  Plus,
  User,
  Calendar,
  AlertTriangle
} from 'lucide-vue-next'
import { showError, showSuccess } from '../../composables/use-toast'
import { confirmDialog } from '../../composables/use-confirm-dialog'
import { apiResult, errorMessage } from '../../utils/api-client'

interface GroupMemberDto {
  userId: string
  fullName: string
  email: string
  role: string
  joinedAt: string
}

interface GroupAiActionItem {
  title: string
  description: string | null
  suggestedOwnerName: string | null
  dueDateSuggestion: string | null
  confidence: number
  sourceEvidence: string | null
}

interface GroupAiActionItemsResponse {
  groupId: string
  source: string
  items: GroupAiActionItem[]
  warnings: string[]
}

interface GroupAiSummaryResponse {
  groupId: string
  summary: string
  keyDecisions: string[]
  unresolvedQuestions: string[]
  warnings: string[]
  messageSources?: string[]
}

interface GroupAiDraftTask {
  title: string
  description: string | null
  priority: string | null
  estimateDays: number | null
  suggestedOwnerName: string | null
  // UI extended fields
  checked?: boolean
  assigneeId?: string | null
}

interface GroupAiDraftProjectResponse {
  groupId: string
  draftProjectName: string
  draftProjectDescription: string
  draftTasks: GroupAiDraftTask[]
  warnings: string[]
}

interface GroupLinkedProjectDto {
  projectId: string
  name: string
  code: string | null
}

interface SelectedAiRequest {
  action: 'summary' | 'task-draft'
  messageIds: string[]
  nonce: number
}

interface AiJobCreatedDto {
  jobId: string
  status: string
  draftId?: string | null
}

interface NativeTaskDraftItem {
  clientId: string
  title: string
  description: string | null
  priority: 'Low' | 'Medium' | 'High' | 'Critical'
  status: 'Todo'
  dueDate: string | null
  assigneeId: string | null
  selected: boolean
  confidence: number
  sourceRefs: string[]
}

interface NativeTaskDraftPayload {
  schemaId: 'task_draft.v5'
  dataState: 'ready' | 'insufficient_evidence'
  tasks: NativeTaskDraftItem[]
}

interface NativeAiJobDetail {
  jobId: string
  status: string
  progressPercent: number
  draftIds: string[]
  selectedProvider: string | null
  selectedModel: string | null
  lastErrorCode: string | null
  lastErrorMessage: string | null
  lastErrorRetryable: boolean
  isMock: boolean
}

interface NativeSummaryTextItem {
  text: string
  sourceRefs: string[]
}

interface NativeSummaryActionItem {
  title: string
  details: string
  sourceRefs: string[]
}

interface NativeSummarySource {
  key: string
  messageId: string
  url: string
}

interface NativeGroupSummaryPayload {
  schemaId: 'group_selected_summary.v1'
  groupId: string
  projectId: string
  messageRange: {
    messageIds: string[]
    fromMessageId: string
    toMessageId: string
  }
  summary: string
  summarySourceRefs: string[]
  keyDecisions: NativeSummaryTextItem[]
  openQuestions: NativeSummaryTextItem[]
  actionCandidates: NativeSummaryActionItem[]
  sourceRefs: NativeSummarySource[]
  warnings: string[]
}

interface NativeAiJobResult<T> {
  jobId: string
  schemaId: string
  result: T
  cacheHit: boolean
  isMock: boolean
  sourceStale: boolean
}

interface NativeAiDraftDetail {
  draftId: string
  aiJobId: string
  projectId: string
  status: string
  schemaId: string | null
  workingPayload: NativeTaskDraftPayload
  rowVersion: string
  confirmAction?: string | null
  confirmationResult?: { createdTaskIds?: string[] } | null
}

interface NativeDraftConfirmResult {
  draftId: string
  status: string
  createdTaskCount: number
  createdTaskIds: string[]
}

const props = defineProps<{
  groupId: string
  members?: GroupMemberDto[]
  selectedRequest?: SelectedAiRequest | null
}>()

const router = useRouter()

// Sub-tab Navigation
const subTab = ref<'summary' | 'draft' | 'action-items'>('summary')

// Common & Tab Loading States
const isSummaryLoading = ref(false)
const isDraftLoading = ref(false)
const isActionLoading = ref(false)
const isCreatingProject = ref(false)

// Cooldown / Anti-spam states (15 seconds)
const summaryCooldown = ref(0)
const draftCooldown = ref(0)
const actionCooldown = ref(0)

let summaryInterval: any = null
let draftInterval: any = null
let actionInterval: any = null

function startCooldown(type: 'summary' | 'draft' | 'action', seconds = 15) {
  if (type === 'summary') {
    summaryCooldown.value = seconds
    if (summaryInterval) clearInterval(summaryInterval)
    summaryInterval = setInterval(() => {
      if (summaryCooldown.value > 0) {
        summaryCooldown.value--
      } else {
        clearInterval(summaryInterval)
      }
    }, 1000)
  } else if (type === 'draft') {
    draftCooldown.value = seconds
    if (draftInterval) clearInterval(draftInterval)
    draftInterval = setInterval(() => {
      if (draftCooldown.value > 0) {
        draftCooldown.value--
      } else {
        clearInterval(draftInterval)
      }
    }, 1000)
  } else if (type === 'action') {
    actionCooldown.value = seconds
    if (actionInterval) clearInterval(actionInterval)
    actionInterval = setInterval(() => {
      if (actionCooldown.value > 0) {
        actionCooldown.value--
      } else {
        clearInterval(actionInterval)
      }
    }, 1000)
  }
}

onBeforeUnmount(() => {
  if (summaryInterval) clearInterval(summaryInterval)
  if (draftInterval) clearInterval(draftInterval)
  if (actionInterval) clearInterval(actionInterval)
  nativeDraftPollToken++
  nativeSummaryPollToken++
})

// Summary States
const summaryText = ref('')
const keyDecisions = ref<string[]>([])
const unresolvedQuestions = ref<string[]>([])
const messageSources = ref<string[]>([])
const summaryWarnings = ref<string[]>([])
const hasGeneratedSummary = ref(false)

// Draft Project States
const draftProjectName = ref('')
const draftProjectDescription = ref('')
const draftTasks = ref<GroupAiDraftTask[]>([])
const draftWarnings = ref<string[]>([])
const extraInstructions = ref('')
const hasGeneratedDraft = ref(false)

// Action Items States
const actionItems = ref<GroupAiActionItem[]>([])
const actionWarnings = ref<string[]>([])
const hasGeneratedActions = ref(false)
const showActionItemsList = computed(() => hasGeneratedActions.value && actionItems.value.length > 0)
const showEmptyActionPlaceholder = computed(() => hasGeneratedActions.value && actionItems.value.length === 0)

const hasGroup = computed(() => Boolean(props.groupId))
const linkedProjects = ref<GroupLinkedProjectDto[]>([])
const selectedProjectId = ref('')
const selectedMessageIds = ref<string[]>([])
const selectedAction = ref<'summary' | 'task-draft'>('summary')
const isSelectedRequestLoading = ref(false)
const nativeJob = ref<NativeAiJobDetail | null>(null)
const nativeDraft = ref<NativeAiDraftDetail | null>(null)
const nativeDraftPayload = ref<NativeTaskDraftPayload | null>(null)
const nativeDraftError = ref('')
const nativeDraftBusy = ref(false)
const nativeCreatedTaskIds = ref<string[]>([])
let nativeDraftPollToken = 0
const nativeSummaryJob = ref<NativeAiJobDetail | null>(null)
const nativeSummaryResult = ref<NativeAiJobResult<NativeGroupSummaryPayload> | null>(null)
const nativeSummaryError = ref('')
const nativeSummaryBusy = ref(false)
let nativeSummaryPollToken = 0

async function loadLinkedProjects() {
  linkedProjects.value = await apiResult<GroupLinkedProjectDto[]>(`/api/groups/${props.groupId}/linked-projects`)
  if (!linkedProjects.value.some(project => project.projectId === selectedProjectId.value)) {
    selectedProjectId.value = linkedProjects.value.length === 1 ? linkedProjects.value[0].projectId : ''
  }
}

function nativeDraftStorageKey() {
  return `qaly:group-task-draft:${props.groupId}`
}

function nativeSummaryStorageKey() {
  return `qaly:group-selected-summary:${props.groupId}`
}

function rememberNativeSummary(projectId: string, jobId: string) {
  localStorage.setItem(nativeSummaryStorageKey(), JSON.stringify({ projectId, jobId }))
}

async function monitorNativeSummary(jobId: string) {
  const token = ++nativeSummaryPollToken
  nativeSummaryError.value = ''
  nativeSummaryResult.value = null
  for (let attempt = 0; attempt < 80 && token === nativeSummaryPollToken; attempt++) {
    const detail = await apiResult<NativeAiJobDetail>(`/api/ai/jobs/${jobId}`)
    nativeSummaryJob.value = detail
    const status = detail.status.toLowerCase()
    if (status === 'succeeded') {
      const result = await apiResult<NativeAiJobResult<NativeGroupSummaryPayload>>(`/api/ai/jobs/${jobId}/result`)
      if (result.schemaId !== 'group_selected_summary.v1' || result.isMock) {
        nativeSummaryError.value = 'Kết quả không đạt contract native hoặc là mock; Qaly không hiển thị như dữ liệu thật.'
        return
      }
      nativeSummaryResult.value = result
      return
    }
    if (['failed', 'canceled', 'cancelled'].includes(status)) {
      nativeSummaryError.value = detail.lastErrorMessage || 'AI không tạo được bản tóm tắt có nguồn.'
      return
    }
    await new Promise(resolve => window.setTimeout(resolve, 750))
  }
  if (token === nativeSummaryPollToken) {
    nativeSummaryError.value = 'Job vẫn đang chạy. Bạn có thể đóng panel và mở lại để đọc tiếp kết quả.'
  }
}

async function restoreNativeSummary() {
  const raw = localStorage.getItem(nativeSummaryStorageKey())
  if (!raw) return
  try {
    const saved = JSON.parse(raw) as { projectId?: string; jobId?: string }
    if (!saved.projectId || !saved.jobId) return
    selectedProjectId.value ||= saved.projectId
    subTab.value = 'summary'
    await monitorNativeSummary(saved.jobId)
  } catch {
    localStorage.removeItem(nativeSummaryStorageKey())
  }
}

function rememberNativeJob(projectId: string, jobId: string) {
  localStorage.setItem(nativeDraftStorageKey(), JSON.stringify({ projectId, jobId }))
}

async function fetchNativeDraft(draftId: string) {
  const detail = await apiResult<NativeAiDraftDetail>(`/api/ai/drafts/${draftId}`)
  nativeDraft.value = detail
  nativeDraftPayload.value = JSON.parse(JSON.stringify(detail.workingPayload))
  nativeCreatedTaskIds.value = detail.confirmationResult?.createdTaskIds ?? nativeCreatedTaskIds.value
}

async function monitorNativeJob(jobId: string) {
  const token = ++nativeDraftPollToken
  nativeDraftError.value = ''
  for (let attempt = 0; attempt < 80 && token === nativeDraftPollToken; attempt++) {
    const detail = await apiResult<NativeAiJobDetail>(`/api/ai/jobs/${jobId}`)
    nativeJob.value = detail
    const status = detail.status.toLowerCase()
    if (status === 'succeeded') {
      const draftId = detail.draftIds[0]
      if (!draftId) {
        nativeDraftError.value = 'Job đã hoàn tất nhưng chưa có draft để review.'
        return
      }
      await fetchNativeDraft(draftId)
      return
    }
    if (['failed', 'canceled', 'cancelled'].includes(status)) {
      nativeDraftError.value = detail.lastErrorMessage || 'AI không tạo được draft. Không có task nào bị tạo.'
      return
    }
    await new Promise(resolve => window.setTimeout(resolve, 750))
  }
  if (token === nativeDraftPollToken) {
    nativeDraftError.value = 'Job vẫn đang chạy. Bạn có thể đóng panel và mở lại để đọc tiếp kết quả.'
  }
}

async function restoreNativeDraft() {
  const raw = localStorage.getItem(nativeDraftStorageKey())
  if (!raw) return
  try {
    const saved = JSON.parse(raw) as { projectId?: string; jobId?: string }
    if (!saved.projectId || !saved.jobId) return
    selectedProjectId.value ||= saved.projectId
    selectedAction.value = 'task-draft'
    subTab.value = 'draft'
    await monitorNativeJob(saved.jobId)
  } catch {
    localStorage.removeItem(nativeDraftStorageKey())
  }
}

async function submitSelectedMessages() {
  if (!selectedProjectId.value || !selectedMessageIds.value.length || isSelectedRequestLoading.value) return
  isSelectedRequestLoading.value = true
  try {
    const sources = selectedMessageIds.value.map(sourceEntityId => ({
      sourceType: 'message',
      sourceEntityId,
      legacySourceKey: null,
      sourceVersion: null,
      sourceHash: null
    }))
    const endpoint = selectedAction.value === 'summary'
      ? `/api/ai/groups/${props.groupId}/summaries`
      : `/api/ai/groups/${props.groupId}/task-drafts`
    const result = await apiResult<AiJobCreatedDto>(endpoint, {
      method: 'POST',
      headers: { 'Idempotency-Key': crypto.randomUUID() },
      body: JSON.stringify({
        projectId: selectedProjectId.value,
        ...(selectedAction.value === 'summary'
          ? { messageIds: selectedMessageIds.value, language: 'vi', cacheMode: 'use' }
          : {
              sourceType: 'message',
              sourceEntityId: selectedMessageIds.value[0],
              sources,
            }),
        providerHint: 'deepseek-chat',
      })
    })
    showSuccess(selectedAction.value === 'summary'
      ? 'Đã tạo job tóm tắt từ tin nhắn đã chọn.'
      : 'AI đang lập task draft có nguồn. Chưa có task nào được tạo.')
    if (selectedAction.value === 'task-draft') {
      nativeDraft.value = null
      nativeDraftPayload.value = null
      nativeCreatedTaskIds.value = []
      rememberNativeJob(selectedProjectId.value, result.jobId)
      await monitorNativeJob(result.jobId)
    } else {
      nativeSummaryJob.value = null
      nativeSummaryResult.value = null
      rememberNativeSummary(selectedProjectId.value, result.jobId)
      await monitorNativeSummary(result.jobId)
    }
  } catch (error) {
    showError(errorMessage(error, 'Không thể gửi các tin nhắn đã chọn cho AI.'))
  } finally {
    isSelectedRequestLoading.value = false
  }
}

async function saveNativeDraft() {
  if (!nativeDraft.value || !nativeDraftPayload.value || nativeDraftBusy.value) return
  nativeDraftBusy.value = true
  try {
    const detail = await apiResult<NativeAiDraftDetail>(`/api/ai/drafts/${nativeDraft.value.draftId}`, {
      method: 'PATCH',
      body: JSON.stringify({
        workingPayloadJson: JSON.stringify(nativeDraftPayload.value),
        rowVersion: nativeDraft.value.rowVersion,
      }),
    })
    nativeDraft.value = detail
    nativeDraftPayload.value = JSON.parse(JSON.stringify(detail.workingPayload))
    showSuccess('Đã lưu task draft. Reload vẫn tiếp tục review được.')
  } catch (error) {
    showError(errorMessage(error, 'Không thể lưu draft; hãy tải lại để tránh ghi đè phiên bản mới hơn.'))
  } finally {
    nativeDraftBusy.value = false
  }
}

async function confirmNativeDraft() {
  if (!nativeDraft.value || !nativeDraftPayload.value || nativeDraftBusy.value) return
  if (!nativeDraftPayload.value.tasks.some(task => task.selected)) {
    showError('Hãy chọn ít nhất một task để tạo.')
    return
  }
  nativeDraftBusy.value = true
  try {
    const key = crypto.randomUUID()
    const result = await apiResult<NativeDraftConfirmResult>(`/api/ai/drafts/${nativeDraft.value.draftId}/confirm`, {
      method: 'POST',
      headers: { 'Idempotency-Key': key },
      body: JSON.stringify({
        editedPayloadJson: JSON.stringify(nativeDraftPayload.value),
        confirmAction: 'create_tasks',
        confirmationNote: 'Người quản lý đã review và xác nhận các task được chọn.',
        rowVersion: nativeDraft.value.rowVersion,
        idempotencyKey: key,
      }),
    })
    nativeCreatedTaskIds.value = result.createdTaskIds
    await fetchNativeDraft(nativeDraft.value.draftId)
    showSuccess(`Đã tạo ${result.createdTaskCount} task được chọn.`)
  } catch (error) {
    showError(errorMessage(error, 'Không thể xác nhận. Nguồn hoặc draft có thể đã thay đổi; chưa tạo task dở dang.'))
  } finally {
    nativeDraftBusy.value = false
  }
}

async function rejectNativeDraft() {
  if (!nativeDraft.value || nativeDraftBusy.value) return
  nativeDraftBusy.value = true
  try {
    const key = crypto.randomUUID()
    const detail = await apiResult<NativeAiDraftDetail>(`/api/ai/drafts/${nativeDraft.value.draftId}/reject`, {
      method: 'POST',
      headers: { 'Idempotency-Key': key },
      body: JSON.stringify({
        reason: 'Người quản lý từ chối task draft.',
        rowVersion: nativeDraft.value.rowVersion,
        idempotencyKey: key,
      }),
    })
    nativeDraft.value = detail
    showSuccess('Đã từ chối draft. Không có task nào được tạo.')
  } catch (error) {
    showError(errorMessage(error, 'Không thể từ chối draft.'))
  } finally {
    nativeDraftBusy.value = false
  }
}

async function retryNativeJob() {
  if (!nativeJob.value || nativeDraftBusy.value) return
  nativeDraftBusy.value = true
  try {
    nativeJob.value = await apiResult<NativeAiJobDetail>(`/api/ai/jobs/${nativeJob.value.jobId}/retry`, {
      method: 'POST',
      body: JSON.stringify({ providerOverride: null }),
    })
    nativeDraftError.value = ''
    await monitorNativeJob(nativeJob.value.jobId)
  } catch (error) {
    showError(errorMessage(error, 'Job này không thể retry.'))
  } finally {
    nativeDraftBusy.value = false
  }
}

async function cancelNativeJob() {
  if (!nativeJob.value || nativeDraftBusy.value) return
  nativeDraftBusy.value = true
  try {
    nativeJob.value = await apiResult<NativeAiJobDetail>(`/api/ai/jobs/${nativeJob.value.jobId}/cancel`, {
      method: 'POST',
      body: JSON.stringify({ reason: 'Người dùng hủy từ native task draft review.' }),
    })
    nativeDraftPollToken++
    nativeDraftError.value = 'Đã hủy job. Không có task nào được tạo.'
  } catch (error) {
    showError(errorMessage(error, 'Không thể hủy job.'))
  } finally {
    nativeDraftBusy.value = false
  }
}

function nativeSourceHref(sourceRef: string) {
  const messageId = sourceRef.startsWith('message:') ? sourceRef.slice('message:'.length) : ''
  return messageId ? `/groups/${props.groupId}?messageId=${messageId}` : null
}

function nativeJobStatusLabel(status?: string) {
  const value = status?.toLowerCase()
  if (value === 'queued') return 'Đang xếp hàng'
  if (value === 'running') return 'AI đang đọc nguồn và soạn task'
  if (value === 'retrying') return 'Đang chờ retry'
  if (value === 'succeeded') return 'Đã tạo draft để review'
  if (value === 'failed') return 'Thất bại trung thực'
  if (value === 'canceled' || value === 'cancelled') return 'Đã hủy'
  return status || 'Chưa chạy'
}

function nativeSummaryStatusLabel(status?: string) {
  const value = status?.toLowerCase()
  if (value === 'queued') return 'Đang xếp hàng'
  if (value === 'running') return 'AI đang đọc đúng các tin đã chọn'
  if (value === 'retrying') return 'Đang chờ retry'
  if (value === 'succeeded') return 'Đã có bản tóm tắt kiểm chứng được'
  if (value === 'failed') return 'Không thể tạo kết quả hợp lệ'
  if (value === 'canceled' || value === 'cancelled') return 'Đã hủy'
  return status || 'Chưa chạy'
}

const nativeSummaryRunning = computed(() => ['queued', 'running', 'retrying'].includes(nativeSummaryJob.value?.status.toLowerCase() ?? ''))

async function retryNativeSummary() {
  if (!nativeSummaryJob.value || nativeSummaryBusy.value) return
  nativeSummaryBusy.value = true
  try {
    nativeSummaryJob.value = await apiResult<NativeAiJobDetail>(`/api/ai/jobs/${nativeSummaryJob.value.jobId}/retry`, {
      method: 'POST',
      body: JSON.stringify({ providerOverride: null }),
    })
    await monitorNativeSummary(nativeSummaryJob.value.jobId)
  } catch (error) {
    showError(errorMessage(error, 'Không thể retry bản tóm tắt này.'))
  } finally {
    nativeSummaryBusy.value = false
  }
}

async function cancelNativeSummary() {
  if (!nativeSummaryJob.value || nativeSummaryBusy.value) return
  nativeSummaryBusy.value = true
  try {
    nativeSummaryJob.value = await apiResult<NativeAiJobDetail>(`/api/ai/jobs/${nativeSummaryJob.value.jobId}/cancel`, {
      method: 'POST',
      body: JSON.stringify({ reason: 'Người dùng hủy Group selected-range summary.' }),
    })
    nativeSummaryPollToken++
    nativeSummaryError.value = 'Đã hủy job; không có kết quả giả được tạo.'
  } catch (error) {
    showError(errorMessage(error, 'Không thể hủy job.'))
  } finally {
    nativeSummaryBusy.value = false
  }
}

const nativeJobRunning = computed(() => ['queued', 'running', 'retrying'].includes(nativeJob.value?.status.toLowerCase() ?? ''))

watch(
  () => props.selectedRequest?.nonce,
  async nonce => {
    if (!nonce || !props.selectedRequest) return
    const request = props.selectedRequest
    const requestGroupId = props.groupId
    selectedAction.value = request.action
    selectedMessageIds.value = [...request.messageIds]
    subTab.value = request.action === 'summary' ? 'summary' : 'draft'
    try {
      await loadLinkedProjects()
      if (props.groupId !== requestGroupId || props.selectedRequest?.nonce !== nonce) return
      // The group watcher also runs immediately when this panel is first mounted.
      // Reapply the captured request after its reset so the first contextual action is not lost.
      selectedAction.value = request.action
      selectedMessageIds.value = [...request.messageIds]
      subTab.value = request.action === 'summary' ? 'summary' : 'draft'
      if (!linkedProjects.value.length) {
        showError('Nhóm chưa liên kết với Project nào. Hãy liên kết Project trước khi dùng AI.')
        return
      }
      if (linkedProjects.value.length === 1) await submitSelectedMessages()
    } catch (error) {
      showError(errorMessage(error, 'Không thể đọc Project liên kết của nhóm.'))
    }
  },
  { immediate: true },
)

// Reset states when group changes
watch(() => props.groupId, async () => {
  nativeDraftPollToken++
  nativeSummaryPollToken++
  selectedMessageIds.value = []
  selectedProjectId.value = ''
  summaryText.value = ''
  keyDecisions.value = []
  unresolvedQuestions.value = []
  messageSources.value = []
  summaryWarnings.value = []
  hasGeneratedSummary.value = false

  draftProjectName.value = ''
  draftProjectDescription.value = ''
  draftTasks.value = []
  draftWarnings.value = []
  extraInstructions.value = ''
  hasGeneratedDraft.value = false

  actionItems.value = []
  actionWarnings.value = []
  hasGeneratedActions.value = false
  nativeJob.value = null
  nativeDraft.value = null
  nativeDraftPayload.value = null
  nativeDraftError.value = ''
  nativeCreatedTaskIds.value = []
  nativeSummaryJob.value = null
  nativeSummaryResult.value = null
  nativeSummaryError.value = ''
  await Promise.all([restoreNativeDraft(), restoreNativeSummary()])
}, { immediate: true })

// Matching function to map a suggested assignee string to actual user UUID
function findAssigneeId(suggestedName: string | null): string | null {
  if (!suggestedName || !props.members || props.members.length === 0) return null
  const nameClean = suggestedName.toLowerCase().trim()
  
  // Try exact or substring match on fullName
  const match = props.members.find(m => {
    const fullNameClean = m.fullName.toLowerCase().trim()
    return fullNameClean.includes(nameClean) || nameClean.includes(fullNameClean)
  })
  if (match) return match.userId

  // Try email prefix match
  const matchEmail = props.members.find(m => {
    const prefix = m.email.toLowerCase().split('@')[0]
    return prefix === nameClean
  })
  return matchEmail ? matchEmail.userId : null
}

// 1. Tóm tắt thảo luận API
async function generateSummary() {
  if (!hasGroup.value || isSummaryLoading.value) return

  isSummaryLoading.value = true
  summaryWarnings.value = []

  try {
    const result = await apiResult<GroupAiSummaryResponse>(`/api/groups/${props.groupId}/ai/summary`, {
      method: 'POST',
      body: JSON.stringify({
        messageLimit: 80
      })
    })

    summaryText.value = result.summary
    keyDecisions.value = result.keyDecisions ?? []
    unresolvedQuestions.value = result.unresolvedQuestions ?? []
    messageSources.value = result.messageSources ?? []
    summaryWarnings.value = result.warnings ?? []
    hasGeneratedSummary.value = true
    showSuccess('Đã tóm tắt cuộc thảo luận thành công!')
    startCooldown('summary', 15)
  } catch (error) {
    showError(errorMessage(error, 'Không thể tạo tóm tắt thảo luận.'))
  } finally {
    isSummaryLoading.value = false
  }
}

// 2. Dự thảo Project API
async function generateDraftProject() {
  if (!hasGroup.value || isDraftLoading.value) return

  isDraftLoading.value = true
  draftWarnings.value = []

  try {
    const result = await apiResult<GroupAiDraftProjectResponse>(`/api/groups/${props.groupId}/ai/draft-project`, {
      method: 'POST',
      body: JSON.stringify({
        messageLimit: 80,
        extraInstructions: extraInstructions.value.trim() || null
      })
    })

    draftProjectName.value = result.draftProjectName
    draftProjectDescription.value = result.draftProjectDescription
    
    // Map initial attributes & perform automatic user assignment matching
    draftTasks.value = (result.draftTasks ?? []).map(task => {
      const matchedUserId = findAssigneeId(task.suggestedOwnerName)
      return {
        ...task,
        checked: true, // Selected by default
        priority: task.priority || 'Medium',
        estimateDays: task.estimateDays || 3,
        assigneeId: matchedUserId
      }
    })

    draftWarnings.value = result.warnings ?? []
    hasGeneratedDraft.value = true
    showSuccess('Đã thiết lập dự thảo dự án thành công!')
    startCooldown('draft', 15)
  } catch (error) {
    showError(errorMessage(error, 'Không thể tạo dự thảo project.'))
  } finally {
    isDraftLoading.value = false
  }
}

// 3. Trích xuất Action items API
async function extractActionItems() {
  if (!hasGroup.value || isActionLoading.value) return

  isActionLoading.value = true
  actionWarnings.value = []

  try {
    const result = await apiResult<GroupAiActionItemsResponse>(`/api/groups/${props.groupId}/ai/action-items`, {
      method: 'POST',
      body: JSON.stringify({
        source: 'chat',
        messageLimit: 80
      })
    })

    actionItems.value = result.items ?? []
    actionWarnings.value = result.warnings ?? []
    hasGeneratedActions.value = true
    showSuccess('Đã trích xuất các hành động thảo luận!')
    startCooldown('action', 15)
  } catch (error) {
    showError(errorMessage(error, 'Không thể trích xuất hành động.'))
  } finally {
    isActionLoading.value = false
  }
}

// Delete a draft task from local listing before creating
function removeDraftTask(index: number) {
  draftTasks.value.splice(index, 1)
}

// Add a blank task row
function addDraftTaskRow() {
  draftTasks.value.push({
    title: 'Task mới',
    description: '',
    priority: 'Medium',
    estimateDays: 3,
    suggestedOwnerName: null,
    checked: true,
    assigneeId: null
  })
}

// Create real project & chosen tasks
async function confirmAndCreateRealProject() {
  if (!draftProjectName.value.trim() || isCreatingProject.value) return

  const selectedTasks = draftTasks.value.filter(t => t.checked)
  const confirmed = await confirmDialog({
    tone: 'warning',
    title: 'Tạo project chính thức?',
    subject: draftProjectName.value.trim(),
    message: `Hệ thống sẽ tạo project mới và ${selectedTasks.length} task đã chọn từ bản nháp AI này.`,
    confirmLabel: 'Tạo project',
  })
  if (!confirmed) return
  
  isCreatingProject.value = true

  try {
    // 1. Create standard project from group
    // Code is generated dynamically using first 4 letters or left null
    const codeSuggestion = draftProjectName.value
      .substring(0, 4)
      .toUpperCase()
      .replace(/[^A-Z0-9]/g, '') || 'PROJ'

    const projectResult = await apiResult<any>(`/api/groups/${props.groupId}/create-project`, {
      method: 'POST',
      body: JSON.stringify({
        name: draftProjectName.value.trim(),
        code: codeSuggestion,
        description: draftProjectDescription.value.trim() || null
      })
    })

    const projectId = projectResult.project?.id
    if (!projectId) {
      throw new Error('Không nhận được mã dự án vừa tạo.')
    }

    // 2. Sequentially create each selected task
    let tasksCreatedCount = 0
    for (const task of selectedTasks) {
      const estDays = task.estimateDays || 1
      const estHours = estDays * 8
      const dueDate = new Date()
      dueDate.setDate(dueDate.getDate() + estDays)

      const taskPayload = {
        title: task.title.trim(),
        description: task.description ? task.description.trim() : null,
        priority: task.priority || 'Medium',
        dueDate: dueDate.toISOString(),
        estimatedHours: estHours,
        projectId: projectId,
        assigneeId: task.assigneeId || null,
        isPrivate: false,
        isPinned: false,
        contributesToProgress: true,
        assigneeIds: task.assigneeId ? [task.assigneeId] : []
      }

      await apiResult('/api/tasks', {
        method: 'POST',
        body: JSON.stringify(taskPayload)
      })
      tasksCreatedCount++
    }

    showSuccess(`Đã tạo thành công dự án "${draftProjectName.value}" cùng ${tasksCreatedCount} công việc!`)
    
    // Redirect to newly created project
    router.push({ name: 'project-detail', params: { projectId } })
    
    // Clear draft states
    hasGeneratedDraft.value = false
    draftTasks.value = []
  } catch (error) {
    showError(errorMessage(error, 'Lỗi trong quá trình tạo dự án và công việc.'))
  } finally {
    isCreatingProject.value = false
  }
}

function confidenceLabel(value: number) {
  return `${Math.round(value * 100)}%`
}
</script>

<template>
  <aside class="group-ai-panel glass-card">
    <header class="group-ai-panel__header">
      <div class="header-title">
        <Sparkles class="ai-spark-icon animate-pulse" :size="18" />
        <h2>Trợ lý AI</h2>
      </div>
    </header>

    <section v-if="selectedMessageIds.length" class="selected-ai-source">
      <strong>{{ selectedMessageIds.length }} tin nhắn đã chọn</strong>
      <select v-if="linkedProjects.length > 1" v-model="selectedProjectId" aria-label="Chọn Project đích">
        <option value="" disabled>Chọn Project đích</option>
        <option v-for="project in linkedProjects" :key="project.projectId" :value="project.projectId">
          {{ project.code ? `${project.code} - ` : '' }}{{ project.name }}
        </option>
      </select>
      <button
        v-if="linkedProjects.length > 1"
        class="primary-button"
        type="button"
        :disabled="!selectedProjectId || isSelectedRequestLoading"
        @click="submitSelectedMessages"
      >
        <Loader2 v-if="isSelectedRequestLoading" :size="15" class="spin-icon" />
        <Sparkles v-else :size="15" />
        {{ selectedAction === 'summary' ? 'Tóm tắt tin đã chọn' : 'Tạo task draft' }}
      </button>
    </section>

    <!-- Sub Navigation Tabs -->
    <nav class="group-ai-tabs" aria-label="AI Tools Sub Navigation">
      <button 
        :class="{ active: subTab === 'summary' }" 
        @click="subTab = 'summary'"
        type="button"
      >
        <FileText :size="14" />
        <span>Tóm tắt</span>
      </button>
      <button 
        :class="{ active: subTab === 'draft' }" 
        @click="subTab = 'draft'"
        type="button"
      >
        <FolderKanban :size="14" />
        <span>Dự thảo</span>
      </button>
      <button 
        :class="{ active: subTab === 'action-items' }" 
        @click="subTab = 'action-items'"
        type="button"
      >
        <ListChecks :size="14" />
        <span>Hành động</span>
      </button>
    </nav>

    <!-- Content Sections -->
    <div class="group-ai-panel__content no-scrollbar">
      
      <!-- SUB-TAB 1: TÓM TẮT THẢO LUẬN -->
      <div v-if="subTab === 'summary'" class="tab-view-container">
        <section v-if="nativeSummaryJob" class="native-task-review native-summary-review" data-testid="group-selected-summary-review">
          <header class="native-task-review__header">
            <div>
              <span class="native-eyebrow">Đúng đoạn chat đã chọn · group_selected_summary.v1</span>
              <h3>{{ nativeSummaryStatusLabel(nativeSummaryJob.status) }}</h3>
              <p>AI chỉ được dùng các tin nhắn trong danh sách nguồn bên dưới; mỗi nhận định phải trỏ lại nguồn.</p>
            </div>
            <span class="native-progress">{{ nativeSummaryJob.progressPercent }}%</span>
          </header>

          <div v-if="nativeSummaryRunning" class="native-process" aria-live="polite">
            <Loader2 :size="17" class="spin-icon" />
            <div>
              <strong>{{ nativeSummaryStatusLabel(nativeSummaryJob.status) }}</strong>
              <span>Kiểm quyền từng tin → giữ đúng thứ tự → gọi model → kiểm schema/nguồn → lưu read-back</span>
            </div>
            <button type="button" class="text-button text-button--danger" :disabled="nativeSummaryBusy" @click="cancelNativeSummary">Hủy job</button>
          </div>

          <div v-if="nativeSummaryError" class="warnings-box native-error">
            <AlertTriangle :size="15" />
            <div>
              <strong>{{ nativeSummaryError }}</strong>
              <small v-if="nativeSummaryJob.lastErrorCode">Mã lỗi: {{ nativeSummaryJob.lastErrorCode }}</small>
            </div>
            <button v-if="nativeSummaryJob.lastErrorRetryable" type="button" class="text-button" :disabled="nativeSummaryBusy" @click="retryNativeSummary">Retry</button>
          </div>

          <div v-if="nativeSummaryJob.status.toLowerCase() === 'succeeded'" class="native-truth-strip">
            <span>Provider: <strong>{{ nativeSummaryJob.selectedProvider || 'không xác định' }}</strong></span>
            <span>Model: <strong>{{ nativeSummaryJob.selectedModel || 'không xác định' }}</strong></span>
            <span v-if="nativeSummaryResult?.cacheHit">Cache hit hợp lệ</span>
            <span v-if="nativeSummaryResult?.sourceStale" class="native-danger">Nguồn đã thay đổi · hãy chạy lại</span>
          </div>

          <template v-if="nativeSummaryResult?.result">
            <section class="result-section">
              <h3 class="section-title"><FileText :size="15" /> Tóm tắt đúng {{ nativeSummaryResult.result.messageRange.messageIds.length }} tin</h3>
              <div class="summary-body-text">{{ nativeSummaryResult.result.summary }}</div>
              <div class="native-sources">
                <strong>Nguồn tóm tắt:</strong>
                <a v-for="sourceRef in nativeSummaryResult.result.summarySourceRefs" :key="sourceRef" :href="nativeSourceHref(sourceRef) || undefined">
                  {{ sourceRef.replace('message:', 'Tin ') }}
                </a>
              </div>
            </section>

            <section class="result-section">
              <h3 class="section-title success-color"><CheckCircle2 :size="15" /> Quyết định</h3>
              <ul v-if="nativeSummaryResult.result.keyDecisions.length" class="grounded-list">
                <li v-for="item in nativeSummaryResult.result.keyDecisions" :key="`${item.text}-${item.sourceRefs.join('-')}`">
                  <span>{{ item.text }}</span>
                  <a v-for="sourceRef in item.sourceRefs" :key="sourceRef" :href="nativeSourceHref(sourceRef) || undefined">{{ sourceRef.replace('message:', 'Tin ') }}</a>
                </li>
              </ul>
              <div v-else class="empty-bullet-text">Không có quyết định đủ bằng chứng trong đoạn đã chọn.</div>
            </section>

            <section class="result-section">
              <h3 class="section-title warning-color"><HelpCircle :size="15" /> Câu hỏi mở</h3>
              <ul v-if="nativeSummaryResult.result.openQuestions.length" class="grounded-list">
                <li v-for="item in nativeSummaryResult.result.openQuestions" :key="`${item.text}-${item.sourceRefs.join('-')}`">
                  <span>{{ item.text }}</span>
                  <a v-for="sourceRef in item.sourceRefs" :key="sourceRef" :href="nativeSourceHref(sourceRef) || undefined">{{ sourceRef.replace('message:', 'Tin ') }}</a>
                </li>
              </ul>
              <div v-else class="empty-bullet-text">Không có câu hỏi mở đủ bằng chứng.</div>
            </section>

            <section class="result-section">
              <h3 class="section-title"><ListChecks :size="15" /> Hành động đề xuất</h3>
              <ul v-if="nativeSummaryResult.result.actionCandidates.length" class="grounded-list">
                <li v-for="item in nativeSummaryResult.result.actionCandidates" :key="`${item.title}-${item.sourceRefs.join('-')}`">
                  <strong>{{ item.title }}</strong>
                  <span>{{ item.details }}</span>
                  <a v-for="sourceRef in item.sourceRefs" :key="sourceRef" :href="nativeSourceHref(sourceRef) || undefined">{{ sourceRef.replace('message:', 'Tin ') }}</a>
                </li>
              </ul>
              <div v-else class="empty-bullet-text">Không có hành động nào được suy diễn khi chưa đủ bằng chứng.</div>
            </section>
          </template>
        </section>

        <div class="action-trigger-box">
          <p class="helper-text">Tổng hợp nội dung thảo luận gần đây trong cuộc trò chuyện nhóm, rút ra các quyết định then chốt.</p>
          <button
            class="primary-button ai-action-btn"
            type="button"
            :disabled="!hasGroup || isSummaryLoading || summaryCooldown > 0"
            @click="generateSummary"
          >
            <Loader2 v-if="isSummaryLoading" :size="16" class="spin-icon" />
            <Sparkles v-else :size="16" />
            <span>{{ summaryCooldown > 0 ? `Tóm tắt thảo luận (Chờ ${summaryCooldown}s)` : 'Tóm tắt thảo luận' }}</span>
          </button>
        </div>

        <div v-if="summaryWarnings.length" class="warnings-box">
          <AlertTriangle :size="14" />
          <div class="warnings-list">
            <span v-for="warning in summaryWarnings" :key="warning">{{ warning }}</span>
          </div>
        </div>

        <div v-if="hasGeneratedSummary" class="ai-result-content">
          <section class="result-section">
            <h3 class="section-title">
              <FileText :size="15" />
              Tóm tắt chung
            </h3>
            <div class="summary-body-text">
              {{ summaryText || 'Không có tóm tắt chi tiết.' }}
            </div>
          </section>

          <section class="result-section">
            <h3 class="section-title success-color">
              <CheckCircle2 :size="15" />
              Quyết định chính
            </h3>
            <ul v-if="keyDecisions.length" class="bullet-list">
              <li v-for="(dec, idx) in keyDecisions" :key="idx">{{ dec }}</li>
            </ul>
            <div v-else class="empty-bullet-text">Không phát hiện quyết định cụ thể nào.</div>
          </section>

          <section class="result-section">
            <h3 class="section-title warning-color">
              <HelpCircle :size="15" />
              Câu hỏi chưa giải quyết
            </h3>
            <ul v-if="unresolvedQuestions.length" class="bullet-list">
              <li v-for="(q, idx) in unresolvedQuestions" :key="idx">{{ q }}</li>
            </ul>
            <div v-else class="empty-bullet-text">Mọi câu hỏi thảo luận đã được giải đáp.</div>
          </section>

          <section v-if="messageSources.length" class="result-section">
            <h3 class="section-title text-slate-500">
              <FileText :size="15" />
              Nguồn đối chiếu (Message Sources)
            </h3>
            <ul class="bullet-list">
              <li v-for="(src, idx) in messageSources" :key="idx" class="italic text-slate-600">
                {{ src }}
              </li>
            </ul>
          </section>
        </div>

        <div v-else-if="!isSummaryLoading" class="panel-empty-placeholder">
          <FileText :size="24" class="muted-icon" />
          <span>Bấm nút phía trên để bắt đầu tóm tắt</span>
        </div>
      </div>

      <!-- SUB-TAB 2: DỰ THẢO PROJECT & TASKS -->
      <div v-if="subTab === 'draft'" class="tab-view-container">

        <section
          v-if="nativeJob || nativeDraft || nativeDraftError"
          class="native-task-review"
          data-testid="source-linked-task-draft-review"
        >
          <header class="native-task-review__header">
            <div>
              <span class="native-eyebrow">Task draft có nguồn · task_draft.v5</span>
              <h3>{{ nativeJobStatusLabel(nativeJob?.status) }}</h3>
              <p>AI chỉ soạn bản nháp từ các tin nhắn đã chọn. Task chỉ được tạo sau khi bạn chọn và xác nhận.</p>
            </div>
            <span class="native-progress">{{ nativeJob?.progressPercent ?? 0 }}%</span>
          </header>

          <div v-if="nativeJobRunning" class="native-process" aria-live="polite">
            <Loader2 :size="17" class="spin-icon" />
            <div>
              <strong>{{ nativeJobStatusLabel(nativeJob?.status) }}</strong>
              <span>Kiểm quyền nguồn → đọc tin đã chọn → gọi model → kiểm schema → chờ review</span>
            </div>
            <button type="button" class="text-button text-button--danger" :disabled="nativeDraftBusy" @click="cancelNativeJob">Hủy job</button>
          </div>

          <div v-if="nativeDraftError" class="warnings-box native-error">
            <AlertTriangle :size="15" />
            <div>
              <strong>{{ nativeDraftError }}</strong>
              <small v-if="nativeJob?.lastErrorCode">Mã lỗi: {{ nativeJob.lastErrorCode }}</small>
            </div>
            <button
              v-if="nativeJob?.lastErrorRetryable"
              type="button"
              class="text-button"
              :disabled="nativeDraftBusy"
              @click="retryNativeJob"
            >
              Retry
            </button>
          </div>

          <div v-if="nativeJob?.status.toLowerCase() === 'succeeded'" class="native-truth-strip">
            <span>Provider: <strong>{{ nativeJob.selectedProvider || 'không xác định' }}</strong></span>
            <span>Model thực tế: <strong>{{ nativeJob.selectedModel || 'không xác định' }}</strong></span>
            <span v-if="nativeJob.isMock" class="native-danger">Mock không được phép dùng làm kết quả native</span>
          </div>

          <div v-if="nativeDraftPayload?.dataState === 'insufficient_evidence'" class="panel-empty-placeholder native-empty">
            <FileText :size="22" />
            <strong>Các tin nhắn đã chọn chưa đủ để tạo task có thể kiểm chứng.</strong>
            <span>Hãy chọn thêm tin nhắn có hành động, đầu ra hoặc deadline rõ ràng.</span>
          </div>

          <div v-else-if="nativeDraftPayload" class="native-task-list">
            <article
              v-for="task in nativeDraftPayload.tasks"
              :key="task.clientId"
              class="native-task-card"
              :class="{ 'native-task-card--unchecked': !task.selected }"
            >
              <div class="native-task-card__top">
                <label class="custom-checkbox">
                  <input v-model="task.selected" type="checkbox" :disabled="nativeDraft?.status !== 'pending_review'" />
                  <span class="checkmark"></span>
                </label>
                <input
                  v-model="task.title"
                  class="task-title-input"
                  maxlength="200"
                  aria-label="Tiêu đề task draft"
                  :disabled="nativeDraft?.status !== 'pending_review'"
                />
                <span class="confidence-tag">{{ confidenceLabel(task.confidence) }}</span>
              </div>
              <textarea
                v-model="task.description"
                rows="3"
                maxlength="4000"
                class="task-desc-textarea"
                aria-label="Mô tả task draft"
                :disabled="nativeDraft?.status !== 'pending_review'"
              ></textarea>
              <div class="native-task-fields">
                <label>
                  Ưu tiên
                  <select v-model="task.priority" class="metadata-select" :disabled="nativeDraft?.status !== 'pending_review'">
                    <option value="Low">Thấp</option>
                    <option value="Medium">Trung bình</option>
                    <option value="High">Cao</option>
                    <option value="Critical">Khẩn cấp</option>
                  </select>
                </label>
                <label>
                  Deadline
                  <input
                    type="date"
                    :value="task.dueDate?.slice(0, 10) || ''"
                    :disabled="nativeDraft?.status !== 'pending_review'"
                    @input="task.dueDate = ($event.target as HTMLInputElement).value ? new Date(`${($event.target as HTMLInputElement).value}T23:59:59`).toISOString() : null"
                  />
                </label>
                <label>
                  Người thực hiện
                  <select v-model="task.assigneeId" class="metadata-select" :disabled="nativeDraft?.status !== 'pending_review'">
                    <option :value="null">Chưa phân công</option>
                    <option v-for="member in members" :key="member.userId" :value="member.userId">{{ member.fullName }}</option>
                  </select>
                </label>
              </div>
              <div class="native-sources">
                <strong>Nguồn:</strong>
                <a
                  v-for="sourceRef in task.sourceRefs"
                  :key="sourceRef"
                  :href="nativeSourceHref(sourceRef) || undefined"
                >
                  {{ sourceRef.replace('message:', 'Tin nhắn ') }}
                </a>
              </div>
            </article>
          </div>

          <footer v-if="nativeDraft" class="native-review-actions">
            <template v-if="nativeDraft.status === 'pending_review'">
              <button type="button" class="text-button" :disabled="nativeDraftBusy" @click="saveNativeDraft">Lưu draft</button>
              <button type="button" class="text-button text-button--danger" :disabled="nativeDraftBusy" @click="rejectNativeDraft">Từ chối</button>
              <button type="button" class="primary-button" :disabled="nativeDraftBusy || !nativeDraftPayload?.tasks.some(task => task.selected)" @click="confirmNativeDraft">
                <Loader2 v-if="nativeDraftBusy" :size="15" class="spin-icon" />
                <Check v-else :size="15" />
                Tạo các task đã chọn
              </button>
            </template>
            <strong v-else-if="nativeDraft.status === 'rejected'">Draft đã bị từ chối · không có task nào được tạo.</strong>
            <div v-else-if="nativeDraft.status === 'confirmed'" class="native-receipt">
              <CheckCircle2 :size="17" />
              <div>
                <strong>Đã xác nhận và có thể đọc lại</strong>
                <a v-for="taskId in nativeCreatedTaskIds" :key="taskId" :href="`/projects/${nativeDraft.projectId}/tasks/${taskId}`">Mở task {{ taskId.slice(0, 8) }}</a>
              </div>
            </div>
          </footer>
        </section>
        
        <div v-if="!nativeJob && !nativeDraft && !hasGeneratedDraft" class="action-trigger-box">
          <p class="helper-text">Tự động đề xuất cấu trúc dự án và lập danh sách công việc cụ thể dựa trên trao đổi của nhóm.</p>
          
          <div class="instructions-input-group">
            <label for="extra-instructions">Yêu cầu bổ sung cho AI (Tùy chọn):</label>
            <textarea
              id="extra-instructions"
              v-model="extraInstructions"
              rows="2"
              placeholder="Ví dụ: Tập trung phân chia các tasks Frontend; Dự án kéo dài trong 2 tuần..."
              class="ai-instructions-textarea"
            ></textarea>
          </div>

          <button
            class="primary-button ai-action-btn"
            type="button"
            :disabled="!hasGroup || isDraftLoading || draftCooldown > 0"
            @click="generateDraftProject"
          >
            <Loader2 v-if="isDraftLoading" :size="16" class="spin-icon" />
            <Sparkles v-else :size="16" />
            <span>{{ draftCooldown > 0 ? `Tạo project nháp (Chờ ${draftCooldown}s)` : 'Tạo project nháp' }}</span>
          </button>
        </div>

        <div v-if="draftWarnings.length" class="warnings-box">
          <AlertTriangle :size="14" />
          <div class="warnings-list">
            <span v-for="warning in draftWarnings" :key="warning">{{ warning }}</span>
          </div>
        </div>

        <!-- DRAFT RENDER & EDIT VIEW -->
        <div v-if="hasGeneratedDraft" class="draft-edit-container">
          <div class="draft-project-info glass-card">
            <div class="form-group">
              <label>Tên dự án dự kiến</label>
              <input type="text" v-model="draftProjectName" class="premium-input font-bold" />
            </div>
            <div class="form-group">
              <label>Mô tả dự án</label>
              <textarea v-model="draftProjectDescription" rows="2" class="premium-textarea"></textarea>
            </div>
          </div>

          <div class="draft-tasks-header">
            <h4>Danh sách công việc dự thảo ({{ draftTasks.length }})</h4>
            <button class="text-button text-button--small" type="button" @click="addDraftTaskRow">
              <Plus :size="14" /> Thêm hàng
            </button>
          </div>

          <div class="draft-tasks-list">
            <article 
              v-for="(task, index) in draftTasks" 
              :key="index" 
              class="draft-task-card"
              :class="{ 'draft-task-card--unchecked': !task.checked }"
            >
              <div class="draft-task-card__header">
                <label class="custom-checkbox">
                  <input type="checkbox" v-model="task.checked" />
                  <span class="checkmark"></span>
                </label>
                <input 
                  type="text" 
                  v-model="task.title" 
                  class="task-title-input" 
                  placeholder="Tiêu đề task"
                />
                <button 
                  class="icon-button icon-button--danger text-red-500" 
                  type="button" 
                  title="Xóa hàng"
                  @click="removeDraftTask(index)"
                >
                  <Trash2 :size="14" />
                </button>
              </div>

              <div class="draft-task-card__body">
                <textarea 
                  v-model="task.description" 
                  rows="2" 
                  class="task-desc-textarea" 
                  placeholder="Mô tả công việc chi tiết..."
                ></textarea>
                
                <div class="task-metadata-grid">
                  <div class="metadata-col">
                    <label>Độ ưu tiên</label>
                    <select v-model="task.priority" class="metadata-select">
                      <option value="Low">Thấp</option>
                      <option value="Medium">Trung bình</option>
                      <option value="High">Cao</option>
                    </select>
                  </div>

                  <div class="metadata-col">
                    <label>Số ngày</label>
                    <input 
                      type="number" 
                      v-model="task.estimateDays" 
                      min="1" 
                      max="100" 
                      class="metadata-number-input"
                    />
                  </div>

                  <div class="metadata-col">
                    <label>Người phụ trách</label>
                    <select v-model="task.assigneeId" class="metadata-select">
                      <option :value="null">Chưa phân công</option>
                      <option 
                        v-for="m in members" 
                        :key="m.userId" 
                        :value="m.userId"
                      >
                        {{ m.fullName }}
                      </option>
                    </select>
                  </div>
                </div>
              </div>
            </article>
          </div>

          <div class="draft-actions-footer">
            <button
              class="primary-button footer-confirm-btn"
              type="button"
              :disabled="isCreatingProject || draftTasks.filter(t => t.checked).length === 0"
              @click="confirmAndCreateRealProject"
            >
              <Loader2 v-if="isCreatingProject" :size="16" class="spin-icon" />
              <Check v-else :size="16" />
              <span>Xác nhận tạo dự án chính thức</span>
            </button>
            <button 
              class="text-button text-button--danger text-center w-full"
              type="button"
              @click="hasGeneratedDraft = false"
            >
              Hủy dự thảo
            </button>
          </div>
        </div>

        <div v-else-if="!isDraftLoading && !nativeJob && !nativeDraft" class="panel-empty-placeholder">
          <FolderKanban :size="24" class="muted-icon" />
          <span>Bấm nút phía trên để lập dự án nháp</span>
        </div>
      </div>

      <!-- SUB-TAB 3: ACTION ITEMS / HÀNH ĐỘNG -->
      <div v-if="subTab === 'action-items'" class="tab-view-container">
        <div class="action-trigger-box">
          <p class="helper-text">Tìm kiếm và trích xuất trực tiếp các việc cần làm được đề cập trong hội thoại.</p>
          <button
            class="primary-button ai-action-btn"
            type="button"
            :disabled="!hasGroup || isActionLoading || actionCooldown > 0"
            @click="extractActionItems"
          >
            <Loader2 v-if="isActionLoading" :size="16" class="spin-icon" />
            <Sparkles v-else :size="16" />
            <span>{{ actionCooldown > 0 ? `Trích xuất Action Items (Chờ ${actionCooldown}s)` : 'Trích xuất Action Items' }}</span>
          </button>
        </div>

        <div v-if="actionWarnings.length" class="warnings-box">
          <AlertTriangle :size="14" />
          <div class="warnings-list">
            <span v-for="warning in actionWarnings" :key="warning">{{ warning }}</span>
          </div>
        </div>

        <div v-if="showActionItemsList" class="action-items-list">
          <article v-for="item in actionItems" :key="`${item.title}-${item.confidence}`" class="group-ai-item">
            <div class="group-ai-item__title">
              <ListChecks :size="15" />
              <strong>{{ item.title }}</strong>
            </div>
            <p v-if="item.description" class="group-ai-item__desc">{{ item.description }}</p>
            <div class="group-ai-item__meta">
              <span v-if="item.suggestedOwnerName" class="meta-tag">
                <User :size="10" /> {{ item.suggestedOwnerName }}
              </span>
              <span v-if="item.dueDateSuggestion" class="meta-tag">
                <Calendar :size="10" /> {{ new Date(item.dueDateSuggestion).toLocaleDateString('vi') }}
              </span>
              <span class="meta-tag confidence-tag">{{ confidenceLabel(item.confidence) }}</span>
            </div>
            <small v-if="item.sourceEvidence" class="group-ai-item__source">
              <em>Dẫn chứng:</em> "{{ item.sourceEvidence }}"
            </small>
          </article>
        </div>

        <div v-else-if="showEmptyActionPlaceholder" class="panel-empty-placeholder">
          <ListChecks :size="24" class="muted-icon" />
          <span>Không phát hiện việc cần làm nào trong đoạn chat gần đây.</span>
        </div>

        <div v-else-if="!isActionLoading" class="panel-empty-placeholder">
          <ListChecks :size="24" class="muted-icon" />
          <span>Bấm nút phía trên để trích xuất hành động</span>
        </div>
      </div>

    </div>
  </aside>
</template>

<style scoped>
.group-ai-panel {
  display: grid;
  grid-template-rows: auto auto minmax(0, 1fr);
  gap: 12px;
  background: transparent;
  border: none;
  backdrop-filter: none;
  border-radius: 0;
  padding: 0;
  margin-top: 10px;
}

.group-ai-panel__header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding-bottom: 4px;
}

.header-title {
  display: flex;
  align-items: center;
  gap: 8px;
}

.ai-spark-icon {
  color: var(--primary);
}

.group-ai-panel__header h2 {
  font-size: 1.15rem;
  font-weight: 800;
  color: var(--text-strong);
  margin: 0;
}

/* AI Sub Tabs Styles */
.group-ai-tabs {
  display: flex;
  background: rgba(148, 163, 184, 0.08);
  padding: 4px;
  border-radius: var(--qaly-radius-lg);
  gap: 4px;
  border: 1px solid rgba(148, 163, 184, 0.12);
}

.group-ai-tabs button {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 6px;
  padding: 8px 6px;
  font-size: 0.72rem;
  font-weight: 800;
  border: none;
  background: transparent;
  color: var(--muted);
  border-radius: var(--qaly-radius-lg);
  cursor: pointer;
  transition: all 0.25s ease;
}

.group-ai-tabs button:hover {
  background: rgba(255, 255, 255, 0.4);
  color: var(--text-strong);
}

.group-ai-tabs button.active {
  background: #ffffff;
  color: var(--primary);
  box-shadow: var(--qaly-shadow-md);
}

/* Content Area */
.group-ai-panel__content {
  overflow-y: auto;
  min-height: 0;
  display: flex;
  flex-direction: column;
}

.tab-view-container {
  display: flex;
  flex-direction: column;
  gap: 14px;
  height: 100%;
}

.action-trigger-box {
  background: rgba(255, 255, 255, 0.65);
  border: 1px solid rgba(148, 163, 184, 0.16);
  border-radius: var(--qaly-radius-lg);
  padding: 12px;
  display: flex;
  flex-direction: column;
  gap: 12px;
  box-shadow: var(--qaly-shadow-md);
}

.helper-text {
  font-size: 0.76rem;
  color: var(--muted);
  line-height: 1.45;
  margin: 0;
}

.native-summary-review {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.grounded-list {
  display: grid;
  gap: 10px;
  margin: 0;
  padding: 0;
  list-style: none;
}

.grounded-list li {
  display: flex;
  flex-wrap: wrap;
  gap: 6px 10px;
  padding: 10px;
  border: 1px solid rgba(148, 163, 184, 0.2);
  border-radius: var(--qaly-radius-md);
  background: rgba(255, 255, 255, 0.62);
}

.grounded-list li > span,
.grounded-list li > strong {
  flex-basis: 100%;
}

.grounded-list a {
  color: var(--primary);
  font-size: 0.72rem;
  font-weight: 700;
}

.ai-action-btn {
  width: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  padding: 10px 16px;
  font-size: 0.8rem;
  font-weight: 800;
  border-radius: var(--qaly-radius-lg);
}

/* Warnings Box */
.warnings-box {
  display: flex;
  align-items: flex-start;
  gap: 8px;
  border: 1px solid rgba(245, 158, 11, 0.24);
  border-radius: var(--qaly-radius-lg);
  background: rgba(255, 251, 235, 0.92);
  color: #b45309;
  padding: 10px 12px;
}

.warnings-list {
  display: flex;
  flex-direction: column;
  gap: 4px;
  font-size: 0.74rem;
  font-weight: 700;
}

/* AI Summarize Results */
.ai-result-content {
  display: flex;
  flex-direction: column;
  gap: 14px;
}

.result-section {
  background: rgba(255, 255, 255, 0.75);
  border: 1px solid rgba(148, 163, 184, 0.15);
  border-radius: var(--qaly-radius-lg);
  padding: 12px;
  box-shadow: var(--qaly-shadow-md);
}

.section-title {
  margin: 0 0 8px 0;
  font-size: 0.82rem;
  font-weight: 800;
  display: flex;
  align-items: center;
  gap: 6px;
  color: var(--text-strong);
}

.section-title.success-color {
  color: #15803d;
}

.section-title.warning-color {
  color: #b45309;
}

.summary-body-text {
  font-size: 0.78rem;
  line-height: 1.55;
  color: #334155;
  white-space: pre-line;
}

.bullet-list {
  margin: 0;
  padding-left: 18px;
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.bullet-list li {
  font-size: 0.78rem;
  line-height: 1.45;
  color: #475569;
}

.empty-bullet-text {
  font-size: 0.74rem;
  color: var(--muted);
  font-style: italic;
  padding-left: 4px;
}

/* AI Draft Project & Edit View */
.draft-edit-container {
  display: flex;
  flex-direction: column;
  gap: 14px;
}

.draft-project-info {
  background: rgba(255, 255, 255, 0.8);
  border: 1px solid rgba(148, 163, 184, 0.16);
  border-radius: var(--qaly-radius-lg);
  padding: 12px;
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.form-group {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.form-group label,
.instructions-input-group label {
  font-size: 0.7rem;
  font-weight: 800;
  text-transform: uppercase;
  color: var(--muted);
}

.premium-input {
  border: 1px solid rgba(148, 163, 184, 0.24);
  border-radius: var(--qaly-radius-lg);
  padding: 8px 10px;
  font-size: 0.82rem;
  color: var(--text-strong);
  background: #ffffff;
  transition: border-color 0.2s ease;
}

.premium-input:focus {
  border-color: var(--primary);
  outline: none;
}

.premium-textarea,
.ai-instructions-textarea {
  border: 1px solid rgba(148, 163, 184, 0.24);
  border-radius: var(--qaly-radius-lg);
  padding: 8px 10px;
  font-size: 0.78rem;
  line-height: 1.45;
  color: var(--text-strong);
  background: #ffffff;
  resize: vertical;
  transition: border-color 0.2s ease;
}

.premium-textarea:focus,
.ai-instructions-textarea:focus {
  border-color: var(--primary);
  outline: none;
}

.instructions-input-group {
  display: flex;
  flex-direction: column;
  gap: 5px;
}

.draft-tasks-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-top: 4px;
}

.draft-tasks-header h4 {
  margin: 0;
  font-size: 0.82rem;
  font-weight: 800;
  color: var(--text-strong);
}

.draft-tasks-list {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.draft-task-card {
  background: var(--panel, #ffffff);
  border: 1px solid var(--line, rgba(193, 211, 232, 0.72));
  border-radius: var(--qaly-radius-lg);
  padding: 12px;
  display: flex;
  flex-direction: column;
  gap: 8px;
  transition: all 0.25s ease;
}

.draft-task-card--unchecked {
  opacity: 0.55;
  border-color: var(--line, rgba(148, 163, 184, 0.2));
  background: var(--bg-soft, rgba(248, 250, 252, 0.6));
}

.draft-task-card__header {
  display: flex;
  align-items: center;
  gap: 8px;
}

.custom-checkbox {
  position: relative;
  display: inline-block;
  width: 18px;
  height: 18px;
  cursor: pointer;
}

.custom-checkbox input {
  position: absolute;
  opacity: 0;
  cursor: pointer;
  height: 0;
  width: 0;
}

.checkmark {
  position: absolute;
  top: 0;
  left: 0;
  height: 18px;
  width: 18px;
  background-color: var(--panel, #ffffff);
  border: 2px solid var(--line, rgba(148, 163, 184, 0.4));
  border-radius: 4px;
  transition: all 0.2s ease;
}

.custom-checkbox input:checked ~ .checkmark {
  background-color: var(--primary);
  border-color: var(--primary);
}

.checkmark:after {
  content: "";
  position: absolute;
  display: none;
}

.custom-checkbox input:checked ~ .checkmark:after {
  display: block;
}

.custom-checkbox .checkmark:after {
  left: 5px;
  top: 1px;
  width: 4px;
  height: 9px;
  border: solid white;
  border-width: 0 2px 2px 0;
  transform: rotate(45deg);
}

.task-title-input {
  flex: 1;
  border: 1px solid transparent;
  background: transparent;
  font-size: 0.8rem;
  font-weight: 800;
  color: var(--text-strong);
  padding: 4px 6px;
  border-radius: 4px;
}

.task-title-input:focus {
  border-color: var(--primary, rgba(148, 163, 184, 0.2));
  background: var(--panel, #ffffff);
  outline: none;
}

.draft-task-card__body {
  display: flex;
  flex-direction: column;
  gap: 8px;
  padding-left: 26px;
}

.task-desc-textarea {
  border: 1px solid transparent;
  background: transparent;
  font-size: 0.74rem;
  color: var(--muted);
  line-height: 1.45;
  resize: vertical;
  padding: 4px 6px;
  border-radius: 4px;
}

.task-desc-textarea:focus {
  border-color: var(--primary, rgba(148, 163, 184, 0.2));
  background: var(--panel, #ffffff);
  outline: none;
}

.task-metadata-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 8px;
}

.task-metadata-grid > :last-child {
  grid-column: 1 / -1;
}

.metadata-col {
  display: flex;
  flex-direction: column;
  gap: 3px;
}

.metadata-col label {
  font-size: 0.64rem;
  font-weight: 800;
  text-transform: uppercase;
  color: var(--muted);
}

.metadata-select {
  border: 1px solid var(--line, rgba(148, 163, 184, 0.24));
  border-radius: 6px;
  padding: 4px 8px;
  font-size: 0.72rem;
  background: var(--panel, #ffffff);
  color: var(--text-strong);
}

.metadata-number-input {
  border: 1px solid var(--line, rgba(148, 163, 184, 0.24));
  border-radius: 6px;
  padding: 4px 8px;
  font-size: 0.72rem;
  background: var(--panel, #ffffff);
  color: var(--text-strong);
  width: 100%;
}

.draft-actions-footer {
  display: flex;
  flex-direction: column;
  gap: 10px;
  margin-top: 8px;
  padding-top: 12px;
  border-top: 1px dashed var(--line, rgba(148, 163, 184, 0.2));
}

.footer-confirm-btn {
  width: 100%;
  padding: 12px;
  font-size: 0.82rem;
  font-weight: 800;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  border-radius: var(--qaly-radius-lg);
}

/* Action Items Listing styling */
.action-items-list {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.group-ai-item {
  display: flex;
  flex-direction: column;
  gap: 6px;
  border: 1px solid var(--line, rgba(193, 211, 232, 0.72));
  border-radius: var(--qaly-radius-lg);
  background: var(--panel, #ffffff);
  padding: 12px;
  box-shadow: var(--qaly-shadow-md);
}

.group-ai-item__title {
  display: flex;
  align-items: center;
  gap: 6px;
  color: var(--text-strong);
  font-size: 0.78rem;
}

.group-ai-item__desc {
  margin: 0;
  color: var(--muted);
  line-height: 1.45;
  font-size: 0.76rem;
}

.group-ai-item__meta {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  margin-top: 2px;
}

.meta-tag {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  border-radius: 6px;
  background: color-mix(in srgb, var(--primary) 12%, var(--panel));
  color: var(--primary);
  padding: 4px 6px;
  font-size: 0.68rem;
  font-weight: 800;
}

.confidence-tag {
  background: var(--bg-soft);
  color: var(--muted);
}

.group-ai-item__source {
  font-size: 0.68rem;
  color: var(--muted, #64748b);
  border-left: 2px solid var(--line, rgba(148, 163, 184, 0.2));
  padding-left: 6px;
  margin-top: 4px;
  line-height: 1.4;
}

/* Empty States Placeholder */
.panel-empty-placeholder {
  flex: 1;
  min-height: 240px;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 12px;
  color: var(--muted);
  font-weight: 800;
  font-size: 0.76rem;
  text-align: center;
  padding: 20px;
  border: 2px dashed var(--line, rgba(148, 163, 184, 0.16));
  border-radius: var(--qaly-radius-lg);
  background: var(--bg-soft, rgba(255, 255, 255, 0.15));
}

.muted-icon {
  color: rgba(148, 163, 184, 0.45);
}

.spin-icon {
  animation: spin-kf 0.9s linear infinite;
}

.selected-ai-source {
  display: grid;
  gap: 8px;
  padding: 10px 12px;
  border-bottom: 1px solid var(--line, rgba(148, 163, 184, 0.22));
  background: color-mix(in srgb, var(--primary) 10%, var(--panel));
}

.selected-ai-source strong {
  color: var(--text-strong);
  font-size: 0.78rem;
}

.selected-ai-source select {
  min-width: 0;
  padding: 8px;
  border: 1px solid var(--line, #bfdbfe);
  border-radius: 6px;
  background: var(--panel, #fff);
  color: var(--text-strong);
}

.native-task-review {
  display: grid;
  gap: 12px;
  padding: 14px;
  border: 1px solid color-mix(in srgb, var(--primary) 28%, var(--line));
  border-radius: var(--qaly-radius-lg);
  background: var(--panel);
}

.native-task-review__header,
.native-task-review__header > div,
.native-process > div,
.native-receipt > div {
  display: grid;
  gap: 4px;
}

.native-task-review__header {
  grid-template-columns: minmax(0, 1fr) auto;
  align-items: start;
}

.native-task-review__header h3,
.native-task-review__header p {
  margin: 0;
}

.native-task-review__header h3 {
  color: var(--text-strong);
  font-size: 1rem;
}

.native-task-review__header p {
  color: var(--muted);
  font-size: .72rem;
  line-height: 1.45;
}

.native-eyebrow {
  color: var(--primary);
  font-size: .64rem;
  font-weight: 900;
  letter-spacing: .06em;
  text-transform: uppercase;
}

.native-progress {
  padding: 5px 8px;
  border-radius: 999px;
  background: color-mix(in srgb, var(--primary) 12%, transparent);
  color: var(--primary);
  font-size: .7rem;
  font-weight: 900;
}

.native-process,
.native-truth-strip,
.native-review-actions,
.native-receipt {
  display: flex;
  align-items: center;
  gap: 9px;
}

.native-process {
  padding: 10px;
  border-radius: 10px;
  background: var(--bg-soft);
  color: var(--text-strong);
}

.native-process > div {
  flex: 1;
}

.native-process span,
.native-error small {
  color: var(--muted);
  font-size: .66rem;
}

.native-error > div {
  flex: 1;
  display: grid;
  gap: 2px;
}

.native-truth-strip {
  flex-wrap: wrap;
  padding: 7px 9px;
  border-radius: 9px;
  background: var(--bg-soft);
  color: var(--muted);
  font-size: .66rem;
}

.native-danger {
  color: #dc2626;
  font-weight: 900;
}

.native-empty {
  min-height: 150px;
}

.native-task-list {
  display: grid;
  gap: 9px;
}

.native-task-card {
  display: grid;
  gap: 9px;
  padding: 11px;
  border: 1px solid var(--line);
  border-radius: 11px;
  background: var(--panel);
}

.native-task-card--unchecked {
  opacity: .56;
}

.native-task-card__top {
  display: grid;
  grid-template-columns: auto minmax(0, 1fr) auto;
  align-items: center;
  gap: 8px;
}

.native-task-fields {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 7px;
}

.native-task-fields label {
  display: grid;
  gap: 4px;
  color: var(--muted);
  font-size: .65rem;
  font-weight: 800;
}

.native-task-fields input,
.native-task-fields select {
  min-width: 0;
  min-height: 34px;
  border: 1px solid var(--line);
  border-radius: 7px;
  background: var(--panel);
  color: var(--text-strong);
  padding: 6px;
}

.native-sources {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 5px;
  color: var(--muted);
  font-size: .65rem;
}

.native-sources a {
  padding: 4px 6px;
  border-radius: 6px;
  background: color-mix(in srgb, var(--primary) 10%, transparent);
  color: var(--primary);
  text-decoration: none;
}

.native-review-actions {
  justify-content: flex-end;
  flex-wrap: wrap;
  padding-top: 10px;
  border-top: 1px solid var(--line);
}

.native-receipt {
  width: 100%;
  justify-content: flex-start;
  color: #047857;
}

.native-receipt a {
  color: #047857;
  font-size: .68rem;
}

@media (max-width: 720px) {
  .native-task-fields {
    grid-template-columns: 1fr;
  }
}

@keyframes spin-kf {
  to {
    transform: rotate(360deg);
  }
}

.font-bold {
  font-weight: 800;
}

.text-red-500 {
  color: #ef4444 !important;
}

.w-full {
  width: 100%;
}

.text-center {
  text-align: center;
}

@media (max-width: 980px) {
  .group-ai-panel {
    min-height: auto;
  }
}
</style>
