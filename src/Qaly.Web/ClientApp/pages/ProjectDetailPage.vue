<script setup lang="ts">
import { MessageSquare, MoreHorizontal, Plus, Send, Search, Clock, Play, Square, Calendar, X, ClipboardList, FileUp, File, Check, Ban, CheckCircle2, CheckSquare, LayoutGrid, List, Lock } from 'lucide-vue-next'
// @ts-ignore
import { VueDraggable } from '../utils/vendor/vue-draggable-plus.js'
import ProjectDetailHeader from '../components/ProjectDetailHeader.vue'
import ProjectActivityTab from '../components/ProjectActivityTab.vue'
import ProjectMembersTab from '../components/ProjectMembersTab.vue'
import ProjectStatsTab from '../components/ProjectStatsTab.vue'
import ProjectWorkloadTab from '../components/ProjectWorkloadTab.vue'
import ProjectWikiTab from '../components/ProjectWikiTab.vue'
import ProjectGanttTab from '../components/ProjectGanttTab.vue'
import WebhooksTab from '../components/WebhooksTab.vue'
import ImportModal from '../components/import/ImportModal.vue'
import ImportUndoBanner from '../components/import/ImportUndoBanner.vue'
import { useDashboardContext } from '../composables/dashboard-context'
import { computed, ref, onMounted, onUnmounted, watch } from 'vue'
import { useRouter } from 'vue-router'
import { apiResult } from '../utils/api-client'
import type { DashboardTask, KanbanMoveResultDto, TaskAssignmentInsightDto } from '../types'
import { showError } from '../composables/use-toast'
import MarkdownIt from 'markdown-it'
import DOMPurify from 'dompurify'

const {
  activeProjectTab,
  activeTaskMenu,
  addManualTimeEntry,
  addMember,
  attachments,
  beginEditTask,
  closeProjectDetails,
  comments,
  createTask,
  createTaskOpen,
  currentUser,
  deleteAttachment,
  deleteComment,
  deleteTask,
  displayStatus,
  formatDate,
  formatFileSize,
  formatTime,
  isProjectAdmin,
  isTaskOverdue,
  moveTaskOnKanban,
  newComment,
  newTaskAssigneeId,
  newTaskDescription,
  newTaskDueDate,
  newTaskIsPrivate,
  newTaskIsPinned,
  newTaskContributesToProgress,
  newTaskPriority,
  newTaskTitle,
  nextStatuses,
  openChatWithPrompt,
  priorities,
  quickEditTaskTitle,
  removeMember,
  selectTaskInProject,
  selectedProject,
  selectedProjectMembers,
  selectedProjectStats,
  selectedTask,
  selectedTaskId,
  statusTone,
  statusColumns,
  submitComment,
  tabs,
  tasksByStatus,
  toggleTaskMenu,
  updateMemberRole,
  updateMemberPermissions,
  uploadAttachment,
  markAsEvidence,
  reviewEvidence,
  users,
  taskSearchQuery,
  taskBeingQuickEditedId,
  timeEntries,
  activeTimer,
  startTimer,
  stopTimer,
  loadDashboard,
} = useDashboardContext()

const router = useRouter()
const showImportModal = ref(false)
const undoBannerData = ref<{ importSessionId: string; importedCount: number; failedCount: number; duplicateSkippedCount: number; createdAt: string } | null>(null)
const assignmentInsight = ref<TaskAssignmentInsightDto | null>(null)
const assignmentInsightLoading = ref(false)
const assignmentInsightError = ref('')
watch(
  () => selectedTask.value?.id,
  () => {
    assignmentInsight.value = null
    assignmentInsightError.value = ''
  },
)

function onImported(result: any) {
  showImportModal.value = false
  if (result?.importSessionId) {
    undoBannerData.value = {
      importSessionId: result.importSessionId,
      importedCount: result.importedCount,
      failedCount: result.failedCount ?? 0,
      duplicateSkippedCount: result.duplicateSkippedCount ?? 0,
      createdAt: new Date().toISOString(),
    }
  }
  loadDashboard()
}

async function handleUndoFromBanner() {
  if (!undoBannerData.value) return
  try {
    const res = await fetch(`/api/import/sessions/${undoBannerData.value.importSessionId}`, { method: 'DELETE' })
    const data = await res.json()
    if (data.isSuccess) {
      undoBannerData.value = null
      loadDashboard()
    }
  } catch { /* ignore */ }
}

function rejectEvidence(attachmentId: string) {
  const note = window.prompt('Lý do từ chối?')
  if (note !== null) {
    reviewEvidence(attachmentId, false, note)
  }
}

const quickEditTitle = ref('')
const manualMinutes = ref<number>(0)
const manualNote = ref('')
const showManualForm = ref(false)
const isKanbanDragging = ref(false)
const isSuppressingTaskClick = ref(false)
const draggingTask = ref<DashboardTask | null>(null)
const taskBoardView = ref<'kanban' | 'list'>('kanban')
const markdown = new MarkdownIt({ linkify: true, breaks: true })

const filteredTaskList = computed(() => statusColumns.flatMap((status: string) => tasksByStatus(status)))
const kanbanTasksByStatus = computed<Record<string, DashboardTask[]>>(() =>
  Object.fromEntries(
    statusColumns.map((status: string) => [status, tasksByStatus(status)]),
  ),
)
const canShowCapacityTab = computed(() => activeProjectTab.value === 'capacity' && !!selectedProject.value)
const canShowActivityTab = computed(() => activeProjectTab.value === 'activity' && !!selectedProject.value)
const canShowGanttTab = computed(() => activeProjectTab.value === 'gantt' && !!selectedProject.value)
const canShowWebhooksTab = computed(() => activeProjectTab.value === 'webhooks' && !!selectedProject.value)
const canShowImportModal = computed(() => showImportModal.value && !!selectedProject.value)
const canShowTaskComments = computed(() => !!selectedTask.value && !selectedTask.value.isRestricted)

function canManageTask(task: DashboardTask) {
  return isProjectAdmin.value && !task.isRestricted
}

function canReviewEvidence(attachment: any) {
  return isProjectAdmin.value && attachment.evidenceApprovalStatus !== 'Approved'
}

function handleTaskTitleDoubleClick(task: DashboardTask) {
  if (task.isRestricted) return
  startQuickEdit(task)
}

function renderMarkdown(value: string) {
  return DOMPurify.sanitize(markdown.render(value || ''))
}

function startQuickEdit(task: DashboardTask) {
  taskBeingQuickEditedId.value = task.id
  quickEditTitle.value = task.title
}

function closeTaskDetails() {
  selectedTaskId.value = null
  showManualForm.value = false
  activeTaskMenu.value = null
  if (selectedProject.value?.id) {
    void router.replace(`/projects/${selectedProject.value.id}`)
  }
}

function handleTaskCardClick(taskId: string) {
  if (isSuppressingTaskClick.value) return
  selectTaskInProject(taskId)
}

async function saveQuickEdit() {
  if (!taskBeingQuickEditedId.value) return
  const saved = await quickEditTaskTitle(taskBeingQuickEditedId.value, quickEditTitle.value)
  if (saved) taskBeingQuickEditedId.value = null
}

async function submitManualEntry() {
  if (!selectedTask.value || manualMinutes.value <= 0) return
  const saved = await addManualTimeEntry(selectedTask.value.id, manualMinutes.value, manualNote.value)
  if (saved) {
    manualMinutes.value = 0; manualNote.value = ''; showManualForm.value = false
  }
}

async function loadAssignmentInsight() {
  if (!selectedProject.value || !selectedTask.value) return
  assignmentInsightLoading.value = true
  assignmentInsightError.value = ''
  try {
    assignmentInsight.value = await apiResult<TaskAssignmentInsightDto>(`/api/ai/tasks/${selectedTask.value.id}/assignment-insight?projectId=${selectedProject.value.id}`)
  } catch (error) {
    assignmentInsightError.value = 'Không thể tải gợi ý assignee.'
  } finally {
    assignmentInsightLoading.value = false
  }
}

function kanbanStatusFromElement(element: HTMLElement | null | undefined) {
  return element?.dataset.kanbanStatus
    ?? element?.closest<HTMLElement>('[data-kanban-status]')?.dataset.kanbanStatus
    ?? null
}

function moveTargetIndex(evt: { newDraggableIndex?: number; newIndex?: number }, targetCount: number) {
  const rawIndex = Number.isInteger(evt.newDraggableIndex) ? evt.newDraggableIndex : evt.newIndex
  return Math.max(0, Math.min(rawIndex ?? targetCount, targetCount))
}

function dragDropState(status: string) {
  const task = draggingTask.value
  if (!isKanbanDragging.value || !task) return 'idle'
  if (task.status === status) return 'source'
  return nextStatuses(task.status).includes(status) ? 'allowed' : 'blocked'
}

function kanbanColumnClass(status: string) {
  const state = dragDropState(status)
  return {
    'is-drop-source': state === 'source',
    'is-drop-allowed': state === 'allowed',
    'is-drop-blocked': state === 'blocked',
  }
}

function isKanbanDropBlocked(status: string) {
  return dragDropState(status) === 'blocked'
}

