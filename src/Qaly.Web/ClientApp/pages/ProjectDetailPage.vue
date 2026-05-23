<script setup lang="ts">
import { MessageSquare, MoreHorizontal, Plus, Send, Search, Clock, Play, Square, Calendar, X, ClipboardList, FileSpreadsheet } from 'lucide-vue-next'
// @ts-ignore
import { VueDraggable } from '../utils/vendor/vue-draggable-plus.js'
import ProjectDetailHeader from '../components/ProjectDetailHeader.vue'
import ProjectMembersTab from '../components/ProjectMembersTab.vue'
import ProjectStatsTab from '../components/ProjectStatsTab.vue'
import ProjectWikiTab from '../components/ProjectWikiTab.vue'
import ProjectGanttTab from '../components/ProjectGanttTab.vue'
import WebhooksTab from '../components/WebhooksTab.vue'
import ImportModal from '../components/import/ImportModal.vue'
import ImportUndoBanner from '../components/import/ImportUndoBanner.vue'
import { useDashboardContext } from '../composables/dashboard-context'
import { ref, onMounted, onUnmounted, watch } from 'vue'
import { apiResult } from '../utils/api-client'
import type { DashboardTask, TaskAssignmentInsightDto } from '../types'
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
  moveTask,
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
  statusTone,
  statusColumns,
  submitComment,
  tabs,
  tasksByStatus,
  toggleTaskMenu,
  updateMemberRole,
  updateMemberPermissions,
  uploadAttachment,
  users,
  taskSearchQuery,
  taskBeingQuickEditedId,
  timeEntries,
  activeTimer,
  startTimer,
  stopTimer,
  loadDashboard,
} = useDashboardContext()

