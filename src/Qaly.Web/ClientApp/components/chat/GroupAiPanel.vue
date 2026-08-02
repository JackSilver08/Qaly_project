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

async function loadLinkedProjects() {
  linkedProjects.value = await apiResult<GroupLinkedProjectDto[]>(`/api/groups/${props.groupId}/linked-projects`)
  if (!linkedProjects.value.some(project => project.projectId === selectedProjectId.value)) {
    selectedProjectId.value = linkedProjects.value.length === 1 ? linkedProjects.value[0].projectId : ''
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
      : '/api/ai/task-drafts/from-source'
    const result = await apiResult<AiJobCreatedDto>(endpoint, {
      method: 'POST',
      headers: { 'Idempotency-Key': crypto.randomUUID() },
      body: JSON.stringify({
        projectId: selectedProjectId.value,
        sourceType: selectedAction.value === 'summary' ? 'group' : 'message',
        sourceEntityId: selectedAction.value === 'summary' ? props.groupId : selectedMessageIds.value[0],
        sources
      })
    })
    showSuccess(selectedAction.value === 'summary'
      ? 'Đã tạo job tóm tắt từ tin nhắn đã chọn.'
      : 'Đã tạo task draft từ tin nhắn đã chọn.')
    await router.replace({
      query: { ...router.currentRoute.value.query, aiActivity: '1', aiTab: 'jobs', aiJob: result.jobId }
    })
  } catch (error) {
    showError(errorMessage(error, 'Không thể gửi các tin nhắn đã chọn cho AI.'))
  } finally {
    isSelectedRequestLoading.value = false
  }
}

watch(
  () => props.selectedRequest?.nonce,
  async nonce => {
    if (!nonce || !props.selectedRequest) return
    selectedAction.value = props.selectedRequest.action
    selectedMessageIds.value = [...props.selectedRequest.messageIds]
    subTab.value = props.selectedRequest.action === 'summary' ? 'summary' : 'draft'
    try {
      await loadLinkedProjects()
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
watch(() => props.groupId, () => {
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
})

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
        
        <div v-if="!hasGeneratedDraft" class="action-trigger-box">
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

        <div v-else-if="!isDraftLoading" class="panel-empty-placeholder">
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
  background: rgba(255, 255, 255, 0.8);
  border: 1px solid rgba(193, 211, 232, 0.72);
  border-radius: var(--qaly-radius-lg);
  padding: 12px;
  display: flex;
  flex-direction: column;
  gap: 8px;
  transition: all 0.25s ease;
}

.draft-task-card--unchecked {
  opacity: 0.55;
  border-color: rgba(148, 163, 184, 0.2);
  background: rgba(248, 250, 252, 0.6);
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
  background-color: #ffffff;
  border: 2px solid rgba(148, 163, 184, 0.4);
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
  border-color: rgba(148, 163, 184, 0.2);
  background: #ffffff;
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
  border-color: rgba(148, 163, 184, 0.2);
  background: #ffffff;
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
  border: 1px solid rgba(148, 163, 184, 0.24);
  border-radius: 6px;
  padding: 4px 8px;
  font-size: 0.72rem;
  background: #ffffff;
  color: var(--text-strong);
}

.metadata-number-input {
  border: 1px solid rgba(148, 163, 184, 0.24);
  border-radius: 6px;
  padding: 4px 8px;
  font-size: 0.72rem;
  background: #ffffff;
  color: var(--text-strong);
  width: 100%;
}

.draft-actions-footer {
  display: flex;
  flex-direction: column;
  gap: 10px;
  margin-top: 8px;
  padding-top: 12px;
  border-top: 1px dashed rgba(148, 163, 184, 0.2);
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
  border: 1px solid rgba(193, 211, 232, 0.72);
  border-radius: var(--qaly-radius-lg);
  background: rgba(255, 255, 255, 0.82);
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
  background: rgba(234, 244, 255, 0.86);
  color: var(--primary);
  padding: 4px 6px;
  font-size: 0.68rem;
  font-weight: 800;
}

.confidence-tag {
  background: rgba(241, 245, 249, 0.86);
  color: #475569;
}

.group-ai-item__source {
  font-size: 0.68rem;
  color: #64748b;
  border-left: 2px solid rgba(148, 163, 184, 0.2);
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
  border: 2px dashed rgba(148, 163, 184, 0.16);
  border-radius: var(--qaly-radius-lg);
  background: rgba(255, 255, 255, 0.15);
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
  border-bottom: 1px solid rgba(148, 163, 184, 0.22);
  background: rgba(239, 246, 255, 0.9);
}

.selected-ai-source strong {
  color: #1e3a8a;
  font-size: 0.78rem;
}

.selected-ai-source select {
  min-width: 0;
  padding: 8px;
  border: 1px solid #bfdbfe;
  border-radius: 6px;
  background: #fff;
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