function canDropKanbanTask(task: DashboardTask, status: string) {
  return task.status === status || nextStatuses(task.status).includes(status)
}

function onDragStart(evt?: { item?: HTMLElement; data?: DashboardTask }) {
  isKanbanDragging.value = true
  isSuppressingTaskClick.value = true
  const taskId = evt?.item?.dataset.id ?? evt?.data?.id
  draggingTask.value = selectedProject.value?.tasks.find((task: DashboardTask) => task.id === taskId) ?? evt?.data ?? null
  selectedTaskId.value = null
  showManualForm.value = false
  activeTaskMenu.value = null
}

function canSelectListStatus(task: DashboardTask, status: string) {
  return task.status === status || nextStatuses(task.status).includes(status)
}

function applyMovedTaskToCurrentProject(task: DashboardTask, status: string, result: KanbanMoveResultDto) {
  const project = selectedProject.value
  if (!project) return

  const boardTasks = result.board?.columns?.flatMap((column) => column.tasks) ?? []
  if (boardTasks.length) {
    const boardTaskIds = new Set(boardTasks.map((item) => item.id))
    project.tasks = [
      ...boardTasks,
      ...project.tasks.filter((item: DashboardTask) => !boardTaskIds.has(item.id)),
    ]
    return
  }

  const currentTask = project.tasks.find((item: DashboardTask) => item.id === task.id)
  if (!currentTask) return

  const movedTask = result.task
  currentTask.status = movedTask.status || status
  currentTask.priority = movedTask.priority ?? currentTask.priority
  currentTask.dueDate = movedTask.dueDate ?? currentTask.dueDate
  currentTask.assigneeId = movedTask.assigneeId ?? currentTask.assigneeId
  currentTask.assigneeName = movedTask.assigneeName ?? currentTask.assigneeName
  currentTask.sortOrder = movedTask.sortOrder ?? currentTask.sortOrder
  currentTask.rowVersion = movedTask.rowVersion ?? currentTask.rowVersion
  currentTask.isPrivate = movedTask.isPrivate ?? currentTask.isPrivate
  currentTask.isRestricted = movedTask.isRestricted ?? currentTask.isRestricted
  currentTask.isPinned = movedTask.isPinned ?? currentTask.isPinned
  currentTask.contributesToProgress = movedTask.contributesToProgress ?? currentTask.contributesToProgress
  currentTask.upvoteCount = movedTask.upvoteCount ?? currentTask.upvoteCount
  currentTask.downvoteCount = movedTask.downvoteCount ?? currentTask.downvoteCount
  currentTask.commentCount = movedTask.commentCount ?? currentTask.commentCount
  currentTask.attachmentCount = movedTask.attachmentCount ?? currentTask.attachmentCount
}

async function handleListStatusChange(task: DashboardTask, event: Event) {
  const select = event.target as HTMLSelectElement
  const status = select.value
  if (!selectedProject.value || task.status === status || !canSelectListStatus(task, status)) {
    select.value = task.status
    return
  }
  const targetTasks = tasksByStatus(status).filter((item: DashboardTask) => item.id !== task.id)
  const afterTaskId = targetTasks[targetTasks.length - 1]?.id ?? null
  const result = await moveTaskOnKanban(selectedProject.value.id, task, status, null, afterTaskId)
  if (!result) {
    select.value = task.status
    return
  }
  applyMovedTaskToCurrentProject(task, status, result)
  closeTaskDetails()
}

const onDragEnd = async (evt: {
  item: HTMLElement
  to: HTMLElement
  from: HTMLElement
  oldIndex?: number
  newIndex?: number
  oldDraggableIndex?: number
  newDraggableIndex?: number
}) => {
  isKanbanDragging.value = false
  draggingTask.value = null
  window.setTimeout(() => {
    isSuppressingTaskClick.value = false
  }, 160)
  const taskId = evt.item.dataset.id
  const newStatus = kanbanStatusFromElement(evt.to)
  const project = selectedProject.value
  if (!taskId || !newStatus || !project || !statusColumns.includes(newStatus)) {
    await loadDashboard()
    showError('Không thể xác định cột đích khi kéo thả nhiệm vụ.')
    return
  }

  const task = project.tasks.find((t: DashboardTask) => t.id === taskId)
  if (!task) {
    await loadDashboard()
    showError('Không tìm thấy nhiệm vụ vừa kéo thả.')
    return
  }

  const sameColumn = evt.to === evt.from && task.status === newStatus
  if (sameColumn && evt.oldIndex === evt.newIndex) return
  if (!canDropKanbanTask(task, newStatus)) {
    await loadDashboard()
    showError(`Không thể chuyển nhiệm vụ từ ${displayStatus(task.status)} sang ${displayStatus(newStatus)}.`)
    return
  }

  const targetTasks = tasksByStatus(newStatus).filter((item: DashboardTask) => item.id !== taskId)
  const targetIndex = moveTargetIndex(evt, targetTasks.length)
  const beforeTaskId = targetTasks[targetIndex]?.id ?? null
  const afterTaskId = beforeTaskId ? null : targetTasks[targetIndex - 1]?.id ?? null

  const result = await moveTaskOnKanban(project.id, task, newStatus, beforeTaskId, afterTaskId)
  if (result) {
    applyMovedTaskToCurrentProject(task, newStatus, result)
    closeTaskDetails()
  }
}

// Keyboard Shortcuts
const handleKeyDown = (e: KeyboardEvent) => {
  if (e.target instanceof HTMLInputElement || e.target instanceof HTMLTextAreaElement) return
  
  if (e.key.toLowerCase() === 'n') {
    e.preventDefault()
    createTaskOpen.value = true
  }
  if (e.key === 'Escape') {
    createTaskOpen.value = false
    showManualForm.value = false
    activeTaskMenu.value = null
    closeTaskDetails()
  }
}

onMounted(() => window.addEventListener('keydown', handleKeyDown))
onUnmounted(() => window.removeEventListener('keydown', handleKeyDown))
</script>