const showImportModal = ref(false)
const undoBannerData = ref<{ importSessionId: string; importedCount: number; createdAt: string } | null>(null)
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
  if (result) {
    undoBannerData.value = {
      importSessionId: result.importSessionId,
      importedCount: result.importedCount,
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

const quickEditTitle = ref('')
const manualMinutes = ref<number>(0)
const manualNote = ref('')
const showManualForm = ref(false)
const markdown = new MarkdownIt({ linkify: true, breaks: true })

function renderMarkdown(value: string) {
  return DOMPurify.sanitize(markdown.render(value || ''))
}

function startQuickEdit(task: DashboardTask) {
  taskBeingQuickEditedId.value = task.id
  quickEditTitle.value = task.title
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

const onDragEnd = async (evt: { item: HTMLElement; to: HTMLElement; from: HTMLElement }) => {
  const taskId = evt.item.getAttribute('data-id')
  const newStatus = evt.to.getAttribute('data-status')
  if (!taskId || !newStatus || evt.to === evt.from || !statusColumns.includes(newStatus)) return

  const task = selectedProject.value?.tasks.find((t: DashboardTask) => t.id === taskId)
  if (!task || task.status === newStatus) return

  await moveTask(task, newStatus)
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

      <div v-if="activeProjectTab === 'tasks'">
        <section id="tasks" class="task-board-shell glass-card">
          <div class="panel-heading">
            <div>
              <span>Nhiệm vụ</span>
              <h2>Bảng công việc</h2>
            </div>
            <div class="board-actions">
              <div class="search-box">
                <Search :size="16" />
                <input v-model="taskSearchQuery" type="text" placeholder="Tìm nhiệm vụ (N: mới)..." />
              </div>
              <button class="primary-button primary-button--compact" type="button" @click="createTaskOpen = !createTaskOpen">
                <Plus :size="16" />
                <span>Nhiệm vụ</span>
              </button>
              <button class="import-btn-sm" type="button" @click="showImportModal = true">
                <FileSpreadsheet :size="14" /> Nhập file
              </button>
            </div>
          </div>

          <transition name="expand">
            <form v-if="createTaskOpen" class="task-create-form glass-card" @submit.prevent="createTask">
              <input v-model="newTaskTitle" type="text" placeholder="Tiêu đề nhiệm vụ" required />
              <input v-model="newTaskDescription" type="text" placeholder="Mô tả" />
              <select v-model="newTaskPriority">
                <option v-for="priority in priorities" :key="priority" :value="priority">{{ priority }}</option>
              </select>
              <select v-model="newTaskAssigneeId">
                <option value="">Chưa giao</option>
                <option v-for="user in selectedProjectMembers" :key="user.id" :value="user.id">{{ user.fullName }}</option>
              </select>
              <input v-model="newTaskDueDate" type="date" />
              <label class="task-option-toggle">
                <input v-model="newTaskIsPrivate" type="checkbox" />
                <span>Riêng tư</span>
              </label>
              <label class="task-option-toggle">
                <input v-model="newTaskContributesToProgress" type="checkbox" />
                <span>Tính tiến độ</span>
              </label>
              <label v-if="isProjectAdmin" class="task-option-toggle">
                <input v-model="newTaskIsPinned" type="checkbox" />
                <span>Ghim</span>
              </label>
              <button class="primary-button primary-button--compact" type="submit">Tạo</button>
            </form>
          </transition>

          <div class="kanban-board">
            <section v-for="status in statusColumns" :key="status" class="kanban-column">
              <div class="kanban-column__header">
                <strong>{{ displayStatus(status) }}</strong>
                <span class="count-badge">{{ tasksByStatus(status).length }}</span>
              </div>

              <VueDraggable
                :model-value="tasksByStatus(status)"
                :animation="200"
                draggable=".kanban-card"
                group="tasks"
                ghost-class="ghost-card"
                drag-class="dragging-card"
                class="kanban-column__list"
                :data-status="status"
                @end="onDragEnd"
              >
                <article
                  v-for="task in tasksByStatus(status)"
                  :key="task.id"
                  class="kanban-card draggable-item"
                  :class="{ 'is-selected': selectedTask?.id === task.id }"
                  :data-id="task.id"
                  @click="selectTaskInProject(task.id)"
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
                    <strong v-else @dblclick.stop="!task.isRestricted && startQuickEdit(task)">
                      <span v-if="task.isPrivate" title="Nhiệm vụ riêng tư">Khóa</span>
                      <span v-if="task.isPinned" title="Nhiệm vụ đã ghim">Ghim</span>
                      {{ task.title }}
                    </strong>
                    
                    <div class="task-card-actions">
                      <span :class="`priority priority--${task.priority.toLowerCase()}`">{{ task.priority }}</span>

                      <div v-if="isProjectAdmin && !task.isRestricted" class="task-menu-dropdown">
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
                <div v-if="tasksByStatus(status).length === 0" class="empty-column-placeholder">Thả nhiệm vụ vào đây</div>
              </VueDraggable>
            </section>
          </div>
        </section>

        <section class="task-detail-panel glass-card">
          <div class="panel-heading">
            <div>
              <span>Chi tiết nhiệm vụ</span>
              <h2>{{ selectedTask?.title ?? 'Chưa chọn nhiệm vụ' }}</h2>
            </div>
            <div v-if="selectedTask" class="task-id-badge">#{{ selectedTask.id.slice(0, 4) }}</div>
          </div>

          <div v-if="selectedTask && !selectedTask.isRestricted" class="comment-list">
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
                <strong>Nhật ký hoạt động</strong>
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

              <div v-if="timeEntries.length > 0" class="entry-history">
                <div v-for="entry in timeEntries.slice(0, 3)" :key="entry.id" class="entry-row">
                  <span>{{ entry.userName }}</span>
                  <span class="minutes-badge">{{ entry.manualMinutes || entry.totalMinutes }}m</span>
                  <span class="time-date">{{ formatDate(entry.startedAt) }}</span>
                </div>
              </div>
            </div>

            <div class="attachment-section glass-card">
              <div class="section-header">
                <strong>Tệp đính kèm</strong>
                <label class="upload-pill">
                  <input type="file" @change="uploadAttachment" />
                  <span>+ Thêm</span>
                </label>
              </div>
              <div class="attachment-grid">
                <article v-for="attachment in attachments" :key="attachment.id" class="file-chip">
                  <div class="file-info">
                    <strong class="truncate">{{ attachment.fileName }}</strong>
                    <span>{{ formatFileSize(attachment.fileSize) }}</span>
                  </div>
                  <button class="close-pill" @click="deleteAttachment(attachment)"><X :size="12" /></button>
                </article>
              </div>
            </div>

            <div class="discussion-section">
              <div class="section-header"><strong>Trao đổi</strong></div>
              <div class="comments-scroll">
                <article
                  v-for="comment in comments"
                  :key="comment.id"
                  class="comment-bubble"
                  :class="{ 'comment-bubble--reply': comment.parentCommentId }"
                >
                  <div class="bubble-top">
                    <strong>{{ comment.authorName }}</strong>
                    <span class="bubble-time">{{ formatTime(comment.createdAt) }}</span>
                  </div>
                  <div class="comment-markdown" v-html="renderMarkdown(comment.content)"></div>
                  <div class="comment-votes">▲ {{ comment.upvoteCount || 0 }} · ▼ {{ comment.downvoteCount || 0 }}</div>
                  <button v-if="isProjectAdmin || comment.authorId === currentUser?.id" class="bubble-delete" @click="deleteComment(comment.id)">Xóa</button>
                </article>
              </div>

              <form class="comment-input-area" @submit.prevent="submitComment">
                <input v-model="newComment" type="text" placeholder="Nhập bình luận..." />
                <button class="send-pill" type="submit" :disabled="!newComment.trim()"><Send :size="16" /></button>
              </form>
            </div>
          </div>
          <div v-else class="empty-state-panel">
            <ClipboardList :size="48" />
            <p>{{ selectedTask?.isRestricted ? 'Bạn không có quyền xem chi tiết nhiệm vụ riêng tư này.' : 'Chọn một nhiệm vụ để xem chi tiết' }}</p>
          </div>
        </section>
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

      <div v-if="activeProjectTab === 'wiki'" class="tab-pane reveal">
        <ProjectWikiTab :project-name="selectedProject?.name ?? ''" :is-admin="isProjectAdmin" />
      </div>

      <div v-if="activeProjectTab === 'gantt' && selectedProject" class="tab-pane reveal">
        <ProjectGanttTab
          :project-id="selectedProject.id"
          :project-members="selectedProject.members"
          @open-task="selectTaskInProject"
        />
      </div>

      <div v-if="activeProjectTab === 'webhooks' && selectedProject" class="tab-pane reveal">
        <WebhooksTab :project-id="selectedProject.id" />
      </div>

      <ImportModal
        v-if="showImportModal && selectedProject"
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

.kanban-column__list {
  min-height: 300px;
  padding: 4px;
}

.ghost-card {
  opacity: 0.42;
  border: 2px dashed rgba(117, 182, 255, 0.66) !important;
  transform: scale(0.98);
}

.dragging-card {
  transform: rotate(2deg);
  box-shadow: 0 20px 40px rgba(2, 8, 23, 0.5) !important;
}

.count-badge {
  padding: 3px 9px;
  border: 1px solid rgba(117, 182, 255, 0.44);
  border-radius: 999px;
  background: rgba(31, 128, 255, 0.18);
  color: #d9ecff;
  font-size: 11px;
  font-weight: 800;
}

.overdue-tag {
  padding: 2px 7px;
  border: 1px solid rgba(239, 68, 68, 0.42);
  border-radius: 6px;
  background: rgba(239, 68, 68, 0.16);
  color: #fecaca;
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
  border: 1px dashed rgba(182, 194, 217, 0.36);
  border-radius: 14px;
  background: rgba(255, 255, 255, 0.03);
  color: var(--muted);
  font-size: 13px;
}

/* Task details */
.task-detail-panel {
  min-height: 600px;
  padding: 24px;
  display: flex;
  flex-direction: column;
  gap: 24px;
}

.task-id-badge {
  padding: 6px 10px;
  border: 1px solid rgba(117, 182, 255, 0.42);
  border-radius: 999px;
  color: #d9edff;
  background: rgba(31, 128, 255, 0.16);
  font-size: 12px;
  font-weight: 800;
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
  border: 1px solid rgba(117, 182, 255, 0.3);
  border-radius: 14px;
  background: rgba(255, 255, 255, 0.04);
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
  border: 1px solid rgba(182, 194, 217, 0.24);
  border-radius: 14px;
  background: rgba(255, 255, 255, 0.04);
}

.manual-log-form input {
  min-height: 38px;
  border: 1px solid rgba(182, 194, 217, 0.24);
  border-radius: 12px;
  padding: 0 12px;
  color: var(--surface-milk);
  background: rgba(8, 21, 39, 0.76);
}

.entry-history {
  display: grid;
  gap: 8px;
}

.entry-row {
  justify-content: space-between;
  padding: 8px 10px;
  border: 1px solid rgba(182, 194, 217, 0.2);
  border-radius: 12px;
  background: rgba(255, 255, 255, 0.04);
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
  border: 1px solid rgba(182, 194, 217, 0.24);
  border-radius: 16px;
  background: rgba(255, 255, 255, 0.04);
}

.upload-pill {
  display: inline-flex;
  align-items: center;
}

.upload-pill input {
  display: none;
}

.upload-pill span {
  border: 1px solid rgba(117, 182, 255, 0.4);
  padding: 5px 10px;
  color: #dbefff;
  background: rgba(31, 128, 255, 0.16);
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
  border: 1px solid rgba(182, 194, 217, 0.24);
  border-radius: 12px;
  background: rgba(255, 255, 255, 0.05);
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
  border: 1px solid rgba(182, 194, 217, 0.24);
  color: var(--muted);
  background: rgba(255, 255, 255, 0.05);
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
  border: 1px solid rgba(182, 194, 217, 0.2);
  border-radius: 14px 14px 14px 6px;
  background: rgba(255, 255, 255, 0.05);
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
  color: var(--surface-milk);
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
  border: 1px solid rgba(182, 194, 217, 0.24);
  border-radius: 999px;
  padding: 0 16px;
  color: var(--surface-milk);
  background: rgba(8, 21, 39, 0.76);
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
  border: 1px dashed rgba(182, 194, 217, 0.34);
  border-radius: 16px;
  color: var(--muted);
  background: rgba(255, 255, 255, 0.03);
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
  border: 1px solid rgba(182, 194, 217, 0.24);
  border-radius: 16px;
  background: rgba(255, 255, 255, 0.04);
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
  color: var(--surface-milk);
}

.assignment-candidates {
  display: grid;
  gap: 8px;
}

.assignment-candidate {
  padding: 10px 12px;
  border: 1px solid rgba(182, 194, 217, 0.18);
  border-radius: 12px;
  background: rgba(8, 21, 39, 0.55);
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
  border: 1px solid rgba(117, 182, 255, 0.4);
  border-radius: 8px;
  padding: 4px 8px;
  color: var(--surface-milk);
  background: rgba(8, 21, 39, 0.8);
}

.assignee-text {
  color: #9fb0ca;
}

.import-btn-sm {
  display: inline-flex;
  align-items: center;
  gap: 5px;
  padding: 6px 12px;
  border: 1px solid rgba(184, 219, 255, 0.34);
  border-radius: 10px;
  background: rgba(255, 255, 255, 0.08);
  color: #d9e9ff;
  font-size: 12px;
  font-weight: 700;
  cursor: pointer;
  transition: transform 220ms ease, border-color 220ms ease, background 220ms ease, box-shadow 220ms ease, color 220ms ease;
}

.import-btn-sm:hover {
  transform: translateY(-1px);
  border-color: rgba(117, 182, 255, 0.62);
  background: rgba(31, 128, 255, 0.2);
  box-shadow: 0 14px 28px rgba(15, 76, 255, 0.24);
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
</style>