<template>
  <div class="dashboard-scroll dashboard-scroll--embedded no-scrollbar">
    <div class="dashboard-main project-home-main no-scrollbar">
      <ProjectDetailHeader
        v-if="selectedProject"
        :project-name="selectedProject.name"
        :description="selectedProject.description"
        :status-label="displayStatus(selectedProject.status)"
        :status-tone="statusTone(selectedProject.status)"
        :progress-label="`${selectedProject.completedTaskCount}/${selectedProject.taskCount} task hoàn thành`"
        :progress-percentage="selectedProject.progressPercentage"
        @back="closeProjectDetails"
        @assistant="openChatWithPrompt()"
      />

      <nav class="project-tabs glass-card">
        <button
          v-for="tab in tabs"
          :key="tab.id"
          type="button"
          class="tab-link"
          :class="{ 'is-active': activeProjectTab === tab.id }"
          @click="activeProjectTab = tab.id"
        >
          {{ tab.label }}
        </button>
      </nav>

      <div v-if="activeProjectTab === 'stats'" class="tab-pane reveal">
        <ProjectStatsTab :stats="selectedProjectStats" />
      </div>

      <div v-if="canShowCapacityTab" class="tab-pane reveal">
        <ProjectWorkloadTab :project-id="selectedProject.id" />
      </div>

      <div v-if="activeProjectTab === 'tasks'">
        <section id="tasks" class="task-board-shell glass-card">
          <div class="panel-heading task-board-heading">
            <div class="task-board-heading__title">
              <span>Nhiệm vụ</span>
              <h2>Bảng công việc</h2>
            </div>
            <div class="task-board-search">
              <div class="search-box task-search-box">
                <Search :size="16" />
                <input v-model="taskSearchQuery" type="text" placeholder="Tìm nhiệm vụ (N: mới)..." />
              </div>
            </div>
            <div class="board-actions">
              <div class="task-view-toggle" aria-label="Chế độ hiển thị nhiệm vụ">
                <button
                  type="button"
                  class="task-view-toggle__button"
                  :class="{ 'is-active': taskBoardView === 'kanban' }"
                  @click="taskBoardView = 'kanban'"
                >
                  <LayoutGrid :size="15" />
                  <span>Kanban</span>
                </button>
                <button
                  type="button"
                  class="task-view-toggle__button"
                  :class="{ 'is-active': taskBoardView === 'list' }"
                  @click="taskBoardView = 'list'"
                >
                  <List :size="15" />
                  <span>List</span>
                </button>
              </div>
              <button class="primary-button primary-button--compact" type="button" @click="createTaskOpen = !createTaskOpen">
                <Plus :size="16" />
                <span>Nhiệm vụ</span>
              </button>
              <button class="import-btn-sm" type="button" @click="showImportModal = true">
                <FileUp :size="14" /> Nhập file
              </button>
            </div>
          </div>

          <Teleport to="body">
            <div v-if="createTaskOpen" class="task-modal-backdrop" @click.self="createTaskOpen = false">
              <div class="task-modal">
                <div class="task-modal-header">
                  <div class="task-modal-title">
                    <CheckSquare :size="20" />
                    <h2>Tạo nhiệm vụ mới</h2>
                  </div>
                  <button type="button" class="icon-button" @click="createTaskOpen = false">
                    <X :size="18" />
                  </button>
                </div>
                
                <form class="task-modal-body" @submit.prevent="createTask">
                  <div class="form-group">
                    <label>Tiêu đề nhiệm vụ</label>
                    <input v-model="newTaskTitle" type="text" placeholder="Nhập tiêu đề nhiệm vụ..." required class="modal-input" />
                  </div>
                  
                  <div class="form-group">
                    <label>Mô tả chi tiết</label>
                    <textarea v-model="newTaskDescription" placeholder="Mô tả nhiệm vụ (không bắt buộc)..." rows="3" class="modal-input"></textarea>
                  </div>
                  
                  <div class="modal-grid-2">
                    <div class="form-group">
                      <label>Độ ưu tiên</label>
                      <select v-model="newTaskPriority" class="modal-input">
                        <option v-for="priority in priorities" :key="priority" :value="priority">{{ priority }}</option>
                      </select>
                    </div>
                    <div class="form-group">
                      <label>Người thực hiện</label>
                      <select v-model="newTaskAssigneeId" class="modal-input">
                        <option value="">Chưa giao</option>
                        <option v-for="user in selectedProjectMembers" :key="user.id" :value="user.id">{{ user.fullName }}</option>
                      </select>
                    </div>
                  </div>
                  
                  <div class="form-group">
                    <label>Hạn chót</label>
                    <input v-model="newTaskDueDate" type="date" class="modal-input" />
                  </div>
                  
                  <div class="modal-options-row">
                    <label class="modal-checkbox">
                      <input v-model="newTaskIsPrivate" type="checkbox" />
                      <span>Riêng tư</span>
                    </label>
                    <label class="modal-checkbox">
                      <input v-model="newTaskContributesToProgress" type="checkbox" />
                      <span>Tính tiến độ</span>
                    </label>
                    <label v-if="isProjectAdmin" class="modal-checkbox">
                      <input v-model="newTaskIsPinned" type="checkbox" />
                      <span>Ghim</span>
                    </label>
                  </div>
                  
                  <div class="task-modal-actions">
                    <button class="btn btn--ghost" type="button" @click="createTaskOpen = false">Hủy</button>
                    <button class="btn btn--primary" type="submit" :disabled="!newTaskTitle.trim()">Tạo nhiệm vụ</button>
                  </div>
                </form>
              </div>
            </div>
          </Teleport>

          <div v-if="taskBoardView === 'kanban'" class="kanban-board" :class="{ 'is-dragging-task': isKanbanDragging }">
            <section
              v-for="status in statusColumns"
              :key="status"
              class="kanban-column"
              :class="kanbanColumnClass(status)"
              :data-kanban-status="status"
            >
              <div class="kanban-column__header">
                <strong>{{ displayStatus(status) }}</strong>
                <div class="kanban-column__badges">
                  <span v-if="isKanbanDropBlocked(status)" class="drop-lock-badge" :title="`Không thể chuyển sang ${displayStatus(status)}`">
                    <Lock :size="13" />
                  </span>
                  <span class="count-badge">{{ kanbanTasksByStatus[status].length }}</span>
                </div>
              </div>

              <VueDraggable
                :model-value="kanbanTasksByStatus[status]"
                :animation="200"
                draggable=".kanban-card"
                group="tasks"
                ghost-class="ghost-card"
                drag-class="dragging-card"
                class="kanban-column__list"
                :class="{ 'is-drop-ready': isKanbanDragging, ...kanbanColumnClass(status) }"
                :data-kanban-status="status"
                :empty-insert-threshold="120"
                @start="onDragStart"
                @end="onDragEnd"
              >
                <article
                  v-for="task in kanbanTasksByStatus[status]"
                  :key="task.id"
                  class="kanban-card draggable-item"
                  :class="{ 'is-selected': selectedTask?.id === task.id }"
                  :data-id="task.id"
                  @click="handleTaskCardClick(task.id)"
                >
                  <div class="kanban-card__top">
                    <input
                      v-if="taskBeingQuickEditedId === task.id"
                      v-model="quickEditTitle"
                      type="text"
                      class="quick-edit-input"
                      @blur="saveQuickEdit"
                      @keyup.enter="saveQuickEdit"
                      @click.stop
                    />
                    <strong v-else @dblclick.stop="handleTaskTitleDoubleClick(task)">
                      <span v-if="task.isPrivate" title="Nhiệm vụ riêng tư">Khóa</span>
                      <span v-if="task.isPinned" title="Nhiệm vụ đã ghim">Ghim</span>
                      {{ task.title }}
                    </strong>
                    
                    <div class="task-card-actions">
                      <span :class="`priority priority--${task.priority.toLowerCase()}`">{{ task.priority }}</span>

                      <div v-if="canManageTask(task)" class="task-menu-dropdown">
                        <button class="icon-button icon-button--small" type="button" @click.stop="toggleTaskMenu(task.id)">
                          <MoreHorizontal :size="14" />
                        </button>
                        <div v-if="activeTaskMenu === task.id" class="dropdown-content glass-card">
                          <button type="button" @click.stop="beginEditTask(task)">Sửa</button>
                          <button type="button" style="color: var(--peach-500)" @click.stop="deleteTask(task.id)">Xóa</button>
                        </div>
                      </div>
                    </div>
                  </div>
                  <p class="assignee-text">{{ task.isRestricted ? 'Bị giới hạn quyền xem' : (task.assigneeName || 'Chưa giao') }} • {{ formatDate(task.dueDate) }}</p>
                  <div class="kanban-card__meta">
                    <span class="meta-item"><MessageSquare :size="12" /> {{ task.commentCount }}</span>
                    <span class="meta-item">▲ {{ task.upvoteCount || 0 }}</span>
                    <span v-if="isTaskOverdue(task)" class="overdue-tag">Quá hạn</span>
                  </div>
                </article>
                <div v-if="kanbanTasksByStatus[status].length === 0" class="empty-column-placeholder">Thả nhiệm vụ vào đây</div>
              </VueDraggable>
            </section>
          </div>

          <div v-else class="task-list-board">
            <div class="task-list-board__head">
              <span>Nhiệm vụ</span>
              <span>Người phụ trách</span>
              <span>Hạn</span>
              <span>Ưu tiên</span>
              <span>Trạng thái</span>
              <span>Hoạt động</span>
            </div>

            <article
              v-for="task in filteredTaskList"
              :key="task.id"
              class="task-list-row"
              :class="{ 'is-selected': selectedTask?.id === task.id }"
              @click="handleTaskCardClick(task.id)"
            >
              <div class="task-list-row__main">
                <strong>
                  <span v-if="task.isPrivate" title="Nhiệm vụ riêng tư">Khóa</span>
                  <span v-if="task.isPinned" title="Nhiệm vụ đã ghim">Ghim</span>
                  {{ task.title }}
                </strong>
                <small>#{{ task.id.slice(0, 4) }}</small>
              </div>
              <span class="task-list-row__muted">{{ task.isRestricted ? 'Bị giới hạn quyền xem' : (task.assigneeName || 'Chưa giao') }}</span>
              <span class="task-list-row__muted">{{ formatDate(task.dueDate) }}</span>
              <span :class="`priority priority--${task.priority.toLowerCase()}`">{{ task.priority }}</span>
              <label class="task-status-select-wrap" @click.stop>
                <select
                  class="task-status-select"
                  :value="task.status"
                  @change.stop="handleListStatusChange(task, $event)"
                >
                  <option
                    v-for="status in statusColumns"
                    :key="status"
                    :value="status"
                    :disabled="!canSelectListStatus(task, status)"
                  >
                    {{ displayStatus(status) }}
                  </option>
                </select>
              </label>
              <div class="task-list-row__activity">
                <span class="meta-item"><MessageSquare :size="12" /> {{ task.commentCount }}</span>
                <span class="meta-item">▲ {{ task.upvoteCount || 0 }}</span>
                <span v-if="isTaskOverdue(task)" class="overdue-tag">Quá hạn</span>
                <div v-if="canManageTask(task)" class="task-menu-dropdown task-list-menu">
                  <button class="icon-button icon-button--small" type="button" @click.stop="toggleTaskMenu(task.id)">
                    <MoreHorizontal :size="14" />
                  </button>
                  <div v-if="activeTaskMenu === task.id" class="dropdown-content glass-card">
                    <button type="button" @click.stop="beginEditTask(task)">Sửa</button>
                    <button type="button" style="color: var(--peach-500)" @click.stop="deleteTask(task.id)">Xóa</button>
                  </div>
                </div>
              </div>
            </article>

            <div v-if="filteredTaskList.length === 0" class="empty-column-placeholder task-list-empty">
              Không có nhiệm vụ phù hợp
            </div>
          </div>
        </section>

        <Teleport to="body">
          <Transition name="task-detail-drawer">
            <div v-if="selectedTask" class="task-detail-backdrop" @click.self="closeTaskDetails">
              <aside class="task-detail-panel task-detail-drawer glass-card" role="dialog" aria-modal="true" aria-label="Chi tiết nhiệm vụ">
          <div class="panel-heading">
            <div>
              <span>Chi tiết nhiệm vụ</span>
              <h2>{{ selectedTask?.title ?? 'Chưa chọn nhiệm vụ' }}</h2>
            </div>
            <div class="task-detail-drawer__actions">
              <div v-if="selectedTask" class="task-id-badge">#{{ selectedTask.id.slice(0, 4) }}</div>
              <button type="button" class="task-detail-drawer__close" aria-label="Đóng chi tiết nhiệm vụ" @click="closeTaskDetails">
                <X :size="18" />
              </button>
            </div>
          </div>

          <div v-if="canShowTaskComments" class="comment-list">
            <div class="assignment-insight glass-card">
              <div class="section-header section-header--space">
                <strong>Gợi ý assignee AI</strong>
                <button class="ghost-pill" type="button" @click="loadAssignmentInsight">Tải gợi ý</button>
              </div>
              <p v-if="assignmentInsightLoading" class="assignment-note">Đang phân tích workload, skill và lịch sử gán việc...</p>
              <p v-else-if="assignmentInsightError" class="assignment-note assignment-note--error">{{ assignmentInsightError }}</p>
              <template v-else-if="assignmentInsight">
                <p class="assignment-note">{{ assignmentInsight.recommendationSummary }}</p>
                <div class="assignment-recommendation">
                  <strong>{{ assignmentInsight.recommendedUserName || 'Chưa có đề xuất' }}</strong>
                  <span>{{ assignmentInsight.recommendedUserId ? 'Người phù hợp nhất hiện tại' : 'Không đủ dữ liệu' }}</span>
                </div>
                <div class="assignment-candidates">
                  <article v-for="candidate in assignmentInsight.candidates.slice(0, 3)" :key="candidate.userId" class="assignment-candidate">
                    <div class="assignment-candidate__top">
                      <strong>{{ candidate.fullName }}</strong>
                      <span>{{ candidate.role }}</span>
                    </div>
                    <div class="assignment-candidate__stats">
                      <span>Điểm: {{ candidate.totalScore }}</span>
                      <span>Đang mở: {{ candidate.activeTaskCount }}</span>
                      <span>Quá hạn: {{ candidate.overdueTaskCount }}</span>
                    </div>
                  </article>
                </div>
              </template>
              <p v-else class="assignment-note">Nhấn "Tải gợi ý" để xem đề xuất dựa trên workload và lịch sử.</p>
            </div>

            <div class="time-tracking-section">
              <div class="section-header">
                <Clock :size="16" />
                <strong>Nhật ký hoạt động và giờ làm</strong>
              </div>
              
              <div class="timer-display glass-card">
                <div v-if="activeTimer" class="timer-active">
                  <div class="timer-pulse"></div>
                  <span>Ghi giờ: <strong>{{ activeTimer.taskTitle }}</strong></span>
                  <button class="stop-pill" @click="stopTimer(activeTimer.id)">
                    <Square :size="14" fill="currentColor" /> Dừng
                  </button>
                </div>
                <div v-else class="timer-idle">
                  <button class="start-pill" @click="startTimer(selectedTask.id)">
                    <Play :size="14" fill="currentColor" /> Bắt đầu
                  </button>
                  <button class="ghost-pill" @click="showManualForm = !showManualForm">Nhập tay</button>
                </div>
              </div>

              <transition name="fade">
                <div v-if="showManualForm" class="manual-log-form glass-card">
                  <div class="form-row">
                    <input v-model.number="manualMinutes" type="number" placeholder="Phút" />
                    <input v-model="manualNote" type="text" placeholder="Ghi chú..." />
                    <button class="primary-button primary-button--compact" @click="submitManualEntry">Ghi nhận</button>
                  </div>
                </div>
              </transition>

              <div v-if="timeEntries.length > 0" class="activity-timeline">
                <div v-for="entry in timeEntries.slice(0, 5)" :key="entry.id" class="timeline-item">
                  <div class="timeline-icon"><Clock :size="12" /></div>
                  <div class="timeline-content">
                    <div class="timeline-header">
                      <strong>{{ entry.userName }}</strong> đã ghi 
                      <span class="minutes-badge">{{ entry.manualMinutes || entry.totalMinutes }} phút</span>
                    </div>
                    <div v-if="entry.note" class="timeline-note">{{ entry.note }}</div>
                    <div class="timeline-time">{{ formatDate(entry.startedAt) }}</div>
                  </div>
                </div>
              </div>
            </div>

            <div class="attachment-section glass-card">
              <div class="section-header">
                <div style="display: flex; align-items: center; gap: 8px;">
                  <File :size="16" />
                  <strong>Tệp đính kèm và minh chứng</strong>
                </div>
                <label class="upload-pill">
                  <input type="file" @change="uploadAttachment" />
                  <span>+ Tải lên</span>
                </label>
              </div>
              <div class="attachment-list">
                <article v-for="attachment in attachments" :key="attachment.id" class="attachment-card">
                  <div class="attachment-card__icon">
                    <File :size="24" stroke-width="1.5" />
                  </div>
                  <div class="attachment-card__info">
                    <strong class="truncate" :title="attachment.fileName">{{ attachment.fileName }}</strong>
                    <div class="attachment-card__meta">
                      <span>{{ formatFileSize(attachment.fileSize) }}</span>
                      <span>•</span>
                      <span>{{ attachment.uploadedByName }}</span>
                    </div>
                  </div>
                  <div class="attachment-card__actions">
                    <label class="evidence-toggle" title="Đánh dấu là minh chứng">
                      <input 
                        type="checkbox" 
                        :checked="attachment.isEvidence" 
                        @change="e => markAsEvidence(attachment.id, (e.target as HTMLInputElement).checked)" 
                      />
                      <span class="slider round"></span>
                      <span class="evidence-label">Minh chứng</span>
                    </label>

                    <template v-if="attachment.isEvidence">
                      <span class="evidence-status-badge" :class="(attachment.evidenceApprovalStatus || '').toLowerCase()">
                        {{ attachment.evidenceApprovalStatus === 'Approved' ? 'Đã duyệt' : (attachment.evidenceApprovalStatus === 'Rejected' ? 'Từ chối' : 'Chờ duyệt') }}
                      </span>
                      
                      <div v-if="canReviewEvidence(attachment)" class="manager-actions">
                        <button class="approve-btn" title="Duyệt minh chứng" @click="reviewEvidence(attachment.id, true, '')">
                          <Check :size="14" /> Duyệt
                        </button>
                        <button class="reject-btn" title="Từ chối" @click="rejectEvidence(attachment.id)">
                          <Ban :size="14" /> Từ chối
                        </button>
                      </div>
                    </template>
                    
                    <button class="icon-button icon-button--small icon-button--danger" @click="deleteAttachment(attachment)" title="Xóa tệp">
                      <X :size="14" />
                    </button>
                  </div>
                </article>
                <div v-if="attachments.length === 0" class="empty-attachments">
                  Chưa có tệp đính kèm nào
                </div>
              </div>
            </div>

            <div class="discussion-section">
              <div class="section-header">
                <MessageSquare :size="16" />
                <strong>Trao đổi</strong>
              </div>
              <div class="chat-container glass-card">
                <div class="chat-scroll no-scrollbar">
                  <article
                    v-for="comment in comments"
                    :key="comment.id"
                    class="chat-message"
                    :class="{ 'chat-message--own': comment.authorId === currentUser?.id }"
                  >
                    <div class="chat-avatar">
                      {{ comment.authorName.charAt(0).toUpperCase() }}
                    </div>
                    <div class="chat-bubble-wrapper">
                      <div class="chat-bubble-meta">
                        <strong>{{ comment.authorName }}</strong>
                        <span>{{ formatTime(comment.createdAt) }}</span>
                      </div>
                      <div class="chat-bubble">
                        <div class="comment-markdown" v-html="renderMarkdown(comment.content)"></div>
                      </div>
                      <div class="chat-actions">
                        <button v-if="isProjectAdmin || comment.authorId === currentUser?.id" class="chat-action-btn" @click="deleteComment(comment.id)">Xóa</button>
                      </div>
                    </div>
                  </article>
                  <div v-if="comments.length === 0" class="empty-chat">
                    Chưa có bình luận nào
                  </div>
                </div>

                <form class="chat-input-area" @submit.prevent="submitComment">
                  <div class="chat-input-wrapper">
                    <input v-model="newComment" type="text" placeholder="Nhập tin nhắn..." />
                    <button class="chat-send-btn" type="submit" :disabled="!newComment.trim()">
                      <Send :size="16" />
                    </button>
                  </div>
                </form>
              </div>
            </div>
          </div>
          <div v-else class="empty-state-panel">
            <ClipboardList :size="48" />
            <p>{{ selectedTask?.isRestricted ? 'Bạn không có quyền xem chi tiết nhiệm vụ riêng tư này.' : 'Chọn một nhiệm vụ để xem chi tiết' }}</p>
          </div>
              </aside>
            </div>
          </Transition>
        </Teleport>
      </div>

      <div v-if="activeProjectTab === 'members'" class="tab-pane reveal">
        <ProjectMembersTab
          :members="selectedProjectMembers"
          :users="users"
          :is-admin="isProjectAdmin"
          @add="addMember"
          @remove="removeMember"
          @update-role="updateMemberRole"
          @update-permissions="updateMemberPermissions"
        />
      </div>

      <div v-if="canShowActivityTab" class="tab-pane reveal">
        <ProjectActivityTab :project-id="selectedProject.id" :project-name="selectedProject.name" />
      </div>

      <div v-if="activeProjectTab === 'wiki'" class="tab-pane reveal">
        <ProjectWikiTab :project-name="selectedProject?.name ?? ''" :is-admin="isProjectAdmin" />
      </div>

      <div v-if="canShowGanttTab" class="tab-pane reveal">
        <ProjectGanttTab
          :project-id="selectedProject.id"
          :project-members="selectedProject.members"
          @open-task="selectTaskInProject"
        />
      </div>

      <div v-if="canShowWebhooksTab" class="tab-pane reveal">
        <WebhooksTab :project-id="selectedProject.id" />
      </div>

      <ImportModal
        v-if="canShowImportModal"
        :project-id="selectedProject.id"
        :project-name="selectedProject.name"
        :project-members="selectedProject.members"
        @close="showImportModal = false"
        @imported="onImported"
      />

      <ImportUndoBanner
        v-if="undoBannerData"
        :import-session-id="undoBannerData.importSessionId"
        :imported-count="undoBannerData.importedCount"
        :failed-count="undoBannerData.failedCount"
        :duplicate-skipped-count="undoBannerData.duplicateSkippedCount"
        :created-at="undoBannerData.createdAt"
        @undo="handleUndoFromBanner"
        @dismiss="undoBannerData = null"
      />
    </div>
  </div>
</template>

<style scoped>
/* Board and drag visuals */
.project-home-main {
  gap: 24px;
  padding: 24px;
}

.project-tabs {
  width: 100%;
  justify-content: center;
}

.task-board-heading {
  display: grid;
  grid-template-columns: minmax(220px, 1fr) minmax(340px, 520px) max-content;
  gap: 12px;
  align-items: center;
}

.task-board-heading__title {
  min-width: 0;
}

.task-board-search {
  display: flex;
  justify-content: flex-end;
  min-width: 0;
}

.task-search-box {
  width: min(100%, 520px);
}

.board-actions {
  display: flex;
  justify-content: flex-end;
  align-items: center;
  gap: 10px;
  min-width: 0;
  flex-wrap: nowrap;
  white-space: nowrap;
}

.task-view-toggle {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  padding: 4px;
  border: 1px solid var(--line);
  border-radius: 12px;
  background: var(--bg-soft);
}

.task-view-toggle__button {
  min-height: 30px;
  display: inline-flex;
  align-items: center;
  gap: 6px;
  border: 0;
  border-radius: 9px;
  padding: 0 10px;
  color: var(--muted);
  background: transparent;
  font-size: 12px;
  font-weight: 800;
  cursor: pointer;
  transition: background 180ms ease, color 180ms ease, box-shadow 180ms ease;
}

.task-view-toggle__button.is-active {
  color: #ffffff;
  background: var(--primary);
  box-shadow: 0 8px 16px rgba(31, 128, 255, 0.22);
}

.task-view-toggle__button.is-active span,
.task-view-toggle__button.is-active svg {
  color: #ffffff;
}

.task-view-toggle__button svg {
  flex: 0 0 auto;
}

.task-board-heading .primary-button,
.task-board-heading .primary-button span {
  color: #ffffff;
  white-space: nowrap;
}

.task-board-heading .primary-button {
  flex: 0 0 auto;
  min-width: 118px;
  justify-content: center;
}

.task-board-heading .primary-button svg {
  color: #ffffff;
  stroke: #ffffff;
}

@media (max-width: 980px) {
  .task-board-heading {
    grid-template-columns: 1fr;
  }

  .task-board-search,
  .board-actions {
    justify-content: flex-start;
  }

  .board-actions {
    flex-wrap: wrap;
  }
}

.kanban-column__list {
  min-height: 300px;
  padding: 4px;
  border: 1px dashed transparent;
  border-radius: 18px;
  transition: border-color 0.16s ease, background 0.16s ease;
}

.kanban-column {
  position: relative;
  transition: border-color 180ms ease, background 180ms ease, box-shadow 180ms ease, opacity 180ms ease, transform 180ms ease;
}

.kanban-board.is-dragging-task .kanban-column {
  opacity: 0.72;
}

.kanban-board.is-dragging-task .kanban-column.is-drop-source {
  opacity: 1;
  border-color: rgba(100, 116, 139, 0.28);
  background: rgba(248, 250, 252, 0.86);
}

.kanban-board.is-dragging-task .kanban-column.is-drop-allowed {
  opacity: 1;
  transform: translateY(-2px);
  border-color: rgba(16, 185, 129, 0.48);
  background: linear-gradient(180deg, rgba(236, 253, 245, 0.96), rgba(255, 255, 255, 0.88));
  box-shadow: 0 18px 42px rgba(16, 185, 129, 0.14);
}

.kanban-board.is-dragging-task .kanban-column.is-drop-blocked {
  opacity: 0.48;
  border-color: rgba(148, 163, 184, 0.18);
  background: rgba(248, 250, 252, 0.52);
}

.kanban-column__list.is-drop-ready {
  border-color: var(--blue-300);
  background: rgba(37, 99, 235, 0.04);
}

.kanban-column__list.is-drop-allowed {
  border-color: rgba(16, 185, 129, 0.62);
  background: rgba(16, 185, 129, 0.08);
}

.kanban-column__list.is-drop-source {
  border-color: rgba(100, 116, 139, 0.28);
  background: rgba(100, 116, 139, 0.05);
}

.kanban-column__list.is-drop-blocked {
  border-color: rgba(148, 163, 184, 0.18);
  background: repeating-linear-gradient(
    -45deg,
    rgba(148, 163, 184, 0.055),
    rgba(148, 163, 184, 0.055) 8px,
    rgba(248, 250, 252, 0.35) 8px,
    rgba(248, 250, 252, 0.35) 16px
  );
}

.kanban-column__badges {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  flex: 0 0 auto;
}

.kanban-column__header .drop-lock-badge {
  width: 26px;
  min-width: 26px;
  height: 26px;
  display: grid;
  place-items: center;
  border: 1px solid rgba(148, 163, 184, 0.34);
  border-radius: 999px;
  color: #64748b;
  background: rgba(241, 245, 249, 0.92);
}

.kanban-column__header .drop-lock-badge svg {
  stroke-width: 2.4;
}

.kanban-board.is-dragging-task .kanban-column.is-drop-allowed .count-badge {
  color: #047857;
  background: rgba(209, 250, 229, 0.9);
}

.ghost-card {
  opacity: 0.5;
  border: 2px dashed var(--blue-300) !important;
  transform: scale(0.98);
}

.dragging-card {
  transform: rotate(2deg);
  box-shadow: 0 20px 40px rgba(15, 76, 255, 0.12) !important;
}

.count-badge {
  padding: 3px 9px;
  border: 1px solid var(--blue-200);
  border-radius: 999px;
  background: var(--blue-50);
  color: var(--primary-dark);
  font-size: 11px;
  font-weight: 800;
}

.overdue-tag {
  padding: 2px 7px;
  border: 1px solid var(--red-200);
  border-radius: 6px;
  background: var(--red-50);
  color: var(--red-600);
  font-size: 10px;
  font-weight: 700;
}

.meta-item {
  color: var(--muted);
}

.empty-column-placeholder {
  height: 100px;
  margin: 8px 0;
  display: flex;
  align-items: center;
  justify-content: center;
  border: 1px dashed var(--line);
  border-radius: 14px;
  background: var(--bg-soft);
  color: var(--muted);
  font-size: 13px;
  pointer-events: none;
}

.task-list-board {
  display: grid;
  gap: 10px;
}

.task-list-board__head,
.task-list-row {
  display: grid;
  grid-template-columns: minmax(220px, 1.6fr) minmax(130px, 0.85fr) minmax(96px, 0.65fr) minmax(88px, 0.55fr) minmax(110px, 0.7fr) minmax(150px, 0.9fr);
  gap: 14px;
  align-items: center;
}

.task-list-board__head {
  padding: 0 16px 4px;
  color: var(--muted);
  font-size: 11px;
  font-weight: 900;
  text-transform: uppercase;
}

.task-list-row {
  min-height: 72px;
  padding: 14px 16px;
  border: 1px solid var(--line);
  border-radius: 14px;
  background: var(--panel);
  cursor: pointer;
  transition: border-color 180ms ease, box-shadow 180ms ease, transform 180ms ease;
}

.task-list-row:hover,
.task-list-row.is-selected {
  border-color: var(--blue-300);
  box-shadow: 0 14px 30px rgba(15, 82, 186, 0.08);
  transform: translateY(-1px);
}

.task-list-row__main {
  min-width: 0;
  display: grid;
  gap: 4px;
}

.task-list-row__main strong {
  min-width: 0;
  color: var(--text-strong);
  font-size: 14px;
  line-height: 1.35;
  overflow-wrap: anywhere;
}

.task-list-row__main small,
.task-list-row__muted {
  color: var(--muted);
  font-size: 12px;
}

.task-status-select-wrap {
  min-width: 0;
  width: fit-content;
  max-width: 100%;
  position: relative;
}

.task-status-select {
  width: fit-content;
  max-width: 100%;
  min-height: 32px;
  border: 1px solid var(--blue-200);
  border-radius: 999px;
  padding: 5px 30px 5px 10px;
  color: var(--primary-dark);
  background: var(--blue-50);
  font-size: 12px;
  font-weight: 800;
  cursor: pointer;
  outline: none;
}

.task-status-select:hover,
.task-status-select:focus {
  border: 1px solid var(--blue-200);
  background: var(--blue-50);
  box-shadow: 0 0 0 3px rgba(31, 128, 255, 0.12);
}

.task-status-select option {
  color: var(--text-strong);
  background: var(--panel);
}

.task-status-select option:disabled {
  color: #94a3b8;
}

.task-list-row__activity {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}

.task-list-menu {
  margin-left: auto;
  position: relative;
}

.task-list-menu .dropdown-content {
  right: 0;
  left: auto;
  top: calc(100% + 6px);
}

.task-list-empty {
  pointer-events: auto;
}

@media (max-width: 1180px) {
  .task-list-board {
    overflow-x: auto;
    padding-bottom: 4px;
  }

  .task-list-board__head,
  .task-list-row {
    min-width: 900px;
  }
}

@media (max-width: 720px) {
  .task-list-board {
    overflow-x: visible;
  }

  .task-list-board__head {
    display: none;
  }

  .task-list-row {
    min-width: 0;
    grid-template-columns: 1fr;
    gap: 8px;
  }

  .task-list-row__activity {
    justify-content: flex-start;
  }
}

/* Task details */
.task-detail-panel {
  min-height: 600px;
  padding: 20px;
  display: flex;
  flex-direction: column;
  gap: 24px;
}

.task-detail-backdrop {
  position: fixed;
  inset: 0;
  z-index: 1100;
  display: flex;
  justify-content: flex-end;
  background: rgba(15, 23, 42, 0.22);
}

.task-detail-drawer {
  width: min(440px, calc(100vw - 24px));
  height: 100vh;
  min-height: 100vh;
  max-height: 100vh;
  overflow-y: auto;
  border-radius: 22px 0 0 22px;
  box-shadow: -28px 0 70px rgba(15, 23, 42, 0.18);
}

.task-detail-drawer__actions {
  display: flex;
  align-items: center;
  gap: 10px;
}

.task-detail-drawer__close {
  width: 36px;
  height: 36px;
  border: 1px solid var(--line);
  border-radius: 999px;
  display: grid;
  place-items: center;
  color: var(--muted);
  background: var(--bg-soft);
  cursor: pointer;
  transition: background 180ms ease, color 180ms ease, transform 180ms ease;
}

.task-detail-drawer__close:hover {
  color: var(--text-strong);
  background: var(--panel);
  transform: translateY(-1px);
}

.task-detail-drawer-enter-active,
.task-detail-drawer-leave-active {
  transition: opacity 180ms ease;
}

.task-detail-drawer-enter-active .task-detail-drawer,
.task-detail-drawer-leave-active .task-detail-drawer {
  transition: transform 220ms ease;
}

.task-detail-drawer-enter-from,
.task-detail-drawer-leave-to {
  opacity: 0;
}

.task-detail-drawer-enter-from .task-detail-drawer,
.task-detail-drawer-leave-to .task-detail-drawer {
  transform: translateX(100%);
}

.task-id-badge {
  padding: 6px 10px;
  border: 1px solid var(--blue-200);
  border-radius: 999px;
  color: var(--primary-dark);
  background: var(--blue-50);
  font-size: 12px;
  font-weight: 800;
}

.kanban-card {
  display: grid;
  gap: 10px;
  padding: 14px 14px 12px;
  min-height: 132px;
  border: 1px solid rgba(226, 232, 240, 0.95);
  border-radius: 16px;
  background: linear-gradient(180deg, rgba(255, 255, 255, 0.98), rgba(248, 250, 252, 0.92));
  box-shadow: 0 8px 20px rgba(15, 23, 42, 0.04);
}

.kanban-card:hover,
.kanban-card.is-selected {
  border-color: rgba(31, 128, 255, 0.26);
  box-shadow: 0 14px 28px rgba(31, 128, 255, 0.09);
}

.kanban-card__top {
  display: grid;
  gap: 10px;
  align-items: start;
}

.kanban-card__top strong {
  min-width: 0;
  display: -webkit-box;
  overflow: hidden;
  -webkit-box-orient: vertical;
  -webkit-line-clamp: 2;
  color: var(--text-strong);
  font-size: 14px;
  line-height: 1.35;
  word-break: normal;
}

.task-card-actions {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  flex-wrap: wrap;
}

.priority {
  border-radius: 999px;
  padding: 4px 9px;
  font-weight: 800;
  font-size: 11px;
}

.assignee-text {
  display: -webkit-box;
  overflow: hidden;
  -webkit-box-orient: vertical;
  -webkit-line-clamp: 1;
  color: var(--muted);
  font-size: 12px;
  line-height: 1.5;
}

.kanban-card__meta {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  align-items: center;
  color: var(--muted);
  font-size: 12px;
}

.task-menu-dropdown .dropdown-content {
  min-width: 120px;
}

.task-menu-dropdown .dropdown-content button {
  width: 100%;
  justify-content: flex-start;
}

.task-list-row {
  min-height: 68px;
  padding: 14px 16px;
  border-radius: 16px;
  background: linear-gradient(180deg, rgba(255, 255, 255, 0.98), rgba(248, 250, 252, 0.92));
}

.task-list-row__main strong {
  font-size: 15px;
  line-height: 1.35;
}

.task-list-row__activity {
  justify-content: flex-start;
}

.empty-column-placeholder {
  border-color: rgba(191, 219, 254, 0.85);
  background: rgba(248, 250, 252, 0.82);
}

.task-board-shell {
  border-radius: 24px;
  box-shadow: 0 18px 42px rgba(15, 23, 42, 0.05);
}

@media (max-width: 640px) {
  .task-detail-drawer {
    width: 100vw;
    border-radius: 0;
  }

  .kanban-card__top strong {
    -webkit-line-clamp: 3;
  }
}

.section-header {
  margin-bottom: 12px;
  display: flex;
  align-items: center;
  gap: 8px;
  color: var(--muted);
  font-size: 13px;
}

.timer-display {
  padding: 16px;
  border: 1px solid var(--line);
  border-radius: 14px;
  background: var(--panel);
}

.timer-active,
.timer-idle,
.form-row,
.entry-row {
  display: flex;
  align-items: center;
  gap: 10px;
}

.timer-pulse {
  width: 8px;
  height: 8px;
  margin-right: 2px;
  border-radius: 50%;
  background: #22d3ee;
  animation: pulse 1.6s infinite;
}

.start-pill,
.stop-pill,
.ghost-pill,
.send-pill,
.upload-pill span,
.close-pill {
  border-radius: 999px;
  cursor: pointer;
  transition: transform 220ms ease, border-color 220ms ease, background 220ms ease, box-shadow 220ms ease, color 220ms ease;
}

.start-pill,
.stop-pill {
  border: 1px solid rgba(184, 219, 255, 0.34);
  padding: 7px 14px;
  color: #f8fafc;
  font-weight: 700;
}

.start-pill {
  background: linear-gradient(135deg, #0f4cff, #22d3ee);
  box-shadow: 0 14px 26px rgba(15, 76, 255, 0.28);
}

.stop-pill {
  background: linear-gradient(135deg, #ef4444, #f97316);
  box-shadow: 0 14px 26px rgba(239, 68, 68, 0.24);
}

.ghost-pill {
  border: 1px solid rgba(182, 194, 217, 0.28);
  padding: 7px 12px;
  color: var(--muted);
  background: rgba(255, 255, 255, 0.06);
}

.start-pill:hover,
.stop-pill:hover,
.ghost-pill:hover {
  transform: translateY(-1px);
}

.manual-log-form {
  padding: 12px;
  border: 1px solid var(--line);
  border-radius: 14px;
  background: var(--panel);
}

.manual-log-form input {
  min-height: 38px;
  border: 1px solid var(--line);
  border-radius: 12px;
  padding: 0 12px;
  color: var(--text-strong);
  background: var(--panel);
}

.entry-history {
  display: grid;
  gap: 8px;
}

.entry-row {
  justify-content: space-between;
  padding: 8px 10px;
  border: 1px solid var(--line);
  border-radius: 12px;
  background: var(--bg-soft);
  color: var(--muted);
}

.minutes-badge {
  padding: 4px 8px;
  border-radius: 999px;
  background: rgba(31, 128, 255, 0.16);
  color: #d5e9ff;
  font-weight: 700;
}

.time-date {
  color: #9fb0ca;
  font-size: 12px;
}

.attachment-section {
  padding: 14px;
  border: 1px solid var(--line);
  border-radius: 16px;
  background: var(--panel);
}

.upload-pill {
  display: inline-flex;
  align-items: center;
}

.upload-pill input {
  display: none;
}

.upload-pill span {
  border: 1px solid var(--blue-200);
  padding: 5px 10px;
  color: var(--primary-dark);
  background: var(--blue-50);
  font-size: 12px;
  font-weight: 700;
}

.attachment-grid {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.file-chip {
  padding: 7px 10px;
  display: flex;
  align-items: center;
  gap: 8px;
  border: 1px solid var(--line);
  border-radius: 12px;
  background: var(--bg-soft);
}

.file-info {
  display: flex;
  flex-direction: column;
  line-height: 1.2;
}

.file-info span {
  font-size: 10px;
  color: var(--muted);
}

.close-pill {
  width: 24px;
  height: 24px;
  border: 1px solid var(--line);
  color: var(--muted);
  background: var(--bg-soft);
}

.truncate {
  max-width: 120px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.discussion-section {
  display: grid;
  gap: 10px;
}

.comments-scroll {
  max-height: 340px;
  overflow-y: auto;
  padding-right: 2px;
}

.comment-bubble {
  margin-bottom: 10px;
  padding: 12px 14px;
  border: 1px solid var(--line);
  border-radius: 14px 14px 14px 6px;
  background: var(--bg-soft);
}

.comment-bubble--reply {
  margin-left: 14px;
  border-left: 2px solid rgba(117, 182, 255, 0.44);
}

.bubble-top {
  display: flex;
  justify-content: space-between;
  margin-bottom: 4px;
  color: var(--muted);
  font-size: 12px;
}

.bubble-time {
  color: #8ca0bf;
}

.comment-markdown {
  color: var(--text-strong);
}

.comment-markdown :deep(*) {
  color: inherit;
}

.comment-votes {
  margin-top: 6px;
  color: #9fb0ca;
  font-size: 12px;
}

.bubble-delete {
  margin-top: 4px;
  border: 0;
  background: transparent;
  color: #fda4af;
  font-size: 11px;
  cursor: pointer;
  padding: 0;
}

.comment-input-area {
  margin-top: 10px;
  display: flex;
  align-items: center;
  gap: 12px;
}

.comment-input-area input {
  flex: 1;
  min-height: 42px;
  border: 1px solid var(--line);
  border-radius: 999px;
  padding: 0 16px;
  color: var(--text-strong);
  background: var(--panel);
  outline: none;
}

.send-pill {
  width: 40px;
  height: 40px;
  border: 1px solid rgba(184, 219, 255, 0.4);
  color: white;
  background: linear-gradient(135deg, #0f4cff, #1f80ff);
  display: flex;
  align-items: center;
  justify-content: center;
}

.send-pill:hover:not(:disabled) {
  transform: translateY(-1px);
  box-shadow: 0 14px 24px rgba(15, 76, 255, 0.34);
}

.send-pill:disabled {
  opacity: 0.54;
}

.empty-state-panel {
  min-height: 400px;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 8px;
  border: 1px dashed var(--line);
  border-radius: 16px;
  color: var(--muted);
  background: var(--bg-soft);
}

.task-option-toggle {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  color: var(--muted);
  font-size: 12px;
  font-weight: 700;
  white-space: nowrap;
}

.task-option-toggle input {
  accent-color: #1f80ff;
}

.assignment-insight {
  padding: 14px;
  border: 1px solid var(--line);
  border-radius: 16px;
  background: var(--panel);
}

.section-header--space {
  justify-content: space-between;
  align-items: center;
}

.assignment-note {
  margin: 0 0 12px;
  color: var(--muted);
  font-size: 12px;
}

.assignment-note--error {
  color: #fca5a5;
}

.assignment-recommendation {
  display: flex;
  justify-content: space-between;
  gap: 12px;
  margin-bottom: 12px;
  color: var(--text-strong);
}

.assignment-candidates {
  display: grid;
  gap: 8px;
}

.assignment-candidate {
  padding: 10px 12px;
  border: 1px solid var(--line);
  border-radius: 12px;
  background: var(--panel);
}

.assignment-candidate__top,
.assignment-candidate__stats {
  display: flex;
  justify-content: space-between;
  gap: 12px;
  flex-wrap: wrap;
}

.assignment-candidate__top span,
.assignment-candidate__stats {
  color: var(--muted);
  font-size: 12px;
}

.quick-edit-input {
  min-height: 30px;
  border: 1px solid var(--line);
  border-radius: 8px;
  padding: 4px 8px;
  color: var(--text-strong);
  background: var(--bg-soft);
}

.assignee-text {
  color: #9fb0ca;
}

.import-btn-sm {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 5px;
  min-width: 92px;
  padding: 6px 12px;
  border: 1px solid var(--line);
  border-radius: 10px;
  background: var(--panel);
  color: var(--text-strong);
  font-size: 12px;
  font-weight: 700;
  white-space: nowrap;
  cursor: pointer;
  transition: transform 220ms ease, border-color 220ms ease, background 220ms ease, box-shadow 220ms ease, color 220ms ease;
}

.import-btn-sm:hover {
  transform: translateY(-1px);
  border-color: var(--line);
  background: var(--bg-soft);
  box-shadow: 0 10px 20px rgba(15, 76, 255, 0.08);
}

/* Animations */
.expand-enter-active,
.expand-leave-active {
  transition: all 280ms ease;
}

.expand-enter-from,
.expand-leave-to {
  opacity: 0;
  transform: translateY(-10px);
}

.fade-enter-active,
.fade-leave-active {
  transition: opacity 220ms ease;
}

.fade-enter-from,
.fade-leave-to {
  opacity: 0;
}

@keyframes pulse {
  0% {
    transform: scale(0.95);
    box-shadow: 0 0 0 0 rgba(34, 211, 238, 0.46);
  }
  70% {
    transform: scale(1);
    box-shadow: 0 0 0 10px rgba(34, 211, 238, 0);
  }
  100% {
    transform: scale(0.95);
    box-shadow: 0 0 0 0 rgba(34, 211, 238, 0);
  }
}
/* Activity Timeline */
.activity-timeline {
  display: flex;
  flex-direction: column;
  gap: 16px;
  margin-top: 10px;
  position: relative;
}

.activity-timeline::before {
  content: "";
  position: absolute;
  left: 11px;
  top: 8px;
  bottom: 8px;
  width: 2px;
  background: var(--line);
  z-index: 0;
}

.timeline-item {
  display: flex;
  gap: 12px;
  position: relative;
  z-index: 1;
}

.timeline-icon {
  width: 24px;
  height: 24px;
  border-radius: 50%;
  background: var(--panel);
  border: 2px solid var(--line);
  display: flex;
  align-items: center;
  justify-content: center;
  color: var(--muted);
  flex-shrink: 0;
}

.timeline-content {
  display: flex;
  flex-direction: column;
  gap: 4px;
  padding-top: 2px;
}

.timeline-header {
  font-size: 13px;
  color: var(--text-strong);
}

.timeline-note {
  font-size: 12px;
  color: var(--muted);
  background: var(--bg-soft);
  padding: 6px 10px;
  border-radius: 8px;
  border: 1px solid var(--line);
}

.timeline-time {
  font-size: 11px;
  color: #8ca0bf;
}

/* Attachments */
.attachment-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.attachment-card {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 12px;
  background: var(--panel);
  border: 1px solid var(--line);
  border-radius: 12px;
  transition: transform 200ms ease, box-shadow 200ms ease;
}

.attachment-card:hover {
  transform: translateY(-2px);
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.05);
}

.attachment-card__icon {
  width: 40px;
  height: 40px;
  border-radius: 8px;
  background: var(--bg-soft);
  color: var(--primary);
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}

.attachment-card__info {
  flex: 1;
  display: flex;
  flex-direction: column;
  min-width: 0;
}

.attachment-card__info strong {
  font-size: 14px;
  color: var(--text-strong);
}

.attachment-card__meta {
  display: flex;
  gap: 6px;
  font-size: 11px;
  color: var(--muted);
  margin-top: 2px;
}

.attachment-card__actions {
  display: flex;
  align-items: center;
  gap: 10px;
}

/* Toggle Switch */
.evidence-toggle {
  position: relative;
  display: inline-flex;
  align-items: center;
  cursor: pointer;
  gap: 6px;
}

.evidence-toggle input {
  opacity: 0;
  width: 0;
  height: 0;
}

.slider {
  position: relative;
  width: 32px;
  height: 18px;
  background-color: #cbd5e1;
  transition: .4s;
  border-radius: 34px;
}

.slider:before {
  position: absolute;
  content: "";
  height: 14px;
  width: 14px;
  left: 2px;
  bottom: 2px;
  background-color: white;
  transition: .4s;
  border-radius: 50%;
}

.evidence-toggle input:checked + .slider {
  background-color: #10b981;
}

.evidence-toggle input:checked + .slider:before {
  transform: translateX(14px);
}

.evidence-label {
  font-size: 12px;
  font-weight: 600;
  color: var(--muted);
}

.evidence-toggle input:checked ~ .evidence-label {
  color: #10b981;
}

.evidence-status-badge {
  padding: 4px 8px;
  border-radius: 6px;
  font-size: 11px;
  font-weight: 700;
  white-space: nowrap;
}

.evidence-status-badge.approved { background: rgba(16, 185, 129, 0.15); color: #059669; }
.evidence-status-badge.rejected { background: rgba(239, 68, 68, 0.15); color: #dc2626; }
.evidence-status-badge.pending,
.evidence-status-badge { background: rgba(245, 158, 11, 0.15); color: #d97706; }

.manager-actions {
  display: flex;
  gap: 4px;
}

.approve-btn, .reject-btn {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  padding: 4px 8px;
  border-radius: 6px;
  font-size: 11px;
  font-weight: 600;
  cursor: pointer;
  border: 1px solid transparent;
  transition: all 0.2s;
}

.approve-btn {
  background: rgba(16, 185, 129, 0.1);
  color: #059669;
}
.approve-btn:hover { background: #10b981; color: white; }

.reject-btn {
  background: rgba(239, 68, 68, 0.1);
  color: #dc2626;
}
.reject-btn:hover { background: #ef4444; color: white; }

.empty-attachments, .empty-chat {
  text-align: center;
  padding: 24px;
  color: var(--muted);
  font-size: 13px;
  border: 1px dashed var(--line);
  border-radius: 12px;
}

/* Chat Redesign */
.chat-container {
  display: flex;
  flex-direction: column;
  height: 400px;
  border: 1px solid var(--line);
  border-radius: 16px;
  background: var(--panel);
  overflow: hidden;
}

.chat-scroll {
  flex: 1;
  overflow-y: auto;
  padding: 16px;
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.chat-message {
  display: flex;
  gap: 12px;
  max-width: 85%;
}

.chat-message--own {
  align-self: flex-end;
  flex-direction: row-reverse;
}

.chat-avatar {
  width: 32px;
  height: 32px;
  border-radius: 50%;
  background: linear-gradient(135deg, #1e293b, #334155);
  color: white;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 14px;
  font-weight: 700;
  flex-shrink: 0;
}

.chat-message--own .chat-avatar {
  background: linear-gradient(135deg, #0f4cff, #22d3ee);
}

.chat-bubble-wrapper {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.chat-message--own .chat-bubble-wrapper {
  align-items: flex-end;
}

.chat-bubble-meta {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 11px;
}

.chat-bubble-meta strong {
  color: var(--text-strong);
}

.chat-bubble-meta span {
  color: #94a3b8;
}

.chat-message--own .chat-bubble-meta {
  flex-direction: row-reverse;
}

.chat-bubble {
  background: var(--bg-soft);
  padding: 10px 14px;
  border-radius: 0 16px 16px 16px;
  color: var(--text-strong);
  font-size: 13px;
  box-shadow: 0 2px 4px rgba(0,0,0,0.02);
}

.chat-message--own .chat-bubble {
  background: #0f4cff;
  color: white;
  border-radius: 16px 0 16px 16px;
}

.chat-message--own .comment-markdown :deep(*) {
  color: white;
}

.chat-actions {
  display: flex;
  gap: 8px;
  opacity: 0;
  transition: opacity 0.2s;
}

.chat-message:hover .chat-actions {
  opacity: 1;
}

.chat-action-btn {
  font-size: 10px;
  color: #f87171;
  background: none;
  border: none;
  cursor: pointer;
  padding: 0;
}

.chat-input-area {
  padding: 12px;
  border-top: 1px solid var(--line);
  background: var(--bg-soft);
}

.chat-input-wrapper {
  display: flex;
  gap: 8px;
  align-items: center;
  background: var(--panel);
  border: 1px solid var(--line);
  border-radius: 24px;
  padding: 4px 4px 4px 16px;
}

.chat-input-wrapper input {
  flex: 1;
  border: none;
  background: transparent;
  outline: none;
  color: var(--text-strong);
  font-size: 13px;
}

.chat-send-btn {
  width: 32px;
  height: 32px;
  border-radius: 50%;
  border: none;
  background: linear-gradient(135deg, #0f4cff, #22d3ee);
  color: white;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  transition: transform 0.2s;
}

.chat-send-btn:hover:not(:disabled) {
  transform: scale(1.05);
}

.chat-send-btn:disabled {
  background: #cbd5e1;
  cursor: not-allowed;
}

.icon-button--danger {
  color: #ef4444;
}
.icon-button--danger:hover {
  background: rgba(239, 68, 68, 0.1);
}

/* Modal Styles */
.task-modal-backdrop {
  position: fixed; inset: 0; z-index: 9999;
  background: rgba(15, 23, 42, 0.4);
  backdrop-filter: blur(8px);
  -webkit-backdrop-filter: blur(8px);
  display: flex; align-items: center; justify-content: center;
  animation: fadeIn 0.3s cubic-bezier(0.16, 1, 0.3, 1);
}

.task-modal {
  --accent: #2563eb;
  --accent-hover: #1d4ed8;
  --text-main: #0f172a;
  --text-muted: #64748b;
  --border-color: #e2e8f0;
  
  width: min(520px, 94vw);
  max-height: 88vh;
  overflow-y: auto;
  border-radius: 20px;
  padding: 0;
  background: #ffffff;
  box-shadow: 
    0 10px 40px -10px rgba(0,0,0,0.1), 
    0 0 0 1px rgba(0,0,0,0.05);
  transform-origin: center;
  animation: modalScaleIn 0.4s cubic-bezier(0.16, 1, 0.3, 1);
}

.task-modal-header {
  display: flex; align-items: center; justify-content: space-between;
  padding: 24px 28px 20px;
  border-bottom: 1px solid var(--border-color);
}

.task-modal-title { 
  display: flex; align-items: center; gap: 12px;
  color: var(--text-main);
}
.task-modal-title h2 { 
  font-size: 1.25rem; font-weight: 700; margin: 0;
  letter-spacing: -0.01em;
}

.icon-button { 
  background: transparent; 
  border: none; 
  color: var(--text-muted); 
  cursor: pointer; 
  transition: all 0.2s ease; 
  display: flex; align-items: center; justify-content: center; 
  padding: 8px; 
  border-radius: 50%; 
}
.icon-button:hover { 
  background: #f1f5f9; 
  color: var(--text-main); 
}

.task-modal-body {
  padding: 24px 28px 28px;
}

.form-group {
  margin-bottom: 24px;
}
.form-group label {
  display: block; 
  font-size: 0.875rem; 
  font-weight: 600; 
  color: var(--text-main); 
  margin-bottom: 8px;
}

.modal-grid-2 {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 16px;
}

.modal-input {
  width: 100%; 
  padding: 12px 16px; 
  border-radius: 12px; 
  font-size: 0.95rem;
  background: #f8fafc; 
  border: 1px solid var(--border-color);
  color: var(--text-main); 
  outline: none; 
  transition: all 0.2s ease;
  box-sizing: border-box;
}
.modal-input:hover {
  background: #ffffff;
  border-color: #cbd5e1;
}
.modal-input:focus { 
  background: #ffffff;
  border-color: var(--accent); 
  box-shadow: 0 0 0 4px rgba(37, 99, 235, 0.1); 
}
.modal-input::placeholder { color: #94a3b8; }

textarea.modal-input { 
  resize: vertical; 
  min-height: 90px;
  line-height: 1.5;
}

.modal-options-row {
  display: flex;
  flex-wrap: wrap;
  gap: 16px;
  margin-bottom: 24px;
  padding: 12px;
  background: #f8fafc;
  border-radius: 12px;
  border: 1px solid var(--border-color);
}

.modal-checkbox {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 0.875rem;
  font-weight: 500;
  color: var(--text-main);
  cursor: pointer;
}
.modal-checkbox input[type="checkbox"] {
  width: 18px;
  height: 18px;
  accent-color: var(--accent);
  cursor: pointer;
}

.task-modal-actions {
  display: flex; justify-content: flex-end; gap: 12px;
  padding-top: 16px;
  border-top: 1px solid var(--border-color);
}

.btn {
  display: inline-flex; align-items: center; gap: 8px;
  padding: 10px 24px; border-radius: 10px; font-size: 0.95rem;
  font-weight: 600; border: none; cursor: pointer; transition: all 0.2s;
}
.btn--primary { 
  background: var(--accent); 
  color: #ffffff; 
  box-shadow: 0 2px 8px -2px rgba(37, 99, 235, 0.4);
}
.btn--primary:hover:not(:disabled) { 
  background: var(--accent-hover); 
  transform: translateY(-1px);
  box-shadow: 0 4px 12px -2px rgba(37, 99, 235, 0.5);
}
.btn--primary:active:not(:disabled) {
  transform: translateY(0);
}
.btn--primary:disabled { 
  opacity: 0.6; cursor: not-allowed; 
  background: #94a3b8;
  box-shadow: none;
}

.btn--ghost {
  background: transparent; 
  color: var(--text-muted);
  border: 1px solid var(--border-color);
}
.btn--ghost:hover { 
  background: #f8fafc; 
  color: var(--text-main); 
  border-color: #cbd5e1;
}

@keyframes fadeIn { 
  from { opacity: 0; backdrop-filter: blur(0px); } 
  to { opacity: 1; backdrop-filter: blur(8px); } 
}
@keyframes modalScaleIn {
  from { opacity: 0; transform: scale(0.96) translateY(10px); }
  to { opacity: 1; transform: scale(1) translateY(0); }
}
</style>
